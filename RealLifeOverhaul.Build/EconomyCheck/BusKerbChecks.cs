using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// A parked car in the bay of the bus stop at 26 1st Street (the owner's screenshot, 10 Nov 2026, "That must not happen anywhere": no car
// but the player's may stand parked in any bus stop, in any district, from any source). The log of that session: the stop had a bay (51 m),
// its zone was "not made yet (in view; ...)" for the whole session, because the sweep left a game car alone while the player could see it
// (BusZones.SendAway, TransitBays.SendAway), and 55 of the city's 68 places had no bay and so no zone at all (BusZones worked at stops with
// a bay only), their kerbs left to the game's parking. Now every place has a zone (BusZoneRules), the game's cars in it go at once, view or
// not, every lane beside it is gated, and every path of the mod that stands a car at a kerb asks BusZones.Holds. Offline: run with --bus-kerb.
internal static partial class Program
{
    private const int KerbStops = 240;          // modelled stops: every heading, every kerb the city has, bays and places without one
    private static readonly float[] KerbWidths = { 2.04f, 2.10f, 2.18f, 3.36f, 4.23f, 4.48f, 4.66f, 5.39f };

    private struct KerbStop
    {
        public float X, Z, AlongX, AlongZ, Kerb, From, To;
        public bool Bay;
        public BusZoneRules.Zone Zone;
    }

    private static uint _kerbSeed = 20261110u;

    private static float KerbRand()
    {
        _kerbSeed = _kerbSeed * 1664525u + 1013904223u;
        return ((_kerbSeed >> 8) & 0xFFFFFF) / 16777216f;
    }

    private static float KerbBetween(float low, float high)
    {
        return low + (high - low) * KerbRand();
    }

    // The modelled city: the stops are laid at every heading, kerb and standing shift the real ones come in.
    private static KerbStop[] KerbCity()
    {
        _kerbSeed = 20261110u;
        var city = new KerbStop[KerbStops];
        for (int i = 0; i < city.Length; i++)
        {
            double angle = (i * 37 % 360) * Math.PI / 180.0 + KerbRand() * 0.02;
            var stop = new KerbStop
            {
                X = KerbBetween(-900f, 900f), Z = KerbBetween(-900f, 900f), AlongX = (float)Math.Sin(angle), AlongZ = (float)Math.Cos(angle),
                Kerb = KerbWidths[i % KerbWidths.Length]
            };
            stop.Bay = stop.Kerb >= 4.2f && i % 2 == 0;
            if (stop.Bay)
            {
                stop.From = -KerbBetween(24f, 33f);
                stop.To = KerbBetween(18f, 27f);
            }
            else
            {
                BusZoneRules.Standing(-1f + (i % 9) * 0.25f, out stop.From, out stop.To);
            }
            stop.Zone = BusZoneRules.Zone.Make(stop.X, 0f, stop.Z, stop.AlongX, stop.AlongZ, stop.From, stop.To, stop.Kerb);
            city[i] = stop;
        }
        return city;
    }

    // A point on the ground by its distance along the stop's lane from its lane point, and aside to the right of it.
    private static void KerbPoint(in KerbStop s, float along, float aside, out float x, out float z)
    {
        x = s.X + s.AlongX * along + s.AlongZ * aside;
        z = s.Z + s.AlongZ * along - s.AlongX * aside;
    }

    // The same question as BusZoneRules.Holds, asked another way: the car's footprint sampled a point at a time. Held if any point of it is in the
    // zone grown by 'grow' metres all round.
    private static bool KerbSampled(in BusZoneRules.Zone z, float cx, float cz, float fx, float fz, float halfLength, float halfWidth, float grow)
    {
        int rows = Math.Max(1, (int)Math.Ceiling(halfLength * 2f / 0.1f)), columns = Math.Max(1, (int)Math.Ceiling(halfWidth * 2f / 0.1f));
        for (int i = 0; i <= rows; i++)
        {
            float a = -halfLength + halfLength * 2f * i / rows;
            for (int k = 0; k <= columns; k++)
            {
                float s = -halfWidth + halfWidth * 2f * k / columns;
                float px = cx + fx * a + fz * s;
                float pz = cz + fz * a - fx * s;
                float dx = px - z.MidX, dz = pz - z.MidZ;
                float along = dx * z.AlongX + dz * z.AlongZ;
                float aside = dx * z.AlongZ - dz * z.AlongX;
                if (Math.Abs(along) <= z.HalfAlong + grow && Math.Abs(aside) <= z.HalfAside + grow)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static void BusKerb()
    {
        KerbStop[] city = KerbCity();
        BusKerbZones(city);
        BusKerbAgreement(city);
        BusKerbScreenshot();
        BusKerbPaths(city);
        BusKerbLanes(city);
        BusKerbCost(city);
        BusKerbSources();
        BusKerbTexts();
    }

    // Every place has a zone that covers the kerb strip of its length and nothing of its own lane.
    private static void BusKerbZones(KerbStop[] city)
    {
        int covered = 0, clear = 0, laneClear = 0, bays = 0, plain = 0, painted = 0;
        foreach (KerbStop s in city)
        {
            bays += s.Bay ? 1 : 0;
            plain += s.Bay ? 0 : 1;
            float outer = s.Kerb + BusZoneRules.KerbSlack;
            bool inside = true;
            float[] alongs = { s.From + 0.02f, (s.From + s.To) * 0.5f, s.To - 0.02f, s.From - BusZoneRules.Margin + 0.02f, s.To + BusZoneRules.Margin - 0.02f };
            float[] asides = { BusZoneRules.Inner + 0.02f, (BusZoneRules.Inner + outer) * 0.5f, outer - 0.02f };
            foreach (float along in alongs)
            {
                foreach (float aside in asides)
                {
                    KerbPoint(s, along, aside, out float x, out float z);
                    inside &= BusZoneRules.Holds(s.Zone, x, 0f, z, s.AlongX, s.AlongZ, 0.01f, 0.01f);
                }
            }
            covered += inside ? 1 : 0;
            bool outside = true;
            foreach (float aside in new[] { BusZoneRules.Inner - 0.05f, 0f, outer + 0.05f, -2f })
            {
                KerbPoint(s, (s.From + s.To) * 0.5f, aside, out float x, out float z);
                outside &= !BusZoneRules.Holds(s.Zone, x, 0f, z, s.AlongX, s.AlongZ, 0.01f, 0.01f);
            }
            foreach (float along in new[] { s.From - BusZoneRules.Margin - 0.05f, s.To + BusZoneRules.Margin + 0.05f })
            {
                KerbPoint(s, along, (BusZoneRules.Inner + outer) * 0.5f, out float x, out float z);
                outside &= !BusZoneRules.Holds(s.Zone, x, 0f, z, s.AlongX, s.AlongZ, 0.01f, 0.01f);
            }
            clear += outside ? 1 : 0;
            // A car in its own lane, along it, from any way along: never in the zone (a taxi at the pole is in the road, not the kerb).
            bool lane = true;
            for (float along = -50f; along <= 50f; along += 0.5f)
            {
                KerbPoint(s, along, 0f, out float x, out float z);
                lane &= !BusZoneRules.Holds(s.Zone, x, 0f, z, s.AlongX, s.AlongZ, 2.5f, 0.95f);
            }
            laneClear += lane ? 1 : 0;
            // The painted box (BusStopDressing.Paint) lies inside the zone: its ends are the zone's or short of them.
            float half = BusBay.BusLength * 0.5f;
            float paintFrom = Math.Max(s.From, -(half + 3f + 4f)), paintTo = Math.Min(s.To, half + 2f + 4f);
            painted += paintFrom >= s.From && paintTo <= s.To ? 1 : 0;
        }
        Check(bays > 20 && plain > 20, "bus kerb: the model city has bays and places without one (" + bays + " and " + plain + ")");
        Check(covered == city.Length, "bus kerb: every place's zone holds every part of its strip, lane edge to kerb, margins included (" + covered + " of " + city.Length + ")");
        Check(clear == city.Length, "bus kerb: every zone stops at its margin, short of the lane's edge and past the kerb (" + clear + " of " + city.Length + ")");
        Check(laneClear == city.Length, "bus kerb: a car in its own lane is never in a zone, however far along it is (" + laneClear + " of " + city.Length + ")");
        Check(painted == city.Length, "bus kerb: the painted box of a stop is inside its zone (" + painted + " of " + city.Length + ")");
        // A place without a bay keeps the standing bus's length with the box painted round it (BusStopDressing's 3 m behind and 2 m ahead).
        bool standing = true;
        for (float stand = -1f; stand <= 1.001f; stand += 0.25f)
        {
            BusZoneRules.Standing(stand, out float from, out float to);
            standing &= from <= stand - BusBay.BusLength * 0.5f - 2.99f && to >= stand + BusBay.BusLength * 0.5f + 1.99f;
        }
        Check(standing, "bus kerb: a place without a bay keeps the standing bus's length and the box round it, whichever way the bus stands from the pole");
    }

    // The two ways of asking agree: the fast rectangles and a car sampled a point at a time, over cars all round every zone at every heading.
    private static void BusKerbAgreement(KerbStop[] city)
    {
        int asked = 0, held = 0, wrongYes = 0, wrongNo = 0;
        _kerbSeed = 77u;
        foreach (KerbStop s in city)
        {
            for (int k = 0; k < 40; k++)
            {
                float along = KerbBetween(s.From - 9f, s.To + 9f);
                float aside = KerbBetween(-1.5f, s.Kerb + 2f);
                double heading = KerbRand() * Math.PI * 2.0;
                float fx = (float)Math.Sin(heading), fz = (float)Math.Cos(heading);
                float length = KerbBetween(1.6f, 3.2f), width = KerbBetween(0.8f, 1.3f);
                KerbPoint(s, along, aside, out float x, out float z);
                bool fast = BusZoneRules.Holds(s.Zone, x, 0f, z, fx, fz, length, width);
                asked++;
                held += fast ? 1 : 0;
                if (!fast && KerbSampled(s.Zone, x, z, fx, fz, length, width, 0f))
                {
                    wrongNo++;    // a point of the car is in the zone and the rectangles said no
                }
                if (fast && !KerbSampled(s.Zone, x, z, fx, fz, length, width, 0.15f))
                {
                    wrongYes++;   // the rectangles said yes and no point of the car comes within 15 cm of the zone
                }
            }
        }
        Check(asked == city.Length * 40 && held > asked / 20 && held < asked - asked / 20, "bus kerb: the cars asked about lie both in zones and out of them (" + held + " of " + asked + " held)");
        Check(wrongNo == 0 && wrongYes == 0, "bus kerb: the rectangles agree with the car's footprint sampled a point at a time (" + wrongNo + " held cars said clear, " + wrongYes + " clear said held)");
        // Height: a car on a bridge above the stop is not in its kerb.
        KerbStop one = city[5];
        KerbPoint(one, 0f, (BusZoneRules.Inner + one.Kerb) * 0.5f, out float bx, out float bz);
        Check(BusZoneRules.Holds(one.Zone, bx, 0.5f, bz, one.AlongX, one.AlongZ, 2.4f, 0.95f) && !BusZoneRules.Holds(one.Zone, bx, 6f, bz, one.AlongX, one.AlongZ, 2.4f, 0.95f),
            "bus kerb: a car on a road a floor above a stop's kerb is not in it");
    }

    // The owner's screenshot: the bay at 26 1st Street, kerb 4.48 m out, 29 m to come in and 22 to pull out, a car beside the pole.
    private static void BusKerbScreenshot()
    {
        var zone = BusZoneRules.Zone.Make(0f, 0f, 0f, 0f, 1f, -29f, 23f, 4.48f);
        // The red car: 4.9 m long, in the strip by the pole, its side 1.2 m off the kerb.
        Check(BusZoneRules.Holds(zone, 3.3f, 0f, 0.3f, 0f, 1f, 2.45f, 0.95f), "bus kerb: the car beside the pole of 26 1st Street is in its stop's zone");
        Check(!BusZoneRules.Holds(zone, 0f, 0f, 0.3f, 0f, 1f, 2.45f, 0.95f), "bus kerb: the same car in the traffic lane is not");
        // The game's lane along that kerb (its box 70 m, from 45 m behind the pole to 25 ahead) cut as the bay's zone cuts it: nothing it parks reaches the zone.
        float from = -29f - BusZoneRules.Margin, to = 23f + BusZoneRules.Margin;
        bool cut = BusBay.Keep(-45f, 25f, from, to, out float keepStart, out float keepEnd, out float lost);
        Check(cut && Math.Abs(keepEnd - from) < 0.001f && Math.Abs(keepStart + 45f) < 0.001f && lost > 0f, "bus kerb: that lane is cut to the part before the zone (" + keepStart + " to " + keepEnd + " m, " + lost + " m given up)");
        int parkedInZone = 0, parked = 0;
        _kerbSeed = 5u;
        for (int day = 0; day < 200; day++)
        {
            for (float slot = 0f; slot + 2.5f < keepEnd - keepStart;)
            {
                float length = KerbBetween(3.8f, 5.4f), off = KerbBetween(0.2f, 0.8f);
                float centre = keepStart + slot + off + length * 0.5f;
                if (centre + length * 0.5f > keepEnd)
                {
                    slot += 5f;
                    continue;
                }
                slot += off + length;
                parked++;
                parkedInZone += BusZoneRules.Holds(zone, 3.3f, 0f, centre, 0f, 1f, length * 0.5f, 0.95f) ? 1 : 0;
            }
        }
        Check(parked > 300 && parkedInZone == 0, "bus kerb: of " + parked + " cars the game parks on 200 days in that lane, cut, none is in the zone (" + parkedInZone + ")");
        // Left whole, the same lane does put cars in the zone: the check can fail.
        int uncutInZone = 0;
        _kerbSeed = 5u;
        for (int day = 0; day < 200; day++)
        {
            for (float slot = 0f; slot + 2.5f < 70f;)
            {
                float length = KerbBetween(3.8f, 5.4f), off = KerbBetween(0.2f, 0.8f);
                float centre = -45f + slot + off + length * 0.5f;
                if (centre + length * 0.5f > 25f)
                {
                    slot += 5f;
                    continue;
                }
                slot += off + length;
                uncutInZone += BusZoneRules.Holds(zone, 3.3f, 0f, centre, 0f, 1f, length * 0.5f, 0.95f) ? 1 : 0;
            }
        }
        Check(uncutInZone > 100, "bus kerb: left whole, that lane puts cars in the zone (" + uncutInZone + " of them in 200 days), so the cut is what keeps them out");
    }

    // Every path that stands a car at a kerb asks the one question: a candidate place along the kerb of every stop is refused exactly where the car would be in the zone.
    private static void BusKerbPaths(KerbStop[] city)
    {
        int places = 0, refused = 0, wrong = 0;
        foreach (KerbStop s in city)
        {
            // The kerb tests of the patrol cars, the precinct's, the rented and bought cars, the test car (halfWidth 0.8 to 1.3, halfLength 1.8 to 3.2, a margin
            // of 0.25 to 0.5), every 0.5 m along the kerb for 80 m, at the aside the finders try (0.8 m, and none, from the lane's point; the kerb's offsets).
            foreach (float aside in new[] { 0f, 0.8f, 2.6f, s.Kerb - 1.1f })
            {
                for (float along = -40f; along <= 40f; along += 0.5f)
                {
                    float halfLength = 2.4f + 0.25f, halfWidth = 1.0f + 0.25f;
                    KerbPoint(s, along, aside, out float x, out float z);
                    bool asked = BusZoneRules.Holds(s.Zone, x, 0f, z, s.AlongX, s.AlongZ, halfLength, halfWidth);
                    float low = BusZoneRules.Inner, high = s.Kerb + BusZoneRules.KerbSlack;
                    // The truth for a car along the lane: its sides, and its ends, against the strip.
                    bool truth = aside + halfWidth > low && aside - halfWidth < high && along + halfLength > s.From - BusZoneRules.Margin && along - halfLength < s.To + BusZoneRules.Margin;
                    places++;
                    refused += asked ? 1 : 0;
                    wrong += asked != truth ? 1 : 0;
                }
            }
        }
        Check(places == city.Length * 4 * 161 && refused > places / 10, "bus kerb: the kerb places asked about lie in zones and out of them (" + refused + " of " + places + " refused)");
        Check(wrong == 0, "bus kerb: a kerb place is refused exactly where a car there would be in the zone, for every stop (" + wrong + " wrong of " + places + ")");
        // The taxi's, the fire scene's and the pursuit's pull in to the kerb (SceneParking.KerbRoom: its box from 12 m behind to 18 m ahead of the car, 2.2 m out): refused
        // while a zone lies in that stretch, free beyond it.
        int pulls = 0, pullsWrong = 0;
        foreach (KerbStop s in city)
        {
            for (float along = -80f; along <= 80f; along += 4f)
            {
                KerbPoint(s, along + 3f, 2.2f, out float x, out float z);
                bool asked = BusZoneRules.Holds(s.Zone, x, 0f, z, s.AlongX, s.AlongZ, 15f, 0.9f + 0.2f);
                bool truth = along + 3f + 15f > s.From - BusZoneRules.Margin && along + 3f - 15f < s.To + BusZoneRules.Margin && 2.2f + 1.1f > BusZoneRules.Inner && 2.2f - 1.1f < s.Kerb + BusZoneRules.KerbSlack;
                pulls++;
                pullsWrong += asked != truth ? 1 : 0;
            }
        }
        Check(pulls > 3000 && pullsWrong == 0, "bus kerb: a taxi or a unit does not pull in to the kerb with a stop's zone in its stretch, and does where there is none (" + pullsWrong + " wrong of " + pulls + ")");
        // A lane the mod stands (new ground, a made building's, a car park's rows: BusZones.Meet, ParkingGame.Room) over a stop's kerb: refused or gated.
        int lanes = 0, lanesWrong = 0;
        foreach (KerbStop s in city)
        {
            foreach (float length in new[] { 12f, 30f })
            {
                for (float along = -50f; along <= 50f; along += 5f)
                {
                    KerbPoint(s, along, s.Kerb - 1.1f, out float x, out float z);
                    bool asked = BusZoneRules.Holds(s.Zone, x, 0f, z, s.AlongX, s.AlongZ, length * 0.5f, 1.1f);
                    bool truth = along + length * 0.5f > s.From - BusZoneRules.Margin && along - length * 0.5f < s.To + BusZoneRules.Margin;
                    lanes++;
                    lanesWrong += asked != truth ? 1 : 0;
                }
            }
        }
        Check(lanes > 4000 && lanesWrong == 0, "bus kerb: a lane of the mod's own over a stop's kerb is met, and one clear of it is not (" + lanesWrong + " wrong of " + lanes + ")");
    }

    // The game's own parking along every stop's kerb, from the game's own slots: a bay's lane is cut, a place without a bay has its lane gated, and after either
    // no car of any lane is in the zone, whatever the lane's run, its spot count or its spacing.
    private static void BusKerbLanes(KerbStop[] city)
    {
        int stops = 0, cars = 0, gated = 0, inZone = 0, cutRuns = 0, lostMost = 0;
        _kerbSeed = 99u;
        foreach (KerbStop s in city)
        {
            stops++;
            // Parking runs along the kerb as the game lays them: 15 to 70 m, a fixed count of spots or the whole box, 5 to 7 m a spot.
            for (int run = 0; run < 4; run++)
            {
                float start = KerbBetween(-60f, 10f);
                float end = start + KerbBetween(15f, 70f);
                float spot = KerbBetween(5f, 7f);
                int count = run % 2 == 0 ? 0 : (int)((end - start) / spot) - 1;
                float keepStart = start, keepEnd = end;
                if (s.Bay)
                {
                    if (BusBay.Keep(start, end, s.From - BusZoneRules.Margin, s.To + BusZoneRules.Margin, out keepStart, out keepEnd, out float lost))
                    {
                        cutRuns++;
                        lostMost = Math.Max(lostMost, (int)lost);
                    }
                }
                float span = count > 0 ? count * spot : keepEnd - keepStart;
                float offset = count > 0 ? Math.Max(0f, (keepEnd - keepStart - span) * 0.5f) : 0f;
                for (float slot = 0f; slot + spot * 0.5f < span;)
                {
                    float length = KerbBetween(3.8f, 5.4f), off = KerbBetween(0.2f, 0.8f);
                    float centre = keepStart + offset + slot + off + length * 0.5f;
                    if (centre + length * 0.5f > keepEnd)
                    {
                        slot += spot;
                        continue;
                    }
                    slot += off + length;
                    cars++;
                    KerbPoint(s, centre, s.Kerb - 1.1f, out float x, out float z);
                    bool held = BusZoneRules.Holds(s.Zone, x, 0f, z, s.AlongX, s.AlongZ, length * 0.5f, 0.95f);
                    if (held && !s.Bay)
                    {
                        gated++;   // the gate switches it off as the game makes it (BusZones.Hush asks the same question)
                        continue;
                    }
                    inZone += held ? 1 : 0;
                }
            }
        }
        Check(stops == KerbStops && cars > 3000, "bus kerb: the model parked " + cars + " cars along " + stops + " stops' kerbs");
        Check(inZone == 0, "bus kerb: after the cut of a bay's lane and the gate of every other lane, no car of any parking lane is in any stop's zone (" + inZone + " of " + cars + ")");
        Check(gated > 100 && cutRuns > 30, "bus kerb: the gate and the cut both had cars to keep out (" + gated + " cars gated, " + cutRuns + " runs cut, the most parking given up beyond a zone " + lostMost + " m)");
    }

    // No cost per frame: the table is read on events, with no allocation; the asking is a few multiplications a zone.
    private static void BusKerbCost(KerbStop[] city)
    {
        var table = new BusZoneRules.Zone[city.Length];
        for (int i = 0; i < city.Length; i++)
        {
            table[i] = city[i].Zone;
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        int hits = BusKerbAsk(table, out double ms);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated == 0, "bus kerb: asking the whole table allocates nothing (" + allocated + " bytes over " + 200 * table.Length + " asks)");
        Check(hits >= 0 && ms < 400.0, "bus kerb: " + 200 * table.Length + " asks of " + table.Length + " zones took " + ms.ToString("F1") + " ms (under 400)");
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization | System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static int BusKerbAsk(BusZoneRules.Zone[] table, out double ms)
    {
        int hits = 0;
        long began = System.Diagnostics.Stopwatch.GetTimestamp();
        for (int q = 0; q < 200; q++)
        {
            float x = -900f + q * 9f, z = 900f - q * 9f;
            for (int i = 0; i < table.Length; i++)
            {
                hits += BusZoneRules.Holds(table[i], x, 0f, z, 0f, 1f, 2.4f, 1f) ? 1 : 0;
            }
        }
        ms = (System.Diagnostics.Stopwatch.GetTimestamp() - began) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        return hits;
    }

    private static string KerbScripts()
    {
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
    }

    private static string KerbSource(string relative)
    {
        return ModSource(relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
    }

    // Between a member's signature and the next, the way the other source checks cut a method out.
    private static string KerbBetweenText(string text, string from, string to)
    {
        int a = text.IndexOf(from, StringComparison.Ordinal);
        int b = a < 0 ? -1 : text.IndexOf(to, a + from.Length, StringComparison.Ordinal);
        return a < 0 || b < 0 ? string.Empty : text.Substring(a, b - a);
    }

    // Every path in the source, and nothing new slipping through: a file that stands a car at a kerb is on a list, and asks.
    private static void BusKerbSources()
    {
        string scene = KerbSource("Traffic/SceneParking.cs");
        Check(KerbBetweenText(scene, "public static bool IsFree(VehicleComponent car", "public static bool Stands(").Contains("BusZones.Holds(")
              && KerbBetweenText(scene, "public static bool Stands(", "public static Collider Touching(").Contains("BusZones.Holds(")
              && KerbBetweenText(scene, "public static float KerbRoom(", "public static int Find(").Contains("BusZones.Holds("),
            "bus kerb source: SceneParking's tests for a vehicle's pose, a kerb place and a pull in each ask BusZones.Holds");
        // The paths that stand a car at a kerb by the kerb place test.
        string[] byStands = { "People/PolicePresence.cs", "Law/PrecinctFrontage.Kerb.cs", "Jobs/JobRental.cs", "Licences/Driving/TestCar.cs", "Licences/Driving/TestRoute.cs", "Wiring/DebtLinks.cs" };
        bool stands = true;
        foreach (string file in byStands)
        {
            string text = KerbSource(file);
            stands &= text.Contains("SceneParking.Stands(") && !Regex.IsMatch(text, @"SceneParking\.IsFree\(\s*(?!car\b)");
        }
        Check(stands, "bus kerb source: the parked patrol cars, the precinct's, the rented, bought and test cars and the test's bay find their kerb place by SceneParking.Stands");
        // The pull ins.
        bool pulls = KerbSource("Transport/Taxi/TaxiCab.cs").Contains("SceneParking.KerbRoom(") && KerbSource("Events/FireScene.cs").Contains("SceneParking.KerbRoom(")
                     && KerbSource("Crime/Scenes/CarPursuitScene.cs").Contains("SceneParking.KerbRoom(");
        Check(pulls, "bus kerb source: the taxi, the fire scene's and the pursuit's stopped cars pull in to the kerb by SceneParking.KerbRoom");
        Check(KerbSource("Traffic/TrafficAccidents.Staging.cs").Contains("SceneParking.IsFree(car, position, rotation, _involved)"), "bus kerb source: where a wreck of a staged accident may lie is asked of SceneParking.IsFree for its car");
        Check(KerbSource("Crime/CrimeCars.cs").Contains("BusZones.Holds(") && KerbSource("CityLife/Activities/CarLeanActivity.cs").Contains("CrimeCars.FindParked("),
            "bus kerb source: the scenes and the driver leaning on a parked car pick one that does not stand in a stop's kerb");
        string ground = KerbSource("NewGround/ParkingGame.cs");
        Check(KerbBetweenText(ground, "public static bool Room(ParkingLaneGenerator lane, Vector3 at, Quaternion turn, float from", "public static ParkingLaneGenerator Copy(ParkingLaneGenerator template, Vector3 at, Quaternion turn, out").Contains("BusZones.HeldAt(")
              && KerbBetweenText(ground, "public static ParkingLaneGenerator Copy(ParkingLaneGenerator template, Vector3 at, Quaternion turn, float from", "private static void Span(").Contains("BusZones.Meet("),
            "bus kerb source: new ground's and the uptown modules' lanes are refused over a stop's kerb (Room) and gated (Copy)");
        Check(KerbSource("NewBuildings/CopyGame.cs").Contains("BusZones.Meet(") && KerbSource("Construction/CarParkGame.cs").Contains("BusZones.Meet("),
            "bus kerb source: a made building's lanes and a car park's rows are gated where they reach a stop's kerb");
        Check(KerbSource("Driving/VehicleTow.cs").Contains("BusZones.Refuses("), "bus kerb source: the tow and the bay's move put no car in a stop's kerb");
        // The game's own cars: at once, in view or not; no look at the view on a car, either way it is taken in.
        string cars = KerbSource("Transport/BusZones.Cars.cs");
        string kerb = KerbSource("Transport/BusZones.Kerb.cs");
        string bays = KerbSource("Transport/TransitBays.cs");
        Check(!KerbBetweenText(cars, "private static int SendAway(", "private static void Release(").Contains("InView") && !KerbBetweenText(bays, "private static bool SendAway(", "catch (Exception").Contains("InView")
              && !kerb.Contains("InView"), "bus kerb source: a game car in a bus stop's kerb goes at once, never left because the player can see it");
        Check(cars.Contains("sent = TakeIn(stop);") && cars.Contains("sent += TakeIn(stop);") && kerb.Contains("GateHandler = Guard.Wrap<GameObject>(Hush)") && kerb.Contains("Delegate.Combine(lane.onGenerateVehicle, GateHandler)")
              && kerb.Contains("Delegate.Remove(Gated[i].onGenerateVehicle, GateHandler)"),
            "bus kerb source: every lane beside a zone, of any kind, is gated by one handler made once, and the sweep covers every place, bay or not");
        string zones = KerbSource("Transport/BusZones.cs");
        Check(KerbBetweenText(zones, "public static void Step()", "// Makes one stop's zone").Contains("Lay();") && !zones.Contains("stop.HasBay && stop.Zone != null")
              && KerbBetweenText(cars, "private static void Watch()", "catch (Exception").Contains("stop.Zone == null || !stop.Zone.Made"),
            "bus kerb source: the zones are laid for every place, not those with a bay, and the watch looks at all of them");
        string stops = KerbSource("Transport/TransitMap.Stops.cs");
        Check(stops.Contains("BusZones.Unclearable(stop, right) != null"), "bus kerb source: a place is never put where the game keeps a delivery van standing at the kerb");
        // Going Public: nothing sent, no other player's car touched.
        Check(!kerb.Contains("Send") && !kerb.Contains("Network") && cars.Contains("GoingPublic.MineVehicle(vehicle)") && kerb.Contains("ParkedMask => 1 << LayerHelper.ParkedVehiclesLayerIndex"),
            "bus kerb source: nothing is sent, only the game's parked cars (the parked-vehicle layer, a lane's children) are taken in, and a player's car is moved only if it is the player's own");
        // No cost per frame.
        string rules = KerbSource("Transport/BusZoneRules.cs");
        Check(!kerb.Contains("Update(") && !kerb.Contains("FindObjects") && !kerb.Contains("System.Linq") && !rules.Contains("UnityEngine") && !rules.Contains("System.Linq"),
            "bus kerb source: no per-frame method, no search of the scene and no LINQ in the bus zones' kerb part, and the rules know no Unity type");
        // No path slips in unlisted: a file that stands a vehicle somewhere is on a list that says how it keeps out of a stop's kerb.
        var spawners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Economy/Banking/Desk/CreditDesk.Tests.cs", "a test's car at the kerb place of EconomyBridges.KerbSpot (SceneParking.Stands)" },
            { "Economy/Dealer/DealerGame.cs", "a lot's own bay, off the street" },
            { "Economy/Estate/EstateGame.Trade.cs", "a car yard's own bay, off the street" },
            { "Family/PartnerCar.cs", "a repair shop's bay, indoors" },
            { "Garage/TwoWheelers.Making.cs", "a bike or scooter, not a car" },
            { "Hustles/HustleGame.cs", "a car bought, at the kerb place of HustleBridges.KerbSpot (SceneParking.Stands)" },
            { "Jobs/JobRental.cs", "the rented car, SceneParking.Stands" },
            { "Licences/Driving/TestCar.cs", "the test car, SceneParking.Stands" },
            { "Driving/VehicleTow.cs", "the tow, BusZones.Refuses" },
            { "Uptown/LifeGame.BigPlaces.cs", "a car at a big place's gate or pump, on its own ground" },
            { "Uptown/LifeGame.Doors.cs", "a car at a big place's gate, on its own ground" }
        };
        var found = new List<string>();
        string scripts = KerbScripts();
        foreach (string path in Directory.Exists(scripts) ? Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories) : new string[0])
        {
            string relative = path.Substring(scripts.Length + 1).Replace('\\', '/');
            string text = File.ReadAllText(path);
            if (Regex.IsMatch(text, @"CreateAndSpawnVehicle\(|TeleportVehicleToGround\(") && !spawners.ContainsKey(relative))
            {
                found.Add(relative + " stands a vehicle and is on no list");
            }
            if (text.Contains("CreateParkedPatrol(") && relative != "People/PolicePresence.cs" && relative != "Law/PrecinctFrontage.Kerb.cs" && relative != "Traffic/TrafficEffects.Police.cs")
            {
                found.Add(relative + " parks a patrol car and is on no list");
            }
            if (text.Contains("RequestParkedVehicle(") && relative != "Transport/BusZones.cs")
            {
                found.Add(relative + " asks the game for a parked car");
            }
            if (Regex.IsMatch(text, @"SceneParking\.IsFree\(\s*(?!car\b)") && relative != "Events/StreetEventScene.Setup.cs" && relative != "Traffic/SceneParking.cs")
            {
                found.Add(relative + " asks SceneParking.IsFree for a place that is no vehicle's: a car's kerb place is asked of Stands");
            }
            if (text.Contains("ParkingLaneGenerator") && Regex.IsMatch(text, @"Object\.Instantiate\(|[^.]Instantiate\(")
                && relative != "NewGround/ParkingGame.cs" && relative != "NewBuildings/CopyGame.cs" && relative != "Construction/CarParkGame.cs" && relative != "Uptown/GroundGame.Art.cs"
                && relative != "Uptown/GroundGame.Mouths.cs")
            {
                found.Add(relative + " makes a parking lane and is on no list");
            }
        }
        Check(found.Count == 0, "bus kerb source: no path that stands a vehicle at a kerb is off the list" + (found.Count > 0 ? " (" + string.Join("; ", found.ToArray()) + ")" : ""));
        Check(spawners.Count == 11, "bus kerb source: the list of paths that spawn a vehicle names how each keeps out of a stop's kerb (" + spawners.Count + " listed)");
    }

    private static void BusKerbTexts()
    {
        var starts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Fixed: parked cars no longer stand in bus stops, in any district." }, { "pt", "Corrigido: carros estacionados não ficam mais em pontos de ônibus" },
            { "zh-cn", "已修复：停放的汽车不再出现在任何街区的公交车站里。" }
        };
        foreach (KeyValuePair<string, string> start in starts)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(LocalesFolder(), start.Key + ".json"));
            int rows = 0;
            bool plain = true;
            // In any What's new row, never the next row alone: the release moves its lines into the version's own.
            foreach (KeyValuePair<string, string> row in words ?? new Dictionary<string, string>())
            {
                if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Key.EndsWith("_title", StringComparison.Ordinal) && row.Value.Contains(start.Value))
                {
                    rows++;
                    plain &= !row.Value.Contains("  ") && !row.Value.Contains((char)0x2014) && !row.Value.Contains((char)0x2013) && !row.Value.Contains('!');
                    plain &= start.Value.StartsWith(FixMarks[start.Key], StringComparison.Ordinal);   // the line begins with the language's fix mark
                }
            }
            Check(rows == 1 && plain, "bus kerb texts: What's new has the fix once in " + start.Key + ", with its fix mark and no double space, dash or exclamation mark (" + rows + " row(s))");
        }
    }
}
