import { jest } from '@jest/globals';
import {
  OfficialUnityMcpClient,
  type OfficialUnitySession,
  type OfficialUnitySessionFactory,
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
): OfficialUnitySession & { close: jest.Mock } {
  return {
    callTool,
    close: jest.fn(async () => undefined),
  };
}

describe('OfficialUnityMcpClient', () => {
  test('lazily starts exactly one official MCP process for concurrent first reads', async () => {
    const gate = deferred<OfficialUnitySession>();
    const factory: jest.MockedFunction<OfficialUnitySessionFactory> = jest.fn(() => gate.promise);
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
    const factory = jest
      .fn<ReturnType<OfficialUnitySessionFactory>, Parameters<OfficialUnitySessionFactory>>()
      .mockResolvedValueOnce(firstSession)
      .mockResolvedValueOnce(secondSession);
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
    expect(firstSession.close).toHaveBeenCalledTimes(1);
  });

  test('does not retry a second failed read', async () => {
    const factory = jest
      .fn<ReturnType<OfficialUnitySessionFactory>, Parameters<OfficialUnitySessionFactory>>()
      .mockResolvedValue(session(async () => {
        throw new Error('Connection closed');
      }));
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
    const factory = jest.fn(async () => activeSession);
    const client = new OfficialUnityMcpClient({
      cliPath: 'unity',
      projectPath: '/projects/game',
      sessionFactory: factory,
    });

    await expect(client.readTool('inspect_gameobject', {})).rejects.toThrow(
      'Method not found',
    );
    expect(factory).toHaveBeenCalledTimes(1);
    expect(activeSession.close).not.toHaveBeenCalled();
  });

  test('closes the active child/client and prevents later startup', async () => {
    const activeSession = session();
    const factory = jest.fn(async () => activeSession);
    const client = new OfficialUnityMcpClient({
      cliPath: 'unity',
      projectPath: '/projects/game',
      sessionFactory: factory,
    });
    await client.readTool('get_console_logs', {});

    await client.close();
    await client.close();

    expect(activeSession.close).toHaveBeenCalledTimes(1);
    expect(client.state).toBe('closed');
    await expect(client.readTool('get_console_logs', {})).rejects.toThrow('closed');
    expect(factory).toHaveBeenCalledTimes(1);
  });
});
