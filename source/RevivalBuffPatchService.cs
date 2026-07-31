using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace RaidRescue
{
    internal static class RevivalBuffPatchService
    {
        private const string ModKey = "RevivalBuffRecovery";
        private const string DefinitionVersion = "1";
        private const string KnownCleanHash =
            "E63BCD36B3DAB7445A3BDF4663BDC94A2AD5ADEBEF4B61F825B704C18B697A4F";
        internal static readonly string SurvivalPlayerRelativePath =
            Path.Combine(
                "Survival", "Scripts", "game",
                "SurvivalPlayer.lua");

        internal const string PatchMarker =
            "-- RAID RESCUE SECRET MOD: preserve pizza and veggie-burger buffs for Revival Baguettes.";

        internal const string OriginalUnstuck =
            "\tself.sv.saved.stats.hp = 0\n" +
            "\tself.sv.respawnInteractionAttempted = false\n" +
            "\tself.sv.saved.isConscious = false";
        internal const string PatchedUnstuck =
            "\tself.sv.saved.stats.hp = 0\n" +
            "\tself.sv.respawnInteractionAttempted = false\n" +
            "\tself:sv_raidRescueCaptureRevivalPerks()\n" +
            "\tself.sv.saved.isConscious = false";

        internal const string OriginalWarehouseDown =
            "\t\t\tself.sv.warehouseEjectionFadeTick = nil\n" +
            "\t\t\tself.sv.saved.stats.hp = 0\n" +
            "\t\t\tself.sv.saved.isConscious = false\n" +
            "\t\t\tself:sv_clearPerks()";
        internal const string PatchedWarehouseDown =
            "\t\t\tself.sv.warehouseEjectionFadeTick = nil\n" +
            "\t\t\tself.sv.saved.stats.hp = 0\n" +
            "\t\t\tself:sv_raidRescueCaptureRevivalPerks()\n" +
            "\t\t\tself.sv.saved.isConscious = false\n" +
            "\t\t\tself:sv_clearPerks()";

        internal const string OriginalDamageDown =
            "\t\t\t\t\tif self.sv.saved.stats.hp <= 0 then\n" +
            "\t\t\t\t\t\tself.sv.respawnInteractionAttempted = false\n" +
            "\t\t\t\t\t\tself.sv.saved.isConscious = false\n" +
            "\t\t\t\t\t\tself:sv_clearPerks()";
        internal const string PatchedDamageDown =
            "\t\t\t\t\tif self.sv.saved.stats.hp <= 0 then\n" +
            "\t\t\t\t\t\tself.sv.respawnInteractionAttempted = false\n" +
            "\t\t\t\t\t\tself:sv_raidRescueCaptureRevivalPerks()\n" +
            "\t\t\t\t\t\tself.sv.saved.isConscious = false\n" +
            "\t\t\t\t\t\tself:sv_clearPerks()";

        internal const string OriginalRevive =
            "function SurvivalPlayer.sv_n_revive( self, params )\n" +
            "\tparams = params or {}\n" +
            "\tlocal character = self.player:getCharacter()\n" +
            "\tif not self.sv.saved.isConscious \n" +
            "\t    and ( self.sv.saved.hasRevivalItem or params.skipRevivalItem )\n" +
            "\t\tand not self.sv.spawnparams.respawn then\n" +
            "\t\tself.sv.saved.stats.hp = self.sv.saved.stats.maxhp\n" +
            "\t\tself.sv.saved.isConscious = true\n" +
            "\t\tself.sv.saved.hasRevivalItem = false\n" +
            "\t\tself.storage:save( self.sv.saved )\n" +
            "\t\tself.network:setClientData( self.sv.saved )\n" +
            "\t\tif not params.skipRevivalItem then\n" +
            "\t\t\tself.network:sendToClient( self.player, \"cl_n_onEffect\", { name = \"Eat - EatFinish\", host = self.player.character } )\n" +
            "\t\tend\n" +
            "\t\tif character then\n" +
            "\t\t\tcharacter:setTumbling( false )\n" +
            "\t\t\tcharacter:setDowned( false )\n" +
            "\t\tend\n" +
            "\t\tself.sv.damageCooldown:start( SpawnDamageCooldown )\n" +
            "\t\tself.player:sendCharacterEvent( \"revive\" )\n" +
            "\tend\n" +
            "end";
        internal const string PatchedRevive =
            "function SurvivalPlayer.sv_n_revive( self, params )\n" +
            "\tparams = params or {}\n" +
            "\tlocal character = self.player:getCharacter()\n" +
            "\tlocal raidRescueUsedBaguette = self.sv.saved.hasRevivalItem and not params.skipRevivalItem\n" +
            "\tif not self.sv.saved.isConscious \n" +
            "\t    and ( self.sv.saved.hasRevivalItem or params.skipRevivalItem )\n" +
            "\t\tand not self.sv.spawnparams.respawn then\n" +
            "\t\tself.sv.saved.stats.hp = self.sv.saved.stats.maxhp\n" +
            "\t\tself.sv.saved.isConscious = true\n" +
            "\t\tself.sv.saved.hasRevivalItem = false\n" +
            "\t\tif raidRescueUsedBaguette then\n" +
            "\t\t\tself:sv_raidRescueRestoreRevivalPerks()\n" +
            "\t\telse\n" +
            "\t\t\tself.sv.saved.raidRescueRevivalPerks = nil\n" +
            "\t\tend\n" +
            "\t\tself.storage:save( self.sv.saved )\n" +
            "\t\tself.network:setClientData( self.sv.saved )\n" +
            "\t\tif not params.skipRevivalItem then\n" +
            "\t\t\tself.network:sendToClient( self.player, \"cl_n_onEffect\", { name = \"Eat - EatFinish\", host = self.player.character } )\n" +
            "\t\tend\n" +
            "\t\tif character then\n" +
            "\t\t\tcharacter:setTumbling( false )\n" +
            "\t\t\tcharacter:setDowned( false )\n" +
            "\t\tend\n" +
            "\t\tself.sv.damageCooldown:start( SpawnDamageCooldown )\n" +
            "\t\tself.player:sendCharacterEvent( \"revive\" )\n" +
            "\tend\n" +
            "end";

        internal const string OriginalRespawnReset =
            "\t\tself.sv.saved.isConscious = true\n" +
            "\t\tself.sv.saved.hasRevivalItem = false\n" +
            "\t\tself.sv.saved.isNewPlayer = false";
        internal const string PatchedRespawnReset =
            "\t\tself.sv.saved.isConscious = true\n" +
            "\t\tself.sv.saved.hasRevivalItem = false\n" +
            "\t\tself.sv.saved.raidRescueRevivalPerks = nil\n" +
            "\t\tself.sv.saved.isNewPlayer = false";

        internal const string OriginalClearFunction =
            "function SurvivalPlayer.sv_clearPerks( self )";
        internal const string PatchedHelpers =
            "-- RAID RESCUE SECRET MOD: preserve pizza and veggie-burger buffs for Revival Baguettes.\n" +
            "function SurvivalPlayer.sv_raidRescueCaptureRevivalPerks( self )\n" +
            "\tif not self.sv.saved.isConscious then\n" +
            "\t\treturn\n" +
            "\tend\n" +
            "\tlocal capturedPerks = {}\n" +
            "\tlocal currentPerks = self.sv.saved.stats.perks or {}\n" +
            "\tfor _, perk in pairs( SurvivalPlayer.Perks ) do\n" +
            "\t\tif currentPerks[perk] == true then\n" +
            "\t\t\tcapturedPerks[perk] = true\n" +
            "\t\tend\n" +
            "\tend\n" +
            "\tself.sv.saved.raidRescueRevivalPerks = capturedPerks\n" +
            "end\n\n" +
            "function SurvivalPlayer.sv_raidRescueRestoreRevivalPerks( self )\n" +
            "\tlocal capturedPerks = self.sv.saved.raidRescueRevivalPerks\n" +
            "\tself.sv.saved.raidRescueRevivalPerks = nil\n" +
            "\tlocal restoredPerks = {}\n" +
            "\tif type( capturedPerks ) == \"table\" then\n" +
            "\t\tfor _, perk in pairs( SurvivalPlayer.Perks ) do\n" +
            "\t\t\tif capturedPerks[perk] == true then\n" +
            "\t\t\t\trestoredPerks[perk] = true\n" +
            "\t\t\tend\n" +
            "\t\tend\n" +
            "\tend\n" +
            "\tself.sv.saved.stats.perks = restoredPerks\n" +
            "\tif self.player.publicData then\n" +
            "\t\tself.player.publicData.perks = restoredPerks\n" +
            "\tend\n" +
            "end\n\n" +
            "function SurvivalPlayer.sv_clearPerks( self )";

        public static GamePatchResult GetStatus()
        {
            GamePatchResult result =
                new GamePatchResult
                {
                    Changes = new List<string>()
                };
            try
            {
                string gamePath =
                    GamePatchService.FindGameInstall();
                if (String.IsNullOrEmpty(gamePath))
                    throw new InvalidOperationException(
                        "Scrap Mechanic was not found.");

                result.GamePath = gamePath;
                string executable =
                    Path.Combine(
                        gamePath, "Release",
                        "ScrapMechanic.exe");
                result.GameVersion =
                    FileVersionInfo.GetVersionInfo(
                        executable).FileVersion;
                string path =
                    Path.Combine(
                        gamePath,
                        SurvivalPlayerRelativePath);
                if (!File.Exists(path))
                    throw new FileNotFoundException(
                        "SurvivalPlayer.lua was not found.",
                        path);

                LuaTextDocument document =
                    AdaptivePatchSupport.ReadLua(path);
                SteamBuildInfo build =
                    AdaptivePatchSupport.GetSteamBuild(
                        gamePath, result.GameVersion);
                return FillStatus(
                    result, gamePath, path,
                    document, build);
            }
            catch (Exception exception)
            {
                result.Success = false;
                result.Error = exception.Message;
                return result;
            }
        }

        public static GamePatchResult SetEnabled(
            bool enabled)
        {
            if (GamePatchService.IsGameRunning())
                return Failure(
                    "Scrap Mechanic is running. Close the game completely before changing secret mods.");

            string gamePath =
                GamePatchService.FindGameInstall();
            if (String.IsNullOrEmpty(gamePath))
                return Failure(
                    "Scrap Mechanic was not found.");

            GamePatchResult result =
                SetEnabledAt(
                    gamePath,
                    Path.Combine(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder.LocalApplicationData),
                        "Raid Rescue", "Game Backups",
                        "Scrap Mechanic", "Secret Mods"),
                    enabled);
            return GameScriptCacheInvalidator
                .DeleteAfterChanges(gamePath, result);
        }

        internal static GamePatchResult SetEnabledAt(
            string gamePath, string backupRoot,
            bool enabled)
        {
            GamePatchResult result =
                new GamePatchResult
                {
                    GamePath = gamePath,
                    Installed = enabled,
                    Changes = new List<string>()
                };
            try
            {
                string executable =
                    Path.Combine(
                        gamePath, "Release",
                        "ScrapMechanic.exe");
                if (!File.Exists(executable))
                    throw new FileNotFoundException(
                        "ScrapMechanic.exe was not found.",
                        executable);
                result.GameVersion =
                    FileVersionInfo.GetVersionInfo(
                        executable).FileVersion;

                string path =
                    Path.Combine(
                        gamePath,
                        SurvivalPlayerRelativePath);
                if (!File.Exists(path))
                    throw new FileNotFoundException(
                        "SurvivalPlayer.lua was not found.",
                        path);

                LuaTextDocument document =
                    AdaptivePatchSupport.ReadLua(path);
                AdaptivePatchSupport.RequireAdaptiveFormat(
                    document, "SurvivalPlayer.lua");
                RequireGuards(document.NormalizedText);
                SteamBuildInfo build =
                    AdaptivePatchSupport.GetSteamBuild(
                        gamePath, result.GameVersion);
                bool clean =
                    IsClean(document.NormalizedText);
                bool installed =
                    IsInstalled(document.NormalizedText);

                if (enabled && installed)
                {
                    if (AdaptivePatchSupport
                        .RequiresBuildRefresh(
                            ModKey, build))
                    {
                        AdaptivePatchSupport
                            .PrepareBuildRefresh(
                                result, ModKey, build,
                                "Revival Buff Recovery was reactivated after the Steam update.");
                        return result;
                    }
                    result.Success = true;
                    result.Installed = true;
                    result.AlreadyPatched = true;
                    AdaptivePatchSupport.FillResult(
                        result, build,
                        PatchCompatibilityState
                            .AdaptiveInstalled,
                        true, true,
                        "Revival Buff Recovery is already installed.");
                    result.Changes.Add(
                        "Revival Buff Recovery is already installed.");
                    return result;
                }
                if (!enabled && clean)
                {
                    result.Success = true;
                    result.Installed = false;
                    result.AlreadyPatched = true;
                    AdaptivePatchSupport.DeleteReceipt(
                        ModKey);
                    AdaptivePatchSupport
                        .DeleteBuildActivation(ModKey);
                    AdaptivePatchSupport.FillResult(
                        result, build,
                        IsKnownClean(document.OriginalHash)
                            ? PatchCompatibilityState
                                .KnownClean
                            : PatchCompatibilityState
                                .CompatibleUpdate,
                        !IsKnownClean(
                            document.OriginalHash),
                        true,
                        "Revival Buff Recovery is already removed.");
                    result.Changes.Add(
                        "Revival Buff Recovery is already removed.");
                    return result;
                }

                if (enabled)
                {
                    string reason = "";
                    bool trustedClean =
                        clean &&
                        (IsKnownClean(
                            document.OriginalHash) ||
                         AdaptivePatchSupport
                            .CanAdaptCleanFiles(
                                build,
                                new[] { path },
                                out reason));
                    if (!trustedClean)
                    {
                        throw new InvalidOperationException(
                            "Revival Buff Recovery cannot be applied: " +
                            (String.IsNullOrEmpty(reason)
                                ? "the protected player-death and revival code is missing, duplicated, or edited."
                                : reason));
                    }
                }
                else if (!installed)
                {
                    throw new InvalidOperationException(
                        "Revival Buff Recovery cannot be removed because its protected code is missing, duplicated, or edited.");
                }

                string transformed =
                    enabled
                        ? PatchText(
                            document.NormalizedText)
                        : UnpatchText(
                            document.NormalizedText);
                byte[] outputBytes =
                    document.Render(transformed);
                string outputHash =
                    AdaptivePatchSupport.Sha256(
                        outputBytes);
                string currentHash =
                    document.OriginalHash;

                string stamp =
                    DateTime.Now.ToString(
                        "yyyyMMdd-HHmmss-fff");
                string backupPath =
                    Path.Combine(
                        backupRoot,
                        (enabled
                            ? "Install-"
                            : "Remove-") +
                        ModKey + "-" + stamp);
                Directory.CreateDirectory(
                    backupPath);
                result.BackupPath = backupPath;
                string backupFile =
                    Path.Combine(
                        backupPath,
                        "SurvivalPlayer.lua");
                File.Copy(
                    path, backupFile, false);
                if (!HashEquals(
                    AdaptivePatchSupport.Sha256(
                        backupFile),
                    currentHash))
                {
                    throw new IOException(
                        "The SurvivalPlayer backup failed checksum verification.");
                }

                AdaptivePatchSupport
                    .WriteBackupManifest(
                        backupPath,
                        "Revival Buff Recovery",
                        enabled
                            ? "Install"
                            : "Remove",
                        gamePath, build,
                        DefinitionVersion,
                        new[]
                        {
                            new AdaptivePatchReceiptFile
                            {
                                RelativePath =
                                    SurvivalPlayerRelativePath,
                                SourceHash =
                                    currentHash,
                                OutputHash =
                                    outputHash,
                                Newline =
                                    document.Newline ==
                                    "\r\n"
                                        ? "CRLF"
                                        : "LF",
                                HasBom =
                                    document.HasBom
                            }
                        });

                AdaptivePatchReceipt receipt =
                    AdaptivePatchSupport
                        .LoadReceipt(ModKey);
                AdaptivePatchReceiptFile receiptFile =
                    AdaptivePatchSupport
                        .FindReceiptFile(
                            receipt,
                            SurvivalPlayerRelativePath);
                try
                {
                    if (!enabled &&
                        receiptFile != null &&
                        HashEquals(
                            currentHash,
                            receiptFile.OutputHash) &&
                        File.Exists(
                            receiptFile.BackupPath) &&
                        HashEquals(
                            AdaptivePatchSupport
                                .Sha256(
                                    receiptFile.BackupPath),
                            receiptFile.SourceHash))
                    {
                        AdaptivePatchSupport
                            .ReplaceFile(
                                path,
                                File.ReadAllBytes(
                                    receiptFile.BackupPath),
                                "revival-buffs-exact-restore");
                        outputHash =
                            receiptFile.SourceHash;
                    }
                    else
                    {
                        AdaptivePatchSupport
                            .ReplaceFile(
                                path, outputBytes,
                                "revival-buffs-adaptive");
                    }
                    if (!HashEquals(
                        AdaptivePatchSupport
                            .Sha256(path),
                        outputHash))
                    {
                        throw new IOException(
                            "SurvivalPlayer.lua failed final output verification.");
                    }
                }
                catch
                {
                    File.Copy(
                        backupFile, path, true);
                    if (!HashEquals(
                        AdaptivePatchSupport
                            .Sha256(path),
                        currentHash))
                    {
                        throw new IOException(
                            "Revival Buff Recovery rollback could not restore SurvivalPlayer.lua.");
                    }
                    throw;
                }

                result.Success = true;
                result.Installed = enabled;
                result.FilesPatched = 1;
                AdaptivePatchSupport.FillResult(
                    result, build,
                    enabled
                        ? PatchCompatibilityState
                            .AdaptiveInstalled
                        : PatchCompatibilityState
                            .CompatibleUpdate,
                    !IsKnownClean(currentHash),
                    true,
                    enabled
                        ? "Installed with exact protected-code matching."
                        : "Removed while preserving unrelated game-script changes.");
                result.Changes.Add(
                    enabled
                        ? "Revival Baguettes now restore every pizza and veggie-burger buff held when the player was knocked out."
                        : "Removed Revival Buff Recovery and restored the prior SurvivalPlayer script.");
                result.Changes.Add(
                    enabled
                        ? "Ordinary respawns and forced revivals still clear buffs normally."
                        : "Saved worlds remain compatible; any unused internal recovery snapshot is ignored by the original game.");

                if (enabled)
                {
                    string baseBackup =
                        AdaptivePatchSupport
                            .CaptureBaseBackup(
                                ModKey,
                                SurvivalPlayerRelativePath,
                                backupFile,
                                currentHash);
                    AdaptivePatchSupport
                        .SaveReceipt(
                            ModKey,
                            new AdaptivePatchReceipt
                            {
                                ModKey = ModKey,
                                DefinitionVersion =
                                    DefinitionVersion,
                                SteamBuildId =
                                    build.BuildId,
                                GameVersion =
                                    result.GameVersion,
                                CreatedUtc =
                                    DateTime.UtcNow
                                        .ToString("O"),
                                Files =
                                    new List<
                                        AdaptivePatchReceiptFile>
                                    {
                                        new AdaptivePatchReceiptFile
                                        {
                                            RelativePath =
                                                SurvivalPlayerRelativePath,
                                            SourceHash =
                                                currentHash,
                                            OutputHash =
                                                outputHash,
                                            BackupPath =
                                                baseBackup,
                                            Newline =
                                                document.Newline ==
                                                "\r\n"
                                                    ? "CRLF"
                                                    : "LF",
                                            HasBom =
                                                document.HasBom
                                        }
                                    }
                            });
                }
                else
                {
                    AdaptivePatchSupport
                        .DeleteReceipt(ModKey);
                }

                AdaptivePatchSupport
                    .QueueBuildActivation(
                        result, ModKey,
                        enabled);
                SecretModBackupRetention.Prune(
                    backupRoot, ModKey,
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

        private static GamePatchResult FillStatus(
            GamePatchResult result, string gamePath,
            string path, LuaTextDocument document,
            SteamBuildInfo build)
        {
            if (document.MixedNewlines)
            {
                result.Success = true;
                result.Installed = false;
                AdaptivePatchSupport.FillResult(
                    result, build,
                    PatchCompatibilityState
                        .OtherModification,
                    false, false,
                    "SurvivalPlayer.lua uses mixed newline styles.");
                return result;
            }

            try
            {
                RequireGuards(
                    document.NormalizedText);
            }
            catch (InvalidDataException exception)
            {
                result.Success = true;
                result.Installed = false;
                AdaptivePatchSupport.FillResult(
                    result, build,
                    PatchCompatibilityState
                        .UnsupportedCode,
                    false, false,
                    "SurvivalPlayer.lua changed a required death, respawn, or revival callback. " +
                    exception.Message);
                return result;
            }

            if (IsInstalled(
                document.NormalizedText))
            {
                if (AdaptivePatchSupport
                    .RequiresBuildRefresh(
                        ModKey, build))
                {
                    AdaptivePatchSupport
                        .MarkRefreshRequired(
                            result, build, null);
                    return result;
                }
                result.Success = true;
                result.Installed = true;
                result.AlreadyPatched = true;
                AdaptivePatchSupport.FillResult(
                    result, build,
                    PatchCompatibilityState
                        .AdaptiveInstalled,
                    true, true,
                    "Revival Buff Recovery is structurally intact.");
                return result;
            }

            if (IsClean(document.NormalizedText))
            {
                AdaptivePatchSupport
                    .DiscardReceiptIfSuperseded(
                        ModKey, gamePath);
                string reason = "";
                bool known =
                    IsKnownClean(
                        document.OriginalHash);
                bool canApply =
                    known ||
                    AdaptivePatchSupport
                        .CanAdaptCleanFiles(
                            build,
                            new[] { path },
                            out reason);
                result.Success = true;
                result.Installed = false;
                AdaptivePatchSupport.FillResult(
                    result, build,
                    known
                        ? PatchCompatibilityState
                            .KnownClean
                        : canApply
                            ? PatchCompatibilityState
                                .CompatibleUpdate
                            : PatchCompatibilityState
                                .OtherModification,
                    !known, canApply,
                    known
                        ? "Verified official SurvivalPlayer file."
                        : reason);
                return result;
            }

            bool partial =
                document.NormalizedText
                    .IndexOf(
                        PatchMarker,
                        StringComparison.Ordinal) >= 0 ||
                CountPatchedParts(
                    document.NormalizedText) > 0;
            result.Success = true;
            result.Installed = false;
            AdaptivePatchSupport.FillResult(
                result, build,
                partial
                    ? PatchCompatibilityState
                        .PartialConflict
                    : PatchCompatibilityState
                        .UnsupportedCode,
                false, false,
                partial
                    ? "SurvivalPlayer.lua contains a partial or conflicting Revival Buff Recovery patch."
                    : "The game update changed protected player-death or revival code.");
            return result;
        }

        internal static string PatchText(
            string text)
        {
            string transformed =
                ReplaceUnique(
                    text, OriginalUnstuck,
                    PatchedUnstuck,
                    "unstuck knockout transition");
            transformed =
                ReplaceUnique(
                    transformed,
                    OriginalWarehouseDown,
                    PatchedWarehouseDown,
                    "warehouse knockout transition");
            transformed =
                ReplaceUnique(
                    transformed,
                    OriginalDamageDown,
                    PatchedDamageDown,
                    "damage knockout transition");
            transformed =
                ReplaceUnique(
                    transformed,
                    OriginalRevive,
                    PatchedRevive,
                    "Revival Baguette callback");
            transformed =
                ReplaceUnique(
                    transformed,
                    OriginalRespawnReset,
                    PatchedRespawnReset,
                    "ordinary respawn reset");
            return ReplaceUnique(
                transformed,
                OriginalClearFunction,
                PatchedHelpers,
                "perk-clearing callback");
        }

        internal static string UnpatchText(
            string text)
        {
            string transformed =
                ReplaceUnique(
                    text, PatchedHelpers,
                    OriginalClearFunction,
                    "Revival Buff Recovery helpers");
            transformed =
                ReplaceUnique(
                    transformed,
                    PatchedRespawnReset,
                    OriginalRespawnReset,
                    "ordinary respawn reset");
            transformed =
                ReplaceUnique(
                    transformed,
                    PatchedRevive,
                    OriginalRevive,
                    "Revival Baguette callback");
            transformed =
                ReplaceUnique(
                    transformed,
                    PatchedDamageDown,
                    OriginalDamageDown,
                    "damage knockout transition");
            transformed =
                ReplaceUnique(
                    transformed,
                    PatchedWarehouseDown,
                    OriginalWarehouseDown,
                    "warehouse knockout transition");
            return ReplaceUnique(
                transformed,
                PatchedUnstuck,
                OriginalUnstuck,
                "unstuck knockout transition");
        }

        private static bool IsClean(string text)
        {
            return AdaptivePatchSupport.Count(
                    text, PatchMarker) == 0 &&
                AdaptivePatchSupport.Count(
                    text, OriginalUnstuck) == 1 &&
                AdaptivePatchSupport.Count(
                    text, OriginalWarehouseDown) == 1 &&
                AdaptivePatchSupport.Count(
                    text, OriginalDamageDown) == 1 &&
                AdaptivePatchSupport.Count(
                    text, OriginalRevive) == 1 &&
                AdaptivePatchSupport.Count(
                    text, OriginalRespawnReset) == 1;
        }

        private static bool IsInstalled(
            string text)
        {
            return AdaptivePatchSupport.Count(
                    text, PatchMarker) == 1 &&
                AdaptivePatchSupport.Count(
                    text, PatchedHelpers) == 1 &&
                AdaptivePatchSupport.Count(
                    text, PatchedUnstuck) == 1 &&
                AdaptivePatchSupport.Count(
                    text, PatchedWarehouseDown) == 1 &&
                AdaptivePatchSupport.Count(
                    text, PatchedDamageDown) == 1 &&
                AdaptivePatchSupport.Count(
                    text, PatchedRevive) == 1 &&
                AdaptivePatchSupport.Count(
                    text, PatchedRespawnReset) == 1;
        }

        private static int CountPatchedParts(
            string text)
        {
            return
                AdaptivePatchSupport.Count(
                    text, PatchedHelpers) +
                AdaptivePatchSupport.Count(
                    text, PatchedUnstuck) +
                AdaptivePatchSupport.Count(
                    text, PatchedWarehouseDown) +
                AdaptivePatchSupport.Count(
                    text, PatchedDamageDown) +
                AdaptivePatchSupport.Count(
                    text, PatchedRevive) +
                AdaptivePatchSupport.Count(
                    text, PatchedRespawnReset);
        }

        private static void RequireGuards(
            string text)
        {
            AdaptivePatchSupport.RequireUnique(
                text,
                "function SurvivalPlayer.sv_n_unstuck( self )",
                "unstuck callback");
            AdaptivePatchSupport.RequireUnique(
                text,
                "function SurvivalPlayer.sv_takeDamage( self, damage, source, typeUuid )",
                "damage callback");
            AdaptivePatchSupport.RequireUnique(
                text,
                "function SurvivalPlayer.sv_n_revive( self, params )",
                "revival callback");
            AdaptivePatchSupport.RequireUnique(
                text,
                "function SurvivalPlayer.sv_e_onSpawnCharacter( self )",
                "respawn callback");
            AdaptivePatchSupport.RequireUnique(
                text,
                "function SurvivalPlayer.sv_clearPerks( self )",
                "perk-clearing callback");
        }

        private static string ReplaceUnique(
            string text, string oldText,
            string newText, string description)
        {
            int first =
                text.IndexOf(
                    oldText,
                    StringComparison.Ordinal);
            if (first < 0 ||
                text.IndexOf(
                    oldText,
                    first + oldText.Length,
                    StringComparison.Ordinal) >= 0)
            {
                throw new InvalidDataException(
                    "The expected " + description +
                    " code was not found exactly once.");
            }
            return text.Substring(0, first) +
                newText +
                text.Substring(
                    first + oldText.Length);
        }

        private static bool IsKnownClean(
            string hash)
        {
            return HashEquals(
                hash, KnownCleanHash);
        }

        private static bool HashEquals(
            string left, string right)
        {
            return String.Equals(
                left, right,
                StringComparison.OrdinalIgnoreCase);
        }

        private static GamePatchResult Failure(
            string message)
        {
            return new GamePatchResult
            {
                Success = false,
                Error = message,
                Changes = new List<string>()
            };
        }
    }

#if LEGACY_SELF_HELPERS
    internal static class RevivalBuffPatchLauncher
    {
        public static GamePatchResult SetEnabled(
            bool enabled)
        {
            if (GamePatchService.IsGameRunning())
                return Failure(
                    "Scrap Mechanic is running. Close the game completely before changing secret mods.");

            if (IsAdministrator())
                return RevivalBuffPatchService
                    .SetEnabled(enabled);
            return ElevatedPatchBroker.Execute(
                ElevatedPatchBroker
                    .RevivalBuffsAction,
                enabled, "");
        }

        private static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity identity =
                    WindowsIdentity.GetCurrent();
                WindowsPrincipal principal =
                    new WindowsPrincipal(identity);
                return principal.IsInRole(
                    WindowsBuiltInRole
                        .Administrator);
            }
            catch
            {
                return false;
            }
        }

        private static GamePatchResult Failure(
            string message)
        {
            return new GamePatchResult
            {
                Success = false,
                Error = message,
                Changes = new List<string>()
            };
        }
    }
#endif
}
