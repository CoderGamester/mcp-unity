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
  private readonly closedStarts = new WeakSet<OfficialUnitySessionStart>();
  private readonly closeSignal: Promise<void>;
  private signalClose!: () => void;
  private active?: OwnedConnection;
  private activeStart?: OfficialUnitySessionStart;
  private startupPromise?: Promise<OwnedConnection>;
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

      this.discardConnection(firstConnection);
      this.assertOpen();
      const retryConnection = await this.getConnection();
      try {
        return await this.raceWithClose(retryConnection.session.callTool(name, args));
      } catch (retryError) {
        if (isTransportInterruption(retryError)) {
          this.discardConnection(retryConnection);
        }
        throw retryError;
      }
    }
  }

  async close(): Promise<void> {
    if (this.closed) return;
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

  private async getConnection(): Promise<OwnedConnection> {
    this.assertOpen();
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
          this.closeStart(start);
          throw new Error(CLOSED_MESSAGE);
        }
        const connection = { start, session };
        this.active = connection;
        this.connectionState = 'connected';
        return connection;
      })
      .catch((error: unknown) => {
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

  private discardConnection(candidate: OwnedConnection): void {
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

  private closeStart(start: OfficialUnitySessionStart): void {
    if (this.closedStarts.has(start)) return;
    this.closedStarts.add(start);
    try {
      void start.close().catch(() => {
        // A broken or terminating child may reject close; ownership is still released.
      });
    } catch {
      // A synchronous close failure must not block shutdown or reconnection.
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

function createOfficialUnitySessionStart(
  options: OfficialUnitySessionOptions,
): OfficialUnitySessionStart {
  const transport = new StdioClientTransport({
    command: options.cliPath,
    args: ['mcp', '--project-path', options.projectPath],
    stderr: 'inherit',
  });
  const client = new Client(
    { name: 'mcp-unity-companion', version: '2.0.0' },
    { capabilities: {} },
  );
  let closeRequested = false;
  let closeStarted = false;

  // Client.connect takes ownership of the transport synchronously before its first
  // await, so close() can terminate startup even while MCP initialization is pending.
  const ready = client.connect(transport).then((): OfficialUnitySession => {
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
    async close(): Promise<void> {
      if (closeStarted) return;
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
