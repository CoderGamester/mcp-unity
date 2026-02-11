# Semantic Lens

## Goal
Implement a Semantic Lens on the Unity Dashboard that lets users focus GameObjects, filter the hierarchy, send focused context to the agent, analyze components, and push console errors with GameObject context.

## Prerequisites
Make sure that the use is currently on the `feature/semantic-lens-context-bridge` branch before beginning implementation.
If not, move them to the correct branch. If the branch does not exist, create it from main.

### Step-by-Step Instructions

#### Step 1: Focus/Flag Infrastructure
- [x] Add the CSS rules below at the end of the existing `<style>` block in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html):

```css
      .node.flagged{outline:1px solid #ffd580;background:#14283e}
      .node-row{display:flex;align-items:center;gap:6px}
      .focus-btn{background:#0c1b2a;border:1px solid #1f3750;color:#5f6f84;padding:2px 6px;border-radius:6px;font-size:12px;min-width:20px;height:20px;line-height:1}
      .focus-btn:hover{border-color:#4fd1c5;color:#e6eef8}
      .focus-btn.focused{color:#ffd580;border-color:#5f4b1c;background:rgba(255,213,128,0.12)}
```

- [x] Replace the `state` declaration in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html) with the block below:

```javascript
      const state = {
        appReady: false,
        activeGameObject: null,
        currentPlayMode: null,
        recentErrors: [],
        lastSceneNames: [],
        lastCompilationState: 'idle',
        lastInspectorFocus: null,
        lastPlayModeState: null,
        lastErrorCount: 0,
        lastSceneInfo: null,
        lastHierarchy: null,
        lastLogs: null,
        lastPlayMode: null,
        focusedObjects: new Set()
      };
```

- [x] Add the focus helpers below immediately after `handleSelection()` in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html):

```javascript
      function isFocused(id) {
        return state.focusedObjects.has(id);
      }

      function toggleFocus(entry) {
        if (!entry || entry.id === undefined || entry.id === null) return;
        if (state.focusedObjects.has(entry.id)) {
          state.focusedObjects.delete(entry.id);
        } else {
          state.focusedObjects.add(entry.id);
        }
        renderHierarchy(state.lastHierarchy || []);
      }
```

- [x] Replace `renderGameObjectNode()` in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html) with the block below:

```javascript
      function renderGameObjectNode(node, depth, parentPath, container) {
        const name = node.name || 'GameObject';
        const currentPath = parentPath ? `${parentPath}/${name}` : name;
        const entry = {
          id: node.instanceId,
          name,
          path: currentPath
        };

        const element = document.createElement('div');
        element.className = 'node object';
        element.style.paddingLeft = `${12 + depth * 14}px`;

        const row = document.createElement('div');
        row.className = 'node-row';

        const focusBtn = document.createElement('button');
        focusBtn.className = 'focus-btn';
        focusBtn.textContent = '*';
        focusBtn.title = isFocused(entry.id) ? 'Unfocus' : 'Focus';
        if (isFocused(entry.id)) {
          focusBtn.classList.add('focused');
          element.classList.add('flagged');
        }

        focusBtn.addEventListener('click', (event) => {
          event.stopPropagation();
          toggleFocus(entry);
        });

        const label = document.createElement('span');
        label.textContent = name;

        row.appendChild(focusBtn);
        row.appendChild(label);
        element.appendChild(row);

        if (state.activeGameObject && state.activeGameObject.id === entry.id) {
          element.classList.add('selected');
        }

        if (state.lastInspectorFocus && state.lastInspectorFocus.id === entry.id) {
          element.classList.add('focused');
        }

        element.addEventListener('click', (event) => {
          event.stopPropagation();
          handleSelection(entry, element);
        });

        container.appendChild(element);

        const children = node.children || [];
        children.forEach(child => renderGameObjectNode(child, depth + 1, currentPath, container));
      }
```

##### Step 1 Verification Checklist
- [x] No build errors
- [ ] Clicking the `*` button toggles focus styling on a GameObject node

#### Step 1 STOP & COMMIT
**STOP & COMMIT:** Agent must stop here and wait for the user to test, stage, and commit the change.

#### Step 2: Hierarchy Filtering (Observer Mode)
- [x] Replace the Hierarchy card markup with the block below in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html):

```html
          <div class="card" style="margin-top:12px">
            <div class="section-title">Hierarchy</div>
            <div class="hierarchy-toolbar">
              <label class="meta"><input id="focusFilter" type="checkbox" /> Show only focused</label>
              <span id="focusCount" class="meta"></span>
            </div>
            <div id="hierarchy">Loading hierarchy...</div>
          </div>
```

- [x] Add the CSS rule below at the end of the existing `<style>` block in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html):

```css
      .hierarchy-toolbar{display:flex;align-items:center;justify-content:space-between;margin-bottom:8px}
```

- [x] Add the DOM references below alongside the other element lookups in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html):

```javascript
      const focusFilter = document.getElementById('focusFilter');
      const focusCount = document.getElementById('focusCount');
```

- [x] Replace the `state` declaration in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html) with the block below:

```javascript
      const state = {
        appReady: false,
        activeGameObject: null,
        currentPlayMode: null,
        recentErrors: [],
        lastSceneNames: [],
        lastCompilationState: 'idle',
        lastInspectorFocus: null,
        lastPlayModeState: null,
        lastErrorCount: 0,
        lastSceneInfo: null,
        lastHierarchy: null,
        lastLogs: null,
        lastPlayMode: null,
        focusedObjects: new Set(),
        focusFilterEnabled: false
      };
```

- [x] Add the filtering helpers below immediately after `countGameObjects()` in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html):

```javascript
      function collectHierarchyIds(hierarchyList) {
        const ids = new Set();

        function visit(node) {
          if (!node) return;
          ids.add(node.instanceId);
          const children = node.children || [];
          children.forEach(child => visit(child));
        }

        if (Array.isArray(hierarchyList)) {
          hierarchyList.forEach(scene => {
            const roots = scene.rootObjects || [];
            roots.forEach(root => visit(root));
          });
        }

        return ids;
      }

      function pruneFocusedObjects(hierarchyList) {
        const ids = collectHierarchyIds(hierarchyList);
        let removed = false;
        for (const id of Array.from(state.focusedObjects)) {
          if (!ids.has(id)) {
            state.focusedObjects.delete(id);
            removed = true;
          }
        }
        return removed;
      }

      function filterNodeForFocus(node, focusedSet) {
        const children = node.children || [];
        const filteredChildren = children.map(child => filterNodeForFocus(child, focusedSet)).filter(Boolean);
        if (focusedSet.has(node.instanceId) || filteredChildren.length > 0) {
          return { ...node, children: filteredChildren };
        }
        return null;
      }

      function filterHierarchyForFocus(list, focusedSet) {
        if (!focusedSet || focusedSet.size === 0) return list;
        const filtered = [];
        list.forEach(scene => {
          const roots = scene.rootObjects || [];
          const filteredRoots = roots.map(root => filterNodeForFocus(root, focusedSet)).filter(Boolean);
          if (filteredRoots.length > 0) {
            filtered.push({ ...scene, rootObjects: filteredRoots });
          }
        });
        return filtered;
      }

      function getVisibleHierarchy(list) {
        if (state.focusFilterEnabled && state.focusedObjects.size > 0) {
          return filterHierarchyForFocus(list, state.focusedObjects);
        }
        return list;
      }

      function updateFocusCounter(totalCount, visibleCount) {
        if (!focusCount) return;
        if (state.focusFilterEnabled && state.focusedObjects.size > 0) {
          focusCount.textContent = `Showing ${visibleCount} of ${totalCount} objects`;
        } else {
          focusCount.textContent = `Showing ${totalCount} objects`;
        }
        if (state.focusedObjects.size > 0) {
          focusCount.textContent += ` | Focused ${state.focusedObjects.size}`;
        }
      }
```

- [x] Add the filter toggle handler below after the `interval.onchange = scheduleAuto;` line in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html):

```javascript
      focusFilter.onchange = () => {
        state.focusFilterEnabled = focusFilter.checked;
        renderHierarchy(state.lastHierarchy || []);
      };
```

- [x] Replace `renderHierarchy()` in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html) with the block below:

```javascript
      function renderHierarchy(list) {
        if (!list || list.length === 0) {
          hierarchy.textContent = 'No scene data';
          updateFocusCounter(0, 0);
          return;
        }

        pruneFocusedObjects(list);
        const totalCount = countGameObjects(list);
        const visibleList = getVisibleHierarchy(list);
        const visibleCount = countGameObjects(visibleList);

        if (state.focusFilterEnabled && state.focusedObjects.size === 0) {
          hierarchy.textContent = 'No focused objects';
          updateFocusCounter(totalCount, 0);
          return;
        }

        hierarchy.innerHTML = '';
        const container = document.createElement('div');

        visibleList.forEach(scene => {
          const sceneNode = document.createElement('div');
          sceneNode.className = 'node scene';
          sceneNode.textContent = scene.name || 'Scene';
          container.appendChild(sceneNode);

          const rootObjects = scene.rootObjects || [];
          rootObjects.forEach(root => renderGameObjectNode(root, 1, '', container));
        });

        hierarchy.appendChild(container);
        detectSceneChanges(list.map(scene => scene.name).filter(Boolean));
        updateFocusCounter(totalCount, visibleCount);
      }
```

##### Step 2 Verification Checklist
- [x] No build errors
- [ ] Toggling "Show only focused" filters the hierarchy and updates the counter

#### Step 2 STOP & COMMIT
**STOP & COMMIT:** Agent must stop here and wait for the user to test, stage, and commit the change.

#### Step 3: Enhanced Context Updates (Deep Trace)
- [x] Note: do not modify or replace any existing MCP tools. This step only updates the dashboard UI logic and uses existing tools as-is.
- [x] Replace the `state` declaration in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html) with the block below:

```javascript
      const state = {
        appReady: false,
        activeGameObject: null,
        currentPlayMode: null,
        recentErrors: [],
        lastSceneNames: [],
        lastCompilationState: 'idle',
        lastInspectorFocus: null,
        lastPlayModeState: null,
        lastErrorCount: 0,
        lastSceneInfo: null,
        lastHierarchy: null,
        lastLogs: null,
        lastPlayMode: null,
        focusedObjects: new Set(),
        focusFilterEnabled: false,
        focusedDetails: new Map(),
        focusRefreshInFlight: false,
        lastFocusIdsKey: '',
        lastFocusSignature: '',
        hierarchyIndex: new Map()
      };
```

- [x] Add the focus context helpers below after the focus-filter helpers from Step 2 (directly below `updateFocusCounter()`) in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html):

```javascript
      function buildHierarchyIndex(hierarchyList) {
        const index = new Map();

        function visit(node, parentPath, parentId, parentName) {
          if (!node) return;
          const name = node.name || 'GameObject';
          const currentPath = parentPath ? `${parentPath}/${name}` : name;
          index.set(node.instanceId, { path: currentPath, parentId, parentName, name });
          const children = node.children || [];
          children.forEach(child => visit(child, currentPath, node.instanceId, name));
        }

        if (Array.isArray(hierarchyList)) {
          hierarchyList.forEach(scene => {
            const roots = scene.rootObjects || [];
            roots.forEach(root => visit(root, '', null, null));
          });
        }

        return index;
      }

      function formatNumber(value) {
        const num = Number(value);
        if (!Number.isFinite(num)) return String(value);
        return num.toFixed(2);
      }

      function formatVector(value) {
        if (!value || typeof value !== 'object') return null;
        const x = value.x ?? value.X;
        const y = value.y ?? value.Y;
        const z = value.z ?? value.Z;
        if (x === undefined || y === undefined || z === undefined) return null;
        return `(${formatNumber(x)}, ${formatNumber(y)}, ${formatNumber(z)})`;
      }

      function getTransformPosition(components) {
        if (!Array.isArray(components)) return null;
        const transform = components.find(component => component && component.type === 'Transform');
        if (!transform || !transform.properties) return null;
        const props = transform.properties;
        const position = props.position || props.localPosition || null;
        return formatVector(position);
      }

      function trimGameObjectForContext(gameObject) {
        if (!gameObject) return null;
        const children = Array.isArray(gameObject.children) ? gameObject.children : [];
        const directChildren = children.map(child => ({
          name: child.name,
          instanceId: child.instanceId,
          activeSelf: child.activeSelf,
          activeInHierarchy: child.activeInHierarchy
        }));
        return {
          name: gameObject.name,
          instanceId: gameObject.instanceId,
          activeSelf: gameObject.activeSelf,
          activeInHierarchy: gameObject.activeInHierarchy,
          tag: gameObject.tag,
          layer: gameObject.layer,
          layerName: gameObject.layerName,
          components: Array.isArray(gameObject.components) ? gameObject.components : [],
          children: directChildren
        };
      }

      function buildFocusSummary(details) {
        return details.map(detail => {
          const components = Array.isArray(detail.components) ? detail.components : [];
          const componentTypes = components.map(component => component.type).filter(Boolean);
          const nonTransform = componentTypes.filter(type => type !== 'Transform');
          const position = getTransformPosition(components);
          const indexEntry = state.hierarchyIndex && state.hierarchyIndex.get(detail.instanceId);
          const parentName = indexEntry ? indexEntry.parentName : null;
          const path = indexEntry ? indexEntry.path : null;
          const children = Array.isArray(detail.children) ? detail.children.map(child => ({
            name: child.name,
            instanceId: child.instanceId,
            activeSelf: child.activeSelf,
            activeInHierarchy: child.activeInHierarchy
          })) : [];
          return {
            id: detail.instanceId,
            name: detail.name,
            path,
            parentName,
            position,
            components,
            componentTypes,
            primaryComponents: nonTransform.slice(0, 3),
            children
          };
        });
      }

      function buildFocusSignature(summary) {
        const payload = summary.map(item => ({
          id: item.id,
          name: item.name,
          position: item.position,
          componentTypes: item.componentTypes,
          children: item.children.map(child => child.instanceId)
        }));
        return JSON.stringify(payload);
      }

      async function fetchFocusedDetails(ids) {
        if (!ids || ids.length === 0) return [];

        const results = await Promise.all(
          ids.map(id => safeCallTool('get_gameobject', { idOrName: String(id) }))
        );

        const details = [];
        results.forEach(result => {
          const data = extractToolData(result);
          if (data && data.gameObject) details.push(data.gameObject);
        });

        return details;
      }

      async function refreshFocusedContext(trigger) {
        if (state.focusRefreshInFlight) return;

        if (!state.focusedObjects || state.focusedObjects.size === 0) {
          state.focusedDetails.clear();
          state.lastFocusSignature = '';
          state.lastFocusIdsKey = '';
          return;
        }

        state.focusRefreshInFlight = true;
        try {
          const ids = Array.from(state.focusedObjects);
          const idsKey = ids.slice().sort().join('|');
          const detailsList = await fetchFocusedDetails(ids);
          if (!detailsList || detailsList.length === 0) return;

          state.focusedDetails.clear();
          detailsList.forEach(detail => {
            if (detail && detail.instanceId !== undefined) {
              state.focusedDetails.set(detail.instanceId, detail);
            }
          });

          const trimmed = detailsList.map(trimGameObjectForContext).filter(Boolean);
          const summary = buildFocusSummary(trimmed);
          const signature = buildFocusSignature(summary);
          const changed = idsKey !== state.lastFocusIdsKey || signature !== state.lastFocusSignature;

          if (changed) {
            const narrativeParts = summary.map(item => {
              const primary = item.primaryComponents && item.primaryComponents.length > 0
                ? item.primaryComponents.join(', ')
                : 'Transform only';
              const positionText = item.position ? ` at ${item.position}` : '';
              return `${item.name} (${primary}${positionText})`;
            });

            await updateAgentContext(
              `User is focusing on ${summary.length} objects: ${narrativeParts.join('; ')}`,
              {
                focus: {
                  count: summary.length,
                  objects: summary,
                  timestamp: Date.now(),
                  trigger
                },
                recentAction: 'focus-refresh'
              }
            );

            state.lastFocusIdsKey = idsKey;
            state.lastFocusSignature = signature;
          }
        } finally {
          state.focusRefreshInFlight = false;
        }
      }
```

- [x] Replace `toggleFocus()` in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html) with the block below:

```javascript
      function toggleFocus(entry) {
        if (!entry || entry.id === undefined || entry.id === null) return;
        if (state.focusedObjects.has(entry.id)) {
          state.focusedObjects.delete(entry.id);
        } else {
          state.focusedObjects.add(entry.id);
        }
        renderHierarchy(state.lastHierarchy || []);
        refreshFocusedContext('focus-toggle');
      }
```

- [x] Replace `syncToAgent()` in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html) with the block below:

```javascript
      async function syncToAgent(source) {
        const sceneInfoPayload = state.lastSceneInfo || {};
        const activeScene = sceneInfoPayload.activeScene || {};
        const sceneName = activeScene.name || 'Unknown';
        const loadedScenes = state.lastSceneNames || [];
        const hierarchyList = state.lastHierarchy || [];
        const gameObjectCount = countGameObjects(hierarchyList);
        const errorCount = state.lastErrorCount || 0;
        const topErrors = state.recentErrors || [];
        const playModePayload = state.lastPlayMode || {};
        const isPlaying = Boolean(playModePayload.isPlaying);
        const isPaused = Boolean(playModePayload.isPaused);
        const inspectorFocus = state.lastInspectorFocus
          ? {
              gameObject: {
                id: state.lastInspectorFocus.id,
                name: state.lastInspectorFocus.name,
                path: state.lastInspectorFocus.path
              },
              component: state.lastInspectorFocus.component
            }
          : null;

        await updateAgentContext(
          `Unity Dashboard synchronized. Scene: ${sceneName}, ${gameObjectCount} objects, ${errorCount} errors.`,
          {
            sceneInfo: {
              name: sceneName,
              loadedScenes,
              rootObjectCount: Number(activeScene.rootCount ?? activeScene.rootObjectCount ?? activeScene.rootObjectsCount ?? 0)
            },
            activeGameObject: state.activeGameObject || null,
            playMode: { isPlaying, isPaused },
            topErrors,
            inspectorFocus,
            focusedObjects: Array.from(state.focusedObjects),
            syncTimestamp: Date.now(),
            syncSource: source
          }
        );
      }
```

- [x] Replace `fetchAll()` in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html) with the block below:

```javascript
      async function fetchAll() {
        // Important: fetch calls independently so one failure doesn't block all rendering.
        const sceneResult = await safeCallTool('get_scene_info', {});
        const hierarchyResult = await safeCallTool('get_scenes_hierarchy', {});
        const logsResult = await safeCallTool('get_console_logs', { limit: 200, includeStackTrace: false });
        const playModeResult = await safeCallTool('get_play_mode_status', {});

        const sceneData = extractToolData(sceneResult);
        const hierarchyData = extractToolData(hierarchyResult);
        const logsData = extractToolData(logsResult);
        const playModeData = extractToolData(playModeResult);

        if (sceneData) {
          updateScene(sceneData);
        } else {
          sceneInfo.textContent = sceneResult ? 'Scene info unavailable' : 'Failed to load scene info';
        }

        const hierarchyList = hierarchyData && hierarchyData.hierarchy ? hierarchyData.hierarchy : hierarchyData;
        state.lastHierarchy = hierarchyList || [];
        state.hierarchyIndex = buildHierarchyIndex(state.lastHierarchy || []);
        if (hierarchyResult) renderHierarchy(state.lastHierarchy);
        else hierarchy.textContent = 'Failed to load hierarchy';

        if (logsResult) updateLogs(logsData);
        else logsContent.textContent = 'Failed to load logs';

        if (playModeData) {
          updatePlayMode(playModeData);
        }

        await fetchInspectorState();
        await refreshFocusedContext('poll');
      }
```

##### Step 3 Verification Checklist
- [ ] No build errors
- [ ] Focused objects trigger `get_gameobject` calls and agent context updates with component details

#### Step 3 STOP & COMMIT
**STOP & COMMIT:** Agent must stop here and wait for the user to test, stage, and commit the change.

#### Step 4: Component Analysis (Point and Explain)
- [x] Add the Inspector card below after the Hierarchy card in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html):

```html
          <div class="card" style="margin-top:12px">
            <div class="section-title">Inspector</div>
            <div id="inspector" class="meta">Select a focused GameObject to inspect.</div>
          </div>
```

- [x] Add the Inspector CSS rules below at the end of the `<style>` block in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html):

```css
      #inspector{max-height:600px;overflow:auto;font-size:12px}
      .inspector-empty{color:var(--muted);font-size:12px;padding:6px 0}
      .component{border:1px solid #142a3d;background:#0b1726;padding:8px;border-radius:6px;margin:8px 0}
      .component-header{display:flex;align-items:center;justify-content:space-between;gap:8px;margin-bottom:6px}
      .component-title{font-size:12px;color:#cfe6f8}
      .component-meta{font-size:11px;color:var(--muted)}
      .component-props{background:#0a1420;border:1px solid #122432;border-radius:6px;padding:8px;white-space:pre-wrap;font-family:monospace;font-size:11px;color:#9fc5ff;margin:0}
      .analysis-note{margin-top:6px;padding:6px;background:#0f2234;border:1px dashed #27425c;border-radius:6px;font-size:11px;color:#a7f3d0}
      .analyze-btn{background:#0c1b2a;border:1px solid #1f3750;color:#9fb1c9;padding:4px 8px;border-radius:6px;font-size:11px;height:24px}
      .analyze-btn:hover{border-color:#4fd1c5;color:#e6eef8}
```

- [x] Add the DOM reference below with the other element lookups in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html):

```javascript
      const inspector = document.getElementById('inspector');
```

- [x] Replace the `state` declaration in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html) with the block below:

```javascript
      const state = {
        appReady: false,
        activeGameObject: null,
        currentPlayMode: null,
        recentErrors: [],
        lastSceneNames: [],
        lastCompilationState: 'idle',
        lastInspectorFocus: null,
        lastPlayModeState: null,
        lastErrorCount: 0,
        lastSceneInfo: null,
        lastHierarchy: null,
        lastLogs: null,
        lastPlayMode: null,
        focusedObjects: new Set(),
        focusFilterEnabled: false,
        focusedDetails: new Map(),
        focusRefreshInFlight: false,
        lastFocusIdsKey: '',
        lastFocusSignature: '',
        hierarchyIndex: new Map(),
        componentAnalyses: new Map()
      };
```

- [x] Add the Inspector rendering helpers below immediately after `refreshFocusedContext()` in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html):

```javascript
      async function requestComponentAnalysis(gameObject, component) {
        if (!gameObject || !component) return;
        const analysisKey = `${gameObject.instanceId}:${component.type}`;
        const prompt = [
          'Analyze this component for misconfiguration or runtime issues.',
          `GameObject: ${gameObject.name} (ID: ${gameObject.instanceId})`,
          `Component: ${component.type}`,
          'Properties:',
          JSON.stringify(component.properties || {}, null, 2)
        ].join('\n');

        state.componentAnalyses.set(analysisKey, { prompt, timestamp: Date.now() });
        renderInspector();

        await updateAgentContext(
          `Analyze component "${component.type}" on GameObject "${gameObject.name}".`,
          {
            componentAnalysis: {
              gameObject: {
                id: gameObject.instanceId,
                name: gameObject.name,
                path: state.hierarchyIndex.get(gameObject.instanceId)?.path || null
              },
              component,
              prompt,
              timestamp: Date.now()
            },
            recentAction: 'component-analysis'
          }
        );
      }

      function renderInspector() {
        if (!inspector) return;
        inspector.innerHTML = '';

        if (!state.activeGameObject) {
          inspector.innerHTML = '<div class="inspector-empty">Select a GameObject to inspect.</div>';
          return;
        }

        if (!state.focusedObjects.has(state.activeGameObject.id)) {
          inspector.innerHTML = '<div class="inspector-empty">Select a focused GameObject to inspect.</div>';
          return;
        }

        const detail = state.focusedDetails.get(state.activeGameObject.id);
        if (!detail) {
          inspector.innerHTML = '<div class="inspector-empty">Focused GameObject details not yet loaded.</div>';
          return;
        }

        const header = document.createElement('div');
        header.className = 'meta';
        header.textContent = `${detail.name} (ID: ${detail.instanceId})`;
        inspector.appendChild(header);

        const components = Array.isArray(detail.components) ? detail.components : [];
        if (components.length === 0) {
          const empty = document.createElement('div');
          empty.className = 'inspector-empty';
          empty.textContent = 'No components found.';
          inspector.appendChild(empty);
          return;
        }

        components.forEach(component => {
          const card = document.createElement('div');
          card.className = 'component';

          const headerRow = document.createElement('div');
          headerRow.className = 'component-header';

          const title = document.createElement('div');
          title.className = 'component-title';
          title.textContent = component.type || 'Component';

          const meta = document.createElement('div');
          meta.className = 'component-meta';
          meta.textContent = component.enabled === false ? 'Disabled' : 'Enabled';

          const analyzeBtn = document.createElement('button');
          analyzeBtn.className = 'analyze-btn';
          analyzeBtn.textContent = 'Analyze';
          analyzeBtn.addEventListener('click', async (event) => {
            event.stopPropagation();
            await requestComponentAnalysis(detail, component);
          });

          headerRow.appendChild(title);
          headerRow.appendChild(meta);
          headerRow.appendChild(analyzeBtn);
          card.appendChild(headerRow);

          if (component.properties) {
            const pre = document.createElement('pre');
            pre.className = 'component-props';
            pre.textContent = JSON.stringify(component.properties, null, 2);
            card.appendChild(pre);
          }

          const analysisKey = `${detail.instanceId}:${component.type}`;
          const analysis = state.componentAnalyses.get(analysisKey);
          if (analysis) {
            const note = document.createElement('div');
            note.className = 'analysis-note';
            note.textContent = analysis.prompt;
            card.appendChild(note);
          }

          inspector.appendChild(card);
        });
      }
```

- [x] Replace `handleSelection()` in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html) with the block below:

```javascript
      function handleSelection(entry, element) {
        setActiveGameObject(entry);
        highlightSelection(element);
        updateAgentContext(
          `User selected GameObject "${entry.name}" (ID: ${entry.id}) in Unity Dashboard.`,
          {
            activeGameObject: { id: entry.id, name: entry.name, path: entry.path },
            recentAction: 'selection'
          }
        );
        renderInspector();
      }
```

- [x] Update `refreshFocusedContext()` in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html) by adding the `renderInspector();` call at the end of the `try` block, just before `finally`:

```javascript
          if (changed) {
            const narrativeParts = summary.map(item => {
              const primary = item.primaryComponents && item.primaryComponents.length > 0
                ? item.primaryComponents.join(', ')
                : 'Transform only';
              const positionText = item.position ? ` at ${item.position}` : '';
              return `${item.name} (${primary}${positionText})`;
            });

            await updateAgentContext(
              `User is focusing on ${summary.length} objects: ${narrativeParts.join('; ')}`,
              {
                focus: {
                  count: summary.length,
                  objects: summary,
                  timestamp: Date.now(),
                  trigger
                },
                recentAction: 'focus-refresh'
              }
            );

            state.lastFocusIdsKey = idsKey;
            state.lastFocusSignature = signature;
          }

          renderInspector();
```

##### Step 4 Verification Checklist
- [ ] No build errors
- [ ] Selecting a focused GameObject shows components with Analyze buttons

#### Step 4 STOP & COMMIT
**STOP & COMMIT:** Agent must stop here and wait for the user to test, stage, and commit the change.

#### Step 5: Console Error Push-to-Chat
- [x] Add the log action CSS rules below at the end of the `<style>` block in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html):

```css
      .log-row{display:flex;align-items:flex-start;justify-content:space-between;gap:8px}
      .log-actions{display:flex;align-items:center;gap:6px}
      .log-action-btn{background:#0c1b2a;border:1px solid #1f3750;color:#9fb1c9;padding:2px 6px;border-radius:6px;font-size:10px;height:22px}
      .log-action-btn:hover{border-color:#4fd1c5;color:#e6eef8}
```

- [x] Replace the `state` declaration in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html) with the block below:

```javascript
      const state = {
        appReady: false,
        activeGameObject: null,
        currentPlayMode: null,
        recentErrors: [],
        lastSceneNames: [],
        lastCompilationState: 'idle',
        lastInspectorFocus: null,
        lastPlayModeState: null,
        lastErrorCount: 0,
        lastSceneInfo: null,
        lastHierarchy: null,
        lastLogs: null,
        lastPlayMode: null,
        focusedObjects: new Set(),
        focusFilterEnabled: false,
        focusedDetails: new Map(),
        focusRefreshInFlight: false,
        lastFocusIdsKey: '',
        lastFocusSignature: '',
        hierarchyIndex: new Map(),
        componentAnalyses: new Map(),
        logSignatures: new Set(),
        lastLogCount: 0
      };
```

- [x] Replace the `get_console_logs` call in `fetchAll()` in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html) with the line below:

```javascript
        const logsResult = await safeCallTool('get_console_logs', { limit: 200, includeStackTrace: false });
```

- [x] Replace the `updateLogs()` function in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html) with the block below:

```javascript
      function getLogKey(item) {
        return `${item.timestamp}|${item.type}|${item.message}`;
      }

      function extractStackFrames(stackTrace, maxFrames = 5) {
        if (!stackTrace) return [];
        return String(stackTrace)
          .split('\n')
          .map(line => line.trim())
          .filter(Boolean)
          .slice(0, maxFrames);
      }

      function extractGameObjectReference(item) {
        const sources = [item.message, item.stackTrace];
        for (const text of sources) {
          if (!text) continue;
          const match = String(text).match(/gameobject\s*['"]([^'"]+)['"]/i);
          if (match) return match[1];
          const pathMatch = String(text).match(/gameobject\s*[:=]\s*([A-Za-z0-9_\/\-\s]+)/i);
          if (pathMatch) return pathMatch[1].trim();
        }
        return null;
      }

      async function fetchStackTraceForLog(item) {
        if (item.stackTrace) return item.stackTrace;
        const result = await safeCallTool('get_console_logs', { limit: 50, includeStackTrace: true });
        const data = extractToolData(result);
        const logs = data && data.logs ? data.logs : [];
        const match = logs.find(entry => entry && entry.message === item.message && entry.timestamp === item.timestamp);
        return match && match.stackTrace ? match.stackTrace : '';
      }

      async function pushConsoleErrorToAgent(item, source, includeStackTrace) {
        const stackTrace = includeStackTrace ? await fetchStackTraceForLog(item) : item.stackTrace;
        const stackFrames = extractStackFrames(stackTrace, 5);
        const hint = extractGameObjectReference(item);
        let gameObjectContext = null;
        let gameObjectName = null;

        if (hint) {
          const result = await safeCallTool('get_gameobject', { idOrName: hint });
          const data = extractToolData(result);
          if (data && data.gameObject) {
            gameObjectContext = trimGameObjectForContext(data.gameObject);
            gameObjectName = data.gameObject.name || hint;
          }
        }

        if (!gameObjectContext && state.activeGameObject && state.activeGameObject.id !== undefined) {
          const result = await safeCallTool('get_gameobject', { idOrName: String(state.activeGameObject.id) });
          const data = extractToolData(result);
          if (data && data.gameObject) {
            gameObjectContext = trimGameObjectForContext(data.gameObject);
            gameObjectName = data.gameObject.name || state.activeGameObject.name;
          }
        }

        const summaryPrefix = item.type === 'warning' ? 'Warning' : 'Critical error';
        const objectSuffix = gameObjectName
          ? ` affecting GameObject "${gameObjectName}".`
          : ' with no specific GameObject reference.';
        const description = `${summaryPrefix} detected: ${item.message}${objectSuffix}`;

        await updateAgentContext(description, {
          consoleError: {
            message: item.message,
            type: item.type,
            timestamp: item.timestamp,
            stackFrames
          },
          gameObject: gameObjectContext,
          recentAction: 'console-error',
          pushSource: source
        });
      }

      async function updateLogs(payload) {
        logsContent.innerHTML = '';
        const items = Array.isArray(payload)
          ? payload
          : (payload && payload.logs ? payload.logs : null);
        if (!items || items.length === 0) {
          logsContent.innerHTML = '<div class="log info">No logs</div>';
          state.lastLogs = { logs: [] };
          return;
        }

        const normalized = items.map(item => ({
          message: item.message || item.text || '',
          timestamp: item.timestamp || '',
          type: normalizeLogType(item.level || item.type),
          stackTrace: item.stackTrace || item.stack || ''
        }));

        const ordered = sortLogsBySeverity(normalized);
        const errorItems = ordered.filter(item => item.type === 'error' || item.type === 'warning');

        if (ordered.length < state.lastLogCount) {
          state.logSignatures = new Set();
        }

        const newErrors = errorItems.filter(item => !state.logSignatures.has(getLogKey(item)));
        for (const item of newErrors) {
          state.logSignatures.add(getLogKey(item));
          await pushConsoleErrorToAgent(item, 'auto', false);
        }

        state.lastLogCount = ordered.length;
        state.lastErrorCount = errorItems.length;
        state.recentErrors = errorItems.slice(0, 3);
        state.lastLogs = { logs: ordered };

        const compilation = detectCompilationState(ordered, errorItems);
        if (compilation.state !== state.lastCompilationState) {
          updateAgentContext(
            `Unity compilation ${compilation.state}. ${compilation.errorCount > 0 ? compilation.errorCount + ' errors' : 'No errors'}`,
            {
              compilation: {
                state: compilation.state,
                errorCount: compilation.errorCount,
                timestamp: Date.now()
              }
            }
          );
          state.lastCompilationState = compilation.state;
        }

        ordered.forEach(item => {
          const row = document.createElement('div');
          row.className = 'log ' + item.type + ' log-row';

          const message = document.createElement('div');
          message.textContent = `[${item.timestamp}] ${item.type.toUpperCase()}: ${item.message}`;
          row.appendChild(message);

          if (item.type === 'error' || item.type === 'warning') {
            const actions = document.createElement('div');
            actions.className = 'log-actions';

            const pushBtn = document.createElement('button');
            pushBtn.className = 'log-action-btn';
            pushBtn.textContent = 'Push again';
            pushBtn.title = 'Push error context to agent';
            pushBtn.addEventListener('click', async (event) => {
              event.stopPropagation();
              await pushConsoleErrorToAgent(item, 'manual', true);
            });

            actions.appendChild(pushBtn);
            row.appendChild(actions);
          }

          logsContent.appendChild(row);
        });
      }
```

- [x] Update `fetchAll()` in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html) to await the async log update:

```javascript
        if (logsResult) await updateLogs(logsData);
        else logsContent.textContent = 'Failed to load logs';
```

##### Step 5 Verification Checklist
- [x] No build errors
- [ ] New errors automatically push context with stack frames and GameObject details
- [ ] Clicking "Push again" on an error re-sends context to the agent

#### Step 5 STOP & COMMIT
**STOP & COMMIT:** Agent must stop here and wait for the user to test, stage, and commit the change.
