using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using McpUnity.Unity;
using Newtonsoft.Json.Linq;
using Unity.EditorCoroutines.Editor;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for executing multiple operations in a single batch request.
    /// Supports sequential execution, stop-on-error, and atomic rollback.
    /// </summary>
    public class BatchExecuteTool : McpToolBase
    {
        private readonly McpUnityServer _server;

        public BatchExecuteTool(McpUnityServer server)
        {
            _server = server;
            Name = "batch_execute";
            Description = "Executes multiple tool operations in a single batch request. Reduces round-trips and enables atomic operations.";
            IsAsync = true;
        }

        public override void ExecuteAsync(JObject parameters, TaskCompletionSource<JObject> tcs)
        {
            EditorCoroutineUtility.StartCoroutineOwnerless(ExecuteBatchCoroutine(parameters, tcs));
        }

        private IEnumerator ExecuteBatchCoroutine(JObject parameters, TaskCompletionSource<JObject> tcs)
        {
            JArray operations = parameters["operations"] as JArray;
            bool stopOnError = parameters["stopOnError"]?.ToObject<bool?>() ?? true;
            bool atomic = parameters["atomic"]?.ToObject<bool?>() ?? false;

            // Validate operations array
            if (operations == null || operations.Count == 0)
            {
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                    "The 'operations' array is required and must contain at least one operation.",
                    "validation_error"
                ));
                yield break;
            }

            // Validate max operations (prevent abuse)
            if (operations.Count > 100)
            {
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                    "Maximum of 100 operations allowed per batch.",
                    "validation_error"
                ));
                yield break;
            }

            JArray results = new JArray();
            int succeeded = 0;
            int failed = 0;
            int undoGroup = -1;

            // Start undo group for atomic operations
            if (atomic)
            {
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Batch Execute");
            }

            for (int i = 0; i < operations.Count; i++)
            {
                JObject operation = operations[i] as JObject;
                if (operation == null)
                {
                    results.Add(CreateOperationResult(i, null, false, null, "Invalid operation format"));
                    failed++;

                    if (stopOnError)
                    {
                        RevertIfAtomic(atomic, undoGroup);
                        break;
                    }
                    continue;
                }

                string toolName = operation["tool"]?.ToString();
                JObject toolParams = operation["params"] as JObject ?? new JObject();
                string operationId = operation["id"]?.ToString() ?? i.ToString();

                // Validate tool name
                if (string.IsNullOrEmpty(toolName))
                {
                    results.Add(CreateOperationResult(i, operationId, false, null, "Missing 'tool' name in operation"));
                    failed++;

                    if (stopOnError)
                    {
                        RevertIfAtomic(atomic, undoGroup);
                        break;
                    }
                    continue;
                }

                // Prevent recursive batch execution
                if (toolName == Name)
                {
                    results.Add(CreateOperationResult(i, operationId, false, null, "Cannot nest batch_execute operations"));
                    failed++;

                    if (stopOnError)
                    {
                        RevertIfAtomic(atomic, undoGroup);
                        break;
                    }
                    continue;
                }

                // Get the tool
                if (!_server.TryGetTool(toolName, out McpToolBase tool))
                {
                    results.Add(CreateOperationResult(i, operationId, false, null, $"Unknown tool: {toolName}"));
                    failed++;

                    if (stopOnError)
                    {
                        RevertIfAtomic(atomic, undoGroup);
                        break;
                    }
                    continue;
                }

                // Execute the tool
                JObject toolResult = null;
                Exception toolException = null;

                if (tool.IsAsync)
                {
                    var toolTcs = new TaskCompletionSource<JObject>();

                    try
                    {
                        tool.ExecuteAsync(toolParams, toolTcs);
                    }
                    catch (Exception ex)
                    {
                        toolException = ex;
                    }

                    // Wait for async tool completion (yield must be outside try-catch)
                    if (toolException == null)
                    {
                        while (!toolTcs.Task.IsCompleted)
                        {
                            yield return null;
                        }

                        if (toolTcs.Task.IsFaulted)
                        {
                            toolException = toolTcs.Task.Exception?.InnerException ?? toolTcs.Task.Exception;
                        }
                        else
                        {
                            toolResult = toolTcs.Task.Result;
                        }
                    }
                }
                else
                {
                    try
                    {
                        toolResult = tool.Execute(toolParams);
                    }
                    catch (Exception ex)
                    {
                        toolException = ex;
                    }
                }

                // Process result
                if (toolException != null)
                {
                    results.Add(CreateOperationResult(i, operationId, false, null, toolException.Message));
                    failed++;

                    if (stopOnError)
                    {
                        RevertIfAtomic(atomic, undoGroup);
                        break;
                    }
                }
                else if (toolResult != null)
                {
                    // Check if the result indicates an error
                    bool isError = toolResult["error"] != null;
                    bool isSuccess = toolResult["success"]?.ToObject<bool?>() ?? !isError;

                    if (isSuccess && !isError)
                    {
                        results.Add(CreateOperationResult(i, operationId, true, toolResult, null));
                        succeeded++;
                    }
                    else
                    {
                        string errorMessage = toolResult["error"]?["message"]?.ToString()
                            ?? toolResult["message"]?.ToString()
                            ?? "Tool execution failed";
                        results.Add(CreateOperationResult(i, operationId, false, toolResult, errorMessage));
                        failed++;

                        if (stopOnError)
                        {
                            RevertIfAtomic(atomic, undoGroup);
                            break;
                        }
                    }
                }
                else
                {
                    results.Add(CreateOperationResult(i, operationId, false, null, "Tool returned null result"));
                    failed++;

                    if (stopOnError)
                    {
                        RevertIfAtomic(atomic, undoGroup);
                        break;
                    }
                }

                // Yield to allow Unity to process other events
                yield return null;
            }

            // Collapse undo group
            if (atomic && undoGroup >= 0 && failed == 0)
            {
                Undo.CollapseUndoOperations(undoGroup);
            }

            // Build response
            string message;
            if (failed == 0)
            {
                message = $"Successfully executed {succeeded}/{operations.Count} operations.";
            }
            else if (atomic && stopOnError)
            {
                message = $"Batch execution failed and rolled back. {succeeded} operations succeeded before failure.";
            }
            else if (stopOnError)
            {
                message = $"Batch execution stopped on error. {succeeded}/{operations.Count} operations succeeded.";
            }
            else
            {
                message = $"Batch execution completed with errors. {succeeded}/{operations.Count} operations succeeded, {failed} failed.";
            }

            tcs.SetResult(new JObject
            {
                ["success"] = failed == 0,
                ["type"] = "text",
                ["message"] = message,
                ["results"] = results,
                ["summary"] = new JObject
                {
                    ["total"] = operations.Count,
                    ["succeeded"] = succeeded,
                    ["failed"] = failed,
                    ["executed"] = succeeded + failed
                }
            });
        }

        private void RevertIfAtomic(bool atomic, int undoGroup)
        {
            if (atomic && undoGroup >= 0)
            {
                Undo.RevertAllDownToGroup(undoGroup);
            }
        }

        private JObject CreateOperationResult(int index, string id, bool success, JObject result, string error)
        {
            var operationResult = new JObject
            {
                ["index"] = index,
                ["id"] = id ?? index.ToString(),
                ["success"] = success
            };

            if (success && result != null)
            {
                operationResult["result"] = result;
            }
            else if (!success)
            {
                operationResult["error"] = error ?? "Unknown error";
            }

            return operationResult;
        }
    }
}
