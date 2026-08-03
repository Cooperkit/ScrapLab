using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace RaidRescue
{
    internal static class PatchCompatibilityState
    {
        internal const string KnownClean = "KNOWN CLEAN";
        internal const string KnownInstalled = "KNOWN INSTALLED";
        internal const string CompatibleUpdate = "COMPATIBLE GAME UPDATE";
        internal const string AdaptiveInstalled = "ADAPTIVE INSTALLED";
        internal const string DefinitionUpdate = "PATCH DEFINITION UPDATE";
        internal const string PartialConflict = "PARTIAL PATCH - REPAIR REQUIRED";
        internal const string UnsupportedCode = "GAME UPDATE CHANGED REQUIRED CODE";
        internal const string OtherModification = "OTHER MODIFICATION DETECTED";
    }

    internal sealed class SteamBuildInfo
    {
        public bool Valid;
        public string BuildId;
        public string GameVersion;
        public DateTime LastUpdatedUtc;
        public string ManifestPath;
        public string Error;

        public bool IsKnownBuild
        {
            get
            {
                return String.Equals(
                    BuildId, AdaptivePatchSupport.KnownBuildId,
                    StringComparison.Ordinal) &&
                    String.Equals(
                        GameVersion, AdaptivePatchSupport.KnownGameVersion,
                        StringComparison.Ordinal);
            }
        }
    }

    internal sealed class LuaTextDocument
    {
        public string Path;
        public byte[] OriginalBytes;
        public string NormalizedText;
        public string Newline;
        public bool HasBom;
        public bool MixedNewlines;
        public string OriginalHash;

        public byte[] Render(string normalizedText)
        {
            string output = normalizedText;
            if (Newline == "\r\n")
                output = output.Replace("\n", "\r\n");
            byte[] content = new UTF8Encoding(false, true).GetBytes(output);
            if (!HasBom)
                return content;

            byte[] bytes = new byte[content.Length + 3];
            bytes[0] = 0xEF;
            bytes[1] = 0xBB;
            bytes[2] = 0xBF;
            Buffer.BlockCopy(content, 0, bytes, 3, content.Length);
            return bytes;
        }
    }

    public sealed class AdaptivePatchReceipt
    {
        public string ModKey { get; set; }
        public string DefinitionVersion { get; set; }
        public string SteamBuildId { get; set; }
        public string GameVersion { get; set; }
        public string CreatedUtc { get; set; }
        public List<AdaptivePatchReceiptFile> Files { get; set; }
    }

    public sealed class AdaptivePatchReceiptFile
    {
        public string RelativePath { get; set; }
        public string SourceHash { get; set; }
        public string OutputHash { get; set; }
        public string BackupPath { get; set; }
        public string Newline { get; set; }
        public bool HasBom { get; set; }
    }

    internal sealed class PatchBuildActivation
    {
        public string ModKey { get; set; }
        public string SteamBuildId { get; set; }
        public string GameVersion { get; set; }
        public string ActivatedUtc { get; set; }
    }

    internal static class AdaptivePatchSupport
    {
        internal static Action<string, string>
            ReplaceFileCompletedForTest = null;
        internal const string KnownBuildId = "24417028";
        internal const string KnownGameVersion = "1.0.2.870";
        internal static string PatchStateRootOverride = null;
        private static readonly TimeSpan SteamTimestampTolerance =
            TimeSpan.FromMinutes(10);

        internal static SteamBuildInfo GetSteamBuild(
            string gamePath, string gameVersion)
        {
            SteamBuildInfo info = new SteamBuildInfo
            {
                GameVersion = gameVersion ?? ""
            };
            try
            {
                string common = Directory.GetParent(
                    Path.GetFullPath(gamePath).TrimEnd(
                        Path.DirectorySeparatorChar)).FullName;
                string steamApps = Directory.GetParent(common).FullName;
                string manifest = Path.Combine(
                    steamApps, "appmanifest_387990.acf");
                info.ManifestPath = manifest;
                if (!File.Exists(manifest))
                    throw new FileNotFoundException(
                        "Steam appmanifest_387990.acf was not found.", manifest);

                string text = File.ReadAllText(
                    manifest, new UTF8Encoding(false, true));
                string appId = ReadVdfValue(text, "appid");
                string buildId = ReadVdfValue(text, "buildid");
                string lastUpdated = ReadVdfValue(text, "LastUpdated");
                long seconds;
                if (!String.Equals(appId, "387990", StringComparison.Ordinal) ||
                    String.IsNullOrEmpty(buildId) ||
                    !Int64.TryParse(lastUpdated, out seconds) ||
                    seconds <= 0)
                {
                    throw new InvalidDataException(
                        "The Scrap Mechanic Steam manifest is incomplete.");
                }

                info.BuildId = buildId;
                info.LastUpdatedUtc =
                    new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    .AddSeconds(seconds);
                info.Valid = true;
            }
            catch (Exception exception)
            {
                info.Valid = false;
                info.Error = exception.Message;
            }
            return info;
        }

        internal static bool CanAdaptCleanFiles(
            SteamBuildInfo build, IEnumerable<string> paths,
            out string reason)
        {
            if (build == null || !build.Valid)
            {
                reason = build == null || String.IsNullOrEmpty(build.Error)
                    ? "The Steam build could not be verified."
                    : build.Error;
                return false;
            }
            if (build.IsKnownBuild)
            {
                reason =
                    "This is the known Steam build, but the file hash is unknown. " +
                    "Another mod or a manual edit may have changed it.";
                return false;
            }

            DateTime latestAllowed =
                build.LastUpdatedUtc.Add(SteamTimestampTolerance);
            foreach (string path in paths)
            {
                if (!File.Exists(path))
                {
                    reason = Path.GetFileName(path) + " is missing.";
                    return false;
                }
                if (File.GetLastWriteTimeUtc(path) > latestAllowed)
                {
                    reason =
                        Path.GetFileName(path) +
                        " was modified after Steam installed this build.";
                    return false;
                }
            }

            reason =
                "The Steam build is newer or different, and every protected " +
                "code target is still compatible.";
            return true;
        }

        internal static LuaTextDocument ReadLua(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            bool hasBom = bytes.Length >= 3 &&
                bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            int offset = hasBom ? 3 : 0;
            string text = new UTF8Encoding(false, true).GetString(
                bytes, offset, bytes.Length - offset);

            int crlf = Regex.Matches(text, "\r\n").Count;
            int loneLf = Regex.Matches(text, "(?<!\r)\n").Count;
            int loneCr = Regex.Matches(text, "\r(?!\n)").Count;
            bool mixed = loneCr > 0 || (crlf > 0 && loneLf > 0);
            string newline = crlf > 0 && loneLf == 0 && loneCr == 0
                ? "\r\n"
                : "\n";

            return new LuaTextDocument
            {
                Path = path,
                OriginalBytes = bytes,
                NormalizedText = text.Replace("\r\n", "\n").Replace("\r", "\n"),
                Newline = newline,
                HasBom = hasBom,
                MixedNewlines = mixed,
                OriginalHash = Sha256(bytes)
            };
        }

        internal static void RequireAdaptiveFormat(
            LuaTextDocument document, string displayName)
        {
            if (document.MixedNewlines)
            {
                throw new InvalidDataException(
                    displayName +
                    " uses mixed newline styles. Adaptive patching was blocked " +
                    "to avoid rewriting unrelated bytes.");
            }
        }

        internal static void RequireUnique(
            string text, string value, string description)
        {
            int first = text.IndexOf(value, StringComparison.Ordinal);
            if (first < 0 ||
                text.IndexOf(
                    value, first + value.Length,
                    StringComparison.Ordinal) >= 0)
            {
                throw new InvalidDataException(
                    "The required " + description +
                    " was not found exactly once.");
            }
        }

        internal static int Count(
            string text, string value)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(
                value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }
            return count;
        }

        internal static string Sha256(byte[] value)
        {
            using (SHA256 algorithm = SHA256.Create())
                return BytesToHex(algorithm.ComputeHash(value));
        }

        internal static string Sha256(string path)
        {
            using (FileStream stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (SHA256 algorithm = SHA256.Create())
                return BytesToHex(algorithm.ComputeHash(stream));
        }

        internal static void ReplaceFile(
            string path, byte[] bytes, string operation)
        {
            string temporary = path + ".raidrescue-" + operation + "-" +
                Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, bytes);
                File.Replace(temporary, path, null, true);
                Action<string, string> completed =
                    ReplaceFileCompletedForTest;
                if (completed != null)
                    completed(path, operation);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporary))
                        File.Delete(temporary);
                }
                catch { }
            }
        }

        internal static AdaptivePatchReceipt LoadReceipt(string modKey)
        {
            try
            {
                string path = GetReceiptPath(modKey);
                if (!File.Exists(path))
                    return null;
                JavaScriptSerializer serializer =
                    new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
                return serializer.Deserialize<AdaptivePatchReceipt>(
                    File.ReadAllText(path, new UTF8Encoding(false, true)));
            }
            catch
            {
                return null;
            }
        }

        internal static void SaveReceipt(
            string modKey, AdaptivePatchReceipt receipt)
        {
            string path = GetReceiptPath(modKey);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            JavaScriptSerializer serializer =
                new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(
                    temporary, serializer.Serialize(receipt),
                    new UTF8Encoding(false));
                if (File.Exists(path))
                    File.Replace(temporary, path, null, true);
                else
                    File.Move(temporary, path);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporary))
                        File.Delete(temporary);
                }
                catch { }
            }
        }

        internal static void DeleteReceipt(string modKey)
        {
            try
            {
                string path = GetReceiptPath(modKey);
                if (File.Exists(path))
                    File.Delete(path);
                string directory = GetReceiptFileDirectory(modKey);
                if (Directory.Exists(directory))
                {
                    DirectoryInfo info = new DirectoryInfo(directory);
                    if ((info.Attributes & FileAttributes.ReparsePoint) == 0)
                        Directory.Delete(directory, true);
                }
            }
            catch { }
        }

        internal static void DiscardReceiptIfSuperseded(
            string modKey, string gamePath)
        {
            AdaptivePatchReceipt receipt = LoadReceipt(modKey);
            if (receipt == null || receipt.Files == null ||
                receipt.Files.Count == 0)
                return;

            foreach (AdaptivePatchReceiptFile file in receipt.Files)
            {
                string path = Path.Combine(gamePath, file.RelativePath);
                if (File.Exists(path) &&
                    String.Equals(
                        Sha256(path), file.OutputHash,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            // The caller invokes this only after proving that every protected
            // ScrapLab snippet is absent. At that point a Steam update has
            // superseded the installed output and the bounded active receipt
            // is no longer a valid uninstall source.
            DeleteReceipt(modKey);
            DeleteBuildActivation(modKey);
        }

        internal static bool RequiresBuildRefresh(
            string modKey, SteamBuildInfo build)
        {
            if (build == null || !build.Valid || build.IsKnownBuild)
                return false;

            PatchBuildActivation activation =
                LoadBuildActivation(modKey);
            return activation == null ||
                !String.Equals(
                    activation.SteamBuildId, build.BuildId,
                    StringComparison.Ordinal) ||
                !String.Equals(
                    activation.GameVersion ?? "",
                    build.GameVersion ?? "",
                    StringComparison.OrdinalIgnoreCase);
        }

        internal static void MarkRefreshRequired(
            GamePatchResult result, SteamBuildInfo build,
            string mode)
        {
            result.Success = true;
            result.Installed = false;
            result.AlreadyPatched = false;
            result.NeedsUpdate = true;
            if (mode != null)
                result.Mode = mode;
            FillResult(
                result, build,
                PatchCompatibilityState.CompatibleUpdate,
                true, true,
                "Steam installed a new game build. Re-enable this mod once " +
                "to refresh Scrap Mechanic's generated script cache.");
        }

        internal static void PrepareBuildRefresh(
            GamePatchResult result, string modKey,
            SteamBuildInfo build, string message)
        {
            result.Success = true;
            result.Installed = true;
            result.AlreadyPatched = false;
            result.NeedsUpdate = false;
            // The Lua is intact, but the new Steam build generated a fresh
            // official bundle. Treat cache invalidation as a patch change.
            result.FilesPatched = Math.Max(result.FilesPatched, 1);
            FillResult(
                result, build,
                PatchCompatibilityState.AdaptiveInstalled,
                !build.IsKnownBuild, true,
                "The mod code was still intact and was activated for this Steam build.");
            if (result.Changes == null)
                result.Changes = new List<string>();
            result.Changes.Add(message);
            QueueBuildActivation(result, modKey, true);
        }

        internal static void QueueBuildActivation(
            GamePatchResult result, string modKey, bool enabled)
        {
            if (result == null || String.IsNullOrWhiteSpace(modKey))
                return;
            if (result.ActivationChanges == null)
            {
                result.ActivationChanges =
                    new Dictionary<string, bool>(
                        StringComparer.OrdinalIgnoreCase);
            }
            result.ActivationChanges[modKey] = enabled;
        }

        internal static void MergeBuildActivations(
            GamePatchResult target, GamePatchResult source)
        {
            if (target == null || source == null ||
                source.ActivationChanges == null)
                return;
            foreach (KeyValuePair<string, bool> pair in
                source.ActivationChanges)
            {
                QueueBuildActivation(target, pair.Key, pair.Value);
            }
        }

        internal static void CommitBuildActivations(
            GamePatchResult result, string gamePath)
        {
            if (result == null || result.ActivationChanges == null ||
                result.ActivationChanges.Count == 0)
                return;

            SteamBuildInfo build = GetSteamBuild(
                gamePath, result.GameVersion);
            foreach (KeyValuePair<string, bool> pair in
                result.ActivationChanges)
            {
                if (!pair.Value)
                {
                    DeleteBuildActivation(pair.Key);
                    continue;
                }
                if (!build.Valid)
                {
                    throw new InvalidOperationException(
                        "The Steam build could not be recorded after resetting " +
                        "the script cache. " + build.Error);
                }
                SaveBuildActivation(
                    pair.Key,
                    new PatchBuildActivation
                    {
                        ModKey = pair.Key,
                        SteamBuildId = build.BuildId,
                        GameVersion = result.GameVersion ?? "",
                        ActivatedUtc = DateTime.UtcNow.ToString("O")
                    });
            }
        }

        internal static void DeleteBuildActivation(string modKey)
        {
            try
            {
                string path = GetActivationPath(modKey);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch { }
        }

        private static PatchBuildActivation LoadBuildActivation(
            string modKey)
        {
            try
            {
                string path = GetActivationPath(modKey);
                if (!File.Exists(path))
                    return null;
                return new JavaScriptSerializer()
                    .Deserialize<PatchBuildActivation>(
                        File.ReadAllText(
                            path, new UTF8Encoding(false, true)));
            }
            catch
            {
                return null;
            }
        }

        private static void SaveBuildActivation(
            string modKey, PatchBuildActivation activation)
        {
            string path = GetActivationPath(modKey);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary =
                path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(
                    temporary,
                    new JavaScriptSerializer().Serialize(activation),
                    new UTF8Encoding(false));
                if (File.Exists(path))
                    File.Replace(temporary, path, null, true);
                else
                    File.Move(temporary, path);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporary))
                        File.Delete(temporary);
                }
                catch { }
            }
        }

        internal static string CaptureBaseBackup(
            string modKey, string relativePath,
            string sourcePath, string expectedHash)
        {
            string directory = GetReceiptFileDirectory(modKey);
            Directory.CreateDirectory(directory);
            string identity = Sha256(
                new UTF8Encoding(false).GetBytes(
                    relativePath.ToUpperInvariant())).Substring(0, 12);
            string path = Path.Combine(
                directory,
                Path.GetFileName(relativePath) + "." + identity + ".base");
            if (File.Exists(path))
            {
                if (!String.Equals(
                    Sha256(path), expectedHash,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException(
                        "The active adaptive base backup has an unexpected checksum.");
                }
                return path;
            }
            File.Copy(sourcePath, path, false);
            if (!String.Equals(
                Sha256(path), expectedHash,
                StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(path); } catch { }
                throw new IOException(
                    "The active adaptive base backup failed checksum verification.");
            }
            return path;
        }

        internal static string CaptureVersionedBaseBackup(
            string modKey, string relativePath,
            byte[] bytes, string expectedHash)
        {
            if (bytes == null || !String.Equals(
                Sha256(bytes), expectedHash,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException(
                    "The adaptive definition-update base has an unexpected checksum.");
            }
            string directory = GetReceiptFileDirectory(modKey);
            Directory.CreateDirectory(directory);
            string identity = Sha256(
                new UTF8Encoding(false).GetBytes(
                    relativePath.ToUpperInvariant())).Substring(0, 12);
            string path = Path.Combine(
                directory,
                Path.GetFileName(relativePath) + "." +
                identity + "." +
                expectedHash.Substring(0, 12) + ".base");
            if (File.Exists(path))
            {
                if (!String.Equals(
                    Sha256(path), expectedHash,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException(
                        "The versioned adaptive base backup has an unexpected checksum.");
                }
                return path;
            }
            File.WriteAllBytes(path, bytes);
            if (!String.Equals(
                Sha256(path), expectedHash,
                StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(path); } catch { }
                throw new IOException(
                    "The versioned adaptive base backup failed checksum verification.");
            }
            return path;
        }

        internal static AdaptivePatchReceiptFile FindReceiptFile(
            AdaptivePatchReceipt receipt, string relativePath)
        {
            if (receipt == null || receipt.Files == null)
                return null;
            foreach (AdaptivePatchReceiptFile file in receipt.Files)
            {
                if (String.Equals(
                    file.RelativePath, relativePath,
                    StringComparison.OrdinalIgnoreCase))
                    return file;
            }
            return null;
        }

        internal static void FillResult(
            GamePatchResult result, SteamBuildInfo build,
            string state, bool adaptive, bool canApply, string reason)
        {
            if (result == null)
                return;
            result.CompatibilityState = state ?? "";
            result.SteamBuildId = build == null ? "" : build.BuildId ?? "";
            result.Adaptive = adaptive;
            result.CanApply = canApply;
            result.CompatibilityReason = reason ?? "";
        }

        internal static void WriteBackupManifest(
            string backupPath, string modName, string action,
            string gamePath, SteamBuildInfo build,
            string definitionVersion,
            IEnumerable<AdaptivePatchReceiptFile> files)
        {
            StringBuilder manifest = new StringBuilder();
            manifest.AppendLine("ScrapLab adaptive secret-mod backup");
            manifest.AppendLine("Mod: " + modName);
            manifest.AppendLine("Action: " + action);
            manifest.AppendLine("Game path: " + gamePath);
            manifest.AppendLine(
                "Steam build ID: " +
                (build == null ? "" : build.BuildId ?? ""));
            manifest.AppendLine(
                "Game version: " +
                (build == null ? "" : build.GameVersion ?? ""));
            manifest.AppendLine(
                "Patch definition: " + definitionVersion);
            manifest.AppendLine(
                "Created UTC: " + DateTime.UtcNow.ToString("O"));
            foreach (AdaptivePatchReceiptFile file in files)
            {
                manifest.AppendLine("");
                manifest.AppendLine("Target: " + file.RelativePath);
                manifest.AppendLine("Source SHA-256: " + file.SourceHash);
                manifest.AppendLine("Output SHA-256: " + file.OutputHash);
                manifest.AppendLine("Newlines: " + file.Newline);
                manifest.AppendLine(
                    "UTF-8 BOM: " + (file.HasBom ? "yes" : "no"));
            }
            File.WriteAllText(
                Path.Combine(backupPath, "MANIFEST.txt"),
                manifest.ToString(), new UTF8Encoding(false));
        }

        private static string GetReceiptPath(string modKey)
        {
            ValidateModKey(modKey);
            return Path.Combine(
                GetPatchStateRoot(),
                modKey + ".json");
        }

        private static string GetReceiptFileDirectory(string modKey)
        {
            ValidateModKey(modKey);
            return Path.Combine(
                GetPatchStateRoot(),
                modKey);
        }

        private static string GetActivationPath(string modKey)
        {
            ValidateModKey(modKey);
            return Path.Combine(
                GetPatchStateRoot(),
                modKey + ".activation.json");
        }

        private static string GetPatchStateRoot()
        {
            if (!String.IsNullOrEmpty(PatchStateRootOverride))
                return Path.GetFullPath(PatchStateRootOverride);
            return ProductPaths.LocalDataPath(
                "Patch State", "Active");
        }

        private static void ValidateModKey(string modKey)
        {
            if (String.IsNullOrEmpty(modKey) ||
                !Regex.IsMatch(
                    modKey, "^[A-Za-z0-9-]{1,60}$",
                    RegexOptions.CultureInvariant))
                throw new InvalidOperationException(
                    "The adaptive patch receipt key is invalid.");
        }

        private static string ReadVdfValue(string text, string key)
        {
            Match match = Regex.Match(
                text,
                "\"" + Regex.Escape(key) + "\"\\s+\"([^\"]*)\"",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return match.Success ? match.Groups[1].Value : "";
        }

        private static string BytesToHex(byte[] value)
        {
            StringBuilder text = new StringBuilder(value.Length * 2);
            foreach (byte item in value)
                text.Append(item.ToString("X2"));
            return text.ToString();
        }
    }
}
