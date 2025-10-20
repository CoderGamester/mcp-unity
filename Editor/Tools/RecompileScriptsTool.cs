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
    public class RecompileScriptsTool : McpToolBase
    {
        private readonly List<CompilerMessage> _compilationLogs = new List<CompilerMessage>();
        private TaskCompletionSource<JObject> _completionSource;
        private int _processedAssemblies = 0;
        private bool _returnWithLogs = true;
        private int _logsLimit = 100;

        public RecompileScriptsTool()
        {
            Name = "recompile_scripts";
            Description = "Recompiles all scripts in the Unity project. Use returnWithLogs parameter to control whether compilation logs are returned. Use logsLimit parameter to limit the number of logs returned when returnWithLogs is true.";
            IsAsync = true; // Compilation is asynchronous
        }

        /// <summary>
        /// Execute the Recompile tool asynchronously
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        /// <param name="tcs">TaskCompletionSource to set the result or exception</param>
        public override void ExecuteAsync(JObject parameters, TaskCompletionSource<JObject> tcs)
        {
            _completionSource = tcs;
            _compilationLogs.Clear();
            _processedAssemblies = 0;

            // Extract and store parameters
            _returnWithLogs = GetBoolParameter(parameters, "returnWithLogs", true);
            _logsLimit = Mathf.Clamp(GetIntParameter(parameters, "logsLimit", 100), 0, 1000);

            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;

            if (EditorApplication.isCompiling == false) {
                McpLogger.LogInfo($"Recompiling all scripts in the Unity project (logsLimit: {_logsLimit})");
                CompilationPipeline.RequestScriptCompilation();
            }
            else {
                McpLogger.LogInfo("Recompilation already in progress. Waiting for completion...");
            }
        }

        private void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            _processedAssemblies++;
            _compilationLogs.AddRange(messages);
        }

        private void OnCompilationFinished(object _)
        {
            McpLogger.LogInfo($"Recompilation completed. Processed {_processedAssemblies} assemblies with {_compilationLogs.Count} compiler messages");

            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;

            try
            {
                // Format logs as JSON array similar to ConsoleLogsService
                JArray logsArray = new JArray();
                
                // Separate errors, warnings, and other messages
                List<CompilerMessage> errors = _compilationLogs.Where(m => m.type == CompilerMessageType.Error).ToList();
                List<CompilerMessage> warnings = _compilationLogs.Where(m => m.type == CompilerMessageType.Warning).ToList();
                List<CompilerMessage> others = _compilationLogs.Where(m => m.type != CompilerMessageType.Error && m.type != CompilerMessageType.Warning).ToList();

                int errorCount = errors.Count;
                int warningCount = warnings.Count;

                // Sort logs and apply logsLimit - prioritize errors if logsLimit is restrictive
                IEnumerable<CompilerMessage> sortedLogs;
                if (!_returnWithLogs || _logsLimit <= 0)
                {
                    sortedLogs = Enumerable.Empty<CompilerMessage>();
                }
                else
                {
                    // Always include all errors, then warnings, then other messages up to the logsLimit
                    var selectedLogs = errors.ToList();
                    var remainingSlots = _logsLimit - selectedLogs.Count;

                    if (remainingSlots > 0)
                    {
                        selectedLogs.AddRange(warnings.Take(remainingSlots));
                        remainingSlots = _logsLimit - selectedLogs.Count;
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

                bool hasErrors = errorCount > 0;
                string summaryMessage = hasErrors
                    ? $"Recompilation completed with {errorCount} error(s) and {warningCount} warning(s)"
                    : $"Successfully recompiled all scripts with {warningCount} warning(s)";
                
                summaryMessage += $" (returnWithLogs: {_returnWithLogs}, logsLimit: {_logsLimit})";

                var response = new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = summaryMessage,
                    ["logs"] = logsArray
                };

                McpLogger.LogInfo($"Setting recompilation result: success={!hasErrors}, errors={errorCount}, warnings={warningCount}");
                _completionSource.SetResult(response);
            }
            catch (Exception ex)
            {
                McpLogger.LogError($"Error creating recompilation response: {ex.Message}\n{ex.StackTrace}");
                _completionSource.SetResult(McpUnitySocketHandler.CreateErrorResponse(
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