# Wireless Vacuum Pipe — Phase 2 Validation

## Current state

Phase 2 is complete. The source regression and clean install/remove fixture pass, including byte-exact restoration of every modified registration file.

The primary in-game run recorded **7 passed, 0 failed** in `game-20260804-040620.log`. After fully restarting Scrap Mechanic, `game-20260804-041446.log` again recorded **7 passed, 0 failed** with three saved endpoints restored, two ready cross-world cell handles, no reconciliation backlog, and the tracked endpoint retaining its identity from the Overworld to Underground depth 1.

No Wireless Pipe manager, endpoint, GUI, duplicate-ID, or handle-limit errors occurred. The logs contain unrelated vanilla elevator invalid-script-reference messages and Seedbot errors; none reference ScrapLab files or coincide with a failed Phase 2 invariant.

Phase 3 pipe-query wrappers and Phase 4 item movement are intentionally absent.

## Implemented contracts

- Permanent part UUID: `a34d9af0-4ba0-431d-b647-2d5435ecf138`.
- Permanent manager UUID: `8a6e31c4-575f-40fa-96f3-85bd23eb34ce`.
- One physical part reusing Vacuum Pipe 1's renderable, collision, rotation, and two pipe openings.
- One optional logic input and no logic output.
- Persistent endpoint identity and `LINK`, `SEND`, or `RECEIVE` mode in the part's own storage.
- Save-owned serializable manager registry and runtime-only Shape, Interactable, group, and handle references.
- Duplicate endpoint-ID rejection with regeneration for copied creations.
- Explicit unload versus destruction behavior.
- Paint polling using the full RGBA hex value as the channel.
- Deterministic mode/color indexes and cross-world status labels.
- One reference-counted handle per unique `world:cellX:cellY` key.
- Active groups are admitted before reconciliation work under a hard 64-cell cap.
- A five-second idle grace before unused handle release.
- Startup records are removed only after their saved cell loads and an 80-tick confirmation window expires.
- Failed world loads retain the record and retry later.
- `server_onWorldChanged` plus throttled world/cell/position polling covers elevator and moving-creation transitions.

## In-game test sequence

Use a disposable Survival world. The installed developer build exposes `/slpipe2`.

1. Enter the world and run `/slpipe2 status`.
   - Expected: manager available, zero or more saved endpoints, and no Lua error.
2. Run `/slpipe2 spawn static` twice while standing in one cell.
   - Both parts start orange and in Link mode.
   - After a short wait, `/slpipe2 status` should report `LINKED`, one match, and one owned cell handle shared by both endpoints.
3. Interact with each part using `E`.
   - Confirm the custom panel opens.
   - Switch one part to `SEND` and the other to `RECEIVE`.
   - Expected status: `SENDING` and `READY TO RECEIVE`.
4. Paint one endpoint a different color, then paint it back.
   - Expected: the endpoints leave and rejoin the group without changing UUID or mode.
5. Connect a switch to one endpoint and turn it OFF, then ON.
   - Expected: `DISABLED BY LOGIC`, then its normal grouped status.
6. Run `/slpipe2 stale` and wait about three seconds.
   - Expected: `PASS startup reconciliation`.
7. Run `/slpipe2 run`, then `/slpipe2 results`.
   - Expected: manager invariants, the 64-cell bound, shared-cell ownership, and reconciliation report PASS.
8. For moving-creation validation, spawn or place an endpoint on a movable creation, stand near it, and run `/slpipe2 track`.
   - Move it more than one meter. Expected: `PASS moving-creation-position`.
   - Move it across a 64-meter cell boundary. Expected: `PASS moving-creation-cell`.
9. Keep the same tracked endpoint on a creation that an underground elevator transfers between worlds.
   - Expected after transfer: `PASS elevator-world-change` and the same endpoint ID with an updated friendly world label.
10. Save, quit completely, reopen the same world, and run `/slpipe2 status` and `/slpipe2 results`.
    - Expected: endpoints confirm their saved records, groups rebuild, and no duplicate or stale entry appears.

When testing is finished, run `/slpipe2 cleanup` in every world containing a test endpoint before removing the Phase 2 build.

## Developer scripts

- Install/status/remove: `tools/experiments/Manage-WirelessVacuumPipePhase2.ps1`
- Source regression: `tests/WirelessVacuumPipePhase2Regression.ps1`
- Game-log prefix: `[ScrapLab Pipe Phase 2]`

The installer stores checksum-verified backups and one development receipt under local ScrapLab application data. It deletes `core_data.cbo` only after verified file changes.
