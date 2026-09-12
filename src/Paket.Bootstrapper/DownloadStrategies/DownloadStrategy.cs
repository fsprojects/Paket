using System;
using System.Diagnostics;

namespace Paket.Bootstrapper.DownloadStrategies
{
    public abstract class DownloadStrategy : IDownloadStrategy
    {
        public abstract string Name { get; }
        public abstract bool CanDownloadHashFile { get; }

        /// <summary>
        /// Paket 12.0 and later are published as a .NET tool only: their releases carry no
        /// paket.exe asset and their NuGet package no longer holds tools/paket.exe. Resolving one
        /// would end in a 404, then a FileNotFoundException on the NuGet fallback, and an exit
        /// code of 1 wherever paket.exe isn't already on disk. Stay on the last release we can
        /// actually download instead.
        /// </summary>
        internal const int LastSupportedMajorVersion = 11;

        internal const string LastSupportedVersion = "11.0.0";

        public IDownloadStrategy FallbackStrategy { get; set; }
        public string GetLatestVersion(bool ignorePrerelease)
        {
            var version = Wrap(() => GetLatestVersionCore(ignorePrerelease), "GetLatestVersion");
            return CapToLastSupportedVersion(version);
        }

        /// <summary>
        /// Applied here rather than in the individual strategies because this method is the single
        /// non-virtual entry point every strategy goes through, and the decorating strategies call
        /// it on the strategy they wrap. That also covers the version sources that never touch the
        /// network: the on-disk cache, the --max-file-age fast path and a local NuGet folder.
        /// </summary>
        internal static string CapToLastSupportedVersion(string version)
        {
            if (String.IsNullOrWhiteSpace(version))
                return version;

            SemVer parsed;
            try
            {
                parsed = SemVer.Create(version);
            }
            catch (Exception)
            {
                // Never let an unparseable version break the bootstrapper; let the caller deal
                // with it as it did before.
                return version;
            }

            if (parsed.Major <= LastSupportedMajorVersion)
                return version;

            ConsoleImpl.WriteAlways(
                "Paket {0} is available, but it is published as a .NET tool only and cannot be downloaded by the bootstrapper. Staying on {1}. To move on, run: dotnet tool install paket",
                version, LastSupportedVersion);

            return LastSupportedVersion;
        }

        public void DownloadVersion(string latestVersion, string target, PaketHashFile hashfile)
        {
            Wrap(() => DownloadVersionCore(latestVersion, target, hashfile), "DownloadVersion");
        }

        public void SelfUpdate(string latestVersion)
        {
            Wrap(() => SelfUpdateCore(latestVersion), "SelfUpdate");
        }

        public PaketHashFile DownloadHashFile(string latestVersion)
        {
            return Wrap(() => DownloadHashFileCore(latestVersion), "DownloadHashFile");
        }

        protected abstract string GetLatestVersionCore(bool ignorePrerelease);
        protected abstract void DownloadVersionCore(string latestVersion, string target, PaketHashFile hashfile);
        protected abstract void SelfUpdateCore(string latestVersion);
        protected abstract PaketHashFile DownloadHashFileCore(string latestVersion);

        private void Wrap(Action action, string actionName)
        {
            if (!ConsoleImpl.IsTraceEnabled)
            {
                action();
                return;
            }

            Wrap(() => {
                action();
                return "void";
            }, actionName);
        }

        private TResult Wrap<TResult>(Func<TResult> func, string actionName)
        {
            if (!ConsoleImpl.IsTraceEnabled)
            {
                return func();
            }

            ConsoleImpl.WriteTrace("[{0}] {1}...", Name, actionName);
            var watch = Stopwatch.StartNew();
            try
            {
                var result = func();
                watch.Stop();
                ConsoleImpl.WriteTrace("[{0}] {1} took {2:0.##} second(s) and returned '{3}'.", Name, actionName, watch.Elapsed.TotalSeconds, result);
                return result;
            }
            catch (Exception exception)
            {
                watch.Stop();
                ConsoleImpl.WriteTrace("[{0}] {1} took {2:0.##} second(s) and failed with '{3}'.", Name, actionName, watch.Elapsed.TotalSeconds, exception.Message);
                throw;
            }
        }
    }
}