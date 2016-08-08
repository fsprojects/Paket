using System;
using System.IO;
using Paket.Bootstrapper.HelperProxies;

namespace Paket.Bootstrapper.DownloadStrategies
{
    internal class TemporarilyIgnoreUpdatesDownloadStrategy : IHaveEffectiveStrategy
    {
        private readonly int _maxFileAgeOfPaketExeInMinutes;
        private readonly string _target;
        private readonly IFileProxy _fileProxy;

        public TemporarilyIgnoreUpdatesDownloadStrategy(
            IDownloadStrategy effectiveStrategy, 
            IFileProxy fileProxy,
            string target,
            int maxFileAgeOfPaketExeInMinutes)
        {
            if (effectiveStrategy == null)
                throw new ArgumentException("TemporarilyIgnoreUpdatesDownloadStrategy needs a non-null effective strategy");

            if (string.IsNullOrEmpty(target))
                throw new ArgumentException("TemporarilyIgnoreUpdatesDownloadStrategy needs a non-empty target");
            
            _effectiveStrategy = effectiveStrategy;
            _maxFileAgeOfPaketExeInMinutes = maxFileAgeOfPaketExeInMinutes;
            _fileProxy = fileProxy;

            _target = target;
        }

        public string GetLatestVersion (bool ignorePrerelease)
        {
            var targetVersion = string.Empty;
            try 
            {
                targetVersion = _fileProxy.GetLocalFileVersion(_target);
            } 
            catch (FileNotFoundException) 
            {
                targetVersion = string.Empty;
            }

            if (!IsOlderThanMaxFileAge())
            {
                ConsoleImpl.WriteDebug("Don't look for new version, as last version is not older than {0} minutes", _maxFileAgeOfPaketExeInMinutes);
                return targetVersion;
            }
            
            var latestVersion = _effectiveStrategy.GetLatestVersion(ignorePrerelease);
            if (latestVersion == targetVersion)
                TouchTarget(_target);

            return latestVersion;
        }

        public void DownloadVersion (string latestVersion, string target)
        {
            _effectiveStrategy.DownloadVersion(latestVersion, target);
            TouchTarget(target);
        }

        public void SelfUpdate (string latestVersion)
        {
            _effectiveStrategy.SelfUpdate (latestVersion);
        }

        public string Name { get { return string.Format("{0} (temporarily ignore updates)", EffectiveStrategy.Name); } }

        public IDownloadStrategy FallbackStrategy { get; set; }

        public IDownloadStrategy EffectiveStrategy {
            get { return _effectiveStrategy; }
            set {
                if (value == null)
                    throw new ArgumentException("TemporarilyIgnoreUpdatesDownloadStrategy needs a non-null EffecitveStrategy");

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
                var lastModification = _fileProxy.GetLastWriteTime(_target);

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
                _fileProxy.Touch(target);
            }
            catch (FileNotFoundException)
            {
                ConsoleImpl.WriteDebug("Could not update the timestamps. File {0} not found!", _target);
            }
            catch (Exception)
            {
                ConsoleImpl.WriteDebug("Could not update the timestamps.");
            }
        }
    }
}

