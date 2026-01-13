import { Logger } from '../utils/logger.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';

/**
 * Represents a queued command with its metadata
 */
export interface QueuedCommand {
  /** Unique identifier for the command */
  id: string;
  /** The request to send to Unity */
  request: {
    id?: string;
    method: string;
    params: any;
  };
  /** Resolve callback for the promise */
  resolve: (value: any) => void;
  /** Reject callback for the promise */
  reject: (reason: any) => void;
  /** Timestamp when the command was queued */
  queuedAt: number;
  /** Optional custom timeout for this specific command (in ms) */
  timeout?: number;
}

/**
 * Configuration options for the CommandQueue
 */
export interface CommandQueueConfig {
  /** Maximum number of commands to queue (default: 100) */
  maxSize?: number;
  /** Default timeout in milliseconds for queued commands (default: 60000) */
  defaultTimeout?: number;
  /** Interval in milliseconds to check for expired commands (default: 5000) */
  cleanupInterval?: number;
}

/**
 * Statistics about the command queue
 */
export interface CommandQueueStats {
  /** Current number of queued commands */
  size: number;
  /** Maximum queue size */
  maxSize: number;
  /** Number of commands that were dropped due to queue overflow */
  droppedCount: number;
  /** Number of commands that expired while queued */
  expiredCount: number;
  /** Number of commands successfully replayed */
  replayedCount: number;
}

/**
 * Result of attempting to enqueue a command
 */
export interface EnqueueResult {
  /** Whether the command was successfully queued */
  success: boolean;
  /** Position in queue (1-indexed) if successful */
  position?: number;
  /** Reason for failure if not successful */
  reason?: string;
}

/**
 * Default configuration values
 */
const DEFAULT_CONFIG = {
  maxSize: 100,
  defaultTimeout: 60000, // 60 seconds
  cleanupInterval: 5000  // 5 seconds
};

/**
 * CommandQueue manages commands that are queued when the Unity connection is unavailable.
 * Commands are stored and replayed when the connection is restored.
 */
export class CommandQueue {
  private queue: QueuedCommand[] = [];
  private config: Required<CommandQueueConfig>;
  private cleanupTimer: NodeJS.Timeout | null = null;
  private logger: Logger;

  // Statistics
  private droppedCount: number = 0;
  private expiredCount: number = 0;
  private replayedCount: number = 0;

  constructor(logger: Logger, config: CommandQueueConfig = {}) {
    this.logger = logger;
    this.config = {
      ...DEFAULT_CONFIG,
      ...config
    };

    // Start cleanup timer
    this.startCleanupTimer();
  }

  /**
   * Enqueue a command to be sent when the connection is restored
   * @param command The command to queue (without queuedAt, which will be added automatically)
   * @returns Result indicating whether the command was queued successfully
   */
  public enqueue(command: Omit<QueuedCommand, 'queuedAt'>): EnqueueResult {
    // Check if queue is full
    if (this.queue.length >= this.config.maxSize) {
      this.droppedCount++;
      this.logger.warn(`Command queue full (${this.config.maxSize}), dropping command: ${command.request.method}`);

      // Reject the command immediately
      command.reject(new McpUnityError(
        ErrorType.CONNECTION,
        `Command queue full (${this.config.maxSize} commands). Try again later.`
      ));

      return {
        success: false,
        reason: `Queue is full (max: ${this.config.maxSize})`
      };
    }

    const queuedCommand: QueuedCommand = {
      ...command,
      queuedAt: Date.now(),
      timeout: command.timeout ?? this.config.defaultTimeout
    };

    this.queue.push(queuedCommand);
    const position = this.queue.length;

    this.logger.debug(`Queued command ${command.id} (${command.request.method}), position: ${position}/${this.config.maxSize}`);

    return {
      success: true,
      position
    };
  }

  /**
   * Get the number of commands currently in the queue
   */
  public get size(): number {
    return this.queue.length;
  }

  /**
   * Check if the queue is empty
   */
  public get isEmpty(): boolean {
    return this.queue.length === 0;
  }

  /**
   * Check if the queue is full
   */
  public get isFull(): boolean {
    return this.queue.length >= this.config.maxSize;
  }

  /**
   * Get all queued commands and clear the queue.
   * Used when connection is restored to replay commands.
   * Expired commands are filtered out and rejected before returning.
   * @returns Array of valid (non-expired) queued commands
   */
  public drain(): QueuedCommand[] {
    // Clean up expired commands first
    this.cleanupExpired();

    const commands = [...this.queue];
    this.queue = [];

    if (commands.length > 0) {
      this.logger.info(`Draining ${commands.length} commands from queue for replay`);
    }

    return commands;
  }

  /**
   * Peek at the next command without removing it
   * @returns The next command in the queue, or undefined if empty
   */
  public peek(): QueuedCommand | undefined {
    return this.queue[0];
  }

  /**
   * Clear all queued commands, rejecting each with an error
   * @param reason The reason for clearing the queue
   */
  public clear(reason: string = 'Queue cleared'): void {
    const count = this.queue.length;

    for (const command of this.queue) {
      command.reject(new McpUnityError(
        ErrorType.CONNECTION,
        reason
      ));
    }

    this.queue = [];

    if (count > 0) {
      this.logger.info(`Cleared ${count} commands from queue: ${reason}`);
    }
  }

  /**
   * Remove expired commands from the queue
   * @returns Number of commands that were expired and removed
   */
  public cleanupExpired(): number {
    const now = Date.now();
    const initialSize = this.queue.length;

    this.queue = this.queue.filter(command => {
      const timeout = command.timeout ?? this.config.defaultTimeout;
      const isExpired = (now - command.queuedAt) > timeout;

      if (isExpired) {
        this.expiredCount++;
        this.logger.debug(`Command ${command.id} (${command.request.method}) expired after ${timeout}ms`);

        command.reject(new McpUnityError(
          ErrorType.TIMEOUT,
          `Command expired after ${timeout}ms in queue`
        ));

        return false;
      }

      return true;
    });

    const expiredCount = initialSize - this.queue.length;
    if (expiredCount > 0) {
      this.logger.info(`Cleaned up ${expiredCount} expired commands from queue`);
    }

    return expiredCount;
  }

  /**
   * Start the periodic cleanup timer
   */
  private startCleanupTimer(): void {
    if (this.cleanupTimer) {
      clearInterval(this.cleanupTimer);
    }

    this.cleanupTimer = setInterval(() => {
      this.cleanupExpired();
    }, this.config.cleanupInterval);

    // Don't prevent process exit
    this.cleanupTimer.unref();
  }

  /**
   * Stop the cleanup timer
   */
  public stopCleanupTimer(): void {
    if (this.cleanupTimer) {
      clearInterval(this.cleanupTimer);
      this.cleanupTimer = null;
    }
  }

  /**
   * Record that a command was successfully replayed
   */
  public recordReplaySuccess(): void {
    this.replayedCount++;
  }

  /**
   * Get statistics about the command queue
   */
  public getStats(): CommandQueueStats {
    return {
      size: this.queue.length,
      maxSize: this.config.maxSize,
      droppedCount: this.droppedCount,
      expiredCount: this.expiredCount,
      replayedCount: this.replayedCount
    };
  }

  /**
   * Reset statistics counters
   */
  public resetStats(): void {
    this.droppedCount = 0;
    this.expiredCount = 0;
    this.replayedCount = 0;
  }

  /**
   * Update configuration dynamically
   * Note: maxSize changes won't affect already-queued commands
   */
  public updateConfig(config: Partial<CommandQueueConfig>): void {
    this.config = { ...this.config, ...config };

    // Restart cleanup timer if interval changed
    if (config.cleanupInterval !== undefined) {
      this.startCleanupTimer();
    }
  }

  /**
   * Clean up resources
   */
  public dispose(): void {
    this.stopCleanupTimer();
    this.clear('Command queue disposed');
  }
}
