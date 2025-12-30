import { v4 as uuidv4 } from 'uuid';
import { Logger } from '../utils/logger.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { promises as fs } from 'fs';
import path from 'path';
import { UnityConnection, ConnectionState, ConnectionStateChange, UnityConnectionConfig } from './unityConnection.js';

// Top-level constant for the Unity settings JSON path
const MCP_UNITY_SETTINGS_PATH = path.resolve(process.cwd(), '../ProjectSettings/McpUnitySettings.json');

interface PendingRequest {
  resolve: (value: any) => void;
  reject: (reason: any) => void;
  timeout: NodeJS.Timeout;
}

interface UnityRequest {
  id?: string;
  method: string;
  params: any;
}

interface UnityResponse {
  jsonrpc: string;
  id: string;
  result?: any;
  error?: {
    message: string;
    type: string;
    details?: any;
  };
}

/**
 * Connection state change callback type
 */
export type ConnectionStateCallback = (change: ConnectionStateChange) => void;

// Re-export connection types for consumers
export { ConnectionState, ConnectionStateChange } from './unityConnection.js';

export class McpUnity {
  private logger: Logger;
  private port: number = 8090;
  private host: string = 'localhost';
  private requestTimeout = 10000;

  private connection: UnityConnection | null = null;
  private pendingRequests: Map<string, PendingRequest> = new Map<string, PendingRequest>();
  private clientName: string = '';

  // Connection state listeners
  private stateListeners: Set<ConnectionStateCallback> = new Set();

  constructor(logger: Logger) {
    this.logger = logger;
  }

  /**
   * Start the Unity connection
   * @param clientName Optional name of the MCP client connecting to Unity
   */
  public async start(clientName?: string): Promise<void> {
    try {
      this.logger.info('Attempting to read startup parameters...');
      await this.parseAndSetConfig();

      this.clientName = clientName || '';

      // Create connection with configuration
      const config: UnityConnectionConfig = {
        host: this.host,
        port: this.port,
        requestTimeout: this.requestTimeout,
        clientName: this.clientName,
        // Use defaults for reconnection and heartbeat from UnityConnection
      };

      this.connection = new UnityConnection(this.logger, config);

      // Set up event handlers
      this.connection.on('stateChange', (change: ConnectionStateChange) => {
        this.handleStateChange(change);
      });

      this.connection.on('message', (data: string) => {
        this.handleMessage(data);
      });

      this.connection.on('error', (error: McpUnityError) => {
        this.logger.error(`Connection error: ${error.message}`);
        // Reject pending requests on connection error
        this.rejectAllPendingRequests(error);
      });

      this.logger.info('Attempting to connect to Unity WebSocket...');
      await this.connection.connect();
      this.logger.info('Successfully connected to Unity WebSocket');

      if (clientName) {
        this.logger.info(`Client identified to Unity as: ${clientName}`);
      }
    } catch (error) {
      this.logger.warn(`Could not connect to Unity WebSocket: ${error instanceof Error ? error.message : String(error)}`);
      this.logger.warn('Will retry connection on next request (with automatic reconnection)');
    }

    return Promise.resolve();
  }

  /**
   * Reads our configuration file and sets parameters of the server based on them.
   */
  private async parseAndSetConfig() {
    const config = await this.readConfigFileAsJson();

    const configPort = config.Port;
    this.port = configPort ? parseInt(configPort, 10) : 8090;
    this.logger.info(`Using port: ${this.port} for Unity WebSocket connection`);

    // Check environment variable first, then config file, then default to localhost
    const configHost = process.env.UNITY_HOST || config.Host;
    this.host = configHost || 'localhost';

    // Initialize timeout from environment variable (in seconds; it is the same as Cline) or use default (10 seconds)
    const configTimeout = config.RequestTimeoutSeconds;
    this.requestTimeout = configTimeout ? parseInt(configTimeout, 10) * 1000 : 10000;
    this.logger.info(`Using request timeout: ${this.requestTimeout / 1000} seconds`);
  }

  /**
   * Handle connection state changes
   */
  private handleStateChange(change: ConnectionStateChange): void {
    this.logger.debug(`Connection state changed: ${change.previousState} -> ${change.currentState}`);

    // Notify all listeners
    for (const listener of this.stateListeners) {
      try {
        listener(change);
      } catch (err) {
        this.logger.error(`Error in state listener: ${err instanceof Error ? err.message : String(err)}`);
      }
    }

    // Handle specific state transitions
    if (change.currentState === ConnectionState.Disconnected) {
      // Reject all pending requests when disconnected
      this.rejectAllPendingRequests(
        new McpUnityError(ErrorType.CONNECTION, change.reason || 'Connection lost')
      );
    }
  }

  /**
   * Handle messages received from Unity
   */
  private handleMessage(data: string): void {
    try {
      const response = JSON.parse(data) as UnityResponse;

      if (response.id && this.pendingRequests.has(response.id)) {
        const request = this.pendingRequests.get(response.id)!;
        clearTimeout(request.timeout);
        this.pendingRequests.delete(response.id);

        if (response.error) {
          request.reject(new McpUnityError(
            ErrorType.TOOL_EXECUTION,
            response.error.message || 'Unknown error',
            response.error.details
          ));
        } else {
          request.resolve(response.result);
        }
      }
    } catch (e) {
      this.logger.error(`Error parsing WebSocket message: ${e instanceof Error ? e.message : String(e)}`);
    }
  }

  /**
   * Reject all pending requests with an error
   */
  private rejectAllPendingRequests(error: McpUnityError): void {
    for (const [id, request] of this.pendingRequests.entries()) {
      clearTimeout(request.timeout);
      request.reject(error);
      this.pendingRequests.delete(id);
    }
  }

  /**
   * Stop the Unity connection
   */
  public async stop(): Promise<void> {
    if (this.connection) {
      this.connection.disconnect('Server stopping');
      this.connection.removeAllListeners();
      this.connection = null;
    }
    this.rejectAllPendingRequests(new McpUnityError(ErrorType.CONNECTION, 'Server stopped'));
    this.logger.info('Unity WebSocket client stopped');
    return Promise.resolve();
  }

  /**
   * Send a request to the Unity server
   */
  public async sendRequest(request: UnityRequest): Promise<any> {
    // Ensure we're connected first
    if (!this.isConnected) {
      if (!this.connection) {
        throw new McpUnityError(ErrorType.CONNECTION, 'Not started - call start() first');
      }

      this.logger.info('Not connected to Unity, connecting first...');
      await this.connection.connect();
    }

    // Use given id or generate a new one
    const requestId = request.id as string || uuidv4();
    const message: UnityRequest = {
      ...request,
      id: requestId
    };

    return new Promise((resolve, reject) => {
      // Double check isConnected again after await
      if (!this.connection || !this.isConnected) {
        reject(new McpUnityError(ErrorType.CONNECTION, 'Not connected to Unity'));
        return;
      }

      // Create timeout for the request
      const timeout = setTimeout(() => {
        if (this.pendingRequests.has(requestId)) {
          this.logger.error(`Request ${requestId} timed out after ${this.requestTimeout}ms`);
          this.pendingRequests.delete(requestId);
          reject(new McpUnityError(ErrorType.TIMEOUT, 'Request timed out'));

          // Force reconnection on timeout (connection may be stale)
          if (this.connection) {
            this.connection.forceReconnect();
          }
        }
      }, this.requestTimeout);

      // Store pending request
      this.pendingRequests.set(requestId, {
        resolve,
        reject,
        timeout
      });

      try {
        this.connection.send(JSON.stringify(message));
        this.logger.debug(`Request sent: ${requestId}`);
      } catch (err) {
        clearTimeout(timeout);
        this.pendingRequests.delete(requestId);
        reject(new McpUnityError(ErrorType.CONNECTION, `Send failed: ${err instanceof Error ? err.message : String(err)}`));
      }
    });
  }

  /**
   * Check if connected to Unity
   * Only returns true if the connection is guaranteed to be active
   */
  public get isConnected(): boolean {
    return this.connection !== null && this.connection.isConnected;
  }

  /**
   * Get current connection state
   */
  public get connectionState(): ConnectionState {
    return this.connection?.connectionState ?? ConnectionState.Disconnected;
  }

  /**
   * Check if currently connecting or reconnecting
   */
  public get isConnecting(): boolean {
    return this.connection?.isConnecting ?? false;
  }

  /**
   * Add a listener for connection state changes
   * @param callback Function to call when connection state changes
   * @returns Function to remove the listener
   */
  public onConnectionStateChange(callback: ConnectionStateCallback): () => void {
    this.stateListeners.add(callback);
    return () => {
      this.stateListeners.delete(callback);
    };
  }

  /**
   * Force a reconnection to Unity
   * Useful when Unity has reloaded and the connection may be stale
   */
  public forceReconnect(): void {
    if (this.connection) {
      this.connection.forceReconnect();
    } else {
      this.logger.warn('Cannot force reconnect - not started');
    }
  }

  /**
   * Get connection statistics
   */
  public getConnectionStats(): {
    state: ConnectionState;
    pendingRequests: number;
    reconnectAttempt?: number;
    timeSinceLastPong?: number;
  } {
    const stats = this.connection?.getStats();
    return {
      state: stats?.state ?? ConnectionState.Disconnected,
      pendingRequests: this.pendingRequests.size,
      reconnectAttempt: stats?.reconnectAttempt,
      timeSinceLastPong: stats?.timeSinceLastPong
    };
  }

  /**
   * Read the McpUnitySettings.json file and return its contents as a JSON object.
   * @returns a JSON object with the contents of the McpUnitySettings.json file.
   */
  private async readConfigFileAsJson(): Promise<any> {
    const configPath = MCP_UNITY_SETTINGS_PATH;
    try {
      const content = await fs.readFile(configPath, 'utf-8');
      const json = JSON.parse(content);
      return json;
    } catch (err) {
      this.logger.debug(`McpUnitySettings.json not found or unreadable: ${err instanceof Error ? err.message : String(err)}`);
      return {};
    }
  }
}
