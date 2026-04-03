using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for getting Unity play mode status
    /// </summary>
    public class GetPlayModeStatusTool : McpToolBase
    {
        public GetPlayModeStatusTool()
        {
            Name = "get_play_mode_status";
            Description = "Gets Unity play mode status (isPlaying, isPaused).";
        }

        public override JObject Execute(JObject parameters)
        {
            try
            {
                bool isPlaying = EditorApplication.isPlaying;
                bool isPaused = EditorApplication.isPaused;

                var result = new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = isPlaying ? (isPaused ? "Play mode (paused)" : "Play mode") : "Edit mode",
                    ["isPlaying"] = isPlaying,
                    ["isPaused"] = isPaused
                };

                McpLogger.LogInfo($"Play mode status requested: isPlaying={isPlaying}, isPaused={isPaused}");

                return result;
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error getting play mode status: {ex.Message}",
                    "play_mode_error"
                );
            }
        }
    }
}
