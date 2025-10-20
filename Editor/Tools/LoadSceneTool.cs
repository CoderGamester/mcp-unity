using System;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for loading a Unity scene, optionally additively
    /// </summary>
    public class LoadSceneTool : McpToolBase
    {
        public LoadSceneTool()
        {
            Name = "load_scene";
            Description = "Loads a scene by path or name. Supports additive loading (default: false)";
        }

        /// <summary>
        /// Execute the LoadScene tool with the provided parameters
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            string scenePath = parameters["scenePath"]?.ToObject<string>();
            string sceneName = parameters["sceneName"]?.ToObject<string>();
            string folderPath = parameters["folderPath"]?.ToObject<string>();
            bool additive = parameters["additive"]?.ToObject<bool?>() ?? false;

            if (string.IsNullOrEmpty(scenePath))
            {
                if (string.IsNullOrEmpty(sceneName))
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        "Provide either 'scenePath' or 'sceneName'",
                        "validation_error"
                    );
                }

                // Resolve scene path by name (optionally within folderPath)
                string filter = $"{sceneName} t:Scene";
                string[] searchInFolders = null;
                if (!string.IsNullOrEmpty(folderPath))
                {
                    if (!AssetDatabase.IsValidFolder(folderPath))
                    {
                        return McpUnitySocketHandler.CreateErrorResponse(
                            $"Folder '{folderPath}' does not exist",
                            "not_found_error"
                        );
                    }
                    searchInFolders = new[] { folderPath };
                }

                var guids = AssetDatabase.FindAssets(filter, searchInFolders);
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName)
                    {
                        scenePath = path;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(scenePath))
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Scene named '{sceneName}' not found",
                        "not_found_error"
                    );
                }
            }

            try
            {
                // Avoid any save prompts: save open scenes before replacing them (non-additive)
                if (!additive)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
                }

                var mode = additive
                    ? UnityEditor.SceneManagement.OpenSceneMode.Additive
                    : UnityEditor.SceneManagement.OpenSceneMode.Single;

                var openedScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, mode);

                // For non-additive, scene becomes active automatically. For additive, we do not change active scene.

                McpLogger.LogInfo($"Loaded scene at path '{scenePath}' (additive={additive})");

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Successfully loaded scene at path '{scenePath}' (additive={additive.ToString().ToLower()})",
                    ["scenePath"] = scenePath,
                    ["additive"] = additive
                };
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error loading scene: {ex.Message}",
                    "scene_load_error"
                );
            }
        }
    }
}


