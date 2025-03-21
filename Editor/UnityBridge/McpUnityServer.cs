using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using McpUnity.Tools;
using McpUnity.Resources;
using McpUnity.Services;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace McpUnity.Unity
{
    /// <summary>
    /// MCP Unity Server to communicate Node.js MCP server.
    /// Uses WebSockets to communicate with Node.js.
    /// </summary>
    [InitializeOnLoad]
    public class McpUnityServer
    {
        private static McpUnityServer _instance;
        
        private readonly WebSocketServer _webSocketServer;
        private readonly Dictionary<string, McpToolBase> _tools = new Dictionary<string, McpToolBase>();
        private readonly Dictionary<string, McpResourceBase> _resources = new Dictionary<string, McpResourceBase>();
        
        private CancellationTokenSource _cts;
        private TestRunnerService _testRunnerService;
        
        /// <summary>
        /// Static constructor that gets called when Unity loads due to InitializeOnLoad attribute
        /// </summary>
        static McpUnityServer()
        {
            // Initialize the singleton instance when Unity loads
            // This ensures the bridge is available as soon as Unity starts
            EditorApplication.quitting += () => Instance.StopServer();
        }
        
        /// <summary>
        /// Singleton instance accessor
        /// </summary>
        public static McpUnityServer Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new McpUnityServer();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Current Listening state
        /// </summary>
        public bool IsListening => _webSocketServer?.IsListening ?? false;
        
        /// <summary>
        /// Private constructor to enforce singleton pattern
        /// </summary>
        private McpUnityServer()
        {
            InitializeServices();
            RegisterResources();
            RegisterTools();
            
            // Create a new WebSocket server
            _webSocketServer = new WebSocketServer(McpUnitySettings.Instance.Port);
            // Add the MCP service endpoint with a handler that references this server
            _webSocketServer.AddWebSocketService("/McpUnity", () => new McpUnitySocketHandler(this));
                
            Debug.Log($"[MCP Unity] Created WebSocket server on port {McpUnitySettings.Instance.Port}");
            
            StartServer();
        }
        
        /// <summary>
        /// Start the WebSocket Server to communicate with Node.js
        /// </summary>
        public void StartServer()
        {
            if (IsListening) return;
            
            try
            {
                // Start the server
                _webSocketServer.Start();
                
                Debug.Log("[MCP Unity] WebSocket server started");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP Unity] Failed to start WebSocket server: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Stop the WebSocket server
        /// </summary>
        public void StopServer()
        {
            if (!IsListening) return;
            
            try
            {
                _webSocketServer?.Stop();
                
                Debug.Log("[MCP Unity] WebSocket server stopped");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP Unity] Error stopping WebSocket server: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Try to get a tool by name
        /// </summary>
        public bool TryGetTool(string name, out McpToolBase tool)
        {
            return _tools.TryGetValue(name, out tool);
        }
        
        /// <summary>
        /// Try to get a resource by name
        /// </summary>
        public bool TryGetResource(string name, out McpResourceBase resource)
        {
            return _resources.TryGetValue(name, out resource);
        }
        
        /// <summary>
        /// Register all available tools
        /// </summary>
        private void RegisterTools()
        {
            // Register MenuItemTool
            MenuItemTool menuItemTool = new MenuItemTool();
            _tools.Add(menuItemTool.Name, menuItemTool);
            
            // Register SelectObjectTool
            SelectObjectTool selectObjectTool = new SelectObjectTool();
            _tools.Add(selectObjectTool.Name, selectObjectTool);
            
            // Register PackageManagerTool
            PackageManagerTool packageManagerTool = new PackageManagerTool();
            _tools.Add(packageManagerTool.Name, packageManagerTool);
            
            // Register RunTestsTool
            RunTestsTool runTestsTool = new RunTestsTool(_testRunnerService);
            _tools.Add(runTestsTool.Name, runTestsTool);
            
            // Register NotifyMessageTool
            NotifyMessageTool notifyMessageTool = new NotifyMessageTool();
            _tools.Add(notifyMessageTool.Name, notifyMessageTool);
        }
        
        /// <summary>
        /// Register all available resources
        /// </summary>
        private void RegisterResources()
        {
            // Register GetMenuItemsResource
            GetMenuItemsResource getMenuItemsResource = new GetMenuItemsResource();
            _resources.Add(getMenuItemsResource.Name, getMenuItemsResource);
            
            // Register GetConsoleLogsResource
            GetConsoleLogsResource getConsoleLogsResource = new GetConsoleLogsResource();
            _resources.Add(getConsoleLogsResource.Name, getConsoleLogsResource);
            
            // Register GetHierarchyResource
            GetHierarchyResource getHierarchyResource = new GetHierarchyResource();
            _resources.Add(getHierarchyResource.Name, getHierarchyResource);
            
            // Register GetPackagesResource
            GetPackagesResource getPackagesResource = new GetPackagesResource();
            _resources.Add(getPackagesResource.Name, getPackagesResource);
            
            // Register GetAssetsResource
            GetAssetsResource getAssetsResource = new GetAssetsResource();
            _resources.Add(getAssetsResource.Name, getAssetsResource);
            
            // Register GetTestsResource
            GetTestsResource getTestsResource = new GetTestsResource(_testRunnerService);
            _resources.Add(getTestsResource.Name, getTestsResource);
        }
        
        /// <summary>
        /// Initialize services used by tools and resources
        /// </summary>
        private void InitializeServices()
        {
            // Create TestRunnerService
            _testRunnerService = new TestRunnerService();
        }
    }
}
