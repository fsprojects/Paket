using System.IO;
using System.IO.Compression;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Paket.Bootstrapper.HelperProxies
{
    class FileSystemProxy : IFileSystemProxy
    {
        public bool FileExists(string filename) => File.Exists(filename);
        public void CopyFile(string fileFrom, string fileTo, bool overwrite) => File.Copy(fileFrom, fileTo, overwrite);
        public void DeleteFile(string filename) => File.Delete(filename);
        public Stream CreateFile(string filename) => File.Create(filename);
        public string GetLocalFileVersion(string filename) => BootstrapperHelper.GetLocalFileVersion(filename);
        public void MoveFile(string fromFile, string toFile) => BootstrapperHelper.FileMove(fromFile, toFile);
        public void ExtractToDirectory(string zipFile, string targetLocation) => ZipFile.ExtractToDirectory(zipFile, targetLocation);
        public DateTime GetLastWriteTime(string filename) => new FileInfo(filename).LastWriteTime;

        public void Touch(string filename)
        {
            var fileInfo = new FileInfo(filename);
            fileInfo.LastWriteTime = fileInfo.LastAccessTime = DateTimeProxy.Now;
        }

        public string GetExecutingAssemblyPath() => Assembly.GetExecutingAssembly().Location;
        public string GetTempPath() => Path.GetTempPath();

        public void CreateDirectory(string dir) => Directory.CreateDirectory(dir);
        public IEnumerable<string> GetDirectories(string dir) => Directory.GetDirectories(dir);
        public bool DirectoryExists(string path) => Directory.Exists(path);
        public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);

        public IEnumerable<string> EnumerateFiles(string path, string filter, SearchOption searchOption)
        {
            return Directory.EnumerateFiles(path, filter, searchOption);
        }
    }
}