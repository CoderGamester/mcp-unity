using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Pipeline.Commands;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace McpUnity.Extensions.Commands
{
    public static class UnloadSceneCommand
    {
        [CliCommand("unload_scene", "Unload an already-loaded scene while protecting dirty and active scene state.")]
        public static UnloadSceneResult Unload(
            [CliArg("path", "Path of the already-loaded scene to unload.", Required = true)] string path,
            [CliArg("force", "Discard unsaved scene changes when true.")] bool force = false)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("'path' is required.");

            var normalizedPath = path.Trim().Replace('\\', '/');
            var loadedScenes = GetLoadedScenes();
            var target = loadedScenes.FirstOrDefault(scene =>
                string.Equals(scene.path, normalizedPath, StringComparison.OrdinalIgnoreCase));

            if (!target.IsValid() || !target.isLoaded)
                throw new InvalidOperationException($"Scene '{normalizedPath}' is not loaded.");

            if (target.isDirty && !force)
            {
                throw new InvalidOperationException(
                    $"Scene '{target.path}' has unsaved changes. Pass force=true to discard them.");
            }

            var active = SceneManager.GetActiveScene();
            if (target.handle == active.handle && loadedScenes.Count == 1)
            {
                throw new InvalidOperationException(
                    $"Cannot unload '{target.path}' because it is the sole loaded active scene.");
            }

            if (target.handle == active.handle)
            {
                var alternative = loadedScenes
                    .Where(scene => scene.handle != target.handle)
                    .OrderBy(scene => scene.path, StringComparer.Ordinal)
                    .ThenBy(scene => scene.name, StringComparer.Ordinal)
                    .ThenBy(scene => scene.handle)
                    .First();

                if (!SceneManager.SetActiveScene(alternative))
                    throw new InvalidOperationException(
                        $"Could not make '{alternative.path}' active before unloading '{target.path}'.");
            }

            if (!EditorSceneManager.CloseScene(target, true))
            {
                if (target.IsValid() && target.isLoaded)
                    SceneManager.SetActiveScene(target);
                throw new InvalidOperationException($"Failed to unload scene '{normalizedPath}'.");
            }

            var remainingActive = SceneManager.GetActiveScene();
            return new UnloadSceneResult
            {
                UnloadedPath = normalizedPath,
                ActiveSceneName = remainingActive.name,
                ActiveScenePath = remainingActive.path
            };
        }

        private static List<Scene> GetLoadedScenes()
        {
            var scenes = new List<Scene>();
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.isLoaded)
                    scenes.Add(scene);
            }

            return scenes;
        }
    }
}
