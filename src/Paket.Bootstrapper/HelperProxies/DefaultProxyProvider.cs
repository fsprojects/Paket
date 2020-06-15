using System;

namespace Paket.Bootstrapper.HelperProxies
{
    public class DefaultProxyProvider : IProxyProvider
    {
        private IProxyProvider This => this;

        IFileSystemProxy IProxyProvider.FileSystemProxy { get; } = new FileSystemProxy();

        private readonly IWebRequestProxy webRequestProxy;
        IWebRequestProxy IProxyProvider.WebRequestProxy => webRequestProxy;

        private Lazy<EnvProxy> _instance = new Lazy<EnvProxy>();
        IEnvProxy IProxyProvider.EnvProxy => _instance.Value;

        public DefaultProxyProvider()
        {
            webRequestProxy = new WebRequestProxy(This.EnvProxy);
        }
    }
}
