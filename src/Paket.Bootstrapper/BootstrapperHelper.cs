using System;
using System.IO;
using Path = Pri.LongPath.Path;
using Directory = Pri.LongPath.Directory;
using DirectoryInfo = Pri.LongPath.DirectoryInfo;
using File = Pri.LongPath.File;
using System.Net;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Paket.Bootstrapper.HelperProxies;

namespace Paket.Bootstrapper
{
    internal static class BootstrapperHelper
    {
        public static string HelpText = @"The paket.bootstrapper downloads the latest version of paket.
Usage for paket bootstrapper:
paket.bootstrapper [OPTIONS] [prerelease|<version>]

Options:
--help                         print this help
--prefer-nuget                 prefer nuget as download source instead of github
--force-nuget                  only use nuget as source
--nuget-source=<NUGET_SOURCE>  uses <NUGET_SOURCE> to download latest paket.
                               NUGET_SOURCE can also be a filepath
--max-file-age=<IN MINUTES>    if the paket.exe already exists, and it is not 
                               older than <IN MINUTES> all checks will be skipped.
--self                         downloads and updates paket.bootstrapper
-f                             don't use local cache; always downloads
-s                             silent mode; errors only. Use twice for no output
-v                             verbose; show more information on console.
--run <other args>             run the downloaded paket.exe with all following arguments";
        const string PaketBootstrapperUserAgent = "Paket.Bootstrapper";

        internal static string GetLocalFileVersion(string target)
        {
            if (!File.Exists(target)) return "";

            try
            {
                var bytes = File.ReadAllBytes(target);
                var attr = Assembly.Load(bytes).GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false).Cast<AssemblyInformationalVersionAttribute>().FirstOrDefault();
                if (attr == null) return "";
                return attr.InformationalVersion;
            }
            catch (Exception) { return ""; }
        }

        internal static string GetTempFile(string name)
        {
            var path = Path.GetTempPath();
            var fileName = Path.Combine(path, name + System.Diagnostics.Process.GetCurrentProcess().Id);
            if (File.Exists(fileName))
                File.Delete(fileName);
            return fileName;
        }

        internal static void PrepareWebClient(WebClient client, string url)
        {
            client.Headers.Add("user-agent", PaketBootstrapperUserAgent);
            client.UseDefaultCredentials = true;
            client.Proxy = GetDefaultWebProxyFor(url);
        }

        internal static HttpWebRequest PrepareWebRequest(string url)
        {
            var request = (HttpWebRequest)HttpWebRequest.Create(url);
            request.UserAgent = PaketBootstrapperUserAgent;
            request.UseDefaultCredentials = true;
            request.Proxy = GetDefaultWebProxyFor(url);
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            return request;
        }

        internal static IWebProxy GetDefaultWebProxyFor(String url)
        {
            Uri uri = new Uri(url);

            IWebProxy result;
            if (EnvProxy.TryGetProxyFor(uri, out result) && result.GetProxy(uri) != uri)
                return result;

            result = WebRequest.GetSystemWebProxy();
            Uri address = result.GetProxy(uri);
            if (address == uri)
                return null;
        
            return new WebProxy(address)
            {
                Credentials = CredentialCache.DefaultCredentials,
                BypassProxyOnLocal = true
            };
        }

        internal static void FileMove(string oldPath, string newPath)
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

        public static bool ValidateHash(IFileSystemProxy fileSystem, string hashFile, string version, string paketFile)
        {
            if (hashFile == null)
            {
                ConsoleImpl.WriteTrace("No hash file expected, bypassing check.");
                return true;
            }
        
            if (!fileSystem.FileExists(hashFile))
            {
                ConsoleImpl.WriteInfo("No hash file of version {0} found.", version);

                return true;
            }

            var dict = fileSystem.ReadAllLines(hashFile)
                .Select(i => i.Split(' '))
                .ToDictionary(i => i[1], i => i[0]);

            string expectedHash;
            if (!dict.TryGetValue("paket.exe", out expectedHash))
            {
                fileSystem.DeleteFile(hashFile);

                throw new InvalidDataException("Paket hash file is corrupted");
            }

            using (var stream = fileSystem.OpenRead(paketFile))
            using (var sha = SHA256.Create())
            {
                byte[] checksum = sha.ComputeHash(stream);
                var hash = BitConverter.ToString(checksum).Replace("-", String.Empty);

                return string.Equals(expectedHash, hash, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
