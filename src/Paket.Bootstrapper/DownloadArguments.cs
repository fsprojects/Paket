using System;

namespace Paket.Bootstrapper
{
    public class DownloadArguments
    {
        public string Folder { get; private set; }
        public string Target { get; set; }
        public string NugetSource { get; set; }
        public bool DoSelfUpdate { get; set; }
        public string LatestVersion { get; set; }
        public bool IgnorePrerelease { get; set; }
        public bool IgnoreCache { get; set; }

        public DownloadArguments()
        {
            IgnorePrerelease = true;
            LatestVersion = String.Empty;
        }

        public DownloadArguments(string latestVersion, bool ignorePrerelease, string folder, string target, bool doSelfUpdate, string nugetSource, bool ignoreCache)
        {
            LatestVersion = latestVersion;
            IgnorePrerelease = ignorePrerelease;
            Folder = folder;
            Target = target;
            DoSelfUpdate = doSelfUpdate;
            NugetSource = nugetSource;
            IgnoreCache = ignoreCache;
        }
    }
}