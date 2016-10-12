using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Paket.Bootstrapper
{
    static class ProcessRunner
    {
        private static Task RedirectStreamAsync(Stream from, Stream to, CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                var buffer = new byte[1024 * 10];
                try
                {
                    while (true)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }
                        var read = await from.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                        if (read == 0)
                        {
                            return;
                        }
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }
                        await to.WriteAsync(buffer, 0, read, cancellationToken);
                    }
                }
                catch (TaskCanceledException)
                {
                    // Don't propagate
                }
            }, CancellationToken.None);
        }

        public static int Run(string fileName, IEnumerable<string> arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = ProcessArguments.ToString(arguments),
                UseShellExecute = false,
                RedirectStandardError =  true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true
            };

            using (var process = Process.Start(psi))
            {
                if (process == null)
                {
                    return 0;
                }

                var stdInTokenSource = new CancellationTokenSource();
                RedirectStreamAsync(Console.OpenStandardInput(), process.StandardInput.BaseStream, stdInTokenSource.Token);
                var outTask = RedirectStreamAsync(process.StandardOutput.BaseStream, Console.OpenStandardOutput(), CancellationToken.None);
                var errorTask = RedirectStreamAsync(process.StandardError.BaseStream, Console.OpenStandardError(), CancellationToken.None);
                process.WaitForExit();

                // The tasks will stop by themselves because the streams will be finished
                Task.WaitAll(outTask, errorTask);

                // No chance that our local stdin finishes, so we must cancel manually
                stdInTokenSource.Cancel();

                return process.ExitCode;
            }
        }
    }
}
