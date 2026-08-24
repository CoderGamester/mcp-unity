import { McpUnity } from '../unity/mcpUnity.js';
import { Logger } from '../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { ReadResourceResult } from '@modelcontextprotocol/sdk/types.js';

// Constants for the resource
const resourceName = 'get_inspector_state';
const resourceUri = 'unity://inspector_state';
const resourceMimeType = 'application/json';

/**
 * Creates and registers the Inspector State resource with the MCP server.
 * Provides the Editor's current selection and inspected component; the
 * Unity Dashboard app polls this URI for its inspector-focus highlight.
 *
 * @param server The MCP server instance to register with
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param logger The logger instance for diagnostic information
 */
export function registerGetInspectorStateResource(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering resource: ${resourceName}`);

  server.resource(
    resourceName,
    resourceUri,
    {
      description: "Current Editor selection and inspected component (Unity Dashboard inspector focus)",
      mimeType: resourceMimeType
    },
    async () => {
      try {
        return await resourceHandler(mcpUnity);
      } catch (error) {
        logger.error(`Error handling resource ${resourceName}: ${error}`);
        throw error;
      }
    }
  );
}

/**
 * Handles requests for the Editor's inspector state from Unity
 *
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @returns A promise that resolves to the inspector state payload
 * @throws McpUnityError if the request to Unity fails
 */
async function resourceHandler(mcpUnity: McpUnity): Promise<ReadResourceResult> {
  const response = await mcpUnity.sendRequest({
    method: resourceName,
    params: {}
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.RESOURCE_FETCH,
      response.message || 'Failed to fetch inspector state from Unity'
    );
  }

  return {
    contents: [{
      uri: resourceUri,
      mimeType: resourceMimeType,
      text: JSON.stringify({
        activeGameObject: response.activeGameObject ?? null,
        focusedComponent: response.focusedComponent ?? null
      }, null, 2)
    }]
  };
}
