using System;
using System.Collections.Generic;
using System.Net;
using Moq;
using NUnit.Framework;
using Paket.Bootstrapper.DownloadStrategies;
using Paket.Bootstrapper.HelperProxies;
using System.Security.Cryptography;
using System.IO;
using System.Text;

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
            mockEffectiveStrategy.SetupGet(s => s.Name).Returns("TestStrategy");
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
            MockReadAndCreate();

            //act
            sut.DownloadVersion("any", "any", null);

            //assert
            mockEffectiveStrategy.Verify(x => x.DownloadVersion(It.IsAny<string>(), It.IsAny<string>(), null), Times.Never);
            mockFileProxy.Verify(x => x.CreateExclusive("any"));
        }

        [Test]
        public void DownloadVersion_DownloadFromFallback()
        {
            //arrange
            mockFileProxy.Setup(x => x.FileExists(It.IsAny<string>())).Returns(false);
            mockFileProxy.Setup(x => x.GetTempPath()).Returns("C:\\temp");
            MockReadAndCreate();

            //act
            sut.DownloadVersion("any", "any", null);

            //assert
            mockEffectiveStrategy.Verify(x => x.DownloadVersion("any", It.IsAny<string>(), null));
            mockFileProxy.Verify(x => x.CreateExclusive("any"));
        }

        private void MockReadAndCreate()
        {
            mockFileProxy.Setup(x => x.OpenRead(It.IsAny<string>())).Returns(() => new MemoryStream());
            mockFileProxy.Setup(x => x.CreateExclusive(It.IsAny<string>())).Returns(() => new MemoryStream());
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
            //arrange
            var content = Guid.NewGuid().ToByteArray();
            var sha = new SHA256Managed();
            var checksum = sha.ComputeHash(new MemoryStream(content));
            var hash = BitConverter.ToString(checksum).Replace("-", String.Empty);
            var hashFile = new PaketHashFile(new List<string> { hash + " paket.exe" });

            var pathInCache = Path.Combine(CacheDownloadStrategy.PaketCacheDir, "any", "paket.exe");

            mockEffectiveStrategy.Setup(x => x.GetLatestVersion(true)).Throws<WebException>().Verifiable();
            mockFileProxy.Setup(x => x.OpenRead(pathInCache)).Returns(() => new MemoryStream(Guid.NewGuid().ToByteArray()));
            mockFileProxy.Setup(x => x.CreateExclusive(It.IsAny<string>())).Returns(() => new MemoryStream());
            mockFileProxy.Setup(x => x.OpenRead(It.Is<string>(s => s.StartsWith("C:\\temp"))))
                .Returns(() => new MemoryStream(content));
            mockFileProxy.Setup(x => x.FileExists(pathInCache)).Returns(true);
            mockFileProxy.Setup(x => x.GetTempPath()).Returns("C:\\temp");

            //act
            sut.DownloadVersion("any", "any", hashFile);

            //assert
            mockEffectiveStrategy.Verify(x => x.DownloadVersion(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PaketHashFile>()), Times.Once);
            mockFileProxy.Verify(x => x.CreateExclusive("any"));
        }

        [Test]
        public void GetLatestVersion_PaketFileNotCorrupt_DontDownloadPaketFile()
        {
            //arrange
            var content = Guid.NewGuid().ToByteArray();
            var sha = new SHA256Managed();
            var checksum = sha.ComputeHash(new MemoryStream(content));
            var hash = BitConverter.ToString(checksum).Replace("-", String.Empty);
            var hashFile = new PaketHashFile(new List<string> { hash + " paket.exe" });

            mockEffectiveStrategy.Setup(x => x.GetLatestVersion(true)).Throws<WebException>().Verifiable();
            mockFileProxy.Setup(x => x.OpenRead(It.IsAny<string>())).Returns(() => new MemoryStream(content));
            mockFileProxy.Setup(x => x.CreateExclusive(It.IsAny<string>())).Returns(() => new MemoryStream());
            mockFileProxy.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);

            //act
            sut.DownloadVersion("any", "any", hashFile);

            //assert
            mockEffectiveStrategy.Verify(x => x.DownloadVersion(It.IsAny<string>(), It.IsAny<string>(), null), Times.Never);
            mockFileProxy.Verify(x => x.CreateExclusive("any"));
        }

        [Test]
        public void GetLatestVersion_PaketHashFileExistsButIsCorrupt_ThrowException()
        {
            var hashFile = new PaketHashFile(new List<string> { Guid.NewGuid().ToString().Replace("-", String.Empty) + " not-paket.exe" });

            //arrange
            mockEffectiveStrategy.Setup(x => x.GetLatestVersion(true)).Throws<WebException>().Verifiable();
            mockFileProxy.Setup(x => x.OpenRead(It.IsAny<string>())).Returns(new MemoryStream(Guid.NewGuid().ToByteArray()));
            mockFileProxy.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);

            //act
            Assert.Throws<InvalidDataException>(() => sut.DownloadVersion("any", "any", hashFile));

            //assert
            mockEffectiveStrategy.Verify(x => x.DownloadVersion(It.IsAny<string>(), It.IsAny<string>(), null), Times.Never);
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

            var hashFile = sut.DownloadHashFile("42.0");

            Assert.That(hashFile, Is.Null);
        }

        [Test]
        public void DownloadHashFile_PresentInCache()
        {
            mockEffectiveStrategy.SetupGet(x => x.CanDownloadHashFile).Returns(true);
            var pathInCache = sut.GetHashFilePathInCache("42.0");
            mockFileProxy.Setup(x => x.FileExists(pathInCache)).Returns(true);
            mockFileProxy.Setup(x => x.ReadAllLines(pathInCache)).Returns(new[] {"123test"});

            var hashFile = sut.DownloadHashFile("42.0");

            Assert.That(hashFile.Content, Is.EquivalentTo(new [] { "123test" }));
            mockEffectiveStrategy.Verify(x => x.DownloadHashFile(It.IsAny<string>()), Times.Never);
        }

        private class NonClosableMemoryStream : MemoryStream
        {
            public NonClosableMemoryStream()
            {
            }

            public override void Close()
            {

            }

            public void ReallyClose()
            {
                base.Close();
            }
        }

        [Test]
        public void DownloadHashFile_NotInCache()
        {
            mockEffectiveStrategy.Setup(x => x.DownloadHashFile(It.IsAny<string>())).Returns(new PaketHashFile(new List<string> { "123test" }));
            mockEffectiveStrategy.SetupGet(x => x.CanDownloadHashFile).Returns(true);
            var pathInCache = sut.GetHashFilePathInCache("42.0");
            mockFileProxy.Setup(x => x.FileExists(pathInCache)).Returns(false);
            var backing = new NonClosableMemoryStream();
            mockFileProxy.Setup(x => x.CreateExclusive(pathInCache)).Returns(backing);

            var hashFile = sut.DownloadHashFile("42.0");

            Assert.That(hashFile.Content, Is.EquivalentTo(new[] { "123test" }));
            mockEffectiveStrategy.Verify(x => x.DownloadHashFile("42.0"), Times.Once);
            backing.Seek(0, SeekOrigin.Begin);
            var reader = new StreamReader(backing);
            Assert.That(reader.ReadToEnd(), Is.EqualTo("123test" + Environment.NewLine));
            backing.ReallyClose();
        }
    }
}
