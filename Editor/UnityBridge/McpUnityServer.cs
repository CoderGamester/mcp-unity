using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEngine;
using McpUnity.Tools;
using McpUnity.Resources;
using McpUnity.Services;
using McpUnity.Utils;
using WebSocketSharp.Server;
using System.IO;
using System.Net.Sockets;
using UnityEditor.Callbacks;

namespace McpUnity.Unity
{
    /// <summary>
    /// Custom WebSocket close codes for Unity-specific events.
    /// Range 4000-4999 is reserved for application use.
    /// </summary>
    public static class UnityCloseCode
    {
        /// <summary>
        /// Unity is entering Play mode - clients should use fast polling instead of backoff
        /// </summary>
        public const ushort PlayMode = 4001;
    }

    /// <summary>
    /// MCP Unity Server to communicate Node.js MCP server.
    /// Uses WebSockets to communicate with Node.js.
    /// </summary>
    [InitializeOnLoad]
    public class McpUnityServer : IDisposable
    {
        private static McpUnityServer _instance;

        private readonly Dictionary<string, McpToolBase> _tools = new Dictionary<string, McpToolBase>();
        private readonly Dictionary<string, McpResourceBase> _resources = new Dictionary<string, McpResourceBase>();

        private const int DelayedStartMaxAttempts = 10;
        private static readonly double[] DelayedStartRetryDelaySeconds = { 0.25d, 0.5d, 1d, 2d, 3d, 5d };

        // Connect + read budget for the post-start liveness probe. Generous enough not to trip on a
        // busy editor, short enough that the probe thread is always gone long before the next start.
        private const int LivenessProbeTimeoutMs = 2000;

        private WebSocketServer _webSocketServer;
        private CancellationTokenSource _cts;
        private TestRunnerService _testRunnerService;
        private ConsoleLogsService _consoleLogsService;
        private bool _delayedStartScheduled;
        private bool _delayedStartRequiresAutoStart;
        private int _delayedStartAttempt;
        private double _delayedStartEarliestTime;
        private string _delayedStartReason;
        private int _connectionGeneration;
        private int _activeConnectionGeneration;

        private enum StartServerResult
        {
            Started,
            AlreadyListening,
            Skipped,
            AddressAlreadyInUse,
            Failed
        }

        /// <summary>
        /// Singleton instance accessor. Returns null in batch mode.
        /// </summary>
        public static McpUnityServer Instance
        {
            get
            {
                // Don't create instance in batch mode to avoid hanging builds
                if (Application.isBatchMode)
                {
                    return null;
                }
                
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
        /// True when a delayed start or retry is waiting for Unity/editor socket cleanup.
        /// </summary>
        public bool HasScheduledStart => _delayedStartScheduled;

        /// <summary>
        /// Human-readable status for scheduled restart attempts.
        /// </summary>
        public string ScheduledStartStatus
        {
            get
            {
                if (!_delayedStartScheduled)
                {
                    return string.Empty;
                }

                return $"Retrying port {McpUnitySettings.Instance.Port} (attempt {_delayedStartAttempt}/{DelayedStartMaxAttempts})";
            }
        }

        /// <summary>
        /// Thread-safe dictionary of connected clients with this server.
        /// WebSocketSharp dispatches OnOpen/OnClose on thread pool threads,
        /// so concurrent access must be safe.
        /// </summary>
        public ConcurrentDictionary<string, string> Clients { get; } = new ConcurrentDictionary<string, string>();

        /// <summary>
        /// Disposes the McpUnityServer instance, stopping the WebSocket server and unsubscribing from Unity Editor events.
        /// This method ensures proper cleanup of resources and prevents memory leaks or unexpected behavior during domain reloads or editor shutdown.
        /// </summary>
        public void Dispose()
        {
            StopServer();

            EditorApplication.quitting -= OnEditorQuitting;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Start the WebSocket Server to communicate with Node.js
        /// </summary>
        public void StartServer()
        {
            CancelScheduledStart();
            ScheduleStartServer(requireAutoStart: false, reason: "manual start");
        }

        /// <summary>
        /// Stop the current server and start it again after Unity has had a chance to release the socket.
        /// </summary>
        public void RestartServer()
        {
            StopServer();
            ScheduleStartServer(requireAutoStart: false, reason: "manual restart");
        }

        /// <summary>
        /// Stop the WebSocket server
        /// </summary>
        /// <param name="closeCode">Optional custom close code to send to clients before stopping</param>
        /// <param name="closeReason">Optional reason message for the close</param>
        public void StopServer(ushort? closeCode = null, string closeReason = null)
        {
            CancelScheduledStart();
            _activeConnectionGeneration = 0;
            McpBackgroundTick.Stop();

            if (_webSocketServer == null)
            {
                Clients.Clear();
                return;
            }

            try
            {
                CloseAllClients(closeCode ?? 1000, closeReason ?? "Server stopping");

                _webSocketServer?.Stop();

                McpLogger.LogInfo("WebSocket server stopped");
            }
            catch (Exception ex)
            {
                McpLogger.LogError($"Error during WebSocketServer.Stop(): {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                _webSocketServer = null;
                Clients.Clear();
                McpLogger.LogInfo("WebSocket server stopped and resources cleaned up.");
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
        /// Installs the MCP Node.js server by running 'npm install' and 'npm run build'
        /// in the server directory if 'node_modules' or 'build' folders are missing.
        /// </summary>
        public void InstallServer()
        {
            string serverPath = McpUtils.GetServerPath();

            if (string.IsNullOrEmpty(serverPath) || !Directory.Exists(serverPath))
            {
                McpLogger.LogError($"Server path not found or invalid: {serverPath}. Make sure that MCP Node.js server is installed.");
                return;
            }

            // Validate server path and warn about potential issues (spaces, special characters)
            if (!McpUtils.ValidateServerPath(serverPath))
            {
                McpLogger.LogError("Server path validation failed. See previous errors for details.");
                return;
            }

            string nodeModulesPath = Path.Combine(serverPath, "node_modules");
            if (!Directory.Exists(nodeModulesPath))
            {
                McpUtils.RunNpmCommand("install", serverPath);
            }

            string buildPath = Path.Combine(serverPath, "build");
            if (!Directory.Exists(buildPath))
            {
                McpUtils.RunNpmCommand("run build", serverPath);
            }
        }

        internal bool ShouldTrackClient(int connectionGeneration)
        {
            return connectionGeneration == _activeConnectionGeneration && IsListening;
        }

        /// <summary>
        /// Private constructor to enforce singleton pattern
        /// </summary>
        private McpUnityServer()
        {
            // Skip all initialization in batch mode (Unity Cloud Build, CI, headless builds)
            // The npm install/build commands can hang indefinitely without node.js available
            if (Application.isBatchMode)
            {
                McpLogger.LogInfo("MCP Unity server disabled: Running in batch mode (Unity Cloud Build or CI)");
                return;
            }

            EditorApplication.quitting -= OnEditorQuitting; // Prevent multiple subscriptions on domain reload
            EditorApplication.quitting += OnEditorQuitting;

            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            InstallServer();
            InitializeServices();
            RegisterResources();
            RegisterTools();

            // Initial start if auto-start is enabled and not recovering from a reload where it was off
            if (McpUnitySettings.Instance.AutoStartServer)
            {
                ScheduleStartServer(requireAutoStart: true, reason: "auto-start");
            }
        }

        private StartServerResult StartServerInternal(bool logAddressInUseAsError, int attempt = 1, double nextRetryDelaySeconds = 0)
        {
            // Skip starting server if this is a Multiplayer Play Mode clone instance
            // Only the main editor should run the WebSocket server to avoid port conflicts
            if (McpUtils.IsMultiplayerPlayModeClone())
            {
                McpLogger.LogInfo("Server startup skipped: Running as Multiplayer Play Mode clone instance. Only the main editor runs the MCP server.");
                return StartServerResult.Skipped;
            }

            if (IsListening)
            {
                McpLogger.LogInfo($"Server start requested, but already listening on port {McpUnitySettings.Instance.Port}.");
                return StartServerResult.AlreadyListening;
            }

            if (_webSocketServer != null)
            {
                StopServer();
            }

            WebSocketServer webSocketServer = null;
            try
            {
                int connectionGeneration = Interlocked.Increment(ref _connectionGeneration);
                var host = McpUnitySettings.Instance.AllowRemoteConnections ? "0.0.0.0" : "localhost";
                webSocketServer = new WebSocketServer($"ws://{host}:{McpUnitySettings.Instance.Port}");
                webSocketServer.Log.Output = (data, path) => { };
                webSocketServer.AddWebSocketService("/McpUnity", () => new McpUnitySocketHandler(this, connectionGeneration));
                webSocketServer.Start();
                _webSocketServer = webSocketServer;
                _activeConnectionGeneration = connectionGeneration;
                McpBackgroundTick.Start();
                McpLogger.LogInfo($"WebSocket server started successfully on {host}:{McpUnitySettings.Instance.Port}.");

                // Start() succeeding only proves the bind succeeded, not that the port is being
                // serviced. Confirm the latter out-of-band (see the method's remarks).
                VerifyServerIsActuallyServing(host, McpUnitySettings.Instance.Port);

                return StartServerResult.Started;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                CleanupFailedStart(webSocketServer);
                string message = $"Failed to start WebSocket server: Port {McpUnitySettings.Instance.Port} is already in use. {ex.Message}";
                if (logAddressInUseAsError)
                {
                    McpLogger.LogError(message);
                }
                else
                {
                    McpLogger.LogWarning($"{message} Attempt {attempt}/{DelayedStartMaxAttempts}; retrying in {nextRetryDelaySeconds:0.##}s.");
                }

                return StartServerResult.AddressAlreadyInUse;
            }
            catch (Exception ex)
            {
                CleanupFailedStart(webSocketServer);
                McpLogger.LogError($"Failed to start WebSocket server: {ex.Message}\n{ex.StackTrace}");
                return StartServerResult.Failed;
            }
        }

        private static double GetDelayedStartDelaySeconds(int attempt)
        {
            int normalizedAttempt = Math.Max(attempt, 1);
            int delayIndex = Math.Min(normalizedAttempt - 1, DelayedStartRetryDelaySeconds.Length - 1);
            return DelayedStartRetryDelaySeconds[delayIndex];
        }

        /// <summary>
        /// Confirms out-of-band that the port just bound is actually being serviced.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A successful <c>WebSocketServer.Start()</c> proves only that the bind succeeded. On
        /// Windows it is possible to end up bound to a port that then accepts TCP connections and
        /// never answers them, so the editor reports a healthy server while every client hangs.
        /// Reported as issue #141 ("Port N is already in use", cleared only by restarting Unity).
        /// </para>
        /// <para>
        /// Silence with the connection held open is the distinguishing signature: a healthy server
        /// either answers or closes the socket. So "data received" AND "peer closed" both count as
        /// alive, and only a read timeout counts as dead.
        /// </para>
        /// <para>
        /// This only DETECTS and reports the condition; it deliberately does not try to repair it.
        /// The message therefore describes what was observed rather than asserting a cause.
        /// </para>
        /// <para>
        /// Every resolved address must be tried. Binding "localhost" can yield an IPv6-only
        /// listener (::1), against which 127.0.0.1 is refused - so a default <c>new TcpClient()</c>
        /// (which is IPv4) cannot reach a perfectly healthy server and would false-alarm.
        /// </para>
        /// <para>
        /// Runs off the main thread, and uses a deliberately non-existent path so it can never
        /// create a /McpUnity session. Only LogWarning/LogError are called from the worker.
        /// </para>
        /// </remarks>
        private static void VerifyServerIsActuallyServing(string host, int port)
        {
            var probeHost = host == "0.0.0.0" ? "localhost" : host;

            var probe = new Thread(() =>
            {
                try
                {
                    // Let the accept loop settle; Start() can return marginally early.
                    Thread.Sleep(250);

                    System.Net.IPAddress[] addresses;
                    try
                    {
                        addresses = System.Net.Dns.GetHostAddresses(probeHost);
                    }
                    catch
                    {
                        return;
                    }

                    foreach (var address in addresses)
                    {
                        using (var client = new TcpClient(address.AddressFamily))
                        {
                            try
                            {
                                if (!client.ConnectAsync(address, port).Wait(LivenessProbeTimeoutMs))
                                {
                                    continue;
                                }
                            }
                            catch
                            {
                                // Refused/unreachable on this family - try the next address.
                                continue;
                            }

                            var stream = client.GetStream();
                            stream.ReadTimeout = LivenessProbeTimeoutMs;

                            // A healthy websocket-sharp answers "HTTP/1.1 501 Not Implemented" to an
                            // unknown path and closes, which proves the accept loop is live without
                            // ever creating a /McpUnity session.
                            var request = System.Text.Encoding.ASCII.GetBytes(
                                "GET /__mcp_liveness__ HTTP/1.1\r\n" +
                                $"Host: {probeHost}:{port}\r\n" +
                                "Upgrade: websocket\r\n" +
                                "Connection: Upgrade\r\n" +
                                "Sec-WebSocket-Key: AAAAAAAAAAAAAAAAAAAAAA==\r\n" +
                                "Sec-WebSocket-Version: 13\r\n\r\n");
                            stream.Write(request, 0, request.Length);

                            var buffer = new byte[64];
                            try
                            {
                                // Returns >0 (answered) or 0 (peer closed) on a live server; throws
                                // IOException on timeout only when nobody is servicing the port.
                                stream.Read(buffer, 0, buffer.Length);
                            }
                            catch (IOException)
                            {
                                McpLogger.LogError(
                                    $"MCP bridge is not serving: port {port} accepted a connection but " +
                                    "returned nothing and did not close it. Clients will hang rather than " +
                                    "fail. Restarting the server from the Server Window may not clear " +
                                    "this; restarting the Unity Editor does.");
                            }

                            // One successful connect is a conclusive test either way.
                            return;
                        }
                    }

                    // No address accepted a connection at all. That is ambiguous - the listener may
                    // still be settling - and the Node bridge's own connect is the authoritative
                    // test, so stay silent rather than false-alarm.
                }
                catch (Exception ex)
                {
                    McpLogger.LogWarning($"MCP liveness probe failed to run: {ex.Message}");
                }
            });

            probe.IsBackground = true;
            probe.Name = "McpUnity Liveness Probe";
            probe.Start();
        }

        private void ScheduleStartServer(bool requireAutoStart, string reason, int attempt = 1)
        {
            int normalizedAttempt = Math.Min(Math.Max(attempt, 1), DelayedStartMaxAttempts);
            if (_delayedStartScheduled)
            {
                _delayedStartRequiresAutoStart = _delayedStartRequiresAutoStart && requireAutoStart;
                _delayedStartAttempt = Math.Max(_delayedStartAttempt, normalizedAttempt);
                _delayedStartReason = reason;
                return;
            }

            _delayedStartScheduled = true;
            _delayedStartRequiresAutoStart = requireAutoStart;
            _delayedStartAttempt = normalizedAttempt;
            _delayedStartReason = reason;
            double delaySeconds = GetDelayedStartDelaySeconds(normalizedAttempt);
            _delayedStartEarliestTime = EditorApplication.timeSinceStartup + delaySeconds;
            McpLogger.LogInfo($"WebSocket server start scheduled in {delaySeconds:0.##}s ({reason}, attempt {normalizedAttempt}/{DelayedStartMaxAttempts}).");
            EditorApplication.delayCall += StartServerAfterDelay;
            EditorApplication.update += StartServerAfterDelayOnUpdate;
        }

        private void CancelScheduledStart()
        {
            if (!_delayedStartScheduled)
            {
                return;
            }

            EditorApplication.delayCall -= StartServerAfterDelay;
            EditorApplication.update -= StartServerAfterDelayOnUpdate;
            _delayedStartScheduled = false;
            _delayedStartRequiresAutoStart = false;
            _delayedStartAttempt = 0;
            _delayedStartEarliestTime = 0;
            _delayedStartReason = null;
        }

        private void StartServerAfterDelay()
        {
            if (!_delayedStartScheduled)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup < _delayedStartEarliestTime)
            {
                EditorApplication.delayCall += StartServerAfterDelay;
                return;
            }

            RunScheduledStart();
        }

        private void StartServerAfterDelayOnUpdate()
        {
            if (!_delayedStartScheduled)
            {
                EditorApplication.update -= StartServerAfterDelayOnUpdate;
                return;
            }

            if (EditorApplication.timeSinceStartup < _delayedStartEarliestTime)
            {
                return;
            }

            RunScheduledStart();
        }

        private void RunScheduledStart()
        {
            _delayedStartScheduled = false;
            EditorApplication.delayCall -= StartServerAfterDelay;
            EditorApplication.update -= StartServerAfterDelayOnUpdate;

            bool requireAutoStart = _delayedStartRequiresAutoStart;
            int attempt = Math.Min(Math.Max(_delayedStartAttempt, 1), DelayedStartMaxAttempts);
            string reason = _delayedStartReason;
            _delayedStartRequiresAutoStart = false;
            _delayedStartAttempt = 0;
            _delayedStartEarliestTime = 0;
            _delayedStartReason = null;

            if (Application.isBatchMode || _instance != this)
            {
                return;
            }

            if (requireAutoStart && !McpUnitySettings.Instance.AutoStartServer)
            {
                McpLogger.LogInfo("Scheduled WebSocket server start skipped because auto-start is disabled.");
                return;
            }

            if (IsListening)
            {
                return;
            }

            bool isFinalAttempt = attempt >= DelayedStartMaxAttempts;
            double nextRetryDelaySeconds = isFinalAttempt ? 0 : GetDelayedStartDelaySeconds(attempt + 1);
            StartServerResult result = StartServerInternal(isFinalAttempt, attempt, nextRetryDelaySeconds);
            if (result == StartServerResult.AddressAlreadyInUse && !isFinalAttempt)
            {
                ScheduleStartServer(requireAutoStart, reason ?? "port still in use", attempt + 1);
            }
        }

        private void CleanupFailedStart(WebSocketServer webSocketServer)
        {
            McpBackgroundTick.Stop();

            if (webSocketServer == null)
            {
                Clients.Clear();
                return;
            }

            try
            {
                if (webSocketServer.IsListening)
                {
                    webSocketServer.Stop();
                }
            }
            catch (Exception ex)
            {
                McpLogger.LogWarning($"Error cleaning up failed WebSocket server start: {ex.Message}");
            }
            finally
            {
                if (ReferenceEquals(_webSocketServer, webSocketServer))
                {
                    _webSocketServer = null;
                }

                Clients.Clear();
            }
        }

        /// <summary>
        /// Close all connected clients with a specific close code
        /// </summary>
        /// <param name="closeCode">WebSocket close code</param>
        /// <param name="reason">Reason message for the close</param>
        private void CloseAllClients(ushort closeCode, string reason)
        {
            if (_webSocketServer == null)
            {
                return;
            }

            try
            {
                var service = _webSocketServer.WebSocketServices["/McpUnity"];
                if (service?.Sessions != null)
                {
                    // Get all active session IDs and close each with the custom code
                    var sessionIds = new List<string>(service.Sessions.IDs);
                    foreach (var sessionId in sessionIds)
                    {
                        service.Sessions.CloseSession(sessionId, closeCode, reason);
                    }
                    McpLogger.LogInfo($"Closed {sessionIds.Count} client connection(s) with code {closeCode}: {reason}");
                }
            }
            catch (Exception ex)
            {
                McpLogger.LogError($"Error closing client connections: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Register all available tools
        /// </summary>
        private void RegisterTools()
        {
            // Register MenuItemTool
            MenuItemTool menuItemTool = new MenuItemTool();
            _tools.Add(menuItemTool.Name, menuItemTool);

            // Register SelectGameObjectTool
            SelectGameObjectTool selectGameObjectTool = new SelectGameObjectTool();
            _tools.Add(selectGameObjectTool.Name, selectGameObjectTool);

            // Register UpdateGameObjectTool
            UpdateGameObjectTool updateGameObjectTool = new UpdateGameObjectTool();
            _tools.Add(updateGameObjectTool.Name, updateGameObjectTool);
            
            // Register PackageManagerTool
            AddPackageTool addPackageTool = new AddPackageTool();
            _tools.Add(addPackageTool.Name, addPackageTool);
            
            // Register RunTestsTool
            RunTestsTool runTestsTool = new RunTestsTool(_testRunnerService);
            _tools.Add(runTestsTool.Name, runTestsTool);
            
            // Register SendConsoleLogTool
            SendConsoleLogTool sendConsoleLogTool = new SendConsoleLogTool();
            _tools.Add(sendConsoleLogTool.Name, sendConsoleLogTool);
            
            // Register GetConsoleLogsTool
            GetConsoleLogsTool getConsoleLogsTool = new GetConsoleLogsTool(_consoleLogsService);
            _tools.Add(getConsoleLogsTool.Name, getConsoleLogsTool);
            
            // Register UpdateComponentTool
            UpdateComponentTool updateComponentTool = new UpdateComponentTool();
            _tools.Add(updateComponentTool.Name, updateComponentTool);
            
            // Register AddAssetToSceneTool
            AddAssetToSceneTool addAssetToSceneTool = new AddAssetToSceneTool();
            _tools.Add(addAssetToSceneTool.Name, addAssetToSceneTool);
            
            // Register CreatePrefabTool
            CreatePrefabTool createPrefabTool = new CreatePrefabTool();
            _tools.Add(createPrefabTool.Name, createPrefabTool);

            // Register CreateSceneTool
            CreateSceneTool createSceneTool = new CreateSceneTool();
            _tools.Add(createSceneTool.Name, createSceneTool);

            // Register DeleteSceneTool
            DeleteSceneTool deleteSceneTool = new DeleteSceneTool();
            _tools.Add(deleteSceneTool.Name, deleteSceneTool);

            // Register LoadSceneTool
            LoadSceneTool loadSceneTool = new LoadSceneTool();
            _tools.Add(loadSceneTool.Name, loadSceneTool);

            // Register SaveSceneTool
            SaveSceneTool saveSceneTool = new SaveSceneTool();
            _tools.Add(saveSceneTool.Name, saveSceneTool);

            // Register GetSceneInfoTool
            GetSceneInfoTool getSceneInfoTool = new GetSceneInfoTool();
            _tools.Add(getSceneInfoTool.Name, getSceneInfoTool);

            // Register GetPlayModeStatusTool
            GetPlayModeStatusTool getPlayModeStatusTool = new GetPlayModeStatusTool();
            _tools.Add(getPlayModeStatusTool.Name, getPlayModeStatusTool);

            // Register SetPlayModeStatusTool
            SetPlayModeStatusTool setPlayModeStatusTool = new SetPlayModeStatusTool();
            _tools.Add(setPlayModeStatusTool.Name, setPlayModeStatusTool);

            // Register UnloadSceneTool
            UnloadSceneTool unloadSceneTool = new UnloadSceneTool();
            _tools.Add(unloadSceneTool.Name, unloadSceneTool);

            // Register RecompileScriptsTool
            RecompileScriptsTool recompileScriptsTool = new RecompileScriptsTool();
            _tools.Add(recompileScriptsTool.Name, recompileScriptsTool);
            
            // Register GetGameObjectTool
            GetGameObjectTool getGameObjectTool = new GetGameObjectTool();
            _tools.Add(getGameObjectTool.Name, getGameObjectTool);

            // Register DuplicateGameObjectTool
            DuplicateGameObjectTool duplicateGameObjectTool = new DuplicateGameObjectTool();
            _tools.Add(duplicateGameObjectTool.Name, duplicateGameObjectTool);

            // Register DeleteGameObjectTool
            DeleteGameObjectTool deleteGameObjectTool = new DeleteGameObjectTool();
            _tools.Add(deleteGameObjectTool.Name, deleteGameObjectTool);

            // Register ReparentGameObjectTool
            ReparentGameObjectTool reparentGameObjectTool = new ReparentGameObjectTool();
            _tools.Add(reparentGameObjectTool.Name, reparentGameObjectTool);

            // Register Transform Tools
            MoveGameObjectTool moveGameObjectTool = new MoveGameObjectTool();
            _tools.Add(moveGameObjectTool.Name, moveGameObjectTool);

            RotateGameObjectTool rotateGameObjectTool = new RotateGameObjectTool();
            _tools.Add(rotateGameObjectTool.Name, rotateGameObjectTool);

            ScaleGameObjectTool scaleGameObjectTool = new ScaleGameObjectTool();
            _tools.Add(scaleGameObjectTool.Name, scaleGameObjectTool);

            SetTransformTool setTransformTool = new SetTransformTool();
            _tools.Add(setTransformTool.Name, setTransformTool);

            // Register Material Tools
            CreateMaterialTool createMaterialTool = new CreateMaterialTool();
            _tools.Add(createMaterialTool.Name, createMaterialTool);

            AssignMaterialTool assignMaterialTool = new AssignMaterialTool();
            _tools.Add(assignMaterialTool.Name, assignMaterialTool);

            ModifyMaterialTool modifyMaterialTool = new ModifyMaterialTool();
            _tools.Add(modifyMaterialTool.Name, modifyMaterialTool);

            GetMaterialInfoTool getMaterialInfoTool = new GetMaterialInfoTool();
            _tools.Add(getMaterialInfoTool.Name, getMaterialInfoTool);

            // Register BatchExecuteTool (must be registered last as it needs access to other tools)
            BatchExecuteTool batchExecuteTool = new BatchExecuteTool(this);
            _tools.Add(batchExecuteTool.Name, batchExecuteTool);
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
            GetConsoleLogsResource getConsoleLogsResource = new GetConsoleLogsResource(_consoleLogsService);
            _resources.Add(getConsoleLogsResource.Name, getConsoleLogsResource);
            
            // Register GetScenesHierarchyResource
            GetScenesHierarchyResource getScenesHierarchyResource = new GetScenesHierarchyResource();
            _resources.Add(getScenesHierarchyResource.Name, getScenesHierarchyResource);
            
            // Register GetPackagesResource
            GetPackagesResource getPackagesResource = new GetPackagesResource();
            _resources.Add(getPackagesResource.Name, getPackagesResource);
            
            // Register GetAssetsResource
            GetAssetsResource getAssetsResource = new GetAssetsResource();
            _resources.Add(getAssetsResource.Name, getAssetsResource);
            
            // Register GetTestsResource
            GetTestsResource getTestsResource = new GetTestsResource(_testRunnerService);
            _resources.Add(getTestsResource.Name, getTestsResource);
            
            // Register GetGameObjectResource
            GetGameObjectResource getGameObjectResource = new GetGameObjectResource();
            _resources.Add(getGameObjectResource.Name, getGameObjectResource);
        }
        
        /// <summary>
        /// Initialize services used by the server
        /// </summary>
        private void InitializeServices()
        {
            // Initialize the test runner service
            _testRunnerService = new TestRunnerService();
            
            // Initialize the console logs service
            _consoleLogsService = new ConsoleLogsService();
        }

        /// <summary>
        /// Called after every domain reload
        /// </summary>
        [DidReloadScripts]
        private static void AfterReload()
        {
            // Skip initialization in batch mode (Unity Cloud Build, CI, headless builds)
            // This prevents npm commands from hanging the build process
            if (Application.isBatchMode)
            {
                return;
            }

            // Ensure Instance is created and hooks are set up after initial domain load
            var currentInstance = Instance;
        }

        /// <summary>
        /// Handles the Unity Editor quitting event. Ensures the server is properly stopped and disposed.
        /// </summary>
        private static void OnEditorQuitting()
        {
            if (Application.isBatchMode || _instance == null) return;
            
            McpLogger.LogInfo("Editor is quitting. Ensuring server is stopped.");
            _instance.Dispose();
        }

        /// <summary>
        /// Handles the Unity Editor's 'before assembly reload' event.
        /// Stops the WebSocket server to prevent port conflicts and ensure a clean state before scripts are recompiled.
        /// </summary>
        private static void OnBeforeAssemblyReload()
        {
            if (Application.isBatchMode || _instance == null) return;
            
            _instance.StopServer();
        }

        /// <summary>
        /// Handles the Unity Editor's 'after assembly reload' event.
        /// If auto-start is enabled, attempts to restart the WebSocket server if it's not already listening.
        /// This ensures the server is operational after script recompilation.
        /// </summary>
        private static void OnAfterAssemblyReload()
        {
            if (Application.isBatchMode || _instance == null) return;
            
            if (McpUnitySettings.Instance.AutoStartServer && !_instance.IsListening)
            {
                _instance.ScheduleStartServer(requireAutoStart: true, reason: "assembly reload");
            }
        }

        /// <summary>
        /// Handles changes in Unity Editor's play mode state.
        /// Stops the server when exiting Edit Mode if configured, and restarts it when entering Play Mode or returning to Edit Mode if auto-start is enabled.
        /// </summary>
        /// <param name="state">The current play mode state change.</param>
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (Application.isBatchMode || _instance == null) return;
            
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    // About to enter Play Mode - use custom close code so clients use fast polling
                    _instance.StopServer(UnityCloseCode.PlayMode, "Unity entering Play mode");
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                    // The assumption above (server stays down through Play, a domain reload on exit
                    // will restart it) only holds when Enter Play Mode Options are OFF or don't disable
                    // domain reload. With "Reload Scene without Reload Domain" -- Project Settings >
                    // Editor > Enter Play Mode Options, a real, supported Unity profile some projects
                    // pick specifically for iteration speed -- NO domain reload happens on either side
                    // of Play Mode, so nothing was ever going to bring the server back. That leaves it
                    // down for the entire Play session, with no way for a client to even ask Unity to
                    // exit Play, since that request needs this same server. Confirmed live: a client
                    // got locked in Play with no way out until the Editor was closed by hand.
                    // Restarting here covers both profiles: if a domain reload already restarted it via
                    // OnAfterAssemblyReload, IsListening is already true and this is a no-op; if it
                    // didn't, this is the only thing that brings it back.
                    if (!_instance.IsListening && McpUnitySettings.Instance.AutoStartServer)
                    {
                        _instance.ScheduleStartServer(requireAutoStart: true, reason: "entered play mode");
                    }
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    // Returned to Edit Mode
                    if (!_instance.IsListening && McpUnitySettings.Instance.AutoStartServer)
                    {
                        _instance.ScheduleStartServer(requireAutoStart: true, reason: "entered edit mode");
                    }
                    break;
            }
        }
    }
}
