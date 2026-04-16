using System;
using Newtonsoft.Json.Linq;
using McpUnity.Services;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for retrieving logs from the Unity console with pagination support
    /// </summary>
    public class GetConsoleLogsTool : McpToolBase
    {
        private readonly IConsoleLogsService _consoleLogsService;

        public GetConsoleLogsTool(IConsoleLogsService consoleLogsService)
        {
            Name = "get_console_logs";
            Description = "Retrieves logs from the Unity console with pagination support to avoid token limits";
            _consoleLogsService = consoleLogsService;
        }

        public override JObject Execute(JObject parameters)
        {
            try
            {
                string logType = parameters?["logType"]?.ToString();
                if (string.IsNullOrWhiteSpace(logType)) logType = null;

                int offset = Math.Max(0, GetIntParameter(parameters, "offset", 0));
                int limit = Math.Max(1, Math.Min(500, GetIntParameter(parameters, "limit", 50)));
                bool includeStackTrace = GetBoolParameter(parameters, "includeStackTrace", true);

                // Use the console logs service to get logs
                JObject result = _consoleLogsService.GetLogsAsJson(logType, offset, limit, includeStackTrace);

                // Add formatted message with pagination info
                string typeFilter = logType != null ? $" of type '{logType}'" : "";
                int returnedCount = result["_returnedCount"]?.Value<int>() ?? 0;
                int filteredCount = result["_filteredCount"]?.Value<int>() ?? 0;
                int totalCount = result["_totalCount"]?.Value<int>() ?? 0;

                result["message"] = $"Retrieved {returnedCount} of {filteredCount} log entries{typeFilter} (offset: {offset}, limit: {limit}, total: {totalCount})";
                result["success"] = true;
                result["type"] = "text";

                // Remove internal count fields
                result.Remove("_totalCount");
                result.Remove("_filteredCount");
                result.Remove("_returnedCount");

                McpLogger.LogInfo($"Console logs retrieved: {returnedCount} entries (logType={logType}, offset={offset}, limit={limit}, includeStackTrace={includeStackTrace})");

                return result;
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error retrieving console logs: {ex.Message}",
                    "console_logs_error"
                );
            }
        }

        private static int GetIntParameter(JObject parameters, string key, int defaultValue)
        {
            if (parameters?[key] != null && int.TryParse(parameters[key].ToString(), out int value))
                return value;
            return defaultValue;
        }

        private static bool GetBoolParameter(JObject parameters, string key, bool defaultValue)
        {
            if (parameters?[key] != null && bool.TryParse(parameters[key].ToString(), out bool value))
                return value;
            return defaultValue;
        }
    }
}
