using System.Collections.Generic;

namespace Paket.Bootstrapper.ConsoleRunnerStrategies
{
    interface IConsoleRunner
    {
        bool IsSupported { get; }
        int Run(string program, IEnumerable<string> arguments);
    }
}