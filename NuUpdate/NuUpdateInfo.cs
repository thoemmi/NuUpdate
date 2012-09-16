using System;
using System.Linq;
using System.Xml.Linq;
using NLog;

namespace NuUpdate {
    internal class UpdateInstructions {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public Shortcut[] Shortcuts;

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

        internal static UpdateInstructions Load(string path) {
            try {
                var doc = XDocument.Load(path);
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
            }
            catch (Exception ex) {
                _logger.ErrorException(String.Format("Reading update instruction from {0} failed", path), ex);
                return null;
            }
        }

    }

    internal class Shortcut {
        public string Title { get; set; }
        public string Description { get; set; }
        public string TargetPath { get; set; }
        public string Arguments { get; set; }
        public string IconPath { get; set; }
        public int IconIndex { get; set; }
    }
}