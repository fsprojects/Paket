using Microsoft.Win32.SafeHandles;

namespace Pri.LongPath
{
	internal sealed class SafeFindHandle : SafeHandleZeroOrMinusOneIsInvalid
	{
		internal SafeFindHandle()
			: base(true)
		{
		}

		protected override bool ReleaseHandle()
		{
			return NativeMethods.FindClose(base.handle);
		}
	}
}