import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';
import { CallToolResultSchema, ErrorCode, McpError, } from '@modelcontextprotocol/sdk/types.js';
const CLOSED_MESSAGE = 'Official Unity MCP client is closed.';
export class OfficialUnityMcpClient {
    options;
    sessionFactory;
    startTeardowns = new WeakMap();
    closeSignal;
    signalClose;
    active;
    activeStart;
    startupPromise;
    teardownPromise;
    closePromise;
    connectionState = 'disconnected';
    closed = false;
    constructor(options) {
        this.options = {
            cliPath: options.cliPath,
            projectPath: options.projectPath,
        };
        this.sessionFactory = options.sessionFactory ?? createOfficialUnitySessionStart;
        this.closeSignal = new Promise((resolve) => {
            this.signalClose = resolve;
        });
    }
    get state() {
        return this.connectionState;
    }
    async readTool(name, args) {
        this.assertOpen();
        const firstConnection = await this.getConnection();
        try {
            return await this.raceWithClose(firstConnection.session.callTool(name, args));
        }
        catch (firstError) {
            if (!isTransportInterruption(firstError)) {
                throw firstError;
            }
            await this.discardConnection(firstConnection);
            this.assertOpen();
            const retryConnection = await this.getConnection();
            try {
                return await this.raceWithClose(retryConnection.session.callTool(name, args));
            }
            catch (retryError) {
                if (isTransportInterruption(retryError)) {
                    await this.discardConnection(retryConnection);
                }
                throw retryError;
            }
        }
    }
    close() {
        if (this.closePromise)
            return this.closePromise;
        this.closed = true;
        this.connectionState = 'closed';
        this.signalClose();
        const start = this.activeStart;
        const teardown = this.teardownPromise;
        this.active = undefined;
        this.activeStart = undefined;
        const pending = new Set();
        if (teardown)
            pending.add(teardown);
        if (start)
            pending.add(this.closeStart(start));
        this.closePromise = Promise.all([...pending]).then(() => undefined);
        return this.closePromise;
    }
    async getConnection() {
        this.assertOpen();
        if (this.teardownPromise) {
            await this.raceWithClose(this.teardownPromise);
            this.assertOpen();
        }
        if (this.active)
            return this.active;
        if (this.startupPromise) {
            return this.raceWithClose(this.startupPromise);
        }
        this.connectionState = 'connecting';
        let start;
        try {
            start = this.sessionFactory(this.options);
        }
        catch (error) {
            this.connectionState = 'disconnected';
            throw error;
        }
        this.activeStart = start;
        const pending = start.ready
            .then((session) => {
            if (this.closed || this.activeStart !== start) {
                throw new Error(CLOSED_MESSAGE);
            }
            const connection = { start, session };
            this.active = connection;
            this.connectionState = 'connected';
            return connection;
        })
            .catch(async (error) => {
            if (this.activeStart === start) {
                this.activeStart = undefined;
                if (!this.closed) {
                    this.connectionState = 'disconnected';
                }
            }
            await this.trackTeardown(start);
            throw error;
        })
            .finally(() => {
            if (this.startupPromise === pending) {
                this.startupPromise = undefined;
            }
        });
        this.startupPromise = pending;
        return this.raceWithClose(pending);
    }
    async discardConnection(candidate) {
        if (this.active?.start === candidate.start) {
            this.active = undefined;
        }
        if (this.activeStart === candidate.start) {
            this.activeStart = undefined;
        }
        if (!this.closed) {
            this.connectionState = 'disconnected';
        }
        await this.trackTeardown(candidate.start);
    }
    closeStart(start) {
        const existing = this.startTeardowns.get(start);
        if (existing)
            return existing;
        const teardown = Promise.resolve().then(() => start.close());
        this.startTeardowns.set(start, teardown);
        return teardown;
    }
    async trackTeardown(start) {
        const teardown = this.closeStart(start);
        this.teardownPromise = teardown;
        try {
            await teardown;
        }
        finally {
            if (this.teardownPromise === teardown) {
                this.teardownPromise = undefined;
            }
        }
    }
    async raceWithClose(operation) {
        return Promise.race([
            operation,
            this.closeSignal.then(() => {
                throw new Error(CLOSED_MESSAGE);
            }),
        ]);
    }
    assertOpen() {
        if (this.closed) {
            throw new Error(CLOSED_MESSAGE);
        }
    }
}
function isTransportInterruption(error) {
    if (error instanceof McpError && error.code === ErrorCode.ConnectionClosed) {
        return true;
    }
    const code = error?.code;
    if (typeof code === 'string' &&
        ['EPIPE', 'ECONNRESET', 'ECONNREFUSED', 'ENOTCONN', 'ERR_STREAM_DESTROYED'].includes(code)) {
        return true;
    }
    const message = error instanceof Error ? error.message : String(error);
    return /^(Connection closed|Not connected)$/i.test(message.trim()) ||
        /transport (?:is )?closed/i.test(message) ||
        /Unity CLI process exited/i.test(message);
}
const INITIALIZE_TIMEOUT_MS = 10_000;
// StdioClientTransport returns immediately after its final owned-child signal.
// A close event should follow in the next event-loop turns; allow a bounded
// scheduling margin so shutdown cannot claim success before process closure.
const CHILD_CLOSE_OBSERVATION_TIMEOUT_MS = 500;
// StdioClientTransport uses two successive 2-second graceful/SIGTERM windows.
// Let both finish so its unref'ed timers expire before forced cleanup begins.
const SDK_CLOSE_GRACE_MS = 4_250;
// A direct StdioClientTransport.close() fallback needs the same complete
// graceful/SIGTERM sequence plus the wrapper's close-event observation margin.
// The transport retains its owned ChildProcess handle throughout that sequence.
const TRANSPORT_CLOSE_TIMEOUT_MS = 4_750;
export class OwnedStdioClientTransport {
    underlying;
    closeObservationTimeoutMs;
    onclose;
    onerror;
    onmessage;
    childClosed;
    resolveChildClosed;
    closePromise;
    startSucceeded = false;
    childCloseObserved = false;
    closeForwarded = false;
    constructor(underlying, closeObservationTimeoutMs = CHILD_CLOSE_OBSERVATION_TIMEOUT_MS) {
        this.underlying = underlying;
        this.closeObservationTimeoutMs = closeObservationTimeoutMs;
        this.childClosed = new Promise((resolve) => {
            this.resolveChildClosed = resolve;
        });
        this.underlying.onclose = () => {
            if (!this.childCloseObserved) {
                this.childCloseObserved = true;
                this.resolveChildClosed();
            }
            if (!this.closeForwarded) {
                this.closeForwarded = true;
                this.onclose?.();
            }
        };
        this.underlying.onerror = (error) => this.onerror?.(error);
        this.underlying.onmessage = (message, extra) => this.onmessage?.(message, extra);
    }
    get sessionId() {
        return this.underlying.sessionId;
    }
    set sessionId(value) {
        this.underlying.sessionId = value;
    }
    async start() {
        await this.underlying.start();
        this.startSucceeded = true;
    }
    send(message, options) {
        return this.underlying.send(message, options);
    }
    setProtocolVersion(version) {
        this.underlying.setProtocolVersion?.(version);
    }
    close() {
        if (this.closePromise)
            return this.closePromise;
        this.closePromise = this.closeOwnedChild();
        return this.closePromise;
    }
    async closeOwnedChild() {
        await this.underlying.close();
        if (!this.startSucceeded || this.childCloseObserved)
            return;
        const observation = await settleWithin(this.childClosed, this.closeObservationTimeoutMs);
        if (observation.status === 'fulfilled')
            return;
        throw new Error(`Unity CLI child did not report process closure within ${this.closeObservationTimeoutMs}ms after stdio transport teardown.`);
    }
}
const DEFAULT_SESSION_DEPENDENCIES = {
    createTransport: (options) => new OwnedStdioClientTransport(new StdioClientTransport({
        command: options.cliPath,
        args: ['mcp', '--project-path', options.projectPath],
        stderr: 'inherit',
    })),
    createClient: () => {
        const client = new Client({ name: 'mcp-unity-companion', version: '2.0.0' }, { capabilities: {} });
        return {
            connect: (transport, options) => client.connect(transport, options),
            callTool: (request, schema) => client.callTool(request, schema),
            close: () => client.close(),
        };
    },
};
export function createOfficialUnitySessionStart(options, dependencies = DEFAULT_SESSION_DEPENDENCIES) {
    const transport = dependencies.createTransport(options);
    const client = dependencies.createClient();
    const initializeAbort = new AbortController();
    let closeRequested = false;
    let teardownPromise;
    // Client.connect takes ownership of the transport synchronously before its first
    // await, so close() can terminate startup even while MCP initialization is pending.
    const ready = client
        .connect(transport, {
        signal: initializeAbort.signal,
        timeout: INITIALIZE_TIMEOUT_MS,
        maxTotalTimeout: INITIALIZE_TIMEOUT_MS,
    })
        .then(() => {
        if (closeRequested) {
            throw new Error(CLOSED_MESSAGE);
        }
        return {
            callTool: async (name, args) => {
                const result = await client.callTool({ name, arguments: args }, CallToolResultSchema);
                return CallToolResultSchema.parse(result);
            },
        };
    });
    return {
        ready,
        close() {
            if (teardownPromise)
                return teardownPromise;
            closeRequested = true;
            initializeAbort.abort();
            teardownPromise = teardownSdkSession(client, transport, {
                sdkCloseGraceMs: dependencies.sdkCloseGraceMs ?? SDK_CLOSE_GRACE_MS,
                transportCloseTimeoutMs: dependencies.transportCloseTimeoutMs ?? TRANSPORT_CLOSE_TIMEOUT_MS,
            });
            return teardownPromise;
        },
    };
}
async function teardownSdkSession(client, transport, deadlines) {
    const clientClose = invokeClose(() => client.close());
    const clientResult = await settleWithin(clientClose, deadlines.sdkCloseGraceMs);
    if (clientResult.status === 'fulfilled')
        return;
    const transportClose = invokeClose(() => transport.close());
    const transportResult = await settleWithin(transportClose, deadlines.transportCloseTimeoutMs);
    if (transportResult.status === 'fulfilled')
        return;
    if (transportResult.status === 'timed-out') {
        throw new Error(`Unity CLI transport teardown timed out after ${deadlines.transportCloseTimeoutMs}ms.`);
    }
    throw new Error(`Unity CLI transport teardown failed: ${errorMessage(transportResult.reason)}`);
}
function invokeClose(operation) {
    try {
        return Promise.resolve(operation());
    }
    catch (error) {
        return Promise.reject(error);
    }
}
function settleWithin(operation, timeoutMs) {
    return new Promise((resolve) => {
        let settled = false;
        const finish = (result) => {
            if (settled)
                return;
            settled = true;
            clearTimeout(timeout);
            resolve(result);
        };
        const timeout = setTimeout(() => finish({ status: 'timed-out' }), Math.max(0, timeoutMs));
        operation.then(() => finish({ status: 'fulfilled' }), (reason) => finish({ status: 'rejected', reason }));
    });
}
function errorMessage(error) {
    return error instanceof Error ? error.message : String(error);
}
