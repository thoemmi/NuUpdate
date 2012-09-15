using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using NLog;
using NuGet;
using NuUpdate.Interop;

namespace NuUpdate {
    public class UpdateManager : IUpdateManager {
        private readonly string _packageId;
        private readonly Version _currentVersion;

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly IPackageRepository _packageRepository;
        private readonly string _appPathBase;
        private readonly string _nuGetCachePath;

        private UpdateInfo _latestPackage;
        private List<UpdateInfo> _availableUpdates = new List<UpdateInfo>();

        public UpdateManager(string packageId, Version currentVersion, string packageSource, string appPathBase = null)
            : this(packageId, currentVersion, packageSource, appPathBase, null) {
        }

        public UpdateManager(string packageId, Version currentVersion, IPackageRepository packageRepository, string appPathBase = null)
            : this(packageId, currentVersion, packageRepository, appPathBase, null) {
        }

        internal UpdateManager(string packageId, Version currentVersion, string packageSource, string appPathBase, string nuGetCachePath)
            : this(packageId, currentVersion, PackageRepositoryFactory.Default.CreateRepository(packageSource), appPathBase, nuGetCachePath) {
        }

        internal UpdateManager(string packageId, Version currentVersion, IPackageRepository packageRepository, string appPathBase, string nuGetCachePath) {
            _packageId = packageId;
            _currentVersion = currentVersion;
            _packageRepository = packageRepository;
            _appPathBase = appPathBase ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _packageId);
            _nuGetCachePath = nuGetCachePath ?? Path.Combine(_appPathBase, "packages");

            _logger.Debug("Package id:      " + packageId);
            _logger.Debug("Current version: " + (currentVersion != null ? currentVersion .ToString() : "<none>"));
            _logger.Debug("Package source:  " + packageRepository.Source);
            _logger.Debug("Target folder:   " + _appPathBase);

            Environment.SetEnvironmentVariable("NuGetCachePath", _nuGetCachePath);

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
            get { return _appPathBase; }
        }

        private void ProgressProviderOnProgressAvailable(object sender, ProgressEventArgs args) {
            _logger.Info("{0}: {1}", args.Operation, args.PercentComplete);

            var handler = ProgressAvailable;
            if (handler != null) {
                handler(this, args);
            }
        }

        private event EventHandler<ProgressEventArgs> ProgressAvailable;

        public Task<UpdateInfo> CheckForUpdate(bool includePrereleases = false) {
            return Task.Factory.StartNew(() => {
                var versionSpec = _currentVersion != null
                                      ? new VersionSpec { MinVersion = new SemanticVersion(_currentVersion), IsMinInclusive = false }
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
            });
        }

        public Task<UpdateInfo> DownloadPackage(UpdateInfo updateInfo, Action<int> callbackPercentCompleted = null) {
            return Task.Factory.StartNew(() => {
                var onProgressAvailable = callbackPercentCompleted != null ? (sender, args) => callbackPercentCompleted(args.PercentComplete) : (EventHandler<ProgressEventArgs>)null;
                if (onProgressAvailable != null) {
                    ProgressAvailable += onProgressAvailable;
                }

                // this line forces NuGet to download the package
                updateInfo.Package.HasProjectContent();

                if (onProgressAvailable != null) {
                    ProgressAvailable -= onProgressAvailable;
                }

                return updateInfo;
            });
        }

        public Task<UpdateInfo> ApplyUpdate(UpdateInfo updateInfo) {
            return Task.Factory.StartNew(() => {
                var targetFolder = GetAppPath(updateInfo);
                _logger.Info("Target path is " + targetFolder);

                if (Directory.Exists(targetFolder)) {
                    Directory.Delete(targetFolder, true);
                }
                Directory.CreateDirectory(targetFolder);

                foreach (var contentFile in updateInfo.Package.GetLibFiles()) {
                    _logger.Info("Extracting " + contentFile.Path);
                    // TODO: net40 is hard-coded. Some day more frameworks should be supported
                    var targetPath = Path.Combine(targetFolder, contentFile.Path.Substring(@"lib\net40\".Length));
                    var targetDir = Path.GetDirectoryName(targetPath);
                    if (targetDir != null && !Directory.Exists(targetDir)) {
                        Directory.CreateDirectory(targetDir);
                    }
                    using (var input = contentFile.GetStream()) {
                        using (var output = File.Create(targetPath)) {
                            input.CopyTo(output);
                        }
                    }
                }
                return updateInfo;
            });
        }

        public Task<UpdateInfo> CreateShortcuts(UpdateInfo updateInfo) {
            return Task.Factory.StartNew(() => {
                var path = GetAppPath(updateInfo);
                foreach (var exePath in Directory.EnumerateFiles(path, "*.exe")) {
                    new ShellLink {
                        Target = exePath
                    }.Save(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), Path.GetFileNameWithoutExtension(exePath) + ".lnk"));
                }
                
                return updateInfo;
            });
        }

        public Task<UpdateInfo> UpdateUninstallInformation(UpdateInfo updateInfo) {
            return Task.Factory.StartNew(() => {
                var installPath = Path.Combine(_appPathBase, "install.exe");
                var estimatedSize = (
                    GetFolderSize(GetAppPath(updateInfo)) 
                    + GetFolderSize(_nuGetCachePath)
                    + new FileInfo(installPath).Length) >> 10;
                using (
                    var keyUninstall =
                        Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", true)) {
                    using (var key = keyUninstall.OpenSubKey(_packageId, true) ?? keyUninstall.CreateSubKey(_packageId)) {
                        if (updateInfo.Package.IconUrl != null) {
                            key.SetValue("DisplayIcon", updateInfo.Package.IconUrl);
                        } else {
                            key.SetValue("DisplayIcon", installPath + ",0");
                        }
                        key.SetValue("DisplayName", updateInfo.Package.Id, RegistryValueKind.String);
                        key.SetValue("DisplayVersion", updateInfo.Version.ToString(), RegistryValueKind.String);
                        key.SetValue("InstallDate", DateTimeOffset.Now.ToString("yyyyMMdd"));
                        key.SetValue("UninstallString", installPath + " /uninstall", RegistryValueKind.ExpandString);
                        key.SetValue("InstallLocation", _appPathBase, RegistryValueKind.ExpandString);
                        key.SetValue("Publisher", String.Join(", ", updateInfo.Package.Authors), RegistryValueKind.String);
                        key.SetValue("VersionMajor", updateInfo.Version.Version.Major, RegistryValueKind.DWord);
                        key.SetValue("VersionMinor", updateInfo.Version.Version.Minor, RegistryValueKind.DWord);
                        key.SetValue("EstimatedSize", estimatedSize, RegistryValueKind.DWord);
                        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    }
                }
                return updateInfo;
            });
        }

        private long GetFolderSize(string path) {
            return Directory.EnumerateDirectories(path).Select(GetFolderSize)
                .Concat(Directory.EnumerateFiles(path).Select(filename => new FileInfo(filename).Length))
                .Sum();
        }

        private string GetAppPath(UpdateInfo updateInfo) {
            return Path.Combine(
                _appPathBase,
                "app-" + updateInfo.Version
                );
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