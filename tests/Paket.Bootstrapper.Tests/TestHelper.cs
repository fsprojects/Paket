using Paket.Bootstrapper.HelperProxies;

namespace Paket.Bootstrapper.Tests
{
    internal static partial class TestHelper
    {
        public static IProxyProvider ProductionProxyProvider { get; } = new DefaultProxyProvider();
        public static IProxyProvider NoSingletonProxyProvider { get; } = new NoSingletonProxyProvider();
    }
}
