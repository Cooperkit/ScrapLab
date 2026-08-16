# ScrapLab Tool Forge

ScrapLab Tool Forge is a separate, generate-only character-tool builder. Version
1 focuses on the Tree Saplings held model and does not write to Scrap Mechanic
or the ScrapLab source tree.

## GUI workflow

1. Run `ScrapLab.ToolForge.exe`.
2. Choose **New** and select the modified Blender binary or ASCII leaf-plant
   FBX 7.x file.
3. Save `project.scraptool.json` in a new project folder.
4. Position the mesh independently on the live first- and third-person Bucket
   rigs. The view buttons select both the preview rig and exported profile;
   **Copy FP → TP** and **Copy TP → FP** provide a starting point. Tool Forge
   renders the exact normalized triangle stream used by each generated DAE.
   **SCREEN** uses the official first-person Bucket camera; turn it off for
   the orbiting edit view. **Auto Upright** aligns the source mesh's longest
   axis to the character rig's Y-up space.
5. Validate, then build the ScrapLab-ready review package.

The source FBX is copied into `Assets/Source` and protected by SHA-256. Binary
FBX output preserves untouched records while rewriting only the selected mesh's
vertex and normal arrays and resetting its model node to identity. Scrap
Mechanic discards arbitrary model-node transforms while compiling character
tools, so each transform must be baked into geometry. The separate FP and TP
runtime `.rend` files use their matching generated DAE controllers bound to
`root_bucket_jnt`, following Scrap Mechanic's vanilla Clay tool while allowing
both hand rigs to be calibrated independently. The transformed FBX files remain
geometry-validation artifacts. Builds are written to a separate package folder.
A rebuild stops if any prior generated artifact was edited.

Position fields and movement snapping are measured in centimeters. The live
preview and Build Package now share the same extracted triangles, power-of-ten
normalization, centimeter conversion, Clay bind matrix, rotation order, and
local transform contract. Blender-only FBX pivots, scene transforms, and loader
axis conversions cannot make the editor disagree with the runtime DAE. The
exported transform is baked into validated positions and unit normals while the
Clay skin bind stays immutable.

## Command line

```text
ScrapLab.ToolForge.exe validate --project project.scraptool.json
ScrapLab.ToolForge.exe build --project project.scraptool.json --output C:\Builds
ScrapLab.ToolForge.exe selftest
```

To open a project directly in the editor:

```text
ScrapLab.ToolForge.exe --project project.scraptool.json
```

## Runtime requirements

- 64-bit Windows with .NET Framework 4.8.
- Microsoft Edge WebView2 Runtime.
- A valid Scrap Mechanic installation for the live character and animation
  preview.

Three.js r185 is bundled under its MIT license. WebView2 redistributable files
are bundled under Microsoft's included license and notice.
