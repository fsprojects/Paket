using System.Net;

namespace Paket.Bootstrapper
{
    internal interface IDownloadStrategy
    {
        string Name { get; }
        IDownloadStrategy FallbackStrategy { get; set; }
        string GetLatestVersion(bool ignorePrerelease);
        void DownloadVersion(string latestVersion, string target);
        bool SelfUpdate(string latestVersion);
    }

    public delegate void PrepareWebClientDelegate(WebClient client, string url);
    public delegate HttpWebRequest PrepareWebRequestDelegate(string url);
    public delegate IWebProxy GetDefaultWebProxyForDelegate(string url);
}