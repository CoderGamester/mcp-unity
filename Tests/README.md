# MCP Unity Tests

This directory contains unit tests for the MCP Unity Server package.

## Important: Test Discovery in Unity

Unity has specific requirements for package test discovery when using local packages. While these tests follow the standard package structure, they may not appear in the Test Runner when the package is included as a local package.

## Workaround for Local Development

To run these tests in your project:

1. Create a new folder at `Assets/McpTests` in your main project
2. Copy all the test files from `Packages/com.gamelovers.mcp-unity/Tests/Editor` to `Assets/McpTests`
3. Open the Test Runner window (Window > General > Test Runner)
4. Tests should now appear in the EditMode tab

This workaround is necessary because Unity sometimes has difficulty discovering tests in local packages. When the package is published to a registry, this workaround should not be needed.

## Test Structure

Tests are organized following Unity's package layout guidelines:

- `Editor`: Contains tests that run in Edit mode
- `Runtime`: Contains tests that run in Play mode (currently not implemented)

Each directory has its own assembly definition file to ensure proper reference handling.