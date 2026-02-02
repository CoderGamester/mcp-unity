import { jest, describe, it, expect, beforeEach } from '@jest/globals';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { registerBatchExecuteTool } from '../tools/batchExecuteTool.js';

// Mock the McpUnity class
const mockSendRequest = jest.fn();
const mockMcpUnity = {
  sendRequest: mockSendRequest
};

// Mock the Logger
const mockLogger = {
  info: jest.fn(),
  debug: jest.fn(),
  warn: jest.fn(),
  error: jest.fn()
};

// Mock the McpServer
const mockServerTool = jest.fn();
const mockServer = {
  tool: mockServerTool
};

describe('Batch Execute Tool', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('registerBatchExecuteTool', () => {
    it('should register the batch_execute tool with the server', () => {
      registerBatchExecuteTool(mockServer as any, mockMcpUnity as any, mockLogger as any);

      expect(mockServerTool).toHaveBeenCalledTimes(1);
      expect(mockServerTool).toHaveBeenCalledWith(
        'batch_execute',
        expect.any(String),
        expect.any(Object),
        expect.any(Function)
      );
      expect(mockLogger.info).toHaveBeenCalledWith('Registering tool: batch_execute');
    });

    it('should have correct tool description mentioning batch and performance', () => {
      registerBatchExecuteTool(mockServer as any, mockMcpUnity as any, mockLogger as any);

      const [, description] = mockServerTool.mock.calls[0];
      expect(description).toContain('batch');
      expect(description).toContain('operations');
    });

    it('should have correct schema with operations array', () => {
      registerBatchExecuteTool(mockServer as any, mockMcpUnity as any, mockLogger as any);

      const [, , schema] = mockServerTool.mock.calls[0];
      expect(schema).toHaveProperty('operations');
      expect(schema).toHaveProperty('stopOnError');
      expect(schema).toHaveProperty('atomic');
    });
  });

  describe('batch_execute handler', () => {
    let toolHandler: (params: any) => Promise<any>;

    beforeEach(() => {
      registerBatchExecuteTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      toolHandler = mockServerTool.mock.calls[0][3];
    });

    it('should send batch request to Unity with correct parameters', async () => {
      mockSendRequest.mockResolvedValue({
        success: true,
        type: 'text',
        message: 'Successfully executed 2/2 operations.',
        results: [
          { index: 0, id: '0', success: true },
          { index: 1, id: '1', success: true }
        ],
        summary: { total: 2, succeeded: 2, failed: 0, executed: 2 }
      });

      const params = {
        operations: [
          { tool: 'create_gameobject', params: { name: 'Test1' } },
          { tool: 'create_gameobject', params: { name: 'Test2' } }
        ],
        stopOnError: true,
        atomic: false
      };

      const result = await toolHandler(params);

      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'batch_execute',
        params: expect.objectContaining({
          operations: expect.arrayContaining([
            expect.objectContaining({ tool: 'create_gameobject' }),
            expect.objectContaining({ tool: 'create_gameobject' })
          ]),
          stopOnError: true,
          atomic: false
        })
      });
      expect(result.content[0].text).toContain('Successfully');
    });

    it('should throw error when operations array is empty', async () => {
      const params = {
        operations: [],
        stopOnError: true
      };

      await expect(toolHandler(params)).rejects.toThrow(McpUnityError);
    });

    it('should throw error when nested batch_execute is detected', async () => {
      const params = {
        operations: [
          { tool: 'batch_execute', params: { operations: [] } }
        ]
      };

      await expect(toolHandler(params)).rejects.toThrow('Cannot nest batch_execute');
    });

    it('should handle partial failures with stopOnError=false', async () => {
      mockSendRequest.mockResolvedValue({
        success: false,
        type: 'text',
        message: 'Batch execution completed with errors. 1/2 operations succeeded, 1 failed.',
        results: [
          { index: 0, id: '0', success: true },
          { index: 1, id: '1', success: false, error: 'Tool failed' }
        ],
        summary: { total: 2, succeeded: 1, failed: 1, executed: 2 }
      });

      const params = {
        operations: [
          { tool: 'tool1', params: {} },
          { tool: 'tool2', params: {} }
        ],
        stopOnError: false
      };

      // With stopOnError=false, should return result even with failures
      const result = await toolHandler(params);
      expect(result.content[0].text).toContain('1/2');
      expect(result.content[0].text).toContain('failed');
    });

    it('should throw error on failure when stopOnError=true', async () => {
      mockSendRequest.mockResolvedValue({
        success: false,
        type: 'text',
        message: 'Batch execution stopped on error. 0/2 operations succeeded.',
        results: [
          { index: 0, id: '0', success: false, error: 'First tool failed' }
        ],
        summary: { total: 2, succeeded: 0, failed: 1, executed: 1 }
      });

      const params = {
        operations: [
          { tool: 'tool1', params: {} },
          { tool: 'tool2', params: {} }
        ],
        stopOnError: true
      };

      await expect(toolHandler(params)).rejects.toThrow(McpUnityError);
    });

    it('should preserve operation ids in request', async () => {
      mockSendRequest.mockResolvedValue({
        success: true,
        type: 'text',
        message: 'Successfully executed 2/2 operations.',
        results: [
          { index: 0, id: 'op1', success: true },
          { index: 1, id: 'op2', success: true }
        ],
        summary: { total: 2, succeeded: 2, failed: 0, executed: 2 }
      });

      const params = {
        operations: [
          { tool: 'tool1', params: {}, id: 'op1' },
          { tool: 'tool2', params: {}, id: 'op2' }
        ]
      };

      await toolHandler(params);

      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'batch_execute',
        params: expect.objectContaining({
          operations: expect.arrayContaining([
            expect.objectContaining({ id: 'op1' }),
            expect.objectContaining({ id: 'op2' })
          ])
        })
      });
    });

    it('should use default values for stopOnError and atomic', async () => {
      mockSendRequest.mockResolvedValue({
        success: true,
        type: 'text',
        message: 'Success',
        results: [],
        summary: { total: 1, succeeded: 1, failed: 0, executed: 1 }
      });

      const params = {
        operations: [{ tool: 'tool1', params: {} }]
      };

      await toolHandler(params);

      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'batch_execute',
        params: expect.objectContaining({
          stopOnError: true,
          atomic: false
        })
      });
    });
  });
});
