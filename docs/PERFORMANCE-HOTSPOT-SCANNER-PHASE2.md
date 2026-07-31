# Performance Hotspot Scanner: Phase 2 App Integration

## Outcome

Phase 2 is complete. ScrapLab now runs the Phase 1 read-only scanner on a
dedicated background thread and exposes a responsive in-app world-density
report with real progress and cancellation.

Phase 3 has not started. The report does not rank cells, assign severity,
construct 3-by-3 neighborhoods, or claim that any record causes FPS loss.

## Operation lifecycle

`source/Performance/PerformanceScanOperationManager.cs` owns one scan operation at a time:

- `Begin` returns immediately with an opaque operation ID.
- A second scan is rejected while one is active.
- `GetStatus` returns an immutable progress snapshot and a terminal result.
- `Cancel` signals the scanner's existing cancellation token.
- `Dispose` requests cancellation without joining the worker thread, so closing
  ScrapLab never waits for a large database scan.
- The worker is a named background thread and never calls the browser.

Terminal states are `completed`, `cancelled`, and `failed`. Unknown operation
IDs fail closed as `not_found`.

The browser result deliberately omits the complete per-cell summary collection.
The host retains bounded aggregation internally; Phase 3 will expose only a
bounded ranked hotspot list.

## Real progress

`PerformanceScanner` reports completed work from six real stages:

1. Checking database layout
2. Counting stored records
3. Grouping records by world and cell
4. Decoding supported record types
5. Preparing the bounded summaries
6. Building and fingerprinting the report

The Harvestable grouping stage reports row-based completion while records are
streamed. Observer errors cannot fail the read-only scan. The UI polls status
every 200 milliseconds and never uses ScrapLab's decorative loading
estimate for this operation.

Phase 2 uses “preparing” rather than claiming to rank hotspots. Actual ranking
belongs to Phase 3.

## Browser bridge and UI

The WebBrowser bridge now provides:

- `BeginPerformanceScan(path)`
- `GetPerformanceScanStatus(operationId)`
- `CancelPerformanceScan(operationId)`

After ordinary world analysis succeeds, the diagnostic view contains a
separate **Scan Performance** action. The section displays:

- a real percentage, active stage, six-stage rail, and **Cancel Scan**;
- worlds, supported records, populated cells, and scan duration;
- supported category totals with byte and record bars;
- decoded per-world totals;
- a clear `NOT RANKED` hotspot value;
- a limitation explaining that save density is not an FPS benchmark.

The UI HTML-escapes result text. Repairs, dropped-item deletion, save selection,
game-mod changes, and ordinary analysis remain locked while the scan is
active. A successful save mutation or selecting a different save invalidates
the old performance result.

## Regression coverage

`tests/PerformanceScanOperationRegression.ps1` generates a 200,576-row dense
fixture and proves:

- `Begin` returns in under 500 milliseconds;
- a second simultaneous scan is rejected;
- visible progress never moves backward and reaches 100 percent;
- the browser result omits unbounded cell summaries;
- Phase 2 still returns no ranked hotspots;
- unknown operation IDs fail closed;
- cancellation reaches a terminal cancelled result;
- a fresh scan works after cancellation;
- disposal requests cancellation and returns without waiting;
- fixture hashes remain unchanged;
- the Phase 2 action, bridge calls, cancellation control, and unranked label
  are present in the embedded UI.

The embedded JavaScript also passes a syntax check after compilation. Phase 0
research, Phase 1 scanner, updater, companion-boundary, adaptive-patch, and
crop-release regressions remain green. The dropped-item regression still
requires its existing external `SourceSave` fixture and was not changed by
Phase 2.

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File tests\PerformanceScanOperationRegression.ps1
```

## Phase 2 exit decision

The Phase 2 exit condition is satisfied:

- large-save scanning runs outside the UI thread;
- cancellation is wired end to end;
- shutdown never joins the scanner thread;
- the UI presents actual scanner progress;
- normal mutation controls are locked during the operation and restored at a
  terminal state;
- completed results are bounded before crossing the browser bridge;
- no Phase 3 ranking policy was introduced early.

Before Phase 3 starts, reread the complete implementation plan from beginning
to end.
