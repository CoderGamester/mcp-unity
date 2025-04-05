import { ResourceTemplate } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { resourceName as hierarchyResourceName } from './getHierarchyResource.js';
// Constants for the resource
const resourceName = 'get_gameobject';
const resourceUri = 'unity://gameobject/{id}';
const resourceMimeType = 'application/json';
/**
 * Creates and registers the GameObject resource with the MCP server
 * This resource provides access to GameObjects in Unity scenes
 *
 * @param server The MCP server instance to register with
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param logger The logger instance for diagnostic information
 */
export function createGetGameObjectResource(server, mcpUnity, logger) {
    // Create a resource template with the MCP SDK
    const resourceTemplate = new ResourceTemplate(resourceUri, {
        // This list method is commented because is calling getHierarchyResource every second to the MCP client in the current format.
        // TODO: Find a new way to implement this so that it doesn't request the list of game objects so often
        list: undefined //async () => listGameObjects(mcpUnity, logger, resourceMimeType)
    });
    logger.info(`Registering resource: ${resourceName}`);
    // Register this resource with the MCP server
    server.resource(resourceName, resourceTemplate, {
        description: 'Retrieve a GameObject by ID or path',
        mimeType: resourceMimeType
    }, async (uri, variables) => {
        try {
            return await resourceHandler(mcpUnity, uri, variables, logger);
        }
        catch (error) {
            logger.error(`Error handling resource ${resourceName}: ${error}`);
            throw error;
        }
    });
}
/**
 * Handles requests for GameObject information from Unity
 *
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param uri The requested resource URI
 * @param variables Variables extracted from the URI template
 * @param logger The logger instance for diagnostic information
 * @returns A promise that resolves to the GameObject data
 * @throws McpUnityError if the request to Unity fails
 */
async function resourceHandler(mcpUnity, uri, variables, logger) {
    // Extract and convert the parameter from the template variables
    const id = decodeURIComponent(variables["id"]);
    // Send request to Unity
    const response = await mcpUnity.sendRequest({
        method: resourceName,
        params: {
            objectPathId: id
        }
    });
    if (!response.success) {
        throw new McpUnityError(ErrorType.RESOURCE_FETCH, response.message || 'Failed to fetch GameObject from Unity');
    }
    return {
        contents: [{
                uri: `unity://gameobject/${id}`,
                mimeType: resourceMimeType,
                text: JSON.stringify(response, null, 2)
            }]
    };
}
/**
 * Get a list of all GameObjects in the scene
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param logger The logger instance
 * @param resourceMimeType The MIME type for the resource
 * @returns A promise that resolves to a list of GameObject resources
 */
async function listGameObjects(mcpUnity, logger, resourceMimeType) {
    const hierarchyResponse = await mcpUnity.sendRequest({
        method: hierarchyResourceName,
        params: {}
    });
    if (!hierarchyResponse.success) {
        logger.error(`Failed to fetch hierarchy: ${hierarchyResponse.message}`);
        throw new Error(hierarchyResponse.message || 'Failed to fetch hierarchy');
    }
    // Process the hierarchy to create a list of GameObject references
    const gameObjects = processHierarchyToGameObjectList(hierarchyResponse.hierarchy || []);
    logger.info(`[getGameObjectResource] Fetched hierarchy with ${gameObjects.length} GameObjects ${hierarchyResponse.hierarchy}`);
    // Create resources array with both instance ID and path URIs
    const resources = [];
    // Add resources for each GameObject
    gameObjects.forEach(obj => {
        // Add resource with instance ID URI
        resources.push({
            uri: `unity://gameobject/${obj.instanceId}`,
            name: obj.name,
            description: `GameObject with instance ID ${obj.instanceId} at path: ${obj.path}`,
            mimeType: resourceMimeType
        });
        // Add resource with path URI if path exists
        if (obj.path) {
            resources.push({
                uri: `unity://gameobject/${encodeURIComponent(obj.path)}`,
                name: obj.name,
                description: `GameObject with instance ID ${obj.instanceId} at path: ${obj.path}`,
                mimeType: resourceMimeType
            });
        }
    });
    return { resources };
}
/**
 * Process the hierarchy data to create a list of GameObject references
 * @param hierarchyData The hierarchy data from Unity
 * @returns An array of GameObject references with their instance IDs and paths
 */
function processHierarchyToGameObjectList(hierarchyData) {
    const gameObjects = [];
    // Helper function to traverse the hierarchy recursively
    function traverseHierarchy(node, path = '') {
        if (!node)
            return;
        // Current path is parent path + node name
        const currentPath = path ? `${path}/${node.name}` : node.name;
        // Add this GameObject to the list
        gameObjects.push({
            instanceId: node.instanceId,
            name: node.name,
            path: currentPath,
            active: node.active,
            uri: `unity://gameobject/${node.instanceId}`
        });
        // Process children recursively
        if (node.children && Array.isArray(node.children)) {
            for (const child of node.children) {
                traverseHierarchy(child, currentPath);
            }
        }
    }
    // Start traversal with each root GameObject
    if (Array.isArray(hierarchyData)) {
        for (const rootNode of hierarchyData) {
            traverseHierarchy(rootNode);
        }
    }
    return gameObjects;
}
