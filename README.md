# Raid Rescue

> A small, backup-first recovery tool for permanent or stuck raids in
> Scrap Mechanic Chapter 2 Survival saves.

![Raid Rescue showing a stored tier-four raid](docs/images/02-raid-detected.png)

Raid Rescue finds Scrap Mechanic Survival saves, reads their stored raid
manager, and explains every raid it finds. If a broken raid is permanently
active, the tool can create a verified backup and clear the saved raid-manager
state. It also includes an optional, version-locked cumulative game hotfix that
prevents the known Chapter 2 raid failure paths and corrects fertilizer growth
timing.

It is designed for regular players: no database editor, command prompt,
installer, or extra dependencies are required.

On first launch, Raid Rescue offers an optional interactive tutorial. The
animated tour explains the safe workflow using the real controls without
changing a save. The **?** button in the title bar opens a detailed Help menu
where the tutorial can be replayed or its first-run prompt can be reset.

## Important

- Close Scrap Mechanic completely before repairing a save.
- Keep the automatic backup until the repaired world has loaded and saved
  successfully.
- Download Raid Rescue only from this repository's **Releases** page.
- Raid Rescue is intended for Windows 10 and Windows 11.

## Download

1. Open the repository's [latest release](../../releases/latest).
2. Download `RaidRescue.exe`.
3. Put it anywhere convenient and double-click it.

The app is portable and does not need to be installed. Clearing a save does not
need administrator access. The first game hotfix or secret-mod change in an app
session asks for Windows administrator approval because Steam normally stores
the game under Program Files. Later patch actions reuse that protected session
without asking again.

Whenever a hotfix or secret-mod action changes game Lua files, Raid Rescue
deletes Scrap Mechanic's generated `Cache\Bundle\core_data.cbo` script cache.
The game rebuilds it automatically on the next normal launch, which may take a
little longer. The `-dev` Steam launch option is not required.

Windows may show a SmartScreen message because independently distributed tools
are not always code-signed. Verify that the download came from this repository
before choosing **More info -> Run anyway**. Do not bypass a warning for a copy
downloaded from somewhere else.

## Quick start

### 1. Close Scrap Mechanic

Exit the game completely. Raid Rescue locks world selection, Browse, Analyze,
and repair access while the game is running so it never opens the live world
database.

### 2. Choose your world

Raid Rescue automatically looks in the normal Scrap Mechanic Survival save
folders. Select the world from the list. If it is not listed, click **Browse**
and choose its `.db` file.

![World selector and Analyze World button](docs/images/01-select-and-analyze.png)

The usual save folder is:

```text
%appdata%\Axolot Games\Scrap Mechanic\User\User_<SteamID>\Save\Survival
```

You can paste that path into File Explorer's address bar.

### 3. Analyze the world

Close Scrap Mechanic, then click **Analyze World**. This step is read-only: it
does not change the save. Raid Rescue safety-locks world selection, Browse, and
Analyze while the game process is running so the live database is never opened.
The controls unlock and the selected world refreshes automatically after the
game closes.

The tool shows the raid level, state, threat, planned enemies, crop triggers,
coordinates, timing values, and signs that the raid is stuck.

![Raid diagnostic results](docs/images/02-raid-detected.png)

### 4. Choose a repair

Scroll to the bottom of the diagnostic. Two repair buttons are shown together:

- **Clear All Raids** repairs the selected save and removes its stored raid
  schedule.
- **Install / Update Hotfix** patches the supported game scripts so impossible
  raids safely cancel and fertilized crop timing stays synchronized.

You may clear the affected save, install the preventive hotfix, or do both.
Close Scrap Mechanic before using either repair.

![Warnings and Clear All Raids button](docs/images/03-review-and-clear.png)

Read the confirmation and choose **Yes**. Raid Rescue then:

1. checks the save database;
2. creates a complete timestamped backup beside the save;
3. verifies the backup;
4. clears the exact base-game raid-manager record in a transaction;
5. checks the repaired save again.

The hotfix has its own animated in-app confirmation explaining exactly what it
changes. On the first patch action, Windows displays one administrator prompt.
Raid Rescue keeps an authenticated elevated patch session alive until the app
closes, so later supported changes do not prompt again. It verifies the
installed game version and every target script, creates checksum-verified
backups, installs all changes atomically, and rolls back automatically if any
step fails.

## Tutorial and Help

- The first-run prompt offers a guided tour and can be declined safely.
- The tour spotlights the real interface and explains world selection,
  read-only analysis, raid cards, backups, Clear All Raids, and the temporary
  hotfix.
- Open the **?** button at any time for the full field manual.
- **Replay Tutorial** starts the tour immediately.
- **Reset First-Run Prompt** makes the welcome question appear the next time
  Raid Rescue starts.

The tutorial preference is a tiny local file stored under:

```text
%LOCALAPPDATA%\Raid Rescue\preferences.ini
```

It contains only the tutorial version and no save data or personal information.

### 5. Test the repaired world

Open Scrap Mechanic, load the world, and confirm that the permanent raid is
gone. Play briefly and save normally. Keep the backup until you are confident
the world is working.

## What Raid Rescue changes

Raid Rescue removes the saved base-game raid-manager record that schedules and
tracks raids.

It does **not** edit:

- player inventories;
- creations or buildings;
- quests;
- containers;
- player records;
- unrelated world objects.

If raid robots have already spawned into the world, their world-unit records
are intentionally left alone. Automatically deleting arbitrary units offline
could remove unrelated robots. The raid schedule itself is still cleared.

## Automatic backup

The verified backup is placed beside the selected save and looks like:

```text
MySurvivalWorld.raidrescue-backup-20260728-161319-395.db
```

### Restore a backup

1. Close Scrap Mechanic.
2. Open the world's `Survival` save folder.
3. Move the repaired `.db` file somewhere safe.
4. Copy the `raidrescue-backup` file.
5. Rename the copy to the original world's exact `.db` filename.
6. Start Scrap Mechanic and load the world.

## Optional game hotfix

The **Install / Update Hotfix** button currently supports verified Scrap
Mechanic **1.0.2.870** game scripts. It addresses the failure paths found in
the Chapter 2 raid and fertilizer code:

- missing or empty raid spawn positions;
- a malformed short spawn-path result;
- unbounded failed path searches;
- stale destroyed-crop references;
- crop survival state being reset after a reload;
- incomplete cleanup of raid and navigation handles;
- normal-soil fertilizer animation running at a different rate from the server;
- growbed fertilizer animation running at a different rate from the server;
- completed fertilized crops waiting for a later update after surviving a raid.

The installer refuses to guess. It will not touch an unsupported game version,
or scripts whose SHA-256 checksums do not match a verified original, previous
Raid Rescue patch, or current cumulative patch. If the raid hotfix is already
installed, the updater keeps it and applies only the missing fertilizer files.
Backups preserve the exact state present immediately before the update and are
stored under:

```text
%localappdata%\Raid Rescue\Game Backups\Scrap Mechanic
```

After changing the verified scripts, Raid Rescue deletes only the generated
`Cache\Bundle\core_data.cbo` file. Scrap Mechanic rebuilds this cache from the
current Lua files on the next normal launch.

Steam may restore the original scripts after a game update or **Verify
integrity of game files**. That is expected. A future game update may include
an official fix, so Raid Rescue does not apply this hotfix to unknown versions.

## Super Secret Mods

Click the small Raid Rescue emblem at the far left of the title bar to open the
hidden patch bay. Turn on the master switch, then install any optional mod:

- **Resource Locator Dots** reveals haybot spines and refineable wood, stone,
  and metal cores while the Connect Tool is equipped. The game requires one
  output slot before it will draw the dot, so the patch exposes one inactive
  logic output. It never sends an ON signal.
- **Developer Commands** unlocks Scrap Mechanic's existing Survival developer
  chat commands. Its **Host Only** option is recommended and grants them only
  to the player hosting the world. **Every Player** gives the command list to
  every joined player while connected; `/kick` and `/ban` remain host-only.
  Available tools include `/unlimited`, `/limited`, `/god`, `/spawn`, item and
  weapon grants, time controls, player utilities, aggro controls, and raid
  commands. Neither option enables the server's full `g_survivalDev` mode.
- **Chemical Fertilizer Splash** makes a player chemical projectile fertilize
  the exact normal-soil plot, growing crop, or growbed it hits. A red Farmbot's
  pesticide projectile fertilizes supported plots in a 2.5-block radius around
  its impact.
- **Dual-Fluid Water Cannon** lets a mounted water cannon accept one Water
  Container and one Chemical Container. Each OFF-to-ON logic pulse consumes
  one of every available liquid and fires both projectiles along the same
  muzzle path. Its built-in tank remains water-only.

These mods are independent from save repair and the normal cumulative hotfix.
Scrap Mechanic must be closed before changing them. On the known game build,
each operation still accepts only verified whole-file checksums. On a later
verified Steam build, Raid Rescue can adapt when every protected code snippet
and required callback is still an exact match. Formatting or comments inside a
protected snippet count as a change and block the operation; unrelated changes
elsewhere in the file are preserved.

Before writing, Raid Rescue reads Steam's manifest, verifies file timestamps,
preflights every target, creates every output in memory, and makes
checksum-verified backups. Multi-file mods and dependencies remain
all-or-nothing and roll back together if any final hash fails. Removing
Chemical Fertilizer Splash restores whichever verified state existed before
installation, including the normal Raid Rescue fertilizer hotfix. Dual-Fluid
Water Cannon requires Chemical Fertilizer Splash; Raid Rescue automatically
installs the dependency and removes the cannon first if the fertilizer mod is
removed.

Adaptive installations keep one bounded active receipt under
`%LOCALAPPDATA%\Raid Rescue\Patch State\Active`. If the installed files are
unchanged, removal restores the exact pre-install bytes. If unrelated edits
were made afterward but all Raid Rescue snippets remain intact, removal
surgically reverses only those snippets. A partial, duplicated, or edited Raid
Rescue snippet blocks removal without writing.

After a Steam update, secret mods are never reinstalled automatically. Patch
Bay shows **Compatible Game Update** when the new files remain safe, and the
user deliberately toggles each wanted mod back on. **Game Update Changed
Required Code**, **Other Modification Detected**, or **Partial Patch - Repair
Required** means Raid Rescue refused to guess.

Secret-mod backups use bounded retention. Raid Rescue keeps the two newest
verified folders for each install, remove, or configure action and removes older
superseded copies only after a change succeeds and its final checksums pass.
Cleanup matches only exact Raid Rescue timestamped folder names; unknown folders
and manual backups are left untouched.

> [!WARNING]
> Developer commands can permanently change the active world. Installing
> Developer Commands does not edit a save, but Raid Rescue cannot undo items,
> units, raid state, time changes, or other effects produced by commands.
> **Every Player** should be used only with people you completely trust because
> any joined player can run world-changing commands. Back up important worlds
> before experimenting.

> [!CAUTION]
> Before disabling Dual-Fluid Water Cannon, disconnect every Chemical Container
> from every mounted water cannon while the mod is still installed, save every
> affected world, and close the game. Restoring the original two-input cannon
> script while those third connections remain can prevent a creation or world
> from loading correctly. Steam Verify and game updates can also restore the
> original script, so perform the same cleanup before either one.

## What the diagnostic shows

- SQLite integrity and repair readiness
- Save version, game tick, and file size
- Raid tier, state, threat value, world slot, and coordinates
- Planned robot total, spawn groups, and robot composition
- Crops and planting records that triggered the raid
- Missing crop references that can indicate a permanent raid
- Stored timing and spawn-point state

## Frequently asked questions

### My world is not in the list

Click **Browse** and select the `.db` file manually. The standard folder is
shown above. Make sure you choose a Survival save, not a backup from a different
world.

### The Clear All Raids button is disabled

Close Scrap Mechanic completely, including any game process still stopping in
the background. Reopen Raid Rescue and analyze the world again.

### The Install / Update Hotfix button says the game is running

Close Scrap Mechanic completely and try again. Raid Rescue checks before asking
for administrator approval and checks again inside the elevated helper.

### The hotfix says my files or version are unsupported

Do not force the patch. A game update may already contain an official fix, or a
mod may have changed one of the same scripts. Use Steam's **Verify integrity of
game files** if you need to restore the originals.

The normal cumulative raid/fertilizer hotfix is intentionally version-locked.
Adaptive compatibility applies only to Super Secret Mods.

### Patch Bay says Compatible Game Update

Steam installed a different build, but the exact code protected by that secret
mod is unchanged. Close the game and toggle the mod normally. Raid Rescue
preserves unrelated updated code and records a verified removal receipt.

### Patch Bay says required code changed or another modification was detected

Do not force the patch. The game update, another mod, or a manual edit changed
something Raid Rescue must understand exactly. **Verify integrity of game
files** can restore official files, but a newly changed protected feature may
need a Raid Rescue update first.

### It says there are no raids

The selected save does not currently contain a stored base-game raid-manager
record. Confirm that you selected the correct world. Modded raid systems may
store their data differently and are not automatically removed.

### Will this delete my builds or inventory?

No. The repair targets one exact base-game raid-manager record. A complete
verified backup is made first.

### Some raid robots are still standing in the world

Raid Rescue clears the raid schedule, but deliberately does not delete spawned
world units. Remove remaining robots normally in-game.

### Does it upload my save?

No. Raid Rescue works locally and offline. It does not upload the save or
require a network connection.

## Compatibility

- Windows 10 or Windows 11
- Scrap Mechanic Chapter 2 Survival database format
- No installer or external dependency downloads
- Save repair requires no administrator rights
- Optional cumulative game hotfix supports verified original and previous Raid
  Rescue states for version 1.0.2.870. Windows requests administrator
  confirmation once per Raid Rescue session, and subsequent supported hotfix
  or secret-mod actions reuse that protected session.
- Super Secret Mods recognize known build `24417028` / `1.0.2.870` by verified
  hashes and can adapt to a different valid Steam build only when every
  protected snippet and structural guard remains exact.

Legacy pre-Chapter-2 saves are detected and left unchanged.

## Build from source

The source is in [`source`](source). On Windows, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

The compiled app is written to `dist\RaidRescue.exe`. The build uses the .NET
Framework compiler included with Windows and links only Windows framework
assemblies.

## Privacy and game assets

Raid Rescue reads only the local save chosen by the user. When Scrap Mechanic
is installed, the app privately loads the game's Shentox and Inter fonts at
runtime to match the game's interface. Those fonts are not copied into or
redistributed with Raid Rescue.

For smoother rendering, the app creates two per-user Windows feature-control
values for `RaidRescue.exe`: IE11 standards mode and GPU rendering. These
settings apply only to Raid Rescue, require no administrator access, and do not
send any information over the internet.

Raid Rescue is an unofficial community tool and is not affiliated with or
endorsed by Axolot Games.
