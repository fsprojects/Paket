using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;
using DWORD=System.UInt32;
using System.Runtime.ConstrainedExecution;
using System.Security.Principal;

namespace Pri.LongPath
{
	internal static class NativeMethods
	{
		internal const int ERROR_SUCCESS = 0;
		internal const int ERROR_FILE_NOT_FOUND = 0x2;
		internal const int ERROR_PATH_NOT_FOUND = 0x3;
		internal const int ERROR_ACCESS_DENIED = 0x5;
		internal const int ERROR_INVALID_HANDLE = 0x6;
		internal const int ERROR_NOT_ENOUGH_MEMORY = 0x8;
		internal const int ERROR_INVALID_DRIVE = 0xf;
		internal const int ERROR_NO_MORE_FILES = 0x12;
		internal const int ERROR_NOT_READY = 0x15;
		internal const int ERROR_SHARING_VIOLATION = 0x20;
		internal const int ERROR_BAD_NETPATH = 0x35;
		internal const int ERROR_NETNAME_DELETED = 0x40;
		internal const int ERROR_FILE_EXISTS = 0x50;
		internal const int ERROR_INVALID_PARAMETER = 0x57;
		internal const int ERROR_INVALID_NAME = 0x7B;
		internal const int ERROR_BAD_PATHNAME = 0xA1;
		internal const int ERROR_ALREADY_EXISTS = 0xB7;
		internal const int ERROR_FILENAME_EXCED_RANGE = 0xCE;  // filename too long.
		internal const int ERROR_DIRECTORY = 0x10B;
		internal const int ERROR_OPERATION_ABORTED = 0x3e3;
		internal const int ERROR_NO_TOKEN = 0x3f0;
		internal const int ERROR_NOT_ALL_ASSIGNED = 0x514;
		internal const int ERROR_INVALID_OWNER = 0x51B;
		internal const int ERROR_INVALID_PRIMARY_GROUP = 0x51C;
		internal const int ERROR_NO_SUCH_PRIVILEGE = 0x521;
		internal const int ERROR_PRIVILEGE_NOT_HELD = 0x522;
		internal const int ERROR_LOGON_FAILURE = 0x52E;
		internal const int ERROR_CANT_OPEN_ANONYMOUS = 0x543;
		internal const int ERROR_NO_SECURITY_ON_OBJECT = 0x546;

		internal const int INVALID_FILE_ATTRIBUTES = -1;
		internal const int FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
		internal const int FILE_WRITE_ATTRIBUTES = 0x0100;
		internal const int FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
		internal const int REPLACEFILE_WRITE_THROUGH = 0x1;
		internal const int REPLACEFILE_IGNORE_MERGE_ERRORS = 0x2;

		internal const int MAX_PATH = 260;
		// While Windows allows larger paths up to a maximum of 32767 characters, because this is only an approximation and
		// can vary across systems and OS versions, we choose a limit well under so that we can give a consistent behavior.
		internal const int MAX_LONG_PATH = 32000;
		internal const int MAX_ALTERNATE = 14;

		public const int FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200;
		public const int FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000;
		public const int FORMAT_MESSAGE_ARGUMENT_ARRAY = 0x00002000;

		[Flags]
		internal enum EFileAccess : uint
		{
			GenericRead = 0x80000000,
			GenericWrite = 0x40000000,
			GenericExecute = 0x20000000,
			GenericAll = 0x10000000,
		}

		[Serializable]
		internal struct WIN32_FILE_ATTRIBUTE_DATA
		{
			internal System.IO.FileAttributes fileAttributes;

			internal uint ftCreationTimeLow;

			internal uint ftCreationTimeHigh;

			internal uint ftLastAccessTimeLow;

			internal uint ftLastAccessTimeHigh;

			internal uint ftLastWriteTimeLow;

			internal uint ftLastWriteTimeHigh;

			internal int fileSizeHigh;

			internal int fileSizeLow;
			public void PopulateFrom(NativeMethods.WIN32_FIND_DATA findData)
			{
				fileAttributes = findData.dwFileAttributes;
				ftCreationTimeLow = (uint)findData.ftCreationTime.dwLowDateTime;
				ftCreationTimeHigh = (uint)findData.ftCreationTime.dwHighDateTime;
				ftLastAccessTimeLow = (uint)findData.ftLastAccessTime.dwLowDateTime;
				ftLastAccessTimeHigh = (uint)findData.ftLastAccessTime.dwHighDateTime;
				ftLastWriteTimeLow = (uint)findData.ftLastWriteTime.dwLowDateTime;
				ftLastWriteTimeHigh = (uint)findData.ftLastWriteTime.dwHighDateTime;
				fileSizeHigh = findData.nFileSizeHigh;
				fileSizeLow = findData.nFileSizeLow;
			}
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		internal struct WIN32_FIND_DATA
		{
			internal System.IO.FileAttributes dwFileAttributes;
			internal FILETIME ftCreationTime;
			internal FILETIME ftLastAccessTime;
			internal FILETIME ftLastWriteTime;
			internal int nFileSizeHigh;
			internal int nFileSizeLow;
			internal int dwReserved0;
			internal int dwReserved1;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
			internal string cFileName;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_ALTERNATE)]
			internal string cAlternate;
		}

		internal static int MakeHRFromErrorCode(int errorCode)
		{
			return unchecked((int)0x80070000 | errorCode);
		}

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool CopyFile(string src, string dst, [MarshalAs(UnmanagedType.Bool)]bool failIfExists);

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto, BestFitMapping = false)]
		internal static extern bool ReplaceFile(String replacedFileName, String replacementFileName, String backupFileName, int dwReplaceFlags, IntPtr lpExclude, IntPtr lpReserved);

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		internal static extern SafeFindHandle FindFirstFile(string lpFileName, out WIN32_FIND_DATA lpFindFileData);

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool FindNextFile(SafeFindHandle hFindFile, out WIN32_FIND_DATA lpFindFileData);

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool FindClose(IntPtr hFindFile);

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		internal static extern uint GetFullPathName(string lpFileName, uint nBufferLength,
			StringBuilder lpBuffer, IntPtr mustBeNull);

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool DeleteFile(string lpFileName);

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool RemoveDirectory(string lpPathName);

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool CreateDirectory(string lpPathName,
			IntPtr lpSecurityAttributes);

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool MoveFile(string lpPathNameFrom, string lpPathNameTo);

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		internal static extern SafeFileHandle CreateFile(
			string lpFileName,
			EFileAccess dwDesiredAccess,
			uint dwShareMode,
			IntPtr lpSecurityAttributes,
			uint dwCreationDisposition,
			uint dwFlagsAndAttributes,
			IntPtr hTemplateFile);

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		internal static extern System.IO.FileAttributes GetFileAttributes(string lpFileName);

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool SetFileAttributes(string lpFileName, [MarshalAs(UnmanagedType.U4)]System.IO.FileAttributes dwFileAttributes);

		internal static long SetFilePointer(SafeFileHandle handle, long offset, System.IO.SeekOrigin origin)
		{
			int num1 = (int)(offset >> 32);
			int num2 = SetFilePointerWin32(handle, (int)offset, ref num1, (int)origin);
			if (num2 == -1 && Marshal.GetLastWin32Error() != 0)
				return -1L;
			return (long)(uint)num1 << 32 | (uint)num2;
		}

		[DllImport("kernel32.dll", EntryPoint = "SetFilePointer", SetLastError = true)]
		internal static extern int SetFilePointerWin32(SafeFileHandle handle, int lo, ref int hi, int origin);

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		internal static extern int FormatMessage(int dwFlags, IntPtr lpSource, int dwMessageId, int dwLanguageId, StringBuilder lpBuffer, int nSize, IntPtr va_list_arguments);

		[DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto, BestFitMapping = false)]
		internal static extern bool DecryptFile(String path, int reservedMustBeZero);

		[DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto, BestFitMapping = false)]
		internal static extern bool EncryptFile(String path);

		public static string GetMessage(int errorCode)
		{
			var sb = new StringBuilder(512);
			int result = FormatMessage(FORMAT_MESSAGE_IGNORE_INSERTS |
									   FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_ARGUMENT_ARRAY,
				IntPtr.Zero, errorCode, 0, sb, sb.Capacity, IntPtr.Zero);
			if (result != 0)
			{
				// result is the # of characters copied to the StringBuilder.
				return sb.ToString();
			}
			else
			{
				return string.Format("Unknown error: {0}", errorCode);
			}
		}
		[StructLayout(LayoutKind.Sequential)]
		internal struct FILE_TIME
		{
			public FILE_TIME(long fileTime)
			{
				ftTimeLow = (uint)fileTime;
				ftTimeHigh = (uint)(fileTime >> 32);
			}

			public long ToTicks()
			{
				return ((long)ftTimeHigh << 32) + ftTimeLow;
			}

			internal uint ftTimeLow;
			internal uint ftTimeHigh;
		}
		[DllImport("kernel32.dll", SetLastError = true)]
		internal static extern unsafe bool SetFileTime(SafeFileHandle hFile, FILE_TIME* creationTime,
					FILE_TIME* lastAccessTime, FILE_TIME* lastWriteTime);

		[DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Auto, ExactSpelling = false, SetLastError = true)]
		internal static extern bool GetFileAttributesEx(string name, int fileInfoLevel, ref WIN32_FILE_ATTRIBUTE_DATA lpFileInformation);

		[DllImport("kernel32.dll", CharSet = CharSet.None, EntryPoint = "SetErrorMode", ExactSpelling = true)]
		private static extern int SetErrorMode_VistaAndOlder(int newMode);
		private static readonly Version ThreadErrorModeMinOsVersion = new Version(6, 1, 7600);
		[DllImport("kernel32.dll", CharSet = CharSet.None, EntryPoint = "SetThreadErrorMode", ExactSpelling = false, SetLastError = true)]
		private static extern bool SetErrorMode_Win7AndNewer(int newMode, out int oldMode);

		internal static int SetErrorMode(int newMode)
		{
			int num;
			if (Environment.OSVersion.Version < ThreadErrorModeMinOsVersion)
			{
				return SetErrorMode_VistaAndOlder(newMode);
			}
			SetErrorMode_Win7AndNewer(newMode, out num);
			return num;
		}

		[DllImport("advapi32.dll",
			EntryPoint = "GetNamedSecurityInfoW",
			CallingConvention = CallingConvention.Winapi,
			SetLastError = true,
			ExactSpelling = true,
			CharSet = CharSet.Unicode)]
		internal static extern DWORD GetSecurityInfoByName(
			string name,
			DWORD objectType,
			DWORD securityInformation,
			out IntPtr sidOwner,
			out IntPtr sidGroup,
			out IntPtr dacl,
			out IntPtr sacl,
			out IntPtr securityDescriptor);

		[DllImport(
			 "advapi32.dll",
			 EntryPoint = "SetNamedSecurityInfoW",
			 CallingConvention = CallingConvention.Winapi,
			 SetLastError = true,
			 ExactSpelling = true,
			 CharSet = CharSet.Unicode)]
		internal static extern DWORD SetSecurityInfoByName(
			string name,
			DWORD objectType,
			DWORD securityInformation,
			byte[] owner,
			byte[] group,
			byte[] dacl,
			byte[] sacl);

		[DllImport(
			 "advapi32.dll",
			 EntryPoint = "SetSecurityInfo",
			 CallingConvention = CallingConvention.Winapi,
			 SetLastError = true,
			 ExactSpelling = true,
			 CharSet = CharSet.Unicode)]
		internal static extern DWORD SetSecurityInfoByHandle(
			SafeHandle handle,
			DWORD objectType,
			DWORD securityInformation,
			byte[] owner,
			byte[] group,
			byte[] dacl,
			byte[] sacl);
		[DllImport(
			 "advapi32.dll",
			 EntryPoint = "GetSecurityDescriptorLength",
			 CallingConvention = CallingConvention.Winapi,
			 SetLastError = true,
			 ExactSpelling = true,
			 CharSet = CharSet.Unicode)]
		internal static extern DWORD GetSecurityDescriptorLength(
			IntPtr byteArray);

		[DllImport("kernel32.dll", SetLastError = true)]
		internal static extern IntPtr LocalFree(IntPtr handle);

		[DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Auto, ExactSpelling = false, SetLastError = true)]
		internal static extern bool SetCurrentDirectory(string path);
		#region for Priviledge class

		internal enum SecurityImpersonationLevel
		{
			Anonymous = 0,
			Identification = 1,
			Impersonation = 2,
			Delegation = 3,
		}

		internal enum TokenType
		{
			Primary = 1,
			Impersonation = 2,
		}

		internal const uint SE_PRIVILEGE_DISABLED = 0x00000000;
		internal const uint SE_PRIVILEGE_ENABLED = 0x00000002;

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		internal struct LUID
		{
			internal uint LowPart;
			internal uint HighPart;
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		internal struct LUID_AND_ATTRIBUTES
		{
			internal LUID Luid;
			internal uint Attributes;
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		internal struct TOKEN_PRIVILEGE
		{
			internal uint PrivilegeCount;
			internal LUID_AND_ATTRIBUTES Privilege;
		}


		[DllImport(
			 "kernel32.dll",
			 SetLastError = true)]
		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
		internal static extern bool CloseHandle(IntPtr handle);

		[DllImport(
			 "advapi32.dll",
			 CharSet = CharSet.Unicode,
			 SetLastError = true)]
		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
		internal static extern bool AdjustTokenPrivileges(
			[In]     SafeTokenHandle TokenHandle,
			[In]     bool DisableAllPrivileges,
			[In]     ref TOKEN_PRIVILEGE NewState,
			[In]     uint BufferLength,
			[In, Out] ref TOKEN_PRIVILEGE PreviousState,
			[In, Out] ref uint ReturnLength);

		[DllImport(
			 "advapi32.dll",
			 CharSet = CharSet.Auto,
			 SetLastError = true)]
		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
		internal static extern
		bool RevertToSelf();

		[DllImport(
			 "advapi32.dll",
			 EntryPoint = "LookupPrivilegeValueW",
			 CharSet = CharSet.Auto,
			 SetLastError = true)]
		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
		internal static extern
		bool LookupPrivilegeValue(
			[In]     string lpSystemName,
			[In]     string lpName,
			[In, Out] ref LUID Luid);

		[DllImport(
			 "kernel32.dll",
			 CharSet = CharSet.Auto,
			 SetLastError = true)]
		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
		internal static extern
		IntPtr GetCurrentProcess();

		[DllImport(
			 "kernel32.dll",
			 CharSet = CharSet.Auto,
			 SetLastError = true)]
		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
		internal static extern
			IntPtr GetCurrentThread();
#if netfx
        [DllImport(
			 "advapi32.dll",
			 CharSet = CharSet.Unicode,
			 SetLastError = true)]
		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
		internal static extern
		bool OpenProcessToken(
			[In]     IntPtr ProcessToken,
			[In]     TokenAccessLevels DesiredAccess,
			[In, Out] ref SafeTokenHandle TokenHandle);

		[DllImport
			 ("advapi32.dll",
			 CharSet = CharSet.Unicode,
			 SetLastError = true)]
		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
		internal static extern
		bool OpenThreadToken(
			[In]     IntPtr ThreadToken,
			[In]     TokenAccessLevels DesiredAccess,
			[In]     bool OpenAsSelf,
			[In, Out] ref SafeTokenHandle TokenHandle);

		[DllImport
			("advapi32.dll",
			 CharSet = CharSet.Unicode,
			 SetLastError = true)]
		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
		internal static extern
		bool DuplicateTokenEx(
			[In]    SafeTokenHandle ExistingToken,
			[In]    TokenAccessLevels DesiredAccess,
			[In]    IntPtr TokenAttributes,
			[In]    SecurityImpersonationLevel ImpersonationLevel,
			[In]    TokenType TokenType,
			[In, Out] ref SafeTokenHandle NewToken);

#endif

        [DllImport
			 ("advapi32.dll",
			 CharSet = CharSet.Unicode,
			 SetLastError = true)]
		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
		internal static extern
		bool SetThreadToken(
			[In]    IntPtr Thread,
			[In]    SafeTokenHandle Token);
#endregion
	}
}