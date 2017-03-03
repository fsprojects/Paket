using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Paket.Build.Tasks
{
    public class Sha512TheFile : Task
    {
        [Required]
        public string InputFile { get; set; }

        [Required]
        public string OutputFile { get; set; }

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.Normal, $"SHA512 '{InputFile} -> {OutputFile}");

            using (var sha = System.Security.Cryptography.SHA512.Create())
            using (var fs = File.OpenRead(InputFile))
            {
                string hash = Convert.ToBase64String(sha.ComputeHash(fs));
                File.WriteAllText(OutputFile, hash);
                return true;
            }
        }

    }

    public class PaketRestoreTask : Task
    {

        /// <summary>
        /// Full path to the msbuild project.
        /// </summary>
        [Required]
        public string ProjectUniqueName { get; set; }

        [Required]
        public ITaskItem[] PackageReferences { get; set; }

        /// <summary>
        /// Target frameworks to apply this for. If empty this applies to all.
        /// </summary>
        public string TargetFrameworks { get; set; }

        public string PaketPackageCache { get; set; }

        /// <summary>
        /// Output items
        /// </summary>
        [Output]
        public ITaskItem[] NewPackageReferences { get; set; }

        [Output]
        public string AlternativeConfigFile { get; set; }

        public string GetRealVersionForPackage(string packageName)
        {
            string paketPackageCachedDir = Path.Combine(PaketPackageCache, packageName);

            string nupkg = 
                Directory.GetFiles(paketPackageCachedDir, $"{packageName}.*.nupkg")
                         .FirstOrDefault();
            
            if (nupkg == null)
                throw new Exception($"Nupkg for package {packageName} not found in dir '{paketPackageCachedDir}'");

            // path/to/System.IO.4.3.0.nupkg -> 4.3.0
            return Path.GetFileNameWithoutExtension(nupkg).Replace($"{packageName}.", "");
        }

        public override bool Execute()
        {            
            var fileInfo = new FileInfo(ProjectUniqueName);
            var fileName = Path.Combine(fileInfo.Directory.FullName, "obj", fileInfo.Name + ".references");
            var lines = System.IO.File.ReadAllLines(fileName);
            var list = new System.Collections.Generic.List<ITaskItem>();
            list.AddRange(PackageReferences);
            char[] delimiterChars = {  ','};

            foreach (var line in lines)
            {
                var splitted = line.Split(delimiterChars);
                if(splitted.Length < 2)
                    break;

                string packageName = splitted[0].Trim();
                string packageVersion = splitted[1].Trim();

                ITaskItem dependency = new TaskItem(packageName);
                dependency.SetMetadata("Version", GetRealVersionForPackage(packageName));

                list.Add(dependency);
            }
            
            this.NewPackageReferences = list.ToArray();

            AlternativeConfigFile = Path.Combine(fileInfo.Directory.FullName, "obj", fileInfo.Name + ".NuGet.Config");

            return true;
        }
    }
}