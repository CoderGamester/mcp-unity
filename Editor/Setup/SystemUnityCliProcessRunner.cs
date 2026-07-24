using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace McpUnity.Extensions.Setup
{
    public sealed class SystemUnityCliProcessRunner : IUnityCliProcessRunner
    {
        private static readonly TimeSpan CleanupGrace = TimeSpan.FromMilliseconds(250);

        public async Task<UnityCliProcessResult> RunAsync(
            string executablePath,
            string arguments,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            using (var process = new Process())
            using (var deadlineSource = new CancellationTokenSource(timeout))
            using (var interruptionSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                deadlineSource.Token))
            {
                try
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = executablePath,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    process.EnableRaisingEvents = true;
                    var exited = new TaskCompletionSource<bool>();
                    process.Exited += (sender, eventArgs) => exited.TrySetResult(true);

                    try
                    {
                        process.Start();
                    }
                    catch (Exception exception)
                    {
                        return new UnityCliProcessResult(string.Empty, exception.Message, -1, false);
                    }

                    var standardOutput = process.StandardOutput.ReadToEndAsync();
                    var standardError = process.StandardError.ReadToEndAsync();
                    if (process.HasExited)
                    {
                        exited.TrySetResult(true);
                    }

                    var interruptionTask = Task.Delay(System.Threading.Timeout.Infinite, interruptionSource.Token);
                    var completed = await Task.WhenAny(exited.Task, interruptionTask);
                    if (completed == exited.Task)
                    {
                        var streams = Task.WhenAll(standardOutput, standardError);
                        if (await Task.WhenAny(streams, interruptionTask) == streams)
                        {
                            return new UnityCliProcessResult(
                                await standardOutput,
                                await standardError,
                                process.ExitCode,
                                false);
                        }
                    }

                    var timedOut = deadlineSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested;
                    TryKillOwnedProcess(process);
                    return await CompleteInterruptedProcessAsync(
                        process,
                        exited.Task,
                        standardOutput,
                        standardError,
                        timedOut);
                }
                finally
                {
                    interruptionSource.Cancel();
                    deadlineSource.Cancel();
                }
            }
        }

        private static async Task<UnityCliProcessResult> CompleteInterruptedProcessAsync(
            Process process,
            Task exited,
            Task<string> standardOutput,
            Task<string> standardError,
            bool timedOut)
        {
            using (var cleanupSource = new CancellationTokenSource(CleanupGrace))
            {
                var cleanupTask = Task.Delay(System.Threading.Timeout.Infinite, cleanupSource.Token);
                await Task.WhenAny(exited, cleanupTask);
                await Task.WhenAny(Task.WhenAll(standardOutput, standardError), cleanupTask);
                return new UnityCliProcessResult(
                    GetCompletedResult(standardOutput),
                    GetCompletedResult(standardError),
                    TryGetExitCode(process),
                    timedOut,
                    !timedOut);
            }
        }

        private static void TryKillOwnedProcess(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static int TryGetExitCode(Process process)
        {
            try
            {
                return process.HasExited ? process.ExitCode : -1;
            }
            catch (InvalidOperationException)
            {
                return -1;
            }
        }

        private static string GetCompletedResult(Task<string> task)
        {
            return task.Status == TaskStatus.RanToCompletion ? task.Result : string.Empty;
        }
    }
}
