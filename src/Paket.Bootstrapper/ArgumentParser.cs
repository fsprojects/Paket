using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using Path = Pri.LongPath.Path;
using Directory = Pri.LongPath.Directory;
using DirectoryInfo = Pri.LongPath.DirectoryInfo;
using File = Pri.LongPath.File;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Paket.Bootstrapper.HelperProxies;

namespace Paket.Bootstrapper
{
    public static class ArgumentParser
    {
        public static class CommandArgs
        {
            public const string Help = "--help";
            public const string PreferNuget = "--prefer-nuget";
            public const string ForceNuget = "--force-nuget";
            public const string Prerelease = "prerelease";
            public const string NugetSourceArgPrefix = "--nuget-source=";
            public const string SelfUpdate = "--self";
            public const string Silent = "-s";
            public const string Verbose = "-v";
            public const string IgnoreCache = "-f";
            public const string MaxFileAge = "--max-file-age=";
            public const string Run = "--run";
        }
        public static class AppSettingKeys
        {
            public const string PreferNuget = "PreferNuget";
            public const string ForceNuget = "ForceNuget";
            public const string NugetSource = "NugetSource";
            public const string PaketVersion = "PaketVersion";
            public const string Prerelease = "Prerelease";
        }
        public static class EnvArgs
        {
            public const string PaketVersionEnv = "PAKET.VERSION";
        }

        private static void FillTarget(DownloadArguments downloadArguments, bool magicMode, IFileSystemProxy fileSystem)
        {
            var folder = Path.GetDirectoryName(fileSystem.GetExecutingAssemblyPath()) ?? "";
            var target = magicMode ? GetMagicModeTarget(fileSystem) : Path.Combine(folder, "paket.exe");

            downloadArguments.Target = target;
            downloadArguments.Folder = Path.GetDirectoryName(target);
        }

        public static BootstrapperOptions ParseArgumentsAndConfigurations(IEnumerable<string> arguments, NameValueCollection appSettings, IDictionary envVariables, IFileSystemProxy fileSystem, IEnumerable<string> argumentsInDependenciesFile)
        {
            var options = new BootstrapperOptions();
            var commandArgs = arguments.ToList();
            var magicMode = GetIsMagicMode(fileSystem);
            var transparentMagicMode = magicMode && commandArgs.IndexOf(CommandArgs.Run) == -1;

            FillTarget(options.DownloadArguments, magicMode, fileSystem);

            // 1 - AppSettings
            FillOptionsFromAppSettings(options, appSettings);

            // 2 - paket.dependencies
            FillNonRunOptionsFromArguments(options, argumentsInDependenciesFile.ToList());

            // 3 - Environment variables
            FillOptionsFromEnvVariables(options, envVariables);

            // 4 - Command line
            if (transparentMagicMode)
            {
                // Transparent magic mode mean that we're renamed 'paket.exe' and --run wasn't passed

                // Virtually add a '-s'
                options.Verbosity -= 1;
                
                // Assume --run and that all arguments are for the real paket binary
                options.Run = true;
                options.RunArgs = new List<string>(commandArgs);
                commandArgs.Clear();

                // Don't check more than twice a day
                //  - Except if we want pre-releases as we're living on the bleeding edge
                //  - Or if we specify a fixed version because it will never check anyway
                //  - Or if the user specified any other value via 'paket.dependencies'
                if (options.DownloadArguments.IgnorePrerelease
                    && string.IsNullOrEmpty(options.DownloadArguments.LatestVersion)
                    && options.DownloadArguments.MaxFileAgeInMinutes == null)
                {
                    options.DownloadArguments.MaxFileAgeInMinutes = 60*12;
                }
            }
            else
            {
                FillRunOptionsFromArguments(options, commandArgs);
                FillNonRunOptionsFromArguments(options, commandArgs);
            }

            if (!options.DownloadArguments.IgnorePrerelease &&
                !string.IsNullOrEmpty(options.DownloadArguments.LatestVersion))
            {
                // PreRelease + specific version -> we prefer the specific version
                options.DownloadArguments.IgnorePrerelease = true;
            }

            options.UnprocessedCommandArgs = commandArgs;

            if ("true" == Environment.GetEnvironmentVariable("PAKET_BOOTSTRAPPER_TRACE"))
            {
                options.Verbosity = Verbosity.Trace;
            }

            return options;
        }

        private static void FillOptionsFromAppSettings(BootstrapperOptions options, NameValueCollection appSettings)
        {
            if (appSettings.IsTrue(AppSettingKeys.PreferNuget))
            {
                options.PreferNuget = true;
            }
            if (appSettings.IsTrue(AppSettingKeys.ForceNuget))
            {
                options.ForceNuget = true;
            }
            var latestVersion = appSettings.GetKey(AppSettingKeys.PaketVersion);
            if (latestVersion != null)
            {
                options.DownloadArguments.LatestVersion = latestVersion;
            }
            if (appSettings.IsTrue(AppSettingKeys.Prerelease))
            {
                options.DownloadArguments.IgnorePrerelease = false;
            }
            var nugetSource = appSettings.GetKey(AppSettingKeys.NugetSource);
            if (nugetSource != null)
            {
                options.DownloadArguments.NugetSource = nugetSource;
            }
        }

        private static void FillOptionsFromEnvVariables(BootstrapperOptions options, IDictionary envVariables)
        {
            var latestVersion = envVariables.GetKey(EnvArgs.PaketVersionEnv);
            if (latestVersion != null)
            {
                options.DownloadArguments.LatestVersion = latestVersion;
            }
        }

        private static void FillRunOptionsFromArguments(BootstrapperOptions options, List<string> commandArgs)
        {
            var runIndex = commandArgs.IndexOf(CommandArgs.Run);
            if (runIndex != -1)
            {
                options.Run = true;
                options.RunArgs = commandArgs.GetRange(runIndex + 1, commandArgs.Count - runIndex - 1);
                commandArgs.RemoveRange(runIndex, commandArgs.Count - runIndex);
            }
        }

        internal static void FillNonRunOptionsFromArguments(BootstrapperOptions options, List<string> commandArgs)
        {
            if (commandArgs.Contains(CommandArgs.PreferNuget))
            {
                options.PreferNuget = true;
                commandArgs.Remove(CommandArgs.PreferNuget);
            }
            if (commandArgs.Contains(CommandArgs.ForceNuget))
            {
                options.ForceNuget = true;
                commandArgs.Remove(CommandArgs.ForceNuget);
            }
            while (commandArgs.Contains(CommandArgs.Silent))
            {
                options.Verbosity -= 1;
                commandArgs.Remove(CommandArgs.Silent);
            }
            while (commandArgs.Contains(CommandArgs.Verbose))
            {
                options.Verbosity += 1;
                commandArgs.Remove(CommandArgs.Verbose);
            }
            if (commandArgs.Contains(CommandArgs.Help))
            {
                options.ShowHelp = true;
                commandArgs.Remove(CommandArgs.Help);
            }

            FillDownloadOptionsFromArguments(options.DownloadArguments, commandArgs);
        }

        private static void FillDownloadOptionsFromArguments(DownloadArguments downloadArguments, List<string> commandArgs)
        {
            if (commandArgs.Contains(CommandArgs.SelfUpdate))
            {
                commandArgs.Remove(CommandArgs.SelfUpdate);
                downloadArguments.DoSelfUpdate = true;
            }
            var nugetSourceArg = commandArgs.SingleOrDefault(x => x.StartsWith(CommandArgs.NugetSourceArgPrefix));
            if (nugetSourceArg != null)
            {
                commandArgs.Remove(nugetSourceArg);
                downloadArguments.NugetSource = nugetSourceArg.Substring(CommandArgs.NugetSourceArgPrefix.Length);
            }
            if (commandArgs.Contains(CommandArgs.IgnoreCache))
            {
                commandArgs.Remove(CommandArgs.IgnoreCache);
                downloadArguments.IgnoreCache = true;
            }

            var maxFileAgeArg = commandArgs.SingleOrDefault(x => x.StartsWith(CommandArgs.MaxFileAge, StringComparison.Ordinal));
            if (maxFileAgeArg != null)
            {
                var parts = maxFileAgeArg.Split("=".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    var maxFileAgeInMinutesCommandArg = parts[1];
                    int parsedMaxFileAgeInMinutesCommandArg;
                    if (int.TryParse(maxFileAgeInMinutesCommandArg, out parsedMaxFileAgeInMinutesCommandArg))
                    {
                        downloadArguments.MaxFileAgeInMinutes = parsedMaxFileAgeInMinutesCommandArg;
                    }
                }

                commandArgs.Remove(maxFileAgeArg);
            }

            if (commandArgs.Count >= 1)
            {
                if (commandArgs[0] == CommandArgs.Prerelease)
                {
                    downloadArguments.IgnorePrerelease = false;
                    commandArgs.Remove(CommandArgs.Prerelease);
                }
                else
                {
                    downloadArguments.LatestVersion = commandArgs[0];
                    commandArgs.Remove(commandArgs[0]);
                }
            }
        }

        private static bool GetIsMagicMode(IFileSystemProxy fileSystemProxy)
        {
            // Magic mode is defined by the bootstrapper being renamed 'paket.exe'
            var fileName = Path.GetFileName(fileSystemProxy.GetExecutingAssemblyPath());
            return string.Equals(fileName, "paket.exe", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetHash(string input)
        {
            using (var hash = SHA256.Create())
            {
                return string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(input)).Select(b => b.ToString("X2")));
            }
        }

        private static string GetMagicModeTarget(IFileSystemProxy fileSystemProxy)
        {
            // In magic mode the real 'paket.exe' is downloaded to the system temporary directory with a
            // stable name depending on the bootstrapper location. It give two advantages :
            //  - Multiple executions of the same boostrapper will use the same path and can reuse the
            //    downloaded executable if needed. All of that without needing any additional state as
            //    our location on the file system act as the only state needed.
            //  - There is no risk to have two bootstrapper instances in two different locations
            //    accessing the same file as their path depends on the bootstrapper path.
            var assemblyLocation = fileSystemProxy.GetExecutingAssemblyPath();
            var targetName = String.Format("paket_{0}.exe",GetHash(assemblyLocation));

            return Path.Combine(fileSystemProxy.GetTempPath(), targetName);
        }

        private static bool IsTrue(this NameValueCollection appSettings, string key)
        {
            return appSettings.GetKey(key).ToLowerSafe() == "true";
        }

        private static string GetKey(this NameValueCollection appSettings, string key)
        {
            if (appSettings != null && appSettings.AllKeys.Any(x => x == key))
                return appSettings.Get(key);
            return null;
        }

        private static string GetKey(this IDictionary dictionary, string key)
        {
            if (dictionary != null && dictionary.Keys.Cast<string>().Any(x => x == key))
                return dictionary[key].ToString();
            return null;
        }

        private static string ToLowerSafe(this string value)
        {
            if(value == null)
                return "";
            return value.ToLower();
        }
    }
}
