# Wireless Vacuum Pipe — Phase 7 Release Validation

**Validated:** 2026-08-04
**Result:** Desktop and single-account release candidate passed; one external
two-player observation remains unavailable on this machine.

## Fresh build

The dependency-free .NET Framework bundle rebuilt successfully with only the
existing unused-field compiler warnings.

| Artifact | Size | SHA-256 |
| --- | ---: | --- |
| `ScrapLab.exe` | 1,626,112 bytes | `3113675571A7E80E4EEE225CEF50CA21699F2FC59A7CCE981887BA6FBA2AA326` |
| `ScrapLab.PatchHelper.exe` | 778,240 bytes | `BCCD08B9C30B6FBCA4672E8404C798BC2CBAF12BCC138654B9C2CD1EEDF8B64E` |
| `ScrapLab.Updater.exe` | 136,704 bytes | `EAF2B94A0911B6B84CBFC980C9C1382CD9B800290B8A1F03562502016EEB2DF5` |
| `ScrapLab-2.5.0.zip` | 819,323 bytes (0.781 MiB) | `BEE39A9D44DC37D816CECF11E4D50340B896A0292F1AABD491212FE70C99DAEB` |

The ZIP contains exactly the three executables and no redistributable runtime,
DLL, webview installer, or package-manager dependency. Every artifact is below
the eight-megabyte limit.

## Automated matrix

All 18 desktop regression entry points passed on the fresh bundle:

- adaptive mod installation, migration, removal, rollback, and composition;
- app update, companion boundary, product migration, and world-action locks;
- crop release and dropped-item read/edit/backup integrity;
- every performance scan, ranking, paging, export, cancellation, and operation
  lifecycle test;
- Raid Detector and shared icon-atlas composition;
- Wireless Vacuum Pipe Phase 1–4 source/runtime contracts;
- the 33-target production patch service, including failure injection after
  every write position, exact and surgical removal, adaptive future builds,
  tamper blocking, Steam overwrite detection, and shared-state rollback;
- Phase 6 Patch Bay, Field Manual, JavaScript, protocol, and live status.

The evolving dropped-item gameplay save no longer contained the original fixed
five-stack snapshot. Its regression was made fixture-tolerant while retaining
the same safety assertions: opt-in scanning, icons, value order, dynamic expired
counts and quantities, individual and clear-all behavior, verified backups, raid
preservation, source immutability, and final SQLite integrity.

One unrelated legacy `/fly` migration fixture was unavailable and was reported
as skipped by the adaptive suite; the current adaptive test suite still passed.

## In-game evidence audit

The original final gameplay logs are present and retain their exact summaries:

| Runtime gate | Evidence | Result |
| --- | --- | ---: |
| Cross-world transaction spike | `game-20260804-021017.log` | 10 passed, 0 failed |
| Endpoint/manager final run | `game-20260804-040620.log` | 7 passed, 0 failed |
| Endpoint/manager full restart | `game-20260804-041446.log` | 7 passed, 0 failed |
| Virtual Link graph | `game-20260804-154523.log` | 11 passed, 0 failed, 0 skipped |
| Directional Send/Receive | `game-20260804-163835.log` | 10 passed, 0 failed, 0 skipped |

That is 45 recorded gameplay passes and zero failures. No error line in those
logs names a ScrapLab wireless runtime file. The logs do contain unrelated base
game asset/cache, elevator, and Seedbot errors, so the audit deliberately uses
path-specific attribution instead of pretending the entire game log is clean.

The shipped Flat Vacuum script has no registered placeable shape in this build.
Its gameplay case remains not applicable, while all of its protected wrapper
sites pass source and patch-service regression coverage.

## UI and assets

- Embedded JavaScript passed Node syntax validation.
- The portable helper contains all seven owned wireless runtime, shape, layout,
  and icon resources.
- The selected icon is 96×96 and contains both visible and fully transparent
  pixels.
- Shared-atlas tests proved that managed tiles compose with Raid Detector and
  that pixels outside ScrapLab-owned cells remain unchanged.
- The production helper now accepts both a verified installed state and a
  verified clean/reinstallable state. After the user's removal restored all
  official registrations but retained exact owned development assets, the
  status correctly reports `KNOWN CLEAN` instead of a partial atlas conflict.

## Microsoft Defender

Microsoft Defender platform `4.18.26060.3008-0` scanned both the complete `dist`
directory and the portable ZIP with remediation disabled. Both scans completed
with **no threats found**.

## Remaining external gate

The implementation plan explicitly requires a real connected second client to
observe a cross-world transaction before public release. Only one Steam account
is available on this machine. The host client→server→client→server nonce and
exact-count loopback passed, but the plan says that loopback does not waive the
two-player observation.

Therefore the code, package, automated matrix, single-account gameplay matrix,
size, and malware scan are release-clean, but the strict public-release gate is
still conditional on one real two-player observation. No version was changed and
nothing was published during this phase.
