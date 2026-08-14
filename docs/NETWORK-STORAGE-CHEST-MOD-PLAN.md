# Network Storage Chest Mod — Detailed Implementation Plan

## Status and scope

This document plans a future save-sensitive ScrapLab Super Secret Mod for Scrap Mechanic Survival. It is based on an investigation of Scrap Mechanic `1.0.5.876` / Steam build `24529696`, the shipped chest and GUI scripts, the container transaction API, and ScrapLab's current Wireless Vacuum Pipe graph and manager.

This planning pass does **not** install or implement the mod. It adds only the design document and the prepared inventory icon.

## Executive summary

Add one custom part named **Network Storage Chest**:

- Permanent custom UUID: `bc7576a7-f226-459a-883c-e8460e955d63`
- Reference part: vanilla **Small Chest** with Vacuum Pipe port
- Reference UUID: `4c474cff-3f6a-4306-93d1-c4c74578afd2`
- Reference internal name: `obj_container_smallchest_pipe`
- Source icon: `source/Patching/Parts/NetworkStorageChest/NetworkStorageChestIcon.png`

The custom UUID above is locked and must never change after public distribution.

The part gives the player one interface for every reachable storage container. It shows aggregate item totals, lets the player withdraw an item without hunting for its chest, and automatically sorts deposited items into the best matching destination.

The terminal works in two configurations:

1. **Physical storage network** — reads and transfers through the locally connected Vacuum Pipe system.
2. **Wireless storage network** — when Wireless Vacuum Pipe is installed, follows its Link and directional routes, including verified cross-world routes.

The implementation must never construct a fake container containing copies of network items. The catalog is read-only index data; every actual item remains in its original container until a server-authoritative `sm.container` transaction moves it.

---

## 1. Investigated game behavior

### 1.1 Reference model

The vanilla piped Small Chest is declared in `Survival/Objects/Database/ShapeSets/interactive_shared.shapeset` with these properties:

| Property | Vanilla value |
|---|---|
| UUID | `4c474cff-3f6a-4306-93d1-c4c74578afd2` |
| Size | `3 × 2 × 3` |
| Renderable | `$SURVIVAL_DATA/Objects/Renderable/interactive/obj_interactive_smallchest_pipe.rend` |
| Rotation set | `PropYZ` |
| Vacuum opening | one `+Y` opening at `0, 1, 0` |
| Pipe type | `Container` |
| Vanilla slots | 10 |
| Default paint | `df7f01` |
| Physics material | `Mechanical` |
| Stack size | 5 |

The Network Storage Chest will register a separate shape that references this exact vanilla renderable, size, rotation, opening, ratings, material, and default orange paint. It will not edit the vanilla chest or redistribute its model files.

### 1.2 GUI constraint

Vanilla `Chest.lua` uses `sm.gui.createContainerGui` and binds one real container to `UpperGrid` and the player inventory to `LowerGrid`.

The GUI API can bind a named visible inventory grid to one real container. `setContainers` can expose several containers to a recipe GUI for ingredient counting, but it does not turn them into one draggable visible container. Mirroring several chests into a temporary mega-container would create duplication, stale-data, and crash-recovery risks.

The safe design is therefore:

- a custom paged item catalog made from server-provided UUID/count data;
- a real 5-slot deposit buffer bound to a normal container grid;
- explicit Take controls that perform validated server transactions;
- no copied or client-owned item state.

### 1.3 Container transactions

Scrap Mechanic provides the primitives needed for atomic transfers:

- `sm.container.beginTransaction`
- `sm.container.spend` / `spendFromSlot`
- `sm.container.collect` / `collectToSlot`
- `sm.container.canSpend`
- `sm.container.canCollect`
- `sm.container.endTransaction`
- `Container:getRevision`

ScrapLab has already verified cross-world spend/collect behavior through the Wireless Vacuum Pipe work. The terminal must use the same transaction and loaded-cell rules. If any source, destination, or player inventory changes before commit, the transaction fails as a whole and the UI refreshes.

### 1.4 Wireless Vacuum Pipe integration

The current pipe wrapper already exposes:

- `ScrapLabPipeGraph.getInputContainers`
- `ScrapLabPipeGraph.getOutputContainers`
- `ScrapLabPipeGraph.getLocalPhysicalContainerShapes`
- `ScrapLabPipeGraph.getDirectContainerShapes`
- cached topology revisions and deduplication
- cross-world endpoint loading through `WirelessPipeManager`

The terminal should add a small public query layer instead of duplicating the graph traversal:

- `getTerminalSpendContainers(shape)` — containers the terminal may withdraw from;
- `getTerminalCollectContainers(shape)` — containers the terminal may deposit into;
- descriptors containing container ID, shape, world ID, local/wireless state, and route priority;
- one topology-generation value for stale snapshot detection.

When Wireless Vacuum Pipe is not installed, the terminal falls back to native local `sm.pipeGraph` behavior. It is an optional integration, not a hard dependency.

---

## 2. Player-facing behavior

### 2.1 Opening the terminal

Interacting with the part opens a Scrap Mechanic-styled **STORAGE NETWORK** panel. The header shows:

- unique item types;
- total item quantity;
- reachable container count;
- local or wireless state;
- number of reachable worlds;
- `INDEXING`, `READY`, `NETWORK CHANGED`, `LIMITED`, or `OFFLINE` status.

The terminal indexes only while at least one player has its GUI open or while its deposit buffer changed. It does no continuous whole-network inventory scan while idle.

### 2.2 Item catalog

The baseline catalog uses 24 fixed item cards per page in a 6 × 4 grid. Each card shows:

- the normal localized game icon;
- localized item name;
- total quantity available to withdraw;
- number of source stacks;
- a local/wireless marker when useful.

Selecting a card opens a compact detail strip with:

- **TAKE 1**;
- **TAKE STACK**;
- **TAKE ALL THAT FITS**.

Default sorting is localized A–Z. Alternate controls sort by total quantity and stack count. Previous/Next controls page through large networks.

Phase 0 will probe whether the normal runtime GUI exposes a reliable text-entry callback. If it does, add a Search field. If it does not, ship paging and sort controls rather than using an unstable or nonfunctional text box.

### 2.3 Depositing and automatic sorting

The lower section is a real 5-slot **DEPOSIT TRAY**. The unified player slot grid stages a clicked stack into it through a revision-checked server transaction; compatible external item drags can still target the real tray.

On a deposit-container revision:

1. The server reads the changed slots.
2. It builds a fresh list of allowed destination containers.
3. It ranks destinations with the best-match algorithm below.
4. It moves as much as safely fits in one atomic transaction.
5. Any remainder stays visible in the deposit tray; it is never deleted or silently dropped.

The UI reports `SORTED`, `PARTIAL — DESTINATIONS FULL`, `NO VALID DESTINATION`, or `NETWORK CHANGED — RETRYING`.

The buffer is excluded from its own network index and routing candidates. If the part is broken, its remaining buffer contents use normal chest-drop behavior so deposited items cannot vanish.

### 2.4 Best-matching destination algorithm

For each item UUID, reject the terminal itself, duplicate container IDs, destroyed shapes, inaccessible directional routes, and every container for which `sm.container.canCollect` fails.

Rank remaining candidates by this stable tuple:

1. A native filtered or specialized container that explicitly accepts the item.
2. An existing partial stack of the same UUID, preferring the fullest partial stack to free slots quickly.
3. A container already holding the same UUID, so item types remain grouped.
4. A general unfiltered storage container with free capacity.
5. Local physical route before wireless route when match quality is otherwise equal.
6. Shorter route, then stable container ID, as deterministic tie-breakers.

The planner may split one deposited stack across several destinations. It calculates a bounded allocation, spends only the routed amount from the exact deposit slot, collects each portion, and commits all portions together. A concurrent edit causes the complete transaction to abort and retry from a new snapshot.

Machine-internal buffers and the terminal's own staging buffer are not general destinations. Only normal pipe-eligible storage containers and containers whose native filters explicitly accept the item are eligible.

### 2.5 Withdrawal algorithm

Every request contains only an action, item UUID, and catalog generation. The server does not trust client quantities or container references.

The server:

1. validates the active terminal session, player, distance, world, and request rate;
2. rebuilds or validates the current spend-container set;
3. clamps the requested quantity to the real network total and player capacity;
4. drains smaller source stacks first to free slots, preferring local sources before wireless sources at equal cost;
5. spends across as many source containers as required and collects once into the player inventory in a single transaction;
6. invalidates touched cache entries and returns the new generation.

`TAKE ALL THAT FITS` fills only available player slots. If nothing fits, nothing is spent. A stale UI selection can never create or destroy items.

### 2.6 Wireless mode semantics

The terminal respects the Wireless Vacuum Pipe mode instead of bypassing it:

| Route | Catalog / withdrawal | Deposit destinations |
|---|---|---|
| Physical pipes only | Local input containers | Local output containers |
| Link | Local and same-color linked containers | Local and same-color linked containers |
| Receive-side terminal | Same-color Send source networks are visible | Local/Receive destinations |
| Send-side terminal | Local Send sources are visible | Same-color Receive destination networks |

Cross-world containers are included only while the Wireless manager reports their endpoint cells ready. A handle-limit or unavailable world is shown as a partial-network warning; ScrapLab must not pretend the missing containers are empty.

---

## 3. Performance architecture

The terminal must not rescan every slot in every chest every fixed tick.

Add an in-memory server module named `NetworkInventoryIndex`:

- Cache decoded contents per `sm.container.getId(container)` plus `Container:getRevision()`.
- Reuse one decoded container entry across overlapping terminals and players.
- Expire unused cache entries; persist no item index to the save.
- Invalidate an entry immediately after a successful ScrapLab transaction.
- Rebuild only containers whose revision changed.
- Process a small fixed number of dirty containers per tick so a huge network cannot hitch one frame.
- Aggregate a terminal snapshot from cached per-container maps.
- Send only UUID/count/stack/source metadata, never shape or container userdata, to clients.
- Refresh the open GUI at a throttled cadence and only when its aggregate generation changes.

Topology and content are separate generations:

- topology generation changes when pipe endpoints, paint channels, modes, shapes, or worlds change;
- content generation changes when a participating container revision changes;
- selecting an item or turning a page does not trigger a world rescan.

Release targets:

- no measurable idle cost with every terminal closed and an unchanged deposit buffer;
- no full-network scan more often than necessary;
- stable frame pacing with at least 500 connected chests;
- no duplicate graph traversal for multiple players viewing the same network;
- indexing progress rather than a single-frame stall on very large systems.

---

## 4. Multiplayer and failure safety

- All item totals are informational; the server is authoritative for every transfer.
- Multiple players may open one or several terminals simultaneously.
- Transactions revalidate current contents, so two players cannot withdraw the same stack.
- Each GUI session receives a random server token and expires on close, death, disconnect, part destruction, excessive distance, or world change.
- Requests are rate-limited and accept only UUIDs present in the current server snapshot.
- The terminal must close or disable Take controls while its network is reindexing after a topology change.
- No destination capacity, source count, or client-provided network path is trusted.
- If the Wireless manager unloads a remote endpoint during a request, the transaction aborts without a partial move.
- Deposit-buffer contents survive save/reload and remain manually retrievable if automatic sorting has nowhere to put them.

The core correctness invariant for every test is:

`player quantity + deposit-buffer quantity + reachable source quantity + reachable destination quantity`

must change only by the intended transfer amount and must never change globally.

---

## 5. Part files and icon

Use the established custom-part layout:

- `Survival/Scripts/ScrapLab/Parts/NetworkStorageChest/NetworkStorageChest.lua`
- `Survival/Scripts/ScrapLab/Storage/NetworkInventoryIndex.lua`
- `Survival/Objects/Database/ShapeSets/ScrapLab/Parts/NetworkStorageChest.shapeset`
- `Survival/Gui/Layouts/ScrapLab/Parts/NetworkStorageChest.layout`

The prepared icon is a transparent 96 × 96 RGBA asset. It preserves the orange piped Small Chest silhouette and adds a restrained three-item network cue. It will use ScrapLab's shared bottom-of-atlas icon coordinator:

- allocate a transparent, unused bottom-row cell;
- preserve Raid Detector, Wireless Vacuum Pipe, and future ScrapLab icons;
- modify only the managed tile and its XML registration;
- verify every nonmanaged atlas pixel remains unchanged;
- remove icons independently in any mod order.

The custom item needs titles and descriptions in all 11 shipped languages. The description should state that it browses physically connected storage and also follows Wireless Vacuum Pipe routes when that mod is installed.

Acquisition was locked at the start of Phase 0. The part is a default-unlocked Craftbot recipe producing one Network Storage Chest in 30 seconds from:

- one vanilla piped Small Chest;
- 10 Component Kits;
- 20 Circuit Boards.

Do not make balancing changes while transaction testing is underway.

---

## 6. ScrapLab patch integration

Add a dedicated `network-storage-chest` patch service covering:

- Survival item declaration;
- custom shape-set registration;
- dangerous/save-sensitive object registration where required;
- owned Lua, layout, and shape-set files;
- recipe and unlock registration after acquisition is chosen;
- shared icon XML and atlas tile;
- 11 inventory-description language files;
- optional, composable Wireless Pipe API additions.

The Patch Bay card belongs in **LOGISTICS · INVENTORY** and states:

> Browse every reachable piped container, withdraw items from one catalog, and automatically sort deposits into the best matching chest. Wireless and cross-world access is enabled when Wireless Vacuum Pipe is installed.

Installation requirements:

- preflight every protected target before writing;
- support the current verified build and adaptive future builds only when every protected snippet is exact and unique;
- preserve BOM and newline style;
- generate every output before the first write;
- create SHA-256-verified backups;
- atomically replace and verify all files;
- roll back the complete transaction on any failure;
- update one bounded active receipt;
- delete `Cache/Bundle/core_data.cbo` only after verified changes.

Composition requirements:

- Network Storage Chest works locally without Wireless Vacuum Pipe.
- Installing Wireless Vacuum Pipe later enables wireless access without reinstalling the chest.
- Removing Wireless Vacuum Pipe leaves the chest installed and local-only.
- Installing/removing either mod in any order preserves the other's shared atlas tiles and shared script registrations.
- Steam overwrite detection reports `REINSTALL REQUIRED — SAVE PART AT RISK` rather than claiming the terminal is active.

Because this is a custom UUID with a persistent deposit buffer, removal is save-sensitive. Before disabling it individually or through the master switch, require a Scrap Mechanic-styled confirmation telling users to:

1. empty every terminal deposit tray;
2. remove every Network Storage Chest from worlds, inventories, containers, and lifts;
3. save and exit the affected worlds;
4. confirm **I REMOVED EVERY NETWORK STORAGE CHEST — DISABLE**.

---

## 7. Implementation phases

### Phase 0 — capability spike and locked specification

- Reread this complete plan before starting.
- Lock display name, recipe/trader acquisition, crafting cost, and permanent UUID.
- Build a temporary custom GUI proving:
  - a 60-item JSON catalog using the game's `GridScrollView`, including scrollbar behavior beyond 50 items;
  - compact filtered-result reflow with no fixed-slot gaps;
  - item button callbacks;
  - one real `JsonContainerBox` deposit grid plus one scrollable, slot-accurate player inventory view that includes hotbar slots `0-9` and backpack slots `10+`;
  - localized titles;
  - optional text search callback, or documented paging fallback.
- Prove the custom shape can reuse the piped Small Chest renderable while running its own script and 5-slot buffer.
- Do not proceed until closing/reopening and save/reload preserve buffer contents without duplication.

### Phase 1 — local read-only network catalog

- Add the terminal part and local-only inventory index.
- Enumerate physical input containers, dedupe by container ID, and exclude the terminal buffer.
- Display paged totals and refresh only on revision changes.
- Add no item movement yet.
- Measure 1, 50, 100, and 500-chest indexing and idle performance.

### Phase 2 — local withdrawals *(qualified: 20 passed, 0 failed)*

- Add Take 1, Take Stack, and Take All That Fits.
- Implement session validation, rate limits, stale-generation handling, multi-container spends, and player-capacity clamping.
- Add automated conservation tests and concurrency simulations.

Implementation note: `/slstorage2 auto` creates its own disposable engine-container station and exercises the complete local withdrawal core. See `NETWORK-STORAGE-CHEST-PHASE2-TEST.md`.

### Phase 3 — deposit tray and smart routing *(implemented; awaiting in-game qualification)*

- Bind the 5-slot buffer to the GUI.
- Implement the best-matching ranker, split allocation, partial-capacity handling, retry behavior, and destruction drops.
- Add destination explanations in debug mode so ranking failures can be diagnosed without exposing UUID tooltips in normal play.

Implementation note: `/slstorage3 auto` builds and removes its own filtered/general storage fixture and tests ranking, splitting, leftovers, conflicts, conservation, and buffer exclusion. See `NETWORK-STORAGE-CHEST-PHASE3-TEST.md`.

### Phase 4 — Wireless Vacuum Pipe integration

**Status: COMPLETE — 2026-08-13.** The final self-contained run in `game-20260813-214134.log` reported **18 passed, 0 failed, 1 skipped**. Link, Send, Receive, Direct Container Only, Entire Pipe Network, ready-state reporting, real terminal catalog access, atomic withdrawal/deposit, item conservation, fixture cleanup, and manager invariants passed. The cross-world fixture was skipped because this run had no second world registered with the manager; cross-world container transactions were already qualified by the completed Wireless Vacuum Pipe phases. Definition 7 includes the Link-scope boolean correction found during qualification. Phase 5 is unlocked; see `NETWORK-STORAGE-CHEST-PHASE4-TEST.md`.

- Reread this complete plan and the current Wireless Vacuum Pipe plan/code before changing shared APIs.
- Add terminal spend/collect descriptor queries to `ScrapLabPipeGraph`.
- Verify Link, Send, Receive, direct-only, whole-network, same-world, and cross-world behavior.
- Show ready/limited/offline remote-world states in the terminal.
- Test both mod installation and removal orders.

### Phase 5 — final GUI and localization

- Apply the final Scrap Mechanic storage/mechanics styling.
- Add indexing progress, sort controls, tooltips/detail panel, clear error states, gamepad-safe focus order, and readable small text.
- Add all 11 translations and validate non-English encodings.
- Integrate the prepared atlas icon.

**Status: COMPLETE — 2026-08-13.** The corrected live qualification in `game-20260813-233507.log` reached **20 passed, 0 failed**. The compact 1120 × 540 interface reads every slot from the one limited-inventory container: hotbar slots `0-9` appear first with `1-0` badges, and backpack slots `10+` follow in the same scrollable grid. There is one player-inventory scrollbar, no tabs, no duplicate native inventory boxes, and no flicker-prone engine hotbar overlay. Clicking a non-empty player slot stages its stack through a revision-checked server transaction into the real five-slot sorting tray. The redundant catalog heading, subtitle, catalog count, idle deposit status, and generation status remain removed, and the catalog uses the reclaimed vertical space. Qualification compares every rendered slot's index, hotbar classification, UUID, and quantity with the live player container, so a duplicate or missing hotbar cannot pass. The harness now waits for its asynchronous GUI-close callback before removing its temporary terminal, preventing a cleanup-only invalid script-reference error. Phase 6 is unlocked. See `NETWORK-STORAGE-CHEST-PHASE5-TEST.md`.

### Phase 6 — Patch Bay and adaptive installer

**Status: COMPLETE — 2026-08-14.** The production patch service now owns the permanent registrations, default Craftbot recipe, six isolated assets, shared icon XML/tile, all 11 inventory descriptions, compatibility probing, bounded receipt, exact/surgical removal, cache invalidation, and transactional rollback. The helper protocol, elevated coordinator, app bridge, Logistics card, active count, master-removal order, Field Manual, and explicit save-sensitive removal acknowledgement are connected. The disposable future-build regression proves adaptive clean install, restart-style installed detection, all owned files, and verified removal. The live transaction retired the Phase 0–5 loader and development receipt, patched and verified 23 production targets, preserved the Raid Detector and Wireless Vacuum Pipe shared icons, and created the production receipt. Final status checks report Network Storage Chest, Wireless Vacuum Pipe, and Raid Detector installed and intact. Wireless recipe detection now validates its exact unique object in the shared Craftbot array, so installing Network Storage afterward cannot create a false partial-patch state. Phase 7 is unlocked; see `NETWORK-STORAGE-CHEST-PHASE6.md`.

- Add protocol, helper, elevated coordinator, app bridge, card, active count, master removal order, receipt, compatibility states, and save-sensitive confirmation.
- Add current-build hashes and adaptive protected snippets.
- Test shared-file and shared-atlas composition against every existing ScrapLab custom-part mod.

### Phase 7 — automated release qualification

**Status: COMPLETE — 2026-08-14.** The self-building coordinator completed
the full functional sequence with **73 passed, 0 failed, 1 skipped**. The only
skip was automatic discovery of a second loaded world; the dedicated Phase 4
cross-world qualification had already passed. The incremental soak completed
with **8 passed, 0 failed**, covering 100- and 500-container indexing, warm
cache reuse, one-container rescanning, overlapping-terminal cache sharing,
refresh persistence, bounded pruning, and closed-terminal idle behavior. It
removed all 502 temporary parts. The post-run regression and log scan passed,
and the receipt-backed qualification loader was removed with the exact
pre-test `SurvivalGame.lua` restored. The temporary harness is not embedded in
the public executable.

- Add `/slstorage auto local`, `/slstorage auto wireless`, and `/slstorage soak` test commands that create and remove their own temporary station; normal testing must not require the user to build a setup.
- Run the functional, rollback, adaptive-update, save/reload, cross-world, concurrency, and performance suites.
- Inspect game logs for Lua, GUI, transaction, cell-handle, and atlas errors.
- Rebuild the portable executables, run Defender scanning, and verify the release package remains below 8 MB.

---

## 8. Required test matrix

### Catalog

- Empty network, one chest, many chests, duplicate item UUIDs, full stacks, partial stacks, tools, blocks, consumables, and filtered containers.
- 24, 25, and hundreds of unique item types to verify paging.
- Add/remove/repaint a pipe endpoint while the GUI is open.
- Save/reload with buffer empty and nonempty.

### Withdrawals

- Take 1, one stack, all, player inventory nearly full, and player inventory full.
- One item split across several local and remote containers.
- Source edited between snapshot and click.
- Two players request the same final stack.
- Close GUI, walk away, die, disconnect, or destroy the part during a request.

### Deposits

- Existing partial stack, same-item chest, matching filtered container, empty general chest, all destinations full, and only partial capacity.
- Stable deterministic choice across reloads.
- Large stack split across destinations.
- Concurrent destination edit and failed commit.
- Terminal buffer excluded from routing and catalog.
- Break the terminal with items left in the buffer.

### Wireless

- No Wireless mod installed.
- Link in the same world and across overworld/underground worlds.
- Send/Receive in both terminal directions.
- Direct Container Only and Entire Pipe Network.
- Remote endpoint unload/reload, stale record reconciliation, handle limit, and manager unavailable.
- Install/remove either mod in every order without damaging shared files or icons.

### Performance

- 100 and 500 chests with terminals closed.
- First open indexing, repeated open from warm cache, revision of one chest, and simultaneous viewers.
- Verify only the changed container is rescanned.
- Long soak with cross-world endpoints and repeated deposits/withdrawals.
- Confirm no growing saved cache, receipt, backup, or manager data.

### Patcher safety

- Clean install, exact removal, surgical removal after unrelated edits, Steam overwrite, partial patch, protected-snippet edit, rollback, and cache invalidation.
- Shared atlas pixel comparison with Raid Detector and Wireless Vacuum Pipe installed in every order.
- Save-sensitive individual and master-switch removal cancellation.

---

## 9. Assumptions and decisions still to lock

- **Network Storage Chest** is the locked display name.
- The permanent UUID and piped Small Chest model reference are locked.
- The terminal has one 5-slot real deposit buffer; it is not counted as network storage.
- Every transfer is manual withdrawal or automatic deposit sorting. The part does not continuously rebalance existing chests.
- Wireless integration respects Link/Send/Receive direction and does not create a separate wireless protocol.
- Cross-world access requires Wireless Vacuum Pipe to be installed and ready.
- Search is included only if the Phase 0 GUI callback probe proves it reliable.
- Acquisition is locked to the default-unlocked Craftbot recipe described in section 5.
- No app version bump, GitHub push, or release is part of this planning pass.
