using System;
using RealLifeOverhaul.Economy;

// A building let unit by unit (Scripts/Economy/Estate): how many units the game's figures make, a unit's share of the
// rent, applicants for each unit within the building's most, a lease in each unit that runs on its own, the rent the
// game is told, the building's wear as the average of its units, a building of many units returning what one of one
// does, a line from before units read as the first unit's lease, and units through the save and back.
internal static partial class Program
{
    private static EstateHolding Units(int count, int value = 1000000, int cond = 900)
    {
        EstateHolding h = Building(value, 1, cond);
        EstateRules.FitUnits(h, count);
        return h;
    }

    // An applicant for a unit, accepted at once: the unit's lease.
    private static void LetUnit(EstateHolding h, int unit, int rent, int record, int day = 1, int term = 90)
    {
        int id = h.NextApplicant++;
        h.Units[unit].Applicants.Add(new RentApplicant { Id = id, Kind = 3, Offer = rent, Record = record, Term = term, Lapse = day + 4 });
        EstateRules.Accept(h, id, day);
    }

    private static void EstateUnits()
    {
        EstateUnitCount();
        EstateUnitRent();
        EstateUnitApplicants();
        EstateUnitLeases();
        EstateUnitWear();
        EstateUnitReturns();
        EstateUnitSave();
    }

    private static void EstateUnitCount()
    {
        Check(EstateRules.UnitCount(1000, 200) == 5 && EstateRules.UnitCount(1999, 200) == 9 && EstateRules.UnitCount(150, 200) == 1, "a building's floor over its unit's size is its units, whole units only, one at least");
        Check(EstateRules.UnitCount(0, 200) == 1 && EstateRules.UnitCount(1000, 0) == 1 && EstateRules.UnitCount(-5, -5) == 1, "a building the game gives no floor or no unit size is one unit");
        Check(EstateRules.UnitCount(100000, 100) == EstateCatalogue.UnitsMost && EstateRules.UnitCount(int.MaxValue, 1) == EstateCatalogue.UnitsMost, "and never more than the most");
        EstateHolding h = Building();
        Check(h.Units.Count == 1 && EstateRules.FitUnits(h, 5) == 5 && h.Units[4] != null && !h.Units[4].Let, "a building gets the units the game counts, empty");
        LetUnit(h, 4, 200, 80);
        Check(EstateRules.FitUnits(h, 2) == 5 && h.Units[4].Let, "a let unit over a smaller count stays until its lease ends");
        h.Units[4].Vacant();
        Check(EstateRules.FitUnits(h, 2) == 2 && EstateRules.FitUnits(h, 0) == 1 && EstateRules.FitUnits(h, 99) == EstateCatalogue.UnitsMost, "empty units over the count go; one unit at least, the most at most");
        Check(EstateRules.UnitKey(h, 0) == EstateRules.Hash(h.Place) && EstateRules.UnitKey(h, 1) != EstateRules.UnitKey(h, 0) && EstateRules.UnitKey(h, 2) != EstateRules.UnitKey(h, 1),
            "the first unit draws as the building did before units, every other unit by its own number");
    }

    private static void EstateUnitRent()
    {
        EstateClimate c = Climate();
        EstateHolding one = Units(1), four = Units(4), twelve = Units(12);
        int market = EstateRules.MarketRent(one, c);
        Check(EstateRules.UnitRent(one, c) == market, "a building of one unit lets at the market's rent, as before units");
        Check(EstateRules.UnitRent(four, c) == market / 4 && EstateRules.UnitRent(twelve, c) == market / 12, "a unit's rent is the building's over its units");
        Check(EstateRules.UnitRent(four, c) * 4 <= market && EstateRules.UnitRent(four, c) * 4 > market - 4, "its units together let for the building's rent, less what rounding takes");
        Check(EstateRules.UnitRent(Units(4, 1000000, 100), c) == 0 && EstateRules.UnitRent(Units(4, 30, 900), c) == 1, "an unfit building lets no unit; a unit of a building that lets at all lets for a dollar at least");
        EstateHolding h = Units(3);
        LetUnit(h, 0, 300, 80);
        LetUnit(h, 1, 250, 80);
        LetUnit(h, 2, 200, 80);
        Check(EstateRules.PayingRent(h) == 750, "the game is told to pay every let unit's rent");
        h.Units[1].Arrears = 250;
        h.Units[1].ArrearsDays = 1;
        Check(EstateRules.PayingRent(h) == 500 && EstateRules.PayingRent(Units(3)) == 0, "but not a unit in arrears, and nothing for a building with nobody in");
        float occupancy, price;
        EstateRules.Encode(EstateRules.PayingRent(h), true, 2400, 100, out occupancy, out price);
        Check(occupancy == 100f && Math.Abs(price * 2400 - 500f) < 0.01f, "the game's figures make exactly the rent of the units that pay");
    }

    private static void EstateUnitApplicants()
    {
        EstateClimate c = Climate();
        Check(EstateRules.ApplicantCap(1) == EstateCatalogue.ApplicantsMost && EstateRules.ApplicantCap(4) == EstateCatalogue.ApplicantsMost + 3 && EstateRules.ApplicantCap(12) == EstateCatalogue.ApplicantsBuildingMost
              && EstateRules.ApplicantCap(0) == EstateCatalogue.ApplicantsMost, "a building takes three applications, one more for each unit after the first, twelve at most");
        EstateHolding h = Units(4);
        for (int day = 1; day < 50; day++)
        {
            for (int u = 0; u < h.Units.Count; u++)
            {
                EstateRules.Apply(h, u, 9, day, c, true);
            }
        }
        bool perUnit = true, ids = true;
        for (int u = 0; u < h.Units.Count; u++)
        {
            perUnit &= h.Units[u].Applicants.Count <= EstateCatalogue.ApplicantsMost;
            for (int a = 0; a < h.Units[u].Applicants.Count; a++)
            {
                int unit;
                ids &= h.ApplicantOf(h.Units[u].Applicants[a].Id, out unit) == h.Units[u].Applicants[a] && unit == u;
            }
        }
        Check(h.ApplicantCount == EstateRules.ApplicantCap(4) && perUnit, "applications wait for each unit, three at most for one, and the building's most in all");
        Check(ids, "every applicant has a number of its own across the building, and is found at the unit applied for");
        int market = EstateRules.UnitRent(h, c);
        bool offers = true;
        for (int u = 0; u < h.Units.Count; u++)
        {
            for (int a = 0; a < h.Units[u].Applicants.Count; a++)
            {
                int offer = h.Units[u].Applicants[a].Offer;
                offers &= offer >= market * EstateCatalogue.OfferLeast / 1000 && offer <= market * EstateCatalogue.OfferMost / 1000 + 1;
            }
        }
        Check(offers, "an applicant offers against the unit's rent, not the building's");
        EstateHolding asked = Units(3);
        asked.Units[1].Ask = EstateCatalogue.AskMost;
        asked.Units[2].ToLet = false;
        int high = 0, low = 0, none = 0;
        for (int day = 1; day <= 2000; day++)
        {
            for (int u = 0; u < 3; u++)
            {
                asked.Units[u].Applicants.Clear();
            }
            low += EstateRules.Apply(asked, 0, 31, day, c) != null ? 1 : 0;
            high += EstateRules.Apply(asked, 1, 31, day, c) != null ? 1 : 0;
            none += EstateRules.Apply(asked, 2, 31, day, c) != null ? 1 : 0;
        }
        Check(low > 600 && high < low / 2 && none == 0, "each unit asks its own rent and is looked for on its own: the market's rent brings " + low + ", the most asked " + high + ", a unit not to let none");
        EstateHolding let = Units(2);
        LetUnit(let, 0, 300, 80);
        Check(EstateRules.Apply(let, 0, 9, 2, c, true) == null && EstateRules.Apply(let, 1, 9, 2, c, true) != null && EstateRules.Apply(let, 5, 9, 2, c, true) == null,
            "nobody applies for a let unit or one the building does not have; the empty unit is looked for");
    }

    private static void EstateUnitLeases()
    {
        EstateClimate c = Climate();
        EstateHolding h = Units(3);
        h.Units[0].Applicants.Add(new RentApplicant { Id = 1, Kind = 1, Offer = 300, Record = 80, Term = 60, Lapse = 9 });
        h.Units[1].Applicants.Add(new RentApplicant { Id = 2, Kind = 2, Offer = 280, Record = 40, Term = 90, Lapse = 9 });
        h.Units[1].Applicants.Add(new RentApplicant { Id = 3, Kind = 4, Offer = 260, Record = 90, Term = 120, Lapse = 9 });
        h.NextApplicant = 4;
        Check(EstateRules.Accept(h, 3, 5) && h.Units[1].Let && h.Units[1].Rent == 260 && h.Units[1].Until == 125 && h.Units[1].Deposit == 260 * EstateCatalogue.DepositDays && h.Units[1].Applicants.Count == 0,
            "an accepted applicant becomes the tenant of the unit applied for, and that unit's others are turned away");
        Check(!h.Units[0].Let && h.Units[0].Applicants.Count == 1 && !h.Units[2].Let && h.AnyLet, "the other units are as they were: their applicants wait on");
        Check(!EstateRules.Accept(h, 2, 5) && !EstateRules.Accept(h, 9, 5), "an application for a unit that is let now, or one that never came, cannot be accepted");
        Check(EstateRules.Accept(h, 1, 5) && h.Units[0].Let && h.Units[0].Rent == 300, "the first unit is let on its own lease");
        int behind, let = h.LetUnits(out behind);
        Check(let == 2 && behind == 0, "two units let, none behind");
        h.Units[1].Arrears = 780;
        h.Units[1].ArrearsDays = 3;
        Check(EstateRules.MayServeNotice(h, 1, 6) && !EstateRules.MayServeNotice(h, 0, 6) && !EstateRules.MayServeNotice(h, 2, 6) && !EstateRules.MayServeNotice(h, 7, 6), "notice is for the unit whose rent is behind, and no other");
        Check(EstateRules.UnitsToAnswer(h, 6) == 1 && EstateRules.ServeNotice(h, 1, 6, 0) && h.Units[1].NoticeEnd == 16 && h.Units[0].NoticeEnd == 0, "one unit waits for an answer; notice there runs at that unit only");
        Check(EstateRules.GoToCourt(h, 1, 17) && h.Units[1].CourtDay == 17 + EstateCatalogue.CourtDays && h.Units[0].CourtDay == 0 && !EstateRules.GoToCourt(h, 0, 17), "the court is asked for that unit; the other lease is not touched");
        h.Units[1].Arrears = 5000;
        TenancyDay order = EstateRules.Day(h, 1, 3, h.Units[1].CourtDay, c);
        Check(order.Evicted && !h.Units[1].Let && h.Units[0].Let && h.Units[0].Rent == 300 && order.Lump == 260 * EstateCatalogue.DepositDays, "on the day of the order that unit's tenant is out, against that unit's deposit, and the other stays");
        TenancyDay empty = EstateRules.Day(h, 2, 3, 30, c);
        Check(!empty.Pays && h.Units[2].VoidDays == 1 && h.Units[1].VoidDays == 0, "an empty unit counts its own empty days");
        Check(EstateRules.Quote(h, EstateWork.Refurbish, -1, c) == 0 && EstateRules.Quote(h, EstateWork.Repair, -1, c) > 0, "with a tenant in one unit, the building can be repaired, not refurbished");
        h.Units[0].Vacant();
        Check(!h.AnyLet && EstateRules.Quote(h, EstateWork.Refurbish, -1, c) > 0, "with every unit empty it can be refurbished");
        h.Units[0].Applicants.Add(new RentApplicant { Id = 8, Offer = 5, Lapse = 3 });
        h.Units[2].Applicants.Add(new RentApplicant { Id = 9, Offer = 5, Lapse = 30 });
        Check(EstateRules.Lapse(h, 10) == 1 && h.ApplicantCount == 1, "applications lapse at every unit by their own days");
        EstateRules.Start(h, EstateWork.Refurbish, -1, 10, c);
        Check(h.ApplicantCount == 0, "works turn every unit's applicants away");
        // The rent the game is told follows each lease: the units' days in one building, from a seed, twice alike.
        string a = UnitHistory(77), b = UnitHistory(77), other = UnitHistory(78);
        Check(a == b && a != other, "the same seed gives a building of units the same history, another seed another");
    }

    private static string UnitHistory(int seed)
    {
        EstateClimate c = Climate();
        EstateHolding h = Units(5);
        var t = new System.Text.StringBuilder();
        for (int day = 1; day <= 400; day++)
        {
            EstateRules.Lapse(h, day);
            EstateRules.Wear(h);
            for (int u = 0; u < h.Units.Count; u++)
            {
                TenancyDay d = EstateRules.Day(h, u, seed, day, c);
                t.Append(d.Pays ? 'p' : d.Missed ? 'm' : d.Left ? 'l' : '.');
                if (EstateRules.Apply(h, u, seed, day, c) != null && h.Units[u].Applicants.Count > 0)
                {
                    EstateRules.Accept(h, h.Units[u].Applicants[0].Id, day);
                }
                EstateRules.ServeNotice(h, u, day, 0);
                EstateRules.GoToCourt(h, u, day);
            }
            t.Append(EstateRules.PayingRent(h)).Append(',');
        }
        return t.ToString() + h.Cond;
    }

    private static void EstateUnitWear()
    {
        EstateHolding one = Units(1);
        LetUnit(one, 0, 300, 20);
        Check(EstateRules.Wear(one) == EstateCatalogue.WearLet + 80 / EstateCatalogue.WearPerRecord, "a building of one unit wears by its tenant's record, as before units");
        Check(EstateRules.Wear(Units(1)) == EstateCatalogue.WearVoid && EstateRules.Wear(Units(8)) == EstateCatalogue.WearVoid, "an empty building wears a little, however many units it has");
        EstateHolding four = Units(4);
        LetUnit(four, 2, 100, 20);
        Check(EstateRules.Wear(four) == (3 + 3 * EstateCatalogue.WearVoid + 2) / 4, "a building of many units wears by the average of its units, rounded: one poor tenant and three empty units wear it two a day");
        EstateHolding all = Units(6);
        for (int u = 0; u < 6; u++)
        {
            LetUnit(all, u, 100, 20);
        }
        Check(EstateRules.Wear(all) == EstateRules.Wear(one), "six poor tenants in six units wear it as one poor tenant wears a building of one");
        EstateHolding floor = Units(3, 1000000, 1);
        EstateRules.Wear(floor);
        EstateRules.Wear(floor);
        Check(floor.Cond == 0, "condition never falls under nothing");
    }

    // A building's net rent over some years, its units let by a landlord who takes the first fair applicant (or anybody after two days).
    private static long UnitsNet(int units, int seed)
    {
        EstateClimate c = Climate();
        EstateHolding h = Units(units);
        h.Cover = true;
        long net = 0;
        for (int day = 1; day <= 6 * c.DaysPerYear; day++)
        {
            net += EstateRules.PayingRent(h);
            EstateRules.Lapse(h, day);
            EstateRules.Wear(h);
            for (int u = 0; u < h.Units.Count; u++)
            {
                EstateUnit unit = h.Units[u];
                EstateRules.Apply(h, u, seed, day, c);
                for (int a = 0; !unit.Let && a < unit.Applicants.Count; a++)
                {
                    if (unit.Applicants[a].Record >= 50 || unit.VoidDays >= 2)
                    {
                        EstateRules.Accept(h, unit.Applicants[a].Id, day);
                    }
                }
                TenancyDay d = EstateRules.Day(h, u, seed, day, c);
                net += d.Lump;
                if (!EstateRules.ServeNotice(h, u, day, c.RentLever) && EstateRules.GoToCourt(h, u, day))
                {
                    net -= EstateRules.CourtFee(c.Index);
                }
            }
            net += EstateRules.Hazard(h, seed, day, c).Payout - EstateRules.Premium(h.Base, c);
        }
        return net - (EstateRules.RepairCost(h.Base, h.Cond, c.Index) - EstateRules.RepairCost(h.Base, 900, c.Index));
    }

    private static void EstateUnitReturns()
    {
        long one = 0, six = 0, twelve = 0;
        for (int seed = 1; seed <= 12; seed++)
        {
            one += UnitsNet(1, seed);
            six += UnitsNet(6, seed);
            twelve += UnitsNet(12, seed);
        }
        Check(one > 0 && six > one * 8 / 10 && six < one * 13 / 10 && twelve > one * 8 / 10 && twelve < one * 13 / 10,
            "a building let in six or twelve units returns about what it does let whole: a unit's rent is its share, and more units stand empty less often all at once (" + one / 12 + ", " + six / 12 + " and " + twelve / 12 + " over six years)");
    }

    private static void EstateUnitSave()
    {
        int version;
        // A line written before units: one tenancy at the building, read as the first unit's lease.
        string old = "v=1^h:4711|3|40|1080|7|3|0|0|0^p:High%20Street~1000000|950000|5|50000|1|880|1|1050|1|1|2|70|1200|9600|130|2400|2|44|0|0|0|0|0|0|-1|0|0|0|0|0|0|30000|4000|2|1|1|7|4|900|50|90|41"
                     + "^g:|1|0|0|0^d:^i:^t:^c:|600|0|0|0^v:";
        EstateState s = EstateStore.Read(old, out version);
        EstateHolding h = s != null && s.Holdings.Count == 1 ? s.Holdings[0] : null;
        Check(h != null && version == 1 && h.Units.Count == 1 && h.Units[0].Tenant == 2 && h.Units[0].Rent == 1200 && h.Units[0].Arrears == 2400 && h.Units[0].NoticeEnd == 44 && h.Units[0].Ask == 1050
              && h.Units[0].Applicants.Count == 1 && h.Units[0].Applicants[0].Id == 7 && h.Managed && h.NextApplicant == 2,
            "a line from before units is read: its one tenancy, asking rent and applicant are the first unit's");
        EstateRules.FitUnits(h, 4);
        Check(h.Units.Count == 4 && !h.Units[1].Let && !h.Units[3].Let && h.Units[0].Rent == 1200, "the game's count then gives it its other units, empty");
        string now = EstateStore.Write(s);
        string head = now.Substring(0, now.IndexOf("^u:", StringComparison.Ordinal));
        Check(head == old.Substring(0, old.IndexOf("^g:", StringComparison.Ordinal)).Replace("v=1^", "v=" + EstateStore.Version + "^"), "the building's record is written field for field as before units, so a version from before them reads the first unit's lease");
        EstateState older = EstateStore.Read(now.Replace("v=" + EstateStore.Version + "^", "v=1^"), out version);
        Check(older != null && EstateStore.Write(older) == now, "and this version reads its own line under the old number as well");

        // units through the save and back
        EstateState many = new EstateState { Seed = 5 };
        EstateHolding b = Units(5);
        b.Place = "Wide%20Road~3";
        LetUnit(b, 0, 210, 70, 3, 60);
        LetUnit(b, 2, 190, 40, 3, 120);
        b.Units[2].Arrears = 380;
        b.Units[2].ArrearsDays = 2;
        b.Units[2].NoticeEnd = 19;
        b.Units[3].Ask = 1200;
        b.Units[3].ToLet = false;
        b.Units[3].VoidDays = 12;
        b.Units[4].Applicants.Add(new RentApplicant { Id = b.NextApplicant++, Kind = 6, Offer = 205, Record = 55, Term = 150, Lapse = 20 });
        many.Holdings.Add(b);
        EstateHolding game = Units(3, 400000);
        game.Place = "Game~1";
        game.Managed = false;
        many.Holdings.Add(game);
        string line = EstateStore.Write(many);
        EstateState back = EstateStore.Read(line, out version);
        EstateHolding r = back != null && back.Holdings.Count == 2 ? back.Holdings[0] : null;
        Check(r != null && version == EstateStore.Version && EstateStore.Write(back) == line, "a building's units are read back and write the same line again");
        Check(r != null && r.Units.Count == 5 && r.Units[0].Rent == 210 && r.Units[2].Rent == 190 && r.Units[2].Arrears == 380 && r.Units[2].NoticeEnd == 19 && r.Units[3].Ask == 1200 && !r.Units[3].ToLet
              && r.Units[3].VoidDays == 12 && r.Units[4].Applicants.Count == 1 && r.Units[4].Applicants[0].Offer == 205 && !r.Units[1].Let && r.NextApplicant == b.NextApplicant,
            "every unit's lease, arrears, notice, asking rent, empty days and applicants");
        Check(back != null && back.Holdings[1].Units.Count == 1 && line.IndexOf("Game~1|1|", StringComparison.Ordinal) < 0, "the blank units of a building the game lets are not written: the game's count gives them again");
        string odd = line.Replace("^u:", "^u:Nobody~1|1|1000|1|2|50|9|0|0|0|0|0|0|0|0;Wide%20Road~3|99|1000|1|2|50|9|0|0|0|0|0|0|0|0;")
            .Replace("^g:", ";Wide%20Road~3|1|1000|1|2|50|9|0|0|0|0|0|0|0|1|3|0|5|50|60|9^g:");   // (unit 1 again, after its own blank record, with the number of unit 5's applicant)
        EstateState wild = EstateStore.Read(odd, out version);
        EstateHolding x = wild != null ? wild.Holding("Wide%20Road~3") : null;
        Check(x != null && wild.Holdings.Count == 2 && x.Units.Count == 5 && x.Units[1].Tenant == 2 && x.Units[1].Rent == 9 && x.Units[1].Applicants.Count == 0,
            "a unit of a building not held, or past the most, is dropped; an applicant whose number the building has already is left out");
        Check(wild != null && EstateStore.Read(line.Replace("^u:", "^u:Wide%20Road~3|1|5|1|2;"), out version) != null, "a unit's record too short to read is left out and the rest is read");
        EstateHolding big = Units(EstateCatalogue.UnitsMost);
        big.Place = "Tall~1";
        for (int u = 0; u < big.Units.Count; u++)
        {
            LetUnit(big, u, 100 + u, 60);
        }
        var full = new EstateState { Seed = 2 };
        full.Holdings.Add(big);
        EstateState fullBack = EstateStore.Read(EstateStore.Write(full), out version);
        Check(fullBack != null && fullBack.Holdings[0].Units.Count == EstateCatalogue.UnitsMost && fullBack.Holdings[0].Units[EstateCatalogue.UnitsMost - 1].Rent == 100 + EstateCatalogue.UnitsMost - 1,
            "a building of the most units keeps every lease through the save");
    }
}
