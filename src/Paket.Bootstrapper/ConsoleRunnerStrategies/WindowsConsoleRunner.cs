using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace Paket.Bootstrapper.ConsoleRunnerStrategies
{
    class WindowsConsoleRunner : IConsoleRunner
    {
        private static readonly PlatformID[] Platforms = {
            PlatformID.Win32NT,
            PlatformID.Win32Windows
        };

        public bool IsSupported => Platforms.Contains(Environment.OSVersion.Platform);

        public unsafe void RunAndExit(string program, IEnumerable<string> arguments)
        {
            program = Path.GetFullPath(program);

            var argString = WindowsProcessArguments.ToString(new [] { program }.Concat(arguments));
            var startupInfo = WindowsPInvoke.STARTUPINFO.Create();
            WindowsPInvoke.PROCESS_INFORMATION processInfo;
            
            // We must use PInvoke instead of Process.Start because we want to pass our console handles untouched
            // and it's an unsupported scenario.
            var result = WindowsPInvoke.CreateProcess(
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

            if (!result)
            {
                throw new Win32Exception();
            }

            var hProcess = new WindowsPInvoke.SafeObjectHandle(processInfo.hProcess);
            var hThread = new WindowsPInvoke.SafeObjectHandle(processInfo.hThread);
            WindowsPInvoke.WaitForSingleObject(hProcess, -1);

            int exitCode;
            WindowsPInvoke.GetExitCodeProcess(processInfo.hProcess, out exitCode);

            hProcess.Close();
            hThread.Close();

            // It might seem strange to exit the process from deep in the callstack but that's the only easy way
            // to do it under unix. So we do the same on windows.
            Environment.Exit(exitCode);
        }
    }
}
