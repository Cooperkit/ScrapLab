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

        internal Dictionary<string, bool> ActivationChanges;
    }
}
