using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Paket.Bootstrapper.HelperProxies;

namespace Paket.Bootstrapper.DownloadStrategies
{
    public class GitHubDownloadStrategy : DownloadStrategy
    {
#if DEBUG && LOCAL_GITHUB
        public static class Constants
        {
            public const string PaketReleasesLatestUrl = "http://127.0.0.1:8080/fsprojects/Paket/releases/latest";
            public const string PaketReleasesUrl = "http://127.0.0.1:8080/fsprojects/Paket/releases";
            public const string PaketExeDownloadUrlTemplate = "http://127.0.0.1:8080/fsprojects/Paket/releases/download/{0}/paket.exe";
            public const string PaketCheckSumDownloadUrlTemplate = "http://127.0.0.1:8080/fsprojects/Paket/releases/download/{0}/paket-sha256.txt";
        }
#else
        public static class Constants
        {
            public const string PaketReleasesLatestUrl = "https://github.com/fsprojects/Paket/releases/latest";
            public const string PaketReleasesUrl = "https://github.com/fsprojects/Paket/releases";
            public const string PaketExeDownloadUrlTemplate = "https://github.com/fsprojects/Paket/releases/download/{0}/paket.exe";
            public const string PaketCheckSumDownloadUrlTemplate = "https://github.com/fsprojects/Paket/releases/download/{0}/paket-sha256.txt";
        }
#endif

        private IWebRequestProxy WebRequestProxy { get; set; }
        private IFileSystemProxy FileSystemProxy { get; set; }
        public override string Name { get { return "Github"; } }

        public override bool CanDownloadHashFile
        {
            get { return true; }
        }

        public GitHubDownloadStrategy(IWebRequestProxy webRequestProxy, IFileSystemProxy fileSystemProxy)
        {
            WebRequestProxy = webRequestProxy;
            FileSystemProxy = fileSystemProxy;
        }

        protected override string GetLatestVersionCore(bool ignorePrerelease)
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

        protected override void DownloadVersionCore(string latestVersion, string target, PaketHashFile hashfile)
        {
            var url = String.Format(Constants.PaketExeDownloadUrlTemplate, latestVersion);
            ConsoleImpl.WriteInfo("Starting download from {0}", url);

            var tmpFile = BootstrapperHelper.GetTempFile("paket");
            WebRequestProxy.DownloadFile(url, tmpFile);

            if (!BootstrapperHelper.ValidateHash(FileSystemProxy, hashfile, latestVersion, tmpFile))
            {
                ConsoleImpl.WriteWarning("Hash of downloaded paket.exe is invalid, retrying once");

                WebRequestProxy.DownloadFile(url, tmpFile);

                if (!BootstrapperHelper.ValidateHash(FileSystemProxy, hashfile, latestVersion, tmpFile))
                {
                    ConsoleImpl.WriteWarning("Hash of downloaded paket.exe still invalid (Using the file anyway)");
                }
                else
                {
                    ConsoleImpl.WriteTrace("Hash of downloaded file successfully found in {0}", hashfile);
                }
            }
            else
            {
                ConsoleImpl.WriteTrace("Hash of downloaded file successfully found in {0}", hashfile);
            }

            FileSystemProxy.CopyFile(tmpFile, target, true);
            FileSystemProxy.DeleteFile(tmpFile);
        }

        protected override void SelfUpdateCore(string latestVersion)
        {
            var executingAssembly = Assembly.GetExecutingAssembly();
            string exePath = executingAssembly.Location;
            var localVersion = FileSystemProxy.GetLocalFileVersion(exePath);
            if (localVersion.StartsWith(latestVersion))
            {
                ConsoleImpl.WriteInfo("Bootstrapper is up to date. Nothing to do.");
                return;
            }

            var url = String.Format("https://github.com/fsprojects/Paket/releases/download/{0}/paket.bootstrapper.exe", latestVersion);
            ConsoleImpl.WriteInfo("Starting download of bootstrapper from {0}", url);

            string renamedPath = BootstrapperHelper.GetTempFile("oldBootstrapper");
            string tmpDownloadPath = BootstrapperHelper.GetTempFile("newBootstrapper");
            WebRequestProxy.DownloadFile(url, tmpDownloadPath);

            try
            {
                FileSystemProxy.MoveFile(exePath, renamedPath);
                FileSystemProxy.MoveFile(tmpDownloadPath, exePath);
                ConsoleImpl.WriteInfo("Self update of bootstrapper was successful.");
            }
            catch (Exception)
            {
                ConsoleImpl.WriteInfo("Self update failed. Resetting bootstrapper.");
                FileSystemProxy.MoveFile(renamedPath, exePath);
                throw;
            }
        }

        protected override PaketHashFile DownloadHashFileCore(string latestVersion)
        {
            var url = string.Format(Constants.PaketCheckSumDownloadUrlTemplate, latestVersion);
            ConsoleImpl.WriteInfo("Starting download from {0}", url);

            var content = WebRequestProxy.DownloadString(url);

            return PaketHashFile.FromString(content);
        }
    }
}