# ScrapLab source layout

The project intentionally uses explicit compiler input lists in `build.ps1`.
Files are grouped by responsibility so adding a feature does not turn the
source root back into a flat collection.

| Folder | Responsibility |
| --- | --- |
| `App/` | Main desktop executable, WebView UI, update client, and patch-helper client. |
| `Shared/` | Models, paths, install discovery, protocol types, and companion security shared across executables. |
| `World/` | Save database access, Lua decoding, item metadata, and world maintenance operations. |
| `Performance/` | Read-only performance scanning, ranking, operation management, and report export. |
| `Patching/` | Game patch services, adaptive compatibility, backup safety, and patch-owned assets. |
| `Patching/Parts/<PartName>/` | Canonical source location for each ScrapLab custom part's owned scripts, shape sets, icons, and future model or texture assets. |
| `Companions/PatchHelper/` | Restricted elevated game-patch executable. |
| `Companions/Updater/` | Fixed self-update executable. |
| `Assets/` | Windows manifest and application icon. |
| `ToolForge/` | Separate generate-only character-tool editor, preview, validators, and package generator. |

## Adding files

1. Put the file in the folder matching its responsibility.
2. Add its relative path to the correct explicit source list in `build.ps1`.
3. Keep shared patch-owned Lua files under `Patching/Scripts/`. Put every
   custom part under `Patching/Parts/<PartName>/` and embed its owned files
   with stable manifest-resource names. Never copy vanilla model or texture
   assets when a part can reference them in place.
4. Run `build.ps1` and the relevant scripts under `tests/`.

Custom-part icons must be added to the catalog in
`Patching/ScrapLabIconAtlasCoordinator.cs`. The hidden shared icon pack installs
all catalog icons in one pass, allocates verified transparent cells from the
atlas bottom-right upward, and leaves per-mod enablement to XML registrations.
It also recognizes the verified legacy opaque Raid Detector tile and can
migrate only that 96x96 cell to the current transparent icon without replacing
the mod's original clean uninstall receipt.
Raid Detector definition migrations likewise keep the exact prior owned Lua as
an embedded compatibility asset, so only a byte-identical known legacy script
can receive the corrected interactable-body world lookup.
Do not add another `ItemIcons` group: Scrap Mechanic silently resolves only the
first group with that name.

Do not place generated binaries here. Build outputs belong in `dist/`, and
packaged portable releases belong in `release/`.

Tool Forge has its own `build-tool-forge.ps1` so its WebView2 and Three.js
preview files never enter the normal ScrapLab release bundle.
