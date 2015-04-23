using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;

namespace Paket.Bootstrapper
{
    class Program
    {
        const string PreferNugetCommandArg = "--prefer-nuget";
        const string PrereleaseCommandArg = "prerelease";
        const string PaketVersionEnv = "PAKET.VERSION";
        const string SelfUpdateCommandArg = "--self";

        static void Main(string[] args)
        {
            var commandArgs = args;
            bool preferNuget = false;
            if (commandArgs.Contains(PreferNugetCommandArg))
            {
                preferNuget = true;
                commandArgs = args.Where(x => x != PreferNugetCommandArg).ToArray();
            }
            var dlArgs = EvaluateCommandArgs(commandArgs);

            var effectiveStrategy = GetEffectiveDownloadStrategy(dlArgs, preferNuget);
            try
            {
                StartPaketBootstrapping(effectiveStrategy, dlArgs);
            }
            catch (RestartException)
            {
                Console.WriteLine("Restarting bootstrapper.");
                ProcessStartInfo s = new ProcessStartInfo();
                s.FileName = Assembly.GetExecutingAssembly().Location;
                s.UseShellExecute = false;
                s.Arguments = String.Join(" ", args.Where(x => x != SelfUpdateCommandArg).Select(x => String.Format("\"{0}\"", x)));
                Process.Start(s);
            }
        }

        private static void StartPaketBootstrapping(IDownloadStrategy downloadStrategy, DownloadArguments dlArgs)
        {
            Action<Exception> handleException = exception =>
            {
                if (!File.Exists(dlArgs.Target))
                    Environment.ExitCode = 1;
                BootstrapperHelper.WriteConsoleError(String.Format("{0} ({1})", exception.Message, downloadStrategy.Name));
            };
            try
            {
                var localVersion = BootstrapperHelper.GetLocalFileVersion(dlArgs.Target);

                var latestVersion = dlArgs.LatestVersion;
                if (latestVersion == "")
                {
                    latestVersion = downloadStrategy.GetLatestVersion(dlArgs.IgnorePrerelease);
                }

                if (dlArgs.DoSelfUpdate)
                {
                    Console.WriteLine("Trying self update");
                    if (downloadStrategy.SelfUpdate(latestVersion))
                        throw new RestartException();
                }
                if (!localVersion.StartsWith(latestVersion))
                {
                    downloadStrategy.DownloadVersion(latestVersion, dlArgs.Target);
                }
                else
                {
                    Console.WriteLine("Paket.exe {0} is up to date.", localVersion);
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
                        Console.WriteLine("'{0}' download failed. Try fallback download from '{1}'.", downloadStrategy.Name, fallbackStrategy.Name);
                        StartPaketBootstrapping(fallbackStrategy, dlArgs);
                        shouldHandleException = !File.Exists(dlArgs.Target);
                    }
                }
                if (shouldHandleException)
                    handleException(exn);
            }
            catch (RestartException)
            {
                throw;
            }
            catch (Exception exn)
            {
                handleException(exn);
            }
        }

        private static IDownloadStrategy GetEffectiveDownloadStrategy(DownloadArguments dlArgs, bool preferNuget)
        {
            var gitHubDownloadStrategy = new GitHubDownloadStrategy(BootstrapperHelper.PrepareWebClient, BootstrapperHelper.GetDefaultWebProxyFor);
            var nugetDownloadStrategy = new NugetDownloadStrategy(BootstrapperHelper.PrepareWebClient, BootstrapperHelper.GetDefaultWebProxyFor, dlArgs.Folder);

            IDownloadStrategy effectiveStrategy;
            if (preferNuget)
            {
                effectiveStrategy = nugetDownloadStrategy;
                nugetDownloadStrategy.FallbackStrategy = gitHubDownloadStrategy;
            }
            else
            {
                effectiveStrategy = gitHubDownloadStrategy;
                gitHubDownloadStrategy.FallbackStrategy = nugetDownloadStrategy;
            }
            return effectiveStrategy;
        }

        private static DownloadArguments EvaluateCommandArgs(string[] args)
        {
            var folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var target = Path.Combine(folder, "paket.exe");

            var latestVersion = Environment.GetEnvironmentVariable(PaketVersionEnv) ?? "";
            var ignorePrerelease = true;
            bool doSelfUpdate = false;

            if (args.Length >= 1)
            {
                if (args[0] == PrereleaseCommandArg)
                {
                    ignorePrerelease = false;
                    latestVersion = "";
                    Console.WriteLine("Prerelease requested. Looking for latest prerelease.");
                }
                else
                {
                    latestVersion = args[0];
                    Console.WriteLine("Version {0} requested.", latestVersion);
                }
                if (args.Contains(SelfUpdateCommandArg))
                {
                    doSelfUpdate = true;
                }
            }
            else if (!String.IsNullOrWhiteSpace(latestVersion))
                Console.WriteLine("Version {0} requested.", latestVersion);
            else Console.WriteLine("No version specified. Downloading latest stable.");


            return new DownloadArguments(latestVersion, ignorePrerelease, folder, target, doSelfUpdate);
        }
    }
}