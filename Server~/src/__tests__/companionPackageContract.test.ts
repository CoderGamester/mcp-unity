import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { execFileSync, spawnSync } from 'node:child_process';
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
      'scripts/clean-build.mjs',
      'scripts/copy-ui.mjs',
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

  test('ships a self-contained Node 20 bundle with notices', () => {
    const entrypoint = fs.readFileSync(
      path.join(serverRoot, 'build', 'index.js'),
      'utf8',
    );
    const bareImports = [
      ...entrypoint.matchAll(
        /^import(?:[\s\S]*?\sfrom\s+|\s*)['"]([^./][^'"]*)['"];?$/gm,
      ),
    ].map((match) => match[1]);

    expect(bareImports.every((specifier) => specifier.startsWith('node:'))).toBe(
      true,
    );
    expect(entrypoint).toContain('MCP Unity Companion could not start');
    expect(
      fs.readFileSync(path.join(serverRoot, 'THIRD_PARTY_NOTICES.md'), 'utf8'),
    ).toEqual(expect.stringContaining('@modelcontextprotocol/sdk 1.26.0'));
  });

  test('starts from a clean copied package with no reachable node_modules', () => {
    const isolatedRoot = fs.mkdtempSync(
      path.join(os.tmpdir(), 'mcp-unity-companion-clean-'),
    );
    const packageRoot = path.join(isolatedRoot, 'package');
    try {
      fs.mkdirSync(packageRoot);
      fs.cpSync(path.join(serverRoot, 'build'), path.join(packageRoot, 'build'), {
        recursive: true,
      });
      for (const file of ['package.json', 'THIRD_PARTY_NOTICES.md']) {
        fs.copyFileSync(path.join(serverRoot, file), path.join(packageRoot, file));
      }
      const result = spawnSync(
        process.execPath,
        [path.join(packageRoot, 'build', 'index.js')],
        {
          cwd: packageRoot,
          encoding: 'utf8',
          env: { PATH: process.env.PATH ?? '' },
          timeout: 10_000,
        },
      );

      expect(result.status).toBe(1);
      expect(result.stderr).toContain('--project-path');
      expect(result.stderr).not.toContain('ERR_MODULE_NOT_FOUND');
    } finally {
      fs.rmSync(isolatedRoot, { recursive: true, force: true });
    }
  });

  test('tracked build is reproducible from source', () => {
    expect(() =>
      execFileSync(process.execPath, ['scripts/build-bundle.mjs', '--check'], {
        cwd: serverRoot,
        stdio: 'pipe',
      }),
    ).not.toThrow();
  });
});

function walkFiles(directory: string): string[] {
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const resolved = path.join(directory, entry.name);
    return entry.isDirectory() ? walkFiles(resolved) : [resolved];
  });
}
