using System;
using System.Net;
using Moq;
using NUnit.Framework;
using Paket.Bootstrapper.DownloadStrategies;
using Paket.Bootstrapper.HelperProxies;

namespace Paket.Bootstrapper.Tests.DownloadStrategies
{
    [TestFixture]
    public class CacheDownloadStrategyTest
    {
        private CacheDownloadStrategy sut;
        private Mock<IDownloadStrategy> mockFallBack;
        private Mock<IDirectoryProxy> mockDirectoryProxy;
        private Mock<IFileProxy> mockFileProxy;

        [SetUp]
        public void Setup()
        {
            mockFallBack = new Mock<IDownloadStrategy>();
            mockDirectoryProxy = new Mock<IDirectoryProxy>();
            mockFileProxy = new Mock<IFileProxy>();
            sut = new CacheDownloadStrategy(mockFallBack.Object, mockDirectoryProxy.Object, mockFileProxy.Object);
        }

        [Test]
        public void CreateStrategy_With_NoFallback()
        {
            //arrange
            //act
            //assert
            Assert.Throws<ArgumentException>(() => new CacheDownloadStrategy(null, mockDirectoryProxy.Object, mockFileProxy.Object));
        }

        [Test]
        public void GetLatestVersion_NonPrerelease()
        {
            //arrange
            mockFallBack.Setup(x => x.GetLatestVersion(true, false)).Returns("any");

            //act
            var result = sut.GetLatestVersion(true, false);

            //assert
            Assert.That(result, Is.EqualTo("any"));
            mockFallBack.Verify();
        }

        [Test]
        public void GetLatestVersion_FallBackStrategyFails_UseOtherFallback()
        {
            //arrange
            mockFallBack.Setup(x => x.GetLatestVersion(true, false)).Throws<WebException>();
            var mockFallbackFallback = new Mock<IDownloadStrategy>();
            mockFallbackFallback.Setup(x => x.GetLatestVersion(true, false)).Returns("any");
            mockFallBack.SetupGet(x => x.FallbackStrategy).Returns(mockFallbackFallback.Object);

            //act
            var result = sut.GetLatestVersion(true, false);

            //assert
            Assert.That(result, Is.EqualTo("any"));
            mockFallBack.Verify();
            mockFallbackFallback.Verify();
        }

        [Test]
        public void GetLatestVersion_NoFallBackStrategy_UseBestCachedVersion()
        {
            //arrange
            mockFallBack.Setup(x => x.GetLatestVersion(true, false)).Throws<WebException>();
            mockDirectoryProxy.Setup(x => x.GetDirectories(It.IsAny<string>())).Returns(new[] {"2.1", "2.2"});

            //act
            var result = sut.GetLatestVersion(true, false);

            //assert
            Assert.That(result, Is.EqualTo("2.2"));
            mockFallBack.Verify();
        }

        [Test]
        public void DownloadVersion_UseCachedVersion()
        {
            //arrange
            mockFileProxy.Setup(x => x.Exists(It.IsAny<string>())).Returns(true);

            //act
            sut.DownloadVersion("any", "any", true);

            //assert
            mockFallBack.Verify(x => x.DownloadVersion(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
            mockFileProxy.Verify(x => x.Copy(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()));
        }

        [Test]
        public void DownloadVersion_DownloadFromFallback()
        {
            //arrange
            mockFileProxy.Setup(x => x.Exists(It.IsAny<string>())).Returns(false);

            //act
            sut.DownloadVersion("any", "any", true);

            //assert
            mockFallBack.Verify(x => x.DownloadVersion("any", "any", true));
            mockFileProxy.Verify(x => x.Copy(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()));
        }

        [Test]
        public void SelfUpdate()
        {
            //arrange
            //act
            sut.SelfUpdate("any", true);

            //assert
            mockFallBack.Verify(x => x.SelfUpdate("any", true));
        }
    }
}
