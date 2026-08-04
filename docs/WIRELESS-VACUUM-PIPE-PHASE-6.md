# Wireless Vacuum Pipe — Phase 6

**Completed:** 2026-08-04
**Scope:** ScrapLab UI integration and user documentation

## Outcome

Wireless Vacuum Pipe is now exposed as a complete ScrapLab Patch Bay feature.
The production patch service from Phase 5 is reachable through the restricted
helper protocol and application bridge, and the Patch Bay reports the installed
and compatibility state returned by that service rather than keeping a separate
UI-only state.

## Patch Bay integration

- Added an eleventh catalog card under **LOGISTICS · PIPE AUTOMATION**.
- Added read-only status and elevated install/remove actions for
  `wireless-vacuum-pipe`.
- Added installed, applying, game-running, compatible-update, reinstall-risk,
  partial-patch, unsupported-code, and unsupported-atlas presentation paths.
- Included the mod in filtering, active-mod totals, game-running locks, and the
  Patch Bay master switch.
- Kept elevation inside the existing coordinator, so the app does not spawn a
  second UI process when the switch changes.

## Save-sensitive removal

The individual switch opens a Scrap Mechanic-styled danger panel before removal.
The action remains disabled until the user confirms that every Wireless Vacuum
Pipe was removed from placed worlds (including underground worlds), inventories,
hotbars, containers, Lifts, and saved creations and that every affected world was
saved with the game fully closed.

The Patch Bay master switch performs the same gate before changing any mod. If
the warning is cancelled, no mod changes. When several save-sensitive mods are
installed, all required warnings are collected first; only then does the removal
sequence start, with Wireless Vacuum Pipe removed first. A failure stops the
sequence immediately.

## Field Manual and public documentation

The in-app Field Manual and README now explain:

- the default Craftbot recipe;
- paint-color channels;
- bidirectional Link mode;
- directional Send and Receive modes;
- overworld and underground routing;
- the optional logic input;
- endpoint status and matching-world information;
- local-first discovery, filtering, backpressure, and transaction safety;
- the 64 loaded-cell safety limit;
- Steam-update reinstall states and save-safe removal.

The Unreleased changelog records the runtime, patch-service, UI, and safety work
as one feature. No application version was changed and nothing was published in
this phase.

## Validation gate

`tests/WirelessVacuumPipePhase6Regression.ps1` validates the helper protocol,
bridge, card, category, status loading, state rendering, active count, danger
modal, master-removal ordering, Field Manual, README, changelog, embedded
JavaScript syntax, and live helper status. The normal companion and UI boundary
regressions remain part of the Phase 7 release matrix.

Phase 7 is now unlocked.
