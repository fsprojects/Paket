using NUnit.Framework;
using Paket.Bootstrapper.DownloadStrategies;

namespace Paket.Bootstrapper.Tests.DownloadStrategies
{
    [TestFixture]
    public class NugetApiHelperTests
    {

        [Test]
        public void DefaultSource()
        {
            var sut = new NugetDownloadStrategy.NugetApiHelper("anyPackage", null);
            Assert.That(sut.GetAllPackageVersions(false), Is.EqualTo("https://www.nuget.org/api/v2/package-versions/anyPackage"));
            Assert.That(sut.GetAllPackageVersions(true), Is.EqualTo("https://www.nuget.org/api/v2/package-versions/anyPackage?includePrerelease=true"));
            Assert.That(sut.GetLatestPackage(), Is.EqualTo("https://www.nuget.org/api/v2/package/anyPackage"));
            Assert.That(sut.GetSpecificPackageVersion(null), Is.EqualTo("https://www.nuget.org/api/v2/package/anyPackage/"));
            Assert.That(sut.GetSpecificPackageVersion("any"), Is.EqualTo("https://www.nuget.org/api/v2/package/anyPackage/any"));
        }

        [Test]
        public void AnySource()
        {
            var sut = new NugetDownloadStrategy.NugetApiHelper("anyPackage", "anySource");
            Assert.That(sut.GetAllPackageVersions(false), Is.EqualTo("anySource/package-versions/anyPackage"));
            Assert.That(sut.GetAllPackageVersions(true), Is.EqualTo("anySource/package-versions/anyPackage?includePrerelease=true"));
            Assert.That(sut.GetLatestPackage(), Is.EqualTo("anySource/package/anyPackage"));
            Assert.That(sut.GetSpecificPackageVersion(null), Is.EqualTo("anySource/package/anyPackage/"));
            Assert.That(sut.GetSpecificPackageVersion("any"), Is.EqualTo("anySource/package/anyPackage/any"));
        }
    }
}
