"""Shot lists for the new hairstyles (the HDRP preview copy, hair_preview.ps1):

    python -I hairstyle_plan.py <plan> <style id> <out.json> [options for the hairstyle finish, e.g. core=0.5,normal=0.8]

    quick   a handful of shots while working: the mod's style at play distance and close up, two colours
    pair    the same beside the game's nearest finished style
    report  the evidence: the game's nearest finished style and ours, four colours on light and dark skin,
            front, side and back at play distance and close up, and under both hats
"""
import json
import sys

from preview_plan import FEMALE, MALE, shot

# style -> the game's nearest style (its finished look is the bar)
NEAREST = {
    "hair_f_long_straight": "FemHair02", "hair_f_waves": "FemHair02",
    "hair_m_crop": "MaleHair07", "hair_m_sidepart": "MaleHair02",
}
COLOURS = (("blond", "light"), ("brown", "light"), ("black", "dark"), ("grey", "dark"))


def who(style):
    return FEMALE if "_f_" in style else MALE


def ours(style, options):
    return ["hairstyle:" + style + (";" + options if options else "")]


def quick(style, options):
    shots = []
    for hair, skin in (("brown", "light"), ("blond", "light")):
        for stage, view, yaw in (("city", "head", 0), ("city", "head", 180), ("creation", "close", 0), ("creation", "close", 110), ("creation", "close", 200)):
            shots.append(shot("q_%s_%s_%s%03d_1mod" % (hair, stage, view, yaw), who(style), None, hair, skin, stage, view, yaw, ours(style, options)))
    return shots


def pair(style, options, colours=(("brown", "light"), ("black", "dark")),
         views=(("city", "head", 0), ("city", "head", 180), ("creation", "close", 0), ("creation", "close", 110), ("creation", "close", 200))):
    shots = []
    for hair, skin in colours:
        for stage, view, yaw in views:
            shots.append(shot("p_%s_%s_%s%03d_0game" % (hair, stage, view, yaw), who(style), NEAREST[style], hair, skin, stage, view, yaw, ["hair"]))
            shots.append(shot("p_%s_%s_%s%03d_1mod" % (hair, stage, view, yaw), who(style), None, hair, skin, stage, view, yaw, ours(style, options)))
    return shots


def probe(style, options):
    """One colour, a few views, one column per variant of the finish options (separated by |)."""
    shots = []
    for k, variant in enumerate(options.split("|")):
        for stage, view, yaw in (("city", "head", 0), ("city", "head", 180), ("creation", "close", 0), ("creation", "close", 200)):
            shots.append(shot("v_brown_%s_%s%03d_%dv" % (stage, view, yaw, k), who(style), None, "brown", "light", stage, view, yaw, ours(style, variant)))
    return shots


def survey(style, options):
    """Every style of the game for this sex, finished: what a new style has to beat (style: any id of that sex)."""
    from preview_plan import FEMALE_HAIR, MALE_HAIR
    shots = []
    for part in (FEMALE_HAIR if "_f_" in style else MALE_HAIR):
        for stage, view, yaw in (("city", "head", 0), ("creation", "close", 0), ("creation", "close", 200)):
            shots.append(shot("s_%s_%s_%s%03d_0game" % (part, stage, view, yaw), who(style), part, "brown", "light", stage, view, yaw, ["hair"]))
    return shots


def report(style, options):
    views = (("city", "head", 0), ("city", "head", 90), ("city", "head", 180), ("creation", "close", 0), ("creation", "close", 110), ("creation", "close", 200))
    shots = pair(style, options, COLOURS, views)
    for hat, shape in (("Hat", "Hat"), ("Half_Hat", "Half_Hat")):
        person = dict(who(style), parts=who(style)["parts"] + ["HeadAccessory/" + hat])
        for stage, view, yaw in (("city", "head", 0), ("city", "head", 180), ("creation", "close", 0), ("creation", "close", 110)):
            shots.append(shot("h_%s_%s_%s%03d_1mod" % (hat.replace("_", ""), stage, view, yaw), person, None, "brown", "light", stage, view, yaw, ours(style, options),
                              shapes={shape: 100}))
    return shots


if __name__ == "__main__":
    plan, style, out = sys.argv[1], sys.argv[2], sys.argv[3]
    options = sys.argv[4] if len(sys.argv) > 4 else ""
    shots = {"quick": quick, "pair": pair, "report": report, "probe": probe, "survey": survey}[plan](style, options)
    with open(out, "w") as f:
        json.dump({"width": 1920, "height": 1080, "shots": shots}, f, indent=1)
    print("plan", plan, style, len(shots), "shots")
