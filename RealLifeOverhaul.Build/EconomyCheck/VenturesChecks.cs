using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Ventures;

// Checks for investors and the market (Scripts/Economy/Ventures): the cast, an offer that comes to the player, a deal
// over a long run, and the save line.
internal static partial class Program
{
    private static readonly string[] FourRivals = { "North Star Holdings", "Bay Avenue Holdings", "Old Mill Holdings", "Summit Holdings" };

    private static void Ventures()
    {
        VenturesCast();
        VenturesOffers();
        VenturesDeals();
        VenturesDealLadder();
        VenturesSave();
    }

    private static VenturesFile NewVentures(int seed, string[] rivals = null)
    {
        var file = new VenturesFile { StartDay = 1 };
        Cast.Make(file, seed, rivals ?? FourRivals);
        return file;
    }

    private static Investor Find(VenturesFile file, InvestorKind kind, Temperament temper)
    {
        foreach (Investor v in file.Investors)
        {
            if (v.Kind == kind && v.Temper == temper)
            {
                return v;
            }
        }
        return null;
    }

    private static Investor Find(VenturesFile file, InvestorKind kind)
    {
        foreach (Investor v in file.Investors)
        {
            if (v.Kind == kind)
            {
                return v;
            }
        }
        return null;
    }

    /// <summary>A business making this much a day, every day, for as long as anybody has looked.</summary>
    private static BusinessFigures Steady(float profit, int profitableDays = 14, int age = 60)
    {
        return new BusinessFigures
        {
            Exists = true, DaysOnRecord = 14, HasYesterday = true, ProfitYesterday = profit, SalesYesterday = profit * 3f, SalesWeek = profit * 21f, AvgProfit7 = profit,
            AvgProfit14 = profit, AvgCost14 = profit * 2f, ProfitableDays = profitableDays, AgeDays = age, Sector = Sector.Retail
        };
    }

    private static int Bits(int mask)
    {
        int bits = 0;
        for (; mask != 0; mask &= mask - 1)
        {
            bits++;
        }
        return bits;
    }

    private static void VenturesCast()
    {
        VenturesFile a = NewVentures(4711), b = NewVentures(4711), other = NewVentures(4712);
        Check(VenturesStore.Write(a) == VenturesStore.Write(b), "ventures: the same seed gives the same investors and companies");
        Check(VenturesStore.Write(a) != VenturesStore.Write(other), "ventures: another seed gives another cast");
        bool kinds = true, tempers = true, cheques = true, likes = true, names = true, companies = true, sectors = true, rivals = true, prices = true;
        bool two = false, three = false;
        for (int seed = 1; seed <= 300; seed++)
        {
            VenturesFile f = NewVentures(seed * 7919);
            int tycoons = 0, angels = 0, firms = 0;
            var seen = new HashSet<string>();
            int firstTycoon = -1;
            for (int i = 0; i < f.Investors.Count; i++)
            {
                Investor v = f.Investors[i];
                int k = (int)v.Kind;
                tycoons += v.Kind == InvestorKind.Tycoon ? 1 : 0;
                angels += v.Kind == InvestorKind.Angel ? 1 : 0;
                firms += v.Kind == InvestorKind.Firm ? 1 : 0;
                if (v.Kind == InvestorKind.Tycoon && firstTycoon < 0)
                {
                    firstTycoon = i;
                }
                tempers &= v.Kind != InvestorKind.Firm || v.Temper == Temperament.Demanding;
                tempers &= v.Kind != InvestorKind.Angel || v.Temper != Temperament.Demanding;
                cheques &= Array.IndexOf(VenturesCatalogue.ChequeLeast[k], v.ChequeLeast) >= 0 && Array.IndexOf(VenturesCatalogue.ChequeMost[k], v.ChequeMost) >= 0 && v.ChequeLeast < v.ChequeMost;
                cheques &= v.Diligence >= VenturesCatalogue.DiligenceLeast[k] && v.Diligence <= VenturesCatalogue.DiligenceMost[k];
                likes &= Bits(v.Likes) == VenturesCatalogue.LikedSectors && v.Likes < 1 << (int)Sector.Count;
                names &= seen.Add(v.Name) && v.Id == i + 1;
            }
            tempers &= f.Investors[firstTycoon].Temper == Temperament.Patient && f.Investors[firstTycoon + 1].Temper == Temperament.Demanding;
            kinds &= tycoons >= VenturesCatalogue.TycoonsLeast && tycoons <= VenturesCatalogue.TycoonsMost && angels == VenturesCatalogue.Angels && firms == VenturesCatalogue.Firms;
            two |= tycoons == 2;
            three |= tycoons == 3;
            companies &= f.Companies.Count == VenturesCatalogue.Companies;
            int sectorMask = 0, rivalCount = 0;
            var companyNames = new HashSet<string>();
            foreach (Company c in f.Companies)
            {
                sectorMask |= 1 << (int)c.Sector;
                rivalCount += c.Rival ? 1 : 0;
                companies &= companyNames.Add(c.Name) && c.Name.Length > 0;
                prices &= c.PriceCents >= VenturesCatalogue.PriceLeastCents && c.PriceCents <= VenturesCatalogue.PriceMostCents && c.VolBp >= VenturesCatalogue.VolLeastBp
                          && c.VolBp <= VenturesCatalogue.VolMostBp && c.YieldBp >= VenturesCatalogue.DividendLeastBp && c.YieldBp <= VenturesCatalogue.DividendMostBp
                          && c.HistoryCount == 1 && c.FairCents == c.PriceCents;
            }
            sectors &= sectorMask == (1 << (int)MarketSector.Count) - 1;
            rivals &= rivalCount == VenturesCatalogue.RivalsListed;
        }
        Check(kinds, "ventures: every cast has two or three tycoons, three angels and two funds");
        Check(two && three, "ventures: some saves have two tycoons and some three");
        Check(tempers, "ventures: the first tycoon is patient and the second demanding, every fund is demanding and no angel is");
        Check(cheques, "ventures: every investor's smallest and largest sums and diligence are their kind's");
        Check(likes, "ventures: every investor likes two sectors");
        Check(names, "ventures: no two investors of a save share a name, and they are numbered from one");
        Check(companies, "ventures: twelve companies, no two with one name");
        Check(sectors, "ventures: every sector of the market has a company");
        Check(rivals && prices, "ventures: the game's rivals are listed, four at most, and every company starts inside the catalogue's figures");
        VenturesFile none = NewVentures(99, new string[0]);
        bool anyRival = false;
        foreach (Company c in none.Companies)
        {
            anyRival |= c.Rival;
        }
        Check(none.Companies.Count == VenturesCatalogue.Companies && !anyRival, "ventures: with no rival to list there are still twelve companies, all invented");
        Check(VenturesCatalogue.Companies >= 10 && VenturesCatalogue.Companies <= 14, "ventures: ten to fourteen companies");
        Check(VenturesCatalogue.SectorOf("ba:businesstype_coffeeshop") == Sector.Food && VenturesCatalogue.SectorOf("ba:businesstype_webdevelopmentagency") == Sector.Office
              && VenturesCatalogue.SectorOf("ba:businesstype_gym") == Sector.Leisure && VenturesCatalogue.SectorOf("ba:businesstype_factory") == Sector.Industry
              && VenturesCatalogue.SectorOf("ba:businesstype_giftshop") == Sector.Retail && VenturesCatalogue.SectorOf(null) == Sector.Retail, "ventures: the game's business types fall into their sectors");
        Check(VenturesCatalogue.Round2(1234) == 1200 && VenturesCatalogue.Round2(987654) == 990000 && VenturesCatalogue.Round2(42) == 42 && VenturesCatalogue.Round2(-361) == -360
              && VenturesCatalogue.Round2(0) == 0 && VenturesCatalogue.Round2(7) == 7, "ventures: money is said in two figures that count");
        Check(VenturesCatalogue.Share(1000f, 2500) == 250 && VenturesCatalogue.Share(-1000f, 2500) == 0 && VenturesCatalogue.Share(999f, 1) == 0 && VenturesCatalogue.Share(0f, 5000) == 0,
            "ventures: a share of a profit is rounded down, and of a loss it is nothing");
    }

    private static void VenturesOffers()
    {
        Check(!OfferRules.Qualifies(Steady(5000f, 9)), "offer: nine profitable days of fourteen is not enough");
        Check(!OfferRules.Qualifies(Steady(1499f)), "offer: a recent profit under the floor is not enough");
        Check(OfferRules.Qualifies(Steady(1500f, 10)), "offer: ten profitable days and the floor's profit qualify");
        BusinessFigures gone = Steady(5000f);
        gone.Exists = false;
        Check(!OfferRules.Qualifies(gone), "offer: a business that is no longer run does not qualify");
        Check(OfferRules.Chance(false, 0) == 25 && OfferRules.Chance(true, 0) == 40, "offer: a quarter each week, more for a sector an investor likes");
        Check(OfferRules.Chance(false, -1) == 12 && OfferRules.Chance(true, -1) == 20 && OfferRules.Chance(false, 1) == 31 && OfferRules.Chance(true, 1) == 50,
            "offer: half as likely in a slowdown and a quarter more likely in a boom");
        Check(OfferRules.Growth(Temperament.Patient, VenturesCatalogue.Standard) == 10 && OfferRules.Growth(Temperament.Demanding, VenturesCatalogue.Hardcore) == 37
              && OfferRules.Growth(Temperament.Even, VenturesCatalogue.Relaxed) == 7, "offer: the growth expected follows the temperament and the level");

        VenturesFile file = NewVentures(31337);
        Investor patient = Find(file, InvestorKind.Tycoon, Temperament.Patient), demanding = Find(file, InvestorKind.Tycoon, Temperament.Demanding);
        Check(OfferRules.InboundShare(patient) == 2000 && OfferRules.InboundShare(demanding) == 3000, "offer: a patient tycoon asks a fifth of the profit and a demanding one three tenths");
        BusinessFigures good = Steady(6000f);
        Offer offer = OfferRules.Make(file, patient, "main#1", "Corner Shop", good, 20, VenturesCatalogue.Standard, OfferSource.Inbound);
        int expected = Math.Min(patient.ChequeMost, 6000 * 60 * 2000 / 10000);
        Check(offer != null && offer.Amount == expected && offer.ShareBp == (expected == 72000 ? 2000 : offer.ShareBp) && offer.Amount >= patient.ChequeLeast && offer.Amount <= patient.ChequeMost,
            "offer: sixty days of recent profit, the investor's share of it, inside their smallest and largest sums (" + (offer != null ? offer.Amount : -1) + ")");
        Check(offer != null && offer.LapseDay == 25 && offer.TargetPct == 10 && offer.TargetDays == 30 && file.Unseen == 1, "offer: it waits five days and names its target");
        Check(OfferRules.Make(file, demanding, "main#1", "Corner Shop", good, 20, VenturesCatalogue.Standard, OfferSource.Inbound) == null, "offer: one offer at a time for a business");
        Check(OfferRules.Make(file, demanding, "side#2", "Kiosk", Steady(1500f), 20, VenturesCatalogue.Standard, OfferSource.Inbound) == null,
            "offer: figures that do not carry the investor's smallest sum bring no offer");
        // A profit so large that the share of it is over the largest sum: the sum is the largest, and the share shrinks to fit.
        int big, bigShare;
        bool fits = OfferRules.Terms(file, patient, "big#9", 400000f, 60, 2000, 20, out big, out bigShare);
        Check(fits && big == patient.ChequeMost && bigShare < 2000 && VenturesCatalogue.Share(400000f * 60, bigShare) >= patient.ChequeMost
              && VenturesCatalogue.Share(400000f * 60, bigShare - 1) < patient.ChequeMost,
            "offer: a sum over the investor's largest is cut to it and the share with it");

        // The counter: over many offers every answer comes, a patient investor says yes more often than a demanding one, and the same offer always gets the same answer.
        int[] byPatient = new int[3], byDemanding = new int[3];
        bool moneyRight = true, shareRight = true, once = true, same = true;
        for (int n = 0; n < 600; n++)
        {
            for (int who = 0; who < 2; who++)
            {
                Investor investor = who == 0 ? patient : demanding;
                var o = new Offer { Id = 1000 + n, InvestorId = investor.Id, Place = "p", Amount = 40000, ShareBp = 2000 };
                bool money = n % 2 == 0;
                CounterAnswer answer = OfferRules.Counter(file, o, investor, Steady(3000f, 10), money);
                (who == 0 ? byPatient : byDemanding)[(int)answer]++;
                int wantMoney = answer == CounterAnswer.Accepted ? 50000 : answer == CounterAnswer.HalfWay ? 45000 : 40000;
                int wantShare = answer == CounterAnswer.Accepted ? 1500 : answer == CounterAnswer.HalfWay ? 1750 : 2000;
                moneyRight &= !money || (o.Amount == Math.Min(investor.ChequeMost, wantMoney) && o.ShareBp == 2000);
                shareRight &= money || (o.ShareBp == wantShare && o.Amount == 40000);
                once &= OfferRules.Counter(file, o, investor, Steady(3000f, 10), money) == CounterAnswer.Refused && o.Countered;
                var again = new Offer { Id = 1000 + n, InvestorId = investor.Id, Place = "p", Amount = 40000, ShareBp = 2000 };
                same &= OfferRules.Counter(file, again, investor, Steady(3000f, 10), money) == answer;
            }
        }
        Check(byPatient[0] > 0 && byPatient[1] > 0 && byPatient[2] > 0 && byDemanding[0] > 0 && byDemanding[1] > 0 && byDemanding[2] > 0, "counter: an investor says yes, meets half way or holds to the terms");
        Check(byPatient[0] > byDemanding[0] && byPatient[2] < byDemanding[2], "counter: a patient investor gives way more often than a demanding one (" + byPatient[0] + " and " + byDemanding[0] + " of 600)");
        Check(moneyRight, "counter: more money is a quarter more, or an eighth half way, and never over the investor's largest sum");
        Check(shareRight, "counter: a smaller share is a quarter less, or an eighth half way");
        Check(once && same, "counter: one counter to an offer, and the same offer always gets the same answer");
        var better = new Offer { Id = 5, InvestorId = demanding.Id, Place = "p", Amount = 40000, ShareBp = 2000 };
        int yesGood = 0, yesPoor = 0;
        for (int n = 0; n < 400; n++)
        {
            better.Id = 3000 + n;
            better.Countered = false;
            yesGood += OfferRules.Counter(file, better, demanding, Steady(3000f, 14), true) == CounterAnswer.Accepted ? 1 : 0;
            better.Countered = false;
            better.Amount = 40000;
            yesPoor += OfferRules.Counter(file, better, demanding, Steady(3000f, 10), true) == CounterAnswer.Accepted ? 1 : 0;
            better.Amount = 40000;
        }
        Check(yesGood > yesPoor, "counter: a business with more profitable days gets a yes more often (" + yesGood + " against " + yesPoor + " of 400)");

        // Taking an offer up.
        Deal deal = OfferRules.Accept(file, offer, good, 21);
        Check(deal != null && file.Offers.Count == 0 && file.Deals.Count == 1 && deal.Amount == offer.Amount && deal.ShareBp == offer.ShareBp && deal.Mood == VenturesCatalogue.MoodStart
              && deal.BaseProfit == 6000 && deal.TargetProfit == 6600 && deal.TargetDay == 51 && file.InvestedTotal == offer.Amount, "deal: an offer taken up is a deal with a target a tenth over the recent profit, thirty days on");
        Check(OfferRules.Make(file, patient, "main#1", "Corner Shop", good, 30, VenturesCatalogue.Standard, OfferSource.Inbound) == null, "offer: no second deal with the same investor on a business");
        Offer second = OfferRules.Make(file, patient, "main#1", "Corner Shop", good, 30, VenturesCatalogue.Standard, OfferSource.FollowOn);
        Check(second != null && second.Amount == Math.Min(patient.ChequeMost, 6000 * 70 * 2000 / 10000), "offer: a second round values the business at seventy days of profit");
        int before = deal.Amount;
        Deal merged = OfferRules.Accept(file, second, good, 31);
        Check(ReferenceEquals(merged, deal) && file.Deals.Count == 1 && deal.Amount == before + second.Amount && deal.ShareBp == 4000, "deal: a second round adds its money and share to the deal that is there");
        int dealsOn;
        Check(file.ShareOn("main#1", 31, out dealsOn) == 4000 && dealsOn == 1, "deal: the share promised on a business adds up");
        int roomSum, roomShare;
        OfferRules.Terms(file, demanding, "main#1", 6000f, 60, 3000, 31, out roomSum, out roomShare);
        Check(roomShare == 2000, "offer: investors together never hold more than six tenths of a business's profit");

        // Money for a business not yet open.
        var forNew = new Offer { Id = file.NextId++, InvestorId = patient.Id, TypeName = "ba:businesstype_webdevelopmentagency", Budget = 50000, Amount = 25000, ShareBp = 2500, TargetPct = 10, TargetDays = 30 };
        file.Offers.Add(forNew);
        Check(forNew.ForNew && OfferRules.Accept(file, forNew, default(BusinessFigures), 40) == null, "pledge: an offer for a business not yet open is not a deal yet");
        Pledge pledge = OfferRules.Promise(file, forNew, 40);
        Check(pledge != null && pledge.OpenBy == 54 && file.Pledges.Count == 1 && file.Offers.Count == 0 && file.InvestedTotal == before + second.Amount, "pledge: the money waits fourteen days for the business to open, and none has moved");
        Deal opened = OfferRules.Open(file, pledge, "new#3", "Pixel Works", 45);
        Check(opened != null && file.Pledges.Count == 0 && opened.Amount == 25000 && opened.TargetProfit == OfferRules.YoungTarget && opened.TargetDay == 75, "pledge: opened in time, the deal starts with a first small target");
    }

    /// <summary>A deal on a business over four hundred days of profits and losses: the investor's share of each profit and nothing of a loss.</summary>
    private static void VenturesDeals()
    {
        VenturesFile file = NewVentures(2024);
        Investor patient = Find(file, InvestorKind.Tycoon, Temperament.Patient);
        var deal = new Deal { Id = file.NextId++, InvestorId = patient.Id, Place = "a#1", Business = "A", Amount = 60000, ShareBp = 2500, StartDay = 1, Mood = 60, Stage = DealStage.Cold };
        file.Deals.Add(deal);
        var purse = new Purse { Money = 10000000 };
        var said = new List<DealEvent>();
        var random = new VenturesRandom(5);
        long expected = 0;
        bool exact = true, neverOnLoss = true;
        for (int day = 2; day <= 401; day++)
        {
            float profit = random.Range(-2500, 5000);
            BusinessFigures b = Steady(profit);
            int was = purse.Money;
            said.Clear();
            DealRules.RunDay(file, deal, b, day, VenturesCatalogue.Standard, purse, said, null);
            int paid = was - purse.Money;
            int due = profit > 0f ? (int)Math.Floor(profit * 0.25) : 0;
            expected += due;
            exact &= paid == due;
            neverOnLoss &= profit > 0f || paid == 0;
        }
        Check(exact && deal.PaidOut == expected && file.PaidOutTotal == expected, "deal: over four hundred days the investor has had exactly their share of every day's profit (" + deal.PaidOut + ")");
        Check(neverOnLoss, "deal: nothing is paid on a day of loss");
        Check(deal.Arrears == 0 && deal.Mood == 60, "deal: paid in full each day, nothing is carried over and the mood has not moved");

        // Cash that is short: what there is goes, the rest is carried to the next day, and the mood falls.
        var poor = new Purse { Money = 100 };
        said.Clear();
        DealRules.RunDay(file, deal, Steady(4000f), 402, VenturesCatalogue.Standard, poor, said, null);
        Check(poor.Money == 0 && deal.Arrears == 900 && deal.Mood == 55 && said.Exists(e => e.What == DealNews.Short), "deal: with too little cash the rest of the share is carried over");
        poor.Money = 5000;
        DealRules.RunDay(file, deal, Steady(0f), 403, VenturesCatalogue.Standard, poor, said, null);
        Check(poor.Money == 4100 && deal.Arrears == 0, "deal: what was carried over is taken first the next day");
        BusinessFigures noStatement = Steady(4000f);
        noStatement.HasYesterday = false;
        DealRules.RunDay(file, deal, noStatement, 404, VenturesCatalogue.Standard, poor, said, null);
        Check(poor.Money == 4100, "deal: a day with no statement from the game pays nothing");

        // The price to buy the investor out.
        Check(DealRules.BuyOutPrice(deal, Steady(4000f)) == 4000 * 75 / 4, "buy-out: the investor's share of seventy-five days of recent profit (" + DealRules.BuyOutPrice(deal, Steady(4000f)) + ")");
        Check(DealRules.BuyOutPrice(deal, Steady(100f)) == 66000 && DealRules.BuyOutPrice(deal, Steady(-500f)) == 66000, "buy-out: never less than a tenth over what they put in");
        var thin = new Purse { Money = 65999 };
        Check(!DealRules.BuyOut(file, deal, Steady(100f), thin) && thin.Money == 65999 && file.Deals.Count == 1, "buy-out: refused, and nothing taken, when the cash is not there");
        thin.Money = 70000;
        int name = file.Reputation;
        Check(DealRules.BuyOut(file, deal, Steady(100f), thin) && thin.Money == 4000 && file.Deals.Count == 0 && file.Reputation == name + VenturesCatalogue.ReputationBoughtOut,
            "buy-out: the price is taken, the deal is over and the founder's name with investors rises a little");

        // The business is sold or closed.
        var sold = new Deal { Id = file.NextId++, InvestorId = patient.Id, Place = "b#2", Business = "B", Amount = 40000, ShareBp = 2000, PaidOut = 10000, Mood = 60 };
        file.Deals.Add(sold);
        BusinessFigures none = default(BusinessFigures);
        said.Clear();
        Check(!DealRules.RunDay(file, sold, none, 10, VenturesCatalogue.Standard, purse, said, null) && sold.Stage == DealStage.Called && sold.CallOwed == 30000 && sold.CallPerDay == 1000,
            "deal: a business sold or closed leaves the investor owed what they put in less what they were paid, over thirty days");
        var soldRelaxed = new Deal { Id = file.NextId++, InvestorId = patient.Id, Place = "c#3", Business = "C", Amount = 40000, ShareBp = 2000, PaidOut = 10000, Mood = 60 };
        file.Deals.Add(soldRelaxed);
        DealRules.RunDay(file, soldRelaxed, none, 10, VenturesCatalogue.Relaxed, purse, said, null);
        Check(soldRelaxed.CallOwed == 15000, "deal: on Relaxed that is halved");
        var repaid = new Deal { Id = file.NextId++, InvestorId = patient.Id, Place = "d#4", Business = "D", Amount = 40000, ShareBp = 2000, PaidOut = 45000, Mood = 60 };
        file.Deals.Add(repaid);
        Check(DealRules.RunDay(file, repaid, none, 10, VenturesCatalogue.Standard, purse, said, null) && file.Deal(repaid.Id) == null,
            "deal: an investor who has already had their money back is owed nothing when the business goes");
        Check(DealRules.BuyOutPrice(sold, Steady(9000f)) == 30000, "buy-out: once the money is called, the price is what is still called");
    }

    /// <summary>A target missed three times running: a warning, a demand, and then the money called back, or not, by who the investor is and the level.</summary>
    private static void VenturesDealLadder()
    {
        VenturesFile file = NewVentures(77);
        Investor demanding = Find(file, InvestorKind.Tycoon, Temperament.Demanding), patient = Find(file, InvestorKind.Tycoon, Temperament.Patient), firm = Find(file, InvestorKind.Firm);
        var purse = new Purse { Money = 5000000 };
        var said = new List<DealEvent>();
        int claims = 0, claimed = 0;
        Func<int, int> court = amount =>
        {
            claims++;
            claimed = amount;
            return 41;
        };
        BusinessFigures flat = Steady(2000f);

        Deal deal = OfferRules.Accept(file, AddOffer(file, demanding, "a#1", 50000, 3000), flat, 1);
        Check(deal.TargetProfit == 2500 && deal.TargetDay == 19, "ladder: a demanding tycoon wants a quarter more profit in eighteen days");
        int day = 2;
        for (; day < deal.TargetDay; day++)
        {
            DealRules.RunDay(file, deal, flat, day, VenturesCatalogue.Standard, purse, said, court);
        }
        Check(deal.Misses == 0 && deal.Stage == DealStage.Running, "ladder: nothing happens before the target's day");
        said.Clear();
        DealRules.RunDay(file, deal, flat, day++, VenturesCatalogue.Standard, purse, said, court);
        Check(deal.Misses == 1 && deal.Stage == DealStage.Running && deal.TargetDay == 37 && deal.Mood == 45 && said.Exists(e => e.What == DealNews.Warned), "ladder: the first miss is a warning and eighteen more days");
        for (; day <= 37; day++)
        {
            said.Clear();
            DealRules.RunDay(file, deal, flat, day, VenturesCatalogue.Standard, purse, said, court);
        }
        Check(deal.Misses == 2 && deal.Stage == DealStage.Demand && deal.DemandDay == 42 && said.Exists(e => e.What == DealNews.Demanded), "ladder: the second miss is a demand, to be answered in five days");
        for (; day <= 42; day++)
        {
            said.Clear();
            DealRules.RunDay(file, deal, flat, day, VenturesCatalogue.Standard, purse, said, court);
        }
        Check(deal.Stage == DealStage.Running && deal.BoardUntil == 72 && deal.ShareNow(50) == 4000 && deal.ShareNow(72) == 3000 && said.Exists(e => e.What == DealNews.DemandDefault),
            "ladder: a demand left unanswered gives the investor a seat on the board: a tenth more of the profit for thirty days");
        int cash = purse.Money;
        DealRules.RunDay(file, deal, flat, day++, VenturesCatalogue.Standard, purse, said, court);
        Check(cash - purse.Money == 800, "ladder: with the seat the share taken is four tenths");
        for (; day <= 60; day++)
        {
            said.Clear();
            DealRules.RunDay(file, deal, flat, day, VenturesCatalogue.Standard, purse, said, court);
        }
        int callSum = Math.Max(12500, 50000 - deal.PaidOut / 2);
        Check(deal.Stage == DealStage.Called && deal.CallOwed == callSum && deal.CallPerDay == (callSum + 29) / 30 && deal.BoardUntil == 0 && said.Exists(e => e.What == DealNews.Called),
            "ladder: the third miss calls the money back: what was put in less half of what was paid, over thirty days (" + deal.CallOwed + ")");
        cash = purse.Money;
        int paidOut = deal.PaidOut;
        DealRules.RunDay(file, deal, Steady(9000f), day++, VenturesCatalogue.Standard, purse, said, court);
        Check(cash - purse.Money == deal.CallPerDay && deal.PaidOut == paidOut, "ladder: once the money is called the share of the profit stops and the daily sum is taken");
        bool ended = false;
        int days = 1;
        while (!ended && days < 40)
        {
            ended = DealRules.RunDay(file, deal, flat, day++, VenturesCatalogue.Standard, purse, said, court);
            days++;
        }
        Check(ended && days == 30 && file.Deal(deal.Id) == null && claims == 0, "ladder: paid each day, the called sum is cleared in thirty days and the deal is over");

        // A patient investor, and any investor on Relaxed, never calls.
        Check(!DealRules.Callable(Temperament.Patient, VenturesCatalogue.Hardcore, 9) && !DealRules.Callable(Temperament.Even, VenturesCatalogue.Standard, 9)
              && !DealRules.Callable(Temperament.Demanding, VenturesCatalogue.Relaxed, 9) && DealRules.Callable(Temperament.Demanding, VenturesCatalogue.Standard, 3)
              && !DealRules.Callable(Temperament.Demanding, VenturesCatalogue.Standard, 2), "ladder: only a demanding investor calls money back, after three misses, and nobody does on Relaxed");
        Deal calm = OfferRules.Accept(file, AddOffer(file, patient, "b#2", 50000, 2000), flat, 100);
        Deal easy = OfferRules.Accept(file, AddOffer(file, demanding, "c#3", 50000, 3000), flat, 100);
        for (day = 101; day <= 260; day++)
        {
            DealRules.RunDay(file, calm, flat, day, VenturesCatalogue.Hardcore, purse, said, court);
            DealRules.RunDay(file, easy, flat, day, VenturesCatalogue.Relaxed, purse, said, court);
        }
        Check(calm.Stage == DealStage.Cold && calm.TargetDay == 0 && easy.Stage == DealStage.Cold, "ladder: they lose interest instead: the share goes on and no more targets are set");

        // A target met.
        Deal growing = OfferRules.Accept(file, AddOffer(file, patient, "d#4", 50000, 2000), flat, 300);
        int reputation = file.Reputation, met = file.TargetsMet;
        said.Clear();
        for (day = 301; day <= 330; day++)
        {
            DealRules.RunDay(file, growing, Steady(2600f), day, VenturesCatalogue.Standard, purse, said, court);
        }
        Check(growing.TargetsMet == 1 && growing.FollowOn && growing.Mood == 80 && growing.BaseProfit == 2600 && growing.TargetProfit == 2860 && growing.TargetDay == 360
              && file.Reputation == reputation + VenturesCatalogue.ReputationMet && file.TargetsMet == met + 1 && said.Exists(e => e.What == DealNews.TargetMet),
            "ladder: a target met lifts the mood, opens a second round and sets the next target from the new profit");

        // A fund already sits on the board: its demand is answered by a promise to cut costs, which is tested.
        Deal fund = OfferRules.Accept(file, AddOffer(file, firm, "e#5", 200000, 3000), flat, 400);
        fund.Misses = 1;
        fund.TargetDay = 401;
        DealRules.RunDay(file, fund, flat, 401, VenturesCatalogue.Standard, purse, said, court);
        Check(fund.Stage == DealStage.Demand && !DealRules.Answer(fund, firm, 402, true, flat) && fund.Stage == DealStage.Demand, "ladder: a fund cannot be given the seat it already has");
        Check(DealRules.Answer(fund, firm, 402, false, flat) && fund.Stage == DealStage.Running && fund.CostTarget == 3600 && fund.CostDay == 416 && fund.TargetDay == 420,
            "ladder: a promise to cut costs: a tenth off the average daily cost, tested fourteen days on");
        BusinessFigures cheaper = Steady(2000f);
        cheaper.AvgCost14 = 3500f;
        for (day = 403; day <= 416; day++)
        {
            said.Clear();
            DealRules.RunDay(file, fund, cheaper, day, VenturesCatalogue.Standard, purse, said, court);
        }
        Check(fund.CostDay == 0 && fund.Misses == 2 && said.Exists(e => e.What == DealNews.PromiseKept) && fund.Stage == DealStage.Running, "ladder: a promise kept is noted and nothing more follows");
        Deal broken = OfferRules.Accept(file, AddOffer(file, firm, "f#6", 200000, 3000), flat, 400);
        broken.Misses = 2;
        broken.Stage = DealStage.Demand;
        broken.DemandDay = 405;
        DealRules.Answer(broken, firm, 402, false, flat);
        for (day = 403; day <= 416; day++)
        {
            said.Clear();
            DealRules.RunDay(file, broken, flat, day, VenturesCatalogue.Standard, purse, said, court);
        }
        Check(broken.Stage == DealStage.Called && said.Exists(e => e.What == DealNews.PromiseBroken) && said.Exists(e => e.What == DealNews.Called), "ladder: a promise broken counts as the next miss");

        // The called sum is not paid: after five days running it goes to the courts, once.
        var empty = new Purse { Money = 0 };
        int owed = broken.CallOwed;
        reputation = file.Reputation;
        bool gone = false;
        for (int n = 0; n < 4; n++)
        {
            gone |= DealRules.RunDay(file, broken, flat, 420 + n, VenturesCatalogue.Standard, empty, said, court);
        }
        Check(!gone && broken.CallMisses == 4 && claims == 0, "claim: four days missed and nothing has gone to court");
        gone = DealRules.RunDay(file, broken, flat, 424, VenturesCatalogue.Standard, empty, said, court);
        Check(gone && claims == 1 && claimed == owed && file.Deal(broken.Id) == null && file.Reputation == reputation + VenturesCatalogue.ReputationClaim,
            "claim: the fifth files a civil claim for all that is owed, the deal is the court's and the founder's name falls");
        Deal stuck = OfferRules.Accept(file, AddOffer(file, firm, "g#7", 200000, 3000), flat, 400);
        DealRules.StartCall(stuck, 100000, VenturesCatalogue.Standard);
        for (int n = 0; n < 12; n++)
        {
            gone = DealRules.RunDay(file, stuck, flat, 430 + n, VenturesCatalogue.Standard, empty, said, null);
        }
        Check(!gone && stuck.CallOwed == 100000 && file.Deal(stuck.Id) != null, "claim: with no court to file with, the sum simply stays owed");
        Check(VenturesCatalogue.CallDays[VenturesCatalogue.Realistic] == 20 && VenturesCatalogue.CallDays[VenturesCatalogue.Hardcore] == 14 && VenturesCatalogue.MissesToCall[VenturesCatalogue.Relaxed] == 0,
            "ladder: the levels' figures");
    }

    private static Offer AddOffer(VenturesFile file, Investor investor, string place, int amount, int shareBp)
    {
        return OfferRules.Add(file, investor, place, place, amount, shareBp, 1, VenturesCatalogue.Standard, OfferSource.Inbound);
    }

    private static void VenturesSave()
    {
        VenturesFile file = NewVentures(123456);
        Investor tycoon = Find(file, InvestorKind.Tycoon), angel = Find(file, InvestorKind.Angel);
        file.StartDay = 14;
        file.Reputation = 62;
        file.Pitches = 3;
        file.OfferDay = 21;
        file.SettledDay = 30;
        file.LeversSeen = 1234;
        file.ElectionSeen = 2;
        file.RateSeen = 5.25f;
        tycoon.ClosedUntil = 90;
        angel.PitchedDay = 28;
        Deal deal = OfferRules.Accept(file, AddOffer(file, tycoon, "ba:street_mainstreet#12", 80000, 2500), Steady(3000f), 20);
        deal.Business = "Tom's | Shop; and ~ more";
        deal.PaidOut = 4321;
        deal.Arrears = 12;
        deal.BoardUntil = 55;
        deal.CostTarget = 900;
        deal.CostDay = 44;
        deal.FollowOn = true;
        Offer open = AddOffer(file, angel, "ba:street_firstavenue#3", 9000, 1200);
        open.Countered = true;
        var forNew = new Offer { Id = file.NextId++, InvestorId = angel.Id, Source = OfferSource.Pitch, TypeName = "ba:businesstype_webdevelopmentagency", Budget = 50000, Amount = 20000, ShareBp = 1500, TargetPct = 10, TargetDays = 30, MadeDay = 29, LapseDay = 34 };
        file.Offers.Add(forNew);
        file.Pledges.Add(new Pledge { Id = file.NextId++, InvestorId = tycoon.Id, TypeName = "ba:businesstype_gym", Budget = 100000, Amount = 45000, ShareBp = 2200, TargetPct = 10, TargetDays = 30, MadeDay = 25, OpenBy = 39 });
        var purse = new Purse { Money = 1000000 };
        for (int day = 1; day <= 40; day++)
        {
            Market.Step(file, day, 0, null, null);
        }
        Market.Buy(file, file.Companies[2], 150, purse);
        Market.Buy(file, file.Companies[9], 40, purse);
        Market.PayDividends(file, purse);

        string line = VenturesStore.Write(file);
        int version;
        VenturesFile back = VenturesStore.Read(line, out version);
        Check(back != null && version == VenturesStore.Version && VenturesStore.Write(back) == line, "save: what is written reads back and writes the same line (" + line.Length + " characters)");
        Check(back != null && back.Investors.Count == file.Investors.Count && back.Deals.Count == 1 && back.Offers.Count == 2 && back.Pledges.Count == 1 && back.Companies.Count == 12
              && back.Holdings.Count == 2 && back.Reputation == 62 && back.RateSeen == 5.25f && back.MarketSteps == 40, "save: every part comes back");
        Deal read = back != null && back.Deals.Count == 1 ? back.Deals[0] : new Deal();
        Check(read.Place == "ba:street_mainstreet#12" && read.Business == "Tom's   Shop  and   more" && read.PaidOut == 4321 && read.BoardUntil == 55 && read.FollowOn && read.CostDay == 44,
            "save: a deal keeps its figures, and a name cannot break the line apart");
        Check(back != null && back.Companies[9].HistoryCount == 30 && back.Companies[9].History[29] == file.Companies[9].History[29] && back.Companies[9].FairCents == file.Companies[9].FairCents
              && back.Companies[8].Name == file.Companies[8].Name && back.Companies[8].Rival, "save: a company keeps thirty days of prices, what it is worth and a rival's name");
        Check(back != null && back.Investor(tycoon.Id).ClosedUntil == 90 && back.Investor(angel.Id).PitchedDay == 28 && back.Offers[1].ForNew && back.Offers[1].TypeName == forNew.TypeName,
            "save: an investor's days and an offer for a business not yet open come back");

        // A line from a later version: fields added to the ends of records and a part this version does not know.
        string later = line.Replace("v=1~", "v=2~").Replace("~i:", "|77|new~i:").Replace("~p:", "|9|later~p:").Replace("~s:", "~z:what|ever;1|2~s:") + "|5|6";
        VenturesFile fromLater = VenturesStore.Read(later, out version);
        Check(fromLater != null && version == 2 && VenturesStore.Write(fromLater) == line, "save: a line from a later version loads, the parts and fields this one does not know skipped");
        var data = new Dictionary<string, string>();
        VenturesStore.Save(data, file);
        Check(data.ContainsKey(VenturesStore.Key) && VenturesStore.Key == "reallife:economy_ventures" && VenturesStore.Load(data, out version) != null && version == 1, "save: the line is kept under its own key");
        data[VenturesStore.Key] = "nonsense";
        Check(VenturesStore.Load(data, out version) == null && VenturesStore.Load(new Dictionary<string, string>(), out version) == null && VenturesStore.Read("v=0~h:1", out version) == null
              && VenturesStore.Read("v=1~h:1|2|3", out version) == null, "save: a line that cannot be read, or holds nobody, gives no file");
        data[VenturesStore.Key] = "v=1~h:x|y~i:1|9|0;2|1|1|0|0|3|100|200|50~o:;;~d:1|2~c:1|1|0|0|5000|100|50|0|0|abc|~s:1|10|100;7|5|5";
        VenturesFile damaged = VenturesStore.Load(data, out version);
        Check(damaged != null && damaged.Investors.Count == 1 && damaged.Deals.Count == 0 && damaged.Offers.Count == 0 && damaged.Companies.Count == 1 && damaged.Companies[0].FairCents == 5000
              && damaged.Holdings.Count == 1 && damaged.NextId == 1, "save: damaged records are dropped one by one and the rest is kept");

        // The caps: more than the file may hold is not read.
        var crowded = new System.Text.StringBuilder("v=1~h:1~i:1|1|0|0|0|3|100|200|50~o:");
        for (int i = 0; i < 20; i++)
        {
            crowded.Append(i > 0 ? ";" : string.Empty).Append(i + 1).Append("|1|0|5000|1000|10|30|1|6|0|0|0|0|p").Append(i).Append("|n|");
        }
        crowded.Append("~d:");
        for (int i = 0; i < 30; i++)
        {
            crowded.Append(i > 0 ? ";" : string.Empty).Append(i + 50).Append("|1|5000|1000|1|0|0|0|300|20|0|0|60|0|0|0|0|0|0|0|0|0|0|0|p").Append(i).Append("|n");
        }
        VenturesFile capped = VenturesStore.Read(crowded.ToString(), out version);
        Check(capped != null && capped.Offers.Count == VenturesCatalogue.OffersMost && capped.Deals.Count == VenturesCatalogue.DealsMost, "save: no more offers and deals are read than the file may hold");
        Check(line.Length < 6000, "save: a full file is a few kilobytes (" + line.Length + ")");

        // While the test tools have moved the market, the save keeps the market as it stood before them.
        file.Backup = Market.Backup(file);
        int price = file.Companies[0].PriceCents, steps = file.MarketSteps;
        for (int day = 41; day <= 70; day++)
        {
            Market.Step(file, day, 0, null, null);
        }
        Check(VenturesStore.Write(file) == line, "save: thirty test days of the market are not what the save keeps");
        Market.Restore(file, file.Backup);
        file.Backup = null;
        Check(file.Companies[0].PriceCents == price && file.MarketSteps == steps && VenturesStore.Write(file) == line, "save: and the test tools put the market back as it was");
    }
}
