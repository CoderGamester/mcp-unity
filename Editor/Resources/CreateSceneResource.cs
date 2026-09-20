using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Utils;

namespace McpUnity.Resources
{
    /// <summary>
    /// Resource for creating a new Unity scene
    /// </summary>
    public class CreateSceneResource : McpResourceBase
    {
        public CreateSceneResource()
        {
            Name = "create_scene";
            Description = "Creates a new scene and saves it to the specified path";
            Uri = "unity://scenes/create";
        }

        /// <summary>
        /// Create a new scene with the provided parameters
        /// </summary>
        /// <param name="parameters">Resource parameters as a JObject</param>
        /// <returns>A JObject containing the result</returns>
        public override JObject Fetch(JObject parameters)
        {
            // Parameters
            string sceneName = parameters["sceneName"]?.ToObject<string>();
            string folderPath = parameters["folderPath"]?.ToObject<string>();
            bool addToBuildSettings = parameters["addToBuildSettings"]?.ToObject<bool?>() ?? false;
            bool makeActive = parameters["makeActive"]?.ToObject<bool?>() ?? true;

            if (string.IsNullOrEmpty(sceneName))
            {
                return new JObject
                {
                    ["success"] = false,
                    ["message"] = "Required parameter 'sceneName' not provided",
                    ["error"] = new JObject
                    {
                        ["type"] = "validation_error",
                        ["message"] = "Required parameter 'sceneName' not provided"
                    }
                };
            }

            // Default folder path
            if (string.IsNullOrEmpty(folderPath))
            {
                folderPath = "Assets";
            }

            // Ensure folder exists
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                string[] parts = folderPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                string current = parts.Length > 0 && parts[0] == "Assets" ? "Assets" : "Assets";
                for (int i = 0; i < parts.Length; i++)
                {
                    if (i == 0 && parts[i] == "Assets") continue;
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, parts[i]);
                    }
                    current = next;
                }
            }

            // Create unique path for the scene
            string basePath = folderPath.TrimEnd('/');
            string scenePath = AssetDatabase.GenerateUniqueAssetPath($"{basePath}/{sceneName}.unity");

            try
            {
                var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

                bool saved = EditorSceneManager.SaveScene(newScene, scenePath);
                if (!saved)
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["message"] = $"Failed to save scene at '{scenePath}'",
                        ["error"] = new JObject
                        {
                            ["type"] = "save_error",
                            ["message"] = $"Failed to save scene at '{scenePath}'"
                        }
                    };
                }

                AssetDatabase.Refresh();

                // Make the scene active if requested
                if (makeActive)
                {
                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                }

                // Optionally add to build settings
                if (addToBuildSettings)
                {
                    AddSceneToBuildSettings(scenePath);
                }

                McpLogger.LogInfo($"Created scene '{sceneName}' at path '{scenePath}'");

                return new JObject
                {
                    ["success"] = true,
                    ["message"] = $"Successfully created scene '{sceneName}' at path '{scenePath}'",
                    ["scenePath"] = scenePath
                };
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["message"] = $"Error creating scene: {ex.Message}",
                    ["error"] = new JObject
                    {
                        ["type"] = "scene_creation_error",
                        ["message"] = $"Error creating scene: {ex.Message}"
                    }
                };
            }
        }

        private void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes;

            foreach (var s in scenes)
            {
                if (s.path == scenePath)
                {
                    return;
                }
            }

            var newList = new EditorBuildSettingsScene[scenes.Length + 1];
            for (int i = 0; i < scenes.Length; i++)
            {
                newList[i] = scenes[i];
            }
            newList[newList.Length - 1] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = newList;
        }
    }
}