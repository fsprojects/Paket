using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

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
            UnixPInvoke.execvp("mono", finalArguments);

            // execv replaces the current process, so if any code execute it mean that it failed
            // The only way to not get killed would be to fork() before, but it's completly unsafe
            // under mono. Luckily starting paket.exe is always the last thing we do so we're ok with
            // not continuing execution.
            throw new InvalidOperationException(
                $"Running under mono failed for '{program}'. errno = {Marshal.GetLastWin32Error()}");
        }
    }
}
