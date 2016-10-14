using System;
using System.Collections.Generic;
using System.Linq;

namespace Paket.Bootstrapper.ConsoleRunnerStrategies
{
    class ConsoleRunner : IConsoleRunner
    {
        public bool IsSupported => runner != null;

        private static readonly IConsoleRunner[] Runners = {
            new WindowsConsoleRunner(),
            new UnixMonoConsoleRunner()
        };

        private readonly IConsoleRunner runner;

        public ConsoleRunner()
        {
            runner = Runners.FirstOrDefault(r => r.IsSupported);
        }

        public void RunAndExit(string program, IEnumerable<string> arguments)
        {
            if (runner == null)
            {
                throw new InvalidOperationException("No supported runner found");
            }

            runner.RunAndExit(program, arguments);
        }
    }
}