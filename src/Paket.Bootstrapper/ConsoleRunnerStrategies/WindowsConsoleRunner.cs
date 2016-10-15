using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using static Paket.Bootstrapper.ConsoleRunnerStrategies.WindowsPInvoke;

namespace Paket.Bootstrapper.ConsoleRunnerStrategies
{
    class WindowsConsoleRunner : IConsoleRunner
    {
        private static readonly PlatformID[] Platforms = {
            PlatformID.Win32NT,
            PlatformID.Win32Windows
        };

        public bool IsSupported => Platforms.Contains(Environment.OSVersion.Platform);

        public unsafe int Run(string program, IEnumerable<string> arguments)
        {
            program = Path.GetFullPath(program);

            var argString = WindowsProcessArguments.ToString(new [] { program }.Concat(arguments));
            var startupInfo = STARTUPINFO.Create();

            bool result;
            int exitCode = 0;
            PROCESS_INFORMATION processInfo;
            RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
            }
            finally
            {
                // We must use PInvoke instead of Process.Start because we want to pass our console handles untouched
                // and it's an unsupported scenario.
                result = CreateProcess(
                    program,
                    argString,
                    null,
                    null,
                    true,
                    0,
                    null,
                    null,
                    ref startupInfo,
                    out processInfo);

                if (result)
                {
                    var hProcess = new SafeObjectHandle(processInfo.hProcess);
                    var hThread = new SafeObjectHandle(processInfo.hThread);
                    WaitForSingleObject(hProcess, -1);

                    GetExitCodeProcess(processInfo.hProcess, out exitCode);

                    hProcess.Close();
                    hThread.Close();
                }
            }

            if (!result)
            {
                throw new Win32Exception();
            }

            return exitCode;
        }
    }
}
