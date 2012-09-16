using System;
using System.IO;
using NuGet;

namespace NuUpdate {
    internal class PathProvider {
        private readonly string _packageId;
        private readonly string _appPathBase;
        private readonly string _nuGetCachePath;

        public PathProvider(string packageId, string appPathBase = null, string nuGetCachePath = null) {
            _packageId = packageId;
            _appPathBase = appPathBase ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _packageId);
            _nuGetCachePath = nuGetCachePath ?? Path.Combine(_appPathBase, "packages");
        }

        public string AppPathBase {
            get { return _appPathBase; }
        }

        public string NuGetCachePath {
            get { return _nuGetCachePath; }
        }

        public string GetAppPath(UpdateInfo updateInfo) {
            return GetAppPath(updateInfo.Version);
        }

        public string GetAppPath(SemanticVersion semanticVersion) {
            return Path.Combine(_appPathBase, "app-" + semanticVersion);
        }
    }
}