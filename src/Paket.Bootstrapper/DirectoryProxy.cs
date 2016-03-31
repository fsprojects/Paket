using System.Collections.Generic;
using System.IO;

namespace Paket.Bootstrapper
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
    }
}