using System;

namespace Paket.Bootstrapper
{
    internal class CacheDownloadStrategy : IDownloadStrategy
    {
        public string Name { get { return "Cache"; } }

        private IDownloadStrategy _fallbackStrategy;
        public IDownloadStrategy FallbackStrategy
        {
            get
            {
                return _fallbackStrategy;
            }
            set
            {
                if (value == null)
                    throw new ArgumentException("CacheDownloadStrategy needs a non-null FallbackStrategy");
                _fallbackStrategy = value;
            }
        }

        public CacheDownloadStrategy(IDownloadStrategy fallbackStrategy)
        {
            FallbackStrategy = fallbackStrategy;
        }

        public string GetLatestVersion(bool ignorePrerelease)
        {
            return FallbackStrategy.GetLatestVersion(ignorePrerelease);
        }

        public void DownloadVersion(string latestVersion, string target, bool silent)
        {
            throw new NotImplementedException();
        }

        public void SelfUpdate(string latestVersion, bool silent)
        {
            FallbackStrategy.SelfUpdate(latestVersion, silent);
        }
    }
}