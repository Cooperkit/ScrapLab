using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace RaidRescue
{
    internal sealed class PatchInputState
    {
        public string Hash;
        public Func<string, string> Upgrade;
    }

    internal sealed class PatchTarget
    {
        public string RelativePath;
        public string LatestHash;
        public List<PatchInputState> Inputs;
    }

    internal sealed class ChemicalPatchVariant
    {
        public string BaseHash;
        public string PatchedHash;
        public bool RestoreCrLf;
    }

    internal sealed class ChemicalPatchTarget
    {
        public string RelativePath;
        public string DisplayName;
        public Func<string, string> Patch;
        public Func<string, string> Unpatch;
        public List<ChemicalPatchVariant> Variants;
    }

    internal static class SecretModBackupRetention
    {
        private const int CopiesPerAction = 2;

        public static void Prune(
            string backupRoot, string modKey,
            string currentBackupPath, GamePatchResult result)
        {
            try
            {
                if (String.IsNullOrEmpty(backupRoot) ||
                    String.IsNullOrEmpty(modKey) ||
                    !Directory.Exists(backupRoot))
                    return;

                string root = Path.GetFullPath(backupRoot)
                    .TrimEnd(Path.DirectorySeparatorChar);
                string current = String.IsNullOrEmpty(currentBackupPath)
                    ? ""
                    : Path.GetFullPath(currentBackupPath)
                        .TrimEnd(Path.DirectorySeparatorChar);
                Regex allowed = new Regex(
                    "^(Install|Remove|Configure)-" +
                    Regex.Escape(modKey) +
                    "-\\d{8}-\\d{6}-\\d{3}$",
                    RegexOptions.CultureInvariant);
                Dictionary<string, List<DirectoryInfo>> groups =
                    new Dictionary<string, List<DirectoryInfo>>(
                        StringComparer.OrdinalIgnoreCase);

                foreach (DirectoryInfo directory in
                    new DirectoryInfo(root).GetDirectories())
                {
                    Match match = allowed.Match(directory.Name);
                    if (!match.Success ||
                        (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                        continue;
                    string full = Path.GetFullPath(directory.FullName)
                        .TrimEnd(Path.DirectorySeparatorChar);
                    if (!String.Equals(
                        Path.GetDirectoryName(full), root,
                        StringComparison.OrdinalIgnoreCase))
                        continue;

                    string action = match.Groups[1].Value;
                    List<DirectoryInfo> group;
                    if (!groups.TryGetValue(action, out group))
                    {
                        group = new List<DirectoryInfo>();
                        groups[action] = group;
                    }
                    group.Add(directory);
                }

                int removed = 0;
                foreach (List<DirectoryInfo> group in groups.Values)
                {
                    group.Sort(delegate(DirectoryInfo left, DirectoryInfo right)
                    {
                        return StringComparer.OrdinalIgnoreCase.Compare(
                            right.Name, left.Name);
                    });
                    int retained = 0;
                    foreach (DirectoryInfo directory in group)
                    {
                        string full = Path.GetFullPath(directory.FullName)
                            .TrimEnd(Path.DirectorySeparatorChar);
                        if (String.Equals(
                            full, current,
                            StringComparison.OrdinalIgnoreCase) ||
                            retained < CopiesPerAction)
                        {
                            retained++;
                            continue;
                        }
                        Directory.Delete(full, true);
                        removed++;
                    }
                }

                if (removed > 0 && result != null &&
                    result.Changes != null)
                {
                    result.Changes.Add(
                        "Removed " + removed +
                        " superseded secret-mod backup" +
                        (removed == 1 ? "." : "s.") +
                        " The two newest recovery points for each action were retained.");
                }
            }
            catch (Exception exception)
            {
                if (result != null && result.Changes != null)
                {
                    result.Changes.Add(
                        "The mod change succeeded, but older backup cleanup was skipped: " +
                        exception.Message);
                }
            }
        }
    }

    internal static class GameScriptCacheInvalidator
    {
        private static readonly string CoreDataRelativePath =
            Path.Combine("Cache", "Bundle", "core_data.cbo");

        public static GamePatchResult DeleteAfterChanges(
            string gamePath, GamePatchResult result)
        {
            return DeleteAfterChangesCore(
                gamePath, result, true);
        }

        internal static GamePatchResult DeleteAfterChangesForTest(
            string gamePath, GamePatchResult result)
        {
            return DeleteAfterChangesCore(
                gamePath, result, false);
        }

        private static GamePatchResult DeleteAfterChangesCore(
            string gamePath, GamePatchResult result,
            bool requireGameClosed)
        {
            if (result == null || result.FilesPatched <= 0)
                return result;

            try
            {
                if (String.IsNullOrEmpty(gamePath))
                    throw new InvalidOperationException(
                        "The Scrap Mechanic install path is unavailable.");
                if (requireGameClosed &&
                    GamePatchService.IsGameRunning())
                    throw new InvalidOperationException(
                        "Scrap Mechanic started before its script cache could be reset. " +
                        "Close the game and apply the patch change again.");

                string root = Path.GetFullPath(gamePath)
                    .TrimEnd(Path.DirectorySeparatorChar) +
                    Path.DirectorySeparatorChar;
                string expected = Path.GetFullPath(
                    Path.Combine(root, CoreDataRelativePath));
                string cachePath = Path.GetFullPath(
                    Path.Combine(gamePath, CoreDataRelativePath));
                if (!String.Equals(
                    cachePath, expected, StringComparison.OrdinalIgnoreCase) ||
                    !cachePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "The Scrap Mechanic script-cache path failed validation.");
                }

                if (File.Exists(cachePath))
                {
                    FileInfo cache = new FileInfo(cachePath);
                    if ((cache.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new InvalidOperationException(
                            "The Scrap Mechanic script cache is a reparse point and was not deleted.");
                    }
                    File.Delete(cachePath);
                    if (File.Exists(cachePath))
                        throw new IOException("Windows did not delete core_data.cbo.");
                }

                if (result.Changes == null)
                    result.Changes = new List<string>();
                result.Changes.Add(
                    "Reset Scrap Mechanic's generated script cache. " +
                    "It will rebuild automatically on the next normal game launch.");
                AdaptivePatchSupport.CommitBuildActivations(
                    result, gamePath);
                return result;
            }
            catch (Exception exception)
            {
                string message =
                    "ScrapLab could not finish the patch activation. " +
                    "Cache\\Bundle\\core_data.cbo must be reset and the Steam " +
                    "build activation must be recorded before the mod can be shown as active. " +
                    exception.Message;
                if (result.Success)
                {
                    result.Success = false;
                    result.Error = message;
                }
                else
                {
                    result.Error = String.IsNullOrEmpty(result.Error)
                        ? message
                        : result.Error + " " + message;
                }
                return result;
            }
        }
    }

    internal static class GamePatchService
    {
        private const string SupportedVersion = "1.0.2.870";
        private const string RaidManagerOriginal =
            "2593203ED332C622070DFCF717464A0AB2B795CD36B80C7D5BBCE0A5DF9D7263";
        private const string RaidUtilOriginal =
            "08966EF7CC8B2A0C1560DA8EA66B710A95D43D1FCDD418C65BEB7E6FCC131413";
        private const string GrowingOriginal =
            "18FBC73B7267A8C97FA365780F96B6F7672A3271F31842322CD77B565F545D17";
        private const string GrowbedOriginal =
            "3FBAD1E8E0976FE9A387FA29A9C81C091EC8A0B0D9FBE65C188E275A8B30435A";

        private const string RaidManagerPatched =
            "668801C05D57D923FA306AD37B5C1F568A73668B8FF0C4A5C0340FEDF8AFF816";
        private const string RaidUtilPatched =
            "CE8BA9F2DF112CCD7E92A782B0660CCF4DBA993C290097EC1109757108F39A11";
        private const string GrowingRaidPatched =
            "CF156C9B1E5E5181F37B3362D67678BF5CF201D1418B5076B6CDD968C9C580C5";
        private const string GrowingCumulativePatched =
            "A790E7DCF43F7C0B85A2A89323BEE2AD369A363A85B583797847B36612056BE0";
        private const string GrowbedPatched =
            "A186D22A35C032F4E2C694B6DC510417355923E6A44EFB8FA66DB08E49732249";

        public static GamePatchResult Install()
        {
            if (IsGameRunning())
            {
                return Failure(
                    "Scrap Mechanic is running. Close the game completely before installing the hotfix.");
            }

            string gamePath = FindGameInstall();
            if (String.IsNullOrEmpty(gamePath))
            {
                return Failure(
                    "Scrap Mechanic was not found. Install or verify the game through Steam, then try again.");
            }

            string backupRoot = ProductPaths.LocalDataPath(
                "Game Backups", "Scrap Mechanic");
            GamePatchResult result =
                InstallPreservingSecretModsAt(gamePath, backupRoot);
            return GameScriptCacheInvalidator.DeleteAfterChanges(gamePath, result);
        }

        internal static GamePatchResult InstallPreservingSecretModsAt(
            string gamePath, string backupRoot)
        {
            string secretBackupRoot = Path.Combine(backupRoot, "Secret Mods");
            bool restoreChemicalMod;
            bool restoreDualFluidCannon;
            try
            {
                restoreChemicalMod =
                    ChemicalFertilizerPatchService.IsInstalledAt(gamePath);
                restoreDualFluidCannon =
                    DualFluidCannonPatchService.IsInstalledAt(gamePath);
                if (restoreDualFluidCannon && !restoreChemicalMod)
                {
                    return Failure(
                        "Dual-Fluid Water Cannon is installed without its required " +
                        "Chemical Fertilizer Splash dependency. Repair the dependency " +
                        "from Super Secret Mods before updating the standard hotfix.");
                }
            }
            catch (Exception exception)
            {
                return Failure(exception.Message);
            }

            if (restoreDualFluidCannon)
            {
                GamePatchResult removeCannon =
                    DualFluidCannonPatchService.SetEnabledAt(
                        gamePath, secretBackupRoot, false);
                if (!removeCannon.Success)
                {
                    return Failure(
                        "The standard hotfix could not preserve Dual-Fluid Water Cannon: " +
                        removeCannon.Error);
                }
            }

            if (restoreChemicalMod)
            {
                GamePatchResult remove =
                    ChemicalFertilizerPatchService.SetEnabledAt(
                        gamePath, secretBackupRoot, false);
                if (!remove.Success)
                {
                    if (restoreDualFluidCannon)
                    {
                        DualFluidCannonPatchService.SetEnabledAt(
                            gamePath, secretBackupRoot, true);
                    }
                    return Failure(
                        "The standard hotfix could not preserve Chemical Fertilizer Splash: " +
                        remove.Error);
                }
            }

            GamePatchResult install = InstallAt(gamePath, backupRoot);
            if (restoreChemicalMod)
            {
                GamePatchResult restore =
                    ChemicalFertilizerPatchService.SetEnabledAt(
                        gamePath, secretBackupRoot, true);
                if (!restore.Success)
                {
                    return Failure(
                        "The standard hotfix finished, but Chemical Fertilizer Splash could " +
                        "not be restored automatically: " + restore.Error);
                }
                if (install.Success)
                {
                    install.Changes.Add(
                        "Preserved the installed Chemical Fertilizer Splash secret mod.");
                }
            }
            if (restoreDualFluidCannon)
            {
                GamePatchResult restoreCannon =
                    DualFluidCannonPatchService.SetEnabledAt(
                        gamePath, secretBackupRoot, true);
                if (!restoreCannon.Success)
                {
                    return Failure(
                        "The standard hotfix finished, but Dual-Fluid Water Cannon could " +
                        "not be restored automatically: " + restoreCannon.Error);
                }
                if (install.Success)
                {
                    install.Changes.Add(
                        "Preserved the installed Dual-Fluid Water Cannon secret mod.");
                }
            }
            return install;
        }

        internal static GamePatchResult InstallAt(string gamePath, string backupRoot)
        {
            GamePatchResult result = new GamePatchResult
            {
                GamePath = gamePath,
                Changes = new List<string>()
            };

            try
            {
                string executable = Path.Combine(gamePath, "Release", "ScrapMechanic.exe");
                if (!File.Exists(executable))
                    throw new FileNotFoundException("ScrapMechanic.exe was not found.", executable);

                result.GameVersion = FileVersionInfo.GetVersionInfo(executable).FileVersion;
                if (!String.Equals(result.GameVersion, SupportedVersion, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "This hotfix supports Scrap Mechanic " + SupportedVersion +
                        " only. Installed version: " + (result.GameVersion ?? "(unknown)") +
                        ". The game may already contain an official fix.");
                }

                List<PatchTarget> targets = GetTargets();
                Dictionary<PatchTarget, string> paths = new Dictionary<PatchTarget, string>();
                Dictionary<PatchTarget, string> currentHashes = new Dictionary<PatchTarget, string>();
                Dictionary<PatchTarget, Func<string, string>> upgrades =
                    new Dictionary<PatchTarget, Func<string, string>>();
                List<PatchTarget> targetsToPatch = new List<PatchTarget>();

                foreach (PatchTarget target in targets)
                {
                    string path = Path.Combine(gamePath, target.RelativePath);
                    if (!File.Exists(path))
                        throw new FileNotFoundException("A required Scrap Mechanic script is missing.", path);

                    string hash = Sha256(path);
                    paths[target] = path;
                    currentHashes[target] = hash;
                    if (HashEquals(hash, target.LatestHash))
                        continue;

                    PatchInputState input = null;
                    foreach (PatchInputState candidate in target.Inputs)
                    {
                        if (HashEquals(hash, candidate.Hash))
                        {
                            input = candidate;
                            break;
                        }
                    }
                    if (input == null)
                    {
                        throw new InvalidOperationException(
                            "The installed " + Path.GetFileName(target.RelativePath) +
                            " does not match any verified Scrap Mechanic 1.0.2 or previous " +
                            "ScrapLab state. No files were changed. Use Steam's Verify " +
                            "integrity feature if you need to restore the official scripts.");
                    }
                    targetsToPatch.Add(target);
                    upgrades[target] = input.Upgrade;
                }

                if (targetsToPatch.Count == 0)
                {
                    result.Success = true;
                    result.AlreadyPatched = true;
                    result.Changes.Add(
                        "The latest cumulative ScrapLab 1.0.2 hotfix is already installed.");
                    return result;
                }

                Dictionary<PatchTarget, string> patchedText =
                    new Dictionary<PatchTarget, string>();
                Dictionary<PatchTarget, string> patchedHashes =
                    new Dictionary<PatchTarget, string>();
                foreach (PatchTarget target in targetsToPatch)
                {
                    string source = ReadUtf8(paths[target]);
                    string transformed = upgrades[target](NormalizeNewlines(source));
                    patchedText[target] = transformed;
                    patchedHashes[target] = Sha256(Encoding.UTF8.GetBytes(transformed));
                    if (!HashEquals(patchedHashes[target], target.LatestHash))
                    {
                        throw new InvalidOperationException(
                            "The generated hotfix did not match its verified checksum.");
                    }
                }

                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
                string backupPath = Path.Combine(
                    backupRoot, "ScrapMechanic-" + SupportedVersion + "-" + stamp);
                Directory.CreateDirectory(backupPath);
                result.BackupPath = backupPath;

                foreach (PatchTarget target in targetsToPatch)
                {
                    string backupFile = Path.Combine(
                        backupPath, Path.GetFileName(target.RelativePath));
                    File.Copy(paths[target], backupFile, false);
                    if (!HashEquals(Sha256(backupFile), currentHashes[target]))
                        throw new IOException("A game-script backup failed checksum verification.");
                }
                WriteManifest(
                    backupPath, gamePath, result.GameVersion,
                    targetsToPatch, currentHashes);

                List<PatchTarget> replaced = new List<PatchTarget>();
                try
                {
                    foreach (PatchTarget target in targetsToPatch)
                    {
                        ReplaceFile(paths[target], patchedText[target]);
                        replaced.Add(target);
                        if (!HashEquals(Sha256(paths[target]), patchedHashes[target]))
                            throw new IOException(
                                "A patched game script failed checksum verification.");
                    }
                }
                catch
                {
                    RollBack(replaced, paths, backupPath, currentHashes);
                    throw;
                }

                result.Success = true;
                result.FilesPatched = targetsToPatch.Count;
                result.Changes.Add("Fixed short and missing raid spawn-path handling.");
                result.Changes.Add("Added bounded pathfinding retries with safe raid cancellation.");
                result.Changes.Add("Fixed crop-survival state after reloading a world.");
                result.Changes.Add("Made empty crop-position cleanup authoritative.");
                result.Changes.Add("Added complete raid handle cleanup and immediate persistence.");
                result.Changes.Add("Synchronized fertilizer growth timing on server and client.");
                result.Changes.Add("Made fertilized crops mature immediately after surviving their raid.");
                return result;
            }
            catch (Exception exception)
            {
                result.Success = false;
                result.Error = exception.Message;
                return result;
            }
        }

        private static List<PatchTarget> GetTargets()
        {
            return new List<PatchTarget>
            {
                new PatchTarget
                {
                    RelativePath = Path.Combine(
                        "Survival", "Scripts", "game", "managers", "RaidManager.lua"),
                    LatestHash = RaidManagerPatched,
                    Inputs = new List<PatchInputState>
                    {
                        Input(RaidManagerOriginal, PatchRaidManager)
                    }
                },
                new PatchTarget
                {
                    RelativePath = Path.Combine(
                        "Survival", "Scripts", "game", "raid_util.lua"),
                    LatestHash = RaidUtilPatched,
                    Inputs = new List<PatchInputState>
                    {
                        Input(RaidUtilOriginal, PatchRaidUtil)
                    }
                },
                new PatchTarget
                {
                    RelativePath = Path.Combine(
                        "Survival", "Scripts", "game", "harvestable", "GrowingHarvestable.lua"),
                    LatestHash = GrowingCumulativePatched,
                    Inputs = new List<PatchInputState>
                    {
                        Input(GrowingOriginal, PatchGrowingCumulative),
                        Input(GrowingRaidPatched, PatchGrowingFertilizer)
                    }
                },
                new PatchTarget
                {
                    RelativePath = Path.Combine(
                        "Survival", "Scripts", "game", "interactables", "Growbed.lua"),
                    LatestHash = GrowbedPatched,
                    Inputs = new List<PatchInputState>
                    {
                        Input(GrowbedOriginal, PatchGrowbedFertilizer)
                    }
                }
            };
        }

        private static PatchInputState Input(
            string hash, Func<string, string> upgrade)
        {
            return new PatchInputState { Hash = hash, Upgrade = upgrade };
        }

        private static string PatchRaidUtil(string text)
        {
            const string oldText =
                "    if #selectedMainSpawns > 0 and #selectedMainSpawns < RAID_TARGET_POINT_COUNT then\n" +
                "        for i = #selectedMainSpawns + 1, RAID_TARGET_POINT_COUNT do\n" +
                "            selectedMainSpawns[#selectedMainSpawns+1].path = selectedMainSpawns[i -1].path\n" +
                "        end\n" +
                "    end";
            const string newText =
                "    -- RAID RESCUE HOTFIX 1.0.2: create padded entries before assigning fields.\n" +
                "    if #selectedMainSpawns > 0 and #selectedMainSpawns < RAID_TARGET_POINT_COUNT then\n" +
                "        for i = #selectedMainSpawns + 1, RAID_TARGET_POINT_COUNT do\n" +
                "            local source = selectedMainSpawns[i - 1]\n" +
                "            selectedMainSpawns[#selectedMainSpawns+1] = { path = source.path, material = source.material }\n" +
                "        end\n" +
                "    end";
            return ReplaceUnique(text, oldText, newText, "raid spawn-list padding");
        }

        private static string PatchGrowingCumulative(string text)
        {
            return PatchGrowingFertilizer(PatchGrowingRaidSurvival(text));
        }

        private static string PatchGrowingRaidSurvival(string text)
        {
            const string oldText =
                "\tself.sv.saved.hasSurvivedRaid = self.sv.saved.hasSurvivedRaid or true -- let old plants act as they have survived a raid";
            const string newText =
                "\t-- RAID RESCUE HOTFIX 1.0.2: preserve a legitimate saved false value.\n" +
                "\tif self.sv.saved.hasSurvivedRaid == nil then\n" +
                "\t\tself.sv.saved.hasSurvivedRaid = true -- let old plants act as they have survived a raid\n" +
                "\tend";
            return ReplaceUnique(text, oldText, newText, "crop raid-survival state");
        }

        private static string PatchGrowingFertilizer(string text)
        {
            text = ReplaceUnique(
                text,
                "local WetStepTime = WaterRetentionTickTime / MaxSoilFrame\n\n" +
                "local IgnoreProjectiles",
                "local WetStepTime = WaterRetentionTickTime / MaxSoilFrame\n" +
                "-- RAID RESCUE HOTFIX 1.0.2: use one authoritative fertilizer rate on server and client.\n" +
                "local FertilizerGrowthMultiplier = 20\n\n" +
                "local IgnoreProjectiles",
                "ground-crop fertilizer multiplier");

            text = ReplaceUnique(
                text,
                "function GrowingHarvestable.sv_e_raidSurvived( self )\n" +
                "\tself.sv.saved.hasSurvivedRaid = true\n" +
                "\tself.storage:save( self.sv.saved )\n" +
                "end",
                "function GrowingHarvestable.sv_e_raidSurvived( self )\n" +
                "\tself.sv.saved.hasSurvivedRaid = true\n" +
                "\tself.storage:save( self.sv.saved )\n" +
                "\t-- Finish immediately if fertilizer already completed the growth timer.\n" +
                "\tself:sv_tryFinishGrowing()\n" +
                "end",
                "post-raid crop completion");

            const string oldServerGrowth =
                "function GrowingHarvestable.server_onReceiveUpdate( self )\n" +
                "\tif not self.sv.harvested and sm.exists( self.harvestable ) then\n" +
                "\t\tlocal currentTick = sm.game.getCurrentTick()\n" +
                "\t\tlocal fertilizeTicks = ( self.sv.saved.fertilizeTick and self.sv.saved.growStartTick ) and ( currentTick - math.max( self.sv.saved.fertilizeTick, self.sv.saved.growStartTick ) ) or 0\n" +
                "\t\tlocal growTicks = self.sv.saved.growStartTick and ( currentTick - self.sv.saved.growStartTick + fertilizeTicks * 14 ) or 0\n" +
                "\t\tlocal growTickTime = DAYCYCLE_TIME_TICKS * ( self.data and self.data.daysToGrow or 0.875 )\n" +
                "\t\tif growTicks >= growTickTime and self.sv.saved.hasSurvivedRaid then\n" +
                "\t\t\tself:sv_done()\n" +
                "\t\t\treturn\n" +
                "\t\tend\n" +
                "\tend\n" +
                "\tif WeatherManager.Sv_IsRaining() then";
            const string newServerGrowth =
                "function GrowingHarvestable.sv_tryFinishGrowing( self )\n" +
                "\tif not self.sv.harvested and sm.exists( self.harvestable ) then\n" +
                "\t\tlocal currentTick = sm.game.getCurrentTick()\n" +
                "\t\tlocal fertilizeTicks = ( self.sv.saved.fertilizeTick and self.sv.saved.growStartTick ) and ( currentTick - math.max( self.sv.saved.fertilizeTick, self.sv.saved.growStartTick ) ) or 0\n" +
                "\t\tlocal growTicks = self.sv.saved.growStartTick and ( currentTick - self.sv.saved.growStartTick + fertilizeTicks * ( FertilizerGrowthMultiplier - 1 ) ) or 0\n" +
                "\t\tlocal growTickTime = DAYCYCLE_TIME_TICKS * ( self.data and self.data.daysToGrow or 0.875 )\n" +
                "\t\tif growTicks >= growTickTime and self.sv.saved.hasSurvivedRaid then\n" +
                "\t\t\tself:sv_done()\n" +
                "\t\t\treturn true\n" +
                "\t\tend\n" +
                "\tend\n" +
                "\treturn false\n" +
                "end\n\n" +
                "function GrowingHarvestable.server_onReceiveUpdate( self )\n" +
                "\tif self:sv_tryFinishGrowing() then\n" +
                "\t\treturn\n" +
                "\tend\n" +
                "\tif WeatherManager.Sv_IsRaining() then";
            text = ReplaceUnique(
                text, oldServerGrowth, newServerGrowth,
                "authoritative ground-crop growth");

            return ReplaceUnique(
                text,
                "\tlocal growTicks = self.cl.growStartTick and ( serverTick - self.cl.growStartTick + fertilizeTicks * 20 ) or 0",
                "\tlocal growTicks = self.cl.growStartTick and ( serverTick - self.cl.growStartTick + fertilizeTicks * ( FertilizerGrowthMultiplier - 1 ) ) or 0",
                "ground-crop client growth");
        }

        private static string PatchGrowbedFertilizer(string text)
        {
            text = ReplaceUnique(
                text,
                "local TimeStep = 0.025\n\n" +
                "-- Server",
                "local TimeStep = 0.025\n" +
                "-- RAID RESCUE HOTFIX 1.0.2: keep growbed visuals synchronized with the server.\n" +
                "local FertilizerGrowthMultiplier = 20\n\n" +
                "-- Server",
                "growbed fertilizer multiplier");

            text = ReplaceUnique(
                text,
                "\t\t\tself.sv.saved.growTicks = math.min( self.sv.saved.growTicks + elapsedActiveTicks * ( self.sv.saved.fertilizer and 20 or 1 ), data.growTickTime )",
                "\t\t\tself.sv.saved.growTicks = math.min( self.sv.saved.growTicks + elapsedActiveTicks * ( self.sv.saved.fertilizer and FertilizerGrowthMultiplier or 1 ), data.growTickTime )",
                "growbed server growth");

            return ReplaceUnique(
                text,
                "\t\t\t\t\tself.cl.growTicks = math.min( self.cl.growTicks + ( self.cl.fertilizer and 15 or 1 ), data.growTickTime )",
                "\t\t\t\t\tself.cl.growTicks = math.min( self.cl.growTicks + ( self.cl.fertilizer and FertilizerGrowthMultiplier or 1 ), data.growTickTime )",
                "growbed client growth");
        }

        private static string PatchRaidManager(string text)
        {
            text = ReplaceUnique(
                text,
                "\tif IsEmptyTable( raid.existingCrops ) then",
                "\t-- RAID RESCUE HOTFIX 1.0.2: object references can become stale after crop replacement.\n" +
                "\tif IsEmptyTable( raid.cropPositions ) then",
                "empty-crop cleanup");

            text = ReplaceUnique(
                text,
                "\tfor raidKey, raid in pairs( raids ) do\n\t\tif HasNearbyPlayer( raid.center ) then",
                "\tfor raidKey, raid in pairs( raids ) do\n" +
                "\t\t-- RAID RESCUE HOTFIX 1.0.2: recover saved raids whose crops no longer exist.\n" +
                "\t\tif raid.cropPositions == nil or IsEmptyTable( raid.cropPositions ) then\n" +
                "\t\t\tself:sv_failRaid( raid, worldId, raidKey )\n" +
                "\t\t\tself.sv.isDirty = true\n" +
                "\t\t\tself.sv.synchToClients = true\n" +
                "\t\telseif HasNearbyPlayer( raid.center ) then",
                "saved empty-raid recovery");

            text = ReplaceUnique(
                text,
                "\t\t\tif raid.attackData.groupSpawns and raid.attackData.spawnPositions then",
                "\t\t\tif raid.attackData.groupSpawns and raid.attackData.spawnPositions and #raid.attackData.spawnPositions > 0 then",
                "empty spawn-position guard");

            const string oldGeneration =
                "\t\t\t\tif self.sv.raidPaths[raidKey] and self.sv.raidsPathGenerationData[raidKey] then\n" +
                "\t\t\t\t\tself.sv.raidPaths[raidKey][#self.sv.raidPaths[raidKey]+1] = CreateRaidPath( raid.center, world, self.sv.raidsPathGenerationData[raidKey].rotation, self.sv.raidsPathGenerationData[raidKey].currentIndex )\n" +
                "\t\t\t\t\tself.sv.raidsPathGenerationData[raidKey].currentIndex = self.sv.raidsPathGenerationData[raidKey].currentIndex + 1\n" +
                "\t\t\t\t\tif #self.sv.raidPaths[raidKey] == RAID_SAMPLE_POINT_COUNT then\n" +
                "\t\t\t\t\t\traid.attackData.spawnPositions = FilterAndSelectPoints( self.sv.raidPaths[raidKey], world, raid.center )\n" +
                "\t\t\t\t\t\tshuffle( raid.attackData.spawnPositions )\n" +
                "\t\t\t\t\t\traid.needsSpawnPoints = false\n" +
                "\t\t\t\t\t\tself.sv.isDirty = true\n" +
                "\t\t\t\t\t\tself.sv.synchToClients = true\n\n" +
                "\t\t\t\t\t\tfor _,handle in ipairs( self.sv.navmeshHandles[raidKey] ) do\n" +
                "\t\t\t\t\t\t\thandle:release()\n" +
                "\t\t\t\t\t\tend\n" +
                "\t\t\t\t\t\tself.sv.navmeshHandles[raidKey] = nil\n" +
                "\t\t\t\t\t\tself.sv.raidPaths[raidKey] = nil\n" +
                "\t\t\t\t\t\tself.sv.raidsPathGenerationData[raidKey] = nil\n" +
                "\t\t\t\t\tend\n" +
                "\t\t\t\tend";
            const string newGeneration =
                "\t\t\t\tif self.sv.raidPaths[raidKey] and self.sv.raidsPathGenerationData[raidKey] then\n" +
                "\t\t\t\t\t-- RAID RESCUE HOTFIX 1.0.2: count attempts, retain only valid paths, and stop retrying forever.\n" +
                "\t\t\t\t\tlocal generationData = self.sv.raidsPathGenerationData[raidKey]\n" +
                "\t\t\t\t\tlocal raidPath = CreateRaidPath( raid.center, world, generationData.rotation, generationData.currentIndex )\n" +
                "\t\t\t\t\tif raidPath then\n" +
                "\t\t\t\t\t\tself.sv.raidPaths[raidKey][#self.sv.raidPaths[raidKey]+1] = raidPath\n" +
                "\t\t\t\t\tend\n" +
                "\t\t\t\t\tgenerationData.currentIndex = generationData.currentIndex + 1\n" +
                "\t\t\t\t\tlocal enoughPaths = #self.sv.raidPaths[raidKey] >= RAID_SAMPLE_POINT_COUNT\n" +
                "\t\t\t\t\tlocal attemptsComplete = generationData.currentIndex > RAID_SAMPLE_POINT_COUNT * 3\n" +
                "\t\t\t\t\tif enoughPaths or attemptsComplete then\n" +
                "\t\t\t\t\t\tlocal spawnPositions = {}\n" +
                "\t\t\t\t\t\tif #self.sv.raidPaths[raidKey] > 0 then\n" +
                "\t\t\t\t\t\t\tspawnPositions = FilterAndSelectPoints( self.sv.raidPaths[raidKey], world, raid.center )\n" +
                "\t\t\t\t\t\tend\n" +
                "\t\t\t\t\t\tif #spawnPositions > 0 then\n" +
                "\t\t\t\t\t\t\traid.attackData.spawnPositions = spawnPositions\n" +
                "\t\t\t\t\t\t\tshuffle( raid.attackData.spawnPositions )\n" +
                "\t\t\t\t\t\t\traid.needsSpawnPoints = false\n" +
                "\t\t\t\t\t\telse\n" +
                "\t\t\t\t\t\t\tself:sv_failRaid( raid, worldId, raidKey )\n" +
                "\t\t\t\t\t\tend\n" +
                "\t\t\t\t\t\tself.sv.isDirty = true\n" +
                "\t\t\t\t\t\tself.sv.synchToClients = true\n\n" +
                "\t\t\t\t\t\tif self.sv.navmeshHandles[raidKey] then\n" +
                "\t\t\t\t\t\t\tfor _,handle in ipairs( self.sv.navmeshHandles[raidKey] ) do\n" +
                "\t\t\t\t\t\t\t\thandle:release()\n" +
                "\t\t\t\t\t\t\tend\n" +
                "\t\t\t\t\t\tend\n" +
                "\t\t\t\t\t\tself.sv.navmeshHandles[raidKey] = nil\n" +
                "\t\t\t\t\t\tself.sv.finishedNavMeshLoads[raidKey] = nil\n" +
                "\t\t\t\t\t\tself.sv.raidPaths[raidKey] = nil\n" +
                "\t\t\t\t\t\tself.sv.raidsPathGenerationData[raidKey] = nil\n" +
                "\t\t\t\t\tend\n" +
                "\t\t\t\tend";
            text = ReplaceUnique(
                text, oldGeneration, newGeneration, "bounded raid path generation");

            const string oldCleanup =
                "\tif raidKey then\n" +
                "\t\tself.sv.saved.worldRaids[worldId][raidKey] = nil\n" +
                "\t\tself.sv.synchToClients = true\n" +
                "\t\tif self.sv.activeRaidLoadHandles[raidKey] then\n" +
                "\t\t\tfor _,handle in ipairs( self.sv.activeRaidLoadHandles[raidKey] ) do\n" +
                "\t\t\t\thandle:release()\n" +
                "\t\t\tend\n" +
                "\t\t\tself.sv.activeRaidLoadHandles[raidKey] = nil\n" +
                "\t\tend\n" +
                "\tend";
            const string newCleanup =
                "\tif raidKey then\n" +
                "\t\tself.sv.saved.worldRaids[worldId][raidKey] = nil\n" +
                "\t\tself.sv.synchToClients = true\n" +
                "\t\tself.sv.isDirty = true\n" +
                "\t\tif self.sv.activeRaidLoadHandles[raidKey] then\n" +
                "\t\t\tfor _,handle in ipairs( self.sv.activeRaidLoadHandles[raidKey] ) do\n" +
                "\t\t\t\thandle:release()\n" +
                "\t\t\tend\n" +
                "\t\t\tself.sv.activeRaidLoadHandles[raidKey] = nil\n" +
                "\t\tend\n" +
                "\t\t-- RAID RESCUE HOTFIX 1.0.2: release unfinished path-generation resources too.\n" +
                "\t\tif self.sv.navmeshHandles[raidKey] then\n" +
                "\t\t\tfor _,handle in ipairs( self.sv.navmeshHandles[raidKey] ) do\n" +
                "\t\t\t\thandle:release()\n" +
                "\t\t\tend\n" +
                "\t\t\tself.sv.navmeshHandles[raidKey] = nil\n" +
                "\t\tend\n" +
                "\t\tself.sv.finishedNavMeshLoads[raidKey] = nil\n" +
                "\t\tself.sv.raidPaths[raidKey] = nil\n" +
                "\t\tself.sv.raidsPathGenerationData[raidKey] = nil\n" +
                "\tend";
            return ReplaceUnique(
                text, oldCleanup, newCleanup, "complete raid resource cleanup");
        }

        private static string ReplaceUnique(
            string text, string oldText, string newText, string description)
        {
            int first = text.IndexOf(oldText, StringComparison.Ordinal);
            if (first < 0 ||
                text.IndexOf(oldText, first + oldText.Length, StringComparison.Ordinal) >= 0)
            {
                throw new InvalidDataException(
                    "The expected " + description + " code was not found exactly once.");
            }
            return text.Substring(0, first) + newText +
                text.Substring(first + oldText.Length);
        }

        private static void ReplaceFile(string path, string text)
        {
            string temporary = path + ".raidrescue-" +
                Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, text, new UTF8Encoding(false));
                File.Replace(temporary, path, null);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }

        private static void RollBack(
            List<PatchTarget> replaced,
            Dictionary<PatchTarget, string> paths,
            string backupPath,
            Dictionary<PatchTarget, string> originalHashes)
        {
            List<string> failures = new List<string>();
            foreach (PatchTarget target in replaced)
            {
                try
                {
                    string backup = Path.Combine(
                        backupPath, Path.GetFileName(target.RelativePath));
                    File.Copy(backup, paths[target], true);
                    if (!HashEquals(Sha256(paths[target]), originalHashes[target]))
                        failures.Add(Path.GetFileName(paths[target]));
                }
                catch
                {
                    failures.Add(Path.GetFileName(paths[target]));
                }
            }
            if (failures.Count > 0)
            {
                throw new IOException(
                    "The hotfix failed and automatic rollback could not restore: " +
                    String.Join(", ", failures.ToArray()) +
                    ". The backed-up files remain in " + backupPath);
            }
        }

        private static void WriteManifest(
            string backupPath, string gamePath, string version,
            List<PatchTarget> targets,
            Dictionary<PatchTarget, string> originalHashes)
        {
            StringBuilder manifest = new StringBuilder();
            manifest.AppendLine("ScrapLab game-script backup");
            manifest.AppendLine("Game path: " + gamePath);
            manifest.AppendLine("Game version: " + version);
            manifest.AppendLine("Created: " + DateTime.Now.ToString("O"));
            manifest.AppendLine();
            foreach (PatchTarget target in targets)
            {
                manifest.AppendLine(
                    Path.GetFileName(target.RelativePath) + " SHA-256 " +
                    originalHashes[target]);
            }
            File.WriteAllText(
                Path.Combine(backupPath, "MANIFEST.txt"),
                manifest.ToString(), new UTF8Encoding(false));
        }

        internal static string FindGameInstall()
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
                programFilesX86, "Steam", "steamapps", "common", "Scrap Mechanic"));
            candidates.Add(Path.Combine(
                programFiles, "Steam", "steamapps", "common", "Scrap Mechanic"));

            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady)
                    continue;
                candidates.Add(Path.Combine(
                    drive.RootDirectory.FullName,
                    "SteamLibrary", "steamapps", "common", "Scrap Mechanic"));
                candidates.Add(Path.Combine(
                    drive.RootDirectory.FullName,
                    "Steam", "steamapps", "common", "Scrap Mechanic"));
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
                if (File.Exists(Path.Combine(full, "Release", "ScrapMechanic.exe")) &&
                    File.Exists(Path.Combine(
                        full, "Survival", "Scripts", "game", "managers", "RaidManager.lua")))
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
                    string library = match.Groups[1].Value.Replace(@"\\", @"\");
                    candidates.Add(Path.Combine(
                        library, "steamapps", "common", "Scrap Mechanic"));
                }
            }
            catch { }
        }

        internal static bool IsGameRunning()
        {
            try
            {
                return Process.GetProcessesByName("ScrapMechanic").Length > 0 ||
                    Process.GetProcessesByName("ScrapMechanicServer").Length > 0;
            }
            catch
            {
                return true;
            }
        }

        private static string ReadUtf8(string path)
        {
            return File.ReadAllText(path, new UTF8Encoding(false, true));
        }

        private static string NormalizeNewlines(string value)
        {
            return value.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        private static string Sha256(string path)
        {
            using (FileStream stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (SHA256 algorithm = SHA256.Create())
                return BytesToHex(algorithm.ComputeHash(stream));
        }

        private static string Sha256(byte[] value)
        {
            using (SHA256 algorithm = SHA256.Create())
                return BytesToHex(algorithm.ComputeHash(value));
        }

        private static string BytesToHex(byte[] value)
        {
            StringBuilder text = new StringBuilder(value.Length * 2);
            foreach (byte item in value)
                text.Append(item.ToString("X2"));
            return text.ToString();
        }

        private static bool HashEquals(string left, string right)
        {
            return String.Equals(left, right, StringComparison.OrdinalIgnoreCase);
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

    internal static class SecretModPatchService
    {
        private const string HarvestCoreOriginal =
            "EB6C84050CC50798A20860943F0D79CCD9708F71F2C9E8595CF1C3BB4E4A8EAE";
        private const string HarvestCoreLocatorV1 =
            "69910ED73E274E08C629FC69346A79CADCB472A7BCF4390D43D7AF1D2D4E2963";
        private const string HarvestCoreLocatorV2 =
            "DEA5674B50316FE245FF6A2290805A99A383E0C025839E22D16AD58DE91BB462";
        private static readonly string HarvestCoreRelativePath = Path.Combine(
            "Survival", "Scripts", "game", "harvestable", "HarvestCore.lua");

        private const string OriginalDeclaration =
            "HarvestCore = class( nil )\n\n" +
            "local RefineTime = 2.8";
        private const string LocatorV1Declaration =
            "HarvestCore = class( nil )\n" +
            "-- RAID RESCUE SECRET MOD: expose a locator-only logic point to the Connect Tool.\n" +
            "HarvestCore.connectionInput = sm.interactable.connectionType.none\n" +
            "HarvestCore.connectionOutput = sm.interactable.connectionType.logic\n" +
            "HarvestCore.maxParentCount = 0\n" +
            "HarvestCore.maxChildCount = 0 -- Marker only; resource cores cannot be wired into creations.\n\n" +
            "local RefineTime = 2.8";
        private const string LocatorV2Declaration =
            "HarvestCore = class( nil )\n" +
            "-- RAID RESCUE SECRET MOD: one inactive output slot makes the locator dot visible.\n" +
            "HarvestCore.maxParentCount = 0\n" +
            "HarvestCore.maxChildCount = 1\n" +
            "HarvestCore.connectionInput = sm.interactable.connectionType.none\n" +
            "HarvestCore.connectionOutput = sm.interactable.connectionType.logic\n" +
            "HarvestCore.colorNormal = sm.color.new( 0x777777ff )\n" +
            "HarvestCore.colorHighlight = sm.color.new( 0x888888ff )\n\n" +
            "local RefineTime = 2.8";

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
                    throw new InvalidOperationException("Scrap Mechanic was not found.");

                result.GamePath = gamePath;
                string executable = Path.Combine(gamePath, "Release", "ScrapMechanic.exe");
                result.GameVersion = FileVersionInfo.GetVersionInfo(executable).FileVersion;
                string path = Path.Combine(gamePath, HarvestCoreRelativePath);
                if (!File.Exists(path))
                    throw new FileNotFoundException("HarvestCore.lua was not found.", path);

                string hash = Sha256(path);
                if (HashEquals(hash, HarvestCoreLocatorV2))
                {
                    SteamBuildInfo build =
                        AdaptivePatchSupport.GetSteamBuild(
                            gamePath, result.GameVersion);
                    if (AdaptivePatchSupport.RequiresBuildRefresh(
                        "ResourceLocator", build))
                    {
                        AdaptivePatchSupport.MarkRefreshRequired(
                            result, build, null);
                        return result;
                    }
                    result.Success = true;
                    result.Installed = true;
                    result.AlreadyPatched = true;
                    AdaptivePatchSupport.FillResult(
                        result, build,
                        PatchCompatibilityState.KnownInstalled,
                        false, true, "Verified ScrapLab file.");
                    return result;
                }
                if (HashEquals(hash, HarvestCoreLocatorV1))
                {
                    result.Success = true;
                    result.Installed = true;
                    result.NeedsUpdate = true;
                    result.Changes.Add(
                        "The older Resource Locator Dots patch is installed and needs the visibility fix.");
                    AdaptivePatchSupport.FillResult(
                        result,
                        AdaptivePatchSupport.GetSteamBuild(
                            gamePath, result.GameVersion),
                        PatchCompatibilityState.KnownInstalled,
                        false, true, "Verified older ScrapLab file.");
                    return result;
                }
                if (HashEquals(hash, HarvestCoreOriginal))
                {
                    AdaptivePatchSupport.DiscardReceiptIfSuperseded(
                        "ResourceLocator", gamePath);
                    result.Success = true;
                    result.Installed = false;
                    AdaptivePatchSupport.FillResult(
                        result,
                        AdaptivePatchSupport.GetSteamBuild(
                            gamePath, result.GameVersion),
                        PatchCompatibilityState.KnownClean,
                        false, true, "Verified official file.");
                    return result;
                }
                return GetAdaptiveStatus(
                    result, gamePath, path);
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
            {
                return Failure(
                    "Scrap Mechanic is running. Close the game completely before changing secret mods.");
            }

            string gamePath = GamePatchService.FindGameInstall();
            if (String.IsNullOrEmpty(gamePath))
                return Failure("Scrap Mechanic was not found.");

            GamePatchResult result = SetEnabledAt(
                gamePath,
                ProductPaths.LocalDataPath(
                    "Game Backups", "Scrap Mechanic", "Secret Mods"),
                enabled);
            return GameScriptCacheInvalidator.DeleteAfterChanges(gamePath, result);
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
                string executable = Path.Combine(gamePath, "Release", "ScrapMechanic.exe");
                if (!File.Exists(executable))
                    throw new FileNotFoundException("ScrapMechanic.exe was not found.", executable);

                result.GameVersion = FileVersionInfo.GetVersionInfo(executable).FileVersion;
                string path = Path.Combine(gamePath, HarvestCoreRelativePath);
                if (!File.Exists(path))
                    throw new FileNotFoundException("HarvestCore.lua was not found.", path);

                string currentHash = Sha256(path);
                string desiredHash = enabled ? HarvestCoreLocatorV2 : HarvestCoreOriginal;
                if (HashEquals(currentHash, desiredHash))
                {
                    SteamBuildInfo build =
                        AdaptivePatchSupport.GetSteamBuild(
                            gamePath, result.GameVersion);
                    if (enabled &&
                        AdaptivePatchSupport.RequiresBuildRefresh(
                            "ResourceLocator", build))
                    {
                        AdaptivePatchSupport.PrepareBuildRefresh(
                            result, "ResourceLocator", build,
                            "Resource Locator Dots were reactivated after the Steam update.");
                        return result;
                    }
                    result.Success = true;
                    result.AlreadyPatched = true;
                    if (!enabled)
                        AdaptivePatchSupport.DeleteBuildActivation(
                            "ResourceLocator");
                    result.Changes.Add(
                        enabled
                            ? "Resource locator dots are already installed."
                            : "Resource locator dots are already removed.");
                    return result;
                }

                bool knownFile =
                    HashEquals(currentHash, HarvestCoreOriginal) ||
                    HashEquals(currentHash, HarvestCoreLocatorV1) ||
                    HashEquals(currentHash, HarvestCoreLocatorV2);
                if (!knownFile)
                {
                    return SetAdaptiveEnabledAt(
                        gamePath, backupRoot, enabled,
                        result, path, currentHash);
                }

                string source = NormalizeNewlines(ReadUtf8(path));
                string transformed;
                if (enabled && HashEquals(currentHash, HarvestCoreOriginal))
                {
                    transformed = ReplaceUnique(
                        source, OriginalDeclaration, LocatorV2Declaration,
                        "HarvestCore locator declaration");
                }
                else if (enabled && HashEquals(currentHash, HarvestCoreLocatorV1))
                {
                    transformed = ReplaceUnique(
                        source, LocatorV1Declaration, LocatorV2Declaration,
                        "HarvestCore locator declaration");
                }
                else if (!enabled && HashEquals(currentHash, HarvestCoreLocatorV2))
                {
                    transformed = ReplaceUnique(
                        source, LocatorV2Declaration, OriginalDeclaration,
                        "HarvestCore locator declaration").Replace("\n", "\r\n");
                }
                else if (!enabled && HashEquals(currentHash, HarvestCoreLocatorV1))
                {
                    transformed = ReplaceUnique(
                        source, LocatorV1Declaration, OriginalDeclaration,
                        "HarvestCore locator declaration").Replace("\n", "\r\n");
                }
                else
                {
                    throw new InvalidOperationException(
                        "HarvestCore.lua does not match the verified original or ScrapLab " +
                        "resource-locator version. No files were changed. Use Steam Verify " +
                        "before changing this mod.");
                }

                string generatedHash = Sha256(Encoding.UTF8.GetBytes(transformed));
                if (!HashEquals(generatedHash, desiredHash))
                {
                    throw new InvalidOperationException(
                        "The generated resource-locator patch did not match its verified checksum.");
                }

                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
                string backupPath = Path.Combine(
                    backupRoot,
                    (enabled ? "Install-" : "Remove-") + "ResourceLocator-" + stamp);
                Directory.CreateDirectory(backupPath);
                result.BackupPath = backupPath;

                string backupFile = Path.Combine(backupPath, "HarvestCore.lua");
                File.Copy(path, backupFile, false);
                if (!HashEquals(Sha256(backupFile), currentHash))
                    throw new IOException("The HarvestCore backup failed checksum verification.");

                StringBuilder manifest = new StringBuilder();
                manifest.AppendLine("ScrapLab secret-mod backup");
                manifest.AppendLine("Mod: Resource Locator Dots");
                manifest.AppendLine("Action: " + (enabled ? "Install" : "Remove"));
                manifest.AppendLine("Game path: " + gamePath);
                manifest.AppendLine("Game version: " + result.GameVersion);
                manifest.AppendLine("Created: " + DateTime.Now.ToString("O"));
                manifest.AppendLine("HarvestCore.lua SHA-256 " + currentHash);
                File.WriteAllText(
                    Path.Combine(backupPath, "MANIFEST.txt"),
                    manifest.ToString(), new UTF8Encoding(false));

                try
                {
                    ReplaceFile(path, transformed);
                    if (!HashEquals(Sha256(path), desiredHash))
                        throw new IOException(
                            "HarvestCore.lua failed its final checksum verification.");
                }
                catch
                {
                    File.Copy(backupFile, path, true);
                    if (!HashEquals(Sha256(path), currentHash))
                    {
                        throw new IOException(
                            "The resource-locator update failed and automatic rollback could " +
                            "not restore HarvestCore.lua. The verified backup remains in " +
                            backupPath);
                    }
                    throw;
                }

                result.Success = true;
                result.FilesPatched = 1;
                AdaptivePatchSupport.FillResult(
                    result,
                    AdaptivePatchSupport.GetSteamBuild(
                        gamePath, result.GameVersion),
                    enabled
                        ? PatchCompatibilityState.KnownInstalled
                        : PatchCompatibilityState.KnownClean,
                    false, true, "Verified current-build transformation.");
                result.Changes.Add(
                    enabled
                        ? "Added visible Connect Tool dots to haybot spines and refineable wood, stone, and metal resource cores."
                        : "Removed Resource Locator Dots and restored the verified original HarvestCore script.");
                result.Changes.Add(
                    enabled
                        ? "Added one inactive logic-output slot and neutral locator colors; the output never sends an ON signal."
                        : "Normal refining behavior remains unchanged.");
                AdaptivePatchSupport.QueueBuildActivation(
                    result, "ResourceLocator", enabled);
                SecretModBackupRetention.Prune(
                    backupRoot, "ResourceLocator",
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

        private static GamePatchResult GetAdaptiveStatus(
            GamePatchResult result, string gamePath, string path)
        {
            LuaTextDocument document = AdaptivePatchSupport.ReadLua(path);
            SteamBuildInfo build = AdaptivePatchSupport.GetSteamBuild(
                gamePath, result.GameVersion);
            if (document.MixedNewlines)
            {
                result.Success = true;
                result.Installed = false;
                AdaptivePatchSupport.FillResult(
                    result, build,
                    PatchCompatibilityState.OtherModification,
                    false, false,
                    "HarvestCore.lua uses mixed newline styles.");
                return result;
            }
            try
            {
                RequireAdaptiveResourceGuards(document.NormalizedText);
            }
            catch (InvalidDataException exception)
            {
                result.Success = true;
                result.Installed = false;
                AdaptivePatchSupport.FillResult(
                    result, build,
                    PatchCompatibilityState.UnsupportedCode,
                    false, false,
                    "HarvestCore.lua changed a required refining or creation callback. " +
                    exception.Message);
                return result;
            }
            int clean = AdaptivePatchSupport.Count(
                document.NormalizedText, OriginalDeclaration);
            int v1 = AdaptivePatchSupport.Count(
                document.NormalizedText, LocatorV1Declaration);
            int v2 = AdaptivePatchSupport.Count(
                document.NormalizedText, LocatorV2Declaration);

            if (v2 == 1 && clean == 0 && v1 == 0)
            {
                if (AdaptivePatchSupport.RequiresBuildRefresh(
                    "ResourceLocator", build))
                {
                    AdaptivePatchSupport.MarkRefreshRequired(
                        result, build, null);
                    return result;
                }
                result.Success = true;
                result.Installed = true;
                result.AlreadyPatched = true;
                AdaptivePatchSupport.FillResult(
                    result, build,
                    PatchCompatibilityState.AdaptiveInstalled,
                    true, true,
                    "Resource Locator Dots are structurally intact.");
                return result;
            }
            if (v1 == 1 && clean == 0 && v2 == 0)
            {
                result.Success = true;
                result.Installed = true;
                result.NeedsUpdate = true;
                AdaptivePatchSupport.FillResult(
                    result, build,
                    PatchCompatibilityState.AdaptiveInstalled,
                    true, true,
                    "The older locator patch is structurally intact.");
                return result;
            }
            if (clean == 1 && v1 == 0 && v2 == 0)
            {
                AdaptivePatchSupport.DiscardReceiptIfSuperseded(
                    "ResourceLocator", gamePath);
                string reason = "";
                bool canApply = AdaptivePatchSupport.CanAdaptCleanFiles(
                    build, new[] { path }, out reason);
                result.Success = true;
                result.Installed = false;
                AdaptivePatchSupport.FillResult(
                    result, build,
                    canApply
                        ? PatchCompatibilityState.CompatibleUpdate
                        : PatchCompatibilityState.OtherModification,
                    canApply, canApply, reason);
                return result;
            }

            bool partial =
                document.NormalizedText.IndexOf(
                    "RAID RESCUE SECRET MOD",
                    StringComparison.Ordinal) >= 0 ||
                clean + v1 + v2 > 0;
            result.Success = true;
            result.Installed = false;
            AdaptivePatchSupport.FillResult(
                result, build,
                partial
                    ? PatchCompatibilityState.PartialConflict
                    : PatchCompatibilityState.UnsupportedCode,
                false, false,
                partial
                    ? "HarvestCore.lua contains a partial or conflicting locator patch."
                    : "The game update changed the protected HarvestCore declaration.");
            return result;
        }

        private static GamePatchResult SetAdaptiveEnabledAt(
            string gamePath, string backupRoot, bool enabled,
            GamePatchResult result, string path, string currentHash)
        {
            LuaTextDocument document = AdaptivePatchSupport.ReadLua(path);
            AdaptivePatchSupport.RequireAdaptiveFormat(
                document, "HarvestCore.lua");
            RequireAdaptiveResourceGuards(document.NormalizedText);
            SteamBuildInfo build = AdaptivePatchSupport.GetSteamBuild(
                gamePath, result.GameVersion);

            int clean = AdaptivePatchSupport.Count(
                document.NormalizedText, OriginalDeclaration);
            int v1 = AdaptivePatchSupport.Count(
                document.NormalizedText, LocatorV1Declaration);
            int v2 = AdaptivePatchSupport.Count(
                document.NormalizedText, LocatorV2Declaration);
            string transformed;
            if (enabled)
            {
                if (v2 == 1 && clean == 0 && v1 == 0 &&
                    AdaptivePatchSupport.RequiresBuildRefresh(
                        "ResourceLocator", build))
                {
                    AdaptivePatchSupport.PrepareBuildRefresh(
                        result, "ResourceLocator", build,
                        "Resource Locator Dots were reactivated after the Steam update.");
                    return result;
                }
                string reason = "";
                if (clean != 1 || v1 != 0 || v2 != 0 ||
                    !AdaptivePatchSupport.CanAdaptCleanFiles(
                        build, new[] { path }, out reason))
                {
                    throw new InvalidOperationException(
                        "Resource Locator Dots cannot be applied: " +
                        (String.IsNullOrEmpty(reason)
                            ? "the protected declaration is not an exact clean match."
                            : reason));
                }
                transformed = ReplaceUnique(
                    document.NormalizedText,
                    OriginalDeclaration, LocatorV2Declaration,
                    "HarvestCore locator declaration");
            }
            else
            {
                if (v2 == 1 && clean == 0 && v1 == 0)
                {
                    transformed = ReplaceUnique(
                        document.NormalizedText,
                        LocatorV2Declaration, OriginalDeclaration,
                        "HarvestCore locator declaration");
                }
                else if (v1 == 1 && clean == 0 && v2 == 0)
                {
                    transformed = ReplaceUnique(
                        document.NormalizedText,
                        LocatorV1Declaration, OriginalDeclaration,
                        "HarvestCore locator declaration");
                }
                else
                {
                    throw new InvalidOperationException(
                        "Resource Locator Dots cannot be removed because its protected " +
                        "declaration is missing, duplicated, or edited.");
                }
            }

            byte[] outputBytes = document.Render(transformed);
            string outputHash = AdaptivePatchSupport.Sha256(outputBytes);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            string backupPath = Path.Combine(
                backupRoot,
                (enabled ? "Install-" : "Remove-") +
                "ResourceLocator-" + stamp);
            Directory.CreateDirectory(backupPath);
            result.BackupPath = backupPath;
            string backupFile = Path.Combine(backupPath, "HarvestCore.lua");
            File.Copy(path, backupFile, false);
            if (!HashEquals(
                AdaptivePatchSupport.Sha256(backupFile), currentHash))
                throw new IOException(
                    "The adaptive HarvestCore backup failed checksum verification.");
            AdaptivePatchSupport.WriteBackupManifest(
                backupPath, "Resource Locator Dots",
                enabled ? "Install" : "Remove",
                gamePath, build, "2",
                new[]
                {
                    new AdaptivePatchReceiptFile
                    {
                        RelativePath = HarvestCoreRelativePath,
                        SourceHash = currentHash,
                        OutputHash = outputHash,
                        Newline = document.Newline == "\r\n"
                            ? "CRLF" : "LF",
                        HasBom = document.HasBom
                    }
                });

            AdaptivePatchReceipt receipt =
                AdaptivePatchSupport.LoadReceipt("ResourceLocator");
            AdaptivePatchReceiptFile receiptFile =
                AdaptivePatchSupport.FindReceiptFile(
                    receipt, HarvestCoreRelativePath);
            try
            {
                if (!enabled && receiptFile != null &&
                    HashEquals(currentHash, receiptFile.OutputHash) &&
                    File.Exists(receiptFile.BackupPath) &&
                    HashEquals(
                        AdaptivePatchSupport.Sha256(receiptFile.BackupPath),
                        receiptFile.SourceHash))
                {
                    AdaptivePatchSupport.ReplaceFile(
                        path, File.ReadAllBytes(receiptFile.BackupPath),
                        "resource-exact-restore");
                    outputHash = receiptFile.SourceHash;
                }
                else
                {
                    AdaptivePatchSupport.ReplaceFile(
                        path, outputBytes, "resource-adaptive");
                }
                if (!HashEquals(
                    AdaptivePatchSupport.Sha256(path), outputHash))
                    throw new IOException(
                        "HarvestCore.lua failed adaptive output verification.");
            }
            catch
            {
                File.Copy(backupFile, path, true);
                if (!HashEquals(
                    AdaptivePatchSupport.Sha256(path), currentHash))
                    throw new IOException(
                        "Adaptive resource-locator rollback could not restore HarvestCore.lua.");
                throw;
            }

            result.Success = true;
            result.Installed = enabled;
            result.FilesPatched = 1;
            AdaptivePatchSupport.FillResult(
                result, build,
                enabled
                    ? PatchCompatibilityState.AdaptiveInstalled
                    : PatchCompatibilityState.CompatibleUpdate,
                true, true,
                enabled
                    ? "Installed with exact protected-code matching on this Steam build."
                    : "Removed while preserving the updated game file.");
            result.Changes.Add(
                enabled
                    ? "Installed Resource Locator Dots on a structurally compatible game update."
                    : "Removed Resource Locator Dots without replacing unrelated updated code.");

            if (enabled)
            {
                string activeBase = AdaptivePatchSupport.CaptureBaseBackup(
                    "ResourceLocator", HarvestCoreRelativePath,
                    backupFile, currentHash);
                AdaptivePatchSupport.SaveReceipt(
                    "ResourceLocator",
                    new AdaptivePatchReceipt
                    {
                        ModKey = "ResourceLocator",
                        DefinitionVersion = "2",
                        SteamBuildId = build.BuildId,
                        GameVersion = result.GameVersion,
                        CreatedUtc = DateTime.UtcNow.ToString("O"),
                        Files = new List<AdaptivePatchReceiptFile>
                        {
                            new AdaptivePatchReceiptFile
                            {
                                RelativePath = HarvestCoreRelativePath,
                                SourceHash = currentHash,
                                OutputHash = outputHash,
                                BackupPath = activeBase,
                                Newline = document.Newline == "\r\n"
                                    ? "CRLF" : "LF",
                                HasBom = document.HasBom
                            }
                        }
                    });
            }
            else
            {
                AdaptivePatchSupport.DeleteReceipt("ResourceLocator");
            }
            AdaptivePatchSupport.QueueBuildActivation(
                result, "ResourceLocator", enabled);
            SecretModBackupRetention.Prune(
                backupRoot, "ResourceLocator", backupPath, result);
            return result;
        }

        private static void RequireAdaptiveResourceGuards(string text)
        {
            AdaptivePatchSupport.RequireUnique(
                text,
                "function HarvestCore.client_onCreate( self )",
                "HarvestCore client creation callback");
            AdaptivePatchSupport.RequireUnique(
                text,
                "function HarvestCore.sv_refine( self, player )",
                "HarvestCore refining callback");
        }

        private static string ReplaceUnique(
            string text, string oldText, string newText, string description)
        {
            int first = text.IndexOf(oldText, StringComparison.Ordinal);
            if (first < 0 ||
                text.IndexOf(oldText, first + oldText.Length, StringComparison.Ordinal) >= 0)
            {
                throw new InvalidDataException(
                    "The expected " + description + " code was not found exactly once.");
            }
            return text.Substring(0, first) + newText +
                text.Substring(first + oldText.Length);
        }

        private static void ReplaceFile(string path, string text)
        {
            string temporary = path + ".raidrescue-secret-" +
                Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, text, new UTF8Encoding(false));
                File.Replace(temporary, path, null);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }

        private static string ReadUtf8(string path)
        {
            return File.ReadAllText(path, new UTF8Encoding(false, true));
        }

        private static string NormalizeNewlines(string value)
        {
            return value.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        private static string Sha256(string path)
        {
            using (FileStream stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (SHA256 algorithm = SHA256.Create())
                return BytesToHex(algorithm.ComputeHash(stream));
        }

        private static string Sha256(byte[] value)
        {
            using (SHA256 algorithm = SHA256.Create())
                return BytesToHex(algorithm.ComputeHash(value));
        }

        private static string BytesToHex(byte[] value)
        {
            StringBuilder text = new StringBuilder(value.Length * 2);
            foreach (byte item in value)
                text.Append(item.ToString("X2"));
            return text.ToString();
        }

        private static bool HashEquals(string left, string right)
        {
            return String.Equals(left, right, StringComparison.OrdinalIgnoreCase);
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

#if LEGACY_SELF_HELPERS
    internal sealed class ElevatedPatchRequest
    {
        public string Token { get; set; }
        public string Action { get; set; }
        public bool Enabled { get; set; }
        public string Mode { get; set; }
    }

    internal static class ElevatedPatchBroker
    {
        private const string HelperSwitch = "--elevated-patch-session";
        internal const string HotfixAction = "hotfix";
        internal const string ResourceAction = "resource";
        internal const string ChemicalDirectAction = "chemical-direct";
        internal const string ChemicalAction = "chemical";
        internal const string CannonAction = "cannon";
        internal const string CommandsAction = "commands";
        internal const string RevivalBuffsAction = "revival-buffs";
        private static readonly object Sync = new object();
        private static Process brokerProcess;
        private static string brokerPipe;
        private static string brokerToken;
        private static NamedPipeServerStream brokerServer;
        private static StreamWriter brokerWriter;
        private static StreamReader brokerReader;

        public static bool TryRunHelper(string[] args)
        {
            if (args == null || args.Length == 0 ||
                !String.Equals(args[0], HelperSwitch, StringComparison.Ordinal))
                return false;
            if (args.Length != 4)
                return true;

            string pipeName = args[1];
            string token = args[2];
            int parentId;
            if (!IsSafeIdentifier(pipeName, 80) ||
                !IsSafeIdentifier(token, 160) ||
                !Int32.TryParse(args[3], out parentId) ||
                parentId <= 0)
                return true;

            RunClient(pipeName, token, parentId);
            return true;
        }

        public static GamePatchResult Execute(
            string action, bool enabled, string mode)
        {
            lock (Sync)
            {
                try
                {
                    EnsureBroker();
                    return SendRequest(new ElevatedPatchRequest
                    {
                        Token = brokerToken,
                        Action = action,
                        Enabled = enabled,
                        Mode = mode ?? ""
                    });
                }
                catch (Win32Exception exception)
                {
                    if (exception.NativeErrorCode == 1223)
                    {
                        ResetBroker();
                        return new GamePatchResult
                        {
                            Success = false,
                            Cancelled = true,
                            Changes = new List<string>()
                        };
                    }
                    return Failure(exception.Message);
                }
                catch (Exception exception)
                {
                    return Failure(
                        "The elevated patch session could not complete the request. " +
                        exception.Message);
                }
            }
        }

        private static void EnsureBroker()
        {
            if (brokerProcess != null)
            {
                try
                {
                    if (!brokerProcess.HasExited &&
                        !String.IsNullOrEmpty(brokerPipe) &&
                        !String.IsNullOrEmpty(brokerToken) &&
                        brokerServer != null &&
                        brokerServer.IsConnected)
                        return;
                }
                catch { }
                ResetBroker();
            }

            brokerPipe = "ScrapLab-" + Guid.NewGuid().ToString("N");
            brokerToken = CreateToken();
            brokerServer = new NamedPipeServerStream(
                brokerPipe, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = Assembly.GetExecutingAssembly().Location,
                Arguments = Quote(HelperSwitch) + " " +
                    Quote(brokerPipe) + " " +
                    Quote(brokerToken) + " " +
                    Quote(Process.GetCurrentProcess().Id.ToString()),
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };
            brokerProcess = Process.Start(start);
            if (brokerProcess == null)
            {
                ResetBroker();
                throw new InvalidOperationException(
                    "Windows did not start the elevated patch session.");
            }

            IAsyncResult waiting =
                brokerServer.BeginWaitForConnection(null, null);
            if (!waiting.AsyncWaitHandle.WaitOne(15000))
            {
                ResetBroker();
                throw new TimeoutException(
                    "The elevated patch session did not connect in time.");
            }
            brokerServer.EndWaitForConnection(waiting);
            brokerWriter = new StreamWriter(
                brokerServer, new UTF8Encoding(false));
            brokerWriter.AutoFlush = true;
            brokerReader = new StreamReader(
                brokerServer, new UTF8Encoding(false, true));
        }

        private static GamePatchResult SendRequest(ElevatedPatchRequest request)
        {
            JavaScriptSerializer serializer =
                new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
            brokerWriter.WriteLine(serializer.Serialize(request));
            string response = brokerReader.ReadLine();
            if (String.IsNullOrEmpty(response))
                throw new IOException(
                    "The elevated patch session returned no result.");
            return serializer.Deserialize<GamePatchResult>(response);
        }

        private static void RunClient(
            string pipeName, string token, int parentId)
        {
            Process parent;
            try
            {
                parent = Process.GetProcessById(parentId);
            }
            catch
            {
                return;
            }

            using (NamedPipeClientStream pipe = new NamedPipeClientStream(
                ".", pipeName, PipeDirection.InOut, PipeOptions.None))
            {
                try
                {
                    pipe.Connect(15000);
                }
                catch
                {
                    return;
                }

                StreamReader reader = new StreamReader(
                    pipe, new UTF8Encoding(false, true));
                StreamWriter writer = new StreamWriter(
                    pipe, new UTF8Encoding(false));
                writer.AutoFlush = true;
                while (!HasExited(parent))
                {
                    GamePatchResult result;
                    try
                    {
                        string text = reader.ReadLine();
                        if (String.IsNullOrEmpty(text))
                            return;
                        JavaScriptSerializer serializer =
                            new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
                        ElevatedPatchRequest request =
                            serializer.Deserialize<ElevatedPatchRequest>(text);
                        if (request == null ||
                            !String.Equals(
                                request.Token, token,
                                StringComparison.Ordinal))
                        {
                            result = Failure(
                                "The elevated patch session rejected an unauthenticated request.");
                        }
                        else
                        {
                            result = Dispatch(request);
                        }

                        writer.WriteLine(serializer.Serialize(result));
                    }
                    catch (Exception exception)
                    {
                        try
                        {
                            JavaScriptSerializer serializer =
                                new JavaScriptSerializer();
                            writer.WriteLine(serializer.Serialize(
                                Failure(exception.Message)));
                        }
                        catch { }
                    }
                }
            }
        }

        private static GamePatchResult Dispatch(ElevatedPatchRequest request)
        {
            switch (request.Action)
            {
                case HotfixAction:
                    return GamePatchService.Install();
                case ResourceAction:
                    return SecretModPatchService.SetEnabled(request.Enabled);
                case ChemicalDirectAction:
                    return ChemicalFertilizerPatchService.SetEnabled(request.Enabled);
                case ChemicalAction:
                    return DualFluidCannonPatchCoordinator.SetChemicalEnabled(
                        request.Enabled);
                case CannonAction:
                    return DualFluidCannonPatchCoordinator.SetCannonEnabled(
                        request.Enabled);
                case CommandsAction:
                    return DeveloperCommandsPatchService.SetEnabled(
                        request.Enabled, request.Mode);
                case RevivalBuffsAction:
                    return RevivalBuffPatchService.SetEnabled(
                        request.Enabled);
                default:
                    return Failure(
                        "The elevated patch session rejected an unknown action.");
            }
        }

        private static bool HasExited(Process process)
        {
            try { return process == null || process.HasExited; }
            catch { return true; }
        }

        private static bool IsSafeIdentifier(string value, int maximumLength)
        {
            if (String.IsNullOrEmpty(value) ||
                value.Length > maximumLength)
                return false;
            foreach (char item in value)
            {
                if (!Char.IsLetterOrDigit(item) && item != '-')
                    return false;
            }
            return true;
        }

        private static string CreateToken()
        {
            byte[] value = new byte[32];
            using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
                generator.GetBytes(value);
            return Convert.ToBase64String(value)
                .Replace("+", "A")
                .Replace("/", "B")
                .TrimEnd('=');
        }

        private static void ResetBroker()
        {
            try
            {
                if (brokerWriter != null)
                    brokerWriter.Dispose();
            }
            catch { }
            try
            {
                if (brokerReader != null)
                    brokerReader.Dispose();
            }
            catch { }
            try
            {
                if (brokerServer != null)
                    brokerServer.Dispose();
            }
            catch { }
            try
            {
                if (brokerProcess != null)
                    brokerProcess.Dispose();
            }
            catch { }
            brokerWriter = null;
            brokerReader = null;
            brokerServer = null;
            brokerProcess = null;
            brokerPipe = null;
            brokerToken = null;
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
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

    internal static class SecretModPatchLauncher
    {
        private const string HelperSwitch = "--set-resource-locator-mod";

        public static bool TryRunHelper(string[] args)
        {
            if (args == null || args.Length == 0 ||
                !String.Equals(args[0], HelperSwitch, StringComparison.Ordinal))
                return false;
            // A recognized internal helper command must never fall through into
            // normal UI startup, even if its arguments are malformed.
            if (args.Length != 3)
                return true;

            bool enabled;
            if (args[1] == "1")
                enabled = true;
            else if (args[1] == "0")
                enabled = false;
            else
                return true;

            string resultPath = args[2];
            GamePatchResult result;
            bool resultPathIsValid = false;
            try
            {
                ValidateResultPath(resultPath);
                resultPathIsValid = true;
                result = SecretModPatchService.SetEnabled(enabled);
            }
            catch (Exception exception)
            {
                result = Failure(exception.Message);
            }

            try
            {
                if (resultPathIsValid)
                {
                    JavaScriptSerializer serializer =
                        new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
                    File.WriteAllText(
                        resultPath, serializer.Serialize(result),
                        new UTF8Encoding(false));
                }
            }
            catch { }
            return true;
        }

        public static GamePatchResult SetEnabled(bool enabled)
        {
            if (GamePatchService.IsGameRunning())
                return Failure(
                    "Scrap Mechanic is running. Close the game completely before changing secret mods.");

            if (IsAdministrator())
                return SecretModPatchService.SetEnabled(enabled);
            return ElevatedPatchBroker.Execute(
                ElevatedPatchBroker.ResourceAction, enabled, "");
        }

        private static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private static void ValidateResultPath(string path)
        {
            string directory = Path.GetFullPath(GetResultDirectory())
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(path);
            if (!full.StartsWith(directory, StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(
                    Path.GetExtension(full), ".json",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The secret-mod result path is invalid.");
            }
        }

        private static string GetResultDirectory()
        {
            return Path.Combine(
                Path.GetTempPath(), "ScrapLab", "patch-results");
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
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

#endif

    internal static class ChemicalFertilizerPatchService
    {
        private const string BaseWorldOriginal =
            "1FD9A93DB893A632E60491501867869C13182296138D1422C778327771430B37";
        private const string SoilOriginal =
            "B73AE51E8E005840CF105CA46B1408B7C3D52EAEA51C1102762E226201A49079";
        private const string GrowingOriginal =
            "18FBC73B7267A8C97FA365780F96B6F7672A3271F31842322CD77B565F545D17";
        private const string GrowingRaidPatched =
            "CF156C9B1E5E5181F37B3362D67678BF5CF201D1418B5076B6CDD968C9C580C5";
        private const string GrowingCumulativePatched =
            "A790E7DCF43F7C0B85A2A89323BEE2AD369A363A85B583797847B36612056BE0";
        private const string GrowbedOriginal =
            "3FBAD1E8E0976FE9A387FA29A9C81C091EC8A0B0D9FBE65C188E275A8B30435A";
        private const string GrowbedHotfixPatched =
            "A186D22A35C032F4E2C694B6DC510417355923E6A44EFB8FA66DB08E49732249";

        // Generated from the exact transformations below.
        private const string BaseWorldChemicalPatched =
            "D07FCF069C5EA900B6C485E5E51F39EAD142BA3617924C4D56F2B1B503051FB7";
        private const string SoilChemicalPatched =
            "9D186714DC4D1F667F80E1CA1A21F1CB748CCCF8DFFFC5B096C033EC836C3198";
        private const string GrowingOriginalChemicalPatched =
            "1927327C48600510CE94EAC79151EEE058D259915ACC36D665DE2FA125EA6EAB";
        private const string GrowingRaidChemicalPatched =
            "EC82941DBF69F0F93F8D0F9AC13F62B63FFBF6308BF3A5CC20061A288924BCD0";
        private const string GrowingCumulativeChemicalPatched =
            "FE7424CE0EE948245D4E70AA0EA560C3F2AB915B886566EA617F8E844F0AE4BB";
        private const string GrowbedOriginalChemicalPatched =
            "ADBB0B234A47FED898B884E9FC2B8FFBAAEBFDC0426101A711F1E948C07D4A52";
        private const string GrowbedHotfixChemicalPatched =
            "1DF8DF596EEEC770C594B600BEBCD2A63506CA4F99BB00C64A81ED9024B7B10E";

        private sealed class AdaptiveChemicalState
        {
            public ChemicalPatchTarget Target;
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
                    throw new InvalidOperationException("Scrap Mechanic was not found.");

                result.GamePath = gamePath;
                string executable = Path.Combine(gamePath, "Release", "ScrapMechanic.exe");
                result.GameVersion = FileVersionInfo.GetVersionInfo(executable).FileVersion;

                int installedCount = 0;
                List<ChemicalPatchTarget> targets = GetTargets();
                foreach (ChemicalPatchTarget target in targets)
                {
                    string path = Path.Combine(gamePath, target.RelativePath);
                    if (!File.Exists(path))
                        throw new FileNotFoundException(target.DisplayName + " was not found.", path);

                    string hash = Sha256(path);
                    bool found = false;
                    foreach (ChemicalPatchVariant variant in target.Variants)
                    {
                        if (HashEquals(hash, variant.PatchedHash))
                        {
                            installedCount++;
                            found = true;
                            break;
                        }
                        if (HashEquals(hash, variant.BaseHash))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        return GetAdaptiveChemicalStatus(
                            result, gamePath, targets);
                    }
                }

                if (installedCount != 0 && installedCount != targets.Count)
                {
                    throw new InvalidOperationException(
                        "Chemical Fertilizer Splash is only partially installed. No automatic " +
                        "changes will be made until the affected scripts are restored with Steam Verify.");
                }

                result.Success = true;
                result.Installed = installedCount == targets.Count;
                result.AlreadyPatched = result.Installed;
                SteamBuildInfo build =
                    AdaptivePatchSupport.GetSteamBuild(
                        gamePath, result.GameVersion);
                if (result.Installed &&
                    AdaptivePatchSupport.RequiresBuildRefresh(
                        "ChemicalFertilizerSplash", build))
                {
                    AdaptivePatchSupport.MarkRefreshRequired(
                        result, build, null);
                    return result;
                }
                if (!result.Installed)
                {
                    AdaptivePatchSupport.DiscardReceiptIfSuperseded(
                        "ChemicalFertilizerSplash", gamePath);
                }
                AdaptivePatchSupport.FillResult(
                    result, build,
                    result.Installed
                        ? PatchCompatibilityState.KnownInstalled
                        : PatchCompatibilityState.KnownClean,
                    false, true,
                    result.Installed
                        ? "Verified ScrapLab files."
                        : "Verified supported base files.");
                return result;
            }
            catch (Exception exception)
            {
                result.Success = false;
                result.Error = exception.Message;
                return result;
            }
        }

        internal static bool IsInstalledAt(string gamePath)
        {
            int installedCount = 0;
            List<ChemicalPatchTarget> targets = GetTargets();
            foreach (ChemicalPatchTarget target in targets)
            {
                string path = Path.Combine(gamePath, target.RelativePath);
                if (!File.Exists(path))
                    throw new FileNotFoundException(target.DisplayName + " was not found.", path);

                string hash = Sha256(path);
                bool found = false;
                foreach (ChemicalPatchVariant variant in target.Variants)
                {
                    if (HashEquals(hash, variant.PatchedHash))
                    {
                        installedCount++;
                        found = true;
                        break;
                    }
                    if (HashEquals(hash, variant.BaseHash))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    return GetAdaptiveChemicalInstalledAt(
                        gamePath, targets);
                }
            }
            if (installedCount != 0 && installedCount != targets.Count)
            {
                throw new InvalidOperationException(
                    "Chemical Fertilizer Splash is only partially installed. Use Steam Verify " +
                    "before installing or updating the standard hotfix.");
            }
            return installedCount == targets.Count;
        }

        internal static bool PreflightAt(string gamePath, bool enabled)
        {
            string executable = Path.Combine(
                gamePath, "Release", "ScrapMechanic.exe");
            if (!File.Exists(executable))
                throw new FileNotFoundException(
                    "ScrapMechanic.exe was not found.", executable);
            string gameVersion =
                FileVersionInfo.GetVersionInfo(executable).FileVersion;
            List<ChemicalPatchTarget> targets = GetTargets();
            int knownInstalled = 0;
            bool allKnown = true;

            foreach (ChemicalPatchTarget target in targets)
            {
                string path = Path.Combine(gamePath, target.RelativePath);
                if (!File.Exists(path))
                    throw new FileNotFoundException(
                        target.DisplayName + " was not found.", path);
                string hash = Sha256(path);
                ChemicalPatchVariant matched = null;
                bool installed = false;
                foreach (ChemicalPatchVariant variant in target.Variants)
                {
                    if (HashEquals(hash, variant.BaseHash))
                    {
                        matched = variant;
                        break;
                    }
                    if (HashEquals(hash, variant.PatchedHash))
                    {
                        matched = variant;
                        installed = true;
                        break;
                    }
                }
                if (matched == null)
                {
                    allKnown = false;
                    break;
                }
                if (installed)
                    knownInstalled++;
                if (installed != enabled)
                {
                    string source = NormalizeNewlines(ReadUtf8(path));
                    string transformed = enabled
                        ? target.Patch(source)
                        : target.Unpatch(source);
                    if (!enabled && matched.RestoreCrLf)
                        transformed = transformed.Replace("\n", "\r\n");
                    string expected = enabled
                        ? matched.PatchedHash
                        : matched.BaseHash;
                    if (!HashEquals(
                        Sha256(Encoding.UTF8.GetBytes(transformed)),
                        expected))
                    {
                        throw new InvalidOperationException(
                            target.DisplayName +
                            " failed dependency preflight generation.");
                    }
                }
            }

            if (allKnown)
            {
                if (knownInstalled != 0 &&
                    knownInstalled != targets.Count)
                {
                    throw new InvalidOperationException(
                        "Chemical Fertilizer Splash is only partially installed.");
                }
                return knownInstalled == targets.Count;
            }

            List<AdaptiveChemicalState> states =
                ProbeAdaptiveChemicalTargets(gamePath, targets, true);
            int clean = 0;
            int installedCount = 0;
            List<string> paths = new List<string>();
            foreach (AdaptiveChemicalState state in states)
            {
                paths.Add(state.Path);
                if (state.Clean) clean++;
                if (state.Installed) installedCount++;
            }
            if (enabled)
            {
                if (installedCount == states.Count)
                    return true;
                string reason = "";
                SteamBuildInfo build =
                    AdaptivePatchSupport.GetSteamBuild(
                        gamePath, gameVersion);
                if (clean != states.Count ||
                    !AdaptivePatchSupport.CanAdaptCleanFiles(
                        build, paths, out reason))
                {
                    throw new InvalidOperationException(
                        "Chemical Fertilizer Splash dependency preflight failed: " +
                        (String.IsNullOrEmpty(reason)
                            ? "a protected target is not an exact clean match."
                            : reason));
                }
                foreach (AdaptiveChemicalState state in states)
                    state.Document.Render(state.PatchedText);
            }
            else
            {
                if (clean == states.Count)
                    return false;
                if (installedCount != 0 &&
                    installedCount != states.Count)
                {
                    throw new InvalidOperationException(
                        "Chemical Fertilizer Splash dependency preflight found a partial patch.");
                }
                if (installedCount == states.Count)
                {
                    foreach (AdaptiveChemicalState state in states)
                        state.Document.Render(state.CleanText);
                }
            }
            return installedCount == states.Count;
        }

        public static GamePatchResult SetEnabled(bool enabled)
        {
            if (GamePatchService.IsGameRunning())
            {
                return Failure(
                    "Scrap Mechanic is running. Close the game completely before changing secret mods.");
            }

            string gamePath = GamePatchService.FindGameInstall();
            if (String.IsNullOrEmpty(gamePath))
                return Failure("Scrap Mechanic was not found.");

            GamePatchResult result = SetEnabledAt(
                gamePath,
                ProductPaths.LocalDataPath(
                    "Game Backups", "Scrap Mechanic", "Secret Mods"),
                enabled);
            return GameScriptCacheInvalidator.DeleteAfterChanges(gamePath, result);
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
                string executable = Path.Combine(gamePath, "Release", "ScrapMechanic.exe");
                if (!File.Exists(executable))
                    throw new FileNotFoundException("ScrapMechanic.exe was not found.", executable);

                result.GameVersion = FileVersionInfo.GetVersionInfo(executable).FileVersion;
                List<ChemicalPatchTarget> targets = GetTargets();
                Dictionary<ChemicalPatchTarget, string> paths =
                    new Dictionary<ChemicalPatchTarget, string>();
                Dictionary<ChemicalPatchTarget, string> currentHashes =
                    new Dictionary<ChemicalPatchTarget, string>();
                Dictionary<ChemicalPatchTarget, string> desiredHashes =
                    new Dictionary<ChemicalPatchTarget, string>();
                Dictionary<ChemicalPatchTarget, string> transformedText =
                    new Dictionary<ChemicalPatchTarget, string>();
                List<ChemicalPatchTarget> targetsToPatch =
                    new List<ChemicalPatchTarget>();

                foreach (ChemicalPatchTarget target in targets)
                {
                    string path = Path.Combine(gamePath, target.RelativePath);
                    if (!File.Exists(path))
                        throw new FileNotFoundException(target.DisplayName + " was not found.", path);

                    string currentHash = Sha256(path);
                    paths[target] = path;
                    currentHashes[target] = currentHash;

                    ChemicalPatchVariant matched = null;
                    bool currentlyInstalled = false;
                    foreach (ChemicalPatchVariant variant in target.Variants)
                    {
                        if (HashEquals(currentHash, variant.BaseHash))
                        {
                            matched = variant;
                            break;
                        }
                        if (HashEquals(currentHash, variant.PatchedHash))
                        {
                            matched = variant;
                            currentlyInstalled = true;
                            break;
                        }
                    }
                    if (matched == null)
                    {
                        return SetAdaptiveChemicalEnabledAt(
                            gamePath, backupRoot, enabled,
                            result, targets);
                    }
                    if (currentlyInstalled == enabled)
                        continue;

                    string source = NormalizeNewlines(ReadUtf8(path));
                    string transformed = enabled
                        ? target.Patch(source)
                        : target.Unpatch(source);
                    if (!enabled && matched.RestoreCrLf)
                        transformed = transformed.Replace("\n", "\r\n");

                    string desiredHash = enabled ? matched.PatchedHash : matched.BaseHash;
                    string generatedHash = Sha256(Encoding.UTF8.GetBytes(transformed));
                    if (!HashEquals(generatedHash, desiredHash))
                    {
                        throw new InvalidOperationException(
                            "The generated " + target.DisplayName +
                            " chemical-fertilizer patch did not match its verified checksum.");
                    }

                    desiredHashes[target] = desiredHash;
                    transformedText[target] = transformed;
                    targetsToPatch.Add(target);
                }

                if (targetsToPatch.Count == 0)
                {
                    SteamBuildInfo build =
                        AdaptivePatchSupport.GetSteamBuild(
                            gamePath, result.GameVersion);
                    if (enabled &&
                        AdaptivePatchSupport.RequiresBuildRefresh(
                            "ChemicalFertilizerSplash", build))
                    {
                        AdaptivePatchSupport.PrepareBuildRefresh(
                            result, "ChemicalFertilizerSplash", build,
                            "Chemical Fertilizer Splash was reactivated after the Steam update.");
                        return result;
                    }
                    result.Success = true;
                    result.AlreadyPatched = true;
                    if (!enabled)
                    {
                        AdaptivePatchSupport.DeleteBuildActivation(
                            "ChemicalFertilizerSplash");
                    }
                    result.Changes.Add(
                        enabled
                            ? "Chemical Fertilizer Splash is already installed."
                            : "Chemical Fertilizer Splash is already removed.");
                    return result;
                }
                if (targetsToPatch.Count != targets.Count)
                {
                    throw new InvalidOperationException(
                        "Chemical Fertilizer Splash is only partially installed. No files were changed.");
                }

                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
                string backupPath = Path.Combine(
                    backupRoot,
                    (enabled ? "Install-" : "Remove-") +
                    "ChemicalFertilizerSplash-" + stamp);
                Directory.CreateDirectory(backupPath);
                result.BackupPath = backupPath;

                foreach (ChemicalPatchTarget target in targetsToPatch)
                {
                    string backupFile = Path.Combine(
                        backupPath, Path.GetFileName(target.RelativePath));
                    File.Copy(paths[target], backupFile, false);
                    if (!HashEquals(Sha256(backupFile), currentHashes[target]))
                        throw new IOException(target.DisplayName + " backup failed checksum verification.");
                }
                WriteManifest(
                    backupPath, gamePath, result.GameVersion, enabled,
                    targetsToPatch, currentHashes);

                List<ChemicalPatchTarget> replaced =
                    new List<ChemicalPatchTarget>();
                try
                {
                    foreach (ChemicalPatchTarget target in targetsToPatch)
                    {
                        ReplaceFile(paths[target], transformedText[target]);
                        replaced.Add(target);
                        if (!HashEquals(Sha256(paths[target]), desiredHashes[target]))
                        {
                            throw new IOException(
                                target.DisplayName + " failed its final checksum verification.");
                        }
                    }
                }
                catch
                {
                    RollBack(replaced, paths, backupPath, currentHashes);
                    throw;
                }

                result.Success = true;
                result.FilesPatched = targetsToPatch.Count;
                AdaptivePatchSupport.FillResult(
                    result,
                    AdaptivePatchSupport.GetSteamBuild(
                        gamePath, result.GameVersion),
                    enabled
                        ? PatchCompatibilityState.KnownInstalled
                        : PatchCompatibilityState.KnownClean,
                    false, true, "Verified current-build transformation.");
                result.Changes.Add(
                    enabled
                        ? "Chemical projectiles now fertilize the exact soil, crop, or growbed they hit."
                        : "Removed direct chemical fertilizing from soil, crops, and growbeds.");
                result.Changes.Add(
                    enabled
                        ? "Red Farmbot pesticide impacts fertilize supported plots in a 2.5-block radius."
                        : "Restored the verified pesticide impact behavior.");
                result.Changes.Add(
                    enabled
                        ? "The patch runs on the authoritative server and keeps existing fertilizer timing fixes."
                        : "Any installed ScrapLab raid and fertilizer hotfix was preserved.");
                AdaptivePatchSupport.QueueBuildActivation(
                    result, "ChemicalFertilizerSplash", enabled);
                SecretModBackupRetention.Prune(
                    backupRoot, "ChemicalFertilizerSplash",
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

        private static GamePatchResult GetAdaptiveChemicalStatus(
            GamePatchResult result, string gamePath,
            List<ChemicalPatchTarget> targets)
        {
            SteamBuildInfo build = AdaptivePatchSupport.GetSteamBuild(
                gamePath, result.GameVersion);
            List<AdaptiveChemicalState> states;
            try
            {
                states = ProbeAdaptiveChemicalTargets(
                    gamePath, targets, false);
            }
            catch (InvalidDataException exception)
            {
                result.Success = true;
                result.Installed = false;
                AdaptivePatchSupport.FillResult(
                    result, build,
                    PatchCompatibilityState.UnsupportedCode,
                    false, false,
                    "A fertilizer target changed a required callback. " +
                    exception.Message);
                return result;
            }
            int clean = 0;
            int installed = 0;
            List<string> paths = new List<string>();
            foreach (AdaptiveChemicalState state in states)
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
                paths.Add(state.Path);
                if (state.Clean) clean++;
                if (state.Installed) installed++;
            }
            result.Success = true;
            if (installed == states.Count)
            {
                if (AdaptivePatchSupport.RequiresBuildRefresh(
                    "ChemicalFertilizerSplash", build))
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
                    "All Chemical Fertilizer Splash targets are structurally intact.");
                return result;
            }
            if (clean == states.Count)
            {
                AdaptivePatchSupport.DiscardReceiptIfSuperseded(
                    "ChemicalFertilizerSplash", gamePath);
                string reason = "";
                bool canApply = AdaptivePatchSupport.CanAdaptCleanFiles(
                    build, paths, out reason);
                result.Installed = false;
                AdaptivePatchSupport.FillResult(
                    result, build,
                    canApply
                        ? PatchCompatibilityState.CompatibleUpdate
                        : PatchCompatibilityState.OtherModification,
                    canApply, canApply, reason);
                return result;
            }

            result.Installed = false;
            bool partial = clean + installed > 0;
            AdaptiveChemicalState failed = null;
            foreach (AdaptiveChemicalState state in states)
            {
                if (!state.Clean && !state.Installed)
                {
                    failed = state;
                    break;
                }
            }
            string failedFile = failed == null
                ? "A fertilizer target"
                : failed.Target.DisplayName;
            AdaptivePatchSupport.FillResult(
                result, build,
                partial
                    ? PatchCompatibilityState.PartialConflict
                    : PatchCompatibilityState.UnsupportedCode,
                false, false,
                partial
                    ? failedFile +
                        " contains a partial, duplicated, or edited fertilizer patch."
                    : failedFile +
                        " changed a protected fertilizer target.");
            return result;
        }

        private static bool GetAdaptiveChemicalInstalledAt(
            string gamePath, List<ChemicalPatchTarget> targets)
        {
            List<AdaptiveChemicalState> states =
                ProbeAdaptiveChemicalTargets(gamePath, targets, false);
            int clean = 0;
            int installed = 0;
            foreach (AdaptiveChemicalState state in states)
            {
                if (state.Clean) clean++;
                if (state.Installed) installed++;
            }
            if (installed == states.Count)
                return true;
            if (clean == states.Count)
                return false;
            throw new InvalidOperationException(
                "Chemical Fertilizer Splash is partially installed or incompatible. " +
                "No dependency changes were made.");
        }

        private static GamePatchResult SetAdaptiveChemicalEnabledAt(
            string gamePath, string backupRoot, bool enabled,
            GamePatchResult result, List<ChemicalPatchTarget> targets)
        {
            List<AdaptiveChemicalState> states =
                ProbeAdaptiveChemicalTargets(gamePath, targets, true);
            List<string> paths = new List<string>();
            int clean = 0;
            int installed = 0;
            foreach (AdaptiveChemicalState state in states)
            {
                paths.Add(state.Path);
                if (state.Clean) clean++;
                if (state.Installed) installed++;
            }
            SteamBuildInfo build = AdaptivePatchSupport.GetSteamBuild(
                gamePath, result.GameVersion);

            if (enabled)
            {
                if (installed == states.Count &&
                    AdaptivePatchSupport.RequiresBuildRefresh(
                        "ChemicalFertilizerSplash", build))
                {
                    AdaptivePatchSupport.PrepareBuildRefresh(
                        result, "ChemicalFertilizerSplash", build,
                        "Chemical Fertilizer Splash was reactivated after the Steam update.");
                    return result;
                }
                string reason = "";
                if (clean != states.Count ||
                    !AdaptivePatchSupport.CanAdaptCleanFiles(
                        build, paths, out reason))
                {
                    throw new InvalidOperationException(
                        "Chemical Fertilizer Splash cannot be applied: " +
                        (String.IsNullOrEmpty(reason)
                            ? "one or more protected targets are not exact clean matches."
                            : reason));
                }
            }
            else if (installed != states.Count)
            {
                throw new InvalidOperationException(
                    "Chemical Fertilizer Splash cannot be removed because one or " +
                    "more protected snippets are missing, duplicated, or edited.");
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            string backupPath = Path.Combine(
                backupRoot,
                (enabled ? "Install-" : "Remove-") +
                "ChemicalFertilizerSplash-" + stamp);
            Directory.CreateDirectory(backupPath);
            result.BackupPath = backupPath;
            Dictionary<ChemicalPatchTarget, string> currentHashes =
                new Dictionary<ChemicalPatchTarget, string>();

            foreach (AdaptiveChemicalState state in states)
            {
                string output = enabled
                    ? state.PatchedText
                    : state.CleanText;
                state.OutputBytes = state.Document.Render(output);
                state.OutputHash =
                    AdaptivePatchSupport.Sha256(state.OutputBytes);
                state.BackupFile = Path.Combine(
                    backupPath, Path.GetFileName(state.Target.RelativePath));
                File.Copy(state.Path, state.BackupFile, false);
                if (!HashEquals(
                    AdaptivePatchSupport.Sha256(state.BackupFile),
                    state.CurrentHash))
                {
                    throw new IOException(
                        state.Target.DisplayName +
                        " adaptive backup failed checksum verification.");
                }
                currentHashes[state.Target] = state.CurrentHash;
            }
            WriteManifest(
                backupPath, gamePath, result.GameVersion,
                enabled, targets, currentHashes);
            List<AdaptivePatchReceiptFile> manifestFiles =
                new List<AdaptivePatchReceiptFile>();
            foreach (AdaptiveChemicalState state in states)
            {
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
                backupPath, "Chemical Fertilizer Splash",
                enabled ? "Install" : "Remove",
                gamePath, build, "2", manifestFiles);

            AdaptivePatchReceipt receipt =
                AdaptivePatchSupport.LoadReceipt(
                    "ChemicalFertilizerSplash");
            bool exactRestore = !enabled && receipt != null;
            if (exactRestore)
            {
                foreach (AdaptiveChemicalState state in states)
                {
                    AdaptivePatchReceiptFile file =
                        AdaptivePatchSupport.FindReceiptFile(
                            receipt, state.Target.RelativePath);
                    if (file == null ||
                        !HashEquals(state.CurrentHash, file.OutputHash) ||
                        !File.Exists(file.BackupPath) ||
                        !HashEquals(
                            AdaptivePatchSupport.Sha256(file.BackupPath),
                            file.SourceHash))
                    {
                        exactRestore = false;
                        break;
                    }
                }
            }

            List<AdaptiveChemicalState> replaced =
                new List<AdaptiveChemicalState>();
            try
            {
                foreach (AdaptiveChemicalState state in states)
                {
                    if (exactRestore)
                    {
                        AdaptivePatchReceiptFile file =
                            AdaptivePatchSupport.FindReceiptFile(
                                receipt, state.Target.RelativePath);
                        AdaptivePatchSupport.ReplaceFile(
                            state.Path, File.ReadAllBytes(file.BackupPath),
                            "chemical-exact-restore");
                        state.OutputHash = file.SourceHash;
                    }
                    else
                    {
                        AdaptivePatchSupport.ReplaceFile(
                            state.Path, state.OutputBytes,
                            "chemical-adaptive");
                    }
                    replaced.Add(state);
                    if (!HashEquals(
                        AdaptivePatchSupport.Sha256(state.Path),
                        state.OutputHash))
                    {
                        throw new IOException(
                            state.Target.DisplayName +
                            " failed adaptive output verification.");
                    }
                }
            }
            catch
            {
                foreach (AdaptiveChemicalState state in replaced)
                    File.Copy(state.BackupFile, state.Path, true);
                foreach (AdaptiveChemicalState state in replaced)
                {
                    if (!HashEquals(
                        AdaptivePatchSupport.Sha256(state.Path),
                        state.CurrentHash))
                    {
                        throw new IOException(
                            "Adaptive chemical-fertilizer rollback could not restore " +
                            state.Target.DisplayName + ".");
                    }
                }
                throw;
            }

            result.Success = true;
            result.Installed = enabled;
            result.FilesPatched = states.Count;
            AdaptivePatchSupport.FillResult(
                result, build,
                enabled
                    ? PatchCompatibilityState.AdaptiveInstalled
                    : PatchCompatibilityState.CompatibleUpdate,
                true, true,
                enabled
                    ? "Installed with exact protected-code matching on this Steam build."
                    : "Removed while preserving unrelated updated code.");
            result.Changes.Add(
                enabled
                    ? "Installed Chemical Fertilizer Splash on a structurally compatible game update."
                    : "Removed Chemical Fertilizer Splash without replacing unrelated updated code.");

            if (enabled)
            {
                AdaptivePatchReceipt newReceipt =
                    new AdaptivePatchReceipt
                    {
                        ModKey = "ChemicalFertilizerSplash",
                        DefinitionVersion = "2",
                        SteamBuildId = build.BuildId,
                        GameVersion = result.GameVersion,
                        CreatedUtc = DateTime.UtcNow.ToString("O"),
                        Files = new List<AdaptivePatchReceiptFile>()
                    };
                foreach (AdaptiveChemicalState state in states)
                {
                    string activeBase =
                        AdaptivePatchSupport.CaptureBaseBackup(
                            "ChemicalFertilizerSplash",
                            state.Target.RelativePath,
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
                    "ChemicalFertilizerSplash", newReceipt);
            }
            else
            {
                AdaptivePatchSupport.DeleteReceipt(
                    "ChemicalFertilizerSplash");
            }
            AdaptivePatchSupport.QueueBuildActivation(
                result, "ChemicalFertilizerSplash", enabled);

            SecretModBackupRetention.Prune(
                backupRoot, "ChemicalFertilizerSplash",
                backupPath, result);
            return result;
        }

        private static List<AdaptiveChemicalState>
            ProbeAdaptiveChemicalTargets(
                string gamePath, List<ChemicalPatchTarget> targets,
                bool requireFormat)
        {
            List<AdaptiveChemicalState> states =
                new List<AdaptiveChemicalState>();
            foreach (ChemicalPatchTarget target in targets)
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
                RequireAdaptiveChemicalGuards(
                    target, document.NormalizedText);

                string patched = null;
                string clean = null;
                bool cleanState = false;
                bool installedState = false;
                int markerCount = GetAdaptiveChemicalMarkerCount(
                    target, document.NormalizedText);
                if (markerCount == 0)
                {
                    try
                    {
                        patched = target.Patch(document.NormalizedText);
                        cleanState = true;
                    }
                    catch (InvalidDataException) { }
                }
                else if (markerCount == 1)
                {
                    try
                    {
                        clean = target.Unpatch(document.NormalizedText);
                        installedState = true;
                    }
                    catch (InvalidDataException) { }
                }

                if (cleanState == installedState)
                {
                    states.Add(new AdaptiveChemicalState
                    {
                        Target = target,
                        Path = path,
                        Document = document,
                        CurrentHash = document.OriginalHash
                    });
                    continue;
                }
                states.Add(new AdaptiveChemicalState
                {
                    Target = target,
                    Path = path,
                    Document = document,
                    CurrentHash = document.OriginalHash,
                    Clean = cleanState,
                    Installed = installedState,
                    PatchedText = patched,
                    CleanText = clean
                });
            }
            return states;
        }

        private static int GetAdaptiveChemicalMarkerCount(
            ChemicalPatchTarget target, string text)
        {
            string marker;
            if (String.Equals(
                target.DisplayName, "BaseWorld.lua",
                StringComparison.Ordinal))
            {
                marker = "local RaidRescueChemicalSplashRadius = 2.5";
            }
            else if (String.Equals(
                target.DisplayName, "HarvestableSoil.lua",
                StringComparison.Ordinal))
            {
                marker =
                    "function HarvestableSoil.sv_e_raidRescueChemicalFertilize( self )";
            }
            else if (String.Equals(
                target.DisplayName, "GrowingHarvestable.lua",
                StringComparison.Ordinal))
            {
                marker =
                    "function GrowingHarvestable.sv_e_raidRescueChemicalFertilize( self )";
            }
            else
            {
                marker =
                    "function Growbed.sv_e_raidRescueChemicalFertilize( self )";
            }
            return AdaptivePatchSupport.Count(text, marker);
        }

        private static void RequireAdaptiveChemicalGuards(
            ChemicalPatchTarget target, string text)
        {
            string guard;
            if (String.Equals(
                target.DisplayName, "BaseWorld.lua",
                StringComparison.Ordinal))
            {
                guard =
                    "function BaseWorld.server_onProjectile( self, hitPos, hitTime, hitVelocity, _, attacker, damage, userData, hitNormal, target, projectileUuid )";
            }
            else if (String.Equals(
                target.DisplayName, "HarvestableSoil.lua",
                StringComparison.Ordinal))
            {
                guard =
                    "function HarvestableSoil.server_onProjectile( self, hitPos, hitTime, hitVelocity, _, attacker, damage, userData, hitNormal, projectileUuid )";
            }
            else if (String.Equals(
                target.DisplayName, "GrowingHarvestable.lua",
                StringComparison.Ordinal))
            {
                guard =
                    "function GrowingHarvestable.server_onProjectile( self, hitPos, hitTime, hitVelocity, _, attacker, damage, userData, hitNormal, projectileUuid )";
            }
            else
            {
                guard =
                    "function Growbed.server_onProjectile( self, hitPos, hitTime, hitVelocity, _, attacker, damage, userData, hitNormal, projectileUuid )";
            }
            AdaptivePatchSupport.RequireUnique(
                text, guard,
                target.DisplayName + " projectile callback");
        }

        private static List<ChemicalPatchTarget> GetTargets()
        {
            return new List<ChemicalPatchTarget>
            {
                Target(
                    Path.Combine("Survival", "Scripts", "game", "worlds", "BaseWorld.lua"),
                    "BaseWorld.lua", PatchBaseWorld, UnpatchBaseWorld,
                    Variant(BaseWorldOriginal, BaseWorldChemicalPatched, false)),
                Target(
                    Path.Combine("Survival", "Scripts", "game", "harvestable", "HarvestableSoil.lua"),
                    "HarvestableSoil.lua", PatchSoil, UnpatchSoil,
                    Variant(SoilOriginal, SoilChemicalPatched, true)),
                Target(
                    Path.Combine("Survival", "Scripts", "game", "harvestable", "GrowingHarvestable.lua"),
                    "GrowingHarvestable.lua", PatchGrowing, UnpatchGrowing,
                    Variant(GrowingOriginal, GrowingOriginalChemicalPatched, false),
                    Variant(GrowingRaidPatched, GrowingRaidChemicalPatched, false),
                    Variant(GrowingCumulativePatched, GrowingCumulativeChemicalPatched, false)),
                Target(
                    Path.Combine("Survival", "Scripts", "game", "interactables", "Growbed.lua"),
                    "Growbed.lua", PatchGrowbed, UnpatchGrowbed,
                    Variant(GrowbedOriginal, GrowbedOriginalChemicalPatched, false),
                    Variant(GrowbedHotfixPatched, GrowbedHotfixChemicalPatched, false))
            };
        }

        private static ChemicalPatchTarget Target(
            string relativePath, string displayName,
            Func<string, string> patch, Func<string, string> unpatch,
            params ChemicalPatchVariant[] variants)
        {
            return new ChemicalPatchTarget
            {
                RelativePath = relativePath,
                DisplayName = displayName,
                Patch = patch,
                Unpatch = unpatch,
                Variants = new List<ChemicalPatchVariant>(variants)
            };
        }

        private static ChemicalPatchVariant Variant(
            string baseHash, string patchedHash, bool restoreCrLf)
        {
            return new ChemicalPatchVariant
            {
                BaseHash = baseHash,
                PatchedHash = patchedHash,
                RestoreCrLf = restoreCrLf
            };
        }

        private const string WorldFertilizerHelpers =
            "local RaidRescueChemicalSplashRadius = 2.5\n" +
            "local RaidRescueGrowbedSet = {\n" +
            "\t[tostring( obj_interactive_growbed_soil )] = true,\n" +
            "\t[tostring( obj_interactive_growbed_potato_sprout )] = true,\n" +
            "\t[tostring( obj_interactive_growbed_potato_mature )] = true,\n" +
            "\t[tostring( obj_interactive_growbed_banana_sprout )] = true,\n" +
            "\t[tostring( obj_interactive_growbed_banana_mature )] = true,\n" +
            "\t[tostring( obj_interactive_growbed_blueberry_sprout )] = true,\n" +
            "\t[tostring( obj_interactive_growbed_blueberry_mature )] = true,\n" +
            "\t[tostring( obj_interactive_growbed_broccoli_sprout )] = true,\n" +
            "\t[tostring( obj_interactive_growbed_broccoli_mature )] = true,\n" +
            "\t[tostring( obj_interactive_growbed_carrot_sprout )] = true,\n" +
            "\t[tostring( obj_interactive_growbed_carrot_mature )] = true,\n" +
            "\t[tostring( obj_interactive_growbed_chili_sprout )] = true,\n" +
            "\t[tostring( obj_interactive_growbed_chili_mature )] = true,\n" +
            "\t[tostring( obj_interactive_growbed_cotton_sprout )] = true,\n" +
            "\t[tostring( obj_interactive_growbed_cotton_mature )] = true,\n" +
            "\t[tostring( obj_interactive_growbed_orange_sprout )] = true,\n" +
            "\t[tostring( obj_interactive_growbed_orange_mature )] = true,\n" +
            "\t[tostring( obj_interactive_growbed_pigmentflower_sprout )] = true,\n" +
            "\t[tostring( obj_interactive_growbed_pigmentflower_mature )] = true,\n" +
            "\t[tostring( obj_interactive_growbed_pineapple_sprout )] = true,\n" +
            "\t[tostring( obj_interactive_growbed_pineapple_mature )] = true,\n" +
            "\t[tostring( obj_interactive_growbed_redbeet_sprout )] = true,\n" +
            "\t[tostring( obj_interactive_growbed_redbeet_mature )] = true,\n" +
            "\t[tostring( obj_interactive_growbed_tomato_sprout )] = true,\n" +
            "\t[tostring( obj_interactive_growbed_tomato_mature )] = true\n" +
            "}\n\n" +
            "local function RaidRescueFertilizeTarget( target )\n" +
            "\tif not target or not sm.exists( target ) then\n" +
            "\t\treturn\n" +
            "\tend\n" +
            "\tlocal targetType = type( target )\n" +
            "\tif targetType == \"Harvestable\" and WaterSplashableSet[tostring( target.uuid )] then\n" +
            "\t\tsm.event.sendToHarvestable( target, \"sv_e_raidRescueChemicalFertilize\" )\n" +
            "\telseif targetType == \"Shape\" and RaidRescueGrowbedSet[tostring( target.uuid )] and target.interactable then\n" +
            "\t\tsm.event.sendToInteractable( target.interactable, \"sv_e_raidRescueChemicalFertilize\" )\n" +
            "\tend\n" +
            "end\n\n" +
            "local function RaidRescueFertilizeSplash( world, position )\n" +
            "\tlocal contacts = sm.physics.getSphereContacts( position, RaidRescueChemicalSplashRadius, world )\n" +
            "\tfor _, harvestable in ipairs( contacts.harvestables or {} ) do\n" +
            "\t\tRaidRescueFertilizeTarget( harvestable )\n" +
            "\tend\n" +
            "\tfor _, body in ipairs( contacts.bodies or {} ) do\n" +
            "\t\tif sm.exists( body ) then\n" +
            "\t\t\tfor _, shape in ipairs( body:getShapes() ) do\n" +
            "\t\t\t\tRaidRescueFertilizeTarget( shape )\n" +
            "\t\t\tend\n" +
            "\t\tend\n" +
            "\tend\n" +
            "end\n\n";

        private static string PatchBaseWorld(string text)
        {
            text = ReplaceUnique(
                text,
                "}\n\nlocal NuggetProjectiles = {",
                "}\n\n" +
                "-- RAID RESCUE SECRET MOD: server-authoritative chemical fertilizing.\n" +
                WorldFertilizerHelpers +
                "local NuggetProjectiles = {",
                "chemical fertilizer world helpers");
            return ReplaceUnique(
                text,
                "\telseif projectileUuid == projectile_pesticide then\n" +
                "\t\tlocal forward = sm.vec3.new( 0, 1, 0 )",
                "\telseif projectileUuid == projectile_chemical then\n" +
                "\t\tRaidRescueFertilizeTarget( target )\n" +
                "\telseif projectileUuid == projectile_pesticide then\n" +
                "\t\tRaidRescueFertilizeTarget( target )\n" +
                "\t\tRaidRescueFertilizeSplash( self.world, hitPos )\n" +
                "\t\tlocal forward = sm.vec3.new( 0, 1, 0 )",
                "chemical and pesticide impact dispatch");
        }

        private static string UnpatchBaseWorld(string text)
        {
            text = ReplaceUnique(
                text,
                "\telseif projectileUuid == projectile_chemical then\n" +
                "\t\tRaidRescueFertilizeTarget( target )\n" +
                "\telseif projectileUuid == projectile_pesticide then\n" +
                "\t\tRaidRescueFertilizeTarget( target )\n" +
                "\t\tRaidRescueFertilizeSplash( self.world, hitPos )\n" +
                "\t\tlocal forward = sm.vec3.new( 0, 1, 0 )",
                "\telseif projectileUuid == projectile_pesticide then\n" +
                "\t\tlocal forward = sm.vec3.new( 0, 1, 0 )",
                "chemical and pesticide impact dispatch");
            return ReplaceUnique(
                text,
                "}\n\n" +
                "-- RAID RESCUE SECRET MOD: server-authoritative chemical fertilizing.\n" +
                WorldFertilizerHelpers +
                "local NuggetProjectiles = {",
                "}\n\nlocal NuggetProjectiles = {",
                "chemical fertilizer world helpers");
        }

        private const string SoilFertilizerEvent =
            "function HarvestableSoil.sv_e_raidRescueChemicalFertilize( self )\n" +
            "\tif not self.sv.saved.fertilizer then\n" +
            "\t\tself.sv.saved.fertilizer = true\n" +
            "\t\tself:sv_saveAndSync()\n" +
            "\tend\n" +
            "end\n\n";

        private static string PatchSoil(string text)
        {
            return ReplaceUnique(
                text,
                "function HarvestableSoil.sv_e_fertilize( self, params )",
                "-- RAID RESCUE SECRET MOD: no-cost fertilizer event for chemical impacts.\n" +
                SoilFertilizerEvent +
                "function HarvestableSoil.sv_e_fertilize( self, params )",
                "soil chemical fertilizer event");
        }

        private static string UnpatchSoil(string text)
        {
            return ReplaceUnique(
                text,
                "-- RAID RESCUE SECRET MOD: no-cost fertilizer event for chemical impacts.\n" +
                SoilFertilizerEvent +
                "function HarvestableSoil.sv_e_fertilize( self, params )",
                "function HarvestableSoil.sv_e_fertilize( self, params )",
                "soil chemical fertilizer event");
        }

        private const string GrowingFertilizerEvent =
            "function GrowingHarvestable.sv_e_raidRescueChemicalFertilize( self )\n" +
            "\tif not self.sv.saved.fertilizeTick then\n" +
            "\t\tself.sv.saved.fertilizeTick = sm.game.getCurrentTick()\n" +
            "\t\tself:sv_saveAndSync()\n" +
            "\tend\n" +
            "end\n\n";

        private static string PatchGrowing(string text)
        {
            text = ReplaceUnique(
                text,
                "local IgnoreProjectiles = {\n\tprojectile_colorblob\n}",
                "local IgnoreProjectiles = {\n" +
                "\tprojectile_colorblob,\n" +
                "\tprojectile_pesticide -- RAID RESCUE SECRET MOD: Farmbot splash fertilizes instead.\n" +
                "}",
                "Farmbot crop-protection projectile list");
            return ReplaceUnique(
                text,
                "function GrowingHarvestable.sv_e_fertilize( self, params )",
                "-- RAID RESCUE SECRET MOD: no-cost fertilizer event for chemical impacts.\n" +
                GrowingFertilizerEvent +
                "function GrowingHarvestable.sv_e_fertilize( self, params )",
                "growing-crop chemical fertilizer event");
        }

        private static string UnpatchGrowing(string text)
        {
            text = ReplaceUnique(
                text,
                "-- RAID RESCUE SECRET MOD: no-cost fertilizer event for chemical impacts.\n" +
                GrowingFertilizerEvent +
                "function GrowingHarvestable.sv_e_fertilize( self, params )",
                "function GrowingHarvestable.sv_e_fertilize( self, params )",
                "growing-crop chemical fertilizer event");
            return ReplaceUnique(
                text,
                "local IgnoreProjectiles = {\n" +
                "\tprojectile_colorblob,\n" +
                "\tprojectile_pesticide -- RAID RESCUE SECRET MOD: Farmbot splash fertilizes instead.\n" +
                "}",
                "local IgnoreProjectiles = {\n\tprojectile_colorblob\n}",
                "Farmbot crop-protection projectile list");
        }

        private const string GrowbedFertilizerEvent =
            "function Growbed.sv_e_raidRescueChemicalFertilize( self )\n" +
            "\tif not self.sv.saved.fertilizer then\n" +
            "\t\tself:sv_performUpdate()\n" +
            "\t\tself.sv.saved.fertilizer = true\n" +
            "\t\tself:sv_saveAndSynch()\n" +
            "\tend\n" +
            "end\n\n";

        private static string PatchGrowbed(string text)
        {
            text = ReplaceUnique(
                text,
                "\t\tif type( attacker ) == \"Unit\" then\n" +
                "\t\t\tif self:sv_canReplace() then",
                "\t\tif type( attacker ) == \"Unit\" and projectileUuid ~= projectile_pesticide then\n" +
                "\t\t\tif self:sv_canReplace() then",
                "Farmbot growbed-protection condition");
            return ReplaceUnique(
                text,
                "function Growbed.sv_e_fertilize( self, params )",
                "-- RAID RESCUE SECRET MOD: no-cost fertilizer event for chemical impacts.\n" +
                GrowbedFertilizerEvent +
                "function Growbed.sv_e_fertilize( self, params )",
                "growbed chemical fertilizer event");
        }

        private static string UnpatchGrowbed(string text)
        {
            text = ReplaceUnique(
                text,
                "-- RAID RESCUE SECRET MOD: no-cost fertilizer event for chemical impacts.\n" +
                GrowbedFertilizerEvent +
                "function Growbed.sv_e_fertilize( self, params )",
                "function Growbed.sv_e_fertilize( self, params )",
                "growbed chemical fertilizer event");
            return ReplaceUnique(
                text,
                "\t\tif type( attacker ) == \"Unit\" and projectileUuid ~= projectile_pesticide then\n" +
                "\t\t\tif self:sv_canReplace() then",
                "\t\tif type( attacker ) == \"Unit\" then\n" +
                "\t\t\tif self:sv_canReplace() then",
                "Farmbot growbed-protection condition");
        }

        private static string ReplaceUnique(
            string text, string oldText, string newText, string description)
        {
            int first = text.IndexOf(oldText, StringComparison.Ordinal);
            if (first < 0 ||
                text.IndexOf(oldText, first + oldText.Length, StringComparison.Ordinal) >= 0)
            {
                throw new InvalidDataException(
                    "The expected " + description + " code was not found exactly once.");
            }
            return text.Substring(0, first) + newText +
                text.Substring(first + oldText.Length);
        }

        private static void WriteManifest(
            string backupPath, string gamePath, string version, bool enabled,
            List<ChemicalPatchTarget> targets,
            Dictionary<ChemicalPatchTarget, string> hashes)
        {
            StringBuilder manifest = new StringBuilder();
            manifest.AppendLine("ScrapLab secret-mod backup");
            manifest.AppendLine("Mod: Chemical Fertilizer Splash");
            manifest.AppendLine("Action: " + (enabled ? "Install" : "Remove"));
            manifest.AppendLine("Game path: " + gamePath);
            manifest.AppendLine("Game version: " + version);
            manifest.AppendLine("Created: " + DateTime.Now.ToString("O"));
            foreach (ChemicalPatchTarget target in targets)
            {
                manifest.AppendLine(
                    target.DisplayName + " SHA-256 " + hashes[target]);
            }
            File.WriteAllText(
                Path.Combine(backupPath, "MANIFEST.txt"),
                manifest.ToString(), new UTF8Encoding(false));
        }

        private static void RollBack(
            List<ChemicalPatchTarget> replaced,
            Dictionary<ChemicalPatchTarget, string> paths,
            string backupPath,
            Dictionary<ChemicalPatchTarget, string> hashes)
        {
            List<string> failures = new List<string>();
            foreach (ChemicalPatchTarget target in replaced)
            {
                try
                {
                    string backup = Path.Combine(
                        backupPath, Path.GetFileName(target.RelativePath));
                    File.Copy(backup, paths[target], true);
                    if (!HashEquals(Sha256(paths[target]), hashes[target]))
                        failures.Add(target.DisplayName);
                }
                catch
                {
                    failures.Add(target.DisplayName);
                }
            }
            if (failures.Count > 0)
            {
                throw new IOException(
                    "The chemical-fertilizer update failed and automatic rollback could not " +
                    "restore: " + String.Join(", ", failures.ToArray()) +
                    ". The verified backups remain in " + backupPath);
            }
        }

        private static void ReplaceFile(string path, string text)
        {
            string temporary = path + ".raidrescue-chemical-" +
                Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, text, new UTF8Encoding(false));
                File.Replace(temporary, path, null);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }

        private static string ReadUtf8(string path)
        {
            return File.ReadAllText(path, new UTF8Encoding(false, true));
        }

        private static string NormalizeNewlines(string value)
        {
            return value.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        private static string Sha256(string path)
        {
            using (FileStream stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (SHA256 algorithm = SHA256.Create())
                return BytesToHex(algorithm.ComputeHash(stream));
        }

        private static string Sha256(byte[] value)
        {
            using (SHA256 algorithm = SHA256.Create())
                return BytesToHex(algorithm.ComputeHash(value));
        }

        private static string BytesToHex(byte[] value)
        {
            StringBuilder text = new StringBuilder(value.Length * 2);
            foreach (byte item in value)
                text.Append(item.ToString("X2"));
            return text.ToString();
        }

        private static bool HashEquals(string left, string right)
        {
            return String.Equals(left, right, StringComparison.OrdinalIgnoreCase);
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

#if LEGACY_SELF_HELPERS
    internal static class ChemicalFertilizerPatchLauncher
    {
        private const string HelperSwitch = "--set-chemical-fertilizer-mod";

        public static bool TryRunHelper(string[] args)
        {
            if (args == null || args.Length == 0 ||
                !String.Equals(args[0], HelperSwitch, StringComparison.Ordinal))
                return false;
            if (args.Length != 3)
                return true;

            bool enabled;
            if (args[1] == "1")
                enabled = true;
            else if (args[1] == "0")
                enabled = false;
            else
                return true;

            string resultPath = args[2];
            GamePatchResult result;
            bool resultPathIsValid = false;
            try
            {
                ValidateResultPath(resultPath);
                resultPathIsValid = true;
                result = ChemicalFertilizerPatchService.SetEnabled(enabled);
            }
            catch (Exception exception)
            {
                result = Failure(exception.Message);
            }

            try
            {
                if (resultPathIsValid)
                {
                    JavaScriptSerializer serializer =
                        new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
                    File.WriteAllText(
                        resultPath, serializer.Serialize(result),
                        new UTF8Encoding(false));
                }
            }
            catch { }
            return true;
        }

        public static GamePatchResult SetEnabled(bool enabled)
        {
            if (GamePatchService.IsGameRunning())
                return Failure(
                    "Scrap Mechanic is running. Close the game completely before changing secret mods.");

            if (IsAdministrator())
                return ChemicalFertilizerPatchService.SetEnabled(enabled);
            return ElevatedPatchBroker.Execute(
                ElevatedPatchBroker.ChemicalDirectAction, enabled, "");
        }

        private static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private static void ValidateResultPath(string path)
        {
            string directory = Path.GetFullPath(GetResultDirectory())
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(path);
            if (!full.StartsWith(directory, StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(
                    Path.GetExtension(full), ".json",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The secret-mod result path is invalid.");
            }
        }

        private static string GetResultDirectory()
        {
            return Path.Combine(
                Path.GetTempPath(), "ScrapLab", "patch-results");
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
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

#endif

    internal static class DualFluidCannonPatchService
    {
        private const string AdaptiveModKey = "DualFluidCannon";
        private const string AdaptiveDefinitionVersion = "2";
        private const string MountedWaterGunOriginal =
            "D25D12F0C1DE5C1F3FD62C315E9BD438777D45A38B6ECD1BDE8CF7F2E8B3B3FA";
        private const string MountedWaterGunPatched =
            "DD125A0F78B15FF321027AC91F7743D637343DFC8B29F0C18BB5AE71F33F82D5";
        private static readonly string MountedWaterGunRelativePath = Path.Combine(
            "Survival", "Scripts", "game", "interactables", "MountedWaterGun.lua");

        private const string OriginalDeclaration =
            "MountedWaterGun.maxParentCount = 2\n" +
            "MountedWaterGun.maxChildCount = 0\n" +
            "MountedWaterGun.connectionInput = bit.bor( sm.interactable.connectionType.logic, sm.interactable.connectionType.water )";
        private const string PatchedDeclaration =
            "MountedWaterGun.maxParentCount = 3\n" +
            "MountedWaterGun.maxChildCount = 0\n" +
            "MountedWaterGun.connectionInput = bit.bor( sm.interactable.connectionType.logic, sm.interactable.connectionType.water, sm.interactable.connectionType.chemical )";

        private const string OriginalFireBlock =
            "-- Attempt to fire a projectile\n" +
            "function MountedWaterGun.sv_tryFire( self )\n" +
            "\tlocal logicInteractable, waterInteractable = self:getInputs()\n" +
            "\tlocal active = logicInteractable and logicInteractable:isActive() or false\n" +
            "\tlocal waterContainer = waterInteractable and waterInteractable:getContainer( 0 ) or nil\n" +
            "\tlocal ownContainer = self.interactable:getContainer( 0 )\n" +
            "\tlocal freeFire = not sm.game.getEnableAmmoConsumption() and not waterContainer\n\n" +
            "\tif freeFire then\n" +
            "\t\tif active and not self.sv.parentActive and self.sv.canFire then\n" +
            "\t\t\tself:sv_fire()\n" +
            "\t\tend\n" +
            "\telse\n" +
            "\t\tif active then\n" +
            "\t\t\tlocal success = false\n" +
            "\t\t\tif not self.sv.parentActive and self.sv.canFire then\n" +
            "\t\t\t\tif waterContainer then\n" +
            "\t\t\t\t\tsm.container.beginTransaction()\n" +
            "\t\t\t\t\tsm.container.spend( waterContainer, obj_consumable_water, 1 )\n" +
            "\t\t\t\t\tif sm.container.endTransaction() then\n" +
            "\t\t\t\t\t\tself:sv_fire()\n" +
            "\t\t\t\t\t\tsuccess = true\n" +
            "\t\t\t\t\tend\n" +
            "\t\t\t\tend\n" +
            "\t\t\t\tif not success and ownContainer then\n" +
            "\t\t\t\t\tsm.container.beginTransaction()\n" +
            "\t\t\t\t\tsm.container.spend( ownContainer, obj_consumable_water, 1 )\n" +
            "\t\t\t\t\tif sm.container.endTransaction() then\n" +
            "\t\t\t\t\t\tself:sv_fire()\n" +
            "\t\t\t\t\t\tsuccess = true\n" +
            "\t\t\t\t\tend\n" +
            "\t\t\t\tend\n" +
            "\t\t\tend\n\n" +
            "\t\tend\n" +
            "\tend\n" +
            "end\n\n" +
            "function MountedWaterGun.sv_fire( self )\n" +
            "\tself.sv.canFire = false\n" +
            "\tlocal firePos = sm.vec3.new( 0.0, 0.0, 0.375 )\n\n" +
            "\t-- Fire projectile from the shape\n" +
            "\tsm.projectile.shapeFire( self.shape, projectile_water, firePos, sm.vec3.new( 0, 0, 1 ) * Force, 0 )\n\n" +
            "\tself.network:sendToClients( \"cl_onShoot\" )\n" +
            "end";

        private const string PatchedFireBlock =
            "-- Attempt to fire every available liquid on one rising-edge trigger.\n" +
            "function MountedWaterGun.sv_tryFire( self )\n" +
            "\tlocal logicInteractable, waterInteractable, chemicalInteractable = self:getInputs()\n" +
            "\tlocal active = logicInteractable and logicInteractable:isActive() or false\n" +
            "\tif not active or self.sv.parentActive or not self.sv.canFire then\n" +
            "\t\treturn\n" +
            "\tend\n\n" +
            "\tlocal fireWater = false\n" +
            "\tlocal fireChemical = false\n" +
            "\tif not sm.game.getEnableAmmoConsumption() then\n" +
            "\t\tfireWater = true\n" +
            "\t\tfireChemical = chemicalInteractable ~= nil\n" +
            "\telse\n" +
            "\t\tlocal waterContainer = waterInteractable and waterInteractable:getContainer( 0 ) or nil\n" +
            "\t\tlocal chemicalContainer = chemicalInteractable and chemicalInteractable:getContainer( 0 ) or nil\n" +
            "\t\tlocal ownContainer = self.interactable:getContainer( 0 )\n\n" +
            "\t\tif waterContainer then\n" +
            "\t\t\tsm.container.beginTransaction()\n" +
            "\t\t\tsm.container.spend( waterContainer, obj_consumable_water, 1 )\n" +
            "\t\t\tfireWater = sm.container.endTransaction()\n" +
            "\t\tend\n" +
            "\t\tif not fireWater and ownContainer then\n" +
            "\t\t\tsm.container.beginTransaction()\n" +
            "\t\t\tsm.container.spend( ownContainer, obj_consumable_water, 1 )\n" +
            "\t\t\tfireWater = sm.container.endTransaction()\n" +
            "\t\tend\n" +
            "\t\tif chemicalContainer then\n" +
            "\t\t\tsm.container.beginTransaction()\n" +
            "\t\t\tsm.container.spend( chemicalContainer, obj_consumable_chemical, 1 )\n" +
            "\t\t\tfireChemical = sm.container.endTransaction()\n" +
            "\t\tend\n" +
            "\tend\n\n" +
            "\tif fireWater or fireChemical then\n" +
            "\t\tself:sv_fire( fireWater, fireChemical )\n" +
            "\tend\n" +
            "end\n\n" +
            "function MountedWaterGun.sv_fire( self, fireWater, fireChemical )\n" +
            "\tself.sv.canFire = false\n" +
            "\tlocal firePos = sm.vec3.new( 0.0, 0.0, 0.375 )\n" +
            "\tlocal fireVelocity = sm.vec3.new( 0, 0, 1 ) * Force\n\n" +
            "\t-- Both projectiles intentionally share the same muzzle path and game tick.\n" +
            "\tif fireWater then\n" +
            "\t\tsm.projectile.shapeFire( self.shape, projectile_water, firePos, fireVelocity, 0 )\n" +
            "\tend\n" +
            "\tif fireChemical then\n" +
            "\t\tsm.projectile.shapeFire( self.shape, projectile_chemical, firePos, fireVelocity, 0 )\n" +
            "\tend\n\n" +
            "\tself.network:sendToClients( \"cl_onShoot\" )\n" +
            "end";

        private const string OriginalConnectionCount =
            "function MountedWaterGun.client_getAvailableParentConnectionCount( self, connectionType )\n" +
            "\tif bit.band( connectionType, sm.interactable.connectionType.logic ) ~= 0 then\n" +
            "\t\treturn 1 - #self.interactable:getParents( sm.interactable.connectionType.logic )\n" +
            "\tend\n" +
            "\tif bit.band( connectionType, sm.interactable.connectionType.water ) ~= 0 then\n" +
            "\t\treturn 1 - #self.interactable:getParents( sm.interactable.connectionType.water )\n" +
            "\tend\n" +
            "\treturn 0\n" +
            "end";
        private const string PatchedConnectionCount =
            "function MountedWaterGun.client_getAvailableParentConnectionCount( self, connectionType )\n" +
            "\tif bit.band( connectionType, sm.interactable.connectionType.logic ) ~= 0 then\n" +
            "\t\treturn 1 - #self.interactable:getParents( sm.interactable.connectionType.logic )\n" +
            "\tend\n" +
            "\tif bit.band( connectionType, sm.interactable.connectionType.water ) ~= 0 then\n" +
            "\t\treturn 1 - #self.interactable:getParents( sm.interactable.connectionType.water )\n" +
            "\tend\n" +
            "\tif bit.band( connectionType, sm.interactable.connectionType.chemical ) ~= 0 then\n" +
            "\t\treturn 1 - #self.interactable:getParents( sm.interactable.connectionType.chemical )\n" +
            "\tend\n" +
            "\treturn 0\n" +
            "end";

        private const string OriginalInputs =
            "function MountedWaterGun.getInputs( self )\n" +
            "\tlocal logicInteractable = nil\n" +
            "\tlocal waterInteractable = nil\n" +
            "\tlocal parents = self.interactable:getParents()\n" +
            "\tif parents[2] then\n" +
            "\t\tif parents[2]:hasOutputType( sm.interactable.connectionType.logic ) then\n" +
            "\t\t\tlogicInteractable = parents[2]\n" +
            "\t\telseif parents[2]:hasOutputType( sm.interactable.connectionType.water ) then\n" +
            "\t\t\twaterInteractable = parents[2]\n" +
            "\t\tend\n" +
            "\tend\n" +
            "\tif parents[1] then\n" +
            "\t\tif parents[1]:hasOutputType( sm.interactable.connectionType.logic ) then\n" +
            "\t\t\tlogicInteractable = parents[1]\n" +
            "\t\telseif parents[1]:hasOutputType( sm.interactable.connectionType.water ) then\n" +
            "\t\t\twaterInteractable = parents[1]\n" +
            "\t\tend\n" +
            "\tend\n\n" +
            "\treturn logicInteractable, waterInteractable\n" +
            "end";
        private const string PatchedInputs =
            "function MountedWaterGun.getInputs( self )\n" +
            "\tlocal logicInteractable = nil\n" +
            "\tlocal waterInteractable = nil\n" +
            "\tlocal chemicalInteractable = nil\n" +
            "\tfor _, parent in ipairs( self.interactable:getParents() ) do\n" +
            "\t\tif parent:hasOutputType( sm.interactable.connectionType.logic ) then\n" +
            "\t\t\tlogicInteractable = parent\n" +
            "\t\telseif parent:hasOutputType( sm.interactable.connectionType.water ) then\n" +
            "\t\t\twaterInteractable = parent\n" +
            "\t\telseif parent:hasOutputType( sm.interactable.connectionType.chemical ) then\n" +
            "\t\t\tchemicalInteractable = parent\n" +
            "\t\tend\n" +
            "\tend\n\n" +
            "\treturn logicInteractable, waterInteractable, chemicalInteractable\n" +
            "end";

        private sealed class AdaptiveCannonState
        {
            internal LuaTextDocument Document;
            internal bool Clean;
            internal bool Installed;
            internal bool Partial;
            internal string Reason;
        }

        public static GamePatchResult GetStatus()
        {
            GamePatchResult result = new GamePatchResult { Changes = new List<string>() };
            try
            {
                string gamePath = GamePatchService.FindGameInstall();
                if (String.IsNullOrEmpty(gamePath))
                    throw new InvalidOperationException("Scrap Mechanic was not found.");
                result.GamePath = gamePath;
                string executable = Path.Combine(gamePath, "Release", "ScrapMechanic.exe");
                result.GameVersion = FileVersionInfo.GetVersionInfo(executable).FileVersion;
                string path = Path.Combine(gamePath, MountedWaterGunRelativePath);
                if (!File.Exists(path))
                    throw new FileNotFoundException("MountedWaterGun.lua was not found.", path);
                string hash = Sha256(path);
                SteamBuildInfo build = AdaptivePatchSupport.GetSteamBuild(
                    gamePath, result.GameVersion);
                if (HashEquals(hash, MountedWaterGunPatched))
                {
                    if (AdaptivePatchSupport.RequiresBuildRefresh(
                        AdaptiveModKey, build))
                    {
                        AdaptivePatchSupport.MarkRefreshRequired(
                            result, build, null);
                        return result;
                    }
                    result.Installed = true;
                    result.AlreadyPatched = true;
                    result.Success = true;
                    AdaptivePatchSupport.FillResult(
                        result, build,
                        PatchCompatibilityState.KnownInstalled,
                        false, true, "Verified ScrapLab file.");
                    return result;
                }
                if (HashEquals(hash, MountedWaterGunOriginal))
                {
                    AdaptivePatchSupport.DiscardReceiptIfSuperseded(
                        AdaptiveModKey, gamePath);
                    result.Installed = false;
                    result.Success = true;
                    AdaptivePatchSupport.FillResult(
                        result, build,
                        PatchCompatibilityState.KnownClean,
                        false, true, "Verified official file.");
                    return result;
                }
                return GetAdaptiveStatus(result, gamePath, path, build);
            }
            catch (Exception exception)
            {
                result.Success = false;
                result.Error = exception.Message;
            }
            return result;
        }

        internal static bool IsInstalledAt(string gamePath)
        {
            string path = Path.Combine(gamePath, MountedWaterGunRelativePath);
            if (!File.Exists(path))
                throw new FileNotFoundException("MountedWaterGun.lua was not found.", path);
            string hash = Sha256(path);
            if (HashEquals(hash, MountedWaterGunPatched))
                return true;
            if (HashEquals(hash, MountedWaterGunOriginal))
                return false;
            AdaptiveCannonState state = ProbeAdaptiveCannon(path);
            if (state.Installed)
                return true;
            if (state.Clean)
                return false;
            throw new InvalidOperationException(state.Reason);
        }

        internal static bool PreflightAt(string gamePath, bool enabled)
        {
            string executable = Path.Combine(
                gamePath, "Release", "ScrapMechanic.exe");
            if (!File.Exists(executable))
                throw new FileNotFoundException(
                    "ScrapMechanic.exe was not found.", executable);
            string gameVersion =
                FileVersionInfo.GetVersionInfo(executable).FileVersion;
            string path = Path.Combine(
                gamePath, MountedWaterGunRelativePath);
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    "MountedWaterGun.lua was not found.", path);
            string hash = Sha256(path);
            if (HashEquals(hash, MountedWaterGunPatched))
                return true;
            if (HashEquals(hash, MountedWaterGunOriginal))
                return false;

            AdaptiveCannonState state = ProbeAdaptiveCannon(path);
            if (enabled)
            {
                if (state.Installed)
                    return true;
                string reason = "";
                SteamBuildInfo build =
                    AdaptivePatchSupport.GetSteamBuild(
                        gamePath, gameVersion);
                if (!state.Clean ||
                    !AdaptivePatchSupport.CanAdaptCleanFiles(
                        build, new[] { path }, out reason))
                {
                    throw new InvalidOperationException(
                        "Dual-Fluid Water Cannon dependency preflight failed: " +
                        (state.Clean ? reason : state.Reason));
                }
                state.Document.Render(
                    Patch(state.Document.NormalizedText));
                return false;
            }

            if (state.Clean)
                return false;
            if (!state.Installed)
            {
                throw new InvalidOperationException(
                    "Dual-Fluid Water Cannon dependency preflight failed: " +
                    state.Reason);
            }
            state.Document.Render(
                Unpatch(state.Document.NormalizedText));
            return true;
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
                string executable = Path.Combine(gamePath, "Release", "ScrapMechanic.exe");
                if (!File.Exists(executable))
                    throw new FileNotFoundException("ScrapMechanic.exe was not found.", executable);
                result.GameVersion = FileVersionInfo.GetVersionInfo(executable).FileVersion;

                string path = Path.Combine(gamePath, MountedWaterGunRelativePath);
                if (!File.Exists(path))
                    throw new FileNotFoundException("MountedWaterGun.lua was not found.", path);
                string currentHash = Sha256(path);
                string desiredHash = enabled
                    ? MountedWaterGunPatched
                    : MountedWaterGunOriginal;
                if (HashEquals(currentHash, desiredHash))
                {
                    SteamBuildInfo build =
                        AdaptivePatchSupport.GetSteamBuild(
                            gamePath, result.GameVersion);
                    if (enabled &&
                        AdaptivePatchSupport.RequiresBuildRefresh(
                            AdaptiveModKey, build))
                    {
                        AdaptivePatchSupport.PrepareBuildRefresh(
                            result, AdaptiveModKey, build,
                            "Dual-Fluid Water Cannon was reactivated after the Steam update.");
                        return result;
                    }
                    result.Success = true;
                    result.AlreadyPatched = true;
                    if (!enabled)
                        AdaptivePatchSupport.DeleteBuildActivation(
                            AdaptiveModKey);
                    result.Changes.Add(
                        enabled
                            ? "Dual-Fluid Water Cannon is already installed."
                            : "Dual-Fluid Water Cannon is already removed.");
                    AdaptivePatchSupport.FillResult(
                        result,
                        AdaptivePatchSupport.GetSteamBuild(
                            gamePath, result.GameVersion),
                        enabled
                            ? PatchCompatibilityState.KnownInstalled
                            : PatchCompatibilityState.KnownClean,
                        false, true, "Verified file already has the requested state.");
                    return result;
                }

                if (!HashEquals(currentHash, MountedWaterGunOriginal) &&
                    !HashEquals(currentHash, MountedWaterGunPatched))
                {
                    return SetAdaptiveEnabledAt(
                        gamePath, backupRoot, enabled,
                        result, path, currentHash);
                }

                string source = NormalizeNewlines(ReadUtf8(path));
                string transformed;
                if (enabled && HashEquals(currentHash, MountedWaterGunOriginal))
                    transformed = Patch(source);
                else if (!enabled && HashEquals(currentHash, MountedWaterGunPatched))
                    transformed = Unpatch(source).Replace("\n", "\r\n");
                else
                    throw new InvalidOperationException(
                        "MountedWaterGun.lua does not match a verified original or ScrapLab " +
                        "state. No files were changed. Use Steam Verify if another mod changed it.");

                string generatedHash = Sha256(Encoding.UTF8.GetBytes(transformed));
                if (!HashEquals(generatedHash, desiredHash))
                {
                    throw new InvalidOperationException(
                        "The generated Dual-Fluid Water Cannon patch did not match its verified checksum.");
                }

                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
                string backupPath = Path.Combine(
                    backupRoot,
                    (enabled ? "Install-" : "Remove-") +
                    "DualFluidWaterCannon-" + stamp);
                Directory.CreateDirectory(backupPath);
                result.BackupPath = backupPath;
                string backupFile = Path.Combine(backupPath, "MountedWaterGun.lua");
                File.Copy(path, backupFile, false);
                if (!HashEquals(Sha256(backupFile), currentHash))
                    throw new IOException("The MountedWaterGun backup failed checksum verification.");

                StringBuilder manifest = new StringBuilder();
                manifest.AppendLine("ScrapLab secret-mod backup");
                manifest.AppendLine("Mod: Dual-Fluid Water Cannon");
                manifest.AppendLine("Action: " + (enabled ? "Install" : "Remove"));
                manifest.AppendLine("Game path: " + gamePath);
                manifest.AppendLine("Game version: " + result.GameVersion);
                manifest.AppendLine("Created: " + DateTime.Now.ToString("O"));
                manifest.AppendLine("MountedWaterGun.lua SHA-256 " + currentHash);
                File.WriteAllText(
                    Path.Combine(backupPath, "MANIFEST.txt"),
                    manifest.ToString(), new UTF8Encoding(false));

                try
                {
                    ReplaceFile(path, transformed);
                    if (!HashEquals(Sha256(path), desiredHash))
                        throw new IOException(
                            "MountedWaterGun.lua failed its final checksum verification.");
                }
                catch
                {
                    File.Copy(backupFile, path, true);
                    if (!HashEquals(Sha256(path), currentHash))
                    {
                        throw new IOException(
                            "The Dual-Fluid Water Cannon update failed and automatic rollback " +
                            "could not restore MountedWaterGun.lua. The verified backup remains in " +
                            backupPath);
                    }
                    throw;
                }

                result.Success = true;
                result.FilesPatched = 1;
                result.Changes.Add(
                    enabled
                        ? "Mounted water cannons now accept one Water Container and one Chemical Container."
                        : "Removed Dual-Fluid Water Cannon and restored the verified original script.");
                result.Changes.Add(
                    enabled
                        ? "Each rising-edge trigger fires every available liquid with one animation and recoil."
                        : "The original water-only firing behavior was restored.");
                AdaptivePatchSupport.QueueBuildActivation(
                    result, AdaptiveModKey, enabled);
                SecretModBackupRetention.Prune(
                    backupRoot, "DualFluidWaterCannon",
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

        private static GamePatchResult GetAdaptiveStatus(
            GamePatchResult result, string gamePath, string path,
            SteamBuildInfo build)
        {
            AdaptiveCannonState state = ProbeAdaptiveCannon(path);
            result.Success = true;
            result.Installed = state.Installed;
            result.AlreadyPatched = state.Installed;
            if (state.Installed)
            {
                if (AdaptivePatchSupport.RequiresBuildRefresh(
                    AdaptiveModKey, build))
                {
                    AdaptivePatchSupport.MarkRefreshRequired(
                        result, build, null);
                    return result;
                }
                AdaptivePatchSupport.FillResult(
                    result, build,
                    PatchCompatibilityState.AdaptiveInstalled,
                    true, true,
                    "The Dual-Fluid Water Cannon patch is structurally intact.");
                return result;
            }
            if (state.Clean)
            {
                AdaptivePatchSupport.DiscardReceiptIfSuperseded(
                    AdaptiveModKey, gamePath);
                string reason;
                bool canApply = AdaptivePatchSupport.CanAdaptCleanFiles(
                    build, new[] { path }, out reason);
                AdaptivePatchSupport.FillResult(
                    result, build,
                    canApply
                        ? PatchCompatibilityState.CompatibleUpdate
                        : PatchCompatibilityState.OtherModification,
                    canApply, canApply, reason);
                return result;
            }

            AdaptivePatchSupport.FillResult(
                result, build,
                state.Partial
                    ? PatchCompatibilityState.PartialConflict
                    : PatchCompatibilityState.UnsupportedCode,
                false, false, state.Reason);
            return result;
        }

        private static AdaptiveCannonState ProbeAdaptiveCannon(string path)
        {
            LuaTextDocument document = AdaptivePatchSupport.ReadLua(path);
            string text = document.NormalizedText;
            if (document.MixedNewlines)
            {
                return new AdaptiveCannonState
                {
                    Document = document,
                    Partial = HasCannonPatchEvidence(text),
                    Reason = "MountedWaterGun.lua uses mixed newline styles."
                };
            }
            try
            {
                RequireAdaptiveCannonGuards(text);
            }
            catch (Exception exception)
            {
                return new AdaptiveCannonState
                {
                    Document = document,
                    Partial = HasCannonPatchEvidence(text),
                    Reason =
                        "MountedWaterGun.lua is missing a required cannon callback. " +
                        exception.Message
                };
            }

            bool clean = false;
            bool installed = false;
            try
            {
                Patch(text);
                clean = true;
            }
            catch (InvalidDataException) { }
            try
            {
                Unpatch(text);
                installed = true;
            }
            catch (InvalidDataException) { }

            if (clean != installed)
            {
                return new AdaptiveCannonState
                {
                    Document = document,
                    Clean = clean,
                    Installed = installed,
                    Reason = clean
                        ? "Every protected cannon target is an exact clean match."
                        : "Every protected ScrapLab cannon target is intact."
                };
            }

            bool partial = HasCannonPatchEvidence(text) ||
                HasAnyCannonTargetEvidence(text);
            return new AdaptiveCannonState
            {
                Document = document,
                Partial = partial,
                Reason = partial
                    ? "MountedWaterGun.lua contains a partial, duplicated, or edited Dual-Fluid Water Cannon patch."
                    : "The game update changed protected mounted-cannon code."
            };
        }

        private static GamePatchResult SetAdaptiveEnabledAt(
            string gamePath, string backupRoot, bool enabled,
            GamePatchResult result, string path, string currentHash)
        {
            AdaptiveCannonState state = ProbeAdaptiveCannon(path);
            LuaTextDocument document = state.Document;
            SteamBuildInfo build = AdaptivePatchSupport.GetSteamBuild(
                gamePath, result.GameVersion);
            string transformed;
            if (enabled)
            {
                if (state.Installed &&
                    AdaptivePatchSupport.RequiresBuildRefresh(
                        AdaptiveModKey, build))
                {
                    AdaptivePatchSupport.PrepareBuildRefresh(
                        result, AdaptiveModKey, build,
                        "Dual-Fluid Water Cannon was reactivated after the Steam update.");
                    return result;
                }
                string reason = "";
                if (!state.Clean ||
                    !AdaptivePatchSupport.CanAdaptCleanFiles(
                        build, new[] { path }, out reason))
                {
                    throw new InvalidOperationException(
                        "Dual-Fluid Water Cannon cannot be applied: " +
                        (state.Clean ? reason : state.Reason));
                }
                transformed = Patch(document.NormalizedText);
            }
            else
            {
                if (!state.Installed)
                {
                    if (state.Clean)
                    {
                        result.Success = true;
                        result.Installed = false;
                        result.AlreadyPatched = true;
                        AdaptivePatchSupport.FillResult(
                            result, build,
                            PatchCompatibilityState.CompatibleUpdate,
                            true, true,
                            "Dual-Fluid Water Cannon is already removed.");
                        AdaptivePatchSupport.DeleteReceipt(AdaptiveModKey);
                        AdaptivePatchSupport.DeleteBuildActivation(
                            AdaptiveModKey);
                        return result;
                    }
                    throw new InvalidOperationException(
                        "Dual-Fluid Water Cannon cannot be removed: " +
                        state.Reason);
                }
                transformed = Unpatch(document.NormalizedText);
            }

            byte[] outputBytes = document.Render(transformed);
            string outputHash = AdaptivePatchSupport.Sha256(outputBytes);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            string backupPath = Path.Combine(
                backupRoot,
                (enabled ? "Install-" : "Remove-") +
                "DualFluidWaterCannon-" + stamp);
            Directory.CreateDirectory(backupPath);
            result.BackupPath = backupPath;
            string backupFile = Path.Combine(
                backupPath, "MountedWaterGun.lua");
            File.Copy(path, backupFile, false);
            if (!HashEquals(
                AdaptivePatchSupport.Sha256(backupFile), currentHash))
                throw new IOException(
                    "The adaptive MountedWaterGun backup failed checksum verification.");
            AdaptivePatchSupport.WriteBackupManifest(
                backupPath, "Dual-Fluid Water Cannon",
                enabled ? "Install" : "Remove",
                gamePath, build, AdaptiveDefinitionVersion,
                new[]
                {
                    new AdaptivePatchReceiptFile
                    {
                        RelativePath = MountedWaterGunRelativePath,
                        SourceHash = currentHash,
                        OutputHash = outputHash,
                        Newline = document.Newline == "\r\n"
                            ? "CRLF" : "LF",
                        HasBom = document.HasBom
                    }
                });

            AdaptivePatchReceipt receipt =
                AdaptivePatchSupport.LoadReceipt(AdaptiveModKey);
            AdaptivePatchReceiptFile receiptFile =
                AdaptivePatchSupport.FindReceiptFile(
                    receipt, MountedWaterGunRelativePath);
            try
            {
                if (!enabled && receiptFile != null &&
                    HashEquals(currentHash, receiptFile.OutputHash) &&
                    File.Exists(receiptFile.BackupPath) &&
                    HashEquals(
                        AdaptivePatchSupport.Sha256(receiptFile.BackupPath),
                        receiptFile.SourceHash))
                {
                    AdaptivePatchSupport.ReplaceFile(
                        path, File.ReadAllBytes(receiptFile.BackupPath),
                        "dual-fluid-exact-restore");
                    outputHash = receiptFile.SourceHash;
                }
                else
                {
                    AdaptivePatchSupport.ReplaceFile(
                        path, outputBytes, "dual-fluid-adaptive");
                }
                if (!HashEquals(
                    AdaptivePatchSupport.Sha256(path), outputHash))
                    throw new IOException(
                        "MountedWaterGun.lua failed adaptive output verification.");
            }
            catch
            {
                File.Copy(backupFile, path, true);
                if (!HashEquals(
                    AdaptivePatchSupport.Sha256(path), currentHash))
                    throw new IOException(
                        "Adaptive Dual-Fluid Water Cannon rollback could not restore MountedWaterGun.lua.");
                throw;
            }

            result.Success = true;
            result.Installed = enabled;
            result.FilesPatched = 1;
            AdaptivePatchSupport.FillResult(
                result, build,
                enabled
                    ? PatchCompatibilityState.AdaptiveInstalled
                    : PatchCompatibilityState.CompatibleUpdate,
                true, true,
                enabled
                    ? "Installed with exact protected-code matching on this Steam build."
                    : "Removed while preserving unrelated updated cannon code.");
            result.Changes.Add(
                enabled
                    ? "Installed Dual-Fluid Water Cannon on a structurally compatible game update."
                    : "Removed Dual-Fluid Water Cannon without replacing unrelated updated code.");

            if (enabled)
            {
                string activeBase = AdaptivePatchSupport.CaptureBaseBackup(
                    AdaptiveModKey, MountedWaterGunRelativePath,
                    backupFile, currentHash);
                AdaptivePatchSupport.SaveReceipt(
                    AdaptiveModKey,
                    new AdaptivePatchReceipt
                    {
                        ModKey = AdaptiveModKey,
                        DefinitionVersion = AdaptiveDefinitionVersion,
                        SteamBuildId = build.BuildId,
                        GameVersion = result.GameVersion,
                        CreatedUtc = DateTime.UtcNow.ToString("O"),
                        Files = new List<AdaptivePatchReceiptFile>
                        {
                            new AdaptivePatchReceiptFile
                            {
                                RelativePath = MountedWaterGunRelativePath,
                                SourceHash = currentHash,
                                OutputHash = outputHash,
                                BackupPath = activeBase,
                                Newline = document.Newline == "\r\n"
                                    ? "CRLF" : "LF",
                                HasBom = document.HasBom
                            }
                        }
                    });
            }
            else
            {
                AdaptivePatchSupport.DeleteReceipt(AdaptiveModKey);
            }
            AdaptivePatchSupport.QueueBuildActivation(
                result, AdaptiveModKey, enabled);

            SecretModBackupRetention.Prune(
                backupRoot, "DualFluidWaterCannon",
                backupPath, result);
            return result;
        }

        private static void RequireAdaptiveCannonGuards(string text)
        {
            AdaptivePatchSupport.RequireUnique(
                text,
                "function MountedWaterGun.server_onFixedUpdate( self, timeStep )",
                "mounted water cannon fixed-update callback");
            AdaptivePatchSupport.RequireUnique(
                text,
                "function MountedWaterGun.client_onInteract( self, character, state )",
                "mounted water cannon interaction callback");
        }

        private static bool HasCannonPatchEvidence(string text)
        {
            return text.IndexOf(
                "every available liquid on one rising-edge trigger",
                StringComparison.Ordinal) >= 0 ||
                text.IndexOf(
                    "projectile_chemical, firePos, fireVelocity",
                    StringComparison.Ordinal) >= 0;
        }

        private static bool HasAnyCannonTargetEvidence(string text)
        {
            return AdaptivePatchSupport.Count(text, OriginalDeclaration) +
                AdaptivePatchSupport.Count(text, PatchedDeclaration) +
                AdaptivePatchSupport.Count(text, OriginalFireBlock) +
                AdaptivePatchSupport.Count(text, PatchedFireBlock) +
                AdaptivePatchSupport.Count(text, OriginalConnectionCount) +
                AdaptivePatchSupport.Count(text, PatchedConnectionCount) +
                AdaptivePatchSupport.Count(text, OriginalInputs) +
                AdaptivePatchSupport.Count(text, PatchedInputs) > 0;
        }

        private static string Patch(string text)
        {
            text = ReplaceUnique(
                text, OriginalDeclaration, PatchedDeclaration,
                "mounted water cannon connection declaration");
            text = ReplaceUnique(
                text, OriginalFireBlock, PatchedFireBlock,
                "mounted water cannon firing code");
            text = ReplaceUnique(
                text, OriginalConnectionCount, PatchedConnectionCount,
                "mounted water cannon connection-count callback");
            return ReplaceUnique(
                text, OriginalInputs, PatchedInputs,
                "mounted water cannon input discovery");
        }

        private static string Unpatch(string text)
        {
            text = ReplaceUnique(
                text, PatchedDeclaration, OriginalDeclaration,
                "mounted water cannon connection declaration");
            text = ReplaceUnique(
                text, PatchedFireBlock, OriginalFireBlock,
                "mounted water cannon firing code");
            text = ReplaceUnique(
                text, PatchedConnectionCount, OriginalConnectionCount,
                "mounted water cannon connection-count callback");
            return ReplaceUnique(
                text, PatchedInputs, OriginalInputs,
                "mounted water cannon input discovery");
        }

        private static string ReplaceUnique(
            string text, string oldText, string newText, string description)
        {
            int first = text.IndexOf(oldText, StringComparison.Ordinal);
            if (first < 0 ||
                text.IndexOf(oldText, first + oldText.Length, StringComparison.Ordinal) >= 0)
            {
                throw new InvalidDataException(
                    "The expected " + description + " code was not found exactly once.");
            }
            return text.Substring(0, first) + newText +
                text.Substring(first + oldText.Length);
        }

        private static void ReplaceFile(string path, string text)
        {
            string temporary = path + ".raidrescue-dual-fluid-" +
                Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, text, new UTF8Encoding(false));
                File.Replace(temporary, path, null);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }

        private static string ReadUtf8(string path)
        {
            return File.ReadAllText(path, new UTF8Encoding(false, true));
        }

        private static string NormalizeNewlines(string value)
        {
            return value.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        private static string Sha256(string path)
        {
            using (FileStream stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (SHA256 algorithm = SHA256.Create())
                return BytesToHex(algorithm.ComputeHash(stream));
        }

        private static string Sha256(byte[] value)
        {
            using (SHA256 algorithm = SHA256.Create())
                return BytesToHex(algorithm.ComputeHash(value));
        }

        private static string BytesToHex(byte[] value)
        {
            StringBuilder text = new StringBuilder(value.Length * 2);
            foreach (byte item in value)
                text.Append(item.ToString("X2"));
            return text.ToString();
        }

        private static bool HashEquals(string left, string right)
        {
            return String.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal static class DualFluidCannonPatchCoordinator
    {
        public static GamePatchResult SetCannonEnabled(bool enabled)
        {
            if (GamePatchService.IsGameRunning())
                return Failure(
                    "Scrap Mechanic is running. Close the game completely before changing secret mods.");
            string gamePath = GamePatchService.FindGameInstall();
            if (String.IsNullOrEmpty(gamePath))
                return Failure("Scrap Mechanic was not found.");
            GamePatchResult result =
                SetCannonEnabledAt(gamePath, GetBackupRoot(), enabled);
            return GameScriptCacheInvalidator.DeleteAfterChanges(gamePath, result);
        }

        public static GamePatchResult SetChemicalEnabled(bool enabled)
        {
            if (GamePatchService.IsGameRunning())
                return Failure(
                    "Scrap Mechanic is running. Close the game completely before changing secret mods.");
            string gamePath = GamePatchService.FindGameInstall();
            if (String.IsNullOrEmpty(gamePath))
                return Failure("Scrap Mechanic was not found.");
            GamePatchResult result =
                SetChemicalEnabledAt(gamePath, GetBackupRoot(), enabled);
            return GameScriptCacheInvalidator.DeleteAfterChanges(gamePath, result);
        }

        internal static GamePatchResult SetCannonEnabledAt(
            string gamePath, string backupRoot, bool enabled)
        {
            try
            {
                if (!enabled)
                    return DualFluidCannonPatchService.SetEnabledAt(
                        gamePath, backupRoot, false);

                // Generate and validate both linked outputs before the first
                // dependency file is written.
                DualFluidCannonPatchService.PreflightAt(
                    gamePath, true);
                bool chemicalWasInstalled =
                    ChemicalFertilizerPatchService.PreflightAt(
                        gamePath, true);
                GamePatchResult chemicalResult =
                    ChemicalFertilizerPatchService.SetEnabledAt(
                        gamePath, backupRoot, true);
                if (!chemicalResult.Success)
                {
                    return Failure(
                        "Dual-Fluid Water Cannon requires Chemical Fertilizer Splash. " +
                        chemicalResult.Error);
                }

                GamePatchResult cannonResult =
                    DualFluidCannonPatchService.SetEnabledAt(
                        gamePath, backupRoot, true);
                if (!cannonResult.Success && !chemicalWasInstalled)
                {
                    GamePatchResult rollback =
                        ChemicalFertilizerPatchService.SetEnabledAt(
                            gamePath, backupRoot, false);
                    if (!rollback.Success)
                    {
                        return Failure(
                            cannonResult.Error + " Dependency rollback also failed: " +
                            rollback.Error);
                    }
                    return Failure(
                        cannonResult.Error +
                        " Chemical Fertilizer Splash was restored to its previous disabled state.");
                }
                if (!cannonResult.Success)
                    return cannonResult;

                if (chemicalResult.FilesPatched > 0 ||
                    chemicalResult.ActivationChanges != null)
                {
                    cannonResult.FilesPatched += chemicalResult.FilesPatched;
                    AdaptivePatchSupport.MergeBuildActivations(
                        cannonResult, chemicalResult);
                    if (String.IsNullOrEmpty(cannonResult.BackupPath))
                        cannonResult.BackupPath = chemicalResult.BackupPath;
                    cannonResult.Changes.Insert(
                        0,
                        chemicalWasInstalled
                            ? "Reactivated the Chemical Fertilizer Splash dependency for this Steam build."
                            : "Installed the required Chemical Fertilizer Splash dependency.");
                }
                return cannonResult;
            }
            catch (Exception exception)
            {
                return Failure(exception.Message);
            }
        }

        internal static GamePatchResult SetChemicalEnabledAt(
            string gamePath, string backupRoot, bool enabled)
        {
            try
            {
                if (enabled)
                {
                    return ChemicalFertilizerPatchService.SetEnabledAt(
                        gamePath, backupRoot, true);
                }

                bool cannonWasInstalled =
                    DualFluidCannonPatchService.PreflightAt(
                        gamePath, false);
                bool chemicalWasInstalled =
                    ChemicalFertilizerPatchService.PreflightAt(
                        gamePath, false);
                if (!chemicalWasInstalled)
                {
                    throw new InvalidOperationException(
                        "Chemical Fertilizer Splash is already missing. " +
                        "The dependent cannon was not changed.");
                }
                GamePatchResult cannonResult = null;
                if (cannonWasInstalled)
                {
                    cannonResult = DualFluidCannonPatchService.SetEnabledAt(
                        gamePath, backupRoot, false);
                    if (!cannonResult.Success)
                    {
                        return Failure(
                            "Chemical Fertilizer Splash cannot be removed while " +
                            "Dual-Fluid Water Cannon remains installed. " +
                            cannonResult.Error);
                    }
                }

                GamePatchResult chemicalResult =
                    ChemicalFertilizerPatchService.SetEnabledAt(
                        gamePath, backupRoot, false);
                if (!chemicalResult.Success && cannonWasInstalled)
                {
                    GamePatchResult rollback =
                        DualFluidCannonPatchService.SetEnabledAt(
                            gamePath, backupRoot, true);
                    if (!rollback.Success)
                    {
                        return Failure(
                            chemicalResult.Error + " Dependency rollback also failed: " +
                            rollback.Error);
                    }
                    return Failure(
                        chemicalResult.Error +
                        " Dual-Fluid Water Cannon was restored because its dependency remained installed.");
                }
                if (!chemicalResult.Success)
                    return chemicalResult;

                if (cannonResult != null)
                {
                    chemicalResult.FilesPatched += cannonResult.FilesPatched;
                    AdaptivePatchSupport.MergeBuildActivations(
                        chemicalResult, cannonResult);
                    if (String.IsNullOrEmpty(chemicalResult.BackupPath))
                        chemicalResult.BackupPath = cannonResult.BackupPath;
                    chemicalResult.Changes.Insert(
                        0, "Removed Dual-Fluid Water Cannon before removing its fertilizer dependency.");
                }
                return chemicalResult;
            }
            catch (Exception exception)
            {
                return Failure(exception.Message);
            }
        }

        private static string GetBackupRoot()
        {
            return ProductPaths.LocalDataPath(
                "Game Backups", "Scrap Mechanic", "Secret Mods");
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

#if LEGACY_SELF_HELPERS
    internal static class DualFluidCannonPatchLauncher
    {
        private const string HelperSwitch = "--set-dual-fluid-cannon-mod";
        private const string CannonAction = "cannon";
        private const string ChemicalAction = "chemical";

        public static bool TryRunHelper(string[] args)
        {
            if (args == null || args.Length == 0 ||
                !String.Equals(args[0], HelperSwitch, StringComparison.Ordinal))
                return false;
            if (args.Length != 4)
                return true;

            bool enabled;
            if (args[2] == "1")
                enabled = true;
            else if (args[2] == "0")
                enabled = false;
            else
                return true;

            string resultPath = args[3];
            GamePatchResult result;
            bool resultPathIsValid = false;
            try
            {
                ValidateResultPath(resultPath);
                resultPathIsValid = true;
                if (String.Equals(args[1], CannonAction, StringComparison.Ordinal))
                    result = DualFluidCannonPatchCoordinator.SetCannonEnabled(enabled);
                else if (String.Equals(args[1], ChemicalAction, StringComparison.Ordinal))
                    result = DualFluidCannonPatchCoordinator.SetChemicalEnabled(enabled);
                else
                    result = Failure("The secret-mod dependency action is invalid.");
            }
            catch (Exception exception)
            {
                result = Failure(exception.Message);
            }

            try
            {
                if (resultPathIsValid)
                {
                    JavaScriptSerializer serializer =
                        new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
                    File.WriteAllText(
                        resultPath, serializer.Serialize(result),
                        new UTF8Encoding(false));
                }
            }
            catch { }
            return true;
        }

        public static GamePatchResult SetCannonEnabled(bool enabled)
        {
            return Run(CannonAction, enabled);
        }

        public static GamePatchResult SetChemicalEnabled(bool enabled)
        {
            return Run(ChemicalAction, enabled);
        }

        private static GamePatchResult Run(string action, bool enabled)
        {
            if (GamePatchService.IsGameRunning())
                return Failure(
                    "Scrap Mechanic is running. Close the game completely before changing secret mods.");

            if (IsAdministrator())
            {
                return action == CannonAction
                    ? DualFluidCannonPatchCoordinator.SetCannonEnabled(enabled)
                    : DualFluidCannonPatchCoordinator.SetChemicalEnabled(enabled);
            }
            return ElevatedPatchBroker.Execute(
                action == CannonAction
                    ? ElevatedPatchBroker.CannonAction
                    : ElevatedPatchBroker.ChemicalAction,
                enabled, "");
        }

        private static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private static void ValidateResultPath(string path)
        {
            string directory = Path.GetFullPath(GetResultDirectory())
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(path);
            if (!full.StartsWith(directory, StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(
                    Path.GetExtension(full), ".json",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The secret-mod result path is invalid.");
            }
        }

        private static string GetResultDirectory()
        {
            return Path.Combine(
                Path.GetTempPath(), "ScrapLab", "patch-results");
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
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

#endif

    internal static class DeveloperCommandsPatchService
    {
        private const string SurvivalGameOriginal =
            "829C6A0D2AE9C13415CCC15E4FFB021A2A3552C66050E030BDAE86EFCE3D5BF7";
        private const string SurvivalGameHostCommands =
            "540604098ED587EA50EE392C28FC1FCB331ACA189BB46332250BE2DB3E8B9C34";
        private const string SurvivalGameEveryoneCommands =
            "6078F8982DA46CCDA2F809C2F4D0D57611E341DBFDA22CF0C5B587A4E2C389D9";
        private const string SurvivalGameHostCommandsWithNoclipV1 =
            "10D60BAC84061D9AB3E2599B4DDABBBF2E8B402F86017572AC5287BE1F9AD93F";
        private const string SurvivalGameEveryoneCommandsWithNoclipV1 =
            "D430763833114D990CECDDA9CC38A7E86DF07DB6AFEEAF1080900EC9F64AE43D";
        private const string SurvivalGameHostCommandsWithNoclipV2 =
            "83D8712594356BD842DBE48CB3DBB5951CAA90A17ADDC378D085C94B4AC7F69A";
        private const string SurvivalGameEveryoneCommandsWithNoclipV2 =
            "177438542AC623D14148B7CBCF99FD1A9DA9E63330B6164CE5D1329ABCBB813E";
        private const string SurvivalGameHostCommandsWithNoclipV3 =
            "5C3BA2745273F339B5F1B0B34D1AF52605CCBDD8F9627EB130BDB83B35F69B19";
        private const string SurvivalGameEveryoneCommandsWithNoclipV3 =
            "E57CF4FE4B2B50B999BFB8368A327C560844C94CBF6678E6BC9851E27DD3850C";
        private const string SurvivalGameHostCommandsWithNoclip =
            "5CB0572E71AAB8630AD0CF9EBC5B7B15AE17819731A662B7BEC0F8FB48447302";
        private const string SurvivalGameEveryoneCommandsWithNoclip =
            "98811D8C2F1EAA6F00E8CD14D48632F62011B50F7A7FDD112124893371DC1563";
        private const string NoclipRuntimeMarker =
            "SCRAPLAB DEVELOPER COMMANDS NOCLIP v4";
        private const string LegacyNoclipRuntimeMarker =
            "SCRAPLAB DEVELOPER COMMANDS NOCLIP v1";
        private const string LegacyNoclipRuntimeHash =
            "67BD272F5F04CC6BFAEC0043BFBBE7C5A1B6CFAE1FB3819D4862C4C168460407";
        private const string LegacyV2NoclipRuntimeMarker =
            "SCRAPLAB DEVELOPER COMMANDS NOCLIP v2";
        private const string LegacyV2NoclipRuntimeHash =
            "BD8D9D2F5C80DF0690DDA8FEBA93542FD0654264408DB4F5EECB819EFCBA8C6A";
        private const string LegacyV3NoclipRuntimeMarker =
            "SCRAPLAB DEVELOPER COMMANDS NOCLIP v3";
        private const string LegacyV3NoclipRuntimeHash =
            "5555C1DD2E6F585D3F6402D8F406A4A9A3C1FC2E6ECDB18A58DB48F0FA6DBCDC";
        private static readonly string LegacyV2NoclipRuntime = "\n" + String.Join("\n", new[]
        {
            "-- SCRAPLAB DEVELOPER COMMANDS NOCLIP v2",
            "-- Camera-driven collision bypass for privileged Survival chat commands.",
            "local ScrapLabNoclipEyeHeight = 0.7",
            "local ScrapLabNoclipSyncTicks = 4",
            "local ScrapLabNoclipUp = sm.vec3.new( 0, 0, 1 )",
            "",
            "local function scrapLabHasNoclipPlayers( players )",
            "\treturn players ~= nil and next( players ) ~= nil",
            "end",
            "",
            "local function scrapLabSafeDirection( direction, fallback )",
            "\tif direction and direction:length2() > 0.0001 then",
            "\t\treturn direction:normalize()",
            "\tend",
            "\treturn fallback or sm.vec3.new( 0, 1, 0 )",
            "end",
            "",
            "local ScrapLabOriginalServerOnCreate = SurvivalGame.server_onCreate",
            "function SurvivalGame.server_onCreate( self )",
            "\tScrapLabOriginalServerOnCreate( self )",
            "\tself.sv.scrapLabNoclipPlayers = {}",
            "\tself.sv.scrapLabNoclipGodBase = nil",
            "end",
            "",
            "local ScrapLabOriginalClientOnCreate = SurvivalGame.client_onCreate",
            "function SurvivalGame.client_onCreate( self )",
            "\tScrapLabOriginalClientOnCreate( self )",
            "\tself.cl.scrapLabNoclip = nil",
            "end",
            "",
            "local ScrapLabOriginalBindChatCommands = SurvivalGame.bindChatCommands",
            "function SurvivalGame.bindChatCommands( self )",
            "\tScrapLabOriginalBindChatCommands( self )",
            "\tif sm.isHost or g_survivalDev then",
            "\t\tsm.game.bindChatCommand( \"/noclip\", {}, \"cl_onChatCommand\", \"Toggle collision-free flight and temporary god mode\" )",
            "\tend",
            "end",
            "",
            "local ScrapLabOriginalClientChatCommand = SurvivalGame.cl_onChatCommand",
            "function SurvivalGame.cl_onChatCommand( self, params )",
            "\tif params[1] == \"/noclip\" then",
            "\t\tlocal direction = sm.camera.getDirection()",
            "\t\tself.network:sendToServer( \"sv_scrapLabToggleNoclip\", { direction = direction } )",
            "\t\treturn",
            "\tend",
            "\tScrapLabOriginalClientChatCommand( self, params )",
            "end",
            "",
            "function SurvivalGame.sv_scrapLabRestoreGodMode( self )",
            "\tif not scrapLabHasNoclipPlayers( self.sv.scrapLabNoclipPlayers ) then",
            "\t\tif self.sv.scrapLabNoclipGodBase ~= nil then",
            "\t\t\tg_godMode = self.sv.scrapLabNoclipGodBase",
            "\t\tend",
            "\t\tself.sv.scrapLabNoclipGodBase = nil",
            "\tend",
            "end",
            "",
            "function SurvivalGame.sv_scrapLabStopNoclip( self, player, moveToTarget, notifyClient )",
            "\tlocal entries = self.sv.scrapLabNoclipPlayers",
            "\tlocal entry = entries and entries[player.id]",
            "\tif not entry then return end",
            "\tlocal character = player:getCharacter()",
            "\tif character and sm.exists( character ) then",
            "\t\tif moveToTarget and character == entry.character and character:getWorld() == entry.world then",
            "\t\t\tcharacter:setWorldPosition( entry.position - ScrapLabNoclipUp * ScrapLabNoclipEyeHeight )",
            "\t\tend",
            "\tend",
            "\tentries[player.id] = nil",
            "\tself:sv_scrapLabRestoreGodMode()",
            "\tif notifyClient then",
            "\t\tself.network:sendToClient( player, \"cl_scrapLabNoclipState\", { enabled = false } )",
            "\tend",
            "end",
            "",
            "function SurvivalGame.sv_scrapLabToggleNoclip( self, params, player )",
            "\tlocal character = player and player:getCharacter()",
            "\tif not character or not sm.exists( character ) then return end",
            "\tself.sv.scrapLabNoclipPlayers = self.sv.scrapLabNoclipPlayers or {}",
            "\tlocal entry = self.sv.scrapLabNoclipPlayers[player.id]",
            "\tif entry then",
            "\t\tif character ~= entry.character or character:getWorld() ~= entry.world then",
            "\t\t\tself:sv_scrapLabStopNoclip( player, false, true )",
            "\t\t\treturn",
            "\t\tend",
            "\t\tlocal feet = entry.position - ScrapLabNoclipUp * ScrapLabNoclipEyeHeight",
            "\t\tlocal radius = character:getRadius()",
            "\t\tlocal height = character:getHeight()",
            "\t\tlocal castHeight = math.max( height - 2 * radius, 0.1 )",
            "\t\tlocal castCenter = feet + ScrapLabNoclipUp * ( height * 0.5 )",
            "\t\tlocal blocked = sm.physics.capsulecast( castCenter, castCenter, radius, castHeight, character, sm.physics.filter.default, entry.world )",
            "\t\tif blocked then",
            "\t\t\tself.network:sendToClient( player, \"client_showMessage\", \"NOCLIP: Move clear of solid objects before disabling\" )",
            "\t\t\treturn",
            "\t\tend",
            "\t\tself:sv_scrapLabStopNoclip( player, true, true )",
            "\t\tself.network:sendToClient( player, \"client_showMessage\", \"NOCLIP: Off\" )",
            "\t\treturn",
            "\tend",
            "",
            "\tif character:isSeated() or character:isTumbling() or character:isDowned() then",
            "\t\tself.network:sendToClient( player, \"client_showMessage\", \"NOCLIP: Stand normally before enabling flight\" )",
            "\t\treturn",
            "\tend",
            "\tif not character:isOnGround() then",
            "\t\tself.network:sendToClient( player, \"client_showMessage\", \"NOCLIP: Stand on solid ground before enabling flight\" )",
            "\t\treturn",
            "\tend",
            "",
            "\tif not scrapLabHasNoclipPlayers( self.sv.scrapLabNoclipPlayers ) then",
            "\t\tself.sv.scrapLabNoclipGodBase = g_godMode == true",
            "\tend",
            "\tg_godMode = true",
            "\tlocal direction = scrapLabSafeDirection( params and params.direction, character:getDirection() )",
            "\tlocal anchor = character:getWorldPosition()",
            "\tlocal position = anchor + ScrapLabNoclipUp * ScrapLabNoclipEyeHeight",
            "\tself.sv.scrapLabNoclipPlayers[player.id] = {",
            "\t\tplayer = player, character = character, world = character:getWorld(),",
            "\t\tposition = position, anchor = anchor, direction = direction, move = sm.vec3.zero(), syncTicks = 0",
            "\t}",
            "\tself.network:sendToClient( player, \"cl_scrapLabNoclipState\", { enabled = true, position = position, direction = direction } )",
            "\tself.network:sendToClient( player, \"client_showMessage\", \"NOCLIP: On - use movement keys and look where you want to fly\" )",
            "end",
            "",
            "function SurvivalGame.sv_scrapLabNoclipInput( self, params, player )",
            "\tlocal entry = self.sv.scrapLabNoclipPlayers and self.sv.scrapLabNoclipPlayers[player.id]",
            "\tif entry then",
            "\t\tentry.direction = scrapLabSafeDirection( params and params.direction, entry.direction )",
            "\t\tlocal move = params and params.move or sm.vec3.zero()",
            "\t\tmove = sm.vec3.new( move.x, move.y, 0 )",
            "\t\tif move:length2() > 400 then move = move:normalize() * 20 end",
            "\t\tentry.move = move",
            "\tend",
            "end",
            "",
            "function SurvivalGame.sv_scrapLabUpdateNoclip( self, timeStep )",
            "\tlocal entries = self.sv.scrapLabNoclipPlayers",
            "\tif not scrapLabHasNoclipPlayers( entries ) then return end",
            "\tg_godMode = true",
            "\tlocal stopped = {}",
            "\tfor playerId, entry in pairs( entries ) do",
            "\t\tlocal player = entry.player",
            "\t\tlocal character = player and player:getCharacter()",
            "\t\tif not character or not sm.exists( character ) or character ~= entry.character or character:getWorld() ~= entry.world then",
            "\t\t\tstopped[#stopped + 1] = player",
            "\t\telse",
            "\t\t\tlocal direction = scrapLabSafeDirection( entry.direction, character:getDirection() )",
            "\t\t\tlocal flatForward = sm.vec3.new( direction.x, direction.y, 0 )",
            "\t\t\tflatForward = scrapLabSafeDirection( flatForward, sm.vec3.new( 0, 1, 0 ) )",
            "\t\t\tlocal right = sm.vec3.new( flatForward.y, -flatForward.x, 0 )",
            "\t\t\tlocal move = entry.move or sm.vec3.zero()",
            "\t\t\tlocal travelVelocity = direction * move:dot( flatForward ) + right * move:dot( right )",
            "\t\t\tentry.position = entry.position + travelVelocity * timeStep",
            "\t\t\tcharacter:setWorldPosition( entry.anchor )",
            "\t\t\tentry.syncTicks = entry.syncTicks + 1",
            "\t\t\tif entry.syncTicks >= ScrapLabNoclipSyncTicks then",
            "\t\t\t\tentry.syncTicks = 0",
            "\t\t\t\tself.network:sendToClient( player, \"cl_scrapLabNoclipSync\", entry.position )",
            "\t\t\tend",
            "\t\tend",
            "\tend",
            "\tfor _, player in ipairs( stopped ) do",
            "\t\tif player then self:sv_scrapLabStopNoclip( player, false, true ) end",
            "\tend",
            "end",
            "",
            "local ScrapLabOriginalServerFixedUpdate = SurvivalGame.server_onFixedUpdate",
            "function SurvivalGame.server_onFixedUpdate( self, timeStep )",
            "\tScrapLabOriginalServerFixedUpdate( self, timeStep )",
            "\tself:sv_scrapLabUpdateNoclip( timeStep )",
            "end",
            "",
            "function SurvivalGame.cl_scrapLabNoclipState( self, data )",
            "\tlocal localPlayer = sm.localPlayer.getPlayer()",
            "\tlocal character = localPlayer and localPlayer:getCharacter()",
            "\tif data.enabled then",
            "\t\tif character and sm.exists( character ) then character:setVisible( false ) end",
            "\t\tself.cl.scrapLabNoclip = { position = data.position, correction = data.position, direction = scrapLabSafeDirection( data.direction, sm.camera.getDirection() ), aimTimer = 0 }",
            "\telse",
            "\t\tif character and sm.exists( character ) then character:setVisible( true ) end",
            "\t\tself.cl.scrapLabNoclip = nil",
            "\t\tsm.camera.setCameraState( sm.camera.state.default )",
            "\t\tsm.localPlayer.setLockedControls( false )",
            "\tend",
            "end",
            "",
            "function SurvivalGame.cl_scrapLabNoclipSync( self, position )",
            "\tif self.cl.scrapLabNoclip then self.cl.scrapLabNoclip.correction = position end",
            "end",
            "",
            "function SurvivalGame.cl_scrapLabUpdateNoclip( self, dt )",
            "\tlocal state = self.cl.scrapLabNoclip",
            "\tif not state then return end",
            "\tlocal player = sm.localPlayer.getPlayer()",
            "\tlocal character = player and player:getCharacter()",
            "\tif not character then return end",
            "",
            "\tstate.direction = scrapLabSafeDirection( sm.localPlayer.getDirection(), state.direction )",
            "\tlocal velocity = character:getVelocity()",
            "\tlocal flatVelocity = sm.vec3.new( velocity.x, velocity.y, 0 )",
            "\tif flatVelocity:length2() > 400 then flatVelocity = flatVelocity:normalize() * 20 end",
            "\tlocal flatForward = sm.vec3.new( state.direction.x, state.direction.y, 0 )",
            "\tflatForward = scrapLabSafeDirection( flatForward, sm.vec3.new( 0, 1, 0 ) )",
            "\tlocal right = sm.vec3.new( flatForward.y, -flatForward.x, 0 )",
            "\tlocal predictedVelocity = state.direction * flatVelocity:dot( flatForward ) + right * flatVelocity:dot( right )",
            "\tstate.position = state.position + predictedVelocity * dt",
            "\tif state.correction then",
            "\t\tlocal error = state.correction - state.position",
            "\t\tif error:length2() > 9 then",
            "\t\t\tstate.position = state.correction",
            "\t\telse",
            "\t\t\tstate.position = state.position + error * math.min( dt * 8, 1 )",
            "\t\tend",
            "\tend",
            "",
            "\tstate.aimTimer = state.aimTimer + dt",
            "\tif state.aimTimer >= 0.05 then",
            "\t\tstate.aimTimer = 0",
            "\t\tself.network:sendToServer( \"sv_scrapLabNoclipInput\", { direction = state.direction, move = flatVelocity } )",
            "\tend",
            "\tsm.camera.setCameraState( sm.camera.state.cutsceneFP )",
            "\tsm.camera.setPosition( state.position )",
            "\tsm.camera.setDirection( state.direction )",
            "\tsm.camera.setFov( sm.camera.getDefaultFov() )",
            "end",
            "",
            "local ScrapLabOriginalClientUpdate = SurvivalGame.client_onUpdate",
            "function SurvivalGame.client_onUpdate( self, dt )",
            "\tScrapLabOriginalClientUpdate( self, dt )",
            "\tself:cl_scrapLabUpdateNoclip( dt )",
            "end",
            "",
            "local ScrapLabOriginalServerPlayerLeft = SurvivalGame.server_onPlayerLeft",
            "function SurvivalGame.server_onPlayerLeft( self, player )",
            "\tif self.sv.scrapLabNoclipPlayers and self.sv.scrapLabNoclipPlayers[player.id] then",
            "\t\tself:sv_scrapLabStopNoclip( player, false, false )",
            "\tend",
            "\tScrapLabOriginalServerPlayerLeft( self, player )",
            "end",
            "",
            "local ScrapLabOriginalServerOnDestroy = SurvivalGame.server_onDestroy",
            "function SurvivalGame.server_onDestroy( self )",
            "\tif scrapLabHasNoclipPlayers( self.sv.scrapLabNoclipPlayers ) and self.sv.scrapLabNoclipGodBase ~= nil then",
            "\t\tg_godMode = self.sv.scrapLabNoclipGodBase",
            "\tend",
            "\tScrapLabOriginalServerOnDestroy( self )",
            "end",
            "-- END SCRAPLAB DEVELOPER COMMANDS NOCLIP v2",
            ""
        });
        private static readonly string LegacyV3NoclipRuntime = "\n" + String.Join("\n", new[]
        {
            "-- SCRAPLAB DEVELOPER COMMANDS NOCLIP v3",
            "-- Engine-input flight using the always-present Survival Lift tool; the normal camera remains untouched.",
            "local ScrapLabNoclipUp = sm.vec3.new( 0, 0, 1 )",
            "",
            "local function scrapLabHasNoclipPlayers( players )",
            "\treturn players ~= nil and next( players ) ~= nil",
            "end",
            "",
            "local function scrapLabSafeDirection( direction, fallback )",
            "\tif direction and direction:length2() > 0.0001 then return direction:normalize() end",
            "\treturn fallback or sm.vec3.new( 0, 1, 0 )",
            "end",
            "",
            "local function scrapLabInstallLiftInputBridge()",
            "\tif SurvivalLift == nil or SurvivalLift.client_onUpdate == nil then return false end",
            "\tif g_scrapLabNoclipLiftClass == SurvivalLift and SurvivalLift.client_onUpdate == g_scrapLabNoclipLiftWrapper then return true end",
            "\tlocal originalUpdate = SurvivalLift.client_onUpdate",
            "\tlocal wrapper = function( self, dt )",
            "\t\toriginalUpdate( self, dt )",
            "\t\tif self.tool and self.tool:isLocal() then",
            "\t\t\tg_scrapLabNoclipToolInput = {",
            "\t\t\t\tmove = self.tool:getRelativeMoveDirection(),",
            "\t\t\t\tdirection = self.tool:getDirection(),",
            "\t\t\t\tspeed = self.tool:getMovementSpeedFraction()",
            "\t\t\t}",
            "\t\tend",
            "\tend",
            "\tg_scrapLabNoclipLiftClass = SurvivalLift",
            "\tg_scrapLabNoclipLiftWrapper = wrapper",
            "\tSurvivalLift.client_onUpdate = wrapper",
            "\treturn true",
            "end",
            "",
            "local ScrapLabOriginalServerOnCreate = SurvivalGame.server_onCreate",
            "function SurvivalGame.server_onCreate( self )",
            "\tScrapLabOriginalServerOnCreate( self )",
            "\tself.sv.scrapLabNoclipPlayers = {}",
            "\tself.sv.scrapLabNoclipGodBase = nil",
            "end",
            "",
            "local ScrapLabOriginalClientOnCreate = SurvivalGame.client_onCreate",
            "function SurvivalGame.client_onCreate( self )",
            "\tScrapLabOriginalClientOnCreate( self )",
            "\tself.cl.scrapLabNoclip = nil",
            "\tscrapLabInstallLiftInputBridge()",
            "end",
            "",
            "local ScrapLabOriginalBindChatCommands = SurvivalGame.bindChatCommands",
            "function SurvivalGame.bindChatCommands( self )",
            "\tScrapLabOriginalBindChatCommands( self )",
            "\tif sm.isHost or g_survivalDev then",
            "\t\tsm.game.bindChatCommand( \"/noclip\", {}, \"cl_onChatCommand\", \"Toggle collision-free flight and temporary god mode\" )",
            "\tend",
            "end",
            "",
            "local ScrapLabOriginalClientChatCommand = SurvivalGame.cl_onChatCommand",
            "function SurvivalGame.cl_onChatCommand( self, params )",
            "\tif params[1] == \"/noclip\" then",
            "\t\tself.network:sendToServer( \"sv_scrapLabToggleNoclip\" )",
            "\t\treturn",
            "\tend",
            "\tScrapLabOriginalClientChatCommand( self, params )",
            "end",
            "",
            "function SurvivalGame.sv_scrapLabRestoreGodMode( self )",
            "\tif not scrapLabHasNoclipPlayers( self.sv.scrapLabNoclipPlayers ) then",
            "\t\tif self.sv.scrapLabNoclipGodBase ~= nil then g_godMode = self.sv.scrapLabNoclipGodBase end",
            "\t\tself.sv.scrapLabNoclipGodBase = nil",
            "\tend",
            "end",
            "",
            "function SurvivalGame.sv_scrapLabStopNoclip( self, player, placeCharacter, notifyClient )",
            "\tlocal entries = self.sv.scrapLabNoclipPlayers",
            "\tlocal entry = entries and entries[player.id]",
            "\tif not entry then return end",
            "\tlocal character = player:getCharacter()",
            "\tif placeCharacter and character and sm.exists( character ) and character == entry.character and character:getWorld() == entry.world then",
            "\t\tcharacter:setWorldPosition( entry.position )",
            "\tend",
            "\tentries[player.id] = nil",
            "\tself:sv_scrapLabRestoreGodMode()",
            "\tif notifyClient then self.network:sendToClient( player, \"cl_scrapLabNoclipState\", false ) end",
            "end",
            "",
            "function SurvivalGame.sv_scrapLabToggleNoclip( self, _, player )",
            "\tlocal character = player and player:getCharacter()",
            "\tif not character or not sm.exists( character ) then return end",
            "\tself.sv.scrapLabNoclipPlayers = self.sv.scrapLabNoclipPlayers or {}",
            "\tlocal entry = self.sv.scrapLabNoclipPlayers[player.id]",
            "\tif entry then",
            "\t\tif character ~= entry.character or character:getWorld() ~= entry.world then",
            "\t\t\tself:sv_scrapLabStopNoclip( player, false, true )",
            "\t\t\treturn",
            "\t\tend",
            "\t\tlocal radius = character:getRadius()",
            "\t\tlocal height = character:getHeight()",
            "\t\tlocal castHeight = math.max( height - 2 * radius, 0.1 )",
            "\t\tlocal castCenter = entry.position + ScrapLabNoclipUp * ( height * 0.5 )",
            "\t\tlocal blocked = sm.physics.capsulecast( castCenter, castCenter, radius, castHeight, character, sm.physics.filter.default, entry.world )",
            "\t\tif blocked then",
            "\t\t\tself.network:sendToClient( player, \"client_showMessage\", \"NOCLIP: Move clear of solid objects before disabling\" )",
            "\t\t\treturn",
            "\t\tend",
            "\t\tself:sv_scrapLabStopNoclip( player, true, true )",
            "\t\tself.network:sendToClient( player, \"client_showMessage\", \"NOCLIP: Off\" )",
            "\t\treturn",
            "\tend",
            "\tif character:isSeated() or character:isTumbling() or character:isDowned() then",
            "\t\tself.network:sendToClient( player, \"client_showMessage\", \"NOCLIP: Stand normally before enabling flight\" )",
            "\t\treturn",
            "\tend",
            "\tif not scrapLabHasNoclipPlayers( self.sv.scrapLabNoclipPlayers ) then self.sv.scrapLabNoclipGodBase = g_godMode == true end",
            "\tg_godMode = true",
            "\tself.sv.scrapLabNoclipPlayers[player.id] = {",
            "\t\tplayer = player, character = character, world = character:getWorld(),",
            "\t\tposition = character:getWorldPosition(), velocity = sm.vec3.zero()",
            "\t}",
            "\tself.network:sendToClient( player, \"cl_scrapLabNoclipState\", true )",
            "\tself.network:sendToClient( player, \"client_showMessage\", \"NOCLIP: On - WASD flies, mouse aims, sprint increases speed\" )",
            "end",
            "",
            "function SurvivalGame.sv_scrapLabNoclipInput( self, velocity, player )",
            "\tlocal entry = self.sv.scrapLabNoclipPlayers and self.sv.scrapLabNoclipPlayers[player.id]",
            "\tif entry and velocity then",
            "\t\tif velocity:length2() > 400 then velocity = velocity:normalize() * 20 end",
            "\t\tentry.velocity = velocity",
            "\tend",
            "end",
            "",
            "function SurvivalGame.sv_scrapLabUpdateNoclip( self, timeStep )",
            "\tlocal entries = self.sv.scrapLabNoclipPlayers",
            "\tif not scrapLabHasNoclipPlayers( entries ) then return end",
            "\tg_godMode = true",
            "\tlocal stopped = {}",
            "\tfor _, entry in pairs( entries ) do",
            "\t\tlocal player = entry.player",
            "\t\tlocal character = player and player:getCharacter()",
            "\t\tif not character or not sm.exists( character ) or character ~= entry.character or character:getWorld() ~= entry.world then",
            "\t\t\tstopped[#stopped + 1] = player",
            "\t\telse",
            "\t\t\tentry.position = entry.position + entry.velocity * timeStep",
            "\t\t\tcharacter:setWorldPosition( entry.position )",
            "\t\tend",
            "\tend",
            "\tfor _, player in ipairs( stopped ) do if player then self:sv_scrapLabStopNoclip( player, false, true ) end end",
            "end",
            "",
            "local ScrapLabOriginalServerFixedUpdate = SurvivalGame.server_onFixedUpdate",
            "function SurvivalGame.server_onFixedUpdate( self, timeStep )",
            "\tScrapLabOriginalServerFixedUpdate( self, timeStep )",
            "\tself:sv_scrapLabUpdateNoclip( timeStep )",
            "end",
            "",
            "function SurvivalGame.cl_scrapLabNoclipState( self, enabled )",
            "\tself.cl.scrapLabNoclip = enabled and { sendTimer = 0 } or nil",
            "end",
            "",
            "function SurvivalGame.cl_scrapLabUpdateNoclip( self, dt )",
            "\tscrapLabInstallLiftInputBridge()",
            "\tlocal state = self.cl.scrapLabNoclip",
            "\tif not state then return end",
            "\tlocal input = g_scrapLabNoclipToolInput",
            "\tif not input or not input.move then return end",
            "\tlocal move = input.move",
            "\tif move:length2() > 1 then move = move:normalize() end",
            "\tlocal direction = scrapLabSafeDirection( input.direction, sm.localPlayer.getDirection() )",
            "\tlocal flatForward = scrapLabSafeDirection( sm.vec3.new( direction.x, direction.y, 0 ), sm.vec3.new( 0, 1, 0 ) )",
            "\tlocal right = sm.vec3.new( flatForward.y, -flatForward.x, 0 )",
            "\tlocal desired = direction * move.y + right * move.x",
            "\tif desired:length2() > 1 then desired = desired:normalize() end",
            "\tlocal speedFraction = math.max( math.min( input.speed or 0.5, 1.0 ), 0.25 )",
            "\tlocal velocity = desired * ( 20 * speedFraction )",
            "\tstate.sendTimer = state.sendTimer + dt",
            "\tif state.sendTimer >= 0.025 then",
            "\t\tstate.sendTimer = 0",
            "\t\tself.network:sendToServer( \"sv_scrapLabNoclipInput\", velocity )",
            "\tend",
            "end",
            "",
            "local ScrapLabOriginalClientUpdate = SurvivalGame.client_onUpdate",
            "function SurvivalGame.client_onUpdate( self, dt )",
            "\tScrapLabOriginalClientUpdate( self, dt )",
            "\tself:cl_scrapLabUpdateNoclip( dt )",
            "end",
            "",
            "local ScrapLabOriginalServerPlayerLeft = SurvivalGame.server_onPlayerLeft",
            "function SurvivalGame.server_onPlayerLeft( self, player )",
            "\tif self.sv.scrapLabNoclipPlayers and self.sv.scrapLabNoclipPlayers[player.id] then self:sv_scrapLabStopNoclip( player, false, false ) end",
            "\tScrapLabOriginalServerPlayerLeft( self, player )",
            "end",
            "",
            "local ScrapLabOriginalServerOnDestroy = SurvivalGame.server_onDestroy",
            "function SurvivalGame.server_onDestroy( self )",
            "\tif scrapLabHasNoclipPlayers( self.sv.scrapLabNoclipPlayers ) and self.sv.scrapLabNoclipGodBase ~= nil then g_godMode = self.sv.scrapLabNoclipGodBase end",
            "\tScrapLabOriginalServerOnDestroy( self )",
            "end",
            "-- END SCRAPLAB DEVELOPER COMMANDS NOCLIP v3",
            ""
        });
        private static readonly string NoclipRuntime = "\n" + String.Join("\n", new[]
        {
            "-- SCRAPLAB DEVELOPER COMMANDS NOCLIP v4",
            "dofile( \"$SURVIVAL_DATA/Scripts/ScrapLab/Noclip.lua\" )",
            "-- END SCRAPLAB DEVELOPER COMMANDS NOCLIP v4",
            ""
        });
        internal const string HostOnlyMode = "host";
        internal const string EveryoneMode = "everyone";
        private static readonly string SurvivalGameRelativePath = Path.Combine(
            "Survival", "Scripts", "game", "SurvivalGame.lua");
        private const string OriginalGate = "\tlocal addCheats = g_survivalDev";
        private const string HostOnlyGate =
            "\t-- RAID RESCUE SECRET MOD: host-only access to existing Survival developer chat commands.\n" +
            "\tlocal addCheats = sm.isHost";
        private const string EveryoneGate =
            "\t-- RAID RESCUE SECRET MOD: every joined player receives the existing Survival developer chat commands.\n" +
            "\tlocal addCheats = true";
        private const string OriginalClientData =
            "self.network:setClientData( { dev = g_survivalDev, gotoLocations = self.sv.gotoLocations }, 1 )";
        private const string EveryoneClientData =
            "self.network:setClientData( { dev = true, gotoLocations = self.sv.gotoLocations }, 1 ) " +
            "-- RAID RESCUE: share command access with joined players.";

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
                    throw new InvalidOperationException("Scrap Mechanic was not found.");

                result.GamePath = gamePath;
                string executable = Path.Combine(gamePath, "Release", "ScrapMechanic.exe");
                result.GameVersion = FileVersionInfo.GetVersionInfo(executable).FileVersion;
                string path = Path.Combine(gamePath, SurvivalGameRelativePath);
                if (!File.Exists(path))
                    throw new FileNotFoundException("SurvivalGame.lua was not found.", path);

                string hash = Sha256(path);
                if (HashEquals(hash, SurvivalGameHostCommandsWithNoclip) ||
                    HashEquals(hash, SurvivalGameEveryoneCommandsWithNoclip))
                {
                    string assetReason;
                    if (!NoclipAssetSupport.IsInstalled(gamePath, out assetReason))
                    {
                        string applyReason;
                        bool canApply = NoclipAssetSupport.CanApply(
                            gamePath, out applyReason);
                        result.Success = true;
                        result.Installed = false;
                        result.Mode = HashEquals(
                            hash, SurvivalGameEveryoneCommandsWithNoclip)
                            ? EveryoneMode : HostOnlyMode;
                        AdaptivePatchSupport.FillResult(
                            result,
                            AdaptivePatchSupport.GetSteamBuild(
                                gamePath, result.GameVersion),
                            canApply
                                ? PatchCompatibilityState.CompatibleUpdate
                                : PatchCompatibilityState.PartialConflict,
                            true, canApply,
                            canApply ? assetReason : applyReason);
                        return result;
                    }
                }
                if (HashEquals(hash, SurvivalGameHostCommandsWithNoclip))
                {
                    SteamBuildInfo build =
                        AdaptivePatchSupport.GetSteamBuild(
                            gamePath, result.GameVersion);
                    if (AdaptivePatchSupport.RequiresBuildRefresh(
                        "DeveloperCommands", build))
                    {
                        AdaptivePatchSupport.MarkRefreshRequired(
                            result, build, HostOnlyMode);
                        return result;
                    }
                    result.Success = true;
                    result.Installed = true;
                    result.AlreadyPatched = true;
                    result.Mode = HostOnlyMode;
                    AdaptivePatchSupport.FillResult(
                        result, build,
                        PatchCompatibilityState.KnownInstalled,
                        false, true, "Verified ScrapLab host-only file.");
                    return result;
                }
                if (HashEquals(hash, SurvivalGameEveryoneCommandsWithNoclip))
                {
                    SteamBuildInfo build =
                        AdaptivePatchSupport.GetSteamBuild(
                            gamePath, result.GameVersion);
                    if (AdaptivePatchSupport.RequiresBuildRefresh(
                        "DeveloperCommands", build))
                    {
                        AdaptivePatchSupport.MarkRefreshRequired(
                            result, build, EveryoneMode);
                        return result;
                    }
                    result.Success = true;
                    result.Installed = true;
                    result.AlreadyPatched = true;
                    result.Mode = EveryoneMode;
                    AdaptivePatchSupport.FillResult(
                        result, build,
                        PatchCompatibilityState.KnownInstalled,
                        false, true, "Verified ScrapLab every-player file.");
                    return result;
                }
                if (HashEquals(hash, SurvivalGameOriginal))
                {
                    AdaptivePatchSupport.DiscardReceiptIfSuperseded(
                        "DeveloperCommands", gamePath);
                    result.Success = true;
                    result.Installed = false;
                    result.Mode = "";
                    AdaptivePatchSupport.FillResult(
                        result,
                        AdaptivePatchSupport.GetSteamBuild(
                            gamePath, result.GameVersion),
                        PatchCompatibilityState.KnownClean,
                        false, true, "Verified official file.");
                    return result;
                }
                if (HashEquals(hash, SurvivalGameHostCommands) ||
                    HashEquals(hash, SurvivalGameEveryoneCommands) ||
                    HashEquals(hash, SurvivalGameHostCommandsWithNoclipV1) ||
                    HashEquals(hash, SurvivalGameEveryoneCommandsWithNoclipV1) ||
                    HashEquals(hash, SurvivalGameHostCommandsWithNoclipV2) ||
                    HashEquals(hash, SurvivalGameEveryoneCommandsWithNoclipV2) ||
                    HashEquals(hash, SurvivalGameHostCommandsWithNoclipV3) ||
                    HashEquals(hash, SurvivalGameEveryoneCommandsWithNoclipV3))
                {
                    SteamBuildInfo build = AdaptivePatchSupport.GetSteamBuild(
                        gamePath, result.GameVersion);
                    result.Success = true;
                    result.Installed = false;
                    result.Mode = HashEquals(hash, SurvivalGameEveryoneCommands) ||
                        HashEquals(hash, SurvivalGameEveryoneCommandsWithNoclipV1) ||
                        HashEquals(hash, SurvivalGameEveryoneCommandsWithNoclipV2) ||
                        HashEquals(hash, SurvivalGameEveryoneCommandsWithNoclipV3)
                        ? EveryoneMode : HostOnlyMode;
                    AdaptivePatchSupport.FillResult(
                        result, build,
                        PatchCompatibilityState.CompatibleUpdate,
                        true, true,
                        "Developer Commands are installed, but the improved /fly controls are not. Enable the mod to upgrade them.");
                    return result;
                }
                return GetAdaptiveDeveloperStatus(
                    result, gamePath, path);
            }
            catch (Exception exception)
            {
                result.Success = false;
                result.Error = exception.Message;
                return result;
            }
        }

        public static GamePatchResult SetEnabled(bool enabled, string mode)
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
                enabled,
                mode);
            return GameScriptCacheInvalidator.DeleteAfterChanges(gamePath, result);
        }

        internal static GamePatchResult SetEnabledAt(
            string gamePath, string backupRoot, bool enabled, string mode)
        {
            NoclipAssetSupport.NoclipAssetTransaction assets = null;
            try
            {
                assets = NoclipAssetSupport.Prepare(
                    gamePath, backupRoot, enabled);
                assets.Apply();
                GamePatchResult result = SetEnabledCoreAt(
                    gamePath, backupRoot, enabled, mode);
                if (!result.Success)
                {
                    assets.Rollback();
                    return result;
                }
                if (assets.FilesChanged > 0)
                {
                    result.FilesPatched += assets.FilesChanged;
                    result.Changes.Add(enabled
                        ? "Installed ScrapLab's isolated noclip module and hidden input tool."
                        : "Removed ScrapLab's isolated noclip scripts and input-tool registration.");
                }
                SecretModBackupRetention.Prune(
                    backupRoot, "DeveloperCommandsAssets",
                    assets.BackupPath, result);
                return result;
            }
            catch (Exception exception)
            {
                if (assets != null)
                {
                    try { assets.Rollback(); }
                    catch (Exception rollback)
                    {
                        return Failure(exception.Message +
                            " Asset rollback also failed: " + rollback.Message);
                    }
                }
                return Failure(exception.Message);
            }
        }

        private static GamePatchResult SetEnabledCoreAt(
            string gamePath, string backupRoot, bool enabled, string mode)
        {
            string selectedMode = enabled ? NormalizeMode(mode) : "";
            GamePatchResult result = new GamePatchResult
            {
                GamePath = gamePath,
                Installed = enabled,
                Mode = selectedMode,
                Changes = new List<string>()
            };
            try
            {
                string executable = Path.Combine(gamePath, "Release", "ScrapMechanic.exe");
                if (!File.Exists(executable))
                    throw new FileNotFoundException("ScrapMechanic.exe was not found.", executable);

                result.GameVersion = FileVersionInfo.GetVersionInfo(executable).FileVersion;
                string path = Path.Combine(gamePath, SurvivalGameRelativePath);
                if (!File.Exists(path))
                    throw new FileNotFoundException("SurvivalGame.lua was not found.", path);

                string currentHash = Sha256(path);
                string desiredHash = !enabled
                    ? SurvivalGameOriginal
                    : (selectedMode == EveryoneMode
                        ? SurvivalGameEveryoneCommandsWithNoclip
                        : SurvivalGameHostCommandsWithNoclip);
                if (HashEquals(currentHash, desiredHash))
                {
                    SteamBuildInfo build =
                        AdaptivePatchSupport.GetSteamBuild(
                            gamePath, result.GameVersion);
                    if (enabled &&
                        AdaptivePatchSupport.RequiresBuildRefresh(
                            "DeveloperCommands", build))
                    {
                        AdaptivePatchSupport.PrepareBuildRefresh(
                            result, "DeveloperCommands", build,
                            "Developer Commands were reactivated after the Steam update.");
                        result.Mode = selectedMode;
                        return result;
                    }
                    result.Success = true;
                    result.AlreadyPatched = true;
                    if (!enabled)
                        AdaptivePatchSupport.DeleteBuildActivation(
                            "DeveloperCommands");
                    result.Changes.Add(
                        enabled
                            ? "Developer commands already use the selected access mode."
                            : "Host developer commands are already locked.");
                    return result;
                }

                bool knownFile =
                    HashEquals(currentHash, SurvivalGameOriginal) ||
                    HashEquals(currentHash, SurvivalGameHostCommands) ||
                    HashEquals(currentHash, SurvivalGameEveryoneCommands) ||
                    HashEquals(currentHash, SurvivalGameHostCommandsWithNoclipV1) ||
                    HashEquals(currentHash, SurvivalGameEveryoneCommandsWithNoclipV1) ||
                    HashEquals(currentHash, SurvivalGameHostCommandsWithNoclipV2) ||
                    HashEquals(currentHash, SurvivalGameEveryoneCommandsWithNoclipV2) ||
                    HashEquals(currentHash, SurvivalGameHostCommandsWithNoclipV3) ||
                    HashEquals(currentHash, SurvivalGameEveryoneCommandsWithNoclipV3) ||
                    HashEquals(currentHash, SurvivalGameHostCommandsWithNoclip) ||
                    HashEquals(currentHash, SurvivalGameEveryoneCommandsWithNoclip);
                if (!knownFile)
                {
                    return SetAdaptiveDeveloperEnabledAt(
                        gamePath, backupRoot, enabled, selectedMode,
                        result, path, currentHash);
                }

                string source = NormalizeNewlines(ReadUtf8(path));
                string originalSource;
                if (HashEquals(currentHash, SurvivalGameOriginal))
                {
                    originalSource = source;
                }
                else if (HashEquals(currentHash, SurvivalGameHostCommands) ||
                         HashEquals(currentHash, SurvivalGameHostCommandsWithNoclipV1) ||
                         HashEquals(currentHash, SurvivalGameHostCommandsWithNoclipV2) ||
                         HashEquals(currentHash, SurvivalGameHostCommandsWithNoclipV3) ||
                         HashEquals(currentHash, SurvivalGameHostCommandsWithNoclip))
                {
                    source = RemoveNoclipRuntime(source);
                    originalSource = ReplaceUnique(
                        source, HostOnlyGate, OriginalGate,
                        "Survival developer-command gate");
                }
                else if (HashEquals(currentHash, SurvivalGameEveryoneCommands) ||
                         HashEquals(currentHash, SurvivalGameEveryoneCommandsWithNoclipV1) ||
                         HashEquals(currentHash, SurvivalGameEveryoneCommandsWithNoclipV2) ||
                         HashEquals(currentHash, SurvivalGameEveryoneCommandsWithNoclipV3) ||
                         HashEquals(currentHash, SurvivalGameEveryoneCommandsWithNoclip))
                {
                    source = RemoveNoclipRuntime(source);
                    originalSource = ReplaceUnique(
                        source, EveryoneGate, OriginalGate,
                        "Survival developer-command gate");
                    originalSource = ReplaceExactCount(
                        originalSource, EveryoneClientData, OriginalClientData, 3,
                        "developer-command client permission broadcasts");
                }
                else
                {
                    throw new InvalidOperationException(
                        "SurvivalGame.lua does not match the verified original or ScrapLab " +
                        "developer-command versions. No files were changed. Use Steam Verify " +
                        "before changing this mod.");
                }

                string transformed = originalSource;
                if (enabled && selectedMode == HostOnlyMode)
                {
                    transformed = ReplaceUnique(
                        originalSource, OriginalGate, HostOnlyGate,
                        "Survival developer-command gate");
                    transformed = InstallNoclipRuntime(transformed);
                }
                else if (enabled && selectedMode == EveryoneMode)
                {
                    transformed = ReplaceUnique(
                        originalSource, OriginalGate, EveryoneGate,
                        "Survival developer-command gate");
                    transformed = ReplaceExactCount(
                        transformed, OriginalClientData, EveryoneClientData, 3,
                        "developer-command client permission broadcasts");
                    transformed = InstallNoclipRuntime(transformed);
                }

                string generatedHash = Sha256(Encoding.UTF8.GetBytes(transformed));
                if (!HashEquals(generatedHash, desiredHash))
                {
                    throw new InvalidOperationException(
                        "The generated developer-command patch did not match its verified checksum.");
                }

                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
                string backupPath = Path.Combine(
                    backupRoot,
                    (enabled ? "Configure-" : "Remove-") +
                    "DeveloperCommands-" + stamp);
                Directory.CreateDirectory(backupPath);
                result.BackupPath = backupPath;

                string backupFile = Path.Combine(backupPath, "SurvivalGame.lua");
                File.Copy(path, backupFile, false);
                if (!HashEquals(Sha256(backupFile), currentHash))
                    throw new IOException("The SurvivalGame backup failed checksum verification.");

                StringBuilder manifest = new StringBuilder();
                manifest.AppendLine("ScrapLab secret-mod backup");
                manifest.AppendLine("Mod: Developer Commands");
                manifest.AppendLine("Action: " + (enabled ? "Install" : "Remove"));
                manifest.AppendLine("Access mode: " +
                    (enabled
                        ? (selectedMode == EveryoneMode ? "Every Player" : "Host Only")
                        : "Disabled"));
                manifest.AppendLine("Game path: " + gamePath);
                manifest.AppendLine("Game version: " + result.GameVersion);
                manifest.AppendLine("Created: " + DateTime.Now.ToString("O"));
                manifest.AppendLine("SurvivalGame.lua SHA-256 " + currentHash);
                File.WriteAllText(
                    Path.Combine(backupPath, "MANIFEST.txt"),
                    manifest.ToString(), new UTF8Encoding(false));

                try
                {
                    ReplaceFile(path, transformed);
                    if (!HashEquals(Sha256(path), desiredHash))
                        throw new IOException(
                            "SurvivalGame.lua failed its final checksum verification.");
                }
                catch
                {
                    File.Copy(backupFile, path, true);
                    if (!HashEquals(Sha256(path), currentHash))
                    {
                        throw new IOException(
                            "The developer-command update failed and automatic rollback could " +
                            "not restore SurvivalGame.lua. The verified backup remains in " +
                            backupPath);
                    }
                    throw;
                }

                result.Success = true;
                result.FilesPatched = 1;
                AdaptivePatchSupport.FillResult(
                    result,
                    AdaptivePatchSupport.GetSteamBuild(
                        gamePath, result.GameVersion),
                    enabled
                        ? PatchCompatibilityState.KnownInstalled
                        : PatchCompatibilityState.KnownClean,
                    false, true, "Verified current-build transformation.");
                result.Changes.Add(
                    enabled
                        ? (selectedMode == EveryoneMode
                            ? "Unlocked Scrap Mechanic's existing Survival developer chat commands for every joined player."
                            : "Unlocked Scrap Mechanic's existing Survival developer chat commands for the host only.")
                        : "Removed Developer Commands and restored the verified original SurvivalGame script.");
                result.Changes.Add(
                    enabled
                        ? (selectedMode == EveryoneMode
                            ? "Joined players receive command registration; kick and ban remain host-only."
                            : "Developer mode itself remains off; normal spawn points, intro flow, and recipes are unchanged.")
                        : "Normal Survival command registration is restored.");
                AdaptivePatchSupport.QueueBuildActivation(
                    result, "DeveloperCommands", enabled);
                SecretModBackupRetention.Prune(
                    backupRoot, "DeveloperCommands",
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

        private static GamePatchResult GetAdaptiveDeveloperStatus(
            GamePatchResult result, string gamePath, string path)
        {
            LuaTextDocument document = AdaptivePatchSupport.ReadLua(path);
            SteamBuildInfo build = AdaptivePatchSupport.GetSteamBuild(
                gamePath, result.GameVersion);
            if (document.MixedNewlines)
            {
                result.Success = true;
                result.Installed = false;
                result.Mode = "";
                AdaptivePatchSupport.FillResult(
                    result, build,
                    PatchCompatibilityState.OtherModification,
                    false, false,
                    "SurvivalGame.lua uses mixed newline styles.");
                return result;
            }
            try
            {
                RequireAdaptiveDeveloperGuards(document.NormalizedText);
            }
            catch (InvalidDataException exception)
            {
                result.Success = true;
                result.Installed = false;
                result.Mode = "";
                AdaptivePatchSupport.FillResult(
                    result, build,
                    PatchCompatibilityState.UnsupportedCode,
                    false, false,
                    "SurvivalGame.lua changed a required command callback. " +
                    exception.Message);
                return result;
            }
            int originalGate = AdaptivePatchSupport.Count(
                document.NormalizedText, OriginalGate);
            int hostGate = AdaptivePatchSupport.Count(
                document.NormalizedText, HostOnlyGate);
            int everyoneGate = AdaptivePatchSupport.Count(
                document.NormalizedText, EveryoneGate);
            int originalBroadcasts = AdaptivePatchSupport.Count(
                document.NormalizedText, OriginalClientData);
            int everyoneBroadcasts = AdaptivePatchSupport.Count(
                document.NormalizedText, EveryoneClientData);
            int noclipRuntime = AdaptivePatchSupport.Count(
                document.NormalizedText, NoclipRuntime);
            bool noclipMarker = document.NormalizedText.IndexOf(
                NoclipRuntimeMarker, StringComparison.Ordinal) >= 0;
            bool legacyNoclipRuntime = GetVerifiedLegacyNoclipRuntime(
                document.NormalizedText) != null;

            if (noclipRuntime > 1 || (noclipMarker && noclipRuntime != 1))
            {
                result.Success = true;
                result.Installed = false;
                result.Mode = "";
                AdaptivePatchSupport.FillResult(
                    result, build,
                    PatchCompatibilityState.PartialConflict,
                    false, false,
                    "The /noclip runtime is partial, duplicated, or edited.");
                return result;
            }

            if (noclipRuntime == 1)
            {
                string assetReason;
                if (!NoclipAssetSupport.IsInstalled(gamePath, out assetReason))
                {
                    string applyReason;
                    bool canApply = NoclipAssetSupport.CanApply(
                        gamePath, out applyReason);
                    result.Success = true;
                    result.Installed = false;
                    result.Mode = everyoneGate == 1
                        ? EveryoneMode : HostOnlyMode;
                    AdaptivePatchSupport.FillResult(
                        result, build,
                        canApply
                            ? PatchCompatibilityState.CompatibleUpdate
                            : PatchCompatibilityState.PartialConflict,
                        true, canApply,
                        canApply ? assetReason : applyReason);
                    return result;
                }
            }

            if (hostGate == 1 && originalGate == 0 &&
                everyoneGate == 0 && originalBroadcasts == 3 &&
                everyoneBroadcasts == 0 && noclipRuntime == 1)
            {
                if (AdaptivePatchSupport.RequiresBuildRefresh(
                    "DeveloperCommands", build))
                {
                    AdaptivePatchSupport.MarkRefreshRequired(
                        result, build, HostOnlyMode);
                    return result;
                }
                result.Success = true;
                result.Installed = true;
                result.AlreadyPatched = true;
                result.Mode = HostOnlyMode;
                AdaptivePatchSupport.FillResult(
                    result, build,
                    PatchCompatibilityState.AdaptiveInstalled,
                    true, true,
                    "Host-only command access is structurally intact.");
                return result;
            }
            if (everyoneGate == 1 && originalGate == 0 &&
                hostGate == 0 && originalBroadcasts == 0 &&
                everyoneBroadcasts == 3 && noclipRuntime == 1)
            {
                if (AdaptivePatchSupport.RequiresBuildRefresh(
                    "DeveloperCommands", build))
                {
                    AdaptivePatchSupport.MarkRefreshRequired(
                        result, build, EveryoneMode);
                    return result;
                }
                result.Success = true;
                result.Installed = true;
                result.AlreadyPatched = true;
                result.Mode = EveryoneMode;
                AdaptivePatchSupport.FillResult(
                    result, build,
                    PatchCompatibilityState.AdaptiveInstalled,
                    true, true,
                    "Every-player command access is structurally intact.");
                return result;
            }
            if (originalGate == 1 && hostGate == 0 &&
                everyoneGate == 0 && originalBroadcasts == 3 &&
                everyoneBroadcasts == 0 && noclipRuntime == 0 &&
                !legacyNoclipRuntime)
            {
                AdaptivePatchSupport.DiscardReceiptIfSuperseded(
                    "DeveloperCommands", gamePath);
                string reason = "";
                bool canApply = AdaptivePatchSupport.CanAdaptCleanFiles(
                    build, new[] { path }, out reason);
                result.Success = true;
                result.Installed = false;
                result.Mode = "";
                AdaptivePatchSupport.FillResult(
                    result, build,
                    canApply
                        ? PatchCompatibilityState.CompatibleUpdate
                        : PatchCompatibilityState.OtherModification,
                    canApply, canApply, reason);
                return result;
            }

            bool legacyHost = hostGate == 1 && originalGate == 0 &&
                everyoneGate == 0 && originalBroadcasts == 3 &&
                everyoneBroadcasts == 0 && noclipRuntime == 0;
            bool legacyEveryone = everyoneGate == 1 && originalGate == 0 &&
                hostGate == 0 && originalBroadcasts == 0 &&
                everyoneBroadcasts == 3 && noclipRuntime == 0;
            if (legacyHost || legacyEveryone)
            {
                result.Success = true;
                result.Installed = false;
                result.Mode = legacyEveryone ? EveryoneMode : HostOnlyMode;
                AdaptivePatchSupport.FillResult(
                    result, build,
                    PatchCompatibilityState.CompatibleUpdate,
                    true, true,
                    legacyNoclipRuntime
                        ? "Developer Commands use a legacy flight runtime. Enable the mod to install the rewritten /fly controls."
                        : "Developer Commands are installed, but /fly is missing. Enable the mod to upgrade it.");
                return result;
            }

            bool partial =
                document.NormalizedText.IndexOf(
                    "RAID RESCUE SECRET MOD",
                    StringComparison.Ordinal) >= 0 ||
                originalGate + hostGate + everyoneGate > 0 ||
                originalBroadcasts + everyoneBroadcasts > 0 ||
                noclipMarker ||
                document.NormalizedText.IndexOf(
                    LegacyNoclipRuntimeMarker,
                    StringComparison.Ordinal) >= 0 ||
                document.NormalizedText.IndexOf(
                    LegacyV2NoclipRuntimeMarker,
                    StringComparison.Ordinal) >= 0 ||
                document.NormalizedText.IndexOf(
                    LegacyV3NoclipRuntimeMarker,
                    StringComparison.Ordinal) >= 0;
            result.Success = true;
            result.Installed = false;
            result.Mode = "";
            AdaptivePatchSupport.FillResult(
                result, build,
                partial
                    ? PatchCompatibilityState.PartialConflict
                    : PatchCompatibilityState.UnsupportedCode,
                false, false,
                partial
                    ? "SurvivalGame.lua contains partial or conflicting command-access code."
                    : "The game update changed the protected command registration code.");
            return result;
        }

        private static GamePatchResult SetAdaptiveDeveloperEnabledAt(
            string gamePath, string backupRoot, bool enabled,
            string selectedMode, GamePatchResult result,
            string path, string currentHash)
        {
            LuaTextDocument document = AdaptivePatchSupport.ReadLua(path);
            AdaptivePatchSupport.RequireAdaptiveFormat(
                document, "SurvivalGame.lua");
            RequireAdaptiveDeveloperGuards(document.NormalizedText);
            SteamBuildInfo build = AdaptivePatchSupport.GetSteamBuild(
                gamePath, result.GameVersion);

            int originalGate = AdaptivePatchSupport.Count(
                document.NormalizedText, OriginalGate);
            int hostGate = AdaptivePatchSupport.Count(
                document.NormalizedText, HostOnlyGate);
            int everyoneGate = AdaptivePatchSupport.Count(
                document.NormalizedText, EveryoneGate);
            int originalBroadcasts = AdaptivePatchSupport.Count(
                document.NormalizedText, OriginalClientData);
            int everyoneBroadcasts = AdaptivePatchSupport.Count(
                document.NormalizedText, EveryoneClientData);
            int noclipRuntime = AdaptivePatchSupport.Count(
                document.NormalizedText, NoclipRuntime);
            bool noclipMarker = document.NormalizedText.IndexOf(
                NoclipRuntimeMarker, StringComparison.Ordinal) >= 0;
            if (noclipRuntime > 1 || (noclipMarker && noclipRuntime != 1))
                throw new InvalidOperationException(
                    "Developer Commands cannot be changed because the /noclip runtime is partial, duplicated, or edited.");
            bool hasNoclipRuntime = noclipRuntime == 1;

            bool clean = originalGate == 1 && hostGate == 0 &&
                everyoneGate == 0 && originalBroadcasts == 3 &&
                everyoneBroadcasts == 0 && !hasNoclipRuntime;
            bool host = hostGate == 1 && originalGate == 0 &&
                everyoneGate == 0 && originalBroadcasts == 3 &&
                everyoneBroadcasts == 0;
            bool everyone = everyoneGate == 1 && originalGate == 0 &&
                hostGate == 0 && originalBroadcasts == 0 &&
                everyoneBroadcasts == 3;

            bool selectedModeAlreadyPresent =
                enabled && hasNoclipRuntime &&
                ((host && selectedMode == HostOnlyMode) ||
                 (everyone && selectedMode == EveryoneMode));
            if (selectedModeAlreadyPresent &&
                AdaptivePatchSupport.RequiresBuildRefresh(
                    "DeveloperCommands", build))
            {
                AdaptivePatchSupport.PrepareBuildRefresh(
                    result, "DeveloperCommands", build,
                    "Developer Commands were reactivated after the Steam update.");
                result.Mode = selectedMode;
                return result;
            }

            string cleanText;
            if (clean)
            {
                string reason = "";
                if (enabled && !AdaptivePatchSupport.CanAdaptCleanFiles(
                    build, new[] { path }, out reason))
                    throw new InvalidOperationException(
                        "Developer Commands cannot be applied: " + reason);
                cleanText = document.NormalizedText;
            }
            else if (host)
            {
                string withoutRuntime = RemoveNoclipRuntime(
                    document.NormalizedText);
                cleanText = ReplaceUnique(
                    withoutRuntime,
                    HostOnlyGate, OriginalGate,
                    "Survival developer-command gate");
            }
            else if (everyone)
            {
                string withoutRuntime = RemoveNoclipRuntime(
                    document.NormalizedText);
                cleanText = ReplaceUnique(
                    withoutRuntime,
                    EveryoneGate, OriginalGate,
                    "Survival developer-command gate");
                cleanText = ReplaceExactCount(
                    cleanText, EveryoneClientData,
                    OriginalClientData, 3,
                    "developer-command client permission broadcasts");
            }
            else
            {
                throw new InvalidOperationException(
                    "Developer Commands cannot be changed because its protected " +
                    "gate or permission broadcasts are missing, duplicated, or edited.");
            }

            if (!enabled && clean)
            {
                result.Success = true;
                result.Installed = false;
                result.Mode = "";
                result.AlreadyPatched = true;
                AdaptivePatchSupport.FillResult(
                    result, build,
                    PatchCompatibilityState.CompatibleUpdate,
                    true, true, "Developer Commands are already removed.");
                AdaptivePatchSupport.DeleteReceipt("DeveloperCommands");
                AdaptivePatchSupport.DeleteBuildActivation(
                    "DeveloperCommands");
                return result;
            }

            string transformed = cleanText;
            if (enabled && selectedMode == HostOnlyMode)
            {
                transformed = ReplaceUnique(
                    cleanText, OriginalGate, HostOnlyGate,
                    "Survival developer-command gate");
                transformed = InstallNoclipRuntime(transformed);
            }
            else if (enabled && selectedMode == EveryoneMode)
            {
                transformed = ReplaceUnique(
                    cleanText, OriginalGate, EveryoneGate,
                    "Survival developer-command gate");
                transformed = ReplaceExactCount(
                    transformed, OriginalClientData,
                    EveryoneClientData, 3,
                    "developer-command client permission broadcasts");
                transformed = InstallNoclipRuntime(transformed);
            }

            byte[] outputBytes = document.Render(transformed);
            string outputHash = AdaptivePatchSupport.Sha256(outputBytes);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            string backupPath = Path.Combine(
                backupRoot,
                (enabled ? "Configure-" : "Remove-") +
                "DeveloperCommands-" + stamp);
            Directory.CreateDirectory(backupPath);
            result.BackupPath = backupPath;
            string backupFile = Path.Combine(
                backupPath, "SurvivalGame.lua");
            File.Copy(path, backupFile, false);
            if (!HashEquals(
                AdaptivePatchSupport.Sha256(backupFile), currentHash))
                throw new IOException(
                    "The adaptive SurvivalGame backup failed checksum verification.");
            AdaptivePatchSupport.WriteBackupManifest(
                backupPath, "Developer Commands",
                enabled ? "Configure " + selectedMode : "Remove",
                gamePath, build, "6",
                new[]
                {
                    new AdaptivePatchReceiptFile
                    {
                        RelativePath = SurvivalGameRelativePath,
                        SourceHash = currentHash,
                        OutputHash = outputHash,
                        Newline = document.Newline == "\r\n"
                            ? "CRLF" : "LF",
                        HasBom = document.HasBom
                    }
                });

            AdaptivePatchReceipt existing =
                AdaptivePatchSupport.LoadReceipt("DeveloperCommands");
            AdaptivePatchReceiptFile existingFile =
                AdaptivePatchSupport.FindReceiptFile(
                    existing, SurvivalGameRelativePath);

            try
            {
                if (!enabled && existingFile != null &&
                    HashEquals(currentHash, existingFile.OutputHash) &&
                    File.Exists(existingFile.BackupPath) &&
                    HashEquals(
                        AdaptivePatchSupport.Sha256(existingFile.BackupPath),
                        existingFile.SourceHash))
                {
                    AdaptivePatchSupport.ReplaceFile(
                        path, File.ReadAllBytes(existingFile.BackupPath),
                        "commands-exact-restore");
                    outputHash = existingFile.SourceHash;
                }
                else
                {
                    AdaptivePatchSupport.ReplaceFile(
                        path, outputBytes, "commands-adaptive");
                }
                if (!HashEquals(
                    AdaptivePatchSupport.Sha256(path), outputHash))
                    throw new IOException(
                        "SurvivalGame.lua failed adaptive output verification.");
            }
            catch
            {
                File.Copy(backupFile, path, true);
                if (!HashEquals(
                    AdaptivePatchSupport.Sha256(path), currentHash))
                    throw new IOException(
                        "Adaptive Developer Commands rollback could not restore SurvivalGame.lua.");
                throw;
            }

            result.Success = true;
            result.Installed = enabled;
            result.Mode = enabled ? selectedMode : "";
            result.FilesPatched = 1;
            AdaptivePatchSupport.FillResult(
                result, build,
                enabled
                    ? PatchCompatibilityState.AdaptiveInstalled
                    : PatchCompatibilityState.CompatibleUpdate,
                true, true,
                enabled
                    ? "Installed with exact protected-code matching on this Steam build."
                    : "Removed while preserving unrelated updated code.");
            result.Changes.Add(
                enabled
                    ? "Configured Developer Commands on a structurally compatible game update."
                    : "Removed Developer Commands without replacing unrelated updated code.");

            if (enabled)
            {
                AdaptivePatchReceiptFile baseFile = existingFile;
                if (baseFile == null)
                {
                    string activeBase =
                        AdaptivePatchSupport.CaptureBaseBackup(
                            "DeveloperCommands",
                            SurvivalGameRelativePath,
                            backupFile, currentHash);
                    baseFile = new AdaptivePatchReceiptFile
                    {
                        RelativePath = SurvivalGameRelativePath,
                        SourceHash = currentHash,
                        BackupPath = activeBase,
                        Newline = document.Newline == "\r\n"
                            ? "CRLF" : "LF",
                        HasBom = document.HasBom
                    };
                }
                baseFile.OutputHash = outputHash;
                AdaptivePatchSupport.SaveReceipt(
                    "DeveloperCommands",
                    new AdaptivePatchReceipt
                    {
                        ModKey = "DeveloperCommands",
                        DefinitionVersion = "6",
                        SteamBuildId = build.BuildId,
                        GameVersion = result.GameVersion,
                        CreatedUtc = existing == null
                            ? DateTime.UtcNow.ToString("O")
                            : existing.CreatedUtc,
                        Files = new List<AdaptivePatchReceiptFile>
                        {
                            baseFile
                        }
                    });
            }
            else
            {
                AdaptivePatchSupport.DeleteReceipt("DeveloperCommands");
            }
            AdaptivePatchSupport.QueueBuildActivation(
                result, "DeveloperCommands", enabled);

            SecretModBackupRetention.Prune(
                backupRoot, "DeveloperCommands", backupPath, result);
            return result;
        }

        private static void RequireAdaptiveDeveloperGuards(string text)
        {
            string guardedText = text;
            if (AdaptivePatchSupport.Count(text, NoclipRuntime) == 1)
            {
                guardedText = text.Replace(NoclipRuntime, "");
            }
            else
            {
                string legacyRuntime = GetVerifiedLegacyNoclipRuntime(text);
                if (legacyRuntime != null)
                    guardedText = text.Replace(legacyRuntime, "");
            }
            AdaptivePatchSupport.RequireUnique(
                guardedText,
                "function SurvivalGame.bindChatCommands( self )",
                "Survival command registration callback");
            AdaptivePatchSupport.RequireUnique(
                guardedText,
                "function SurvivalGame.sv_updateClientData( self )",
                "Survival client-data callback");
            AdaptivePatchSupport.RequireUnique(
                guardedText,
                "function SurvivalGame.server_onCreate( self )",
                "Survival server creation callback");
            AdaptivePatchSupport.RequireUnique(
                guardedText,
                "function SurvivalGame.client_onCreate( self )",
                "Survival client creation callback");
            AdaptivePatchSupport.RequireUnique(
                guardedText,
                "function SurvivalGame.server_onFixedUpdate( self, timeStep )",
                "Survival server update callback");
            AdaptivePatchSupport.RequireUnique(
                guardedText,
                "function SurvivalGame.client_onUpdate( self, dt )",
                "Survival client update callback");
            AdaptivePatchSupport.RequireUnique(
                guardedText,
                "function SurvivalGame.server_onPlayerLeft( self, player )",
                "Survival player-leave callback");
            AdaptivePatchSupport.RequireUnique(
                guardedText,
                "function SurvivalGame.server_onDestroy( self )",
                "Survival server destruction callback");
            AdaptivePatchSupport.RequireUnique(
                guardedText,
                "function SurvivalGame.cl_onChatCommand( self, params )",
                "Survival chat-command callback");
        }

        private static string InstallNoclipRuntime(string text)
        {
            int runtimeCount = AdaptivePatchSupport.Count(text, NoclipRuntime);
            bool hasMarker = text.IndexOf(
                NoclipRuntimeMarker, StringComparison.Ordinal) >= 0;
            if (runtimeCount == 1)
                return text;
            if (runtimeCount != 0 || hasMarker ||
                GetVerifiedLegacyNoclipRuntime(text) != null)
                throw new InvalidDataException(
                    "The /noclip runtime is partial, duplicated, or edited.");
            return text + NoclipRuntime;
        }

        private static string RemoveNoclipRuntime(string text)
        {
            int runtimeCount = AdaptivePatchSupport.Count(text, NoclipRuntime);
            bool hasMarker = text.IndexOf(
                NoclipRuntimeMarker, StringComparison.Ordinal) >= 0;
            if (runtimeCount == 0)
            {
                if (hasMarker)
                    throw new InvalidDataException(
                        "The /noclip runtime is partial or edited.");
                string legacyRuntime = GetVerifiedLegacyNoclipRuntime(text);
                if (legacyRuntime != null)
                    return text.Replace(legacyRuntime, "");
                return text;
            }
            if (runtimeCount != 1)
                throw new InvalidDataException(
                    "The /noclip runtime is duplicated.");
            return text.Replace(NoclipRuntime, "");
        }

        private static string GetVerifiedLegacyNoclipRuntime(string text)
        {
            string v1 = GetVerifiedLegacyNoclipRuntime(
                text, LegacyNoclipRuntimeMarker, LegacyNoclipRuntimeHash);
            string v2 = GetVerifiedLegacyNoclipRuntime(
                text, LegacyV2NoclipRuntimeMarker, LegacyV2NoclipRuntimeHash);
            string v3 = GetVerifiedLegacyNoclipRuntime(
                text, LegacyV3NoclipRuntimeMarker, LegacyV3NoclipRuntimeHash);
            int found = (v1 != null ? 1 : 0) + (v2 != null ? 1 : 0) +
                (v3 != null ? 1 : 0);
            if (found > 1)
                throw new InvalidDataException(
                    "Multiple legacy /noclip runtimes were found.");
            return v1 ?? v2 ?? v3;
        }

        private static string GetVerifiedLegacyNoclipRuntime(
            string text, string marker, string expectedHash)
        {
            string startToken = "\n-- " + marker + "\n";
            string endToken = "-- END " + marker + "\n";
            if (text.IndexOf(marker, StringComparison.Ordinal) < 0)
                return null;

            int start = text.IndexOf(
                startToken, StringComparison.Ordinal);
            int end = start < 0
                ? -1
                : text.IndexOf(
                    endToken, start + startToken.Length,
                    StringComparison.Ordinal);
            if (start < 0 || end < 0 ||
                text.IndexOf(
                    startToken, start + startToken.Length,
                    StringComparison.Ordinal) >= 0 ||
                text.IndexOf(
                    endToken, end + endToken.Length,
                    StringComparison.Ordinal) >= 0)
            {
                throw new InvalidDataException(
                    "The original /noclip runtime is partial or duplicated.");
            }

            string runtime = text.Substring(
                start, end + endToken.Length - start);
            string runtimeHash = AdaptivePatchSupport.Sha256(
                Encoding.UTF8.GetBytes(runtime));
            if (!HashEquals(runtimeHash, expectedHash))
            {
                throw new InvalidDataException(
                    "The original /noclip runtime was edited.");
            }
            return runtime;
        }

        private static string NormalizeMode(string mode)
        {
            if (String.Equals(mode, EveryoneMode, StringComparison.OrdinalIgnoreCase))
                return EveryoneMode;
            if (String.Equals(mode, HostOnlyMode, StringComparison.OrdinalIgnoreCase) ||
                String.IsNullOrWhiteSpace(mode))
                return HostOnlyMode;
            throw new InvalidOperationException(
                "The selected developer-command access mode is invalid.");
        }

        private static string ReplaceExactCount(
            string text, string oldText, string newText, int expectedCount,
            string description)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(oldText, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += oldText.Length;
            }
            if (count != expectedCount)
            {
                throw new InvalidDataException(
                    "Expected " + expectedCount + " " + description +
                    " but found " + count + ".");
            }
            return text.Replace(oldText, newText);
        }

        private static string ReplaceUnique(
            string text, string oldText, string newText, string description)
        {
            int first = text.IndexOf(oldText, StringComparison.Ordinal);
            if (first < 0 ||
                text.IndexOf(oldText, first + oldText.Length, StringComparison.Ordinal) >= 0)
            {
                throw new InvalidDataException(
                    "The expected " + description + " code was not found exactly once.");
            }
            return text.Substring(0, first) + newText +
                text.Substring(first + oldText.Length);
        }

        private static void ReplaceFile(string path, string text)
        {
            string temporary = path + ".raidrescue-secret-" +
                Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, text, new UTF8Encoding(false));
                File.Replace(temporary, path, null);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }

        private static string ReadUtf8(string path)
        {
            return File.ReadAllText(path, new UTF8Encoding(false, true));
        }

        private static string NormalizeNewlines(string value)
        {
            return value.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        private static string Sha256(string path)
        {
            using (FileStream stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (SHA256 algorithm = SHA256.Create())
                return BytesToHex(algorithm.ComputeHash(stream));
        }

        private static string Sha256(byte[] value)
        {
            using (SHA256 algorithm = SHA256.Create())
                return BytesToHex(algorithm.ComputeHash(value));
        }

        private static string BytesToHex(byte[] value)
        {
            StringBuilder text = new StringBuilder(value.Length * 2);
            foreach (byte item in value)
                text.Append(item.ToString("X2"));
            return text.ToString();
        }

        private static bool HashEquals(string left, string right)
        {
            return String.Equals(left, right, StringComparison.OrdinalIgnoreCase);
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

#if LEGACY_SELF_HELPERS
    internal static class DeveloperCommandsPatchLauncher
    {
        private const string HelperSwitch = "--set-developer-commands-mod";

        public static bool TryRunHelper(string[] args)
        {
            if (args == null || args.Length == 0 ||
                !String.Equals(args[0], HelperSwitch, StringComparison.Ordinal))
                return false;
            if (args.Length != 4)
                return true;

            bool enabled;
            if (args[1] == "1")
                enabled = true;
            else if (args[1] == "0")
                enabled = false;
            else
                return true;

            string mode = args[2];
            string resultPath = args[3];
            GamePatchResult result;
            bool resultPathIsValid = false;
            try
            {
                ValidateResultPath(resultPath);
                resultPathIsValid = true;
                result = DeveloperCommandsPatchService.SetEnabled(enabled, mode);
            }
            catch (Exception exception)
            {
                result = Failure(exception.Message);
            }

            try
            {
                if (resultPathIsValid)
                {
                    JavaScriptSerializer serializer =
                        new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
                    File.WriteAllText(
                        resultPath, serializer.Serialize(result),
                        new UTF8Encoding(false));
                }
            }
            catch { }
            return true;
        }

        public static GamePatchResult SetEnabled(bool enabled, string mode)
        {
            if (GamePatchService.IsGameRunning())
                return Failure(
                    "Scrap Mechanic is running. Close the game completely before changing secret mods.");

            if (IsAdministrator())
                return DeveloperCommandsPatchService.SetEnabled(enabled, mode);
            return ElevatedPatchBroker.Execute(
                ElevatedPatchBroker.CommandsAction, enabled, mode);
        }

        private static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private static void ValidateResultPath(string path)
        {
            string directory = Path.GetFullPath(GetResultDirectory())
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(path);
            if (!full.StartsWith(directory, StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(
                    Path.GetExtension(full), ".json",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The secret-mod result path is invalid.");
            }
        }

        private static string GetResultDirectory()
        {
            return Path.Combine(
                Path.GetTempPath(), "ScrapLab", "patch-results");
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
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

    internal static class GamePatchLauncher
    {
        private const string HelperSwitch = "--install-raid-hotfix";

        public static bool TryRunHelper(string[] args)
        {
            if (args == null || args.Length == 0 ||
                !String.Equals(args[0], HelperSwitch, StringComparison.Ordinal))
                return false;
            if (args.Length != 2)
                return true;

            string resultPath = args[1];
            GamePatchResult result;
            bool resultPathIsValid = false;
            try
            {
                ValidateResultPath(resultPath);
                resultPathIsValid = true;
                result = GamePatchService.Install();
            }
            catch (Exception exception)
            {
                result = new GamePatchResult
                {
                    Success = false,
                    Error = exception.Message,
                    Changes = new List<string>()
                };
            }

            try
            {
                if (resultPathIsValid)
                {
                    JavaScriptSerializer serializer =
                        new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
                    File.WriteAllText(
                        resultPath, serializer.Serialize(result),
                        new UTF8Encoding(false));
                }
            }
            catch { }
            return true;
        }

        public static GamePatchResult Install()
        {
            if (GamePatchService.IsGameRunning())
                return Failure(
                    "Scrap Mechanic is running. Close the game completely before installing the hotfix.");

            if (IsAdministrator())
                return GamePatchService.Install();
            return ElevatedPatchBroker.Execute(
                ElevatedPatchBroker.HotfixAction, true, "");
        }

        private static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private static void ValidateResultPath(string path)
        {
            string directory = Path.GetFullPath(GetResultDirectory())
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(path);
            if (!full.StartsWith(directory, StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(
                    Path.GetExtension(full), ".json",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The hotfix result path is invalid.");
            }
        }

        private static string GetResultDirectory()
        {
            return Path.Combine(
                Path.GetTempPath(), "ScrapLab", "patch-results");
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
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
#endif
}
