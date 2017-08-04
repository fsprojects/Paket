using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.AccessControl;
#if !NET_2_0
using System.Linq;
#endif

namespace Pri.LongPath
{
	using SafeFileHandle = Microsoft.Win32.SafeHandles.SafeFileHandle;
	using FileAccess = System.IO.FileAccess;
	using FileMode = System.IO.FileMode;
	using FileOptions = System.IO.FileOptions;
	using FileShare = System.IO.FileShare;
	using SearchOption = System.IO.SearchOption;

	public static class Directory
	{
		internal static SafeFileHandle GetDirectoryHandle(string normalizedPath)
		{
			var handle = NativeMethods.CreateFile(normalizedPath,
				NativeMethods.EFileAccess.GenericWrite,
				(uint)(FileShare.Write | FileShare.Delete),
				IntPtr.Zero, (int)FileMode.Open, NativeMethods.FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero);
			if (!handle.IsInvalid) return handle;
			var ex = Common.GetExceptionFromLastWin32Error();
			Console.WriteLine("error {0} with {1}\n{2}", ex.Message, normalizedPath, ex.StackTrace);
			throw ex;
		}
#if EXTRAS
		public static void SetAttributes(string path, System.IO.FileAttributes fileAttributes)
		{
			Common.SetAttributes(path, fileAttributes);
		}

		public static System.IO.FileAttributes GetAttributes(string path)
		{
			return Common.GetAttributes(path);
		}
#endif // EXTRAS

		public static string GetCurrentDirectory()
		{
			return Path.RemoveLongPathPrefix(Path.NormalizeLongPath("."));
		}

		public static void Delete(string path, bool recursive)
		{
		    if (Common.IsRunningOnMono())
		    {
		        System.IO.Directory.Delete(path, recursive);
		        return;
		    }

            /* MSDN: https://msdn.microsoft.com/en-us/library/fxeahc5f.aspx
			   The behavior of this method differs slightly when deleting a directory that contains a reparse point, 
			   such as a symbolic link or a mount point. 
			   (1) If the reparse point is a directory, such as a mount point, it is unmounted and the mount point is deleted. 
			   This method does not recurse through the reparse point. 
			   (2) If the reparse point is a symbolic link to a file, the reparse point is deleted and not the target of 
			   the symbolic link.
			*/

            try 
			{
				var reparseFlags = (System.IO.FileAttributes.Directory | System.IO.FileAttributes.ReparsePoint);
				var isDirectoryReparsePoint = (Common.GetAttributes(path) & reparseFlags) == reparseFlags;

				if (isDirectoryReparsePoint) {
					Delete(path);
					return;
				}
			}
			catch (System.IO.FileNotFoundException) {
				// ignore: not there when we try to delete, it doesn't matter
			}

			if (recursive == false) 
			{
				Delete(path);
				return;
			}

			try
			{
				foreach (var file in EnumerateFileSystemEntries(path, "*", false, true, SearchOption.TopDirectoryOnly))
				{
					File.Delete(file);
				}
			}
			catch (System.IO.FileNotFoundException)
			{
				// ignore: not there when we try to delete, it doesn't matter
			}

			try
			{
				foreach (var subPath in EnumerateFileSystemEntries(path, "*", true, false, SearchOption.TopDirectoryOnly))
				{
					Delete(subPath, true);
				}
			}
			catch (System.IO.FileNotFoundException)
			{
				// ignore: not there when we try to delete, it doesn't matter
			}

			try
			{
				Delete(path);
			}
			catch (System.IO.FileNotFoundException)
			{
				// ignore: not there when we try to delete, it doesn't matter
			}
		}

		/// <summary>
		///     Deletes the specified empty directory.
		/// </summary>
		/// <param name="path">
		///      A <see cref="string"/> containing the path of the directory to delete.
		/// </param>
		/// <exception cref="ArgumentNullException">
		///     <paramref name="path"/> is <see langword="null"/>.
		/// </exception>
		/// <exception cref="ArgumentException">
		///     <paramref name="path"/> is an empty string (""), contains only white
		///     space, or contains one or more invalid characters as defined in
		///     <see cref="Path.GetInvalidPathChars()"/>.
		///     <para>
		///         -or-
		///     </para>
		///     <paramref name="path"/> contains one or more components that exceed
		///     the drive-defined maximum length. For example, on Windows-based
		///     platforms, components must not exceed 255 characters.
		/// </exception>
		/// <exception cref="System.IO.PathTooLongException">
		///     <paramref name="path"/> exceeds the system-defined maximum length.
		///     For example, on Windows-based platforms, paths must not exceed
		///     32,000 characters.
		/// </exception>
		/// <exception cref="System.IO.DirectoryNotFoundException">
		///     <paramref name="path"/> could not be found.
		/// </exception>
		/// <exception cref="UnauthorizedAccessException">
		///     The caller does not have the required access permissions.
		///     <para>
		///         -or-
		///     </para>
		///     <paramref name="path"/> refers to a directory that is read-only.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		///     <paramref name="path"/> is a file.
		///     <para>
		///         -or-
		///     </para>
		///     <paramref name="path"/> refers to a directory that is not empty.
		///     <para>
		///         -or-
		///     </para>
		///     <paramref name="path"/> refers to a directory that is in use.
		///     <para>
		///         -or-
		///     </para>
		///     <paramref name="path"/> specifies a device that is not ready.
		/// </exception>
		public static void Delete(string path)
		{
		    if (Common.IsRunningOnMono())
		    {
		        System.IO.Directory.Delete(path);
		    }

		    var normalizedPath = Path.NormalizeLongPath(path);
			if (!NativeMethods.RemoveDirectory(normalizedPath))
			{
				throw Common.GetExceptionFromLastWin32Error();
			}
		}

		/// <summary>
		///     Returns a value indicating whether the specified path refers to an existing directory.
		/// </summary>
		/// <param name="path">
		///     A <see cref="string"/> containing the path to check.
		/// </param>
		/// <returns>
		///     <see langword="true"/> if <paramref name="path"/> refers to an existing directory;
		///     otherwise, <see langword="false"/>.
		/// </returns>
		/// <remarks>
		///     Note that this method will return false if any error occurs while trying to determine
		///     if the specified directory exists. This includes situations that would normally result in
		///     thrown exceptions including (but not limited to); passing in a directory name with invalid
		///     or too many characters, an I/O error such as a failing or missing disk, or if the caller
		///     does not have Windows or Code Access Security (CAS) permissions to to read the directory.
		/// </remarks>
		public static bool Exists(string path)
		{
		    if (Common.IsRunningOnMono()) return System.IO.Directory.Exists(path);

            bool isDirectory;
			return Common.Exists(path, out isDirectory) && isDirectory;
		}

#if NET_4_0 || NET_4_5
		/// <summary>
		///     Returns a enumerable containing the directory names of the specified directory.
		/// </summary>
		/// <param name="path">
		///     A <see cref="string"/> containing the path of the directory to search.
		/// </param>
		/// <returns>
		///     A <see cref="IEnumerable{T}"/> containing the directory names within <paramref name="path"/>.
		/// </returns>
		/// <exception cref="ArgumentNullException">
		///     <paramref name="path"/> is <see langword="null"/>.
		/// </exception>
		/// <exception cref="ArgumentException">
		///     <paramref name="path"/> is an empty string (""), contains only white
		///     space, or contains one or more invalid characters as defined in
		///     <see cref="Path.GetInvalidPathChars()"/>.
		///     <para>
		///         -or-
		///     </para>
		///     <paramref name="path"/> contains one or more components that exceed
		///     the drive-defined maximum length. For example, on Windows-based
		///     platforms, components must not exceed 255 characters.
		/// </exception>
		/// <exception cref="System.IO.PathTooLongException">
		///     <paramref name="path"/> exceeds the system-defined maximum length.
		///     For example, on Windows-based platforms, paths must not exceed
		///     32,000 characters.
		/// </exception>
		/// <exception cref="System.IO.DirectoryNotFoundException">
		///     <paramref name="path"/> contains one or more directories that could not be
		///     found.
		/// </exception>
		/// <exception cref="UnauthorizedAccessException">
		///     The caller does not have the required access permissions.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		///     <paramref name="path"/> is a file.
		///     <para>
		///         -or-
		///     </para>
		///     <paramref name="path"/> specifies a device that is not ready.
		/// </exception>
		public static IEnumerable<string> EnumerateDirectories(string path)
		{
		    if (Common.IsRunningOnMono()) return System.IO.Directory.EnumerateDirectories(path);

            return EnumerateFileSystemEntries(path, "*", true, false, SearchOption.TopDirectoryOnly);
		}
#endif

#if NET_4_5
		/// <summary>
		///     Returns a enumerable containing the directory names of the specified directory that
		///     match the specified search pattern.
		/// </summary>
		/// <param name="path">
		///     A <see cref="String"/> containing the path of the directory to search.
		/// </param>
		/// <param name="searchPattern">
		///     A <see cref="String"/> containing search pattern to match against the names of the
		///     directories in <paramref name="path"/>, otherwise, <see langword="null"/> or an empty
		///     string ("") to use the default search pattern, "*".
		/// </param>
		/// <returns>
		///     A <see cref="IEnumerable{T}"/> containing the directory names within <paramref name="path"/>
		///     that match <paramref name="searchPattern"/>.
		/// </returns>
		/// <exception cref="ArgumentNullException">
		///     <paramref name="path"/> is <see langword="null"/>.
		/// </exception>
		/// <exception cref="ArgumentException">
		///     <paramref name="path"/> is an empty string (""), contains only white
		///     space, or contains one or more invalid characters as defined in
		///     <see cref="Path.GetInvalidPathChars()"/>.
		///     <para>
		///         -or-
		///     </para>
		///     <paramref name="path"/> contains one or more components that exceed
		///     the drive-defined maximum length. For example, on Windows-based
		///     platforms, components must not exceed 255 characters.
		/// </exception>
		/// <exception cref="System.IO.PathTooLongException">
		///     <paramref name="path"/> exceeds the system-defined maximum length.
		///     For example, on Windows-based platforms, paths must not exceed
		///     32,000 characters.
		/// </exception>
		/// <exception cref="System.IO.DirectoryNotFoundException">
		///     <paramref name="path"/> contains one or more directories that could not be
		///     found.
		/// </exception>
		/// <exception cref="UnauthorizedAccessException">
		///     The caller does not have the required access permissions.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		///     <paramref name="path"/> is a file.
		///     <para>
		///         -or-
		///     </para>
		///     <paramref name="path"/> specifies a device that is not ready.
		/// </exception>
		public static IEnumerable<string> EnumerateDirectories(string path, string searchPattern)
		{
		    if (Common.IsRunningOnMono()) return System.IO.Directory.EnumerateDirectories(path, searchPattern);

            return EnumerateFileSystemEntries(path, searchPattern, true, false, System.IO.SearchOption.TopDirectoryOnly);
		}

		public static IEnumerable<string> EnumerateDirectories(string path, string searchPattern, System.IO.SearchOption options)
		{
		    if (Common.IsRunningOnMono()) return System.IO.Directory.EnumerateDirectories(path, searchPattern, options);


            return EnumerateFileSystemEntries(path, searchPattern, true, false, options);
		}
#endif

#if NET_4_0 || NET_4_5
		/// <summary>
		///     Returns a enumerable containing the file names of the specified directory.
		/// </summary>
		/// <param name="path">
		///     A <see cref="string"/> containing the path of the directory to search.
		/// </param>
		/// <returns>
		///     A <see cref="IEnumerable{T}"/> containing the file names within <paramref name="path"/>.
		/// </returns>
		/// <exception cref="ArgumentNullException">
		///     <paramref name="path"/> is <see langword="null"/>.
		/// </exception>
		/// <exception cref="ArgumentException">
		///     <paramref name="path"/> is an empty string (""), contains only white
		///     space, or contains one or more invalid characters as defined in
		///     <see cref="Path.GetInvalidPathChars()"/>.
		///     <para>
		///         -or-
		///     </para>
		///     <paramref name="path"/> contains one or more components that exceed
		///     the drive-defined maximum length. For example, on Windows-based
		///     platforms, components must not exceed 255 characters.
		/// </exception>
		/// <exception cref="System.IO.PathTooLongException">
		///     <paramref name="path"/> exceeds the system-defined maximum length.
		///     For example, on Windows-based platforms, paths must not exceed
		///     32,000 characters.
		/// </exception>
		/// <exception cref="System.IO.DirectoryNotFoundException">
		///     <paramref name="path"/> contains one or more directories that could not be
		///     found.
		/// </exception>
		/// <exception cref="UnauthorizedAccessException">
		///     The caller does not have the required access permissions.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		///     <paramref name="path"/> is a file.
		///     <para>
		///         -or-
		///     </para>
		///     <paramref name="path"/> specifies a device that is not ready.
		/// </exception>
		public static IEnumerable<string> EnumerateFiles(string path)
		{
		    if (Common.IsRunningOnMono()) return System.IO.Directory.EnumerateFiles(path);

            return EnumerateFileSystemEntries(path, "*", false, true, SearchOption.TopDirectoryOnly);
		}

		public static IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption options)
		{
		    if (Common.IsRunningOnMono()) return System.IO.Directory.EnumerateFiles(path, searchPattern, options);

            return EnumerateFileSystemEntries(path, searchPattern, false, true, options);
		}

		/// <summary>
		///     Returns a enumerable containing the file names of the specified directory that
		///     match the specified search pattern.
		/// </summary>
		/// <param name="path">
		///     A <see cref="string"/> containing the path of the directory to search.
		/// </param>
		/// <param name="searchPattern">
		///     A <see cref="string"/> containing search pattern to match against the names of the
		///     files in <paramref name="path"/>, otherwise, <see langword="null"/> or an empty
		///     string ("") to use the default search pattern, "*".
		/// </param>
		/// <returns>
		///     A <see cref="IEnumerable{T}"/> containing the file names within <paramref name="path"/>
		///     that match <paramref name="searchPattern"/>.
		/// </returns>
		/// <exception cref="ArgumentNullException">
		///     <paramref name="path"/> is <see langword="null"/>.
		/// </exception>
		/// <exception cref="ArgumentException">
		///     <paramref name="path"/> is an empty string (""), contains only white
		///     space, or contains one or more invalid characters as defined in
		///     <see cref="Path.GetInvalidPathChars()"/>.
		///     <para>
		///         -or-
		///     </para>
		///     <paramref name="path"/> contains one or more components that exceed
		///     the drive-defined maximum length. For example, on Windows-based
		///     platforms, components must not exceed 255 characters.
		/// </exception>
		/// <exception cref="System.IO.PathTooLongException">
		///     <paramref name="path"/> exceeds the system-defined maximum length.
		///     For example, on Windows-based platforms, paths must not exceed
		///     32,000 characters.
		/// </exception>
		/// <exception cref="System.IO.DirectoryNotFoundException">
		///     <paramref name="path"/> contains one or more directories that could not be
		///     found.
		/// </exception>
		/// <exception cref="UnauthorizedAccessException">
		///     The caller does not have the required access permissions.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		///     <paramref name="path"/> is a file.
		///     <para>
		///         -or-
		///     </para>
		///     <paramref name="path"/> specifies a device that is not ready.
		/// </exception>
		public static IEnumerable<string> EnumerateFiles(string path, string searchPattern)
		{
		    if (Common.IsRunningOnMono()) return System.IO.Directory.EnumerateFiles(path, searchPattern);

            return EnumerateFileSystemEntries(path, searchPattern, false, true, SearchOption.TopDirectoryOnly);
		}

		/// <summary>
		///     Returns a enumerable containing the file and directory names of the specified directory.
		/// </summary>
		/// <param name="path">
		///     A <see cref="string"/> containing the path of the directory to search.
		/// </param>
		/// <returns>
		///     A <see cref="IEnumerable{T}"/> containing the file and directory names within
		///     <paramref name="path"/>.
		/// </returns>
		/// <exception cref="ArgumentNullException">
		///     <paramref name="path"/> is <see langword="null"/>.
		/// </exception>
		/// <exception cref="ArgumentException">
		///     <paramref name="path"/> is an empty string (""), contains only white
		///     space, or contains one or more invalid characters as defined in
		///     <see cref="Path.GetInvalidPathChars()"/>.
		///     <para>
		///         -or-
		///     </para>
		///     <paramref name="path"/> contains one or more components that exceed
		///     the drive-defined maximum length. For example, on Windows-based
		///     platforms, components must not exceed 255 characters.
		/// </exception>
		/// <exception cref="System.IO.PathTooLongException">
		///     <paramref name="path"/> exceeds the system-defined maximum length.
		///     For example, on Windows-based platforms, paths must not exceed
		///     32,000 characters.
		/// </exception>
		/// <exception cref="System.IO.DirectoryNotFoundException">
		///     <paramref name="path"/> contains one or more directories that could not be
		///     found.
		/// </exception>
		/// <exception cref="UnauthorizedAccessException">
		///     The caller does not have the required access permissions.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		///     <paramref name="path"/> is a file.
		///     <para>
		///         -or-
		///     </para>
		///     <paramref name="path"/> specifies a device that is not ready.
		/// </exception>
		public static IEnumerable<string> EnumerateFileSystemEntries(string path)
		{
		    if (Common.IsRunningOnMono()) return System.IO.Directory.EnumerateFileSystemEntries(path);

            return EnumerateFileSystemEntries(path, null, true, true, SearchOption.TopDirectoryOnly);
		}

		/// <summary>
		///     Returns a enumerable containing the file and directory names of the specified directory
		///     that match the specified search pattern.
		/// </summary>
		/// <param name="path">
		///     A <see cref="string"/> containing the path of the directory to search.
		/// </param>
		/// <param name="searchPattern">
		///     A <see cref="string"/> containing search pattern to match against the names of the
		///     files and directories in <paramref name="path"/>, otherwise, <see langword="null"/>
		///     or an empty string ("") to use the default search pattern, "*".
		/// </param>
		/// <returns>
		///     A <see cref="IEnumerable{T}"/> containing the file and directory names within
		///     <paramref name="path"/>that match <paramref name="searchPattern"/>.
		/// </returns>
		/// <exception cref="ArgumentNullException">
		///     <paramref name="path"/> is <see langword="null"/>.
		/// </exception>
		/// <exception cref="ArgumentException">
		///     <paramref name="path"/> is an empty string (""), contains only white
		///     space, or contains one or more invalid characters as defined in
		///     <see cref="Path.GetInvalidPathChars()"/>.
		///     <para>
		///         -or-
		///     </para>
		///     <paramref name="path"/> contains one or more components that exceed
		///     the drive-defined maximum length. For example, on Windows-based
		///     platforms, components must not exceed 255 characters.
		/// </exception>
		/// <exception cref="System.IO.PathTooLongException">
		///     <paramref name="path"/> exceeds the system-defined maximum length.
		///     For example, on Windows-based platforms, paths must not exceed
		///     32,000 characters.
		/// </exception>
		/// <exception cref="System.IO.DirectoryNotFoundException">
		///     <paramref name="path"/> contains one or more directories that could not be
		///     found.
		/// </exception>
		/// <exception cref="UnauthorizedAccessException">
		///     The caller does not have the required access permissions.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		///     <paramref name="path"/> is a file.
		///     <para>
		///         -or-
		///     </para>
		///     <paramref name="path"/> specifies a device that is not ready.
		/// </exception>
		public static IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern)
		{
		    if (Common.IsRunningOnMono()) return System.IO.Directory.EnumerateFileSystemEntries(path, searchPattern);

            return EnumerateFileSystemEntries(path, searchPattern, true, true, SearchOption.TopDirectoryOnly);
		}

		public static IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption options)
		{
		    if (Common.IsRunningOnMono()) return System.IO.Directory.EnumerateFileSystemEntries(path, searchPattern, options);

            return EnumerateFileSystemEntries(path, searchPattern, true, true, options);
		}
#endif // NET_4_0 || NET_4_5

		internal static IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, bool includeDirectories, bool includeFiles, SearchOption option)
		{
            var normalizedSearchPattern = Common.NormalizeSearchPattern(searchPattern);
			var normalizedPath = Path.NormalizeLongPath(path);

			return EnumerateNormalizedFileSystemEntries(includeDirectories, includeFiles, option, normalizedPath, normalizedSearchPattern);
		}

		private static IEnumerable<string> EnumerateNormalizedFileSystemEntries(bool includeDirectories, bool includeFiles,
			SearchOption option, string normalizedPath, string normalizedSearchPattern)
		{
			// First check whether the specified path refers to a directory and exists
			System.IO.FileAttributes attributes;
			var errorCode = Common.TryGetDirectoryAttributes(normalizedPath, out attributes);
			if (errorCode != 0)
			{
				throw Common.GetExceptionFromWin32Error(errorCode);
			}

			if (option == SearchOption.AllDirectories)
				return EnumerateFileSystemIteratorRecursive(normalizedPath, normalizedSearchPattern, includeDirectories,
					includeFiles);

			return EnumerateFileSystemIterator(normalizedPath, normalizedSearchPattern, includeDirectories, includeFiles);
		}

		private static IEnumerable<string> EnumerateFileSystemIterator(string normalizedPath, string normalizedSearchPattern, bool includeDirectories, bool includeFiles)
		{
			// NOTE: Any exceptions thrown from this method are thrown on a call to IEnumerator<string>.MoveNext()

			var path = Common.IsPathUnc(normalizedPath) ? normalizedPath : Path.RemoveLongPathPrefix(normalizedPath);

			NativeMethods.WIN32_FIND_DATA findData;
			using (var handle = BeginFind(Path.Combine(normalizedPath, normalizedSearchPattern), out findData))
			{
				if (handle == null)
					yield break;

				do
				{
					if (IsDirectory(findData.dwFileAttributes))
					{
						if (IsCurrentOrParentDirectory(findData.cFileName)) continue;

						if (includeDirectories)
						{
							yield return Path.Combine(Path.RemoveLongPathPrefix(path), findData.cFileName);
						}
					}
					else
					{
						if (includeFiles)
						{
							yield return Path.Combine(Path.RemoveLongPathPrefix(path), findData.cFileName);
						}
					}
				} while (NativeMethods.FindNextFile(handle, out findData));

				var errorCode = Marshal.GetLastWin32Error();
				if (errorCode != NativeMethods.ERROR_NO_MORE_FILES)
					throw Common.GetExceptionFromWin32Error(errorCode);
			}
		}

		private static IEnumerable<string> EnumerateFileSystemIteratorRecursive(string normalizedPath, string normalizedSearchPattern, bool includeDirectories, bool includeFiles)
		{
			// NOTE: Any exceptions thrown from this method are thrown on a call to IEnumerator<string>.MoveNext()
			var pendingDirectories = new Queue<string>();
			pendingDirectories.Enqueue(normalizedPath);
			while (pendingDirectories.Count > 0)
			{
				normalizedPath = pendingDirectories.Dequeue();
				// get all subdirs to recurse in the next iteration
				foreach (var subdir in EnumerateNormalizedFileSystemEntries(true, false, SearchOption.TopDirectoryOnly, normalizedPath, "*"))
				{
					pendingDirectories.Enqueue(Path.NormalizeLongPath(subdir));
				}

				var path = Common.IsPathUnc(normalizedPath) ? normalizedPath : Path.RemoveLongPathPrefix(normalizedPath);

				NativeMethods.WIN32_FIND_DATA findData;
				using (var handle = BeginFind(Path.Combine(normalizedPath, normalizedSearchPattern), out findData))
				{
					if (handle == null)
						continue;

					do
					{
						var fullPath = Path.Combine(path, findData.cFileName);
						if (IsDirectory(findData.dwFileAttributes))
						{
							if (IsCurrentOrParentDirectory(findData.cFileName)) continue;
							var fullNormalizedPath = Path.Combine(normalizedPath, findData.cFileName);
							System.Diagnostics.Debug.Assert(Exists(fullPath));
							System.Diagnostics.Debug.Assert(Exists(Common.IsPathUnc(fullNormalizedPath) ? fullNormalizedPath : Path.RemoveLongPathPrefix(fullNormalizedPath)));

							if (includeDirectories)
							{
								yield return Path.RemoveLongPathPrefix(fullPath);
							}
						}
						else if (includeFiles)
						{
							yield return Path.RemoveLongPathPrefix(fullPath);
						}
					} while (NativeMethods.FindNextFile(handle, out findData));

					var errorCode = Marshal.GetLastWin32Error();
					if (errorCode != NativeMethods.ERROR_NO_MORE_FILES)
						throw Common.GetExceptionFromWin32Error(errorCode);
				}
			}
		}

		internal static SafeFindHandle BeginFind(string normalizedPathWithSearchPattern,
			out NativeMethods.WIN32_FIND_DATA findData)
		{
			normalizedPathWithSearchPattern = normalizedPathWithSearchPattern.TrimEnd('\\');
			var handle = NativeMethods.FindFirstFile(normalizedPathWithSearchPattern, out findData);
			if (!handle.IsInvalid) return handle;
			var errorCode = Marshal.GetLastWin32Error();
			if (errorCode != NativeMethods.ERROR_FILE_NOT_FOUND &&
				errorCode != NativeMethods.ERROR_PATH_NOT_FOUND &&
				errorCode != NativeMethods.ERROR_NOT_READY)
			{
				throw Common.GetExceptionFromWin32Error(errorCode);
			}

			return null;
		}

		internal static bool IsDirectory(System.IO.FileAttributes attributes)
		{
			return (attributes & System.IO.FileAttributes.Directory) == System.IO.FileAttributes.Directory;
		}

		private static bool IsCurrentOrParentDirectory(string directoryName)
		{
			return directoryName.Equals(".", StringComparison.OrdinalIgnoreCase) || directoryName.Equals("..", StringComparison.OrdinalIgnoreCase);
		}

		public static void Move(string sourcePath, string destinationPath)
		{
		    if (Common.IsRunningOnMono())
		    {
                System.IO.File.Move(sourcePath, destinationPath);
		        return;
		    }

			string normalizedSourcePath = Path.NormalizeLongPath(sourcePath, "sourcePath");
			string normalizedDestinationPath = Path.NormalizeLongPath(destinationPath, "destinationPath");

			if (NativeMethods.MoveFile(normalizedSourcePath, normalizedDestinationPath)) return;

			var lastWin32Error = Marshal.GetLastWin32Error();
			if (lastWin32Error == NativeMethods.ERROR_ACCESS_DENIED)
				throw new System.IO.IOException(string.Format("Access to the path '{0}'is denied.", sourcePath), NativeMethods.MakeHRFromErrorCode(lastWin32Error));
			throw Common.GetExceptionFromWin32Error(lastWin32Error, "path");
		}

		private static DirectoryInfo CreateDirectoryUnc(string path)
		{
			var length = path.Length;
			if (length >= 2 && Path.IsDirectorySeparator(path[length - 1]))
				--length;

			var rootLength = Path.GetRootLength(path);

			var pathComponents = new List<string>();

			if (length > rootLength)
			{
				for (var index = length - 1; index >= rootLength; --index)
				{
					var subPath = path.Substring(0, index + 1);
					if (!Exists(subPath))
						pathComponents.Add(subPath);
					while (index > rootLength && path[index] != System.IO.Path.DirectorySeparatorChar &&
						   path[index] != System.IO.Path.AltDirectorySeparatorChar)
						--index;
				}
			}
			while (pathComponents.Count > 0)
			{
				var str = Path.NormalizeLongPath(pathComponents[pathComponents.Count - 1]);
				pathComponents.RemoveAt(pathComponents.Count - 1);

				if (NativeMethods.CreateDirectory(str, IntPtr.Zero)) continue;

				// To mimic Directory.CreateDirectory, we don't throw if the directory (not a file) already exists
				var errorCode = Marshal.GetLastWin32Error();
				if (errorCode != NativeMethods.ERROR_ALREADY_EXISTS || !Exists(path))
				{
					throw Common.GetExceptionFromWin32Error(errorCode);
				}
			}
			return new DirectoryInfo(path);
		}

		/// <summary>
		///     Creates the specified directory.
		/// </summary>
		/// <param name="path">
		///     A <see cref="string"/> containing the path of the directory to create.
		/// </param>
		/// <exception cref="ArgumentNullException">
		///     <paramref name="path"/> is <see langword="null"/>.
		/// </exception>
		/// <exception cref="ArgumentException">
		///     <paramref name="path"/> is an empty string (""), contains only white
		///     space, or contains one or more invalid characters as defined in
		///     <see cref="Path.GetInvalidPathChars()"/>.
		///     <para>
		///         -or-
		///     </para>
		///     <paramref name="path"/> contains one or more components that exceed
		///     the drive-defined maximum length. For example, on Windows-based
		///     platforms, components must not exceed 255 characters.
		/// </exception>
		/// <exception cref="System.IO.PathTooLongException">
		///     <paramref name="path"/> exceeds the system-defined maximum length.
		///     For example, on Windows-based platforms, paths must not exceed
		///     32,000 characters.
		/// </exception>
		/// <exception cref="System.IO.DirectoryNotFoundException">
		///     <paramref name="path"/> contains one or more directories that could not be
		///     found.
		/// </exception>
		/// <exception cref="UnauthorizedAccessException">
		///     The caller does not have the required access permissions.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		///     <paramref name="path"/> is a file.
		///     <para>
		///         -or-
		///     </para>
		///     <paramref name="path"/> specifies a device that is not ready.
		/// </exception>
		/// <remarks>
		///     Note: Unlike <see cref="Directory.CreateDirectory(System.String)"/>, this method only creates
		///     the last directory in <paramref name="path"/>.
		/// </remarks>
		public static DirectoryInfo CreateDirectory(string path)
		{
		    if (Common.IsRunningOnMono()) return new DirectoryInfo(System.IO.Directory.CreateDirectory(path).FullName);

			if (Common.IsPathUnc(path)) return CreateDirectoryUnc(path);
			var normalizedPath = Path.NormalizeLongPath(path);
			var fullPath = Path.RemoveLongPathPrefix(normalizedPath);

			var length = fullPath.Length;
			if (length >= 2 && Path.IsDirectorySeparator(fullPath[length - 1]))
				--length;

			var rootLength = Path.GetRootLength(fullPath);

			var pathComponents = new List<string>();

			if (length > rootLength)
			{
				for (var index = length - 1; index >= rootLength; --index)
				{
					var subPath = fullPath.Substring(0, index + 1);
					if (!Exists(subPath))
						pathComponents.Add(Path.NormalizeLongPath(subPath));
					while (index > rootLength && fullPath[index] != System.IO.Path.DirectorySeparatorChar &&
						   fullPath[index] != System.IO.Path.AltDirectorySeparatorChar)
						--index;
				}
			}
			while (pathComponents.Count > 0)
			{
				var str = pathComponents[pathComponents.Count - 1];
				pathComponents.RemoveAt(pathComponents.Count - 1);

				if (NativeMethods.CreateDirectory(str, IntPtr.Zero)) continue;

				// To mimic Directory.CreateDirectory, we don't throw if the directory (not a file) already exists
				var errorCode = Marshal.GetLastWin32Error();
				if (errorCode != NativeMethods.ERROR_ALREADY_EXISTS || !Exists(path))
				{
					throw Common.GetExceptionFromWin32Error(errorCode);
				}
			}
			return new DirectoryInfo(fullPath);
		}

		public static string[] GetDirectories(string path, string searchPattern, SearchOption searchOption)
		{
		    if (Common.IsRunningOnMono()) return System.IO.Directory.GetDirectories(path, searchPattern, searchOption);

            return EnumerateFileSystemEntries(path, searchPattern, true, false, searchOption).ToArray();
        }

        public static string[] GetFiles(string path)
        {
            if (Common.IsRunningOnMono()) return System.IO.Directory.GetFiles(path);

            return EnumerateFileSystemEntries(path, "*", false, true, SearchOption.TopDirectoryOnly).ToArray();
        }

        //public static IEnumerable<string> EnumerateFiles(string path)
        //{
        //    if (Common.IsRunningOnMono()) return System.IO.Directory.EnumerateFiles(path);

        //    return EnumerateFileSystemEntries(path, "*", false, true, SearchOption.TopDirectoryOnly).ToArray();
        //}

        //public static IEnumerable<string> EnumerateFiles(string path, string searchPattern)
        //{
        //    if (Common.IsRunningOnMono()) return System.IO.Directory.EnumerateFiles(path, searchPattern);

        //    return EnumerateFileSystemEntries(path, searchPattern, false, true, SearchOption.TopDirectoryOnly);
        //}

        //public static IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
        //{
        //    if (Common.IsRunningOnMono()) return System.IO.Directory.EnumerateFiles(path, searchPattern, searchOption);

        //    return EnumerateFileSystemEntries(path, searchPattern, false, true, searchOption);
        //}

        public static unsafe void SetCreationTimeUtc(string path, DateTime creationTimeUtc)
		{
		    if (Common.IsRunningOnMono())
		    {
                System.IO.Directory.SetCreationTimeUtc(path, creationTimeUtc);
		        return;
		    }

            var normalizedPath = Path.NormalizeLongPath(Path.GetFullPath(path));

			using (var handle = GetDirectoryHandle(normalizedPath))
			{
				var fileTime = new NativeMethods.FILE_TIME(creationTimeUtc.ToFileTimeUtc());
				var r = NativeMethods.SetFileTime(handle, &fileTime, null, null);
				if (r) return;
				var errorCode = Marshal.GetLastWin32Error();
				Common.ThrowIOError(errorCode, path);
			}
		}

		public static unsafe void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc)
		{
		    if (Common.IsRunningOnMono())
		    {
		        System.IO.Directory.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
		        return;
		    }
            var normalizedPath = Path.NormalizeLongPath(Path.GetFullPath(path));

			using (SafeFileHandle handle = GetDirectoryHandle(normalizedPath))
			{
				var fileTime = new NativeMethods.FILE_TIME(lastWriteTimeUtc.ToFileTimeUtc());
				var r = NativeMethods.SetFileTime(handle, null, null, &fileTime);
				if (r) return;
				var errorCode = Marshal.GetLastWin32Error();
				Common.ThrowIOError(errorCode, path);
			}
		}

		public static unsafe void SetLastAccessTimeUtc(string path, DateTime lastWriteTimeUtc)
		{
		    if (Common.IsRunningOnMono())
		    {
		        System.IO.Directory.SetLastAccessTimeUtc(path, lastWriteTimeUtc);
		        return;
		    }

            var normalizedPath = Path.NormalizeLongPath(Path.GetFullPath(path));

			using (var handle = GetDirectoryHandle(normalizedPath))
			{
				var fileTime = new NativeMethods.FILE_TIME(lastWriteTimeUtc.ToFileTimeUtc());
				var r = NativeMethods.SetFileTime(handle, null, &fileTime, null);
				if (r) return;
				var errorCode = Marshal.GetLastWin32Error();
				Common.ThrowIOError(errorCode, path);
			}
		}

		public static DirectoryInfo GetParent(string path)
		{
			var directoryName = Path.GetDirectoryName(path);
			return directoryName == null ? null : new DirectoryInfo(directoryName);
		}

#if netfx
        public static DirectoryInfo CreateDirectory(string path, DirectorySecurity directorySecurity)
		{
			CreateDirectory(path);
			SetAccessControl(path, directorySecurity);
			return new DirectoryInfo(path);
		}

		public static DirectorySecurity GetAccessControl(string path)
		{
			const AccessControlSections includeSections = AccessControlSections.Access | AccessControlSections.Owner | AccessControlSections.Group;
			return GetAccessControl(path, includeSections);
		}

		public static DirectorySecurity GetAccessControl(string path, AccessControlSections includeSections)
		{
		    if (Common.IsRunningOnMono()) return System.IO.Directory.GetAccessControl(path, includeSections);

			var normalizedPath = Path.NormalizeLongPath(Path.GetFullPath(path));
			IntPtr sidOwner, sidGroup, dacl, sacl, byteArray;
			var securityInfos = Common.ToSecurityInfos(includeSections);

			var errorCode = (int)NativeMethods.GetSecurityInfoByName(normalizedPath,
				(uint)ResourceType.FileObject,
				(uint)securityInfos,
				out sidOwner,
				out sidGroup,
				out dacl,
				out sacl,
				out byteArray);

			Common.ThrowIfError(errorCode, byteArray);

			var length = NativeMethods.GetSecurityDescriptorLength(byteArray);

			var binaryForm = new byte[length];

			Marshal.Copy(byteArray, binaryForm, 0, (int)length);

			NativeMethods.LocalFree(byteArray);
			var ds = new DirectorySecurity();
			ds.SetSecurityDescriptorBinaryForm(binaryForm);
			return ds;
		}
#endif

        public static DateTime GetCreationTime(string path)
		{
			return GetCreationTimeUtc(path).ToLocalTime();
		}

		public static DateTime GetCreationTimeUtc(string path)
		{
			var di = new DirectoryInfo(path);
			return di.CreationTimeUtc;
		}

		public static string[] GetDirectories(string path)
		{
		    if (Common.IsRunningOnMono()) return System.IO.Directory.GetDirectories(path);
			return EnumerateFileSystemEntries(path, "*", true, false, SearchOption.TopDirectoryOnly).ToArray();
		}

		public static string[] GetDirectories(string path, string searchPattern)
		{
		    if (Common.IsRunningOnMono()) return System.IO.Directory.GetDirectories(path, searchPattern);
            return EnumerateFileSystemEntries(path, searchPattern, true, false, SearchOption.TopDirectoryOnly).ToArray();
		}

		public static string GetDirectoryRoot(string path)
		{
			var fullPath = Path.GetFullPath(path);
			return fullPath.Substring(0, Path.GetRootLength(fullPath));
		}

		public static string[] GetFiles(string path, string searchPattern)
		{
		    if (Common.IsRunningOnMono()) return System.IO.Directory.GetFiles(path, searchPattern);
			return EnumerateFileSystemEntries(path, searchPattern, false, true, SearchOption.TopDirectoryOnly).ToArray();
		}

		public static string[] GetFiles(string path, string searchPattern, SearchOption options)
		{
		    if (Common.IsRunningOnMono()) return System.IO.Directory.GetFiles(path, searchPattern, options);
            return EnumerateFileSystemEntries(path, searchPattern, false, true, options).ToArray();
		}

		public static string[] GetFileSystemEntries(string path)
		{
		    if (Common.IsRunningOnMono()) return System.IO.Directory.GetFileSystemEntries(path);
            return EnumerateFileSystemEntries(path, null, true, true, SearchOption.TopDirectoryOnly).ToArray();
		}

		public static string[] GetFileSystemEntries(string path, string searchPattern)
		{
		    if (Common.IsRunningOnMono()) return System.IO.Directory.GetFileSystemEntries(path, searchPattern);
            return EnumerateFileSystemEntries(path, searchPattern, true, true, SearchOption.TopDirectoryOnly).ToArray();
		}

		public static string[] GetFileSystemEntries(string path, string searchPattern, SearchOption options)
		{
		    if (Common.IsRunningOnMono()) return System.IO.Directory.GetFileSystemEntries(path, searchPattern);
            return EnumerateFileSystemEntries(path, searchPattern, true, true, options).ToArray();
		}

		public static DateTime GetLastAccessTime(string path)
		{
			return GetLastAccessTimeUtc(path).ToLocalTime();
		}

		public static DateTime GetLastAccessTimeUtc(string path)
		{
		    if (Common.IsRunningOnMono()) return System.IO.Directory.GetLastAccessTimeUtc(path);

            var di = new DirectoryInfo(path);
			return di.LastAccessTimeUtc;
		}

		public static DateTime GetLastWriteTime(string path)
		{
			return GetLastWriteTimeUtc(path).ToLocalTime();
		}

		public static DateTime GetLastWriteTimeUtc(string path)
		{
		    if (Common.IsRunningOnMono()) return System.IO.Directory.GetLastWriteTimeUtc(path);

            var di = new DirectoryInfo(path);
			return di.LastWriteTimeUtc;
		}

		public static string[] GetLogicalDrives()
		{
			return System.IO.Directory.GetLogicalDrives();
		}

#if netfx
        public static void SetAccessControl(string path, DirectorySecurity directorySecurity)
		{
		    if (Common.IsRunningOnMono())
		    {
		        System.IO.Directory.SetAccessControl(path, directorySecurity);
		        return;
		    }

            if (path == null) throw new ArgumentNullException("path");
			if (directorySecurity == null) throw new ArgumentNullException("directorySecurity");
			var name = Path.NormalizeLongPath(Path.GetFullPath(path));

			Common.SetAccessControlExtracted(directorySecurity, name);
		}
#endif

        public static void SetCreationTime(string path, DateTime creationTime)
		{
			SetCreationTimeUtc(path, creationTime.ToUniversalTime());
		}

		public static void SetLastAccessTime(string path, DateTime lastAccessTime)
		{
			SetLastAccessTimeUtc(path, lastAccessTime.ToUniversalTime());
		}

		public static void SetLastWriteTime(string path, DateTime lastWriteTimeUtc)
		{
		    if (Common.IsRunningOnMono())
		    {
		        System.IO.Directory.SetLastWriteTime(path, lastWriteTimeUtc);
		        return;
		    }

            unsafe
			{
				var normalizedPath = Path.NormalizeLongPath(Path.GetFullPath(path));

				using (var handle = GetDirectoryHandle(normalizedPath))
				{
					var fileTime = new NativeMethods.FILE_TIME(lastWriteTimeUtc.ToFileTimeUtc());
					var r = NativeMethods.SetFileTime(handle, null, null, &fileTime);
					if (r) return;
					var errorCode = Marshal.GetLastWin32Error();
					Common.ThrowIOError(errorCode, path);
				}
			}
		}

		public static void SetCurrentDirectory(string path)
		{
		    if (Common.IsRunningOnMono())
		    {
		        System.IO.Directory.SetCurrentDirectory(path);
		        return;
		    }
#if true
            throw new NotSupportedException("Windows does not support setting the current directory to a long path");
#else
			string normalizedPath = Path.NormalizeLongPath(Path.GetFullPath(path));
			if (!NativeMethods.SetCurrentDirectory(normalizedPath))
			{
				int lastWin32Error = Marshal.GetLastWin32Error();
				if (lastWin32Error == NativeMethods.ERROR_FILE_NOT_FOUND)
				{
					lastWin32Error = NativeMethods.ERROR_PATH_NOT_FOUND;
				}
				Common.ThrowIOError(lastWin32Error, normalizedPath);
			}
#endif
		}
	}
}