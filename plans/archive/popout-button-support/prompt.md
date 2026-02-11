To fix the "popout" issue and keep the dashboard visible, follow these steps:

### 1. Fix the "Open in Editor" (Popout) Capability

If you don't see the "Open in Editor" icon in the top-right of that dashboard frame, it is likely because the server is not sending the correct metadata. In your **`mcp-unity` server** code (the Node.js/TypeScript part), ensure your `show_unity_dashboard` tool returns the resource with the `ui: true` flag:

```typescript
// server.ts snippet
server.setRequestHandler(CallToolRequestSchema, async (request) => {
  if (request.params.name === "show_unity_dashboard") {
    return {
      content: [
        {
          type: "resource",
          uri: "unity://ui/dashboard",
          metadata: { 
            "ui": true, // Critical for VS Code 1.109 to treat it as an App
            "title": "Unity Dashboard" 
          }
        }
      ]
    };
  }
});

```

### 2. How to Pin the App (No Code Needed)

Once the metadata is correct, VS Code provides a native way to pin this:

1. **Click the Icon:** Look at the bar labeled **"dashboard"** in your screenshot. To the right of that text, a small **"Open in Editor"** icon (a square with an arrow) should appear.
2. **Split View:** Once it opens as a tab, **drag the tab** to the far right edge of your screen.
3. **Result:** You now have a persistent Unity Dashboard on the right that stays visible even as the chat scrolls.

---

### 3. Adding a "Live Console" to your Dashboard

To make the "Console Logs" section in your screenshot actually work, you need to add a script to your `dashboard.html` that polls the existing `unity://logs` resource from `mcp-unity`.

**Update your `dashboard.html` script section:**

```javascript
const app = new MCPApp();

async function refreshLogs() {
    try {
        // Calls the existing resource in CoderGamester/mcp-unity
        const logs = await app.readResource("unity://logs");
        const logContainer = document.querySelector('.console-logs'); // Adjust to your class
        
        logContainer.innerHTML = logs.contents.map(log => `
            <div style="border-bottom: 1px solid #333; padding: 4px; font-size: 11px;">
                <span style="color: ${log.level === 'Error' ? '#f44' : '#aaa'}">[${log.level}]</span> 
                ${log.message}
            </div>
        `).join('');
        
        // Auto-scroll to bottom
        logContainer.scrollTop = logContainer.scrollHeight;
    } catch (e) {
        console.error("Failed to fetch logs", e);
    }
}

// Refresh every 2 seconds if the "Auto" checkbox is checked
setInterval(() => {
    if (document.getElementById('auto-refresh-logs').checked) {
        refreshLogs();
    }
}, 2000);

```

---

### Summary of Workflow Improvements

* **Sync to Agent:** Use `app.updateModelContext()` inside your "Sync" button to send the current Hierarchy state to the AI so it "sees" what you see.
* **Persistent UI:** By moving the dashboard to a **Split Editor Tab**, you prevent the "disappearing" problem entirely.
* **Tool-to-App:** Remember that the dashboard is just a **View**; it uses the **Tools** you already have (like `move_gameobject`) to do the actual work in Unity.
