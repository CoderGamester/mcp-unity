import * as z from 'zod';
import { McpUnity } from '../unity/mcpUnity.js';
import { Logger } from '../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'batch_execute';
const toolDescription = `Executes multiple tool operations in a single batch request.
Reduces network round-trips and enables atomic operations with rollback support.
Performance improvement: 10-100x for repetitive operations.`;

const operationSchema = z.object({
  tool: z.string().describe('The name of the tool to execute'),
  params: z.record(z.any()).optional().default({}).describe('Parameters to pass to the tool'),
  id: z.string().optional().describe('Optional identifier for this operation (for tracking in results)')
});

const paramsSchema = z.object({
  operations: z.array(operationSchema)
    .min(1, 'At least one operation is required')
    .max(100, 'Maximum of 100 operations allowed per batch')
    .describe('Array of operations to execute sequentially'),
  stopOnError: z.boolean()
    .default(true)
    .describe('If true, stops execution on the first error. Default: true'),
  atomic: z.boolean()
    .default(false)
    .describe('If true, rolls back all operations if any fails (uses Unity Undo system). Default: false')
});

/**
 * Result of a single operation in the batch
 */
interface OperationResult {
  index: number;
  id: string;
  success: boolean;
  result?: any;
  error?: string;
}

/**
 * Summary of batch execution
 */
interface BatchSummary {
  total: number;
  succeeded: number;
  failed: number;
  executed: number;
}

/**
 * Response from the batch execute tool
 */
interface BatchExecuteResponse {
  success: boolean;
  type: string;
  message: string;
  results: OperationResult[];
  summary: BatchSummary;
}

/**
 * Creates and registers the Batch Execute tool with the MCP server
 */
export function registerBatchExecuteTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
    async (params: any) => {
      try {
        logger.info(`Executing tool: ${toolName}`, {
          operationCount: params.operations?.length,
          stopOnError: params.stopOnError,
          atomic: params.atomic
        });
        const result = await batchExecuteHandler(mcpUnity, params, logger);
        logger.info(`Tool execution completed: ${toolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${toolName}`, error);
        throw error;
      }
    }
  );
}

async function batchExecuteHandler(
  mcpUnity: McpUnity,
  params: z.infer<typeof paramsSchema>,
  logger: Logger
): Promise<CallToolResult> {
  // Validate operations array
  if (!params.operations || params.operations.length === 0) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "The 'operations' array is required and must contain at least one operation"
    );
  }

  if (params.operations.length > 100) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Maximum of 100 operations allowed per batch"
    );
  }

  // Validate no nested batch_execute operations
  for (const op of params.operations) {
    if (op.tool === 'batch_execute') {
      throw new McpUnityError(
        ErrorType.VALIDATION,
        "Cannot nest batch_execute operations"
      );
    }
  }

  logger.info(`Sending batch with ${params.operations.length} operations to Unity`);

  // Send the batch request to Unity
  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: {
      operations: params.operations.map((op, index) => ({
        tool: op.tool,
        params: op.params ?? {},
        id: op.id ?? index.toString()
      })),
      stopOnError: params.stopOnError ?? true,
      atomic: params.atomic ?? false
    }
  }) as BatchExecuteResponse;

  // Format the response message
  let resultText = response.message || 'Batch execution completed';

  // Add summary details
  if (response.summary) {
    resultText += `\n\nSummary: ${response.summary.succeeded}/${response.summary.total} succeeded`;
    if (response.summary.failed > 0) {
      resultText += `, ${response.summary.failed} failed`;
    }
  }

  // Add individual results if there are failures or detailed info
  if (response.results && response.results.length > 0) {
    const failures = response.results.filter(r => !r.success);
    if (failures.length > 0) {
      resultText += '\n\nFailed operations:';
      for (const failure of failures) {
        resultText += `\n  - [${failure.id}] ${failure.error || 'Unknown error'}`;
      }
    }
  }

  // Determine if we should throw an error or return success
  if (!response.success && params.stopOnError) {
    // When stopOnError is true and we failed, throw to signal the error clearly
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      resultText
    );
  }

  return {
    content: [{
      type: 'text',
      text: resultText
    }]
  };
}
