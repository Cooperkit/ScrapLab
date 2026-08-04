# Wireless Vacuum Pipe — Phase 5 Production Patch Service

## Result

**Phase 5 is complete — 2026-08-04.** Wireless Vacuum Pipe is now managed by a production ScrapLab patch service instead of the Phase 2–4 development installers. The live Scrap Mechanic build `24529696` (`1.0.5.876`) was migrated successfully: all development harness registrations were removed, the production transaction changed 33 targets, the verified active receipt records all 33 targets, and `Cache/Bundle/core_data.cbo` was removed after the successful write.

The Patch Bay card, app bridge, confirmation flow, Help content, README, and changelog remain Phase 6 work.

## Production service

`WirelessVacuumPipePatchService` owns the complete part installation as one transaction:

- six ScrapLab-owned files: manager, graph adapter, directional transfer service, part script, shape set, and GUI layout;
- item, shape-set, and scriptable-manager registrations;
- the default-unlocked Craftbot recipe and Recipe Manager unlock entry;
- protected adapters for every verified vanilla pipe consumer;
- the shared icon XML registration and binary atlas catalog;
- localized inventory names and descriptions in all 11 shipped languages.

The production shape definition follows the Phase 0 lock: it is visible in the creative inventory and stacks to five. The recipe produces two pipes in 30 seconds from two Vacuum Pipes, two Component Kits, and four Circuit Boards.

The production Crafter adapter excludes Phase 3 diagnostic logging. The Phase 2–4 automatic test harnesses are not embedded or registered by the production service.

## Compatibility model

Known Steam build `24529696` uses verified official hashes for fast recognition. A compatible future build may use adaptive installation only when every protected original snippet and structural guard is exact and unique. The service blocks changed, missing, duplicated, partially installed, or conflicting protected code before any game file is written.

Existing ScrapLab changes are trusted only through their active receipts. This permits composition with installed public mods while still rejecting an unexplained same-build modification.

Status probes distinguish `KNOWN CLEAN`, `KNOWN INSTALLED`, `COMPATIBLE UPDATE`, `ADAPTIVE INSTALLED`, partial or conflicting installation, unsupported protected code, and `REINSTALL REQUIRED — SAVE PART AT RISK` after Steam removes required registrations.

## Atomic safety and removal

Every text, binary, and owned-file output is generated before writing. The transaction:

1. preserves each text file's BOM and newline format;
2. creates SHA-256-verified backups;
3. atomically replaces each target;
4. verifies every output hash;
5. rolls back every changed target if any write or verification fails;
6. restores the previous mod receipt and shared icon state on rollback;
7. stores one bounded active definition receipt after success;
8. removes `core_data.cbo` only after verified game-file changes.

Exact removal restores byte-identical pre-install backups when installed outputs are untouched. Surgical removal removes only intact ScrapLab snippets when unrelated changes were made later. Edited, duplicated, or partially removed ScrapLab snippets block removal without writing.

Partial owned-file sets are rejected. A mixture of present and missing owned runtime files is not treated as clean or installed.

## Shared icon catalog

The shared catalog is now definition 2 and contains both Raid Detector and Wireless Vacuum Pipe icons. The complete known icon pack is written at the bottom of the official atlas, while each mod controls only its own XML registration. This prevents repeated binary atlas edits as future ScrapLab parts are toggled.

The transaction preserves the one shared official-atlas baseline, the active shared state receipt, the backup mirror receipt, and every other active ScrapLab icon and XML entry. Rollback restores the exact pre-operation shared state, mirror receipt, and baseline snapshot.

## Validation

`tests/WirelessVacuumPipePatchServiceRegression.ps1` passed:

- clean known-build install and restart recognition;
- 33-target receipt and owned-file verification;
- creative visibility, stack size, recipe, and 11 languages;
- Raid Detector icon composition;
- exact removal;
- injected failure after every actual write position with complete rollback;
- exact shared-atlas state, mirror, and baseline rollback;
- surgical removal preserving an unrelated edit;
- protected-snippet tamper blocking;
- adaptive future-build install/removal with an unrelated edit;
- Steam-overwrite detection and save-risk status.

Raid Detector and Wireless Vacuum Pipe Phase 1–4 regressions also passed. The older global adaptive regression assumes a completely vanilla live Developer Commands file and cannot run while that independent public mod is installed; this is an existing fixture limitation, not a Wireless Vacuum Pipe failure.

## Developer entry points

- Service: `source/Patching/WirelessVacuumPipePatchService.cs`
- Atomic support: `source/Patching/AtomicCustomPartPatchSupport.cs`
- Shared atlas: `source/Patching/ScrapLabIconAtlasCoordinator.cs`
- Runtime scripts: `source/Patching/Scripts/ScrapLab/PipeSystem/`
- Part files: `source/Patching/Parts/WirelessVacuumPipe/`
- Regression: `tests/WirelessVacuumPipePatchServiceRegression.ps1`
- Controller: `tools/experiments/Manage-WirelessVacuumPipePhase5.ps1`

Phase 6 is unlocked.
