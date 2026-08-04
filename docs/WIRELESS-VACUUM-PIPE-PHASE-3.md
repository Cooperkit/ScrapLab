# Wireless Vacuum Pipe — Phase 3 Validation

## Current state

Phase 3 definition 10 replaces the long manual machine checklist with layered automatic validation and requires no player-built test station. Source regression proves every protected consumer call, install/migration path, rollback, and byte-exact removal in isolated fixtures. The in-game `/slpipe3 auto` command creates its own temporary linked Water Container rigs, waits for the engine and manager, runs every topology and resource query check, then removes the fixtures and prints the result.

**Phase 3 is complete as of 2026-08-04.** The final automatic run in `game-20260804-154523.log` reported **11 passed, 0 failed, 0 skipped**. The desktop coordinator independently reported **6 passed, 0 failed, 1 not-applicable Flat Vacuum skip**. A post-run save inspection found no temporary fixture IDs and no pending cleanup receipt. Phase 4 is unlocked.

The corrected Link graph run recorded **9 passed, 0 failed** in `game-20260804-045233.log`, including one cross-world endpoint and one remote Piped Small Chest. A follow-up save inspection found that world unload invoked script teardown and the endpoint's unguarded `server_onDestroy` deleted its persistent manager row. The endpoint now uses the same unload-versus-real-destruction guard as vanilla persistent parts and performs one final state refresh before unloading.

The final no-revisit test passed on 2026-08-04. After a full game restart, the manager restored both saved endpoints and each world reported its cross-world match immediately; the player did not need to revisit the remote world to repopulate the manager. This closes the endpoint lifecycle regression.

Definition 10 patches all eight planned native consumers: Crafter, Flat Vacuum, Garage Chest, Ore Crusher, Prospector, Refinery, Vacuum, and the shared resource-container utilities. It does not add the autonomous Send/Receive transfer system; that remains Phase 4 work.

The first full-consumer run exposed two integration defects. Craftbot's client GUI queried only its locally visible graph and disabled crafting before the server received a request, even though the server could see linked storage. The shared wrapper also resolved `sm.interactable.connectionType` at file load, but terrain-generation Lua states intentionally lack `sm.interactable`. Definition 3 added an on-demand, server-authoritative GUI container sync and lazy resource-type binding.

The next Craftbot test exposed a class-registration defect: the new network callbacks were declared after `Craftbot = class( Crafter )`. Scrap Mechanic copies the base class at subclass creation, so the live Craftbot never received those late callbacks and logged `callback does not exist`. Definition 4 places the complete bridge before every Crafter subclass declaration and logs both sides of each GUI synchronization under `[ScrapLab Pipe Crafter]`. It creates no mirrored inventory and performs no GUI-side item mutation.

Definition 4's generated diagnostics accidentally contained the literal PowerShell escape text `` `t `` at the beginning of two Lua lines. That syntax error prevented `Crafter`, `Craftbot`, and the other subclasses from loading. Definition 5 repairs both lines, keeps the callback-order fix, and adds a fixture migration plus a generated-Lua guard that rejects literal escape text.

Definition 6 restores directional machine boundaries. The earlier virtual traversal treated a neutral Wireless Pipe as proof that every remote chest was valid for both input and output, allowing a Craftbot to deposit finished parts backward into its ingredient network. The graph now classifies the machine's first physical branch from its official pipe-opening order and runtime opening positions. Input discovery uses only input branches, output discovery uses only output branches, and traversal stops at other directional machines instead of crossing through them.

The definition 6 Craftbot test passed in both the same world and across the Overworld/Underground boundary. Ingredients came from the linked input network and completed output no longer returned to the input chest. Log `game-20260804-130259.log` showed no callback or Craftbot class-load failures, but it exposed a visual-only error when vanilla tried to animate an impossible physical path to a wireless chest. Definition 7 adds a central two-shape minimum guard to `PipeEffectPlayer`; wireless transfers skip that invalid animation instead of repeatedly failing in `ValidatePath`.

The definition 7 confirmation run in `game-20260804-131214.log` contains no `ValidatePath`, missing callback, missing Crafter class, missing Craftbot class, or ScrapLab pipe failures. The Craftbot Phase 3 consumer is complete. Remaining tracebacks are unrelated vanilla achievement, Vault, and unit callbacks that also occur with core files modified.

The first Vacuum matrix exposed a multi-Link bus defect. The manager correctly reported two remote peers, but the graph exposed only one registered container. Definition 8 no longer assumes that the engine's native query completely represents the origin-side network of a neutral Link pipe. It builds a deterministic, deduplicated union from every reachable origin and peer Link root, while preserving native local results first and retaining machine direction barriers. This makes several matching Links behave as one conjoined inventory and lets a pump continue to the next non-empty container. The harness now records `multi-link-container-union` and logs the container count behind each linked root.

Definition 9 added a read-only `resource-container-union` check for the `getMatchingPipedContainers` path used by Prospector and shared resource helpers. Definition 10 makes that test self-contained: it imports two empty, disposable Water Container/Link rigs, uses a previously discovered remote world when one exists, and otherwise performs the same-world suite without requiring the player to place anything. A persisted cleanup receipt identifies only the temporary shape IDs; interrupted runs are cleaned on the next load. The desktop `tests/Run-WirelessVacuumPipeAutoValidation.ps1` coordinator runs all three phase regressions, verifies the installed receipt, detects unavailable game content, and reads the latest in-game summary and wireless safety log.

The shipped `FlatVacuum.lua` is currently orphaned content: no placeable shape in this build registers that script. Flat Vacuum gameplay tests are therefore **SKIPPED / NOT APPLICABLE**, not failures. Its protected call sites remain covered automatically so a future game build can expose the part without requiring a new graph patch.

Multi-output destination selection now combines duplicate UUID requests, applies the engine's live filter/capacity checks, and conservatively accounts for distinct outputs competing for the same empty slots. It performs no inventory mutation and is safe when called from inside Refinery's existing container transaction.

## Implemented contracts

- Vanilla local pipe results are queried first and retain their original order.
- Virtual results are added only through enabled, live `LINK` endpoints of the same paint channel.
- Remote endpoint cells must be ready and inside the manager's bounded ownership policy.
- Traversal uses stable breadth-first ordering, visited endpoint and shape sets, container deduplication, and hard safety limits.
- The manager exposes an event-driven topology revision; the wrapper caches only peer topology, never item counts or container capacity.
- Manager absence, loading, traversal failure, or conflicting topology returns the untouched native result.
- Cross-world effects never receive an impossible direct world-to-world visual path.

## Re-running the in-game validation

1. Fully restart Scrap Mechanic so definition 10 loads.
2. Stand anywhere with a live character and run `/slpipe3 auto`.
3. Wait for the summary. Do not place, paint, fill, or connect anything.

Expected result: **11 passed, 0 failed, 0 skipped** when the save already contains a discovered second world. A new save with no remote-world record reports **10 passed, 0 failed, 1 skipped** for the cross-world check. The temporary containers are empty, inventory contents are never changed, and every spawned fixture is removed automatically.

## Completed automatic validation gate

The distinct live engine paths already have representative proof: Craftbot input/output passed in the same world and cross-world, normal Vacuum input/output passed, the cross-world restart/no-revisit case passed, and the multi-Link rollover passed with multiple containers. Refinery and Ore Crusher use the already-proven output selector and vanilla transactions; Garage uses the already-proven input listing; Prospector's unique resource-container query is now exercised directly by the automatic harness.

Run `/slpipe3 auto` once after a full restart, then close the game normally and run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Run-WirelessVacuumPipeAutoValidation.ps1
```

The gate passed on 2026-08-04: the in-game run reported **11 passed, 0 failed, 0 skipped**, and the desktop coordinator reported **6 passed, 0 failed, 1 not-applicable skip**. Phase 4 is unlocked. The former manual matrix remains in [`WIRELESS-VACUUM-PIPE-PHASE-3-REMAINING-TESTS.md`](WIRELESS-VACUUM-PIPE-PHASE-3-REMAINING-TESTS.md) only as an optional targeted diagnostic when a future regression points to a specific machine.

## Developer files

- Graph wrapper: `source/Patching/Scripts/ScrapLab/PipeSystem/ScrapLabPipeGraph.lua`
- Harness: `source/Patching/Parts/WirelessVacuumPipe/WirelessVacuumPipePhase3Harness.lua`
- Install/status/remove: `tools/experiments/Manage-WirelessVacuumPipePhase3.ps1`
- Source regression: `tests/WirelessVacuumPipePhase3Regression.ps1`
- Automatic coordinator: `tests/Run-WirelessVacuumPipeAutoValidation.ps1`
- Game-log prefixes: `[ScrapLab Pipe Phase 3]` and `[ScrapLab Pipe Crafter]`
