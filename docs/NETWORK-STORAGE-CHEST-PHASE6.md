# Network Storage Chest — Phase 6 Qualification

Phase 6 converts the completed development part into a normal ScrapLab Super
Secret Mod. It does not add new inventory behavior; it makes the qualified
part installable, removable, recoverable, and safe to compose with the rest of
the Patch Bay.

## Phase 5 entry gate

The final live Phase 5 run in `game-20260813-233507.log` reported:

```text
[ScrapLab Storage Phase 5] PHASE 5 COMPLETE: 20 passed, 0 failed.
```

The qualification compared the unified player view against real live slots,
including hotbar indices `0-9`, and exercised server-validated staging into the
five-slot deposit tray. A cleanup-only late GUI callback was corrected after
the successful run so the temporary terminal now waits for its close event
before deletion.

## Production installer coverage

`NetworkStorageChestPatchService` performs one preflighted transaction across:

- the shape-set index and custom shape registration;
- the Survival item declaration and native pipe-container list;
- the default-unlocked Craftbot recipe and Recipe Manager registration;
- the isolated terminal Lua, inventory-index Lua, main GUI, item GUI, shape set,
  and localization catalog;
- all 11 shipped inventory-description languages;
- the shared icon XML registration and managed bottom-up atlas tile.

Current known files use verified hashes. Compatible future Steam builds may use
the adaptive path only when every protected snippet and structural guard is
exact and unique. The installer preserves BOM and newline style, creates
SHA-256-verified backups, writes atomically, verifies every output, and rolls
back the whole transaction if one target fails. `core_data.cbo` is removed only
after verified game-file changes.

Removal restores exact backups while installed hashes still match. If unrelated
edits occurred later, it surgically reverses only intact ScrapLab snippets. A
changed, duplicated, or partial ScrapLab snippet blocks removal without writing.
Steam overwrite detection reports **REINSTALL REQUIRED - SAVE PART AT RISK**.

## App integration

The Logistics card exposes installed, applying, game-running, compatible-update,
conflict, partial-patch, unsupported-icon, and reinstall-required states. It is
included in the active-mod count and is removed before other mods in the master
flow. Individual and master removal both require the user to confirm that every
terminal has been emptied and removed from worlds, inventories, containers,
Lifts, and saved creations.

The Field Manual and README document the recipe, catalog, sorting tray, optional
Wireless Vacuum Pipe behavior, Steam-update recovery, and save-safe removal.

## Automated regression

Run from the publication-kit root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\NetworkStorageChestPhase6Regression.ps1
```

The regression creates a disposable future-build Steam fixture and verifies:

- helper action, dispatch, app bridge, Patch Bay card, active count, master
  removal order, danger acknowledgement, and Field Manual text;
- embedded JavaScript syntax when Node is available;
- adaptive clean status and eligibility;
- atomic installation and installed-state detection;
- all six owned production assets;
- verified removal back to the clean fixture.

## Live migration result

The final transaction on 2026-08-14:

- retired the Phase 0–5 development loader, harness files, and active receipt;
- restored the verified pre-development targets before production preflight;
- restored and validated Wireless Vacuum Pipe's pre-Network shared-file receipt;
- patched and verified 23 production targets;
- preserved the Raid Detector and Wireless Vacuum Pipe icon registrations;
- created the bounded `NetworkStorageChest.json` production receipt;
- removed `core_data.cbo` for the next normal launch.

Final helper status reports Network Storage Chest, Wireless Vacuum Pipe, and
Raid Detector installed and intact. Each of their icon UUIDs appears exactly
once, the shared icon state lists all three active mods, and no development
harness loader remains in `SurvivalGame.lua`.

Qualification also found and fixed a shared Craftbot-array ordering issue:
Wireless Vacuum Pipe now validates its exact unique recipe object in place.
A later trusted Network Storage recipe no longer produces a false **PARTIAL
PATCH - REPAIR REQUIRED** state, and the regression covers that composition.

**Phase 6 is complete. Phase 7 is unlocked.**
