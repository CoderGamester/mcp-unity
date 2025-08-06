using System;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for creating prefabs with optional MonoBehaviour scripts
    /// </summary>
    public class CreatePrefabTool : McpToolBase
    {
        public CreatePrefabTool()
        {
            Name = "create_prefab";
            Description = "Creates a prefab with optional MonoBehaviour script and serialized field values";
        }
        
        /// <summary>
        /// Execute the CreatePrefab tool with the provided parameters
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            // Extract parameters
            string scriptName = parameters["scriptName"]?.ToObject<string>();
            string prefabName = parameters["prefabName"]?.ToObject<string>();
            JObject fieldValues = parameters["fieldValues"]?.ToObject<JObject>();
            
            // Validate required parameters
            if (string.IsNullOrEmpty(prefabName))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Required parameter 'prefabName' not provided", 
                    "validation_error"
                );
            }
            
            // Create a temporary GameObject
            GameObject tempObject = new GameObject(prefabName);

            // Add component if provided
            if (!string.IsNullOrEmpty(scriptName))
            {
                try
                {
                    // Add component
                    Component component = AddComponent(tempObject, scriptName);
            
                    // Apply field values if provided and component exists
                    ApplyFieldValues(fieldValues, component);
                }
                catch (Exception ex)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Failed to add component '{scriptName}' to GameObject", 
                        "component_error"
                    );
                }
            }
            
            // For safety, we'll create a unique name if prefab already exists
            int counter = 1;
            string prefabPath = $"{prefabName}.prefab";
            while (AssetDatabase.AssetPathToGUID(prefabPath) != "")
            {
                prefabPath = $"{prefabName}_{counter}.prefab";
                counter++;
            }
            
            // Create the prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(tempObject, prefabPath);
            
            // Clean up temporary object
            UnityEngine.Object.DestroyImmediate(tempObject);
            
            // Refresh the asset database
            AssetDatabase.Refresh();
            
            // Log the action
            McpLogger.LogInfo($"Created prefab '{prefab.name}' at path '{prefabPath}' from script '{scriptName}'");
            
            // Create the response
            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = $"Successfully created prefab '{prefab.name}' at path '{prefabPath}'",
                ["prefabPath"] = prefabPath
            };
        }

        private Component AddComponent(GameObject gameObject, string scriptName)
        {
            // Find the script type
            Type scriptType = Type.GetType($"{scriptName}, Assembly-CSharp");
            if (scriptType == null)
            {
                // Try with just the class name
                scriptType = Type.GetType(scriptName);
            }
                
            if (scriptType == null)
            {
                // Try to find the type using AppDomain
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    scriptType = assembly.GetType(scriptName);
                    if (scriptType != null)
                        break;
                }
            }
                
            // Throw an error if the type was not found
            if (scriptType == null)
            {
                return null;
            }
                
            // Check if the type is a MonoBehaviour
            if (!typeof(MonoBehaviour).IsAssignableFrom(scriptType))
            {
                return null;
            }
            
            return gameObject.AddComponent(scriptType);
        }

        private void ApplyFieldValues(JObject fieldValues, Component component)
        {
            // Apply field values if provided and component exists
            if (fieldValues == null || fieldValues.Count == 0)
            {
                return;
            }
            
            Undo.RecordObject(component, "Set field values");
                
            foreach (var property in fieldValues.Properties())
            {
                // Get the field/property info
                var fieldInfo = component.GetType().GetField(property.Name, 
                    System.Reflection.BindingFlags.Public | 
                    System.Reflection.BindingFlags.NonPublic | 
                    System.Reflection.BindingFlags.Instance);
                            
                if (fieldInfo != null)
                {
                    // Set field value
                    object value = property.Value.ToObject(fieldInfo.FieldType);
                    fieldInfo.SetValue(component, value);
                }
                else
                {
                    // Try property
                    var propInfo = component.GetType().GetProperty(property.Name, 
                        System.Reflection.BindingFlags.Public | 
                        System.Reflection.BindingFlags.NonPublic | 
                        System.Reflection.BindingFlags.Instance);
                                
                    if (propInfo != null && propInfo.CanWrite)
                    {
                        object value = property.Value.ToObject(propInfo.PropertyType);
                        propInfo.SetValue(component, value);
                    }
                }
            }
        }
    }
}
