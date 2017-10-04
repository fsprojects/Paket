using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Moq;
using NUnit.Framework;
using Paket.Bootstrapper.DownloadStrategies;
using Paket.Bootstrapper.HelperProxies;

namespace Paket.Bootstrapper.Tests.DownloadStrategies
{
    [TestFixture]
    public class GitHubDownloadStrategyTest
    {
        private GitHubDownloadStrategy sut;
        private Mock<IWebRequestProxy> mockWebProxy;
        private Mock<IFileSystemProxy> mockFileProxy;

        [SetUp]
        public void Setup()
        {
            mockWebProxy = new Mock<IWebRequestProxy>();
            mockFileProxy = new Mock<IFileSystemProxy>();
            sut = new GitHubDownloadStrategy(mockWebProxy.Object, mockFileProxy.Object);
        }

        [Test]
        public void GetLatestVersion()
        {
            //arrange
            mockWebProxy.Setup(x => x.DownloadString(GitHubDownloadStrategy.Constants.PaketReleasesLatestUrl)).Returns("<title>Release 2.57.1 · fsprojects/Paket</title>").Verifiable();

            //act
            var result = sut.GetLatestVersion(true);

            //assert
            Assert.That(result, Is.EqualTo("2.57.1"));
            mockWebProxy.Verify();
        }

        [Test]
        public void GetLatestVersion_Prerelease()
        {
            //arrange
            mockWebProxy.Setup(x => x.DownloadString(GitHubDownloadStrategy.Constants.PaketReleasesLatestUrl)).Returns("<title>Release 2.57.1 · fsprojects/Paket</title>").Verifiable();
            mockWebProxy.Setup(x => x.DownloadString(GitHubDownloadStrategy.Constants.PaketReleasesUrl)).Returns("Paket/tree/2.57.2-pre\"Paket/tree/2.57.1\"").Verifiable();

            //act
            var result = sut.GetLatestVersion(false);

            //assert
            Assert.That(result, Is.EqualTo("2.57.2-pre"));
            mockWebProxy.Verify();
        }

        [Test]
        public void DownloadVersion_NoHash()
        {
            //arrange
            var tempFileName = BootstrapperHelper.GetTempFile("paket");

            //act
            sut.DownloadVersion("2.57.1", "paketExeLocation", null);

            //assert
            mockWebProxy.Verify(x => x.DownloadFile(It.IsAny<string>(), tempFileName), Times.Once);
            mockFileProxy.Verify(x => x.CopyFile(tempFileName, "paketExeLocation", true), Times.Once);
            mockFileProxy.Verify(x => x.DeleteFile(tempFileName), Times.Once);
        }

        [Test]
        public void DownloadVersion_HashFileNotFound()
        {
            //arrange
            var tempFileName = BootstrapperHelper.GetTempFile("paket");

            //act
            sut.DownloadVersion("2.57.1", "paketExeLocation", null);

            //assert
            mockWebProxy.Verify(x => x.DownloadFile(It.IsAny<string>(), tempFileName), Times.Once);
            mockFileProxy.Verify(x => x.CopyFile(tempFileName, "paketExeLocation", true), Times.Once);
            mockFileProxy.Verify(x => x.DeleteFile(tempFileName), Times.Once);
        }

        [Test]
        public void DownloadVersion_InvalidHashRetryOnce()
        {
            //arrange
            var content = Guid.NewGuid().ToByteArray();
            var sha = new SHA256Managed();
            var checksum = sha.ComputeHash(new MemoryStream(content));
            var hash = BitConverter.ToString(checksum).Replace("-", String.Empty);
            mockFileProxy.Setup(x => x.OpenRead(It.IsAny<string>())).Returns(() => new MemoryStream(Guid.NewGuid().ToByteArray()));
            var tempFileName = BootstrapperHelper.GetTempFile("paket");

            //act
            sut.DownloadVersion("2.57.1", "paketExeLocation", new PaketHashFile(new List<string> { hash + " paket.exe" }));

            //assert
            mockWebProxy.Verify(x => x.DownloadFile(It.IsAny<string>(), tempFileName), Times.Exactly(2));
            mockFileProxy.Verify(x => x.CopyFile(tempFileName, "paketExeLocation", true), Times.Once);
            mockFileProxy.Verify(x => x.DeleteFile(tempFileName), Times.Once);
        }

        [Test]
        public void DownloadVersion_ValidHashOk()
        {
            //arrange
            var content = Guid.NewGuid().ToByteArray();
            var sha = new SHA256Managed();
            var checksum = sha.ComputeHash(new MemoryStream(content));
            var hash = BitConverter.ToString(checksum).Replace("-", String.Empty);
            mockFileProxy.Setup(x => x.OpenRead(It.IsAny<string>())).Returns(new MemoryStream(content));
            var tempFileName = BootstrapperHelper.GetTempFile("paket");

            //act
            sut.DownloadVersion("2.57.1", "paketExeLocation", new PaketHashFile(new List<string> { hash + " paket.exe" }));

            //assert
            mockWebProxy.Verify(x => x.DownloadFile(It.IsAny<string>(), tempFileName), Times.Once);
            mockFileProxy.Verify(x => x.CopyFile(tempFileName, "paketExeLocation", true), Times.Once);
            mockFileProxy.Verify(x => x.DeleteFile(tempFileName), Times.Once);
        }

        [Test]
        public void SelfUpdate()
        {
            //arrange
            var tempFileNameNew = BootstrapperHelper.GetTempFile("newBootstrapper");
            var tempFileNameOld = BootstrapperHelper.GetTempFile("oldBootstrapper");

            mockWebProxy.Setup(x => x.DownloadFile(It.IsAny<string>(), It.IsAny<string>())).Verifiable();
            mockFileProxy.Setup(x => x.GetLocalFileVersion(It.IsAny<string>())).Returns("2.52.1");
            
            //act
            sut.SelfUpdate("2.57.1");

            //assert
            mockWebProxy.Verify(x => x.DownloadFile(It.IsAny<string>(), tempFileNameNew));
            mockFileProxy.Verify(x => x.MoveFile(Assembly.GetAssembly(typeof(GitHubDownloadStrategy)).Location, tempFileNameOld));
            mockFileProxy.Verify(x => x.MoveFile(tempFileNameNew, Assembly.GetAssembly(typeof(GitHubDownloadStrategy)).Location));
        }

        [Test]
        public void DownloadHashFile()
        {
            var expectedUrl = string.Format(GitHubDownloadStrategy.Constants.PaketCheckSumDownloadUrlTemplate, "42.0");
            mockWebProxy.Setup(x => x.DownloadString(expectedUrl)).Returns("123test");

            var hashFile = sut.DownloadHashFile("42.0");

            Assert.That(hashFile.Content, Is.EquivalentTo(new[] { "123test"}));
            mockWebProxy.Verify(x => x.DownloadString(expectedUrl), Times.Once);
        }

        [Test]
        public void CanDownloadHashFile()
        {
            Assert.That(sut.CanDownloadHashFile, Is.True);
        }
    }
}