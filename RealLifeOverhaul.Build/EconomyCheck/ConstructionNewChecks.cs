using System;
using System.Collections.Generic;
using RealLifeOverhaul.Construction;

// The Construction app's later features (Scripts/Construction: FinishRules, VarianceRules, AuctionRules, BuildState.Auction, BuildStore.New): the fit-out finish and what each
// tier does to cost, days and rent; the height variance hearing and its one suit for the player and the city's builders; land auctions on the calendar with the city's eight
// builders bidding by licence level, standing and need, the player's bids and offers on city lots; the rival developers' worth from their record and what they made on land.
// Each is driven over 720 game days with a seeded city and the save round trip, and an old line (before the parts) must load.
internal static partial class Program
{
    private static void ConstructionNewChecks()
    {
        FinishChecks();
        VarianceChecks();
        AuctionRuleChecks();
        AuctionRunChecks();
        NewStoreChecks();
        NewSourceChecks();
    }

    private static void FinishChecks()
    {
        Check(FinishRules.Tiers == 3 && FinishRules.TierOf(0) == FinishRules.Standard && FinishRules.TierOf(1) == FinishRules.Basic && FinishRules.TierOf(3) == FinishRules.Premium
              && FinishRules.TierOf(99) == FinishRules.Premium, "construction finish: the saved number reads 0 as the standard finish and 1 to 3 as basic, standard, premium");
        long estimate = 2000000L;
        Check(FinishRules.CostOf(0) == 1000 && FinishRules.CostOf(FinishRules.Standard + 1) == 1000 && FinishRules.CostOf(FinishRules.Premium + 1) == 1122 && FinishRules.CostOf(FinishRules.Basic + 1) == 948,
            "construction finish: a premium fit-out makes the build 12.2% dearer, a basic one 5.2% cheaper, the standard finish and an unchosen one the same");
        Check(FinishRules.Estimate(estimate, 0, FinishRules.Premium) == 2244000L && FinishRules.Estimate(2244000L, FinishRules.Premium + 1, FinishRules.Standard) == estimate && FinishRules.Estimate(-5L, 0, FinishRules.Premium) == 0L,
            "construction finish: an estimate follows the finish chosen, and back again, and none without an estimate");
        bool days = true;
        for (int hours = 20; hours <= 800; hours += 30)
        {
            days &= FinishRules.Hours(hours, FinishRules.Basic) <= FinishRules.Hours(hours, FinishRules.Standard) && FinishRules.Hours(hours, FinishRules.Standard) == hours
                    && FinishRules.Hours(hours, FinishRules.Premium) >= hours && FinishRules.DaysMore(hours, FinishRules.Premium) >= 0 && FinishRules.DaysMore(hours, FinishRules.Basic) <= 0;
        }
        Check(days, "construction finish: a premium finish never takes fewer days and a basic one never more, the standard finish keeps the site's hours");
        Check(FinishRules.Rent(1000000L, FinishRules.Premium) == 1150000L && FinishRules.Rent(1000000L, FinishRules.Basic) == 900000L && FinishRules.Rent(1000000L, FinishRules.Standard) == 1000000L,
            "construction finish: the rent follows the tier's share of the market's");
        Check(FinishRules.PaybackMonths(1000000L, 100000L) == 10 && FinishRules.PaybackMonths(1000000L, 0L) == 0, "construction finish: the months that pay a cost back, none without rent");
        Check(FinishRules.ByMeans(0L, 100000L, 60) == FinishRules.Basic && FinishRules.ByMeans(100000L, 1000000L, 60) == FinishRules.Basic
              && FinishRules.ByMeans(100000000L, 1000000L, 60) == FinishRules.Premium && FinishRules.ByMeans(100000000L, 1000000L, 20) == FinishRules.Standard
              && FinishRules.ByMeans(3000000L, 1000000L, 60) == FinishRules.Standard, "construction finish: a builder short of cash goes basic, a rich one premium where the use is wanted, else standard");
        bool order = true;
        for (long cash = 0L; cash <= 80000000L; cash += 500000L)
        {
            order &= FinishRules.ByMeans(cash + 500000L, 1000000L, 60) >= FinishRules.ByMeans(cash, 1000000L, 60);
        }
        Check(order, "construction finish: more cash never picks a lower tier");
        Check(FinishRules.Gain(1000000L, FinishRules.Premium) > FinishRules.Gain(1000000L, FinishRules.Standard) && FinishRules.Gain(1000000L, FinishRules.Standard) > FinishRules.Gain(1000000L, FinishRules.Basic),
            "construction finish: a developer clears more on a better finish");
        // Over 720 days a city of builders picks tiers by the same rule as their cash moves: all three tiers are picked, and the same seed picks the same.
        int[] picks = new int[3];
        for (int day = 1; day <= 720; day++)
        {
            for (int r = 0; r < BuildCatalogue.RivalCount; r++)
            {
                long cash = LandMarketRules.Means(BuildCatalogue.RivalReputation[r], day / 90, day / 120, 1000) * (day % 7 + 1) / 4L;
                picks[FinishRules.ByMeans(cash, 600000L + 40000L * r, 30 + (day * 7 + r * 13) % 60)]++;
            }
        }
        Check(picks[0] > 0 && picks[1] > 0 && picks[2] > 0, "construction finish: over 720 days the city's builders pick each tier as their means move (" + picks[0] + ", " + picks[1] + ", " + picks[2] + ")");
    }

    private static void VarianceChecks()
    {
        Check(VarianceRules.Over(14, 12) == 2 && VarianceRules.Over(10, 12) == 0 && VarianceRules.Askable(1) && VarianceRules.Askable(4) && !VarianceRules.Askable(0) && !VarianceRules.Askable(5),
            "construction variance: one to four floors over the code's limit may be asked for");
        bool suit = true;
        for (int over = 1; over < 5; over++)
        {
            for (int q = 30; q <= 90; q += 20)
            {
                for (int standing = 20; standing <= 80; standing += 20)
                {
                    for (int level = 0; level < LevelRules.Levels; level++)
                    {
                        int s = VarianceRules.Suit(over, q, standing, level, 0, 50);
                        suit &= s >= 50 && s <= 950 && VarianceRules.Suit(over + 1, q, standing, level, 0, 50) <= s && VarianceRules.Suit(over, q + 10, standing, level, 0, 50) >= s
                                && VarianceRules.Suit(over, q, standing + 10, level, 0, 50) >= s && VarianceRules.Suit(over, q, standing, level + 1, 0, 50) >= s
                                && VarianceRules.Suit(over, q, standing, level, 1, 50) >= s && VarianceRules.Suit(over, q, standing, level, 0, 80) >= s;
                    }
                }
            }
        }
        Check(suit, "construction variance: the suit falls with each floor over and rises with better plans, standing, licence, a building code and demand, always 50 to 950");
        string matter = VarianceRules.Matter("lot_7", 13);
        Check(VarianceRules.Planners(77, matter, 500) == VarianceRules.Planners(77, matter, 500) && !VarianceRules.Planners(77, matter, 0) && VarianceRules.Planners(77, matter, 1000),
            "construction variance: the planners' vote is the same each time it is asked, never for a suit of none and always for all");
        // The player and a city builder, over 720 days, ask by the one rule: the share granted follows the suit, and a stronger case is granted at least as often.
        int weakGranted = 0, strongGranted = 0, asked = 0;
        for (int day = 1; day <= 720; day++)
        {
            string m = VarianceRules.Matter("lot_" + day, 13 + day % 3);
            int weak = VarianceRules.Suit(3, 40, 30, 0, -1, 30), strong = VarianceRules.Suit(1, 85, 70, 3, 1, 80);
            weakGranted += VarianceRules.Planners(5, m, weak) ? 1 : 0;
            strongGranted += VarianceRules.Planners(5, m, strong) ? 1 : 0;
            asked++;
        }
        Check(asked == 720 && strongGranted > weakGranted + 200 && weakGranted > 0 && strongGranted < asked, "construction variance: over 720 hearings a strong case is granted far oftener than a weak one, and neither always (" + weakGranted + " and " + strongGranted + ")");
        Check(VarianceRules.Fee(1000L) == 2000L && VarianceRules.Fee(-1L) == 0L, "construction variance: the hearing costs twice the zoning fee");
    }

    private static void AuctionRuleChecks()
    {
        Check(AuctionRules.NextOpen(0) == 12 && AuctionRules.NextOpen(11) == 12 && AuctionRules.NextOpen(12) == 26 && AuctionRules.NextOpen(25) == 26 && AuctionRules.NextOpen(26) == 40,
            "construction auction: the calendar opens on day 12 and every fortnight after it");
        Check(AuctionRules.OpeningOf(1000000L) == 700000L && AuctionRules.ReserveOf(1000000L) == 850000L && AuctionRules.Step(1000000L) == 30000L && AuctionRules.Step(10000L) == 1000L,
            "construction auction: the opening bid is 70% of the list price, the reserve 85%, a step 3%, a thousand at the least");
        var a = new AuctionFile { Id = 1, List = 1000000L, Opening = 700000L, Reserve = 850000L, Opened = 12, Ends = 18, Area = 400 };
        Check(AuctionRules.NextBid(a) == 700000L && AuctionRules.BidWhy(a, 700000L, 5000000L, 13, 0) == null && AuctionRules.BidWhy(a, 690000L, 5000000L, 13, 0) == "reallife_auc_answer_bid_range"
              && AuctionRules.BidWhy(a, 700000L, 600000L, 13, 0) == "reallife_bld_answer_no_money" && AuctionRules.BidWhy(a, 3000000L, 9000000L, 13, 0) == "reallife_auc_answer_bid_range",
            "construction auction: the first bid is the opening one, under it or over twice the list price is out of range, and the money must be there");
        AuctionRules.Bid(a, 700000L, 13);
        Check(a.Leader == -1 && a.Bid == 700000L && a.Mine == 700000L && AuctionRules.NextBid(a) == 730000L && AuctionRules.BidWhy(a, 730000L, 5000000L, 13, 0) == "reallife_auc_answer_leading"
              && AuctionRules.BidWhy(a, 730000L, 5000000L, 13, BuildCatalogue.LotsMost) == "reallife_auc_answer_leading",
            "construction auction: the player's bid leads, the next is a step over, and the leader cannot bid against themselves");
        a.Leader = 3;
        Check(AuctionRules.BidWhy(a, 730000L, 5000000L, 13, BuildCatalogue.LotsMost) == "reallife_bld_answer_too_many_lots" && AuctionRules.BidWhy(a, 730000L, 5000000L, 19, 0) == "reallife_auc_answer_over",
            "construction auction: no bid past the last day or with a full set of lots to hold");
        Check(AuctionRules.MinLevel(100) == 0 && AuctionRules.MinLevel(400) == 1 && AuctionRules.MinLevel(900) == 2 && AuctionRules.MinLevel(5000) == 3, "construction auction: the bigger the lot, the higher the licence level that bids");
        bool ceiling = true;
        for (int level = 0; level < LevelRules.Levels - 1; level++)
        {
            for (int standing = 0; standing < 100; standing += 10)
            {
                for (int need = 0; need < 1000; need += 100)
                {
                    long c = AuctionRules.Ceiling(1000000L, level, standing, need);
                    ceiling &= c >= 800000L && c <= 1300000L && AuctionRules.Ceiling(1000000L, level + 1, standing, need) >= c && AuctionRules.Ceiling(1000000L, level, standing + 10, need) >= c
                               && AuctionRules.Ceiling(1000000L, level, standing, need + 100) >= c;
                }
            }
        }
        Check(ceiling, "construction auction: a builder's ceiling rises with its level, its standing and its need, between 80% and 130% of the list price");
        Check(AuctionRules.Need(0L, 4000000L) == 1000 && AuctionRules.Need(4000000L, 4000000L) == 0 && AuctionRules.Need(2000000L, 4000000L) == 500 && AuctionRules.Need(1L, 0L) == 0
              && AuctionRules.Chance(0) == 300 && AuctionRules.Chance(1000) == 633, "construction auction: need is all of it with no land held and none at the means, and a builder in need bids oftener");
        long means = LandMarketRules.Means(60, 4, 6, 1000);
        Check(AuctionRules.Worth(60, 4, 6, 1000, 0L) == means && AuctionRules.Worth(60, 4, 6, 1000, 500000L) == means + 500000L && AuctionRules.Cash(means, 100000L) == means - 100000L && AuctionRules.Cash(1000L, 5000L) == 0L,
            "construction auction: a rival developer's worth is its record's means and what it has made on land, and its cash is that less the land it holds");
        Check(AuctionRules.OfferAt(1000000L, 0) == 750000L && AuctionRules.OfferAt(1000000L, AuctionRules.OfferPoints - 1) == 1000000L && AuctionRules.OfferAt(1000000L, 99) == 1000000L,
            "construction offer: the selector runs from three quarters of the list price to the list price");
        Check(AuctionRules.OfferOdds(740000L, 1000000L) == 0 && AuctionRules.OfferOdds(750000L, 1000000L) == 0 && AuctionRules.OfferOdds(875000L, 1000000L) == 450 && AuctionRules.OfferOdds(1000000L, 1000000L) == 900
              && AuctionRules.OfferOdds(1200000L, 1000000L) == 900 && AuctionRules.OfferOdds(5L, 0L) == 0, "construction offer: no chance under three quarters of the list price, nine in ten at it");
        bool rising = true;
        for (long price = 600000L; price < 1100000L; price += 10000L)
        {
            rising &= AuctionRules.OfferOdds(price + 10000L, 1000000L) >= AuctionRules.OfferOdds(price, 1000000L);
        }
        Check(rising, "construction offer: a higher price is never less likely to be taken");
        Check(AuctionRules.OfferWhy(true, true, false, false, 0, 50, 0, 800000L, 1000000L, 9000000L, 0) == "reallife_bld_answer_session_land"
              && AuctionRules.OfferWhy(false, true, true, false, 0, 50, 0, 800000L, 1000000L, 9000000L, 0) == "reallife_auc_answer_at_auction"
              && AuctionRules.OfferWhy(false, true, false, true, 0, 50, 0, 800000L, 1000000L, 9000000L, 0) == "reallife_land_answer_waiting"
              && AuctionRules.OfferWhy(false, true, false, false, 3, 50, 0, 800000L, 1000000L, 9000000L, 0) == "reallife_auc_answer_offers_most"
              && AuctionRules.OfferWhy(false, true, false, false, 0, 50, 48, 800000L, 1000000L, 9000000L, 0) == "reallife_land_answer_rest"
              && AuctionRules.OfferWhy(false, true, false, false, 0, 50, 0, 1100000L, 1000000L, 9000000L, 0) == "reallife_land_answer_price"
              && AuctionRules.OfferWhy(false, true, false, false, 0, 50, 0, 800000L, 1000000L, 500000L, 0) == "reallife_bld_answer_no_money"
              && AuctionRules.OfferWhy(false, true, false, false, 0, 50, 0, 800000L, 1000000L, 9000000L, 0) == null, "construction offer: every reason an offer is refused, and none when it may be made");
    }

    // ---- 720 days of a seeded city: the calendar, eight builders, a player who bids now and then, the lots they win developed later and paid for in the builders' worth.

    private sealed class AuctionCity
    {
        public readonly BuildState State = new BuildState { Seed = 4242 };
        public readonly List<AuctionLot> Lots = new List<AuctionLot>(40);
        public readonly HashSet<string> Taken = new HashSet<string>();
        public readonly List<KeyValuePair<int, long>>[] Held = new List<KeyValuePair<int, long>>[BuildCatalogue.RivalCount];
        public readonly List<AuctionEvent> Events = new List<AuctionEvent>(16);
        public long PlayerCash = 80000000L, PlayerPaid;
        public int Opened, Sold, SoldToRivals, SoldToPlayer, Passed, Outbid, RivalBids, Developed, FloorBreaks, LevelBreaks, CashBreaks;
        public readonly int[] Wins = new int[BuildCatalogue.RivalCount];

        public AuctionCity()
        {
            State.Firm.Founded = 1;
            for (int r = 0; r < BuildCatalogue.RivalCount; r++)
            {
                Held[r] = new List<KeyValuePair<int, long>>();
            }
            for (int i = 0; i < 300; i++)
            {
                int area = 150 + (i * 137) % 1900;
                Lots.Add(new AuctionLot { Lot = "lot_" + i, List = 300000L + (area * 900L) + (i % 5) * 150000L, Area = area });
            }
        }

        public long HeldOf(int r)
        {
            long sum = 0L;
            for (int i = 0; i < Held[r].Count; i++)
            {
                sum += Held[r][i].Value;
            }
            return sum;
        }

        public Bidder[] Builders()
        {
            var b = new Bidder[BuildCatalogue.RivalCount];
            for (int r = 0; r < b.Length; r++)
            {
                RivalFile f = State.Rivals[r];
                long worth = AuctionRules.Worth(f.Reputation, f.Built, f.Won, 1000, f.Gained);
                b[r] = new Bidder { Level = LevelRules.Rival(f.Built, f.Reputation), Standing = f.Reputation, Cash = AuctionRules.Cash(worth, HeldOf(r)), Need = AuctionRules.Need(HeldOf(r), worth) };
            }
            return b;
        }

        public void Step(int day)
        {
            // the builders' lands built and sold: the director's GrowBuilt, here a lot held sixty days is developed and its gain taken
            for (int r = 0; r < BuildCatalogue.RivalCount; r++)
            {
                for (int i = Held[r].Count - 1; i >= 0; i--)
                {
                    if (day - Held[r][i].Key >= 60)
                    {
                        long price = Held[r][i].Value;
                        State.Rivals[r].Gained += FinishRules.Gain(price, FinishRules.Standard);
                        State.Rivals[r].Built++;
                        Held[r].RemoveAt(i);
                        Developed++;
                    }
                }
            }
            if (day % 30 == 0)
            {
                State.Rivals[day / 30 % BuildCatalogue.RivalCount].Won++;
            }
            var open = new List<AuctionLot>(Lots.Count);
            for (int i = 0; i < Lots.Count; i++)
            {
                if (!Taken.Contains(Lots[i].Lot))
                {
                    open.Add(Lots[i]);
                }
            }
            Bidder[] builders = Builders();
            // the player bids when a lot is up that they can pay for and do not lead, on every third day
            foreach (AuctionFile a in State.Auctions)
            {
                if (a.Live && day % 3 == 0 && AuctionRules.BidWhy(a, AuctionRules.NextBid(a), PlayerCash - PlayerHeld(), day, 0) == null && a.Bid < a.List)
                {
                    AuctionRules.Bid(a, AuctionRules.NextBid(a), day);
                }
            }
            Events.Clear();
            AuctionRules.Day(State, day, open, builders, true, Events);
            foreach (AuctionEvent e in Events)
            {
                switch (e.Kind)
                {
                    case AuctionEventKind.Opened:
                        Opened++;
                        break;
                    case AuctionEventKind.PlayerOutbid:
                        Outbid++;
                        RivalBids++;
                        Check(e.Rival >= 0 && e.Auction.Leader != -1, "construction auction run: a builder outbids the player and the player no longer leads");
                        break;
                    case AuctionEventKind.RivalBid:
                        RivalBids++;
                        break;
                    case AuctionEventKind.Passed:
                        Passed++;
                        break;
                    case AuctionEventKind.Sold:
                        Sold++;
                        Taken.Add(e.Auction.Lot);
                        if (e.Auction.Leader == -1)
                        {
                            if (PlayerCash - PlayerHeld() >= e.Auction.Bid)
                            {
                                SoldToPlayer++;
                                PlayerPaid += e.Auction.Bid;
                            }
                        }
                        else
                        {
                            SoldToRivals++;
                            Wins[e.Auction.Leader]++;
                            Bidder b = builders[e.Auction.Leader];
                            LevelBreaks += b.Level < AuctionRules.MinLevel(e.Auction.Area) ? 1 : 0;
                            FloorBreaks += e.Auction.Bid > AuctionRules.Ceiling(e.Auction.List, b.Level, b.Standing, b.Need) ? 1 : 0;
                            CashBreaks += e.Auction.Bid > b.Cash ? 1 : 0;
                            Held[e.Auction.Leader].Add(new KeyValuePair<int, long>(day, e.Auction.Bid));
                        }
                        Check(e.Auction.Bid >= e.Auction.Reserve, "construction auction run: no lot is knocked down under its reserve");
                        break;
                }
            }
            // a lot passed in or the player could not pay is for sale again
            foreach (AuctionFile a in State.Auctions)
            {
                if (a.State == AuctionState.Passed && a.Closed == day)
                {
                    Taken.Remove(a.Lot);
                }
            }
        }

        private long PlayerHeld()
        {
            long sum = 0L;
            foreach (AuctionFile a in State.Auctions)
            {
                sum += a.Live && a.Leader == -1 ? a.Bid : 0L;
            }
            return sum;
        }
    }

    private static void AuctionRunChecks()
    {
        var city = new AuctionCity();
        string at30 = null;
        AuctionCity twin = null;
        var opens = new List<int>();
        for (int day = 1; day <= 720; day++)
        {
            int before = city.Opened;
            city.Step(day);
            if (city.Opened > before)
            {
                opens.Add(day);
            }
            if (day == 30)
            {
                at30 = BuildStore.Write(city.State);
            }
        }
        bool calendar = opens.Count > 40;
        for (int i = 1; i < opens.Count; i++)
        {
            calendar &= (opens[i] - 12) % AuctionRules.EveryDays == 0 && opens[i] > opens[i - 1];
        }
        Check(calendar && opens[0] == 12, "construction auction run: lots go under the hammer on the fortnightly calendar from day 12 (" + opens.Count + " openings in 720 days)");
        Check(city.Opened >= 40 && city.Sold > 20 && city.Passed >= 0, "construction auction run: over 720 days lots are put up (" + city.Opened + ") and sold (" + city.Sold + "), passed in " + city.Passed);
        Check(city.RivalBids > 100 && city.SoldToRivals > 5, "construction auction run: the city's builders bid (" + city.RivalBids + " bids) and take lots (" + city.SoldToRivals + ")");
        Check(city.SoldToPlayer > 0 && city.Outbid > 0, "construction auction run: the player's bids win lots (" + city.SoldToPlayer + ") and are outbid by builders (" + city.Outbid + ")");
        Check(city.FloorBreaks == 0 && city.LevelBreaks == 0 && city.CashBreaks == 0, "construction auction run: no builder wins above its ceiling, below its licence level for the lot, or beyond its cash");
        int winners = 0;
        for (int r = 0; r < BuildCatalogue.RivalCount; r++)
        {
            winners += city.Wins[r] > 0 ? 1 : 0;
        }
        Check(winners >= 3, "construction auction run: more than one builder takes lots (" + winners + " of 8)");
        long gainedAll = 0L;
        bool worthOk = true;
        for (int r = 0; r < BuildCatalogue.RivalCount; r++)
        {
            RivalFile f = city.State.Rivals[r];
            gainedAll += f.Gained;
            worthOk &= AuctionRules.Worth(f.Reputation, f.Built, f.Won, 1000, f.Gained) == LandMarketRules.Means(f.Reputation, f.Built, f.Won, 1000) + f.Gained && f.Gained >= 0L;
        }
        Check(city.Developed > 5 && gainedAll > 0L && worthOk, "construction auction run: the builders develop the land they won (" + city.Developed + ") and their worth follows what they made (" + gainedAll + ")");
        Check(city.State.Auctions.Count <= AuctionRules.KeptMost, "construction auction run: the auctions kept stay within " + AuctionRules.KeptMost);
        // the same seed and the same days give the same state
        twin = new AuctionCity();
        string at30Twin = null;
        for (int day = 1; day <= 720; day++)
        {
            twin.Step(day);
            if (day == 30)
            {
                at30Twin = BuildStore.Write(twin.State);
            }
        }
        Check(BuildStore.Write(twin.State) == BuildStore.Write(city.State) && at30 == at30Twin, "construction auction run: the same seed and days give the same state on any machine");
        // a save on day 30 read back and played on gives the same next days as the one that never stopped
        var resumed = new AuctionCity();
        BuildState back = BuildStore.Read(at30, out _);
        Check(back != null && BuildStore.Write(back) == at30, "construction auction run: the day-30 line reads back and writes as it was");
        if (back != null)
        {
            var copy = new AuctionCity();
            CopyInto(copy, back, city, 30);
            for (int day = 31; day <= 60; day++)
            {
                copy.Step(day);
            }
            var straight = new AuctionCity();
            for (int day = 1; day <= 60; day++)
            {
                straight.Step(day);
            }
            Check(BuildStore.Write(copy.State) == BuildStore.Write(straight.State), "construction auction run: a save written on day 30 and read back plays the next 30 days as the one that never stopped");
        }
        Check(resumed.State.Auctions.Count == 0, "construction auction run: a fresh state has no auctions");
    }

    // The resumed city: the state read from the line, the builders' lands and the rest as the straight run had them on that day (rebuilt by playing the same 30 days).
    private static void CopyInto(AuctionCity into, BuildState saved, AuctionCity like, int day)
    {
        var replay = new AuctionCity();
        for (int d = 1; d <= day; d++)
        {
            replay.Step(d);
        }
        into.State.Auctions.Clear();
        into.State.CityOffers.Clear();
        foreach (AuctionFile a in saved.Auctions)
        {
            into.State.Auctions.Add(a);
        }
        for (int r = 0; r < BuildCatalogue.RivalCount; r++)
        {
            into.State.Rivals[r].Reputation = saved.Rivals[r].Reputation;
            into.State.Rivals[r].Built = saved.Rivals[r].Built;
            into.State.Rivals[r].Won = saved.Rivals[r].Won;
            into.State.Rivals[r].Gained = saved.Rivals[r].Gained;
            into.Held[r].AddRange(replay.Held[r]);
        }
        into.State.NextAuctionDay = saved.NextAuctionDay;
        into.State.NextId = saved.NextId;
        foreach (string lot in replay.Taken)
        {
            into.Taken.Add(lot);
        }
        into.PlayerCash = replay.PlayerCash;
    }

    private static void NewStoreChecks()
    {
        var s = new BuildState { Seed = 99 };
        s.Firm.Founded = 5;
        var p = new ProjectFile { Id = 7, Kind = ProjectKind.Plot, Stage = ProjectStage.Ready, Finish = 3, FinishBy = 40, FinishFixed = true, FinishRented = true, Variance = VarianceState.Granted,
            VarianceDay = 30, VarianceFloors = 14, VarianceAyes = 3, VarianceNoes = 2, Storeys = 14, Area = 900, Hours = 120, Use = BuildUse.Offices };
        s.Projects.Add(p);
        s.NextId = 20;
        s.Rivals[2].Gained = 1234567L;
        s.Rivals[2].VarianceAsked = 3;
        s.Rivals[2].VarianceGranted = 2;
        s.NextAuctionDay = 26;
        s.Auctions.Add(new AuctionFile { Id = 11, Lot = "lot_9", Opened = 12, Ends = 18, Area = 500, List = 900000L, Opening = 630000L, Reserve = 765000L, Bid = 660000L, Leader = 4, Bids = 2, Mine = 630000L,
            Round = 14, HallId = 3, Hall = "Ada Quill", LogCount = 2 });
        s.Auctions[0].LogWho[0] = -1;
        s.Auctions[0].LogBid[0] = 630000L;
        s.Auctions[0].LogDay[0] = 13;
        s.Auctions[0].LogWho[1] = 4;
        s.Auctions[0].LogBid[1] = 660000L;
        s.Auctions[0].LogDay[1] = 14;
        s.CityOffers.Add(new CityOfferFile { Id = 12, Lot = "lot_3", Price = 700000L, List = 900000L, Made = 20, Due = 21, State = CityOfferState.Waiting });
        string line = BuildStore.Write(s);
        BuildState back = BuildStore.Read(line, out _);
        ProjectFile q = back != null ? back.Project(7) : null;
        Check(back != null && q != null && q.Finish == 3 && q.FinishBy == 40 && q.FinishFixed && q.FinishRented && q.Variance == VarianceState.Granted && q.VarianceDay == 30 && q.VarianceFloors == 14
              && q.VarianceAyes == 3 && q.VarianceNoes == 2, "construction save: a project's finish and variance are written and read back");
        Check(back != null && back.Rivals[2].Gained == 1234567L && back.Rivals[2].VarianceAsked == 3 && back.Rivals[2].VarianceGranted == 2 && back.Rivals[3].Gained == 0L,
            "construction save: a rival developer's money and variances are written and read back");
        AuctionFile a = back != null ? back.Auction(11) : null;
        Check(a != null && a.Lot == "lot_9" && a.Leader == 4 && a.Bid == 660000L && a.Mine == 630000L && a.Hall == "Ada Quill" && a.HallId == 3 && a.LogCount == 2 && a.LogWho[0] == -1 && a.LogBid[1] == 660000L
              && a.Round == 14 && back.NextAuctionDay == 26, "construction save: an auction, its log and the calendar's next day are written and read back");
        CityOfferFile o = back != null ? back.CityOffer(12) : null;
        Check(o != null && o.Price == 700000L && o.List == 900000L && o.Lot == "lot_3" && o.Waiting, "construction save: the player's offer for a city lot is written and read back");
        Check(back != null && BuildStore.Write(back) == line, "construction save: a line read and written again is the same");
        var plain = new BuildState { Seed = 99 };
        plain.Firm.Founded = 5;
        plain.Projects.Add(new ProjectFile { Id = 1, Kind = ProjectKind.Plot, Stage = ProjectStage.Design });
        string old = BuildStore.Write(plain);
        BuildState oldBack = BuildStore.Read(old, out _);
        Check(old.IndexOf("^n:", StringComparison.Ordinal) < 0 && old.IndexOf("^e:", StringComparison.Ordinal) < 0 && old.IndexOf("^i:", StringComparison.Ordinal) < 0 && oldBack != null
              && oldBack.Project(1).Finish == 0 && !oldBack.Project(1).FinishFixed && oldBack.Project(1).Variance == VarianceState.None && oldBack.Auctions.Count == 0 && oldBack.Rivals[0].Gained == 0L,
            "construction save: a line from before the parts has none of them and loads with no finish chosen, no variance, no auctions");
        BuildState passed = BuildStore.Read(line + "^v:whatever", out _);
        Check(passed != null && passed.Auctions.Count == 1, "construction save: a part of another version's after them is passed over");
    }

    // The wiring, read from the sources: each rule has its caller on the day's pass or a press, the player and the city's builders share the one hearing and the one finish rule, and
    // nothing runs a frame.
    private static void NewSourceChecks()
    {
        string kit = ModSource(@"Construction\ConstructionDirector.Kit.cs"), finish = ModSource(@"Construction\ConstructionDirector.Finish.cs"), variance = ModSource(@"Construction\ConstructionDirector.Variance.cs");
        string auction = ModSource(@"Construction\ConstructionDirector.Auction.cs"), projects = ModSource(@"Construction\ConstructionDirector.Projects.cs");
        string liveFinish = ModSource(@"Construction\Live\ConstructionLive.Finish.cs"), liveAuction = ModSource(@"Construction\Live\ConstructionLive.Auction.cs");
        Check(kit.Contains("FinishDay(day);") && kit.Contains("VarianceDayPass(day);") && kit.Contains("AuctionDay(day);"), "construction wiring: the day's pass runs the finish window, the variance sittings and the auctions");
        Check(liveFinish.Contains("Director.SetFinish(p, i)") && liveFinish.Contains("Director.AskVariance(p)") && liveAuction.Contains("Director.Bid(id, amount)") && liveAuction.Contains("Director.OfferCity(spaceId, price)"),
            "construction wiring: the finish step, Ask the council, Place bid and Make the offer each reach their director call");
        Check(ModSource(@"Construction\ExposureRules.cs").Contains("FinishRules.CostOf(p.Finish)") && projects.Contains("FinishRules.CostOf(p.Finish)") && projects.Contains("FinishRules.Hours(hours"),
            "construction wiring: the finish changes the materials the site asks for, the estimate and the site's working hours");
        Check(projects.Contains("int limit = LimitFor(p);") && projects.Contains("p.Variance == VarianceState.Pending"), "construction wiring: the planning decision holds the plans to the floors a variance allowed, and no application waits on a pending one");
        Check(variance.Contains("VarianceHearing(VarianceRules.Matter(place, p.VarianceFloors)") && variance.Contains("VarianceHearing(matter, 0, r.Use") && variance.Contains("CouncilVote(matter"),
            "construction wiring: the player's variance and a city builder's are heard by the one method, which asks the mod's one council");
        Check(ModSource(@"Construction\ConstructionDirector.CityRedevelop.cs").Contains("CityVariance(r, UptownStoreys(), day);"), "construction wiring: a city builder extending to the top of the code may ask the council for a floor more");
        Check(ModSource(@"Construction\ConstructionDirector.Grow.cs").Contains("g.Tier = BuilderFinish(g.Rival") && ModSource(@"Construction\ConstructionDirector.Grow.cs").Contains("FinishRules.Gain(g.Price"),
            "construction wiring: a city builder picks its finish by its means and clears that finish's gain when it has built");
        Check(auction.Contains("Open(TabLand, pick)") && auction.Contains("AuctionPickBase + a.Id") && ModSource(@"Construction\Live\ConstructionLive.cs").Contains("\"auc:\" + (_wantPick - ConstructionDirector.AuctionPickBase)"),
            "construction wiring: an auction's notices open the Land tab on its row");
        Check(ModSource(@"Construction\AuctionRules.cs").Contains("PeoplePool.For(") && !ModSource(@"Construction\AuctionRules.cs").Contains("FirstNames") && !auction.Contains("FirstNames"), "construction wiring: the auctioneer is a person of the shared people pool and no new name list is made");
        Check(ModSource(@"Construction\ConstructionDirector.Actions.cs").Contains("|| AuctionOn(lot)"), "construction wiring: a space up for auction is not sold outright and the developers leave it alone");
        Check(ModSource(@"Construction\ConstructionDirector.Market.cs").Contains("RivalMarginPermille[BuildCatalogue.Clamp(won.Bidder") && ModSource(@"Construction\ConstructionDirector.LandMarket.cs").Contains("].Gained += o.Price - g.Price;"),
            "construction wiring: a rival developer's worth grows with the works it wins and the land it sells at a profit");
        bool frame = true;
        foreach (string text in new[] { finish, variance, auction, liveFinish, liveAuction, ModSource(@"Construction\FinishRules.cs"), ModSource(@"Construction\VarianceRules.cs"), ModSource(@"Construction\AuctionRules.cs") })
        {
            frame &= !text.Contains("void Update(") && !text.Contains("LateUpdate") && !text.Contains("OnGUI");
        }
        Check(frame, "construction wiring: nothing of the new features runs a frame");
        Check(ModSource(@"Construction\ConstructionDirector.Auction.cs").Contains("GoingPublic.InSession") && ModSource(@"Construction\AuctionRules.cs").Contains("inSession"), "construction wiring: no land is auctioned or offered for in a Going Public session");
    }
}
