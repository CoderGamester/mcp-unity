import { spawn } from 'node:child_process';
import { once } from 'node:events';
import type { Transport } from '@modelcontextprotocol/sdk/shared/transport.js';
import { jest } from '@jest/globals';
import {
  createOfficialUnitySessionStart,
  OfficialUnityMcpClient,
  type OfficialUnitySession,
  type OfficialUnitySessionFactory,
  type OfficialUnitySessionStart,
} from '../unity/officialUnityMcpClient.js';

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((resolver) => {
    resolve = resolver;
  });
  return { promise, resolve };
}

async function completesWithin<T>(
  operation: Promise<T>,
  timeoutMs: number,
): Promise<T | 'timed-out'> {
  let timeout: NodeJS.Timeout | undefined;
  try {
    return await Promise.race([
      operation,
      new Promise<'timed-out'>((resolve) => {
        timeout = setTimeout(() => resolve('timed-out'), timeoutMs);
      }),
    ]);
  } finally {
    if (timeout) clearTimeout(timeout);
  }
}

function session(
  callTool: OfficialUnitySession['callTool'] = async () => ({
    content: [{ type: 'text', text: '{"ok":true}' }],
  }),
): OfficialUnitySession {
  return { callTool };
}

function sessionStart(
  ready: Promise<OfficialUnitySession> | OfficialUnitySession,
  close: jest.Mock = jest.fn(async () => undefined),
): OfficialUnitySessionStart & { close: jest.Mock } {
  return { ready: Promise.resolve(ready), close };
}

function sdkTransport(close: () => Promise<void>): Transport {
  return {
    start: async () => undefined,
    send: async () => undefined,
    close,
  };
}

describe('OfficialUnityMcpClient', () => {
  test('lazily starts exactly one official MCP process for concurrent first reads', async () => {
    const gate = deferred<OfficialUnitySession>();
    const start = sessionStart(gate.promise);
    const factory: jest.MockedFunction<OfficialUnitySessionFactory> = jest.fn(
      () => start,
    );
    const client = new OfficialUnityMcpClient({
      cliPath: '/opt/unity',
      projectPath: '/projects/game',
      sessionFactory: factory,
    });

    expect(factory).not.toHaveBeenCalled();

    const first = client.readTool('get_console_logs', { limit: 10 });
    const second = client.readTool('package_list', { scope: 'installed' });
    expect(factory).toHaveBeenCalledTimes(1);
    expect(factory).toHaveBeenCalledWith({
      cliPath: '/opt/unity',
      projectPath: '/projects/game',
    });

    gate.resolve(session());
    await expect(Promise.all([first, second])).resolves.toHaveLength(2);
    expect(factory).toHaveBeenCalledTimes(1);
    expect(client.state).toBe('connected');
  });

  test('reconnects once and retries the same read-only call exactly once', async () => {
    const firstSession = session(jest.fn(async () => {
      throw new Error('transport closed');
    }));
    const secondCall = jest.fn(async () => ({
      content: [{ type: 'text' as const, text: '{"recovered":true}' }],
    }));
    const secondSession = session(secondCall);
    const firstStart = sessionStart(firstSession);
    const secondStart = sessionStart(secondSession);
    const factory = jest
      .fn<ReturnType<OfficialUnitySessionFactory>, Parameters<OfficialUnitySessionFactory>>()
      .mockReturnValueOnce(firstStart)
      .mockReturnValueOnce(secondStart);
    const client = new OfficialUnityMcpClient({
      cliPath: 'unity',
      projectPath: '/projects/game',
      sessionFactory: factory,
    });

    await expect(client.readTool('list_tests', { mode: 'editor' })).resolves.toEqual({
      content: [{ type: 'text', text: '{"recovered":true}' }],
    });
    expect(factory).toHaveBeenCalledTimes(2);
    expect(firstSession.callTool).toHaveBeenCalledTimes(1);
    expect(secondCall).toHaveBeenCalledTimes(1);
    expect(secondCall).toHaveBeenCalledWith('list_tests', { mode: 'editor' });
    expect(firstStart.close).toHaveBeenCalledTimes(1);
  });

  test('does not retry a second failed read', async () => {
    const factory = jest
      .fn<ReturnType<OfficialUnitySessionFactory>, Parameters<OfficialUnitySessionFactory>>()
      .mockImplementation(() =>
        sessionStart(
          session(async () => {
            throw new Error('Connection closed');
          }),
        ),
      );
    const client = new OfficialUnityMcpClient({
      cliPath: 'unity',
      projectPath: '/projects/game',
      sessionFactory: factory,
    });

    await expect(client.readTool('package_list', {})).rejects.toThrow('Connection closed');
    expect(factory).toHaveBeenCalledTimes(2);
  });

  test('does not reconnect for a non-transport command error', async () => {
    const activeSession = session(async () => {
      throw new Error('Method not found: inspect_gameobject');
    });
    const activeStart = sessionStart(activeSession);
    const factory = jest.fn(() => activeStart);
    const client = new OfficialUnityMcpClient({
      cliPath: 'unity',
      projectPath: '/projects/game',
      sessionFactory: factory,
    });

    await expect(client.readTool('inspect_gameobject', {})).rejects.toThrow(
      'Method not found',
    );
    expect(factory).toHaveBeenCalledTimes(1);
    expect(activeStart.close).not.toHaveBeenCalled();
  });

  test('closes the active child/client and prevents later startup', async () => {
    const activeSession = session();
    const activeStart = sessionStart(activeSession);
    const factory = jest.fn(() => activeStart);
    const client = new OfficialUnityMcpClient({
      cliPath: 'unity',
      projectPath: '/projects/game',
      sessionFactory: factory,
    });
    await client.readTool('get_console_logs', {});

    await client.close();
    await client.close();

    expect(activeStart.close).toHaveBeenCalledTimes(1);
    expect(client.state).toBe('closed');
    await expect(client.readTool('get_console_logs', {})).rejects.toThrow('closed');
    expect(factory).toHaveBeenCalledTimes(1);
  });

  test('close rejects pending reads promptly but waits for actual teardown', async () => {
    const gate = deferred<OfficialUnitySession>();
    const teardown = deferred<void>();
    const start = sessionStart(gate.promise, jest.fn(() => teardown.promise));
    const factory = jest.fn(() => start);
    const client = new OfficialUnityMcpClient({
      cliPath: 'unity',
      projectPath: '/projects/game',
      sessionFactory: factory,
    });
    const read = client.readTool('get_console_logs', {});
    let closeSettled = false;
    const close = client.close().then(() => {
      closeSettled = true;
    });

    await expect(
      Promise.race([
        read,
        new Promise((_, reject) =>
          setTimeout(() => reject(new Error('read did not stop')), 100),
        ),
      ]),
    ).rejects.toThrow('closed');
    await new Promise((resolve) => setImmediate(resolve));
    expect(closeSettled).toBe(false);
    expect(start.close).toHaveBeenCalledTimes(1);

    teardown.resolve();
    await close;
    expect(closeSettled).toBe(true);

    gate.resolve(session());
    await new Promise((resolve) => setImmediate(resolve));
    expect(start.close).toHaveBeenCalledTimes(1);
  });

  test('close does not resolve before an active child teardown resolves', async () => {
    const teardown = deferred<void>();
    const start = sessionStart(session(), jest.fn(() => teardown.promise));
    const client = new OfficialUnityMcpClient({
      cliPath: 'unity',
      projectPath: '/projects/game',
      sessionFactory: () => start,
    });
    await client.readTool('get_console_logs', {});

    let closeSettled = false;
    const close = client.close().then(() => {
      closeSettled = true;
    });
    await new Promise((resolve) => setImmediate(resolve));

    expect(closeSettled).toBe(false);
    expect(start.close).toHaveBeenCalledTimes(1);

    teardown.resolve();
    await close;
    await client.close();
    expect(start.close).toHaveBeenCalledTimes(1);
  });

  test('shutdown interrupts a read whose tool call never resolves', async () => {
    const callGate = deferred<Awaited<ReturnType<OfficialUnitySession['callTool']>>>();
    const start = sessionStart(session(() => callGate.promise));
    const client = new OfficialUnityMcpClient({
      cliPath: 'unity',
      projectPath: '/projects/game',
      sessionFactory: () => start,
    });
    const read = client.readTool('get_console_logs', {});
    await new Promise((resolve) => setImmediate(resolve));

    await client.close();

    await expect(
      Promise.race([
        read,
        new Promise((_, reject) =>
          setTimeout(() => reject(new Error('read did not stop')), 100),
        ),
      ]),
    ).rejects.toThrow('closed');
    expect(start.close).toHaveBeenCalledTimes(1);
  });

  test('close wins a transport-failure retry race without spawning again', async () => {
    let client!: OfficialUnityMcpClient;
    const firstStart = sessionStart(
      session(async () => {
        throw new Error('Connection closed');
      }),
      jest.fn(async () => {
        void client.close();
      }),
    );
    const factory = jest.fn(() => firstStart);
    client = new OfficialUnityMcpClient({
      cliPath: 'unity',
      projectPath: '/projects/game',
      sessionFactory: factory,
    });

    await expect(client.readTool('package_list', {})).rejects.toThrow('closed');
    expect(factory).toHaveBeenCalledTimes(1);
    expect(firstStart.close).toHaveBeenCalledTimes(1);
  });

  test('shutdown closes a pending retry start exactly once', async () => {
    const retryGate = deferred<OfficialUnitySession>();
    const firstStart = sessionStart(
      session(async () => {
        throw new Error('Connection closed');
      }),
    );
    const retryStart = sessionStart(retryGate.promise);
    const factory = jest
      .fn<ReturnType<OfficialUnitySessionFactory>, Parameters<OfficialUnitySessionFactory>>()
      .mockReturnValueOnce(firstStart)
      .mockReturnValueOnce(retryStart);
    const client = new OfficialUnityMcpClient({
      cliPath: 'unity',
      projectPath: '/projects/game',
      sessionFactory: factory,
    });
    const read = client.readTool('package_list', {});
    while (factory.mock.calls.length < 2) {
      await new Promise((resolve) => setImmediate(resolve));
    }

    await client.close();
    await expect(read).rejects.toThrow('closed');
    expect(firstStart.close).toHaveBeenCalledTimes(1);
    expect(retryStart.close).toHaveBeenCalledTimes(1);

    retryGate.resolve(session());
    await new Promise((resolve) => setImmediate(resolve));
    expect(retryStart.close).toHaveBeenCalledTimes(1);
  });

  test('preserves a healthy reconnected session after a command-level retry error', async () => {
    const firstStart = sessionStart(
      session(async () => {
        throw new Error('Connection closed');
      }),
    );
    const retryCall = jest
      .fn<ReturnType<OfficialUnitySession['callTool']>, Parameters<OfficialUnitySession['callTool']>>()
      .mockRejectedValueOnce(new Error('Method not found: inspect_gameobject'))
      .mockResolvedValueOnce({
        content: [{ type: 'text', text: '{"ok":true}' }],
      });
    const retryStart = sessionStart(session(retryCall));
    const factory = jest
      .fn<ReturnType<OfficialUnitySessionFactory>, Parameters<OfficialUnitySessionFactory>>()
      .mockReturnValueOnce(firstStart)
      .mockReturnValueOnce(retryStart);
    const client = new OfficialUnityMcpClient({
      cliPath: 'unity',
      projectPath: '/projects/game',
      sessionFactory: factory,
    });

    await expect(client.readTool('inspect_gameobject', {})).rejects.toThrow(
      'Method not found',
    );
    await expect(client.readTool('get_console_logs', {})).resolves.toMatchObject({
      content: [{ type: 'text', text: '{"ok":true}' }],
    });
    expect(factory).toHaveBeenCalledTimes(2);
    expect(retryStart.close).not.toHaveBeenCalled();
  });

  test('completes old child teardown before invoking the replacement factory', async () => {
    const teardown = deferred<void>();
    const firstStart = sessionStart(
      session(async () => {
        throw new Error('Connection closed');
      }),
      jest.fn(() => teardown.promise),
    );
    const secondStart = sessionStart(session());
    const factory = jest
      .fn<ReturnType<OfficialUnitySessionFactory>, Parameters<OfficialUnitySessionFactory>>()
      .mockReturnValueOnce(firstStart)
      .mockReturnValueOnce(secondStart);
    const client = new OfficialUnityMcpClient({
      cliPath: 'unity',
      projectPath: '/projects/game',
      sessionFactory: factory,
    });

    const read = client.readTool('get_console_logs', {});
    await new Promise((resolve) => setImmediate(resolve));

    expect(firstStart.close).toHaveBeenCalledTimes(1);
    expect(factory).toHaveBeenCalledTimes(1);

    teardown.resolve();
    await expect(read).resolves.toMatchObject({
      content: [{ type: 'text', text: '{"ok":true}' }],
    });
    expect(factory).toHaveBeenCalledTimes(2);
  });
});

describe('official Unity SDK session ownership', () => {
  test('aborts pending SDK initialization and awaits one memoized client teardown', async () => {
    let connectOptions:
      | { signal?: AbortSignal; timeout?: number; maxTotalTimeout?: number }
      | undefined;
    const clientClose = deferred<void>();
    const client = {
      connect: jest.fn(
        async (
          _transport: unknown,
          options: {
            signal?: AbortSignal;
            timeout?: number;
            maxTotalTimeout?: number;
          },
        ) => {
          connectOptions = options;
          await new Promise<void>((_resolve, reject) => {
            options.signal?.addEventListener(
              'abort',
              () => reject(new Error('initialize aborted')),
              { once: true },
            );
          });
        },
      ),
      callTool: jest.fn(),
      close: jest.fn(() => clientClose.promise),
    };
    const transport = {
      pid: null,
      start: async () => undefined,
      send: async () => undefined,
      close: jest.fn(async () => undefined),
    };
    const start = createOfficialUnitySessionStart(
      { cliPath: '/opt/unity', projectPath: '/projects/game' },
      {
        createClient: () => client,
        createTransport: () => transport,
      },
    );
    const ready = start.ready.catch((error: unknown) => error);

    const firstClose = start.close();
    const secondClose = start.close();
    expect(firstClose).toBe(secondClose);
    expect(connectOptions).toMatchObject({
      timeout: 10_000,
      maxTotalTimeout: 10_000,
    });
    expect(connectOptions?.signal?.aborted).toBe(true);
    expect(client.close).toHaveBeenCalledTimes(1);

    let closeSettled = false;
    void firstClose.then(() => {
      closeSettled = true;
    });
    await new Promise((resolve) => setImmediate(resolve));
    expect(closeSettled).toBe(false);

    clientClose.resolve();
    await firstClose;
    await expect(ready).resolves.toBeInstanceOf(Error);
    expect(client.close).toHaveBeenCalledTimes(1);
    expect(transport.close).not.toHaveBeenCalled();
  });

  test('forces direct bounded transport cleanup when SDK client close stalls', async () => {
    const transportClose = deferred<void>();
    const client = {
      connect: jest.fn(async () => undefined),
      callTool: jest.fn(),
      close: jest.fn(() => new Promise<void>(() => undefined)),
    };
    const transport = {
      get pid(): never {
        throw new Error('teardown must not read a raw PID');
      },
      start: async () => undefined,
      send: async () => undefined,
      close: jest.fn(() => transportClose.promise),
    };
    const start = createOfficialUnitySessionStart(
      { cliPath: '/opt/unity', projectPath: '/projects/game' },
      {
        createClient: () => client,
        createTransport: () => transport,
        sdkCloseGraceMs: 10,
        transportCloseTimeoutMs: 100,
      },
    );
    await start.ready;

    let closeSettled = false;
    const close = start.close().then(() => {
      closeSettled = true;
    });
    await new Promise((resolve) => setTimeout(resolve, 25));

    expect(client.close).toHaveBeenCalledTimes(1);
    expect(transport.close).toHaveBeenCalledTimes(1);
    expect(closeSettled).toBe(false);

    transportClose.resolve();
    await close;
    expect(closeSettled).toBe(true);
  });

  test('reports an actionable error when owned transport cleanup rejects', async () => {
    const start = createOfficialUnitySessionStart(
      { cliPath: '/opt/unity', projectPath: '/projects/game' },
      {
        createClient: () => ({
          connect: jest.fn(async () => undefined),
          callTool: jest.fn(),
          close: jest.fn(async () => {
            throw new Error('SDK close failed');
          }),
        }),
        createTransport: () =>
          sdkTransport(jest.fn(async () => {
            throw new Error('owned child cleanup failed');
          })),
        sdkCloseGraceMs: 10,
        transportCloseTimeoutMs: 10,
      },
    );
    await start.ready;

    await expect(start.close()).rejects.toThrow(
      'Unity CLI transport teardown failed: owned child cleanup failed',
    );
  });

  test('reports an actionable error when owned transport cleanup times out', async () => {
    const start = createOfficialUnitySessionStart(
      { cliPath: '/opt/unity', projectPath: '/projects/game' },
      {
        createClient: () => ({
          connect: jest.fn(async () => undefined),
          callTool: jest.fn(),
          close: jest.fn(() => new Promise<void>(() => undefined)),
        }),
        createTransport: () =>
          sdkTransport(jest.fn(() => new Promise<void>(() => undefined))),
        sdkCloseGraceMs: 5,
        transportCloseTimeoutMs: 10,
      },
    );
    await start.ready;

    await expect(start.close()).rejects.toThrow(
      'Unity CLI transport teardown timed out after 10ms',
    );
  });

  const posixTest = process.platform === 'win32' ? test.skip : test;
  posixTest(
    'awaits transport-owned cleanup of a real stubborn child close event',
    async () => {
      const child = spawn(
        process.execPath,
        [
          '-e',
          "process.on('SIGTERM', () => {}); process.stdout.write('ready\\n'); setInterval(() => {}, 1000);",
        ],
        { stdio: ['ignore', 'pipe', 'ignore'] },
      );
      await once(child, 'spawn');
      await once(child.stdout!, 'data');
      const childClosed = once(child, 'close');
      const transportClose = jest.fn(async () => {
        child.kill('SIGTERM');
        const force = setTimeout(() => child.kill('SIGKILL'), 50);
        try {
          await childClosed;
        } finally {
          clearTimeout(force);
        }
      });
      const client = {
        connect: jest.fn(async () => undefined),
        callTool: jest.fn(),
        close: jest.fn(() => new Promise<void>(() => undefined)),
      };
      const start = createOfficialUnitySessionStart(
        { cliPath: '/unused/unity', projectPath: '/projects/game' },
        {
          createClient: () => client,
          createTransport: () => sdkTransport(transportClose),
          sdkCloseGraceMs: 10,
          transportCloseTimeoutMs: 1000,
        },
      );
      await start.ready;
      try {
        const closeStartedAt = Date.now();
        await expect(
          completesWithin(start.close().then(() => 'closed'), 1500),
        ).resolves.toBe('closed');
        expect(Date.now() - closeStartedAt).toBeGreaterThanOrEqual(40);
        expect(transportClose).toHaveBeenCalledTimes(1);
        expect(child.exitCode !== null || child.signalCode !== null).toBe(true);
      } finally {
        await start.close();
        if (child.exitCode === null && child.signalCode === null) {
          child.kill('SIGKILL');
          await childClosed;
        }
      }
    },
    3000,
  );
});
