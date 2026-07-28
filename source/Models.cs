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
        public List<string> Warnings { get; set; }
        public bool GameRunning { get; set; }
        public bool CanClear { get; set; }
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
}
