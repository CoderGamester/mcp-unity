using System;
using McpUnity.Utils;
using UnityEngine;
using UnityEditor;

namespace McpUnity.Unity
{
    /// <summary>
    /// Editor window for controlling the MCP Unity Server.
    /// Provides UI for starting/stopping the server and configuring settings.
    /// </summary>
    public class McpUnityEditorWindow : EditorWindow
    {
        private GUIStyle _headerStyle;
        private GUIStyle _subHeaderStyle;
        private GUIStyle _boxStyle;
        private GUIStyle _wrappedLabelStyle;
        private GUIStyle _connectedClientBoxStyle; // Style for individual connected clients
        private GUIStyle _connectedClientLabelStyle; // Style for labels in connected client boxes
        private int _selectedTab = 0;
        private readonly string[] _tabNames = { "Server", "Help" };
        private bool _isInitialized = false;
        private string _mcpConfigJson = "";
        private bool _tabsIndentationJson = false;
        private Vector2 _helpTabScrollPosition = Vector2.zero;
        private Vector2 _serverTabScrollPosition = Vector2.zero;

        [MenuItem("Tools/MCP Unity/Server Window", false, 1)]
        public static void ShowWindow()
        {
            var window = GetWindow<McpUnityEditorWindow>("MCP Unity");
            window.minSize = new Vector2(600, 400);
        }

        private void OnGUI()
        {
            InitializeStyles();

            EditorGUILayout.BeginVertical();
            
            // Header
            EditorGUILayout.Space();
            WrappedLabel("MCP Unity", _headerStyle);
            EditorGUILayout.Space();
            
            // Tabs
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabNames);
            EditorGUILayout.Space();
            
            switch (_selectedTab)
            {
                case 0: // Server tab
                    DrawServerTab();
                    break;
                case 1: // Help tab
                    DrawHelpTab();
                    break;
            }

            // Version info at the bottom
            GUILayout.FlexibleSpace();
            WrappedLabel($"MCP Unity v{McpUnitySettings.ServerVersion}", EditorStyles.miniLabel, GUILayout.Width(150));
            
            EditorGUILayout.EndVertical();
        }

        #region Tab Drawing Methods

        private void DrawServerTab()
        {
            _serverTabScrollPosition = EditorGUILayout.BeginScrollView(_serverTabScrollPosition);
            EditorGUILayout.BeginVertical("box");
            
            // Server status
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Status:", GUILayout.Width(120));
            
            McpUnitySettings settings = McpUnitySettings.Instance;
            McpUnityServer mcpUnityServer = McpUnityServer.Instance;
            string statusText = mcpUnityServer.IsListening ? "Server Online" : "Server Offline";
            Color statusColor = mcpUnityServer.IsListening  ? Color.green : Color.red;
            
            GUIStyle statusStyle = new GUIStyle(EditorStyles.boldLabel);
            statusStyle.normal.textColor = statusColor;
            
            EditorGUILayout.LabelField(statusText, statusStyle);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // Port configuration
            EditorGUILayout.BeginHorizontal();
            int newPort = EditorGUILayout.IntField("Connection Port", settings.Port);
            if (newPort < 1 || newPort > 65536)
            {
                newPort = settings.Port;
                Debug.LogError($"{newPort} is an invalid port number. Please enter a number between 1 and 65535.");
            }
            
            if (newPort != settings.Port)
            {
                settings.Port = newPort;
                settings.SaveSettings();
                mcpUnityServer.StopServer();
                mcpUnityServer.StartServer(); // Restart the server.newPort
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // Test timeout setting
            EditorGUILayout.BeginHorizontal();
            int newTimeout = EditorGUILayout.IntField(new GUIContent("Request Timeout (seconds)", "Timeout in seconds for tool request"), settings.RequestTimeoutSeconds);
            if (newTimeout < McpUnitySettings.RequestTimeoutMinimum)
            {
                newTimeout = McpUnitySettings.RequestTimeoutMinimum;
                Debug.LogError($"Request timeout must be at least {McpUnitySettings.RequestTimeoutMinimum} seconds.");
            }
            
            if (newTimeout != settings.RequestTimeoutSeconds)
            {
                settings.RequestTimeoutSeconds = newTimeout;
                settings.SaveSettings();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // Auto start server toggle
            bool autoStartServer = EditorGUILayout.Toggle(new GUIContent("Auto Start Server", "Automatically starts the MCP server when Unity opens"), settings.AutoStartServer);
            if (autoStartServer != settings.AutoStartServer)
            {
                settings.AutoStartServer = autoStartServer;
                settings.SaveSettings();
            }
            
            EditorGUILayout.Space();
            
            // Allow remote connections toggle
            bool allowRemoteConnections = EditorGUILayout.Toggle(new GUIContent("Allow Remote Connections", "Allow connections from remote MCP bridges. When disabled, only localhost connections are allowed (default)."), settings.AllowRemoteConnections);
            if (allowRemoteConnections != settings.AllowRemoteConnections)
            {
                settings.AllowRemoteConnections = allowRemoteConnections;
                settings.SaveSettings();
                // Restart server to apply binding change
                mcpUnityServer.StopServer();
                mcpUnityServer.StartServer();
            }
            
            EditorGUILayout.Space();
            
            // Enable info logs toggle
            bool enableInfoLogs = EditorGUILayout.Toggle(new GUIContent("Enable Info Logs", "Show informational logs in the Unity console"), settings.EnableInfoLogs);
            if (enableInfoLogs != settings.EnableInfoLogs)
            {
                settings.EnableInfoLogs = enableInfoLogs;
                settings.SaveSettings();
            }
            
            EditorGUILayout.Space();

            // Server control buttons
            EditorGUILayout.BeginHorizontal();
            
            // Connect button - enabled only when disconnected
            GUI.enabled = !mcpUnityServer.IsListening;
            if (GUILayout.Button("Start Server", GUILayout.Height(30)))
            {
                mcpUnityServer.StartServer();
            }
            
            // Disconnect button - enabled only when connected
            GUI.enabled = mcpUnityServer.IsListening;
            if (GUILayout.Button("Stop Server", GUILayout.Height(30)))
            {
                mcpUnityServer.StopServer();
            }
            
            //Repaint();
            
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(); 
            
            EditorGUILayout.LabelField("Connected Clients", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box"); // Keep the default gray box for the container

            var clients = mcpUnityServer.Clients;
            
            if (clients.Count > 0)
            {
                foreach (var client in clients)
                {
                    EditorGUILayout.BeginVertical(_connectedClientBoxStyle); // Use green background for each client
                    
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("ID:", _connectedClientLabelStyle, GUILayout.Width(50));                    
                    EditorGUILayout.LabelField(client.Key, EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Name:", _connectedClientLabelStyle, GUILayout.Width(50));
                    EditorGUILayout.LabelField(client.Value, _connectedClientLabelStyle);
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space();
                }
            }
            else
            {
                GUIStyle wrapStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
                wrapStyle.wordWrap = true;
                GUILayout.Label("No clients connected\nInvoke a tool from the MCP Client to connect", wrapStyle, GUILayout.ExpandWidth(true));
            }
                
            EditorGUILayout.EndVertical();

            // NPM Executable Path
            string newNpmPath = EditorGUILayout.TextField(new GUIContent("NPM Executable Path", "Optional: Full path to the npm executable (e.g., /Users/user/.asdf/shims/npm or C:\\path\\to\\npm.cmd). If not set, 'npm' from the system PATH will be used."), settings.NpmExecutablePath);
            if (newNpmPath != settings.NpmExecutablePath)
            {
                settings.NpmExecutablePath = newNpmPath;
                settings.SaveSettings();
            }
            
            EditorGUILayout.Space();
            
            // MCP Config generation section
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("MCP Configuration", EditorStyles.boldLabel);

            var before = _tabsIndentationJson;
            _tabsIndentationJson = EditorGUILayout.Toggle("Use Tabs indentation", _tabsIndentationJson);
            
            if (string.IsNullOrEmpty(_mcpConfigJson) || before != _tabsIndentationJson)
            {
                _mcpConfigJson = McpUtils.GenerateMcpConfigJson(_tabsIndentationJson);
            }
                
            if (GUILayout.Button("Copy to Clipboard", GUILayout.Height(30)))
            {
                EditorGUIUtility.systemCopyBuffer = _mcpConfigJson;
            }
            
            EditorGUILayout.TextArea(_mcpConfigJson, GUILayout.Height(200));

            EditorGUILayout.Space();
            
            ShowConfigButton("Windsurf", McpUtils.AddToWindsurfIdeConfig);
            
            EditorGUILayout.Space();
            
            ShowConfigButton("Claude Desktop", McpUtils.AddToClaudeDesktopConfig);
            
            EditorGUILayout.Space();
            
            ShowConfigButton("Cursor", McpUtils.AddToCursorConfig);

            EditorGUILayout.Space();

            ShowConfigButton("Claude Code", McpUtils.AddToClaudeCodeConfig);

            EditorGUILayout.Space();

            ShowConfigButton("GitHub Copilot", McpUtils.AddToGitHubCopilotConfig);

            EditorGUILayout.Separator();
            EditorGUILayout.Separator();

            EditorGUILayout.Space(); 

            // Force Install Server button
            if (GUILayout.Button("Force Install Server", GUILayout.Height(30)))
            {
                McpUnityServer.Instance.InstallServer();
                McpLogger.LogInfo("MCP Unity Server installed successfully.");
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawHelpTab()
        {
            // Begin scrollable area
            _helpTabScrollPosition = EditorGUILayout.BeginScrollView(_helpTabScrollPosition);
            
            WrappedLabel("About MCP Unity", _subHeaderStyle);
            EditorGUILayout.BeginVertical(_boxStyle);
            WrappedLabel("MCP Unity is a Unity Editor integration of the Model Context Protocol (MCP), which enables standardized communication between AI models and applications.");
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Open MCP Protocol Documentation"))
            {
                Application.OpenURL("https://modelcontextprotocol.io");
            }
            
            EditorGUILayout.EndVertical();
            
            // IDE Integration settings
            EditorGUILayout.Space();
            WrappedLabel("IDE Integration Settings", _subHeaderStyle);
            
            EditorGUILayout.BeginVertical(_boxStyle);
            string ideIntegrationTooltip = "Add the Library/PackedCache folder to VSCode-like IDE workspaces so code can be indexed for the AI to access it. This improves code intelligence for Unity packages in VSCode, Cursor, and similar IDEs.";
            
            WrappedLabel("These settings help improve code intelligence in VSCode-like IDEs by adding the Unity Package Cache to your workspace. This is automatically configured when the MCP Unity tool is opened in Unity.");
            EditorGUILayout.Space();
            
            // Add button to manually update workspace
            if (GUILayout.Button(new GUIContent("Update Workspace Cache Now", ideIntegrationTooltip), GUILayout.Height(24)))
            {
                bool updated = VsCodeWorkspaceUtils.AddPackageCacheToWorkspace();
                if (updated)
                {
                    EditorUtility.DisplayDialog("Workspace Updated", "Successfully added Library/PackedCache to workspace files. Please restart your IDE and open the workspace.", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Workspace Update Failed", "No workspace files were found or needed updating.", "OK");
                }
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space();
            WrappedLabel("Available Tools", _subHeaderStyle);
            
            EditorGUILayout.BeginVertical(_boxStyle);
            
            // execute_menu_item
            WrappedLabel("execute_menu_item", EditorStyles.boldLabel);
            WrappedLabel("Executes a function that is currently tagged with MenuItem attribute in the project or in the Unity Editor's menu path");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Example prompt:", EditorStyles.miniLabel);
            WrappedLabel("Execute the menu item 'GameObject/Create Empty' to create a new empty GameObject", new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Italic });
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            
            // select_gameobject
            WrappedLabel("select_gameobject", EditorStyles.boldLabel);
            WrappedLabel("Selects game objects in the Unity hierarchy by path or instance ID");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Example prompt:", EditorStyles.miniLabel);
            WrappedLabel("Select the Main Camera object in my scene", new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Italic });
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            
            // update_gameobject
            WrappedLabel("update_gameobject", EditorStyles.boldLabel);
            WrappedLabel("Updates a GameObject's core properties (name, tag, layer, active/static state), or creates the GameObject if it does not exist");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Example prompt:", EditorStyles.miniLabel);
            WrappedLabel("Set the Player object's tag to 'Enemy' and make it inactive", new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Italic });
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            
            // update_component
            WrappedLabel("update_component", EditorStyles.boldLabel);
            WrappedLabel("Updates component fields on a GameObject or adds it to the GameObject if it does not contain the component");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Example prompt:", EditorStyles.miniLabel);
            WrappedLabel("Add a Rigidbody component to the Player object and set its mass to 5", new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Italic });
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            
            // add_package
            WrappedLabel("add_package", EditorStyles.boldLabel);
            WrappedLabel("Installs new packages in the Unity Package Manager");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Example prompt:", EditorStyles.miniLabel);
            WrappedLabel("Add the TextMeshPro package to my project", new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Italic });
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            
            // run_tests
            WrappedLabel("run_tests", EditorStyles.boldLabel);
            WrappedLabel("Runs tests using the Unity Test Runner");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Example prompt:", EditorStyles.miniLabel);
            WrappedLabel("Run all the EditMode tests in my project", new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Italic });
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            
            // send_console_log
            WrappedLabel("send_console_log", EditorStyles.boldLabel);
            WrappedLabel("Sends console logs to the Unity Editor console");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Example prompt:", EditorStyles.miniLabel);
            WrappedLabel("Send a console log to Unity that the task has been completed", new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Italic });
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            
            // add_asset_to_scene
            WrappedLabel("add_asset_to_scene", EditorStyles.boldLabel);
            WrappedLabel("Adds an asset from the AssetDatabase to the Unity scene");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Example prompt:", EditorStyles.miniLabel);
            WrappedLabel("Add the Player prefab from my project to the current scene", new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Italic });
            EditorGUILayout.EndVertical();
            
            // recompile_scripts
            WrappedLabel("recompile_scripts", EditorStyles.boldLabel);
            WrappedLabel("Recompiles all scripts in the Unity project");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Example prompt:", EditorStyles.miniLabel);
            WrappedLabel("Recompile scripts in my project", new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Italic });
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndVertical();
            
            // Available Resources section
            EditorGUILayout.Space();
            WrappedLabel("Available Resources", _subHeaderStyle);
            
            EditorGUILayout.BeginVertical(_boxStyle);
            
            // unity://menu-items
            WrappedLabel("unity://menu-items", EditorStyles.boldLabel);
            WrappedLabel("Retrieves a list of all available menu items in the Unity Editor");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Example prompt:", EditorStyles.miniLabel);
            WrappedLabel("Show me all available menu items related to GameObject creation", new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Italic });
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            
            // unity://hierarchy
            WrappedLabel("unity://hierarchy", EditorStyles.boldLabel);
            WrappedLabel("Retrieves a list of all game objects in the Unity hierarchy");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Example prompt:", EditorStyles.miniLabel);
            WrappedLabel("Show me the current scene hierarchy structure", new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Italic });
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            
            // unity://gameobject/{id}
            WrappedLabel("unity://gameobject/{id}", EditorStyles.boldLabel);
            WrappedLabel("Retrieves detailed information about a specific GameObject, including all components with serialized properties and fields");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Example prompt:", EditorStyles.miniLabel);
            WrappedLabel("Get me detailed information about the Player GameObject", new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Italic });
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            
            // unity://logs
            WrappedLabel("unity://logs", EditorStyles.boldLabel);
            WrappedLabel("Retrieves a list of all logs from the Unity console");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Example prompt:", EditorStyles.miniLabel);
            WrappedLabel("Show me the recent error messages from the Unity console", new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Italic });
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            
            // unity://packages
            WrappedLabel("unity://packages", EditorStyles.boldLabel);
            WrappedLabel("Retrieves information about installed and available packages from the Unity Package Manager");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Example prompt:", EditorStyles.miniLabel);
            WrappedLabel("List all the packages currently installed in my Unity project", new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Italic });
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            
            // unity://assets
            WrappedLabel("unity://assets", EditorStyles.boldLabel);
            WrappedLabel("Retrieves information about assets in the Unity Asset Database");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Example prompt:", EditorStyles.miniLabel);
            WrappedLabel("Find all texture assets in my project", new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Italic });
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            
            // unity://tests/{testMode}
            WrappedLabel("unity://tests/{testMode}", EditorStyles.boldLabel);
            WrappedLabel("Retrieves information about tests in the Unity Test Runner");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Example prompt:", EditorStyles.miniLabel);
            WrappedLabel("List all available tests in my Unity project", new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Italic });
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            
            EditorGUILayout.EndVertical();
            
            // Author information
            EditorGUILayout.Space();
            WrappedLabel("Author", _subHeaderStyle);
            
            EditorGUILayout.BeginVertical(_boxStyle);
            
            WrappedLabel("Created by CoderGamester", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            WrappedLabel("For issues, feedback, or contributions, please visit:");
            
            // Begin horizontal layout for buttons
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("GitHub: https://github.com/CoderGamester", GUILayout.Height(30)))
            {
                Application.OpenURL("https://github.com/CoderGamester");
            }
            
            if (GUILayout.Button("LinkedIn: Miguel Tomás", GUILayout.Height(30)))
            {
                Application.OpenURL("https://www.linkedin.com/in/miguel-tomas/");
            }
            
            // End horizontal layout
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            
            // End scrollable area
            EditorGUILayout.EndScrollView();
        }

        #endregion

        #region Helper Methods

        private void InitializeStyles()
        {
            if (_isInitialized) return;
            
            // Window title
            titleContent = new GUIContent("MCP Unity");
            
            // Header style
            _headerStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(0, 0, 10, 10)
            };
            
            // Sub-header style
            _subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                margin = new RectOffset(0, 0, 10, 5)
            };
            
            // Box style
            _boxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(0, 0, 10, 10)
            };
            
            // Connected client box style with green background
            _connectedClientBoxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(0, 0, 5, 5)
            };
            
            // Create a light green texture for the background
            Texture2D greenTexture = new Texture2D(1, 1);
            greenTexture.SetPixel(0, 0, new Color(0.8f, 1.0f, 0.8f, 1.0f)); // Light green color
            greenTexture.Apply();
            
            _connectedClientBoxStyle.normal.background = greenTexture;
            
            // Label style for text in connected client boxes (black text for contrast)
            _connectedClientLabelStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = Color.black }
            };
            
            // Wrapped label style for text that needs to wrap
            _wrappedLabelStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                richText = true
            };
            
            _isInitialized = true;
        }
        
        /// <summary>
        /// Creates a label with text that properly wraps based on available width
        /// </summary>
        /// <param name="text">The text to display</param>
        /// <param name="style">Optional style override (wordWrap will be forced true)</param>
        /// <param name="options">Layout options</param>
        private void WrappedLabel(string text, GUIStyle style = null, params GUILayoutOption[] options)
        {
            if (style == null)
            {
                // Use our predefined wrapped label style
                EditorGUILayout.LabelField(text, _wrappedLabelStyle, options);
                return;
            }
            
            // Create a temporary style with wordWrap enabled based on the provided style
            GUIStyle wrappedStyle = new GUIStyle(style)
            {
                wordWrap = true
            };
            
            EditorGUILayout.LabelField(text, wrappedStyle, options);
        }

        
            
        // Helper to show a config button with unified logic
        private void ShowConfigButton(string configLabel, Func<bool, bool> configAction)
        {
            if (GUILayout.Button($"Configure {configLabel}", GUILayout.Height(30)))
            {
                bool added = configAction(_tabsIndentationJson);
                if (added)
                {
                    EditorUtility.DisplayDialog("Success", $"The MCP configuration was successfully added to the {configLabel} config file.", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", $"The MCP configuration could not be added to the {configLabel} config file.", "OK");
                }
            }
        }

        
        #endregion
    }
}
