<p align="center">
  <img src="docs/images/scraplab-logo.png" width="190" alt="ScrapLab industrial globe and wrench emblem">
</p>

<h1 align="center">ScrapLab</h1>

<p align="center">
  <strong>Inspect · Repair · Tune</strong><br>
  An offline, backup-first world toolkit for Scrap Mechanic Survival.
</p>

<p align="center">
  <a href="https://github.com/Cooperkit/ScrapLab/releases/latest"><strong>Download the latest release</strong></a>
  ·
  <a href="#quick-start">Quick start</a>
  ·
  <a href="#safety">Safety</a>
  ·
  <a href="#optional-mod-workshop">Mod workshop</a>
</p>

---

ScrapLab turns a Scrap Mechanic Survival save into a readable world report.
It can inspect database health, locate likely performance hotspots, inventory
loose pickups, recover persisted raid state, and manage optional guarded game
patches—all without an installer, command prompt, or external dependencies.

ScrapLab is designed for ordinary players as well as technical world owners.
Read-only tools stay read-only. Every save-editing action creates and verifies
a complete timestamped backup before the original database is changed.

## What ScrapLab can do

| Tool | What it provides |
| --- | --- |
| **World Diagnostics** | Database health, save version, game tick, decoded world names, stored raids, and actionable warnings. |
| **Performance Scanner** | Read-only cell-density analysis, evidence-backed hotspot ranking, coverage reporting, cancellation, World Explorer paging, and privacy-safe JSON export. |
| **Loose Item Scanner** | Item icons, quantities, values, world names, positions, and despawn state for decoded ground pickups. |
| **Pickup Cleanup** | Remove one pickup, only expired pickups, or every safely decoded loose pickup after confirmation and backup verification. |
| **Raid Recovery** | Release crops registered to persisted raids, clear the stored raid schedule, and repair crops stranded by an older clear. |
| **Mod Workshop** | Install or remove optional quality-of-life Lua patches with exact-code guards, verified backups, rollback, and Steam-update detection. |

ScrapLab deliberately leaves player inventories, containers, quests, terrain,
creations, buildings, and unrelated world objects alone.

## Download

1. Open the [latest ScrapLab release](https://github.com/Cooperkit/ScrapLab/releases/latest).
2. Download `ScrapLab-2.10.0.zip` or the newest complete ZIP.
3. Extract the ZIP into its own folder.
4. Keep these three files together:
   - `ScrapLab.exe`
   - `ScrapLab.PatchHelper.exe`
   - `ScrapLab.Updater.exe`
5. Run `ScrapLab.exe`.

ScrapLab is portable and supports Windows 10 and Windows 11. Save inspection
and repair do not require administrator access. Windows asks for administrator
approval only when the optional Mod Workshop needs to change Steam game files.
One approved helper session is reused until ScrapLab closes.

> **Upgrading from Raid Rescue 1.x?** Install the ScrapLab 2.0 complete ZIP
> manually once because the old updater cannot safely rename all three
> programs. ScrapLab then migrates compatible settings and patch state, and
> normal one-click updates resume from 2.x onward.

## Quick start

### 1. Close Scrap Mechanic

Exit the game completely before opening a save. ScrapLab automatically locks
world controls whenever `ScrapMechanic.exe` or `ScrapMechanicServer.exe` is
running, and unlocks them when the process exits.

### 2. Choose a Survival world

ScrapLab searches every normal Scrap Mechanic `User_*` Survival folder and
puts the newest saves first. Use **Browse** only when the save is not listed.

The default location is:

```text
%APPDATA%\Axolot Games\Scrap Mechanic\User\User_<SteamID>\Save\Survival
```

Choose the normal `.db` file—not a `.scraplab-backup` or legacy
`.raidrescue-backup` file.

### 3. Analyze World

**Analyze World** is read-only. It checks SQLite integrity and builds the main
world report without loading the more expensive loose-pickup inventory.

### 4. Run only the tools you need

- Choose **Scan Performance** for hotspot analysis.
- Choose **Scan Loose Items** for pickup cards and cleanup controls.
- Review **Raid Recovery** only when the world contains persisted raid state
  or orphaned raid crops.

### 5. Read every confirmation

Cleanup and recovery dialogs identify the exact world and planned change.
Keep Scrap Mechanic closed until ScrapLab reports that verification finished.

## Performance scanner

The performance scanner streams supported world records on a background
thread. It currently measures proven, cell-located `Harvestable` and `Unit`
records rather than guessing about unsupported database tables.

Each result includes:

- world and cell coordinates;
- record counts and stored payload bytes;
- category totals;
- measured 3-by-3 neighborhood evidence;
- deterministic severity and confidence explanations;
- coverage and unsupported-schema warnings;
- the largest supported records encountered;
- a bounded, paged World Explorer.

The scanner fingerprints the source before and after scanning. If the save
changes or Scrap Mechanic starts, ScrapLab rejects the stale result. Exported
`ScrapLab-Performance-Report-v3.json` files omit the local save path, raw Lua
payloads, Steam IDs, and player-identifying data.

Hotspot results are leads for investigation, not promises of FPS improvement.
A dense cell can be legitimate, and unsupported record types may still affect
performance.

## Loose items

Loose pickups do not load during the normal world analysis. Choose
**Scan Loose Items** when you want the inventory report.

- Items are sorted by recovery value, with progression resources near the top.
- Installed game recipes are used when ScrapLab can prove an item's material
  value; unknown or modded items receive a stable category fallback.
- **Remove Item** changes only the selected decoded pickup and its matching
  Lua-storage record.
- **Clear Expired** removes only pickups already marked pending world cleanup.
- **Clear All Dropped Items** removes every safely decoded pickup in the report.
- Ambiguous or unreadable records are skipped instead of guessed.

Placed blocks, creations, player inventories, containers, quests, and ordinary
world structures are not treated as loose pickups.

## Raid recovery

Scrap Mechanic has officially corrected the original permanent-raid bug, so
ScrapLab no longer presents the old game hotfix button. Offline recovery tools
remain for worlds that already contain unwanted persisted state:

- **Resolve & Clear Raids** releases every verified registered crop and then
  removes the base-game raid-manager record.
- **Repair Orphaned Crops** repairs growing crops still waiting for a raid when
  no active stored raid references them.

Already-spawned robot units are left in the world. Automatically deleting
arbitrary offline units could remove unrelated robots.

## Safety

ScrapLab follows a fail-closed workflow:

1. Confirm Scrap Mechanic is closed.
2. Integrity-check the selected SQLite database.
3. Create a full timestamped backup beside the save.
4. Verify the backup before writing.
5. Re-read the affected records immediately before the change.
6. Make only the selected edit in a transaction.
7. Integrity-check and re-analyze the result.

New backups look like:

```text
MySurvivalWorld.scraplab-backup-20260731-143522-184.db
```

To restore one, close the game, preserve the repaired file somewhere else,
copy the backup, and rename the copy to the world's original `.db` filename.
Legacy `.raidrescue-backup` files remain valid restore sources.

## Optional Mod Workshop

Click the small ScrapLab emblem at the far left of the title bar to open the
hidden workshop. These patches change installed Scrap Mechanic Lua files, not
the selected save database.

The compact **All Mods** switch beside the Patch Catalog search installs every
currently compatible gameplay mod with one administrator approval. Its mixed
state means a compatible mod is missing or has an update ready; blocked mods
are shown as skipped instead of preventing the rest of the batch. Developer
Commands are always manual and are never changed by **All Mods**. Bulk removal
uses one combined custom-part warning, preserves Developer Commands, leaves the
Patch Bay armed, and stops before later removals if any mod cannot be restored.

| Mod | Effect |
| --- | --- |
| **Developer Commands** | Enables `/fly`, `/spawntree`, `/unlimited`, and other dev commands with configurable access. |
| **Resource Locator Dots** | Shows Connect Tool dots on refinable resource cores. |
| **Full-Speed Carrying** | Restores normal movement and sprinting while carrying. |
| **Better Engines** | Boosts engine power and top-tier fuel efficiency. |
| **Better Freezer & Beehive** | Adds freezer water input, larger storage, and 4x production. |
| **Better Plasma Drills** | Adds levels 4–5 with stronger, faster, longer-range drilling. |
| **Raid Detector** | Adds a trader-bought 256-meter raid logic sensor. |
| **Tree Saplings** | Adds plantable Small, Medium, and Large native-tree saplings. |
| **Wireless Vacuum Pipe** | Links same-color pipe systems, including across worlds. |
| **Network Storage Chest** | Browses network storage and routes deposits in Smart or Nearest mode. |
| **Revival Buff Recovery** | Restores food buffs after a Revival Baguette revive. |
| **Chemical Fertilizer Splash** | Makes chemical projectiles fertilize crops, grow beds, and registered ScrapLab plants. |
| **Dual-Fluid Water Cannon** | Fires connected water and chemical together on a logic pulse. |

Every workshop action checks compatibility, creates verified backups, and
rolls back failed changes. Active uninstall state is stored separately from
timestamped recovery history, so deleting old **Game Backups** cannot change
whether a mod is installed or installable. Recovery retention is bounded.

### Custom parts

- **Raid Detector** — UUID `a638a8aa-6f4f-41c2-9e31-702687066092`. Costs four
  Caged Farmers at the Hideout Trader and outputs logic while a scheduled or
  active raid is within 256 meters.
- **Tree Saplings** — three 20-stack items grow into random native trees in
  5/7/10 minutes. Fertilizer runs remaining growth at 2.5× speed; E safely
  uproots a pot. Their Tool Forge-generated held mesh uses a Clay-compatible
  skinned DAE, the complete vanilla Clay/Bucket first- and third-person
  animation family, and a size-specific green, yellow, or orange runtime tint.
  Native crowns have a 30% chance to drop the matching size, and the Hideout
  sells five for one, two, or three Caged Farmers. Remove all sapling items and
  planted pots before disabling; fully grown native trees remain safe. Existing
  Chemical Fertilizer Splash installs expose a one-click registry update so
  chemical impacts can fertilize saplings without treating them as watered crops.
- **Wireless Vacuum Pipe** — UUID `a34d9af0-4ba0-431d-b647-2d5435ecf138`.
  Craft two from two Vacuum Pipes, two Component Kits, and four Circuit Boards.
  Matching colors use **Link** mode to join pipe systems, or route **Send** networks
  to **Receive**, including across worlds. Send and Receive default to
  **Direct Container Only**; switch to
  **Entire Pipe Network** when wanted. Receive-side machines can pull from Send
  storage, while producer machines on Send (including water pumps) can place
  output into Receive storage. Cross-world routing retains its 64-cell cap.
  Remote cells load on demand, stable ordered container selections are cached
  within hard memory limits until their bodies or wireless route change, and idle
  directional routes use a long backoff so a
  configured but unused cross-world channel does not stay fully simulated.
  Vacuum Pumps retain unchanged input/output route topology for no more than
  five ticks, bound packing-station geometry and item-source probes, and avoid
  scanning an inactive intake trigger. Wireless topology changes invalidate
  the snapshot immediately; item quantities and native container transactions
  remain live and authoritative. Prospectors with no matching input or a full
  output now honor their native 0.4-second retry delay instead of rescanning a
  large unchanged container route on every server tick.
  When a Craftbot opens before a requested cell finishes loading, a bounded
  readiness refresh updates its recipe counts automatically instead of
  requiring another storage UI to be opened first. Machines that retain a
  container list refresh that list when wireless topology becomes ready.
  The Wireless Vacuum Pipe save warning must be followed before removal because
  worlds and creations can contain its custom UUID.
- **Network Storage Chest** — UUID `bc7576a7-f226-459a-883c-e8460e955d63`.
  Craft one from a piped Small Chest, ten Component Kits, and twenty Circuit
  Boards. Browse connected storage with search, native colored-type filters,
  and deterministic type/name sorting, then route its three-slot tray with
  **Smart Sort** or **Nearest Empty**. Wireless Vacuum Pipe support is optional,
  and connected players deposit through host-authoritative transactions. Busy
  Craftbots and pumps are indexed in the background: unchanged scans are not
  redrawn and changed totals are coalesced. Withdrawals prefer fuller local
  source stacks and commit against exact live slots, so unrelated changes in the
  same chest do not cancel the request. Short item, route, or transaction races
  retry safely under the original click without duplicating or losing items.

ScrapLab's Craftbot recipes are grouped immediately after the ordinary Vacuum
Pipe recipe instead of being appended to the bottom. Raid Detector and Tree
Saplings remain Hideout trades, and advanced Plasma Drills remain upgrade-only.

All custom icons share the verified **ScrapLab Icon Pack**.

> **Custom-part warning:** before disabling one of these mods, remove all of
> its custom parts from worlds, inventories, containers, Lifts, and saved
> creations. Save and close the game first. If Steam removes a registration,
> reinstall the mod before loading an affected save.

### Gameplay patch notes

- **Full-Speed Carrying** is client-side, so each multiplayer user must install
  it to receive normal carry movement and sprinting.
- **Better Freezer & Beehive** gives new machines five input slots, 4x speed,
  and larger output storage. Existing machines keep their current slot count.
- **Better Plasma Drills** adds levels 4 and 5 for 25 and 50 Component Kits,
  with greater speed, range, radius, battery capacity, and unit damage.
- **Developer Commands** includes `/fly`; flight and damage protection apply
  only to the player who enabled it. The prototype
  `/spawntree [random|small|medium|large]` places one native tree on the aimed
  terrain. Use Every Player access only with people you trust.

> **Advanced drill warning:** remove or downgrade every level-4/5 drill before
> disabling the mod or verifying game files.

> **Dual-Fluid Water Cannon warning:** disconnect Chemical Containers and save
> affected worlds before disabling the mod or verifying game files.

After a Steam update, ScrapLab offers **Update** or reinstall actions only when
the protected game code is still compatible. Conflicts and partial patches are
blocked without writing. When Steam Verify cleanly removes a patch, ScrapLab
automatically retires its old active receipt before reinstalling it. A compact
superseded marker preserves save-sensitive warnings without allowing stale
backup paths to affect the new transaction. Exact orphaned ScrapLab-owned files
are adopted safely and removed again with the fresh installation.

The shared custom-icon baseline belongs to active patch state, not recovery
history. ScrapLab migrates older baselines automatically and can reconstruct
only its managed transparent atlas tiles when that technical baseline is lost
or corrupt; unrelated game pixels are left untouched.

## Updates and migration

ScrapLab checks the latest stable GitHub release shortly after startup and
every 30 minutes while open. Network failures remain quiet and every offline
tool continues working.

One-click update accepts only official HTTPS assets, requires GitHub's
published SHA-256 digests, verifies product names and versions, and uses the
fixed updater companion for bounded rollback. Updating the application does
not open a save or reinstall optional mods.

On first start, ScrapLab copies missing compatible data from:

```text
%LOCALAPPDATA%\Raid Rescue
```

to:

```text
%LOCALAPPDATA%\ScrapLab
```

Preferences, active patch receipts, and verified game-script backups are
migrated without deleting or overwriting the legacy originals. Legacy in-game
Lua identifiers intentionally remain unchanged so installed Raid Rescue
patches can still be detected, configured, and removed safely.

## Build from source

On Windows, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

The build uses the .NET Framework compiler included with Windows and produces:

```text
dist\ScrapLab.exe
dist\ScrapLab.PatchHelper.exe
dist\ScrapLab.Updater.exe
release\ScrapLab-2.10.0.zip
```

No runtime dependency download is required. To Authenticode-sign a release,
set `SCRAPLAB_SIGN_CERT_SHA1` to the certificate thumbprint and make
`signtool.exe` available. The legacy `RAID_RESCUE_SIGN_CERT_SHA1` variable is
also accepted for existing build machines.

Regression coverage includes database integrity, Lua-storage rewrites,
dropped-item safety, adaptive patch transactions, companion boundaries, Raid
Detector atlas pixel isolation and byte-exact restoration, update validation,
product migration, performance aggregation and ranking, operation
cancellation, World Explorer paging, and privacy-safe export.

### ScrapLab Tool Forge

The repository also contains a separate, generate-only character-tool builder.
Its first template imports a Blender binary or ASCII leaf-plant FBX 7.x,
previews it on Scrap
Mechanic's installed Clay/Bucket rigs, and stages a reviewable Tree Saplings
held-tool package without editing ScrapLab or the game. The editable FBX stays
in the package, while the game-facing output is a deterministic skinned DAE
with the same `jnt_right_weapon` / `root_bucket_jnt` attachment contract used
by the vanilla Clay tool.

```powershell
powershell -ExecutionPolicy Bypass -File .\build-tool-forge.ps1
```

This produces `dist\ToolForge\ScrapLab.ToolForge.exe` and the standalone
`release\ScrapLab-Tool-Forge-1.0.0.zip`. See
[`docs/SCRAPLAB-TOOL-FORGE.md`](docs/SCRAPLAB-TOOL-FORGE.md) for its safety
boundary, project format, CLI, and integration workflow.

## Privacy

Save inspection and repair happen locally. ScrapLab never uploads a world.
The only routine network request is the GitHub release check. Installed Scrap
Mechanic fonts and item icons are read locally at runtime and are not bundled
with this repository.

## Disclaimer

ScrapLab is an unofficial community project and is not affiliated with or
endorsed by Axolot Games. Keep backups and test repaired worlds before deleting
their recovery copies.
