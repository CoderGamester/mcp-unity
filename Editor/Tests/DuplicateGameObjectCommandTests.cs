using System;
using McpUnity.Extensions.Commands;
using NUnit.Framework;
using Unity.Pipeline;
using Unity.Pipeline.Editor.Authoring;
using Unity.Pipeline.Models;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace McpUnity.Extensions.Tests
{
    public class DuplicateGameObjectCommandTests
    {
        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Undo.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
        }

        [Test]
        public void Duplicate_RejectsNonGameObjectSource()
        {
            var material = new Material(FindShader());
            try
            {
                Assert.Throws<ArgumentException>(() =>
                    DuplicateGameObjectCommand.Duplicate(Ref(material)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void Duplicate_PreservesSourceSceneWhenParentIsOmitted()
        {
            var source = new GameObject("Source");
            source.transform.position = new Vector3(3f, 4f, 5f);

            AuthoringResult result = null;
            Assert.DoesNotThrow(() =>
                result = DuplicateGameObjectCommand.Duplicate(Ref(source), name: "Copy"));

            Assert.That(ObjectResolver.TryResolve(ToRef(result), out var resolved, out var error), Is.True, error);
            var duplicate = (GameObject)resolved;
            Assert.That(duplicate, Is.Not.SameAs(source));
            Assert.That(duplicate.name, Is.EqualTo("Copy"));
            Assert.That(duplicate.scene, Is.EqualTo(source.scene));
            Assert.That(duplicate.transform.position, Is.EqualTo(source.transform.position));
        }

        [Test]
        public void Duplicate_LeavesNameUnchangedWhenRenameIsOmitted()
        {
            var source = new GameObject("Source");

            var result = DuplicateGameObjectCommand.Duplicate(Ref(source));

            Assert.That(ObjectResolver.TryResolve(ToRef(result), out var resolved, out var error), Is.True, error);
            Assert.That(resolved.name, Is.EqualTo(source.name));
        }

        [Test]
        public void Duplicate_AppliesOptionalParentWithWorldPositionStaysFalseByDefault()
        {
            var source = new GameObject("Source");
            source.transform.localPosition = new Vector3(2f, 0f, 0f);
            var parent = new GameObject("Parent");
            parent.transform.position = new Vector3(10f, 0f, 0f);

            AuthoringResult result = null;
            Assert.DoesNotThrow(() =>
                result = DuplicateGameObjectCommand.Duplicate(
                    Ref(source),
                    parent: Ref(parent),
                    name: "ChildCopy"));

            Assert.That(ObjectResolver.TryResolve(ToRef(result), out var resolved, out var error), Is.True, error);
            var duplicate = (GameObject)resolved;
            Assert.That(duplicate.transform.parent, Is.SameAs(parent.transform));
            Assert.That(duplicate.transform.localPosition, Is.EqualTo(source.transform.localPosition));
            Assert.That(duplicate.name, Is.EqualTo("ChildCopy"));
        }

        [Test]
        public void Duplicate_RegistersCreatedObjectWithUndo()
        {
            var source = new GameObject("Source");

            AuthoringResult result = null;
            Assert.DoesNotThrow(() => result = DuplicateGameObjectCommand.Duplicate(Ref(source)));
            Assert.That(result.InstanceId, Is.Not.Null);
            Assert.That(PipelineUtils.IdToObject(result.InstanceId.Value), Is.Not.Null);

            Undo.PerformUndo();

            Assert.That(PipelineUtils.IdToObject(result.InstanceId.Value), Is.Null);
            Assert.That(source, Is.Not.Null, "Undo should remove only the duplicate.");
        }

        private static ObjectRef Ref(UnityEngine.Object obj) =>
            new ObjectRef { InstanceId = PipelineUtils.GetObjectId(obj) };

        private static ObjectRef ToRef(AuthoringResult result) =>
            new ObjectRef { InstanceId = result.InstanceId };

        private static Shader FindShader() =>
            Shader.Find("Standard") ??
            Shader.Find("Sprites/Default") ??
            Shader.Find("Hidden/InternalErrorShader");
    }
}
