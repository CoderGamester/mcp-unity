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
    /// Tool for saving a Unity scene
    /// </summary>
    public class SaveSceneTool : McpToolBase
    {
        public SaveSceneTool()
        {
            Name = "save_scene";
            Description = "Saves the current active scene. Optionally saves to a new path (Save As)";
        }

        /// <summary>
        /// Execute the SaveScene tool with the provided parameters
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            string scenePath = parameters["scenePath"]?.ToObject<string>();
            bool saveAs = parameters["saveAs"]?.ToObject<bool?>() ?? false;

            try
            {
                Scene activeScene = SceneManager.GetActiveScene();

                if (!activeScene.IsValid())
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        "No valid active scene to save",
                        "validation_error"
                    );
                }

                string targetPath;

                if (saveAs || !string.IsNullOrEmpty(scenePath))
                {
                    // Save As mode - need a path
                    if (string.IsNullOrEmpty(scenePath))
                    {
                        return McpUnitySocketHandler.CreateErrorResponse(
                            "Parameter 'scenePath' is required when 'saveAs' is true",
                            "validation_error"
                        );
                    }

                    // Ensure the path has .unity extension
                    if (!scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    {
                        scenePath += ".unity";
                    }

                    // Ensure the path starts with Assets/
                    if (!scenePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    {
                        scenePath = "Assets/" + scenePath;
                    }

                    // Ensure the directory exists
                    string directory = System.IO.Path.GetDirectoryName(scenePath);
                    if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
                    {
                        CreateFolderHierarchy(directory);
                    }

                    targetPath = scenePath;
                }
                else
                {
                    // Save to current path
                    targetPath = activeScene.path;

                    if (string.IsNullOrEmpty(targetPath))
                    {
                        return McpUnitySocketHandler.CreateErrorResponse(
                            "Scene has no path. Use 'scenePath' parameter to specify where to save the scene",
                            "validation_error"
                        );
                    }
                }

                bool saved = EditorSceneManager.SaveScene(activeScene, targetPath);

                if (!saved)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Failed to save scene to '{targetPath}'",
                        "save_error"
                    );
                }

                AssetDatabase.Refresh();

                McpLogger.LogInfo($"Saved scene '{activeScene.name}' to path '{targetPath}'");

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Successfully saved scene '{activeScene.name}' to '{targetPath}'",
                    ["scenePath"] = targetPath,
                    ["sceneName"] = activeScene.name
                };
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error saving scene: {ex.Message}",
                    "scene_save_error"
                );
            }
        }

        /// <summary>
        /// Creates folder hierarchy for the given path
        /// </summary>
        private void CreateFolderHierarchy(string folderPath)
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
    }
}
