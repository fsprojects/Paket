using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using Path = Pri.LongPath.Path;
using Directory = Pri.LongPath.Directory;
using DirectoryInfo = Pri.LongPath.DirectoryInfo;
using File = Pri.LongPath.File;
using System.Linq;
using System.Net;
using System.Reflection;
using Paket.Bootstrapper.HelperProxies;

namespace Paket.Bootstrapper.DownloadStrategies
{
    internal class NugetDownloadStrategy : DownloadStrategy
    {
        internal class NugetApiHelper
        {
            private readonly string packageName;
            private readonly string nugetSource;

            const string NugetSourceAppSettingsKey = "NugetSource";
            const string DefaultNugetSource = "https://www.nuget.org/api/v2";
            const string GetPackageVersionTemplate = "{0}/package-versions/{1}";
            const string GetLatestFromNugetUrlTemplate = "{0}/package/{1}";
            const string GetSpecificFromNugetUrlTemplate = "{0}/package/{1}/{2}";

            public NugetApiHelper(string packageName, string nugetSource)
            {
                this.packageName = packageName;
                this.nugetSource = nugetSource ?? ConfigurationManager.AppSettings[NugetSourceAppSettingsKey] ?? DefaultNugetSource;
            }

            internal string GetAllPackageVersions(bool includePrerelease)
            {
                var request = String.Format(GetPackageVersionTemplate, nugetSource, packageName);
                const string withPrereleases = "?includePrerelease=true";
                if (includePrerelease)
                    request += withPrereleases;
                return request;
            }

            internal string GetLatestPackage()
            {
                return String.Format(GetLatestFromNugetUrlTemplate, nugetSource, packageName);
            }

            internal string GetSpecificPackageVersion(string version)
            {
                return String.Format(GetSpecificFromNugetUrlTemplate, nugetSource, packageName, version);
            }
        }

        private IWebRequestProxy WebRequestProxy { get; set; }
        private IFileSystemProxy FileSystemProxy { get; set; }
        private string Folder { get; set; }
        private string NugetSource { get; set; }
        private const string PaketNugetPackageName = "Paket";
        private const string PaketBootstrapperNugetPackageName = "Paket.Bootstrapper";

        public NugetDownloadStrategy(IWebRequestProxy webRequestProxy, IFileSystemProxy fileSystemProxy, string folder, string nugetSource)
        {
            WebRequestProxy = webRequestProxy;
            FileSystemProxy = fileSystemProxy;
            Folder = folder;
            NugetSource = nugetSource;
        }

        public override string Name
        {
            get { return "Nuget"; }
        }

        public override bool CanDownloadHashFile
        {
            get { return false; }
        }

        protected override string GetLatestVersionCore(bool ignorePrerelease)
        {
            IEnumerable<string> allVersions = null;
            if (FileSystemProxy.DirectoryExists(NugetSource))
            {
                var paketPrefix = "paket.";
                allVersions = FileSystemProxy.
                    EnumerateFiles(NugetSource, "paket.*.nupkg", SearchOption.TopDirectoryOnly).
                    Select(x => Path.GetFileNameWithoutExtension(x)).
                    // If the specified character isn't a digit, then the file
                    // likely contains the bootstrapper or paket.core
                    Where(x => x.Length > paketPrefix.Length && Char.IsDigit(x[paketPrefix.Length])).
                    Select(x => x.Substring(paketPrefix.Length));
            }
            else
            {
                var apiHelper = new NugetApiHelper(PaketNugetPackageName, NugetSource);
                var versionRequestUrl = apiHelper.GetAllPackageVersions(!ignorePrerelease);
                var versions = WebRequestProxy.DownloadString(versionRequestUrl);
                allVersions = versions.
                    Trim('[', ']').
                    Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).
                    Select(x => x.Trim('"'));
            }
            var latestVersion = allVersions.
                    Select(SemVer.Create).
                    Where(x => !ignorePrerelease || (x.PreRelease == null)).
                    OrderBy(x => x).
                    LastOrDefault(x => !String.IsNullOrWhiteSpace(x.Original));
            return latestVersion != null ? latestVersion.Original : String.Empty;
        }

        protected override void DownloadVersionCore(string latestVersion, string target, string hashfile)
        {
            var apiHelper = new NugetApiHelper(PaketNugetPackageName, NugetSource);

            const string paketNupkgFile = "paket.latest.nupkg";
            const string paketNupkgFileTemplate = "paket.{0}.nupkg";

            var paketDownloadUrl = apiHelper.GetLatestPackage();
            var paketFile = paketNupkgFile;
            if (!String.IsNullOrWhiteSpace(latestVersion))
            {
                paketDownloadUrl = apiHelper.GetSpecificPackageVersion(latestVersion);
                paketFile = String.Format(paketNupkgFileTemplate, latestVersion);
            }

            var randomFullPath = Path.Combine(Folder, Path.GetRandomFileName());
            FileSystemProxy.CreateDirectory(randomFullPath);
            var paketPackageFile = Path.Combine(randomFullPath, paketFile);

            if (FileSystemProxy.DirectoryExists(NugetSource))
            {
                if (String.IsNullOrWhiteSpace(latestVersion)) latestVersion = GetLatestVersion(false);
                var sourcePath = Path.Combine(NugetSource, String.Format(paketNupkgFileTemplate, latestVersion));

                ConsoleImpl.WriteInfo("Starting download from {0}", sourcePath);

                FileSystemProxy.CopyFile(sourcePath, paketPackageFile);
            }
            else
            {
                ConsoleImpl.WriteInfo("Starting download from {0}", paketDownloadUrl);

                try
                {
                    WebRequestProxy.DownloadFile(paketDownloadUrl, paketPackageFile);
                }
                catch (WebException webException)
                {
                    if (webException.Status == WebExceptionStatus.ProtocolError && !string.IsNullOrEmpty(latestVersion))
                    {
                        var response = (HttpWebResponse) webException.Response;
                        if (response.StatusCode == HttpStatusCode.NotFound)
                        {
                            throw new WebException(String.Format("Version {0} wasn't found (404)",latestVersion),
                                webException);
                        }
                        if (response.StatusCode == HttpStatusCode.BadRequest)
                        {
                            // For cases like "The package version is not a valid semantic version"
                            throw new WebException(String.Format("Unable to get version '{0}': {1}",latestVersion,response.StatusDescription),
                                webException);
                        }
                    }
                    Console.WriteLine(webException.ToString());
                    throw;
                }
            }

            FileSystemProxy.ExtractToDirectory(paketPackageFile, randomFullPath);
            var paketSourceFile = Path.Combine(randomFullPath, "tools", "paket.exe");
            FileSystemProxy.CopyFile(paketSourceFile, target, true);
            FileSystemProxy.DeleteDirectory(randomFullPath, true);
        }

        protected override void SelfUpdateCore(string latestVersion)
        {
            string target = Assembly.GetExecutingAssembly().Location;
            var localVersion = FileSystemProxy.GetLocalFileVersion(target);
            if (localVersion.StartsWith(latestVersion))
            {
                ConsoleImpl.WriteInfo("Bootstrapper is up to date. Nothing to do.");
                return;
            }
            var apiHelper = new NugetApiHelper(PaketBootstrapperNugetPackageName, NugetSource);

            const string paketNupkgFile = "paket.bootstrapper.latest.nupkg";
            const string paketNupkgFileTemplate = "paket.bootstrapper.{0}.nupkg";
            var getLatestFromNugetUrl = apiHelper.GetLatestPackage();

            var paketDownloadUrl = getLatestFromNugetUrl;
            var paketFile = paketNupkgFile;
            if (!String.IsNullOrWhiteSpace(latestVersion))
            {
                paketDownloadUrl = apiHelper.GetSpecificPackageVersion(latestVersion);
                paketFile = String.Format(paketNupkgFileTemplate, latestVersion);
            }

            var randomFullPath = Path.Combine(Folder, Path.GetRandomFileName());
            FileSystemProxy.CreateDirectory(randomFullPath);
            var paketPackageFile = Path.Combine(randomFullPath, paketFile);

            if (FileSystemProxy.DirectoryExists(NugetSource))
            {
                if (String.IsNullOrWhiteSpace(latestVersion)) latestVersion = GetLatestVersion(false);
                var sourcePath = Path.Combine(NugetSource, String.Format(paketNupkgFileTemplate, latestVersion));

                ConsoleImpl.WriteInfo("Starting download from {0}", sourcePath);

                FileSystemProxy.CopyFile(sourcePath, paketPackageFile);
            }
            else
            {
                ConsoleImpl.WriteInfo("Starting download from {0}", paketDownloadUrl);

                WebRequestProxy.DownloadFile(paketDownloadUrl, paketPackageFile);
            }

            FileSystemProxy.ExtractToDirectory(paketPackageFile, randomFullPath);

            var paketSourceFile = Path.Combine(randomFullPath, "tools", "paket.bootstrapper.exe");
            var renamedPath = BootstrapperHelper.GetTempFile("oldBootstrapper");
            try
            {
                FileSystemProxy.MoveFile(target, renamedPath);
                FileSystemProxy.MoveFile(paketSourceFile, target);
                ConsoleImpl.WriteInfo("Self update of bootstrapper was successful.");
            }
            catch (Exception)
            {
                ConsoleImpl.WriteInfo("Self update failed. Resetting bootstrapper.");
                FileSystemProxy.MoveFile(renamedPath, target);
                throw;
            }
            FileSystemProxy.DeleteDirectory(randomFullPath, true);
        }

        protected override string DownloadHashFileCore(string latestVersion)
        {
            // TODO: implement get hash file
            return null;
        }
    }
}