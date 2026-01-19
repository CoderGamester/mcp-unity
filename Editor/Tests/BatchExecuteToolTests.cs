using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using McpUnity.Tools;
using McpUnity.Unity;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using Newtonsoft.Json.Linq;

namespace McpUnity.Tests
{
    /// <summary>
    /// Tests for BatchExecuteTool functionality
    /// </summary>
    public class BatchExecuteToolTests
    {
        private BatchExecuteTool _batchTool;
        private GameObject _testObject;

        [SetUp]
        public void SetUp()
        {
            // Get the server instance to access registered tools
            _batchTool = new BatchExecuteTool(McpUnityServer.Instance);
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up any test objects
            if (_testObject != null)
            {
                Object.DestroyImmediate(_testObject);
                _testObject = null;
            }
        }

        #region Basic Properties Tests

        [Test]
        public void BatchExecuteTool_HasCorrectName()
        {
            Assert.AreEqual("batch_execute", _batchTool.Name);
        }

        [Test]
        public void BatchExecuteTool_IsAsync()
        {
            Assert.IsTrue(_batchTool.IsAsync, "BatchExecuteTool should be async");
        }

        [Test]
        public void BatchExecuteTool_HasDescription()
        {
            Assert.IsNotNull(_batchTool.Description);
            Assert.IsTrue(_batchTool.Description.Contains("batch"), "Description should mention batch");
        }

        #endregion

        #region Validation Tests

        [UnityTest]
        public IEnumerator BatchExecuteTool_WithEmptyOperations_ReturnsError()
        {
            // Arrange
            JObject parameters = new JObject
            {
                ["operations"] = new JArray()
            };

            var tcs = new TaskCompletionSource<JObject>();

            // Act
            _batchTool.ExecuteAsync(parameters, tcs);

            while (!tcs.Task.IsCompleted)
            {
                yield return null;
            }

            JObject result = tcs.Task.Result;

            // Assert
            Assert.IsNotNull(result["error"], "Should return error for empty operations");
            Assert.IsTrue(result["error"]["message"].ToString().Contains("operations"),
                "Error should mention operations");
        }

        [UnityTest]
        public IEnumerator BatchExecuteTool_WithNullOperations_ReturnsError()
        {
            // Arrange
            JObject parameters = new JObject();

            var tcs = new TaskCompletionSource<JObject>();

            // Act
            _batchTool.ExecuteAsync(parameters, tcs);

            while (!tcs.Task.IsCompleted)
            {
                yield return null;
            }

            JObject result = tcs.Task.Result;

            // Assert
            Assert.IsNotNull(result["error"], "Should return error for null operations");
        }

        [UnityTest]
        public IEnumerator BatchExecuteTool_WithTooManyOperations_ReturnsError()
        {
            // Arrange
            JArray operations = new JArray();
            for (int i = 0; i < 101; i++)
            {
                operations.Add(new JObject
                {
                    ["tool"] = "get_scene_info",
                    ["params"] = new JObject()
                });
            }

            JObject parameters = new JObject
            {
                ["operations"] = operations
            };

            var tcs = new TaskCompletionSource<JObject>();

            // Act
            _batchTool.ExecuteAsync(parameters, tcs);

            while (!tcs.Task.IsCompleted)
            {
                yield return null;
            }

            JObject result = tcs.Task.Result;

            // Assert
            Assert.IsNotNull(result["error"], "Should return error for too many operations");
            Assert.IsTrue(result["error"]["message"].ToString().Contains("100"),
                "Error should mention limit");
        }

        [UnityTest]
        public IEnumerator BatchExecuteTool_WithNestedBatchExecute_ReturnsError()
        {
            // Arrange
            JObject parameters = new JObject
            {
                ["operations"] = new JArray
                {
                    new JObject
                    {
                        ["tool"] = "batch_execute",
                        ["params"] = new JObject
                        {
                            ["operations"] = new JArray()
                        }
                    }
                }
            };

            var tcs = new TaskCompletionSource<JObject>();

            // Act
            _batchTool.ExecuteAsync(parameters, tcs);

            while (!tcs.Task.IsCompleted)
            {
                yield return null;
            }

            JObject result = tcs.Task.Result;

            // Assert - Should fail because of nested batch_execute
            Assert.IsFalse(result["success"]?.ToObject<bool>() ?? true, "Should fail for nested batch");
        }

        [UnityTest]
        public IEnumerator BatchExecuteTool_WithUnknownTool_ReturnsError()
        {
            // Arrange
            JObject parameters = new JObject
            {
                ["operations"] = new JArray
                {
                    new JObject
                    {
                        ["tool"] = "nonexistent_tool_12345",
                        ["params"] = new JObject()
                    }
                },
                ["stopOnError"] = true
            };

            var tcs = new TaskCompletionSource<JObject>();

            // Act
            _batchTool.ExecuteAsync(parameters, tcs);

            while (!tcs.Task.IsCompleted)
            {
                yield return null;
            }

            JObject result = tcs.Task.Result;

            // Assert
            Assert.IsFalse(result["success"]?.ToObject<bool>() ?? true, "Should fail for unknown tool");
            Assert.IsNotNull(result["results"], "Should have results array");
            JArray results = result["results"] as JArray;
            Assert.AreEqual(1, results.Count);
            Assert.IsFalse(results[0]["success"]?.ToObject<bool>() ?? true);
            Assert.IsTrue(results[0]["error"]?.ToString().Contains("Unknown tool"));
        }

        #endregion

        #region Successful Execution Tests

        [UnityTest]
        public IEnumerator BatchExecuteTool_WithSingleOperation_Succeeds()
        {
            // Arrange - get_scene_info requires no parameters and always succeeds
            JObject parameters = new JObject
            {
                ["operations"] = new JArray
                {
                    new JObject
                    {
                        ["tool"] = "get_scene_info",
                        ["params"] = new JObject(),
                        ["id"] = "op1"
                    }
                }
            };

            var tcs = new TaskCompletionSource<JObject>();

            // Act
            _batchTool.ExecuteAsync(parameters, tcs);

            while (!tcs.Task.IsCompleted)
            {
                yield return null;
            }

            JObject result = tcs.Task.Result;

            // Assert
            Assert.IsTrue(result["success"]?.ToObject<bool>() ?? false, "Should succeed");
            Assert.IsNotNull(result["results"], "Should have results array");
            Assert.IsNotNull(result["summary"], "Should have summary");

            JObject summary = result["summary"] as JObject;
            Assert.AreEqual(1, summary["total"]?.ToObject<int>());
            Assert.AreEqual(1, summary["succeeded"]?.ToObject<int>());
            Assert.AreEqual(0, summary["failed"]?.ToObject<int>());
        }

        [UnityTest]
        public IEnumerator BatchExecuteTool_WithMultipleOperations_Succeeds()
        {
            // Arrange - Use operations that don't require external dependencies
            JObject parameters = new JObject
            {
                ["operations"] = new JArray
                {
                    new JObject
                    {
                        ["tool"] = "get_scene_info",
                        ["params"] = new JObject(),
                        ["id"] = "op1"
                    },
                    new JObject
                    {
                        ["tool"] = "get_scene_info",
                        ["params"] = new JObject(),
                        ["id"] = "op2"
                    }
                }
            };

            var tcs = new TaskCompletionSource<JObject>();

            // Act
            _batchTool.ExecuteAsync(parameters, tcs);

            while (!tcs.Task.IsCompleted)
            {
                yield return null;
            }

            JObject result = tcs.Task.Result;

            // Assert
            Assert.IsTrue(result["success"]?.ToObject<bool>() ?? false, "Should succeed");

            JObject summary = result["summary"] as JObject;
            Assert.AreEqual(2, summary["total"]?.ToObject<int>());
            Assert.AreEqual(2, summary["succeeded"]?.ToObject<int>());
            Assert.AreEqual(0, summary["failed"]?.ToObject<int>());

            JArray results = result["results"] as JArray;
            Assert.AreEqual(2, results.Count);
            Assert.AreEqual("op1", results[0]["id"]?.ToString());
            Assert.AreEqual("op2", results[1]["id"]?.ToString());
        }

        #endregion

        #region StopOnError Tests

        [UnityTest]
        public IEnumerator BatchExecuteTool_StopOnErrorTrue_StopsAtFirstError()
        {
            // Arrange - First operation fails, second should not execute
            JObject parameters = new JObject
            {
                ["operations"] = new JArray
                {
                    new JObject
                    {
                        ["tool"] = "nonexistent_tool",
                        ["params"] = new JObject(),
                        ["id"] = "fail1"
                    },
                    new JObject
                    {
                        ["tool"] = "get_scene_info",
                        ["params"] = new JObject(),
                        ["id"] = "should_not_run"
                    }
                },
                ["stopOnError"] = true
            };

            var tcs = new TaskCompletionSource<JObject>();

            // Act
            _batchTool.ExecuteAsync(parameters, tcs);

            while (!tcs.Task.IsCompleted)
            {
                yield return null;
            }

            JObject result = tcs.Task.Result;

            // Assert
            Assert.IsFalse(result["success"]?.ToObject<bool>() ?? true, "Should fail");

            JObject summary = result["summary"] as JObject;
            Assert.AreEqual(2, summary["total"]?.ToObject<int>());
            Assert.AreEqual(0, summary["succeeded"]?.ToObject<int>());
            Assert.AreEqual(1, summary["failed"]?.ToObject<int>());
            Assert.AreEqual(1, summary["executed"]?.ToObject<int>(), "Should only execute 1 operation");
        }

        [UnityTest]
        public IEnumerator BatchExecuteTool_StopOnErrorFalse_ContinuesAfterError()
        {
            // Arrange - First operation fails, but should continue
            JObject parameters = new JObject
            {
                ["operations"] = new JArray
                {
                    new JObject
                    {
                        ["tool"] = "nonexistent_tool",
                        ["params"] = new JObject(),
                        ["id"] = "fail1"
                    },
                    new JObject
                    {
                        ["tool"] = "get_scene_info",
                        ["params"] = new JObject(),
                        ["id"] = "should_run"
                    }
                },
                ["stopOnError"] = false
            };

            var tcs = new TaskCompletionSource<JObject>();

            // Act
            _batchTool.ExecuteAsync(parameters, tcs);

            while (!tcs.Task.IsCompleted)
            {
                yield return null;
            }

            JObject result = tcs.Task.Result;

            // Assert - Should have partial success
            Assert.IsFalse(result["success"]?.ToObject<bool>() ?? true, "Overall should fail");

            JObject summary = result["summary"] as JObject;
            Assert.AreEqual(2, summary["total"]?.ToObject<int>());
            Assert.AreEqual(1, summary["succeeded"]?.ToObject<int>(), "Second operation should succeed");
            Assert.AreEqual(1, summary["failed"]?.ToObject<int>());
            Assert.AreEqual(2, summary["executed"]?.ToObject<int>(), "Should execute both operations");
        }

        #endregion

        #region Response Format Tests

        [UnityTest]
        public IEnumerator BatchExecuteTool_ResponseContainsRequiredFields()
        {
            // Arrange
            JObject parameters = new JObject
            {
                ["operations"] = new JArray
                {
                    new JObject
                    {
                        ["tool"] = "get_scene_info",
                        ["params"] = new JObject()
                    }
                }
            };

            var tcs = new TaskCompletionSource<JObject>();

            // Act
            _batchTool.ExecuteAsync(parameters, tcs);

            while (!tcs.Task.IsCompleted)
            {
                yield return null;
            }

            JObject result = tcs.Task.Result;

            // Assert - Check all required fields are present
            Assert.IsNotNull(result["success"], "Response should have 'success' field");
            Assert.IsNotNull(result["type"], "Response should have 'type' field");
            Assert.IsNotNull(result["message"], "Response should have 'message' field");
            Assert.IsNotNull(result["results"], "Response should have 'results' field");
            Assert.IsNotNull(result["summary"], "Response should have 'summary' field");

            JObject summary = result["summary"] as JObject;
            Assert.IsNotNull(summary["total"], "Summary should have 'total' field");
            Assert.IsNotNull(summary["succeeded"], "Summary should have 'succeeded' field");
            Assert.IsNotNull(summary["failed"], "Summary should have 'failed' field");
            Assert.IsNotNull(summary["executed"], "Summary should have 'executed' field");
        }

        [UnityTest]
        public IEnumerator BatchExecuteTool_OperationResultsHaveCorrectFormat()
        {
            // Arrange
            JObject parameters = new JObject
            {
                ["operations"] = new JArray
                {
                    new JObject
                    {
                        ["tool"] = "get_scene_info",
                        ["params"] = new JObject(),
                        ["id"] = "custom_id"
                    }
                }
            };

            var tcs = new TaskCompletionSource<JObject>();

            // Act
            _batchTool.ExecuteAsync(parameters, tcs);

            while (!tcs.Task.IsCompleted)
            {
                yield return null;
            }

            JObject result = tcs.Task.Result;

            // Assert
            JArray results = result["results"] as JArray;
            Assert.AreEqual(1, results.Count);

            JObject opResult = results[0] as JObject;
            Assert.IsNotNull(opResult["index"], "Operation result should have 'index' field");
            Assert.IsNotNull(opResult["id"], "Operation result should have 'id' field");
            Assert.IsNotNull(opResult["success"], "Operation result should have 'success' field");
            Assert.AreEqual("custom_id", opResult["id"]?.ToString());
            Assert.AreEqual(0, opResult["index"]?.ToObject<int>());
        }

        #endregion
    }
}
