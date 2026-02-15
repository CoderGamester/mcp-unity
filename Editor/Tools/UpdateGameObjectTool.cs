using System;
using UnityEngine;
using UnityEditor;
using McpUnity.Utils; // For GameObjectHierarchyCreator and McpLogger
using McpUnity.Unity; // For McpUnitySocketHandler
using Newtonsoft.Json.Linq; // For JObject

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for updating or creating a GameObject in the Unity Editor.
    /// Supports setting name, tag, layer, active state, and static state by instance ID or hierarchy path.
    /// Returns a JObject result similar to UpdateComponentTool for consistency.
    /// </summary>
    public class UpdateGameObjectTool : McpToolBase
    {
        public UpdateGameObjectTool()
        {
            Name = "update_gameobject";
            Description = "Updates or creates a GameObject and its properties (name, tag, layer, active state, static state) based on instance ID or object path.";
            IsAsync = false; // Operations are expected to be quick
        }

        /// <summary>
        /// Executes the update or creation of a GameObject based on the provided parameters.
        /// </summary>
        /// <param name="parameters">A JObject containing: instanceId (int?), objectPath (string), name (string), tag (string), layer (int?), isActiveSelf (bool?), isStatic (bool?)</param>
        /// <returns>JObject with success, message, instanceId, name, and path fields (see UpdateComponentTool for format)</returns>
        public override JObject Execute(JObject parameters)
        {
            // Extract parameters from JObject
            int? instanceId = parameters["instanceId"]?.ToObject<int?>();
            string objectPath = parameters["objectPath"]?.ToObject<string>();
            JObject gameObjectData = parameters["gameObjectData"] as JObject;

            string newName = gameObjectData? ["name"]?.ToObject<string>();
            string newTag = gameObjectData? ["tag"]?.ToObject<string>();
            int? newLayer = gameObjectData? ["layer"]?.ToObject<int?>();
            bool? newIsActiveSelf = (gameObjectData?["activeSelf"] ?? gameObjectData?["isActiveSelf"])?.ToObject<bool?>();
            bool? newIsStatic = (gameObjectData?["isStatic"] ?? gameObjectData?["static"])?.ToObject<bool?>();

            GameObject targetGameObject = null;
            string identifierInfo = "";

            // Identify or create the GameObject by instanceId or objectPath
            if (instanceId.HasValue)
            {
                targetGameObject = EditorUtility.InstanceIDToObject(instanceId.Value) as GameObject;
                identifierInfo = $"instance ID {instanceId.Value}";
            }
            else if (!string.IsNullOrEmpty(objectPath))
            {
                // Will create the GameObject if it doesn't exist
                targetGameObject = GameObjectHierarchyCreator.FindOrCreateHierarchicalGameObject(objectPath);
                identifierInfo = $"path '{objectPath}'";
            }
            else
            {
                // Neither instanceId nor objectPath was provided
                return McpUnitySocketHandler.CreateErrorResponse("Either 'instanceId' or 'objectPath' must be provided.", "validation_error");
            }

            // Check if we could not identify or create the GameObject
            if (targetGameObject == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse($"Target GameObject could not be identified or created using {identifierInfo}.", "unknown_error");
            }

            // Record for undo in Unity Editor
            Undo.RecordObject(targetGameObject, "Update GameObject Properties");
            bool propertiesUpdated = false;
            string originalNameForLog = targetGameObject.name;

            // Update name if provided and different
            if (!string.IsNullOrEmpty(newName) && targetGameObject.name != newName)
            {
                targetGameObject.name = newName;
                propertiesUpdated = true;
            }

            // Update tag if provided and different, warn if tag doesn't exist
            if (!string.IsNullOrEmpty(newTag))
            {
                bool tagExists = Array.Exists(UnityEditorInternal.InternalEditorUtility.tags, t => t.Equals(newTag));
                if (!tagExists)
                {
                    McpLogger.LogWarning($"UpdateGameObjectTool: Tag '{newTag}' does not exist for GameObject '{originalNameForLog}'. Tag not changed. Please create the tag in Unity's Tag Manager.");
                }
                else if (!targetGameObject.CompareTag(newTag))
                {
                    targetGameObject.tag = newTag;
                    propertiesUpdated = true;
                }
            }

            // Update layer if provided and valid
            if (newLayer.HasValue)
            {
                if (newLayer.Value < 0 || newLayer.Value > 31)
                {
                    McpLogger.LogWarning($"UpdateGameObjectTool: Invalid layer value {newLayer.Value} for GameObject '{originalNameForLog}'. Layer must be between 0 and 31. Layer not changed.");
                }
                else if (targetGameObject.layer != newLayer.Value)
                {
                    targetGameObject.layer = newLayer.Value;
                    propertiesUpdated = true;
                }
            }

            // Update active state if provided and different
            if (newIsActiveSelf.HasValue && targetGameObject.activeSelf != newIsActiveSelf.Value)
            {
                targetGameObject.SetActive(newIsActiveSelf.Value);
                propertiesUpdated = true;
            }

            // Update static state if provided and different
            if (newIsStatic.HasValue && targetGameObject.isStatic != newIsStatic.Value)
            {
                targetGameObject.isStatic = newIsStatic.Value;
                propertiesUpdated = true;
            }

            // Mark as dirty if any property was changed
            if (propertiesUpdated)
            {
                EditorUtility.SetDirty(targetGameObject);
            }

            // Compose result message and return as JObject (like UpdateComponentTool)
            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = propertiesUpdated
                    ? $"GameObject '{targetGameObject.name}' (identified by {identifierInfo}) updated successfully."
                    : $"No properties were changed for GameObject '{targetGameObject.name}' (identified by {identifierInfo}).",
                ["instanceId"] = targetGameObject.GetInstanceID(),
                ["name"] = targetGameObject.name,
                ["path"] = GetGameObjectPath(targetGameObject)
            };
        }

        /// <summary>
        /// Utility to get the hierarchy path of a GameObject (root/child/.../target)
        /// </summary>
        /// <param name="obj">The GameObject to get the path for</param>
        /// <returns>Hierarchy path as string</returns>
        private static string GetGameObjectPath(GameObject obj)
        {
            if (obj == null) return null;
            string path = "/" + obj.name;
            while (obj.transform.parent != null)
            {
                obj = obj.transform.parent.gameObject;
                path = "/" + obj.name + path;
            }
            return path;
        }
    }
}
