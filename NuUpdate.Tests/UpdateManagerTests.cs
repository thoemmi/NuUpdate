using System;
using System.Linq;
using Moq;
using NUnit.Framework;
using NuGet;

namespace NuUpdate.Tests {
    [TestFixture]
    public class UpdateManagerTests {
        private const string APP_NAME = "TestApp";

        [Test]
        public void WithNoCurrentVersionGetAllPackages() {
            var rep = GetLocalRepository();

            var sut = new UpdateManager(APP_NAME, rep);
            sut.CheckForUpdate().Wait();

            Assert.AreEqual(2, sut.AvailableUpdates.Count());
        }

        [Test]
        public void NoNewerReleasePackages() {
            var rep = GetLocalRepository();

            var sut = new UpdateManager(APP_NAME, rep);
            sut.CheckForUpdate(new Version(1, 1)).Wait();

            Assert.AreEqual(0, sut.AvailableUpdates.Count());
        }

        [Test]
        public void FindPrereleasePackage() {
            var rep = GetLocalRepository();

            var sut = new UpdateManager(APP_NAME, rep);
            sut.CheckForUpdate(new Version(1, 1), includePrereleases: true).Wait();

            Assert.AreEqual(1, sut.AvailableUpdates.Count());
        }

        private static IPackageRepository GetLocalRepository() {
            var repository = new Mock<IPackageRepository>();
            var packages = new[] {
                CreateMockPackage("Solutionizer", "1.0"), 
                CreateMockPackage("Solutionizer", "1.1", isLatest: true),
                CreateMockPackage("Solutionizer", "1.2-beta", isAbsoluteLatestVersion: true)
            };
            repository.Setup(c => c.GetPackages()).Returns(packages.AsQueryable);
            return repository.Object;
        }

        private static IPackage CreateMockPackage(string name, string version, string desc = null, string tags = null, bool isLatest = false, bool isAbsoluteLatestVersion = false) {
            var package = new Mock<IPackage>();
            package.SetupGet(p => p.Id).Returns(name);
            package.SetupGet(p => p.Version).Returns(SemanticVersion.Parse(version));
            package.SetupGet(p => p.Description).Returns(desc);
            package.SetupGet(p => p.Tags).Returns(tags);
            package.SetupGet(p => p.Listed).Returns(true);
            package.SetupGet(p => p.IsLatestVersion).Returns(isLatest);
            package.SetupGet(p => p.IsAbsoluteLatestVersion).Returns(isAbsoluteLatestVersion);
            return package.Object;
        }
    }
}