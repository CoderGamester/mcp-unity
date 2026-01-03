import { McpUnityError, ErrorType, handleError } from '../utils/errors.js';

describe('McpUnityError', () => {
  describe('constructor', () => {
    it('should create error with type and message', () => {
      const error = new McpUnityError(ErrorType.CONNECTION, 'Connection failed');

      expect(error.type).toBe(ErrorType.CONNECTION);
      expect(error.message).toBe('Connection failed');
      expect(error.name).toBe('McpUnityError');
      expect(error.details).toBeUndefined();
    });

    it('should create error with details', () => {
      const details = { host: 'localhost', port: 8090 };
      const error = new McpUnityError(ErrorType.CONNECTION, 'Connection failed', details);

      expect(error.details).toEqual(details);
    });

    it('should be an instance of Error', () => {
      const error = new McpUnityError(ErrorType.INTERNAL, 'Test error');

      expect(error).toBeInstanceOf(Error);
      expect(error).toBeInstanceOf(McpUnityError);
    });
  });

  describe('toJSON', () => {
    it('should serialize error to JSON object', () => {
      const error = new McpUnityError(ErrorType.VALIDATION, 'Invalid input', { field: 'name' });

      const json = error.toJSON();

      expect(json).toEqual({
        type: ErrorType.VALIDATION,
        message: 'Invalid input',
        details: { field: 'name' },
      });
    });

    it('should serialize error without details', () => {
      const error = new McpUnityError(ErrorType.TIMEOUT, 'Request timed out');

      const json = error.toJSON();

      expect(json).toEqual({
        type: ErrorType.TIMEOUT,
        message: 'Request timed out',
        details: undefined,
      });
    });
  });
});

describe('handleError', () => {
  it('should return McpUnityError as-is', () => {
    const originalError = new McpUnityError(ErrorType.TOOL_EXECUTION, 'Tool failed');

    const result = handleError(originalError, 'test');

    expect(result).toBe(originalError);
  });

  it('should wrap standard Error in McpUnityError', () => {
    const standardError = new Error('Something went wrong');

    const result = handleError(standardError, 'TestContext');

    expect(result).toBeInstanceOf(McpUnityError);
    expect(result.type).toBe(ErrorType.INTERNAL);
    expect(result.message).toBe('TestContext error: Something went wrong');
    expect(result.details).toBe(standardError);
  });

  it('should handle error without message', () => {
    const errorWithoutMessage = {};

    const result = handleError(errorWithoutMessage, 'Context');

    expect(result.message).toBe('Context error: Unknown error');
  });
});

describe('ErrorType', () => {
  it('should have all expected error types', () => {
    expect(ErrorType.CONNECTION).toBe('connection_error');
    expect(ErrorType.TOOL_EXECUTION).toBe('tool_execution_error');
    expect(ErrorType.RESOURCE_FETCH).toBe('resource_fetch_error');
    expect(ErrorType.VALIDATION).toBe('validation_error');
    expect(ErrorType.INTERNAL).toBe('internal_error');
    expect(ErrorType.TIMEOUT).toBe('timeout_error');
  });
});
