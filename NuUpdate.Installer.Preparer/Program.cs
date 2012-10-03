using System;
using System.IO;
using NuUpdate.Installer.Interop;

namespace NuUpdate.Installer.Preparer {
    internal class Program {
        private static int Main(string[] args) {
            if (args.Length != 4) {
                Console.WriteLine("Usage:");
                Console.WriteLine("NuUpdate.Installer.Preparer.exe <installer.exe> <package id> <package source> <current package>");
                return 1;
            }

            var installerPath = args[0];
            var packageId = args[1];
            var packageSource = args[2];
            var currentPackage = args[3];

            if (!File.Exists(installerPath)) {
                Console.WriteLine("installer cannot be found at " + installerPath);
                return 2;
            }
            if (!File.Exists(currentPackage)) {
                Console.WriteLine("The current package cannot be found at " + currentPackage);
                return 2;
            }

            Console.WriteLine("installer:       " + installerPath);
            Console.WriteLine("package id:      " + packageId);
            Console.WriteLine("package source:  " + packageSource);
            Console.WriteLine("current package: " + currentPackage);

            Win32ResourceManager.UpdateRessource(installerPath, 1711, packageId + "|" + packageSource);
            var currentPackageBytes = File.ReadAllBytes(currentPackage);
            Win32ResourceManager.UpdateRessource(installerPath, 1712, currentPackageBytes);

            Console.WriteLine("Patched successfully.");

            return 0;
        }
    }
}