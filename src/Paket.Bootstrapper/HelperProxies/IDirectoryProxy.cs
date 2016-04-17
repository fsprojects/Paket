using System.Collections.Generic;
using System.IO;

namespace Paket.Bootstrapper.HelperProxies
{
    public interface IDirectoryProxy
    {
        void CreateDirectory(string path);

        IEnumerable<string> GetDirectories(string path);
        bool Exists(string path);
        IEnumerable<string> EnumerateFiles(string path, string filter, SearchOption searchOption);
        void Delete(string path, bool recursive);
    }
}