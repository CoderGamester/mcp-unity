using System;
using System.Collections;
using McpUnity.Extensions.Commands;
using NUnit.Framework;
using Unity.Pipeline;
using Unity.Pipeline.Editor.Authoring;
using Unity.Pipeline.Models;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace McpUnity.Extensions.Tests
{
    public class DuplicateGameObjectCommandTests
    {
        private const string ActiveScenePath = "Assets/__McpUnityDuplicateActiveScene.unity";

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
            AssetDatabase.DeleteAsset(ActiveScenePath);
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

        [UnityTest]
        public IEnumerator Duplicate_UndoRedoRestoresConfiguredStateFromNonActiveSourceScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            Assert.That(EditorSceneManager.SaveScene(activeScene, ActiveScenePath), Is.True);
            var sourceScene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
            Assert.That(SceneManager.SetActiveScene(activeScene), Is.True);

            var parent = new GameObject("DestinationParent");
            parent.transform.position = new Vector3(10f, 20f, 30f);

            var source = new GameObject("Source");
            source.transform.localPosition = new Vector3(1f, 2f, 3f);
            source.transform.localRotation = Quaternion.Euler(15f, 25f, 35f);
            source.transform.localScale = new Vector3(2f, 3f, 4f);
            SceneManager.MoveGameObjectToScene(source, sourceScene);
            Assert.That(SceneManager.GetActiveScene(), Is.Not.EqualTo(sourceScene));

            DuplicateGameObjectCommand.Duplicate(
                Ref(source),
                parent: Ref(parent),
                name: "RestoredCopy");
            yield return null;

            var duplicate = parent.transform.Find("RestoredCopy");
            Assert.That(duplicate, Is.Not.Null);
            AssertConfiguredState(duplicate, activeScene, parent.transform, source.transform);

            Undo.PerformUndo();
            yield return null;
            Assert.That(parent.transform.Find("RestoredCopy"), Is.Null);

            Undo.PerformRedo();
            yield return null;
            var restored = parent.transform.Find("RestoredCopy");
            Assert.That(restored, Is.Not.Null);
            AssertConfiguredState(restored, activeScene, parent.transform, source.transform);
        }

        private static void AssertConfiguredState(
            Transform actual,
            Scene expectedScene,
            Transform expectedParent,
            Transform expectedTransform)
        {
            Assert.That(actual.gameObject.scene, Is.EqualTo(expectedScene));
            Assert.That(actual.parent, Is.SameAs(expectedParent));
            Assert.That(actual.name, Is.EqualTo("RestoredCopy"));
            Assert.That(actual.localPosition, Is.EqualTo(expectedTransform.localPosition));
            Assert.That(Quaternion.Angle(actual.localRotation, expectedTransform.localRotation),
                Is.LessThan(0.001f));
            Assert.That(actual.localScale, Is.EqualTo(expectedTransform.localScale));
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
