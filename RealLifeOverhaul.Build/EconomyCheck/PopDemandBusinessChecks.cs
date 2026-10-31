using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Dealership;
using RealLifeOverhaul.Hustles;
using RealLifeOverhaul.Population;

// The mod's own businesses on the city's people (the brief bizpop): each keeps its own mechanics and moves its old figure by the people's factor. For each:
// with no population, or on the anchor day (every factor a thousand), its figures are exactly what they were (nothing jumps); a pool the business shares with
// its rivals is the people's, and what the player's business and the rivals hold of it sums to it (money and people conserve); its figures lean the right
// way with the people; and over the ten years the population lives without the player (PopDemandChecks' run) its figures stay within the money scale.
internal static partial class Program
{
    private static void PopDemandBusinessChecks()
    {
        DemandBank();
        DemandBranches();
        DemandEstate();
        DemandConstruction();
        DemandShops();
    }

    // The ten-year range of a set of demands, each within its bounds; the first that is not, named.
    private static bool DemandsWithin(PopDemand[] demands, int least, int most, out string worst)
    {
        worst = string.Empty;
        bool ok = true;
        foreach (PopDemand d in demands)
        {
            DemandRange(d, out int lo, out int hi);
            bool one = lo >= least && hi <= most;
            if (!one && worst.Length == 0)
            {
                worst = ": " + d + " " + lo + " to " + hi;
            }
            ok &= one;
        }
        return ok;
    }

    // ---------------------------------------------------------------- the construction company: its buyers and its tenders

    private static void DemandConstruction()
    {
        bool same = true;
        for (int ask = 0; ask < BuildCatalogue.AskChancePermille.Length; ask++)
        {
            for (int rep = 0; rep <= 100; rep += 25)
            {
                same &= LandRules.BuyerChance(ask, rep, 1000) == LandRules.BuyerChance(ask, rep);
                same &= ShowFlatRules.BuyerChance(60, 50, rep, ViewingHost.Agent, 1000) == ShowFlatRules.BuyerChance(60, 50, rep, ViewingHost.Agent);
            }
        }
        for (int day = 1; day < 200; day++)
        {
            foreach (TenderSource src in new[] { TenderSource.Client, TenderSource.Public, TenderSource.Refit })
            {
                same &= TenderRules.NextIn(77, day, src, 1000) == TenderRules.NextIn(77, day, src);
            }
        }
        Check(same, "population demand, construction: with no population, and on the anchor day, flat buyers and the tenders' days come exactly as before");
        Check(LandRules.BuyerChance(1, 50, 1300) > LandRules.BuyerChance(1, 50) && LandRules.BuyerChance(0, 100, 9000) == 900 && LandRules.BuyerChance(2, 0, 100) == 50
              && ShowFlatRules.BuyerChance(60, 50, 50, ViewingHost.Player, 1500) > ShowFlatRules.BuyerChance(60, 50, 50, ViewingHost.Player),
            "population demand, construction: more households of an age to buy bring more buyers, a chance kept within its bounds");
        long usual = 0, busy = 0, quiet = 0;
        for (int day = 1; day <= 400; day++)
        {
            usual += TenderRules.NextIn(9, day, TenderSource.Client);
            busy += TenderRules.NextIn(9, day, TenderSource.Client, 1500);
            quiet += TenderRules.NextIn(9, day, TenderSource.Client, 600);
        }
        Check(busy < usual * 75 / 100 && quiet > usual * 150 / 100 && TenderRules.NextIn(9, 1, TenderSource.Public, 99999) >= 1,
            "population demand, construction: more demand for building brings the city's tenders sooner and less brings them later, never on the same day (" + usual / 400.0 + " days, " + busy / 400.0 + " busy, " + quiet / 400.0 + " quiet)");
        Check(DemandsWithin(new[] { PopDemand.FlatBuyers, PopDemand.ClientTenders, PopDemand.PublicTenders }, 500, 1800, out string worst),
            "population demand, construction: over ten years without the player its buyers and tenders stay between half and 1.8 times their first day" + worst);
    }

    // ---------------------------------------------------------------- the car showroom, online shops, the marketplace and the academy

    private static void DemandShops()
    {
        Check(RealLifeOverhaul.Careers.StudyRules.AcademyStudents(3, 0, 1000) == RealLifeOverhaul.Careers.StudyRules.AcademyStudents(3, 0) && RealLifeOverhaul.Careers.StudyRules.AcademyStudents(5, 1, 1000) == RealLifeOverhaul.Careers.StudyRules.AcademyStudents(5, 1)
              && RealLifeOverhaul.Careers.StudyRules.AcademyStudents(5, 0, 1500) > RealLifeOverhaul.Careers.StudyRules.AcademyStudents(5, 0) && RealLifeOverhaul.Careers.StudyRules.AcademyStudents(1, 0, 9000) == RealLifeOverhaul.Careers.StudyCatalogue.AcademyPerStaff
              && RealLifeOverhaul.Careers.StudyRules.AcademyStudents(5, 0, 0) == Math.Min(5 * RealLifeOverhaul.Careers.StudyCatalogue.AcademyPerStaff, 5 * 4),
            "population demand, academy: as before with no population; more residents who want a course fill more seats, never more than its staff can teach");
        bool online = true;
        for (int q = 0; q < OnlineCatalogue.Qualities; q++)
        {
            online &= OnlineRules.PerDay(q, false, 4, 60, 1000, 1, 4000, 0, 0, 1, 1, 1, NetSpeedRules.Unknown, 1000) == OnlineRules.PerDay(q, false, 4, 60, 1000, 1, 4000, 0, 0, 1, 1, 1, NetSpeedRules.Unknown);
        }
        int more = OnlineRules.PerDay(1, false, 4, 60, 1000, 1, 4000, 0, 0, 1, 1, 1, NetSpeedRules.Unknown, 1300), base0 = OnlineRules.PerDay(1, false, 4, 60, 1000, 1, 4000, 0, 0, 1, 1, 1, NetSpeedRules.Unknown);
        Check(online && more > base0 && OnlineRules.PerDay(2, true, 12, 100, 1600, 0, 5000, 50, 20, 0, 0, 1, NetSpeedRules.Unknown, 9000) <= OnlineCatalogue.OrdersMost[2] * 1000,
            "population demand, online shops: as before with no population; more of the households' spending online brings more orders, never past the site's bound");
        PopDemandState m = _demandLast;
        if (m != null)
        {
            PopDemandState was = CityDemand.Live;
            CityDemand.Live = m;
            bool kinds = CityDemand.Online("ba:businesstype_clothingstore") == m.OnlineFactors[(int)Spend.Clothing] && CityDemand.Online("ba:businesstype_gym") == m.Factors[(int)PopDemand.Online]
                         && CityDemand.Online(null) == m.Factors[(int)PopDemand.Online];
            CityDemand.Live = was;
            Check(kinds, "population demand, online shops: a clothing store's site follows its kind's online spending; a kind nobody buys online follows the whole online market");
        }
        var s = new HustleState { Seed = 99 };
        var homes = new List<HustlePlace> { new HustlePlace { Street = "Main", Number = 1 } };
        bool listings = true;
        int listed = 0, listedMore = 0, listedLess = 0;
        for (int day = 1; day <= 60; day++)
        {
            HustleRules.Morning(s, day, homes, null, null, 1000, 1000);
            int a = s.Listings.Count;
            HustleRules.Morning(s, day, homes, null, null, 1000);
            listings &= s.Listings.Count == a;
            listed += a;
            HustleRules.Morning(s, day, homes, null, null, 1000, 1500);
            listedMore += s.Listings.Count;
            HustleRules.Morning(s, day, homes, null, null, 1000, 600);
            listedLess += s.Listings.Count;
        }
        Check(listings && listed == 60 * HustleCatalogue.GoodsADay && listedMore > listed * 13 / 10 && listedLess < listed * 7 / 10,
            "population demand, marketplace: as before with no population; more households selling bring more listings each morning, fewer bring fewer (" + listed + ", " + listedMore + ", " + listedLess + ")");
        Check(HustleRules.OfferChance(1000, 1000, 60, false, 12, 1000) == HustleRules.OfferChance(1000, 1000, 60, false, 12) && HustleRules.OfferChance(1000, 1000, 60, false, 12, 1500) > HustleRules.OfferChance(1000, 1000, 60, false, 12)
              && HustleRules.OfferChance(600, 1000, 100, true, 12, 9000) <= 800 && HustleRules.OfferChance(1000, 1000, 60, false, 3, 2000) == 0,
            "population demand, marketplace: more buyers among the households bring more offers for a listed thing, a chance kept within its bounds and its hours");
        Check(DemandsWithin(new[] { PopDemand.CarBuyers, PopDemand.Online, PopDemand.Listings, PopDemand.Bargains, PopDemand.Students }, 500, 1800, out string worst),
            "population demand, shops: over ten years without the player car buyers, online spending, the marketplace's sellers and buyers and the academy's students stay between half and 1.8 times their first day" + worst);
    }

    // The worst and best of a demand's factor over the ten years, after the anchor.
    private static void DemandRange(PopDemand d, out int least, out int most)
    {
        least = int.MaxValue;
        most = 0;
        for (int n = 0; _demandYears != null && n < _demandYears.Length; n++)
        {
            int f = _demandYears[n][(int)d];
            least = Math.Min(least, f);
            most = Math.Max(most, f);
        }
        if (least == int.MaxValue)
        {
            least = most = 1000;
        }
    }

    // ---------------------------------------------------------------- the own bank: its savers and borrowers are the people's

    private static void DemandBank()
    {
        BankCity c = BankCity.Quiet(60);
        var neutral = c;
        neutral.People = new int[OwnBankCatalogue.Savers + OwnBankCatalogue.Segments];
        for (int i = 0; i < neutral.People.Length; i++)
        {
            neutral.People[i] = 1000;
        }
        bool same = true;
        for (int k = 0; k < OwnBankCatalogue.Savers; k++)
        {
            same &= OwnBankRules.SavingsPool(neutral, k) == OwnBankRules.SavingsPool(c, k);
        }
        for (int j = 0; j < OwnBankCatalogue.Segments; j++)
        {
            same &= OwnBankRules.DemandPool(neutral, j) == OwnBankRules.DemandPool(c, j);
        }
        Check(same && OwnBankRules.SavingsPool(c, OwnBankCatalogue.People) == OwnBankCatalogue.SavingsCents[OwnBankCatalogue.People],
            "population demand, own bank: with no population, and on the anchor day, the city's savings and borrowing are the catalogue's to the cent");

        var b = new OwnBank();
        OwnBankRules.Found(b, 1, 7, OwnBankRules.CapitalLeast(PriceRules.Start), "Check");
        var richer = neutral;
        richer.People = (int[])neutral.People.Clone();
        richer.People[OwnBankCatalogue.People] = 1300;
        richer.People[OwnBankCatalogue.Savers + OwnBankCatalogue.Poor] = 700;
        long pool = OwnBankRules.SavingsPool(richer, OwnBankCatalogue.People), own = OwnBankRules.SaversWanted(richer, b, OwnBankCatalogue.People, 1000);
        long poolBefore = OwnBankRules.SavingsPool(neutral, OwnBankCatalogue.People), ownBefore = OwnBankRules.SaversWanted(neutral, b, OwnBankCatalogue.People, 1000);
        Check(pool == poolBefore * 1300 / 1000 && own > ownBefore && own < pool && pool - own > poolBefore - ownBefore,
            "population demand, own bank: households saving three tenths more make the city's savings pool three tenths larger; the bank and the rival banks share it, the rivals holding the rest");
        Check(OwnBankRules.DemandPool(richer, OwnBankCatalogue.Poor) == OwnBankRules.DemandPool(neutral, OwnBankCatalogue.Poor) * 700 / 1000
              && OwnBankRules.DemandPool(richer, OwnBankCatalogue.Good) == OwnBankRules.DemandPool(neutral, OwnBankCatalogue.Good),
            "population demand, own bank: fewer poorly rated households borrow less from the city's lenders, each band by its own people");
        var cycle = neutral;
        cycle.Cycle = CycleState.Recession;
        Check(OwnBankRules.DemandPool(cycle, OwnBankCatalogue.Fair) < OwnBankRules.DemandPool(neutral, OwnBankCatalogue.Fair),
            "population demand, own bank: the national cycle still moves what the city borrows, the people's figures on top");

        // Ten years of the people: the city's savings and borrowing within the money scale.
        bool scale = true;
        string worst = string.Empty;
        foreach (PopDemand d in new[] { PopDemand.SaversPeople, PopDemand.SaversFirms, PopDemand.BorrowPoor, PopDemand.BorrowFair, PopDemand.BorrowGood, PopDemand.BorrowShops, PopDemand.BorrowFirms })
        {
            DemandRange(d, out int least, out int most);
            bool ok = least >= 500 && most <= 2000;
            if (!ok && worst.Length == 0)
            {
                worst = d + " " + least + " to " + most;
            }
            scale &= ok;
        }
        DemandRange(PopDemand.SaversPeople, out int sLeast, out int sMost);
        Check(scale, "population demand, own bank: over ten years without the player the city's savings and borrowing stay between half and twice their first day (people's savings "
                     + OwnBankCatalogue.SavingsCents[0] / 100 * sLeast / 1000 / 1000000 + "M to " + OwnBankCatalogue.SavingsCents[0] / 100 * sMost / 1000 / 1000000 + "M)" + (worst.Length > 0 ? ": " + worst : string.Empty));
    }

    // ---------------------------------------------------------------- the property business: hotel guests, car hire, applicants and their offers, the agency

    private static void DemandEstate()
    {
        bool same = true;
        for (int tier = 0; tier < EstateCatalogue.HotelTiers; tier++)
        {
            for (int season = 0; season < 4; season++)
            {
                same &= HotelRules.Demand(tier, season, 4, 0, 1, 1000, 1000, 600, false, 10, 1000) == HotelRules.Demand(tier, season, 4, 0, 1, 1000, 1000, 600, false, 10);
            }
        }
        for (int k = 0; k < EstateCatalogue.HireClasses; k++)
        {
            same &= HireRules.Demand(k, 1, 0, 1, 1000, 600, 1000) == HireRules.Demand(k, 1, 0, 1, 1000, 600);
        }
        for (int ask = 700; ask <= 1400; ask += 100)
        {
            same &= EstateRules.ApplicantChance(ask, 900, 1, 1000) == EstateRules.ApplicantChance(ask, 900, 1);
        }
        same &= AgencyRules.InstructionChance(400, 2, 1000) == AgencyRules.InstructionChance(400, 2);
        EstateClimate c = Climate(), neutral = Climate();
        neutral.Guests = neutral.Hirers = neutral.Seekers = neutral.Budget = neutral.Movers = 1000;
        same &= EstateRules.Seekers(c, "ba:neighborhood_midtown") == 1000 && AgencyRules.Movers(c, "ba:neighborhood_midtown") == 1000 && EstateRules.People(0) == 1000;
        var a = HotelAt();
        var b2 = HotelAt();
        for (int day = 1; day <= 60; day++)
        {
            HotelNight x = HotelRules.Night(a, 31, day, c), y = HotelRules.Night(b2, 31, day, neutral);
            same &= x.Sold0 == y.Sold0 && x.Sold1 == y.Sold1 && x.Takings == y.Takings && x.Costs == y.Costs;
        }
        Check(same, "population demand, property: with no population, and on the anchor day, hotels, car hire, applicants and the agency's owners come exactly as before, night for night");

        EstateClimate busy = Climate();
        busy.Guests = 1300;
        busy.Hirers = 1500;
        busy.Seekers = 1500;
        busy.Budget = 1200;
        busy.Movers = 1500;
        var quiet = HotelAt();
        var full = HotelAt();
        long soldQuiet = 0, soldBusy = 0;
        for (int day = 1; day <= 200; day++)
        {
            soldQuiet += HotelRules.Night(quiet, 77, day, c).Sold0;
            soldBusy += HotelRules.Night(full, 77, day, busy).Sold0;
        }
        Check(soldBusy > soldQuiet * 11 / 10 && soldBusy <= 200L * HotelRules.Rooms(1, 0),
            "population demand, property: more business travel and visitors fill more of a hotel's rooms, never more than it has (" + soldQuiet + " against " + soldBusy + " room nights)");
        Check(HireRules.Demand(0, 1, 0, 1, 1000, 600, 1500) > HireRules.Demand(0, 1, 0, 1, 1000, 600) && HireRules.Demand(0, 1, 0, 0, 600, 1000, 9000) == EstateCatalogue.HireDemandMost,
            "population demand, property: more visitors and residents without a car hire more cars, a car still idle now and then");
        int came = 0, cameBusy = 0;
        long offers = 0, offersBusy = 0;
        for (int day = 1; day <= 2000; day++)
        {
            EstateHolding h = Building(), hb = Building();
            RentApplicant one = EstateRules.Apply(h, 0, 4242, day, c), two = EstateRules.Apply(hb, 0, 4242, day, busy);
            if (one != null)
            {
                came++;
                offers += one.Offer * 1000L / Math.Max(1, EstateRules.MarketRent(h, c));
            }
            if (two != null)
            {
                cameBusy++;
                offersBusy += two.Offer * 1000L / Math.Max(1, EstateRules.MarketRent(hb, busy));
            }
        }
        long meanOffer = came > 0 ? offers / came : 0, meanBusy = cameBusy > 0 ? offersBusy / cameBusy : 0;
        Check(cameBusy > came * 13 / 10 && meanBusy > meanOffer + 60 && meanBusy < meanOffer + 140,
            "population demand, property: half as many households again looking for a flat bring a landlord more applicants, and a fifth more to spend offers about a tenth more ("
            + came + " to " + cameBusy + " applicants, offers " + meanOffer + " to " + meanBusy + " per mille of the market)");
        busy.HousingIn = district => district == "ba:neighborhood_midtown" ? 2000 : 500;
        Check(EstateRules.Seekers(busy, "ba:neighborhood_midtown") == 3000 && EstateRules.Seekers(busy, "ba:neighborhood_industrycity") == 750 && AgencyRules.Movers(busy, "ba:neighborhood_midtown") == 3000,
            "population demand, property: a district where more households are in the housing market brings its landlords and the agency more, one where fewer less");
        Check(AgencyRules.InstructionChance(400, 1, 1500) > AgencyRules.InstructionChance(400, 1) && AgencyRules.InstructionChance(1000, 0, 9000) == 1000,
            "population demand, property: more households moving and owners selling bring the agency more owners, a chance never past certain");
        bool scale = true;
        string worst = string.Empty;
        foreach (PopDemand d in new[] { PopDemand.HotelGuests, PopDemand.CarHire, PopDemand.Tenants, PopDemand.TenantBudget, PopDemand.Instructions })
        {
            DemandRange(d, out int least, out int most);
            bool ok = least >= 500 && most <= 1800;
            if (!ok && worst.Length == 0)
            {
                worst = d + " " + least + " to " + most;
            }
            scale &= ok;
        }
        DemandRange(PopDemand.HotelGuests, out int hLeast, out int hMost);
        Check(scale, "population demand, property: over ten years without the player hotel guests, hirers, applicants and the agency's owners stay between half and 1.8 times their first day (a hotel's ordinary night "
                     + EstateCatalogue.Occupancy * hLeast / 1000 / 10 + "% to " + EstateCatalogue.Occupancy * hMost / 1000 / 10 + "% full)" + (worst.Length > 0 ? ": " + worst : string.Empty));
    }

    // A mid-range hotel at going rates of 120 and 200.
    private static HotelFile HotelAt()
    {
        var f = new HotelFile { Slot = "hotel_check", Tier = 1 };
        f.Going[0] = 120;
        f.Going[1] = 200;
        return f;
    }

    // ---------------------------------------------------------------- the bank's branches: what the residents round each come for

    private static void DemandBranches()
    {
        var none = default(BranchPeople);
        var even = new BranchPeople { Sums = 1000, Saving = 1000, Borrowing = 1000, Poor = 1000, Fair = 1000, Good = 1000 };
        bool same = true;
        for (int draw = 0; draw < 1000; draw++)
        {
            same &= BranchRules.Kind(draw, false, even) == BranchRules.Kind(draw, false) && BranchRules.Kind(draw, true, even) == BranchRules.Kind(draw, true)
                    && BranchRules.Asker(draw, even) == BranchRules.Asker(draw) && BranchRules.Kind(draw, false, none) == BranchRules.Kind(draw, false);
        }
        for (int kind = 0; kind < BranchCatalogue.Kinds; kind++)
        {
            same &= BranchRules.Sum(kind, 500, PriceRules.Start, even) == BranchRules.Sum(kind, 500, PriceRules.Start) && BranchRules.Sum(kind, 500, PriceRules.Start, none) == BranchRules.Sum(kind, 500, PriceRules.Start);
        }
        Check(same, "population demand, branches: residents as the city's average, or no population, bring every branch the catalogue's own mix and sums, visit for visit");

        var savers = new BranchPeople { Sums = 1500, Saving = 1400, Borrowing = 600, Poor = 300, Fair = 900, Good = 1500 };
        int deposits = 0, withdrawals = 0, loans = 0, evenDeposits = 0, evenWithdrawals = 0, evenLoans = 0, poor = 0, evenPoor = 0;
        for (int draw = 0; draw < 1000; draw++)
        {
            int k = BranchRules.Kind(draw, false, savers), e = BranchRules.Kind(draw, false);
            deposits += k == BranchCatalogue.Deposit ? 1 : 0;
            withdrawals += k == BranchCatalogue.Withdraw ? 1 : 0;
            loans += k == BranchCatalogue.Loan ? 1 : 0;
            evenDeposits += e == BranchCatalogue.Deposit ? 1 : 0;
            evenWithdrawals += e == BranchCatalogue.Withdraw ? 1 : 0;
            evenLoans += e == BranchCatalogue.Loan ? 1 : 0;
            poor += BranchRules.Asker(draw, savers) == OwnBankCatalogue.Poor ? 1 : 0;
            evenPoor += BranchRules.Asker(draw) == OwnBankCatalogue.Poor ? 1 : 0;
        }
        Check(deposits > evenDeposits && withdrawals < evenWithdrawals && loans < evenLoans && poor < evenPoor
              && BranchRules.Sum(BranchCatalogue.Deposit, 500, PriceRules.Start, savers) == BranchRules.Sum(BranchCatalogue.Deposit, 500, PriceRules.Start) * 1500 / 1000,
            "population demand, branches: round a branch of richer savers more pay in and fewer take out or borrow, the sums are larger, and fewer askers are poorly rated");
        var extreme = new BranchPeople { Sums = 9000, Saving = 9000, Borrowing = 9000, Poor = 9000, Fair = 0, Good = 0 };
        long top = BranchRules.Sum(BranchCatalogue.Loan, 999, PriceRules.Start, extreme);
        Check(top <= BranchRules.Sum(BranchCatalogue.Loan, 999, PriceRules.Start) * BranchCatalogue.PeopleMost / 1000 + 1 && BranchRules.Asker(500, extreme) == OwnBankCatalogue.Poor,
            "population demand, branches: however far a district leans, a counter's sums stay within twice the usual");

        PopDemandState m = _demandLast;
        if (m != null)
        {
            long hh = 0, income = 0, saving = 0;
            for (int d = 0; d < m.Districts; d++)
            {
                hh += m.Households[d];
                income += (long)m.Households[d] * m.IncomeLean[d];
                saving += (long)m.Disposable[d] * m.SavingLean[d];
            }
            long meanIncome = hh > 0 ? income / hh : 1000;
            Check(Math.Abs(meanIncome - 1000) <= 10, "population demand, branches: the districts' income leans average the city's over its households, so the city's branches together keep the catalogue's sums (" + meanIncome + ")");
        }
    }
}
