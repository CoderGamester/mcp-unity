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
