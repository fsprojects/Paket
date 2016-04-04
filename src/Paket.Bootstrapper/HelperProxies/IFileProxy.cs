using System;
using System.IO;

namespace Paket.Bootstrapper.HelperProxies
{
    public interface IFileProxy
    {
        bool Exists(string filename);
        void Copy(string fileFrom, string fileTo, bool overwrite = false);
        void Delete(string filename);
        Stream Create(string tmpFile);
        string GetLocalFileVersion(string filename);
        void FileMove(string fromFile, string toFile);
        void ExtractToDirectory(string zipFile, string targetLocation);
    }
}