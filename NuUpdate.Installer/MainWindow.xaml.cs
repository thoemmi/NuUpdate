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

namespace NuUpdate.Installer {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly IUpdateManager _updateManager;
        private const string PACKAGE_ID = "DemoApp";
        private const string PACKAGE_SOURCE = "http://localhost:8084/nuget/";

        public MainWindow() {
            InitializeComponent();
            ConfigureLogging(PACKAGE_ID);
            _updateManager = new UpdateManager(PACKAGE_ID, null, PACKAGE_SOURCE);

            Title = PACKAGE_ID + " Installer";
            lblProgress.Text = PACKAGE_ID + " will be installed once you press Start.";
        }

        private static void ConfigureLogging(string packageName) {
            var dataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                packageName);

            var fileTarget2 = new FileTarget {
                FileName = Path.Combine(dataFolder, "install.log"),
                Layout = new SimpleLayout("${longdate} - ${message} ${exception:format=tostring}")
            };
            var fileTarget = new AsyncTargetWrapper(fileTarget2);

            var config = new LoggingConfiguration();
            config.AddTarget("file", fileTarget);
            var rule1 = new LoggingRule("*", LogLevel.Debug, fileTarget);
            config.LoggingRules.Add(rule1);

            if (Debugger.IsAttached) {
                var debuggerTarget = new DebuggerTarget();
                config.AddTarget("debugger", debuggerTarget);
                var rule2 = new LoggingRule("*", LogLevel.Debug, debuggerTarget);
                config.LoggingRules.Add(rule2);
            }

            LogManager.Configuration = config;

            PresentationTraceSources.DataBindingSource.Listeners.Add(new NLogTraceListener());

            _logger.Info("----------------------------------");
            _logger.Info("Installer {0} started", packageName);
        }


        private void BtnStartClick1(object sender, RoutedEventArgs e) {
            btnStart.IsEnabled = false;
            progressBar.IsIndeterminate = true;
            lblProgress.Text = "Checking for latest package of " + PACKAGE_ID;

            _logger.Info("Started checking for updates");
            _updateManager.CheckForUpdate().ContinueWith(OnCheckForUpdateCompleted, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void OnCheckForUpdateCompleted(Task<UpdateInfo> task) {
            progressBar.Minimum = 0;
            progressBar.Maximum = 105;
            progressBar.IsIndeterminate = false;
            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;

            var updateInfo = task.Result;
            lblProgress.Text = "Downloading version " + updateInfo.Version + " of " + PACKAGE_ID;
            _logger.Info("Started downloading package version " + updateInfo.Version);
            _updateManager.DownloadPackage(updateInfo, percentCompleted => Dispatcher.BeginInvoke((Action)(() => {
                progressBar.Value = percentCompleted;
                TaskbarItemInfo.ProgressValue = percentCompleted / progressBar.Maximum;
            }))).ContinueWith(OnDownloadPackageCompleted, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void OnDownloadPackageCompleted(Task<UpdateInfo> task) {
            progressBar.Value = 100;
            TaskbarItemInfo.ProgressValue = 100 / progressBar.Maximum;

            var updateInfo = task.Result;
            lblProgress.Text = "Installing version " + updateInfo.Version + " of " + PACKAGE_ID;
            _logger.Info("Applying version " + updateInfo.Version);
            _updateManager.ApplyUpdate(updateInfo).ContinueWith(OnApplyUpdateCompleted, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void OnApplyUpdateCompleted(Task task) {
            progressBar.Value = progressBar.Maximum;
            TaskbarItemInfo.ProgressValue = 1;

            lblProgress.Text = "Installed successfully";
            btnStart.Content = "Close";
            btnStart.IsEnabled = true;
        }
    }
}
