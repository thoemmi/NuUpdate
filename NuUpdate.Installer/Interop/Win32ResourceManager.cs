using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using NLog;

namespace NuUpdate.Installer.Interop {
    public static class Win32ResourceManager {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("Kernel32.dll", EntryPoint = "FindResourceW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr FindResource(IntPtr hModule, string pName, string pType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint SizeofResource(IntPtr hModule, IntPtr hResInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr BeginUpdateResource(string pFileName, [MarshalAs(UnmanagedType.Bool)] bool bDeleteExistingResources);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UpdateResource(IntPtr hUpdate, string lpType, string lpName, ushort wLanguage, IntPtr lpData, uint cbData);

        [DllImport("kernel32", SetLastError = true)]
        private static extern int EndUpdateResource(IntPtr hUpdate, bool fDiscard);

        /// <summary>
        /// returns null if ressource could not read
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="resourceId"></param>
        /// <param name="throwOnException"> </param>
        /// <returns></returns>
        public static byte[] ReadRessource(string fileName, int resourceId, bool throwOnException = false) {
            var library = IntPtr.Zero;
            try {
                library = LoadLibrary(fileName);
                if (library == IntPtr.Zero) {
                    throw new Exception(string.Format("Error LoadLibrary in ReadRessource File={0} ID={1}: {2}", fileName, resourceId,
                                                      new Win32Exception(Marshal.GetLastWin32Error()).Message));
                }
                var hRes = FindResource(library, resourceId.ToString(CultureInfo.InvariantCulture), "RT_RCDATA");
                var size = SizeofResource(library, hRes);
                var pt = LoadResource(library, hRes);
                if (pt == IntPtr.Zero) {
                    return null;
                }
                var ret = new byte[size];
                Marshal.Copy(pt, ret, 0, (int) size);

                return ret;
            } catch (Exception ex) {
                if (throwOnException) {
                    throw;
                }
                _logger.ErrorException(String.Format("Error ReadRessource File={0} ID={1}", fileName, resourceId), ex);
                return null;
            } finally {
                if (library != IntPtr.Zero) {
                    FreeLibrary(library);
                }
            }
        }

        public static T ReadRessource<T>(string fileName, int resourceId, bool throwOnException = false) where T : class {
            var bytes = ReadRessource(fileName, resourceId, throwOnException);
            if (bytes != null) {
                var serializer = new XmlSerializer(typeof (T));
                using (var ms = new MemoryStream(bytes)) {
                    return (T) serializer.Deserialize(ms);
                }
            } else {
                return null;
            }
        }

        public static void UpdateRessource<T>(string fileName, int resourceId, T obj) {
            var serializer = new XmlSerializer(typeof (T));
            using (var ms = new MemoryStream()) {
                serializer.Serialize(ms, obj);
                UpdateRessource(fileName, resourceId, ms.ToArray());
            }
        }

        public static void UpdateRessource(string fileName, int resourceId, byte[] data) {
            var hModule = BeginUpdateResource(fileName, false);
            if (hModule != IntPtr.Zero) {
                var pa = Marshal.AllocHGlobal(data.Length);
                Marshal.Copy(data, 0, pa, data.Length);
                var ok = UpdateResource(hModule, "RT_RCDATA", resourceId.ToString(CultureInfo.InvariantCulture), 0, pa, (uint) data.Length);
                if (!ok) {
                    throw new Exception("Writing resource failed: " + fileName);
                }
                EndUpdateResource(hModule, false);
            } else {
                var message = Marshal.GetLastWin32Error();
                _logger.Error("Could not write updated resource file " + fileName);
                throw new Win32Exception(message);
            }
        }
    }
}