using System.IO;
using System.IO.Compression;
using Path = Pri.LongPath.Path;
using Directory = Pri.LongPath.Directory;
using DirectoryInfo = Pri.LongPath.DirectoryInfo;
using File = Pri.LongPath.File;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Paket.Bootstrapper.HelperProxies
{
    class FileSystemProxy : IFileSystemProxy
    {
        public string GetCurrentDirectory() { return Directory.GetCurrentDirectory(); }
        public bool FileExists(string filename) { return File.Exists(filename); }
        public void CopyFile(string fileFrom, string fileTo, bool overwrite) { File.Copy(fileFrom, fileTo, overwrite); }
        public void DeleteFile(string filename) { File.Delete(filename); }
        public Stream CreateFile(string filename) { return File.Create(filename); }
        public string GetLocalFileVersion(string filename) { return BootstrapperHelper.GetLocalFileVersion(filename); }
        public void MoveFile(string fromFile, string toFile) { BootstrapperHelper.FileMove(fromFile, toFile); }
        public void ExtractToDirectory(string zipFile, string targetLocation) { ZipFile.ExtractToDirectory(zipFile, targetLocation); }
        public DateTime GetLastWriteTime(string filename) { return new FileInfo(filename).LastWriteTime; }

        public void Touch(string filename)
        {
            var fileInfo = new FileInfo(filename);
            fileInfo.LastWriteTime = fileInfo.LastAccessTime = DateTimeProxy.Now;
        }

        public string GetExecutingAssemblyPath() { return Assembly.GetExecutingAssembly().Location; }
        public string GetTempPath() { return Path.GetTempPath(); }

        public void CreateDirectory(string dir) { Directory.CreateDirectory(dir); }
        public IEnumerable<string> GetDirectories(string dir) { return Directory.GetDirectories(dir); }
        public bool DirectoryExists(string path) { return Directory.Exists(path); }
        public void DeleteDirectory(string path, bool recursive) { Directory.Delete(path, recursive); }

        public IEnumerable<string> EnumerateFiles(string path, string filter, SearchOption searchOption)
        {
            return Directory.EnumerateFiles(path, filter, searchOption);
        }

        public IEnumerable<string> ReadAllLines(string filename)
        {
            return File.ReadAllLines(filename);
        }

        public Stream OpenRead(string filename)
        {
            return File.OpenRead(filename);
        }
    }
}