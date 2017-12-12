using System.IO;
using System.IO.Compression;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        // This limit is an upper value where we are nearly sure that an some deadlock hapenned and
        // not just a very slow process downloading a file for example.
        // It's there mainly to avoid hogging resources on build servers if something really, really
        // wrong happens.
        private static readonly TimeSpan WaitHighLimit = TimeSpan.FromMinutes(15);

        private static Stream WaitForFileOpen(string path, FileMode filemode, FileAccess fileAccess)
        {
            Stopwatch watch = null;
            int wait = 100;
            while (watch == null || watch.Elapsed < WaitHighLimit)
            {
                try
                {
                    var readOnly = fileAccess == FileAccess.Read;
                    return File.Open(path, filemode, fileAccess, readOnly ? FileShare.Read : FileShare.None);
                }
                catch (Exception exception)
                {
                    if (exception.HResult != HRESULT_ERROR_SHARING_VIOLATION)
                    {
                        throw;
                    }

                    if (watch == null)
                    {
                        watch = Stopwatch.StartNew();
                    }
                    Thread.Sleep(wait);
                    wait = Math.Max(wait + 10, 1000);
                }
            }

            throw new TimeoutException("Timeout while waiting for a file to be available");
        }

        public Stream CreateExclusive(string path)
        {
            return WaitForFileOpen(path, FileMode.Create, FileAccess.ReadWrite);
        }

        public void WaitForFileFinished(string path)
        {
            WaitForFileOpen(path, FileMode.Open, FileAccess.Read).Dispose();
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
            return WaitForFileOpen(filename, FileMode.Open, FileAccess.Read);
        }
    }
}