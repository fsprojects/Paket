namespace Paket.Bootstrapper
{
    internal class DownloadArguments
    {
        public string Folder { get; private set; }
        public string Target { get; private set; }
        public string LatestVersion { get; private set; }
        public bool IgnorePrerelease { get; private set; }

        public DownloadArguments(string latestVersion, bool ignorePrerelease, string folder, string target)
        {
            LatestVersion = latestVersion;
            IgnorePrerelease = ignorePrerelease;
            Folder = folder;
            Target = target;
        }
    }
}