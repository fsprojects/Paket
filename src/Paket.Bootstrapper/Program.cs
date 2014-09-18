using System;
using System.Diagnostics;
using System.IO;
using System.Net;


namespace Paket.Bootstrapper
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var folder = "";
                if (args.Length > 1)
                    folder = args[0];

                var target = Path.Combine(folder, "paket.exe");
                var localVersion = "";
                if (File.Exists(target))
                {
                    FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(target);
                    localVersion = fvi.FileVersion;
                }

                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("user-agent", "Paket.Bootstrapper");
                    var releasesUrl = "https://api.github.com/repos/fsprojects/Paket/releases";
                    var data = client.DownloadString(releasesUrl);
                    var start = data.IndexOf("tag_name") + 11;
                    var end = data.IndexOf("\"", start);
                    var latestVersion = data.Substring(start, end - start);

                    if (!localVersion.StartsWith(latestVersion))
                    {
                        var url = String.Format("https://github.com/fsprojects/Paket/releases/download/{0}/paket.exe", latestVersion);

                        Console.WriteLine("Starting download from {0}", url);

                        var request = (HttpWebRequest)HttpWebRequest.Create(url);
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
            }
            catch (Exception exn)
            {
                Environment.ExitCode = 1;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(exn.Message);
            }
        }
    }
}
