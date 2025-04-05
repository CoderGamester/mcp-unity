using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using McpUnity.Unity;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for updating component fields on GameObjects
    /// </summary>
    public class UpdateComponentTool : McpToolBase
    {
        public UpdateComponentTool()
        {
            Name = "update_component";
            Description = "Updates component fields on a GameObject or adds it to the GameObject if it does not contain the component";
        }
        
        /// <summary>
        /// Execute the UpdateComponent tool with the provided parameters synchronously
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
            }
            // Then try by path
            else if (parameters["objectPath"] != null)
            {
                string objectPath = parameters["objectPath"].ToObject<string>();
                targetObject = GameObject.Find(objectPath);
            }

            // Validate GameObject
            if (targetObject == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"GameObject with path '{parameters["objectPath"]}' or instance ID {parameters["instanceId"]} not found",
                    "object_not_found"
                );
            }

            // Get component name and validate
            string componentName = parameters["componentName"]?.ToObject<string>();
            if (string.IsNullOrEmpty(componentName))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Required parameter 'componentName' not provided",
                    "validation_error"
                );
            }

            // Find component type
            Type componentType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name == componentName && typeof(Component).IsAssignableFrom(t));

            if (componentType == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Component type '{componentName}' not found in Unity",
                    "component_not_found"
                );
            }

            // Get or add component
            Component component = targetObject.GetComponent(componentType) ?? targetObject.AddComponent(componentType);

            // Get component data
            JObject componentData = parameters["componentData"]?.ToObject<JObject>();
            if (componentData == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Required parameter 'componentData' not provided",
                    "validation_error"
                );
            }

            // Update component fields
            List<JObject> updateErrors = new List<JObject>();
            if (componentData != null && componentData.Count > 0)
            {
                updateErrors = UpdateComponentData(component, componentData);
            }
            
            // Ensure changes are saved if there were no errors
            if (updateErrors.Count == 0)
            {
                EditorUtility.SetDirty(targetObject);
                if (PrefabUtility.IsPartOfAnyPrefab(targetObject))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                }
            }
            
            // Create the response based on success/failure
            bool success = updateErrors.Count == 0;
            string message;
            if (success)
            {
                message = targetObject.GetComponent(componentType) == null
                    ? $"Successfully added component '{componentName}' to GameObject '{targetObject.name}' and updated its data"
                    : $"Successfully updated component '{componentName}' on GameObject '{targetObject.name}'";
            }
            else
            {
                message = $"Failed to fully update component '{componentName}' on GameObject '{targetObject.name}'. See errors for details.";
            }

            JObject response = new JObject
            {
                ["success"] = success,
                ["type"] = "text",
                ["message"] = message
            };

            if (!success)
            {
                response["errors"] = new JArray(updateErrors);
            }

            return response;
        }
        
        /// <summary>
        /// Find a GameObject by its hierarchy path
        /// </summary>
        /// <param name="path">The path to the GameObject (e.g. "Canvas/Panel/Button")</param>
        /// <returns>The GameObject if found, null otherwise</returns>
        private GameObject FindGameObjectByPath(string path)
        {
            // Split the path by '/'
            string[] pathParts = path.Split('/');
            
            // If the path is empty, return null
            if (pathParts.Length == 0)
            {
                return null;
            }
            
            // Search through all root GameObjects in all scenes
            foreach (GameObject rootObj in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (rootObj.name == pathParts[0])
                {
                    // Found the root object, now traverse down the path
                    GameObject current = rootObj;
                    
                    // Start from index 1 since we've already matched the root
                    for (int i = 1; i < pathParts.Length; i++)
                    {
                        Transform child = current.transform.Find(pathParts[i]);
                        if (child == null)
                        {
                            // Path segment not found
                            return null;
                        }
                        
                        // Move to the next level
                        current = child.gameObject;
                    }
                    
                    // If we got here, we found the full path
                    return current;
                }
            }
            
            // Not found
            return null;
        }
        
        /// <summary>
        /// Find a component type by name
        /// </summary>
        /// <param name="componentName">The name of the component type</param>
        /// <returns>The component type, or null if not found</returns>
        private Type FindComponentType(string componentName)
        {
            // First try direct match
            Type type = Type.GetType(componentName);
            if (type != null && typeof(Component).IsAssignableFrom(type))
            {
                return type;
            }
            
            // Try common Unity namespaces
            string[] commonNamespaces = new string[] 
            {
                "UnityEngine",
                "UnityEngine.UI",
                "UnityEngine.EventSystems",
                "UnityEngine.Animations",
                "UnityEngine.Rendering",
                "TMPro"
            };
            
            foreach (string ns in commonNamespaces)
            {
                type = Type.GetType($"{ns}.{componentName}, UnityEngine");
                if (type != null && typeof(Component).IsAssignableFrom(type))
                {
                    return type;
                }
            }
            
            // Try assemblies search
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (Type t in assembly.GetTypes())
                    {
                        if (t.Name == componentName && typeof(Component).IsAssignableFrom(t))
                        {
                            return t;
                        }
                    }
                }
                catch (Exception)
                {
                    // Some assemblies might throw exceptions when getting types
                    continue;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Update component data based on the provided JObject
        /// </summary>
        /// <param name="component">The component to update</param>
        /// <param name="componentData">The data to apply to the component</param>
        /// <returns>A list of JObjects detailing any errors encountered. Empty list means success.</returns>
        private List<JObject> UpdateComponentData(Component component, JObject componentData)
        {
            List<JObject> errors = new List<JObject>();
            if (component == null || componentData == null)
            {
                errors.Add(new JObject { ["name"] = "component", ["reason"] = "Component or data was null" });
                return errors;
            }
            
            Type componentType = component.GetType();
            
            // Record object for undo
            Undo.RecordObject(component, $"Update {componentType.Name} fields");
            
            // Process each field in the component data
            foreach (var property in componentData.Properties())
            {
                string memberName = property.Name;
                JToken memberValue = property.Value;
                
                // Skip null values
                if (memberValue.Type == JTokenType.Null)
                {
                    continue;
                }

                try
                {
                    PropertyInfo propInfo = componentType.GetProperty(memberName, 
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                    if (propInfo != null && propInfo.CanWrite)
                    {
                        object value = ConvertJTokenToValue(memberValue, propInfo.PropertyType);
                        propInfo.SetValue(component, value);
                        continue; // Successfully set property
                    }

                    // If no writable property found, try fields
                    FieldInfo fieldInfo = componentType.GetField(memberName, 
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        
                    if (fieldInfo != null)
                    {
                        object value = ConvertJTokenToValue(memberValue, fieldInfo.FieldType);
                        fieldInfo.SetValue(component, value);
                        continue; // Successfully set field
                    }

                    // If neither property nor field was found or usable
                    errors.Add(new JObject
                    {
                        ["name"] = memberName,
                        ["reason"] = propInfo != null ? "Property is read-only" : "Property or Field not found"
                    });
                }
                catch (Exception ex) // Catch errors during conversion or SetValue
                {
                    errors.Add(new JObject
                    {
                        ["name"] = memberName,
                        ["reason"] = $"Error setting value: {ex.Message}" // Include exception message
                    });
                }
            }
            
            return errors;
        }
        
        /// <summary>
        /// Convert a JToken to a value of the specified type
        /// </summary>
        /// <param name="token">The JToken to convert</param>
        /// <param name="targetType">The target type to convert to</param>
        /// <returns>The converted value</returns>
        private object ConvertJTokenToValue(JToken token, Type targetType)
        {
            if (token == null)
            {
                return null;
            }
            
            // Handle Unity Vector types
            if (targetType == typeof(Vector2) && token.Type == JTokenType.Object)
            {
                JObject vector = (JObject)token;
                return new Vector2(
                    vector["x"]?.ToObject<float>() ?? 0f,
                    vector["y"]?.ToObject<float>() ?? 0f
                );
            }
            
            if (targetType == typeof(Vector3) && token.Type == JTokenType.Object)
            {
                JObject vector = (JObject)token;
                return new Vector3(
                    vector["x"]?.ToObject<float>() ?? 0f,
                    vector["y"]?.ToObject<float>() ?? 0f,
                    vector["z"]?.ToObject<float>() ?? 0f
                );
            }
            
            if (targetType == typeof(Vector4) && token.Type == JTokenType.Object)
            {
                JObject vector = (JObject)token;
                return new Vector4(
                    vector["x"]?.ToObject<float>() ?? 0f,
                    vector["y"]?.ToObject<float>() ?? 0f,
                    vector["z"]?.ToObject<float>() ?? 0f,
                    vector["w"]?.ToObject<float>() ?? 0f
                );
            }
            
            if (targetType == typeof(Quaternion) && token.Type == JTokenType.Object)
            {
                JObject quaternion = (JObject)token;
                return new Quaternion(
                    quaternion["x"]?.ToObject<float>() ?? 0f,
                    quaternion["y"]?.ToObject<float>() ?? 0f,
                    quaternion["z"]?.ToObject<float>() ?? 0f,
                    quaternion["w"]?.ToObject<float>() ?? 1f
                );
            }
            
            if (targetType == typeof(Color) && token.Type == JTokenType.Object)
            {
                JObject color = (JObject)token;
                return new Color(
                    color["r"]?.ToObject<float>() ?? 0f,
                    color["g"]?.ToObject<float>() ?? 0f,
                    color["b"]?.ToObject<float>() ?? 0f,
                    color["a"]?.ToObject<float>() ?? 1f
                );
            }
            
            if (targetType == typeof(Bounds) && token.Type == JTokenType.Object)
            {
                JObject bounds = (JObject)token;
                Vector3 center = bounds["center"]?.ToObject<Vector3>() ?? Vector3.zero;
                Vector3 size = bounds["size"]?.ToObject<Vector3>() ?? Vector3.one;
                return new Bounds(center, size);
            }
            
            if (targetType == typeof(Rect) && token.Type == JTokenType.Object)
            {
                JObject rect = (JObject)token;
                return new Rect(
                    rect["x"]?.ToObject<float>() ?? 0f,
                    rect["y"]?.ToObject<float>() ?? 0f,
                    rect["width"]?.ToObject<float>() ?? 0f,
                    rect["height"]?.ToObject<float>() ?? 0f
                );
            }
            
            // Handle UnityEngine.Object types;
            if (targetType == typeof(UnityEngine.Object))
            {
                return token.ToObject<UnityEngine.Object>();
            }
            
            // Handle enum types
            if (targetType.IsEnum)
            {
                // If JToken is a string, try to parse as enum name
                if (token.Type == JTokenType.String)
                {
                    string enumName = token.ToObject<string>();
                    if (Enum.TryParse(targetType, enumName, true, out object result))
                    {
                        return result;
                    }
                    
                    // If parsing fails, try to convert numeric value
                    if (int.TryParse(enumName, out int enumValue))
                    {
                        return Enum.ToObject(targetType, enumValue);
                    }
                }
                // If JToken is a number, convert directly to enum
                else if (token.Type == JTokenType.Integer)
                {
                    return Enum.ToObject(targetType, token.ToObject<int>());
                }
            }
            
            // For other types, use JToken's ToObject method
            try
            {
                return token.ToObject(targetType);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP Unity] Error converting value '{token}' to type {targetType.Name}: {ex.Message}");
                // Throw exception instead of returning null
                throw new InvalidCastException($"Could not convert value '{token}' to type {targetType.Name}", ex);
            }
        }
    }
}
