using System.Collections.Generic;
using System.Text;

namespace Paket.Bootstrapper.ConsoleRunnerStrategies
{
    static class WindowsProcessArguments
    {
        static void AppendEscaped(StringBuilder builder, string arg)
        {
            var needQuote = arg.Contains(" ") || arg.Contains("\t");
            if (needQuote)
            {
                builder.Append('"');
            }

            var index = 0;
            while (index < arg.Length)
            {
                var c = arg[index];
                if (c == '"')
                {
                    builder.Append(@"\""");
                    
                } else if (c == '\\')
                {
                    var isLast = index == arg.Length-1;
                    if (isLast && needQuote)
                    {
                        builder.Append(@"\\");
                    } else if (needQuote)
                    {
                        var next = arg[index+1];
                        if (next == '"')
                        {
                            builder.Append(@"\\\""");
                            index += 1;
                        }
                        else
                        {
                            builder.Append(c);
                        }
                        
                    } else
                    {
                        builder.Append(c);
                    }
                }
                else
                {
                    builder.Append(c);
                }
                index += 1;
            }

            if (needQuote)
            {
                builder.Append('"');
            }
        }

        public static string ToString(IEnumerable<string> args)
        {
            var builder = new StringBuilder();
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