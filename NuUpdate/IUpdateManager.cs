using System;
using System.Threading.Tasks;

namespace NuUpdate {
    public interface IUpdateManager {
        Task<UpdateInfo> CheckForUpdate(bool includePrereleases = false);

        Task<UpdateInfo> DownloadPackage(UpdateInfo updateInfo, Action<int> callbackPercentCompleted = null);

        Task<UpdateInfo> ApplyUpdate(UpdateInfo updateInfo);

        Task<UpdateInfo> UpdateUninstallInformation(UpdateInfo updateInfo);

        string AppPathBase { get; }
    }
}