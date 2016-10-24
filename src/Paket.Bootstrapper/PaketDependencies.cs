using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Paket.Bootstrapper
{
    static class PaketDependencies
    {
        private static readonly Regex bootstrapperArgsLine =
            new Regex("^(#|//)\\s*bootstrapper:(?<args>.*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        const string DEPENDENCY_FILE = "paket.dependencies";

        public static string GetBootstrapperArgs(TextReader reader)
        {
            var line = reader.ReadLine();
            while (line != null)
            {
                var match = bootstrapperArgsLine.Match(line);
                if (match.Success)
                {
                    return match.Groups["args"].Value;
                }
                line = reader.ReadLine();
            }
            return null;
        }

        public static string GetBootstrapperArgsForFolder(string folder)
        {
            try
            {
                var path = Path.Combine(folder, DEPENDENCY_FILE);
                if (!File.Exists(path))
                {
                    return null;
                }

                using (var fileStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (var reader = new StreamReader(fileStream))
                    {
                        return GetBootstrapperArgs(reader);
                    }
                }
            }
            catch (Exception)
            {
                // ¯\_(ツ)_/¯
                return null;
            }
        }
    }
}
