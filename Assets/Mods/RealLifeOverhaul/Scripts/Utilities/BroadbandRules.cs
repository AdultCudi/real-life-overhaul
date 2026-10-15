using System;

namespace RealLifeOverhaul.Utilities
{
    public enum CompanyState
    {
        None = 0,
        Running = 1,
        Sold = 2
    }

    /// <summary>The player's broadband company: its network, the city's market shares, its settings and its books.</summary>
    public sealed class Network
    {
        public const int Districts = 7;          // BroadbandCatalogue.Districts
        public const int Providers = 5;          // the four rivals (InternetCatalogue.Providers) and the company
        public const int Own = 4;

        public CompanyState State;
        public int FoundedDay, SoldDay;
        public long SalePrice;
        public readonly int[] Built = new int[Districts];               // the day a district's network opened; minus the day it will, while it is dug; 0 none
        public readonly int[] Spent = new int[Districts];               // what each district's network cost, money
        public readonly int[,] Share = new int[Districts, Providers];   // thousandths of each district's connections, by provider
        public int PriceShare = 1000;            // the company's prices, thousandths of the usual
        public int Maintenance = 1000;           // thousandths of the usual upkeep spent
        public int Marketing;                    // a budget of BroadbandCatalogue.Marketing
        public int MonthTakings, MonthCosts;     // this month so far, money
        public int LastTakings, LastCosts, LastConnections, LastGained;   // the last month's
        public readonly int[] History = new int[BroadbandCatalogue.HistoryMonths];   // each recorded month's profit, the oldest first
        public int HistoryCount;
        public int ScaleFrom = EndScaleRules.Unset;                     // the day the end game's scale began to come in for the company (Core/EndScale)
        public readonly bool[] Scaled = new bool[Districts];            // a district's network dug (or extended) at scale: it reaches the whole district
        public readonly int[] Crew = new int[BroadbandCatalogue.CrewKinds];   // its crews at scale: field engineers, call centre agents, network operations
        public long Owed;                                               // what the scale added to its costs that the cash could not meet, carried to the next day, money
        // ---- read each day, never saved
        public int Scale;                                               // the end game's share today, set by the company before the rules run
        public int Cover = EndScaleRules.Full;                          // how far its crews cover what they need, thousandths

        public bool Running => State == CompanyState.Running;
        public bool Exists => State != CompanyState.None;

        public bool Open(int district, int day)
        {
            return district >= 0 && district < Districts && Built[district] > 0 && Built[district] <= day;
        }

        public bool Digging(int district)
        {
            return district >= 0 && district < Districts && Built[district] < 0;
        }

        public int ReadyDay(int district)
        {
            return Digging(district) ? -Built[district] : 0;
        }
    }

    /// <summary>
    /// The rules of the player's broadband company (BroadbandCatalogue), game-free and checked by EconomyCheck: what it
    /// costs to found and build, how the city's market chooses among the providers of a district each month, what the
    /// company takes and spends each day, how the rivals answer it, and what a buyer pays. The market is calibrated on
    /// the city as it stands: with no newcomer and the rivals at their list prices nothing moves, so it is the company's
    /// arrival, its prices and the rivals' promotions that move it, a part of the way each month, so that customers are
    /// won and lost over months, never in a day. At the end game's scale (Core/EndScale, the network's Scale, 0 to 1000)
    /// a district's network is dug to every home of the city at its real size for a fortune, the company keeps crews of
    /// field engineers, call centre agents and network operations engineers (short of them it fails more and wins less),
    /// and its upkeep and installations cost what the real ones do; at a share of 0 every figure is as before. Cost:
    /// nothing per frame; a few sums a day and a month.
    /// </summary>
    public static class BroadbandRules
    {
        private static readonly string[] None = new string[0];

        public static int District(string id)
        {
            return id == null ? -1 : Array.IndexOf(BroadbandCatalogue.Districts, id);
        }

        /// <summary>An amount at the start's prices at today's, money (in longs: a network costs millions).</summary>
        public static int At(int amount, int level)
        {
            return Clip(((long)amount * Level(level) + PriceRules.Start / 2) / PriceRules.Start);
        }

        public static int LicenceFee(int level, int share = 0) => At(EndScaleRules.Mix(BroadbandCatalogue.LicenceFee, BroadbandCatalogue.LicenceFeeScale, share), level);

        /// <summary>What digging a district's network costs, money: before the scale its old figure; at it, every home and business of the district at its real size.</summary>
        public static int BuildCost(int district, int level, int share = 0)
        {
            if (district < 0 || district >= Network.Districts)
            {
                return 0;
            }
            long scale = (long)MarketAtScale(district) * (district == BroadbandCatalogue.FarDistrict ? BroadbandCatalogue.HomeCentsFarScale : BroadbandCatalogue.HomeCentsScale) / 100;
            return Clip((EndScaleRules.Mix((long)BroadbandCatalogue.BuildCost[district], scale, share) * Level(level) + PriceRules.Start / 2) / PriceRules.Start);
        }

        /// <summary>The least (or the most) a district's network costs to dig, money, for the Guide.</summary>
        public static int BuildCostRange(int level, int share, bool most)
        {
            int pick = BuildCost(0, level, share);
            for (int d = 1; d < Network.Districts; d++)
            {
                int cost = BuildCost(d, level, share);
                pick = most ? Math.Max(pick, cost) : Math.Min(pick, cost);
            }
            return pick;
        }

        /// <summary>A district's homes and businesses at the city's real size.</summary>
        public static int MarketAtScale(int district) => BroadbandCatalogue.Market[district] * BroadbandCatalogue.MarketScale / 1000;

        /// <summary>The connections a district's market has for the company: the old number, or (dug at scale) the real size as the scale's share has come in.</summary>
        public static int MarketOf(Network n, int district)
        {
            int before = BroadbandCatalogue.Market[district];
            return n != null && n.Scaled[district] ? EndScaleRules.Mix(before, MarketAtScale(district), n.Scale) : before;
        }

        /// <summary>
        /// What extending a district's network dug before the scale to the whole district costs, money: the scale's network
        /// less what it cost. 0 where there is nothing to extend (not open, already at scale, or no scale).
        /// </summary>
        public static int ExtendCost(Network n, int district, int level, int day, int buy)
        {
            if (n == null || !n.Running || buy <= 0 || district < 0 || district >= Network.Districts || !n.Open(district, day) || n.Scaled[district])
            {
                return 0;
            }
            return Math.Max(1, BuildCost(district, level, buy) - n.Spent[district]);
        }

        /// <summary>A district's network extended to the whole district: it is at scale from now, and what it cost is added to what it had cost.</summary>
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

        /// <summary>What one of the company's plans costs a month now, cents: its usual price at the company's prices and today's.</summary>
        public static int PlanCents(Network n, int plan, int level)
        {
            return PlanCents(n.PriceShare, plan, level);
        }

        /// <summary>What one of the company's plans costs a month at a share of its usual price (thousandths) and today's prices, cents.</summary>
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

        /// <summary>The company founded: the city's market as it stands before it (the rivals' shares where each reaches, by its hold), its settings as usual, its books empty.</summary>
        public static void Found(Network n, int day)
        {
            n.State = CompanyState.Running;
            n.FoundedDay = day;
            n.SoldDay = 0;
            n.SalePrice = 0;
            n.Owed = 0;
            for (int d = 0; d < Network.Districts; d++)
            {
                n.Built[d] = n.Spent[d] = 0;
                int total = 0;
                for (int p = 0; p < Network.Own; p++)
                {
                    total += InternetRules.Reaches(p, BroadbandCatalogue.Districts[d]) ? BroadbandCatalogue.RivalHold[p] : 0;
                }
                for (int p = 0; p < Network.Providers; p++)
                {
                    n.Share[d, p] = p < Network.Own && total > 0 && InternetRules.Reaches(p, BroadbandCatalogue.Districts[d]) ? BroadbandCatalogue.RivalHold[p] * 1000 / total : 0;
                }
                Whole(n, d);
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

        /// <summary>A district's network ordered: dug for BuildMonths of the game's months from a day, at a cost.</summary>
        public static void StartBuild(Network n, int district, int day, int monthDays, int cost, bool scaled = false)
        {
            n.Built[district] = -(day + BroadbandCatalogue.BuildMonths * Math.Max(1, monthDays));
            n.Spent[district] = Math.Max(0, cost);
            n.Scaled[district] = scaled;
        }

        /// <summary>The networks ready by a day opened on the day they were ready; how many opened.</summary>
        public static int Finish(Network n, int day)
        {
            int opened = 0;
            for (int d = 0; d < Network.Districts; d++)
            {
                if (n.Digging(d) && n.ReadyDay(d) <= day)
                {
                    n.Built[d] = n.ReadyDay(d);
                    opened++;
                }
            }
            return opened;
        }

        /// <summary>How far a district trusts the company's network, thousandths: a ninth when it opens there, all of it TrustMonths later.</summary>
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

        /// <summary>
        /// A month of the city's market: in each district every provider there has its weight (its hold, by its price
        /// against its usual; the company's grown with the district's trust, and by its reliability and marketing), and
        /// each share moves Follow of the way to its weight's share of the whole. promo: each rival's price this month,
        /// thousandths of its list. A company sold is run by its buyer at the usual prices, maintenance and no marketing.
        /// </summary>
        public static void Month(Network n, int[] promo, int day, int monthDays)
        {
            if (!n.Exists)
            {
                return;
            }
            var weight = new double[Network.Providers];
            double own = n.Running ? Pull(n) : 1.0;
            double e = BroadbandCatalogue.PriceElasticity / 1000.0;
            for (int d = 0; d < Network.Districts; d++)
            {
                double sum = 0.0;
                for (int p = 0; p < Network.Own; p++)
                {
                    int price = promo != null && p < promo.Length && promo[p] > 0 ? promo[p] : 1000;
                    weight[p] = InternetRules.Reaches(p, BroadbandCatalogue.Districts[d]) ? BroadbandCatalogue.RivalHold[p] * Math.Pow(1000.0 / price, e) : 0.0;
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

        /// <summary>What the company's own choices do to its weight: its prices (squared against the usual), its reliability, its marketing.</summary>
        public static double Pull(Network n)
        {
            double price = Math.Pow(1000.0 / Clamp(n.PriceShare, BroadbandCatalogue.PriceLeast, BroadbandCatalogue.PriceMost), BroadbandCatalogue.PriceElasticity / 1000.0);
            double reliability = Math.Pow((double)BroadbandCatalogue.OutageBase / OutagePerMille(n.Maintenance), BroadbandCatalogue.ReliabilityElasticity / 1000.0);
            int budget = BroadbandCatalogue.Marketing[Clamp(n.Marketing, 0, BroadbandCatalogue.Marketing.Length - 1)];
            double marketing = 1.0 + BroadbandCatalogue.MarketingPull / 1000.0 * Math.Log(1.0 + (double)budget / BroadbandCatalogue.MarketingUnit);
            return price * reliability * marketing * EndScaleRules.Working(n.Cover) / 1000.0;
        }

        // A district's shares made to sum to a thousand (the rounding goes to the largest).
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
            for (int d = 0; d < Network.Districts; d++)
            {
                if (district < 0 || district == d)
                {
                    total += (long)n.Share[d, Network.Own] * MarketOf(n, d) / 1000;
                }
            }
            return Clip(total);
        }

        /// <summary>
        /// A day's takings, money: each connection's share of its month at the company's prices. Nothing on a day its
        /// network failed (its customers' credit), nor while it has no headquarters to bill them from.
        /// </summary>
        public static int DayTakings(Network n, int level, int monthDays, bool outage, bool billing)
        {
            if (!n.Running || outage || !billing)
            {
                return 0;
            }
            return Money((long)Connections(n) * AverageCents(n, level) / Math.Max(1, monthDays) * EndScaleRules.Mix(1000, BroadbandCatalogue.IncomeAtScale, n.Scale) / 1000);
        }

        /// <summary>
        /// A day's costs, money: its staff (engineers and the call centre for every connection), its network (the backbone
        /// and boxes for every connection, and the upkeep of what is open by the maintenance spent) and its marketing.
        /// </summary>
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
            for (int d = 0; d < Network.Districts; d++)
            {
                if (n.Open(d, day))
                {
                    long before = (long)BuildCost(d, level) * BroadbandCatalogue.UpkeepPerMille / 1000;
                    upkeep += n.Scaled[d] ? EndScaleRules.Mix(before, (long)BuildCost(d, level, EndScaleRules.Full) * BroadbandCatalogue.UpkeepPerMilleScale / 1000, s) : before;
                }
            }
            upkeep = upkeep * Clamp(n.Maintenance, BroadbandCatalogue.MaintenanceLeast, BroadbandCatalogue.MaintenanceMost) / 1000;
            // its engineers and call centre: by the connection before the scale, its crews' wages at it
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

        // ---------------------------------------------------------------- the crews at scale

        /// <summary>The people of a crew the company needs at the full scale: field engineers and call centre agents by its connections, network operations engineers by the districts open.</summary>
        public static int CrewAtScale(Network n, int kind, int day)
        {
            if (n == null || !n.Running)
            {
                return 0;
            }
            if (kind == BroadbandCatalogue.Operations)
            {
                int open = 0;
                for (int d = 0; d < Network.Districts; d++)
                {
                    open += n.Open(d, day) ? 1 : 0;
                }
                return open * BroadbandCatalogue.OperationsPerDistrict;
            }
            return EndScaleRules.Per(Connections(n), kind == BroadbandCatalogue.Field ? BroadbandCatalogue.FieldConnections : BroadbandCatalogue.CallConnections);
        }

        public static int CrewNeed(Network n, int kind, int day) => EndScaleRules.Need(CrewAtScale(n, kind, day), n != null ? n.Scale : 0);

        /// <summary>A day's hiring: each crew a share of its need nearer it (or down to it); the cover they give is kept for the day.</summary>
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

        /// <summary>What a day's staff and network come to once charged: the base that cannot be refused and what the cash met of the rest, money.</summary>
        public struct DayCharge
        {
            public long Staff, Network;
            public int Unpaid;          // thousandths of what the scale added (and was owed) that went unpaid
        }

        /// <summary>
        /// A day's staff and network charged at the end game's scale, after the day's takings: the lesser of each and its
        /// figure before the scale is the base that cannot be refused (at scale the crews' wages, not the old cost per
        /// connection over the larger city); what each is over its figure before, and what was owed from before, is taken
        /// only as far as the cash goes. What is left is owed to the next day, and the crews leave in the unpaid share.
        /// cash: the player's money before the day's bookings; staff0, network0: the day's figures at a share of 0.
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

        /// <summary>
        /// A month's installations charged: what they cost before the scale cannot be refused; what the scale adds is taken
        /// only as far as the cash goes, and the rest is owed to the next day. Money.
        /// </summary>
        public static long ChargeInstalls(Network n, long cash, int before, int after, int level, int share)
        {
            long base_ = Installs(before, after, level, 0), extra = Math.Max(0, Installs(before, after, level, share) - base_);
            long room = cash - base_;
            long paid = EndScaleRules.Take(ref room, extra);
            n.Owed += extra - paid;
            return base_ + paid;
        }

        /// <summary>A day whose costs at scale went partly unpaid: each crew loses that share of its people (Core/EndScale).</summary>
        public static void Leave(Network n, int unpaidPerMille)
        {
            for (int k = 0; k < BroadbandCatalogue.CrewKinds; k++)
            {
                n.Crew[k] = EndScaleRules.Leave(n.Crew[k], unpaidPerMille);
            }
        }

        /// <summary>The crews' wages a day in a month of WageMonthDays, cents at the start's prices (a day of a month of m days: this by WageMonthDays / m).</summary>
        public static long CrewCents(Network n)
        {
            long sum = 0;
            for (int k = 0; k < BroadbandCatalogue.CrewKinds; k++)
            {
                sum += (long)Math.Max(0, n.Crew[k]) * BroadbandCatalogue.CrewWage[k];
            }
            return sum;
        }

        /// <summary>A month's books closed: its profit recorded (the last HistoryMonths kept), the figures kept for the app, and a new month begun.</summary>
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

        /// <summary>The last year's profit (the months recorded, at most HistoryMonths), money.</summary>
        public static long YearProfit(Network n)
        {
            long sum = 0;
            for (int i = 0; i < n.HistoryCount && i < n.History.Length; i++)
            {
                sum += n.History[i];
            }
            return sum;
        }

        /// <summary>
        /// What a buyer pays: SaleMultiple years of the last year's profit (fewer at the end game's scale, so that digging
        /// a network to sell never beats running it), or a share of what the network cost if that is more; money, in a
        /// long (a network at scale is worth hundreds of millions).
        /// </summary>
        public static long SaleValue(Network n)
        {
            long spent = 0;
            for (int d = 0; d < Network.Districts; d++)
            {
                spent += n.Spent[d];
            }
            long earnings = Math.Max(0L, YearProfit(n)) * EndScaleRules.Mix(BroadbandCatalogue.SaleMultiple, BroadbandCatalogue.SaleMultipleScale, n.Scale);
            return Math.Max(earnings, spent * BroadbandCatalogue.SaleFloorShare / 1000);
        }

        /// <summary>What the player gets for the company: what a buyer pays, less what it owes (settled out of the price); money.</summary>
        public static long SaleNet(Network n) => Math.Max(0L, SaleValue(n) - Math.Max(0L, n.Owed));

        /// <summary>
        /// The most a rival may charge this month, thousandths of its list: its highest, less the company's share of the
        /// rival's markets (Answer), never under the rival's floor. A rival the company has not reached charges as it likes.
        /// </summary>
        public static int AnswerCap(Network n, int rival)
        {
            if (n == null || !n.Exists || rival < 0 || rival >= Network.Own)
            {
                return InternetCatalogue.PromoMost;
            }
            long own = 0, total = 0;
            for (int d = 0; d < Network.Districts; d++)
            {
                if (InternetRules.Reaches(rival, BroadbandCatalogue.Districts[d]))
                {
                    own += (long)n.Share[d, Network.Own] * MarketOf(n, d);
                    total += 1000L * MarketOf(n, d);
                }
            }
            int share = total > 0 ? (int)(own * 1000 / total) : 0;
            return Math.Max(InternetCatalogue.PromoLeast, InternetCatalogue.PromoMost - share * BroadbandCatalogue.Answer / 1000);
        }

        /// <summary>
        /// The company as an internet provider (InternetCatalogue.Own), as the network stands on a day: it reaches where
        /// it is open; the player's own places are connected free, and fail as often as its maintenance allows. Sold, it
        /// is its buyer's, at the usual prices and maintenance. With no company (or none in this save) it reaches nowhere.
        /// </summary>
        public static void Apply(Network n, int day)
        {
            Provider own = InternetCatalogue.Own;
            bool sold = n != null && n.State == CompanyState.Sold;
            int count = 0;
            for (int d = 0; n != null && n.Exists && d < Network.Districts; d++)
            {
                count += n.Open(d, day) ? 1 : 0;
            }
            string[] reach = count == 0 ? None : new string[count];
            for (int d = 0, i = 0; count > 0 && d < Network.Districts; d++)
            {
                if (n.Open(d, day))
                {
                    reach[i++] = BroadbandCatalogue.Districts[d];
                }
            }
            own.Districts = reach;
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
