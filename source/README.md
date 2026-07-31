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
| `Patching/` | Game patch services, adaptive compatibility, backup safety, and patch-owned Lua assets. |
| `Companions/PatchHelper/` | Restricted elevated game-patch executable. |
| `Companions/Updater/` | Fixed self-update executable. |
| `Assets/` | Windows manifest and application icon. |

## Adding files

1. Put the file in the folder matching its responsibility.
2. Add its relative path to the correct explicit source list in `build.ps1`.
3. Keep patch-owned Lua files under `Patching/Scripts/` and embed them with a
   stable manifest-resource name.
4. Run `build.ps1` and the relevant scripts under `tests/`.

Do not place generated binaries here. Build outputs belong in `dist/`, and
packaged portable releases belong in `release/`.
