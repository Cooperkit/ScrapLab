# Network Storage Chest — Phase 4 Automatic Test

Phase 4 integrates the terminal with Wireless Vacuum Pipe without making that mod a hard dependency.

## Run the test

1. Close Scrap Mechanic before ScrapLab installs this development build.
2. Start the game normally and enter the save used for the pipe tests.
3. Stand in an open area and run:

   ```text
   /slstorage4 auto
   ```

The command builds disposable same-world and cross-world networks, runs the complete test, clears their inventories, and removes every temporary part. Do not build or place test items.

The test covers Link read/write union, Receive-from-Send, Send-to-Receive, Direct Container Only, Entire Pipe Network, same-world and cross-world discovery, ready-state reporting, real atomic withdrawal and deposit, conservation, and manager invariants.

The expected final message is `PHASE 4 AUTOMATIC TEST COMPLETE` with zero failed checks. A cross-world check is skipped only when the save has no previously discovered second world.
