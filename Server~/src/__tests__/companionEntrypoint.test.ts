import { EventEmitter } from 'node:events';
import { jest } from '@jest/globals';
import { InMemoryTransport } from '@modelcontextprotocol/sdk/inMemory.js';
import { startCompanion } from '../companionEntrypoint.js';

describe('companion entrypoint', () => {
  test('checks the resolved CLI before serving and reports newer-major warnings', async () => {
    const checkCli = jest.fn(async (command: string) => ({
      command,
      version: '2.0.0',
      warning: 'Unity CLI 2.0.0 is newer than the tested major version 1.',
    }));
    const stderr = { write: jest.fn(() => true) };
    const projectPath = '/projects/game';
    const runtime = await startCompanion({
      argv: ['--project-path', projectPath],
      environment: { UNITY_CLI_PATH: '/env/unity' },
      isUnityProject: () => true,
      checkCli,
      transport: new InMemoryTransport(),
      signals: new EventEmitter(),
      stdin: new EventEmitter(),
      stderr,
    });

    expect(checkCli).toHaveBeenCalledWith('/env/unity');
    expect(stderr.write).toHaveBeenCalledWith(
      'Warning: Unity CLI 2.0.0 is newer than the tested major version 1.\n',
    );
    expect(runtime.officialClient.state).toBe('disconnected');

    await runtime.shutdown();
    expect(runtime.officialClient.state).toBe('closed');
  });
});
