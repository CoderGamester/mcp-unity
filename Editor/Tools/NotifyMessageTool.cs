using System;
using System.Threading.Tasks;
using McpUnity.Unity;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for displaying messages in the Unity console
    /// </summary>
    public class NotifyMessageTool : McpToolBase
    {
        /// <summary>
        /// Supported message types for Unity console output
        /// </summary>
        private enum MessageType
        {
            Info,
            Warning,
            Error
        }

        public NotifyMessageTool()
        {
            Name = "notify_message";
            Description = "Sends a message to the Unity console";
        }
        
        /// <summary>
        /// Execute the NotifyMessage tool with the provided parameters synchronously
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            // Extract parameters
            string message = parameters["message"]?.ToObject<string>();
            string type = parameters["type"]?.ToObject<string>()?.ToLower() ?? "info";
            
            // Validate message
            if (string.IsNullOrEmpty(message))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Required parameter 'message' not provided or is empty",
                    "validation_error"
                );
            }

            // Parse and validate message type
            if (!Enum.TryParse<MessageType>(type, true, out var messageType))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Invalid message type '{type}'. Valid types are: info, warning, error",
                    "validation_error"
                );
            }

            // Log the message based on type
            switch (messageType)
            {
                case MessageType.Warning:
                    Debug.LogWarning($"[MCP Unity] {message}");
                    break;
                case MessageType.Error:
                    Debug.LogError($"[MCP Unity] {message}");
                    break;
                default:
                    Debug.Log($"[MCP Unity] {message}");
                    break;
            }
            
            // Create the response
            return new JObject
            {
                ["success"] = true,
                ["message"] = $"Message displayed: {message}",
                ["type"] = "text"
            };
        }
    }
}
