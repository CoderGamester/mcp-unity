import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';
import type {
  Transport,
  TransportSendOptions,
} from '@modelcontextprotocol/sdk/shared/transport.js';
import type {
  JSONRPCMessage,
  MessageExtraInfo,
} from '@modelcontextprotocol/sdk/types.js';
import { jest } from '@jest/globals';
import { OwnedStdioClientTransport } from '../unity/officialUnityMcpClient.js';

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((resolver) => {
    resolve = resolver;
  });
  return { promise, resolve };
}

class FakeTransport implements Transport {
  onclose?: () => void;
  onerror?: (error: Error) => void;
  onmessage?: <T extends JSONRPCMessage>(
    message: T,
    extra?: MessageExtraInfo,
  ) => void;
  sessionId = 'fake-session';
  readonly start = jest.fn(async () => undefined);
  readonly send = jest.fn(
    async (_message: JSONRPCMessage, _options?: TransportSendOptions) =>
      undefined,
  );
  readonly close = jest.fn(async () => undefined);
  readonly setProtocolVersion = jest.fn((_version: string) => undefined);
}

describe('OwnedStdioClientTransport', () => {
  test('delegates the Transport contract and forwards callbacks', async () => {
    const underlying = new FakeTransport();
    const transport = new OwnedStdioClientTransport(underlying, 100);
    const onclose = jest.fn();
    const onerror = jest.fn();
    const onmessage = jest.fn();
    transport.onclose = onclose;
    transport.onerror = onerror;
    transport.onmessage = onmessage;
    const message = {
      jsonrpc: '2.0' as const,
      method: 'notifications/initialized',
    };
    const options = { relatedRequestId: 7 };

    await transport.start();
    await transport.send(message, options);
    transport.setProtocolVersion?.('2025-06-18');
    const error = new Error('stderr warning');
    underlying.onmessage?.(message);
    underlying.onerror?.(error);
    underlying.onclose?.();
    transport.sessionId = 'updated-session';

    expect(underlying.start).toHaveBeenCalledTimes(1);
    expect(underlying.send).toHaveBeenCalledWith(message, options);
    expect(underlying.setProtocolVersion).toHaveBeenCalledWith('2025-06-18');
    expect(underlying.sessionId).toBe('updated-session');
    expect(transport.sessionId).toBe('updated-session');
    expect(onmessage).toHaveBeenCalledWith(message, undefined);
    expect(onerror).toHaveBeenCalledWith(error);
    expect(onclose).toHaveBeenCalledTimes(1);
  });

  test('memoizes close and waits for the actual child close callback', async () => {
    const underlying = new FakeTransport();
    const transport = new OwnedStdioClientTransport(underlying, 100);
    await transport.start();

    const firstClose = transport.close();
    const secondClose = transport.close();
    expect(firstClose).toBe(secondClose);

    let settled = false;
    void firstClose.then(() => {
      settled = true;
    });
    await new Promise((resolve) => setImmediate(resolve));
    expect(underlying.close).toHaveBeenCalledTimes(1);
    expect(settled).toBe(false);

    underlying.onclose?.();
    await firstClose;
    expect(settled).toBe(true);
  });

  test('reports an actionable error if a started child never reports close', async () => {
    const underlying = new FakeTransport();
    const transport = new OwnedStdioClientTransport(underlying, 10);
    await transport.start();

    await expect(transport.close()).rejects.toThrow(
      'Unity CLI child did not report process closure within 10ms',
    );
  });

  test('does not await a child close event when start failed', async () => {
    const underlying = new FakeTransport();
    underlying.start.mockRejectedValueOnce(new Error('spawn failed'));
    const transport = new OwnedStdioClientTransport(underlying, 10);

    await expect(transport.start()).rejects.toThrow('spawn failed');
    await expect(transport.close()).resolves.toBeUndefined();
    expect(underlying.close).toHaveBeenCalledTimes(1);
  });

  test('spontaneous close resolves the barrier and forwards onclose exactly once', async () => {
    const underlying = new FakeTransport();
    const transport = new OwnedStdioClientTransport(underlying, 100);
    const onclose = jest.fn();
    transport.onclose = onclose;
    await transport.start();

    underlying.onclose?.();
    underlying.onclose?.();

    await expect(transport.close()).resolves.toBeUndefined();
    expect(onclose).toHaveBeenCalledTimes(1);
    expect(underlying.close).toHaveBeenCalledTimes(1);
  });

  const posixTest = process.platform === 'win32' ? test.skip : test;
  posixTest(
    'waits for a real pinned Stdio child close event and leaves no child handle',
    async () => {
      const underlying = new StdioClientTransport({
        command: process.execPath,
        args: [
          '-e',
          [
            "process.on('SIGTERM', () => {});",
            "process.stderr.write('ready\\n');",
            'setInterval(() => {}, 1000);',
          ].join(''),
        ],
        stderr: 'pipe',
      });
      const transport = new OwnedStdioClientTransport(underlying, 1_000);
      const childClosed = deferred<void>();
      let closeEventObserved = false;
      transport.onclose = () => {
        closeEventObserved = true;
        childClosed.resolve();
      };

      const ready = new Promise<void>((resolve) => {
        underlying.stderr?.once('data', () => resolve());
      });
      await transport.start();
      await ready;

      try {
        let closeResolvedBeforeEvent = false;
        const close = transport.close().then(() => {
          closeResolvedBeforeEvent = !closeEventObserved;
        });
        await close;
        await childClosed.promise;

        expect(closeResolvedBeforeEvent).toBe(false);
        expect(closeEventObserved).toBe(true);
      } finally {
        await Promise.allSettled([transport.close()]);
        await childClosed.promise;
      }
    },
    7_000,
  );
});
