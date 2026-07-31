# Performance Hotspot Scanner: Phase 4 Expanded Record Coverage

## Outcome

Phase 4 is complete. The scanner now includes persistent `Unit` rows as its
second proven cell-located record family. It streams those rows into the
existing per-world, per-cell, category, neighborhood, and ranking model while
preserving the meaning of all Phase 1 through Phase 3 fields.

Unknown tables and malformed optional Unit schemas are reported as unsupported
coverage. They are never queried through a save-provided identifier and never
converted into a fake category.

The full implementation plan, Phase 0 research, and previous phase records were
reread before this phase began.

## Why Unit was selected

The same six-save corpus used by Phase 0 provided one stable Unit layout across
four version-26 and two version-28 saves:

```text
Unit(id, worldId, x, y, data)
```

Across 7,582 real rows:

- every row's table cell matched the swapped-axis payload mapping;
- position values were big-endian floats at payload offsets 40 and 44;
- version-26 payloads were 60 bytes;
- sampled version-28 payloads ranged from 65 to 75 bytes;
- every inspected source fingerprint remained unchanged.

That evidence proves table-level location and payload size. It does not prove
individual Unit UUID or subtype labels, so the scanner exposes one honest
**Persistent units** category instead of guessing character or enemy names.

## Schema and streaming behavior

The Unit capability is enabled only when all five allowlisted columns are
present. Queries remain fixed application constants:

- count supported Unit rows;
- stream `id`, `worldId`, `x`, `y`, and `data` in stable ID order.

Rows are aggregated as they are read. The scanner retains per-cell and
per-world counters, category counters, warnings, and the bounded largest-record
list; it does not materialize the Unit table in memory.

The table's `x` and `y` values are authoritative. The optional payload position
is a confidence check:

- matching position: increments decoded Unit coverage;
- short, unreadable, or disagreeing position: remains in the table cell,
  increments unreadable Unit coverage, and lowers affected hotspot confidence
  to `PARTIAL`.

This prevents an optional payload problem from hiding a known persisted record
or moving it to an unproven location.

## Coverage and ranking

The scan result version is now `3`.

Unit rows participate in:

- stored-record and payload-byte totals;
- world and cell summaries;
- 3-by-3 neighborhood totals;
- category bars;
- largest supported records;
- hotspot ranking and evidence.

A Unit-heavy hotspot receives a structured **Many persistent units** evidence
statement once its neighborhood contains at least 24 Unit rows. Existing
Harvestable evidence and severity thresholds are unchanged. A synthetic
category fallback remains only for direct legacy ranker tests; database scans
always serialize their real contributing categories.

Report-level coverage continues to mean decoded supported records divided by
all records considered by the current allowlist. Unsupported tables are
reported separately so broadening the allowlist does not silently reinterpret
old coverage.

## Unknown and modded schema behavior

The scanner compares `sqlite_master` results against a fixed supported-table
set and returns:

- the number of unsupported tables;
- at most 32 escaped table names for diagnostics and UI display.

If a table named `Unit` exists without the complete recognized layout, Unit
scanning is disabled and `Unit (unsupported layout)` is reported. No dynamic
SQL is built from that table name or any other save-provided identifier.

The UI labels such a result **Partial schema coverage** and continues to show
all proven data.

## Regression coverage

The generated fixture matrix now contains six profiles. The new Unit fixture
contains:

- 600 valid Unit payloads concentrated at cell `(5, 6)`;
- one payload whose optional position disagrees with its authoritative cell;
- one supported Harvestable record.

It proves a `HEAVY`, 601-Unit hotspot with `PARTIAL` confidence and explicit
Unit concentration evidence. The modded fixture contains a malformed Unit
table and another unknown table; both are reported while their rows remain
excluded.

The regressions prove:

- complete Unit capability detection;
- rejection of a malformed optional Unit layout;
- exact streaming counts and bytes;
- exact category decoded/unreadable totals;
- stable authoritative-cell grouping on payload disagreement;
- Unit hotspot evidence and partial confidence;
- unsupported-table reporting;
- deterministic JSON;
- unchanged source hashes;
- asynchronous bridge results remain bounded and omit raw cells.

The built scanner also passed a read-only smoke test against six anonymous real
saves: 7,582 of 7,582 Unit rows decoded, zero unreadable, and all source hashes
unchanged.

## Phase 4 exit decision

The Phase 4 exit condition is satisfied:

- Unit entered the allowlist only after real-corpus evidence and a generated
  fixture existed;
- the fixture and regression gate preceded enabling the family;
- old result fields and severity meanings remain intact;
- malformed optional layouts and unknown mod tables degrade safely;
- row processing remains streaming and memory-bounded;
- the UI explains added coverage without claiming to measure FPS.

Path nodes, voxel terrain, portals, rigid bodies, scriptable objects, shapes,
joints, containers, controllers, tools, and arbitrary payload subtypes remain
deferred. Phase 5 report export and World Explorer reuse have not started.

Before Phase 5 starts, reread the complete implementation plan from beginning
to end.
