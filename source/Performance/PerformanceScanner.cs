using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Web.Script.Serialization;

namespace RaidRescue
{
    public static class PerformanceScanner
    {
        private const int CurrentScanVersion = 3;
        private const int MaximumLargestRecords = 25;

        public static PerformanceScanResult Scan(string path)
        {
            return Scan(path, CancellationToken.None);
        }

        public static PerformanceScanResult Scan(
            string path, CancellationToken cancellation)
        {
            return Scan(path, cancellation, null);
        }

        internal static PerformanceScanResult Scan(
            string path,
            CancellationToken cancellation,
            Action<PerformanceScanProgress> progress)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                ReportProgress(
                    progress, 1, "schema",
                    "Checking database layout",
                    0, 1,
                    "Verifying the save and supported SQLite layout.");
                cancellation.ThrowIfCancellationRequested();
                ValidatePath(path);
                if (RaidService.IsGameRunning())
                {
                    return Failure(
                        "Safety lock: Scrap Mechanic is running. " +
                        "Close the game completely before scanning.",
                        stopwatch.ElapsedMilliseconds);
                }

                string fullPath = Path.GetFullPath(path);
                SourceFingerprint before =
                    SourceFingerprint.Capture(fullPath, cancellation);
                PerformanceScanResult result = NewResult();
                result.FileSizeBytes = before.DatabaseSize;

                Dictionary<int, WorldAccumulator> worlds =
                    new Dictionary<int, WorldAccumulator>();
                Dictionary<CellKey, CellAccumulator> cells =
                    new Dictionary<CellKey, CellAccumulator>();
                List<PerformanceLargestRecord> largest =
                    new List<PerformanceLargestRecord>();

                using (SqliteDatabase database =
                    SqliteDatabase.OpenReadOnly(fullPath))
                {
                    cancellation.ThrowIfCancellationRequested();
                    result.DatabaseStatus = database.QuickCheck();
                    if (!String.Equals(
                        result.DatabaseStatus, "ok",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            "SQLite integrity check failed.");
                    }

                    SupportedSchema schema =
                        database.ReadSupportedSchema();
                    result.Schema = ToCoverage(schema);
                    UnsupportedTableSummary unsupported =
                        database.ReadUnsupportedTables();
                    result.UnsupportedTableCount =
                        unsupported.Count;
                    result.UnsupportedTables =
                        unsupported.Names;
                    if (schema.UnitTablePresent &&
                        !schema.CanReadUnitCells)
                    {
                        result.UnsupportedTableCount++;
                        if (result.UnsupportedTables.Count < 32)
                        {
                            result.UnsupportedTables.Add(
                                "Unit (unsupported layout)");
                        }
                    }
                    if (result.UnsupportedTableCount > 0)
                    {
                        result.Warnings.Add(
                            result.UnsupportedTableCount +
                            " table(s) use unrecognized or " +
                            "not-yet-supported schemas and were " +
                            "excluded from coverage and ranking.");
                    }
                    if (!schema.CanReadGame)
                    {
                        throw new InvalidDataException(
                            "The Game table does not contain the " +
                            "required save metadata.");
                    }

                    long saveVersion;
                    long gameTick;
                    database.ReadGameInfo(
                        out saveVersion, out gameTick);
                    if (saveVersion <= 0 || gameTick < 0)
                    {
                        throw new InvalidDataException(
                            "The Game table contains invalid save metadata.");
                    }
                    result.SaveVersion = saveVersion;
                    result.Warnings.Add(
                        "Counts include only the currently allowlisted " +
                        "record families. Other tables remain unknown.");

                    SqliteStorageStatistics storage =
                        database.ReadStorageStatistics();
                    result.DatabasePageBytes = storage.PageSizeBytes;
                    result.DatabaseAllocatedBytes = SafeMultiply(
                        storage.PageSizeBytes, storage.PageCount);
                    result.DatabaseFreeBytes = SafeMultiply(
                        storage.PageSizeBytes, storage.FreePageCount);

                    ReportProgress(
                        progress, 1, "schema",
                        "Checking database layout",
                        1, 1,
                        "The supported database layout is ready.");

                    PerformanceCategoryMetric harvestables =
                        NewCategory(
                            "harvestable", "Harvestables");
                    PerformanceCategoryMetric units =
                        NewCategory("unit", "Persistent units");
                    PerformanceCategoryMetric scripts = null;
                    PerformanceCategoryMetric metadata = null;
                    int countUnits =
                        2 +
                        (schema.CanReadScriptTotals ? 1 : 0) +
                        (schema.CanReadWorldMetadata ? 1 : 0);
                    int countedUnits = 0;
                    long harvestableRows = 0;
                    long unitRows = 0;
                    ReportProgress(
                        progress, 2, "counting",
                        "Counting stored records",
                        countedUnits, countUnits,
                        "Counting allowlisted rows and payload bytes.");
                    if (schema.CanReadHarvestableCells)
                    {
                        harvestableRows =
                            database.CountHarvestableRows(cancellation);
                    }
                    countedUnits++;
                    ReportProgress(
                        progress, 2, "counting",
                        "Counting stored records",
                        countedUnits, countUnits,
                        "Harvestable rows counted.");
                    if (schema.CanReadUnitCells)
                    {
                        unitRows =
                            database.CountUnitRows(cancellation);
                    }
                    countedUnits++;
                    ReportProgress(
                        progress, 2, "counting",
                        "Counting stored records",
                        countedUnits, countUnits,
                        schema.CanReadUnitCells
                            ? "Persistent Unit rows counted."
                            : "The Unit layout is unsupported.");

                    if (schema.CanReadScriptTotals)
                    {
                        scripts = AddWorldPayloadCategory(
                            database,
                            SupportedTableKind.ScriptData,
                            "script-data",
                            "Script records",
                            worlds,
                            cancellation);
                        if (scripts.RecordCount > 0)
                        {
                            result.Warnings.Add(
                                "Script records are counted as raw rows " +
                                "and bytes; their payload types are not " +
                                "decoded in Phase 1.");
                        }
                        countedUnits++;
                        ReportProgress(
                            progress, 2, "counting",
                            "Counting stored records",
                            countedUnits, countUnits,
                            "Script record totals counted.");
                    }
                    else
                    {
                        result.Warnings.Add(
                            "ScriptData totals are unavailable because " +
                            "the table layout is unsupported.");
                    }

                    if (schema.CanReadWorldMetadata)
                    {
                        metadata = AddWorldPayloadCategory(
                            database,
                            SupportedTableKind.GenericData,
                            "generic-data",
                            "World metadata",
                            worlds,
                            cancellation);
                        countedUnits++;
                        ReportProgress(
                            progress, 2, "counting",
                            "Counting stored records",
                            countedUnits, countUnits,
                            "World metadata totals counted.");
                    }

                    ReportProgress(
                        progress, 3, "grouping",
                        "Grouping records by world and cell",
                        0, Math.Max(
                            1, Sum(harvestableRows, unitRows)),
                        "Streaming proven cell records into bounded totals.");
                    long processedCellRows = 0;
                    long totalCellRows =
                        Sum(harvestableRows, unitRows);
                    long progressInterval = Math.Max(
                        256, totalCellRows / 200);
                    if (schema.CanReadHarvestableCells)
                    {
                        database.StreamHarvestableCells(
                            delegate(
                                int worldId, int cellX, int cellY,
                                long payloadBytes)
                            {
                                cancellation.ThrowIfCancellationRequested();
                                if (payloadBytes < 0)
                                {
                                    throw new InvalidDataException(
                                        "A Harvestable payload length was negative.");
                                }

                                WorldAccumulator world =
                                    GetWorld(worlds, worldId);
                                Add(ref world.RecordCount, 1);
                                Add(
                                    ref world.PayloadBytes,
                                    payloadBytes);

                                CellKey key =
                                    new CellKey(worldId, cellX, cellY);
                                CellAccumulator cell;
                                if (!cells.TryGetValue(key, out cell))
                                {
                                    cell = new CellAccumulator
                                    {
                                        WorldId = worldId,
                                        CellX = cellX,
                                        CellY = cellY
                                    };
                                    cells.Add(key, cell);
                                    Add(ref world.PopulatedCells, 1);
                                }
                                Add(ref cell.RecordCount, 1);
                                Add(
                                    ref cell.PayloadBytes,
                                    payloadBytes);
                                AddCellCategory(
                                    cell,
                                    "harvestable",
                                    "Harvestables",
                                    payloadBytes,
                                    true);

                                harvestables.RecordCount = Sum(
                                    harvestables.RecordCount, 1);
                                harvestables.PayloadBytes = Sum(
                                    harvestables.PayloadBytes,
                                    payloadBytes);
                                harvestables.DecodedCount = Sum(
                                    harvestables.DecodedCount, 1);
                                AddLargest(
                                    largest,
                                    new PerformanceLargestRecord
                                    {
                                        CategoryKey = "harvestable",
                                        WorldId = worldId,
                                        CellX = cellX,
                                        CellY = cellY,
                                        PayloadBytes = payloadBytes
                                    });
                                processedCellRows++;
                                if (processedCellRows ==
                                        totalCellRows ||
                                    processedCellRows %
                                        progressInterval == 0)
                                {
                                    ReportProgress(
                                        progress, 3, "grouping",
                                        "Grouping records by world and cell",
                                        processedCellRows,
                                        Math.Max(1, totalCellRows),
                                        "Aggregating cell records without " +
                                        "retaining raw rows.");
                                }
                            },
                            cancellation);
                        result.Categories.Add(harvestables);
                    }
                    else
                    {
                        result.Warnings.Add(
                            "Harvestable cells are unavailable because " +
                            "the table layout is unsupported.");
                    }
                    if (schema.CanReadUnitCells)
                    {
                        database.StreamUnitCells(
                            delegate(
                                int worldId, int cellX, int cellY,
                                long payloadBytes, byte[] data)
                            {
                                cancellation.ThrowIfCancellationRequested();
                                if (payloadBytes < 0)
                                {
                                    throw new InvalidDataException(
                                        "A Unit payload length was negative.");
                                }
                                bool decoded = UnitPositionMatchesCell(
                                    data, cellX, cellY);
                                WorldAccumulator world =
                                    GetWorld(worlds, worldId);
                                Add(ref world.RecordCount, 1);
                                Add(
                                    ref world.PayloadBytes,
                                    payloadBytes);

                                CellKey key =
                                    new CellKey(worldId, cellX, cellY);
                                CellAccumulator cell;
                                if (!cells.TryGetValue(key, out cell))
                                {
                                    cell = new CellAccumulator
                                    {
                                        WorldId = worldId,
                                        CellX = cellX,
                                        CellY = cellY
                                    };
                                    cells.Add(key, cell);
                                    Add(ref world.PopulatedCells, 1);
                                }
                                Add(ref cell.RecordCount, 1);
                                Add(
                                    ref cell.PayloadBytes,
                                    payloadBytes);
                                AddCellCategory(
                                    cell,
                                    "unit",
                                    "Persistent units",
                                    payloadBytes,
                                    decoded);

                                units.RecordCount = Sum(
                                    units.RecordCount, 1);
                                units.PayloadBytes = Sum(
                                    units.PayloadBytes,
                                    payloadBytes);
                                if (decoded)
                                {
                                    units.DecodedCount = Sum(
                                        units.DecodedCount, 1);
                                }
                                else
                                {
                                    units.UnreadableCount = Sum(
                                        units.UnreadableCount, 1);
                                }
                                AddLargest(
                                    largest,
                                    new PerformanceLargestRecord
                                    {
                                        CategoryKey = "unit",
                                        WorldId = worldId,
                                        CellX = cellX,
                                        CellY = cellY,
                                        PayloadBytes = payloadBytes
                                    });
                                processedCellRows++;
                                if (processedCellRows ==
                                        totalCellRows ||
                                    processedCellRows %
                                        progressInterval == 0)
                                {
                                    ReportProgress(
                                        progress, 3, "grouping",
                                        "Grouping records by world and cell",
                                        processedCellRows,
                                        Math.Max(1, totalCellRows),
                                        "Aggregating Unit cells without " +
                                        "retaining raw rows.");
                                }
                            },
                            cancellation);
                        result.Categories.Add(units);
                        if (units.UnreadableCount > 0)
                        {
                            result.Warnings.Add(
                                units.UnreadableCount +
                                " Unit row(s) retained their proven table " +
                                "cell but had an unreadable payload position.");
                        }
                    }
                    else
                    {
                        result.Warnings.Add(
                            "Unit cells are unavailable because the " +
                            "table layout is unsupported or absent.");
                    }
                    ReportProgress(
                        progress, 3, "grouping",
                        "Grouping records by world and cell",
                        Math.Max(1, processedCellRows),
                        Math.Max(1, totalCellRows),
                        "World and cell aggregation is complete.");
                    if (scripts != null)
                        result.Categories.Add(scripts);
                    if (metadata != null)
                        result.Categories.Add(metadata);

                    ReportProgress(
                        progress, 4, "decoding",
                        "Decoding supported record types",
                        0, 1,
                        "Resolving validated world descriptors.");
                    Dictionary<int, string> worldNames =
                        schema.CanReadWorldMetadata
                            ? WorldStorage.ReadWorldNames(database)
                            : new Dictionary<int, string>();
                    if (!schema.CanReadWorldMetadata)
                    {
                        result.Warnings.Add(
                            "World metadata uses an unsupported layout; " +
                            "numeric world names are shown.");
                    }
                    ReportProgress(
                        progress, 4, "decoding",
                        "Decoding supported record types",
                        1, 1,
                        "Supported world descriptors are decoded.");

                    ReportProgress(
                        progress, 5, "ranking",
                        "Ranking potential hotspots",
                        0, 1,
                        "Comparing 3-by-3 neighborhoods inside each world.");
                    BuildResult(
                        result, worlds, cells, largest, worldNames);
                    PerformanceHotspotRanker.Rank(
                        result, cancellation);
                    ReportProgress(
                        progress, 5, "ranking",
                        "Ranking potential hotspots",
                        1, 1,
                        "Overlapping clusters are collapsed and ranked.");
                }

                ReportProgress(
                    progress, 6, "report",
                    "Building the report",
                    0, 2,
                    "Rechecking the game and source fingerprint.");
                cancellation.ThrowIfCancellationRequested();
                if (RaidService.IsGameRunning())
                {
                    return Failure(
                        "Scrap Mechanic started while the scan was running. " +
                        "The scan result was discarded.",
                        stopwatch.ElapsedMilliseconds);
                }

                SourceFingerprint after =
                    SourceFingerprint.Capture(fullPath, cancellation);
                ReportProgress(
                    progress, 6, "report",
                    "Building the report",
                    1, 2,
                    "Comparing the final source fingerprint.");
                if (!before.Equals(after))
                {
                    return Failure(
                        "The save changed while it was being scanned. " +
                        "Close Scrap Mechanic and scan again.",
                        stopwatch.ElapsedMilliseconds);
                }

                result.SourceUnchanged = true;
                result.Success = true;
                result.DurationMilliseconds =
                    stopwatch.ElapsedMilliseconds;
                ReportProgress(
                    progress, 6, "report",
                    "Building the report",
                    2, 2,
                    "The read-only performance report is complete.");
                return result;
            }
            catch (OperationCanceledException)
            {
                PerformanceScanResult cancelled = NewResult();
                cancelled.Cancelled = true;
                cancelled.Error = "The performance scan was cancelled.";
                cancelled.DurationMilliseconds =
                    stopwatch.ElapsedMilliseconds;
                return cancelled;
            }
            catch (Exception exception)
            {
                return Failure(
                    FriendlyError(exception),
                    stopwatch.ElapsedMilliseconds);
            }
        }

        public static string SerializeDeterministic(
            PerformanceScanResult result)
        {
            if (result == null)
                throw new ArgumentNullException("result");
            PerformanceScanResult snapshot =
                new PerformanceScanResult
            {
                Success = result.Success,
                Cancelled = result.Cancelled,
                Error = result.Error,
                ScanVersion = result.ScanVersion,
                // Wall-clock duration is diagnostic data, not scan content.
                // Normalizing it makes fixture output byte-for-byte stable.
                DurationMilliseconds = 0,
                SaveVersion = result.SaveVersion,
                DatabaseStatus = result.DatabaseStatus,
                FileSizeBytes = result.FileSizeBytes,
                DatabasePageBytes = result.DatabasePageBytes,
                DatabaseAllocatedBytes =
                    result.DatabaseAllocatedBytes,
                DatabaseFreeBytes = result.DatabaseFreeBytes,
                WorldsScanned = result.WorldsScanned,
                PopulatedCells = result.PopulatedCells,
                TotalRecords = result.TotalRecords,
                TotalPayloadBytes = result.TotalPayloadBytes,
                Coverage = result.Coverage,
                DecodedSupportedRecords =
                    result.DecodedSupportedRecords,
                RecordsConsidered = result.RecordsConsidered,
                SourceUnchanged = result.SourceUnchanged,
                UnsupportedTableCount =
                    result.UnsupportedTableCount,
                Schema = result.Schema,
                UnsupportedTables = result.UnsupportedTables,
                Worlds = result.Worlds,
                Cells = result.Cells,
                Categories = result.Categories,
                LargestRecords = result.LargestRecords,
                Hotspots = result.Hotspots,
                Warnings = result.Warnings
            };
            return new JavaScriptSerializer
            {
                MaxJsonLength = Int32.MaxValue
            }.Serialize(snapshot);
        }

        private static void ReportProgress(
            Action<PerformanceScanProgress> callback,
            int stage,
            string stageKey,
            string stageLabel,
            long completedUnits,
            long totalUnits,
            string message)
        {
            if (callback == null)
                return;
            long safeTotal = Math.Max(1, totalUnits);
            long safeCompleted = Math.Max(
                0, Math.Min(completedUnits, safeTotal));
            double stageRatio =
                (double)safeCompleted / safeTotal;
            int overall = (int)Math.Round(
                (((stage - 1) + stageRatio) / 6.0) * 100.0);
            PerformanceScanProgress value =
                new PerformanceScanProgress
                {
                    Stage = stage,
                    StageCount = 6,
                    StageKey = stageKey,
                    StageLabel = stageLabel,
                    CompletedUnits = safeCompleted,
                    TotalUnits = safeTotal,
                    OverallPercent = Math.Max(
                        0, Math.Min(100, overall)),
                    Message = message
                };
            try
            {
                callback(value);
            }
            catch
            {
                // A status observer must never invalidate a read-only scan.
            }
        }

        private static PerformanceCategoryMetric AddWorldPayloadCategory(
            SqliteDatabase database,
            SupportedTableKind table,
            string key,
            string displayName,
            IDictionary<int, WorldAccumulator> worlds,
            CancellationToken cancellation)
        {
            PerformanceCategoryMetric category =
                NewCategory(key, displayName);
            foreach (WorldPayloadTotal total in
                database.CountSupportedRows(table, cancellation))
            {
                cancellation.ThrowIfCancellationRequested();
                if (total.RecordCount < 0 || total.PayloadBytes < 0)
                {
                    throw new InvalidDataException(
                        "A supported table returned a negative total.");
                }
                WorldAccumulator world =
                    GetWorld(worlds, total.WorldId);
                Add(ref world.RecordCount, total.RecordCount);
                Add(ref world.PayloadBytes, total.PayloadBytes);
                category.RecordCount = Sum(
                    category.RecordCount, total.RecordCount);
                category.PayloadBytes = Sum(
                    category.PayloadBytes, total.PayloadBytes);
            }
            return category;
        }

        private static void BuildResult(
            PerformanceScanResult result,
            IDictionary<int, WorldAccumulator> worldTotals,
            IDictionary<CellKey, CellAccumulator> cellTotals,
            IList<PerformanceLargestRecord> largest,
            IDictionary<int, string> worldNames)
        {
            List<int> worldIds = new List<int>(worldTotals.Keys);
            worldIds.Sort();
            foreach (int worldId in worldIds)
            {
                WorldAccumulator total = worldTotals[worldId];
                string name =
                    WorldStorage.ResolveName(worldNames, worldId);
                result.Worlds.Add(new PerformanceWorldSummary
                {
                    WorldId = worldId,
                    WorldName = name,
                    PopulatedCells = total.PopulatedCells,
                    TotalRecords = total.RecordCount,
                    TotalPayloadBytes = total.PayloadBytes,
                    HotspotCount = 0
                });
                result.TotalRecords = Sum(
                    result.TotalRecords, total.RecordCount);
                result.TotalPayloadBytes = Sum(
                    result.TotalPayloadBytes, total.PayloadBytes);
            }

            List<CellAccumulator> orderedCells =
                new List<CellAccumulator>(cellTotals.Values);
            orderedCells.Sort(CompareCells);
            foreach (CellAccumulator cell in orderedCells)
            {
                result.Cells.Add(new PerformanceCellSummary
                {
                    WorldId = cell.WorldId,
                    WorldName = WorldStorage.ResolveName(
                        worldNames, cell.WorldId),
                    CellX = cell.CellX,
                    CellY = cell.CellY,
                    ApproximateCenterX =
                        (cell.CellY * 64.0) + 32.0,
                    ApproximateCenterY =
                        (cell.CellX * 64.0) + 32.0,
                    TotalRecords = cell.RecordCount,
                    TotalPayloadBytes = cell.PayloadBytes,
                    Categories = CopyCategories(
                        cell.Categories.Values)
                });
            }

            List<PerformanceLargestRecord> orderedLargest =
                new List<PerformanceLargestRecord>(largest);
            orderedLargest.Sort(CompareLargest);
            foreach (PerformanceLargestRecord record in orderedLargest)
            {
                record.WorldName = WorldStorage.ResolveName(
                    worldNames, record.WorldId);
                result.LargestRecords.Add(record);
            }

            result.WorldsScanned = result.Worlds.Count;
            result.PopulatedCells = result.Cells.Count;
            foreach (PerformanceCategoryMetric category in
                result.Categories)
            {
                result.DecodedSupportedRecords = Sum(
                    result.DecodedSupportedRecords,
                    category.DecodedCount);
                result.RecordsConsidered = Sum(
                    result.RecordsConsidered,
                    category.RecordCount);
            }
            result.Coverage =
                result.RecordsConsidered == 0
                    ? 1.0
                    : (double)result.DecodedSupportedRecords /
                        result.RecordsConsidered;
        }

        private static PerformanceSchemaCoverage ToCoverage(
            SupportedSchema schema)
        {
            return new PerformanceSchemaCoverage
            {
                CanReadGame = schema.CanReadGame,
                CanReadHarvestableCells =
                    schema.CanReadHarvestableCells,
                CanReadUnitCells =
                    schema.CanReadUnitCells,
                UnitTablePresent =
                    schema.UnitTablePresent,
                CanReadWorldMetadata =
                    schema.CanReadWorldMetadata,
                CanReadScriptTotals =
                    schema.CanReadScriptTotals,
                GenericDataLayout =
                    String.IsNullOrEmpty(schema.GenericDataLayout)
                        ? "unsupported"
                        : schema.GenericDataLayout,
                ScriptDataLayout =
                    String.IsNullOrEmpty(schema.ScriptDataLayout)
                        ? "unsupported"
                        : schema.ScriptDataLayout
            };
        }

        private static PerformanceCategoryMetric NewCategory(
            string key, string displayName)
        {
            return new PerformanceCategoryMetric
            {
                Key = key,
                DisplayName = displayName
            };
        }

        private static void AddCellCategory(
            CellAccumulator cell,
            string key,
            string displayName,
            long payloadBytes,
            bool decoded)
        {
            PerformanceCategoryMetric category;
            if (!cell.Categories.TryGetValue(key, out category))
            {
                category = NewCategory(key, displayName);
                cell.Categories.Add(key, category);
            }
            category.RecordCount = Sum(category.RecordCount, 1);
            category.PayloadBytes = Sum(
                category.PayloadBytes, payloadBytes);
            if (decoded)
            {
                category.DecodedCount = Sum(
                    category.DecodedCount, 1);
            }
            else
            {
                category.UnreadableCount = Sum(
                    category.UnreadableCount, 1);
            }
        }

        private static List<PerformanceCategoryMetric> CopyCategories(
            IEnumerable<PerformanceCategoryMetric> categories)
        {
            List<PerformanceCategoryMetric> result =
                new List<PerformanceCategoryMetric>();
            foreach (PerformanceCategoryMetric category in categories)
            {
                result.Add(new PerformanceCategoryMetric
                {
                    Key = category.Key,
                    DisplayName = category.DisplayName,
                    RecordCount = category.RecordCount,
                    PayloadBytes = category.PayloadBytes,
                    DecodedCount = category.DecodedCount,
                    UnreadableCount = category.UnreadableCount
                });
            }
            result.Sort(delegate(
                PerformanceCategoryMetric left,
                PerformanceCategoryMetric right)
            {
                return String.Compare(
                    left.Key, right.Key,
                    StringComparison.Ordinal);
            });
            return result;
        }

        private static bool UnitPositionMatchesCell(
            byte[] data, int cellX, int cellY)
        {
            if (data == null || data.Length < 48)
                return false;
            float worldX = ReadBigEndianSingle(data, 40);
            float worldY = ReadBigEndianSingle(data, 44);
            if (Single.IsNaN(worldX) ||
                Single.IsInfinity(worldX) ||
                Single.IsNaN(worldY) ||
                Single.IsInfinity(worldY))
            {
                return false;
            }
            return cellX == (int)Math.Floor(worldY / 64.0) &&
                cellY == (int)Math.Floor(worldX / 64.0);
        }

        private static float ReadBigEndianSingle(
            byte[] data, int offset)
        {
            byte[] bytes = new byte[4];
            Buffer.BlockCopy(data, offset, bytes, 0, 4);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return BitConverter.ToSingle(bytes, 0);
        }

        private static WorldAccumulator GetWorld(
            IDictionary<int, WorldAccumulator> worlds, int worldId)
        {
            WorldAccumulator world;
            if (!worlds.TryGetValue(worldId, out world))
            {
                world = new WorldAccumulator { WorldId = worldId };
                worlds.Add(worldId, world);
            }
            return world;
        }

        private static void AddLargest(
            IList<PerformanceLargestRecord> records,
            PerformanceLargestRecord candidate)
        {
            records.Add(candidate);
            if (records.Count <= MaximumLargestRecords)
                return;

            int leastIndex = 0;
            for (int index = 1; index < records.Count; index++)
            {
                if (CompareLargest(
                    records[index], records[leastIndex]) > 0)
                {
                    leastIndex = index;
                }
            }
            records.RemoveAt(leastIndex);
        }

        private static int CompareLargest(
            PerformanceLargestRecord left,
            PerformanceLargestRecord right)
        {
            int comparison =
                right.PayloadBytes.CompareTo(left.PayloadBytes);
            if (comparison != 0)
                return comparison;
            comparison = left.WorldId.CompareTo(right.WorldId);
            if (comparison != 0)
                return comparison;
            comparison = left.CellX.CompareTo(right.CellX);
            if (comparison != 0)
                return comparison;
            comparison = left.CellY.CompareTo(right.CellY);
            if (comparison != 0)
                return comparison;
            return String.Compare(
                left.CategoryKey, right.CategoryKey,
                StringComparison.Ordinal);
        }

        private static int CompareCells(
            CellAccumulator left, CellAccumulator right)
        {
            int comparison = left.WorldId.CompareTo(right.WorldId);
            if (comparison != 0)
                return comparison;
            comparison = left.CellX.CompareTo(right.CellX);
            if (comparison != 0)
                return comparison;
            return left.CellY.CompareTo(right.CellY);
        }

        private static void Add(ref long target, long value)
        {
            target = Sum(target, value);
        }

        private static long Sum(long left, long right)
        {
            return checked(left + right);
        }

        private static long SafeMultiply(long left, long right)
        {
            return checked(left * right);
        }

        private static PerformanceScanResult NewResult()
        {
            return new PerformanceScanResult
            {
                ScanVersion = CurrentScanVersion,
                Error = String.Empty,
                DatabaseStatus = String.Empty,
                Schema = new PerformanceSchemaCoverage
                {
                    GenericDataLayout = "unsupported",
                    ScriptDataLayout = "unsupported"
                },
                UnsupportedTables = new List<string>(),
                Worlds = new List<PerformanceWorldSummary>(),
                Cells = new List<PerformanceCellSummary>(),
                Categories = new List<PerformanceCategoryMetric>(),
                LargestRecords =
                    new List<PerformanceLargestRecord>(),
                Hotspots = new List<PerformanceCellHotspot>(),
                Warnings = new List<string>()
            };
        }

        private static PerformanceScanResult Failure(
            string error, long durationMilliseconds)
        {
            PerformanceScanResult result = NewResult();
            result.Error = error ?? "The performance scan failed.";
            result.DurationMilliseconds = durationMilliseconds;
            return result;
        }

        private static void ValidatePath(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentException(
                    "Choose a Scrap Mechanic Survival save.");
            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException(
                    "The selected save no longer exists.");
            if (!String.Equals(
                Path.GetExtension(fullPath), ".db",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "The selected file is not a .db Survival save.");
            }
        }

        private static string FriendlyError(Exception exception)
        {
            if (exception is BadImageFormatException ||
                exception is DllNotFoundException)
            {
                return "Windows' built-in SQLite component is " +
                    "unavailable on this computer.";
            }
            if (exception is UnauthorizedAccessException)
            {
                return "Windows denied access to the save. Check the " +
                    "file permissions and try again.";
            }
            if (exception is SqliteException)
            {
                return "SQLite could not read the selected save: " +
                    exception.Message;
            }
            return exception.Message;
        }

        private sealed class WorldAccumulator
        {
            public int WorldId;
            public long PopulatedCells;
            public long RecordCount;
            public long PayloadBytes;
        }

        private sealed class CellAccumulator
        {
            public int WorldId;
            public int CellX;
            public int CellY;
            public long RecordCount;
            public long PayloadBytes;
            public readonly Dictionary<
                string, PerformanceCategoryMetric> Categories =
                new Dictionary<
                    string, PerformanceCategoryMetric>(
                        StringComparer.Ordinal);
        }

        private sealed class CellKey : IEquatable<CellKey>
        {
            private readonly int worldId;
            private readonly int cellX;
            private readonly int cellY;

            public CellKey(int world, int x, int y)
            {
                worldId = world;
                cellX = x;
                cellY = y;
            }

            public bool Equals(CellKey other)
            {
                return other != null &&
                    worldId == other.worldId &&
                    cellX == other.cellX &&
                    cellY == other.cellY;
            }

            public override bool Equals(object value)
            {
                return Equals(value as CellKey);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = (hash * 31) + worldId;
                    hash = (hash * 31) + cellX;
                    hash = (hash * 31) + cellY;
                    return hash;
                }
            }
        }

        private sealed class SourceFingerprint :
            IEquatable<SourceFingerprint>
        {
            private readonly FingerprintPart database;
            private readonly FingerprintPart wal;
            private readonly FingerprintPart shm;

            private SourceFingerprint(
                FingerprintPart main,
                FingerprintPart writeAheadLog,
                FingerprintPart sharedMemory)
            {
                database = main;
                wal = writeAheadLog;
                shm = sharedMemory;
            }

            public long DatabaseSize
            {
                get { return database.Size; }
            }

            public static SourceFingerprint Capture(
                string path, CancellationToken cancellation)
            {
                return new SourceFingerprint(
                    FingerprintPart.Capture(path, cancellation),
                    FingerprintPart.Capture(
                        path + "-wal", cancellation),
                    FingerprintPart.Capture(
                        path + "-shm", cancellation));
            }

            public bool Equals(SourceFingerprint other)
            {
                return other != null &&
                    database.Equals(other.database) &&
                    wal.Equals(other.wal) &&
                    shm.Equals(other.shm);
            }

            public override bool Equals(object value)
            {
                return Equals(value as SourceFingerprint);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = database.GetHashCode();
                    hash = (hash * 31) + wal.GetHashCode();
                    hash = (hash * 31) + shm.GetHashCode();
                    return hash;
                }
            }
        }

        private sealed class FingerprintPart :
            IEquatable<FingerprintPart>
        {
            public bool Exists;
            public long Size;
            public string Digest;

            public static FingerprintPart Capture(
                string path, CancellationToken cancellation)
            {
                if (!File.Exists(path))
                {
                    return new FingerprintPart
                    {
                        Digest = String.Empty
                    };
                }

                using (FileStream stream = new FileStream(
                    path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    1024 * 1024, FileOptions.SequentialScan))
                using (SHA256 algorithm = SHA256.Create())
                {
                    byte[] buffer = new byte[1024 * 1024];
                    int read;
                    while ((read = stream.Read(
                        buffer, 0, buffer.Length)) > 0)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        algorithm.TransformBlock(
                            buffer, 0, read, buffer, 0);
                    }
                    algorithm.TransformFinalBlock(
                        new byte[0], 0, 0);
                    return new FingerprintPart
                    {
                        Exists = true,
                        Size = stream.Length,
                        Digest = Convert.ToBase64String(
                            algorithm.Hash)
                    };
                }
            }

            public bool Equals(FingerprintPart other)
            {
                return other != null &&
                    Exists == other.Exists &&
                    Size == other.Size &&
                    String.Equals(
                        Digest, other.Digest,
                        StringComparison.Ordinal);
            }

            public override bool Equals(object value)
            {
                return Equals(value as FingerprintPart);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Exists ? 1 : 0;
                    hash = (hash * 31) + Size.GetHashCode();
                    hash = (hash * 31) +
                        (Digest ?? String.Empty).GetHashCode();
                    return hash;
                }
            }
        }
    }
}
