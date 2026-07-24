import { jest } from '@jest/globals';
import type { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import {
  CompanionResourceService,
  type UnityReadClient,
} from '../resources/companionResources.js';

function toolResult(payload: unknown): CallToolResult {
  return {
    content: [],
    structuredContent: payload as Record<string, unknown>,
  };
}

function textResult(payload: unknown): CallToolResult {
  return {
    content: [{ type: 'text', text: JSON.stringify(payload) }],
  };
}

function clientWith(
  implementation: UnityReadClient['readTool'],
): UnityReadClient & { readTool: jest.MockedFunction<UnityReadClient['readTool']> } {
  return { readTool: jest.fn(implementation) };
}

describe('companion resource mappings', () => {
  test('maps logs with defaults and clamped limits', async () => {
    const client = clientWith(async () => toolResult({ logs: [] }));
    const resources = new CompanionResourceService(client);

    await resources.read('unity://logs');
    await resources.read('unity://logs?severity=warning&limit=9999');
    await resources.read('unity://logs?limit=0');

    expect(client.readTool).toHaveBeenNthCalledWith(1, 'get_console_logs', {
      severity: 'all',
      limit: 100,
    });
    expect(client.readTool).toHaveBeenNthCalledWith(2, 'get_console_logs', {
      severity: 'warning',
      limit: 1000,
    });
    expect(client.readTool).toHaveBeenNthCalledWith(3, 'get_console_logs', {
      severity: 'all',
      limit: 1,
    });
  });

  test('validates log severity and numeric limits', async () => {
    const resources = new CompanionResourceService(clientWith(async () => toolResult({})));

    await expect(resources.read('unity://logs?severity=debug')).rejects.toThrow('severity');
    await expect(resources.read('unity://logs?limit=many')).rejects.toThrow('limit');
  });

  test('maps scene hierarchy and deterministically truncates depth-first', async () => {
    const hierarchy = {
      sceneName: 'Main',
      roots: [
        {
          name: 'A',
          children: [
            { name: 'A1', children: [{ name: 'A1a', children: [] }] },
            { name: 'A2', children: [] },
          ],
        },
        { name: 'B', children: [] },
      ],
    };
    const client = clientWith(async () => textResult(hierarchy));
    const resources = new CompanionResourceService(client);

    const result = await resources.read(
      'unity://scenes-hierarchy?path=Assets%2FMain.unity&max_nodes=3',
    );

    expect(client.readTool).toHaveBeenCalledWith('get_scene_hierarchy', {
      path: 'Assets/Main.unity',
    });
    expect(result.payload).toMatchObject({
      sceneName: 'Main',
      roots: [
        {
          name: 'A',
          children: [
            {
              name: 'A1',
              children: [{ name: 'A1a', children: [] }],
              childrenTruncated: false,
            },
          ],
          childrenTruncated: true,
          omittedDescendants: 1,
        },
      ],
      truncation: {
        truncated: true,
        maxNodes: 3,
        returnedNodes: 3,
        totalNodesKnown: true,
        totalNodes: 5,
        omittedNodes: 2,
      },
    });
  });

  test('uses hierarchy defaults and clamps max_nodes', async () => {
    const client = clientWith(async () => toolResult({ roots: [] }));
    const resources = new CompanionResourceService(client);

    expect((await resources.read('unity://scenes-hierarchy')).payload).toMatchObject({
      truncation: { maxNodes: 500 },
    });
    expect(
      (await resources.read('unity://scenes-hierarchy?max_nodes=99999')).payload,
    ).toMatchObject({ truncation: { maxNodes: 2000 } });
    await expect(
      resources.read('unity://scenes-hierarchy?max_nodes=nope'),
    ).rejects.toThrow('max_nodes');
  });

  test('bounds a 15k-deep hierarchy with stack-safe iterative traversal', async () => {
    let node: Record<string, unknown> = { name: 'leaf', children: [] };
    for (let depth = 0; depth < 15_000; depth++) {
      node = { name: `node-${depth}`, children: [node] };
    }
    const client = clientWith(async () => toolResult({ roots: [node] }));
    const resources = new CompanionResourceService(client);

    const result = await resources.read(
      'unity://scenes-hierarchy?max_nodes=2000',
    );
    const truncation = result.payload.truncation as Record<string, unknown>;

    expect(truncation).toMatchObject({
      truncated: true,
      maxNodes: 2000,
      returnedNodes: 2000,
      totalNodesKnown: false,
      totalNodesAtLeast: 8001,
      omittedNodesAtLeast: 6001,
      traversalBudget: 8000,
      visitedNodes: 8000,
    });

    let output = (result.payload.roots as Array<Record<string, unknown>>)[0];
    let outputCount = 1;
    while ((output.children as unknown[]).length > 0) {
      output = (output.children as Array<Record<string, unknown>>)[0];
      outputCount++;
    }
    expect(outputCount).toBe(2000);
    expect(output.childrenTruncated).toBe(true);
    expect(output.omittedDescendantsKnown).toBe(false);
  });

  test('bounds very wide hierarchies without scanning or cloning every child', async () => {
    const children = Array.from({ length: 50_000 }, (_, index) => ({
      name: `child-${index}`,
      components: ['Transform'],
      children: [],
    }));
    const client = clientWith(async () =>
      toolResult({ roots: [{ name: 'root', children }] }),
    );
    const resources = new CompanionResourceService(client);

    const result = await resources.read(
      'unity://scenes-hierarchy?max_nodes=2',
    );
    const truncation = result.payload.truncation as Record<string, unknown>;
    const root = (result.payload.roots as Array<Record<string, unknown>>)[0];

    expect(truncation).toMatchObject({
      returnedNodes: 2,
      totalNodesKnown: false,
      totalNodesAtLeast: 1027,
      omittedNodesAtLeast: 1025,
      traversalBudget: 1026,
      visitedNodes: 1026,
    });
    expect((root.children as unknown[])).toHaveLength(1);
    expect(root.childrenTruncated).toBe(true);
    expect(root.omittedDescendants).toBe(1024);
    expect(root.omittedDescendantsKnown).toBe(false);
  });

  test('bounds every projected hierarchy value from one malicious node', async () => {
    const huge = 'x'.repeat(2_000_000);
    const components: unknown[] = Array.from(
      { length: 100_000 },
      (_, index) =>
        index === 0
          ? huge
          : index === 1
            ? { name: huge, nested: { surprise: huge } }
            : 'Transform',
    );
    const hierarchy = {
      sceneName: huge,
      scenePath: huge,
      isDirty: { nested: true },
      isActive: true,
      metadataSurprise: { payload: huge },
      roots: [
        {
          name: huge,
          hierarchyPath: huge,
          instanceId: { nested: 42 },
          activeSelf: 'true',
          components,
          objectSurprise: { payload: huge },
          children: [],
        },
      ],
    };
    const resources = new CompanionResourceService(
      clientWith(async () => toolResult(hierarchy)),
    );

    const result = await resources.read(
      'unity://scenes-hierarchy?max_nodes=1',
    );
    const root = (result.payload.roots as Array<Record<string, unknown>>)[0];
    const outputComponents = root.components as string[];

    expect((result.payload.sceneName as string).length).toBeLessThanOrEqual(256);
    expect((result.payload.scenePath as string).length).toBeLessThanOrEqual(1024);
    expect((root.name as string).length).toBeLessThanOrEqual(256);
    expect((root.hierarchyPath as string).length).toBeLessThanOrEqual(1024);
    expect(outputComponents.length).toBeLessThanOrEqual(32);
    expect(outputComponents.every((value) => value.length <= 128)).toBe(true);
    expect(root).not.toHaveProperty('instanceId');
    expect(root).not.toHaveProperty('activeSelf');
    expect(root).not.toHaveProperty('objectSurprise');
    expect(result.payload).not.toHaveProperty('metadataSurprise');
    expect(root.projection).toMatchObject({
      truncatedStringCount: 2,
      truncatedStrings: {
        name: { originalLength: 2_000_000, returnedLength: 256 },
        hierarchyPath: { originalLength: 2_000_000, returnedLength: 1024 },
      },
      omittedKnownFieldCount: 2,
      omittedKnownFields: ['instanceId', 'activeSelf'],
      components: {
        sourceCount: 100_000,
        returnedCount: 32,
        omittedCount: 99_968,
        namesTruncated: 2,
        scanTruncated: true,
      },
    });
    expect(result.payload.metadataProjection).toMatchObject({
      truncatedStringCount: 2,
      truncatedStrings: {
        sceneName: { originalLength: 2_000_000, returnedLength: 256 },
        scenePath: { originalLength: 2_000_000, returnedLength: 1024 },
      },
      omittedKnownFieldCount: 1,
      omittedKnownFields: ['isDirty'],
    });
    expect(Buffer.byteLength(JSON.stringify(result.payload))).toBeLessThan(
      10_000,
    );
  });

  test('maps a GameObject target to bounded inspect_gameobject defaults', async () => {
    const client = clientWith(async () => toolResult({ name: 'Player' }));
    const resources = new CompanionResourceService(client);

    await resources.read('unity://gameobject/%2FPlayer%2FCamera');

    expect(client.readTool).toHaveBeenCalledWith('inspect_gameobject', {
      target: '/Player/Camera',
      max_depth: 2,
      max_nodes: 200,
      include_components: true,
      include_properties: true,
      max_properties_per_component: 100,
    });
    await expect(resources.read('unity://gameobject/')).rejects.toThrow('target');
  });

  test('maps installed packages and validates include_indirect', async () => {
    const client = clientWith(async () => toolResult({ packages: [] }));
    const resources = new CompanionResourceService(client);

    await resources.read('unity://packages');
    await resources.read('unity://packages?include_indirect=false');

    expect(client.readTool).toHaveBeenNthCalledWith(1, 'package_list', {
      scope: 'installed',
      include_indirect: true,
    });
    expect(client.readTool).toHaveBeenNthCalledWith(2, 'package_list', {
      scope: 'installed',
      include_indirect: false,
    });
    await expect(
      resources.read('unity://packages?include_indirect=sometimes'),
    ).rejects.toThrow('include_indirect');
  });

  test.each(['all', 'editor', 'playmode'])('maps test mode %s', async (mode) => {
    const client = clientWith(async () => toolResult({ tests: [] }));
    const resources = new CompanionResourceService(client);

    await resources.read(`unity://tests/${mode}`);

    expect(client.readTool).toHaveBeenCalledWith('list_tests', { mode });
  });

  test('rejects invalid test modes and unknown resources', async () => {
    const resources = new CompanionResourceService(clientWith(async () => toolResult({})));

    await expect(resources.read('unity://tests/runtime')).rejects.toThrow('mode');
    await expect(resources.read('unity://assets')).rejects.toThrow('Unknown companion resource');
  });
});

describe('official tool response decoding', () => {
  test('prefers object structuredContent and falls back to the first JSON text item', async () => {
    const structuredClient = clientWith(async () =>
      toolResult({ source: 'structured' }),
    );
    const textClient = clientWith(async () => ({
      content: [
        { type: 'image', data: 'AA==', mimeType: 'image/png' },
        { type: 'text', text: '{"source":"text"}' },
      ],
    }));

    await expect(
      new CompanionResourceService(structuredClient).read('unity://logs'),
    ).resolves.toMatchObject({ payload: { source: 'structured' } });
    await expect(
      new CompanionResourceService(textClient).read('unity://logs'),
    ).resolves.toMatchObject({ payload: { source: 'text' } });
  });

  test('returns clear errors for tool errors and malformed payloads', async () => {
    const toolError = clientWith(async () => ({
      isError: true,
      content: [{ type: 'text', text: 'Editor is not connected' }],
    }));
    const malformed = clientWith(async () => ({
      content: [{ type: 'text', text: 'not-json' }],
    }));
    const empty = clientWith(async () => ({ content: [] }));

    await expect(
      new CompanionResourceService(toolError).read('unity://logs'),
    ).rejects.toThrow('Editor is not connected');
    await expect(
      new CompanionResourceService(malformed).read('unity://logs'),
    ).rejects.toThrow('malformed');
    await expect(
      new CompanionResourceService(empty).read('unity://logs'),
    ).rejects.toThrow('no JSON payload');
  });

  test('adds actionable context for missing commands, Pipeline disconnects, and CLI exit', async () => {
    for (const message of [
      'Method not found: inspect_gameobject',
      'Pipeline connection refused',
      'Unity CLI process exited with code 1',
    ]) {
      const client = clientWith(async () => {
        throw new Error(message);
      });
      await expect(
        new CompanionResourceService(client).read('unity://gameobject/Player'),
      ).rejects.toThrow(`inspect_gameobject failed: ${message}`);
    }
  });
});
