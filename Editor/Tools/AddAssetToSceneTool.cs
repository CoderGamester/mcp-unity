using System;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for adding assets from the AssetDatabase to the Unity scene
    /// </summary>
    public class AddAssetToSceneTool : McpToolBase
    {
        public AddAssetToSceneTool()
        {
            Name = "add_asset_to_scene";
            Description = "Adds an asset from the AssetDatabase to the Unity scene";
        }
        
        /// <summary>
        /// Execute the AddAssetToScene tool with the provided parameters
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            // Extract parameters
            string assetPath = parameters["assetPath"]?.ToObject<string>();
            string guid = parameters["guid"]?.ToObject<string>();
            Vector3 position = parameters["position"]?.ToObject<JObject>() != null 
                ? new Vector3(
                    parameters["position"]["x"]?.ToObject<float>() ?? 0f,
                    parameters["position"]["y"]?.ToObject<float>() ?? 0f,
                    parameters["position"]["z"]?.ToObject<float>() ?? 0f
                ) 
                : Vector3.zero;
            
            // Optional parent game object
            string parentPath = parameters["parentPath"]?.ToObject<string>();
            int? parentId = parameters["parentId"]?.ToObject<int?>();
            
            // Validate parameters - require either assetPath or guid
            if (string.IsNullOrEmpty(assetPath) && string.IsNullOrEmpty(guid))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Required parameter 'assetPath' or 'guid' not provided", 
                    "validation_error"
                );
            }
            
            // If we have a GUID but no path, convert GUID to path
            if (string.IsNullOrEmpty(assetPath) && !string.IsNullOrEmpty(guid))
            {
                assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath))
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Asset with GUID '{guid}' not found", 
                        "not_found_error"
                    );
                }
            }
            
            // Load the asset
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Failed to load asset at path '{assetPath}'", 
                    "not_found_error"
                );
            }
            
            // Check if the asset is a prefab or another type that can be instantiated
            bool isPrefab = PrefabUtility.GetPrefabAssetType(asset) != PrefabAssetType.NotAPrefab;
            bool canInstantiate = asset is GameObject || isPrefab;
            
            if (!canInstantiate)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Asset of type '{asset.GetType().Name}' cannot be instantiated in the scene", 
                    "invalid_asset_type"
                );
            }
            
            // Instantiate the asset
            GameObject instance = null;
            try
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                
                // Set position
                instance.transform.position = position;
                
                // Set parent if specified
                if (!string.IsNullOrEmpty(parentPath) || parentId.HasValue)
                {
                    GameObject parent = null;
                    
                    // Try to find parent by ID first
                    if (parentId.HasValue)
                    {
                        parent = EditorUtility.InstanceIDToObject(parentId.Value) as GameObject;
                    }
                    // Otherwise try to find by path
                    else if (!string.IsNullOrEmpty(parentPath))
                    {
                        parent = GameObject.Find(parentPath);
                    }
                    
                    if (parent != null)
                    {
                        instance.transform.SetParent(parent.transform, false);
                    }
                    else
                    {
                        // Parent not found, return an error instead of adding to root
                        string identifier = parentId.HasValue ? $"ID {parentId.Value}" : $"path '{parentPath}'";
                        return McpUnitySocketHandler.CreateErrorResponse(
                            $"Parent GameObject not found with {identifier}", 
                            "parent_not_found"
                        );
                    }
                }
                
                // Select the newly created object
                Selection.activeGameObject = instance;
                EditorGUIUtility.PingObject(instance);
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error instantiating asset: {ex.Message}", 
                    "instantiation_error"
                );
            }
            
            // Log the action
            Debug.Log($"[MCP Unity] Added asset '{asset.name}' to scene from path '{assetPath}'");
            
            // Create the response
            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = $"Successfully added asset '{asset.name}' with instance ID {instance.GetInstanceID()} to the scene",
                ["instanceId"] = instance.GetInstanceID()
            };
        }
    }
}
