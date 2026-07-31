using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace RaidRescue
{
    internal sealed class PerformanceReportExportPayload
    {
        public bool Success;
        public string Error;
        public string SuggestedFileName;
        public string Json;
    }

    internal static class PerformanceReportExporter
    {
        private const int CurrentFormatVersion = 1;

        public static PerformanceReportExportPayload Create(
            PerformanceScanResult result,
            string appVersion,
            DateTime exportedUtc)
        {
            if (result == null || !result.Success ||
                !result.SourceUnchanged)
            {
                return Failure(
                    "Only a completed, unchanged performance scan can " +
                    "be exported.");
            }

            PerformanceReportDocument document =
                new PerformanceReportDocument
                {
                    Format = "scraplab-performance-report",
                    FormatVersion = CurrentFormatVersion,
                    AppVersion = String.IsNullOrWhiteSpace(appVersion)
                        ? "unknown"
                        : appVersion.Trim(),
                    ScannerVersion = result.ScanVersion,
                    ExportedUtc = exportedUtc.ToUniversalTime().ToString(
                        "yyyy-MM-dd'T'HH:mm:ss'Z'"),
                    SaveVersion = result.SaveVersion,
                    Summary = new PerformanceReportSummary
                    {
                        WorldsScanned = result.WorldsScanned,
                        PopulatedCells = result.PopulatedCells,
                        TotalRecords = result.TotalRecords,
                        TotalPayloadBytes = result.TotalPayloadBytes,
                        PotentialHotspots = result.Hotspots == null
                            ? 0
                            : result.Hotspots.Count,
                        FileSizeBytes = result.FileSizeBytes,
                        DatabasePageBytes = result.DatabasePageBytes,
                        DatabaseAllocatedBytes =
                            result.DatabaseAllocatedBytes,
                        DatabaseFreeBytes = result.DatabaseFreeBytes
                    },
                    Coverage = new PerformanceReportCoverage
                    {
                        Ratio = result.Coverage,
                        DecodedSupportedRecords =
                            result.DecodedSupportedRecords,
                        RecordsConsidered = result.RecordsConsidered,
                        UnsupportedTableCount =
                            result.UnsupportedTableCount
                    },
                    Worlds = CopyWorlds(result.Worlds),
                    Categories = CopyCategories(result.Categories),
                    Hotspots = CopyHotspots(result.Hotspots),
                    Warnings = CopyStrings(result.Warnings)
                };

            string json = new JavaScriptSerializer
            {
                MaxJsonLength = Int32.MaxValue
            }.Serialize(document);
            return new PerformanceReportExportPayload
            {
                Success = true,
                Error = String.Empty,
                SuggestedFileName =
                    "ScrapLab-Performance-Report-v" +
                    result.ScanVersion + ".json",
                Json = json
            };
        }

        private static PerformanceReportExportPayload Failure(string error)
        {
            return new PerformanceReportExportPayload
            {
                Error = error ?? "The performance report could not be exported.",
                SuggestedFileName =
                    "ScrapLab-Performance-Report.json",
                Json = String.Empty
            };
        }

        private static List<PerformanceWorldSummary> CopyWorlds(
            IEnumerable<PerformanceWorldSummary> values)
        {
            List<PerformanceWorldSummary> result =
                new List<PerformanceWorldSummary>();
            if (values == null)
                return result;
            foreach (PerformanceWorldSummary value in values)
            {
                result.Add(new PerformanceWorldSummary
                {
                    WorldId = value.WorldId,
                    WorldName = SafeWorldName(
                        value.WorldName, value.WorldId),
                    PopulatedCells = value.PopulatedCells,
                    TotalRecords = value.TotalRecords,
                    TotalPayloadBytes = value.TotalPayloadBytes,
                    HotspotCount = value.HotspotCount
                });
            }
            return result;
        }

        internal static List<PerformanceCategoryMetric> CopyCategories(
            IEnumerable<PerformanceCategoryMetric> values)
        {
            List<PerformanceCategoryMetric> result =
                new List<PerformanceCategoryMetric>();
            if (values == null)
                return result;
            foreach (PerformanceCategoryMetric value in values)
            {
                result.Add(new PerformanceCategoryMetric
                {
                    Key = value.Key,
                    DisplayName = value.DisplayName,
                    RecordCount = value.RecordCount,
                    PayloadBytes = value.PayloadBytes,
                    DecodedCount = value.DecodedCount,
                    UnreadableCount = value.UnreadableCount
                });
            }
            return result;
        }

        private static List<PerformanceCellHotspot> CopyHotspots(
            IEnumerable<PerformanceCellHotspot> values)
        {
            List<PerformanceCellHotspot> result =
                new List<PerformanceCellHotspot>();
            if (values == null)
                return result;
            foreach (PerformanceCellHotspot value in values)
            {
                List<PerformanceEvidence> evidence =
                    new List<PerformanceEvidence>();
                if (value.Evidence != null)
                {
                    foreach (PerformanceEvidence item in value.Evidence)
                    {
                        evidence.Add(new PerformanceEvidence
                        {
                            Key = item.Key,
                            Label = item.Label,
                            Explanation = item.Explanation,
                            ObservedValue = item.ObservedValue,
                            ComparisonValue = item.ComparisonValue
                        });
                    }
                }
                result.Add(new PerformanceCellHotspot
                {
                    Rank = value.Rank,
                    WorldRank = value.WorldRank,
                    WorldId = value.WorldId,
                    WorldName = SafeWorldName(
                        value.WorldName, value.WorldId),
                    CellX = value.CellX,
                    CellY = value.CellY,
                    ApproximateCenter = value.ApproximateCenter == null
                        ? null
                        : new PositionInfo
                        {
                            X = value.ApproximateCenter.X,
                            Y = value.ApproximateCenter.Y,
                            Z = value.ApproximateCenter.Z
                        },
                    CenterRecords = value.CenterRecords,
                    CenterPayloadBytes = value.CenterPayloadBytes,
                    NeighborhoodRecords = value.NeighborhoodRecords,
                    NeighborhoodPayloadBytes =
                        value.NeighborhoodPayloadBytes,
                    NeighborhoodPopulatedCells =
                        value.NeighborhoodPopulatedCells,
                    TotalRecords = value.TotalRecords,
                    TotalPayloadBytes = value.TotalPayloadBytes,
                    Percentile = value.Percentile,
                    RecordPercentile = value.RecordPercentile,
                    PayloadPercentile = value.PayloadPercentile,
                    Severity = value.Severity,
                    Confidence = value.Confidence,
                    Evidence = evidence,
                    Categories = CopyCategories(value.Categories)
                });
            }
            return result;
        }

        private static List<string> CopyStrings(IEnumerable<string> values)
        {
            List<string> result = new List<string>();
            if (values == null)
                return result;
            foreach (string value in values)
                result.Add(value ?? String.Empty);
            return result;
        }

        private static string SafeWorldName(string value, int worldId)
        {
            string fallback = "World " + worldId;
            if (String.IsNullOrWhiteSpace(value))
                return fallback;
            string text = value.Trim();
            if (text.Length > 120 ||
                text.IndexOf('/') >= 0 ||
                text.IndexOf('\\') >= 0 ||
                text.IndexOf(':') >= 0)
            {
                return fallback;
            }
            foreach (char character in text)
            {
                if (Char.IsControl(character))
                    return fallback;
            }
            return text;
        }

        private sealed class PerformanceReportDocument
        {
            public string Format { get; set; }
            public int FormatVersion { get; set; }
            public string AppVersion { get; set; }
            public int ScannerVersion { get; set; }
            public string ExportedUtc { get; set; }
            public long SaveVersion { get; set; }
            public PerformanceReportSummary Summary { get; set; }
            public PerformanceReportCoverage Coverage { get; set; }
            public List<PerformanceWorldSummary> Worlds { get; set; }
            public List<PerformanceCategoryMetric> Categories { get; set; }
            public List<PerformanceCellHotspot> Hotspots { get; set; }
            public List<string> Warnings { get; set; }
        }

        private sealed class PerformanceReportSummary
        {
            public long WorldsScanned { get; set; }
            public long PopulatedCells { get; set; }
            public long TotalRecords { get; set; }
            public long TotalPayloadBytes { get; set; }
            public long PotentialHotspots { get; set; }
            public long FileSizeBytes { get; set; }
            public long DatabasePageBytes { get; set; }
            public long DatabaseAllocatedBytes { get; set; }
            public long DatabaseFreeBytes { get; set; }
        }

        private sealed class PerformanceReportCoverage
        {
            public double Ratio { get; set; }
            public long DecodedSupportedRecords { get; set; }
            public long RecordsConsidered { get; set; }
            public int UnsupportedTableCount { get; set; }
        }
    }
}
