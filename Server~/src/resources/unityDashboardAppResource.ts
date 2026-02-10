import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { ReadResourceResult } from '@modelcontextprotocol/sdk/types.js';
import { registerAppResource, RESOURCE_MIME_TYPE } from '@modelcontextprotocol/ext-apps/server';
import { Logger } from '../utils/logger.js';

const resourceName = 'unity_dashboard_app';
const appResourceUri = 'ui://unity-dashboard';
const legacyResourceName = 'unity_dashboard_app_legacy';
const legacyResourceUri = 'unity://ui/dashboard';
const resourceMimeType = RESOURCE_MIME_TYPE;

export function registerUnityDashboardAppResource(server: McpServer, logger: Logger) {
  logger.info(`Registering resource: ${resourceName}`);

  registerAppResource(
    server,
    resourceName,
    appResourceUri,
    {
      description: 'Unity dashboard MCP App UI',
    },
    async () => {
      try {
        return readDashboardHtml();
      } catch (error) {
        logger.error(`Error reading dashboard HTML: ${error}`);
        throw error;
      }
    }
  );

  // Legacy URI for compatibility with older hosts / docs that expect unity://ui/dashboard
  server.resource(
    legacyResourceName,
    legacyResourceUri,
    {
      description: 'Unity dashboard MCP App UI (legacy resource URI)',
      mimeType: resourceMimeType
    },
    async () => {
      try {
        return readDashboardHtml(legacyResourceUri);
      } catch (error) {
        logger.error(`Error reading dashboard HTML (legacy uri): ${error}`);
        throw error;
      }
    }
  );
}

function readDashboardHtml(uriOverride?: string): ReadResourceResult {
  const { text } = readUnityDashboardHtml();
  const uri = uriOverride ?? appResourceUri;

  return {
    contents: [
      {
        uri,
        mimeType: resourceMimeType,
        text,
        _meta: {
          // For hosts that still look for legacy view hints.
          view: 'mcp-app',
          ui: {
            prefersBorder: true
          }
        }
      }
    ]
  };
}

export function readUnityDashboardHtml(): { text: string; mimeType: string } {
  const htmlPath = resolveDashboardPath();
  const text = fs.readFileSync(htmlPath, 'utf8');

  return { text, mimeType: resourceMimeType };
}

function resolveDashboardPath(): string {
  // IMPORTANT: do not use process.cwd() here.
  // VS Code runs MCP servers with the CWD of the *client workspace*, which may be
  // unrelated to the server's install location.
  const moduleDir = path.dirname(fileURLToPath(import.meta.url));

  const candidates = [
    // Works when running TS directly (src/resources -> src/ui)
    // and when running built JS with copied assets (build/resources -> build/ui)
    path.join(moduleDir, '..', 'ui', 'unity-dashboard.html'),

    // Fallback for dev repos where build output exists but UI wasn't copied.
    path.join(moduleDir, '..', '..', 'src', 'ui', 'unity-dashboard.html'),
  ];

  for (const candidate of candidates) {
    if (fs.existsSync(candidate)) {
      return candidate;
    }
  }

  throw new Error(
    `Unity dashboard UI file is missing. Checked: ${candidates.join(', ')}`
  );
}
