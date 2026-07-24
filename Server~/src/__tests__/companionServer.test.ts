import { jest } from '@jest/globals';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { InMemoryTransport } from '@modelcontextprotocol/sdk/inMemory.js';
import type { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { createCompanionServer } from '../companionServer.js';
import {
  CompanionResourceService,
  type UnityReadClient,
} from '../resources/companionResources.js';

async function connectedCompanion() {
  const readTool = jest.fn(async (): Promise<CallToolResult> => ({
    content: [],
    structuredContent: { ok: true },
  }));
  const unityClient: UnityReadClient = { readTool };
  const server = createCompanionServer(new CompanionResourceService(unityClient));
  const client = new Client(
    { name: 'companion-test', version: '1.0.0' },
    { capabilities: {} },
  );
  const [clientTransport, serverTransport] = InMemoryTransport.createLinkedPair();
  await Promise.all([
    server.connect(serverTransport),
    client.connect(clientTransport),
  ]);
  return { client, server, readTool };
}

describe('outer MCP companion catalog', () => {
  test('advertises exactly one dashboard tool, two prompts, and six resources', async () => {
    const { client, server } = await connectedCompanion();
    try {
      const tools = await client.listTools();
      const prompts = await client.listPrompts();
      const resources = await client.listResources();
      const templates = await client.listResourceTemplates();

      expect(tools.tools.map((tool) => tool.name)).toEqual(['show_unity_dashboard']);
      expect(prompts.prompts.map((prompt) => prompt.name).sort()).toEqual([
        'gameobject_handling_strategy',
        'unity_dashboard',
      ]);
      expect(resources.resources.map((resource) => resource.uri)).toEqual([
        'ui://unity-dashboard',
      ]);
      expect(templates.resourceTemplates.map((template) => template.uriTemplate).sort()).toEqual([
        'unity://gameobject/{target}',
        'unity://logs{?severity,limit}',
        'unity://packages{?include_indirect}',
        'unity://scenes-hierarchy{?path,max_nodes}',
        'unity://tests/{mode}',
      ]);

      const forbiddenMutationTools = [
        'assign_material',
        'duplicate_gameobject',
        'editor_step',
        'execute_menu_item',
        'package_add',
        'run_tests',
        'unload_scene',
      ];
      expect(tools.tools.map((tool) => tool.name)).not.toEqual(
        expect.arrayContaining(forbiddenMutationTools),
      );
    } finally {
      await client.close();
      await server.close();
    }
  });

  test('routes concrete resource reads without advertising official tools', async () => {
    const { client, server, readTool } = await connectedCompanion();
    try {
      const result = await client.readResource({
        uri: 'unity://logs?severity=error&limit=4',
      });
      expect(readTool).toHaveBeenCalledWith('get_console_logs', {
        severity: 'error',
        limit: 4,
      });
      expect(JSON.parse(result.contents[0].text as string)).toEqual({ ok: true });
    } finally {
      await client.close();
      await server.close();
    }
  });

  test('dashboard tool and app resource expose MCP App metadata and bundled HTML', async () => {
    const { client, server } = await connectedCompanion();
    try {
      const tools = await client.listTools();
      expect(tools.tools[0]._meta).toMatchObject({
        ui: { resourceUri: 'ui://unity-dashboard' },
        'ui/resourceUri': 'ui://unity-dashboard',
      });

      const toolResult = await client.callTool({ name: 'show_unity_dashboard' });
      expect(toolResult.isError).not.toBe(true);
      const app = await client.readResource({ uri: 'ui://unity-dashboard' });
      expect(app.contents[0].mimeType).toBe('text/html;profile=mcp-app');
      const html = app.contents[0].text as string;
      for (const resource of [
        'unity://logs',
        'unity://scenes-hierarchy',
        'unity://gameobject/',
        'unity://packages',
        'unity://tests/',
        'ui://unity-dashboard',
      ]) {
        expect(html).toContain(resource);
      }
      expect(html).toContain('refreshInFlight');
      expect(html).toContain('MIN_REFRESH_MS');
      expect(html).toContain('Pipeline');
      expect(html).toContain('truncation');
      expect(html).not.toContain('set_play_mode_status');
      expect(html).not.toContain('tools/call');
    } finally {
      await client.close();
      await server.close();
    }
  });

  test('prompts use official Pipeline and five extension command names without aliases', async () => {
    const { client, server } = await connectedCompanion();
    try {
      const strategy = await client.getPrompt({
        name: 'gameobject_handling_strategy',
      });
      const dashboard = await client.getPrompt({ name: 'unity_dashboard' });
      const promptText = [...strategy.messages, ...dashboard.messages]
        .map((message) =>
          message.content.type === 'text' ? message.content.text : '',
        )
        .join('\n');

      for (const command of [
        'get_scene_hierarchy',
        'package_list',
        'list_tests',
        'inspect_gameobject',
        'duplicate_gameobject',
        'unload_scene',
        'editor_step',
        'assign_material',
      ]) {
        expect(promptText).toContain(command);
      }
      for (const legacyAlias of [
        'get_gameobject',
        'get_scene_info',
        'set_play_mode_status',
        'update_gameobject',
      ]) {
        expect(promptText).not.toContain(legacyAlias);
      }
    } finally {
      await client.close();
      await server.close();
    }
  });
});
