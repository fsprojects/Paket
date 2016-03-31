using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;

namespace Paket.Bootstrapper.DownloadStrategies
{
    internal class GitHubDownloadStrategy : IDownloadStrategy
    {
        private PrepareWebClientDelegate PrepareWebClient { get; set; }
        private PrepareWebRequestDelegate PrepareWebRequest { get; set; }
        public string Name { get { return "Github"; } }
        public IDownloadStrategy FallbackStrategy { get; set; }

        public GitHubDownloadStrategy(PrepareWebClientDelegate prepareWebClient, PrepareWebRequestDelegate prepareWebRequest)
        {
            PrepareWebClient = prepareWebClient;
            PrepareWebRequest = prepareWebRequest;
        }

        public string GetLatestVersion(bool ignorePrerelease, bool silent)
        {
            using (var client = new WebClient())
            {
                var latestStable = GetLatestStable(client);
                if (ignorePrerelease)
                    return latestStable;
                else
                    return Max(GetLatestPrerelease(client), latestStable);
            }
        }

        private string Max(string prerelease, string latestStable)
        {
            var greater = new[] { prerelease, latestStable }.Where(x => !string.IsNullOrEmpty(x)).Select(SemVer.Create).OrderByDescending(x => x).FirstOrDefault();
            if (greater == null) return "";
            return greater.Original;
        }

        private string GetLatestPrerelease(WebClient client)
        {
            const string releases = "https://github.com/fsprojects/Paket/releases";
            PrepareWebClient(client, releases);
            var data = client.DownloadString(releases);
            return GetVersions(data).FirstOrDefault(s => s.Contains("-"));
        }

        private string GetLatestStable(WebClient client)
        {
            const string latest = "https://github.com/fsprojects/Paket/releases/latest";
            PrepareWebClient(client, latest);
            var data = client.DownloadString(latest);
            var title = data.Substring(data.IndexOf("<title>") + 7, (data.IndexOf("</title>") + 8 - data.IndexOf("<title>") + 7)); // grabs everything in the <title> tag
            var version = title.Split(' ')[1]; // Release, 1.34.0, etc, etc, etc <-- the release number is the second part fo this split string
            return version;
        }

        private List<string> GetVersions(string data)
        {
            var start = 0;
            var versions = new List<string>();
            while ((start = data.IndexOf("Paket/tree/", start)) != -1)
            {
                start = start + 11;
                var end = data.IndexOf("\"", start);
                var latestVersion = data.Substring(start, end - start);
                if (!versions.Contains(latestVersion)) versions.Add(latestVersion);
            }
            return versions;
        }

        public void DownloadVersion(string latestVersion, string target, bool silent)
        {
            var url = String.Format("https://github.com/fsprojects/Paket/releases/download/{0}/paket.exe", latestVersion);
            if (!silent)
                Console.WriteLine("Starting download from {0}", url);

            var request = PrepareWebRequest(url);

            using (var httpResponse = (HttpWebResponse)request.GetResponse())
            {
                using (var httpResponseStream = httpResponse.GetResponseStream())
                {
                    const int bufferSize = 4096;
                    byte[] buffer = new byte[bufferSize];
                    var tmpFile = BootstrapperHelper.GetTempFile("paket");

                    using (var fileStream = File.Create(tmpFile))
                    {
                        int bytesRead;
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

        public void SelfUpdate(string latestVersion, bool silent)
        {
            var executingAssembly = Assembly.GetExecutingAssembly();
            string exePath = executingAssembly.Location;
            var localVersion = BootstrapperHelper.GetLocalFileVersion(exePath);
            if (localVersion.StartsWith(latestVersion))
            {
                if (!silent)
                    Console.WriteLine("Bootstrapper is up to date. Nothing to do.");
                return;
            }

            var url = String.Format("https://github.com/fsprojects/Paket/releases/download/{0}/paket.bootstrapper.exe", latestVersion);
            if (!silent)
                Console.WriteLine("Starting download of bootstrapper from {0}", url);

            var request = PrepareWebRequest(url);

            string renamedPath = BootstrapperHelper.GetTempFile("oldBootstrapper");
            string tmpDownloadPath = BootstrapperHelper.GetTempFile("newBootstrapper");

            using (var httpResponse = (HttpWebResponse)request.GetResponse())
            {
                using (Stream httpResponseStream = httpResponse.GetResponseStream(), toStream = File.Create(tmpDownloadPath))
                {
                    httpResponseStream.CopyTo(toStream);
                }
            }
            try
            {
                BootstrapperHelper.FileMove(exePath, renamedPath);
                BootstrapperHelper.FileMove(tmpDownloadPath, exePath);
                if (!silent)
                    Console.WriteLine("Self update of bootstrapper was successful.");
            }
            catch (Exception)
            {
                if (!silent)
                    Console.WriteLine("Self update failed. Resetting bootstrapper.");
                BootstrapperHelper.FileMove(renamedPath, exePath);
                throw;
            }
        }

    }
}