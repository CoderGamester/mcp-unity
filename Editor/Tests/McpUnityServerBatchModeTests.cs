using System.Reflection;
using McpUnity.Unity;
using NUnit.Framework;

namespace McpUnity.Tests
{
    public class McpUnityServerBatchModeTests
    {
        [TestCase(false, false, null, true)]
        [TestCase(false, true, null, true)]
        [TestCase(true, false, null, false)]
        [TestCase(true, false, "false", false)]
        [TestCase(true, false, "true", true)]
        [TestCase(true, false, "TRUE", true)]
        [TestCase(true, false, "1", true)]
        [TestCase(true, true, null, true)]
        public void BatchModeServerRequiresAnExplicitOptIn(bool isBatchMode, bool allowBatchModeServer, string batchModeOverride, bool expected)
        {
            MethodInfo method = typeof(McpUnityServer).GetMethod(
                "ShouldRunInCurrentProcess",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(bool), typeof(bool), typeof(string) },
                null);

            Assert.NotNull(method, "Batch-mode policy must remain centralized in a testable helper.");

            bool actual = (bool)method.Invoke(null, new object[] { isBatchMode, allowBatchModeServer, batchModeOverride });

            Assert.AreEqual(expected, actual);
        }
    }
}
