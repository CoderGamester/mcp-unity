# MCP Unity - AI Agent Guide

## 1. Project Overview
**MCP Unity** bridges AI assistants (Cursor, Claude, Windsurf, Google Antigravity) to the Unity Editor via the Model Context Protocol (MCP). It allows agents to inspect scenes, manipulate GameObjects, run tests, and manage packages directly within Unity.

## 2. Architecture
- **Dual Architecture**:
  - **Server (Node.js/TypeScript)**: Runs as the MCP server (stdio), handles protocol messages, and forwards requests to Unity via WebSocket.
  - **Client (Unity/C#)**: Runs inside Unity Editor, listens for WebSocket commands, executes Editor API calls, and returns results.
- **Communication**: JSON-RPC over WebSocket (default port 8080).

## 3. Directory Structure
```
/
├── Editor/                 # Unity C# Editor Code (The "Client")
│   ├── Tools/              # Tool implementations (McpToolBase)
│   ├── Resources/          # Resource implementations (McpResourceBase)
│   ├── UnityBridge/        # WebSocket server & message handling
│   └── Services/           # Core services (Logs, Tests)
├── Server~/                # Node.js MCP Server Code (The "Server")
│   ├── src/tools/          # MCP Tool definitions (mirrors Editor/Tools)
│   ├── src/resources/      # MCP Resource definitions
│   └── src/unity/          # WebSocket client logic
└── docs/                   # Documentation images

## 4. Key Files
- `McpUnitySocketHandler.cs` — WebSocket message routing
- `McpUnityServer.cs` — Unity-side server lifecycle
- `McpUnity.ts` — Node.js client connecting to Unity
- `index.ts` — MCP server entry, tool/resource registration
```

## 5. Coding Standards
- **Unity**: Unity 6 standards.
- **C#**: C# 9.0 features allowed.
- **Namespaces**: Explicit namespaces (e.g., `McpUnity.Tools`). No global `using`.
- **Async**: Use `IsAsync = true` in tools/resources for main thread operations (Editor API).

## 6. Development Workflow
To add a new capability (e.g., "RotateObject"):

1.  **Unity (C#)**:
    - Create `Editor/Tools/RotateObjectTool.cs` inheriting `McpToolBase`.
    - Implement `Execute` (sync) or `ExecuteAsync` (if touching Editor API).
    - Register in `McpUnityServer.cs` (if not auto-discovered).

2.  **Server (TS)**:
    - Create `Server~/src/tools/rotateObjectTool.ts`.
    - Define input schema (zod) and tool handler.
    - Forward request to Unity via `McpUnity.instance.callTool("RotateObject", args)`.

3.  **Build**:
    - Unity compiles automatically.
    - Server: `cd Server~ && npm run build`.

## 7. Update Policy
- **Update this file** when architecture changes, whenever tools/resources are added/renamed/removed, or when new core patterns are introduced.
- **Keep concise**: Focus on high-level patterns for AI context.

