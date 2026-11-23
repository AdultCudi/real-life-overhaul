using System;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>Where the player's broadband company stands; saved as its number.</summary>
    public enum CompanyState
    {
        None = 0,
        Running = 1,
        Sold = 2
    }

    /// <summary>The player's broadband company: its network, the city's market shares, its settings and its books.</summary>
    public sealed class Network
    {
        public const int Districts = 9;          // BroadbandCatalogue.Districts: the game's seven and the mod's two
        public const int Legacy = BroadbandCatalogue.Legacy;   // the first of them are a save before the mod's own two kept
        public const int Slots = Districts + BroadbandCatalogue.ExtraMax;   // the places the arrays keep: those and the districts a free update adds
        public const int Providers = 5;          // the four rivals (InternetCatalogue.Providers) and the company
        public const int Own = 4;

        public CompanyState State;
        public int FoundedDay, SoldDay;
        public long SalePrice;
        public int Count = Districts;            // the districts in use: the catalogue's, then those registered after them (never fewer)
        public readonly string[] Id = new string[Slots];       // each district's game id
        public readonly int[] Size = new int[Slots];           // its connections at the start's prices: the catalogue's, or a later district's as it was measured when registered
        public readonly int[] Far = new int[Slots];            // thousandths of the usual cost per connection its distance makes it (BroadbandRules.FarShare)
        public readonly int[] Built = new int[Slots];          // the day a district's network opened; minus the day it will while dug; 0 none
        public readonly int[] Spent = new int[Slots];          // what each district's network cost, money
        public readonly int[,] Share = new int[Slots, Providers];   // thousandths of each district's connections, by provider
        public int PriceShare = 1000;            // the company's prices, thousandths of the usual
        public int Maintenance = 1000;           // thousandths of the usual upkeep spent
        public int Marketing;                    // a budget of BroadbandCatalogue.Marketing
        public int MonthTakings, MonthCosts;     // this month so far, money
        public int LastTakings, LastCosts, LastConnections, LastGained;   // the last month's
        public readonly int[] History = new int[BroadbandCatalogue.HistoryMonths];   // each recorded month's profit, the oldest first
        public int HistoryCount;
        public int ScaleFrom = EndScaleRules.Unset;   // the day the end game's scale began to come in for the company
        public readonly bool[] Scaled = new bool[Slots];   // dug or extended at scale: it reaches the whole district
        public readonly int[] Crew = new int[BroadbandCatalogue.CrewKinds];   // its crews at scale: field, call centre, network operations
        public long Owed;   // money the scale added that the cash could not meet, carried to the next day
        // read each day, never saved
        public int Scale;                                               // the end game's share today, set by the company before the rules run
        public int Cover = EndScaleRules.Full;                          // how far its crews cover what they need, thousandths
        public readonly int[] People = new int[Slots];                  // thousandths of each district's homes and businesses against the anchor day's (the living population); 0 is read as 1000

        public Network()
        {
            for (int d = 0; d < Districts; d++)
            {
                Id[d] = BroadbandCatalogue.Districts[d];
                Size[d] = BroadbandCatalogue.Market[d];
                Far[d] = BroadbandCatalogue.Far[d];
            }
        }

        /// <summary>A district's place by its id; -1 for one the network has no place for.</summary>
        public int IndexOf(string id)
        {
            for (int d = 0; d < Count && id != null; d++)
            {
                if (string.Equals(Id[d], id, StringComparison.Ordinal))
                {
                    return d;
                }
            }
            return -1;
        }

        /// <summary>
        /// A district the catalogue does not list (one a free update added) given the next place, sized and distanced as measured; its place if it had one, -1 when
        /// the places are full or the id empty. A place once given is never taken back, so a save's records by id keep to their district.
        /// </summary>
        public int Register(string id, int size, int far)
        {
            int at = IndexOf(id);
            if (at >= 0 || string.IsNullOrEmpty(id) || Count >= Slots)
            {
                return at;
            }
            Id[Count] = id;
            Size[Count] = Math.Max(1, size);
            Far[Count] = Math.Max(1000, Math.Min(BroadbandCatalogue.FarMost, far));
            return Count++;
        }

        public bool Running => State == CompanyState.Running;
        public bool Exists => State != CompanyState.None;

        public bool Open(int district, int day)
        {
            return district >= 0 && district < Count && Built[district] > 0 && Built[district] <= day;
        }

        public bool Digging(int district)
        {
            return district >= 0 && district < Count && Built[district] < 0;
        }

        public int ReadyDay(int district)
        {
            return Digging(district) ? -Built[district] : 0;
        }
    }

    /// <summary>
    /// The rules of the player's broadband company. With no newcomer at list prices the market stands still, so customers move
    /// over months; at the end game's scale (Scale, 0 to 1000) figures are the real city's. Checked offline (EconomyCheck).
    /// </summary>
    public static class BroadbandRules
    {
        private static readonly string[] None = new string[0];

        /// <summary>A catalogued district's place by its id; -1 for any other (a later district's place is in its network: <see cref="Network.IndexOf"/>).</summary>
        public static int District(string id)
        {
            return id == null ? -1 : Array.IndexOf(BroadbandCatalogue.Districts, id);
        }

        /// <summary>An amount at the start's prices at today's, money; in longs, as a network costs millions.</summary>
        public static int At(int amount, int level)
        {
            return Clip(((long)amount * Level(level) + PriceRules.Start / 2) / PriceRules.Start);
        }

        public static int LicenceFee(int level, int share = 0) => At(EndScaleRules.Mix(BroadbandCatalogue.LicenceFee, BroadbandCatalogue.LicenceFeeScale, share), level);

        /// <summary>
        /// The thousandths of the usual cost per connection a district's distance makes it: nothing more than <see cref="BroadbandCatalogue.NearMetres"/> from the middle
        /// of the nearest district the catalogue lists is the usual cost, <see cref="BroadbandCatalogue.FarMetres"/> or more is <see cref="BroadbandCatalogue.FarMost"/>
        /// (the Hamptons'), and in a line between. The metres come from <see cref="NearestMetres"/>.
        /// </summary>
        public static int FarShare(int nearestMetres)
        {
            if (nearestMetres <= BroadbandCatalogue.NearMetres)
            {
                return 1000;
            }
            if (nearestMetres >= BroadbandCatalogue.FarMetres)
            {
                return BroadbandCatalogue.FarMost;
            }
            return 1000 + (BroadbandCatalogue.FarMost - 1000) * (nearestMetres - BroadbandCatalogue.NearMetres) / (BroadbandCatalogue.FarMetres - BroadbandCatalogue.NearMetres);
        }

        /// <summary>Metres from a point on the city's map to the nearest middle of the catalogue's districts.</summary>
        public static int NearestMetres(float x, float z)
        {
            double best = double.MaxValue;
            for (int d = 0; d < BroadbandCatalogue.MiddleX.Length; d++)
            {
                double dx = x - BroadbandCatalogue.MiddleX[d], dz = z - BroadbandCatalogue.MiddleZ[d];
                best = Math.Min(best, Math.Sqrt(dx * dx + dz * dz));
            }
            return (int)Math.Min(int.MaxValue / 4, best);
        }

        /// <summary>A later district's connections at the start's prices from the homes and businesses the population counts there: ten each, never
        /// under a village's; the default with none counted.</summary>
        public static int SizeFromHomes(int homes)
        {
            return homes > 0 ? Math.Max(BroadbandCatalogue.PerHousehold * 300, homes * BroadbandCatalogue.PerHousehold) : BroadbandCatalogue.DefaultMarket;
        }

        /// <summary>A district's network's cost at the start's prices, money: the catalogue's figure, or for a later district its connections at the usual cost each, by its distance.</summary>
        public static int CostAtStart(Network n, int district)
        {
            if (district >= 0 && district < Network.Districts)
            {
                return BroadbandCatalogue.BuildCost[district];
            }
            return Clip((long)n.Size[district] * BroadbandCatalogue.BuildPerConnection / 100 * n.Far[district] / 1000);
        }

        /// <summary>A district's network's cost, money: the old figure before the scale; at it, every home and business at the real size, the farther the dearer.</summary>
        public static int BuildCost(Network n, int district, int level, int share = 0)
        {
            if (n == null || district < 0 || district >= n.Count)
            {
                return 0;
            }
            long scale = (long)MarketAtScale(n, district) * BroadbandCatalogue.HomeCentsScale / 100 * n.Far[district] / 1000;
            return Clip((EndScaleRules.Mix((long)CostAtStart(n, district), scale, share) * Level(level) + PriceRules.Start / 2) / PriceRules.Start);
        }

        /// <summary>The same for a catalogued district with no network at hand (the checks' and the Guide's).</summary>
        public static int BuildCost(int district, int level, int share = 0)
        {
            return district < 0 || district >= Network.Districts ? 0 : BuildCost(Catalogue, district, level, share);
        }

        private static readonly Network Catalogue = new Network();

        /// <summary>The least or the most a district's network costs, money, for the Guide: over the districts the network has (the catalogue's with none).</summary>
        public static int BuildCostRange(Network n, int level, int share, bool most)
        {
            Network of = n ?? Catalogue;
            int pick = BuildCost(of, 0, level, share);
            for (int d = 1; d < of.Count; d++)
            {
                int cost = BuildCost(of, d, level, share);
                pick = most ? Math.Max(pick, cost) : Math.Min(pick, cost);
            }
            return pick;
        }

        /// <summary>The same with no network at hand: over the catalogue's districts.</summary>
        public static int BuildCostRange(int level, int share, bool most) => BuildCostRange(null, level, share, most);

        /// <summary>A catalogued district's homes and businesses at the city's real size.</summary>
        public static int MarketAtScale(int district) => BroadbandCatalogue.Market[district] * BroadbandCatalogue.MarketScale / 1000;

        /// <summary>Any district's homes and businesses at the city's real size.</summary>
        public static int MarketAtScale(Network n, int district) => (int)Math.Min(int.MaxValue / 4, (long)n.Size[district] * BroadbandCatalogue.MarketScale / 1000);

        /// <summary>
        /// A district's connections for the company: the old number, or, dug at scale, the real size as the scale comes in; either moved by the district's homes
        /// and businesses against the anchor day's where the living population runs (households forming and leaving, businesses opening and closing).
        /// </summary>
        public static int MarketOf(Network n, int district)
        {
            n = n ?? Catalogue;   // no company: the catalogue's own figures
            int before = n.Size[district];
            int size = n.Scaled[district] ? EndScaleRules.Mix(before, MarketAtScale(n, district), n.Scale) : before;
            return (int)Math.Min(int.MaxValue / 4, (long)size * PeopleOf(n, district) / 1000);
        }

        /// <summary>A district's connections the company reaches once dug, or would if dug now (at scale, the whole district), as the pages list them.</summary>
        public static int MarketShown(Network n, int district, int share, bool built)
        {
            return built ? MarketOf(n, district) : (int)((long)EndScaleRules.Mix(n.Size[district], MarketAtScale(n, district), share) * PeopleOf(n, district) / 1000);
        }

        /// <summary>Thousandths of a district's homes and businesses against the anchor day's: a thousand with no living population.</summary>
        public static int PeopleOf(Network n, int district)
        {
            return n != null && district >= 0 && district < Network.Slots && n.People[district] > 0 ? n.People[district] : 1000;
        }

        /// <summary>What extending a network dug before the scale costs: the scale's network less what it cost; 0 with nothing to extend.</summary>
        public static int ExtendCost(Network n, int district, int level, int day, int buy)
        {
            if (n == null || !n.Running || buy <= 0 || district < 0 || district >= n.Count || !n.Open(district, day) || n.Scaled[district])
            {
                return 0;
            }
            return Math.Max(1, BuildCost(n, district, level, buy) - n.Spent[district]);
        }

        /// <summary>A network extended to the whole district: at scale from now, its cost added to what it had cost.</summary>
        public static void Extend(Network n, int district, int cost)
        {
            n.Scaled[district] = true;
            n.Spent[district] = Clip((long)n.Spent[district] + Math.Max(0, cost));
        }

        public static int MarketingMoney(int budget, int level)
        {
            return At(BroadbandCatalogue.Marketing[Clamp(budget, 0, BroadbandCatalogue.Marketing.Length - 1)], level);
        }

        /// <summary>How often the network fails, in a thousand days, by what is spent on its maintenance.</summary>
        public static int OutagePerMille(int maintenance)
        {
            int m = Clamp(maintenance, BroadbandCatalogue.MaintenanceLeast, BroadbandCatalogue.MaintenanceMost);
            return Clamp((BroadbandCatalogue.OutageBase * 1000 + m / 2) / m, BroadbandCatalogue.OutageLeast, BroadbandCatalogue.OutageMost);
        }

        /// <summary>A plan's price a month now, cents, at the company's prices and today's.</summary>
        public static int PlanCents(Network n, int plan, int level)
        {
            return PlanCents(n.PriceShare, plan, level);
        }

        /// <summary>A plan's price a month at a share of its usual (thousandths) and today's prices, cents.</summary>
        public static int PlanCents(int priceShare, int plan, int level)
        {
            int k = Clamp(plan, 0, BroadbandCatalogue.Mbps.Length - 1);
            long cents = (long)BroadbandCatalogue.DefaultCents[k] * Clamp(priceShare, BroadbandCatalogue.PriceLeast, BroadbandCatalogue.PriceMost) / 1000;
            return Clip(cents * Level(level) / PriceRules.Start);
        }

        /// <summary>A connection's price a month by the plans' usual mix, cents.</summary>
        public static int AverageCents(Network n, int level)
        {
            long sum = 0;
            for (int k = 0; k < BroadbandCatalogue.Mbps.Length; k++)
            {
                sum += (long)BroadbandCatalogue.TierMix[k] * PlanCents(n, k, level);
            }
            return Clip(sum / 1000);
        }

        /// <summary>The company founded: the market as it stands (the rivals' shares where each reaches, by hold), settings as usual, books empty.</summary>
        public static void Found(Network n, int day)
        {
            n.State = CompanyState.Running;
            n.FoundedDay = day;
            n.SoldDay = 0;
            n.SalePrice = 0;
            n.Owed = 0;
            for (int d = 0; d < n.Count; d++)
            {
                Seed(n, d);
            }
            Array.Clear(n.Scaled, 0, n.Scaled.Length);
            Array.Clear(n.Crew, 0, n.Crew.Length);
            n.Cover = EndScaleRules.Full;
            n.PriceShare = 1000;
            n.Maintenance = 1000;
            n.Marketing = 0;
            n.MonthTakings = n.MonthCosts = n.LastTakings = n.LastCosts = n.LastConnections = n.LastGained = 0;
            Array.Clear(n.History, 0, n.History.Length);
            n.HistoryCount = 0;
        }

        /// <summary>A district with no network of the company's yet: nothing built or spent, the market as it stands (each rival's share where it reaches, by its hold).</summary>
        public static void Seed(Network n, int d)
        {
            n.Built[d] = n.Spent[d] = 0;
            n.Scaled[d] = false;
            int total = 0;
            for (int p = 0; p < Network.Own; p++)
            {
                total += InternetRules.Reaches(p, n.Id[d]) ? BroadbandCatalogue.RivalHold[p] : 0;
            }
            for (int p = 0; p < Network.Providers; p++)
            {
                n.Share[d, p] = p < Network.Own && total > 0 && InternetRules.Reaches(p, n.Id[d]) ? BroadbandCatalogue.RivalHold[p] * 1000 / total : 0;
            }
            Whole(n, d);
        }

        /// <summary>A district's network ordered: dug for BuildMonths from a day, at a cost.</summary>
        public static void StartBuild(Network n, int district, int day, int monthDays, int cost, bool scaled = false)
        {
            n.Built[district] = -(day + BroadbandCatalogue.BuildMonths * Math.Max(1, monthDays));
            n.Spent[district] = Math.Max(0, cost);
            n.Scaled[district] = scaled;
        }

        /// <summary>The networks ready by a day opened on the day they were ready; how many.</summary>
        public static int Finish(Network n, int day)
        {
            int opened = 0;
            for (int d = 0; d < n.Count; d++)
            {
                if (n.Digging(d) && n.ReadyDay(d) <= day)
                {
                    n.Built[d] = n.ReadyDay(d);
                    opened++;
                }
            }
            return opened;
        }

        /// <summary>A district's trust in the network, thousandths: a ninth when it opens there, all of it TrustMonths later.</summary>
        public static int Trust(Network n, int district, int day, int monthDays)
        {
            if (!n.Open(district, day))
            {
                return 0;
            }
            int m = Math.Max(1, monthDays);
            long trust = 1000L * (day - n.Built[district] + m) / ((long)BroadbandCatalogue.TrustMonths * m);
            return (int)Math.Max(0L, Math.Min(1000L, trust));
        }

        /// <summary>A month of the market: each share moves Follow of the way to its provider's weight; <paramref name="promo"/>, the rivals' prices.</summary>
        public static void Month(Network n, int[] promo, int day, int monthDays)
        {
            if (!n.Exists)
            {
                return;
            }
            var weight = new double[Network.Providers];
            double own = n.Running ? Pull(n) : 1.0;
            double e = BroadbandCatalogue.PriceElasticity / 1000.0;
            for (int d = 0; d < n.Count; d++)
            {
                double sum = 0.0;
                for (int p = 0; p < Network.Own; p++)
                {
                    int price = promo != null && p < promo.Length && promo[p] > 0 ? promo[p] : 1000;
                    weight[p] = InternetRules.Reaches(p, n.Id[d]) ? BroadbandCatalogue.RivalHold[p] * Math.Pow(1000.0 / price, e) : 0.0;
                    sum += weight[p];
                }
                weight[Network.Own] = n.Open(d, day) ? BroadbandCatalogue.OwnHold * Trust(n, d, day, monthDays) / 1000.0 * own : 0.0;
                sum += weight[Network.Own];
                if (sum <= 0.0)
                {
                    continue;
                }
                for (int p = 0; p < Network.Providers; p++)
                {
                    int now = n.Share[d, p];
                    double target = 1000.0 * weight[p] / sum;
                    n.Share[d, p] = weight[p] > 0.0 ? Math.Max(0, (int)Math.Round(now + (target - now) * BroadbandCatalogue.Follow / 1000.0, MidpointRounding.AwayFromZero)) : 0;
                }
                Whole(n, d);
            }
        }

        /// <summary>The company's own pull on its weight: its prices, reliability and marketing.</summary>
        public static double Pull(Network n)
        {
            double price = Math.Pow(1000.0 / Clamp(n.PriceShare, BroadbandCatalogue.PriceLeast, BroadbandCatalogue.PriceMost), BroadbandCatalogue.PriceElasticity / 1000.0);
            double reliability = Math.Pow((double)BroadbandCatalogue.OutageBase / OutagePerMille(n.Maintenance), BroadbandCatalogue.ReliabilityElasticity / 1000.0);
            int budget = BroadbandCatalogue.Marketing[Clamp(n.Marketing, 0, BroadbandCatalogue.Marketing.Length - 1)];
            double marketing = 1.0 + BroadbandCatalogue.MarketingPull / 1000.0 * Math.Log(1.0 + (double)budget / BroadbandCatalogue.MarketingUnit);
            return price * reliability * marketing * EndScaleRules.Working(n.Cover) / 1000.0;
        }

        // a district's shares made to sum to a thousand, the rounding to the largest
        private static void Whole(Network n, int d)
        {
            int sum = 0, largest = 0;
            for (int p = 0; p < Network.Providers; p++)
            {
                sum += n.Share[d, p];
                largest = n.Share[d, p] > n.Share[d, largest] ? p : largest;
            }
            if (sum > 0)
            {
                n.Share[d, largest] = Math.Max(0, n.Share[d, largest] + 1000 - sum);
            }
        }

        /// <summary>The company's connections in a district, or in all.</summary>
        public static int Connections(Network n, int district = -1)
        {
            long total = 0;
            for (int d = 0; d < n.Count; d++)
            {
                if (district < 0 || district == d)
                {
                    total += (long)n.Share[d, Network.Own] * MarketOf(n, d) / 1000;
                }
            }
            return Clip(total);
        }

        /// <summary>A day's takings, money: each connection's share of its month; nothing on a day of an outage or without a headquarters.</summary>
        public static int DayTakings(Network n, int level, int monthDays, bool outage, bool billing)
        {
            if (!n.Running || outage || !billing)
            {
                return 0;
            }
            return Money((long)Connections(n) * AverageCents(n, level) / Math.Max(1, monthDays) * EndScaleRules.Mix(1000, BroadbandCatalogue.IncomeAtScale, n.Scale) / 1000);
        }

        /// <summary>A day's costs, money: staff and the network for every connection, the upkeep of what is open, and marketing.</summary>
        public static void DayCosts(Network n, int level, int day, int monthDays, out int staff, out int network, out int marketing, int share = -1)
        {
            staff = network = marketing = 0;
            int s = share < 0 ? Clamp(n.Scale, 0, EndScaleRules.Full) : Clamp(share, 0, EndScaleRules.Full);
            if (!n.Running)
            {
                return;
            }
            int m = Math.Max(1, monthDays);
            long lvl = Level(level), connections = Connections(n), upkeep = 0;
            for (int d = 0; d < n.Count; d++)
            {
                if (n.Open(d, day))
                {
                    long before = (long)BuildCost(d, level) * BroadbandCatalogue.UpkeepPerMille / 1000;
                    upkeep += n.Scaled[d] ? EndScaleRules.Mix(before, (long)BuildCost(d, level, EndScaleRules.Full) * BroadbandCatalogue.UpkeepPerMilleScale / 1000, s) : before;
                }
            }
            upkeep = upkeep * Clamp(n.Maintenance, BroadbandCatalogue.MaintenanceLeast, BroadbandCatalogue.MaintenanceMost) / 1000;
            // by the connection before the scale, its crews' wages at it
            staff = Money(connections * BroadbandCatalogue.StaffCents * lvl / PriceRules.Start / m * (EndScaleRules.Full - s) / EndScaleRules.Full
                          + (s > 0 ? CrewCents(n) * BroadbandCatalogue.WageMonthDays / m * lvl / PriceRules.Start : 0));
            network = Money(connections * BroadbandCatalogue.NetworkCents * lvl / PriceRules.Start / m + upkeep * 100 / m);
            marketing = Clip(((long)MarketingMoney(n.Marketing, level) + m / 2) / m);
        }

        /// <summary>The installations of a month's new connections, money.</summary>
        public static int Installs(int before, int after, int level, int share = 0)
        {
            return Money((long)Math.Max(0, after - before) * EndScaleRules.Mix(BroadbandCatalogue.InstallCents, BroadbandCatalogue.InstallCentsScale, share) * Level(level) / PriceRules.Start);
        }

        /// <summary>A crew's people needed at the full scale: field and call centre by connections, operations by districts open.</summary>
        public static int CrewAtScale(Network n, int kind, int day)
        {
            if (n == null || !n.Running)
            {
                return 0;
            }
            if (kind == BroadbandCatalogue.Operations)
            {
                int open = 0;
                for (int d = 0; d < n.Count; d++)
                {
                    open += n.Open(d, day) ? 1 : 0;
                }
                return open * BroadbandCatalogue.OperationsPerDistrict;
            }
            return EndScaleRules.Per(Connections(n), kind == BroadbandCatalogue.Field ? BroadbandCatalogue.FieldConnections : BroadbandCatalogue.CallConnections);
        }

        public static int CrewNeed(Network n, int kind, int day) => EndScaleRules.Need(CrewAtScale(n, kind, day), n != null ? n.Scale : 0);

        /// <summary>A day's hiring: each crew a share nearer its need; the cover they give kept for the day.</summary>
        public static void Hire(Network n, int day)
        {
            int cover = EndScaleRules.Full;
            for (int k = 0; k < BroadbandCatalogue.CrewKinds; k++)
            {
                int need = CrewNeed(n, k, day);
                n.Crew[k] = EndScaleRules.Hire(n.Crew[k], need, BroadbandCatalogue.CrewPace);
                cover = Math.Min(cover, EndScaleRules.Cover(n.Crew[k], need));
            }
            n.Cover = cover;
        }

        /// <summary>A day's staff and network once charged, money: the base that cannot be refused and what the cash met of the rest.</summary>
        public struct DayCharge
        {
            public long Staff, Network;
            public int Unpaid;   // thousandths of what the scale added (and was owed) that went unpaid
        }

        /// <summary>
        /// A day's staff and network at scale: up to <paramref name="staff0"/> and <paramref name="network0"/> never refused, the
        /// rest as far as <paramref name="cash"/> goes; what is left is owed, and the crews leave in the unpaid share.
        /// </summary>
        public static DayCharge Charge(Network n, long cash, long takings, long staff, long network, long marketing, long staff0, long network0)
        {
            long baseStaff = Math.Min(staff, staff0), baseNetwork = Math.Min(network, network0);
            long room = cash + takings - baseStaff - baseNetwork - marketing;
            long staffDue = Math.Max(0, staff - staff0) + Math.Max(0, n.Owed), networkDue = Math.Max(0, network - network0);
            long staffPaid = EndScaleRules.Take(ref room, staffDue), networkPaid = EndScaleRules.Take(ref room, networkDue);
            n.Owed = staffDue + networkDue - staffPaid - networkPaid;
            int unpaid = EndScaleRules.Unpaid(staffDue + networkDue, staffPaid + networkPaid);
            if (unpaid > 0)
            {
                Leave(n, unpaid);
            }
            return new DayCharge { Staff = baseStaff + staffPaid, Network = baseNetwork + networkPaid, Unpaid = unpaid };
        }

        /// <summary>A month's installations: the cost before the scale always; the scale's extra as far as the cash goes, the rest owed.</summary>
        public static long ChargeInstalls(Network n, long cash, int before, int after, int level, int share)
        {
            long base_ = Installs(before, after, level, 0), extra = Math.Max(0, Installs(before, after, level, share) - base_);
            long room = cash - base_;
            long paid = EndScaleRules.Take(ref room, extra);
            n.Owed += extra - paid;
            return base_ + paid;
        }

        /// <summary>A day's costs at scale went partly unpaid: each crew loses that share of its people.</summary>
        public static void Leave(Network n, int unpaidPerMille)
        {
            for (int k = 0; k < BroadbandCatalogue.CrewKinds; k++)
            {
                n.Crew[k] = EndScaleRules.Leave(n.Crew[k], unpaidPerMille);
            }
        }

        /// <summary>The crews' wages a day in a WageMonthDays month, cents at the start's prices.</summary>
        public static long CrewCents(Network n)
        {
            long sum = 0;
            for (int k = 0; k < BroadbandCatalogue.CrewKinds; k++)
            {
                sum += (long)Math.Max(0, n.Crew[k]) * BroadbandCatalogue.CrewWage[k];
            }
            return sum;
        }

        /// <summary>A month's books closed: its profit recorded (the last HistoryMonths), the figures kept for the app.</summary>
        public static void CloseMonth(Network n, int connections, int gained)
        {
            int profit = n.MonthTakings - n.MonthCosts;
            if (n.HistoryCount < n.History.Length)
            {
                n.History[n.HistoryCount++] = profit;
            }
            else
            {
                Array.Copy(n.History, 1, n.History, 0, n.History.Length - 1);
                n.History[n.History.Length - 1] = profit;
            }
            n.LastTakings = n.MonthTakings;
            n.LastCosts = n.MonthCosts;
            n.LastConnections = connections;
            n.LastGained = gained;
            n.MonthTakings = n.MonthCosts = 0;
        }

        /// <summary>The last year's profit, money.</summary>
        public static long YearProfit(Network n)
        {
            long sum = 0;
            for (int i = 0; i < n.HistoryCount && i < n.History.Length; i++)
            {
                sum += n.History[i];
            }
            return sum;
        }

        /// <summary>What a buyer pays, money: years of the last year's profit (fewer at scale), or a share of the network's cost if more.</summary>
        public static long SaleValue(Network n)
        {
            long spent = 0;
            for (int d = 0; d < n.Count; d++)
            {
                spent += n.Spent[d];
            }
            long earnings = Math.Max(0L, YearProfit(n)) * EndScaleRules.Mix(BroadbandCatalogue.SaleMultiple, BroadbandCatalogue.SaleMultipleScale, n.Scale);
            return Math.Max(earnings, spent * BroadbandCatalogue.SaleFloorShare / 1000);
        }

        /// <summary>What the player gets for the company: what a buyer pays less what it owes, money.</summary>
        public static long SaleNet(Network n) => Math.Max(0L, SaleValue(n) - Math.Max(0L, n.Owed));

        /// <summary>A rival's most this month, thousandths of its list: its highest less the company's share of its markets, never under its floor.</summary>
        public static int AnswerCap(Network n, int rival)
        {
            if (n == null || !n.Exists || rival < 0 || rival >= Network.Own)
            {
                return InternetCatalogue.PromoMost;
            }
            long own = 0, total = 0;
            for (int d = 0; d < n.Count; d++)
            {
                if (InternetRules.Reaches(rival, n.Id[d]))
                {
                    own += (long)n.Share[d, Network.Own] * MarketOf(n, d);
                    total += 1000L * MarketOf(n, d);
                }
            }
            int share = total > 0 ? (int)(own * 1000 / total) : 0;
            return Math.Max(InternetCatalogue.PromoLeast, InternetCatalogue.PromoMost - share * BroadbandCatalogue.Answer / 1000);
        }

        /// <summary>The company as a provider today (InternetCatalogue.Own): where open, at its reliability; sold, its buyer's; none, nowhere.</summary>
        public static void Apply(Network n, int day)
        {
            Provider own = InternetCatalogue.Own;
            bool sold = n != null && n.State == CompanyState.Sold;
            int count = 0;
            for (int d = 0; n != null && n.Exists && d < n.Count; d++)
            {
                count += n.Open(d, day) ? 1 : 0;
            }
            string[] reach = count == 0 ? None : new string[count];
            for (int d = 0, i = 0; count > 0 && d < n.Count; d++)
            {
                if (n.Open(d, day))
                {
                    reach[i++] = n.Id[d];
                }
            }
            own.Districts = reach;
            InternetCatalogue.OwnLine.Districts = sold ? None : reach;   // the company's own line for a data centre goes with the company
            own.NameKey = sold ? BroadbandCatalogue.SoldNameKey : BroadbandCatalogue.OwnNameKey;
            own.OutagePerMille = n != null && n.Running ? OutagePerMille(n.Maintenance) * EndScaleRules.Outages(n.Cover) / 1000 : BroadbandCatalogue.OutageBase;
            for (int k = 0; k < own.MonthCents.Length; k++)
            {
                own.MonthCents[k] = sold ? BroadbandCatalogue.DefaultCents[k] : 0;
            }
        }

        private static int Money(long cents)
        {
            return Clip((cents + 50) / 100);
        }

        private static long Level(int level)
        {
            return Math.Max(PriceRules.Least, Math.Min(PriceRules.Most, level));
        }

        private static int Clip(long value)
        {
            return (int)Math.Max(int.MinValue / 4, Math.Min(int.MaxValue / 4, value));
        }

        private static int Clamp(int value, int least, int most)
        {
            return value < least ? least : value > most ? most : value;
        }
    }
}
