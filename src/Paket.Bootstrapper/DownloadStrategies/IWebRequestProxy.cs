using System;
using System.IO;
using System.Net;

namespace Paket.Bootstrapper.DownloadStrategies
{
    public interface IWebRequestProxy
    {
        string DownloadString(string address);
        Stream GetResponseStream(string url);
    }

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
    }
}