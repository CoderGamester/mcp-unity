import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

// Color schema for material properties
const colorSchema = z.object({
  r: z.number().min(0).max(1).describe('Red component (0-1)'),
  g: z.number().min(0).max(1).describe('Green component (0-1)'),
  b: z.number().min(0).max(1).describe('Blue component (0-1)'),
  a: z.number().min(0).max(1).optional().default(1).describe('Alpha component (0-1)')
});

// Vector4 schema for material properties
const vector4Schema = z.object({
  x: z.number().describe('X component'),
  y: z.number().describe('Y component'),
  z: z.number().describe('Z component'),
  w: z.number().optional().default(0).describe('W component')
});

// ============================================================================
// CREATE MATERIAL TOOL
// ============================================================================

const createMaterialToolName = 'create_material';
const createMaterialToolDescription = 'Creates a new material with the specified shader and saves it to the project. Use the "color" parameter for easy color setting.';
const createMaterialParamsSchema = z.object({
  name: z.string().describe('The name of the material'),
  shader: z.string().optional().describe('The shader name. Auto-detects render pipeline if not specified (URP: "Universal Render Pipeline/Lit", Built-in: "Standard")'),
  savePath: z.string().describe('The asset path to save the material (e.g., "Assets/Materials/MyMaterial.mat")'),
  color: colorSchema.optional().describe('The base color of the material. Auto-detects correct property name (_BaseColor for URP, _Color for Standard)'),
  properties: z.record(z.any()).optional().describe('Optional initial property values as key-value pairs (advanced usage)')
});

/**
 * Registers the Create Material tool with the MCP server
 */
export function registerCreateMaterialTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${createMaterialToolName}`);

  server.tool(
    createMaterialToolName,
    createMaterialToolDescription,
    createMaterialParamsSchema.shape,
    async (params: any) => {
      try {
        logger.info(`Executing tool: ${createMaterialToolName}`, params);
        const result = await createMaterialHandler(mcpUnity, params);
        logger.info(`Tool execution successful: ${createMaterialToolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${createMaterialToolName}`, error);
        throw error;
      }
    }
  );
}

async function createMaterialHandler(mcpUnity: McpUnity, params: any): Promise<CallToolResult> {
  if (!params.name) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Required parameter 'name' must be provided"
    );
  }

  if (!params.savePath) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Required parameter 'savePath' must be provided"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: createMaterialToolName,
    params: {
      name: params.name,
      shader: params.shader, // Let Unity auto-detect if not specified
      savePath: params.savePath,
      color: params.color,   // Auto-maps to correct shader property (_BaseColor, _Color, etc.)
      properties: params.properties
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to create material'
    );
  }

  return {
    content: [{
      type: response.type,
      text: response.message || `Successfully created material '${params.name}'`
    }]
  };
}

// ============================================================================
// ASSIGN MATERIAL TOOL
// ============================================================================

const assignMaterialToolName = 'assign_material';
const assignMaterialToolDescription = 'Assigns a material to a GameObject\'s Renderer component at a specific material slot';
const assignMaterialParamsSchema = z.object({
  instanceId: z.number().optional().describe('The instance ID of the GameObject'),
  objectPath: z.string().optional().describe('The path of the GameObject in the hierarchy (alternative to instanceId)'),
  materialPath: z.string().describe('The asset path to the material (e.g., "Assets/Materials/MyMaterial.mat")'),
  slot: z.number().int().min(0).optional().default(0).describe('The material slot index (default: 0)')
});

/**
 * Registers the Assign Material tool with the MCP server
 */
export function registerAssignMaterialTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${assignMaterialToolName}`);

  server.tool(
    assignMaterialToolName,
    assignMaterialToolDescription,
    assignMaterialParamsSchema.shape,
    async (params: any) => {
      try {
        logger.info(`Executing tool: ${assignMaterialToolName}`, params);
        const result = await assignMaterialHandler(mcpUnity, params);
        logger.info(`Tool execution successful: ${assignMaterialToolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${assignMaterialToolName}`, error);
        throw error;
      }
    }
  );
}

async function assignMaterialHandler(mcpUnity: McpUnity, params: any): Promise<CallToolResult> {
  if ((params.instanceId === undefined || params.instanceId === null) &&
      (!params.objectPath || params.objectPath.trim() === '')) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Either 'instanceId' or 'objectPath' must be provided"
    );
  }

  if (!params.materialPath) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Required parameter 'materialPath' must be provided"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: assignMaterialToolName,
    params: {
      instanceId: params.instanceId,
      objectPath: params.objectPath,
      materialPath: params.materialPath,
      slot: params.slot ?? 0
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to assign material'
    );
  }

  return {
    content: [{
      type: response.type,
      text: response.message || `Successfully assigned material`
    }]
  };
}

// ============================================================================
// MODIFY MATERIAL TOOL
// ============================================================================

const modifyMaterialToolName = 'modify_material';
const modifyMaterialToolDescription = 'Modifies properties of an existing material. Supports colors (e.g., _Color), floats (e.g., _Metallic), and textures (e.g., _MainTex path)';
const modifyMaterialParamsSchema = z.object({
  materialPath: z.string().describe('The asset path to the material (e.g., "Assets/Materials/MyMaterial.mat")'),
  properties: z.record(z.any()).describe('Property name to value mapping. Colors: {r,g,b,a}, Vectors: {x,y,z,w}, Floats: number, Textures: asset path string')
});

/**
 * Registers the Modify Material tool with the MCP server
 */
export function registerModifyMaterialTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${modifyMaterialToolName}`);

  server.tool(
    modifyMaterialToolName,
    modifyMaterialToolDescription,
    modifyMaterialParamsSchema.shape,
    async (params: any) => {
      try {
        logger.info(`Executing tool: ${modifyMaterialToolName}`, params);
        const result = await modifyMaterialHandler(mcpUnity, params);
        logger.info(`Tool execution successful: ${modifyMaterialToolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${modifyMaterialToolName}`, error);
        throw error;
      }
    }
  );
}

async function modifyMaterialHandler(mcpUnity: McpUnity, params: any): Promise<CallToolResult> {
  if (!params.materialPath) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Required parameter 'materialPath' must be provided"
    );
  }

  if (!params.properties || Object.keys(params.properties).length === 0) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Required parameter 'properties' must be provided and contain at least one property"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: modifyMaterialToolName,
    params: {
      materialPath: params.materialPath,
      properties: params.properties
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to modify material'
    );
  }

  return {
    content: [{
      type: response.type,
      text: response.message || `Successfully modified material`
    }]
  };
}

// ============================================================================
// GET MATERIAL INFO TOOL
// ============================================================================

const getMaterialInfoToolName = 'get_material_info';
const getMaterialInfoToolDescription = 'Gets detailed information about a material including its shader and all properties with current values';
const getMaterialInfoParamsSchema = z.object({
  materialPath: z.string().describe('The asset path to the material (e.g., "Assets/Materials/MyMaterial.mat")')
});

/**
 * Registers the Get Material Info tool with the MCP server
 */
export function registerGetMaterialInfoTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${getMaterialInfoToolName}`);

  server.tool(
    getMaterialInfoToolName,
    getMaterialInfoToolDescription,
    getMaterialInfoParamsSchema.shape,
    async (params: any) => {
      try {
        logger.info(`Executing tool: ${getMaterialInfoToolName}`, params);
        const result = await getMaterialInfoHandler(mcpUnity, params);
        logger.info(`Tool execution successful: ${getMaterialInfoToolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${getMaterialInfoToolName}`, error);
        throw error;
      }
    }
  );
}

async function getMaterialInfoHandler(mcpUnity: McpUnity, params: any): Promise<CallToolResult> {
  if (!params.materialPath) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Required parameter 'materialPath' must be provided"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: getMaterialInfoToolName,
    params: {
      materialPath: params.materialPath
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to get material info'
    );
  }

  // Format the response with material details
  let text = `Material: ${response.materialName}\n`;
  text += `Shader: ${response.shaderName}\n`;
  text += `Render Queue: ${response.renderQueue} (${response.renderQueueCategory})\n`;
  text += `Instancing: ${response.enableInstancing}\n`;
  text += `Pass Count: ${response.passCount}\n\n`;
  text += `Properties:\n`;

  if (response.properties && Array.isArray(response.properties)) {
    for (const prop of response.properties) {
      let valueStr = '';
      if (prop.value === null || prop.value === undefined) {
        valueStr = 'null';
      } else if (typeof prop.value === 'object') {
        valueStr = JSON.stringify(prop.value);
      } else {
        valueStr = String(prop.value);
      }

      text += `  ${prop.name} (${prop.type}): ${valueStr}`;
      if (prop.description) {
        text += ` - ${prop.description}`;
      }
      text += '\n';
    }
  }

  return {
    content: [{
      type: 'text',
      text: text
    }],
    data: {
      materialName: response.materialName,
      materialPath: response.materialPath,
      shaderName: response.shaderName,
      renderQueue: response.renderQueue,
      renderQueueCategory: response.renderQueueCategory,
      enableInstancing: response.enableInstancing,
      doubleSidedGI: response.doubleSidedGI,
      passCount: response.passCount,
      properties: response.properties
    }
  };
}
