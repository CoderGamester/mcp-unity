import * as z from "zod";
import { Logger } from "../utils/logger.js";
import { McpUnity } from "../unity/mcpUnity.js";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { McpUnityError, ErrorType } from "../utils/errors.js";
import { CallToolResult } from "@modelcontextprotocol/sdk/types.js";

// Constants for the tool
const toolName = "get_gameobject";
const toolDescription =
  "Retrieves detailed information about a specific GameObject by instance ID, name, or hierarchical path (e.g., 'Parent/Child/MyObject'). Returns all component properties including Transform position, rotation, scale, and more.";
const paramsSchema = z.object({
  idOrName: z
    .string()
    .describe(
      "The instance ID (integer), name, or hierarchical path of the GameObject to retrieve. Use hierarchical paths like 'Canvas/Panel/Button' for nested objects."
    ),
});

/**
 * Creates and registers the Get GameObject tool with the MCP server
 * This tool allows retrieving detailed information about GameObjects in Unity scenes
 *
 * @param server The MCP server instance to register with
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param logger The logger instance for diagnostic information
 */
export function registerGetGameObjectTool(
  server: McpServer,
  mcpUnity: McpUnity,
  logger: Logger
) {
  logger.info(`Registering tool: ${toolName}`);

  // Register this tool with the MCP server
  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
    async (params: z.infer<typeof paramsSchema>) => {
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
 * Handles requests for GameObject information from Unity
 *
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param params The parameters for the tool
 * @returns A promise that resolves to the tool execution result
 * @throws McpUnityError if the request to Unity fails
 */
async function toolHandler(
  mcpUnity: McpUnity,
  params: z.infer<typeof paramsSchema>
): Promise<CallToolResult> {
  const { idOrName } = params;

  // Send request to Unity
  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: {
      idOrName: idOrName,
    },
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || "Failed to fetch GameObject from Unity"
    );
  }

  return {
    content: [
      {
        type: "text",
        text: JSON.stringify(response, null, 2),
      },
    ],
  };
}


