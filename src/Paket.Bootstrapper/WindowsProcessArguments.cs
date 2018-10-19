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

            if (arg == "")
            {
                builder.Append(@"""""");
                return;
            }

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

        private static bool ParseBackslashes(ref int backslashes, StringBuilder buffer, char c)
        {
            if (c == '\\')
            {
                backslashes += 1;
                return true;
            }

            if (backslashes <= 0)
            {
                return false;
            }

            if (c == '"')
            {
                if (backslashes % 2 == 0)
                {
                    // Even number of backslashes, the backslashes are considered escaped but not the quote
                    buffer.Append('\\', backslashes/2);
                }
                else
                {
                    // Odd number of backslashes, the backslashes are considered escaped and the quote too
                    buffer.Append('\\', (backslashes-1)/2);
                    buffer.Append(c);
                    backslashes = 0;
                    return true;
                }
            }
            else
            {
                // Backslashes not followed by a quote are interpreted literally
                buffer.Append('\\', backslashes);
            }
            backslashes = 0;

            return false;
        }

        public static List<string> Parse(string args)
        {
            if (args == null)
            {
                return new List<string>();
            }

            var result = new List<string>();
            var buffer = new StringBuilder(args.Length);
            var inParameter = false;
            var quoted = false;
            var backslashes = 0;

            for(int i = 0; i < args.Length; i++)
            {
                var c = args[i];
                if (!inParameter && c != ' ' && c != '\t')
                {
                    inParameter = true;
                }

                if (!inParameter)
                {
                    continue;
                }

                if (ParseBackslashes(ref backslashes, buffer, c))
                {
                    continue;
                }

                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < args.Length)
                        {
                            var nextC = args[i + 1];
                            if (nextC == '"')
                            {
                                // Special double quote case, insert only one quote and continue the double quoted part
                                // "post 2008" behavior in http://www.daviddeley.com/autohotkey/parameters/parameters.htm#WINARGV
                                i += 1;
                                buffer.Append(c);
                                continue;
                            }
                        }

                        // All escapes have been handled so it ends a quoted part
                        quoted = false;
                    }
                    else
                    {
                        buffer.Append(c);
                    }
                }
                else
                {
                    if (c == ' ' || c == '\t')
                    {
                        inParameter = false;
                        result.Add(buffer.ToString());
                        buffer.Clear();
                    }
                    else if (c == '"')
                    {
                        // All escapes have been handled so it start a quoted part
                        quoted = true;
                    }
                    else
                    {
                        buffer.Append(c);
                    }
                }
            }

            if (inParameter)
            {
                buffer.Append('\\', backslashes);
                result.Add(buffer.ToString());
            }
            
            return result;
        }
    }
}