using System;

namespace NuUpdate {
    public interface IUpdateManager {
        UpdateInfo CheckForUpdate(bool includePrereleases = false);

        void DownloadPackage(UpdateInfo updateInfo, Action<int> callbackPercentCompleted = null);

        void ApplyUpdate(UpdateInfo updateInfo);

        void CreateShortcuts(UpdateInfo updateInfo);

        void UpdateUninstallInformation(UpdateInfo updateInfo);

        string AppPathBase { get; }
    }
}