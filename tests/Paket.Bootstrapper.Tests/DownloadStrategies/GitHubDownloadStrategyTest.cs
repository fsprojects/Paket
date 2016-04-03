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
        private Mock<IFileProxy> mockFileProxy;

        [SetUp]
        public void Setup()
        {
            mockWebProxy = new Mock<IWebRequestProxy>();
            mockFileProxy = new Mock<IFileProxy>();
            sut = new GitHubDownloadStrategy(mockWebProxy.Object, mockFileProxy.Object);
        }

        [Test]
        public void GetLatestVersion()
        {
            //arrange
            mockWebProxy.Setup(x => x.DownloadString(GitHubDownloadStrategy.Constants.PaketReleasesLatestUrl)).Returns("<title>Release 2.57.1 · fsprojects/Paket</title>");

            //act
            var result = sut.GetLatestVersion(true, false);

            //assert
            Assert.That(result, Is.EqualTo("2.57.1"));
            mockWebProxy.Verify();
        }

        [Test]
        public void GetLatestVersion_Prerelease()
        {
            //arrange
            mockWebProxy.Setup(x => x.DownloadString(GitHubDownloadStrategy.Constants.PaketReleasesLatestUrl)).Returns("<title>Release 2.57.1 · fsprojects/Paket</title>");
            mockWebProxy.Setup(x => x.DownloadString(GitHubDownloadStrategy.Constants.PaketReleasesUrl)).Returns("Paket/tree/2.57.2-pre\"Paket/tree/2.57.1\"");

            //act
            var result = sut.GetLatestVersion(false, false);

            //assert
            Assert.That(result, Is.EqualTo("2.57.2-pre"));
            mockWebProxy.Verify();
        }

        [Test]
        public void DownloadVersion()
        {
            //arrange
            var byteArray = Encoding.ASCII.GetBytes("paketExeContent");
            var stream = new MemoryStream(byteArray);
            var tempFileName = BootstrapperHelper.GetTempFile("paket");

            mockWebProxy.Setup(x => x.GetResponseStream(It.IsAny<string>())).Returns(stream);
            var buffer = new byte[byteArray.Length];
            mockFileProxy.Setup(x => x.Create(tempFileName)).Returns(new MemoryStream(buffer));

            //act
            sut.DownloadVersion("2.57.1", "paketExeLocation", false);

            //assert
            mockWebProxy.Verify();
            mockFileProxy.Verify(x => x.Copy(tempFileName, "paketExeLocation", true));
            mockFileProxy.Verify(x => x.Delete(tempFileName));
            var text = Encoding.ASCII.GetString(buffer);
            Assert.That(text, Is.EqualTo("paketExeContent"));
        }

        [Test]
        public void SelfUpdate()
        {
            //arrange
            var byteArray = Encoding.ASCII.GetBytes("paketExeContent");
            var stream = new MemoryStream(byteArray);
            var tempFileNameNew = BootstrapperHelper.GetTempFile("newBootstrapper");
            var tempFileNameOld = BootstrapperHelper.GetTempFile("oldBootstrapper");

            mockWebProxy.Setup(x => x.GetResponseStream(It.IsAny<string>())).Returns(stream);
            var buffer = new byte[byteArray.Length];
            mockFileProxy.Setup(x => x.Create(tempFileNameNew)).Returns(new MemoryStream(buffer));
            mockFileProxy.Setup(x => x.GetLocalFileVersion(It.IsAny<string>())).Returns("2.52.1");
            
            //act
            sut.SelfUpdate("2.57.1", false);

            //assert
            mockWebProxy.Verify();
            mockFileProxy.Verify(x => x.FileMove(Assembly.GetAssembly(typeof(GitHubDownloadStrategy)).Location, tempFileNameOld));
            mockFileProxy.Verify(x => x.FileMove(tempFileNameNew, Assembly.GetAssembly(typeof(GitHubDownloadStrategy)).Location));

            var text = Encoding.ASCII.GetString(buffer);
            Assert.That(text, Is.EqualTo("paketExeContent"));
        }
    }
}