using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NuGet;

namespace NuUpdate {
    public class UpdateManager : IUpdateManager {
        private readonly string _packageId;
        private readonly Version _currentVersion;

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly IPackageRepository _packageRepository;
        private readonly string _appPathBase;

        private UpdateInfo _latestPackage;
        private List<UpdateInfo> _availableUpdates = new List<UpdateInfo>();

        public UpdateManager(string packageId, Version currentVersion, string packageSource, string appPathBase = null)
            : this(packageId, currentVersion, PackageRepositoryFactory.Default.CreateRepository(packageSource), appPathBase) {
        }

        public UpdateManager(string packageId, Version currentVersion, IPackageRepository packageRepository, string appPathBase = null) {
            _packageId = packageId;
            _currentVersion = currentVersion;
            _packageRepository = packageRepository;
            _appPathBase = appPathBase ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _packageId); ;

            Environment.SetEnvironmentVariable("NuGetCachePath", Path.Combine(_appPathBase, "packages"));

            var progressProvider = _packageRepository as IProgressProvider;
            if (progressProvider != null) {
                progressProvider.ProgressAvailable += ProgressProviderOnProgressAvailable;
            }

            var httpClientEvents = _packageRepository as IHttpClientEvents;
            if (httpClientEvents != null) {
                httpClientEvents.SendingRequest += (sender, args) => _logger.Info("requesting {0}", args.Request.RequestUri);
            }
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

                if (_availableUpdates.Count == 0) {
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

        public Task ApplyUpdate(UpdateInfo updateInfo) {
            return Task.Factory.StartNew(() => {
                var targetFolder = GetAppPath(updateInfo);
                _logger.Info("Target path is " + targetFolder);

                if (Directory.Exists(targetFolder)) {
                    Directory.Delete(targetFolder, true);
                }
                Directory.CreateDirectory(targetFolder);

                foreach (var contentFile in updateInfo.Package.GetContentFiles()) {
                    Trace.Assert(contentFile.Path.StartsWith(@"content\", StringComparison.InvariantCultureIgnoreCase));

                    _logger.Info("Extracting " + contentFile.Path);
                    var targetPath = Path.Combine(targetFolder, contentFile.Path.Substring(@"content\".Length));
                    using (var input = contentFile.GetStream()) {
                        using (var output = File.Create(targetPath)) {
                            input.CopyTo(output);
                        }
                    }
                }
            });
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