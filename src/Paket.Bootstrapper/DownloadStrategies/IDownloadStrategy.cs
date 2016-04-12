using System.Net;

namespace Paket.Bootstrapper.DownloadStrategies
{
    public interface IDownloadStrategy
    {
        string Name { get; }
        IDownloadStrategy FallbackStrategy { get; set; }
        string GetLatestVersion(bool ignorePrerelease);
        void DownloadVersion(string latestVersion, string target);
        void SelfUpdate(string latestVersion);
    }

    public interface ICachedDownloadStrategy : IDownloadStrategy
    {
        IDownloadStrategy EffectiveStrategy { get; }
    }

    public delegate void PrepareWebClientDelegate(WebClient client, string url);
    public delegate HttpWebRequest PrepareWebRequestDelegate(string url);
    public delegate IWebProxy GetDefaultWebProxyForDelegate(string url);
}