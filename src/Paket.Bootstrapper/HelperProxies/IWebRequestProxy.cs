using System.IO;

namespace Paket.Bootstrapper.HelperProxies
{
    public interface IWebRequestProxy
    {
        string DownloadString(string address);
        Stream GetResponseStream(string url);
        void DownloadFile(string url, string targetLocation);
    }
}