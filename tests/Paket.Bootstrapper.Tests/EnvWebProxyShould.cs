using NUnit.Framework;
using System;
using System.Net;
using System.Reflection;

namespace Paket.Bootstrapper.Tests
{
    [TestFixture]
    class EnvWebProxyShould
    {
        private sealed class FakeEnvProxy : EnvProxy
        {
            public FakeEnvProxy()
            {
                var instanceField = typeof(EnvProxy).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
                instanceField.SetValue(null, this);
            }
            new public bool TryGetProxyFor(Uri uri, out IWebProxy proxy)
            {
                return EnvProxy.TryGetProxyFor(uri, out proxy);
            }
        }
        private sealed class DisposableEnvVar : IDisposable
        {
            private readonly string name;
            private readonly string oldValue;
            public DisposableEnvVar(string name, string value = null)
            {
                this.name = name;
                oldValue = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, value);
            }
            public void Dispose()
            {
                Environment.SetEnvironmentVariable(name, oldValue);
            }
        }
        
        [Test] public void GetNoProxyIfNoneDefined()
        {
            using (new DisposableEnvVar("http_proxy"))
            using (new DisposableEnvVar("https_proxy"))
            {
                var envProxy = new FakeEnvProxy();
                IWebProxy proxy;
                Assert.IsFalse(envProxy.TryGetProxyFor(new Uri("http://github.com"), out proxy));
                Assert.IsFalse(envProxy.TryGetProxyFor(new Uri("https://github.com"), out proxy));
            }
        }

        [Test] public void GetHttpProxyWithNoPortNoCredentials()
        {
            using (new DisposableEnvVar("http_proxy", "http://proxy.local"))
            using (new DisposableEnvVar("no_proxy"))
            {
                var envProxy = new FakeEnvProxy();
                IWebProxy proxy;
                Assert.IsTrue(envProxy.TryGetProxyFor(new Uri("http://github.com"), out proxy));
                var webProxy = proxy as WebProxy;
                Assert.IsNotNull(webProxy);
                Assert.That(webProxy.Address, Is.EqualTo(new Uri("http://proxy.local")));
                Assert.IsTrue(webProxy.BypassProxyOnLocal);
                Assert.That(webProxy.BypassList.Length, Is.EqualTo(0));
                Assert.IsNull(webProxy.Credentials);
            }
        }

        [Test] public void GetHttpProxyWithPortNoCredentials()
        {
            using (new DisposableEnvVar("http_proxy", "http://proxy.local:8080"))
            using (new DisposableEnvVar("no_proxy"))
            {
                var envProxy = new FakeEnvProxy();
                IWebProxy proxy;
                Assert.IsTrue(envProxy.TryGetProxyFor(new Uri("http://github.com"), out proxy));
                var webProxy = proxy as WebProxy;
                Assert.IsNotNull(webProxy);
                Assert.That(webProxy.Address, Is.EqualTo(new Uri("http://proxy.local:8080")));
                Assert.IsTrue(webProxy.BypassProxyOnLocal);
                Assert.That(webProxy.BypassList.Length, Is.EqualTo(0));
                Assert.IsNull(webProxy.Credentials);
            }
        }

        [Test] public void GetHttpProxyWithPortAndCredentials()
        {
            const string password = "p@ssw0rd:";
            using (new DisposableEnvVar("http_proxy", string.Format("http://user:{0}@proxy.local:8080", Uri.EscapeDataString(password))))
            using (new DisposableEnvVar("no_proxy"))
            {
                var envProxy = new FakeEnvProxy();
                IWebProxy proxy;
                Assert.IsTrue(envProxy.TryGetProxyFor(new Uri("http://github.com"), out proxy));
                var webProxy = proxy as WebProxy;
                Assert.IsNotNull(webProxy);
                Assert.That(webProxy.Address, Is.EqualTo(new Uri("http://proxy.local:8080")));
                Assert.IsTrue(webProxy.BypassProxyOnLocal);
                Assert.That(webProxy.BypassList.Length, Is.EqualTo(0));
                var credentials = webProxy.Credentials as NetworkCredential;
                Assert.IsNotNull(credentials);
                Assert.That(credentials.UserName, Is.EqualTo("user"));
                Assert.That(credentials.Password, Is.EqualTo(password));
            }
        }

        [Test] public void GetHttpsProxyWithPortAndCredentials()
        {
            const string password = "p@ssw0rd:";
            using (new DisposableEnvVar("https_proxy", string.Format("https://user:{0}@proxy.local:8080", Uri.EscapeDataString(password))))
            using (new DisposableEnvVar("no_proxy"))
            {
                var envProxy = new FakeEnvProxy();
                IWebProxy proxy;
                Assert.IsTrue(envProxy.TryGetProxyFor(new Uri("https://github.com"), out proxy));
                var webProxy = proxy as WebProxy;
                Assert.IsNotNull(webProxy);
                Assert.That(webProxy.Address, Is.EqualTo(new Uri("http://proxy.local:8080")));
                Assert.IsTrue(webProxy.BypassProxyOnLocal);
                Assert.That(webProxy.BypassList.Length, Is.EqualTo(0));
                var credentials = webProxy.Credentials as NetworkCredential;
                Assert.IsNotNull(credentials);
                Assert.That(credentials.UserName, Is.EqualTo("user"));
                Assert.That(credentials.Password, Is.EqualTo(password));
            }
        }

        [Test] public void GetHttpProxyWithBypassList()
        {
            using (new DisposableEnvVar("http_proxy", string.Format("http://proxy.local:8080")))
            using (new DisposableEnvVar("no_proxy", ".local,127.0.0.1"))
            {
                var envProxy = new FakeEnvProxy();
                IWebProxy proxy;
                Assert.IsTrue(envProxy.TryGetProxyFor(new Uri("http://github.com"), out proxy));
                var webProxy = proxy as WebProxy;
                Assert.IsNotNull(webProxy);
                Assert.That(webProxy.Address, Is.EqualTo(new Uri("http://proxy.local:8080")));
                Assert.IsTrue(webProxy.BypassProxyOnLocal);
                Assert.That(webProxy.BypassList.Length, Is.EqualTo(2));
                Assert.That(".local", Does.Match(webProxy.BypassList[0]));
                Assert.That("127.0.0.1", Does.Match(webProxy.BypassList[1]));
                Assert.IsNull(webProxy.Credentials);
            }
        }
    }
}
