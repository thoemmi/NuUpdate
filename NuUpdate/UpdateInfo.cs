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

    public static class UpdateInfoExtensions {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private static string GetChildAsString(XContainer parent, string element) {
            var x = parent.Element(element);
            return x != null ? x.Value : null;
        }

        private static int GetChildAsInt32(XContainer parent, string element) {
            var s = GetChildAsString(parent, element);
            int n;
            if (!String.IsNullOrEmpty(s) && Int32.TryParse(s, out n)) {
                return n;
            }
            return default(int);
        }

        internal static UpdateInstructions GetUpdateInstructions(this UpdateInfo self) {
            var file = self.Package.GetContentFiles().FirstOrDefault(f => f.EffectivePath == "NuUpdate.xml");
            if (file != null) {
                try {
                    var doc = XDocument.Load(file.GetStream());
                    var shortcuts = doc.Descendants("Shortcut").Select(element => new Shortcut {
                        Title = GetChildAsString(element, "Title"),
                        Description = GetChildAsString(element, "Description"),
                        TargetPath = GetChildAsString(element, "TargetPath"),
                        Arguments = GetChildAsString(element, "Arguments"),
                        IconPath = GetChildAsString(element, "IconPath"),
                        IconIndex = GetChildAsInt32(element, "IconPath")
                    }
                        ).ToArray();
                    return new UpdateInstructions {
                        Shortcuts = shortcuts
                    };
                } catch (Exception ex) {
                    _logger.ErrorException(String.Format("Reading update instruction from {0} failed", file.Path), ex);
                }
            } else {
                _logger.Info("No update instructions found.");
            }

            return null;
        }
    }
}