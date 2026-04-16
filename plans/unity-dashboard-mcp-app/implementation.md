# Unity Dashboard MCP App

## Goal
Deliver a VS Code MCP App dashboard that visualizes Unity scene hierarchy, console logs, and play controls with polling-based refresh and a dedicated app view.

## Prerequisites
Make sure that the use is currently on the `feature/unity-dashboard-mcp-app` branch before beginning implementation.
If not, move them to the correct branch. If the branch does not exist, create it from main.

### Step-by-Step Instructions

#### Step 1: Add Dashboard UI Resource
- [ ] Create the resource handler at [Server~/src/resources/unityDashboardAppResource.ts](Server~/src/resources/unityDashboardAppResource.ts).
- [ ] Copy and paste code below into [Server~/src/resources/unityDashboardAppResource.ts](Server~/src/resources/unityDashboardAppResource.ts):

```typescript
import fs from 'node:fs';
import path from 'node:path';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { ReadResourceResult } from '@modelcontextprotocol/sdk/types.js';
import { Logger } from '../utils/logger.js';

const resourceName = 'unity_dashboard_app';
const resourceUri = 'unity://ui/dashboard';
const resourceMimeType = 'text/html';

export function registerUnityDashboardAppResource(server: McpServer, logger: Logger) {
  logger.info(`Registering resource: ${resourceName}`);

  server.resource(
    resourceName,
    resourceUri,
    {
      description: 'Unity Dashboard MCP App UI (HTML) for VS Code MCP Apps',
      mimeType: resourceMimeType
    },
    async () => {
      try {
        return readDashboardHtml();
      } catch (error) {
        logger.error(`Error handling resource ${resourceName}: ${error}`);
        throw error;
      }
    }
  );
}

function readDashboardHtml(): ReadResourceResult {
  const htmlPath = resolveDashboardPath();
  const html = fs.readFileSync(htmlPath, 'utf8');

  return {
    contents: [
      {
        uri: resourceUri,
        mimeType: resourceMimeType,
        text: html
      }
    ]
  };
}

function resolveDashboardPath(): string {
  const srcPath = path.join(process.cwd(), 'src', 'ui', 'unity-dashboard.html');
  if (fs.existsSync(srcPath)) {
    return srcPath;
  }

  const buildPath = path.join(process.cwd(), 'build', 'ui', 'unity-dashboard.html');
  if (fs.existsSync(buildPath)) {
    return buildPath;
  }

  return srcPath;
}
```

- [ ] Create the HTML shell at [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html).
- [ ] Copy and paste code below into [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html):

```html
<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Unity Dashboard</title>
    <link
      rel="stylesheet"
      href="https://fonts.googleapis.com/css2?family=Space+Grotesk:wght@400;500;600;700&family=Space+Mono:wght@400;700&display=swap"
    />
    <style>
      :root {
        color-scheme: light;
        --bg: #f6f4ef;
        --bg-accent: #f1e7d7;
        --panel: #ffffff;
        --ink: #1f1b16;
        --muted: #6f655b;
        --line: #e3d7c7;
        --primary: #2f6f5e;
        --primary-strong: #235544;
        --accent: #d77a5b;
        --shadow: 0 24px 60px rgba(41, 29, 17, 0.12);
        --radius: 20px;
      }

      * {
        box-sizing: border-box;
      }

      body {
        margin: 0;
        font-family: "Space Grotesk", "Space Mono", sans-serif;
        background: radial-gradient(circle at top, var(--bg-accent), var(--bg));
        color: var(--ink);
        min-height: 100vh;
        padding: 28px;
      }

      .app {
        max-width: 1200px;
        margin: 0 auto;
        display: grid;
        gap: 20px;
      }

      header {
        background: var(--panel);
        border-radius: var(--radius);
        padding: 24px;
        box-shadow: var(--shadow);
        border: 1px solid var(--line);
      }

      h1 {
        margin: 0 0 6px 0;
        font-size: 32px;
        letter-spacing: -0.02em;
      }

      .subtitle {
        color: var(--muted);
        font-size: 14px;
      }

      main {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
        gap: 20px;
      }

      .panel {
        background: var(--panel);
        border-radius: var(--radius);
        padding: 20px;
        border: 1px solid var(--line);
        min-height: 220px;
      }
    </style>
  </head>
  <body>
    <div class="app">
      <header>
        <h1>Unity Scene Dashboard</h1>
        <div class="subtitle">MCP App shell. Logic is added in Step 3.</div>
      </header>
      <main>
        <section class="panel">Scene hierarchy placeholder</section>
        <section class="panel">Console logs placeholder</section>
      </main>
    </div>
  </body>
</html>
```

- [ ] Register the dashboard resource in the MCP server.
- [ ] Copy and paste code below into [Server~/src/index.ts](Server~/src/index.ts):

```typescript
// Import MCP SDK components
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { McpUnity } from './unity/mcpUnity.js';
import { Logger, LogLevel } from './utils/logger.js';
import { registerCreateSceneTool } from './tools/createSceneTool.js';
import { registerMenuItemTool } from './tools/menuItemTool.js';
import { registerSelectGameObjectTool } from './tools/selectGameObjectTool.js';
import { registerAddPackageTool } from './tools/addPackageTool.js';
import { registerRunTestsTool } from './tools/runTestsTool.js';
import { registerSendConsoleLogTool } from './tools/sendConsoleLogTool.js';
import { registerGetConsoleLogsTool } from './tools/getConsoleLogsTool.js';
import { registerUpdateComponentTool } from './tools/updateComponentTool.js';
import { registerAddAssetToSceneTool } from './tools/addAssetToSceneTool.js';
import { registerUpdateGameObjectTool } from './tools/updateGameObjectTool.js';
import { registerCreatePrefabTool } from './tools/createPrefabTool.js';
import { registerDeleteSceneTool } from './tools/deleteSceneTool.js';
import { registerLoadSceneTool } from './tools/loadSceneTool.js';
import { registerSaveSceneTool } from './tools/saveSceneTool.js';
import { registerGetSceneInfoTool } from './tools/getSceneInfoTool.js';
import { registerUnloadSceneTool } from './tools/unloadSceneTool.js';
import { registerRecompileScriptsTool } from './tools/recompileScriptsTool.js';
import { registerGetGameObjectTool } from './tools/getGameObjectTool.js';
import { registerTransformTools } from './tools/transformTools.js';
import { registerCreateMaterialTool, registerAssignMaterialTool, registerModifyMaterialTool, registerGetMaterialInfoTool } from './tools/materialTools.js';
import { registerDuplicateGameObjectTool, registerDeleteGameObjectTool, registerReparentGameObjectTool } from './tools/gameObjectTools.js';
import { registerBatchExecuteTool } from './tools/batchExecuteTool.js';
import { registerGetMenuItemsResource } from './resources/getMenuItemResource.js';
import { registerGetConsoleLogsResource } from './resources/getConsoleLogsResource.js';
import { registerGetHierarchyResource } from './resources/getScenesHierarchyResource.js';
import { registerGetPackagesResource } from './resources/getPackagesResource.js';
import { registerGetAssetsResource } from './resources/getAssetsResource.js';
import { registerGetTestsResource } from './resources/getTestsResource.js';
import { registerGetGameObjectResource } from './resources/getGameObjectResource.js';
import { registerUnityDashboardAppResource } from './resources/unityDashboardAppResource.js';
import { registerGameObjectHandlingPrompt } from './prompts/gameobjectHandlingPrompt.js';

// Initialize loggers
const serverLogger = new Logger('Server', LogLevel.INFO);
const unityLogger = new Logger('Unity', LogLevel.INFO);
const toolLogger = new Logger('Tools', LogLevel.INFO);
const resourceLogger = new Logger('Resources', LogLevel.INFO);

// Initialize the MCP server
const server = new McpServer (
  {
    name: "MCP Unity Server",
    version: "1.0.0"
  },
  {
    capabilities: {
      tools: {},
      resources: {},
      prompts: {},
    },
  }
);

// Initialize MCP HTTP bridge with Unity editor
const mcpUnity = new McpUnity(unityLogger);

// Register all tools into the MCP server
registerMenuItemTool(server, mcpUnity, toolLogger);
registerSelectGameObjectTool(server, mcpUnity, toolLogger);
registerAddPackageTool(server, mcpUnity, toolLogger);
registerRunTestsTool(server, mcpUnity, toolLogger);
registerSendConsoleLogTool(server, mcpUnity, toolLogger);
registerGetConsoleLogsTool(server, mcpUnity, toolLogger);
registerUpdateComponentTool(server, mcpUnity, toolLogger);
registerAddAssetToSceneTool(server, mcpUnity, toolLogger);
registerUpdateGameObjectTool(server, mcpUnity, toolLogger);
registerCreatePrefabTool(server, mcpUnity, toolLogger);
registerCreateSceneTool(server, mcpUnity, toolLogger);
registerDeleteSceneTool(server, mcpUnity, toolLogger);
registerLoadSceneTool(server, mcpUnity, toolLogger);
registerSaveSceneTool(server, mcpUnity, toolLogger);
registerGetSceneInfoTool(server, mcpUnity, toolLogger);
registerUnloadSceneTool(server, mcpUnity, toolLogger);
registerRecompileScriptsTool(server, mcpUnity, toolLogger);
registerGetGameObjectTool(server, mcpUnity, toolLogger);
registerTransformTools(server, mcpUnity, toolLogger);
registerDuplicateGameObjectTool(server, mcpUnity, toolLogger);
registerDeleteGameObjectTool(server, mcpUnity, toolLogger);
registerReparentGameObjectTool(server, mcpUnity, toolLogger);

// Register Material Tools
registerCreateMaterialTool(server, mcpUnity, toolLogger);
registerAssignMaterialTool(server, mcpUnity, toolLogger);
registerModifyMaterialTool(server, mcpUnity, toolLogger);
registerGetMaterialInfoTool(server, mcpUnity, toolLogger);

// Register Batch Execute Tool (high-priority for performance)
registerBatchExecuteTool(server, mcpUnity, toolLogger);

// Register all resources into the MCP server
registerGetTestsResource(server, mcpUnity, resourceLogger);
registerGetGameObjectResource(server, mcpUnity, resourceLogger);
registerGetMenuItemsResource(server, mcpUnity, resourceLogger);
registerGetConsoleLogsResource(server, mcpUnity, resourceLogger);
registerGetHierarchyResource(server, mcpUnity, resourceLogger);
registerGetPackagesResource(server, mcpUnity, resourceLogger);
registerGetAssetsResource(server, mcpUnity, resourceLogger);
registerUnityDashboardAppResource(server, resourceLogger);

// Register all prompts into the MCP server
registerGameObjectHandlingPrompt(server);

// Server startup function
async function startServer() {
  try {
    // Initialize STDIO transport for MCP client communication
    const stdioTransport = new StdioServerTransport();
    
    // Connect the server to the transport
    await server.connect(stdioTransport);

    serverLogger.info('MCP Server started');
    
    // Get the client name from the MCP server
    const clientName = server.server.getClientVersion()?.name || 'Unknown MCP Client';
    serverLogger.info(`Connected MCP client: ${clientName}`);
    
    // Start Unity Bridge connection with client name in headers
    await mcpUnity.start(clientName);
    
  } catch (error) {
    serverLogger.error('Failed to start server', error);
    process.exit(1);
  }
}

// Graceful shutdown handler
let isShuttingDown = false;
async function shutdown() {
  if (isShuttingDown) return;
  isShuttingDown = true;

  try {
    serverLogger.info('Shutting down...');
    await mcpUnity.stop();
    await server.close();
  } catch (error) {
    // Ignore errors during shutdown
  }
  process.exit(0);
}

// Start the server
startServer();

// Handle shutdown signals
process.on('SIGINT', shutdown);
process.on('SIGTERM', shutdown);
process.on('SIGHUP', shutdown);

// Handle stdin close (when MCP client disconnects)
process.stdin.on('close', shutdown);
process.stdin.on('end', shutdown);
process.stdin.on('error', shutdown);

// Handle uncaught exceptions - exit cleanly if it's just a closed pipe
process.on('uncaughtException', (error: NodeJS.ErrnoException) => {
  // EPIPE/EOF errors are expected when the MCP client disconnects
  if (error.code === 'EPIPE' || error.code === 'EOF' || error.code === 'ERR_USE_AFTER_CLOSE') {
    shutdown();
    return;
  }
  serverLogger.error('Uncaught exception', error);
  process.exit(1);
});

// Handle unhandled promise rejections
process.on('unhandledRejection', (reason) => {
  serverLogger.error('Unhandled rejection', reason);
  process.exit(1);
});
```

##### Step 1 Verification Checklist
- [ ] Run `cd Server~ && npm run build` and confirm the build completes.
- [ ] Run `cd Server~ && npm run inspector`, then read `unity://ui/dashboard` and confirm HTML content is returned.

#### Step 1 STOP & COMMIT
**STOP & COMMIT:** Agent must stop here and wait for the user to test, stage, and commit the change.

#### Step 2: Add Tool to Open the Dashboard App
- [ ] Create the MCP tool at [Server~/src/tools/showUnityDashboardTool.ts](Server~/src/tools/showUnityDashboardTool.ts).
- [ ] Copy and paste code below into [Server~/src/tools/showUnityDashboardTool.ts](Server~/src/tools/showUnityDashboardTool.ts):

```typescript
import * as z from 'zod';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { Logger } from '../utils/logger.js';

const toolName = 'show_unity_dashboard';
const toolDescription = 'Opens the Unity dashboard MCP App in VS Code.';
const paramsSchema = z.object({});

export function registerShowUnityDashboardTool(server: McpServer, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.tool(toolName, toolDescription, paramsSchema.shape, async () => {
    try {
      logger.info(`Executing tool: ${toolName}`);
      const result = await toolHandler();
      logger.info(`Tool execution successful: ${toolName}`);
      return result;
    } catch (error) {
      logger.error(`Tool execution failed: ${toolName}`, error);
      throw error;
    }
  });
}

async function toolHandler(): Promise<CallToolResult> {
  return {
    content: [
      {
        type: 'resource',
        uri: 'unity://ui/dashboard',
        metadata: {
          view: 'mcp-app'
        }
      }
    ]
  };
}
```

- [ ] Register the tool in the MCP server.
- [ ] Copy and paste code below into [Server~/src/index.ts](Server~/src/index.ts):

```typescript
// Import MCP SDK components
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { McpUnity } from './unity/mcpUnity.js';
import { Logger, LogLevel } from './utils/logger.js';
import { registerCreateSceneTool } from './tools/createSceneTool.js';
import { registerMenuItemTool } from './tools/menuItemTool.js';
import { registerSelectGameObjectTool } from './tools/selectGameObjectTool.js';
import { registerAddPackageTool } from './tools/addPackageTool.js';
import { registerRunTestsTool } from './tools/runTestsTool.js';
import { registerSendConsoleLogTool } from './tools/sendConsoleLogTool.js';
import { registerGetConsoleLogsTool } from './tools/getConsoleLogsTool.js';
import { registerUpdateComponentTool } from './tools/updateComponentTool.js';
import { registerAddAssetToSceneTool } from './tools/addAssetToSceneTool.js';
import { registerUpdateGameObjectTool } from './tools/updateGameObjectTool.js';
import { registerCreatePrefabTool } from './tools/createPrefabTool.js';
import { registerDeleteSceneTool } from './tools/deleteSceneTool.js';
import { registerLoadSceneTool } from './tools/loadSceneTool.js';
import { registerSaveSceneTool } from './tools/saveSceneTool.js';
import { registerGetSceneInfoTool } from './tools/getSceneInfoTool.js';
import { registerUnloadSceneTool } from './tools/unloadSceneTool.js';
import { registerRecompileScriptsTool } from './tools/recompileScriptsTool.js';
import { registerGetGameObjectTool } from './tools/getGameObjectTool.js';
import { registerTransformTools } from './tools/transformTools.js';
import { registerCreateMaterialTool, registerAssignMaterialTool, registerModifyMaterialTool, registerGetMaterialInfoTool } from './tools/materialTools.js';
import { registerDuplicateGameObjectTool, registerDeleteGameObjectTool, registerReparentGameObjectTool } from './tools/gameObjectTools.js';
import { registerBatchExecuteTool } from './tools/batchExecuteTool.js';
import { registerShowUnityDashboardTool } from './tools/showUnityDashboardTool.js';
import { registerGetMenuItemsResource } from './resources/getMenuItemResource.js';
import { registerGetConsoleLogsResource } from './resources/getConsoleLogsResource.js';
import { registerGetHierarchyResource } from './resources/getScenesHierarchyResource.js';
import { registerGetPackagesResource } from './resources/getPackagesResource.js';
import { registerGetAssetsResource } from './resources/getAssetsResource.js';
import { registerGetTestsResource } from './resources/getTestsResource.js';
import { registerGetGameObjectResource } from './resources/getGameObjectResource.js';
import { registerUnityDashboardAppResource } from './resources/unityDashboardAppResource.js';
import { registerGameObjectHandlingPrompt } from './prompts/gameobjectHandlingPrompt.js';

// Initialize loggers
const serverLogger = new Logger('Server', LogLevel.INFO);
const unityLogger = new Logger('Unity', LogLevel.INFO);
const toolLogger = new Logger('Tools', LogLevel.INFO);
const resourceLogger = new Logger('Resources', LogLevel.INFO);

// Initialize the MCP server
const server = new McpServer (
  {
    name: "MCP Unity Server",
    version: "1.0.0"
  },
  {
    capabilities: {
      tools: {},
      resources: {},
      prompts: {},
    },
  }
);

// Initialize MCP HTTP bridge with Unity editor
const mcpUnity = new McpUnity(unityLogger);

// Register all tools into the MCP server
registerMenuItemTool(server, mcpUnity, toolLogger);
registerSelectGameObjectTool(server, mcpUnity, toolLogger);
registerAddPackageTool(server, mcpUnity, toolLogger);
registerRunTestsTool(server, mcpUnity, toolLogger);
registerSendConsoleLogTool(server, mcpUnity, toolLogger);
registerGetConsoleLogsTool(server, mcpUnity, toolLogger);
registerUpdateComponentTool(server, mcpUnity, toolLogger);
registerAddAssetToSceneTool(server, mcpUnity, toolLogger);
registerUpdateGameObjectTool(server, mcpUnity, toolLogger);
registerCreatePrefabTool(server, mcpUnity, toolLogger);
registerCreateSceneTool(server, mcpUnity, toolLogger);
registerDeleteSceneTool(server, mcpUnity, toolLogger);
registerLoadSceneTool(server, mcpUnity, toolLogger);
registerSaveSceneTool(server, mcpUnity, toolLogger);
registerGetSceneInfoTool(server, mcpUnity, toolLogger);
registerShowUnityDashboardTool(server, toolLogger);
registerUnloadSceneTool(server, mcpUnity, toolLogger);
registerRecompileScriptsTool(server, mcpUnity, toolLogger);
registerGetGameObjectTool(server, mcpUnity, toolLogger);
registerTransformTools(server, mcpUnity, toolLogger);
registerDuplicateGameObjectTool(server, mcpUnity, toolLogger);
registerDeleteGameObjectTool(server, mcpUnity, toolLogger);
registerReparentGameObjectTool(server, mcpUnity, toolLogger);

// Register Material Tools
registerCreateMaterialTool(server, mcpUnity, toolLogger);
registerAssignMaterialTool(server, mcpUnity, toolLogger);
registerModifyMaterialTool(server, mcpUnity, toolLogger);
registerGetMaterialInfoTool(server, mcpUnity, toolLogger);

// Register Batch Execute Tool (high-priority for performance)
registerBatchExecuteTool(server, mcpUnity, toolLogger);

// Register all resources into the MCP server
registerGetTestsResource(server, mcpUnity, resourceLogger);
registerGetGameObjectResource(server, mcpUnity, resourceLogger);
registerGetMenuItemsResource(server, mcpUnity, resourceLogger);
registerGetConsoleLogsResource(server, mcpUnity, resourceLogger);
registerGetHierarchyResource(server, mcpUnity, resourceLogger);
registerGetPackagesResource(server, mcpUnity, resourceLogger);
registerGetAssetsResource(server, mcpUnity, resourceLogger);
registerUnityDashboardAppResource(server, resourceLogger);

// Register all prompts into the MCP server
registerGameObjectHandlingPrompt(server);

// Server startup function
async function startServer() {
  try {
    // Initialize STDIO transport for MCP client communication
    const stdioTransport = new StdioServerTransport();
    
    // Connect the server to the transport
    await server.connect(stdioTransport);

    serverLogger.info('MCP Server started');
    
    // Get the client name from the MCP server
    const clientName = server.server.getClientVersion()?.name || 'Unknown MCP Client';
    serverLogger.info(`Connected MCP client: ${clientName}`);
    
    // Start Unity Bridge connection with client name in headers
    await mcpUnity.start(clientName);
    
  } catch (error) {
    serverLogger.error('Failed to start server', error);
    process.exit(1);
  }
}

// Graceful shutdown handler
let isShuttingDown = false;
async function shutdown() {
  if (isShuttingDown) return;
  isShuttingDown = true;

  try {
    serverLogger.info('Shutting down...');
    await mcpUnity.stop();
    await server.close();
  } catch (error) {
    // Ignore errors during shutdown
  }
  process.exit(0);
}

// Start the server
startServer();

// Handle shutdown signals
process.on('SIGINT', shutdown);
process.on('SIGTERM', shutdown);
process.on('SIGHUP', shutdown);

// Handle stdin close (when MCP client disconnects)
process.stdin.on('close', shutdown);
process.stdin.on('end', shutdown);
process.stdin.on('error', shutdown);

// Handle uncaught exceptions - exit cleanly if it's just a closed pipe
process.on('uncaughtException', (error: NodeJS.ErrnoException) => {
  // EPIPE/EOF errors are expected when the MCP client disconnects
  if (error.code === 'EPIPE' || error.code === 'EOF' || error.code === 'ERR_USE_AFTER_CLOSE') {
    shutdown();
    return;
  }
  serverLogger.error('Uncaught exception', error);
  process.exit(1);
});

// Handle unhandled promise rejections
process.on('unhandledRejection', (reason) => {
  serverLogger.error('Unhandled rejection', reason);
  process.exit(1);
});
```

##### Step 2 Verification Checklist
- [ ] Run `cd Server~ && npm run build` and confirm the build completes.
- [ ] In MCP Inspector, call the `show_unity_dashboard` tool and confirm the response returns a resource with `metadata.view` set to `mcp-app`.

#### Step 2 STOP & COMMIT
**STOP & COMMIT:** Agent must stop here and wait for the user to test, stage, and commit the change.

#### Step 3: Implement Dashboard Frontend Logic
- [ ] Replace the dashboard HTML with the full UI and MCP App logic.
- [ ] Copy and paste code below into [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html):

```html
<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Unity Dashboard</title>
    <link
      rel="stylesheet"
      href="https://fonts.googleapis.com/css2?family=Space+Grotesk:wght@400;500;600;700&family=Space+Mono:wght@400;700&display=swap"
    />
    <style>
      :root {
        color-scheme: light;
        --bg: #f6f4ef;
        --bg-accent: #f1e7d7;
        --panel: #ffffff;
        --ink: #1f1b16;
        --muted: #6f655b;
        --line: #e3d7c7;
        --primary: #2f6f5e;
        --primary-strong: #235544;
        --accent: #d77a5b;
        --warning: #d6982f;
        --danger: #b94a3f;
        --shadow: 0 24px 60px rgba(41, 29, 17, 0.12);
        --radius: 20px;
      }

      * {
        box-sizing: border-box;
      }

      body {
        margin: 0;
        font-family: "Space Grotesk", "Space Mono", sans-serif;
        background: radial-gradient(circle at top, var(--bg-accent), var(--bg));
        color: var(--ink);
        min-height: 100vh;
        padding: 28px;
      }

      .app {
        max-width: 1220px;
        margin: 0 auto;
        display: grid;
        gap: 20px;
      }

      .shell {
        background: var(--panel);
        border-radius: var(--radius);
        border: 1px solid var(--line);
        box-shadow: var(--shadow);
      }

      .hero {
        padding: 26px;
        display: grid;
        gap: 18px;
      }

      .eyebrow {
        text-transform: uppercase;
        letter-spacing: 0.2em;
        font-size: 11px;
        color: var(--muted);
      }

      h1 {
        margin: 0;
        font-size: 34px;
        letter-spacing: -0.02em;
      }

      .hero-row {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: 16px;
        justify-content: space-between;
      }

      .status-pill {
        padding: 8px 14px;
        border-radius: 999px;
        border: 1px solid var(--line);
        background: #fdf9f2;
        font-size: 13px;
        display: inline-flex;
        align-items: center;
        gap: 8px;
      }

      .status-dot {
        width: 8px;
        height: 8px;
        border-radius: 999px;
        background: var(--warning);
      }

      .status-good {
        background: #e6f3ef;
        border-color: #c7e4db;
        color: var(--primary-strong);
      }

      .status-good .status-dot {
        background: var(--primary);
      }

      .status-bad {
        background: #f7e8e4;
        border-color: #f1d1c9;
        color: var(--danger);
      }

      .meta-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
        gap: 12px;
      }

      .meta-card {
        border: 1px solid var(--line);
        border-radius: 16px;
        padding: 12px 14px;
        background: #fbf7ef;
      }

      .meta-card h3 {
        margin: 0 0 6px 0;
        font-size: 12px;
        text-transform: uppercase;
        letter-spacing: 0.14em;
        color: var(--muted);
      }

      .meta-card p {
        margin: 0;
        font-size: 15px;
        font-weight: 600;
      }

      .controls {
        display: flex;
        flex-wrap: wrap;
        gap: 10px;
        align-items: center;
      }

      button {
        font: inherit;
        padding: 10px 16px;
        border-radius: 999px;
        border: 1px solid transparent;
        background: var(--primary);
        color: #fff;
        cursor: pointer;
        transition: transform 0.15s ease, box-shadow 0.15s ease;
      }

      button.secondary {
        background: #fff;
        color: var(--primary-strong);
        border-color: var(--line);
      }

      button.ghost {
        background: transparent;
        border: 1px dashed var(--line);
        color: var(--muted);
      }

      button:disabled {
        opacity: 0.55;
        cursor: not-allowed;
        box-shadow: none;
      }

      button:hover:not(:disabled) {
        transform: translateY(-1px);
        box-shadow: 0 10px 18px rgba(38, 47, 42, 0.12);
      }

      input[type="number"] {
        border-radius: 10px;
        border: 1px solid var(--line);
        padding: 8px 10px;
        font-family: "Space Mono", monospace;
        width: 90px;
      }

      .panel-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
        gap: 20px;
      }

      .panel {
        padding: 18px;
        display: grid;
        gap: 14px;
      }

      .panel h2 {
        margin: 0;
        font-size: 18px;
      }

      .panel-body {
        border: 1px solid var(--line);
        border-radius: 16px;
        padding: 12px;
        background: #fff;
        min-height: 240px;
        max-height: 420px;
        overflow: auto;
        font-size: 13px;
      }

      .tree {
        list-style: none;
        padding-left: 16px;
        margin: 0;
      }

      .tree summary {
        cursor: pointer;
        list-style: none;
      }

      .tree summary::-webkit-details-marker {
        display: none;
      }

      .node-row {
        display: flex;
        align-items: center;
        gap: 8px;
        padding: 4px 0;
      }

      .node-name {
        font-weight: 600;
      }

      .node-meta {
        font-family: "Space Mono", monospace;
        font-size: 11px;
        color: var(--muted);
      }

      .inactive {
        opacity: 0.5;
      }

      .log-item {
        border-bottom: 1px dashed var(--line);
        padding: 8px 0;
      }

      .log-type {
        font-family: "Space Mono", monospace;
        font-size: 11px;
        text-transform: uppercase;
        letter-spacing: 0.12em;
      }

      .log-type.info {
        color: var(--primary-strong);
      }

      .log-type.warning {
        color: var(--warning);
      }

      .log-type.error {
        color: var(--danger);
      }

      .log-time {
        font-size: 11px;
        color: var(--muted);
      }

      .panel-footer {
        display: flex;
        flex-wrap: wrap;
        gap: 10px;
        align-items: center;
        justify-content: space-between;
        font-size: 12px;
        color: var(--muted);
      }

      .filters {
        display: flex;
        gap: 8px;
        flex-wrap: wrap;
      }

      .filters label {
        display: flex;
        gap: 6px;
        align-items: center;
        font-size: 12px;
      }

      .loading {
        color: var(--muted);
        font-size: 13px;
      }

      .refresh-row {
        display: flex;
        gap: 8px;
        align-items: center;
        flex-wrap: wrap;
      }

      @keyframes fadeUp {
        from {
          opacity: 0;
          transform: translateY(8px);
        }
        to {
          opacity: 1;
          transform: translateY(0);
        }
      }

      .panel,
      .hero {
        animation: fadeUp 0.35s ease;
      }

      @media (max-width: 720px) {
        body {
          padding: 18px;
        }
      }
    </style>
  </head>
  <body>
    <div class="app">
      <section class="shell hero">
        <div class="eyebrow">MCP Unity</div>
        <h1>Unity Scene Dashboard</h1>
        <div class="hero-row">
          <div class="status-pill" id="connection-status">
            <span class="status-dot"></span>
            <span>Connecting to MCP...</span>
          </div>
          <div class="controls">
            <button id="play-button">Play</button>
            <button id="pause-button" class="secondary">Pause</button>
            <button id="step-button" class="secondary">Step</button>
            <button id="refresh-button" class="ghost">Refresh</button>
          </div>
        </div>
        <div class="meta-grid">
          <div class="meta-card">
            <h3>Active Scene</h3>
            <p id="active-scene">Loading...</p>
          </div>
          <div class="meta-card">
            <h3>Root Objects</h3>
            <p id="root-count">--</p>
          </div>
          <div class="meta-card">
            <h3>Play Mode</h3>
            <p id="play-mode">Unknown</p>
          </div>
          <div class="meta-card">
            <h3>Last Refresh</h3>
            <p id="last-refresh">--</p>
          </div>
        </div>
      </section>

      <section class="panel-grid">
        <section class="shell panel">
          <h2>Scene Hierarchy</h2>
          <div class="panel-body" id="hierarchy-panel">
            <div class="loading">Waiting for data...</div>
          </div>
          <div class="panel-footer">
            <span id="hierarchy-summary">0 objects</span>
            <div class="refresh-row">
              <label for="auto-refresh">
                <input id="auto-refresh" type="checkbox" /> Auto refresh
              </label>
              <label for="refresh-interval">Interval</label>
              <input id="refresh-interval" type="number" min="2" value="5" />
              <span>sec</span>
            </div>
          </div>
        </section>

        <section class="shell panel">
          <h2>Console Logs</h2>
          <div class="panel-body" id="logs-panel">
            <div class="loading">Waiting for logs...</div>
          </div>
          <div class="panel-footer">
            <div class="filters">
              <label>
                <input type="checkbox" data-filter="info" checked /> Info
              </label>
              <label>
                <input type="checkbox" data-filter="warning" checked /> Warning
              </label>
              <label>
                <input type="checkbox" data-filter="error" checked /> Error
              </label>
            </div>
            <div class="refresh-row">
              <label for="log-limit">Limit</label>
              <input id="log-limit" type="number" min="10" max="500" value="100" />
            </div>
          </div>
        </section>
      </section>
    </div>

    <script src="https://cdn.jsdelivr.net/npm/@modelcontextprotocol/sdk-apps/dist/index.js"></script>
    <script>
      const app = new MCPApp();

      const elements = {
        connectionStatus: document.getElementById('connection-status'),
        activeScene: document.getElementById('active-scene'),
        rootCount: document.getElementById('root-count'),
        playMode: document.getElementById('play-mode'),
        lastRefresh: document.getElementById('last-refresh'),
        hierarchyPanel: document.getElementById('hierarchy-panel'),
        hierarchySummary: document.getElementById('hierarchy-summary'),
        logsPanel: document.getElementById('logs-panel'),
        refreshButton: document.getElementById('refresh-button'),
        playButton: document.getElementById('play-button'),
        pauseButton: document.getElementById('pause-button'),
        stepButton: document.getElementById('step-button'),
        autoRefresh: document.getElementById('auto-refresh'),
        refreshInterval: document.getElementById('refresh-interval'),
        logLimit: document.getElementById('log-limit')
      };

      const filters = Array.from(document.querySelectorAll('[data-filter]'));

      const state = {
        autoRefreshId: null,
        hierarchy: [],
        logs: []
      };

      function setConnectionStatus(text, tone) {
        elements.connectionStatus.classList.remove('status-good', 'status-bad');
        if (tone === 'good') {
          elements.connectionStatus.classList.add('status-good');
        }
        if (tone === 'bad') {
          elements.connectionStatus.classList.add('status-bad');
        }
        elements.connectionStatus.querySelector('span:last-child').textContent = text;
      }

      function updateLastRefresh() {
        const now = new Date();
        elements.lastRefresh.textContent = now.toLocaleTimeString();
      }

      function normalizeLogType(type) {
        const raw = String(type || '').toLowerCase();
        if (raw === 'log' || raw === 'info') return 'info';
        if (raw === 'warning') return 'warning';
        if (raw === 'error' || raw === 'exception' || raw === 'assert') return 'error';
        return 'info';
      }

      function readFilterSelection() {
        return filters
          .filter((input) => input.checked)
          .map((input) => input.dataset.filter);
      }

      async function callTool(toolName, params) {
        return app.callTool(toolName, params || {});
      }

      async function readResourceJson(uri) {
        const result = await app.readResource(uri);
        const text = result && result.contents && result.contents[0] && result.contents[0].text
          ? result.contents[0].text
          : '';
        return text ? JSON.parse(text) : null;
      }

      function extractData(result) {
        if (result && result.data) {
          return result.data;
        }
        if (result && result.content && result.content[0] && result.content[0].text) {
          try {
            return JSON.parse(result.content[0].text);
          } catch (error) {
            return null;
          }
        }
        return null;
      }

      function renderHierarchy(nodes) {
        elements.hierarchyPanel.innerHTML = '';

        if (!Array.isArray(nodes) || nodes.length === 0) {
          elements.hierarchyPanel.innerHTML = '<div class="loading">No hierarchy data available.</div>';
          elements.hierarchySummary.textContent = '0 objects';
          return;
        }

        const list = document.createElement('ul');
        list.className = 'tree';
        nodes.forEach((node) => list.appendChild(buildHierarchyNode(node)));
        elements.hierarchyPanel.appendChild(list);

        const total = countNodes(nodes);
        elements.hierarchySummary.textContent = `${total} objects`; 
      }

      function buildHierarchyNode(node) {
        const li = document.createElement('li');
        const hasChildren = Array.isArray(node.children) && node.children.length > 0;

        if (hasChildren) {
          const details = document.createElement('details');
          details.open = true;
          const summary = document.createElement('summary');
          summary.appendChild(buildNodeLabel(node));
          details.appendChild(summary);

          const childList = document.createElement('ul');
          childList.className = 'tree';
          node.children.forEach((child) => childList.appendChild(buildHierarchyNode(child)));
          details.appendChild(childList);
          li.appendChild(details);
        } else {
          const row = document.createElement('div');
          row.className = 'node-row';
          row.appendChild(buildNodeLabel(node));
          li.appendChild(row);
        }

        return li;
      }

      function buildNodeLabel(node) {
        const row = document.createElement('div');
        row.className = 'node-row';
        if (node.active === false) {
          row.classList.add('inactive');
        }

        const name = document.createElement('span');
        name.className = 'node-name';
        name.textContent = node.name || 'Unnamed';

        const meta = document.createElement('span');
        meta.className = 'node-meta';
        meta.textContent = `#${node.instanceId ?? '--'}`;

        row.appendChild(name);
        row.appendChild(meta);

        return row;
      }

      function countNodes(nodes) {
        let count = 0;
        nodes.forEach((node) => {
          count += 1;
          if (Array.isArray(node.children)) {
            count += countNodes(node.children);
          }
        });
        return count;
      }

      function renderLogs(logs) {
        elements.logsPanel.innerHTML = '';

        if (!Array.isArray(logs) || logs.length === 0) {
          elements.logsPanel.innerHTML = '<div class="loading">No logs returned.</div>';
          return;
        }

        const selected = readFilterSelection();
        const filteredLogs = logs.filter((log) => selected.includes(normalizeLogType(log.type)));

        if (filteredLogs.length === 0) {
          elements.logsPanel.innerHTML = '<div class="loading">No logs match current filters.</div>';
          return;
        }

        filteredLogs.forEach((log) => {
          const container = document.createElement('div');
          container.className = 'log-item';

          const type = normalizeLogType(log.type);
          const typeEl = document.createElement('div');
          typeEl.className = `log-type ${type}`;
          typeEl.textContent = type;

          const time = document.createElement('div');
          time.className = 'log-time';
          time.textContent = log.timestamp || '';

          const message = document.createElement('div');
          message.textContent = log.message || '';

          container.appendChild(typeEl);
          container.appendChild(time);
          container.appendChild(message);
          elements.logsPanel.appendChild(container);
        });
      }

      async function refreshSceneInfo() {
        const result = await callTool('get_scene_info', {});
        const data = extractData(result);
        if (!data || !data.activeScene) {
          elements.activeScene.textContent = 'Unavailable';
          elements.rootCount.textContent = '--';
          return;
        }

        elements.activeScene.textContent = data.activeScene.name || 'Unnamed';
        elements.rootCount.textContent = String(data.activeScene.rootCount ?? '--');
      }

      async function refreshPlayMode() {
        try {
          const result = await callTool('get_play_mode_status', {});
          const data = extractData(result);
          if (!data) {
            elements.playMode.textContent = 'Unknown';
            return;
          }

          const isPlaying = Boolean(data.isPlaying);
          const isPaused = Boolean(data.isPaused);

          if (isPlaying && isPaused) {
            elements.playMode.textContent = 'Play (Paused)';
          } else if (isPlaying) {
            elements.playMode.textContent = 'Play';
          } else {
            elements.playMode.textContent = 'Edit';
          }
        } catch (error) {
          elements.playMode.textContent = 'Unknown';
        }
      }

      async function refreshHierarchy() {
        try {
          const hierarchy = await readResourceJson('unity://scenes_hierarchy');
          state.hierarchy = hierarchy || [];
          renderHierarchy(state.hierarchy);
        } catch (error) {
          elements.hierarchyPanel.innerHTML = '<div class="loading">Failed to load hierarchy.</div>';
        }
      }

      async function refreshLogs() {
        try {
          const limit = Number(elements.logLimit.value) || 100;
          const result = await callTool('get_console_logs', {
            limit,
            includeStackTrace: false
          });
          const data = extractData(result);
          const logs = data && data.logs ? data.logs : data && data.data && data.data.logs ? data.data.logs : data && data.logs ? data.logs : [];
          state.logs = logs || [];
          renderLogs(state.logs);
        } catch (error) {
          elements.logsPanel.innerHTML = '<div class="loading">Failed to load logs.</div>';
        }
      }

      async function refreshAll() {
        await Promise.all([
          refreshSceneInfo(),
          refreshPlayMode(),
          refreshHierarchy(),
          refreshLogs()
        ]);
        updateLastRefresh();
      }

      function startAutoRefresh() {
        stopAutoRefresh();
        const seconds = Math.max(2, Number(elements.refreshInterval.value) || 5);
        state.autoRefreshId = window.setInterval(refreshAll, seconds * 1000);
      }

      function stopAutoRefresh() {
        if (state.autoRefreshId) {
          window.clearInterval(state.autoRefreshId);
          state.autoRefreshId = null;
        }
      }

      elements.refreshButton.addEventListener('click', refreshAll);
      elements.playButton.addEventListener('click', async () => {
        await callTool('execute_menu_item', { menuPath: 'Edit/Play' });
        refreshPlayMode();
      });
      elements.pauseButton.addEventListener('click', async () => {
        await callTool('execute_menu_item', { menuPath: 'Edit/Pause' });
        refreshPlayMode();
      });
      elements.stepButton.addEventListener('click', async () => {
        await callTool('execute_menu_item', { menuPath: 'Edit/Step' });
      });
      elements.autoRefresh.addEventListener('change', () => {
        if (elements.autoRefresh.checked) {
          startAutoRefresh();
        } else {
          stopAutoRefresh();
        }
      });
      elements.refreshInterval.addEventListener('change', () => {
        if (elements.autoRefresh.checked) {
          startAutoRefresh();
        }
      });
      filters.forEach((input) => {
        input.addEventListener('change', () => renderLogs(state.logs));
      });
      elements.logLimit.addEventListener('change', refreshLogs);

      app.on('ready', async () => {
        setConnectionStatus('Connected to MCP', 'good');
        await refreshAll();
      });

      app.on('error', () => {
        setConnectionStatus('Connection error', 'bad');
      });
    </script>
  </body>
</html>
```

##### Step 3 Verification Checklist
- [ ] Open the dashboard via the `show_unity_dashboard` tool and confirm the UI renders.
- [ ] Click Play, Pause, Step and confirm Unity responds.
- [ ] Click Refresh and confirm hierarchy and logs update.
- [ ] Enable Auto refresh and confirm data updates on interval.

#### Step 3 STOP & COMMIT
**STOP & COMMIT:** Agent must stop here and wait for the user to test, stage, and commit the change.

#### Step 4: Add Play Mode Status Tool
- [ ] Create the Unity-side tool at [Editor/Tools/GetPlayModeStatusTool.cs](Editor/Tools/GetPlayModeStatusTool.cs).
- [ ] Copy and paste code below into [Editor/Tools/GetPlayModeStatusTool.cs](Editor/Tools/GetPlayModeStatusTool.cs):

```csharp
using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for getting Unity play mode status
    /// </summary>
    public class GetPlayModeStatusTool : McpToolBase
    {
        public GetPlayModeStatusTool()
        {
            Name = "get_play_mode_status";
            Description = "Gets Unity play mode status (isPlaying, isPaused)";
        }

        /// <summary>
        /// Execute the GetPlayModeStatus tool with the provided parameters
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                bool isPlaying = EditorApplication.isPlaying;
                bool isPaused = EditorApplication.isPaused;

                var result = new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = isPlaying ? (isPaused ? "Play mode (paused)" : "Play mode") : "Edit mode",
                    ["isPlaying"] = isPlaying,
                    ["isPaused"] = isPaused
                };

                McpLogger.LogInfo($"Play mode status: isPlaying={isPlaying}, isPaused={isPaused}");

                return result;
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error getting play mode status: {ex.Message}",
                    "play_mode_status_error"
                );
            }
        }
    }
}
```

- [ ] Register the Unity tool in the MCP Unity server.
- [ ] Copy and paste code below into [Editor/UnityBridge/McpUnityServer.cs](Editor/UnityBridge/McpUnityServer.cs):

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEngine;
using McpUnity.Tools;
using McpUnity.Resources;
using McpUnity.Services;
using McpUnity.Utils;
using WebSocketSharp.Server;
using System.IO;
using System.Net.Sockets;
using UnityEditor.Callbacks;

namespace McpUnity.Unity
{
    /// <summary>
    /// Custom WebSocket close codes for Unity-specific events.
    /// Range 4000-4999 is reserved for application use.
    /// </summary>
    public static class UnityCloseCode
    {
        /// <summary>
        /// Unity is entering Play mode - clients should use fast polling instead of backoff
        /// </summary>
        public const ushort PlayMode = 4001;
    }

    /// <summary>
    /// MCP Unity Server to communicate Node.js MCP server.
    /// Uses WebSockets to communicate with Node.js.
    /// </summary>
    [InitializeOnLoad]
    public class McpUnityServer : IDisposable
    {
        private static McpUnityServer _instance;

        private readonly Dictionary<string, McpToolBase> _tools = new Dictionary<string, McpToolBase>();
        private readonly Dictionary<string, McpResourceBase> _resources = new Dictionary<string, McpResourceBase>();

        private WebSocketServer _webSocketServer;
        private CancellationTokenSource _cts;
        private TestRunnerService _testRunnerService;
        private ConsoleLogsService _consoleLogsService;
        
        /// <summary>
        /// Called after every domain reload
        /// </summary>
        [DidReloadScripts]
        private static void AfterReload()
        {
            // Skip initialization in batch mode (Unity Cloud Build, CI, headless builds)
            // This prevents npm commands from hanging the build process
            if (Application.isBatchMode)
            {
                return;
            }
            
            // Ensure Instance is created and hooks are set up after initial domain load
            var currentInstance = Instance;
        }
        
        /// <summary>
        /// Singleton instance accessor. Returns null in batch mode.
        /// </summary>
        public static McpUnityServer Instance
        {
            get
            {
                // Don't create instance in batch mode to avoid hanging builds
                if (Application.isBatchMode)
                {
                    return null;
                }
                
                if (_instance == null)
                {
                    _instance = new McpUnityServer();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Current Listening state
        /// </summary>
        public bool IsListening => _webSocketServer?.IsListening ?? false;

        /// <summary>
        /// Dictionary of connected clients with this server
        /// </summary>
        public Dictionary<string, string> Clients { get; } = new Dictionary<string, string>();

        /// <summary>
        /// Private constructor to enforce singleton pattern
        /// </summary>
        private McpUnityServer()
        {
            // Skip all initialization in batch mode (Unity Cloud Build, CI, headless builds)
            // The npm install/build commands can hang indefinitely without node.js available
            if (Application.isBatchMode)
            {
                McpLogger.LogInfo("MCP Unity server disabled: Running in batch mode (Unity Cloud Build or CI)");
                return;
            }
            
            EditorApplication.quitting -= OnEditorQuitting; // Prevent multiple subscriptions on domain reload
            EditorApplication.quitting += OnEditorQuitting;

            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            InstallServer();
            InitializeServices();
            RegisterResources();
            RegisterTools();

            // Initial start if auto-start is enabled and not recovering from a reload where it was off
            if (McpUnitySettings.Instance.AutoStartServer)
            {
                 StartServer();
            }
        }

        /// <summary>
        /// Disposes the McpUnityServer instance, stopping the WebSocket server and unsubscribing from Unity Editor events.
        /// This method ensures proper cleanup of resources and prevents memory leaks or unexpected behavior during domain reloads or editor shutdown.
        /// </summary>
        public void Dispose()
        {
            StopServer();

            EditorApplication.quitting -= OnEditorQuitting;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// Start the WebSocket Server to communicate with Node.js
        /// </summary>
        public void StartServer()
        {
            // Skip starting server if this is a Multiplayer Play Mode clone instance
            // Only the main editor should run the WebSocket server to avoid port conflicts
            if (McpUtils.IsMultiplayerPlayModeClone())
            {
                McpLogger.LogInfo("Server startup skipped: Running as Multiplayer Play Mode clone instance. Only the main editor runs the MCP server.");
                return;
            }

            if (IsListening)
            {
                McpLogger.LogInfo($"Server start requested, but already listening on port {McpUnitySettings.Instance.Port}.");
                return;
            }

            try
            {
                var host = McpUnitySettings.Instance.AllowRemoteConnections ? "0.0.0.0" : "localhost";
                _webSocketServer = new WebSocketServer($"ws://{host}:{McpUnitySettings.Instance.Port}");
                _webSocketServer.ReuseAddress = true;
                _webSocketServer.AddWebSocketService("/McpUnity", () => new McpUnitySocketHandler(this));
                _webSocketServer.Start();
                McpLogger.LogInfo($"WebSocket server started successfully on {host}:{McpUnitySettings.Instance.Port}.");
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                McpLogger.LogError($"Failed to start WebSocket server: Port {McpUnitySettings.Instance.Port} is already in use. {ex.Message}");
            }
            catch (Exception ex)
            {
                McpLogger.LogError($"Failed to start WebSocket server: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Stop the WebSocket server
        /// </summary>
        /// <param name="closeCode">Optional custom close code to send to clients before stopping</param>
        /// <param name="closeReason">Optional reason message for the close</param>
        public void StopServer(ushort? closeCode = null, string closeReason = null)
        {
            if (!IsListening)
            {
                return;
            }

            try
            {
                // If a custom close code is provided, close all client connections with that code first
                if (closeCode.HasValue && _webSocketServer != null)
                {
                    CloseAllClients(closeCode.Value, closeReason ?? "Server stopping");
                }

                _webSocketServer?.Stop();

                McpLogger.LogInfo("WebSocket server stopped");
            }
            catch (Exception ex)
            {
                McpLogger.LogError($"Error during WebSocketServer.Stop(): {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                _webSocketServer = null;
                Clients.Clear();
                McpLogger.LogInfo("WebSocket server stopped and resources cleaned up.");
            }
        }

        /// <summary>
        /// Close all connected clients with a specific close code
        /// </summary>
        /// <param name="closeCode">WebSocket close code (4000-4999 for application use)</param>
        /// <param name="reason">Reason message for the close</param>
        private void CloseAllClients(ushort closeCode, string reason)
        {
            if (_webSocketServer == null)
            {
                return;
            }

            try
            {
                var service = _webSocketServer.WebSocketServices["/McpUnity"];
                if (service?.Sessions != null)
                {
                    // Get all active session IDs and close each with the custom code
                    var sessionIds = new List<string>(service.Sessions.IDs);
                    foreach (var sessionId in sessionIds)
                    {
                        service.Sessions.CloseSession(sessionId, closeCode, reason);
                    }
                    McpLogger.LogInfo($"Closed {sessionIds.Count} client connection(s) with code {closeCode}: {reason}");
                }
            }
            catch (Exception ex)
            {
                McpLogger.LogError($"Error closing client connections: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Try to get a tool by name
        /// </summary>
        public bool TryGetTool(string name, out McpToolBase tool)
        {
            return _tools.TryGetValue(name, out tool);
        }
        
        /// <summary>
        /// Register all available tools
        /// </summary>
        private void RegisterTools()
        {
            // Register MenuItemTool
            MenuItemTool menuItemTool = new MenuItemTool();
            _tools.Add(menuItemTool.Name, menuItemTool);
            
            // Register SelectGameObjectTool
            SelectGameObjectTool selectGameObjectTool = new SelectGameObjectTool();
            _tools.Add(selectGameObjectTool.Name, selectGameObjectTool);

            // Register UpdateGameObjectTool
            UpdateGameObjectTool updateGameObjectTool = new UpdateGameObjectTool();
            _tools.Add(updateGameObjectTool.Name, updateGameObjectTool);
            
            // Register PackageManagerTool
            AddPackageTool addPackageTool = new AddPackageTool();
            _tools.Add(addPackageTool.Name, addPackageTool);
            
            // Register RunTestsTool
            RunTestsTool runTestsTool = new RunTestsTool(_testRunnerService);
            _tools.Add(runTestsTool.Name, runTestsTool);
            
            // Register SendConsoleLogTool
            SendConsoleLogTool sendConsoleLogTool = new SendConsoleLogTool();
            _tools.Add(sendConsoleLogTool.Name, sendConsoleLogTool);
            
            // Register UpdateComponentTool
            UpdateComponentTool updateComponentTool = new UpdateComponentTool();
            _tools.Add(updateComponentTool.Name, updateComponentTool);
            
            // Register AddAssetToSceneTool
            AddAssetToSceneTool addAssetToSceneTool = new AddAssetToSceneTool();
            _tools.Add(addAssetToSceneTool.Name, addAssetToSceneTool);
            
            // Register CreatePrefabTool
            CreatePrefabTool createPrefabTool = new CreatePrefabTool();
            _tools.Add(createPrefabTool.Name, createPrefabTool);

            // Register CreateSceneTool
            CreateSceneTool createSceneTool = new CreateSceneTool();
            _tools.Add(createSceneTool.Name, createSceneTool);

            // Register DeleteSceneTool
            DeleteSceneTool deleteSceneTool = new DeleteSceneTool();
            _tools.Add(deleteSceneTool.Name, deleteSceneTool);

            // Register LoadSceneTool
            LoadSceneTool loadSceneTool = new LoadSceneTool();
            _tools.Add(loadSceneTool.Name, loadSceneTool);

            // Register SaveSceneTool
            SaveSceneTool saveSceneTool = new SaveSceneTool();
            _tools.Add(saveSceneTool.Name, saveSceneTool);

            // Register GetSceneInfoTool
            GetSceneInfoTool getSceneInfoTool = new GetSceneInfoTool();
            _tools.Add(getSceneInfoTool.Name, getSceneInfoTool);

            // Register GetPlayModeStatusTool
            GetPlayModeStatusTool getPlayModeStatusTool = new GetPlayModeStatusTool();
            _tools.Add(getPlayModeStatusTool.Name, getPlayModeStatusTool);

            // Register UnloadSceneTool
            UnloadSceneTool unloadSceneTool = new UnloadSceneTool();
            _tools.Add(unloadSceneTool.Name, unloadSceneTool);

            // Register RecompileScriptsTool
            RecompileScriptsTool recompileScriptsTool = new RecompileScriptsTool();
            _tools.Add(recompileScriptsTool.Name, recompileScriptsTool);
            
            // Register GetGameObjectTool
            GetGameObjectTool getGameObjectTool = new GetGameObjectTool();
            _tools.Add(getGameObjectTool.Name, getGameObjectTool);

            // Register DuplicateGameObjectTool
            DuplicateGameObjectTool duplicateGameObjectTool = new DuplicateGameObjectTool();
            _tools.Add(duplicateGameObjectTool.Name, duplicateGameObjectTool);

            // Register DeleteGameObjectTool
            DeleteGameObjectTool deleteGameObjectTool = new DeleteGameObjectTool();
            _tools.Add(deleteGameObjectTool.Name, deleteGameObjectTool);

            // Register ReparentGameObjectTool
            ReparentGameObjectTool reparentGameObjectTool = new ReparentGameObjectTool();
            _tools.Add(reparentGameObjectTool.Name, reparentGameObjectTool);

            // Register Transform Tools
            MoveGameObjectTool moveGameObjectTool = new MoveGameObjectTool();
            _tools.Add(moveGameObjectTool.Name, moveGameObjectTool);

            RotateGameObjectTool rotateGameObjectTool = new RotateGameObjectTool();
            _tools.Add(rotateGameObjectTool.Name, rotateGameObjectTool);

            ScaleGameObjectTool scaleGameObjectTool = new ScaleGameObjectTool();
            _tools.Add(scaleGameObjectTool.Name, scaleGameObjectTool);

            SetTransformTool setTransformTool = new SetTransformTool();
            _tools.Add(setTransformTool.Name, setTransformTool);

            // Register Material Tools
            CreateMaterialTool createMaterialTool = new CreateMaterialTool();
            _tools.Add(createMaterialTool.Name, createMaterialTool);

            AssignMaterialTool assignMaterialTool = new AssignMaterialTool();
            _tools.Add(assignMaterialTool.Name, assignMaterialTool);

            ModifyMaterialTool modifyMaterialTool = new ModifyMaterialTool();
            _tools.Add(modifyMaterialTool.Name, modifyMaterialTool);

            GetMaterialInfoTool getMaterialInfoTool = new GetMaterialInfoTool();
            _tools.Add(getMaterialInfoTool.Name, getMaterialInfoTool);

            // Register BatchExecuteTool (must be registered last as it needs access to other tools)
            BatchExecuteTool batchExecuteTool = new BatchExecuteTool(this);
            _tools.Add(batchExecuteTool.Name, batchExecuteTool);
        }
        
        /// <summary>
        /// Register all available resources
        /// </summary>
        private void RegisterResources()
        {
            // Register GetMenuItemsResource
            GetMenuItemsResource getMenuItemsResource = new GetMenuItemsResource();
            _resources.Add(getMenuItemsResource.Name, getMenuItemsResource);
            
            // Register GetConsoleLogsResource
            GetConsoleLogsResource getConsoleLogsResource = new GetConsoleLogsResource(_consoleLogsService);
            _resources.Add(getConsoleLogsResource.Name, getConsoleLogsResource);
            
            // Register GetScenesHierarchyResource
            GetScenesHierarchyResource getScenesHierarchyResource = new GetScenesHierarchyResource();
            _resources.Add(getScenesHierarchyResource.Name, getScenesHierarchyResource);
            
            // Register GetPackagesResource
            GetPackagesResource getPackagesResource = new GetPackagesResource();
            _resources.Add(getPackagesResource.Name, getPackagesResource);
            
            // Register GetAssetsResource
            GetAssetsResource getAssetsResource = new GetAssetsResource();
            _resources.Add(getAssetsResource.Name, getAssetsResource);
            
            // Register GetTestsResource
            GetTestsResource getTestsResource = new GetTestsResource(_testRunnerService);
            _resources.Add(getTestsResource.Name, getTestsResource);
            
            // Register GetGameObjectResource
            GetGameObjectResource getGameObjectResource = new GetGameObjectResource();
            _resources.Add(getGameObjectResource.Name, getGameObjectResource);
        }
        
        /// <summary>
        /// Initialize services used by the server
        /// </summary>
        private void InitializeServices()
        {
            // Initialize the test runner service
            _testRunnerService = new TestRunnerService();
            
            // Initialize the console logs service
            _consoleLogsService = new ConsoleLogsService();
        }

        /// <summary>
        /// Handles the Unity Editor quitting event. Ensures the server is properly stopped and disposed.
        /// </summary>
        private static void OnEditorQuitting()
        {
            if (Application.isBatchMode || _instance == null) return;
            
            McpLogger.LogInfo("Editor is quitting. Ensuring server is stopped.");
            _instance.Dispose();
        }

        /// <summary>
        /// Handles the Unity Editor's 'before assembly reload' event.
        /// Stops the WebSocket server to prevent port conflicts and ensure a clean state before scripts are recompiled.
        /// </summary>
        private static void OnBeforeAssemblyReload()
        {
            if (Application.isBatchMode || _instance == null) return;
            
            // Only stop server if it's currently running
            if (_instance.IsListening)
            {
                McpLogger.LogInfo("Assembly reload detected. Stopping MCP server.");
                _instance.StopServer();
            }
        }

        /// <summary>
        /// Handles the Unity Editor's 'after assembly reload' event.
        /// Restarts the WebSocket server if it was running before the reload and auto-start is enabled.
        /// </summary>
        private static void OnAfterAssemblyReload()
        {
            if (Application.isBatchMode || _instance == null) return;
            
            // Restart server if auto-start is enabled
            if (McpUnitySettings.Instance.AutoStartServer)
            {
                McpLogger.LogInfo("Assembly reload complete. Restarting MCP server if needed.");
                _instance.StartServer();
            }
        }

        /// <summary>
        /// Handles Unity Editor play mode state changes.
        /// </summary>
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (Application.isBatchMode) return;
            
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                McpLogger.LogInfo("Unity entering Play Mode.");
                StopServer(UnityCloseCode.PlayMode, "Unity entering play mode");
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                McpLogger.LogInfo("Unity exited Play Mode.");
                StartServer();
            }
        }

        /// <summary>
        /// Get the current WebSocket server instance
        /// </summary>
        public WebSocketServer WebSocketServer => _webSocketServer;

        /// <summary>
        /// Install MCP Unity server on first load
        /// </summary>
        private void InstallServer()
        {
            if (Application.isBatchMode)
            {
                return;
            }
            
            // Check if the server is already installed
            if (McpUtils.IsServerInstalled())
            {
                return;
            }

            try
            {
                McpLogger.LogInfo("Installing MCP Unity server...");
                McpUtils.InstallServer();
            }
            catch (Exception ex)
            {
                McpLogger.LogError($"Failed to install MCP Unity server: {ex.Message}");
            }
        }
    }
}
```

- [ ] Create the Node tool at [Server~/src/tools/getPlayModeStatusTool.ts](Server~/src/tools/getPlayModeStatusTool.ts).
- [ ] Copy and paste code below into [Server~/src/tools/getPlayModeStatusTool.ts](Server~/src/tools/getPlayModeStatusTool.ts):

```typescript
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import * as z from 'zod';
import { Logger } from '../utils/logger.js';

const toolName = 'get_play_mode_status';
const toolDescription = 'Gets Unity play mode status (isPlaying, isPaused).';

const paramsSchema = z.object({});

export function registerGetPlayModeStatusTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

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

async function toolHandler(mcpUnity: McpUnity, params: any) {
  const response = await mcpUnity.sendRequest({
    method: toolName,
    params
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to get play mode status'
    );
  }

  const statusText = response.isPlaying
    ? (response.isPaused ? 'Play mode (paused)' : 'Play mode')
    : 'Edit mode';

  return {
    content: [
      {
        type: response.type as 'text',
        text: statusText
      }
    ],
    data: {
      isPlaying: response.isPlaying,
      isPaused: response.isPaused
    }
  };
}
```

- [ ] Register the Node tool in the MCP server.
- [ ] Copy and paste code below into [Server~/src/index.ts](Server~/src/index.ts):

```typescript
// Import MCP SDK components
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { McpUnity } from './unity/mcpUnity.js';
import { Logger, LogLevel } from './utils/logger.js';
import { registerCreateSceneTool } from './tools/createSceneTool.js';
import { registerMenuItemTool } from './tools/menuItemTool.js';
import { registerSelectGameObjectTool } from './tools/selectGameObjectTool.js';
import { registerAddPackageTool } from './tools/addPackageTool.js';
import { registerRunTestsTool } from './tools/runTestsTool.js';
import { registerSendConsoleLogTool } from './tools/sendConsoleLogTool.js';
import { registerGetConsoleLogsTool } from './tools/getConsoleLogsTool.js';
import { registerUpdateComponentTool } from './tools/updateComponentTool.js';
import { registerAddAssetToSceneTool } from './tools/addAssetToSceneTool.js';
import { registerUpdateGameObjectTool } from './tools/updateGameObjectTool.js';
import { registerCreatePrefabTool } from './tools/createPrefabTool.js';
import { registerDeleteSceneTool } from './tools/deleteSceneTool.js';
import { registerLoadSceneTool } from './tools/loadSceneTool.js';
import { registerSaveSceneTool } from './tools/saveSceneTool.js';
import { registerGetSceneInfoTool } from './tools/getSceneInfoTool.js';
import { registerGetPlayModeStatusTool } from './tools/getPlayModeStatusTool.js';
import { registerUnloadSceneTool } from './tools/unloadSceneTool.js';
import { registerRecompileScriptsTool } from './tools/recompileScriptsTool.js';
import { registerGetGameObjectTool } from './tools/getGameObjectTool.js';
import { registerTransformTools } from './tools/transformTools.js';
import { registerCreateMaterialTool, registerAssignMaterialTool, registerModifyMaterialTool, registerGetMaterialInfoTool } from './tools/materialTools.js';
import { registerDuplicateGameObjectTool, registerDeleteGameObjectTool, registerReparentGameObjectTool } from './tools/gameObjectTools.js';
import { registerBatchExecuteTool } from './tools/batchExecuteTool.js';
import { registerShowUnityDashboardTool } from './tools/showUnityDashboardTool.js';
import { registerGetMenuItemsResource } from './resources/getMenuItemResource.js';
import { registerGetConsoleLogsResource } from './resources/getConsoleLogsResource.js';
import { registerGetHierarchyResource } from './resources/getScenesHierarchyResource.js';
import { registerGetPackagesResource } from './resources/getPackagesResource.js';
import { registerGetAssetsResource } from './resources/getAssetsResource.js';
import { registerGetTestsResource } from './resources/getTestsResource.js';
import { registerGetGameObjectResource } from './resources/getGameObjectResource.js';
import { registerUnityDashboardAppResource } from './resources/unityDashboardAppResource.js';
import { registerGameObjectHandlingPrompt } from './prompts/gameobjectHandlingPrompt.js';

// Initialize loggers
const serverLogger = new Logger('Server', LogLevel.INFO);
const unityLogger = new Logger('Unity', LogLevel.INFO);
const toolLogger = new Logger('Tools', LogLevel.INFO);
const resourceLogger = new Logger('Resources', LogLevel.INFO);

// Initialize the MCP server
const server = new McpServer (
  {
    name: "MCP Unity Server",
    version: "1.0.0"
  },
  {
    capabilities: {
      tools: {},
      resources: {},
      prompts: {},
    },
  }
);

// Initialize MCP HTTP bridge with Unity editor
const mcpUnity = new McpUnity(unityLogger);

// Register all tools into the MCP server
registerMenuItemTool(server, mcpUnity, toolLogger);
registerSelectGameObjectTool(server, mcpUnity, toolLogger);
registerAddPackageTool(server, mcpUnity, toolLogger);
registerRunTestsTool(server, mcpUnity, toolLogger);
registerSendConsoleLogTool(server, mcpUnity, toolLogger);
registerGetConsoleLogsTool(server, mcpUnity, toolLogger);
registerUpdateComponentTool(server, mcpUnity, toolLogger);
registerAddAssetToSceneTool(server, mcpUnity, toolLogger);
registerUpdateGameObjectTool(server, mcpUnity, toolLogger);
registerCreatePrefabTool(server, mcpUnity, toolLogger);
registerCreateSceneTool(server, mcpUnity, toolLogger);
registerDeleteSceneTool(server, mcpUnity, toolLogger);
registerLoadSceneTool(server, mcpUnity, toolLogger);
registerSaveSceneTool(server, mcpUnity, toolLogger);
registerGetSceneInfoTool(server, mcpUnity, toolLogger);
registerGetPlayModeStatusTool(server, mcpUnity, toolLogger);
registerShowUnityDashboardTool(server, toolLogger);
registerUnloadSceneTool(server, mcpUnity, toolLogger);
registerRecompileScriptsTool(server, mcpUnity, toolLogger);
registerGetGameObjectTool(server, mcpUnity, toolLogger);
registerTransformTools(server, mcpUnity, toolLogger);
registerDuplicateGameObjectTool(server, mcpUnity, toolLogger);
registerDeleteGameObjectTool(server, mcpUnity, toolLogger);
registerReparentGameObjectTool(server, mcpUnity, toolLogger);

// Register Material Tools
registerCreateMaterialTool(server, mcpUnity, toolLogger);
registerAssignMaterialTool(server, mcpUnity, toolLogger);
registerModifyMaterialTool(server, mcpUnity, toolLogger);
registerGetMaterialInfoTool(server, mcpUnity, toolLogger);

// Register Batch Execute Tool (high-priority for performance)
registerBatchExecuteTool(server, mcpUnity, toolLogger);

// Register all resources into the MCP server
registerGetTestsResource(server, mcpUnity, resourceLogger);
registerGetGameObjectResource(server, mcpUnity, resourceLogger);
registerGetMenuItemsResource(server, mcpUnity, resourceLogger);
registerGetConsoleLogsResource(server, mcpUnity, resourceLogger);
registerGetHierarchyResource(server, mcpUnity, resourceLogger);
registerGetPackagesResource(server, mcpUnity, resourceLogger);
registerGetAssetsResource(server, mcpUnity, resourceLogger);
registerUnityDashboardAppResource(server, resourceLogger);

// Register all prompts into the MCP server
registerGameObjectHandlingPrompt(server);

// Server startup function
async function startServer() {
  try {
    // Initialize STDIO transport for MCP client communication
    const stdioTransport = new StdioServerTransport();
    
    // Connect the server to the transport
    await server.connect(stdioTransport);

    serverLogger.info('MCP Server started');
    
    // Get the client name from the MCP server
    const clientName = server.server.getClientVersion()?.name || 'Unknown MCP Client';
    serverLogger.info(`Connected MCP client: ${clientName}`);
    
    // Start Unity Bridge connection with client name in headers
    await mcpUnity.start(clientName);
    
  } catch (error) {
    serverLogger.error('Failed to start server', error);
    process.exit(1);
  }
}

// Graceful shutdown handler
let isShuttingDown = false;
async function shutdown() {
  if (isShuttingDown) return;
  isShuttingDown = true;

  try {
    serverLogger.info('Shutting down...');
    await mcpUnity.stop();
    await server.close();
  } catch (error) {
    // Ignore errors during shutdown
  }
  process.exit(0);
}

// Start the server
startServer();

// Handle shutdown signals
process.on('SIGINT', shutdown);
process.on('SIGTERM', shutdown);
process.on('SIGHUP', shutdown);

// Handle stdin close (when MCP client disconnects)
process.stdin.on('close', shutdown);
process.stdin.on('end', shutdown);
process.stdin.on('error', shutdown);

// Handle uncaught exceptions - exit cleanly if it's just a closed pipe
process.on('uncaughtException', (error: NodeJS.ErrnoException) => {
  // EPIPE/EOF errors are expected when the MCP client disconnects
  if (error.code === 'EPIPE' || error.code === 'EOF' || error.code === 'ERR_USE_AFTER_CLOSE') {
    shutdown();
    return;
  }
  serverLogger.error('Uncaught exception', error);
  process.exit(1);
});

// Handle unhandled promise rejections
process.on('unhandledRejection', (reason) => {
  serverLogger.error('Unhandled rejection', reason);
  process.exit(1);
});
```

##### Step 4 Verification Checklist
- [ ] In Unity, call the `get_play_mode_status` tool in MCP Inspector and confirm `isPlaying` toggles when entering Play mode.
- [ ] Open the dashboard and confirm the Play Mode card updates when switching modes.

#### Step 4 STOP & COMMIT
**STOP & COMMIT:** Agent must stop here and wait for the user to test, stage, and commit the change.

#### Step 5: Update Documentation and Add Usage Guide
- [ ] Create the usage guide at [plans/unity-dashboard-mcp-app/usage.md](plans/unity-dashboard-mcp-app/usage.md).
- [ ] Copy and paste code below into [plans/unity-dashboard-mcp-app/usage.md](plans/unity-dashboard-mcp-app/usage.md):

```markdown
# Unity Dashboard MCP App Usage

## Requirements
- VS Code 1.109 or later
- MCP Unity server built with the dashboard resource
- Unity Editor running with MCP Unity server enabled

## Open the Dashboard
1. Build the Node server: `cd Server~ && npm run build`.
2. Start the MCP Inspector or your MCP client.
3. Call the `show_unity_dashboard` tool.
4. The dashboard opens as a VS Code MCP App.

## What You Can Do
- View the active scene name and root object count.
- Inspect a collapsible scene hierarchy tree.
- View Unity console logs with filtering by level.
- Use Play, Pause, and Step controls.
- Enable auto-refresh and tune the refresh interval.

## Troubleshooting
- If the dashboard does not render, confirm VS Code is 1.109+ and the tool response includes metadata `{ "view": "mcp-app" }`.
- If scene data fails to load, verify Unity is connected and the MCP server logs show a healthy WebSocket connection.
```

- [ ] Update the tools and resources list in [AGENTS.md](AGENTS.md).
- [ ] Copy and paste code below into [AGENTS.md](AGENTS.md):

```markdown
## MCP Unity — AI Agent Guide (MCP Package)

### Purpose (what this repo is)
**MCP Unity** exposes Unity Editor capabilities to MCP-enabled clients by running:
- **Unity-side “client” (C# Editor scripts)**: a WebSocket server inside the Unity Editor that executes tools/resources.
- **Node-side “server” (TypeScript)**: an MCP stdio server that registers MCP tools/resources and forwards requests to Unity over WebSocket.

### How it works (high-level data flow)
- **MCP client** ⇄ (stdio / MCP SDK) ⇄ **Node server** (`Server~/src/index.ts`)
- **Node server** ⇄ (WebSocket JSON-RPC-ish) ⇄ **Unity Editor** (`Editor/UnityBridge/McpUnityServer.cs` + `McpUnitySocketHandler.cs`)
- **Tool/Resource names must match exactly** across Node and Unity (typically `lower_snake_case`).

### Key defaults & invariants
- **Unity WebSocket endpoint**: `ws://localhost:8090/McpUnity` by default.
- **Config file**: `ProjectSettings/McpUnitySettings.json` (written/read by Unity; read opportunistically by Node).
- **Execution thread**: Tool/resource execution is dispatched via `EditorCoroutineUtility` and runs on the **Unity main thread**. Keep synchronous work short; use async patterns for long work.

### Repo layout (where to change what)
```
/
├── Editor/                       # Unity Editor package code (C#)
│   ├── Tools/                    # Tools (inherit McpToolBase)
│   ├── Resources/                # Resources (inherit McpResourceBase)
│   ├── UnityBridge/              # WebSocket server + message routing
│   ├── Services/                 # Test/log services used by tools/resources
│   └── Utils/                    # Shared helpers (config, logging, workspace integration)
├── Server~/                      # Node MCP server (TypeScript, ESM)
│   ├── src/index.ts              # Registers tools/resources/prompts with MCP SDK
│   ├── src/tools/                # MCP tool definitions (zod schema + handler)
│   ├── src/resources/            # MCP resource definitions
│   └── src/unity/mcpUnity.ts      # WebSocket client that talks to Unity
└── server.json                   # MCP registry metadata (name/version/package)
```

### Quickstart (local dev)
- **Unity side**
  - Open the Unity project that has this package installed.
  - Ensure the server is running (auto-start is controlled by `McpUnitySettings.AutoStartServer`).
  - Settings persist in `ProjectSettings/McpUnitySettings.json`.

- **Node side (build)**
  - `cd Server~ && npm run build`
  - The MCP entrypoint is `Server~/build/index.js` (published as an MCP stdio server).

- **Node side (debug/inspect)**
  - `cd Server~ && npm run inspector` to use the MCP Inspector.

### Configuration (Unity ↔ Node bridge)
The Unity settings file is the shared contract:
- **Path**: `ProjectSettings/McpUnitySettings.json`
- **Fields**
  - **Port** (default **8090**): Unity WebSocket server port.
  - **RequestTimeoutSeconds** (default **10**): Node request timeout (Node reads this if the settings file is discoverable).
  - **AllowRemoteConnections** (default **false**): Unity binds to `0.0.0.0` when enabled; otherwise `localhost`.
  - **EnableInfoLogs**: Unity console logging verbosity.
  - **NpmExecutablePath**: optional npm path for Unity-driven install/build.

Node reads config from `../ProjectSettings/McpUnitySettings.json` relative to **its current working directory**. If not found, Node falls back to:
- **host**: `localhost`
- **port**: `8090`
- **timeout**: `10s`

**Remote connection note**:
- If Unity is on another machine, set `AllowRemoteConnections=true` in Unity and set `UNITY_HOST=<unity_machine_ip_or_hostname>` for the Node process.

### Adding a new capability

### Add a tool
1. **Unity (C#)**
   - Add `Editor/Tools/<YourTool>Tool.cs` inheriting `McpToolBase`.
   - Set `Name` to the MCP tool name (recommended: `lower_snake_case`).
   - Implement:
     - `Execute(JObject parameters)` for synchronous work, or
     - set `IsAsync = true` and implement `ExecuteAsync(JObject parameters, TaskCompletionSource<JObject> tcs)` for long-running operations.
   - Register it in `Editor/UnityBridge/McpUnityServer.cs` (`RegisterTools()`).

2. **Node (TypeScript)**
   - Add `Server~/src/tools/<yourTool>Tool.ts`.
   - Register the tool in `Server~/src/index.ts`.
   - Use a zod schema for params; forward to Unity using the same `method` string:
     - `mcpUnity.sendRequest({ method: toolName, params: {...} })`

3. **Build**
   - `cd Server~ && npm run build`

### Add a resource
1. **Unity (C#)**
   - Add `Editor/Resources/<YourResource>Resource.cs` inheriting `McpResourceBase`.
   - Set `Name` (method string) and `Uri` (e.g. `unity://...`).
   - Implement `Fetch(...)` or `FetchAsync(...)`.
   - Register in `Editor/UnityBridge/McpUnityServer.cs` (`RegisterResources()`).

2. **Node (TypeScript)**
   - Add `Server~/src/resources/<yourResource>.ts`, register in `Server~/src/index.ts`.
   - Forward to Unity via `mcpUnity.sendRequest({ method: resourceName, params: {} })`.

### Logging & debugging
- **Unity**
  - Uses `McpUnity.Utils.McpLogger` (info logs gated by `EnableInfoLogs`).
  - Connection lifecycle is managed in `Editor/UnityBridge/McpUnityServer.cs` (domain reload & playmode transitions stop/restart the server).

- **Node**
  - Logging is controlled by env vars:
    - `LOGGING=true` enables console logging.
    - `LOGGING_FILE=true` writes `log.txt` in the Node process working directory.

### Common pitfalls
- **Port mismatch**: Unity default is **8090**; update docs/config if you change it.
- **Name mismatch**: Node `toolName`/`resourceName` must equal Unity `Name` exactly, or Unity responds `unknown_method`.
- **Long main-thread work**: synchronous `Execute()` blocks the Unity editor; use async patterns for heavy operations.
- **Remote connections**: Unity must bind `0.0.0.0` (`AllowRemoteConnections=true`) and Node must target the correct host (`UNITY_HOST`).
- **Unity domain reload**: the server stops during script reloads and may restart; avoid relying on persistent in-memory state across reloads.
- **Multiplayer Play Mode**: Clone instances automatically skip server startup; only the main editor hosts the MCP server.

### Release/version bump checklist
- Update versions consistently:
  - Unity package `package.json` (`version`)
  - Node server `Server~/package.json` (`version`)
  - MCP registry `server.json` (`version` + npm identifier/version)
- Rebuild Node output: `cd Server~ && npm run build`

### Available tools (current)
- `show_unity_dashboard` — Open the Unity dashboard MCP App in VS Code
- `execute_menu_item` — Execute Unity menu items
- `select_gameobject` — Select GameObjects in hierarchy
- `update_gameobject` — Update or create GameObject properties
- `update_component` — Update or add components on GameObjects
- `add_package` — Install packages via Package Manager
- `run_tests` — Run Unity Test Runner tests
- `send_console_log` — Send logs to Unity console
- `add_asset_to_scene` — Add assets to scene
- `create_prefab` — Create prefabs with optional scripts
- `create_scene` — Create and save new scenes
- `load_scene` — Load scenes (single or additive)
- `delete_scene` — Delete scenes and remove from Build Settings
- `save_scene` — Save current scene (with optional Save As)
- `get_scene_info` — Get active scene info and loaded scenes list
- `get_play_mode_status` — Get Unity play mode status
- `unload_scene` — Unload scene from hierarchy
- `get_gameobject` — Get detailed GameObject info
- `get_console_logs` — Retrieve Unity console logs
- `recompile_scripts` — Recompile all project scripts
- `duplicate_gameobject` — Duplicate GameObjects with optional rename/reparent
- `delete_gameobject` — Delete GameObjects from scene
- `reparent_gameobject` — Change GameObject parent in hierarchy
- `create_material` — Create materials with specified shader
- `assign_material` — Assign materials to Renderer components
- `modify_material` — Modify material properties (colors, floats, textures)
- `get_material_info` — Get material details including all properties

### Available resources (current)
- `unity://ui/dashboard` — Unity dashboard MCP App UI
- `unity://menu-items` — List of available menu items
- `unity://scenes-hierarchy` — Current scene hierarchy
- `unity://gameobject/{id}` — GameObject details by ID or path
- `unity://logs` — Unity console logs
- `unity://packages` — Installed and available packages
- `unity://assets` — Asset database information
- `unity://tests/{testMode}` — Test Runner test information

### Update policy (for agents)
- Update this file when:
  - tools/resources/prompts are added/removed/renamed,
  - config shape or default ports/paths change,
  - the bridge protocol changes (request/response contract).
- Keep it **high-signal**: where to edit code, how to run/build/debug, and the invariants that prevent subtle breakage.
```

- [ ] Update the main README to include the MCP App dashboard tool and resource.
- [ ] Copy and paste code below into [README.md](README.md):

```markdown
# MCP Unity Editor (Game Engine)

[![](https://badge.mcpx.dev?status=on 'MCP Enabled')](https://modelcontextprotocol.io/introduction)
[![](https://img.shields.io/badge/Unity-000000?style=flat&logo=unity&logoColor=white 'Unity')](https://unity.com/releases/editor/archive)
[![](https://img.shields.io/badge/Node.js-339933?style=flat&logo=nodedotjs&logoColor=white 'Node.js')](https://nodejs.org/en/download/)
[![](https://img.shields.io/github/stars/CoderGamester/mcp-unity 'Stars')](https://github.com/CoderGamester/mcp-unity/stargazers)
[![](https://img.shields.io/github/last-commit/CoderGamester/mcp-unity 'Last Commit')](https://github.com/CoderGamester/mcp-unity/commits/main)
[![](https://img.shields.io/badge/License-MIT-red.svg 'MIT License')](https://opensource.org/licenses/MIT)

| [🇺🇸English](README.md) | [🇨🇳简体中文](README_zh-CN.md) | [🇯🇵日本語](README-ja.md) |
|----------------------|---------------------------------|----------------------|

```        
                              ,/(/.   *(/,                                  
                          */(((((/.   *((((((*.                             
                     .*((((((((((/.   *((((((((((/.                         
                 ./((((((((((((((/    *((((((((((((((/,                     
             ,/(((((((((((((/*.           */(((((((((((((/*.                
            ,%%#((/((((((*                    ,/(((((/(#&@@(                
            ,%%##%%##((((((/*.             ,/((((/(#&@@@@@@(                
            ,%%######%%##((/(((/*.    .*/(((//(%@@@@@@@@@@@(                
            ,%%####%#(%%#%%##((/((((((((//#&@@@@@@&@@@@@@@@(                
            ,%%####%(    /#%#%%%##(//(#@@@@@@@%,   #@@@@@@@(                
            ,%%####%(        *#%###%@@@@@@(        #@@@@@@@(                
            ,%%####%(           #%#%@@@@,          #@@@@@@@(                
            ,%%##%%%(           #%#%@@@@,          #@@@@@@@(                
            ,%%%#*              #%#%@@@@,             *%@@@(                
            .,      ,/##*.      #%#%@@@@,     ./&@#*      *`                
                ,/#%#####%%#/,  #%#%@@@@, ,/&@@@@@@@@@&\.                    
                 `*#########%%%%###%@@@@@@@@@@@@@@@@@@&*´                   
                    `*%%###########%@@@@@@@@@@@@@@&*´                        
                        `*%%%######%@@@@@@@@@@&*´                            
                            `*#%%##%@@@@@&*´                                 
                               `*%#%@&*´                                     
                                                       
     ███╗   ███╗ ██████╗██████╗         ██╗   ██╗███╗   ██╗██╗████████╗██╗   ██╗
     ████╗ ████║██╔════╝██╔══██╗        ██║   ██║████╗  ██║██║╚══██╔══╝╚██╗ ██╔╝
     ██╔████╔██║██║     ██████╔╝        ██║   ██║██╔██╗ ██║██║   ██║    ╚████╔╝ 
     ██║╚██╔╝██║██║     ██╔═══╝         ██║   ██║██║╚██╗██║██║   ██║     ╚██╔╝  
     ██║ ╚═╝ ██║╚██████╗██║             ╚██████╔╝██║ ╚████║██║   ██║      ██║   
     ╚═╝     ╚═╝ ╚═════╝╚═╝              ╚═════╝ ╚═╝  ╚═══╝╚═╝   ╚═╝      ╚═╝   
```       

MCP Unity is an implementation of the Model Context Protocol for Unity Editor, allowing AI assistants to interact with your Unity projects. This package provides a bridge between Unity and a Node.js server that implements the MCP protocol, enabling AI agents like Cursor, Windsurf, Claude Code, Codex CLI, GitHub Copilot, and Google Antigravity to execute operations within the Unity Editor.

## Features

### IDE Integration - Package Cache Access

MCP Unity provides automatic integration with VSCode-like IDEs (Visual Studio Code, Cursor, Windsurf, Google Antigravity) by adding the Unity `Library/PackedCache` folder to your workspace. This feature:

- Improves code intelligence for Unity packages
- Enables better autocompletion and type information for Unity packages
- Helps AI coding assistants understand your project's dependencies

### MCP Server Tools

The following tools are available for manipulating and querying Unity scenes and GameObjects via MCP:

- `show_unity_dashboard`: Opens the Unity dashboard MCP App in VS Code
  > **Example prompt:** "Open the Unity dashboard app"

- `execute_menu_item`: Executes Unity menu items (functions tagged with the MenuItem attribute)
  > **Example prompt:** "Execute the menu item 'GameObject/Create Empty' to create a new empty GameObject"

- `select_gameobject`: Selects game objects in the Unity hierarchy by path or instance ID
  > **Example prompt:** "Select the Main Camera object in my scene"

- `update_gameobject`: Updates a GameObject's core properties (name, tag, layer, active/static state), or creates the GameObject if it does not exist
  > **Example prompt:** "Set the Player object's tag to 'Enemy' and make it inactive"

- `update_component`: Updates component fields on a GameObject or adds it to the GameObject if it does not contain the component
  > **Example prompt:** "Add a Rigidbody component to the Player object and set its mass to 5"

- `add_package`: Installs new packages in the Unity Package Manager
  > **Example prompt:** "Add the TextMeshPro package to my project"

- `run_tests`: Runs tests using the Unity Test Runner
  > **Example prompt:** "Run all the EditMode tests in my project"

- `send_console_log`: Send a console log to Unity
  > **Example prompt:** "Send a console log to Unity Editor"

- `add_asset_to_scene`: Adds an asset from the AssetDatabase to the Unity scene
  > **Example prompt:** "Add the Player prefab from my project to the current scene"

- `create_prefab`: Creates a prefab with optional MonoBehaviour script and serialized field values
  > **Example prompt:** "Create a prefab named 'Player' from the 'PlayerController' script"

- `create_scene`: Creates a new scene and saves it to the specified path
  > **Example prompt:** "Create a new scene called 'Level1' in the Scenes folder"

- `load_scene`: Loads a scene by path or name, with optional additive loading
  > **Example prompt:** "Load the MainMenu scene"

- `delete_scene`: Deletes a scene by path or name and removes it from Build Settings
  > **Example prompt:** "Delete the old TestScene from my project"

- `get_gameobject`: Gets detailed information about a specific GameObject including all components
  > **Example prompt:** "Get the details of the Player GameObject"

- `get_console_logs`: Retrieves logs from the Unity console with pagination support
  > **Example prompt:** "Show me the last 20 error logs from the Unity console"

- `recompile_scripts`: Recompiles all scripts in the Unity project
  > **Example prompt:** "Recompile scripts in my Unity project"

- `save_scene`: Saves the current active scene, with optional Save As to a new path
  > **Example prompt:** "Save the current scene" or "Save the scene as 'Assets/Scenes/Level2.unity'"

- `get_scene_info`: Gets information about the active scene including name, path, dirty state, and all loaded scenes
  > **Example prompt:** "What scenes are currently loaded in my project?"

- `get_play_mode_status`: Gets Unity play mode status
  > **Example prompt:** "Is Unity in play mode?"

- `unload_scene`: Unloads a scene from the hierarchy (does not delete the scene asset)
  > **Example prompt:** "Unload the UI scene from the hierarchy"

- `duplicate_gameobject`: Duplicates a GameObject in the scene with optional renaming and reparenting
  > **Example prompt:** "Duplicate the Enemy prefab 5 times and rename them Enemy_1 through Enemy_5"

- `delete_gameobject`: Deletes a GameObject from the scene
  > **Example prompt:** "Delete the old Player object from the scene"

- `reparent_gameobject`: Changes the parent of a GameObject in the hierarchy
  > **Example prompt:** "Move the HealthBar object to be a child of the UI Canvas"

- `move_gameobject`: Moves a GameObject to a new position (local or world space)
  > **Example prompt:** "Move the Player object to position (10, 0, 5) in world space"

- `rotate_gameobject`: Rotates a GameObject to a new rotation (local or world space, Euler angles or quaternion)
  > **Example prompt:** "Rotate the Camera 45 degrees on the Y axis"

- `scale_gameobject`: Scales a GameObject to a new local scale
  > **Example prompt:** "Scale the Enemy object to twice its size"

- `set_transform`: Sets position, rotation, and scale of a GameObject in a single operation
  > **Example prompt:** "Set the Cube's position to (0, 5, 0), rotation to (0, 90, 0), and scale to (2, 2, 2)"

- `create_material`: Creates a new material with specified shader and saves it to the project
  > **Example prompt:** "Create a red material called 'EnemyMaterial' using the URP Lit shader"

- `assign_material`: Assigns a material to a GameObject's Renderer component
  > **Example prompt:** "Assign the 'EnemyMaterial' to the Enemy GameObject"

- `modify_material`: Modifies properties of an existing material (colors, floats, textures)
  > **Example prompt:** "Change the color of 'EnemyMaterial' to blue and set metallic to 0.8"

- `get_material_info`: Gets detailed information about a material including shader and all properties
  > **Example prompt:** "Show me all the properties of the 'PlayerMaterial'"

- `batch_execute`: Executes multiple tool operations in a single batch request, reducing round-trips and enabling atomic operations with optional rollback on failure
  > **Example prompt:** "Create 10 empty GameObjects named Enemy_1 through Enemy_10 in a single batch operation"

### MCP Server Resources

- `unity://ui/dashboard`: Unity dashboard MCP App UI
  > **Example prompt:** "Open the Unity dashboard app"

- `unity://menu-items`: Retrieves a list of all available menu items in the Unity Editor to facilitate `execute_menu_item` tool
  > **Example prompt:** "Show me all available menu items related to GameObject creation"

- `unity://scenes-hierarchy`: Retrieves a list of all game objects in the current Unity scene hierarchy
  > **Example prompt:** "Show me the current scenes hierarchy structure"

- `unity://gameobject/{id}`: Retrieves detailed information about a specific GameObject by instance ID or object path in the scene hierarchy, including all GameObject components with it's serialized properties and fields
  > **Example prompt:** "Get me detailed information about the Player GameObject"

- `unity://logs`: Retrieves a list of all logs from the Unity console
  > **Example prompt:** "Show me the recent error messages from the Unity console"

- `unity://packages`: Retrieves information about installed and available packages from the Unity Package Manager
  > **Example prompt:** "List all the packages currently installed in my Unity project"

- `unity://assets`: Retrieves information about assets in the Unity Asset Database
  > **Example prompt:** "Find all texture assets in my project"

- `unity://tests/{testMode}`: Retrieves information about tests in the Unity Test Runner
  > **Example prompt:** "List all available tests in my Unity project"

### MCP App Dashboard (VS Code 1.109+)

The Unity dashboard MCP App provides a visual UI for inspecting the scene hierarchy, console logs, and play controls directly inside VS Code.

1. Build the server with `cd Server~ && npm run build`.
2. Call `show_unity_dashboard` to open the app.
3. Use the Play, Pause, Step, Refresh, and Auto Refresh controls inside the dashboard.

## Requirements
- Unity 6 or later - to [install the server](#install-server)
- Node.js 18 or later - to [start the server](#start-server)
- npm 9 or later - to [debug the server](#debug-server)

> [!NOTE]
> **Project Paths with Spaces**
>
> MCP Unity supports project paths containing spaces. However, if you experience connection issues, try moving your project to a path without spaces as a troubleshooting step.
>
> **Examples:**
> -   ✅ **Recommended:** `C:\Users\YourUser\Documents\UnityProjects\MyAwesomeGame`
> -   ✅ **Supported:** `C:\Users\Your User\Documents\Unity Projects\My Awesome Game`
>
> MCP Unity can also install packages and dependencies automatically, saving time for AI agents:
> - Runs `npm install` and `npm run build` on initial setup
> - Adds the `Library/PackedCache` directory to the workspace for improved code intelligence
> - Handles configuration setup for MCP clients
>
> This is controlled in the Unity Editor under Tools > MCP Unity > Server Window

## <a name="install-server"></a>Installation

Installing this MCP Unity Server is a multi-step process:

### Step 1: Install Node.js 
> To run MCP Unity server, you'll need to have Node.js 18 or later installed on your computer:

![node](docs/node.jpg)

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">Windows</span></summary>

1. Visit the [Node.js download page](https://nodejs.org/en/download/)
2. Download the Windows Installer (.msi) for the LTS version (recommended)
3. Run the installer and follow the installation wizard
4. Verify the installation by opening PowerShell and running:
   ```bash
   node --version
   ```
</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">macOS</span></summary>

1. Visit the [Node.js download page](https://nodejs.org/en/download/)
2. Download the macOS Installer (.pkg) for the LTS version (recommended)
3. Run the installer and follow the installation wizard
4. Alternatively, if you have Homebrew installed, you can run:
   ```bash
   brew install node@18
   ```
5. Verify the installation by opening Terminal and running:
   ```bash
   node --version
   ```
</details>

### Step 2: Install Unity MCP Server package via Unity Package Manager
1. Open the Unity Package Manager (Window > Package Manager)
2. Click the "+" button in the top-left corner
3. Select "Add package from git URL..."
4. Enter: `https://github.com/CoderGamester/mcp-unity.git`
5. Click "Add"

![package manager](https://github.com/user-attachments/assets/a72bfca4-ae52-48e7-a876-e99c701b0497)

### Step 3: Configure AI LLM Client

<details open>
<summary><span style="font-size: 1.1em; font-weight: bold;">Option 1: Configure using Unity Editor</span></summary>

1. Open the Unity Editor
2. Navigate to Tools > MCP Unity > Server Window
3. Click on the "Configure" button for your AI LLM client as shown in the image below

![image](docs/configure.jpg)

4. Confirm the configuration installation with the given popup

![image](https://github.com/user-attachments/assets/b1f05d33-3694-4256-a57b-8556005021ba)

</details>

<details>
<summary><span style="font-size: 1.1em; font-weight: bold;">Option 2: Configure Manually</span></summary>

Open the MCP configuration file of your AI client and add the MCP Unity server configuration:

> Replace `ABSOLUTE/PATH/TO` with the absolute path to your MCP Unity installation or just copy the text from the Unity Editor MCP Server window (Tools > MCP Unity > Server Window).

**For JSON-based clients** (Cursor, Windsurf, Claude Code, GitHub Copilot, etc.):

```json
{
  "mcpServers": {
    "unity": {
      "command": "node",
      "args": [
        "ABSOLUTE/PATH/TO/mcp-unity/Server~/build/index.js"
      ]
    }
  }
}
```

**For TOML-based clients** (Cline, Roo Code, etc.):

```toml
[mcpServers.unity]
command = "node"
args = ["ABSOLUTE/PATH/TO/mcp-unity/Server~/build/index.js"]
```

</details>

---

### Step 4: Start the MCP Unity Server

#### Option 1: Start from Unity Editor
1. Open Unity Editor
2. Go to Tools > MCP Unity > Server Window
3. Click "Start Server"

#### Option 2: Start from Terminal

```bash
cd Server~
npm install
npm run build
npm start
```

### Step 5: Start the MCP Client

Now that the server is running, you can start using MCP Unity with your AI assistant. Ensure your MCP client is configured to use the MCP Unity server, then begin issuing commands.

## Usage Examples

### Example 1: Create a Scene

You can ask your AI assistant to create a new Unity scene:

> "Create a new Unity scene called 'Level1' in the Scenes folder"

### Example 2: Create a GameObject

You can ask your AI assistant to create a new GameObject in your Unity scene:

> "Create a new empty GameObject named 'Player' and position it at (0, 1, 0)"

### Example 3: Inspect GameObjects

You can ask your AI assistant to inspect a specific GameObject:

> "Show me details about the GameObject named 'Enemy'"

### Example 4: Run Tests

You can ask your AI assistant to run tests using the Unity Test Runner:

> "Run all the tests in my project"

## Troubleshooting

### Server Not Starting
- **Check if port 8090 is already in use**: Another application might be using the same port.
- **Verify that Node.js is installed correctly**: Ensure you can run `node` and `npm` commands in your terminal.
- **Check Unity console for errors**: The MCP Unity package logs errors to the Unity console if something fails.

### MCP Client Not Connecting
- **Verify your MCP configuration**: Ensure your MCP client config includes the correct path to `Server~/build/index.js`.
- **Check firewall settings**: Ensure `ws://localhost:8090` is allowed through your firewall.
- **Check Unity server settings**: Ensure `McpUnitySettings.json` has the correct port and `AutoStartServer` is enabled.
- **Check for any errors in the server logs**: Set `LOGGING=true` to see detailed logs.

---

If you run into issues, please open an issue on GitHub with your logs and configuration details.
```

##### Step 5 Verification Checklist
- [ ] Confirm the documentation lists `show_unity_dashboard`, `get_play_mode_status`, and `unity://ui/dashboard`.
- [ ] Follow [plans/unity-dashboard-mcp-app/usage.md](plans/unity-dashboard-mcp-app/usage.md) to open the dashboard.

#### Step 5 STOP & COMMIT
**STOP & COMMIT:** Agent must stop here and wait for the user to test, stage, and commit the change.
