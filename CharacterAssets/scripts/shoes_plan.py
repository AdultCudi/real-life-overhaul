"""Preview plans for the second round of shoes (boot, loafer, flats, heels): python -I shoes_plan.py <plan> <out.json>

    ref                 the game's own shoes these are measured against
    <style>             the evidence sheet of one style (as preview_plan.shoes_trial lays it out)
    <style>_clip        the same shoe standing, walking and running under every hem it has to clear
    <style>_quick       a handful of shots while working
"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from preview_plan import BUILDS, FEMALE, MALE, RUN, STRIDE, shot  # noqa: E402

# style -> (the game's nearest shoe: compared against, and its skin part shows the bare ankle; colourways; sexes)
SHOES2 = {
    "boot": ("Boots", ("brown", "black", "tan", "oxblood"), "mf"),
    "loafer": ("Shoes", ("brown", "black", "tan", "oxblood"), "mf"),
    "flat": ("NakedFeet", ("black", "nude", "red", "navy"), "f"),
    "heel": ("SuitShoes", ("black", "nude", "red", "navy"), "f"),
}
# What the game's nearest shoe is when the skin comes from another piece (the flats sit on the bare foot).
COMPARE = {"flat": "SuitShoes"}


def person(sex, game, legs="ShortJeans"):
    if sex == "m":
        return dict(MALE, parts=["Eyebrows/Eyebrows14", "Torso/Shirt", "Legs/" + legs, "Feet/" + game], cloth={"Torso": "6FA83A", "Legs": "3A4A66"}), "MaleHair04"
    return dict(FEMALE, parts=["Eyebrows/Eyebrows04", "Torso/Shirt", "Legs/" + legs, "Feet/" + game], cloth={"Torso": "7D3F3E", "Legs": "3A4A66"}), "FemHair02"


def add(shots, name, sex, game, finish, view="feet", yaw=20, legs="ShortJeans", stage="creation", **more):
    who, hair = person(sex, game, legs)
    shots.append(shot(name, who, hair, "brown", "light", stage, view, yaw, finish, **more))


def ref():
    shots = []
    for sex, games in (("m", ("Boots", "SuitShoes", "Shoes", "NakedFeet")), ("f", ("Boots", "SuitShoes", "Shoes", "NakedFeet"))):
        for game in games:
            for yaw in (20, 100, 200):
                add(shots, "ref_%s_%s_%03d" % (sex, game, yaw), sex, game, [], yaw=yaw)
    return shots


def style_sheet(style):
    game, colours, sexes = SHOES2[style]
    compare = COMPARE.get(style, game)
    first = sexes[0]
    worn = ["wear:feet_%s_%s_%s" % (first, style, colours[0])]
    shots = []
    for yaw in (20, 100, 200):
        add(shots, "%02d_game_%d" % (len(shots), yaw), first, compare, [], yaw=yaw)
    for yaw in (20, 100, 200):
        add(shots, "%02d_mod_%d" % (len(shots), yaw), first, game, worn, yaw=yaw)
    add(shots, "%02d_mod_stride" % len(shots), first, game, worn, yaw=60, pose=STRIDE)
    add(shots, "%02d_mod_run" % len(shots), first, game, worn, yaw=250, pose=RUN)
    for colour in colours[1:]:
        add(shots, "%02d_%s" % (len(shots), colour), first, game, ["wear:feet_%s_%s_%s" % (first, style, colour)])
    if len(sexes) > 1:
        other = ["wear:feet_f_%s_%s" % (style, colours[0])]
        for yaw in (20, 200):
            add(shots, "%02d_woman_%d" % (len(shots), yaw), "f", game, other, yaw=yaw)
    add(shots, "%02d_city_game" % len(shots), first, compare, [], view="game", yaw=0, stage="city")
    add(shots, "%02d_city_mod" % len(shots), first, game, worn, view="game", yaw=0, stage="city")
    return shots


# Shoes with a shaft: the trousers go inside it, by the "Boots" shape they carry for the game's own tall boots.
TUCKED = ("boot",)
POSES = (("stand", None), ("walk", STRIDE), ("run", RUN))


def clip(style, only=None):
    """Standing, walking and running: bare leg, then under every hem of the mod the shoe can meet."""
    game, colours, sexes = SHOES2[style]
    shots = []
    for sex in sexes:
        worn = "wear:feet_%s_%s_%s" % (sex, style, colours[0])
        outfits = [("bare", "ShortJeans", [])]
        for legs in ("jeans_indigo", "chinos_khaki", "suit_charcoal"):
            outfits.append((legs.split("_")[0], "Pants", ["wear:legs_%s_%s" % (sex, legs)]))
        if sex == "f":
            outfits.append(("dress", "FormalSkirt", ["wear:dress_f_summer_navy", "wear:skirt_f_summer_navy"]))
            outfits.append(("pencil", "FormalSkirt", ["wear:legs_f_pencil_charcoal"]))
        for label, legs, extra in outfits:
            if only and label not in only:
                continue
            for pose_name, pose in POSES:
                for yaw in (70, 250):
                    view = "feet" if label == "bare" else "hem"
                    more = {"pose": pose} if pose else {}
                    if style in TUCKED and legs == "Pants":
                        more["shapes"] = {"Boots": 100}
                    add(shots, "%s_%s_%s_%s_%d" % (sex, label, pose_name, style, yaw), sex, game, extra + [worn], view=view, yaw=yaw, legs=legs, **more)
    return shots


def quick(style):
    game, colours, sexes = SHOES2[style]
    first = sexes[0]
    worn = ["wear:feet_%s_%s_%s" % (first, style, colours[0])]
    shots = []
    for yaw in (20, 100, 200, 300):
        add(shots, "q%d_mod_%03d" % (len(shots), yaw), first, game, worn, yaw=yaw)
    add(shots, "q%d_stride" % len(shots), first, game, worn, yaw=60, pose=STRIDE)
    add(shots, "q%d_run" % len(shots), first, game, worn, yaw=250, pose=RUN)
    return shots


if __name__ == "__main__":
    name, out = sys.argv[1], sys.argv[2]
    if name == "ref":
        shots = ref()
    elif name == "refheel":
        shots = [s for s in ref() if "f_SuitShoes" in s["name"]]
        add(shots, "ref_f_SuitShoes_stride", "f", "SuitShoes", [], yaw=60, pose=STRIDE)
        add(shots, "ref_f_SuitShoes_run", "f", "SuitShoes", [], yaw=250, pose=RUN)
        add(shots, "ref_f_SuitShoes_300", "f", "SuitShoes", [], yaw=300)
    elif name == "boot_back":
        # The back of the leg above the collar, the game's boot against ours, under the tucked trousers.
        shots = []
        for sex, legs in (("m", "suit_charcoal"), ("f", "jeans_indigo"), ("f", "chinos_khaki")):
            for label, feet in (("game", []), ("mod", ["wear:feet_%s_boot_brown" % sex])):
                for yaw in (70, 180, 250):
                    add(shots, "%s_%s_%s_%d" % (sex, legs.split("_")[0], label, yaw), sex, "Boots", ["wear:legs_%s_%s" % (sex, legs)] + feet, view="feet", yaw=yaw,
                        legs="Pants", shapes={"Boots": 100})
    elif name == "builds":
        # The three builds of the body (the shoes carry the foot's Fat, Slim and Musc shapes) on a bare leg.
        shots = []
        for style, (game, colours, sexes) in SHOES2.items():
            for sex in sexes:
                for build, shapes in BUILDS:
                    add(shots, "%s_%s_%s" % (style, sex, build), sex, game, ["wear:feet_%s_%s_%s" % (sex, style, colours[0])], yaw=100, shapes=shapes)
    elif name.endswith("_clip"):
        shots = clip(name[:-5])
    elif "_clip_" in name:                       # boot_clip_bare,dress: only those outfits
        style, _, only = name.partition("_clip_")
        shots = clip(style, only.split(","))
    elif name.endswith("_quick"):
        shots = quick(name[:-6])
    else:
        shots = style_sheet(name)
    with open(out, "w") as f:
        json.dump({"width": 1920, "height": 1080, "shots": shots}, f, indent=1)
    print("plan", name, len(shots), "shots")
