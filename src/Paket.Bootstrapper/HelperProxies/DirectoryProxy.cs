using System.Collections.Generic;
using System.IO;

namespace Paket.Bootstrapper.HelperProxies
{
    internal class DirectoryProxy : IDirectoryProxy
    {
        public void CreateDirectory(string dir)
        {
            Directory.CreateDirectory(dir);
        }

        public IEnumerable<string> GetDirectories(string dir)
        {
            return Directory.GetDirectories(dir);
        }

        public bool Exists(string path)
        {
            return Directory.Exists(path);
        }

        public IEnumerable<string> EnumerateFiles(string path, string filter, SearchOption searchOption)
        {
            return Directory.EnumerateFiles(path, filter, searchOption);
        }

        public void Delete(string path, bool recursive)
        {
            Directory.Delete(path, recursive);
        }
    }
}