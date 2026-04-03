# GameObject Inspector Pane

## Goal
Add an interactive GameObject inspector pane to the Unity dashboard that lets users view and edit GameObject properties, transform values, and component fields from within the MCP app UI.

## Prerequisites
Make sure that the user is currently on the feature/gameobject-inspector-pane branch before beginning implementation.
If not, move them to the correct branch. If the branch does not exist, create it from main.

## Tech Stack and Dependencies
- Unity Editor package (C#) + Node.js MCP server (TypeScript, ESM) with an HTML/JS/CSS MCP App UI.
- UI entry point is [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html).
- Build command: `cd Server~` then `npm run build`.
- Tooling used in the UI: `get_gameobject`, `update_gameobject`, `set_transform`, `update_component`, `select_gameobject`, `delete_gameobject`, `duplicate_gameobject`.

## Step-by-Step Instructions

#### Step 1: Basic Inspector Pane With GameObject Properties
- [ ] In [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html), replace the `.grid{...}` rule with the following block:

```css
      .grid{display:grid;grid-template-columns:minmax(0,1.1fr) minmax(0,0.9fr);gap:12px}
      @media (max-width: 1000px){.grid{grid-template-columns:1fr}}
```

- [ ] In the same `<style>` block, add the following inspector styles before the closing `</style>` tag:

```css
      .inspector-card{height:100%}
      .inspector{display:flex;flex-direction:column;gap:8px;max-height:800px;overflow:auto}
      .inspector-content{display:flex;flex-direction:column;gap:12px}
      .inspector-section{border:1px solid #122432;border-radius:8px;padding:10px;background:#0a1624}
      .inspector-header{font-size:12px;color:#9fb1c9;margin-bottom:8px;text-transform:uppercase;letter-spacing:0.08em}
      .inspector-row{display:flex;justify-content:space-between;gap:8px;font-size:12px;color:#cfe6f8}
      .inspector-row span{color:#9fb1c9}
      .inspector-field{display:flex;align-items:center;gap:8px;margin-top:6px}
      .inspector-field label{flex:0 0 80px;color:#9fb1c9;font-size:12px}
      .inspector-field input[type="text"],
      .inspector-field input[type="number"]{flex:1;background:#0f1c2e;border:1px solid #1a3a5a;border-radius:6px;color:#e6eef8;padding:6px;font-size:12px}
      .inspector-field input[type="checkbox"]{transform:scale(1.1)}
      .inspector-status{font-size:12px;padding:6px 8px;border-radius:6px;border:1px solid #1a3a5a;background:#0f1c2e;color:#cfe6f8}
      .inspector-status.hidden{display:none}
      .inspector-status.success{border-color:#22c55e;color:#bbf7d0}
      .inspector-status.error{border-color:#b91c1c;color:#fecaca}
      .inspector-status.pending{border-color:#a16207;color:#fde68a}
```

- [ ] Replace the existing `<div class="grid">...</div>` markup with the following:

```html
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
          <div class="card inspector-card">
            <div class="section-title">Inspector</div>
            <div id="inspectorPane" class="inspector">
              <div id="inspectorStatus" class="inspector-status hidden"></div>
              <div id="inspectorContent" class="inspector-content">
                <div class="meta">No GameObject selected.</div>
              </div>
            </div>
          </div>
        </div>
      </div>
```

- [ ] Replace the `const state = { ... }` block with the following:

```javascript
      const state = {
        appReady: false,
        activeGameObject: null,
        activeGameObjectDetails: null,
        inspectorBusy: false,
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
```

- [ ] After the existing DOM references (`logsContent`, `debugToggle`, `debugPanel`, `debugClose`), add the following new references:

```javascript
      const inspectorPane = document.getElementById('inspectorPane');
      const inspectorContent = document.getElementById('inspectorContent');
      const inspectorStatus = document.getElementById('inspectorStatus');
```

- [ ] Replace the `handleSelection` function with the following:

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
        fetchGameObjectDetails(entry);
      }
```

- [ ] Insert the following inspector helpers immediately after `handleSelection`:

```javascript
      function setInspectorStatus(type, message) {
        if (!inspectorStatus) return;
        inspectorStatus.textContent = message || '';
        inspectorStatus.className = 'inspector-status' + (type ? ' ' + type : '');
        if (!message) inspectorStatus.classList.add('hidden');
        else inspectorStatus.classList.remove('hidden');
      }

      function setInspectorBusy(isBusy) {
        state.inspectorBusy = isBusy;
        if (!inspectorPane) return;
        inspectorPane.style.opacity = isBusy ? '0.7' : '1';
        inspectorPane.style.pointerEvents = isBusy ? 'none' : 'auto';
      }

      function renderInspectorPlaceholder(message) {
        if (!inspectorContent) return;
        inspectorContent.innerHTML = `<div class="meta">${message}</div>`;
      }

      async function fetchGameObjectDetails(entry, options = {}) {
        if (!entry) {
          state.activeGameObjectDetails = null;
          renderInspector();
          return;
        }

        if (!options.silent) {
          setInspectorStatus('pending', 'Loading GameObject details...');
        }

        const result = await safeCallTool('get_gameobject', { idOrName: String(entry.id) });
        const data = extractToolData(result);
        const gameObject = data && data.gameObject ? data.gameObject : null;

        if (!gameObject) {
          state.activeGameObjectDetails = null;
          setInspectorStatus('error', 'Failed to load GameObject details.');
          renderInspector();
          return;
        }

        state.activeGameObjectDetails = gameObject;
        setInspectorStatus('success', 'GameObject details loaded.');
        renderInspector();
      }

      function buildGameObjectUpdateData(field, value) {
        const data = { [field]: value };
        if (field === 'activeSelf') {
          data.isActiveSelf = value;
        }
        return data;
      }

      async function updateGameObjectProperty(field, value) {
        if (!state.activeGameObject) return;

        setInspectorBusy(true);
        setInspectorStatus('pending', 'Updating GameObject...');

        const result = await safeCallTool('update_gameobject', {
          instanceId: state.activeGameObject.id,
          gameObjectData: buildGameObjectUpdateData(field, value)
        });

        if (!result) {
          setInspectorStatus('error', 'Update failed.');
          setInspectorBusy(false);
          return;
        }

        setInspectorStatus('success', 'Update complete.');
        await fetchGameObjectDetails(state.activeGameObject, { silent: true });
        await fetchAll();
        setInspectorBusy(false);
      }

      function renderInspector() {
        if (!inspectorContent) return;

        if (!state.activeGameObject) {
          renderInspectorPlaceholder('No GameObject selected.');
          return;
        }

        if (!state.activeGameObjectDetails) {
          renderInspectorPlaceholder('Loading GameObject details...');
          return;
        }

        const go = state.activeGameObjectDetails;
        const isStatic = Boolean(go.isStatic);

        inspectorContent.innerHTML = `
          <div class="inspector-section">
            <div class="inspector-header">GameObject</div>
            <div class="inspector-row"><span>ID</span><div>${state.activeGameObject.id}</div></div>
            <div class="inspector-row"><span>Path</span><div>${state.activeGameObject.path}</div></div>
            <div class="inspector-row"><span>Active</span><div>${go.activeSelf ? 'Yes' : 'No'}</div></div>
            <div class="inspector-row"><span>Layer</span><div>${go.layer} (${go.layerName || 'Unknown'})</div></div>
          </div>

          <div class="inspector-section">
            <div class="inspector-header">Properties</div>
            <div class="inspector-field">
              <label for="goName">Name</label>
              <input id="goName" type="text" value="${go.name || ''}" />
            </div>
            <div class="inspector-field">
              <label for="goTag">Tag</label>
              <input id="goTag" type="text" value="${go.tag || ''}" />
            </div>
            <div class="inspector-field">
              <label for="goLayer">Layer</label>
              <input id="goLayer" type="number" min="0" max="31" value="${Number.isFinite(go.layer) ? go.layer : 0}" />
            </div>
            <div class="inspector-field">
              <label for="goActive">Active</label>
              <input id="goActive" type="checkbox" ${go.activeSelf ? 'checked' : ''} />
            </div>
            <div class="inspector-field">
              <label for="goStatic">Static</label>
              <input id="goStatic" type="checkbox" ${isStatic ? 'checked' : ''} />
            </div>
          </div>
        `;

        const nameInput = inspectorContent.querySelector('#goName');
        const tagInput = inspectorContent.querySelector('#goTag');
        const layerInput = inspectorContent.querySelector('#goLayer');
        const activeInput = inspectorContent.querySelector('#goActive');
        const staticInput = inspectorContent.querySelector('#goStatic');

        nameInput.addEventListener('change', () => updateGameObjectProperty('name', nameInput.value.trim()));
        tagInput.addEventListener('change', () => updateGameObjectProperty('tag', tagInput.value.trim()));
        layerInput.addEventListener('change', () => updateGameObjectProperty('layer', Number(layerInput.value)));
        activeInput.addEventListener('change', () => updateGameObjectProperty('activeSelf', activeInput.checked));
        staticInput.addEventListener('change', () => updateGameObjectProperty('isStatic', staticInput.checked));
      }
```

- [ ] In `fetchAll`, add a call to `renderInspector()` after `await fetchInspectorState();`:

```javascript
        await fetchInspectorState();
        renderInspector();
```

##### Step 1 Verification Checklist
- [ ] Run `cd Server~` then `npm run build`.
- [ ] Open the Unity dashboard app and select a GameObject in the hierarchy; the Inspector pane appears with basic properties.
- [ ] Edit Name, Tag, and Layer and confirm the GameObject updates in Unity after refresh.
- [ ] Toggle Active and confirm the GameObject enables or disables in Unity.

#### Step 1 STOP & COMMIT
**STOP & COMMIT:** Agent must stop here and wait for the user to test, stage, and commit the change.

#### Step 2: Transform Editing UI
- [ ] Append the following CSS to the end of the `<style>` block in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html):

```css
      .transform-grid{display:grid;grid-template-columns:repeat(3,1fr);gap:6px}
      .transform-row{display:flex;align-items:center;gap:8px;margin-top:6px}
      .transform-row label{flex:0 0 100px;color:#9fb1c9;font-size:12px}
      .transform-row input{flex:1;background:#0f1c2e;border:1px solid #1a3a5a;border-radius:6px;color:#e6eef8;padding:6px;font-size:12px}
      .transform-unit{font-size:11px;color:#9fb1c9;margin-left:4px}
      .transform-actions{display:flex;gap:6px;flex-wrap:wrap;margin-top:8px}
      .transform-actions button{font-size:11px;height:28px;padding:0 8px}
      .toggle-group{display:inline-flex;border:1px solid #1a3a5a;border-radius:6px;overflow:hidden}
      .toggle-group button{border:none;border-right:1px solid #1a3a5a;background:#0f1c2e;color:#9fb1c9;height:28px;padding:0 10px}
      .toggle-group button.active{background:#1b3b5a;color:#fff}
      .toggle-group button:last-child{border-right:none}
```

- [ ] Add `transformSpace: 'world'` to the `state` block:

```javascript
        transformSpace: 'world',
```

- [ ] Insert the following helpers after `buildGameObjectUpdateData`:

```javascript
      function getTransformComponent(go) {
        if (!go || !Array.isArray(go.components)) return null;
        return go.components.find(component => component.type === 'Transform') || null;
      }

      function normalizeVector3(value, fallback = { x: 0, y: 0, z: 0 }) {
        if (!value || typeof value !== 'object') return { ...fallback };
        return {
          x: Number(value.x ?? fallback.x ?? 0),
          y: Number(value.y ?? fallback.y ?? 0),
          z: Number(value.z ?? fallback.z ?? 0)
        };
      }

      function getTransformVectors(transform) {
        const props = transform && transform.properties ? transform.properties : {};
        const isLocal = state.transformSpace === 'local';
        const position = isLocal ? props.localPosition : props.position;
        const rotation = isLocal ? props.localEulerAngles : props.eulerAngles;
        const scale = props.localScale;

        return {
          position: normalizeVector3(position),
          rotation: normalizeVector3(rotation),
          scale: normalizeVector3(scale, { x: 1, y: 1, z: 1 })
        };
      }

      function setTransformSpace(space) {
        if (space !== 'world' && space !== 'local') return;
        state.transformSpace = space;
        renderInspector();
      }

      async function updateTransformField(field, vector) {
        if (!state.activeGameObject) return;

        setInspectorBusy(true);
        setInspectorStatus('pending', 'Updating transform...');

        const result = await safeCallTool('set_transform', {
          instanceId: state.activeGameObject.id,
          [field]: vector,
          space: state.transformSpace
        });

        if (!result) {
          setInspectorStatus('error', 'Transform update failed.');
          setInspectorBusy(false);
          return;
        }

        setInspectorStatus('success', 'Transform updated.');
        await fetchGameObjectDetails(state.activeGameObject, { silent: true });
        setInspectorBusy(false);
      }
```

- [ ] Replace the existing `renderInspector` function with the following version:

```javascript
      function renderInspector() {
        if (!inspectorContent) return;

        if (!state.activeGameObject) {
          renderInspectorPlaceholder('No GameObject selected.');
          return;
        }

        if (!state.activeGameObjectDetails) {
          renderInspectorPlaceholder('Loading GameObject details...');
          return;
        }

        const go = state.activeGameObjectDetails;
        const isStatic = Boolean(go.isStatic);
        const transform = getTransformComponent(go);
        const vectors = getTransformVectors(transform);

        inspectorContent.innerHTML = `
          <div class="inspector-section">
            <div class="inspector-header">GameObject</div>
            <div class="inspector-row"><span>ID</span><div>${state.activeGameObject.id}</div></div>
            <div class="inspector-row"><span>Path</span><div>${state.activeGameObject.path}</div></div>
            <div class="inspector-row"><span>Active</span><div>${go.activeSelf ? 'Yes' : 'No'}</div></div>
            <div class="inspector-row"><span>Layer</span><div>${go.layer} (${go.layerName || 'Unknown'})</div></div>
          </div>

          <div class="inspector-section">
            <div class="inspector-header">Properties</div>
            <div class="inspector-field">
              <label for="goName">Name</label>
              <input id="goName" type="text" value="${go.name || ''}" />
            </div>
            <div class="inspector-field">
              <label for="goTag">Tag</label>
              <input id="goTag" type="text" value="${go.tag || ''}" />
            </div>
            <div class="inspector-field">
              <label for="goLayer">Layer</label>
              <input id="goLayer" type="number" min="0" max="31" value="${Number.isFinite(go.layer) ? go.layer : 0}" />
            </div>
            <div class="inspector-field">
              <label for="goActive">Active</label>
              <input id="goActive" type="checkbox" ${go.activeSelf ? 'checked' : ''} />
            </div>
            <div class="inspector-field">
              <label for="goStatic">Static</label>
              <input id="goStatic" type="checkbox" ${isStatic ? 'checked' : ''} />
            </div>
          </div>

          <div class="inspector-section">
            <div class="inspector-header">Transform</div>
            <div class="transform-row">
              <label>Space</label>
              <div class="toggle-group">
                <button type="button" id="transformWorld" class="${state.transformSpace === 'world' ? 'active' : ''}">World</button>
                <button type="button" id="transformLocal" class="${state.transformSpace === 'local' ? 'active' : ''}">Local</button>
              </div>
            </div>
            <div class="transform-row">
              <label>Position <span class="transform-unit">units</span></label>
              <div class="transform-grid">
                <input id="posX" type="number" step="0.1" value="${vectors.position.x}" />
                <input id="posY" type="number" step="0.1" value="${vectors.position.y}" />
                <input id="posZ" type="number" step="0.1" value="${vectors.position.z}" />
              </div>
            </div>
            <div class="transform-row">
              <label>Rotation <span class="transform-unit">deg</span></label>
              <div class="transform-grid">
                <input id="rotX" type="number" step="15" value="${vectors.rotation.x}" />
                <input id="rotY" type="number" step="15" value="${vectors.rotation.y}" />
                <input id="rotZ" type="number" step="15" value="${vectors.rotation.z}" />
              </div>
            </div>
            <div class="transform-row">
              <label>Scale</label>
              <div class="transform-grid">
                <input id="scaleX" type="number" step="0.1" value="${vectors.scale.x}" />
                <input id="scaleY" type="number" step="0.1" value="${vectors.scale.y}" />
                <input id="scaleZ" type="number" step="0.1" value="${vectors.scale.z}" />
              </div>
            </div>
            <div class="transform-actions">
              <button type="button" id="resetPosition">Reset Position</button>
              <button type="button" id="resetRotation">Reset Rotation</button>
              <button type="button" id="resetScale">Reset Scale</button>
            </div>
          </div>
        `;

        const nameInput = inspectorContent.querySelector('#goName');
        const tagInput = inspectorContent.querySelector('#goTag');
        const layerInput = inspectorContent.querySelector('#goLayer');
        const activeInput = inspectorContent.querySelector('#goActive');
        const staticInput = inspectorContent.querySelector('#goStatic');
        const posX = inspectorContent.querySelector('#posX');
        const posY = inspectorContent.querySelector('#posY');
        const posZ = inspectorContent.querySelector('#posZ');
        const rotX = inspectorContent.querySelector('#rotX');
        const rotY = inspectorContent.querySelector('#rotY');
        const rotZ = inspectorContent.querySelector('#rotZ');
        const scaleX = inspectorContent.querySelector('#scaleX');
        const scaleY = inspectorContent.querySelector('#scaleY');
        const scaleZ = inspectorContent.querySelector('#scaleZ');
        const worldBtn = inspectorContent.querySelector('#transformWorld');
        const localBtn = inspectorContent.querySelector('#transformLocal');
        const resetPosition = inspectorContent.querySelector('#resetPosition');
        const resetRotation = inspectorContent.querySelector('#resetRotation');
        const resetScale = inspectorContent.querySelector('#resetScale');

        nameInput.addEventListener('change', () => updateGameObjectProperty('name', nameInput.value.trim()));
        tagInput.addEventListener('change', () => updateGameObjectProperty('tag', tagInput.value.trim()));
        layerInput.addEventListener('change', () => updateGameObjectProperty('layer', Number(layerInput.value)));
        activeInput.addEventListener('change', () => updateGameObjectProperty('activeSelf', activeInput.checked));
        staticInput.addEventListener('change', () => updateGameObjectProperty('isStatic', staticInput.checked));

        worldBtn.addEventListener('click', () => setTransformSpace('world'));
        localBtn.addEventListener('click', () => setTransformSpace('local'));

        const readPosition = () => ({
          x: Number(posX.value),
          y: Number(posY.value),
          z: Number(posZ.value)
        });
        const readRotation = () => ({
          x: Number(rotX.value),
          y: Number(rotY.value),
          z: Number(rotZ.value)
        });
        const readScale = () => ({
          x: Number(scaleX.value),
          y: Number(scaleY.value),
          z: Number(scaleZ.value)
        });

        posX.addEventListener('change', () => updateTransformField('position', readPosition()));
        posY.addEventListener('change', () => updateTransformField('position', readPosition()));
        posZ.addEventListener('change', () => updateTransformField('position', readPosition()));
        rotX.addEventListener('change', () => updateTransformField('rotation', readRotation()));
        rotY.addEventListener('change', () => updateTransformField('rotation', readRotation()));
        rotZ.addEventListener('change', () => updateTransformField('rotation', readRotation()));
        scaleX.addEventListener('change', () => updateTransformField('scale', readScale()));
        scaleY.addEventListener('change', () => updateTransformField('scale', readScale()));
        scaleZ.addEventListener('change', () => updateTransformField('scale', readScale()));

        resetPosition.addEventListener('click', () => {
          posX.value = 0;
          posY.value = 0;
          posZ.value = 0;
          updateTransformField('position', readPosition());
        });
        resetRotation.addEventListener('click', () => {
          rotX.value = 0;
          rotY.value = 0;
          rotZ.value = 0;
          updateTransformField('rotation', readRotation());
        });
        resetScale.addEventListener('click', () => {
          scaleX.value = 1;
          scaleY.value = 1;
          scaleZ.value = 1;
          updateTransformField('scale', readScale());
        });
      }
```

##### Step 2 Verification Checklist
- [ ] Run `cd Server~` then `npm run build`.
- [ ] Select a GameObject and confirm the Transform section shows position, rotation, and scale values.
- [ ] Edit Position X and confirm the object moves along X in Unity.
- [ ] Toggle World or Local space and confirm the values update accordingly.
- [ ] Click Reset buttons and confirm the transform resets to defaults.

#### Step 2 STOP & COMMIT
**STOP & COMMIT:** Agent must stop here and wait for the user to test, stage, and commit the change.

#### Step 3: Component List and Component Field Editing
- [ ] Append the following CSS to the end of the `<style>` block in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html):

```css
      .components-list{display:flex;flex-direction:column;gap:8px}
      .component-card{border:1px solid #1a3a5a;border-radius:6px;background:#0f1c2e}
      .component-card summary{cursor:pointer;padding:6px 8px;font-size:12px;background:#3c3c3c;color:#d2d2d2}
      .component-card[open] summary{background:#2b2b2b}
      .component-body{padding:8px}
      .component-field{display:flex;align-items:center;gap:8px;margin-top:6px}
      .component-field label{flex:0 0 130px;color:#9fb1c9;font-size:12px}
      .component-field input[type="text"],
      .component-field input[type="number"]{flex:1;background:#0f1c2e;border:1px solid #1a3a5a;border-radius:6px;color:#e6eef8;padding:6px;font-size:12px}
      .component-field input[type="checkbox"]{transform:scale(1.1)}
      .component-field input[type="color"]{width:40px;height:28px;border:none;background:transparent}
      .component-field .vector3{display:grid;grid-template-columns:repeat(3,1fr);gap:6px;flex:1}
      .component-field .readonly{color:#9fb1c9}
```

- [ ] Insert the following component helpers after `updateTransformField`:

```javascript
      function isVector3(value) {
        return value && typeof value === 'object'
          && Number.isFinite(value.x)
          && Number.isFinite(value.y)
          && Number.isFinite(value.z);
      }

      function isColor(value) {
        return value && typeof value === 'object'
          && Number.isFinite(value.r)
          && Number.isFinite(value.g)
          && Number.isFinite(value.b);
      }

      function colorToHex(color) {
        const clamp = (v) => Math.max(0, Math.min(255, Math.round(v * 255)));
        const toHex = (v) => clamp(v).toString(16).padStart(2, '0');
        return `#${toHex(color.r)}${toHex(color.g)}${toHex(color.b)}`;
      }

      function hexToColor(hex, alpha) {
        const clean = String(hex || '').replace('#', '');
        if (clean.length !== 6) return { r: 1, g: 1, b: 1, a: alpha ?? 1 };
        const intVal = Number.parseInt(clean, 16);
        const r = (intVal >> 16) & 255;
        const g = (intVal >> 8) & 255;
        const b = intVal & 255;
        return { r: r / 255, g: g / 255, b: b / 255, a: alpha ?? 1 };
      }

      function getComponentDisplayFields(component) {
        if (!component || !component.properties) return [];
        const entries = Object.entries(component.properties)
          .filter(([key]) => key !== '_skipped');

        if (component.type === 'Transform') return [];

        if (component.type === 'Camera') {
          const allowed = ['fieldOfView', 'clearFlags', 'cullingMask', 'nearClipPlane', 'farClipPlane', 'orthographic', 'orthographicSize', 'backgroundColor'];
          return entries.filter(([key]) => allowed.includes(key));
        }

        if (component.type === 'Light') {
          const allowed = ['type', 'color', 'intensity', 'range', 'spotAngle'];
          return entries.filter(([key]) => allowed.includes(key));
        }

        if (component.type === 'Renderer') {
          const allowed = ['enabled', 'shadowCastingMode', 'receiveShadows', 'sortingLayerName', 'sortingOrder'];
          return entries.filter(([key]) => allowed.includes(key));
        }

        return entries;
      }

      async function updateComponentField(componentName, fieldName, value) {
        if (!state.activeGameObject) return;

        setInspectorBusy(true);
        setInspectorStatus('pending', `Updating ${componentName}...`);

        const result = await safeCallTool('update_component', {
          instanceId: state.activeGameObject.id,
          componentName,
          componentData: { [fieldName]: value }
        });

        if (!result) {
          setInspectorStatus('error', `${componentName} update failed.`);
          setInspectorBusy(false);
          return;
        }

        setInspectorStatus('success', `${componentName} updated.`);
        await fetchGameObjectDetails(state.activeGameObject, { silent: true });
        setInspectorBusy(false);
      }

      function renderComponents(go) {
        const components = Array.isArray(go.components) ? go.components : [];
        if (components.length === 0) return '<div class="meta">No components.</div>';

        return components.map((component) => {
          if (component.type === 'Transform') {
            return `
              <details class="component-card">
                <summary>Transform</summary>
                <div class="component-body">
                  <div class="meta">Transform editing is handled above.</div>
                </div>
              </details>
            `;
          }

          const fields = getComponentDisplayFields(component);
          if (fields.length === 0) {
            return `
              <details class="component-card">
                <summary>${component.type}</summary>
                <div class="component-body">
                  <div class="meta">No editable fields detected.</div>
                </div>
              </details>
            `;
          }

          const fieldHtml = fields.map(([key, value]) => {
            if (typeof value === 'boolean') {
              return `
                <div class="component-field">
                  <label>${key}</label>
                  <input type="checkbox" data-component="${component.type}" data-field="${key}" ${value ? 'checked' : ''} />
                </div>
              `;
            }

            if (Number.isFinite(value)) {
              return `
                <div class="component-field">
                  <label>${key}</label>
                  <input type="number" data-component="${component.type}" data-field="${key}" value="${value}" />
                </div>
              `;
            }

            if (typeof value === 'string') {
              return `
                <div class="component-field">
                  <label>${key}</label>
                  <input type="text" data-component="${component.type}" data-field="${key}" value="${value}" />
                </div>
              `;
            }

            if (isVector3(value)) {
              return `
                <div class="component-field">
                  <label>${key}</label>
                  <div class="vector3">
                    <input type="number" data-component="${component.type}" data-field="${key}" data-axis="x" value="${value.x}" />
                    <input type="number" data-component="${component.type}" data-field="${key}" data-axis="y" value="${value.y}" />
                    <input type="number" data-component="${component.type}" data-field="${key}" data-axis="z" value="${value.z}" />
                  </div>
                </div>
              `;
            }

            if (isColor(value)) {
              return `
                <div class="component-field">
                  <label>${key}</label>
                  <input type="color" data-component="${component.type}" data-field="${key}" data-alpha="${value.a ?? 1}" value="${colorToHex(value)}" />
                </div>
              `;
            }

            return `
              <div class="component-field">
                <label>${key}</label>
                <div class="readonly">Unsupported type</div>
              </div>
            `;
          }).join('');

          return `
            <details class="component-card">
              <summary>${component.type}</summary>
              <div class="component-body">${fieldHtml}</div>
            </details>
          `;
        }).join('');
      }

      function bindComponentEditors() {
        const inputs = inspectorContent.querySelectorAll('[data-component][data-field]');
        inputs.forEach((input) => {
          const componentName = input.dataset.component;
          const fieldName = input.dataset.field;
          const axis = input.dataset.axis;

          if (input.type === 'checkbox') {
            input.addEventListener('change', () => {
              updateComponentField(componentName, fieldName, input.checked);
            });
            return;
          }

          if (input.type === 'color') {
            input.addEventListener('change', () => {
              const alpha = Number(input.dataset.alpha ?? 1);
              updateComponentField(componentName, fieldName, hexToColor(input.value, alpha));
            });
            return;
          }

          if (axis) {
            input.addEventListener('change', () => {
              const group = inspectorContent.querySelectorAll(
                `input[data-component="${componentName}"][data-field="${fieldName}"][data-axis]`
              );
              const vec = { x: 0, y: 0, z: 0 };
              group.forEach((item) => {
                vec[item.dataset.axis] = Number(item.value);
              });
              updateComponentField(componentName, fieldName, vec);
            });
            return;
          }

          input.addEventListener('change', () => {
            const value = input.type === 'number' ? Number(input.value) : input.value;
            updateComponentField(componentName, fieldName, value);
          });
        });
      }
```

- [ ] Replace the existing `renderInspector` function with the following version:

```javascript
      function renderInspector() {
        if (!inspectorContent) return;

        if (!state.activeGameObject) {
          renderInspectorPlaceholder('No GameObject selected.');
          return;
        }

        if (!state.activeGameObjectDetails) {
          renderInspectorPlaceholder('Loading GameObject details...');
          return;
        }

        const go = state.activeGameObjectDetails;
        const isStatic = Boolean(go.isStatic);
        const transform = getTransformComponent(go);
        const vectors = getTransformVectors(transform);

        inspectorContent.innerHTML = `
          <div class="inspector-section">
            <div class="inspector-header">GameObject</div>
            <div class="inspector-row"><span>ID</span><div>${state.activeGameObject.id}</div></div>
            <div class="inspector-row"><span>Path</span><div>${state.activeGameObject.path}</div></div>
            <div class="inspector-row"><span>Active</span><div>${go.activeSelf ? 'Yes' : 'No'}</div></div>
            <div class="inspector-row"><span>Layer</span><div>${go.layer} (${go.layerName || 'Unknown'})</div></div>
          </div>

          <div class="inspector-section">
            <div class="inspector-header">Properties</div>
            <div class="inspector-field">
              <label for="goName">Name</label>
              <input id="goName" type="text" value="${go.name || ''}" />
            </div>
            <div class="inspector-field">
              <label for="goTag">Tag</label>
              <input id="goTag" type="text" value="${go.tag || ''}" />
            </div>
            <div class="inspector-field">
              <label for="goLayer">Layer</label>
              <input id="goLayer" type="number" min="0" max="31" value="${Number.isFinite(go.layer) ? go.layer : 0}" />
            </div>
            <div class="inspector-field">
              <label for="goActive">Active</label>
              <input id="goActive" type="checkbox" ${go.activeSelf ? 'checked' : ''} />
            </div>
            <div class="inspector-field">
              <label for="goStatic">Static</label>
              <input id="goStatic" type="checkbox" ${isStatic ? 'checked' : ''} />
            </div>
          </div>

          <div class="inspector-section">
            <div class="inspector-header">Transform</div>
            <div class="transform-row">
              <label>Space</label>
              <div class="toggle-group">
                <button type="button" id="transformWorld" class="${state.transformSpace === 'world' ? 'active' : ''}">World</button>
                <button type="button" id="transformLocal" class="${state.transformSpace === 'local' ? 'active' : ''}">Local</button>
              </div>
            </div>
            <div class="transform-row">
              <label>Position <span class="transform-unit">units</span></label>
              <div class="transform-grid">
                <input id="posX" type="number" step="0.1" value="${vectors.position.x}" />
                <input id="posY" type="number" step="0.1" value="${vectors.position.y}" />
                <input id="posZ" type="number" step="0.1" value="${vectors.position.z}" />
              </div>
            </div>
            <div class="transform-row">
              <label>Rotation <span class="transform-unit">deg</span></label>
              <div class="transform-grid">
                <input id="rotX" type="number" step="15" value="${vectors.rotation.x}" />
                <input id="rotY" type="number" step="15" value="${vectors.rotation.y}" />
                <input id="rotZ" type="number" step="15" value="${vectors.rotation.z}" />
              </div>
            </div>
            <div class="transform-row">
              <label>Scale</label>
              <div class="transform-grid">
                <input id="scaleX" type="number" step="0.1" value="${vectors.scale.x}" />
                <input id="scaleY" type="number" step="0.1" value="${vectors.scale.y}" />
                <input id="scaleZ" type="number" step="0.1" value="${vectors.scale.z}" />
              </div>
            </div>
            <div class="transform-actions">
              <button type="button" id="resetPosition">Reset Position</button>
              <button type="button" id="resetRotation">Reset Rotation</button>
              <button type="button" id="resetScale">Reset Scale</button>
            </div>
          </div>

          <div class="inspector-section">
            <div class="inspector-header">Components</div>
            <div id="componentsList" class="components-list">${renderComponents(go)}</div>
          </div>
        `;

        const nameInput = inspectorContent.querySelector('#goName');
        const tagInput = inspectorContent.querySelector('#goTag');
        const layerInput = inspectorContent.querySelector('#goLayer');
        const activeInput = inspectorContent.querySelector('#goActive');
        const staticInput = inspectorContent.querySelector('#goStatic');
        const posX = inspectorContent.querySelector('#posX');
        const posY = inspectorContent.querySelector('#posY');
        const posZ = inspectorContent.querySelector('#posZ');
        const rotX = inspectorContent.querySelector('#rotX');
        const rotY = inspectorContent.querySelector('#rotY');
        const rotZ = inspectorContent.querySelector('#rotZ');
        const scaleX = inspectorContent.querySelector('#scaleX');
        const scaleY = inspectorContent.querySelector('#scaleY');
        const scaleZ = inspectorContent.querySelector('#scaleZ');
        const worldBtn = inspectorContent.querySelector('#transformWorld');
        const localBtn = inspectorContent.querySelector('#transformLocal');
        const resetPosition = inspectorContent.querySelector('#resetPosition');
        const resetRotation = inspectorContent.querySelector('#resetRotation');
        const resetScale = inspectorContent.querySelector('#resetScale');

        nameInput.addEventListener('change', () => updateGameObjectProperty('name', nameInput.value.trim()));
        tagInput.addEventListener('change', () => updateGameObjectProperty('tag', tagInput.value.trim()));
        layerInput.addEventListener('change', () => updateGameObjectProperty('layer', Number(layerInput.value)));
        activeInput.addEventListener('change', () => updateGameObjectProperty('activeSelf', activeInput.checked));
        staticInput.addEventListener('change', () => updateGameObjectProperty('isStatic', staticInput.checked));

        worldBtn.addEventListener('click', () => setTransformSpace('world'));
        localBtn.addEventListener('click', () => setTransformSpace('local'));

        const readPosition = () => ({
          x: Number(posX.value),
          y: Number(posY.value),
          z: Number(posZ.value)
        });
        const readRotation = () => ({
          x: Number(rotX.value),
          y: Number(rotY.value),
          z: Number(rotZ.value)
        });
        const readScale = () => ({
          x: Number(scaleX.value),
          y: Number(scaleY.value),
          z: Number(scaleZ.value)
        });

        posX.addEventListener('change', () => updateTransformField('position', readPosition()));
        posY.addEventListener('change', () => updateTransformField('position', readPosition()));
        posZ.addEventListener('change', () => updateTransformField('position', readPosition()));
        rotX.addEventListener('change', () => updateTransformField('rotation', readRotation()));
        rotY.addEventListener('change', () => updateTransformField('rotation', readRotation()));
        rotZ.addEventListener('change', () => updateTransformField('rotation', readRotation()));
        scaleX.addEventListener('change', () => updateTransformField('scale', readScale()));
        scaleY.addEventListener('change', () => updateTransformField('scale', readScale()));
        scaleZ.addEventListener('change', () => updateTransformField('scale', readScale()));

        resetPosition.addEventListener('click', () => {
          posX.value = 0;
          posY.value = 0;
          posZ.value = 0;
          updateTransformField('position', readPosition());
        });
        resetRotation.addEventListener('click', () => {
          rotX.value = 0;
          rotY.value = 0;
          rotZ.value = 0;
          updateTransformField('rotation', readRotation());
        });
        resetScale.addEventListener('click', () => {
          scaleX.value = 1;
          scaleY.value = 1;
          scaleZ.value = 1;
          updateTransformField('scale', readScale());
        });

        bindComponentEditors();
      }
```

##### Step 3 Verification Checklist
- [ ] Run `cd Server~` then `npm run build`.
- [ ] Select a GameObject with multiple components and confirm each component renders as an expandable card.
- [ ] Edit a BoxCollider field such as `isTrigger` and confirm it updates in Unity.
- [ ] Edit a Light field such as `intensity` and confirm the change in Unity.

#### Step 3 STOP & COMMIT
**STOP & COMMIT:** Agent must stop here and wait for the user to test, stage, and commit the change.

#### Step 4: Inspector Actions and Polish
- [ ] Append the following CSS to the end of the `<style>` block in [Server~/src/ui/unity-dashboard.html](Server~/src/ui/unity-dashboard.html):

```css
      .inspector-toolbar{display:flex;align-items:center;justify-content:space-between;gap:8px;margin-bottom:8px}
      .inspector-actions{display:flex;gap:6px}
      .inspector-actions button{height:28px;font-size:11px;padding:0 10px}
      .inspector-actions button.danger{border-color:#b91c1c;color:#fecaca}
      .field-pending{border-color:#a16207 !important}
      .field-success{border-color:#22c55e !important}
      .field-error{border-color:#b91c1c !important}
      .modal{position:fixed;inset:0;background:rgba(0,0,0,0.55);display:none;align-items:center;justify-content:center;z-index:10001}
      .modal.visible{display:flex}
      .modal-card{background:#0f1c2e;border:1px solid #1a3a5a;border-radius:8px;padding:16px;min-width:280px}
      .modal-title{font-size:14px;color:#e6eef8;margin-bottom:8px}
      .modal-body{font-size:12px;color:#9fb1c9;margin-bottom:12px}
      .modal-actions{display:flex;justify-content:flex-end;gap:8px}
```

- [ ] Replace the inspector card markup with the following (within the grid layout):

```html
          <div class="card inspector-card">
            <div class="inspector-toolbar">
              <div class="section-title">Inspector</div>
              <div class="inspector-actions">
                <button id="inspectorFocusBtn" title="Focus in Unity">Focus</button>
                <button id="inspectorDuplicateBtn" title="Duplicate">Duplicate</button>
                <button id="inspectorDeleteBtn" class="danger" title="Delete">Delete</button>
              </div>
            </div>
            <div id="inspectorPane" class="inspector">
              <div id="inspectorStatus" class="inspector-status hidden"></div>
              <div id="inspectorContent" class="inspector-content">
                <div class="meta">No GameObject selected.</div>
              </div>
            </div>
          </div>
```

- [ ] Add the delete confirmation modal markup just before the `<script>` tag:

```html
    <div id="deleteModal" class="modal">
      <div class="modal-card">
        <div class="modal-title">Delete GameObject?</div>
        <div class="modal-body">This action cannot be undone.</div>
        <div class="modal-actions">
          <button id="deleteCancelBtn">Cancel</button>
          <button id="deleteConfirmBtn" class="danger">Delete</button>
        </div>
      </div>
    </div>
```

- [ ] After the inspector DOM references, add the action button and modal references:

```javascript
      const inspectorFocusBtn = document.getElementById('inspectorFocusBtn');
      const inspectorDuplicateBtn = document.getElementById('inspectorDuplicateBtn');
      const inspectorDeleteBtn = document.getElementById('inspectorDeleteBtn');
      const deleteModal = document.getElementById('deleteModal');
      const deleteCancelBtn = document.getElementById('deleteCancelBtn');
      const deleteConfirmBtn = document.getElementById('deleteConfirmBtn');
```

- [ ] Add the following helpers after the inspector DOM references:

```javascript
      const inspectorDebounceTimers = new Map();
      const FIELD_FLASH_MS = 900;

      function debounceInspector(key, fn, delay = 500) {
        const existing = inspectorDebounceTimers.get(key);
        if (existing) clearTimeout(existing);
        inspectorDebounceTimers.set(key, setTimeout(fn, delay));
      }

      function flashFieldState(element, state) {
        if (!element) return;
        element.classList.remove('field-pending', 'field-success', 'field-error');
        if (state) element.classList.add(`field-${state}`);
        if (state && state !== 'pending') {
          setTimeout(() => {
            element.classList.remove('field-success', 'field-error');
          }, FIELD_FLASH_MS);
        }
      }

      function bindTextInput(input, commit) {
        if (!input) return;
        input.addEventListener('focus', () => {
          input.dataset.originalValue = input.value;
        });
        input.addEventListener('keydown', (event) => {
          if (event.key === 'Escape') {
            input.value = input.dataset.originalValue || '';
            input.blur();
          }
          if (event.key === 'Enter') {
            input.blur();
            commit();
          }
        });
        input.addEventListener('change', commit);
      }

      function bindCheckboxInput(input, commit) {
        if (!input) return;
        input.addEventListener('focus', () => {
          input.dataset.originalValue = input.checked ? 'true' : 'false';
        });
        input.addEventListener('keydown', (event) => {
          if (event.key === 'Escape') {
            input.checked = input.dataset.originalValue === 'true';
            input.blur();
          }
          if (event.key === 'Enter') {
            input.blur();
            commit();
          }
        });
        input.addEventListener('change', commit);
      }

      function bindNumberInput(input, commit) {
        if (!input) return;
        input.addEventListener('focus', () => {
          input.dataset.originalValue = input.value;
        });
        input.addEventListener('keydown', (event) => {
          if (event.key === 'Escape') {
            input.value = input.dataset.originalValue || '0';
            input.blur();
          }
          if (event.key === 'Enter') {
            input.blur();
            commit();
          }
        });
        input.addEventListener('input', () => {
          const key = input.id || `${input.dataset.component}-${input.dataset.field}-${input.dataset.axis || ''}`;
          debounceInspector(key, commit, 500);
        });
        input.addEventListener('change', commit);
      }
```

- [ ] Replace `setInspectorStatus` with the following version that adds a spinner for pending states:

```javascript
      function setInspectorStatus(type, message) {
        if (!inspectorStatus) return;
        if (type === 'pending' && message) {
          inspectorStatus.innerHTML = `<span class="spinner"></span> ${message}`;
        } else {
          inspectorStatus.textContent = message || '';
        }
        inspectorStatus.className = 'inspector-status' + (type ? ' ' + type : '');
        if (!message) inspectorStatus.classList.add('hidden');
        else inspectorStatus.classList.remove('hidden');
      }
```

- [ ] Replace `updateGameObjectProperty` with the following version to apply field feedback and validation:

```javascript
      async function updateGameObjectProperty(field, value, input) {
        if (!state.activeGameObject) return;

        if (field === 'layer' && (!Number.isFinite(value) || value < 0 || value > 31)) {
          setInspectorStatus('error', 'Layer must be between 0 and 31.');
          flashFieldState(input, 'error');
          return;
        }

        setInspectorBusy(true);
        setInspectorStatus('pending', 'Updating GameObject...');
        flashFieldState(input, 'pending');

        const result = await safeCallTool('update_gameobject', {
          instanceId: state.activeGameObject.id,
          gameObjectData: buildGameObjectUpdateData(field, value)
        });

        if (!result) {
          setInspectorStatus('error', 'Update failed.');
          flashFieldState(input, 'error');
          setInspectorBusy(false);
          return;
        }

        setInspectorStatus('success', 'Update complete.');
        flashFieldState(input, 'success');
        await fetchGameObjectDetails(state.activeGameObject, { silent: true });
        await fetchAll();
        setInspectorBusy(false);
      }
```

- [ ] Replace `updateTransformField` with the following version to apply field feedback:

```javascript
      async function updateTransformField(field, vector, input) {
        if (!state.activeGameObject) return;

        setInspectorBusy(true);
        setInspectorStatus('pending', 'Updating transform...');
        if (input) flashFieldState(input, 'pending');

        const result = await safeCallTool('set_transform', {
          instanceId: state.activeGameObject.id,
          [field]: vector,
          space: state.transformSpace
        });

        if (!result) {
          setInspectorStatus('error', 'Transform update failed.');
          if (input) flashFieldState(input, 'error');
          setInspectorBusy(false);
          return;
        }

        setInspectorStatus('success', 'Transform updated.');
        if (input) flashFieldState(input, 'success');
        await fetchGameObjectDetails(state.activeGameObject, { silent: true });
        setInspectorBusy(false);
      }
```

- [ ] Replace `updateComponentField` with the following version to apply field feedback:

```javascript
      async function updateComponentField(componentName, fieldName, value, input) {
        if (!state.activeGameObject) return;

        setInspectorBusy(true);
        setInspectorStatus('pending', `Updating ${componentName}...`);
        if (input) flashFieldState(input, 'pending');

        const result = await safeCallTool('update_component', {
          instanceId: state.activeGameObject.id,
          componentName,
          componentData: { [fieldName]: value }
        });

        if (!result) {
          setInspectorStatus('error', `${componentName} update failed.`);
          if (input) flashFieldState(input, 'error');
          setInspectorBusy(false);
          return;
        }

        setInspectorStatus('success', `${componentName} updated.`);
        if (input) flashFieldState(input, 'success');
        await fetchGameObjectDetails(state.activeGameObject, { silent: true });
        setInspectorBusy(false);
      }
```

- [ ] Replace `bindComponentEditors` with the following version to support debounced number input and keyboard shortcuts:

```javascript
      function bindComponentEditors() {
        const inputs = inspectorContent.querySelectorAll('[data-component][data-field]');
        inputs.forEach((input) => {
          const componentName = input.dataset.component;
          const fieldName = input.dataset.field;
          const axis = input.dataset.axis;

          if (input.type === 'checkbox') {
            bindCheckboxInput(input, () => {
              updateComponentField(componentName, fieldName, input.checked, input);
            });
            return;
          }

          if (input.type === 'color') {
            input.addEventListener('change', () => {
              const alpha = Number(input.dataset.alpha ?? 1);
              updateComponentField(componentName, fieldName, hexToColor(input.value, alpha), input);
            });
            return;
          }

          if (axis) {
            bindNumberInput(input, () => {
              const group = inspectorContent.querySelectorAll(
                `input[data-component="${componentName}"][data-field="${fieldName}"][data-axis]`
              );
              const vec = { x: 0, y: 0, z: 0 };
              group.forEach((item) => {
                vec[item.dataset.axis] = Number(item.value);
              });
              updateComponentField(componentName, fieldName, vec, input);
            });
            return;
          }

          if (input.type === 'number') {
            bindNumberInput(input, () => {
              updateComponentField(componentName, fieldName, Number(input.value), input);
            });
            return;
          }

          bindTextInput(input, () => {
            updateComponentField(componentName, fieldName, input.value, input);
          });
        });
      }
```

- [ ] Replace the body of the inspector action handlers section with the following to wire action buttons and modal:

```javascript
      inspectorFocusBtn.addEventListener('click', async () => {
        if (!state.activeGameObject) {
          setInspectorStatus('error', 'Select a GameObject first.');
          return;
        }
        await safeCallTool('select_gameobject', { instanceId: state.activeGameObject.id });
        setInspectorStatus('success', 'Focused in Unity.');
      });

      inspectorDuplicateBtn.addEventListener('click', async () => {
        if (!state.activeGameObject) {
          setInspectorStatus('error', 'Select a GameObject first.');
          return;
        }
        setInspectorBusy(true);
        const result = await safeCallTool('duplicate_gameobject', { instanceId: state.activeGameObject.id });
        if (!result) {
          setInspectorStatus('error', 'Duplicate failed.');
          setInspectorBusy(false);
          return;
        }
        setInspectorStatus('success', 'Duplicate created.');
        await fetchAll();
        setInspectorBusy(false);
      });

      inspectorDeleteBtn.addEventListener('click', () => {
        if (!state.activeGameObject) {
          setInspectorStatus('error', 'Select a GameObject first.');
          return;
        }
        deleteModal.classList.add('visible');
      });

      deleteCancelBtn.addEventListener('click', () => {
        deleteModal.classList.remove('visible');
      });

      deleteConfirmBtn.addEventListener('click', async () => {
        if (!state.activeGameObject) {
          deleteModal.classList.remove('visible');
          return;
        }
        setInspectorBusy(true);
        const result = await safeCallTool('delete_gameobject', { instanceId: state.activeGameObject.id, includeChildren: true });
        deleteModal.classList.remove('visible');
        if (!result) {
          setInspectorStatus('error', 'Delete failed.');
          setInspectorBusy(false);
          return;
        }
        setInspectorStatus('success', 'GameObject deleted.');
        state.activeGameObject = null;
        state.activeGameObjectDetails = null;
        renderInspector();
        await fetchAll();
        setInspectorBusy(false);
      });
```

- [ ] Replace the existing `renderInspector` function with the following version to add debounced input handlers:

```javascript
      function renderInspector() {
        if (!inspectorContent) return;

        if (!state.activeGameObject) {
          renderInspectorPlaceholder('No GameObject selected.');
          return;
        }

        if (!state.activeGameObjectDetails) {
          renderInspectorPlaceholder('Loading GameObject details...');
          return;
        }

        const go = state.activeGameObjectDetails;
        const isStatic = Boolean(go.isStatic);
        const transform = getTransformComponent(go);
        const vectors = getTransformVectors(transform);

        inspectorContent.innerHTML = `
          <div class="inspector-section">
            <div class="inspector-header">GameObject</div>
            <div class="inspector-row"><span>ID</span><div>${state.activeGameObject.id}</div></div>
            <div class="inspector-row"><span>Path</span><div>${state.activeGameObject.path}</div></div>
            <div class="inspector-row"><span>Active</span><div>${go.activeSelf ? 'Yes' : 'No'}</div></div>
            <div class="inspector-row"><span>Layer</span><div>${go.layer} (${go.layerName || 'Unknown'})</div></div>
          </div>

          <div class="inspector-section">
            <div class="inspector-header">Properties</div>
            <div class="inspector-field">
              <label for="goName">Name</label>
              <input id="goName" type="text" value="${go.name || ''}" />
            </div>
            <div class="inspector-field">
              <label for="goTag">Tag</label>
              <input id="goTag" type="text" value="${go.tag || ''}" />
            </div>
            <div class="inspector-field">
              <label for="goLayer">Layer</label>
              <input id="goLayer" type="number" min="0" max="31" value="${Number.isFinite(go.layer) ? go.layer : 0}" />
            </div>
            <div class="inspector-field">
              <label for="goActive">Active</label>
              <input id="goActive" type="checkbox" ${go.activeSelf ? 'checked' : ''} />
            </div>
            <div class="inspector-field">
              <label for="goStatic">Static</label>
              <input id="goStatic" type="checkbox" ${isStatic ? 'checked' : ''} />
            </div>
          </div>

          <div class="inspector-section">
            <div class="inspector-header">Transform</div>
            <div class="transform-row">
              <label>Space</label>
              <div class="toggle-group">
                <button type="button" id="transformWorld" class="${state.transformSpace === 'world' ? 'active' : ''}">World</button>
                <button type="button" id="transformLocal" class="${state.transformSpace === 'local' ? 'active' : ''}">Local</button>
              </div>
            </div>
            <div class="transform-row">
              <label>Position <span class="transform-unit">units</span></label>
              <div class="transform-grid">
                <input id="posX" type="number" step="0.1" value="${vectors.position.x}" />
                <input id="posY" type="number" step="0.1" value="${vectors.position.y}" />
                <input id="posZ" type="number" step="0.1" value="${vectors.position.z}" />
              </div>
            </div>
            <div class="transform-row">
              <label>Rotation <span class="transform-unit">deg</span></label>
              <div class="transform-grid">
                <input id="rotX" type="number" step="15" value="${vectors.rotation.x}" />
                <input id="rotY" type="number" step="15" value="${vectors.rotation.y}" />
                <input id="rotZ" type="number" step="15" value="${vectors.rotation.z}" />
              </div>
            </div>
            <div class="transform-row">
              <label>Scale</label>
              <div class="transform-grid">
                <input id="scaleX" type="number" step="0.1" value="${vectors.scale.x}" />
                <input id="scaleY" type="number" step="0.1" value="${vectors.scale.y}" />
                <input id="scaleZ" type="number" step="0.1" value="${vectors.scale.z}" />
              </div>
            </div>
            <div class="transform-actions">
              <button type="button" id="resetPosition">Reset Position</button>
              <button type="button" id="resetRotation">Reset Rotation</button>
              <button type="button" id="resetScale">Reset Scale</button>
            </div>
          </div>

          <div class="inspector-section">
            <div class="inspector-header">Components</div>
            <div id="componentsList" class="components-list">${renderComponents(go)}</div>
          </div>
        `;

        const nameInput = inspectorContent.querySelector('#goName');
        const tagInput = inspectorContent.querySelector('#goTag');
        const layerInput = inspectorContent.querySelector('#goLayer');
        const activeInput = inspectorContent.querySelector('#goActive');
        const staticInput = inspectorContent.querySelector('#goStatic');
        const posX = inspectorContent.querySelector('#posX');
        const posY = inspectorContent.querySelector('#posY');
        const posZ = inspectorContent.querySelector('#posZ');
        const rotX = inspectorContent.querySelector('#rotX');
        const rotY = inspectorContent.querySelector('#rotY');
        const rotZ = inspectorContent.querySelector('#rotZ');
        const scaleX = inspectorContent.querySelector('#scaleX');
        const scaleY = inspectorContent.querySelector('#scaleY');
        const scaleZ = inspectorContent.querySelector('#scaleZ');
        const worldBtn = inspectorContent.querySelector('#transformWorld');
        const localBtn = inspectorContent.querySelector('#transformLocal');
        const resetPosition = inspectorContent.querySelector('#resetPosition');
        const resetRotation = inspectorContent.querySelector('#resetRotation');
        const resetScale = inspectorContent.querySelector('#resetScale');

        bindTextInput(nameInput, () => updateGameObjectProperty('name', nameInput.value.trim(), nameInput));
        bindTextInput(tagInput, () => updateGameObjectProperty('tag', tagInput.value.trim(), tagInput));
        bindNumberInput(layerInput, () => updateGameObjectProperty('layer', Number(layerInput.value), layerInput));
        bindCheckboxInput(activeInput, () => updateGameObjectProperty('activeSelf', activeInput.checked, activeInput));
        bindCheckboxInput(staticInput, () => updateGameObjectProperty('isStatic', staticInput.checked, staticInput));

        worldBtn.addEventListener('click', () => setTransformSpace('world'));
        localBtn.addEventListener('click', () => setTransformSpace('local'));

        const readPosition = () => ({
          x: Number(posX.value),
          y: Number(posY.value),
          z: Number(posZ.value)
        });
        const readRotation = () => ({
          x: Number(rotX.value),
          y: Number(rotY.value),
          z: Number(rotZ.value)
        });
        const readScale = () => ({
          x: Number(scaleX.value),
          y: Number(scaleY.value),
          z: Number(scaleZ.value)
        });

        bindNumberInput(posX, () => updateTransformField('position', readPosition(), posX));
        bindNumberInput(posY, () => updateTransformField('position', readPosition(), posY));
        bindNumberInput(posZ, () => updateTransformField('position', readPosition(), posZ));
        bindNumberInput(rotX, () => updateTransformField('rotation', readRotation(), rotX));
        bindNumberInput(rotY, () => updateTransformField('rotation', readRotation(), rotY));
        bindNumberInput(rotZ, () => updateTransformField('rotation', readRotation(), rotZ));
        bindNumberInput(scaleX, () => updateTransformField('scale', readScale(), scaleX));
        bindNumberInput(scaleY, () => updateTransformField('scale', readScale(), scaleY));
        bindNumberInput(scaleZ, () => updateTransformField('scale', readScale(), scaleZ));

        resetPosition.addEventListener('click', () => {
          posX.value = 0;
          posY.value = 0;
          posZ.value = 0;
          updateTransformField('position', readPosition(), posX);
        });
        resetRotation.addEventListener('click', () => {
          rotX.value = 0;
          rotY.value = 0;
          rotZ.value = 0;
          updateTransformField('rotation', readRotation(), rotX);
        });
        resetScale.addEventListener('click', () => {
          scaleX.value = 1;
          scaleY.value = 1;
          scaleZ.value = 1;
          updateTransformField('scale', readScale(), scaleX);
        });

        bindComponentEditors();
      }
```

##### Step 4 Verification Checklist
- [ ] Run `cd Server~` then `npm run build`.
- [ ] Click Delete and confirm the modal appears; confirm and verify the GameObject is removed.
- [ ] Click Duplicate and verify a new copy appears in the hierarchy.
- [ ] Edit transform position and confirm updates occur only after 500ms pause.
- [ ] Enter an invalid layer value (greater than 31) and confirm an inline error message appears.
- [ ] Press Escape while editing an input and confirm the original value is restored.

#### Step 4 STOP & COMMIT
**STOP & COMMIT:** Agent must stop here and wait for the user to test, stage, and commit the change.
