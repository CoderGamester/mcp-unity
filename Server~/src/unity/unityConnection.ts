import WebSocket from 'ws';
import { EventEmitter } from 'events';
import { Logger } from '../utils/logger.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';

/**
 * Connection states for the Unity WebSocket connection
 */
export enum ConnectionState {
  Disconnected = 'disconnected',
  Connecting = 'connecting',
  Connected = 'connected',
  Reconnecting = 'reconnecting'
}

/**
 * Custom WebSocket close codes for Unity-specific events
 * Range 4000-4999 is reserved for application use
 */
export const UnityCloseCode = {
  /** Unity is entering Play mode - use fast polling instead of backoff */
  PLAY_MODE: 4001
} as const;

/**
 * Connection state change event data
 */
export interface ConnectionStateChange {
  previousState: ConnectionState;
  currentState: ConnectionState;
  reason?: string;
  attemptNumber?: number;
}

/**
 * Configuration for the Unity connection
 */
export interface UnityConnectionConfig {
  host: string;
  port: number;
  requestTimeout: number;
  clientName?: string;

  // Reconnection settings
  minReconnectDelay?: number;      // Default: 1000ms
  maxReconnectDelay?: number;      // Default: 30000ms
  reconnectBackoffMultiplier?: number;  // Default: 2
  maxReconnectAttempts?: number;   // Default: unlimited (-1)

  // Heartbeat settings
  heartbeatInterval?: number;      // Default: 30000ms (30 seconds)
  heartbeatTimeout?: number;       // Default: 5000ms (5 seconds)

  // Play mode settings
  playModePollingInterval?: number; // Default: 3000ms (3 seconds) - used instead of backoff during Play mode
}

/**
 * Default configuration values
 */
const DEFAULT_CONFIG = {
  minReconnectDelay: 1000,
  maxReconnectDelay: 30000,
  reconnectBackoffMultiplier: 2,
  maxReconnectAttempts: -1,  // Unlimited
  heartbeatInterval: 30000,
  heartbeatTimeout: 5000,
  playModePollingInterval: 3000  // Fixed 3 second polling during Play mode
};

/**
 * UnityConnection manages the WebSocket connection to Unity Editor
 * with automatic reconnection, exponential backoff, and heartbeat monitoring.
 *
 * Events:
 * - 'stateChange': Emitted when connection state changes
 * - 'message': Emitted when a message is received from Unity
 * - 'error': Emitted when an error occurs
 */
export class UnityConnection extends EventEmitter {
  private logger: Logger;
  private config: Required<UnityConnectionConfig>;
  private ws: WebSocket | null = null;
  private state: ConnectionState = ConnectionState.Disconnected;

  // Reconnection state
  private reconnectAttempt: number = 0;
  private reconnectTimer: NodeJS.Timeout | null = null;
  private isManualDisconnect: boolean = false;
  private isPlayModeReconnect: boolean = false;  // True when reconnecting due to Unity Play mode

  // Heartbeat state
  private heartbeatTimer: NodeJS.Timeout | null = null;
  private heartbeatTimeoutTimer: NodeJS.Timeout | null = null;
  private lastPongTime: number = 0;
  private awaitingPong: boolean = false;

  constructor(logger: Logger, config: UnityConnectionConfig) {
    super();
    this.logger = logger;
    this.config = {
      ...DEFAULT_CONFIG,
      ...config
    } as Required<UnityConnectionConfig>;
  }

  /**
   * Get the current connection state
   */
  public get connectionState(): ConnectionState {
    return this.state;
  }

  /**
   * Check if currently connected
   */
  public get isConnected(): boolean {
    return this.state === ConnectionState.Connected &&
           this.ws !== null &&
           this.ws.readyState === WebSocket.OPEN;
  }

  /**
   * Check if currently connecting or reconnecting
   */
  public get isConnecting(): boolean {
    return this.state === ConnectionState.Connecting ||
           this.state === ConnectionState.Reconnecting;
  }

  /**
   * Get time since last successful heartbeat response (pong)
   */
  public get timeSinceLastPong(): number {
    if (this.lastPongTime === 0) return -1;
    return Date.now() - this.lastPongTime;
  }

  /**
   * Update configuration dynamically
   */
  public updateConfig(config: Partial<UnityConnectionConfig>): void {
    this.config = { ...this.config, ...config };
  }

  /**
   * Connect to Unity WebSocket server
   */
  public async connect(): Promise<void> {
    if (this.isConnected) {
      this.logger.debug('Already connected to Unity');
      return;
    }

    if (this.isConnecting) {
      this.logger.debug('Connection already in progress');
      return;
    }

    this.isManualDisconnect = false;
    return this.doConnect();
  }

  /**
   * Disconnect from Unity WebSocket server
   */
  public disconnect(reason?: string): void {
    this.isManualDisconnect = true;
    this.stopReconnectTimer();
    this.stopHeartbeat();
    this.closeWebSocket(reason || 'Manual disconnect');
    this.setState(ConnectionState.Disconnected, reason || 'Manual disconnect');
  }

  /**
   * Send a message to Unity
   */
  public send(message: string): void {
    if (!this.isConnected || !this.ws) {
      throw new McpUnityError(ErrorType.CONNECTION, 'Not connected to Unity');
    }

    try {
      this.ws.send(message);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : String(err);
      throw new McpUnityError(ErrorType.CONNECTION, `Send failed: ${errorMessage}`);
    }
  }

  /**
   * Get WebSocket instance (for advanced use)
   */
  public get webSocket(): WebSocket | null {
    return this.ws;
  }

  /**
   * Internal: Perform the actual connection
   */
  private async doConnect(): Promise<void> {
    const isReconnecting = this.reconnectAttempt > 0;
    this.setState(
      isReconnecting ? ConnectionState.Reconnecting : ConnectionState.Connecting,
      isReconnecting ? `Reconnection attempt ${this.reconnectAttempt}` : 'Connecting'
    );

    return new Promise<void>((resolve, reject) => {
      const wsUrl = `ws://${this.config.host}:${this.config.port}/McpUnity`;
      this.logger.debug(`Connecting to ${wsUrl}...`);

      // Create connection options with headers for client identification
      const options: WebSocket.ClientOptions = {
        headers: {
          'X-Client-Name': this.config.clientName || ''
        },
        origin: this.config.clientName || ''
      };

      // Clean up existing socket first
      this.closeWebSocket('Preparing new connection');

      // Create new WebSocket
      this.ws = new WebSocket(wsUrl, options);

      // Connection timeout
      const connectionTimeout = setTimeout(() => {
        if (this.ws && this.ws.readyState === WebSocket.CONNECTING) {
          this.logger.warn('Connection timeout');
          this.closeWebSocket('Connection timeout');

          const error = new McpUnityError(ErrorType.CONNECTION, 'Connection timeout');
          this.handleConnectionFailure(error);
          reject(error);
        }
      }, this.config.requestTimeout);

      this.ws.onopen = () => {
        clearTimeout(connectionTimeout);
        this.logger.info('WebSocket connected to Unity');

        // Reset reconnection state on successful connection
        this.reconnectAttempt = 0;
        this.isPlayModeReconnect = false;  // Clear Play mode flag
        this.lastPongTime = Date.now();

        this.setState(ConnectionState.Connected, 'Connection established');
        this.startHeartbeat();
        resolve();
      };

      this.ws.onerror = (err) => {
        clearTimeout(connectionTimeout);
        const errorMessage = err.message || 'Unknown error';
        this.logger.error(`WebSocket error: ${errorMessage}`);

        const error = new McpUnityError(ErrorType.CONNECTION, `Connection failed: ${errorMessage}`);
        this.emit('error', error);

        // Don't reject here - let onclose handle cleanup and reconnection
      };

      this.ws.onmessage = (event) => {
        this.emit('message', event.data.toString());
      };

      this.ws.onclose = (event) => {
        clearTimeout(connectionTimeout);
        this.stopHeartbeat();

        const reason = event.reason || `Code: ${event.code}`;
        this.logger.debug(`WebSocket closed: ${reason}`);

        // Check if Unity is entering Play mode (custom close code 4001)
        if (event.code === UnityCloseCode.PLAY_MODE) {
          this.logger.info('Unity entering Play mode - using fast polling for reconnection');
          this.isPlayModeReconnect = true;
        }

        // Clear WebSocket reference
        this.ws = null;

        // Handle reconnection if not manual disconnect
        if (!this.isManualDisconnect) {
          this.handleConnectionFailure(new McpUnityError(ErrorType.CONNECTION, reason));
        } else {
          this.setState(ConnectionState.Disconnected, reason);
        }

        // Reject if we were in initial connection
        if (this.state === ConnectionState.Connecting) {
          reject(new McpUnityError(ErrorType.CONNECTION, reason));
        }
      };

      // Handle WebSocket ping/pong for heartbeat
      this.ws.on('pong', () => {
        this.handlePong();
      });
    });
  }

  /**
   * Handle connection failure and schedule reconnection
   */
  private handleConnectionFailure(error: McpUnityError): void {
    if (this.isManualDisconnect) {
      this.setState(ConnectionState.Disconnected, 'Manual disconnect');
      return;
    }

    // Check max reconnect attempts (skip for Play mode - unlimited retries)
    if (!this.isPlayModeReconnect &&
        this.config.maxReconnectAttempts !== -1 &&
        this.reconnectAttempt >= this.config.maxReconnectAttempts) {
      this.logger.error(`Max reconnection attempts (${this.config.maxReconnectAttempts}) reached`);
      this.setState(ConnectionState.Disconnected, 'Max reconnection attempts reached');
      this.emit('error', new McpUnityError(ErrorType.CONNECTION, 'Max reconnection attempts reached'));
      return;
    }

    // Use fixed polling interval for Play mode, exponential backoff otherwise
    const delay = this.isPlayModeReconnect
      ? this.config.playModePollingInterval
      : this.calculateBackoffDelay();

    this.reconnectAttempt++;

    const modeInfo = this.isPlayModeReconnect ? ' (Play mode polling)' : '';
    this.logger.info(`Scheduling reconnection attempt ${this.reconnectAttempt} in ${delay}ms${modeInfo}`);
    this.setState(ConnectionState.Reconnecting, `Waiting ${delay}ms before attempt ${this.reconnectAttempt}${modeInfo}`);

    this.reconnectTimer = setTimeout(() => {
      this.reconnectTimer = null;
      this.doConnect().catch((err) => {
        this.logger.warn(`Reconnection attempt ${this.reconnectAttempt} failed: ${err.message}`);
      });
    }, delay);
  }

  /**
   * Calculate exponential backoff delay
   */
  private calculateBackoffDelay(): number {
    const baseDelay = this.config.minReconnectDelay;
    const multiplier = this.config.reconnectBackoffMultiplier;
    const maxDelay = this.config.maxReconnectDelay;

    // Exponential backoff: base * multiplier^attempt
    const delay = baseDelay * Math.pow(multiplier, this.reconnectAttempt);

    // Add jitter (0-20% random variation) to prevent thundering herd
    const jitter = delay * 0.2 * Math.random();

    return Math.min(delay + jitter, maxDelay);
  }

  /**
   * Stop reconnection timer
   */
  private stopReconnectTimer(): void {
    if (this.reconnectTimer) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }
    this.reconnectAttempt = 0;
  }

  /**
   * Start heartbeat monitoring
   */
  private startHeartbeat(): void {
    this.stopHeartbeat();

    if (this.config.heartbeatInterval <= 0) {
      this.logger.debug('Heartbeat disabled');
      return;
    }

    this.logger.debug(`Starting heartbeat with ${this.config.heartbeatInterval}ms interval`);

    this.heartbeatTimer = setInterval(() => {
      this.sendHeartbeat();
    }, this.config.heartbeatInterval);
  }

  /**
   * Stop heartbeat monitoring
   */
  private stopHeartbeat(): void {
    if (this.heartbeatTimer) {
      clearInterval(this.heartbeatTimer);
      this.heartbeatTimer = null;
    }
    if (this.heartbeatTimeoutTimer) {
      clearTimeout(this.heartbeatTimeoutTimer);
      this.heartbeatTimeoutTimer = null;
    }
    this.awaitingPong = false;
  }

  /**
   * Send heartbeat ping
   */
  private sendHeartbeat(): void {
    if (!this.isConnected || !this.ws) {
      return;
    }

    // If we're still waiting for a pong from the last ping, connection may be stale
    if (this.awaitingPong) {
      this.logger.warn('No pong received for previous ping, connection may be stale');
      this.handleStaleConnection();
      return;
    }

    try {
      this.awaitingPong = true;
      this.ws.ping();
      this.logger.debug('Heartbeat ping sent');

      // Set timeout for pong response
      this.heartbeatTimeoutTimer = setTimeout(() => {
        if (this.awaitingPong) {
          this.logger.warn('Heartbeat timeout - no pong received');
          this.handleStaleConnection();
        }
      }, this.config.heartbeatTimeout);
    } catch (err) {
      this.logger.error(`Failed to send heartbeat: ${err instanceof Error ? err.message : String(err)}`);
      this.awaitingPong = false;
    }
  }

  /**
   * Handle pong response
   */
  private handlePong(): void {
    this.awaitingPong = false;
    this.lastPongTime = Date.now();

    if (this.heartbeatTimeoutTimer) {
      clearTimeout(this.heartbeatTimeoutTimer);
      this.heartbeatTimeoutTimer = null;
    }

    this.logger.debug('Heartbeat pong received');
  }

  /**
   * Handle stale connection detected by heartbeat
   */
  private handleStaleConnection(): void {
    this.logger.warn('Stale connection detected, forcing reconnection');
    this.awaitingPong = false;

    // Force close and trigger reconnection
    this.closeWebSocket('Stale connection detected');
    this.handleConnectionFailure(new McpUnityError(ErrorType.CONNECTION, 'Stale connection detected'));
  }

  /**
   * Close WebSocket cleanly
   */
  private closeWebSocket(reason?: string): void {
    if (!this.ws) return;

    this.logger.debug(`Closing WebSocket: ${reason || 'No reason'}`);

    // Remove all event handlers first
    this.ws.onopen = null;
    this.ws.onmessage = null;
    this.ws.onerror = null;
    this.ws.onclose = null;
    this.ws.removeAllListeners('pong');

    try {
      if (this.ws.readyState === WebSocket.CONNECTING) {
        this.ws.terminate();
      } else if (this.ws.readyState === WebSocket.OPEN) {
        this.ws.close(1000, reason);
      }
    } catch (err) {
      this.logger.error(`Error closing WebSocket: ${err instanceof Error ? err.message : String(err)}`);
    }

    this.ws = null;
  }

  /**
   * Set connection state and emit event
   */
  private setState(newState: ConnectionState, reason?: string): void {
    if (this.state === newState) return;

    const previousState = this.state;
    this.state = newState;

    const change: ConnectionStateChange = {
      previousState,
      currentState: newState,
      reason,
      attemptNumber: this.reconnectAttempt > 0 ? this.reconnectAttempt : undefined
    };

    this.logger.debug(`Connection state: ${previousState} -> ${newState} (${reason || 'no reason'})`);
    this.emit('stateChange', change);
  }

  /**
   * Force a reconnection (useful after Unity domain reload)
   */
  public forceReconnect(): void {
    this.logger.info('Forcing reconnection...');
    this.isManualDisconnect = false;
    this.stopReconnectTimer();
    this.closeWebSocket('Force reconnect');
    this.reconnectAttempt = 0;  // Reset attempts for fresh reconnect

    this.doConnect().catch((err) => {
      this.logger.warn(`Force reconnect failed: ${err.message}`);
    });
  }

  /**
   * Get connection statistics
   */
  public getStats(): {
    state: ConnectionState;
    reconnectAttempt: number;
    timeSinceLastPong: number;
    isAwaitingPong: boolean;
  } {
    return {
      state: this.state,
      reconnectAttempt: this.reconnectAttempt,
      timeSinceLastPong: this.timeSinceLastPong,
      isAwaitingPong: this.awaitingPong
    };
  }
}
