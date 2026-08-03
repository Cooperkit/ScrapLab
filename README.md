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
2. Download `ScrapLab-2.4.0.zip` or the newest complete ZIP.
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

| Mod | Effect |
| --- | --- |
| **Developer Commands** | Unlocks Survival chat commands plus `/fly` collision-free flight, with configurable Host Only or Every Player access. |
| **Resource Locator Dots** | Makes refineable resource cores and haybot spines visible with the Connect Tool. |
| **Full-Speed Carrying** | Restores normal walking and sprinting for hand-carried objects and Lift-held creations, using the game's native carry sprint animations. |
| **Better Engines** | Gives every Electric Engine gear 10,000 power and raises level-5 Electric/Gas efficiency to 40,250 points per battery or fuel item. |
| **Better Freezer & Beehive** | Adds direct Freezer water-container input, 4x production, five input slots for newly placed machines, and larger finished-item buffers. |
| **Better Plasma Drills** | Adds level-4 and level-5 upgrades, greater speed, battery capacity, range, beam radii up to 10, and 20–300 unit damage per second. |
| **Raid Detector** | Adds a beacon-housed logic sensor sold by the Hideout Trader that stays on for scheduled or active raids within 256 meters. |
| **Revival Buff Recovery** | Restores the exact pizza and veggie-burger buffs held before a real Revival Baguette revive. |
| **Chemical Fertilizer Splash** | Lets chemical projectiles fertilize farm plots and grow beds; farmbot chemicals use a radius. |
| **Dual-Fluid Water Cannon** | Accepts logic, water, and chemical inputs and fires every available liquid on one OFF-to-ON pulse. |

Every workshop action preflights all targets before writing, creates
SHA-256-verified backups, replaces files atomically, verifies the outputs, and
rolls the entire operation back if one target fails. Backup retention is
bounded rather than growing forever.

**Raid Detector** is a custom, save-sensitive part with permanent UUID
`a638a8aa-6f4f-41c2-9e31-702687066092`. Buy one repeatedly from the unlocked
Hideout Trader for four Caged Farmers. It scans a 256-meter 3D sphere in its
current world every ten fixed ticks and keeps one normal fan-out logic output
on from raid scheduling through the active attack. The part reuses the vanilla
beacon housing, animates only while detecting, and creates no map marker, menu,
or sound. Its concept #1 artwork is part of the shared **ScrapLab Icon Pack**.
Definition 2 uses the interactable body's world reference so the server scan
works correctly during the complete countdown and active-attack sequence.
When the first custom-part mod is installed, ScrapLab writes every icon shipped
with that app version into verified transparent cells, allocating from the
bottom-right of the atlas upward so normal game additions can continue growing
from the top. Individual mod toggles then change only their icon's XML
registration; the PNG is rewritten only when the pack is first installed,
expanded by an app update, or removed with the final custom-part mod. Decoded
pixels outside all managed cells must remain identical. One bounded baseline
and catalog receipt is shared instead of copying the 11 MB atlas per mod.
Custom icons use true alpha transparency. If an older verified Raid Detector
installation still contains the opaque blue-background artwork, its Patch Bay
card offers a safe **Update** action that replaces only that managed icon tile.
The same Update action migrates verified definition-1 detector scripts without
unregistering the custom part or requiring detectors to be removed from saves.

> **Raid Detector save warning:** remove every detector from worlds,
> inventories, containers, and lifts, save the affected worlds, and close the
> game before disabling the mod. If Steam removes its registrations, reinstall
> it before opening a world that may still contain the custom part.

After Steam installs a new game build, installed-state receipts are compared
with the actual Lua files. Compatible updated scripts may be patched only when
every protected snippet and structural guard remains exact. Changed protected
code, partial patches, duplicate targets, and unrelated modifications on a
known build are blocked without writing.

> **Dual-Fluid Water Cannon warning:** disconnect Chemical Containers from
> mounted water cannons and save each affected world before disabling the mod,
> running Steam Verify, or allowing a game update to restore the original
> two-input script.

**Full-Speed Carrying** changes only the carry tools' sprint restrictions and
animation slots. Crouching, water and chemical-goop movement, damage states,
Lift placement, and save data remain untouched. Because the restriction is
client-side, each multiplayer participant who wants the effect must enable it
in their own game installation.

**Better Engines** keeps the original speed curves, gear counts, bearing
limits, upgrade paths, saved settings, and Gas Engine power. It changes only
the Electric Engine gear-power table and the normal/creative level-5
efficiency records in the Electric and Gas engine scripts.

**Better Freezer & Beehive** lets one directly connected Water Container feed
a Freezer, with connected water preferred before its internal supply. Freezers
produce 20 ice every 21.6 seconds and store up to 2,500 finished ice; Beehives
produce one beeswax every 43.2 seconds and store up to 100. Newly placed
machines receive five filtered 20-item input slots. Existing machines keep
their current slot count, and five-slot containers created by the mod remain
save-persistent after removal without changing either machine UUID.

**Better Plasma Drills** upgrades level 3 to level 4 for 25 Component Kits and
level 4 to level 5 for 50. Level 4 uses 5 speed, 6,000 battery points, 40 range,
and radius settings 5–7; level 5 uses 10 speed, 12,000 points, 75 range, and
radius settings 8–10. Both retain level-3 material capability while updating
voxel terrain every three or two ticks respectively. Levels 1–5 deal 20, 30,
50, 100, and 300 unit damage per second. This remains continuous beam damage
against every unit-backed creature the vanilla raycast can hit; player
characters, voxel behavior, battery use, impact force, and part destruction
are unchanged.

An intact older Better Plasma Drills installation appears as **Damage Update
Available**. Its Patch Bay **Update** action migrates the protected Lua and
receipt atomically without temporarily removing the level-4/5 UUIDs. Original
uninstall bases and unrelated later edits are preserved.

> **Save warning:** Levels 4 and 5 use permanent ScrapLab UUIDs. Downgrade or
> remove every advanced drill from worlds, inventories, containers, and lifts
> before disabling the mod or verifying game files. If Steam replaces the mod,
> reinstall it before loading affected worlds.

Developer commands and optional mods can permanently change gameplay. Back up
important worlds and use Every Player command access only with trusted players.
`/fly` uses ScrapLab's hidden input tool and isolated scripts under
`Survival/Scripts/ScrapLab`. Smooth impulse flight handles open space, while
short capsule-checked position steps cross solid geometry. Scrap Mechanic's
normal camera and mouse controls remain untouched, falling and ragdolling are
suppressed during flight, personal damage protection stays active, and noclip
refuses to exit inside solid geometry. The world-bound player script performs
flight physics; the command script only coordinates permissions, input, and
multiplayer state. Flight protection is keyed to the requesting player and does
not change Scrap Mechanic's separate global `/god` setting or protect other
players in the hosted world.

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
release\ScrapLab-2.4.0.zip
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

## Privacy

Save inspection and repair happen locally. ScrapLab never uploads a world.
The only routine network request is the GitHub release check. Installed Scrap
Mechanic fonts and item icons are read locally at runtime and are not bundled
with this repository.

## Disclaimer

ScrapLab is an unofficial community project and is not affiliated with or
endorsed by Axolot Games. Keep backups and test repaired worlds before deleting
their recovery copies.
