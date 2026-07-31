# Changelog

## 2.0.0

- Rebranded Raid Rescue as **ScrapLab**, a broader Scrap Mechanic Survival
  world viewer, save-maintenance toolkit, performance scanner, and optional
  mod workshop. Raid recovery remains available as one focused feature.
- Renamed the portable programs to `ScrapLab.exe`,
  `ScrapLab.PatchHelper.exe`, and `ScrapLab.Updater.exe`, refreshed Windows
  product metadata, and added a new industrial world-inspection application
  icon and in-app emblem.
- Added a non-destructive first-start migration from
  `%LOCALAPPDATA%\Raid Rescue` to `%LOCALAPPDATA%\ScrapLab` for preferences,
  active patch receipts, and verified game-script backups. Legacy data is not
  deleted or overwritten.
- Added the read-only **Performance Hotspot Scanner** with real background
  progress, cancellation, source fingerprinting, Harvestable and Unit
  coverage, evidence-backed cell ranking, bounded World Explorer paging, and
  privacy-safe `ScrapLab-Performance-Report-v3.json` export.
- Renamed new save backups to `.scraplab-backup-...db`; existing Raid Rescue
  backups remain valid restore sources.
- Kept legacy in-game Lua patch identifiers unchanged so already-installed
  secret mods remain detectable, configurable, and removable after the app
  rename.
- Kept update URL validation compatible with both the current
  `Cooperkit/Raid-Rescue` repository and a future `Cooperkit/ScrapLab` rename.
  Installing 2.0 from a 1.x build requires the complete ZIP once; automatic
  updates resume after the transition.

## 1.16.0

- Split automatic replacement into a fixed `RaidRescue.Updater.exe`
  companion. The main app no longer copies itself to a random temporary EXE
  or accepts an internal update-helper command line.
- Split every game-script status and patch operation into
  `RaidRescue.PatchHelper.exe`. The main app no longer contains the patch
  implementation and can no longer relaunch itself elevated.
- Restricted elevated Patch Bay requests to a versioned allowlist over a
  current-Windows-user-only named pipe, verified that the connected process is
  the exact helper Windows started, verified from the elevated side that the
  pipe server is the declared parent process, and bound the session to the
  exact sibling `RaidRescue.exe` parent.
- The fixed updater accepts only same-folder Raid Rescue components, verifies
  GitHub SHA-256 digests, product names, file versions, and matching publisher
  certificates when signed, and rolls the main app and patch helper back
  together on failure.
- Added optional same-certificate Authenticode signing for all three programs
  through `RAID_RESCUE_SIGN_CERT_SHA1`.
- Changed Windows distribution to a complete three-file portable ZIP. Version
  1.16.0 is a one-time manual bundle transition from older single-file builds.

## 1.15.0

- Added **Revival Buff Recovery** to the hidden Super Secret Mods patch bay.
- Players revived with a real Revival Baguette now regain the exact pizza and
  veggie-burger buffs they held when knocked out: maximum health, hammer
  speed, fall protection, and high jump.
- Buff snapshots are isolated per player and persist if a knocked-out player
  disconnects and rejoins before revival.
- Normal respawns and forced revivals explicitly discard the snapshot so buffs
  cannot leak into another life or an unrelated revival.
- Added exact protected-code matching, checksum-verified backups, adaptive
  Steam-update compatibility, surgical removal, cache invalidation, and
  elevated patch-session support for the new mod.

## 1.14.0

- Raid cards now show the decoded world name, matching dropped-item cards,
  instead of exposing the internal numeric world slot.
- Replaced the old destructive **Clear All Raids** workflow with
  **Resolve & Clear Raids**. Raid Rescue now releases the exact live growing
  crops registered to each stored raid before removing the raid-manager record
  in the same SQLite transaction.
- Added detection and backup-first repair for growing crops stranded by an
  older raid clear. Only crops with `hasSurvivedRaid = false` and no active
  raid reference are eligible.
- Added strict crop-storage validation, optimistic row updates, post-write Lua
  verification, final SQLite integrity checks, and a fail-closed repair lock
  when a live crop cannot be proven safe.
- Added regression coverage for one-bit Lua rewriting, active crop release,
  orphan detection and repair, stale references, and malformed crop storage.

## 1.13.0

- Expanded world analysis with a **Dropped Items** scanner for loose Scrap
  Mechanic inventory pickups stored as loot harvestables.
- Added real item names and icons loaded from the installed game's English
  inventory catalogs and 96-pixel icon atlases, with safe fallbacks for
  unknown or modded UUIDs.
- Each pickup card now shows stack quantity, loot type, the decoded world name,
  precise XYZ coordinates, special loot flags, description, and the remaining
  in-game despawn time without exposing internal cell or entity identifiers.
- Dropped-item cards are now ordered by recovery value. Progression and quest
  items rank first, crafted items use Scrap Mechanic's installed recipe
  ingredients, and the full item catalog has stable category fallbacks.
- Loose pickups are now opt-in: the normal raid diagnostic leaves them
  unloaded until **Scan Loose Items** is selected.
- Added a Scrap Mechanic-styled **Item Totals** report with combined
  quantities, stack counts, value tiers, and world-wide summary counters.
- Refined Item Totals with a two-column grid, locked square icon frames, a
  geometry-safe SVG diamond badge, and a draggable custom cyan scrollbar.
- Added a compact header control that collapses or expands every dropped-item
  card while keeping totals and cleanup actions available.
- Replaced static scanner animations with staged percentage progress for
  world analysis, loose-item scans, save cleanup, secret-mod operations, and
  automatic app updates. Bars now finish only when the operation returns.
- Removed cell coordinates and internal entity IDs from pickup-card pills;
  safe cleanup still retains and verifies those identifiers internally.
- Added backup-first **Remove Item** and **Clear All Dropped Items** actions
  with a Scrap Mechanic-styled in-app confirmation.
- Added **Clear Expired** for removing only loose pickups marked
  **Pending World Cleanup** while preserving every active drop.
- Loose-item removal validates the Harvestable-to-ScriptData relationship,
  verifies a timestamped SQLite backup, deletes only the exact paired rows in
  one transaction, preserves raid storage, performs final integrity checks,
  and re-analyzes the edited save.
- Ambiguous, malformed, or undecodable loot is reported and excluded instead
  of being guessed or deleted.
- Secret Mods now track the Steam build for which their generated script
  bundle was activated. After Steam updates the game, intact old patch
  snippets display as **Game Updated - Re-enable** until the user deliberately
  refreshes them; a cache-only refresh does not rewrite unchanged Lua.
- Hidden the legacy cumulative raid/fertilizer hotfix control and its Help
  section now that Scrap Mechanic ships the official raid correction. Offline
  **Clear All Raids** remains available for already-affected saves.
- Locked every Patch Bay switch and its Options control column to matching
  border-box geometry.
- Updated the tutorial, Field Manual, diagnostics wording, and automated
  regression coverage for icon loading, individual removal, clear-all,
  backups, raid preservation, source-save isolation, and Steam-build cache
  reactivation.

## 1.12.0

- Added automatic GitHub update checks shortly after startup and every 30
  minutes while Raid Rescue remains open.
- Added a Scrap Mechanic-styled update console with **Later**, **View Release**,
  and one-click **Update + Restart** controls.
- Update downloads run off the UI thread and require the official
  `Cooperkit/Raid-Rescue` release URL, the `RaidRescue.exe` asset, GitHub's
  SHA-256 digest, and a matching newer executable version.
- Added a temporary self-update helper that waits for Raid Rescue to close,
  atomically replaces the executable, verifies it again, reopens the app, and
  restores the bounded previous-executable backup if installation fails.
- Added manual update checking and the installed version to the Field Manual.

## 1.11.1

- Moved **Developer Commands** above **Resource Locator Dots** in the Super
  Secret Mods catalog.
- Shortened the Patch Bay safety notice while retaining the save-repair and
  rotating-backup guidance.

## 1.11.0

- Added adaptive future-update compatibility for every Super Secret Mod while
  keeping the normal cumulative raid/fertilizer hotfix strictly locked to
  verified game versions.
- Added a known Steam-build catalog for build `24417028`, game version
  `1.0.2.870`, and the existing verified official and Raid Rescue hashes.
- Raid Rescue now reads `appmanifest_387990.acf`, checks the Steam update time,
  and accepts a newer build only when every protected snippet and required Lua
  callback is still an exact structural match.
- Formatting, comments, missing targets, duplicate targets, partial Raid Rescue
  markers, mixed newlines, and changes to protected code are rejected before
  any file is written. Unrelated changes elsewhere in compatible updated files
  are preserved.
- Added preflight generation, dynamic output hashes, byte-preserving UTF-8 BOM
  and LF/CRLF handling, atomic verified writes, and all-file rollback for
  adaptive installs.
- Added bounded active installation receipts recording Steam build, patch
  definition, source/output hashes, file format, and checksum-verified base
  backups.
- Adaptive removal restores the exact pre-install bytes when the installed
  hashes are unchanged. If unrelated edits were made later, Raid Rescue removes
  only its intact snippets; edited, duplicated, or partial patch snippets block
  removal without writing.
- Steam-overwritten secret mods are shown as uninstalled and are never
  automatically reapplied. Superseded active receipts are discarded only after
  Raid Rescue confirms that none of its protected snippets remain.
- Patch Bay now displays **Compatible Game Update**, **Game Update Changed
  Required Code**, **Other Modification Detected**, and **Partial Patch -
  Repair Required** states with a concise affected-file explanation.
- Added isolated future-build regression tests covering unrelated updates,
  host/every-player command modes, linked fertilizer/cannon transactions,
  exact restoration, and rejection of protected changes and same-build manual
  edits.

## 1.10.2

- Fixed installed Lua patches being ignored by normal Scrap Mechanic 1.0.2
  launches because the game continued loading its older generated script cache.
- Raid Rescue now deletes only `Cache\Bundle\core_data.cbo` after a hotfix,
  secret-mod install, removal, dependency change, or option change actually
  modifies verified Lua files.
- Scrap Mechanic rebuilds the cache automatically on the next normal launch, so
  Raid Rescue patches no longer require the `-dev` Steam launch option.
- No-op patch actions leave an existing cache untouched.
- Added clear in-app and README guidance that the first launch after a patch
  change may take a little longer while the cache is rebuilt.

## 1.10.1

- Added isolated regression coverage proving every Super Secret Mod returns
  every affected Lua file to its exact verified pre-install bytes.
- Clarified that Chemical Fertilizer removal preserves the independent normal
  cumulative fertilizer hotfix when it was present before the secret mod.
- Added bounded secret-mod backup retention: the two newest backups for each
  install, remove, or configure action are retained instead of allowing the
  folder to grow forever.
- Retention runs only after successful final checksum verification, never
  removes the current rollback backup, skips reparse points, and ignores every
  folder that does not match an exact Raid Rescue timestamped name.
- Updated Patch Bay messaging, Help, and documentation to explain exact
  restoration and backup rotation.

## 1.10.0

- Added one authenticated elevated patch session shared by the cumulative
  hotfix and every Super Secret Mod.
- Windows now requests administrator approval only on the first patch action
  after Raid Rescue opens. Later toggles reuse the same hidden elevated broker.
- The broker accepts only fixed Raid Rescue patch actions over a randomized,
  token-authenticated named pipe; it cannot execute arbitrary commands or
  accept arbitrary file paths.
- The broker watches its parent Raid Rescue process and exits automatically
  when the app closes.
- Updated in-app progress messages, Help, and documentation for the one-prompt
  workflow.

## 1.9.2

- Fixed Resource Locator Dots launching a second Raid Rescue window during
  elevated installation or removal. Its launcher and helper now agree on the
  three-argument protocol.
- Hardened every elevated patch helper so a recognized but malformed internal
  command exits safely instead of falling through into normal app startup.
- Simplified installed Patch Bay status badges to display only
  **INSTALLED**, while actionable states such as applying, unsupported files,
  missing dependencies, and available updates remain descriptive.

## 1.9.1

- Added an animated **Options** panel to Developer Commands with **Host Only**
  and **Every Player** access modes.
- Host Only remains the recommended default. Every Player registers the
  built-in Survival command list for every joined player while connected;
  `/kick` and `/ban` remain host-only.
- Added a required high-trust acknowledgement before Every Player can be
  installed.
- Added checksum-locked Host Only and Every Player script variants, safe
  in-place switching between them, timestamped verified backups, automatic
  rollback, and exact original-file restoration.
- Updated Patch Bay status, Help, warnings, and documentation to show the
  installed access mode and explain that commands can permanently change a
  world.

## 1.9.0

- Added **Host Developer Commands** to the scalable Super Secret Mods catalog.
- Unlocks Scrap Mechanic's complete existing Survival developer command list
  for the world host, including `/unlimited`, `/god`, `/spawn`, item grants,
  time controls, player utilities, aggro controls, and raid commands.
- Uses `sm.isHost` only for command registration instead of enabling
  `g_survivalDev`, preserving normal spawn points, intro flow, and recipe
  progression.
- Added a Scrap Mechanic-styled installation warning explaining that commands
  can permanently change the active world.
- Added exact-version and checksum locking for `SurvivalGame.lua`, timestamped
  SHA-256-verified backups, atomic replacement, automatic rollback, and exact
  original-file restoration.
- Integrated the new mod with Patch Bay filtering, active counts, game-running
  locks, the master switch, status reporting, Help, and elevated installation.

## 1.8.1

- Redesigned Super Secret Mods as a scalable patch catalog with a fixed master
  control, compact mod cards, live filtering, an independent custom scrollbar,
  and fixed feedback/status controls.
- Added room for future secret mods without allowing the panel to grow beyond
  the fixed Raid Rescue window.
- Added a dedicated creation/save compatibility warning before every operation
  that removes Dual-Fluid Water Cannon: its own switch, removing Chemical
  Fertilizer Splash, or disabling the master switch.
- Removal now requires confirming that every Chemical Container connection was
  removed from mounted water cannons and all affected worlds were saved.
- Documented the same safe-removal requirement for Steam Verify and game
  updates, which can also restore the original two-input cannon script.

## 1.8.0

- Added **Dual-Fluid Water Cannon** to the hidden Super Secret Mods patch bay.
- Mounted water cannons can now accept one logic connection, one Water
  Container, and one Chemical Container in any connection order.
- Each OFF-to-ON logic pulse consumes and fires every available liquid once,
  with both projectiles sharing the same muzzle path and game tick.
- Preserved external-water priority, the original water-only internal tank,
  single-shot triggering, one animation, one sound, and one recoil impulse.
- Added automatic Chemical Fertilizer Splash dependency installation and safe
  cannon-first removal when the fertilizer mod is disabled.
- Added a custom dependency confirmation, one-prompt elevated coordinator,
  timestamped checksum-verified backups, exact uninstallation, and cross-mod
  rollback.

## 1.7.1

- Fixed Resource Locator Dots not appearing in game by declaring the one output
  slot Scrap Mechanic requires before the Connect Tool renders a logic point.
- Kept the locator output inactive and limited it to one child connection.
- Added neutral normal and highlight colors for a clear locator point.
- Added checksum-locked, backup-first upgrades from the older invisible
  Resource Locator Dots patch without requiring Steam Verify.
- Updated the patch-bay status to clearly identify and install the visibility
  update.

## 1.7.0

- Added **Chemical Fertilizer Splash** to the hidden Super Secret Mods patch bay.
- Player chemical projectiles now fertilize the exact normal-soil crop or
  growbed they hit.
- Red Farmbot pesticide impacts now fertilize supported soil, crops, and
  growbeds in a server-authoritative 2.5-block radius.
- Protected directly hit crops and growbeds from the Farmbot projectile's
  normal unit-projectile destruction path while this mod is enabled.
- Added checksum-locked support for official, raid-only, and cumulative
  Raid Rescue script states.
- Added four-file atomic installation, timestamped checksum-verified backups,
  rollback, exact uninstallation, and automatic preservation when the normal
  cumulative hotfix is installed or updated later.

## 1.6.0

- Corrected tutorial badge numbers so they match the visible step labels, and fixed the Step 7 spotlight to frame the Help button inside the custom title bar.
- Added clearly labeled tutorial-only raid data, with Steps 4–6 spotlighting the example raid and its real repair controls instead of the entire empty diagnostics panel.
- Added a hidden animated Super Secret Mods patch bay behind the title-bar emblem, including a persistent master toggle and slots for future experimental patches.
- Added the first Resource Locator Dots patch for haybot spines and refineable
  resource cores. Its zero-slot locator design was corrected in 1.7.1 because
  the game did not render a connection point without an available output slot.
- Rebuilt the secret patch-bay badge as a single SVG coordinate system so its letter and diamond remain precisely centered at every DPI scale.

- Added an optional first-run tutorial prompt with persistent local state.
- Added a nine-step animated interactive tour that spotlights the real
  interface and explains the complete backup-first workflow.
- Added a custom Help menu covering quick start, raid diagnostics, save repair,
  the cumulative hotfix, backups, restoration, antivirus warnings, and common
  problems.
- Added Replay Tutorial and Reset First-Run Prompt controls.
- Added a dedicated animated **?** Help button to the custom title bar.
- Replaced the title-bar question-mark font glyph with a geometrically centered
  SVG so its alignment remains exact at different Windows DPI scales.
- Reduced the title-bar Help icon, rebuilt the tutorial step badge as a fixed
  square SVG, and shortened the tutorial text for faster reading.
- Reworked the tutorial card entrance and replaced the animated step badge with
  a static layered industrial SVG using a dark mount, amber rim, beveled face,
  hard highlight, and lower shadow.
- Removed every step-badge animation and the orbiting square indicator.
- Restored the continuously moving cyan tutorial chevrons using a clipped
  transform layer.
- Removed the animated full-window spotlight shadow and moved its pulse to a
  small signal bar, preventing expensive whole-window repaints.
- Enabled GPU rendering for Raid Rescue's embedded browser through its
  per-user Windows feature control.
- Added eased wheel scrolling to the main interface, Help manual, and save
  list.
- Throttled custom-scrollbar layout work to one update per rendered frame and
  stopped redundant style writes.
- Rebuilt the full-width hazard animation as a composited transform layer and
  pause decorative animations during active scrolling.

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
