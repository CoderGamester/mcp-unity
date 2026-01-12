# MCP Unity Improvement Plan

This document outlines a comprehensive plan to improve the [mcp-unity](https://github.com/CoderGamester/mcp-unity) project. The improvements are organized into phases, with dependencies noted where relevant.

---

## Phase 1: Foundation & Stability

Critical fixes that improve reliability and remove blockers for adoption.

### 1.1 Fix Spaces in Path Limitation ✅

**Problem:** Project paths containing spaces cause failures throughout the system.

**Tasks:**
- [x] Audit all path handling in C# Unity code (`Editor/` directory)
- [x] Audit all path handling in TypeScript server code (`Server/src/`)
- [x] Wrap all path strings in proper escaping/quoting
- [x] Add path validation on startup with clear error messages
- [x] Add integration tests with space-containing paths

**Completed Changes:**
- `Editor/Tools/AddPackageTool.cs`: Fixed file URL encoding for paths with spaces (encodes as `%20`)
- `Editor/Utils/McpUtils.cs`: Added `ValidateServerPath()` and `EncodePathForFileUrl()` methods
- `Editor/UnityBridge/McpUnityServer.cs`: Added path validation call during server installation
- `Editor/Tests/`: Added NUnit test suite for path handling (9 tests)
- `Server~/src/__tests__/`: Added Jest test suite (29 tests covering errors, logger, and path handling)

**Audit Findings:**
- MCP config JSON serialization already handles paths with spaces correctly
- `ProcessStartInfo.WorkingDirectory` handles spaces correctly for npm commands
- TypeScript server uses Node's `path` module which handles spaces correctly

**Files to modify:**
- `Editor/McpUnityServer.cs`
- `Server/src/index.ts`
- All tool handlers in `Server/src/tools/`

**PR:** `fix/spaces-in-path`

---

### 1.1b Graceful Shutdown (Added) ✅

**Problem:** MCP server didn't handle shutdown signals properly, causing errors on client disconnect.

**Tasks:**
- [x] Add isShuttingDown flag to prevent double-shutdown race conditions
- [x] Handle SIGTERM and SIGHUP signals in addition to SIGINT
- [x] Handle stdin close/end/error when MCP client disconnects
- [x] Gracefully handle EPIPE/EOF errors as expected disconnection
- [x] Call server.close() during shutdown for proper cleanup

**Completed Changes:**
- `Server~/src/index.ts`: Added graceful shutdown handler with signal/stdin handling

**PR:** `fix/graceful-shutdown`

---

### 1.2 Connection Resilience ✅

**Problem:** Domain reloads during Play Mode break the WebSocket connection, requiring manual intervention.

**Tasks:**
- [x] Implement automatic reconnection with exponential backoff (1s, 2s, 4s, 8s, max 30s)
- [x] Add connection state enum: `Disconnected`, `Connecting`, `Connected`, `Reconnecting`
- [x] Emit connection state change events for UI feedback
- [x] Preserve server state across reconnections
- [x] Add heartbeat/ping-pong mechanism to detect stale connections
- [x] Handle Unity's `EditorApplication.playModeStateChanged` gracefully

**Completed Changes:**
- `Server~/src/unity/unityConnection.ts`: New file with `UnityConnection` class that manages WebSocket connections with:
  - `ConnectionState` enum for state tracking
  - Exponential backoff reconnection (configurable min/max delay, multiplier)
  - Heartbeat/ping-pong mechanism with configurable interval and timeout
  - Event emission for state changes (`stateChange`, `message`, `error`)
  - Automatic reconnection on disconnect (unless manual)
  - Stale connection detection via heartbeat
- `Server~/src/unity/mcpUnity.ts`: Refactored to use `UnityConnection` class
  - Added `connectionState` getter exposing current state
  - Added `isConnecting` getter for connecting/reconnecting state
  - Added `onConnectionStateChange()` method for state change listeners
  - Added `forceReconnect()` method for manual reconnection
  - Added `getConnectionStats()` method for diagnostics
- `Server~/src/__tests__/unityConnection.test.ts`: Added 14 tests for connection handling
- `Server~/jest.config.js`: Added Jest configuration for ESM support
- `Server~/package.json`: Added test scripts and Jest dependencies

**Unity C# Side (Already Implemented):**
- `Editor/UnityBridge/McpUnityServer.cs` already handles `playModeStateChanged`:
  - Stops server when exiting Edit Mode
  - Restarts server when entering Edit Mode (if AutoStartServer enabled)
  - Handles assembly reload events (before/after)

**Files modified:**
- `Server~/src/unity/unityConnection.ts` (new file)
- `Server~/src/unity/mcpUnity.ts`
- `Server~/package.json`
- `Server~/jest.config.js` (new file)
- `Server~/tsconfig.json`
- `Server~/src/__tests__/unityConnection.test.ts` (new file)

**Branch:** `feature/connection-resilience`

---

### 1.3 Command Queuing ✅

**Problem:** Commands sent during disconnection are lost.

**Tasks:**
- [x] Create command queue in TypeScript server
- [x] Queue commands when connection state is `Reconnecting`
- [x] Replay queued commands on successful reconnection
- [x] Add configurable queue size limit (default: 100 commands)
- [x] Add queue timeout (default: 60s) to discard stale commands
- [x] Return appropriate status to MCP client during queuing

**Completed Changes:**
- `Server~/src/unity/commandQueue.ts`: New `CommandQueue` class with:
  - Configurable max size (default: 100) and timeout (default: 60s)
  - `enqueue()`, `drain()`, `clear()`, `peek()` methods
  - Automatic cleanup of expired commands with periodic timer
  - Statistics tracking (dropped, expired, replayed counts)
- `Server~/src/unity/mcpUnity.ts`: Integration with McpUnity:
  - `SendRequestOptions` interface for per-request `queueIfDisconnected` control
  - `McpUnityConfig` interface for queue configuration
  - Commands queued when connection is `Reconnecting` or `Connecting`
  - Automatic replay of queued commands when connection is restored
  - `setQueueingEnabled()`, `getQueueStats()`, `queuedCommandCount` methods
- `Server~/src/__tests__/commandQueue.test.ts`: 14 Jest tests for CommandQueue

**Branch:** `feature/command-queuing`

---

## Phase 2: Core Tool Expansion

New tools that address common Unity workflows.

### 2.1 Transform Manipulation Tools ✅

**New tools:**

#### `move_gameobject`
```typescript
{
  path: string,           // Hierarchy path or instance ID
  position: { x: number, y: number, z: number },
  space: "world" | "local",  // Default: "world"
  relative: boolean       // If true, adds to current position. Default: false
}
```

#### `rotate_gameobject`
```typescript
{
  path: string,
  rotation: { x: number, y: number, z: number },  // Euler angles
  space: "world" | "local",
  relative: boolean
}
```

#### `scale_gameobject`
```typescript
{
  path: string,
  scale: { x: number, y: number, z: number },
  relative: boolean       // If true, multiplies current scale
}
```

#### `set_transform`
```typescript
{
  path: string,
  position?: { x: number, y: number, z: number },
  rotation?: { x: number, y: number, z: number },
  scale?: { x: number, y: number, z: number },
  space: "world" | "local"
}
```

**Files created:**
- `Editor/Tools/TransformTools.cs`
- `Server~/src/tools/transformTools.ts`

**Completed Changes:**
- `MoveGameObjectTool`: Moves GameObjects with world/local space and absolute/relative positioning
- `RotateGameObjectTool`: Rotates GameObjects using Euler angles with world/local space and absolute/relative rotation
- `ScaleGameObjectTool`: Scales GameObjects with absolute or relative (multiplicative) scaling
- `SetTransformTool`: Comprehensive tool to set position, rotation, and scale in one operation
- `TransformToolUtils`: Shared utility class for GameObject finding and path resolution
- All tools support both `instanceId` and `objectPath` for GameObject identification
- All tools properly integrate with Unity's Undo system
- Tools registered in `McpUnityServer.cs` and `index.ts`

---

### 2.2 GameObject Operations ✅

**New tools:**

#### `duplicate_gameobject`
```typescript
{
  instanceId?: number,    // Instance ID of the GameObject
  objectPath?: string,    // Hierarchy path (alternative to instanceId)
  newName?: string,       // Default: "{originalName}"
  newParent?: string,     // New parent path. Default: same parent
  newParentId?: number,   // New parent instance ID (alternative to newParent)
  count?: number          // Number of copies. Default: 1, Max: 100
}
```

#### `delete_gameobject`
```typescript
{
  instanceId?: number,    // Instance ID of the GameObject
  objectPath?: string,    // Hierarchy path (alternative to instanceId)
  includeChildren: boolean  // Default: true. If false, children are moved to parent.
}
```

#### `reparent_gameobject`
```typescript
{
  instanceId?: number,    // Instance ID of the GameObject
  objectPath?: string,    // Hierarchy path (alternative to instanceId)
  newParent?: string | null,  // null = root level
  newParentId?: number | null,  // Instance ID of new parent (alternative to newParent)
  worldPositionStays: boolean  // Default: true
}
```

**Completed Changes:**
- `Editor/Tools/GameObjectTools.cs`: New file containing:
  - `GameObjectToolUtils`: Utility class for finding GameObjects by ID or path
  - `DuplicateGameObjectTool`: Duplicates GameObjects with optional rename, reparent, and batch copy
  - `DeleteGameObjectTool`: Deletes GameObjects with option to preserve children
  - `ReparentGameObjectTool`: Changes GameObject parent with world position preservation option
- `Server~/src/tools/gameObjectTools.ts`: TypeScript handlers for all three tools
- All tools support both `instanceId` and `objectPath` for GameObject identification
- All tools properly integrate with Unity's Undo system
- Tools registered in `McpUnityServer.cs` and `index.ts`

**Files created:**
- `Editor/Tools/GameObjectTools.cs`
- `Server~/src/tools/gameObjectTools.ts`

---

### 2.3 Scene Management Tools ✅

**New tools:**

#### `create_scene` (Already existed)
```typescript
{
  name: string,
  addToBuild: boolean,    // Default: false
  makeActive: boolean     // Default: true
}
```

#### `load_scene` (Already existed)
```typescript
{
  path: string,           // Scene asset path
  mode: "single" | "additive",  // Default: "single"
  makeActive: boolean
}
```

#### `save_scene`
```typescript
{
  path?: string,          // If omitted, saves to current path
  saveAs: boolean         // Default: false
}
```

#### `get_scene_info`
```typescript
{
  // No parameters - returns active scene info
}
// Returns: { name, path, isDirty, rootCount, isLoaded }
```

#### `unload_scene`
```typescript
{
  path: string
}
```

**Completed Changes:**
- `Editor/Tools/SaveSceneTool.cs`: Saves the active scene, with optional Save As functionality to a new path
- `Editor/Tools/GetSceneInfoTool.cs`: Returns comprehensive info about the active scene and all loaded scenes
- `Editor/Tools/UnloadSceneTool.cs`: Unloads a scene from the hierarchy without deleting the asset
- `Server~/src/tools/saveSceneTool.ts`: TypeScript handler for save_scene
- `Server~/src/tools/getSceneInfoTool.ts`: TypeScript handler for get_scene_info
- `Server~/src/tools/unloadSceneTool.ts`: TypeScript handler for unload_scene
- Tools registered in `McpUnityServer.cs` and `index.ts`

**Notes:**
- `create_scene` and `load_scene` already existed in the codebase
- `delete_scene` also existed (removes scene asset entirely, different from unload)
- All tools follow existing patterns with proper error handling

---

### 2.4 Material & Shader Tools ✅

**New tools:**

#### `create_material`
```typescript
{
  name: string,
  shader: string,         // Shader name, e.g., "Standard", "Universal Render Pipeline/Lit"
  savePath: string,       // Asset path to save
  properties?: Record<string, any>  // Initial property values
}
```

#### `assign_material`
```typescript
{
  instanceId?: number,    // Instance ID of the GameObject
  objectPath?: string,    // Hierarchy path (alternative to instanceId)
  materialPath: string,   // Asset path to material
  slot?: number           // Material slot index. Default: 0
}
```

#### `modify_material`
```typescript
{
  materialPath: string,
  properties: Record<string, any>  // Property name -> value
}
// Supports: colors (_Color), floats (_Metallic), textures (_MainTex path)
```

#### `get_material_info`
```typescript
{
  materialPath: string
}
// Returns: shader, all properties with current values
```

**Completed Changes:**
- `Editor/Tools/MaterialTools.cs`: New file containing:
  - `MaterialToolUtils`: Utility class for shader lookup, material loading, and property conversion
  - `CreateMaterialTool`: Creates new materials with specified shader and optional initial properties
  - `AssignMaterialTool`: Assigns materials to GameObjects' Renderer components at specific slots
  - `ModifyMaterialTool`: Modifies material properties (colors, floats, vectors, textures)
  - `GetMaterialInfoTool`: Returns comprehensive material info including all shader properties with current values
- `Server~/src/tools/materialTools.ts`: TypeScript handlers for all four material tools
- All tools support Unity's Undo system for reversible operations
- Tools registered in `McpUnityServer.cs` and `index.ts`

**Files created:**
- `Editor/Tools/MaterialTools.cs`
- `Server~/src/tools/materialTools.ts`

---

### 2.5 Animation Tools

**New tools:**

#### `create_animation_clip`
```typescript
{
  name: string,
  savePath: string,
  length: number,         // Duration in seconds
  wrapMode: "once" | "loop" | "pingPong" | "clampForever"
}
```

#### `add_animation_curve`
```typescript
{
  clipPath: string,
  propertyPath: string,   // e.g., "localPosition.x"
  type: string,           // Component type, e.g., "Transform"
  keyframes: Array<{ time: number, value: number, inTangent?: number, outTangent?: number }>
}
```

#### `set_animator_parameter`
```typescript
{
  gameObjectPath: string,
  parameterName: string,
  value: boolean | number | string  // Type inferred from parameter type
}
```

#### `play_animation`
```typescript
{
  gameObjectPath: string,
  stateName: string,
  layer?: number,
  normalizedTime?: number
}
```

**Files to create:**
- `Editor/Tools/AnimationTools.cs`
- `Server/src/tools/animationTools.ts`

---

### 2.6 Prefab Tools

**New tools:**

#### `unpack_prefab`
```typescript
{
  gameObjectPath: string,
  mode: "root" | "completely"  // Default: "root"
}
```

#### `apply_prefab_overrides`
```typescript
{
  gameObjectPath: string,
  applyTo: "base" | "all"  // Apply to base prefab or all nested
}
```

#### `revert_prefab`
```typescript
{
  gameObjectPath: string,
  revertMode: "all" | "properties" | "addedComponents" | "addedGameObjects"
}
```

#### `get_prefab_info`
```typescript
{
  gameObjectPath: string
}
// Returns: isPrefab, prefabAssetPath, hasOverrides, overrideDetails
```

**Files to create:**
- `Editor/Tools/PrefabTools.cs`
- `Server/src/tools/prefabTools.ts`

---

## Phase 3: Resource Expansion

New resources that provide richer context to AI assistants.

### 3.1 Project Settings Resource

**New resource:** `unity://project-settings/{category}`

**Categories:**
- `player` - Player settings (company name, product name, icons, splash)
- `quality` - Quality levels and settings
- `physics` - Physics settings (gravity, layers, etc.)
- `physics2d` - 2D physics settings
- `time` - Time settings (fixed timestep, timescale)
- `audio` - Audio settings
- `editor` - Editor-specific settings
- `tags-and-layers` - Tags and sorting/physics layers
- `input` - Input manager axes (legacy)

**Implementation:**
- Read from `ProjectSettings/*.asset` files
- Parse YAML format
- Return structured JSON

**Files to create:**
- `Editor/Resources/ProjectSettingsResource.cs`
- `Server/src/resources/projectSettings.ts`

---

### 3.2 Scripts Resource

**New resource:** `unity://scripts`

**Returns:**
```json
{
  "scripts": [
    {
      "name": "PlayerController",
      "namespace": "Game.Player",
      "path": "Assets/Scripts/Player/PlayerController.cs",
      "guid": "abc123...",
      "baseClass": "MonoBehaviour",
      "publicFields": [
        { "name": "moveSpeed", "type": "float", "defaultValue": 5.0 },
        { "name": "jumpForce", "type": "float", "defaultValue": 10.0 }
      ],
      "publicMethods": [
        { "name": "Jump", "parameters": [], "returnType": "void" },
        { "name": "TakeDamage", "parameters": ["int amount"], "returnType": "void" }
      ],
      "serializedFields": [
        { "name": "groundCheck", "type": "Transform" }
      ]
    }
  ]
}
```

**Implementation:**
- Use `TypeCache.GetTypesDerivedFrom<MonoBehaviour>()`
- Reflect on types to get fields/methods
- Cache results, invalidate on script recompile

**Files to create:**
- `Editor/Resources/ScriptsResource.cs`
- `Server/src/resources/scripts.ts`

---

### 3.3 Selection Resource

**New resource:** `unity://selection`

**Returns:**
```json
{
  "activeGameObject": {
    "instanceId": 12345,
    "name": "Player",
    "path": "/Player"
  },
  "selectedGameObjects": [...],
  "activeObject": {
    "type": "Material",
    "name": "PlayerMaterial",
    "path": "Assets/Materials/PlayerMaterial.mat"
  },
  "selectedObjects": [...]
}
```

**Implementation:**
- Read from `Selection.activeGameObject`, `Selection.gameObjects`
- Read from `Selection.activeObject`, `Selection.objects`

**Files to create:**
- `Editor/Resources/SelectionResource.cs`
- `Server/src/resources/selection.ts`

---

### 3.4 Build Settings Resource

**New resource:** `unity://build-settings`

**Returns:**
```json
{
  "scenes": [
    { "path": "Assets/Scenes/Main.unity", "enabled": true, "index": 0 }
  ],
  "target": "StandaloneWindows64",
  "targetGroup": "Standalone",
  "development": false,
  "scriptingBackend": "IL2CPP"
}
```

**Files to create:**
- `Editor/Resources/BuildSettingsResource.cs`
- `Server/src/resources/buildSettings.ts`

---

## Phase 4: Developer Experience

Improvements that make the tool easier to use and debug.

### 4.1 Enhanced Error Messages

**Tasks:**
- [ ] Create custom exception types for common errors
- [ ] Include Unity stack traces in error responses
- [ ] Add error codes for programmatic handling
- [ ] Include suggested fixes in error messages
- [ ] Log errors to Unity console with clickable stack traces

**Error format:**
```json
{
  "error": {
    "code": "GAMEOBJECT_NOT_FOUND",
    "message": "GameObject '/Player/Weapon' not found in scene",
    "suggestion": "Check the hierarchy path. Available root objects: Main Camera, Player, Environment",
    "stackTrace": "...",
    "timestamp": "2024-01-15T10:30:00Z"
  }
}
```

**Files to create/modify:**
- `Editor/Errors/McpError.cs` (new)
- `Editor/Errors/ErrorCodes.cs` (new)
- `Server/src/errors/mcpErrors.ts` (new)
- All existing tool handlers

---

### 4.2 Batch Operations

**New tool:** `batch_execute`

```typescript
{
  operations: Array<{
    tool: string,
    parameters: Record<string, any>,
    id?: string  // Optional ID for referencing results
  }>,
  stopOnError: boolean,  // Default: true
  atomic: boolean        // If true, rollback all on any failure. Default: false
}
```

**Returns:**
```json
{
  "results": [
    { "id": "op1", "success": true, "result": {...} },
    { "id": "op2", "success": false, "error": {...} }
  ],
  "summary": {
    "total": 5,
    "succeeded": 4,
    "failed": 1
  }
}
```

**Files to create:**
- `Editor/Tools/BatchExecutor.cs`
- `Server/src/tools/batchExecute.ts`

---

### 4.3 Undo Support

**Tasks:**
- [ ] Wrap all GameObject creation in `Undo.RegisterCreatedObjectUndo()`
- [ ] Wrap all property modifications in `Undo.RecordObject()`
- [ ] Group related operations with `Undo.SetCurrentGroupName()`
- [ ] Add `undoGroupName` parameter to all modification tools
- [ ] Create `undo` tool to programmatically undo operations

**New tool:** `undo`
```typescript
{
  steps?: number  // Number of undo steps. Default: 1
}
```

**New tool:** `redo`
```typescript
{
  steps?: number
}
```

**New tool:** `get_undo_history`
```typescript
{
  limit?: number  // Default: 20
}
// Returns list of undo group names
```

**Files to modify:**
- All files in `Editor/Tools/`

---

### 4.4 Dry-Run Mode

**Tasks:**
- [ ] Add `dryRun: boolean` parameter to all modification tools
- [ ] When `dryRun: true`, validate and return what would change without applying
- [ ] Include validation errors in dry-run response

**Dry-run response format:**
```json
{
  "dryRun": true,
  "wouldSucceed": true,
  "changes": [
    { "type": "create", "target": "GameObject", "name": "NewObject" },
    { "type": "modify", "target": "Transform", "property": "position", "from": [0,0,0], "to": [1,2,3] }
  ],
  "warnings": [
    "GameObject 'Player' already has a Rigidbody component"
  ]
}
```

**Files to modify:**
- All files in `Editor/Tools/`
- All files in `Server/src/tools/`

---

## Phase 5: Security & Safety

Protect users from unintended or malicious operations.

### 5.1 Operation Permissions

**Tasks:**
- [ ] Create permission configuration file (`mcp-unity-permissions.json`)
- [ ] Define permission levels: `read`, `write`, `execute`, `admin`
- [ ] Map each tool to required permission level
- [ ] Add allowlist/blocklist for specific tools
- [ ] Add path restrictions (can only modify files under certain paths)
- [ ] UI in Unity Editor to configure permissions

**Permission config format:**
```json
{
  "defaultLevel": "write",
  "tools": {
    "delete_gameobject": "admin",
    "execute_menu_item": "admin",
    "run_tests": "execute"
  },
  "blocklist": ["delete_gameobject"],
  "allowlist": null,
  "pathRestrictions": {
    "allowedPaths": ["Assets/"],
    "blockedPaths": ["Assets/Plugins/"]
  }
}
```

**Files to create:**
- `Editor/Security/PermissionManager.cs`
- `Editor/Security/PermissionConfig.cs`
- `Editor/UI/PermissionsWindow.cs`
- `Server/src/security/permissions.ts`

---

### 5.2 Confirmation Prompts

**Tasks:**
- [ ] Identify destructive operations (delete, overwrite, clear)
- [ ] Add `requireConfirmation` flag to dangerous tools
- [ ] Implement confirmation flow via MCP protocol
- [ ] Add Unity Editor dialog for confirmation
- [ ] Allow users to configure which operations need confirmation
- [ ] Add "always allow" option per tool

**Destructive operations requiring confirmation:**
- `delete_gameobject`
- `delete_asset`
- `clear_console`
- `unload_scene`
- Overwriting existing files/assets

**Files to create:**
- `Editor/Security/ConfirmationManager.cs`
- `Server/src/security/confirmation.ts`

---

### 5.3 Audit Logging

**Tasks:**
- [ ] Create audit log file (`Logs/mcp-unity-audit.log`)
- [ ] Log all operations with: timestamp, tool name, parameters, result, duration
- [ ] Add log rotation (max file size, max files)
- [ ] Create audit log viewer in Unity Editor
- [ ] Add option to export logs as JSON/CSV

**Log format:**
```json
{
  "timestamp": "2024-01-15T10:30:00.123Z",
  "tool": "update_gameobject",
  "parameters": { "path": "/Player", "name": "Hero" },
  "result": "success",
  "duration_ms": 15,
  "changes": [
    { "property": "name", "from": "Player", "to": "Hero" }
  ]
}
```

**Files to create:**
- `Editor/Logging/AuditLogger.cs`
- `Editor/Logging/LogRotation.cs`
- `Editor/UI/AuditLogWindow.cs`

---

## Phase 6: Performance

Optimizations for large projects.

### 6.1 Streaming for Large Resources

**Tasks:**
- [ ] Implement pagination for list resources
- [ ] Add `limit` and `offset` parameters to resource queries
- [ ] Implement cursor-based pagination for very large datasets
- [ ] Add streaming support for hierarchy traversal
- [ ] Return total count with paginated results

**Paginated response format:**
```json
{
  "items": [...],
  "pagination": {
    "total": 15000,
    "limit": 100,
    "offset": 0,
    "hasMore": true,
    "nextCursor": "abc123"
  }
}
```

**Resources to paginate:**
- `unity://scenes-hierarchy` (large scenes)
- `unity://assets` (large projects)
- `unity://scripts` (many scripts)
- `unity://logs` (many log entries)

**Files to modify:**
- All files in `Editor/Resources/`
- All files in `Server/src/resources/`

---

### 6.2 Caching Layer

**Tasks:**
- [ ] Identify static/semi-static resources
- [ ] Implement in-memory cache with TTL
- [ ] Add cache invalidation hooks
- [ ] Add cache statistics endpoint
- [ ] Make caching configurable

**Cache configuration:**
```json
{
  "enabled": true,
  "resources": {
    "unity://menu-items": { "ttl": 3600 },
    "unity://packages": { "ttl": 300 },
    "unity://scripts": { "ttl": 60, "invalidateOn": ["scriptRecompile"] }
  }
}
```

**Invalidation triggers:**
- Script recompile: invalidate scripts cache
- Asset import: invalidate assets cache
- Scene change: invalidate hierarchy cache
- Package change: invalidate packages cache

**Files to create:**
- `Server/src/cache/cacheManager.ts`
- `Server/src/cache/cacheConfig.ts`
- `Editor/Cache/CacheInvalidator.cs`

---

## Phase 7: Documentation & Onboarding

Make the project accessible to new users.

### 7.1 Example Prompts Library

**Tasks:**
- [ ] Create `docs/prompts/` directory
- [ ] Write example prompts for common workflows
- [ ] Categorize by use case
- [ ] Include expected outcomes
- [ ] Add to README

**Categories:**
- Scene setup
- GameObject manipulation
- Material and lighting
- Animation
- Testing
- Build and deployment

**Example prompt document:**
```markdown
# Creating a Basic Scene

## Prompt
"Create a new scene called 'Level1' with a ground plane, a player spawn point, and directional lighting"

## What MCP Unity Does
1. Creates new scene "Level1"
2. Creates plane named "Ground" at origin
3. Creates empty GameObject "PlayerSpawn" at (0, 1, 0)
4. Creates Directional Light with default settings

## Tools Used
- create_scene
- update_gameobject (x3)
- update_component (for light settings)
```

**Files to create:**
- `docs/prompts/README.md`
- `docs/prompts/scene-setup.md`
- `docs/prompts/gameobject-manipulation.md`
- `docs/prompts/materials-lighting.md`
- `docs/prompts/animation.md`
- `docs/prompts/testing.md`

---

### 7.2 Video Tutorials

**Tasks:**
- [ ] Plan video content outline
- [ ] Record installation walkthrough
- [ ] Record basic usage demo
- [ ] Record advanced workflows
- [ ] Upload to YouTube/hosting
- [ ] Embed in README

**Video list:**
1. **Installation & Setup** (5 min)
   - Installing Node.js
   - Adding Unity package
   - Configuring AI client

2. **Basic Usage** (10 min)
   - Connecting to Unity
   - Creating GameObjects
   - Modifying properties
   - Running tests

3. **Advanced Workflows** (15 min)
   - Batch operations
   - Custom tool creation
   - Debugging connections

---

### 7.3 Troubleshooting Guide

**Tasks:**
- [ ] Document common errors and solutions
- [ ] Add diagnostic commands
- [ ] Create self-test tool
- [ ] Add to wiki/docs

**New tool:** `diagnose`
```typescript
{
  // No parameters
}
// Returns system diagnostic info
```

**Diagnostic response:**
```json
{
  "unityVersion": "6000.0.0f1",
  "mcpUnityVersion": "1.2.0",
  "nodeVersion": "20.0.0",
  "websocketStatus": "connected",
  "lastMessageTime": "2024-01-15T10:30:00Z",
  "pendingCommands": 0,
  "cacheStats": { "hits": 150, "misses": 20 },
  "permissions": { "level": "write", "blockedTools": [] }
}
```

**Files to create:**
- `docs/TROUBLESHOOTING.md`
- `Editor/Tools/DiagnoseTools.cs`
- `Server/src/tools/diagnose.ts`

---

## Implementation Order

Recommended order based on dependencies and impact:

### Sprint 1: Critical Stability
1. 1.1 Fix Spaces in Path
2. 1.2 Connection Resilience
3. 1.3 Command Queuing

### Sprint 2: Core Tools
4. 2.1 Transform Tools
5. 2.2 GameObject Operations
6. 2.3 Scene Management

### Sprint 3: Extended Tools
7. 2.4 Material Tools
8. 2.5 Animation Tools
9. 2.6 Prefab Tools

### Sprint 4: Resources & DX
10. 3.1-3.4 New Resources
11. 4.1 Enhanced Errors
12. 4.3 Undo Support

### Sprint 5: Advanced Features
13. 4.2 Batch Operations
14. 4.4 Dry-Run Mode
15. 6.1 Streaming/Pagination

### Sprint 6: Security
16. 5.1 Permissions
17. 5.2 Confirmations
18. 5.3 Audit Logging

### Sprint 7: Polish
19. 6.2 Caching
20. 7.1 Prompt Library
21. 7.3 Troubleshooting Guide
22. 7.2 Video Tutorials

---

## Contributing

Each phase should be implemented as separate PRs:
- Create feature branch from `main`
- Implement with tests
- Update documentation
- Submit PR with description of changes

## Testing Strategy

- **Unit tests**: All new C# code should have NUnit tests
- **Integration tests**: TypeScript handlers should have Jest tests
- **E2E tests**: Test full flow from MCP client to Unity and back
- **Manual testing**: Verify with Claude Desktop, Cursor, and Windsurf
