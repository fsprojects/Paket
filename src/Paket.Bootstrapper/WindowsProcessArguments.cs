using System.Collections.Generic;
using System.Text;

namespace Paket.Bootstrapper
{
    static class WindowsProcessArguments
    {
        static void AddBackslashes(StringBuilder builder, int backslashes, bool beforeQuote)
        {
            if (backslashes == 0)
            {
                return;
            }

            // Always using 'backslashes * 2' would work it would just produce needless '\'
            var count = beforeQuote || backslashes % 2 == 0
                ? backslashes * 2
                : (backslashes-1) * 2 + 1 ;

            for(int i = 0; i < count; i++)
            {
                builder.Append('\\');
            }
        }

        /* A good summary of the code used to process arguments in most windows programs can be found at :
         * http://www.daviddeley.com/autohotkey/parameters/parameters.htm#WINARGV
         */
        static void AppendEscaped(StringBuilder builder, string arg)
        {
            builder.EnsureCapacity(builder.Length + arg.Length);

            var needQuote = false;
            var containsQuoteOrBackslash = false;
            foreach(var c in arg)
            {
                needQuote |= (c == ' ');
                needQuote |= (c == '\t');
                containsQuoteOrBackslash |= (c == '"');
                containsQuoteOrBackslash |= (c == '\\');
            }

            if (needQuote)
            {
                builder.Append('"');
            }
            else if (!containsQuoteOrBackslash)
            {
                // No special characters are present, early exit
                builder.Append(arg);
                return;
            }

            var index = 0;
            var backslashes = 0;
            while (index < arg.Length)
            {
                var c = arg[index];
                if (c == '\\')
                {
                    backslashes++;
                }
                else if (c == '"')
                {
                    AddBackslashes(builder, backslashes, true);
                    backslashes = 0;
                    builder.Append('\\');
                    builder.Append(c);
                }
                else
                {
                    AddBackslashes(builder, backslashes, false);
                    backslashes = 0;
                    builder.Append(c);
                }
                index += 1;
            }

            AddBackslashes(builder, backslashes, needQuote);

            if (needQuote)
            {
                builder.Append('"');
            }
        }

        public static string ToString(IEnumerable<string> args)
        {
            var builder = new StringBuilder(255);
            foreach (var arg in args)
            {
                if (builder.Length != 0)
                {
                    builder.Append(' ');
                }
                AppendEscaped(builder, arg);
            }
            return builder.ToString();
        }
    }
}