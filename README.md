# MCP Unity 2.0

MCP Unity 2.0 is a Unity CLI extension package for focused Editor authoring workflows. Unity Pipeline supplies the broad command catalog; this package adds five commands where project-specific safety or bounded inspection is valuable. An optional private Node companion adds read-oriented MCP resources and a dashboard.

Version 2.0.0 supports Unity 6000.0, Unity 6000.3, and Unity 6000.5. It requires Unity CLI 1.0.0-beta.2 or newer. Unity CLI and Pipeline are experimental products, so keep the pins in this repository synchronized when upgrading.

> [!IMPORTANT]
> This is a breaking architecture change from 1.4.0. There is no custom WebSocket bridge, no listener on port 8090, and no `ProjectSettings/McpUnitySettings.json`. See [Migration from 1.4.0](#migration-from-140).

## Architecture

The normal data flow is:

```text
MCP client <-> Unity CLI (`unity mcp`) <-> Pipeline <-> Unity Editor
                                              ^
                                              |
                                  MCP Unity extension commands
```

The optional companion is a second MCP server:

```text
MCP client <-> private Node companion <-> `unity mcp` <-> Pipeline <-> Unity Editor
```

Unity Package Manager installs the exact dependency `com.unity.pipeline@0.3.1-exp.1` automatically from this package's manifest. MCP Unity does not run `unity pipeline install`, mutate a project manifest, or vendor Pipeline.

Unity CLI is machine-level software and is never downloaded or installed by this package. A developer or CI image must install it explicitly.

## Install

1. Use Unity 6000.0, Unity 6000.3, or Unity 6000.5.
2. In Package Manager, choose **Add package from git URL** and enter:

   ```text
   https://github.com/CoderGamester/mcp-unity.git#2.0.0
   ```

3. Let UPM resolve `com.unity.pipeline@0.3.1-exp.1`.
4. Install Unity CLI by following the [official Unity CLI documentation](https://docs.unity.com/en-us/unity-cli/use-unity-cli).
5. Open `Window > MCP Unity > Setup`.

The Setup window is user-initiated and never opens on import. It shows the project and resolved Pipeline paths, checks only `unity --version`, and can copy official installation or MCP configuration text. It does not execute installers, request elevation, modify PATH, change shell files, run upgrades, write client configuration, or store a machine-specific CLI path in project settings.

CLI lookup order is:

1. the path entered in the Setup window;
2. `UNITY_CLI_PATH`;
3. `unity` from `PATH`.

CLI 1.x versions at or above Unity CLI 1.0.0-beta.2 are compatible. A newer major version is reported as untested rather than silently accepted as tested.

If UPM cannot resolve Pipeline, treat it as a normal package-resolution failure: confirm the Unity version, registry/network access, and the exact dependency pin in `package.json`, then retry Package Manager resolution.

## Configure the primary MCP server

The supported primary entrypoint is:

```bash
unity mcp --project-path "/absolute/path/to/UnityProject"
```

A JSON-based MCP client can use:

```json
{
  "mcpServers": {
    "unity": {
      "command": "unity",
      "args": [
        "mcp",
        "--project-path",
        "/absolute/path/to/UnityProject"
      ]
    }
  }
}
```

If Unity CLI is not on `PATH`, set the MCP client's command to its absolute executable path or arrange `UNITY_CLI_PATH` in the launch environment. The Setup window copies, but never writes, this configuration.

## MCP Unity extension commands

Pipeline discovers these commands through `[CliCommand]`:

- `inspect_gameobject` — bounded GameObject, hierarchy, component, and serialized-property inspection. Depth defaults to 2 and caps at 8; nodes default to 200 and cap at 1,000.
- `duplicate_gameobject` — duplicate a source with optional parent/name and `world_position_stays`; records Unity Undo and returns the new identity.
- `unload_scene` — unload a scene by path; protects dirty scenes unless `force=true` and never unloads the only active scene.
- `editor_step` — advance one Editor frame; requires play mode and returns the resulting Editor state.
- `assign_material` — assign a material to a Renderer slot with validation, Undo, and prefab-modification recording.

All other Editor operations use commands supplied by `com.unity.pipeline@0.3.1-exp.1`. Run the CLI/MCP command discovery flow to inspect the full official catalog.

## Optional MCP companion

`Server~` is a private, bundled Node 20+ package. It is optional: the Unity package and all five extension commands remain usable through the primary `unity mcp` server without Node.

The companion requires `--project-path <absolute-path>` and accepts `--unity-cli-path <absolute-path>`. Its lookup order is the argument, `UNITY_CLI_PATH`, then `unity` from `PATH`. It validates Unity CLI 1.0.0-beta.2 or newer and never installs it.

Resolve the package path shown by `Window > MCP Unity > Setup`, then configure:

```json
{
  "mcpServers": {
    "mcp-unity-companion": {
      "command": "node",
      "args": [
        "/resolved/upm/package/path/Server~/build/index.js",
        "--project-path",
        "/absolute/path/to/UnityProject"
      ]
    }
  }
}
```

To pin a CLI executable for only this companion, use the optional argument:

```json
{
  "mcpServers": {
    "mcp-unity-companion": {
      "command": "node",
      "args": [
        "/resolved/upm/package/path/Server~/build/index.js",
        "--project-path",
        "/absolute/path/to/UnityProject",
        "--unity-cli-path",
        "/absolute/path/to/unity"
      ]
    }
  }
}
```

The companion exposes exactly:

- Tool: `show_unity_dashboard`
- Resources:
  - `unity://logs{?severity,limit}`
  - `unity://scenes-hierarchy{?path,max_nodes}`
  - `unity://gameobject/{target}`
  - `unity://packages{?include_indirect}`
  - `unity://tests/{mode}`
  - `ui://unity-dashboard`
- Prompts:
  - `gameobject_handling_strategy`
  - `unity_dashboard`

The companion lazily starts `unity mcp`, retries one interrupted read-only resource request, and never mirrors or retries mutation commands.

## Remote and CI operation

The package is local-only. Run Unity CLI on the same host as the Unity Editor. For a remote workflow, connect to that host through SSH or external agent infrastructure and launch the CLI there; do not expose an Editor socket.

CI must install Unity CLI before starting MCP Unity. The official non-interactive beta-channel commands currently surfaced by the Setup window are:

macOS/Linux:

```bash
curl -fsSL https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.sh | UNITY_CLI_CHANNEL=beta bash
```

Windows PowerShell:

```powershell
$env:UNITY_CLI_CHANNEL='beta'; irm https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.ps1 | iex
```

After installation, CI should run `unity --version` and require Unity CLI 1.0.0-beta.2 or newer before launching `unity mcp --project-path <absolute-project-path>`.

## Migration from 1.4.0

The table is intentionally exhaustive. Its inventory is enforced against the actual tool constants, resource registrations, prompt registrations, URI templates, and settings fields in the `1.4.0` tag.

### Tools

| 1.4.0 concept | 2.0 replacement |
|---|---|
| `tool:add_asset_to_scene` | Pipeline `instantiate_prefab` for prefab assets; use the relevant Pipeline asset/authoring command for other asset types. |
| `tool:add_package` | Pipeline `package_add`; poll `package_status` when needed. |
| `tool:assign_material` | MCP Unity extension `assign_material`. |
| `tool:batch_execute` | Removed. Let the MCP client sequence Pipeline commands; use Pipeline's purpose-built plural commands such as `create_gameobjects` where available. |
| `tool:create_material` | Pipeline `create_asset` with type `UnityEngine.Material`, then `set_material_properties`. |
| `tool:create_prefab` | Pipeline `create_gameobject`/`attach_script` as needed, then `create_prefab`. |
| `tool:create_scene` | Pipeline `create_scene`. |
| `tool:delete_gameobject` | Pipeline `delete_gameobject`. |
| `tool:delete_scene` | Pipeline `remove_scene_from_build` when applicable, then `delete_asset` with confirmation. |
| `tool:duplicate_gameobject` | MCP Unity extension `duplicate_gameobject`. |
| `tool:execute_menu_item` | Pipeline `menu`. |
| `tool:get_console_logs` | Pipeline `get_console_logs`, or companion `unity://logs{?severity,limit}`. |
| `tool:get_gameobject` | MCP Unity extension `inspect_gameobject`, or companion `unity://gameobject/{target}`. |
| `tool:get_material_info` | Pipeline `get_material_properties`. |
| `tool:get_play_mode_status` | Pipeline `editor_status`. |
| `tool:get_scene_info` | Pipeline `list_open_scenes`. |
| `tool:get_scenes_hierarchy` | Pipeline `get_scene_hierarchy`, or companion `unity://scenes-hierarchy{?path,max_nodes}`. |
| `tool:load_scene` | Pipeline `open_scene`. |
| `tool:modify_material` | Pipeline `set_material_properties`. |
| `tool:move_gameobject` | Pipeline `set_transform`. |
| `tool:recompile_scripts` | Pipeline `recompile`; poll `recompile_status`. |
| `tool:reparent_gameobject` | Pipeline `set_parent`. |
| `tool:rotate_gameobject` | Pipeline `set_transform`. |
| `tool:run_tests` | Pipeline `list_tests`, `run_tests`, and `test_status`. |
| `tool:save_scene` | Pipeline `save_scene` or `save_all`. |
| `tool:scale_gameobject` | Pipeline `set_transform`. |
| `tool:select_gameobject` | Pipeline `set_selection`; inspect with `get_selection`. |
| `tool:send_console_log` | Removed. Use project logging code or a project-specific `[CliCommand]`; Pipeline provides `get_console_logs` and `clear_console`, not arbitrary log injection. |
| `tool:set_play_mode_status` | Pipeline `editor_play`, `editor_pause`, and `editor_stop`; use extension `editor_step` to step. |
| `tool:set_transform` | Pipeline `set_transform`. |
| `tool:show_unity_dashboard` | Optional companion tool `show_unity_dashboard`. |
| `tool:unload_scene` | MCP Unity extension `unload_scene`. |
| `tool:update_component` | Pipeline `add_component` and `set_component_properties`. |
| `tool:update_gameobject` | Pipeline `create_gameobject`, `rename_gameobject`, `set_active`, `set_tag`, `set_layer`, `set_parent`, and `set_transform` as required. |

### Resources, prompts, and URIs

| 1.4.0 concept | 2.0 replacement |
|---|---|
| `resource:get_assets` | Removed as a resource; use Pipeline `find_assets`. |
| `resource:get_console_logs` | Companion `unity://logs{?severity,limit}` or Pipeline `get_console_logs`. |
| `resource:get_gameobject` | Companion `unity://gameobject/{target}` or extension `inspect_gameobject`. |
| `resource:get_menu_items` | Removed as a resource; call Pipeline `menu` without a path to list menu items. |
| `resource:get_packages` | Companion `unity://packages{?include_indirect}` or Pipeline `package_list`. |
| `resource:get_scenes_hierarchy` | Companion `unity://scenes-hierarchy{?path,max_nodes}` or Pipeline `get_scene_hierarchy`. |
| `resource:get_tests` | Companion `unity://tests/{mode}` or Pipeline `list_tests`. |
| `resource:unity_dashboard_app` | Companion `ui://unity-dashboard`. |
| `resource:unity_dashboard_app_legacy` | Removed; use companion `ui://unity-dashboard`. |
| `uri:ui://unity-dashboard` | Retained by the optional companion. |
| `uri:unity://assets` | Removed; use Pipeline `find_assets`. |
| `uri:unity://gameobject/{idOrName}` | Companion `unity://gameobject/{target}`. |
| `uri:unity://logs/{logType}?offset={offset}&limit={limit}&includeStackTrace={includeStackTrace}` | Companion `unity://logs{?severity,limit}`; pagination/stack controls are no longer public URI arguments. |
| `uri:unity://menu-items` | Removed; call Pipeline `menu` without a path. |
| `uri:unity://packages` | Companion `unity://packages{?include_indirect}`. |
| `uri:unity://scenes_hierarchy` | Renamed to companion `unity://scenes-hierarchy{?path,max_nodes}`. |
| `uri:unity://tests/{testMode}` | Renamed to companion `unity://tests/{mode}`. |
| `uri:unity://ui/dashboard` | Removed legacy alias; use `ui://unity-dashboard`. |
| `prompt:gameobject_handling_strategy` | Retained by the optional companion. |
| `prompt:unity_dashboard` | Retained by the optional companion. |

### Configuration and integration

| 1.4.0 concept | 2.0 replacement |
|---|---|
| `config:Port` | Removed; Unity CLI owns transport. There is no package port setting. |
| `config:RequestTimeoutSeconds` | Removed; use MCP host/CLI timeout controls. |
| `config:AutoStartServer` | Removed; the MCP host explicitly launches `unity mcp`. |
| `config:EnableInfoLogs` | Removed; use Unity CLI and Editor logging. |
| `config:NpmExecutablePath` | Removed; the Unity package does not run npm. |
| `config:AllowRemoteConnections` | Removed; run the CLI on the Unity host and connect through SSH/external agent infrastructure. |
| `concept:UNITY_HOST` | Removed; there is no host override for an Editor socket. |
| `concept:ProjectSettings/McpUnitySettings.json` | Removed and never created. |
| `concept:Unity-driven npm install/build` | Removed; the bundled companion build is shipped in `Server~/build`, and maintainers build it outside Unity. |
| `concept:automatic MCP-client configuration` | Removed; `Window > MCP Unity > Setup` only copies configuration after a user action. |
| `concept:PackedCache mutation` | Removed; MCP Unity never edits IDE workspaces or PackedCache references. |
| `concept:custom WebSocket endpoint/port` | Removed; Unity CLI/Pipeline own communication and the old endpoint is not opened. |

Old tool aliases are not retained. Update prompts and client automation to the replacement names above before upgrading.

## Development

Install and test the private companion:

```bash
cd Server~
npm ci
npm test -- --runInBand --detectOpenHandles
npm run build
npm audit
```

Run Unity EditMode tests from the Editor Test Runner or in batch mode on all supported lines:

```bash
"/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics -projectPath "/path/to/test-project" \
  -runTests -testPlatform EditMode -testResults "/tmp/results.xml"
```

See [AGENTS.md](AGENTS.md) for the maintainer architecture and release invariants, and [CHANGELOG.md](CHANGELOG.md) for release notes.

## Security and audit note

The companion uses stdio only. The exact required `@modelcontextprotocol/sdk@1.26.0` pin currently brings two moderate npm audit advisories through its unused Hono HTTP adapter. They are tracked as an inherited pinned-SDK risk; MCP Unity does not import or expose that HTTP adapter. Changing the SDK pin requires a coordinated compatibility review.

## License

[MIT](LICENSE.md)
