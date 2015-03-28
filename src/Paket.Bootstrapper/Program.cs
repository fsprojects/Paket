using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;

namespace Paket.Bootstrapper
{
    class Program
    {
        static IWebProxy GetDefaultWebProxyFor(String url)
        {
            IWebProxy result = WebRequest.GetSystemWebProxy();
            Uri uri = new Uri(url);
            Uri address = result.GetProxy(uri);

            if (address == uri)
                return null;

            return new WebProxy(address) { 
                Credentials = CredentialCache.DefaultCredentials,
                BypassProxyOnLocal = true
            };
        }

        static void Main(string[] args)
        {
            var folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var target = Path.Combine(folder, "paket.exe");
            var latestVersion = "";
            var ignorePrerelease = true;
            
            try
            {
                if (args.Length >= 1)
                {
                    if (args[0] == "prerelease")
                    {
                        ignorePrerelease = false;
                        Console.WriteLine("Prerelease requested. Looking for latest prerelease.");
                    }
                    else
                    {
                        latestVersion = args[0];
                        Console.WriteLine("Version {0} requested.", latestVersion);
                    }
                }
                else Console.WriteLine("No version specified. Downloading latest stable.");
                var localVersion = "";

                if (File.Exists(target))
                {
                    try
                    {
                        FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(target);
                        if (fvi.FileVersion != null)
                            localVersion = fvi.FileVersion;
                    }
                    catch (Exception)
                    {
                    }
                }

                if (latestVersion == "")
                {
                    using (WebClient client = new WebClient())
                    {
                        var releasesUrl = "https://github.com/fsprojects/Paket/releases";

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
                }

                if (!localVersion.StartsWith(latestVersion))
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

                            using (FileStream fileStream = File.Create(target))
                            {
                                while ((bytesRead = httpResponseStream.Read(buffer, 0, bufferSize)) != 0)
                                {
                                    fileStream.Write(buffer, 0, bytesRead);
                                }
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Paket.exe {0} is up to date.", localVersion);
                }
            }
            catch (WebException)
            {
                if (!File.Exists(target))
                {
                    Console.WriteLine("Github download failed. Try downloading Paket directly from 'nuget.org'.");
                    NugetAlternativeDownload(folder, target, latestVersion, ignorePrerelease);
                }
            }
            catch (Exception exn)
            {
                if (!File.Exists(target))
                    Environment.ExitCode = 1;
                WriteConsoleError(exn.Message);
            }
        }

        private static void NugetAlternativeDownload(string folder, string target, string latestVersion, bool ignorePrereleases)
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    const string getLatestFromNugetUrl = "https://www.nuget.org/api/v2/package/Paket";
                    const string getVersionsFromNugetUrl = "https://www.nuget.org/api/v2/package-versions/Paket?includePrereleases=true";
                    const string getSpecificFromNugetUrlTemplate = "https://www.nuget.org/api/v2/package/Paket/{0}";
                    const string paketNupkgFile = "paket.latest.nupkg";
                    const string paketNupkgFileTemplate = "paket.{0}.nupkg";

                    var paketDownloadUrl = getLatestFromNugetUrl;
                    var paketFile = paketNupkgFile;
                    if (latestVersion == "" && !ignorePrereleases)
                    {
                        PrepareWebClient(client, getVersionsFromNugetUrl);
                        var versions = client.DownloadString(getVersionsFromNugetUrl);
                        latestVersion = versions.
                                            Trim('[', ']').
                                            Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries).
                                            Select(x => x.Trim('"')).
                                            LastOrDefault(x => !String.IsNullOrWhiteSpace(x)) ?? String.Empty;
                    }
                    if (latestVersion != "")
                    {
                        paketDownloadUrl = String.Format(getSpecificFromNugetUrlTemplate, latestVersion);
                        paketFile = String.Format(paketNupkgFileTemplate, latestVersion);
                    }

                    var randomFullPath = Path.Combine(folder, Path.GetRandomFileName());
                    Directory.CreateDirectory(randomFullPath);
                    var paketPackageFile = Path.Combine(randomFullPath, paketFile);
                    Console.WriteLine("Starting download from {0}", paketDownloadUrl);
                    PrepareWebClient(client, paketDownloadUrl);
                    client.DownloadFile(paketDownloadUrl, paketPackageFile);

                    ZipFile.ExtractToDirectory(paketPackageFile, randomFullPath);
                    var paketSourceFile = Path.Combine(randomFullPath, "Tools", "Paket.exe");
                    File.Copy(paketSourceFile, target);
                    Directory.Delete(randomFullPath, true);
                }
            }
            catch (Exception exn)
            {
                if (!File.Exists(target))
                    Environment.ExitCode = 1;
                WriteConsoleError(exn.Message);
            }
        }

        private static void PrepareWebClient(WebClient client, string url)
        {
            client.Headers.Add("user-agent", "Paket.Bootstrapper");
            client.UseDefaultCredentials = true;
            client.Proxy = GetDefaultWebProxyFor(url);
        }

        private static void WriteConsoleError(string message)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ForegroundColor = oldColor;
        }
    }
}