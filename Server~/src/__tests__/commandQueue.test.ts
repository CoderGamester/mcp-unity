import { jest, describe, it, expect, beforeEach, afterEach } from '@jest/globals';
import { CommandQueue, CommandQueueConfig, QueuedCommand } from '../unity/commandQueue';
import { Logger, LogLevel } from '../utils/logger';
import { McpUnityError, ErrorType } from '../utils/errors';

// Create a silent logger for tests
const createTestLogger = (): Logger => {
  return new Logger('Test', LogLevel.ERROR);
};

describe('CommandQueue', () => {
  let queue: CommandQueue;
  let logger: Logger;

  beforeEach(() => {
    logger = createTestLogger();
    queue = new CommandQueue(logger);
  });

  afterEach(() => {
    queue.dispose();
  });

  describe('constructor', () => {
    it('should create with default configuration', () => {
      const stats = queue.getStats();
      expect(stats.maxSize).toBe(100);
      expect(stats.size).toBe(0);
    });

    it('should accept custom configuration', () => {
      const customQueue = new CommandQueue(logger, {
        maxSize: 50,
        defaultTimeout: 30000,
        cleanupInterval: 1000,
      });

      const stats = customQueue.getStats();
      expect(stats.maxSize).toBe(50);

      customQueue.dispose();
    });
  });

  describe('enqueue', () => {
    it('should successfully enqueue a command', () => {
      const result = queue.enqueue({
        id: 'test-1',
        request: { method: 'test', params: {} },
        resolve: jest.fn(),
        reject: jest.fn(),
      });

      expect(result.success).toBe(true);
      expect(result.position).toBe(1);
      expect(queue.size).toBe(1);
    });

    it('should enqueue multiple commands in order', () => {
      for (let i = 1; i <= 3; i++) {
        const result = queue.enqueue({
          id: `test-${i}`,
          request: { method: 'test', params: {} },
          resolve: jest.fn(),
          reject: jest.fn(),
        });
        expect(result.position).toBe(i);
      }

      expect(queue.size).toBe(3);
    });

    it('should reject commands when queue is full', () => {
      const smallQueue = new CommandQueue(logger, { maxSize: 2 });
      const rejectFn = jest.fn();

      // Fill the queue
      smallQueue.enqueue({
        id: 'test-1',
        request: { method: 'test', params: {} },
        resolve: jest.fn(),
        reject: jest.fn(),
      });
      smallQueue.enqueue({
        id: 'test-2',
        request: { method: 'test', params: {} },
        resolve: jest.fn(),
        reject: jest.fn(),
      });

      // Try to add one more
      const result = smallQueue.enqueue({
        id: 'test-3',
        request: { method: 'test', params: {} },
        resolve: jest.fn(),
        reject: rejectFn,
      });

      expect(result.success).toBe(false);
      expect(result.reason).toContain('Queue is full');
      expect(rejectFn).toHaveBeenCalled();
      expect(smallQueue.getStats().droppedCount).toBe(1);

      smallQueue.dispose();
    });
  });

  describe('size and isEmpty', () => {
    it('should report correct size', () => {
      expect(queue.size).toBe(0);
      expect(queue.isEmpty).toBe(true);

      queue.enqueue({
        id: 'test-1',
        request: { method: 'test', params: {} },
        resolve: jest.fn(),
        reject: jest.fn(),
      });

      expect(queue.size).toBe(1);
      expect(queue.isEmpty).toBe(false);
    });

    it('should report isFull correctly', () => {
      const smallQueue = new CommandQueue(logger, { maxSize: 1 });

      expect(smallQueue.isFull).toBe(false);

      smallQueue.enqueue({
        id: 'test-1',
        request: { method: 'test', params: {} },
        resolve: jest.fn(),
        reject: jest.fn(),
      });

      expect(smallQueue.isFull).toBe(true);

      smallQueue.dispose();
    });
  });

  describe('drain', () => {
    it('should return all queued commands and clear the queue', () => {
      const resolve1 = jest.fn();
      const resolve2 = jest.fn();

      queue.enqueue({
        id: 'test-1',
        request: { method: 'method1', params: {} },
        resolve: resolve1,
        reject: jest.fn(),
      });
      queue.enqueue({
        id: 'test-2',
        request: { method: 'method2', params: {} },
        resolve: resolve2,
        reject: jest.fn(),
      });

      expect(queue.size).toBe(2);

      const commands = queue.drain();

      expect(commands.length).toBe(2);
      expect(commands[0].id).toBe('test-1');
      expect(commands[1].id).toBe('test-2');
      expect(queue.size).toBe(0);
      expect(queue.isEmpty).toBe(true);
    });

    it('should return empty array when queue is empty', () => {
      const commands = queue.drain();
      expect(commands).toEqual([]);
    });
  });

  describe('peek', () => {
    it('should return the first command without removing it', () => {
      queue.enqueue({
        id: 'test-1',
        request: { method: 'test', params: {} },
        resolve: jest.fn(),
        reject: jest.fn(),
      });

      const peeked = queue.peek();

      expect(peeked).toBeDefined();
      expect(peeked?.id).toBe('test-1');
      expect(queue.size).toBe(1); // Still in queue
    });

    it('should return undefined for empty queue', () => {
      const peeked = queue.peek();
      expect(peeked).toBeUndefined();
    });
  });

  describe('clear', () => {
    it('should clear all commands and reject them', () => {
      const reject1 = jest.fn();
      const reject2 = jest.fn();

      queue.enqueue({
        id: 'test-1',
        request: { method: 'test', params: {} },
        resolve: jest.fn(),
        reject: reject1,
      });
      queue.enqueue({
        id: 'test-2',
        request: { method: 'test', params: {} },
        resolve: jest.fn(),
        reject: reject2,
      });

      queue.clear('Test clear');

      expect(queue.size).toBe(0);
      expect(reject1).toHaveBeenCalled();
      expect(reject2).toHaveBeenCalled();

      // Check that rejection includes correct error type
      const error1 = reject1.mock.calls[0][0] as McpUnityError;
      expect(error1.type).toBe(ErrorType.CONNECTION);
      expect(error1.message).toBe('Test clear');
    });

    it('should handle clearing empty queue', () => {
      expect(() => queue.clear()).not.toThrow();
    });
  });

  describe('cleanupExpired', () => {
    it('should remove expired commands', async () => {
      const shortTimeoutQueue = new CommandQueue(logger, {
        defaultTimeout: 50, // 50ms timeout
        cleanupInterval: 100000, // Long interval so we control cleanup
      });

      const rejectFn = jest.fn();

      shortTimeoutQueue.enqueue({
        id: 'test-1',
        request: { method: 'test', params: {} },
        resolve: jest.fn(),
        reject: rejectFn,
      });

      expect(shortTimeoutQueue.size).toBe(1);

      // Wait for expiration
      await new Promise(resolve => setTimeout(resolve, 100));

      const expiredCount = shortTimeoutQueue.cleanupExpired();

      expect(expiredCount).toBe(1);
      expect(shortTimeoutQueue.size).toBe(0);
      expect(rejectFn).toHaveBeenCalled();

      const error = rejectFn.mock.calls[0][0] as McpUnityError;
      expect(error.type).toBe(ErrorType.TIMEOUT);
      expect(shortTimeoutQueue.getStats().expiredCount).toBe(1);

      shortTimeoutQueue.dispose();
    });

    it('should not remove non-expired commands', () => {
      queue.enqueue({
        id: 'test-1',
        request: { method: 'test', params: {} },
        resolve: jest.fn(),
        reject: jest.fn(),
      });

      const expiredCount = queue.cleanupExpired();

      expect(expiredCount).toBe(0);
      expect(queue.size).toBe(1);
    });

    it('should respect per-command timeout', async () => {
      const rejectFn = jest.fn();

      // Default timeout is 60s, but we set a short custom timeout
      queue.enqueue({
        id: 'test-1',
        request: { method: 'test', params: {} },
        resolve: jest.fn(),
        reject: rejectFn,
        timeout: 50, // 50ms timeout
      });

      await new Promise(resolve => setTimeout(resolve, 100));

      const expiredCount = queue.cleanupExpired();

      expect(expiredCount).toBe(1);
      expect(rejectFn).toHaveBeenCalled();
    });
  });

  describe('statistics', () => {
    it('should track statistics correctly', () => {
      // Initial stats
      let stats = queue.getStats();
      expect(stats.size).toBe(0);
      expect(stats.droppedCount).toBe(0);
      expect(stats.expiredCount).toBe(0);
      expect(stats.replayedCount).toBe(0);

      // Add a command
      queue.enqueue({
        id: 'test-1',
        request: { method: 'test', params: {} },
        resolve: jest.fn(),
        reject: jest.fn(),
      });

      stats = queue.getStats();
      expect(stats.size).toBe(1);

      // Record replay success
      queue.recordReplaySuccess();
      stats = queue.getStats();
      expect(stats.replayedCount).toBe(1);
    });

    it('should reset statistics', () => {
      queue.recordReplaySuccess();
      queue.recordReplaySuccess();

      let stats = queue.getStats();
      expect(stats.replayedCount).toBe(2);

      queue.resetStats();

      stats = queue.getStats();
      expect(stats.droppedCount).toBe(0);
      expect(stats.expiredCount).toBe(0);
      expect(stats.replayedCount).toBe(0);
    });
  });

  describe('updateConfig', () => {
    it('should update configuration dynamically', () => {
      queue.updateConfig({ maxSize: 50 });

      const stats = queue.getStats();
      expect(stats.maxSize).toBe(50);
    });
  });

  describe('dispose', () => {
    it('should clean up resources and reject pending commands', () => {
      const rejectFn = jest.fn();

      queue.enqueue({
        id: 'test-1',
        request: { method: 'test', params: {} },
        resolve: jest.fn(),
        reject: rejectFn,
      });

      queue.dispose();

      expect(queue.size).toBe(0);
      expect(rejectFn).toHaveBeenCalled();
    });
  });

  describe('drain filters expired commands', () => {
    it('should filter out expired commands when draining', async () => {
      const shortTimeoutQueue = new CommandQueue(logger, {
        defaultTimeout: 50,
        cleanupInterval: 100000,
      });

      const rejectFn = jest.fn();
      const resolveFn = jest.fn();

      // Add an expired command
      shortTimeoutQueue.enqueue({
        id: 'expired',
        request: { method: 'test', params: {} },
        resolve: jest.fn(),
        reject: rejectFn,
        timeout: 10,
      });

      // Add a non-expired command
      shortTimeoutQueue.enqueue({
        id: 'valid',
        request: { method: 'test', params: {} },
        resolve: resolveFn,
        reject: jest.fn(),
        timeout: 60000,
      });

      // Wait for first command to expire
      await new Promise(resolve => setTimeout(resolve, 50));

      const commands = shortTimeoutQueue.drain();

      expect(commands.length).toBe(1);
      expect(commands[0].id).toBe('valid');
      expect(rejectFn).toHaveBeenCalled();

      shortTimeoutQueue.dispose();
    });
  });
});
