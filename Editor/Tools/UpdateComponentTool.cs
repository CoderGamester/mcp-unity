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

            // Validate and update each field
            var invalidFields = new JArray();
            foreach (var field in componentData.Properties())
            {
                try
                {
                    // Get the property or field info
                    PropertyInfo prop = componentType.GetProperty(field.Name, 
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    FieldInfo fieldInfo = componentType.GetField(field.Name, 
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                    if (prop == null && fieldInfo == null)
                    {
                        invalidFields.Add(new JObject
                        {
                            ["name"] = field.Name,
                            ["reason"] = "Field not found on component"
                        });
                        continue;
                    }

                    // Get the type we need to convert to
                    Type targetType = prop?.PropertyType ?? fieldInfo.FieldType;
                    
                    try
                    {
                        // Convert the value to the correct type
                        object value = field.Value.ToObject(targetType);

                        // Set the value
                        if (prop != null && prop.CanWrite)
                        {
                            prop.SetValue(component, value);
                        }
                        else if (fieldInfo != null)
                        {
                            fieldInfo.SetValue(component, value);
                        }
                        else
                        {
                            invalidFields.Add(new JObject
                            {
                                ["name"] = field.Name,
                                ["reason"] = "Field is read-only"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        invalidFields.Add(new JObject
                        {
                            ["name"] = field.Name,
                            ["reason"] = $"Invalid value format: {ex.Message}"
                        });
                    }
                }
                catch (Exception ex)
                {
                    invalidFields.Add(new JObject
                    {
                        ["name"] = field.Name,
                        ["reason"] = $"Error accessing field: {ex.Message}"
                    });
                }
            }

            // Create response
            var response = new JObject
            {
                ["success"] = true,
                ["message"] = invalidFields.Count > 0
                    ? $"Updated component '{componentName}' with some invalid fields"
                    : $"Successfully updated component '{componentName}' on GameObject '{targetObject.name}'",
                ["componentName"] = componentName,
                ["gameObjectName"] = targetObject.name,
                ["instanceId"] = targetObject.GetInstanceID()
            };

            if (invalidFields.Count > 0)
            {
                response["invalidFields"] = invalidFields;
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
        /// <returns>True if the component was updated successfully</returns>
        private bool UpdateComponentData(Component component, JObject componentData)
        {
            if (component == null || componentData == null)
            {
                return false;
            }
            
            Type componentType = component.GetType();
            bool anySuccess = false;
            
            // Record object for undo
            Undo.RecordObject(component, $"Update {componentType.Name} fields");
            
            // Process each field in the component data
            foreach (var property in componentData.Properties())
            {
                string fieldName = property.Name;
                JToken fieldValue = property.Value;
                
                // Skip null values
                if (fieldValue.Type == JTokenType.Null)
                {
                    continue;
                }
                
                // Try to update field
                FieldInfo fieldInfo = componentType.GetField(fieldName, 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    
                if (fieldInfo != null)
                {
                    object value = ConvertJTokenToValue(fieldValue, fieldInfo.FieldType);
                    fieldInfo.SetValue(component, value);
                    anySuccess = true;
                    continue;
                }
                else
                {
                    Debug.LogWarning($"[MCP Unity] Field '{fieldName}' not found on component '{componentType.Name}'");
                }
            }
            
            return anySuccess;
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
                Debug.LogError($"[MCP Unity] Error converting value to type {targetType.Name}: {ex.Message}");
                return null;
            }
        }
    }
}
