import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';
import { CallToolResultSchema, ErrorCode, McpError, } from '@modelcontextprotocol/sdk/types.js';
const CLOSED_MESSAGE = 'Official Unity MCP client is closed.';
export class OfficialUnityMcpClient {
    options;
    sessionFactory;
    closedStarts = new WeakSet();
    closeSignal;
    signalClose;
    active;
    activeStart;
    startupPromise;
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
            this.discardConnection(firstConnection);
            this.assertOpen();
            const retryConnection = await this.getConnection();
            try {
                return await this.raceWithClose(retryConnection.session.callTool(name, args));
            }
            catch (retryError) {
                if (isTransportInterruption(retryError)) {
                    this.discardConnection(retryConnection);
                }
                throw retryError;
            }
        }
    }
    async close() {
        if (this.closed)
            return;
        this.closed = true;
        this.connectionState = 'closed';
        this.signalClose();
        const start = this.activeStart;
        this.active = undefined;
        this.activeStart = undefined;
        this.startupPromise = undefined;
        if (start) {
            this.closeStart(start);
        }
    }
    async getConnection() {
        this.assertOpen();
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
                this.closeStart(start);
                throw new Error(CLOSED_MESSAGE);
            }
            const connection = { start, session };
            this.active = connection;
            this.connectionState = 'connected';
            return connection;
        })
            .catch((error) => {
            if (this.activeStart === start) {
                this.activeStart = undefined;
                if (!this.closed) {
                    this.connectionState = 'disconnected';
                }
            }
            this.closeStart(start);
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
    discardConnection(candidate) {
        if (this.active?.start === candidate.start) {
            this.active = undefined;
        }
        if (this.activeStart === candidate.start) {
            this.activeStart = undefined;
        }
        if (!this.closed) {
            this.connectionState = 'disconnected';
        }
        this.closeStart(candidate.start);
    }
    closeStart(start) {
        if (this.closedStarts.has(start))
            return;
        this.closedStarts.add(start);
        try {
            void start.close().catch(() => {
                // A broken or terminating child may reject close; ownership is still released.
            });
        }
        catch {
            // A synchronous close failure must not block shutdown or reconnection.
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
function createOfficialUnitySessionStart(options) {
    const transport = new StdioClientTransport({
        command: options.cliPath,
        args: ['mcp', '--project-path', options.projectPath],
        stderr: 'inherit',
    });
    const client = new Client({ name: 'mcp-unity-companion', version: '2.0.0' }, { capabilities: {} });
    let closeRequested = false;
    let closeStarted = false;
    // Client.connect takes ownership of the transport synchronously before its first
    // await, so close() can terminate startup even while MCP initialization is pending.
    const ready = client.connect(transport).then(() => {
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
        async close() {
            if (closeStarted)
                return;
            closeStarted = true;
            closeRequested = true;
            // Initiate teardown immediately but do not make companion shutdown wait for
            // the SDK's bounded graceful-process termination sequence.
            void client.close().catch(() => {
                // Startup or the child transport may already have failed.
            });
        },
    };
}
