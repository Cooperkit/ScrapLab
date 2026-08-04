# Wireless Vacuum Pipe — Phase 0 Record

## Result

Phase 0 is complete. The Wireless Vacuum Pipe now has a permanent identity, locked survival balance, approved optional logic behavior, and a validated transparent inventory icon. No Scrap Mechanic game files were changed during this phase.

## Required plan reread

Before beginning this phase, the complete 927-line `WIRELESS-VACUUM-PIPE-MOD-PLAN.md` was reread from disk. Work was kept within Phase 0; the cross-world transaction prototype and runtime manager remain Phase 1 work.

## Locked identity

| Field | Locked value |
|---|---|
| Displayed name | **Wireless Vacuum Pipe** |
| Uppercase title | `WIRELESS VACUUM PIPE` |
| Internal item name | `obj_pneumatic_pipe_wireless` |
| Lua class name | `WirelessVacuumPipe` |
| Permanent UUID | `a34d9af0-4ba0-431d-b647-2d5435ecf138` |
| Reference model UUID | `59ea6ce8-239b-4eed-8847-a51b907d9b42` — Vacuum Pipe 1 |
| Craftable base pipe UUID | `9b8f2abd-265c-4750-b8b9-fe6cb564633c` — Vacuum Pipe |
| Stack size | 5 |

The permanent UUID was generated once and checked against the complete ScrapLab repository and the installed game's `Survival` and `Data` trees. No collision was found. It must never change after public distribution.

## Locked survival recipe

The recipe is default-unlocked in `craftbot_core`, beside the normal Vacuum Pipe family.

**Output: 2 Wireless Vacuum Pipes in 30 seconds**

| Ingredient | UUID | Quantity |
|---|---|---:|
| Vacuum Pipe | `9b8f2abd-265c-4750-b8b9-fe6cb564633c` | 2 |
| Component Kit | `5530e6a0-4748-4926-b134-50ca9ecb9dcf` | 2 |
| Circuit Board | `f152e4df-bc40-44fb-8d20-3b3ff70cdfe3` | 4 |

The paired output is deliberate: a wireless network requires at least two endpoints, so the first completed craft is immediately usable. Each endpoint still consumes one physical Vacuum Pipe, while Component Kits and Circuit Boards price the save-wide wireless behavior without making early automation prohibitively expensive.

The installed build confirms that:

- vanilla Vacuum Pipe is already a default-unlocked Craftbot recipe;
- its vanilla recipe costs 2 Metal Block 1 and 5 Glass;
- the Craftbot recipe system accepts placeable parts as ingredients, as used by official engine/controller recipes.

## Locked logic behavior

The optional logic input is approved.

- Maximum parents: 1
- Maximum children: 0
- Input type: logic
- Output type: none
- No logic wire: enabled
- Connected signal ON: enabled
- Connected signal OFF: disabled

This preserves simple placement for ordinary users while allowing automated network shutdown. Disabling an endpoint changes only routing participation; it does not change the paint channel, mode, persistent endpoint ID, or stored manager record.

## Locked baseline balance

- Default mode: `PIPE_LINK`
- Directional Send/Receive attempt interval: every 4 fixed ticks
- Initial active remote endpoint-cell cap: 64

The values are implementation baselines. Phase 1 and later profiling may lower the cell cap or tune throughput before release, but any change must be documented rather than silently altering the Phase 0 manifest.

## Icon selection and validation

Selected direction: **candidate #1 — wireless signal pulse**.

Atlas source:

`source/Patching/Parts/WirelessVacuumPipe/WirelessVacuumPipeIcon.png`

Verification:

| Size | Transparent corner | Visible pixels | Cyan cue pixels | Visible bounds |
|---:|---:|---:|---:|---|
| 24×24 | Yes | 239 | 20 | `3,1` through `20,19` |
| 32×32 | Yes | 407 | 41 | `4,2` through `27,25` |
| 96×96 | Yes | 3,370 | 304 | `14,6` through `81,76` |

The orange pipe silhouette, silver lower band, hollow center, and cyan wireless pulse remain distinguishable at all three tested sizes. The atlas-ready file is 96×96 `Format32bppArgb`, has alpha value 0 in every tested corner, and has SHA-256:

`FE550A6318D707609F03C5FE72449A2694C9832CDBD4B1FD50F20BC3D40A2967`

Preview files:

- `docs/images/wireless-vacuum-pipe-icon-selected-24.png`
- `docs/images/wireless-vacuum-pipe-icon-selected-32.png`
- `source/Patching/Parts/WirelessVacuumPipe/WirelessVacuumPipeIcon.png`

## Machine-readable lock

All Phase 0 constants are also recorded in:

`source/Patching/Parts/WirelessVacuumPipe/WirelessVacuumPipe.phase0.json`

Later phase code and tests should consume or verify this file rather than duplicating identifiers and balance constants without a check.

## Exit checklist

- [x] Complete implementation plan reread before phase work.
- [x] Permanent UUID generated and collision-checked.
- [x] Displayed and internal names locked.
- [x] Survival recipe, output quantity, cost, unlock state, and craft time locked.
- [x] Optional-on-by-default logic input approved.
- [x] Directional throughput and cell-cap baselines recorded.
- [x] Candidate #1 promoted to the part source folder.
- [x] Transparency and readability verified at 24, 32, and 96 pixels.
- [x] No Phase 1 runtime or game patch implementation started.

Phase 1 may begin only after the complete implementation plan is reread again.
