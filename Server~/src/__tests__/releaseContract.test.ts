import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const serverRoot = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  '..',
  '..',
);
const repositoryRoot = path.resolve(serverRoot, '..');
const legacySnapshotPath = path.join(
  serverRoot,
  'src',
  '__tests__',
  'fixtures',
  'legacy-1.4.0-inventory.json',
);
const legacySnapshotSha256 =
  'd6c1c9aa87f69febf8034e290e57d60f668f8abe9b540a02a27ac375fbaa1227';

const readRepositoryFile = (relativePath: string): string =>
  fs.readFileSync(path.join(repositoryRoot, relativePath), 'utf8');

const rootPackage = JSON.parse(readRepositoryFile('package.json')) as Record<
  string,
  unknown
>;
const companionPackage = JSON.parse(
  readRepositoryFile('Server~/package.json'),
) as Record<string, unknown>;
const readme = readRepositoryFile('README.md');
const agents = readRepositoryFile('AGENTS.md');

const legacyTools = [
  'add_asset_to_scene',
  'add_package',
  'assign_material',
  'batch_execute',
  'create_material',
  'create_prefab',
  'create_scene',
  'delete_gameobject',
  'delete_scene',
  'duplicate_gameobject',
  'execute_menu_item',
  'get_console_logs',
  'get_gameobject',
  'get_material_info',
  'get_play_mode_status',
  'get_scene_info',
  'get_scenes_hierarchy',
  'load_scene',
  'modify_material',
  'move_gameobject',
  'recompile_scripts',
  'reparent_gameobject',
  'rotate_gameobject',
  'run_tests',
  'save_scene',
  'scale_gameobject',
  'select_gameobject',
  'send_console_log',
  'set_play_mode_status',
  'set_transform',
  'show_unity_dashboard',
  'unload_scene',
  'update_component',
  'update_gameobject',
] as const;

const legacyResources = [
  'get_assets',
  'get_console_logs',
  'get_gameobject',
  'get_menu_items',
  'get_packages',
  'get_scenes_hierarchy',
  'get_tests',
  'unity_dashboard_app',
  'unity_dashboard_app_legacy',
] as const;

const legacyUris = [
  'ui://unity-dashboard',
  'unity://assets',
  'unity://gameobject/{idOrName}',
  'unity://logs/{logType}?offset={offset}&limit={limit}&includeStackTrace={includeStackTrace}',
  'unity://menu-items',
  'unity://packages',
  'unity://scenes_hierarchy',
  'unity://tests/{testMode}',
  'unity://ui/dashboard',
] as const;

const legacyPrompts = [
  'gameobject_handling_strategy',
  'unity_dashboard',
] as const;

const legacySettings = [
  'Port',
  'RequestTimeoutSeconds',
  'AutoStartServer',
  'EnableInfoLogs',
  'NpmExecutablePath',
  'AllowRemoteConnections',
] as const;

const legacyConcepts = [
  'env:UNITY_HOST',
  'env:LOGGING',
  'env:LOGGING_FILE',
  'path:ProjectSettings/McpUnitySettings.json',
  'integration:Unity-driven npm install/build',
  'integration:automatic MCP-client configuration',
  'integration:PackedCache mutation',
  'integration:custom WebSocket endpoint/port',
  'integration:Docker deployment/Dockerfile/exposed ports',
  'integration:Smithery configuration',
  'integration:Node npm executable/bin/publication surface',
  'integration:MCP registry server.json',
  'integration:MCP registry mcpName/mcpname',
  'integration:Glama registry metadata',
] as const;

const legacyEvidenceCounts: Record<(typeof legacyConcepts)[number], number> = {
  'env:UNITY_HOST': 1,
  'env:LOGGING': 1,
  'env:LOGGING_FILE': 1,
  'path:ProjectSettings/McpUnitySettings.json': 1,
  'integration:Unity-driven npm install/build': 2,
  'integration:automatic MCP-client configuration': 2,
  'integration:PackedCache mutation': 2,
  'integration:custom WebSocket endpoint/port': 2,
  'integration:Docker deployment/Dockerfile/exposed ports': 2,
  'integration:Smithery configuration': 1,
  'integration:Node npm executable/bin/publication surface': 3,
  'integration:MCP registry server.json': 1,
  'integration:MCP registry mcpName/mcpname': 2,
  'integration:Glama registry metadata': 1,
};

const extensionCommands = [
  'assign_material',
  'duplicate_gameobject',
  'editor_step',
  'inspect_gameobject',
  'unload_scene',
] as const;

const companionResources = [
  'unity://logs{?severity,limit}',
  'unity://scenes-hierarchy{?path,max_nodes}',
  'unity://gameobject/{target}',
  'unity://packages{?include_indirect}',
  'unity://tests/{mode}',
  'ui://unity-dashboard',
] as const;

const companionTools = ['show_unity_dashboard'] as const;
const companionPrompts = [
  'gameobject_handling_strategy',
  'unity_dashboard',
] as const;

interface LegacyEvidence {
  path: string;
  contains?: string;
  absent?: boolean;
}

interface LegacySnapshot {
  schemaVersion: number;
  sourceTag: string;
  sourceCommit: string;
  tools: string[];
  resources: string[];
  uris: string[];
  prompts: string[];
  settings: string[];
  integrations: Array<{
    id: string;
    evidence: LegacyEvidence[];
  }>;
}

describe('2.0 release contract', () => {
  test('keeps release metadata synchronized and private', () => {
    expect(rootPackage).toMatchObject({
      version: '2.0.0',
      unity: '6000.0',
      dependencies: {
        'com.unity.pipeline': '0.3.1-exp.1',
        'com.unity.test-framework': '1.3.3',
      },
    });
    expect(companionPackage).toMatchObject({
      name: 'mcp-unity-companion',
      version: '2.0.0',
      private: true,
    });
    for (const property of ['mcpName', 'mcpname']) {
      expect(rootPackage).not.toHaveProperty(property);
      expect(companionPackage).not.toHaveProperty(property);
    }
    for (const property of ['bin', 'files', 'publishConfig']) {
      expect(companionPackage).not.toHaveProperty(property);
    }
    expect(fs.existsSync(path.join(repositoryRoot, 'server.json'))).toBe(false);
    expect(fs.existsSync(path.join(repositoryRoot, 'glama.json'))).toBe(false);

    for (const relativePath of [
      'Server~/package-lock.json',
      'Server~/src/companionServer.ts',
      'Server~/src/unity/officialUnityMcpClient.ts',
      'Server~/src/ui/unity-dashboard.html',
    ]) {
      expect(readRepositoryFile(relativePath)).toContain('2.0.0');
    }
  });

  test('ships an immutable 1.4.0 snapshot and validates it when the tag is available', () => {
    expect(fs.existsSync(legacySnapshotPath)).toBe(true);
    if (!fs.existsSync(legacySnapshotPath)) return;

    const snapshotBytes = fs.readFileSync(legacySnapshotPath);
    expect(createHash('sha256').update(snapshotBytes).digest('hex')).toBe(
      legacySnapshotSha256,
    );
    const snapshot = JSON.parse(snapshotBytes.toString('utf8')) as LegacySnapshot;
    expect(snapshot).toMatchObject({
      schemaVersion: 1,
      sourceTag: '1.4.0',
      sourceCommit: 'bbfb1c0681519ced5b357ce7cc3c1ee68c9dc64e',
    });
    expect(snapshot.tools.sort()).toEqual([...legacyTools].sort());
    expect(snapshot.resources.sort()).toEqual([...legacyResources].sort());
    expect(snapshot.uris.sort()).toEqual([...legacyUris].sort());
    expect(snapshot.prompts.sort()).toEqual([...legacyPrompts].sort());
    expect(snapshot.settings.sort()).toEqual([...legacySettings].sort());
    expect(snapshot.integrations.map(({ id }) => id).sort()).toEqual(
      [...legacyConcepts].sort(),
    );
    for (const integration of snapshot.integrations) {
      expect(integration.evidence).toHaveLength(
        legacyEvidenceCounts[
          integration.id as (typeof legacyConcepts)[number]
        ],
      );
      for (const evidence of integration.evidence) {
        expect(evidence.path).toEqual(expect.any(String));
        expect(evidence.path.length).toBeGreaterThan(0);
        const evidenceKeys = Object.keys(evidence).sort();
        if (evidence.absent !== undefined) {
          expect(evidenceKeys).toEqual(['absent', 'path']);
          expect(evidence.absent).toBe(true);
        } else {
          expect(evidenceKeys).toEqual(['contains', 'path']);
          expect(evidence.contains).toEqual(expect.any(String));
          expect(evidence.contains?.length).toBeGreaterThan(0);
        }
      }
    }

    // Shallow clones and packaged UPM copies may not contain the tag object.
    // In those environments the checked-in snapshot above remains mandatory
    // and continues to drive every migration-table assertion.
    if (!gitObjectExists('1.4.0^{commit}')) return;

    expect(git('rev-parse', '1.4.0^{commit}').trim()).toBe(
      snapshot.sourceCommit,
    );
    const files = registeredCatalogModules(
      git('show', '1.4.0:Server~/src/index.ts'),
    );
    const inventory = {
      tools: new Set<string>(),
      resources: new Set<string>(),
      uris: new Set<string>(),
      prompts: new Set<string>(),
    };

    for (const file of files) {
      const source = git('show', `1.4.0:${file}`);
      if (file.includes('/tools/')) {
        collectMatches(
          inventory.tools,
          source,
          /const\s+\w*(?:toolName|ToolName)\s*=\s*['"]([a-z0-9_]+)['"]/g,
        );
      } else if (file.includes('/resources/')) {
        collectMatches(
          inventory.resources,
          source,
          /const\s+(?:resourceName|legacyResourceName)\s*=\s*['"]([a-z0-9_]+)['"]/g,
        );
        collectMatches(
          inventory.uris,
          source,
          /const\s+\w*(?:Uri|URI)\s*=\s*['"]((?:unity|ui):\/\/[^'"]+)['"]/g,
        );
      } else if (file.includes('/prompts/')) {
        collectMatches(
          inventory.prompts,
          source,
          /server\.prompt\(\s*['"]([a-z0-9_]+)['"]/g,
        );
      }
    }

    const settingsSource = git(
      'show',
      '1.4.0:Editor/UnityBridge/McpUnitySettings.cs',
    );
    const discoveredSettings = new Set<string>();
    collectMatches(
      discoveredSettings,
      settingsSource,
      /public\s+(?:int|bool|string)\s+([A-Za-z0-9_]+)\s*=/g,
    );

    expect([...inventory.tools].sort()).toEqual([...legacyTools].sort());
    expect([...inventory.resources].sort()).toEqual([...legacyResources].sort());
    expect([...inventory.uris].sort()).toEqual([...legacyUris].sort());
    expect([...inventory.prompts].sort()).toEqual([...legacyPrompts].sort());
    expect([...discoveredSettings].sort()).toEqual([...legacySettings].sort());

    for (const integration of snapshot.integrations) {
      for (const evidence of integration.evidence) {
        const exists = gitObjectExists(`1.4.0:${evidence.path}`);
        expect(exists).toBe(!evidence.absent);
        if (!evidence.absent && evidence.contains) {
          expect(git('show', `1.4.0:${evidence.path}`)).toContain(
            evidence.contains,
          );
        }
      }
    }
  });

  test('maps every 1.4.0 catalog and configuration concept', () => {
    for (const tool of legacyTools) expectMigrationRow(`tool:${tool}`);
    for (const resource of legacyResources) {
      expectMigrationRow(`resource:${resource}`);
    }
    for (const uri of legacyUris) expectMigrationRow(`uri:${uri}`);
    for (const prompt of legacyPrompts) expectMigrationRow(`prompt:${prompt}`);
    for (const setting of legacySettings) {
      expectMigrationRow(`config:${setting}`);
    }
    for (const concept of legacyConcepts) {
      expectMigrationRow(`concept:${concept}`);
    }
  });

  test('advertises only the 2.0 extension and companion catalogs', () => {
    const extensionSection = markdownSection(
      readme,
      '## MCP Unity extension commands',
      '## Optional MCP companion',
    );
    const companionSection = markdownSection(
      readme,
      '## Optional MCP companion',
      '## Migration from 1.4.0',
    );

    expect(markdownDashCatalog(extensionSection)).toEqual(
      sorted(extensionCommands),
    );
    expect(markdownLabeledCatalog(companionSection, 'Tool')).toEqual(
      sorted(companionTools),
    );
    expect(markdownNestedCatalog(companionSection, 'Resources')).toEqual(
      sorted(companionResources),
    );
    expect(markdownNestedCatalog(companionSection, 'Prompts')).toEqual(
      sorted(companionPrompts),
    );
  });

  test('keeps runtime and documented public catalogs exact', () => {
    const commandNames = new Set<string>();
    for (const file of walkFiles(path.join(repositoryRoot, 'Editor', 'Commands'))) {
      if (!file.endsWith('.cs')) continue;
      collectMatches(
        commandNames,
        fs.readFileSync(file, 'utf8'),
        /\[CliCommand\(\s*"([a-z0-9_]+)"/g,
      );
    }
    expect(sorted(commandNames)).toEqual(sorted(extensionCommands));

    const companionSource = readRepositoryFile('Server~/src/companionServer.ts');
    const dashboardSource = readRepositoryFile(
      'Server~/src/resources/dashboardResource.ts',
    );
    const promptSource = readRepositoryFile(
      'Server~/src/prompts/companionPrompts.ts',
    );
    const runtimeTools = new Set<string>();
    const runtimeResources = new Set<string>();
    const runtimePrompts = new Set<string>();
    collectMatches(
      runtimeTools,
      companionSource,
      /registerAppTool\(\s*server,\s*'([^']+)'/g,
    );
    collectMatches(
      runtimeResources,
      companionSource,
      /template:\s*'((?:unity|ui):\/\/[^']+)'/g,
    );
    collectMatches(
      runtimeResources,
      dashboardSource,
      /DASHBOARD_URI\s*=\s*'((?:unity|ui):\/\/[^']+)'/g,
    );
    collectMatches(
      runtimePrompts,
      promptSource,
      /server\.registerPrompt\(\s*'([^']+)'/g,
    );

    expect(sorted(runtimeTools)).toEqual(sorted(companionTools));
    expect(sorted(runtimeResources)).toEqual(sorted(companionResources));
    expect(sorted(runtimePrompts)).toEqual(sorted(companionPrompts));

    const agentsCatalog = markdownSection(
      agents,
      '## Public catalogs',
      '## Adding or changing an extension command',
    );
    const [agentsExtensions, agentsCompanion = ''] = agentsCatalog.split(
      'The optional companion exposes only:',
    );
    expect(markdownPlainCatalog(agentsExtensions)).toEqual(
      sorted(extensionCommands),
    );
    expect(markdownLowercaseLabeledCatalog(agentsCompanion, 'tool')).toEqual(
      sorted(companionTools),
    );
    expect(markdownNestedCatalog(agentsCompanion, 'resources')).toEqual(
      sorted(companionResources),
    );
    expect(markdownNestedCatalog(agentsCompanion, 'prompts')).toEqual(
      sorted(companionPrompts),
    );
  });

  test('keeps README and AGENTS aligned on pins and architecture', () => {
    for (const document of [readme, agents]) {
      expect(document).toContain('2.0.0');
      expect(document).toContain('com.unity.pipeline');
      expect(document).toContain('0.3.1-exp.1');
      expect(document).toContain('Unity CLI 1.0.0-beta.2');
      expect(document).toContain('Unity 6000.0');
      expect(document).toContain('Unity 6000.3');
      expect(document).toContain('Unity 6000.5');
      expect(document).toContain('Window > MCP Unity > Setup');
      expect(document).toContain('unity mcp --project-path');
      expect(document).toContain('npm audit --omit=dev');
    }
    expect(readme).toContain(
      'current project path and the resolved Pipeline version and compatibility state',
    );
    expect(readme).toContain(
      'uses the package resolver path internally when it generates companion configuration',
    );
    expect(readme).not.toContain('shows the project and resolved Pipeline paths');
    expect(readme).not.toContain('package path shown by');

    const primaryConfiguration = markdownSection(
      readme,
      '## Configure the primary MCP server',
      '## MCP Unity extension commands',
    );
    expect(primaryConfiguration).not.toContain('UNITY_CLI_PATH');
    expect(primaryConfiguration).toContain('absolute executable path');
    expect(primaryConfiguration).toContain('`PATH`');
  });

  test('keeps every AI guidance file anchored to the 2.0 maintainer guide', () => {
    const guidanceFiles = walkFiles(repositoryRoot)
      .map((file) => path.relative(repositoryRoot, file))
      .filter(isAiGuidanceFile);

    expect(guidanceFiles).toEqual(
      expect.arrayContaining(['.windsurfrules', 'AGENTS.md', 'CLAUDE.md', 'llms.txt']),
    );
    for (const relativePath of guidanceFiles) {
      if (relativePath === 'AGENTS.md') continue;
      const content = readRepositoryFile(relativePath);
      expect(content).toContain('AGENTS.md');
      for (const staleMarker of [
        'McpUnityServer.cs',
        'McpToolBase',
        'McpUnitySettings.json',
        'websocket-sharp',
        'WebSocket Bridge',
        'default 8090',
      ]) {
        expect(content).not.toContain(staleMarker);
      }
    }
  });

  test('prevents the legacy bridge and publication surface from returning', () => {
    const forbiddenPaths = [
      'server.json',
      'glama.json',
      'Editor/UnityBridge',
      'Editor/Tools',
      'Editor/Resources',
      'Editor/Services',
      'Server~/src/tools',
      'Server~/src/unity/mcpUnity.ts',
      'Server~/src/unity/commandQueue.ts',
      'Server~/Dockerfile',
      'Server~/smithery.yaml',
    ];
    const repositoryFiles = walkFiles(repositoryRoot);
    const normalizedFiles = repositoryFiles.map((file) =>
      path.relative(repositoryRoot, file).split(path.sep).join('/').toLowerCase(),
    );
    for (const relativePath of forbiddenPaths) {
      const forbidden = relativePath.toLowerCase();
      expect(
        normalizedFiles.some(
          (file) => file === forbidden || file.startsWith(`${forbidden}/`),
        ),
      ).toBe(false);
    }
    expect(
      normalizedFiles.some(
        (file) =>
          file.includes('websocket-sharp') ||
          file.includes('websocketsharp'),
      ),
    ).toBe(false);

    const productionText = repositoryFiles
      .filter((file) => {
        const relative = path.relative(repositoryRoot, file);
        const normalized = relative.split(path.sep).join('/').toLowerCase();
        return (
          !normalized.includes('/__tests__/') &&
          !normalized.startsWith('docs/') &&
          !normalized.endsWith('.md') &&
          !normalized.endsWith('.meta') &&
          !normalized.includes('/build/') &&
          !normalized.includes('/node_modules/') &&
          isProductionSourceOrConfig(normalized)
        );
      })
      .map((file) => fs.readFileSync(file, 'utf8'))
      .join('\n')
      .toLowerCase();

    expect(findForbiddenLegacyMarkers(productionText)).toEqual([]);
    expect(productionText).not.toMatch(/(^|[^0-9])8090([^0-9]|$)/);
  });

  test('detects mixed-case legacy markers after normalization', () => {
    expect(
      findForbiddenLegacyMarkers(
        'WEBSOCKET-SHARP WebSocketSharp McPuNiTySeTtInGs LOCALHOST:8090 PACKEDCACHE',
      ),
    ).toEqual(
      expect.arrayContaining([
        'websocket-sharp',
        'websocketsharp',
        'mcpunitysettings',
        'localhost:8090',
        'packedcache',
      ]),
    );
  });

  test('does not trust a 1.4.0 tag owned by an unrelated consumer repository', () => {
    if (process.env.MCP_UNITY_NESTED_CONSUMER_CHECK === '1') return;

    const consumerRoot = fs.mkdtempSync(
      path.join(os.tmpdir(), 'mcp-unity-consumer-git-'),
    );
    const packageRoot = path.join(
      consumerRoot,
      'Packages',
      'com.gamelovers.mcp-unity',
    );
    try {
      gitAt(consumerRoot, 'init', '--quiet');
      fs.writeFileSync(
        path.join(consumerRoot, 'consumer.txt'),
        'unrelated consumer repository\n',
      );
      gitAt(consumerRoot, 'add', 'consumer.txt');
      gitAt(
        consumerRoot,
        '-c',
        'user.name=Contract Test',
        '-c',
        'user.email=contract-test@example.invalid',
        'commit',
        '--quiet',
        '-m',
        'consumer fixture',
      );
      gitAt(consumerRoot, 'tag', '1.4.0');
      fs.cpSync(repositoryRoot, packageRoot, {
        recursive: true,
        filter: (source) => {
          const relative = path
            .relative(repositoryRoot, source)
            .split(path.sep)
            .join('/');
          return ![
            '.git',
            'Server~/build',
            'Server~/node_modules',
          ].some(
            (excluded) =>
              relative === excluded || relative.startsWith(`${excluded}/`),
          );
        },
      });
      fs.symlinkSync(
        path.join(serverRoot, 'node_modules'),
        path.join(packageRoot, 'Server~', 'node_modules'),
        'dir',
      );

      expect(gitObjectExistsAt(packageRoot, '1.4.0^{commit}')).toBe(false);
      expect(() =>
        execFileSync(
          process.execPath,
          [
            '--experimental-vm-modules',
            path.join(serverRoot, 'node_modules', 'jest', 'bin', 'jest.js'),
            '--runInBand',
            '--config',
            path.join(packageRoot, 'Server~', 'jest.config.js'),
            path.join(
              packageRoot,
              'Server~',
              'src',
              '__tests__',
              'releaseContract.test.ts',
            ),
          ],
          {
            cwd: path.join(packageRoot, 'Server~'),
            env: {
              ...process.env,
              MCP_UNITY_NESTED_CONSUMER_CHECK: '1',
            },
            stdio: 'pipe',
          },
        ),
      ).not.toThrow();
    } finally {
      fs.rmSync(consumerRoot, { recursive: true, force: true });
    }
  });

  test('marks untranslated readmes as legacy documentation', () => {
    for (const relativePath of ['README-ja.md', 'README_zh-CN.md']) {
      const localizedReadme = readRepositoryFile(relativePath);
      expect(localizedReadme.slice(0, 1000)).toContain(
        'MCP Unity 2.0 documentation',
      );
      expect(localizedReadme.slice(0, 1000)).toContain('README.md');
    }
  });
});

function git(...args: string[]): string {
  return gitAt(repositoryRoot, ...args);
}

function gitAt(cwd: string, ...args: string[]): string {
  return execFileSync('git', args, {
    cwd,
    encoding: 'utf8',
  });
}

function gitObjectExists(objectName: string): boolean {
  return gitObjectExistsAt(repositoryRoot, objectName);
}

function gitObjectExistsAt(cwd: string, objectName: string): boolean {
  if (!ownsGitRepository(cwd)) return false;
  try {
    execFileSync('git', ['cat-file', '-e', objectName], {
      cwd,
      stdio: 'ignore',
    });
    return true;
  } catch {
    return false;
  }
}

function ownsGitRepository(packageRoot: string): boolean {
  try {
    const topLevel = execFileSync(
      'git',
      ['rev-parse', '--show-toplevel'],
      {
        cwd: packageRoot,
        encoding: 'utf8',
        stdio: ['ignore', 'pipe', 'ignore'],
      },
    ).trim();
    return fs.realpathSync(topLevel) === fs.realpathSync(packageRoot);
  } catch {
    return false;
  }
}

function findForbiddenLegacyMarkers(source: string): string[] {
  const normalizedSource = source.toLowerCase();
  return [
    'websocket-sharp',
    'websocketsharp',
    'localhost:8090',
    'mcpunitysettings',
    'com.unity.editorcoroutines',
    'com.unity.nuget.newtonsoft-json',
    'packedcache',
  ].filter((marker) => normalizedSource.includes(marker));
}

function collectMatches(
  target: Set<string>,
  source: string,
  pattern: RegExp,
): void {
  for (const match of source.matchAll(pattern)) target.add(match[1]);
}

function registeredCatalogModules(indexSource: string): string[] {
  const modules = new Set<string>();
  for (const match of indexSource.matchAll(
    /import\s+\{([^}]+)\}\s+from\s+['"]\.\/(tools|resources|prompts)\/([^'"]+)\.js['"]/g,
  )) {
    const importedNames = match[1]
      .split(',')
      .map((name) => name.trim())
      .filter(Boolean);
    const remainingIndex = indexSource.slice(match.index + match[0].length);
    for (const importedName of importedNames) {
      expect(remainingIndex).toMatch(
        new RegExp(`\\b${escapeRegExp(importedName)}\\s*\\(`),
      );
    }
    modules.add(`Server~/src/${match[2]}/${match[3]}.ts`);
  }
  return [...modules].sort();
}

function expectMigrationRow(concept: string): void {
  expect(readme).toContain(`| \`${concept}\` |`);
}

function markdownDashCatalog(markdown: string): string[] {
  return [...markdown.matchAll(/^- `([^`]+)` —/gm)]
    .map((match) => match[1])
    .sort();
}

function markdownPlainCatalog(markdown: string): string[] {
  return [...markdown.matchAll(/^- `([^`]+)`$/gm)]
    .map((match) => match[1])
    .sort();
}

function markdownLabeledCatalog(markdown: string, label: string): string[] {
  const pattern = new RegExp(`^- ${escapeRegExp(label)}: \`([^\`]+)\`$`, 'gm');
  return [...markdown.matchAll(pattern)].map((match) => match[1]).sort();
}

function markdownLowercaseLabeledCatalog(
  markdown: string,
  label: string,
): string[] {
  const pattern = new RegExp(`^- ${escapeRegExp(label)} \`([^\`]+)\`$`, 'gm');
  return [...markdown.matchAll(pattern)].map((match) => match[1]).sort();
}

function markdownNestedCatalog(markdown: string, label: string): string[] {
  const lines = markdown.split(/\r?\n/);
  const start = lines.findIndex((line) => line === `- ${label}:`);
  expect(start).toBeGreaterThanOrEqual(0);
  const values: string[] = [];
  for (let index = start + 1; index < lines.length; index += 1) {
    const match = lines[index].match(/^  - `([^`]+)`$/);
    if (!match) break;
    values.push(match[1]);
  }
  return values.sort();
}

function sorted(values: Iterable<string>): string[] {
  return [...values].sort();
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function isProductionSourceOrConfig(relativePath: string): boolean {
  const baseName = path.posix.basename(relativePath);
  return (
    /\.(?:asmdef|cs|js|json|mjs|ps1|sh|ts|xml|yaml|yml)$/.test(relativePath) ||
    baseName === 'dockerfile'
  );
}

function markdownSection(
  markdown: string,
  startHeading: string,
  endHeading: string,
): string {
  const start = markdown.indexOf(startHeading);
  const end = markdown.indexOf(endHeading, start + startHeading.length);
  expect(start).toBeGreaterThanOrEqual(0);
  expect(end).toBeGreaterThan(start);
  return markdown.slice(start, end);
}

function isAiGuidanceFile(relativePath: string): boolean {
  const normalized = relativePath.split(path.sep).join('/').toLowerCase();
  const baseName = path.posix.basename(normalized);
  return (
    ['.cursorrules', '.windsurfrules', 'agents.md', 'claude.md', 'gemini.md', 'llms.txt'].includes(
      baseName,
    ) || normalized.endsWith('/copilot-instructions.md')
  );
}

function walkFiles(directory: string): string[] {
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    if (
      ['.git', '.superpowers', 'build', 'node_modules'].includes(
        entry.name.toLowerCase(),
      )
    ) {
      return [];
    }
    const resolved = path.join(directory, entry.name);
    return entry.isDirectory() ? walkFiles(resolved) : [resolved];
  });
}
