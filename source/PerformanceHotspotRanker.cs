using System;
using System.Collections.Generic;
using System.Threading;

namespace RaidRescue
{
    internal static class PerformanceHotspotRanker
    {
        internal const int MaximumHotspots = 50;
        private const long MinimumNeighborhoodRecords = 24;
        private const long MinimumLargePayloadRecords = 4;
        private const long MinimumNeighborhoodPayloadBytes =
            256L * 1024L;
        private const long StrongRecordCount = 500;
        private const long StrongPayloadBytes = 1024L * 1024L;
        private const long StrongNeighborRecords = 250;

        public static void Rank(
            PerformanceScanResult result,
            CancellationToken cancellation)
        {
            if (result == null)
                throw new ArgumentNullException("result");
            result.Hotspots.Clear();
            foreach (PerformanceWorldSummary world in result.Worlds)
                world.HotspotCount = 0;
            if (result.Cells.Count == 0)
                return;

            Dictionary<int, List<PerformanceCellSummary>> worlds =
                GroupByWorld(result.Cells);
            List<Candidate> allCandidates = new List<Candidate>();
            List<int> worldIds = new List<int>(worlds.Keys);
            worldIds.Sort();
            foreach (int worldId in worldIds)
            {
                cancellation.ThrowIfCancellationRequested();
                List<Candidate> candidates = BuildWorldCandidates(
                    worlds[worldId], cancellation);
                candidates.Sort(CompareWithinWorld);
                List<Candidate> selected =
                    SuppressOverlapping(candidates, cancellation);
                for (int index = 0; index < selected.Count; index++)
                {
                    selected[index].WorldRank = index + 1;
                    allCandidates.Add(selected[index]);
                }
            }

            allCandidates.Sort(CompareAcrossWorlds);
            int count = Math.Min(
                MaximumHotspots, allCandidates.Count);
            Dictionary<int, PerformanceWorldSummary> summaries =
                IndexWorlds(result.Worlds);
            for (int index = 0; index < count; index++)
            {
                cancellation.ThrowIfCancellationRequested();
                Candidate candidate = allCandidates[index];
                PerformanceCellHotspot hotspot =
                    ToResult(candidate, index + 1);
                result.Hotspots.Add(hotspot);
                PerformanceWorldSummary summary;
                if (summaries.TryGetValue(
                    hotspot.WorldId, out summary))
                {
                    summary.HotspotCount++;
                }
            }
        }

        private static Dictionary<int, List<PerformanceCellSummary>>
            GroupByWorld(IEnumerable<PerformanceCellSummary> cells)
        {
            Dictionary<int, List<PerformanceCellSummary>> result =
                new Dictionary<int, List<PerformanceCellSummary>>();
            foreach (PerformanceCellSummary cell in cells)
            {
                List<PerformanceCellSummary> world;
                if (!result.TryGetValue(cell.WorldId, out world))
                {
                    world = new List<PerformanceCellSummary>();
                    result.Add(cell.WorldId, world);
                }
                world.Add(cell);
            }
            return result;
        }

        private static List<Candidate> BuildWorldCandidates(
            IList<PerformanceCellSummary> cells,
            CancellationToken cancellation)
        {
            Dictionary<CellCoordinate, PerformanceCellSummary> index =
                new Dictionary<CellCoordinate, PerformanceCellSummary>();
            foreach (PerformanceCellSummary cell in cells)
            {
                index.Add(
                    new CellCoordinate(cell.CellX, cell.CellY),
                    cell);
            }

            List<Candidate> totals = new List<Candidate>();
            List<long> recordDistribution = new List<long>();
            List<long> byteDistribution = new List<long>();
            foreach (PerformanceCellSummary cell in cells)
            {
                cancellation.ThrowIfCancellationRequested();
                Candidate candidate = new Candidate
                {
                    Cell = cell,
                    Categories = new Dictionary<
                        string, PerformanceCategoryMetric>(
                            StringComparer.Ordinal)
                };
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    for (int offsetY = -1; offsetY <= 1; offsetY++)
                    {
                        PerformanceCellSummary neighbor;
                        if (!index.TryGetValue(
                            new CellCoordinate(
                                cell.CellX + offsetX,
                                cell.CellY + offsetY),
                            out neighbor))
                        {
                            continue;
                        }
                        candidate.NeighborhoodRecords = Add(
                            candidate.NeighborhoodRecords,
                            neighbor.TotalRecords);
                        candidate.NeighborhoodPayloadBytes = Add(
                            candidate.NeighborhoodPayloadBytes,
                            neighbor.TotalPayloadBytes);
                        candidate.NeighborhoodPopulatedCells++;
                        if (neighbor.Categories != null &&
                            neighbor.Categories.Count > 0)
                        {
                            foreach (
                                PerformanceCategoryMetric category in
                                neighbor.Categories)
                            {
                                AddCategory(
                                    candidate.Categories, category);
                            }
                        }
                        else
                        {
                            AddCategory(
                                candidate.Categories,
                                new PerformanceCategoryMetric
                                {
                                    Key = "harvestable",
                                    DisplayName = "Harvestables",
                                    RecordCount =
                                        neighbor.TotalRecords,
                                    PayloadBytes =
                                        neighbor.TotalPayloadBytes,
                                    DecodedCount =
                                        neighbor.TotalRecords
                                });
                        }
                    }
                }
                totals.Add(candidate);
                recordDistribution.Add(
                    candidate.NeighborhoodRecords);
                byteDistribution.Add(
                    candidate.NeighborhoodPayloadBytes);
            }
            recordDistribution.Sort();
            byteDistribution.Sort();

            List<Candidate> candidates = new List<Candidate>();
            foreach (Candidate candidate in totals)
            {
                cancellation.ThrowIfCancellationRequested();
                candidate.RecordPercentile = Percentile(
                    recordDistribution,
                    candidate.NeighborhoodRecords);
                candidate.PayloadPercentile = Percentile(
                    byteDistribution,
                    candidate.NeighborhoodPayloadBytes);
                candidate.Score =
                    (candidate.RecordPercentile * 0.65) +
                    (candidate.PayloadPercentile * 0.35);
                if (!PassesEvidenceFloor(candidate) ||
                    candidate.Score < 90.0)
                {
                    continue;
                }
                candidate.StrongSignals =
                    CountStrongSignals(candidate);
                candidate.Severity =
                    GetSeverity(candidate);
                candidates.Add(candidate);
            }
            return candidates;
        }

        private static bool PassesEvidenceFloor(Candidate candidate)
        {
            return candidate.NeighborhoodRecords >=
                    MinimumNeighborhoodRecords ||
                (candidate.NeighborhoodRecords >=
                    MinimumLargePayloadRecords &&
                 candidate.NeighborhoodPayloadBytes >=
                    MinimumNeighborhoodPayloadBytes);
        }

        private static int CountStrongSignals(Candidate candidate)
        {
            int signals = 0;
            if (candidate.NeighborhoodRecords >= StrongRecordCount)
                signals++;
            if (candidate.NeighborhoodPayloadBytes >=
                StrongPayloadBytes)
            {
                signals++;
            }
            long neighborRecords = candidate.NeighborhoodRecords -
                candidate.Cell.TotalRecords;
            if (neighborRecords >= StrongNeighborRecords &&
                candidate.NeighborhoodPopulatedCells >= 3)
            {
                signals++;
            }
            return signals;
        }

        private static string GetSeverity(Candidate candidate)
        {
            if (candidate.Score >= 99.5 &&
                candidate.StrongSignals >= 2)
            {
                return "VERY HEAVY";
            }
            if (candidate.Score >= 97.0 &&
                candidate.StrongSignals >= 1)
            {
                return "HEAVY";
            }
            return "NOTABLE";
        }

        private static List<Candidate> SuppressOverlapping(
            IList<Candidate> ordered,
            CancellationToken cancellation)
        {
            List<Candidate> selected = new List<Candidate>();
            foreach (Candidate candidate in ordered)
            {
                cancellation.ThrowIfCancellationRequested();
                bool overlaps = false;
                foreach (Candidate existing in selected)
                {
                    if (Math.Abs(
                            candidate.Cell.CellX -
                            existing.Cell.CellX) <= 2 &&
                        Math.Abs(
                            candidate.Cell.CellY -
                            existing.Cell.CellY) <= 2)
                    {
                        overlaps = true;
                        break;
                    }
                }
                if (!overlaps)
                    selected.Add(candidate);
            }
            return selected;
        }

        private static PerformanceCellHotspot ToResult(
            Candidate candidate, int rank)
        {
            PerformanceCellSummary cell = candidate.Cell;
            PerformanceCellHotspot hotspot =
                new PerformanceCellHotspot
                {
                    Rank = rank,
                    WorldRank = candidate.WorldRank,
                    WorldId = cell.WorldId,
                    WorldName = cell.WorldName,
                    CellX = cell.CellX,
                    CellY = cell.CellY,
                    ApproximateCenter = new PositionInfo
                    {
                        X = cell.ApproximateCenterX,
                        Y = cell.ApproximateCenterY,
                        Z = 0.0
                    },
                    CenterRecords = cell.TotalRecords,
                    CenterPayloadBytes = cell.TotalPayloadBytes,
                    NeighborhoodRecords =
                        candidate.NeighborhoodRecords,
                    NeighborhoodPayloadBytes =
                        candidate.NeighborhoodPayloadBytes,
                    NeighborhoodPopulatedCells =
                        candidate.NeighborhoodPopulatedCells,
                    // Displayed totals intentionally represent the 3-by-3
                    // neighborhood described by each card.
                    TotalRecords =
                        candidate.NeighborhoodRecords,
                    TotalPayloadBytes =
                        candidate.NeighborhoodPayloadBytes,
                    Percentile = candidate.Score,
                    RecordPercentile =
                        candidate.RecordPercentile,
                    PayloadPercentile =
                        candidate.PayloadPercentile,
                    Severity = candidate.Severity,
                    Confidence = GetConfidence(
                        candidate.Categories),
                    Evidence = BuildEvidence(candidate),
                    Categories = OrderedCategories(
                        candidate.Categories)
                };
            return hotspot;
        }

        private static List<PerformanceEvidence> BuildEvidence(
            Candidate candidate)
        {
            List<PerformanceEvidence> evidence =
                new List<PerformanceEvidence>();
            evidence.Add(new PerformanceEvidence
            {
                Key = "world-percentile",
                Label = "Highest-density portion of this world",
                Explanation =
                    "Record count and stored bytes are ranked separately " +
                    "inside this world, then combined 65/35.",
                ObservedValue =
                    (long)Math.Round(candidate.Score * 100.0),
                ComparisonValue =
                    String.Equals(
                        candidate.Severity, "VERY HEAVY",
                        StringComparison.Ordinal)
                        ? 9950
                        : (String.Equals(
                            candidate.Severity, "HEAVY",
                            StringComparison.Ordinal)
                            ? 9700
                            : 9000)
            });
            if (candidate.NeighborhoodRecords >=
                MinimumNeighborhoodRecords)
            {
                evidence.Add(new PerformanceEvidence
                {
                    Key = "persisted-record-density",
                    Label = "High persisted object density",
                    Explanation =
                        "The centered 3-by-3 cell neighborhood contains " +
                        candidate.NeighborhoodRecords +
                        " supported cell-located records.",
                    ObservedValue =
                        candidate.NeighborhoodRecords,
                    ComparisonValue =
                        candidate.NeighborhoodRecords >=
                            StrongRecordCount
                            ? StrongRecordCount
                            : MinimumNeighborhoodRecords
                });
            }
            PerformanceCategoryMetric harvestables =
                FindCategory(
                    candidate.Categories, "harvestable");
            if (harvestables != null &&
                harvestables.RecordCount >=
                    MinimumNeighborhoodRecords)
            {
                evidence.Add(new PerformanceEvidence
                {
                    Key = "harvestable-concentration",
                    Label = "Large concentration of harvestables",
                    Explanation =
                        harvestables.RecordCount +
                        " Harvestable records contribute to this " +
                        "neighborhood.",
                    ObservedValue = harvestables.RecordCount,
                    ComparisonValue =
                        MinimumNeighborhoodRecords
                });
            }
            PerformanceCategoryMetric units =
                FindCategory(candidate.Categories, "unit");
            if (units != null &&
                units.RecordCount >=
                    MinimumNeighborhoodRecords)
            {
                evidence.Add(new PerformanceEvidence
                {
                    Key = "unit-concentration",
                    Label = "Many persistent units",
                    Explanation =
                        units.RecordCount +
                        " Unit records contribute to this " +
                        "neighborhood.",
                    ObservedValue = units.RecordCount,
                    ComparisonValue =
                        MinimumNeighborhoodRecords
                });
            }
            if (candidate.NeighborhoodPayloadBytes >=
                MinimumNeighborhoodPayloadBytes)
            {
                evidence.Add(new PerformanceEvidence
                {
                    Key = "stored-payload",
                    Label = "Unusually large supported payload total",
                    Explanation =
                        "The same neighborhood stores at least 256 KiB " +
                        "of decoded Harvestable payload data.",
                    ObservedValue =
                        candidate.NeighborhoodPayloadBytes,
                    ComparisonValue =
                        candidate.NeighborhoodPayloadBytes >=
                            StrongPayloadBytes
                            ? StrongPayloadBytes
                            : MinimumNeighborhoodPayloadBytes
                });
            }
            long neighborRecords = candidate.NeighborhoodRecords -
                candidate.Cell.TotalRecords;
            if (neighborRecords >= StrongNeighborRecords &&
                candidate.NeighborhoodPopulatedCells >= 3)
            {
                evidence.Add(new PerformanceEvidence
                {
                    Key = "dense-neighbors",
                    Label = "Multiple dense neighboring cells",
                    Explanation =
                        "Adjacent populated cells contribute at least " +
                        StrongNeighborRecords +
                        " additional persisted records.",
                    ObservedValue = neighborRecords,
                    ComparisonValue = StrongNeighborRecords
                });
            }
            return evidence;
        }

        private static void AddCategory(
            IDictionary<string, PerformanceCategoryMetric> totals,
            PerformanceCategoryMetric value)
        {
            PerformanceCategoryMetric total;
            if (!totals.TryGetValue(value.Key, out total))
            {
                total = new PerformanceCategoryMetric
                {
                    Key = value.Key,
                    DisplayName = value.DisplayName
                };
                totals.Add(value.Key, total);
            }
            total.RecordCount = Add(
                total.RecordCount, value.RecordCount);
            total.PayloadBytes = Add(
                total.PayloadBytes, value.PayloadBytes);
            total.DecodedCount = Add(
                total.DecodedCount, value.DecodedCount);
            total.UnreadableCount = Add(
                total.UnreadableCount, value.UnreadableCount);
        }

        private static PerformanceCategoryMetric FindCategory(
            IDictionary<string, PerformanceCategoryMetric> categories,
            string key)
        {
            PerformanceCategoryMetric result;
            return categories.TryGetValue(key, out result)
                ? result
                : null;
        }

        private static List<PerformanceCategoryMetric>
            OrderedCategories(
                IDictionary<string, PerformanceCategoryMetric> values)
        {
            List<PerformanceCategoryMetric> result =
                new List<PerformanceCategoryMetric>();
            foreach (PerformanceCategoryMetric value in values.Values)
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
            result.Sort(delegate(
                PerformanceCategoryMetric left,
                PerformanceCategoryMetric right)
            {
                int comparison =
                    right.RecordCount.CompareTo(left.RecordCount);
                if (comparison != 0)
                    return comparison;
                return String.Compare(
                    left.Key, right.Key,
                    StringComparison.Ordinal);
            });
            return result;
        }

        private static string GetConfidence(
            IDictionary<string, PerformanceCategoryMetric> categories)
        {
            long records = 0;
            long decoded = 0;
            long unreadable = 0;
            foreach (PerformanceCategoryMetric category in
                categories.Values)
            {
                records = Add(records, category.RecordCount);
                decoded = Add(decoded, category.DecodedCount);
                unreadable = Add(
                    unreadable, category.UnreadableCount);
            }
            if (records > 0 &&
                decoded == records &&
                unreadable == 0)
            {
                return "HIGH";
            }
            if (decoded > 0)
                return "PARTIAL";
            return "RAW DATA ONLY";
        }

        private static double Percentile(
            IList<long> ordered, long value)
        {
            int low = 0;
            int high = ordered.Count;
            while (low < high)
            {
                int middle = low + ((high - low) / 2);
                if (ordered[middle] <= value)
                    low = middle + 1;
                else
                    high = middle;
            }
            return ordered.Count == 0
                ? 0.0
                : ((double)low / ordered.Count) * 100.0;
        }

        private static int CompareWithinWorld(
            Candidate left, Candidate right)
        {
            int comparison = right.Score.CompareTo(left.Score);
            if (comparison != 0)
                return comparison;
            comparison = right.Cell.TotalRecords.CompareTo(
                left.Cell.TotalRecords);
            if (comparison != 0)
                return comparison;
            comparison =
                right.Cell.TotalPayloadBytes.CompareTo(
                    left.Cell.TotalPayloadBytes);
            if (comparison != 0)
                return comparison;
            comparison = left.Cell.CellX.CompareTo(right.Cell.CellX);
            if (comparison != 0)
                return comparison;
            return left.Cell.CellY.CompareTo(right.Cell.CellY);
        }

        private static int CompareAcrossWorlds(
            Candidate left, Candidate right)
        {
            int comparison = SeverityOrder(right.Severity).CompareTo(
                SeverityOrder(left.Severity));
            if (comparison != 0)
                return comparison;
            comparison = right.Score.CompareTo(left.Score);
            if (comparison != 0)
                return comparison;
            comparison =
                right.NeighborhoodRecords.CompareTo(
                    left.NeighborhoodRecords);
            if (comparison != 0)
                return comparison;
            comparison =
                right.NeighborhoodPayloadBytes.CompareTo(
                    left.NeighborhoodPayloadBytes);
            if (comparison != 0)
                return comparison;
            comparison =
                left.Cell.WorldId.CompareTo(right.Cell.WorldId);
            if (comparison != 0)
                return comparison;
            comparison = left.Cell.CellX.CompareTo(right.Cell.CellX);
            if (comparison != 0)
                return comparison;
            return left.Cell.CellY.CompareTo(right.Cell.CellY);
        }

        private static int SeverityOrder(string severity)
        {
            if (String.Equals(
                severity, "VERY HEAVY",
                StringComparison.Ordinal))
            {
                return 3;
            }
            if (String.Equals(
                severity, "HEAVY",
                StringComparison.Ordinal))
            {
                return 2;
            }
            return 1;
        }

        private static Dictionary<int, PerformanceWorldSummary>
            IndexWorlds(IEnumerable<PerformanceWorldSummary> worlds)
        {
            Dictionary<int, PerformanceWorldSummary> result =
                new Dictionary<int, PerformanceWorldSummary>();
            foreach (PerformanceWorldSummary world in worlds)
                result[world.WorldId] = world;
            return result;
        }

        private static long Add(long left, long right)
        {
            return checked(left + right);
        }

        private sealed class Candidate
        {
            public PerformanceCellSummary Cell;
            public long NeighborhoodRecords;
            public long NeighborhoodPayloadBytes;
            public int NeighborhoodPopulatedCells;
            public double RecordPercentile;
            public double PayloadPercentile;
            public double Score;
            public int StrongSignals;
            public int WorldRank;
            public string Severity;
            public Dictionary<
                string, PerformanceCategoryMetric> Categories;
        }

        private struct CellCoordinate : IEquatable<CellCoordinate>
        {
            private readonly int x;
            private readonly int y;

            public CellCoordinate(int xValue, int yValue)
            {
                x = xValue;
                y = yValue;
            }

            public bool Equals(CellCoordinate other)
            {
                return x == other.x && y == other.y;
            }

            public override bool Equals(object value)
            {
                return value is CellCoordinate &&
                    Equals((CellCoordinate)value);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (x * 397) ^ y;
                }
            }
        }
    }
}
