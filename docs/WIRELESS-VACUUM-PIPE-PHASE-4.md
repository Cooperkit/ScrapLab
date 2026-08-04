# Wireless Vacuum Pipe — Phase 4 Validation

## Current state

**Phase 4 definition 3 is complete — 2026-08-04.** The final automatic run in `game-20260804-163835.log` reported **10 passed, 0 failed, 0 skipped**, including exact same-world and cross-world delivery with a 3/3 receiver split. The desktop coordinator reported **7 passed, 0 failed, 0 skipped**. Post-run inspection of `ITSSCRAPENNING.db` found the saved Phase 4 harness state with `cleanup=ABSENT`, and the temporary endpoint IDs were absent from the manager registry. No Phase 4 Lua or sandbox errors occurred. Phase 5 is unlocked.

Definition 2 removed the first run's cross-script sandbox violation. Definition 3 fixed the second run's zero-container timeout by supplementing the engine's empty directional query for a neutral Wireless Pipe root with the already-proven local physical traversal. That traversal stops at directional machinery and never follows a wireless peer, so `SEND`/`RECEIVE` remains separate from `LINK`. Definition 3 also gives interrupted fixtures a dedicated cell-ready recovery path and refuses to overwrite a pending cleanup receipt.

The transfer system follows the Phase 1 safety result:

- every color channel has one short-lived scheduler lock;
- one transfer is selected every four fixed ticks;
- selection stores endpoint IDs, generations, container shape IDs, item UUID, and quantity—but never a cached Container authority;
- commit occurs on the following fixed tick;
- sender, receiver, cell handles, modes, colors, generations, shapes, local native/physical graph routes, source quantity, filters, and destination capacity are all freshly resolved before the transaction begins;
- one native transaction queues one spend and one collect;
- failed validation or commit changes neither inventory and retries on a later interval;
- receiver and sender cursors advance only after a successful commit;
- inventory truth never exists in a manager buffer, mirrored container, spawned item, or escrow record.

Multiple Senders and Receivers use stable endpoint ordering with saved round-robin cursors. Empty sources report `CHANNEL EMPTY`; blocked destinations report `DESTINATION FULL`. Link endpoints never join a directional group.

Successful transfers create two local effects instead of one impossible cross-world path: the source world animates its container toward the Sender, and the destination world animates its Receiver toward the destination container. Cross-world activity also uses a short double glow pulse.

## Automatic in-game test

1. Start Scrap Mechanic normally and load the test survival save.
2. Stand anywhere with a live character.
3. Run:

```text
/slpipe4 auto
```

Do not build or place anything. The command creates three temporary Water Container/Wireless Pipe rigs:

- one local Sender;
- one local Receiver;
- one Receiver in a previously discovered remote world when available, otherwise a second same-world Receiver.

It then verifies:

- four-tick scheduling and the one-tick fresh-resolution boundary;
- exactly six source items become exactly six destination items;
- same-world delivery;
- cross-world delivery when the save has another discovered world;
- fair Receiver round-robin distribution;
- empty sources create nothing;
- full destinations consume nothing;
- locks and pending routes are released;
- manager and handle invariants remain valid.

Expected result with a discovered remote world: **10 passed, 0 failed, 0 skipped**. A save without another discovered world reports **9 passed, 0 failed, 1 skipped**.

The test removes all fixtures and clears its cleanup receipt automatically. If the game closes during the run, a dedicated cell-ready recovery callback loads each recorded fixture cell and removes only the exact saved shape IDs. A new run is blocked until that receipt is cleared, so interrupted fixture information cannot be overwritten.

After the summary appears, close the game normally. The desktop coordinator is:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Run-WirelessVacuumPipePhase4Validation.ps1
```

Phase 5 is unlocked. The automatic run exceeded the nine-pass gate with zero failures, the coordinator reported zero failures, and post-run save inspection confirmed that no cleanup receipt or temporary endpoint registration remained.

## Developer files

- Directional transfer: `source/Patching/Scripts/ScrapLab/PipeSystem/WirelessPipeTransfer.lua`
- Manager host: `source/Patching/Scripts/ScrapLab/PipeSystem/WirelessPipeManager.lua`
- Endpoint effects: `source/Patching/Parts/WirelessVacuumPipe/WirelessVacuumPipe.lua`
- Automatic harness: `source/Patching/Parts/WirelessVacuumPipe/WirelessVacuumPipePhase4Harness.lua`
- Development installer: `tools/experiments/Manage-WirelessVacuumPipePhase4.ps1`
- Source regression: `tests/WirelessVacuumPipePhase4Regression.ps1`
- Validation coordinator: `tests/Run-WirelessVacuumPipePhase4Validation.ps1`
- Log prefixes: `[ScrapLab Pipe Phase 4]` and `[ScrapLab Pipe Transfer]`
