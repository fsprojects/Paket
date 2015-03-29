using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;

namespace Paket.Bootstrapper
{
    internal class NugetDownloadStrategy : IDownloadStrategy
    {
        private PrepareWebClientDelegate PrepareWebClient { get; set; }
        private GetDefaultWebProxyForDelegate GetDefaultWebProxyFor { get; set; }
        private string Folder { get; set; }

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

        public IDownloadStrategy FallbackStrategy
        {
            get;
            set;
        }

        public string GetLatestVersion(bool ignorePrerelease)
        {
            using (WebClient client = new WebClient())
            {
                const string getVersionsFromNugetUrl = "https://www.nuget.org/api/v2/package-versions/Paket";
                const string withPrereleases = "?includePrereleases=true";

                var versionRequestUrl = getVersionsFromNugetUrl;
                if (!ignorePrerelease)
                    versionRequestUrl += withPrereleases;
                PrepareWebClient(client, versionRequestUrl);
                var versions = client.DownloadString(versionRequestUrl);
                var latestVersion = versions.
                    Trim('[', ']').
                    Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).
                    Select(x => x.Trim('"')).
                    LastOrDefault(x => !String.IsNullOrWhiteSpace(x)) ?? String.Empty;
                return latestVersion;
            }
        }

        public void DownloadVersion(string latestVersion, string target)
        {
            using (WebClient client = new WebClient())
            {
                const string getLatestFromNugetUrl = "https://www.nuget.org/api/v2/package/Paket";
                const string getSpecificFromNugetUrlTemplate = "https://www.nuget.org/api/v2/package/Paket/{0}";
                const string paketNupkgFile = "paket.latest.nupkg";
                const string paketNupkgFileTemplate = "paket.{0}.nupkg";

                var paketDownloadUrl = getLatestFromNugetUrl;
                var paketFile = paketNupkgFile;
                if (latestVersion != "")
                {
                    paketDownloadUrl = String.Format(getSpecificFromNugetUrlTemplate, latestVersion);
                    paketFile = String.Format(paketNupkgFileTemplate, latestVersion);
                }

                var randomFullPath = Path.Combine(Folder, Path.GetRandomFileName());
                Directory.CreateDirectory(randomFullPath);
                var paketPackageFile = Path.Combine(randomFullPath, paketFile);
                Console.WriteLine("Starting download from {0}", paketDownloadUrl);
                PrepareWebClient(client, paketDownloadUrl);
                client.DownloadFile(paketDownloadUrl, paketPackageFile);

                ZipFile.ExtractToDirectory(paketPackageFile, randomFullPath);
                var paketSourceFile = Path.Combine(randomFullPath, "Tools", "Paket.exe");
                File.Copy(paketSourceFile, target, true);
                Directory.Delete(randomFullPath, true);
            }
        }

    }
}