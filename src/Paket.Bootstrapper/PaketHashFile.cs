using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Paket.Bootstrapper
{
    public class PaketHashFile
    {
        public List<string> Content { get; private set; }

        public PaketHashFile(List<string> content)
        {
            Content = content;
        }

        public static PaketHashFile FromStrings(IEnumerable<string> lines)
        {
            return new PaketHashFile(lines as List<string> ?? lines.ToList());
        }

        public static PaketHashFile FromString(string value)
        {
            return FromStrings(value.Split(new[] {'\r', '\n'}, StringSplitOptions.RemoveEmptyEntries));
        }

        public void WriteToStream(Stream stream)
        {
            using (var writer = new StreamWriter(stream, Encoding.UTF8, 1024, true))
            {
                foreach (var line in Content)
                {
                    writer.WriteLine(line);
                }
            }
        }

        public override string ToString()
        {
            return string.Join("\r\n", Content);
        }
    }
}
