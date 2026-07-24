import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const serverRoot = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  '..',
  '..',
);
const packageJson = JSON.parse(
  fs.readFileSync(path.join(serverRoot, 'package.json'), 'utf8'),
) as Record<string, unknown>;

describe('private companion package contract', () => {
  test('uses exact private metadata and runtime dependencies', () => {
    expect(packageJson).toMatchObject({
      name: 'mcp-unity-companion',
      version: '2.0.0',
      private: true,
      engines: { node: '>=20' },
      dependencies: {
        '@modelcontextprotocol/sdk': '1.26.0',
        '@modelcontextprotocol/ext-apps': '1.0.1',
        zod: '3.25.76',
      },
    });
    expect(Object.keys(packageJson.dependencies as object).sort()).toEqual([
      '@modelcontextprotocol/ext-apps',
      '@modelcontextprotocol/sdk',
      'zod',
    ]);
    expect(packageJson).not.toHaveProperty('bin');
    expect(packageJson).not.toHaveProperty('files');
    expect(packageJson).not.toHaveProperty('publishConfig');
    expect(packageJson).not.toHaveProperty('mcpName');
  });

  test('contains no legacy transport, mutation proxy, Docker, or Smithery source', () => {
    for (const obsoletePath of [
      'Dockerfile',
      '.dockerignore',
      'smithery.yaml',
      'src/tools',
      'src/unity/mcpUnity.ts',
      'src/unity/unityConnection.ts',
      'src/unity/commandQueue.ts',
    ]) {
      expect(fs.existsSync(path.join(serverRoot, obsoletePath))).toBe(false);
    }

    const source = walkFiles(path.join(serverRoot, 'src'))
      .filter(
        (file) =>
          file.endsWith('.ts') && !file.includes(`${path.sep}__tests__${path.sep}`),
      )
      .map((file) => fs.readFileSync(file, 'utf8'))
      .join('\n');
    for (const forbidden of [
      'WebSocket',
      'ws://',
      'localhost:8090',
      'set_play_mode_status',
      'update_gameobject',
      'add_package',
    ]) {
      expect(source).not.toContain(forbidden);
    }
  });

  test('ships a companion-only build for Git/UPM installations', () => {
    expect(fs.existsSync(path.join(serverRoot, 'build', 'index.js'))).toBe(true);
    expect(
      fs.existsSync(path.join(serverRoot, 'build', 'ui', 'unity-dashboard.html')),
    ).toBe(true);
    for (const obsoleteBuildPath of [
      'build/tools',
      'build/unity/mcpUnity.js',
      'build/unity/unityConnection.js',
      'build/unity/commandQueue.js',
    ]) {
      expect(fs.existsSync(path.join(serverRoot, obsoleteBuildPath))).toBe(false);
    }
  });
});

function walkFiles(directory: string): string[] {
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const resolved = path.join(directory, entry.name);
    return entry.isDirectory() ? walkFiles(resolved) : [resolved];
  });
}
