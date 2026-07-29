using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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

            string backupRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Raid Rescue", "Game Backups", "Scrap Mechanic");
            return InstallAt(gamePath, backupRoot);
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
                            "Raid Rescue state. No files were changed. Use Steam's Verify " +
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
                        "The latest cumulative Raid Rescue 1.0.2 hotfix is already installed.");
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
            manifest.AppendLine("Raid Rescue game-script backup");
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

        private static string FindGameInstall()
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

    internal static class GamePatchLauncher
    {
        private const string HelperSwitch = "--install-raid-hotfix";

        public static bool TryRunHelper(string[] args)
        {
            if (args == null || args.Length != 2 ||
                !String.Equals(args[0], HelperSwitch, StringComparison.Ordinal))
                return false;

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

            string resultDirectory = GetResultDirectory();
            Directory.CreateDirectory(resultDirectory);
            string resultPath = Path.Combine(
                resultDirectory, Guid.NewGuid().ToString("N") + ".json");

            try
            {
                ProcessStartInfo start = new ProcessStartInfo
                {
                    FileName = Assembly.GetExecutingAssembly().Location,
                    Arguments = Quote(HelperSwitch) + " " + Quote(resultPath),
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (Process process = Process.Start(start))
                    process.WaitForExit();

                if (!File.Exists(resultPath))
                    return Failure(
                        "The elevated hotfix installer did not return a result.");

                JavaScriptSerializer serializer =
                    new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
                return serializer.Deserialize<GamePatchResult>(
                    File.ReadAllText(resultPath, Encoding.UTF8));
            }
            catch (Win32Exception exception)
            {
                if (exception.NativeErrorCode == 1223)
                {
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
                return Failure(exception.Message);
            }
            finally
            {
                try
                {
                    if (File.Exists(resultPath))
                        File.Delete(resultPath);
                }
                catch { }
            }
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
                Path.GetTempPath(), "RaidRescue", "patch-results");
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
}
