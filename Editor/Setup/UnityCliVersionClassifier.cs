using System;
using System.Text.RegularExpressions;

namespace McpUnity.Extensions.Setup
{
    public enum UnityCliCompatibility
    {
        MissingOrFailed,
        Incompatible,
        Compatible,
        UntestedNewer
    }

    public sealed class UnityCliCompatibilityResult
    {
        public UnityCliCompatibilityResult(UnityCliCompatibility status, string version)
        {
            Status = status;
            Version = version;
        }

        public UnityCliCompatibility Status { get; }

        public string Version { get; }
    }

    public static class UnityCliVersionClassifier
    {
        private static readonly Regex VersionPattern = new Regex(
            @"(?<![0-9])(?<major>0|[1-9][0-9]*)\.(?<minor>0|[1-9][0-9]*)\.(?<patch>0|[1-9][0-9]*)(?:-(?<pre>[0-9A-Za-z.-]+))?",
            RegexOptions.Compiled);

        public static UnityCliCompatibilityResult Classify(string output, bool processSucceeded, bool timedOut)
        {
            if (!processSucceeded || timedOut || string.IsNullOrEmpty(output))
            {
                return new UnityCliCompatibilityResult(UnityCliCompatibility.MissingOrFailed, null);
            }

            var match = VersionPattern.Match(output);
            if (!match.Success)
            {
                return new UnityCliCompatibilityResult(UnityCliCompatibility.MissingOrFailed, null);
            }

            var version = match.Value;
            var major = int.Parse(match.Groups["major"].Value);
            if (major > 1)
            {
                return new UnityCliCompatibilityResult(UnityCliCompatibility.UntestedNewer, version);
            }

            if (major < 1 || CompareToMinimum(match) < 0)
            {
                return new UnityCliCompatibilityResult(UnityCliCompatibility.Incompatible, version);
            }

            return new UnityCliCompatibilityResult(UnityCliCompatibility.Compatible, version);
        }

        private static int CompareToMinimum(Match match)
        {
            var minor = int.Parse(match.Groups["minor"].Value);
            if (minor != 0)
            {
                return minor;
            }

            var patch = int.Parse(match.Groups["patch"].Value);
            if (patch != 0)
            {
                return patch;
            }

            var prerelease = match.Groups["pre"].Success ? match.Groups["pre"].Value : null;
            if (prerelease == null)
            {
                return 1;
            }

            return ComparePrerelease(prerelease, "beta.2");
        }

        private static int ComparePrerelease(string left, string right)
        {
            var leftIdentifiers = left.Split('.');
            var rightIdentifiers = right.Split('.');
            var length = Math.Max(leftIdentifiers.Length, rightIdentifiers.Length);
            for (var index = 0; index < length; index++)
            {
                if (index == leftIdentifiers.Length)
                {
                    return -1;
                }

                if (index == rightIdentifiers.Length)
                {
                    return 1;
                }

                int leftNumber;
                int rightNumber;
                var leftIsNumber = int.TryParse(leftIdentifiers[index], out leftNumber);
                var rightIsNumber = int.TryParse(rightIdentifiers[index], out rightNumber);
                if (leftIsNumber && rightIsNumber)
                {
                    if (leftNumber != rightNumber)
                    {
                        return leftNumber.CompareTo(rightNumber);
                    }

                    continue;
                }

                if (leftIsNumber != rightIsNumber)
                {
                    return leftIsNumber ? -1 : 1;
                }

                var comparison = string.CompareOrdinal(leftIdentifiers[index], rightIdentifiers[index]);
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return 0;
        }
    }
}
