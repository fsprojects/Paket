using System.IO;

namespace Paket.Bootstrapper.HelperProxies
{
    class FileProxy : IFileProxy
    {
        public bool Exists(string filename)
        {
            return File.Exists(filename);
        }

        public void Copy(string fileFrom, string fileTo, bool overwrite)
        {
            File.Copy(fileFrom, fileTo, overwrite);
        }
    }
}