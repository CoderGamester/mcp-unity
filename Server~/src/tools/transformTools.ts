import * as z from 'zod';
import { McpUnity } from '../unity/mcpUnity.js';
import { Logger } from '../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

// Common Vector3 schema
const vector3Schema = z.object({
  x: z.number().describe('X component'),
  y: z.number().describe('Y component'),
  z: z.number().describe('Z component')
});

// ============================================================================
// move_gameobject Tool
// ============================================================================

const moveToolName = 'move_gameobject';
const moveToolDescription = 'Moves a GameObject to a new position. Supports world/local space and absolute/relative positioning.';
const moveParamsSchema = z.object({
  instanceId: z.number().optional().describe('The instance ID of the GameObject to move'),
  objectPath: z.string().optional().describe('The path of the GameObject in the hierarchy (alternative to instanceId)'),
  position: vector3Schema.describe('The target position'),
  space: z.enum(['world', 'local']).default('world').describe('Coordinate space: "world" or "local"'),
  relative: z.boolean().default(false).describe('If true, adds to current position instead of setting absolute position')
});

/**
 * Registers the move_gameobject tool with the MCP server
 */
export function registerMoveGameObjectTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${moveToolName}`);

  server.tool(
    moveToolName,
    moveToolDescription,
    moveParamsSchema.shape,
    async (params: z.infer<typeof moveParamsSchema>) => {
      try {
        logger.info(`Executing tool: ${moveToolName}`, params);
        const result = await moveToolHandler(mcpUnity, params);
        logger.info(`Tool execution successful: ${moveToolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${moveToolName}`, error);
        throw error;
      }
    }
  );
}

async function moveToolHandler(mcpUnity: McpUnity, params: z.infer<typeof moveParamsSchema>): Promise<CallToolResult> {
  validateGameObjectIdentifier(params);

  const response = await mcpUnity.sendRequest({
    method: moveToolName,
    params: {
      instanceId: params.instanceId,
      objectPath: params.objectPath,
      position: params.position,
      space: params.space,
      relative: params.relative
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to move GameObject'
    );
  }

  return {
    content: [{
      type: response.type,
      text: response.message || 'GameObject moved successfully'
    }]
  };
}

// ============================================================================
// rotate_gameobject Tool
// ============================================================================

const rotateToolName = 'rotate_gameobject';
const rotateToolDescription = 'Rotates a GameObject using Euler angles. Supports world/local space and absolute/relative rotation.';
const rotateParamsSchema = z.object({
  instanceId: z.number().optional().describe('The instance ID of the GameObject to rotate'),
  objectPath: z.string().optional().describe('The path of the GameObject in the hierarchy (alternative to instanceId)'),
  rotation: vector3Schema.describe('The rotation in Euler angles (degrees)'),
  space: z.enum(['world', 'local']).default('world').describe('Coordinate space: "world" or "local"'),
  relative: z.boolean().default(false).describe('If true, adds to current rotation instead of setting absolute rotation')
});

/**
 * Registers the rotate_gameobject tool with the MCP server
 */
export function registerRotateGameObjectTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${rotateToolName}`);

  server.tool(
    rotateToolName,
    rotateToolDescription,
    rotateParamsSchema.shape,
    async (params: z.infer<typeof rotateParamsSchema>) => {
      try {
        logger.info(`Executing tool: ${rotateToolName}`, params);
        const result = await rotateToolHandler(mcpUnity, params);
        logger.info(`Tool execution successful: ${rotateToolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${rotateToolName}`, error);
        throw error;
      }
    }
  );
}

async function rotateToolHandler(mcpUnity: McpUnity, params: z.infer<typeof rotateParamsSchema>): Promise<CallToolResult> {
  validateGameObjectIdentifier(params);

  const response = await mcpUnity.sendRequest({
    method: rotateToolName,
    params: {
      instanceId: params.instanceId,
      objectPath: params.objectPath,
      rotation: params.rotation,
      space: params.space,
      relative: params.relative
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to rotate GameObject'
    );
  }

  return {
    content: [{
      type: response.type,
      text: response.message || 'GameObject rotated successfully'
    }]
  };
}

// ============================================================================
// scale_gameobject Tool
// ============================================================================

const scaleToolName = 'scale_gameobject';
const scaleToolDescription = 'Scales a GameObject. Supports absolute and relative (multiplicative) scaling.';
const scaleParamsSchema = z.object({
  instanceId: z.number().optional().describe('The instance ID of the GameObject to scale'),
  objectPath: z.string().optional().describe('The path of the GameObject in the hierarchy (alternative to instanceId)'),
  scale: vector3Schema.describe('The scale values'),
  relative: z.boolean().default(false).describe('If true, multiplies current scale instead of setting absolute scale')
});

/**
 * Registers the scale_gameobject tool with the MCP server
 */
export function registerScaleGameObjectTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${scaleToolName}`);

  server.tool(
    scaleToolName,
    scaleToolDescription,
    scaleParamsSchema.shape,
    async (params: z.infer<typeof scaleParamsSchema>) => {
      try {
        logger.info(`Executing tool: ${scaleToolName}`, params);
        const result = await scaleToolHandler(mcpUnity, params);
        logger.info(`Tool execution successful: ${scaleToolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${scaleToolName}`, error);
        throw error;
      }
    }
  );
}

async function scaleToolHandler(mcpUnity: McpUnity, params: z.infer<typeof scaleParamsSchema>): Promise<CallToolResult> {
  validateGameObjectIdentifier(params);

  const response = await mcpUnity.sendRequest({
    method: scaleToolName,
    params: {
      instanceId: params.instanceId,
      objectPath: params.objectPath,
      scale: params.scale,
      relative: params.relative
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to scale GameObject'
    );
  }

  return {
    content: [{
      type: response.type,
      text: response.message || 'GameObject scaled successfully'
    }]
  };
}

// ============================================================================
// set_transform Tool
// ============================================================================

const setTransformToolName = 'set_transform';
const setTransformToolDescription = 'Sets a GameObject\'s transform (position, rotation, scale) in one operation. All transform properties are optional.';
const setTransformParamsSchema = z.object({
  instanceId: z.number().optional().describe('The instance ID of the GameObject'),
  objectPath: z.string().optional().describe('The path of the GameObject in the hierarchy (alternative to instanceId)'),
  position: vector3Schema.optional().describe('The position to set'),
  rotation: vector3Schema.optional().describe('The rotation in Euler angles (degrees)'),
  scale: vector3Schema.optional().describe('The scale to set'),
  space: z.enum(['world', 'local']).default('world').describe('Coordinate space for position and rotation: "world" or "local"')
}).refine(
  data => data.position !== undefined || data.rotation !== undefined || data.scale !== undefined,
  { message: 'At least one of position, rotation, or scale must be provided' }
);

/**
 * Registers the set_transform tool with the MCP server
 */
export function registerSetTransformTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${setTransformToolName}`);

  server.tool(
    setTransformToolName,
    setTransformToolDescription,
    // Use base shape without refine for MCP schema registration
    z.object({
      instanceId: z.number().optional().describe('The instance ID of the GameObject'),
      objectPath: z.string().optional().describe('The path of the GameObject in the hierarchy (alternative to instanceId)'),
      position: vector3Schema.optional().describe('The position to set'),
      rotation: vector3Schema.optional().describe('The rotation in Euler angles (degrees)'),
      scale: vector3Schema.optional().describe('The scale to set'),
      space: z.enum(['world', 'local']).default('world').describe('Coordinate space for position and rotation: "world" or "local"')
    }).shape,
    async (params: any) => {
      try {
        logger.info(`Executing tool: ${setTransformToolName}`, params);
        const result = await setTransformToolHandler(mcpUnity, params);
        logger.info(`Tool execution successful: ${setTransformToolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${setTransformToolName}`, error);
        throw error;
      }
    }
  );
}

async function setTransformToolHandler(mcpUnity: McpUnity, params: any): Promise<CallToolResult> {
  validateGameObjectIdentifier(params);

  // Validate that at least one transform property is provided
  if (!params.position && !params.rotation && !params.scale) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      'At least one of position, rotation, or scale must be provided'
    );
  }

  const response = await mcpUnity.sendRequest({
    method: setTransformToolName,
    params: {
      instanceId: params.instanceId,
      objectPath: params.objectPath,
      position: params.position,
      rotation: params.rotation,
      scale: params.scale,
      space: params.space
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to set transform'
    );
  }

  return {
    content: [{
      type: response.type,
      text: response.message || 'Transform updated successfully'
    }]
  };
}

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Validates that either instanceId or objectPath is provided
 */
function validateGameObjectIdentifier(params: { instanceId?: number; objectPath?: string }) {
  if ((params.instanceId === undefined || params.instanceId === null) &&
      (!params.objectPath || params.objectPath.trim() === '')) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Either 'instanceId' or 'objectPath' must be provided"
    );
  }
}

/**
 * Registers all transform tools with the MCP server
 */
export function registerTransformTools(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  registerMoveGameObjectTool(server, mcpUnity, logger);
  registerRotateGameObjectTool(server, mcpUnity, logger);
  registerScaleGameObjectTool(server, mcpUnity, logger);
  registerSetTransformTool(server, mcpUnity, logger);
}
