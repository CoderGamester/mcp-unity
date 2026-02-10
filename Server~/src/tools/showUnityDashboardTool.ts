import * as z from 'zod';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { Logger } from '../utils/logger.js';
import { registerAppTool } from '@modelcontextprotocol/ext-apps/server';
import { readUnityDashboardHtml } from '../resources/unityDashboardAppResource.js';

const toolName = 'show_unity_dashboard';
const toolDescription = 'Opens the Unity dashboard MCP App in VS Code.';
const paramsSchema = z.object({});

export function registerShowUnityDashboardTool(server: McpServer, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  registerAppTool(server, toolName, {
    description: toolDescription,
    inputSchema: paramsSchema.shape,
    _meta: {
      ui: {
        resourceUri: 'ui://unity-dashboard'
      }
    }
  }, async () => {
    try {
      logger.info(`Executing tool: ${toolName}`);
      const result = await toolHandler();
      logger.info(`Tool execution successful: ${toolName}`);
      return result;
    } catch (error) {
      logger.error(`Tool execution failed: ${toolName}`, error);
      throw error;
    }
  });
}

async function toolHandler(): Promise<CallToolResult> {
  // Returning an embedded resource here is the most compatible option:
  // - Some hosts open the app via tool metadata (_meta.ui.resourceUri)
  // - Others rely on content blocks with view hints (legacy)
  const { text, mimeType } = readUnityDashboardHtml();

  return {
    content: [
      {
        type: 'resource',
        resource: {
          // IMPORTANT: Use the ui:// scheme so MCP App-capable hosts (e.g. VS Code)
          // recognize this as an App resource and enable native “Open in Editor”.
          uri: 'ui://unity-dashboard',
          mimeType,
          text,
          _meta: {
            view: 'mcp-app'
          }
        }
      },
      // Legacy fallback for hosts/docs that still expect this URI.
      {
        type: 'resource',
        resource: {
          uri: 'unity://ui/dashboard',
          mimeType,
          text,
          _meta: {
            view: 'mcp-app'
          }
        }
      }
    ]
  };
}
