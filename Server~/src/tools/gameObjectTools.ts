import * as z from 'zod';
import { McpUnity } from '../unity/mcpUnity.js';
import { Logger } from '../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

// ============================================================================
// Duplicate GameObject Tool
// ============================================================================

const duplicateToolName = 'duplicate_gameobject';
const duplicateToolDescription = 'Duplicates a GameObject in the Unity scene. Can create multiple copies and optionally rename or reparent them.';
const duplicateParamsSchema = z.object({
  instanceId: z.number().optional().describe('The instance ID of the GameObject to duplicate'),
  objectPath: z.string().optional().describe('The path of the GameObject in the hierarchy to duplicate (alternative to instanceId)'),
  newName: z.string().optional().describe('New name for the duplicated GameObject(s). If count > 1, numbers will be appended.'),
  newParent: z.string().optional().describe('Path to the new parent GameObject. If not specified, uses the same parent as the original.'),
  newParentId: z.number().optional().describe('Instance ID of the new parent GameObject (alternative to newParent path).'),
  count: z.number().int().min(1).max(100).default(1).describe('Number of copies to create. Default: 1, Max: 100'),
});

/**
 * Creates and registers the Duplicate GameObject tool with the MCP server
 */
export function registerDuplicateGameObjectTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${duplicateToolName}`);

  server.tool(
    duplicateToolName,
    duplicateToolDescription,
    duplicateParamsSchema.shape,
    async (params: any) => {
      try {
        logger.info(`Executing tool: ${duplicateToolName}`, params);
        const result = await duplicateHandler(mcpUnity, params);
        logger.info(`Tool execution successful: ${duplicateToolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${duplicateToolName}`, error);
        throw error;
      }
    }
  );
}

async function duplicateHandler(mcpUnity: McpUnity, params: any): Promise<CallToolResult> {
  // Validate parameters - require either instanceId or objectPath
  if ((params.instanceId === undefined || params.instanceId === null) &&
      (!params.objectPath || params.objectPath.trim() === '')) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Either 'instanceId' or 'objectPath' must be provided"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: duplicateToolName,
    params: {
      instanceId: params.instanceId,
      objectPath: params.objectPath,
      newName: params.newName,
      newParent: params.newParent,
      newParentId: params.newParentId,
      count: params.count ?? 1,
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to duplicate the GameObject'
    );
  }

  return {
    content: [{
      type: response.type || 'text',
      text: response.message || 'Successfully duplicated the GameObject'
    }]
  };
}

// ============================================================================
// Delete GameObject Tool
// ============================================================================

const deleteToolName = 'delete_gameobject';
const deleteToolDescription = 'Deletes a GameObject from the Unity scene. By default, also deletes all children.';
const deleteParamsSchema = z.object({
  instanceId: z.number().optional().describe('The instance ID of the GameObject to delete'),
  objectPath: z.string().optional().describe('The path of the GameObject in the hierarchy to delete (alternative to instanceId)'),
  includeChildren: z.boolean().default(true).describe('If true (default), deletes all children. If false, children are moved to the deleted object\'s parent.'),
});

/**
 * Creates and registers the Delete GameObject tool with the MCP server
 */
export function registerDeleteGameObjectTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${deleteToolName}`);

  server.tool(
    deleteToolName,
    deleteToolDescription,
    deleteParamsSchema.shape,
    async (params: any) => {
      try {
        logger.info(`Executing tool: ${deleteToolName}`, params);
        const result = await deleteHandler(mcpUnity, params);
        logger.info(`Tool execution successful: ${deleteToolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${deleteToolName}`, error);
        throw error;
      }
    }
  );
}

async function deleteHandler(mcpUnity: McpUnity, params: any): Promise<CallToolResult> {
  // Validate parameters - require either instanceId or objectPath
  if ((params.instanceId === undefined || params.instanceId === null) &&
      (!params.objectPath || params.objectPath.trim() === '')) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Either 'instanceId' or 'objectPath' must be provided"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: deleteToolName,
    params: {
      instanceId: params.instanceId,
      objectPath: params.objectPath,
      includeChildren: params.includeChildren ?? true,
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to delete the GameObject'
    );
  }

  return {
    content: [{
      type: response.type || 'text',
      text: response.message || 'Successfully deleted the GameObject'
    }]
  };
}

// ============================================================================
// Reparent GameObject Tool
// ============================================================================

const reparentToolName = 'reparent_gameobject';
const reparentToolDescription = 'Changes the parent of a GameObject. Can move to a new parent or to the root level (null parent).';
const reparentParamsSchema = z.object({
  instanceId: z.number().optional().describe('The instance ID of the GameObject to reparent'),
  objectPath: z.string().optional().describe('The path of the GameObject in the hierarchy to reparent (alternative to instanceId)'),
  newParent: z.string().nullable().optional().describe('Path to the new parent GameObject. Use null to move to root level.'),
  newParentId: z.number().nullable().optional().describe('Instance ID of the new parent GameObject. Use null to move to root level.'),
  worldPositionStays: z.boolean().default(true).describe('If true (default), the world position is preserved. If false, local position is reset to zero.'),
});

/**
 * Creates and registers the Reparent GameObject tool with the MCP server
 */
export function registerReparentGameObjectTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${reparentToolName}`);

  server.tool(
    reparentToolName,
    reparentToolDescription,
    reparentParamsSchema.shape,
    async (params: any) => {
      try {
        logger.info(`Executing tool: ${reparentToolName}`, params);
        const result = await reparentHandler(mcpUnity, params);
        logger.info(`Tool execution successful: ${reparentToolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${reparentToolName}`, error);
        throw error;
      }
    }
  );
}

async function reparentHandler(mcpUnity: McpUnity, params: any): Promise<CallToolResult> {
  // Validate parameters - require either instanceId or objectPath
  if ((params.instanceId === undefined || params.instanceId === null) &&
      (!params.objectPath || params.objectPath.trim() === '')) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Either 'instanceId' or 'objectPath' must be provided"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: reparentToolName,
    params: {
      instanceId: params.instanceId,
      objectPath: params.objectPath,
      newParent: params.newParent,
      newParentId: params.newParentId,
      worldPositionStays: params.worldPositionStays ?? true,
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to reparent the GameObject'
    );
  }

  return {
    content: [{
      type: response.type || 'text',
      text: response.message || 'Successfully reparented the GameObject'
    }]
  };
}
