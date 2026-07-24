using System;
using Unity.Pipeline.Commands;
using Unity.Pipeline.Editor.Authoring;
using Unity.Pipeline.Models;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace McpUnity.Extensions.Commands
{
    public static class DuplicateGameObjectCommand
    {
        [CliCommand("duplicate_gameobject", "Duplicate a GameObject in its loaded scene with optional parenting and renaming.")]
        public static AuthoringResult Duplicate(
            [CliArg("source", "GameObject reference to duplicate.", Required = true)] ObjectRef source,
            [CliArg("parent", "Optional GameObject parent for the duplicate.")] ObjectRef parent = null,
            [CliArg("name", "Optional name for the duplicate.")] string name = null,
            [CliArg("world_position_stays", "Preserve world transform when applying the optional parent.")] bool worldPositionStays = false)
        {
            var sourceObject = CommandObjectResolver.Resolve<GameObject>(source, "source");
            if (!sourceObject.scene.IsValid() || !sourceObject.scene.isLoaded)
                throw new ArgumentException("'source' must be a GameObject in a loaded scene.");

            GameObject parentObject = null;
            if (parent != null && !parent.IsEmpty)
            {
                parentObject = CommandObjectResolver.Resolve<GameObject>(parent, "parent");
                if (!parentObject.scene.IsValid() || !parentObject.scene.isLoaded)
                    throw new ArgumentException("'parent' must be a GameObject in a loaded scene.");
            }

            var duplicate = Object.Instantiate(sourceObject);
            duplicate.name = string.IsNullOrEmpty(name) ? sourceObject.name : name;
            Undo.RegisterCreatedObjectUndo(duplicate, "Duplicate GameObject");

            if (duplicate.scene != sourceObject.scene)
                SceneManager.MoveGameObjectToScene(duplicate, sourceObject.scene);

            if (parentObject != null)
            {
                duplicate.transform.SetParent(parentObject.transform, worldPositionStays);
            }
            else if (sourceObject.transform.parent != null)
            {
                duplicate.transform.SetParent(sourceObject.transform.parent, true);
            }

            EditorUtility.SetDirty(duplicate);
            EditorSceneManager.MarkSceneDirty(duplicate.scene);
            return ObjectResolver.Describe(duplicate);
        }
    }
}
