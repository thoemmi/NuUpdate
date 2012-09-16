using System;
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

        public void CreateShortcuts(UpdateInfo updateInfo) {
            var appPath = _pathProvider.GetAppPath(updateInfo);

            var instructions = updateInfo.GetUpdateInstructions();
            if (instructions != null && instructions.Shortcuts != null && instructions.Shortcuts.Length > 0) {
                foreach (var shortcut in instructions.Shortcuts) {
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
                    }
                    catch (Exception ex) {
                        _logger.ErrorException(String.Format("Creating shortcut for \"{0}\" failed", shortcut.TargetPath), ex);
                    }
                }
            } else {
                _logger.Info("No update instructions found, creating a shortcut for each executable in start menu.");
                foreach (var exePath in Directory.EnumerateFiles(appPath, "*.exe")) {
                    try {
                        var lnkFilename = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), Path.GetFileNameWithoutExtension(exePath) + ".lnk");
                        new ShellLink {
                            Target = exePath
                        }.Save(lnkFilename);
                        _logger.Info("Created shortcut for \"{0}\" at \"{1}\".", exePath, lnkFilename);
                    }
                    catch (Exception ex) {
                        _logger.ErrorException(String.Format("Creating shortcut for \"{0}\" failed", exePath), ex);
                    }
                }
            }            
        }
    }
}