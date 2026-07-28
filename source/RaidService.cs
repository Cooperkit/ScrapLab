using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace RaidRescue
{
    internal static class RaidService
    {
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
            AnalysisResult result = NewAnalysis(path);
            try
            {
                ValidatePath(path);
                FileInfo file = new FileInfo(path);
                result.Name = file.Name;
                result.Modified = file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
                result.SizeBytes = file.Length;
                result.Size = FormatBytes(file.Length);
                result.GameRunning = IsGameRunning();

                using (SqliteDatabase database = SqliteDatabase.OpenReadOnly(path))
                {
                    result.DatabaseStatus = database.QuickCheck();
                    if (!String.Equals(result.DatabaseStatus, "ok", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Warnings.Add(
                            "SQLite reported database damage. Raid Rescue will not edit this file.");
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
                            "Raid Rescue did not change it.");
                    }

                    long rowId;
                    byte[] record = database.ReadRaidRecord(out rowId);
                    result.RaidManagerPresent = record != null;
                    result.RaidManagerRowId = rowId;
                    if (record != null)
                    {
                        ScriptPayload payload = LuaStorage.ParseScriptData(record);
                        ValidateRaidPayload(payload);
                        ReadRaids(payload.Value, database, result);
                    }
                }

                if (result.GameRunning)
                {
                    result.Warnings.Add(
                        "Scrap Mechanic is running. Close the game before clearing raids.");
                }
                if (!result.RaidManagerPresent)
                {
                    result.Warnings.Add(
                        "No stored raid-manager record was found. This save has no persisted raids to clear.");
                }

                result.RaidCount = result.Raids.Count;
                result.CanClear =
                    result.RaidManagerPresent &&
                    result.RaidCount > 0 &&
                    String.Equals(result.DatabaseStatus, "ok", StringComparison.OrdinalIgnoreCase) &&
                    !result.GameRunning;
                result.Success = true;
            }
            catch (Exception exception)
            {
                result.Success = false;
                result.CanClear = false;
                result.Error = FriendlyError(exception);
            }
            return result;
        }

        public static RepairResult ClearRaids(string path)
        {
            RepairResult result = new RepairResult { Path = path };
            try
            {
                ValidatePath(path);
                if (IsGameRunning())
                    throw new InvalidOperationException(
                        "Scrap Mechanic is running. Close the game completely and try again.");

                AnalysisResult before = Analyze(path);
                result.Before = before;
                if (!before.Success)
                    throw new InvalidOperationException("The save could not be analyzed: " + before.Error);
                if (!String.Equals(before.DatabaseStatus, "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "The save failed SQLite's integrity check and was not changed.");
                if (!before.RaidManagerPresent || before.RaidCount == 0)
                    throw new InvalidOperationException("This save has no stored raids to clear.");

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

                if (IsGameRunning())
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

                result.After = Analyze(path);
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

        private static void ReadRaids(
            object rootValue, SqliteDatabase database, AnalysisResult analysis)
        {
            LuaTable root = rootValue as LuaTable;
            if (root == null)
                throw new InvalidDataException("The raid-manager root value is not a Lua table.");

            LuaTable worlds = root.Get("worldRaids") as LuaTable;
            if (worlds == null)
                return;

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
                directory, name + ".raidrescue-backup-" + stamp + extension);
            int suffix = 2;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(
                    directory,
                    name + ".raidrescue-backup-" + stamp + "-" +
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
