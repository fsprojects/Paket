using System;
using System.Security;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Runtime.ConstrainedExecution;

namespace Pri.LongPath
{
	internal class SafeTokenHandle : SafeHandleZeroOrMinusOneIsInvalid
	{
		private SafeTokenHandle() : base(true) { }

		// 0 is an Invalid Handle
		internal SafeTokenHandle(IntPtr handle)
			: base(true)
		{
			SetHandle(handle);
		}

		internal static SafeTokenHandle InvalidHandle
		{
			get { return new SafeTokenHandle(IntPtr.Zero); }
		}

		[DllImport("kernel32.dll", SetLastError = true),
		 SuppressUnmanagedCodeSecurity,
		 ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
		private static extern bool CloseHandle(IntPtr handle);

		protected override bool ReleaseHandle()
		{
			return CloseHandle(handle);
		}
	}
}
