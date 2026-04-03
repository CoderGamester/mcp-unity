# MCP App Bidirectional Context Improvements

## Goal
Enable the Unity dashboard MCP app to push selection, play mode, console, scene, compilation, and sync context to the agent, with optional inspector focus context once the inspector resource is added.

## Prerequisites
Make sure that the use is currently on the `feature/mcp-app-bidirectional-context` branch before beginning implementation.
If not, move them to the correct branch. If the branch does not exist, create it from main.

### Step-by-Step Instructions

#### Step 1: Update Dashboard App Context Push (Steps 1-7)
- [x] Update the Unity dashboard HTML to initialize the MCP App SDK, add context helpers/state, selection tracking, play mode updates, console error tracking, sync button, scene change notifications, and compilation status indicators.
- [x] Copy and paste code below into [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html):

```html
<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width,initial-scale=1" />
    <title>Unity Dashboard</title>
    <style>
      :root{--bg:#0b1220;--card:#07101a;--muted:#9fb1c9;--accent:#4fd1c5}
      html,body{height:100%;margin:0}
      body{font-family:Inter,Segoe UI,Roboto,system-ui,Arial;background:linear-gradient(180deg,#071021 0%,#071627 100%);color:#e6eef8;padding:16px}
      .app{max-width:1100px;margin:0 auto}
      header{display:flex;align-items:center;justify-content:space-between;margin-bottom:12px}
      h1{font-size:18px;margin:0}
      .controls{display:flex;gap:8px}
      button{background:transparent;border:1px solid #163144;color:var(--muted);padding:8px 12px;border-radius:6px;cursor:pointer}
      button.primary{background:var(--accent);color:#022; border-color:transparent}
      .grid{display:grid;grid-template-columns:1fr 360px;gap:12px}
      .card{background:var(--card);border:1px solid #122432;padding:12px;border-radius:8px}
      .section-title{font-size:13px;color:var(--muted);margin-bottom:8px}
      #hierarchy{max-height:560px;overflow:auto;font-size:13px}
      .node{padding:6px 8px;border-radius:4px;color:#cfe6f8}
      .node:hover{background:#0f2940}
      .node.selected{background:#1b3b5a;color:#fff}
      .node.scene{color:#9fc5ff;font-weight:600}
      .logs{max-height:560px;overflow:auto;font-family:monospace;font-size:12px}
      .log.info{color:#9fc5ff}
      .log.warn{color:#ffd580}
      .log.error{color:#ff9b9b}
      .meta{font-size:12px;color:var(--muted)}
      .toolbar{display:flex;gap:8px;align-items:center}
      label{font-size:12px;color:var(--muted)}
      input[type=range]{width:120px}
      .status-row{display:flex;align-items:center;gap:8px;margin-top:6px}
      .status-badge{font-size:11px;padding:2px 8px;border-radius:999px;border:1px solid #214256;color:#cfe6f8;text-transform:uppercase;letter-spacing:0.04em}
      .status-badge.playing{background:#0f3b2e;border-color:#1f8b6f;color:#a7f3d0}
      .status-badge.edit{background:#1a2232;border-color:#2a3b55;color:#cbd5e1}
      .status-badge.compiling{background:#2a1f10;border-color:#a16207;color:#fde68a}
      .status-badge.failed{background:#3b1313;border-color:#b91c1c;color:#fecaca}
      .status-badge.success{background:#0f2b1c;border-color:#22c55e;color:#bbf7d0}
      .spinner{width:10px;height:10px;border:2px solid #a16207;border-top-color:transparent;border-radius:50%;display:inline-block;animation:spin 1s linear infinite}
      @keyframes spin{to{transform:rotate(360deg)}}
    </style>
  </head>
  <body>
    <div class="app">
      <header>
        <h1>Unity Dashboard</h1>
        <div class="controls">
          <div class="toolbar">
            <button id="syncBtn" class="primary" title="Sync context to agent">Sync to Agent</button>
            <button id="playBtn" title="Play">Play</button>
            <button id="pauseBtn" title="Pause">Pause</button>
            <button id="stepBtn" title="Step">Step</button>
            <button id="refreshBtn" title="Refresh">Refresh</button>
            <label class="meta"><input id="autoRefresh" type="checkbox"/> Auto</label>
            <input id="interval" type="range" min="1" max="10" value="3" />
            <span id="intervalLabel" class="meta">3s</span>
          </div>
        </div>
      </header>

      <div class="grid">
        <div>
          <div class="card">
            <div class="section-title">Scene</div>
            <div id="sceneInfo" class="meta">Loading scene info...</div>
          </div>

          <div class="card" style="margin-top:12px">
            <div class="section-title">Hierarchy</div>
            <div id="hierarchy">Loading hierarchy...</div>
          </div>
        </div>

        <div>
          <div class="card">
            <div class="section-title">Play Mode</div>
            <div id="playMode" class="meta">
              <div class="status-row">
                <span id="playModeBadge" class="status-badge edit">Edit</span>
                <span id="playModeText">Edit mode</span>
              </div>
              <div class="status-row">
                <span id="compileBadge" class="status-badge edit">Idle</span>
                <span id="compileText">Compilation idle</span>
                <span id="compileSpinner" class="spinner" style="display:none" aria-hidden="true"></span>
              </div>
            </div>
          </div>

          <div class="card" style="margin-top:12px">
            <div class="section-title">Console Logs</div>
            <div id="logs" class="logs">Loading logs...</div>
          </div>
        </div>
      </div>
    </div>

    <script src="https://cdn.jsdelivr.net/npm/@modelcontextprotocol/sdk-apps/dist/index.js"></script>
    <script>
      const PROTOCOL_VERSION = '2026-01-26';
      const app = new MCPApp();

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
        lastPlayMode: null
      };

      async function updateAgentContext(description, metadata) {
        if (!state.appReady) return;
        try {
          await app.updateModelContext({
            description,
            ...metadata
          });
        } catch (error) {
          // Ignore context update failures to keep UI responsive.
        }
      }

      async function initializeApp() {
        await app.initialize({
          appInfo: { name: 'unity-dashboard', version: '1.0.0' },
          appCapabilities: { tools: {} },
          protocolVersion: PROTOCOL_VERSION
        });
        state.appReady = true;
      }

      async function callTool(name, args = {}) {
        return app.callTool(name, args);
      }

      const playBtn = document.getElementById('playBtn');
      const pauseBtn = document.getElementById('pauseBtn');
      const stepBtn = document.getElementById('stepBtn');
      const refreshBtn = document.getElementById('refreshBtn');
      const syncBtn = document.getElementById('syncBtn');
      const autoRefresh = document.getElementById('autoRefresh');
      const interval = document.getElementById('interval');
      const intervalLabel = document.getElementById('intervalLabel');
      const sceneInfo = document.getElementById('sceneInfo');
      const hierarchy = document.getElementById('hierarchy');
      const logs = document.getElementById('logs');
      const playModeBadge = document.getElementById('playModeBadge');
      const playModeText = document.getElementById('playModeText');
      const compileBadge = document.getElementById('compileBadge');
      const compileText = document.getElementById('compileText');
      const compileSpinner = document.getElementById('compileSpinner');

      playBtn.onclick = () => callTool('execute_menu_item', { menuPath: 'Edit/Play' }).catch(() => {});
      pauseBtn.onclick = () => callTool('execute_menu_item', { menuPath: 'Edit/Pause' }).catch(() => {});
      stepBtn.onclick = () => callTool('execute_menu_item', { menuPath: 'Edit/Step' }).catch(() => {});
      refreshBtn.onclick = fetchAll;
      syncBtn.onclick = () => syncToAgent('manual');

      interval.oninput = () => { intervalLabel.textContent = interval.value + 's'; };

      function extractToolData(callToolResult) {
        if (!callToolResult) return null;
        if (callToolResult.data) return callToolResult.data;
        const first = (callToolResult.content || [])[0];
        if (first && typeof first.text === 'string') {
          try { return JSON.parse(first.text); } catch { return null; }
        }
        return null;
      }

      function normalizeLogType(type) {
        const raw = String(type || '').toLowerCase();
        if (raw === 'log' || raw === 'info') return 'info';
        if (raw === 'warning') return 'warning';
        if (raw === 'error' || raw === 'exception' || raw === 'assert') return 'error';
        return 'info';
      }

      function sortLogsBySeverity(items) {
        const priority = { error: 0, warning: 1, info: 2 };
        return items.slice().sort((a, b) => priority[a.type] - priority[b.type]);
      }

      function arraysEqual(a, b) {
        if (a === b) return true;
        if (!a || !b) return false;
        if (a.length !== b.length) return false;
        for (let i = 0; i < a.length; i += 1) {
          if (a[i] !== b[i]) return false;
        }
        return true;
      }

      function updateScene(payload) {
        if (!payload) return;
        const active = payload.activeScene;
        if (!active) return;
        state.lastSceneInfo = payload;
        sceneInfo.textContent = `${active.name} - Root Count: ${active.rootCount} - ${active.isLoaded ? 'Loaded' : 'Not loaded'}`;
      }

      function detectSceneChanges(sceneNames) {
        if (!sceneNames) return;
        if (sceneNames.length === 0 && state.lastSceneNames.length === 0) {
          return;
        }
        if (!arraysEqual(sceneNames, state.lastSceneNames)) {
          const added = sceneNames.filter(name => !state.lastSceneNames.includes(name));
          const removed = state.lastSceneNames.filter(name => !sceneNames.includes(name));
          updateAgentContext(
            `Unity scene changed. Now loaded: ${sceneNames.join(', ') || 'None'}`,
            {
              sceneChange: {
                loaded: sceneNames,
                added,
                removed
              }
            }
          );
        }
        state.lastSceneNames = sceneNames;
      }

      function setActiveGameObject(entry) {
        state.activeGameObject = entry;
      }

      function highlightSelection(element) {
        const previous = hierarchy.querySelector('.node.selected');
        if (previous && previous !== element) {
          previous.classList.remove('selected');
        }
        element.classList.add('selected');
      }

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
      }

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
        element.textContent = name;

        if (state.activeGameObject && state.activeGameObject.id === entry.id) {
          element.classList.add('selected');
        }

        element.addEventListener('click', (event) => {
          event.stopPropagation();
          handleSelection(entry, element);
        });

        container.appendChild(element);

        const children = node.children || [];
        children.forEach(child => renderGameObjectNode(child, depth + 1, currentPath, container));
      }

      function renderHierarchy(list) {
        if (!list || list.length === 0) {
          hierarchy.textContent = 'No scene data';
          return;
        }

        hierarchy.innerHTML = '';
        const container = document.createElement('div');

        list.forEach(scene => {
          const sceneNode = document.createElement('div');
          sceneNode.className = 'node scene';
          sceneNode.textContent = scene.name || 'Scene';
          container.appendChild(sceneNode);

          const rootObjects = scene.rootObjects || [];
          rootObjects.forEach(root => renderGameObjectNode(root, 1, '', container));
        });

        hierarchy.appendChild(container);
        detectSceneChanges(list.map(scene => scene.name).filter(Boolean));
      }

      function isCompilationError(message) {
        const text = String(message || '').toLowerCase();
        return text.includes('compilererror') || text.includes('.cs(') || text.includes('cs(');
      }

      function detectCompilationState(logsList, errorItems) {
        const messages = logsList.map(item => String(item.message || '').toLowerCase());
        const isCompiling = messages.some(message => message.includes('compiling'));
        const compileErrors = errorItems.filter(item => isCompilationError(item.message));
        if (isCompiling) return { state: 'compiling', errorCount: compileErrors.length };
        if (compileErrors.length > 0) return { state: 'failed', errorCount: compileErrors.length };
        if (state.lastCompilationState === 'failed' || state.lastCompilationState === 'compiling') {
          return { state: 'success', errorCount: 0 };
        }
        return { state: state.lastCompilationState || 'idle', errorCount: 0 };
      }

      function updateCompilationStatus(stateValue, errorCount) {
        const labelMap = {
          idle: 'Idle',
          compiling: 'Compiling',
          failed: 'Failed',
          success: 'Success'
        };
        const badgeClassMap = {
          idle: 'edit',
          compiling: 'compiling',
          failed: 'failed',
          success: 'success'
        };

        const badgeClass = badgeClassMap[stateValue] || 'edit';
        compileBadge.className = `status-badge ${badgeClass}`;
        compileBadge.textContent = labelMap[stateValue] || 'Idle';

        if (stateValue === 'failed') {
          compileText.textContent = `Compilation failed (${errorCount} errors)`;
        } else if (stateValue === 'success') {
          compileText.textContent = 'Compilation succeeded';
        } else if (stateValue === 'compiling') {
          compileText.textContent = 'Compiling...';
        } else {
          compileText.textContent = 'Compilation idle';
        }

        compileSpinner.style.display = stateValue === 'compiling' ? 'inline-block' : 'none';
      }

      function updateLogs(payload) {
        logs.innerHTML = '';
        const items = payload && payload.logs ? payload.logs : null;
        if (!items || items.length === 0) {
          logs.textContent = 'No logs';
          state.lastLogs = { logs: [] };
          return;
        }

        const normalized = items.map(item => ({
          message: item.message || item.text || '',
          timestamp: item.timestamp || '',
          type: normalizeLogType(item.level || item.type)
        }));

        const ordered = sortLogsBySeverity(normalized);
        const errorItems = ordered.filter(item => item.type === 'error' || item.type === 'warning');
        const newErrorCount = errorItems.length;

        if (newErrorCount > state.lastErrorCount) {
          const delta = errorItems.slice(0, newErrorCount - state.lastErrorCount);
          if (delta.length > 0) {
            updateAgentContext(
              `${delta.length} new error(s) in Unity Console: ${delta.map(item => item.message).join('; ')}`,
              {
                consoleErrors: delta.slice(0, 3).map(item => ({
                  message: item.message,
                  type: item.type,
                  timestamp: item.timestamp
                })),
                recentAction: 'console-error'
              }
            );
          }
        }

        state.lastErrorCount = newErrorCount;
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
        updateCompilationStatus(compilation.state, compilation.errorCount);

        ordered.forEach(item => {
          const el = document.createElement('div');
          el.className = 'log ' + item.type;
          el.textContent = `[${item.timestamp}] ${item.type.toUpperCase()}: ${item.message}`;
          logs.appendChild(el);
        });
      }

      function updatePlayMode(payload) {
        if (!payload) return;

        const isPlaying = Boolean(payload.isPlaying);
        const isPaused = Boolean(payload.isPaused);
        state.lastPlayMode = payload;

        const currentKey = `${isPlaying}-${isPaused}`;
        if (currentKey !== state.lastPlayModeState) {
          updateAgentContext(
            `Unity ${isPlaying ? 'entered Play mode' : 'exited Play mode'}.`,
            {
              unityState: {
                isPlaying,
                isPaused,
                timestamp: Date.now()
              }
            }
          );
          state.lastPlayModeState = currentKey;
        }

        const badgeLabel = isPlaying ? (isPaused ? 'Paused' : 'Play') : 'Edit';
        playModeBadge.textContent = badgeLabel;
        playModeBadge.className = `status-badge ${isPlaying ? 'playing' : 'edit'}`;
        playModeText.textContent = isPlaying ? (isPaused ? 'Play mode paused' : 'Play mode') : 'Edit mode';
      }

      function countGameObjects(hierarchyList) {
        let count = 0;
        function visit(node) {
          if (!node) return;
          count += 1;
          const children = node.children || [];
          children.forEach(child => visit(child));
        }

        if (Array.isArray(hierarchyList)) {
          hierarchyList.forEach(scene => {
            const roots = scene.rootObjects || [];
            roots.forEach(root => visit(root));
          });
        }
        return count;
      }

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

        await updateAgentContext(
          `Unity Dashboard synchronized. Scene: ${sceneName}, ${gameObjectCount} objects, ${errorCount} errors.`,
          {
            sceneInfo: {
              name: sceneName,
              loadedScenes,
              rootObjectCount: activeScene.rootCount || 0
            },
            activeGameObject: state.activeGameObject || null,
            playMode: { isPlaying, isPaused },
            topErrors,
            syncTimestamp: Date.now(),
            syncSource: source
          }
        );
      }

      async function fetchAll() {
        try {
          const [sceneResult, hierarchyResult, logsResult, playModeResult] = await Promise.all([
            callTool('get_scene_info', {}),
            callTool('get_scenes_hierarchy', {}),
            callTool('get_console_logs', { limit: 200, includeStackTrace: false }),
            callTool('get_play_mode_status', {})
          ]);

          const sceneData = extractToolData(sceneResult);
          const hierarchyData = extractToolData(hierarchyResult);
          const logsData = extractToolData(logsResult);
          const playModeData = extractToolData(playModeResult);

          updateScene(sceneData);

          const hierarchyList = hierarchyData && hierarchyData.hierarchy ? hierarchyData.hierarchy : hierarchyData;
          state.lastHierarchy = hierarchyList || [];
          renderHierarchy(state.lastHierarchy);

          updateLogs(logsData);
          updatePlayMode(playModeData);
        } catch (error) {
          // Keep UI usable even if one call fails.
        }
      }

      let timer = null;
      function scheduleAuto() {
        if (timer) clearInterval(timer);
        if (autoRefresh.checked) {
          timer = setInterval(fetchAll, Number(interval.value) * 1000);
        }
      }
      autoRefresh.onchange = scheduleAuto;
      interval.onchange = scheduleAuto;

      (async () => {
        try {
          await initializeApp();
        } catch (error) {
          // If init fails, the host likely doesn't support MCP Apps.
        }
        await fetchAll();
        await syncToAgent('auto');
      })();
    </script>
  </body>
</html>
```

- [ ] Open the dashboard once to confirm the automatic sync runs without errors.

##### Step 1 Verification Checklist
- [ ] No build errors
- [ ] Open the Unity dashboard and confirm the Sync button appears.
- [ ] Click a GameObject in the hierarchy and confirm the selection highlight appears.
- [ ] Enter/exit play mode and confirm the play mode badge updates.
- [ ] Trigger a warning or error and confirm the console section updates.

#### Step 1 STOP & COMMIT
**STOP & COMMIT:** Agent must stop here and wait for the user to test, stage, and commit the change.

#### Step 2: Add Inspector Focus Resource (Step 8)
- [ ] Create the Unity inspector state resource.
- [ ] Copy and paste code below into [Editor/Resources/GetInspectorStateResource.cs](Editor/Resources/GetInspectorStateResource.cs):

```csharp
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Resources
{
    /// <summary>
    /// Resource for retrieving the current Inspector focus (selected GameObject and component).
    /// </summary>
    public class GetInspectorStateResource : McpResourceBase
    {
        public GetInspectorStateResource()
        {
            Name = "get_inspector_state";
            Description = "Retrieves the active Inspector selection and focused component";
            Uri = "unity://inspector_state";
        }

        public override JObject Fetch(JObject parameters)
        {
            GameObject activeGameObject = Selection.activeGameObject;
            JObject activeObjectPayload = null;

            if (activeGameObject != null)
            {
                activeObjectPayload = new JObject
                {
                    ["name"] = activeGameObject.name,
                    ["instanceId"] = activeGameObject.GetInstanceID(),
                    ["path"] = GetHierarchyPath(activeGameObject.transform)
                };
            }

            string focusedComponent = GetFocusedComponentType();

            return new JObject
            {
                ["success"] = true,
                ["message"] = "Retrieved inspector state",
                ["activeGameObject"] = activeObjectPayload,
                ["focusedComponent"] = focusedComponent
            };
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null) return null;

            List<string> segments = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                segments.Add(current.name);
                current = current.parent;
            }
            segments.Reverse();

            return string.Join("/", segments);
        }

        private static string GetFocusedComponentType()
        {
            ActiveEditorTracker tracker = ActiveEditorTracker.sharedTracker;
            if (tracker == null) return null;

            Editor[] editors = tracker.activeEditors;
            if (editors == null || editors.Length == 0) return null;

            foreach (Editor editor in editors)
            {
                if (editor == null) continue;
                if (editor.target is Component component)
                {
                    return component.GetType().Name;
                }
                if (editor.target is GameObject)
                {
                    return "GameObject";
                }
            }

            return null;
        }
    }
}
```

- [ ] Replace the `RegisterResources()` method in [Editor/UnityBridge/McpUnityServer.cs](Editor/UnityBridge/McpUnityServer.cs) with the code below:

```csharp
        private void RegisterResources()
        {
            // Register GetMenuItemsResource
            GetMenuItemsResource getMenuItemsResource = new GetMenuItemsResource();
            _resources.Add(getMenuItemsResource.Name, getMenuItemsResource);

            // Register GetConsoleLogsResource
            GetConsoleLogsResource getConsoleLogsResource = new GetConsoleLogsResource(_consoleLogsService);
            _resources.Add(getConsoleLogsResource.Name, getConsoleLogsResource);

            // Register GetScenesHierarchyResource
            GetScenesHierarchyResource getScenesHierarchyResource = new GetScenesHierarchyResource();
            _resources.Add(getScenesHierarchyResource.Name, getScenesHierarchyResource);

            // Register GetPackagesResource
            GetPackagesResource getPackagesResource = new GetPackagesResource();
            _resources.Add(getPackagesResource.Name, getPackagesResource);

            // Register GetAssetsResource
            GetAssetsResource getAssetsResource = new GetAssetsResource();
            _resources.Add(getAssetsResource.Name, getAssetsResource);

            // Register GetTestsResource
            GetTestsResource getTestsResource = new GetTestsResource(_testRunnerService);
            _resources.Add(getTestsResource.Name, getTestsResource);

            // Register GetGameObjectResource
            GetGameObjectResource getGameObjectResource = new GetGameObjectResource();
            _resources.Add(getGameObjectResource.Name, getGameObjectResource);

            // Register GetInspectorStateResource
            GetInspectorStateResource getInspectorStateResource = new GetInspectorStateResource();
            _resources.Add(getInspectorStateResource.Name, getInspectorStateResource);
        }
```

- [ ] Create the Node resource definition.
- [ ] Copy and paste code below into [Server~/src/resources/getInspectorStateResource.ts](Server~/src/resources/getInspectorStateResource.ts):

```typescript
import { Logger } from '../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { ReadResourceResult } from '@modelcontextprotocol/sdk/types.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';

const resourceName = 'get_inspector_state';
const resourceUri = 'unity://inspector_state';
const resourceMimeType = 'application/json';

export function registerGetInspectorStateResource(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering resource: ${resourceName}`);

  server.resource(
    resourceName,
    resourceUri,
    {
      description: 'Retrieve the active Inspector selection and focused component in Unity',
      mimeType: resourceMimeType
    },
    async () => {
      try {
        return await resourceHandler(mcpUnity);
      } catch (error) {
        logger.error(`Error handling resource ${resourceName}: ${error}`);
        throw error;
      }
    }
  );
}

async function resourceHandler(mcpUnity: McpUnity): Promise<ReadResourceResult> {
  const response = await mcpUnity.sendRequest({
    method: resourceName,
    params: {}
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.RESOURCE_FETCH,
      response.message || 'Failed to fetch inspector state from Unity'
    );
  }

  return {
    contents: [{
      uri: resourceUri,
      mimeType: resourceMimeType,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
```

- [ ] Register the resource in [Server~/src/index.ts](Server~/src/index.ts) by adding the import and registration call below.
- [ ] Add the import with the other resource imports:

```typescript
import { registerGetInspectorStateResource } from './resources/getInspectorStateResource.js';
```

- [ ] Add the registration with the other resource registrations (after `registerGetGameObjectResource(...)` is a good place):

```typescript
registerGetInspectorStateResource(server, mcpUnity, resourceLogger);
```

- [ ] Replace the dashboard HTML with the inspector-aware version by copying the code below into [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html):

```html
<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width,initial-scale=1" />
    <title>Unity Dashboard</title>
    <style>
      :root{--bg:#0b1220;--card:#07101a;--muted:#9fb1c9;--accent:#4fd1c5}
      html,body{height:100%;margin:0}
      body{font-family:Inter,Segoe UI,Roboto,system-ui,Arial;background:linear-gradient(180deg,#071021 0%,#071627 100%);color:#e6eef8;padding:16px}
      .app{max-width:1100px;margin:0 auto}
      header{display:flex;align-items:center;justify-content:space-between;margin-bottom:12px}
      h1{font-size:18px;margin:0}
      .controls{display:flex;gap:8px}
      button{background:transparent;border:1px solid #163144;color:var(--muted);padding:8px 12px;border-radius:6px;cursor:pointer}
      button.primary{background:var(--accent);color:#022; border-color:transparent}
      .grid{display:grid;grid-template-columns:1fr 360px;gap:12px}
      .card{background:var(--card);border:1px solid #122432;padding:12px;border-radius:8px}
      .section-title{font-size:13px;color:var(--muted);margin-bottom:8px}
      #hierarchy{max-height:560px;overflow:auto;font-size:13px}
      .node{padding:6px 8px;border-radius:4px;color:#cfe6f8}
      .node:hover{background:#0f2940}
      .node.selected{background:#1b3b5a;color:#fff}
      .node.focused{outline:1px solid #ffd580;background:#14283e}
      .node.scene{color:#9fc5ff;font-weight:600}
      .logs{max-height:560px;overflow:auto;font-family:monospace;font-size:12px}
      .log.info{color:#9fc5ff}
      .log.warn{color:#ffd580}
      .log.error{color:#ff9b9b}
      .meta{font-size:12px;color:var(--muted)}
      .toolbar{display:flex;gap:8px;align-items:center}
      label{font-size:12px;color:var(--muted)}
      input[type=range]{width:120px}
      .status-row{display:flex;align-items:center;gap:8px;margin-top:6px}
      .status-badge{font-size:11px;padding:2px 8px;border-radius:999px;border:1px solid #214256;color:#cfe6f8;text-transform:uppercase;letter-spacing:0.04em}
      .status-badge.playing{background:#0f3b2e;border-color:#1f8b6f;color:#a7f3d0}
      .status-badge.edit{background:#1a2232;border-color:#2a3b55;color:#cbd5e1}
      .status-badge.compiling{background:#2a1f10;border-color:#a16207;color:#fde68a}
      .status-badge.failed{background:#3b1313;border-color:#b91c1c;color:#fecaca}
      .status-badge.success{background:#0f2b1c;border-color:#22c55e;color:#bbf7d0}
      .spinner{width:10px;height:10px;border:2px solid #a16207;border-top-color:transparent;border-radius:50%;display:inline-block;animation:spin 1s linear infinite}
      @keyframes spin{to{transform:rotate(360deg)}}
    </style>
  </head>
  <body>
    <div class="app">
      <header>
        <h1>Unity Dashboard</h1>
        <div class="controls">
          <div class="toolbar">
            <button id="syncBtn" class="primary" title="Sync context to agent">Sync to Agent</button>
            <button id="playBtn" title="Play">Play</button>
            <button id="pauseBtn" title="Pause">Pause</button>
            <button id="stepBtn" title="Step">Step</button>
            <button id="refreshBtn" title="Refresh">Refresh</button>
            <label class="meta"><input id="autoRefresh" type="checkbox"/> Auto</label>
            <input id="interval" type="range" min="1" max="10" value="3" />
            <span id="intervalLabel" class="meta">3s</span>
          </div>
        </div>
      </header>

      <div class="grid">
        <div>
          <div class="card">
            <div class="section-title">Scene</div>
            <div id="sceneInfo" class="meta">Loading scene info...</div>
          </div>

          <div class="card" style="margin-top:12px">
            <div class="section-title">Hierarchy</div>
            <div id="hierarchy">Loading hierarchy...</div>
          </div>
        </div>

        <div>
          <div class="card">
            <div class="section-title">Play Mode</div>
            <div id="playMode" class="meta">
              <div class="status-row">
                <span id="playModeBadge" class="status-badge edit">Edit</span>
                <span id="playModeText">Edit mode</span>
              </div>
              <div class="status-row">
                <span id="compileBadge" class="status-badge edit">Idle</span>
                <span id="compileText">Compilation idle</span>
                <span id="compileSpinner" class="spinner" style="display:none" aria-hidden="true"></span>
              </div>
            </div>
          </div>

          <div class="card" style="margin-top:12px">
            <div class="section-title">Console Logs</div>
            <div id="logs" class="logs">Loading logs...</div>
          </div>
        </div>
      </div>
    </div>

    <script src="https://cdn.jsdelivr.net/npm/@modelcontextprotocol/sdk-apps/dist/index.js"></script>
    <script>
      const PROTOCOL_VERSION = '2026-01-26';
      const app = new MCPApp();

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
        lastPlayMode: null
      };

      async function updateAgentContext(description, metadata) {
        if (!state.appReady) return;
        try {
          await app.updateModelContext({
            description,
            ...metadata
          });
        } catch (error) {
          // Ignore context update failures to keep UI responsive.
        }
      }

      async function initializeApp() {
        await app.initialize({
          appInfo: { name: 'unity-dashboard', version: '1.0.0' },
          appCapabilities: { tools: {} },
          protocolVersion: PROTOCOL_VERSION
        });
        state.appReady = true;
      }

      async function callTool(name, args = {}) {
        return app.callTool(name, args);
      }

      async function readResourceJson(uri) {
        const result = await app.readResource(uri);
        const text = result && result.contents && result.contents[0] ? result.contents[0].text : '';
        if (!text) return null;
        try {
          return JSON.parse(text);
        } catch (error) {
          return null;
        }
      }

      const playBtn = document.getElementById('playBtn');
      const pauseBtn = document.getElementById('pauseBtn');
      const stepBtn = document.getElementById('stepBtn');
      const refreshBtn = document.getElementById('refreshBtn');
      const syncBtn = document.getElementById('syncBtn');
      const autoRefresh = document.getElementById('autoRefresh');
      const interval = document.getElementById('interval');
      const intervalLabel = document.getElementById('intervalLabel');
      const sceneInfo = document.getElementById('sceneInfo');
      const hierarchy = document.getElementById('hierarchy');
      const logs = document.getElementById('logs');
      const playModeBadge = document.getElementById('playModeBadge');
      const playModeText = document.getElementById('playModeText');
      const compileBadge = document.getElementById('compileBadge');
      const compileText = document.getElementById('compileText');
      const compileSpinner = document.getElementById('compileSpinner');

      playBtn.onclick = () => callTool('execute_menu_item', { menuPath: 'Edit/Play' }).catch(() => {});
      pauseBtn.onclick = () => callTool('execute_menu_item', { menuPath: 'Edit/Pause' }).catch(() => {});
      stepBtn.onclick = () => callTool('execute_menu_item', { menuPath: 'Edit/Step' }).catch(() => {});
      refreshBtn.onclick = fetchAll;
      syncBtn.onclick = () => syncToAgent('manual');

      interval.oninput = () => { intervalLabel.textContent = interval.value + 's'; };

      function extractToolData(callToolResult) {
        if (!callToolResult) return null;
        if (callToolResult.data) return callToolResult.data;
        const first = (callToolResult.content || [])[0];
        if (first && typeof first.text === 'string') {
          try { return JSON.parse(first.text); } catch { return null; }
        }
        return null;
      }

      function normalizeLogType(type) {
        const raw = String(type || '').toLowerCase();
        if (raw === 'log' || raw === 'info') return 'info';
        if (raw === 'warning') return 'warning';
        if (raw === 'error' || raw === 'exception' || raw === 'assert') return 'error';
        return 'info';
      }

      function sortLogsBySeverity(items) {
        const priority = { error: 0, warning: 1, info: 2 };
        return items.slice().sort((a, b) => priority[a.type] - priority[b.type]);
      }

      function arraysEqual(a, b) {
        if (a === b) return true;
        if (!a || !b) return false;
        if (a.length !== b.length) return false;
        for (let i = 0; i < a.length; i += 1) {
          if (a[i] !== b[i]) return false;
        }
        return true;
      }

      function updateScene(payload) {
        if (!payload) return;
        const active = payload.activeScene;
        if (!active) return;
        state.lastSceneInfo = payload;
        sceneInfo.textContent = `${active.name} - Root Count: ${active.rootCount} - ${active.isLoaded ? 'Loaded' : 'Not loaded'}`;
      }

      function detectSceneChanges(sceneNames) {
        if (!sceneNames) return;
        if (sceneNames.length === 0 && state.lastSceneNames.length === 0) {
          return;
        }
        if (!arraysEqual(sceneNames, state.lastSceneNames)) {
          const added = sceneNames.filter(name => !state.lastSceneNames.includes(name));
          const removed = state.lastSceneNames.filter(name => !sceneNames.includes(name));
          updateAgentContext(
            `Unity scene changed. Now loaded: ${sceneNames.join(', ') || 'None'}`,
            {
              sceneChange: {
                loaded: sceneNames,
                added,
                removed
              }
            }
          );
        }
        state.lastSceneNames = sceneNames;
      }

      function setActiveGameObject(entry) {
        state.activeGameObject = entry;
      }

      function highlightSelection(element) {
        const previous = hierarchy.querySelector('.node.selected');
        if (previous && previous !== element) {
          previous.classList.remove('selected');
        }
        element.classList.add('selected');
      }

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
      }

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
        element.textContent = name;

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

      function renderHierarchy(list) {
        if (!list || list.length === 0) {
          hierarchy.textContent = 'No scene data';
          return;
        }

        hierarchy.innerHTML = '';
        const container = document.createElement('div');

        list.forEach(scene => {
          const sceneNode = document.createElement('div');
          sceneNode.className = 'node scene';
          sceneNode.textContent = scene.name || 'Scene';
          container.appendChild(sceneNode);

          const rootObjects = scene.rootObjects || [];
          rootObjects.forEach(root => renderGameObjectNode(root, 1, '', container));
        });

        hierarchy.appendChild(container);
        detectSceneChanges(list.map(scene => scene.name).filter(Boolean));
      }

      function isCompilationError(message) {
        const text = String(message || '').toLowerCase();
        return text.includes('compilererror') || text.includes('.cs(') || text.includes('cs(');
      }

      function detectCompilationState(logsList, errorItems) {
        const messages = logsList.map(item => String(item.message || '').toLowerCase());
        const isCompiling = messages.some(message => message.includes('compiling'));
        const compileErrors = errorItems.filter(item => isCompilationError(item.message));
        if (isCompiling) return { state: 'compiling', errorCount: compileErrors.length };
        if (compileErrors.length > 0) return { state: 'failed', errorCount: compileErrors.length };
        if (state.lastCompilationState === 'failed' || state.lastCompilationState === 'compiling') {
          return { state: 'success', errorCount: 0 };
        }
        return { state: state.lastCompilationState || 'idle', errorCount: 0 };
      }

      function updateCompilationStatus(stateValue, errorCount) {
        const labelMap = {
          idle: 'Idle',
          compiling: 'Compiling',
          failed: 'Failed',
          success: 'Success'
        };
        const badgeClassMap = {
          idle: 'edit',
          compiling: 'compiling',
          failed: 'failed',
          success: 'success'
        };

        const badgeClass = badgeClassMap[stateValue] || 'edit';
        compileBadge.className = `status-badge ${badgeClass}`;
        compileBadge.textContent = labelMap[stateValue] || 'Idle';

        if (stateValue === 'failed') {
          compileText.textContent = `Compilation failed (${errorCount} errors)`;
        } else if (stateValue === 'success') {
          compileText.textContent = 'Compilation succeeded';
        } else if (stateValue === 'compiling') {
          compileText.textContent = 'Compiling...';
        } else {
          compileText.textContent = 'Compilation idle';
        }

        compileSpinner.style.display = stateValue === 'compiling' ? 'inline-block' : 'none';
      }

      function updateLogs(payload) {
        logs.innerHTML = '';
        const items = payload && payload.logs ? payload.logs : null;
        if (!items || items.length === 0) {
          logs.textContent = 'No logs';
          state.lastLogs = { logs: [] };
          return;
        }

        const normalized = items.map(item => ({
          message: item.message || item.text || '',
          timestamp: item.timestamp || '',
          type: normalizeLogType(item.level || item.type)
        }));

        const ordered = sortLogsBySeverity(normalized);
        const errorItems = ordered.filter(item => item.type === 'error' || item.type === 'warning');
        const newErrorCount = errorItems.length;

        if (newErrorCount > state.lastErrorCount) {
          const delta = errorItems.slice(0, newErrorCount - state.lastErrorCount);
          if (delta.length > 0) {
            updateAgentContext(
              `${delta.length} new error(s) in Unity Console: ${delta.map(item => item.message).join('; ')}`,
              {
                consoleErrors: delta.slice(0, 3).map(item => ({
                  message: item.message,
                  type: item.type,
                  timestamp: item.timestamp
                })),
                recentAction: 'console-error'
              }
            );
          }
        }

        state.lastErrorCount = newErrorCount;
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
        updateCompilationStatus(compilation.state, compilation.errorCount);

        ordered.forEach(item => {
          const el = document.createElement('div');
          el.className = 'log ' + item.type;
          el.textContent = `[${item.timestamp}] ${item.type.toUpperCase()}: ${item.message}`;
          logs.appendChild(el);
        });
      }

      function updatePlayMode(payload) {
        if (!payload) return;

        const isPlaying = Boolean(payload.isPlaying);
        const isPaused = Boolean(payload.isPaused);
        state.lastPlayMode = payload;

        const currentKey = `${isPlaying}-${isPaused}`;
        if (currentKey !== state.lastPlayModeState) {
          updateAgentContext(
            `Unity ${isPlaying ? 'entered Play mode' : 'exited Play mode'}.`,
            {
              unityState: {
                isPlaying,
                isPaused,
                timestamp: Date.now()
              }
            }
          );
          state.lastPlayModeState = currentKey;
        }

        const badgeLabel = isPlaying ? (isPaused ? 'Paused' : 'Play') : 'Edit';
        playModeBadge.textContent = badgeLabel;
        playModeBadge.className = `status-badge ${isPlaying ? 'playing' : 'edit'}`;
        playModeText.textContent = isPlaying ? (isPaused ? 'Play mode paused' : 'Play mode') : 'Edit mode';
      }

      function countGameObjects(hierarchyList) {
        let count = 0;
        function visit(node) {
          if (!node) return;
          count += 1;
          const children = node.children || [];
          children.forEach(child => visit(child));
        }

        if (Array.isArray(hierarchyList)) {
          hierarchyList.forEach(scene => {
            const roots = scene.rootObjects || [];
            roots.forEach(root => visit(root));
          });
        }
        return count;
      }

      function updateInspectorFocus(payload) {
        if (!payload || !payload.activeGameObject) {
          if (state.lastInspectorFocus !== null) {
            state.lastInspectorFocus = null;
            renderHierarchy(state.lastHierarchy || []);
          }
          return;
        }

        const focus = {
          id: payload.activeGameObject.instanceId,
          name: payload.activeGameObject.name,
          path: payload.activeGameObject.path,
          component: payload.focusedComponent || null
        };

        const previous = state.lastInspectorFocus;
        const isSame = previous && previous.id === focus.id && previous.component === focus.component;

        if (!isSame) {
          updateAgentContext(
            `User is inspecting ${focus.component || 'GameObject'} on ${focus.name}`,
            {
              inspectorFocus: {
                gameObject: { id: focus.id, name: focus.name, path: focus.path },
                component: focus.component,
                timestamp: Date.now()
              }
            }
          );
          state.lastInspectorFocus = focus;
          renderHierarchy(state.lastHierarchy || []);
        }
      }

      async function fetchInspectorState() {
        try {
          const payload = await readResourceJson('unity://inspector_state');
          updateInspectorFocus(payload);
        } catch (error) {
          // Ignore inspector polling errors so the rest of the dashboard still works.
        }
      }

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
              rootObjectCount: activeScene.rootCount || 0
            },
            activeGameObject: state.activeGameObject || null,
            playMode: { isPlaying, isPaused },
            topErrors,
            inspectorFocus,
            syncTimestamp: Date.now(),
            syncSource: source
          }
        );
      }

      async function fetchAll() {
        try {
          const [sceneResult, hierarchyResult, logsResult, playModeResult] = await Promise.all([
            callTool('get_scene_info', {}),
            callTool('get_scenes_hierarchy', {}),
            callTool('get_console_logs', { limit: 200, includeStackTrace: false }),
            callTool('get_play_mode_status', {})
          ]);

          const sceneData = extractToolData(sceneResult);
          const hierarchyData = extractToolData(hierarchyResult);
          const logsData = extractToolData(logsResult);
          const playModeData = extractToolData(playModeResult);

          updateScene(sceneData);

          const hierarchyList = hierarchyData && hierarchyData.hierarchy ? hierarchyData.hierarchy : hierarchyData;
          state.lastHierarchy = hierarchyList || [];
          renderHierarchy(state.lastHierarchy);

          updateLogs(logsData);
          updatePlayMode(playModeData);
          await fetchInspectorState();
        } catch (error) {
          // Keep UI usable even if one call fails.
        }
      }

      let timer = null;
      function scheduleAuto() {
        if (timer) clearInterval(timer);
        if (autoRefresh.checked) {
          timer = setInterval(fetchAll, Number(interval.value) * 1000);
        }
      }
      autoRefresh.onchange = scheduleAuto;
      interval.onchange = scheduleAuto;

      (async () => {
        try {
          await initializeApp();
        } catch (error) {
          // If init fails, the host likely doesn't support MCP Apps.
        }
        await fetchAll();
        await syncToAgent('auto');
      })();
    </script>
  </body>
</html>
```

- [ ] Open Unity once to generate the new .meta file for the inspector resource and include it in source control.

##### Step 2 Verification Checklist
- [ ] No build errors
- [ ] Open the dashboard and select a GameObject in the Unity Inspector.
- [ ] Confirm the focused GameObject is highlighted in the dashboard hierarchy.
- [ ] Ask the agent to modify the focused component and confirm it targets the correct GameObject.

#### Step 2 STOP & COMMIT
**STOP & COMMIT:** Agent must stop here and wait for the user to test, stage, and commit the change.
