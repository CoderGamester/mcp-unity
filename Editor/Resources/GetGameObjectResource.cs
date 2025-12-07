using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace McpUnity.Resources
{
    /// <summary>
    /// Resource for retrieving detailed information about a specific GameObject
    /// </summary>
    public class GetGameObjectResource : McpResourceBase
    {
        public GetGameObjectResource()
        {
            Name = "get_gameobject";
            Description = "Retrieves detailed information about a specific GameObject by instance ID or object name or path";
            Uri = "unity://gameobject/{idOrName}";
        }
        
        /// <summary>
        /// Fetch information about a specific GameObject
        /// </summary>
        /// <param name="parameters">Resource parameters as a JObject. Should include 'objectPathId' which can be either an instance ID or a path</param>
        /// <returns>A JObject containing the GameObject data</returns>
        public override JObject Fetch(JObject parameters)
        {
            // Validate parameters
            if (parameters == null || !parameters.ContainsKey("idOrName"))
            {
                return new JObject
                {
                    ["success"] = false,
                    ["message"] = "Missing required parameter: idOrName"
                };
            }

            string idOrName = parameters["idOrName"]?.ToObject<string>();
            
            if (string.IsNullOrEmpty(idOrName))
            {
                return new JObject
                {
                    ["success"] = false,
                    ["message"] = "Parameter 'objectPathId' cannot be null or empty"
                };
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
                return new JObject
                {
                    ["success"] = false,
                    ["message"] = $"GameObject with '{idOrName}' reference not found. Make sure the GameObject exists and is loaded in the current scene(s)."
                };
            }

            // Convert the GameObject to a JObject
            JObject gameObjectData = GameObjectToJObject(gameObject, true);
                
            // Create the response
            return new JObject
            {
                ["success"] = true,
                ["message"] = $"Retrieved GameObject data for '{gameObject.name}'",
                ["gameObject"] = gameObjectData,
                ["instanceId"] = gameObject.GetInstanceID()
            };
        }

        /// <summary>
        /// Convert a GameObject to a JObject with its hierarchy
        /// </summary>
        /// <param name="gameObject">The GameObject to convert</param>
        /// <param name="includeDetailedComponents">Whether to include detailed component information</param>
        /// <returns>A JObject representing the GameObject</returns>
        public static JObject GameObjectToJObject(GameObject gameObject, bool includeDetailedComponents)
        {
            if (gameObject == null) return null;
            
            // Add children
            JArray childrenArray = new JArray();
            foreach (Transform child in gameObject.transform)
            {
                childrenArray.Add(GameObjectToJObject(child.gameObject, includeDetailedComponents));
            }
            
            // Create a JObject for the game object
            JObject gameObjectJson = new JObject
            {
                ["name"] = gameObject.name,
                ["activeSelf"] = gameObject.activeSelf,
                ["activeInHierarchy"] = gameObject.activeInHierarchy,
                ["tag"] = gameObject.tag,
                ["layer"] = gameObject.layer,
                ["layerName"] = LayerMask.LayerToName(gameObject.layer),
                ["instanceId"] = gameObject.GetInstanceID(),
                ["components"] = GetComponentsInfo(gameObject, includeDetailedComponents),
                ["children"] = childrenArray
            };
            
            return gameObjectJson;
        }
        
        /// <summary>
        /// Namespace prefixes for components with native/C++ code that crash when accessed via reflection.
        /// These components will only have basic info (type, enabled) serialized, not detailed properties.
        /// </summary>
        private static readonly string[] UnsafeNamespacePrefixes = new string[]
        {
            "Pathfinding",  // A* Pathfinding Project
            "FMOD",         // FMOD audio
            "FMODUnity",    // FMOD Unity integration
        };
        
        /// <summary>
        /// Check if a component type is from a native plugin that may crash when accessed via reflection
        /// </summary>
        private static bool IsUnsafeNativeComponent(Type componentType)
        {
            if (componentType == null) return true;
            
            string fullName = componentType.FullName ?? "";
            string namespaceName = componentType.Namespace ?? "";
            
            foreach (string prefix in UnsafeNamespacePrefixes)
            {
                if (namespaceName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                    fullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// Get information about the components attached to a GameObject
        /// </summary>
        /// <param name="gameObject">The GameObject to get components from</param>
        /// <param name="includeDetailedInfo">Whether to include detailed component information</param>
        /// <returns>A JArray containing component information</returns>
        private static JArray GetComponentsInfo(GameObject gameObject, bool includeDetailedInfo = false)
        {
            Component[] components = gameObject.GetComponents<Component>();
            JArray componentsArray = new JArray();
            
            foreach (Component component in components)
            {
                if (component == null) continue;
                
                Type componentType = component.GetType();
                bool isUnsafe = IsUnsafeNativeComponent(componentType);
                
                JObject componentJson = new JObject
                {
                    ["type"] = componentType.Name,
                    ["enabled"] = IsComponentEnabled(component)
                };

                // Add detailed information if requested and component is safe to inspect
                if (includeDetailedInfo)
                {
                    if (isUnsafe)
                    {
                        componentJson["properties"] = new JObject
                        {
                            ["_skipped"] = "Native plugin component - serialization skipped for safety"
                        };
                    }
                    else
                    {
                        componentJson["properties"] = GetComponentProperties(component);
                    }
                }
                    
                componentsArray.Add(componentJson);
            }
            
            return componentsArray;
        }
        
        /// <summary>
        /// Check if a component is enabled (if it has an enabled property)
        /// </summary>
        /// <param name="component">The component to check</param>
        /// <returns>True if the component is enabled, false otherwise</returns>
        private static bool IsComponentEnabled(Component component)
        {
            // Check if the component is a Behaviour (has enabled property)
            if (component is Behaviour behaviour)
            {
                return behaviour.enabled;
            }
            
            // Check if the component is a Renderer
            if (component is Renderer renderer)
            {
                return renderer.enabled;
            }
            
            // Check if the component is a Collider
            if (component is Collider collider)
            {
                return collider.enabled;
            }
            
            // Default to true for components without an enabled property
            return true;
        }

        /// <summary>
        /// Maximum depth for serializing nested objects to prevent stack overflow from circular references
        /// </summary>
        private const int MaxSerializationDepth = 5;
        
        /// <summary>
        /// Maximum items to serialize in a collection to prevent excessive output
        /// </summary>
        private const int MaxCollectionItems = 50;

        /// <summary>
        /// Get all serialized fields, public fields and public properties of a component
        /// </summary>
        /// <param name="component">The component to get properties from</param>
        /// <returns>A JObject containing all the component properties</returns>
        private static JObject GetComponentProperties(Component component)
        {
            if (component == null) return null;

            JObject propertiesJson = new JObject();
            Type componentType = component.GetType();
            
            // Track visited objects to prevent circular reference loops
            HashSet<object> visited = new HashSet<object>(new ReferenceEqualityComparer());

            // Get serialized fields (both public and private with SerializeField attribute)
            FieldInfo[] fields = componentType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (FieldInfo field in fields)
            {
                // Include public fields and serialized private fields
                bool isSerializedField = field.IsPublic || field.GetCustomAttributes(typeof(SerializeField), true).Length > 0;

                if (!isSerializedField) continue;
                try
                {
                    object value = field.GetValue(component);
                    propertiesJson[field.Name] = SerializeValue(value, 0, visited);
                }
                catch (Exception)
                {
                    // Skip fields that cannot be serialized
                    propertiesJson[field.Name] = "Unable to serialize";
                }
            }

            // Get public properties
            PropertyInfo[] properties = componentType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (PropertyInfo property in properties)
            {
                // Only include properties with a getter and skip properties that might cause issues or are not useful
                if (!property.CanRead || ShouldSkipProperty(property)) continue;
                
                try
                {
                    object value = property.GetValue(component);
                    propertiesJson[property.Name] = SerializeValue(value, 0, visited);
                }
                catch (Exception)
                {
                    // Skip properties that cannot be serialized
                    propertiesJson[property.Name] = "Unable to serialize";
                }
            }

            return propertiesJson;
        }
        
        /// <summary>
        /// Reference equality comparer for tracking visited objects (prevents circular reference infinite loops)
        /// </summary>
        private class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        /// <summary>
        /// Determine if a property should be skipped during serialization
        /// </summary>
        /// <param name="property">The property to check</param>
        /// <returns>True if the property should be skipped, false otherwise</returns>
        private static bool ShouldSkipProperty(PropertyInfo property)
        {
            // Skip properties that might cause issues or are not useful
            string[] skippedProperties = new string[]
            {
                "mesh", "sharedMesh", "material", "materials",
                "sharedMaterial", "sharedMaterials", "sprite",
                "mainTexture", "mainTextureOffset", "mainTextureScale"
            };

            return Array.IndexOf(skippedProperties, property.Name.ToLower()) >= 0;
        }

        /// <summary>
        /// Serialize a value to a JToken with depth limiting and circular reference protection
        /// </summary>
        /// <param name="value">The value to serialize</param>
        /// <param name="depth">Current recursion depth</param>
        /// <param name="visited">Set of already visited reference objects to detect circular references</param>
        /// <returns>A JToken representing the value</returns>
        private static JToken SerializeValue(object value, int depth = 0, HashSet<object> visited = null)
        {
            if (value == null)
                return JValue.CreateNull();
            
            // Depth limit check to prevent stack overflow
            if (depth > MaxSerializationDepth)
                return "[max depth exceeded]";
            
            Type valueType = value.GetType();
            
            // For reference types (excluding strings), check for circular references
            if (!valueType.IsValueType && !(value is string))
            {
                if (visited == null)
                    visited = new HashSet<object>(new ReferenceEqualityComparer());
                    
                if (visited.Contains(value))
                    return "[circular reference]";
                    
                visited.Add(value);
            }

            // Handle common Unity types
            if (value is Vector2 vector2)
                return new JObject { ["x"] = vector2.x, ["y"] = vector2.y };

            if (value is Vector3 vector3)
                return new JObject { ["x"] = vector3.x, ["y"] = vector3.y, ["z"] = vector3.z };

            if (value is Vector4 vector4)
                return new JObject { ["x"] = vector4.x, ["y"] = vector4.y, ["z"] = vector4.z, ["w"] = vector4.w };

            if (value is Quaternion quaternion)
                return new JObject { ["x"] = quaternion.x, ["y"] = quaternion.y, ["z"] = quaternion.z, ["w"] = quaternion.w };

            if (value is Color color)
                return new JObject { ["r"] = color.r, ["g"] = color.g, ["b"] = color.b, ["a"] = color.a };

            if (value is Bounds bounds)
                return new JObject { 
                    ["center"] = SerializeValue(bounds.center, depth + 1, visited), 
                    ["size"] = SerializeValue(bounds.size, depth + 1, visited) 
                };

            if (value is Rect rect)
                return new JObject { ["x"] = rect.x, ["y"] = rect.y, ["width"] = rect.width, ["height"] = rect.height };

            if (value is UnityEngine.Object unityObject)
                return unityObject != null ? unityObject.name : null;

            // Handle arrays and lists with item limit
            if (value is System.Collections.IList list)
            {
                JArray array = new JArray();
                int count = 0;
                foreach (var item in list)
                {
                    if (count >= MaxCollectionItems)
                    {
                        array.Add($"[... and {list.Count - count} more items]");
                        break;
                    }
                    array.Add(SerializeValue(item, depth + 1, visited));
                    count++;
                }
                return array;
            }

            // Handle dictionaries with item limit
            if (value is System.Collections.IDictionary dict)
            {
                JObject obj = new JObject();
                int count = 0;
                foreach (System.Collections.DictionaryEntry entry in dict)
                {
                    if (count >= MaxCollectionItems)
                    {
                        obj["_truncated"] = $"{dict.Count - count} more entries";
                        break;
                    }
                    obj[entry.Key.ToString()] = SerializeValue(entry.Value, depth + 1, visited);
                    count++;
                }
                return obj;
            }

            // Handle enum by using the name
            if (value is Enum enumValue)
                return enumValue.ToString();

            // Handle primitive types directly
            if (valueType.IsPrimitive || value is string || value is decimal)
            {
                return JToken.FromObject(value);
            }

            // For complex types we don't recognize, return type name to avoid unsafe deep serialization
            return $"[{valueType.Name}]";
        }
    }
}
