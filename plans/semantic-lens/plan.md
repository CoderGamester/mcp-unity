# Semantic Lens (Agentic Context Bridge)

**Branch:** `feature/semantic-lens-context-bridge`
**Description:** Transform the Unity Dashboard into a "Semantic Lens" that focuses agent attention on specific GameObjects through focus indicators, filtering, and semantic analysis features.

## Goal
Convert the Unity Dashboard from showing all scene data into an intelligent "Observer" that lets users flag specific GameObjects for deep agent analysis. Instead of manually editing properties, users will flag objects for focus, request semantic summaries, and push high-priority errors directly to the agent's context - reducing token usage and improving agent relevance.

## Context
**Research Finding:** The dashboard already implements `updateAgentContext()` and pushes context for selections, scene changes, errors, and play mode changes ([Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html#L274-287), L618-625). This plan extends that foundation with focus management, filtering, and semantic analysis features.

## Implementation Steps

### Step 1: Focus/Flag Infrastructure
**Files:** 
- Server~/src/ui/unity-dashboard.html (state management + UI indicators)

**What:** 
Add the ability to flag GameObjects as "focused" for agent attention. Implement:
- State management: `state.focusedObjects = new Set()` to track focused GameObject IDs
- Visual indicators: Add `*` indicator buttons next to GameObjects in hierarchy
- Toggle logic: Click to add/remove from focus set with visual feedback
- Persistence: Store focus state in hierarchy rendering

**Testing:** 
- Click `*` indicator next to a GameObject → changes color and ID added to `state.focusedObjects`
- Click again → indicator changes back and ID removed from set
- Multiple objects can be focused simultaneously

---

### Step 2: Hierarchy Filtering ("Observer Mode")
**Files:**
- Server~/src/ui/unity-dashboard.html (hierarchy rendering logic)

**What:**
Add a "Show Only Focused" toggle above the hierarchy view. When enabled:
- Filter hierarchy to display only focused GameObjects (and their parents for context)
- Preserve tree structure (show parent chain even if parent not focused)
- Display count: "Showing 3 of 847 objects"
- Allow switching between full/filtered view

**Testing:**
- Focus 3 objects → enable filter → only those 3 (+ parent chain) visible
- Toggle filter off → full hierarchy restored
- Counter displays correct focused object count

---

### Step 3: Enhanced Context Updates (Deep Trace)
**Files:**
- Server~/src/ui/unity-dashboard.html (context update logic)

**What:**
When GameObjects are focused, automatically push enhanced context to the agent:
- Fetch detailed data for all focused objects in parallel requests
- Include: full component details, transform data, direct children, parent relationships
- Update agent context with semantic narrative:
  - "User is focusing on 3 objects: Player (CharacterController at position 0,0,0), Enemy (NavMeshAgent stuck), Camera (following Player)"
- **Timing:** Auto-refresh during normal polling cycles (every 3 seconds when focused objects exist)
- **Data depth:** Include focused object + direct children only (not recursive to save tokens)
- **Throttling:** Only push updates when focused set changes or GameObject data changes (detect via dirty state)

**Testing:**
- Focus a GameObject with CharacterController → agent context includes component properties
- Check agent can reference focused object details in conversation
- Parallel requests complete in <1 second for 5 focused objects

---

### Step 4: Component Analysis ("Point and Explain")
**Files:**
- Server~/src/ui/unity-dashboard.html (inspector detail view)

**What:**
Add semantic analysis mode for focused GameObjects:
- When a focused GameObject is selected, show detailed component list
- Add "🔍 Analyze Component" button next to each component
- On click: push component data to agent with diagnostic prompt
  - Example: "This NavMeshAgent has speed=0 and angular speed=0. Why might the character be stuck?"
- **Display strategy:** Show analysis both in-dashboard (below component) AND push to chat for conversation context
- **Analysis scope:** Show analyze button for all components (including Transform) to allow full inspection
- **Prompt style:** Use generic "analyze this component" with full property data; let agent determine issues

**Testing:**
- Focus GameObject with NavMeshAgent → click "Analyze" → agent receives component properties + diagnostic prompt
- Agent responds with analysis (e.g., "Speed is set to 0, preventing movement")
- Works for various component types (Rigidbody, Collider, custom scripts)

---

### Step 5: Console Error Push-to-Chat
**Files:**
- Server~/src/ui/unity-dashboard.html (console log UI)

**What:**
Implement automatic and manual error pushing to agent context:
- **Auto-push behavior:** All new errors/warnings automatically pushed to agent when they occur (without stack traces to minimize tokens)
- Add "Push to Agent" button next to errors for manual re-push with full details
- **Manual push behavior:** When user clicks "Push to Agent", fetch full log with stack trace on-demand
- Extract GameObject reference from stack trace (if present during manual push)
- Fetch affected GameObject's current state via `get_gameobject` (during manual push)
- Package error + GameObject context + stack trace into high-priority context update
- Push to agent with urgent framing: "Critical error detected: NullReferenceException in PlayerController.Update() affecting GameObject 'Player' at position (0, -10, 0)"
- **Stack trace strategy:** Auto-push includes only error message/type; manual push fetches and includes stack trace for deeper analysis
- **No GameObject case:** Push scene context + active GameObject if stack trace doesn't reference specific object

**Testing:**
- Trigger a NullReferenceException in Unity → appears in dashboard logs and auto-pushes summary to agent
- Click "Push to Agent" → agent receives full error message + stack trace + offending GameObject state
- Agent can reference error details and suggest fixes
- Errors without GameObject references still push successfully (e.g., compilation errors)
- Auto-push doesn't include stack traces (lower token usage); manual push includes everything

---

## Success Criteria
- ✅ Users can flag multiple GameObjects as "focused" with visual indicators
- ✅ Hierarchy can filter to show only focused objects
- ✅ Agent receives enhanced context with full component data for focused objects
- ✅ Component analysis buttons trigger agent diagnostic responses
- ✅ Console errors can be manually pushed to agent with GameObject context
- ✅ Token usage reduced: agent sees only relevant objects, not all 500+ in scene

## Technical Constraints
- **Polling-based architecture:** Unity cannot push notifications; dashboard must poll for changes
- **Parallel requests:** Fetch focused object details in parallel using Promise.all for efficiency
- **Context is transient:** Agent forgets focus when conversation cleared (acceptable)
- **Main thread execution:** Unity tool calls block editor momentarily (keep requests minimal)
- **Fork constraint:** Cannot modify existing MCP tools; work within existing tool contracts

## Future Enhancements (Out of Scope)
- Persist focus set across sessions (save to Unity EditorPrefs)
- Visual gizmos in Unity Scene view for focused objects
- AI-suggested focus targets based on errors/warnings
- "Focus Presets" (e.g., "All Characters", "All Cameras")
