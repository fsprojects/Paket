using System.IO;
using Path = Pri.LongPath.Path;
using Directory = Pri.LongPath.Directory;
using DirectoryInfo = Pri.LongPath.DirectoryInfo;
using File = Pri.LongPath.File;

namespace Paket.Bootstrapper.HelperProxies
{
    public interface IWebRequestProxy
    {
        string DownloadString(string address);
        void DownloadFile(string url, string targetLocation);
    }
}