"""Writes the shot lists (plans) the HDRP preview renders: python -I preview_plan.py <plan name> <out.json>

A shot = who (sex, parts, colours), where (stage: city or creation), from where (yaw: the character turns,
the camera stays where the game has it) and which looks ("finish": the mod's treatments, none = the game's own).
"""
import json
import sys

HAIR = {"blond": "D9B36A", "brown": "5A3A22", "black": "1A1614", "grey": "9A9A98", "ginger": "92532F", "sand": "AB7E55"}
SKIN = {"light": "D9A07F", "dark": "6A2F20", "pale": "F0C8AA", "mid": "A8704C"}

MALE = {"sex": "Male", "parts": ["Eyebrows/Eyebrows14", "Torso/Shirt", "Legs/CargoPants_Camuflage", "Feet/Sneakers03"],
        "cloth": {"Torso": "6FA83A"}}
FEMALE = {"sex": "Female", "parts": ["Eyebrows/Eyebrows04", "Torso/HighNeckShirt", "Legs/Pants", "Feet/Sneakers"],
          "cloth": {"Torso": "7D3F3E"}}
MALE_HAIR = ["MaleHair01", "MaleHair02", "MaleHair03", "MaleHair04", "MaleHair05", "MaleHair06", "MaleHair07", "MaleHair08"]
FEMALE_HAIR = ["FemHair01", "FemHair02", "FemHair03", "FemHair04", "FemHair05", "FemHair06"]

CROPS = {"game": [300, 560], "head": [200, 200], "close": [1000, 1000]}


def shot(name, who, hair_part, hair, skin, stage, view, yaw=0, finish=None, beard=None, **more):
    parts = list(who["parts"]) + (["Hair/" + hair_part] if hair_part else []) + (["Beard/" + beard] if beard else [])
    entry = {"name": name, "sex": who["sex"], "parts": parts, "hair": HAIR.get(hair, hair), "skin": SKIN.get(skin, skin),
             "stage": stage, "view": view, "yaw": yaw, "crop": CROPS[view], "cloth": who["cloth"],
             "finish": finish or []}
    entry.update(more)
    return entry


def calibration():
    """The player's own screenshots, re-staged with the game's unmodified look."""
    sand = dict(hair="sand", skin="light")
    return [
        shot("city_game", MALE, "MaleHair06", stage="city", view="game", beard="MaleBeard05", **sand),
        shot("city_head", MALE, "MaleHair06", stage="city", view="head", beard="MaleBeard05", **sand),
        shot("creation_close", dict(MALE, parts=["Eyebrows/Eyebrows", "Torso/HighNeckShirt", "Legs/ShortJeans", "Feet/SportSneakers"],
                                    cloth={"Torso": "7D3F3E"}),
             "MaleHair05", "ginger", "dark", "creation", "close", yaw=0, beard="MaleBeard06"),
    ]


def matrix(prefix, finishes, hairs, who, colours=(("blond", "light"), ("brown", "light"), ("black", "dark"), ("grey", "dark")),
           views=(("city", "head", 0), ("city", "head", 180), ("creation", "close", 0), ("creation", "close", 110)), beard=None):
    shots = []
    for label, finish in finishes:
        for hair_part in hairs:
            for hair, skin in colours:
                for stage, view, yaw in views:
                    shots.append(shot("%s_%s_%s_%s_%s%d_%s" % (prefix, hair_part or "none", hair, stage, view, yaw, label),
                                      who, hair_part, hair, skin, stage, view, yaw, finish, beard))
    return shots


def hair_trial():
    """Working plan: whatever is being tried at the moment."""
    finishes = (("0game", []), ("1finished", ["hair"]))
    return matrix("m", finishes, ["MaleHair05"], MALE, (("sand", "light"), ("black", "dark")), (("city", "head", 0), ("creation", "close", 0)))


def hair_all():
    """Every hairstyle of the game, its own look against the finished one, light and dark, front and back."""
    finishes = (("0game", []), ("1finished", ["hair"]))
    colours = (("sand", "light"), ("black", "dark"))
    views = (("city", "head", 0), ("city", "head", 180), ("creation", "close", 0))
    return matrix("m", finishes, MALE_HAIR, MALE, colours, views) + matrix("f", finishes, FEMALE_HAIR, FEMALE, colours, views)


def hair_report():
    """The sheets kept in evidence: four styles, four colours on light and dark skin, the game's look against the finish."""
    finishes = (("0game", []), ("1finished", ["hair"]))
    views = (("city", "head", 0), ("city", "head", 180), ("creation", "close", 0), ("creation", "close", 110))
    return (matrix("m", finishes, ["MaleHair05", "MaleHair01"], MALE, views=views, beard="MaleBeard02")
            + matrix("f", finishes, ["FemHair03", "FemHair02"], FEMALE, views=views))


def beard_report():
    finishes = (("0game", []), ("1finished", ["hair"]))
    views = (("city", "head", 0), ("creation", "close", 0), ("creation", "close", 60))
    shots = []
    for beard in ("MaleBeard01", "MaleBeard02", "MaleBeard04", "MaleBeard05", "MaleBeard06"):
        for hair, skin in (("brown", "light"), ("black", "dark"), ("grey", "light")):
            for label, finish in finishes:
                for stage, view, yaw in views:
                    shots.append(shot("b_%s_%s_%s_%s%d_%s" % (beard, hair, stage, view, yaw, label), MALE, "MaleHair04", hair, skin,
                                      stage, view, yaw, finish, beard))
    return shots


CROPS.update({"torso": [900, 900]})


def wardrobe_trial():
    """A garment of the mod against the game's nearest equivalent: play distance and close up, front, side and back."""
    shots = []
    who = dict(MALE, parts=["Eyebrows/Eyebrows14", "Torso/Sweater", "Legs/Pants", "Feet/Sneakers03"], cloth={"Torso": "7A2E2E", "Legs": "3A4A66"})
    for label, finish in (("0game", []), ("1mod", ["wear:top_m_knit"])):
        for stage, view, yaws in (("city", "game", (0, 90, 180)), ("creation", "torso", (0, 60, 180))):
            for yaw in yaws:
                shots.append(shot("w_knit_%s_%s%d_%s" % (stage, view, yaw, label), who, "MaleHair04", "brown", "light", stage, view, yaw, finish))
    return shots


CROPS.update({"feet": [900, 700]})
STRIDE = {"UpperLeg.L": [-28, 0, 0], "LowerLeg.L": [35, 0, 0], "Foot.L": [-12, 0, 0], "UpperLeg.R": [22, 0, 0], "LowerLeg.R": [10, 0, 0],
          "Foot.R": [18, 0, 0], "ToesFoot.R": [-30, 0, 0]}


SHOES = {"court": ("Sneakers03", ("white", "green", "black", "grey")), "runner": ("Sneakers03", ("grey", "black", "navy", "white")),
         "derby": ("Shoes", ("brown", "black", "tan", "oxblood"))}


def shoes_trial():
    """Every shoe of the mod: beside the game's nearest close up from three sides and mid-stride, its other colourways,
    on the woman's foot, and at play distance."""
    shots = []
    for style, (game, colours) in SHOES.items():
        man = dict(MALE, parts=["Eyebrows/Eyebrows14", "Torso/Shirt", "Legs/ShortJeans", "Feet/" + game], cloth={"Torso": "6FA83A", "Legs": "3A4A66"})
        woman = dict(FEMALE, parts=["Eyebrows/Eyebrows04", "Torso/HighNeckShirt", "Legs/ShortJeans", "Feet/" + game], cloth={"Torso": "7D3F3E", "Legs": "3A4A66"})
        first = ["wear:feet_m_%s_%s" % (style, colours[0])]
        for label, finish in (("0game", []), ("1mod", first)):
            for yaw in (20, 100, 200):
                shots.append(shot("s_%s_creation_feet%d_%s" % (style, yaw, label), man, "MaleHair04", "brown", "light", "creation", "feet", yaw, finish))
        shots.append(shot("s_%s_creation_feet61_1mod" % style, man, "MaleHair04", "brown", "light", "creation", "feet", 60, first, pose=STRIDE))
        for k, colour in enumerate(colours[1:]):
            shots.append(shot("s_%s_creation_feet%d_2%s" % (style, 21 + k, colour), man, "MaleHair04", "brown", "light", "creation", "feet", 20,
                              ["wear:feet_m_%s_%s" % (style, colour)]))
        for yaw in (25, 205):
            shots.append(shot("s_%s_creation_feet%d_3woman" % (style, yaw), woman, "FemHair02", "brown", "light", "creation", "feet", yaw - 5,
                              ["wear:feet_f_%s_%s" % (style, colours[0])]))
        shots.append(shot("s_%s_city_game0_1mod" % style, man, "MaleHair04", "brown", "light", "city", "game", 0, first))
    return shots


# ---------------------------------------------------------------- garments (scripts/build_garments.py)

CROPS.update({"legs": [620, 1000], "body": [620, 1000], "hem": [800, 620]})
RUN = {"UpperLeg.L": [-52, 0, 0], "LowerLeg.L": [72, 0, 0], "Foot.L": [-8, 0, 0], "UpperLeg.R": [28, 0, 0], "LowerLeg.R": [38, 0, 0],
       "Foot.R": [24, 0, 0], "ToesFoot.R": [-30, 0, 0], "Spine": [8, 0, 0]}
SIT = {"UpperLeg.L": [-90, 0, 0], "LowerLeg.L": [90, 0, 0], "UpperLeg.R": [-90, 0, 0], "LowerLeg.R": [90, 0, 0]}
ELBOWS = {"LowerArm.L": [0, 0, -90], "LowerArm.R": [0, 0, 90]}
BUILDS = (("fat", {"Fat": 100, "fat": 100}), ("slim", {"Slim": 100, "slim": 100}), ("musc", {"Musc": 100, "musc": 100, "Muscular": 100}))

# id -> (who, the game's nearest piece, the game parts worn with it, colourways, view, what else of the mod is worn in the poses)
GARMENTS = {
    "legs_m_jeans": dict(sex="m", slot="Legs", game="Pants", parts=["Torso/Shirt", "Feet/Sneakers03"], view="legs",
                         colours=("indigo", "navy", "sky", "black", "grey"), tint={"Legs": "3A4A66", "Torso": "B8B4AA"},
                         shoes=("court_white", "runner_grey", "derby_brown"), boots=True),
    "top_m_tee": dict(sex="m", slot="Torso", game="Shirt", parts=["Legs/Pants", "Feet/Sneakers03"], view="torso",
                      colours=("white", "black", "heather", "navy", "olive"), tint={"Legs": "3A4A66", "Torso": "B8B4AA"},
                      with_=("legs_m_jeans_indigo", "feet_m_court_white")),
    "top_m_bomber": dict(sex="m", slot="Torso", game="Sweater", parts=["Legs/Pants", "Feet/Sneakers03"], view="torso",
                         colours=("sage", "black", "navy", "burgundy", "tan"), tint={"Legs": "3A4A66", "Torso": "4A5A3A"},
                         with_=("legs_m_jeans_indigo", "feet_m_court_white")),
    "legs_f_jeans": dict(sex="f", slot="Legs", game="Pants", parts=["Torso/Shirt", "Feet/Sneakers03"], view="legs",
                         colours=("indigo", "navy", "sky", "black", "grey"), tint={"Legs": "3A4A66", "Torso": "B8B4AA"},
                         shoes=("court_white", "runner_grey", "derby_brown"), boots=True),
    "top_f_tee": dict(sex="f", slot="Torso", game="Shirt", parts=["Legs/Pants", "Feet/Sneakers03"], view="torso",
                      colours=("white", "black", "blush", "teal", "mustard"), tint={"Legs": "3A4A66", "Torso": "B8B4AA"},
                      with_=("legs_f_jeans_indigo", "feet_f_court_white")),
    "legs_m_chinos": dict(sex="m", slot="Legs", game="Pants", parts=["Torso/Shirt", "Feet/Sneakers03"], view="legs",
                          colours=("tan", "stone", "navy", "olive", "charcoal", "rust"), tint={"Legs": "8A7A5A", "Torso": "B8B4AA"},
                          shoes=("court_white", "runner_grey", "derby_brown"), boots=True),
    "legs_m_suit": dict(sex="m", slot="Legs", game="Pants", parts=["Torso/Shirt", "Feet/Sneakers03"], view="legs",
                        colours=("charcoal", "navy", "black", "grey", "tan"), tint={"Legs": "2A2A30", "Torso": "B8B4AA"},
                        shoes=("derby_black", "court_white", "runner_grey"), boots=True),
    "legs_f_chinos": dict(sex="f", slot="Legs", game="Pants", parts=["Torso/Shirt", "Feet/Sneakers03"], view="legs",
                          colours=("tan", "stone", "navy", "olive", "charcoal", "rust"), tint={"Legs": "8A7A5A", "Torso": "B8B4AA"},
                          shoes=("court_white", "runner_grey", "derby_brown"), boots=True),
    "legs_f_suit": dict(sex="f", slot="Legs", game="Pants", parts=["Torso/Shirt", "Feet/Sneakers03"], view="legs",
                        colours=("charcoal", "navy", "black", "grey", "tan"), tint={"Legs": "2A2A30", "Torso": "B8B4AA"},
                        shoes=("derby_black", "court_white", "runner_grey"), boots=True),
    "legs_f_pencil": dict(sex="f", slot="Legs", game="FormalSkirt", parts=["Torso/Shirt", "Feet/Sneakers03"], view="body",
                          colours=("charcoal", "navy", "black", "grey", "tan"), tint={"Legs": "2A2A30", "Torso": "B8B4AA"},
                          with_=("feet_f_court_white", "top_f_tee_white")),
    "dress_f_evening": dict(sex="f", slot="Torso", game="Shirt", parts=["Legs/FormalSkirt", "Feet/Sneakers03"], view="body",
                            colours=("black", "emerald", "burgundy", "navy", "cream", "red"), tint={"Legs": "2A2A30", "Torso": "B8B4AA"},
                            pair="skirt_f_evening", with_=("feet_f_derby_black",)),
    # A dress is a bodice and a skirt: shown beside the game's shirt and formal skirt, which also lend it their bare arms and legs.
    "dress_f_summer": dict(sex="f", slot="Torso", game="Shirt", parts=["Legs/FormalSkirt", "Feet/Sneakers03"], view="body",
                           colours=("dots", "dotsred", "sage", "finedots", "black"), tint={"Legs": "2A2A30", "Torso": "B8B4AA"},
                           pair="skirt_f_summer", with_=("feet_f_court_white",)),
}


# Further families of garments keep their entries in files of their own (plans_<family>.py beside this one, each
# with a GARMENTS dict of the same form), as their descriptions are in garments_<family>.py.
def _families():
    import glob
    import importlib
    import os
    here = os.path.dirname(os.path.abspath(__file__))
    if here not in sys.path:
        sys.path.insert(0, here)      # this script is run with -I, which leaves its folder out
    for path in sorted(glob.glob(os.path.join(here, "plans_*.py"))):
        try:
            GARMENTS.update(importlib.import_module(os.path.splitext(os.path.basename(path))[0]).GARMENTS)
        except Exception as error:      # a family that is being worked on must not stop the others' sheets
            print("plans of %s not loaded: %r" % (os.path.basename(path), error), file=sys.stderr)


_families()


def garment(garment_id):
    """One garment's evidence: the game's nearest piece and the mod's from three sides, the poses, the hem on each shoe
    (trousers), the builds, every colourway, and the play distance."""
    g = GARMENTS[garment_id]
    base = MALE if g["sex"] == "m" else FEMALE
    hair = "MaleHair04" if g["sex"] == "m" else "FemHair02"
    who = dict(base, parts=[base["parts"][0]] + g["parts"] + ["%s/%s" % (g["slot"], g["game"])], cloth=g["tint"])
    view = g["view"]

    def worn(colour):
        return ["wear:%s_%s" % (garment_id, colour)] + (["wear:%s_%s" % (g["pair"], colour)] if g.get("pair") else [])
    shots = []

    def add(label, finish, yaw=0, stage="creation", shot_view=None, **more):
        shot_view = shot_view or view
        more = {k: v for k, v in more.items() if v is not None}
        shots.append(shot("%02d_%s" % (len(shots), label), who, hair, "brown", "light", stage, shot_view, yaw, finish, **more))
    for yaw, side in ((0, "front"), (90, "side"), (180, "back")):
        add("game_" + side, [], yaw)
        add("mod_" + side, worn(g["colours"][0]), yaw)
    shoe = ["wear:feet_%s_%s" % (g["sex"], g["shoes"][0])] if g.get("shoes") else ["wear:" + other for other in g.get("with_", ())]
    poses = (("walk", STRIDE, 68), ("run", RUN, 68), ("sit", SIT, 68))
    if g["slot"] != "Legs":
        poses += (("arms_up", None, 0), ("elbows", ELBOWS, 40))
    for name, pose, arms in poses:
        for yaw in (50, 230) if name != "sit" else (40, 100):
            add("%s_%d" % (name, yaw), worn(g["colours"][0]) + shoe, yaw, pose=pose, arms=arms)
    for style in g.get("shoes", ()):
        for name, pose in (("stand", None), ("walk", STRIDE), ("run", RUN)):
            add("%s_%s" % (style.split("_")[0], name), worn(g["colours"][0]) + ["wear:feet_%s_%s" % (g["sex"], style)], 70, shot_view="hem", pose=pose)
    if g.get("boots"):
        # The game's tall boots: its trousers have a "Boots" shape that draws the hem in; ours follows it.
        booted = dict(who, parts=[p for p in who["parts"] if not p.startswith("Feet/")] + ["Feet/Boots"])
        for name, pose in (("stand", None), ("walk", STRIDE)):
            shots.append(shot("%02d_boots_%s" % (len(shots), name), booted, hair, "brown", "light", "creation", "hem", 70, worn(g["colours"][0]),
                              shapes={"Boots": 100}, **({"pose": pose} if pose else {})))
    for name, shapes in BUILDS:
        add("build_" + name, worn(g["colours"][0]) + shoe, 25, shapes=shapes)
    for colour in g["colours"][1:]:
        add("colour_" + colour, worn(colour) + shoe, 25)
    add("city_game", [], 0, "city", "game")
    add("city_mod", worn(g["colours"][0]) + shoe, 0, "city", "game")
    return shots


def catalogue_colours(garment_id):
    """The colourways a garment has in the build, in the catalogue's (the palette's) order."""
    import os
    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "build", "catalogue_garments.json")
    with open(path) as f:
        items = json.load(f)["items"]
    return [item["id"][len(garment_id) + 1:] for item in items if item["mesh"] == garment_id]


def palette(garment_id, colours=None, view=None, yaw=25, city=False):
    """A garment's palette: every colourway on the model from the front three-quarter view (small tiles on one sheet).
    colours: only these; view: another framing ("torso", "legs", "body": close-ups of a print); city: the play distance."""
    g = GARMENTS[garment_id]
    base = MALE if g["sex"] == "m" else FEMALE
    hair = "MaleHair04" if g["sex"] == "m" else "FemHair02"
    who = dict(base, parts=[base["parts"][0]] + g["parts"] + ["%s/%s" % (g["slot"], g["game"])], cloth=g["tint"])
    other = ["wear:feet_%s_%s" % (g["sex"], g["shoes"][0])] if g.get("shoes") else ["wear:" + o for o in g.get("with_", ())]
    shots = []
    for colour in colours or catalogue_colours(garment_id):
        worn = ["wear:%s_%s" % (garment_id, colour)] + (["wear:%s_%s" % (g["pair"], colour)] if g.get("pair") else [])
        stage, shot_view = ("city", "game") if city else ("creation", view or g["view"])
        shots.append(shot("%02d_%s" % (len(shots), colour), who, hair, "brown", "light", stage, shot_view, yaw, worn + other))
    return shots


PLANS = {
    "shoes_trial": shoes_trial,
    "wardrobe_trial": wardrobe_trial,
    "hair_report": hair_report,
    "beard_report": beard_report,
    "calibration": calibration,
    "hair_trial": hair_trial,
    "hair_all": hair_all,
}

if __name__ == "__main__":
    name, out = sys.argv[1], sys.argv[2]
    only = sys.argv[3].split(",") if len(sys.argv) > 3 and sys.argv[3] not in ("", "-") else None      # a garment plan cut down to shots whose label starts so
    if name.startswith("palette_"):
        # palette_<garment id> [colours] [view or "city"] [yaw]: the palette sheet, or close-ups of some colourways
        view = sys.argv[4] if len(sys.argv) > 4 and sys.argv[4] != "-" else None
        shots = palette(name[8:], only, None if view == "city" else view, int(sys.argv[5]) if len(sys.argv) > 5 else 25, view == "city")
        only = None
    else:
        shots = garment(name) if name in GARMENTS else PLANS[name]()
    if only:
        shots = [s for s in shots if any(s["name"][3:].startswith(o) for o in only)]
    with open(out, "w") as f:
        json.dump({"width": 1920, "height": 1080, "shots": shots}, f, indent=1)
    print("plan", name, len(shots), "shots")
