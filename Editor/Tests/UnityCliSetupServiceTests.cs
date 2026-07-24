using System.Reflection;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using McpUnity.Extensions.Commands;
using McpUnity.Extensions.Setup;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;

namespace McpUnity.Extensions.Tests
{
    public class UnityCliSetupServiceTests
    {
        [Test]
        public void CliPathResolver_PrefersTheLiveWindowPath()
        {
            var assembly = typeof(AssignMaterialCommand).Assembly;
            var resolverType = assembly.GetType("McpUnity.Extensions.Setup.UnityCliPathResolver");

            Assert.That(resolverType, Is.Not.Null);
            var resolve = resolverType.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static);
            var result = resolve.Invoke(null, new object[] { "/window/unity", "/environment/unity" });
            var executablePath = (string)result.GetType().GetProperty("ExecutablePath").GetValue(result);

            Assert.That(executablePath, Is.EqualTo("/window/unity"));
        }

        [Test]
        public void CliPathResolver_UsesEnvironmentThenPathWhenTheLiveWindowPathIsEmpty()
        {
            var environment = UnityCliPathResolver.Resolve(" ", "/environment/unity");
            var path = UnityCliPathResolver.Resolve(string.Empty, string.Empty);

            Assert.That(environment.ExecutablePath, Is.EqualTo("/environment/unity"));
            Assert.That(environment.Source, Is.EqualTo(UnityCliPathSource.Environment));
            Assert.That(path.ExecutablePath, Is.EqualTo("unity"));
            Assert.That(path.Source, Is.EqualTo(UnityCliPathSource.Path));
        }

        [Test]
        public void CliPathResolver_RecognizesExplicitWindowsAbsolutePathsOnEveryEditorPlatform()
        {
            var resolution = UnityCliPathResolver.Resolve(@"C:\Program Files\Unity\unity.exe", null);

            Assert.That(resolution.IsExplicitAbsolutePath, Is.True);
        }

        [Test]
        public void PipelineStatus_IdentifiesExactMissingAndUntestedVersions()
        {
            Assert.That(UnityCliPipelineStatus.Classify("0.3.1-exp.1"), Is.EqualTo(UnityCliPipelineState.ExactSupported));
            Assert.That(UnityCliPipelineStatus.Classify(null), Is.EqualTo(UnityCliPipelineState.Missing));
            Assert.That(UnityCliPipelineStatus.Classify("0.3.2-exp.1"), Is.EqualTo(UnityCliPipelineState.DifferentUntested));
            Assert.That(UnityCliPipelineStatus.GetDisplayName(UnityCliPipelineState.ExactSupported),
                Is.EqualTo("exact supported"));
            Assert.That(UnityCliPipelineStatus.GetDisplayName(UnityCliPipelineState.Missing), Is.EqualTo("missing"));
            Assert.That(UnityCliPipelineStatus.GetDisplayName(UnityCliPipelineState.DifferentUntested),
                Is.EqualTo("different/untested"));
        }

        [Test]
        public void CliVersionClassifier_ParsesAndClassifiesCompatibilityBoundaries()
        {
            Assert.That(UnityCliVersionClassifier.Classify("unity 1.0.0-beta.1", true, false).Status,
                Is.EqualTo(UnityCliCompatibility.Incompatible));
            Assert.That(UnityCliVersionClassifier.Classify("unity 1.0.0-beta.2", true, false).Status,
                Is.EqualTo(UnityCliCompatibility.Compatible));
            Assert.That(UnityCliVersionClassifier.Classify("unity 1.0.0", true, false).Status,
                Is.EqualTo(UnityCliCompatibility.Compatible));
            Assert.That(UnityCliVersionClassifier.Classify("unity 1.0.0-rc.1", true, false).Status,
                Is.EqualTo(UnityCliCompatibility.Compatible));
            Assert.That(UnityCliVersionClassifier.Classify("unity 1.0.0-alpha.9", true, false).Status,
                Is.EqualTo(UnityCliCompatibility.Incompatible));
            Assert.That(UnityCliVersionClassifier.Classify("unity 2.0.0", true, false).Status,
                Is.EqualTo(UnityCliCompatibility.UntestedNewer));
            Assert.That(UnityCliVersionClassifier.Classify("unknown", true, false).Status,
                Is.EqualTo(UnityCliCompatibility.MissingOrFailed));
            Assert.That(UnityCliVersionClassifier.Classify("unity 1.0.0-beta.2", false, false).Status,
                Is.EqualTo(UnityCliCompatibility.MissingOrFailed));
        }

        [Test]
        public void CliCheck_UsesOnlyVersionCommandWithFiveSecondTimeoutAndClassifiesFailures()
        {
            var runner = new RecordingRunner(new UnityCliProcessResult("", "not found", 127, false));
            var service = new UnityCliCheckService(runner, () => "/environment/unity");

            var result = service.CheckAsync(string.Empty, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(runner.ExecutablePath, Is.EqualTo("/environment/unity"));
            Assert.That(runner.Arguments, Is.EqualTo("--version"));
            Assert.That(runner.Timeout, Is.EqualTo(TimeSpan.FromSeconds(5)));
            Assert.That(result.Compatibility.Status, Is.EqualTo(UnityCliCompatibility.MissingOrFailed));
        }

        [Test]
        public void CliCheck_ClassifiesRunnerTimeoutAsMissingOrFailed()
        {
            var runner = new RecordingRunner(new UnityCliProcessResult("", "", -1, true));
            var service = new UnityCliCheckService(runner, () => null);

            var result = service.CheckAsync("/opt/unity", CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result.Compatibility.Status, Is.EqualTo(UnityCliCompatibility.MissingOrFailed));
            Assert.That(result.Process.TimedOut, Is.True);
        }

        [Test]
        public void SetupContent_UsesOfficialPlatformInstallCommands()
        {
            Assert.That(UnityCliSetupContent.GetInstallCommand(false), Is.EqualTo(
                "curl -fsSL https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.sh | UNITY_CLI_CHANNEL=beta bash"));
            Assert.That(UnityCliSetupContent.GetInstallCommand(true), Is.EqualTo(
                "$env:UNITY_CLI_CHANNEL='beta'; irm https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.ps1 | iex"));
            Assert.That(UnityCliSetupContent.DocumentationUrl, Is.EqualTo(
                "https://docs.unity.com/en-us/unity-cli/use-unity-cli"));
        }

        [Test]
        public void CliConfiguration_GeneratesEscapedOfficialAndCompanionEntries()
        {
            const string projectPath = "C:\\Project Space\\\"Quoted\"";
            const string executablePath = "C:\\Program Files\\Unity\\unity.exe";
            const string packagePath = "C:\\Package Space\\mcp-unity";

            var official = JObject.Parse(UnityCliConfiguration.CreateOfficial(executablePath, projectPath));
            var companion = JObject.Parse(UnityCliConfiguration.CreateCompanion(
                packagePath, projectPath, executablePath, true));

            Assert.That((string)official["mcpServers"]["unity"]["command"], Is.EqualTo(executablePath));
            Assert.That(official["mcpServers"]["unity"]["args"].Values<string>(), Is.EqualTo(new[]
            {
                "mcp", "--project-path", projectPath
            }));
            Assert.That((string)companion["mcpServers"]["mcp-unity-companion"]["command"], Is.EqualTo("node"));
            Assert.That((string)companion["mcpServers"]["mcp-unity-companion"]["args"][0], Is.EqualTo(
                "C:\\Package Space\\mcp-unity/Server~/build/index.js"));
            Assert.That((string)companion["mcpServers"]["mcp-unity-companion"]["env"]["UNITY_CLI_PATH"],
                Is.EqualTo(executablePath));
            Assert.That(UnityCliConfiguration.CreateCompanion(packagePath, projectPath, executablePath, false),
                Does.Not.Contain("UNITY_CLI_PATH"));
        }

        [Test]
        public void SetupServices_DoNotWriteProjectSettingsOrConfigurationFiles()
        {
            var projectSettings = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ProjectSettings");
            var before = Directory.Exists(projectSettings)
                ? Directory.GetFiles(projectSettings, "*", SearchOption.AllDirectories)
                : Array.Empty<string>();
            var runner = new RecordingRunner(new UnityCliProcessResult("unity 1.0.0-beta.2", "", 0, false));
            var service = new UnityCliCheckService(runner, () => null);

            service.CheckAsync("/opt/unity", CancellationToken.None).GetAwaiter().GetResult();

            var after = Directory.Exists(projectSettings)
                ? Directory.GetFiles(projectSettings, "*", SearchOption.AllDirectories)
                : Array.Empty<string>();
            Assert.That(after, Is.EquivalentTo(before));
        }

        [Test]
        public void SetupWindow_IsUserInitiatedWithoutStaticInitialization()
        {
            var package = PackageInfo.FindForAssembly(typeof(AssignMaterialCommand).Assembly);
            var windowPath = Path.Combine(package.assetPath, "Editor/Setup/UnityCliSetupWindow.cs");

            Assert.That(File.Exists(windowPath), Is.True);
            var source = File.ReadAllText(windowPath);
            Assert.That(source, Does.Contain("[MenuItem(\"Window/MCP Unity/Setup\")]"));
            Assert.That(source, Does.Not.Contain("InitializeOnLoad"));
            Assert.That(source, Does.Not.Contain("[InitializeOnLoad"));
        }

        private sealed class RecordingRunner : IUnityCliProcessRunner
        {
            private readonly UnityCliProcessResult result;

            public RecordingRunner(UnityCliProcessResult result)
            {
                this.result = result;
            }

            public string ExecutablePath { get; private set; }

            public string Arguments { get; private set; }

            public TimeSpan Timeout { get; private set; }

            public Task<UnityCliProcessResult> RunAsync(
                string executablePath,
                string arguments,
                TimeSpan timeout,
                CancellationToken cancellationToken)
            {
                ExecutablePath = executablePath;
                Arguments = arguments;
                Timeout = timeout;
                return Task.FromResult(result);
            }
        }
    }
}
