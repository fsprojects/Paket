using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

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
            public const string IgnoreCache = "-f";
            public const string MaxFileAge = "--max-file-age=";
            public const string Run = "--run";
        }
        public static class AppSettingKeys
        {
            public const string PreferNugetAppSettingsKey = "PreferNuget";
            public const string ForceNugetAppSettingsKey = "ForceNuget";
            public const string PaketVersionAppSettingsKey = "PaketVersion";
            public const string PrereleaseAppSettingsKey = "Prerelease";
        }
        public static class EnvArgs
        {
            public const string PaketVersionEnv = "PAKET.VERSION";
        }

        public static BootstrapperOptions ParseArgumentsAndConfigurations(IEnumerable<string> arguments, NameValueCollection appSettings, IDictionary envVariables, bool magicMode)
        {
            var options = new BootstrapperOptions();

            var commandArgs = arguments.ToList();

            ApplyAppSettings(appSettings, options);

            var runIndex = commandArgs.IndexOf(CommandArgs.Run);
            if (magicMode && runIndex == -1)
            {
                options.Silent = true;
                options.Run = true;
                options.RunArgs = commandArgs;
                EvaluateDownloadOptions(options.DownloadArguments, new string[0], appSettings, envVariables, true);
                options.DownloadArguments.MaxFileAgeInMinutes = 60*12;
                return options;
            }

            if (runIndex != -1)
            {
                options.Run = true;
                options.RunArgs = commandArgs.GetRange(runIndex + 1, commandArgs.Count - runIndex - 1);
                commandArgs.RemoveRange(runIndex, commandArgs.Count - runIndex);
            }

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
            if (commandArgs.Contains(CommandArgs.Silent))
            {
                options.Silent = true;
                commandArgs.Remove(CommandArgs.Silent);
            }
            if (commandArgs.Contains(CommandArgs.Help))
            {
                options.ShowHelp = true;
                commandArgs.Remove(CommandArgs.Help);
            }

            commandArgs = EvaluateDownloadOptions(options.DownloadArguments, commandArgs, appSettings, envVariables, magicMode).ToList();

            options.UnprocessedCommandArgs = commandArgs;
            return options;
        }

        private static void ApplyAppSettings(NameValueCollection appSettings, BootstrapperOptions options)
        {
            if (appSettings.GetKey(AppSettingKeys.PreferNugetAppSettingsKey).ToLowerSafe() == "true")
            {
                options.PreferNuget = true;
            }
            if (appSettings.GetKey(AppSettingKeys.ForceNugetAppSettingsKey).ToLowerSafe() == "true")
            {
                options.ForceNuget = true;
            }
        }

        static string GetHash(string input)
        {
            using (var hash = SHA256.Create())
            {
                return string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(input)).Select(b => b.ToString("X2")));
            }
        }

        static string GetMagicModeTarget()
        {
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            var targetName = $"paket_{GetHash(assemblyLocation)}.exe";

            return Path.Combine(Path.GetTempPath(), targetName);
        }

        private static IEnumerable<string> EvaluateDownloadOptions(DownloadArguments downloadArguments, IEnumerable<string> args, NameValueCollection appSettings, IDictionary envVariables, bool magicMode)
        {
            var folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var target = magicMode ? GetMagicModeTarget() : Path.Combine(folder, "paket.exe");
            string nugetSource = downloadArguments.NugetSource;

            var latestVersion = appSettings.GetKey(AppSettingKeys.PaketVersionAppSettingsKey) ?? envVariables.GetKey(EnvArgs.PaketVersionEnv) ?? downloadArguments.LatestVersion;
            var prerelease = (appSettings.GetKey(AppSettingKeys.PrereleaseAppSettingsKey).ToLowerSafe() == "true" &&
                              string.IsNullOrEmpty(latestVersion)) || !downloadArguments.IgnorePrerelease;
            bool doSelfUpdate = downloadArguments.DoSelfUpdate;
            var ignoreCache = downloadArguments.IgnoreCache;
            var commandArgs = args.ToList();
            int? maxFileAgeInMinutes = downloadArguments.MaxFileAgeInMinutes;

            if (commandArgs.Contains(CommandArgs.SelfUpdate))
            {
                commandArgs.Remove(CommandArgs.SelfUpdate);
                doSelfUpdate = true;
            }
            var nugetSourceArg = commandArgs.SingleOrDefault(x => x.StartsWith(CommandArgs.NugetSourceArgPrefix));
            if (nugetSourceArg != null)
            {
                commandArgs = commandArgs.Where(x => !x.StartsWith(CommandArgs.NugetSourceArgPrefix)).ToList();
                nugetSource = nugetSourceArg.Substring(CommandArgs.NugetSourceArgPrefix.Length);
            }
            if (commandArgs.Contains(CommandArgs.IgnoreCache))
            {
                commandArgs.Remove(CommandArgs.IgnoreCache);
                ignoreCache = true;
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
                        maxFileAgeInMinutes = parsedMaxFileAgeInMinutesCommandArg;
                }

                commandArgs.Remove(maxFileAgeArg);
            }

            if (commandArgs.Count >= 1)
            {
                if (commandArgs[0] == CommandArgs.Prerelease)
                {
                    prerelease = true;
                    latestVersion = String.Empty;
                    commandArgs.Remove(CommandArgs.Prerelease);
                }
                else
                {
                    prerelease = false;
                    latestVersion = commandArgs[0];
                    commandArgs.Remove(commandArgs[0]);
                }
            }

            downloadArguments.LatestVersion = latestVersion;
            downloadArguments.IgnorePrerelease = !prerelease;
            downloadArguments.IgnoreCache = ignoreCache;
            downloadArguments.NugetSource = nugetSource;
            downloadArguments.DoSelfUpdate = doSelfUpdate;
            downloadArguments.Target = target;
            downloadArguments.Folder = Path.GetDirectoryName(target);
            downloadArguments.MaxFileAgeInMinutes = maxFileAgeInMinutes;
            return commandArgs;
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
            if (value != null)
                return value.ToLower();
            return null;
        }
    }
}
