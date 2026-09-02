using System;
using System.IO;
using McpUnity.Utils;
using UnityEngine;
using UnityEditor;

namespace McpUnity.Unity
{
    /// <summary>
    /// Handles persistence of MCP Unity settings
    /// </summary>
    [Serializable]
    public class McpUnitySettings
    {
        // Constants
        public const string ServerVersion = "1.5.0";
        public const string PackageName = "com.gamelovers.mcp-unity";
        public const int RequestTimeoutMinimum = 10;
        
        // Paths
        private const string SettingsPath = "ProjectSettings/McpUnitySettings.json";
        
        private static McpUnitySettings _instance;

        [Tooltip("Port number for MCP server")]
        public int Port = 8090;
        
        [Tooltip("Timeout in seconds for tool request")]
        public int RequestTimeoutSeconds = RequestTimeoutMinimum;
        
        [Tooltip("Whether to automatically start the MCP server when Unity opens")]
        public bool AutoStartServer = true;

        [Tooltip("Allow the MCP WebSocket server to run in Unity -batchmode. Node package installation is still skipped in batch mode. This is disabled by default so CI and cloud builds keep the existing behavior.")]
        public bool AllowBatchModeServer = false;
        
        [Tooltip("Whether to show info logs in the Unity console")]
        public bool EnableInfoLogs = false;

        [Tooltip("Optional: Full path to the npm executable (e.g., /Users/user/.asdf/shims/npm or C:\\path\\to\\npm.cmd). If not set, 'npm' from the system PATH will be used.")]
        public string NpmExecutablePath = string.Empty;
        
        [Tooltip("Allow connections from remote MCP bridges. When disabled, only localhost connections are allowed (default).")]
        public bool AllowRemoteConnections = false;

        [Tooltip("Allow MCP clients to install Unity packages. Disabled by default because installed packages can execute Editor code.")]
        public bool AllowPackageInstallation = false;

        /// <summary>
        /// Singleton instance of settings
        /// </summary>
        public static McpUnitySettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new McpUnitySettings();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Returns whether a project settings file already exists without creating one.
        /// This lets batch-mode CI keep its no-initialization behavior unless it has explicitly opted in.
        /// </summary>
        internal static bool HasPersistedSettings => File.Exists(SettingsPath);

        /// <summary>
        /// Private constructor for singleton
        /// </summary>
        private McpUnitySettings() 
        { 
            LoadSettings();
        }

        /// <summary>
        /// Load settings from disk
        /// </summary>
        public void LoadSettings()
        {
            try
            {
                // Load settings from McpUnitySettings.json
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    JsonUtility.FromJsonOverwrite(json, this);
                }
                else
                {
                    // Create default settings file on the first time initialization
                    SaveSettings();
                }
            }
            catch (Exception ex)
            {
                // Can't use LoggerService here as it depends on settings
                Debug.LogError($"[MCP Unity] Failed to load settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Save settings to disk
        /// </summary>
        /// <remarks>
        /// WARNING: This file is also read by the MCP server. Changes here will require updates to it. See mcpUnity.ts
        /// </remarks>
        public void SaveSettings()
        {
            try
            {
                // Save settings to McpUnitySettings.json
                string json = JsonUtility.ToJson(this, true);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                // Can't use LoggerService here as it might create circular dependency
                Debug.LogError($"[MCP Unity] Failed to save settings: {ex.Message}");
            }
        }
    }
}
