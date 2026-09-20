using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json.Linq;
using McpUnity.Utils;

namespace McpUnity.Resources
{
    /// <summary>
    /// Resource for getting information about the active scene
    /// </summary>
    public class GetSceneInfoResource : McpResourceBase
    {
        public GetSceneInfoResource()
        {
            Name = "get_scene_info";
            Description = "Gets information about the active scene and all loaded scenes";
            Uri = "unity://scene/info";
        }

        /// <summary>
        /// Fetch scene information
        /// </summary>
        /// <param name="parameters">Resource parameters as a JObject</param>
        /// <returns>A JObject containing scene information</returns>
        public override JObject Fetch(JObject parameters)
        {
            try
            {
                var sceneInfo = new JObject();
                var activeScene = SceneManager.GetActiveScene();

                sceneInfo["activeScene"] = new JObject
                {
                    ["name"] = activeScene.name,
                    ["path"] = activeScene.path,
                    ["buildIndex"] = activeScene.buildIndex,
                    ["isLoaded"] = activeScene.isLoaded,
                    ["isDirty"] = activeScene.isDirty,
                    ["rootObjectCount"] = activeScene.GetRootGameObjects()?.Length ?? 0
                };

                var loadedScenes = new JArray();
                int sceneCount = SceneManager.sceneCount;
                for (int i = 0; i < sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    loadedScenes.Add(new JObject
                    {
                        ["name"] = scene.name,
                        ["path"] = scene.path,
                        ["buildIndex"] = scene.buildIndex,
                        ["isLoaded"] = scene.isLoaded,
                        ["isDirty"] = scene.isDirty
                    });
                }

                sceneInfo["loadedScenes"] = loadedScenes;
                sceneInfo["loadedSceneCount"] = sceneCount;

                return new JObject
                {
                    ["success"] = true,
                    ["message"] = "Successfully retrieved scene info",
                    ["sceneInfo"] = sceneInfo
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["message"] = $"Error getting scene info: {ex.Message}",
                    ["error"] = new JObject
                    {
                        ["type"] = "scene_info_error",
                        ["message"] = $"Error getting scene info: {ex.Message}"
                    }
                };
            }
        }
    }
}