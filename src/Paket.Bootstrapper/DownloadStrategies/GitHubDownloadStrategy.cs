using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Paket.Bootstrapper.HelperProxies;

namespace Paket.Bootstrapper.DownloadStrategies
{
    public class GitHubDownloadStrategy : IDownloadStrategy
    {
        private const int HttpBufferSize = 4096;

        public static class Constants
        {
            public const string PaketReleasesLatestUrl = "https://github.com/fsprojects/Paket/releases/latest";
            public const string PaketReleasesUrl = "https://github.com/fsprojects/Paket/releases";
            public const string PaketExeDownloadUrlTemplate = "https://github.com/fsprojects/Paket/releases/download/{0}/paket.exe";
        }

        public IWebRequestProxy WebRequestProxy { get; set; }
        public IFileProxy FileProxy { get; set; }
        public string Name { get { return "Github"; } }
        public IDownloadStrategy FallbackStrategy { get; set; }

        public GitHubDownloadStrategy(IWebRequestProxy webRequestProxy, IFileProxy fileProxy)
        {
            WebRequestProxy = webRequestProxy;
            FileProxy = fileProxy;
        }

        public string GetLatestVersion(bool ignorePrerelease, bool silent)
        {
            var latestStable = GetLatestStable();
            if (ignorePrerelease)
                return latestStable;
            else
                return Max(GetLatestPrerelease(), latestStable);
        }

        private string Max(string prerelease, string latestStable)
        {
            var greater = new[] { prerelease, latestStable }.Where(x => !string.IsNullOrEmpty(x)).Select(SemVer.Create).OrderByDescending(x => x).FirstOrDefault();
            if (greater == null) return "";
            return greater.Original;
        }

        private string GetLatestPrerelease()
        {
            var data = WebRequestProxy.DownloadString(Constants.PaketReleasesUrl);
            return GetVersions(data).FirstOrDefault(s => s.Contains("-"));
        }

        private string GetLatestStable()
        {
            var data = WebRequestProxy.DownloadString(Constants.PaketReleasesLatestUrl);
            var title = data.Substring(data.IndexOf("<title>") + 7, (data.IndexOf("</title>") + 8) - (data.IndexOf("<title>") + 7)); // grabs everything in the <title> tag
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
            var url = String.Format(Constants.PaketExeDownloadUrlTemplate, latestVersion);
            if (!silent)
                Console.WriteLine("Starting download from {0}", url);

            using (var httpResponseStream = WebRequestProxy.GetResponseStream(url))
            {
                //byte[] buffer = new byte[bufferSize];
                var tmpFile = BootstrapperHelper.GetTempFile("paket");

                using (var fileStream = FileProxy.Create(tmpFile))
                {
                    httpResponseStream.CopyTo(fileStream, HttpBufferSize);
                }

                FileProxy.Copy(tmpFile, target, true);
                FileProxy.Delete(tmpFile);
            }
        }

        public void SelfUpdate(string latestVersion, bool silent)
        {
            var executingAssembly = Assembly.GetExecutingAssembly();
            string exePath = executingAssembly.Location;
            var localVersion = FileProxy.GetLocalFileVersion(exePath);
            if (localVersion.StartsWith(latestVersion))
            {
                if (!silent)
                    Console.WriteLine("Bootstrapper is up to date. Nothing to do.");
                return;
            }

            var url = String.Format("https://github.com/fsprojects/Paket/releases/download/{0}/paket.bootstrapper.exe", latestVersion);
            if (!silent)
                Console.WriteLine("Starting download of bootstrapper from {0}", url);

            string renamedPath = BootstrapperHelper.GetTempFile("oldBootstrapper");
            string tmpDownloadPath = BootstrapperHelper.GetTempFile("newBootstrapper");

            using (Stream httpResponseStream = WebRequestProxy.GetResponseStream(url), toStream = FileProxy.Create(tmpDownloadPath))
            {
                httpResponseStream.CopyTo(toStream, HttpBufferSize);
            }
            try
            {
                FileProxy.FileMove(exePath, renamedPath);
                FileProxy.FileMove(tmpDownloadPath, exePath);
                if (!silent)
                    Console.WriteLine("Self update of bootstrapper was successful.");
            }
            catch (Exception)
            {
                if (!silent)
                    Console.WriteLine("Self update failed. Resetting bootstrapper.");
                FileProxy.FileMove(renamedPath, exePath);
                throw;
            }
        }

    }
}