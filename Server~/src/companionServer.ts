import { registerAppResource, registerAppTool } from '@modelcontextprotocol/ext-apps/server';
import {
  McpServer,
  ResourceTemplate,
} from '@modelcontextprotocol/sdk/server/mcp.js';
import { registerCompanionPrompts } from './prompts/companionPrompts.js';
import type { CompanionResourceService } from './resources/companionResources.js';
import {
  DASHBOARD_URI,
  readDashboardHtml,
} from './resources/dashboardResource.js';
import { boundedError } from './utils/boundedError.js';

const RESOURCE_TEMPLATES = [
  {
    name: 'unity_logs',
    template: 'unity://logs{?severity,limit}',
    description: 'Recent Unity Editor logs.',
  },
  {
    name: 'unity_scenes_hierarchy',
    template: 'unity://scenes-hierarchy{?path,max_nodes}',
    description: 'Bounded hierarchy of an open Unity scene.',
  },
  {
    name: 'unity_gameobject',
    template: 'unity://gameobject/{target}',
    description: 'Bounded GameObject inspection.',
  },
  {
    name: 'unity_packages',
    template: 'unity://packages{?include_indirect}',
    description: 'Installed Unity packages.',
  },
  {
    name: 'unity_tests',
    template: 'unity://tests/{mode}',
    description: 'Available Unity tests.',
  },
] as const;

export interface CompanionServerOptions {
  readDashboardHtml?: typeof readDashboardHtml;
}

export function createCompanionServer(
  resources: CompanionResourceService,
  options: CompanionServerOptions = {},
): McpServer {
  const server = new McpServer(
    { name: 'MCP Unity Companion', version: '2.0.0' },
    { capabilities: { tools: {}, resources: {}, prompts: {} } },
  );

  registerAppTool(
    server,
    'show_unity_dashboard',
    {
      description: 'Open the read-only Unity CLI and Pipeline dashboard.',
      annotations: { readOnlyHint: true },
      _meta: {
        ui: {
          resourceUri: DASHBOARD_URI,
        },
      },
    },
    async () => ({
      content: [
        {
          type: 'text',
          text: 'Unity dashboard opened. Its views are read-only.',
        },
      ],
    }),
  );

  registerAppResource(
    server,
    'unity_dashboard',
    DASHBOARD_URI,
    {
      description: 'Read-only Unity CLI and Pipeline dashboard.',
      _meta: { ui: { prefersBorder: true } },
    },
    async () => {
      try {
        const dashboard = (
          options.readDashboardHtml ?? readDashboardHtml
        )();
        return {
          contents: [
            {
              uri: DASHBOARD_URI,
              mimeType: dashboard.mimeType,
              text: dashboard.text,
              _meta: {
                ui: {
                  csp: {
                    connectDomains: [],
                    resourceDomains: [],
                    frameDomains: [],
                    baseUriDomains: [],
                  },
                },
              },
            },
          ],
        };
      } catch (error) {
        throw boundedError(error);
      }
    },
  );

  for (const definition of RESOURCE_TEMPLATES) {
    server.registerResource(
      definition.name,
      new ResourceTemplate(definition.template, { list: undefined }),
      {
        description: definition.description,
        mimeType: 'application/json',
      },
      async (uri) => {
        try {
          const result = await resources.read(uri.toString());
          return {
            contents: [
              {
                uri: result.uri,
                mimeType: 'application/json',
                text: JSON.stringify(result.payload),
              },
            ],
          };
        } catch (error) {
          throw boundedError(error);
        }
      },
    );
  }

  registerCompanionPrompts(server);
  return server;
}
