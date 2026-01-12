using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for unloading a Unity scene (without deleting the asset)
    /// </summary>
    public class UnloadSceneTool : McpToolBase
    {
        public UnloadSceneTool()
        {
            Name = "unload_scene";
            Description = "Unloads a scene by path or name (does not delete the scene asset, just closes it from the hierarchy)";
        }

        /// <summary>
        /// Execute the UnloadScene tool with the provided parameters
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            string scenePath = parameters["scenePath"]?.ToObject<string>();
            string sceneName = parameters["sceneName"]?.ToObject<string>();
            bool removeScene = parameters["removeScene"]?.ToObject<bool?>() ?? true;

            if (string.IsNullOrEmpty(scenePath) && string.IsNullOrEmpty(sceneName))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Provide either 'scenePath' or 'sceneName'",
                    "validation_error"
                );
            }

            try
            {
                Scene sceneToUnload;

                if (!string.IsNullOrEmpty(scenePath))
                {
                    sceneToUnload = SceneManager.GetSceneByPath(scenePath);
                }
                else
                {
                    sceneToUnload = SceneManager.GetSceneByName(sceneName);
                }

                if (!sceneToUnload.IsValid())
                {
                    string identifier = !string.IsNullOrEmpty(scenePath) ? $"path '{scenePath}'" : $"name '{sceneName}'";
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Scene with {identifier} is not currently loaded",
                        "not_found_error"
                    );
                }

                // Check if this is the only loaded scene
                if (SceneManager.sceneCount <= 1)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        "Cannot unload the only loaded scene. Load another scene first or create a new scene",
                        "validation_error"
                    );
                }

                string unloadedSceneName = sceneToUnload.name;
                string unloadedScenePath = sceneToUnload.path;
                bool wasDirty = sceneToUnload.isDirty;

                // If scene has unsaved changes, save it first
                if (wasDirty)
                {
                    bool savePrompt = parameters["saveIfDirty"]?.ToObject<bool?>() ?? true;
                    if (savePrompt && !string.IsNullOrEmpty(unloadedScenePath))
                    {
                        EditorSceneManager.SaveScene(sceneToUnload);
                    }
                }

                // Close/unload the scene
                bool success = EditorSceneManager.CloseScene(sceneToUnload, removeScene);

                if (!success)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Failed to unload scene '{unloadedSceneName}'",
                        "unload_error"
                    );
                }

                McpLogger.LogInfo($"Unloaded scene '{unloadedSceneName}' (path: '{unloadedScenePath}')");

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Successfully unloaded scene '{unloadedSceneName}'",
                    ["sceneName"] = unloadedSceneName,
                    ["scenePath"] = unloadedScenePath,
                    ["wasDirty"] = wasDirty,
                    ["removed"] = removeScene
                };
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error unloading scene: {ex.Message}",
                    "scene_unload_error"
                );
            }
        }
    }
}
