using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;

namespace RealLifeOverhaul.Construction
{
    /// <summary>A city lot that may be put under the hammer: its id, list price and floor area.</summary>
    public struct AuctionLot
    {
        public string Lot;
        public long List;
        public int Area;
    }

    /// <summary>What one of the city's eight builders brings to a bid: its licence level, standing, the cash it can put into land and how much it needs land.</summary>
    public struct Bidder
    {
        public int Level, Standing, Need;
        public long Cash;
    }

    /// <summary>What happened in a day's pass of the auctions, for the director to settle and tell.</summary>
    public enum AuctionEventKind
    {
        Opened = 0,
        PlayerOutbid = 1,
        RivalBid = 2,
        Sold = 3,       // knocked down to the leading bid (the player's or a builder's): the director settles the money and the land
        Passed = 4
    }

    /// <summary>One thing that happened in a day's pass of the auctions: its kind, the auction and the builder it is about.</summary>
    public struct AuctionEvent
    {
        public AuctionEventKind Kind;
        public AuctionFile Auction;
        public int Rival;
    }

    /// <summary>
    /// Land auctions and offers on city lots, game-free. The city puts open lots under the hammer on a calendar (every fortnight, a few at a time, for six days);
    /// the city's eight builders bid on those whose size their licence level allows, from the cash they have (their record's means and what they have made on land, less
    /// the land they hold), up to a ceiling that rises with their level and standing and with how much they need land; the player bids from the Land tab. A bid is a step over
    /// the leader's, the hammer falls on the sixth day, and a price under the reserve is passed in. An offer on a lot not at auction is answered the next working day
    /// by a seeded chance that rises from nothing at three quarters of the list price to nine in ten at the list price and over. Every draw is from the save's seed and
    /// what it is about, so the same on every machine. COST: nothing per frame; a day's pass is a few multiplications for each auction open.
    /// </summary>
    public static class AuctionRules
    {
        public const int FirstDay = 12, EveryDays = 14, RunDays = 6, OpenMost = 3, KeptMost = 16;
        public const int OpeningPermille = 700, ReservePermille = 850, StepPermille = 30, BidMostPermille = 2000;
        public const int DrawsADay = 2;                 // builders asked to bid on each auction each day
        public const int AgainDays = 28;                // days a lot passed in rests before it is put up again
        public const int OffersKept = 20, OffersWaitingMost = 3, OfferGapDays = 4;
        public const int OfferFloorPermille = 750, OfferSureAbove = 1000;   // under the floor never; at the list price and over, nine in ten
        public const int OfferPoints = 6;               // prices the offer's selector steps through

        // ---- the calendar

        /// <summary>The first day of the calendar after a day: the first day (FirstDay) and every fortnight on from it.</summary>
        public static int NextOpen(int day)
        {
            if (day < FirstDay)
            {
                return FirstDay;
            }
            return FirstDay + ((day - FirstDay) / EveryDays + 1) * EveryDays;
        }

        /// <summary>The bid asked of a lot to begin and the least price that sells it, in whole thousands.</summary>
        public static long OpeningOf(long list) => Round(list * OpeningPermille / 1000L);

        public static long ReserveOf(long list) => Round(list * ReservePermille / 1000L);

        /// <summary>The step a bid must be over the leading one.</summary>
        public static long Step(long list) => Math.Max(BuildCatalogue.LandRound, Round(list * StepPermille / 1000L));

        /// <summary>The next bid on an auction: the opening one while nobody has bid, else a step over the leader's.</summary>
        public static long NextBid(AuctionFile a) => a.Leader == -2 ? a.Opening : a.Bid + Step(a.List);

        /// <summary>The most a bid may be: a bid is a step over the leader's, not a wild guess.</summary>
        public static long BidMost(long list) => Round(Math.Max(1L, list) * BidMostPermille / 1000L);

        private static long Round(long money)
        {
            long unit = BuildCatalogue.LandRound;
            return Math.Max(unit, Math.Max(0L, money) / unit * unit);
        }

        // ---- the city's builders

        /// <summary>The least licence level that bids on a lot of so many square metres: the bigger the lot, the taller what a builder would put on it.</summary>
        public static int MinLevel(int area) => area < 300 ? 0 : area < 600 ? 1 : area < 1200 ? 2 : 3;

        /// <summary>How much a builder needs land, 0 to 1000: all of it with none held, none when it holds as much as its means.</summary>
        public static int Need(long held, long means)
        {
            if (means <= 0L)
            {
                return 0;
            }
            return (int)Math.Max(0L, Math.Min(1000L, 1000L - Math.Max(0L, held) * 1000L / means));
        }

        /// <summary>A rival developer's worth: what its record gives it (LandMarketRules.Means) and what it has made on land since.</summary>
        public static long Worth(int reputation, int built, int won, int prices, long gained) => LandMarketRules.Means(reputation, built, won, prices) + gained;

        /// <summary>The cash a builder can put into land now: its worth less the land it holds at what it paid; 0 where it is short.</summary>
        public static long Cash(long worth, long held) => Math.Max(0L, worth - Math.Max(0L, held));

        /// <summary>The most a builder will bid for a lot: its list price times a share that rises with licence level, standing and need.</summary>
        public static long Ceiling(long list, int level, int standing, int need)
        {
            int share = 880 + BuildCatalogue.Clamp(level, 0, LevelRules.Levels - 1) * 40 + (BuildCatalogue.Clamp(standing, 0, 100) - 50) * 2 + BuildCatalogue.Clamp(need, 0, 1000) / 10;
            return Round(Math.Max(0L, list) * BuildCatalogue.Clamp(share, 800, 1300) / 1000L);
        }

        /// <summary>The chance, in thousandths, that a builder asked on a day bids: more the more it needs land.</summary>
        public static int Chance(int need) => 300 + BuildCatalogue.Clamp(need, 0, 1000) / 3;

        /// <summary>True when a builder would bid this much on this auction: its licence covers the lot, its cash and its ceiling reach the bid.</summary>
        public static bool Wants(AuctionFile a, Bidder b, long bid, long committed)
        {
            return b.Level >= MinLevel(a.Area) && bid <= Ceiling(a.List, b.Level, b.Standing, b.Need) && bid <= b.Cash - committed;
        }

        // ---- the player's bid

        /// <summary>Why the player cannot bid this much now (a text key), or null.</summary>
        public static string BidWhy(AuctionFile a, long amount, long cash, int day, int lots)
        {
            if (a == null || !a.Live || day > a.Ends)
            {
                return "reallife_auc_answer_over";
            }
            if (a.Leader == -1)
            {
                return "reallife_auc_answer_leading";
            }
            if (lots >= BuildCatalogue.LotsMost)
            {
                return "reallife_bld_answer_too_many_lots";
            }
            if (amount < NextBid(a) || amount > BidMost(a.List))
            {
                return "reallife_auc_answer_bid_range";
            }
            return cash < amount ? "reallife_bld_answer_no_money" : null;
        }

        /// <summary>The player's bid taken: they lead at the amount.</summary>
        public static void Bid(AuctionFile a, long amount, int day)
        {
            a.Bid = amount;
            a.Leader = -1;
            a.Mine = Math.Max(a.Mine, amount);
            a.Bids++;
            a.Note(-1, amount, day);
        }

        /// <summary>The bid the selector offers at a step: the next bid and then a step more each.</summary>
        public static long BidAt(AuctionFile a, int step) => NextBid(a) + Math.Max(0, step) * Step(a.List);

        // ---- a day

        /// <summary>
        /// One day of the calendar and the rooms: lots put up when the calendar's day has come (from the candidates, in a seeded order, never one already up or passed
        /// in lately), the builders' bids on each auction open for each day since it was last run, and the hammer on those whose day has come. Events are added to
        /// <paramref name="events"/> for the director. <paramref name="builders"/> are the city's eight, <paramref name="committed"/> a scratch array of the same size.
        /// </summary>
        public static void Day(BuildState s, int day, IList<AuctionLot> candidates, Bidder[] builders, bool opening, List<AuctionEvent> events)
        {
            if (opening)
            {
                OpenDay(s, day, candidates, events);
            }
            for (int i = 0; i < s.Auctions.Count; i++)
            {
                AuctionFile a = s.Auctions[i];
                if (!a.Live)
                {
                    continue;
                }
                for (int d = Math.Max(a.Opened, a.Round) + 1; d <= Math.Min(day, a.Ends); d++)
                {
                    Room(s, a, d, builders, events);
                }
                a.Round = Math.Max(a.Round, Math.Min(day, a.Ends));
                if (day >= a.Ends)
                {
                    Hammer(a, day, events);
                }
            }
            s.TidyAuctions();
        }

        private static void OpenDay(BuildState s, int day, IList<AuctionLot> candidates, List<AuctionEvent> events)
        {
            if (s.NextAuctionDay <= 0)
            {
                s.NextAuctionDay = NextOpen(day - 1);   // the calendar's first look: the first date at or after today
            }
            if (day < s.NextAuctionDay)
            {
                return;
            }
            s.NextAuctionDay = NextOpen(day);
            int room = OpenMost;
            for (int i = 0; i < s.Auctions.Count; i++)
            {
                room -= s.Auctions[i].Live ? 1 : 0;
            }
            for (int k = 0; k < room && candidates != null && candidates.Count > 0; k++)
            {
                int best = -1;
                int bestKey = int.MaxValue;
                for (int c = 0; c < candidates.Count; c++)
                {
                    AuctionLot lot = candidates[c];
                    AuctionFile last = s.LastAuction(lot.Lot);
                    if (lot.List <= 0L || s.AuctionOn(lot.Lot) != null || (last != null && !last.Live && day - last.Closed < AgainDays))
                    {
                        continue;
                    }
                    int key = BuildRandom.Mix(BuildRandom.Mix(s.Seed, day), BuildRandom.Hash(lot.Lot)) & 0x7FFFFFFF;
                    if (key < bestKey)
                    {
                        bestKey = key;
                        best = c;
                    }
                }
                if (best < 0)
                {
                    return;
                }
                AuctionLot pick = candidates[best];
                int id = s.NewId();
                PoolPerson hall = PeoplePool.For(BuildRandom.Mix(s.Seed, id) & 0x7FFFFFFFL);
                var a = new AuctionFile
                {
                    Id = id, Lot = pick.Lot, Opened = day, Ends = day + RunDays, Area = pick.Area, List = pick.List, Opening = OpeningOf(pick.List), Reserve = ReserveOf(pick.List),
                    Round = day, HallId = hall.Id, Hall = hall.Name ?? string.Empty
                };
                s.Auctions.Add(a);
                events.Add(new AuctionEvent { Kind = AuctionEventKind.Opened, Auction = a, Rival = -1 });
            }
        }

        // One day of the room on one auction: builders asked in a seeded order bid a step over the leader if their licence, cash and ceiling allow and the draw says so.
        private static void Room(BuildState s, AuctionFile a, int day, Bidder[] builders, List<AuctionEvent> events)
        {
            for (int k = 0; k < DrawsADay; k++)
            {
                BuildRandom draw = BuildRandom.Of(s.Seed, a.Id * 64 + day, 0x4A01 + k);
                int r = draw.Range(0, builders.Length);
                if (r == a.Leader)
                {
                    continue;
                }
                Bidder b = builders[r];
                if (!draw.Chance(Chance(b.Need)))
                {
                    continue;
                }
                long bid = NextBid(a);
                if (!Wants(a, b, bid, Committed(s, r, a)))
                {
                    continue;
                }
                bool outbid = a.Leader == -1;
                a.Bid = bid;
                a.Leader = r;
                a.Bids++;
                a.Note(r, bid, day);
                events.Add(new AuctionEvent { Kind = outbid ? AuctionEventKind.PlayerOutbid : AuctionEventKind.RivalBid, Auction = a, Rival = r });
            }
        }

        // What a builder already leads with elsewhere: the cash it cannot bid twice.
        private static long Committed(BuildState s, int rival, AuctionFile except)
        {
            long sum = 0L;
            for (int i = 0; i < s.Auctions.Count; i++)
            {
                AuctionFile a = s.Auctions[i];
                sum += a.Live && !ReferenceEquals(a, except) && a.Leader == rival ? a.Bid : 0L;
            }
            return sum;
        }

        private static void Hammer(AuctionFile a, int day, List<AuctionEvent> events)
        {
            a.Closed = day;
            if (a.Leader != -2 && a.Bid >= a.Reserve)
            {
                a.State = AuctionState.Sold;
                events.Add(new AuctionEvent { Kind = AuctionEventKind.Sold, Auction = a, Rival = a.Leader });
            }
            else
            {
                a.State = AuctionState.Passed;
                events.Add(new AuctionEvent { Kind = AuctionEventKind.Passed, Auction = a, Rival = -1 });
            }
        }

        // ---- offers on city lots

        /// <summary>The prices the offer's selector steps through: from three quarters of the list price to the list price, whole thousands, ascending.</summary>
        public static long OfferAt(long list, int step)
        {
            int n = BuildCatalogue.Clamp(step, 0, OfferPoints - 1);
            long from = list * OfferFloorPermille / 1000L, to = list;
            return Round(from + (to - from) * n / (OfferPoints - 1));
        }

        /// <summary>The chance, in thousandths, that the city takes an offer: none under three quarters of the list price, rising to 900 at the list price.</summary>
        public static int OfferOdds(long price, long list)
        {
            if (list <= 0L || price * 1000L < list * OfferFloorPermille)
            {
                return 0;
            }
            long over = Math.Min(price * 1000L / list, OfferSureAbove) - OfferFloorPermille;
            return (int)(over * 900L / (OfferSureAbove - OfferFloorPermille));
        }

        /// <summary>The city's answer to an offer, drawn from the save's seed and the offer's number.</summary>
        public static bool Takes(int seed, int offer, long price, long list) => BuildRandom.Of(seed, offer, 0x4F11).Range(0, 1000) < OfferOdds(price, list);

        /// <summary>Why the player cannot offer this price for a lot now (a text key), or null. Never at an auction, one waiting offer a lot and a few in all, a rest after a refusal.</summary>
        public static string OfferWhy(bool inSession, bool found, bool atAuction, bool waiting, int waitingAll, int day, int lastDay, long price, long list, long cash, int lots)
        {
            if (inSession)
            {
                return "reallife_bld_answer_session_land";
            }
            if (!found || list <= 0L)
            {
                return "reallife_bld_answer_no_lot";
            }
            if (atAuction)
            {
                return "reallife_auc_answer_at_auction";
            }
            if (waiting)
            {
                return "reallife_land_answer_waiting";
            }
            if (waitingAll >= OffersWaitingMost)
            {
                return "reallife_auc_answer_offers_most";
            }
            if (lots >= BuildCatalogue.LotsMost)
            {
                return "reallife_bld_answer_too_many_lots";
            }
            if (lastDay > 0 && day - lastDay < OfferGapDays)
            {
                return "reallife_land_answer_rest";
            }
            if (price <= 0L || price > list)
            {
                return "reallife_land_answer_price";
            }
            return cash < price ? "reallife_bld_answer_no_money" : null;
        }

        /// <summary>The day the city answers an offer made on a day.</summary>
        public static int AnswerDay(int day) => LandMarketRules.AnswerDay(day);
    }
}
