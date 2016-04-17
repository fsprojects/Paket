
using System;

namespace Paket.Bootstrapper
{
    public static class ConsoleImpl
    {
        public static bool IsSilent { get; set; }
        internal static void WriteError(string message, params object[] parameters)
        {
            WriteError(String.Format(message, parameters));
        }

        internal static void WriteError(string message)
        {
            WriteConsole(message, ConsoleColor.Red);
        }

        internal static void WriteInfo(string message, params object[] parameters)
        {
            WriteInfo(String.Format(message, parameters));
        }

        internal static void WriteInfo(string message)
        {
            WriteConsole(message, ConsoleColor.Yellow);
        }
        internal static void WriteDebug(string message, params object[] parameters)
        {
            WriteDebug(String.Format(message, parameters));
        }

        internal static void WriteDebug(string message)
        {
            WriteConsole(message, Console.ForegroundColor);
        }

        private static void WriteConsole(string message, ConsoleColor consoleColor)
        {
            if (IsSilent)
                return;
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = consoleColor;
            Console.WriteLine(message);
            Console.ForegroundColor = oldColor;
        }


    }

}
