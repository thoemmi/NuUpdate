using System;
using System.IO;
using NuUpdate.Installer.Interop;

namespace NuUpdate.Installer.Preparer {
    internal class Program {
        private static int Main(string[] args) {
            if (args.Length != 3) {
                Console.WriteLine("Usage:");
                Console.WriteLine("NuUpdate.Installer.Preparer.exe <installer.exe> <package id> <package source>");
                return 1;
            }

            var installerPath = args[0];
            var packageId = args[1];
            var packageSource = args[2];

            if (!File.Exists(installerPath)) {
                Console.WriteLine("installer cannot be found at " + installerPath);
                return 2;
            }

            Console.WriteLine("installer:      " + installerPath);
            Console.WriteLine("package id:     " + packageId);
            Console.WriteLine("package source: " + packageSource);

            Win32ResourceManager.UpdateRessource(installerPath, 1711, packageId + "|" + packageSource);

            Console.WriteLine("Patched successfully.");

            return 0;
        }
    }
}