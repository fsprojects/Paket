namespace Paket.Bootstrapper.HelperProxies
{
    public class DefaultProxyProvider : IProxyProvider
    {
        public IFileSystemProxy FileSystemProxy { get; }

        public IWebRequestProxy WebRequestProxy { get; }

        public IEnvProxy EnvProxy { get; }

        public DefaultProxyProvider()
        {
            FileSystemProxy = new FileSystemProxy();
            EnvProxy = new EnvProxy();
            WebRequestProxy = new WebRequestProxy(EnvProxy);
        }
    }
}
