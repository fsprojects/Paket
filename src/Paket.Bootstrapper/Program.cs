using System;
using System.Configuration;
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
    class Program
    {
        
        static void Main(string[] args)
        {
            Console.CancelKeyPress += CancelKeyPressed;

            var options = ArgumentParser.ParseArgumentsAndConfigurations(args, ConfigurationManager.AppSettings, Environment.GetEnvironmentVariables());
            if (!options.Silent && options.UnprocessedCommandArgs.Any())
                Console.WriteLine("Ignoring the following unknown arguments: {0}", String.Join(", ", options.UnprocessedCommandArgs));

            var effectiveStrategy = GetEffectiveDownloadStrategy(options.DownloadArguments, options.PreferNuget, options.ForceNuget);

            StartPaketBootstrapping(effectiveStrategy, options.DownloadArguments, options.Silent);
        }

        private static void CancelKeyPressed(object sender, ConsoleCancelEventArgs e)
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

        private static void StartPaketBootstrapping(IDownloadStrategy downloadStrategy, DownloadArguments dlArgs, bool silent)
        {
            Action<Exception> handleException = exception =>
            {
                if (!File.Exists(dlArgs.Target))
                    Environment.ExitCode = 1;
                BootstrapperHelper.WriteConsoleError(String.Format("{0} ({1})", exception.Message, downloadStrategy.Name));
            };
            try
            {
                if (!silent)
                {
                    string versionRequested;
                    if (!dlArgs.IgnorePrerelease)
                        versionRequested = "prerelease requested";
                    else if (String.IsNullOrWhiteSpace(dlArgs.LatestVersion))
                        versionRequested = "downloading latest stable";
                    else
                        versionRequested = string.Format("version {0} requested", dlArgs.LatestVersion);

                    Console.WriteLine("Checking Paket version ({0})...", versionRequested);
                }

                var localVersion = BootstrapperHelper.GetLocalFileVersion(dlArgs.Target);

                var specificVersionRequested = true;
                var latestVersion = dlArgs.LatestVersion;

                if (latestVersion == String.Empty)
                {
                    latestVersion = downloadStrategy.GetLatestVersion(dlArgs.IgnorePrerelease, silent);
                    specificVersionRequested = false;
                }

                if (dlArgs.DoSelfUpdate)
                {
                    if (!silent)
                        Console.WriteLine("Trying self update");
                    downloadStrategy.SelfUpdate(latestVersion, silent);
                }
                else
                {
                    var currentSemVer = String.IsNullOrEmpty(localVersion) ? new SemVer() : SemVer.Create(localVersion);
                    var latestSemVer = SemVer.Create(latestVersion);
                    var comparison = currentSemVer.CompareTo(latestSemVer);

                    if ((comparison > 0 && specificVersionRequested) || comparison < 0)
                    {
                        downloadStrategy.DownloadVersion(latestVersion, dlArgs.Target, silent);
                        if (!silent)
                            Console.WriteLine("Done.");
                    }
                    else
                    {
                        if (!silent)
                            Console.WriteLine("Paket.exe {0} is up to date.", localVersion);
                    }
                }
            }
            catch (WebException exn)
            {
                var shouldHandleException = true;
                if (!File.Exists(dlArgs.Target))
                {
                    if (downloadStrategy.FallbackStrategy != null)
                    {
                        var fallbackStrategy = downloadStrategy.FallbackStrategy;
                        if (!silent)
                            Console.WriteLine("'{0}' download failed. If using Mono, you may need to import trusted certificates using the 'mozroots' tool as none are contained by default. Trying fallback download from '{1}'.", 
                                downloadStrategy.Name, fallbackStrategy.Name);
                        StartPaketBootstrapping(fallbackStrategy, dlArgs, silent);
                        shouldHandleException = !File.Exists(dlArgs.Target);
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
            var gitHubDownloadStrategy = new GitHubDownloadStrategy(new WebRequestProxy(), new FileProxy());
            var nugetDownloadStrategy = new NugetDownloadStrategy(new WebRequestProxy(), new DirectoryProxy(), new FileProxy(), dlArgs.Folder, dlArgs.NugetSource);

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

            return dlArgs.IgnoreCache ? effectiveStrategy : new CacheDownloadStrategy(effectiveStrategy, new DirectoryProxy(), new FileProxy());
        }

    }
}
