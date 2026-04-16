# Unity Dashboard Popout Button Support

**Branch:** `fix-popout-button`
**Description:** Fix missing "Open in Editor" popout button in Unity Dashboard MCP App

## Goal
Fix the Unity Dashboard so that the "Open in Editor" popout button appears in VS Code 1.109.0. The infrastructure is already implemented using the modern MCP Apps SDK (`@modelcontextprotocol/ext-apps` v1.0.0), but the popout button is not showing. This requires adding the `_meta.ui: true` flag to the tool result.

## Current Status

**Environment:**
- VS Code version: 1.109.0 (confirmed MCP Apps compatible)
- Issue: Popout button not appearing in dashboard panel

**Already Implemented:**
- Tool uses `registerAppTool` from `@modelcontextprotocol/ext-apps/server`
- Resource uses `registerAppResource` with `ui://unity-dashboard` scheme
- Tool has `_meta.ui.resourceUri` pointing to the app resource
- Resource has `_meta.view: 'mcp-app'` and `_meta.ui` with CSP settings
- Dashboard HTML has full JSON-RPC communication with host
- Auto-refresh functionality for logs, hierarchy, and scene info
- Agent context syncing via `ui/update-model-context`
- Inspector focus tracking

**Root Cause:**
VS Code 1.109.0 requires a `_meta.ui: true` flag at the tool result level (not just in the resource metadata) to display the popout button. This flag is currently missing from the `toolHandler()` return value in `showUnityDashboardTool.ts`.

## Implementation Steps

### Step 1: Add Missing Metadata Flag
**Files:** 
- `Server~/src/tools/showUnityDashboardTool.ts` (line ~37-65)

**What:** 
Add `_meta: { ui: true }` to the `CallToolResult` returned by `toolHandler()`. This tells VS Code that the tool result contains an MCP App that should display a popout button.

**Change:**
```typescript
return {
  content: [...],
  _meta: {
    ui: true  // Add this for VS Code 1.109+ popout button support
  }
};
```

**Testing:**
1. Make the code change
2. Run `npm run build` in `Server~/` directory
3. Restart the MCP server in VS Code
4. Ask Copilot: "show the Unity dashboard"
5. Verify "Open in Editor" icon (square with arrow) appears in panel header
6. Click icon and confirm dashboard opens in new editor tab

### Step 2: Comprehensive Testing
**Files:** All Unity Dashboard features

**What:** 
After the popout button appears, verify all dashboard features work correctly in both inline and popout modes:

1. **Hierarchy Display:** Scene hierarchy renders with proper indentation
2. **Logs Display:** Console logs show with correct color coding (info/warn/error)
3. **Scene Info:** Active scene name and root count display
4. **Auto-Refresh:** Toggle auto-refresh and verify it polls Unity at the specified interval
5. **Play Mode Status:** Play/edit mode badge displays correctly
6. **GameObject Selection:** Click GameObjects in hierarchy and verify selection highlighting
7. **Agent Context Sync:** Click "Sync" button and verify agent receives context
8. **Split View Persistence:** Drag dashboard to right edge and verify it stays visible
9. **Compilation Status:** Trigger script compilation and verify status badge updates
10. **Inspector Focus:** Select GameObject in Unity Inspector and verify dashboard highlights it

**Testing:**
- Test each feature in **inline mode** (dashboard in chat panel)
- Then click "Open in Editor" and test all features in **popout mode**
- Verify no console errors in browser DevTools (F12)
- Check that auto-refresh continues when dashboard is in background tab
- Verify agent receives context updates from both modes

## Notes

- The fix is minimal: just add one metadata flag
- VS Code 1.109.0 is confirmed compatible with MCP Apps
- All backend infrastructure is already in place
- The prompt.md file was an early draft; actual implementation is more sophisticated
- The `ui://` scheme is already being used correctly
- Resource-level metadata is also correct; just need tool-level flag

## Success Criteria

- [x] Root cause identified: missing `_meta.ui: true` flag
- [ ] Code change made to add the flag
- [ ] Server rebuilt successfully
- [ ] Dashboard displays when tool is called
- [ ] "Open in Editor" icon appears in dashboard panel header
- [ ] Clicking icon opens dashboard in new editor tab
- [ ] All features work in inline mode
- [ ] All features work in popout mode
- [ ] Dashboard persists when pinned to side
- [ ] No console errors or warnings

## Risk Assessment

**Low Risk:**
- Change is minimal (adding one metadata property)
- No breaking changes to existing functionality
- Backwards compatible with older VS Code versions
- Resource-level metadata remains unchanged

**Testing Coverage:**
- Unit tests not required (metadata flag only)
- Manual testing sufficient for UI change
- Can roll back by removing the flag if issues arise

## Future Enhancements (Optional)

If testing reveals additional improvements:
1. Add user preference to auto-open dashboard in popout mode
2. Add keyboard shortcuts for dashboard actions (F5 refresh, etc.)
3. Improve log filtering (search box, type filter)
4. Add visual progress indicators for long-running operations
5. Add "collapse all" / "expand all" buttons for hierarchy tree
6. Persist dashboard state (selected GameObject, auto-refresh settings) across sessions

