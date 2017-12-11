using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Paket.Bootstrapper
{
    static class PaketRunner
    {
        static readonly Version VersionWithFromBootstrapper = new Version("3.23.3");

        static IEnumerable<string> SetBootstrapperArgument(string program, IEnumerable<string> arguments)
        {
            var setBootstrapperArg = true;
            try
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(program);
                var version = new Version(versionInfo.FileVersion);
                setBootstrapperArg = version >= VersionWithFromBootstrapper;
            }
            catch (Exception ex)
            {
                ConsoleImpl.WriteWarning("Could not detect bootstrapper version. Message: {0}", ex.Message);
            }

            return
               setBootstrapperArg
                    ? new[] { "--from-bootstrapper" }.Concat(arguments)
                    : arguments;
        }

        public static int Run(string program, IEnumerable<string> arguments)
        {
            arguments = SetBootstrapperArgument(program, arguments);
            var argString = WindowsProcessArguments.ToString(arguments);
            var process = new Process
            {
                StartInfo =
                {
                    FileName = program,
                    Arguments = argString,
                    UseShellExecute = false
                }
            };
            process.Start();
            process.WaitForExit();
            return process.ExitCode;
        }
    }
}