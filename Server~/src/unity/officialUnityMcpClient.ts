import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';
import {
  CallToolResultSchema,
  ErrorCode,
  McpError,
  type CallToolResult,
} from '@modelcontextprotocol/sdk/types.js';

export interface OfficialUnitySession {
  callTool(name: string, args: Record<string, unknown>): Promise<CallToolResult>;
}

export interface OfficialUnitySessionStart {
  readonly ready: Promise<OfficialUnitySession>;
  close(): Promise<void>;
}

export interface OfficialUnitySessionOptions {
  cliPath: string;
  projectPath: string;
}

export type OfficialUnitySessionFactory = (
  options: OfficialUnitySessionOptions,
) => OfficialUnitySessionStart;

export type UnityConnectionState =
  | 'disconnected'
  | 'connecting'
  | 'connected'
  | 'closed';

export interface OfficialUnityMcpClientOptions extends OfficialUnitySessionOptions {
  sessionFactory?: OfficialUnitySessionFactory;
}

interface OwnedConnection {
  start: OfficialUnitySessionStart;
  session: OfficialUnitySession;
}

const CLOSED_MESSAGE = 'Official Unity MCP client is closed.';

export class OfficialUnityMcpClient {
  private readonly options: OfficialUnitySessionOptions;
  private readonly sessionFactory: OfficialUnitySessionFactory;
  private readonly startTeardowns =
    new WeakMap<OfficialUnitySessionStart, Promise<void>>();
  private readonly closeSignal: Promise<void>;
  private signalClose!: () => void;
  private active?: OwnedConnection;
  private activeStart?: OfficialUnitySessionStart;
  private startupPromise?: Promise<OwnedConnection>;
  private teardownPromise?: Promise<void>;
  private closePromise?: Promise<void>;
  private connectionState: UnityConnectionState = 'disconnected';
  private closed = false;

  constructor(options: OfficialUnityMcpClientOptions) {
    this.options = {
      cliPath: options.cliPath,
      projectPath: options.projectPath,
    };
    this.sessionFactory = options.sessionFactory ?? createOfficialUnitySessionStart;
    this.closeSignal = new Promise<void>((resolve) => {
      this.signalClose = resolve;
    });
  }

  get state(): UnityConnectionState {
    return this.connectionState;
  }

  async readTool(
    name: string,
    args: Record<string, unknown>,
  ): Promise<CallToolResult> {
    this.assertOpen();
    const firstConnection = await this.getConnection();
    try {
      return await this.raceWithClose(firstConnection.session.callTool(name, args));
    } catch (firstError) {
      if (!isTransportInterruption(firstError)) {
        throw firstError;
      }

      await this.discardConnection(firstConnection);
      this.assertOpen();
      const retryConnection = await this.getConnection();
      try {
        return await this.raceWithClose(retryConnection.session.callTool(name, args));
      } catch (retryError) {
        if (isTransportInterruption(retryError)) {
          await this.discardConnection(retryConnection);
        }
        throw retryError;
      }
    }
  }

  close(): Promise<void> {
    if (this.closePromise) return this.closePromise;
    this.closed = true;
    this.connectionState = 'closed';
    this.signalClose();

    const start = this.activeStart;
    const teardown = this.teardownPromise;
    this.active = undefined;
    this.activeStart = undefined;
    const pending = new Set<Promise<void>>();
    if (teardown) pending.add(teardown);
    if (start) pending.add(this.closeStart(start));
    this.closePromise = Promise.all([...pending]).then(() => undefined);
    return this.closePromise;
  }

  private async getConnection(): Promise<OwnedConnection> {
    this.assertOpen();
    if (this.teardownPromise) {
      await this.raceWithClose(this.teardownPromise);
      this.assertOpen();
    }
    if (this.active) return this.active;
    if (this.startupPromise) {
      return this.raceWithClose(this.startupPromise);
    }

    this.connectionState = 'connecting';
    let start: OfficialUnitySessionStart;
    try {
      start = this.sessionFactory(this.options);
    } catch (error) {
      this.connectionState = 'disconnected';
      throw error;
    }

    this.activeStart = start;
    const pending = start.ready
      .then((session): OwnedConnection => {
        if (this.closed || this.activeStart !== start) {
          throw new Error(CLOSED_MESSAGE);
        }
        const connection = { start, session };
        this.active = connection;
        this.connectionState = 'connected';
        return connection;
      })
      .catch(async (error: unknown) => {
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

  private async discardConnection(candidate: OwnedConnection): Promise<void> {
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

  private closeStart(start: OfficialUnitySessionStart): Promise<void> {
    const existing = this.startTeardowns.get(start);
    if (existing) return existing;
    const teardown = Promise.resolve().then(() => start.close());
    this.startTeardowns.set(start, teardown);
    return teardown;
  }

  private async trackTeardown(start: OfficialUnitySessionStart): Promise<void> {
    const teardown = this.closeStart(start);
    this.teardownPromise = teardown;
    try {
      await teardown;
    } finally {
      if (this.teardownPromise === teardown) {
        this.teardownPromise = undefined;
      }
    }
  }

  private async raceWithClose<T>(operation: Promise<T>): Promise<T> {
    return Promise.race([
      operation,
      this.closeSignal.then(() => {
        throw new Error(CLOSED_MESSAGE);
      }),
    ]);
  }

  private assertOpen(): void {
    if (this.closed) {
      throw new Error(CLOSED_MESSAGE);
    }
  }
}

function isTransportInterruption(error: unknown): boolean {
  if (error instanceof McpError && error.code === ErrorCode.ConnectionClosed) {
    return true;
  }
  const code = (error as { code?: unknown } | null)?.code;
  if (
    typeof code === 'string' &&
    ['EPIPE', 'ECONNRESET', 'ECONNREFUSED', 'ENOTCONN', 'ERR_STREAM_DESTROYED'].includes(
      code,
    )
  ) {
    return true;
  }
  const message = error instanceof Error ? error.message : String(error);
  return /^(Connection closed|Not connected)$/i.test(message.trim()) ||
    /transport (?:is )?closed/i.test(message) ||
    /Unity CLI process exited/i.test(message);
}

interface UnitySdkTransport {
  close(): Promise<void>;
}

interface UnitySdkClient {
  connect(
    transport: UnitySdkTransport,
    options: {
      signal: AbortSignal;
      timeout: number;
      maxTotalTimeout: number;
    },
  ): Promise<void>;
  callTool(
    request: { name: string; arguments: Record<string, unknown> },
    schema: typeof CallToolResultSchema,
  ): Promise<unknown>;
  close(): Promise<void>;
}

export interface OfficialUnitySessionDependencies {
  createTransport(options: OfficialUnitySessionOptions): UnitySdkTransport;
  createClient(): UnitySdkClient;
  sdkCloseGraceMs?: number;
  transportCloseTimeoutMs?: number;
}

const INITIALIZE_TIMEOUT_MS = 10_000;
// StdioClientTransport uses two successive 2-second graceful/SIGTERM windows.
// Let both finish so its unref'ed timers expire before forced cleanup begins.
const SDK_CLOSE_GRACE_MS = 4_250;
// A direct StdioClientTransport.close() fallback needs the same complete
// graceful/SIGTERM sequence; the transport retains its owned ChildProcess handle.
const TRANSPORT_CLOSE_TIMEOUT_MS = 4_250;

const DEFAULT_SESSION_DEPENDENCIES: OfficialUnitySessionDependencies = {
  createTransport: (options) =>
    new StdioClientTransport({
      command: options.cliPath,
      args: ['mcp', '--project-path', options.projectPath],
      stderr: 'inherit',
    }),
  createClient: () => {
    const client = new Client(
      { name: 'mcp-unity-companion', version: '2.0.0' },
      { capabilities: {} },
    );
    return {
      connect: (transport, options) =>
        client.connect(transport as StdioClientTransport, options),
      callTool: (request, schema) => client.callTool(request, schema),
      close: () => client.close(),
    };
  },
};

export function createOfficialUnitySessionStart(
  options: OfficialUnitySessionOptions,
  dependencies: OfficialUnitySessionDependencies = DEFAULT_SESSION_DEPENDENCIES,
): OfficialUnitySessionStart {
  const transport = dependencies.createTransport(options);
  const client = dependencies.createClient();
  const initializeAbort = new AbortController();
  let closeRequested = false;
  let teardownPromise: Promise<void> | undefined;

  // Client.connect takes ownership of the transport synchronously before its first
  // await, so close() can terminate startup even while MCP initialization is pending.
  const ready = client
    .connect(transport, {
      signal: initializeAbort.signal,
      timeout: INITIALIZE_TIMEOUT_MS,
      maxTotalTimeout: INITIALIZE_TIMEOUT_MS,
    })
    .then((): OfficialUnitySession => {
      if (closeRequested) {
        throw new Error(CLOSED_MESSAGE);
      }
      return {
        callTool: async (name, args) => {
          const result = await client.callTool(
            { name, arguments: args },
            CallToolResultSchema,
          );
          return CallToolResultSchema.parse(result);
        },
      };
    });

  return {
    ready,
    close(): Promise<void> {
      if (teardownPromise) return teardownPromise;
      closeRequested = true;
      initializeAbort.abort();
      teardownPromise = teardownSdkSession(client, transport, {
        sdkCloseGraceMs:
          dependencies.sdkCloseGraceMs ?? SDK_CLOSE_GRACE_MS,
        transportCloseTimeoutMs:
          dependencies.transportCloseTimeoutMs ?? TRANSPORT_CLOSE_TIMEOUT_MS,
      });
      return teardownPromise;
    },
  };
}

interface TeardownDeadlines {
  sdkCloseGraceMs: number;
  transportCloseTimeoutMs: number;
}

async function teardownSdkSession(
  client: UnitySdkClient,
  transport: UnitySdkTransport,
  deadlines: TeardownDeadlines,
): Promise<void> {
  const clientClose = invokeClose(() => client.close());
  const clientResult = await settleWithin(clientClose, deadlines.sdkCloseGraceMs);

  if (clientResult.status === 'fulfilled') return;

  const transportClose = invokeClose(() => transport.close());
  const transportResult = await settleWithin(
    transportClose,
    deadlines.transportCloseTimeoutMs,
  );
  if (transportResult.status === 'fulfilled') return;
  if (transportResult.status === 'timed-out') {
    throw new Error(
      `Unity CLI transport teardown timed out after ${deadlines.transportCloseTimeoutMs}ms.`,
    );
  }
  throw new Error(
    `Unity CLI transport teardown failed: ${errorMessage(transportResult.reason)}`,
  );
}

type Settlement =
  | { status: 'fulfilled' }
  | { status: 'rejected'; reason: unknown }
  | { status: 'timed-out' };

function invokeClose(operation: () => Promise<void>): Promise<void> {
  try {
    return Promise.resolve(operation());
  } catch (error) {
    return Promise.reject(error);
  }
}

function settleWithin(
  operation: Promise<void>,
  timeoutMs: number,
): Promise<Settlement> {
  return new Promise((resolve) => {
    let settled = false;
    const finish = (result: Settlement): void => {
      if (settled) return;
      settled = true;
      clearTimeout(timeout);
      resolve(result);
    };
    const timeout = setTimeout(
      () => finish({ status: 'timed-out' }),
      Math.max(0, timeoutMs),
    );
    operation.then(
      () => finish({ status: 'fulfilled' }),
      (reason: unknown) => finish({ status: 'rejected', reason }),
    );
  });
}

function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}
