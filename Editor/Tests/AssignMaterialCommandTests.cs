using System;
using System.IO;
using System.Linq;
using System.Reflection;
using McpUnity.Extensions.Commands;
using NUnit.Framework;
using Unity.Pipeline;
using Unity.Pipeline.Models;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace McpUnity.Extensions.Tests
{
    public class AssignMaterialCommandTests
    {
        private const string Root = "Assets/__McpUnityAssignMaterialTests";

        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EnsureFolder(Root);
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(Root);
            AssetDatabase.Refresh();
        }

        [Test]
        public void Assign_RejectsNonGameObjectInput()
        {
            var material = CreateMaterialAsset("Input");

            Assert.Throws<ArgumentException>(() =>
                AssignMaterialCommand.Assign(Ref(material), Ref(material)));
        }

        [Test]
        public void Assign_RejectsNonMaterialInput()
        {
            var gameObject = CreateRendererObject("Renderer");

            Assert.Throws<ArgumentException>(() =>
                AssignMaterialCommand.Assign(Ref(gameObject), Ref(gameObject)));
        }

        [Test]
        public void Assign_RequiresRenderer()
        {
            var gameObject = new GameObject("NoRenderer");
            var material = CreateMaterialAsset("Material");

            Assert.Throws<InvalidOperationException>(() =>
                AssignMaterialCommand.Assign(Ref(gameObject), Ref(material)));
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void Assign_ValidatesSlot(int slot)
        {
            var gameObject = CreateRendererObject("Renderer");
            var renderer = gameObject.GetComponent<Renderer>();
            renderer.sharedMaterials = new[] { CreateMaterialAsset("Original") };
            var replacement = CreateMaterialAsset("Replacement");

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                AssignMaterialCommand.Assign(Ref(gameObject), Ref(replacement), slot));
        }

        [Test]
        public void Assign_UpdatesSharedMaterialMarksDirtyAndReturnsIdentities()
        {
            var gameObject = CreateRendererObject("Renderer");
            var renderer = gameObject.GetComponent<Renderer>();
            renderer.sharedMaterials = new[] { CreateMaterialAsset("Original") };
            var replacement = CreateMaterialAsset("Replacement");

            object result = null;
            Assert.DoesNotThrow(() =>
                result = AssignMaterialCommand.Assign(Ref(gameObject), Ref(replacement)));

            Assert.That(renderer.sharedMaterials[0], Is.SameAs(replacement));
            Assert.That(EditorUtility.IsDirty(renderer), Is.True);
            Assert.That(Property<int>(result, "Slot"), Is.EqualTo(0));
            Assert.That(Property<AuthoringResult>(result, "GameObject").InstanceId,
                Is.EqualTo(PipelineUtils.GetObjectId(gameObject)));
            Assert.That(Property<AuthoringResult>(result, "Material").Guid, Is.Not.Empty);
        }

        [Test]
        public void Assign_RecordsPrefabInstancePropertyModification()
        {
            var source = CreateRendererObject("PrefabSource");
            source.GetComponent<Renderer>().sharedMaterials = new[] { CreateMaterialAsset("Original") };
            var prefabPath = Root + "/Renderer.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(source, prefabPath);
            UnityEngine.Object.DestroyImmediate(source);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var replacement = CreateMaterialAsset("Replacement");

            Assert.DoesNotThrow(() =>
                AssignMaterialCommand.Assign(Ref(instance), Ref(replacement)));

            var modifications = PrefabUtility.GetPropertyModifications(instance.GetComponent<Renderer>());
            Assert.That(modifications, Is.Not.Null);
            Assert.That(
                modifications.Any(modification =>
                    modification.propertyPath == "m_Materials.Array.data[0]"),
                Is.True);
        }

        private static GameObject CreateRendererObject(string name)
        {
            var gameObject = new GameObject(name);
            gameObject.AddComponent<MeshRenderer>();
            return gameObject;
        }

        private static Material CreateMaterialAsset(string name)
        {
            var shader = Shader.Find("Standard") ??
                         Shader.Find("Sprites/Default") ??
                         Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            AssetDatabase.CreateAsset(material, Root + "/" + name + ".mat");
            return material;
        }

        private static ObjectRef Ref(UnityEngine.Object obj) =>
            new ObjectRef { InstanceId = PipelineUtils.GetObjectId(obj) };

        private static T Property<T>(object instance, string name)
        {
            Assert.That(instance, Is.Not.Null);
            var property = instance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Expected public property '{name}'.");
            return (T)property.GetValue(instance);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
