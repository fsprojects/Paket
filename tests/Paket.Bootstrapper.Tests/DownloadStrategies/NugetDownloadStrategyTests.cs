using System;
using System.IO;
using Moq;
using NUnit.Framework;
using Paket.Bootstrapper.DownloadStrategies;
using Paket.Bootstrapper.HelperProxies;

namespace Paket.Bootstrapper.Tests.DownloadStrategies
{
    [TestFixture]
    public class NugetDownloadStrategyTests
    {
        private NugetDownloadStrategy sut;
        private Mock<IDirectoryProxy> mockDirProxy;
        private Mock<IWebRequestProxy> mockWebRequestProxy;
        private Mock<IFileProxy> mockFileProxy;

        [SetUp]
        public void Setup()
        {
            mockFileProxy = new Mock<IFileProxy>();
            mockDirProxy = new Mock<IDirectoryProxy>();
            mockWebRequestProxy = new Mock<IWebRequestProxy>();
        }

        public void CreateSystemUnderTestWithDefaultApi()
        {
            sut = new NugetDownloadStrategy(mockWebRequestProxy.Object, mockDirProxy.Object, mockFileProxy.Object, "folder", null);
        }

        private void CreateSystemUnderTestWithNugetFolder()
        {
            mockDirProxy.Setup(x => x.Exists("anyNugetFolder")).Returns(true);
            mockWebRequestProxy = null; //set to null, to ensure no test uses it
            sut = new NugetDownloadStrategy(null, mockDirProxy.Object, mockFileProxy.Object, "folder", "anyNugetFolder");
        }

        [Test]
        public void DefaultApi_GetLatestVersion_NoPrerelease()
        {
            //arrange
            CreateSystemUnderTestWithDefaultApi();
            mockWebRequestProxy.Setup(x => x.DownloadString(It.IsAny<string>())).Returns("[\"2.57.1\",\"2.57.0\"]");

            //act
            var version = sut.GetLatestVersion(true, false);

            //assert
            Assert.That(version, Is.EqualTo("2.57.1"));
        }

        [Test]
        public void DefaultApi_GetLatestVersion_WithPrerelease_ChoosePrelease()
        {
            //arrange
            CreateSystemUnderTestWithDefaultApi();
            mockWebRequestProxy.Setup(x => x.DownloadString(It.IsAny<string>())).Returns("[\"2.57.1-pre\",\"2.57.0\"]");

            //act
            var version = sut.GetLatestVersion(false, false);

            //assert
            Assert.That(version, Is.EqualTo("2.57.1-pre"));
        }

        [Test]
        public void DefaultApi_GetLatestVersion_TwoDifferentResults()
        {
            //arrange
            CreateSystemUnderTestWithDefaultApi();
            mockWebRequestProxy.Setup(x => x.DownloadString(It.IsAny<string>())).Returns("[\"2.57.1-pre\",\"2.57.0\"]");

            //act
            var version = sut.GetLatestVersion(false, false);
            var version2 = sut.GetLatestVersion(true, false);

            //assert
            Assert.That(version, Is.Not.EqualTo(version2));
        }

        [Test]
        public void DefaultApi_GetLatestVersion_WithPrerelease_ChooseLatest()
        {
            //arrange
            CreateSystemUnderTestWithDefaultApi();
            mockWebRequestProxy.Setup(x => x.DownloadString(It.IsAny<string>())).Returns("[\"2.57.2\",\"2.57.1-pre\",\"2.57.0\"]");

            //act
            var version = sut.GetLatestVersion(true, false);

            //assert
            Assert.That(version, Is.EqualTo("2.57.2"));
        }

        [Test]
        public void DefaultApi_DownloadVersion()
        {
            //arrange
            CreateSystemUnderTestWithDefaultApi();

            //act
            sut.DownloadVersion(null, "paket", false);

            //assert
            mockWebRequestProxy.Verify(x => x.DownloadFile("https://www.nuget.org/api/v2/package/Paket", It.IsAny<string>()));
            mockFileProxy.Verify(x => x.ExtractToDirectory(It.Is<string>(s => s.StartsWith("folder") && s.EndsWith("paket.latest.nupkg")), It.IsAny<string>()));
            mockFileProxy.Verify(x => x.Copy(It.Is<string>(s => s.StartsWith("folder") && s.EndsWith("paket.exe")), "paket", true));
            mockDirProxy.Verify(x => x.Delete(It.Is<string>(s => s.StartsWith("folder")), true));
        }

        [Test]
        public void DefaultApi_DownloadSpecificVersion()
        {
            //arrange
            CreateSystemUnderTestWithDefaultApi();

            //act
            sut.DownloadVersion("2.57.0", "paket", false);

            //assert
            mockWebRequestProxy.Verify(x => x.DownloadFile("https://www.nuget.org/api/v2/package/Paket/2.57.0", It.IsAny<string>()));
            mockFileProxy.Verify(x => x.ExtractToDirectory(It.Is<string>(s => s.StartsWith("folder") && s.EndsWith("paket.2.57.0.nupkg")), It.IsAny<string>()));
        }

        [Test]
        public void DefaultApi_SelfUpdate_VersionIsEqual_DoNothing()
        {
            //arrange
            CreateSystemUnderTestWithDefaultApi();
            mockFileProxy.Setup(x => x.GetLocalFileVersion(It.IsAny<string>())).Returns("2.57.1");

            //act
            sut.SelfUpdate("2.57.1", false);

            //assert
            mockWebRequestProxy.Verify(x => x.DownloadFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void DefaultApi_SelfUpdate()
        {
            //arrange
            CreateSystemUnderTestWithDefaultApi();
            mockFileProxy.Setup(x => x.GetLocalFileVersion(It.IsAny<string>())).Returns("2.57.0");

            //act
            sut.SelfUpdate("2.57.1", false);

            //assert
            mockDirProxy.Verify(x => x.CreateDirectory(It.Is<string>(s => s.StartsWith("folder"))));
            mockWebRequestProxy.Verify(x => x.DownloadFile("https://www.nuget.org/api/v2/package/Paket.Bootstrapper/2.57.1", It.IsAny<string>()));
            mockFileProxy.Verify(x => x.ExtractToDirectory(It.Is<string>(s => s.EndsWith("paket.bootstrapper.2.57.1.nupkg")), It.IsAny<string>()));
            mockFileProxy.Verify(x => x.FileMove(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2));
            mockDirProxy.Verify(x => x.Delete(It.IsAny<string>(), It.IsAny<bool>()));
        }

        [Test]
        public void DefaultApi_SelfUpdate_FailsWhenNullIsSpecified()
        {
            //arrange
            CreateSystemUnderTestWithDefaultApi();
            mockFileProxy.Setup(x => x.GetLocalFileVersion(It.IsAny<string>())).Returns("2.57.0");

            //act
            Assert.Throws<ArgumentNullException>(() => sut.SelfUpdate(null, false));

            //assert
            mockWebRequestProxy.Verify(x => x.DownloadFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void NugetFolder_GetLatestVersion_NoPrerelease()
        {
            //arrange
            CreateSystemUnderTestWithNugetFolder();
            mockDirProxy.Setup(
                x => x.EnumerateFiles(It.IsAny<string>(), "paket.*.nupkg", SearchOption.TopDirectoryOnly))
                .Returns(new[] { "paket.2.57.0.nupkg", "paket.2.57.1.nupkg" });

            //act
            var version = sut.GetLatestVersion(true, false);

            //assert
            Assert.That(version, Is.EqualTo("2.57.1"));
        }

        [Test]
        public void NugetFolder_GetLatestVersion_WithPrerelease_ChoosePrelease()
        {
            //arrange
            CreateSystemUnderTestWithNugetFolder();
            mockDirProxy.Setup(
                x => x.EnumerateFiles(It.IsAny<string>(), "paket.*.nupkg", SearchOption.TopDirectoryOnly))
                .Returns(new[] { "paket.2.57.1-pre.nupkg", "paket.2.57.0.nupkg" });

            //act
            var version = sut.GetLatestVersion(false, false);

            //assert
            Assert.That(version, Is.EqualTo("2.57.1-pre"));
        }

        [Test]
        public void NugetFolder_GetLatestVersion_TwoDifferentResults()
        {
            //arrange
            CreateSystemUnderTestWithNugetFolder();
            mockDirProxy.Setup(
                x => x.EnumerateFiles(It.IsAny<string>(), "paket.*.nupkg", SearchOption.TopDirectoryOnly))
                .Returns(new[] { "paket.2.57.1-pre.nupkg", "paket.2.57.0.nupkg" });

            //act
            var version = sut.GetLatestVersion(false, false);
            var version2 = sut.GetLatestVersion(true, false);

            //assert
            Assert.That(version, Is.Not.EqualTo(version2));
        }

        [Test]
        public void NugetFolder_GetLatestVersion_WithPrerelease_ChooseLatest()
        {
            //arrange
            CreateSystemUnderTestWithNugetFolder();
            mockDirProxy.Setup(
                x => x.EnumerateFiles(It.IsAny<string>(), "paket.*.nupkg", SearchOption.TopDirectoryOnly))
                .Returns(new[] { "paket.2.57.2.nupkg", "2.57.1-pre.nupkg", "paket.2.57.0.nupkg" });

            //act
            var version = sut.GetLatestVersion(true, false);

            //assert
            Assert.That(version, Is.EqualTo("2.57.2"));
        }

        [Test]
        public void NugetFolder_DownloadVersion_NoVersionSpecified_GetsLatestVersion()
        {
            //arrange
            CreateSystemUnderTestWithNugetFolder();
            mockDirProxy.Setup(
                x => x.EnumerateFiles(It.IsAny<string>(), "paket.*.nupkg", SearchOption.TopDirectoryOnly))
                .Returns(new[] { "paket.111.nupkg" });

            //act
            sut.DownloadVersion(null, "paket", false);

            //assert
            mockFileProxy.Verify(x => x.Copy(It.Is<string>(s => s.StartsWith("anyNugetFolder") && s.EndsWith("paket.111.nupkg")), It.Is<string>(s => s.StartsWith("folder") && s.EndsWith("paket.latest.nupkg")), false));

            mockFileProxy.Verify(x => x.ExtractToDirectory(It.Is<string>(s => s.StartsWith("folder") && s.EndsWith("paket.latest.nupkg")), It.IsAny<string>()));
            mockFileProxy.Verify(x => x.Copy(It.Is<string>(s => s.StartsWith("folder") && s.EndsWith("paket.exe")), "paket", true));
            mockDirProxy.Verify(x => x.Delete(It.Is<string>(s => s.StartsWith("folder")), true));
        }

        [Test]
        public void NugetFolder_DownloadSpecificVersion()
        {
            //arrange
            CreateSystemUnderTestWithNugetFolder();

            //act
            sut.DownloadVersion("2.57.0", "paket", false);

            //assert
            mockFileProxy.Verify(x => x.Copy(It.Is<string>(s => s.StartsWith("anyNugetFolder") && s.EndsWith("paket.2.57.0.nupkg")), It.Is<string>(s => s.StartsWith("folder") && s.EndsWith("paket.2.57.0.nupkg")), false));
            mockFileProxy.Verify(x => x.ExtractToDirectory(It.Is<string>(s => s.StartsWith("folder") && s.EndsWith("paket.2.57.0.nupkg")), It.IsAny<string>()));
        }

        [Test]
        public void NugetFolder_SelfUpdate_VersionIsEqual_DoNothing()
        {
            //arrange
            CreateSystemUnderTestWithNugetFolder();
            mockFileProxy.Setup(x => x.GetLocalFileVersion(It.IsAny<string>())).Returns("2.57.1");

            //act
            sut.SelfUpdate("2.57.1", false);

            //assert
            mockFileProxy.Verify(x => x.Copy(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [Test]
        public void NugetFolder_SelfUpdate()
        {
            //arrange
            CreateSystemUnderTestWithNugetFolder();
            mockFileProxy.Setup(x => x.GetLocalFileVersion(It.IsAny<string>())).Returns("2.57.0");

            //act
            sut.SelfUpdate("2.57.1", false);

            //assert
            mockFileProxy.Verify(x => x.Copy(It.Is<string>(s => s.StartsWith("anyNugetFolder") && s.EndsWith("paket.bootstrapper.2.57.1.nupkg")), It.Is<string>(s => s.StartsWith("folder") && s.EndsWith("paket.bootstrapper.2.57.1.nupkg")), false));

            mockDirProxy.Verify(x => x.CreateDirectory(It.Is<string>(s => s.StartsWith("folder"))));
            mockFileProxy.Verify(x => x.ExtractToDirectory(It.Is<string>(s => s.EndsWith("paket.bootstrapper.2.57.1.nupkg")), It.IsAny<string>()));
            mockFileProxy.Verify(x => x.FileMove(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2));
            mockDirProxy.Verify(x => x.Delete(It.IsAny<string>(), It.IsAny<bool>()));
        }

        [Test]
        public void NugetFolder_SelfUpdate_FailsWhenNullIsSpecified()
        {
            //arrange
            CreateSystemUnderTestWithNugetFolder();
            mockFileProxy.Setup(x => x.GetLocalFileVersion(It.IsAny<string>())).Returns("2.57.0");

            //act
            Assert.Throws<ArgumentNullException>(() => sut.SelfUpdate(null, false));

            //assert
            mockFileProxy.Verify(x => x.Copy(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

    }
}