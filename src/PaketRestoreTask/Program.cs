using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Paket.Build.Tasks
{
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

        /// <summary>
        /// Output items
        /// </summary>
        [Output]
        public ITaskItem[] NewPackageReferences { get; set; }

        [Output]
        public string AlternativeConfigFile { get; set; }

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
                ITaskItem dependency = new TaskItem(splitted[0].Trim());
                dependency.SetMetadata("Version", splitted[1].Trim());
                list.Add(dependency);
            }
            
            this.NewPackageReferences = list.ToArray();

            AlternativeConfigFile = Path.Combine(fileInfo.Directory.FullName, "obj", fileInfo.Name + ".NuGet.Config");

            return true;
        }
    }
}