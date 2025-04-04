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
        private int _testCount;
        private int _passCount;
        private int _failCount;
        private List<JObject> _testResults;

        public RunTestsTool(ITestRunnerService testRunnerService)
        {
            Name = "run_tests";
            Description = "Runs Unity's Test Runner tests";
            IsAsync = true;
            
            _testRunnerService = testRunnerService;
            _testRunnerService.TestRunnerApi.RegisterCallbacks(this);
        }

        public override JObject Execute(JObject parameters)
        {
            throw new NotSupportedException("This tool only supports async execution. Please use ExecuteAsync instead.");
        }

        public override void ExecuteAsync(JObject parameters, TaskCompletionSource<JObject> tcs)
        {
            // Extract parameters
            string testMode = parameters["testMode"]?.ToObject<string>() ?? "EditMode";

            // Validate test mode
            if (!Enum.TryParse<TestMode>(testMode, true, out var mode))
            {
                tcs.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                    $"Invalid test mode '{testMode}'. Valid modes are: EditMode, PlayMode",
                    "validation_error"
                ));
                return;
            }

            // Initialize test tracking
            _testCount = 0;
            _passCount = 0;
            _failCount = 0;
            _testResults = new List<JObject>();
            _testCompletionSource = tcs;

            try
            {
                string testFilter = parameters["testFilter"]?.ToObject<string>();
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

        public void RunFinished(ITestResultAdaptor testResults)
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

        public void TestStarted(ITestAdaptor test)
        {
            // Optional: Add test started tracking if needed
        }

        public void TestFinished(ITestResultAdaptor result)
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
                ["name"] = result.Test.Name,
                ["status"] = result.TestStatus.ToString(),
                ["message"] = result.Message,
                ["duration"] = result.Duration
            });
        }
    }
}
