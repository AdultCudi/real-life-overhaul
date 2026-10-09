"""What the code relies on in the site module's props (props_site.py), measured on the built meshes by check_life.py.
Kit space: x right, y forward, z up (Unity's x, z, y). The numbers are those of Scripts/Civic/CivicProps.cs (the
stand-ins), Scripts/Civic/SiteDresser.cs (BayWidth 2.4, DeckHeight 2.4, LiftHeight 2.0, pieces of 2 m laid end to end),
Scripts/Civic/CampaignWorld.cs (BoxTop 0.30), Scripts/Justice/Courthouse.cs and Scripts/Law/StationInterior.cs."""
import numpy as np


def run(c):
    print("Site props: what stands on what, and what tiles")

    def box(name, size, tolerance=0.006):
        v = c.kit(name)
        for axis, want in zip("xyz", size):
            if want is not None:
                c.extent(name, "size " + axis, float(np.ptp(v[:, "xyz".index(axis)])), want, tolerance)
        return v

    # The campaign.
    v = box("soapbox", (0.60, 0.45, 0.30), 0.002)
    top = v[v[:, 2] > 0.2985]
    c.extent("soapbox", "top (CampaignWorld.BoxTop)", float(top[:, 2].max()), 0.30, 0.0005)
    c.extent("soapbox", "the top's width", float(np.ptp(top[:, 0])), 0.596, 0.003)
    c.extent("soapbox", "the top's depth", float(np.ptp(top[:, 1])), 0.445, 0.003)
    c.extent("soapbox", "stands on the ground", float(v[:, 2].min()), 0.0, 0.0005)
    for k in range(4):
        v = box("campaign_poster_%d" % k, (0.60, None, 0.85), 0.001)
        c.extent("campaign_poster_%d" % k, "pivot at the middle (z)", float(v[:, 2].max() + v[:, 2].min()), 0.0, 0.001)
        c.extent("campaign_poster_%d" % k, "mount behind the pivot (the code leaves 0.012)", float(-v[:, 1].min()), 0.010, 0.0005)
        c.extent("campaign_poster_%d" % k, "printed side in front of the pivot", float(v[:, 1].max()), 0.007, 0.001)
        box("campaign_board_%d" % k, (0.60, 0.46, 0.93), 0.006)
    box("polling_sign", (0.60, 0.46, 0.93), 0.006)

    # The scaffold: one bay every 2.40 m, a lift on the first deck 2.40 up, then every 2.00.
    for name, height in (("scaffold_bay", 2.40), ("scaffold_bay_upper", 2.00)):
        v = c.kit(name)
        c.extent(name, "height", float(v[:, 2].max()), height, 0.0005)
        c.extent(name, "stands on its pivot's level", float(v[:, 2].min()), 0.0, 0.0005)
        boards = v[(np.abs(v[:, 2] - (height - 0.015)) < 0.016) & (v[:, 1] > 0.085) & (v[:, 1] < 1.715)]
        c.extent(name, "deck top (the code stands people on it 0.80 out)", float(boards[:, 2].max()), height, 0.0005)
        c.extent(name, "deck's width", float(np.ptp(boards[:, 0])), 2.40, 0.001)
        c.extent(name, "deck's wall edge", float(boards[:, 1].min()), 0.09, 0.002)
        c.extent(name, "deck's street edge", float(boards[:, 1].max()), 1.71, 0.002)
        tubes = v[(np.abs(v[:, 2] - height) < 1e-4) & ((v[:, 1] < 0.088) | (v[:, 1] > 1.712))]          # the standards' tops
        c.extent(name, "standards reach x", float(np.abs(tubes[:, 0]).max()), 1.197, 0.003)          # side by side with the next bay's, not through them
        c.extent(name, "street-side standards (the code: 1.74 from the pivot)", float(tubes[tubes[:, 1] > 1.0][:, 1].mean()), 1.74, 0.003)
        c.extent(name, "wall-side standards", float(tubes[tubes[:, 1] < 1.0][:, 1].mean()), 0.06, 0.004)
        ends = v[(np.abs(v[:, 2] - (height - 0.102)) < 0.03) & (np.abs(np.abs(v[:, 0]) - 1.20) < 1e-4)]
        c.report(len(ends) >= 24, "%-19s ledgers end at x +-1.20, where the next bay's begin" % name)
    v = c.kit("scaffold_bay")
    c.extent("scaffold_bay", "nothing behind the wall gap (0.12 m)", float(-v[:, 1].min()), 0.015, 0.003)
    walk = v[(v[:, 2] > 0.2) & (v[:, 2] < 2.0) & (v[:, 1] > 0.15) & (v[:, 1] < 1.65) & (np.abs(v[:, 0]) < 1.1)]
    c.report(len(walk) == 0, "scaffold_bay        the way through along x is clear below 2.0 m (%d vertices in it)" % len(walk))
    v = c.kit("scaffold_bay_upper")
    rail = v[(np.abs(v[:, 2] - 1.00) < 0.03) & (np.abs(np.abs(v[:, 0]) - 1.20) < 1e-4)]
    c.extent("scaffold_bay_upper", "guard rail height", float((rail[:, 2].max() + rail[:, 2].min()) / 2), 1.00, 0.002)
    c.extent("scaffold_bay_upper", "guard rail from the pivot", float((rail[:, 1].max() + rail[:, 1].min()) / 2), 1.692, 0.002)
    v = c.kit("scaffold_net")
    c.extent("scaffold_net", "width", float(np.ptp(v[:, 0])), 2.38, 0.001)
    c.extent("scaffold_net", "height", float(np.ptp(v[:, 2])), 1.96, 0.001)
    c.extent("scaffold_net", "out from the pivot", float(v[:, 1].mean()), 1.80, 0.006)
    c.extent("scaffold_net", "clear of the lift's rails (y 1.716)", float(v[:, 1].min()), 1.795, 0.006)
    v = c.kit("scaffold_fan")
    c.extent("scaffold_fan", "width", float(np.ptp(v[:, 0])), 2.40, 0.001)
    c.extent("scaffold_fan", "outer edge, out", float(v[:, 1].max()), 2.60, 0.02)
    c.extent("scaffold_fan", "outer edge, up", float(v[:, 2].max()), 2.83, 0.02)
    board = v[np.abs(v[:, 0]) > 1.19]
    c.extent("scaffold_fan", "inner edge at the first deck's (y 1.80)", float(board[:, 1].min()), 1.795, 0.012)
    c.extent("scaffold_fan", "inner edge just over the deck (2.40)", float(board[:, 2].min()), 2.393, 0.012)

    # The building site.
    for name in ("site_hoarding", "site_hoarding_notice"):
        v = box(name, (2.40, 0.10, 2.40), 0.002)
        c.extent(name, "nothing further back than (the wall is 0.12 behind)", float(-v[:, 1].min()), 0.06, 0.001)
    v = c.kit("site_hoarding_notice")
    board = v[(v[:, 1] > 0.0385) & (np.abs(v[:, 0]) < 0.61)]
    c.extent("site_hoarding_notice", "the board's width", float(np.ptp(board[:, 0])), 1.20, 0.002)
    c.extent("site_hoarding_notice", "the board's middle, up", float((board[:, 2].max() + board[:, 2].min()) / 2), 1.50, 0.004)
    v = box("site_notice_board", (0.90, 0.45, 1.80), 0.002)
    face = v[(v[:, 1] > 0.0395) & (v[:, 1] < 0.0405)]
    c.extent("site_notice_board", "the board's middle, up", float((face[:, 2].max() + face[:, 2].min()) / 2), 1.45, 0.002)
    c.extent("site_notice_board", "the board's height", float(np.ptp(face[:, 2])), 0.65, 0.008)
    box("skip_bin", (3.40, 1.70, 1.25), 0.003)

    # Road works.
    v = box("road_sign_works", (0.75, 0.60, 1.55), 0.002)
    face = v[(v[:, 1] > 0.0255) & (v[:, 1] < 0.0265) & (v[:, 2] > 0.5)]
    c.extent("road_sign_works", "board's bottom edge", float(face[:, 2].min()), 0.775, 0.002)
    c.extent("road_sign_works", "board's face (the stand-in's is 0.03)", float(face[:, 1].max()), 0.026, 0.001)
    for name, width, high in (("trench_cover", 1.00, 0.020), ("asphalt_patch", 1.20, 0.012)):
        v = box(name, (width, 2.00, high), 0.003)
        c.extent(name, "ends at -1.0 and +1.0 (pieces are laid every 2.00 m)", float(v[:, 1].max() - 1.0) + float(-1.0 - v[:, 1].min()), 0.0, 0.0005)
    v = c.kit("trench_cover")
    for side in (-1, 1):
        ends = [v[(np.abs(v[:, 1] - y) < 1e-4) & (v[:, 0] * side > 0)] for y in (-1.0, 1.0)]
        same = len(ends[0]) == len(ends[1]) and np.allclose(np.sort(ends[0][:, [0, 2]], axis=0), np.sort(ends[1][:, [0, 2]], axis=0), atol=2e-4)
        c.report(same, "trench_cover        the %s bank's two ends are the same section" % ("left" if side < 0 else "right"))
    v = box("road_marking", (0.10, 2.00, None), 0.001)
    c.extent("road_marking", "above the road", float(v[:, 2].min()), 0.004, 0.0005)
    box("spoil_heap", (0.90, 0.90, 0.35), 0.01)
    v = box("works_tent", (1.50, 1.50, 1.90), 0.004)
    inside = v[(np.abs(v[:, 0]) < 0.65) & (np.abs(v[:, 1]) < 0.65) & (v[:, 2] < 1.30)]
    c.report(len(inside) == 0, "works_tent          nothing inside it below 1.3 m: it goes over the open cover (%d vertices)" % len(inside))
    front = v[(v[:, 1] > 0.5) & (np.abs(v[:, 0]) < 0.69) & (v[:, 2] < 1.30)]
    c.report(len(front) == 0, "works_tent          open at +Y up to 1.3 m (%d vertices in the opening)" % len(front))
    box("manhole_open", (0.90, 0.90, 0.06), 0.002)
    box("manhole_lid", (0.66, 0.66, 0.05), 0.002)

    # Justice and the station.
    v = box("courthouse_sign", (1.42, 0.42, 1.95), 0.004)
    board = v[(np.abs(v[:, 1] - 0.025) < 0.0002) & (np.abs(v[:, 0]) < 0.66)]
    c.extent("courthouse_sign", "board's middle, up (the stand-in: 1.52)", float((board[:, 2].max() + board[:, 2].min()) / 2), 1.52, 0.002)
    c.extent("courthouse_sign", "board's width", float(np.ptp(board[:, 0])), 1.30, 0.015)
    v = c.kit("litter_pile")
    c.extent("litter_pile", "furthest piece from the pivot", float(np.hypot(v[:, 0], v[:, 1]).max()), 0.22, 0.02)
    c.extent("litter_pile", "lies on the ground", float(v[:, 2].min()), 0.0, 0.006)
    v = box("office_desk", (1.40, 0.70, None), 0.001)                       # the code stands it by its bounds: the footprint must be the desk's
    c.extent("office_desk", "footprint centred on x", float(v[:, 0].max() + v[:, 0].min()), 0.0, 0.001)
    c.extent("office_desk", "footprint centred on y", float(v[:, 1].max() + v[:, 1].min()), 0.0, 0.001)
    c.extent("office_desk", "top", float(v[(v[:, 2] < 0.7505) & (np.abs(v[:, 0]) > 0.6)][:, 2].max()), 0.75, 0.001)
    v = box("evidence_door", (1.04, None, 2.12), 0.001)
    c.extent("evidence_door", "its back is flat on the wall", float(v[:, 1].min()), 0.0, 0.0005)
    c.extent("evidence_door", "proud of the wall", float(v[:, 1].max()), 0.066, 0.004)
