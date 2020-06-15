namespace Paket.Bootstrapper.HelperProxies
{
  public interface IProxyProvider
  {
    IFileSystemProxy FileSystemProxy { get; }
    IWebRequestProxy WebRequestProxy { get; }
    IEnvProxy EnvProxy { get; }
  }
}
