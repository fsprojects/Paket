
using System;

namespace Paket.Bootstrapper
{
    public static class ConsoleImpl
    {
        public static Verbosity Verbosity { get; set; }

        public static bool IsTraceEnabled => Verbosity >= Verbosity.Trace;

        internal static void WriteError(string message, params object[] parameters)
        {
            WriteConsole(string.Format(message, parameters), ConsoleColor.Red, Verbosity.ErrorsOnly);
        }

        internal static void WriteError(string message)
        {
            WriteConsole(message, ConsoleColor.Red, Verbosity.ErrorsOnly);
        }

        internal static void WriteWarning(string message, params object[] parameters)
        {
            WriteConsole(string.Format(message, parameters), ConsoleColor.Yellow);
        }

        internal static void WriteWarning(string message)
        {
            WriteConsole(message, ConsoleColor.Yellow);
        }

        internal static void WriteInfo(string message, params object[] parameters)
        {
            WriteConsole(string.Format(message, parameters), Console.ForegroundColor);
        }

        internal static void WriteInfo(string message)
        {
            WriteConsole(message, Console.ForegroundColor);
        }

        internal static void WriteTrace(string message, params object[] parameters)
        {
            WriteConsole(string.Format(message, parameters), ConsoleColor.DarkGray, Verbosity.Trace);
        }

        internal static void WriteTrace(string message)
        {
            WriteConsole(message, ConsoleColor.DarkGray, Verbosity.Trace);
        }

        private static void WriteConsole(string message, ConsoleColor consoleColor, Verbosity minVerbosity = Verbosity.Normal)
        {
            if (Verbosity < minVerbosity)
            {
                return;
            }

            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = consoleColor;
            Console.WriteLine(message);
            Console.ForegroundColor = oldColor;
        }
    }

}
