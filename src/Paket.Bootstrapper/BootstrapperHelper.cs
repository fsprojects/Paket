using System;
using System.IO;
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
--output-dir=<PATH>            Download paket to the specified directory.
--as-tool                      Install the package as .net sdk tool.
--run <other args>             run the downloaded paket.exe with all following arguments";
        const string PaketBootstrapperUserAgent = "Paket.Bootstrapper";

        internal static string GetLocalFileVersion(string target, FileSystemProxy fileSystemProxy)
        {
            if (!File.Exists(target))
            {
                ConsoleImpl.WriteTrace("File doesn't exists, no version information: {0}", target);
                return "";
            }

            try
            {
                var bytes = new MemoryStream();
                using (var stream = fileSystemProxy.OpenRead(target))
                {
                    stream.CopyTo(bytes);
                }
                var attr = Assembly.Load(bytes.ToArray()).GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false).Cast<AssemblyInformationalVersionAttribute>().FirstOrDefault();
                if (attr == null)
                {
                    ConsoleImpl.WriteWarning("No assembly version found in {0}", target);
                    return "";
                }
                
                return attr.InformationalVersion;
            }
            catch (Exception exception)
            {
                ConsoleImpl.WriteWarning("Unable to get file version from {0}: {1}", target, exception);
                return "";
            }
        }

        internal static string GetTempFile(string name)
        {
            var path = Path.GetTempPath();
            var fileName = Path.Combine(path, name + System.Diagnostics.Process.GetCurrentProcess().Id);
            if (File.Exists(fileName))
                File.Delete(fileName);
            return fileName;
        }

        internal static void PrepareWebClient(WebClient client, string url, IEnvProxy envProxy)
        {
            client.Headers.Add("user-agent", PaketBootstrapperUserAgent);
            client.UseDefaultCredentials = true;
            client.Proxy = GetDefaultWebProxyFor(url, envProxy);
        }

        internal static HttpWebRequest PrepareWebRequest(string url, IEnvProxy envProxy)
        {
            var request = (HttpWebRequest)HttpWebRequest.Create(url);
            request.UserAgent = PaketBootstrapperUserAgent;
            request.UseDefaultCredentials = true;
            request.Proxy = GetDefaultWebProxyFor(url, envProxy);
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            return request;
        }

        internal static IWebProxy GetDefaultWebProxyFor(String url, IEnvProxy envProxy)
        {
            Uri uri = new Uri(url);

            IWebProxy result;
            if (envProxy.TryGetProxyFor(uri, out result) && result.GetProxy(uri) != uri)
                return result;

#if NO_SYSTEMWEBPROXY
            return null;
#else
            result = WebRequest.GetSystemWebProxy();
            Uri address = result.GetProxy(uri);
            if (address == uri)
                return null;
        
            return new WebProxy(address)
            {
                Credentials = CredentialCache.DefaultCredentials,
                BypassProxyOnLocal = true
            };
#endif
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

        public static bool ValidateHash(IFileSystemProxy fileSystem, PaketHashFile hashFile, string version, string paketFile)
        {
            if (hashFile == null)
            {
                ConsoleImpl.WriteTrace("No hashFile file expected, bypassing check.");
                return true;
            }
        
            var dict = hashFile.Content
                .Select(i => i.Split(' '))
                .ToDictionary(i => i[1], i => i[0]);

            string expectedHash;
            if (!dict.TryGetValue("paket.exe", out expectedHash))
            {
                throw new InvalidDataException("Paket hashFile file is corrupted");
            }

            using (var stream = fileSystem.OpenRead(paketFile))
            using (var sha = SHA256.Create())
            {
                byte[] checksum = sha.ComputeHash(stream);
                var hash = BitConverter.ToString(checksum).Replace("-", String.Empty);

                ConsoleImpl.WriteTrace("Expected hash  = {0}", expectedHash);
                ConsoleImpl.WriteTrace("paket.exe hash = {0} ({1})", hash, paketFile);
                return string.Equals(expectedHash, hash, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
