Since you’re building on top of **CoderGamester/mcp-unity**, you already have a solid foundation. That repository handles the heavy lifting of the **JSON-RPC** communication between the AI and the Unity Editor.

To turn this into an **MCP App** (the visual UI component in VS Code 1.109), you need to bridge the "Server" (which talks to Unity) with a "Webview" (which displays the UI in the chat).

### The Architecture

---

## Step 1: Define the UI Tool in the Server

In the `mcp-unity` server code (usually in `index.ts` depending on the fork), you need to register a tool that returns a **Resource**. This resource tells VS Code to render your App.

Add a tool like `get_unity_dashboard`:

```typescript
// If using the TypeScript version of mcp-unity
server.setRequestHandler(ListToolsRequestSchema, async () => {
  return {
    tools: [
      {
        name: "show_unity_dashboard",
        description: "Opens a visual dashboard to inspect the Unity scene and control playback.",
        inputSchema: { type: "object", properties: {} }
      }
    ]
  };
});

server.setRequestHandler(CallToolRequestSchema, async (request) => {
  if (request.params.name === "show_unity_dashboard") {
    return {
      content: [
        {
          type: "resource",
          uri: "unity://ui/dashboard",
          metadata: { "view": "mcp-app" } // This tells VS Code 1.109 to treat this as an App
        }
      ]
    };
  }
});

```

---

## Step 2: Create the MCP App (The Frontend)

You need an HTML file that acts as your visual interface. VS Code will load this in a sandboxed iframe. Create a file named `unity-dashboard.html` in your project.

```html
<!DOCTYPE html>
<html>
<head>
    <script src="https://cdn.jsdelivr.net/npm/@modelcontextprotocol/sdk-apps/dist/index.js"></script>
    <style>
        body { font-family: sans-serif; color: var(--vscode-foreground); padding: 10px; }
        .btn { background: #007acc; color: white; border: none; padding: 5px 10px; cursor: pointer; }
        .status-playing { color: #4caf50; }
    </style>
</head>
<body>
    <h3>Unity Control Panel</h3>
    <div id="status">Connecting to Unity...</div>
    <hr/>
    <button class="btn" onclick="togglePlay()">Play/Pause</button>
    <button class="btn" onclick="refreshScene()">Refresh Hierarchy</button>
    
    <ul id="hierarchy"></ul>

    <script>
        const mcp = new MCPApp();

        async function togglePlay() {
            // This calls the existing tools in CoderGamester/mcp-unity
            await mcp.callTool("execute_command", { command: "EditorApplication.isPlaying = !EditorApplication.isPlaying;" });
            updateStatus();
        }

        async function updateStatus() {
            const result = await mcp.callTool("get_project_info", {});
            document.getElementById('status').innerText = `Unity Project: ${result.projectName}`;
        }

        mcp.on('ready', updateStatus);
    </script>
</body>
</html>

```

---

## Step 3: Register the App Resource

You must tell the server to "serve" this HTML file when the URI `unity://ui/dashboard` is requested.

```typescript
server.setRequestHandler(ReadResourceRequestSchema, async (request) => {
  if (request.params.uri === "unity://ui/dashboard") {
    const htmlContent = fs.readFileSync('./unity-dashboard.html', 'utf8');
    return {
      contents: [{
        uri: request.params.uri,
        mimeType: "text/html",
        text: htmlContent
      }]
    };
  }
});

```

---

## Step 4: Configuration in VS Code

To see your new App in action, ensure your `mcp.json` (accessible via `MCP: Open User Configuration`) points to your modified server:

```json
{
  "mcpServers": {
    "unity-custom": {
      "command": "node",
      "args": ["Server~/build/index.js"],
      "env": {
        "UNITY_PORT": "8090"
      }
    }
  }
}

```

---

### Why this is better than just the Server:

* **Zero-Latency Hierarchy:** Instead of asking the AI "What's in my scene?" and waiting for a text list, the App can fetch the hierarchy and display it as a clickable tree.
* **Visual Feedback:** You can see if Unity is in "Play Mode" at a glance without typing a command.
* **Custom Inspectors:** You can build sliders to adjust variables in real-time while the AI is writing code in the main editor window.