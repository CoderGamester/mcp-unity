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
- `get_play_mode_status` — Get Unity play mode status (isPlaying, isPaused)
- `set_play_mode_status` — Control Unity play mode (play, pause, stop, step)
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

### Available apps (current)
- `show_unity_dashboard` — Open the Unity dashboard MCP App in VS Code

### Available resources (current)
- `unity://menu-items` — List of available menu items
- `unity://scenes-hierarchy` — Current scene hierarchy
- `unity://gameobject/{id}` — GameObject details by ID or path
- `unity://logs` — Unity console logs
- `unity://packages` — Installed and available packages
- `unity://assets` — Asset database information
- `unity://tests/{testMode}` — Test Runner test information
- `unity://ui/dashboard` — Unity dashboard MCP App UI

### Available prompts (current)
- `unity_dashboard` — Opens Unity dashboard MCP app with guided information about features
- `gameobject_handling_strategy` — Provides structured workflow for GameObject operations

### Update policy (for agents)
- Update this file when:
  - tools/resources/prompts are added/removed/renamed,
  - config shape or default ports/paths change,
  - the bridge protocol changes (request/response contract).
- Keep it **high-signal**: where to edit code, how to run/build/debug, and the invariants that prevent subtle breakage.

