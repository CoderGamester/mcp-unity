# MCP Unity 2.0 maintainer guide

## Scope

MCP Unity 2.0.0 extends Unity CLI and Pipeline. It is not a transport bridge.

Supported Editors are Unity 6000.0, Unity 6000.3, and Unity 6000.5. The root package must keep these exact release pins:

- `com.unity.pipeline@0.3.1-exp.1`
- `com.unity.test-framework@1.3.3`
- minimum Unity `6000.0`
- minimum Unity CLI 1.0.0-beta.2

Unity CLI remains an explicit developer/CI installation. `Window > MCP Unity > Setup` may detect it with only `unity --version` and copy instructions/configuration after a user action. It must never install, upgrade, elevate, edit PATH/shell files, write client configuration, or persist a machine path in the project.

## Architecture and data flow

Primary flow:

```text
MCP host -> `unity mcp --project-path <absolute-project>` -> Pipeline
    -> Pipeline built-ins + MCP Unity `[CliCommand]` extensions -> Unity Editor
```

Optional read-oriented flow:

```text
MCP host -> private `Server~/build/index.js`
    -> lazily owned Unity CLI stdio session -> Pipeline -> Unity Editor
```

UPM resolves Pipeline from the root `package.json`. Do not invoke `unity pipeline install`, use `Client.Add` during initialization, or vendor Pipeline.

The package is local-only. Remote users run Unity CLI on the Unity host and reach it through SSH or external agent infrastructure.

## Layout

```text
Editor/
  Commands/               Five public Pipeline extension commands and DTOs
  Setup/                  User-initiated CLI/Pipeline checks and copy helpers
  Tests/                  EditMode contracts, safety, Undo, bounds, setup tests
Server~/
  src/cli/                Args, CLI lookup, strict version check
  src/unity/              Owned official Unity MCP child session
  src/resources/          Read-only resource projection and dashboard
  src/prompts/            Companion prompts
  src/ui/                 Dashboard source
  src/__tests__/          Companion and release-contract tests
  build/                  Bundled private companion output
package.json              UPM identity and exact Pipeline dependency
README.md                 User setup and exhaustive 1.4 migration table
CHANGELOG.md              Release notes
```

`Server~` is private and bundled as one self-contained ESM entrypoint. Keep Node 20+, `@modelcontextprotocol/sdk@1.26.0`, `@modelcontextprotocol/ext-apps@1.0.1`, and `zod@3.25.76` exact until a coordinated upgrade is tested. It has no npm `bin`, publish configuration, registry manifest, Docker image, or Smithery surface. `npm run build` must regenerate `build/index.js`, copy the dashboard, and update `THIRD_PARTY_NOTICES.md`; `npm run build:check` must then prove parity. The Node 20 clean archive MCP smoke must initialize the shipped bundle and read the copied dashboard with no reachable `node_modules`. Never require package users to install npm dependencies.

## Public catalogs

The only MCP Unity commands are:

- `inspect_gameobject`
- `duplicate_gameobject`
- `unload_scene`
- `editor_step`
- `assign_material`

The optional companion exposes only:

- tool `show_unity_dashboard`
- resources:
  - `unity://logs{?severity,limit}`
  - `unity://scenes-hierarchy{?path,max_nodes}`
  - `unity://gameobject/{target}`
  - `unity://packages{?include_indirect}`
  - `unity://tests/{mode}`
  - `ui://unity-dashboard`
- prompts:
  - `gameobject_handling_strategy`
  - `unity_dashboard`

Everything else maps to the official Pipeline 0.3.1-exp.1 catalog. Legacy aliases are intentionally absent.

## Adding or changing an extension command

1. Start with a failing EditMode test under `Editor/Tests`.
2. Implement the command under `Editor/Commands` with Pipeline `[CliCommand]`, `[CliArg]`, `ObjectRef`, and `AuthoringResult` contracts.
3. Keep inputs explicit and bounded. Inspection-like outputs need depth, count, collection, and string limits. `inspect_gameobject` must use one shared aggregate conversion-work budget across all component/property scans and lazy conversions, retain stable camelCase counters and limit markers, and serialize to at most 512 KiB.
4. Record Undo for scene/object mutation and record prefab instance modifications.
5. Return stable DTOs with explicit camelCase serialization names.
6. Update `CommandDiscoveryTests` so a collision with the pinned Pipeline catalog fails.
7. Update README/AGENTS catalogs and migration guidance when public behavior changes.
8. Run the complete Unity matrix.

Do not add Node proxies for mutation commands.

## Adding or changing a companion resource

1. Start with a failing Jest test in `Server~/src/__tests__`.
2. Map the URI to an official read-only Pipeline command or one of the five extensions in `Server~/src/resources/companionResources.ts`.
3. Validate and bound all URI inputs. Every Unity-backed resource must remain at or below 512 KiB and carry honest top-level projection/truncation metadata; bound strings, arrays, objects, keys, depth, values, and traversal work.
4. Decode structured content and JSON text defensively. Route transport, command, URI, CLI, disconnect, malformed-payload, and outer MCP resource errors through the centralized 4 KiB UTF-8-safe error-detail bound with an explicit `[truncated]` marker.
5. Permit at most one reconnect/retry for a transport-interrupted read.
6. Never retry, mirror, or expose a mutation tool.
7. Register only the approved URI in `Server~/src/companionServer.ts` and update catalog contract tests.

The companion CLI lookup order is `--unity-cli-path`, `UNITY_CLI_PATH`, then `unity` from `PATH`. It may execute `--version` for validation and lazily launch `unity mcp --project-path <project>` for its MCP session. It must not install Unity CLI.

## Test and build commands

Companion:

```bash
cd Server~
npm ci
npm test -- --runInBand --detectOpenHandles
npm run build
npm run build:check
npm audit --omit=dev
```

The pinned MCP SDK currently reports two moderate advisories through an unused Hono HTTP adapter. The companion is stdio-only. Record the audit result; do not change the mandated SDK pin without a compatibility upgrade task.

Unity EditMode batch pattern:

```bash
"<Unity.app>/Contents/MacOS/Unity" \
  -batchmode -nographics -projectPath "<disposable-project>" \
  -runTests -testPlatform EditMode -testResults "<results.xml>"
```

Run on:

- `/Applications/Unity/Hub/Editor/6000.0.80f1/Unity.app`
- `/Applications/Unity/Hub/Editor/6000.3.18f1/Unity.app`
- `/Applications/Unity/Hub/Editor/6000.5.5f1/Unity.app`

Also run `git diff --check`, verify a clean UPM install/removal, confirm no old bridge listener exists, and confirm no legacy settings file is created.

## Release and architecture invariants

- Root and companion versions stay synchronized at `2.0.0` for this release.
- README and this guide state Pipeline `0.3.1-exp.1`, Unity CLI 1.0.0-beta.2, and all three supported Unity lines.
- UPM declares Pipeline exactly once as a transitive dependency of consuming projects.
- Removing MCP Unity from a consumer manifest must not leave a direct Pipeline entry.
- `Window > MCP Unity > Setup` never opens automatically.
- No custom WebSocket server/client, socket handler, command queue, reconnect generation, port 8090 management, or `ProjectSettings/McpUnitySettings.json` may return.
- No direct Editor Coroutines or Newtonsoft package dependency may return.
- No registry server manifest, npm publication surface, automatic client configuration, Unity-driven npm build, or PackedCache workspace mutation may return.
- The package remains useful without the companion.
- The exhaustive migration inventory in README is checked against the actual `1.4.0` tag.

Run `unity mcp --project-path <absolute-project>` in a disposable project and verify official plus custom commands are discoverable before a release.

## Update policy

Update README, this file, release contract tests, and CHANGELOG whenever a version pin, supported Unity line, command/resource/prompt name, setup behavior, or companion contract changes.
