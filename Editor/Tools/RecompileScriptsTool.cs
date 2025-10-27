using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        private class CompilationRequest 
        {
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
        
        private class CompilationResult 
        {
            public readonly List<CompilerMessage> SortedLogs;
            public readonly int WarningsCount;
            public readonly int ErrorsCount;
            
            public bool HasErrors => ErrorsCount > 0;
            
            public CompilationResult(List<CompilerMessage> sortedLogs, int warningsCount, int errorsCount) 
            {
                SortedLogs = sortedLogs;
                WarningsCount = warningsCount;
                ErrorsCount = errorsCount;
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
                McpLogger.LogInfo("Recompiling all scripts in the Unity project");
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
        private void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            _processedAssemblies++;
            _compilationLogs.AddRange(messages);
        }

        /// <summary>
        /// Stop tracking and complete all pending requests
        /// </summary>
        private void OnCompilationFinished(object _)
        {
            McpLogger.LogInfo($"Recompilation completed. Processed {_processedAssemblies} assemblies with {_compilationLogs.Count} compiler messages");

            // Sort logs by type: first errors, then warnings and info
            List<CompilerMessage> sortedLogs = _compilationLogs.OrderBy(x => x.type).ToList();
            int errorsCount = _compilationLogs.Count(l => l.type == CompilerMessageType.Error);
            int warningsCount = _compilationLogs.Count(l => l.type == CompilerMessageType.Warning);
            CompilationResult result = new CompilationResult(sortedLogs, warningsCount, errorsCount);
            
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
                CompleteRequest(request, result);
            }
        }

        /// <summary>
        /// Process a completed compilation request
        /// </summary>
        private static void CompleteRequest(CompilationRequest request, CompilationResult result)
        {
            JArray logsArray = new JArray();
            IEnumerable<CompilerMessage> logsToReturn = request.ReturnWithLogs ? result.SortedLogs.Take(request.LogsLimit) : Enumerable.Empty<CompilerMessage>();

            foreach (var message in logsToReturn)
            {
                var logObject = new JObject 
                {
                    ["message"] = message.message,
                    ["type"] = message.type.ToString()
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

            string summaryMessage = result.HasErrors
                                        ? $"Recompilation completed with {result.ErrorsCount} error(s) and {result.WarningsCount} warning(s)"
                                        : $"Successfully recompiled all scripts with {result.WarningsCount} warning(s)";

            summaryMessage += $" (returnWithLogs: {request.ReturnWithLogs}, logsLimit: {request.LogsLimit})";

            var response = new JObject 
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = summaryMessage,
                ["logs"] = logsArray
            };

            request.CompletionSource.SetResult(response);
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