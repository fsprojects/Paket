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
                "To keep paket.exe, move to the bootstrapper of Paket 11, which stays on the latest 11.x: remove the `version` line from paket.dependencies, then run `.paket/paket.bootstrapper.exe --self`." + Environment.NewLine +
                "If .paket/paket.exe is the bootstrapper itself (magic mode), replace it with https://github.com/fsprojects/Paket/releases/latest/download/paket.bootstrapper.exe");
            return 1;
        }
    }
}
