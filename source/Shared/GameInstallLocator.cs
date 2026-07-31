using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace RaidRescue
{
    internal static class GameInstallLocator
    {
        internal static string Find()
        {
            List<string> candidates = new List<string>();
            AddUninstallLocation(candidates, RegistryView.Registry32);
            AddUninstallLocation(candidates, RegistryView.Registry64);
            AddSteamLibraries(candidates);

            string programFilesX86 = Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFilesX86);
            string programFiles = Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFiles);
            candidates.Add(Path.Combine(
                programFilesX86, "Steam", "steamapps", "common",
                "Scrap Mechanic"));
            candidates.Add(Path.Combine(
                programFiles, "Steam", "steamapps", "common",
                "Scrap Mechanic"));

            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady)
                    continue;
                candidates.Add(Path.Combine(
                    drive.RootDirectory.FullName, "SteamLibrary",
                    "steamapps", "common", "Scrap Mechanic"));
                candidates.Add(Path.Combine(
                    drive.RootDirectory.FullName, "Steam",
                    "steamapps", "common", "Scrap Mechanic"));
            }

            HashSet<string> seen = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (string candidate in candidates)
            {
                if (String.IsNullOrEmpty(candidate))
                    continue;
                string full;
                try { full = Path.GetFullPath(candidate); }
                catch { continue; }
                if (!seen.Add(full))
                    continue;
                if (File.Exists(Path.Combine(
                    full, "Release", "ScrapMechanic.exe")) &&
                    File.Exists(Path.Combine(
                    full, "Survival", "Scripts", "game",
                    "managers", "RaidManager.lua")))
                    return full;
            }
            return null;
        }

        private static void AddUninstallLocation(
            List<string> candidates, RegistryView view)
        {
            try
            {
                using (RegistryKey machine = RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine, view))
                using (RegistryKey app = machine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 387990"))
                {
                    if (app == null)
                        return;
                    string location = app.GetValue("InstallLocation") as string;
                    if (!String.IsNullOrEmpty(location))
                        candidates.Add(location);
                }
            }
            catch { }
        }

        private static void AddSteamLibraries(List<string> candidates)
        {
            try
            {
                string steamPath = null;
                using (RegistryKey steam = Registry.CurrentUser.OpenSubKey(
                    @"Software\Valve\Steam"))
                {
                    if (steam != null)
                        steamPath = steam.GetValue("SteamPath") as string;
                }
                if (String.IsNullOrEmpty(steamPath))
                    return;
                candidates.Add(Path.Combine(
                    steamPath, "steamapps", "common", "Scrap Mechanic"));
                string libraries = Path.Combine(
                    steamPath, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(libraries))
                    return;
                string vdf = File.ReadAllText(libraries);
                MatchCollection paths = Regex.Matches(
                    vdf, "\"path\"\\s+\"([^\"]+)\"",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                foreach (Match match in paths)
                {
                    string library =
                        match.Groups[1].Value.Replace(@"\\", @"\");
                    candidates.Add(Path.Combine(
                        library, "steamapps", "common", "Scrap Mechanic"));
                }
            }
            catch { }
        }
    }
}
