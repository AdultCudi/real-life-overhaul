using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Rigged tenders, game-free: now and then three of the city's builders agree which of them is to win a tender (the
    /// leader, who bids high) and the others put in cover bids higher still; the player's company may be asked to cover too,
    /// for a cut of the winning price. The city's inquiry can find a ring out in the weeks after the award, at once when it is
    /// reported. Every draw is seeded by the save's seed, the tender and the day, so a ring is the same however often asked.
    /// </summary>
    public static class RingRules
    {
        public const int Members = 3;                   // builders in a ring: the leader and two who cover
        public const int CityWorksPermille = 90;        // a ring on one of the city's works, of the tenders
        public const int OtherPermille = 140;           // ...on a business's or a public building's
        public const int ApproachPermille = 650;        // the player's licensed company is asked to cover, of the rings
        public const int ApproachLeastReputation = 20;  // ...when its standing is at least this
        public const int LeaderMarkupPermille = 100;    // more the leader asks than its own price
        public const int CoverLeastPermille = 1180, CoverMostPermille = 1260;   // of the value: a cover bid
        public const int PlayerCoverPermille = 1250;    // the cover bid the player is asked for (a step the app's bids take)
        public const int CutPermille = 25;              // of the winning price: the player's cut for covering
        public const int FoundPermille = 25;            // a day, the inquiry finds a ring out...
        public const int FoundDays = 45;                // ...for this many days after the award
        public const int RepFined = 10;                 // a builder's standing lost when its ring is found out
        public const int RepReported = 3;               // the player's company, for reporting a ring
        public const int RepCaught = 15;                // ...for being found in one
        public const int StandingReported = 2, StandingCaught = -6;

        /// <summary>True when a ring forms on a tender: the save's seed and the tender's own number, a little likelier off the city's own works.</summary>
        public static bool Forms(int seed, int tender, TenderSource source)
        {
            BuildRandom random = BuildRandom.Of(seed, tender, 307);
            return random.Chance(source == TenderSource.CityWorks ? CityWorksPermille : OtherPermille);
        }

        /// <summary>The ring's members (a bit for each rival) and its leader, three different builders.</summary>
        public static int Ring(int seed, int tender, out int leader)
        {
            BuildRandom random = BuildRandom.Of(seed, tender, 311);
            leader = random.Range(0, BuildCatalogue.RivalCount);
            int members = 1 << leader, count = 1;
            for (int guard = 0; count < Members && guard < 64; guard++)
            {
                int r = random.Range(0, BuildCatalogue.RivalCount);
                if ((members & (1 << r)) == 0)
                {
                    members |= 1 << r;
                    count++;
                }
            }
            return members;
        }

        /// <summary>True when the ring asks the player's company to cover: licensed, and of some standing.</summary>
        public static bool Approach(int seed, int tender, bool licensed, int reputation)
        {
            BuildRandom random = BuildRandom.Of(seed, tender, 313);
            return licensed && reputation >= ApproachLeastReputation && random.Chance(ApproachPermille);
        }

        public static bool Member(int members, int rival) => rival >= 0 && rival < 31 && (members & (1 << rival)) != 0;

        /// <summary>
        /// A ring's bid in place of a member's own: the leader's price marked up, a cover a high share of the value, each
        /// scored again as the client scores every bid. The leader's days stay its own.
        /// </summary>
        public static TenderBid Rigged(TenderBid own, int seed, int tender, long value, int standardDays, bool leader, bool special)
        {
            long price;
            if (leader)
            {
                price = own.Price * (1000L + LeaderMarkupPermille) / 1000L;
            }
            else
            {
                BuildRandom random = BuildRandom.Of(seed, tender, 317 + own.Bidder);
                price = value * random.Range(CoverLeastPermille, CoverMostPermille + 1) / 1000L;
            }
            int reputation = BuildCatalogue.RivalReputation[BuildCatalogue.Clamp(own.Bidder, 0, BuildCatalogue.RivalCount - 1)];
            return new TenderBid { Bidder = own.Bidder, Price = price, Days = own.Days, Score = TenderRules.Score(price, value, own.Days, standardDays, reputation, special) };
        }

        /// <summary>The player's cut for covering: a share of the winning price.</summary>
        public static long Cut(long winningPrice)
        {
            return Math.Max(0L, winningPrice) * CutPermille / 1000L;
        }

        /// <summary>True when the city's inquiry finds a ring out on a day after its award (a reported ring is found at once).</summary>
        public static bool Found(int seed, int tender, int awarded, int day)
        {
            if (awarded <= 0 || day <= awarded || day - awarded > FoundDays)
            {
                return false;
            }
            BuildRandom random = BuildRandom.Of(seed, tender * 7 + day, 331);
            return random.Chance(FoundPermille);
        }

        /// <summary>The members in a ring, counted.</summary>
        public static int Count(int members)
        {
            int n = 0;
            for (int m = members; m != 0; m >>= 1)
            {
                n += m & 1;
            }
            return n;
        }
    }
}
