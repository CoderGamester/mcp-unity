using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using McpUnity.Unity;
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
        /// <summary>
        /// Supported test modes for Unity Test Runner
        /// </summary>
        private enum TestMode
        {
            EditMode,
            PlayMode
        }

        private TaskCompletionSource<JObject> _testCompletionSource;
        private int _testCount;
        private int _passCount;
        private int _failCount;
        private List<JObject> _testResults;

        public RunTestsTool()
        {
            Name = "run_tests";
            Description = "Runs Unity's Test Runner tests";
            IsAsync = true;
        }

        public override JObject Execute(JObject parameters)
        {
            throw new NotSupportedException("This tool only supports async execution. Please use ExecuteAsync instead.");
        }

        public override async Task<JObject> ExecuteAsync(JObject parameters)
        {
            // Extract parameters
            string testMode = parameters["testMode"]?.ToObject<string>() ?? "EditMode";
            string testFilter = parameters["testFilter"]?.ToObject<string>();

            // Validate test mode
            if (!Enum.TryParse<TestMode>(testMode, true, out var mode))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Invalid test mode '{testMode}'. Valid modes are: EditMode, PlayMode",
                    "validation_error"
                );
            }

            // Initialize test tracking
            _testCount = 0;
            _passCount = 0;
            _failCount = 0;
            _testResults = new List<JObject>();
            _testCompletionSource = new TaskCompletionSource<JObject>();

            try
            {
                var testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
                testRunnerApi.RegisterCallbacks(this);

                var filter = new Filter
                {
                    testMode = mode == TestMode.EditMode ? TestMode.EditMode : TestMode.PlayMode
                };

                if (!string.IsNullOrEmpty(testFilter))
                {
                    filter.testNames = new[] { testFilter };
                }

                testRunnerApi.Execute(new ExecutionSettings(filter));

                // Wait for test completion or timeout after 5 minutes
                var timeoutTask = Task.Delay(TimeSpan.FromMinutes(5));
                var completedTask = await Task.WhenAny(_testCompletionSource.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        "Failed to run tests: Request timed out",
                        "timeout_error"
                    );
                }

                return await _testCompletionSource.Task;
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Failed to run tests: {ex.Message}",
                    "execution_error"
                );
            }
        }

        public void RunStarted(ITestAdapterRef testsToRun)
        {
            _testCount = testsToRun.TestCaseCount;
            
            if (_testCount == 0)
            {
                _testCompletionSource.TrySetResult(new JObject
                {
                    ["success"] = false,
                    ["message"] = "No tests found matching the specified criteria",
                    ["testCount"] = 0,
                    ["passCount"] = 0,
                    ["failCount"] = 0,
                    ["results"] = new JArray()
                });
            }
        }

        public void RunFinished(ITestResultAdapterRef testResults)
        {
            var response = new JObject
            {
                ["success"] = _failCount == 0,
                ["message"] = $"Tests completed: {_passCount} passed, {_failCount} failed",
                ["testCount"] = _testCount,
                ["passCount"] = _passCount,
                ["failCount"] = _failCount,
                ["results"] = JArray.FromObject(_testResults)
            };

            _testCompletionSource.TrySetResult(response);
        }

        public void TestStarted(ITestAdapterRef test)
        {
            // Optional: Add test started tracking if needed
        }

        public void TestFinished(ITestResultAdapterRef result)
        {
            if (result.TestStatus == TestStatus.Passed)
            {
                _passCount++;
            }
            else if (result.TestStatus == TestStatus.Failed)
            {
                _failCount++;
            }

            _testResults.Add(new JObject
            {
                ["name"] = result.Name,
                ["status"] = result.TestStatus.ToString(),
                ["message"] = result.Message,
                ["duration"] = result.Duration
            });
        }
    }
}
