# Network Storage Chest - Phase 5 Automatic Test

Phase 5 is installed in the live Scrap Mechanic development build. Nothing needs to be placed or built.

## Run the qualification

1. Start Scrap Mechanic normally and open any Survival world.
2. Open chat and run:

   ```text
   /slstorage5 auto
   ```

3. Stay in the world for a few seconds. ScrapLab creates one temporary Network Storage Chest, checks the real client GUI and icon, and removes the part automatically.
4. The required final result is:

   ```text
   PHASE 5 COMPLETE: 20 passed, 0 failed. Temporary terminal removed.
   ```

The command validates all 11 installed inventory translations, the real part/server/client instance, required widgets, native progress bar, keyboard/controller focus contract, 60-item compact search reflow, all three sort modes, route markers, localized tooltip detail, atlas registration, selection scroll preservation, and an actual create/render/close cycle against real containers. Its live inventory check compares every rendered slot with the player's real limited-inventory container, including hotbar slots `0-9` and every backpack slot after them.

## Visual check

After the automatic result passes, open any placed Network Storage Chest once and confirm:

- the panel remains compact and the old engine hotbar overlay does not flash during interaction;
- exactly one player-inventory scrollbar exists and no duplicate inventory window is visible;
- hotbar items occupy the first two rows and show `1-0` badges, followed by the backpack slots in the same grid;
- selecting an item while scrolled down leaves the catalog at the same position;
- search results immediately close their gaps;
- item cards show `L`, `W`, `M`, or `X` in the upper-left for local, wireless, mixed, or cross-world sources;
- selecting an item shows its icon, quantity, source count, and route type on the right;
- the indexing bar moves while a network refresh is running;
- clicking a non-empty player slot safely stages that stack in the five-slot deposit tray, while empty slots do nothing;
- the deposit tray is exactly five slots;
- the prepared Network Storage Chest icon appears instead of an empty square.

Close the game after the result so the latest log can be checked before Phase 6.
