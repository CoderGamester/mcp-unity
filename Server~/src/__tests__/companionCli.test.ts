import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { jest } from '@jest/globals';
import {
  CLI_DOCUMENTATION_URL,
  checkUnityCli,
  parseCompanionArguments,
  resolveUnityCliPath,
  runUnityCliVersion,
} from '../cli/companionCli.js';

describe('companion arguments', () => {
  const projectPath = path.resolve('/tmp', 'unity-project');

  test('requires an absolute existing project path', () => {
    expect(() => parseCompanionArguments([], () => true)).toThrow('--project-path');
    expect(() => parseCompanionArguments(['--project-path', 'relative'], () => true)).toThrow('absolute');
    expect(() => parseCompanionArguments(['--project-path', projectPath], () => false)).toThrow('existing Unity project');
  });

  test('accepts the required project path and optional CLI path', () => {
    expect(
      parseCompanionArguments(
        ['--project-path', projectPath, '--unity-cli-path', '/opt/unity-cli'],
        () => true,
      ),
    ).toEqual({
      projectPath,
      unityCliPath: '/opt/unity-cli',
    });
  });

  test('requires an explicitly supplied CLI path to be absolute', () => {
    expect(() =>
      parseCompanionArguments(
        ['--project-path', projectPath, '--unity-cli-path', 'relative/unity'],
        () => true,
      ),
    ).toThrow('--unity-cli-path must be absolute');
  });

  test('rejects unknown, duplicate, or valueless arguments', () => {
    expect(() => parseCompanionArguments(['--wat'], () => true)).toThrow('Unknown argument');
    expect(() =>
      parseCompanionArguments(
        ['--project-path', projectPath, '--project-path', projectPath],
        () => true,
      ),
    ).toThrow('Duplicate');
    expect(() =>
      parseCompanionArguments(['--project-path', projectPath, '--unity-cli-path'], () => true),
    ).toThrow('requires a value');
  });
});

describe('Unity CLI resolution and compatibility', () => {
  test('resolves explicit argument, then environment, then PATH command', () => {
    expect(resolveUnityCliPath('/explicit/unity', { UNITY_CLI_PATH: '/env/unity' })).toBe(
      '/explicit/unity',
    );
    expect(resolveUnityCliPath(undefined, { UNITY_CLI_PATH: '/env/unity' })).toBe('/env/unity');
    expect(resolveUnityCliPath(undefined, {})).toBe('unity');
  });

  test('trims a configured environment path and ignores empty values', () => {
    expect(resolveUnityCliPath(undefined, { UNITY_CLI_PATH: '  /env/unity  ' })).toBe(
      '/env/unity',
    );
    expect(resolveUnityCliPath(undefined, { UNITY_CLI_PATH: '   ' })).toBe('unity');
  });

  test.each([
    ['Unity CLI 1.0.0-beta.2', '1.0.0-beta.2'],
    ['unity version 1.0.0', '1.0.0'],
    ['2.4.1', '2.4.1'],
    ['Unity CLI 1.0.0-beta.2.alpha+build.01.sha-abc', '1.0.0-beta.2.alpha+build.01.sha-abc'],
    ['999999999999999999999999.0.0-dev.1', '999999999999999999999999.0.0-dev.1'],
  ])('accepts compatible version output %s', async (stdout, version) => {
    const result = await checkUnityCli('/opt/unity', async (command, args) => {
      expect(command).toBe('/opt/unity');
      expect(args).toEqual(['--version']);
      return { stdout, stderr: '' };
    });

    expect(result.version).toBe(version);
    expect(result.warning).toBe(
      version.startsWith('2.')
        ? 'Unity CLI 2.4.1 is newer than the tested major version 1.'
        : version.startsWith('999')
          ? `Unity CLI ${version} is newer than the tested major version 1.`
          : undefined,
    );
  });

  test.each(['0.9.9', '1.0.0-alpha.9', '1.0.0-beta.1'])(
    'rejects incompatible version %s with documentation',
    async (version) => {
      await expect(
        checkUnityCli('unity', async () => ({ stdout: version, stderr: '' })),
      ).rejects.toThrow(CLI_DOCUMENTATION_URL);
    },
  );

  test.each([
    'not a version',
    '1.0',
    '01.0.0',
    '1.00.0',
    '1.0.00',
    '1.0.0-beta.01',
    '1.0.0-',
    '1.0.0-beta..2',
    '1.0.0+',
    '1.0.0+build..sha',
    '1.0.0+bad_meta',
    '1.0.0-beta.2..garbage',
  ])(
    'rejects malformed version output %s',
    async (version) => {
      await expect(
        checkUnityCli('unity', async () => ({ stdout: version, stderr: '' })),
      ).rejects.toThrow(CLI_DOCUMENTATION_URL);
    },
  );

  test('turns a missing executable into an actionable error', async () => {
    const missing = Object.assign(new Error('spawn unity ENOENT'), { code: 'ENOENT' });

    await expect(
      checkUnityCli('unity', async () => {
        throw missing;
      }),
    ).rejects.toThrow(CLI_DOCUMENTATION_URL);
  });

  test('does not truncate a valid longer prerelease to the minimum prefix', async () => {
    const result = await checkUnityCli('unity', async () => ({
      stdout: '1.0.0-beta.2foo',
      stderr: '',
    }));

    expect(result.version).toBe('1.0.0-beta.2foo');
  });

  test('invokes the real version process without a shell and with only --version', async () => {
    const calls: Array<Record<string, unknown>> = [];
    const fakeChild = {
      stdout: null,
      stderr: null,
      pid: 123,
      once(event: string, listener: (...args: unknown[]) => void) {
        if (event === 'close') queueMicrotask(() => listener(0, null));
        return this;
      },
      kill: jest.fn(),
    };

    await runUnityCliVersion(
      '/opt/unity',
      ['--version'],
      { timeoutMs: 100 },
      ((command: string, args: readonly string[], options: Record<string, unknown>) => {
        calls.push({ command, args, options });
        return fakeChild;
      }) as never,
    );

    expect(calls).toEqual([
      {
        command: '/opt/unity',
        args: ['--version'],
        options: expect.objectContaining({ shell: false }),
      },
    ]);
  });

  test('observes cancellation that occurs while the version process is spawning', async () => {
    const controller = new AbortController();
    const fakeChild = {
      stdout: null,
      stderr: null,
      pid: 123,
      once() {
        return this;
      },
      kill: jest.fn(),
    };

    await expect(
      Promise.race([
        runUnityCliVersion(
          '/opt/unity',
          ['--version'],
          { timeoutMs: 1000, signal: controller.signal },
          (() => {
            controller.abort();
            return fakeChild;
          }) as never,
        ),
        new Promise((_, reject) =>
          setTimeout(
            () => reject(new Error('cancelled version invocation did not settle')),
            100,
          ),
        ),
      ]),
    ).rejects.toThrow('cancelled');
    expect(fakeChild.kill).toHaveBeenCalled();
  });

  const posixTest = process.platform === 'win32' ? test.skip : test;
  posixTest(
    'times out promptly when a descendant retains the version process stdio',
    async () => {
      const fixtureDirectory = fs.mkdtempSync(
        path.join(os.tmpdir(), 'mcp-unity-cli-version-'),
      );
      const executable = path.join(fixtureDirectory, 'unity-version-fixture');
      fs.writeFileSync(
        executable,
        `#!/usr/bin/env node
const { spawn } = require('node:child_process');
spawn(process.execPath, ['-e', 'setTimeout(() => {}, 1500)'], {
  stdio: ['ignore', process.stdout, process.stderr]
});
process.stdout.write('1.0.0-beta.2\\n');
`,
        { mode: 0o755 },
      );

      const startedAt = Date.now();
      try {
        await expect(
          Promise.race([
            runUnityCliVersion(executable, ['--version'], { timeoutMs: 100 }),
            new Promise((_, reject) =>
              setTimeout(
                () => reject(new Error('version invocation did not settle')),
                1000,
              ),
            ),
          ]),
        ).rejects.toThrow('timed out');
        expect(Date.now() - startedAt).toBeLessThan(1000);
      } finally {
        fs.rmSync(fixtureDirectory, { recursive: true, force: true });
      }
    },
    3000,
  );
});
