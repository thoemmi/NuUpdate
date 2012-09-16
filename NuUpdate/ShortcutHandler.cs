using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using NLog;
using NuUpdate.Interop;

namespace NuUpdate {
    internal class ShortcutHandler {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private readonly PathProvider _pathProvider;

        public ShortcutHandler(PathProvider pathProvider) {
            _pathProvider = pathProvider;
        }

        private IEnumerable<Shortcut> GetShortcuts(UpdateInfo updateInfo) {
            var instructions = updateInfo.GetUpdateInstructions();
            if (instructions != null && instructions.Shortcuts != null && instructions.Shortcuts.Length > 0) {
                _logger.Info("Found shortcut information in update instructions.");
                return instructions.Shortcuts;
            } else {
                _logger.Info("No update instructions found, creating a shortcut for each executable in start menu.");
                return Directory
                    .EnumerateFiles(_pathProvider.GetAppPath(updateInfo), "*.exe")
                    .Select(exePath => new Shortcut {
                        Title = Path.GetFileNameWithoutExtension(exePath),
                        TargetPath = exePath,
                    });
            }
        }

        public void CreateShortcuts(UpdateInfo updateInfo) {
            var appPath = _pathProvider.GetAppPath(updateInfo);

            foreach (var shortcut in GetShortcuts(updateInfo)) {
                try {
                    var lnkFilename = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), shortcut.Title + ".lnk");
                    var target = Path.Combine(appPath, shortcut.TargetPath);

                    if (!File.Exists(target)) {
                        _logger.Warn("File \"{0}\" does not exist, nevertheless we'll create the shortcut.");
                    }

                    new ShellLink {
                        Target = target,
                        Description = shortcut.Description,
                        Arguments = shortcut.Arguments,
                        IconPath = shortcut.IconPath,
                        IconIndex = shortcut.IconIndex,
                    }.Save(lnkFilename);

                    _logger.Info("Created shortcut for \"{0}\" at \"{1}\".", target, lnkFilename);
                } catch (Exception ex) {
                    _logger.ErrorException(String.Format("Creating shortcut \"{0}\" to \"{1}\" failed", shortcut.Title, shortcut.TargetPath), ex);
                }
            }
        }
    }
}