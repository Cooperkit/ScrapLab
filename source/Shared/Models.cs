using System;
using System.Collections.Generic;

namespace RaidRescue
{
    public sealed class SaveFileInfo
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public string Modified { get; set; }
        public long SizeBytes { get; set; }
        public string Size { get; set; }
        public string UserFolder { get; set; }
    }

    public sealed class AppState
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public bool GameRunning { get; set; }
        public List<SaveFileInfo> Saves { get; set; }
    }

    public sealed class EnemyInfo
    {
        public string Name { get; set; }
        public string Uuid { get; set; }
        public long Quantity { get; set; }
    }

    public sealed class PositionInfo
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    public sealed class RaidInfo
    {
        public int Number { get; set; }
        public int WorldSlot { get; set; }
        public string WorldName { get; set; }
        public string Key { get; set; }
        public long Tier { get; set; }
        public long ThreatValue { get; set; }
        public long MaximumThreatValue { get; set; }
        public string State { get; set; }
        public long PlannedEnemyCount { get; set; }
        public int SpawnGroups { get; set; }
        public List<EnemyInfo> Enemies { get; set; }
        public List<EnemyInfo> Crops { get; set; }
        public PositionInfo Center { get; set; }
        public long TickCounter { get; set; }
        public long LastSpawnTick { get; set; }
        public long TimeoutTick { get; set; }
        public long SavedTick { get; set; }
        public int TrackedCrops { get; set; }
        public int StaleCropReferences { get; set; }
        public int PlantingRecords { get; set; }
        public int LiveRaiderReferences { get; set; }
        public bool NeedsSpawnPoints { get; set; }
        public bool LooksStuck { get; set; }
        public List<string> Notes { get; set; }
    }

    public sealed class DroppedItemInfo
    {
        public long EntityId { get; set; }
        public int WorldId { get; set; }
        public string WorldName { get; set; }
        public int CellX { get; set; }
        public int CellY { get; set; }
        public string Uuid { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public long Quantity { get; set; }
        public int ValueScore { get; set; }
        public string ValueTier { get; set; }
        public string DropType { get; set; }
        public PositionInfo Position { get; set; }
        public long KillTick { get; set; }
        public long RemainingTicks { get; set; }
        public long RemainingSeconds { get; set; }
        public bool Expired { get; set; }
        public bool Epic { get; set; }
        public bool QuestItem { get; set; }
        public string LimitedLoot { get; set; }

        internal long ScriptRowId;
        internal byte[] ScriptKey;
    }

    public sealed class AnalysisResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public string Path { get; set; }
        public string Name { get; set; }
        public string Modified { get; set; }
        public long SizeBytes { get; set; }
        public string Size { get; set; }
        public string DatabaseStatus { get; set; }
        public long SaveVersion { get; set; }
        public long GameTick { get; set; }
        public bool RaidManagerPresent { get; set; }
        public long RaidManagerRowId { get; set; }
        public int RaidCount { get; set; }
        public List<RaidInfo> Raids { get; set; }
        public int DroppedItemCount { get; set; }
        public long DroppedItemQuantity { get; set; }
        public int ExpiredDroppedItemCount { get; set; }
        public int UnreadableDroppedItemCount { get; set; }
        public bool DroppedItemsScanned { get; set; }
        public List<DroppedItemInfo> DroppedItems { get; set; }
        public Dictionary<string, string> DroppedItemIcons { get; set; }
        public List<string> Warnings { get; set; }
        public bool GameRunning { get; set; }
        public bool CanClear { get; set; }
        public int OrphanedRaidCropCount { get; set; }
        public int UnreadableRaidCropCount { get; set; }
        public int UnreleasableRaidCropCount { get; set; }
        public bool CanRepairOrphanedCrops { get; set; }
        public bool CanClearDroppedItems { get; set; }
        public bool CanClearExpiredDroppedItems { get; set; }
    }

    public sealed class RepairResult
    {
        public bool Success { get; set; }
        public bool Cancelled { get; set; }
        public string Error { get; set; }
        public string Path { get; set; }
        public string BackupPath { get; set; }
        public int RecordsRemoved { get; set; }
        public int CropsReleased { get; set; }
        public int CropsAlreadySafe { get; set; }
        public int MissingCropReferences { get; set; }
        public string DatabaseStatus { get; set; }
        public AnalysisResult Before { get; set; }
        public AnalysisResult After { get; set; }
    }

    public sealed class DroppedItemRepairResult
    {
        public bool Success { get; set; }
        public bool Cancelled { get; set; }
        public string Error { get; set; }
        public string Path { get; set; }
        public string BackupPath { get; set; }
        public long TargetEntityId { get; set; }
        public int ItemsRemoved { get; set; }
        public long QuantityRemoved { get; set; }
        public string DatabaseStatus { get; set; }
        public AnalysisResult Before { get; set; }
        public AnalysisResult After { get; set; }
    }

    public sealed class GamePatchResult
    {
        public bool Success { get; set; }
        public bool Cancelled { get; set; }
        public bool AlreadyPatched { get; set; }
        public bool Installed { get; set; }
        public bool NeedsUpdate { get; set; }
        public string Mode { get; set; }
        public string CompatibilityState { get; set; }
        public string SteamBuildId { get; set; }
        public bool Adaptive { get; set; }
        public bool CanApply { get; set; }
        public string CompatibilityReason { get; set; }
        public string Error { get; set; }
        public string GamePath { get; set; }
        public string GameVersion { get; set; }
        public string BackupPath { get; set; }
        public int FilesPatched { get; set; }
        public List<string> Changes { get; set; }
        public List<GamePatchBatchItem> BatchItems { get; set; }

        internal Dictionary<string, bool> ActivationChanges;
    }

    public sealed class GamePatchBatchItem
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public string Outcome { get; set; }
        public string Reason { get; set; }
    }

    public sealed class PerformanceScanResult
    {
        public bool Success { get; set; }
        public bool Cancelled { get; set; }
        public string Error { get; set; }
        public int ScanVersion { get; set; }
        public long DurationMilliseconds { get; set; }
        public long SaveVersion { get; set; }
        public string DatabaseStatus { get; set; }
        public long FileSizeBytes { get; set; }
        public long DatabasePageBytes { get; set; }
        public long DatabaseAllocatedBytes { get; set; }
        public long DatabaseFreeBytes { get; set; }
        public long WorldsScanned { get; set; }
        public long PopulatedCells { get; set; }
        public long TotalRecords { get; set; }
        public long TotalPayloadBytes { get; set; }
        public double Coverage { get; set; }
        public long DecodedSupportedRecords { get; set; }
        public long RecordsConsidered { get; set; }
        public bool SourceUnchanged { get; set; }
        public int UnsupportedTableCount { get; set; }
        public PerformanceSchemaCoverage Schema { get; set; }
        public List<string> UnsupportedTables { get; set; }
        public List<PerformanceWorldSummary> Worlds { get; set; }
        public List<PerformanceCellSummary> Cells { get; set; }
        public List<PerformanceCategoryMetric> Categories { get; set; }
        public List<PerformanceLargestRecord> LargestRecords { get; set; }
        public List<PerformanceCellHotspot> Hotspots { get; set; }
        public List<string> Warnings { get; set; }
    }

    public sealed class PerformanceSchemaCoverage
    {
        public bool CanReadGame { get; set; }
        public bool CanReadHarvestableCells { get; set; }
        public bool CanReadUnitCells { get; set; }
        public bool UnitTablePresent { get; set; }
        public bool CanReadWorldMetadata { get; set; }
        public bool CanReadScriptTotals { get; set; }
        public string GenericDataLayout { get; set; }
        public string ScriptDataLayout { get; set; }
    }

    public sealed class PerformanceWorldSummary
    {
        public int WorldId { get; set; }
        public string WorldName { get; set; }
        public long PopulatedCells { get; set; }
        public long TotalRecords { get; set; }
        public long TotalPayloadBytes { get; set; }
        public long HotspotCount { get; set; }
    }

    public sealed class PerformanceCellSummary
    {
        public int WorldId { get; set; }
        public string WorldName { get; set; }
        public int CellX { get; set; }
        public int CellY { get; set; }
        public double ApproximateCenterX { get; set; }
        public double ApproximateCenterY { get; set; }
        public long TotalRecords { get; set; }
        public long TotalPayloadBytes { get; set; }
        public List<PerformanceCategoryMetric> Categories { get; set; }
    }

    public sealed class PerformanceCategoryMetric
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public long RecordCount { get; set; }
        public long PayloadBytes { get; set; }
        public long DecodedCount { get; set; }
        public long UnreadableCount { get; set; }
    }

    public sealed class PerformanceLargestRecord
    {
        public string CategoryKey { get; set; }
        public int WorldId { get; set; }
        public string WorldName { get; set; }
        public int CellX { get; set; }
        public int CellY { get; set; }
        public long PayloadBytes { get; set; }
    }

    public sealed class PerformanceCellHotspot
    {
        public int Rank { get; set; }
        public int WorldRank { get; set; }
        public int WorldId { get; set; }
        public string WorldName { get; set; }
        public int CellX { get; set; }
        public int CellY { get; set; }
        public PositionInfo ApproximateCenter { get; set; }
        public long CenterRecords { get; set; }
        public long CenterPayloadBytes { get; set; }
        public long NeighborhoodRecords { get; set; }
        public long NeighborhoodPayloadBytes { get; set; }
        public int NeighborhoodPopulatedCells { get; set; }
        public long TotalRecords { get; set; }
        public long TotalPayloadBytes { get; set; }
        public double Percentile { get; set; }
        public double RecordPercentile { get; set; }
        public double PayloadPercentile { get; set; }
        public string Severity { get; set; }
        public string Confidence { get; set; }
        public List<PerformanceEvidence> Evidence { get; set; }
        public List<PerformanceCategoryMetric> Categories { get; set; }
    }

    public sealed class PerformanceEvidence
    {
        public string Key { get; set; }
        public string Label { get; set; }
        public string Explanation { get; set; }
        public long ObservedValue { get; set; }
        public long ComparisonValue { get; set; }
    }

    public sealed class PerformanceScanProgress
    {
        public int Stage { get; set; }
        public int StageCount { get; set; }
        public string StageKey { get; set; }
        public string StageLabel { get; set; }
        public long CompletedUnits { get; set; }
        public long TotalUnits { get; set; }
        public int OverallPercent { get; set; }
        public string Message { get; set; }
    }

    public sealed class PerformanceScanStartResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public string OperationId { get; set; }
    }

    public sealed class PerformanceScanOperationStatus
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public string OperationId { get; set; }
        public string State { get; set; }
        public bool Terminal { get; set; }
        public bool CanCancel { get; set; }
        public PerformanceScanProgress Progress { get; set; }
        public PerformanceScanResult Result { get; set; }
    }

    public sealed class PerformanceReportExportResult
    {
        public bool Success { get; set; }
        public bool Cancelled { get; set; }
        public string Error { get; set; }
        public string FileName { get; set; }
    }

    public sealed class PerformanceCellPage
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public string OperationId { get; set; }
        public int ScanVersion { get; set; }
        public int WorldId { get; set; }
        public string WorldName { get; set; }
        public int Offset { get; set; }
        public int Limit { get; set; }
        public long TotalCells { get; set; }
        public bool HasMore { get; set; }
        public List<PerformanceCellSummary> Cells { get; set; }
    }
}
