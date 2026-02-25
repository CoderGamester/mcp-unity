using System;
using System.Reflection;
using McpUnity.Unity;
using McpUnity.Utils;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for updating component data in the Unity Editor
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
            // Extract parameters
            int? instanceId = parameters["instanceId"]?.ToObject<int?>();
            string objectPath = parameters["objectPath"]?.ToObject<string>();
            string componentName = parameters["componentName"]?.ToObject<string>();
            JObject componentData = parameters["componentData"] as JObject;
            
            // Validate parameters - require either instanceId or objectPath
            if (!instanceId.HasValue && string.IsNullOrEmpty(objectPath))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Either 'instanceId' or 'objectPath' must be provided", 
                    "validation_error"
                );
            }
            
            if (string.IsNullOrEmpty(componentName))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Required parameter 'componentName' not provided", 
                    "validation_error"
                );
            }
            
            // Find the GameObject by instance ID or path
            GameObject gameObject = null;
            string identifier = "unknown";
            
            if (instanceId.HasValue)
            {
                gameObject = EditorUtility.InstanceIDToObject(instanceId.Value) as GameObject;
                identifier = $"ID {instanceId.Value}";
            }
            else
            {
                // Find by path
                gameObject = GameObject.Find(objectPath);
                identifier = $"path '{objectPath}'";
                
                if (gameObject == null)
                {
                    // Try to find using the Unity Scene hierarchy path
                    gameObject = FindGameObjectByPath(objectPath);
                }
            }
                    
            if (gameObject == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"GameObject with path '{objectPath}' or instance ID {instanceId} not found", 
                    "not_found_error"
                );
            }
            
            McpLogger.LogInfo($"[MCP Unity] Updating component '{componentName}' on GameObject '{gameObject.name}' (found by {identifier})");
            
            // Try to find the component by name
            Component component = gameObject.GetComponent(componentName);
            
            // If component not found, try to add it
            if (component == null)
            {
                Type componentType = FindComponentType(componentName);
                if (componentType == null)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Component type '{componentName}' not found in Unity", 
                        "component_error"
                    );
                }
                
                component = Undo.AddComponent(gameObject, componentType);

                // Ensure changes are saved
                EditorUtility.SetDirty(gameObject);
                if (PrefabUtility.IsPartOfAnyPrefab(gameObject))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                }
                
                McpLogger.LogInfo($"[MCP Unity] Added component '{componentName}' to GameObject '{gameObject.name}'");
            }
            // Update component fields
            if (componentData != null && componentData.Count > 0)
            {
                bool success = UpdateComponentData(component, componentData, out string errorMessage);
                // If update failed, return error
                if (!success)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(errorMessage, "update_error");
                }

                // Ensure field changes are saved
                EditorUtility.SetDirty(gameObject);
                if (PrefabUtility.IsPartOfAnyPrefab(gameObject))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                }

            }

            // Create the response
            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = $"Successfully updated component '{componentName}' on GameObject '{gameObject.name}'"
            };
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
            GameObject[] rootGameObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            
            // If the path is empty, return null
            if (pathParts.Length == 0)
            {
                return null;
            }
            
            // Search through all root GameObjects in all scenes
            foreach (GameObject rootObj in rootGameObjects)
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
        /// Recursively search for a field through the type hierarchy
        /// </summary>
        /// <param name="type">The type to start searching from</param>
        /// <param name="fieldName">The name of the field to find</param>
        /// <returns>The FieldInfo if found, null otherwise</returns>
        private FieldInfo GetFieldRecursive(Type type, string fieldName)
        {
            while (type != null && type != typeof(object))
            {
                FieldInfo field = type.GetField(fieldName, 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (field != null) 
                    return field;
                type = type.BaseType;
            }
            return null;
        }

        /// <summary>
        /// Recursively search for a property through the type hierarchy
        /// </summary>
        /// <param name="type">The type to start searching from</param>
        /// <param name="propertyName">The name of the property to find</param>
        /// <returns>The PropertyInfo if found, null otherwise</returns>
        private PropertyInfo GetPropertyRecursive(Type type, string propertyName)
        {
            while (type != null && type != typeof(object))
            {
                PropertyInfo prop = type.GetProperty(propertyName, 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (prop != null) 
                    return prop;
                type = type.BaseType;
            }
            return null;
        }
        
        /// <summary>
        /// Update component data based on the provided JObject
        /// Uses SerializedObject API as primary method (handles base class fields and nested paths),
        /// with reflection as fallback for non-serialized properties.
        /// </summary>
        /// <param name="component">The component to update</param>
        /// <param name="componentData">The data to apply to the component</param>
        /// <param name="errorMessage">Error message if any fields failed to update</param>
        /// <returns>True if all fields were updated successfully</returns>
        private bool UpdateComponentData(Component component, JObject componentData, out string errorMessage)
        {
            errorMessage = "";
            
            if (component == null || componentData == null)
            {
                errorMessage = "Component or component data is null";
                return false;
            }

            Type componentType = component.GetType();
            bool fullSuccess = true;

            // Record object for undo
            Undo.RecordObject(component, $"Update {componentType.Name} fields");
            
            // Create SerializedObject for the primary approach
            SerializedObject serializedObject = new SerializedObject(component);
            
            // Process each field or property in the component data
            foreach (var property in componentData.Properties())
            {
                string fieldName = property.Name;
                JToken fieldValue = property.Value;
                
                // Skip null field names
                if (string.IsNullOrEmpty(fieldName))
                {
                    continue;
                }
                
                // Primary approach: Try SerializedObject API first
                // This handles base class fields and nested paths automatically
                SerializedProperty serializedProperty = serializedObject.FindProperty(fieldName);
                
                if (serializedProperty != null)
                {
                    SetSerializedPropertyValue(serializedProperty, fieldValue);
                    continue;
                }
                
                // Fallback: Use reflection with recursive base class search
                // This handles non-serialized properties that SerializedObject can't access
                
                // Try to find field (including in base classes)
                FieldInfo fieldInfo = GetFieldRecursive(componentType, fieldName);
                    
                if (fieldInfo != null)
                {
                    object value = ConvertJTokenToValue(fieldValue, fieldInfo.FieldType);
                    fieldInfo.SetValue(component, value);
                    continue;
                }
                
                // Try to find property (including in base classes)
                PropertyInfo propertyInfo = GetPropertyRecursive(componentType, fieldName);
                
                if (propertyInfo != null && propertyInfo.CanWrite)
                {
                    object value = ConvertJTokenToValue(fieldValue, propertyInfo.PropertyType);
                    propertyInfo.SetValue(component, value);
                    continue;
                }
                
                fullSuccess = false;
                errorMessage = $"Field or Property with name '{fieldName}' not found on component '{componentType.Name}'";
                McpLogger.LogWarning($"[MCP Unity] {errorMessage}");
            }
            
            // Apply all SerializedObject changes
            serializedObject.ApplyModifiedProperties();

            return fullSuccess;
        }

        /// <summary>
        /// Update component data using Unity's SerializedObject API
        /// This properly handles all serialized fields including private ones in base classes
        /// </summary>
        /// <param name="component">The component to update</param>
        /// <param name="componentData">The data to apply to the component</param>
        /// <param name="errorMessage">Error message if any fields failed to update</param>
        /// <returns>True if all fields were updated successfully</returns>
        private bool UpdateComponentDataSerialized(Component component, JObject componentData, out string errorMessage)
        {
            errorMessage = "";
            SerializedObject serializedObject = new SerializedObject(component);
            bool allFieldsFound = true;
            
            foreach (var property in componentData.Properties())
            {
                string fieldPath = property.Name;
                JToken fieldValue = property.Value;
                
                // Skip null values
                if (string.IsNullOrEmpty(fieldPath) || fieldValue.Type == JTokenType.Null)
                {
                    continue;
                }
                
                // SerializedObject.FindProperty uses Unity's serialization path
                SerializedProperty serializedProperty = serializedObject.FindProperty(fieldPath);
                
                if (serializedProperty == null)
                {
                    McpLogger.LogWarning($"[MCP Unity] SerializedProperty '{fieldPath}' not found on {component.GetType().Name}");
                    errorMessage = $"Property '{fieldPath}' not found";
                    allFieldsFound = false;
                    continue;
                }
                
                SetSerializedPropertyValue(serializedProperty, fieldValue);
            }
            
            serializedObject.ApplyModifiedProperties();
            return allFieldsFound;
        }

        /// <summary>
        /// Set a SerializedProperty value from a JToken
        /// </summary>
        /// <param name="prop">The SerializedProperty to set</param>
        /// <param name="value">The JToken value to apply</param>
        private void SetSerializedPropertyValue(SerializedProperty prop, JToken value)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    prop.intValue = value.ToObject<int>();
                    break;
                    
                case SerializedPropertyType.Boolean:
                    prop.boolValue = value.ToObject<bool>();
                    break;
                    
                case SerializedPropertyType.Float:
                    prop.floatValue = value.ToObject<float>();
                    break;
                    
                case SerializedPropertyType.String:
                    prop.stringValue = value.ToObject<string>();
                    break;
                    
                case SerializedPropertyType.Color:
                    if (value.Type == JTokenType.Object)
                    {
                        JObject color = (JObject)value;
                        prop.colorValue = new Color(
                            color["r"]?.ToObject<float>() ?? 0f,
                            color["g"]?.ToObject<float>() ?? 0f,
                            color["b"]?.ToObject<float>() ?? 0f,
                            color["a"]?.ToObject<float>() ?? 1f
                        );
                    }
                    break;
                    
                case SerializedPropertyType.Vector2:
                    if (value.Type == JTokenType.Object)
                    {
                        JObject v2 = (JObject)value;
                        prop.vector2Value = new Vector2(
                            v2["x"]?.ToObject<float>() ?? 0f,
                            v2["y"]?.ToObject<float>() ?? 0f
                        );
                    }
                    break;
                    
                case SerializedPropertyType.Vector3:
                    if (value.Type == JTokenType.Object)
                    {
                        JObject v3 = (JObject)value;
                        prop.vector3Value = new Vector3(
                            v3["x"]?.ToObject<float>() ?? 0f,
                            v3["y"]?.ToObject<float>() ?? 0f,
                            v3["z"]?.ToObject<float>() ?? 0f
                        );
                    }
                    break;
                    
                case SerializedPropertyType.Vector4:
                    if (value.Type == JTokenType.Object)
                    {
                        JObject v4 = (JObject)value;
                        prop.vector4Value = new Vector4(
                            v4["x"]?.ToObject<float>() ?? 0f,
                            v4["y"]?.ToObject<float>() ?? 0f,
                            v4["z"]?.ToObject<float>() ?? 0f,
                            v4["w"]?.ToObject<float>() ?? 0f
                        );
                    }
                    break;
                    
                case SerializedPropertyType.Quaternion:
                    if (value.Type == JTokenType.Object)
                    {
                        JObject q = (JObject)value;
                        prop.quaternionValue = new Quaternion(
                            q["x"]?.ToObject<float>() ?? 0f,
                            q["y"]?.ToObject<float>() ?? 0f,
                            q["z"]?.ToObject<float>() ?? 0f,
                            q["w"]?.ToObject<float>() ?? 1f
                        );
                    }
                    break;
                    
                case SerializedPropertyType.Rect:
                    if (value.Type == JTokenType.Object)
                    {
                        JObject rect = (JObject)value;
                        prop.rectValue = new Rect(
                            rect["x"]?.ToObject<float>() ?? 0f,
                            rect["y"]?.ToObject<float>() ?? 0f,
                            rect["width"]?.ToObject<float>() ?? 0f,
                            rect["height"]?.ToObject<float>() ?? 0f
                        );
                    }
                    break;
                    
                case SerializedPropertyType.Bounds:
                    if (value.Type == JTokenType.Object)
                    {
                        JObject bounds = (JObject)value;
                        JObject center = bounds["center"] as JObject;
                        JObject size = bounds["size"] as JObject;
                        prop.boundsValue = new Bounds(
                            center != null ? new Vector3(
                                center["x"]?.ToObject<float>() ?? 0f,
                                center["y"]?.ToObject<float>() ?? 0f,
                                center["z"]?.ToObject<float>() ?? 0f
                            ) : Vector3.zero,
                            size != null ? new Vector3(
                                size["x"]?.ToObject<float>() ?? 0f,
                                size["y"]?.ToObject<float>() ?? 0f,
                                size["z"]?.ToObject<float>() ?? 0f
                            ) : Vector3.one
                        );
                    }
                    break;
                    
                case SerializedPropertyType.Enum:
                    if (value.Type == JTokenType.String)
                    {
                        // Find enum value by name
                        string[] enumNames = prop.enumNames;
                        string enumValue = value.ToObject<string>();
                        int index = Array.IndexOf(enumNames, enumValue);
                        if (index >= 0)
                        {
                            prop.enumValueIndex = index;
                        }
                        else
                        {
                            // Try case-insensitive match
                            for (int i = 0; i < enumNames.Length; i++)
                            {
                                if (string.Equals(enumNames[i], enumValue, StringComparison.OrdinalIgnoreCase))
                                {
                                    prop.enumValueIndex = i;
                                    break;
                                }
                            }
                        }
                    }
                    else if (value.Type == JTokenType.Integer)
                    {
                        prop.enumValueIndex = value.ToObject<int>();
                    }
                    break;
                    
                case SerializedPropertyType.ObjectReference:
                    // Handle asset references
                    if (value.Type == JTokenType.String)
                    {
                        string assetPath = value.ToObject<string>();
                        if (!string.IsNullOrEmpty(assetPath))
                        {
                            UnityEngine.Object asset = null;
                            
                            // Try to load as asset path
                            if (assetPath.StartsWith("Assets/") || assetPath.StartsWith("Packages/"))
                            {
                                asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                            }
                            
                            // If not found, try to find by name
                            if (asset == null)
                            {
                                string[] guids = AssetDatabase.FindAssets(assetPath);
                                if (guids.Length > 0)
                                {
                                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                                    asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                                }
                            }
                            
                            prop.objectReferenceValue = asset;
                        }
                        else
                        {
                            prop.objectReferenceValue = null;
                        }
                    }
                    else if (value.Type == JTokenType.Object)
                    {
                        JObject obj = (JObject)value;
                        string guid = obj["guid"]?.ToObject<string>();
                        string path = obj["path"]?.ToObject<string>();
                        
                        if (!string.IsNullOrEmpty(guid))
                        {
                            path = AssetDatabase.GUIDToAssetPath(guid);
                        }
                        
                        if (!string.IsNullOrEmpty(path))
                        {
                            prop.objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                        }
                    }
                    else if (value.Type == JTokenType.Null)
                    {
                        prop.objectReferenceValue = null;
                    }
                    break;
                    
                case SerializedPropertyType.LayerMask:
                    prop.intValue = value.ToObject<int>();
                    break;
                    
                case SerializedPropertyType.Vector2Int:
                    if (value.Type == JTokenType.Object)
                    {
                        JObject v2i = (JObject)value;
                        prop.vector2IntValue = new Vector2Int(
                            v2i["x"]?.ToObject<int>() ?? 0,
                            v2i["y"]?.ToObject<int>() ?? 0
                        );
                    }
                    break;
                    
                case SerializedPropertyType.Vector3Int:
                    if (value.Type == JTokenType.Object)
                    {
                        JObject v3i = (JObject)value;
                        prop.vector3IntValue = new Vector3Int(
                            v3i["x"]?.ToObject<int>() ?? 0,
                            v3i["y"]?.ToObject<int>() ?? 0,
                            v3i["z"]?.ToObject<int>() ?? 0
                        );
                    }
                    break;
                    
                case SerializedPropertyType.RectInt:
                    if (value.Type == JTokenType.Object)
                    {
                        JObject rectInt = (JObject)value;
                        prop.rectIntValue = new RectInt(
                            rectInt["x"]?.ToObject<int>() ?? 0,
                            rectInt["y"]?.ToObject<int>() ?? 0,
                            rectInt["width"]?.ToObject<int>() ?? 0,
                            rectInt["height"]?.ToObject<int>() ?? 0
                        );
                    }
                    break;
                    
                case SerializedPropertyType.BoundsInt:
                    if (value.Type == JTokenType.Object)
                    {
                        JObject boundsInt = (JObject)value;
                        JObject position = boundsInt["position"] as JObject;
                        JObject size = boundsInt["size"] as JObject;
                        prop.boundsIntValue = new BoundsInt(
                            position != null ? new Vector3Int(
                                position["x"]?.ToObject<int>() ?? 0,
                                position["y"]?.ToObject<int>() ?? 0,
                                position["z"]?.ToObject<int>() ?? 0
                            ) : Vector3Int.zero,
                            size != null ? new Vector3Int(
                                size["x"]?.ToObject<int>() ?? 0,
                                size["y"]?.ToObject<int>() ?? 0,
                                size["z"]?.ToObject<int>() ?? 0
                            ) : Vector3Int.one
                        );
                    }
                    break;
                    
                case SerializedPropertyType.Generic:
                    // For complex types, try to iterate children
                    if (value.Type == JTokenType.Object)
                    {
                        JObject obj = (JObject)value;
                        foreach (var child in obj.Properties())
                        {
                            SerializedProperty childProp = prop.FindPropertyRelative(child.Name);
                            if (childProp != null)
                            {
                                SetSerializedPropertyValue(childProp, child.Value);
                            }
                        }
                    }
                    break;
                    
                case SerializedPropertyType.ArraySize:
                    prop.intValue = value.ToObject<int>();
                    break;
                    
                default:
                    McpLogger.LogWarning($"[MCP Unity] Unsupported property type: {prop.propertyType} for {prop.name}");
                    break;
            }
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
            
            // Handle UnityEngine.Object types (assets) by path or GUID
            if (typeof(UnityEngine.Object).IsAssignableFrom(targetType))
            {
                if (token.Type == JTokenType.String)
                {
                    string assetPath = token.ToObject<string>();
                    
                    if (string.IsNullOrEmpty(assetPath))
                    {
                        return null;
                    }
                    
                    // Try to load as asset path
                    if (assetPath.StartsWith("Assets/") || assetPath.StartsWith("Packages/"))
                    {
                        UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath(assetPath, targetType);
                        if (asset != null)
                        {
                            return asset;
                        }
                        McpLogger.LogWarning($"[MCP Unity] Could not load asset at path: {assetPath}");
                    }
                    
                    // Try to find by name in project
                    string[] guids = AssetDatabase.FindAssets($"{assetPath} t:{targetType.Name}");
                    if (guids.Length > 0)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                        return AssetDatabase.LoadAssetAtPath(path, targetType);
                    }
                    
                    // Final fallback - search without type filter
                    guids = AssetDatabase.FindAssets(assetPath);
                    if (guids.Length > 0)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                        UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath(path, targetType);
                        if (asset != null)
                        {
                            return asset;
                        }
                    }
                    
                    McpLogger.LogWarning($"[MCP Unity] Could not find asset: {assetPath}");
                    return null;
                }
                else if (token.Type == JTokenType.Object)
                {
                    // Handle object with guid or path property
                    JObject obj = (JObject)token;
                    string guid = obj["guid"]?.ToObject<string>();
                    string path = obj["path"]?.ToObject<string>();
                    
                    if (!string.IsNullOrEmpty(guid))
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                        if (!string.IsNullOrEmpty(assetPath))
                        {
                            return AssetDatabase.LoadAssetAtPath(assetPath, targetType);
                        }
                        McpLogger.LogWarning($"[MCP Unity] Could not find asset with GUID: {guid}");
                    }
                    else if (!string.IsNullOrEmpty(path))
                    {
                        return AssetDatabase.LoadAssetAtPath(path, targetType);
                    }
                }
                else if (token.Type == JTokenType.Null)
                {
                    return null;
                }
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
                McpLogger.LogError($"[MCP Unity] Error converting value to type {targetType.Name}: {ex.Message}");
                return null;
            }
        }
    }
}
