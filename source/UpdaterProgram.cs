using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace RaidRescue
{
    internal static class UpdaterProgram
    {
        private const string ApplySwitch = "--apply-update";
        private const string StagePrefix = ".RaidRescue.Update.";

        private static int Main(string[] args)
        {
            string targetMain = null;
            string targetPatch = null;
            string backupMain = null;
            string backupPatch = null;
            string expectedVersion = "";
            try
            {
                if (args == null || args.Length != 9 ||
                    !String.Equals(
                        args[0], ApplySwitch, StringComparison.Ordinal))
                    return 2;
                int parentId;
                if (!Int32.TryParse(args[1], out parentId) || parentId <= 0)
                    throw new InvalidDataException(
                        "The updater received an invalid parent process.");

                string stageMain = Path.GetFullPath(args[2]);
                targetMain = Path.GetFullPath(args[3]);
                string mainDigest = NormalizeDigest(args[4]);
                string stagePatch = Path.GetFullPath(args[5]);
                targetPatch = Path.GetFullPath(args[6]);
                string patchDigest = NormalizeDigest(args[7]);
                expectedVersion = args[8] ?? "";
                Version version;
                if (!Version.TryParse(expectedVersion, out version) ||
                    !IsSha256(mainDigest) || !IsSha256(patchDigest))
                    throw new InvalidDataException(
                        "The updater received invalid verification data.");

                ValidatePaths(
                    stageMain, targetMain, stagePatch, targetPatch);
                WaitForParent(parentId);
                VerifyExecutable(
                    stageMain, mainDigest, version,
                    "Raid Rescue for Scrap Mechanic");
                VerifyExecutable(
                    stagePatch, patchDigest, version,
                    "Raid Rescue Patch Helper for Scrap Mechanic");
                CompanionSecurity.RequireMatchingSignerWhenSigned(
                    targetMain, stageMain);
                CompanionSecurity.RequireMatchingSignerWhenSigned(
                    targetMain, stagePatch);

                string data = GetUpdateDataDirectory();
                Directory.CreateDirectory(data);
                backupMain = Path.Combine(data, "previous-main.exe");
                backupPatch = Path.Combine(data, "previous-patch-helper.exe");
                File.Copy(targetMain, backupMain, true);
                File.Copy(targetPatch, backupPatch, true);
                string oldMainDigest = ComputeSha256(targetMain);
                string oldPatchDigest = ComputeSha256(targetPatch);
                VerifyCopy(backupMain, oldMainDigest);
                VerifyCopy(backupPatch, oldPatchDigest);

                Replace(stagePatch, targetPatch);
                VerifyCopy(targetPatch, patchDigest);
                Replace(stageMain, targetMain);
                VerifyCopy(targetMain, mainDigest);

                WriteStatus(true, expectedVersion, "");
                try
                {
                    StartMain(targetMain);
                }
                catch
                {
                    Restore(
                        backupMain, targetMain, oldMainDigest,
                        backupPatch, targetPatch, oldPatchDigest);
                    throw;
                }
                return 0;
            }
            catch (Exception exception)
            {
                string message =
                    "The automatic update was rolled back. " +
                    exception.Message;
                try
                {
                    if (!String.IsNullOrEmpty(backupMain) &&
                        !String.IsNullOrEmpty(backupPatch) &&
                        File.Exists(backupMain) &&
                        File.Exists(backupPatch))
                    {
                        Restore(
                            backupMain, targetMain,
                            ComputeSha256(backupMain),
                            backupPatch, targetPatch,
                            ComputeSha256(backupPatch));
                    }
                }
                catch { }
                WriteStatus(false, expectedVersion, message);
                try
                {
                    if (!String.IsNullOrEmpty(targetMain) &&
                        File.Exists(targetMain))
                        StartMain(targetMain);
                }
                catch { }
                return 1;
            }
        }

        private static void ValidatePaths(
            string stageMain,
            string targetMain,
            string stagePatch,
            string targetPatch)
        {
            string updater = Path.GetFullPath(
                System.Reflection.Assembly.GetExecutingAssembly().Location);
            string directory = Path.GetDirectoryName(updater);
            if (!String.Equals(
                Path.GetFileName(updater), "RaidRescue.Updater.exe",
                StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(
                    Path.GetDirectoryName(targetMain), directory,
                    StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(
                    Path.GetDirectoryName(targetPatch), directory,
                    StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(
                    Path.GetDirectoryName(stageMain), directory,
                    StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(
                    Path.GetDirectoryName(stagePatch), directory,
                    StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(
                    Path.GetFileName(targetMain), "RaidRescue.exe",
                    StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(
                    Path.GetFileName(targetPatch),
                    "RaidRescue.PatchHelper.exe",
                    StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(stageMain).StartsWith(
                    StagePrefix + "Main.",
                    StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(stagePatch).StartsWith(
                    StagePrefix + "Patch.",
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "The updater rejected paths outside its release bundle.");
            if (!File.Exists(stageMain) || !File.Exists(stagePatch) ||
                !File.Exists(targetMain) || !File.Exists(targetPatch))
                throw new FileNotFoundException(
                    "An update component disappeared before installation.");
        }

        private static void WaitForParent(int parentId)
        {
            try
            {
                using (Process parent = Process.GetProcessById(parentId))
                {
                    string expected = Path.Combine(
                        Path.GetDirectoryName(
                            System.Reflection.Assembly
                                .GetExecutingAssembly().Location),
                        "RaidRescue.exe");
                    if (!String.Equals(
                        Path.GetFullPath(parent.MainModule.FileName),
                        Path.GetFullPath(expected),
                        StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException(
                            "The updater rejected an unexpected parent program.");
                    if (!parent.WaitForExit(60000))
                        throw new TimeoutException(
                            "Raid Rescue did not close in time.");
                }
            }
            catch (ArgumentException)
            {
                // The verified parent already exited.
            }
        }

        private static void VerifyExecutable(
            string path,
            string digest,
            Version expected,
            string product)
        {
            FileInfo file = new FileInfo(path);
            if (!file.Exists || file.Length < 50000 ||
                file.Length > 12 * 1024 * 1024)
                throw new InvalidDataException(
                    "A staged executable has an unexpected size.");
            VerifyCopy(path, digest);
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
            Version actual;
            if (!Version.TryParse(info.FileVersion, out actual) ||
                actual.Major != expected.Major ||
                actual.Minor != expected.Minor ||
                actual.Build != expected.Build ||
                !String.Equals(
                    info.ProductName, product, StringComparison.Ordinal))
                throw new InvalidDataException(
                    "A staged executable failed identity verification.");
        }

        private static void Restore(
            string backupMain,
            string targetMain,
            string mainDigest,
            string backupPatch,
            string targetPatch,
            string patchDigest)
        {
            File.Copy(backupPatch, targetPatch, true);
            File.Copy(backupMain, targetMain, true);
            VerifyCopy(targetPatch, patchDigest);
            VerifyCopy(targetMain, mainDigest);
        }

        private static void Replace(string stage, string target)
        {
            try
            {
                File.Replace(stage, target, null, true);
                return;
            }
            catch (PlatformNotSupportedException) { }
            catch (IOException) { }

            string displaced = target + ".previous-" +
                Guid.NewGuid().ToString("N") + ".tmp";
            File.Move(target, displaced);
            try
            {
                File.Move(stage, target);
                TryDelete(displaced);
            }
            catch
            {
                if (!File.Exists(target) && File.Exists(displaced))
                    File.Move(displaced, target);
                throw;
            }
        }

        private static void VerifyCopy(string path, string digest)
        {
            if (!String.Equals(
                ComputeSha256(path), digest,
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "An update component failed SHA-256 verification.");
        }

        private static string ComputeSha256(string path)
        {
            using (FileStream stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (SHA256 algorithm = SHA256.Create())
            {
                StringBuilder text = new StringBuilder(64);
                foreach (byte value in algorithm.ComputeHash(stream))
                    text.Append(value.ToString("X2"));
                return text.ToString();
            }
        }

        private static string NormalizeDigest(string value)
        {
            return (value ?? "").Trim().ToUpperInvariant();
        }

        private static bool IsSha256(string value)
        {
            if (String.IsNullOrEmpty(value) || value.Length != 64)
                return false;
            foreach (char character in value)
            {
                if (!Uri.IsHexDigit(character))
                    return false;
            }
            return true;
        }

        private static void StartMain(string path)
        {
            Process process = Process.Start(new ProcessStartInfo
            {
                FileName = path,
                WorkingDirectory = Path.GetDirectoryName(path),
                UseShellExecute = true
            });
            if (process == null)
                throw new InvalidOperationException(
                    "Windows could not reopen Raid Rescue.");
        }

        private static void WriteStatus(
            bool success, string version, string error)
        {
            try
            {
                string directory = GetUpdateDataDirectory();
                Directory.CreateDirectory(directory);
                File.WriteAllText(
                    Path.Combine(directory, "update-status.ini"),
                    "Success=" + (success ? "1" : "0") + Environment.NewLine +
                    "Version=" + (version ?? "") + Environment.NewLine +
                    "Error=" + Convert.ToBase64String(
                        Encoding.UTF8.GetBytes(error ?? "")) +
                    Environment.NewLine,
                    new UTF8Encoding(false));
            }
            catch { }
        }

        private static string GetUpdateDataDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "Raid Rescue", "Updates");
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!String.IsNullOrEmpty(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch { }
        }
    }
}
