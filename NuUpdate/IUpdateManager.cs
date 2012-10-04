using System;

namespace NuUpdate {
    public interface IUpdateManager {
        UpdateInfo CheckForUpdate(bool includePrereleases = false);

        UpdateInfo DownloadPackage(UpdateInfo updateInfo, Action<int> callbackPercentCompleted = null);

        UpdateInfo ApplyUpdate(UpdateInfo updateInfo);

        UpdateInfo CreateShortcuts(UpdateInfo updateInfo);

        UpdateInfo UpdateUninstallInformation(UpdateInfo updateInfo);

        string AppPathBase { get; }
    }
}