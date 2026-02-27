# GameObject Inspector Pane

**Branch:** `feature/gameobject-inspector-pane`
**Description:** Add an interactive inspector pane to the Unity dashboard that displays and allows editing of selected GameObject properties

## Goal
Enable users to select a GameObject in the hierarchy pane and view/edit its properties in a dedicated inspector pane (replacing the console log location in the right pane). This provides a Unity Editor-like experience within the VS Code MCP dashboard, allowing users to modify GameObject properties, transform values, and component fields without switching to Unity.

## Implementation Steps

### Step 1: Basic Inspector Pane with GameObject Properties
**Files:** Server~/src/ui/unity-dashboard.html  
**What:** Add a new inspector pane that displays when a GameObject is selected. Show read-only GameObject information (ID, path, type) and editable basic properties (name, tag, layer, activeSelf, isStatic). Fetch full GameObject details using `get_gameobject` tool when selection changes. Add input controls for name (text), tag (text), layer (number 0-31), activeSelf (checkbox), and isStatic (checkbox). Wire up change handlers to call `update_gameobject` tool with modified properties. Add visual feedback for successful/failed updates.  
**Testing:** 
- Select a GameObject in hierarchy → Inspector pane appears with correct properties
- Edit GameObject name → Name updates in both Unity Editor and hierarchy pane after refresh
- Toggle activeSelf checkbox → GameObject becomes enabled/disabled in Unity
- Change layer value → GameObject layer updates in Unity

### Step 2: Transform Editing UI
**Files:** Server~/src/ui/unity-dashboard.html  
**What:** Add a "Transform" section within the inspector pane showing position (x, y, z), rotation (x, y, z), and scale (x, y, z) values. Create number inputs for each axis with step controls (small increments like 0.1 for position/scale, 15° for rotation). Add space toggle (World/Local) for position and rotation. Implement real-time updates using `set_transform` tool (which accepts all transform properties optionally). Add "Reset" buttons for position (0,0,0), rotation (0,0,0), and scale (1,1,1). Show units in labels (position: world units, rotation: degrees).  
**Testing:**
- Select GameObject → Transform section shows current position/rotation/scale
- Edit position X value → GameObject moves along X axis in Unity Scene view
- Change rotation Y value → GameObject rotates around Y axis
- Toggle World/Local space → Verify position/rotation updates respect coordinate space
- Click Reset buttons → Transform values reset to defaults

### Step 3: Component List and Component Field Editing
**Files:** Server~/src/ui/unity-dashboard.html  
**What:** Add a "Components" section that lists all components on the selected GameObject (from `get_gameobject` response which includes components array). Display each component in an expandable/collapsible card showing component type name and serialized public fields. For each field, detect type (bool → checkbox, number → input type="number", string → text input, Vector3 → three number inputs, Color → color picker, enum → dropdown if values available). Wire field changes to call `update_component` tool with `{ componentName, componentData: { fieldName: newValue } }`. Handle special cases: Transform component (already covered in Step 2, show as read-only or hide), Camera (FOV, clearFlags, cullingMask), Light (color, intensity, range), Renderer (materials - show as list).  
**Testing:**
- Select GameObject with multiple components → All components listed with expand/collapse
- Expand BoxCollider component → Shows size, center, isTrigger fields
- Edit BoxCollider isTrigger checkbox → Collider becomes trigger in Unity
- Expand Light component → Edit intensity value → Light brightness changes in Unity Scene
- Edit Camera FOV → Field of view updates in Game/Scene view

### Step 4: Inspector Actions and Polish
**Files:** Server~/src/ui/unity-dashboard.html  
**What:** Add action buttons at top of inspector pane: "Delete GameObject", "Duplicate GameObject", "Focus in Unity" (calls `select_gameobject` to ping in Editor). Implement delete confirmation modal to prevent accidents. Add loading states for tool calls (show spinner/disable controls while updating). Improve error handling: show inline error messages when tool calls fail (e.g., invalid layer number, component field validation errors). Add keyboard shortcuts: Enter to commit focused input, Escape to cancel edit and restore original value. Implement debouncing for number inputs (wait 500ms after user stops typing before calling tool). Add visual feedback: success flash (green border), error flash (red border), pending state (yellow border). Ensure inspector pane scrolls independently from hierarchy pane.  
**Testing:**
- Click "Delete GameObject" → Confirmation modal appears → Confirm → GameObject removed from hierarchy
- Click "Duplicate GameObject" → New copy appears in hierarchy with incremented name
- Click "Focus in Unity" → GameObject highlights/pings in Unity Editor
- Rapidly type in transform position input → Tool call only fires after 500ms pause
- Enter invalid layer value (> 31) → Red error message appears, value doesn't save
- Edit multiple properties quickly → Loading states prevent race conditions
- Scroll inspector content → Hierarchy pane remains scrollable independently

## Technical Notes

### Available MCP Tools
- `get_gameobject` - Fetch complete GameObject details including components
- `update_gameobject` - Update name, tag, layer, activeSelf, isStatic
- `set_transform` - Update position, rotation, scale (all optional)
- `update_component` - Update component fields or add new component
- `select_gameobject` - Focus GameObject in Unity Editor
- `delete_gameobject` - Delete GameObject from scene
- `duplicate_gameobject` - Duplicate GameObject with optional rename/reparent

### State Management Pattern (Already Established)
```javascript
state.activeGameObject = { id, name, path };  // Set on hierarchy selection
state.lastHierarchy = [...];  // Re-render after updates to show changes
```

### Tool Call Pattern (Already Established)
```javascript
async function callTool(name, args = {}) {
  return rpcRequest('tools/call', { name, arguments: args }, TOOL_TIMEOUT_MS);
}
```

### Component Data Structure (from get_gameobject)
```json
{
  "instance_id": -123456,
  "name": "Main Camera",
  "path": "Main Camera",
  "tag": "MainCamera",
  "layer": 0,
  "active_self": true,
  "is_static": false,
  "components": [
    {
      "type": "Transform",
      "properties": {
        "position": { "x": 0, "y": 1, "z": -10 },
        "rotation": { "x": 0, "y": 0, "z": 0 },
        "localScale": { "x": 1, "y": 1, "z": 1 }
      }
    },
    {
      "type": "Camera",
      "properties": {
        "fieldOfView": 60,
        "nearClipPlane": 0.3,
        "farClipPlane": 1000
      }
    }
  ]
}
```

## Design Considerations

### Layout
- **Right panel (full height)** - Inspector pane replaces console log location; console log moved to separate view
- Show "No GameObject Selected" placeholder when `state.activeGameObject` is null
- Use collapsible sections for Transform, Components, and Actions
- Match Unity Editor visual hierarchy (header → transform → components)
- All sections expanded by default (no persistence)

### Visual Theme
- **Unity Editor dark theme** - Match Unity Inspector colors and styling
- Use Unity-style section headers (#3c3c3c background, #d2d2d2 text)
- Input field styling: #242424 background, #d2d2d2 text, #007acc focus border
- Section dividers: 1px solid #3c3c3c

### Scope
- **Component editing only** - No UI for adding new components (use agent/tools for that)
- Focus on editing existing component fields and GameObject properties
- Delegate complex operations (add component, attach scripts) to agent workflows

### Performance
- Debounce numeric inputs (500ms) to avoid excessive tool calls
- Only fetch GameObject details when selection changes (not on every refresh)
- Cache component schemas to avoid repeated metadata lookups

### User Experience
- Visual feedback for all actions (loading, success, error states)
- Preserve scroll position when refreshing hierarchy
- Maintain selection highlight when hierarchy refreshes
- Keyboard navigation support (Tab between inputs, Enter to commit)
- Clear error messages with actionable guidance

### Error Handling
- Validate input ranges before calling tools (layer 0-31, rotation -360 to 360, etc.)
- Handle tool call failures gracefully (show error, revert to previous value)
- Detect when selected GameObject is deleted → Clear inspector pane
- Handle component field type mismatches (show error if wrong type provided)
