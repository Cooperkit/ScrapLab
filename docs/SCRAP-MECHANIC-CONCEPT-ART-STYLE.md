# Scrap Mechanic Visual Design Reference

## Mandatory use

Read this document before making concept art, a model, texture, icon, animation, or visual effect for a ScrapLab feature.

Do not begin by prompting for "a Scrap Mechanic style object." That phrase is too vague and repeatedly produces generic orange industrial science-fiction art. First identify the correct asset family, the gameplay scale, and any existing game item involved. Then use the family-specific rules in this document.

This document is intended to replace a fresh visual audit for normal concept work. Reinspect the game only when:

- A game update introduces a major new visual family.
- The brief depends on an existing asset not documented here.
- The requested asset must physically attach to or reuse a particular model.
- The guide and the current game files disagree.

## Reference version and audit scope

The current reference was rebuilt from Scrap Mechanic `1.0.5.876`, Steam build `24529696`.

The audit covered:

- 93 official top-level shape-set files.
- 1,455 registered block and part records parsed without errors.
- 6,975 FBX, 2,100 DAE, 832 OBJ, and 239 compiled mesh files catalogued by family.
- 5,647 TGA and 8,623 PNG files catalogued.
- The creative and survival item atlases and their XML registrations.
- English inventory names and descriptions, collision dimensions, default paint colors, renderable definitions, materials, texture references, and preview rotations.
- Representative diffuse, ASG, normal, light-cap, mat-cap, metal-profile, pose, and animated-UV assets.
- Detailed contact-sheet review across handheld tools, mechanics, logic, vehicles, farming, production, storage, construction, warehouse, decor, lighting, scrap, spaceship, Chapter 2, underground, organic, and environmental families.
- Geometry inspection of 28 editable handheld DAE assets. Individual handheld components range from simple 155-vertex forms to 5,650-position hero tools. The median inspected file contains about 892 position vertices. The style is intentionally low-to-mid complexity, not primitive low-poly and not modern high-density hard-surface modeling.

The local audit boards are stored under `work/deep-style-audit/`. They are research artifacts made from installed game icons and must not be distributed with ScrapLab.

## The most important correction

Scrap Mechanic does not have one universal "orange scrap machine" style.

It has several related visual families. They share readability, playful exaggeration, stylized materials, and game-scale construction, but they differ strongly in cleanliness, palette, geometry, and technology. A farm container, warehouse compressor, spaceship wall, scrap engine, Dekotora lamp, plasma drill, and Spudgun should not be forced through the same palette or wear recipe.

The previous guide made four incorrect universal rules:

1. It treated red, yellow, orange, gray, and cyan as the whole game's palette.
2. It required nearly every object to look worn, welded, and improvised.
3. It favored short, stout forms even when official tools are elongated or open-framed.
4. It invented new ammunition instead of checking whether "Small Explosive Canister" named an existing game part.

Never repeat those assumptions.

## Step zero: resolve the brief against game data

Before visual design, search the inventory descriptions and item declarations for every capitalized or game-specific noun in the brief.

For each named object, record:

- Exact in-game title.
- UUID.
- Shape-set name and internal object name.
- Collision dimensions.
- Inventory icon silhouette.
- Renderable, model, and diffuse texture paths.
- Default paint color and whether the surface is paintable.
- Relevant gameplay description.

If an exact game item exists, use that item. Do not reinterpret it as a generic object with a similar English name.

### Current example: Small Explosive Canister

The requested launcher ammunition is the existing **Small Explosive Canister**, not a drink-can-shaped custom round.

| Property | Verified value |
|---|---|
| Title | Small Explosive Canister |
| UUID | `8d3b98de-c981-4f05-abfe-d22ee4781d33` |
| Internal name | `obj_interactive_propanetank_small` |
| Shape set | `Data/Objects/Database/ShapeSets/interactive.shapeset` |
| Collision box | `2 x 2 x 2` build-grid units |
| Default paint color | `cb0a00` |
| Stack size | 10 |
| Renderable | `Data/Objects/Renderable/Interactive/obj_interactive_propanetank_small.rend` |
| Model | `Data/Objects/Mesh/interactive/obj_interactive_propanetank_small.fbx` |
| Diffuse texture | `Data/Objects/Textures/interactive/obj_interactive_propanetank_small_dif.tga` |
| Material treatment | Painted metal using pose animation plus diffuse, ASG, and normal maps |

Its silhouette is a compact red rounded-square container with a dark protective top handle/cage, a central metal valve or cap, darker lower panels, and bold yellow warning graphics. Any launcher concept must visibly accept this square 2 x 2 x 2 object. A small circular barrel and cylindrical cartridges are incompatible with the brief.

## Universal visual grammar

These rules recur across most asset families.

### 1. Readability before detail

- The object must read from the normal game camera and in a 96 x 96 inventory cell.
- Use a small number of clearly separated masses.
- Let silhouette, color blocking, and large material breaks carry the design.
- Details that disappear when the concept is reduced to 96 px are supporting details, not the design.
- Do not use dense, evenly distributed greebles.

### 2. Function is exaggerated

- Handles are large enough to notice.
- Buttons, switches, connection ports, blades, drills, lights, seats, bearings, and output openings are visually obvious.
- A machine usually exposes the one component that communicates its job.
- The object can contain whimsical or physically simplified engineering, but the player should understand how to use it.

### 3. Forms are stylized, not uniformly chunky

- Most hard surfaces use readable bevels or rounded edges.
- Cylinders, boxes, hoops, rails, panels, pipes, and frames are simplified into strong graphic shapes.
- Thin sheets, antennae, rails, blades, signs, braces, and cables are valid when their function requires them.
- Long tools and pipes can be highly elongated. Broad machines and containers can be nearly cubic.
- Choose proportions from function and official family references, not a universal short-and-stout rule.

### 4. Parts are built in modules

- A primary body is combined with distinct functional modules.
- Modules commonly use visible seams, color changes, collars, brackets, or frames.
- Upgradeable parts preserve a recognizable core while adding modules, guards, lights, or higher-quality housings.
- Repetition is used deliberately: stacked engine layers, repeated coils, paired cylinders, repeated vents, or a row of resource ports.

### 5. Color is segmented

- Color normally follows components and materials, not random camouflage patches.
- Painted housings, exposed metal, rubber, resource windows, and powered indicators form separate color regions.
- Small bright accents help identify controls, resources, or active elements.
- The default `color` field in a shape set is often a player-paint tint and is not proof that the entire visible asset is that color.

### 6. Surface information is hand-authored and broad

- Diffuse maps carry recognizable painted color variation, broad wear, labels, grime placement, and value grouping.
- ASG and normal maps add controlled surface response and form detail.
- Materials remain readable and colorful rather than physically perfect.
- Wear is placed at useful edges and contact points. It is not uniform procedural noise.
- Large scratches and chips may be visible on scrap or industrial assets, but they do not cover every surface.

### 7. The tone is playful and usable

- Even dangerous objects use bold, toy-like readability.
- Ordinary objects remain recognizable: toilet, cash register, mattress, kitchen pot, sign, battery, crate, engine, drill.
- Designs may be funny, improvised, colorful, or exaggerated without becoming parody props.
- Avoid grim military realism and generic cinematic science fiction.

## Asset families

Choose exactly one primary family before creating a concept. A secondary family may influence a few details, but it must not erase the primary family.

### A. Handheld mechanic tools

Examples: connect tool, paint tool, weld tool, sledgehammer, impact driver, clay gun.

Traits:

- Strong, instantly recognizable working end.
- Grip and hand position are clear.
- Silhouette may be compact, forked, circular, or elongated depending on function.
- Mechanical and household forms are combined freely.
- Bright colors can be playful and broad, especially on paint and connection tools.
- Hero tools can be more detailed than buildable blocks, but details remain grouped rather than evenly scattered.
- The visual center is the action: prongs for connecting, spinning paint hardware, welding jaws, hammer head, or clay mechanism.

Do not force all handheld tools into a gun silhouette.

### B. Spudgun weapon family

Examples: Scrap Spudgun, Spudgun, Spud Shotgun, Spud Gatling, Spud Launcher.

Verified construction structure:

- Base or receiver.
- Barrel.
- Sight.
- Stock.
- Tank or reservoir.
- Grip and animation rig.

Official assets intentionally mix manufactured weapon parts with repurposed forms. Production folders include broom and mop stocks, gas-can and glove tanks, can and iron-clamp sights, a fryer-like barrel, a scrap barrel, and a spinner barrel.

Traits:

- Usually elongated, but each variant has one strong silhouette change.
- A thin or moderate central body links visually distinct modules.
- Yellow/orange grip and base pieces, red barrel or receiver pieces, gray metal, pale reservoir forms, and dark recesses recur, but not as a fixed percentage formula.
- Modules have different shapes and materials rather than merging into one armored shell.
- The finished object looks assembled, colorful, and slightly absurd.
- Existing variants are not dominated by pressure gauges, military rails, tactical stocks, or realistic magazines.

For new weapons, begin by sketching the six modules separately. Then assemble them. Do not begin with a generic firearm and add Scrap Mechanic colors afterward.

### C. Scrap and early-survival machinery

Examples: Scrap Gas Engine, Scrap Driver's Seat, damaged spaceship pieces, crude survival stations.

Traits:

- Strong asymmetry and visible repair.
- Salvaged boards, bent metal, exposed hardware, tied or taped parts, and mismatched modules.
- Dirtier and rougher than standard mechanic parts.
- Warm rust-red, orange, dull yellow, dark metal, old wood, and faded paint are common.
- Silhouette remains readable despite damage.

Use the heaviest wear here, not on every game asset.

### D. Standard mechanics, logic, and upgradeable parts

Examples: engines, controllers, sensors, pistons, suspensions, seats, thrusters, chests.

Traits:

- Compact manufactured housings with clear mounting logic.
- Orange/yellow frame pieces often surround gray, black, white, red, or cyan functional cores.
- Rounded rectangles, protective hoops, stacked plates, visible coils, and clean ports recur.
- Upgrade levels add organized complexity: more coils, guards, lights, fins, polished surfaces, and stronger color separation.
- Higher levels can look cleaner and more advanced than scrap parts.

Avoid treating them as rusty handmade prototypes.

### E. Farming, food, production, and storage

Examples: crop crates, growbeds, containers, Craftbots, Resource Collector, consumables, seeds.

Traits:

- Friendly, rounded, colorful shapes.
- Resource identity is strong: green fertilizer, blue water, red gas, produce-colored crates, visible produce, leaves, and labels.
- Containers use simple box silhouettes with one or two resource-specific caps, windows, handles, or bands.
- Craftbots combine orange/yellow frames with appliance-like machinery and characterful moving parts.
- Organic objects use saturated stylized color and soft hand-painted value variation.
- Surfaces range from clean plastic and painted metal to wood, cloth, paper, food, and crystal.

Do not add industrial grime to food and seed assets unless the exact family requires it.

### F. Construction, industrial, and warehouse parts

Examples: beams, ducts, pipe fittings, compressors, tanks, scaffolds, warehouse frames, signs.

Traits:

- Grid-aware proportions and repeatable modular edges.
- Large flat planes, rails, braces, flanges, vents, mesh, and structural frames.
- Gray, white, orange, yellow, and red are common, but muted blue, green, tan, and bare metal also appear.
- More straight edges and thin structural parts are allowed than in handheld tools.
- Warning stripes and signage appear where functionally justified.
- Geometry is often simpler than hero tools because many pieces tile or repeat.

Do not turn a handheld concept into warehouse equipment merely by adding hazard stripes and a pressure gauge.

### G. Decor, furniture, and ordinary props

Examples: signs, cash register, mattress, mirror, paper stack, kitchen pot, toilet paper, lamps.

Traits:

- Recognizable real-world silhouette comes first.
- Proportions are gently exaggerated.
- One color or material idea often dominates.
- Detail density is low to moderate.
- Assets may be clean, humorous, soft, domestic, or lightly worn.
- Small visual jokes are common, but the object remains usable and recognizable.

### H. Spaceship and facility technology

Examples: spaceship wall panels, ship ventilation, ship wiring, elevator systems, packing stations, key readers.

Traits:

- Modular panels and equipment sized to architectural grids.
- White, gray, red, yellow, blue, and black are arranged in bold component blocks.
- Damaged ship assets combine clean manufactured forms with broken edges and exposed internals.
- Screens and status colors are clear, bright, and limited.
- Shapes are more manufactured than scrap machinery but still stylized and readable.

### I. Chapter 2, underground, crystal, and alien technology

Examples: plasma drills, gems, molten orbs, crystalline harvestables, cablebot parts, underground materials.

Traits:

- Stronger blue, cyan, magenta, violet, green, and iridescent accents.
- Dark metal frames can surround luminous or crystalline cores.
- Organic faceting, segmented shells, cables, and energy channels appear.
- Higher technology can be cleaner, smoother, and more reflective.
- Glow is feature-specific and can be more prominent than in standard mechanic parts.

Do not apply this neon palette to ordinary workshop tools without a gameplay reason.

### J. Dekotora and high-decoration assets

Examples: neon bearings, bulbs, light frames, arrows, stars, scrolling panels.

Traits:

- Cyan, magenta, pink, white, and polished metal are intentionally intense.
- Repeated light modules and graphic patterns are the feature.
- Forms are decorative, symmetrical, and clean.
- This is an exception to the restrained-glow rule.

Do not use Dekotora as the default reference for unrelated parts.

### K. Organic and environmental assets

Examples: trees, roots, crops, minerals, crystals, mud, ore, alien plants.

Traits:

- Simplified clustered shapes and strong silhouettes.
- Hand-painted gradients and color variation are more important than mechanical edge wear.
- Trees use readable trunk and canopy masses rather than realistic leaf density.
- Crystals and ores use bold color identities and exaggerated facets.

## Proportion and scale

### Build-grid awareness

- Buildable objects must respect their registered collision footprint.
- Show the expected mounting face, sticky faces, ports, or connection points.
- A concept that visually fits a 1 x 1 part cannot secretly require a 4 x 4 collision body.
- Existing ammunition or connected containers must fit the visible loading or attachment space.

### Handheld scale

- Verify the player interaction and animation category before deciding pistol, two-handed tool, or shoulder launcher scale.
- A 2 x 2 x 2 buildable canister is large relative to existing Spudgun ammunition. A weapon that launches it needs an open cradle, square chamber, fork, sling, lift arm, or visibly oversized receiver.
- Do not shrink an existing game part merely to make the concept convenient.

### Complexity

- Use simple geometry for repeated structural pieces.
- Use moderate detail for interactables and containers.
- Reserve the highest detail for handheld hero tools, major machines, characters, and focal technology.
- The inspected tool meshes confirm that silhouette-defining modules receive detail while simple stocks, handles, and barrels can remain much lighter.

## Materials and wear by family

Do not use one universal material recipe.

| Wear level | Appropriate families | Treatment |
|---|---|---|
| 0 - Clean | Dekotora, some Chapter 2 tech, gems, new plastic, screens | Clear color, controlled highlights, almost no chipping |
| 1 - Used | Standard mechanic parts, containers, tools, furniture | Small edge wear, contact marks, clean recess definition |
| 2 - Industrial | Warehouse machinery, ship equipment, heavy tools | Broader chips, grease at joints, worn metal edges |
| 3 - Scrap | Early-survival and damaged assets | Mismatched paint, dents, patches, heavy localized wear |

Universal material rules:

- Paint chips reveal a coherent under-material.
- Rubber remains dark and broad, with readable molded or wrapped ridges.
- Metal highlights should support form, not make everything chrome.
- Plastic, cloth, wood, food, stone, crystal, and organic surfaces must retain their own identity.
- Do not apply gritty photoreal roughness noise to the whole asset.
- Do not use modern cinematic PBR as the concept-art target. Aim for a hand-painted, stylized game asset with controlled material response.

## Color selection workflow

Do not use a fixed global percentage palette.

1. Choose the asset family.
2. Identify the gameplay resource or interaction color.
3. Choose the base construction materials.
4. Select one primary paint color and one supporting color from the family.
5. Add emissive color only when the part is powered, active, or resource-coded.
6. Check the 96 px preview for clear color separation.

Useful recurring anchors, not universal requirements:

- Mechanic orange/yellow: `df7f01`, `c98605`, `ebb100` family.
- Explosive red: `cb0a00` family.
- Water and utility blue: `3e9ffe`, `0b9ade` family.
- Green farm/organic paint: `577d07`, `83a633` family.
- Dark housing: near-black, charcoal, cool gray.
- Light housing: warm white, pale gray, galvanized metal.
- Powered cyan: use only for active electronics, plasma, water, or a deliberate interface cue.

The installed asset audit shows strong colors across the entire spectrum. Orange is common because many parts are paintable or mechanic-coded; it is not permission to make every original part orange.

## Handheld design workflow

Use this process for tools and weapons.

### Pass 1: gameplay diagram

Draw only:

- Hands or grip location.
- Input or ammunition.
- Loading or attachment path.
- Energy, pressure, spring, or motor source.
- Working end or muzzle.
- Moving component.

If these cannot be explained in one diagram, the design is not ready for styling.

### Pass 2: module silhouettes

Create four silhouettes using only flat shapes. No texture, scratches, gauges, bolts, or labels.

Each silhouette must differ by mechanism, not decoration. Examples:

- Open cradle versus enclosed chamber.
- Lever thrower versus pneumatic ram.
- Top-loading fork versus side-loading square breech.
- Elastic or spring sling versus powered piston.

### Pass 3: family construction

Choose the closest official family and assign modules accordingly. For a Spudgun-relative design, explicitly define base, working end, sight or aiming cue, support or stock, energy source, and grip.

### Pass 4: color and materials

Apply the family's cleanliness and palette. Do not add a gauge, hose, tank, light, or hazard stripe unless the gameplay diagram needs it.

### Pass 5: 96 px test

Reduce the concept to 96 x 96. Confirm:

- The working end is obvious.
- The grip and balance are believable.
- The ammunition or input type is recognizable.
- The design does not collapse into a generic gun shape.

## Concept-art presentation

### Exploration sheets

- Prefer simplified stylized game-asset renders or clean colored design sketches for early exploration.
- Use consistent three-quarter or side views and equal scale.
- Keep the complete object inside the frame.
- Use a flat neutral background with minimal presentation decoration.
- Use number labels only unless annotations are requested.
- Do not let glossy final rendering hide a weak silhouette.

### Final model concepts

- Include a hero three-quarter view plus side, front, and top views when modeling will follow.
- Show moving parts in both states when relevant.
- Place the exact existing ammunition, container, or connected part beside the concept for scale.
- Include a player-hand or simple scale block only when scale is otherwise ambiguous.
- Use soft game-preview lighting, not dramatic cinematic lighting.

### Inventory icons

- Transparent background.
- Elevated three-quarter view consistent with the game's atlas.
- Strong padding and no cropping.
- No text, badge, environment, floor, or dramatic particle effect.
- Preserve the actual paint and material separation at 96 x 96.

## Image-generation rules

Image generation tends to drift toward generic industrial science fiction. Counteract that explicitly.

### Always state

- The selected asset family.
- The exact existing item and UUID when one is involved.
- Collision or scale constraints.
- The visible operating mechanism.
- The required cleanliness/wear level.
- "Hand-painted stylized game asset, low-to-mid complexity, broad color blocks, restrained surface noise."
- "Modules remain visually separate; do not merge them into one armored shell."
- "No modern military design and no generic sci-fi hard-surface concept art."

### Avoid prompt traps

Do not overuse these words because image models exaggerate them:

- Industrial.
- Hazard.
- Heavy.
- Pressure gauge.
- Pipes and hoses.
- Rivets everywhere.
- Battle-worn.
- PBR.
- Realistic.
- Sci-fi.

Use exact visual functions instead. For example, say "one hose from the lower reservoir to the rear ram" rather than "lots of exposed industrial hoses."

### Generation sequence

For a four-concept request:

1. Generate a mechanism and silhouette sheet first.
2. Reject concepts that misunderstand existing items or scale.
3. Select viable silhouettes.
4. Generate colored game-style refinements.
5. Only then request a polished 3D model concept.

Do not jump directly to four highly rendered red-and-yellow weapons. That workflow hid the errors in the previous concepts.

## Reusable prompt scaffold

```text
Use case: stylized-concept
Asset type: ScrapLab [tool / part / machine / icon] concept

Gameplay brief:
[One sentence describing what the object does.]

Existing game references:
- Exact item: [title and UUID, or "none"]
- Required scale or collision footprint: [verified value]
- Primary visual family: [one family from this guide]
- Secondary influence: [optional, limited]

Functional diagram:
- Player or mounting interaction: [grip, seat, port, sticky face]
- Input: [exact item, fluid, power, signal]
- Mechanism: [one clearly described mechanism]
- Output: [projectile, logic, crafted item, movement]
- Visible moving part: [exact part]

Visual construction:
[Three to five large modules only. Explain the purpose of each.]

Color and material:
[Family-specific palette and wear level.]
Hand-painted stylized game asset, low-to-mid complexity, broad color blocks, restrained surface noise.
Modules remain visually separate; do not merge them into one armored shell.

Presentation:
[Silhouette sheet / colored exploration / orthographic model sheet / transparent icon.]

Must preserve:
[Exact existing item shape, scale, attachment, color identity, or other invariants.]

Avoid:
generic sci-fi hard-surface design, modern military firearm language, random gauges, decorative hoses, repeated hazard stripes, all-orange paint, excessive cyan glow, dense micro-greebles, photoreal grime, armored shell, logos, watermark
```

## Launcher-specific corrected brief

Use this section when returning to the handheld Small Explosive Canister launcher.

### Non-negotiable requirements

- Ammunition is the exact Small Explosive Canister UUID `8d3b98de-c981-4f05-abfe-d22ee4781d33`.
- Preserve its red rounded-square body, top handle/cage, central cap, dark lower panels, yellow warning graphic, and 2 x 2 x 2 proportions.
- Show the full-size canister beside the weapon and inside or on the loading mechanism.
- The weapon must be handheld, but the canister size probably requires a two-handed or shoulder-scale tool rather than a pistol.
- The loading opening cannot be a small circular barrel.
- The weapon must use a square chamber, open cradle, fork, sling, lift arm, or another mechanism that visibly accepts the canister.
- The design may be related to Spudgun construction, but it must not simply be the official launcher with a square muzzle.

### Appropriate visual direction

- Primary family: Spudgun weapon family.
- Secondary influence: standard mechanic machinery or scrap machinery, depending on the desired progression tier.
- Use one memorable launching mechanism and a small number of visible modules.
- Reuse the visual idea of repurposed household or workshop components, not the exact official meshes.
- Let the canister's red body be a major color anchor; the launcher itself does not also need to be mostly red.
- A broad open frame may match the game better than a sealed red receiver.

### Four mechanism directions worth exploring

1. **Spring cradle:** a square open basket on rails with a large compressed spring and a clear release lever.
2. **Pneumatic ram:** a rear cylinder pushes the canister out of a short square guide, with one visible reservoir and one hose.
3. **Mechanical thrower:** a compact hinged arm or cup launches the canister from above the tool body.
4. **Twin-fork sling:** two mechanic-style arms hold the canister between them and snap forward using an exposed elastic or torsion system.

These are mechanism directions, not finished visual designs. Their silhouettes must be tested before color rendering.

## Review rubric

Score each category from 0 to 2. A concept must score at least 16 out of 20 and cannot score 0 in Brief accuracy, Scale, Function, or Family match.

| Category | 0 | 1 | 2 |
|---|---|---|---|
| Brief accuracy | Invents or changes named game items | Mostly correct | Exact verified items and invariants |
| Scale | Impossible or inconsistent | Ambiguous | Existing items and player interaction fit visibly |
| Function | Operation is hidden | Partly readable | Input, mechanism, movement, and output are obvious |
| Family match | Generic sci-fi or wrong family | Mixed cues | Clearly belongs to the selected official family |
| Silhouette | Generic blob | Readable | Distinct mechanism at 96 px |
| Module design | Armored shell or random greebles | Some separation | Clear functional modules |
| Color | Universal orange/red treatment | Mostly coherent | Family and gameplay color logic |
| Materials | Photoreal noise or wrong wear | Partly suitable | Correct family-specific surfaces and wear |
| Originality | Copies an official asset | Familiar arrangement | New mechanism using shared visual grammar |
| Presentation | Cropped, noisy, or misleading | Usable | Clean comparison and truthful scale |

## Rejection checklist

Reject the concept immediately if any answer is yes:

- Did it replace an existing named game item with invented ammunition or geometry?
- Is the item physically too large for the visible chamber, holder, or attachment?
- Did the concept begin as a generic firearm or machine and receive game colors afterward?
- Are four variants mainly the same silhouette with different greebles?
- Is most of the identity coming from scratches, hazard stripes, or cyan lights?
- Are modules merged into one modern armored housing?
- Does it look like military equipment, a realistic power tool, or generic sci-fi concept art?
- Is the wear level wrong for the selected family?
- Does the concept need explanatory text because the working end is unclear?
- Does it fail when reduced to 96 x 96?

## Maintenance

When a concept fails, update this guide with the root cause, not merely a new negative adjective. When the game updates, keep the universal grammar stable and amend only the affected family. Record exact UUIDs and dimensions whenever a future brief depends on an existing item.
