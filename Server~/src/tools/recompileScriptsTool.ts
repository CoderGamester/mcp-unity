import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

// Constants for the tool
const toolName = 'recompile_scripts';
const toolDescription = 'Recompiles all scripts in the Unity project.';
const paramsSchema = z.object({
  returnWithLogs: z.boolean().optional().default(true).describe('Whether to return compilation logs'),
  logsLimit: z.number().int().min(0).max(1000).optional().default(100).describe('Maximum number of compilation logs to return')
});

/**
 * Creates and registers the Recompile Scripts tool with the MCP server
 * This tool allows recompiling all scripts in the Unity project
 *
 * @param server The MCP server instance to register with
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param logger The logger instance for diagnostic information
 */
export function registerRecompileScriptsTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
 * Handles recompile scripts tool requests
 *
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param params The parameters for the tool
 * @returns A promise that resolves to the tool execution result
 * @throws McpUnityError if the request to Unity fails
 */
async function toolHandler(mcpUnity: McpUnity, params: z.infer<typeof paramsSchema>): Promise<CallToolResult> {
  // Validate and prepare parameters
  const returnWithLogs = params.returnWithLogs ?? true;
  const logsLimit = Math.max(0, Math.min(1000, params.logsLimit || 100));

  // Send to Unity with validated parameters
  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: {
      returnWithLogs,
      logsLimit
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || `Failed to recompile scripts`
    );
  }

  return {
    content: [
      {
        type: 'text',
        text: response.message
      },
      {
        type: 'text',
        text: JSON.stringify({
          logs: response.logs
        }, null, 2)
      }
    ]
  };
}
