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
            // Extract parameters
            string objectPath = parameters["objectPath"]?.ToObject<string>();
            int? instanceId = parameters["instanceId"]?.ToObject<int?>();
            
            // Validate parameters - require either objectPath or instanceId
            if (string.IsNullOrEmpty(objectPath) && !instanceId.HasValue)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Required parameter 'objectPath' or 'instanceId' not provided", 
                    "validation_error"
                );
            }
            
            GameObject foundObject = null;
            string identifier = "";

            // First try to find by instance ID if provided
            if (instanceId.HasValue)
            {
                foundObject = EditorUtility.InstanceIDToObject(instanceId.Value) as GameObject;
                identifier = $"instance ID {instanceId.Value}";
            }
            // Otherwise, try to find by object path/name if provided
            else
            {
                // Try to find the object by path in the hierarchy
                foundObject = GameObject.Find(objectPath);
                identifier = $"path '{objectPath}'";
            }

            // Check if we actually found the object
            if (foundObject == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"GameObject with {identifier} not found",
                    "not_found_error"
                );
            }

            // Set the selection and ping the object
            Selection.activeGameObject = foundObject;
            EditorGUIUtility.PingObject(foundObject);
            
            // Log the selection
            Debug.Log($"[MCP Unity] Selected GameObject: {foundObject.name} (found by {identifier})");
            
            // Create the response with instanceId for tracking
            return new JObject
            {
                ["success"] = true,
                ["message"] = $"Successfully selected GameObject: {foundObject.name}",
                ["type"] = "text",
                ["instanceId"] = foundObject.GetInstanceID()
            };
        }
    }
}
