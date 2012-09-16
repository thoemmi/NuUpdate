using System;
using System.IO;
using Moq;
using NUnit.Framework;
using NuGet;

namespace NuUpdate.Tests {
    [TestFixture]
    public class PathProviderTests {
        [Test]
        public void PreservesGivenPath() {
            var tempPath = Path.GetTempPath();

            var sut = new PathProvider("testapp", tempPath);

            Assert.AreEqual(tempPath, sut.AppPathBase);
        }

        [Test]
        public void AppPathDefaultsToUnderLocalAppData() {
            var sut = new PathProvider("testapp");

            var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "testapp");
            Assert.AreEqual(expected, sut.AppPathBase);
        }

        [Test]
        public void CacheFolderDefaultsUnderAppBasePath() {
            var sut = new PathProvider("testapp");

            var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "testapp", "packages");
            Assert.AreEqual(expected, sut.NuGetCachePath);
        }

        [Test]
        public void CreatesVersionedPathForSemanticVersion() {
            var sut = new PathProvider("testapp");

            var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "testapp", "app-1.2.3-beta");
            Assert.AreEqual(expected, sut.GetAppPath(new SemanticVersion(1, 2, 3, "beta")));
        }

        [Test]
        public void CreatesVersionedPathForUpdateInfo() {
            var package = new Mock<IPackage>();
            package.SetupGet(p => p.Version).Returns(new SemanticVersion(1, 2, 3, "beta"));

            var sut = new PathProvider("testapp");

            var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "testapp", "app-1.2.3-beta");
            Assert.AreEqual(expected, sut.GetAppPath(new UpdateInfo(package.Object)));
        }
    }
}