using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using McpUnity.Unity;
using McpUnity.Services;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for running Unity Test Runner tests
    /// </summary>
    public class RunTestsTool : McpToolBase, ICallbacks
    {
        private readonly ITestRunnerService _testRunnerService;
        private TaskCompletionSource<JObject> _testCompletionSource;
        private List<JObject> _testResults;
        private int _testCount;
        private int _passCount;
        private int _failCount;

        public RunTestsTool(ITestRunnerService testRunnerService)
        {
            Name = "run_tests";
            Description = "Runs Unity's Test Runner tests";
            IsAsync = true;
            
            _testRunnerService = testRunnerService;
            _testRunnerService.TestRunnerApi.RegisterCallbacks(this);
            _testResults = new List<JObject>();
        }

        public override JObject Execute(JObject parameters)
        {
            throw new NotSupportedException("This tool only supports async execution. Please use ExecuteAsync instead.");
        }

        public override void ExecuteAsync(JObject parameters, TaskCompletionSource<JObject> tcs)
        {
            _testCompletionSource = tcs;

            // Reset counters and ensure results list exists
            _testResults?.Clear();
            _testResults = _testResults ?? new List<JObject>();
            _testCount = 0;
            _passCount = 0;
            _failCount = 0;

            // Extract parameters
            string testMode = parameters["testMode"]?.ToObject<string>()?.ToLower() ?? "editmode";
            string testFilter = parameters["testFilter"]?.ToObject<string>();

            // Validate test mode
            if (!Enum.TryParse<TestMode>(testMode, true, out var mode))
            {
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                    $"Invalid test mode '{testMode}'. Valid modes are: EditMode, PlayMode",
                    "validation_error"
                ));
                return;
            }

            try
            {
                _testRunnerService.ExecuteTests(mode, testFilter, tcs);
            }
            catch (Exception ex)
            {
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                    $"Failed to run tests: {ex.Message}",
                    "execution_error"
                ));
            }
        }

        public void RunStarted(ITestAdaptor testsToRun)
        {
            if (testsToRun == null) return;
            
            _testCount = testsToRun.TestCaseCount;
            Debug.Log($"[MCP Unity] Starting test run with {_testCount} tests...");
        }

        public void RunFinished(ITestResultAdaptor testResults)
        {
            try
            {
                Debug.Log($"[MCP Unity] Tests completed: {_passCount} passed, {_failCount} failed");

                var resultsArray = _testResults != null ? 
                    JArray.FromObject(_testResults) : 
                    new JArray();

                var response = new JObject
                {
                    ["success"] = _failCount == 0,
                    ["message"] = $"Tests completed: {_passCount} passed, {_failCount} failed",
                    ["testCount"] = _testCount,
                    ["passCount"] = _passCount,
                    ["failCount"] = _failCount,
                    ["results"] = resultsArray
                };

                _testCompletionSource?.TrySetResult(response);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP Unity] Error in RunFinished: {ex.Message}");
                if (_testCompletionSource != null && !_testCompletionSource.Task.IsCompleted)
                {
                    _testCompletionSource.TrySetResult(McpUnitySocketHandler.CreateErrorResponse(
                        $"Error processing test results: {ex.Message}",
                        "result_error"
                    ));
                }
            }
        }

        public void TestStarted(ITestAdaptor test)
        {
            if (test?.Name == null) return;
            Debug.Log($"[MCP Unity] Starting test: {test.Name}");
        }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result?.Test == null) return;

            try
            {
                string status = result.TestStatus.ToString();
                Debug.Log($"[MCP Unity] Test finished: {result.Test.Name} - {status}");

                if (result.TestStatus == TestStatus.Passed)
                {
                    _passCount++;
                }
                else if (result.TestStatus == TestStatus.Failed)
                {
                    _failCount++;
                }

                if (_testResults != null)
                {
                    _testResults.Add(new JObject
                    {
                        ["name"] = result.Test?.Name ?? "Unknown Test",
                        ["status"] = status,
                        ["message"] = result.Message ?? string.Empty,
                        ["duration"] = result.Duration
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP Unity] Error in TestFinished: {ex.Message}");
            }
        }
    }
}
