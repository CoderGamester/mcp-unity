import path from 'node:path';
import {
  CLI_DOCUMENTATION_URL,
  checkUnityCli,
  parseCompanionArguments,
  resolveUnityCliPath,
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

  test.each([
    ['Unity CLI 1.0.0-beta.2', '1.0.0-beta.2'],
    ['unity version 1.0.0', '1.0.0'],
    ['2.4.1', '2.4.1'],
  ])('accepts compatible version output %s', async (stdout, version) => {
    const result = await checkUnityCli('/opt/unity', async (command, args) => {
      expect(command).toBe('/opt/unity');
      expect(args).toEqual(['--version']);
      return { stdout, stderr: '' };
    });

    expect(result.version).toBe(version);
    expect(result.warning).toBe(version.startsWith('2.') ? 'Unity CLI 2.4.1 is newer than the tested major version 1.' : undefined);
  });

  test.each(['0.9.9', '1.0.0-alpha.9', '1.0.0-beta.1'])(
    'rejects incompatible version %s with documentation',
    async (version) => {
      await expect(
        checkUnityCli('unity', async () => ({ stdout: version, stderr: '' })),
      ).rejects.toThrow(CLI_DOCUMENTATION_URL);
    },
  );

  test.each(['not a version', '1.0', '1.0.0-beta'])(
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
});
