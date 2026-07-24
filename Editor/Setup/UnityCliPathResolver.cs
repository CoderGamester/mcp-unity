using System;
using System.IO;

namespace McpUnity.Extensions.Setup
{
    public enum UnityCliPathSource
    {
        Window,
        Environment,
        Path
    }

    public sealed class UnityCliPathResolution
    {
        public UnityCliPathResolution(string executablePath, UnityCliPathSource source)
        {
            ExecutablePath = executablePath;
            Source = source;
        }

        public string ExecutablePath { get; }

        public UnityCliPathSource Source { get; }

        public bool IsExplicitAbsolutePath =>
            Source == UnityCliPathSource.Window && IsAbsolutePath(ExecutablePath);

        private static bool IsAbsolutePath(string path)
        {
            return Path.IsPathRooted(path) ||
                (path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' &&
                    (path[2] == '\\' || path[2] == '/')) ||
                path.StartsWith("\\\\", StringComparison.Ordinal);
        }
    }

    public static class UnityCliPathResolver
    {
        public static UnityCliPathResolution Resolve(string liveWindowPath, string environmentPath)
        {
            if (!string.IsNullOrWhiteSpace(liveWindowPath))
            {
                return new UnityCliPathResolution(liveWindowPath.Trim(), UnityCliPathSource.Window);
            }

            if (!string.IsNullOrWhiteSpace(environmentPath))
            {
                return new UnityCliPathResolution(environmentPath.Trim(), UnityCliPathSource.Environment);
            }

            return new UnityCliPathResolution("unity", UnityCliPathSource.Path);
        }
    }
}
