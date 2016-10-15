using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using static Paket.Bootstrapper.ConsoleRunnerStrategies.UnixPInvoke;

namespace Paket.Bootstrapper.ConsoleRunnerStrategies
{
    class UnixMonoConsoleRunner : IConsoleRunner
    {
        private const int EXECV_FAILED_EXIT_STATUS = 0xFF;
        private static readonly bool IsMono = Type.GetType("Mono.Runtime") != null;

        private static readonly PlatformID[] Platforms =
        {
            PlatformID.Unix,
            PlatformID.MacOSX
        };

        public bool IsSupported => IsMono && Platforms.Contains(Environment.OSVersion.Platform);

        public unsafe int Run(string program, IEnumerable<string> arguments)
        {
            // This whole method is doing dangerous things.
            //
            // We can't use 'execv' directly as it would completly replace our process, that would result in mono not
            // call it's tty_teardown (https://github.com/mono/mono/blob/0bcbe39b148bb498742fc68416f8293ccd350fb6/mono/metadata/console-unix.c#L202)
            // method and the console might look weird. Also we can't execute code anymore after that.
            //
            // The solution is to to use the traditional 'fork' pattern but mono doesn't compile it's GC with
            // '#define HANDLE_FORK' it seem so it's dangerous. Reason is that the child side of a fork only get one
            // thread all other threads don't exists anymore, including a potential GC thread that could have some lock
            // held.
            // 
            // Luckily that would only be a problem if we needed GC to be functional in the child process, but we don't
            // so we preallocate everything. The runtime could theorically still require the GC but we use a CER to
            // completly disable this behavior. 
            var finalArguments = new[] {"mono", program}.Concat(arguments).ToArray();

            // Unix environment are using UTF-8, we prealocate everything and pin the memory we will need
            using (var path = new Utf8UnmanagedString("mono"))
            using (var argv = new Utf8UnmanagedStringArray(finalArguments))
            {
                // Try to ensure that the GC isn't running, in theory we won't trigger it during our CER block but
                // better safe than sorry.
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                GC.WaitForPendingFinalizers();

                int childPid;
                // Start a CER as we need to avoid any allocations by the runtime itself inside of the forked child,
                // none of the GC infrastructure is running there, it would freeze the process.
                RuntimeHelpers.PrepareConstrainedRegions();
                try
                {
                }
                finally
                {
                    childPid = fork();
                    if (childPid == 0)
                    {
                        // If execvp succeed it will replace our process.
                        execvp(path.Pointer, argv.Pointer);

                        // If execvp failed we can't do anything much, especially not let mono continue, thinking that
                        // it can run managed code. So we kill our process.
                        _exit(EXECV_FAILED_EXIT_STATUS);
                    }
                }

                int status;
                if (waitpid(childPid, out status, 0) != -1)
                {
                    if (WIFEXITED(status))
                    {
                        var exitCode = WEXITSTATUS(status);
                        if (exitCode != EXECV_FAILED_EXIT_STATUS)
                        {
                            return exitCode;
                        }
                    }
                }

                throw new InvalidOperationException("Unable to run under mono");
            }
        }

        private unsafe class Utf8UnmanagedString : IDisposable
        {
            private readonly GCHandle handle;

            public Utf8UnmanagedString(string str)
            {
                var count = Encoding.UTF8.GetByteCount(str);
                var bytes = new byte[count + 1];
                Encoding.UTF8.GetBytes(str, 0, str.Length, bytes, 0);
                handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            }

            public byte* Pointer => (byte*) handle.AddrOfPinnedObject().ToPointer();

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            ~Utf8UnmanagedString()
            {
                Dispose(false);
            }

            private void Dispose(bool disposing)
            {
                handle.Free();
            }
        }

        private unsafe class Utf8UnmanagedStringArray : IDisposable
        {
            private readonly GCHandle handle;
            private readonly Utf8UnmanagedString[] unmanagedStrings;

            public Utf8UnmanagedStringArray(string[] strings)
            {
                unmanagedStrings = strings.Select(s => new Utf8UnmanagedString(s)).ToArray();
                var unmanagedPointers = new byte*[unmanagedStrings.Length + 1];
                for (var i = 0; i < unmanagedStrings.Length; i++)
                {
                    unmanagedPointers[i] = unmanagedStrings[i].Pointer;
                }
                handle = GCHandle.Alloc(unmanagedPointers, GCHandleType.Pinned);
            }

            public byte** Pointer => (byte**) handle.AddrOfPinnedObject().ToPointer();

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            ~Utf8UnmanagedStringArray()
            {
                Dispose(false);
            }

            private void Dispose(bool disposing)
            {
                handle.Free();
                if (disposing)
                {
                    foreach (var s in unmanagedStrings)
                    {
                        s.Dispose();
                    }
                }
            }
        }
    }
}