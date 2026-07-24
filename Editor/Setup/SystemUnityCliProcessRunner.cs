using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace McpUnity.Extensions.Setup
{
    public sealed class SystemUnityCliProcessRunner : IUnityCliProcessRunner
    {
        public async Task<UnityCliProcessResult> RunAsync(
            string executablePath,
            string arguments,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            using (var process = new Process())
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

                var completed = await Task.WhenAny(exited.Task, Task.Delay(timeout, cancellationToken));
                if (completed != exited.Task)
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }

                    await exited.Task;
                    return new UnityCliProcessResult(
                        await standardOutput,
                        await standardError,
                        process.ExitCode,
                        true);
                }

                return new UnityCliProcessResult(
                    await standardOutput,
                    await standardError,
                    process.ExitCode,
                    false);
            }
        }
    }
}
