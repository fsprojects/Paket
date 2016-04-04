using System.IO;

namespace Paket.Bootstrapper.HelperProxies
{
    public interface IWebRequestProxy
    {
        string DownloadString(string address);
        void DownloadFile(string url, string targetLocation);
        void DownloadFile(string url, Stream stream, int bufferSize);
    }
}