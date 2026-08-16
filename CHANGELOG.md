# Changelog

## 2.10.0 - 2026-08-15

- Added Wireless Vacuum Pipe definition 17 after repeated definition-16
  benchmarks isolated Prospectors as the next machine-side hot path. Empty
  input networks and full output networks now honor the game's existing
  0.4-second collection retry delay instead of rescanning the same large pipe
  graph every fixed tick. Inventory contents and native transactions remain
  live, and intact definition-16 installations receive the change through the
  verified one-click update path without replacing their uninstall backups.
- Added Wireless Vacuum Pipe definition 16 after the definition-15 benchmark
  proved that unchanged Vacuum input/output routes were still copied every
  fixed tick. Each Vacuum now retains direction-specific container topology
  for at most five ticks, refreshes immediately when wireless topology changes,
  and continues to read item quantities and perform native transactions live.
  Exact definition-14 and definition-15 installations both migrate through the
  verified one-click update path without replacing their clean uninstall base.
- Added Wireless Vacuum Pipe definition 15 from the caller-attribution
  benchmark. Vacuum Pumps now reuse one input topology snapshot per fixed
  update, briefly cache topology-checked spend sources with live item
  validation, throttle repeated packing-station ray/sphere probes, and skip
  incoming area scans while unpowered. Existing definition-14 installations
  receive the change through the normal verified one-click update path.
- Added Wireless Vacuum Pipe definition 14 after the clean dense benchmark
  isolated repeated full container selection as the remaining cross-world hot
  path. Exact native-plus-wireless results now use a bounded per-caller cache
  with body, topology, direction, and remote-readiness invalidation. Item
  contents and transactions stay live, native ordering is preserved, callers
  receive safe copies, hot lease renewals are throttled, and new diagnostics
  expose cache size, hits, invalidations, and trims.
- Reduced dense Wireless Vacuum Pipe overhead. Directional routes now move a
  bounded item batch per transaction, checkpoint round-robin cursors instead
  of saving after every item, prune inactive channel state, maintain remote
  cell handles on changes and deadlines, share read-only terminal topology
  views, and avoid allocating path-effect players for idle endpoints.
- Bounded Network Storage Chest memory and update cost. The shared inventory
  cache now uses the game-wide clock, expires old entries even when no terminal
  is open, and enforces a hard entry limit. Closing the final viewer releases
  retained server/client catalogs and topology state. Open catalogs update
  changed aggregate records and existing widgets in place instead of rebuilding
  the entire network inventory for quantity-only changes.
- Added safe one-click definition updates for Wireless Vacuum Pipe definition
  16 and Network Storage Chest definition 10. Existing verified owned files are
  recognized by their released hashes and keep their original uninstall bases.

## 2.9.2 - 2026-08-15

- Made Network Storage Chest withdrawals resilient to continuously moving
  Craftbot, pump, and loop-crafting inventories. Definition 9 selects likely
  source chests from the live index, prefers fuller local stacks, and lets the
  native exact-slot transaction—not an unrelated whole-chest revision—decide
  whether a transfer is valid. Brief slot, route, and transaction conflicts now
  retry silently for up to half a second under one UI request, with precise
  final messages if a machine keeps the item busy or the route disappears.
- Fixed Network Storage Chests repeatedly flashing **INDEXING**, rebuilding the
  item grid, and blocking withdrawals while Craftbots, loop crafting, or Vacuum
  Pumps changed connected containers. Definition 8 now coalesces background
  container revisions, suppresses unchanged snapshots, limits changed catalog
  publications, and keeps the last verified catalog interactive. Withdrawals
  re-read only the selected item's live sources and remain atomic, so unrelated
  machine activity no longer makes the terminal difficult to use.
- Fixed Craftbot recipe counts initially showing zero when matching Wireless
  Vacuum Pipe storage was in an unloaded cell or another world. Definition 12
  now reports demand-load readiness and runs a short, bounded GUI refresh until
  the requested cells settle. Retained machine container lists also rebuild on
  topology/readiness changes, without restoring permanent remote simulation.
- Rebuilt secret-mod patch-state lifecycle handling. When Steam Verify removes
  a compatible patch, ScrapLab now retires the stale active receipt before a
  reinstall, preserves only a bounded superseded warning, and creates a fresh
  uninstall receipt from the verified live files. A failed reinstall no longer
  restores obsolete receipt authority, while a successful reinstall clears the
  warning automatically.
- Separated active patch authority from timestamped recovery history. The
  shared custom-icon atlas baseline now lives with active Patch State, legacy
  baselines migrate automatically, and an intact live icon catalog can safely
  reconstruct corrupt or missing managed baseline tiles. Deleting **Game
  Backups** therefore no longer changes mod compatibility or installation
  state.
- Made unreferenced corrupt adaptive base files self-healing while continuing
  to block any checksum failure still referenced by an active receipt. Backup
  rotation now also bounds definition-update and receipt-repair histories.
- Moved Raid Detector installs and removals onto the common atomic custom-part
  transaction path, including shared-state rollback and correct cleanup of
  exact owned files left behind by Steam Verify.

## 2.9.1 - 2026-08-15

- Fixed save-sensitive custom-part mods failing to reinstall after Steam
  verification with **The active adaptive base backup has an unexpected
  checksum**. Adaptive bases are now content-addressed by source hash, so a
  valid older receipt can coexist with a newly restored Steam source until the
  replacement receipt is verified; unreferenced bases are then pruned safely.
- Moved individual and **All Mods** patch transactions off the browser UI
  thread. The Patch Catalog, animations, scrolling, and window controls remain
  responsive while UAC, backup verification, file patching, and rollback run,
  and successful results update the catalog without a second synchronous
  twelve-mod status sweep.

## 2.9.0 - 2026-08-15

- Reworked Wireless Vacuum Pipe performance using the completed dense 64-chest,
  1,152-stack benchmark. Remote endpoint cells now use demand-renewed leases
  instead of remaining simulated merely because colors match; stable physical
  and negative pipe components persist until their bodies change; Network
  Storage Chest terminal routes reuse cached descriptors; and idle directional
  channels back off to occasional source inspections.
- Fixed cross-world directional activity pulses targeting client Shape scripts
  that do not exist in server-only remote cells. Visual pulses are now sent
  only to nearby players in the endpoint's world, while item transactions remain
  fully server-authoritative.
- Fixed directional Wireless Vacuum Pipes so producer machines attached to a
  **Send** endpoint can place output into storage behind matching **Receive**
  endpoints. This includes water pumps, preserves native local-storage
  priority, and respects the receiver's Direct Container Only setting.

## 2.8.0 - 2026-08-15

- Added a native colored-item-type filter to Network Storage Chest. Its new
  default **TYPE** sort groups blocks, interactives, parts, tools, and
  consumables, then alphabetizes each group; quantity and stack sorts now use
  deterministic type/name tie-breakers.
- Rebuilt Network Storage Chest hover text as readable single-line tooltips that
  fit Scrap Mechanic's fixed-height tooltip skin. Removed redundant Backpack and
  Hotbar location labels, and matched the Type button's font size to Sort.
- Expanded the item-type control so long localized labels no longer collide
  with its colored type indicator, and compacted the unified player inventory
  to show occupied hotbar/backpack stacks without rendering empty slots.
- Moved installed and newly added ScrapLab Craftbot recipes into one stable
  group immediately after the vanilla Vacuum Pipe recipe. Existing Network
  Storage Chest and Wireless Vacuum Pipe installs receive one-click definition
  updates without unregistering either save-sensitive UUID.
- Fixed connected multiplayer clients being unable to deposit through a
  Network Storage Chest, especially with cross-world Wireless Vacuum Pipe
  routes. Definition 4 no longer compares a delayed client inventory revision
  against the host; the server re-reads the requested slot and commits the
  player-to-tray move atomically using authoritative inventory state.
- Network Storage Chest deposit results now remain visible briefly instead of
  being immediately replaced by the automatic routing status. Optional routing
  diagnostics also record the player, result, and moved quantity.

## 2.7.1 - 2026-08-14

- Replaced every native and one-off app scrollbar with one ScrapLab machinery
  design: recessed dark rails, illuminated gold thumbs, grip markings, smooth
  wheel input, track jumping, dragging, high-DPI positioning, and reduced-motion
  support across the world selector, Help, Patch Catalog, batch dialogs, loose-
  item totals, and main workspace.

## 2.7.0 - 2026-08-14

- Redesigned the Network Storage Chest deposit strip as a real three-slot tray
  plus a native two-slot-wide routing toggle. Each terminal persists Smart Sort
  or Nearest Empty mode; nearest routing uses same-world physical distance,
  compatible empty-slot checks, cross-world fallback, and deterministic ties.
- Added a lossless five-to-three-slot migration. Occupied legacy trays are
  never resized or hidden; they keep routing safely and migrate only after they
  are empty and the terminal UI is closed. Definition 3 updates the runtime,
  GUI, and localization atomically while retaining original uninstall backups.
- Added a compact **Update** action to every Patch Catalog mod card whenever
  that installed patch reports a newer verified definition. Updates use each
  mod's existing atomic installer; Developer Commands preserve the selected
  Host Only or Every Player access mode.
- Upgraded Network Storage Chest smart deposits with content-learning routing.
  Exact items and native filtered containers remain authoritative, while normal
  chests now develop cached item-family profiles so interactive parts prefer
  other parts instead of unrelated fuel, food, or resource storage. Empty
  storage is preferred over contaminating an unrelated chest.
- Added a verified in-place Network Storage Chest definition-2 update. Existing
  installations keep their original clean-file receipt and save-sensitive part
  registrations while only the two owned routing scripts are replaced and
  checksum-verified. Routing profiles reuse the revision cache instead of
  rescanning every destination slot for every deposit.
- Fixed Network Storage Chest being misreported as **Partial Patch - Repair
  Required** after Steam Verify restored every official registration but left
  ScrapLab's intact owned runtime and UI files. It now shows **Reinstall
  Required - Save Part at Risk**, reinstalls safely, and retains the original
  missing-file ownership so a later removal still deletes those owned files.
- Added a compact three-state **All Mods** control to the Patch Catalog header.
  It installs or updates all 11 compatible gameplay mods through one elevated
  coordinator, reports incompatible skips without blocking independent mods,
  and always leaves Developer Commands under manual control.
- Added a single combined save-sensitive warning for bulk removal, dependency-
  safe ordering, first-failure stopping, per-mod installed/updated/skipped/
  failed results, mixed-state accessibility, game-running lockout, and a
  reduced-motion-safe Scrap Mechanic machinery treatment.
- Redesigned the in-game **Wireless Vacuum Pipe** panel as a centered native
  Scrap Mechanic machine interface. Operation modes now use proper selected
  controls, connection health has a color-coded status lamp, channel/world/
  match information is grouped into a readable dashboard, and Link versus
  directional routing controls no longer leave confusing empty space.
- Added an in-place definition-8 UI update for intact existing Wireless Vacuum
  Pipe installations. The update replaces only verified owned runtime assets;
  it does not unregister the save-sensitive part.

## 2.6.0 - 2026-08-14

- Added the save-sensitive **Network Storage Chest** Super Secret Mod with
  permanent UUID `bc7576a7-f226-459a-883c-e8460e955d63`, a default-unlocked
  Craftbot recipe, isolated runtime and GUI files, all 11 inventory-description
  languages, and an icon managed through the shared ScrapLab Icon Pack.
- Added one searchable, scrollable catalog for reachable local storage,
  server-validated multi-container withdrawals, and a five-slot deposit tray
  that prioritizes partial stacks, matching-item storage, filtered storage,
  and then empty general containers. Hotbar and backpack slots appear together
  in one player-inventory view.
- Added optional Wireless Vacuum Pipe integration for Link and directional
  routes, including qualified cross-world access, without making the wireless
  mod a dependency. Removing Wireless Vacuum Pipe leaves local catalog access
  intact.
- Added an atomic adaptive installer with exact protected snippets, verified
  backups, bounded receipts, rollback, surgical removal, shared-file and
  shared-atlas composition, cache invalidation, Steam-overwrite detection, and
  a save-sensitive removal gate in both the individual and master flows.
- Completed the Phase 5 live qualification with **20 passed, 0 failed** and
  added a Phase 6 production-service regression covering clean install,
  installed-state detection, owned assets, exact removal, helper protocol,
  app bridge, Patch Bay state, danger confirmation, and embedded JavaScript.
- Completed the automated Phase 7 release qualification with **73 passed,
  0 failed, 1 skipped** across the functional suites and **8 passed, 0 failed**
  in the incremental 500-container soak. The soak verified warm shared caches,
  one-container rescanning, refresh persistence, bounded pruning, and removal
  of all 502 temporary fixtures. The temporary test loader was then removed
  and the exact production game loader restored.
- Fixed Wireless Vacuum Pipe falsely reporting a partial Craftbot recipe after
  Network Storage Chest was installed later. Shared recipe arrays now validate
  the exact unique Wireless recipe in place and preserve later ScrapLab recipes
  in either removal order.

## 2.5.1 - 2026-08-05

- Fixed a Craftbot input Link ignoring a same-color Link attached to the
  Craftbot's output chest system. Input resource discovery may now traverse
  that Link and count the complete chest network, while output routing retains
  its one-way loop guard so finished items cannot feed back into the input.
- Fixed Wireless Vacuum Pipe definition 3 failing to load inside Scrap
  Mechanic's restricted Lua runtime because `setmetatable` is unavailable.
  Definition 4 uses bounded ordinary cache tables: graph entries are discarded
  every ten ticks and manager shape entries are removed with their endpoints.
  Intact definition-3 installations receive the correction through the existing
  atomic **UPDATE** action without unregistering the custom part.
- Reworked Wireless Vacuum Pipe graph expansion for dense logistics builds.
  Physical pipe components are cached for a short topology-checked interval,
  shared across connected machines, and scanned without sorting every node's
  neighbours. Repeated same-tick native queries are memoized and networks with
  no applicable wireless route now return through the vanilla fast path.
- Added idle Send/Receive backoff, constant-time active-route checks, cached
  endpoint status, and direct shape-to-endpoint indexing. Empty or full channels
  now retry progressively less often while active transfers retain their normal
  rate.
- Added a safe definition-2 to definition-3 Wireless Vacuum Pipe migration.
  The update atomically replaces only the three verified runtime files, keeps
  the original clean uninstall backups, and restores the complete definition-2
  state if migration fails.

## 2.5.0 - 2026-08-04

- Added Receive-side pull routing to Wireless Vacuum Pipe. Pumps and other
  machines attached to a Receive endpoint can now consume supplies from
  matching Send endpoints, including across worlds.
- Added a persistent per-endpoint transfer-scope option for Send and Receive.
  **Direct Container Only** is the safe default; players may opt into the entire
  attached pipe network when they intentionally want broad draining or filling.
- Added a one-click definition update that upgrades intact existing Wireless
  Vacuum Pipe installations without unregistering their custom UUID or replacing
  their original uninstall backups.
- Fixed Raid Detector incorrectly reporting **PARTIAL PATCH — REPAIR REQUIRED**
  after Wireless Vacuum Pipe added its entries to the shared localization
  files. Custom-part language entries are now validated independently of
  install order, and removing either part preserves the other part's entries.
- Fixed Wireless Vacuum Pipe states after an intentional removal, Steam Verify,
  or an older development-to-production migration left only intact inactive
  ScrapLab-owned files. The patcher now treats verified official registrations
  as clean, safely reuses exact owned assets on reinstall, and guarantees those
  assets are deleted by the next uninstall instead of showing a false icon-atlas
  conflict.
- Fixed Patch Bay compatibility presentation so a partial Wireless Vacuum Pipe
  state cannot be mislabeled solely because its explanation mentions an icon,
  and benign **Verified official Better Plasma Drills files** text is no longer
  displayed as though it were a warning.

- Added the save-sensitive **Wireless Vacuum Pipe** Super Secret Mod with
  permanent UUID `a34d9af0-4ba0-431d-b647-2d5435ecf138`, a default-unlocked
  Craftbot recipe, isolated ScrapLab runtime, localized inventory descriptions,
  and a transparent icon managed by the shared bottom-up icon catalog.
- Added bidirectional same-color **Link** networks and directional **Send** to
  **Receive** transfers across loaded overworld and underground worlds. Native
  local storage remains preferred, transfers preserve filters and backpressure,
  optional logic controls endpoint availability, and remote loading is bounded
  by shared handles and a 64-cell cap.
- Added a 33-target atomic patch service with known/adaptive preflight, verified
  backups, exact and surgical removal, bounded receipts, Steam-overwrite
  detection, cache invalidation, and complete rollback after any failed write.
- Added the Logistics Patch Bay category, Wireless Vacuum Pipe card, helper
  protocol and app bridge, live compatibility states, active-mod counting, and
  Scrap Mechanic-styled progress feedback.
- Added save-sensitive individual and master-switch removal confirmations. The
  master flow warns about Wireless Vacuum Pipe before changing any other mod and
  removes it first only after every required acknowledgement succeeds.
- Added Field Manual and README guidance for crafting, paint channels, Link,
  Send/Receive, cross-world loading, optional logic, route status, backpressure,
  Steam-update recovery, and save-safe removal.

## 2.4.0 - 2026-08-03

- Added the save-sensitive **Raid Detector** Super Secret Mod with permanent
  UUID `a638a8aa-6f4f-41c2-9e31-702687066092`. The beacon-housed part checks
  its current world every ten fixed ticks and outputs logic while a scheduled
  or active raid is within a 256-meter 3D sphere.
- Added a repeatable Hideout Trader purchase costing four Caged Farmers,
  default-unlocked trade registration, isolated ScrapLab part script and shape
  set, and localized inventory descriptions for all 11 shipped languages.
- Added the shared, versioned **ScrapLab Icon Pack** coordinator. Installing the
  first custom-part mod writes every currently shipped ScrapLab icon in one
  pass; later mod toggles change only their XML registrations. Cells are chosen
  from the verified transparent bottom-right of the atlas upward to reduce
  collisions with future official icons. The PNG changes again only for a
  catalog expansion or final custom-part removal, and one bounded baseline and
  catalog receipt avoids duplicating the 11 MB atlas per mod.
- Added atomic 19-target preflight, verified backups, rollback, exact/surgical
  removal, adaptive update handling, cache invalidation, Patch Bay status and
  filtering, Help guidance, and a save-sensitive removal confirmation.
- Added Raid Detector transaction regression coverage, including restart
  detection, owned-file tamper blocking, shared-atlas retention, and byte-exact
  restoration.
- Replaced the Raid Detector's opaque blue icon background with true alpha
  transparency. Existing verified installations expose a one-click icon update
  that changes only the managed 96x96 atlas tile and preserves their original
  clean uninstall backups.
- Fixed the Raid Detector's server-side world lookup. Definition 2 now obtains
  the world through the interactable body, allowing its output to remain on
  throughout both the scheduled countdown and active raid instead of every scan
  failing on Scrap Mechanic's unsupported `Shape.getWorld` member.
- Added a safe definition-1 migration that can install the raid-logic and
  transparent-icon fixes together, changes only verified legacy assets, keeps
  existing detector UUIDs registered, and preserves the original uninstall
  receipt and backups.

## 2.3.0

- Added the independent **Better Freezer & Beehive** Patch Bay mod. Freezers
  accept one direct Water Container with external-first consumption, both
  machines produce four times faster, newly placed machines receive five
  filtered input slots, and finished storage increases to 2,500 ice and 100
  beeswax.
- Added atomic two-script adaptive preflight, verified backups, rollback,
  bounded receipts, exact/surgical removal, update compatibility states, game
  cache invalidation, and persistent-container guidance for the new mod.
- Fixed `/fly` in multiplayer so damage protection, flight movement, and
  anti-ragdoll state apply only to the requesting player. Flight no longer
  changes Scrap Mechanic's global god-mode flag or protects everyone in the
  hosted world, and verified noclip v7 installations upgrade safely to v8.
- Added explicit Better Plasma Drill unit damage of 20, 30, 50, 100, and 300
  damage per second for levels 1-5 while preserving the vanilla unit targeting,
  beam impact, battery use, and mining behavior.
- Added atomic patch-definition upgrades. Intact version-1 Better Plasma Drills
  installations now show **Damage Update Available** and migrate to definition
  2 without removing advanced UUID registrations or losing their original
  uninstall bases.
- Added a dedicated Patch Bay **Update** action plus damage and migration
  guidance in the card, Help page, and README.

## 2.2.0

- Redesigned **Super Secret Mods** as a dedicated full-window Patch Bay rather
  than a small overlay menu.
- Added a Scrap Mechanic-styled workspace header, World Lab return control,
  master patch rail, active-mod counter, catalog search, and filters for
  command, movement, machinery, mining, farming, and survival patches.
- Expanded the catalog into a responsive two-column layout with its own custom
  scrollbar, persistent safety guidance, dependency information, and operation
  feedback while preserving every existing patch, option, confirmation, and
  rollback workflow.
- Kept the redesigned page compatible with ScrapLab's embedded Windows browser
  and high-DPI scaling, including smooth low-cost page and card transitions.

## 2.1.0

- Added the optional, save-sensitive **Better Plasma Drills** workshop mod.
  Plasma Drills now upgrade through levels 4 and 5 with permanent UUIDs,
  stronger speed and battery capacity, 40/75 range, faster voxel updates, and
  beam-radius settings up to 10.
- Added atomic 17-file registration for advanced drills across Lua, shape data,
  casing insertion, icons, and all 11 shipped languages, plus explicit removal
  warnings and Steam-update reinstall detection.

- Added the optional **Better Engines** workshop mod. All 13 Electric Engine
  gears use 10,000 power, while normal and built-in creative level-5 Electric
  and Gas engines use 40,250 battery/fuel points.
- Added a reusable adaptive multi-file mod transaction engine with complete
  preflight, dynamic output checksums, verified backups, atomic replacement,
  rollback, active receipts, exact restoration, and safe surgical removal.
- Added the optional **Full-Speed Carrying** workshop mod. Hand-carried
  objects and Lift-held creations no longer block sprinting, and CarryTool now
  uses Scrap Mechanic's native first- and third-person carry sprint animations.
- Added two-file adaptive preflight, SHA-256-verified backups, atomic writes,
  rollback, exact/surgical removal, build-refresh tracking, and Patch Bay/help
  integration for Full-Speed Carrying.

## 2.0.1

- Added `/fly` to Developer Commands with smooth camera-directed flight,
  collision traversal, ragdoll prevention, god-mode coordination, and faster
  Shift movement. ScrapLab-owned Lua now lives under
  `Survival/Scripts/ScrapLab` and supports verified upgrades from earlier
  noclip implementations.
- Reorganized the source tree by application, companion, patching,
  performance, shared, and world responsibilities without changing the
  portable three-program layout.
- Stopped reporting warnings for modern growing crops that legitimately omit
  obsolete raid-growth storage. Ambiguous, malformed, or actively referenced
  crop storage remains fail-closed.

## 2.0.0

- Stopped reporting one warning per growing crop when its obsolete
  raid-growth storage row is simply absent and no active raid references it.
  Ambiguous or malformed storage still warns, and active raid crops remain
  fail-closed when their required storage cannot be verified.
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
- Added `/noclip` to Developer Commands. It provides camera-directed flight
  through collisions, keeps god mode active while anyone is noclipping,
  restores the previous god-mode state after the last player exits, and
  refuses to place a player back inside solid geometry.
- Upgraded existing Developer Commands installs in place, added exact and
  adaptive runtime detection, and made edited, duplicated, or partial noclip
  code block installation and removal without writing.
- Rebuilt noclip v4 around isolated `Survival/Scripts/ScrapLab` modules and a
  hidden ScrapLab input tool. The Lift script is no longer the primary input
  provider; a delayed Lift hook remains only as a compatibility fallback for
  game builds that fail to instantiate the hidden tool.
- Fixed noclip v5 after live game logs showed that Scrap Mechanic rejects
  physics impulses issued by the world-less `SurvivalGame` GameClass. Flight
  physics now runs from the world-bound `BasePlayer` PlayerClass, while
  `SurvivalGame` retains command, permission, and multiplayer state handling.
- Added an exact, checksum-locked upgrade from the affected v4 module to v5 so
  users can repair the installed mod by enabling Developer Commands once.
- Reworked noclip v6 after live testing exposed spring-like oscillation between
  the vanilla character controller and instant full-strength velocity
  correction. Removed the climbing controller, smoothed acceleration and
  braking, capped every physics correction, and added gravity feed-forward for
  stable hovering without alternating overshoot.
- Added checksum-locked upgrades from both v4 and v5 modules to v6.
- Renamed the flight command from `/noclip` to `/fly` in noclip module v7.
  Normal flight keeps its comfortable existing speed, while holding Shift now
  uses a separately detected sprint state and raises top speed from 20 to 36.
- Added exact upgrades for the v6 controller and its earlier hidden input tool,
  so both scripts update together without requiring mod removal.
- Replaced constant character teleporting with smooth velocity-impulse flight
  in open space. Short authoritative position steps are used only when a
  capsule sweep detects geometry that noclip must cross.
- Suppressed normal falling with the character climbing state and added both a
  per-player tumble guard and a fixed-update safety check so collisions cannot
  leave a noclipping player ragdolled.
- Removed every scripted camera override, control lock, hidden-character mode,
  and stationary anchor. Scrap Mechanic retains ownership of mouse input,
  sensitivity, and inverted-look preferences.
- Added checksum-verified migration from noclip v1, v2, and v3 to v4. The
  installer backs up and restores `tools.json` byte-for-byte, owns only its two
  ScrapLab Lua files, rejects edited assets, and preserves adaptive-patching
  and exact-removal safety.

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
