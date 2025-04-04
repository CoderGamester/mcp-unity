# MCP Unity Server

The purpose of this package is to provide a MCP Unity Server for executing Unity operations and request Editor information from AI MCP enabled hosts.

## Setup

### Requirements
- Unity 2022.3 or higher

### Installation
1. Open the Package Manager in Unity
2. Click the "+" button and select "Add package from git URL..."
3. Enter the repository URL: `https://github.com/CoderGamester/mcp-unity.git`
4. Click "Add"

Or add the following dependency to your `manifest.json`:
```json
"com.gamelovers.mcp-unity": "https://github.com/CoderGamester/mcp-unity.git"
```

For local development, you can add the package via the filesystem:
```json
"com.gamelovers.mcp-unity": "file:/path/to/mcp-unity"
```

## Running Tests

Due to how Unity handles tests in local packages, you may need to manually copy test files into your project to run them:

1. Create a new folder at `Assets/McpTests`
2. Copy the test files from `Packages/com.gamelovers.mcp-unity/Tests/Editor` to `Assets/McpTests`
3. Open the Test Runner window (Window > General > Test Runner)
4. Tests should now appear in the EditMode tab

## Further Documentation
See the [Wiki](https://github.com/CoderGamester/mcp-unity/wiki) for full documentation.