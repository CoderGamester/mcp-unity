const GAMEOBJECT_STRATEGY = `Use Pipeline read commands before mutations:
1. Read get_scene_hierarchy or find_gameobjects to obtain a stable target.
2. Use inspect_gameobject for bounded component and serialized-property inspection.
3. Use official Pipeline authoring commands for standard changes.
4. Use only the MCP Unity extensions when needed: duplicate_gameobject, unload_scene, editor_step, and assign_material.
5. Re-read get_scene_hierarchy or inspect_gameobject to verify the result.`;
const DASHBOARD_GUIDE = `Open show_unity_dashboard for a read-only project overview.
The companion resources map to official Pipeline commands: get_console_logs, get_scene_hierarchy, package_list, and list_tests.
GameObject details use inspect_gameobject. The other MCP Unity extensions are duplicate_gameobject, unload_scene, editor_step, and assign_material.
The dashboard never invokes mutations; execute any authoring command explicitly through Unity CLI/Pipeline.`;
export function registerCompanionPrompts(server) {
    server.registerPrompt('gameobject_handling_strategy', {
        description: 'A safe discovery, targeting, mutation, and verification workflow.',
    }, async () => ({
        messages: [
            {
                role: 'user',
                content: { type: 'text', text: GAMEOBJECT_STRATEGY },
            },
        ],
    }));
    server.registerPrompt('unity_dashboard', {
        description: 'Guidance for the read-only Unity dashboard and Pipeline commands.',
    }, async () => ({
        messages: [
            {
                role: 'user',
                content: { type: 'text', text: DASHBOARD_GUIDE },
            },
        ],
    }));
}
