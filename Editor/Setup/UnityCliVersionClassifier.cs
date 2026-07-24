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
            @"(?<![0-9])(?<major>0|[1-9][0-9]*)\.(?<minor>0|[1-9][0-9]*)\.(?<patch>0|[1-9][0-9]*)(?:-(?<pre>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?![0-9A-Za-z.-])",
            RegexOptions.Compiled);

        public static UnityCliCompatibilityResult Classify(string output, bool processSucceeded, bool timedOut)
        {
            if (!processSucceeded || timedOut || string.IsNullOrEmpty(output))
            {
                return new UnityCliCompatibilityResult(UnityCliCompatibility.MissingOrFailed, null);
            }

            try
            {
                var match = VersionPattern.Match(output);
                if (!match.Success || !HasValidPrerelease(match))
                {
                    return new UnityCliCompatibilityResult(UnityCliCompatibility.MissingOrFailed, null);
                }

                var version = match.Value;
                var majorComparison = CompareNumericIdentifier(match.Groups["major"].Value, "1");
                if (majorComparison > 0)
                {
                    return new UnityCliCompatibilityResult(UnityCliCompatibility.UntestedNewer, version);
                }

                if (majorComparison < 0 || CompareToMinimum(match) < 0)
                {
                    return new UnityCliCompatibilityResult(UnityCliCompatibility.Incompatible, version);
                }

                return new UnityCliCompatibilityResult(UnityCliCompatibility.Compatible, version);
            }
            catch
            {
                return new UnityCliCompatibilityResult(UnityCliCompatibility.MissingOrFailed, null);
            }
        }

        private static int CompareToMinimum(Match match)
        {
            var minorComparison = CompareNumericIdentifier(match.Groups["minor"].Value, "0");
            if (minorComparison != 0)
            {
                return minorComparison;
            }

            var patchComparison = CompareNumericIdentifier(match.Groups["patch"].Value, "0");
            if (patchComparison != 0)
            {
                return patchComparison;
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

                var leftIsNumber = IsNumericIdentifier(leftIdentifiers[index]);
                var rightIsNumber = IsNumericIdentifier(rightIdentifiers[index]);
                if (leftIsNumber && rightIsNumber)
                {
                    var numericComparison = CompareNumericIdentifier(leftIdentifiers[index], rightIdentifiers[index]);
                    if (numericComparison != 0)
                    {
                        return numericComparison;
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

        private static bool HasValidPrerelease(Match match)
        {
            if (!match.Groups["pre"].Success)
            {
                return true;
            }

            foreach (var identifier in match.Groups["pre"].Value.Split('.'))
            {
                if (string.IsNullOrEmpty(identifier) ||
                    (IsNumericIdentifier(identifier) && identifier.Length > 1 && identifier[0] == '0'))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsNumericIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            for (var index = 0; index < value.Length; index++)
            {
                if (value[index] < '0' || value[index] > '9')
                {
                    return false;
                }
            }

            return true;
        }

        private static int CompareNumericIdentifier(string left, string right)
        {
            if (left.Length != right.Length)
            {
                return left.Length.CompareTo(right.Length);
            }

            return string.CompareOrdinal(left, right);
        }
    }
}
