import { jest, describe, it, expect, beforeEach } from '@jest/globals';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import {
  registerCreateMaterialTool,
  registerAssignMaterialTool,
  registerModifyMaterialTool,
  registerGetMaterialInfoTool
} from '../tools/materialTools.js';

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

describe('Material Tools', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('registerCreateMaterialTool', () => {
    it('should register the create_material tool with the server', () => {
      registerCreateMaterialTool(mockServer as any, mockMcpUnity as any, mockLogger as any);

      expect(mockServerTool).toHaveBeenCalledTimes(1);
      expect(mockServerTool).toHaveBeenCalledWith(
        'create_material',
        expect.any(String),
        expect.any(Object),
        expect.any(Function)
      );
      expect(mockLogger.info).toHaveBeenCalledWith('Registering tool: create_material');
    });

    it('should have correct tool description', () => {
      registerCreateMaterialTool(mockServer as any, mockMcpUnity as any, mockLogger as any);

      const [, description] = mockServerTool.mock.calls[0];
      expect(description).toContain('material');
      expect(description).toContain('shader');
    });
  });

  describe('registerAssignMaterialTool', () => {
    it('should register the assign_material tool with the server', () => {
      registerAssignMaterialTool(mockServer as any, mockMcpUnity as any, mockLogger as any);

      expect(mockServerTool).toHaveBeenCalledTimes(1);
      expect(mockServerTool).toHaveBeenCalledWith(
        'assign_material',
        expect.any(String),
        expect.any(Object),
        expect.any(Function)
      );
      expect(mockLogger.info).toHaveBeenCalledWith('Registering tool: assign_material');
    });
  });

  describe('registerModifyMaterialTool', () => {
    it('should register the modify_material tool with the server', () => {
      registerModifyMaterialTool(mockServer as any, mockMcpUnity as any, mockLogger as any);

      expect(mockServerTool).toHaveBeenCalledTimes(1);
      expect(mockServerTool).toHaveBeenCalledWith(
        'modify_material',
        expect.any(String),
        expect.any(Object),
        expect.any(Function)
      );
      expect(mockLogger.info).toHaveBeenCalledWith('Registering tool: modify_material');
    });
  });

  describe('registerGetMaterialInfoTool', () => {
    it('should register the get_material_info tool with the server', () => {
      registerGetMaterialInfoTool(mockServer as any, mockMcpUnity as any, mockLogger as any);

      expect(mockServerTool).toHaveBeenCalledTimes(1);
      expect(mockServerTool).toHaveBeenCalledWith(
        'get_material_info',
        expect.any(String),
        expect.any(Object),
        expect.any(Function)
      );
      expect(mockLogger.info).toHaveBeenCalledWith('Registering tool: get_material_info');
    });
  });

  describe('create_material handler', () => {
    let handler: Function;

    beforeEach(() => {
      registerCreateMaterialTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      handler = mockServerTool.mock.calls[0][3];
    });

    it('should throw validation error when name is missing', async () => {
      const params = { savePath: 'Assets/Materials/Test.mat' };

      await expect(handler(params)).rejects.toThrow(McpUnityError);
      await expect(handler(params)).rejects.toMatchObject({
        type: ErrorType.VALIDATION,
        message: expect.stringContaining('name')
      });
    });

    it('should throw validation error when savePath is missing', async () => {
      const params = { name: 'TestMaterial' };

      await expect(handler(params)).rejects.toThrow(McpUnityError);
      await expect(handler(params)).rejects.toMatchObject({
        type: ErrorType.VALIDATION,
        message: expect.stringContaining('savePath')
      });
    });

    it('should send request to Unity with correct parameters', async () => {
      mockSendRequest.mockResolvedValue({
        success: true,
        type: 'text',
        message: 'Material created'
      });

      const params = {
        name: 'TestMaterial',
        shader: 'Standard',
        savePath: 'Assets/Materials/Test.mat',
        properties: { _Color: { r: 1, g: 0, b: 0, a: 1 } }
      };

      await handler(params);

      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'create_material',
        params: {
          name: 'TestMaterial',
          shader: 'Standard',
          savePath: 'Assets/Materials/Test.mat',
          properties: { _Color: { r: 1, g: 0, b: 0, a: 1 } }
        }
      });
    });

    it('should use default shader when not specified', async () => {
      mockSendRequest.mockResolvedValue({
        success: true,
        type: 'text',
        message: 'Material created'
      });

      const params = {
        name: 'TestMaterial',
        savePath: 'Assets/Materials/Test.mat'
      };

      await handler(params);

      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'create_material',
        params: expect.objectContaining({
          shader: 'Standard'
        })
      });
    });

    it('should throw tool execution error on Unity failure', async () => {
      mockSendRequest.mockResolvedValue({
        success: false,
        message: 'Shader not found'
      });

      const params = {
        name: 'TestMaterial',
        shader: 'NonExistentShader',
        savePath: 'Assets/Materials/Test.mat'
      };

      await expect(handler(params)).rejects.toThrow(McpUnityError);
      await expect(handler(params)).rejects.toMatchObject({
        type: ErrorType.TOOL_EXECUTION
      });
    });
  });

  describe('assign_material handler', () => {
    let handler: Function;

    beforeEach(() => {
      registerAssignMaterialTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      handler = mockServerTool.mock.calls[0][3];
    });

    it('should throw validation error when neither instanceId nor objectPath provided', async () => {
      const params = { materialPath: 'Assets/Materials/Test.mat' };

      await expect(handler(params)).rejects.toThrow(McpUnityError);
      await expect(handler(params)).rejects.toMatchObject({
        type: ErrorType.VALIDATION,
        message: expect.stringContaining('instanceId')
      });
    });

    it('should throw validation error when materialPath is missing', async () => {
      const params = { objectPath: '/Player' };

      await expect(handler(params)).rejects.toThrow(McpUnityError);
      await expect(handler(params)).rejects.toMatchObject({
        type: ErrorType.VALIDATION,
        message: expect.stringContaining('materialPath')
      });
    });

    it('should send request with instanceId', async () => {
      mockSendRequest.mockResolvedValue({
        success: true,
        type: 'text',
        message: 'Material assigned'
      });

      const params = {
        instanceId: 12345,
        materialPath: 'Assets/Materials/Test.mat',
        slot: 0
      };

      await handler(params);

      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'assign_material',
        params: {
          instanceId: 12345,
          objectPath: undefined,
          materialPath: 'Assets/Materials/Test.mat',
          slot: 0
        }
      });
    });

    it('should send request with objectPath', async () => {
      mockSendRequest.mockResolvedValue({
        success: true,
        type: 'text',
        message: 'Material assigned'
      });

      const params = {
        objectPath: '/Player/Body',
        materialPath: 'Assets/Materials/Test.mat'
      };

      await handler(params);

      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'assign_material',
        params: expect.objectContaining({
          objectPath: '/Player/Body',
          slot: 0
        })
      });
    });

    it('should use default slot of 0 when not specified', async () => {
      mockSendRequest.mockResolvedValue({
        success: true,
        type: 'text',
        message: 'Material assigned'
      });

      const params = {
        instanceId: 12345,
        materialPath: 'Assets/Materials/Test.mat'
      };

      await handler(params);

      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'assign_material',
        params: expect.objectContaining({
          slot: 0
        })
      });
    });
  });

  describe('modify_material handler', () => {
    let handler: Function;

    beforeEach(() => {
      registerModifyMaterialTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      handler = mockServerTool.mock.calls[0][3];
    });

    it('should throw validation error when materialPath is missing', async () => {
      const params = { properties: { _Color: { r: 1, g: 0, b: 0, a: 1 } } };

      await expect(handler(params)).rejects.toThrow(McpUnityError);
      await expect(handler(params)).rejects.toMatchObject({
        type: ErrorType.VALIDATION,
        message: expect.stringContaining('materialPath')
      });
    });

    it('should throw validation error when properties is empty', async () => {
      const params = { materialPath: 'Assets/Materials/Test.mat', properties: {} };

      await expect(handler(params)).rejects.toThrow(McpUnityError);
      await expect(handler(params)).rejects.toMatchObject({
        type: ErrorType.VALIDATION,
        message: expect.stringContaining('properties')
      });
    });

    it('should throw validation error when properties is missing', async () => {
      const params = { materialPath: 'Assets/Materials/Test.mat' };

      await expect(handler(params)).rejects.toThrow(McpUnityError);
    });

    it('should send request with correct properties', async () => {
      mockSendRequest.mockResolvedValue({
        success: true,
        type: 'text',
        message: 'Material modified'
      });

      const params = {
        materialPath: 'Assets/Materials/Test.mat',
        properties: {
          _Color: { r: 1, g: 0.5, b: 0, a: 1 },
          _Metallic: 0.5,
          _MainTex: 'Assets/Textures/Wood.png'
        }
      };

      await handler(params);

      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'modify_material',
        params: {
          materialPath: 'Assets/Materials/Test.mat',
          properties: {
            _Color: { r: 1, g: 0.5, b: 0, a: 1 },
            _Metallic: 0.5,
            _MainTex: 'Assets/Textures/Wood.png'
          }
        }
      });
    });
  });

  describe('get_material_info handler', () => {
    let handler: Function;

    beforeEach(() => {
      registerGetMaterialInfoTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      handler = mockServerTool.mock.calls[0][3];
    });

    it('should throw validation error when materialPath is missing', async () => {
      const params = {};

      await expect(handler(params)).rejects.toThrow(McpUnityError);
      await expect(handler(params)).rejects.toMatchObject({
        type: ErrorType.VALIDATION,
        message: expect.stringContaining('materialPath')
      });
    });

    it('should send request and return formatted response', async () => {
      mockSendRequest.mockResolvedValue({
        success: true,
        type: 'text',
        message: 'Material info',
        materialName: 'TestMaterial',
        materialPath: 'Assets/Materials/Test.mat',
        shaderName: 'Standard',
        renderQueue: 2000,
        renderQueueCategory: 'Geometry',
        enableInstancing: false,
        doubleSidedGI: false,
        passCount: 1,
        properties: [
          { name: '_Color', type: 'Color', value: { r: 1, g: 1, b: 1, a: 1 }, description: 'Main Color' },
          { name: '_Metallic', type: 'Float', value: 0, description: 'Metallic' }
        ]
      });

      const params = { materialPath: 'Assets/Materials/Test.mat' };
      const result = await handler(params);

      expect(result.content[0].text).toContain('Material: TestMaterial');
      expect(result.content[0].text).toContain('Shader: Standard');
      expect(result.content[0].text).toContain('_Color');
      expect(result.content[0].text).toContain('_Metallic');
    });

    it('should include data object in response', async () => {
      mockSendRequest.mockResolvedValue({
        success: true,
        type: 'text',
        message: 'Material info',
        materialName: 'TestMaterial',
        materialPath: 'Assets/Materials/Test.mat',
        shaderName: 'Standard',
        renderQueue: 2000,
        renderQueueCategory: 'Geometry',
        enableInstancing: false,
        doubleSidedGI: false,
        passCount: 1,
        properties: []
      });

      const params = { materialPath: 'Assets/Materials/Test.mat' };
      const result = await handler(params);

      expect(result.data).toBeDefined();
      expect(result.data.materialName).toBe('TestMaterial');
      expect(result.data.shaderName).toBe('Standard');
    });
  });
});
