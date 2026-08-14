# Network Storage Chest — Phase 2 Test

Phase 2 adds server-authoritative local withdrawals. The test is completely automatic: it creates four temporary parts, fills real engine containers, performs the transfers, checks every item count, and removes the station.

## Run the automatic qualification

1. Install the current probe build and launch a Survival world.
2. Stand in an open area.
3. Run:

   ```text
   /slstorage2 auto
   ```

4. Do not pick up or alter the temporary station while the command runs.
5. Wait for `PHASE 2 AUTOMATIC TEST COMPLETE` in chat.
6. Close Scrap Mechanic so the newest game log can be inspected.

No chest, pipe, item, second player, or manual test setup is required.

## What the command proves

- Take 1 removes exactly one item and drains the smallest source stack first.
- Take Stack gathers one normal game stack across multiple containers.
- Take All gathers across multiple source containers.
- Take All That Fits clamps to exact destination capacity.
- A full destination spends nothing.
- A stale source revision aborts without moving anything.
- Two same-time requests cannot claim the same final item.
- An unavailable UUID spends nothing.
- Every scenario verifies total source-plus-destination conservation.
- Session tokens rotate, invalid tokens expire, stale catalog generations are rejected, and rapid repeat requests are rate-limited.
- The disposable station and all test items are removed at completion.

If the game closes during a run, running `/slstorage2 auto` again in the same world removes the interrupted station before starting a fresh test.

## Short manual UI check

After the automatic test passes, connect a Network Storage Chest to an ordinary local piped chest and open its panel:

1. Select an item card.
2. Confirm all three withdrawal controls enable only after the catalog says `READY`.
3. Try `TAKE 1`, `TAKE STACK`, and `TAKE ALL THAT FITS`.
4. Confirm the transfer message is readable and the catalog total refreshes.
5. Fill the player inventory and confirm a rejected transfer removes nothing.

The manual check is only visual/player-inventory confirmation; the conservation and concurrency work is covered by the automatic command.
