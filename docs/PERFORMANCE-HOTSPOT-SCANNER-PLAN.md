# Performance Hotspot Scanner Plan

## Status

Phases 0 through 5 completed on 2026-07-31.
The scanner is available in the app as a cancellable, read-only world-density
report with bounded, explainable 3-by-3 hotspot ranking. It ranks only proven
record families. Phase 4 added the validated persistent-Unit family and
explicit unknown/modded schema reporting without guessing at Unit subtypes.
Phase 5 added a versioned privacy-safe JSON export and a local, paginated
aggregated-cell interface for World Explorer reuse. Scan-to-scan comparison is
deferred until exported reports have real-world format stability.
The validated research and allowlists are recorded in
[PERFORMANCE-HOTSPOT-SCANNER-RESEARCH.md](PERFORMANCE-HOTSPOT-SCANNER-RESEARCH.md).
The command-level scanner foundation is recorded in
[PERFORMANCE-HOTSPOT-SCANNER-PHASE1.md](PERFORMANCE-HOTSPOT-SCANNER-PHASE1.md).
The asynchronous lifecycle and UI integration are recorded in
[PERFORMANCE-HOTSPOT-SCANNER-PHASE2.md](PERFORMANCE-HOTSPOT-SCANNER-PHASE2.md).
The calibrated ranking policy and presentation are recorded in
[PERFORMANCE-HOTSPOT-SCANNER-PHASE3.md](PERFORMANCE-HOTSPOT-SCANNER-PHASE3.md).
The expanded Unit coverage and safe-degradation behavior are recorded in
[PERFORMANCE-HOTSPOT-SCANNER-PHASE4.md](PERFORMANCE-HOTSPOT-SCANNER-PHASE4.md).
The export contract and bounded World Explorer interface are recorded in
[PERFORMANCE-HOTSPOT-SCANNER-PHASE5.md](PERFORMANCE-HOTSPOT-SCANNER-PHASE5.md).

Before starting any later phase, reread this entire plan from beginning to end.

## Product Goal

Add an optional, completely read-only scan that helps a player find areas and
record categories in a Scrap Mechanic Survival save that are unusually dense
and may contribute to poor performance.

The scanner must answer four questions in plain language:

1. Which worlds and cells contain the most stored objects?
2. What kinds of records make each area unusually heavy?
3. Where should the player investigate in-game?
4. How confident is ScrapLab in each finding?

The scanner must describe results as **potential performance hotspots**. A save
database can reveal stored density and unusually large records, but it cannot
measure frame rate, physics cost, CPU time, or prove that one record is causing
lag.

## Safety Rules

- Open the selected save through `SqliteDatabase.OpenReadOnly`.
- Never add delete, edit, cleanup, or repair actions in the first release.
- Require Scrap Mechanic to be closed so the scan sees one consistent save
  snapshot.
- Never upload the save, scan results, coordinates, UUIDs, or filenames.
- Hash or fingerprint the file before and after the scan. If the file changes,
  discard the result and ask the player to close the game and scan again.
- Unknown tables, columns, UUIDs, and payloads must be reported as unknown,
  never treated as corruption.
- Query only allowlisted table and column names discovered and validated during
  development. Do not construct SQL from untrusted database identifiers.
- A failed or cancelled scan must not affect ordinary raid analysis, dropped
  item scanning, or repair readiness.

## User Experience

### Entry point

After an ordinary world analysis succeeds, add a new optional action:

**SCAN PERFORMANCE**

Supporting text:

> Find unusually crowded areas and heavy save records. Read-only and local.

This remains separate from **Scan Loose Items**. A player should not have to
load item icons or every dropped-item detail just to run the performance scan.

### Scan progress

Use real progress stages rather than an animated estimate:

1. Checking database layout
2. Counting stored records
3. Grouping records by world and cell
4. Decoding supported record types
5. Ranking potential hotspots
6. Building the report

The scan must run off the UI thread and expose a working **Cancel Scan** action.
Closing the app during a scan must cancel safely and exit without waiting for
the entire database to finish.

### Results layout

Display a new **Performance Scan** section with:

- A compact summary strip:
  - Worlds scanned
  - Stored records counted
  - Populated cells
  - Potential hotspots
  - Scan duration
- A world selector using the same decoded names as raid and dropped-item cards.
- A ranked list of hotspot cards.
- A category breakdown showing which record families occupy the save.
- A short limitations panel explaining that this is database-density analysis,
  not an FPS benchmark.

Each hotspot card should show:

- Rank and severity: `NOTABLE`, `HEAVY`, or `VERY HEAVY`
- Decoded world name
- Cell coordinates
- Approximate world-space center when the conversion is proven
- Total records represented
- Stored payload bytes
- Breakdown by supported category
- A comparison such as “heavier than 96% of populated cells in this world”
- One or more evidence statements:
  - “Large concentration of harvestables”
  - “Many loose pickup stacks”
  - “Unusually large script payloads”
  - “Multiple dense neighboring cells”
- Confidence: `HIGH`, `PARTIAL`, or `RAW DATA ONLY`
- **Copy coordinates** when coordinates are reliable

Do not show internal row IDs on normal cards. They may appear only in an
explicit developer diagnostic export.

### Empty and uncertain states

- No hotspots: “No unusually dense saved areas were found.”
- Unsupported schema: explain which parts could not be counted and continue
  with supported data.
- Only raw counts available: show the result with `RAW DATA ONLY`, not a
  guessed object name.
- Changed save: discard the report and tell the player the file changed during
  the scan.

## Scanner Scope

### Version-one measurements

The first implementation should use data ScrapLab already understands plus
safe SQLite metadata:

- Save file size
- SQLite page size, page count, and free-page count
- Row counts for validated game tables
- Total payload bytes for validated blob columns
- Records grouped by `worldId`
- Records grouped by cell where validated `x` and `y` cell columns exist
- `Harvestable` record count and payload bytes per cell
- Decoded loose pickup stack count and item quantity per cell
- Raid count, crop references, and live-raider references per world
- Largest individual supported records
- Decoded world names from `WorldStorage`

SQLite storage statistics must be labeled as save-size information. Free pages
or a large database do not automatically mean the world will run slowly.

### Candidate record families for later decoders

During the research phase, collect schemas from clean, long-running, warehouse,
modded, and Chapter 2 saves. Determine which tables safely expose world and
cell information for:

- Bodies and creations
- Shapes and joints
- Units and characters
- Harvestables
- Script records
- Containers and inventories
- Lifts and other persistent interactables

These are candidates, not assumptions. A family enters the scanner only after
its table layout, coordinates, identifiers, and version behavior are proven by
fixtures.

### Explicitly out of scope for version one

- Automatically deleting objects from hotspot cells
- Claiming an exact FPS improvement
- Editing, vacuuming, or compacting the database
- Launching or controlling Scrap Mechanic
- Online analytics or community comparisons
- Reading arbitrary mod payloads as if they used the base-game format
- Rendering a full terrain map

## Data Architecture

### New result models

Add models similar to the following:

```text
PerformanceScanResult
  Success
  Cancelled
  Error
  ScanVersion
  DurationMilliseconds
  FileSizeBytes
  DatabasePageBytes
  DatabaseAllocatedBytes
  DatabaseFreeBytes
  WorldsScanned
  PopulatedCells
  TotalRecords
  TotalPayloadBytes
  Hotspots[]
  Worlds[]
  Categories[]
  LargestRecords[]
  Warnings[]
  Coverage

PerformanceWorldSummary
  WorldId
  WorldName
  PopulatedCells
  TotalRecords
  TotalPayloadBytes
  HotspotCount

PerformanceCellHotspot
  WorldId
  WorldName
  CellX
  CellY
  ApproximateCenter
  TotalRecords
  TotalPayloadBytes
  Percentile
  Severity
  Confidence
  Evidence[]
  Categories[]

PerformanceCategoryMetric
  Key
  DisplayName
  RecordCount
  PayloadBytes
  DecodedCount
  UnreadableCount

PerformanceEvidence
  Key
  Label
  Explanation
  ObservedValue
  ComparisonValue
```

Use `long` for counts and byte totals. Never assume that record counts or
payload sums fit in a 32-bit integer.

### New scanner service

Create `source/Performance/PerformanceScanner.cs` rather than expanding
`source/World/RaidService.cs`.
Its responsibilities:

- Validate the input path and game-closed requirement.
- Capture the source fingerprint.
- Open one read-only database connection.
- Detect supported schema capabilities.
- Stream and aggregate supported rows.
- Decode only proven record formats.
- Rank hotspots.
- Recheck the source fingerprint.
- Return a bounded result model.

`RaidService` may expose a small shared helper for decoded loose pickups, but
the new scanner must avoid building the full dropped-item card and icon model.
Performance scanning needs counts and cell totals, not every embedded image.

### SQLite access additions

Extend `SqliteNative.cs` with narrowly scoped methods:

- `ReadStorageStatistics`
- `ReadSupportedSchema`
- `CountSupportedRows`
- Streaming readers for each validated record family
- Cancellation checks between rows and queries

Do not return millions of records in a `List<T>`. Aggregate records while the
SQLite statement is being read and retain only:

- Per-world counters
- Per-cell counters
- Per-category counters
- A bounded top-record heap
- Warnings and coverage totals

The final JSON sent to the embedded browser should contain summaries and the
top hotspot cards, not raw database rows.

### Background operation bridge

The existing `window.external` calls are synchronous. A large scan must not be
implemented as one blocking bridge call.

Add a background operation interface:

```text
BeginPerformanceScan(path) -> operation ID
GetPerformanceScanStatus(operation ID) -> progress/result JSON
CancelPerformanceScan(operation ID)
```

Only one performance scan may run at a time. The WinForms host owns the worker
thread, cancellation signal, progress state, and final result. The browser
polls status at a modest interval and stops polling when the operation reaches
a terminal state.

All browser callbacks and window-closing paths must tolerate a disposed browser
or cancelled operation.

## Hotspot Ranking

### Aggregation unit

Use `(worldId, cellX, cellY)` as the smallest displayed unit when a record has
proven cell coordinates.

Also calculate a 3-by-3 neighborhood total around each populated cell. Physics
or object density near a cell boundary should not be split into two harmless
looking cards. Store both the center-cell metrics and neighborhood metrics, but
avoid returning nine duplicate cards for the same cluster.

### Ranking principles

- Rank cells separately inside each world before comparing worlds.
- Prefer percentile comparisons over one universal threshold because
  Overworld and warehouse floors have very different distributions.
- Require a minimum amount of evidence before labeling a cell a hotspot.
- Weight stored record count and payload bytes separately.
- Treat a dropped stack as one persisted object for density. Item quantity is
  useful context but must not make one stack of 50 items look like 50 physical
  objects.
- Do not let one unknown payload create a `VERY HEAVY` label without explaining
  that the result is based only on stored bytes.
- Collapse overlapping high-ranked neighborhoods into one cluster.

### Initial severity policy

Thresholds must be calibrated against regression fixtures before release. The
first policy should be transparent and conservative:

- `NOTABLE`: passes the minimum evidence floor and is in the highest-density
  portion of its world.
- `HEAVY`: high percentile plus at least one strong absolute signal.
- `VERY HEAVY`: extreme percentile plus multiple independent strong signals.

Keep the evidence that produced the label in the result model. The UI should
never display a severity that cannot be explained.

### Coverage and confidence

Calculate coverage for every report:

```text
decoded supported records / all records considered
```

Use:

- `HIGH`: coordinates and category are decoded for nearly all contributing
  records.
- `PARTIAL`: the cell is known, but some categories or payloads are unknown.
- `RAW DATA ONLY`: ranking is based mainly on row and byte counts.

Do not combine unknown rows into a fake “other object” category unless the
table and location are both proven.

## Performance Requirements

- The app UI remains responsive throughout the scan.
- Cancellation is acknowledged promptly between SQLite rows or bounded query
  batches.
- Memory usage grows with the number of populated cells, not total database
  rows.
- Keep only a configurable maximum number of hotspot cards, initially 50.
- Keep only a configurable maximum number of largest-record entries,
  initially 25.
- Sort and serialize only after aggregation finishes.
- Avoid loading item icons during the performance scan.
- Record stage timings in a local diagnostic log.
- If a scan exceeds a conservative time budget, continue only while progress
  is advancing and allow immediate cancellation.

## Compatibility Strategy

Build a schema capability object from validated checks rather than relying on
one save version number.

Examples:

- `CanReadHarvestableCells`
- `CanReadWorldMetadata`
- `CanReadRaidManager`
- Future flags for each proven entity family

Older or modded saves should receive a partial report instead of a fatal error
when optional tables or columns are missing. Core failures—an unreadable Game
table, failed SQLite integrity check, or changing source file—must stop the
scan.

Include a `ScanVersion` in results so saved reports remain understandable after
the ranking algorithm changes.

## UI Design Direction

Match the existing Scrap Mechanic-inspired interface:

- Amber scan action
- Cyan world and coordinate accents
- Orange/red only for genuinely heavy findings
- Compact geometric severity badges
- Horizontal category bars
- Animated scan sweep while active
- Cards entering in ranked order after completion

Use CSS bars and simple inline SVG only. The current embedded `WebBrowser`
engine must remain supported; do not depend on modern canvas, WebGL, CSS grid
features that the existing renderer cannot reliably display, or third-party
chart libraries.

A future World Explorer can consume the same aggregated cell result, but the
first release should use a ranked list and a simple cell-density grid rather
than a full terrain map.

## Diagnostics and Export

Add an optional **Export Performance Report** action after a successful scan.
The default report should contain:

- Scanner and app versions
- Save version, but not the local save path
- World display names
- Aggregated counts and bytes
- Hotspot cells and evidence
- Coverage and warnings

Do not include Steam IDs, Windows usernames, absolute paths, raw database
blobs, or full inventory contents.

A separate developer-only diagnostic mode may include schema names and
anonymous record-format statistics, but it must still exclude raw payloads by
default.

## Implementation Phases

### Phase 0: Fixture research

- Collect copies of representative saves outside the repository.
- Include new, long-running, warehouse, Chapter 2, and modded worlds.
- Inventory tables and schemas without changing the saves.
- Identify which tables have proven world/cell coordinates.
- Document record-family confidence and version differences.
- Add sanitized generated fixtures to tests; never commit personal saves.

Exit condition: the first supported table allowlist and coordinate semantics
are proven.

### Phase 1: Scanner foundation

- Add result models and `PerformanceScanner`.
- Add read-only storage statistics and row-count APIs.
- Stream and aggregate `Harvestable` records.
- Reuse decoded world names.
- Add source fingerprint checks and cancellation.
- Produce a deterministic JSON result without UI.

Exit condition: a command-level regression can scan a fixture with bounded
memory and no file changes.

### Phase 2: Asynchronous app integration

- Add begin, status, and cancel bridge methods.
- Add real staged progress.
- Handle app shutdown and browser disposal safely.
- Add the **Scan Performance** entry point and results section.

Exit condition: a large fixture can be cancelled and the UI never freezes.

### Phase 3: Ranking and presentation

- Implement cell and 3-by-3 neighborhood aggregation.
- Calibrate transparent severity thresholds.
- Add evidence, coverage, and confidence.
- Build ranked cards, world filters, and category summaries.
- Add coordinate copying and accessible empty/error states.

Exit condition: every displayed severity can be traced to serialized evidence.

### Phase 4: Expanded record coverage

- Add one validated record-family decoder at a time.
- Add a fixture and regression test before enabling each family.
- Recalculate coverage without changing old result meaning.
- Add unknown/modded schema reporting.

Exit condition: supported families degrade safely across the fixture matrix.

### Phase 5: Report export and World Explorer reuse

- Add privacy-safe JSON or HTML export.
- Expose aggregated cell data to the planned World Explorer.
- Consider scan-to-scan comparison after the result format is stable.

## Test Plan

### Unit tests

- Per-world and per-cell aggregation
- 3-by-3 neighborhood totals
- Overlapping hotspot cluster suppression
- Deterministic ranking and tie-breaking
- Severity evidence generation
- Coverage and confidence calculation
- World-name resolution and fallback
- 64-bit count and byte accumulation
- Result caps for hotspots and largest records
- File fingerprint comparison
- Cancellation between records and stages

### Database regression tests

- Empty but valid save
- Ordinary new Survival save
- Dense long-running save
- Multiple worlds and warehouse floors
- Save with no optional world metadata
- Older schema missing optional tables
- Modded save with extra tables and unknown UUIDs
- Large blobs
- Malformed optional payload that SQLite can still read
- Failed `PRAGMA quick_check`

For every read-only fixture test:

1. Hash the source before scanning.
2. Scan a copied fixture.
3. Assert expected aggregates and rankings.
4. Hash both source and fixture afterward.
5. Assert neither file changed.

### UI and lifecycle tests

- Real progress reaches completion.
- Cancel leaves ordinary app controls usable.
- Closing during every stage exits promptly.
- Starting a second scan is rejected cleanly.
- Changing the selected world cancels or invalidates the old result.
- Game launch during a scan invalidates the result.
- Unknown text is HTML-escaped.
- Large values do not overflow or destroy card layout.
- Results render in the existing Windows browser engine.

## Acceptance Criteria

The feature is ready when:

- It performs no write statement and leaves the source hash unchanged.
- The game must be closed and file changes invalidate the result.
- The UI remains responsive and cancellation works.
- Memory use is bounded by populated cells rather than raw rows.
- Every hotspot includes world, cell, evidence, percentile, and confidence.
- No result claims to measure FPS or guarantees a lag fix.
- Unknown and modded content degrades to partial coverage without crashing.
- At least five representative fixture types pass.
- A large-save regression completes without loading raw rows into one list.
- Existing raid, dropped-item, repair, patch, and updater regressions still
  pass unchanged.

## Recommended First Release

Ship the first version with:

- Read-only storage and supported-table totals
- Per-world summaries
- Harvestable and loose-pickup density by cell
- Raid-related world totals
- 3-by-3 neighborhood hotspot ranking
- Top 20 hotspot cards
- Transparent evidence and confidence
- Background progress and cancellation

Do not wait for every Scrap Mechanic record family before shipping. A smaller,
well-labeled scanner with high-confidence results is more useful than a broad
scanner that guesses at unknown save data.
