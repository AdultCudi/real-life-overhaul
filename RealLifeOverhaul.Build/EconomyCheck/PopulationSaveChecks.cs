using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using RealLifeOverhaul;
using RealLifeOverhaul.DistrictSave;
using RealLifeOverhaul.Population;

// The population's line in the save (one modData line, binary columns compressed in pieces on the save's workers, as the district save keeps its
// records), a save with no line or a damaged one, a saved population bound to the city as it stands at a load, its part of a Going Public session
// applied on a guest, the factor the game's own customers are moved by, and every screen of its view as the design system will draw it.
internal static partial class Program
{
    private static void PopulationSaveChecks()
    {
        PopulationSim sim = PopulationMade(555, out PopulationState s, out List<BusinessSeed> seeds);
        for (int day = 2; day <= 13; day++)
        {
            sim.BeginDay(day, Ordinary, null);
            sim.RunAll();
        }
        var clock = Stopwatch.StartNew();
        string line = PopulationStore.Encode(s);
        double encodeMs = clock.Elapsed.TotalMilliseconds;
        var zip = new DistrictSaveZip(DistrictSaveZip.WorkersHere);
        PopulationStore.Line(s, zip);   // warm
        clock.Restart();
        string pieces = PopulationStore.Line(s, zip);
        double zipMs = clock.Elapsed.TotalMilliseconds;
        clock.Restart();
        PopulationState back = PopulationStore.Decode(line, out int version);
        double decodeMs = clock.Elapsed.TotalMilliseconds;
        if (Environment.GetEnvironmentVariable("RLO_POPULATION_REPORT") == "1")
        {
            Console.WriteLine("  the save's line: " + line.Length / 1024 + " KB (" + s.Households.Live + " households, " + s.People.Live + " people); written in " + zipMs.ToString("F1", CultureInfo.InvariantCulture)
                              + " ms on " + zip.Workers + " workers (" + encodeMs.ToString("F1", CultureInfo.InvariantCulture) + " ms in one stream), read in " + decodeMs.ToString("F1", CultureInfo.InvariantCulture) + " ms");
        }
        Check(back != null && version == PopulationStore.Version && Fingerprint(back) == Fingerprint(s), "population save: a population's line reads back the same population, to the byte");
        Check(pieces != null && PopulationStore.Decode(pieces, out _) is PopulationState fromPieces && Fingerprint(fromPieces) == Fingerprint(s),
            "population save: the line made in pieces on the workers reads back the same as the one made in one stream");
        Check(back != null && back.Businesses.Count == s.Businesses.Count && back.Businesses.Anchor[0] == s.Businesses.Anchor[0] && back.Employers.Count == s.Employers.Count
              && back.Links.Count == s.Links.Count && back.Books.IncomeTax == s.Books.IncomeTax && back.Median == s.Median && back.Districts.Count == s.Districts.Count,
            "population save: its businesses' anchors, employers, links, books and districts come back too");
        Check(line.Length < 2 * 1024 * 1024 && zipMs < 200 && decodeMs < 400,
            "population save: the city's line under 2 MB, written under 200 ms and read under 400 ms here (" + line.Length / 1024 + " KB, " + zipMs.ToString("F0", CultureInfo.InvariantCulture) + " and "
            + decodeMs.ToString("F0", CultureInfo.InvariantCulture) + " ms)");
        // A year of 365 days: months of thirty days make every ledger line bigger; a month closed and half the next lived, the line still under the budget.
        PopulationState year = PopulationCity(out List<BusinessSeed> yearSeeds);
        var yearSim = new PopulationSim();
        yearSim.BeginGenerate(year, 555, 1, 365, yearSeeds);
        yearSim.RunAll();
        for (int day = 2; day <= 46; day++)
        {
            yearSim.BeginDay(day, Ordinary, null);
            yearSim.RunAll();
        }
        string longYear = PopulationStore.Encode(year);
        PopulationState longBack = PopulationStore.Decode(longYear, out _);
        if (Environment.GetEnvironmentVariable("RLO_POPULATION_REPORT") == "1")
        {
            Console.WriteLine("  the save's line in a year of 365 days: " + longYear.Length / 1024 + " KB (" + year.Households.Live + " households)");
        }
        Check(longYear.Length < 2 * 1024 * 1024 && longBack != null && Fingerprint(longBack) == Fingerprint(year),
            "population save: in a year of 365 days, a month closed and half the next lived, the line is under 2 MB too and reads back the same (" + longYear.Length / 1024 + " KB)");
        Check(PopulationStore.Decode(null, out _) == null && PopulationStore.Decode("v=1~not base64", out _) == null && PopulationStore.Decode("v=1~" + Convert.ToBase64String(new byte[] { 1, 2, 3 }), out _) == null
              && PopulationStore.Decode(line.Substring(0, line.Length / 2), out _) == null && PopulationStore.Decode("x=1~", out _) == null,
            "population save: a save with no line, or a damaged one, reads as none (and the city is generated again), never a fault");
        // A beta's line (version 3, its homes counted above shops too) reads, but is generated again from the buildings with its own seed: the same city
        // a new save of that seed has, in the owner's size.
        PopulationState beta = PopulationStore.Decode(PopulationStore.EncodeVersion(s, 3), out int betaVersion);
        PopulationMade(beta != null ? beta.Seed : 0, out PopulationState remade, out _);
        PopulationMade(555, out PopulationState fresh, out _);
        Check(back != null && back.Measure == PopulationCatalogue.MeasureVersion && !GenerateRules.Outdated(back) && !GenerateRules.Outdated(null)
              && beta != null && betaVersion == 3 && beta.Measure == 0 && GenerateRules.Outdated(beta) && Fingerprint(remade) == Fingerprint(fresh)
              && remade.Households.Live >= PopulationCatalogue.HouseholdsLeast && remade.Households.Live <= PopulationCatalogue.HouseholdsMost,
            "population save: a line of this measure is kept; a beta's (version 3, homes above shops counted too) is generated again with its own seed, the same city a "
            + "new save of it has, in the owner's size (" + remade.Households.Live + " households)");

        // Bound to the city as it stands: the same city binds every household back to its flat; a building gone marks its households away.
        PopulationState again = PopulationStore.Decode(line, out _);
        PopulationState city = PopulationCity(out _);
        int away = CityTable.Rebind(again, new List<CityBuilding>(city.Buildings), new List<PopDistrict>(city.Districts));
        Check(again != null && away == 0 && Fingerprint(again) == Fingerprint(s), "population save: bound to the same city every household is in its flat again, and nothing changes");
        PopulationState less = PopulationStore.Decode(line, out _);
        var fewer = new List<CityBuilding>(city.Buildings);
        int gone = -1, goneHouseholds = 0;
        for (int b = 0; b < fewer.Count; b++)
        {
            if (fewer[b].Use == Use.Homes && !fewer[b].Villa && MeasureRules.Flats(fewer[b], city.Districts[fewer[b].District]) > 20)
            {
                gone = b;
                break;
            }
        }
        for (int h = 0; h < s.Households.Count; h++)
        {
            goneHouseholds += s.Households.Alive(h) && s.Households.Building[h] == gone ? 1 : 0;
        }
        fewer.RemoveAt(gone);
        int marked = CityTable.Rebind(less, fewer, new List<PopDistrict>(city.Districts));
        Check(marked == goneHouseholds && marked > 0 && less.People.Live == s.People.Live,
            "population save: a building not standing at a load marks its households away, keeping every person (" + marked + " households)");

        PopulationShareChecks(s);
        PopulationFactorChecks();
        PopulationViewChecks(s, sim.Demand);
    }

    private static void PopulationShareChecks(PopulationState host)
    {
        string part = PopulationShare.Write(host);
        var guest = new PopulationState();
        Check(PopulationShare.Apply(guest, part) && guest.Day == host.Day && guest.Books.IncomeTax == host.Books.IncomeTax && guest.HostPeople == host.People.Live,
            "population share: a guest takes the host's day, books and count of people");
        int seen = 0, alike = 0;
        for (int b = 0; b < host.Businesses.Count; b++)
        {
            if (!host.Businesses.Seen[b])
            {
                continue;
            }
            seen++;
            int g = guest.BusinessOfKey(host.Businesses.Key[b]);
            alike += g >= 0 && guest.Businesses.Takings[g] == host.Businesses.Takings[b] && guest.Businesses.Visits[g] == host.Businesses.Visits[b] ? 1 : 0;
        }
        Check(seen > 0 && alike == seen, "population share: every business's takings and customers reach the guest by its address (" + alike + " of " + seen + ")");
        bool districts = true;
        for (int d = 0; d < host.Districts.Count; d++)
        {
            int g = guest.DistrictOf(host.Districts[d].Id);
            districts &= g >= 0 && guest.Figures[g].People == host.Figures[d].People && guest.Figures[g].Unemployed == host.Figures[d].Unemployed;
        }
        Check(districts, "population share: every district's figures reach the guest by its id");
        int anchored = 0;
        for (int g = 0; g < guest.Businesses.Count; g++)
        {
            anchored += guest.Businesses.AnchorDay[g] == guest.Day && guest.Businesses.Anchor[g] == guest.Businesses.Takings[g] || guest.Businesses.Takings[g] == 0 ? 1 : 0;
        }
        Check(anchored == guest.Businesses.Count, "population share: a guest anchors each business on the first takings it hears, so its own count stands that day");
        Check(!PopulationShare.Apply(guest, "v=x~h:1") && !PopulationShare.Apply(guest, "nothing") && !PopulationShare.Apply(null, part) && guest.Day == host.Day,
            "population share: a line this version cannot read is not applied, and the guest's state stays as it was");
        Check(part.Length < 1024 * 1024, "population share: the part is under a megabyte, the shared city's most on the wire before its compression (" + part.Length / 1024 + " KB)");
    }

    private static void PopulationFactorChecks()
    {
        Check(CustomerRules.Factor(500, 500, 3) == 1000 && CustomerRules.Factor(750, 500, 3) == 1500 && CustomerRules.Factor(0, 500, 3) == 0 && CustomerRules.Factor(900, 0, -1) == 1000,
            "population factor: today's takings against the anchor's; the game's own count with no anchor yet");
        Check(CustomerRules.Factor(long.MaxValue / 2000, 1, 1) == CustomerRules.FactorMost, "population factor: kept within its bound however far the takings run");
        Check(Math.Abs(CustomerRules.InitialScale(1500, 875, 875, true) - 1.5f) < 1e-6 && Math.Abs(CustomerRules.InitialScale(1000, 800, 1000, true) - 0.8f) < 1e-6
              && Math.Abs(CustomerRules.InitialScale(1000, 800, 1000, false) - 1f) < 1e-6,
            "population factor: the game's promotion since the anchor is taken back out of its formula (its attention carries it), never for the casino's");
        Check(CustomerRules.PromotionMultiplier(0.5f, 50) == 875 && CustomerRules.PromotionMultiplier(0.5f, 150) == 1250 && CustomerRules.PromotionAttention(875, 1050) == 200
              && CustomerRules.LeverAttention(-12) == -120, "population factor: a campaign's promotion and today's levers as attention, in thousandths");
    }

    private static void PopulationViewChecks(PopulationState s, PoolRules.DemandDay dd)
    {
        PopulationView city = PopulationViews.City(s);
        Check(city.Hero.Count >= 3 && city.Hero.Count <= 4 && city.Rows.Count > 0 && city.Rows.Count <= PopulationViews.RowsMost && city.Primary != null && city.PrimaryTarget.Kind == ViewKind.Contributors,
            "population views: the city has a few figures, its districts and one primary action to who contributes");
        int d = city.Rows[0].Target.Index;
        PopulationView district = PopulationViews.District(s, d);
        Check(district.TitleKey && district.Rows.Count > 0 && district.Rows[0].Target.Kind == ViewKind.Building, "population views: a district lists its fullest buildings, each leading to its building");
        PopulationView building = PopulationViews.Building(s, district.Rows[0].Target.Index);
        Check(building.Rows.Count > 0 && building.Rows[0].Target.Kind == ViewKind.Household && building.Rows[0].NameSeed != 0, "population views: a building lists its households by their heads' names");
        int h = building.Rows[0].Target.Index;
        PopulationView household = PopulationViews.Household(s, dd, h);
        Check(household.Hero.Count == 4 && household.Rows.Count == s.Households.Size[h] && household.Details.Count >= 4,
            "population views: a household shows what it earned, paid in tax, spent and saved, its people, its rent and to whom, its savings and debt");
        PopulationView person = PopulationViews.Person(s, household.Rows[0].Target.Index);
        Check(person.Hero.Count == 3 && person.Primary != null, "population views: a person shows their age, pay and hours, and leads to their employer or household");
        int worker = -1;
        for (int p = 0; p < s.People.Count && worker < 0; p++)
        {
            int e = s.People.Id[p] != 0 && s.People.Status[p] == (byte)Status.Worker ? s.People.Job[p] : -1;
            worker = e >= 0 && s.Employers.Business[e] >= 0 && s.Employers.BillLast[e] > 0 ? p : -1;
        }
        long share = 0, output = 0;
        if (worker >= 0)
        {
            int e = s.People.Job[worker];
            output = s.Employers.OutputLast[e];
            for (int p = 0; p < s.People.Count; p++)
            {
                share += s.People.Id[p] != 0 && s.People.Job[p] == e ? PopulationViews.ValueAdded(s, p) : 0;
            }
        }
        Check(worker >= 0 && share > 0 && share <= output, "population views: a person's part of their employer's value added is by their share of its wage bill, never past the whole (" + share / 100 + " of " + output / 100 + ")");
        int busiest = 0;
        for (int b = 0; b < s.Businesses.Count; b++)
        {
            busiest = s.Businesses.Takings[b] > s.Businesses.Takings[busiest] ? b : busiest;
        }
        PopulationView business = PopulationViews.Business(s, dd, busiest);
        long from = 0;
        foreach (long v in PopulationViews.CustomersFrom(s, dd, busiest))
        {
            from += v;
        }
        Check(business.Rows.Count > 0 && business.Rows[0].Target.Kind == ViewKind.District && from <= s.Businesses.Takings[busiest] && from >= s.Businesses.Takings[busiest] * 95 / 100,
            "population views: a business shows the districts its customers come from (their spending there adds to its takings), its classes, wage bill and taxes");
        PopulationView who = PopulationViews.Contributors(s, dd);
        Check(who.Hero.Count == 3 && who.Rows.Count == PopulationViews.RowsMost && who.Rows[0].Value >= who.Rows[1].Value && who.Primary != null,
            "population views: who contributes lists the employers that pay most, the player's three shares, and leads to the district that spends most");
        PopulationView none = PopulationViews.Household(s, dd, -1);
        Check(none.Rows.Count == 0 && none.Empty != null, "population views: a screen with nothing to show has its words for it, never a blank");
    }
}
