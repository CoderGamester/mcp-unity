# Changelog

All notable changes to MCP Unity are documented here.

## [2.0.0] - Unreleased

### Breaking architecture

- Replaced the custom Unity WebSocket/Node bridge with Unity CLI and `com.unity.pipeline@0.3.1-exp.1`.
- Raised the minimum Editor version to Unity 6000.0 and defined support for Unity 6000.0, 6000.3, and 6000.5.
- Removed the former bridge listener, settings file, remote socket mode, tool aliases, command queues, retry generations, port/time-out controls, automatic MCP client configuration, Unity-driven npm workflow, and PackedCache workspace mutation.
- Removed the registry/published-server surface. `Server~` is now a private optional companion.

### Added

- Five Pipeline extensions: `inspect_gameobject`, `duplicate_gameobject`, `unload_scene`, `editor_step`, and `assign_material`.
- User-initiated `Window > MCP Unity > Setup` flow for Pipeline status, Unity CLI detection, official installation instructions, and configuration copying.
- Optional read-oriented companion resources, prompts, and dashboard.
- Exhaustive 1.4.0 migration table and release-contract tests.
- A self-contained Node 20 companion bundle with generated third-party notices plus portable shell-free clean-archive startup, fake child MCP, and stdio dashboard verification.
- Aggregate 512 KiB output ceilings for `inspect_gameobject` and every companion resource, including exact-boundary pre-allocation work accounting without a second value traversal, explicit component/property/projection truncation metadata, stack-safe adversarial payload handling, and 4 KiB bounded companion errors including dashboard failures.

### Experimental dependencies

- Unity CLI 1.0.0-beta.2 or newer is installed separately by each user or CI environment.
- Pipeline is pinned to experimental version `0.3.1-exp.1` and is resolved automatically by UPM.
- The exact MCP SDK pin has two inherited moderate npm advisories in an unused Hono HTTP adapter; the companion uses stdio only.

## [1.4.0] - 2026-07-24

Final release of the legacy custom bridge.

### 🆕 What is New

- Added the Unity Dashboard MCP App with Play Mode controls, scene hierarchy browsing, console and package views, GameObject selection, focus filtering, an inspector panel, and bidirectional agent context. The release includes the `show_unity_dashboard` tool, `ui://unity-dashboard` resource, and `unity_dashboard` prompt ([#109](https://github.com/CoderGamester/mcp-unity/pull/109)).
- Added `get_play_mode_status` and `set_play_mode_status` for starting, pausing, stepping, and stopping Play Mode, plus tool access to scene hierarchy and console data used by the dashboard ([#109](https://github.com/CoderGamester/mcp-unity/pull/109)).
- Added project-local auto-configuration for Cursor, Claude Code, and Codex CLI, with portable project-relative paths suitable for Git-shared MCP configuration ([`a32e47d`](https://github.com/CoderGamester/mcp-unity/commit/a32e47d4ec8731685394dd562aa6f4f119f2bf79)).
- Added OpenCode auto-configuration and relative-path support for GitHub Copilot, OpenCode, and manually copied workspace configurations ([`ec0e1e8`](https://github.com/CoderGamester/mcp-unity/commit/ec0e1e859e9f26c2e0e65577bee609ce318715e6), [`320e443`](https://github.com/CoderGamester/mcp-unity/commit/320e443e93ebba4fbf2e3630424b3b939448d55f)).
- Added Unity bridge request diagnostics and expanded lifecycle, restart, tool, dashboard, resource, and release-metadata test coverage.

### 🛠️ What Was Fixed

- Fixed `update_component` support for private serialized fields, inherited fields, nested property paths, and asset references resolved by path or GUID ([#106](https://github.com/CoderGamester/mcp-unity/pull/106)).
- Bounded deep and oversized `get_gameobject` responses with configurable depth/component scopes, a 5 MB safety ceiling, and explicit truncation markers instead of dropping the MCP connection ([`dfc623a`](https://github.com/CoderGamester/mcp-unity/commit/dfc623acbfb08438dcddd4e9b25de61de0cb1ebf)).
- Moved WebSocket request execution onto Unity's main thread to prevent Editor API access from corrupting the Inspector, then replaced cross-thread `delayCall` mutation with an update-drained concurrent queue so requests continue while the Editor is unfocused ([#139](https://github.com/CoderGamester/mcp-unity/pull/139), [#151](https://github.com/CoderGamester/mcp-unity/pull/151)).
- Kept the Windows Editor loop ticking while minimized or unfocused. The native timer now starts only after the WebSocket listener succeeds and stops on failed startup, shutdown, disposal, assembly reload, Editor quit, and Play Mode transitions; its callback is rooted, exception-guarded, and reports Win32 timer errors ([#150](https://github.com/CoderGamester/mcp-unity/pull/150)).
- Restarted the WebSocket server after entering Play Mode when domain reload is disabled, while preserving the existing reload-enabled lifecycle ([#152](https://github.com/CoderGamester/mcp-unity/pull/152)).
- Removed invalid WebSocket origin headers that prevented some Codex and MCP clients from connecting ([#149](https://github.com/CoderGamester/mcp-unity/pull/149)).
- Hardened WebSocket shutdown, restart cleanup, retry cancellation, failed-start handling, and request diagnostics to avoid stale listeners and duplicate restart attempts.
- Fixed a Unity `.meta` GUID collision with Meta XR SDK and Unity AI Assistant packages ([#133](https://github.com/CoderGamester/mcp-unity/pull/133)).
- Updated object lookup and response IDs for Unity's newer `EntityId` API while retaining `InstanceID` behavior on older supported Editors, and migrated material inspection to Unity's public shader-property APIs to remove Unity 6 compatibility warnings.
- Reduced noisy bridge logging, disabled info logs by default, and made batch-tool lookup independently testable.

### 🔄 What Changed

- Clarified global versus project-local MCP configuration and documented portable, team-shared configuration variants ([#140](https://github.com/CoderGamester/mcp-unity/pull/140)).
- Synchronized the Unity package, Node server, and MCP runtime versions at `1.4.0`.
- Removed the invalid npm publication declaration from the legacy registry manifest.
- Preserved Unity 2022.3 compatibility for this final custom WebSocket bridge release.

### New Contributors

* @Hinneman made their first contribution in https://github.com/CoderGamester/mcp-unity/pull/109
* @cfirz made their first contribution in https://github.com/CoderGamester/mcp-unity/pull/139
* @mashai made their first contribution in https://github.com/CoderGamester/mcp-unity/pull/140
* @Fen747 made their first contribution in https://github.com/CoderGamester/mcp-unity/pull/149
* @stefangrosu44-stack made their first contribution in https://github.com/CoderGamester/mcp-unity/pull/150
* @Feetschaa made their first contribution in https://github.com/CoderGamester/mcp-unity/pull/151
* @Numbcris made their first contribution in https://github.com/CoderGamester/mcp-unity/pull/152

**Full Changelog**: https://github.com/CoderGamester/mcp-unity/compare/1.3.0...1.4.0

[2.0.0]: https://github.com/CoderGamester/mcp-unity/compare/1.4.0...HEAD
[1.4.0]: https://github.com/CoderGamester/mcp-unity/releases/tag/1.4.0
