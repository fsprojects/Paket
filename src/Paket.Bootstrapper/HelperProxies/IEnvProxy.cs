using System;
using System.Net;

namespace Paket.Bootstrapper.HelperProxies
{
  public interface IEnvProxy
  {
    bool TryGetProxyFor(Uri uri, out IWebProxy proxy);
  }
}
