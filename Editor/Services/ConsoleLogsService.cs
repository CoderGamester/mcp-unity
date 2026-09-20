using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Services
{
    /// <summary>
    /// Service for managing Unity console logs
    /// </summary>
    public class ConsoleLogsService : IConsoleLogsService
    {
        // Static mapping for MCP log types to Unity log types
        // Some MCP types map to multiple Unity types (e.g., "error" includes Error, Exception and Assert)
        private static readonly Dictionary<string, HashSet<string>> LogTypeMapping = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "info", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Log" } },
            { "error", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Error", "Exception", "Assert" } }
        };
        
        // Structure to store log information
        private class LogEntry
        {
            public string Message { get; set; }
            public string StackTrace { get; set; }
            public LogType Type { get; set; }
            public DateTime Timestamp { get; set; }
        }
        
        // Reflection cache for Unity internal API (avoids repeated reflection)
        private static MethodInfo _logEntriesGetCountMethod;
        private static bool _reflectionInitialized;
        private static readonly object _reflectionLock = new object();
        
        // Constants for log management
        private const int MaxLogEntries = 1000;
        private const int CleanupThreshold = 200; // Remove oldest entries when exceeding max
        
        // Collection to store all log messages
        private readonly List<LogEntry> _logEntries = new List<LogEntry>();
        
        /// <summary>
        /// Constructor
        /// </summary>
        public ConsoleLogsService()
        {
            StartListening();
        }
        
        /// <summary>
        /// Start listening for logs
        /// </summary>
        public void StartListening()
        {
            // Register for log messages
            Application.logMessageReceivedThreaded += OnLogMessageReceived;

#if UNITY_6000_0_OR_NEWER
            // Unity 6 specific implementation
            ConsoleWindowUtility.consoleLogsChanged += OnConsoleCountChanged;
#else
            // Unity 2022.3 implementation using reflection
            EditorApplication.update += CheckConsoleClearViaReflection;
#endif
        }
        
        /// <summary>
        /// Stop listening for logs
        /// </summary>
        public void StopListening()
        {
            // Unregister from log messages
            Application.logMessageReceivedThreaded -= OnLogMessageReceived;
            
#if UNITY_6000_0_OR_NEWER
            // Unity 6 specific implementation
            ConsoleWindowUtility.consoleLogsChanged -= OnConsoleCountChanged;
#else
            // Unity 2022.3 implementation using reflection
            EditorApplication.update -= CheckConsoleClearViaReflection;
#endif
        }
        /// <summary>
        /// Get logs as a JSON array with pagination support
        /// </summary>
        /// <param name="logType">Filter by log type (empty for all)</param>
        /// <param name="offset">Starting index (0-based)</param>
        /// <param name="limit">Maximum number of logs to return (default: 100)</param>
        /// <param name="includeStackTrace">Whether to include stack trace in logs (default: true)</param>
        /// <returns>JObject containing logs array and pagination info</returns>
        public JObject GetLogsAsJson(string logType = "", int offset = 0, int limit = 100, bool includeStackTrace = true)
        {
            // Convert log entries to a JSON array, filtering by logType if provided
            JArray logsArray = new JArray();
            bool filter = !string.IsNullOrEmpty(logType);
            int totalCount = 0;
            int filteredCount = 0;
            int currentIndex = 0;
            
            // Map MCP log types to Unity log types outside the loop for better performance
            HashSet<string> unityLogTypes = null;
            if (filter)
            {
                if (LogTypeMapping.TryGetValue(logType, out var mapped))
                {
                    unityLogTypes = mapped;
                }
                else
                {
                    // If no mapping exists, create a set with the original type for case-insensitive comparison
                    unityLogTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { logType };
                }
            }
            
            lock (_logEntries)
            {
                totalCount = _logEntries.Count;
                
                // Single pass: count filtered entries and collect the requested page (newest first)
                for (int i = _logEntries.Count - 1; i >= 0; i--)
                {
                    var entry = _logEntries[i];
                    
                    // Skip if filtering and entry doesn't match the filter
                    if (filter && !unityLogTypes.Contains(entry.Type.ToString()))
                        continue;
                    
                    // Count filtered entries
                    filteredCount++;
                    
                    // Check if we're in the offset range and haven't reached the limit yet
                    if (currentIndex >= offset && logsArray.Count < limit)
                    {
                        var logObject = new JObject
                        {
                            ["message"] = entry.Message,
                            ["type"] = entry.Type.ToString(),
                            ["timestamp"] = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
                        };
                        
                        // Only include stack trace if requested
                        if (includeStackTrace)
                        {
                            logObject["stackTrace"] = entry.StackTrace;
                        }
                        
                        logsArray.Add(logObject);
                    }
                    
                    currentIndex++;
                    
                    // Early exit if we've collected enough logs
                    if (currentIndex >= offset + limit) break;
                }
            }
            
            return new JObject
            {
                ["logs"] = logsArray,
                ["_totalCount"] = totalCount,
                ["_filteredCount"] = filteredCount,
                ["_returnedCount"] = logsArray.Count
            };
        }
        
        /// <summary>
        /// Clear all stored logs
        /// </summary>
        private void ClearLogs()
        {
            lock (_logEntries)
            {
                _logEntries.Clear();
            }
        }
        
        /// <summary>
        /// Manually clean up old log entries, keeping only the most recent ones
        /// </summary>
        /// <param name="keepCount">Number of recent entries to keep (default: 500)</param>
        public void CleanupOldLogs(int keepCount = 500)
        {
            lock (_logEntries)
            {
                if (_logEntries.Count > keepCount)
                {
                    int removeCount = _logEntries.Count - keepCount;
                    _logEntries.RemoveRange(0, removeCount);
                }
            }
        }
        
        /// <summary>
        /// Get current log count
        /// </summary>
        /// <returns>Number of stored log entries</returns>
        public int GetLogCount()
        {
            lock (_logEntries)
            {
                return _logEntries.Count;
            }
        }
        
        /// <summary>
        /// Check if console was cleared using reflection (for Unity 2022.3)
        /// Uses cached reflection to avoid repeated expensive reflection calls
        /// </summary>
        private void CheckConsoleClearViaReflection()
        {
            // Initialize reflection once
            if (!_reflectionInitialized)
            {
                lock (_reflectionLock)
                {
                    if (!_reflectionInitialized)
                    {
                        try
                        {
                            var logEntriesType = Type.GetType("UnityEditor.LogEntries,UnityEditor");
                            if (logEntriesType != null)
                            {
                                _logEntriesGetCountMethod = logEntriesType.GetMethod("GetCount",
                                    BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[MCP Unity] Failed to initialize LogEntries reflection: {ex.Message}");
                        }
                        _reflectionInitialized = true;
                    }
                }
            }
            
            // If reflection failed to initialize, skip this check
            if (_logEntriesGetCountMethod == null) return;

            try
            {
                int currentTotalCount = (int)_logEntriesGetCountMethod.Invoke(null, null);
                        
                // If we had logs before, but now we don't, console was likely cleared
                if (currentTotalCount == 0 && _logEntries.Count > 0)
                {
                    ClearLogs();
                }
            }
            catch (Exception ex)
            {
                // Reflection invocation failed - clear cache so it retries next time
                _logEntriesGetCountMethod = null;
                _reflectionInitialized = false;
                Debug.LogWarning($"[MCP Unity] LogEntries reflection failed, will retry: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Callback for when a log message is received
        /// </summary>
        /// <param name="logString">The log message</param>
        /// <param name="stackTrace">The stack trace</param>
        /// <param name="type">The log type</param>
        private void OnLogMessageReceived(string logString, string stackTrace, LogType type)
        {
            // Add the log entry to our collection
            lock (_logEntries)
            {
                _logEntries.Add(new LogEntry
                {
                    Message = logString,
                    StackTrace = stackTrace,
                    Type = type,
                    Timestamp = DateTime.Now
                });
                
                // Clean up old entries if we exceed the maximum
                if (_logEntries.Count > MaxLogEntries)
                {
                    _logEntries.RemoveRange(0, CleanupThreshold);
                }
            }
        }
        
#if UNITY_6000_0_OR_NEWER
        /// <summary>
        /// Called when the console logs count changes
        /// </summary>
        private void OnConsoleCountChanged()
        {
            ConsoleWindowUtility.GetConsoleLogCounts(out int error, out int warning, out int log);
            if (error == 0 && warning == 0 && log == 0 && _logEntries.Count > 0)
            {
                ClearLogs();
            }
        }
#endif
    }
}
