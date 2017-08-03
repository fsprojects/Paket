using System;
using Moq;
using NUnit.Framework;
using Paket.Bootstrapper.DownloadStrategies;
using Paket.Bootstrapper.HelperProxies;
using System.IO;
using Path = Pri.LongPath.Path;
using Directory = Pri.LongPath.Directory;
using DirectoryInfo = Pri.LongPath.DirectoryInfo;
using File = Pri.LongPath.File;

namespace Paket.Bootstrapper.Tests.DownloadStrategies
{
    [TestFixture]
    public class TemporarilyIgnoreUpdatesDownloadStrategyTest
    {
        private const string Target = @"C:\Test\paket.exe";
        private TemporarilyIgnoreUpdatesDownloadStrategy _sut;
        private Mock<IDownloadStrategy> _mockEffectiveStrategy;
        private Mock<IFileSystemProxy> _mockFileProxy;
        private static readonly DateTime Now = new DateTime(2016, 1, 20, 10, 0, 0);

        [SetUp]
        public void Setup()
        {
            _mockEffectiveStrategy = new Mock<IDownloadStrategy>();
            _mockFileProxy = new Mock<IFileSystemProxy>();
            DateTimeProxy.GetNow = () => Now;

            _sut = new TemporarilyIgnoreUpdatesDownloadStrategy(
                _mockEffectiveStrategy.Object, 
                _mockFileProxy.Object,
                Target,
                10);
        }

        [TearDown]
        public void TearDown()
        {
            DateTimeProxy.GetNow = null;
        }

        [Test]
        public void CreateStrategy_With_NoEffective()
        {
            //arrange
            //act
            //assert
            Assert.Throws<ArgumentException>(() => new TemporarilyIgnoreUpdatesDownloadStrategy(null, _mockFileProxy.Object, Target, 10));
        }

        [Test]
        public void CreateStrategy_Without_Target()
        {
            //arrange
            //act
            //assert
            Assert.Throws<ArgumentException>(() => new TemporarilyIgnoreUpdatesDownloadStrategy(_mockEffectiveStrategy.Object, _mockFileProxy.Object, string.Empty, 10));
        }

        [Test]
        public void Name()
        {
            //arrange
            _mockEffectiveStrategy.SetupGet(x => x.Name).Returns("any");

            //act
            //assert
            Assert.That(_sut.Name, Is.EqualTo("any (temporarily ignore updates)"));
        }

        [Test]
        public void GetLatestVersion_Target_Does_Not_Exist()
        {
            //arrange
            _mockFileProxy
                .Setup(fp => fp.GetLastWriteTime(Target))
                .Throws<FileNotFoundException>();

            _mockFileProxy
                .Setup(fp => fp.GetLocalFileVersion(Target))
                .Throws<FileNotFoundException>();

            _mockEffectiveStrategy.Setup(x => x.GetLatestVersion(true)).Returns("any");

            //act
            var result = _sut.GetLatestVersion(true);

            //assert
            Assert.That(result, Is.EqualTo("any"));
            _mockFileProxy.Verify();
            _mockEffectiveStrategy.Verify();
        }

        [Test]
        public void GetLatestVersion_Target_Is_Newer_Than_Threshold()
        {
            //arrange
            _mockFileProxy.Setup(fp => fp.GetLastWriteTime(Target)).Returns(Now.AddMinutes(-9));
            _mockFileProxy.Setup(fp => fp.GetLocalFileVersion(Target)).Returns("any");
            _mockEffectiveStrategy.Setup(x => x.GetLatestVersion(true)).Returns("new");

            //act
            var result = _sut.GetLatestVersion(true);

            //assert
            Assert.That(result, Is.EqualTo("any"));

            _mockFileProxy.Verify();
            _mockEffectiveStrategy.Verify(es => es.GetLatestVersion(true), Times.Never());
        }

        [Test]
        public void GetLatestVersion_Target_Is_Older_Than_Threshold()
        {
            //arrange
            _mockFileProxy.Setup(fp => fp.GetLastWriteTime(Target)).Returns(Now.AddMinutes(-11));
            _mockFileProxy.Setup(fp => fp.GetLocalFileVersion(Target)).Returns("any");
            _mockEffectiveStrategy.Setup(x => x.GetLatestVersion(true)).Returns("new");

            //act
            var result = _sut.GetLatestVersion(true);

            //assert
            Assert.That(result, Is.EqualTo("new"));

            _mockFileProxy.Verify();
            _mockFileProxy.Verify(fp => fp.Touch(Target), Times.Never());
            _mockEffectiveStrategy.Verify();
        }

        [Test]
        public void GetLatestVersion_Target_Is_Older_Than_Threshold_Same_Version()
        {
            //arrange
            _mockFileProxy.Setup(fp => fp.GetLastWriteTime(Target)).Returns(Now.AddMinutes(-11));
            _mockFileProxy.Setup(fp => fp.GetLocalFileVersion(Target)).Returns("any");
            _mockFileProxy.Setup(fp => fp.Touch(Target)).Verifiable();
            _mockEffectiveStrategy.Setup(x => x.GetLatestVersion(true)).Returns("any");

            //act
            var result = _sut.GetLatestVersion(true);

            //assert
            Assert.That(result, Is.EqualTo("any"));

            _mockFileProxy.Verify();
            _mockEffectiveStrategy.Verify();
        }
            
        [Test]
        public void SelfUpdate()
        {
            //arrange
            _mockEffectiveStrategy.Setup(x => x.SelfUpdate("any")).Verifiable();

            //act
            _sut.SelfUpdate("any");

            //assert
            _mockEffectiveStrategy.Verify();
        }

        [Test]
        public void DownloadVersion_Touches_Target()
        {
            //arrange
            _mockFileProxy.Setup(fp => fp.Touch(Target)).Verifiable();
            _mockEffectiveStrategy.Setup(x => x.DownloadVersion("any", Target, null)).Verifiable();

            //act
            _sut.DownloadVersion("any", Target, null);

            //assert
            _mockEffectiveStrategy.Verify();
            _mockFileProxy.Verify();
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void CanDownloadHashFile(bool can)
        {
            _mockEffectiveStrategy.SetupGet(x => x.CanDownloadHashFile).Returns(can);
            Assert.That(_sut.CanDownloadHashFile, Is.EqualTo(can));
        }

        [Test]
        public void DownloadHashFile()
        {
            _mockEffectiveStrategy.Setup(x => x.DownloadHashFile("42.0")).Returns(@"C:\42.txt");

            var hashFilePath = _sut.DownloadHashFile("42.0");

            Assert.That(hashFilePath, Is.EqualTo(@"C:\42.txt"));
            _mockEffectiveStrategy.Verify(x => x.DownloadHashFile("42.0"), Times.Once);
        }
    }
}

