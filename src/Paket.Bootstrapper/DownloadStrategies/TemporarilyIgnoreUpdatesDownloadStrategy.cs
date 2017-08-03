using System;
using System.IO;
using Path = Pri.LongPath.Path;
using Directory = Pri.LongPath.Directory;
using DirectoryInfo = Pri.LongPath.DirectoryInfo;
using File = Pri.LongPath.File;
using Paket.Bootstrapper.HelperProxies;

namespace Paket.Bootstrapper.DownloadStrategies
{
    internal class TemporarilyIgnoreUpdatesDownloadStrategy : DownloadStrategy, IHaveEffectiveStrategy
    {
        private readonly int _maxFileAgeOfPaketExeInMinutes;
        private readonly string _target;
        private readonly IFileSystemProxy fileSystemProxy;

        public TemporarilyIgnoreUpdatesDownloadStrategy(
            IDownloadStrategy effectiveStrategy, 
            IFileSystemProxy fileSystemProxy,
            string target,
            int maxFileAgeOfPaketExeInMinutes)
        {
            if (effectiveStrategy == null)
                throw new ArgumentException("TemporarilyIgnoreUpdatesDownloadStrategy needs a non-null effective strategy");

            if (string.IsNullOrEmpty(target))
                throw new ArgumentException("TemporarilyIgnoreUpdatesDownloadStrategy needs a non-empty target");
            
            _effectiveStrategy = effectiveStrategy;
            _maxFileAgeOfPaketExeInMinutes = maxFileAgeOfPaketExeInMinutes;
            this.fileSystemProxy = fileSystemProxy;

            _target = target;
        }

        protected override string GetLatestVersionCore(bool ignorePrerelease)
        {
            var targetVersion = string.Empty;
            try 
            {
                targetVersion = fileSystemProxy.GetLocalFileVersion(_target);
            } 
            catch (FileNotFoundException) 
            {
                targetVersion = string.Empty;
            }

            if (!IsOlderThanMaxFileAge())
            {
                ConsoleImpl.WriteInfo("Don't look for new version, as last version is not older than {0} minutes", _maxFileAgeOfPaketExeInMinutes);
                return targetVersion;
            }
            
            ConsoleImpl.WriteTrace("Target file is older than {0} minutes or not found.", _maxFileAgeOfPaketExeInMinutes);

            var latestVersion = _effectiveStrategy.GetLatestVersion(ignorePrerelease);
            if (latestVersion == targetVersion)
            {
                ConsoleImpl.WriteTrace("Target file version is already the latest version (v{0})", latestVersion);
                TouchTarget(_target);
            }

            return latestVersion;
        }

        protected override void DownloadVersionCore(string latestVersion, string target, string hashfile)
        {
            _effectiveStrategy.DownloadVersion(latestVersion, target, hashfile);
            TouchTarget(target);
        }

        protected override void SelfUpdateCore(string latestVersion)
        {
            _effectiveStrategy.SelfUpdate (latestVersion);
        }

        public override string Name { get { return string.Format("{0} (temporarily ignore updates)", EffectiveStrategy.Name); } }

        public override bool CanDownloadHashFile
        {
            get { return EffectiveStrategy.CanDownloadHashFile; }
        }

        public IDownloadStrategy EffectiveStrategy {
            get { return _effectiveStrategy; }
            set {
                if (value == null)
                    throw new ArgumentException("TemporarilyIgnoreUpdatesDownloadStrategy needs a non-null EffectiveStrategy");

                _effectiveStrategy = value;
            }
        }
            
        private IDownloadStrategy _effectiveStrategy;

        private bool IsOlderThanMaxFileAge()
        {
            if (_maxFileAgeOfPaketExeInMinutes <= 0)
                return true;
            
            try 
            {
                var lastModification = fileSystemProxy.GetLastWriteTime(_target);
                ConsoleImpl.WriteTrace("Target file last modification: {0}", lastModification);

                return DateTimeProxy.Now > lastModification.AddMinutes(_maxFileAgeOfPaketExeInMinutes);
            } 
            catch (FileNotFoundException) 
            {
                return true;
            }
        }

        private void TouchTarget(string target)
        {
            try
            {
                fileSystemProxy.Touch(target);
            }
            catch (FileNotFoundException)
            {
                ConsoleImpl.WriteInfo("Could not update the timestamps. File {0} not found!", _target);
            }
            catch (Exception)
            {
                ConsoleImpl.WriteInfo("Could not update the timestamps.");
            }
        }

        protected override string DownloadHashFileCore(string latestVersion)
        {
            return _effectiveStrategy.DownloadHashFile(latestVersion);
        }
    }
}

