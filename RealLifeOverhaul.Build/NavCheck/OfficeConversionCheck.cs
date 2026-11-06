using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using RealLifeOverhaul.Offices;

// The game-free parts of the empty shops made offices (Offices/ConversionRules.cs, ConversionStore.cs): how many in each
// district with offices and in the city, which shops (the same on every machine, whatever order the city lists them
// in), never a shop that is not empty and to let, never in a district without offices, never more than half a
// district's shops, one somebody has kept as an office, one no longer needed a shop again, and the save's line.
internal static partial class Program
{
    // The city as the game ships it (Build 3682, the buildings' own assets): offices of the game's and shops whose
    // structure the game shares with its offices (size C version 1, size D version 2), by district.
    private static readonly (string District, int Offices, int Shops)[] GameCity =
    {
        ("ba:neighborhood_garmentdistrict", 18, 9),
        ("ba:neighborhood_hellskitchen", 8, 9),
        ("ba:neighborhood_lowermanhattan", 15, 8),
        ("ba:neighborhood_midtown", 29, 13),
        ("ba:neighborhood_murrayhill", 15, 11),
        ("ba:neighborhood_industrycity", 0, 36),
        ("ba:neighborhood_thehamptons", 0, 10),
    };

    private static List<ConversionRow> City(Func<string, int, bool> officeFree, Func<string, int, bool> shopFree)
    {
        var rows = new List<ConversionRow>();
        foreach (var d in GameCity)
        {
            for (int i = 0; i < d.Offices; i++)
            {
                rows.Add(new ConversionRow { Street = d.District + "_officestreet", Number = i + 1, District = d.District, Office = true, Free = officeFree(d.District, i) });
            }
            for (int i = 0; i < d.Shops; i++)
            {
                rows.Add(new ConversionRow { Street = d.District + "_shopstreet", Number = 100 + i * 2, District = d.District, Office = false, Free = shopFree(d.District, i) });
            }
        }
        return rows;
    }

    private static HashSet<string> Chosen(IList<ConversionRow> rows, out int free)
    {
        var want = new bool[rows.Count];
        free = ConversionRules.Decide(rows, want);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < rows.Count; i++)
        {
            if (want[i])
            {
                keys.Add(ConversionRules.Key(rows[i].Street, rows[i].Number));
            }
        }
        return keys;
    }

    private static int In(IList<ConversionRow> rows, HashSet<string> chosen, string district)
    {
        int n = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            n += rows[i].District == district && chosen.Contains(ConversionRules.Key(rows[i].Street, rows[i].Number)) ? 1 : 0;
        }
        return n;
    }

    private static void OfficeConversions()
    {
        // The player's report: every office of the game's is taken (the mod's claims and the game's businesses), every shop empty.
        List<ConversionRow> full = City((d, i) => false, (d, i) => true);
        HashSet<string> chosen = Chosen(full, out int free);
        bool two = true;
        foreach (var d in GameCity)
        {
            int n = In(full, chosen, d.District);
            two &= d.Offices > 0 ? n == ConversionRules.DistrictFloor : n == 0;
        }
        Check(two, "offices: with every office taken, each district with offices has exactly " + ConversionRules.DistrictFloor + " shops made offices and the districts without offices none");
        Check(free == 10 && free >= ConversionRules.CityFloor, "offices: the game's city then has 10 offices to let, at least the city's " + ConversionRules.CityFloor + " (" + free + ")");

        // The same city listed in another order gives the same shops: the choice is the address's, not the list's.
        var shuffled = new List<ConversionRow>(full);
        var random = new Random(7);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            ConversionRow t = shuffled[i];
            shuffled[i] = shuffled[j];
            shuffled[j] = t;
        }
        HashSet<string> again = Chosen(shuffled, out int freeAgain);
        Check(again.SetEquals(chosen) && freeAgain == free, "offices: the same city in another order makes the same shops offices (the same on every machine)");
        HashSet<string> twice = Chosen(full, out _);
        Check(twice.SetEquals(chosen), "offices: asked twice, the same shops");

        // Enough of the game's own: no shop is an office (and one that was, and stands empty, is a shop again).
        List<ConversionRow> plenty = City((d, i) => i < 3, (d, i) => true);
        Check(Chosen(plenty, out int plentyFree).Count == 0 && plentyFree == 15, "offices: three empty offices of the game's in each office district make no shop an office (" + plentyFree + " to let)");

        // One of the game's own free in a district: one shop there.
        List<ConversionRow> one = City((d, i) => i == 0, (d, i) => true);
        HashSet<string> oneChosen = Chosen(one, out int oneFree);
        bool each = true;
        foreach (var d in GameCity)
        {
            each &= In(one, oneChosen, d.District) == (d.Offices > 0 ? 1 : 0);
        }
        Check(each && oneFree == 10, "offices: one empty office of the game's in a district makes one shop there an office");

        // Never a shop that is not empty and to let; the shops that are, in the ranking's order.
        List<ConversionRow> someLet = City((d, i) => false, (d, i) => i % 3 == 0);
        HashSet<string> letChosen = Chosen(someLet, out _);
        bool onlyFree = true;
        for (int i = 0; i < someLet.Count; i++)
        {
            onlyFree &= someLet[i].Office || someLet[i].Free || !letChosen.Contains(ConversionRules.Key(someLet[i].Street, someLet[i].Number));
        }
        Check(onlyFree, "offices: a shop somebody rents, owns or has a business in is never made an office");

        // A shop kept as an office (somebody has it) stays one, counts against the cap, and is kept above it.
        List<ConversionRow> held = City((d, i) => false, (d, i) => true);
        string mid = "ba:neighborhood_midtown";
        int heldNow = 0;
        for (int i = 0; i < held.Count; i++)
        {
            if (held[i].District == mid && !held[i].Office && heldNow < 7)
            {
                ConversionRow r = held[i];
                r.Held = true;
                r.Free = false;
                held[i] = r;
                heldNow++;
            }
        }
        HashSet<string> heldChosen = Chosen(held, out _);
        bool keptAll = true;
        for (int i = 0; i < held.Count; i++)
        {
            keptAll &= !held[i].Held || heldChosen.Contains(ConversionRules.Key(held[i].Street, held[i].Number));
        }
        Check(keptAll && In(held, heldChosen, mid) == 7, "offices: seven shops kept as offices in Midtown stay offices, above its cap of " + ConversionRules.Cap(13) + ", and no more are made there");
        Check(ConversionRules.Cap(13) == 6 && ConversionRules.Cap(9) == 4 && ConversionRules.Cap(1) == ConversionRules.DistrictFloor && ConversionRules.Cap(0) == ConversionRules.DistrictFloor,
            "offices: the cap is half a district's shops, and at least the floor");

        // The city's floor: two office districts with every office taken give four; more are made in turn up to eight.
        var two2 = new List<ConversionRow>();
        foreach (string d in new[] { "a", "b" })
        {
            for (int i = 0; i < 5; i++)
            {
                two2.Add(new ConversionRow { Street = d + "office", Number = i, District = d, Office = true, Free = false });
            }
            for (int i = 0; i < 20; i++)
            {
                two2.Add(new ConversionRow { Street = d + "shop", Number = i, District = d, Office = false, Free = true });
            }
        }
        HashSet<string> cityChosen = Chosen(two2, out int cityFree);
        Check(cityChosen.Count == ConversionRules.CityFloor && cityFree == ConversionRules.CityFloor && In(two2, cityChosen, "a") == 4 && In(two2, cityChosen, "b") == 4,
            "offices: two office districts make " + ConversionRules.CityFloor + " offices to let between them, four each (" + cityFree + ")");

        // Too few shops: the city's floor is not met and the choice ends (the cap holds).
        var small = new List<ConversionRow>
        {
            new ConversionRow { Street = "o", Number = 1, District = "x", Office = true, Free = false },
            new ConversionRow { Street = "s", Number = 1, District = "x", Office = false, Free = true },
            new ConversionRow { Street = "s", Number = 2, District = "x", Office = false, Free = true },
            new ConversionRow { Street = "s", Number = 3, District = "x", Office = false, Free = true },
            new ConversionRow { Street = "s", Number = 4, District = "x", Office = false, Free = false },
        };
        HashSet<string> smallChosen = Chosen(small, out int smallFree);
        Check(smallChosen.Count == ConversionRules.Cap(4) && smallFree == 2, "offices: a district of four shops makes two offices at most, whatever the city's floor (" + smallChosen.Count + ")");

        // No rows, no offices anywhere, a shop with no district.
        Check(ConversionRules.Decide(new List<ConversionRow>(), new bool[0]) == 0, "offices: an empty city makes nothing");
        var noOffices = new List<ConversionRow>
        {
            new ConversionRow { Street = "s", Number = 1, District = "x", Office = false, Free = true },
            new ConversionRow { Street = "s", Number = 2, District = null, Office = false, Free = true },
        };
        var noWant = new bool[2];
        ConversionRules.Decide(noOffices, noWant);
        Check(!noWant[0] && !noWant[1], "offices: a district without offices, or a shop without a district, keeps its shops");

        // The ranking: the same number for the same address, another for the next door.
        Check(ConversionRules.Rank("ba:street_fifthavenue", 12) == ConversionRules.Rank("ba:street_fifthavenue", 12)
              && ConversionRules.Rank("ba:street_fifthavenue", 12) != ConversionRules.Rank("ba:street_fifthavenue", 14)
              && ConversionRules.Rank("ba:street_fifthavenue", 12) != ConversionRules.Rank("ba:street_sixthavenue", 12), "offices: the ranking is the address's");
        Check(ConversionRules.Rank("a", 1) == Fnv("a", 1), "offices: the ranking is FNV-1a over the street's id and the number's four bytes, the same on every machine");

        // The save's line: the places kept, sorted, escaped; another version or a damaged line gives none.
        var places = new HashSet<string>(StringComparer.Ordinal) { ConversionRules.Key("ba:street_b", 2), ConversionRules.Key("ba:street_a", 10), ConversionRules.Key("odd^%street", 3) };
        string line = ConversionStore.Write(places);
        var read = new HashSet<string>(StringComparer.Ordinal);
        Check(ConversionStore.Read(line, read) && read.SetEquals(places), "offices: the save's line reads back the places it was written with (" + line + ")");
        Check(line == ConversionStore.Write(new List<string> { ConversionRules.Key("odd^%street", 3), ConversionRules.Key("ba:street_a", 10), ConversionRules.Key("ba:street_b", 2) }),
            "offices: the same places give the same line, in any order");
        Check(ConversionStore.Read(null, read) && read.Count == 0 && ConversionStore.Read("v=1", read) && read.Count == 0, "offices: no line, or one with no places, gives none");
        Check(!ConversionStore.Read("v=2^ba:street_a#1", read) && read.Count == 0, "offices: a line of a later version gives no places (and is left as it was)");
        Check(!ConversionStore.Read("v=1^nonumber", read) && read.Count == 0, "offices: a damaged line gives no places");

        // The player's report (20 and 22 4th Avenue): the map said office and BizMan offered shops. The game keeps two copies of a
        // building (the city scene's and its list's); a type written to one alone is two types. Both must read the one wanted.
        const string Off = "ba:buildingtype_office", Shop = "ba:buildingtype_retail";
        Check(ConversionRules.Disagreement(Off, Off, Off) == null && ConversionRules.Disagreement(Off, Off, null) == null,
            "offices: a building whose copies all read office agrees, and a building the game's list does not hold has one place to read");
        Check(ConversionRules.Disagreement(Off, Off, Shop) != null && ConversionRules.Disagreement(Off, Off, Shop).Contains(Shop),
            "offices: the map's copy reading office and the game's list still reading shop is caught, with what the list reads (the report's 20 and 22 4th Avenue)");
        Check(ConversionRules.Disagreement(Off, Shop, Off) != null && ConversionRules.Disagreement(Off, null, Off) != null && ConversionRules.Disagreement(null, null, null) == null,
            "offices: the city's copy reading shop is caught, so is a missing one, and a type nobody wants against no copies agrees");

        // Every business the game offers in a type is one made for it (BuildingTypeData.availableBusinessTypes against each business's suitable type).
        var suits = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ba:businesstype_florist"] = Shop, ["ba:businesstype_cafeteria"] = Shop, ["ba:businesstype_supermarket"] = Shop,
            ["ba:businesstype_techfirm"] = Off, ["ba:businesstype_lawfirm"] = Off,
        };
        Func<string, string> suitable = name => suits.TryGetValue(name, out string t) ? t : null;
        Check(ConversionRules.WrongOffer(Off, new[] { "ba:businesstype_techfirm", "ba:businesstype_lawfirm" }, suitable) == null
              && ConversionRules.WrongOffer(Shop, new[] { "ba:businesstype_florist", "ba:businesstype_cafeteria", "ba:businesstype_supermarket" }, suitable) == null
              && ConversionRules.WrongOffer(Off, new string[0], suitable) == null && ConversionRules.WrongOffer(Off, null, suitable) == null,
            "offices: an office offering offices, a shop offering shops, and a type that offers nothing, are right");
        string flagged = ConversionRules.WrongOffer(Off, new[] { "ba:businesstype_techfirm", "ba:businesstype_florist", "ba:businesstype_cafeteria" }, suitable);
        Check(flagged != null && flagged.StartsWith("ba:businesstype_florist", StringComparison.Ordinal) && flagged.Contains(Shop),
            "offices: an office that offers a florist is named at the first wrong business, with the type that business suits (" + flagged + ")");
        Check(ConversionRules.WrongOffer(Shop, new[] { "ba:businesstype_unknown" }, suitable) != null && ConversionRules.WrongOffer(Shop, new[] { "ba:businesstype_techfirm" }, null) != null,
            "offices: an offered business the game has no type for is wrong, and so is every one when nothing says what suits what");

        // The sources: a type is written to a copy of a building in one place of the conversion, to both copies and put back to both.
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string game = Source(scripts, @"Offices\ConversionGame.cs");
        string setType = Body(game, "public static void SetType(");
        int writes = 0;
        foreach (string file in Directory.GetFiles(Path.Combine(scripts, "Offices"), "*.cs"))
        {
            string code = Source(scripts, @"Offices\" + Path.GetFileName(file));
            for (int at = code.IndexOf(".BuildingType = ", StringComparison.Ordinal); at >= 0; at = code.IndexOf(".BuildingType = ", at + 1, StringComparison.Ordinal))
            {
                writes++;
            }
        }
        Check(setType.Contains("game.BuildingType = type") && setType.Contains("scene.BuildingType = type") && writes == 2,
            "offices: the conversion writes a building's type in one place, SetType, to the game's list's copy and the city scene's (" + writes + " writes in Scripts/Offices)");
        string conversions = Source(scripts, @"Offices\Conversions.cs");
        string restore = Body(conversions, "private static void Restore(");
        string choose = Body(conversions, "private void Choose(");
        Check(restore.Contains("SetType(m.B, m.G, m.C, m.Was)") && choose.Contains("SetType(m.B, m.G, c, ConversionGame.Office)") && choose.Contains("ConversionGame.Disagreement(m.B, m.G, ConversionGame.Office)")
              && conversions.Contains("GameCopies.Contains(building)"),
            "offices: a shop made an office is made in both copies and checked at once, put back in both even when the scene's is gone, and asked for by either copy");

        // The cost: the choice over the game's whole city, made on a change only.
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 200; i++)
        {
            Chosen(full, out _);
        }
        watch.Stop();
        double each200 = watch.Elapsed.TotalMilliseconds / 200.0;
        Check(each200 < 2.0, "offices: one choice over the game's city takes under 2 ms (" + each200.ToString("F3") + " ms)");
    }

    private static uint Fnv(string s, int number)
    {
        uint h = 2166136261u;
        foreach (char c in s)
        {
            h = (h ^ c) * 16777619u;
        }
        uint n = (uint)number;
        for (int i = 0; i < 4; i++)
        {
            h = (h ^ (n & 0xFFu)) * 16777619u;
            n >>= 8;
        }
        return h;
    }
}
