import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';

/**
 * Registers the Unity dashboard prompt with the MCP server.
 * This prompt provides an easy way to open and interact with the Unity dashboard MCP app.
 * 
 * @param server The McpServer instance to register the prompt with.
 */
export function registerUnityDashboardPrompt(server: McpServer) {
  server.prompt(
    'unity_dashboard',
    'Opens the Unity dashboard MCP app in VS Code with real-time Unity Editor information',
    {},
    async () => ({
      messages: [
        {
          role: 'user', 
          content: {
            type: 'text',
            text: `You are an expert AI assistant integrated with Unity via an MCP server.

The Unity Dashboard is a powerful MCP app that provides real-time access to Unity Editor information through an interactive interface in VS Code.

Dashboard Features:
- **Play Mode Controls**: Start, pause, stop, and step through Play Mode
- **Scene Hierarchy**: Browse and interact with GameObjects in your scene
- **Console Logs**: View Unity console messages (info, warnings, errors)
- **Package Manager**: List installed packages and their versions
- **Scene Management**: View loaded scenes and their status

To open the Unity Dashboard:
Use the "show_unity_dashboard" tool to launch the dashboard app in VS Code.

Requirements:
- VS Code 1.109 or later (MCP Apps support)
- Unity Editor with MCP Unity server running and connected

Usage Scenarios:
- Monitor Unity Editor state while working with AI
- Quick access to scene hierarchy and GameObject information
- Debug console logs without switching to Unity
- Control Play Mode from within your coding environment
- View package dependencies at a glance

Once opened, the dashboard remains available in your VS Code editor tabs and updates in real-time as changes occur in Unity.`
          }
        },
        {
          role: 'user',
          content: {
            type: 'text',
            text: `Open the Unity dashboard app now.`
          }
        }
      ]
    })
  );
}
