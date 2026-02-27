import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'set_play_mode_status';
const toolDescription = "Controls Unity play mode. Actions: 'play' (start or unpause), 'pause' (toggle pause), 'stop' (exit play mode), 'step' (advance one frame while paused).";

const paramsSchema = z.object({
  action: z.enum(['play', 'pause', 'stop', 'step']).describe("The play mode action to execute: 'play', 'pause', 'stop', or 'step'")
});

export function registerSetPlayModeStatusTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
    async (params: any) => {
      try {
        logger.info(`Executing tool: ${toolName}`, params);
        const result = await toolHandler(mcpUnity, params);
        logger.info(`Tool execution successful: ${toolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${toolName}`, error);
        throw error;
      }
    }
  );
}

async function toolHandler(mcpUnity: McpUnity, params: any): Promise<CallToolResult> {
  const validatedParams = paramsSchema.parse(params);
  
  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: validatedParams
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || `Failed to execute play mode action: ${validatedParams.action}`
    );
  }

  const statusText = response.isPlaying
    ? (response.isPaused ? 'Playing (paused)' : 'Playing')
    : 'Edit mode';

  return {
    content: [
      {
        type: 'text',
        text: `Play mode action '${validatedParams.action}' executed successfully. Current state: ${statusText}`
      }
    ],
    isError: false
  };
}
