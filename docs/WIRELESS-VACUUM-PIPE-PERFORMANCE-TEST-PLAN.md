# Wireless Vacuum Pipe Performance Investigation and Test Plan

## Definition-17 bounded Prospector retries (implemented and verified)

Two definition-16 runs completed **9 passed, 0 failed** with no ScrapLab
traceback and exact route-call reductions. Every hot stage made 960 Vacuum
input-route calls and 360 output-route calls instead of definition 15's 4,800
and 1,800. Both runs had independently unstable host frame rate (7.73% and
6.42% paired-baseline drift), so neither is accepted as a final FPS comparison.
Their server attribution is nevertheless consistent: Vacuum work fell while
two Prospectors repeatedly spent roughly 72–94 ms per sample searching the
same 128-chest input network.

Source inspection found that `CollectionTickDelay` already declares a
0.4-second retry interval, but `lastCollectionTick` advances only after a
successful pickup. Once that initial interval expires, an empty or nonmatching
network is therefore rescanned every 40 Hz fixed update. Completed items have
the equivalent problem when every output is full: `doneTick` is not advanced,
so the same output route is searched every tick.

Definition 17 enforces the delay that the vanilla code already declares:

1. Record the input-attempt tick before searching the live ordered input
   containers. A failed search retries after 16 ticks instead of on the next
   tick; a successful native transaction retains the same timing.
2. Record the completed-output attempt tick before searching live ordered
   output containers. Full outputs retry after 16 ticks while completed items
   remain stored and collectable.
3. Do not cache inventory contents, slots, quantities, or transaction
   authority. Every permitted attempt still queries live containers and uses
   the original native transaction logic.
4. Recognize the exact definition-16 Prospector integration and migrate only
   its two protected retry sites through the normal atomic update path. Edited,
   partial, or duplicated sites remain blocked.

Acceptance requires definition-16 migration, exact removal, rollback and
tamper regressions, portable builds, and a full `/slpipeperf auto` run. In the
unchanged empty-input benchmark fixture, the expected Prospector input-route
count falls from roughly 1,200 toward 75 per 15-second hot stage.

### Definition-17 measurement

The full definition-17 benchmark completed **9 passed, 0 failed**. Prospector
input-route calls fell from 1,200 to 74–76 per hot stage, matching the expected
16-tick cadence, and complete Prospector fixed-update time fell from 72–94 ms
to 13–17 ms. Vacuum calls remained at the definition-16 target of 960 input and
360 output queries. Inventory quantities and item-type signatures remained
conserved, directional transfers committed successfully, and Wireless Pipe
Manager invariants passed.

The run's FPS result is intentionally rejected: baseline A measured 223.09 FPS
and cleanup baseline B measured 174.73 FPS, a 24.31% drift. Log inspection
showed that harness definition 4 destroyed 64 populated native chests directly,
emitting hundreds of nonempty-container warnings immediately before baseline
B. Shape-by-shape removal also allowed queued callbacks to reference scripts
that had already been destroyed. Those are benchmark-cleanup artifacts, not a
definition-17 routing failure.

Harness definition 5 fixes the measurement procedure before any further
production optimization:

1. Transactionally remove every test item from each benchmark-owned container.
2. Remove each imported disposable creation once with the native whole-creation
   operation instead of destroying every shape separately.
3. Preserve the exact cleanup receipt and fail safely if any container cannot
   be emptied or any creation cannot be removed.
4. Use the same safe cleanup path for interrupted-run recovery.

The final definition-17 run under harness 5 completed **9 passed, 0 failed**
with 4.71% paired-baseline drift, inside the five-percent acceptance limit.
Baseline A measured 234.30 FPS, cleanup baseline B measured 245.59 FPS, and
their paired mean was 239.94 FPS. Harness cleanup removed every test item
transactionally before deleting each disposable creation; the log contained
zero nonempty-container destruction warnings, zero cleanup failures, and zero
ScrapLab Lua tracebacks.

| Stage | FPS | Loss from paired baseline |
| --- | ---: | ---: |
| Local 32-chest inventory | 241.57 | -0.7% |
| Same-world Link, 64 chests | 245.94 | +2.5% |
| Same-world inventory index | 234.77 | -2.2% |
| Raw cross-world cell | 199.94 | -16.7% |
| Cross-world Link, 64 chests | 241.16 | +0.5% |
| Cross-world inventory index | 187.59 | -21.8% |
| Dense cross-world Send/Receive | 204.68 | -14.7% |

The accepted trace confirms the definition-17 target: each hot stage made only
74-76 Prospector input-route queries rather than 1,200. Complete Prospector
fixed-update work was 18-21 ms per 15-second sample, down from definition 16's
72-94 ms. Inventory indexing consumed 42-46 ms for aggregation, 6-10 ms for
7,680 live reads, and 8-11 ms for terminal selection. Dense directional work
consumed 52 ms in the manager and 48 ms in the transfer coordinator. Vacuum
fixed updates remain the largest measured ScrapLab-aware consumer at 131-167
ms per hot sample, but this is only about 0.9-1.1% of one CPU second per second
and its route-query counts remain bounded at 960 input and 360 output queries.

The remaining FPS loss begins when the benchmark loads and simulates the
remote underground cell, even when no expensive inventory catalog is active.
The engine also emits its existing `Vault.lua` and elevator script-reference
errors while those remote cells load. No ScrapLab loop, unbounded cache,
manager invariant failure, item loss, or cleanup leak appears in the accepted
trace. Further reductions in cross-world FPS cannot safely come from stale
inventory caches or weaker transaction checks; the next useful experiment
must isolate native remote-cell contents and machine simulation from the
Wireless Pipe route itself before changing production behavior again.

## Definition-16 bounded Vacuum route snapshots (implemented)

The post-definition-15 benchmark completed **9 passed, 0 failed** with only
0.34% baseline drift. Against the exact definition-14 caller-attribution run:

| Stage | Definition 14 | Definition 15 | Change |
| --- | ---: | ---: | ---: |
| Raw cross-world cell | 193.92 FPS | 206.87 FPS | +6.7% |
| Cross-world inventory index | 181.27 FPS | 200.35 FPS | +10.5% |
| Dense cross-world Send/Receive | 190.80 FPS | 209.44 FPS | +9.8% |

Vacuum fixed-update time fell from 313 to 210 ms in the raw remote stage, 331
to 202 ms during cross-world indexing, and 299 to 229 ms during directional
transfer. Packing-station spend selections fell from 3,000 to 600 per sample.
All inventory conservation, manager, transfer, cleanup, and Lua checks passed.

The trace also exposed the next dominant allocation: eight loaded Vacuums made
4,800 input-route calls per 15-second sample, one per Vacuum per fixed tick.
Each graph cache hit still returns a defensive copy of the full 128-container
route. The route membership is topology, not inventory content, so copying it
every tick is unnecessary.

Definition 16 therefore retains one input and one output shape-list snapshot per
Vacuum for no more than five ticks (0.125 seconds):

1. Input and output directions have independent snapshots so mode behavior and
   native ordering remain unchanged.
2. A Wireless Pipe Manager topology revision invalidates both snapshots on the
   next access, before their five-tick deadline.
3. Local physical changes remain bounded by the shorter five-tick consumer
   lifetime, which is stricter than the graph cache's existing 20-tick body
   validation interval.
4. The snapshot stores only shape topology. Item counts, `canSpend`,
   `canCollect`, exact-slot selection, and native container transactions remain
   live; no cached data is allowed to authorize an item move.
5. Both exact definition-14 and definition-15 Vacuum integrations migrate
   atomically while retaining the original clean-file receipt backups. Edited,
   partial, or duplicated blocks remain blocked.

Acceptance requires install/update/remove round trips, exact definition-15 and
definition-14 migration, rollback and tamper checks, script/UI regressions, a
portable rebuild, then another full `/slpipeperf auto` run. The repeat should
reduce Vacuum input calls from 4,800 toward 960 per hot stage and reduce output
calls proportionally without changing conservation or readiness results.

### Definition-16 measurements

The first run completed **9 passed, 0 failed** with no ScrapLab traceback and
proved the targeted behavior: every hot stage made 960 Vacuum input-route calls
and 360 output-route calls, exactly one fifth of definition 15's 4,800 and
1,800. In the raw cross-world stage, Vacuum fixed-update time fell from 210 to
149 ms and mean FPS rose from 206.87 to 214.61.

The run is not yet a valid final FPS comparison because its cleanup baseline
fell from 241.13 to 223.18 FPS, producing 7.73% baseline drift. The harness
marks any drift above 5% for repetition. Later inventory and directional stage
FPS therefore cannot be separated reliably from changing host load, although
their conservation, query-count, transfer, and cleanup assertions all passed.
A second run also completed **9 passed, 0 failed** and repeated the exact
960/360 input/output call totals. Its baseline improved from 215.46 to 229.76
FPS during the run, producing 6.42% drift, so it likewise cannot provide a
valid final FPS comparison. Server attribution remained usable: Vacuum work
measured 197, 165, and 143 ms in the raw, indexed, and directional cross-world
stages, while Prospector work remained 72–94 ms. That stable Prospector cost is
the evidence used for definition 17 rather than drawing a conclusion from the
unstable host FPS values.

## Definition-15 Vacuum hot-path optimization (implemented)

The caller-attribution run completed **9 passed, 0 failed** with 1.34%
baseline drift. Its paired baseline was 241.89 FPS. The raw remote-cell and
cross-world inventory stages measured 193.92 and 181.27 FPS respectively.
Vacuum Pumps dominated the loaded-machine work: 4,800 fixed updates consumed
about 299–331 ms per 15-second hot sample, compared with about 75 ms for
Prospectors and 15–20 ms for Crafters.

The trace also isolated 6,000 server-side input queries, 3,000 server-side
packing-station spend selections, and an additional 3,000 client-side spend
selections from Vacuum Pumps. Source inspection found repeated input topology
resolution inside one update, duplicate server/client packing-station
ray/sphere probes, and incoming area-trigger scans even when a pump was not
powered.

Definition 15 applies the following bounded consumer optimization:

1. Resolve Vacuum input containers at most once per server fixed update and
   reuse that exact ordered snapshot for all decisions made during the update.
2. Cache validated packing-station geometry for five ticks (0.125 seconds),
   while continuing to read the station's live filling state and requested
   UUID every tick. Failed geometry probes retry after two ticks.
3. Cache a selected spend shape for at most five ticks. Topology changes
   invalidate it immediately, and live `canSpend` validation runs before every
   reuse. An emptied source triggers a fresh ordered search in the same update.
4. Skip incoming Vacuum area-trigger enumeration while the pump has no active
   logic parent. Powered pumps retain the original harvestable, liquid,
   projectile, transaction, cooldown, and feedback behavior.
5. Preserve the definition-14 uninstall base and migrate its exact intact
   Vacuum integration through the standard atomic definition-update path.

Acceptance requires exact install/update/remove round trips, protected-snippet
tamper rejection, one-click migration from a live definition-14 receipt, Lua
hot-path regression checks, and a full post-update `/slpipeperf auto` run.

## Definition-14 measured result and caller-attribution pass

Two full definition-14/graph-v15 runs completed **9 passed, 0 failed**. The
resolved-selection cache removed all 9,600 native graph traversals from each
hot stage and produced roughly 10,800 resolved-cache hits. Input graph time
fell from about 523 ms to 99–138 ms per 15-second sample, while output graph
time fell from about 238 ms to 30–52 ms. Cross-world Link residency remained
near the paired baseline, confirming that loaded cells and idle endpoints are
not the remaining bottleneck.

The expensive cross-world inventory stage nevertheless remained about 21.7%
to 22.0% below its paired baseline. Its fixed call totals were 7,200 input
queries, 3,600 output queries, 6,000 spend selections, and 2,400 collect
selections per sample. That stable 12/6/10/4-per-tick pattern points to active
machine consumers repeatedly examining the returned live containers after the
now-cached graph lookup.

Development harness definition 4 therefore adds a measurement-only caller
attribution pass before any further production optimization:

1. Wrap the fixed updates of Crafter, Vacuum, Flat Vacuum, Garage Chest, Ore
   Crusher, Prospector, and Refinery while a benchmark sample is active.
2. Attribute every graph input, output, spend, collect, and terminal call to
   the active consumer class, while retaining the existing aggregate metrics.
3. Measure each consumer's complete fixed-update duration so time spent
   scanning returned live containers is visible even when it occurs outside
   ScrapLab's graph functions.
4. Re-run dynamic consumer discovery throughout the benchmark because Scrap
   Mechanic may load interactable class tables after the harness itself.
5. Record `consumerAttributionVersion` and the number of installed consumer
   profilers in every stage result. The harness changes no routing, storage,
   transaction, cache, or production behavior.

One full `/slpipeperf auto` run with all seven profilers active is sufficient
to select the next production optimization. Any missing profiler or an
unattributed hot call must be resolved before changing the runtime again.

## Definition-14 resolved-query optimization plan (implemented)

The clean definition-13 benchmark completed **9 passed, 0 failed** with only
0.36% baseline drift. Idle cross-world Link residency was effectively free,
but the three stages that repeatedly resolved complete container lists lost
23.5% to 27.0% of the paired baseline. Each 15-second stage made 9,600 native
pipe-graph calls and roughly 17,400 virtual or negative-cache lookups. This
isolates the next bottleneck to repeated container selection—not cell
residency, endpoint rendering, item aggregation, or transfer transactions.

Definition 14 implements the following bounded optimization:

1. **Cache the final ordered selection per caller and direction.** Input and
   output results use separate keys containing the caller's world and shape.
   The cached list preserves Scrap Mechanic's native containers first and the
   existing wireless extension order afterward. Results are copied before they
   reach callers, so consumer code cannot mutate the cache.
2. **Keep item authority live.** The cache contains Shape references only. It
   never stores slot contents, item counts, filters, free capacity, revisions,
   or transaction results. Every index and transfer still reads live
   containers, and every write still uses native container transactions.
3. **Invalidate on every relevant topology signal.** Native results mirror the
   physical component's body tracker and revalidate at most 20 ticks after a
   body changes. Wireless topology revision and remote-world readiness changes
   immediately clear virtual, terminal, and resolved selections. Missing
   shapes and invalid trackers also discard an entry before reuse.
4. **Bound long-session memory.** Native, virtual, terminal, and resolved query
   maps each cap at 2,048 entries, trim least-recently-used entries in one batch
   to 1,792, and prune entries idle for 1,200 ticks. This avoids both unbounded
   retention and a full sort on every insertion beyond the limit.
5. **Reduce lease-renewal call traffic.** A hot cached graph result requests a
   lease renewal no more than once every 20 ticks, and a terminal result no more
   than once every 30 ticks. Those intervals remain well inside the existing
   80- and 120-tick lease lifetimes.
6. **Expose proof in benchmark diagnostics.** Runtime snapshots now report
   native and resolved entry counts, resolved hits, native/resolved
   invalidations, and cache trims. A successful repeat should replace most of
   the 9,600 native calls with resolved hits while retaining all conservation,
   readiness, manager, and cleanup assertions.
7. **Migrate safely.** Wireless Vacuum Pipe definition 14 recognizes the exact
   definition-13 graph hash and exposes the normal one-click **UPDATE** action.
   The update replaces only verified owned runtime files, preserves the
   original clean uninstall backup, verifies output hashes, and rolls back to
   the complete definition-13 state on failure.

Acceptance requires the existing automatic dense benchmark, cache-bound and
invalidation regressions, definition-update migration checks, embedded UI and
JavaScript validation, the complete project regression suite, and portable
package builds. The in-game comparison remains authoritative for FPS; static
tests prove the safety boundaries but do not substitute for the benchmark.

## Definition-13 remediation plan (implemented)

This follow-up targets the remaining long-session memory retention and the
active Network Storage/Send-Receive costs that were visible after definition
11 removed permanent matched-route simulation.

1. **Bound shared inventory memory.** Use only Scrap Mechanic's global game
   tick for shared cache age, prune on a periodic heartbeat even with no open
   terminal, cap the cache at 4,096 records, and trim in batches so crossing
   the cap cannot cause a full sort on every insertion.
2. **Release closed-terminal state.** When the final viewer closes, discard the
   terminal's server descriptors, records, aggregate, snapshot, scan queues,
   route state, and client catalog/icon/widget tables. Deposit routing remains
   safe because it obtains a fresh transaction-authoritative topology.
3. **Make live catalog changes incremental.** Maintain aggregate totals by
   subtracting the previous changed-container record and adding its replacement.
   On the client, reuse cached item metadata and existing widgets when UUID
   order is unchanged. Rebuild the grid only when filtering/sorting/membership
   actually changes, preserving its scroll position.
4. **Reduce directional-transfer amplification.** Move up to one bounded stack
   (maximum 100 items) per atomic transaction, use destination capacity as the
   final batch size, keep fairness cursors in runtime memory, checkpoint them
   every 200 ticks, and remove retry/cursor state for inactive channels.
5. **Make manager maintenance event/deadline driven.** Recompute cell-handle
   ownership only after endpoint/lease/readiness changes or the next actual
   lease, retry, release, or reconciliation deadline. Keep a 400-tick safety
   maintenance ceiling and expose run counters to the benchmark.
6. **Stop copying stable topology.** Extend physical validation to 20 ticks and
   return borrowed read-only terminal descriptor/state views from the graph
   cache. Network Storage must never normalize or mutate those shared entries.
7. **Reduce idle endpoint client work.** Poll configuration twice per second,
   consume visual activity every two ticks, create `PipeEffectPlayer` only for
   a real transfer, and send glow changes only when their visible value moves.
8. **Ship as safe definition updates.** Wireless Vacuum Pipe definition 13 and
   Network Storage Chest definition 10 recognize every verified currently
   released owned-file hash. Updates preserve the original clean uninstall
   bases, remain atomic per mod, and expose the normal compact **UPDATE** action.

Safety invariants remain unchanged: native container transactions are the only
item authority; source and destination are freshly re-resolved before commit;
cross-world cell limits remain enforced; cache entries never make a write
decision; and save-sensitive UUID removal behavior is untouched.

Qualification includes static hot-path regressions, owned-file legacy-hash
migration tests, fake-game atomic install/update/removal tests, embedded UI and
JavaScript checks, the existing 32-chest/1,152-stack benchmark contract, a full
portable build, and a final in-game `/slpipeperf auto` comparison after the
definition update is installed.

**Status:** The definition-11 after-change qualification pass finished on
2026-08-15. All nine benchmark stages and every conservation, manager, and
cleanup invariant passed. The worst workload loss fell from 14.30% to 2.07%
versus the paired baseline, with only 0.17% baseline drift.

Definition 11 replaces permanent matched-route cell ownership with
demand-renewed leases, retains physical graph components until their bodies or
topology actually change, caches negative virtual lookups and terminal
descriptors, progressively backs idle directional routes down to a ten-second
retry, and sends activity visuals only to nearby clients that have the shape
loaded. The optimized runtime, atomic definition-10 migration, uninstall
receipt promotion, portable build, and static/integration regressions are all
verified. Run `/slpipeperf auto` once in-game to collect the measured
before/after result. That result is recorded below.

The first pass automatically creates two 32-chest inventory systems. Every
chest receives 18 deterministic mixed stacks selected from 24 item types, so
same-world and cross-world Link stages exercise 64 chests and 1,152 occupied
stacks without asking the tester to build anything. It measures baseline,
local inventory, same-world Link/indexing, raw cross-world residency,
cross-world Link/indexing, dense Send/Receive, and post-cleanup recovery. It
also verifies item quantity/type conservation and manager invariants.

The wider consumer, endpoint-scaling, render, and long-soak matrices below
remain the second qualification pass after this first run identifies which
subsystem deserves the next fixture expansion.

### Definition-11 after-change result (2026-08-15)

The repeat run used the same two 32-chest fixtures, 1,152 occupied stacks,
3,450 items, stage timings, and conservation checks as the definition-10
qualification run. It completed **9 passed, 0 failed**, kept manager
invariants valid, preserved every item UUID and quantity, and removed every
fixture. The directional stage safely consolidated the same inventory from
1,152 to 1,121 stacks without changing its 3,450-item total.

| Stage | Mean FPS | 1% low FPS | Loss vs paired baseline | Definition-10 loss |
| --- | ---: | ---: | ---: | ---: |
| Baseline A | 271.18 | 95.84 | — | — |
| Local 32-chest inventory | 273.07 | 96.79 | effectively zero | effectively zero |
| Same-world Link, 64 chests | 272.30 | 99.96 | effectively zero | 4.73% |
| Same-world inventory index | 265.97 | 89.74 | 2.00% | 7.49% |
| Raw cross-world cell residency | 267.12 | 94.06 | 1.58% | 9.14% |
| Cross-world Link | 271.27 | 94.87 | 0.05% | 8.83% |
| Cross-world inventory index | 265.80 | 91.40 | 2.07% | 12.55% |
| Cross-world Send/Receive | 266.24 | 89.91 | 1.90% | 14.30% |
| Cleanup baseline B | 271.63 | 93.35 | — | — |

The paired baseline was 271.41 FPS. The largest remaining measured loss was
the active cross-world inventory-index workload at 2.07%; the dense
Send/Receive workload was close behind at 1.90%. Idle cross-world Link fell
from an 8.83% loss to 0.05%, confirming that demand-renewed cell ownership
removed nearly all of the permanent matched-route cost.

The sampled stages performed no physical graph rebuilds. Inventory-index
stages served 60 terminal-cache hits, while the directional stage served 300
component-cache hits and spent 37 ms in its fixed-update path over the
15-second sample. No Wireless Vacuum Pipe traceback, benchmark failure, or
remote `cl_n_directionalActivity` error appeared. Three game errors emitted
during an underground-world transition came from vanilla elevator/Vault
callbacks (`sv_e_updateFloorDisplay`, `sv_e_setMarkerLocked`, and
`Vault.sv_count`), not a ScrapLab pipe script.

This pass validates the definition-11 production optimization. Future work
can concentrate on scaling across many simultaneous active inventory indexes
and directional channels rather than revisiting the eliminated idle-route
overhead.

### Complete qualification result (2026-08-15)

The full run created two 32-chest systems containing 1,152 occupied stacks and
3,450 items, exercised same-world and cross-world Link routes, indexed the
joined inventories, ran a dense cross-world Send/Receive workload, and removed
every fixture. Item quantities and the per-UUID signature were identical
before and after every non-transfer stage. The directional stage merged stacks
from 1,152 to 1,121 while preserving all 3,450 items and the full UUID
signature. Cleanup returned the save to zero test containers, stacks, and
items. Baseline drift was 3.53%, below the five-percent repeat threshold.

| Stage | Mean FPS | 1% low FPS | Loss vs paired baseline |
| --- | ---: | ---: | ---: |
| Baseline A | 217.01 | 108.74 | — |
| Local 32-chest inventory | 214.73 | 102.30 | effectively zero |
| Same-world Link, 64 chests | 203.15 | 80.11 | 4.73% |
| Same-world inventory index | 197.26 | 76.25 | 7.49% |
| Raw cross-world cell residency | 193.74 | 94.01 | 9.14% |
| Cross-world Link | 194.40 | 79.07 | 8.83% |
| Cross-world inventory index | 186.47 | 68.61 | 12.55% |
| Cross-world Send/Receive | 182.74 | 69.12 | 14.30% |
| Cleanup baseline B | 209.47 | 108.59 | — |

The paired baseline was 213.24 FPS with a 108.66 FPS one-percent low. Raw
remote-cell residency alone accounts for the largest single average-frame-rate
loss. Adding the virtual graph to that cell changes average FPS little but
reduces the one-percent low from 94.01 to 79.07, identifying graph discovery as
a substantial stutter source.

The graph received about 1,174 virtual queries per second in the Link stages
and rebuilt physical components 24–32 times per second. Same-world indexing
added 60 terminal topology discoveries totaling 132 ms; cross-world indexing
added 60 totaling 127 ms. Inventory reads and aggregation were comparatively
cheap, so the Network Storage Chest should cache topology descriptors while
continuing to read live item revisions.

Dense directional transfer completed 75 of 75 selected transactions with no
rejection, loss, or duplication. Its inclusive fixed-update work totaled
139 ms over 15 seconds, much smaller than the retained remote-cell and graph
costs. It is a later optimization target, not the first one.

The log also exposed two server networking errors outside the harness failure
counter: remote endpoint shape scripts called `sendToClients` for
`cl_n_directionalActivity` even though the host client had no valid client-side
instance for those remote-world shapes. Item routing still completed safely,
but remote visual pulses must not be broadcast from unloaded client shapes.
This is both a correctness cleanup and avoidable cross-world network work.

**Ranked production work:**

1. Replace permanent matched-route cell ownership with short, demand-renewed
   remote-cell leases and a safe loading/retry state.
2. Replace the global 10-tick graph reset with persistent, revision-validated
   component and negative-origin caches.
3. Cache Network Storage terminal topology by graph/manager revision while
   keeping inventory contents live.
4. Suppress or locally broker directional activity effects for remote shapes
   that do not exist on a client.
5. Optimize the active directional scheduler only after the first four items.

### First-pass observation (2026-08-15)

The initial five-stage run completed with zero harness failures, no ScrapLab
traceback, intact item totals, valid manager invariants, and complete fixture
cleanup. It measured 185.8 FPS at baseline A, 185.0 FPS with one local
32-chest/576-stack system, 176.7 FPS with a same-world Link pair, and 176.4 FPS
while indexing that pair. Inventory indexing therefore added little average
cost beyond the active Link graph itself. The Link stage increased wrapped
input-container time from 455 ms to 703 ms per 15-second sample and performed
360 physical scans/5,100 node visits. Manager and directional-scheduler time
remained negligible in this same-world case.

The Link pair reduced one-percent-low FPS from 100.4 to 75.8, while the index
stage reached 69.8. Baseline B improved to 196.1 FPS, producing 5.4% baseline
drift, so these values are evidence of the graph hot path but not a final
qualification result. Harness definition 2 fixes the half-filled second rig,
requires a remote world, and reports baseline drift before the next run.

**Correctness prerequisite:** Definition 10 adds the previously missing
SEND-side producer destination path. A machine producing into a SEND network
can now select eligible storage behind matching RECEIVE endpoints, with local
native storage first and the receiver's Direct Container Only setting intact.
The performance harness must include this water-pump/output path so future
optimizations cannot regress it.

**Scope:** The initial pass measured and isolated the runtime cost without
changing gameplay behavior. The resulting definition-11 implementation keeps
routing, transfer semantics, and save data unchanged while replacing only the
runtime ownership, caching, retry, and visual-notification strategy.

## 1. Objective

The benchmark must explain why one cross-world route can reduce the host's
frame rate by about 10 FPS and predict how the system behaves with many
endpoints, containers, machines, channels, and worlds.

It must distinguish four different costs:

1. Scrap Mechanic's cost for keeping another world cell loaded and simulated.
2. ScrapLab manager, endpoint, and directional-scheduler work.
3. Virtual pipe-graph discovery and container-selection work.
4. Client rendering, effect, and synchronization work.

A correctness pass is not a performance pass. Existing phase tests prove that
items move safely and that routes survive world transitions; they do not
measure frame time, Lua CPU time, scaling, or idle overhead.

## 2. Current code-level findings

### 2.1 Global route activation makes unrelated machines enter the wrapper path

`WirelessPipeManager.sv_rebuildGroups` exposes a global `link`, `directional`,
`input`, and `output` capability. As soon as one compatible endpoint group
exists anywhere in the save, `ScrapLabPipeGraph.extendNativeShapeList` no
longer takes its exact-native fast path for the matching direction.

That means a single Link pair can make every loaded patched Craftbot, Vacuum,
Flat Vacuum, Garage Chest, Prospector, Refinery, Ore Crusher, and compatible
resource helper ask the virtual graph whether its own physical pipe component
contains an endpoint. Machines that are nowhere near a Wireless Vacuum Pipe
still pay for this negative discovery.

This matches the earlier observation that the slowdown is strongest around
dense groups of parts with pipe connection points.

### 2.2 Every matched group continuously holds its endpoint cells

`WirelessPipeManager.sv_buildDesiredCells` treats every matched enabled
endpoint as active. `sv_updateHandleOwnership` then retains one
`loadCellWithHandle` handle per unique endpoint cell. Cross-world routes also
call `sm.world.loadWorld` when necessary.

The handles are not demand-based. They remain held while the group is paired,
even when no item is moving, no machine needs a remote container, and no pipe
panel is open. A remote handle causes the cell's bodies, machines,
interactables, physics, and server scripts to stay available for simulation.

The 64-cell cap limits the worst memory/simulation explosion, but it does not
make one or several retained cells cheap.

### 2.3 Physical topology is rebuilt on a timer

`ScrapLabPipeGraph` discards all physical, direct-container, native-query, and
virtual-query entries every 10 fixed ticks (0.25 seconds). The cache does share
a scan between consumers during that short epoch, which was a substantial
improvement over per-call traversal, but it still forces every active physical
component to be rediscovered four times per second.

Each rebuild can:

- walk every piped shape in the component;
- call `getPipedNeighbours`;
- test UUIDs and containers;
- collect and validate bodies;
- sort shapes, containers, and endpoint records;
- allocate fresh tables and result arrays.

Large networks and many separate negative components therefore scale with
both network size and the number of loaded machine components.

### 2.4 Cache hits still perform work

Within a cache epoch, a hit still validates bodies with `hasChanged`, checks
shape existence, copies lists, and deduplicates output shapes. A cached result
is much cheaper than a traversal, but it is not a constant shared immutable
result.

### 2.5 Vanilla consumers query at very different rates

The most important hot paths are not equally expensive:

- An active/eligible Craftbot checks output containers every fixed tick and
  checks inputs while a recipe is looping.
- Vacuum scripts can perform several input and destination queries in one
  fixed update, depending on their mode and nearby harvestables/liquids.
- Prospectors poll inputs periodically and rebuild automated tasks when their
  water source changes.
- A Network Storage Chest is mostly dormant while closed, but an open terminal
  refreshes topology and polls inventory revisions.
- SEND/RECEIVE scheduling backs an empty or blocked channel down to one attempt
  per second, but each attempt can scan sender slots and candidate receivers.

The benchmark must report cost by consumer instead of treating every endpoint
pair as equivalent.

### 2.6 Manager and endpoint housekeeping will matter at scale

Every 10 ticks, the manager loops over endpoint records, reconstructs desired
cell ownership, resets endpoint handle state, sorts records, and constructs a
full handle signature. Each endpoint also polls paint, logic, world, cell, and
status every 10 ticks. This is unlikely to explain a 10-FPS loss with only two
endpoints, but it is an O(endpoint count) or O(endpoint count log endpoint
count) scaling risk.

### 2.7 Client animation is a lower-priority suspect

Every locally loaded Wireless Vacuum Pipe updates UV/glow state at 40 Hz and
updates a `PipeEffectPlayer` every rendered frame. This can matter with many
visible endpoints, but a server-only remote cell is not rendered by the local
client. It is not the leading explanation for a one-pair cross-world loss.

### 2.8 Directional activity can target an absent client script

The complete benchmark contains no ScrapLab Lua traceback or repeating error
loop, but it did capture two engine networking errors while the cross-world
directional stage was active. `WirelessVacuumPipe.server_onFixedUpdate`
consumes transfer activity and `sv_onDirectionalActivity` calls
`self.network:sendToClients("cl_n_directionalActivity", ...)`. A remote cell
may be loaded only on the server; in that case the host client has no valid
client-side instance for the remote endpoint shape, and the engine reports an
invalid script instance.

The transfer itself is already complete before this optional visual message.
Production code should therefore never publish the pulse from a server-only
remote endpoint. Local endpoint animation may remain, but remote activity must
be suppressed or relayed only to clients that own a live instance.

## 3. Benchmark design

### 3.1 One-command in-game runner

Add a temporary, non-release performance harness with these commands:

```text
/slpipeperf auto
/slpipeperf quick
/slpipeperf status
/slpipeperf results
/slpipeperf cancel
```

`/slpipeperf auto` is the qualification command. It creates every disposable
fixture, runs the complete matrix, restores the starting state, removes every
created shape, releases every test handle, and prints one final summary. The
player must not need to place a pipe, chest, machine, logic gate, or item.

`quick` runs only the baseline, raw-handle, idle Link, idle Craftbot, and
cleanup-recovery stages. It is useful while iterating on instrumentation, but
it does not qualify an optimization.

### 3.2 Instrumentation

Use two coordinated samplers.

#### Client frame sampler

Sample `deltaTime` from `SurvivalGame.client_onUpdate` for each measurement
window. Record:

- frame count and elapsed wall time;
- mean, median, p95, and p99 frame time;
- average FPS and one-percent-low FPS;
- frames above 25 ms, 33.3 ms, 50 ms, and 100 ms;
- maximum frame time;
- whether the game window lost focus or the client paused.

Raw per-frame samples stay in memory only for the active stage. Emit aggregate
results to the log to avoid creating a new logging bottleneck.

#### Server Lua sampler

Use `os.clock()` in explicitly enabled profiling windows. Record inclusive and
exclusive time, calls, maximum call time, and sampled percentiles for:

- manager fixed update;
- desired-cell and handle ownership updates;
- endpoint refresh/status publishing;
- native pipe queries;
- physical component builds;
- component validity checks;
- virtual Link discovery;
- terminal descriptor discovery;
- container selection;
- directional schedule and commit work.

Continue recording the existing graph counters:

- native calls and cache hits;
- fast-path returns;
- physical scans and physical nodes;
- component/direct/virtual cache hits;
- directional attempts, skips, selections, commits, and rejects.

Add temporary counters for negative origin queries, result-array copies,
body-validity checks, endpoint matches visited, cells loaded/released, and
live server scripts in each fixture where they can be counted reliably.

Timing must be opt-in because `os.clock()` around every production call would
itself distort normal gameplay. The harness enables it only during a bounded
window and measures profiler overhead with an empty control loop.

### 3.3 Measurement window

Each scenario uses an A/B/A sequence:

1. 10-second warm-up.
2. 20-second baseline A.
3. Apply the scenario and wait for cell/topology stabilization.
4. 30-second treatment measurement.
5. Remove or disable the treatment and wait for handle grace/cleanup.
6. 20-second baseline B.

Use the average of baseline A and B for the paired comparison. Repeat any stage
whose two baselines differ by more than five percent. This prevents a loading
spike, background task, or thermal drift from being mistaken for wireless
cost.

Run the core stages three times in interleaved order. Large scaling stages may
run once initially and repeat only near a threshold or regression.

## 4. Isolation matrix

### Group A — Raw engine world/cell cost

These stages do not call the wireless graph.

| Stage | Treatment | What it isolates |
| --- | --- | --- |
| A0 | No extra handle | Stable reference baseline |
| A1 | Handle for an empty cell in the current world | Same-world cell simulation cost |
| A2 | Load the remote world object without a cell handle | `sm.world.loadWorld` cost |
| A3 | Handle for an empty cell in another world | Pure cross-world cell cost |
| A4 | Remote cell containing one inert chest | Saved-body/interactable cost |
| A5 | Release every test handle and wait through grace | Unload/recovery and leak check |

If A3 alone reproduces most of the FPS loss, no amount of Lua graph caching can
fully solve the issue. The route residency policy must change.

### Group B — Idle endpoint cost

| Stage | Treatment |
| --- | --- |
| B0 | Two loaded endpoints on different colors (unpaired) |
| B1 | Two matched Link endpoints in the same cell |
| B2 | Two matched Link endpoints in separate cells in one world |
| B3 | One matched Link endpoint in each of two worlds |
| B4 | B3 disabled by logic without deleting the parts |
| B5 | B3 unpaired by paint color |

Comparing B3 with A3 separates manager/endpoint overhead from the engine's raw
remote-cell cost. B4/B5 must return close to baseline after handle grace.

### Group C — Global negative-query cost

Keep one valid Link pair active, but place tested machines on physical networks
that contain no Wireless Vacuum Pipe.

| Stage | Loaded unrelated components |
| --- | ---: |
| C0 | 0 |
| C1 | 1 idle Craftbot |
| C2 | 16 isolated idle Craftbots |
| C3 | 64 isolated idle Craftbots |
| C4 | Mixed Craftbots, Vacuums, Prospectors, Garage Chests, and containers |

Repeat C1-C4 with the Link pair unpaired. The difference directly measures the
global `Sv_HasVirtualRoute` negative-discovery tax. This is a priority test
because the present architecture activates the virtual path globally.

### Group D — Connected graph size

Measure a machine that is genuinely attached to the wireless network.

Vary one dimension at a time:

- physical shapes per component: 8, 64, 256, 1,024;
- containers per component: 1, 8, 32, 128;
- Link endpoints in one conjoined bus: 2, 4, 8, 16, 32;
- unique endpoint cells: 1, 2, 4, 8, 16;
- worlds: one world, overworld plus one underground world, and multiple
  already-created underground worlds when available.

Record cold traversal, warm-cache query, periodic epoch rebuild, and steady
state separately. Stop expansion automatically if a stage falls below 20 FPS,
exceeds 50 ms p95 frame time, or leaves more handles than its fixture owns.

### Group E — Consumer matrix

Test each consumer with local-only, same-world wireless, and cross-world
wireless storage:

- chests and pipes only;
- idle Craftbot;
- looping Craftbot with missing ingredients;
- looping Craftbot actively crafting;
- Vacuum disconnected, connected but inactive, and actively collecting;
- Prospector idle and active;
- Network Storage Chest closed, open and idle, searching, depositing, and
  withdrawing.

This identifies which vanilla call sites need consumer-specific throttling or
memoization.

### Group F — SEND/RECEIVE scheduler

Run 1, 4, 16, and 32 independent color channels for:

- empty senders;
- non-empty senders with full receivers;
- active same-world transfer;
- active cross-world transfer;
- Direct Container Only;
- Entire Pipe Network with 1, 16, and 64 candidate containers.

Measure both the four-tick active cadence and the 40-tick idle backoff. Confirm
that throughput, fairness, and item conservation remain correct while timing
the selection scans.

Also exercise the definition-10 producer route separately from the background
scheduler:

- a water pump in water attached to SEND and a directly attached Water
  Container behind RECEIVE;
- the same arrangement across the overworld/underground boundary;
- a RECEIVE container one or more ordinary pipes away with Direct Container
  Only enabled, then Entire Pipe Network enabled;
- a local SEND-side destination present, full, and removed, proving native
  local priority and remote fallback;
- an empty/full/filtered RECEIVE network and multiple matching receivers.

Record generated and collected water before and after every stage. A passing
performance result is invalid if one unit of liquid is lost, duplicated, or
sent outside the selected receiver scope.

### Group G — Client/render cost

Measure 1, 16, 64, and 128 visible local endpoint parts in these states:

- unpaired and dark;
- linked glow;
- continuous directional activity pulses;
- configuration panel open and closed.

Repeat with the same endpoints held server-side in a remote world but not
visible. A difference only in the visible set points to client animation or
effects; a difference in both points to server simulation.

### Group H — Soak and cleanup

Run a 20-minute mixed workload that repeatedly:

- opens and closes routes;
- moves items;
- changes channel/mode/logic;
- opens and closes a Network Storage Chest;
- loads and unloads the remote world;
- destroys and recreates fixture endpoints.

At the end, require:

- zero test shapes and containers;
- zero test endpoint records;
- zero test cell handles;
- zero pending transfers or locks;
- cache sizes back within their starting bounds;
- no item loss or duplication;
- post-cleanup FPS within five percent of the paired baseline;
- no ScrapLab traceback in the log.

## 5. Structured results

Every stage writes one compact line with a unique prefix so a desktop parser
can extract it without treating unrelated game warnings as test failures:

```text
[ScrapLab Pipe Perf] RESULT { stage=..., run=..., frames=..., fpsMean=...,
frameP95Ms=..., onePercentLow=..., luaMs=..., handles=..., scans=...,
nodes=..., negativeQueries=..., status=PASS|WARN|FAIL }
```

The final report contains:

- build/game version and wireless definition version;
- graphics resolution, VSync state when detectable, and host/client role;
- starting endpoint/handle/world counts;
- paired FPS and frame-time deltas;
- Lua time by subsystem;
- scan/query/cache rates per second;
- scaling curves per endpoint, cell, component, and container;
- cleanup verification;
- a ranked bottleneck conclusion.

Do not fail the test because the general game log contains unrelated base-game
warnings. Fail only on harness invariants, ScrapLab-owned tracebacks, unsafe
cleanup, or explicit performance gates.

## 6. Initial performance gates

These are provisional targets for the first measurement pass and may be
tightened after a clean baseline corpus exists.

- One idle cross-world pair in empty fixture cells: no more than five-percent
  median FPS loss and no more than ten-percent one-percent-low loss.
- One idle Link pair must not make 16 unrelated isolated machines consume more
  than 0.5 ms of ScrapLab Lua time per fixed tick at p95.
- Sixteen endpoints sharing cells: manager/endpoint housekeeping below 1 ms per
  fixed tick at p95.
- Sixty-four endpoints: manager/endpoint housekeeping below 2.5 ms per fixed
  tick at p95, with the existing handle cap enforced.
- Warm-cache graph queries must be at least ten times cheaper than their cold
  component rebuild for networks of 256 or more shapes.
- Empty directional groups at maximum backoff must use less than one percent of
  the fixed-tick budget in aggregate.
- Cleanup must release every owned handle and restore frame performance within
  five percent of baseline.

The engine-only A3 result is reported separately. If it fails the end-to-end
FPS target while ScrapLab Lua time is low, that is a residency-design failure,
not a graph-optimization failure.

## 7. Decision rules after the first run

### If raw remote-cell loading is most of the loss

Prototype demand-based route leases instead of permanently retaining every
paired cell. Link queries, active transfers, and open storage terminals would
renew short leases; dormant links would not simulate an unused remote cell.
Correctness tests must prove that the first query safely reports loading and
retries without losing an item.

### If unrelated negative queries are most of the loss

Replace the 10-tick global cache reset with persistent, topology-validated
component records and a negative-origin cache. Pre-index components containing
loaded wireless endpoints, retain negative results until a participating body
changes, and prune records by age rather than discarding every component four
times per second.

### If connected graph copying/validation is most of the loss

Return immutable cached descriptor IDs internally, avoid rebuilding/deduping
shape arrays on every hit, validate each body once per fixed tick, and split
topology revision by affected component/channel instead of invalidating every
route globally.

### If one vanilla consumer dominates

Throttle or memoize only that consumer's safe topology query. Inventory
capacity and item counts remain live; only topology and existence checks may be
cached. Do not change vanilla transfer transactions or item ordering to gain
speed.

### If client animation dominates

Make idle UV/glow state event-driven and run pulse interpolation only while an
endpoint is visible and changing. Do not remove state feedback before the
server-side tests prove it is actually relevant.

## 8. Implementation order

1. Add profiling counters and the one-command harness without changing routing.
2. Run the quick A/B suite to validate that instrumentation overhead is small.
3. Run the complete matrix on the dedicated mod-testing save.
4. Inspect the generated log report and classify the dominant cost.
5. Implement one targeted optimization behind the same benchmark.
6. Repeat the complete matrix and all existing correctness regressions.
7. Keep the optimization only if it improves the measured bottleneck without
   changing item conservation, route order, world behavior, or cleanup safety.

No production definition bump, app release, or gameplay patch change should be
made until steps 1-4 identify the actual dominant cost.
