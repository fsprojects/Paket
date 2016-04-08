using System;
using NUnit.Framework;
using Paket.Bootstrapper.DownloadStrategies;

namespace Paket.Bootstrapper.Tests
{
    [TestFixture]
    public class ProgramTests
    {

        [Test]
        public void GetDownloadStrategy_Cached_Github_Nuget()
        {
            //arrange

            //act
            var downloadArguments = new DownloadArguments(String.Empty, true, "any", "any", false, String.Empty, false);
            var strategy = Program.GetEffectiveDownloadStrategy(downloadArguments, false, false);

            //assert
            Assert.That(strategy, Is.TypeOf<CacheDownloadStrategy>());
            Assert.That(((CacheDownloadStrategy)strategy).EffectiveStrategy, Is.TypeOf<GitHubDownloadStrategy>());
            Assert.That(strategy.FallbackStrategy, Is.TypeOf<CacheDownloadStrategy>());
            Assert.That(((CacheDownloadStrategy)strategy.FallbackStrategy).EffectiveStrategy, Is.TypeOf<NugetDownloadStrategy>());
            Assert.That(strategy.FallbackStrategy.FallbackStrategy, Is.Null);
        }

        [Test]
        public void GetDownloadStrategy_Cached_Nuget_Github()
        {
            //arrange

            //act
            var downloadArguments = new DownloadArguments(String.Empty, true, "any", "any", false, String.Empty, false);
            var strategy = Program.GetEffectiveDownloadStrategy(downloadArguments, true, false);

            //assert
            Assert.That(strategy, Is.TypeOf<CacheDownloadStrategy>());
            Assert.That(((CacheDownloadStrategy)strategy).EffectiveStrategy, Is.TypeOf<NugetDownloadStrategy>());
            Assert.That(strategy.FallbackStrategy, Is.TypeOf<CacheDownloadStrategy>());
            Assert.That(((CacheDownloadStrategy)strategy.FallbackStrategy).EffectiveStrategy, Is.TypeOf<GitHubDownloadStrategy>());
        }

        [Test]
        public void GetDownloadStrategy_Cached_Nuget_Nothing()
        {
            //arrange

            //act
            var downloadArguments = new DownloadArguments(String.Empty, true, "any", "any", false, String.Empty, false);
            var strategy = Program.GetEffectiveDownloadStrategy(downloadArguments, true, true);

            //assert
            Assert.That(strategy, Is.TypeOf<CacheDownloadStrategy>());
            Assert.That(((CacheDownloadStrategy)strategy).EffectiveStrategy, Is.TypeOf<NugetDownloadStrategy>());
            Assert.That(strategy.FallbackStrategy, Is.Null);
        }

        [Test]
        public void GetDownloadStrategy_Cached_Nuget_Nothing_ForceOnly()
        {
            //arrange

            //act
            var downloadArguments = new DownloadArguments(String.Empty, true, "any", "any", false, String.Empty, false);
            var strategy = Program.GetEffectiveDownloadStrategy(downloadArguments, false, true);

            //assert
            Assert.That(strategy, Is.TypeOf<CacheDownloadStrategy>());
            Assert.That(((CacheDownloadStrategy)strategy).EffectiveStrategy, Is.TypeOf<NugetDownloadStrategy>());
            Assert.That(strategy.FallbackStrategy, Is.Null);
        }
    }
}
