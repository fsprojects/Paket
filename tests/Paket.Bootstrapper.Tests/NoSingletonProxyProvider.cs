using Paket.Bootstrapper.HelperProxies;

namespace Paket.Bootstrapper.Tests
{
    internal class NoSingletonProxyProvider : IProxyProvider
    {
        public IFileSystemProxy FileSystemProxy => new FileSystemProxy();

        public IWebRequestProxy WebRequestProxy => new WebRequestProxy(EnvProxy);

        public IEnvProxy EnvProxy => new EnvProxy();
    }
}
