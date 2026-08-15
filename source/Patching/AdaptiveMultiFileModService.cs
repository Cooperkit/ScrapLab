using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace RaidRescue
{
    internal sealed class AdaptiveModFileDefinition
    {
        public string RelativePath;
        public string DisplayName;
        public string KnownCleanHash;
        public string Marker;
        public Func<string, string> Patch;
        public Func<string, string> Unpatch;
        public Action<string> Guard;
        public Func<string, bool> TrustedCleanVariant;
    }

    internal sealed class AdaptiveMultiFileModDefinition
    {
        public string ModKey;
        public string DisplayName;
        public string DefinitionVersion;
        public string InstalledReason;
        public string RemovedReason;
        public List<string> InstallChanges;
        public List<string> UpgradeChanges;
        public List<string> RemoveChanges;
        public List<AdaptiveModFileDefinition> Files;
    }

    internal static class AdaptiveMultiFileModService
    {
        private sealed class FileState
        {
            public AdaptiveModFileDefinition Definition;
            public string Path;
            public LuaTextDocument Document;
            public string CurrentHash;
            public bool Clean;
            public bool Installed;
            public bool NeedsUpgrade;
            public string PatchedText;
            public string CleanText;
            public byte[] OutputBytes;
            public string OutputHash;
            public string BackupFile;
        }

        internal static GamePatchResult GetStatus(
            AdaptiveMultiFileModDefinition definition)
        {
            GamePatchResult result = new GamePatchResult
            {
                Changes = new List<string>()
            };
            try
            {
                RequireDefinition(definition);
                string gamePath = GamePatchService.FindGameInstall();
                if (String.IsNullOrEmpty(gamePath))
                    throw new InvalidOperationException(
                        "Scrap Mechanic was not found.");

                result.GamePath = gamePath;
                string executable = Path.Combine(
                    gamePath, "Release", "ScrapMechanic.exe");
                if (!File.Exists(executable))
                    throw new FileNotFoundException(
                        "ScrapMechanic.exe was not found.", executable);
                result.GameVersion = FileVersionInfo.GetVersionInfo(
                    executable).FileVersion;
                SteamBuildInfo build = AdaptivePatchSupport.GetSteamBuild(
                    gamePath, result.GameVersion);
                List<FileState> states = Probe(
                    definition, gamePath, false);
                return FillStatus(
                    definition, result, gamePath, build, states);
            }
            catch (Exception exception)
            {
                result.Success = false;
                result.Error = exception.Message;
                return result;
            }
        }

        internal static GamePatchResult GetStatusAt(
            AdaptiveMultiFileModDefinition definition,
            string gamePath)
        {
            GamePatchResult result = new GamePatchResult
            {
                GamePath = gamePath,
                Changes = new List<string>()
            };
            try
            {
                RequireDefinition(definition);
                string executable = Path.Combine(
                    gamePath, "Release", "ScrapMechanic.exe");
                if (!File.Exists(executable))
                    throw new FileNotFoundException(
                        "ScrapMechanic.exe was not found.", executable);
                result.GameVersion = FileVersionInfo.GetVersionInfo(
                    executable).FileVersion;
                SteamBuildInfo build =
                    AdaptivePatchSupport.GetSteamBuild(
                        gamePath, result.GameVersion);
                return FillStatus(
                    definition, result, gamePath, build,
                    Probe(definition, gamePath, false));
            }
            catch (Exception exception)
            {
                result.Success = false;
                result.Error = exception.Message;
                return result;
            }
        }

        internal static GamePatchResult SetEnabled(
            AdaptiveMultiFileModDefinition definition,
            bool enabled)
        {
            if (GamePatchService.IsGameRunning())
                return Failure(
                    "Scrap Mechanic is running. Close the game completely before changing secret mods.");

            string gamePath = GamePatchService.FindGameInstall();
            if (String.IsNullOrEmpty(gamePath))
                return Failure("Scrap Mechanic was not found.");

            GamePatchResult result = SetEnabledAt(
                definition, gamePath,
                ProductPaths.LocalDataPath(
                    "Game Backups", "Scrap Mechanic", "Secret Mods"),
                enabled);
            return GameScriptCacheInvalidator.DeleteAfterChanges(
                gamePath, result);
        }

        internal static GamePatchResult SetEnabledAt(
            AdaptiveMultiFileModDefinition definition,
            string gamePath, string backupRoot,
            bool enabled)
        {
            GamePatchResult result = new GamePatchResult
            {
                GamePath = gamePath,
                Installed = enabled,
                Changes = new List<string>()
            };
            try
            {
                RequireDefinition(definition);
                string executable = Path.Combine(
                    gamePath, "Release", "ScrapMechanic.exe");
                if (!File.Exists(executable))
                    throw new FileNotFoundException(
                        "ScrapMechanic.exe was not found.", executable);
                result.GameVersion = FileVersionInfo.GetVersionInfo(
                    executable).FileVersion;

                SteamBuildInfo build = AdaptivePatchSupport.GetSteamBuild(
                    gamePath, result.GameVersion);
                List<FileState> states = Probe(
                    definition, gamePath, true);
                int cleanCount = CountClean(states);
                int installedCount = CountInstalled(states);

                if (enabled && installedCount == states.Count)
                {
                    if (NeedsDefinitionUpgrade(states))
                    {
                        return ApplyDefinitionUpgrade(
                            definition, result, gamePath,
                            backupRoot, build, states);
                    }
                    if (AdaptivePatchSupport.RequiresBuildRefresh(
                        definition.ModKey, build))
                    {
                        AdaptivePatchSupport.PrepareBuildRefresh(
                            result, definition.ModKey, build,
                            definition.DisplayName +
                            " was reactivated after the Steam update.");
                        return result;
                    }
                    result.Success = true;
                    result.Installed = true;
                    result.AlreadyPatched = true;
                    AdaptivePatchSupport.FillResult(
                        result, build,
                        PatchCompatibilityState.AdaptiveInstalled,
                        true, true,
                        definition.DisplayName +
                        " is already installed.");
                    result.Changes.Add(
                        definition.DisplayName +
                        " is already installed.");
                    return result;
                }

                if (!enabled && cleanCount == states.Count)
                {
                    result.Success = true;
                    result.Installed = false;
                    result.AlreadyPatched = true;
                    AdaptivePatchSupport.DeleteReceipt(
                        definition.ModKey);
                    AdaptivePatchSupport.DeleteBuildActivation(
                        definition.ModKey);
                    bool known = AreAllKnownClean(states);
                    AdaptivePatchSupport.FillResult(
                        result, build,
                        known
                            ? PatchCompatibilityState.KnownClean
                            : PatchCompatibilityState.CompatibleUpdate,
                        !known, true,
                        definition.DisplayName +
                        " is already removed.");
                    result.Changes.Add(
                        definition.DisplayName +
                        " is already removed.");
                    return result;
                }

                if (enabled)
                {
                    string reason = "";
                    List<string> unknownPaths =
                        UnknownCleanPaths(states);
                    bool trusted = cleanCount == states.Count &&
                        (unknownPaths.Count == 0 ||
                         AdaptivePatchSupport.CanAdaptCleanFiles(
                            build, unknownPaths, out reason));
                    if (!trusted)
                    {
                        throw new InvalidOperationException(
                            definition.DisplayName +
                            " cannot be applied: " +
                            (String.IsNullOrEmpty(reason)
                                ? "one or more protected targets changed."
                            : reason));
                    }

                    AdaptivePatchSupport.RetireVerifiedSupersededReceipt(
                        definition.ModKey,
                        "Steam Verify restored every protected " +
                        definition.DisplayName + " target to a verified clean state.");
                }
                else if (installedCount != states.Count)
                {
                    throw new InvalidOperationException(
                        definition.DisplayName +
                        " cannot be removed because one or more protected snippets are missing, duplicated, or edited.");
                }

                string stamp = DateTime.Now.ToString(
                    "yyyyMMdd-HHmmss-fff");
                string backupPath = Path.Combine(
                    backupRoot,
                    (enabled ? "Install-" : "Remove-") +
                    definition.ModKey + "-" + stamp);
                Directory.CreateDirectory(backupPath);
                result.BackupPath = backupPath;

                List<AdaptivePatchReceiptFile> manifestFiles =
                    new List<AdaptivePatchReceiptFile>();
                foreach (FileState state in states)
                {
                    string output = enabled
                        ? state.PatchedText
                        : state.CleanText;
                    state.OutputBytes = state.Document.Render(output);
                    state.OutputHash = AdaptivePatchSupport.Sha256(
                        state.OutputBytes);
                    state.BackupFile = Path.Combine(
                        backupPath,
                        state.Definition.RelativePath);
                    Directory.CreateDirectory(
                        Path.GetDirectoryName(state.BackupFile));
                    File.Copy(state.Path, state.BackupFile, false);
                    if (!HashEquals(
                        AdaptivePatchSupport.Sha256(state.BackupFile),
                        state.CurrentHash))
                    {
                        throw new IOException(
                            state.Definition.DisplayName +
                            " backup failed checksum verification.");
                    }
                    manifestFiles.Add(new AdaptivePatchReceiptFile
                    {
                        RelativePath = state.Definition.RelativePath,
                        SourceHash = state.CurrentHash,
                        OutputHash = state.OutputHash,
                        Newline = state.Document.Newline == "\r\n"
                            ? "CRLF" : "LF",
                        HasBom = state.Document.HasBom
                    });
                }

                AdaptivePatchSupport.WriteBackupManifest(
                    backupPath, definition.DisplayName,
                    enabled ? "Install" : "Remove",
                    gamePath, build,
                    definition.DefinitionVersion,
                    manifestFiles);

                AdaptivePatchReceipt receipt =
                    AdaptivePatchSupport.LoadReceipt(
                        definition.ModKey);
                bool exactRestore = !enabled && receipt != null;
                if (exactRestore)
                {
                    foreach (FileState state in states)
                    {
                        AdaptivePatchReceiptFile file =
                            AdaptivePatchSupport.FindReceiptFile(
                                receipt,
                                state.Definition.RelativePath);
                        if (file == null ||
                            !HashEquals(
                                state.CurrentHash,
                                file.OutputHash) ||
                            !File.Exists(file.BackupPath) ||
                            !HashEquals(
                                AdaptivePatchSupport.Sha256(
                                    file.BackupPath),
                                file.SourceHash))
                        {
                            exactRestore = false;
                            break;
                        }
                    }
                }

                List<FileState> replaced = new List<FileState>();
                try
                {
                    foreach (FileState state in states)
                    {
                        if (exactRestore)
                        {
                            AdaptivePatchReceiptFile file =
                                AdaptivePatchSupport.FindReceiptFile(
                                    receipt,
                                    state.Definition.RelativePath);
                            AdaptivePatchSupport.ReplaceFile(
                                state.Path,
                                File.ReadAllBytes(file.BackupPath),
                                definition.ModKey + "-exact-restore");
                            state.OutputHash = file.SourceHash;
                        }
                        else
                        {
                            AdaptivePatchSupport.ReplaceFile(
                                state.Path, state.OutputBytes,
                                definition.ModKey + "-adaptive");
                        }
                        replaced.Add(state);
                        if (!HashEquals(
                            AdaptivePatchSupport.Sha256(state.Path),
                            state.OutputHash))
                        {
                            throw new IOException(
                                state.Definition.DisplayName +
                                " failed final output verification.");
                        }
                    }
                }
                catch
                {
                    foreach (FileState state in replaced)
                        File.Copy(state.BackupFile, state.Path, true);
                    foreach (FileState state in replaced)
                    {
                        if (!HashEquals(
                            AdaptivePatchSupport.Sha256(state.Path),
                            state.CurrentHash))
                        {
                            throw new IOException(
                                definition.DisplayName +
                                " rollback could not restore " +
                                state.Definition.DisplayName + ".");
                        }
                    }
                    throw;
                }

                result.Success = true;
                result.Installed = enabled;
                result.FilesPatched = states.Count;
                bool adaptive = !AreAllKnownClean(states);
                AdaptivePatchSupport.FillResult(
                    result, build,
                    enabled
                        ? (adaptive
                            ? PatchCompatibilityState.AdaptiveInstalled
                            : PatchCompatibilityState.KnownInstalled)
                        : (adaptive
                            ? PatchCompatibilityState.CompatibleUpdate
                            : PatchCompatibilityState.KnownClean),
                    adaptive, true,
                    enabled
                        ? definition.InstalledReason
                        : definition.RemovedReason);
                AddChanges(
                    result,
                    enabled
                        ? definition.InstallChanges
                        : definition.RemoveChanges);

                if (enabled)
                {
                    AdaptivePatchReceipt newReceipt =
                        new AdaptivePatchReceipt
                        {
                            ModKey = definition.ModKey,
                            DefinitionVersion =
                                definition.DefinitionVersion,
                            SteamBuildId = build.BuildId,
                            GameVersion = result.GameVersion,
                            CreatedUtc = DateTime.UtcNow.ToString("O"),
                            Files = new List<AdaptivePatchReceiptFile>()
                        };
                    foreach (FileState state in states)
                    {
                        string activeBase =
                            AdaptivePatchSupport.CaptureBaseBackup(
                                definition.ModKey,
                                state.Definition.RelativePath,
                                state.BackupFile,
                                state.CurrentHash);
                        newReceipt.Files.Add(
                            new AdaptivePatchReceiptFile
                            {
                                RelativePath =
                                    state.Definition.RelativePath,
                                SourceHash = state.CurrentHash,
                                OutputHash = state.OutputHash,
                                BackupPath = activeBase,
                                Newline = state.Document.Newline == "\r\n"
                                    ? "CRLF" : "LF",
                                HasBom = state.Document.HasBom
                            });
                    }
                    AdaptivePatchSupport.SaveReceipt(
                        definition.ModKey, newReceipt);
                }
                else
                {
                    AdaptivePatchSupport.DeleteReceipt(
                        definition.ModKey);
                }

                AdaptivePatchSupport.QueueBuildActivation(
                    result, definition.ModKey, enabled);
                SecretModBackupRetention.Prune(
                    backupRoot, definition.ModKey,
                    backupPath, result);
                return result;
            }
            catch (Exception exception)
            {
                result.Success = false;
                result.Error = exception.Message;
                return result;
            }
        }

        private static GamePatchResult ApplyDefinitionUpgrade(
            AdaptiveMultiFileModDefinition definition,
            GamePatchResult result, string gamePath,
            string backupRoot, SteamBuildInfo build,
            List<FileState> states)
        {
            string stamp = DateTime.Now.ToString(
                "yyyyMMdd-HHmmss-fff");
            string backupPath = Path.Combine(
                backupRoot, "Update-" +
                definition.ModKey + "-" + stamp);
            Directory.CreateDirectory(backupPath);
            result.BackupPath = backupPath;

            AdaptivePatchReceipt previous =
                AdaptivePatchSupport.LoadReceipt(
                    definition.ModKey);
            AdaptivePatchReceipt updated =
                new AdaptivePatchReceipt
                {
                    ModKey = definition.ModKey,
                    DefinitionVersion =
                        definition.DefinitionVersion,
                    SteamBuildId = build.BuildId,
                    GameVersion = result.GameVersion,
                    CreatedUtc = DateTime.UtcNow.ToString("O"),
                    Files = new List<AdaptivePatchReceiptFile>()
                };
            List<AdaptivePatchReceiptFile> manifestFiles =
                new List<AdaptivePatchReceiptFile>();

            foreach (FileState state in states)
            {
                state.OutputBytes = state.Document.Render(
                    state.PatchedText);
                state.OutputHash = AdaptivePatchSupport.Sha256(
                    state.OutputBytes);
                state.BackupFile = Path.Combine(
                    backupPath,
                    state.Definition.RelativePath);
                Directory.CreateDirectory(
                    Path.GetDirectoryName(state.BackupFile));
                File.Copy(state.Path, state.BackupFile, false);
                if (!HashEquals(
                    AdaptivePatchSupport.Sha256(
                        state.BackupFile),
                    state.CurrentHash))
                {
                    throw new IOException(
                        state.Definition.DisplayName +
                        " update backup failed checksum verification.");
                }

                AdaptivePatchReceiptFile priorFile =
                    AdaptivePatchSupport.FindReceiptFile(
                        previous,
                        state.Definition.RelativePath);
                bool preservePriorBase =
                    priorFile != null &&
                    HashEquals(
                        state.CurrentHash,
                        priorFile.OutputHash) &&
                    File.Exists(priorFile.BackupPath) &&
                    HashEquals(
                        AdaptivePatchSupport.Sha256(
                            priorFile.BackupPath),
                        priorFile.SourceHash);

                string sourceHash;
                string sourceBackup;
                if (preservePriorBase)
                {
                    sourceHash = priorFile.SourceHash;
                    sourceBackup = priorFile.BackupPath;
                }
                else
                {
                    byte[] cleanBytes = state.Document.Render(
                        state.CleanText);
                    sourceHash = AdaptivePatchSupport.Sha256(
                        cleanBytes);
                    sourceBackup =
                        AdaptivePatchSupport.CaptureVersionedBaseBackup(
                            definition.ModKey,
                            state.Definition.RelativePath,
                            cleanBytes, sourceHash);
                }

                updated.Files.Add(
                    new AdaptivePatchReceiptFile
                    {
                        RelativePath =
                            state.Definition.RelativePath,
                        SourceHash = sourceHash,
                        OutputHash = state.OutputHash,
                        BackupPath = sourceBackup,
                        Newline = state.Document.Newline == "\r\n"
                            ? "CRLF" : "LF",
                        HasBom = state.Document.HasBom
                    });
                manifestFiles.Add(
                    new AdaptivePatchReceiptFile
                    {
                        RelativePath =
                            state.Definition.RelativePath,
                        SourceHash = state.CurrentHash,
                        OutputHash = state.OutputHash,
                        Newline = state.Document.Newline == "\r\n"
                            ? "CRLF" : "LF",
                        HasBom = state.Document.HasBom
                    });
            }

            AdaptivePatchSupport.WriteBackupManifest(
                backupPath, definition.DisplayName,
                "Definition update", gamePath, build,
                definition.DefinitionVersion,
                manifestFiles);

            List<FileState> replaced = new List<FileState>();
            try
            {
                foreach (FileState state in states)
                {
                    if (HashEquals(
                        state.CurrentHash,
                        state.OutputHash))
                        continue;
                    replaced.Add(state);
                    AdaptivePatchSupport.ReplaceFile(
                        state.Path, state.OutputBytes,
                        definition.ModKey +
                        "-definition-update");
                    if (!HashEquals(
                        AdaptivePatchSupport.Sha256(
                            state.Path),
                        state.OutputHash))
                    {
                        throw new IOException(
                            state.Definition.DisplayName +
                            " failed definition-update verification.");
                    }
                }
                AdaptivePatchSupport.SaveReceipt(
                    definition.ModKey, updated);
            }
            catch
            {
                foreach (FileState state in replaced)
                    File.Copy(
                        state.BackupFile,
                        state.Path, true);
                foreach (FileState state in replaced)
                {
                    if (!HashEquals(
                        AdaptivePatchSupport.Sha256(
                            state.Path),
                        state.CurrentHash))
                    {
                        throw new IOException(
                            definition.DisplayName +
                            " definition-update rollback could not restore " +
                            state.Definition.DisplayName + ".");
                    }
                }
                throw;
            }

            result.Success = true;
            result.Installed = true;
            result.NeedsUpdate = false;
            result.AlreadyPatched = false;
            result.FilesPatched = replaced.Count;
            AdaptivePatchSupport.FillResult(
                result, build,
                PatchCompatibilityState.AdaptiveInstalled,
                true, true,
                definition.DisplayName +
                " was updated to patch definition " +
                definition.DefinitionVersion + ".");
            AddChanges(result,
                definition.UpgradeChanges ??
                definition.InstallChanges);
            AdaptivePatchSupport.QueueBuildActivation(
                result, definition.ModKey, true);
            SecretModBackupRetention.Prune(
                backupRoot, definition.ModKey,
                backupPath, result);
            return result;
        }

        private static GamePatchResult FillStatus(
            AdaptiveMultiFileModDefinition definition,
            GamePatchResult result, string gamePath,
            SteamBuildInfo build, List<FileState> states)
        {
            foreach (FileState state in states)
            {
                if (state.Document.MixedNewlines)
                {
                    result.Success = true;
                    result.Installed = false;
                    AdaptivePatchSupport.FillResult(
                        result, build,
                        PatchCompatibilityState.OtherModification,
                        false, false,
                        state.Definition.DisplayName +
                        " uses mixed newline styles.");
                    return result;
                }
            }

            int clean = CountClean(states);
            int installed = CountInstalled(states);
            result.Success = true;
            if (installed == states.Count)
            {
                if (NeedsDefinitionUpgrade(states))
                {
                    result.Installed = true;
                    result.NeedsUpdate = true;
                    result.AlreadyPatched = false;
                    AdaptivePatchSupport.FillResult(
                        result, build,
                        PatchCompatibilityState.DefinitionUpdate,
                        true, true,
                        definition.DisplayName +
                        " is installed, but a newer verified patch definition is available.");
                    return result;
                }
                if (AdaptivePatchSupport.RequiresBuildRefresh(
                    definition.ModKey, build))
                {
                    AdaptivePatchSupport.MarkRefreshRequired(
                        result, build, null);
                    return result;
                }
                result.Installed = true;
                result.AlreadyPatched = true;
                AdaptivePatchSupport.FillResult(
                    result, build,
                    PatchCompatibilityState.AdaptiveInstalled,
                    true, true,
                    definition.DisplayName +
                    " is structurally intact.");
                return result;
            }

            if (clean == states.Count)
            {
                AdaptivePatchSupport.DiscardReceiptIfSuperseded(
                    definition.ModKey, gamePath);
                List<string> unknownPaths =
                    UnknownCleanPaths(states);
                string reason = "";
                bool known = unknownPaths.Count == 0;
                bool canApply = known ||
                    AdaptivePatchSupport.CanAdaptCleanFiles(
                        build, unknownPaths, out reason);
                result.Installed = false;
                AdaptivePatchSupport.FillResult(
                    result, build,
                    known
                        ? PatchCompatibilityState.KnownClean
                        : canApply
                            ? PatchCompatibilityState.CompatibleUpdate
                            : PatchCompatibilityState.OtherModification,
                    !known, canApply,
                    known
                        ? "Verified official " +
                            definition.DisplayName + " files."
                        : reason);
                return result;
            }

            FileState failed = null;
            foreach (FileState state in states)
            {
                if (!state.Clean && !state.Installed)
                {
                    failed = state;
                    break;
                }
            }
            result.Installed = false;
            AdaptivePatchSupport.FillResult(
                result, build,
                clean + installed > 0
                    ? PatchCompatibilityState.PartialConflict
                    : PatchCompatibilityState.UnsupportedCode,
                false, false,
                (failed == null
                    ? "A protected file"
                    : failed.Definition.DisplayName) +
                " contains changed, duplicated, or partial " +
                definition.DisplayName + " code.");
            return result;
        }

        private static List<FileState> Probe(
            AdaptiveMultiFileModDefinition definition,
            string gamePath, bool requireFormat)
        {
            List<FileState> states = new List<FileState>();
            foreach (AdaptiveModFileDefinition file in definition.Files)
            {
                string path = Path.Combine(
                    gamePath, file.RelativePath);
                if (!File.Exists(path))
                    throw new FileNotFoundException(
                        file.DisplayName + " was not found.", path);
                LuaTextDocument document =
                    AdaptivePatchSupport.ReadLua(path);
                if (requireFormat)
                {
                    AdaptivePatchSupport.RequireAdaptiveFormat(
                        document, file.DisplayName);
                }
                file.Guard(document.NormalizedText);

                int markerCount = AdaptivePatchSupport.Count(
                    document.NormalizedText, file.Marker);
                bool clean = false;
                bool installed = false;
                string patched = null;
                string unpatched = null;
                if (markerCount == 0)
                {
                    try
                    {
                        patched = file.Patch(
                            document.NormalizedText);
                        clean = true;
                    }
                    catch (InvalidDataException) { }
                }
                else if (markerCount == 1)
                {
                    try
                    {
                        unpatched = file.Unpatch(
                            document.NormalizedText);
                        patched = file.Patch(unpatched);
                        installed = true;
                    }
                    catch (InvalidDataException) { }
                }

                states.Add(new FileState
                {
                    Definition = file,
                    Path = path,
                    Document = document,
                    CurrentHash = document.OriginalHash,
                    Clean = clean,
                    Installed = installed,
                    NeedsUpgrade = installed && !String.Equals(
                        patched, document.NormalizedText,
                        StringComparison.Ordinal),
                    PatchedText = patched,
                    CleanText = unpatched
                });
            }
            return states;
        }

        private static List<string> UnknownCleanPaths(
            List<FileState> states)
        {
            List<string> paths = new List<string>();
            foreach (FileState state in states)
            {
                if (!HashEquals(
                    state.CurrentHash,
                    state.Definition.KnownCleanHash) &&
                    (state.Definition.TrustedCleanVariant == null ||
                     !state.Definition.TrustedCleanVariant(
                        state.Document.NormalizedText)))
                {
                    paths.Add(state.Path);
                }
            }
            return paths;
        }

        private static bool AreAllKnownClean(
            List<FileState> states)
        {
            return UnknownCleanPaths(states).Count == 0;
        }

        private static int CountClean(List<FileState> states)
        {
            int count = 0;
            foreach (FileState state in states)
                if (state.Clean) count++;
            return count;
        }

        private static int CountInstalled(List<FileState> states)
        {
            int count = 0;
            foreach (FileState state in states)
                if (state.Installed) count++;
            return count;
        }

        private static bool NeedsDefinitionUpgrade(
            List<FileState> states)
        {
            foreach (FileState state in states)
                if (state.NeedsUpgrade) return true;
            return false;
        }

        private static void AddChanges(
            GamePatchResult result, List<string> changes)
        {
            if (changes == null)
                return;
            foreach (string change in changes)
            {
                if (!String.IsNullOrEmpty(change))
                    result.Changes.Add(change);
            }
        }

        private static void RequireDefinition(
            AdaptiveMultiFileModDefinition definition)
        {
            if (definition == null ||
                String.IsNullOrEmpty(definition.ModKey) ||
                String.IsNullOrEmpty(definition.DisplayName) ||
                String.IsNullOrEmpty(definition.DefinitionVersion) ||
                definition.Files == null ||
                definition.Files.Count == 0)
            {
                throw new InvalidOperationException(
                    "The adaptive mod definition is incomplete.");
            }
            foreach (AdaptiveModFileDefinition file in definition.Files)
            {
                if (file == null ||
                    String.IsNullOrEmpty(file.RelativePath) ||
                    String.IsNullOrEmpty(file.DisplayName) ||
                    String.IsNullOrEmpty(file.KnownCleanHash) ||
                    String.IsNullOrEmpty(file.Marker) ||
                    file.Patch == null ||
                    file.Unpatch == null ||
                    file.Guard == null)
                {
                    throw new InvalidOperationException(
                        "An adaptive mod file definition is incomplete.");
                }
            }
        }

        private static bool HashEquals(
            string left, string right)
        {
            return String.Equals(
                left, right,
                StringComparison.OrdinalIgnoreCase);
        }

        private static GamePatchResult Failure(string message)
        {
            return new GamePatchResult
            {
                Success = false,
                Error = message,
                Changes = new List<string>()
            };
        }
    }
}
