using System;
using System.IO;
using System.Threading;
using McpUnity.Extensions.Commands;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Extensions.Setup
{
    public sealed class UnityCliSetupWindow : EditorWindow
    {
        private string cliPath = string.Empty;
        private UnityCliCheckResult checkResult;
        private bool isChecking;

        [MenuItem("Window/MCP Unity/Setup")]
        private static void Open()
        {
            GetWindow<UnityCliSetupWindow>("MCP Unity Setup");
        }

        private void OnGUI()
        {
            var projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            EditorGUILayout.LabelField("Unity project", projectPath);
            DrawPipelineStatus();
            EditorGUILayout.Space();

            cliPath = EditorGUILayout.TextField("Unity CLI executable (optional)", cliPath);
            using (new EditorGUI.DisabledScope(isChecking))
            {
                if (GUILayout.Button(isChecking ? "Checking Unity CLI..." : "Check Unity CLI"))
                {
                    CheckUnityCli();
                }
            }

            if (checkResult != null)
            {
                DrawCheckResult(projectPath);
            }
        }

        private void DrawPipelineStatus()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForPackageName("com.unity.pipeline");
            var version = package == null ? null : package.version;
            var state = UnityCliPipelineStatus.GetDisplayName(UnityCliPipelineStatus.Classify(version));
            EditorGUILayout.LabelField("com.unity.pipeline", version ?? "missing");
            EditorGUILayout.LabelField("Pipeline state", state);
        }

        private void DrawCheckResult(string projectPath)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Unity CLI", checkResult.Candidate.ExecutablePath);
            EditorGUILayout.LabelField("Status", checkResult.Compatibility.Status.ToString());
            EditorGUILayout.LabelField("Version", checkResult.Compatibility.Version ?? "not detected");
            if (!string.IsNullOrEmpty(checkResult.Process.StandardError))
            {
                EditorGUILayout.HelpBox(checkResult.Process.StandardError, MessageType.Warning);
            }

            if (checkResult.Compatibility.Status == UnityCliCompatibility.MissingOrFailed ||
                checkResult.Compatibility.Status == UnityCliCompatibility.Incompatible)
            {
                if (GUILayout.Button("Copy official install command"))
                {
                    EditorGUIUtility.systemCopyBuffer = UnityCliSetupContent.GetInstallCommand(Application.platform == RuntimePlatform.WindowsEditor);
                }

                if (GUILayout.Button("Open Unity CLI documentation"))
                {
                    Application.OpenURL(UnityCliSetupContent.DocumentationUrl);
                }

                return;
            }

            if (GUILayout.Button("Copy official MCP configuration"))
            {
                EditorGUIUtility.systemCopyBuffer = UnityCliConfiguration.CreateOfficial(
                    checkResult.Candidate.ExecutablePath,
                    projectPath);
            }

            if (GUILayout.Button("Copy companion configuration"))
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(AssignMaterialCommand).Assembly);
                EditorGUIUtility.systemCopyBuffer = UnityCliConfiguration.CreateCompanion(
                    package.resolvedPath,
                    projectPath,
                    checkResult.Candidate.ExecutablePath,
                    checkResult.Candidate.IsExplicitAbsolutePath);
            }
        }

        private async void CheckUnityCli()
        {
            isChecking = true;
            Repaint();
            var service = new UnityCliCheckService(
                new SystemUnityCliProcessRunner(),
                () => Environment.GetEnvironmentVariable("UNITY_CLI_PATH"));
            var result = await service.CheckAsync(cliPath, CancellationToken.None);
            EditorApplication.delayCall += () => ApplyCheckResult(result);
        }

        private void ApplyCheckResult(UnityCliCheckResult result)
        {
            if (this == null)
            {
                return;
            }

            checkResult = result;
            isChecking = false;
            Repaint();
        }
    }
}
