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
need administrator access. Installing the optional game hotfix asks for Windows
administrator approval because Steam normally stores the game under Program
Files.

Windows may show a SmartScreen message because independently distributed tools
are not always code-signed. Verify that the download came from this repository
before choosing **More info -> Run anyway**. Do not bypass a warning for a copy
downloaded from somewhere else.

## Quick start

### 1. Close Scrap Mechanic

Exit the game completely. Raid Rescue can inspect a save while the game is
open, but it deliberately disables repair to prevent two programs from writing
to the same save.

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

The hotfix has its own animated in-app confirmation explaining exactly what it changes. If you
accept, Windows displays an administrator prompt. Raid Rescue verifies the
installed game version and every target script, creates checksum-verified
backups, installs all changes atomically, and rolls back automatically if any
step fails.

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

Steam may restore the original scripts after a game update or **Verify
integrity of game files**. That is expected. A future game update may include
an official fix, so Raid Rescue does not apply this hotfix to unknown versions.

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
  Rescue states for version 1.0.2.870 and requires a Windows administrator
  confirmation

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

Raid Rescue is an unofficial community tool and is not affiliated with or
endorsed by Axolot Games.
