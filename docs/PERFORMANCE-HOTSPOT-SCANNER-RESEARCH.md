# Performance Hotspot Scanner: Phase 0 Research

## Outcome

Phase 0 is complete. The first read-only table allowlist, Harvestable cell
semantics, version differences, privacy rules, and generated fixture matrix are
now proven well enough to begin Phase 1.

This work does not add the scanner to the ScrapLab UI. It establishes the
evidence that the scanner implementation must follow.

## Research method

Six local Survival saves were inspected in place with
`tests/InventoryPerformanceSchemas.py`.

The inventory tool:

- opens SQLite with `mode=ro`, `immutable=1`, and `PRAGMA query_only=ON`;
- runs `PRAGMA quick_check`;
- hashes the database, `-wal`, and `-shm` sidecars before and after inspection;
- discards the report if any fingerprint changes;
- emits anonymous sample labels rather than paths or filenames;
- reports schemas and aggregate counts only;
- never emits row values, raw blobs, UUIDs, coordinates, Steam IDs, or Windows
  usernames.

Every sampled save returned `quick_check=ok`, and every before/after fingerprint
matched. No personal save or inventory output is stored in this repository.

## Representative local corpus

The corpus covered both observed save-version families and the scenarios needed
for the first scanner boundary:

| Anonymous profile | Save version | Size | Harvestables | Harvestable worlds | Evidence |
| --- | ---: | ---: | ---: | ---: | --- |
| Current long-running multi-world | 28 | 35.44 MiB | 78,822 | 12 | 14 decoded world descriptors, including 4 warehouses |
| Current new/mod-testing | 28 | 0.95 MiB | 4,114 | 1 | One decoded Overworld descriptor |
| Legacy mod-testing | 26 | 0.46 MiB | 1,640 | 1 | Legacy storage layout |
| Legacy small world | 26 | 1.16 MiB | 5,234 | 1 | Legacy storage layout |
| Legacy long-running world | 26 | 10.64 MiB | 43,906 | 1 | Dense legacy sample |
| Legacy multi-world | 26 | 23.83 MiB | 24,770 | 5 | Multiple legacy world IDs |

Save version 28 is the current/Chapter-2-era layout observed by this research.
The tool does not infer content from that label: current world descriptors are
accepted only when their embedded world ID and compressed payload validate.

## Proven schema differences

### Stable tables and columns

`Harvestable` was identical in all six samples:

```text
id INTEGER
worldId INTEGER
x INTEGER
y INTEGER
size INTEGER
data BLOB
```

The following columns were also present in both version families:

- `Game.savegameversion`
- `Game.gametick`
- `GenericData.worldId`
- `GenericData.data`
- `ScriptData.worldId`
- `ScriptData.data`

### Version 26 to version 28 split

| Table | Version 26 | Version 28 |
| --- | --- | --- |
| `Game` | `savegameversion, flags, seed, gametick, mods` | Adds `uniqueIds` |
| `GenericData` | `uid, key, worldId, flags, data` | `id, channel, worldId, flags, data` |
| `ScriptData` | `uid, key, worldId, flags, data` | `id, channel, worldId, flags, data` |
| Additional current tables | Not observed | `ScriptableObject`, `ShapeGroup`, `VoxelTerrain` |

The scanner must detect one of the two complete, recognized layouts. It must
not assume that `uid`/`key` exists merely because `GenericData` or `ScriptData`
exists.

## First supported allowlist

Only the following identifiers may be queried in the first scanner foundation:

| Table | Allowed columns | Initial use | Confidence |
| --- | --- | --- | --- |
| `Game` | `savegameversion`, `gametick` | Core compatibility and consistent-snapshot metadata | High |
| `Harvestable` | `id`, `worldId`, `x`, `y`, `size`, `data` | Per-world/per-cell counts, payload bytes, and proven decoders | High |
| `GenericData` | `worldId`, `data` | Strictly validated world descriptors under a recognized v26 or v28 layout | High when decoded; fallback otherwise |
| `ScriptData` | `worldId`, `data` | Raw per-world record and payload-byte totals only | Raw data only |

Table names and column names must remain constants in application code.
Capability checks may compare save-provided schema names against these
constants, but save-provided identifiers must never be interpolated into SQL.

Unknown columns are compatible additions. A missing required column disables
only that capability. An unrecognized `GenericData` or `ScriptData` layout is
reported as unsupported rather than guessed.

## Harvestable coordinate semantics

The `Harvestable.x` and `Harvestable.y` columns are cell indexes. They are the
authoritative grouping coordinates for the first scanner.

The axes are reversed relative to the decoded world-position fields stored in
the Harvestable payload:

```text
cellX = floor(worldY / 64)
cellY = floor(worldX / 64)
```

Across 158,486 decoded real rows:

- 158,481 matched that mapping exactly;
- all rows in five saves matched;
- five exceptional rows existed in one current multi-world save;
- the five exceptions were special `size=0` or `size=3` rows;
- a non-swapped interpretation matched only a small incidental subset.

Therefore:

- group and display cells using the database `x` and `y` values;
- do not recompute the cell from payload position;
- if an approximate center is shown, convert database axes explicitly:

```text
worldCenterX = (cellY * 64) + 32
worldCenterY = (cellX * 64) + 32
```

- label the result as an approximate center;
- keep the conversion behind the recognized Harvestable capability;
- treat payload/cell disagreement as an unreadable coordinate detail, not save
  corruption and not a reason to move the record to another cell.

## Phase 4 Unit evidence

Phase 4 repeated the privacy-safe inventory over the same six-save corpus
before broadening the allowlist. `Unit` used the following exact schema in all
six version-26 and version-28 samples:

```text
id INTEGER
worldId INTEGER
x INTEGER
y INTEGER
data BLOB
```

All 7,582 sampled Unit rows followed the same authoritative cell and optional
payload-position relationship already proven for Harvestables:

```text
cellX = floor(worldY / 64)
cellY = floor(worldX / 64)
```

The position uses big-endian single-precision values at payload offsets 40 and
44. Version-26 payloads were 60 bytes; the sampled version-28 payloads ranged
from 65 through 75 bytes. The scanner always groups by the table's `x` and `y`
cell columns. Payload position is only a confidence check, so a short,
unreadable, or disagreeing optional payload remains in its authoritative table
cell and lowers confidence instead of relocating or discarding the row.

The proven Phase 4 addition is deliberately table-level:

| Table | Allowed columns | Scanner category | Confidence |
| --- | --- | --- | --- |
| `Unit` | `id`, `worldId`, `x`, `y`, `data` | Persistent units per world/cell and payload bytes | High when the optional position validates; partial otherwise |

Unit UUIDs and subtypes were not emitted during research and are not guessed by
the scanner. A supported Unit row therefore appears as **Persistent units**,
not as an invented enemy or character name.

The built scanner was also run read-only over all six anonymous local samples.
It counted and decoded all 7,582 Unit rows, reported zero unreadable rows, and
verified every source hash unchanged.

## Deferred record families

Several tables expose plausible world or cell fields, but they are not in the
first allowlist because location semantics or payload families are not yet
proven:

| Candidate | Observed location columns | Phase 0 decision |
| --- | --- | --- |
| `PathNode` | `worldId, x, y` | Defer; path topology may make row density misleading |
| `VoxelTerrain` | `worldId, x, y` | Current-only; defer until payload/storage meaning is proven |
| `Portal` | Two world/cell endpoint sets | Defer; one row represents a connection, not one cell object |
| `RigidBody` | `worldId` | World count only is possible, but body/cell attribution is unproven |
| `ScriptableObject` | `worldId` | Current-only; payload families are unproven |
| `ChildShape`, `Joint`, `ShapeGroup` | Relational IDs | Defer until ownership back to a proven body/cell is validated |
| `Container`, `Controller`, `Tool` | No proven world/cell columns | Do not assign a location |

An extra unknown table in a modded save remains unknown. It is never treated as
corruption, and its payload is not assigned to a fake object category.

## Generated fixture matrix

`tests/GeneratePerformanceFixtures.py` creates six temporary SQLite fixtures:

1. Ordinary current save
2. Dense long-running save with a deliberately crowded cell and large blobs
3. Multi-world save with synthetic Overworld and warehouse descriptors
4. Legacy version-26 storage layout
5. Modded save with an extra unknown table
6. Unit-dense save with 600 validated Unit payloads and one deliberately
   mismatched optional payload

The files are generated under the system temporary directory during the
regression and are never committed. Their Harvestable blobs contain synthetic
positions that exercise positive, negative, multi-world, and axis-swapped
coordinates.

`tests/PerformanceFixtureResearchRegression.py` proves:

- all six fixture profiles are inventoried;
- the first Harvestable capability is detected;
- every generated coordinate follows the proven axis mapping;
- five complete Unit layouts are accepted and one malformed modded Unit layout
  is rejected;
- valid Unit payload coordinates follow the proven mapping while the
  deliberately mismatched optional payload is reported;
- warehouse descriptors are recognized;
- source hashes are unchanged;
- diagnostic JSON contains no source path or filename.

Run it with:

```powershell
python tests\PerformanceFixtureResearchRegression.py
```

## Phase 0 exit decision

The exit condition is satisfied:

- the first supported table and column allowlist is explicit;
- Harvestable cell coordinates and world-center conversion are documented from
  158,486 real rows;
- version 26 and version 28 schema differences are represented;
- ordinary, dense, multi-world/warehouse, legacy, and modded behavior has
  generated regression coverage;
- the inspection path is demonstrably read-only and privacy-safe;
- unsupported families are recorded as unknown instead of being guessed.

Before Phase 1 begins, reread the entire implementation plan. Phase 1 must use
this allowlist and must not silently broaden it.
