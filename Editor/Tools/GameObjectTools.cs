using System;
using UnityEngine;
using UnityEditor;
using McpUnity.Unity;
using Newtonsoft.Json.Linq;

namespace McpUnity.Tools
{
    /// <summary>
    /// Utility class for common GameObject operations
    /// </summary>
    public static class GameObjectToolUtils
    {
        /// <summary>
        /// Find a GameObject by instance ID or hierarchy path
        /// </summary>
        /// <param name="instanceId">Optional instance ID</param>
        /// <param name="objectPath">Optional hierarchy path</param>
        /// <param name="gameObject">Output GameObject if found</param>
        /// <param name="identifierInfo">Description of how the object was identified</param>
        /// <returns>Error JObject if not found, null if successful</returns>
        public static JObject FindGameObject(int? instanceId, string objectPath, out GameObject gameObject, out string identifierInfo)
        {
            gameObject = null;
            identifierInfo = "";

            if (instanceId.HasValue)
            {
                gameObject = EditorUtility.InstanceIDToObject(instanceId.Value) as GameObject;
                identifierInfo = $"instance ID {instanceId.Value}";
            }
            else if (!string.IsNullOrEmpty(objectPath))
            {
                gameObject = GameObject.Find(objectPath);
                if (gameObject == null)
                {
                    // Try finding by traversing hierarchy
                    gameObject = FindGameObjectByPath(objectPath);
                }
                identifierInfo = $"path '{objectPath}'";
            }
            else
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Either 'instanceId' or 'objectPath' must be provided.",
                    "validation_error"
                );
            }

            if (gameObject == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"GameObject not found using {identifierInfo}.",
                    "not_found_error"
                );
            }

            return null; // Success
        }

        /// <summary>
        /// Find a GameObject by its hierarchy path
        /// </summary>
        private static GameObject FindGameObjectByPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            path = path.TrimStart('/');
            string[] parts = path.Split('/');

            if (parts.Length == 0) return null;

            // Find root object
            GameObject current = null;
            GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

            foreach (var root in rootObjects)
            {
                if (root.name == parts[0])
                {
                    current = root;
                    break;
                }
            }

            if (current == null) return null;

            // Traverse children
            for (int i = 1; i < parts.Length; i++)
            {
                Transform child = current.transform.Find(parts[i]);
                if (child == null) return null;
                current = child.gameObject;
            }

            return current;
        }

        /// <summary>
        /// Get the full hierarchy path of a GameObject
        /// </summary>
        public static string GetGameObjectPath(GameObject obj)
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

    /// <summary>
    /// Tool for duplicating GameObjects in the Unity Editor
    /// </summary>
    public class DuplicateGameObjectTool : McpToolBase
    {
        public DuplicateGameObjectTool()
        {
            Name = "duplicate_gameobject";
            Description = "Duplicates a GameObject in the Unity scene. Can create multiple copies and optionally rename or reparent them.";
            IsAsync = false;
        }

        public override JObject Execute(JObject parameters)
        {
            int? instanceId = parameters["instanceId"]?.ToObject<int?>();
            string objectPath = parameters["objectPath"]?.ToObject<string>();
            string newName = parameters["newName"]?.ToObject<string>();
            string newParentPath = parameters["newParent"]?.ToObject<string>();
            int? newParentId = parameters["newParentId"]?.ToObject<int?>();
            int count = parameters["count"]?.ToObject<int?>() ?? 1;

            // Validate count
            if (count < 1 || count > 100)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Count must be between 1 and 100.",
                    "validation_error"
                );
            }

            // Find source GameObject
            JObject error = GameObjectToolUtils.FindGameObject(instanceId, objectPath, out GameObject sourceObject, out string identifierInfo);
            if (error != null) return error;

            // Find new parent if specified
            GameObject newParent = null;
            if (newParentId.HasValue)
            {
                newParent = EditorUtility.InstanceIDToObject(newParentId.Value) as GameObject;
                if (newParent == null)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"New parent GameObject not found with instance ID {newParentId.Value}.",
                        "not_found_error"
                    );
                }
            }
            else if (!string.IsNullOrEmpty(newParentPath))
            {
                newParent = GameObject.Find(newParentPath);
                if (newParent == null)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"New parent GameObject not found at path '{newParentPath}'.",
                        "not_found_error"
                    );
                }
            }

            // Create duplicates
            JArray duplicatedObjects = new JArray();

            for (int i = 0; i < count; i++)
            {
                GameObject duplicate = UnityEngine.Object.Instantiate(sourceObject);
                Undo.RegisterCreatedObjectUndo(duplicate, $"Duplicate {sourceObject.name}");

                // Set name
                if (!string.IsNullOrEmpty(newName))
                {
                    duplicate.name = count > 1 ? $"{newName} ({i + 1})" : newName;
                }
                else
                {
                    // Remove "(Clone)" suffix and optionally add number
                    string baseName = sourceObject.name;
                    duplicate.name = count > 1 ? $"{baseName} ({i + 1})" : baseName;
                }

                // Set parent
                Transform targetParent = newParent != null ? newParent.transform : sourceObject.transform.parent;
                if (targetParent != null)
                {
                    duplicate.transform.SetParent(targetParent, true);
                }

                duplicatedObjects.Add(new JObject
                {
                    ["instanceId"] = duplicate.GetInstanceID(),
                    ["name"] = duplicate.name,
                    ["path"] = GameObjectToolUtils.GetGameObjectPath(duplicate)
                });
            }

            EditorUtility.SetDirty(sourceObject.scene.GetRootGameObjects()[0]);

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = count == 1
                    ? $"Successfully duplicated GameObject '{sourceObject.name}'."
                    : $"Successfully created {count} duplicates of GameObject '{sourceObject.name}'.",
                ["duplicatedObjects"] = duplicatedObjects
            };
        }
    }

    /// <summary>
    /// Tool for deleting GameObjects in the Unity Editor
    /// </summary>
    public class DeleteGameObjectTool : McpToolBase
    {
        public DeleteGameObjectTool()
        {
            Name = "delete_gameobject";
            Description = "Deletes a GameObject from the Unity scene. By default, also deletes all children.";
            IsAsync = false;
        }

        public override JObject Execute(JObject parameters)
        {
            int? instanceId = parameters["instanceId"]?.ToObject<int?>();
            string objectPath = parameters["objectPath"]?.ToObject<string>();
            bool includeChildren = parameters["includeChildren"]?.ToObject<bool?>() ?? true;

            // Find target GameObject
            JObject error = GameObjectToolUtils.FindGameObject(instanceId, objectPath, out GameObject targetObject, out string identifierInfo);
            if (error != null) return error;

            string deletedName = targetObject.name;
            string deletedPath = GameObjectToolUtils.GetGameObjectPath(targetObject);
            int childCount = targetObject.transform.childCount;

            if (!includeChildren && childCount > 0)
            {
                // Move children to parent before deleting
                Transform parent = targetObject.transform.parent;
                Transform[] children = new Transform[childCount];

                for (int i = 0; i < childCount; i++)
                {
                    children[i] = targetObject.transform.GetChild(i);
                }

                foreach (Transform child in children)
                {
                    Undo.SetTransformParent(child, parent, "Reparent before delete");
                }
            }

            // Delete the GameObject
            Undo.DestroyObjectImmediate(targetObject);

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = includeChildren && childCount > 0
                    ? $"Successfully deleted GameObject '{deletedName}' and {childCount} children."
                    : $"Successfully deleted GameObject '{deletedName}'.",
                ["deletedPath"] = deletedPath,
                ["childrenPreserved"] = !includeChildren && childCount > 0 ? childCount : 0
            };
        }
    }

    /// <summary>
    /// Tool for changing the parent of GameObjects in the Unity Editor
    /// </summary>
    public class ReparentGameObjectTool : McpToolBase
    {
        public ReparentGameObjectTool()
        {
            Name = "reparent_gameobject";
            Description = "Changes the parent of a GameObject. Can move to a new parent or to the root level (null parent).";
            IsAsync = false;
        }

        public override JObject Execute(JObject parameters)
        {
            int? instanceId = parameters["instanceId"]?.ToObject<int?>();
            string objectPath = parameters["objectPath"]?.ToObject<string>();
            string newParentPath = parameters["newParent"]?.ToObject<string>();
            int? newParentId = parameters["newParentId"]?.ToObject<int?>();
            bool worldPositionStays = parameters["worldPositionStays"]?.ToObject<bool?>() ?? true;

            // Find target GameObject
            JObject error = GameObjectToolUtils.FindGameObject(instanceId, objectPath, out GameObject targetObject, out string identifierInfo);
            if (error != null) return error;

            string oldPath = GameObjectToolUtils.GetGameObjectPath(targetObject);
            Transform oldParent = targetObject.transform.parent;

            // Find new parent (null means root level)
            Transform newParentTransform = null;
            bool moveToRoot = false;

            // Check if explicitly moving to root (newParent is null or empty string)
            if (parameters["newParent"] != null && parameters["newParent"].Type == JTokenType.Null)
            {
                moveToRoot = true;
            }
            else if (newParentId.HasValue)
            {
                GameObject newParent = EditorUtility.InstanceIDToObject(newParentId.Value) as GameObject;
                if (newParent == null)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"New parent GameObject not found with instance ID {newParentId.Value}.",
                        "not_found_error"
                    );
                }
                newParentTransform = newParent.transform;
            }
            else if (!string.IsNullOrEmpty(newParentPath))
            {
                GameObject newParent = GameObject.Find(newParentPath);
                if (newParent == null)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"New parent GameObject not found at path '{newParentPath}'.",
                        "not_found_error"
                    );
                }
                newParentTransform = newParent.transform;
            }
            else if (parameters["newParent"] == null && parameters["newParentId"] == null)
            {
                // Neither specified - move to root
                moveToRoot = true;
            }

            // Prevent parenting to self or descendants
            if (newParentTransform != null)
            {
                if (newParentTransform == targetObject.transform)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        "Cannot parent a GameObject to itself.",
                        "validation_error"
                    );
                }

                if (newParentTransform.IsChildOf(targetObject.transform))
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        "Cannot parent a GameObject to one of its descendants.",
                        "validation_error"
                    );
                }
            }

            // Check if already at target parent
            if (moveToRoot && oldParent == null)
            {
                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"GameObject '{targetObject.name}' is already at the root level.",
                    ["instanceId"] = targetObject.GetInstanceID(),
                    ["name"] = targetObject.name,
                    ["path"] = oldPath,
                    ["changed"] = false
                };
            }

            if (!moveToRoot && newParentTransform == oldParent)
            {
                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"GameObject '{targetObject.name}' is already a child of the specified parent.",
                    ["instanceId"] = targetObject.GetInstanceID(),
                    ["name"] = targetObject.name,
                    ["path"] = oldPath,
                    ["changed"] = false
                };
            }

            // Perform reparenting
            Undo.SetTransformParent(targetObject.transform, newParentTransform, "Reparent GameObject");

            if (!worldPositionStays)
            {
                // Reset local position when worldPositionStays is false
                Undo.RecordObject(targetObject.transform, "Reset Local Position");
                targetObject.transform.localPosition = Vector3.zero;
                targetObject.transform.localRotation = Quaternion.identity;
                targetObject.transform.localScale = Vector3.one;
            }

            string newPath = GameObjectToolUtils.GetGameObjectPath(targetObject);
            string parentDescription = newParentTransform != null
                ? $"'{newParentTransform.gameObject.name}'"
                : "root level";

            EditorUtility.SetDirty(targetObject);

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = $"Successfully reparented GameObject '{targetObject.name}' to {parentDescription}.",
                ["instanceId"] = targetObject.GetInstanceID(),
                ["name"] = targetObject.name,
                ["oldPath"] = oldPath,
                ["newPath"] = newPath,
                ["changed"] = true
            };
        }
    }
}
