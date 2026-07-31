# Performance Hotspot Scanner: Phase 3 Ranking and Presentation

## Outcome

Phase 3 is complete. ScrapLab now converts the proven Phase 1 Harvestable
cell aggregates into a bounded list of potential performance hotspots. Every
displayed severity contains serialized evidence and thresholds that explain
why it was assigned.

Phase 4 has not started. Bodies, creations, shapes, units, loose pickups,
containers, arbitrary script payloads, and mod-defined records are not guessed
or added to cell rankings.

The full implementation plan was reread from beginning to end before Phase 3
work began.

## Ranking policy

`source/Performance/PerformanceHotspotRanker.cs` performs deterministic ranking:

1. Group populated cells by decoded world.
2. Calculate record and payload totals for the centered 3-by-3 neighborhood
   around every populated cell.
3. Rank neighborhood record count and payload bytes separately inside each
   world.
4. Combine those percentiles as 65% record density and 35% stored bytes.
5. Apply a conservative absolute evidence floor.
6. Assign a transparent severity.
7. Sort deterministically and collapse overlapping 3-by-3 clusters.
8. Compare the remaining per-world candidates and retain at most 50 cards.

An overlap is suppressed when two candidate centers are within two cells on
both axes, because their centered 3-by-3 neighborhoods intersect. The stronger
candidate survives; exact ties use center-cell records, center bytes, and
coordinates in stable order.

The result retains both center-cell and neighborhood metrics. Displayed card
totals are explicitly labeled as neighborhood totals.

## Calibrated thresholds

A candidate must first satisfy both:

- a combined world percentile of at least 90%; and
- either:
  - at least 24 records in its neighborhood; or
  - at least four records and 256 KiB of supported payload data.

Strong absolute signals are:

- at least 500 neighborhood records;
- at least 1 MiB of neighborhood payload data;
- at least 250 records contributed by three or more populated neighboring
  cells.

Severity is assigned as:

- `NOTABLE`: evidence floor plus combined percentile of at least 90%.
- `HEAVY`: combined percentile of at least 97% plus one strong signal.
- `VERY HEAVY`: combined percentile of at least 99.5% plus two strong signals.

The fixture matrix calibrates all three labels. A 24-record isolated cell is
`NOTABLE`; a 500-record cell is `HEAVY`; and the dense fixture's independent
record and byte signals produce one `VERY HEAVY` cluster. Ordinary,
multi-world, legacy, and modded-extra-table fixtures produce no false hotspot.

## Evidence, confidence, and coverage

Every hotspot includes:

- global and per-world rank;
- world ID and decoded world name;
- center cell coordinates;
- proven approximate world-space center;
- center-cell records and bytes;
- 3-by-3 records, bytes, and populated-cell count;
- record, payload, and combined percentiles;
- severity and confidence;
- decoded category totals;
- one or more structured evidence statements with observed and comparison
  values.

The current ranked contributors are all validated Harvestable records with
proven coordinates and category, so their confidence is `HIGH`. Unknown or
world-only rows are not inserted into a fake cell category. Report-level
coverage continues to show decoded supported records divided by all records
considered by the current allowlist.

The scan result version is now `2`, recording the introduction of the ranking
algorithm.

## Presentation

The embedded browser now displays:

- the number of potential hotspots in the summary strip;
- decoded world-filter buttons, including worlds with zero findings;
- ranked cards entering in deterministic order;
- geometric `NOTABLE`, `HEAVY`, and `VERY HEAVY` badges;
- `HIGH`, `PARTIAL`, or `RAW DATA ONLY` confidence text from the result;
- neighborhood, center-cell, payload, cell-coordinate, and world-center
  metrics;
- a plain-language within-world comparison;
- every serialized evidence statement;
- horizontal decoded-category bars;
- a **Copy World Center** action through the WinForms clipboard bridge;
- accessible all-world and per-world empty states;
- a limitation stating that database density is not an FPS benchmark.

All save-derived text is passed through the existing HTML escaping helper.
The interface uses flex layout, CSS bars, and simple effects supported by the
existing Windows browser engine.

## Regression coverage

`tests/PerformanceHotspotRankingRegression.ps1` proves:

- exact centered 3-by-3 aggregation;
- overlapping-cluster suppression;
- deterministic center selection and tie-breaking;
- all three severity labels;
- evidence completeness and threshold satisfaction;
- high confidence for fully decoded contributors;
- proven coordinate conversion;
- decoded category totals;
- 64-bit record accumulation;
- global and per-world ranks;
- the 50-card cap;
- identical output across repeated ranking.

The generated database scanner regression proves that:

- the dense 50,576-row fixture produces exactly one Overworld hotspot at cell
  `(3, -2)`;
- its neighborhood total is 50,016 records;
- it is `VERY HEAVY` with at least three evidence statements;
- the other four fixture types produce no hotspot;
- deterministic JSON and source hashes remain unchanged.

The lifecycle test proves ranked hotspots and evidence cross the browser
bridge while the unbounded cell list does not. Embedded JavaScript syntax,
Phase 0 research, Phase 1 scanning, cancellation, updater, companion security,
adaptive patching, and crop-release regressions remain green.

The existing dropped-item regression still requires its mandatory external
`SourceSave` fixture and was not changed by Phase 3.

Run the Phase 3 regression:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File tests\PerformanceHotspotRankingRegression.ps1
```

## Phase 3 exit decision

The Phase 3 exit condition is satisfied:

- every displayed severity traces to serialized percentile and absolute
  evidence;
- ranking is separate inside each world and deterministic across worlds;
- boundary-spanning density is represented by 3-by-3 neighborhoods;
- overlapping neighborhoods do not create duplicate cards;
- the browser receives at most 50 hotspot cards and no raw per-cell list;
- unknown record families remain unknown;
- the UI provides world filtering, evidence, confidence, category summaries,
  copyable proven coordinates, and honest limitations.

Before Phase 4 starts, reread the complete implementation plan from beginning
to end.
