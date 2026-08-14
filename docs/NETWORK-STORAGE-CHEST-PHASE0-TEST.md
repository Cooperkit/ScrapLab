# Network Storage Chest — Phase 0 in-game test

## Purpose

This test is the release gate for Phase 0 only. It proves the reused piped Small Chest model, custom script, 60-item scrolling catalog, item-icon atlas lookup, button callbacks, search callbacks, localized title lookup, JSON container transactions, and persistence of the real 5-slot engine container.

Phase 0 intentionally uses sample catalog data. It does not scan pipe networks, withdraw items, or sort deposits.

## Test procedure

1. Start Scrap Mechanic normally. Do not add `-dev`.
2. Load a Survival test world.
3. Run:

   ```text
   /slstorage0 spawn
   ```

4. Confirm one orange piped Small Chest appears in front of the player. It must use the exact vanilla Small Chest model with one top Vacuum Pipe port.
5. Interact with it and verify:

   - the custom capability-probe screen opens, not the ordinary vanilla **CHEST / BACKPACK** screen;
   - the title says **NETWORK STORAGE CHEST**;
   - the subtitle clearly says this is a Phase 0 sample-data probe;
   - the catalog reports **SHOWING 60 OF 60**;
   - the visible item icons use compact inventory-style slots;
   - the mouse wheel and the custom scrollbar reach every catalog row;
   - the right side contains the player inventory followed by a 5-slot deposit buffer;
   - both right-side grids show real items and empty slots, never placeholder orange dots, `999` captions, or blank panels;
   - the status says **MODEL + SCRIPT + BUFFER READY**.

6. Scroll to the final row, then click at least three different catalog icons. Each entire slot must be clickable and both the selected-item panel and bottom line must immediately change to the localized name.
7. Type `metal` into the search field. Matching items must rebuild from the first slot with no blank gaps. Cycle the sort control through **VALUE**, **NAME**, and **COUNT**, press Enter once, then click **CLEAR** and confirm all 60 items return.
8. Move one ordinary test stack from the player inventory into the deposit buffer.

   - The item must remain in the buffer.
   - Nothing should be sorted, consumed, copied, or moved elsewhere.
   - The bottom line should report the deposit-grid callback.

9. Close the panel and reopen it. Confirm the exact same item and quantity remain in the same deposit slot.
10. Run:

    ```text
    /slstorage0 status
    ```

    The result must begin with **PASS** and report `slots=5`.
11. Quit Scrap Mechanic completely, launch it again, reload the same world, and reopen the same chest. Confirm the exact item and quantity still remain in the deposit buffer.
12. Move every test item back into the player inventory, then run:

    ```text
    /slstorage0 cleanup
    ```

    Cleanup must remove the empty probe. It must refuse if the deposit buffer still contains anything.

## Result reporting

After completing the test, report:

- whether every step passed;
- the exact step number of any failure;
- what appeared on screen;
- whether the game crashed or the GUI became stuck.

ScrapLab will inspect both game logs before Phase 0 is marked complete. Phase 1 must not begin until close/reopen and full save/reload persistence both pass.

## Completed validation

Phase 0 passed every manual test on 2026-08-13, including the 60-item catalog, scrolling, compact search reflow, inventory transactions, close/reopen persistence, full save/reload persistence, status checks, and safe cleanup. ScrapLab inspected `game-20260813-180347.log` and `game-20260813-181211.log`; no Network Storage Chest callback failure was present.
