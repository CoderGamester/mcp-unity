using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace McpUnity.Tests
{
    public class TestRunner
    {
        [Test]
        public void SimplePassTest()
        {
            Debug.Log("Running a very simple pass test");
            Assert.Pass("This test should always pass");
        }

        [Test]
        public void SimpleAssertionTest()
        {
            Debug.Log("Running simple assertion test");
            Assert.That(1 + 1, Is.EqualTo(2), "Basic math should work");
        }
    }
}