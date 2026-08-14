# Network Storage Chest — Phase 3 Test

Phase 3 turns the real five-slot deposit tray into a safe local-network auto-sorter.

Run this command in a Survival world while standing in an open area:

```text
/slstorage3 auto
```

The command creates and removes its own terminal, filtered Water Container, and three piped chests. No building or items are required.

It verifies:

- specialized filtered containers are preferred;
- the fullest partial stack is filled first;
- a chest already holding an item beats an empty chest;
- one tray stack can split across multiple destinations;
- partial capacity leaves the exact remainder in the tray;
- a completely full network leaves every deposited item safe;
- a destination revision conflict moves nothing;
- every scenario conserves the exact global item total;
- the terminal buffer is excluded from its own destination list;
- all disposable parts and items are removed.

Wait for `PHASE 3 AUTOMATIC TEST COMPLETE`, close Scrap Mechanic, and inspect the newest game log before unlocking Phase 4.

For destination-ranking diagnostics near a real terminal, toggle:

```text
/slstorage3 debug
```

Debug explanations go to the game log and do not add UUID tooltips to the normal interface.
