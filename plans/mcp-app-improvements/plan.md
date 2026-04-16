# MCP App Bidirectional Context Improvements

**Branch:** `feature/mcp-app-bidirectional-context`
**Description:** Transform Unity dashboard from polling-based to context-aware by implementing push notifications using updateModelContext()

## Goal
Enable the Unity dashboard MCP app to proactively push context to the agent, allowing natural interactions like "add a script to this" without explicitly naming GameObjects. This eliminates the need for agents to poll Unity state and creates a shared-workspace model where the app and agent maintain synchronized knowledge of the Unity editor state.

## Background
Currently, the dashboard uses a polling-based "pull" model where it fetches Unity state every 1-10 seconds. The MCP App SDK provides `updateModelContext()` API that allows pushing context directly to the agent's short-term memory. This plan implements Phase 1 (dashboard-side improvements) that require no Unity C# changes.

## Implementation Steps

### Step 1: Initialize MCP App SDK and Context Infrastructure
**Files:** 
- `Editor/Resources/GetUnityDashboardResource.cs` (read-only for reference)
- Dashboard HTML embedded in GetUnityDashboardResource

**What:** Add proper MCPApp SDK initialization and create helper functions for context management. Replace manual JSON-RPC implementation with SDK methods where appropriate. Add global state tracking for active selection, play mode, and recent errors.

**Details:**
- Import proper SDK types from `@modelcontextprotocol/ext-apps`
- Initialize `const app = new MCPApp()` early in script lifecycle
- Create `updateAgentContext(description, metadata)` helper function
- Add state variables: `activeGameObject`, `currentPlayMode`, `recentErrors`, `lastSceneNames`, `lastCompilationState`, `lastInspectorFocus`
- Track scene changes for pushing context updates
- Track compilation state for error notifications
- Track Inspector focus for context awareness

**Testing:** Open dashboard, check browser console for SDK initialization messages, verify no errors in manual RPC calls

---

### Step 2: Implement GameObject Selection and Context Pushing
**Files:**
- Dashboard HTML (hierarchy rendering section)

**What:** Make hierarchy items clickable and push selection context to the agent when users click GameObjects. Store selected object ID/path in state and highlight the selected item visually.

**Details:**
- Add click handlers to hierarchy list items (`<li>` elements in `renderHierarchy()`)
- Style selected item with CSS highlight (background color change)
- On click: call `app.updateModelContext()` with:
  ```javascript
  {
    description: `User selected GameObject "${name}" (ID: ${instanceId}) in Unity Dashboard.`,
    activeGameObject: { id: instanceId, name: name, path: fullPath },
    recentAction: "selection"
  }
  ```
- Store selection in global `activeGameObject` state
- Keep object selected on repeated clicks (no toggle/deselect behavior - repeated clicks maintain selection)
- Auto-sync full context on dashboard first open

**Testing:** 
1. Open dashboard with active Unity scene
2. Click various GameObjects in hierarchy
3. Ask agent: "What am I looking at?" or "Add a Rigidbody to this"
4. Verify agent references the selected object without prompting

---

### Step 3: Implement Play Mode State Tracking
**Files:**
- Dashboard HTML (play mode status section)

**What:** Push context updates when Unity enters/exits play mode. Since dashboard already polls play mode status, intercept status changes and push to agent.

**Details:**
- Track previous play mode state in global variable `lastPlayModeState`
- On each `updatePlayMode()` call, compare new state with previous
- If changed, call `app.updateModelContext()` with:
  ```javascript
  {
    description: `Unity ${isPlaying ? "entered Play mode" : "exited Play mode"}.`,
    unityState: { 
      isPlaying: isPlaying, 
      isPaused: isPaused,
      timestamp: Date.now()
    }
  }
  ```
- Update visual indicator (green/gray status badge)

**Testing:**
1. Open dashboard with Unity editor visible
2. Enter play mode in Unity
3. Verify agent receives context update (check logs or ask agent "Is Unity playing?")
4. Exit play mode and verify state change is pushed

---

### Step 4: Implement Console Error Tracking and Context Pushing
**Files:**
- Dashboard HTML (console logs section)

**What:** Push context updates when new errors or warnings appear in Unity console. Track error count and push only new errors to avoid spam.

**Details:**
- Track previous error count in global `lastErrorCount`
- On each `updateLogs()` call, compare current error count with previous
- If new errors detected, push top 3 most recent errors:
  ```javascript
  {
    description: `${newErrorCount} new error(s) in Unity Console: ${errors.map(e => e.message).join('; ')}`,
    consoleErrors: errors.slice(0, 3).map(e => ({
      message: e.message,
      type: e.type,
      timestamp: e.timestamp
    })),
    recentAction: "console-error"
  }
  ```
- Push both warnings and errors (filter by severity)
- Group by severity: errors first, then warnings
- Display visual indicators (red for errors, orange for warnings)

**Testing:**
1. Open dashboard
2. Trigger an error in Unity (e.g., broken script compilation)
3. Verify agent receives error context
4. Ask agent: "What's wrong in Unity?"
5. Verify agent references the specific error without querying

---

### Step 5: Add "Sync to Agent" Button
**Files:**
- Dashboard HTML (add new UI section)

**What:** Add a prominent button that gathers comprehensive Unity state and pushes it to agent all at once. This "primes" the agent with full context on demand.

**Details:**
- Add button in header: `<button id="syncButton">🔄 Sync to Agent</button>`
- On click, gather:
  - Current scene name and loaded scene count
  - Active GameObject (if any)
  - Total GameObject count in scene
  - Top 3 console errors/warnings
  - Play mode state
  - Last update timestamp
- Push comprehensive context:
  ```javascript
  {
    description: `Unity Dashboard synchronized. Scene: ${sceneName}, ${gameObjectCount} objects, ${errorCount} errors.`,
    sceneInfo: { name, loadedScenes, rootObjectCount },
    activeGameObject: activeGameObject || null,
    playMode: { isPlaying, isPaused },
    topErrors: errors.slice(0, 3),
    syncTimestamp: Date.now()
  }
  ```
- Automatically trigger sync on dashboard first open (call sync function in `init()`)
- Allow manual re-sync via button for subsequent state updates

**Testing:**
1. Open dashboard with complex scene
2. Click "Sync to Agent" button
3. Ask agent: "What's the current Unity state?" or "Summarize my scene"
4. Verify agent has complete awareness of scene, errors, play mode
5. Test with empty scene and verify graceful handling

---
# Step 6: Implement Scene Change Notifications
**Files:**
- Dashboard HTML (hierarchy update section)

**What:** Detect when scenes are loaded/unloaded and push context updates to agent. Track loaded scene names and push changes when they differ.

**Details:**
- Store previous scene names in `lastSceneNames` array
- On each hierarchy update, extract loaded scene names
- Compare with previous state; if different, push context:
  ```javascript
  {
    description: `Unity scene changed. Now loaded: ${sceneNames.join(', ')}`,
    sceneChange: {
      loaded: sceneNames,
      added: newScenes,
      removed: removedScenes
    }
  }
  ```
- Update scene display in dashboard header AND warnings
- ✅ Dashboard auto-syncs context on first open
- ✅ "Sync to Agent" button available for manual re-sync
- ✅ Agent notified of scene changes (load/unload)
- ✅ Agent aware of compilation status and errors
- ✅ Agent knows Inspector focus for targeted modifications
- ✅ Natural commands work: "Add Rigidbody to this", "Fix that error", "Modify this property"
- ✅ Minimal Unity C# changes (may need Inspector state resource for Step 8)

---

## Implementation Notes

**Step 8 Dependency:** Inspector focus tracking may require a new Unity resource (`GetInspectorStateResource`) to expose:
- Currently selected GameObject in Unity (not just dashboard)
- Active Inspector component focus
- This would be a small C# addition but provides high-value context

**Alternative for Step 8:** If Unity C# changes are not desired, skip Inspector focus tracking in Phase 1 and defer to Phase 3 with Unity-initiated push events.

**Decisions Made:**
- Console logs: Push both errors AND warnings (grouped by severity)
- Auto-sync: Automatic on dashboard first open, manual button for re-sync
- Scene events: Track and push scene load/unload notifications (Step 6)
- Selection behavior: No toggle - clicking maintains selection state
**Details:**
- Parse console logs for compilation markers (errors starting with script names, "CompilerError")
- Track compilation state: `idle`, `compiling`, `success`, `failed`
- On state change, push context:
  ```javascript
  {
    description: `Unity compilation ${state}. ${errorCount > 0 ? errorCount + ' errors' : 'No errors'}`,
    compilation: {
      state: state,
      errorCount: errorCount,
      timestamp: Date.now()
    }
  }
  ```
- Show compilation status indicator (animated spinner during compilation)

**Testing:**
1. Open dashboard
2. Introduce syntax error in C# script
3. Verify agent receives compilation failure notification
4. Fix error and save
5. Verify agent receives compilation success notification
6. Ask agent: "Did my code compile?"

---

### Step 8: Implement Inspector Focus Context
**Files:**
- Dashboard HTML (add inspector focus tracking)
- May require new Unity tool/resource for Inspector state

**What:** Push context when user focuses on specific components in Unity Inspector. This helps agent understand what the user is examining or modifying.

**Details:**
- Add new polling call to check Unity Inspector state (may need new Unity resource)
- Track last focused component type and GameObject
- On focus change, push context:
  ```javascript
  {
    description: `User is inspecting ${componentType} on ${gameObjectName}`,
    inspectorFocus: {
      gameObject: { id, name, path },
      component: componentType,
      timestamp: Date.now()
    }
  }
  ```
- Highlight focused GameObject in dashboard hierarchy

**Testing:**
1. Open dashboard and Unity editor side-by-side
2. Select GameObject in Unity hierarchy
3. Click component in Inspector (e.g., Transform)
4. Verify dashboard highlights GameObject
5. Ask agent: "Modify this component's properties"
6. Verify agent knows which component is in focus

---

## Out of Scope (Future Phases)

**Phase 2 - Enhanced Context:**
- Push component details when GameObject selected
- Build settings state
- Package manager state changes
- Asset import progres
- Package manager state changes

**Phase 3 - Unity-Initiated Push:**
- Unity C# event hooks (`Selection.selectionChanged`, `EditorApplication.playModeStateChanged`)
- WebSocket push notifications from Unity to dashboard
- Real-time bidirectional communication without polling

---

## Success Criteria

After implementing this plan:
- ✅ User can click GameObjects in dashboard and agent knows selection
- ✅ Agent aware of play mode changes without explicit queries
- ✅ Agent automatically notified of new console errors
- ✅ "Sync to Agent" button provides instant comprehensive context
- ✅ Natural commands work: "Add Rigidbody to this", "Fix that error"
- ✅ Zero Unity C# code changes required (dashboard HTML only, except Step 8 if implemented)
