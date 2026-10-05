using NUnit.Framework;
using Paket.Bootstrapper.DownloadStrategies;

namespace Paket.Bootstrapper.Tests.DownloadStrategies
{
    [TestFixture]
    public class DownloadStrategyVersionCapTests
    {
        private FakeStrategy sut;

        [SetUp]
        public void Setup()
        {
            sut = new FakeStrategy();
        }

        [Test]
        public void GetLatestVersion_CapsAVersionWeCanNoLongerDownload()
        {
            //arrange
            sut.LatestVersion = "12.0.0";

            //act
            var result = sut.GetLatestVersion(true);

            //assert
            Assert.That(result, Is.EqualTo(DownloadStrategy.LastSupportedVersion));
        }

        [Test]
        public void GetLatestVersion_CapsAPrereleaseOfAnUnsupportedMajor()
        {
            //arrange
            sut.LatestVersion = "12.0.0-alpha001";

            //act
            var result = sut.GetLatestVersion(false);

            //assert
            Assert.That(result, Is.EqualTo(DownloadStrategy.LastSupportedVersion));
        }

        [Test]
        public void GetLatestVersion_LeavesTheLastSupportedMajorAlone()
        {
            //arrange
            sut.LatestVersion = "11.2.3";

            //act
            var result = sut.GetLatestVersion(true);

            //assert
            Assert.That(result, Is.EqualTo("11.2.3"));
        }

        [Test]
        public void GetLatestVersion_LeavesAnOlderVersionAlone()
        {
            //arrange
            sut.LatestVersion = "10.3.1";

            //act
            var result = sut.GetLatestVersion(true);

            //assert
            Assert.That(result, Is.EqualTo("10.3.1"));
        }

        [Test]
        public void GetLatestVersion_PassesAnEmptyVersionThrough()
        {
            //arrange
            // An empty string is what the strategies return when they find nothing at all, and
            // SemVer.Create would throw on it.
            sut.LatestVersion = "";

            //act
            var result = sut.GetLatestVersion(true);

            //assert
            Assert.That(result, Is.EqualTo(""));
        }

        [Test]
        public void GetLatestVersion_PassesAnUnparseableVersionThrough()
        {
            //arrange
            sut.LatestVersion = "not a version";

            //act
            var result = sut.GetLatestVersion(true);

            //assert
            Assert.That(result, Is.EqualTo("not a version"));
        }

        private class FakeStrategy : DownloadStrategy
        {
            public string LatestVersion { get; set; }

            public override string Name { get { return "Fake"; } }

            public override bool CanDownloadHashFile { get { return false; } }

            protected override string GetLatestVersionCore(bool ignorePrerelease)
            {
                return LatestVersion;
            }

            protected override void DownloadVersionCore(string latestVersion, string target, PaketHashFile hashfile)
            {
            }

            protected override void SelfUpdateCore(string latestVersion)
            {
            }

            protected override PaketHashFile DownloadHashFileCore(string latestVersion)
            {
                return null;
            }
        }
    }
}
