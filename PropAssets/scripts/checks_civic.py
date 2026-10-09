"""What the mod's code and the clips rely on in the civic props, measured on the built meshes (check_life.py calls
run(c) with itself): the seat heights, the suitcase's carry handle, and the faces the code letters on."""
import numpy as np


def run(c):
    print("Civic props: seats, the suitcase's handle, the lettered faces")
    # Seats: 0.46 m up.
    c.extent("precinct_bench", "seat height", c.top_near("precinct_bench", 0.0, 0.03, 0.20, across=0.90, below=0.50), 0.46, 0.0005)
    c.extent("precinct_bench", "length", np.ptp(c.kit("precinct_bench")[:, 0]), 1.80, 0.01)
    c.extent("bus_shelter", "bench seat height", c.top_near("bus_shelter", 0.40, -0.35, 1.00, across=0.19, below=0.60), 0.46, 0.0005)
    v = c.kit("bus_shelter")
    seat = v[(np.abs(v[:, 2] - 0.46) < 0.0005) & (v[:, 0] > 0.15) & (v[:, 0] < 0.62)]
    c.extent("bus_shelter", "bench from y (the slats' edges are chamfered 6 mm)", seat[:, 1].min(), -1.30, 0.007)
    c.extent("bus_shelter", "bench to y", seat[:, 1].max(), 0.60, 0.007)
    c.extent("bus_shelter", "height", v[:, 2].max(), 2.50)
    c.extent("bus_shelter", "length", np.ptp(v[:, 1]), 3.76)
    c.extent("bus_shelter", "depth", np.ptp(v[:, 0]), 1.70)
    # The suitcase: a straight bar along X at (0, 0, 0.630), 0.028 thick, 0.03 clear of the shell under it.
    c.grip("suitcase", (0, 0, 0.630), (1, 0, 0), reach=0.05)
    v = c.kit("suitcase")
    under = v[v[:, 2] < 0.5875]          # the shell and the seam round it; the handle's feet are outside the fingers
    c.extent("suitcase", "clear space under the handle", 0.630 - 0.014 - under[:, 2].max(), 0.031, 0.004)
    c.extent("suitcase", "height", v[:, 2].max(), 0.65, 0.001)
    c.extent("suitcase", "width", np.ptp(v[:, 0]), 0.45, 0.012)
    c.extent("suitcase", "depth", np.ptp(v[:, 1]), 0.25, 0.016)
    # The precinct's sign (Law/Precincts.cs): the lamp's socket on the right post, the board's face behind the name.
    v = c.kit("precinct_sign")
    post = v[(np.abs(v[:, 0] - 0.86) < 0.04) & (np.abs(v[:, 1]) < 0.04)]
    c.extent("precinct_sign", "right post top", post[:, 2].max(), 2.90, 0.0005)
    board = v[(np.abs(np.abs(v[:, 0]) - 0.825) < 0.0005) & ((np.abs(v[:, 2] - 2.05) < 0.0005) | (np.abs(v[:, 2] - 2.85) < 0.0005))]
    c.extent("precinct_sign", "board face (the name is lettered at 0.027)", board[:, 1].max(), 0.025, 0.0005)
    c.extent("precinct_sign", "board bottom", board[:, 2].min(), 2.05, 0.0005)
    c.extent("precinct_sign", "board top", board[:, 2].max(), 2.85, 0.0005)
    c.extent("precinct_sign", "nothing behind", v[:, 1].min(), -0.04, 0.0015)
    c.extent("precinct_sign", "width", np.ptp(v[:, 0]), 1.80, 0.002)
    v = c.kit("blue_lamp")
    c.extent("blue_lamp", "height", v[:, 2].max(), 0.27, 0.0005)
    c.extent("blue_lamp", "across", np.ptp(v[:, 0]), 0.20, 0.002)
    # The bus stop's flag (Transport/BusStopDressing.cs letters at y +-0.019, at x 0.30, 2.50 m up).
    v = c.kit("bus_stop_sign")
    flag = v[(v[:, 0] > 0.045) & (v[:, 2] > 2.0) & (np.abs(v[:, 1]) < 0.0151)]
    c.extent("bus_stop_sign", "flag face", flag[:, 1].max(), 0.015, 0.0005)
    c.extent("bus_stop_sign", "flag middle x", (flag[:, 0].max() + flag[:, 0].min()) / 2, 0.30, 0.001)
    c.extent("bus_stop_sign", "flag middle height", (flag[:, 2].max() + flag[:, 2].min()) / 2, 2.42, 0.001)
    c.extent("bus_stop_sign", "height", v[:, 2].max(), 2.85, 0.0005)
    # The hotel's sign (Travel/Hotels.cs letters at y 0.085, 1.50 m up, in a box 0.60 x 0.34).
    v = c.kit("hotel_sign")
    plate = v[(v[:, 2] > 1.32) & (v[:, 2] < 1.68) & (v[:, 1] > 0.075)]
    c.extent("hotel_sign", "name plate face", plate[:, 1].max(), 0.080, 0.0005)
    c.extent("hotel_sign", "name plate width", np.ptp(plate[:, 0]), 0.60, 0.006)
    c.extent("hotel_sign", "name plate middle", (plate[:, 2].max() + plate[:, 2].min()) / 2, 1.50, 0.001)
    c.extent("hotel_sign", "height", v[:, 2].max(), 1.90, 0.002)
    c.extent("hotel_sign", "nothing in front of the plate", v[:, 1].max(), 0.080, 0.0005)
    for name, wide, deep in (("hotel_awning", 2.40, 1.40), ("station_awning", 2.60, 1.40)):
        v = c.kit(name)
        c.extent(name, "width", np.ptp(v[:, 0]), wide, 0.01)
        c.extent(name, "reach", v[:, 1].max(), deep, 0.005)
        c.extent(name, "underside at the pivot", v[:, 2].min(), 0.0, 0.001)
        c.extent(name, "wall edge at the pivot", v[:, 1].min(), 0.0, 0.001)
    v = c.kit("street_bin")
    c.extent("street_bin", "height", v[:, 2].max(), 0.90, 0.0005)
    c.extent("street_bin", "across", np.ptp(v[:, 0]), 0.54, 0.004)
