using System.IO;
using System.Net;

namespace Paket.Bootstrapper.HelperProxies
{
    public class WebRequestProxy : IWebRequestProxy
    {
        private readonly IEnvProxy envProxy;

        public WebRequestProxy(IEnvProxy envProxy)
        {
            Client = new WebClient();
            this.envProxy = envProxy;
        }

        private WebClient Client { get; set; }

        public string DownloadString(string address)
        {
            BootstrapperHelper.PrepareWebClient(Client, address, envProxy);
            return Client.DownloadString(address);
        }

        public Stream GetResponseStream(string url)
        {
            var request = BootstrapperHelper.PrepareWebRequest(url, envProxy);

            using (var httpResponse = (HttpWebResponse)request.GetResponse())
            {
                return httpResponse.GetResponseStream();
            }
        }

        public void DownloadFile(string url, string targetLocation)
        {
            BootstrapperHelper.PrepareWebClient(Client, url, envProxy);
            Client.DownloadFile(url, targetLocation);
        }
    }
}