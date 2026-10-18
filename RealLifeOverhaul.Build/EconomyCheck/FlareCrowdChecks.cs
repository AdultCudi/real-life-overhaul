using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using RealLifeOverhaul;
using RealLifeOverhaul.Flare;

// Flare's city of four hundred and its influencers with no fixed most, and Mom and Alex with one face in every game:
// the first forty people exactly as the generator before made them (every saved "c0" to "c39" is still who it was), the
// rest with names nobody else in the city has, dealt round the districts, of every nature and a fitting age; the day's
// posts, reactions, replies and messages as many as before, spread over all four hundred; the cost of making them and
// of a day; Going Public (the same people from the city's seed on every machine, another version of RLO refused whole,
// a person this version does not have left out); the influencers over years round the level, the safety bound never
// reached, the records kept small; Mom's and Alex's look the same for every seed and mended in a save from before; the
// Guide's figures from the code.
internal static partial class Program
{
    private static void FlareCrowd()
    {
        CrowdFirstForty();
        CrowdNames();
        CrowdSpread();
        CrowdDayVolume();
        CrowdTiming();
        CrowdShare();
        CrowdInfluencers();
        CrowdRetire();
        CrowdFixedLooks();
        CrowdTexts();
    }

    // ---------------------------------------------------------------- the generator before (0.28.3), as it was

    private static readonly string[] OldFirst =
    {
        "Ada", "Bea", "Cal", "Dina", "Eli", "Fay", "Gus", "Hana", "Ivo", "Juno", "Kai", "Lena", "Milo", "Nell", "Otto", "Pia",
        "Quin", "Rae", "Seb", "Tess", "Uma", "Vic", "Wes", "Xena", "Yara", "Zeke", "Arlo", "Bree", "Cyd", "Dex", "Elsa", "Finn"
    };

    private static readonly string[] OldLast =
    {
        "Abbott", "Barlow", "Calder", "Dunmore", "Ellery", "Fenwick", "Garrow", "Hollis", "Imber", "Jarvis", "Kestrel", "Lowry",
        "Maddox", "Norland", "Oakes", "Pemberly", "Quarry", "Ramsey", "Stroud", "Tilney", "Upton", "Varley", "Whitcombe", "Yardley",
        "Ashby", "Brook", "Corwin", "Darby", "Easton", "Frost", "Greer", "Hale"
    };

    private static List<FlareCitizen> OldCitizensOf(int seed, IReadOnlyList<string> districts)
    {
        var into = new List<FlareCitizen>();
        for (int i = 0; i < 40; i++)
        {
            int first = FlareRules.Roll(seed, i, 601) % OldFirst.Length;
            int last = (FlareRules.Roll(seed, i, 602) + i * 7) % OldLast.Length;
            string f = OldFirst[first], l = OldLast[last];
            into.Add(new FlareCitizen
            {
                Index = i, First = f, Last = l,
                Handle = "@" + f.ToLowerInvariant() + "_" + l.ToLowerInvariant() + (10 + FlareRules.Roll(seed, i, 603) % 90),
                District = districts != null && districts.Count > 0 ? districts[FlareRules.Roll(seed, i, 604) % districts.Count] : null,
                Persona = (FlarePersona)(FlareRules.Roll(seed, i, 605) % 8)
            });
        }
        return into;
    }

    private static readonly string[] CrowdDistricts =
    {
        "ba:neighborhood_midtown", "ba:neighborhood_garment", "ba:neighborhood_hamptons", "ba:neighborhood_hellskitchen", "ba:neighborhood_murrayhill",
        "ba:neighborhood_downtown", "ba:neighborhood_queens", "ba:neighborhood_brooklyn", "ba:neighborhood_harbor"
    };

    // ---------------------------------------------------------------- who is who

    private static void CrowdFirstForty()
    {
        var made = new List<FlareCitizen>();
        int seeds = 0, differ = 0;
        string first = null;
        foreach (IReadOnlyList<string> districts in new IReadOnlyList<string>[] { null, new[] { "d1" }, new[] { "d1", "d2", "d3" }, CrowdDistricts })
        {
            for (int seed = -50; seed < 1500; seed += 1)
            {
                List<FlareCitizen> old = OldCitizensOf(seed, districts);
                FlareLiveRules.CitizensOf(seed, districts, made);
                seeds++;
                for (int i = 0; i < 40; i++)
                {
                    FlareCitizen a = old[i], b = made[i];
                    if (a.Index != b.Index || a.First != b.First || a.Last != b.Last || a.Handle != b.Handle || a.District != b.District || a.Persona != b.Persona)
                    {
                        differ++;
                        first = first ?? "seed " + seed + ", c" + i + ": " + a.Handle + " became " + b.Handle;
                    }
                }
            }
        }
        Check(differ == 0 && FlareLiveRules.FirstCitizens == 40, "Flare crowd: the first forty of the city are exactly who the generator before made, for " + seeds + " cities ("
                                                                 + (first ?? "every name, handle, district and nature the same") + ")");
        FlareLiveRules.CitizensOf(1234, CrowdDistricts, made);
        bool numbered = made.Count == FlareLiveRules.Citizens && FlareLiveRules.Citizens == 400;
        for (int i = 0; i < made.Count; i++)
        {
            numbered &= made[i].Index == i;
        }
        Check(numbered, "Flare crowd: the city has four hundred people, numbered in order (" + made.Count + ")");
        var again = new List<FlareCitizen>();
        FlareLiveRules.CitizensOf(1234, CrowdDistricts, again);
        bool same = again.Count == made.Count;
        for (int i = 0; same && i < made.Count; i++)
        {
            same = again[i].Handle == made[i].Handle && again[i].District == made[i].District && again[i].Age == made[i].Age && again[i].Female == made[i].Female;
        }
        Check(same, "Flare crowd: the same city's seed makes the same four hundred on every machine");
        bool alone = true;
        for (int seed = 0; seed < 200; seed++)
        {
            FlareLiveRules.CitizensOf(seed, CrowdDistricts, made);
            for (int i = 0; i < made.Count; i++)
            {
                alone &= FlareLiveRules.CitizenFemale(seed, i) == made[i].Female && FlareLiveRules.CitizenAge(seed, i) == made[i].Age;
            }
        }
        Check(alone, "Flare crowd: anybody's sex and age can be worked out on their own, without the list (for their picture)");
    }

    private static void CrowdNames()
    {
        var made = new List<FlareCitizen>();
        int clashes = 0, oldClashCities = 0, cities = 0, mostOfOneFirst = 0;
        bool handlesPlain = true;
        string clash = null;
        for (int seed = 1; seed <= 1000; seed++)
        {
            FlareLiveRules.CitizensOf(seed * 7919, CrowdDistricts, made);
            cities++;
            var names = new HashSet<string>(StringComparer.Ordinal);
            var firsts = new Dictionary<string, int>(StringComparer.Ordinal);
            bool oldClash = false;
            for (int i = 0; i < made.Count; i++)
            {
                string full = made[i].First + " " + made[i].Last;
                if (!names.Add(full))
                {
                    if (i >= FlareLiveRules.FirstCitizens)
                    {
                        clashes++;
                        clash = clash ?? full + " (c" + i + ", city " + seed + ")";
                    }
                    else
                    {
                        oldClash = true;   // (two of the first forty may share a name, as they always could: they keep it)
                    }
                }
                firsts[made[i].First] = firsts.TryGetValue(made[i].First, out int n) ? n + 1 : 1;
                foreach (char c in made[i].Handle.Substring(1))
                {
                    handlesPlain &= char.IsLetterOrDigit(c) && c < 128 || c == '_';
                }
            }
            foreach (KeyValuePair<string, int> kv in firsts)
            {
                mostOfOneFirst = Math.Max(mostOfOneFirst, kv.Value);
            }
            oldClashCities += oldClash ? 1 : 0;
        }
        Check(clashes == 0, "Flare crowd: nobody after the first forty shares a full name with anybody in their city, over " + cities + " cities (" + (clash ?? "none") + "; "
                            + oldClashCities + " cities have two of the first forty alike, kept as saves know them)");
        Check(mostOfOneFirst <= 16, "Flare crowd: no first name is everywhere: at most " + mostOfOneFirst + " people of four hundred share one");
        Check(handlesPlain, "Flare crowd: every handle is plain letters, digits and an underscore");
        // The name banks: no name twice, no first name both a woman's and a man's, every one a plain word.
        bool banks = true;
        foreach (string bank in new[] { "Women", "Men", "Surnames" })
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in Bank(bank))
            {
                banks &= seen.Add(name) && name.Length >= 2 && char.IsUpper(name[0]);
                foreach (char c in name)
                {
                    banks &= char.IsLetter(c) && c < 128;
                }
            }
        }
        var women = new HashSet<string>(Bank("Women"), StringComparer.Ordinal);
        foreach (string man in Bank("Men"))
        {
            banks &= !women.Contains(man);
        }
        Check(banks && Bank("Women").Length >= 50 && Bank("Men").Length >= 50 && Bank("Surnames").Length >= 100,
            "Flare crowd: the name banks have " + Bank("Women").Length + " women's and " + Bank("Men").Length + " men's first names and " + Bank("Surnames").Length + " surnames, none twice");
    }

    private static string[] Bank(string name)
    {
        return (string[])typeof(FlareLiveRules).GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null);
    }

    private static void CrowdSpread()
    {
        var made = new List<FlareCitizen>();
        bool even = true, ages = true, mixed = true, personas = true;
        string worst = null;
        for (int seed = 0; seed < 300; seed++)
        {
            FlareLiveRules.CitizensOf(seed * 31 + 5, CrowdDistricts, made);
            var after = new int[CrowdDistricts.Length];
            var all = new int[CrowdDistricts.Length];
            var kinds = new int[8];
            int women = 0;
            for (int i = 0; i < made.Count; i++)
            {
                int d = Array.IndexOf(CrowdDistricts, made[i].District);
                all[d]++;
                after[d] += i >= FlareLiveRules.FirstCitizens ? 1 : 0;
                kinds[(int)made[i].Persona]++;
                women += made[i].Female ? 1 : 0;
                int age = made[i].Age;
                switch (made[i].Persona)
                {
                    case FlarePersona.Student: ages &= age >= 18 && age <= 25; break;
                    case FlarePersona.Parent: ages &= age >= 27 && age <= 50; break;
                    case FlarePersona.Retiree: ages &= age >= 65 && age <= 82; break;
                    default: ages &= age >= 20 && age <= 64; break;
                }
            }
            int lo = int.MaxValue, hi = 0, allLo = int.MaxValue, allHi = 0;
            for (int d = 0; d < after.Length; d++)
            {
                lo = Math.Min(lo, after[d]);
                hi = Math.Max(hi, after[d]);
                allLo = Math.Min(allLo, all[d]);
                allHi = Math.Max(allHi, all[d]);
            }
            even &= allHi - allLo <= 1;
            worst = !even && worst == null ? "city " + seed + ": " + allLo + " to " + allHi + " a district" : worst;
            mixed &= women >= 160 && women <= 240;
            foreach (int k in kinds)
            {
                personas &= k >= 25 && k <= 80;
            }
        }
        Check(even, "Flare crowd: the city's people are spread evenly over its nine districts, 44 or 45 in each, the first forty where they always were (" + (worst ?? "every city") + ")");
        Check(personas && mixed, "Flare crowd: every nature has its share of the four hundred, and women and men about half each");
        Check(ages, "Flare crowd: ages suit what people are like (students 18 to 25, parents 27 to 50, retirees 65 to 82, anybody else 20 to 64)");
        FlareLiveRules.CitizensOf(9, null, made);
        bool none = made.Count == 400;
        foreach (FlareCitizen c in made)
        {
            none &= c.District == null && c.First != null && c.Last != null;
        }
        Check(none, "Flare crowd: a city with no districts known still has its four hundred, none placed");
    }

    // ---------------------------------------------------------------- the day's volume, the same as before

    private static void CrowdDayVolume()
    {
        int posts400 = 0, posts40 = 0, talk400 = 0, talk40 = 0;
        var seen = new HashSet<int>();
        bool inRange = true;
        for (int day = 10; day < 70; day++)
        {
            FlareFile a = FlareLiveFile(77), b = FlareLiveFile(77);
            FlareCityFacts facts = FlareFakeCity(day, 1, day % 4 == 0);
            List<FlareCitizen> all = FlareCitizensOf(a, facts);
            posts400 += FlareLiveRules.CityDay(a, facts, all, null);
            posts40 += FlareLiveRules.CityDay(b, FlareFakeCity(day, 1, day % 4 == 0), all.GetRange(0, 40), null);
            foreach (FlarePost p in a.Posts)
            {
                if (p.Author == FlareAuthor.Citizen)
                {
                    talk400++;
                    seen.Add(p.Who);
                    inRange &= p.Who >= 0 && p.Who < FlareLiveRules.Citizens;
                }
            }
            foreach (FlarePost p in b.Posts)
            {
                talk40 += p.Author == FlareAuthor.Citizen ? 1 : 0;
            }
        }
        Check(posts400 == posts40 && talk400 == talk40 && talk400 == 60 * FlareLiveRules.CityTalkPerDay[1],
            "Flare crowd: the city posts as much a day with four hundred people as with forty (" + posts400 + " and " + posts40 + " posts over sixty days, " + talk400 + " of its people's)");
        Check(inRange && seen.Count > 300, "Flare crowd: the day's posts come from all over the four hundred (" + seen.Count + " different people over sixty days)");
        // Reactions and replies to a post: as many as before (by how far it went), from anybody of the four hundred, evenly.
        var b2 = new StringBuilder(256);
        var r = new List<FlareReaction>();
        var who = new int[FlareLiveRules.Citizens];
        int mostReactions = 0, mostReplies = 0, low = 0, high = 0;
        for (int i = 0; i < 4000; i++)
        {
            var p = new FlarePost { Id = i, Seed = i * 7919 + 3, Reach = 200 + i % 3000, Bad = i % 9 == 0, Viral = i % 13 == 0, Author = FlareAuthor.Player, Day = 1 };
            FlareLiveRules.ReadReactors(FlareLiveRules.Reactors(p, null, null, false, b2), r);
            int c = 0;
            foreach (FlareReaction x in r)
            {
                if (x.Who.StartsWith("c", StringComparison.Ordinal) && int.TryParse(x.Who.Substring(1), out int n))
                {
                    c++;
                    who[n]++;
                    low += n < 200 ? 1 : 0;
                    high += n >= 200 ? 1 : 0;
                }
            }
            mostReactions = Math.Max(mostReactions, c);
            FlareFile f = FlareLiveFile(3);
            var made = new List<FlarePost>();
            mostReplies = Math.Max(mostReplies, FlareLiveRules.CityReplies(f, p, made));
            foreach (FlarePost reply in made)
            {
                inRange &= reply.Who >= 0 && reply.Who < FlareLiveRules.Citizens;
            }
        }
        Check(mostReactions <= FlareLiveRules.CitizensReactMost && mostReplies <= FlareLiveRules.PileOnMost && inRange,
            "Flare crowd: a post draws as many of the city's people as before, at most " + mostReactions + " reactions and " + mostReplies + " replies");
        Check(Math.Abs(low - high) * 10 < low + high, "Flare crowd: the people who react are picked evenly from all four hundred (" + low + " of the first half, " + high + " of the second)");
        // Messages: one every two days at most, from anybody.
        FlareFile m = FlareLiveFile(8);
        m.LastPostDay = 1000;
        int dms = 0, gaps = 0, last = -100, highDm = 0;
        for (int day = 0; day < 1000; day++)
        {
            m.LastPostDay = day;
            if (FlareLiveRules.CityDm(m, day, day % 3 == 0, out string with, out string what))
            {
                dms++;
                gaps += day - last < FlareLiveRules.DmGapDays ? 1 : 0;
                last = day;
                highDm += with.StartsWith("c", StringComparison.Ordinal) && int.Parse(with.Substring(1)) >= 40 ? 1 : 0;
            }
        }
        Check(dms > 100 && dms <= 500 && gaps == 0 && highDm > dms / 2, "Flare crowd: the city's people write once every two days at most, as before (" + dms + " in a thousand days, " + highDm + " from people after the first forty)");
    }

    // ---------------------------------------------------------------- the cost

    private static void CrowdTiming()
    {
        CrowdBuildCost(400);
        double build40 = CrowdBuildCost(40), build400 = CrowdBuildCost(400);
        double day40 = CrowdDayCost(40), day400 = CrowdDayCost(400);
        Console.WriteLine("  Flare crowd: making the city's people " + build40.ToString("0.000") + " ms for 40, " + build400.ToString("0.000") + " ms for 400; a day's posts "
                          + day40.ToString("0.000") + " ms with 40, " + day400.ToString("0.000") + " ms with 400");
        Check(build400 < 2.0, "Flare crowd: the four hundred are made in " + build400.ToString("0.000") + " ms (once for a seed: Flare starting, or a guest taking the host's city)");
        Check(day400 < 1.0 && day400 < day40 * 3 + 0.05, "Flare crowd: a day's posts cost about the same with four hundred as with forty (" + day40.ToString("0.000") + " and " + day400.ToString("0.000") + " ms)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double CrowdBuildCost(int count)
    {
        const int rounds = 200;
        var made = new List<FlareCitizen>(400);
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < rounds; i++)
        {
            FlareLiveRules.CitizensOf(i * 977 + 13, CrowdDistricts, made);
            if (count < made.Count)
            {
                made.RemoveRange(count, made.Count - count);   // (forty: the generator before made only these; the list's cost is near the same)
            }
        }
        watch.Stop();
        double all = watch.Elapsed.TotalMilliseconds / rounds;
        if (count >= FlareLiveRules.Citizens)
        {
            return all;
        }
        watch.Restart();
        for (int i = 0; i < rounds; i++)
        {
            List<FlareCitizen> old = OldCitizensOf(i * 977 + 13, CrowdDistricts);
            made.Clear();
            made.AddRange(old);
        }
        watch.Stop();
        return watch.Elapsed.TotalMilliseconds / rounds;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double CrowdDayCost(int count)
    {
        const int rounds = 60;
        var watch = new Stopwatch();
        var made = new List<FlarePost>(32);
        for (int i = 0; i < rounds; i++)
        {
            FlareFile f = FlareLiveFile(300 + i);
            FlareCityFacts facts = FlareFakeCity(10 + i, 2, true);
            List<FlareCitizen> cast = FlareCitizensOf(f, facts);
            if (count < cast.Count)
            {
                cast.RemoveRange(count, cast.Count - count);
            }
            watch.Start();
            FlareLiveRules.CityDay(f, facts, cast, made);
            watch.Stop();
        }
        return watch.Elapsed.TotalMilliseconds / rounds;
    }

    // ---------------------------------------------------------------- Going Public

    private static void CrowdShare()
    {
        // The host's post by one of the people after the first forty reads on a guest of this version, who has the same four hundred.
        FlareFile host = FlareLiveFile(31);
        host.Posts.Add(new FlarePost { Id = 1, Day = 4, Hour = 9, Author = FlareAuthor.Citizen, Who = 250, Source = FlareSource.CityTalk, Arg = 12, Reach = 100, Topic = "city" });
        host.Posts.Add(new FlarePost { Id = 2, Day = 4, Hour = 9, Author = FlareAuthor.Citizen, Who = 3, Source = FlareSource.CityTalk, Arg = 12, Reach = 100, Topic = "city" });
        string whole = FlareNetShare.WriteWhole(host, 4, "Host", "Hana", true, null, 1);
        var read = new List<int>();
        foreach (string line in whole.Split('\n'))
        {
            if (FlareNetShare.ReadPost(line.Split('|'), out FlarePost p, out int reply) && p.Author == FlareAuthor.Citizen)
            {
                read.Add(p.Who);
            }
        }
        var hostPeople = new List<FlareCitizen>();
        var guestPeople = new List<FlareCitizen>();
        FlareLiveRules.CitizensOf(FlareLiveRules.CitySeed(host), CrowdDistricts, hostPeople);
        FlareLiveRules.CitizensOf(host.Net.CitySeed, CrowdDistricts, guestPeople);
        Check(read.Count == 2 && read.Contains(250) && read.Contains(3) && hostPeople[250].Handle == guestPeople[250].Handle,
            "Flare crowd: a host's post by c250 reaches a guest of this version as the same person (" + guestPeople[250].Handle + ")");
        // A person this version does not have (a later one's, or a line gone wrong) is left out, as any line it cannot read.
        bool outOfRange = true;
        foreach (int who in new[] { -1, FlareLiveRules.Citizens, 99999 })
        {
            var b = new StringBuilder();
            FlareNetShare.WritePost(b, host, new FlarePost { Id = 9, NetId = 9, Day = 4, Hour = 9, Author = FlareAuthor.Citizen, Who = who, Source = FlareSource.CityTalk, Arg = 12 }, "Host", "Hana");
            outOfRange &= !FlareNetShare.ReadPost(b.ToString().Split('|'), out FlarePost _, out int _);
        }
        var ok = new StringBuilder();
        FlareNetShare.WritePost(ok, host, new FlarePost { Id = 9, NetId = 9, Day = 4, Hour = 9, Author = FlareAuthor.Citizen, Who = FlareLiveRules.Citizens - 1, Source = FlareSource.CityTalk, Arg = 12 }, "Host", "Hana");
        Check(outOfRange && FlareNetShare.ReadPost(ok.ToString().Split('|'), out FlarePost _, out int _),
            "Flare crowd: a post by somebody this version's city does not have is left out on a guest, the last of the four hundred read");
        // An RLO of 40 people (0.28.3) and one of 400 never mix: the shared city refuses another version whole, so each keeps its own city.
        var m = new SharedMessage { Kind = SharedKind.Update, Version = "0.28.4", Sequence = 3 };
        m.Parts.Add(new SharedPart(FlareNetShare.Part, "P|1"));
        byte[] bytes = SharedCityWire.Write(m);
        Check(SharedCityWire.Read(bytes, "0.28.3", out SharedMessage _) == SharedRead.OtherVersion && SharedCityWire.Read(bytes, "0.28.4", out SharedMessage _) == SharedRead.Ok,
            "Flare crowd: a host with four hundred and a guest with forty (or the reverse) share nothing: another version's message is read as another version's");
        // A guest that follows makes none of the city's posts, whatever the city's size.
        FlareFile guest = FlareLiveFile(31);
        FlareCityFacts follow = FlareFakeCity(5, 1, true);
        follow.Following = true;
        Check(FlareLiveRules.CityDay(guest, follow, FlareCitizensOf(guest, follow), null) == 0, "Flare crowd: a guest that follows makes none of the city's posts");
        // Nothing is saved for the people: the posts' line names them by number only.
        FlareFile f = FlareLiveFile(41);
        FlareLiveRules.CityDay(f, FlareFakeCity(12, 1, true), FlareCitizensOf(f, FlareFakeCity(12, 1, true)), null);
        string saved = FlareStore.WritePosts(f);
        var everyone = FlareCitizensOf(f, FlareFakeCity(12, 1, true));
        bool named = false;
        foreach (FlareCitizen c in everyone)
        {
            named |= saved.Contains(c.Handle);
        }
        Check(!named, "Flare crowd: the city's people are made from its seed and never saved (no handle in the posts' line, " + saved.Length + " characters)");
    }

    // ---------------------------------------------------------------- the influencers, round the level

    private static void CrowdInfluencers()
    {
        Check(FlareCast.Start >= 15 && FlareCast.Start <= 20 && FlareCast.Level == FlareLiveRules.Citizens / FlareCast.CitizensEach && FlareCast.ActiveSoft >= FlareCast.Level * 3,
            "Flare crowd: the city starts with " + FlareCast.Start + " influencers, settles round " + FlareCast.Level + " for four hundred people, a safety bound at " + FlareCast.ActiveSoft);
        Check(FlareCast.ArrivalChance(0) > FlareCast.ArrivalChance(FlareCast.Level) && FlareCast.ArrivalChance(FlareCast.Level) > 0 && FlareCast.ArrivalChance(FlareCast.Level * 2) == 0,
            "Flare crowd: newcomers come more often to a small cast, now and then at the level, none at twice it");
        Check(FlareCast.PostChanceOf(FlareCast.PostsLevel) == FlareCast.PostChance && FlareCast.PostChanceOf(FlareCast.PostsLevel * 2) == FlareCast.PostChance / 2,
            "Flare crowd: more influencers than the feed's share post a little less often each");
        var heat = new int[(int)FlareNiche.Business + 1];
        var news = new List<FlareCastNews>();
        var shops = new List<FlareShop> { new FlareShop { Place = "A~1", Name = "Shop", Niche = FlareNiche.Food, District = FlareDistricts[0] } };
        var visits = new List<FlarePost>();
        int lowest = int.MaxValue, highest = 0, rises = 0, falls = 0, softHit = 0, keptMost = 0;
        long sum = 0, months = 0, posts = 0, days = 0;
        string trace = null;
        for (int seed = 1; seed <= 12; seed++)
        {
            FlareFile f = FlareCastFile(400 + seed);
            int prev = f.ActiveInfluencers;
            var line = new StringBuilder();
            for (int day = 2; day <= 365 * 6; day++)
            {
                if (day % FlareCast.MonthDays == 0)
                {
                    heat[(int)FlareNiche.Food] = seed % 3 == 0 ? 750 : 250;
                    heat[(int)FlareNiche.Nightlife] = seed % 4 == 0 ? 300 : 0;
                    news.Clear();
                    FlareCast.Month(f, day, heat, null, seed % 2 == 0, FlareDistricts, news);
                    int now = f.ActiveInfluencers;
                    rises += now > prev ? 1 : 0;
                    falls += now < prev ? 1 : 0;
                    prev = now;
                    softHit += now >= FlareCast.ActiveSoft ? 1 : 0;
                    keptMost = Math.Max(keptMost, f.Influencers.Count);
                    if (day > 365)
                    {
                        lowest = Math.Min(lowest, now);
                        highest = Math.Max(highest, now);
                        sum += now;
                        months++;
                    }
                    if (seed == 1 && day % (FlareCast.MonthDays * 6) == 0)
                    {
                        line.Append(line.Length > 0 ? " " : string.Empty).Append(now);
                    }
                }
                int before = f.Posts.Count;
                int made = 0;
                visits.Clear();
                FlareCast.Day(f, day, shops, null, null, null, visits);
                foreach (FlarePost p in f.Posts)
                {
                    made += p.Author == FlareAuthor.Influencer && p.Day == day ? 1 : 0;
                }
                if (day > 365)
                {
                    posts += made;
                    days++;
                }
            }
            trace = trace ?? line.ToString();
        }
        double mean = (double)sum / Math.Max(1, months), perDay = (double)posts / Math.Max(1, days);
        Console.WriteLine("  Flare crowd: active influencers over six years, every half year (one city): " + trace + "; after the first year " + lowest + " to " + highest + ", "
                          + mean.ToString("0.0") + " on average; " + perDay.ToString("0.0") + " of their posts a day; at most " + keptMost + " records kept");
        Check(lowest >= FlareCast.Level / 2 && highest <= FlareCast.Level * 2 && mean >= FlareCast.Level - 4 && mean <= FlareCast.Level + 4,
            "Flare crowd: over six years the influencers stay round the level (" + lowest + " to " + highest + ", " + mean.ToString("0.0") + " on average, level " + FlareCast.Level + ")");
        Check(rises > 20 && falls > 20, "Flare crowd: the number grows and shrinks as the months go (" + rises + " months up, " + falls + " down), with no runaway");
        Check(softHit == 0 && keptMost < FlareCast.KeptMost / 2, "Flare crowd: the safety bound is never reached in play and the records stay few (" + keptMost + " at most, bound " + FlareCast.KeptMost + ")");
        Check(perDay <= 10.0, "Flare crowd: the influencers' own posts a day stay about what ten made before (" + perDay.ToString("0.0") + ")");
        // A cast far over the level (a save of a later version, or a test) is pulled back down, and none come while it is.
        FlareFile big = FlareCastFile(9);
        for (int i = 0; i < 40; i++)
        {
            FlareCast.New(big, (FlareNiche)(i % 6), FlareDistricts[0], 1, 3000 + i * 100, -1, false);
        }
        int start = big.ActiveInfluencers, arrived = 0;
        for (int mo = 1; mo <= 24; mo++)
        {
            news.Clear();
            FlareCast.Month(big, 30 * mo + 1, heat, null, false, FlareDistricts, news);
            foreach (FlareCastNews n in news)
            {
                arrived += n.Kind == FlareCastNews.What.Arrived ? 1 : 0;
            }
        }
        Check(big.ActiveInfluencers < start - 10, "Flare crowd: a crowded cast thins out (" + start + " to " + big.ActiveInfluencers + " in two years, " + arrived + " newcomers)");
        // The safety bound: at it, no newcomer at all, whatever draws them.
        FlareFile full = FlareCastFile(10);
        while (full.ActiveInfluencers < FlareCast.ActiveSoft)
        {
            FlareCast.New(full, FlareNiche.Food, FlareDistricts[0], 1, 90000, -1, false);
        }
        int count = full.Influencers.Count;
        Array.Clear(heat, 0, heat.Length);
        heat[0] = 1000;
        full.ViralNiche = 0;
        full.ViralNicheDay = 60;
        var friends = new List<FlarePerson> { new FlarePerson { Index = 2, First = "Sam", Stage = 3, Taste = FlareTaste.Sport, Closeness = 60 } };
        int bornAt = 0;
        for (int mo = 2; mo < 14; mo++)
        {
            news.Clear();
            FlareCast.Month(full, 30 * mo, heat, friends, true, FlareDistricts, news);
            foreach (FlareCastNews n in news)
            {
                bornAt += n.Kind == FlareCastNews.What.Arrived && full.ActiveInfluencers > FlareCast.ActiveSoft ? 1 : 0;
            }
        }
        Check(bornAt == 0, "Flare crowd: never a newcomer past the safety bound (" + count + " records)");
        // The save: every record kept is read back (the bound before dropped the newest past forty).
        FlareFile many = FlareCastFile(12);
        for (int i = 0; i < 60; i++)
        {
            FlareInfluencer inf = FlareCast.New(many, FlareNiche.Tech, FlareDistricts[1], 1, 5000, -1, false);
            inf.Quit = i % 2 == 0;
        }
        FlareFile back = FlareRules.Open(12, 1, 0, 50);
        FlareStore.ReadCast(back, FlareStore.WriteCast(many));
        Check(back.Influencers.Count == many.Influencers.Count && back.ActiveInfluencers == many.ActiveInfluencers,
            "Flare crowd: a save keeps every influencer, " + many.Influencers.Count + " of them (" + back.Influencers.Count + " read back)");
    }

    private static void CrowdRetire()
    {
        FlareFile f = FlareCastFile(61);
        FlareInfluencer gone = FlareCast.New(f, FlareNiche.Food, FlareDistricts[0], 1, 500, -1, false);
        FlareInfluencer posted = FlareCast.New(f, FlareNiche.Food, FlareDistricts[0], 1, 500, -1, false);
        FlareInfluencer followed = FlareCast.New(f, FlareNiche.Food, FlareDistricts[0], 1, 500, -1, false);
        FlareInfluencer hired = FlareCast.New(f, FlareNiche.Food, FlareDistricts[0], 1, 500, -1, false);
        FlareInfluencer wrote = FlareCast.New(f, FlareNiche.Food, FlareDistricts[0], 1, 500, -1, false);
        FlareInfluencer friend = FlareCast.New(f, FlareNiche.Food, FlareDistricts[0], 1, 500, 3, false);
        foreach (FlareInfluencer inf in new[] { gone, posted, followed, hired, wrote, friend })
        {
            inf.Quit = true;
        }
        f.Posts.Add(new FlarePost { Id = 900, Author = FlareAuthor.Influencer, Who = posted.Id, Day = 1 });
        f.Follows.Add("i:" + followed.Id);
        var hire = new FlareHire { Id = 1, Place = "A~1", From = 1, To = 5 };
        hire.Who.Add(hired.Id);
        f.Hires.Add(hire);
        FlareLiveRules.Dm(f.Net, "i:" + wrote.Id, "x", 10, false, null, "reallife_flare_dm_collab", null);
        int active = f.ActiveInfluencers;
        int went = FlareCast.Retire(f);
        Check(went == 1 && f.Influencer(gone.Id) == null && f.Influencer(posted.Id) != null && f.Influencer(followed.Id) != null && f.Influencer(hired.Id) != null
              && f.Influencer(wrote.Id) != null && f.Influencer(friend.Id) != null && f.ActiveInfluencers == active,
            "Flare crowd: one who stopped and is named by nothing goes; one with a post, a follow, a deal or a message, a friend, and every active one stay");
        FlareFile crowd = FlareCastFile(62);
        for (int i = 0; i < FlareCast.KeptMost + 30; i++)
        {
            FlareInfluencer inf = FlareCast.New(crowd, FlareNiche.Food, FlareDistricts[0], 1, 500, -1, false);
            inf.Quit = true;
            crowd.Follows.Add("i:" + inf.Id);   // (every one named: only the bound lets them go)
        }
        FlareCast.Retire(crowd);
        Check(crowd.Influencers.Count == FlareCast.KeptMost && crowd.ActiveInfluencers == FlareCast.Start, "Flare crowd: past the records' bound the oldest who stopped go, never an active one ("
                                                                                                         + crowd.Influencers.Count + " kept)");
    }

    // ---------------------------------------------------------------- Mom and Alex

    private static void CrowdFixedLooks()
    {
        var people = new SocialPerson[SocialRules.CastSize];
        bool fixedAll = true, others = false;
        int firstOther = 0;
        for (int seed = 1; seed <= 2000; seed += 3)
        {
            SocialCatalogue.MakeCast(people, seed, 0, 112, 9);
            SocialPerson mom = people[(int)SocialOrigin.Family], alex = people[(int)SocialOrigin.OldFriend];
            fixedAll &= mom.Name == "Mom" && mom.Female && mom.Age == FixedLooks.MomAge && mom.Seed == FixedLooks.MomSeed;
            fixedAll &= alex.Name == "Alex" && alex.Female == FixedLooks.AlexFemale && alex.Age == FixedLooks.AlexAge && alex.Seed == FixedLooks.AlexSeed;
            others |= firstOther != 0 && people[2].Seed != firstOther;
            firstOther = firstOther == 0 ? people[2].Seed : firstOther;
        }
        Check(fixedAll && others && FixedLooks.MomSeed != FixedLooks.AlexSeed, "Flare crowd: Mom (a woman of " + FixedLooks.MomAge + ") and Alex (a man of " + FixedLooks.AlexAge
                                                                            + ") have the same look for every new game's seed, and the people to be met are still each game's own");
        // A save from before, with Alex rolled a woman of 33 and Mom of 61, each a seed of their own: mended as it is read, and only that.
        SocialCatalogue.MakeCast(people, 4242, 0, 112, 9);
        var data = new Dictionary<string, string>();
        SocialState.Load(data, 0, 112, 9, 4242);
        SocialState.Save(data);
        string lines = data["reallife:social_people"];
        string[] records = lines.Split('\n');
        records[0] = Field(records[0], 2, "1");
        records[0] = Field(records[0], 3, "61");
        records[0] = Field(records[0], 4, "998877");
        records[1] = Field(records[1], 2, "1");
        records[1] = Field(records[1], 3, "33");
        records[1] = Field(records[1], 4, "112233");
        data["reallife:social_people"] = string.Join("\n", records);
        int thirdSeed = SocialState.Get(2).Seed;
        string thirdName = SocialState.Get(2).Name;
        SocialState.Load(data, 5, 112, 9, 999);
        SocialPerson m2 = SocialState.Get(0), a2 = SocialState.Get(1);
        Check(m2 != null && a2 != null && m2.Seed == FixedLooks.MomSeed && m2.Age == FixedLooks.MomAge && m2.Female && a2.Seed == FixedLooks.AlexSeed && a2.Age == FixedLooks.AlexAge
              && a2.Female == FixedLooks.AlexFemale && m2.Name == "Mom" && a2.Name == "Alex" && SocialState.Get(2).Seed == thirdSeed && SocialState.Get(2).Name == thirdName,
            "Flare crowd: a save from before has Mom's and Alex's own looks changed to the one of every game as it is read, their names and everybody else as they were");
        var again = new Dictionary<string, string>();
        SocialState.Save(again);
        Check(again["reallife:social_people"].Split('\n')[1].Split('|')[4] == FixedLooks.AlexSeed.ToString(), "Flare crowd: and saved so");
        // The contacts: with the social life off, Mom's and the old friend's pictures are the same look, never a seed of the save's.
        bool contacts = ContactCatalogue.TryGet("reallife-mom", out ContactLook momLook) && ContactCatalogue.TryGet("reallife-friend", out ContactLook friendLook)
                        && momLook.Seed == FixedLooks.MomSeed && momLook.Age == FixedLooks.MomAge && momLook.Female == FixedLooks.MomFemale
                        && friendLook.Seed == FixedLooks.AlexSeed && friendLook.Age == FixedLooks.AlexAge && friendLook.Female == FixedLooks.AlexFemale;
        Check(contacts, "Flare crowd: Mom's and the old friend's contact pictures (the social life off) have the same look as in the street");
        // Every place that builds their face takes it from the one look: the street, the contact, the wedding's mother.
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string family = File.ReadAllText(System.IO.Path.Combine(scripts, "Family", "FamilyCast.cs"));
        string social = File.ReadAllText(System.IO.Path.Combine(scripts, "Social", "SocialCast.cs"));
        string pictures = File.ReadAllText(System.IO.Path.Combine(scripts, "Phone", "Contacts", "ContactPictures.cs"));
        Check(family.Contains("MotherSeed = FixedLooks.MomSeed") && family.Contains("age = FixedLooks.MomAge") && social.Contains("UnityEngine.Random.InitState(who.Seed)")
              && pictures.Contains("look.Seed != 0 ? look.Seed : ContactCatalogue.Seed(id, save)"),
            "Flare crowd: the wedding's mother, the street character (with the beard's dice from the seed) and the contact all draw Mom and Alex from the one look");
    }

    private static string Field(string record, int index, string value)
    {
        string[] f = record.Split('|');
        f[index] = value;
        return string.Join("|", f);
    }

    // ---------------------------------------------------------------- the Guide and What's new

    private static void CrowdTexts()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("Flare crowd", words, codes))
        {
            return;
        }
        bool facts = true, old = false;
        for (int l = 0; l < 3; l++)
        {
            string page = words[l]["reallife_guide_flare"];
            facts &= page.Contains("{flare_cast_level}") && page.Contains("{flare_cast_start}") && page.Contains("{flare_citizens}") && words[l]["reallife_guide_social"].Contains("Alex");
            old |= page.Contains("{flare_cast_most}");
        }
        string meta = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Meta"));
        string list = File.ReadAllText(System.IO.Path.Combine(meta, "FeatureList.cs")) + File.ReadAllText(System.IO.Path.Combine(meta, "FeatureList.Life.cs"))
                      + File.ReadAllText(System.IO.Path.Combine(meta, "FeatureList.Economy.cs"));
        Check(facts && !old && list.Contains(".Fact(\"flare_cast_level\", () => Whole(Flare.FlareCast.Level))") && list.Contains(".Fact(\"flare_cast_start\", () => Whole(Flare.FlareCast.Start))")
              && list.Contains(".Fact(\"flare_citizens\", () => Whole(Flare.FlareLiveRules.Citizens))") && !list.Contains("flare_cast_most"),
            "Flare crowd: the Guide's Flare page gives the city's people, the influencers' start and level from the code, in en, pt and zh-cn, and no longer a most");
        // any reallife_changes_ row: the line is in the next row until a release numbers it
        bool Row(Dictionary<string, string> t, string part)
        {
            foreach (var r in t)
                if (r.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !r.Key.EndsWith("_title", StringComparison.Ordinal) && r.Value.Contains(part)) return true;
            return false;
        }
        bool next = Row(words[0], "400 people") && Row(words[0], "Mom and Alex look the same in every game.") && Row(words[1], "400 pessoas") && Row(words[2], "400");
        Check(next, "Flare crowd: What's new has the four hundred, the influencers and Mom's and Alex's faces in en, pt and zh-cn");
    }
}
