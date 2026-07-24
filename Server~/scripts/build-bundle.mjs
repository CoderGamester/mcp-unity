import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { build } from 'esbuild';

const serverRoot = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  '..',
);
const trackedBuild = path.join(serverRoot, 'build');
const trackedNotices = path.join(serverRoot, 'THIRD_PARTY_NOTICES.md');
const checkOnly = process.argv.includes('--check');
const temporaryRoot = fs.mkdtempSync(
  path.join(os.tmpdir(), 'mcp-unity-companion-build-'),
);
const outputRoot = path.join(temporaryRoot, 'build');

try {
  fs.mkdirSync(outputRoot, { recursive: true });
  execFileSync(
    process.execPath,
    [
      path.join(serverRoot, 'node_modules', 'typescript', 'bin', 'tsc'),
      '--noEmit',
    ],
    { cwd: serverRoot, stdio: 'inherit' },
  );
  const buildResult = await build({
    entryPoints: [path.join(serverRoot, 'src', 'index.ts')],
    outfile: path.join(outputRoot, 'index.js'),
    bundle: true,
    platform: 'node',
    format: 'esm',
    target: 'node20',
    charset: 'utf8',
    legalComments: 'none',
    metafile: true,
    sourcemap: false,
    minify: false,
    treeShaking: true,
    logLevel: 'error',
    banner: {
      js: [
        "import { createRequire as __mcpCreateRequire } from 'node:module';",
        'const require = __mcpCreateRequire(import.meta.url);',
      ].join('\n'),
    },
  });
  const bundledEntrypoint = path.join(outputRoot, 'index.js');
  fs.writeFileSync(
    bundledEntrypoint,
    fs.readFileSync(bundledEntrypoint, 'utf8').replace(/^[\t ]+$/gm, ''),
  );

  fs.mkdirSync(path.join(outputRoot, 'ui'), { recursive: true });
  fs.copyFileSync(
    path.join(serverRoot, 'src', 'ui', 'unity-dashboard.html'),
    path.join(outputRoot, 'ui', 'unity-dashboard.html'),
  );

  const notices = createThirdPartyNotices(buildResult.metafile.inputs);
  const generatedNotices = path.join(temporaryRoot, 'THIRD_PARTY_NOTICES.md');
  fs.writeFileSync(generatedNotices, notices);

  if (checkOnly) {
    assertDirectoriesEqual(trackedBuild, outputRoot);
    assertFilesEqual(trackedNotices, generatedNotices);
  } else {
    fs.rmSync(trackedBuild, { recursive: true, force: true });
    fs.cpSync(outputRoot, trackedBuild, { recursive: true });
    fs.copyFileSync(generatedNotices, trackedNotices);
  }
} finally {
  fs.rmSync(temporaryRoot, { recursive: true, force: true });
}

function createThirdPartyNotices(inputs) {
  const packageRoots = new Map();
  for (const input of Object.keys(inputs)) {
    const normalized = input.split(path.sep).join('/');
    const marker = 'node_modules/';
    const markerIndex = normalized.lastIndexOf(marker);
    if (markerIndex < 0) continue;
    const relative = normalized.slice(markerIndex + marker.length);
    const segments = relative.split('/');
    const packageName = segments[0].startsWith('@')
      ? `${segments[0]}/${segments[1]}`
      : segments[0];
    const packageRoot = path.resolve(
      serverRoot,
      normalized.slice(0, markerIndex + marker.length),
      packageName,
    );
    packageRoots.set(packageName, packageRoot);
  }

  const sections = [...packageRoots]
    .sort(([left], [right]) => left.localeCompare(right))
    .map(([packageName, packageRoot]) => {
      const manifest = JSON.parse(
        fs.readFileSync(path.join(packageRoot, 'package.json'), 'utf8'),
      );
      const licensePath = findLicenseFile(packageRoot);
      if (!licensePath) {
        throw new Error(`Bundled package ${packageName} has no license file.`);
      }
      const licenseText = fs.readFileSync(licensePath, 'utf8').trim();
      const source =
        typeof manifest.repository === 'string'
          ? manifest.repository
          : manifest.repository?.url ?? manifest.homepage ?? 'Not declared';
      return [
        `## ${packageName} ${manifest.version}`,
        '',
        `License: ${manifest.license ?? 'See included text'}`,
        '',
        `Source: ${source}`,
        '',
        '```text',
        licenseText.replaceAll('```', '`` `'),
        '```',
      ].join('\n');
    });

  return [
    '# Third-Party Notices',
    '',
    'MCP Unity bundles the following runtime dependencies into `build/index.js`.',
    'This file is generated from the exact packages included by the companion build.',
    '',
    ...sections.flatMap((section) => [section, '']),
  ].join('\n');
}

function findLicenseFile(packageRoot) {
  const candidates = fs
    .readdirSync(packageRoot)
    .filter((name) => /^(license|licence|copying|notice)(\.|$)/i.test(name))
    .sort();
  return candidates.length > 0 ? path.join(packageRoot, candidates[0]) : undefined;
}

function assertDirectoriesEqual(expectedRoot, actualRoot) {
  const expectedFiles = walkFiles(expectedRoot).map((file) =>
    path.relative(expectedRoot, file),
  );
  const actualFiles = walkFiles(actualRoot).map((file) =>
    path.relative(actualRoot, file),
  );
  if (JSON.stringify(expectedFiles) !== JSON.stringify(actualFiles)) {
    throw new Error(
      `Tracked build files differ.\nExpected: ${expectedFiles.join(', ')}\nActual: ${actualFiles.join(', ')}`,
    );
  }
  for (const relative of expectedFiles) {
    assertFilesEqual(
      path.join(expectedRoot, relative),
      path.join(actualRoot, relative),
    );
  }
}

function walkFiles(directory) {
  if (!fs.existsSync(directory)) return [];
  return fs
    .readdirSync(directory, { withFileTypes: true })
    .sort((left, right) => left.name.localeCompare(right.name))
    .flatMap((entry) => {
      const resolved = path.join(directory, entry.name);
      return entry.isDirectory() ? walkFiles(resolved) : [resolved];
    });
}

function assertFilesEqual(expected, actual) {
  if (
    !fs.existsSync(expected) ||
    !fs.existsSync(actual) ||
    !fs.readFileSync(expected).equals(fs.readFileSync(actual))
  ) {
    throw new Error(
      `Tracked artifact ${path.relative(serverRoot, expected)} is stale. Run npm run build.`,
    );
  }
}
