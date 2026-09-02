import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

// Constants for the tool
const toolName = 'add_package';
const toolDescription = 'Adds packages into the Unity Package Manager when package installation is explicitly enabled in Unity';
const paramsSchema = z.object({
  source: z.string().describe('The source to use (registry, github, or disk) to add the package'),
  packageName: z.string().optional().describe('The package name to add from Unity registry (e.g. com.unity.textmeshpro)'),
  version: z.string().optional().describe('The version to use for registry packages (optional)'),
  repositoryUrl: z.string().optional().describe('The GitHub repository URL (e.g. https://github.com/username/repo.git)'),
  branch: z.string().optional().describe('The branch to use for GitHub packages (optional)'),
  path: z.string().optional().describe('The path to use (folder path for disk method or subfolder for GitHub)')
});

/**
 * Creates and registers the Add Package tool with the MCP server
 * This tool allows adding packages to the Unity Package Manager
 * 
 * @param server The MCP server instance to register with
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param logger The logger instance for diagnostic information
 */
export function registerAddPackageTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);
      
  // Register this tool with the MCP server
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

/**
 * Handles add package tool requests
 * 
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param params The parameters for the tool
 * @returns A promise that resolves to the tool execution result
 * @throws McpUnityError if the request to Unity fails
 */
async function toolHandler(mcpUnity: McpUnity, params: any): Promise<CallToolResult> {
  const { source, packageName, version, repositoryUrl, branch, path } = params;
        
  // Validate required parameters based on method
  if (source === 'registry' && !packageName) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      'Required parameter "packageName" not provided for registry source'
    );
  } else if (source === 'github' && !repositoryUrl) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      'Required parameter "repositoryUrl" not provided for github source'
    );
  } else if (source === 'disk' && !path) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      'Required parameter "path" not provided for disk source'
    );
  }
  
  // Send to Unity
  const response = await mcpUnity.sendRequest({
    method: toolName,
    params
  });
  
  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || `Failed to manage package with source: ${source}`
    );
  }
  
  return {
    content: [{
      type: response.type,
      text: response.message
    }]
  };
}
