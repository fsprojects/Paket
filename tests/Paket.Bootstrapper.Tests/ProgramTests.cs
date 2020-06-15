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
            var downloadArguments = new DownloadArguments(String.Empty, true, "any", "any", false, String.Empty, false, null);
            var strategy = Program.GetEffectiveDownloadStrategy(downloadArguments, false, false, TestHelper.ProductionProxyProvider);

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
            var downloadArguments = new DownloadArguments(String.Empty, true, "any", "any", false, String.Empty, false, null);
            var strategy = Program.GetEffectiveDownloadStrategy(downloadArguments, true, false, TestHelper.ProductionProxyProvider);

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
            var downloadArguments = new DownloadArguments(String.Empty, true, "any", "any", false, String.Empty, false, null);
            var strategy = Program.GetEffectiveDownloadStrategy(downloadArguments, true, true, TestHelper.ProductionProxyProvider);

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
            var downloadArguments =  new DownloadArguments(String.Empty, true, "any", "any", false, String.Empty, false, null);
            var strategy = Program.GetEffectiveDownloadStrategy(downloadArguments, false, true, TestHelper.ProductionProxyProvider);

            //assert
            Assert.That(strategy, Is.TypeOf<CacheDownloadStrategy>());
            Assert.That(((CacheDownloadStrategy)strategy).EffectiveStrategy, Is.TypeOf<NugetDownloadStrategy>());
            Assert.That(strategy.FallbackStrategy, Is.Null);
        }

        [Test]
        public void GetDownloadStrategy_NotCached()
        {
            //arrange

            //act
            var downloadArguments = new DownloadArguments(String.Empty, true, "any", "any", false, String.Empty, true, null);
            var strategy = Program.GetEffectiveDownloadStrategy(downloadArguments, false, false, TestHelper.ProductionProxyProvider);

            //assert
            Assert.That(strategy, Is.TypeOf<GitHubDownloadStrategy>());
            Assert.That(strategy.FallbackStrategy, Is.TypeOf<NugetDownloadStrategy>());
            Assert.That(strategy.FallbackStrategy.FallbackStrategy, Is.Null);
        }

        [Test]
        public void GetDownloadStrategy_NotCached_TemporarilyIgnored()
        {
            //arrange

            //act
            var downloadArguments = new DownloadArguments(String.Empty, true, "any", "any", false, String.Empty, true, 10);
            var strategy = Program.GetEffectiveDownloadStrategy(downloadArguments, false, false, TestHelper.ProductionProxyProvider);

            //assert
            Assert.That(strategy, Is.TypeOf<TemporarilyIgnoreUpdatesDownloadStrategy>());
            var effectiveStrategy = ((TemporarilyIgnoreUpdatesDownloadStrategy)strategy).EffectiveStrategy;
            Assert.That(effectiveStrategy, Is.TypeOf<GitHubDownloadStrategy>());
            Assert.That(effectiveStrategy.FallbackStrategy, Is.TypeOf<NugetDownloadStrategy>());
            Assert.That(effectiveStrategy.FallbackStrategy.FallbackStrategy, Is.Null);
            Assert.That(strategy.FallbackStrategy, Is.Null);
        }

        [Test]
        public void GetDownloadStrategy_TemporarilyIgnored_Cached_Nuget_Nothing()
        {
            //arrange

            //act
            var downloadArguments = new DownloadArguments(String.Empty, true, "any", "any", false, String.Empty, false, 10);
            var strategy = Program.GetEffectiveDownloadStrategy(downloadArguments, true, true, TestHelper.ProductionProxyProvider);

            //assert
            Assert.That(strategy, Is.TypeOf<TemporarilyIgnoreUpdatesDownloadStrategy>());
            Assert.That(((TemporarilyIgnoreUpdatesDownloadStrategy)strategy).EffectiveStrategy, Is.TypeOf<CacheDownloadStrategy>());
            Assert.That(((CacheDownloadStrategy)((TemporarilyIgnoreUpdatesDownloadStrategy)strategy).EffectiveStrategy).EffectiveStrategy, Is.TypeOf<NugetDownloadStrategy>());
            Assert.That(strategy.FallbackStrategy, Is.Null);
        }
    }
}
