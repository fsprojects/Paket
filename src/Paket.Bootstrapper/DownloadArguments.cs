namespace Paket.Bootstrapper
{
    internal class DownloadArguments
    {
        public string Folder { get; private set; }
        public string Target { get; private set; }
        public string NugetSource { get; private set; }
        public bool DoSelfUpdate { get; set; }
        public string LatestVersion { get; private set; }
        public bool IgnorePrerelease { get; private set; }
        public bool IgnoreCache { get; private set; }

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