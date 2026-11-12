using System;
using System.Collections.Generic;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Seasons;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// What the game and the population hold about the businesses that make niches, read once on a day and kept: how many businesses of each niche's types the city has
    /// and, with the population running, the jobs at them, the rents and the buildings that let flats. Never read per frame. Game-free.
    /// </summary>
    public sealed class NicheCensus
    {
        public int Day = -1;                 // the day it was read; -1 never
        public readonly int[] Businesses = new int[NicheCatalogue.Count];
        public readonly int[] Staff = new int[NicheCatalogue.Count];     // jobs at those businesses (the population's employers); 0 where it does not run
        public readonly bool[] Exists = new bool[NicheCatalogue.Count];  // the city can have a business of one of its types
        public int RentIndex = 1000;         // thousandths: the districts' rents against the game's own, on average
        public bool RentKnown;               // the population ran and gave the rents
        public bool Population;              // the staff came from the population's employers
        public int Lettings;                 // residential buildings that let flats, counted from the population's households; 0 where it does not run

        public void Clear()
        {
            Array.Clear(Businesses, 0, Businesses.Length);
            Array.Clear(Staff, 0, Staff.Length);
            Array.Clear(Exists, 0, Exists.Length);
            RentIndex = 1000;
            RentKnown = false;
            Population = false;
            Lettings = 0;
        }
    }

    /// <summary>What moves a niche's buyers today, thousandths each: the city's cycle, the season, the rents and the AI wave. 1000 where the mod holds no data for the niche.</summary>
    public struct NicheMood
    {
        public int Cycle, Season, Rent, Hype;

        public int All => (int)Math.Max(0L, (long)Cycle * Season / 1000 * Rent / 1000 * Hype / 1000);
    }

    /// <summary>A niche as the Tech app shows it, read on the day and kept. Cents at the start's prices.</summary>
    public struct NicheFacts
    {
        public bool Exists;
        public int Businesses, Staff;
        public long Buyers;          // the niche's buyers today
        public long Ceiling;         // the most it can pay a month
        public int Rivals;           // rivals in it, of the market's
        public int Going;            // thousandths of the segment's price
        public int Share;            // thousandths of the market's rivals that sell in it
        public NicheMood Mood;
    }

    /// <summary>
    /// The rules of niches (the plan's section 7): a niche's buyers from the city's real businesses of its kind, its revenue ceiling, its rivals and its going price,
    /// how a neighbouring niche opens, and the niche of one of the city's startups. Whole numbers and seeded rolls only. Game-free: phase 5 reads these for rival startups.
    /// </summary>
    public static class NicheRules
    {
        private static Dictionary<string, int> _byType;

        /// <summary>The niche a business type belongs to, or -1.</summary>
        public static int NicheOfType(string type)
        {
            if (_byType == null)
            {
                var map = new Dictionary<string, int>(48, StringComparer.Ordinal);
                for (int n = 0; n < NicheCatalogue.Count; n++)
                {
                    for (int t = 0; t < NicheCatalogue.Defs[n].Types.Length; t++)
                    {
                        map[NicheCatalogue.Defs[n].Types[t]] = n;
                    }
                }
                _byType = map;
            }
            return !string.IsNullOrEmpty(type) && _byType.TryGetValue(type, out int found) ? found : -1;
        }

        /// <summary>A business of the city counted into its niche. Staff are the jobs at it, 0 when the population does not say.</summary>
        public static void Count(NicheCensus c, string type, int staff)
        {
            int n = NicheOfType(type);
            if (n >= 0)
            {
                c.Businesses[n]++;
                c.Staff[n] += Math.Max(0, staff);
            }
        }

        /// <summary>A niche exists if the city can have one of its types (<paramref name="can"/> says so for a type) or, for landlords, if the buildings that let flats were counted.</summary>
        public static void Exist(NicheCensus c, Func<string, bool> can)
        {
            int lettings = c.Lettings;
            for (int n = 0; n < NicheCatalogue.Count; n++)
            {
                NicheDef d = NicheCatalogue.Defs[n];
                bool any = false;
                for (int t = 0; t < d.Types.Length && !any; t++)
                {
                    any = can != null && can(d.Types[t]);
                }
                c.Exists[n] = d.Rented ? lettings > 0 : any;
                if (d.Rented)
                {
                    c.Businesses[n] = Math.Max(0, lettings);
                }
            }
        }

        // ---------------------------------------------------------------- what moves a niche

        /// <summary>
        /// The share of the cycle's move a niche's businesses feel, thousandths: those with a habit of buying (groceries) feel less of a downturn than the ones that are
        /// treats (a night out). From the population's habit table; 1000 for a niche none of whose types it knows.
        /// </summary>
        public static int Swing(NicheDef d)
        {
            int kind = NicheCatalogue.SpendOf(d);
            if (kind < 0 || kind >= PopulationCatalogue.Habit.Length)
            {
                return 1000;
            }
            return Math.Max(600, Math.Min(1600, (1000 - PopulationCatalogue.Habit[kind]) * 2));
        }

        /// <summary>What moves a niche's buyers today. The AI wave counts only for an AI product (<paramref name="ai"/>).</summary>
        public static NicheMood Mood(NicheDef d, NicheCensus c, int cycle, int season, int hype, bool ai)
        {
            var m = new NicheMood { Cycle = 1000, Season = 1000, Rent = 1000, Hype = 1000 };
            int cyc = FirmsRules.CycleSpend[Math.Max(0, Math.Min(3, cycle))];
            m.Cycle = 1000 + (cyc - 1000) * Swing(d) / 1000;
            if (d.Good != null)
            {
                int good = SeasonRules.IndexOf(d.Good);
                if (good >= 0)
                {
                    m.Season = 1000 + (SeasonRules.DemandOf(good, season) - 1000) * NicheCatalogue.SeasonShare / 1000;
                }
            }
            if (d.Rented && c.RentKnown)
            {
                m.Rent = Math.Max(NicheCatalogue.RentShareLeast, Math.Min(NicheCatalogue.RentShareMost, c.RentIndex));
            }
            if (ai)
            {
                m.Hype = 1000 + (hype - 1000) * NicheCatalogue.HypeShare / 1000;
            }
            return m;
        }

        /// <summary>
        /// A niche's buyers: the city's businesses of its kind, each standing for the region's like it as the sector's markets have it (SectorRules.WorldAt), moved by the
        /// niche's mood. No cap but the city: a small niche has few.
        /// </summary>
        public static long Buyers(NicheDef d, NicheCensus c, int days, int year, in NicheMood mood)
        {
            long here = c.Businesses[d.Id];
            long world = SectorRules.WorldAt(d.Buyers, days, year);
            return Math.Max(0L, here * world / 1000 * mood.All / 1000);
        }

        // ---------------------------------------------------------------- its price, rivals and ceiling

        /// <summary>
        /// What a niche's businesses will bear against the market's, thousandths: richer ones (a bigger basket per customer, the population's table) pay more. 1000 for a
        /// niche none of whose types the table knows.
        /// </summary>
        public static int PriceFactor(NicheDef d)
        {
            long sum = 0;
            int n = 0;
            for (int i = 0; i < d.Types.Length; i++)
            {
                if (PopulationCatalogue.TypeIndex(d.Types[i]) >= 0)
                {
                    sum += PopulationCatalogue.BasketOfType(d.Types[i]);
                    n++;
                }
            }
            if (n == 0)
            {
                return 1000;
            }
            long ratio = Math.Max(1L, sum * 1000 / n / NicheCatalogue.NeutralBasket);
            return Math.Max(NicheCatalogue.PriceLeast, Math.Min(NicheCatalogue.PriceMost, 1000 + (int)((long)NicheCatalogue.PriceSpread * Log2(ratio) / 1000)));
        }

        // log2 of a ratio in thousandths, in thousandths: whole doublings and then a straight line to the next
        private static int Log2(long mille)
        {
            int whole = 0;
            long r = Math.Max(1L, mille);
            while (r >= 2000)
            {
                r /= 2;
                whole++;
            }
            while (r < 1000)
            {
                r *= 2;
                whole--;
            }
            return whole * 1000 + (int)(r - 1000);
        }

        /// <summary>The going price in a niche: the market's going price (thousandths of the segment's price) times what the niche's businesses bear.</summary>
        public static int Going(NicheDef d, int marketGoing) => (int)Math.Max(1L, (long)marketGoing * PriceFactor(d) / 1000);

        /// <summary>
        /// The share of a market's rivals that sell in a niche, thousandths: a niche's customers are a part of the market's, and rivals go where the customers are, so the
        /// share follows the square root of that part. A very small niche has few, never none.
        /// </summary>
        public static int RivalShare(long nicheBuyers, long marketBuyers)
        {
            if (marketBuyers <= 0)
            {
                return 1000;
            }
            long part = Math.Max(0L, Math.Min(1000L, nicheBuyers * 1000 / marketBuyers));
            return Math.Max(NicheCatalogue.RivalFloor, Math.Min(1000, Isqrt(part * 1000)));
        }

        public static int Isqrt(long n)
        {
            if (n <= 0)
            {
                return 0;
            }
            long x = (long)Math.Sqrt(n);
            while (x * x > n)
            {
                x--;
            }
            while ((x + 1) * (x + 1) <= n)
            {
                x++;
            }
            return (int)x;
        }

        /// <summary>The most a niche can pay a month at a price: its buyers at the segment's list price times the going price, cents at the start's prices.</summary>
        public static long Ceiling(NicheDef d, long buyers, int goingMille)
        {
            int seg = d.Buyers == (int)SectorBuyers.Enterprises ? (int)Segment.Enterprise : (int)Segment.Business;
            return Math.Max(0L, buyers) * FirmsCatalogue.Price[seg] / 1000 * Math.Max(1, goingMille) / 1000;
        }

        /// <summary>Deals a niche holds: the businesses that could sign, one deal each.</summary>
        public static long Deals(long buyers) => Math.Max(0L, buyers);

        // ---------------------------------------------------------------- a product in its niche

        /// <summary>
        /// A market narrowed to a niche for one product: the niche's buyers in place of the market's, the same features and spend, one rival at the price the niche will bear
        /// (<paramref name="bearsMille"/>). The pull of every product in it is set by <see cref="Pull"/> once the product's own is worked out.
        /// </summary>
        public static void View(SectorMarketDay v, SectorMarketDay md, long nicheBuyers, int bearsMille)
        {
            v.Clear();
            v.Buyers = Math.Max(0L, Math.Min(nicheBuyers, md.Buyers));
            v.Features = md.Features;
            v.Count = md.Count;
            v.Spend = md.Spend;
            v.Who = md.Who;
            v.Leader = md.Leader;
            v.Mine = md.Mine;
            v.MineFeatures = md.MineFeatures;
            v.RivalFeatures = md.RivalFeatures;
            v.Rivals = 1;
            v.PriceSum = Math.Max(1, bearsMille);
        }

        /// <summary>The niche's pull: the product's own and the share of the market's other pull that sells in the niche. The product's own is already in the market's.</summary>
        public static void Pull(SectorMarketDay v, SectorMarketDay md, long own, long ownInMarket)
        {
            v.All = Math.Max(0L, own) + Math.Max(0L, md.All - Math.Max(0L, ownInMarket)) * RivalShare(v.Buyers, md.Buyers) / 1000;
        }

        /// <summary>
        /// The market a product of a founded firm is weighed in, worked out once for the day's sale and for the product's page, so the page says what the sale
        /// does: the buyers of the firm's niche and of the neighbours it opened (<see cref="Ramp"/> in) in place of the market's, at the price the niche bears,
        /// with the product's appeal, awareness and the firm's selling points worked again there (<paramref name="appeal"/>, <paramref name="aware"/> and
        /// <paramref name="crew"/> come in as the whole market's and go out as the niche's) and its share of the pull among everyone who sells there. Returns
        /// <paramref name="md"/> itself, changing nothing, for a firm whose founding does not count or whose niches do not buy in the product's market.
        /// COST: a few sums.
        /// </summary>
        public static SectorMarketDay Market(SectorMarketDay into, FoundedFirm f, Product p, Firm firm, SectorMarketDay md, long[] today, SectorProduct record, RivalryWorld world, int day,
            long pulled, int founder, ref int appeal, ref int aware, ref Crew crew, out int served)
        {
            // into: the view, written; today: each niche's buyers for this kind of product; record: the product's record in the sector (a launch's lift); world: the rivals' pool,
            // null with competition off; pulled: its pull in the whole market; founder: the selling points a sales channel adds; served: one bit for each niche it sells into
            served = 0;
            if (f == null || !f.Counts || f.Niche < 0)
            {
                return md;
            }
            int market = SectorRules.MarketOf(p);
            long total = 0;
            bool serves = false;
            int bits = 0;
            if (Serves(NicheCatalogue.Def(f.Niche), market))
            {
                total += today[f.Niche];
                serves = true;
                bits |= 1 << f.Niche;
            }
            for (int i = 0; i < f.Expanded.Count; i++)
            {
                if (Serves(NicheCatalogue.Def(f.Expanded[i].Niche), market))
                {
                    total += today[f.Expanded[i].Niche] * Ramp(day - f.Expanded[i].Day) / 1000;
                    serves = true;
                    bits |= 1 << f.Expanded[i].Niche;
                }
            }
            if (!serves)
            {
                return md;
            }
            long buyers = Math.Min(total, md.Buyers);
            View(into, md, buyers, (int)Math.Max(1L, (long)Going(NicheCatalogue.Def(f.Niche), md.Going) * FoundRules.Bear(f) / 1000));
            buyers = into.Buyers;
            appeal = SectorRules.Appeal(p, firm, crew, into);
            int marketing = FirmsCatalogue.MarketingDay[TalentCatalogue.Clamp(p.Marketing, 0, FirmsCatalogue.MarketingDay.Length - 1)];
            aware = SectorRules.Awareness(marketing, buyers, SectorRules.Have(p), md.Who, SectorRules.Boost(record, day) + FoundRules.Boost(f.Channel) + f.Wom);
            served = bits;
            if (world != null)
            {
                // one pool of buyers for everyone who sells in the niche: the city's startups pull in the same buyers (RivalryRules.PlayerAll)
                into.All = RivalryRules.PlayerAll(world, bits, market, md.All, SectorRules.Attraction(appeal, aware), pulled, buyers, md.Buyers, day);
            }
            else
            {
                Pull(into, md, SectorRules.Attraction(appeal, aware), pulled);
            }
            if (f.Channel == FoundCatalogue.ChannelSales)
            {
                crew.Selling += founder;
            }
            return into;
        }

        // ---------------------------------------------------------------- expansion and the markets

        public static bool Neighbours(int from, int to) => from >= 0 && to >= 0 && from < NicheCatalogue.Count && to < NicheCatalogue.Count && (NicheCatalogue.Defs[from].Near & (1 << to)) != 0;

        /// <summary>The thousandths of an expanded niche's buyers that count after so many days: the product is reused, so it starts at half and fills.</summary>
        public static int Ramp(int days) => Math.Min(1000, NicheCatalogue.ExpandStart + Math.Max(0, days) * (1000 - NicheCatalogue.ExpandStart) / Math.Max(1, NicheCatalogue.ExpandRamp));

        /// <summary>The market (SectorMarket) a product of a niche sells in: AI, or software, for the niche's kind of buyer.</summary>
        public static int MarketOf(NicheDef d, bool ai)
        {
            if (d.Buyers == (int)SectorBuyers.Enterprises)
            {
                return ai ? (int)SectorMarket.EnterpriseAi : (int)SectorMarket.EnterpriseSoftware;
            }
            return ai ? (int)SectorMarket.BusinessAi : (int)SectorMarket.BusinessSoftware;
        }

        /// <summary>Whether a product's market is one a niche's businesses buy in: their kind of buyer, and not the apps and consumer markets.</summary>
        public static bool Serves(NicheDef d, int market)
        {
            if (market < 0 || market >= (int)SectorMarket.Count || market == (int)SectorMarket.Apps)
            {
                return false;
            }
            return SectorRules.BuyersOf(market) == d.Buyers;
        }

        public static bool IsAi(int market) => market == (int)SectorMarket.BusinessAi || market == (int)SectorMarket.EnterpriseAi || market == (int)SectorMarket.ConsumerAi;

        /// <summary>
        /// The niche of one of the city's startups, seeded and the same on every machine, among the niches that buy in its market; -1 for a startup that sells to people
        /// at large or builds games (no niche in its market).
        /// </summary>
        public static int OfStartup(int seed, int startupId, int sector)
        {
            if (sector < 0 || sector >= SectorCatalogue.StartupMarket.Length)
            {
                return -1;
            }
            int market = SectorCatalogue.StartupMarket[sector];
            if (market < 0)
            {
                return -1;
            }
            int count = 0;
            for (int n = 0; n < NicheCatalogue.Count; n++)
            {
                count += Serves(NicheCatalogue.Defs[n], market) ? 1 : 0;
            }
            if (count == 0)
            {
                return -1;
            }
            int pick = TechRandom.Range(seed, startupId, 71, sector, 0, count - 1);
            for (int n = 0; n < NicheCatalogue.Count; n++)
            {
                if (Serves(NicheCatalogue.Defs[n], market) && pick-- == 0)
                {
                    return n;
                }
            }
            return -1;
        }

        /// <summary>The niche an older save's firm is placed in, seeded by its place, among those its product's market serves; -1 when none does.</summary>
        public static int OfFirm(int seed, string place, int market)
        {
            int count = 0;
            for (int n = 0; n < NicheCatalogue.Count; n++)
            {
                count += Serves(NicheCatalogue.Defs[n], market) ? 1 : 0;
            }
            if (count == 0)
            {
                return -1;
            }
            int pick = TechRandom.Range(seed, TechRandom.Of(place), 73, market, 0, count - 1);
            for (int n = 0; n < NicheCatalogue.Count; n++)
            {
                if (Serves(NicheCatalogue.Defs[n], market) && pick-- == 0)
                {
                    return n;
                }
            }
            return -1;
        }
    }
}
