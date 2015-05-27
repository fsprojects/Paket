namespace Paket.Bootstrapper
{
    internal class DownloadArguments
    {
        public string Folder { get; private set; }
        public string Target { get; private set; }
        public bool DoSelfUpdate { get; set; }
        public string LatestVersion { get; private set; }
        public bool IgnorePrerelease { get; private set; }

        public DownloadArguments(string latestVersion, bool ignorePrerelease, string folder, string target, bool doSelfUpdate)
        {
            LatestVersion = latestVersion;
            IgnorePrerelease = ignorePrerelease;
            Folder = folder;
            Target = target;
            DoSelfUpdate = doSelfUpdate;
        }
    }
}