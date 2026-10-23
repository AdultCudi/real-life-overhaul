using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul;
using RealLifeOverhaul.Meta;
using RealLifeOverhaul.Utilities;
using NyDistricts = RealLifeOverhaul.CityDistricts;

// DLC and update readiness (Core/CityRules, Core/CityDistricts, Meta/FeatureCities and the tables by district): the city
// check's decision table for the New York the mod knows, a New York with a district a free update adds, another city, a
// city read only from its scenes and one not read at all; its log line; the districts the mod knows and their order; and
// every table by district given a district it does not know: precincts, civic wards, internet and broadband, hotels and
// City Utilities' district.
internal static partial class Program
{
    private const string NewDistrict = "ba:neighborhood_jfkairport";   // a district a free update could add (the roadmap's airport)
    private const string OtherCity = "ba:neighborhood_pudong";          // a district of another city

    private static void CityReady()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        CityDecisions();
        CityLines();
        CityDistrictOrder();
        CityDistrictTables();
        CityFeatureTable();
        Console.WriteLine("  DLC and update readiness: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // A census of a city: so many buildings in each district given, and the scenes given.
    private static CityCensus Census(string[] scenes, params string[] districts)
    {
        var census = new CityCensus();
        foreach (string scene in scenes)
        {
            census.Scenes.Add(scene);
        }
        foreach (string district in districts)
        {
            for (int i = 0; i < 20; i++)
            {
                census.Add(district);
            }
        }
        foreach (string district in NyDistricts.NewYork)
        {
            census.GameDistricts.Add(district);
        }
        census.GameDistricts.Add(NyDistricts.Global);
        return census;
    }

    private static readonly string[] NewYorkSceneSet = { "MainScene", "UI", "Buildings", "ExteriorProps", "LightsAndPostprocessing", "Streets", "Indoor", "LowerManhattan", "IndustryCity", "TheHamptons" };

    private static void CityDecisions()
    {
        var verdict = new CityVerdict();

        // The New York of Build 3682: its seven districts and the global one, its scenes.
        var all = new List<string>(NyDistricts.NewYork) { NyDistricts.Global };
        CityCensus ny = Census(NewYorkSceneSet, all.ToArray());
        CityRules.Decide(ny, verdict);
        Check(verdict.Kind == CityKind.NewYork && verdict.Source == CitySource.Buildings && verdict.KnownDistricts == 7 && verdict.NewDistricts.Count == 0
              && verdict.NewYorkFeatures, "the city check: New York's seven districts are New York, its features on (" + verdict.Kind + ")");
        Check(verdict.NewYorkScenes.Count == 3 && verdict.OtherScenes.Count == 0, "the city check: New York's three scenes found, no other");
        Check(ny.Here("ba:neighborhood_midtown") && !ny.Here(NewDistrict) && !ny.Here(null), "a district is here only with a building in the loaded city");

        // New York with a district a free update adds: still New York, the new district named.
        CityCensus grown = Census(NewYorkSceneSet, "ba:neighborhood_midtown", "ba:neighborhood_hellskitchen", NewDistrict);
        grown.GameDistricts.Add(NewDistrict);
        CityRules.Decide(grown, verdict);
        Check(verdict.Kind == CityKind.NewYorkGrown && verdict.NewYorkFeatures && verdict.NewDistricts.Count == 1 && verdict.NewDistricts[0] == NewDistrict,
            "the city check: New York with a new district is still New York, the new district named (" + verdict.Kind + ")");
        Check(verdict.Elsewhere.Count == 5, "the districts of the game's data with no building here are named (" + verdict.Elsewhere.Count + ")");

        // Another city: none of the seven among its buildings, whatever its scenes say.
        CityCensus shanghai = Census(new[] { "MainScene", "UI", "Buildings", "Streets", "Indoor", "Shanghai" }, OtherCity, "ba:neighborhood_thebund");
        shanghai.GameDistricts.Add(OtherCity);
        CityRules.Decide(shanghai, verdict);
        Check(verdict.Kind == CityKind.Other && !verdict.NewYorkFeatures && verdict.KnownDistricts == 0 && verdict.OtherScenes.Count == 1 && verdict.OtherScenes[0] == "Shanghai",
            "the city check: a city with none of New York's districts is another city, New York's features off (" + verdict.Kind + ")");
        CityCensus disguised = Census(NewYorkSceneSet, OtherCity);
        CityRules.Decide(disguised, verdict);
        Check(verdict.Kind == CityKind.Other && verdict.Source == CitySource.Buildings, "the buildings decide over the scenes: New York's scenes with another city's buildings is another city");
        CityCensus seasonal = Census(new[] { "MainScene", "Buildings", "HalloweenStreetDecorations" }, NyDistricts.NewYork);
        CityRules.Decide(seasonal, verdict);
        Check(verdict.Kind == CityKind.NewYork, "a season's scene the mod does not know never makes New York another city while the buildings are read");

        // Before the buildings are listed: the scenes alone.
        CityRules.Decide(Census(NewYorkSceneSet), verdict);
        Check(verdict.Kind == CityKind.NewYork && verdict.Source == CitySource.Scenes, "the scenes alone: New York's scenes are New York");
        CityRules.Decide(Census(new[] { "MainScene", "UI", "Buildings", "Shanghai" }), verdict);
        Check(verdict.Kind == CityKind.Other && verdict.Source == CitySource.Scenes && !verdict.NewYorkFeatures, "the scenes alone: none of New York's and one of another city's is another city");
        CityRules.Decide(Census(new[] { "MainScene", "UI", "Buildings", "Streets", "ChristmasStreetDecorations" }), verdict);
        Check(verdict.Kind == CityKind.Unknown && verdict.NewYorkFeatures, "the scenes alone: only the shared scenes is not known, and the mod runs as it always has");
        CityRules.Decide(new CityCensus(), verdict);
        Check(verdict.Kind == CityKind.Unknown && verdict.Source == CitySource.Nothing && verdict.NewYorkFeatures, "nothing read: not known, New York's features on");
        CityRules.Decide(null, verdict);
        Check(verdict.Kind == CityKind.Unknown && verdict.NewYorkFeatures, "no census: not known");
        var empty = new CityCensus();
        Check(empty.Here("ba:neighborhood_midtown") && empty.Here(NewDistrict), "while the buildings are not listed, every district is here");
        CityCensus blank = Census(NewYorkSceneSet);
        blank.Add(null);
        blank.Add(string.Empty);
        CityRules.Decide(blank, verdict);
        Check(verdict.Kind == CityKind.Other && blank.ByDistrict.ContainsKey(NyDistricts.Global) && verdict.NewDistricts.Count == 0,
            "buildings with no district count as the global one, which is no city's");

        // The same census decides the same on every machine, and again.
        CityRules.Decide(grown, verdict);
        string first = CityRules.Line(verdict, grown);
        CityRules.Decide(grown, verdict);
        Check(first == CityRules.Line(verdict, grown), "the same city gives the same verdict and line every time");
    }

    private static void CityLines()
    {
        var verdict = new CityVerdict();
        CityCensus shanghai = Census(new[] { "Shanghai" }, OtherCity);
        string line = CityRules.Line(CityRules.Decide(shanghai, verdict), shanghai);
        Check(line.StartsWith("City check: another city (", StringComparison.Ordinal) && line.EndsWith("): New York features off in this city.", StringComparison.Ordinal),
            "another city's line says what was found and that New York's features are off in this city: " + line);
        CityCensus ny = Census(NewYorkSceneSet, NyDistricts.NewYork);
        line = CityRules.Line(CityRules.Decide(ny, verdict), ny);
        Check(line.StartsWith("City check: New York (7 of 7 districts of New York among 140 buildings", StringComparison.Ordinal) && line.EndsWith("): New York features on.", StringComparison.Ordinal),
            "New York's line: " + line);
        CityCensus grown = Census(NewYorkSceneSet, "ba:neighborhood_midtown", NewDistrict);
        line = CityRules.Line(CityRules.Decide(grown, verdict), grown);
        Check(line.Contains(NewDistrict + " (20)") && line.EndsWith("the districts it does not know get the mod's defaults.", StringComparison.Ordinal),
            "a grown New York's line names the new district and its buildings: " + line);
        line = CityRules.Line(CityRules.Decide(new CityCensus(), verdict), new CityCensus());
        Check(line.EndsWith("New York features on, as the mod has always run.", StringComparison.Ordinal), "a city not read says the mod runs as always: " + line);
        foreach (string text in new[] { line, CityRules.Line(CityRules.Decide(shanghai, verdict), shanghai) })
        {
            Check(text.IndexOf('—') < 0 && text.IndexOf('–') < 0 && text.IndexOf(" - ", StringComparison.Ordinal) < 0 && text.IndexOf('!') < 0,
                "the city check's line has no dash and no exclamation mark");
        }
    }

    private static void CityDistrictOrder()
    {
        Check(NyDistricts.NewYork.Length == 7, "New York has the seven districts of Build 3682");
        for (int i = 0; i < NyDistricts.NewYork.Length; i++)
        {
            Check(NyDistricts.Known(NyDistricts.NewYork[i]) && NyDistricts.IndexOf(NyDistricts.NewYork[i]) == i, "a known district is found at its place: " + NyDistricts.NewYork[i]);
            Check(i == 0 || string.CompareOrdinal(NyDistricts.NewYork[i - 1], NyDistricts.NewYork[i]) < 0, "the seven are in ordinal order");
        }
        Check(!NyDistricts.Known(NewDistrict) && !NyDistricts.Known(NyDistricts.Global) && !NyDistricts.Known(null) && !NyDistricts.Known(string.Empty)
              && NyDistricts.IndexOf(NewDistrict) == -1, "a new district, the global one, null and an empty id are not known");

        // The known first: a new district sorts after the seven, whatever its name.
        var sorted = new SortedDictionary<string, int>(NyDistricts.KnownFirst) { { "ba:neighborhood_aardvark", 0 }, { NewDistrict, 0 } };
        foreach (string d in NyDistricts.NewYork)
        {
            sorted[d] = 0;
        }
        var order = new List<string>(sorted.Keys);
        Check(order.Count == 9 && order.GetRange(0, 7).TrueForAll(NyDistricts.Known) && order[7] == "ba:neighborhood_aardvark" && order[8] == NewDistrict,
            "the known districts come first, the new ones after in ordinal order: " + string.Join(", ", order));

        // The game's order kept for the seven, a new district after them, the global one and repeats left out.
        var game = new List<string>
        {
            "ba:neighborhood_garmentdistrict", "ba:neighborhood_thehamptons", NewDistrict, "ba:neighborhood_industrycity", "ba:neighborhood_hellskitchen",
            "ba:neighborhood_midtown", NyDistricts.Global, "ba:neighborhood_lowermanhattan", "ba:neighborhood_murrayhill", "ba:neighborhood_midtown", string.Empty, null
        };
        var into = new List<string> { "left over" };
        NyDistricts.Ordered(game, null, into);
        Check(into.Count == 8 && into[0] == "ba:neighborhood_garmentdistrict" && into[1] == "ba:neighborhood_thehamptons" && into[2] == "ba:neighborhood_industrycity"
              && into[6] == "ba:neighborhood_murrayhill" && into[7] == NewDistrict, "the seven keep the game's order and their places, a new district comes after: " + string.Join(", ", into));
        var without = new List<string>(game);
        without.Remove(NewDistrict);
        var before = NyDistricts.Ordered(without, null, new List<string>());
        bool same = true;
        for (int i = 0; i < before.Count; i++)
        {
            same &= before[i] == into[i];
        }
        Check(same && before.Count == 7, "a district a free update adds moves no saved index of the seven (civic wards keep each district's place)");
        NyDistricts.Ordered(game, d => d != "ba:neighborhood_midtown", into);
        Check(!into.Contains("ba:neighborhood_midtown") && into.Count == 7, "a district not in the loaded city is left out");
        NyDistricts.Ordered(game, NyDistricts.Known, into);
        Check(into.Count == 7 && !into.Contains(NewDistrict), "in another city, a list of New York's own districts is the seven");
        Check(NyDistricts.Ordered(null, null, into).Count == 0, "no list from the game: none");
    }

    private static void CityDistrictTables()
    {
        // Precincts (Law/Precincts.cs: SortedDictionary by CityDistricts.KnownFirst, six at most, none in the Hamptons).
        var districts = new SortedDictionary<string, int>(NyDistricts.KnownFirst) { { "ba:neighborhood_aaa_new", 0 }, { NewDistrict, 0 } };
        foreach (string d in NyDistricts.NewYork)
        {
            districts[d] = 0;
        }
        var stations = new List<string>();
        foreach (string d in districts.Keys)
        {
            if (stations.Count < 6 && d != "ba:neighborhood_thehamptons")
            {
                stations.Add(d);
            }
        }
        Check(stations.TrueForAll(NyDistricts.Known) && stations.Count == 6, "a new district never takes a precinct from one of New York's six: " + string.Join(", ", stations));

        // Internet: a district the catalogue does not know is reached by the providers that reach everywhere, and only those.
        int reach = 0;
        for (int p = 0; p < InternetCatalogue.Providers.Length; p++)
        {
            bool everywhere = true;
            foreach (string d in NyDistricts.NewYork)
            {
                everywhere &= InternetRules.Reaches(p, d);
            }
            Check(InternetRules.Reaches(p, NewDistrict) == everywhere, "a new district is reached by " + InternetCatalogue.Providers[p].Id + " exactly when it reaches all seven");
            reach += InternetRules.Reaches(p, NewDistrict) ? 1 : 0;
        }
        Check(reach == 2, "a business in a new district can still be connected: wireless and copper reach it (" + reach + ")");
        Check(!InternetRules.Reaches(0, "ba:neighborhood_thehamptons") && InternetRules.Reaches(0, "ba:neighborhood_midtown") && InternetRules.Reaches(1, "ba:neighborhood_midtown")
              && !InternetRules.Reaches(1, "ba:neighborhood_garmentdistrict"), "the known districts are reached as before");
        Check(!InternetRules.Reaches(0, null) && !InternetRules.Reaches(-1, NewDistrict), "no district, no provider: nothing reached");

        // Broadband: the player's network has New York's seven; a new district has no place in it and costs nothing to ask about.
        Check(BroadbandCatalogue.Districts.Length == NyDistricts.NewYork.Length && Array.TrueForAll(BroadbandCatalogue.Districts, NyDistricts.Known),
            "the broadband tables are New York's seven districts");
        Check(BroadbandRules.District(NewDistrict) == -1 && BroadbandRules.District(null) == -1 && BroadbandRules.BuildCost(-1, 100) == 0
              && BroadbandRules.BuildCost(BroadbandCatalogue.Districts.Length, 100) == 0, "a new district is no place in the network, and its cost is nothing, not a fault");

        // Hotels: every slot is in a district the mod knows, so a new district simply has no hotel.
        foreach (HotelSlot slot in TravelCatalogue.Hotels)
        {
            Check(NyDistricts.Known(slot.Neighbourhood), "the hotel " + slot.Id + " is in one of New York's districts");
        }
        // City Utilities' district.
        Check(NyDistricts.Known(CityReserves.UtilitiesDistrict), "City Utilities stands in for itself in one of New York's districts");
    }

    private static void CityFeatureTable()
    {
        Check(FeatureCities.Twice().Count == 0, "the city table names each feature once: " + string.Join(", ", FeatureCities.Twice()));
        var ny = FeatureCities.NewYork(new List<string>());
        foreach (string f in new[] { "law", "travel", "transport", "utilities", "civic", "navigation", "tech_sites", "office_space", "justice", "careers_study", "economy_city", "districtview" })
        {
            Check(ny.Contains(f), "the city table calls " + f + " New York's");
        }
        foreach (string f in new[] { "phone", "economy_banks", "life", "home", "camera", "markets", "flare", "calendar", "photos" })
        {
            Check(FeatureCities.Of(f) == CityScope.AnyCity, "the city table lets " + f + " run in any city");
        }
        foreach (string f in FeatureCities.Named)
        {
            string why = FeatureCities.Why(f);
            Check(!string.IsNullOrEmpty(why) && why.IndexOf(" - ", StringComparison.Ordinal) < 0, "the city table says what " + f + " stands on");
        }
        Check(FeatureCities.Of("not_a_feature") == CityScope.AnyCity && FeatureCities.Why(null) == null && !FeatureCities.Has(null), "a feature the table does not name runs anywhere");
    }
}
