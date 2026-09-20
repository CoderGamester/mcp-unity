import { Logger } from '../utils/logger.js';
import { ResourceTemplate, McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { ReadResourceResult } from '@modelcontextprotocol/sdk/types.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { Variables } from '@modelcontextprotocol/sdk/shared/uriTemplate.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';

export const resourceName = 'create_scene';
export const resourceUri = 'unity://scenes/create';
export const resourceMimeType = 'application/json';

export function registerCreateSceneResource(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering resource: ${resourceName}`);

  // Use a template with parameters for the create scene resource
  const template = new ResourceTemplate(
    resourceUri,
    {
      list: undefined
    }
  );

  server.resource(
    resourceName,
    template,
    {
      description: 'Creates a new Unity scene and saves it to the specified path',
      mimeType: resourceMimeType
    },
    async (uri: URL, variables: Variables, _extra: any) => {
      try {
        return await resourceHandler(mcpUnity, uri, variables, logger);
      } catch (error) {
        logger.error(`Error handling resource ${resourceName}: ${error}`);
        throw error;
      }
    }
  );
}

async function resourceHandler(
  mcpUnity: McpUnity,
  uri: URL,
  variables: Variables,
  logger: Logger
): Promise<ReadResourceResult> {
  const sceneName = variables['sceneName'] as string;
  const folderPath = variables['folderPath'] as string || 'Assets';
  const addToBuildSettings = variables['addToBuildSettings'] === 'true';
  const makeActive = variables['makeActive'] !== 'false';

  if (!sceneName || sceneName.trim() === '') {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Required parameter 'sceneName' not provided"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: resourceName,
    params: {
      sceneName,
      folderPath,
      addToBuildSettings,
      makeActive
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.RESOURCE_FETCH,
      response.message || 'Failed to create scene'
    );
  }

  return {
    contents: [{
      uri: resourceUri,
      mimeType: resourceMimeType,
      text: JSON.stringify(response, null, 2)
    }]
  };
}