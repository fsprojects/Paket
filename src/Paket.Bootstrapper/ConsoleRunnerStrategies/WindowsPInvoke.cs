// Copyright (c) to owners found in https://github.com/AArnott/pinvoke/blob/master/COPYRIGHT.md. All rights reserved.
// Licensed under the MIT license. See https://github.com/AArnott/pinvoke/blob/master/LICENSE.txt for full license information.

using System;
using System.Runtime.InteropServices;

namespace Paket.Bootstrapper.ConsoleRunnerStrategies
{
    class WindowsPInvoke
    {
        public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [DllImport("Kernel32", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        public class SafeObjectHandle : SafeHandle
        {
            public static readonly SafeObjectHandle Invalid = new SafeObjectHandle();
            public static readonly SafeObjectHandle Null = new SafeObjectHandle(IntPtr.Zero, false);

            public SafeObjectHandle()
                : base(INVALID_HANDLE_VALUE, true)
            {
            }

            public SafeObjectHandle(IntPtr preexistingHandle, bool ownsHandle = true)
                : base(INVALID_HANDLE_VALUE, ownsHandle)
            {
                SetHandle(preexistingHandle);
            }

            public override bool IsInvalid => handle == INVALID_HANDLE_VALUE || handle == IntPtr.Zero;
            protected override bool ReleaseHandle() => CloseHandle(handle);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public uint dwFillAttribute;
            public uint dwFlags;
            public ushort wShowWindow;
            public ushort cbReserved2;
            public IntPtr lpReserved2;
            public SafeObjectHandle hStdInput;
            public SafeObjectHandle hStdOutput;
            public SafeObjectHandle hStdError;
            public static STARTUPINFO Create()
            {
                return new STARTUPINFO
                {
                    cb = Marshal.SizeOf(typeof(STARTUPINFO)),
                    hStdInput = SafeObjectHandle.Null,
                    hStdOutput = SafeObjectHandle.Null,
                    hStdError = SafeObjectHandle.Null,
                };
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [DllImport("Kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern unsafe bool CreateProcess(
            string lpApplicationName,
            string lpCommandLine,
            void* lpProcessAttributes,
            void* lpThreadAttributes,
            [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
            uint dwCreationFlags,
            void* lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("Kernel32", SetLastError = true)]
        public static extern uint WaitForSingleObject(
            SafeHandle hHandle,
            int dwMilliseconds);

        [DllImport("Kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetExitCodeProcess(IntPtr hProcess, out int lpExitCode);
    }
}