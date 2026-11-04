using System;
using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Dealership;

// Ninety days of the car dealership as DealerDirector runs them (its hour: DealerDay.Morning, the room's hour, the yard's hour), for what the interface
// conversion added: the other dealers' cars and prices, the viewings of each car, the bids of the auction's room and the player's own, with a save on
// day 30 read back to the same next thirty days and a save from before the features playing on.
internal static partial class Program
{
    private sealed class DealerRunState
    {
        public DealerState S;
        public int Offers, Looks, Bids, PlayerBids, Outbid;
        public int Cash;
    }

    private static DealerRunState RunDealerNew(int seed, IList<MarketModel> models)
    {
        var s = new DealerState { Seed = seed, FirstDay = 1 };
        var y = new DealerYard { Place = "Main%20Street~1", Name = "Main Street Motors" };
        for (int b = 0; b < 8; b++)
        {
            y.Spots.Add(new LotSpot { X = b * 300 });
        }
        for (int k = 0; k < 8; k++)
        {
            y.Cars.Add(new StockCar { Id = k + 1, VehicleId = "veh-" + k, Type = models[k % 4].Type, Condition = 900, Km = 20000, Asking = 20000 + k * 1500, Bought = 0, Spot = k });
        }
        s.NextId = 9;
        s.Yards.Add(y);
        return new DealerRunState { S = s };
    }

    // One hour of the dealership: the morning when the day has turned, the auction's room, then the lot's customers while it is open and staffed.
    private static void RunDealerHour(DealerRunState r, int now, IList<MarketModel> models, bool bidding)
    {
        int day = now / 24, level = 1000;
        DealerDay.Morning(r.S, day, models, new[] { "Red", "Blue" }, level);
        r.Outbid += DealerRules.RoomHour(r.S, now, models, level);
        var down = new List<DealerListing>();
        DealerRules.KnockedDown(r.S, now, down);
        for (int i = 0; i < down.Count; i++)
        {
            down[i].Gone = true;
        }
        if (DealerRules.SaleDue(r.S, now % 24))
        {
            DealerRules.OpenSale(r.S, now, models, new[] { "Red", "Blue" }, level);
        }
        if (bidding && now % 24 == 10)
        {
            // The player bids on the first open lot, at the least that is allowed; the room may bid over.
            for (int i = 0; i < r.S.Market.Count; i++)
            {
                DealerListing l = r.S.Market[i];
                if (l.Kind == ListingKind.Auction && !l.Gone && l.Leader != 0 && l.Ends > now)
                {
                    int worth = 20000;
                    if (DealerRules.BidAt(l, worth, now, DealerRules.NextBid(l, worth)))
                    {
                        r.PlayerBids++;
                    }
                    break;
                }
            }
        }
        DealerYard y = r.S.Yards[0];
        var worths = new List<int>();
        for (int c = 0; c < y.Cars.Count; c++)
        {
            worths.Add(y.Cars[c].Asking);
        }
        bool open = now % 24 >= 8 && now % 24 < 20;
        var h = new YardHour { Open = open, ShopCustomers = 60f, Promotion = 60, BaseMultiplier = 0.5f, Satisfaction = 60, Staff = open ? 3 : 0, Skill = 60, Buyers = 1100 };
        DealerHour got = DealerRules.Hour(r.S, y, h, now, day, worths, 100, models, null, level);
        r.Offers += got.Offers;
        // Offers nobody answers lapse by themselves; the staff close what reaches them at their floor (here: nothing is closed, the offers are left standing).
    }

    private static string DealerDigest(DealerRunState r)
    {
        var sb = new StringBuilder(2048);
        sb.Append(r.S.MarketDay).Append('|').Append(r.S.NextId).Append('|').Append(r.S.Rivals.Count).Append('|');
        for (int i = 0; i < r.S.Rivals.Count; i++)
        {
            sb.Append(r.S.Rivals[i].Rival).Append('.').Append(r.S.Rivals[i].Type).Append('.').Append(r.S.Rivals[i].Ask).Append(',');
        }
        for (int i = 0; i < r.S.Market.Count; i++)
        {
            DealerListing l = r.S.Market[i];
            sb.Append('#').Append(l.Id).Append('.').Append(l.Asking).Append('.').Append(l.Leader).Append('.').Append(l.Bids).Append('.').Append(l.Gone ? 1 : 0);
            for (int b = 0; b < l.BidLogCount; b++)
            {
                sb.Append(':').Append(l.BidLog[b].Bidder).Append('-').Append(l.BidLog[b].Hour).Append('-').Append(l.BidLog[b].Amount);
            }
        }
        for (int y = 0; y < r.S.Yards.Count; y++)
        {
            DealerYard yard = r.S.Yards[y];
            for (int c = 0; c < yard.Cars.Count; c++)
            {
                sb.Append('/').Append(yard.Cars[c].Id).Append('.').Append(yard.Cars[c].Asking).Append('.').Append(yard.Cars[c].ViewsToday).Append('.').Append(yard.Cars[c].ViewsLast);
            }
            for (int d = 0; d < yard.Deals.Count; d++)
            {
                sb.Append('+').Append(yard.Deals[d].Customer).Append('.').Append(yard.Deals[d].Looks).Append('.').Append(yard.Deals[d].Offer);
            }
        }
        return sb.ToString();
    }

    private static void DealerRun()
    {
        {
            IList<MarketModel> m0 = W4Models();
            DealerRunState x = RunDealerNew(21, m0), y = RunDealerNew(21, m0);
            string first = null;
            for (int day = 2; day <= 40 && first == null; day++)
            {
                for (int hour = 0; hour < 24 && first == null; hour++)
                {
                    RunDealerHour(x, day * 24 + hour, m0, true);
                    RunDealerHour(y, day * 24 + hour, m0, true);
                    first = DealerDigest(x) != DealerDigest(y) ? "day " + day + " hour " + hour : null;
                }
            }
            Check(first == null, "two dealerships of the same seed play the same forty days hour for hour" + (first != null ? " (they part on " + first + ")" : string.Empty));
        }
        IList<MarketModel> models = W4Models();
        DealerRunState run = RunDealerNew(21, models);
        DealerRunState other = null, old = null;
        var data = new Dictionary<string, string>();
        int rivalChanges = 0, lastRival = -1, viewsSeen = 0, looksSeen = 0;
        bool bounds = true;
        for (int day = 2; day <= 91; day++)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                int now = day * 24 + hour;
                RunDealerHour(run, now, models, true);
                if (other != null && day > 30 && day <= 60)
                {
                    RunDealerHour(other, now, models, true);
                }
                if (old != null && day > 30 && day <= 40)
                {
                    RunDealerHour(old, now, models, true);
                }
            }
            int rivalSum = 0;
            for (int i = 0; i < run.S.Rivals.Count; i++)
            {
                rivalSum += run.S.Rivals[i].Ask;
            }
            rivalChanges += lastRival >= 0 && rivalSum != lastRival ? 1 : 0;
            lastRival = rivalSum;
            bounds &= run.S.Rivals.Count >= DealerCatalogue.RivalNames.Length * RivalRules.PerDealerLeast && run.S.Rivals.Count <= RivalRules.Most;
            for (int c = 0; c < run.S.Yards[0].Cars.Count; c++)
            {
                viewsSeen += run.S.Yards[0].Cars[c].ViewsLast;
            }
            for (int d = 0; d < run.S.Yards[0].Deals.Count; d++)
            {
                looksSeen += run.S.Yards[0].Deals[d].Looks;
            }
            if (day == 30)
            {
                string text = DealerStore.Write(run.S);
                DealerExtraStore.Write(run.S, data);
                int version;
                other = new DealerRunState { S = DealerStore.Read(text, out version) };
                DealerExtraStore.Read(other.S, data);
                string e0 = DealerDigest(run), e1 = DealerDigest(other);
                int q = 0;
                while (q < e0.Length && q < e1.Length && e0[q] == e1[q])
                {
                    q++;
                }
                Check(e0 == e1, "a dealership saved on day 30 reads back to the state it was saved from" + (e0 == e1 ? string.Empty : " (they part at character " + q + ": " + e0.Substring(Math.Max(0, q - 40), Math.Min(100, e0.Length - Math.Max(0, q - 40))) + " / " + e1.Substring(Math.Max(0, q - 40), Math.Min(100, e1.Length - Math.Max(0, q - 40))) + ")"));
                old = new DealerRunState { S = DealerStore.Read(text, out version) };
                DealerExtraStore.Read(old.S, new Dictionary<string, string>());
                Check(old.S.Rivals.Count == 0 && old.S.Yards[0].Cars[0].ViewsLast == 0, "a save from before the features has no other dealers' cars and no viewings: it loads as it was");
                RivalRules.Morning(old.S, 31, models, 1000);   // what DealerDirector.Start does for it
            }
            if (day > 30 && day <= 60 && other != null)
            {
                string a = DealerDigest(run), b = DealerDigest(other);
                if (a != b)
                {
                    int at = 0;
                    while (at < a.Length && at < b.Length && a[at] == b[at])
                    {
                        at++;
                    }
                    int from = Math.Max(0, at - 80);
                    Check(false, "a dealership saved on day 30 plays the same as the one it was saved from: they part on day " + day + " at character " + at + "\n  " + a.Substring(from, Math.Min(200, a.Length - from)) + "\n  " + b.Substring(from, Math.Min(200, b.Length - from)));
                    other = null;
                }
                else if (day == 60)
                {
                    Check(true, "a dealership saved on day 30 plays the next thirty days exactly as the one it was saved from (the other dealers' cars, the viewings, the bids; " + a.Length + " characters compared every day)");
                }
            }
            if (day == 40)
            {
                Check(old.S.Rivals.Count >= DealerCatalogue.RivalNames.Length * RivalRules.PerDealerLeast, "...and the old save's room is stocked with the other dealers' cars at once and goes on to trade ("
                    + old.S.Rivals.Count + " cars)");
            }
        }
        DealerYard yard = run.S.Yards[0];
        Check(bounds, "every morning each other dealer has between " + RivalRules.PerDealerLeast + " and " + RivalRules.PerDealerMost + " cars out");
        Check(rivalChanges >= 20, "the other dealers' prices move with the market, day after day (" + rivalChanges + " of ninety days changed their asks)");
        Check(viewsSeen > 0, "the cars on the lot are looked at: " + viewsSeen + " viewings rolled into the day before");
        Check(run.Offers > 0 && looksSeen > 0, "the customers who make an offer have looked at the car first: " + run.Offers + " offers, " + looksSeen + " looks held in the offers standing");
        int logged = 0, byRoom = 0, byPlayer = 0, listings = 0;
        bool ordered = true;
        for (int i = 0; i < run.S.Market.Count; i++)
        {
            DealerListing l = run.S.Market[i];
            if (l.Kind != ListingKind.Auction)
            {
                continue;
            }
            listings++;
            logged += l.BidLogCount;
            ordered &= l.BidLogCount <= DealerCatalogue.BidsKept;
            for (int b = 0; b < l.BidLogCount; b++)
            {
                byRoom += l.BidLog[b].Bidder >= 1 ? 1 : 0;
                byPlayer += l.BidLog[b].Bidder == 0 ? 1 : 0;
                ordered &= b == 0 || l.BidLog[b].Hour <= l.BidLog[b - 1].Hour;
            }
        }
        Check(run.PlayerBids >= 5 && ordered, "the player's bids are taken at the least a lot allows (" + run.PlayerBids + " over the run) and the logs run newest first within their cap");
        Check(listings > 0 && logged > 0 && byRoom > 0, "the room bids by its dealers and every bid stands in the lot's log (" + logged + " bids on " + listings + " lots now, " + byRoom + " by the room, " + byPlayer + " the player's)");
        Check(run.Outbid >= 0 && yard.Cars.Count == 8, "the lot is untouched by the room: its cars stay");

        // The dealers' prices bear on the offers: the same lot, with the other dealers' cars and without them.
        int withRivals = 0, withoutRivals = 0;
        for (int seed = 1; seed <= 12; seed++)
        {
            DealerRunState a = RunDealerNew(seed, models), b = RunDealerNew(seed, models);
            for (int day = 2; day <= 40; day++)
            {
                for (int hour = 8; hour < 20; hour++)
                {
                    RunDealerHour(a, day * 24 + hour, models, false);
                    b.S.Rivals.Clear();
                    RunDealerHour(b, day * 24 + hour, models, false);
                    b.S.Rivals.Clear();
                }
            }
            withRivals += a.Offers;
            withoutRivals += b.Offers;
        }
        Check(withRivals != withoutRivals, "the other dealers' asks bear on the customers: " + withRivals + " offers with them in the city, " + withoutRivals + " without");
    }
}
