using System;

namespace Paket.Bootstrapper
{
    public class DownloadArguments
    {
        public string Folder { get; set; }
        public string Target { get; set; }
        public string NugetSource { get; set; }
        public bool DoSelfUpdate { get; set; }
        public string LatestVersion { get; set; }
        public bool IgnorePrerelease { get; set; }
        public bool IgnoreCache { get; set; }
        public int? MaxFileAgeInMinutes { get; set; }
        public bool AsTool { get; set; }

        public DownloadArguments()
        {
            IgnorePrerelease = true;
            LatestVersion = String.Empty;
            MaxFileAgeInMinutes = null;
            AsTool = false;
        }

        public DownloadArguments(string latestVersion, bool ignorePrerelease, string folder, string target, bool doSelfUpdate, string nugetSource, bool ignoreCache, int? maxFileAgeInMinutes)
        {
            LatestVersion = latestVersion;
            IgnorePrerelease = ignorePrerelease;
            Folder = folder;
            Target = target;
            DoSelfUpdate = doSelfUpdate;
            NugetSource = nugetSource;
            IgnoreCache = ignoreCache;
            MaxFileAgeInMinutes = maxFileAgeInMinutes;
        }
    }
}