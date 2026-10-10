using System;

namespace Paket.LegacyStub
{
    public static class Program
    {
        public static int Main()
        {
            Console.Error.WriteLine(
                "paket.exe is no longer published: Paket 12 and later only ship as a .NET tool." + Environment.NewLine +
                "Install it with `dotnet tool install paket` and run `dotnet paket`, see https://fsprojects.github.io/Paket/installation.html" + Environment.NewLine +
                "To keep paket.exe, pin Paket 11 with a `version 11.0.0` line in paket.dependencies, or use paket.bootstrapper.exe 11.x, which never downloads past 11.");
            return 1;
        }
    }
}
