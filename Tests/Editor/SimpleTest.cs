using NUnit.Framework;
using UnityEngine;

namespace McpUnity.Tests
{
    public class SimpleTest
    {
        [Test]
        public void SimplePassingTest()
        {
            Debug.Log("Running SimplePassingTest");
            Assert.Pass("This test should pass");
        }
    }
} 