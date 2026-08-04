# Wireless Vacuum Pipe — Phase 1 Cross-World Safety Spike

## Status

**COMPLETE — 10 PASSED, 0 FAILED (2026-08-04)**

The verified `game-20260804-021017.log` run passed all eight automatic cases, process save/reload persistence, and the single-account network loopback. Phase 2 is unlocked. A true connected second-client observation remains a mandatory Phase 7 release check because only one Steam account is currently available.

## Verified result

| Gate | Result | Evidence |
|---|---|---|
| Normal cross-world commit | PASS | `9/1` after moving one |
| Destination exactly full | PASS | commit succeeded at `0/200` |
| Destination already full | PASS | commit rejected at `1/200` |
| Source changed after selection | PASS | stale transfer rejected at `1/0` |
| Receiver handle released before commit | PASS | atomic commit produced `1/1` |
| Error before commit | PASS | abort preserved `2/0` |
| Error after commit | PASS | exactly-once result `1/1` |
| Endpoint destroyed after selection | PASS | fresh resolve rejected route; source stayed `1` |
| Save, exit, and reload | PASS | counts remained `2/1` |
| Host network loopback | PASS | client→server→client→server returned `2/1` |

Final probe status: `10 passed, 0 failed; pending reload=false`.

This probe deliberately uses two disposable vanilla **Piped Small Chests**, so the temporary endpoints visibly have native pipe ports. The ports are not part of the transaction test: Phase 1 directly proves cross-world container atomicity before Phase 3 adds virtual pipe traversal. The probe does not register the permanent Wireless Vacuum Pipe UUID, patch native pipe consumers, or install any production feature.

## What the probe proves

- The source endpoint is created only in the overworld.
- The destination endpoint is created only in an underground world.
- Both endpoint cells are retained through `loadCellWithHandle`, even after the player leaves that world.
- A single global container transaction queues the source spend and remote destination collect.
- Failed transactions preserve both sides; successful transactions change both sides exactly once.
- A persisted journal checks the same counts after a full save, exit, and reload.
- The one-account loopback sends a nonce and exact counts client→server→client→server and verifies the returned server-authoritative state.
- The actual connected second-client comparison remains deferred—not marked passed—until Phase 7 release validation.

The probe uses Circuit Boards (`f152e4df-bc40-44fb-8d20-3b3ff70cdfe3`) as harmless test cargo and vanilla Piped Small Chests (`4c474cff-3f6a-4306-93d1-c4c74578afd2`) as temporary endpoints.

The first installed probe exposed a reload-only bug: it assigned the boolean return from `sm.world.loadWorld` over the saved World reference. Version 2 keeps the original World userdata, discards only endpoints corrupted by version 1, preserves recorded test results, and asks the host to recreate the two endpoint chests.

The next real run exposed two additional probe/architecture details, corrected in version 3:

- Piped Small Chests report a technical stack ceiling of 65,535, which is not the selected item's actual capacity. The exact-full cases now binary-search with `Container.canCollect`, using the engine's authoritative admission result.
- A cached Container can still accept a transaction during the tick its Shape is queued for destruction. The production design now selects endpoint IDs first, waits until the next scheduler tick, freshly resolves both endpoints, and commits only if every fresh guard passes. The destruction case proves that the stale route is rejected and the source remains unchanged.

Version 3 also spawns the disposable probe endpoints as static Shapes so they cannot fall away from their persisted coordinates while the host travels between worlds.

## Files

- Runtime probe: `source/Patching/Parts/WirelessVacuumPipe/ScrapLabPipePhase1Probe.lua`
- Machine-readable gate: `source/Patching/Parts/WirelessVacuumPipe/WirelessVacuumPipe.phase1.json`
- Reversible installer: `tools/experiments/Manage-WirelessVacuumPipePhase1Probe.ps1`
- Log reader: `tools/experiments/Read-WirelessVacuumPipePhase1Results.ps1`
- Regression: `tests/WirelessVacuumPipePhase1Regression.ps1`

## Safety

- Use a disposable copy of a Survival world, not an important save.
- Scrap Mechanic must be closed while installing or removing the probe.
- Installation creates and SHA-256 verifies a timestamped `SurvivalGame.lua` backup.
- The installer appends one isolated `dofile` block and leaves existing ScrapLab patches intact.
- Removal restores the exact original bytes when no later edit occurred; otherwise it removes only the intact probe block.
- The owned probe script is removed only when its checksum still matches the installation receipt.
- `core_data.cbo` is deleted only after a verified install or removal.
- `/slpipeprobe cleanup` destroys only the two probe-created chests and releases their handles.

## In-game gate procedure

1. Install the probe while Scrap Mechanic is closed.
2. Open a disposable Survival test world in the overworld.
3. Run `/slpipeprobe source`. An orange piped source chest appears in front of the host.
4. Travel through an underground elevator into an underground world.
5. Run `/slpipeprobe destination`. A cyan piped destination chest appears in front of the host.
6. Run `/slpipeprobe runall`. Seven cases finish synchronously; the endpoint-destruction guard reports `PENDING` and then completes after its fresh-resolution tick.
7. Confirm all eight automatic cases report `PASS`:
   - `normal`
   - `exact-full`
   - `already-full`
   - `source-changed`
   - `receiver-unload`
   - `error-before-commit`
   - `error-after-commit`
   - `endpoint-destroyed`
8. Run `/slpipeprobe reload`, then save and exit the game immediately.
9. Reopen the same world. The log must report `PASS save-reload gate`; `/slpipeprobe status` must include the recorded pass.
10. Run `/slpipeprobe loopback`. It must report `PASS host-client-loopback` after a full client→server→client→server round trip.
11. Run `/slpipeprobe status`. The Phase 1 gate is valid only with ten passes and zero failures.
12. Run `/slpipeprobe cleanup`, close Scrap Mechanic, and remove the probe.

When a second account becomes available for release validation, the host and client each run `/slpipeprobe observe`, then the host runs `/slpipeprobe observercheck`. That separate check must report `PASS host-client-observation` with at least two matching observers before release.

Individual automatic cases can be repeated with `/slpipeprobe run <case>`. `/slpipeprobe help` prints the compact command list.

## Pass/fail policy

Phase 1 passes only when:

- every successful transaction subtracts and adds exactly once;
- every rejected or aborted transaction changes neither side;
- the post-reload counts exactly match the committed counts;
- the single-account network loopback returns the same nonce and server-authoritative counts;
- the real connected second-client comparison remains explicitly deferred to Phase 7 rather than being reported as passed;
- the game log contains no Lua exception from the probe outside the two deliberately caught error cases.

Any failure stops the transparent cross-world PIPE LINK design. The project must not proceed to Phase 2 by weakening this gate or silently substituting item spawning, shared fake inventories, or a non-durable transfer.

## Development commands

From the publication-kit root in an elevated PowerShell:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\experiments\Manage-WirelessVacuumPipePhase1Probe.ps1 -Action Install
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\experiments\Read-WirelessVacuumPipePhase1Results.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\experiments\Manage-WirelessVacuumPipePhase1Probe.ps1 -Action Remove
```

The repository regression is:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\WirelessVacuumPipePhase1Regression.ps1
```
