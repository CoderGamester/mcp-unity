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

- Completed the Windows native background-tick lifecycle so minimized/unfocused operation starts only after the socket listens and stops on shutdown, failed startup, disposal, reload, Editor quit, and play-mode transitions.
- Rooted and guarded the native callback and surfaced timer failures with the Win32 error.
- Synchronized Unity and Node versions at 1.4.0 and removed the invalid npm declaration from the legacy registry manifest.
- Preserved Unity 2022.3 compatibility for the final legacy release.

[2.0.0]: https://github.com/CoderGamester/mcp-unity/compare/1.4.0...HEAD
[1.4.0]: https://github.com/CoderGamester/mcp-unity/releases/tag/1.4.0
