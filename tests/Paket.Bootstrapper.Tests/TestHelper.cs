using Paket.Bootstrapper.HelperProxies;

namespace Paket.Bootstrapper.Tests
{
    internal static class TestHelper
    {
        public static IProxyProvider ProxyProvider { get; } = new DefaultProxyProvider();
        public static IProxyProvider TestProxyProvider { get; } = new NoSingletonProxyProvider();

        private class NoSingletonProxyProvider : IProxyProvider
        {
            private IProxyProvider This => this;
            IFileSystemProxy IProxyProvider.FileSystemProxy => new FileSystemProxy();

            IWebRequestProxy IProxyProvider.WebRequestProxy => new WebRequestProxy(This.EnvProxy);

            IEnvProxy IProxyProvider.EnvProxy => new EnvProxy();
        }
    }
}
