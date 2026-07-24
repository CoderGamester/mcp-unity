import { cp, mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';

const serverRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const temporaryRoot = await mkdtemp(path.join(os.tmpdir(), 'mcp-unity-clean-mcp-'));
const cleanPackage = path.join(temporaryRoot, 'package');
const cleanServer = path.join(cleanPackage, 'Server~');
const projectPath = path.join(temporaryRoot, 'UnityProject');
const fakeMcp = path.join(cleanServer, 'mcp');
let client;
let transport;

try {
  await mkdir(cleanServer, { recursive: true });
  await cp(path.join(serverRoot, 'build'), path.join(cleanServer, 'build'), {
    recursive: true,
  });
  await cp(
    path.join(serverRoot, 'package.json'),
    path.join(cleanServer, 'package.json'),
  );
  await cp(
    path.join(serverRoot, 'THIRD_PARTY_NOTICES.md'),
    path.join(cleanServer, 'THIRD_PARTY_NOTICES.md'),
  );
  await mkdir(path.join(projectPath, 'Assets'), { recursive: true });
  await mkdir(path.join(projectPath, 'ProjectSettings'), { recursive: true });
  await writeFile(
    fakeMcp,
    [
      "import readline from 'node:readline';",
      '',
      'const input = readline.createInterface({ input: process.stdin });',
      "input.on('line', (line) => {",
      '  const request = JSON.parse(line);',
      '  if (request.id === undefined) return;',
      '  let result;',
      "  if (request.method === 'initialize') {",
      '    result = {',
      '      protocolVersion: request.params.protocolVersion,',
      '      capabilities: { tools: {} },',
      "      serverInfo: { name: 'portable-fake-unity-mcp', version: '1.0.0' },",
      '    };',
      "  } else if (request.method === 'tools/call') {",
      '    result = {',
      "      content: [{ type: 'text', text: '{\"logs\":[]}' }],",
      '      structuredContent: { logs: [] },',
      '    };',
      '  } else {',
      '    result = {};',
      '  }',
      '  process.stdout.write(JSON.stringify({',
      "    jsonrpc: '2.0',",
      '    id: request.id,',
      '    result,',
      "  }) + '\\n');",
      '});',
      '',
    ].join('\n'),
    'utf8',
  );

  assertNoAncestorNodeModules(cleanServer);

  const cleanEntrypoint = path.join(cleanServer, 'build', 'index.js');
  const startup = spawnSync(process.execPath, [cleanEntrypoint], {
    cwd: cleanServer,
    encoding: 'utf8',
    env: { PATH: process.env.PATH ?? '' },
    timeout: 10_000,
  });
  if (
    startup.status !== 1 ||
    !startup.stderr.includes('--project-path') ||
    startup.stderr.includes('ERR_MODULE_NOT_FOUND')
  ) {
    throw new Error(
      `Clean companion startup smoke failed: status=${startup.status}, stderr=${startup.stderr}`,
    );
  }

  transport = new StdioClientTransport({
    command: process.execPath,
    args: [
      cleanEntrypoint,
      '--project-path',
      projectPath,
      '--unity-cli-path',
      process.execPath,
    ],
    cwd: cleanServer,
    stderr: 'pipe',
  });
  client = new Client(
    { name: 'clean-archive-dashboard-smoke', version: '1.0.0' },
    { capabilities: {} },
  );
  await client.connect(transport);
  const result = await client.readResource({ uri: 'ui://unity-dashboard' });
  const content = result.contents[0];

  if (content?.mimeType !== 'text/html;profile=mcp-app') {
    throw new Error(`Unexpected dashboard MIME type: ${content?.mimeType}`);
  }
  if (
    typeof content.text !== 'string' ||
    !content.text.includes('Unity Dashboard') ||
    !content.text.includes('unity://logs')
  ) {
    throw new Error('Bundled dashboard resource did not return its HTML.');
  }
  if (
    !content._meta ||
    !('ui' in content._meta) ||
    !content._meta.ui ||
    typeof content._meta.ui !== 'object' ||
    !('csp' in content._meta.ui)
  ) {
    throw new Error('Bundled dashboard resource did not return MCP App metadata.');
  }

  const logs = await client.readResource({
    uri: 'unity://logs?severity=all&limit=1',
  });
  const logsPayload = JSON.parse(logs.contents[0]?.text ?? 'null');
  if (!Array.isArray(logsPayload?.logs)) {
    throw new Error('Portable fake MCP server did not return Unity logs.');
  }
} finally {
  await client?.close().catch(() => undefined);
  await transport?.close().catch(() => undefined);
  await rm(temporaryRoot, { recursive: true, force: true });
}

function assertNoAncestorNodeModules(start) {
  let current = path.resolve(start);
  while (true) {
    const candidate = path.join(current, 'node_modules');
    if (
      candidate !== path.join(serverRoot, 'node_modules') &&
      existsSync(candidate)
    ) {
      throw new Error(`Clean package has an accessible node_modules at ${candidate}`);
    }
    const parent = path.dirname(current);
    if (parent === current) break;
    current = parent;
  }
}
