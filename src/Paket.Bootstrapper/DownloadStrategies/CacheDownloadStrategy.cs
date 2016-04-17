using System;
using System.IO;
using System.Linq;
using System.Net;
using Paket.Bootstrapper.HelperProxies;

namespace Paket.Bootstrapper.DownloadStrategies
{
    internal class CacheDownloadStrategy : ICachedDownloadStrategy
    {
        public string Name { get { return String.Format("{0} - cached", EffectiveStrategy.Name); } }
        public IDownloadStrategy FallbackStrategy { get; set; }

        private readonly string _paketCacheDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NuGet", "Cache", "Paket");

        public IDownloadStrategy EffectiveStrategy { get; set; }
        public IDirectoryProxy DirectoryProxy { get; set; }
        public IFileProxy FileProxy { get; set; }

        public CacheDownloadStrategy(IDownloadStrategy effectiveStrategy, IDirectoryProxy directoryProxy, IFileProxy fileProxy)
        {
            if (effectiveStrategy == null)
                throw new ArgumentException("CacheDownloadStrategy needs a non-null effective strategy");
            if (effectiveStrategy.FallbackStrategy != null)
                throw new ArgumentException("CacheDownloadStrategy should not have a fallback strategy");

            EffectiveStrategy = effectiveStrategy;
            DirectoryProxy = directoryProxy;
            FileProxy = fileProxy;
        }


        public string GetLatestVersion(bool ignorePrerelease)
        {
            try
            {
                return EffectiveStrategy.GetLatestVersion(ignorePrerelease);
            }
            catch (WebException)
            {
                if (FallbackStrategy == null)
                {
                    var latestVersion = GetLatestVersionInCache(ignorePrerelease);

                    ConsoleImpl.WriteDebug("Unable to look up the latest version online, the cache contains version {0}.", latestVersion);

                    return latestVersion;
                }
                throw;
            }
        }

        public void DownloadVersion(string latestVersion, string target)
        {
            var cached = Path.Combine(_paketCacheDir, latestVersion, "paket.exe");

            if (!FileProxy.Exists(cached))
            {
                ConsoleImpl.WriteDebug("Version {0} not found in cache.", latestVersion);

                EffectiveStrategy.DownloadVersion(latestVersion, target);
                DirectoryProxy.CreateDirectory(Path.GetDirectoryName(cached));
                FileProxy.Copy(target, cached);
            }
            else
            {
                ConsoleImpl.WriteDebug("Copying version {0} from cache.", latestVersion);

                FileProxy.Copy(cached, target, true);
            }
        }

        public void SelfUpdate(string latestVersion)
        {
            EffectiveStrategy.SelfUpdate(latestVersion);
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