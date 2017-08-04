using System;
using System.Text;
using FileAccess = System.IO.FileAccess;
using FileMode = System.IO.FileMode;
using FileStream = System.IO.FileStream;
using StreamWriter = System.IO.StreamWriter;
using FileShare = System.IO.FileShare;
using FileOptions = System.IO.FileOptions;
using StreamReader = System.IO.StreamReader;
using FileAttributes = System.IO.FileAttributes;
using System.Security.AccessControl;

namespace Pri.LongPath
{
	public class FileInfo : FileSystemInfo
	{
		private readonly string name;

		public DirectoryInfo Directory
		{
			get
			{
				var dirName = DirectoryName;
				return dirName == null ? null : new DirectoryInfo(dirName);
			}
		}

	    public System.IO.FileInfo SysFileInfo
        {
	        get
	        {
	            return new System.IO.FileInfo(FullPath);
	        }
        }

	    public override System.IO.FileSystemInfo SystemInfo { get { return SysFileInfo; } }

        public string DirectoryName
		{
			get
			{
				return Path.GetDirectoryName(FullPath);
			}
		}

		public override bool Exists
		{
			get
			{
			    if (Common.IsRunningOnMono()) return SysFileInfo.Exists;

				if (state == State.Uninitialized)
				{
					Refresh();
				}
				return state == State.Initialized &&
				       (data.fileAttributes & System.IO.FileAttributes.Directory) != System.IO.FileAttributes.Directory;
			}
		}

		public long Length
		{
			get { return GetFileLength(); }
		}

		public override string Name
		{
			get { return name; }
		}

		public FileInfo(string fileName)
		{
			OriginalPath = fileName;
			FullPath = Path.GetFullPath(fileName);
			name = Path.GetFileName(fileName);
			DisplayPath = GetDisplayPath(fileName);
		}

		private string GetDisplayPath(string originalPath)
		{
			return originalPath;
		}

		private long GetFileLength()
		{
		    if (Common.IsRunningOnMono()) return SysFileInfo.Length;

            if (state == State.Uninitialized)
			{
				Refresh();
			}
			if(state == State.Error)
				Common.ThrowIOError(errorCode, FullPath);
			return ((long)data.fileSizeHigh) << 32 | (data.fileSizeLow & 0xFFFFFFFFL);
		}

		public StreamWriter AppendText()
		{
			return File.CreateStreamWriter(FullPath, true);
		}

		public FileInfo CopyTo(string destFileName)
		{
			return CopyTo(destFileName, false);
		}

		public FileInfo CopyTo(string destFileName, bool overwrite)
		{
			File.Copy(FullPath, destFileName, overwrite);
			return new FileInfo(destFileName);
		}

		public FileStream Create()
		{
			return File.Create(FullPath);
		}

		public StreamWriter CreateText()
		{
			return File.CreateStreamWriter(FullPath, false);
		}

		public override void Delete()
		{
			File.Delete(FullPath);
		}

		public void MoveTo(string destFileName)
		{
			File.Move(FullPath, destFileName);
		}

		public FileStream Open(FileMode mode)
		{
			return Open(mode, FileAccess.ReadWrite, FileShare.None);
		}

		public FileStream Open(FileMode mode, FileAccess access)
		{
			return Open(mode, access, FileShare.None);
		}

		public FileStream Open(FileMode mode, FileAccess access, FileShare share)
		{
		    if (Common.IsRunningOnMono()) return SysFileInfo.Open(mode, access, share);

            return File.Open(FullPath, mode, access, share, 4096, FileOptions.SequentialScan);
		}

		public FileStream OpenRead()
		{
		    if (Common.IsRunningOnMono()) return SysFileInfo.OpenRead();
            return File.Open(FullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.None);
		}

		public StreamReader OpenText()
		{
			return File.CreateStreamReader(FullPath, Encoding.UTF8, true, 1024);
		}

		public FileStream OpenWrite()
		{
			return File.Open(FullPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
		}

		public override string ToString()
		{
			return DisplayPath;
		}

		public void Encrypt()
		{
			File.Encrypt(FullPath);
		}

		public void Decrypt()
		{
			File.Decrypt(FullPath);
		}

		public bool IsReadOnly
		{
			get
			{
			    if (Common.IsRunningOnMono()) return SysFileInfo.IsReadOnly;

                return (Attributes & FileAttributes.ReadOnly) != 0;
			}
			set
			{
			    if (Common.IsRunningOnMono())
			    {
			        SysFileInfo.IsReadOnly = value;
			        return;
			    }

                if (value)
				{
					Attributes |= FileAttributes.ReadOnly;
					return;
				}
				Attributes &= ~FileAttributes.ReadOnly;
			}
		}

		public FileInfo Replace(string destinationFilename, string backupFilename)
		{
			return Replace(destinationFilename, backupFilename, false);
		}

		public FileInfo Replace(string destinationFilename, string backupFilename, bool ignoreMetadataErrors)
		{
			File.Replace(FullPath, destinationFilename, backupFilename, ignoreMetadataErrors);
			return new FileInfo(destinationFilename);
		}

#if netfx
        public FileSecurity GetAccessControl()
		{
			return File.GetAccessControl(FullPath);
		}

        public FileSecurity GetAccessControl(AccessControlSections includeSections)
		{
			return File.GetAccessControl(FullPath, includeSections);
		}

		public void SetAccessControl(FileSecurity security)
		{
			File.SetAccessControl(FullPath, security);
        }
#endif

    }
}