using System;
using System.Threading;
using System.Threading.Tasks;

namespace McpUnity.Extensions.Setup
{
    public sealed class UnityCliProcessResult
    {
        public UnityCliProcessResult(
            string standardOutput,
            string standardError,
            int exitCode,
            bool timedOut,
            bool cancelled = false)
        {
            StandardOutput = standardOutput ?? string.Empty;
            StandardError = standardError ?? string.Empty;
            ExitCode = exitCode;
            TimedOut = timedOut;
            Cancelled = cancelled;
        }

        public string StandardOutput { get; }

        public string StandardError { get; }

        public int ExitCode { get; }

        public bool TimedOut { get; }

        public bool Cancelled { get; }
    }

    public interface IUnityCliProcessRunner
    {
        Task<UnityCliProcessResult> RunAsync(
            string executablePath,
            string arguments,
            TimeSpan timeout,
            CancellationToken cancellationToken);
    }

    public sealed class UnityCliCheckResult
    {
        public UnityCliCheckResult(
            UnityCliPathResolution candidate,
            UnityCliProcessResult process,
            UnityCliCompatibilityResult compatibility)
        {
            Candidate = candidate;
            Process = process;
            Compatibility = compatibility;
        }

        public UnityCliPathResolution Candidate { get; }

        public UnityCliProcessResult Process { get; }

        public UnityCliCompatibilityResult Compatibility { get; }
    }

    public sealed class UnityCliCheckService
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
        private readonly IUnityCliProcessRunner processRunner;
        private readonly Func<string> environmentPath;

        public UnityCliCheckService(IUnityCliProcessRunner processRunner, Func<string> environmentPath)
        {
            this.processRunner = processRunner;
            this.environmentPath = environmentPath;
        }

        public async Task<UnityCliCheckResult> CheckAsync(string liveWindowPath, CancellationToken cancellationToken)
        {
            var candidate = UnityCliPathResolver.Resolve(liveWindowPath, environmentPath());
            UnityCliProcessResult process;
            try
            {
                process = await processRunner.RunAsync(candidate.ExecutablePath, "--version", Timeout, cancellationToken);
            }
            catch (Exception exception)
            {
                process = new UnityCliProcessResult(string.Empty, exception.Message, -1, false);
            }

            var output = process.StandardOutput + Environment.NewLine + process.StandardError;
            var compatibility = UnityCliVersionClassifier.Classify(
                output,
                process.ExitCode == 0 && !process.Cancelled,
                process.TimedOut);
            return new UnityCliCheckResult(candidate, process, compatibility);
        }
    }
}
