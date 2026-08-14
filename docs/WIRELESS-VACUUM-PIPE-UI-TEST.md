# Wireless Vacuum Pipe UI Redesign Test Plan

This test plan qualifies definition 8 of the Wireless Vacuum Pipe. The change
is presentation-only: endpoint storage, routing, pipe traversal, and transfer
rules must remain unchanged.

## Automated qualification

Run from the publication-kit root with Scrap Mechanic closed:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\WirelessVacuumPipeUiRegression.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\WirelessVacuumPipePhase2Regression.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\WirelessVacuumPipePatchServiceRegression.ps1
```

Then build the portable application and run the normal Wireless Vacuum Pipe
release regressions. The UI regression checks XML validity, required widgets,
native skins, centered dimensions, direct button captions, status-color
coverage, and the definition-7-to-8 owned UI-asset upgrade path.

## In-game visual and interaction test

Test at 1280x720, 1920x1080, and the monitor's native high resolution.

1. Place a Wireless Vacuum Pipe and open it. Confirm the panel is centered,
   fully on screen, and leaves the hotbar visible.
2. Switch through **Link**, **Send**, and **Receive**. Exactly one mode button
   must be selected and the gold active-mode value must match it.
3. In Link mode, confirm the transfer-scope control is replaced by the Link
   network message. In Send and Receive, toggle between **Direct Container
   Only** and **Entire Pipe Network**, close the panel, reopen it, and confirm
   the selection persists.
4. Paint two endpoints the same color. Confirm the swatch and hex channel
   update, the match count becomes one, and linked world names are readable.
5. Exercise these states and verify the lamp color and text agree:
   **Unpaired/Channel Empty** amber, **Linked/Sending/Ready to Receive** green,
   **Cross-World Linked** cyan, **Disabled by Logic** grey, manager unavailable
   red, and remote-cell limit orange.
6. Link matching endpoints in two or more worlds. Confirm each panel shows the
   correct current world and up to two matching world names; additional worlds
   must collapse into a compact `+N MORE` count without clipped text.
7. Hold a logic input off and on while the panel is open. Status must refresh
   without flicker, button-state loss, or the panel reopening.
8. Complete one same-world and one cross-world item transfer after using every
   control. Item counts and transfer direction must match the pre-redesign
   behavior.

## Existing-install update test

1. Start with an intact v2.6.0 Wireless Vacuum Pipe installation.
2. Open ScrapLab and confirm the mod remains installed but offers its normal
   compact update action.
3. Apply the update with the game closed. Confirm the part remains registered
   in the save and the redesigned panel opens.
4. Interrupt or simulate failure during a disposable test installation and
   verify the complete prior definition is restored.
5. Confirm disabling still uses the existing save-sensitive warning; the UI
   update itself must never show that removal warning.
