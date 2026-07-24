import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const serverRoot = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  '..',
  '..',
);
const repositoryRoot = path.resolve(serverRoot, '..');

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
  'UNITY_HOST',
  'ProjectSettings/McpUnitySettings.json',
  'Unity-driven npm install/build',
  'automatic MCP-client configuration',
  'PackedCache mutation',
  'custom WebSocket endpoint/port',
] as const;

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
    expect(rootPackage).not.toHaveProperty('mcpname');
    expect(companionPackage).not.toHaveProperty('bin');
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

  test('derives the complete migration inventory from the 1.4.0 tag', () => {
    // Git metadata is present in repository CI and proves this snapshot against
    // the release tag. UPM package archives intentionally omit .git; the
    // embedded inventory still drives all migration-row assertions there.
    if (!fs.existsSync(path.join(repositoryRoot, '.git'))) return;

    const files = gitLines(
      'ls-tree',
      '-r',
      '--name-only',
      '1.4.0',
      'Server~/src/tools',
      'Server~/src/resources',
      'Server~/src/prompts',
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

    for (const command of extensionCommands) {
      expect(extensionSection).toContain(`\`${command}\``);
    }
    for (const legacyTool of legacyTools) {
      if (!extensionCommands.includes(legacyTool as (typeof extensionCommands)[number])) {
        expect(extensionSection).not.toContain(`\`${legacyTool}\``);
      }
    }
    for (const resource of companionResources) {
      expect(companionSection).toContain(`\`${resource}\``);
    }
    for (const prompt of legacyPrompts) {
      expect(companionSection).toContain(`\`${prompt}\``);
    }
    expect(companionSection).toContain('`show_unity_dashboard`');
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
    ];
    for (const relativePath of forbiddenPaths) {
      expect(fs.existsSync(path.join(repositoryRoot, relativePath))).toBe(false);
    }

    const productionText = walkFiles(repositoryRoot)
      .filter((file) => {
        const relative = path.relative(repositoryRoot, file);
        return (
          !relative.includes(`${path.sep}__tests__${path.sep}`) &&
          !relative.startsWith(`docs${path.sep}`) &&
          !relative.endsWith('.md') &&
          !relative.endsWith('.meta') &&
          !relative.includes(`${path.sep}build${path.sep}`) &&
          !relative.includes(`${path.sep}node_modules${path.sep}`) &&
          /\.(?:cs|ts|json|asmdef)$/.test(relative)
        );
      })
      .map((file) => fs.readFileSync(file, 'utf8'))
      .join('\n');

    for (const forbiddenText of [
      'websocket-sharp',
      'WebSocketSharp',
      'localhost:8090',
      'McpUnitySettings',
      'com.unity.editorcoroutines',
      'com.unity.nuget.newtonsoft-json',
      'PackedCache',
    ]) {
      expect(productionText).not.toContain(forbiddenText);
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
  return execFileSync('git', args, {
    cwd: repositoryRoot,
    encoding: 'utf8',
  });
}

function gitLines(...args: string[]): string[] {
  return git(...args).trim().split(/\r?\n/).filter(Boolean);
}

function collectMatches(
  target: Set<string>,
  source: string,
  pattern: RegExp,
): void {
  for (const match of source.matchAll(pattern)) target.add(match[1]);
}

function expectMigrationRow(concept: string): void {
  expect(readme).toContain(`| \`${concept}\` |`);
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

function walkFiles(directory: string): string[] {
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    if (
      ['.git', '.superpowers', 'build', 'node_modules'].includes(entry.name)
    ) {
      return [];
    }
    const resolved = path.join(directory, entry.name);
    return entry.isDirectory() ? walkFiles(resolved) : [resolved];
  });
}
