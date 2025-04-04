using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using McpUnity.Unity;
using UnityEngine;
using UnityEditor.TestTools.TestRunner.Api;
using Newtonsoft.Json.Linq;

namespace McpUnity.Services
{
    /// <summary>
    /// Service for accessing Unity Test Runner functionality
    /// </summary>
    public class TestRunnerService : ITestRunnerService
    {
        private readonly TestRunnerApi _testRunnerApi;
        
        /// <summary>
        /// Get the TestRunnerApi instance
        /// </summary>
        public TestRunnerApi TestRunnerApi => _testRunnerApi;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public TestRunnerService()
        {
            _testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
        }
        
        /// <summary>
        /// Get a list of all available tests
        /// </summary>
        public List<TestItemInfo> GetAllTests()
        {
            var tests = new List<TestItemInfo>();
            
            // Get tests for both edit mode and play mode
            _testRunnerApi.RetrieveTestList(TestMode.EditMode, adaptor => CollectTestItems(adaptor, tests));
            _testRunnerApi.RetrieveTestList(TestMode.PlayMode, adaptor => CollectTestItems(adaptor, tests));
            
            return tests;
        }

        /// <summary>
        /// Execute tests with the provided parameters
        /// </summary>
        /// <param name="testMode">Test mode to run</param>
        /// <param name="testFilter">Optional test filter</param>
        /// <param name="completionSource">TaskCompletionSource to resolve when tests are complete</param>
        /// <param name="timeoutMinutes">Timeout in minutes, defaults to 10</param>
        /// <returns>Task that resolves with test results when tests are complete</returns>
        public async void ExecuteTests(
            TestMode testMode, 
            string testFilter, 
            TaskCompletionSource<JObject> completionSource, 
            int timeoutMinutes = 10)
        {
            // Create filter
            var filter = new Filter
            {
                testMode = testMode
            };
                
            // Apply name filter if provided
            if (!string.IsNullOrEmpty(testFilter))
            {
                filter.testNames = new[] { testFilter };
            }
                
            // Execute tests
            _testRunnerApi.Execute(new ExecutionSettings(filter));

            Task completedTask = await Task.WhenAny(
                completionSource.Task,
                Task.Delay(TimeSpan.FromMinutes(timeoutMinutes))
            );

            if (completedTask != completionSource.Task)
            {
                completionSource.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                    $"Test run timed out after {timeoutMinutes} minutes",
                    "test_runner_timeout"
                ));
            }
        }
        
        /// <summary>
        /// Recursively collect test items from test adaptors
        /// </summary>
        private void CollectTestItems(ITestAdaptor testAdaptor, List<TestItemInfo> tests, string parentPath = "")
        {
            if (testAdaptor.IsSuite)
            {
                // For suites (namespaces, classes), collect all children
                foreach (var child in testAdaptor.Children)
                {
                    string currentPath = string.IsNullOrEmpty(parentPath) ? testAdaptor.Name : $"{parentPath}.{testAdaptor.Name}";
                    CollectTestItems(child, tests, currentPath);
                }
            }
            else
            {
                // For individual tests, add to the list
                string fullPath = string.IsNullOrEmpty(parentPath) ? testAdaptor.Name : $"{parentPath}.{testAdaptor.Name}";
                
                tests.Add(new TestItemInfo
                {
                    Name = testAdaptor.Name,
                    FullName = testAdaptor.FullName,
                    Path = fullPath,
                    TestMode = testAdaptor.TestMode.ToString(),
                    RunState = testAdaptor.RunState.ToString()
                });
            }
        }
    }
    
    /// <summary>
    /// Information about a test item
    /// </summary>
    public class TestItemInfo
    {
        public string Name { get; set; }
        public string FullName { get; set; }
        public string Path { get; set; }
        public string TestMode { get; set; }
        public string RunState { get; set; }
    }
}
