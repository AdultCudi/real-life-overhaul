"""Whole scenes to look at, made of the props as they are exported: previews/scene_<name>.png.

    blender -b -P scenes.py                  every scene whose props exist
    blender -b -P scenes.py -- precinct bus  only these

Positions are said in Unity's axes (x right, y up, z forward: what the READMEs and the mod's code use) and turned into
the kit's here. A wall is a plain grey box, a person a figure of boxes 1.78 m tall ((x, z) on the ground, (x, z, y) standing on a floor y up).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import prop_kit as kit  # noqa: E402
import props  # noqa: E402
import props_service  # noqa: E402


def city_bus(x, z, turn=0.0, open_doors=False, roof=True):
    """The city bus put together as Transport/BusVehicle does it (props_service.bus_parts: the body, its roof, its wheels
    and its door leaves), its middle on the road at (x, z), facing `turn`. Without its roof, as the player riding it sees it from above."""
    a = math.radians(turn)
    things = [("city_bus", (x, 0.0, z), turn)]
    for name, (px, py, pz), spin in props_service.bus_parts(open_doors, roof):
        things.append((name, (x + px * math.cos(a) + py * math.sin(a), pz, z - px * math.sin(a) + py * math.cos(a)), turn - spin))
    return things


# name: (things, walls, people, camera). A thing is (prop id, (x, y, z), turn about the vertical in degrees: 0 faces +Z,
# 90 faces +X). A wall is ((sx, sy, sz), (cx, cy, cz)). A person is ((x, z), facing). Camera: (target, azimuth, elevation,
# distance, fov), azimuth 0 looking at the scene from +Z.
SCENES = {
    "precinct": (
        [("station_awning", (0, 2.35, 0), 0), ("police_shield_emblem", (0, 3.30, 0), 0), ("precinct_sign", (2.4, 0, 0.16), 0),
         ("blue_lamp", (3.26, 2.90, 0.16), 0), ("notice_board", (-1.9, 0.9, 0), 0), ("precinct_bench", (-3.9, 0, 0.30), 0),
         ("bollard", (-3.0, 0, 2.6), 0), ("bollard", (-1.5, 0, 2.6), 0), ("bollard", (1.5, 0, 2.6), 0), ("bollard", (3.0, 0, 2.6), 0),
         ("flag_pole", (-5.4, 0, 1.4), 0), ("street_bin", (1.2, 0, 0.35), 0), ("barrier", (5.6, 0, 1.6), 75)],
        [((13.0, 5.0, 0.3), (-0.5, 2.5, -0.15)), ((1.1, 2.1, 0.06), (0, 1.05, 0.02))],
        [((0.8, 1.2), 200), ((-3.2, 1.4), 150)],
        ((-0.6, 1.9, 0.5), 18, 9, 17.0, 34)),
    "precinct_inside": (
        [("precinct_desk", (0, 0, 0), 0), ("holding_cell_bars", (4.2, 0, -1.2), 0), ("precinct_bench", (-3.2, 0, -1.3), 0),
         ("notice_board", (-3.2, 0.9, -1.75), 0), ("police_shield_emblem", (0, 1.9, -1.75), 0)],
        [((11.0, 3.2, 0.2), (0.5, 1.6, -1.86))],
        [((0.3, -0.9), 0), ((-1.2, 1.6), 180)],
        ((0.4, 1.2, 0), 14, 10, 11.5, 36)),
    "bus_stop": (       # the bus stands with its front door at the flag, its doors open, the kerb on its right
        [("bus_stop_sign", (0.4, 0, -2.6), 0), ("bus_shelter", (1.45, 0, 1.2), 0), ("street_bin", (1.5, 0, -3.3), 0),
         ("bollard", (0.3, 0, 4.2), 0)] + city_bus(-2.45, -6.85, open_doors=True),
        [((0.3, 4.0, 22.0), (3.4, 2.0, -3.5))],
        [((0.5, -0.5), 250), ((1.5, 0.3), 270), ((-0.95, -2.4), 270), ((0.6, -3.9), 300)],
        ((0.0, 1.0, -3.0), 325, 16, 22.0, 38)),
    "fire": (
        [("fire_engine", (-2.6, 0, -7.0), 0), ("street_bin", (0.2, 0, 5.0), 0), ("hose_coil", (-0.8, 0, -3.6), 30), ("hose_nozzle", (0.17, 1.02, 0.46), 0),
         ("cone", (-4.6, 0, -1.0), 0), ("cone", (-4.6, 0, 2.0), 0), ("cone", (-4.6, 0, 5.0), 0), ("tape_post", (2.6, 0, -1.5), 0), ("tape_post", (2.6, 0, 2.5), 0),
         ("tape_4m_sag", (2.6, 0.95, 0.5), 0), ("barrier", (-2.4, 0, 9.0), 0)],
        [((0.3, 6.0, 20.0), (4.4, 3.0, 0))],
        [((0.0, 0.0), 0), ((-0.95, -6.45), 270), ((-3.4, 3.0), 250)],
        ((-1.2, 1.4, -2.5), 305, 16, 30.0, 36)),
    "hotel": (
        [("hotel_awning", (0, 2.75, 0), 0), ("hotel_sign", (1.9, 0, 0.08), 0), ("luggage_cart", (-2.1, 0, 0.55), 0), ("suitcase", (-1.1, 0, 0.45), 20),
         ("street_bin", (3.3, 0, 0.35), 0), ("bollard", (-2.0, 0, 2.6), 0), ("bollard", (2.0, 0, 2.6), 0)],
        [((10.0, 5.0, 0.3), (0, 2.5, -0.15)), ((1.4, 2.3, 0.06), (0, 1.15, 0.02))],
        [((-0.9, 1.3), 160)],
        ((0, 1.7, 0.4), 16, 8, 12.5, 34)),
    "market": (
        [("clothes_rack", (0, 0, 0), 0), ("clothes_rack", (1.9, 0, -0.35), -18), ("shopping_bag", (0.45, 0.38, 0.80), 80), ("shopping_bag", (-0.55, 0.38, 0.95), 15),
         ("moving_box", (-1.5, 0, 0.2), 12), ("gift_box", (-1.5, 0.35, 0.2), -20), ("window_boarded", (-1.6, 0, -1.5), 0), ("employer_sign_4", (0.2, 2.0, -1.5), 0),
         ("closed_notice", (-0.55, 0, -0.9), 10), ("fruit_stand", (4.3, 0, 0.2), -20), ("vendor_cart", (-4.0, 0, 0.4), 15), ("vendor_umbrella", (-4.9, 0, -0.3), 0)],
        [((12.0, 3.0, 0.2), (0.3, 1.5, -1.6))],
        [((0.10, 0.75), 180), ((-4.0, -0.5), 0)],
        ((0.2, 1.0, -0.2), 12, 14, 13.0, 38)),
    "film": (
        [("film_camera", (0, 0, -3.5), 0), ("light_stand", (-2.2, 0, -1.5), 25), ("light_stand", (2.2, 0, -1.5), -25), ("directors_chair", (1.4, 0, -5.6), 0),
         ("firework_rack", (-3.6, 0, 2.6), 20), ("placard", (3.2, 0, 1.2), 180), ("banner_pole", (3.9, 0, 1.0), 180)],
        [],
        [((0, -4.25), 0), ((0.5, -5.6), 0), ((-0.55, 1.6), 90), ((0.55, 1.6), 270), ((3.0, 1.0), 180)],
        ((0.3, 1.1, -1.5), 215, 14, 14.5, 36)),
    "garage": (
        [("bicycle_city", (-3.2, 0, 0), 90), ("bicycle_road", (-1.9, 0, 0), 90), ("motorbike_standard", (0.2, 0, 0), 60), ("motorbike_sport", (2.4, 0, 0), 60),
         ("bike_dock", (-3.6, 0, -1.9), 0), ("bike_dock_kiosk", (-5.6, 0, -1.6), 0), ("helmet", (1.3, 0, 2.0), 30), ("bicycle_helmet", (-2.6, 0, 1.6), 30),
         ("wheel_5spoke", (3.9, 0.34, 0.4), 180), ("wheel_mesh", (3.9, 0.34, 1.2), 180), ("wheel_steel", (3.9, 0.34, 2.0), 180), ("car_roof_bar", (4.4, 0.02, -1.0), 0),
         ("car_bull_bar", (-0.6, 0.0, 2.2), 0), ("car_spoiler", (2.6, 0.02, 2.2), 0), ("car_spoiler_lip", (2.6, 0, 3.0), 0)],
        [((13.0, 3.0, 0.2), (0, 1.5, -2.0))],
        [((1.3, 0.9), 180)],
        ((0.2, 0.7, 0.3), 14, 16, 15.5, 38)),
    "site": (
        [('scaffold_bay', (0, 0, 0), 0), ('scaffold_bay', (2.4, 0, 0), 0), ('scaffold_bay', (4.8, 0, 0), 0), ('scaffold_fan', (0, 0, 0), 0), ('scaffold_fan', (2.4, 0, 0), 0), ('scaffold_fan', (4.8, 0, 0), 0), ('scaffold_bay_upper', (0, 2.4, 0), 0), ('scaffold_bay_upper', (0, 4.4, 0), 0), ('scaffold_bay_upper', (2.4, 2.4, 0), 0), ('scaffold_bay_upper', (2.4, 4.4, 0), 0), ('scaffold_bay_upper', (4.8, 2.4, 0), 0), ('scaffold_bay_upper', (4.8, 4.4, 0), 0), ('scaffold_net', (0, 2.4, 0), 0), ('scaffold_net', (0, 4.4, 0), 0), ('scaffold_net', (2.4, 2.4, 0), 0), ('scaffold_net', (2.4, 4.4, 0), 0), ('scaffold_net', (4.8, 2.4, 0), 0), ('scaffold_net', (4.8, 4.4, 0), 0), ('site_hoarding_notice', (-2.5, 0, 0), 0), ('site_hoarding', (-4.9, 0, 0), 0), ('skip_bin', (2.4, 0, 4.6), 0), ('site_notice_board', (7.3, 0, 1.2), 25), ('step_ladder', (-2.5, 0, 1.06), 180), ('bucket', (-1.9, 0, 0.95), 0), ('tower_crane', (-6, 0, -16), 35)],
        [((14.0, 7.0, 0.3), (1.5, 3.5, -0.27))],
        [((1.0, 0.92), 0), ((8.0, 2.2), 200)],
        ((2, 2.6, 1), 28, 12, 21.0, 40)),
    "road_works": (
        [('trench_cover', (0, 0, -3), 0), ('trench_cover', (0, 0, -1), 0), ('trench_cover', (0, 0, 1), 0), ('trench_cover', (0, 0, 3), 0), ('asphalt_patch', (-2.6, 0, -3), 0), ('asphalt_patch', (-2.6, 0, -1), 0), ('asphalt_patch', (-2.6, 0, 1), 0), ('asphalt_patch', (-2.6, 0, 3), 0), ('road_marking', (2.3, 0, -3), 0), ('road_marking', (3.25, 0, -3), 0), ('road_marking', (2.3, 0, -1), 0), ('road_marking', (3.25, 0, -1), 0), ('road_marking', (2.3, 0, 1), 0), ('road_marking', (3.25, 0, 1), 0), ('road_marking', (2.3, 0, 3), 0), ('road_marking', (3.25, 0, 3), 0), ('spoil_heap', (0, 0, 4.8), 0), ('spoil_heap', (0, 0, -4.8), 40), ('cone', (0.85, 0, -4.4), 0), ('cone', (0.85, 0, -2.2), 0), ('cone', (0.85, 0, 0.0), 0), ('cone', (0.85, 0, 2.2), 0), ('cone', (0.85, 0, 4.4), 0), ('barrier', (0.3, 0, -6.4), 0), ('road_sign_works', (-1.6, 0, -9), 180), ('toolbox', (-1.1, 0, 2.4), 0), ('manhole_open', (6, 0, 0), 0), ('works_tent', (6, 0, 0), 90), ('manhole_lid', (6, 0, 1.25), 0), ('cone', (5.45, 0, -1.15), 0), ('cone', (5.45, 0, 1.15), 0), ('cone', (6.95, 0, -1.15), 0), ('cone', (6.95, 0, 1.15), 0)],
        [],
        [((-1.4, 1.2), 90), ((6.0, -1.9), 0)],
        ((2.4, 0.5, -0.8), 200, 26, 19.0, 40)),
    "campaign": (
        [('soapbox', (0, 0, 0), 0), ('campaign_board_0', (-1.1, 0, -0.9), 0), ('campaign_board_1', (1.1, 0, -0.9), 0), ('campaign_poster_0', (-2.25, 1.55, -1.588), 0), ('campaign_poster_1', (-0.75, 1.55, -1.588), 0), ('campaign_poster_2', (0.75, 1.55, -1.588), 0), ('campaign_poster_3', (2.25, 1.55, -1.588), 0), ('polling_sign', (-3.0, 0, 0.6), -60), ('courthouse_sign', (5.0, 0, -1.3), 0), ('litter_pile', (1.8, 0, 1.4), 0), ('campaign_board_2', (-4.6, 0, -0.9), 0), ('campaign_board_3', (3.2, 0, -0.9), 0), ('campaign_lawn_sign', (-5.6, 0, 0.4), 0)],
        [((13.0, 3.2, 0.2), (0, 1.6, -1.7))],
        [((0, 0), 0), ((-1.15, 2.3), 180), ((0, 2.3), 180), ((1.15, 2.3), 180)],
        ((0, 1.1, -0.2), 18, 10, 13.5, 38)),
    "bus_inside": (   # the bus at the stop as the player boarding it sees it from above: its roof not drawn, somebody in the aisle and at the strap
        [("bus_stop_sign", (0.4, 0, -2.6), 0), ("bus_shelter", (1.45, 0, 1.2), 0)] + city_bus(-2.45, -6.85, open_doors=True, roof=False),
        [((13.0, 3.2, 0.2), (0, 1.6, -1.7))],
        [((-2.30, -4.85, props_service.BUS_FLOOR), 180), ((-2.43, -7.56, props_service.BUS_FLOOR), 0), ((-0.95, -2.40), 270)],
        ((-2.45, 1.0, -6.6), 150, 52, 15.0, 38)),
    "station": (
        [('office_desk', (0, 0, 0), 0), ('office_desk', (2.2, 0, 0), 0), ('evidence_door', (-2.2, 0, -1.0), 0), ('precinct_desk', (5.2, 0, 0.2), 0), ('holding_cell_bars', (-6.0, 0, -0.9), 0), ('precinct_bench', (-6.0, 0, 0.6), 180), ('notice_board', (1.1, 1.0, -1.04), 0)],
        [((14.0, 3.0, 0.2), (-1.0, 1.5, -1.15))],
        [((0, 0.65), 180), ((5.2, -0.7), 0)],
        ((-0.4, 1.1, 0), 12, 12, 13.0, 38)),
    "wedding": (
        [("wedding_arch", (0, 0, 2.0), 180), ("flower_stand", (-1.44, 0, -0.2), 0), ("flower_stand", (1.44, 0, -0.2), 0), ("flower_stand", (-0.86, 0, -2.0), 0),
         ("flower_stand", (0.86, 0, -2.0), 0), ("aisle_runner", (0, 0, -1.8), 0), ("aisle_runner", (0, 0, -2.8), 0), ("aisle_runner", (0, 0, -3.8), 0),
         ("aisle_runner", (0, 0, -4.8), 0), ("aisle_runner", (0, 0, -0.8), 0), ("aisle_runner", (0, 0, 0.2), 0), ("bouquet", (0.75, 1.05, 0.0), 0)],
        [],
        [((0, 1.25), 180), ((-0.55, 0), 90), ((0.55, 0), 270), ((-1.6, -2.6), 0), ((1.6, -2.6), 0), ((-1.6, -3.9), 0)],
        ((0, 1.2, -0.6), 200, 12, 13.0, 36)),
    "nursery": (
        [("cot", (0, 0, 0), 90), ("high_chair", (1.6, 0, 0.1), -20), ("baby_bundle", (0, 0.53, 0), 90), ("birthday_cake", (1.52, 0.83, 0.33), -20),
         ("driving_school_sign", (-2.6, 0, 0.3), 0), ("test_checkpoint_beacon", (-3.8, 0, 0.3), 0), ("learner_plate", (-2.6, 1.6, -0.63), 0),
         ("ring_box", (2.6, 0.0, 0.6), 0), ("parking_box_posts", (6.5, 0, -1.5), 0), ("parking_box_marker", (6.5, 0, -1.5), 0)],
        [((7.0, 2.6, 0.2), (-0.5, 1.3, -0.75))],
        [((0, 0.68), 180)],
        ((0.4, 0.8, 0), 15, 14, 12.0, 38)),
}


def main():
    arguments = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    names = [a for a in arguments if not a.startswith("--")] or list(SCENES)
    wanted = {thing[0] for name in names for thing in SCENES[name][0]}
    made = {prop.name: prop for prop, _ in props.build_all(wanted)}
    os.makedirs(kit.PREVIEWS, exist_ok=True)
    from PIL import Image
    for name in names:
        things, walls, people, (target, azimuth, elevation, distance, fov) = SCENES[name]
        missing = sorted({thing[0] for thing in things} - set(made))
        if len(missing) > len(things) // 3:
            print("scene %s skipped: no %s" % (name, ", ".join(missing)))
            continue
        scene, camera = kit.new_scene()
        for prop, (x, y, z), turn in things:
            if prop in made:
                kit.place(made[prop], (x, z, y), turn=-turn)
        for (sx, sy, sz), (cx, cy, cz) in walls:
            kit.reference_box((sx, sz, sy), (cx, cz, cy), (0.50, 0.47, 0.44))
        for where, facing in people:
            kit.figure((where[0], where[1]), -facing, where[2] if len(where) > 2 else 0.0)
        kit.aim(camera, (target[0], target[2], target[1]), azimuth, elevation, distance, fov=fov)
        path = os.path.join(kit.PREVIEWS, "scene_%s.png" % name)
        kit.render(scene, path, 1280, 720)
        kit.compact(Image.open(path), path)
        print("scene %s%s" % (name, " (without %s)" % ", ".join(missing) if missing else ""))


try:
    main()
except SystemExit:
    raise
except Exception:
    import traceback
    traceback.print_exc()
    sys.exit(1)
