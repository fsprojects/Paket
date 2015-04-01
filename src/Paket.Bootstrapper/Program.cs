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

        static IWebProxy GetDefaultWebProxyFor(String url)
        {
            IWebProxy result = WebRequest.GetSystemWebProxy();
            Uri uri = new Uri(url);
            Uri address = result.GetProxy(uri);

            if (address == uri)
                return null;

            return new WebProxy(address)
            {
                Credentials = CredentialCache.DefaultCredentials,
                BypassProxyOnLocal = true
            };
        }

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

            StartPaketBootstrapping(effectiveStrategy, dlArgs);
        }

        private static void StartPaketBootstrapping(IDownloadStrategy downloadStrategy, DownloadArguments dlArgs)
        {
            Action<Exception> handleException = exception =>
            {
                if (!File.Exists(dlArgs.Target))
                    Environment.ExitCode = 1;
                WriteConsoleError(String.Format("{0} ({1})", exception.Message, downloadStrategy.Name));
            };
            try
            {
                var localVersion = GetLocalFileVersion(dlArgs.Target);

                var latestVersion = dlArgs.LatestVersion;
                if (latestVersion == "")
                {
                    latestVersion = downloadStrategy.GetLatestVersion(dlArgs.IgnorePrerelease);
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
            catch (Exception exn)
            {
                handleException(exn);
            }
        }

        private static IDownloadStrategy GetEffectiveDownloadStrategy(DownloadArguments dlArgs, bool preferNuget)
        {
            var gitHubDownloadStrategy = new GitHubDownloadStrategy(PrepareWebClient, GetDefaultWebProxyFor);
            var nugetDownloadStrategy = new NugetDownloadStrategy(PrepareWebClient, GetDefaultWebProxyFor, dlArgs.Folder);

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

        private static string GetLocalFileVersion(string target)
        {
            var localVersion = "";

            if (File.Exists(target))
            {
                try
                {
                    FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(target);
                    if (fvi.FileVersion != null)
                        localVersion = fvi.FileVersion;
                }
                catch (Exception)
                {
                }
            }
            return localVersion;
        }

        private static DownloadArguments EvaluateCommandArgs(string[] args)
        {
            var folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var target = Path.Combine(folder, "paket.exe");

            var latestVersion = "";
            var ignorePrerelease = true;

            if (args.Length >= 1)
            {
                if (args[0] == PrereleaseCommandArg)
                {
                    ignorePrerelease = false;
                    Console.WriteLine("Prerelease requested. Looking for latest prerelease.");
                }
                else
                {
                    latestVersion = args[0];
                    Console.WriteLine("Version {0} requested.", latestVersion);
                }
            }
            else Console.WriteLine("No version specified. Downloading latest stable.");


            return new DownloadArguments(latestVersion, ignorePrerelease, folder, target);
        }

        private static void PrepareWebClient(WebClient client, string url)
        {
            client.Headers.Add("user-agent", "Paket.Bootstrapper");
            client.UseDefaultCredentials = true;
            client.Proxy = GetDefaultWebProxyFor(url);
        }

        private static void WriteConsoleError(string message)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ForegroundColor = oldColor;
        }
    }

}