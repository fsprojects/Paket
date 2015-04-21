using System;
using System.IO;
using System.Net;
using System.Reflection;

namespace Paket.Bootstrapper
{
    internal class GitHubDownloadStrategy : IDownloadStrategy
    {
        private PrepareWebClientDelegate PrepareWebClient { get; set; }
        private GetDefaultWebProxyForDelegate GetDefaultWebProxyFor { get; set; }
        public string Name { get { return "Github"; } }
        public IDownloadStrategy FallbackStrategy { get; set; }

        public GitHubDownloadStrategy(PrepareWebClientDelegate prepareWebClient, GetDefaultWebProxyForDelegate getDefaultWebProxyFor)
        {
            PrepareWebClient = prepareWebClient;
            GetDefaultWebProxyFor = getDefaultWebProxyFor;
        }

        public string GetLatestVersion(bool ignorePrerelease)
        {
            string latestVersion = "";
            using (WebClient client = new WebClient())
            {
                const string releasesUrl = "https://github.com/fsprojects/Paket/releases";
                PrepareWebClient(client, releasesUrl);

                var data = client.DownloadString(releasesUrl);
                var start = 0;
                while (latestVersion == "")
                {
                    start = data.IndexOf("Paket/tree/", start) + 11;
                    var end = data.IndexOf("\"", start);
                    latestVersion = data.Substring(start, end - start);
                    if (latestVersion.Contains("-") && ignorePrerelease)
                        latestVersion = "";
                }
            }
            return latestVersion;
        }

        public void DownloadVersion(string latestVersion, string target)
        {
            var url = String.Format("https://github.com/fsprojects/Paket/releases/download/{0}/paket.exe", latestVersion);

            Console.WriteLine("Starting download from {0}", url);

            var request = (HttpWebRequest)HttpWebRequest.Create(url);

            request.UseDefaultCredentials = true;
            request.Proxy = GetDefaultWebProxyFor(url);
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            using (HttpWebResponse httpResponse = (HttpWebResponse)request.GetResponse())
            {
                using (Stream httpResponseStream = httpResponse.GetResponseStream())
                {
                    const int bufferSize = 4096;
                    byte[] buffer = new byte[bufferSize];
                    int bytesRead = 0;
                    var tmpFile = Path.GetTempFileName();

                    using (FileStream fileStream = File.Create(tmpFile))
                    {
                        while ((bytesRead = httpResponseStream.Read(buffer, 0, bufferSize)) != 0)
                        {
                            fileStream.Write(buffer, 0, bytesRead);
                        }
                    }

                    File.Copy(tmpFile, target, true);
                    File.Delete(tmpFile);
                }
            }
        }

        public bool SelfUpdate(string latestVersion)
        {
            bool updateSuccess = false;
            var executingAssembly = Assembly.GetExecutingAssembly();
            string exePath = executingAssembly.Location;
            var localVersion = Program.GetLocalFileVersion(exePath);
            if (localVersion.StartsWith(latestVersion))
            {
                Console.WriteLine("Bootstrapper is up to date. Nothing to do.");
                return updateSuccess;
            }

            var url = String.Format("https://github.com/fsprojects/Paket/releases/download/{0}/paket.bootstrapper.exe", latestVersion);
            Console.WriteLine("Starting download of bootstrapper from {0}", url);

            var request = (HttpWebRequest)HttpWebRequest.Create(url);

            request.UseDefaultCredentials = true;
            request.Proxy = GetDefaultWebProxyFor(url);

            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;


            string renamedPath = exePath + ".old";
            Move(exePath, renamedPath);
            try
            {
                using (HttpWebResponse httpResponse = (HttpWebResponse)request.GetResponse())
                {
                    using (Stream httpResponseStream = httpResponse.GetResponseStream(), toStream = File.Create(exePath))
                    {
                        httpResponseStream.CopyTo(toStream);
                        updateSuccess = true;
                    }
                }
                if (updateSuccess)
                    File.Delete(renamedPath);
            }
            catch (Exception)
            {
                Console.WriteLine("Self update failed. Resetting bootstrapper.");
                Move(renamedPath, exePath);
                throw;
            }
            
            return updateSuccess;
        }

        protected void Move(string oldPath, string newPath)
        {
            try
            {
                if (File.Exists(newPath))
                {
                    File.Delete(newPath);
                }
            }
            catch (FileNotFoundException)
            {

            }

            File.Move(oldPath, newPath);
        }
    }
}