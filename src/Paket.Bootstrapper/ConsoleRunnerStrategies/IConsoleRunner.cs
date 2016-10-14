using System.Collections.Generic;

namespace Paket.Bootstrapper.ConsoleRunnerStrategies
{
    interface IConsoleRunner
    {
        bool IsSupported { get; }
        void RunAndExit(string program, IEnumerable<string> arguments);
    }
}