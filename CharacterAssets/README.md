# Character assets

What ships, both judged in the HDRP preview (see below) before going in:

- **The hair finish** (`Scripts/Character/HairFinish.cs`, applied by `HairLook.cs`): the game's own hairstyles and
  beards, finished. The game draws them as see-through strips with one flat colour and one fixed blond highlight,
  which is why black hair comes out streaked blond, thin hair looks bald from play distance and everything has a
  wig's sheen. A finished layer writes depth where its strands are solid (the shader's depth prepass, which the
  game's build has), carries strand maps (`hair_strand_mask`, `hair_strand_normal`: generated noise, two 512 px
  textures for all hair) and takes its gloss, highlight and back-light from the hair colour, bounded against lit skin
  so that hair keeps its colour against a lamp (see "Hair against a strong light"). Sheets of every case, the
  game's look beside the finished one: `evidence\hair_*.jpg`, `evidencebeard_*.jpg`.
- **A scalp underlay** for the player's hair: a smooth, matt, hair-coloured cap on the skull, worn under the game's
  own hair and cut to each hairstyle's footprint, so what shows between the strips of a thin style is more hair.

```
build.ps1                 everything, start to finish (see below)
build_bundle.ps1          only the last step: build\ -> the asset bundle, and the check that it loads
scripts\
  extract_reference.py    reads the game's detailed character (rig, meshes) into _reference\ for fitting
  ba_common.py            reference meshes, coordinate systems, mesh export, preview renders
  hair_lib.py             the head (skull surface, hairlines), scalp cap, strand cards
  build_shoes.py          THE SHOES: three styles, four colourways each, both sexes (see "Shoes")
  build_garments.py       THE CLOTHES: cut, draped, baked, painted (see "Garments"); garment_lib.py is its toolbox
  build_wardrobe.py       the rejected pilot (see "Wardrobe pilot"); its Form class is still what measures the body
  build_strands.py        the two strand maps of the hair finish (noise only)
  build_underlays.py      THE UNDERLAYS: cap per sex, footprint texture per hairstyle, before/after renders
  make_catalogue.py       joins the catalogues into build\catalogue.json
  overview.py             stacks preview sheets into one image
  hair_atlas.py, hair_styles.py, build_hair.py, build_beards.py
                          parked: generated strand-card hair and beards (see "What did not work")
UnityProject\             a bare 2022.3 project whose only job is to turn build\ into the bundle
CREDITS.md                source and licence of every asset
_reference\ _tools\ build\ previews\     generated, git-ignored
```

## The preview: where a look is judged

Two rounds of looks were judged in Blender and failed in the game, because the game's HDRP shading is nothing
like Blender's. So nothing is accepted any more until it looks right in `PreviewProject`: a Unity 2022.3.62f2
project with HDRP 14.0.12 (the game's versions) that rebuilds the game's detailed character from the local
installation and renders it in batch mode.

```
preview.ps1 -Plan <plan.json> -Out <folder>    renders a list of shots to PNG (about 2 s a shot)
trial.ps1 <plan name>                          plan -> shots -> one labelled sheet per view, in previewsscripts\extract_preview.py                     game files -> _reference\preview (hierarchy, 108 meshes, 115 materials, 191 textures)
scripts\preview_plan.py                        the plans: who, which colours, which stage and view, which finish
scripts\preview_sheet.py                       lays shots (or cut-outs of screenshots) out side by side
PreviewProject\Assets\Editor\                  GameCharacter (the rebuild), PreviewRunner (stages, cameras, capture), PreviewLooks (the mod's looks)
PreviewProject\shared_sources.txt              game-free files of the mod the preview compiles too: what is judged is the code that ships
calibration\                                   the preview's version of the game's own look beside the player's screenshots
```

A look is given the bind poses of the game's MALE head mesh, whoever wears it, exactly as the mod does
(`PreviewLooks.WornBindposes`). Until 9 October the preview used the worn head's own, which put every look 7 mm too
far forward on the woman (her head mesh is bound that much further along the body's depth): women's sheets older than
that are 7 mm off; the ones in `evidence\` for the palette and the second set's last checks are not.

What is exact and what is not:

- **Hair and beards are exact.** `HDRP/Hair` is a stock shader of the same HDRP version, and every float, colour,
  texture, keyword, disabled pass and render queue of the game's 44 hair materials is copied from the game's files.
- **Skin, eyes and clothes are stand-ins.** The game's own shader graphs (`SH_Character*`) cannot be had outside
  the game; they are drawn with `HDRP/Lit` from the same textures (colour mask multiplied in, normal map, AO and
  smoothness from the mask map). Clothes that use texture arrays come out plain grey.
- **The stages are reconstructions.** Character creation uses the real light positions, intensities and camera of
  the game's scene (level2); the city uses the sun the F9 reports give (13 500 lux, white) and the gameplay camera
  (field of view 63, 3.1 m). Exposure, tone mapping and ambient light are not readable from outside and were set
  by eye against the screenshots in `calibration\`: the close-up matches well; the city view matches in scale,
  silhouette and hair, while skin and cloth come out paler than the game's (the stand-in shaders again).

So: trust the preview for hair against skin at both distances; treat skin and cloth results as relative
(before against after), and check those in the game.

Needs a GPU: HDRP does not render with `-nographics`, and the shots are taken from the editor's update loop
(no `-quit`) because the sky and the temporal anti-aliasing need a few frames to settle.

Also read from the game's files, and what the hair finish rests on: the compiled `HDRP/Hair` shader in the build
has the passes `TransparentDepthPrepass`, `TransparentDepthPostpass` and `TransparentBackface` for every keyword
set its hair materials use, including the alpha-tested set of the female styles; the game itself runs the depth
prepass on one material (`M_FemHair06Bun 1`).

## Run it

Needs Big Ambitions installed, Unity 2022.3.62f2 (the game's version) and Blender
(`winget install BlenderFoundation.Blender`; made with 5.2). Then:

```
powershell -File CharacterAssets\build.ps1
```

It installs UnityPy and Pillow into `_tools\pylib` (nothing system-wide), extracts the reference, builds the
caps and footprints into `build\`, renders previews into `previews\`, builds
`Assets\Mods\RealLifeOverhaul\Bundles\reallife_character` and loads it back as a check. `-SkipPreviews` and
`-SkipBundle` shorten it; `-GameData` points at another install. The dotnet build of the mod copies `Bundles\`
into the installed mod folder. Run it again after a game update that changes the hairstyles.

`previews\under_<m|f>_<game mesh>.png` shows each hairstyle without (left pair) and with (right pair) its
underlay. The game's strand textures are not part of the reference, so its strips are drawn as plain
half-clear ribbons: good for seeing where the scalp shows, not for judging the look. That needs the game.

## Hair against a strong light

Reported twice ("See I am bald under light"): at night, with a car's lamps a few metres beyond the player's head, the
head was a pale dome in the lamp's colour with a few bright flecks on it, and no hair to be seen.

**Cause** (read from the shader's source, `Runtime/Material/Hair/Hair.hlsl` of HDRP 14.0.12 and the `HDRP/Hair` graph,
and measured here). The game's hair is the "cards" Kajiya-Kay model. Per unit of light arriving, hair sends back:

| | | depends on the hair colour |
|---|---|---|
| lit from the front | colour / pi | yes |
| the highlight | 0.25 x Fresnel x (`_Specular` x (0.7 white + 0.3 `_SpecularColor`) + `_SecondarySpecular` x 0.5 x `_SpecularColor`) x (n + 2) / (2 pi) | no: 70% of it is white |
| light coming through | `_TransmissionColor` x (S + `_TransmissionRim` x R), S for a light behind the hair, R on every outline | no |

Fresnel is 0.05 head on and goes to 1 when the light is behind the hair as the camera sees it (0.26 with the lamp
150 degrees round). Lit skin sends back about 0.16. The game gives every hair material the back-light colour `FFA45F`
(1.0, 0.37, 0.11) and highlights of 0.31 and 0.60 at gloss 0.58: up to 0.55 through and 1.06 in the highlight, whatever the
hair colour, against 0.004 for black hair lit from the front. The game never shows this: its detailed characters only
appear in character creation, lit from the front. The mod puts one in the street with a camera behind it.
The finish as it was (until 11 October) set the back-light to the hair colour lifted a quarter towards white, which for
black hair is still 0.10 (two thirds of lit skin), left the highlight unbounded (0.92 at its worst), and left the
scalp underlay with the game's orange back-light. From play distance, where the strands blur into one mass (the head
is 36 pixels wide), all of that adds up to a head as bright as skin in the lamp's colour.

**Fix** (`Scripts/Character/HairRules.cs`, pure arithmetic, checked offline in `RealLifeOverhaul.Build/NavCheck/HairCheck.cs`;
applied by `HairFinish.Colour`, `HairFinish.Underlay` and `HairFinish.Through`):

- light through hair takes the hair's own colour at a quarter of the brightness the hair has lit from the front, never
  more than a quarter of lit skin for dark hair and half for any hair; a layer painted on the skull (the game's "Scalp" layers, the
  mod's underlay) lets nothing through and has no outline glow;
- the highlight is bounded at its worst angle: 0.2 of lit skin for black hair, up to 1.5 times the hair's own front-lit
  brightness for paler hair and never above lit skin; the gloss comes down first (to 0.3 at the lowest), then the strength;
- each strand texture gets its own alpha gain, from its alpha histogram when it is read back: the smallest from 3 to 6
  at which 0.7 of its strands are solid (the thinnest, `T_MaleHair04_BC`, gets 4.5; most keep 3);
- the game's one solid hair layer (the opaque scalp of `MaleHair08`) gets the bounded back-light too.

**Seen and measured**: `hair_light.ps1` renders every style at night from the third-person camera (the "night" stage of
`PreviewRunner`: 4 m behind the head, one shadowless lamp 3 m from the head at 0.8 m, 150, 90 and 20 degrees round from
the camera) as the game's own hair, as the finish was ("before": `hair:bounded=0,want=0` and `underlay:old=1`) and as it
is, in black, dark brown, blond and grey over light and dark skin. `evidence\hair-light\*.jpg` are the sheets (true
game pixels, twice enlarged); `evidence\hair-light\measured.txt` the numbers from a second set without tone mapping.
Black hair with the lamp behind, brightest hundredth of the hair as a share of lit skin, over all 14 styles: the game's
own 0.50 to 2.14, before 0.26 to 1.11, after 0.00 to 0.04 (dark brown: after at most 0.26). `strong_lamp_behind.jpg` is the
player's style under lamps 18, 70 and 280 times stronger, where lit skin is far past white: the game's and the earlier
finish are a white-hot dome, the finish now a dim brown fuzz, which is the hair's own colour where strands face the lamp.
`close_up_before_after.jpg` is the character creation view, to show the look close up is kept. The preview's lamp and exposure are chosen, not the game's:
what a car's lamp does at night in the game is for the game to show (Options > Tests has "Test: a strong lamp beyond my
head for 20 seconds").

## How it fits the game's character

- The game's detailed character has one skeleton of 55 bones, and every renderer on it lists them in the same
  order. Mod meshes store bone indices in that order and are written in the bind space of the game's male head
  mesh. At runtime the mod copies that mesh's bind poses onto them and hands them the character's own bones.
  (Other game meshes have other bind poses, some at another scale; `ba_common.load_reference` brings each into
  the common space.)
- The catalogue entry of an underlay names the game hair mesh it goes under (`under`) and the sex of the head.
  The mod looks the worn hair mesh up by name; a hairstyle without an entry is simply left as it is.
- The material is a copy of one of the game's own soft scalp materials with the underlay's texture, drawn
  before every layer of the hair, with the gloss turned down, in the hair's colour a little darkened.

## Budgets

Hair finish, for the characters that show the player (the double in the city, character creation, the shop preview):

| | |
|---|---|
| draws | one extra depth-only draw per hair layer (2 to 7 layers a style), nothing else added |
| textures | 2 shared strand maps of 512 px (BC7 and BC5, mips); one DXT5 copy of each strand texture worn (0.7 to 1.4 MB each), made once on the GPU read-back when a style is first worn |
| when | on a change of look only; nothing per frame |

Shoes: 6 meshes of about 4 000 triangles (one pair worn at a time), 3 normal maps of 1024 px (BC5) and 12 colour
maps of 512 px (BC7), all in the one bundle; one extra skinned renderer for the pair and one for the bare ankle,
in place of the game's own shoe renderer. Nothing per frame.

Underlay:


| | |
|---|---|
| per character | 1 extra skinned renderer, 1 material, 1 296 triangles, rigid on the skull |
| textures | 14 of 256 px, BC7, mipmaps (one per hairstyle and sex) |
| bundle | under 1 MB, loaded once per session, asynchronously |
| per frame | nothing |

## Garments (shipped)

`scriptsuild_garments.py` (tools in `scripts\garment_lib.py`) makes each garment the same way; one description
per garment in `GARMENTS`.

1. **Cut.** Fine quad panels (`Sheet`: a grid over across and up) are laid from the body's measured cross-sections
   (`Surface`: ray-cast slices of the game's meshes, which are only measured) with ease per region and openings
   that are drawn curves. Trousers: one sheet per leg, columns round the leg from the inseam, the crotch curve on
   the centre plane, the hip from the game's own trousers (every top of the game is cut to clear those), the waist
   along their top edge, the hem on a slant so it clears the shoe by its cut. Tops: front and back panels, straight
   rows to the armpit and above it a patch between the armholes (half the arm's outline each), the shoulder seams
   and the neckline; set-in sleeves whose first ring is the armhole and whose rings follow the arm round the elbow.
2. **Drape.** Blender's cloth solver on the welded fine panels (`drape`): pinned where the garment is held
   (waistband; neckline and shoulder seams; ribbed bands), a weak spring to the cut everywhere else so the
   silhouette stays, and the cloth a few percent larger than cut where it should fold (`grow`): that surplus is
   what buckles. Collision bodies are the bare body plus closed shells (`hulls`) of what the garment lies over
   (shoes, the game's trousers). Then it is frozen, lightly relaxed and pushed clear of the body.
3. **Game mesh.** Every third row and column of the draped panels (so the big folds are geometry and both meshes
   share one UV layout), plus construction as geometry: waistband standing proud, belt loops, button, hems and
   openings turned in so every edge has thickness, collar bands.
4. **Bake and paint.** The fine surface's normals in the game mesh's tangent frame, resampled into the panel's
   rectangle of the normal map (`Atlas`; no ray-cast bake needed), with the fabric's relief added; the folds'
   hollows darken the colour; seams, stitching, pockets, zip, ribbing and wear are painted in the panel's own
   metric coordinates, so they sit on the geometry. Painted at 2048, shipped at 1024.
5. **Skin.** Weights and Fat/Slim/Musc by closest surface point from the game's own nearest garment
   (`weights=`), so it bends and changes build as the game's does. The catalogue names the game piece whose skin
   part shows what the garment leaves bare (`skinFrom`); the game's piece itself is not drawn, so there is no body
   under the cloth to poke through. (The neck and upper chest belong to the head's mesh: over the shoulders a top
   is kept outside the game's own top, `yoke=`, or cut with more ease there.)

| garment | what | colourways | triangles |
|---|---|---|---|
| `legs_m_jeans` | men's straight-leg jeans: five pockets and fly stitched, belt loops, button, yoke | indigo, dark rinse, light wash, black, grey | 5 910 |
| `top_m_tee` | men's crew-neck T-shirt: set-in sleeves, ribbed collar, twin-needle hems | white, black, heather grey, navy, olive | 3 136 |
| `top_m_bomber` | men's bomber: blouson body, ribbed collar, cuffs and hem band, front zip closed to the collar (nothing under it has to show: with a jacket picked, the game's top is simply not drawn), welt pockets | sage, black, navy, burgundy, tan | 3 608 |
| `legs_f_jeans` | women's jeans: the same cut round the woman's body, slimmer leg | as the men's | 5 910 |
| `top_f_tee` | women's fitted T-shirt, scoop neck | white, black, blush, teal, mustard | 3 136 |
| `dress_f_summer` + `skirt_f_summer` | summer dress: fitted bodice with scoop neck and short sleeves (a top) and a flared, lined knee-length skirt (worn in the legs' place), meeting in the waist seam | navy and red with dots, sage, yellow with fine dots, black | 2 304 + 4 576 |
| `legs_m_chinos`, `legs_f_chinos` | tapered chinos: cotton twill, slanted front pockets, buttoned welt pockets behind, fly, stitched hem (`paint_chinos`) | khaki, stone, navy, olive, charcoal, rust | 5 910 |
| `legs_m_suit`, `legs_f_suit` | suit trousers: the chinos' cut in smooth wool, pressed creases, no visible stitching at waistband and hem | charcoal, navy, black, mid grey, tan | 5 910 |
| `legs_f_pencil` | pencil skirt to the knee on its own waistband: darts, side seams, centre back seam with a vent (`tube_skirt`, cut to the game's own skirt at hip and waist) | as the suit trousers | 1 920 |
| `dress_f_evening` + `skirt_f_evening` | evening dress: fitted bodice with a soft V neck and cap sleeves, and a long slim A-line skirt to the calf | black, emerald, burgundy, midnight, champagne, red | 2 304 + 6 656 |

All trousers carry the game's `Boots` shape (`garment_lib.follow_shape`: where the game's trousers draw their hem in for
the tall boots, ours go to where the game's cloth ends up, so the hem is inside the shaft). The mod sets it from the
game's own trousers by name, and leaves it off when shoes of the mod are worn (`ModLooks.Wear`).

**Skin under cloth** (`garment_lib.skin_hide`, catalogue `skinHide`, `ModLooks.SkinMesh`): a garment may list the
triangles of the bare skin shown with it that lie under its cloth; the mod leaves them out of the skin part it cuts
from the game's piece (only if that part still has the triangle count the list was made for). The evening skirt uses
it for the legs well above its hem. It exists because a coarse garment over fine skin does not bend exactly as the skin
does: the game never has skin under cloth, and neither should we.

**Sleeveless tops: tried, not shipped.** The evening dress was first built without sleeves (`SLEEVELESS_TRIAL` in
`build_garments.py`; `sleeveless=True` leaves the sleeves off and binds the armholes): bare arms and shoulders from the
game's `Swimsuit` torso (the game's `InsideTop` is a strappy camisole and `Top` a sleeveless shell: both also usable
as skin sources). Front, sides and every build came out clean once the skin under the cloth was hidden; the shoulder
straps and upper back did not: the head mesh's bib lies over the torso skin there, the cloth has to stand off it, and
with the skin hidden one then sees under the rim. What it needs: the strap and back neckline cut to hug the skin
(2 mm) with the game mesh as fine as the cloth there, or the bodice cut exactly to the outline of the game's
`InsideTop`/`Top` cloth so that there is no skin under it at all. Until then dresses have (short) sleeves.

A dress is two looks so that each takes its bare skin from a game piece of its own slot (arms from the game's
shirt, legs from the game's formal skirt) and nothing new is needed in the game: the skirt is cut from the
bodice's hem as built (`<bodice>_waist.npz`, so build the bodice first: `GARMENTS` is in that order) and takes the
bodice's weights and body shapes along the seam. One entry of the Dress slot picks both and overrides the Top
and Trousers slots.

One material each and one 1024 px normal map per garment (BC5). Colour comes one of two ways:

- **Tint mask** (tees, bomber, dress and skirt, and the way to do every new garment): the game's own convention for
  `SH_CharacterClothes`, read from its materials in the F9 reports and from its textures: a pale base map with only
  the shading in it (`<id>_bc`), and a colour mask (`<id>_cm`) whose pure green, blue and red areas each take one
  colour (`_MaskColorGreen` = the cloth, `Blue` = trim, `Red` = a third: a print, a zip), switched on per channel
  (`_ColorMaskMapHas…Channel`), with the sheen colour (`_SpecularColor…`) at half the cloth's colour as the game's
  own materials have it on average. A colourway is then three colours in the catalogue (`tints`) and costs no
  texture; a plain garment uses the shared 8 px all-green mask (`tint_green`). `garment_lib.Tinted` writes these; a
  colourway that needs its own base or mask (heather yarn, another dot size) gets one by its key. The mod sets this
  up in `ModLooks.FinishCloth`; the preview multiplies it out on the CPU (`PreviewLooks.Tinted`).
- **Baked** (jeans, shoes): one colour map per colourway, where a colourway is more than a tint (a denim wash mixes
  warp and weft by wear; a shoe's panels).

**Memory.** `ModAssets` opens the bundle and reads the catalogue and the two strand maps (0.7 MB); a look's mesh and
textures are read asynchronously when it is first worn (the game's piece, or the look worn before, shows for the few
frames that takes) and unloaded when no character wears it any more (`Request`, `Released`, `Sweep`: on a change of
look only). Before: every texture in memory from the start, 55.8 MB. Now: 8.4 MB with the dearest outfit worn
(`build_bundle.ps1` prints both figures: the `MEMORY` line of `UnityProject\Logs\verify.log`). On disk the bundle
went from 41.8 MB to 30.6 MB with the tint masks; the jeans' ten baked washes are 13 MB of that.
The F9 report's "new looks" block lists what is in memory and, per worn piece, its maps and tint colours.
They are picked on the options page (Top, Trousers, Dress per sex; the game's own slots are untouched and saves
load without the mod).

Judged in the preview against the game's nearest piece: level in silhouette and cleaner in make (seams, stitching,
folds, edges with thickness), and new cuts. In the game only the shoes have been seen so far (F9 captures of
9 October: the court sneakers draw correctly under the game's cloth shader, with the bare ankle from `skinFrom`);
no garment and no tint mask has been seen in the game yet. Known weak points: sleeve and tee hems are a little uneven
after draping; the dress print still stretches a little on the sleeve caps; in a full running lunge from behind, the
bent leg's calf shows as a small pale triangle through the back of the evening skirt and a thin line of the pencil
skirt's hem turn-in hangs below it; on the Fat build the women's tee shows one speck of the pencil skirt's waistband.
Not built yet: a hoodie (hood, kangaroo pocket, drawcords), a button shirt (placket, collar with stand, cuffs: the
front opening and collar are the new construction, and blazers follow from it), blazers, a blouse, leggings.

```
blender -b -P scriptsuild_garments.py -- legs_m_jeans     build one (all without a name; --no-drape to check a cut fast)
garment.ps1 legs_m_jeans                                    its evidence sheet -> evidence\garment_legs_m_jeans.jpg
garment.ps1 legs_m_jeans -Only mod_front,walk               a few shots while working -> previews\garment_legs_m_jeans.png
ship.ps1                                                    catalogue, bundle, the mod's build and checks
```

**To add a garment:** (1) a description in `GARMENTS` (copy the nearest one; the numbers are metres); for a new
kind of garment a `cut_*` function that returns sheets, and a `paint_*`; (2) an entry in `GARMENTS` of
`scripts\preview_plan.py` (the game's nearest piece, what is worn with it, the colourways); (3) build with
`--no-drape`, look at `garment.ps1 <id> -Only mod_front,mod_side,mod_back`, then drape and look at the whole
sheet: every pose, every build; (4) a `Colours(...)` line and label keys in `CharacterCatalogue.cs`, the labels
at the end of `Locales\en.json`; (5) `ship.ps1`, commit with the sheet. What went wrong on the way here, so it is
not tried again: a cloth mass above about 0.03 (it is per vertex: the cloth sags and necks in), draping over a
shoe or trousers as built (open and inner faces drag the cloth: use `hulls`), a plain Coons patch between the
armholes (it balloons the chest), fold shading measured against the game mesh's facets (stripes: `cavity` is
measured against the cloth's own smoothed surface), a hem left to the solver (ruffles: hold it to its cut).

## The palette, tiers and the selection (shipped)

Every garment is offered in one shared palette (`PALETTE` in `build_garments.py`, the same ids in
`CharacterCatalogue.Palette`): 22 plain colours and 10 prints or two-tone entries (heather, three dot prints, breton
stripes, pinstripe, gingham, colour block, two contrast trims). A garment that cannot carry a print still has the id,
as the entry's plain colour, so garment x colour is always valid: 14 garment meshes (12 picks) x 32 = 448 items, all
tints over shared base maps and masks. The jeans are tinted too now (three base maps per sex carry wash and wear;
`evidence\palette_jeans_baked_vs_tinted_*.jpg`). Sheets of every colourway: `evidence\palette_<garment>.jpg`, prints
close up: `evidence\palette_closeup_*.jpg`. Every garment has a `tier` (everyday, smart, premium).

On the options page each of Top, Trousers and Dress (per sex) is two dropdowns: the garment (the game's own first, then
by tier, the tier in the label) and the colour (the palette). The pick is still the id `<mesh>_<colour>` in the save
(old ids are read through aliases). The game's options page draws a dropdown from PlayerPrefs each time the page is
drawn and cannot be told to change one from code while it is open, so the mod writes its picks there on every change
(a mirror only: see "Whose picks" below) and the page shows them the next time it is drawn. Shoes keep one dropdown.
A new garment is one line in `CharacterCatalogue.Garments`, a new colour one line in `Palette` (and in `PALETTE`).
The game has no free colour picker for clothes: each piece has preset colourways written to `_MaskColor*`; copying
those onto our tint is possible (about 40 lines in `ModLooks`) but depends on which game piece is worn underneath.

Bundle now: 502 looks, 43.5 MB on disk; textures in memory 0.7 MB at start and 9.4 MB with the dearest outfit worn.

**Where the wardrobe goes next** (the player, having seen it in the game: "The dress looks good. Add more selections
and colors. More cloth types. Regular d2d wear and premium wears also."): (1) the dress family from the proven bodice
+ skirt: lengths mini/knee/midi/maxi, sleeves cap/short/three-quarter/long, necklines, skirt fullness, pleats: each is
a spec in `GARMENTS` (bodice first, then its skirt) plus a line in `CharacterCatalogue.Garments` and in
`preview_plan.GARMENTS`; (2) fabrics: the cloth shader has no detail map, so a fabric is a paint variant (its weave
in the garment's normal map) plus material numbers; sheen needs the mask-map packing of `SH_CharacterClothes`, which
is not known yet (for the skin shader it was read from the compiled shader: A = smoothness, G = occlusion; do the same
for the cloth graph, then ship small flat mask maps per fabric); (3) everyday tops (hoodie, polo, shirt: the front
opening and collar are the missing construction) and premium tailoring (blazer, coat), which follow from the shirt.

## Whose picks, and the game's own screens

Reported against 0.36.0: "In character creation I am compulsorily starting with what I set in the mod options page.
That is not changing." Two causes. The picks were read from PlayerPrefs at game start, so every new character began
in what some other game had last picked; and `ModLooks` drew the mod's piece over whatever piece of that kind the
game showed, so a click on one of the game's own entries changed the game's data and nothing on screen.

Now (`CharacterCatalogue`, `ModLooks.Apply`, `GridLooks`):

- **A pick belongs to one character.** Character creation starts with none (`BeginCreator`) and writes each pick
  straight into the new game's `modData`; a loaded save's picks are read from its `modData` and from nowhere else
  (`ReadSave`: a save without them wears the game's own); the main menu has none (`Leave`). PlayerPrefs is only what
  the options page draws its dropdowns from: written on every change, never read as a pick. A pick made on the
  options page with no character is not taken (one log line), and the page shows "the game's own" again when next
  drawn.
- **The last deliberate choice wins, per kind** (top, legs, feet; a dress is top and legs). Beside the picks the
  catalogue keeps the game's own piece and colour that lie under each kind (`reallife:look_game_<kind>` in the save).
  The game announces every change of a character's look (`AppearanceSetter.visualsUpdated`: the creator's entries,
  its colour row, Randomise, the body and gender tiles, the clothing store's and the hairdresser's screens all end
  there); `ModLooks.Apply` runs inside that announcement and calls `CharacterCatalogue.Observe`, which compares. A
  kind whose piece or colour is now another was picked by the player: the mod's look of that kind comes off at once,
  the options page follows, one log line. Nothing is polled.
- **A piece the game shows only for a while takes nothing off.** The clothing store's screen works on a copy of the
  player and writes it back only on Save; the shower and the toilet strip the player in the save's own data and put
  the clothes back. The mod's look is drawn on a character only over the very piece it was picked over (`Shows`):
  over a piece being tried, or bare skin, the game's own shows, and the look is back when that piece is. Saving the
  store's screen changes the save's data, which is seen the moment the player's double is dressed again (or, with
  the detailed player off, at the next pick, save or load).
- **The creator offers the mod's starter set**: the Everyday tier (`CharacterCatalogue.Starter`: tees, bomber, jeans,
  chinos, the summer dress, court sneakers), in every colourway. Everything else is bought at the rack (Leisure's
  "clothes must be bought", which a new game always starts with; what the new character wears is owned). A Smart or
  Premium garment picked on the options page during creation is not put on.

**What the game's creator allowed.** Entries can be appended to its grids safely, so there is no panel of the mod's
own. `CharacterCustomizer` raises `onSubCategoryChanged` when it has filled the grid (`ElementPicker.SetList`);
`GridLooks` then adds one tile per starter garment to the Torso, Legs and Feet grids as copies of the grid's own
entry template (so they lay out, scroll and are cleared exactly as the game's entries are: the game destroys every
copy of the template when it refills the grid), hands the grid's "selected" mark to and fro, and fills the game's
colour row through its public `AppearanceColorPicker.SetList` with the garment's colourways. Three private fields are
read by reflection (`elementPicker`, `onSubCategoryChanged`, `ElementPicker.entryTemplate` and its
`_currentSelectedOutline`); if one is missing the creator is left exactly as the game made it and the log says so.
A tile is a swatch of the cloth in its colourway (drawn from the item's three tint colours in the bundle's catalogue,
prints included), the garment's name and an "RLO" tag. The game's entries carry rendered thumbnails
(`AppearanceElementVariant.elementIcon`, made in the developers' editor); ours have none in the bundle yet.

**To verify in the game** (Player.log lines start with `Looks:`):

1. Main menu, mod options: every Top / Trousers / Dress / Shoes dropdown shows "Game's own", whatever was picked
   before the update.
2. New game. The character stands in the game's own random look; the log has "character creation starts in the
   game's own look".
3. Torso: after the game's entries come the mod's tiles (men: T-shirt, bomber; women: T-shirt, summer dress), tagged
   RLO. Click the T-shirt: it is on the character within a few frames, its tile is the marked one, the colour row
   shows 32 swatches. Click a swatch: the colour changes. Log: "Top (men) is now the mod's 'top_m_tee_...' (from
   character creation)".
4. Click any of the game's own Torso entries: the game's piece shows at once, its entry is marked, the colour row
   is the game's. Log: "Top is now the game's own '...' (picked in the game's own screen); the mod's '...' is taken
   off." The same with a colour of the game's piece, with Randomise and with the other body tile.
5. Legs and Feet the same (jeans, chinos; court sneakers). A woman's summer dress is in Torso; with it on, picking
   any Legs entry of the game's takes the whole dress off.
6. Start the game in a mod T-shirt: the character in the city wears it; the options page shows it; the wardrobe at
   home lists it as owned.
7. Quit to the main menu: the dropdowns show "Game's own" again. Load an older save: it wears what it wore, not what
   the new game picked. Start another new game: the creator starts in the game's own look.
8. In the city, with a mod top on: a clothing store's own screen shows the player in the mod top; clicking one of the
   store's tops shows that top (log: "shows the game's own piece for now"); closing without saving brings the mod
   top back; saving takes it off for good ("picked in the game's own screen"). A shower: bare skin, and the mod
   top is back afterwards.

Not verified (the game could not be run): how the tiles and their text sit in the grid's cells; whether 32 swatches
fit the colour row; the look of a swatch on the game's swatch template; that the mods' options can be opened during
character creation at all.

## Wardrobe pilot (not shipped)

`scripts\build_wardrobe.py` makes a garment around the game's body with topology of its own: the bare body is
softened, grown by the garment's ease and voxel-remeshed into a new even surface, cut open at collar, cuffs and
hem, given the body's skin weights and Fat/Slim/Musc shapes by nearest-point sampling, ribbed bands with an inner
lip, and UVs in real units for a tiling fabric (a generated knit so far). The mod's runtime for worn garments
(`ModLooks`: cloth layers, `skinFrom`, body shapes) and the preview (`wear:<id>`) both take the result as it is.

One garment has been through it, a men's knit sweater (11 110 triangles, one material, 512 px knit tile):
`trial.ps1 wardrobe_trial` renders it beside the game's sweater. Verdict: **not good enough to offer.** The fabric
reads as knitwear and the fit follows the body without the body showing, but the neckline is ragged (it follows
the neck's skin-weight contour instead of a designed line), the hips flare, the surface is lumpy where the form
is, there are no folds, and it is over the 8 000 triangle budget. What a garment worth offering needs:

- openings designed as curves (a neckline, a cuff, a hem drawn on the form) and cut cleanly along them;
- a silhouette of its own (drop from the chest, straight side seams) instead of an offset of the body;
- folds: baked from a cloth simulation in Blender on this same mesh, into the normal map;
- panels and detail (collar, placket, pockets, seams) as geometry or baked detail, and a per-garment texture
  with a colour mask, not only a tiling fabric;
- pose tests (walk, run, sit) against trousers and shoes, which only exist once those are made too;
- the game's cloth shader, which the preview cannot show: every garment needs a look in the game.

That is modelling work per garment, with the route above as its base; none of it is in the bundle or on offer.

## Shoes (shipped)

`scripts\build_shoes.py` builds a shoe as one: a last a few millimetres outside the game's foot, an upper lasted
onto a welted sole with a stepped, grooved midsole wall, a boxed toe, a collar that is an oval round the ankle
with a rolled-in lining, a padded tongue, laces as geometry, and textures painted in the upper's own
(around, up) coordinates, so panels, seams, stitching, perforations and mesh panels sit exactly on the geometry.
One description per style (`STYLES`); both sexes from the same last.

| style | what | colourways |
|---|---|---|
| `court` | low leather court sneaker, perforated quarters, cup sole | white and navy, white and green, black, grey |
| `runner` | knitted mesh under leather overlays, thick toothed midsole | grey and blue, black and red, navy and orange, white |
| `derby` | cap-toe leather derby, thin welted sole, stacked heel | brown, black, tan, oxblood |
| `boot` | laced leather boot to above the ankle: lacing carried up the shaft, six eyelet pairs, padded collar, lug sole (5 964 triangles; the Feet slot ends at 0.226 m, so no taller) | brown, black, wheat, oxblood |
| `loafer` | penny loafer: sewn-in apron, saddle strap with slot, low stacked heel (4 288 triangles) | brown, black, tan, burgundy |

Trousers of ours always take their `Boots` shape when the boot of ours is worn (`ModLooks`: mesh name ending `_boot`).
Built but NOT offered (kept out of the bundle by `make_catalogue.REJECTED`): `flat` (ballet flat on the bare foot:
toe faceted, top line uneven) and `heel` (court shoe built round the skin of the game's pumps, which is a foot already
standing on a 9.5 cm heel: that skin dictates pitch and top line; ours is plainer and more faceted than the game's
pumps). Sheets of all four: `evidence\shoes_<style>.jpg`; plans in `scripts\shoes_plan.py`.

About 4 000 triangles a pair (budget 6 000), one material, one 1024 px normal map per style and one 512 px colour
map per colourway. Colour is in the texture: the mod's cloth materials do not use the game's colour mask, so a
shoe is picked in a colourway on the options page (two dropdowns, men's and women's shoes) instead of tinted in
the game's colour picker. `trial.ps1 shoes_trial` renders each beside the game's nearest shoe close up from three
sides, mid-stride, in every colourway, on the woman's foot and at play distance: `evidence\shoes_<style>.jpg`.

Judged in the preview: level with the game's own shoes in make, and new styles. Not better than the game's laced
boot, which is why there is no boot here: a pull-on ankle boot was tried and was worse (plain, flat) and is gone.
Still open: tread is painted, not modelled; the tongue is a plain padded tab; heels and flats need another sole
construction. In the game the cloth path of `ModLooks` (role `cloth`, `skinFrom`) runs for the first time with
these: if anything is not as it expects, it logs once, shows the game's own shoes and says so in the F9 report.

## What did not work

- **Hairstyles from a sculpted base plus guided strand cards** (round three, `scripts\hairstyle_*.py`, judged only in
  the HDRP preview; sheets `evidence\hairstyle_*.jpg`: the game's finished style beside ours). Four styles (men's
  textured crop and side part, women's long straight and shoulder-length waves), 9 000 to 14 000 triangles, three
  layers each on one generated 1024 px strand atlas. All four rejected. What the method solved: solid at play
  distance (a base surface under the cards), no pale lump per card (cards take their shading normal from the base,
  strands along V, core alpha about 0.97 and `_TransmissionRim` 0 on these layers). What still fails: the hairline (a
  generated sheet ends in a rim; the game's styles end in authored wisps), lock structure close up (even strands read
  as straw), the fall of long hair (stiff panels beside the face), and the women's hat shapes. Next thing to try, if
  anyone does: guides groomed by hand in Blender for one style with the hairline as its own row of short guides, and
  an atlas painted from rendered particle hair. The preview code for wearing such a mesh with real `HDRP/Hair` layers
  is kept as `scripts\hairstyle_PreviewLooks.cs.txt` (a copy of `PreviewLooks.cs` with a `hairstyle:<id>` finish);
  the working copy of the preview project it ran in (`PreviewProject_hair`, `build_hair`) is local and git-ignored.

- **Generated strand-card hair and beards** (round one). Eight hairstyles and three beards built from the
  parked scripts looked acceptable in Blender and bad in the game: under the game's hair shader every card
  lit as its own pale lump. Getting cards to shade as one mass needs flow and normal maps tuned against the
  real shader, which cannot be previewed outside the game. The scripts and the runtime path that swaps the
  game's hair for a mod mesh (`CharacterCatalogue`, `ModLooks`) are kept; nothing is offered to the player.
- **MakeHuman CC0 hair**: wig-like shells, worse than the game's own close up. See `CREDITS.md`.
