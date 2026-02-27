# Unity Dashboard MCP App

**Branch:** `feature/unity-dashboard-mcp-app`
**Description:** Add visual UI dashboard for Unity scene inspection and control as an MCP App in VS Code

## Goal
Implement an MCP App (VS Code 1.109+ feature) that provides a visual dashboard for inspecting Unity scenes and controlling playback directly within VS Code, eliminating the need for text-based queries to view scene state.

## Prerequisites & Assumptions
- Will attempt with current MCP SDK version 1.7.0; may need upgrade if MCP Apps not supported
- VS Code 1.109+ required for MCP Apps feature
- Phase 1 features (confirmed):
  - Scene hierarchy viewer (read-only tree view)
  - Play/Pause/Step control buttons
  - Console log display panel
- Update mechanism: Polling-based using existing MCP tool/resource pattern
- HTML served from separate file (`Server~/src/ui/unity-dashboard.html`)

## Implementation Steps

### Step 1: Create Dashboard UI Resource
**Files:** 
- `Server~/src/resources/unityDashboardAppResource.ts`
- `Server~/src/ui/unity-dashboard.html`
- `Server~/src/index.ts`

**What:** Create the HTML-based MCP App UI that displays Unity scene information and provides interactive controls. Register it as a resource with URI `unity://ui/dashboard` that returns HTML content with `text/html` MIME type. The HTML will use the MCP App SDK to communicate with the server and call existing tools.

**Testing:** 
- Build the server with `npm run build`
- Start MCP Inspector: `npm run inspector`
- Read the `unity://ui/dashboard` resource and verify HTML is returned
- Verify the HTML contains scene hierarchy, controls, and console log sections

### Step 2: Create Tool to Trigger Dashboard
**Files:**
- `Server~/src/tools/showUnityDashboardTool.ts`
- `Server~/src/index.ts`

**What:** Create a tool `show_unity_dashboard` that returns a resource reference with metadata `{ "view": "mcp-app" }` to tell VS Code to render the dashboard as an MCP App instead of inline text.

**Testing:**
- Call the tool via MCP Inspector
- Verify response contains resource reference with metadata `{ "view": "mcp-app" }`
- Test in VS Code 1.109+ to verify dashboard opens as an app view
- If app view doesn't appear, may need to upgrade MCP SDK or adjust metadata format

### Step 3: Implement Dashboard Frontend Logic
**Files:**
- `Server~/src/ui/unity-dashboard.html`

**What:** Add JavaScript to the HTML that:
1. Uses MCP App SDK to call existing tools (`get_scene_info`, `unity://scenes-hierarchy` resource, `get_console_logs`)
2. Displays scene hierarchy as a collapsible tree view
3. Adds Play/Pause/Step buttons that call `execute_menu_item` with Unity menu paths
4. Shows console logs in a scrollable panel with filtering (info/warning/error)
5. Implements polling-based refresh (user can trigger manual refresh or enable auto-refresh with configurable interval)

**Testing:**
- Open dashboard in VS Code via `show_unity_dashboard` tool
- Click Play button and verify Unity enters play mode
- Click Pause button and verify Unity exits play mode
- Click Step button and verify Unity advances one frame
- Click "Refresh" and verify scene hierarchy and console logs update
- Enable auto-refresh and verify data updates at specified interval
- Test with scenes containing 50-100 GameObjects
- Verify console log filtering works (show/hide info/warning/error)

### Step 4: Add Play Mode Status Tool (Optional Enhancement)
**Files:**
- `Editor/Tools/GetPlayModeStatusTool.cs`
- `Editor/UnityBridge/McpUnityServer.cs`
- `Server~/src/tools/getPlayModeStatusTool.ts`
- `Server~/src/index.ts`

**What:** Create a new Unity-side tool that returns `EditorApplication.isPlaying` status, since this isn't currently exposed. This enables the dashboard to show accurate play/pause button state.

**Testing:**
- Call tool when Unity is in Edit mode (should return `isPlaying: false`)
- Call tool when Unity is in Play mode (should return `isPlaying: true`)
- Dashboard button UI should update to reflect current state

### Step 5: Update Documentation
**Files:**
- `AGENTS.md`
- `README.md`
- `plans/unity-dashboard-mcp-app/usage.md` (new)

**What:** 
- Add `show_unity_dashboard` to available tools list in AGENTS.md
- Add `unity://ui/dashboard` to available resources list
- Add `get_play_mode_status` to available tools (if Step 4 implemented)
- Create usage guide showing how to open and use the dashboard
- Update README with MCP App feature section

**Testing:**
- Verify documentation is accurate by following steps from scratch
- Test with a new Unity project to ensure instructions work

## Technical Considerations

### MCP App SDK Integration
The prompt references `@modelcontextprotocol/sdk-apps` package for client-side MCP communication.
Approach:
- Load from CDN in the HTML file (avoids build complexity)
- Use the SDK's client API to call MCP tools/resources from the dashboard
- Handle communication via `MCPApp` class as shown in prompt example

### Resource Serving
Current MCP SDK `server.resource()` method returns `ReadResourceResult` which expects text content. For HTML:
- Use `mimeType: "text/html"`
- Read HTML from `Server~/src/ui/unity-dashboard.html` using `fs.readFileSync()`
- Import Node.js `fs` module in the resource handler
- Path resolution: use `import.meta.url` or `__dirname` equivalent for ESM

### Security & Sandboxing
VS Code MCP Apps run in sandboxed iframes with CSP restrictions:
- No inline event handlers (use `addEventListener` instead of `onclick`)
- External scripts must be from trusted CDNs
- Test CSP compliance during implementation; adjust HTML if needed
- VS Code CSP policies to be verified during testing phase

### Unity WebSocket Connection
Dashboard HTML runs in VS Code (browser context), not Node.js:
- Cannot directly connect WebSocket to Unity
- Must proxy all Unity calls through MCP tools/resources
- This is the expected pattern per the prompt

## Future Enhancements (Post Phase 1)
1. GameObject selection sync (click in dashboard → select in Unity Editor)
2. GameObject inspector panel (view properties when selected)
3. Performance profiling panel (FPS, memory usage)
4. Asset browser integration
5. Custom C# script execution panel
6. WebSocket push notifications for real-time updates (eliminate polling)
