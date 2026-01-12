using System;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for getting information about the active Unity scene
    /// </summary>
    public class GetSceneInfoTool : McpToolBase
    {
        public GetSceneInfoTool()
        {
            Name = "get_scene_info";
            Description = "Gets information about the active scene including name, path, dirty state, root object count, and loaded state";
        }

        /// <summary>
        /// Execute the GetSceneInfo tool with the provided parameters
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                Scene activeScene = SceneManager.GetActiveScene();

                if (!activeScene.IsValid())
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        "No valid active scene",
                        "validation_error"
                    );
                }

                // Get all loaded scenes info
                int loadedSceneCount = SceneManager.sceneCount;
                var loadedScenes = new JArray();

                for (int i = 0; i < loadedSceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    loadedScenes.Add(new JObject
                    {
                        ["name"] = scene.name,
                        ["path"] = scene.path,
                        ["buildIndex"] = scene.buildIndex,
                        ["isLoaded"] = scene.isLoaded,
                        ["isDirty"] = scene.isDirty,
                        ["rootCount"] = scene.isLoaded ? scene.rootCount : 0,
                        ["isActive"] = scene == activeScene
                    });
                }

                var result = new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Active scene: '{activeScene.name}'",
                    ["activeScene"] = new JObject
                    {
                        ["name"] = activeScene.name,
                        ["path"] = activeScene.path,
                        ["buildIndex"] = activeScene.buildIndex,
                        ["isDirty"] = activeScene.isDirty,
                        ["isLoaded"] = activeScene.isLoaded,
                        ["rootCount"] = activeScene.isLoaded ? activeScene.rootCount : 0
                    },
                    ["loadedSceneCount"] = loadedSceneCount,
                    ["loadedScenes"] = loadedScenes
                };

                McpLogger.LogInfo($"Retrieved scene info for active scene '{activeScene.name}'");

                return result;
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error getting scene info: {ex.Message}",
                    "scene_info_error"
                );
            }
        }
    }
}
