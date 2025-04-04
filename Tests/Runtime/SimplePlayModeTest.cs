using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace McpUnity.Tests
{
    public class SimplePlayModeTest
    {
        [Test]
        public void SimplePlayModeTestPasses()
        {
            Debug.Log("Running Simple PlayMode Test");
            Assert.Pass("This play mode test should pass");
        }

        [UnityTest]
        public IEnumerator SimplePlayModeUnityTestWithEnumeratorPasses()
        {
            Debug.Log("Running Simple PlayMode Unity Test");
            yield return null;
            Assert.Pass("This play mode unity test should pass");
        }
    }
}