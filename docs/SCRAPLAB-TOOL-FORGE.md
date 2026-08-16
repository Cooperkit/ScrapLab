# ScrapLab Tool Forge architecture

Tool Forge is intentionally separate from the shipping ScrapLab save and patch
application. Its first template turns a Blender binary or ASCII FBX 7.x file
derived from the vanilla leaf-plant mesh into a reviewable Tree Saplings
held-tool package.

The imported FBX is the editable source format. The live preview is constructed
from the same parsed, triangulated, normalized geometry used by the runtime, so
FBX loader pivots and model transforms cannot alter its placement. Builds create
separate `TreeSaplingHeldFp.dae` and `TreeSaplingHeldTp.dae` profiles. Each is
a minimal COLLADA 1.4.1 mesh whose vertices are fully
weighted to `root_bucket_jnt` below `jnt_right_weapon`, matching the vanilla
Clay tool's attachment contract. Scrap Mechanic loads this DAE from the
generated renderable; it does not load the source FBX directly.
Position fields and translation snapping are centimeters in both the gizmo and
generator and are converted to game units exactly once. The preview uses the
same power-of-ten source normalization as the generated positions, while
rotated normals are renormalized and the vanilla Clay skin bind remains
unchanged. Its **SCREEN** mode follows the official first-person Bucket
animation's `jnt_camera`; the alternate orbit mode is intended for editing.
Imports are
analyzed once for their longest axis; the Tree Saplings template starts new
projects upright and exposes **Auto Upright** for existing projects.
The exact vanilla Clay bind pose is the immutable base transform. Editor
position, rotation, and scale are baked into tool-local geometry beneath that
base, so neither animated joint coordinates nor editor transforms can deform
the skin controller.

## Safety boundary

- The imported FBX is copied into the project and checksum-locked.
- Binary FBX records are parsed locally. Untouched properties retain their
  original encoded bytes; only the selected model's local translation,
  rotation, and scale are generated into the output copy.
- The editor reads vanilla character, animation, and texture assets directly
  from the installed game for preview only.
- Build output is staged, parsed again, hashed, and then moved into the selected
  output folder.
- Existing output is replaceable only while every recorded artifact still
  matches the previous manifest.
- No action installs a mod, edits the game, launches the game, or updates the
  ScrapLab repository.

## Project and template contracts

`project.scraptool.json` schema 2 stores the source hash, selected model and
material, independent first-person and third-person transforms, Clay/Bucket
preset, permanent Tree Sapling UUIDs, colors, and output location. Schema-1
projects migrate by cloning the original transform into both profiles, so no
existing calibration is discarded. Template versioning leaves room for future
animation and tool families without changing existing projects.

The generated `TreeSaplingTool.assets.json` maps package files to intended game
paths. `TreeSaplingTool.generated.lua` is produced from a protected copy of the
current source and changes only the animation and held-renderable functions.

## Building

```powershell
powershell -ExecutionPolicy Bypass -File .\build-tool-forge.ps1
```

The build creates a portable folder under `dist/ToolForge`, runs the built-in
FBX/generator safety tests, and produces a local zip under `release/`.
