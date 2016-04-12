using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using Paket.Bootstrapper.HelperProxies;

namespace Paket.Bootstrapper.DownloadStrategies
{
    internal class NugetDownloadStrategy : IDownloadStrategy
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
        private IDirectoryProxy DirectoryProxy { get; set; }
        private IFileProxy FileProxy { get; set; }
        private string Folder { get; set; }
        private string NugetSource { get; set; }
        private const string PaketNugetPackageName = "Paket";
        private const string PaketBootstrapperNugetPackageName = "Paket.Bootstrapper";

        public NugetDownloadStrategy(IWebRequestProxy webRequestProxy, IDirectoryProxy directoryProxy, IFileProxy fileProxy, string folder, string nugetSource)
        {
            WebRequestProxy = webRequestProxy;
            DirectoryProxy = directoryProxy;
            FileProxy = fileProxy;
            Folder = folder;
            NugetSource = nugetSource;
        }

        public string Name
        {
            get { return "Nuget"; }
        }

        public IDownloadStrategy FallbackStrategy { get; set; }

        public string GetLatestVersion(bool ignorePrerelease)
        {
            IEnumerable<string> allVersions = null;
            if (DirectoryProxy.Exists(NugetSource))
            {
                var paketPrefix = "paket.";
                allVersions = DirectoryProxy.
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

        public void DownloadVersion(string latestVersion, string target)
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
            DirectoryProxy.CreateDirectory(randomFullPath);
            var paketPackageFile = Path.Combine(randomFullPath, paketFile);

            if (DirectoryProxy.Exists(NugetSource))
            {
                if (String.IsNullOrWhiteSpace(latestVersion)) latestVersion = GetLatestVersion(false);
                var sourcePath = Path.Combine(NugetSource, String.Format(paketNupkgFileTemplate, latestVersion));

                ConsoleImpl.WriteDebug("Starting download from {0}", sourcePath);

                FileProxy.Copy(sourcePath, paketPackageFile);
            }
            else
            {
                ConsoleImpl.WriteDebug("Starting download from {0}", paketDownloadUrl);

                WebRequestProxy.DownloadFile(paketDownloadUrl, paketPackageFile);
            }

            FileProxy.ExtractToDirectory(paketPackageFile, randomFullPath);
            var paketSourceFile = Path.Combine(randomFullPath, "tools", "paket.exe");
            FileProxy.Copy(paketSourceFile, target, true);
            DirectoryProxy.Delete(randomFullPath, true);
        }

        public void SelfUpdate(string latestVersion)
        {
            string target = Assembly.GetExecutingAssembly().Location;
            var localVersion = FileProxy.GetLocalFileVersion(target);
            if (localVersion.StartsWith(latestVersion))
            {
                ConsoleImpl.WriteDebug("Bootstrapper is up to date. Nothing to do.");
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
            DirectoryProxy.CreateDirectory(randomFullPath);
            var paketPackageFile = Path.Combine(randomFullPath, paketFile);

            if (DirectoryProxy.Exists(NugetSource))
            {
                if (String.IsNullOrWhiteSpace(latestVersion)) latestVersion = GetLatestVersion(false);
                var sourcePath = Path.Combine(NugetSource, String.Format(paketNupkgFileTemplate, latestVersion));

                ConsoleImpl.WriteDebug("Starting download from {0}", sourcePath);

                FileProxy.Copy(sourcePath, paketPackageFile);
            }
            else
            {
                ConsoleImpl.WriteDebug("Starting download from {0}", paketDownloadUrl);

                WebRequestProxy.DownloadFile(paketDownloadUrl, paketPackageFile);
            }

            FileProxy.ExtractToDirectory(paketPackageFile, randomFullPath);

            var paketSourceFile = Path.Combine(randomFullPath, "tools", "paket.bootstrapper.exe");
            var renamedPath = BootstrapperHelper.GetTempFile("oldBootstrapper");
            try
            {
                FileProxy.FileMove(target, renamedPath);
                FileProxy.FileMove(paketSourceFile, target);
                ConsoleImpl.WriteDebug("Self update of bootstrapper was successful.");
            }
            catch (Exception)
            {
                ConsoleImpl.WriteDebug("Self update failed. Resetting bootstrapper.");
                FileProxy.FileMove(renamedPath, target);
                throw;
            }
            DirectoryProxy.Delete(randomFullPath, true);
        }
    }
}