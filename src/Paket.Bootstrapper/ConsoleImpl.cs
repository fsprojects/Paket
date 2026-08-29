
using System;

namespace Paket.Bootstrapper
{
    public static class ConsoleImpl
    {
        public static Verbosity Verbosity { get; set; }

        public static bool IsTraceEnabled { get { return Verbosity >= Verbosity.Trace; } }

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

        internal static void WriteAlways(string message, params object[] parameters)
        {
            WriteConsoleCore(string.Format(message, parameters), Console.ForegroundColor);
        }

        internal static void WriteAlways(string message)
        {
            WriteConsoleCore(message, Console.ForegroundColor);
        }

        private static void WriteConsole(string message, ConsoleColor consoleColor, Verbosity minVerbosity = Verbosity.Normal)
        {
            if (Verbosity < minVerbosity)
            {
                return;
            }

            WriteConsoleCore(message, consoleColor);
        }

        private static void WriteConsoleCore(string message, ConsoleColor consoleColor)
        {
            var oldColor = Console.ForegroundColor;
            // Only touch Console.ForegroundColor when it actually needs to change. On some
            // terminals (e.g. Mono on Linux/macOS) assigning the property - even to the value
            // it already holds - forces an explicit ANSI color code, which can override the
            // terminal's default foreground color (see #3401).
            var needsColorChange = oldColor != consoleColor;
            if (needsColorChange)
            {
                Console.ForegroundColor = consoleColor;
            }
            Console.WriteLine(message);
            if (needsColorChange)
            {
                Console.ForegroundColor = oldColor;
            }
        }
    }

}
