using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;

namespace Paket.Bootstrapper
{
    internal class NugetDownloadStrategy : IDownloadStrategy
    {
        internal class NugetApiHelper
        {
            private readonly string packageName;
            const string GetPackageVersionTemplate = "https://www.nuget.org/api/v2/package-versions/{0}";
            const string GetLatestFromNugetUrlTemplate = "https://www.nuget.org/api/v2/package/{0}";
            const string GetSpecificFromNugetUrlTemplate = "https://www.nuget.org/api/v2/package/{0}/{1}";

            public NugetApiHelper(string packageName)
            {
                this.packageName = packageName;
            }

            internal string GetAllPackageVersions(bool includePrerelease)
            {
                var request = String.Format(GetPackageVersionTemplate, packageName);
                const string withPrereleases = "?includePrerelease=true";
                if (includePrerelease)
                    request += withPrereleases;
                return request;
            }

            internal string GetLatestPackage()
            {
                return String.Format(GetLatestFromNugetUrlTemplate, packageName);
            }

            internal string GetSpecificPackageVersion(string version)
            {
                return String.Format(GetSpecificFromNugetUrlTemplate, packageName, version);
            }
        }


        private PrepareWebClientDelegate PrepareWebClient { get; set; }
        private GetDefaultWebProxyForDelegate GetDefaultWebProxyFor { get; set; }
        private string Folder { get; set; }
        private const string PaketNugetPackageName = "Paket";
        private const string PaketBootstrapperNugetPackageName = "Paket.Bootstrapper";

        public NugetDownloadStrategy(PrepareWebClientDelegate prepareWebClient, GetDefaultWebProxyForDelegate getDefaultWebProxyFor, string folder)
        {
            PrepareWebClient = prepareWebClient;
            GetDefaultWebProxyFor = getDefaultWebProxyFor;
            Folder = folder;
        }

        public string Name
        {
            get { return "Nuget"; }
        }

        public IDownloadStrategy FallbackStrategy { get; set; }

        public string GetLatestVersion(bool ignorePrerelease)
        {
            var apiHelper = new NugetApiHelper(PaketNugetPackageName);
            using (var client = new WebClient())
            {
                var versionRequestUrl = apiHelper.GetAllPackageVersions(!ignorePrerelease);
                PrepareWebClient(client, versionRequestUrl);
                var versions = client.DownloadString(versionRequestUrl);
                var latestVersion = versions.
                    Trim('[', ']').
                    Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).
                    Select(x => x.Trim('"')).
                    Select(SemVer.Create).
                    OrderBy(x => x).
                    LastOrDefault(x => !String.IsNullOrWhiteSpace(x.Original));
                return latestVersion != null ? latestVersion.Original : String.Empty;
            }
        }

        public void DownloadVersion(string latestVersion, string target, bool silent)
        {
            var apiHelper = new NugetApiHelper(PaketNugetPackageName);
            using (WebClient client = new WebClient())
            {
                const string paketNupkgFile = "paket.latest.nupkg";
                const string paketNupkgFileTemplate = "paket.{0}.nupkg";

                var paketDownloadUrl = apiHelper.GetLatestPackage();
                var paketFile = paketNupkgFile;
                if (latestVersion != String.Empty)
                {
                    paketDownloadUrl = apiHelper.GetSpecificPackageVersion(latestVersion);
                    paketFile = String.Format(paketNupkgFileTemplate, latestVersion);
                }

                var randomFullPath = Path.Combine(Folder, Path.GetRandomFileName());
                Directory.CreateDirectory(randomFullPath);
                var paketPackageFile = Path.Combine(randomFullPath, paketFile);
                if (!silent)
                    Console.WriteLine("Starting download from {0}", paketDownloadUrl);
                PrepareWebClient(client, paketDownloadUrl);
                client.DownloadFile(paketDownloadUrl, paketPackageFile);

                ZipFile.ExtractToDirectory(paketPackageFile, randomFullPath);
                var paketSourceFile = Path.Combine(randomFullPath, "tools", "paket.exe");
                File.Copy(paketSourceFile, target, true);
                Directory.Delete(randomFullPath, true);
            }
        }

        public void SelfUpdate(string latestVersion, bool silent)
        {
            var executingAssembly = Assembly.GetExecutingAssembly();
            string target = executingAssembly.Location;
            var localVersion = BootstrapperHelper.GetLocalFileVersion(target);
            if (localVersion.StartsWith(latestVersion))
            {
                if (!silent)
                    Console.WriteLine("Bootstrapper is up to date. Nothing to do.");
                return;
            }
            var apiHelper = new NugetApiHelper(PaketBootstrapperNugetPackageName);

            const string paketNupkgFile = "paket.bootstrapper.latest.nupkg";
            const string paketNupkgFileTemplate = "paket.bootstrapper.{0}.nupkg";
            var getLatestFromNugetUrl = apiHelper.GetLatestPackage();

            var paketDownloadUrl = getLatestFromNugetUrl;
            var paketFile = paketNupkgFile;
            if (latestVersion != String.Empty)
            {
                paketDownloadUrl = apiHelper.GetSpecificPackageVersion(latestVersion);
                paketFile = String.Format(paketNupkgFileTemplate, latestVersion);
            }

            var randomFullPath = Path.Combine(Folder, Path.GetRandomFileName());
            Directory.CreateDirectory(randomFullPath);
            var paketPackageFile = Path.Combine(randomFullPath, paketFile);
            if (!silent)
                Console.WriteLine("Starting download from {0}", paketDownloadUrl);
            using (var client = new WebClient())
            {
                PrepareWebClient(client, paketDownloadUrl);
                client.DownloadFile(paketDownloadUrl, paketPackageFile);
            }
            ZipFile.ExtractToDirectory(paketPackageFile, randomFullPath);

            var paketSourceFile = Path.Combine(randomFullPath, "tools", "paket.bootstrapper.exe");
            var renamedPath = Path.GetTempFileName();
            try
            {
                BootstrapperHelper.FileMove(target, renamedPath);
                BootstrapperHelper.FileMove(paketSourceFile, target);
                if (!silent)
                    Console.WriteLine("Self update of bootstrapper was successful.");
            }
            catch (Exception)
            {
                if (!silent)
                    Console.WriteLine("Self update failed. Resetting bootstrapper.");
                BootstrapperHelper.FileMove(renamedPath, target);
                throw;
            }
            Directory.Delete(randomFullPath, true);
        }
    }
}