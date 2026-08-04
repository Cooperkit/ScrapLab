# Wireless Vacuum Pipe Mod — Detailed Implementation Plan

## Status and scope

This document is the implementation plan for a future ScrapLab Super Secret Mod. It is based on an investigation of Scrap Mechanic `1.0.5.876` / Steam build `24529696`, the shipped Survival Lua scripts, the engine pipe API, Scrap Mechanic's cross-world manager patterns, and ScrapLab's current custom-part and shared-icon-atlas systems.

This planning pass does **not** modify the game or implement the mod.

## Executive summary

Add one custom part named **Wireless Vacuum Pipe**. It uses the exact world model, collision, dimensions, rotation, and physical pipe openings of vanilla **Vacuum Pipe 1**:

- Permanent custom UUID: `a34d9af0-4ba0-431d-b647-2d5435ecf138`
- Reference UUID: `59ea6ce8-239b-4eed-8847-a51b907d9b42`
- Internal vanilla name: `obj_pneumatic_pipe_01`
- The vanilla UUID is a model reference only. The permanent custom UUID above is locked and must never be changed after public distribution.

The part has three operating modes:

1. **PIPE LINK** — Same-colored Link endpoints become one bidirectional virtual pipe network. This behaves like building a very long pipe and works across different worlds, including the overworld and underground worlds.
2. **SEND** — Takes items from its local physical pipe network.
3. **RECEIVE** — Accepts items from same-colored Send endpoints and places them into its local physical pipe network.

Color is the channel. Painting a part changes its wireless group. Link endpoints only join other Link endpoints; Send endpoints only target Receive endpoints. Range is unlimited inside the current save.

The native `sm.pipeGraph` is engine-owned and local to a loaded world. A custom part cannot create a real cross-world graph edge merely by declaring pipe openings. The correct design is therefore:

- a server-authoritative, game-wide ScrapLab manager;
- stable endpoint records and controlled cell-loading handles for paired endpoints;
- explicit ScrapLab pipe-query wrappers used by the vanilla scripts that consume pipe graphs;
- native container transactions for actual item changes;
- split local visual paths at each wireless endpoint instead of drawing an impossible cross-world path.

Cross-world container commits are a **release gate**. The API describes one transaction as shared across all containers, but the prototype must prove that spending from a container in one loaded world and collecting into a container in another commits atomically and survives save/reload. Transparent cross-world Link mode must not ship until that test passes.

---

## 1. Investigated game behavior

### 1.1 Vacuum Pipe 1

The reference part is declared in:

- `Survival/Scripts/game/survival_items.lua`
- `Survival/Objects/Database/ShapeSets/vacumpipe.shapeset`
- `Survival/Gui/IconMapSurvival.xml`

Its relevant shape properties are:

| Property | Vanilla value |
|---|---|
| UUID | `59ea6ce8-239b-4eed-8847-a51b907d9b42` |
| Base UUID | `9b8f2abd-265c-4750-b8b9-fe6cb564633c` |
| Renderable | `$SURVIVAL_DATA/Objects/Renderable/vacuumpipe/obj_vacuumpipe_pipe1.rend` |
| Collision | `$SURVIVAL_DATA/Objects/Collision/obj_pneumatic_pipe_01.obj` |
| Size | `3 × 1 × 3` |
| Rotation set | `PropYmm` |
| Pipe openings | `+Y` and `-Y` |
| Pipe type | `Pipe` |
| Extendable | `true` |
| Default paint | `df7f01` |
| Physics material | Metal |

Implementation rule: register a separate ScrapLab shape that points to these vanilla assets. Do not edit `vacumpipe.shapeset`, redistribute the renderable, or repurpose the vanilla UUID.

### 1.2 Native pipe topology

`Survival/Scripts/game/util/pipes.lua` contains Lua graph utilities such as `RecursePipedShapeGraph`, but the container discovery used by production machines is primarily exposed through the engine namespace `sm.pipeGraph`.

Important native operations include:

- `getInputContainers`
- `getOutputContainers`
- `getMatchingPipedContainers`
- `getContainerShapeToCollectTo`
- `getContainerShapeToSpendFrom`
- `getContainerPath`
- pipe-lighting functions
- automated pipe task functions used while bodies are unloaded

The engine builds those relationships from physical pipe neighbors in one loaded world. It has no public API for inserting a synthetic edge between arbitrary shapes or different worlds. Replacing `sm.pipeGraph` itself globally would be fragile and could affect unrelated mods, so the plan uses explicit wrappers at protected call sites.

### 1.3 Vanilla scripts that consume pipe graphs

The current shipped Survival code directly uses `sm.pipeGraph` in these active scripts:

1. `Survival/Scripts/game/interactables/Crafter.lua`
2. `Survival/Scripts/game/interactables/FlatVacuum.lua`
3. `Survival/Scripts/game/interactables/GarageChest.lua`
4. `Survival/Scripts/game/interactables/OreCrusher.lua`
5. `Survival/Scripts/game/interactables/Prospector.lua`
6. `Survival/Scripts/game/interactables/Refinery.lua`
7. `Survival/Scripts/game/interactables/Vacuum.lua`
8. `Survival/Scripts/util.lua`

Every relevant query, selection, path, and lighting call must be classified before patching. Query calls will use the ScrapLab wrapper. Native automated-task methods remain local unless testing proves a safe equivalent.

### 1.4 Cross-world patterns already used by the game

The game already persists World objects and uses explicit cell-loading handles for cross-world systems:

- `WorldManager.lua` stores named worlds in manager storage.
- `UndergroundElevatorManager.lua` retains world/floor records, calls `world:loadCellWithHandle(...)`, and releases those handles when they are no longer required.
- `Survival/ScriptableObjects/scriptableObjectSets/sob_managers.sobset` registers long-lived manager scriptable objects.

Wireless pipes should follow that established pattern. An endpoint in an unloaded underground world cannot be queried safely; the manager must hold a load handle for the endpoint cell while that endpoint participates in an active wireless group.

### 1.5 Container transactions

The official API states that `sm.container.beginTransaction()` starts a transaction shared across all containers, followed by `spend`/`collect` operations and `endTransaction()` to commit. That is the right authority boundary for item movement. ScrapLab must never mirror item counts or manually subtract and add outside a transaction.

This documentation does not explicitly guarantee cross-world commits. A real overworld-to-underground test is mandatory before release.

Official references:

- [Scrap Mechanic API: pipe graph](https://scrapmechanic.com/api/namespace_Game_sm_pipeGraph.html)
- [Scrap Mechanic API: containers and transactions](https://scrapmechanic.com/api/namespace_Game_sm_container.html)

---

## 2. Player-facing design

### 2.1 One part, three modes

The same placed part switches between modes through an interaction GUI. Mode changes do not replace the shape and therefore preserve orientation, paint, physical pipe connections, logic connection, and persistent endpoint identity.

#### PIPE LINK

- Default mode.
- All enabled Link endpoints with the same paint color are members of one virtual network.
- Membership is save-wide, not world-local.
- Two or more endpoints form a link; a single endpoint reports `UNPAIRED`.
- The network is bidirectional.
- More than two endpoints form a bus/mesh rather than an ambiguous pair.
- It does not autonomously move items. It expands the container visibility of machines connected to any member, matching the mental model of one long physical pipe.

#### SEND

- Searches its local, native physical pipe network for eligible source containers.
- Transfers only to enabled Receive endpoints with the same paint color.
- Does not connect to Link endpoints or other Send endpoints.

#### RECEIVE

- Exposes its local, native physical pipe network as destinations to matching Send endpoints.
- Does not pull by itself.
- Applies normal capacity and item-filter rules at the destination.

### 2.2 Channel behavior

- The Paint Tool color is the wireless channel.
- The full stable color value, not a localized color name, is stored and compared.
- The script polls for paint changes because the investigated scripts do not expose a reliable server color-change callback.
- Repainting immediately leaves the previous group and joins the new group.
- A color change never transfers or destroys an item in flight; it is applied between completed transaction attempts.

### 2.3 Optional logic control

The custom part has:

- up to one logic input;
- no logic output;
- two normal physical pipe openings inherited from the reference geometry.

Behavior:

- no logic parent: enabled;
- logic parent connected and ON: enabled;
- logic parent connected and OFF: disabled.

This permits automation without forcing every player to wire a switch. A disabled endpoint remains registered but is excluded from routing and releases its remote load requirements when no other active endpoint needs them.

### 2.4 Status and GUI

The interaction panel should match Scrap Mechanic and ScrapLab styling and show only useful information:

- current mode;
- paint/channel swatch;
- enabled/disabled state;
- number of matching endpoints;
- connected world names;
- current routing state;
- compact explanation of the selected mode.

Status labels:

- `UNPAIRED`
- `LINKED`
- `CROSS-WORLD LINKED`
- `SENDING`
- `READY TO RECEIVE`
- `DISABLED BY LOGIC`
- `DESTINATION FULL`
- `CHANNEL EMPTY`
- `REMOTE CELL LOAD LIMIT`
- `WIRELESS MANAGER UNAVAILABLE`

World labels should prefer game metadata:

- `OVERWORLD`
- `UNDERGROUND — DEPTH N`
- known warehouse or dungeon labels;
- a stable generic world identifier only when no friendly metadata exists.

---

## 3. Part and asset specification

### 3.1 Source organization

Use ScrapLab's existing custom-part convention:

```text
source/Patching/Parts/WirelessVacuumPipe/
  WirelessVacuumPipe.lua
  WirelessVacuumPipe.shapeset
  WirelessVacuumPipe.layout
  WirelessVacuumPipeIcon.png
```

Shared runtime code belongs in the existing owned script hierarchy:

```text
source/Patching/Scripts/ScrapLab/PipeSystem/
  WirelessPipeManager.lua
  ScrapLabPipeGraph.lua
  WirelessPipeTransfer.lua
```

Installed destinations:

```text
Survival/Scripts/ScrapLab/Parts/WirelessVacuumPipe/WirelessVacuumPipe.lua
Survival/Scripts/ScrapLab/PipeSystem/WirelessPipeManager.lua
Survival/Scripts/ScrapLab/PipeSystem/ScrapLabPipeGraph.lua
Survival/Scripts/ScrapLab/PipeSystem/WirelessPipeTransfer.lua
Survival/Objects/Database/ShapeSets/ScrapLab/Parts/WirelessVacuumPipe.shapeset
Survival/Gui/Layouts/ScrapLab/Parts/WirelessVacuumPipe.layout
```

### 3.2 UUID policy

- Permanent Wireless Vacuum Pipe UUID: `a34d9af0-4ba0-431d-b647-2d5435ecf138`.
- Phase 0 collision checks found no occurrence in the ScrapLab repository or the installed game's `Survival` and `Data` trees.
- Record it in the source asset, patch descriptor, test fixtures, README, and removal warning.
- Never change it after a public build.
- Never use `59ea6ce8-239b-4eed-8847-a51b907d9b42` as the custom UUID; doing so would replace or conflict with the vanilla pipe.

### 3.3 World appearance

Reuse the reference part's:

- renderable;
- collision;
- size;
- rotation set;
- two pipe openings;
- orange default paint;
- Metal physics material.

Do not add an antenna, screen, radio dish, or other geometry absent from the actual world part. State feedback should use supported UV frame/glow controls and subtle client-only pulses. The normal Paint Tool color remains visibly truthful because it is also the channel.

### 3.4 New item icon

Selected direction: **candidate #1 — wireless signal pulse**. The finalized atlas-ready source is `source/Patching/Parts/WirelessVacuumPipe/WirelessVacuumPipeIcon.png`.

The selected custom 96×96 transparent RGBA icon follows `docs/SCRAP-MECHANIC-CONCEPT-ART-STYLE.md`:

- recognizable Vacuum Pipe 1 silhouette;
- elevated three-quarter Scrap Mechanic inventory angle;
- orange/yellow painted metal, dark steel joints, chunky readable construction;
- restrained paired cyan wireless rings and a small center pulse inside the opening;
- strong padding so the shape is not cropped in hotbar or trader UI;
- transparent pixels outside the object and wireless accent;
- no blue square background, floor, text, badge, photorealism, or tiny unreadable detail;
- no visual hardware that the in-world model does not possess.

Use the existing shared `ScrapLabIconAtlasCoordinator`:

- add the icon to the managed ScrapLab catalog;
- allocate it in a bottom-of-atlas managed cell;
- preserve the existing Raid Detector icon and future ScrapLab icons;
- patch only the XML registration needed for the new UUID;
- keep one bounded shared atlas baseline/receipt;
- verify all pixels outside managed cells are unchanged;
- restore only this icon's cell and XML entry when this mod is removed.

Do not create a second `ItemIcons` group, a separate atlas, or a full atlas replacement.

---

## 4. Runtime architecture

### 4.1 WirelessPipeManager singleton

Register an owned server manager in `sob_managers.sobset`. It must remain alive independently of any particular endpoint or world cell.

Persistent manager storage contains serializable endpoint records only:

```text
endpointId
partUuid
world reference / stable world identity
cellX, cellY
last known position
mode
channel color
enabled state
last confirmed save tick
record format version
```

Runtime-only state contains:

```text
live Shape and Interactable references
cell load handles
group indexes by mode and color
route cache
round-robin cursors
per-group transfer locks
stale-record reconciliation state
```

Use the manager's own `self.storage`; do not claim a generic numeric global storage channel that could collide with another mod.

### 4.2 Endpoint lifecycle

Each part stores a generated stable `endpointId` in its own interactable storage.

On `server_onCreate`:

1. load or create the stable endpoint ID;
2. collect world, cell, position, mode, color, and logic state;
3. register or refresh the manager record;
4. request a group rebuild;
5. publish a compact client status payload.

On a fixed, throttled interval:

- detect paint changes;
- detect logic changes;
- detect cell/world changes from moving creations or elevators;
- refresh the manager record only when state changes;
- avoid per-tick global route rebuilding.

On `server_onDestroy`:

- unregister the endpoint;
- release its load-handle requirements;
- invalidate affected group routes.

Implement `server_onWorldChanged` where the API supplies it, plus the periodic position/world check as a safety net.

### 4.3 Startup reconciliation

Persistent records can outlive a destroyed part if its cell was not loaded during shutdown. Reconcile them safely:

1. load saved records as `UNCONFIRMED`;
2. request their saved cells within the configured handle budget;
3. allow live endpoint scripts to confirm themselves;
4. remove a stale record only after its world and cell loaded successfully and the confirmation timeout elapsed;
5. retain the record and report a load error if the world/cell could not be loaded.

Never delete an endpoint record merely because its remote world was temporarily unavailable.

### 4.4 Cell-loading policy

Cross-world operations require both endpoint cells to be loaded. The manager therefore owns reference-counted cell handles.

Rules:

- unpaired and disabled endpoints do not keep remote cells loaded;
- an active group keeps one handle per unique endpoint cell, not one handle per route;
- multiple endpoints in the same cell share one handle;
- handles are released after a short idle grace period to avoid load/unload thrashing;
- a hard default cap of **64 active endpoint cells** protects memory and simulation performance;
- exceeding the cap disables new remote routes and reports `REMOTE CELL LOAD LIMIT` without transferring anything;
- do not load a 3×3 area around every endpoint unless a targeted test proves the single endpoint cell cannot initialize its local physical network correctly.

The test phase must determine how far vanilla pipes and connected bodies remain usable when only the endpoint cell is held. If the engine requires adjacent cells for a connected creation, load the smallest proven deterministic footprint and revise the cap accordingly.

---

## 5. Virtual graph behavior for PIPE LINK

### 5.1 Why a wrapper is required

A physical pipe does not proactively shuffle inventory. Machines ask the pipe graph for suitable containers when they need input or output. To behave like a long pipe, Link mode must extend those queries. A background broker or hidden mirrored inventory would change vanilla behavior and introduce duplication risks.

### 5.2 ScrapLabPipeGraph interface

Provide owned wrapper methods corresponding to the relevant native queries:

```text
getInputContainers(originShape, ...)
getOutputContainers(originShape, ...)
getMatchingPipedContainers(originShape, ...)
getContainerShapeToCollectTo(originShape, itemUuid, quantity, ...)
getContainerShapeToSpendFrom(originShape, itemUuid, quantity, ...)
getVisualRoute(originShape, destinationShape, ...)
```

If the manager is absent, loading, in conflict, or has no matching endpoint reachable from the origin, each wrapper returns the exact native result.

### 5.3 Discovery algorithm

For a wrapper query originating from a vanilla machine:

1. Run the native local `sm.pipeGraph` query.
2. Discover enabled Link endpoints reachable through the same native physical graph.
3. Ask `WirelessPipeManager` for enabled same-color Link peers anywhere in the save.
4. For each live remote endpoint, run the same native query against that endpoint's local graph.
5. Repeat across additional Link groups reachable on the remote side.
6. Stop cycles with visited keys based on stable endpoint ID plus world/shape identity.
7. Deduplicate containers using `world identity + shape id + container index`.
8. Return a deterministic result.

Ordering:

1. local native containers first;
2. wireless routes by fewest wireless hops;
3. stable endpoint ID order for equal-hop routes;
4. native closest-first order inside each local segment.

This preserves vanilla preference for nearby local storage while making remote storage predictable.

### 5.4 Selection helpers

The wrapper must not assume that a returned remote container can accept or supply an item. Selection uses native validation:

- `sm.container.canCollect(...)` for candidate destinations;
- `sm.container.canSpend(...)` for candidate sources;
- the machine's existing filter and quantity rules;
- the same transaction call path the vanilla script already uses.

No candidate means no operation. Never fall back to deleting, spawning, or buffering an item.

### 5.5 Graph caching

Cache only topology, not inventory capacity or item counts.

Invalidate topology when:

- an endpoint is created/destroyed;
- its mode, color, logic state, world, or cell changes;
- a remote load handle becomes ready or is released;
- the native endpoint shape reference becomes invalid.

Capacity and filter checks remain live for every item operation.

### 5.6 Vanilla call-site integration

Patch the eight investigated scripts with exact, unique transformations:

- replace protected pipe query calls with `ScrapLabPipeGraph` equivalents;
- leave unrelated game logic byte-for-byte unchanged;
- preserve the original argument order and return shapes expected by each caller;
- use a small owned loader rather than copying wrapper logic into each vanilla file;
- leave native automated-task registration/cancellation local unless the prototype demonstrates a safe cross-world extension.

Keeping endpoint cells loaded allows active linked machines to use their normal scripted logic. This is preferable to fabricating cross-world offline tasks inside the engine's local pipe scheduler.

---

## 6. SEND / RECEIVE transfer behavior

Directional mode is intentionally separate from transparent Link mode. It performs an active, server-authoritative transfer.

### 6.1 Source and destination selection

On a throttled server interval:

1. a Send endpoint finds eligible source containers through its **local native** pipe graph;
2. the manager resolves same-color enabled Receive endpoints;
3. receivers are considered in stable round-robin order;
4. each receiver finds eligible destination containers through its **local native** pipe graph;
5. source and destination filters/capacity are checked;
6. one bounded transaction moves the item;
7. the round-robin cursor advances only after a successful transfer.

Multiple Send and Receive endpoints on one color are supported. A stable endpoint-ID order plus a persistent round-robin cursor prevents one receiver from monopolizing the channel.

### 6.2 Throughput

Initial recommended constant:

- attempt once every 4 fixed ticks;
- move one valid inventory unit/stack operation per successful attempt;
- use the smallest quantity accepted by the source/destination contract unless the native vacuum operation already provides an explicit stack quantity.

Keep this as a named server constant so balancing can change without altering transaction correctness. Do not expose a throughput option in the first UI version.

### 6.3 Backpressure and failure

- Full or filtered destinations consume nothing.
- Empty sources consume nothing.
- A disabled or unloaded receiver is skipped.
- If every receiver is unavailable, the sender reports `DESTINATION FULL` or `CHANNEL EMPTY` and waits.
- Failed `beginTransaction` or `endTransaction` calls are retried on a later interval.
- Never retry by duplicating a previously committed collect/spend.

### 6.4 Concurrency

Use one manager lock per channel group plus the container transaction:

- only one Send operation per group is assembled at a time;
- locks are short-lived and always released on errors;
- inventory truth remains in the containers, not the manager;
- route selection is recalculated when a candidate becomes invalid.

Phase 1 proved that a cached Container reference may remain transaction-valid during the same tick in which its Shape is queued for destruction. Production transfer code therefore uses a two-step scheduler guard:

1. select endpoint IDs and candidate routes without retaining a Container as authority;
2. on the following scheduler tick, freshly resolve both endpoint Shapes and Containers from the manager registry;
3. verify the endpoint generation, enabled state, world/cell handle, shape UUID, filters, source quantity, and destination capacity again;
4. begin and finish the transaction in that same callback only after every fresh guard passes.

If either endpoint cannot be freshly resolved, skip the transaction entirely. Never commit through a cached Container obtained before an endpoint-destruction boundary.

---

## 7. Cross-world transaction release gate

### 7.1 Mandatory prototype

Before integrating every machine, build a developer-only probe using two loaded endpoints:

- source container in the overworld;
- destination container in an underground world;
- both endpoint cells held by manager load handles;
- one `beginTransaction` containing source `spend` and remote `collect`;
- verify `endTransaction` result;
- save, exit, reload, and recount both containers.

Required cases:

- normal success;
- destination exactly full after commit;
- destination already full;
- source changes before commit;
- receiver cell unload request during transfer;
- endpoint destroyed during selection;
- save/quit immediately after success;
- simulated script error before and after `endTransaction`;
- host plus connected client observing the same state.

Current Phase 1 development has only one Steam account available. A real connected second client therefore remains an explicit Phase 7 release-validation case. Phase 1 uses a host client→server→client→server loopback with a nonce and exact count comparison as its networking smoke test; this substitution does not replace or waive the eventual two-player release test.

### 7.2 Pass condition

Proceed only if every successful commit subtracts and adds exactly once, every failed commit changes neither container, and the result is identical after reload.

### 7.3 Failure policy

If a single transaction cannot safely span worlds:

- do **not** ship transparent cross-world PIPE LINK mode;
- do not fake shared containers or spawn replacement items;
- retain same-world Link support if it passes;
- directional Send/Receive may use a separately designed durable escrow journal, but only after its own crash-recovery proof.

A possible escrow fallback would persist a transfer record between a verified source spend and destination collect and either complete or refund it after restart. That is a separate implementation and test plan, not an automatic fallback hidden inside this feature.

---

## 8. Effects and client presentation

### 8.1 In-world state

Use only effects supported by the reused model:

- idle/unpaired: no glow;
- linked: low steady glow;
- local activity: short pulse;
- cross-world activity: slightly brighter double pulse;
- disabled/error: no animated pulse.

The exact channel remains visible as the shape's paint color. Effects must be throttled and event-driven to avoid the low-frame-rate UI/runtime problems previously seen in ScrapLab.

### 8.2 Item path effects

A client path cannot interpolate directly between coordinates in different worlds. Render transfer feedback as separate segments:

1. local source container to local wireless endpoint;
2. endpoint pulse/disappearance;
3. remote endpoint pulse/appearance for clients in that world;
4. remote endpoint to local destination container.

Do not send remote world positions to a client and draw a line across coordinate spaces. Clients only receive effects for their current world.

### 8.3 Network payloads

- Server owns endpoint membership and all transfer decisions.
- Client receives compact state enums, channel color, local animation events, and sanitized display counts.
- Never trust a client-provided endpoint ID, world, item UUID, quantity, or container reference.
- Mode-change requests are validated against the requesting player's current interaction with the part.

---

## 9. Game-file patch plan

### 9.1 Owned files to install

- Wireless Vacuum Pipe part script
- custom shape set
- custom GUI layout
- Wireless Pipe Manager
- ScrapLab pipe graph wrapper
- directional transfer helper
- processed transparent icon asset embedded in the helper

### 9.2 Vanilla registrations to patch

- `Survival/Scripts/game/survival_items.lua`
- `Survival/Objects/Database/shapesets.json`
- `Survival/ScriptableObjects/scriptableObjectSets/sob_managers.sobset`
- `Survival/CraftingRecipes/craftbot/craftbot_core.json`
- `Survival/Scripts/game/managers/RecipeManager.lua`
- `Survival/Gui/IconMapSurvival.xml`
- shared `Survival/Gui/IconMapSurvival.png` through `ScrapLabIconAtlasCoordinator`
- all 11 shipped `inventoryDescriptions.json` localization files
- the eight pipe-consuming scripts listed in section 1.3

`vacumpipe.shapeset` is a read-only dependency used for verification and model references; it should not be edited.

### 9.3 Survival acquisition

Phase 0 locks one default-unlocked `craftbot_core` recipe:

- output: 2 Wireless Vacuum Pipes;
- craft time: 30 seconds;
- 2 Vacuum Pipes (`9b8f2abd-265c-4750-b8b9-fe6cb564633c`);
- 2 Component Kits (`5530e6a0-4748-4926-b134-50ca9ecb9dcf`);
- 4 Circuit Boards (`f152e4df-bc40-44fb-8d20-3b3ff70cdfe3`);
- creative inventory entry for testing;
- no Hideout trade.

The two-part output gives the player a usable first link. Placeable parts are valid Craftbot ingredients in the official recipe data, and the base Vacuum Pipe is already default-unlocked.

### 9.4 Known-build catalog

Add verified source hashes for Steam build `24529696`, game version `1.0.5.876`. Investigation-time hashes for protected core targets are:

| Target | SHA-256 |
|---|---|
| `Scripts/game/util/pipes.lua` | `9E494D72BE3CDB8E666F4B1B2AFD34C2105CA2E653468251ABE8D302180F8146` |
| `Scripts/util.lua` | `0F768A843C92003AB6AE722C8475F1C4ED586E48634DE44F309648356F0C0B99` |
| `interactables/Vacuum.lua` | `C4272F5FE215F703EC3F91B2DEFF2729E6F549ABC851D80591163FD06955C446` |
| `interactables/FlatVacuum.lua` | `70E674AE4DB6247C23327DFB826DA87BFC72DD87378A09160D338D1CE2638F2D` |
| `interactables/Crafter.lua` | `486A95F37EF37878296BC776F10D47991E2B6075FDEE777DC531C816855F2D1B` |
| `interactables/Refinery.lua` | `75F008423BC451E3AFB93F0DD1063FEB0015D3B3A5F80DC3C34DC135B8DFF0BE` |
| `interactables/OreCrusher.lua` | `74B237181DDE8D68CBE15685B73F2375969F10FD7E155D56DC4E7F3151F7CE85` |
| `interactables/Prospector.lua` | `BC1C078D77D82C4A55D620787F3C0832AD7CB72A16A64CF27C53811C44AE4279` |
| `interactables/GarageChest.lua` | `D868B7C9D06D776DBF4A037F067C232EE951317C1761D40FD873EC42B4D5C722` |
| `sob_managers.sobset` | `2CFF5DF5D86ACD101914E0C3D3B1A2A25EB715A37A33AE5AE5F90E72B84C2B04` |
| `vacumpipe.shapeset` (read-only) | `139DFFE47D4DC655C39C73CBAEC381AE2F33FD09066E11476830B1920E8E122F` |

Shared registration files may already contain ScrapLab markers from Raid Detector or Better Plasma Drills. Compatibility must be descriptor-based and compositional rather than relying only on these whole-file hashes.

---

## 10. ScrapLab integration and safety

### 10.1 Patch Bay card

Add a save-sensitive card under `LOGISTICS · PIPE AUTOMATION`:

**WIRELESS VACUUM PIPE**
Connect physical pipe networks by paint color—even between the overworld and underground. Includes bidirectional Link and directional Send/Receive modes.

States:

- `NOT INSTALLED`
- `INSTALLED`
- `APPLYING`
- `GAME RUNNING`
- `COMPATIBLE GAME UPDATE`
- `OTHER MODIFICATION DETECTED`
- `PARTIAL PATCH — REPAIR REQUIRED`
- `UNSUPPORTED PIPE CODE`
- `UNSUPPORTED ICON ATLAS`
- `REINSTALL REQUIRED — SAVE PART AT RISK`
- `ROLLBACK FAILED`

Add the mod to:

- Patch Helper protocol;
- elevated coordinator;
- application bridge;
- Patch Bay active count;
- master switch removal order;
- Help page;
- README;
- Unreleased changelog.

### 10.2 Save-sensitive removal

Because the part has a custom UUID, disabling the mod while one exists can damage or make a save unloadable.

Require the confirmation action:

**I REMOVED EVERY WIRELESS VACUUM PIPE — DISABLE**

The warning must tell players to remove it from:

- placed worlds, including every underground world;
- inventories and hotbars;
- containers;
- lifts and saved creations.

The master switch shows the same warning and aborts before removing any other mod if the user cancels or Wireless Vacuum Pipe removal fails.

### 10.3 Atomic patch transaction

Treat every target as one transaction:

1. validate Steam manifest/build metadata;
2. preflight every protected snippet and owned-file state;
3. preflight shared atlas ownership and all localization encodings;
4. generate every output in memory/temp storage;
5. create SHA-256-verified backups;
6. write with atomic replace;
7. verify all output hashes and managed atlas pixels;
8. roll back every changed target if any operation fails;
9. delete `Cache/Bundle/core_data.cbo` only after verified changes.

No target is written until all targets pass preflight.

### 10.4 Adaptive compatibility

- Use exact, unique protected snippets and structural guards.
- Preserve BOM and LF/CRLF style.
- Reject mixed-newline adaptive targets.
- On the known Steam build, block unknown protected states as third-party/manual modifications.
- On a future verified Steam build, allow adaptive installation only if every protected call site and registration remains exact and unique.
- Detect missing, duplicated, partial, or edited ScrapLab wrappers before writing.
- Do not globally replace arbitrary text matching `sm.pipeGraph`; each call site needs a descriptor tied to its containing function and expected occurrence count.

### 10.5 Composition with existing mods

The new patch must preserve and recognize:

- Raid Detector custom UUID, manager, localizations, and shared icon cell;
- Better Plasma Drills registrations and language entries;
- Full-Speed Carrying and other current script patches;
- future ScrapLab icon catalog members.

Shared files are removed surgically. The final shared icon removal restores the byte-exact atlas/XML baseline whenever their current states still match the coordinator receipt.

### 10.6 Receipt and restoration

Store one bounded active receipt containing:

- mod and patch-definition versions;
- Steam build/game version;
- assigned part UUID;
- source/output hashes for every target;
- owned-file hashes;
- backup paths and hashes;
- newline/BOM details;
- shared atlas catalog version and allocated cell;
- manager/script registration markers.

Removal policy:

- exact installed hashes: restore exact pre-install backups;
- unrelated later edits with intact ScrapLab snippets: surgically remove only ScrapLab changes;
- edited/duplicated/partial ScrapLab snippets: block without writing;
- Steam update removed every ScrapLab marker: clear the superseded receipt only after confirming no custom registration remains, then show the mod uninstalled/reinstall-required as appropriate.

---

## 11. Implementation sequence

### Phase 0 — Lock identifiers and balance

**Status: COMPLETE — 2026-08-04.** See `docs/WIRELESS-VACUUM-PIPE-PHASE-0.md` and the machine-readable `WirelessVacuumPipe.phase0.json` lock.

- [x] Allocate the permanent custom UUID.
- [x] Confirm final displayed name.
- [x] Approve survival recipe and cost.
- [x] Approve the recommended optional logic input.
- [x] Create the transparent item icon and verify it at 24, 32, and 96 pixels.

### Phase 1 — Cross-world safety spike

**Status: COMPLETE — 2026-08-04.** The verified run recorded all eight transaction cases, save/reload persistence, and the single-account network loopback as **10 passed, 0 failed** in `game-20260804-021017.log`. Phase 2 is unlocked. The unavailable real connected-client observation remains mandatory before release in Phase 7.

- [x] Implement a temporary manager and two developer endpoints.
- [x] Load overworld and underground endpoint cells with handles.
- [x] Run the cross-world transaction matrix in section 7.
- [x] Lock the capacity and fresh-endpoint-resolution rules learned from the probe.

### Phase 2 — Endpoint and manager

**Status: COMPLETE — 2026-08-04.** The final run recorded **7 passed, 0 failed** in `game-20260804-040620.log`, and the full restart verification repeated **7 passed, 0 failed** in `game-20260804-041446.log`. Endpoint storage, Link/Send/Receive UI state, color and logic grouping, stale-record reconciliation, shared handles, moving creations, cell-boundary movement, Overworld-to-Underground elevator transfer, and save/reload persistence passed. Phase 3 is unlocked; see `docs/WIRELESS-VACUUM-PIPE-PHASE-2.md`.

- [x] Add the owned shape/script/layout.
- [x] Register persistent endpoints.
- [x] Implement color/mode/logic grouping.
- [x] Implement handle reference counting, cap, reconciliation, and world labels.
- [x] Verify endpoint movement through elevators and moving creations.

### Phase 3 — Virtual Link graph

**Status: COMPLETE — 2026-08-04.** The final self-contained run in `game-20260804-154523.log` reported **11 passed, 0 failed, 0 skipped**. The desktop coordinator reported **6 passed, 0 failed, 1 not-applicable Flat Vacuum skip**, and post-run save inspection confirmed that the temporary fixture IDs and cleanup receipt were gone. Endpoint lifecycle, Craftbot same/cross-world flow, Vacuum input/output, restart-without-revisit, multi-Link rollover, deterministic graph safety, and every protected consumer path are validated. The shipped Flat Vacuum script has no registered placeable shape in this build, so gameplay coverage remains explicitly not applicable while its protected code is regression-tested. Phase 4 is unlocked; see `docs/WIRELESS-VACUUM-PIPE-PHASE-3.md`.

- [x] Implement wrappers with exact native fallback.
- [x] Add deterministic traversal, cycle guards, deduplication, and topology cache.
- [x] Validate the first low-risk consumer in game.
- [x] Integrate and validate the remaining native pipe consumers.
- [x] Pass the self-contained in-game and desktop validation gates.

### Phase 4 — Directional transfer

**Status: COMPLETE — 2026-08-04.** Definition 3's final run in `game-20260804-163835.log` reported **10 passed, 0 failed, 0 skipped**. It moved all six water exactly once, split them 3/3 across same-world and cross-world Receivers, passed empty/full backpressure, released its group lock, and preserved manager invariants. The desktop coordinator reported **7 passed, 0 failed, 0 skipped** with no Phase 4 runtime safety errors. Post-run save inspection found `cleanup=ABSENT` and no temporary endpoint registrations. Definition 3 preserves native results first, supplements neutral roots with local-only physical traversal, and has dedicated crash-recovery callbacks that cannot be overwritten by a new run. Phase 5 is unlocked; see `docs/WIRELESS-VACUUM-PIPE-PHASE-4.md`.

- [x] Implement native-local source/destination discovery.
- [x] Add group locks, round-robin scheduling, backpressure, and transaction retries.
- [x] Add split local visual effects.
- [x] Add a self-contained in-game validation fixture and desktop coordinator.
- [x] Pass the in-game automatic validation and verify fixture cleanup.

### Phase 5 — ScrapLab patch service

**Status: COMPLETE — 2026-08-04.** The production service atomically manages 33 targets, including six owned runtime files, every protected pipe consumer, the default Craftbot recipe, all 11 languages, and the shared definition-2 bottom-atlas catalog. Exhaustive failure injection passed after every actual write position, along with exact/surgical removal, adaptive future-build behavior, tamper blocking, Steam-overwrite detection, and shared-state rollback. The live build was migrated from the Phase 2–4 harnesses to the receipt-backed production installation and its Lua cache was invalidated. Phase 6 is unlocked; see `docs/WIRELESS-VACUUM-PIPE-PHASE-5.md`.

- [x] Add known-build hashes and adaptive descriptors.
- [x] Add atomic owned/text/binary file plans.
- [x] Integrate the shared icon coordinator and all 11 languages.
- [x] Add receipt, removal, update-overwrite detection, and cache invalidation.

### Phase 6 — App UI and documentation

**Status: COMPLETE — 2026-08-04.** Wireless Vacuum Pipe is now a first-class
Logistics Patch Bay mod with live helper status, installation/removal controls,
compatibility states, active-mod counting, and focused applying feedback. Its
individual removal and Patch Bay master-disable paths require explicit
save-safety acknowledgement; the master path warns before changing any mod and
removes Wireless Vacuum Pipe first. The Field Manual, README, and changelog now
cover crafting, modes, paint channels, cross-world operation, logic control,
backpressure, update recovery, and safe removal. Phase 7 is unlocked; see
`docs/WIRELESS-VACUUM-PIPE-PHASE-6.md`.

- [x] Add Patch Bay card/status/toggle.
- [x] Add the save-sensitive individual and master removal confirmations.
- [x] Add Help explanations for modes, channels, cross-world loading, and safe removal.
- [x] Update README and changelog.

### Phase 7 — Release validation

**Status: CONDITIONALLY COMPLETE — 2026-08-04.** The fresh dependency-free
bundle, all 18 desktop regressions, 45 recorded single-account gameplay checks,
embedded JavaScript/assets, live production receipt, shared atlas, exhaustive
write-failure rollback, and package boundaries passed. Microsoft Defender found
no threats in either the executables or ZIP, which is 815,259 bytes. The only
unmet strict release gate is the real connected second-player observation from
section 7.1; this machine has one Steam account, and the already-passed loopback
does not waive that requirement. No version was changed and nothing was
published. See `docs/WIRELESS-VACUUM-PIPE-PHASE-7.md`.

- [x] Run the complete automated functional and failure test matrix.
- [x] Audit the completed single-account in-game functional matrix.
- [x] Build dependency-free executables.
- [x] Validate JavaScript/UI assets.
- [x] Run Microsoft Defender scan.
- [x] Confirm package size remains below 8 MB.
- [ ] Observe a cross-world commit from a real connected second client.
- [x] Keep publishing/version bump as separate user-requested actions.

---

## 12. Test plan

### 12.1 Part registration and appearance

- Custom part and vanilla Vacuum Pipe 1 coexist.
- Model, collision, rotation, paint, and both openings match Vacuum Pipe 1.
- The custom UUID remains stable across reinstall/update.
- Icon has transparent background in inventory, hotbar, recipe, handbook, and dropped-item UI.
- Only the managed bottom atlas cell and UUID XML entry change.

### 12.2 Link topology

- One endpoint: unpaired and native local behavior only.
- Two same-color endpoints: bidirectional link.
- Three or more same-color endpoints: deterministic mesh/bus.
- Different colors remain isolated.
- Link never joins Send/Receive.
- Repaint, mode switch, logic disable, deletion, and rebuild invalidate routes.
- Physical plus wireless loops terminate without recursion errors or duplicate containers.
- Local containers are preferred before remote containers.

### 12.3 Cross-world cases

- overworld ↔ underground depth 1;
- overworld ↔ deeper underground world;
- underground depth 1 ↔ depth 2;
- two separate non-overworld worlds;
- endpoint at a cell boundary;
- endpoint creation moved through an elevator;
- remote world temporarily unavailable;
- save/reload while endpoints are in different worlds;
- host alone and multiplayer client present.

### 12.4 Vanilla machine integration

Test input, output, full, empty, and filtered cases for:

- Craftbot/Crafter;
- Vacuum and Flat Vacuum;
- Refinery;
- Ore Crusher;
- Prospector;
- Garage Chest;
- every active utility path using pipe container discovery.

For each machine, test local-only, same-world wireless, and cross-world wireless routes.

### 12.5 Send/Receive

- one sender/one receiver;
- multiple senders/one receiver;
- one sender/multiple receivers;
- multiple senders/multiple receivers;
- fair round-robin distribution;
- empty source, full receiver, filtered receiver, and changing filters;
- repaint during idle and immediately after a commit;
- receiver disable/delete/unload during route selection;
- transaction contention with a player or machine changing the same inventory;
- no loss or duplication after save/reload.

### 12.6 World loading and performance

- exact handle count and reference sharing for endpoints in one cell;
- handle release after disable/unpair/removal;
- idle grace period prevents thrashing;
- 64-cell cap blocks extra routes safely;
- startup stale-record reconciliation;
- failed world load retains records;
- large physical pipe network behavior from a single held endpoint cell;
- fixed-tick cost with 2, 16, 64, and more registered endpoints;
- no per-frame full-save scan and no client UI frame-rate regression.

### 12.7 Patch safety

- clean known-build install/restart/removal;
- adaptive future build with unrelated edits;
- changed, missing, or duplicated protected pipe calls block before writes;
- partial owned files and partial wrapper calls report repair-required;
- rollback after failure at every write position;
- exact and surgical removal;
- composition with every existing ScrapLab mod in every install/removal order;
- Steam overwrite detection reports `REINSTALL REQUIRED — SAVE PART AT RISK`;
- cache deletion only after verified changes;
- removal confirmation cancellation leaves every file and mod untouched.

---

## 13. Locked Phase 0 decisions

Phase 0 locked the balance and identity baseline:

1. **Permanent UUID** — `a34d9af0-4ba0-431d-b647-2d5435ecf138`.
2. **Displayed name** — Wireless Vacuum Pipe; internal name `obj_pneumatic_pipe_wireless`.
3. **Survival recipe** — default-unlocked Craftbot recipe producing two parts in 30 seconds from two Vacuum Pipes, two Component Kits, and four Circuit Boards.
4. **Logic input** — optional-on-by-default; one logic parent, no outputs.
5. **Directional throughput** — four-fixed-tick attempt interval baseline, subject to measured release tuning.
6. **Maximum loaded cells** — initial cap 64, with any profiling-driven reduction documented explicitly.

The cross-world technical architecture must not be weakened to bypass its Phase 1 safety gate.

## 14. Final feasibility judgment

The feature is technically plausible, including overworld-to-underground operation, but it cannot be implemented as a normal engine pipe connection. The safe design is an owned cross-world manager plus explicit pipe-query integration and strict server transactions.

The highest-risk items are:

1. proving atomic container commits across two loaded worlds;
2. keeping only the minimum remote cells loaded without breaking local pipe discovery;
3. covering every vanilla pipe consumer without fragile global monkey-patching;
4. preserving save safety for the custom UUID and shared icon atlas.

The phased gate plan resolves those risks before the large multi-file patch is allowed to ship.
