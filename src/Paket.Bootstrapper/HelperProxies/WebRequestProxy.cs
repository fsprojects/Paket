using System.IO;
using Path = Pri.LongPath.Path;
using Directory = Pri.LongPath.Directory;
using DirectoryInfo = Pri.LongPath.DirectoryInfo;
using File = Pri.LongPath.File;
using System.Net;

namespace Paket.Bootstrapper.HelperProxies
{
    public class WebRequestProxy : IWebRequestProxy
    {
        public WebRequestProxy()
        {
            Client = new WebClient();
        }

        private WebClient Client { get; set; }

        public string DownloadString(string address)
        {
            BootstrapperHelper.PrepareWebClient(Client, address);
            return Client.DownloadString(address);
        }

        public Stream GetResponseStream(string url)
        {
            var request = BootstrapperHelper.PrepareWebRequest(url);

            using (var httpResponse = (HttpWebResponse)request.GetResponse())
            {
                return httpResponse.GetResponseStream();
            }
        }

        public void DownloadFile(string url, string targetLocation)
        {
            BootstrapperHelper.PrepareWebClient(Client, url);
            Client.DownloadFile(url, targetLocation);
        }
    }
}