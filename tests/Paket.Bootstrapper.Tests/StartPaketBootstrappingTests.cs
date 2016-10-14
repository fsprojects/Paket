using System;
using System.Net;
using Moq;
using NUnit.Framework;
using Paket.Bootstrapper.DownloadStrategies;
using Paket.Bootstrapper.HelperProxies;

namespace Paket.Bootstrapper.Tests
{
    [TestFixture]
    public class StartPaketBootstrappingTests
    {
        private DownloadArguments dlArgs;
        private Mock<IDownloadStrategy> mockDownloadStrategy;
        private Mock<IFileProxy> mockFileProxy;

        private static Action DoNothing = () => { };

        [SetUp]
        public void Setup()
        {
            mockDownloadStrategy = new Mock<IDownloadStrategy>();
            dlArgs = new DownloadArguments { Target = "paket.exe" };
            mockFileProxy = new Mock<IFileProxy>();
        }

        [Test]
        public void DownloadNewVersion_LocalVersionIsOutdated()
        {
            //arrange
            mockFileProxy.Setup(x => x.GetLocalFileVersion("paket.exe")).Returns("1.0").Verifiable();
            mockDownloadStrategy.Setup(x => x.GetLatestVersion(dlArgs.IgnorePrerelease)).Returns("1.1");

            //act
            Program.StartPaketBootstrapping(mockDownloadStrategy.Object, dlArgs, mockFileProxy.Object, DoNothing);

            //assert
            mockFileProxy.Verify();
            mockDownloadStrategy.Verify(x => x.DownloadVersion("1.1", dlArgs.Target));
        }

        [Test]
        public void DownloadSepcificVersion_IsUpgrade()
        {
            //arrange
            dlArgs.LatestVersion = "1.3";
            mockFileProxy.Setup(x => x.GetLocalFileVersion("paket.exe")).Returns("1.0").Verifiable();

            //act
            Program.StartPaketBootstrapping(mockDownloadStrategy.Object, dlArgs, mockFileProxy.Object, DoNothing);

            //assert
            mockFileProxy.Verify();
            mockDownloadStrategy.Verify(x => x.DownloadVersion("1.3", dlArgs.Target));
        }

        [Test]
        public void DownloadSepcificVersion_IsDowngrade()
        {
            //arrange
            dlArgs.LatestVersion = "1.3";
            mockFileProxy.Setup(x => x.GetLocalFileVersion("paket.exe")).Returns("1.4").Verifiable();

            //act
            Program.StartPaketBootstrapping(mockDownloadStrategy.Object, dlArgs, mockFileProxy.Object, DoNothing);

            //assert
            mockFileProxy.Verify();
            mockDownloadStrategy.Verify(x => x.DownloadVersion("1.3", dlArgs.Target));
        }

        [Test]
        public void DownloadNoNewVersion_LocalVersionIsCurrent()
        {
            //arrange
            mockFileProxy.Setup(x => x.GetLocalFileVersion("paket.exe")).Returns("1.0").Verifiable();
            mockDownloadStrategy.Setup(x => x.GetLatestVersion(dlArgs.IgnorePrerelease)).Returns("1.0");

            //act
            Program.StartPaketBootstrapping(mockDownloadStrategy.Object, dlArgs, mockFileProxy.Object, DoNothing);

            //assert
            mockFileProxy.Verify();
            mockDownloadStrategy.Verify(x => x.DownloadVersion(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void DownloadCurrentVersion_LocalVersionIsPrerelease()
        {
            //arrange
            mockFileProxy.Setup(x => x.GetLocalFileVersion("paket.exe")).Returns("1.1-alpha").Verifiable();
            mockDownloadStrategy.Setup(x => x.GetLatestVersion(dlArgs.IgnorePrerelease)).Returns("1.0");

            //act
            Program.StartPaketBootstrapping(mockDownloadStrategy.Object, dlArgs, mockFileProxy.Object, DoNothing);

            //assert
            mockFileProxy.Verify();
            mockDownloadStrategy.Verify(x => x.DownloadVersion(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Test]
        public void DownloadPrerelease_LocalVersionIsCurrent()
        {
            //arrange
            dlArgs.IgnorePrerelease = !dlArgs.IgnorePrerelease;
            mockFileProxy.Setup(x => x.GetLocalFileVersion("paket.exe")).Returns("1.0").Verifiable();
            mockDownloadStrategy.Setup(x => x.GetLatestVersion(dlArgs.IgnorePrerelease)).Returns("1.1-alpha");

            //act
            Program.StartPaketBootstrapping(mockDownloadStrategy.Object, dlArgs, mockFileProxy.Object, DoNothing);

            //assert
            mockFileProxy.Verify();
            mockDownloadStrategy.Verify(x => x.DownloadVersion("1.1-alpha", dlArgs.Target));
        }

        [Test]
        public void DoSelfUpdate()
        {
            //arrange
            dlArgs.DoSelfUpdate = true;
            mockFileProxy.Setup(x => x.GetLocalFileVersion("paket.exe")).Returns("1.0").Verifiable();
            mockDownloadStrategy.Setup(x => x.GetLatestVersion(dlArgs.IgnorePrerelease)).Returns("1.1");

            //act
            Program.StartPaketBootstrapping(mockDownloadStrategy.Object, dlArgs, mockFileProxy.Object, DoNothing);

            //assert
            mockFileProxy.Verify();
            mockDownloadStrategy.Verify(x => x.SelfUpdate("1.1"));
        }

        [Test]
        public void DoSelfUpdate_IgnorePrerelease()
        {
            //arrange
            dlArgs.DoSelfUpdate = true;
            dlArgs.IgnorePrerelease = !dlArgs.IgnorePrerelease;
            mockFileProxy.Setup(x => x.GetLocalFileVersion("paket.exe")).Returns("1.0").Verifiable();
            mockDownloadStrategy.Setup(x => x.GetLatestVersion(dlArgs.IgnorePrerelease)).Returns("1.1-alpha");

            //act
            Program.StartPaketBootstrapping(mockDownloadStrategy.Object, dlArgs, mockFileProxy.Object, DoNothing);

            //assert
            mockFileProxy.Verify();
            mockDownloadStrategy.Verify(x => x.SelfUpdate("1.1-alpha"));
        }

        [Test]
        public void DoSelfUpdate_SpecificVersion()
        {
            //arrange
            dlArgs.DoSelfUpdate = true;
            dlArgs.LatestVersion = "1.5";
            mockFileProxy.Setup(x => x.GetLocalFileVersion("paket.exe")).Returns("1.0").Verifiable();
            mockDownloadStrategy.Setup(x => x.GetLatestVersion(dlArgs.IgnorePrerelease)).Returns("1.1-alpha");

            //act
            Program.StartPaketBootstrapping(mockDownloadStrategy.Object, dlArgs, mockFileProxy.Object, DoNothing);

            //assert
            mockFileProxy.Verify();
            mockDownloadStrategy.Verify(x => x.SelfUpdate("1.5"));
        }

        [Test]
        public void NoFallbackStrategy_SilentlyFails()
        {
            //arrange
            mockFileProxy.Setup(x => x.GetLocalFileVersion("paket.exe")).Returns("1.0").Verifiable();
            mockDownloadStrategy.Setup(x => x.GetLatestVersion(dlArgs.IgnorePrerelease)).Throws(new WebException());

            //act
            Program.StartPaketBootstrapping(mockDownloadStrategy.Object, dlArgs, mockFileProxy.Object, DoNothing);

            //assert
            mockFileProxy.Verify();
        }

        [Test]
        public void FirstStrategyFails_UseFallbackStrategy_FallbackStrategyWillBeUsed()
        {
            //arrange
            mockFileProxy.Setup(x => x.GetLocalFileVersion("paket.exe")).Returns("1.0").Verifiable();
            var mockFallbackStrategy = new Mock<IDownloadStrategy>();
            mockDownloadStrategy.SetupGet(x => x.FallbackStrategy).Returns(mockFallbackStrategy.Object);
            mockDownloadStrategy.Setup(x => x.GetLatestVersion(dlArgs.IgnorePrerelease)).Throws(new WebException());
            mockFallbackStrategy.Setup(x => x.GetLatestVersion(dlArgs.IgnorePrerelease)).Returns("1.2");

            //act
            Program.StartPaketBootstrapping(mockDownloadStrategy.Object, dlArgs, mockFileProxy.Object, DoNothing);

            //assert
            mockFileProxy.Verify();
            mockFallbackStrategy.Verify(x => x.DownloadVersion("1.2", dlArgs.Target));
        }
        
    }
}
