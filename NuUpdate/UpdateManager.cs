using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using NLog;
using NuGet;

namespace NuUpdate {
    public class UpdateManager : IUpdateManager {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly string _packageId;
        private readonly SemanticVersion _currentVersion;
        private readonly IPackageRepository _packageRepository;
        private readonly PathProvider _pathProvider;

        private UpdateInfo _latestPackage;
        private List<UpdateInfo> _availableUpdates = new List<UpdateInfo>();

        public UpdateManager(string packageId, SemanticVersion currentVersion, string packageSource, string appPathBase = null)
            : this(packageId, currentVersion, packageSource, appPathBase, null) {
        }

        public UpdateManager(string packageId, SemanticVersion currentVersion, IPackageRepository packageRepository, string appPathBase = null)
            : this(packageId, currentVersion, packageRepository, appPathBase, null) {
        }

        internal UpdateManager(string packageId, SemanticVersion currentVersion, string packageSource, string appPathBase, string nuGetCachePath)
            : this(packageId, currentVersion, PackageRepositoryFactory.Default.CreateRepository(packageSource), appPathBase, nuGetCachePath) {
        }

        internal UpdateManager(string packageId, SemanticVersion currentVersion, IPackageRepository packageRepository, string appPathBase, string nuGetCachePath) {
            _packageId = packageId;
            _currentVersion = currentVersion;
            _packageRepository = packageRepository;
            _pathProvider = new PathProvider(packageId, appPathBase, nuGetCachePath);

            _logger.Debug("Package id:      " + packageId);
            _logger.Debug("Current version: " + (currentVersion != null ? currentVersion .ToString() : "<none>"));
            try {
                _logger.Debug("Package source:  " + packageRepository.Source);
            } catch (System.Net.WebException) {
                _logger.Warn("Package source:  accessing packageRepository.Source failed, assuming the nuget feed isn't available");
            }
            _logger.Debug("Target folder:   " + _pathProvider.AppPathBase);
            _logger.Debug("Cache folder:    " + _pathProvider.NuGetCachePath);

            Environment.SetEnvironmentVariable("NuGetCachePath", _pathProvider.NuGetCachePath);

            var progressProvider = _packageRepository as IProgressProvider;
            if (progressProvider != null) {
                progressProvider.ProgressAvailable += ProgressProviderOnProgressAvailable;
            }

            var httpClientEvents = _packageRepository as IHttpClientEvents;
            if (httpClientEvents != null) {
                httpClientEvents.SendingRequest += (sender, args) => _logger.Info("requesting {0}", args.Request.RequestUri);
            }
        }

        public string AppPathBase {
            get { return _pathProvider.AppPathBase; }
        }

        private int _lastPercentComplete = -1;

        private void ProgressProviderOnProgressAvailable(object sender, ProgressEventArgs args) {
            if (_lastPercentComplete == args.PercentComplete) {
                return;
            }
            _lastPercentComplete = args.PercentComplete;

            _logger.Info("{0}: {1}", args.Operation, args.PercentComplete);

            var handler = ProgressAvailable;
            if (handler != null) {
                handler(this, args);
            }
        }

        private event EventHandler<ProgressEventArgs> ProgressAvailable;

        public UpdateInfo CheckForUpdate(bool includePrereleases = false) {
            var versionSpec = _currentVersion != null
                                    ? new VersionSpec { MinVersion = _currentVersion, IsMinInclusive = false }
                                    : null;
            var packages = _packageRepository.FindPackages(_packageId, versionSpec, includePrereleases, true).ToArray();

            _availableUpdates = packages.Select(p => new UpdateInfo(p)).ToList();
            RaiseAvailableUpdatesChanged();

            // IsAbsoluteLatestVersion and IsLatestVersion are not what I expected them to be...
            //_latestPackage = _availableUpdates.SingleOrDefault(
            //    p => includePrereleases ? p.Package.IsAbsoluteLatestVersion : p.Package.IsLatestVersion)
            //                 ?? _availableUpdates.OrderByDescending(p => p.Version).FirstOrDefault();
            _latestPackage = _availableUpdates.OrderByDescending(p => p.Version).FirstOrDefault();

            if (_latestPackage == null) {
                _logger.Debug("No updates found");
            } else {
                _logger.Debug("Found {0} updates, latest is {1}", _availableUpdates.Count, _latestPackage.Version);
            }

            return _latestPackage;
        }

        public void DownloadPackage(UpdateInfo updateInfo, Action<int> callbackPercentCompleted = null) {
            var onProgressAvailable = callbackPercentCompleted != null ? (sender, args) => callbackPercentCompleted(args.PercentComplete) : (EventHandler<ProgressEventArgs>)null;
            if (onProgressAvailable != null) {
                ProgressAvailable += onProgressAvailable;
            }

            // this line forces NuGet to download the package
            updateInfo.Package.HasProjectContent();

            if (onProgressAvailable != null) {
                ProgressAvailable -= onProgressAvailable;
            }
        }

        private static IEnumerable<Tuple<IPackageFile, string>> GetFiles(IPackage package) {
            if (package.GetLibFiles().IsEmpty() && package.GetContentFiles().IsEmpty()) {
                foreach (var packageFile in package.GetFiles()) {
                    yield return new Tuple<IPackageFile, string>(packageFile, packageFile.Path);
                }
            } else {
                // TODO: net40 is hard-coded. Some day more frameworks should be supported
                foreach (var packageFile in package.GetLibFiles()) {
                    yield return new Tuple<IPackageFile, string>(packageFile, packageFile.Path.Substring(@"lib\net40\".Length));
                }
                foreach (var packageFile in package.GetContentFiles()) {
                    yield return new Tuple<IPackageFile, string>(packageFile, packageFile.Path.Substring(@"content\".Length));
                }
            }
        }

        public void ApplyUpdate(UpdateInfo updateInfo) {
            var targetFolder = _pathProvider.GetAppPath(updateInfo);
            _logger.Info("Target path is " + targetFolder);

            if (Directory.Exists(targetFolder)) {
                Directory.Delete(targetFolder, true);
            }
            Directory.CreateDirectory(targetFolder);

            foreach (var contentFile in GetFiles(updateInfo.Package)) {
                _logger.Info("Extracting " + contentFile.Item1.Path);
                var targetPath = Path.Combine(targetFolder, contentFile.Item2);
                var targetDir = Path.GetDirectoryName(targetPath);
                if (targetDir != null && !Directory.Exists(targetDir)) {
                    Directory.CreateDirectory(targetDir);
                }
                using (var input = contentFile.Item1.GetStream()) {
                    using (var output = File.Create(targetPath)) {
                        input.CopyTo(output);
                    }
                }
            }
        }

        public void CreateShortcuts(UpdateInfo updateInfo) {
            var appPath = _pathProvider.GetAppPath(updateInfo);
            var shortcutHandler = new ShortcutHandler();
            foreach (var shortcut in shortcutHandler.GetShortcuts(appPath)) {
                shortcutHandler.CreateShortcut(shortcut, appPath);
            }
        }

        public void UpdateUninstallInformation(UpdateInfo updateInfo) {
            var installPath = Path.Combine(_pathProvider.AppPathBase, "install.exe");
            var estimatedSize = (
                GetFolderSize(_pathProvider.GetAppPath(updateInfo))
                + GetFolderSize(_pathProvider.NuGetCachePath)
                + new FileInfo(installPath).Length) >> 10;
            using (
                var keyUninstall = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", true)) {
                Debug.Assert(keyUninstall != null, "keyUninstall != null");
                using (var key = keyUninstall.OpenSubKey(_packageId, true) ?? keyUninstall.CreateSubKey(_packageId)) {
                    Debug.Assert(key != null, "key != null");
                    if (updateInfo.Package.IconUrl != null) {
                        key.SetValue("DisplayIcon", updateInfo.Package.IconUrl);
                    } else {
                        key.SetValue("DisplayIcon", installPath + ",0");
                    }
                    key.SetValue("DisplayName", updateInfo.Package.Id, RegistryValueKind.String);
                    key.SetValue("DisplayVersion", updateInfo.Version.ToString(), RegistryValueKind.String);
                    key.SetValue("InstallDate", DateTimeOffset.Now.ToString("yyyyMMdd"));
                    key.SetValue("UninstallString", installPath + " /uninstall", RegistryValueKind.ExpandString);
                    key.SetValue("InstallLocation", _pathProvider.AppPathBase, RegistryValueKind.ExpandString);
                    key.SetValue("Publisher", String.Join(", ", updateInfo.Package.Authors), RegistryValueKind.String);
                    key.SetValue("VersionMajor", updateInfo.Version.Version.Major, RegistryValueKind.DWord);
                    key.SetValue("VersionMinor", updateInfo.Version.Version.Minor, RegistryValueKind.DWord);
                    key.SetValue("EstimatedSize", estimatedSize, RegistryValueKind.DWord);
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                }
            }
        }

        private static long GetFolderSize(string path) {
            if (!Directory.Exists(path)) {
                return 0;
            }

            return Directory.EnumerateDirectories(path).Select(GetFolderSize)
                .Concat(Directory.EnumerateFiles(path).Select(filename => new FileInfo(filename).Length))
                .Sum();
        }

        public IEnumerable<UpdateInfo> AvailableUpdates {
            get { return _availableUpdates; }
        }

        public event EventHandler AvailableUpdatesChanged;

        private void RaiseAvailableUpdatesChanged() {
            var handler = AvailableUpdatesChanged;
            if (handler != null) {
                handler(this, EventArgs.Empty);
            }
        }
    }
}