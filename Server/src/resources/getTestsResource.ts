import { z } from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { ReadResourceResult } from '@modelcontextprotocol/sdk/types.js';
import { ResourceDefinition } from './resourceRegistry.js';
import { ResourceTemplate } from '@modelcontextprotocol/sdk/server/mcp.js';

export interface TestItem {
  name: string;
  fullName: string;
  path: string;
  testMode: string;
  runState: string;
}

/**
 * Lists available test modes (EditMode and PlayMode)
 * @param resourceMimeType The MIME type for the resources
 * @returns A list of test mode resources
 */
function listTestModes(resourceMimeType: string) {
  return {
    resources: [
      { 
        uri: `unity://tests/EditMode`, 
        name: "EditMode tests",
        description: "List of all EditMode tests in Unity's test runner",
        mimeType: resourceMimeType
      },
      { 
        uri: `unity://tests/PlayMode`, 
        name: "PlayMode tests",
        description: "List of all PlayMode tests in Unity's test runner",
        mimeType: resourceMimeType
      }
    ]
  };
}

export function createGetTestsResource(mcpUnity: McpUnity, logger: Logger): ResourceDefinition {
  const resourceName = 'get_tests';
  const resourceMimeType = 'application/json';
  
  // Create the resource definition
  return {
    name: resourceName,
    uri: new ResourceTemplate('unity://tests/{testMode}', { 
      list: () => listTestModes(resourceMimeType)
    }),
    metadata: {
      description: "Gets the list of available tests from Unity's Test Runner",
      mimeType: resourceMimeType
    },
    
    // Handler for the resource
    handler: async (params: any): Promise<ReadResourceResult> => {
      // Extract query parameters from the URL if available
      const testMode = params?.testMode || params?.mode || 'EditMode';
      
      const response = await mcpUnity.sendRequest({
        method: resourceName,
        params: {
          testMode
        }
      });
      
      return {
        contents: [{
          uri: `unity://tests/${testMode}`,
          mimeType: resourceMimeType,
          text: JSON.stringify(response, null, 2)
        }]
      };
    }
  };
}
