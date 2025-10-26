using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using McpUnity.Unity;
using McpUnity.Utils;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace McpUnity.Tools {
    /// <summary>
    /// Tool to recompile all scripts in the Unity project
    /// </summary>
    public class RecompileScriptsTool : McpToolBase
    {
        private class CompilationRequest {
            public readonly bool ReturnWithLogs;
            public readonly int LogsLimit;
            public readonly TaskCompletionSource<JObject> CompletionSource;
            
            public CompilationRequest(bool returnWithLogs, int logsLimit, TaskCompletionSource<JObject> completionSource)
            {
                ReturnWithLogs = returnWithLogs;
                LogsLimit = logsLimit;
                CompletionSource = completionSource;
            }
        }
        
        private readonly List<CompilationRequest> _pendingRequests = new List<CompilationRequest>();
        private readonly List<CompilerMessage> _compilationLogs = new List<CompilerMessage>();
        private int _processedAssemblies = 0;

        public RecompileScriptsTool()
        {
            Name = "recompile_scripts";
            Description = "Recompiles all scripts in the Unity project";
            IsAsync = true; // Compilation is asynchronous
        }

        /// <summary>
        /// Execute the Recompile tool asynchronously
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        /// <param name="tcs">TaskCompletionSource to set the result or exception</param>
        public override void ExecuteAsync(JObject parameters, TaskCompletionSource<JObject> tcs)
        {
            // Extract and store parameters
            var returnWithLogs = GetBoolParameter(parameters, "returnWithLogs", true);
            var logsLimit = Mathf.Clamp(GetIntParameter(parameters, "logsLimit", 100), 0, 1000);
            var request = new CompilationRequest(returnWithLogs, logsLimit, tcs);
            
            bool hasActiveRequest = false;
            lock (_pendingRequests)
            {
                hasActiveRequest = _pendingRequests.Count > 0;
                _pendingRequests.Add(request);
            }

            if (hasActiveRequest)
            {
                McpLogger.LogInfo("Recompilation already in progress. Waiting for completion...");
                return;
            }
            
            // On first request, initialize compilation listeners and start compilation
            StartCompilationTracking();
                
            if (EditorApplication.isCompiling == false)
            {
                McpLogger.LogInfo($"Recompiling all scripts in the Unity project (logsLimit: {logsLimit})");
                CompilationPipeline.RequestScriptCompilation();
            }
        }

        /// <summary>
        /// Subscribe to compilation events, reset tracked state
        /// </summary>
        private void StartCompilationTracking()
        {
            _compilationLogs.Clear();
            _processedAssemblies = 0;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
        }
        
        /// <summary>
        /// Unsubscribe from compilation events
        /// </summary>
        private void StopCompilationTracking()
        {
            CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
        }

        /// <summary>
        /// Record compilation logs for every single assembly
        /// </summary>
        /// <param name="assemblyPath"></param>
        /// <param name="messages"></param>
        private void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            _processedAssemblies++;
            _compilationLogs.AddRange(messages);
        }

        /// <summary>
        /// Complete all pending requests and stop tracking
        /// </summary>
        /// <param name="_"></param>
        private void OnCompilationFinished(object _)
        {
            McpLogger.LogInfo($"Recompilation completed. Processed {_processedAssemblies} assemblies with {_compilationLogs.Count} compiler messages");

            // Separate errors, warnings, and other messages
            List<CompilerMessage> errors = _compilationLogs.Where(m => m.type == CompilerMessageType.Error).ToList();
            List<CompilerMessage> warnings = _compilationLogs.Where(m => m.type == CompilerMessageType.Warning).ToList();
            List<CompilerMessage> others = _compilationLogs.Where(m => m.type != CompilerMessageType.Error && m.type != CompilerMessageType.Warning).ToList();
            
            // Stop tracking before completing requests
            StopCompilationTracking();
            
            // Complete all requests received before compilation end, the next received request will start a new compilation
            List<CompilationRequest> requestsToComplete = new List<CompilationRequest>();
            
            lock (_pendingRequests)
            {
                requestsToComplete.AddRange(_pendingRequests);
                _pendingRequests.Clear();
            }

            foreach (var request in requestsToComplete)
            {
                CompleteRequest(request, errors, warnings, others);
            }
        }

        /// <summary>
        /// Process a completed compilation request
        /// </summary>
        private static void CompleteRequest(CompilationRequest request, List<CompilerMessage> errors, List<CompilerMessage> warnings, List<CompilerMessage> others)
        {
            try {
                JArray logsArray = new JArray();

                // Sort logs and apply logsLimit - prioritize errors if logsLimit is restrictive
                IEnumerable<CompilerMessage> sortedLogs;
                if (!request.ReturnWithLogs || request.LogsLimit <= 0)
                {
                    sortedLogs = Enumerable.Empty<CompilerMessage>();
                }
                else {
                    // Always include all errors, then warnings, then other messages up to the logsLimit
                    var selectedLogs = errors.ToList();
                    var remainingSlots = request.LogsLimit - selectedLogs.Count;

                    if (remainingSlots > 0)
                    {
                        selectedLogs.AddRange(warnings.Take(remainingSlots));
                        remainingSlots = request.LogsLimit - selectedLogs.Count;
                    }

                    if (remainingSlots > 0)
                    {
                        selectedLogs.AddRange(others.Take(remainingSlots));
                    }

                    sortedLogs = selectedLogs;
                }

                foreach (var message in sortedLogs)
                {
                    var logObject = new JObject 
                    {
                        ["message"] = message.message,
                        ["type"] = message.type.ToString(),
                        ["timestamp"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                    };

                    // Add file information if available
                    if (!string.IsNullOrEmpty(message.file))
                    {
                        logObject["file"] = message.file;
                        logObject["line"] = message.line;
                        logObject["column"] = message.column;
                    }

                    logsArray.Add(logObject);
                }

                bool hasErrors = errors.Count > 0;
                string summaryMessage = hasErrors
                                            ? $"Recompilation completed with {errors.Count} error(s) and {warnings.Count} warning(s)"
                                            : $"Successfully recompiled all scripts with {warnings.Count} warning(s)";

                summaryMessage += $" (returnWithLogs: {request.ReturnWithLogs}, logsLimit: {request.LogsLimit})";

                var response = new JObject 
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = summaryMessage,
                    ["logs"] = logsArray
                };

                McpLogger.LogInfo($"Setting recompilation result: success={!hasErrors}, errors={errors.Count}, warnings={warnings.Count}");
                request.CompletionSource.SetResult(response);
            } 
            catch (Exception ex) 
            {
                McpLogger.LogError($"Error creating recompilation response: {ex.Message}\n{ex.StackTrace}");
                request.CompletionSource.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                    $"Error creating recompilation response: {ex.Message}",
                    "response_creation_error"
                ));
            }
        }

        /// <summary>
        /// Helper method to safely extract integer parameters with default values
        /// </summary>
        /// <param name="parameters">JObject containing parameters</param>
        /// <param name="key">Parameter key to extract</param>
        /// <param name="defaultValue">Default value if parameter is missing or invalid</param>
        /// <returns>Extracted integer value or default</returns>
        private static int GetIntParameter(JObject parameters, string key, int defaultValue)
        {
            if (parameters?[key] != null && int.TryParse(parameters[key].ToString(), out int value))
                return value;
            return defaultValue;
        }

        /// <summary>
        /// Helper method to safely extract boolean parameters with default values
        /// </summary>
        /// <param name="parameters">JObject containing parameters</param>
        /// <param name="key">Parameter key to extract</param>
        /// <param name="defaultValue">Default value if parameter is missing or invalid</param>
        /// <returns>Extracted boolean value or default</returns>
        private static bool GetBoolParameter(JObject parameters, string key, bool defaultValue)
        {
            if (parameters?[key] != null && bool.TryParse(parameters[key].ToString(), out bool value))
                return value;
            return defaultValue;
        }
    }
}