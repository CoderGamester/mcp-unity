import { jest, describe, it, expect, beforeEach, afterEach } from '@jest/globals';
import { Logger, LogLevel } from '../utils/logger.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { McpUnity, ConnectionState } from '../unity/mcpUnity.js';
import { registerTransformTools } from '../tools/transformTools.js';
import path from 'path';
import { z } from 'zod';
import { zodToJsonSchema } from 'zod-to-json-schema';

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

describe('Request timeout handling', () => {
  beforeEach(() => {
    jest.useFakeTimers();
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  it('rejects timed out requests without forcing a reconnect', async () => {
    const logger = new Logger('Test', LogLevel.ERROR);
    const unity = new McpUnity(logger, { queueingEnabled: false });
    const connection = {
      isConnected: true,
      isConnecting: false,
      connectionState: ConnectionState.Connected,
      send: jest.fn(),
      connect: jest.fn(),
      disconnect: jest.fn(),
      removeAllListeners: jest.fn(),
      forceReconnect: jest.fn(),
      getStats: jest.fn(() => ({
        state: ConnectionState.Connected,
        reconnectAttempt: 0,
        timeSinceLastPong: 0
      }))
    };

    (unity as any).connection = connection;

    const request = {
      id: 'request-timeout',
      method: 'run_tests',
      params: { mode: 'edit' }
    };

    const promise = unity.sendRequest(request, { timeout: 50 });
    const timeoutResult = expect(promise).rejects.toMatchObject({
      type: ErrorType.TIMEOUT,
      message: 'Request timed out'
    });

    expect(connection.send).toHaveBeenCalledWith(JSON.stringify(request));

    await jest.advanceTimersByTimeAsync(50);
    await timeoutResult;

    expect(connection.forceReconnect).not.toHaveBeenCalled();
    expect(unity.getConnectionStats().pendingRequests).toBe(0);
    expect(unity.connectionState).toBe(ConnectionState.Connected);

    await unity.stop();
  });
});

describe('Authentication startup failures', () => {
  it('surfaces a stored authentication failure on subsequent tool calls', async () => {
    const logger = new Logger('Test', LogLevel.ERROR);
    const unity = new McpUnity(logger, { queueingEnabled: true });
    const authenticationError = new McpUnityError(
      ErrorType.AUTHENTICATION,
      'Authentication token is unavailable'
    );
    (unity as any).startupError = authenticationError;

    await expect(unity.sendRequest({
      method: 'get_play_mode_status',
      params: {}
    })).rejects.toBe(authenticationError);
  });
});

describe('Unity request/response diagnostics', () => {
  const createMockLogger = () => ({
    debug: jest.fn(),
    info: jest.fn(),
    warn: jest.fn(),
    error: jest.fn()
  });

  it('logs request id and connection state when sending to Unity', async () => {
    const logger = createMockLogger();
    const unity = new McpUnity(logger as any, { queueingEnabled: false });
    const connection = {
      isConnected: true,
      isConnecting: false,
      connectionState: ConnectionState.Connected,
      send: jest.fn(),
      connect: jest.fn(),
      disconnect: jest.fn(),
      removeAllListeners: jest.fn(),
      forceReconnect: jest.fn(),
      getStats: jest.fn(() => ({
        state: ConnectionState.Connected,
        reconnectAttempt: 0,
        timeSinceLastPong: 0
      }))
    };

    (unity as any).connection = connection;

    const promise = unity.sendRequest({
      id: 'request-diagnostics',
      method: 'get_scene_info',
      params: {}
    });

    (unity as any).handleMessage(JSON.stringify({
      id: 'request-diagnostics',
      result: { success: true }
    }));

    await expect(promise).resolves.toEqual({ success: true });
    expect(logger.info).toHaveBeenCalledWith(
      'Sending Unity request request-diagnostics (get_scene_info) while connection state is connected; pending requests before send: 0'
    );
    expect(logger.info).toHaveBeenCalledWith(
      'Received Unity response for request request-diagnostics; pending requests before match: 1'
    );

    await unity.stop();
  });

  it('logs ignored Unity responses without a matching pending request', async () => {
    const logger = createMockLogger();
    const unity = new McpUnity(logger as any, { queueingEnabled: false });

    (unity as any).handleMessage(JSON.stringify({
      id: 'unknown-request',
      result: { success: true }
    }));

    expect(logger.warn).toHaveBeenCalledWith(
      'Ignoring Unity response for unknown request unknown-request; pending requests: 0'
    );

    await unity.stop();
  });
});

describe('Transform schema compatibility', () => {
  const mockSendRequest = jest.fn();
  const mockMcpUnity = { sendRequest: mockSendRequest };
  const mockLogger = {
    info: jest.fn(),
    debug: jest.fn(),
    warn: jest.fn(),
    error: jest.fn()
  };
  const mockServerTool = jest.fn();
  const mockServer = { tool: mockServerTool };

  function collectLocalPropertyRefs(node: unknown, refs: string[] = []): string[] {
    if (Array.isArray(node)) {
      for (const item of node) {
        collectLocalPropertyRefs(item, refs);
      }
      return refs;
    }

    if (!node || typeof node !== 'object') {
      return refs;
    }

    for (const [key, value] of Object.entries(node)) {
      if (key === '$ref' && typeof value === 'string' && value.startsWith('#/properties/')) {
        refs.push(value);
      }
      collectLocalPropertyRefs(value, refs);
    }

    return refs;
  }

  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('registers transform tools', () => {
    registerTransformTools(mockServer as any, mockMcpUnity as any, mockLogger as any);

    expect(mockServerTool).toHaveBeenCalledTimes(4);
    expect(mockServerTool).toHaveBeenCalledWith('move_gameobject', expect.any(String), expect.any(Object), expect.any(Function));
    expect(mockServerTool).toHaveBeenCalledWith('rotate_gameobject', expect.any(String), expect.any(Object), expect.any(Function));
    expect(mockServerTool).toHaveBeenCalledWith('scale_gameobject', expect.any(String), expect.any(Object), expect.any(Function));
    expect(mockServerTool).toHaveBeenCalledWith('set_transform', expect.any(String), expect.any(Object), expect.any(Function));
  });

  it('does not emit local #/properties refs for transform tool schemas', () => {
    registerTransformTools(mockServer as any, mockMcpUnity as any, mockLogger as any);

    for (const call of mockServerTool.mock.calls) {
      const paramsShape = call[2];
      const schemaJson = zodToJsonSchema(z.object(paramsShape), { strictUnions: true });
      const refs = collectLocalPropertyRefs(schemaJson);

      expect(refs).toEqual([]);
    }
  });
});
