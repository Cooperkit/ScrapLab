using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace RaidRescue
{
    internal static class CarrySprintPatchService
    {
        private const string ModKey = "FullSpeedCarrying";
        private const string DefinitionVersion = "1";

        private const string KnownCarryCleanHash =
            "BF08DEB38238C34B1C3A884AF10C4FA153846E21BA287E2F8D983BC7DB908200";
        private const string KnownLiftCleanHash =
            "1DFD50CF6B82238793F5BBFD01A00ABD891BC7342D66CA941ED15E68926B767E";

        internal static readonly string CarryToolRelativePath =
            Path.Combine(
                "Survival", "Scripts", "game", "tools",
                "CarryTool.lua");
        internal static readonly string SurvivalLiftRelativePath =
            Path.Combine(
                "Survival", "Scripts", "game", "tools",
                "SurvivalLift.lua");

        internal const string CarryPatchMarker =
            "-- SCRAPLAB SECRET MOD: full-speed hand carrying with native sprint animations.";
        internal const string LiftPatchMarker =
            "-- SCRAPLAB SECRET MOD: full-speed lift carrying.";

        internal const string OriginalAnimationHeader =
            "local function buildAnimSet(config)\n" +
            "    local prefix = config.animation.prefix\n" +
            "    local set = {";
        internal const string PatchedAnimationHeader =
            "local function buildAnimSet(config)\n" +
            "    local prefix = config.animation.prefix\n" +
            "    " + CarryPatchMarker + "\n" +
            "    local sprintPrefix = prefix\n" +
            "    local sprintMovement = sprintPrefix == \"toolgorp\" and \"toolgorp_sprint\" or sprintPrefix .. \"_sprint_idle\"\n" +
            "    local set = {";

        internal const string OriginalMovementAnimations =
            "            idle       = prefix .. \"_idle\",\n" +
            "            runFwd     = prefix .. \"_run\",\n" +
            "            runBwd     = prefix .. \"_runbwd\",\n" +
            "            jump       = prefix .. \"_jump\",";
        internal const string PatchedMovementAnimations =
            "            idle       = prefix .. \"_idle\",\n" +
            "            runFwd     = prefix .. \"_run\",\n" +
            "            runBwd     = prefix .. \"_runbwd\",\n" +
            "            sprint     = sprintMovement,\n" +
            "            sprintLeft = sprintPrefix == \"bucket\" and \"bucket_sprint_left\" or sprintMovement,\n" +
            "            sprintRight = sprintPrefix == \"bucket\" and \"bucket_sprint_right\" or sprintMovement,\n" +
            "            jump       = prefix .. \"_jump\",";

        internal const string OriginalFirstPersonAnimations =
            "        fp = {\n" +
            "            idle       = { prefix .. \"_idle\", { looping = true } },\n" +
            "            equip      = { prefix .. \"_pickup\", { nextAnimation = \"idle\" } },\n" +
            "            unequip    = { prefix .. \"_putdown\" },\n" +
            "        },";
        internal const string PatchedFirstPersonAnimations =
            "        fp = {\n" +
            "            idle       = { prefix .. \"_idle\", { looping = true } },\n" +
            "            equip      = { prefix .. \"_pickup\", { nextAnimation = \"idle\" } },\n" +
            "            unequip    = { prefix .. \"_putdown\" },\n" +
            "            sprintInto = { sprintPrefix .. \"_sprint_into\", { nextAnimation = \"sprintIdle\", blendNext = 0.2 } },\n" +
            "            sprintExit = { sprintPrefix .. \"_sprint_exit\", { nextAnimation = \"idle\", blendNext = 0 } },\n" +
            "            sprintIdle = { sprintPrefix .. \"_sprint_idle\", { looping = true } },\n" +
            "        },";

        internal const string OriginalFirstPersonUpdate =
            "\tif self.tool:isLocal() then\n" +
            "\t\tupdateFpAnimations( self.cl.fpAnimations, self.equipped, dt )\n" +
            "    end";
        internal const string PatchedFirstPersonUpdate =
            "\tif self.tool:isLocal() then\n" +
            "        if self.equipped then\n" +
            "            local sprinting = self.tool:isSprinting()\n" +
            "            local current = self.cl.fpAnimations.currentAnimation\n" +
            "            if sprinting and current ~= \"sprintInto\" and current ~= \"sprintIdle\" then\n" +
            "                swapFpAnimation( self.cl.fpAnimations, \"sprintExit\", \"sprintInto\", 0.0 )\n" +
            "            elseif not sprinting and ( current == \"sprintIdle\" or current == \"sprintInto\" ) then\n" +
            "                swapFpAnimation( self.cl.fpAnimations, \"sprintInto\", \"sprintExit\", 0.0 )\n" +
            "            end\n" +
            "        end\n" +
            "\t\tupdateFpAnimations( self.cl.fpAnimations, self.equipped, dt )\n" +
            "    end";

        internal const string OriginalCarrySprintBlock =
            "\tif self.tool:isLocal() then\n" +
            "\t\tself.tool:setBlockSprint( true )\n" +
            "\t\tlocal carryContainer = sm.localPlayer.getCarry()";
        internal const string PatchedCarrySprintBlock =
            "\tif self.tool:isLocal() then\n" +
            "\t\tself.tool:setBlockSprint( false )\n" +
            "\t\tlocal carryContainer = sm.localPlayer.getCarry()";

        internal const string OriginalLiftSprintBlock =
            "\t\tlocal carry = self.selectedBodies and #self.selectedBodies > 0 and self.equipped\n" +
            "\t\tself.tool:setBlockSprint( carry )";
        internal const string PatchedLiftSprintBlock =
            "\t\tlocal carry = self.selectedBodies and #self.selectedBodies > 0 and self.equipped\n" +
            "\t\t" + LiftPatchMarker + "\n" +
            "\t\tself.tool:setBlockSprint( false )";

        private sealed class PatchTarget
        {
            public string RelativePath;
            public string DisplayName;
            public string KnownCleanHash;
            public string Marker;
            public Func<string, string> Patch;
            public Func<string, string> Unpatch;
            public Action<string> Guard;
        }

        private sealed class PatchState
        {
            public PatchTarget Target;
            public string Path;
            public LuaTextDocument Document;
            public string CurrentHash;
            public bool Clean;
            public bool Installed;
            public string PatchedText;
            public string CleanText;
            public byte[] OutputBytes;
            public string OutputHash;
            public string BackupFile;
        }

        public static GamePatchResult GetStatus()
        {
            GamePatchResult result = new GamePatchResult
            {
                Changes = new List<string>()
            };
            try
            {
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
                List<PatchState> states = Probe(gamePath, false);
                return FillStatus(result, gamePath, build, states);
            }
            catch (Exception exception)
            {
                result.Success = false;
                result.Error = exception.Message;
                return result;
            }
        }

        public static GamePatchResult SetEnabled(bool enabled)
        {
            if (GamePatchService.IsGameRunning())
                return Failure(
                    "Scrap Mechanic is running. Close the game completely before changing secret mods.");

            string gamePath = GamePatchService.FindGameInstall();
            if (String.IsNullOrEmpty(gamePath))
                return Failure("Scrap Mechanic was not found.");

            GamePatchResult result = SetEnabledAt(
                gamePath,
                ProductPaths.LocalDataPath(
                    "Game Backups", "Scrap Mechanic", "Secret Mods"),
                enabled);
            return GameScriptCacheInvalidator.DeleteAfterChanges(
                gamePath, result);
        }

        internal static GamePatchResult SetEnabledAt(
            string gamePath, string backupRoot, bool enabled)
        {
            GamePatchResult result = new GamePatchResult
            {
                GamePath = gamePath,
                Installed = enabled,
                Changes = new List<string>()
            };
            try
            {
                string executable = Path.Combine(
                    gamePath, "Release", "ScrapMechanic.exe");
                if (!File.Exists(executable))
                    throw new FileNotFoundException(
                        "ScrapMechanic.exe was not found.", executable);
                result.GameVersion = FileVersionInfo.GetVersionInfo(
                    executable).FileVersion;

                SteamBuildInfo build = AdaptivePatchSupport.GetSteamBuild(
                    gamePath, result.GameVersion);
                List<PatchState> states = Probe(gamePath, true);
                int cleanCount = CountClean(states);
                int installedCount = CountInstalled(states);

                if (enabled && installedCount == states.Count)
                {
                    if (AdaptivePatchSupport.RequiresBuildRefresh(
                        ModKey, build))
                    {
                        AdaptivePatchSupport.PrepareBuildRefresh(
                            result, ModKey, build,
                            "Full-Speed Carrying was reactivated after the Steam update.");
                        return result;
                    }
                    result.Success = true;
                    result.Installed = true;
                    result.AlreadyPatched = true;
                    AdaptivePatchSupport.FillResult(
                        result, build,
                        PatchCompatibilityState.AdaptiveInstalled,
                        true, true,
                        "Full-Speed Carrying is already installed.");
                    result.Changes.Add(
                        "Full-Speed Carrying is already installed.");
                    return result;
                }
                if (!enabled && cleanCount == states.Count)
                {
                    result.Success = true;
                    result.Installed = false;
                    result.AlreadyPatched = true;
                    AdaptivePatchSupport.DeleteReceipt(ModKey);
                    AdaptivePatchSupport.DeleteBuildActivation(ModKey);
                    bool known = AreAllKnownClean(states);
                    AdaptivePatchSupport.FillResult(
                        result, build,
                        known
                            ? PatchCompatibilityState.KnownClean
                            : PatchCompatibilityState.CompatibleUpdate,
                        !known, true,
                        "Full-Speed Carrying is already removed.");
                    result.Changes.Add(
                        "Full-Speed Carrying is already removed.");
                    return result;
                }

                if (enabled)
                {
                    string reason = "";
                    List<string> unknownPaths = UnknownCleanPaths(states);
                    bool trusted = cleanCount == states.Count &&
                        (unknownPaths.Count == 0 ||
                         AdaptivePatchSupport.CanAdaptCleanFiles(
                            build, unknownPaths, out reason));
                    if (!trusted)
                    {
                        throw new InvalidOperationException(
                            "Full-Speed Carrying cannot be applied: " +
                            (String.IsNullOrEmpty(reason)
                                ? "CarryTool.lua or SurvivalLift.lua changed protected carrying code."
                            : reason));
                    }

                    AdaptivePatchSupport.RetireVerifiedSupersededReceipt(
                        ModKey,
                        "Steam Verify restored every protected Full-Speed Carrying target to a verified clean state.");
                }
                else if (installedCount != states.Count)
                {
                    throw new InvalidOperationException(
                        "Full-Speed Carrying cannot be removed because one or more protected snippets are missing, duplicated, or edited.");
                }

                string stamp = DateTime.Now.ToString(
                    "yyyyMMdd-HHmmss-fff");
                string backupPath = Path.Combine(
                    backupRoot,
                    (enabled ? "Install-" : "Remove-") +
                    ModKey + "-" + stamp);
                Directory.CreateDirectory(backupPath);
                result.BackupPath = backupPath;

                List<AdaptivePatchReceiptFile> manifestFiles =
                    new List<AdaptivePatchReceiptFile>();
                foreach (PatchState state in states)
                {
                    string output = enabled
                        ? state.PatchedText
                        : state.CleanText;
                    state.OutputBytes = state.Document.Render(output);
                    state.OutputHash = AdaptivePatchSupport.Sha256(
                        state.OutputBytes);
                    state.BackupFile = Path.Combine(
                        backupPath,
                        Path.GetFileName(state.Target.RelativePath));
                    File.Copy(state.Path, state.BackupFile, false);
                    if (!HashEquals(
                        AdaptivePatchSupport.Sha256(state.BackupFile),
                        state.CurrentHash))
                    {
                        throw new IOException(
                            state.Target.DisplayName +
                            " backup failed checksum verification.");
                    }
                    manifestFiles.Add(new AdaptivePatchReceiptFile
                    {
                        RelativePath = state.Target.RelativePath,
                        SourceHash = state.CurrentHash,
                        OutputHash = state.OutputHash,
                        Newline = state.Document.Newline == "\r\n"
                            ? "CRLF" : "LF",
                        HasBom = state.Document.HasBom
                    });
                }

                AdaptivePatchSupport.WriteBackupManifest(
                    backupPath, "Full-Speed Carrying",
                    enabled ? "Install" : "Remove",
                    gamePath, build, DefinitionVersion,
                    manifestFiles);

                AdaptivePatchReceipt receipt =
                    AdaptivePatchSupport.LoadReceipt(ModKey);
                bool exactRestore = !enabled && receipt != null;
                if (exactRestore)
                {
                    foreach (PatchState state in states)
                    {
                        AdaptivePatchReceiptFile file =
                            AdaptivePatchSupport.FindReceiptFile(
                                receipt, state.Target.RelativePath);
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

                List<PatchState> replaced = new List<PatchState>();
                try
                {
                    foreach (PatchState state in states)
                    {
                        if (exactRestore)
                        {
                            AdaptivePatchReceiptFile file =
                                AdaptivePatchSupport.FindReceiptFile(
                                    receipt, state.Target.RelativePath);
                            AdaptivePatchSupport.ReplaceFile(
                                state.Path,
                                File.ReadAllBytes(file.BackupPath),
                                "full-speed-carry-exact-restore");
                            state.OutputHash = file.SourceHash;
                        }
                        else
                        {
                            AdaptivePatchSupport.ReplaceFile(
                                state.Path, state.OutputBytes,
                                "full-speed-carry-adaptive");
                        }
                        replaced.Add(state);
                        if (!HashEquals(
                            AdaptivePatchSupport.Sha256(state.Path),
                            state.OutputHash))
                        {
                            throw new IOException(
                                state.Target.DisplayName +
                                " failed final output verification.");
                        }
                    }
                }
                catch
                {
                    foreach (PatchState state in replaced)
                        File.Copy(state.BackupFile, state.Path, true);
                    foreach (PatchState state in replaced)
                    {
                        if (!HashEquals(
                            AdaptivePatchSupport.Sha256(state.Path),
                            state.CurrentHash))
                        {
                            throw new IOException(
                                "Full-Speed Carrying rollback could not restore " +
                                state.Target.DisplayName + ".");
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
                        ? "Installed with exact protected-code matching."
                        : "Restored carrying behavior while preserving unrelated script changes.");
                result.Changes.Add(
                    enabled
                        ? "Hand-carried objects now allow normal walking and sprinting with native carry animations."
                        : "Restored the original hand-carry sprint restriction.");
                result.Changes.Add(
                    enabled
                        ? "Lift-held creations no longer block sprinting."
                        : "Restored the original lift-carry sprint restriction.");

                if (enabled)
                {
                    AdaptivePatchReceipt newReceipt =
                        new AdaptivePatchReceipt
                        {
                            ModKey = ModKey,
                            DefinitionVersion = DefinitionVersion,
                            SteamBuildId = build.BuildId,
                            GameVersion = result.GameVersion,
                            CreatedUtc = DateTime.UtcNow.ToString("O"),
                            Files = new List<AdaptivePatchReceiptFile>()
                        };
                    foreach (PatchState state in states)
                    {
                        string activeBase =
                            AdaptivePatchSupport.CaptureBaseBackup(
                                ModKey, state.Target.RelativePath,
                                state.BackupFile, state.CurrentHash);
                        newReceipt.Files.Add(
                            new AdaptivePatchReceiptFile
                            {
                                RelativePath = state.Target.RelativePath,
                                SourceHash = state.CurrentHash,
                                OutputHash = state.OutputHash,
                                BackupPath = activeBase,
                                Newline = state.Document.Newline == "\r\n"
                                    ? "CRLF" : "LF",
                                HasBom = state.Document.HasBom
                            });
                    }
                    AdaptivePatchSupport.SaveReceipt(
                        ModKey, newReceipt);
                }
                else
                {
                    AdaptivePatchSupport.DeleteReceipt(ModKey);
                }

                AdaptivePatchSupport.QueueBuildActivation(
                    result, ModKey, enabled);
                SecretModBackupRetention.Prune(
                    backupRoot, ModKey, backupPath, result);
                return result;
            }
            catch (Exception exception)
            {
                result.Success = false;
                result.Error = exception.Message;
                return result;
            }
        }

        internal static string PatchCarryText(string text)
        {
            string transformed = ReplaceUnique(
                text, OriginalAnimationHeader,
                PatchedAnimationHeader,
                "carry animation builder");
            transformed = ReplaceUnique(
                transformed, OriginalMovementAnimations,
                PatchedMovementAnimations,
                "third-person carry movement animations");
            transformed = ReplaceUnique(
                transformed, OriginalFirstPersonAnimations,
                PatchedFirstPersonAnimations,
                "first-person carry animations");
            transformed = ReplaceUnique(
                transformed, OriginalFirstPersonUpdate,
                PatchedFirstPersonUpdate,
                "first-person carry animation update");
            return ReplaceUnique(
                transformed, OriginalCarrySprintBlock,
                PatchedCarrySprintBlock,
                "hand-carry sprint block");
        }

        internal static string UnpatchCarryText(string text)
        {
            string transformed = ReplaceUnique(
                text, PatchedCarrySprintBlock,
                OriginalCarrySprintBlock,
                "hand-carry sprint block");
            transformed = ReplaceUnique(
                transformed, PatchedFirstPersonUpdate,
                OriginalFirstPersonUpdate,
                "first-person carry animation update");
            transformed = ReplaceUnique(
                transformed, PatchedFirstPersonAnimations,
                OriginalFirstPersonAnimations,
                "first-person carry animations");
            transformed = ReplaceUnique(
                transformed, PatchedMovementAnimations,
                OriginalMovementAnimations,
                "third-person carry movement animations");
            return ReplaceUnique(
                transformed, PatchedAnimationHeader,
                OriginalAnimationHeader,
                "carry animation builder");
        }

        internal static bool HasIntactCarryPatch(string text)
        {
            if (AdaptivePatchSupport.Count(text, CarryPatchMarker) != 1)
                return false;
            try
            {
                string clean = UnpatchCarryText(text);
                return AdaptivePatchSupport.Count(clean, CarryPatchMarker) == 0;
            }
            catch (InvalidDataException) { return false; }
        }

        internal static string PatchLiftText(string text)
        {
            return ReplaceUnique(
                text, OriginalLiftSprintBlock,
                PatchedLiftSprintBlock,
                "lift-carry sprint block");
        }

        internal static string UnpatchLiftText(string text)
        {
            return ReplaceUnique(
                text, PatchedLiftSprintBlock,
                OriginalLiftSprintBlock,
                "lift-carry sprint block");
        }

        private static GamePatchResult FillStatus(
            GamePatchResult result, string gamePath,
            SteamBuildInfo build, List<PatchState> states)
        {
            foreach (PatchState state in states)
            {
                if (state.Document.MixedNewlines)
                {
                    result.Success = true;
                    result.Installed = false;
                    AdaptivePatchSupport.FillResult(
                        result, build,
                        PatchCompatibilityState.OtherModification,
                        false, false,
                        state.Target.DisplayName +
                        " uses mixed newline styles.");
                    return result;
                }
            }

            int clean = CountClean(states);
            int installed = CountInstalled(states);
            result.Success = true;
            if (installed == states.Count)
            {
                if (AdaptivePatchSupport.RequiresBuildRefresh(
                    ModKey, build))
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
                    "CarryTool.lua and SurvivalLift.lua contain intact Full-Speed Carrying patches.");
                return result;
            }

            if (clean == states.Count)
            {
                AdaptivePatchSupport.DiscardReceiptIfSuperseded(
                    ModKey, gamePath);
                List<string> unknownPaths = UnknownCleanPaths(states);
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
                        ? "Verified official carrying-tool scripts."
                        : reason);
                return result;
            }

            result.Installed = false;
            PatchState failed = null;
            foreach (PatchState state in states)
            {
                if (!state.Clean && !state.Installed)
                {
                    failed = state;
                    break;
                }
            }
            string failedFile = failed == null
                ? "The carrying scripts"
                : failed.Target.DisplayName;
            AdaptivePatchSupport.FillResult(
                result, build,
                clean + installed > 0
                    ? PatchCompatibilityState.PartialConflict
                    : PatchCompatibilityState.UnsupportedCode,
                false, false,
                failedFile +
                " contains changed, duplicated, or partial Full-Speed Carrying code.");
            return result;
        }

        private static List<PatchState> Probe(
            string gamePath, bool requireFormat)
        {
            List<PatchState> states = new List<PatchState>();
            foreach (PatchTarget target in GetTargets())
            {
                string path = Path.Combine(gamePath, target.RelativePath);
                if (!File.Exists(path))
                    throw new FileNotFoundException(
                        target.DisplayName + " was not found.", path);
                LuaTextDocument document =
                    AdaptivePatchSupport.ReadLua(path);
                if (requireFormat)
                {
                    AdaptivePatchSupport.RequireAdaptiveFormat(
                        document, target.DisplayName);
                }

                target.Guard(document.NormalizedText);
                int markerCount = AdaptivePatchSupport.Count(
                    document.NormalizedText, target.Marker);
                bool clean = false;
                bool installed = false;
                string patched = null;
                string unpatched = null;
                if (markerCount == 0)
                {
                    try
                    {
                        patched = target.Patch(
                            document.NormalizedText);
                        clean = true;
                    }
                    catch (InvalidDataException) { }
                }
                else if (markerCount == 1)
                {
                    try
                    {
                        unpatched = target.Unpatch(
                            document.NormalizedText);
                        installed = true;
                    }
                    catch (InvalidDataException) { }
                }

                states.Add(new PatchState
                {
                    Target = target,
                    Path = path,
                    Document = document,
                    CurrentHash = document.OriginalHash,
                    Clean = clean,
                    Installed = installed,
                    PatchedText = patched,
                    CleanText = unpatched
                });
            }
            return states;
        }

        private static List<PatchTarget> GetTargets()
        {
            return new List<PatchTarget>
            {
                new PatchTarget
                {
                    RelativePath = CarryToolRelativePath,
                    DisplayName = "CarryTool.lua",
                    KnownCleanHash = KnownCarryCleanHash,
                    Marker = CarryPatchMarker,
                    Patch = PatchCarryText,
                    Unpatch = UnpatchCarryText,
                    Guard = RequireCarryGuards
                },
                new PatchTarget
                {
                    RelativePath = SurvivalLiftRelativePath,
                    DisplayName = "SurvivalLift.lua",
                    KnownCleanHash = KnownLiftCleanHash,
                    Marker = LiftPatchMarker,
                    Patch = PatchLiftText,
                    Unpatch = UnpatchLiftText,
                    Guard = RequireLiftGuards
                }
            };
        }

        private static void RequireCarryGuards(string text)
        {
            AdaptivePatchSupport.RequireUnique(
                text, "local function buildAnimSet(config)",
                "carry animation builder");
            AdaptivePatchSupport.RequireUnique(
                text, "function CarryTool.client_onUpdate( self, dt )",
                "carry update callback");
            AdaptivePatchSupport.RequireUnique(
                text, "function CarryTool.client_onEquip( self, animate )",
                "carry equip callback");
            AdaptivePatchSupport.RequireUnique(
                text, "function CarryTool.client_onUnequip( self )",
                "carry unequip callback");
        }

        private static void RequireLiftGuards(string text)
        {
            AdaptivePatchSupport.RequireUnique(
                text, "dofile \"$GAME_DATA/Scripts/game/Lift.lua\"",
                "Survival Lift base script import");
            AdaptivePatchSupport.RequireUnique(
                text,
                "\t\tlocal carry = self.selectedBodies and #self.selectedBodies > 0 and self.equipped",
                "active Survival Lift carry state");
        }

        private static int CountClean(List<PatchState> states)
        {
            int count = 0;
            foreach (PatchState state in states)
                if (state.Clean) count++;
            return count;
        }

        private static int CountInstalled(List<PatchState> states)
        {
            int count = 0;
            foreach (PatchState state in states)
                if (state.Installed) count++;
            return count;
        }

        private static List<string> UnknownCleanPaths(
            List<PatchState> states)
        {
            List<string> paths = new List<string>();
            foreach (PatchState state in states)
            {
                if (!HashEquals(
                    state.CurrentHash,
                    state.Target.KnownCleanHash) &&
                    !(String.Equals(
                        state.Target.RelativePath,
                        CarryToolRelativePath,
                        StringComparison.OrdinalIgnoreCase) &&
                      BetterPlasmaDrillsPatchService.HasIntactCarryPatch(
                        state.Document.NormalizedText)))
                {
                    paths.Add(state.Path);
                }
            }
            return paths;
        }

        private static bool AreAllKnownClean(
            List<PatchState> states)
        {
            return UnknownCleanPaths(states).Count == 0;
        }

        private static string ReplaceUnique(
            string text, string oldText,
            string newText, string description)
        {
            int first = text.IndexOf(
                oldText, StringComparison.Ordinal);
            if (first < 0 ||
                text.IndexOf(
                    oldText, first + oldText.Length,
                    StringComparison.Ordinal) >= 0)
            {
                throw new InvalidDataException(
                    "The expected " + description +
                    " code was not found exactly once.");
            }
            return text.Substring(0, first) +
                newText +
                text.Substring(first + oldText.Length);
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
