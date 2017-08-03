using System;
using System.Collections.Generic;
using System.IO;
using Path = Pri.LongPath.Path;
using Directory = Pri.LongPath.Directory;
using DirectoryInfo = Pri.LongPath.DirectoryInfo;
using File = Pri.LongPath.File;

namespace Paket.Bootstrapper.HelperProxies
{
    public interface IFileSystemProxy
    {
        string GetCurrentDirectory();
        bool FileExists(string filename);
        void CopyFile(string fileFrom, string fileTo, bool overwrite = false);
        void DeleteFile(string filename);
        Stream CreateFile(string tmpFile);
        string GetLocalFileVersion(string filename);
        void MoveFile(string fromFile, string toFile);
        void ExtractToDirectory(string zipFile, string targetLocation);
        DateTime GetLastWriteTime(string filename);
        void Touch(string filename);
        IEnumerable<string> ReadAllLines(string filename);
        Stream OpenRead(string filename);

        void CreateDirectory(string path);
        IEnumerable<string> GetDirectories(string path);
        bool DirectoryExists(string path);
        IEnumerable<string> EnumerateFiles(string path, string filter, SearchOption searchOption);
        void DeleteDirectory(string path, bool recursive);

        string GetExecutingAssemblyPath();
        string GetTempPath();
    }
}