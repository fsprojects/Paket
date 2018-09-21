using Paket.Bootstrapper.HelperProxies;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paket.Bootstrapper.InstallKind
{
    public class InstallAsTool
    {
        private IFileSystemProxy FileSystemProxy { get; set; }

        public InstallAsTool(IFileSystemProxy fileSystemProxy)
        {
            FileSystemProxy = fileSystemProxy;
        }

        private string CreateNugetConfigForBootstrapper(string paketToolNupkgDir)
        {
            string path = Path.GetTempFileName();
            ConsoleImpl.WriteTrace(string.Format("Create nuget config for dotnet install in '{0}'", path));
            ConsoleImpl.WriteTrace(string.Format("Path of local feed '{0}'", paketToolNupkgDir));

            var text = new[]
                {
"<?xml version=\"1.0\" encoding=\"utf-8\"?>",
"<configuration>",
"<packageSources>",
"    <!--To inherit the global NuGet package sources remove the <clear/> line below -->",
"    <clear />",
string.Format("    <add key=\"download_paket_tool\" value=\"{0}\" />", paketToolNupkgDir),
"</packageSources>",
"</configuration>"
                };
            File.WriteAllText(path, string.Join(System.Environment.NewLine, text));
            return path;
        }

        private int Dotnet(string argString)
        {
            ConsoleImpl.WriteInfo("Running dotnet {0}", argString);
            var process = new System.Diagnostics.Process
            {
                StartInfo =
                {
                    FileName = "dotnet",
                    Arguments = argString,
                    CreateNoWindow = true,
                    UseShellExecute = false
                }
            };
            process.Start();
            process.WaitForExit();
            return process.ExitCode;
        }

        public void Run(string pkgDir, string target, string version)
        {
            string toolWorkDir = Path.GetDirectoryName(target);

            string bootstrapperNugetConfig = CreateNugetConfigForBootstrapper(pkgDir);

            int exitCode = Dotnet(String.Format(@"tool install paket --version {2} --tool-path ""{0}"" --configfile ""{1}""", toolWorkDir, bootstrapperNugetConfig, version));
            if (exitCode != 0)
            {
                Environment.Exit(exitCode);
            }
        }
    }
}
