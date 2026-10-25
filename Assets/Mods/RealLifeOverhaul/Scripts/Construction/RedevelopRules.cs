using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Redevelopment's rules (docs/features/construction.md, "Redevelopment"), one for each thing and the same for the player and
    /// the city's developers: a demolition's works, its permit, the notice to a building's tenants and its business and what
    /// buying them out costs, the rubble hauled away and a contractor's quote. Money is at the start's prices and charged at
    /// today's (the caller's price level, in thousandths). Game-free: EconomyCheck's RedevelopChecks compiles it.
    /// </summary>
    public static class RedevelopRules
    {
        // ------------------------------------------------------------------ demolition

        public const int DemolitionWorks = 6;                   // the programme's demolition works (Civic's SiteKind.Demolition): the works tables' row, the site's kind
        public const int DemolishTenthsPerHundred = 30;         // tenths of a working hour for each 100 m2 of floor: knocking down is quicker than putting up
        public const int DemolishHoursLeast = 40, DemolishHoursMost = 240;
        public const int RubbleArea = 100;                      // m2 of floor: a lorry load of rubble each
        public const int RubbleLoadPrice = 2400;                // money a load hauled away and tipped
        public const int PermitFee = 900;                       // money, and the building's value's thousandths below
        public const int PermitFeePermille = 1;
        public const int PermitDaysLeast = 3, PermitDaysMost = 6;   // working days to the permit's decision
        public const int PermitArea = 2500;                     // m2 of floor: a working day more for each, up to the most
        public const int NoticeDays = 7;                        // days' notice to a building's tenants and business before its works
        public const int TenantDays = 14;                       // days of what the tenants pay, paid to buy them out
        public const int BusinessDays = 30;                     // days of the unit's rent, paid to buy its business out
        public const float LookDownShare = 0.6f;                // of a demolition's works: its fourth of Civic's five phases, the building coming down behind the wrap

        /// <summary>The working hours knocking down a building of so many square metres of floor takes at the usual pace.</summary>
        public static int DemolitionHours(int area)
        {
            long hours = (long)Math.Max(0, area) * DemolishTenthsPerHundred / 1000L;
            return (int)Math.Max(DemolishHoursLeast, Math.Min(DemolishHoursMost, hours));
        }

        /// <summary>Lorry loads of rubble a building of so many square metres of floor leaves: one for each hundred, at least one.</summary>
        public static int RubbleLoads(int area)
        {
            return Math.Max(1, (Math.Max(0, area) + RubbleArea - 1) / RubbleArea);
        }

        /// <summary>What hauling a building's rubble away costs, at today's prices.</summary>
        public static long RubbleCost(int area, int prices)
        {
            return (long)RubbleLoads(area) * RubbleLoadPrice * Math.Max(1, prices) / 1000L;
        }

        /// <summary>The demolition permit's fee at City Hall: a fee at today's prices and a share of the building's value.</summary>
        public static long PermitFeeOf(long value, int prices)
        {
            return (long)PermitFee * Math.Max(1, prices) / 1000L + Math.Max(0L, value) * PermitFeePermille / 1000L;
        }

        /// <summary>Working days to the demolition permit's decision: a few, a day more for a large building.</summary>
        public static int PermitDays(int area)
        {
            return Math.Max(PermitDaysLeast, Math.Min(PermitDaysMost, PermitDaysLeast + Math.Max(0, area) / PermitArea));
        }

        /// <summary>
        /// What buying a building's tenants and its business out costs: <see cref="TenantDays"/> of what its tenants pay a day
        /// (the deed's occupancy of its floor area, its rent a square metre), and <see cref="BusinessDays"/> of the unit's rent
        /// when a business of the city's trades in it. Nothing for an empty building.
        /// </summary>
        public static long BuyOut(double tenantsDaily, double unitRentDaily, bool business)
        {
            double cost = Math.Max(0.0, tenantsDaily) * TenantDays + (business ? Math.Max(0.0, unitRentDaily) * BusinessDays : 0.0);
            return cost <= 0.0 || double.IsNaN(cost) || double.IsInfinity(cost) ? 0L : (long)Math.Round(cost);
        }

        /// <summary>The day a notice given on <paramref name="day"/> runs out: the works may start the day after.</summary>
        public static int NoticeEnds(int day)
        {
            return day + NoticeDays;
        }

        /// <summary>
        /// The city's best demolition contractor's quote: every one of the city's builders bids on the works (their margin,
        /// speciality and luck; Dunmore and Pike the demolition house), the best score wins. The same seed and day give the same
        /// quote. Its price covers the rubble hauled away too.
        /// </summary>
        public static RefitQuote Quote(int seed, int day, int area, int prices)
        {
            int hours = DemolitionHours(area);
            long value = TenderRules.WorksValue(DemolitionWorks, hours, prices) + RubbleCost(area, prices);
            int days = TenderRules.StandardDays(hours);
            var bids = new TenderBid[BuildCatalogue.RivalCount];
            int count = CommissionRules.Bids(seed, day * 37 + 11, value, days, Speciality.Demolition, null, bids);   // the one way the city's builders quote a client
            TenderBid won = bids[Math.Max(0, TenderRules.Winner(bids, count))];
            return new RefitQuote { Rival = won.Bidder, Price = won.Price / 10L * 10L, Days = Math.Max(1, won.Days), Hours = hours };
        }

        // ------------------------------------------------------------------ extensions and change of use

        public const int StoreysMost = 12;          // storeys a district building's record has at most: the floors core's most (NewBuildingsRules.MostFloors)
        public const int ConversionWorks = RefitRules.Works;   // a change of use is worked as the programme's refit works: stripped to its shell, fitted out anew

        /// <summary>
        /// Storeys an Uptown building of so many may be given on top: up to the building code's limit in force (the elected
        /// government's, PlansRules.HeightLimit) and the most a district record takes; 0 when it stands at either already.
        /// </summary>
        public static int ExtensionRoom(int storeys, int limit)
        {
            return Math.Max(0, Math.Min(limit, StoreysMost) - Math.Max(1, storeys));
        }

        /// <summary>
        /// The floor area an extension adds: a building of its size's storey area, of so many storeys in all, less the floor area it
        /// has (a building standing from the first day keeps its design's own, which need not be a whole number of storeys, so a
        /// storey on it may add a little less or more than a storey's area); a square metre at the least.
        /// </summary>
        public static int AddedArea(int storeySqm, int storeys, int sqmNow)
        {
            return Math.Max(1, Math.Max(1, storeySqm) * Math.Max(1, storeys) - Math.Max(0, sqmNow));
        }

        /// <summary>Working hours a change of use takes: a refit's of its floor area and half again, the building stripped to its shell first.</summary>
        public static int ConversionHours(int sqm)
        {
            return RefitRules.Hours(sqm) * 3 / 2;
        }

        /// <summary>
        /// The city's best renovation contractor's quote for a change of use: the eight builders bid for its works as for a client's
        /// refit, the renovators best placed (CommissionRules.Bids, Speciality.Renovation), seeded by the save and the day.
        /// </summary>
        public static RefitQuote ConversionQuote(int seed, int day, int sqm, int prices)
        {
            int hours = ConversionHours(sqm);
            long value = TenderRules.WorksValue(ConversionWorks, hours, prices);
            int days = TenderRules.StandardDays(hours);
            var bids = new TenderBid[BuildCatalogue.RivalCount];
            int count = CommissionRules.Bids(seed, day * 41 + 13, value, days, Speciality.Renovation, null, bids);
            TenderBid won = bids[Math.Max(0, TenderRules.Winner(bids, count))];
            return new RefitQuote { Rival = won.Bidder, Price = won.Price / 10L * 10L, Days = Math.Max(1, won.Days), Hours = hours };
        }

        // ------------------------------------------------------------------ the city's developers

        public const int CityRates = 4;                 // the option's steps: off, slow, usual, lively
        public const int OfferDays = 7;                 // days a developer's offer for a building for sale takes to complete: the player's time to buy it first
        public const int CityRedevsMost = 3;            // the city's redevelopments under way at once at most
        public const int CityRedevsKept = 16;           // those kept in the save, the last ones done among them
        public const int CityShortlist = 4;             // the buildings a move is drawn among: those for sale first, then the emptiest
        public const int HoldingPermille = 330;         // a move made by a rival tycoon's holding while one is in play
        public const int CityPlansQuality = 70;         // a developer's plans before City Hall: a practice's, as good as the middling one's
        public const int CityPatienceDays = 30;         // days past its works' end a redevelopment waits on the district before it is given up
        private static readonly int[] CityGapLeast = { 0, 24, 12, 6 }, CityGapMost = { 0, 36, 18, 9 };   // days between moves, by the option
        private static readonly int[] MoveWeights = { 30, 15, 10, 25, 20 };   // Redevelop, Assemble, Split, Extend, Convert

        /// <summary>
        /// Days to the city's developers' next move at an option's rate (1 slow: 24 to 36; 2 usual: 12 to 18; 3 lively: 6 to 9),
        /// drawn from the save's seed and the day; 0 when they are off.
        /// </summary>
        public static int CityGap(int rate, int seed, int day)
        {
            if (rate <= 0 || rate >= CityRates)
            {
                return 0;
            }
            return BuildRandom.Of(seed, day, 0x5EDE).Range(CityGapLeast[rate], CityGapMost[rate] + 1);
        }

        /// <summary>
        /// The move the city's developers make, drawn by the moves' weights among those the district holds a building for
        /// (<paramref name="can"/>, by CityMove), from the save's seed and the day; CityMove.Count when it holds none.
        /// </summary>
        public static CityMove MoveOf(int seed, int day, bool[] can)
        {
            int total = 0;
            for (int m = 0; m < MoveWeights.Length && m < can.Length; m++)
            {
                total += can[m] ? MoveWeights[m] : 0;
            }
            if (total <= 0)
            {
                return CityMove.Count;
            }
            int roll = BuildRandom.Of(seed, day, 0x30FE).Range(0, total);
            for (int m = 0; m < MoveWeights.Length && m < can.Length; m++)
            {
                if (!can[m])
                {
                    continue;
                }
                if (roll < MoveWeights[m])
                {
                    return (CityMove)m;
                }
                roll -= MoveWeights[m];
            }
            return CityMove.Count;
        }

        /// <summary>One of so many, drawn from the save's seed, the day and what it is for; 0 for none.</summary>
        public static int PickOf(int seed, int day, int salt, int count)
        {
            return count <= 1 ? 0 : BuildRandom.Of(seed, day, 0x7A11 + salt).Range(0, count);
        }

        /// <summary>True when a rival tycoon's holding makes the move (while one is in play), from the save's seed and the day.</summary>
        public static bool ByHolding(int seed, int day)
        {
            return BuildRandom.Of(seed, day, 0x4F1D).Chance(HoldingPermille);
        }

        /// <summary>
        /// A building's place in the developers' order: one for sale first (their offer goes in before anyone else's), then one
        /// with no business trading in it, then the rest; within each, by the save's seed and the lot, the same every day.
        /// </summary>
        public static long Rank(bool forSale, bool trading, int seed, string lot)
        {
            long group = forSale ? 0L : trading ? 2L : 1L;
            return group * 0x100000000L + (uint)BuildRandom.Mix(seed, BuildRandom.Hash(lot));
        }

        /// <summary>Days a developer's new building takes to go up on cleared ground: three weeks and a day for each 150 m2, at most eleven weeks.</summary>
        public static int NewBuildDays(int sqm)
        {
            return BuildCatalogue.Clamp(21 + Math.Max(0, sqm) / 150, 21, 77);
        }

        // ------------------------------------------------------------------ joining and splitting land

        /// <summary>
        /// True when two neighbouring sites of so many spaces each may be joined into one: together no wider than the district's
        /// grid takes a building (BuildCatalogue.SiteSpacesMost, the widest design of the city's that fills a site), and of no more
        /// lots than a site keeps.
        /// </summary>
        public static bool Joins(int spacesA, int spacesB, int lots)
        {
            return spacesA >= 1 && spacesB >= 1 && spacesA + spacesB <= BuildCatalogue.SiteSpacesMost && lots >= 2 && lots <= BuildCatalogue.SiteSpacesMost;
        }

        /// <summary>
        /// A sum shared out by weights into <paramref name=into/>: each share its weight's part, rounded down, what is left over
        /// to the first, so the shares add up to the sum exactly (a split site's price, by its lots' worth as land).
        /// </summary>
        public static void Shares(long total, long[] weights, int count, long[] into)
        {
            long sum = 0L, given = 0L;
            for (int i = 0; i < count; i++)
            {
                sum += Math.Max(0L, weights[i]);
            }
            for (int i = 0; i < count; i++)
            {
                into[i] = sum > 0L ? total * Math.Max(0L, weights[i]) / sum : 0L;
                given += into[i];
            }
            if (count > 0)
            {
                into[0] += total - given;
            }
        }
    }
}
