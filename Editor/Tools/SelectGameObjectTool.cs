using System;
using System.Threading.Tasks;
using McpUnity.Unity;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for selecting GameObjects in the Unity Editor
    /// </summary>
    public class SelectGameObjectTool : McpToolBase
    {
        public SelectGameObjectTool()
        {
            Name = "select_gameobject";
            Description = "Sets the selected GameObject in the Unity editor by path or instance ID";
        }
        
        /// <summary>
        /// Execute the SelectGameObject tool with the provided parameters synchronously
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            GameObject targetObject = null;

            // Try to find by instance ID first
            if (parameters["instanceId"] != null)
            {
                int instanceId = parameters["instanceId"].ToObject<int>();
                targetObject = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
                
                if (targetObject == null)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"GameObject with instance ID {instanceId} not found",
                        "object_not_found"
                    );
                }
            }
            // Then try by path/name
            else if (parameters["objectPath"] != null)
            {
                string objectPath = parameters["objectPath"].ToObject<string>();
                
                if (string.IsNullOrEmpty(objectPath))
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        "Object path cannot be empty",
                        "validation_error"
                    );
                }

                // Try to find by full path first
                targetObject = GameObject.Find(objectPath);

                // If not found, try to find by name
                if (targetObject == null)
                {
                    var allObjects = UnityEngine.Resources.FindObjectsOfTypeAll<GameObject>();
                    foreach (var obj in allObjects)
                    {
                        if (obj.name == objectPath)
                        {
                            targetObject = obj;
                            break;
                        }
                    }
                }

                if (targetObject == null)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"GameObject with path or name '{objectPath}' not found",
                        "object_not_found"
                    );
                }
            }
            else
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Either instanceId or objectPath must be provided",
                    "validation_error"
                );
            }

            // Select the object
            Selection.activeGameObject = targetObject;

            return new JObject
            {
                ["success"] = true,
                ["message"] = $"Successfully selected GameObject: {targetObject.name}",
                ["instanceId"] = targetObject.GetInstanceID()
            };
        }
    }
}
