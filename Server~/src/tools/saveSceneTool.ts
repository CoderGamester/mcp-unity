import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import * as z from 'zod';
import { Logger } from '../utils/logger.js';

const toolName = 'save_scene';
const toolDescription = 'Saves the current active scene. Optionally saves to a new path (Save As)';

const paramsSchema = z.object({
  scenePath: z.string().optional().describe("The path to save the scene to (e.g., 'Assets/Scenes/MyScene.unity'). Required if saveAs is true"),
  saveAs: z.boolean().optional().describe('If true, saves to a new path specified by scenePath. Default: false')
});

export function registerSaveSceneTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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

async function toolHandler(mcpUnity: McpUnity, params: any) {
  // Validate that scenePath is provided when saveAs is true
  if (params.saveAs && !params.scenePath) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "'scenePath' is required when 'saveAs' is true"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to save scene'
    );
  }

  return {
    content: [{
      type: response.type,
      text: response.message || 'Successfully saved scene'
    }],
    data: {
      scenePath: response.scenePath,
      sceneName: response.sceneName
    }
  };
}
