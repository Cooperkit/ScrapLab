using System;
using System.IO;

namespace RaidRescue
{
    internal static class ProductPaths
    {
        internal const string ProductName = "ScrapLab";
        internal const string LegacyProductName = "Raid Rescue";
        private const string MigrationMarker =
            "migration-from-raid-rescue-v1.complete";
        private static readonly object MigrationSync = new object();
        private static bool migrationComplete;
        internal static string LocalDataRootOverride = null;
        internal static string LegacyLocalDataRootOverride = null;

        internal static string LocalDataRoot
        {
            get
            {
                if (!String.IsNullOrEmpty(LocalDataRootOverride))
                    return Path.GetFullPath(LocalDataRootOverride);
                return Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    ProductName);
            }
        }

        internal static string LegacyLocalDataRoot
        {
            get
            {
                if (!String.IsNullOrEmpty(LegacyLocalDataRootOverride))
                    return Path.GetFullPath(LegacyLocalDataRootOverride);
                return Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    LegacyProductName);
            }
        }

        internal static string LocalDataPath(params string[] parts)
        {
            string path = LocalDataRoot;
            if (parts != null)
            {
                foreach (string part in parts)
                    path = Path.Combine(path, part);
            }
            return path;
        }

        internal static void EnsureLegacyDataMigrated()
        {
            lock (MigrationSync)
            {
                if (migrationComplete)
                    return;

                string currentRoot = LocalDataRoot;
                Directory.CreateDirectory(currentRoot);
                string marker = Path.Combine(
                    currentRoot, MigrationMarker);
                if (File.Exists(marker))
                {
                    migrationComplete = true;
                    return;
                }

                string legacyRoot = LegacyLocalDataRoot;
                if (Directory.Exists(legacyRoot))
                {
                    CopyFileIfMissing(
                        Path.Combine(legacyRoot, "preferences.ini"),
                        Path.Combine(currentRoot, "preferences.ini"));
                    CopyFileIfMissing(
                        Path.Combine(legacyRoot, "secret-mods.ini"),
                        Path.Combine(currentRoot, "secret-mods.ini"));
                    CopyDirectoryIfMissing(
                        Path.Combine(legacyRoot, "Patch State"),
                        Path.Combine(currentRoot, "Patch State"));
                    CopyDirectoryIfMissing(
                        Path.Combine(legacyRoot, "Game Backups"),
                        Path.Combine(currentRoot, "Game Backups"));
                }

                File.WriteAllText(
                    marker,
                    "ScrapLab copied compatible Raid Rescue settings, " +
                    "patch receipts, and backups without deleting the originals." +
                    Environment.NewLine);
                migrationComplete = true;
            }
        }

        private static void CopyFileIfMissing(
            string source, string destination)
        {
            if (!File.Exists(source) || File.Exists(destination))
                return;
            string directory = Path.GetDirectoryName(destination);
            if (!String.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.Copy(source, destination, false);
        }

        private static void CopyDirectoryIfMissing(
            string source, string destination)
        {
            if (!Directory.Exists(source))
                return;
            DirectoryInfo sourceDirectory =
                new DirectoryInfo(source);
            if ((sourceDirectory.Attributes &
                    FileAttributes.ReparsePoint) != 0)
                return;

            Directory.CreateDirectory(destination);
            foreach (FileInfo file in sourceDirectory.GetFiles())
            {
                if ((file.Attributes &
                        FileAttributes.ReparsePoint) != 0)
                    continue;
                CopyFileIfMissing(
                    file.FullName,
                    Path.Combine(destination, file.Name));
            }
            foreach (DirectoryInfo directory in
                sourceDirectory.GetDirectories())
            {
                if ((directory.Attributes &
                        FileAttributes.ReparsePoint) != 0)
                    continue;
                CopyDirectoryIfMissing(
                    directory.FullName,
                    Path.Combine(destination, directory.Name));
            }
        }
    }
}
