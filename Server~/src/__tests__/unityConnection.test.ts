import { jest, describe, it, expect, beforeEach, afterEach } from '@jest/globals';

// Mock WebSocket before importing modules that use it
jest.unstable_mockModule('ws', () => ({
  default: jest.fn().mockImplementation(() => ({
    readyState: 1,
    onopen: null,
    onclose: null,
    onerror: null,
    onmessage: null,
    send: jest.fn(),
    close: jest.fn(),
    terminate: jest.fn(),
    ping: jest.fn(),
    on: jest.fn(),
    removeAllListeners: jest.fn()
  })),
  WebSocket: {
    CONNECTING: 0,
    OPEN: 1,
    CLOSING: 2,
    CLOSED: 3
  }
}));

// Dynamic imports after mocking
const { UnityConnection, ConnectionState } = await import('../unity/unityConnection');
const { Logger, LogLevel } = await import('../utils/logger');
const { McpUnityError, ErrorType } = await import('../utils/errors');

// Type imports
import type { ConnectionStateChange } from '../unity/unityConnection';

// Create a logger that doesn't output anything (for testing)
const createTestLogger = () => {
  process.env.LOGGING = 'false';
  process.env.LOGGING_FILE = 'false';
  return new Logger('Test', LogLevel.ERROR);
};

describe('UnityConnection', () => {
  let connection: InstanceType<typeof UnityConnection>;
  let testLogger: InstanceType<typeof Logger>;

  beforeEach(() => {
    testLogger = createTestLogger();
    connection = new UnityConnection(testLogger, {
      host: 'localhost',
      port: 8090,
      requestTimeout: 5000,
      clientName: 'TestClient',
      minReconnectDelay: 100,
      maxReconnectDelay: 1000,
      heartbeatInterval: 0
    });
  });

  afterEach(() => {
    connection.disconnect();
    jest.clearAllMocks();
  });

  describe('Initial State', () => {
    it('should start in disconnected state', () => {
      expect(connection.connectionState).toBe(ConnectionState.Disconnected);
    });

    it('should not be connected initially', () => {
      expect(connection.isConnected).toBe(false);
    });

    it('should not be connecting initially', () => {
      expect(connection.isConnecting).toBe(false);
    });

    it('should have -1 for timeSinceLastPong before any connection', () => {
      expect(connection.timeSinceLastPong).toBe(-1);
    });
  });

  describe('State Change Events', () => {
    it('should emit stateChange event when connect is called', (done) => {
      let firstEvent = true;
      connection.on('stateChange', (change: ConnectionStateChange) => {
        // Only check the first state change event
        if (firstEvent && change.currentState === ConnectionState.Connecting) {
          firstEvent = false;
          expect(change.previousState).toBe(ConnectionState.Disconnected);
          expect(change.currentState).toBe(ConnectionState.Connecting);
          done();
        }
      });

      connection.connect().catch(() => {});
    });

    it('should include reason in state change', (done) => {
      let eventReceived = false;
      connection.on('stateChange', (change: ConnectionStateChange) => {
        if (!eventReceived && change.currentState === ConnectionState.Connecting) {
          eventReceived = true;
          expect(change.reason).toBeDefined();
          done();
        }
      });

      connection.connect().catch(() => {});
    });
  });

  describe('Configuration', () => {
    it('should update configuration dynamically', () => {
      connection.updateConfig({ heartbeatInterval: 60000 });
      expect(connection.connectionState).toBe(ConnectionState.Disconnected);
    });
  });

  describe('getStats', () => {
    it('should return correct stats in initial state', () => {
      const stats = connection.getStats();
      expect(stats.state).toBe(ConnectionState.Disconnected);
      expect(stats.reconnectAttempt).toBe(0);
      expect(stats.timeSinceLastPong).toBe(-1);
      expect(stats.isAwaitingPong).toBe(false);
    });
  });

  describe('Disconnect', () => {
    it('should set state to disconnected on manual disconnect', () => {
      connection.disconnect('Test disconnect');
      expect(connection.connectionState).toBe(ConnectionState.Disconnected);
    });

    it('should emit stateChange event when disconnecting from connecting state', (done) => {
      // First start connecting, then disconnect
      connection.on('stateChange', (change: ConnectionStateChange) => {
        if (change.currentState === ConnectionState.Disconnected &&
            change.previousState !== ConnectionState.Disconnected) {
          done();
        }
      });

      // Start connection then immediately disconnect
      connection.connect().catch(() => {});
      // Give time for the connecting state to be set
      setTimeout(() => {
        connection.disconnect('Test disconnect');
      }, 10);
    });
  });

  describe('Send', () => {
    it('should throw error when not connected', () => {
      expect(() => connection.send('test')).toThrow(McpUnityError);
    });
  });

  describe('forceReconnect', () => {
    it('should trigger connecting state', () => {
      connection.forceReconnect();
      expect(connection.isConnecting).toBe(true);
    });
  });
});

describe('ConnectionState Enum', () => {
  it('should have correct values', () => {
    expect(ConnectionState.Disconnected).toBe('disconnected');
    expect(ConnectionState.Connecting).toBe('connecting');
    expect(ConnectionState.Connected).toBe('connected');
    expect(ConnectionState.Reconnecting).toBe('reconnecting');
  });
});

describe('Exponential Backoff Configuration', () => {
  it('should accept backoff configuration', () => {
    const testLogger = createTestLogger();
    const connection = new UnityConnection(testLogger, {
      host: 'localhost',
      port: 8090,
      requestTimeout: 5000,
      minReconnectDelay: 1000,
      maxReconnectDelay: 30000,
      reconnectBackoffMultiplier: 2
    });

    expect(connection.connectionState).toBe(ConnectionState.Disconnected);
    connection.disconnect();
  });
});
