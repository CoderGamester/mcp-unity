using NUnit.Framework;
using UnityEngine;

namespace McpUnity.Tests
{
    public class BasicTests
    {
        [Test]
        public void BasicTest()
        {
            Debug.Log("[MCP Unity Test] Running basic test");
            Assert.That(1 + 1, Is.EqualTo(2), "Basic math should work");
        }
    }
} 