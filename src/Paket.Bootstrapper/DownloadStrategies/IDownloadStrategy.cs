namespace Paket.Bootstrapper.DownloadStrategies
{
    public interface IDownloadStrategy
    {
        string Name { get; }
        IDownloadStrategy FallbackStrategy { get; set; }
        string GetLatestVersion(bool ignorePrerelease);
        void DownloadVersion(string latestVersion, string target, string hashFile);
        void SelfUpdate(string latestVersion);
        string DownloadHashFile(string latestVersion);
        bool CanDownloadHashFile { get; }
    }
}