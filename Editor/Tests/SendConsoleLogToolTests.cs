using System.Globalization;
using McpUnity.Tools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace McpUnity.Tests
{
    public class SendConsoleLogToolTests
    {
        private CultureInfo _originalCulture;

        [SetUp]
        public void SetUp()
        {
            _originalCulture = CultureInfo.CurrentCulture;
        }

        [TearDown]
        public void TearDown()
        {
            CultureInfo.CurrentCulture = _originalCulture;
        }

        [Test]
        public void Execute_WithUppercaseWarningUnderTurkishCulture_LogsWarning()
        {
            // In tr-TR, "WARNING".ToLower() is "warnıng" (dotless i), which used to fall through to Debug.Log
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            LogAssert.Expect(LogType.Warning, "[MCP]: culture check");

            JObject result = new SendConsoleLogTool().Execute(new JObject
            {
                ["message"] = "culture check",
                ["type"] = "WARNING"
            });

            Assert.IsTrue(result["success"]?.Value<bool>() ?? false, result.ToString());
        }
    }
}
