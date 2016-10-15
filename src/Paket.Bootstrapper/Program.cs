using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using Paket.Bootstrapper.DownloadStrategies;
using Paket.Bootstrapper.HelperProxies;

[assembly: InternalsVisibleTo("Paket.Bootstrapper.Tests")]

namespace Paket.Bootstrapper
{
    static class Program
    {
        private static readonly ConsoleRunner ConsoleRunner = new ConsoleRunner();

        static bool GetIsMagicMode()
        {
            var fileName = Path.GetFileName(Assembly.GetExecutingAssembly().Location);
            return string.Equals(fileName, "paket.exe", StringComparison.OrdinalIgnoreCase);
        }

        static void Main(string[] args)
        {
            Console.CancelKeyPress += CancelKeyPressed;

            var magicMode = GetIsMagicMode();
            var options = ArgumentParser.ParseArgumentsAndConfigurations(args, ConfigurationManager.AppSettings,
                Environment.GetEnvironmentVariables(), magicMode);
            if (options.ShowHelp)
            {
                ConsoleImpl.WriteDebug(BootstrapperHelper.HelpText);
                return;
            }

            ConsoleImpl.IsSilent = options.Silent;
            if (options.UnprocessedCommandArgs.Any())
                ConsoleImpl.WriteInfo("Ignoring the following unknown argument(s): {0}", String.Join(", ", options.UnprocessedCommandArgs));

            var effectiveStrategy = GetEffectiveDownloadStrategy(options.DownloadArguments, options.PreferNuget, options.ForceNuget);

            StartPaketBootstrapping(effectiveStrategy, options.DownloadArguments, new FileProxy(), () => OnSuccessfulDownload(options));
        }

        private static void OnSuccessfulDownload(BootstrapperOptions options)
        {
            if (options.Run && File.Exists(options.DownloadArguments.Target))
            {
                Console.CancelKeyPress -= CancelKeyPressed;
                try
                {
                    var exitCode = ConsoleRunner.Run(options.DownloadArguments.Target, options.RunArgs);
                    Environment.Exit(exitCode);
                }
                catch (Exception e)
                {
                    ConsoleImpl.WriteError("Running paket failed with: {0}", e);
                }
            }
        }

        private static void CancelKeyPressed(object o, ConsoleCancelEventArgs eventArgs)
        {
            Console.WriteLine("Bootstrapper cancelled");
            var folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var target = Path.Combine(folder, "paket.exe");

            var exitCode = 1;
            if (File.Exists(target))
            {
                var localVersion = BootstrapperHelper.GetLocalFileVersion(target);
                Console.WriteLine("Detected existing paket.exe ({0}). Cancelling normally.", localVersion);
                exitCode = 0;
            }
            Environment.Exit(exitCode);
        }

        public static void StartPaketBootstrapping(IDownloadStrategy downloadStrategy, DownloadArguments dlArgs, IFileProxy fileProxy, Action onSuccess)
        {
            Action<Exception> handleException = exception =>
            {
                if (!fileProxy.Exists(dlArgs.Target))
                    Environment.ExitCode = 1;
                ConsoleImpl.WriteError(String.Format("{0} ({1})", exception.Message, downloadStrategy.Name));
            };
            try
            {
                string versionRequested;
                if (!dlArgs.IgnorePrerelease)
                    versionRequested = "prerelease requested";
                else if (String.IsNullOrWhiteSpace(dlArgs.LatestVersion))
                    versionRequested = "downloading latest stable";
                else
                    versionRequested = string.Format("version {0} requested", dlArgs.LatestVersion);

                ConsoleImpl.WriteDebug("Checking Paket version ({0})...", versionRequested);

                var localVersion = fileProxy.GetLocalFileVersion(dlArgs.Target);

                var specificVersionRequested = true;
                var latestVersion = dlArgs.LatestVersion;

                if (latestVersion == String.Empty)
                {
                    latestVersion = downloadStrategy.GetLatestVersion(dlArgs.IgnorePrerelease);
                    specificVersionRequested = false;
                }

                if (dlArgs.DoSelfUpdate)
                {
                    ConsoleImpl.WriteDebug("Trying self update");
                    downloadStrategy.SelfUpdate(latestVersion);
                }
                else
                {
                    var currentSemVer = String.IsNullOrEmpty(localVersion) ? new SemVer() : SemVer.Create(localVersion);
                    if (currentSemVer.PreRelease != null && dlArgs.IgnorePrerelease)
                        currentSemVer = new SemVer();
                    var latestSemVer = SemVer.Create(latestVersion);
                    var comparison = currentSemVer.CompareTo(latestSemVer);

                    if ((comparison > 0 && specificVersionRequested) || comparison < 0)
                    {
                        downloadStrategy.DownloadVersion(latestVersion, dlArgs.Target);
                        ConsoleImpl.WriteDebug("Done.");
                    }
                    else
                    {
                        ConsoleImpl.WriteDebug("Paket.exe {0} is up to date.", localVersion);
                    }
                }

                onSuccess();
            }
            catch (WebException exn)
            {
                var shouldHandleException = true;
                if (!fileProxy.Exists(dlArgs.Target))
                {
                    if (downloadStrategy.FallbackStrategy != null)
                    {
                        var fallbackStrategy = downloadStrategy.FallbackStrategy;
                        ConsoleImpl.WriteDebug("'{0}' download failed. If using Mono, you may need to import trusted certificates using the 'mozroots' tool as none are contained by default. Trying fallback download from '{1}'.", downloadStrategy.Name, fallbackStrategy.Name);
                        StartPaketBootstrapping(fallbackStrategy, dlArgs, fileProxy, onSuccess);
                        shouldHandleException = !fileProxy.Exists(dlArgs.Target);
                    }
                }
                if (shouldHandleException)
                    handleException(exn);
            }
            catch (Exception exn)
            {
                handleException(exn);
            }
        }

        public static IDownloadStrategy GetEffectiveDownloadStrategy(DownloadArguments dlArgs, bool preferNuget, bool forceNuget)
        {
            var gitHubDownloadStrategy = new GitHubDownloadStrategy(new WebRequestProxy(), new FileProxy()).AsCached(dlArgs.IgnoreCache);
            var nugetDownloadStrategy = new NugetDownloadStrategy(new WebRequestProxy(), new DirectoryProxy(), new FileProxy(), dlArgs.Folder, dlArgs.NugetSource).AsCached(dlArgs.IgnoreCache);

            IDownloadStrategy effectiveStrategy;
            if (forceNuget)
            {
                effectiveStrategy = nugetDownloadStrategy;
                nugetDownloadStrategy.FallbackStrategy = null;
            }
            else if (preferNuget)
            {
                effectiveStrategy = nugetDownloadStrategy;
                nugetDownloadStrategy.FallbackStrategy = gitHubDownloadStrategy;
            }
            else
            {
                effectiveStrategy = gitHubDownloadStrategy;
                gitHubDownloadStrategy.FallbackStrategy = nugetDownloadStrategy;
            }

            return effectiveStrategy.AsTemporarilyIgnored(dlArgs.MaxFileAgeInMinutes, dlArgs.Target);
        }

        private static IDownloadStrategy AsCached(this IDownloadStrategy effectiveStrategy, bool ignoreCache)
        {
            if (ignoreCache)
                return effectiveStrategy;
            return new CacheDownloadStrategy(effectiveStrategy, new DirectoryProxy(), new FileProxy());
        }

        private static IDownloadStrategy AsTemporarilyIgnored(this IDownloadStrategy effectiveStrategy, int? maxFileAgeInMinutes, string target)
        {
            if (maxFileAgeInMinutes.HasValue)
                return new TemporarilyIgnoreUpdatesDownloadStrategy(effectiveStrategy, new FileProxy(), target, maxFileAgeInMinutes.Value);
            return effectiveStrategy;
        }
    }
}
