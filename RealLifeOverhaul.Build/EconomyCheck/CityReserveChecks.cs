using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Careers;

// The city's buildings the mod keeps fixed (Scripts/Core/CityReserves, with Core/SpecialNameBook): the two largest office
// buildings kept for the player (no feature of the mod ever picks them, the player may still rent or buy them), the
// courthouse at the game's moving company (never a large office, never the tax office again, never a player's), the move
// of the courthouse and City Utilities told once in an older save, and never a player's building taken. And, read as
// text, that every picker of the mod asks the book, and the Guide and What's new say so.
internal static partial class Program
{
    private static void CityReserveChecks()
    {
        // ---------------------------------------------------------------- the two kept for the player
        // The city's office buildings that may be rented or bought, largest first, from the game's data (Buildings bundle,
        // no special service): the two the mod keeps are the two largest.
        var offices = new List<(string Street, int Number, int Sqm)>
        {
            ("ba:street_seventhstreet", 17, 102502), ("ba:street_broadwaystreet", 12, 58989), ("ba:street_broadwaystreet", 1, 48567),
            ("ba:street_thirdavenue", 13, 42433), ("ba:street_firstavenue", 1, 39096), ("ba:street_secondavenue", 26, 5971), ("ba:street_thirdstreet", 43, 422)
        };
        CityReserves.TopTwo(offices.Count, null, i => offices[i].Sqm, i => offices[i].Street, i => offices[i].Number, out int first, out int second);
        Check(first == 0 && second == 1 && CityReserves.IsPlayerOffice(offices[first].Street, offices[first].Number) && CityReserves.IsPlayerOffice(offices[second].Street, offices[second].Number)
              && !CityReserves.IsPlayerOffice("ba:street_broadwaystreet", 1) && !CityReserves.IsPlayerOffice(CampusRules.HomeStreet, CampusRules.HomeNumber),
            "kept offices: the two kept for the player are the city's two largest office buildings, 17 7th Street and 12 Broadway, and only they");
        Check(CityReserves.PlayerOfficeAddress(0, null) == "17 7th Street" && CityReserves.PlayerOfficeAddress(1, null) == "12 Broadway",
            "kept offices: their addresses as the game writes them, for the Guide");

        var book = new SpecialNameBook();
        for (int i = 0; i < CityReserves.PlayerOfficeStreets.Length; i++)
        {
            book.Reserve(CityReserves.PlayerOfficeId + (i + 1), CityReserves.PlayerOfficeStreets[i], CityReserves.PlayerOfficeNumbers[i], null);
        }
        bool uniOn = true;
        book.Reserve(CampusRules.BookId, CampusRules.HomeStreet, CampusRules.HomeNumber, () => uniOn);
        int pick = SpecialNameBook.PickLargest(offices.Count, null, i => offices[i].Sqm, i => offices[i].Street, i => offices[i].Number, book.Holds);
        Check(pick == 2, "kept offices: a feature taking the largest office (City Utilities, the university meanwhile, the courthouse's last fallback) takes the third, 1 Broadway");
        bool refused = false;
        foreach (string id in new[] { "utilities_office", "courthouse", "police_2", "techhq_tower_0", CampusRules.BookId, "hotel_1" })
        {
            for (int i = 0; i < 2; i++)
            {
                refused |= book.Set(id, CityReserves.PlayerOfficeStreets[i], CityReserves.PlayerOfficeNumbers[i], SpecialKind.Civic, "reallife_util_desk_title");
            }
        }
        Check(!refused && book.Count == 0, "kept offices: no feature of the mod is put in the book there (City Utilities, the courthouse, a police station, a tech tower, the university, a hotel)");
        Check(book.HolderOf("ba:street_seventhstreet", 17) == CityReserves.PlayerOfficeId + "1" && book.Holds("ba:street_broadwaystreet", 12)
              && SpecialNameBook.GivesWay(false, book.HolderOf("ba:street_broadwaystreet", 12), "police_")
              && SpecialNameBook.GivesWay(false, book.HolderOf("ba:street_seventhstreet", 17), "techhq_"),
            "kept offices: every picker that asks who holds a building passes them over, and a police station or a tower the save had there gives way");
        Check(!book.Refuses("ba:street_seventhstreet", 17, false, false) && !book.Refuses("ba:street_broadwaystreet", 12, false, false),
            "kept offices: they are not refused to the player: to rent or buy as the game has them");
        book.Clear();
        Check(book.Holds("ba:street_seventhstreet", 17) && book.Holds("ba:street_broadwaystreet", 12), "kept offices: kept whatever the options, through the city's unloading");

        // ---------------------------------------------------------------- the courthouse
        Check(CityReserves.IsCourt("ba:street_twelfthstreet", 15) && !CityReserves.IsCourt("ba:street_fifthavenue", 70) && !CityReserves.IsPlayerOffice(CityReserves.CourtStreet, CityReserves.CourtNumber)
              && CityReserves.CourtStreet != CampusRules.HomeStreet,
            "courthouse: 15 12th Street, the same in every game: not the tax office, not one of the player's offices, not the university's");
        Check(CityReserves.CourtServes("ba:businesstype_movingservice", false) && !CityReserves.CourtServes(null, false) && !CityReserves.CourtServes("ba:businesstype_irs", false)
              && !CityReserves.CourtServes("ba:businesstype_movingservice", true),
            "courthouse: it takes that building only as the game's moving company, a special service never to let or sell (so never an office, large or small), and never a player's");
        offices.Sort((a, b) => b.Sqm.CompareTo(a.Sqm));
        Check(offices.TrueForAll(o => !CityReserves.IsCourt(o.Street, o.Number)), "courthouse: none of the city's office buildings is the courthouse's");

        // ---------------------------------------------------------------- older saves
        string court = CityReserves.Mark(CityReserves.CourtStreet, CityReserves.CourtNumber);
        Check(CityReserves.TellMoved(null, court, true, CityReserves.OldCourt) && CityReserves.TellMoved(CityReserves.OldCourt, court, true, CityReserves.OldCourt),
            "older saves: a save that had the courthouse at the tax office is told once that it has moved");
        Check(!CityReserves.TellMoved(court, court, true, CityReserves.OldCourt), "older saves: ...and never again once the save keeps the new place");
        Check(!CityReserves.TellMoved(null, court, false, CityReserves.OldCourt), "older saves: a new game is told nothing");
        Check(CityReserves.TellMoved(null, "ba:street_broadwaystreet|1", true, CityReserves.OldUtilities) && !CityReserves.TellMoved(null, CityReserves.OldUtilities, true, CityReserves.OldUtilities),
            "older saves: City Utilities moving off 17 7th Street is told once; one that stayed where it was is not");

        // ---------------------------------------------------------------- never a player's
        Check(SpecialNameBook.GivesWay(true, null, "police_") && !SpecialNameBook.MaySit(true, false) && !SpecialNameBook.MaySit(false, true),
            "player's buildings: a building any player rents or owns is never taken, and a feature on one moves off it");
        CityReserveTexts();
        CityReserveSources();
    }

    private static void CityReserveTexts()
    {
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales"));
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(folder, "en.json")), pt = LoadWords(System.IO.Path.Combine(folder, "pt.json")), zh = LoadWords(System.IO.Path.Combine(folder, "zh-cn.json"));
        if (en == null || pt == null || zh == null)
        {
            Check(false, "kept offices: the texts are at " + folder);
            return;
        }
        var missing = new List<string>();
        foreach ((string key, string[] names) in new[] { ("reallife_justice_note_court_moved", new[] { "{address}" }), ("reallife_util_note_moved", new[] { "{address}" }),
                     ("reallife_guide_office_space", new[] { "{offices_kept_1}", "{offices_kept_2}", "{offices_district}" }) })
        {
            foreach (Dictionary<string, string> lang in new[] { en, pt, zh })
            {
                if (!lang.TryGetValue(key, out string text) || Array.Exists(names, n => text.IndexOf(n, StringComparison.Ordinal) < 0))
                {
                    missing.Add(key);
                }
            }
        }
        Check(missing.Count == 0, "kept offices: the move notices and the Guide's office page with the two kept offices are in en, pt and zh-cn" + (missing.Count > 0 ? ": " + string.Join(", ", missing) : string.Empty));
        Check(CampusHomeSaid(en, "The two largest office buildings, 17 7th Street and 12 Broadway, are kept for the player"), "kept offices: What's new says so in en");
        Check(CampusHomeSaid(pt, "Os dois maiores prédios comerciais, 17 7th Street e 12 Broadway, ficam para o jogador"), "kept offices: What's new says so in pt");
        Check(CampusHomeSaid(zh, "最大的两栋办公楼 17 7th Street 和 12 Broadway 留给玩家"), "kept offices: What's new says so in zh-cn");
    }

    // Every picker of the mod asks the book (Holds, HolderOf or ReservedFor), so the reservations hold for all of them.
    private static void CityReserveSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string Read(string file) => System.IO.File.Exists(System.IO.Path.Combine(scripts, file)) ? System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, file)) : string.Empty;
        string links = Read(@"Wiring\OfficeLinks.cs"), court = Read(@"Justice\Courthouse.cs"), util = Read(@"Utilities\UtilitiesOffice.cs");
        Check(links.Contains("SpecialBuildings.Reserve(CityReserves.PlayerOfficeId + (i + 1), CityReserves.PlayerOfficeStreets[i], CityReserves.PlayerOfficeNumbers[i], null)"),
            "kept offices: reserved at every city load before any feature chooses, whatever the options");
        var pickers = new (string File, string Asks)[]
        {
            (@"Utilities\UtilitiesOffice.cs", "SpecialBuildings.Holds(building)"),
            (@"Justice\Courthouse.cs", "SpecialBuildings.ReservedFor("),
            (@"Law\Precincts.cs", "SpecialBuildings.HolderOf(building)"),
            (@"Tech\Sites\Headquarters.cs", "SpecialBuildings.HolderOf(building)"),
            (@"Careers\CampusBuilding.cs", "SpecialBuildings.Holds)"),
            (@"Offices\Conversions.cs", "!SpecialBuildings.Holds(c)"),
            (@"Economy\Brands\BrandsGame.cs", "SpecialBuildings.Holds(building.StreetName, building.StreetNumber)"),
        };
        var deaf = new List<string>();
        foreach ((string file, string asks) in pickers)
        {
            if (!Read(file).Contains(asks))
            {
                deaf.Add(file);
            }
        }
        Check(deaf.Count == 0, "kept offices: every picker of an office building asks the book, so none takes the kept ones" + (deaf.Count > 0 ? ": not " + string.Join(", ", deaf) : string.Empty));
        int host = court.IndexOf("CityReserves.IsCourt(", StringComparison.Ordinal), passes = court.IndexOf("for (int pass = 0;", StringComparison.Ordinal);
        Check(host > 0 && passes > host && court.Contains("return building;   // (its fixed building"),
            "courthouse: its fixed building is taken before the old rule (the tax office, a bank, a school, the largest office) is ever asked");
        Check(court.Contains("PlaceMoves.Chosen(CityReserves.CourtKey, JusticeState.Key, CityReserves.OldCourt") && util.Contains("PlaceMoves.Chosen(CityReserves.UtilitiesKey, UtilitiesStore.Key, CityReserves.OldUtilities"),
            "older saves: the courthouse and City Utilities tell a move once through the save");
        Check(court.Contains("SpecialBuildings.Players(building)") && util.Contains("SpecialBuildings.Players(building)"), "player's buildings: the courthouse and City Utilities never choose a player's building");
    }
}
