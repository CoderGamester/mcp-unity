using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
    public class UnityCliSetupReviewTests
    {
        [Test]
        public void CompanionConfiguration_UsesAbsoluteResolvedPackagePathThroughTheWindowBoundary()
        {
            var package = PackageInfo.FindForAssembly(typeof(AssignMaterialCommand).Assembly);
            var resolvedPackagePath = Path.GetFullPath(package.resolvedPath);
            var configuration = JObject.Parse(UnityCliConfiguration.CreateCompanion(
                resolvedPackagePath,
                "/absolute/project",
                "/absolute/unity",
                true));
            var serverPath = (string)configuration["mcpServers"]["mcp-unity-companion"]["args"][0];
            var windowSource = File.ReadAllText(Path.Combine(package.assetPath, "Editor/Setup/UnityCliSetupWindow.cs"));

            Assert.That(Path.IsPathRooted(serverPath), Is.True);
            Assert.That(serverPath, Is.EqualTo(
                resolvedPackagePath.TrimEnd('/', '\\') + "/Server~/build/index.js"));
            Assert.That(windowSource, Does.Contain("package.resolvedPath"));
            Assert.That(windowSource, Does.Not.Contain("package.assetPath"));
        }

        [Test]
        public void CliVersionClassifier_RejectsMalformedPrereleasesAndClassifiesHugeCoreIdentifiersWithoutThrowing()
        {
            var huge = new string('9', 128);

            Assert.DoesNotThrow(() =>
                UnityCliVersionClassifier.Classify("unity " + huge + ".0.0", true, false));
            Assert.That(UnityCliVersionClassifier.Classify("unity " + huge + ".0.0", true, false).Status,
                Is.EqualTo(UnityCliCompatibility.UntestedNewer));
            Assert.That(UnityCliVersionClassifier.Classify("unity 1.0.0-", true, false).Status,
                Is.EqualTo(UnityCliCompatibility.MissingOrFailed));
            Assert.That(UnityCliVersionClassifier.Classify("unity 1.0.0-beta..2", true, false).Status,
                Is.EqualTo(UnityCliCompatibility.MissingOrFailed));
            Assert.That(UnityCliVersionClassifier.Classify("unity 1.0.0-beta.02", true, false).Status,
                Is.EqualTo(UnityCliCompatibility.MissingOrFailed));
            Assert.That(UnityCliVersionClassifier.Classify("unity 1." + huge + ".0", true, false).Status,
                Is.EqualTo(UnityCliCompatibility.Compatible));
        }

        [Test]
        public void CliVersionClassifier_ValidatesBuildMetadataWithoutChangingPrecedence()
        {
            var huge = new string('9', 128);

            var release = UnityCliVersionClassifier.Classify("unity 1.0.0+build.7", true, false);
            var prerelease = UnityCliVersionClassifier.Classify("unity 1.0.0-beta.2+build.7", true, false);

            Assert.That(release.Status, Is.EqualTo(UnityCliCompatibility.Compatible));
            Assert.That(release.Version, Is.EqualTo("1.0.0+build.7"));
            Assert.That(prerelease.Status, Is.EqualTo(UnityCliCompatibility.Compatible));
            Assert.That(UnityCliVersionClassifier.Classify("unity " + huge + ".0.0+build.7", true, false).Status,
                Is.EqualTo(UnityCliCompatibility.UntestedNewer));
            Assert.That(UnityCliVersionClassifier.Classify("unity 1.0.0+", true, false).Status,
                Is.EqualTo(UnityCliCompatibility.MissingOrFailed));
            Assert.That(UnityCliVersionClassifier.Classify("unity 1.0.0+build..7", true, false).Status,
                Is.EqualTo(UnityCliCompatibility.MissingOrFailed));
            Assert.That(UnityCliVersionClassifier.Classify("unity 1.0.0+build!", true, false).Status,
                Is.EqualTo(UnityCliCompatibility.MissingOrFailed));
        }

        [Test]
        public void CliCheckService_ContainsMalformedAndLargeVersionOutput()
        {
            var huge = new string('9', 128);
            var service = new UnityCliCheckService(
                new FixedRunner(new UnityCliProcessResult("unity " + huge + ".0.0-beta.02", string.Empty, 0, false)),
                () => null);

            UnityCliCheckResult result = null;
            Assert.DoesNotThrow(() => result = service.CheckAsync("/opt/unity", CancellationToken.None).GetAwaiter().GetResult());

            Assert.That(result.Compatibility.Status, Is.EqualTo(UnityCliCompatibility.MissingOrFailed));
        }

        [Test]
        public async Task ProcessRunner_TimeoutKillsItsOwnedProcessAndReturnsDespiteAChildHoldingPipes()
        {
            RequirePosixHelper();
            var helperDirectory = CreateHelperDirectory();
            var parentPidPath = Path.Combine(helperDirectory, "parent.pid");
            var childPidPath = Path.Combine(helperDirectory, "child.pid");
            var runner = new SystemUnityCliProcessRunner();
            var command = "echo $$ > '" + parentPidPath +
                "'; exec 3>&1 4>&2; (while :; do sleep 1; done) >&3 2>&4 & echo $! > '" + childPidPath + "'; wait";

            try
            {
                var stopwatch = Stopwatch.StartNew();
                var task = runner.RunAsync(
                    "/bin/sh",
                    "-c \"" + command + "\"",
                    TimeSpan.FromMilliseconds(100),
                    CancellationToken.None);

                Assert.That(WaitForFile(childPidPath, TimeSpan.FromSeconds(1)), Is.True);
                Assert.That(await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(2))), Is.EqualTo(task));

                var result = await task;
                Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)));
                Assert.That(result.TimedOut, Is.True);
                Assert.That(result.Cancelled, Is.False);
                Assert.That(IsProcessRunning(ReadPid(parentPidPath)), Is.False);
            }
            finally
            {
                KillIfRunning(ReadPid(childPidPath));
                Directory.Delete(helperDirectory, true);
            }
        }

        [Test]
        public async Task ProcessRunner_ReportsExternalCancellationSeparatelyFromTimeout()
        {
            RequirePosixHelper();
            var helperDirectory = CreateHelperDirectory();
            var parentPidPath = Path.Combine(helperDirectory, "parent.pid");
            var runner = new SystemUnityCliProcessRunner();
            var command = "echo $$ > '" + parentPidPath + "'; sleep 30";
            var cancellation = new CancellationTokenSource();

            try
            {
                var task = runner.RunAsync(
                    "/bin/sh",
                    "-c \"" + command + "\"",
                    TimeSpan.FromSeconds(5),
                    cancellation.Token);
                Assert.That(WaitForFile(parentPidPath, TimeSpan.FromSeconds(1)), Is.True);

                cancellation.Cancel();
                Assert.That(await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(2))), Is.EqualTo(task));

                var result = await task;
                Assert.That(result.TimedOut, Is.False);
                Assert.That(result.Cancelled, Is.True);
                Assert.That(IsProcessRunning(ReadPid(parentPidPath)), Is.False);
            }
            finally
            {
                cancellation.Dispose();
                KillIfRunning(ReadPid(parentPidPath));
                Directory.Delete(helperDirectory, true);
            }
        }

        [Test]
        public async Task ProcessRunner_DeadlineBoundsDrainWhenTheParentExitsButChildKeepsPipesOpen()
        {
            RequirePosixHelper();
            var helperDirectory = CreateHelperDirectory();
            var childPidPath = Path.Combine(helperDirectory, "child.pid");
            var runner = new SystemUnityCliProcessRunner();
            var command = "exec 3>&1 4>&2; (while :; do sleep 1; done) >&3 2>&4 & echo $! > '" +
                childPidPath + "'; exit 0";

            try
            {
                var stopwatch = Stopwatch.StartNew();
                var task = runner.RunAsync(
                    "/bin/sh",
                    "-c \"" + command + "\"",
                    TimeSpan.FromMilliseconds(100),
                    CancellationToken.None);
                Assert.That(WaitForFile(childPidPath, TimeSpan.FromSeconds(1)), Is.True);

                Assert.That(await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(2))), Is.EqualTo(task));

                var result = await task;
                Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)));
                Assert.That(result.TimedOut, Is.True);
                Assert.That(IsProcessRunning(ReadPid(childPidPath)), Is.True);
            }
            finally
            {
                KillIfRunning(ReadPid(childPidPath));
                Directory.Delete(helperDirectory, true);
            }
        }

        [Test]
        public void ProcessRunner_UsesDisposableCancellationSourcesForDeadlineAndCleanup()
        {
            var package = PackageInfo.FindForAssembly(typeof(AssignMaterialCommand).Assembly);
            var source = File.ReadAllText(Path.Combine(
                package.assetPath,
                "Editor/Setup/SystemUnityCliProcessRunner.cs"));

            Assert.That(source, Does.Contain("new CancellationTokenSource(timeout)"));
            Assert.That(source, Does.Contain("CancellationTokenSource.CreateLinkedTokenSource"));
            Assert.That(source, Does.Not.Contain("Task.Delay(timeout)"));
        }

        [Test]
        public void SetupServices_PreserveProjectSettingsContentAndEnvironment()
        {
            var projectSettingsDirectory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ProjectSettings");
            var projectSettingsBefore = SnapshotProjectSettings(projectSettingsDirectory);
            var environmentBefore = SnapshotEnvironment();
            var service = new UnityCliCheckService(
                new FixedRunner(new UnityCliProcessResult("unity 1.0.0-beta.2", string.Empty, 0, false)),
                () => null);

            service.CheckAsync("/opt/unity", CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(SnapshotProjectSettings(projectSettingsDirectory), Is.EqualTo(projectSettingsBefore));
            Assert.That(SnapshotEnvironment(), Is.EqualTo(environmentBefore));
        }

        [Test]
        public void SetupServices_StaticContractHasNoPersistentWriteApis()
        {
            var package = PackageInfo.FindForAssembly(typeof(AssignMaterialCommand).Assembly);
            var setupDirectory = Path.Combine(package.assetPath, "Editor/Setup");
            var source = string.Join("\n", Directory.GetFiles(setupDirectory, "*.cs")
                .Select(File.ReadAllText)
                .ToArray());

            Assert.That(source, Does.Not.Contain("File.Write"));
            Assert.That(source, Does.Not.Contain("File.Append"));
            Assert.That(source, Does.Not.Contain("WriteAll"));
            Assert.That(source, Does.Not.Contain("EditorPrefs.Set"));
            Assert.That(source, Does.Not.Contain("Environment.SetEnvironmentVariable"));
            Assert.That(source, Does.Contain("process.Start()"));
        }

        private sealed class FixedRunner : IUnityCliProcessRunner
        {
            private readonly UnityCliProcessResult result;

            public FixedRunner(UnityCliProcessResult result)
            {
                this.result = result;
            }

            public Task<UnityCliProcessResult> RunAsync(
                string executablePath,
                string arguments,
                TimeSpan timeout,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(result);
            }
        }

        private static string CreateHelperDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "mcp-unity-cli-runner-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static void RequirePosixHelper()
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                Assert.Ignore("POSIX helper-process regression is not available on Windows.");
            }
        }

        private static bool WaitForFile(string path, TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (!File.Exists(path) && stopwatch.Elapsed < timeout)
            {
                Thread.Sleep(10);
            }

            return File.Exists(path);
        }

        private static int ReadPid(string path)
        {
            int pid;
            return File.Exists(path) && int.TryParse(File.ReadAllText(path).Trim(), out pid) ? pid : -1;
        }

        private static bool IsProcessRunning(int pid)
        {
            if (pid <= 0)
            {
                return false;
            }

            try
            {
                using (var process = Process.GetProcessById(pid))
                {
                    return !process.HasExited;
                }
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static void KillIfRunning(int pid)
        {
            if (!IsProcessRunning(pid))
            {
                return;
            }

            using (var process = Process.GetProcessById(pid))
            {
                process.Kill();
            }
        }

        private static Dictionary<string, string> SnapshotProjectSettings(string directory)
        {
            var snapshot = new Dictionary<string, string>();
            if (!Directory.Exists(directory))
            {
                return snapshot;
            }

            foreach (var path in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                snapshot[Path.GetRelativePath(directory, path)] = ComputeHash(path);
            }

            return snapshot;
        }

        private static Dictionary<string, string> SnapshotEnvironment()
        {
            var snapshot = new Dictionary<string, string>();
            foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
            {
                snapshot[(string)variable.Key] = (string)variable.Value;
            }

            return snapshot;
        }

        private static string ComputeHash(string path)
        {
            using (var sha256 = SHA256.Create())
            {
                return BitConverter.ToString(sha256.ComputeHash(File.ReadAllBytes(path)));
            }
        }
    }
}
