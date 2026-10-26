using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>One bid on a tender: who (-1 the player, else a rival of the city's contractors), the price, the days and the client's score.</summary>
    public struct TenderBid
    {
        public int Bidder;
        public long Price;
        public int Days;
        public int Score;   // lower is better
    }

    /// <summary>
    /// Tenders: what the city pays for its works by the hour, a client's and a public building's price, the rival builders'
    /// bids (their margin, how busy they are, whether the work is their speciality, a little of their own luck), the client's
    /// score of a bid (price, time and the builder's reputation) and the award. Seeded by the tender, the same on every
    /// machine. Game-free.
    /// </summary>
    public static class TenderRules
    {
        /// <summary>What the city pays for one of its sites: its working hours at the programme's rate, at today's prices.</summary>
        public static long WorksValue(int civicKind, int hours, int prices)
        {
            int k = BuildCatalogue.Clamp(civicKind, 0, BuildCatalogue.WorksRate.Length - 1);
            return (long)BuildCatalogue.WorksRate[k] * Math.Max(1, hours) * Math.Max(1, prices) / 1000L;
        }

        /// <summary>What a client or the city pays for a building it orders: its cost and a margin.</summary>
        public static long BuildingValue(long estimate)
        {
            long value = Math.Max(0L, estimate) * (1000L + BuildCatalogue.PublicMarginPermille) / 1000L;
            return value / 1000L * 1000L;
        }

        /// <summary>The working days a job of so many hours takes at the usual pace.</summary>
        public static int StandardDays(int hours)
        {
            return Math.Max(1, (hours + 9) / 10);
        }

        /// <summary>The speciality a tender's work is: roads for roadworks and the utilities, renovation for scaffolding, refits and rebuilds, by use for a new building.</summary>
        public static Speciality NeedOf(TenderSource source, int civicKind, BuildUse use)
        {
            if (source == TenderSource.Public || source == TenderSource.Landmark)
            {
                return use == BuildUse.Homes ? Speciality.Homes : Speciality.Offices;
            }
            switch (civicKind)
            {
                case 0:
                case 3:
                    return Speciality.Roads;
                case 5:
                    return use == BuildUse.Homes ? Speciality.Homes : Speciality.Offices;
                case RedevelopRules.DemolitionWorks:
                    return Speciality.Demolition;
                default:
                    return Speciality.Renovation;
            }
        }

        /// <summary>
        /// A rival's bid: the value with their margin, more when busy (sites going), less on their speciality, and their own
        /// luck of a few per cent; their days a little either way of the usual.
        /// </summary>
        public static TenderBid RivalBid(int seed, int tender, int rival, long value, int days, Speciality need, int busy)
        {
            int r = BuildCatalogue.Clamp(rival, 0, BuildCatalogue.RivalCount - 1);
            BuildRandom random = BuildRandom.Of(seed, tender, 83 + r);
            long permille = 920 + BuildCatalogue.RivalMarginPermille[r] + Math.Min(4, Math.Max(0, busy)) * 15 + random.Range(-30, 31);
            bool special = BuildCatalogue.RivalSpeciality[r] == need;
            permille = permille * (special ? 960 : 1030) / 1000;
            int d = Math.Max(1, days * random.Range(900, 1151) / 1000);
            long price = value * permille / 1000L;
            return new TenderBid { Bidder = r, Price = price, Days = d, Score = Score(price, value, d, days, BuildCatalogue.RivalReputation[r], special) };
        }

        /// <summary>The player's bid at a share of the value and the usual or a fast time.</summary>
        public static TenderBid PlayerBid(long value, int pricePermille, bool fast, int days, int reputation, bool special)
        {
            int p = BuildCatalogue.Clamp(pricePermille, BuildCatalogue.BidLeastPermille, BuildCatalogue.BidMostPermille);
            long price = value * p / 1000L;
            int d = Math.Max(1, fast ? days * BuildCatalogue.FastPermille / 1000 : days);
            return new TenderBid { Bidder = -1, Price = price, Days = d, Score = Score(price, value, d, days, reputation, special) };
        }

        /// <summary>
        /// The client's score of a bid, lower better: the price against the value most, the time against the usual, and how
        /// little the builder is thought of; a speciality's work scores a little better.
        /// </summary>
        public static int Score(long price, long value, int days, int standardDays, int reputation, bool special)
        {
            long priceShare = value > 0 ? price * 1000L / value : 1000L;
            long timeShare = standardDays > 0 ? days * 1000L / standardDays : 1000L;
            long standing = (100 - BuildCatalogue.Clamp(reputation, 0, 100)) * 10L;
            long score = (priceShare * BuildCatalogue.ScorePricePermille + timeShare * BuildCatalogue.ScoreTimePermille + standing * BuildCatalogue.ScoreRepPermille) / 1000L;
            if (special)
            {
                score = score * (1000L - BuildCatalogue.SpecialBidPermille) / 1000L;
            }
            return (int)Math.Min(int.MaxValue, score);
        }

        /// <summary>The winning bid of those given: the lowest score, then the lowest price, then the player before a rival, then the first rival.</summary>
        public static int Winner(TenderBid[] bids, int count)
        {
            int best = -1;
            for (int i = 0; bids != null && i < count && i < bids.Length; i++)
            {
                if (best < 0)
                {
                    best = i;
                    continue;
                }
                TenderBid a = bids[i], b = bids[best];
                if (a.Score < b.Score || (a.Score == b.Score && (a.Price < b.Price || (a.Price == b.Price && a.Bidder < b.Bidder))))
                {
                    best = i;
                }
            }
            return best;
        }

        /// <summary>The days until the next client's, public building's or business's refit tender, from the save's seed and the day.</summary>
        public static int NextIn(int seed, int day, TenderSource source)
        {
            BuildRandom random = BuildRandom.Of(seed, day, 89 + (int)source);
            switch (source)
            {
                case TenderSource.Public:
                    return random.Range(BuildCatalogue.PublicEveryLeast, BuildCatalogue.PublicEveryMost + 1);
                case TenderSource.Refit:
                    return random.Range(BuildCatalogue.RefitEveryLeast, BuildCatalogue.RefitEveryMost + 1);
                default:
                    return random.Range(BuildCatalogue.ClientEveryLeast, BuildCatalogue.ClientEveryMost + 1);
            }
        }
    }
}
