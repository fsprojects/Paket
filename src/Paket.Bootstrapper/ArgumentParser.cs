using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Paket.Bootstrapper
{
    public static class ArgumentParser
    {
        public static class CommandArgs
        {
            public const string PreferNuget = "--prefer-nuget";
            public const string ForceNuget = "--force-nuget";
            public const string Prerelease = "prerelease";
            public const string NugetSourceArgPrefix = "--nuget-source=";
            public const string SelfUpdate = "--self";
            public const string Silent = "-s";
            public const string IgnoreCache = "-f";
        }
        public static class AppSettingKeys
        {
            public const string PreferNugetAppSettingsKey = "PreferNuget";
            public const string ForceNugetAppSettingsKey = "ForceNuget";
            public const string PaketVersionAppSettingsKey = "PaketVersion";
        }
        public static class EnvArgs
        {
            public const string PaketVersionEnv = "PAKET.VERSION";
        }

        public static BootstrapperOptions ParseArgumentsAndConfigurations(IEnumerable<string> arguments, NameValueCollection appSettings, IDictionary envVariables)
        {
            var options = new BootstrapperOptions();
            
            var commandArgs = arguments.ToList();

            if (commandArgs.Contains(CommandArgs.PreferNuget))
            {
                options.PreferNuget = true;
                commandArgs.Remove(CommandArgs.PreferNuget);
            }
            else if (appSettings.GetKey(AppSettingKeys.PreferNugetAppSettingsKey) == "true")
            {
                options.PreferNuget = true;
            }
            if (commandArgs.Contains(CommandArgs.ForceNuget))
            {
                options.ForceNuget = true;
                commandArgs.Remove(CommandArgs.ForceNuget);
            }
            else if (appSettings.GetKey(AppSettingKeys.ForceNugetAppSettingsKey) == "true")
            {
                options.ForceNuget = true;
            }
            if (commandArgs.Contains(CommandArgs.Silent))
            {
                options.Silent = true;
                commandArgs.Remove(CommandArgs.Silent);
            }

            commandArgs = EvaluateDownloadOptions(options.DownloadArguments, commandArgs, appSettings, envVariables).ToList();

            options.UnprocessedCommandArgs = commandArgs;
            return options;
        }

        private static IEnumerable<string> EvaluateDownloadOptions(DownloadArguments downloadArguments, IEnumerable<string> args, NameValueCollection appSettings, IDictionary envVariables)
        {
            var folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var target = Path.Combine(folder, "paket.exe");
            string nugetSource = null;

            var latestVersion = appSettings.GetKey(AppSettingKeys.PaketVersionAppSettingsKey) ?? envVariables.GetKey(EnvArgs.PaketVersionEnv) ?? String.Empty;
            var ignorePrerelease = true;
            bool doSelfUpdate = false;
            var ignoreCache = false;
            var commandArgs = args.ToList();

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
            if (commandArgs.Count >= 1)
            {
                if (commandArgs[0] == CommandArgs.Prerelease)
                {
                    ignorePrerelease = false;
                    latestVersion = String.Empty;
                }
                else
                {
                    latestVersion = commandArgs[0];
                }
            }

            downloadArguments.LatestVersion = latestVersion;
            downloadArguments.IgnorePrerelease = ignorePrerelease;
            downloadArguments.IgnoreCache = ignoreCache;
            downloadArguments.NugetSource = nugetSource;
            downloadArguments.DoSelfUpdate = doSelfUpdate;
            downloadArguments.Target = target;
            return commandArgs;
        }

        private static string GetKey(this NameValueCollection appSettings, string key)
        {
            return null;
        }

        private static string GetKey(this IDictionary dictionary, string key)
        {
            return null;
        }
    }

    public class BootstrapperOptions
    {
        public BootstrapperOptions()
        {
            DownloadArguments = new DownloadArguments();
        }

        public DownloadArguments DownloadArguments { get; set; }

        public bool Silent { get; set; }
        public bool ForceNuget { get; set; }
        public bool PreferNuget { get; set; }
        public IEnumerable<string> UnprocessedCommandArgs { get; set; }
    }
}
