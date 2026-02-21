This **Product Specification** defines the integration layer between the Unity Dashboard (MCP App) and the AI Agent. It focuses on turning your visual Inspector into a "sensory input" for the AI, ensuring that when you move a slider, the agent "feels" the change.

---

# Product Specification: Agentic Unity Inspector Integration

## 1. Overview

This specification extends the `feature/gameobject-inspector-pane` branch. It defines the communication protocol between the **Visual UI (HTML/JS)** and the **AI Agent (VS Code Chat)** to ensure a shared state, reducing token waste and improving proactive assistance.

## 2. Core Architecture: The "Context-Push" Model

Rather than the agent polling Unity for data, the Dashboard will **push** state updates using the MCP App SDK's `updateModelContext` method.

## 3. Functional Requirements

### 3.1 Selection Synchronicity

* **Trigger:** User selects a GameObject in the Hierarchy or Inspector.
* **Action:** App calls `app.updateModelContext`.
* **Payload:** * `description`: "User is now focusing on [ObjectName]."
* `active_id`: InstanceID.
* `summary`: A high-level list of attached components (e.g., "Has Rigidbody, BoxCollider, and PlayerController").

### 3.2 Live Property Awareness (Transform & Components)

* **Trigger:** User finishes editing a property (e.g., `onBlur` for text or `onChange` for checkboxes).
* **Action:** App calls the relevant Unity tool and simultaneously updates the Agent context.
* **Benefit:** Prevents the agent from hallucinating old positions or variable values.

### 3.3 The "Agent Help" Directive

* **UI Element:** A "Ask Agent to Review" button next to complex components.
* **Action:** Fetches full `get_gameobject` JSON and pushes it to the context with a specific instruction: *"Analyze this component for common errors or optimization opportunities."*

## 4. Technical Implementation (Data Mapping)

| UI Interaction | MCP Tool Called | Agent Context Push (JSON) |
| --- | --- | --- |
| **Move Transform** | `set_transform` | `{ "lastChange": "position", "newValue": [x,y,z] }` |
| **Toggle Component** | `update_component` | `{ "component": "Light", "enabled": false }` |
| **Error (Invalid Input)** | N/A (UI Catch) | `{ "error": "Layer out of bounds", "helpRequested": true }` |

---

## 5. Visual Feedback & Theme

* **State Indicators:** * **Blue Pulse:** Syncing to Agent.
* **Green Pulse:** Unity update successful.
* **Red Pulse:** Tool failure (triggers Agent troubleshooting mode).

---

## 6. Recommended Next Step

To make this spec a reality, the most high-value logic you can implement right now is the **Selection State Listener**. This ensures that the moment you click an object in your new Inspector, the AI is immediately "primed" with that object's identity.