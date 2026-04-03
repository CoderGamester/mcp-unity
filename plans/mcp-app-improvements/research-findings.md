# MCP Unity Dashboard App - Research Findings for Bidirectional Context Updates

## Executive Summary

This document contains comprehensive research findings on the current MCP Unity dashboard implementation to inform the implementation of bidirectional context updates using `updateModelContext`.

---

## 1. Current Dashboard Implementation

### 1.1 HTML/JavaScript Architecture

**File:** `Server~/src/ui/unity-dashboard.html`

The dashboard is a single HTML file that implements an MCP App using JSON-RPC over postMessage.

#### Key Features:
- **Protocol Version:** `2026-01-26` (SEP-1865 compatible)
- **Communication:** JSON-RPC 2.0 over `window.parent.postMessage`
- **State Management:** Local UI state with polling-based refresh
- **Auto-refresh:** Configurable polling (1-10 seconds)

#### Current UI Components:
1. **Play Mode Controls:** Play, Pause, Step buttons
2. **Scene Information:** Active scene name, root count, loaded state
3. **Hierarchy View:** Scene list with root objects
4. **Console Logs:** Paginated log display (info, warn, error)
5. **Auto-refresh:** Checkbox and interval slider (1-10s)

#### Current RPC Methods Used:
```javascript
// Initialization
rpcRequest('ui/initialize', { appInfo, appCapabilities, protocolVersion })
rpcNotify('ui/notifications/initialized', {})

// Tool calling
rpcRequest('tools/call', { name, arguments })
```

#### Data Fetching Pattern (Current):
```javascript
async function fetchAll() {
  const [sceneResult, hierarchyResult, logsResult, playModeResult] = await Promise.all([
    callTool('get_scene_info', {}),
    callTool('get_scenes_hierarchy', {}),
    callTool('get_console_logs', { limit: 200, includeStackTrace: false }),
    callTool('get_play_mode_status', {}),
  ]);
  // Update UI from results
}
```

**Key Finding:** Currently uses polling-based "pull" model. All state updates are initiated by the dashboard at fixed intervals.

### 1.2 User Interactions (Current)

The dashboard currently supports these user actions:
1. **Play/Pause/Step:** Execute Unity menu items
2. **Refresh:** Manually trigger data fetch
3. **Auto-refresh toggle:** Enable/disable polling
4. **Interval adjustment:** Change polling frequency

**Key Finding:** No GameObject selection, context updates, or bidirectional communication patterns currently implemented.

### 1.3 Existing updateModelContext Usage

**Result:** No existing usage of `updateModelContext` found in the codebase.

The current implementation uses only:
- `rpcRequest()` - for tool calls and initialization
- `rpcNotify()` - for initialization notification

**Key Finding:** The MCP App SDK (from `@modelcontextprotocol/ext-apps`) is installed but `updateModelContext` is not yet being used.

---

## 2. Unity-Side Resources

### 2.1 GetScenesHierarchyResource

**File:** `Editor/Resources/GetScenesHierarchyResource.cs`

**Purpose:** Fetches all GameObjects in loaded scenes with their active state.

**URI:** `unity://scenes_hierarchy`

**Method Name:** `get_scenes_hierarchy`

**Data Structure:**
```json
{
  "success": true,
  "message": "Retrieved hierarchy with X root objects",
  "hierarchy": [
    {
      "name": "SceneName",
      "path": "Assets/Scenes/SceneName.unity",
      "buildIndex": 0,
      "isDirty": false,
      "rootObjects": [
        {
          "name": "GameObject",
          "activeSelf": true,
          "activeInHierarchy": true,
          "tag": "Untagged",
          "layer": 0,
          "layerName": "Default",
          "instanceId": -12345,
          "components": [...],
          "children": [...]
        }
      ]
    }
  ]
}
```

**Key Method:** `GameObjectToJObject(GameObject, bool includeDetailedComponents)` - recursively builds hierarchy tree.

### 2.2 GetConsoleLogsResource

**File:** `Editor/Resources/GetConsoleLogsResource.cs`

**Purpose:** Retrieves Unity console logs with pagination and filtering.

**URI:** `unity://logs/{logType}`

**Method Name:** `get_console_logs`

**Parameters:**
- `logType`: "info" | "warning" | "error" (optional)
- `offset`: pagination offset (default: 0)
- `limit`: max entries (default: 100, max: 1000)
- `includeStackTrace`: boolean (default: true)

**Service:** Uses `IConsoleLogsService` which maintains an in-memory log buffer.

**Key Finding:** Logs are collected via Unity's `Application.logMessageReceived` event handler in `ConsoleLogsService.cs`.

### 2.3 GetGameObjectResource

**File:** `Editor/Resources/GetGameObjectResource.cs`

**Purpose:** Retrieves detailed information about a specific GameObject.

**URI:** `unity://gameobject/{idOrName}`

**Method Name:** `get_gameobject`

**Parameters:**
- `idOrName`: Can be instance ID (integer) or hierarchical path (e.g., "Canvas/Panel/Button")

**Lookup Strategy:**
1. Try parsing as integer → `EditorUtility.InstanceIDToObject()`
2. Otherwise → `GameObject.Find()` with path

**Data Structure:** Same as hierarchy node but with full component details including reflected properties.

**Key Finding:** The resource supports both instance ID and path-based lookups, making it flexible for selection contexts.

### 2.4 GetPlayModeStatusTool

**File:** `Editor/Tools/GetPlayModeStatusTool.cs`

**Method Name:** `get_play_mode_status`

**Data Structure:**
```json
{
  "success": true,
  "type": "text",
  "message": "Play mode (paused)" | "Play mode" | "Edit mode",
  "isPlaying": true,
  "isPaused": false
}
```

**Key Finding:** Uses `EditorApplication.isPlaying` and `EditorApplication.isPaused` for real-time state.

---

## 3. Unity-Side Resource/Tool Registration

### 3.1 McpUnityServer Architecture

**File:** `Editor/UnityBridge/McpUnityServer.cs`

**Pattern:** Singleton instance that manages WebSocket server and tool/resource registration.

**Key Components:**
- `Dictionary<string, McpToolBase> _tools`
- `Dictionary<string, McpResourceBase> _resources`
- `WebSocketServer _webSocketServer` (port 8090 by default)

**Lifecycle Hooks:**
- `EditorApplication.playModeStateChanged` - stops/restarts server on play mode changes
- `AssemblyReloadEvents.beforeAssemblyReload` - stops server before script recompilation
- `EditorApplication.quitting` - cleanup on editor shutdown

**Key Finding:** Unity server is request-response only. No push notification mechanism currently exists.

### 3.2 McpUnitySocketHandler

**File:** `Editor/UnityBridge/McpUnitySocketHandler.cs`

**Pattern:** WebSocket message handler that routes incoming JSON-RPC requests to tools/resources.

**Message Flow:**
1. Receive JSON-RPC request via WebSocket
2. Parse method name and parameters
3. Dispatch to tool or resource via `EditorCoroutineUtility` (main thread)
4. Return JSON-RPC response

**Key Finding:** All execution happens on Unity's main thread via coroutines. Synchronous tools use `Execute()`, async tools use `ExecuteAsync()` with `TaskCompletionSource`.

**Current Limitation:** Only supports request-response. No notification/event push mechanism to clients.

---

## 4. Node-Side MCP Server Implementation

### 4.1 Tool Registration Pattern

**File:** `Server~/src/tools/getScenesHierarchyTool.ts` (example)

**Pattern:**
```typescript
export function registerGetScenesHierarchyTool(
  server: McpServer, 
  mcpUnity: McpUnity, 
  logger: Logger
) {
  server.tool(toolName, toolDescription, paramsSchema.shape, async (params) => {
    const response = await mcpUnity.sendRequest({ method: toolName, params });
    return {
      content: [{ type: 'text', text: JSON.stringify(hierarchy) }],
      data: { hierarchy }
    };
  });
}
```

**Key Components:**
- `McpServer` - from `@modelcontextprotocol/sdk`
- `McpUnity` - WebSocket client wrapper
- Zod schema for parameter validation

### 4.2 ShowUnityDashboardTool

**File:** `Server~/src/tools/showUnityDashboardTool.ts`

**Purpose:** Opens the Unity dashboard MCP App in VS Code.

**Pattern:** Uses `registerAppTool` from `@modelcontextprotocol/ext-apps/server`

**Response Structure:**
```typescript
{
  content: [{
    type: 'resource',
    resource: {
      uri: 'unity://ui/dashboard',
      mimeType: 'text/html',
      text: htmlContent,
      _meta: { view: 'mcp-app' }
    }
  }]
}
```

**Key Finding:** The tool returns HTML content with `_meta: { view: 'mcp-app' }` to signal the host to render it as an app.

### 4.3 UnityDashboardAppResource

**File:** `Server~/src/resources/unityDashboardAppResource.ts`

**Purpose:** Serves the dashboard HTML as an MCP resource.

**URIs:**
- Primary: `ui://unity-dashboard` (new standard)
- Legacy: `unity://ui/dashboard` (backward compatibility)

**Pattern:** Uses `registerAppResource` from `@modelcontextprotocol/ext-apps/server`

**Key Finding:** The resource reads `unity-dashboard.html` from disk and serves it with `RESOURCE_MIME_TYPE` (text/html).

### 4.4 McpUnity Bridge Layer

**File:** `Server~/src/unity/mcpUnity.ts`

**Purpose:** WebSocket client that manages connection to Unity Editor.

**Key Features:**
- Automatic reconnection with exponential backoff
- Heartbeat monitoring
- Command queuing during disconnection
- Request timeout handling (default: 10s)

**Message Protocol:**
```typescript
interface UnityRequest {
  id?: string;
  method: string;
  params: any;
}

interface UnityResponse {
  jsonrpc: string;
  id: string;
  result?: any;
  error?: { message: string; type: string; };
}
```

**Key Finding:** The bridge is purely request-response. No provision for Unity-initiated notifications to Node.

---

## 5. MCP SDK and ext-apps Integration

### 5.1 Package Dependencies

**File:** `Server~/package.json`

```json
{
  "@modelcontextprotocol/ext-apps": "^1.0.0",
  "@modelcontextprotocol/sdk": "^1.7.0"
}
```

### 5.2 MCP App SDK Usage (Current)

The dashboard implements the MCP Apps protocol (SEP-1865) using manual JSON-RPC:

```javascript
// Current implementation in unity-dashboard.html
const PROTOCOL_VERSION = '2026-01-26';

// Initialize app
await rpcRequest('ui/initialize', {
  appInfo: { name: 'unity-dashboard', version: '1.0.0' },
  appCapabilities: { tools: {} },
  protocolVersion: PROTOCOL_VERSION,
});

// Call tools
const result = await rpcRequest('tools/call', { name, arguments: args });
```

**Key Finding:** The dashboard implements its own RPC bridge rather than importing the MCP App SDK client library.

### 5.3 Available MCP App Methods (from ext-apps)

Based on the protocol version `2026-01-26` and the ext-apps package, the following methods should be available:

**From the dashboard (client → host):**
- `ui/initialize` - Initialize the app
- `ui/notifications/initialized` - Notify initialization complete
- `tools/call` - Request tool execution
- **`ui/updateModelContext`** - Update agent's context (NOT YET USED)

**From the host (host → dashboard):**
- `ui/notifications/host-context-changed` - Host context changed
- Tool execution responses

**Key Finding:** `updateModelContext` is part of the MCP Apps protocol but not yet implemented in the dashboard.

---

## 6. Architecture and Data Flow

### 6.1 Current Architecture (Pull Model)

```
Agent (LLM)
    ↓ (requests tool)
VS Code MCP Client
    ↓ (stdio)
Node MCP Server (index.ts)
    ↓ (WebSocket request)
Unity WebSocket Server (McpUnityServer.cs)
    ↓ (executes on main thread)
Unity Resources/Tools
    ↑ (returns data)
Node MCP Server
    ↑ (returns result)
VS Code MCP Client
    ↑ (returns to agent)
Agent (LLM)

Dashboard App:
    - Polls Unity state every N seconds
    - No agent context updates
    - No push notifications
```

### 6.2 Proposed Architecture (Push Model with Context Updates)

```
Agent (LLM) ←─ updateModelContext ─┐
    ↑                               │
VS Code MCP Client                  │
    ↑                               │
Node MCP Server                     │
    ↑                               │
Unity WebSocket Server              │
    ↑                               │
Unity Tools/Resources               │
                                    │
Dashboard App ──────────────────────┘
    - User clicks GameObject → updateModelContext
    - Console error appears → updateModelContext
    - Play mode changes → updateModelContext
```

**Key Changes Needed:**
1. Import MCP App SDK client in dashboard HTML
2. Add event handlers for user interactions (selection, etc.)
3. Call `updateModelContext()` when context-relevant events occur
4. Consider Unity → Dashboard push notifications (optional enhancement)

---

## 7. Key Findings Summary

### 7.1 What's Working Well

1. **Solid Foundation:** Well-structured tool/resource system with clear separation of concerns
2. **Reliable Communication:** WebSocket bridge with reconnection, heartbeat, and queuing
3. **MCP Apps Integration:** Dashboard already uses MCP Apps protocol (SEP-1865)
4. **Comprehensive Tools:** Rich set of tools for Unity manipulation
5. **Flexible GameObject Lookup:** Supports both instance IDs and hierarchical paths

### 7.2 Current Limitations

1. **No bidirectional context:** Dashboard polls Unity; agent has no awareness of UI actions
2. **No updateModelContext usage:** API is available but not implemented
3. **No selection mechanism:** Dashboard can't track or report selected GameObjects
4. **No Unity → Dashboard push:** Unity can't proactively notify dashboard of state changes
5. **Manual RPC implementation:** Dashboard doesn't import the MCP App SDK client library

### 7.3 Required Changes for Bidirectional Context

#### High Priority:
1. **Add updateModelContext calls in dashboard:**
   - On GameObject selection (when implemented)
   - On console error detection
   - On play mode changes
   - On scene changes

2. **Implement GameObject selection in dashboard:**
   - Make hierarchy items clickable
   - Track selected GameObject ID/path
   - Update context when selection changes

3. **Add "Sync to Agent" button:**
   - Gather current scene, selection, top errors
   - Send comprehensive context update

#### Medium Priority:
4. **Replace manual RPC with SDK client:**
   - Import `@modelcontextprotocol/ext-apps/client` (if available)
   - Use SDK methods instead of manual postMessage

5. **Add more context triggers:**
   - Component changes
   - Material assignments
   - Script compilation errors

#### Low Priority (Future Enhancement):
6. **Unity → Dashboard push notifications:**
   - Extend McpUnitySocketHandler to send notifications
   - Add EventEmitter pattern in Unity for state changes
   - Update dashboard to handle incoming notifications
   - Consider performance impact

---

## 8. Implementation Recommendations

### 8.1 Phase 1: Basic Context Updates (Quick Win)

**Goal:** Enable agent awareness of dashboard state without Unity changes.

**Changes:**
1. Add `updateModelContext` method to dashboard
2. Implement clickable hierarchy items
3. Track selected GameObject
4. Send context updates on selection, play mode change, console errors

**Estimated Effort:** 2-4 hours

**Dependencies:** None (works with current Unity implementation)

### 8.2 Phase 2: Enhanced Context (Iterative Improvement)

**Goal:** Provide richer context to the agent.

**Changes:**
1. Add "Sync to Agent" button
2. Include scene metadata in context
3. Push top 3 console errors automatically
4. Add component information to selection context

**Estimated Effort:** 4-6 hours

**Dependencies:** Phase 1 complete

### 8.3 Phase 3: Unity-Initiated Push (Advanced)

**Goal:** Real-time notifications from Unity to dashboard.

**Changes:**
1. Add notification mechanism to McpUnitySocketHandler
2. Hook Unity events (Selection.selectionChanged, console logs, etc.)
3. Push notifications to all connected clients
4. Handle notifications in dashboard
5. Trigger context updates from Unity events

**Estimated Effort:** 8-12 hours

**Dependencies:** Careful design to avoid performance impact

**Considerations:**
- Unity Editor performance impact
- WebSocket message volume
- Client filtering (which events matter?)
- Backward compatibility

---

## 9. Code References

### Key Files to Modify:

**Dashboard (Primary):**
- `Server~/src/ui/unity-dashboard.html` - Add updateModelContext, selection, events

**Node Server (Supporting):**
- `Server~/src/resources/unityDashboardAppResource.ts` - Resource serving
- `Server~/src/tools/showUnityDashboardTool.ts` - Tool for opening app

**Unity (Optional - Phase 3):**
- `Editor/UnityBridge/McpUnitySocketHandler.cs` - Add notification sending
- `Editor/UnityBridge/McpUnityServer.cs` - Add Unity event hooks

### Patterns to Follow:

**updateModelContext call:**
```javascript
// In dashboard HTML
async function updateAgentContext(description, data) {
  await rpcRequest('ui/updateModelContext', {
    description,
    ...data
  });
}
```

**GameObject selection:**
```javascript
function onSelectGameObject(instanceId, name, path) {
  await updateAgentContext(
    `User selected GameObject: ${name} (${path})`,
    {
      selectedGameObject: { instanceId, name, path },
      action: "selection"
    }
  );
}
```

---

## 10. Next Steps

1. **Review findings** with stakeholders
2. **Prioritize phases** based on value and effort
3. **Create detailed implementation plan** for Phase 1
4. **Prototype updateModelContext** in a branch
5. **Test with real agent interactions**
6. **Iterate based on user feedback**

---

## Appendix: Related Files

### Dashboard Implementation:
- `Server~/src/ui/unity-dashboard.html` (262 lines)
- `Server~/src/resources/unityDashboardAppResource.ts` (108 lines)
- `Server~/src/tools/showUnityDashboardTool.ts` (60 lines)

### Unity Resources:
- `Editor/Resources/GetScenesHierarchyResource.cs` (90+ lines)
- `Editor/Resources/GetConsoleLogsResource.cs` (90+ lines)
- `Editor/Resources/GetGameObjectResource.cs` (433 lines)
- `Editor/Tools/GetPlayModeStatusTool.cs` (50 lines)

### Unity Bridge:
- `Editor/UnityBridge/McpUnityServer.cs` (542 lines)
- `Editor/UnityBridge/McpUnitySocketHandler.cs` (237 lines)

### Node Bridge:
- `Server~/src/unity/mcpUnity.ts` (535 lines)
- `Server~/src/unity/unityConnection.ts` (542 lines)
- `Server~/src/index.ts` (183 lines)

### Documentation:
- `plans/mcp-app-improvements/prompt.md` - Feature request document
- `AGENTS.md` - Agent instructions for codebase
- `README.md` - Project documentation
