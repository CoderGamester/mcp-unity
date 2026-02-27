using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for controlling Unity play mode (play, pause, step)
    /// </summary>
    public class SetPlayModeStatusTool : McpToolBase
    {
        public SetPlayModeStatusTool()
        {
            Name = "set_play_mode_status";
            Description = "Controls Unity play mode. Actions: 'play', 'pause', 'stop', 'step'.";
        }

        public override JObject Execute(JObject parameters)
        {
            try
            {
                string action = parameters["action"]?.ToString()?.ToLower();
                
                if (string.IsNullOrEmpty(action))
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        "Missing required parameter 'action'. Valid actions: 'play', 'pause', 'stop', 'step'",
                        "missing_parameter"
                    );
                }

                bool wasPlaying = EditorApplication.isPlaying;
                bool wasPaused = EditorApplication.isPaused;

                switch (action)
                {
                    case "play":
                        if (!EditorApplication.isPlaying)
                        {
                            // Start play mode
                            EditorApplication.isPlaying = true;
                        }
                        else if (EditorApplication.isPaused)
                        {
                            // Unpause if already playing
                            EditorApplication.isPaused = false;
                        }
                        break;

                    case "pause":
                        if (EditorApplication.isPlaying)
                        {
                            EditorApplication.isPaused = !EditorApplication.isPaused;
                        }
                        else
                        {
                            return McpUnitySocketHandler.CreateErrorResponse(
                                "Cannot pause: Editor is not in play mode",
                                "invalid_state"
                            );
                        }
                        break;

                    case "stop":
                        if (EditorApplication.isPlaying)
                        {
                            EditorApplication.isPlaying = false;
                        }
                        break;

                    case "step":
                        if (EditorApplication.isPlaying)
                        {
                            EditorApplication.Step();
                        }
                        else
                        {
                            return McpUnitySocketHandler.CreateErrorResponse(
                                "Cannot step: Editor is not in play mode",
                                "invalid_state"
                            );
                        }
                        break;

                    default:
                        return McpUnitySocketHandler.CreateErrorResponse(
                            $"Invalid action '{action}'. Valid actions: 'play', 'pause', 'stop', 'step'",
                            "invalid_parameter"
                        );
                }

                // Give Unity a moment to update state
                bool isPlaying = EditorApplication.isPlaying;
                bool isPaused = EditorApplication.isPaused;

                var result = new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Action '{action}' executed. State: {(isPlaying ? (isPaused ? "Playing (paused)" : "Playing") : "Edit mode")}",
                    ["action"] = action,
                    ["wasPlaying"] = wasPlaying,
                    ["wasPaused"] = wasPaused,
                    ["isPlaying"] = isPlaying,
                    ["isPaused"] = isPaused
                };

                McpLogger.LogInfo($"Play mode action '{action}' executed. isPlaying={isPlaying}, isPaused={isPaused}");

                return result;
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error controlling play mode: {ex.Message}",
                    "play_mode_error"
                );
            }
        }
    }
}
