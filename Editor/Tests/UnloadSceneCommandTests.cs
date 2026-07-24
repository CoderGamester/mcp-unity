using System;
using System.IO;
using System.Reflection;
using McpUnity.Extensions.Commands;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace McpUnity.Extensions.Tests
{
    public class UnloadSceneCommandTests
    {
        private const string Root = "Assets/__McpUnityUnloadSceneTests";

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
        public void Unload_RejectsSceneThatIsNotLoaded()
        {
            var path = Root + "/SavedButClosed.unity";
            CreateScene(path, NewSceneMode.Single);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Assert.Throws<InvalidOperationException>(() => UnloadSceneCommand.Unload(path));
        }

        [Test]
        public void Unload_RejectsDirtySceneUnlessForced()
        {
            CreateScene(Root + "/Active.unity", NewSceneMode.Single);
            var targetPath = Root + "/Dirty.unity";
            var target = CreateScene(targetPath, NewSceneMode.Additive);
            SceneManager.MoveGameObjectToScene(new GameObject("Unsaved"), target);
            EditorSceneManager.MarkSceneDirty(target);

            Assert.Throws<InvalidOperationException>(() => UnloadSceneCommand.Unload(targetPath));
            Assert.That(SceneManager.GetSceneByPath(targetPath).isLoaded, Is.True);
        }

        [Test]
        public void Unload_RejectsSoleLoadedActiveScene()
        {
            var path = Root + "/Only.unity";
            CreateScene(path, NewSceneMode.Single);

            Assert.Throws<InvalidOperationException>(() => UnloadSceneCommand.Unload(path, force: true));
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(path));
        }

        [Test]
        public void Unload_ForcedDirtyActiveSceneChoosesDeterministicAlternative()
        {
            var zPath = Root + "/Zeta.unity";
            var aPath = Root + "/Alpha.unity";
            var targetPath = Root + "/Target.unity";
            CreateScene(zPath, NewSceneMode.Single);
            CreateScene(aPath, NewSceneMode.Additive);
            var target = CreateScene(targetPath, NewSceneMode.Additive);
            SceneManager.SetActiveScene(target);
            SceneManager.MoveGameObjectToScene(new GameObject("Unsaved"), target);
            EditorSceneManager.MarkSceneDirty(target);

            object result = null;
            Assert.DoesNotThrow(() => result = UnloadSceneCommand.Unload(targetPath, force: true));

            Assert.That(SceneManager.GetSceneByPath(targetPath).isLoaded, Is.False);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(aPath));
            Assert.That(Property<string>(result, "UnloadedPath"), Is.EqualTo(targetPath));
            Assert.That(Property<string>(result, "ActiveScenePath"), Is.EqualTo(aPath));
        }

        private static Scene CreateScene(string path, NewSceneMode mode)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            Assert.That(EditorSceneManager.SaveScene(scene, path), Is.True);
            return scene;
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

        private static T Property<T>(object instance, string name)
        {
            Assert.That(instance, Is.Not.Null);
            var property = instance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Expected public property '{name}'.");
            return (T)property.GetValue(instance);
        }
    }
}
