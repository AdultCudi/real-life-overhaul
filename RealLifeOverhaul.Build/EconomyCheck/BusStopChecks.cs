using System;
using RealLifeOverhaul;

// The street at a bus stop (Scripts/Transport/BusBay, BusInside, BusZones, TransitMap): which of the game's parked cars and
// parking spots a bay's zone takes, where the stop's pole stands against the doors and the ways to them, and where the bus
// stands so that neither door lines up with what stays on the kerb.
internal static partial class Program
{
    private static void BusStopStreet()
    {
        // A parked car against a zone and its margin.
        const float from = -29f, to = 22f, m = BusBay.CarMargin;
        Check(BusBay.CarInZone(-2f, 3f, from, to), "bay cars: a car across the bay's middle is in it");
        Check(BusBay.CarInZone(from - 3f, from + 0.5f, from, to) && BusBay.CarInZone(to - 0.5f, to + 4f, from, to), "bay cars: a car over either end is in it");
        Check(BusBay.CarInZone(from - 5f, from - m + 0.05f, from, to) && BusBay.CarInZone(to + m - 0.05f, to + 5f, from, to), "bay cars: a bumper within the margin of an end is in it");
        Check(!BusBay.CarInZone(from - 6f, from - m - 0.05f, from, to) && !BusBay.CarInZone(to + m + 0.05f, to + 6f, from, to), "bay cars: a car beyond the margin is not");
        Check(BusBay.CarInZone(3f, -2f, from, to), "bay cars: a car's ends may be given either way round");
        Check(m >= 1f, "bay cars: the margin is a metre at the least");

        // A lane cut to the zone keeps no spot whose car would reach into it: the game lays a car only where it fits whole in the box.
        const float spot = 5f;
        float laneStart = from - 30f, laneEnd = to + 2f;
        BusBay.Keep(laneStart, laneEnd, from - m, to + m, out float keepStart, out float keepEnd, out float lost);
        bool none = true;
        for (float a = keepStart; a + spot <= keepEnd + 1e-4f; a += spot)
        {
            none &= !BusBay.CarInZone(a, a + spot, from, to);
        }
        Check(none && keepEnd <= from - m + 1e-4f && lost > 0f, "bay cars: what is left of a lane holds no spot whose car reaches the zone or its margin");
        Check(BusBay.SpotsLost(laneEnd - laneStart, keepEnd - keepStart, spot) == (int)Math.Floor((laneEnd - laneStart) / spot) - (int)Math.Floor((keepEnd - keepStart) / spot),
              "bay cars: the spots given up are the whole spots the lane no longer holds");
        Check(BusBay.SpotsLost(20f, 20f, spot) == 0 && BusBay.SpotsLost(20f, 0.02f, spot) == 4 && BusBay.SpotsLost(19.99f, 15f, spot) == 0 && BusBay.SpotsLost(20f, 14.9f, spot) == 2,
              "bay cars: spots given up, counted whole");

        // The boarding strip lies within a bay's zone for every way the bus may stand in it (BusZones keeps the zone clear of cars).
        const float kerb = 4.2f;
        const float waitAlong = -1.6f, waitAside = kerb + 1.25f;
        if (BusBay.Lay(kerb, out _, out _, out _, out float behind, out float ahead))
        {
            bool within = true;
            for (int i = 0; i < BusInside.StandShifts.Length; i++)
            {
                float s = BusInside.StandShifts[i];
                if (s < -1e-4f || s > BusInside.BayStandMost + 1e-4f)
                {
                    continue;
                }
                BusInside.Strip kerbStrip = BusInside.BoardingStrips(kerb, waitAlong, waitAside, s)[0];
                within &= kerbStrip.Along0 - kerbStrip.Half > -behind && kerbStrip.Along1 + kerbStrip.Half < ahead;
                within &= BusBay.BusLength * 0.5f + s < ahead + 1e-4f;
            }
            Check(within, "bay cars: the kerb by both doors lies within the bay's zone however the bus stands in it");
        }
        else
        {
            Check(false, "bay cars: a bay is laid out at a kerb " + kerb.ToString("F1") + " m from the lane's line");
        }

        // The pole: every place it may stand is clear of the strips and both doors, however the bus stands.
        foreach (float k in new[] { 3.0f, 3.4f, 4.2f, 5.2f })
        {
            for (int i = 0; i < BusInside.SignPlaces.Length; i++)
            {
                BusInside.Spot place = BusInside.SignPlaces[i];
                Check(BusInside.SignClearAlways(place.Z, k + place.X, k, -1.6f, k + 1.25f), "bus stop pole: place " + i + " is clear of the doors and the ways to them (kerb " + k.ToString("F1") + " m)");
            }
        }
        Check(!BusInside.SignClear(0f, kerb + 0.5f, kerb, waitAlong, waitAside, 0f), "bus stop pole: where it used to stand (at the kerb beside the bus's middle) is not clear");
        Check(!BusInside.SignClear(BusInside.FrontDoor, kerb + 0.5f, kerb, waitAlong, waitAside, 0f) && !BusInside.SignClear(BusInside.MiddleDoor, kerb + 0.3f, kerb, waitAlong, waitAside, 0f),
              "bus stop pole: never in front of either door");
        Check(!BusInside.SignClear(BusInside.FrontDoor + 0.5f, kerb + 0.5f, kerb, waitAlong, waitAside, 0.5f) && BusInside.InDoorWay(BusInside.FrontDoor + 1f, kerb + 0.4f, kerb, 1f, 0f),
              "bus stop pole: the doors are where the bus stands, not where the pole is");
        Check(!BusInside.SignClear((waitAlong + BusInside.FrontDoor) * 0.5f, (waitAside + kerb) * 0.5f, kerb, waitAlong, waitAside, 0f), "bus stop pole: never on the way from the waiting place to a door");
        Check(BusInside.SignPlaces[BusInside.SignByShelter].X >= BusInside.DoorStep + BusInside.PoleReach + BusInside.PoleClear, "bus stop pole: by the shelter it stands back beyond the doors' steps");
        Check(BusInside.SignPlaces[1].Z < 0f && BusInside.SignPlaces[1].Z > BusInside.SignPlaces[2].Z, "bus stop pole: without a shelter, behind the waiting place before the bus's rear");
        Check(Math.Abs(BusInside.SignPlaces[2].Z) > BusBay.BusLength * 0.5f + 0.5f && BusInside.SignPlaces[3].Z > BusBay.BusLength * 0.5f + 0.5f, "bus stop pole: the last resorts are beyond the bus's ends");

        // Where the bus stands: neither door against what stays on the kerb.
        float[] at = new float[4], half = new float[4];
        Check(BusInside.StandShift(at, half, 0, -1f, 1f) == 0f, "bus stands: with nothing on the kerb, beside the pole");
        at[0] = BusInside.FrontDoor;
        half[0] = 0.15f;   // a street tree's trunk at the front door
        float stand = BusInside.StandShift(at, half, 1, -1f, 1f);
        Check(BusInside.DoorFree(BusInside.FrontDoor + stand, at, half, 1) && BusInside.DoorFree(BusInside.MiddleDoor + stand, at, half, 1) && Math.Abs(stand) >= 0.99f,
              "bus stands: a tree at the front door, the bus a metre along (" + stand.ToString("F1") + " m)");
        at[0] = BusInside.MiddleDoor;
        stand = BusInside.StandShift(at, half, 1, -1f, 1f);
        Check(BusInside.DoorFree(BusInside.MiddleDoor + stand, at, half, 1) && BusInside.DoorFree(BusInside.FrontDoor + stand, at, half, 1), "bus stands: a tree at the middle door, both doors clear");
        Check(BusInside.StandShift(at, half, 1, 0f, BusInside.BayStandMost) >= 0f && BusInside.StandShift(at, half, 1, 0f, BusInside.BayStandMost) <= BusInside.BayStandMost,
              "bus stands: in a bay never short of the pole, and no more than its slack on");
        at[0] = BusInside.FrontDoor;
        half[0] = 1.5f;   // something long at the front door, more than the bus may move for
        at[1] = BusInside.MiddleDoor;
        half[1] = 1.5f;
        Check(BusInside.StandShift(at, half, 2, -1f, 1f) == 0f, "bus stands: where no place will do, beside the pole as before");
        at[0] = BusInside.FrontDoor + 0.2f;
        half[0] = 0.15f;
        at[1] = BusInside.MiddleDoor - 0.6f;
        half[1] = 0.15f;
        stand = BusInside.StandShift(at, half, 2, -1f, 1f);
        Check(BusInside.DoorFree(BusInside.FrontDoor + stand, at, half, 2), "bus stands: the front door, the way in, is cleared before the middle one");
        Check(BusInside.StandShifts[0] == 0f && Array.TrueForAll(BusInside.StandShifts, s => Math.Abs(s) <= 1f + 1e-4f), "bus stands: beside the pole first, never more than a metre from it");
        // The pole itself never stands at a door, so it never moves the bus.
        float[] pole = new float[BusInside.SignPlaces.Length], reach = new float[BusInside.SignPlaces.Length];
        int poles = 0;
        for (int i = 0; i < BusInside.SignPlaces.Length; i++)
        {
            if (BusInside.SignPlaces[i].X <= BusInside.DoorStep + BusInside.PoleReach)
            {
                pole[poles] = BusInside.SignPlaces[i].Z;
                reach[poles++] = BusInside.PoleReach;
            }
        }
        bool polesClear = true;
        for (int i = 0; i < poles; i++)
        {
            polesClear &= BusInside.StandShift(new[] { pole[i] }, new[] { reach[i] }, 1, -1f, 1f) == 0f;
        }
        Check(polesClear, "bus stands: the pole at the kerb (" + poles + " of its places) lines up with neither door");
    }
}
