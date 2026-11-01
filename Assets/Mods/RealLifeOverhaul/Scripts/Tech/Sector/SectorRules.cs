using System;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The rules of the tech firms other than the game studio under Living population (plans/tech-overhaul DRAFT2 section 3):
    /// one market for each kind of product and buyer, sized from the city's people and firms with the world they stand for,
    /// shared by every product in it, the player's and the rivals', by <see cref="ScaleRules.Share"/>; the curves that take
    /// the place of the old clamps (appeal, awareness from marketing, quality, reputation's top, the boosts); a product's
    /// day in its market. The rivals are in SectorRules.Rivals, IT services in SectorRules.Work, data centres and the cloud
    /// in SectorRules.Infra, the desk sessions in SectorRules.Desk. Whole numbers and seeded rolls only, so every machine
    /// gets the same. Game-free: EconomyCheck's SectorChecks hold it.
    /// </summary>
    public static partial class SectorRules
    {
        public const int NoteShipped = 1, NoteCut = 2, NoteNew = 3, NoteRaised = 4, NoteOutage = 5;
        public const int AnchorDays = 365;           // days an older save's market anchor fades over to the city's own size

        // ---------------------------------------------------------------- which market

        /// <summary>The market a product sells in (SectorMarket), or -1 for a game (the studio's genres, or the firms' own rules).</summary>
        public static int MarketOf(ProductKind kind, int segment)
        {
            int s = Clamp(segment, 0, 2);
            switch (kind)
            {
                case ProductKind.Saas: return s;
                case ProductKind.App: return (int)SectorMarket.Apps;
                case ProductKind.Licence: return (int)SectorMarket.Licences;
                case ProductKind.Model: return (int)SectorMarket.ConsumerAi + s;
                default: return -1;
            }
        }

        public static int MarketOf(Product p) => p != null ? MarketOf(p.K, p.Segment) : -1;

        /// <summary>Who buys in a market (SectorBuyers), which is also the segment its products are sold to.</summary>
        public static int BuyersOf(int market)
        {
            switch ((SectorMarket)Clamp(market, 0, (int)SectorMarket.Count - 1))
            {
                case SectorMarket.ConsumerSoftware:
                case SectorMarket.Apps:
                case SectorMarket.ConsumerAi:
                    return (int)SectorBuyers.People;
                case SectorMarket.BusinessSoftware:
                case SectorMarket.BusinessAi:
                    return (int)SectorBuyers.Businesses;
                default:
                    return (int)SectorBuyers.Enterprises;
            }
        }

        // ---------------------------------------------------------------- the city a market is sized from

        /// <summary>
        /// The city's firms by size from the population's employers (their kind, PopulationState's EmployerKind, and their
        /// jobs): a business of the game's or a special service is one firm of its size; an office building's other floors are
        /// many firms in Manhattan's office mix; a public body buys as a large organisation from its size up; the mod's own
        /// companies are large; a residential building's staff buy no software. Whole numbers by thousandths of a firm.
        /// </summary>
        public static void FirmsOf(byte[] kinds, int[] slots, int count, out int small, out int mid, out int large)
        {
            long s = 0, m = 0, l = 0;
            int n = Math.Min(count, Math.Min(kinds != null ? kinds.Length : 0, slots != null ? slots.Length : 0));
            for (int e = 0; e < n; e++)
            {
                long jobs = slots[e];
                if (jobs <= 0)
                {
                    continue;
                }
                switch ((EmployerKind)kinds[e])
                {
                    case EmployerKind.Business:
                    case EmployerKind.Special:
                        if (jobs >= SectorCatalogue.LargeLeast)
                        {
                            l += 1000;
                        }
                        else if (jobs >= SectorCatalogue.SmallLeast)
                        {
                            m += 1000;
                        }
                        else
                        {
                            s += 1000;
                        }
                        break;
                    case EmployerKind.Tenants:
                        l += jobs * SectorCatalogue.TenantLargeShare / SectorCatalogue.LargeFirmJobs;
                        m += jobs * SectorCatalogue.TenantMidShare / SectorCatalogue.MidFirmJobs;
                        s += jobs * (1000 - SectorCatalogue.TenantLargeShare - SectorCatalogue.TenantMidShare) / SectorCatalogue.SmallFirmJobs;
                        break;
                    case EmployerKind.Public:
                        if (jobs >= SectorCatalogue.PublicLargeLeast)
                        {
                            l += 1000;
                        }
                        else
                        {
                            m += 1000;
                        }
                        break;
                    case EmployerKind.Mod:
                        l += 1000;
                        break;
                }
            }
            small = (int)Math.Min(int.MaxValue, (s + 500) / 1000);
            mid = (int)Math.Min(int.MaxValue, (m + 500) / 1000);
            large = (int)Math.Min(int.MaxValue, (l + 500) / 1000);
        }

        /// <summary>A day's digital spending a person, thousandths of a cent: what the first day's is kept as (SectorFile.DigitalAnchor).</summary>
        public static long DigitalPerPerson(long cents, int people) => people > 0 && cents > 0 ? cents * 1000 / people : 0;

        /// <summary>The households' digital spending a person against the first day's, thousandths within the bounds; 1000 with nothing to go by.</summary>
        public static int Digital(long cents, int people, long anchor)
        {
            long now = DigitalPerPerson(cents, people);
            if (now <= 0 || anchor <= 0)
            {
                return 1000;
            }
            return (int)Math.Max(SectorCatalogue.DigitalLeast, Math.Min(SectorCatalogue.DigitalMost, now * 1000 / anchor));
        }

        /// <summary>
        /// How many buyers beyond the city each of the city's stands for after so many days, thousandths: the world factor,
        /// grown by its rate each of the save's years of <paramref name="year"/> days and filling towards its ceiling
        /// (ScaleRules.Grown). Whole numbers, the same everywhere.
        /// </summary>
        public static long WorldAt(int who, int days, int year)
        {
            int w = Clamp(who, 0, (int)SectorBuyers.Count - 1);
            return ScaleRules.Grown(SectorCatalogue.World[w], SectorCatalogue.WorldGrowth[w], SectorCatalogue.WorldCeiling[w], days, year);
        }

        /// <summary>
        /// What the economy does to a market's buyers today, thousandths: the households' mood for digital spending for
        /// people (the cycle while it is not read), the cycle for firms.
        /// </summary>
        public static int SpendOf(int market, SectorCityFigures city, int cycle)
        {
            int cyc = FirmsRules.CycleSpend[Clamp(cycle, 0, 3)];
            if (BuyersOf(market) != (int)SectorBuyers.People)
            {
                return cyc;
            }
            return city.Known ? Clamp(city.Digital, SectorCatalogue.DigitalLeast, SectorCatalogue.DigitalMost) : cyc;
        }

        /// <summary>
        /// The buyers of a market after so many days of the sector in a calendar of <paramref name="year"/> days a year: the
        /// city's people, or its small and mid-sized businesses, or its large organisations, times the world they stand for,
        /// times what the economy does to them. No cap: the city and the world are the market.
        /// </summary>
        public static long Buyers(int market, SectorCityFigures city, int days, int cycle, int year)
        {
            int who = BuyersOf(market);
            long here = who == (int)SectorBuyers.People ? city.People : who == (int)SectorBuyers.Businesses ? (long)city.Small + city.Mid : city.Large;
            return Math.Max(0L, here * WorldAt(who, days, year) / 1000 * SpendOf(market, city, cycle) / 1000);
        }

        /// <summary>A market's anchor for an older save's customers today: what it was set at, fading straight to 1000 over <see cref="AnchorDays"/>.</summary>
        public static int AnchorNow(int anchor, int from, int day)
        {
            int gone = day - from;
            if (anchor <= 1000 || gone >= AnchorDays)
            {
                return 1000;
            }
            return anchor - (int)((long)(anchor - 1000) * Math.Max(0, gone) / AnchorDays);
        }

        // ---------------------------------------------------------------- the curves in place of the clamps

        /// <summary>
        /// What a product's features do to its appeal against its market's going features, thousandths: 2r/(1+r) of the
        /// ratio, so the going features are 1000, twice as many 1333, a third as many 500, and no clamp at either end.
        /// </summary>
        public static int FeatureAppeal(int features, int going)
        {
            long r = (Math.Max(0L, features) + 1) * 1000000L / (Math.Max(0L, going) + 1);   // the ratio in millionths
            return (int)((2L * SectorCatalogue.FeatureCurve * r + (1000000 + r) / 2) / (1000000 + r));
        }

        /// <summary>How sound a product is for its bugs, thousandths: all of it with none, half at half its room, a third at its room, never nothing.</summary>
        public static int Soundness(int bugs, int room)
        {
            long half = Math.Max(1, room / 2);
            return (int)Math.Max(1L, 1000L * half / (half + Math.Max(0, bugs)));
        }

        /// <summary>A gain with diminishing returns: none at nothing, half the gain at <paramref name="half"/>, nearer the gain the more there is, never past it.</summary>
        public static int Lift(long amount, long half, int gain)
        {
            if (amount <= 0 || gain <= 0)
            {
                return 0;
            }
            return (int)((long)gain * amount / (amount + Math.Max(1L, half)));
        }

        /// <summary>A firm's reliability or security posture from what its people bring (thousandths summed, uncapped): its base with none, rising towards 1000 by the curve.</summary>
        public static int Posture(int baseline, long brought, int half) => baseline + Lift(brought, half, 1000 - baseline);

        /// <summary>What product people add to a feature's points, thousandths: a curve of their share of the build, half its gain at a tenth of the points.</summary>
        public static int ProductBoost(in Crew c)
        {
            long share = (long)Math.Max(0, c.Product) * 1000 / Math.Max(1, c.Engineering + c.Product);
            return Lift(share, 100, SectorCatalogue.ProductGain);
        }

        /// <summary>How a product's price weighs against its market's going price (both thousandths of the segment's list price): the money scale's curve.</summary>
        public static int PriceAppeal(int tier, int going) => ScaleRules.PriceAppeal(FirmsCatalogue.PriceTiers[Clamp(tier, 0, 2)], Math.Max(1, going));

        /// <summary>
        /// A product's appeal in its market, thousandths, with no clamp: its features against the market's going ones, how
        /// sound it is, its price against the going price, the firm's name, and what its designers lift it by.
        /// </summary>
        public static int Appeal(Product p, Firm firm, in Crew c, SectorMarketDay m)
        {
            long a = FeatureAppeal(p.Features, m.Average);
            a = a * Soundness(p.Bugs, FirmsRules.Allowance(p)) / 1000;
            a = a * PriceAppeal(p.Tier, m.Going) / 1000;
            a = a * (500 + Clamp(firm.Reputation, 0, 1000)) / 1000;
            a = a * (1000 + Lift(c.Design, c.Engineering + 1, SectorCatalogue.DesignGain)) / 1000;
            return (int)Math.Min(int.MaxValue / 4, a);
        }

        /// <summary>
        /// Thousandths of a market's buyers who know of a product: what a month of its marketing reaches (ScaleRules.Reach over the
        /// buyers, the floor with none), what its customers tell others, and a launch's lift; the most a market ever hears.
        /// </summary>
        public static int Awareness(long marketingDay, long buyers, long customers, int who, int boost)
        {
            if (buyers <= 0)
            {
                return 0;
            }
            int reach = ScaleRules.Reach(Math.Max(0L, marketingDay) * FirmsCatalogue.MonthDays, buyers);
            long told = Math.Max(0L, customers) * SectorCatalogue.WordOfMouth[Clamp(who, 0, (int)SectorBuyers.Count - 1)] / buyers;
            return (int)Math.Min(ScaleCatalogue.ReachMost, reach + told + Math.Max(0, boost));
        }

        /// <summary>A product's pull in its market: its appeal among those who know of it.</summary>
        public static long Attraction(int appeal, int awareness) => (long)Math.Max(0, appeal) * Math.Max(0, awareness) / 1000;

        /// <summary>The customers a product's share of its market comes to: the buyers times its share among every product's pull and buying none.</summary>
        public static long Target(SectorMarketDay m, long attraction) => m.Buyers * ScaleRules.Share(attraction, m.All) / 1000;

        // ---------------------------------------------------------------- a day of the markets

        /// <summary>
        /// A day's markets begun: each market's buyers from the city (an older save's anchor in), the economy's pull, and the
        /// rivals' features and prices counted in; the player's products are counted in next (<see cref="Count"/>), then every
        /// product's pull (<see cref="Attract"/>). COST: a pass over the eight markets and the rival products.
        /// </summary>
        public static void BeginDay(SectorFile f, int day, int cycle)
        {
            int days = Math.Max(0, day - f.FirstDay);
            for (int m = 0; m < f.Markets.Length; m++)
            {
                SectorMarketDay md = f.Markets[m];
                md.Clear();
                md.Who = BuyersOf(m);
                md.Spend = SpendOf(m, f.City, cycle);
                md.Buyers = Buyers(m, f.City, days, cycle, f.YearDays) * AnchorNow(f.Anchor[m], f.AnchorDay, day) / 1000;
            }
            for (int i = 0; i < f.Rivals.Count; i++)
            {
                RivalProduct r = f.Rivals[i];
                SectorMarketDay md = f.Markets[Clamp(r.Market, 0, f.Markets.Length - 1)];
                md.Features += Math.Max(0, r.Features);
                md.Count++;
                md.PriceSum += FirmsCatalogue.PriceTiers[Clamp(r.Tier, 0, 2)];
                md.Rivals++;
                md.RivalFeatures = Math.Max(md.RivalFeatures, r.Features);
                if (md.Leader < 0 || r.Customers > f.Rivals[md.Leader].Customers)
                {
                    md.Leader = i;
                }
            }
        }

        /// <summary>A product of the player's counted into its market: its features in the going features, its customers in the player's.</summary>
        public static void Count(SectorFile f, Product p)
        {
            int m = MarketOf(p);
            if (m < 0 || p.Stage >= 2)
            {
                return;
            }
            SectorMarketDay md = f.Markets[m];
            md.Features += Math.Max(0, p.Features);
            md.Count++;
            if (p.Live)
            {
                md.Mine += p.Users + (p.Segment == (int)Segment.Consumer ? p.Free : 0);
                md.MineFeatures = Math.Max(md.MineFeatures, p.Features);
            }
        }

        /// <summary>Every rival product's appeal, awareness and pull worked out and added to its market's (the player's products' are added by <see cref="Join"/>).</summary>
        public static void Attract(SectorFile f)
        {
            for (int i = 0; i < f.Rivals.Count; i++)
            {
                RivalProduct r = f.Rivals[i];
                SectorMarketDay md = f.Markets[Clamp(r.Market, 0, f.Markets.Length - 1)];
                r.Appeal = RivalAppeal(f, r, md);
                r.Awareness = Awareness(RivalMarketing(r), md.Buyers, r.Customers, md.Who, 0);
                r.Attraction = Attraction(r.Appeal, r.Awareness);
                md.All += r.Attraction;
            }
        }

        /// <summary>A product of the player's pull added to its market's.</summary>
        public static void Join(SectorMarketDay m, long attraction)
        {
            m.All += Math.Max(0L, attraction);
        }

        // ---------------------------------------------------------------- a product of the player's on sale

        /// <summary>
        /// A day on sale in a shared market (the place of FirmsRules.Sell under Living population): the product's customers
        /// move towards its share of the market's buyers (people faster, companies slower, sellers speeding businesses and
        /// companies), a licence's deals close at its pace while it is under its share, and those past their share drift to
        /// the products that took it; churn, takings, hosting and the risks (an outage, a breach) by the firms' own rules.
        /// The share is the only cap: no product has a market of its own.
        /// </summary>
        public static ProductDay SellDay(FirmsFile f, Firm firm, Product p, in Crew c, int support, int day, SectorMarketDay m, int appeal, int awareness, int hostingRate)
        {
            var r = new ProductDay();
            int seg = Clamp(p.Segment, 0, 2), who = Clamp(m.Who, 0, (int)SectorBuyers.Count - 1);
            bool consumer = seg == (int)Segment.Consumer;
            long target = Target(m, Attraction(appeal, awareness));
            long have = p.Users + (consumer ? p.Free : 0);
            r.Marketing = FirmsCatalogue.MarketingDay[Clamp(p.Marketing, 0, FirmsCatalogue.MarketingDay.Length - 1)];
            if (p.K == ProductKind.Licence)
            {
                long open = Math.Max(0L, target - p.Users);
                p.Carry = open > 0 ? p.Carry + LicenceRate(appeal, m.Spend, c.Selling) : Math.Min(p.Carry, 999999L);   // nothing is banked while its share is full
                long deals = Math.Min(open, p.Carry / 1000000);
                p.Carry -= deals * 1000000;
                r.Won = deals;
                r.Takings += deals * FirmsCatalogue.LicenceDeal * FirmsCatalogue.PriceTiers[Clamp(p.Tier, 0, 2)] / 1000;
            }
            else if (target > have)
            {
                long selling = consumer ? 1000 : 1000 + Lift(c.Selling, SectorCatalogue.SellingHalf, SectorCatalogue.SellingGain);
                long micro = (target - have) * SectorCatalogue.Pace[who] * selling / 1000 + p.Carry;   // millionths of a customer
                long whole = micro / 1000000;
                p.Carry = micro % 1000000;
                if (consumer)
                {
                    long paying = whole * FirmsCatalogue.ConsumerPaying / 1000;
                    int part = (int)(whole * FirmsCatalogue.ConsumerPaying % 1000);
                    if (part > 0 && TechRandom.Mille(f.Seed, p.Id, day, 19) < part)
                    {
                        paying++;
                    }
                    r.Won = paying;
                    p.Free += whole - paying;
                }
                else
                {
                    r.Won = whole;
                }
            }
            else
            {
                p.Carry = 0;
            }
            long churn = FirmsCatalogue.Churn[seg];
            if (p.K == ProductKind.Licence)
            {
                churn /= 3;
            }
            churn = churn * (2000 - Soundness(p.Bugs, FirmsRules.Allowance(p))) / 1000;
            int needed = FirmsRules.SupportNeeded(p);
            if (needed > support)
            {
                // the whole extra is reached only with nobody answering at all
                churn = churn * (1000 + Math.Min(FirmsCatalogue.SupportChurnMost, (needed - support) * 1000 / Math.Max(1, needed))) / 1000;
            }
            churn = churn * (p.Tier == 0 ? 900 : p.Tier == 2 ? 1150 : 1000) / 1000;
            int behind = m.Average - p.Features;
            if (behind > 0)
            {
                churn = churn * (1000 + Lift(behind, 10, 1000)) / 1000;
            }
            double lost = p.Users * churn / 1000.0 / FirmsCatalogue.MonthDays;
            long lostWhole = (long)lost;
            if (TechRandom.Mille(f.Seed, p.Id, day, 7) < (int)((lost - lostWhole) * 1000))
            {
                lostWhole++;
            }
            long overFree = 0;
            if (have > target)
            {
                long over = (have - target) * SectorCatalogue.Pace[who] / 1000000;   // past its share: they drift to the products that took it
                long overPaying = have > 0 ? over * p.Users / have : 0;
                lostWhole += overPaying;
                overFree = over - overPaying;
            }
            r.Lost = Math.Min(p.Users, lostWhole);
            p.Free -= Math.Min(p.Free, p.Free * churn * 2 / 1000 / FirmsCatalogue.MonthDays + overFree);
            p.Users = Math.Max(0, p.Users + r.Won - r.Lost);
            r.Takings += FirmsRules.MonthlyRevenue(p) / FirmsCatalogue.MonthDays;
            r.Hosting = (p.Users + p.Free / 10) * FirmsCatalogue.HostingCost[seg] * hostingRate / 1000 / FirmsCatalogue.MonthDays;
            Risks(f, firm, p, c, day, seg, ref r);
            p.Revenue += r.Takings;
            return r;
        }

        // An outage and a breach by the firms' own rules (FirmsRules.Sell's), the same rolls.
        private static void Risks(FirmsFile f, Firm firm, Product p, in Crew c, int day, int seg, ref ProductDay r)
        {
            long exposure = p.Users + p.Free;
            if (exposure <= 0)
            {
                return;
            }
            long outage = (long)FirmsCatalogue.OutageChance * 1000 * (1000 + firm.Debt) / 1000 * (1000 - c.Reliability) / 1000;
            if (TechRandom.Hash(f.Seed, p.Id, day, 11) % 1000000u < outage)
            {
                r.Outage = true;
                long gone = Math.Min(p.Users, p.Users * FirmsCatalogue.OutageChurn / 1000);
                p.Users -= gone;
                r.Lost += gone;
                if (seg == (int)Segment.Enterprise)
                {
                    r.Takings = Math.Max(0, r.Takings - FirmsRules.MonthlyRevenue(p) * FirmsCatalogue.SlaCredit / 1000);
                }
                firm.Outages++;
                firm.Reputation = Math.Max(0, firm.Reputation - 30);
            }
            long breach = (long)FirmsCatalogue.BreachChance * 1000 * (1000 - c.Security) / 1000 * Digits(exposure) / 3;
            if (TechRandom.Hash(f.Seed, p.Id, day, 13) % 1000000u < breach)
            {
                r.Breach = true;
                r.Affected = exposure;
                r.Fine = FirmsRules.Fine(exposure, firm.Revenue30 * 12, day < firm.RegisteredUntil);
                long gone = Math.Min(p.Users, p.Users * 50 / 1000);
                p.Users -= gone;
                r.Lost += gone;
                firm.Breaches++;
                firm.Reputation = Math.Max(0, firm.Reputation - 100);
            }
        }

        private static int Digits(long n)
        {
            int d = 1;
            while (n >= 10 && d < 9)
            {
                n /= 10;
                d++;
            }
            return d;
        }

        /// <summary>
        /// Millionths of a licence deal a day under Living population: so much a seller at the product's appeal and the economy,
        /// the enquiries that come by themselves and every seller the firm has (no top of eight: the product's share of the
        /// companies is the limit), as FirmsRules.LicenceRate otherwise.
        /// </summary>
        public static long LicenceRate(int appeal, int spend, int selling)
        {
            long sellers = FirmsCatalogue.LicenceInbound + (long)Math.Max(0, selling) * 1000 / FirmsCatalogue.PointsPerDay;
            return (long)FirmsCatalogue.LicenceLeadsPerSeller * 1000 * Math.Max(0, appeal) / 1000 * Math.Max(0, spend) / 1000 * sellers / 1000;
        }

        // ---------------------------------------------------------------- reputation with no top

        /// <summary>
        /// A firm's reputation moves towards what the soundness of its products and work earns it (thousandths): from 250 with
        /// nothing to show towards 1000 for work that is all sound, with no top of 800; a twentieth of the way a day at the
        /// bottom, a sixtieth near the top, at least a point while it is off.
        /// </summary>
        public static void Reputation(Firm firm, int sound)
        {
            int target = SectorCatalogue.ReputationBase + SectorCatalogue.ReputationSpan * Clamp(sound, 0, 1000) / 1000;
            firm.Reputation = Toward(firm.Reputation, target);
        }

        /// <summary>A name a step nearer its target by the reputation's pace (the same for the player's firms and the rivals' companies).</summary>
        public static int Toward(int reputation, int target)
        {
            int rep = Clamp(reputation, 0, 1000);
            int pace = SectorCatalogue.ReputationPace + (SectorCatalogue.ReputationSlow - SectorCatalogue.ReputationPace) * rep / 1000;
            int step = (target - rep) / Math.Max(1, pace);
            if (step == 0 && target != rep)
            {
                step = target > rep ? 1 : -1;
            }
            return Clamp(rep + step, 0, 1000);
        }

        public static int Clamp(int v, int least, int most) => v < least ? least : v > most ? most : v;
    }
}
