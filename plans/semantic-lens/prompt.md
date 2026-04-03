## Revised Product Specification: Agentic Context Bridge (The "Sentinel" Approach)

**Goal:** Transform the VS Code Dashboard from a "Control Panel" into a **"Semantic Lens"** that focuses the Agent’s attention on specific scene data without flooding the context window.

### 1. The "Observer" Hierarchy

Instead of just a list of names, the Hierarchy in the MCP App should allow you to **"Flag for Agent."**

* **Feature:** A "Focus" icon next to GameObjects.
* **Logic:** When clicked, it doesn't just select the object; it sends a "Deep Trace" to the agent (Components, Tags, Layers, and Children).
* **Why:** This tells the agent, "This is the 'Main Character' of our current conversation. Ignore the rest of the 500 objects in the scene."

### 2. Semantic Component Summaries (The "Insight" Pane)

Instead of editable fields for every variable (Step 3 in your original plan), replace them with **Component Summaries**.

* **Visual:** A list of components with a "What is this doing?" button.
* **Logic:** When you click a component (e.g., `NavMeshAgent`), the UI sends the specific values to the agent and asks: *"Based on these NavMesh settings, why might the character be stuck?"*
* **Why:** You aren't changing a float; you are asking for a diagnosis based on a float.

### 3. "Unity-to-Agent" Event Stream

Instead of a static log, create a **Contextual Event Feed**.

* **Feature:** A "Push to Chat" button on console errors.
* **Logic:** Clicking an error in the Dashboard doesn't just show the stack trace; it packages the stack trace + the offending GameObject's current state and sends it as a "High Priority" context update.

---

## Comparison: Utility vs. Agency

| Feature | The "Manual" Approach (Manual Labor) | The "Agentic" Approach (Collaborative) |
| --- | --- | --- |
| **Transform** | You type `10.5` into an input box. | You click **"Describe Position"**; Agent says: "The Player is currently inside the wall collider." |
| **Components** | You check a `isTrigger` box. | You click **"Check Logic"**; Agent says: "This trigger has no associated script. Should I write an `OnTrigger` handler?" |
| **Hierarchy** | You scroll through 100 items. | You use a **"Filter for Errors"** toggle; the UI only shows objects with missing references for the Agent to fix. |

---

## 4. The "Agentic" Recommended Next Step

To align with your goal of **improving understanding rather than manual control**, change your implementation Step 1 from "Editable Fields" to a **"Contextual Snapshot"** system.

### Your New "Step 1": The "Point and Explain" Logic

Instead of building input boxes for Name, Tag, and Layer, build a **"Tell Agent About This"** button.

1. **UI:** User selects a GameObject in the Hierarchy.
2. **Logic:** The App automatically calls `get_gameobject`.
3. **Agentic Push:** The App calls `app.updateModelContext` with a "Semantic Narrative":
> *"I am now looking at the 'Player' object. It has a 'CharacterController' but the 'Gravity' value is set to 0. It is currently positioned at (0, -10, 0), which is below the floor."*


4. **Result:** The Agent immediately responds in the chat (without you typing): *"I see the Player is below the floor with no gravity. Would you like me to reset the position and apply a standard gravity value?"*