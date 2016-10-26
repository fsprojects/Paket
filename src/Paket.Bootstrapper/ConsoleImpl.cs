
using System;

namespace Paket.Bootstrapper
{
    public static class ConsoleImpl
    {
        public static SilentMode Silent { get; set; }

        internal static void WriteError(string message, params object[] parameters)
        {
            WriteError(string.Format(message, parameters));
        }

        internal static void WriteError(string message)
        {
            WriteConsole(message, ConsoleColor.Red, true);
        }

        internal static void WriteInfo(string message, params object[] parameters)
        {
            WriteInfo(string.Format(message, parameters));
        }

        internal static void WriteInfo(string message)
        {
            WriteConsole(message, ConsoleColor.Yellow);
        }
        internal static void WriteDebug(string message, params object[] parameters)
        {
            WriteDebug(string.Format(message, parameters));
        }

        internal static void WriteDebug(string message)
        {
            WriteConsole(message, Console.ForegroundColor);
        }

        private static void WriteConsole(string message, ConsoleColor consoleColor, bool error = false)
        {
            if (Silent == SilentMode.Silent)
            {
                return;
            }
            if (Silent == SilentMode.ErrorsOnly && !error)
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
