using System;
using System.Net;
using Moq;
using NUnit.Framework;
using Paket.Bootstrapper.DownloadStrategies;
using Paket.Bootstrapper.HelperProxies;
using System.Security.Cryptography;
using System.IO;
using Path = Pri.LongPath.Path;
using Directory = Pri.LongPath.Directory;
using DirectoryInfo = Pri.LongPath.DirectoryInfo;
using File = Pri.LongPath.File;

namespace Paket.Bootstrapper.Tests.DownloadStrategies
{
    [TestFixture]
    public class CacheDownloadStrategyTest
    {
        private CacheDownloadStrategy sut;
        private Mock<IDownloadStrategy> mockEffectiveStrategy;
        private Mock<IFileSystemProxy> mockFileProxy;

        [SetUp]
        public void Setup()
        {
            mockEffectiveStrategy = new Mock<IDownloadStrategy>();
            mockFileProxy = new Mock<IFileSystemProxy>();
            sut = new CacheDownloadStrategy(mockEffectiveStrategy.Object, mockFileProxy.Object);
        }

        [Test]
        public void CreateStrategy_With_NoEffective()
        {
            //arrange
            //act
            //assert
            Assert.Throws<ArgumentException>(() => new CacheDownloadStrategy(null, mockFileProxy.Object));
        }

        [Test]
        public void CreateStrategy_EffectiveStrategyHasFallback()
        {
            //arrange
            mockEffectiveStrategy.SetupGet(x => x.FallbackStrategy).Returns(new Mock<DownloadStrategy>().Object);

            //act
            //assert
            Assert.Throws<ArgumentException>(() => new CacheDownloadStrategy(mockEffectiveStrategy.Object, mockFileProxy.Object));
        }

        [Test]
        public void Name()
        {
            //arrange
            mockEffectiveStrategy.SetupGet(x => x.Name).Returns("any");
            //act
            //assert
            Assert.That(sut.Name, Is.EqualTo("any - cached"));
        }

        [Test]
        public void GetLatestVersion_NonPrerelease()
        {
            //arrange
            mockEffectiveStrategy.Setup(x => x.GetLatestVersion(true)).Returns("any");

            //act
            var result = sut.GetLatestVersion(true);

            //assert
            Assert.That(result, Is.EqualTo("any"));
            mockEffectiveStrategy.Verify();
        }
        
        [Test]
        public void GetLatestVersion_EffectiveStrategyFails_UseFallback()
        {
            //arrange
            mockEffectiveStrategy.Setup(x => x.GetLatestVersion(true)).Throws<WebException>().Verifiable();
            var mockFallback = new Mock<DownloadStrategy>();
            mockEffectiveStrategy.SetupGet(x => x.FallbackStrategy).Returns(mockFallback.Object);
            mockFileProxy.Setup(x => x.GetDirectories(It.IsAny<string>())).Returns(new[] { "1.0" });

            //act
            var result = sut.GetLatestVersion(true);

            //assert
            Assert.That(result, Is.EqualTo("1.0"));
            mockFallback.Verify();
        }

        [Test]
        public void GetLatestVersion_NoFallBackStrategy_UseBestCachedVersion()
        {
            //arrange
            mockEffectiveStrategy.Setup(x => x.GetLatestVersion(true)).Throws<WebException>().Verifiable();
            mockFileProxy.Setup(x => x.GetDirectories(It.IsAny<string>())).Returns(new[] {"2.1", "2.2"});

            //act
            var result = sut.GetLatestVersion(true);

            //assert
            Assert.That(result, Is.EqualTo("2.2"));
            mockEffectiveStrategy.Verify();
        }

        [Test]
        public void GetLatestVersion_NoFallBackStrategy_UseBestCachedVersion_CanHandleWrongSubdirectories()
        {
            //arrange
            mockEffectiveStrategy.Setup(x => x.GetLatestVersion(true)).Throws<WebException>().Verifiable();
            mockFileProxy.Setup(x => x.GetDirectories(It.IsAny<string>())).Returns(new[] { "2.1", "2.2", "wrongVersion" });

            //act
            var result = sut.GetLatestVersion(true);

            //assert
            Assert.That(result, Is.EqualTo("2.2"));
        }

        [Test]
        public void GetLatestVersion_NoFallBackStrategy_UseBestCachedVersion_IgnorePrereleaseSubFolder()
        {
            //arrange
            mockEffectiveStrategy.Setup(x => x.GetLatestVersion(true)).Throws<WebException>().Verifiable();
            mockFileProxy.Setup(x => x.GetDirectories(It.IsAny<string>())).Returns(new[] { "2.1", "2.2", "2.3-alpha" });

            //act
            var result = sut.GetLatestVersion(true);

            //assert
            Assert.That(result, Is.EqualTo("2.2"));
        }

        [Test]
        public void DownloadVersion_UseCachedVersion()
        {
            //arrange
            mockFileProxy.Setup(x => x.FileExists(ItHasFilename("paket.exe"))).Returns(true);

            //act
            sut.DownloadVersion("any", "any", null);

            //assert
            mockEffectiveStrategy.Verify(x => x.DownloadVersion(It.IsAny<string>(), It.IsAny<string>(), null), Times.Never);
            mockFileProxy.Verify(x => x.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()));
        }

        [Test]
        public void DownloadVersion_DownloadFromFallback()
        {
            //arrange
            mockFileProxy.Setup(x => x.FileExists(It.IsAny<string>())).Returns(false);

            //act
            sut.DownloadVersion("any", "any", null);

            //assert
            mockEffectiveStrategy.Verify(x => x.DownloadVersion("any", "any", null));
            mockFileProxy.Verify(x => x.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()));
        }

        [Test]
        public void SelfUpdate()
        {
            //arrange
            //act
            sut.SelfUpdate("any");

            //assert
            mockEffectiveStrategy.Verify(x => x.SelfUpdate("any"));
        }
        
        [Test]
        public void GetLatestVersion_PaketFileCorrupt_DownloadPaketFile()
        {
            const string hashFile = @"C:\hash.txt";

            //arrange
            mockEffectiveStrategy.Setup(x => x.GetLatestVersion(true)).Throws<WebException>().Verifiable();
            mockFileProxy.Setup(x => x.OpenRead(It.IsAny<string>())).Returns(new MemoryStream(Guid.NewGuid().ToByteArray()));
            mockFileProxy.Setup(x => x.ReadAllLines(hashFile)).Returns(new[] { Guid.NewGuid().ToString().Replace("-", String.Empty) + " paket.exe" });
            mockFileProxy.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);

            //act
            sut.DownloadVersion("any", "any", hashFile);

            //assert
            mockEffectiveStrategy.Verify(x => x.DownloadVersion(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            mockFileProxy.Verify(x => x.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()));
        }

        [Test]
        public void GetLatestVersion_PaketFileNotCorrupt_DontDownloadPaketFile()
        {
            const string hashFile = @"C:\hash.txt";

            //arrange
            var content = Guid.NewGuid().ToByteArray();
            var sha = new SHA256Managed();
            var checksum = sha.ComputeHash(new MemoryStream(content));
            var hash = BitConverter.ToString(checksum).Replace("-", String.Empty);
            mockEffectiveStrategy.Setup(x => x.GetLatestVersion(true)).Throws<WebException>().Verifiable();
            mockFileProxy.Setup(x => x.OpenRead(It.IsAny<string>())).Returns(new MemoryStream(content));
            mockFileProxy.Setup(x => x.ReadAllLines(hashFile)).Returns(new[] { hash + " paket.exe" });
            mockFileProxy.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);

            //act
            sut.DownloadVersion("any", "any", hashFile);

            //assert
            mockEffectiveStrategy.Verify(x => x.DownloadVersion(It.IsAny<string>(), It.IsAny<string>(), null), Times.Never);
            mockFileProxy.Verify(x => x.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()));
        }

        [Test]
        public void GetLatestVersion_PaketHashFileExistsButIsCorrupt_DeleteHashFileAndThrowException()
        {
            const string hashFile = @"C:\hash.txt";

            //arrange
            mockEffectiveStrategy.Setup(x => x.GetLatestVersion(true)).Throws<WebException>().Verifiable();
            mockFileProxy.Setup(x => x.OpenRead(It.IsAny<string>())).Returns(new MemoryStream(Guid.NewGuid().ToByteArray()));
            mockFileProxy.Setup(x => x.ReadAllLines(hashFile)).Returns(new[] { Guid.NewGuid().ToString().Replace("-", String.Empty) + " not-paket.exe" });
            mockFileProxy.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);

            //act
            Assert.Throws<InvalidDataException>(() => sut.DownloadVersion("any", "any", hashFile));

            //assert
            mockEffectiveStrategy.Verify(x => x.DownloadVersion(It.IsAny<string>(), It.IsAny<string>(), null), Times.Never);
            mockFileProxy.Verify(x => x.DeleteFile(hashFile));
        }

        public string ItHasFilename(string filename)
        {
            return It.IsRegex($@"\w*[\\/]{filename}");
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void CanDownloadHashFile(bool can)
        {
            mockEffectiveStrategy.SetupGet(x => x.CanDownloadHashFile).Returns(can);
            Assert.That(sut.CanDownloadHashFile, Is.EqualTo(can));
        }

        [Test]
        public void DownloadHashFile_NotSupported()
        {
            mockEffectiveStrategy.SetupGet(x => x.CanDownloadHashFile).Returns(false);

            var hashFilePath = sut.DownloadHashFile("42.0");

            Assert.That(hashFilePath, Is.Null);
        }

        [Test]
        public void DownloadHashFile_PresentInCache()
        {
            mockEffectiveStrategy.SetupGet(x => x.CanDownloadHashFile).Returns(true);
            var pathInCache = sut.GetHashFilePathInCache("42.0");
            mockFileProxy.Setup(x => x.FileExists(pathInCache)).Returns(true);

            var hashFilePath = sut.DownloadHashFile("42.0");

            Assert.That(hashFilePath, Is.EqualTo(pathInCache));
            mockEffectiveStrategy.Verify(x => x.DownloadHashFile(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void DownloadHashFile_NotInCache()
        {
            const string pathFromEffectiveStrategy = @"C:\hash.txt";
            mockEffectiveStrategy.Setup(x => x.DownloadHashFile(It.IsAny<string>())).Returns(pathFromEffectiveStrategy);
            mockEffectiveStrategy.SetupGet(x => x.CanDownloadHashFile).Returns(true);
            var pathInCache = sut.GetHashFilePathInCache("42.0");
            mockFileProxy.Setup(x => x.FileExists(pathInCache)).Returns(false);

            var hashFilePath = sut.DownloadHashFile("42.0");

            Assert.That(hashFilePath, Is.EqualTo(pathInCache));
            mockEffectiveStrategy.Verify(x => x.DownloadHashFile("42.0"), Times.Once);
            mockFileProxy.Verify(x => x.CopyFile(pathFromEffectiveStrategy, pathInCache, true), Times.Once);
        }
    }
}
