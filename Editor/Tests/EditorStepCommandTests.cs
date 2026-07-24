using System;
using McpUnity.Extensions.Commands;
using NUnit.Framework;
using UnityEditor;

namespace McpUnity.Extensions.Tests
{
    public class EditorStepCommandTests
    {
        [Test]
        public void Step_RejectsOutsidePlayMode()
        {
            Assert.That(EditorApplication.isPlaying, Is.False);
            Assert.Throws<InvalidOperationException>(() => EditorStepCommand.Step());
        }
    }
}
