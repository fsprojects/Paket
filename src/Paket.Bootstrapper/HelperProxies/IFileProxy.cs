namespace Paket.Bootstrapper.HelperProxies
{
    public interface IFileProxy
    {
        bool Exists(string filename);
        void Copy(string fileFrom, string fileTo, bool overwrite = false);
    }
}