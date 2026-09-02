using System;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Threading.Tasks;
using McpUnity.Tools;
using McpUnity.Unity;
using McpUnity.Utils;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace McpUnity.Tests
{
    public class McpUnitySecurityTests
    {
        private string _temporaryDirectory;
        private Type _authenticationType;

        [SetUp]
        public void SetUp()
        {
            _temporaryDirectory = Path.Combine(Path.GetTempPath(), "mcp-unity-security-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporaryDirectory);
            _authenticationType = typeof(McpUnityServer).Assembly.GetType(
                "McpUnity.Unity.McpUnityAuthentication",
                true);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_temporaryDirectory))
            {
                Directory.Delete(_temporaryDirectory, true);
            }
        }

        [Test]
        public void OriginValidatorAcceptsOnlyMissingOrigin()
        {
            var handler = new McpUnitySocketHandler(null, 0);
            PropertyInfo property = typeof(McpUnitySocketHandler).GetProperty(
                "OriginValidator",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(property);

            var validator = (Delegate)property.GetValue(handler);
            Assert.IsTrue((bool)validator.DynamicInvoke(new object[] { null }));

            foreach (string origin in new[]
            {
                string.Empty,
                " ",
                "null",
                "https://attacker.example",
                "http://localhost:8090",
                "chrome-extension://abcdefghijklmnop"
            })
            {
                Assert.IsFalse((bool)validator.DynamicInvoke(origin), $"Origin should be rejected: '{origin}'");
            }
        }

        [Test]
        public void AuthenticationTokenIsCreatedPersistedAndRotated()
        {
            string tokenPath = Path.Combine(_temporaryDirectory, "nested directory", "bridge-token");
            string firstToken = InvokeAuthentication<string>("GetOrCreateTokenAtPath", tokenPath);
            string persistedToken = InvokeAuthentication<string>("GetOrCreateTokenAtPath", tokenPath);
            string rotatedToken = InvokeAuthentication<string>("RegenerateTokenAtPath", tokenPath);

            StringAssert.IsMatch("^[0-9a-f]{64}$", firstToken);
            Assert.AreEqual(firstToken, persistedToken);
            Assert.AreNotEqual(firstToken, rotatedToken);
            Assert.AreEqual(rotatedToken, File.ReadAllText(tokenPath));
        }

        [Test]
        public void AuthenticationTokenFailsClosedWhenExistingFileIsMalformed()
        {
            string tokenPath = Path.Combine(_temporaryDirectory, "bridge-token");
            File.WriteAllText(tokenPath, "malformed");

            var exception = Assert.Throws<TargetInvocationException>(() =>
                InvokeAuthentication<string>("GetOrCreateTokenAtPath", tokenPath));
            Assert.IsInstanceOf<InvalidDataException>(exception.InnerException);
        }

        [Test]
        public void WebSocketServerUsesBasicAuthenticationAndExpectedCredentials()
        {
            const string token = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            Type webSocketServerType = Type.GetType(
                "WebSocketSharp.Server.WebSocketServer, websocket-sharp",
                true);
            object webSocketServer = Activator.CreateInstance(webSocketServerType, "ws://localhost:18090");

            MethodInfo configure = typeof(McpUnityServer).GetMethod(
                "ConfigureWebSocketSecurity",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(configure);
            configure.Invoke(null, new[] { webSocketServer, token });

            object schemes = webSocketServerType.GetProperty("AuthenticationSchemes").GetValue(webSocketServer);
            Assert.AreEqual("Basic", schemes.ToString());
            Assert.AreEqual("MCP Unity", webSocketServerType.GetProperty("Realm").GetValue(webSocketServer));

            var credentialFinder = (Delegate)webSocketServerType.GetProperty("UserCredentialsFinder").GetValue(webSocketServer);
            object credentials = credentialFinder.DynamicInvoke(new GenericIdentity("mcp-unity"));
            Assert.NotNull(credentials);
            Assert.AreEqual("mcp-unity", credentials.GetType().GetProperty("UserName").GetValue(credentials));
            Assert.AreEqual(token, credentials.GetType().GetProperty("Password").GetValue(credentials));
            Assert.IsNull(credentialFinder.DynamicInvoke(new GenericIdentity("other-client")));
        }

        [Test]
        public void AddPackageIsDeniedBeforeParameterValidationUnlessEnabled()
        {
            McpUnitySettings settings = McpUnitySettings.Instance;
            bool originalValue = settings.AllowPackageInstallation;

            try
            {
                var tool = new AddPackageTool();
                settings.AllowPackageInstallation = false;
                var deniedCompletion = new TaskCompletionSource<JObject>();
                tool.ExecuteAsync(new JObject(), deniedCompletion);
                Assert.AreEqual(
                    "package_installation_disabled",
                    deniedCompletion.Task.Result["error"]?["type"]?.ToString());

                settings.AllowPackageInstallation = true;
                var validationCompletion = new TaskCompletionSource<JObject>();
                tool.ExecuteAsync(new JObject(), validationCompletion);
                Assert.AreEqual(
                    UnityEngine.Application.isBatchMode ? "package_installation_disabled" : "validation_error",
                    validationCompletion.Task.Result["error"]?["type"]?.ToString());

                MethodInfo policy = typeof(AddPackageTool).GetMethod(
                    "IsPackageInstallationAllowed",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.NotNull(policy);
                Assert.IsTrue((bool)policy.Invoke(null, new object[] { false, true }));
                Assert.IsFalse((bool)policy.Invoke(null, new object[] { false, false }));
                Assert.IsFalse((bool)policy.Invoke(null, new object[] { true, true }));
            }
            finally
            {
                settings.AllowPackageInstallation = originalValue;
            }
        }

        [Test]
        public void GeneratedClientConfigurationsReferenceTheTokenFile()
        {
            StringAssert.Contains("MCP_UNITY_AUTH_TOKEN_PATH", McpUtils.GenerateMcpConfigJson(false));
            StringAssert.Contains("MCP_UNITY_AUTH_TOKEN_PATH", McpUtils.GenerateOpenCodeConfigJson(false));
            StringAssert.Contains("MCP_UNITY_AUTH_TOKEN_PATH", McpUtils.GenerateMcpConfigToml());
        }

        private T InvokeAuthentication<T>(string methodName, params object[] arguments)
        {
            MethodInfo method = _authenticationType.GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method, $"Authentication method not found: {methodName}");
            return (T)method.Invoke(null, arguments);
        }
    }
}
