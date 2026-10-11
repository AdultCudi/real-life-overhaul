using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Ventures;

// Checks for investors and the market (Scripts/Economy/Ventures): the cast, an offer that comes to the player, a deal
// over a long run, the save line, the pitch and the stock market.
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
        VenturesPitchBank();
        VenturesPitchTruth();
        VenturesPitchJudgement();
        VenturesPitchDecision();
        VenturesMarket();
        VenturesNoMoneyPrinter();
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
        Pledge pledge = OfferRules.Promise(file, forNew, 40, 1);
        Check(pledge != null && pledge.OpenBy == 54 && pledge.Had == 1 && file.Pledges.Count == 1 && file.Offers.Count == 0 && file.InvestedTotal == before + second.Amount, "pledge: the money waits fourteen days for the business to open, and none has moved");
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
        Check(soldRelaxed.CallOwed == 30000 && soldRelaxed.Stage == DealStage.Called, "deal: and the same on every level: a business sold is no way out of a deal");
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
        Check(!patient.Open(100000) && patient.ClosedUntil > 100000 && file.Reputation == VenturesCatalogue.ReputationStart + 2 * VenturesCatalogue.ReputationCold,
            "ladder: an investor who loses interest before they have had their money back will not back the founder again, and the founder's name falls (" + file.Reputation + ")");
        patient.ClosedUntil = 0;
        demanding.ClosedUntil = 0;
        file.Reputation = VenturesCatalogue.ReputationStart;
        Deal repaidCold = OfferRules.Accept(file, AddOffer(file, patient, "h#8", 30000, 2000), flat, 100);
        repaidCold.PaidOut = 30000;
        repaidCold.Misses = 2;
        repaidCold.TargetDay = 101;
        DealRules.RunDay(file, repaidCold, flat, 101, VenturesCatalogue.Standard, purse, said, court);
        Check(repaidCold.Stage == DealStage.Cold && patient.ClosedUntil == 0 && file.Reputation == VenturesCatalogue.ReputationStart, "ladder: one who has had their money back loses interest with no hard feelings");
        file.Deals.Remove(repaidCold);
        Deal content = OfferRules.Accept(file, AddOffer(file, patient, "i#9", 30000, 2000), flat, 100);
        content.PaidOut = 29700;
        said.Clear();
        for (day = 101; day <= 130; day++)
        {
            DealRules.RunDay(file, content, Steady(2600f), day, VenturesCatalogue.Standard, purse, said, court);
        }
        Check(content.Stage == DealStage.Quiet && content.TargetDay == 0 && content.TargetsMet == 1 && said.Exists(e => e.What == DealNews.PaidBack),
            "ladder: an investor who has had their money back and sees a target met sets no more targets");
        int quietCash = purse.Money;
        DealRules.RunDay(file, content, Steady(2600f), 131, VenturesCatalogue.Standard, purse, said, court);
        Check(quietCash - purse.Money == 520 && content.Stage == DealStage.Quiet, "ladder: and goes on taking their share");
        file.Deals.Remove(content);
        file.Reputation = VenturesCatalogue.ReputationStart;
        file.TargetsMet = 0;

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
        file.Pledges.Add(new Pledge { Id = file.NextId++, InvestorId = tycoon.Id, TypeName = "ba:businesstype_gym", Budget = 100000, Amount = 45000, ShareBp = 2200, TargetPct = 10, TargetDays = 30, MadeDay = 25, OpenBy = 39, Had = 2 });
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
              && back.Holdings.Count == 2 && back.Reputation == 62 && back.RateSeen == 5.25f && back.MarketSteps == 40 && back.Pledges[0].Had == 2
              && back.Pledges[0].TypeName == "ba:businesstype_gym", "save: every part comes back");
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

    /// <summary>A founder with one modest business and nothing against them.</summary>
    private static PitchFacts Founder(bool running = true)
    {
        return new PitchFacts
        {
            Running = running, Sector = Sector.Retail, SalesYesterday = 2400, SalesWeek = 16000, ProfitYesterday = 900, ProfitWeek = 850, AvgProfit7 = 850f, Staff = 2, AgeDays = 40,
            ProfitableDays = 10, Cash = 12000, Owed = 20000, Payments = 400, Rating = 640, Convictions = 0, Businesses = 1, Missed = 1, Jobs = 1, OldestDays = 40, Budget = 50000
        };
    }

    private static void VenturesPitchBank()
    {
        int facts = 0, plan = 0, founder = 0;
        var keys = new HashSet<string>();
        bool shaped = true;
        for (int i = 0; i < Pitch.Bank.Length; i++)
        {
            Question q = Pitch.Bank[i];
            facts += q.Kind == QuestionKind.Fact ? 1 : 0;
            plan += q.Kind == QuestionKind.Judgement ? 1 : 0;
            founder += q.Kind == QuestionKind.Founder ? 1 : 0;
            shaped &= keys.Add(q.Key) && q.Id == i && (q.Kind == QuestionKind.Fact ? q.Fact != Fact.None && q.Answers == null : q.Answers != null && q.Answers.Length == 3);
        }
        Check(Pitch.Bank.Length >= 40 && facts == 14 && plan == 18 && founder == 9, "pitch: a bank of at least forty questions of three sorts (" + Pitch.Bank.Length + ")");
        Check(shaped, "pitch: every question has its own text, and three answers unless it asks for a figure");

        VenturesFile file = NewVentures(8086);
        var drawn = new List<int>();
        var again = new List<int>();
        var scratch = new long[3];
        bool counts = true, fit = true, same = true, mix = true;
        var everAsked = new HashSet<int>();
        for (int meeting = 1; meeting <= 120; meeting++)
        {
            foreach (Investor v in file.Investors)
            {
                PitchFacts f = Founder(meeting % 3 != 0);
                Pitch.Draw(file.Seed, meeting, v, f, drawn);
                Pitch.Draw(file.Seed, meeting, v, f, again);
                counts &= drawn.Count == VenturesCatalogue.Questions[(int)v.Kind] && drawn.Count >= 6 && drawn.Count <= 8 && new HashSet<int>(drawn).Count == drawn.Count;
                same &= string.Join(",", drawn) == string.Join(",", again);
                int[] kinds = new int[3];
                foreach (int index in drawn)
                {
                    Question q = Pitch.Bank[index];
                    kinds[(int)q.Kind]++;
                    fit &= Pitch.Askable(q, f, scratch);
                    everAsked.Add(index);
                }
                int[] want = VenturesCatalogue.QuestionMix[(int)v.Kind];
                mix &= kinds[0] == want[0] && kinds[2] == want[2] && kinds[1] == drawn.Count - want[0] - want[2];
            }
        }
        Check(counts, "pitch: six to eight questions, by the kind of investor, none twice");
        Check(mix, "pitch: a fund asks for more figures and an angel more about the founder");
        Check(fit, "pitch: every question drawn fits the business");
        Check(same, "pitch: the same meeting draws the same questions");
        Check(everAsked.Count >= Pitch.Bank.Length - 1, "pitch: over many meetings the whole bank is asked (" + everAsked.Count + " of " + Pitch.Bank.Length + "; a clean record is never asked about as a figure)");
        PitchFacts unopened = Founder(false);
        bool noRunning = true;
        for (int meeting = 1; meeting <= 60; meeting++)
        {
            Pitch.Draw(file.Seed, meeting, file.Investors[meeting % file.Investors.Count], unopened, drawn);
            foreach (int index in drawn)
            {
                noRunning &= (Pitch.Bank[index].Fits & Question.New) != 0;
            }
        }
        Check(noRunning, "pitch: nobody asks what a business not yet open took yesterday");
        // With no debts, no record and no credit file there is less to ask for as a figure: the plan fills the meeting.
        PitchFacts bare = Founder(false);
        bare.Owed = 0;
        bare.Payments = 0;
        bare.Missed = 0;
        bare.Rating = 0;
        Investor fund = Find(file, InvestorKind.Firm);
        Pitch.Draw(file.Seed, 5, fund, bare, drawn);
        Check(drawn.Count == 8, "pitch: where few figures can be asked for, questions about the plan make up the number");

        // The words: every question's line in en.json has the parts the meeting reads from it.
        string locale = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales\en.json"));
        Dictionary<string, string> words = System.IO.File.Exists(locale)
            ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(System.IO.File.ReadAllText(locale).TrimStart('﻿')) : null;
        Check(words != null, "pitch: en.json is where the checks look for it (" + locale + ")");
        bool parts = true, named = true;
        var topics = new HashSet<string>();
        foreach (Question q in Pitch.Bank)
        {
            string line;
            if (words == null || !words.TryGetValue(q.Key, out line))
            {
                parts = false;
                continue;
            }
            string[] cut = line.Split('|');
            parts &= cut.Length == (q.Kind == QuestionKind.Fact ? 2 : 5) && Array.TrueForAll(cut, part => part.Trim().Length > 0 && part == part.Trim());
            parts &= cut[0].EndsWith("?") && topics.Add(cut[cut.Length - 1]);
            named &= line.Contains("{business}") == (q.Kind == QuestionKind.Fact && q.Fits == Question.Running) || (q.Kind != QuestionKind.Fact && !line.Contains("{business}"));
        }
        Check(parts, "pitch: every question is a question, its answers and what it is about, and no two are about the same thing");
        Check(named, "pitch: only a question that fits a running business alone names the business");
    }

    private static void VenturesPitchTruth()
    {
        var values = new long[3];
        PitchFacts f = Founder();
        Check(Pitch.Options(Fact.SalesYesterday, f, values) == 3 && values[0] == 2400 && values[1] == 3100 && values[2] == 4300, "pitch: takings are offered as the true figure and two that sound better (" + values[1] + ", " + values[2] + ")");
        Check(Pitch.Options(Fact.Owed, f, values) == 3 && values[0] == 20000 && values[1] == 10000 && values[2] == 0, "pitch: a debt is offered as it is, as half of it and as none");
        Check(Pitch.Options(Fact.Missed, f, values) == 2 && values[0] == 1 && values[1] == 0, "pitch: one missed payment can only be told or denied");
        Check(Pitch.Options(Fact.Convictions, f, values) == 1, "pitch: a clean record is not asked about as a figure to improve");
        Check(Pitch.Options(Fact.Rating, f, values) == 3 && values[0] == 640 && values[1] == 680 && values[2] == 740, "pitch: a credit rating and two better ones");
        Check(Pitch.Options(Fact.ProfitableDays, f, values) == 3 && values[0] == 10 && values[1] == 13 && values[2] == 14, "pitch: profitable days never over fourteen");
        f.ProfitYesterday = -361;
        Check(Pitch.Options(Fact.ProfitYesterday, f, values) == 3 && values[0] == -360 && values[1] == 400 && values[2] == 1200, "pitch: a loss is offered as the loss it was, and as two profits");
        f.Rating = 0;
        Check(Pitch.Options(Fact.Rating, f, values) == 1, "pitch: with no credit file the rating is not asked for");
        bool neverFatal = true, lieWorthMore = true, truthFirst = true;
        for (int fact = 1; fact <= (int)Fact.Payments; fact++)
        {
            long worst = Pitch.LowerBetter((Fact)fact) ? 100000000 : -100000000;
            neverFatal &= Pitch.FactPoints((Fact)fact, worst) == VenturesCatalogue.TruthBase;
            neverFatal &= Pitch.FactPoints((Fact)fact, Pitch.LowerBetter((Fact)fact) ? 0 : 100000000) == VenturesCatalogue.PointsMost;
            PitchFacts weak = Founder();
            int count = Pitch.Options((Fact)fact, weak, values);
            truthFirst &= values[0] == (Pitch.UnitOf((Fact)fact) == FactUnit.Money ? VenturesCatalogue.Round2(Pitch.Truth((Fact)fact, weak)) : Pitch.Truth((Fact)fact, weak));
            for (int o = 1; o < count; o++)
            {
                lieWorthMore &= Pitch.FactPoints((Fact)fact, values[o]) >= Pitch.FactPoints((Fact)fact, values[o - 1]);
                lieWorthMore &= Pitch.LowerBetter((Fact)fact) ? values[o] < values[o - 1] : values[o] > values[o - 1];
            }
        }
        Check(neverFatal, "pitch: the weakest true figure still scores four of ten, and the strongest ten");
        Check(truthFirst && lieWorthMore, "pitch: the first figure is the true one and each further one sounds better and would score at least as much");

        // A meeting told nothing but the truth is never ended and closes nobody, however weak the figures.
        VenturesFile file = NewVentures(1999);
        PitchFacts poor = Founder();
        poor.SalesYesterday = 150;
        poor.SalesWeek = 900;
        poor.ProfitYesterday = -400;
        poor.ProfitWeek = -120;
        poor.AvgProfit7 = -120f;
        poor.ProfitableDays = 3;
        poor.Owed = 400000;
        poor.Missed = 9;
        poor.Convictions = 3;
        poor.Rating = 410;
        bool honestEnds = true, honestFloor = true;
        int day = 100;
        foreach (Investor v in file.Investors)
        {
            int name = file.Reputation;
            PitchState state = Pitch.Begin(file, v, poor, "a#1", "A", null, day);
            while (!Pitch.Over(state))
            {
                Question q = Pitch.Bank[state.Questions[state.Index]];
                AnswerResult r = Pitch.Answer(file, state, v, 0, VenturesCatalogue.Hardcore, day);
                honestFloor &= q.Kind != QuestionKind.Fact || (r.Points >= VenturesCatalogue.TruthBase && !r.Untrue && r.Chance == 0);
            }
            honestEnds &= !state.Caught && state.Points.Count == state.Questions.Count && v.ClosedUntil == 0 && file.Reputation == name && v.PitchedDay == day;
            day += 20;
        }
        Check(honestEnds && honestFloor, "pitch: the truth about weak figures costs points and never ends the meeting, closes nobody and harms no name");

        // An untrue figure: the chance follows the investor's diligence and how far from the truth it is; caught, the meeting is over.
        Investor angel = Find(file, InvestorKind.Angel), firm = Find(file, InvestorKind.Firm);
        Check(Pitch.CatchChance(firm, VenturesCatalogue.Standard, 70, false) == firm.Diligence * 45 / 100 && Pitch.CatchChance(firm, VenturesCatalogue.Standard, 70, true) == firm.Diligence * 90 / 100,
            "pitch: a figure a little better is caught at 45% of the investor's diligence, one much better at 90% (" + firm.Diligence + ")");
        Check(Pitch.CatchChance(angel, VenturesCatalogue.Standard, 70, true) < Pitch.CatchChance(firm, VenturesCatalogue.Standard, 70, true), "pitch: a fund checks harder than an angel");
        Check(Pitch.CatchChance(angel, VenturesCatalogue.Hardcore, 70, false) > Pitch.CatchChance(angel, VenturesCatalogue.Standard, 70, false)
              && Pitch.CatchChance(angel, VenturesCatalogue.Relaxed, 70, false) < Pitch.CatchChance(angel, VenturesCatalogue.Standard, 70, false), "pitch: the level moves how hard everybody checks");
        Check(Pitch.CatchChance(angel, VenturesCatalogue.Standard, 45, false) > Pitch.CatchChance(angel, VenturesCatalogue.Standard, 70, false), "pitch: a founder caught before is checked harder by everybody");
        int caught = 0, lied = 0, meetings = 0;
        bool endsIt = true, closes = true, costsName = true, noPoints = true;
        for (int seed = 1; seed <= 400; seed++)
        {
            VenturesFile g = NewVentures(seed);
            Investor v = g.Investors[seed % g.Investors.Count];
            int name = g.Reputation;
            PitchState state = Pitch.Begin(g, v, Founder(), "a#1", "A", null, 50);
            meetings++;
            bool wasCaught = false;
            while (!Pitch.Over(state))
            {
                Question q = Pitch.Bank[state.Questions[state.Index]];
                AnswerResult r = Pitch.Answer(g, state, v, q.Kind == QuestionKind.Fact ? 2 : 0, VenturesCatalogue.Standard, 50);
                lied += r.Untrue ? 1 : 0;
                if (r.Caught)
                {
                    caught++;
                    wasCaught = true;
                    endsIt &= Pitch.Over(state) && state.Caught;
                    noPoints &= r.Points == 0 && r.Roll < r.Chance;
                    AnswerResult after = Pitch.Answer(g, state, v, 0, VenturesCatalogue.Standard, 50);
                    endsIt &= after.Points == 0 && state.Points.Count == state.Index;
                }
                else
                {
                    noPoints &= !r.Untrue || r.Roll >= r.Chance;
                }
            }
            closes &= wasCaught ? v.ClosedUntil == 50 + VenturesCatalogue.CaughtCloseDays && !v.Open(109) && v.Open(110) : v.ClosedUntil == 0;
            costsName &= g.Reputation == name + (wasCaught ? VenturesCatalogue.ReputationCaught : 0);
        }
        Check(caught > 60 && caught < lied, "pitch: some untrue figures are caught and some are not (" + caught + " of " + lied + " in " + meetings + " meetings)");
        Check(endsIt && noPoints, "pitch: a caught figure always ends the meeting there, and scores nothing");
        Check(closes && costsName, "pitch: the investor who caught it sees nobody for sixty days and the founder's name with all of them falls by twenty-five");
        Investor open = file.Investors[0];
        open.PitchedDay = 0;
        open.ClosedUntil = 0;
        Check(open.Open(5), "pitch: an investor never approached can be approached");
        open.PitchedDay = 5;
        Check(!open.Open(14) && open.Open(15) && open.OpenDay == 15, "pitch: one approach to an investor in ten days");
    }

    private static void VenturesPitchJudgement()
    {
        var angel = new Investor { Id = 90, Kind = InvestorKind.Angel, Temper = Temperament.Patient };
        var tycoon = new Investor { Id = 91, Kind = InvestorKind.Tycoon, Temper = Temperament.Even };
        var hardTycoon = new Investor { Id = 92, Kind = InvestorKind.Tycoon, Temper = Temperament.Demanding };
        var fund = new Investor { Id = 93, Kind = InvestorKind.Firm, Temper = Temperament.Demanding };
        Investor[] who = { angel, tycoon, hardTycoon, fund };
        PitchFacts shop = Founder(), office = Founder();
        office.Sector = Sector.Office;
        var lists = new string[who.Length];
        int[] firstBest = new int[3];
        bool inRange = true, recklessNeverBest = true;
        int planQuestions = 0, sectorMoves = 0;
        for (int i = 0; i < Pitch.Bank.Length; i++)
        {
            Question q = Pitch.Bank[i];
            if (q.Kind != QuestionKind.Judgement)
            {
                continue;
            }
            planQuestions++;
            for (int w = 0; w < who.Length; w++)
            {
                int best = Pitch.Best(q, who[w], shop);
                lists[w] += best;
                firstBest[best]++;
                recklessNeverBest &= q.Answers[best].Style != Style.Reckless;
                for (int a = 0; a < 3; a++)
                {
                    int points = Pitch.Points(q, a, who[w], shop);
                    inRange &= points >= 0 && points <= VenturesCatalogue.PointsMost;
                }
            }
            sectorMoves += Pitch.Best(q, tycoon, shop) != Pitch.Best(q, tycoon, office) ? 1 : 0;
        }
        Check(inRange, "pitch: an answer about the plan is worth nothing to ten");
        Check(lists[0] != lists[3] && lists[0] != lists[2] && lists[1] != lists[3] && lists[0] != lists[1], "pitch: the best answers are not the same list for every investor");
        int differ = 0;
        for (int i = 0; i < planQuestions; i++)
        {
            differ += lists[0][i] != lists[3][i] ? 1 : 0;
        }
        Check(differ >= planQuestions / 2, "pitch: a patient angel and a fund want a different answer to at least half the questions about the plan (" + differ + " of " + planQuestions + ")");
        Check(firstBest[0] > 0 && firstBest[1] > 0 && firstBest[2] > 0, "pitch: no place in the list of answers is always the best one");
        Check(recklessNeverBest, "pitch: a reckless answer is nobody's best");
        Check(sectorMoves >= 2, "pitch: for some questions the best answer depends on the business's sector (" + sectorMoves + ")");
        // The answers that are best for one investor score clearly worse with another.
        int ownScore = 0, othersScore = 0;
        for (int i = 0; i < Pitch.Bank.Length; i++)
        {
            Question q = Pitch.Bank[i];
            if (q.Kind == QuestionKind.Judgement)
            {
                int best = Pitch.Best(q, angel, shop);
                ownScore += Pitch.Points(q, best, angel, shop);
                othersScore += Pitch.Points(q, best, fund, shop);
            }
        }
        Check(othersScore * 100 < ownScore * 75, "pitch: the angel's best answers are worth less than three quarters as much to a fund (" + othersScore + " against " + ownScore + ")");

        // The founder: an answer the record does not bear out is worth one point.
        PitchFacts f = Founder();
        Question before = null, missed = null, record = null, backers = null;
        foreach (Question q in Pitch.Bank)
        {
            before = q.Key == "reallife_ven_q_p02" ? q : before;
            missed = q.Key == "reallife_ven_q_p04" ? q : missed;
            record = q.Key == "reallife_ven_q_p06" ? q : record;
            backers = q.Key == "reallife_ven_q_p07" ? q : backers;
        }
        Check(Pitch.Points(before, 0, angel, f) > Pitch.Unsupported && Pitch.Points(before, 1, angel, f) == Pitch.Unsupported && Pitch.Points(before, 2, angel, f) == Pitch.Unsupported,
            "founder: with one business forty days old, only 'my first business' is believed");
        f.Businesses = 3;
        f.OldestDays = 90;
        Check(Pitch.Points(before, 0, fund, f) == Pitch.Unsupported && Pitch.Points(before, 1, fund, f) >= 9 && Pitch.Points(before, 2, fund, f) > Pitch.Unsupported,
            "founder: with three businesses the same answers score the other way round");
        Check(Pitch.Points(missed, 0, tycoon, f) == Pitch.Unsupported && Pitch.Points(missed, 1, tycoon, f) > 4, "founder: 'never missed a payment' is not believed of a file that shows one; owning up is");
        f.Missed = 0;
        Check(Pitch.Points(missed, 0, tycoon, f) >= 9 && Pitch.Points(missed, 1, tycoon, f) == Pitch.Unsupported, "founder: and with a clean file it is the other way round");
        f.Bankruptcies = 1;
        Check(Pitch.Points(missed, 0, tycoon, f) == Pitch.Unsupported, "founder: a bankruptcy on the credit file counts as having failed to pay");
        Check(Pitch.Points(record, 0, fund, f) >= 8 && Pitch.Points(record, 1, fund, f) == Pitch.Unsupported, "founder: a clean record can be claimed, a conviction that is not there cannot be owned up to");
        f.Convictions = 1;
        Check(Pitch.Points(record, 0, fund, f) == Pitch.Unsupported && Pitch.Points(record, 1, fund, f) > Pitch.Unsupported, "founder: with a conviction it is the other way round");
        Check(Pitch.Points(backers, 0, angel, f) > Pitch.Unsupported && Pitch.Points(backers, 1, angel, f) == Pitch.Unsupported, "founder: nobody can claim backers they do not have");
        f.Backers = 2;
        Check(Pitch.Points(backers, 1, angel, f) > Pitch.Unsupported && Pitch.Points(backers, 0, angel, f) == Pitch.Unsupported, "founder: nor deny the ones they have");
        PitchFacts inOrder = Founder();
        inOrder.Missed = 0;
        Check(Pitch.Holds(Claim.Worked, Founder()) && !Pitch.Holds(Claim.GoodCredit, Founder()) && Pitch.Holds(Claim.BankInOrder, inOrder) && !Pitch.Holds(Claim.BankInOrder, Founder())
              && !Pitch.Holds(Claim.SteadyProfit, Founder()),
            "founder: a job held, a credit rating, a debt in order and a steady profit are read from the record");
    }

    private static void VenturesPitchDecision()
    {
        VenturesFile file = NewVentures(31415);
        Investor angel = Find(file, InvestorKind.Angel), tycoon = Find(file, InvestorKind.Tycoon), fund = Find(file, InvestorKind.Firm);
        Check(Pitch.Needed(angel, VenturesCatalogue.Standard) == 45 && Pitch.Needed(tycoon, VenturesCatalogue.Standard) == 55 && Pitch.Needed(fund, VenturesCatalogue.Hardcore) == 68
              && Pitch.Needed(angel, VenturesCatalogue.Relaxed) == 40, "decision: each kind of investor wants its own score, moved by the level");
        var points = new List<int> { 5, 5, 5, 5, 5, 5 };
        var plain = new Investor { Id = 81, Kind = InvestorKind.Tycoon, Temper = Temperament.Even, ChequeLeast = 25000, ChequeMost = 750000, Diligence = 60 };
        PitchFacts f = Founder();
        Check(Pitch.Score(points, plain, f, 70) == 50 && Pitch.Score(points, plain, f, 45) == 45 && Pitch.Score(points, plain, f, 100) == 56, "decision: the score is the points of the most, moved by the founder's name");
        var liking = new Investor { Id = 80, Kind = InvestorKind.Tycoon, Temper = Temperament.Even, Likes = 1 << (int)Sector.Retail, ChequeLeast = 25000, ChequeMost = 750000, Diligence = 60 };
        Check(Pitch.Score(points, liking, f, 70) == 55, "decision: five more for a business in a sector the investor likes");

        // Scores map to sums inside the investor's range and inside what the business's figures support.
        bool inside = true, supported = true, shares = true, monotone = true, refusedBelow = true;
        int invested = 0;
        for (int profit = 500; profit <= 60000; profit += 1700)
        {
            long lastValue = -1;
            int lastShare = int.MaxValue;
            for (int each = 0; each <= 10; each++)
            {
                var p = new List<int> { each, each, each, each, each, each, each };
                PitchFacts b = Founder();
                b.AvgProfit7 = profit;
                PitchDecision d = Pitch.Decide(file, plain, b, "x#1", p, VenturesCatalogue.Standard, 10);
                if (d.Verdict != Verdict.Invest)
                {
                    refusedBelow &= d.Amount == 0 && d.ShareBp == 0 && (d.Score >= d.Needed || d.Verdict == Verdict.LowScore);
                    continue;
                }
                refusedBelow &= d.Score >= d.Needed;
                invested++;
                inside &= d.Amount >= plain.ChequeLeast && d.Amount <= plain.ChequeMost;
                supported &= d.Amount <= (long)profit * d.ValueDays * d.ShareBp / 10000 + 1 && d.ValueDays >= VenturesCatalogue.ValueDaysLeast && d.ValueDays <= VenturesCatalogue.ValueDaysMost;
                shares &= d.ShareBp > 0 && d.ShareBp <= VenturesCatalogue.ShareMostBp[1];
                monotone &= d.ValueDays >= lastValue && d.ShareBp <= lastShare;
                lastValue = d.ValueDays;
                lastShare = d.ShareBp;
            }
        }
        Check(invested > 60, "decision: a good enough meeting about a profitable enough business brings money (" + invested + " of 396)");
        Check(refusedBelow, "decision: under the score the investor wants there is no money");
        Check(inside, "decision: the sum is never under the investor's smallest or over their largest");
        Check(supported, "decision: the sum is never more than the investor's share of forty to ninety days of the true recent profit");
        Check(shares, "decision: the share is inside the kind's range");
        Check(monotone, "decision: a better score values the business higher and takes a smaller share");

        // One meeting worked through by hand.
        PitchFacts w = Founder();
        w.AvgProfit7 = 4000f;
        var meeting = new List<int> { 8, 10, 7, 8, 6, 9, 8 };
        PitchDecision made = Pitch.Decide(file, plain, w, "y#2", meeting, VenturesCatalogue.Standard, 10);
        Check(made.Verdict == Verdict.Invest && made.Score == 80 && made.Needed == 55 && made.ValueDays == 68 && made.ShareBp == 2389 && made.Amount == 64900 && made.Best == 1 && made.Worst == 4,
            "decision: 56 of 70 points is 80; with 55 needed the business is valued at 68 days of 4,000 and a share of 23.89% brings 64,900 ("
            + made.Score + ", " + made.ValueDays + ", " + made.ShareBp + ", " + made.Amount + ")");

        // Figures that do not carry the smallest sum, no profit, and an angel backing a young business on its founder.
        var top = new List<int> { 10, 10, 10, 10, 10, 10 };
        PitchFacts small = Founder();
        small.AvgProfit7 = 300f;
        Check(Pitch.Decide(file, plain, small, "z#3", top, VenturesCatalogue.Standard, 10).Verdict == Verdict.TooSmall, "decision: a profit too small for the investor's smallest sum is said so");
        small.AvgProfit7 = -50f;
        Check(Pitch.Decide(file, plain, small, "z#3", top, VenturesCatalogue.Standard, 10).Verdict == Verdict.NoProfit, "decision: no profit, no valuation");
        small.AgeDays = 12;
        PitchDecision young = Pitch.Decide(file, angel, small, "z#3", top, VenturesCatalogue.Standard, 10);
        Check(young.Verdict == Verdict.Invest && young.OnFounder && young.Amount == angel.ChequeLeast && young.ShareBp == VenturesCatalogue.ShareLeastBp[0],
            "decision: an angel backs a business under thirty days old on its founder: their smallest sum");
        small.AgeDays = 30;
        Check(Pitch.Decide(file, angel, small, "z#3", top, VenturesCatalogue.Standard, 10).Verdict == Verdict.NoProfit, "decision: at thirty days it has to show a profit like any other");

        // A business not yet open: a share of the budget from an angel or a fund; a tycoon backs none.
        PitchFacts unopened = Founder(false);
        unopened.Budget = 100000;
        var seed = new Investor { Id = 83, Kind = InvestorKind.Angel, Temper = Temperament.Even, ChequeLeast = 5000, ChequeMost = 100000, Diligence = 30 };
        PitchDecision least = Pitch.Decide(file, seed, unopened, null, new List<int> { 5, 4, 5, 4, 5, 4 }, VenturesCatalogue.Standard, 10);
        PitchDecision most = Pitch.Decide(file, seed, unopened, null, top, VenturesCatalogue.Standard, 10);
        Check(least.Verdict == Verdict.Invest && least.Score == 45 && least.Amount == 30000 && least.ShareBp == VenturesCatalogue.ShareMostBp[0] && most.Amount == 70000 && most.ShareBp == VenturesCatalogue.ShareLeastBp[0],
            "decision: a business not yet open gets three to seven tenths of its budget (" + least.Amount + " to " + most.Amount + ")");
        PitchDecision notOpen = Pitch.Decide(file, plain, unopened, null, top, VenturesCatalogue.Standard, 10);
        Check(notOpen.Verdict == Verdict.NotOpen && notOpen.Amount == 0 && Pitch.Decide(file, tycoon, unopened, null, top, VenturesCatalogue.Hardcore, 10).Verdict == Verdict.NotOpen,
            "decision: a tycoon puts no money into a business that is not open yet");
        unopened.Budget = 10000;
        Check(Pitch.Decide(file, fund, unopened, null, top, VenturesCatalogue.Standard, 10).Verdict == Verdict.TooSmall, "decision: a budget too small for a fund is said so");
        unopened.Budget = 500000;
        Check(Pitch.Decide(file, angel, unopened, null, top, VenturesCatalogue.Standard, 10).Amount == angel.ChequeMost, "decision: and nobody puts in more than their largest sum");

        // No room: the business already has all the investors it may have.
        Deal held = OfferRules.Accept(file, AddOffer(file, tycoon, "full#1", 50000, 6000), Steady(5000f), 5);
        PitchFacts full = Founder();
        full.AvgProfit7 = 5000f;
        Check(held != null && Pitch.Decide(file, fund, full, "full#1", top, VenturesCatalogue.Standard, 10).Verdict == Verdict.NoRoom, "decision: a business with six tenths of its profit promised has no room for another investor");
    }

    // ---------------------------------------------------------------- the market

    private const int MarketYear = 60;          // the game's default days in a year (DifficultySetting.daysPerYear)
    private const int MarketYears = 10;
    private const int MarketSeeds = 80;
    private const int MarketCash = 1000000;

    /// <summary>A day's news made up for the checks: about one day in three something happens, now and then two things, a policy or the bank rate.</summary>
    private static void MadeUpNews(int seed, int day, List<MarketNews> into, VenturesFile file)
    {
        into.Clear();
        var random = new VenturesRandom((int)VenturesRandom.Hash(seed, day, 31));
        if (random.Below(100) < 35)
        {
            into.Add(new MarketNews { Cause = (MarketCause)random.Range(1, (int)MarketCause.Accident) });
        }
        if (random.Below(100) < 10)
        {
            into.Add(new MarketNews { Cause = (MarketCause)random.Range(1, (int)MarketCause.Accident) });
        }
        if (random.Below(100) < 6)
        {
            into.Add(new MarketNews { Cause = MarketCause.Policy, Lever = random.Below(13), Scale = random.Below(2) == 0 ? 100 : -100 });
        }
        if (random.Below(100) < 4)
        {
            into.Add(new MarketNews { Cause = MarketCause.BankRate, Scale = random.Below(2) == 0 ? 50 : -50 });
        }
        if (random.Below(100) < 5)
        {
            into.Add(new MarketNews { Cause = MarketCause.RivalMove, Company = file.Companies[8 + random.Below(4)].RivalName });
        }
        if (random.Below(100) < 2)
        {
            into.Add(new MarketNews { Cause = MarketCause.Election });
        }
    }

    /// <summary>What a rule of thumb is worth after ten years: the cash and the shares sold at the last price, commission paid.</summary>
    private static double Trade(int seed, int rule, out int trades)
    {
        VenturesFile file = NewVentures(seed);
        var purse = new Purse { Money = MarketCash };
        var news = new List<MarketNews>();
        int slice = MarketCash / file.Companies.Count;
        trades = 0;
        for (int day = 1; day <= MarketYear * MarketYears; day++)
        {
            MadeUpNews(seed, day, news, file);
            foreach (Company c in file.Companies)
            {
                Holding held = file.Holding(c.Id);
                bool has = held != null && held.Shares > 0;
                bool buy = false, sell = false;
                int cause;
                switch (rule)
                {
                    case 0:   // buy everything on the first day and hold it
                        buy = day == 1;
                        break;
                    case 1:   // buy after a fall, sell after a rise
                        buy = c.LastBp <= -150;
                        sell = c.LastBp >= 150;
                        break;
                    case 2:   // buy after a rise, sell after a fall
                        buy = c.LastBp >= 150;
                        sell = c.LastBp <= -150;
                        break;
                    case 3:   // buy well under the thirty-day average, sell well over it
                        {
                            long sum = 0;
                            for (int h = 0; h < c.HistoryCount; h++)
                            {
                                sum += c.History[h];
                            }
                            double mean = (double)sum / c.HistoryCount;
                            buy = c.HistoryCount >= 20 && c.PriceCents < mean * 0.94;
                            sell = c.PriceCents > mean * 1.04;
                        }
                        break;
                    case 4:   // buy the day before a dividend, sell the day after
                        buy = Market.DaysToDividend(file) == 1;
                        sell = Market.DaysToDividend(file) == VenturesCatalogue.DividendEveryDays;
                        break;
                    case 5:   // read the paper before the market does: buy what today's news lifts, sell it tomorrow
                        buy = Market.NewsFor(c, news, out cause) >= 40;
                        sell = !buy;
                        break;
                    case 6:   // sell what today's news will knock, buy it back tomorrow
                        sell = Market.NewsFor(c, news, out cause) <= -40;
                        buy = !sell;
                        break;
                    case 7:   // hold everything, but sell what has run a tenth over its thirty-day average and buy it back under that average
                        {
                            long sum = 0;
                            for (int h = 0; h < c.HistoryCount; h++)
                            {
                                sum += c.History[h];
                            }
                            double mean = (double)sum / c.HistoryCount;
                            sell = c.PriceCents > mean * 1.10;
                            buy = day == 1 || c.PriceCents < mean;
                        }
                        break;
                }
                if (sell && has)
                {
                    Market.Sell(file, c, held.Shares, purse);
                    trades++;
                }
                else if (buy && !has)
                {
                    int shares = Math.Min(Market.MostBuyable(file, c, purse.Money), slice * 100 / c.PriceCents);
                    if (shares > 0 && Market.Buy(file, c, shares, purse) == TradeAnswer.Done)
                    {
                        trades++;
                    }
                }
            }
            Market.Step(file, day, 0, news, null);
            if (Market.DividendDue(file))
            {
                Market.PayDividends(file, purse);
            }
        }
        double worth = purse.Money;
        foreach (Holding h in file.Holdings)
        {
            int value = Market.Value(file.Company(h.CompanyId), h.Shares);
            worth += value - Market.Commission(value);
        }
        return worth;
    }

    private static void VenturesMarket()
    {
        // The same seed and the same news give the same market, and each day depends only on the seed and its number.
        VenturesFile a = NewVentures(555), b = NewVentures(555), other = NewVentures(556);
        var news = new List<MarketNews>();
        bool floor = true, history = true;
        for (int day = 1; day <= 200; day++)
        {
            MadeUpNews(555, day, news, a);
            Market.Step(a, day, 0, news, null);
            Market.Step(b, day, 0, news, null);
            Market.Step(other, day, 0, news, null);
            foreach (Company c in a.Companies)
            {
                floor &= c.PriceCents >= VenturesCatalogue.PriceFloorCents;
                history &= c.HistoryCount == Math.Min(VenturesCatalogue.HistoryDays, day + 1) && c.History[c.HistoryCount - 1] == c.PriceCents;
            }
        }
        Check(VenturesStore.Write(a) == VenturesStore.Write(b), "market: the same seed and news give the same prices after two hundred days");
        Check(VenturesStore.Write(a) != VenturesStore.Write(other), "market: another seed gives another market");
        Check(floor && history && a.MarketSteps == 200 && a.MarketDay == 200, "market: no price under one unit, and thirty days of closing prices are kept");
        var text = new System.Text.StringBuilder();
        Market.Step(a, 201, 0, news, text);
        Check(text.Length > 200 && text.ToString().Contains(a.Companies[0].Name), "market: a day's moves can be written out with their causes");

        // The news: each piece moves the sectors it is about, all of it together is capped, and the cap is under the cost of selling and buying back.
        Company transport = a.Companies[0], utilities = a.Companies[5], bank = a.Companies[6], rival = a.Companies[8];
        int why;
        var storm = new List<MarketNews> { new MarketNews { Cause = MarketCause.Storm } };
        Check(Market.NewsFor(transport, storm, out why) == -60 && why == (int)MarketCause.Storm && Market.NewsFor(utilities, storm, out why) == 40 && Market.NewsFor(bank, storm, out why) == 0 && why == 0,
            "market: a storm knocks transport, lifts the utilities and leaves the banks alone");
        var policy = new List<MarketNews> { new MarketNews { Cause = MarketCause.Policy, Lever = 7, Scale = 100 } };
        var relaxed = new List<MarketNews> { new MarketNews { Cause = MarketCause.Policy, Lever = 7, Scale = -100 } };
        Check(Market.NewsFor(transport, policy, out why) == 40 && Market.NewsFor(transport, relaxed, out why) == -40, "market: a council policy moves its sectors one way when it is raised and the other when it is cut");
        var rate = new List<MarketNews> { new MarketNews { Cause = MarketCause.BankRate, Scale = 50 } };
        Check(Market.NewsFor(bank, rate, out why) == 20 && Market.NewsFor(a.Companies[1], rate, out why) == -15, "market: half a point on the bank rate lifts the banks and weighs on property");
        var move = new List<MarketNews> { new MarketNews { Cause = MarketCause.RivalMove, Company = rival.RivalName } };
        Check(rival.Rival && Market.NewsFor(rival, move, out why) == Market.RivalMoveBp && Market.NewsFor(a.Companies[9], move, out why) <= 0 && Market.NewsFor(a.Companies[2], move, out why) == -10,
            "market: a rival's move lifts that rival's own shares and weighs on the other shops");
        var everything = new List<MarketNews>();
        for (int cause = 1; cause < (int)MarketCause.Count; cause++)
        {
            for (int n = 0; n < 4; n++)
            {
                everything.Add(new MarketNews { Cause = (MarketCause)cause, Lever = n * 3, Scale = 100 });
            }
        }
        bool capped = true;
        foreach (Company c in a.Companies)
        {
            capped &= Math.Abs(Market.NewsFor(c, everything, out why)) <= VenturesCatalogue.NewsCapBp;
        }
        Check(capped && VenturesCatalogue.NewsCapBp < 2 * VenturesCatalogue.CommissionBp, "market: all the news of a day moves a company by 1.5% at most, less than the 2% it costs to sell and buy back");
        Check(Market.CauseKeys.Length == (int)MarketCause.Count && VenturesCatalogue.MarketSectorKeys.Length == (int)MarketSector.Count && VenturesCatalogue.MovesKeys.Length == (int)MarketSector.Count,
            "market: a name for every cause and every sector");

        // The climate: a boom lifts every price a little each day and a slowdown weighs on it.
        VenturesFile boom = NewVentures(909), slow = NewVentures(909);
        for (int day = 1; day <= 300; day++)
        {
            Market.Step(boom, day, 1, null, null);
            Market.Step(slow, day, -1, null, null);
        }
        long boomSum = 0, slowSum = 0;
        for (int i = 0; i < boom.Companies.Count; i++)
        {
            boomSum += boom.Companies[i].PriceCents;
            slowSum += slow.Companies[i].PriceCents;
        }
        Check(boomSum > slowSum, "market: prices are higher after a long boom than after a long slowdown");

        // Trading: the commission, the limit, and exact money.
        Check(Market.Commission(100000) == 1000 && Market.Commission(120) == 5 && Market.Commission(501) == 6 && Market.Commission(0) == 0, "market: commission is 1%, rounded up, five at least");
        VenturesFile file = NewVentures(321);
        Company company = file.Companies[3];
        company.PriceCents = 4321;
        var purse = new Purse { Money = 100000 };
        Check(Market.Cost(company, 100) == 4321 && Market.Cost(company, 7) == 303 && Market.Value(company, 7) == 302, "market: a purchase is rounded up to whole money and a sale down");
        Check(Market.Buy(file, company, 100, purse) == TradeAnswer.Done && purse.Money == 100000 - 4321 - 44 && file.Holding(company.Id).Shares == 100 && file.Holding(company.Id).Cost == 4365,
            "market: buying takes the price and the commission, and the holding remembers what it cost");
        Check(Market.Buy(file, company, 5000, purse) == TradeAnswer.OverLimit && Market.Buy(file, company, 4000, purse) == TradeAnswer.NoCash && purse.Money == 95635,
            "market: no more than five thousand shares of one company, and nothing bought without the cash for it and its commission");
        int most = Market.MostBuyable(file, company, purse.Money);
        Check(most > 0 && Market.Cost(company, most) + Market.Commission(Market.Cost(company, most)) <= purse.Money
              && Market.Cost(company, most + 1) + Market.Commission(Market.Cost(company, most + 1)) > purse.Money, "market: the most that can be bought is what the cash carries (" + most + ")");
        Check(Market.Sell(file, company, 101, purse) == TradeAnswer.NoShares && Market.Sell(file, file.Companies[4], 1, purse) == TradeAnswer.NoShares, "market: nobody sells what they do not hold");
        company.PriceCents = 5000;
        Check(Market.Sell(file, company, 40, purse) == TradeAnswer.Done && purse.Money == 95635 + 2000 - 20 && file.Holding(company.Id).Shares == 60 && file.Holding(company.Id).Cost == 2619,
            "market: selling pays the price less the commission, and the cost of what is left is its part of the whole");
        int cost;
        Check(Market.Worth(file, out cost) == 3000 && cost == 2619, "market: the holdings' worth and cost add up");
        Check(Market.Sell(file, company, 60, purse) == TradeAnswer.Done && file.Holdings.Count == 0, "market: a holding sold out is gone");
        // Bought and sold at once, at one price: the player is down by the two commissions and nothing else.
        purse.Money = 50000;
        Market.Buy(file, company, 200, purse);
        Market.Sell(file, company, 200, purse);
        Check(purse.Money == 50000 - 100 - 100, "market: buying and selling at one price costs the two commissions, 2% in all");

        // Dividends: every fifteen market days, on the price of the day.
        VenturesFile paying = NewVentures(777);
        var income = new Purse { Money = 500000 };
        Company payer = paying.Companies[1];
        Market.Buy(paying, payer, 1000, income);
        int cash = income.Money, paid = 0, payouts = 0;
        bool right = true, onlyThen = true;
        for (int day = 1; day <= 90; day++)
        {
            Market.Step(paying, day, 0, null, null);
            onlyThen &= Market.DividendDue(paying) == (day % VenturesCatalogue.DividendEveryDays == 0);
            if (Market.DividendDue(paying))
            {
                int due = (int)(1000L * payer.PriceCents * payer.YieldBp / 1000000L);
                int got = Market.PayDividends(paying, income);
                right &= got == due && Market.Dividend(payer, 1000) == due;
                paid += got;
                payouts++;
            }
            right &= Market.DaysToDividend(paying) == VenturesCatalogue.DividendEveryDays - day % VenturesCatalogue.DividendEveryDays;
        }
        Check(onlyThen && payouts == 6 && right && income.Money == cash + paid && paying.Holding(payer.Id).Dividends == paid && paying.DividendsTotal == paid && paid > 0,
            "market: a dividend every fifteen days, the company's yield on the price of that day (" + paid + " on a thousand shares in ninety days)");
    }

    /// <summary>
    /// The market is no money printer. Ten game years (of the game's default sixty days) from eighty seeds, with news
    /// made up at about the rate the city makes it: buying every company on the first day and holding gives a modest
    /// return, and no rule of thumb does better than that by more than a small margin.
    /// </summary>
    private static void VenturesNoMoneyPrinter()
    {
        string[] names = { "buy and hold", "buy after a fall, sell after a rise", "buy after a rise, sell after a fall", "buy under the thirty-day average, sell over it",
            "buy for the dividend, sell after it", "buy on the day's good news", "sell on the day's bad news", "hold, but sell what has run a tenth over its average" };
        var mean = new double[names.Length];
        double worst = double.MaxValue, best = 0;
        int tradesAll = 0;
        for (int rule = 0; rule < names.Length; rule++)
        {
            for (int seed = 1; seed <= MarketSeeds; seed++)
            {
                int trades;
                double worth = Trade(seed * 104729, rule, out trades);
                mean[rule] += worth / MarketSeeds;
                tradesAll += rule > 0 ? trades : 0;
                if (rule == 0)
                {
                    worst = Math.Min(worst, worth);
                    best = Math.Max(best, worth);
                }
            }
        }
        double yearly = Math.Pow(mean[0] / MarketCash, 1.0 / MarketYears) - 1.0;
        Check(yearly > 0.03 && yearly < 0.07, "no money printer: buying everything and holding it returns " + (yearly * 100).ToString("0.0") + "% a game year after commission, between 3% and 7%");
        Check(worst > MarketCash * 1.2 && best < MarketCash * 2.2, "no money printer: after ten years the unluckiest of eighty holders has " + (worst / MarketCash).ToString("0.00")
              + " times the money put in and the luckiest " + (best / MarketCash).ToString("0.00") + ": inside 1.2 and 2.2");
        for (int rule = 1; rule < names.Length; rule++)
        {
            double edge = Math.Pow(mean[rule] / mean[0], 1.0 / MarketYears) - 1.0;
            Check(edge < 0.01, "no money printer: '" + names[rule] + "' beats holding by " + (edge * 100).ToString("0.00") + "% a year: under 1%");
        }
        Check(tradesAll > 5000, "no money printer: the rules did trade (" + tradesAll + " trades)");
        // The figures the README quotes, written out on every run.
        var line = new System.Text.StringBuilder("Market over " + MarketYears + " years of " + MarketYear + " days, " + MarketSeeds + " seeds: holding returns " + (yearly * 100).ToString("0.00")
                                                 + "% a year (" + (worst / MarketCash).ToString("0.00") + " to " + (best / MarketCash).ToString("0.00") + " times the money after ten years)");
        for (int rule = 1; rule < names.Length; rule++)
        {
            line.Append("; '").Append(names[rule]).Append("' ").Append(((Math.Pow(mean[rule] / mean[0], 1.0 / MarketYears) - 1.0) * 100).ToString("+0.00;-0.00")).Append('%');
        }
        Console.WriteLine(line.Append('.').ToString());
    }
}
