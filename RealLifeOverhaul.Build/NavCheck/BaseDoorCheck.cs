using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.Nav;

// The minimap's alignment check (a player's log, 28 Oct 2026, and a dozen before it: "the door furthest from its own building's outline is
// 3841.7 m outside it: FAIL"). The kept file named each outline's building by its place in CityManager's list, and the game fills that list
// with FindObjectsByType(None), whose order it does not keep from one load to the next, so a city read from the file paired a door with another
// building's outline, kilometres away. Now an outline is named by its building's address and found again by it. These checks run that whole
// path on a made-up city whose list is put in another order, as a new load would, and read the sources that must keep it so.
internal static partial class Program
{
    // A made-up city of 60 buildings in 6 streets: a 20 m square each, 120 m apart across a 1 km field, the door 3 m from the square's edge.
    private sealed class MadeCity
    {
        public readonly string[] Street = new string[60];
        public readonly int[] Number = new int[60];
        public readonly float[] CentreX = new float[60], CentreZ = new float[60];

        public MadeCity()
        {
            for (int i = 0; i < 60; i++)
            {
                Street[i] = "ba:street_" + (char)('a' + i % 6);
                Number[i] = 1 + i / 6;
                CentreX[i] = -500f + (i % 10) * 120f;
                CentreZ[i] = -300f + (i / 10) * 120f;
            }
        }

        public float DoorX(int i)
        {
            return CentreX[i] + 13f;   // 10 m of half-width and 3 m beyond
        }

        public float DoorZ(int i)
        {
            return CentreZ[i];
        }

        // The measured city, as the game's list stood in the session that measured it: outline h is building h, named by its address.
        public NavBaseData Measure()
        {
            var data = new NavBaseData { Build = 1, Cell = 8f, Columns = 1, Rows = 1, Cells = new byte[1], Hulls = 60 };
            data.HullStart = new int[61];
            data.HullKey = new long[60];
            data.HullX = new float[240];
            data.HullZ = new float[240];
            float[] dx = { -10f, 10f, 10f, -10f }, dz = { -10f, -10f, 10f, 10f };   // anticlockwise
            for (int h = 0; h < 60; h++)
            {
                data.HullStart[h] = h * 4;
                data.HullKey[h] = NavBaseMath.BuildingKey(Street[h], Number[h]);
                for (int k = 0; k < 4; k++)
                {
                    data.HullX[h * 4 + k] = CentreX[h] + dx[k];
                    data.HullZ[h * 4 + k] = CentreZ[h] + dz[k];
                }
            }
            data.HullStart[60] = 240;
            return data;
        }

        // The game's list at another load: the same buildings in another order (a fixed shuffle), place n holding building order[n].
        public int[] Order(int seed)
        {
            var order = new int[60];
            for (int i = 0; i < 60; i++)
            {
                order[i] = i;
            }
            var random = new Random(seed);
            for (int i = 59; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int t = order[i];
                order[i] = order[j];
                order[j] = t;
            }
            return order;
        }
    }

    private static void BaseDoors()
    {
        var city = new MadeCity();
        NavBaseData data = city.Measure();
        int[] order = city.Order(7);
        var pick = new int[3];

        // ---- the name of a building
        long a = NavBaseMath.BuildingKey("ba:street_hovgaardlane", 2), b = NavBaseMath.BuildingKey("ba:street_hovgaardlane", 4), c = NavBaseMath.BuildingKey("ba:street_lakestreet", 2);
        Check(a != 0L && b != 0L && c != 0L && a != b && a != c && b != c && a == NavBaseMath.BuildingKey("ba:street_hovgaardlane", 2),
              "minimap doors: a building's key is its address's, the same every time, and another address another key");
        Check(NavBaseMath.BuildingKey(null, 3) == 0L && NavBaseMath.BuildingKey(string.Empty, 3) == 0L, "minimap doors: no street, no key");
        var all = new HashSet<long>();
        for (int i = 0; i < 60; i++)
        {
            all.Add(data.HullKey[i]);
        }
        Check(all.Count == 60, "minimap doors: the 60 buildings of a made-up city have 60 keys");

        // ---- the old way: outline h is the building at place h of the list kept; the list at the next load is in another order
        var oldDoorX = new float[60];
        var oldDoorZ = new float[60];
        for (int h = 0; h < 60; h++)
        {
            oldDoorX[h] = city.DoorX(order[h]);   // what the building now at place h has for a door
            oldDoorZ[h] = city.DoorZ(order[h]);
        }
        int got = NavBaseMath.FarApart(oldDoorX, oldDoorZ, 60, pick);
        float oldWorst = NavBaseMath.WorstOutside(oldDoorX, oldDoorZ, data, pick, got, out int oldBlame);
        Check(got == 3 && oldWorst > 100f && oldBlame >= 0,
              "minimap doors: by place in the list, a list in another order pairs doors with other buildings' outlines (" + oldWorst.ToString("F0") + " m out), which is the failure of the logs");

        // ---- by address: every outline finds its own building whatever the order
        bool everyOrder = true;
        string why = string.Empty;
        for (int seed = 1; seed <= 25 && everyOrder; seed++)
        {
            int[] at = city.Order(seed);   // at[n]: the building at place n of the list
            var live = new long[60];
            for (int n = 0; n < 60; n++)
            {
                live[n] = NavBaseMath.BuildingKey(city.Street[at[n]], city.Number[at[n]]);
            }
            var found = new int[60];
            int matched = NavBaseMath.Resolve(data.HullKey, 60, live, 60, found);
            bool own = matched == 60;
            var doorX = new float[60];
            var doorZ = new float[60];
            var known = new bool[60];
            for (int h = 0; h < 60; h++)
            {
                own &= found[h] >= 0 && at[found[h]] == h;
                known[h] = found[h] >= 0;
                doorX[h] = known[h] ? city.DoorX(at[found[h]]) : 0f;
                doorZ[h] = known[h] ? city.DoorZ(at[found[h]]) : 0f;
            }
            int picks = NavBaseMath.FarApartKnown(doorX, doorZ, known, 60, pick);
            float worst = NavBaseMath.WorstOutside(doorX, doorZ, data, pick, picks, out _);
            if (!own || picks != 3 || worst > 8f)
            {
                everyOrder = false;
                why = " (order " + seed + ", matched " + matched + ", worst " + worst.ToString("F1") + " m)";
            }
        }
        Check(everyOrder, "minimap doors: by address, every outline finds its own building in 25 orders of the list, and the doors picked lie 3 m from their outlines" + why);

        // ---- a building gone from the list, a building the mod stands, an address two buildings share
        var gone = new long[60];
        for (int n = 0; n < 60; n++)
        {
            gone[n] = n == 17 ? 0L : data.HullKey[n];   // one left out: the mod's own stands in its place, or it was knocked down
        }
        var place = new int[60];
        int left = NavBaseMath.Resolve(data.HullKey, 60, gone, 60, place);
        Check(left == 59 && place[17] == -1 && place[16] == 16 && place[18] == 18, "minimap doors: an outline whose building has left the list finds none, and the others still find theirs");
        var twice = (long[])data.HullKey.Clone();
        twice[5] = twice[4];
        int shared = NavBaseMath.Resolve(data.HullKey, 60, twice, 60, place);
        Check(place[4] == -1 && place[5] == -1 && shared == 58, "minimap doors: an address two buildings share names neither, rather than the wrong one");
        NavBaseData doubled = city.Measure();
        doubled.HullKey[9] = doubled.HullKey[8];
        int outlines = NavBaseMath.Resolve(doubled.HullKey, 60, data.HullKey, 60, place);
        Check(place[8] == -1 && place[9] == -1 && outlines == 58, "minimap doors: nor do two outlines of one address");
        Check(NavBaseMath.Resolve(data.HullKey, 60, new long[60], 60, place) == 0, "minimap doors: a list with no address finds nothing, and nothing is thrown");

        // ---- the check picks among the buildings found only, and names the one it blames
        var known2 = new bool[60];
        var x2 = new float[60];
        var z2 = new float[60];
        for (int i = 0; i < 60; i++)
        {
            known2[i] = i % 2 == 1;
            x2[i] = i == 20 ? -9000f : city.DoorX(i);   // a door far to the west, at a building not found
            z2[i] = city.DoorZ(i);
        }
        int some = NavBaseMath.FarApartKnown(x2, z2, known2, 60, pick);
        Check(some == 3 && known2[pick[0]] && known2[pick[1]] && known2[pick[2]] && pick[0] != pick[1] && pick[1] != pick[2] && pick[0] != pick[2],
              "minimap doors: the three far apart are picked from the buildings found, never from one not found");
        Check(NavBaseMath.FarApartKnown(x2, z2, new bool[60], 60, pick) == 0, "minimap doors: none found, none picked");

        var dx = new float[60];
        var dz = new float[60];
        for (int i = 0; i < 60; i++)
        {
            dx[i] = city.DoorX(i);
            dz[i] = city.DoorZ(i);
        }
        int[] three = { 3, 41, 27 };
        Check(NavBaseMath.WorstOutside(dx, dz, data, three, 3, out int fine) <= 3.01f && fine >= 0, "minimap doors: doors 3 m from their outlines read 3 m");
        dx[41] += 3800f;   // the door of the second pick lies 3.8 km from its outline
        float blamed = NavBaseMath.WorstOutside(dx, dz, data, three, 3, out int which);
        Check(which == 1 && blamed > 3790f && blamed < 3810f, "minimap doors: the pick furthest from its outline is the one named (here the second, 3.8 km out)");
        Check(NavBaseMath.WorstOutside(dx, dz, data, three, 0, out int nobody) == 0f && nobody == -1, "minimap doors: with no pick there is no one to blame");

        // ---- the kept file keeps the names, and a file of the old kind is made again
        byte[] bytes = data.Encode();
        NavBaseData read = NavBaseData.Decode(bytes, 1, 0f, 0f, 0f, 0f);
        bool same = read != null && read.Hulls == 60;
        for (int i = 0; same && i < 60; i++)
        {
            same &= read.HullKey[i] == data.HullKey[i];
        }
        Check(same && NavBaseData.Format == 2, "minimap doors: the keys are written and read back, in a file of the second kind");
        byte[] first = (byte[])bytes.Clone();
        first[4] = 1;   // the file's kind, as the first kind (the place in the list) wrote it
        uint sum = 2166136261u;
        for (int i = 0; i < first.Length - 4; i++)
        {
            sum = (sum ^ first[i]) * 16777619u;
        }
        first[first.Length - 4] = (byte)sum;
        first[first.Length - 3] = (byte)(sum >> 8);
        first[first.Length - 2] = (byte)(sum >> 16);
        first[first.Length - 1] = (byte)(sum >> 24);
        Check(NavBaseData.Decode(first, 1, 0f, 0f, 0f, 0f) == null, "minimap doors: a kept file of the first kind, whose places in the list mean nothing now, is not used: the city is measured again");
        NavBaseData more = data.With(new List<float> { 0f, 5f, 5f, 0f }, new List<float> { 0f, 0f, 5f, 5f }, new List<int> { 0 });
        Check(more.Hulls == 61 && more.HullKey.Length == 61 && more.HullKey[60] == 0L && more.HullKey[59] == data.HullKey[59], "minimap doors: the mod's own buildings drawn on the city have no key");

        // ---- the sources
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string director = Source(scripts, @"Navigation\NavDirector.Base.cs"), baseGame = Source(scripts, @"Navigation\NavBaseGame.cs"), measure = Source(scripts, @"Navigation\NavBase.cs");
        string rules = Source(scripts, @"Navigation\NavBaseRules.cs");
        string line = Body(director, "private void BaseCheck()"), marks = Body(director, "public void TestBaseMarks()");
        Check(director.Length > 0 && baseGame.Length > 0 && measure.Length > 0 && rules.Length > 0 && !director.Contains("HullOf") && !baseGame.Contains("HullOf") && !measure.Contains("HullOf") && !rules.Contains("HullOf"),
              "minimap doors sources: nothing names an outline's building by its place in the game's list any more");
        Check(measure.Contains("_hullKey.Add(NavBaseGame.KeyOf(buildings[_cursor]))") && baseGame.Contains("NavBaseMath.Resolve(data.HullKey"),
              "minimap doors sources: the city is measured with each building's address and read back by it");
        Check(line.Contains("NavBaseGame.Find(buildings, data, found)") && line.Contains("NavBaseMath.FarApartKnown(") && line.Contains("NavBaseMath.WorstOutside(") && !line.Contains("buildings[index]"),
              "minimap doors sources: the alignment check finds each building by address, picks among those found, and judges the worst pick");
        Check(line.Contains("that of '\" + NavBaseGame.NameOf(found[h]) + \"', its door at (\"") && line.Contains("outlines found their building by its address") && line.Contains("its outline's middle at ("),
              "minimap doors sources: its line names the building and the door it blames, where its outline is, and how many outlines found their building");
        Check(marks.Contains("NavBaseGame.Find(buildings, data, found)") && marks.Contains("NavBaseMath.FarApartKnown(") && !marks.Contains("buildings[index]"),
              "minimap doors sources: the test that marks three buildings finds them by address too");
        Check(Body(director, "private void BaseShown(").Contains("Meta.Dev.Verbose") && Body(director, "private void BaseShown(").Contains("BaseCheck();") && !Body(baseGame, "public static int Find(").Contains("Update"),
              "minimap doors sources: the finding by address runs for the check (once per picture, with Developer diagnostics on) and the test, never a frame");
    }
}
