using McpUnity.Resources;
using McpUnity.Unity;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for retrieving detailed information about a specific GameObject.
    /// This tool provides the same functionality as the get_gameobject resource,
    /// but as a tool that can be invoked directly without URI template parameters.
    /// </summary>
    public class GetGameObjectTool : McpToolBase
    {
        public GetGameObjectTool()
        {
            Name = "get_gameobject";
            Description = "Retrieves detailed information about a specific GameObject by instance ID, name, or hierarchical path (e.g., \"Parent/Child/MyObject\"). Returns all component properties including Transform position, rotation, scale, and more.";
        }

        /// <summary>
        /// Execute the GetGameObject tool with the provided parameters
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject. Should include 'idOrName' which can be an instance ID, name, or path</param>
        /// <returns>A JObject containing the GameObject data</returns>
        public override JObject Execute(JObject parameters)
        {
            // Validate parameters
            if (parameters == null || !parameters.ContainsKey("idOrName"))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Missing required parameter: idOrName",
                    "validation_error"
                );
            }

            string idOrName = parameters["idOrName"]?.ToObject<string>();

            if (string.IsNullOrEmpty(idOrName))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Parameter 'idOrName' cannot be null or empty",
                    "validation_error"
                );
            }

            GameObject gameObject = null;

            // Try to parse as an instance ID first
            if (int.TryParse(idOrName, out int instanceId))
            {
                // Unity Instance IDs are typically negative, but we'll accept any integer
                UnityEngine.Object unityObject = EditorUtility.InstanceIDToObject(instanceId);
                gameObject = unityObject as GameObject;
            }
            else
            {
                // Otherwise, treat it as a name or hierarchical path
                gameObject = GameObject.Find(idOrName);
            }

            // Check if the GameObject was found
            if (gameObject == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"GameObject with '{idOrName}' reference not found. Make sure the GameObject exists and is loaded in the current scene(s).",
                    "not_found_error"
                );
            }

            // Convert the GameObject to a JObject using the resource's static method
            JObject gameObjectData = GetGameObjectResource.GameObjectToJObject(gameObject, true);

            // Create the response
            return new JObject
            {
                ["success"] = true,
                ["message"] = $"Retrieved GameObject data for '{gameObject.name}'",
                ["gameObject"] = gameObjectData,
                ["instanceId"] = gameObject.GetInstanceID()
            };
        }
    }
}


