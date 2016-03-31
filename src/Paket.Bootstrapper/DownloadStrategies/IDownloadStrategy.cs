using System.Net;

namespace Paket.Bootstrapper.DownloadStrategies
{
    public interface IDownloadStrategy
    {
        string Name { get; }
        IDownloadStrategy FallbackStrategy { get; }
        string GetLatestVersion(bool ignorePrerelease, bool silent);
        void DownloadVersion(string latestVersion, string target, bool silent);
        void SelfUpdate(string latestVersion, bool silent);
    }

    public delegate void PrepareWebClientDelegate(WebClient client, string url);
    public delegate HttpWebRequest PrepareWebRequestDelegate(string url);
    public delegate IWebProxy GetDefaultWebProxyForDelegate(string url);
}