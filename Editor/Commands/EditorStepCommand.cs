using System;
using Unity.Pipeline.Commands;

namespace McpUnity.Extensions.Commands
{
    public static class EditorStepCommand
    {
        [CliCommand("editor_step", "Advance play mode by one frame.")]
        public static EditorStepResult Step()
        {
            if (!UnityEditor.EditorApplication.isPlaying)
                throw new InvalidOperationException("'editor_step' requires the editor to be in play mode.");

            UnityEditor.EditorApplication.Step();
            return new EditorStepResult
            {
                IsPlaying = UnityEditor.EditorApplication.isPlaying,
                IsPaused = UnityEditor.EditorApplication.isPaused
            };
        }
    }
}
