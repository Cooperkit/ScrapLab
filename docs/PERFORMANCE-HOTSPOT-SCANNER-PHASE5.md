# Performance Hotspot Scanner: Phase 5 Export and Explorer Reuse

## Outcome

Phase 5 is complete. A successful performance scan can now be exported as a
small, versioned JSON report, and its retained aggregate cells can be inspected
through a bounded local World Explorer interface.

The normal completed-operation response still contains no full cell list. Cell
data crosses the browser bridge only after an explicit local request, one page
at a time.

The complete implementation plan and all Phase 0 through Phase 4 records were
reread before Phase 5 work began.

## Privacy-safe report contract

**Export JSON** opens a normal Windows save dialog. The default filename is:

```text
ScrapLab-Performance-Report-v3.json
```

The report uses its own contract version independently from the scanner result
version:

```text
Format: scraplab-performance-report
FormatVersion: 1
ScannerVersion: 3
```

The default report contains only:

- ScrapLab app version;
- scanner and report-format versions;
- UTC export time;
- Scrap Mechanic save version;
- aggregate database-size, record, payload, world, cell, and hotspot totals;
- decoded world summaries;
- supported category summaries;
- ranked hotspot coordinates, evidence, confidence, and category totals;
- coverage totals and warnings.

It deliberately excludes:

- source paths and save filenames;
- Windows usernames and Steam IDs;
- raw SQLite blobs or payload fields;
- internal database row IDs;
- complete per-cell collections;
- largest-record diagnostics;
- schema-layout and unsupported-table names;
- inventories or unrelated save content.

Path-like world display names are replaced with the anonymous `World <id>`
fallback before serialization. Report creation is rejected unless the scan
completed successfully and its source fingerprint remained unchanged.

The save operation writes UTF-8 JSON without a byte-order mark. The browser
receives only success, cancellation, a privacy-safe error, and the chosen base
filename; it does not receive the destination directory.

## World Explorer reuse contract

The completed scan stays in the host process. The new local bridge method is:

```text
GetPerformanceWorldCells(operationId, worldId, offset, limit)
```

It returns only aggregate `PerformanceCellSummary` values for one proven world:

- cell coordinates and approximate world center;
- supported record and payload totals;
- decoded category totals.

Safety and performance boundaries:

- the operation ID must identify the current successful completed scan;
- an old, replaced, running, cancelled, failed, or unknown operation fails
  closed;
- the world ID must be present in the completed report;
- negative offsets normalize to zero;
- every request is capped at 250 cells even if the caller asks for more;
- returned cells and category objects are copies, so browser-side mutation
  cannot alter the retained scan result;
- pages use the scanner's deterministic world/cell-coordinate ordering;
- no raw database rows, blobs, UUIDs, paths, or filenames are returned.

The current interface requests 25 cells per page and provides world selection,
previous/next controls, coordinates, world centers, record totals, payload
sizes, and category breakdowns. This is intentionally a compact aggregate-cell
explorer rather than a terrain map or an FPS heatmap.

## Comparison decision

Scan-to-scan comparison was considered and deliberately deferred.

The new export format is only at version 1, the scanner allowlist can grow, and
coverage denominators may legitimately change when a new family is proven. A
comparison feature shipped now could label an allowlist expansion as world
growth or treat changed ranking thresholds as a performance regression.

A future comparison must first define:

- compatible report and scanner-version pairs;
- category additions and coverage changes;
- world identity without local paths or personal identifiers;
- cells that appear or disappear because decoding improved;
- honest wording that compares stored density rather than measured FPS.

Deferring comparison preserves the meaning of the first export contract.

## Regression coverage

`tests/PerformancePhase5Regression.ps1` proves:

- deterministic JSON for a fixed scan, app version, and UTC time;
- correct format, scanner, app, and save versions;
- exact aggregate, world, category, hotspot, and coverage values;
- exclusion of source paths, filenames, raw-payload fields, full cells,
  largest-record diagnostics, schemas, and unsupported-table names;
- replacement of a deliberately path-like world display name;
- export rejection without a current successful operation;
- negative-offset normalization and the hard 250-cell page cap;
- a 289-cell world returns 250 cells followed by 39 cells with correct
  continuation flags;
- unknown worlds and stale or replaced operation IDs fail closed;
- mutating a returned page cannot change the host's retained result;
- the UI and browser bridge include both export and explorer controls;
- source hashes remain unchanged throughout export and paging.

All Phase 0 through Phase 4 scanner, ranking, lifecycle, JavaScript, updater,
companion-security, adaptive-patch, and crop-release regressions remain part of
the final compatibility pass.

Run the Phase 5 regression:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File tests\PerformancePhase5Regression.ps1
```

## Phase 5 exit decision

The Phase 5 exit condition is satisfied:

- users can export a useful report without exporting private source details;
- the export contract is independently versioned;
- the retained aggregate cells are reusable through a bounded local interface;
- the normal browser status result remains bounded and omits all cells;
- malformed requests and stale operation IDs fail closed;
- no export or explorer action writes to the selected save;
- the interface continues to describe density evidence rather than FPS;
- comparison was evaluated and deferred with explicit compatibility reasons.

No later scanner phase is defined in the current plan.
