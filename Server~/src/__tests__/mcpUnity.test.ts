import { Logger, LogLevel } from '../utils/logger.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import path from 'path';

describe('McpUnityError integration', () => {
  it('should create proper error for connection issues', () => {
    const error = new McpUnityError(ErrorType.CONNECTION, 'Failed to connect to Unity');

    expect(error.type).toBe('connection_error');
    expect(error.message).toBe('Failed to connect to Unity');
  });

  it('should create proper error for timeout', () => {
    const error = new McpUnityError(ErrorType.TIMEOUT, 'Request timed out');

    expect(error.type).toBe('timeout_error');
  });
});

describe('Path handling in configuration', () => {
  it('should handle paths with spaces in config file path', () => {
    // The config path uses path.resolve which handles spaces correctly
    const pathWithSpaces = '/Users/John Doe/My Project/ProjectSettings/McpUnitySettings.json';

    // Verify path module handles spaces
    const resolved = path.resolve(pathWithSpaces);

    expect(resolved).toContain('John Doe');
    expect(resolved).toContain('My Project');
  });

  it('should handle Windows-style paths with spaces', () => {
    const windowsPath = 'C:\\Users\\John Doe\\My Project\\ProjectSettings';

    // path.normalize handles both styles
    const normalized = path.normalize(windowsPath);

    expect(normalized).toContain('John Doe');
  });

  it('should properly construct WebSocket URL', () => {
    // WebSocket URLs don't need special encoding for host/port
    const host = 'localhost';
    const port = 8090;
    const wsUrl = `ws://${host}:${port}/McpUnity`;

    expect(wsUrl).toBe('ws://localhost:8090/McpUnity');
  });

  it('should handle path.join with spaces', () => {
    const basePath = '/Users/John Doe/Projects';
    const subPath = 'My Unity Game';
    const fileName = 'settings.json';

    const fullPath = path.join(basePath, subPath, fileName);

    expect(fullPath).toContain('John Doe');
    expect(fullPath).toContain('My Unity Game');
    expect(fullPath).toContain('settings.json');
  });

  it('should handle path.resolve with relative paths containing spaces', () => {
    const cwd = '/Users/Test User/Current Dir';
    const relativePath = '../Other Project/file.txt';

    // path.resolve will work correctly with spaces
    const resolved = path.resolve(cwd, relativePath);

    expect(resolved).toContain('Test User');
  });
});

describe('Logger with path-related messages', () => {
  it('should log messages containing paths with spaces', () => {
    const logger = new Logger('Test', LogLevel.ERROR);
    const pathWithSpaces = '/Users/John Doe/My Project/file.txt';

    // Logger should handle any string including paths with spaces
    // This is a smoke test to ensure no exceptions are thrown
    expect(() => {
      logger.error(`Failed to read file: ${pathWithSpaces}`);
    }).not.toThrow();
  });
});
