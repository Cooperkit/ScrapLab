# Performance Hotspot Scanner: Phase 1 Foundation

## Outcome

Phase 1 is complete. ScrapLab now contains a command-level, read-only
performance scanner foundation. It is compiled into the main executable but is
not connected to the browser bridge or UI.

Phase 2, ranking, severity, hotspot cards, and user-facing scan controls have
not started.

## Implemented foundation

### Result model

`source/Shared/Models.cs` now defines:

- `PerformanceScanResult`
- `PerformanceSchemaCoverage`
- `PerformanceWorldSummary`
- `PerformanceCellSummary`
- `PerformanceCategoryMetric`
- `PerformanceLargestRecord`
- the later-phase hotspot and evidence result shapes

Counts and payload totals use `long`. Phase 1 returns ordered world, cell,
category, and bounded largest-record summaries. `Hotspots` remains empty until
the Phase 3 ranking policy is implemented.

### Read-only SQLite APIs

`source/World/SqliteNative.cs` now provides narrowly scoped, constant-SQL methods for:

- SQLite page size, page count, and free-page count;
- the Phase 0 schema capability checks;
- raw row and payload totals grouped by world;
- streaming Harvestable world/cell/payload lengths;
- both proven version-26 and version-28 world-metadata layouts.

Save-provided table or column names are never interpolated into SQL.
Harvestable rows are passed directly to an aggregation callback and are never
collected into a full-record list.

### Scanner service

`source/Performance/PerformanceScanner.cs`:

1. validates the selected `.db` path;
2. refuses to scan while Scrap Mechanic or its server is running;
3. fingerprints the database and its `-wal`/`-shm` sidecars;
4. opens one `SqliteDatabase.OpenReadOnly` connection;
5. requires a successful `PRAGMA quick_check`;
6. detects allowlisted schema capabilities;
7. reads storage statistics and core save metadata;
8. streams and aggregates Harvestables by world and cell;
9. counts recognized `ScriptData` and `GenericData` layouts by world;
10. reuses strict decoded names from `WorldStorage`;
11. retains only the 25 largest supported records;
12. checks cancellation throughout hashing, queries, and row streaming;
13. rejects the result if the game starts or any source fingerprint changes.

Memory grows with populated worlds and cells rather than Harvestable row count.
No item icons, inventory cards, raw blobs, UUIDs, filenames, or paths enter the
result.

Approximate centers follow the Phase 0 proven axis conversion:

```text
worldCenterX = (cellY * 64) + 32
worldCenterY = (cellX * 64) + 32
```

### Deterministic command output

`PerformanceScanner.SerializeDeterministic` serializes the result in stable
world/cell/category order. It normalizes only wall-clock duration, which is
diagnostic rather than save content. It does not mutate the live result while
serializing.

## Regression coverage

`tests/PerformanceScannerRegression.ps1` generates and scans five temporary
fixtures:

1. Ordinary current save
2. Dense long-running save
3. Multi-world warehouse save
4. Legacy version-26 save
5. Modded save with an unknown table

The dense fixture contains 50,576 Harvestable rows but only 289 populated
cells. The regression proves:

- exact world, cell, record, and Harvestable totals;
- only cell/world summaries and 25 largest records are retained;
- repeated scans produce byte-for-byte deterministic JSON;
- current warehouse world names decode correctly;
- paths and filenames are absent from JSON;
- original fixtures and scan copies retain their SHA-256 hashes;
- a deliberately changed synthetic file produces a different fingerprint;
- a pre-cancelled scan returns a cancelled result;
- Phase 1 does not perform Phase 3 ranking.

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File tests\PerformanceScannerRegression.ps1
```

## Real-save smoke result

A read-only smoke scan against the current version-28 save completed with:

- 98,767 supported records
- 78,822 Harvestables
- 15 represented worlds
- 8,164 populated Harvestable cells
- 25 retained largest-record summaries

The scanner reported `SourceUnchanged=true`, and a separate SHA-256 check also
matched before and after. No path, filename, coordinates, or raw save content
was persisted from this smoke test.

## Phase 1 exit decision

The Phase 1 exit condition is satisfied:

- the scanner builds as part of `ScrapLab.exe`;
- the SQLite connection is read-only;
- source fingerprints are checked before and after;
- cancellation is represented and checked during bounded work;
- Harvestables are streamed rather than returned as a raw list;
- memory is bounded by populated cells, worlds, categories, and 25 records;
- output order is deterministic;
- a command-level regression scans all generated fixture types without file
  changes;
- existing updater, companion-boundary, adaptive-patch, and crop-release
  regressions remain green.

Before Phase 2 starts, reread the complete implementation plan. Phase 2 must
add only asynchronous lifecycle and UI integration; it must not introduce the
Phase 3 ranking policy early.
