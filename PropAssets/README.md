# Prop assets

The street props the mod places itself (cordon, stretcher, police and paramedic equipment, accident and
crime-scene dressing), modelled by script in Blender and shipped with the mod as one asset bundle. Parallel to
`CharacterAssets\` and independent of it: own scripts, own Unity project, own bundle.

```
build.ps1                 everything, start to finish (see below)
build_bundle.ps1          only the last step: build\ -> the asset bundle, and the check that it loads
scripts\
  atlas.py                paints the one texture atlas (surface finishes and lettered faces) and says where each region is
  prop_kit.py             the modelling kit: box, lathe, tube, prism, loft, grid; atlas mapping; export; preview renders
  props.py                THE PROPS: one function each, and the list with triangle budgets
  city_props.py           the city-life props (tools, things carried, the coffee cart): same rules, listed by props.py
  life_props.py           the second round (things held, pushed and worn, job props, street furniture): likewise
  props_<name>.py         the later rounds, a module each (civic, service, events, everyday, garage, family, site), with its
  paint_<name>.py           own painting module (what it adds to the atlas) and
  checks_<name>.py          its own measurements for check_life.py
  scenes.py               whole scenes made of the exported props: previews\scene_<name>.png
  build_props.py          builds every prop into build\ and renders previews\
  check_life.py           measures the handles and heights that animation clips rely on; the build fails if one has moved
  overview.py             stacks preview sheets into one image
UnityProject\             a bare 2022.3 project whose only job is to turn build\ into the bundle
previews\                 a render of every prop, and of a whole scene (committed: look at them)
CREDITS.md                source and licence of every asset
_reference\ _tools\ build\     generated, git-ignored
```

## Run it

Needs Blender (`winget install BlenderFoundation.Blender`; made with 5.2) and Unity 2022.3.62f2 (the game's
version). The game is not needed. Then:

```
powershell -File PropAssets\build.ps1
```

It installs Pillow into `_tools\pylib` (nothing system-wide), paints the atlas, builds every mesh into `build\`,
renders `previews\`, builds `Assets\Mods\RealLifeOverhaul\Bundles\reallife_props` and loads it back as a check
(about two minutes, most of it Unity). `-SkipPreviews` and `-SkipBundle` shorten it. The dotnet build of the mod
copies `Bundles\` into the installed mod folder.

**Look at the renders before committing.** `previews\<id>.png` shows the prop from the front and the back, then
beside a 1.8 m bar banded every 0.3 m. `scene_cordon.png`, `scene_cordon_street.png` and the `scene_*.png` of `scenes.py` (a precinct frontage, a bus stop
with its shelter and bus, a fire, a hotel frontage, a market, a film shoot, ...) show whole scenes. The
previews are drawn from the exported vertices, normals and UVs with the atlas's colour, metallic, smoothness and
occlusion, and nothing else: no Blender-only shading.

## How it is made

- **One atlas, two materials.** Every prop is one mesh with one material slot. All of them share
  `props_base` (4096 x 2048, colour) and `props_mask` (2048 x 1024, HDRP mask map: metallic, occlusion, -,
  smoothness). **Both sides of both must be powers of two:** Unity silently keeps any other size uncompressed
  (the 2048 x 1280 atlas shipped as RGBA32, 17.5 MB of video memory, not the 4.2 MB this file used to claim);
  the bundle check now fails if a texture is not BC7. The mod makes two materials from the game's `HDRP/Lit` at run time, both instanced: `lit`, and
  `glow` (the same, lighting itself faintly in its own colours) for what must read at night. No normal map: the
  shapes carry chamfers and area-weighted normals instead, which is what gives the edges their highlight.
- **Swatches and decals.** A swatch is one surface finish whose patch gets dirtier towards the bottom; a part
  mapped onto it by height picks up grime and contact shadow where it meets the ground, with no unwrap. A decal
  is a painted face (the barrier board, a badge). The tape's row spans the whole atlas so it tiles along U.
- **No colliders, scripts or materials in the bundle**, meshes are not kept readable, textures are BC7 with
  mipmaps. `PropLibrary` does nothing per frame.

## Budgets (kept by the build: it fails if a prop, or the total, goes over)

| | limit | now |
|---|---|---|
| triangles, each prop | its own budget, beside its maker in the `PROPS` lists | see the tables below |
| triangles, all props together | 130 000 | 118 104 (240 props: the two-wheelers are in the bundle whole and in parts, the city bus in parts) |
| materials | 2, shared by everything | 2 |
| textures | one atlas: 4096 x 2048 colour + 2048 x 1024 mask, BC7 (the bundle check fails on anything else); the two road paint pictures | 14.0 MB of video memory (10.7 + 2.7, and 0.7 for the road paint), 248 regions, 87 % of the atlas used |
| meshes in memory | | 6.6 MB |
| bundle on disk | 16 MB | 10.6 MB, loaded once per session, asynchronously |

**Why the ceilings went up** (they were 30 000 triangles, a 2048 x 2048 atlas and 9 MB for 77 props). The first two
rounds were things in a hand and a cordon. The later rounds are a police station's frontage, bus stops and a bus,
a fire engine, hotels, street events, home and social life, a garage and a building site: 240 props, among
them vehicle bodies of 3 700 to 6 300 triangles. None of it is drawn unless a feature places it, a placed prop is one
instanced draw call with one of two materials, and a scene shows a dozen at most: the totals are what sits in memory,
not what is rendered. Memory is the real cost: about 6.6 MB of meshes and 14.0 MB of texture, where the game's own
city uses gigabytes. The atlas doubled in width only (BC7 keeps it at one byte a pixel); `PropLibrary` reads exactly
one bundle with one colour and one mask texture, so a second bundle or a second atlas would have needed code and a
third material. If the atlas fills again, 4096 x 4096 is the next step (27 MB); nothing else changes.

### Working on one module

`PROPS_DEV=name[,name]` makes `atlas.py`, the build and the checks use the base props and only those modules, and
`PROPS_BUILD=<folder>` sends everything they write to a folder of one's own (`PropAssetsuild_*` is git-ignored).
With both set, several people can work on a module each at the same time without the others' files having to be
whole. Previews of single props: `blender -b -P scriptsuild_props.py -- id id`. A module may list `SIDES` (see the
end of `props.py`): those props also get `previews\<id>_sides.png`, six views beside what they go over or who uses them.

## Using a prop

```csharp
if (PropLibrary.TryGet("cone", out Mesh mesh, out Material material))
{
    filter.sharedMesh = mesh;               // shared: never change it
    renderer.sharedMaterial = material;     // shared: never .material, never a property block per instance
}
else
{
    // not loaded yet, bundle missing, or no such prop: build the plain shape as before
}
Vector3 size = PropLibrary.SizeOf("cone");  // bounds in metres, zero if unknown
```

`PropLibrary.Load(context)` is called when the city loads and `Dispose()` when it unloads (`Core\RealLifeOverhaulMod.cs`).
The load takes a moment: a prop built in the first second of a city gets `false`. Either build props when they
are first needed (the cordon and stretcher already do) or check `PropLibrary.Ready`.

Unity axes: X right, Y up, Z forward. All sizes are width x height x length (X x Y x Z) in metres. Use scale 1
unless noted. Ground props have their pivot on the ground at the centre of their footprint.

### Cordon

| id | size | triangles | material | pivot and facing | use |
|---|---|---|---|---|---|
| `cone` | 0.36 x 0.75 x 0.36 | 274 | glow | ground centre; round | accents round a wreck |
| `tape_post` | 0.38 x 1.10 x 0.38 | 188 | glow | ground centre; grip loop spans X | a cordon post. **Tape ties on at 0.95 m** (`SceneCordon.TapeHeight`), the post is 1.10 m (`PostHeight`) |
| `tape` | 0 x 0.075 x 1.0 | 4 | glow | centre of the strip; runs along Z; reads from both sides | scale Z to the span: the lettering stretches with it, so use only for short gaps |
| `tape_4m` | 0 x 0.075 x 4.0 | 4 | glow | as `tape` | four repeats of the lettering. Scale Z by `length / 4`: undistorted at the cordon's 4 m spacing |
| `tape_4m_sag` | 0 x 0.145 x 4.0 | 32 | glow | centre of the strip **at the height of its ends**; hangs 0.07 m in the middle | the better-looking one; same scaling |
| `barrier` | 2.40 x 1.08 x 0.74 | 508 | lit | ground centre; the board runs along X, lettered on both faces | `LookRotation(road direction)` puts it across the lane, as now |

The tape is designed to run post to post, not cone to cone: a 0.75 m cone is too low to carry tape at a height
that reads as a barrier. Current code: position at the midpoint plus `TapeHeight`, `LookRotation(run)`, and
replace `localScale = (0.012, 0.075, length)` with `(1, 1, length / 4)`.

### Ambulance crew

| id | size | triangles | material | pivot and facing | use |
|---|---|---|---|---|---|
| `stretcher` | 0.60 x 0.87 x 2.03 | 1 698 | lit | ground centre; **head end at +Z**; flat of the mattress 0.70 m up (`Stretcher.BedHeight`), 2.0 m long (`HalfLength` 1) | replaces every piece `Stretcher.Build` makes except the sheet. The head section rises 10 degrees over the last 0.55 m (0.095 m at the end) under a pillow |
| `stretcher_sheet` | 0.56 x 0.36 x 1.96 | 768 | lit | **the stretcher's pivot**: child of the stretcher at zero | a covered body, head to toe. Replaces the `Sheet` object |
| `stretcher_blanket` | 0.56 x 0.37 x 1.32 | 480 | lit | the stretcher's pivot | feet to chest over someone carried as they are. Its top is 0.25 to 0.27 m above the mattress at hips and chest: a body lying flat fits under it, one lying awkwardly will poke through |
| `medic_bag` | 0.64 x 0.42 x 0.26 | 368 | lit | ground centre; emblem faces +Z and -Z | beside a kneeling paramedic |
| `first_aid_box` | 0.30 x 0.26 x 0.11 | 120 | lit | ground centre; face at +Z and -Z | |

### Police and paramedic equipment (on a character)

Parent to the bone with **local rotation zero** and the local position below, then tune by eye. The offsets are
measured on the game's detailed character in its bind pose (`_reference`, see "Fit to the character"), whose
hips, chest and skull bones are axis-aligned with the character there. They are a starting point: the street
characters have not been measured, and bones may carry a scale (divide the offset by `bone.lossyScale`, and
multiply the prop's local scale by its inverse to keep it life-size).

| id | size | triangles | material | pivot | bone (`HumanBodyBones`; rig name) | local position |
|---|---|---|---|---|---|---|
| `police_cap` | 0.23 x 0.14 x 0.31 | 492 | lit | centre of the band's lower edge; peak at +Z | `Head` (Skull) | (0, 0.113, 0) |
| `duty_belt` | 0.40 x 0.30 x 0.30 | 724 | lit | centre of the ring at half its height; buckle at +Z, holster at +X (right hip) | `Hips` | (0, 0.082, 0.029) |
| `badge` | 0.056 x 0.072 x 0.005 | 92 | lit | centre of its back; faces +Z | `UpperChest` | (-0.07, 0.143, 0.116) left breast |
| `chest_panel_police` | 0.11 x 0.04 | 12 | glow | centre, on the cloth; faces +Z | `UpperChest` | (0.07, 0.143, 0.116) right breast |
| `chest_panel_ems` | 0.09 x 0.042 | 12 | glow | as above | `UpperChest` | (0.07, 0.143, 0.116) |
| `back_panel_police` | 0.26 x 0.10 | 12 | glow | centre, on the cloth; **faces -Z**, already the right way round for a back | `UpperChest` | (0, 0.113, -0.111) |
| `back_panel_ems` | 0.26 x 0.10 | 12 | glow | as above ("PARAMEDIC") | `UpperChest` | (0, 0.113, -0.111) |
| `shoulder_radio` | 0.06 x 0.19 x 0.04 | 92 | lit | centre of its back (the clip); speaker faces +Z, cord hangs down | `UpperChest` | (-0.09, 0.193, 0.081) left collar bone |
| `handcuffs` | 0.20 x 0.012 x 0.08 | 424 | lit | ground centre, lying flat, closed | none (on the ground, or a hand) | |

- **Belt size.** The inside of the ring is an ellipse 0.150 m half width (X) and 0.118 m half depth (Z); the
  belt is 0.055 m high and 0.006 m thick. For another waist scale X by `halfWidth / 0.150` and Z by
  `halfDepth / 0.118`. The reference character measures about 0.145 x 0.11 at belt height over trousers.
- **Cap size.** The inside of the band is 0.076 m half width and 0.097 m half depth, 0.043 m high; the game's
  head measures 0.066 to 0.075 by 0.090 there, and the game's own hat 0.075. Hair is not hidden by it: use the
  game's `Hat` blend shape on the hair (as its own hats do) or a short style.
- The panels are curved to a 0.24 m radius. They sit on the cloth; push them out a few millimetres if they flicker.

### Accident and crime scene

| id | size | triangles | material | pivot and facing | use |
|---|---|---|---|---|---|
| `debris_glass` | 0.77 x 0.01 x 0.81 | 102 | lit | ground centre | glass round an impact; give it a random heading |
| `debris_plastic` | 0.64 x 0.06 x 1.16 | 160 | lit | ground centre | bumper and lamp fragments; random heading |
| `flare` | 0.03 x 0.03 x 0.29 | 116 | glow | ground centre; lies along Z, burning end at +Z | pair it with a light if it should light the road |
| `evidence_marker_1` .. `_4` | 0.09 x 0.125 x 0.084 | 8 each | lit | ground centre; number faces +Z and -Z | |
| `duffel_bag` | 0.70 x 0.35 x 0.30 | 260 | lit | ground centre; lies along X | |
| `spray_can` | 0.066 x 0.20 x 0.066 | 156 | lit | ground centre; standing | |
| `crowbar` | 0.11 x 0.02 x 0.72 | 116 | lit | ground centre; lies along Z, hook at +Z | |

A high-visibility vest is not here: it would need skinning to the body, which is the character pipeline's job.

### City life (`scripts\city_props.py`)

Hand tools stand on the ground at their working end with their length along Y, because that is how the clips of
`CityAssets` hold them. **Do not parent them by hand:** the clip that goes with a tool carries its grip
(`ClipLibrary.TryGet(clip).Grip`, applied by `HeldTool.Attach`), measured on the game's hand bones. The heights
below are where the clips' hands are; the model and the clip must agree.

| id | size | triangles | material | pivot and facing | held |
|---|---|---|---|---|---|
| `broom` | 0.43 x 1.49 x 0.10 | 162 | lit | middle of the bristles' edge; the shaft is Y | right hand 1.04 m up, left 1.33 m (`sweep`, `broom_rest`). The bristles are cut to lie flat when the shaft leans 30 degrees back |
| `shovel` | 0.23 x 1.20 x 0.07 | 284 | lit | point of the blade; hollow faces +Z | left 0.84 m, right in the D-handle 1.17 m (`shovel_dig`); right only (`shovel_rest`) |
| `jackhammer` | 0.58 x 1.04 x 0.27 | 622 | lit | point of the bit; upright | a hand on each handle, 1.02 m up, 0.21 m either side |
| `stop_paddle` | 0.45 x 1.91 x 0.04 | 78 | glow | foot of the staff; STOP faces +Z, SLOW faces -Z | right hand 1.10 m up (`paddle_hold`) |
| `squeegee` | 0.36 x 0.42 x 0.03 | 156 | lit | end of the handle; the rubber is 0.42 m up, along X | right hand 0.07 m up (`squeegee_wipe`, `squeegee_rest`) |
| `clipboard` | 0.24 x 0.02 x 0.33 | 84 | lit | ground centre, lying flat, clip at +Z | on the left palm under its near left corner (`clipboard_write`) |
| `leaflet`, `leaflet_stack` | 0.105 x 0.148 | 12 each | lit | centre, lying flat | right hand, by its near edge (`leaflet_offer`); left palm (`leaflet_idle`) |
| `coffee_cup` | 0.09 x 0.12 x 0.09 | 118 | lit | ground centre | right hand round the sleeve 0.06 m up (`cup_hold`, `sip`, `counter_reach`) |
| `phone` | 0.07 x 0.01 x 0.15 | 28 | lit | centre, lying flat, screen up | both hands, landscape (`take_photo`) |
| `cigarette` | 0.084 long | 42 | glow | the filter end; lies along +Z | right hand, between the fingers (`smoke`) |
| `bucket` | 0.31 x 0.28 x 0.31 | 246 | lit | ground centre | stands beside the window cleaner |
| `toolbox` | 0.47 x 0.24 x 0.23 | 252 | lit | ground centre; lies along X | stands beside a kneeling worker |
| `parcel` | 0.39 x 0.24 x 0.28 | 80 | lit | ground centre | no clip yet |
| `sandwich_board` | 0.60 x 0.93 x 0.46 | 100 | lit | ground centre; faces look along +Z and -Z | not placed yet |
| `coffee_cart` | 1.79 x 2.05 x 1.07 | 930 | lit | ground centre; **customers at +Z, vendor at -Z**; counter 0.975 m up, 0.80 m deep | `CoffeeCartActivity` |

### Life (`scripts\life_props.py`)

**The grip numbers are a contract with the clips of `CityAssets`:** a clip puts the middle of a palm at exactly
the point given, and the handle there is 0.025 to 0.035 m thick. `scripts\check_life.py` (a step of `build.ps1`)
cuts the built meshes at those points and fails the build if a handle is not there. All points below are in
Unity axes relative to the prop's pivot (x right, y up, z forward); the kit's (x, y, z) is Unity's (x, z, y).

Held (the clip's grip places them; do not parent by hand):

| id | size | triangles | material | pivot and facing | held |
|---|---|---|---|---|---|
| `mop` | 0.30 x 1.49 x 0.16 | 260 | lit | middle of the mop head on the ground; the shaft is Y | right hand 0.95 m up, left 1.28 m (on the black sleeve). The head lies flat when the shaft leans 30 degrees back (-Z), as the broom's does |
| `rake` | 0.46 x 1.55 x 0.05 | 624 | lit | middle of the tines' edge on the ground; the shaft is Y | leaf rake; right hand 0.95 m, left 1.30 m. The tips lie on the ground when the shaft leans 30 degrees back (-Z) |
| `paint_roller` | 0.27 x 0.45 x 0.09 | 408 | lit | end of the handle; the handle is Y | one hand 0.07 m up (handle 0.14 m long, 0.032 m thick). Roller 0.23 m along X, radius 0.045 m, centred 0.40 m up; its working side is +Z |
| `watering_can` | 0.20 x 0.36 x 0.64 | 498 | lit | ground centre of the body; spout and rose towards +Z, ending at (0, 0.30, 0.42) | by the bar over the top: it runs along Z, centred (0, 0.34, -0.02), 0.03 m thick, 0.11 m of it straight, 0.08 m clear above the body |
| `guitar` | 0.38 x 1.00 x 0.11 | 698 | lit | bottom end of the body, standing on its end, neck up; sound board faces +Z | fret hand wraps the neck (0.045 x 0.032 m) 0.78 m up; the strumming hand is over the sound hole at (0, 0.30, 0.05). Nut 0.86 m up; body 0.38 x 0.48 x 0.10 |
| `notebook` | 0.10 x 0.012 x 0.15 | 268 | lit | centre, lying flat; spiral at +Z, written page up | flat on a palm |
| `radio_handset` | 0.064 x 0.21 x 0.039 | 294 | lit | centre of its base, standing; grille and display face +Z | round the body (0.06 x 0.035 m) 0.06 m up; antenna and knob above 0.13 m |
| `newspaper` | 0.58 x 0.40 x 0.05 | 128 | lit | bottom of the centre fold; stands across X; the reader is at -Z (inner pages), front and back page face +Z | hands at (-0.25, 0.13, 0) and (0.25, 0.13, 0). The fold is 0.03 m nearer +Z than the outer edges, so the sheet is 0.026 m behind those two points |
| `map_folded` | 0.44 x 0.30 x 0.04 | 48 | lit | bottom centre; stands across X; map print faces -Z (the reader), cover panel +Z | hands at (-0.19, 0.10, 0) and (0.19, 0.10, 0) |
| `umbrella` | 1.04 x 0.92 x 1.04 | 726 | lit | bottom of the handle; the shaft is Y | straight handle 0.13 m long, 0.03 m thick, held 0.06 m up. Canopy 1.02 m across (8 ribs), rim 0.62 m up, tip 0.86 m, ferrule to 0.92 m |
| `parcel_s`, `parcel_m`, `parcel_l` | 0.22 x 0.10 x 0.16, 0.38 x 0.24 x 0.28, 0.50 x 0.32 x 0.36 | 82, 86, 86 | lit | ground centre; label on top | a hand under each end along X. `parcel_m` is the size of the old `parcel`, which stays |
| `shelf_box` | 0.43 x 0.25 x 0.45 | 558 | lit | ground centre; open carton 0.40 x 0.25 x 0.30, its flaps folded down outside (the long ones stand 0.07 m out along Z) | in both arms, hands under the ends along X; tins inside, 0.05 m below the rim |
| `hand_truck` | 0.47 x 1.20 x 0.37 | 756 | lit | the ground under the middle of the axle; toe plate reaches +Z 0.26 m; wheels (radius 0.11 m) at x +-0.21 | two rubber grips 0.12 m long, 0.032 m thick, pointing back and up at 45 degrees, centred (-0.17, 1.14, -0.04) and (0.17, 1.14, -0.04) |
| `pushchair` | 0.52 x 1.02 x 0.85 | 1 195 | lit | ground centre of the four wheels; travels towards +Z | the bar along X from -0.26 to 0.26 at y 0.95, z -0.44 (0.03 m thick); hands on the grips centred x +-0.225 |
| `stretcher_push_bar` | 0.63 x 0.38 x 2.25 (from 0.59 to 0.97 m up) | 576 | lit | **the stretcher's pivot**: child of the stretcher at zero, like `stretcher_sheet` | a U-shaped bar at each end, clamped to the litter's tube: cross bar 0.03 m thick, 0.95 m up, 1.11 m from the middle; rubber grips at (+-0.225, 0.95, +-1.11). The stretcher's own mesh is unchanged |

For jobs:

| id | size | triangles | material | pivot and facing | use |
|---|---|---|---|---|---|
| `food_bag` | 0.43 x 0.48 x 0.36 | 458 | lit | ground centre; **the straps (+Z) go against the wearer's back**, the logo faces -Z | insulated delivery backpack: the box is 0.42 x 0.44 x 0.30, the grab handle on the lid and the straps make up the rest |
| `taxi_sign` | 0.36 x 0.155 x 0.12 | 132 | glow | ground centre (it sits on a roof); "TAXI" on its +Z and -Z faces | the lamp is 0.13 m high on a 0.025 m black magnetic base. A car's sides are its X: turn the sign so its faces look fore and aft |
| `flyer` | 0.148 x 0.007 x 0.21 | 64 | lit | centre, lying flat, printed side up, its head towards +Z (as `leaflet`) | one A5 flyer |
| `flyer_stack` | 0.15 x 0.21 x 0.02 | 60 | lit | **middle of the top edge: the stack hangs below the pivot**, upright; printed face +Z | a hand's worth, five uneven layers |
| `job_board` | 1.28 x 1.85 x 0.54 | 554 | lit | ground centre; **read from +Z**; the back is a plain board | help-wanted board: the board itself from 0.75 to 1.75 m, 1.08 m wide, on two posts with weighted feet |
| `mop_bucket` | 0.39 x 0.76 x 0.54 | 704 | lit | ground centre of the tub (0.36 x 0.37 x 0.50 on four castors); the wringer and its lever (to 0.76 m) at the -Z end | goes with `mop` |
| `guitar_case` | 1.08 x 0.52 x 0.58 | 969 | lit | ground centre of the lower shell (1.08 x 0.11 x 0.42, lying along X, neck end at +X); the lid stands open along -Z, leaning 10 degrees back | a busker's tips: coins and a note in it |
| `vendor_cart` | 1.67 x 1.19 x 0.84 | 2 070 | lit | ground centre; **customers at +Z, vendor at -Z**; counter 0.975 m up, 0.80 m deep (z -0.40 to 0.40), 1.50 m wide; wheels at +X, legs and push handle at -X | hot-dog cart, same service geometry as `coffee_cart`. No awning: stand `vendor_umbrella` beside or behind it (there is a socket on the counter at (0.66, 0.975, -0.31)) |
| `vendor_umbrella` | 2.11 x 2.35 x 2.11 | 792 | lit | ground centre, at the foot of the pole | market parasol, red and white, rim 2.0 m up, the valance hangs to 1.9 m |

Worn and street furniture:

| id | size | triangles | material | pivot and facing | use |
|---|---|---|---|---|---|
| `mail_satchel` | 0.40 x 0.76 x 0.38 | 608 | lit | **the upper-chest bone: the kit point (0, 0, 1.30)** of a figure standing at the origin facing +Y (Unity (0, 1.30, 0), facing +Z) | modelled as worn: parent to `UpperChest` at local zero with the body's rotation. The bag (0.11 x 0.28 x 0.36) hangs at the left hip, centred (-0.20, -0.42, 0) from the pivot; the strap crosses chest and back over the right shoulder, 0.195 m above the pivot. It was fitted to the measurements given (shoulders +-0.17 at 1.42 m, chest 0.30 x 0.20), not to the game's mesh: expect to nudge it |
| `step_ladder` | 0.47 x 0.72 x 0.75 | 440 | lit | the ground under the middle of the top platform; **climbed from -Z** | tread tops at (0, 0.24, -0.24) and (0, 0.48, -0.12), 0.40 x 0.09; platform top 0.72 m up, 0.28 m deep. One stands on the 0.48 m tread facing +Z. As specified the platform overhangs that tread (its edge is at z -0.14, its underside 0.19 m above the tread), so the shins meet it |
| `chess_table` | 0.70 x 0.83 x 1.58 | 1 540 | lit | ground centre of the table | table top 0.74 m up, 0.70 m square (the inlaid board and the pieces stand on it); two stools 0.34 m across, seats 0.45 m up, centred z -0.62 and +0.62. White plays from -Z |
| `fruit_stand` | 1.72 x 2.22 x 1.18 | 1 528 | lit | ground centre of the four posts; **customers at +Z, vendor at -Z** | six crates on a deck sloping to the customers; the vendor's flat counter strip is 0.975 m up, z -0.45 to -0.20; the awning slopes from 2.2 m at the back to 2.05 m at its front edge (z 0.68), where the valance hangs to 1.92 m |
| `newspaper_kiosk` | 1.79 x 2.40 x 1.52 | 730 | lit | ground centre of the booth (1.40 x 1.20); **serves at +Z** | sill 0.975 m up, reaching z 0.79; the opening is 0.90 m wide; the open shutters stand 0.2 m out either side; nobody is seen inside |

<!-- module: civic -->
### Civic (`scripts\props_civic.py`, painted by `paint_civic.py`, measured by `checks_civic.py`)

The police station, the bus stop and the hotel frontage. Three of them carry a plain area that the mod letters itself: keep the numbers below and the code together (`Law\Precincts.cs`, `Transport\BusStopDressing.cs`, `Travel\Hotels.cs`).

| id | size (X x Y x Z, Unity) | triangles | material | pivot and facing | use / grips / which feature |
|---|---|---|---|---|---|
| `precinct_sign` | 1.80 x 2.90 x 0.08 | 516 | lit | ground centre; **read from +Z**; nothing behind z -0.04 | Law, precinct frontage. Two posts 0.07 square at x +-0.86, flat tops 2.90 m up; the right one (0.86, 2.90, 0) carries `blue_lamp`. Board 1.65 x 0.80 from 2.05 to 2.85 m, **its face at z +0.025**: POLICE in white centred 2.56 m up (letters about 0.28 m high), a white rule at 2.27 m, and under it a plain blue strip for the precinct's name, which the code letters at (0, 2.15, 0.027) in a box 1.50 x 0.17. A white border line runs 0.009 to 0.018 m inside the board's edge (2.059 to 2.068 m up at the bottom): it is under the lowest 3 mm of that box, where no letter reaches. Back plain blue with two steel rails |
| `blue_lamp` | 0.20 x 0.27 x 0.20 | 284 | glow | centre of its base (it sits on a post top); round | Law. Blue glass 0.17 across from 0.02 to 0.24 m, four glazing bars, a dark cap 0.20 across, a collar 0.068 across at the foot (the post is 0.07) |
| `precinct_desk` | 2.40 x 1.10 x 0.80 | 740 | lit | ground centre; **the public stand at +Z**, the officer at -Z | front counter: public shelf 1.10 m up (z 0.10 to 0.40), blue fascia with POLICE between two shields on the +Z face; the officer's writing surface 0.78 m up (z -0.40 to 0.16) with a monitor (its back to the public, top 1.095 m), keyboard, papers, phone, pen pot; a drawer pedestal at +X; the knee space is open from x -1.15 to 0.63. Not asked for by code yet |
| `precinct_bench` | 1.80 x 0.85 x 0.55 | 742 | lit | ground centre; **the sitter faces +Z** | waiting-room bench: five wooden slats, **seat top 0.46 m up** from z -0.21 to 0.27; back slats to 0.84 m leaning 5 degrees; steel frame on three bolted feet, an arm at each end 0.655 m up, a chrome cuff rail along the back 0.545 m up. Not asked for by code yet |
| `holding_cell_bars` | 3.00 x 2.40 x 0.10 | 1 098 | lit | ground centre; runs along X; the same from both sides | round bars 0.022 thick, 0.112 apart, in a frame 0.06 square with three flat rails (0.45, 1.20, 1.95 m); **the door is at the +X end**: clear opening x 0.53 to 1.44 (0.91 wide) under a transom 2.08 m up, shown shut, hinged at x 1.44 (three hinges on the +Z side), lock box 0.15 x 0.24 x 0.09 at x 0.54 to 0.69, 1.05 m up, a keyhole on both faces. No collider: give it a box 3.0 x 2.4 x 0.06. Not asked for by code yet |
| `notice_board` | 1.20 x 1.60 x 0.06 | 232 | lit | **bottom centre of its back** (it hangs on a wall: y 0 is its lower edge); faces +Z | aluminium frame, cork 1.09 x 1.49 under a blue POLICE NOTICES header: a WANTED poster with a face, a district map, community notices; one sheet curls off the board. Hang it with its lower edge about 0.9 m up. Not asked for by code yet |
| `flag_pole` | 1.71 x 6.00 x 0.34 | 558 | lit | ground centre of the pole (the base plate is 0.34 square); **the flag flies to +X** | tapered white pole 0.11 to 0.06 thick, gold ball, halyard and cleat (1.30 m up on the +X side); a city flag 1.46 x 0.90 from 4.87 to 5.78 m up, blue with a white band and a seal (no country's), waved in the mesh: it does not move. Not asked for by code yet |
| `police_shield_emblem` | 0.62 x 0.80 x 0.05 | 246 | lit | **centre of its back** (on the wall); faces +Z | a gold police shield 0.04 thick with a bevelled rim, blue enamel, a raised seal and a raised banner reading POLICE (letters 0.07 m: a plaque to be read at a door, not across a street). Not asked for by code yet |
| `station_awning` | 2.60 x 0.50 x 1.40 | 450 | lit | **the middle of its wall edge, at its underside** (as `hotel_awning`); reaches out along +Z | police-blue canvas sloping from 0.50 at the wall to 0.30 at the front, closed ends, white piping; POLICE in white on the front valance (letters 0.18 m, centred 0.16 m above the pivot); an aluminium frame under it that is meant to be seen. Hang it 2.3 m or more above the pavement (its underside is the pivot). Not asked for by code yet |
| `bollard` | 0.17 x 0.90 x 0.17 | 324 | lit | ground centre; round | cast-iron black, flared foot, neck rings, ball top, a white reflective band 0.44 to 0.52 m up. Not asked for by code yet |
| `bus_stop_sign` | 0.61 x 2.85 x 0.12 | 334 | glow | **ground centre of the pole**; the flag hangs to +X, its faces look along +Z and -Z | Transport, `BusStopDressing`. Pole 0.07 thick on a foot 0.12 across (the 0.61 and 0.12 are that foot; the stand-in has one of 0.16). Flag 0.50 x 0.62 x 0.03 centred (0.30, 2.42, 0), **faces at z +-0.015** (the code letters at +-0.019): blue, plain from 2.33 m up, where the route numbers go (white, at (0.30, 2.50), box 0.46 x 0.40); below that a white band with a bus and BUS STOP. A timetable case 0.26 x 0.36 centred (0.20, 1.45, 0), printed both sides |
| `bus_shelter` | 1.70 x 2.50 x 3.76 | 1 342 | lit | ground centre of its floor (1.4 deep along X, 3.6 long along Z); **open to -X (the road), back at +X**, the end panel at +Z | Transport. Six posts 0.07 square (x +-0.66; z +-1.76, and +-0.59 on the back), three bays of glass over a kick panel on the back, a screen 0.48 wide at the back corner of the -Z end, a framed panel at the +Z end with the route map and a poster on both faces (1.04 x 1.70, centred 1.32 m up). **Bench seat 0.46 m up**, 0.40 deep (x 0.20 to 0.60), from z -1.30 to +0.60, with a rail to lean on 0.80 m up. Barrel roof 1.70 x 3.76 reaching x -0.93 over the kerb (crown 2.50 m, underside of its fascia 2.27 m), a small BUS STOP plate on the fascia. The glass is opaque. The code adds no lettering to it |
| `hotel_sign` | 0.70 x 1.90 x 0.16 | 490 | lit | ground centre; face to +Z, its back to a wall | Travel, `Hotels.BuildSign`. Brass tube frame with an arched top and a ball finial on a weighted black base; a black board with HOTEL and three stars (centred 1.13 m up). **The name plate is 0.60 x 0.34 centred 1.50 m up, black on a brass backing, its face at z +0.080**: the code letters the name in cream at z 0.085. Nothing is in front of z 0.080 |
| `hotel_awning` | 2.40 x 0.45 x 1.40 | 520 | lit | **the middle of its wall edge, at its underside**; reaches out along +Z | Travel, `Hotels.BuildAwning` (hung 2.75 m up). Cream canvas with tone-on-tone stripes (one colour for all four tiers) curving down from 0.45 at the wall to a scalloped valance that hangs from 0.15 to 0.0 at the front and along both ends; a brass frame under it |
| `luggage_cart` | 1.10 x 1.75 x 0.60 | 1 804 | lit | ground centre; long side along X | Travel, `Hotels.BuildCart`. Brass birdcage trolley: carpeted deck 0.22 m up on four castors, a hoop at each end (x +-0.50) leaning in to a hanging rail 1.655 m up, a ball on each; on it a navy case, a tan leather case, an olive holdall, a hat box and one hanger |
| `suitcase` | 0.45 x 0.65 x 0.25 | 608 | lit | ground centre; standing, handle up | hard-shell case (light blue, ribbed on both broad faces, four wheels inside its height). **Carried by the bar over its top: along X, centred (0, 0.630, 0), 0.028 thick, 0.12 of it straight, 0.030 clear above the shell** (whose top is 0.585). For `carry_bag_walk` (right hand); not used by code yet |
| `street_bin` | 0.54 x 0.90 x 0.54 | 546 | lit | ground centre; round | Events, `FireScene` (`EventProps.Bin`): the flames are put 0.85 m up in the middle, so the top is open (rim 0.90 m, inside 0.45 across, litter in it 0.64 to 0.69 m up). Dark green, a gold band at 0.59 to 0.67 m and a thin one under the black rim |

Atlas: 18 regions (4 swatches `civic_iron`, `civic_grey`, `civic_lamp_blue`, `civic_leather`; 14 decals), 942 424 pixels with their gutters. Module total 10 834 triangles.
<!-- end: civic -->

<!-- module: service -->
### Service vehicles (`scripts\props_service.py`, painted by `paint_service.py`, measured by `checks_service.py`)

**The city bus is the mod's own vehicle** (`Transport/BusVehicle`), on no vehicle of the game's: its body and its parts
are props of their own that the code puts together, spinning and steering the wheels, sliding the door leaves and
switching a mesh of lit lamps for each group of them; without the bundle it builds plain shapes of the same sizes in
the same places. **The fire engine is a body that stands over one of the game's own traffic vehicles** while **all**
of that vehicle's renderers are hidden (`EngineLivery.Dress`), so it carries its own wheels. Both are lofted hulls
(cross-sections along the length, windows let in 2.4 cm with a chamfered surround), not boxes. **A body over a game's
vehicle is made to that vehicle: the same length, and its axles where that vehicle's are**, so that the hidden
vehicle's colliders and traffic footprint are the body's own and the wheels turn about the points the vehicle turns
about (a body with other axles slides sideways through every bend). Measured on the game's prefabs (wheel meshes, in
the prefab's own space):

| vehicle | mesh (X x Y x Z) | origin | axles (z) | tyre radius | used for |
|---|---|---|---|---|---|
| Mersaidi Dash | 2.33 x 2.60 x 6.66 | under the **middle** of the body (z -3.32 to +3.34) | -1.62 and +2.40 (wheelbase 4.02), track 1.69 | 0.38 | nothing now: the bus was a shuttle body over it until the mod had a bus of its own (`city_bus`) |
| Delivery Truck | 2.72 x 3.01 x 7.43 | under the **rear axle** (z -2.353 to +5.077) | 0 and +4.118, twins at the back | 0.42 | `fire_engine` |
| Freight Truck T1 | 2.89 x 4.00 x 10.98 | under the **rear axle** (z -2.98 to +7.99) | 0 and +6.876, twins at the back | 0.54 | nothing: an 11 m pumper is not believable; a repainted one is the stand-in |
| UMC Desert | 2.36 x 2.03 x 5.53 | under the rear axle (z -1.12 to +4.41) | 0 and +3.47 | 0.40 | nothing now (it carried the shuttle body where no Dash was to be had) |

The game's vehicles do not agree on where their origin is, so **the code never places a body by the vehicle's origin**:
it reads the vehicle's axles from its wheel meshes (`TrafficTools.Axles`) and puts the middle between the body's axles on
the middle between the vehicle's. The engine was first built 8.20 m long with a 4.70 m wheelbase for the Freight Truck
and stood at that truck's origin, which is its rear axle: 2.5 m behind the truck's middle. It is now the Delivery
Truck's size; `EventRoad.PickTruck` takes a Delivery Truck when the body is in the bundle and `EngineLivery` gives the
body to no other vehicle (any other is repainted, as without the bundle).

| id | size (X x Y x Z, Unity) | triangles | material | pivot and facing | use / grips / which feature |
|---|---|---|---|---|---|
| `city_bus` | 2.82 x 2.94 x 10.90 (2.40 wide under the mirrors; from 0.11 to 3.05 m up) | 3 746 | lit | ground centre under the middle of the bus; nose to +Z; doors on +X | `Transport/BusVehicle` (`Shapes.Prop("city_bus")` under its shell, which leans and dips; its shadow is on). The mod's own low-floor midi bus, red (`BusVehicle.Livery`) with a grey skirt. **Axles at z +2.90 and -2.90** (`BusBay.BusAxleFront`, `BusWheelbase`) under arches 0.58 in radius with a rubber lip, and a box between the wheels so that nothing is seen through them: a front wheel steered 38 degrees stays inside its arch, 0.09 clear of the box. **Two doors on +X**, open in the side from the floor at 0.33 (`Floor`) to 2.63, centred z 4.45 and -0.55 (`FrontDoorAhead`, `MiddleDoorAhead`), 1 cm inside the leaves all round; a dark inside behind them (a floor with a yellow edge, yellow poles and a rail, a validator by the driver, two seats across from the middle door). Nothing stands out of the side where the leaves stand shut (x 1.21 to 1.25) or slide open (out to 1.32) but the mirrors, which hang outside them (1.34 to 1.41). **Flat black panels under the code's lettering**: the sign box over the windscreen, 1.72 x 0.30 with its face at z 5.45 (lettered at 5.465); over the first window behind the front door, 1.20 x 0.30 flush with the side at x 1.20 (lettered at 1.212); a box 0.54 x 0.30 at the back, its face at z -5.45. 10.90 long over those boxes (the bumpers stop 5 mm short of them), 3.05 high over the air-conditioning pod at the back of the roof (`Height`). A windscreen round the corners, a continuous window band, mirrors on arms from the roof's corners, the engine's grille across the back and up its left corner, the lamp units where the lit lamps go (below), CITY TRANSIT and fleet number 2417, a wheelchair sign at the middle door |
| `city_bus_wheel` | 0.30 x 0.96 x 0.96 | 346 | lit | the middle of the wheel; **its axle along X, the hub to +X** | `BusVehicle.Wheels`: at (+-1.02, 0.48, 2.90), the left one turned 180 degrees about Y; spun about X by the distance rolled and steered about Y (up to 38 degrees). Tyre 0.48 in radius and 0.30 wide; a steel wheel with ten holes and ten nuts round a hub cap, the nuts modelled over the painted ones so that it is seen to turn |
| `city_bus_wheel_twin` | 0.56 x 0.96 x 0.96 | 442 | lit | as `city_bus_wheel` | `BusVehicle.Wheels`: the rear pairs, one mesh each, at (+-0.92, 0.48, -2.90). Two tyres with a groove between them, 0.56 over both, the hub set deep in the outer wheel; its outer face is the bus's side (x 1.20). Where the bundle lacks it the code draws `city_bus_wheel` out to the pair's width |
| `city_bus_door` | 0.04 x 2.30 x 0.60 | 47 | lit | the middle of its lower edge; its outside to +X | `BusVehicle.Doors`: two in each door at (1.23, 0.33, the door's middle +-0.30), slid out 0.07 and along 0.56 away from the door's middle as it opens. Glass in a thin red frame with a rail across 1 m up and rubber on the edges, the same both ways round. Wholly open, the front door's forward leaf reaches z 5.61, 0.16 m past the nose |
| `city_bus_light_head` | 1.43 x 0.12 x 0 | 32 | glow | **the bus's own**: the ground under its middle, as `city_bus` | `BusVehicle.Lights`, all five: each is the lens of its lamps in `city_bus` again, 1.5 mm in front of it, in the glow material, and the code shows or hides the whole group (`checks_service.py` holds every face to its lens). These: the two round lamps of each headlamp unit, the units' middles at (+-0.66, 0.74), their faces at z 5.444; from dusk |
| `city_bus_light_tail` | 1.91 x 0.15 x 0 | 4 | glow | as `city_bus_light_head` | the third lens down in each back corner's unit, (+-0.90, 1.00), at z -5.444; from dusk |
| `city_bus_light_brake` | 1.91 x 1.74 x 0.01 | 6 | glow | as `city_bus_light_head` | the second lens down in each back unit, (+-0.90, 1.16), and the high lamp over the number, (0, 2.81): clear of the back lettering (2.52 to 2.72), where the plain shapes had it; while it slows and stands |
| `city_bus_light_left`, `_right` | 0.47 x 0.71 x 10.89 | 6 each | glow | as `city_bus_light_head` | the indicators of a side: the outer end of the headlamp unit (+-0.77, 0.74), the top lens of the back unit (+-0.90, 1.31), the repeater on the side (+-1.20, 0.80, 3.70) between the front wheel and the front door |
| `fire_engine` | 2.72 x 3.20 x 7.43 (body 2.50 wide; mirrors make up the rest) | 6 224 | lit | ground centre under the middle of the body; nose to +Z | Pumper for `Events/EventRoad.EngineLivery` (a child of the truck, scale undone, its axles on the truck's, the truck wholly hidden; its shadow is switched on, the truck's own went with its meshes). **Own wheels: axles at z +2.756 and -1.362 (wheelbase 4.118, the Delivery Truck's)**, tyres 0.51 in radius, twins at the back (`EngineLivery.BodyAxleFront`, `BodyAxleRear`). **Its own light bar** on the cab roof, the middle of its underside at (0, 3.006, 2.535), 1.50 long, top 3.20: the code adds `fire_light_bar` only to a truck without the modelled body, and shows `fire_bar_lens` over each end of this one in turn while the lights flash. **Pump panels on both sides** (the crew get out on whichever side the fire is): 1.30 x 1.48, centred z 0.756, from 0.66 to 2.14 up, at x +-1.22 (`PanelOut`, `PanelAhead`); `FireScene.PlanCrew` stands the pump operator at (+-1.72, 0, 0.756) facing the body, where `pump_panel`'s hands are on it. **Outlets** low on both sides at z -0.333, 0.76 up, reaching x +-1.34 (`OutletAhead`; the hose is laid from the ground 0.15 m outside the truck's colliders there). Short forward-control cab from z 1.406 to 3.535: the crew doors centred z 1.78 (`CrewDoorAhead`), the front doors over the wheels, z 2.136 to 3.455 (`FrontDoorAhead` 2.756; "FIRE DEPT / ENGINE CO. 7" on them); lockers behind roller shutters, the reflective band from 1.12 to 1.28 all round, "FIRE & RESCUE" 0.25 m high on both sides, chevrons on the back; on top a hose bed, two crosslays, a deck gun, a ladder (10 rungs) on the right and two suction hoses on the left. Total height 3.20 is inside what `TrafficEffects.RoofHeight` accepts (under 3.5), so the flasher's lights sit at 3.5, over the bar |
| `fire_bar_lens` | 0.60 x 0.13 x 0.30 (x 0.15 to 0.753) | 44 | glow | **the middle of the underside of `fire_engine`'s light bar** (that prop's (0, 3.006, 2.535)); the lens is at +X | The +X red lens module of the engine's light bar again, 3 mm proud of it all round, in the glow material: the engine is one mesh in the lit material, so nothing of it can light up by itself. `EngineLivery` puts one at the bar's pivot and a second turned 180 degrees about Y (the bar is the same front and back), both switched off; `BarFlash` (on the object of the two flashing lights, so only while an engine is out) shows the left one with the red light and the right one with the white, touching the two renderers only when the state flips |
| `fire_light_bar` | 1.30 x 0.17 x 0.28 | 316 | glow | centre of its underside; lies along X; the same from front and back | For a truck that has no modelled body (`EventPropsBar.Build`: on the roof over the middle of the truck's colliders). Two rubber feet, a black base rail, red lens modules either end and a clear one in the middle with ribbed faces. The code neither tints nor flashes it: the flashing is two point lights of `TrafficEffects.CreateFlasher` 0.3 m above the roof |
<!-- end: service -->

<!-- module: events -->
### Fires and street events (`scripts\props_events.py`, `paint_events.py`, `checks_events.py`)

Asked for by id in `Scripts\Events\EventProps.cs`, which builds a plain stand-in until the bundle has each. All points are in Unity axes from the prop's pivot. `EVENTS_FIT=1` adds two previews that are not for the bundle (`events_fit_crew`, `events_fit_runner`: the worn things on a dummy with the game skeleton's proportions).

| id | size (X x Y x Z, Unity) | triangles | material | pivot and facing | use / grips / which feature |
|---|---|---|---|---|---|
| `hose_nozzle` | 0.07 x 0.09 x 0.47 | 554 | lit | the back of the barrel (the coupling ends 0.05 m behind it); points along +Z; the bale handle is on top (+Y) | fire crew (`FireScene`): gripped round the black sleeve at (0, 0, 0.06) and round the barrel at (0, 0, 0.30), 0.031 m thick at both; the jet leaves at z 0.42; the hose joins at z -0.05. `hose_hold` holds it there (right palm at 0.06, left under the barrel at 0.30) |
| `fire_helmet` | 0.30 x 0.18 x 0.40 | 552 | lit | centre of the rim's lower edge (the tip of the neck brim is the lowest point; the band's own lower edge is 0.01 m up); peak at +Z | `Head` bone, 0.085 m up and 0.012 m forward (`people.Wear(who, Helmet, Head, 0.085f, 0.012f)`). Inside of the band 0.082 m half width, 0.103 m half depth, centred on the pivot; brim reaches z +0.165 and -0.235. Hair is not hidden by it |
| `fire_air_pack` | 0.34 x 0.76 x 0.44 | 1 014 | lit | **the upper-chest bone** of a wearer facing +Z, as `mail_satchel` | `UpperChest` at local zero with the body's rotation (`Wear(who, AirPack, UpperChest, 0, 0)`). Fitted to the game skeleton's numbers, not to its mesh: straps cross the shoulders 0.24 m above the pivot, 0.105 m either side; chest straps 0.13 m ahead; back plate 0.116 to 0.138 m behind, the cylinder's back 0.27 m behind; waist belt 0.19 m below (the `duty_belt` ellipse, 0.156 x 0.120 inside, centred 0.03 m ahead); lowest point 0.505 m below. Expect to nudge it |
| `placard` | 0.56 x 2.19 x 0.03 (the mesh starts 0.70 m up) | 36 | lit | the stop paddle's: the fist is 1.10 m above the pivot; "FAIR RENT NOW" faces +Z, "HOMES FOR ALL" faces -Z | protesters and marchers (`EventPeople.CarryStaff`, at `placard_hold`'s grip). A lath 0.028 x 0.020 m from 0.70 to 2.15 m, a card 0.56 x 0.42 m stapled either side of it from 1.77 to 2.19 m |
| `banner_pole` | 0.72 x 2.20 x 0.03 (starts 0.70 m up) | 192 | lit | as `placard`; the cloth is to +X of the pole | the first two marchers of a parade. Pole 0.03 m thick with a brass finial; cloth 0.69 x 0.48 m from 1.68 to 2.16 m, gently waved, the same emblem on both sides (mirrored on the +Z side) |
| `runner_bib` | 0.22 x 0.17 x 0.02 | 12 | lit | **the upper-chest bone**; the number's middle is 0.134 m ahead of it and 0.07 m below; faces +Z | fun run (`Wear(who, Bib, UpperChest, 0, 0)`). Number 247, bowed to a 0.30 m radius (its side edges are 0.02 m behind its middle); one-sided |
| `film_camera` | 0.85 x 1.70 x 1.07 (z -0.59 to +0.48) | 1 474 | lit | ground centre of the tripod; the lens looks along +Z, 1.47 m up | film shoot. **Made for an operator standing 0.75 m behind the pivot on the lens's line** (where `FilmSet` puts them): eyecup at (0.032, 1.50, -0.59), pan bar's rubber grip at (0.227, 1.124, -0.391) pointing back, down and right, the focus whip's handle at (-0.135, 1.41, -0.295). Tripod feet 0.45 m out, one forward. `camera_operate` fits it |
| `light_stand` | 0.86 x 2.50 x 0.74 (z -0.49 to +0.25) | 1 006 | glow | ground centre of the stand; the lamp, 2.32 m up, shines along +Z | film shoot. A fresnel with barn doors on a three-section stand, cable down the mast, a sandbag on the rear leg. The whole prop is the glow material (one material a prop): the lens is the only bright part, the rest is black and steel and does not show. It casts no light: pair it with a light if the set should be lit |
| `directors_chair` | 0.55 x 1.10 x 0.45 | 456 | lit | ground centre; faces +Z | film shoot (`EventProps.Clapper`). The tall kind, as the stand-in was: seat rails 0.62 m up (the canvas hangs to 0.58), arms 0.86, back sling lettered DIRECTOR on both faces to 1.10; a script lies on the seat, so it is not for sitting on |
| `firework_rack` | 1.20 x 0.54 x 0.50 | 822 | lit | ground centre; lies along X | fireworks. Twelve mortar tubes (0.096 m across, mouths 0.53 m up) in two rows in a timber frame, capped, fused along the rows, a DANGER label on both long sides |
| `hose_coil` | 0.52 x 0.095 x 0.52 | 696 | lit | ground centre; lying flat | Fires (`EventProps.HoseCoil`): a rolled length of yellow delivery hose with its brass coupling at the outer end. Made by the lead to the stand-in's size; plain, reads as a rolled hose at street distance |
<!-- end: events -->

<!-- module: everyday -->
### Everyday (`scripts\props_everyday.py`, painted by `paint_everyday.py`, measured by `checks_everyday.py`)

All points are Unity axes relative to the pivot (x right, y up, z forward). Glass is opaque: a drink is the colour of the glass's lower part plus a flat surface inside. 35 props, 11 632 triangles, 30 atlas regions (496 000 px with gutters).

Home:

| id | size (X x Y x Z, Unity) | triangles | material | pivot and facing | use / grips / which feature |
|---|---|---|---|---|---|
| `pan` | 0.44 x 0.059 x 0.26 (pan 0.26 across, 0.05 deep) | 282 | lit | centre of the base; handle along +X | `cook_stir`. The handle is a straight round bar 0.028 m thick from x 0.14 to 0.31 at y 0.045: grip centre (0.23, 0.045, 0), axis X (so the bar's top is 0.059 up). Red enamel, non-stick inside, an egg and bacon painted on the cooking surface |
| `plate` | 0.26 x 0.024 x 0.26 | 142 | lit | centre of the base | `eat_sit`. Dinner plate on a foot; steak, chips and peas painted on it (nothing modelled above the rim) |
| `mug` | 0.114 x 0.10 x 0.08 (body 0.08 across) | 216 | lit | centre of the base; C handle at +X, reaching x 0.074 | `drink_stand`. Held round the body 0.05 up: a plain cylinder 0.08 across from y 0.004 to 0.097. Coffee surface 0.012 below the rim |
| `run_checkpoint` | 1.60 x 1.40 x 1.60 | 596 | glow | ground centre; the flags' faces look along +Z and -Z | `Home\JogRun` (`Shapes.Prop("run_checkpoint")`, placed unrotated at the checkpoint). A blue ring 1.6 across (0.14 wide, 0.02 high, white dashes) and a feather flag 1.4 high at x -0.73 and +0.73, "CHECKPOINT" up each; 0.92 m clear between the flags |

Social (held: the clip's grip places them):

| id | size (X x Y x Z, Unity) | triangles | material | pivot and facing | use / grips / which feature |
|---|---|---|---|---|---|
| `wine_glass` | 0.07 x 0.20 x 0.07 | 236 | lit | the middle of the stem (foot at y -0.085, rim at +0.115) | `cheers`. Held by the stem at the origin: a round bar 0.009 m thick, straight from y -0.066 to +0.028. Red wine to y 0.078 |
| `beer_bottle` | 0.066 x 0.24 x 0.066 | 208 | lit | 0.08 m up the axis (base at y -0.08, cap at +0.16); label faces +Z | Brown glass, crown cap. The hand closes round the body at the origin: a plain cylinder 0.066 across from y -0.075 to +0.055. **Built to the Social list (0.07 x 0.24, pivot 0.08 up); no code places it yet. Leisure's list says 0.065 x 0.23 without a pivot** |
| `takeaway_box` | 0.14 x 0.09 x 0.11 | 122 | lit | the middle of the underside; printed sides face +Z and -Z | `eat_standing` (left palm). Open noodle carton, flaps up and out, noodles to y 0.076, a fork in it towards +X |
| `popcorn_tub` | 0.16 x 0.20 x 0.16 | 230 | lit | the middle of the underside; "POPCORN" on +Z and -Z | Red and white striped tub 0.11 across at the foot, 0.16 at the rim (0.165 up), heaped to 0.20 |
| `gift_box` | 0.20 x 0.14 x 0.20 | 238 | lit | ground centre | Red box with a lid (0.10 high), gold ribbon both ways, bow to 0.14 |
| `flowers_bunch` | 0.22 x 0.42 x 0.22 | 496 | lit | the wrapped stems, 0.10 m above their cut ends (blooms to y +0.32); the paper stands higher at -Z | The hand closes round the paper at the origin: 0.039 m thick there (0.035 at y -0.03, 0.042 at +0.03), widening above y 0.04. Ten blooms in red, yellow and pink |
| `gym_bag` | 0.50 x 0.26 x 0.24 | 528 | lit | the middle of the handles; the bag lies along X | The handles meet in a wrap: a round bar along X at the origin, 0.03 m thick, 0.11 m long. The bag's top is 0.061 below the pivot, its underside 0.245 below. Teal barrel, white hoops, end pockets, zip |
| `moving_box` | 0.45 x 0.35 x 0.35 | 94 | lit | ground centre; "FRAGILE" on +Z and -Z, a hand hole in each X end | Carried as `parcel_m`: a hand under each end along X (`OutingScene.BoxProp` is `parcel_m` today) |
| `newspaper_ledger` | 0.58 x 0.40 x 0.05 | 128 | lit | as `newspaper`: bottom of the centre fold, read from -Z, front and back page face +Z | Vertex for vertex the `newspaper` prop (hands at (-0.25, 0.13, 0) and (0.25, 0.13, 0)) with the front page "THE CITY LEDGER" (the phone contact's name is "City Ledger") |

Leisure:

| id | size (X x Y x Z, Unity) | triangles | material | pivot and facing | use / grips / which feature |
|---|---|---|---|---|---|
| `fishing_rod` | 0.098 x 0.102 x 2.10 | 828 | lit | the butt of the handle; **the rod runs along +Z**, reel under it (-Y), crank on the left (-X) | `Fishing` (`LeisureProps`; the code takes the tip as 2.1 m along +Z). Cork handle 0.028 m thick on the Z axis from 0 to 0.56 (butt cap 0 to 0.02, reel seat 0.34 to 0.46: 0.029 thick, 0.032 at its two hoods). Hands at z 0.10, 0.40, 0.52. Spinning reel below at z 0.37 to 0.47, lowest point y -0.086; crank knob centred (-0.058, -0.056, 0.402). Blank 0.012 to 0.003 at the tip, five guides and a tip ring underneath. **0.098 wide, not 0.07: the knob at x -0.06 and the bail make it so** |
| `fishing_float` | 0.045 x 0.11 x 0.045 | 113 | lit | the waterline | Red above to 0.043, orange antenna to 0.07, white below to -0.04 |
| `fishing_line` | 0.004 x 0.004 x 1.0 | 8 | lit | one end; runs along +Z | Scale Z to the span (`Fishing.Frame` does). A four-sided pale thread, 4 mm across like the stand-in |
| `drink_glass` | 0.075 x 0.12 x 0.075 | 200 | lit | ground centre | `NightOut` (`Body.Play("sip", "drink_glass")`): held by the `coffee_cup` grip 0.06 up, where it is 0.069 across. Tumbler, amber drink to 0.086, three ice cubes |
| `cocktail_glass` | 0.11 x 0.16 x 0.11 | 225 | lit | ground centre | Martini glass: stem 0.008 thick from y 0.02 to 0.08, pink drink, an olive on a stick. Not used by code yet |
| `clothes_rack` | 1.44 x 1.76 x 0.56 | 1 811 | lit | ground centre; rail along X 1.55 m up; **browsed from +Z**; "SALE" sign on top reads from +Z and -Z | `ClothesShop` (`LeisureProps`; its NavMeshObstacle 1.5 x 1.6 x 0.6 still fits). Rail 0.035 thick; seven garments at x -0.54 .. +0.54 every 0.18, hung across the rail (0.42 to 0.52 wide in Z, hems 0.49 to 0.86 up), each on a wire hook 6 mm thick: 0.17 m of clear rail between hooks for the hand of `browse_rack` |
| `shopping_bag` | 0.32 x 0.38 x 0.12 | 202 | lit | **top centre of the handles: the bag hangs below**; shop panel on +Z and -Z | Paper carrier 0.29 high, twisted-paper handles 0.09 above it, pink tissue in the top. Not used by code yet |

Transport:

| id | size (X x Y x Z, Unity) | triangles | material | pivot and facing | use / grips / which feature |
|---|---|---|---|---|---|
| `turnstile` | cabinet 0.30 x 1.00 x 1.20; **0.75 wide with the arms** | 592 | lit | ground centre of the cabinet; walked past along Z on its +X side, entered from +Z | Stainless cabinet, top 0.975 up; card reader on the top at (0, 0.975, 0.455), a go / no-entry display behind it to 1.00 and on both ends; tripod hub on the +X side 0.80 up, one arm level across the way out to x 0.60, 0.77 up. Not placed by code yet |
| `fuel_pump` | 0.50 x 1.60 x 0.40 | 768 | lit | ground centre; price and litres on +Z and -Z | Display from 0.88 to 1.36 up, a nozzle in its boot and a hose looping down on each side (x +-0.25), brand board on top, kick plate. Not placed by code yet |
| `jerry_can` | 0.35 x 0.45 x 0.18 | 412 | lit | ground centre; spout cap at the +X top corner (to 0.45) | `refuel_hold`. Carried by the bar over the top: along X, centred (0, 0.415, 0), 0.03 m thick, 0.11 m of it straight, 0.03 m clear above the can's flat top (0.37 up). Red, pressed X on both sides |
| `warning_triangle` | 0.45 x 0.42 x 0.05 | 152 | glow | ground centre; faces +Z and -Z alike | Hollow red reflective triangle, yellow inner edge, on a weighted foot 0.34 x 0.05 |
| `car_wash_arch` | 3.60 x 3.20 x 1.00 | 1 563 | lit | ground centre; driven through along Z, entry face (traffic lights, spray arch) at +Z | 2.60 m clear between the side brushes, 2.42 m under the top brush. "CAR WASH" on top reads from +Z and -Z (letters 0.2 m). Not placed by code yet |

Careers and business (none placed by code yet: each needs a line of placement):

| id | size (X x Y x Z, Unity) | triangles | material | pivot and facing | use / grips / which feature |
|---|---|---|---|---|---|
| `dispatch_desk` | 1.20 x 1.40 x 0.50 | 554 | lit | ground centre; **the dispatcher stands at +Z facing -Z**; the map faces +Z | `DispatchJob`, clip `desk_plan`. Sloping top 1.03 up at the back, 0.97 at its front edge (z +0.25); the city map on the board behind (z -0.22) from 0.64 to 1.37, pins and a route on it; radio handset, base set with a mike, papers, a mug of pens |
| `employer_sign` | 0.60 x 0.30 x 0.03 | 58 | lit | the middle of its back, on the wall; faces +Z | Blank blue enamel plate in a brass frame with four screws, for lettering by code (plate 0.58 x 0.28, 0.026 proud) |
| `employer_sign_1` .. `_6` | 0.60 x 0.30 x 0.03 | 58 each | lit | as `employer_sign` | In the order of `CareerCatalogue.All`: 1 Brightline Promotions, 2 Brightside Contract Cleaning, 3 Uncle Lou's Street Eats, 4 Fairway Markets, 5 Hudson Parcel Co., 6 Five Borough Cabs. Capitals 0.07 to 0.09 m high |
| `window_boarded` | 1.20 x 1.50 x 0.05 | 96 | lit | ground centre of its back face; faces +Z | Plywood sheet 0.018 thick, two battens screwed across its front, a sprayed tag and a "SORRY CLOSED" note. The back is plain |
| `closed_notice` | 0.34 x 1.30 x 0.32 | 186 | lit | ground centre; faces +Z, leaning 8 degrees back | A3 notice (0.30 x 0.42, "CLOSED BY ORDER", crest, seal) in a frame on a weighted stand (base 0.34 across) |
<!-- end: everyday -->

<!-- module: family -->
### Family and driving school (`scripts\props_family.py`, `paint_family.py`, `checks_family.py`)

**The code is the specification** for the first seven: each has the pivot, facing and size of the plain-shape stand-in it replaces (`Scripts\Family\FamilyCast.cs` `FamilyProps.StandIn`, looked up at line 452; `Scripts\Licences\LicenceProps.cs` `Make`, looked up at line 49), and `checks_family.py` measures them on the built meshes. The other seven are **not asked for by code yet**: their numbers are their own contract. All points are in Unity axes from the prop's pivot (x right, y up, z forward). 14 ids, 7 254 triangles, 163 840 atlas pixels (13 regions: 9 decals, 4 swatches).

| id | size (X x Y x Z, Unity) | triangles | material | pivot and facing | use / grips / which feature |
|---|---|---|---|---|---|
| `wedding_arch` | 2.10 x 2.50 x 0.30 | 1 930 | lit | ground centre; **dressed on its +Z face** (`Wedding.Place` turns +Z to the couple and the guests; the officiant stands 0.75 m in front of it) | Family, a wedding of tier 1 or more: white poles 0.054 m thick in two arches 0.17 m apart, **posts at x +-0.95** on foot plates, a diamond trellis in each side and rungs over the top; a garland with fifteen white and pink roses over the +Z crown, a white swag under it, a drop tied back with roses down each +Z post; a sprig with three roses on the -Z crown. 1.61 m clear between the drops, 2.03 m under the swag. Nobody walks through it |
| `flower_stand` | 0.40 x 1.20 x 0.40 | 870 | lit | ground centre; the same all round | Family, a wedding with flowers (four of them): an ivory column on a square plinth, **its top 0.26 m square at y 0.95**, a brass bowl, a dome of thirteen white and pink roses over a collar of twelve leaves to y 1.20 |
| `aisle_runner` | 1.10 x 0.018 x 1.00 | 80 | lit | ground centre; **its length is Z, laid end to end every 1.00 m** | Family, a wedding of tier 2: one metre of ivory cloth, hemmed along both sides (hems to y 0.018) with a gold line inside each hem and a few petals; open at both ends (z +-0.50, the same section at each), top only (nothing underneath). Lies from the ground up: on uneven paving a strip can sink, as the stand-in does |
| `cot` | 0.72 x 0.90 x 1.26 | 898 | lit | ground centre; **its length is Z**; pillow at the +Z end | Family, a child under three at home: beech cot on four castors, corner posts at (+-0.335, +-0.605) to y 0.90, **side rails along Z whose tops are y 0.885, centred x +-0.33** (0.04 wide, 0.05 deep), thirteen bars a side, solid end boards with a curved top, pale-blue mattress 0.60 x 1.12 with its top at **y 0.515**, a blue blanket over the -Z half, a white pillow at z 0.29 to 0.51. `rock_cot` stands at (+-0.68, 0, 0) facing the cot. `baby_bundle` at (0, 0.53, 0.0) lies on the blanket with its head towards the pillow |
| `high_chair` | 0.48 x 1.05 x 0.50 | 544 | lit | ground centre; **faces +Z** (the tray is at +Z) | Family, a child of one or two at home: beech high chair, four splayed legs (feet at (+-0.22, +-0.23)), pale-blue padded seat with its top at **y 0.645**, padded back to y 1.05 leaning 8 degrees, a white tray with a rim at **y 0.815** (0.44 x 0.24, z 0.01 to 0.25), a foot rest at y 0.34, a three-point harness with a red buckle. Nobody sits in it |
| `driving_school_sign` | 0.62 x 1.05 x 0.52 | 428 | lit | ground centre; **a face to +Z and one to -Z** (`DrivingSchool` turns +Z to the street; its middle is 0.55 m from the wall) | Licences, outside a driving school: a sandwich board, two white boards 0.56 x 0.90 in black frames leaning 12 degrees to a hinge bar at y 1.02 to 1.05, feet at z +-0.26, two steel stays. Each face: DRIVING SCHOOL (capitals 0.09 m), a learner plate 0.36 m across (white square, red L), LESSONS on a red band. Read across a street by its L, not by its lettering |
| `test_checkpoint_beacon` | 0.56 x 2.90 x 0.22 (x -0.11 to +0.45) | 314 | glow | **the middle of its base** on the ground; the flag flies to +X (the code does not turn it) | Licences, the practical test's next checkpoint, 2.4 m to the kerb side of the lane: a rubber base 0.22 m across, a teal post 0.09 m thick tapering to 0.07 at y 2.56 with two white reflective bands (y 0.95 to 1.13, 1.38 to 1.56), a yellow flag 0.44 x 0.28 on a staff from y 2.60 to 2.88, a teal knob to 2.90 |
| `learner_plate` | 0.18 x 0.18 x 0.004 | 34 | lit | **centre of its back**; the L faces +Z | not asked for by code yet. L plate: white, bold red L, rounded corners; one-sided (stick it on a bumper or a boot lid) |
| `parking_box_marker` | 2.40 x 0 x 5.50 (flat, at y 0.005) | 16 | lit | ground centre; **the bay's length is Z** | not asked for by code yet (`LicenceProps.MakeBay` draws the test's bay itself, sized to each route). A parking bay in worn white paint: four lines 0.10 m wide round a 2.40 x 5.50 rectangle (2.20 x 5.30 clear inside), a 0.30 m tick inwards from the middle of each side. One-sided, lying 5 mm above the pivot's plane |
| `parking_box_posts` | 2.52 x 1.20 x 5.59 | 256 | glow | ground centre of the same bay | not asked for by code yet. Four red and white banded poles 0.04 m thick and 1.20 m high on rubber bases 0.22 m across, **their centres at (+-1.15, +-2.70)**; 2.26 x 5.36 m clear between the poles |
| `ring_box` | 0.05 x 0.06 x 0.05 | 256 | lit | **centre of its base**; the lid stands open at the back (-Z), **the ring shows from +Z** | the tool of `propose_kneel` (and `_down`, `_up`): it comes with the clip (`JobBody.Clip` attaches a clip's tool at its grip), on the **right** palm. Red velvet ring box, open: base 0.05 x 0.05 x 0.018, lid 0.042 m high standing at the back, a gold ring with a stone upright in a white cushion. The lid is a little shorter than the base: it would not close |
| `bouquet` | 0.28 x 0.40 x 0.28 | 756 | lit | **the middle of the wrapped stems, 0.10 m above their cut ends**; the stems are Y; the bow is at +Z | not asked for by code yet (the wish-list wants it in the left hand). Round bridal posy: thirteen white and pink roses in a dome (y 0.15 to 0.30) over a collar of leaves. **The fist closes round the wrap: a round bar 0.035 m thick from y -0.06 to +0.06**; a pink bow with tails on the +Z side at y 0.08. No clip holds it: parent it to a hand so the palm is at the pivot and the stems run along the fist |
| `birthday_cake` | 0.26 x 0.16 x 0.26 | 432 | lit | **centre of the base**; HAPPY BIRTHDAY on top **reads from +Z** | not asked for by code yet. Iced cake 0.20 m across with its top at y 0.088 on a silver board 0.26 m across, pink piping, sprinkles, five candles with flames to y 0.16. Lit, not glow (the flames are 2 cm) |
| `baby_bundle` | 0.22 x 0.18 x 0.50 | 440 | lit | **the middle of its underside**; lies along Z, **head at +Z**, face up (+Y) | not asked for by code yet. A swaddled baby: white blanket with a pink edge across the chest, a face (nose, shut eyes), a pink hat. Held by `hold_baby` (attach at the clip's grip): **the left palm is under (0, 0, -0.10)**, fingers towards the feet, thumb to the bundle's +X; the head rests in the crook of the left arm |
<!-- end: family -->

<!-- module: garage -->
### Garage (`scripts\props_garage.py`, `paint_garage.py`, `checks_garage.py`)

40 props (15 and the 25 parts of the four bikes), 29 314 triangles, 12 atlas regions (280 064 px with gutters; `garage_fairing` is no longer on anything). **Made to what `Scripts/Garage` asks for by id: the code is the specification.** Every number below is `BikeCatalogue.All` (`BikeBody.cs`) or a constant of `GarageCar.cs` / `BikePlaces.cs`, in Unity axes from the prop's pivot (x right, y up, z forward); `checks_garage.py` measures them on the built meshes. The riders' clips are in `CityAssets\scripts\clips_garage.py`.

A two-wheeler's pivot is the vehicle's origin, **on the ground under its middle, which is not midway between the axles**. Each is built from the parts `BikeBody` moves, each about the point it is turned about, and is in the bundle **twice**: in those parts, which is what `BikeBody.Build` assembles for anything ridden or coloured (wheels spin, cranks turn with level pedals, fork and front wheel steer), and put together as ONE mesh under the bike's own id, for a bike that only stands on show in the model's own colour (`Build(..., still: true)`: one draw call) and for the sheets of `CityAssets` and `scenes.py`. `checks_garage.py` puts the parts where the code puts them and holds them to the whole. Both stands are folded: the code stands a parked bike upright and it is ridden like that.

**Paint.** What is in the bike's colour (frame tubes, mudguards, chain guard, tank, tail, side covers, fairing) is a part of its own in the parts build, `<bike>_frame_paint` and `<bike>_fork_paint`, mapped on the neutral swatch `garage_white`; the code draws those two with `PropLibrary.Tinted(colour)` (the atlas material times the colour, one shared material a colour; it divides by the swatch's (0.90, 0.90, 0.88) so that what is seen is the colour asked for). In the whole bike the same faces are on the colour's own swatch (`garage_teal`, `garage_red`, `garage_navy`), and `BikeDef.Colour` is those swatches to the digit (0x124D70, 0x9E090A, 0x090F2B), so a shop's display bike and the one ridden away are the same colour. The sport bike's fairing used to carry a decal (red over white with a swoosh): a decal across both cannot be tinted, so the fairing is now plain paint and the white belly pan a shell of its own lapped over it, with three slats for the vent.

| part | pivot | what |
|---|---|---|
| `<bike>_frame` | the vehicle's origin | everything fixed to the frame that is not in the bike's colour: saddle or seat, engine, exhaust, carrier, chain, stand, plate |
| `<bike>_frame_paint` | the vehicle's origin | the painted part of the frame; neutral, tinted by code |
| `<bike>_fork` | `HeadTop`, the top of the steering axis | the whole steering assembly: fork, yokes, bars, grips, levers, lamp, basket, mirrors |
| `<bike>_fork_paint` | `HeadTop` | its painted part (fork blades, front mudguard); neutral. `bicycle_road` has none: its fork is black |
| `<bicycle>_wheel` | the middle of the axle, axle along X | one mesh for both axles |
| `<motorbike>_wheel_front`, `_wheel_rear` | the middle of the axle, axle along X | the front one is a child of the fork |
| `<bicycle>_crank` | the bottom bracket | both cranks, the chainring and the pedals' spindles; the right crank forward, as `BikeBody.Pedals(def, 0)` has it |
| `<bicycle>_pedal` | its middle | one pedal, 0.09 x 0.03 x 0.075; the code keeps one level at each crank's end |

| | frame | frame_paint | fork | fork_paint | wheel(s) | crank | pedal |
|---|---|---|---|---|---|---|---|
| `bicycle_city` (triangles) | 340 | 234 | 565 | 176 | 444 | 88 | 44 |
| `bicycle_road` | 412 | 120 | 768 | | 444 | 88 | 44 |
| `motorbike_standard` | 1 326 | 264 | 990 | 48 | 524 front, 572 rear | | |
| `motorbike_sport` | 1 116 | 600 | 484 | 48 | 580 front, 572 rear | | |

| id | size (X x Y x Z, Unity) | triangles | material | pivot and facing | use / grips / which feature |
|---|---|---|---|---|---|
| `bicycle_city` | 0.64 x 1.17 x 1.79 | 2 379 | lit | the vehicle's origin: the ground under its middle; front wheel to +Z | `BikeBody.cs` (CityBicycle; also stood in the docks and racks by `BikePlaces`). Town bicycle, teal step-through frame, mudguards, chain guard, carrier, wicker basket, lamp, bell. **Axles** (0, 0.34, -0.52) and (0, 0.34, 0.58), wheels 0.68 across. **Bottom bracket** (0, 0.29, -0.08), cranks 0.17; pedals 0.09 x 0.03 x 0.075 whose MIDDLE is on the crank circle at x +-0.13: the right one forward (0.13, 0.29, 0.09), the left back (-0.13, 0.29, -0.25). **Saddle top** (0, 0.93, -0.30). **Grips** along X, 0.03 thick, 0.11 long, centred (+-0.26, 1.14, 0.16), on a bar that rises and sweeps back from the stem. **Steering axis** from (0, 1.00, 0.36) down to (0, 0.70, 0.44). Teal (`BikeDef.Colour` 0x124D70); in parts it is whatever colour the code asks for |
| `bicycle_road` | 0.45 x 1.06 x 1.72 | 2 364 | lit | as `bicycle_city` | `BikeBody.cs` (RoadBicycle). Road bicycle, red diamond frame with a sloping top tube, black deep rims, derailleur, bottle; **a drop bar**: black-taped tops along X 1.00 up and 0.42 ahead of the pivot on a short forward stem, bending forward at x +-0.205 to the brake hoods (about (+-0.205, 1.02, 0.52), levers hanging in front) and down into the drops, which end at (+-0.205, 0.875, 0.43). **Axles** (0, 0.34, -0.49) and (0, 0.34, 0.55). **Bottom bracket** (0, 0.28, -0.07), cranks and pedals as the city bicycle's. **Saddle top** (0, 0.95, -0.29). **Grips: the tops**, along X, 0.030 thick, centred (+-0.13, 1.00, 0.42) (`BikeDef.Grip`): the one part of a drop bar that lies along X, which is what the game's hand rig holds; the hoods and drops are not held. **Steering axis** from (0, 0.99, 0.36) down to (0, 0.72, 0.43). No stand |
| `motorbike_standard` | 0.74 x 1.26 x 2.07 | 3 724 | lit | as `bicycle_city` | `BikeBody.cs` (StandardMotorbike). Naked roadster, navy tank, finned twin, exhaust and silencer on the right, twin shocks, mirrors (1.26 up; the bars are at 1.09), plate. **Axles** (0, 0.33, -0.651) and (0, 0.33, 0.723) (the game's scooter's), wheels 0.66 across, tyres 0.115 and 0.15 wide. **Seat top** (0, 0.83, -0.20), flat from z -0.325 to -0.115. **Pegs** round, along X, 0.032 thick, 0.08 long, centred (+-0.24, 0.34, -0.10). **Grips** along X, 0.034 thick, 0.12 long, centred (+-0.31, 1.075, 0.30). **Steering axis** from (0, 0.98, 0.40) down to (0, 0.80, 0.47); the fork legs run parallel to it through the front axle |
| `motorbike_sport` | 0.76 x 1.14 x 2.08 | 3 400 | lit | as `bicycle_city` | `BikeBody.cs` (SportMotorbike). Sports bike, red with a white belly pan, full fairing with smoked screen and twin lamps (fixed to the frame), tail cowl, upside-down forks, twin front discs; **clip-ons**: a bar clamped to each fork leg under the top yoke, running back and out to its grip. Axles as the standard. **Seat top** (0, 0.84, -0.22), flat from z -0.32 to -0.16. **Pegs** along X, 0.032 thick, centred (+-0.24, 0.38, -0.16). **Grips** along X, 0.034 thick, centred (+-0.27, 0.915, 0.42) (`BikeDef.Grip`), with their levers and reservoirs; the inner ends of the levers are just inside the fairing's rear lip, as on the real thing. **Steering axis** from (0, 0.96, 0.41) down to (0, 0.78, 0.48) |
| `helmet` | 0.28 x 0.31 x 0.32 | 506 | lit | **the Head bone** (the base of the skull); visor to +Z | `BikeBody.Helmet` (`BikeMount` puts it at the Head bone's position with the PLAYER's rotation, each frame). Full-face helmet, white, red stripe, smoked visor. From 0.080 below the pivot (chin) to 0.226 above. The head's middle is 0.113 above the pivot; the opening is 0.096 x 0.117 half sizes (a 0.076 x 0.097 head and 0.02 all round) and it is hollow to 0.208 above the pivot. Hair is not hidden: use the game's `Hat` blend shape |
| `bicycle_helmet` | 0.21 x 0.22 x 0.32 | 396 | lit | as `helmet` | **not asked for by code.** Vented cycling helmet on top of the head, teal with ten dark vents, a short peak, straps to buckles under the ears at (+-0.081, 0.018, 0.004). From 0.089 to 0.231 above the pivot; rim inside 0.084 x 0.105 half sizes |
| `wheel_5spoke` | 0.22 x 0.68 x 0.68 | 1 022 | lit | the middle of the wheel; **the axle along X, the outer face looking along -X** | `GarageShop.WheelIds`, `GarageCar.Wheels`: put in place of the car's own wheel (which is hidden), scaled (width / 0.22, radius / 0.34, radius / 0.34), and turned 180 degrees about Y on the car's right side. **A whole wheel, tyre included**: radius 0.34, 0.22 wide, rim 0.25 in radius with its lip 0.007 inside the outer sidewall, the back closed. Five thick silver spokes; brake disc and red caliper seen between them; chrome cap, five nuts |
| `wheel_mesh` | 0.22 x 0.68 x 0.68 | 1 128 | lit | as `wheel_5spoke` | the same wheel with a cross-laced gold mesh centre (ten pairs of spokes) in a polished lip |
| `wheel_steel` | 0.22 x 0.68 x 0.68 | 1 016 | lit | as `wheel_5spoke` | the same wheel with a pressed steel centre painted white, eight ventilation holes (painted dark, not cut through), chrome hub cap, five nuts |
| `car_spoiler` | 1.40 x 0.20 x 0.26 | 308 | lit | on the boot deck under the middle of its two feet; leading edge to +Z | `GarageCar.Fit`: at (0, DeckPostHeight, Rear + 0.26), X scaled by the deck's half width / 0.70. Raised wing, gloss black: aerofoil 0.12 to 0.17 up between two end plates, **top 0.18 above the pivot**, on two uprights at x +-0.42 whose feet are 0.056 x 0.19 and **reach 0.02 below the pivot** (into the deck, so that no gap shows on a deck sloping up to 25 degrees) |
| `car_spoiler_lip` | 1.40 x 0.06 x 0.18 | 94 | lit | the centre of its underside; front edge to +Z | **not asked for by code** (it has one spoiler, the wing). Boot-lid lip, gloss black: nothing at the front edge rising to 0.06 at the back, ends rounded off |
| `car_roof_bar` | 1.20 x 0.13 x 0.06 | 212 | lit | on the roof under the middle of the bar, at the height its feet stand on; the bar along X | `GarageCar.Fit`: ONE bar, fitted twice, at (0, RoofFootHeight, z), X scaled by (the roof's half width + 0.04) / 0.60. Aluminium aero bar 0.080 to 0.110 up (**top 0.11 above the pivot**; it clears a roof crowned 0.06 over its feet) with a rubber strip and end caps, on two black clamp feet at x +-0.52 that **reach 0.02 below the pivot** |
| `car_bull_bar` | 1.00 x 0.60 x 0.14 | 648 | lit | the bottom centre of the hoop, 0.02 behind the front of its tubes; faces +Z | `GarageCar.Fit`: at (0, NoseLow + 0.05, Front + 0.07), X scaled by the nose's half width / 0.50, Y by the nose's height / 0.60 (0.72 to 1.07 on the cars it fits). Chrome tubular bull bar: outer hoop with its lower corners cut in, uprights at x +-0.275, rails 0.14 and 0.37 up, a dark slotted skid plate; two mounts at the uprights **reach 0.12 behind the pivot** at 0.14 up (into the nose). No round lamps: the Y scale would squash them |
| `bike_dock` | 2.60 x 1.25 x 0.18 | 938 | glow | ground centre, **0.80 behind the line of the hoops**; the bicycles stand on the pivot's line facing +Z | `BikePlaces.Dress` (`Shapes.Prop("bike_dock", root)`, then two city bicycles at x -0.8 and 0). Bike-share dock for three: a rail on the ground from x -1.14 to +1.46 at z 0.75 to 0.90, three wheel hoops 0.72 high at x -0.80, 0, +0.80 on the line z 0.80 (0.087 clear between their legs, a lock with a green light on each), a green pay post 1.25 high at (1.38, 0, 0.80) whose screen and numbered bicycle panel face -Z. A city bicycle at a hoop's x on z 0 has its front tyre between the legs, over the rail and under the hoop's top; its basket and lamp are above and behind the hoop |
| `bike_dock_kiosk` | 0.50 x 1.70 x 0.30 | 288 | glow | ground centre; faces +Z | **not asked for by code** (the dock has its own pay post). Payment terminal: screen 1.22 to 1.42 up, card reader beside it, a map of the docks 0.58 to 1.03 up, "BIKES" across the top in 0.12 m letters on both faces |
<!-- end: garage -->

<!-- module: site -->
<!-- module: site -->
### Civic life, the courts, the station's back rooms (`scripts\props_site.py`, painted by `paint_site.py`, measured by `checks_site.py`)

**The code is the specification**: every id in the first three tables is asked for by the mod, which draws a stand-in of boxes until the bundle has it, and each model has that stand-in's size, pivot and facing (`Scripts\Civic\CivicProps.cs`: `Resolve`, line 157, asks `PropLibrary`; the stand-ins are `CivicStandIns.Build`, line 225. `Scripts\Justice\Courthouse.cs` line 229, `CommunityService.cs` line 334. `Scripts\Law\StationProps.cs` `Holder`, line 56, called from `StationInterior.cs` lines 805 and 813). All points are Unity axes from the pivot. The code letters nothing on any of these models. The four campaign colours are `CivicColours.Party(0..3)` to the digit (amber, teal, plum, olive), as swatches.

How the pieces go together (`Scripts\Civic\SiteDresser.cs`; `checks_site.py` holds the models to it):

- **Scaffold.** `scaffold_bay` every 2.40 m along a wall (`BayWidth`), its pivot 0.12 m off the wall (`WallGap`), deck top 2.40 m up. `scaffold_bay_upper` at the same pivot raised 2.40 m (`DeckHeight`), and another every 2.00 m above (`LiftHeight`). `scaffold_net` at an upper bay's pivot, `scaffold_fan` at a first bay's. Standards are at x +-1.175, not the stand-in's +-1.18: a bay's stand beside the next bay's instead of through them. Ledgers, boards, rails, toe boards, nets and fans run from x -1.20 to +1.20 and meet the next bay's.
- **Hoarding.** `site_hoarding` / `site_hoarding_notice` every 2.40 m, 0.12 m off the wall; nothing of them is further back than z -0.06.
- **Road works.** `trench_cover`, `asphalt_patch` and `road_marking` are 2.00 m long and laid four in a row every 2.00 m: each one's two ends are the same section, so they join. `manhole_open` where the cover was, `manhole_lid` lying 1.25 m further along the lane, `works_tent` over the hole with its open side to the pavement.

Campaign (`Scripts\Civic\CampaignWorld.cs` lines 276, 303, 343, 416, 570, 596):

| id | size (X x Y x Z, Unity) | triangles | material | pivot and facing | use / grips / which feature |
|---|---|---|---|---|---|
| `campaign_poster_0` .. `_3` | 0.60 x 0.85 x 0.017 | 144 each | lit | **the centre of its back as the code hangs it** (0.012 m off the wall); printed side +Z | Election poster on a mount: a band of stars in the party's colour top and bottom, the candidate (a different head for each), VOTE (capitals 0.14 m). **The mount reaches 0.010 m behind the pivot**, to close the gap the code leaves at the wall; the print is 0.007 m in front |
| `campaign_board_0` .. `_3` | 0.60 x 0.93 x 0.46 | 272 each | lit | ground centre; the same on both faces (+Z and -Z) | `sandwich_board`'s frame in the party's colour: the candidate, VOTE (capitals 0.13 m), three stars |
| `polling_sign` | 0.60 x 0.93 x 0.46 | 216 | lit | ground centre; both faces alike | White A-board: POLLING PLACE on a blue band, VOTE HERE (capitals 0.125 m), a ballot going into its box. **No arrow** (the wish-list's): the code stands it 2.8 m along the wall from the door facing back at it, and it is read from both sides |
| `soapbox` | 0.60 x 0.30 x 0.45 | 576 | lit | ground centre | A wooden crate upside down: **the top is 0.30 m up and flat all over** (`CampaignWorld.BoxTop`; three boards 4 mm apart). Slatted long sides, panelled ends with a hand hole. `speech_gesture` keeps the feet inside 0.36 x 0.26 |

Building sites and road works (`Scripts\Civic\SiteDresser.cs` lines 382, 837 to 876, 945 to 952, 992 to 1010, 1054):

| id | size (X x Y x Z, Unity) | triangles | material | pivot and facing | use / grips / which feature |
|---|---|---|---|---|---|
| `scaffold_bay` | 2.45 x 2.40 x 1.83 (the bay itself 2.40 x 2.40 x 1.80; the rest is base plates, couplers and sleeves) | 612 | lit | **the ground at the middle of its wall side**; it stands out towards +Z (z -0.015 to +1.815) | The first lift. Standards at (+-1.175, 0.06) and (+-1.175, 1.74) on base plates, ledgers 2.30 up, four transoms, **boarded deck from 2.37 to 2.40 up, z 0.09 to 1.71** (the code stands workers on it 0.80 out). One brace, across the wall side: the way through along X is clear below 2.0 m. Yellow sleeves on the street-side standards from 0.30 to 1.85 |
| `scaffold_bay_upper` | 2.43 x 2.00 x 1.81 | 528 | lit | as `scaffold_bay`, on the deck below | A lift above: the same plan, its own deck from 1.97 to 2.00 up; on the street side (z 1.69) guard rails 1.00 and 0.50 up and a toe board to 0.15, which guard the deck it stands on; spigot collars at the foot of the standards |
| `scaffold_net` | 2.38 x 1.96 x 0.012 | 192 | lit | as `scaffold_bay_upper`: the net is 1.80 m out, from 0.02 to 1.98 up | Green debris netting for one upper bay, the same from both sides, a little slack. Opaque |
| `scaffold_fan` | 2.40 x 0.81 x 0.88 (from 2.04 to 2.85 up) | 120 | lit | as `scaffold_bay` | The protection fan: boards sloping up 28 degrees from the first deck's outer edge (z 1.80, 2.41 up) to (z 2.60, 2.83 up), on two raking tubes from the street-side standards |
| `site_hoarding` | 2.40 x 2.40 x 0.10 | 214 | lit | ground centre of the stand-in's sheet: the mesh is z -0.06 to +0.04; painted side +Z | Blue painted panel, a white band from 2.23 to 2.37, a DANGER KEEP OUT sign 0.95 x 0.41 (capitals 0.14 m) centred 1.70 up, a hard-hat roundel either side; timber frame and concrete feet behind |
| `site_hoarding_notice` | 2.40 x 2.40 x 0.10 | 282 | lit | as `site_hoarding` | The same carrying the board instead of the warning sign: white, 1.20 x 0.80 centred 1.50 up, OPENING SOON (capitals 0.17 m), face at z 0.040 |
| `site_notice_board` | 0.90 x 1.80 x 0.45 | 484 | lit | ground centre; read from +Z | Two galvanised posts at x +-0.38 on weighted feet 0.45 long; board 0.90 x 0.65 centred 1.45 up, face at z 0.040: NOTICE (capitals 0.13 m) on a blue band, a site plan and a sheet of small print. The reader stands 0.9 m in front (`SiteDresser.Notice`) |
| `skip_bin` | 3.40 x 1.25 x 1.70 | 722 | lit | ground centre; its length along X | Builder's skip: sloped ends, rim rail, four lifting lugs, CITY SKIPS on both long sides (capitals 0.20 m), rubble and timber to 0.15 m under the rim |
| `road_sign_works` | 0.75 x 1.55 x 0.60 | 488 | lit | ground centre; face +Z | ROAD WORK AHEAD (capitals 0.16 m) on an orange board 0.75 square with rounded corners, bottom edge 0.775 up, face at z 0.026, on a steel stand with a sandbag over its back rail |
| `trench_cover` | 1.00 x 0.02 x 2.00 | 237 | lit | ground centre; lies along Z | 2 m of open trench: a painted trench 0.56 wide (cut sides, an iron main, a red duct) between two banks of broken surface with lifted slabs. Its ends join the next piece's |
| `asphalt_patch` | 1.20 x 0.012 x 2.00 | 18 | lit | ground centre; lies along Z | 2 m of fresh surfacing, rolled edges, sealed along both sides. Its ends join |
| `road_marking` | 0.10 x 0 x 2.00 | 30 | lit | the road under its middle; lies along Z; **flat, one-sided, 0.004 above the road** | Sprayed yellow setting-out line: two strokes with a break, a cross tick, an arrow head |
| `spoil_heap` | 0.90 x 0.35 x 0.90 | 192 | lit | ground centre; any heading | Earth and broken surface, its top off centre, slabs of asphalt in it |
| `works_tent` | 1.50 x 1.90 x 1.50 | 506 | lit | ground centre; **open at +Z** | Frame tent over an open cover: legs at (+-0.72, +-0.72), yellow walls on -Z and both X sides with a red and white reflective band 1.0 up, pyramid top with an orange valance (eaves 1.53), the door rolled up under the front eave. No floor; nothing inside below 1.3 m |
| `manhole_open` | 0.90 x 0.06 x 0.90 | 334 | lit | ground centre | A square of broken-out bedding 0.02 high, the iron frame standing 0.06 up (0.80 across outside, 0.62 inside), the shaft painted inside. It has no lid of its own |
| `manhole_lid` | 0.66 x 0.05 x 0.66 | 110 | lit | ground centre; round | Cast-iron cover lying flat: tread pattern, CITY SEWER, two key holes. The cover in place, and the one lifted out |

The courts and the station (`Scripts\Justice\Courthouse.cs`, `CommunityService.cs`; `Scripts\Law\StationInterior.cs`):

| id | size (X x Y x Z, Unity) | triangles | material | pivot and facing | use / grips / which feature |
|---|---|---|---|---|---|
| `courthouse_sign` | 1.42 x 1.95 x 0.42 | 856 | lit | ground centre; **read from +Z**; its back to a wall 0.30 m behind | The stand-in's sign made properly: two black posts at x +-0.60 on stone feet with brass ball finials to 1.95; a navy board 1.30 x 0.62 centred 1.52 up, face at z 0.025, lettered COURTHOUSE (capitals 0.14 m) and "EQUAL JUSTICE UNDER LAW" under a pair of scales; a brass cap over it. English only: the stand-in's translated lettering is not added to the model |
| `litter_pile` | 0.43 x 0.10 x 0.41 | 446 | lit | ground centre; any heading | A crushed drinks can, a ball of paper, a flattened carton, a paper cup on its side with its lid off, a sheet of newspaper, a crisp packet, three cigarette ends, all within 0.23 m of the pivot. `litter_pick`'s hand closes 0.55 m ahead of the player |
| `office_desk` | 1.40 x 1.16 x 0.70 (the top is 0.75 up; the rest is the screen) | 954 | lit | ground centre (the code stands it by its bounds: the footprint is exactly the top's); **whoever works at it stands at +Z**, its back to a wall | Wood top on a dark steel frame, three drawers on the right (x +0.24 to +0.66) facing +Z, a modesty panel at the back; a screen facing +Z showing a street map, keyboard, mouse, a case file, a tray of paper, a mug |
| `evidence_door` | 1.04 x 2.12 x 0.066 | 622 | lit | the floor under the middle of its back face (hung by its bounds); read from +Z | Closed grey steel door in a dark frame: a blue plate lettered EVIDENCE 0.70 x 0.16 centred 1.62 up (capitals 0.105 m: a door plate, not a street sign), lever handle and lock at x +0.34, 1.02 up, a keypad over it, kick plate, three hinges at x -0.455, a closer on the head. English only |

Not asked for by code (made for an earlier wish-list; kept because they are done):

| id | size (X x Y x Z, Unity) | triangles | material | pivot and facing | use / grips / which feature |
|---|---|---|---|---|---|
| `tower_crane` | 6.0 x 47.75 x 40.06 | 1 468 | lit | ground centre of its pad; jib along +Z | Static tower crane: lattice mast to the slewing ring 40 m up, cab at +X, 30 m jib, 10 m counter-jib with its weights, trolley 18 m out, hook block 26 m up. Civic's README says no site can use one (no empty lot) |
| `campaign_lawn_sign` | 0.60 x 1.07 x 0.026 | 312 | lit | ground centre; both faces alike | `campaign_poster_1`'s print on both faces of a board from 0.22 to 1.07 up, on a wire stake |
| `gavel` | 0.10 x 0.07 x 0.28 | 320 | lit | ground centre; lying on its side, head across X at the +Z end | Judge's gavel; the handle is 0.022 thick. Justice wants only its sound |
| `gavel_block` | 0.12 x 0.03 x 0.12 | 130 | lit | ground centre; round | Sound block |

Atlas: 28 regions (5 swatches: `site_party_0` .. `_3`, `site_door`; 23 decals), 465 536 pixels with their gutters. One word is one strip that several props share (`site_vote` is on ten props), the stars are geometry. Module total 12 633 triangles.
<!-- end: site -->
<!-- end: site -->

## Fit to the character

`CharacterAssets\scripts\extract_reference.py <game data> PropAssets\_reference PropAssets\_tools\pylib` (needs
UnityPy in that pylib) reads the game's detailed character for measuring. Nothing from it is used by the build:
the two sizes taken from it are the constants `WAIST` and `HEAD` at the top of `props.py`, and the local
positions in the table above.

## Add a prop

1. Write a function in the module it belongs to, `scripts\props_<name>.py` (copy the nearest one), and add it to
   that file's `PROPS` with a triangle budget. A new finish or lettered face goes in the module's
   `paint_<name>.py` (`register` returns the swatches and the painters). Add them at the end: regions are painted
   in order from one random stream, so appending leaves every earlier region as it was. If a clip will hold the
   prop, add its grip to `checks_<name>.py`. A new module is three such files and its name in the `EXTRA` lists
   of `atlas.py`, `props.py` and `check_life.py`.
2. `python scripts\atlas.py` if the atlas changed, then `blender -b -P scripts\build_props.py -- <id>` and look
   at `previews\<id>.png`. Repeat.
3. `build.ps1`, add a row to the tables above, then the mod's own build.
