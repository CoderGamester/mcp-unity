import { jest } from '@jest/globals';
import {
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

  test('close returns promptly before connect resolves and late readiness is owned once', async () => {
    const gate = deferred<OfficialUnitySession>();
    const start = sessionStart(gate.promise);
    const factory = jest.fn(() => start);
    const client = new OfficialUnityMcpClient({
      cliPath: 'unity',
      projectPath: '/projects/game',
      sessionFactory: factory,
    });
    const read = client.readTool('get_console_logs', {});

    await expect(
      Promise.race([
        client.close().then(() => 'closed'),
        new Promise((resolve) => setTimeout(() => resolve('timed-out'), 100)),
      ]),
    ).resolves.toBe('closed');
    await expect(read).rejects.toThrow('closed');
    expect(start.close).toHaveBeenCalledTimes(1);

    gate.resolve(session());
    await new Promise((resolve) => setImmediate(resolve));
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
});
