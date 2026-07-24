namespace McpUnity.Extensions.Setup
{
    public static class UnityCliSetupContent
    {
        public const string DocumentationUrl = "https://docs.unity.com/en-us/unity-cli/use-unity-cli";

        public static string GetInstallCommand(bool isWindows)
        {
            return isWindows
                ? "$env:UNITY_CLI_CHANNEL='beta'; irm https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.ps1 | iex"
                : "curl -fsSL https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.sh | UNITY_CLI_CHANNEL=beta bash";
        }
    }
}
