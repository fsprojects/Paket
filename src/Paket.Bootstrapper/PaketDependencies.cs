using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Paket.Bootstrapper
{
    static class PaketDependencies
    {
        private static readonly Regex bootstrapperArgsLine =
            new Regex("^\\s*version\\s+(?<args>.*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        const string DEPENDENCY_FILE = "paket.dependencies";

        public static string GetBootstrapperArgs(TextReader reader)
        {
            var line = reader.ReadLine();
            while (line != null)
            {
                var match = bootstrapperArgsLine.Match(line);
                if (match.Success)
                {
                    return match.Groups["args"].Value.Trim();
                }
                line = reader.ReadLine();
            }
            return null;
        }

        public static string LocateDependenciesFile(DirectoryInfo folder)
        {
            var path = Path.Combine(folder.FullName, DEPENDENCY_FILE);
            if (File.Exists(path))
            {
                return path;
            }
            else
            {
                if (folder.Parent != null)
                {
                    return LocateDependenciesFile(folder.Parent);
                }
                else
                {
                    return null;
                }
            }
        }

        public static string GetBootstrapperArgsForFolder(string folder)
        {
            try
            {
                var path = LocateDependenciesFile(new DirectoryInfo(folder));
                if (path == null)
                {
                    ConsoleImpl.WriteTrace("Dependencies file was not found.");
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
            catch (Exception e)
            {
                // ¯\_(ツ)_/¯
                ConsoleImpl.WriteTrace("Error while retrieving arguments from paket.dependencies file: {0}", e);
                return null;
            }
        }
    }
}
