using System;
using System.IO;
using System.Linq;
using System.Net;

namespace Paket.Bootstrapper
{
    internal class CacheDownloadStrategy : IDownloadStrategy
    {
        public string Name { get { return "Cache"; } }

        private readonly string _paketCacheDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NuGet", "Cache", "Paket");

        private IDownloadStrategy _fallbackStrategy;
        public IDownloadStrategy FallbackStrategy
        {
            get
            {
                return _fallbackStrategy;
            }
            private set
            {
                if (value == null)
                    throw new ArgumentException("CacheDownloadStrategy needs a non-null FallbackStrategy");
                _fallbackStrategy = value;
            }
        }

        public IDirectoryProxy DirectoryProxy { get; set; }
        public IFileProxy FileProxy { get; set; }

        public CacheDownloadStrategy(IDownloadStrategy fallbackStrategy, IDirectoryProxy directoryProxy, IFileProxy fileProxy)
        {
            FallbackStrategy = fallbackStrategy;
            DirectoryProxy = directoryProxy;
            FileProxy = fileProxy;
        }

        public string GetLatestVersion(bool ignorePrerelease, bool silent)
        {
            try
            {
                return FallbackStrategy.GetLatestVersion(ignorePrerelease, silent);
            }
            catch (WebException)
            {
                if (FallbackStrategy.FallbackStrategy != null)
                {
                    FallbackStrategy = FallbackStrategy.FallbackStrategy;
                    return GetLatestVersion(ignorePrerelease, silent);
                }

                var latestVersion = GetLatestVersionInCache(ignorePrerelease);

                if (!silent)
                    Console.WriteLine("Unable to look up the latest version online, the cache contains version {0}.", latestVersion);

                return latestVersion;
            }
        }

        public void DownloadVersion(string latestVersion, string target, bool silent)
        {
            var cached = Path.Combine(_paketCacheDir, latestVersion, "paket.exe");

            if (!FileProxy.Exists(cached))
            {
                if (!silent)
                    Console.WriteLine("Version {0} not found in cache.", latestVersion);

                FallbackStrategy.DownloadVersion(latestVersion, target, silent);
                DirectoryProxy.CreateDirectory(Path.GetDirectoryName(cached));
                FileProxy.Copy(target, cached);
            }
            else
            {
                if (!silent)
                    Console.WriteLine("Copying version {0} from cache.", latestVersion);

                FileProxy.Copy(cached, target, true);
            }
        }

        public void SelfUpdate(string latestVersion, bool silent)
        {
            FallbackStrategy.SelfUpdate(latestVersion, silent);
        }

        private string GetLatestVersionInCache(bool ignorePrerelease)
        {
            DirectoryProxy.CreateDirectory(_paketCacheDir);
            var zero = new SemVer();

            return DirectoryProxy.GetDirectories(_paketCacheDir)
                .Select(Path.GetFileName)
                .OrderByDescending(x =>
                {
                    try
                    {
                        var version = SemVer.Create(x);

                        if (ignorePrerelease && version.PreRelease != null)
                            return zero;
                        else
                            return version;
                    }
                    catch (Exception)
                    {
                        return zero;
                    }
                })
                .FirstOrDefault() ?? "0";
        }
    }
}