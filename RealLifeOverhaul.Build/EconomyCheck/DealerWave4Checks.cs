using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Dealership;

// Wave 4 of the car showroom (Scripts/Economy/Dealer/RivalRules.cs, ViewRules.cs, DealerExtraStore.cs): the other dealers' cars with prices of their own and a buyer who compares
// them with the player's, the looks each car gets in a day and what a buyer looked before an offer, the last eight bids of each auction lot
// (the dealers of the room are the bidders) and a bid above the next step, each through the save and back, from a save that has none of it, and
// in the time an hour allows.
internal static partial class Program
{
    private static void DealerWave4()
    {
        W4Rivals();
        W4Compare();
        W4Looks();
        W4Bids();
        W4DealerStore();
        W4DealerCost();
    }

    private static IList<MarketModel> W4Models()
    {
        var list = new List<MarketModel>();
        for (int i = 0; i < DealerModels.Length; i++)
        {
            list.Add(DealerModels[i]);
        }
        for (int i = 0; i < 20; i++)
        {
            list.Add(new MarketModel("ba:vehicletype_extra" + i, 8000f + i * 3500f));
        }
        return list;
    }

    private static DealerState W4Dealer(int seed = 9)
    {
        var s = new DealerState { Seed = seed, FirstDay = 1 };
        var y = new DealerYard { Place = "Main Street|1", Name = "Main Street Motors" };
        for (int b = 0; b < 8; b++)
        {
            y.Spots.Add(new LotSpot { X = b * 300 });
        }
        s.Yards.Add(y);
        return s;
    }

    // ------------------------------------------------------------------------------------------------- the other dealers' cars

    private static bool W4Suppliers;   // the dealers' price checks run again with the suppliers on, as shipped (BizAsksChecks.Day.cs)

    private static void W4Rivals()
    {
        IList<MarketModel> models = W4Models();
        DealerState s = W4Dealer();
        RivalRules.Morning(s, 10, models, 1000, W4Suppliers);
        int dealers = DealerCatalogue.RivalNames.Length;
        var count = new int[dealers];
        bool known = true, priced = true;
        for (int i = 0; i < s.Rivals.Count; i++)
        {
            count[s.Rivals[i].Rival]++;
            bool found = false;
            for (int m = 0; m < models.Count; m++)
            {
                found |= models[m].Type == s.Rivals[i].Type;
            }
            known &= found;
            priced &= s.Rivals[i].Ask > 0;
        }
        bool counts = true;
        for (int r = 0; r < dealers; r++)
        {
            counts &= count[r] >= RivalRules.PerDealerLeast && count[r] <= RivalRules.PerDealerMost;
        }
        Check(counts && s.Rivals.Count <= RivalRules.Most, "each of the " + dealers + " dealers keeps " + RivalRules.PerDealerLeast + " to " + RivalRules.PerDealerMost + " cars (" + string.Join(",", count) + ")");
        Check(known && priced, "their cars are the game's own vehicle types, each with a price");
        DealerState again = W4Dealer();
        RivalRules.Morning(again, 10, models, 1000, W4Suppliers);
        bool same = again.Rivals.Count == s.Rivals.Count;
        for (int i = 0; same && i < s.Rivals.Count; i++)
        {
            same &= again.Rivals[i].Type == s.Rivals[i].Type && again.Rivals[i].Ask == s.Rivals[i].Ask && again.Rivals[i].Rival == s.Rivals[i].Rival;
        }
        Check(same, "the same save and day put out the same cars at the same prices, on every machine");
        int before = s.Rivals.Count;
        var firstDay = new List<RivalCar>(s.Rivals);
        for (int day = 11; day < 41; day++)
        {
            RivalRules.Morning(s, day, models, 1000, W4Suppliers);
        }
        int changed = 0;
        for (int i = 0; i < s.Rivals.Count; i++)
        {
            bool stillThere = false;
            for (int k = 0; k < firstDay.Count; k++)
            {
                stillThere |= firstDay[k].Rival == s.Rivals[i].Rival && firstDay[k].Day == s.Rivals[i].Day && firstDay[k].Type == s.Rivals[i].Type;
            }
            changed += stillThere ? 0 : 1;
        }
        Check(changed > before / 2 && s.Rivals.Count <= RivalRules.Most, "over a month the dealers sell cars and put new ones out, never more than the most (" + changed + " of " + s.Rivals.Count + " new)");
        int lowBias = int.MaxValue, highBias = 0;
        for (int r = 0; r < dealers; r++)
        {
            int b = RivalRules.BiasOf(9, r);
            lowBias = Math.Min(lowBias, b);
            highBias = Math.Max(highBias, b);
            Check(b >= RivalRules.BiasLeast && b <= RivalRules.BiasMost && b == RivalRules.BiasOf(9, r), "dealer " + r + "'s usual price against the worth is its own, in range, and the same every time (" + b + ")");
        }
        Check(highBias > lowBias, "the dealers do not all price alike");
        DealerState dear = W4Dealer();
        RivalRules.Morning(dear, 10, models, 1400, W4Suppliers);
        long sumNow = 0, sumDear = 0;
        for (int i = 0; i < s.Rivals.Count && i < dear.Rivals.Count; i++)
        {
            sumDear += dear.Rivals[i].Ask;
        }
        DealerState base1 = W4Dealer();
        RivalRules.Morning(base1, 10, models, 1000, W4Suppliers);
        for (int i = 0; i < base1.Rivals.Count && i < dear.Rivals.Count; i++)
        {
            sumNow += base1.Rivals[i].Ask;
        }
        Check(sumDear > sumNow * 12 / 10, "the dealers' prices follow the city's price level");
        string type = s.Rivals[0].Type;
        int who;
        int cheapest = RivalRules.Cheapest(s, type, out who);
        bool lowest = cheapest > 0 && who >= 0;
        for (int i = 0; i < s.Rivals.Count; i++)
        {
            lowest &= s.Rivals[i].Type != type || s.Rivals[i].Ask >= cheapest;
        }
        Check(lowest && RivalRules.Cheapest(s, "ba:vehicletype_nothing", out who) == 0 && who == -1, "the cheapest rival price for a type is found, and none says so");
        RivalRules.Morning(s, 50, new List<MarketModel>(), 1000, W4Suppliers);
        Check(s.Rivals.Count == 0, "with no vehicle types in the game the dealers have no cars to show");
    }

    private static void W4Compare()
    {
        Check(RivalRules.Compare(10000, 10000) == 1000 && RivalRules.Compare(10000, 0) == 1000 && RivalRules.Compare(0, 10000) == 1000, "a price level with a rival's, or no rival's, changes nothing");
        int dear = RivalRules.Compare(11000, 10000), dearer = RivalRules.Compare(13000, 10000), wild = RivalRules.Compare(40000, 10000);
        Check(dear == 800 && dearer == RivalRules.DearFloor && wild == RivalRules.DearFloor, "dearer than the rival, a buyer's offer chance falls to " + dear + " thousandths at ten per cent over, never under " + RivalRules.DearFloor);
        int cheap = RivalRules.Compare(9000, 10000), cheaper = RivalRules.Compare(1000, 10000);
        Check(cheap == 1050 && cheaper == RivalRules.CheapMost, "cheaper than the rival it rises, to " + cheap + " at ten per cent under, never over " + RivalRules.CheapMost);
        bool monotone = true;
        for (int ask = 5000; ask < 20000; ask += 250)
        {
            monotone &= RivalRules.Compare(ask + 250, 10000) <= RivalRules.Compare(ask, 10000);
        }
        Check(monotone, "the dearer a car the less a buyer's chance, all the way");

        // The whole rule: a showroom that asks more than the rival for the same car gets fewer offers than one that asks less.
        IList<MarketModel> models = W4Models();
        int offersDear = 0, offersCheap = 0;
        for (int run = 0; run < 8; run++)
        {
            DealerState a = W4Dealer(30 + run), b = W4Dealer(30 + run);
            RivalRules.Morning(a, 10, models, 1000);
            RivalRules.Morning(b, 10, models, 1000);
            a.Rivals.Clear();
            b.Rivals.Clear();
            a.Rivals.Add(new RivalCar { Rival = 0, Type = models[1].Type, Ask = 30000, Day = 9 });
            b.Rivals.Add(new RivalCar { Rival = 0, Type = models[1].Type, Ask = 30000, Day = 9 });
            DealerYard ya = a.Yards[0], yb = b.Yards[0];
            for (int k = 0; k < 4; k++)
            {
                ya.Cars.Add(new StockCar { Id = k + 1, Type = models[1].Type, Condition = 900, Km = 40000, Asking = 36000, Bought = 9, Spot = k });
                yb.Cars.Add(new StockCar { Id = k + 1, Type = models[1].Type, Condition = 900, Km = 40000, Asking = 26000, Bought = 9, Spot = k });
            }
            var worths = new List<int> { 30000, 30000, 30000, 30000 };
            var h = new YardHour { Open = true, ShopCustomers = 60f, Promotion = 60, BaseMultiplier = 0.5f, Satisfaction = 60, Staff = 4, Skill = 60, Buyers = 1000 };
            for (int hour = 8; hour < 20; hour++)
            {
                for (int day = 10; day < 20; day++)
                {
                    int now = day * 24 + hour;
                    DealerHour ra = DealerRules.Hour(a, ya, h, now, day, worths, 100, models, null, 1000);
                    DealerHour rb = DealerRules.Hour(b, yb, h, now, day, worths, 100, models, null, 1000);
                    offersDear += ra.Offers;
                    offersCheap += rb.Offers;
                    ya.Deals.Clear();
                    yb.Deals.Clear();
                }
            }
        }
        Check(offersCheap > offersDear, "a showroom asking under the other dealers' price for the same car gets more offers than one asking over it (" + offersCheap + " against " + offersDear + ")");
    }

    // ------------------------------------------------------------------------------------------------- looks

    private static void W4Looks()
    {
        var car = new StockCar { Id = 1 };
        Check(ViewRules.Look(car) == 1 && ViewRules.Look(car) == 2 && car.ViewsToday == 2, "each buyer who looks at a car is counted for the day");
        for (int i = 0; i < 300; i++)
        {
            ViewRules.Look(car);
        }
        Check(car.ViewsToday == ViewRules.LooksMost, "the count stops at " + ViewRules.LooksMost);
        var y = new DealerYard { Place = "p" };
        y.Cars.Add(car);
        y.Cars.Add(new StockCar { Id = 2, ViewsToday = 3 });
        ViewRules.NewDay(y);
        Check(car.ViewsLast == ViewRules.LooksMost && car.ViewsToday == 0 && y.Cars[1].ViewsLast == 3 && y.Cars[1].ViewsToday == 0, "when the day closes today's looks are yesterday's and every car starts with none");
        var seen = new StockCar { ViewsToday = 5 };
        int a = ViewRules.LooksBeforeOffer(seen, 3, 7, 11), b = ViewRules.LooksBeforeOffer(seen, 3, 7, 11);
        Check(a == b && a >= 1 && a <= 3, "a buyer looked once to three times before the offer, the same every time (" + a + ")");
        Check(ViewRules.LooksBeforeOffer(new StockCar { ViewsToday = 0 }, 3, 7, 11) == 1 && ViewRules.LooksBeforeOffer(new StockCar { ViewsToday = 2 }, 3, 7, 11) <= 2, "never more than the car was looked at");

        // Through the hour: looks are counted by the buyers the floor serves, and an offer says how often its buyer looked.
        IList<MarketModel> models = W4Models();
        DealerState s = W4Dealer();
        RivalRules.Morning(s, 10, models, 1000);
        DealerYard lot = s.Yards[0];
        for (int k = 0; k < 6; k++)
        {
            lot.Cars.Add(new StockCar { Id = k + 1, Type = models[k % 4].Type, Condition = 900, Km = 20000, Asking = 20000 + k * 1000, Bought = 9, Spot = k });
        }
        var worths = new List<int> { 20000, 21000, 22000, 23000, 24000, 25000 };
        var h = new YardHour { Open = true, ShopCustomers = 60f, Promotion = 70, BaseMultiplier = 0.5f, Satisfaction = 60, Staff = 4, Skill = 70, Buyers = 1200 };
        int served = 0, offers = 0;
        for (int hour = 8; hour < 20; hour++)
        {
            DealerHour r = DealerRules.Hour(s, lot, h, 10 * 24 + hour, 10, worths, 100, models, null, 1000);
            served += r.Served;
            offers += r.Offers;
        }
        int views = 0;
        for (int c = 0; c < lot.Cars.Count; c++)
        {
            views += lot.Cars[c].ViewsToday;
        }
        bool looks = lot.Deals.Count > 0;
        for (int d = 0; d < lot.Deals.Count; d++)
        {
            looks &= lot.Deals[d].Looks >= 1 && lot.Deals[d].Looks <= 3;
        }
        Check(views > 0 && views <= served && looks, "a day's buyers are counted as looks on the cars (" + views + " looks for " + served + " served), and each offer says how often its buyer looked");
        DealerRules.CloseDay(s, lot, 11);
        int sum = 0;
        for (int c = 0; c < lot.Cars.Count; c++)
        {
            sum += lot.Cars[c].ViewsLast;
        }
        Check(sum == views && offers >= 0, "closing the day keeps the looks as yesterday's");
    }

    // ------------------------------------------------------------------------------------------------- bids

    private static void W4Bids()
    {
        var l = new DealerListing { Id = 5, Kind = ListingKind.Auction, Asking = 10000, Ends = 1000, Leader = -1 };
        int worth = 16000;
        int step = DealerRules.Increment(worth);
        Check(DealerRules.NextBid(l, worth) == 10000 && !DealerRules.BidAt(l, worth, 100, 9000) && !DealerRules.BidAt(l, worth, 100, DealerRules.BidMost(worth) + 1) && !DealerRules.BidAt(l, worth, 1000, 10000),
            "a bid under the opening one, over the most or after the hammer is refused");
        Check(DealerRules.BidAt(l, worth, 100, 10600) && l.Asking == 10600 && l.Leader == 0 && l.Bids == 1 && l.BidLogCount == 1 && l.BidLog[0].Bidder == 0 && l.BidLog[0].Hour == 100 && l.BidLog[0].Amount == 10600,
            "the player may bid above the opening price, and it is the bid on the lot, written first in its log");
        Check(!DealerRules.BidAt(l, worth, 101, 20000) && !DealerRules.Bid(l, worth, 101), "the player cannot bid against themselves");
        l.Leader = 3;
        Check(!DealerRules.BidAt(l, worth, 101, 10600 + step - 1) && DealerRules.Bid(l, worth, 101) && l.Asking == 10600 + step, "over a rival's bid it takes at least one step (" + step + "); Bid keeps its old meaning");
        for (int i = 0; i < 12; i++)
        {
            l.LogBid(1 + i % 6, 200 + i, 12000 + i * 100);
        }
        Check(l.BidLogCount == DealerCatalogue.BidsKept && l.BidLog[0].Amount == 12000 + 11 * 100 && l.BidLog[0].Hour == 211 && l.BidLog[DealerCatalogue.BidsKept - 1].Hour == 204,
            "the log keeps the last " + DealerCatalogue.BidsKept + " bids, newest first");

        // The room: the dealers bid by their ceilings and every bid they make is written, with who.
        IList<MarketModel> models = W4Models();
        DealerState s = W4Dealer();
        DealerRules.OpenSale(s, 9 * 24, models, new[] { "Red" }, 1000);
        int lots = 0, logged = 0, rivalsBid = 0;
        for (int hour = 9 * 24 + 1; hour < 9 * 24 + 8; hour++)
        {
            DealerRules.RoomHour(s, hour, models, 1000);
        }
        for (int i = 0; i < s.Market.Count; i++)
        {
            DealerListing lot = s.Market[i];
            if (lot.Kind != ListingKind.Auction)
            {
                continue;
            }
            lots++;
            logged += lot.BidLogCount;
            Check(lot.BidLogCount == Math.Min(lot.Bids, DealerCatalogue.BidsKept), "every bid on lot " + lot.Id + " is in its log (" + lot.Bids + " bids, " + lot.BidLogCount + " logged)");
            for (int b = 0; b < lot.BidLogCount; b++)
            {
                rivalsBid += lot.BidLog[b].Bidder >= 1 && lot.BidLog[b].Bidder <= DealerCatalogue.RivalNames.Length ? 1 : 0;
                Check(lot.BidLog[b].Amount > 0 && lot.BidLog[b].Amount <= lot.Ceiling && (b == 0 || lot.BidLog[b].Amount <= lot.BidLog[b - 1].Amount) && (b == 0 || lot.BidLog[b].Hour <= lot.BidLog[b - 1].Hour),
                    "the log of lot " + lot.Id + " runs newest first, by amount and hour, and never past the room's ceiling");
            }
        }
        Check(lots == DealerCatalogue.AuctionLots && logged > 0 && rivalsBid == logged, "the dealers of the room are the bidders: " + logged + " bids on " + lots + " lots, every one by a named dealer");
    }

    // ------------------------------------------------------------------------------------------------- the save

    private static void W4DealerStore()
    {
        IList<MarketModel> models = W4Models();
        DealerState s = W4Dealer();
        RivalRules.Morning(s, 10, models, 1000);
        DealerYard y = s.Yards[0];
        y.Cars.Add(new StockCar { Id = 1, Type = models[0].Type, ViewsToday = 4, ViewsLast = 7 });
        y.Cars.Add(new StockCar { Id = 2, Type = models[1].Type });
        y.Deals.Add(new DealerOffer { Id = 3, Car = 1, Customer = "x", Looks = 2 });
        DealerRules.OpenSale(s, 9 * 24, models, new[] { "Red" }, 1000);
        for (int hour = 9 * 24 + 1; hour < 9 * 24 + 6; hour++)
        {
            DealerRules.RoomHour(s, hour, models, 1000);
        }
        var data = new Dictionary<string, string>();
        DealerExtraStore.Write(s, data);
        DealerState back = W4Dealer();
        back.Yards[0].Cars.Add(new StockCar { Id = 1, Type = models[0].Type });
        back.Yards[0].Cars.Add(new StockCar { Id = 2, Type = models[1].Type });
        back.Yards[0].Deals.Add(new DealerOffer { Id = 3, Car = 1, Customer = "x" });
        for (int i = 0; i < s.Market.Count; i++)
        {
            DealerListing l = s.Market[i];
            back.Market.Add(new DealerListing { Id = l.Id, Kind = l.Kind, Type = l.Type });
        }
        DealerExtraStore.Read(back, data);
        bool rivals = back.Rivals.Count == s.Rivals.Count;
        for (int i = 0; rivals && i < s.Rivals.Count; i++)
        {
            rivals &= back.Rivals[i].Rival == s.Rivals[i].Rival && back.Rivals[i].Type == s.Rivals[i].Type && back.Rivals[i].Ask == s.Rivals[i].Ask && back.Rivals[i].Day == s.Rivals[i].Day;
        }
        Check(rivals, "the other dealers' cars are the same through the save and back (" + back.Rivals.Count + ")");
        Check(back.Yards[0].Cars[0].ViewsToday == 4 && back.Yards[0].Cars[0].ViewsLast == 7 && back.Yards[0].Cars[1].ViewsToday == 0 && back.Yards[0].Deals[0].Looks == 2, "each car's looks and each offer's looks are the same");
        bool bids = true;
        int auction = 0;
        for (int i = 0; i < s.Market.Count; i++)
        {
            DealerListing a = s.Market[i], b = back.Market[i];
            auction += a.BidLogCount;
            bids &= a.BidLogCount == b.BidLogCount;
            for (int k = 0; k < a.BidLogCount; k++)
            {
                bids &= a.BidLog[k].Amount == b.BidLog[k].Amount && a.BidLog[k].Bidder == b.BidLog[k].Bidder && a.BidLog[k].Hour == b.BidLog[k].Hour;
            }
        }
        Check(bids && auction > 0, "every lot's last bids are the same (" + auction + " bids)");

        // A save from before: none of the three lines.
        DealerState old = W4Dealer();
        old.Yards[0].Cars.Add(new StockCar { Id = 1, Type = models[0].Type });
        DealerExtraStore.Read(old, new Dictionary<string, string>());
        Check(old.Rivals.Count == 0 && old.Yards[0].Cars[0].ViewsToday == 0, "a save from before has no rival cars, looks or bids: it loads as it was, and the rivals are put out at once by the director");
        RivalRules.Morning(old, 10, models, 1000);
        Check(old.Rivals.Count >= DealerCatalogue.RivalNames.Length * RivalRules.PerDealerLeast, "(and one morning's restock fills them)");
        var junk = new Dictionary<string, string>
        {
            { DealerExtraStore.RivalsKey, "v=1|9,ba%3Avehicletype_x,100,1;1,,5,2;x" }, { DealerExtraStore.ViewsKey, "v=1|c,99,1,1;o,1" }, { DealerExtraStore.BidsKey, "v=1|42,1_2_3" }
        };
        DealerExtraStore.Read(old, junk);
        Check(old.Rivals.Count == 0, "a rival that is not one of the dealers, a car with no type and a line that is not a record are dropped, and nothing is thrown");
        Check(!DealerExtraStore.ReadRivals(old, "rubbish") && !DealerExtraStore.ReadViews(old, null) && !DealerExtraStore.ReadBids(old, string.Empty), "a line that is not the format is refused");
        var big = new DealerState { Seed = 1 };
        for (int i = 0; i < 100; i++)
        {
            big.Rivals.Add(new RivalCar { Rival = i % 6, Type = "ba:vehicletype_x" + i, Ask = 1000 + i, Day = i });
        }
        DealerState capped = W4Dealer();
        DealerExtraStore.ReadRivals(capped, DealerExtraStore.WriteRivals(big));
        Check(capped.Rivals.Count == RivalRules.Most, "no more than " + RivalRules.Most + " rival cars are kept");
    }

    private static void W4DealerCost()
    {
        IList<MarketModel> models = W4Models();
        DealerState s = W4Dealer();
        var watch = Stopwatch.StartNew();
        for (int day = 1; day <= 365; day++)
        {
            RivalRules.Morning(s, day, models, 1000);
        }
        double year = watch.Elapsed.TotalMilliseconds;
        watch.Restart();
        int cheapest = 0, who;
        for (int i = 0; i < 100000; i++)
        {
            cheapest += RivalRules.Cheapest(s, models[i % models.Count].Type, out who) > 0 ? 1 : 0;
        }
        double find = watch.Elapsed.TotalMilliseconds;
        Check(year < 300, "a year of the dealers' mornings is " + year.ToString("0.0") + " ms (under 300), about " + (year / 365).ToString("0.00") + " ms a morning");
        Check(find < 600 && cheapest >= 0, "100,000 looks for a rival's price take " + find.ToString("0.0") + " ms (under 600)");
    }
}
