# Unity Dashboard Popout Button Support

## Goal
Add the missing tool-level UI metadata so the Unity Dashboard shows the "Open in Editor" popout button in VS Code 1.109.0.

## Prerequisites
Make sure that the use is currently on the `fix-popout-button` branch before beginning implementation.
If not, move them to the correct branch. If the branch does not exist, create it from main.

### Step-by-Step Instructions

#### Step 1: Add Tool-Level UI Metadata
- [x] Open the file at `Server~/src/tools/showUnityDashboardTool.ts`.
- [x] Copy and paste the complete code below into `Server~/src/tools/showUnityDashboardTool.ts`:

```typescript
import * as z from 'zod';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { Logger } from '../utils/logger.js';
import { registerAppTool } from '@modelcontextprotocol/ext-apps/server';
import { readUnityDashboardHtml } from '../resources/unityDashboardAppResource.js';

const toolName = 'show_unity_dashboard';
const toolDescription = 'Opens the Unity dashboard MCP App in VS Code.';
const paramsSchema = z.object({});

export function registerShowUnityDashboardTool(server: McpServer, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  registerAppTool(server, toolName, {
    description: toolDescription,
    inputSchema: paramsSchema.shape,
    _meta: {
      ui: {
        resourceUri: 'ui://unity-dashboard'
      }
    }
  }, async () => {
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
  // Returning an embedded resource here is the most compatible option:
  // - Some hosts open the app via tool metadata (_meta.ui.resourceUri)
  // - Others rely on content blocks with view hints (legacy)
  const { text, mimeType } = readUnityDashboardHtml();

  return {
    content: [
      {
        type: 'resource',
        resource: {
          // IMPORTANT: Use the ui:// scheme so MCP App-capable hosts (e.g. VS Code)
          // recognize this as an App resource and enable native “Open in Editor”.
          uri: 'ui://unity-dashboard',
          mimeType,
          text,
          _meta: {
            view: 'mcp-app'
          }
        }
      },
      // Legacy fallback for hosts/docs that still expect this URI.
      {
        type: 'resource',
        resource: {
          uri: 'unity://ui/dashboard',
          mimeType,
          text,
          _meta: {
            view: 'mcp-app'
          }
        }
      }
    ],
    _meta: {
      ui: true
    }
  };
}
```

##### Step 1 Verification Checklist
- [x] Run `npm run build` from the `Server~/` directory.
- [x] Confirm the build completes without errors.

#### Step 1 STOP & COMMIT
**STOP & COMMIT:** Agent must stop here and wait for the user to test, stage, and commit the change.

#### Step 2: Validate Dashboard Behavior (Inline + Popout)
- [ ] Restart the MCP server in VS Code.
- [ ] Ask Copilot: "show the Unity dashboard".
- [ ] Confirm the "Open in Editor" icon appears in the dashboard panel header.
- [ ] Click the icon and confirm the dashboard opens in a new editor tab.
- [ ] In inline mode, verify each feature below:
  - [ ] Hierarchy display renders with indentation.
  - [ ] Logs show correct info/warn/error coloring.
  - [ ] Scene info shows active scene name and root count.
  - [ ] Auto-refresh toggles and polls at the set interval.
  - [ ] Play/edit mode badge is correct.
  - [ ] GameObject selection highlights when clicked.
  - [ ] Agent context sync updates after clicking Sync.
  - [ ] Split view persists when pinned to the right edge.
  - [ ] Compilation status badge updates during a script compile.
  - [ ] Inspector focus highlights the selected GameObject.
- [ ] Repeat all checks in popout mode (editor tab).
- [ ] Open DevTools (F12) and confirm no console errors.
- [ ] Ensure auto-refresh continues when the popout tab is in the background.
- [ ] Verify agent context updates arrive in both inline and popout modes.

##### Step 2 Verification Checklist
- [ ] No UI regressions in either mode.
- [ ] No console errors in DevTools.

#### Step 2 STOP & COMMIT
**STOP & COMMIT:** Agent must stop here and wait for the user to test, stage, and commit the change.
