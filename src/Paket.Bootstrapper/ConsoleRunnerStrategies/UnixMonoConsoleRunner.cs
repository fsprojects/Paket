using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using static Paket.Bootstrapper.ConsoleRunnerStrategies.UnixPInvoke;

namespace Paket.Bootstrapper.ConsoleRunnerStrategies
{
    class UnixMonoConsoleRunner : IConsoleRunner
    {
        private static readonly bool IsMono = Type.GetType("Mono.Runtime") != null;

        private static readonly PlatformID[] Platforms = {
            PlatformID.Unix,
            PlatformID.MacOSX
        };

        public bool IsSupported => IsMono && Platforms.Contains(Environment.OSVersion.Platform);

        public void RunAndExit(string program, IEnumerable<string> arguments)
        {
            var finalArguments = new[] {"mono", program}.Concat(arguments).ToArray();

            var childPid = fork();
            if (childPid == 0)
            {
                execvp("mono", finalArguments);
                _exit(0);
            }

            int status;
            if (waitpid(childPid, out status, 0) != -1)
            {
                if (WIFEXITED(status))
                {
                    Environment.Exit(WEXITSTATUS(status));
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Running under mono failed for '{program}'. errno = {Marshal.GetLastWin32Error()}");
                }
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unknown error while running '{program}'. errno = {Marshal.GetLastWin32Error()}");
            }
        }
    }
}
