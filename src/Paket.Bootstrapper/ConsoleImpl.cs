
using System;

namespace Paket.Bootstrapper
{
    public static class ConsoleImpl
    {
        public static Verbosity Verbosity { get; set; }

        internal static void WriteError(string message, params object[] parameters)
        {
            WriteConsole(string.Format(message, parameters), ConsoleColor.Red, Verbosity.ErrorsOnly);
        }

        internal static void WriteInfo(string message, params object[] parameters)
        {
            WriteConsole(string.Format(message, parameters), ConsoleColor.Yellow);
        }

        internal static void WriteDebug(string message, params object[] parameters)
        {
            WriteConsole(string.Format(message, parameters), Console.ForegroundColor);
        }

        internal static void WriteTrace(string message, params object[] parameters)
        {
            WriteConsole(string.Format(message, parameters), ConsoleColor.DarkGray, Verbosity.Trace);
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
