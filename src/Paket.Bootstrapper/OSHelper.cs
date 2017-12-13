using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Linq;

namespace Paket.Bootstrapper
{
    internal static class OSHelper
    {
        public static bool IsWindow
        {
            get { return Environment.OSVersion.Platform == PlatformID.Win32NT; }
        }
    }
}
