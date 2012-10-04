using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shell;
using NLog;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;
using NLog.Targets.Wrappers;
using NuUpdate.Installer.Interop;

namespace NuUpdate.Installer {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly IUpdateManager _updateManager;
        private readonly string _packageId;
        private readonly string _packageSource;

        public MainWindow() {
            InitializeComponent();

            var logfile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "installer.log");
            ConfigureLogging(logfile);

            var s = Win32ResourceManager.ReadRessource<string>(GetType().Assembly.Location, 1711);
            if (String.IsNullOrEmpty(s)) {
                _logger.Error("Win32 resource specifying package id and source not found.");
                MessageBox.Show("This is not a valid installer. See\n" + logfile + "\nfor details.", "Installer", MessageBoxButton.OK,
                                MessageBoxImage.Error);
                Close();
                return;
            }
            _logger.Debug("Read \"{0}\" from Win32 resource");

            var parts = s.Split('|');
            if (parts.Length != 2) {
                _logger.Error("Win32 resource does not specify package id and source.");
                MessageBox.Show("This is not a valid installer. See\n" + logfile + "\nfor details.", "Installer", MessageBoxButton.OK,
                                MessageBoxImage.Error);
                Close();
                return;
            }
            if (!IsUriValid(parts[1])) {
                _logger.Error("The package source URI \"{0}\" is invalid", parts[1]);
                MessageBox.Show("This is not a valid installer. See\n" + logfile + "\nfor details.", "Installer", MessageBoxButton.OK,
                                MessageBoxImage.Error);
                Close();
                return;
            }

            _packageId = parts[0];
            _packageSource = parts[1];

            var oldLogFile = logfile;
            logfile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                _packageId,
                "install.log");

            _logger.Info("Switching to application specific log file " + logfile);

            ConfigureLogging(logfile);
            try {
                File.Delete(oldLogFile);
            } catch (Exception ex) {
                _logger.WarnException("Deleting temp log failed", ex);
            }
            _logger.Info("Installer started");

            _logger.Info("Detected package id:     " + _packageId);
            _logger.Info("Detected package source: " + _packageSource);

            _updateManager = new UpdateManager(_packageId, null, _packageSource);

            Title = _packageId + " Installer";
            lblProgress.Text = _packageId + " will be installed once you press Start.";
        }

        private static bool IsUriValid(string url) {
            Uri dummyUri;
            return Uri.TryCreate(url, UriKind.Absolute, out dummyUri);
        }

        private static void ConfigureLogging(string logFileName) {
            var fileTarget = new FileTarget {
                FileName = logFileName,
                Header = new SimpleLayout("${longdate} ----------------------------------------${newline}${longdate} - Process ${processname:fullName=true}"),
                Layout = new SimpleLayout("${longdate} - ${message} ${exception:format=tostring}")
            };
            var asyncTarget = new AsyncTargetWrapper(fileTarget);

            var config = new LoggingConfiguration();
            config.AddTarget("file", asyncTarget);
            var rule1 = new LoggingRule("*", LogLevel.Debug, asyncTarget);
            config.LoggingRules.Add(rule1);

            if (Debugger.IsAttached) {
                var debuggerTarget = new DebuggerTarget {
                    Layout = new SimpleLayout("${logger}: ${level:uppercase=true} ${message}${onexception:inner=${newline}${exception:format=tostring:maxInnerExceptionLevel=10}}")
                };
                config.AddTarget("debugger", debuggerTarget);
                var rule2 = new LoggingRule("*", LogLevel.Debug, debuggerTarget);
                config.LoggingRules.Add(rule2);
            }

            LogManager.Configuration = config;
        }

        private void BtnStartClick1(object sender, RoutedEventArgs e) {
            btnStart.IsEnabled = false;
            progressBar.IsIndeterminate = true;
            lblProgress.Text = "Checking for latest package of " + _packageId;

            _logger.Info("Started checking for updates");
            Task.Factory
                .StartNew(() => _updateManager.CheckForUpdate())
                .ContinueWith(OnCheckForUpdateCompleted, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void OnCheckForUpdateCompleted(Task<UpdateInfo> task) {
            progressBar.IsIndeterminate = false;
            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;

            var updateInfo = task.Result;
            lblProgress.Text = "Downloading version " + updateInfo.Version + " of " + _packageId;
            _logger.Info("Started downloading package version " + updateInfo.Version);
            Task.Factory.StartNew(() => _updateManager.DownloadPackage(updateInfo, percentCompleted => Dispatcher.BeginInvoke((Action) (() => {
                progressBar.Value = percentCompleted;
                TaskbarItemInfo.ProgressValue = percentCompleted/progressBar.Maximum;
            })))).ContinueWith(OnDownloadPackageCompleted, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void OnDownloadPackageCompleted(Task<UpdateInfo> task) {
            progressBar.Value = 100;
            TaskbarItemInfo.ProgressValue = 100/progressBar.Maximum;

            var updateInfo = task.Result;
            lblProgress.Text = "Installing version " + updateInfo.Version + " of " + _packageId;
            _logger.Info("Applying version " + updateInfo.Version);
            Task.Factory
                .StartNew(() => _updateManager.ApplyUpdate(updateInfo))
                .ContinueWith(OnApplyUpdateCompleted, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void OnApplyUpdateCompleted(Task<UpdateInfo> task) {
            progressBar.Value = 105;
            TaskbarItemInfo.ProgressValue = progressBar.Value / progressBar.Maximum;

            var setupPath = Path.GetFullPath(Path.Combine(_updateManager.AppPathBase, "install.exe"));
            File.Copy(GetType().Assembly.Location, setupPath, true);

            var updateInfo = task.Result;

            Task.Factory
                .StartNew(()=> _updateManager.CreateShortcuts(updateInfo))
                .ContinueWith(t => _updateManager.UpdateUninstallInformation(updateInfo))
                .ContinueWith(UpdateUninstallInformation, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void UpdateUninstallInformation(Task<UpdateInfo> updateInfo) {
            progressBar.Value = progressBar.Maximum;
            TaskbarItemInfo.ProgressValue = 1.0;
            lblProgress.Text = "Installed successfully";
            btnStart.Content = "Close";
            btnStart.IsEnabled = true;
        }
    }
}