using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Tech;

// The midnight round's and the saves' pace (Scripts/Tech/People/PlaceRules, Scripts/Careers/Work/ProCatalogue's kept sets,
// Scripts/Tech/TechCodec's TechWriter): a building's place key is the company's own text, and a look-up passing buildings
// over on their number finds the very building the whole compare found, at the caps; which professions a business type
// employs, kept per type, is what the stems say for every type and profession; the save's lines written into kept
// builders are character for character the lines of the writer as it was, and cost the one string they are.
internal static partial class Program
{
    private static void WorkPace()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        PacePlaceKeys();
        PaceFindByNumber();
        PaceTalentIndex();
        PaceEmploysKept();
        PaceTechWriterSame();
        PaceTechWriterCost();
        Console.WriteLine("  the midnight round's pace: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // ---------------------------------------------------------------- place keys

    private static readonly string[] PaceStreets =
    {
        "ba:street_hovgaardlane", "ba:street_5thavenue", "", null, "West 42nd ~ Street", "a~b~c", "semi;colon|bar", "hash#and%percent", "Ünïcødé straße", "  spaced  ", "~", "-", "7",
        "ba:street_1st~5"
    };

    private static readonly int[] PaceNumbers = { 0, 1, -1, 7, 42, 999, 10000, int.MaxValue, int.MinValue, -2147483647 };

    private static void PacePlaceKeys()
    {
        bool same = true, number = true;
        foreach (string street in PaceStreets)
        {
            foreach (int n in PaceNumbers)
            {
                string key = PlaceRules.Key(street, n);
                same &= key == Uri.EscapeDataString(street ?? string.Empty) + "~" + n.ToString(CultureInfo.InvariantCulture);
                number &= PlaceRules.Number(key, out int back) && back == n;
            }
        }
        Check(same, "work pace: a building's place key is the company's own text (the street escaped, '~', the number)");
        Check(number, "work pace: the number read back out of every key is the building's, streets with '~' and the largest and smallest numbers included");
        bool refused = !PlaceRules.Number(null, out _) && !PlaceRules.Number("", out _) && !PlaceRules.Number("street", out _) && !PlaceRules.Number("street~", out _)
                       && !PlaceRules.Number("street~-", out _) && !PlaceRules.Number("street~12a", out _) && !PlaceRules.Number("street~99999999999", out _)
                       && !PlaceRules.Number("street~2147483648", out _) && !PlaceRules.Number("street~1 2", out _);
        Check(refused, "work pace: a key whose number cannot be read is said so (and compared whole)");
    }

    // The game's look-up of one of the player's buildings by its place key, as it was (every key made and compared) and as
    // it is (a building whose number is not the key's passed over first), on 2,500 buildings: the same building, every time.
    private static void PaceFindByNumber()
    {
        var rng = new Random(9);
        var streets = new List<string>();
        for (int i = 0; i < 60; i++)
        {
            streets.Add(i % 7 == 0 ? "Old ~ Road " + i : "ba:street_" + i);
        }
        var regs = new List<(string Street, int Number, bool Mine)>();
        for (int i = 0; i < 2500; i++)
        {
            regs.Add((streets[rng.Next(streets.Count)], rng.Next(1, 120), rng.Next(3) == 0));
        }
        regs.Add(regs[5]);   // (a building twice, as a damaged save may have it: the first is found)
        var places = new List<string>();
        for (int i = 0; i < 400; i++)
        {
            var r = regs[rng.Next(regs.Count)];
            places.Add(PlaceRules.Key(r.Street, r.Number));
        }
        places.AddRange(new[] { "", "nowhere~5", "ba:street_3~0007", "ba:street_3~7", "ba:street_3~", "%7E~3", "ba:street_3~-0" });
        int same = 0;
        foreach (string place in places)
        {
            int whole = -1, quick = -1;
            for (int i = 0; i < regs.Count; i++)
            {
                if (regs[i].Mine && PlaceRules.Key(regs[i].Street, regs[i].Number) == place)
                {
                    whole = i;
                    break;
                }
            }
            bool numbered = PlaceRules.Number(place, out int number);
            for (int i = 0; i < regs.Count; i++)
            {
                if ((!numbered || regs[i].Number == number) && regs[i].Mine && PlaceRules.Key(regs[i].Street, regs[i].Number) == place)
                {
                    quick = i;
                    break;
                }
            }
            same += whole == quick ? 1 : 0;
        }
        Check(same == places.Count, "work pace: a building looked for by its key, passing others over on their number, is the one the whole compare finds (" + same + " of " + places.Count + ")");
    }

    // ---------------------------------------------------------------- the professionals' files by id

    // The people's read finds each employee's file from TalentFile.Index: at the cap of 600, with an id twice and files
    // with no id, every id gives the file TalentFile.Of gives.
    private static void PaceTalentIndex()
    {
        TalentFile file = PaceTalent(TalentStore.PeopleMost);
        file.People[17].Id = file.People[400].Id;   // (an id twice: the first is the one)
        file.People[30].Id = string.Empty;
        file.People[31].Id = string.Empty;
        file.People[32].Id = null;
        var index = new Dictionary<string, Profile>();
        file.Index(index);
        int asked = 0, same = 0;
        var ids = file.People.Select(p => p.Id).Where(id => id != null).ToList();
        ids.AddRange(new[] { "absent", "pro:99999", "emp_" });
        foreach (string id in ids)
        {
            asked++;
            same += ReferenceEquals(index.TryGetValue(id, out Profile found) ? found : null, file.Of(id)) ? 1 : 0;
        }
        Check(same == asked && index.Count < file.People.Count, "work pace: every professional's file by id from the index is TalentFile.Of's, an id twice and no id included (" + same + " of " + asked + ")");
    }

    // ---------------------------------------------------------------- which professions a type employs

    private static void PaceEmploysKept()
    {
        Check(ProCatalogue.All.Length == ProCatalogue.Count && ProCatalogue.All.Length <= 64 && Enumerable.Range(0, ProCatalogue.All.Length).All(i => ProCatalogue.All[i].Id == i),
            "work pace: every profession of the catalogue is at its own place in it (the kept sets are by place, 64 at most)");
        var types = new List<string> { null, "", "ba:businesstype_empty", "ba:businesstype_headquarters", "ba:businesstype_webdevelopmentagency", "ba:businesstype_graphicdesigner",
            "ba:businesstype_lawfirm", "ba:businesstype_bank", "ba:businesstype_warehouse", "ba:businesstype_supermarket", "ba:businesstype_hospital" };
        foreach (Profession p in ProCatalogue.All)
        {
            foreach (string stem in p.Types)
            {
                types.Add("ba:businesstype_" + stem);
                types.Add(stem + "x");
                types.Add("z" + stem.ToUpperInvariant());
            }
        }
        var rng = new Random(13);
        for (int i = 0; i < 400; i++)
        {
            Profession a = ProCatalogue.All[rng.Next(ProCatalogue.All.Length)], b = ProCatalogue.All[rng.Next(ProCatalogue.All.Length)];
            string sa = a.Types.Length > 0 ? a.Types[rng.Next(a.Types.Length)] : "none", sb = b.Types.Length > 0 ? b.Types[rng.Next(b.Types.Length)] : "none";
            types.Add("ba:businesstype_" + sa.Substring(0, Math.Max(1, sa.Length - rng.Next(3))) + sb);
        }
        int asked = 0, same = 0, employing = 0;
        for (int round = 0; round < 2; round++)   // (asked twice: from the stems, then from the kept set)
        {
            foreach (string type in types)
            {
                bool any = false;
                long set = ProCatalogue.EmployedBy(type);
                foreach (Profession p in ProCatalogue.All)
                {
                    bool want = ProCatalogue.Employs(p, type);
                    any |= want;
                    asked += 2;
                    same += ProCatalogue.EmploysKept(p, type) == want ? 1 : 0;
                    same += ProCatalogue.Holds(set, p, type) == want ? 1 : 0;
                }
                asked++;
                same += ProCatalogue.AnyEmploys(type) == any ? 1 : 0;
                employing += any && round == 0 ? 1 : 0;
            }
        }
        Check(same == asked && employing > 50, "work pace: which professions a business type employs, kept per type, is what the stems say for every type and profession (" + same + " of "
                                               + asked + "; " + employing + " types employ)");
        Check(ProCatalogue.EmploysKept(null, "ba:businesstype_bank") == ProCatalogue.Employs(null, "ba:businesstype_bank"), "work pace: and for no profession, as the stems say");
        double whole = double.MaxValue, kept = double.MaxValue;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            whole = Math.Min(whole, PaceEmployersWhole(types));
            kept = Math.Min(kept, PaceEmployersKept(types));
        }
        Console.WriteLine("  work pace: a day's employers (1,000 businesses: whether any profession works there, then each profession asked of each) " + whole.ToString("0.000")
                          + " ms from the stems, " + kept.ToString("0.000") + " ms from the kept sets");
        Check(kept < whole * 0.25, "work pace: and asking the kept sets costs a fraction of the stems (" + kept.ToString("0.000") + " against " + whole.ToString("0.000") + " ms)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double PaceEmployersWhole(List<string> types)
    {
        int found = 0;
        long began = Stopwatch.GetTimestamp();
        for (int b = 0; b < 1000; b++)
        {
            string type = types[b % types.Count];
            for (int p = 0; p < ProCatalogue.All.Length; p++)
            {
                if (ProCatalogue.Employs(ProCatalogue.All[p], type))
                {
                    found++;
                    break;
                }
            }
        }
        for (int b = 0; b < 1000; b++)
        {
            string type = types[b % types.Count];
            for (int p = 0; p < ProCatalogue.All.Length; p++)
            {
                found += ProCatalogue.Employs(ProCatalogue.All[p], type) ? 1 : 0;
            }
        }
        double ms = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
        return found < 0 ? ms + 1 : ms;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double PaceEmployersKept(List<string> types)
    {
        int found = 0;
        long began = Stopwatch.GetTimestamp();
        var sets = new long[1000];
        for (int b = 0; b < 1000; b++)
        {
            string type = types[b % types.Count];
            found += ProCatalogue.AnyEmploys(type) ? 1 : 0;
            sets[b] = ProCatalogue.EmployedBy(type);   // (as WorkGame.Employers reads each employer once a day)
        }
        for (int b = 0; b < 1000; b++)
        {
            string type = types[b % types.Count];
            for (int p = 0; p < ProCatalogue.All.Length; p++)
            {
                found += ProCatalogue.Holds(sets[b], ProCatalogue.All[p], type) ? 1 : 0;   // (as each vacancy asks of every employer)
            }
        }
        double ms = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
        return found < 0 ? ms + 1 : ms;
    }

    // ---------------------------------------------------------------- the save's writer

    // TechWriter as it was written up to 0.17.1: a builder of its own, a string of every number and of every text.
    private sealed class RefTechWriter
    {
        private readonly StringBuilder _t = new StringBuilder(4096);
        private bool _first;
        private int _fields;

        public RefTechWriter(int version)
        {
            _t.Append("v=").Append(version.ToString(CultureInfo.InvariantCulture));
        }

        public RefTechWriter Part(char letter)
        {
            _t.Append('~').Append(letter).Append(':');
            _first = true;
            return this;
        }

        public RefTechWriter Record()
        {
            if (!_first)
            {
                _t.Append(';');
            }
            _first = false;
            _fields = 0;
            return this;
        }

        public RefTechWriter Add(long value)
        {
            Sep();
            _t.Append(value.ToString(CultureInfo.InvariantCulture));
            return this;
        }

        public RefTechWriter Add(bool value) => Add(value ? 1 : 0);

        public RefTechWriter Add(string text)
        {
            Sep();
            _t.Append(TechWriter.Clean(text));
            return this;
        }

        public RefTechWriter Place(string key)
        {
            Sep();
            _t.Append((key ?? string.Empty).Replace("~", "%7E").Replace(";", " ").Replace("|", " "));
            return this;
        }

        private void Sep()
        {
            if (_fields++ > 0)
            {
                _t.Append('|');
            }
        }

        public override string ToString() => _t.ToString();
    }

    private static readonly string[] PaceTexts = { null, "", "plain", "a~b;c|d", "~;|", "émigré ☕ 名字", "pro:17", "id=+/x", "  ", "%7E", "-", new string('x', 300) };
    private static readonly long[] PaceLongs = { 0, 1, -1, 9, 10, -10, 123456789, int.MaxValue, int.MinValue, long.MaxValue, long.MinValue, long.MinValue + 1, 1000000000000L };

    // The same writing done by both writers, op by op, from a seed.
    private static void PaceWriteBoth(int seed, int records, TechWriter w, RefTechWriter r)
    {
        var rng = new Random(seed);
        for (int part = 0; part < 4; part++)
        {
            char letter = (char)('a' + rng.Next(26));
            w.Part(letter);
            r.Part(letter);
            for (int rec = 0; rec < records; rec++)
            {
                w.Record();
                r.Record();
                int fields = rng.Next(0, 12);
                for (int f = 0; f < fields; f++)
                {
                    switch (rng.Next(4))
                    {
                        case 0:
                            long v = rng.Next(3) == 0 ? PaceLongs[rng.Next(PaceLongs.Length)] : rng.Next(int.MinValue, int.MaxValue);
                            w.Add(v);
                            r.Add(v);
                            break;
                        case 1:
                            bool b = rng.Next(2) == 0;
                            w.Add(b);
                            r.Add(b);
                            break;
                        case 2:
                            string s = PaceTexts[rng.Next(PaceTexts.Length)];
                            w.Add(s);
                            r.Add(s);
                            break;
                        default:
                            string k = rng.Next(3) == 0 ? PaceTexts[rng.Next(PaceTexts.Length)] : PlaceRules.Key(PaceStreets[rng.Next(PaceStreets.Length)], rng.Next(-5, 500));
                            w.Place(k);
                            r.Place(k);
                            break;
                    }
                }
            }
        }
    }

    private static TalentFile PaceTalent(int people)
    {
        var rng = new Random(21);
        var f = new TalentFile { Seed = 77, FirstDay = 3, LastDay = 200, NextPro = 41 };
        for (int i = 0; i < people; i++)
        {
            f.People.Add(new Profile
            {
                Id = i % 9 == 0 ? "pro:" + i : "emp_" + i + "=+/", Role = rng.Next(TalentCatalogue.RoleCount), Level = rng.Next(TalentCatalogue.Levels), A = rng.Next(100000), B = rng.Next(100000),
                Main = rng.Next(100000), Morale = rng.Next(1000), Burnout = rng.Next(1000), Joined = rng.Next(300), ReviewDay = rng.Next(400), PoachBy = PaceTexts[rng.Next(PaceTexts.Length)] ?? string.Empty,
                Place = PlaceRules.Key(PaceStreets[rng.Next(PaceStreets.Length)], rng.Next(1, 300)), Name = PaceTexts[rng.Next(PaceTexts.Length)] ?? string.Empty, Wage = rng.Next(5000),
                NonCompete = rng.Next(2) == 0, Female = rng.Next(2) == 0, Age = 20 + rng.Next(40), WorkDay = rng.Next(300), WorkHours = rng.Next(12)
            });
        }
        return f;
    }

    private static void PaceTechWriterSame()
    {
        bool same = true;
        for (int seed = 1; seed <= 60; seed++)
        {
            var w = new TechWriter(seed % 3);
            var r = new RefTechWriter(seed % 3);
            PaceWriteBoth(seed, seed % 10 == 0 ? 400 : 6, w, r);
            string line = w.ToString();
            same &= line == r.ToString() && ReferenceEquals(line, w.ToString());
        }
        Check(same, "work pace: the save's writer with a kept builder writes every line character for character as it did (numbers, flags, texts, place keys; asked twice, the same line)");

        // Two lines written at once (a writer begun while another is open) are each their own.
        var outer = new TechWriter(1);
        var outerRef = new RefTechWriter(1);
        PaceWriteBoth(5, 20, outer, outerRef);
        var inner = new TechWriter(2);
        var innerRef = new RefTechWriter(2);
        PaceWriteBoth(6, 20, inner, innerRef);
        string innerLine = inner.ToString();
        PaceWriteBoth(7, 20, outer, outerRef);
        Check(outer.ToString() == outerRef.ToString() && innerLine == innerRef.ToString(), "work pace: two lines written at once are each their own");
        bool refused = false;
        try
        {
            inner.Add(1);
        }
        catch (InvalidOperationException)
        {
            refused = true;
        }
        Check(refused, "work pace: a line once written takes nothing more (its builder is the next line's)");

        // The professionals' line at the cap, and back.
        TalentFile file = PaceTalent(TalentStore.PeopleMost);
        string first = TalentStore.Write(file);
        string again = TalentStore.Write(file);
        TalentFile back = TalentStore.Read(first, out int version);
        Check(first == again && back.People.Count == TalentStore.PeopleMost && TalentStore.Write(back) == first && version == TalentStore.Version,
            "work pace: the professionals' line at the cap (" + first.Length + " characters) is the same every time and reads back to itself");
    }

    private static void PaceTechWriterCost()
    {
        TalentFile file = PaceTalent(TalentStore.PeopleMost);
        TalentStore.Write(file);   // (the kept builder grown once)
        long bytes = long.MaxValue;
        int length = 0;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            bytes = Math.Min(bytes, PaceWriteBytes(file, out length));
        }
        Console.WriteLine("  work pace: the professionals' line at the cap, " + length + " characters, allocates " + bytes + " bytes to write");
        Check(bytes < length * 2L + 64 * 1024, "work pace: writing the professionals' line at the cap allocates little beyond the line itself (" + bytes + " bytes for " + length + " characters)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static long PaceWriteBytes(TalentFile file, out int length)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        string line = TalentStore.Write(file);
        long after = GC.GetAllocatedBytesForCurrentThread();
        length = line.Length;
        return after - before;
    }
}
