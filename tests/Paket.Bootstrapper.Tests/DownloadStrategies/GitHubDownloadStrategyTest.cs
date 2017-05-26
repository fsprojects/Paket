using System.IO;
using System.Reflection;
using System.Text;
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
        public void DownloadVersion()
        {
            //arrange
            var tempFileName = BootstrapperHelper.GetTempFile("paket");
            mockWebProxy.Setup(x => x.DownloadFile(It.IsAny<string>(), It.IsAny<string>())).Verifiable();

            //act
            sut.DownloadVersion("2.57.1", "paketExeLocation", null);

            //assert
            mockWebProxy.Verify(x => x.DownloadFile(It.IsAny<string>(), tempFileName));
            mockFileProxy.Verify(x => x.CopyFile(tempFileName, "paketExeLocation", true));
            mockFileProxy.Verify(x => x.DeleteFile(tempFileName));
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
    }
}