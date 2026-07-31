using System;
using System.Collections.Generic;
using System.Threading;

namespace RaidRescue
{
    internal sealed class PerformanceScanOperationManager : IDisposable
    {
        private readonly object sync = new object();
        private Operation current;
        private bool disposed;

        public PerformanceScanStartResult Begin(string path)
        {
            Operation operation;
            lock (sync)
            {
                if (disposed)
                {
                    return StartFailure(
                        "The performance scanner is shutting down.");
                }
                if (current != null && !current.Terminal)
                {
                    return StartFailure(
                        "A performance scan is already running.");
                }
                if (current != null)
                    current.Cancellation.Dispose();

                operation = new Operation
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Path = path,
                    State = "queued",
                    Progress = new PerformanceScanProgress
                    {
                        Stage = 0,
                        StageCount = 6,
                        StageKey = "queued",
                        StageLabel = "Queued",
                        OverallPercent = 0,
                        Message = "Waiting for the scanner thread."
                    },
                    Cancellation =
                        new CancellationTokenSource()
                };
                operation.Worker = new Thread(
                    new ThreadStart(
                        delegate { Run(operation); }))
                {
                    IsBackground = true,
                    Name = "ScrapLab Performance Scan"
                };
                current = operation;
            }

            try
            {
                operation.Worker.Start();
                return new PerformanceScanStartResult
                {
                    Success = true,
                    Error = String.Empty,
                    OperationId = operation.Id
                };
            }
            catch (Exception exception)
            {
                lock (sync)
                {
                    operation.State = "failed";
                    operation.Terminal = true;
                    operation.Error = exception.Message;
                }
                return StartFailure(
                    "The performance scan could not start: " +
                    exception.Message);
            }
        }

        public PerformanceScanOperationStatus GetStatus(
            string operationId)
        {
            lock (sync)
            {
                if (current == null ||
                    !String.Equals(
                        current.Id, operationId,
                        StringComparison.Ordinal))
                {
                    return new PerformanceScanOperationStatus
                    {
                        Success = false,
                        Error = "The performance scan operation was not found.",
                        OperationId = operationId ?? String.Empty,
                        State = "not_found",
                        Terminal = true,
                        CanCancel = false,
                        Progress = EmptyProgress()
                    };
                }
                return new PerformanceScanOperationStatus
                {
                    Success = true,
                    Error = current.Error ?? String.Empty,
                    OperationId = current.Id,
                    State = current.State,
                    Terminal = current.Terminal,
                    CanCancel =
                        !current.Terminal &&
                        !String.Equals(
                            current.State, "cancelling",
                            StringComparison.Ordinal),
                    Progress = CopyProgress(current.Progress),
                    Result = current.Terminal &&
                        current.Result != null
                            ? ToBrowserResult(current.Result)
                            : null
                };
            }
        }

        public bool Cancel(string operationId)
        {
            lock (sync)
            {
                if (current == null ||
                    current.Terminal ||
                    !String.Equals(
                        current.Id, operationId,
                        StringComparison.Ordinal))
                    return false;
                if (!String.Equals(
                    current.State, "cancelling",
                    StringComparison.Ordinal))
                {
                    current.State = "cancelling";
                    current.Error = String.Empty;
                    current.Cancellation.Cancel();
                }
                return true;
            }
        }

        public PerformanceReportExportPayload CreateExport(
            string operationId,
            string appVersion,
            DateTime exportedUtc)
        {
            lock (sync)
            {
                if (!IsCompletedOperation(operationId))
                {
                    return new PerformanceReportExportPayload
                    {
                        Error = "A completed performance report is not " +
                            "available for export.",
                        SuggestedFileName =
                            "ScrapLab-Performance-Report.json",
                        Json = String.Empty
                    };
                }
                return PerformanceReportExporter.Create(
                    current.Result, appVersion, exportedUtc);
            }
        }

        public PerformanceCellPage GetWorldCells(
            string operationId,
            int worldId,
            int offset,
            int limit)
        {
            lock (sync)
            {
                int safeOffset = Math.Max(0, offset);
                int safeLimit = Math.Max(1, Math.Min(250, limit));
                if (!IsCompletedOperation(operationId))
                {
                    return CellPageFailure(
                        operationId,
                        worldId,
                        safeOffset,
                        safeLimit,
                        "A completed performance report is not available.");
                }

                PerformanceScanResult result = current.Result;
                PerformanceWorldSummary selectedWorld = null;
                if (result.Worlds != null)
                {
                    foreach (PerformanceWorldSummary world in result.Worlds)
                    {
                        if (world.WorldId == worldId)
                        {
                            selectedWorld = world;
                            break;
                        }
                    }
                }
                if (selectedWorld == null)
                {
                    return CellPageFailure(
                        operationId,
                        worldId,
                        safeOffset,
                        safeLimit,
                        "The requested world is not part of this report.");
                }

                List<PerformanceCellSummary> pageCells =
                    new List<PerformanceCellSummary>();
                long total = 0;
                if (result.Cells != null)
                {
                    foreach (PerformanceCellSummary cell in result.Cells)
                    {
                        if (cell.WorldId != worldId)
                            continue;
                        if (total >= safeOffset &&
                            pageCells.Count < safeLimit)
                        {
                            pageCells.Add(CopyCell(cell));
                        }
                        total++;
                    }
                }

                return new PerformanceCellPage
                {
                    Success = true,
                    Error = String.Empty,
                    OperationId = current.Id,
                    ScanVersion = result.ScanVersion,
                    WorldId = worldId,
                    WorldName = selectedWorld.WorldName,
                    Offset = safeOffset,
                    Limit = safeLimit,
                    TotalCells = total,
                    HasMore = safeOffset + pageCells.Count < total,
                    Cells = pageCells
                };
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                    return;
                disposed = true;
                if (current != null && !current.Terminal)
                {
                    current.State = "cancelling";
                    current.Cancellation.Cancel();
                }
            }
        }

        private void Run(Operation operation)
        {
            lock (sync)
            {
                if (disposed)
                    operation.Cancellation.Cancel();
                operation.State = operation.Cancellation.IsCancellationRequested
                    ? "cancelling"
                    : "running";
            }

            PerformanceScanResult result = PerformanceScanner.Scan(
                operation.Path,
                operation.Cancellation.Token,
                delegate(PerformanceScanProgress progress)
                {
                    lock (sync)
                    {
                        if (!ReferenceEquals(current, operation) ||
                            operation.Terminal)
                            return;
                        operation.Progress = CopyProgress(progress);
                    }
                });

            lock (sync)
            {
                if (!ReferenceEquals(current, operation))
                    return;
                operation.Result = result;
                operation.Error = result.Error ?? String.Empty;
                operation.Terminal = true;
                if (result.Success)
                {
                    operation.State = "completed";
                    operation.Progress = new PerformanceScanProgress
                    {
                        Stage = 6,
                        StageCount = 6,
                        StageKey = "complete",
                        StageLabel = "Complete",
                        CompletedUnits = 1,
                        TotalUnits = 1,
                        OverallPercent = 100,
                        Message = "The performance report is ready."
                    };
                }
                else if (result.Cancelled)
                {
                    operation.State = "cancelled";
                }
                else
                {
                    operation.State = "failed";
                }
            }
        }

        private static PerformanceScanStartResult StartFailure(
            string error)
        {
            return new PerformanceScanStartResult
            {
                Success = false,
                Error = error,
                OperationId = String.Empty
            };
        }

        private static PerformanceScanProgress EmptyProgress()
        {
            return new PerformanceScanProgress
            {
                StageCount = 6,
                StageKey = "none",
                StageLabel = "Not running",
                Message = String.Empty
            };
        }

        private static PerformanceScanProgress CopyProgress(
            PerformanceScanProgress value)
        {
            if (value == null)
                return EmptyProgress();
            return new PerformanceScanProgress
            {
                Stage = value.Stage,
                StageCount = value.StageCount,
                StageKey = value.StageKey,
                StageLabel = value.StageLabel,
                CompletedUnits = value.CompletedUnits,
                TotalUnits = value.TotalUnits,
                OverallPercent = value.OverallPercent,
                Message = value.Message
            };
        }

        private static PerformanceScanResult ToBrowserResult(
            PerformanceScanResult value)
        {
            return new PerformanceScanResult
            {
                Success = value.Success,
                Cancelled = value.Cancelled,
                Error = value.Error,
                ScanVersion = value.ScanVersion,
                DurationMilliseconds = value.DurationMilliseconds,
                SaveVersion = value.SaveVersion,
                DatabaseStatus = value.DatabaseStatus,
                FileSizeBytes = value.FileSizeBytes,
                DatabasePageBytes = value.DatabasePageBytes,
                DatabaseAllocatedBytes =
                    value.DatabaseAllocatedBytes,
                DatabaseFreeBytes = value.DatabaseFreeBytes,
                WorldsScanned = value.WorldsScanned,
                PopulatedCells = value.PopulatedCells,
                TotalRecords = value.TotalRecords,
                TotalPayloadBytes = value.TotalPayloadBytes,
                Coverage = value.Coverage,
                DecodedSupportedRecords =
                    value.DecodedSupportedRecords,
                RecordsConsidered = value.RecordsConsidered,
                SourceUnchanged = value.SourceUnchanged,
                UnsupportedTableCount =
                    value.UnsupportedTableCount,
                Schema = value.Schema,
                UnsupportedTables = value.UnsupportedTables,
                Worlds = value.Worlds,
                // Per-cell summaries stay in the host. Only the ranked,
                // bounded hotspot cards cross the browser bridge.
                Cells = new List<PerformanceCellSummary>(),
                Categories = value.Categories,
                LargestRecords = value.LargestRecords,
                Hotspots = value.Hotspots,
                Warnings = value.Warnings
            };
        }

        private bool IsCompletedOperation(string operationId)
        {
            return current != null &&
                current.Terminal &&
                current.Result != null &&
                current.Result.Success &&
                String.Equals(
                    current.Id,
                    operationId,
                    StringComparison.Ordinal);
        }

        private static PerformanceCellPage CellPageFailure(
            string operationId,
            int worldId,
            int offset,
            int limit,
            string error)
        {
            return new PerformanceCellPage
            {
                Success = false,
                Error = error ?? "The cell page could not be read.",
                OperationId = operationId ?? String.Empty,
                WorldId = worldId,
                WorldName = String.Empty,
                Offset = offset,
                Limit = limit,
                Cells = new List<PerformanceCellSummary>()
            };
        }

        private static PerformanceCellSummary CopyCell(
            PerformanceCellSummary value)
        {
            return new PerformanceCellSummary
            {
                WorldId = value.WorldId,
                WorldName = value.WorldName,
                CellX = value.CellX,
                CellY = value.CellY,
                ApproximateCenterX = value.ApproximateCenterX,
                ApproximateCenterY = value.ApproximateCenterY,
                TotalRecords = value.TotalRecords,
                TotalPayloadBytes = value.TotalPayloadBytes,
                Categories = PerformanceReportExporter.CopyCategories(
                    value.Categories)
            };
        }

        private sealed class Operation
        {
            public string Id;
            public string Path;
            public string State;
            public string Error;
            public bool Terminal;
            public PerformanceScanProgress Progress;
            public PerformanceScanResult Result;
            public CancellationTokenSource Cancellation;
            public Thread Worker;
        }
    }
}
