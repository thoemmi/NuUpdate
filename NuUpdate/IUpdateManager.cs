using System;
using System.Threading.Tasks;

namespace NuUpdate {
    public interface IUpdateManager {
        Task<UpdateInfo> CheckForUpdate(Version currentVersion = null, bool includePrereleases = false);

        Task<UpdateInfo> DownloadPackage(UpdateInfo updateInfo, Action<int> callbackPercentCompleted = null);

        Task ApplyUpdate(UpdateInfo updateInfo);
    }
}