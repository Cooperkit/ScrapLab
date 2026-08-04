# Wireless Vacuum Pipe - Remaining Phase 3 Tests

## What this test run proves

This checklist is retained only for targeted diagnostics. Definition 10 replaced it as the release gate with a self-building `/slpipe3 auto` fixture plus `tests/Run-WirelessVacuumPipeAutoValidation.ps1`. Do not build this setup or run the whole matrix unless an automatic check or a real gameplay report identifies a specific consumer.

The shipped `FlatVacuum.lua` has no registered placeable shape in the current build. F1-F5 are not applicable unless a future update exposes that part.

This run covers:

- Vacuum input and output;
- Flat Vacuum pickup collection;
- Refinery output overflow;
- Ore Crusher output overflow;
- Prospector resource input, connected water, and output;
- Scrap City Garage Chest resource listing;
- local priority, color isolation, capacity/filter rejection, and restart-without-revisit behavior.

`SEND` and `RECEIVE` are Phase 4 features. Leave every endpoint in `LINK` mode for this run.

No multiplayer account is required. Cross-world tests use one player moving between the Overworld and Underground.

## Pass rule

A test passes only when all expected item counts and world objects match exactly. A working animation by itself is not a pass.

Stop after any of these failures:

- an item disappears without reaching its destination;
- an item appears twice;
- a machine takes input even though every destination is full or incompatible;
- a local-only machine stops working;
- the game crashes or logs a new ScrapLab pipe error.

Leave the failed setup untouched, close the game normally, and report the test ID. That preserves the best log and save evidence.

## Test station setup

Use a disposable test save and make these two stations.

### Station A - machine station

- The machine being tested.
- One Wireless Vacuum Pipe connected through ordinary Vacuum Pipes.
- The Wireless Vacuum Pipe is in `LINK` mode.
- A button or switch when the machine needs logic power.

### Station B - storage station

- One Wireless Vacuum Pipe in `LINK` mode.
- One real **Piped Small Chest** connected through its pipe port. An ordinary chest does not count.
- Keep the storage endpoint the same paint color as the machine endpoint.

For same-world tests, keep Station B far enough away that no normal pipe connects the stations. For cross-world tests, move or rebuild Station B in the Underground.

For machines with separate input and output pipe sides, use two wireless channels:

- **Blue** for input;
- **Yellow** for output.

Use the same physical machine side that works in the local baseline. Do not bridge the blue and yellow pipe networks together.

Before each operation, write down the exact source and destination counts. Use one input item or one short button pulse whenever possible.

## P0 - clean preflight

1. Fully close and restart Scrap Mechanic.
2. Load the test world.
3. Stand beside a Wireless Vacuum Pipe and run:

   ```text
   /slpipe3 auto
   ```

Expected:

- `definition 9` is active;
- the endpoint reports its correct world, color, mode, and matches;
- the structural harness reports **11 passed, 0 failed**, including `multi-link-container-union` and `resource-container-union`;
- there is no `WIRELESS MANAGER UNAVAILABLE` message.

Result: [ ] PASS  [ ] FAIL

## V - Vacuum tests

Use a normal Vacuum, not a Flat Vacuum. A short button pulse is safer than holding a switch ON because it produces one easy-to-count operation.

### V1 - local outgoing baseline

1. Disconnect or repaint the wireless endpoint so there is no match.
2. Put exactly 2 Potatoes in a local Piped Small Chest connected to the Vacuum's input side.
3. Set the Vacuum to outgoing mode and aim into open space.
4. Pulse it once.

Expected: one Potato is fired and the local chest changes from 2 to 1.

Result: [ ] PASS  [ ] FAIL

### V2 - same-world wireless outgoing

1. Remove the local source chest.
2. Put exactly 2 Potatoes in the distant same-world Station B chest.
3. Match the endpoint colors and leave both in `LINK`.
4. Pulse the outgoing Vacuum once.

Expected: one Potato is fired and the remote chest changes from 2 to 1.

Result: [ ] PASS  [ ] FAIL

### V3 - cross-world wireless outgoing

Repeat V2 with the source chest in the Underground and the Vacuum in the Overworld.

Expected: one Potato is fired and the Underground chest loses exactly one Potato.

Result: [ ] PASS  [ ] FAIL

### V4 - same-world wireless incoming

1. Set the Vacuum to incoming mode.
2. Put its nozzle in or immediately above a water source.
3. Use an empty distant same-world Station B chest as the only output.
4. Pulse the Vacuum once.

Expected: the remote chest gains exactly 1 Water.

Result: [ ] PASS  [ ] FAIL

### V5 - cross-world wireless incoming

Repeat V4 with the empty output chest in the Underground.

Expected: the Underground chest gains exactly 1 Water.

Result: [ ] PASS  [ ] FAIL

### V6 - empty-source safety

Run the outgoing Vacuum with its linked source chest empty.

Expected: no projectile, no new item, and no crash. The Vacuum may show its normal empty/error feedback.

Result: [ ] PASS  [ ] FAIL

### V7 - multi-Link container rollover

This is the regression that definition 8 specifically fixes.

1. Connect the outgoing Vacuum to its own blue `LINK` endpoint.
2. Connect a same-world Water Container holding exactly 2 Water to a second blue `LINK` endpoint.
3. Connect a cross-world Water Container holding exactly 2 Water to a third blue `LINK` endpoint.
4. Run `/slpipe3 status` beside the pump endpoint. The log must describe all three linked roots and both registered Water Containers.
5. Hold the Vacuum ON long enough to fire all 4 Water, then turn it OFF.

Expected: one source may drain before the other, but the Vacuum immediately continues with the next non-empty source. Both Water Containers end at 0, exactly 4 Water projectiles fire, and the pump does not stop after the first container empties.

Result: [ ] PASS  [ ] FAIL

## F - Flat Vacuum tests

The Flat Vacuum has one supported storage direction: it collects eligible harvestables from its suction area into output storage. It does not have the normal Vacuum's selectable outgoing mode.

Use a mature Pigment Flower or Cotton Plant positioned inside the Flat Vacuum's collection area.

### F1 - local collection baseline

1. Use only a local Piped Small Chest.
2. Power the Flat Vacuum once while the mature plant is in its collection area.

Expected: the plant is harvested once and its resource appears in the local chest. If seeds drop, record their exact count too.

Result: [ ] PASS  [ ] FAIL

### F2 - same-world wireless collection

Repeat F1 with no local output chest and an empty distant same-world Station B chest.

Expected: the plant disappears once and the correct resource appears once in the remote chest. Any generated seeds must also exist exactly once.

Result: [ ] PASS  [ ] FAIL

### F3 - cross-world wireless collection

Repeat F2 with the output chest in the Underground.

Expected: the Underground chest receives the exact harvest and seed quantities once.

Result: [ ] PASS  [ ] FAIL

### F4 - full-destination safety

1. Fill every slot of the only linked output chest so it cannot accept the plant resource or seeds.
2. Power the Flat Vacuum with a mature plant in range.

Expected: the plant remains harvestable, the chest does not change, and no item is lost or duplicated.

Result: [ ] PASS  [ ] FAIL

### F5 - filtered-destination safety

1. Replace the output chest with a Water Container as the only linked destination.
2. Power the Flat Vacuum with a mature Pigment Flower or Cotton Plant in range.

Expected: the plant remains, and the Water Container receives neither the crop resource nor its seed.

Result: [ ] PASS  [ ] FAIL

## R - Refinery tests

Use a Wood resource rod where possible. One normal Wood rod produces 20 Scrap Wood Blocks. The Refinery always fills its own output inventory first, so wireless output is tested only when that inventory cannot hold the entire next batch.

### Preparing Refinery overflow

First try dragging harmless filler stacks into the Refinery output GUI. If the GUI blocks insertion, let the Refinery produce normally until its five output slots cannot hold another complete 20-block Wood batch. `/unlimited` may be used to obtain test materials, but do not use a command that directly changes the tested source or destination counts.

Record:

- internal output count before the final rod: ______
- remote chest Scrap Wood count before: ______

### R1 - local baseline

With enough room in the Refinery's internal output and no wireless match, refine one Wood rod.

Expected: the rod is consumed once and exactly 20 Scrap Wood Blocks are added internally.

Result: [ ] PASS  [ ] FAIL

### R2 - same-world wireless overflow

With insufficient internal room for the complete batch, connect an empty same-world linked output chest and refine one Wood rod.

Expected: the rod is consumed once; internal plus remote Scrap Wood increases by exactly 20; only the amount that did not fit internally appears remotely.

Result: [ ] PASS  [ ] FAIL

### R3 - cross-world wireless overflow

Repeat R2 with the overflow chest in the Underground.

Expected: total Scrap Wood across the Refinery and Underground chest increases by exactly 20, with no duplicate batch.

Result: [ ] PASS  [ ] FAIL

### R4 - full-output rollback

Make both the internal output and the only linked remote destination unable to fit the next complete batch, then supply one Wood rod.

Expected: production waits or reports full; the Wood rod remains present in the collector/Refinery input total; neither output destination changes.

Result: [ ] PASS  [ ] FAIL

## O - Ore Crusher tests

A basic drill casing is the easiest deterministic input: one tier-1 casing produces exactly 10 tier-1 nuggets. The Ore Crusher also prefers its internal output, so prepare overflow as with the Refinery.

### O1 - local baseline

Crush one tier-1 drill casing with internal output space and no wireless match.

Expected: one casing is consumed and exactly 10 tier-1 nuggets appear internally.

Result: [ ] PASS  [ ] FAIL

### O2 - same-world wireless overflow

Make the internal output unable to hold the complete 10-nugget batch, connect an empty same-world linked output chest, and crush one tier-1 casing.

Expected: one casing is consumed; internal plus remote tier-1 nuggets increases by exactly 10.

Result: [ ] PASS  [ ] FAIL

### O3 - cross-world wireless overflow

Repeat O2 with the output chest in the Underground.

Expected: the total output increases by exactly 10 and only the overflow appears in the Underground chest.

Result: [ ] PASS  [ ] FAIL

### O4 - full-output rollback

Make the internal output and remote output unable to accept the next full batch, then insert one tier-1 casing.

Expected: crushing does not complete and the casing remains in the collector/Ore Crusher input total. No partial output, loss, or duplicate is allowed.

Result: [ ] PASS  [ ] FAIL

## P - Prospector tests

The Prospector accepts Residue Ore, consumes Water while operating, waits about 40-60 seconds, and produces a random result. Because the result is random, record the item and quantity displayed in the finished bucket instead of expecting one fixed item.

Use two different wireless colors so input and output cannot loop into each other.

### P1 - local baseline

1. Put one Residue Ore in a local input chest on the Prospector input side.
2. Supply Water through its normal connected Water Container or internal water inventory.
3. Leave the output local or collect the completed bucket by hand.

Expected: exactly one Residue Ore and the normal amount of Water are consumed, and exactly one random result is produced.

Result: [ ] PASS  [ ] FAIL

### P2 - same-world wireless resource input

1. Put exactly 1 Residue Ore in the blue same-world remote input chest.
2. Leave a free Prospector production slot and provide Water locally.

Expected: the remote chest changes from 1 Residue Ore to 0 and one production slot starts.

Result: [ ] PASS  [ ] FAIL

### P3 - cross-world wireless resource input

Repeat P2 with the Residue Ore chest in the Underground.

Expected: the Underground chest loses exactly one Residue Ore and one production slot starts.

Result: [ ] PASS  [ ] FAIL

### P4 - linked connected-water path

1. Connect the Prospector to an **empty local Water Container** using the normal Connect Tool water connection.
2. Physically pipe that Water Container's network to a blue `LINK` endpoint.
3. Put exactly 40 Water in the matching remote network and place one Residue Ore in the Prospector.
4. Record the remote Water count as soon as production starts and again when the result finishes.

Expected: production starts by spending Water from the linked remote network. The empty local Water Container remains at 0 and acts only as the connection bridge. For one 40-60 second production, the remote supply should lose 17-25 Water, never more than 25.

Result: [ ] PASS  [ ] FAIL

### P5 - same-world wireless output

1. Put an empty yellow linked output chest on the Prospector output side.
2. Wait for one production slot to finish; do not collect the bucket by hand.

Expected: the finished bucket clears automatically and the exact displayed result and quantity appear once in the remote chest.

Result: [ ] PASS  [ ] FAIL

### P6 - cross-world wireless output

Repeat P5 with the yellow output chest in the Underground.

Expected: the finished result appears once in the Underground chest and the Prospector bucket clears.

Result: [ ] PASS  [ ] FAIL

### P7 - no-water safety

Use one Residue Ore but remove all Water from the internal, local connected, and linked remote containers.

Expected: no production progress completes and no result is created. The Residue Ore remains stored by the Prospector rather than disappearing.

Result: [ ] PASS  [ ] FAIL

### P8 - full-output safety

Let one result finish while every yellow output destination is full or incompatible.

Expected: the finished result remains in its Prospector bucket and can still be collected by hand after the test.

Result: [ ] PASS  [ ] FAIL

## G - Scrap City Garage Chest test

This test must be performed at Scrap City Garage because `GarageChest.lua` supplies the tracked-blueprint resource list used by that garage.

### G1 - local listing baseline

1. Track a small saved creation in the Garage interface. Prefer one with a distinctive, easy-to-count part such as a Bearing.
2. Put a known quantity of that part in storage physically piped to the Garage Chest.
3. Open the Logbook Garage page.

Expected: the tracked creation resource line includes the exact local quantity.

Result: [ ] PASS  [ ] FAIL

### G2 - same-world wireless listing

Move that known quantity to a distant same-world chest connected through a matching `LINK`, then reopen the Garage page.

Expected: the resource line still includes the remote quantity exactly once.

Result: [ ] PASS  [ ] FAIL

### G3 - cross-world wireless listing

Move the known quantity to the matching Underground chest, return to Scrap City Garage, and reopen the Garage page.

Expected: the Garage resource line includes the Underground quantity exactly once.

Result: [ ] PASS  [ ] FAIL

If the displayed number is cached, close and reopen the Logbook once. Do not rebuild or revisit the Underground endpoint merely to make the number appear; needing that revisit is a failure.

## S - shared routing safety

These checks use the Vacuum because a one-Potato outgoing pulse and one-Water incoming pulse are easy to count.

### S1 - local-first ordering

Connect one eligible local source chest and one eligible wireless source chest, each with 2 Potatoes. Pulse an outgoing Vacuum once.

Expected: the local chest changes from 2 to 1; the remote chest stays at 2.

Result: [ ] PASS  [ ] FAIL

### S2 - color isolation

Paint the machine endpoint blue and the only storage endpoint yellow, then pulse once.

Expected: no remote count changes. Restore matching colors afterward.

Result: [ ] PASS  [ ] FAIL

### S3 - logic-disable isolation

Connect a logic parent to one endpoint and turn that parent OFF. Pulse the machine once.

Expected: no item crosses that endpoint and the panel reports `DISABLED BY LOGIC`.

Result: [ ] PASS  [ ] FAIL

### S4 - physical-plus-wireless cycle

Create one small legal physical loop that also reaches two same-color Link endpoints. Put exactly 2 Potatoes in one source and pulse an outgoing Vacuum once.

Expected: exactly one Potato is fired, exactly one remains, and the game does not freeze or log recursion/duplicate-container errors.

Result: [ ] PASS  [ ] FAIL

## X - restart without revisiting the remote world

This is the final functional gate.

1. In the Underground remote chest, leave exactly 2 Potatoes and enough free slots for Water.
2. Save while standing in the Overworld beside the Vacuum.
3. Fully exit Scrap Mechanic to desktop.
4. Restart and load the save.
5. **Do not visit the Underground.**
6. In outgoing mode, pulse once. Expect one Potato to fire.
7. Change to incoming mode at a water source and pulse once. Expect one Water to route remotely.
8. Run:

   ```text
   /slpipe3 status
   /slpipe3 run
   /slpipe3 results
   ```

9. Only now visit the Underground and inspect the chest.

Expected:

- Potatoes changed from 2 to 1;
- Water increased by exactly 1;
- the remote endpoint was not revisited before either operation;
- the harness still reports **11 passed, 0 failed**.

Result: [ ] PASS  [ ] FAIL

## Final report

Close the game normally after the final test so the log is complete. Send this compact report:

```text
Phase 3 remaining tests complete
P0: PASS/FAIL
Vacuum V1-V7: __ passed, __ failed
Flat Vacuum F1-F5: __ passed, __ failed
Refinery R1-R4: __ passed, __ failed
Ore Crusher O1-O4: __ passed, __ failed
Prospector P1-P8: __ passed, __ failed
Garage G1-G3: __ passed, __ failed
Safety S1-S4: __ passed, __ failed
Restart X: PASS/FAIL
Failed test IDs: none / ...
Anything visually strange: none / ...
Game fully closed: YES
```

Phase 4 unlocks only after the counts pass and the newest game log contains no new ScrapLab pipe, Lua, assertion, transaction, or path-validation error caused by these tests.
