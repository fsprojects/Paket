using NUnit.Framework;

namespace Paket.Bootstrapper.Tests
{
    [TestFixture]
    public class SemVerTests
    {

        [Test]
        public void NormalVersion()
        {
            //arrange

            //act
            var sut = SemVer.Create("2.57.1");

            //assert
            Assert.That(sut.Major, Is.EqualTo(2));
            Assert.That(sut.Minor, Is.EqualTo(57));
            Assert.That(sut.Patch, Is.EqualTo(1));
            Assert.That(sut.Build, Is.EqualTo("0"));
            Assert.That(sut.PreRelease, Is.Null);
            Assert.That(sut.PreReleaseBuild, Is.EqualTo("0"));
            Assert.That(sut.Original, Is.EqualTo("2.57.1"));
        }

        [Test]
        public void SimplePreRelease()
        {
            //arrange

            //act
            var sut = SemVer.Create("2.57.1-pre");

            //assert
            Assert.That(sut.Major, Is.EqualTo(2));
            Assert.That(sut.Minor, Is.EqualTo(57));
            Assert.That(sut.Patch, Is.EqualTo(1));
            Assert.That(sut.Build, Is.EqualTo("0"));
            Assert.That(sut.PreRelease, Is.Not.Null);
            Assert.That(sut.PreRelease.Name, Is.EqualTo("pre"));
            Assert.That(sut.PreRelease.Number, Is.Null);
            Assert.That(sut.PreRelease.Origin, Is.EqualTo("pre"));
            Assert.That(sut.PreReleaseBuild, Is.EqualTo("0"));
            Assert.That(sut.Original, Is.EqualTo("2.57.1-pre"));
        }

        [Test]
        public void ExtendedPreRelease()
        {
            //arrange

            //act
            var sut = SemVer.Create("2.57.1-pre123");

            //assert
            Assert.That(sut.Major, Is.EqualTo(2));
            Assert.That(sut.Minor, Is.EqualTo(57));
            Assert.That(sut.Patch, Is.EqualTo(1));
            Assert.That(sut.Build, Is.EqualTo("0"));
            Assert.That(sut.PreRelease, Is.Not.Null);
            Assert.That(sut.PreRelease.Name, Is.EqualTo("pre"));
            Assert.That(sut.PreRelease.Number, Is.EqualTo(123));
            Assert.That(sut.PreRelease.Origin, Is.EqualTo("pre123"));
            Assert.That(sut.PreReleaseBuild, Is.EqualTo("0"));
            Assert.That(sut.Original, Is.EqualTo("2.57.1-pre123"));
        }

        [Test]
        public void ExtendedPreReleaseWithBuildNumber()
        {
            //arrange

            //act
            var sut = SemVer.Create("2.57.1.456-pre123");

            //assert
            Assert.That(sut.Major, Is.EqualTo(2));
            Assert.That(sut.Minor, Is.EqualTo(57));
            Assert.That(sut.Patch, Is.EqualTo(1));
            Assert.That(sut.Build, Is.EqualTo("456"));
            Assert.That(sut.PreRelease, Is.Not.Null);
            Assert.That(sut.PreRelease.Name, Is.EqualTo("pre"));
            Assert.That(sut.PreRelease.Number, Is.EqualTo(123));
            Assert.That(sut.PreRelease.Origin, Is.EqualTo("pre123"));
            Assert.That(sut.PreReleaseBuild, Is.EqualTo("0"));
            Assert.That(sut.Original, Is.EqualTo("2.57.1.456-pre123"));
        }

        [Test]
        public void Compare()
        {
            //arrange

            //act
            var result = SemVer.Create("0.0").CompareTo(SemVer.Create("0.1"));

            //assert
            Assert.That(result, Is.EqualTo(0.CompareTo(1)));
        }

        [Test]
        public void ComparePrereleaseToStable()
        {
            //arrange

            //act
            var result = SemVer.Create("0.1-pre").CompareTo(SemVer.Create("0.1"));

            //assert
            Assert.That(result, Is.EqualTo(0.CompareTo(1)));
        }
        
    }
}
