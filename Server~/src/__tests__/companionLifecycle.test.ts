import { EventEmitter } from 'node:events';
import { jest } from '@jest/globals';
import { installShutdownHandlers } from '../companionLifecycle.js';

describe('companion process shutdown', () => {
  test.each(['SIGINT', 'SIGTERM'] as const)(
    'closes the official child/client and outer server on %s',
    async (signal) => {
      const signals = new EventEmitter();
      const stdin = new EventEmitter();
      const closeOfficialClient = jest.fn(async () => undefined);
      const closeServer = jest.fn(async () => undefined);
      const handlers = installShutdownHandlers({
        signals,
        stdin,
        closeOfficialClient,
        closeServer,
      });

      signals.emit(signal);
      await handlers.shutdown();

      expect(closeOfficialClient).toHaveBeenCalledTimes(1);
      expect(closeServer).toHaveBeenCalledTimes(1);
      handlers.dispose();
    },
  );

  test.each(['close', 'end'] as const)(
    'closes once when stdin emits %s',
    async (event) => {
      const signals = new EventEmitter();
      const stdin = new EventEmitter();
      const closeOfficialClient = jest.fn(async () => undefined);
      const closeServer = jest.fn(async () => undefined);
      const handlers = installShutdownHandlers({
        signals,
        stdin,
        closeOfficialClient,
        closeServer,
      });

      stdin.emit(event);
      stdin.emit(event);
      await handlers.shutdown();

      expect(closeOfficialClient).toHaveBeenCalledTimes(1);
      expect(closeServer).toHaveBeenCalledTimes(1);
      handlers.dispose();
    },
  );
});
