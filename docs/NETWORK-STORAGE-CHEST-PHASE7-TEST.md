# Network Storage Chest — Phase 7 Release Qualification

**Completed 2026-08-14:** functional suite 73 passed, 0 failed, 1 skipped;
500-container soak 8 passed, 0 failed. All 502 temporary parts were removed,
and the receipt-backed test layer was uninstalled after log validation.

Phase 7 uses a temporary, receipt-backed qualification layer. It creates and
removes every test fixture automatically. You do not need to build anything or
move any items.

## Before starting

1. Let ScrapLab install the Phase 7 qualification layer while the game is
   closed.
2. Launch Scrap Mechanic normally and enter the Survival world you use for
   testing.
3. Stay in that world until each command reports its final summary.

## Commands

Run:

```text
/slstorage auto all
```

Wait for `ALL SUMMARY`. This chains local withdrawals, concurrency, smart
deposit routing, the final GUI/localization checks, and wireless routing. Every
station removes itself before the next one starts.

Then run:

```text
/slstorage soak
```

The soak test incrementally creates 500 containers and two terminals. It checks
closed-terminal idle cost, cold and warm indexing, one-container revision
rescans, shared cache reuse, five-slot buffer refresh persistence, bounded cache
pruning, and incremental cleanup. The final message must say that all 502
temporary parts were removed.

If the game closes during the soak, return to the same world and run
`/slstorage soak` again. The harness first removes the interrupted fixture using
its saved shape IDs, then starts a clean run.

After both summaries appear, close Scrap Mechanic completely. ScrapLab will
inspect the log, remove the temporary qualification layer, restore the exact
pre-test `SurvivalGame.lua`, and perform the final build, Defender, and package
checks.
