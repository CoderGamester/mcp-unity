using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json.Linq;

namespace McpUnity.Resources
{
    /// <summary>
    /// Resource for retrieving all game objects in the Unity scenes hierarchy
    /// </summary>
    /// <summary>
/// Resource for retrieving all game objects in the Unity scenes hierarchy
/// </summary>
public class GetScenesHierarchyResource : McpResourceBase
    {
        public GetScenesHierarchyResource()
        {
            Name = "get_scenes_hierarchy";
            Description = "Retrieves all game objects in the Unity loaded scenes with their active state";
            Uri = "unity://scenes_hierarchy";
        }
        
        /// <summary>
        /// Fetch all game objects in the Unity loaded scenes
        /// </summary>
        /// <param name="parameters">Resource parameters as a JObject (not used)</param>
        /// <returns>A JObject containing the hierarchy of game objects</returns>
        public override JObject Fetch(JObject parameters)
        {
            // Get all game objects in the hierarchy
            JArray hierarchyArray = GetSceneHierarchy();
                
            // Create the response
            return new JObject
            {
                ["success"] = true,
                ["message"] = $"Retrieved hierarchy with {hierarchyArray.Count} root objects",
                ["hierarchy"] = hierarchyArray
            };
        }
        
        /// <summary>
        /// Get all game objects in the Unity loaded scenes
        /// </summary>
        /// <returns>A JArray containing the hierarchy of game objects</returns>
        private JArray GetSceneHierarchy()
        {
            JArray rootObjectsArray = new JArray();
            
            // Get all loaded scenes
            int sceneCount = SceneManager.sceneCount;
            for (int i = 0; i < sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                if (scene.isLoaded == false)
                {
                    continue;
                }
                
                // Create a scene object
                JObject sceneObject = new JObject
                {
                    ["name"] = scene.name,
                    ["path"] = scene.path,
                    ["buildIndex"] = scene.buildIndex,
                    ["isDirty"] = scene.isDirty,
                    ["rootObjects"] = new JArray()
                };
                
                // Get root game objects in the scene
                GameObject[] rootObjects = scene.GetRootGameObjects();
                JArray rootObjectsInScene = (JArray)sceneObject["rootObjects"];
                
                foreach (GameObject rootObject in rootObjects)
                {
                    // Add the root object and its children to the array
                    rootObjectsInScene.Add(GetGameObjectResource.GameObjectToJObject(rootObject, false));
                }
                
                // Add the scene to the root objects array
                rootObjectsArray.Add(sceneObject);
            }
            
            return rootObjectsArray;
        }
    }
}
