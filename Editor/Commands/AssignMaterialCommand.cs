using System;
using Unity.Pipeline.Commands;
using Unity.Pipeline.Editor.Authoring;
using Unity.Pipeline.Models;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace McpUnity.Extensions.Commands
{
    public static class AssignMaterialCommand
    {
        [CliCommand("assign_material", "Assign a Material to a Renderer shared-material slot.")]
        public static AssignMaterialResult Assign(
            [CliArg("game_object", "GameObject reference whose Renderer will be edited.", Required = true)] ObjectRef gameObject,
            [CliArg("material", "Material reference to assign.", Required = true)] ObjectRef material,
            [CliArg("slot", "Renderer shared-material slot index.")] int slot = 0)
        {
            var target = CommandObjectResolver.Resolve<GameObject>(gameObject, "game_object");
            var assignedMaterial = CommandObjectResolver.Resolve<Material>(material, "material");
            var renderer = target.GetComponent<Renderer>();
            if (renderer == null)
                throw new InvalidOperationException($"GameObject '{target.name}' does not have a Renderer.");

            var sharedMaterials = renderer.sharedMaterials;
            if (slot < 0 || slot >= sharedMaterials.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(slot),
                    slot,
                    $"Slot must be between 0 and {sharedMaterials.Length - 1} for Renderer '{renderer.name}'.");
            }

            Undo.RecordObject(renderer, "Assign Material");
            sharedMaterials[slot] = assignedMaterial;
            renderer.sharedMaterials = sharedMaterials;
            EditorUtility.SetDirty(renderer);
            if (target.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(target.scene);
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);

            return new AssignMaterialResult
            {
                GameObject = ObjectResolver.Describe(target),
                Material = ObjectResolver.Describe(assignedMaterial),
                Slot = slot
            };
        }
    }
}
