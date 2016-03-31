using System.Collections.Generic;

namespace Paket.Bootstrapper
{
    public interface IDirectoryProxy
    {
        void CreateDirectory(string paketCacheDir);

        IEnumerable<string> GetDirectories(string paketCacheDir);
    }
}