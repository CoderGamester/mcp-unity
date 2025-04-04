# Instructions for Using MCP Unity Tests

Since Unity has specific requirements for package test discovery, we recommend the following workaround if tests aren't appearing in the Test Runner:

1. In your main project, create a new folder at `Assets/McpTests`
2. Copy the test files from `Packages/com.gamelovers.mcp-unity/Tests/Editor` to `Assets/McpTests`
3. Create a new Assembly Definition file in `Assets/McpTests` with the following configuration:

```json
{
    "name": "McpUnity.Tests.Editor",
    "rootNamespace": "McpUnity.Tests",
    "references": [
        "GUID:27619889b8ba8c24980f49ee34dbb44a",
        "GUID:0acc523941302664db1f4e527237feb3",
        "McpUnity.Editor"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll",
        "Newtonsoft.Json.dll"
    ],
    "autoReferenced": true,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

4. Save the assembly definition file and allow Unity to compile it
5. Open the Test Runner window (Window > General > Test Runner)
6. Tests should now appear in the EditMode tab

This workaround is necessary because Unity sometimes has difficulty discovering tests in local packages. When the package is published to a registry, this workaround should not be needed.