import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { McpUnity } from "../unity/mcpUnity.js";
import { Logger } from "../utils/logger.js";
import * as z from "zod";
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = "create_scene";
const toolDescription = "Creates a new scene and saves it to the specified path";

const paramsSchema = z.object({
  sceneName: z
    .string()
    .describe("The name of the scene to create (without extension)"),
  folderPath: z
    .string()
    .optional()
    .describe("The folder path under 'Assets' to save into (default: Assets)"),
  addToBuildSettings: z
    .boolean()
    .optional()
    .describe("Whether to add the scene to Build Settings"),
  makeActive: z
    .boolean()
    .optional()
    .describe("Whether to open/make the new scene active after creating it"),
});

// Type inferred from the schema for type-safe handler params
type CreateSceneParams = z.infer<typeof paramsSchema>;

export function registerCreateSceneTool(
  server: McpServer,
  mcpUnity: McpUnity,
  logger: Logger
) {
  logger.info(`Registering tool: ${toolName}`);

  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
    async (params: CreateSceneParams): Promise<CallToolResult> => {
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

async function toolHandler(mcpUnity: McpUnity, params: CreateSceneParams): Promise<CallToolResult> {
  if (!params.sceneName) {
    throw new Error("'sceneName' must be provided");
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params,
  });

  if (!response.success) {
    throw new Error(response.message || "Failed to create scene");
  }

  return {
    content: [
      {
        type: response.type || "text",
        text: response.message || "Successfully created scene",
      },
    ],
    data: {
      scenePath: response.scenePath,
    },
  };
}