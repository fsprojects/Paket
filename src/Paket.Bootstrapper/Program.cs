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
        private static readonly Stopwatch executionWatch = new Stopwatch();

        static void Main(string[] args)
        {
            AppContext.SetSwitch("Switch.System.Net.DontEnableSystemDefaultTlsVersions", false);
            executionWatch.Start();
            Console.CancelKeyPress += CancelKeyPressed;

            IProxyProvider proxyProvider = new DefaultProxyProvider();

            var appSettings = ConfigurationManager.AppSettings;

            var appConfigInWorkingDir = Path.Combine(Environment.CurrentDirectory, "paket.bootstrapper.exe.config");
            if (File.Exists(appConfigInWorkingDir))
            {
                var exeInWorkingDir = Path.Combine(Environment.CurrentDirectory, "paket.bootstrapper.exe");
                var exeConf = ConfigurationManager.OpenExeConfiguration(null);
                if (exeConf != null)
                {
                    var nv = new System.Collections.Specialized.NameValueCollection();
                    foreach (KeyValueConfigurationElement kv in exeConf.AppSettings.Settings)
                    {
                        nv.Add(kv.Key, kv.Value);
                    }
                    appSettings = nv;
                }
            }

            var optionsBeforeDependenciesFile = ArgumentParser.ParseArgumentsAndConfigurations(args, appSettings,
                Environment.GetEnvironmentVariables(), proxyProvider.FileSystemProxy, Enumerable.Empty<string>());
            ConsoleImpl.Verbosity = optionsBeforeDependenciesFile.Verbosity;

            var argumentsFromDependenciesFile =
                WindowsProcessArguments.Parse(
                    PaketDependencies.GetBootstrapperArgsForFolder(proxyProvider.FileSystemProxy));
            var options = ArgumentParser.ParseArgumentsAndConfigurations(args, appSettings,
                Environment.GetEnvironmentVariables(), proxyProvider.FileSystemProxy, argumentsFromDependenciesFile);
            if (options.ShowHelp)
            {
                ConsoleImpl.WriteAlways(BootstrapperHelper.HelpText);
                return;
            }

            ConsoleImpl.Verbosity = options.Verbosity;
            if (options.UnprocessedCommandArgs.Any())
                ConsoleImpl.WriteWarning("Ignoring the following unknown argument(s): {0}", String.Join(", ", options.UnprocessedCommandArgs));

#if PAKET_BOOTSTRAP_NO_CACHE
            ConsoleImpl.WriteTrace("Force ignore cache, because not implemented.");
            options.DownloadArguments.IgnoreCache = true;
#endif

            var effectiveStrategy = GetEffectiveDownloadStrategy(options.DownloadArguments, options.PreferNuget, options.ForceNuget, proxyProvider);
            ConsoleImpl.WriteTrace("Using strategy: " + effectiveStrategy.Name);
            ConsoleImpl.WriteTrace("Using install kind: " + (options.DownloadArguments.AsTool? "tool": "exe"));

            StartPaketBootstrapping(effectiveStrategy, options.DownloadArguments, proxyProvider.FileSystemProxy, () => OnSuccessfulDownload(options));
        }

        private static void OnSuccessfulDownload(BootstrapperOptions options)
        {
            if (options.Run && File.Exists(options.DownloadArguments.Target))
            {
                Console.CancelKeyPress -= CancelKeyPressed;
                try
                {
                    var exitCode = PaketRunner.Run(options.DownloadArguments.Target, options.RunArgs);
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
                var localVersion = BootstrapperHelper.GetLocalFileVersion(target, new FileSystemProxy());
                Console.WriteLine("Detected existing paket.exe ({0}). Cancelling normally.", localVersion);
                exitCode = 0;
            }
            Environment.Exit(exitCode);
        }

        public static void StartPaketBootstrapping(IDownloadStrategy downloadStrategy, DownloadArguments dlArgs, IFileSystemProxy fileSystemProxy, Action onSuccess)
        {
            Action<Exception> handleException = exception =>
            {
#if DEBUG
                Environment.ExitCode = 1;
                ConsoleImpl.WriteError(String.Format("{0} ({1})", exception.ToString(), downloadStrategy.Name));
                return;
#else
                ConsoleImpl.WriteError(String.Format("{0} ({1})", exception.Message, downloadStrategy.Name));
                if (!fileSystemProxy.FileExists(dlArgs.Target))
                {
                    Environment.ExitCode = 1;
                }
                else
                {
                    fileSystemProxy.WaitForFileFinished(dlArgs.Target);
                    onSuccess();
                }
#endif
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

                ConsoleImpl.WriteInfo("Checking Paket version ({0})...", versionRequested);
                ConsoleImpl.WriteTrace("Target path is {0}", dlArgs.Target);
                var localVersion = fileSystemProxy.GetLocalFileVersion(dlArgs.Target);
                ConsoleImpl.WriteTrace("File in target path version: v{0}", string.IsNullOrEmpty(localVersion) ? "UNKNOWN" : localVersion);

                var specificVersionRequested = true;
                var latestVersion = dlArgs.LatestVersion;

                if (latestVersion == string.Empty)
                {
                    ConsoleImpl.WriteTrace("No version specified, checking online...");

                    var getLatestVersionWatch = Stopwatch.StartNew();
                    latestVersion = downloadStrategy.GetLatestVersion(dlArgs.IgnorePrerelease);
                    getLatestVersionWatch.Stop();

                    ConsoleImpl.WriteTrace("Latest version check found v{0} in {1:0.##} second(s)", latestVersion, getLatestVersionWatch.Elapsed.TotalSeconds);
                    specificVersionRequested = false;
                }

                if (dlArgs.DoSelfUpdate)
                {
                    ConsoleImpl.WriteInfo("Trying self update");
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
                        PaketHashFile hashFile = null;
                        if (downloadStrategy.CanDownloadHashFile)
                        {
                            ConsoleImpl.WriteTrace("Downloading hash for v{0} ...", latestVersion);
                            var downloadHashWatch = Stopwatch.StartNew();
                            hashFile = downloadStrategy.DownloadHashFile(latestVersion);
                            downloadHashWatch.Stop();

                            ConsoleImpl.WriteTrace("Hash download took {0:0.##} second(s)", downloadHashWatch.Elapsed.TotalSeconds);
                        }

                        ConsoleImpl.WriteTrace("Downloading v{0} ...", latestVersion);

                        var downloadWatch = Stopwatch.StartNew();
                        downloadStrategy.DownloadVersion(latestVersion, dlArgs.Target, hashFile);
                        downloadWatch.Stop();

                        ConsoleImpl.WriteTrace("Download took {0:0.##} second(s)", downloadWatch.Elapsed.TotalSeconds);
                        ConsoleImpl.WriteInfo("Done in {0:0.##} second(s).", executionWatch.Elapsed.TotalSeconds);
                    }
                    else
                    {
                        ConsoleImpl.WriteInfo("Paket.exe {0} is up to date.", localVersion);
                    }
                }

                executionWatch.Stop();
                ConsoleImpl.WriteTrace("Paket Bootstrapping took {0:0.##} second(s)", executionWatch.Elapsed.TotalSeconds);

                onSuccess();
            }
            catch (WebException exn)
            {
                var shouldHandleException = true;
                if (!fileSystemProxy.FileExists(dlArgs.Target))
                {
                    if (downloadStrategy.FallbackStrategy != null)
                    {
                        var fallbackStrategy = downloadStrategy.FallbackStrategy;
                        ConsoleImpl.WriteInfo("'{0}' download failed. If using Mono, you may need to import trusted certificates using the 'mozroots' tool as none are contained by default. Trying fallback download from '{1}'.", downloadStrategy.Name, fallbackStrategy.Name);
                        StartPaketBootstrapping(fallbackStrategy, dlArgs, fileSystemProxy, onSuccess);
                        shouldHandleException = !fileSystemProxy.FileExists(dlArgs.Target);
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

        public static DownloadStrategy GetEffectiveDownloadStrategy(DownloadArguments dlArgs, bool preferNuget, bool forceNuget, IProxyProvider proxyProvider)
        {
            var gitHubDownloadStrategy =
                dlArgs.AsTool
                ? new GitHubDownloadToolStrategy(proxyProvider.WebRequestProxy, proxyProvider.FileSystemProxy).AsCached(dlArgs.IgnoreCache)
                : new GitHubDownloadStrategy(proxyProvider.WebRequestProxy, proxyProvider.FileSystemProxy).AsCached(dlArgs.IgnoreCache);
            var nugetDownloadStrategy = new NugetDownloadStrategy(proxyProvider.WebRequestProxy, proxyProvider.FileSystemProxy, dlArgs.Folder, dlArgs.NugetSource, dlArgs.AsTool).AsCached(dlArgs.IgnoreCache);

            DownloadStrategy effectiveStrategy;
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

        private static DownloadStrategy AsCached(this DownloadStrategy effectiveStrategy, bool ignoreCache)
        {
            if (ignoreCache)
                return effectiveStrategy;
            return new CacheDownloadStrategy(effectiveStrategy, new FileSystemProxy());
        }

        private static DownloadStrategy AsTemporarilyIgnored(this DownloadStrategy effectiveStrategy, int? maxFileAgeInMinutes, string target)
        {
            if (maxFileAgeInMinutes.HasValue)
                return new TemporarilyIgnoreUpdatesDownloadStrategy(effectiveStrategy, new FileSystemProxy(), target, maxFileAgeInMinutes.Value);
            return effectiveStrategy;
        }
    }
}
