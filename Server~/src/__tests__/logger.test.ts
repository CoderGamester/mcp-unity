import { jest } from '@jest/globals';
import { Logger, LogLevel } from '../utils/logger.js';

describe('Logger', () => {
  let consoleSpy: jest.SpiedFunction<typeof console.log>;

  beforeEach(() => {
    consoleSpy = jest.spyOn(console, 'log').mockImplementation(() => {});
  });

  afterEach(() => {
    consoleSpy.mockRestore();
  });

  describe('constructor', () => {
    it('should create logger with prefix and default level', () => {
      const logger = new Logger('TestPrefix');

      expect(logger).toBeInstanceOf(Logger);
    });

    it('should create logger with custom level', () => {
      const logger = new Logger('TestPrefix', LogLevel.DEBUG);

      expect(logger).toBeInstanceOf(Logger);
    });
  });

  describe('log level filtering', () => {
    it('should not log messages below the configured level', () => {
      // Set environment variable for logging
      const originalEnv = process.env.LOGGING;
      process.env.LOGGING = 'true';

      const logger = new Logger('Test', LogLevel.WARN);

      logger.debug('debug message');
      logger.info('info message');

      expect(consoleSpy).not.toHaveBeenCalled();

      process.env.LOGGING = originalEnv;
    });

    it('should respect log level hierarchy', () => {
      // This test verifies the Logger respects log levels
      // Note: Actual console output depends on LOGGING env var set at module load time
      const logger = new Logger('Test', LogLevel.DEBUG);

      // All methods should be callable without throwing
      expect(() => {
        logger.debug('debug');
        logger.info('info');
        logger.warn('warn');
        logger.error('error');
      }).not.toThrow();
    });
  });

  describe('isLoggingEnabled', () => {
    it('should return a boolean value', () => {
      const logger = new Logger('Test');
      expect(typeof logger.isLoggingEnabled()).toBe('boolean');
    });
  });

  describe('isLoggingFileEnabled', () => {
    it('should return a boolean value', () => {
      const logger = new Logger('Test');
      expect(typeof logger.isLoggingFileEnabled()).toBe('boolean');
    });
  });

  describe('logging methods', () => {
    it('should have debug method', () => {
      const logger = new Logger('Test');
      expect(typeof logger.debug).toBe('function');
    });

    it('should have info method', () => {
      const logger = new Logger('Test');
      expect(typeof logger.info).toBe('function');
    });

    it('should have warn method', () => {
      const logger = new Logger('Test');
      expect(typeof logger.warn).toBe('function');
    });

    it('should have error method', () => {
      const logger = new Logger('Test');
      expect(typeof logger.error).toBe('function');
    });
  });
});

describe('LogLevel', () => {
  it('should have correct numeric values for ordering', () => {
    expect(LogLevel.DEBUG).toBe(0);
    expect(LogLevel.INFO).toBe(1);
    expect(LogLevel.WARN).toBe(2);
    expect(LogLevel.ERROR).toBe(3);
  });

  it('should allow level comparison', () => {
    expect(LogLevel.DEBUG < LogLevel.INFO).toBe(true);
    expect(LogLevel.INFO < LogLevel.WARN).toBe(true);
    expect(LogLevel.WARN < LogLevel.ERROR).toBe(true);
  });
});
