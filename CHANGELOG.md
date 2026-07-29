# Changelog

## 1.5.5

- Added a hard safety lock that prevents world databases from being analyzed while Scrap Mechanic is running.
- Disabled world selection, Browse, and Analyze controls while the game process is active.
- Added fresh process checks both immediately before the UI request and immediately before SQLite is opened.
- Controls unlock and the selected world refreshes automatically after the game closes.

## 1.5.4

- Fixed the underlying pseudo-element box-model mismatch that shifted the animated warning diamond by exactly its 3px border width.
- The diamond and geometric exclamation now use the same true 30×30 centered coordinate box.

## 1.5.3

- Replaced font-positioned logo letters with one shared, geometrically centered vector mark.
- Rebuilt the hotfix exclamation from centered shapes so font baselines can no longer shift it.

## 1.5.2

- Centered the warning exclamation mark inside the hotfix confirmation diamond.
- Rebuilt the title-bar emblem to match the layered yellow Raid Rescue logo used in the main interface.

## 1.5.1

- Replaced the generic Windows Yes/No hotfix confirmation with a fully in-app Scrap Mechanic-style warning panel.
- Added animated hazard stripes, warning indicators, a clear safety checklist, and dedicated **CANCEL** / **INSTALL HOTFIX** controls.
- Kept the Windows administrator prompt only where Windows itself requires elevated access to the Steam game folder.

## 1.5.0

- Added a fertilizer growth-timing hotfix for normal soil and growbeds.
- Synchronized client animation with the server-authoritative 20x fertilizer
  multiplier.
- Fertilized ground crops that have completed their timer now mature
  immediately when their raid-survival requirement is released.
- Redesigned the installer as a cumulative updater.
- Existing verified Raid Rescue raid patches are recognized and upgraded
  without requiring Steam verification or reverting the older fixes.
- Backups and rollback now preserve the exact pre-update state, including a
  previous Raid Rescue patch.

## 1.4.3

- Added automatic Scrap Mechanic process monitoring.
- The running-game warning now disappears as soon as the process exits.
- Automatically re-analyzes the selected save and unlocks eligible repair
  controls after the game closes.
- Immediately disables repair controls if the game starts while Raid Rescue is
  open.

## 1.4.2

- Removed UUID browser tooltips from enemy and crop chips.

## 1.4.1

- Added a custom mechanical Raid Rescue application logo.
- Embedded a multi-resolution icon in the executable for File Explorer,
  shortcuts, the taskbar, and the running window.

## 1.4.0

- Added an **Install Raid Hotfix** button beside **Clear All Raids**.
- Added an explicit confirmation before any game files are changed.
- Added a version- and checksum-locked hotfix for Scrap Mechanic 1.0.2.870.
- Added verified game-script backups and automatic rollback on failure.
- Added safe refusal for running games, unsupported updates, and modified scripts.
- Fixed the empty spawn-point, stale crop-reference, and crop reload paths that
  can leave raids permanently active.

## 1.3.1

- Replaced the title-bar emblem with a fixed square SVG.
- Corrected diamond proportions and letter centering.

## 1.3.0

- Added a Scrap Mechanic-inspired custom window bar.
- Added custom minimize and close controls.
- Removed maximize and resize support.
- Added high-DPI awareness.
- Added a custom mechanical scrollbar.

## 1.2.3

- Rebuilt raid-tier badges as fixed square SVGs.

## 1.2

- Redesigned the interface around Scrap Mechanic's visual language.
- Added a custom save picker, larger diagnostic text, and UI animations.

## 1.0

- Initial backup-first raid inspection and repair release.
