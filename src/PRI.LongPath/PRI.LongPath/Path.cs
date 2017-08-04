using System;
using System.Globalization;
using System.Text;
#if !NET_2_0
using System.Linq;
#endif

namespace Pri.LongPath
{
	public static class Path
	{
		public static readonly char[] InvalidPathChars = System.IO.Path.GetInvalidPathChars();
		private static readonly char[] invalidFileNameChars = System.IO.Path.GetInvalidFileNameChars();
		internal const string LongPathPrefix = @"\\?\";
        internal const string UNCLongPathPrefix = @"\\?\UNC\";

		public static readonly char DirectorySeparatorChar = System.IO.Path.DirectorySeparatorChar;
		public static readonly char AltDirectorySeparatorChar = System.IO.Path.AltDirectorySeparatorChar;
		public static readonly char VolumeSeparatorChar = ':';

		public static readonly char PathSeparator = System.IO.Path.PathSeparator;

		internal static string NormalizeLongPath(string path)
		{
		    if (Common.IsRunningOnMono())
		        return path;

            return NormalizeLongPath(path, "path");
		}

		// Normalizes path (can be longer than MAX_PATH) and adds \\?\ long path prefix
		internal static string NormalizeLongPath(string path, string parameterName)
		{
			if (path == null)
				throw new ArgumentNullException(parameterName);

			if (path.Length == 0)
				throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, "'{0}' cannot be an empty string.", parameterName), parameterName);

			if (Common.IsPathUnc(path)) return CheckAddLongPathPrefix(path);
			StringBuilder buffer = new StringBuilder(path.Length + 1); // Add 1 for NULL
			uint length = NativeMethods.GetFullPathName(path, (uint)buffer.Capacity, buffer, IntPtr.Zero);
			if (length > buffer.Capacity)
			{
				// Resulting path longer than our buffer, so increase it

				buffer.Capacity = (int)length;
				length = NativeMethods.GetFullPathName(path, length, buffer, IntPtr.Zero);
			}

			if (length == 0)
			{
				throw Common.GetExceptionFromLastWin32Error(parameterName);
			}

			if (length > NativeMethods.MAX_LONG_PATH)
			{
				throw Common.GetExceptionFromWin32Error(NativeMethods.ERROR_FILENAME_EXCED_RANGE, parameterName);
			}

			if (length > 1 && buffer[0] == DirectorySeparatorChar && buffer[1] == DirectorySeparatorChar)
			{
				if (length < 2) throw new ArgumentException("The UNC path should be of the form \\\\server\\share.");
				var parts = buffer.ToString().Split(new [] {DirectorySeparatorChar}, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length < 2) throw new ArgumentException("The UNC path should be of the form \\\\server\\share.");
			}

			return AddLongPathPrefix(buffer.ToString());
		}

		internal static bool TryNormalizeLongPath(string path, out string result)
		{
			try
			{
				result = NormalizeLongPath(path);
				return true;
			}
			catch (ArgumentException)
			{
			}
			catch (System.IO.PathTooLongException)
			{
			}

			result = null;
			return false;
		}

        internal static string CheckAddLongPathPrefix(string path)
        {
            if (string.IsNullOrEmpty(path) || path.StartsWith(@"\\?\"))
            {
                return path;
            }

            var maxPathLimit = NativeMethods.MAX_PATH;
            Uri uri;
            if (Uri.TryCreate(path, UriKind.Absolute, out uri) && uri.IsUnc)
            {
                // What's going on here?  Empirical evidence shows that Windows has trouble dealing with UNC paths
                // longer than MAX_PATH *minus* the length of the "\\hostname\" prefix.  See the following tests:
                //  - UncDirectoryTests.TestDirectoryCreateNearMaxPathLimit
                //  - UncDirectoryTests.TestDirectoryEnumerateDirectoriesNearMaxPathLimit
                var rootPathLength = 3 + uri.Host.Length;
                maxPathLimit -= rootPathLength;
            }

            if (path.Length >= maxPathLimit)
            {
                return AddLongPathPrefix(path);
            }

            return path;
        }

		internal static string RemoveLongPathPrefix(string normalizedPath)
		{

            if (string.IsNullOrEmpty(normalizedPath) || !normalizedPath.StartsWith(LongPathPrefix))
            {
                return normalizedPath;
            }

            if (normalizedPath.StartsWith(UNCLongPathPrefix, StringComparison.InvariantCultureIgnoreCase))
            {
                return string.Format(@"\\{0}", normalizedPath.Substring(UNCLongPathPrefix.Length));
            }

            return normalizedPath.Substring(LongPathPrefix.Length);
		}

		private static string AddLongPathPrefix(string path)
		{
            if (string.IsNullOrEmpty(path) || path.StartsWith(LongPathPrefix))
            {
                return path;
            }

            // http://msdn.microsoft.com/en-us/library/aa365247.aspx
            if (path.StartsWith(@"\\"))
            {
                // UNC.
                return UNCLongPathPrefix + path.Substring(2);
            }

			return LongPathPrefix + path;
		}

		public static string Combine(string path1, string path2)
		{
			if (path1 == null || path2 == null)
				throw new ArgumentNullException(path1 == null ? "path1" : "path2");

			CheckInvalidPathChars(path1);
			CheckInvalidPathChars(path2);
			if (path2.Length == 0)
				return path1;
			if (path1.Length == 0 || IsPathRooted(path2))
				return path2;
			char ch = path1[path1.Length - 1];
			if (ch != DirectorySeparatorChar && ch != AltDirectorySeparatorChar &&
				ch != VolumeSeparatorChar)
				return path1 + DirectorySeparatorChar + path2;
			return path1 + path2;
		}

		public static bool IsPathRooted(string path)
		{
			return System.IO.Path.IsPathRooted(path);
		}

		public static string Combine(string path1, string path2, string path3)
		{
			if (path1 == null || path2 == null || path3 == null)
				throw new ArgumentNullException(path1 == null ? "path1" : path2 == null ? "path2" : "path3");

			return Combine(Combine(path1, path2), path3);
        }

        public static string Combine(string path1, string path2, string path3, string path4)
        {
            if (path1 == null || path2 == null || path3 == null || path4 == null)
                throw new ArgumentNullException(path1 == null ? "path1" : path2 == null ? "path2" : path3 == null ? "path3" : "path4");

            return Combine(Combine(Combine(path1, path2), path3), path4);
        }

        //public static string Combine(params string[] paths)
        //{
        //    if (paths == null || paths.Contains(null))
        //        throw new ArgumentNullException(nameof(paths));

        //    if (paths.Length == 0)
        //        return "";

        //    string result = paths[0];
        //    for (int i = 1; i < paths.Length; i++)
        //        result = Path.Combine(result, paths[i]);
        //    return result;
        //}

        private static void CheckInvalidPathChars(string path)
		{
			if (HasIllegalCharacters(path))
				throw new ArgumentException("Invalid characters in path", "path");
		}

		private static bool HasIllegalCharacters(string path)
		{
#if NET_2_0
			foreach (var e in path)
			{
				if (InvalidPathChars.Contains(e))
				{
					return true;
				}
			}
			return false;
#else
			return path.Any(InvalidPathChars.Contains);
#endif
		}

		public static string GetFileName(string path)
		{
			if (path == null) return null;
			return System.IO.Path.GetFileName(Path.NormalizeLongPath(path));
		}

		public static string GetFullPath(string path)
		{
			return Common.IsPathUnc(path) ? path : Path.RemoveLongPathPrefix(Path.NormalizeLongPath(path));
		}

        public static String GetDirectoryName(String path)
        {
            if (path != null)
            {
                bool removedPrefix;
                String tempPath = TryRemoveLongPathPrefix(path, out removedPrefix);

                Path.CheckInvalidPathChars(tempPath);
                int root = GetRootLength(tempPath);
                int i = tempPath.Length;
                if (i > root)
                {
                    i = tempPath.Length;
                    if (i == root) return null;
                    while (i > root && tempPath[--i] != Path.DirectorySeparatorChar && tempPath[i] != Path.AltDirectorySeparatorChar) ;
                    String result = tempPath.Substring(0, i);
                    if (removedPrefix)
                    {
                        result = Path.AddLongPathPrefix(result);
                    }

                    return result;
                }
            }
            return null;
        }

        private static String TryRemoveLongPathPrefix(String path, out bool removed)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            removed = Path.HasLongPathPrefix(path);
            if (!removed)
                return path;
            return Path.RemoveLongPathPrefix(path);
        }

        internal static bool HasLongPathPrefix(string path)
        {
            return path.StartsWith(LongPathPrefix, StringComparison.Ordinal);
        }

        private static int GetUncRootLength(string path)
        {
            var components = path.Split(new[] { DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            var root = string.Format(@"\\{0}\{1}\", components[0], components[1]);
	        return root.Length;
        }

		internal static int GetRootLength(string path)
		{
			if (Common.IsPathUnc(path)) return GetUncRootLength(path);
			path = Path	.GetFullPath(path);
			Path.CheckInvalidPathChars(path);
			int rootLength = 0;
			int length = path.Length;
			if (length >= 1 && IsDirectorySeparator(path[0]))
			{
				rootLength = 1;
				if (length >= 2 && IsDirectorySeparator(path[1]))
				{
					rootLength = 2;
					int num = 2;
					while (rootLength >= length ||
						   ((path[rootLength] == System.IO.Path.DirectorySeparatorChar ||
							 path[rootLength] == System.IO.Path.AltDirectorySeparatorChar) && --num <= 0))
						++rootLength;
				}
			}
			else if (length >= 2 && path[1] == System.IO.Path.VolumeSeparatorChar)
			{
				rootLength = 2;
				if (length >= 3 && IsDirectorySeparator(path[2]))
					++rootLength;
			}
			return rootLength;
		}

		internal static bool IsDirectorySeparator(char c)
		{
			return c == DirectorySeparatorChar || c == AltDirectorySeparatorChar;
		}

		public static char[] GetInvalidPathChars()
		{
			return InvalidPathChars;
		}

		public static char[] GetInvalidFileNameChars()
		{
			return invalidFileNameChars;
		}

		public static string GetRandomFileName()
		{
			return System.IO.Path.GetRandomFileName();
		}

		public static string GetPathRoot(string path)
		{
			if (path == null) return null;
		    if (Path.IsPathRooted(path))
		    {
				if(!Common.IsPathUnc(path))
					path = Path.RemoveLongPathPrefix(Path.NormalizeLongPath(path));
		        return path.Substring(0, GetRootLength(path));
		    }
		    return string.Empty;
		}

		public static string GetExtension(string path)
		{
			return System.IO.Path.GetExtension(path);
		}

		public static bool HasExtension(string path)
		{
			return System.IO.Path.HasExtension(path);
		}

		public static string GetTempPath()
		{
			return System.IO.Path.GetTempPath();
		}

		public static string GetTempFileName()
		{
			return System.IO.Path.GetTempFileName();
		}

		public static string GetFileNameWithoutExtension(string path)
		{
			return System.IO.Path.GetFileNameWithoutExtension(path);
		}

		public static string ChangeExtension(string filename, string extension)
		{
			return System.IO.Path.ChangeExtension(filename, extension);
		}

		public static string Combine(params string[] paths)
		{
			if(paths == null) throw new ArgumentNullException("paths");
			if (paths.Length == 0) return string.Empty;
			if (paths.Length == 1) return paths[0];
			var path = paths[0];
			for (int i = 1; i < paths.Length; ++i)
			{
				path = Path.Combine(path, paths[i]);
			}
			return path;
		}
	}
}