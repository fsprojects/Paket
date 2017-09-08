using System.IO;
using System.IO.Compression;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace Paket.Bootstrapper.HelperProxies
{
    class FileSystemProxy : IFileSystemProxy
    {
        // ReSharper disable once InconsistentNaming
        public const int HRESULT_ERROR_SHARING_VIOLATION = unchecked((int) 0x80070020);

        public string GetCurrentDirectory() { return Directory.GetCurrentDirectory(); }
        public bool FileExists(string filename) { return File.Exists(filename); }
        public void CopyFile(string fileFrom, string fileTo, bool overwrite) { File.Copy(fileFrom, fileTo, overwrite); }
        public void DeleteFile(string filename) { File.Delete(filename); }
        public Stream CreateFile(string filename) { return File.Create(filename); }
        public string GetLocalFileVersion(string filename) { return BootstrapperHelper.GetLocalFileVersion(filename, this); }
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

        private static Stream WaitForFileOpen(string path, FileMode filemode)
        {
            while (true)
            {
                try
                {
                    return File.Open(path, filemode, FileAccess.ReadWrite, FileShare.None);
                }
                catch (Exception exception)
                {
                    if (exception.HResult != HRESULT_ERROR_SHARING_VIOLATION)
                    {
                        throw;
                    }

                    Thread.Sleep(100);
                }
            }
        }

        public Stream CreateExclusive(string path)
        {
            return WaitForFileOpen(path, FileMode.Create);
        }

        public void WaitForFileFinished(string path)
        {
            WaitForFileOpen(path, FileMode.Open).Dispose();
        }

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
            using (var reader = new StreamReader(OpenRead(filename)))
            {
                var line = reader.ReadLine();
                while (line != null)
                {
                    yield return line;
                    line = reader.ReadLine();
                }
            }
        }

        public Stream OpenRead(string filename)
        {
            return WaitForFileOpen(filename, FileMode.Open);
        }
    }
}