import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import * as z from 'zod';
import { Logger } from '../utils/logger.js';

const toolName = 'get_scene_info';
const toolDescription = 'Gets information about the active scene including name, path, dirty state, root object count, and loaded state. Also returns info about all currently loaded scenes.';

const paramsSchema = z.object({});

export function registerGetSceneInfoTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
  const response = await mcpUnity.sendRequest({
    method: toolName,
    params
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to get scene info'
    );
  }

  // Format the scene info for display
  const activeScene = response.activeScene;
  let text = `Active Scene: ${activeScene.name}\n`;
  text += `  Path: ${activeScene.path || '(unsaved)'}\n`;
  text += `  Build Index: ${activeScene.buildIndex}\n`;
  text += `  Is Dirty: ${activeScene.isDirty}\n`;
  text += `  Is Loaded: ${activeScene.isLoaded}\n`;
  text += `  Root Count: ${activeScene.rootCount}\n`;

  if (response.loadedSceneCount > 1) {
    text += `\nLoaded Scenes (${response.loadedSceneCount}):\n`;
    for (const scene of response.loadedScenes) {
      text += `  - ${scene.name}${scene.isActive ? ' (active)' : ''}: ${scene.path || '(unsaved)'}\n`;
    }
  }

  return {
    content: [{
      type: response.type as "text",
      text: text
    }],
    data: {
      activeScene: response.activeScene,
      loadedSceneCount: response.loadedSceneCount,
      loadedScenes: response.loadedScenes
    }
  };
}
