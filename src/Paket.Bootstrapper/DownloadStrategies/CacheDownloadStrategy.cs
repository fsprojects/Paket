using System;
using System.IO;
using System.Linq;
using System.Net;
using Paket.Bootstrapper.HelperProxies;
using System.Security.Cryptography;

namespace Paket.Bootstrapper.DownloadStrategies
{
    internal class CacheDownloadStrategy : DownloadStrategy, IHaveEffectiveStrategy
    {
        public override string Name { get { return String.Format("{0} - cached", EffectiveStrategy.Name); } }

        private readonly string _paketCacheDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NuGet", "Cache", "Paket");

        public IDownloadStrategy EffectiveStrategy { get; set; }
        public IFileSystemProxy FileSystemProxy { get; set; }

        public CacheDownloadStrategy(IDownloadStrategy effectiveStrategy, IFileSystemProxy fileSystemProxy)
        {
            if (effectiveStrategy == null)
                throw new ArgumentException("CacheDownloadStrategy needs a non-null effective strategy");
            if (effectiveStrategy.FallbackStrategy != null)
                throw new ArgumentException("CacheDownloadStrategy should not have a fallback strategy");

            EffectiveStrategy = effectiveStrategy;
            FileSystemProxy = fileSystemProxy;
        }


        protected override string GetLatestVersionCore(bool ignorePrerelease)
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

                    ConsoleImpl.WriteInfo("Unable to look up the latest version online, the cache contains version {0}.", latestVersion);

                    return latestVersion;
                }
                throw;
            }
        }

        protected override void DownloadVersionCore(string latestVersion, string target)
        {
            var cached = Path.Combine(_paketCacheDir, latestVersion, "paket.exe");

            if (!FileSystemProxy.FileExists(cached))
            {
                ConsoleImpl.WriteInfo("Version {0} not found in cache.", latestVersion);

                DownloadAndPlaceInCache(latestVersion, target, cached);
                return;
            }

            if (!ValidateHash(latestVersion, cached))
            {
                ConsoleImpl.WriteWarning("Version {0} found in cache but it's hash isn't valid.", latestVersion);

                DownloadAndPlaceInCache(latestVersion, target, cached);
                return;
            }

            ConsoleImpl.WriteInfo("Copying version {0} from cache.", latestVersion);
            ConsoleImpl.WriteTrace("{0} -> {1}", cached, target);
            FileSystemProxy.CopyFile(cached, target, true);
        }

        private void DownloadAndPlaceInCache(string latestVersion, string target, string cached)
        {
            EffectiveStrategy.DownloadVersion(latestVersion, target);

            ConsoleImpl.WriteTrace("Caching version {0} for later", latestVersion);
            FileSystemProxy.CreateDirectory(Path.GetDirectoryName(cached));
            FileSystemProxy.CopyFile(target, cached);

            if (!ValidateHash(latestVersion, cached))
            {
                ConsoleImpl.WriteWarning("Hash of downloaded paket.exe is invalid");
            }
        }

        protected override string DownloadHashFileCore(string latestVersion)
        {
            var cached = GetHashFilePathInCache(latestVersion);

            if (!FileSystemProxy.FileExists(cached))
            {
                ConsoleImpl.WriteInfo("Hash file of version {0} not found in cache.", latestVersion);
                var effectivePath = EffectiveStrategy.DownloadHashFile(latestVersion);

                ConsoleImpl.WriteTrace("Copying hash file in cache.");
                ConsoleImpl.WriteTrace("{0} -> {1}", effectivePath, cached);
                FileSystemProxy.CopyFile(effectivePath, cached, true);
            }

            return cached;
        }

        protected override void SelfUpdateCore(string latestVersion)
        {
            EffectiveStrategy.SelfUpdate(latestVersion);
        }

        private string GetLatestVersionInCache(bool ignorePrerelease)
        {
            FileSystemProxy.CreateDirectory(_paketCacheDir);
            var zero = new SemVer();

            return FileSystemProxy.GetDirectories(_paketCacheDir)
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

        private bool ValidateHash(string version, string paketFile)
        {
            var hashFile = GetHashFilePathInCache(version);

            if (!FileSystemProxy.FileExists(hashFile))
            {
                ConsoleImpl.WriteInfo("No hash file of version {0} found in cache.", version);

                return true;
            }

            var dict = FileSystemProxy.ReadAllLines(hashFile)
                                      .Select(i => i.Split(' '))
                                      .ToDictionary(i => i[1], i => i[0]);

            string expectedHash;
            if (!dict.TryGetValue("paket.exe", out expectedHash))
            {
                FileSystemProxy.DeleteFile(hashFile);

                throw new InvalidDataException("Paket hash file is corrupted");
            }

            using (var stream = FileSystemProxy.OpenRead(paketFile))
            using (var sha = SHA256.Create())
            {
                byte[] checksum = sha.ComputeHash(stream);
                var hash = BitConverter.ToString(checksum).Replace("-", String.Empty);

                return string.Equals(expectedHash, hash, StringComparison.OrdinalIgnoreCase);
            }
        }

        private string GetHashFilePathInCache(string version)
        {
            return Path.Combine(_paketCacheDir, version, "paket-sha256.txt");
        }
    }
}