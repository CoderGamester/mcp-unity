# Improve mcp app usage

To improve the agentic workflow, we need to transition from the agent "polling" Unity to the App "pushing" context to the agent.

---

### The Strategy: Bidirectional Context

The key to making the agent "aware" of what's happening in your UI is the **`app.updateModelContext()`** method provided by the MCP App SDK. This allows your UI to inject data directly into the agent's short-term memory without the user having to type anything.

#### 1. Implement "Push" Notifications in your HTML

In your `mcp-app.html` (the dashboard), you should trigger context updates whenever the user interacts with the UI or when Unity state changes.

```javascript
// Inside your <script> in the MCP App HTML
const app = new MCPApp();

// Function to tell the agent what we are looking at
async function onSelectObject(gameObjectId, name) {
    await app.updateModelContext({
        description: `User has selected GameObject: ${name} (${gameObjectId}) in the Unity Dashboard.`,
        activeObject: gameObjectId,
        recentAction: "selection"
    });
    console.log("Agent context updated.");
}

// Function to notify agent of a state change (e.g., entering Play Mode)
async function onPlayModeChanged(isPlaying) {
    await app.updateModelContext({
        description: `Unity is now in ${isPlaying ? "Play" : "Edit"} mode.`,
        unityState: isPlaying ? "playing" : "stopped"
    });
}

```

#### 2. Using the "Active Context" in Prompts

Once you've implemented `updateModelContext`, you no longer need to tell the agent which object you are talking about. You can simply say:

> "Add a movement script to this."

Because the App pushed the `activeObject` ID to the model context, the agent will look at that hidden metadata and know exactly which ID to pass to the `update_component` tool.

---

### Why this changes the "Agentic" Workflow

By adding these hooks, you move from a **request-response** model to a **shared-workspace** model.

| Step | Old Tool-Only Workflow | New MCP App Workflow |
| --- | --- | --- |
| **Selection** | You type: "Look at the Player object." | You click 'Player' in the App; Agent says "I see you've selected Player." |
| **Error Handling** | Agent: "Is there an error in the console?" | App pushes console error to Context; Agent: "I see the NullReferenceException, fixing it now." |
| **Discovery** | Agent runs `get_scene_info` (uses tokens). | App renders hierarchy locally (0 tokens); pushes only the *relevant* part of the tree to the Agent. |

---

### Pro-Tip: The "Focus" Button

A great way to improve your workflow is to add a **"Sync to Agent"** button in your Unity Dashboard. When clicked, it gathers the most important data from your current Unity view (current scene, selected object, and top 3 console errors) and sends one large `updateModelContext` call.

This "primes" the agent instantly, making it feel like it is truly sitting next to you.
