using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace RaidRescue
{
    internal sealed class RaidCropReference
    {
        public int WorldId;
        public long HarvestableId;
    }

    internal sealed class RaidCropStorageState
    {
        public RaidCropReference Crop;
        public StoredScriptRecord Script;
        public bool FlagPresent;
        public bool HasSurvivedRaid;
    }

    internal static class RaidService
    {
        private const string NormalLootUuid =
            "97fe0cf2-0591-4e98-9beb-9186f4fd83c8";
        private const string BiggerLootUuid =
            "282f332e-eb95-4553-b711-4a027e92391d";
        private const string LimitedLootUuid =
            "d1d56712-a3f0-4af8-bb53-7ad6cb37d34b";
        private const long ServerTicksPerSecond = 40;

        private static readonly Dictionary<string, string> EnemyNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "9f4fde94-312f-4417-b13b-84029c5d6b52", "Farmbot" },
                { "c8bfb8f3-7efc-49ac-875a-eb85ac0614db", "Haybot" },
                { "c68914f8-d769-4638-9071-f7dbd1d97351", "Green Tapebot" },
                { "97efd943-d176-479a-a6f4-46373327ddcd", "Yellow Tapebot" },
                { "58992f50-ca36-44e1-8c47-4996d89d6a9a", "Blue Totebot" },
                { "8984bdbf-521e-4eed-b3c4-2b5e287eb879", "Green Totebot" },
                { "9360d346-3ff2-4925-a068-660cf5dd5267", "Red Totebot" }
            };

        private static readonly Dictionary<string, string> CropNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "d3fdedca-7e1c-45cc-a1db-f0deee381a71", "Banana" },
                { "bb600268-cd29-4715-babe-5fd02645eb1c", "Blueberry" },
                { "b1a17952-b6a2-436d-81e4-df8ffb552166", "Orange" },
                { "1337f492-aa23-42d0-af7a-fae45b47e55f", "Pineapple" },
                { "6dd177f4-3312-4b1e-a986-4421b5e83bff", "Carrot" },
                { "18efedc5-8706-4ecb-afd4-e9294d3f1052", "Redbeet" },
                { "c6f80a93-5b16-45ef-a478-ca56a50f61ae", "Tomato" },
                { "1675314b-0dfc-4d34-b854-0bdf0476221d", "Broccoli" },
                { "ec1cf82f-e8f3-4ca6-8e35-a4bdf0e8e259", "Potato" },
                { "779b5e09-7ce7-4a16-9817-02f5cb8e11f6", "Cotton" },
                { "81f76937-af88-4882-a64c-bc86a4092c20", "Chili" },
                { "1ebfd7c2-89df-4455-a0cc-2a43a718125b", "Pigment flower" }
            };

        private static readonly HashSet<string> RaidCropUuids =
            new HashSet<string>(
                new[]
                {
                    "d3fdedca-7e1c-45cc-a1db-f0deee381a71",
                    "bb600268-cd29-4715-babe-5fd02645eb1c",
                    "b1a17952-b6a2-436d-81e4-df8ffb552166",
                    "1337f492-aa23-42d0-af7a-fae45b47e55f",
                    "6dd177f4-3312-4b1e-a986-4421b5e83bff",
                    "18efedc5-8706-4ecb-afd4-e9294d3f1052",
                    "c6f80a93-5b16-45ef-a478-ca56a50f61ae",
                    "1675314b-0dfc-4d34-b854-0bdf0476221d",
                    "ec1cf82f-e8f3-4ca6-8e35-a4bdf0e8e259",
                    "81f76937-af88-4882-a64c-bc86a4092c20"
                },
                StringComparer.OrdinalIgnoreCase);

        public static bool IsGameRunning()
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

        public static AppState Discover()
        {
            AppState state = new AppState
            {
                Success = true,
                GameRunning = IsGameRunning(),
                Saves = new List<SaveFileInfo>()
            };

            try
            {
                string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string usersRoot = Path.Combine(
                    roaming, "Axolot Games", "Scrap Mechanic", "User");
                if (!Directory.Exists(usersRoot))
                    return state;

                foreach (string userDirectory in Directory.GetDirectories(usersRoot, "User_*"))
                {
                    string survival = Path.Combine(userDirectory, "Save", "Survival");
                    if (!Directory.Exists(survival))
                        continue;

                    foreach (string path in Directory.GetFiles(survival, "*.db", SearchOption.TopDirectoryOnly))
                    {
                        try
                        {
                            FileInfo info = new FileInfo(path);
                            state.Saves.Add(ToSaveInfo(info, new DirectoryInfo(userDirectory).Name));
                        }
                        catch
                        {
                            // One inaccessible save should not hide the rest.
                        }
                    }
                }

                state.Saves = state.Saves
                    .OrderByDescending(item => File.GetLastWriteTimeUtc(item.Path))
                    .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }
            catch (Exception exception)
            {
                state.Success = false;
                state.Error = FriendlyError(exception);
            }
            return state;
        }

        public static AnalysisResult Analyze(string path)
        {
            return AnalyzeCore(path, true, true);
        }

        public static AnalysisResult AnalyzeRaidsOnly(string path)
        {
            return AnalyzeCore(path, false, true);
        }

        private static AnalysisResult AnalyzeCore(
            string path, bool includeDroppedItems,
            bool enforceGameClosed)
        {
            AnalysisResult result = NewAnalysis(path);
            result.DroppedItemsScanned = includeDroppedItems;
            try
            {
                result.GameRunning =
                    enforceGameClosed && IsGameRunning();
                if (result.GameRunning)
                {
                    result.Success = false;
                    result.CanClear = false;
                    result.Error =
                        "Safety lock: Scrap Mechanic is running. ScrapLab did not open the save database. " +
                        "Close the game completely and try again.";
                    result.Warnings.Add(
                        "World analysis is locked while Scrap Mechanic is running.");
                    return result;
                }

                ValidatePath(path);
                FileInfo file = new FileInfo(path);
                result.Name = file.Name;
                result.Modified = file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
                result.SizeBytes = file.Length;
                result.Size = FormatBytes(file.Length);

                // Close the UI-to-I/O race if the game starts after the first check.
                result.GameRunning =
                    enforceGameClosed && IsGameRunning();
                if (result.GameRunning)
                {
                    result.Success = false;
                    result.CanClear = false;
                    result.Error =
                        "Safety lock: Scrap Mechanic started before analysis. " +
                        "ScrapLab did not open the save database.";
                    result.Warnings.Add(
                        "Close Scrap Mechanic completely before analyzing a world.");
                    return result;
                }

                using (SqliteDatabase database = SqliteDatabase.OpenReadOnly(path))
                {
                    result.DatabaseStatus = database.QuickCheck();
                    if (!String.Equals(result.DatabaseStatus, "ok", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Warnings.Add(
                            "SQLite reported database damage. ScrapLab will not edit this file.");
                    }

                    long saveVersion;
                    long gameTick;
                    database.ReadGameInfo(out saveVersion, out gameTick);
                    result.SaveVersion = saveVersion;
                    result.GameTick = gameTick;

                    if (!database.HasColumn("ScriptData", "uid") ||
                        !database.HasColumn("ScriptData", "key"))
                    {
                        throw new InvalidDataException(
                            "This save uses Scrap Mechanic's legacy pre-Chapter-2 database format. " +
                            "ScrapLab did not change it.");
                    }

                    long rowId;
                    byte[] record = database.ReadRaidRecord(out rowId);
                    result.RaidManagerPresent = record != null;
                    result.RaidManagerRowId = rowId;
                    object raidRoot = null;
                    if (record != null)
                    {
                        ScriptPayload payload = LuaStorage.ParseScriptData(record);
                        ValidateRaidPayload(payload);
                        raidRoot = payload.Value;
                        ReadRaids(raidRoot, database, result);
                    }

                    List<RaidCropReference> activeRaidCrops =
                        CollectRaidCropReferences(raidRoot);
                    ValidateActiveRaidCrops(
                        database, activeRaidCrops, result);
                    FindOrphanedRaidCrops(
                        database, activeRaidCrops, result);

                    if (includeDroppedItems)
                    {
                        result.DroppedItems = ReadDroppedItems(
                            database, result.GameTick, result, true);
                    }
                }

                if (!result.RaidManagerPresent)
                {
                    result.Warnings.Add(
                        "No stored raid-manager record was found. This save has no persisted raids to clear.");
                }

                result.RaidCount = result.Raids.Count;
                result.DroppedItemCount = result.DroppedItems.Count;
                result.DroppedItemQuantity =
                    result.DroppedItems.Sum(item => item.Quantity);
                result.ExpiredDroppedItemCount =
                    result.DroppedItems.Count(item => item.Expired);
                result.CanClear =
                    result.RaidManagerPresent &&
                    result.RaidCount > 0 &&
                    result.UnreleasableRaidCropCount == 0 &&
                    String.Equals(result.DatabaseStatus, "ok", StringComparison.OrdinalIgnoreCase) &&
                    !result.GameRunning;
                result.CanRepairOrphanedCrops =
                    result.OrphanedRaidCropCount > 0 &&
                    String.Equals(result.DatabaseStatus, "ok", StringComparison.OrdinalIgnoreCase) &&
                    !result.GameRunning;
                result.CanClearDroppedItems =
                    result.DroppedItemCount > 0 &&
                    String.Equals(result.DatabaseStatus, "ok", StringComparison.OrdinalIgnoreCase) &&
                    !result.GameRunning;
                result.CanClearExpiredDroppedItems =
                    result.ExpiredDroppedItemCount > 0 &&
                    String.Equals(result.DatabaseStatus, "ok", StringComparison.OrdinalIgnoreCase) &&
                    !result.GameRunning;
                result.Success = true;
            }
            catch (Exception exception)
            {
                result.Success = false;
                result.CanClear = false;
                result.CanRepairOrphanedCrops = false;
                result.CanClearDroppedItems = false;
                result.CanClearExpiredDroppedItems = false;
                result.Error = FriendlyError(exception);
            }
            return result;
        }

        public static RepairResult ClearRaids(string path)
        {
            return ClearRaidsCore(path, true);
        }

        private static RepairResult ClearRaidsCore(
            string path, bool enforceGameClosed)
        {
            RepairResult result = new RepairResult { Path = path };
            try
            {
                ValidatePath(path);
                if (enforceGameClosed && IsGameRunning())
                    throw new InvalidOperationException(
                        "Scrap Mechanic is running. Close the game completely and try again.");

                AnalysisResult before =
                    AnalyzeCore(path, false, enforceGameClosed);
                result.Before = before;
                if (!before.Success)
                    throw new InvalidOperationException("The save could not be analyzed: " + before.Error);
                if (!String.Equals(before.DatabaseStatus, "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "The save failed SQLite's integrity check and was not changed.");
                if (!before.RaidManagerPresent || before.RaidCount == 0)
                    throw new InvalidOperationException("This save has no stored raids to clear.");
                if (before.UnreleasableRaidCropCount > 0)
                    throw new InvalidOperationException(
                        "One or more live raid crops could not be safely decoded. " +
                        "The raid was not cleared.");

                string backup = MakeBackupPath(path);
                result.BackupPath = backup;
                SqliteDatabase.Backup(path, backup);

                using (SqliteDatabase backupDatabase = SqliteDatabase.OpenReadOnly(backup))
                {
                    string backupStatus = backupDatabase.QuickCheck();
                    if (!String.Equals(backupStatus, "ok", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException(
                            "The safety backup failed its integrity check. The original was not changed.");
                }

                if (enforceGameClosed && IsGameRunning())
                    throw new InvalidOperationException(
                        "Scrap Mechanic started while the backup was being made. The original was not changed.");

                using (SqliteDatabase database = SqliteDatabase.OpenReadWrite(path, false))
                {
                    bool transaction = false;
                    try
                    {
                        database.Execute("BEGIN IMMEDIATE");
                        transaction = true;
                        string status = database.QuickCheck();
                        if (!String.Equals(status, "ok", StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException(
                                "The save failed its final integrity check. No changes were committed.");

                        long currentRaidRowId;
                        byte[] currentRaidRecord =
                            database.ReadRaidRecord(out currentRaidRowId);
                        if (currentRaidRecord == null ||
                            currentRaidRowId != before.RaidManagerRowId)
                        {
                            throw new InvalidDataException(
                                "The raid-manager record changed after analysis. " +
                                "No changes were committed.");
                        }
                        ScriptPayload currentRaidPayload =
                            LuaStorage.ParseScriptData(currentRaidRecord);
                        ValidateRaidPayload(currentRaidPayload);
                        List<RaidCropReference> cropReferences =
                            CollectRaidCropReferences(
                                currentRaidPayload.Value);
                        ReleaseRaidCrops(
                            database, cropReferences, result);

                        result.RecordsRemoved = database.DeleteRaidRecord();
                        if (result.RecordsRemoved != 1)
                            throw new InvalidDataException(
                                "The expected raid-manager record was not found. No changes were committed.");

                        database.Execute("COMMIT");
                        transaction = false;
                        result.DatabaseStatus = database.QuickCheck();
                    }
                    catch
                    {
                        if (transaction)
                        {
                            try { database.Execute("ROLLBACK"); }
                            catch { }
                        }
                        throw;
                    }
                }

                if (!String.Equals(result.DatabaseStatus, "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(
                        "The edited save did not pass its final integrity check. Restore the safety backup.");

                result.After =
                    AnalyzeCore(path, false, enforceGameClosed);
                if (!result.After.Success || result.After.RaidManagerPresent)
                    throw new InvalidDataException(
                        "The raid-manager record could not be verified as cleared. Restore the safety backup.");

                result.Success = true;
            }
            catch (Exception exception)
            {
                result.Success = false;
                result.Error = FriendlyError(exception);
            }
            return result;
        }

        public static RepairResult RepairOrphanedRaidCrops(
            string path)
        {
            return RepairOrphanedRaidCropsCore(
                path, true);
        }

        private static RepairResult
            RepairOrphanedRaidCropsCore(
                string path, bool enforceGameClosed)
        {
            RepairResult result =
                new RepairResult { Path = path };
            try
            {
                ValidatePath(path);
                if (enforceGameClosed && IsGameRunning())
                    throw new InvalidOperationException(
                        "Scrap Mechanic is running. Close the game completely and try again.");

                AnalysisResult before =
                    AnalyzeCore(path, false, enforceGameClosed);
                result.Before = before;
                if (!before.Success)
                    throw new InvalidOperationException(
                        "The save could not be analyzed: " +
                        before.Error);
                if (!String.Equals(
                    before.DatabaseStatus, "ok",
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "The save failed SQLite's integrity check and was not changed.");
                }
                if (before.OrphanedRaidCropCount == 0)
                    throw new InvalidOperationException(
                        "This save has no orphaned raid crops to repair.");

                string backup = MakeBackupPath(path);
                result.BackupPath = backup;
                SqliteDatabase.Backup(path, backup);
                using (SqliteDatabase backupDatabase =
                    SqliteDatabase.OpenReadOnly(backup))
                {
                    string backupStatus =
                        backupDatabase.QuickCheck();
                    if (!String.Equals(
                        backupStatus, "ok",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            "The safety backup failed its integrity check. " +
                            "The original was not changed.");
                    }
                }

                if (enforceGameClosed && IsGameRunning())
                    throw new InvalidOperationException(
                        "Scrap Mechanic started while the backup was being made. " +
                        "The original was not changed.");

                using (SqliteDatabase database =
                    SqliteDatabase.OpenReadWrite(path, false))
                {
                    bool transaction = false;
                    try
                    {
                        database.Execute("BEGIN IMMEDIATE");
                        transaction = true;
                        string status = database.QuickCheck();
                        if (!String.Equals(
                            status, "ok",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidDataException(
                                "The save failed its final integrity check. " +
                                "No changes were committed.");
                        }

                        long raidRowId;
                        byte[] raidRecord =
                            database.ReadRaidRecord(out raidRowId);
                        object raidRoot = null;
                        if (raidRecord != null)
                        {
                            ScriptPayload raidPayload =
                                LuaStorage.ParseScriptData(
                                    raidRecord);
                            ValidateRaidPayload(raidPayload);
                            raidRoot = raidPayload.Value;
                        }
                        List<RaidCropReference> activeRaidCrops =
                            CollectRaidCropReferences(raidRoot);
                        AnalysisResult current =
                            NewAnalysis(path);
                        List<RaidCropStorageState> orphaned =
                            FindOrphanedRaidCrops(
                                database, activeRaidCrops,
                                current);
                        if (orphaned.Count == 0)
                        {
                            throw new InvalidDataException(
                                "The orphaned crop list changed after analysis. " +
                                "No changes were committed.");
                        }

                        foreach (
                            RaidCropStorageState crop in orphaned)
                        {
                            ReleaseCropStorage(
                                database, crop, result);
                        }

                        database.Execute("COMMIT");
                        transaction = false;
                        result.DatabaseStatus =
                            database.QuickCheck();
                    }
                    catch
                    {
                        if (transaction)
                        {
                            try { database.Execute("ROLLBACK"); }
                            catch { }
                        }
                        throw;
                    }
                }

                if (!String.Equals(
                    result.DatabaseStatus, "ok",
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "The edited save did not pass its final integrity check. " +
                        "Restore the safety backup.");
                }

                result.After =
                    AnalyzeCore(path, false, enforceGameClosed);
                if (!result.After.Success ||
                    result.After.OrphanedRaidCropCount != 0)
                {
                    throw new InvalidDataException(
                        "The orphaned crop repair could not be verified. " +
                        "Restore the safety backup.");
                }
                result.Success = true;
            }
            catch (Exception exception)
            {
                result.Success = false;
                result.Error = FriendlyError(exception);
            }
            return result;
        }

        public static DroppedItemRepairResult ClearDroppedItems(
            string path, long entityId)
        {
            return ClearDroppedItemsCore(path, entityId, false);
        }

        public static DroppedItemRepairResult ClearExpiredDroppedItems(
            string path)
        {
            return ClearDroppedItemsCore(path, 0, true);
        }

        private static DroppedItemRepairResult ClearDroppedItemsCore(
            string path, long entityId, bool expiredOnly)
        {
            DroppedItemRepairResult result = new DroppedItemRepairResult
            {
                Path = path,
                TargetEntityId = entityId
            };
            try
            {
                ValidatePath(path);
                if (entityId < 0)
                    throw new ArgumentOutOfRangeException(
                        "entityId", "The dropped-item identifier is invalid.");
                if (IsGameRunning())
                    throw new InvalidOperationException(
                        "Scrap Mechanic is running. Close the game completely and try again.");

                AnalysisResult before = Analyze(path);
                result.Before = before;
                if (!before.Success)
                    throw new InvalidOperationException(
                        "The save could not be analyzed: " + before.Error);
                if (!String.Equals(
                    before.DatabaseStatus, "ok",
                    StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "The save failed SQLite's integrity check and was not changed.");

                List<DroppedItemInfo> requested = SelectDroppedItems(
                    before.DroppedItems, entityId, expiredOnly);
                if (requested.Count == 0)
                {
                    throw new InvalidOperationException(
                        expiredOnly
                            ? "This save has no expired loose items pending world cleanup."
                            : entityId == 0
                            ? "This save has no decoded loose items to clear."
                            : "That loose item is no longer present in the selected save.");
                }

                string backup = MakeBackupPath(path);
                result.BackupPath = backup;
                SqliteDatabase.Backup(path, backup);

                using (SqliteDatabase backupDatabase =
                    SqliteDatabase.OpenReadOnly(backup))
                {
                    string backupStatus = backupDatabase.QuickCheck();
                    if (!String.Equals(
                        backupStatus, "ok",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            "The safety backup failed its integrity check. " +
                            "The original was not changed.");
                    }

                    long backupVersion;
                    long backupTick;
                    backupDatabase.ReadGameInfo(
                        out backupVersion, out backupTick);
                    AnalysisResult backupScan = NewAnalysis(backup);
                    List<DroppedItemInfo> backupItems = ReadDroppedItems(
                        backupDatabase, backupTick, backupScan, false);
                    VerifyDroppedItemSnapshot(
                        requested,
                        SelectDroppedItems(
                            backupItems, entityId, expiredOnly));
                }

                if (IsGameRunning())
                    throw new InvalidOperationException(
                        "Scrap Mechanic started while the backup was being made. " +
                        "The original was not changed.");

                using (SqliteDatabase database =
                    SqliteDatabase.OpenReadWrite(path, false))
                {
                    bool transaction = false;
                    try
                    {
                        database.Execute("BEGIN IMMEDIATE");
                        transaction = true;
                        string status = database.QuickCheck();
                        if (!String.Equals(
                            status, "ok",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidDataException(
                                "The save failed its final integrity check. " +
                                "No changes were committed.");
                        }

                        long saveVersion;
                        long gameTick;
                        database.ReadGameInfo(out saveVersion, out gameTick);
                        AnalysisResult liveScan = NewAnalysis(path);
                        List<DroppedItemInfo> currentItems =
                            ReadDroppedItems(
                                database, gameTick, liveScan, false);
                        List<DroppedItemInfo> targets =
                            SelectDroppedItems(
                                currentItems, entityId, expiredOnly);
                        VerifyDroppedItemSnapshot(requested, targets);

                        foreach (DroppedItemInfo item in targets)
                        {
                            if (database.DeleteScriptDataRow(
                                item.ScriptRowId) != 1)
                            {
                                throw new InvalidDataException(
                                    "The storage record for " + item.Name +
                                    " changed before removal. No changes were committed.");
                            }
                            if (database.DeleteHarvestable(item.EntityId) != 1)
                            {
                                throw new InvalidDataException(
                                    "The world entity for " + item.Name +
                                    " changed before removal. No changes were committed.");
                            }
                            result.ItemsRemoved++;
                            result.QuantityRemoved += item.Quantity;
                        }

                        string editedStatus = database.QuickCheck();
                        if (!String.Equals(
                            editedStatus, "ok",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidDataException(
                                "The edited save failed its integrity check. " +
                                "No changes were committed.");
                        }

                        database.Execute("COMMIT");
                        transaction = false;
                        result.DatabaseStatus = database.QuickCheck();
                    }
                    catch
                    {
                        if (transaction)
                        {
                            try { database.Execute("ROLLBACK"); }
                            catch { }
                        }
                        throw;
                    }
                }

                if (!String.Equals(
                    result.DatabaseStatus, "ok",
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "The edited save did not pass its final integrity check. " +
                        "Restore the safety backup.");
                }

                result.After = Analyze(path);
                if (!result.After.Success)
                    throw new InvalidDataException(
                        "The edited save could not be re-analyzed. " +
                        "Restore the safety backup.");
                if (result.After.RaidManagerPresent !=
                        before.RaidManagerPresent ||
                    result.After.RaidCount != before.RaidCount)
                {
                    throw new InvalidDataException(
                        "Unrelated raid storage changed during loose-item removal. " +
                        "Restore the safety backup.");
                }

                foreach (DroppedItemInfo removed in requested)
                {
                    if (result.After.DroppedItems.Any(
                        item => item.EntityId == removed.EntityId))
                    {
                        throw new InvalidDataException(
                            "A removed loose item was still present after verification. " +
                            "Restore the safety backup.");
                    }
                }

                result.Success = true;
            }
            catch (Exception exception)
            {
                result.Success = false;
                result.Error = FriendlyError(exception);
            }
            return result;
        }

        private static List<DroppedItemInfo> ReadDroppedItems(
            SqliteDatabase database, long gameTick,
            AnalysisResult analysis, bool includeIcons)
        {
            List<DroppedItemInfo> items = new List<DroppedItemInfo>();
            Dictionary<int, string> worldNames =
                WorldStorage.ReadWorldNames(database);
            foreach (HarvestableRecord harvestable in
                database.ReadHarvestables())
            {
                string harvestableUuid;
                PositionInfo position;
                if (!TryReadHarvestableData(
                    harvestable.Data, out harvestableUuid, out position) ||
                    !IsLooseLootType(harvestableUuid))
                    continue;

                try
                {
                    if (harvestable.Id < 0 ||
                        harvestable.Id > UInt32.MaxValue)
                    {
                        throw new InvalidDataException(
                            "The loose-item entity identifier is outside " +
                            "Scrap Mechanic's supported range.");
                    }

                    byte[] key = BitConverter.GetBytes(
                        checked((uint)harvestable.Id));
                    List<StoredScriptRecord> scripts =
                        database.ReadScriptRecords(
                            key, harvestable.WorldId);
                    if (scripts.Count != 1)
                    {
                        throw new InvalidDataException(
                            scripts.Count == 0
                                ? "Its Lua storage record is missing."
                                : "Its Lua storage key is ambiguous.");
                    }

                    StoredScriptRecord script = scripts[0];
                    ScriptPayload payload =
                        LuaStorage.ParseScriptData(script.Data);
                    if (!BytesEqual(payload.Key, script.Key) ||
                        payload.WorldId != script.WorldId ||
                        payload.LuaVersion < 1)
                    {
                        throw new InvalidDataException(
                            "Its Lua storage header does not match the database row.");
                    }

                    LuaTable root = payload.Value as LuaTable;
                    LuaUserData uuidData =
                        root == null
                            ? null
                            : root.Get("uuid") as LuaUserData;
                    if (uuidData == null ||
                        !String.Equals(
                            uuidData.Type, "Uuid",
                            StringComparison.Ordinal) ||
                        String.IsNullOrEmpty(uuidData.Uuid))
                    {
                        throw new InvalidDataException(
                            "Its item UUID is missing from Lua storage.");
                    }

                    long quantity = ToLong(root.Get("quantity"), 0);
                    if (quantity <= 0)
                        throw new InvalidDataException(
                            "Its stored stack quantity is invalid.");

                    long killTick = ToLong(root.Get("killTick"), 0);
                    long remainingTicks =
                        killTick > 0 ? killTick - gameTick : 0;
                    ItemCatalogEntry catalog = includeIcons
                        ? ItemCatalog.Find(uuidData.Uuid)
                        : new ItemCatalogEntry
                        {
                            Name = uuidData.Uuid,
                            Description = String.Empty,
                            IconDataUrl = String.Empty
                        };
                    if (includeIcons &&
                        !analysis.DroppedItemIcons.ContainsKey(
                            uuidData.Uuid))
                    {
                        analysis.DroppedItemIcons[uuidData.Uuid] =
                            catalog.IconDataUrl ?? String.Empty;
                    }

                    bool epic = ToBool(root.Get("epic"));
                    bool questItem = ToBool(root.Get("questItem"));
                    string limitedLoot =
                        AsString(root.Get("limitedLoot")) ?? String.Empty;
                    int valueScore = catalog.RecoveryValue;
                    if (epic)
                        valueScore = Math.Max(valueScore, 7000);
                    if (!String.IsNullOrEmpty(limitedLoot))
                        valueScore = Math.Max(valueScore, 8000);
                    if (questItem)
                        valueScore = Math.Max(valueScore, 10000);

                    items.Add(new DroppedItemInfo
                    {
                        EntityId = harvestable.Id,
                        WorldId = harvestable.WorldId,
                        WorldName = WorldStorage.ResolveName(
                            worldNames, harvestable.WorldId),
                        CellX = harvestable.CellX,
                        CellY = harvestable.CellY,
                        Uuid = uuidData.Uuid,
                        Name = catalog.Name,
                        Description = catalog.Description ?? String.Empty,
                        Quantity = quantity,
                        ValueScore = valueScore,
                        ValueTier = ItemCatalog.RecoveryTier(valueScore),
                        DropType = LootTypeName(harvestableUuid),
                        Position = position,
                        KillTick = killTick,
                        RemainingTicks = remainingTicks,
                        RemainingSeconds = remainingTicks > 0
                            ? (remainingTicks + ServerTicksPerSecond - 1) /
                                ServerTicksPerSecond
                            : 0,
                        Expired = killTick > 0 && remainingTicks <= 0,
                        Epic = epic,
                        QuestItem = questItem,
                        LimitedLoot = limitedLoot,
                        ScriptRowId = script.RowId,
                        ScriptKey = script.Key
                    });
                }
                catch (Exception exception)
                {
                    analysis.UnreadableDroppedItemCount++;
                    analysis.Warnings.Add(
                        "Loose item #" +
                        harvestable.Id.ToString(
                            CultureInfo.InvariantCulture) +
                        " could not be decoded and will not be offered for removal: " +
                        exception.Message);
                }
            }

            return items
                .OrderByDescending(item => item.ValueScore)
                .ThenByDescending(item => item.Quantity)
                .ThenBy(item => item.Name,
                    StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.EntityId)
                .ToList();
        }

        private static bool TryReadHarvestableData(
            byte[] data, out string uuid, out PositionInfo position)
        {
            uuid = null;
            position = null;
            if (data == null || data.Length < 64)
                return false;

            byte[] uuidBytes = new byte[16];
            Buffer.BlockCopy(data, 20, uuidBytes, 0, 16);
            Array.Reverse(uuidBytes);
            string hex = BitConverter.ToString(uuidBytes)
                .Replace("-", "").ToLowerInvariant();
            uuid =
                hex.Substring(0, 8) + "-" +
                hex.Substring(8, 4) + "-" +
                hex.Substring(12, 4) + "-" +
                hex.Substring(16, 4) + "-" +
                hex.Substring(20, 12);

            position = new PositionInfo
            {
                Z = ReadBigEndianSingle(data, 36),
                X = ReadBigEndianSingle(data, 40),
                Y = ReadBigEndianSingle(data, 44)
            };
            return true;
        }

        private static double ReadBigEndianSingle(
            byte[] data, int offset)
        {
            byte[] bytes = new byte[4];
            Buffer.BlockCopy(data, offset, bytes, 0, 4);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return BitConverter.ToSingle(bytes, 0);
        }

        private static bool IsLooseLootType(string uuid)
        {
            return String.Equals(
                    uuid, NormalLootUuid,
                    StringComparison.OrdinalIgnoreCase) ||
                String.Equals(
                    uuid, BiggerLootUuid,
                    StringComparison.OrdinalIgnoreCase) ||
                String.Equals(
                    uuid, LimitedLootUuid,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string LootTypeName(string uuid)
        {
            if (String.Equals(
                uuid, LimitedLootUuid,
                StringComparison.OrdinalIgnoreCase))
                return "Limited loot";
            if (String.Equals(
                uuid, BiggerLootUuid,
                StringComparison.OrdinalIgnoreCase))
                return "Quest / large loot";
            return "Loose pickup";
        }

        private static List<DroppedItemInfo> SelectDroppedItems(
            IEnumerable<DroppedItemInfo> items, long entityId,
            bool expiredOnly)
        {
            if (items == null)
                return new List<DroppedItemInfo>();
            return items
                .Where(item =>
                    (entityId == 0 || item.EntityId == entityId) &&
                    (!expiredOnly || item.Expired))
                .OrderBy(item => item.EntityId)
                .ToList();
        }

        private static void VerifyDroppedItemSnapshot(
            IList<DroppedItemInfo> expected,
            IList<DroppedItemInfo> actual)
        {
            if (expected == null || actual == null ||
                expected.Count != actual.Count)
            {
                throw new InvalidDataException(
                    "The loose-item list changed during backup verification. " +
                    "The save was not edited.");
            }

            for (int index = 0; index < expected.Count; index++)
            {
                DroppedItemInfo left = expected[index];
                DroppedItemInfo right = actual[index];
                if (left.EntityId != right.EntityId ||
                    left.WorldId != right.WorldId ||
                    left.Quantity != right.Quantity ||
                    !String.Equals(
                        left.Uuid, right.Uuid,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "Loose item #" +
                        left.EntityId.ToString(
                            CultureInfo.InvariantCulture) +
                        " changed during backup verification. " +
                        "The save was not edited.");
                }
            }
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null ||
                left.Length != right.Length)
                return false;
            for (int index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index])
                    return false;
            }
            return true;
        }

        private static List<RaidCropReference>
            CollectRaidCropReferences(object rootValue)
        {
            Dictionary<string, RaidCropReference> references =
                new Dictionary<string, RaidCropReference>(
                    StringComparer.Ordinal);
            LuaTable root = rootValue as LuaTable;
            LuaTable worlds =
                root == null
                    ? null
                    : root.Get("worldRaids") as LuaTable;
            if (worlds == null)
                return references.Values.ToList();

            foreach (LuaEntry worldEntry in worlds.Entries)
            {
                int worldId = ToInt(worldEntry.Key, 0);
                LuaTable raids = worldEntry.Value as LuaTable;
                if (raids == null)
                    continue;
                foreach (LuaEntry raidEntry in raids.Entries)
                {
                    LuaTable raid = raidEntry.Value as LuaTable;
                    LuaTable crops =
                        raid == null
                            ? null
                            : raid.Get("existingCrops") as LuaTable;
                    if (crops == null)
                        continue;
                    foreach (LuaEntry cropEntry in crops.Entries)
                    {
                        LuaUserData reference =
                            cropEntry.Value as LuaUserData;
                        if (reference == null ||
                            !String.Equals(
                                reference.Type, "Harvestable",
                                StringComparison.Ordinal) ||
                            reference.Id < 0 ||
                            reference.Id > UInt32.MaxValue)
                        {
                            continue;
                        }
                        RaidCropReference crop =
                            new RaidCropReference
                            {
                                WorldId = worldId,
                                HarvestableId = reference.Id
                            };
                        references[CropKey(crop)] = crop;
                    }
                }
            }
            return references.Values
                .OrderBy(crop => crop.WorldId)
                .ThenBy(crop => crop.HarvestableId)
                .ToList();
        }

        private static void ValidateActiveRaidCrops(
            SqliteDatabase database,
            IList<RaidCropReference> crops,
            AnalysisResult analysis)
        {
            foreach (RaidCropReference crop in crops)
            {
                HarvestableRecord harvestable =
                    database.ReadHarvestable(
                        crop.HarvestableId);
                if (harvestable == null ||
                    harvestable.WorldId != crop.WorldId)
                {
                    continue;
                }
                try
                {
                    ReadRaidCropStorage(
                        database, crop, harvestable);
                }
                catch (Exception exception)
                {
                    analysis.UnreleasableRaidCropCount++;
                    analysis.Warnings.Add(
                        "Raid crop #" +
                        crop.HarvestableId.ToString(
                            CultureInfo.InvariantCulture) +
                        " could not be prepared for safe raid clearing: " +
                        exception.Message);
                }
            }
            if (analysis.UnreleasableRaidCropCount > 0)
            {
                analysis.Warnings.Add(
                    "Raid clearing is locked because " +
                    analysis.UnreleasableRaidCropCount.ToString(
                        CultureInfo.InvariantCulture) +
                    " existing raid crop record(s) could not be proven safe.");
            }
        }

        private static List<RaidCropStorageState>
            FindOrphanedRaidCrops(
                SqliteDatabase database,
                IList<RaidCropReference> activeRaidCrops,
                AnalysisResult analysis)
        {
            HashSet<string> active =
                new HashSet<string>(
                    activeRaidCrops.Select(CropKey),
                    StringComparer.Ordinal);
            List<RaidCropStorageState> orphaned =
                new List<RaidCropStorageState>();

            foreach (HarvestableRecord harvestable in
                database.ReadHarvestables())
            {
                string uuid;
                PositionInfo position;
                if (!TryReadHarvestableData(
                    harvestable.Data, out uuid, out position) ||
                    !RaidCropUuids.Contains(uuid))
                {
                    continue;
                }

                RaidCropReference crop =
                    new RaidCropReference
                    {
                        WorldId = harvestable.WorldId,
                        HarvestableId = harvestable.Id
                    };
                try
                {
                    RaidCropStorageState storage =
                        ReadRaidCropStorage(
                            database, crop, harvestable);
                    if (storage.FlagPresent &&
                        !storage.HasSurvivedRaid &&
                        !active.Contains(CropKey(crop)))
                    {
                        orphaned.Add(storage);
                    }
                }
                catch (Exception exception)
                {
                    analysis.UnreadableRaidCropCount++;
                    if (analysis.UnreadableRaidCropCount <= 8)
                    {
                        analysis.Warnings.Add(
                            "Growing crop #" +
                            harvestable.Id.ToString(
                                CultureInfo.InvariantCulture) +
                            " has unreadable raid-growth storage: " +
                            exception.Message);
                    }
                }
            }

            analysis.OrphanedRaidCropCount = orphaned.Count;
            if (analysis.UnreadableRaidCropCount > 8)
            {
                analysis.Warnings.Add(
                    (analysis.UnreadableRaidCropCount - 8).ToString(
                        CultureInfo.InvariantCulture) +
                    " additional growing crop storage warning(s) were omitted.");
            }
            if (orphaned.Count > 0)
            {
                analysis.Warnings.Add(
                    orphaned.Count.ToString(
                        CultureInfo.InvariantCulture) +
                    " growing crop(s) are still waiting for a raid that no longer exists. " +
                    "Use Repair Orphaned Crops to release their growth safely.");
            }
            return orphaned;
        }

        private static RaidCropStorageState
            ReadRaidCropStorage(
                SqliteDatabase database,
                RaidCropReference crop,
                HarvestableRecord harvestable)
        {
            if (harvestable.Id != crop.HarvestableId ||
                harvestable.WorldId != crop.WorldId)
            {
                throw new InvalidDataException(
                    "The harvestable identity does not match the raid reference.");
            }

            string uuid;
            PositionInfo position;
            if (!TryReadHarvestableData(
                harvestable.Data, out uuid, out position) ||
                !RaidCropUuids.Contains(uuid))
            {
                throw new InvalidDataException(
                    "The referenced harvestable is not a supported growing raid crop.");
            }

            List<StoredScriptRecord> scripts =
                database.ReadScriptRecords(
                    CropStorageKey(crop.HarvestableId),
                    crop.WorldId);
            if (scripts.Count != 1)
            {
                throw new InvalidDataException(
                    scripts.Count == 0
                        ? "Its crop storage record is missing."
                        : "Its crop storage key is ambiguous.");
            }

            StoredScriptRecord script = scripts[0];
            ScriptPayload payload =
                LuaStorage.ParseScriptData(script.Data);
            if (!BytesEqual(payload.Key, script.Key) ||
                payload.WorldId != script.WorldId ||
                payload.Flags != script.Flags ||
                payload.LuaVersion < 1)
            {
                throw new InvalidDataException(
                    "Its Lua storage header does not match the database row.");
            }
            LuaTable root = payload.Value as LuaTable;
            if (root == null)
                throw new InvalidDataException(
                    "Its crop storage root is not a Lua table.");

            object flag = root.Get("hasSurvivedRaid");
            RaidCropStorageState state =
                new RaidCropStorageState
                {
                    Crop = crop,
                    Script = script,
                    FlagPresent = flag != null
                };
            if (flag != null)
            {
                if (!(flag is bool))
                    throw new InvalidDataException(
                        "Its hasSurvivedRaid value is not a boolean.");
                state.HasSurvivedRaid = (bool)flag;
            }
            return state;
        }

        private static void ReleaseRaidCrops(
            SqliteDatabase database,
            IList<RaidCropReference> crops,
            RepairResult result)
        {
            foreach (RaidCropReference crop in crops)
            {
                HarvestableRecord harvestable =
                    database.ReadHarvestable(
                        crop.HarvestableId);
                if (harvestable == null ||
                    harvestable.WorldId != crop.WorldId)
                {
                    result.MissingCropReferences++;
                    continue;
                }

                RaidCropStorageState storage =
                    ReadRaidCropStorage(
                        database, crop, harvestable);
                if (!storage.FlagPresent ||
                    storage.HasSurvivedRaid)
                {
                    result.CropsAlreadySafe++;
                    continue;
                }
                ReleaseCropStorage(
                    database, storage, result);
            }
        }

        private static void ReleaseCropStorage(
            SqliteDatabase database,
            RaidCropStorageState storage,
            RepairResult result)
        {
            if (!storage.FlagPresent ||
                storage.HasSurvivedRaid)
            {
                result.CropsAlreadySafe++;
                return;
            }

            bool found;
            bool originalValue;
            byte[] rewritten = LuaStorage.SetRootBoolean(
                storage.Script.Data, "hasSurvivedRaid",
                true, out found, out originalValue);
            if (!found || originalValue)
            {
                throw new InvalidDataException(
                    "The crop survival flag changed before it could be repaired.");
            }
            int changed = database.UpdateScriptData(
                storage.Script.RowId,
                storage.Script.Key,
                storage.Script.WorldId,
                storage.Script.Data,
                rewritten);
            if (changed != 1)
            {
                throw new InvalidDataException(
                    "The crop storage row changed before it could be repaired.");
            }

            StoredScriptRecord verified =
                database.ReadScriptRecord(
                    storage.Script.RowId);
            if (verified == null ||
                !BytesEqual(
                    verified.Key, storage.Script.Key) ||
                verified.WorldId !=
                    storage.Script.WorldId)
            {
                throw new InvalidDataException(
                    "The repaired crop storage row could not be found.");
            }
            ScriptPayload payload =
                LuaStorage.ParseScriptData(verified.Data);
            LuaTable root = payload.Value as LuaTable;
            object flag =
                root == null
                    ? null
                    : root.Get("hasSurvivedRaid");
            if (!(flag is bool) || !(bool)flag)
            {
                throw new InvalidDataException(
                    "The repaired crop survival flag could not be verified.");
            }
            result.CropsReleased++;
        }

        private static byte[] CropStorageKey(
            long harvestableId)
        {
            if (harvestableId < 0 ||
                harvestableId > UInt32.MaxValue)
            {
                throw new InvalidDataException(
                    "The crop identifier is outside Scrap Mechanic's supported range.");
            }
            uint value = checked((uint)harvestableId);
            return new[]
            {
                (byte)value,
                (byte)(value >> 8),
                (byte)(value >> 16),
                (byte)(value >> 24)
            };
        }

        private static string CropKey(
            RaidCropReference crop)
        {
            return crop.WorldId.ToString(
                CultureInfo.InvariantCulture) +
                ":" +
                crop.HarvestableId.ToString(
                    CultureInfo.InvariantCulture);
        }

        private static void ReadRaids(
            object rootValue, SqliteDatabase database, AnalysisResult analysis)
        {
            LuaTable root = rootValue as LuaTable;
            if (root == null)
                throw new InvalidDataException("The raid-manager root value is not a Lua table.");

            LuaTable worlds = root.Get("worldRaids") as LuaTable;
            if (worlds == null)
                return;

            Dictionary<int, string> worldNames =
                WorldStorage.ReadWorldNames(database);
            foreach (LuaEntry worldEntry in worlds.Entries)
            {
                LuaTable raids = worldEntry.Value as LuaTable;
                if (raids == null)
                    continue;

                int worldSlot = ToInt(worldEntry.Key, 0);
                foreach (LuaEntry raidEntry in raids.Entries)
                {
                    LuaTable raid = raidEntry.Value as LuaTable;
                    if (raid == null)
                        continue;
                    RaidInfo info = ParseRaid(raidEntry.Key, raid, worldSlot, database);
                    info.WorldName =
                        WorldStorage.ResolveName(worldNames, worldSlot);
                    info.Number = analysis.Raids.Count + 1;
                    analysis.Raids.Add(info);
                    if (info.LiveRaiderReferences > 0)
                    {
                        analysis.Warnings.Add(
                            "Raid " + info.Number.ToString(CultureInfo.InvariantCulture) +
                            " references " + info.LiveRaiderReferences.ToString(CultureInfo.InvariantCulture) +
                            " already-spawned robot(s). Their raid schedule will be removed, but world units " +
                            "are deliberately left untouched to avoid deleting unrelated entities.");
                    }
                }
            }
        }

        private static RaidInfo ParseRaid(
            object entryKey, LuaTable raid, int worldSlot, SqliteDatabase database)
        {
            RaidInfo info = new RaidInfo
            {
                WorldSlot = worldSlot,
                Key = AsString(raid.Get("key")) ?? AsString(entryKey) ?? "(unknown)",
                Tier = ToLong(raid.Get("level"), 0),
                ThreatValue = ToLong(raid.Get("value"), 0),
                MaximumThreatValue = ToLong(raid.Get("maxValue"), 0),
                TickCounter = ToLong(raid.Get("tickCounter"), 0),
                LastSpawnTick = ToLong(raid.Get("lastSpawnTick"), 0),
                TimeoutTick = ToLong(raid.Get("timeoutTick"), 0),
                SavedTick = ToLong(raid.Get("savedTick"), 0),
                NeedsSpawnPoints = ToBool(raid.Get("needsSpawnPoints")),
                Enemies = new List<EnemyInfo>(),
                Crops = new List<EnemyInfo>(),
                Notes = new List<string>()
            };

            LuaUserData center = raid.Get("center") as LuaUserData;
            if (center != null && center.Type == "Vec3")
            {
                info.Center = new PositionInfo
                {
                    X = center.X,
                    Y = center.Y,
                    Z = center.Z
                };
            }

            LuaTable existingCrops = raid.Get("existingCrops") as LuaTable;
            if (existingCrops != null)
            {
                foreach (LuaEntry entry in existingCrops.Entries)
                {
                    LuaUserData reference = entry.Value as LuaUserData;
                    if (reference == null || reference.Type != "Harvestable")
                        continue;
                    info.TrackedCrops++;
                    if (!database.RowExists("Harvestable", reference.Id))
                        info.StaleCropReferences++;
                }
            }

            LuaTable plantingOrder = raid.Get("plantingOrder") as LuaTable;
            info.PlantingRecords = plantingOrder == null ? 0 : plantingOrder.Count;
            info.Crops = AggregateCrops(plantingOrder);

            LuaTable raiders = raid.Get("raiders") as LuaTable;
            info.LiveRaiderReferences = CountReferences(raiders, "Unit");

            LuaTable attackData = raid.Get("attackData") as LuaTable;
            LuaTable groups = attackData == null ? null : attackData.Get("groupSpawns") as LuaTable;
            info.SpawnGroups = groups == null ? 0 : groups.Count;
            info.Enemies = AggregateEnemies(groups);
            long aggregated = info.Enemies.Sum(item => item.Quantity);
            info.PlannedEnemyCount = ToLong(raid.Get("totalEnemyCount"), aggregated);

            int cropPositions = TableCount(raid.Get("cropPositions"));
            int plantingLocations = TableCount(raid.Get("plantingLocations"));
            if (info.NeedsSpawnPoints)
                info.State = "Preparing spawn points";
            else if (attackData != null && info.LastSpawnTick > 0)
                info.State = "Active / spawning";
            else if (attackData != null)
                info.State = "Scheduled";
            else
                info.State = "Tracking crops";

            info.LooksStuck =
                info.TrackedCrops > 0 &&
                info.StaleCropReferences == info.TrackedCrops &&
                cropPositions == 0 &&
                plantingLocations == 0;
            if (info.LooksStuck)
            {
                info.Notes.Add(
                    "All stored crop object references are missing while the raid record remains.");
            }
            if (info.PlannedEnemyCount != aggregated && aggregated > 0)
            {
                info.Notes.Add(
                    "Stored total enemy count differs from the decoded spawn-group total.");
            }
            if (info.LiveRaiderReferences > 0)
            {
                info.Notes.Add(
                    "Already-spawned raid robots are referenced; the safe repair does not delete world units.");
            }
            return info;
        }

        private static List<EnemyInfo> AggregateEnemies(LuaTable groups)
        {
            Dictionary<string, long> counts =
                new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            if (groups != null)
            {
                foreach (LuaEntry groupEntry in groups.Entries)
                {
                    LuaTable group = groupEntry.Value as LuaTable;
                    LuaTable enemies = group == null ? null : group.Get("enemyList") as LuaTable;
                    if (enemies == null)
                        continue;

                    foreach (LuaEntry enemyEntry in enemies.Entries)
                    {
                        LuaTable enemy = enemyEntry.Value as LuaTable;
                        LuaUserData uuid = enemy == null ? null : enemy.Get("uuid") as LuaUserData;
                        if (uuid == null || String.IsNullOrEmpty(uuid.Uuid))
                            continue;
                        long quantity = ToLong(enemy.Get("quantity"), 0);
                        counts[uuid.Uuid] = counts.ContainsKey(uuid.Uuid)
                            ? counts[uuid.Uuid] + quantity
                            : quantity;
                    }
                }
            }
            return counts
                .Select(pair => new EnemyInfo
                {
                    Uuid = pair.Key,
                    Name = FriendlyName(EnemyNames, pair.Key, "Unknown robot"),
                    Quantity = pair.Value
                })
                .OrderByDescending(item => item.Quantity)
                .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static List<EnemyInfo> AggregateCrops(LuaTable plantingOrder)
        {
            Dictionary<string, long> counts =
                new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            if (plantingOrder != null)
            {
                foreach (LuaEntry recordEntry in plantingOrder.Entries)
                {
                    LuaTable record = recordEntry.Value as LuaTable;
                    LuaUserData uuid = record == null ? null : record.Get("uid") as LuaUserData;
                    if (uuid == null || String.IsNullOrEmpty(uuid.Uuid))
                        continue;
                    counts[uuid.Uuid] = counts.ContainsKey(uuid.Uuid)
                        ? counts[uuid.Uuid] + 1
                        : 1;
                }
            }
            return counts
                .Select(pair => new EnemyInfo
                {
                    Uuid = pair.Key,
                    Name = FriendlyName(CropNames, pair.Key, "Unknown crop"),
                    Quantity = pair.Value
                })
                .OrderByDescending(item => item.Quantity)
                .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static string FriendlyName(
            Dictionary<string, string> names, string uuid, string fallback)
        {
            string name;
            return names.TryGetValue(uuid, out name) ? name : fallback;
        }

        private static int CountReferences(LuaTable table, string type)
        {
            if (table == null)
                return 0;
            int count = 0;
            foreach (LuaEntry entry in table.Entries)
            {
                if (ContainsReference(entry.Value, type, 0))
                    count++;
            }
            return count;
        }

        private static bool ContainsReference(object value, string type, int depth)
        {
            if (depth > 16)
                return false;
            LuaUserData data = value as LuaUserData;
            if (data != null)
                return data.Type == type;
            LuaTable table = value as LuaTable;
            if (table == null)
                return false;
            foreach (LuaEntry entry in table.Entries)
            {
                if (ContainsReference(entry.Value, type, depth + 1))
                    return true;
            }
            return false;
        }

        private static void ValidateRaidPayload(ScriptPayload payload)
        {
            if (payload.Key == null || BitConverter.ToString(payload.Key).Replace("-", "") !=
                "4C554100000001082D")
                throw new InvalidDataException("The raid-manager storage key is unexpected.");
            if (payload.LuaVersion < 1)
                throw new InvalidDataException("The raid-manager serialization version is unsupported.");
        }

        private static AnalysisResult NewAnalysis(string path)
        {
            return new AnalysisResult
            {
                Path = path,
                DatabaseStatus = "not checked",
                Raids = new List<RaidInfo>(),
                DroppedItems = new List<DroppedItemInfo>(),
                DroppedItemIcons =
                    new Dictionary<string, string>(
                        StringComparer.OrdinalIgnoreCase),
                Warnings = new List<string>()
            };
        }

        private static void ValidatePath(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Choose a Scrap Mechanic survival save first.");
            string full = Path.GetFullPath(path);
            if (!File.Exists(full))
                throw new FileNotFoundException("The selected save file does not exist.", full);
            if (new FileInfo(full).Length < 100)
                throw new InvalidDataException("The selected file is too small to be a survival save.");
        }

        private static string MakeBackupPath(string path)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            string name = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            string candidate = Path.Combine(
                directory, name + ".scraplab-backup-" + stamp + extension);
            int suffix = 2;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(
                    directory,
                    name + ".scraplab-backup-" + stamp + "-" +
                    suffix.ToString(CultureInfo.InvariantCulture) + extension);
                suffix++;
            }
            return candidate;
        }

        private static SaveFileInfo ToSaveInfo(FileInfo file, string userFolder)
        {
            return new SaveFileInfo
            {
                Path = file.FullName,
                Name = file.Name,
                Modified = file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                SizeBytes = file.Length,
                Size = FormatBytes(file.Length),
                UserFolder = userFolder
            };
        }

        private static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            double value = bytes;
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }
            return value.ToString(unit == 0 ? "0" : "0.0", CultureInfo.InvariantCulture) +
                   " " + units[unit];
        }

        private static int TableCount(object value)
        {
            LuaTable table = value as LuaTable;
            return table == null ? 0 : table.Count;
        }

        private static string AsString(object value)
        {
            return value as string;
        }

        private static bool ToBool(object value)
        {
            return value is bool && (bool)value;
        }

        private static long ToLong(object value, long fallback)
        {
            if (value == null)
                return fallback;
            try
            {
                return Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static int ToInt(object value, int fallback)
        {
            long converted = ToLong(value, fallback);
            if (converted < Int32.MinValue || converted > Int32.MaxValue)
                return fallback;
            return (int)converted;
        }

        private static string FriendlyError(Exception exception)
        {
            if (exception is BadImageFormatException || exception is DllNotFoundException)
                return "Windows' built-in SQLite component is unavailable on this computer.";
            if (exception is UnauthorizedAccessException)
                return "Windows denied access to the save. Check the file permissions and try again.";
            if (exception is SqliteException)
                return "SQLite could not read the selected save: " + exception.Message;
            return exception.Message;
        }
    }
}
