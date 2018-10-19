namespace Paket.Bootstrapper.DownloadStrategies
{
    public interface IHaveEffectiveStrategy
    {
        IDownloadStrategy EffectiveStrategy { get; }
    }
}
