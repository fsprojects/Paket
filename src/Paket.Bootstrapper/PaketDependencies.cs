using Paket.Bootstrapper.HelperProxies;
using System;
using System.IO;
using Path = Pri.LongPath.Path;
using Directory = Pri.LongPath.Directory;
using DirectoryInfo = Pri.LongPath.DirectoryInfo;
using File = Pri.LongPath.File;
using System.Text.RegularExpressions;

namespace Paket.Bootstrapper
{
    internal static class PaketDependencies
    {
        private static readonly Regex bootstrapperArgsLine =
            new Regex("^\\s*version\\s+(?<args>.*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        internal const string DEPENDENCY_FILE = "paket.dependencies";

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

        public static string LocateDependenciesFile(IFileSystemProxy proxy, DirectoryInfo folder)
        {
            var path = Path.Combine(folder.FullName, DEPENDENCY_FILE);
            if (proxy.FileExists(path))
            {
                return path;
            }
            else
            {
                if (folder.Parent != null)
                {
                    return LocateDependenciesFile(proxy, folder.Parent);
                }
                else
                {
                    return null;
                }
            }
        }

        public static string GetBootstrapperArgsForFolder(IFileSystemProxy proxy)
        {
            try
            {
                var folder = proxy.GetCurrentDirectory();
                var path = LocateDependenciesFile(proxy, new DirectoryInfo(folder));
                if (path == null)
                {
                    ConsoleImpl.WriteTrace("Dependencies file was not found.");
                    return null;
                }

                using (var fileStream = proxy.OpenRead(path))
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
