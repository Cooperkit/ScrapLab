# Network Storage Chest — Phase 1 automatic test

Phase 1 replaces the sample catalog with a read-only index of physically connected local input containers. It does not withdraw network items or automatically route deposits.

## Run the complete test

1. Start Survival with a live character.
2. Stand in an open area.
3. Run:

   ```text
   /slstorage1 auto
   ```

Nothing needs to be built or placed. The command creates one disposable Network Storage Chest, a T-pipe, and two real piped chests in front of the player. It fills them with controlled test items, runs the complete test, empties every inventory, and removes the station.

The final chat line must report:

```text
AUTOMATIC TEST COMPLETE: 11 passed, 0 failed. Disposable station removed.
```

## Run the Phase 1 scale qualification

After the fast physical-network test passes, stand in an open area and run:

```text
/slstorage1 qualify
```

Stay near the starting point. ScrapLab creates 500 real engine-backed piped
chests and two Network Storage Chest terminals in batches, completes the
qualification without requiring a built setup, empties the fixtures, removes
all 502 parts, and verifies their removal.

The final chat line must report:

```text
PHASE 1 QUALIFICATION COMPLETE: 9 passed, 0 failed. All 502 test parts removed.
```

This qualification validates:

- cold indexing of 1, 50, 100, and 500 real engine containers;
- the 12-container-per-tick scan budget and actual slot-scan counts;
- shared-cache reuse by two terminals viewing the same 500 containers;
- the normal client open request and server catalog snapshot callback;
- six continuous seconds of zero container scans while both terminals are closed;
- verified item-safe removal of every generated test part.

## What the command validates

- actual native physical T-pipe topology;
- exactly two deduplicated input containers;
- combined quantities, stacks, and source counts;
- exclusion of the terminal's real five-slot buffer;
- warm-cache reindexing without rescanning unchanged slots;
- refresh after one connected container revision changes;
- refresh after one physical branch is removed;
- aggregate correctness at 1, 50, 100, and 500 simulated container records;
- zero active viewers and `IDLE` status after the test session closes;
- item-safe cleanup of every disposable container and shape.

## Interrupted-run recovery

The fixture shape IDs and cell are recorded before testing. If the game closes or crashes during the test, run `/slstorage1 auto` again. ScrapLab loads that cell, removes the interrupted fixture, and starts a fresh test automatically.

## Failure evidence

If any check fails, close the game and provide the newest `game-*.log`. Detailed result lines begin with `[ScrapLab Storage Phase 1 Auto]`.
