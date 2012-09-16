using System;
using System.Linq;
using System.Xml.Linq;
using NLog;
using NuGet;

namespace NuUpdate {
    public class UpdateInfo {
        private readonly IPackage _package;

        public UpdateInfo(IPackage package) {
            _package = package;
        }

        internal IPackage Package {
            get { return _package; }
        }

        public SemanticVersion Version {
            get { return _package.Version; }
        }

        public DateTimeOffset? Published {
            get { return _package.Published; }
        }

        public string ReleaseNotes {
            get { return _package.ReleaseNotes; }
        }

        public bool IsReleaseVersion {
            get { return _package.IsReleaseVersion(); }
        }
    }
}