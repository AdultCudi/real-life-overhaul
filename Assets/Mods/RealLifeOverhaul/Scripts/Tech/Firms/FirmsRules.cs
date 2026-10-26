using System;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What a contract's day came to, for the director to tell and to book.</summary>
    public struct WorkNote
    {
        public int Contract;
        public int Kind;             // 1 done, 2 failed, 3 billed, 4 offered, 5 tender won, 6 tender lost, 7 a tender put out, 8 lapsed
        public long Amount;          // cents paid in (or a penalty taken off)
    }

    /// <summary>
    /// The rules of tech firms: who does the work, a product's day of building and selling, reputation, value and relief;
    /// contracts are in FirmsRules.Contracts. Game-free; every roll is <see cref="TechRandom"/>'s.
    /// </summary>
    public static partial class FirmsRules
    {
        public const int NoteDone = 1, NoteFailed = 2, NoteBilled = 3, NoteOffered = 4, NoteWon = 5, NoteLostTender = 6, NoteTender = 7, NoteLapsed = 8;

        /// <summary>What the city's cycle does to spending on software, thousandths, boom to recession.</summary>
        public static readonly int[] CycleSpend = { 1100, 1000, 920, 820 };

        public static bool Makes(FirmKind firm, ProductKind kind) => (FirmsCatalogue.MakesProducts[(int)firm] & (1 << (int)kind)) != 0;

        public static bool Takes(FirmKind firm, WorkKind kind) => (FirmsCatalogue.TakesWork[(int)firm] & (1 << (int)kind)) != 0;

        /// <summary>True for a role whose points build products and contract work: engineering, data, security and infrastructure, but not testers.</summary>
        public static bool Builds(RoleDef r)
        {
            return r != null && r.Id != TalentCatalogue.QaTester
                   && (r.Family == Family.Engineering || r.Family == Family.Data || r.Family == Family.Security || r.Family == Family.Infrastructure);
        }

        /// <summary>True for a role that can work on a kind of contract: designers on branding, builders on the rest, with web designers on a website.</summary>
        public static bool CanDo(RoleDef r, WorkKind kind)
        {
            if (r == null)
            {
                return false;
            }
            if (kind == WorkKind.Branding)
            {
                return r.Family == Family.Design;
            }
            return Builds(r) || (kind == WorkKind.Website && r.Id == TalentCatalogue.WebDesigner);
        }

        /// <summary>True for a role that does any of a firm's work: its products, or a kind of contract it takes.</summary>
        public static bool WorksFor(RoleDef r, FirmKind firm)
        {
            if (FirmsCatalogue.MakesProducts[(int)firm] != 0 && Builds(r))
            {
                return true;
            }
            for (int k = 0; k < (int)WorkKind.Count; k++)
            {
                if (Takes(firm, (WorkKind)k) && CanDo(r, (WorkKind)k))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The roles a firm of a kind wants first, a newcomer's role when their skill fits. Returns how many; none for other kinds.</summary>
        public static int Wanted(int kind, int[] into)
        {
            int n = 0;
            switch (kind)
            {
                case (int)FirmKind.Software:
                    into[n++] = TalentCatalogue.SoftwareEngineer;
                    into[n++] = TalentCatalogue.UxDesigner;
                    into[n++] = TalentCatalogue.CustomerSuccess;
                    break;
                case (int)FirmKind.Games:
                    into[n++] = TalentCatalogue.SoftwareEngineer;
                    into[n++] = TalentCatalogue.GraphicDesignerRole;
                    into[n++] = TalentCatalogue.CustomerSuccess;
                    break;
                case (int)FirmKind.Consultancy:
                    into[n++] = 2;   // back end
                    into[n++] = TalentCatalogue.ItSupport;
                    into[n++] = TalentCatalogue.UxDesigner;
                    break;
                case (int)FirmKind.Security:
                    into[n++] = TalentCatalogue.SecurityAnalyst;
                    into[n++] = TalentCatalogue.ItSupport;
                    break;
                case (int)FirmKind.AiLab:
                    into[n++] = TalentCatalogue.MlEngineer;
                    into[n++] = TalentCatalogue.CustomerSuccess;
                    break;
            }
            return n;
        }

        /// <summary>Why nobody can work now, or (<paramref name="yesterday"/>) worked yesterday: the first thing to put right; None if somebody can.</summary>
        public static Idle Why(in FirmFacts f, bool yesterday)
        {
            if (yesterday && f.Worked > 0)
            {
                return Idle.None;
            }
            if (f.Staff <= 0)
            {
                return Idle.NoStaff;
            }
            if (f.Workers <= 0)
            {
                return Idle.NoWorkers;
            }
            if (f.Closed)
            {
                return f.Net == 1 ? Idle.NetNone : f.Net == 2 ? Idle.NetInstalling : f.Net == 3 ? Idle.NetOutage : f.Dark ? Idle.NoPower : Idle.Shut;
            }
            if (f.OpenDays <= 0)
            {
                return Idle.NoHours;
            }
            if (f.ShiftHours <= 0)
            {
                return f.Stations <= 0 ? Idle.NoStations : Idle.NoShifts;
            }
            if (!yesterday)
            {
                return Idle.None;
            }
            return f.ShiftsYesterday <= 0 ? Idle.DayOff : Idle.Away;
        }

        /// <summary>The points a contract gets from what the firm's builders and designers have left. Returns what was given.</summary>
        public static int Give(WorkKind kind, int need, ref int engineering, ref int design, ref int web)
        {
            need = Math.Max(0, need);
            if (kind == WorkKind.Branding)
            {
                int d = Math.Min(need, Math.Max(0, design));
                design -= d;
                web = Math.Min(web, design);
                return d;
            }
            int e = Math.Min(need, Math.Max(0, engineering));
            engineering -= e;
            int w = 0;
            if (kind == WorkKind.Website)
            {
                w = Math.Min(need - e, Math.Max(0, Math.Min(web, design)));
                web -= w;
                design -= w;
            }
            return e + w;
        }

        /// <summary>Work points a feature costs when so many are built.</summary>
        public static int FeatureCost(int built) => FirmsCatalogue.FeatureBase + FirmsCatalogue.FeatureStep * Math.Max(0, built);

        /// <summary>How a product's bugs weigh on it, thousandths of a sound one: 1000 bug-free, 300 at the worst.</summary>
        public static int Quality(Product p)
        {
            int allowance = Math.Max(1, p.Features) * FirmsCatalogue.BugsPerFeature * 2 + 10;
            return 1000 - Math.Min(700, (int)Math.Min(int.MaxValue, (long)p.Bugs * 1000 / allowance));
        }

        /// <summary>
        /// Thousandths of a day's work a kind of firm gets from its office's connection (NetSpeedRules at the kind's lean). A
        /// firm with no working connection reads 1000: the utilities close it by the hour (the one rule for a place without a
        /// connection), so its people's hours already say so, and a day read at the wrong moment never wipes out work done.
        /// </summary>
        public static int Connection(int kind, int mbps)
        {
            return mbps == 0 ? 1000 : NetSpeedRules.Factor(mbps, FirmsCatalogue.NetLean[TalentCatalogue.Clamp(kind, 0, FirmsCatalogue.NetLean.Length - 1)]);
        }

        /// <summary>The day's work points at a connection's share: the same points the office's spare hours share, not selling or support.</summary>
        public static void Connected(ref Crew c, int net)
        {
            if (net == 1000)
            {
                return;
            }
            net = Math.Max(0, net);
            c.Engineering = (int)((long)c.Engineering * net / 1000);
            c.Design = (int)((long)c.Design * net / 1000);
            c.Web = (int)((long)c.Web * net / 1000);
            c.Testing = (int)((long)c.Testing * net / 1000);
        }

        /// <summary>What product people add to a feature's points, thousandths: up to 400 with one for every few engineers.</summary>
        public static int ProductBoost(in Crew c) => Math.Min(400, c.Product * 3000 / Math.Max(1, c.Engineering + c.Product));

        /// <summary>A day of building: the points, split by the firm's focus, go to features, bug fixes and debt. Returns the features finished.</summary>
        public static int Build(Firm firm, Product p, in Crew c, int points, int testing)
        {
            int[] split = FirmsCatalogue.FocusSplit[TalentCatalogue.Clamp(firm.Focus, 0, (int)Focus.Count - 1)];
            long feature = (long)points * split[0] / 1000 * (1000 + ProductBoost(c)) / 1000 * 1000 / (1000 + firm.Debt);
            int fix = points * split[1] / 1000 / FirmsCatalogue.PointsPerFix + testing;   // a tester's point fixes a bug
            int debt = points * split[2] / 1000 * FirmsCatalogue.DebtPerPoint;
            int finished = 0;
            p.Bugs = (int)Math.Min(int.MaxValue, p.Bugs + feature * Math.Max(30, c.Faults) / 1000);
            p.Progress = (int)Math.Min(int.MaxValue, p.Progress + feature);
            if (p.K != ProductKind.Game)
            {
                while (p.Features < FirmsCatalogue.FeaturesMost && p.Progress >= FeatureCost(p.Features)
                       && (p.K != ProductKind.Model || p.Compute >= InfraRules.TrainNeed(p.Features)))
                {
                    if (p.K == ProductKind.Model)
                    {
                        p.Compute -= InfraRules.TrainNeed(p.Features);   // a model's feature is a generation's training too
                    }
                    p.Progress -= FeatureCost(p.Features);
                    p.Features++;
                    finished++;
                    firm.Debt = Math.Min(1000, firm.Debt + FirmsCatalogue.DebtPerFeature * (1000 - c.SeniorShare) / 1000);
                }
                if (p.Features >= FirmsCatalogue.FeaturesMost)
                {
                    p.Progress = 0;
                }
            }
            p.Bugs = Math.Max(0, p.Bugs - fix);
            firm.Debt = Math.Max(0, firm.Debt - debt);
            return finished;
        }

        /// <summary>True when a game's whole build is done and it can be launched.</summary>
        public static bool GameReady(Product p) =>
            p.K == ProductKind.Game && p.Stage == 0 && p.Progress >= FirmsCatalogue.GameScope[TalentCatalogue.Clamp(p.Scope, 0, 2)];

        public static bool CanLaunch(Product p) => p.Stage == 0 && (p.K == ProductKind.Game ? GameReady(p) : p.Features >= FirmsCatalogue.LaunchFeatures);

        /// <summary>How a product appeals against its rival's, thousandths: features, quality, price tier, reputation and design.</summary>
        public static int Appeal(Product p, Firm firm, in Crew c)
        {
            long a = 1000L * (p.Features + 1) / (p.RivalFeatures + 1);
            a = Math.Max(200, Math.Min(2000, a));
            a = a * Quality(p) / 1000;
            a = a * FirmsCatalogue.PriceAppeal[TalentCatalogue.Clamp(p.Tier, 0, 2)] / FirmsCatalogue.PriceAppeal[TalentCatalogue.Clamp(p.RivalTier, 0, 2)];
            a = a * (500 + firm.Reputation) / 1000;
            a = a * (1000 + Math.Min(200, c.Design * 1000 / Math.Max(1, c.Engineering + 1))) / 1000;
            return (int)Math.Max(50, Math.Min(3000, a));
        }

        /// <summary>A subscription's price a month for one customer at the tier chosen, cents.</summary>
        public static long MonthPrice(Product p)
        {
            return (long)FirmsCatalogue.Price[TalentCatalogue.Clamp(p.Segment, 0, 2)] * FirmsCatalogue.PriceTiers[TalentCatalogue.Clamp(p.Tier, 0, 2)] / 1000;
        }

        /// <summary>A product's recurring revenue a month, cents: subscriptions, an app's advertising, a licence's upkeep.</summary>
        public static long MonthlyRevenue(Product p)
        {
            switch (p.K)
            {
                case ProductKind.Licence:
                    return p.Users * FirmsCatalogue.LicenceDeal * FirmsCatalogue.LicenceMaintenance / 1000 / 12;
                case ProductKind.App:
                    return p.Users * MonthPrice(p) * (1000 - FirmsCatalogue.AppStoreCut) / 1000
                           + (p.Users + p.Free) * FirmsCatalogue.Price[0] * FirmsCatalogue.AppAds / 1000 / 20;
                case ProductKind.Game:
                    return 0;
                default:
                    return p.Users * MonthPrice(p);
            }
        }

        /// <summary>The support people a product needs: its tickets a day over what one person answers.</summary>
        public static int SupportNeeded(Product p)
        {
            long tickets = (p.Users + p.Free / 20) * FirmsCatalogue.Tickets[TalentCatalogue.Clamp(p.Segment, 0, 2)] / 1000;
            return (int)Math.Min(1000, (tickets + FirmsCatalogue.TicketsPerPerson - 1) / FirmsCatalogue.TicketsPerPerson);
        }

        /// <summary>A day on sale: customers won and lost, the takings and costs, and the risks (an outage, a breach, the rival shipping).</summary>
        public static ProductDay Sell(FirmsFile f, Firm firm, Product p, in Crew c, int support, int day, int cycle, int hostingRate)
        {
            var r = new ProductDay();
            int seg = TalentCatalogue.Clamp(p.Segment, 0, 2);
            int appeal = Appeal(p, firm, c);
            int reach = FirmsCatalogue.MarketingReach[TalentCatalogue.Clamp(p.Marketing, 0, FirmsCatalogue.MarketingReach.Length - 1)];
            int spend = CycleSpend[TalentCatalogue.Clamp(cycle, 0, 3)];
            r.Marketing = FirmsCatalogue.MarketingDay[TalentCatalogue.Clamp(p.Marketing, 0, FirmsCatalogue.MarketingDay.Length - 1)];
            if (p.K == ProductKind.Game)
            {
                int age = Math.Max(0, day - p.Launched);
                double decay = Math.Pow(0.5, age / (double)FirmsCatalogue.GameHalfLife);
                double units = FirmsCatalogue.GamePlayers * FirmsCatalogue.GameLaunchShare / 1e6 * appeal * (1000 + p.Hype) / 1000.0 * reach / 1000.0 * spend / 1000.0
                               * decay;
                long whole = (long)units;
                p.Carry += (long)((units - whole) * 1e6);
                whole += p.Carry / 1000000;
                p.Carry %= 1000000;
                r.Won = whole;
                p.Users += whole;
                r.Takings = whole * FirmsCatalogue.GamePrice[TalentCatalogue.Clamp(p.Scope, 0, 2)] * (1000 - FirmsCatalogue.AppStoreCut) / 1000;
                p.Revenue += r.Takings;
                return r;
            }
            long open = Math.Max(0, FirmsCatalogue.Market[seg] - p.Users - p.Free);
            int selling = seg == (int)Segment.Consumer ? 1000 : 1000 + Math.Min(2000, c.Selling * 50);
            if (p.K == ProductKind.Licence)
            {
                // The deals the day has earned, carried in millionths (Carry) until a whole one closes: a launched licence sells on
                // a known pace, never by a daily draw that at a launch's appeal left it unsold for months.
                p.Carry += open > 0 ? LicenceRate(appeal, spend, c.Selling) : 0;
                long deals = Math.Min(open, p.Carry / 1000000);
                p.Carry -= deals * 1000000;
                r.Won = deals;
                r.Takings += deals * FirmsCatalogue.LicenceDeal * FirmsCatalogue.PriceTiers[TalentCatalogue.Clamp(p.Tier, 0, 2)] / 1000;
            }
            else
            {
                double won = open * (double)FirmsCatalogue.Acquire[seg] / 1e6 * appeal / 1000.0 * reach / 1000.0 * selling / 1000.0 * spend / 1000.0;
                long whole = (long)won;
                p.Carry += (long)((won - whole) * 1e6);
                whole += p.Carry / 1000000;
                p.Carry %= 1000000;
                if (seg == (int)Segment.Consumer)
                {
                    // The paying share of the day's sign-ups, its part of one customer drawn for (a day of 19 sign-ups is 0.95 of a
                    // payer): cut off, a product winning fewer than twenty a day never had a paying customer at all.
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
            long churn = FirmsCatalogue.Churn[seg];
            if (p.K == ProductKind.Licence)
            {
                churn /= 3;
            }
            churn = churn * (2000 - Quality(p)) / 1000;
            int needed = SupportNeeded(p);
            if (needed > support)
            {
                churn = churn * (1000 + Math.Min(1000, (needed - support) * 1000 / Math.Max(1, needed))) / 1000;
            }
            churn = churn * (p.Tier == 0 ? 900 : p.Tier == 2 ? 1150 : 1000) / 1000;
            if (p.RivalFeatures > p.Features)
            {
                churn = churn * (1000 + Math.Min(1000, (p.RivalFeatures - p.Features) * 100)) / 1000;
            }
            double lost = p.Users * churn / 1000.0 / FirmsCatalogue.MonthDays;
            long lostWhole = (long)lost;
            if (TechRandom.Mille(f.Seed, p.Id, day, 7) < (int)((lost - lostWhole) * 1000))
            {
                lostWhole++;
            }
            r.Lost = Math.Min(p.Users, lostWhole);
            p.Free -= Math.Min(p.Free, p.Free * churn * 2 / 1000 / FirmsCatalogue.MonthDays);
            p.Users = Math.Max(0, p.Users + r.Won - r.Lost);
            r.Takings += MonthlyRevenue(p) / FirmsCatalogue.MonthDays;
            r.Hosting = (p.Users + p.Free / 10) * FirmsCatalogue.HostingCost[seg] * hostingRate / 1000 / FirmsCatalogue.MonthDays;
            long exposure = p.Users + p.Free;
            if (exposure > 0)
            {
                long outage = (long)FirmsCatalogue.OutageChance * 1000 * (1000 + firm.Debt) / 1000 * (1000 - c.Reliability) / 1000;
                if (TechRandom.Hash(f.Seed, p.Id, day, 11) % 1000000u < outage)
                {
                    r.Outage = true;
                    long gone = Math.Min(p.Users, p.Users * FirmsCatalogue.OutageChurn / 1000);
                    p.Users -= gone;
                    r.Lost += gone;
                    if (seg == (int)Segment.Enterprise)
                    {
                        r.Takings = Math.Max(0, r.Takings - MonthlyRevenue(p) * FirmsCatalogue.SlaCredit / 1000);
                    }
                    firm.Outages++;
                    firm.Reputation = Math.Max(0, firm.Reputation - 30);
                }
                long breach = (long)FirmsCatalogue.BreachChance * 1000 * (1000 - c.Security) / 1000 * Digits(exposure) / 3;
                if (TechRandom.Hash(f.Seed, p.Id, day, 13) % 1000000u < breach)
                {
                    r.Breach = true;
                    r.Affected = exposure;
                    r.Fine = Fine(exposure, firm.Revenue30 * 12, day < firm.RegisteredUntil);
                    long gone = Math.Min(p.Users, p.Users * 50 / 1000);
                    p.Users -= gone;
                    r.Lost += gone;
                    firm.Breaches++;
                    firm.Reputation = Math.Max(0, firm.Reputation - 100);
                }
            }
            if (TechRandom.Mille(f.Seed, p.Id, day, 17) < FirmsCatalogue.RivalLaunchChance && p.RivalFeatures < FirmsCatalogue.FeaturesMost)
            {
                p.RivalFeatures++;
                r.RivalShipped = true;
            }
            p.Revenue += r.Takings;
            return r;
        }

        /// <summary>
        /// Millionths of a licence deal a day: so much a seller at the product's appeal and the economy's spend, the firm's
        /// sellers counted by their points (one in good heart a day is one seller), and a firm with nobody selling still
        /// worth its enquiries (<see cref="FirmsCatalogue.LicenceInbound"/>).
        /// </summary>
        public static long LicenceRate(int appeal, int spend, int selling)
        {
            return (long)FirmsCatalogue.LicenceLeadsPerSeller * 1000 * Math.Max(0, appeal) / 1000 * Math.Max(0, spend) / 1000 * LicenceSellers(selling) / 1000;
        }

        /// <summary>The sellers a licence has, thousandths: the enquiries that come by themselves and the firm's selling points over a seller's day, eight at the most.</summary>
        public static int LicenceSellers(int selling)
        {
            long sellers = FirmsCatalogue.LicenceInbound + (long)Math.Max(0, selling) * 1000 / FirmsCatalogue.PointsPerDay;
            return (int)Math.Min(FirmsCatalogue.LicenceSellersMost, sellers);
        }

        /// <summary>Days a licence takes to close a deal at this pace, at least one; 0 when it closes none.</summary>
        public static int LicenceDealDays(long rate)
        {
            return rate <= 0 ? 0 : (int)Math.Min(9999L, Math.Max(1L, (1000000L + rate - 1) / rate));
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

        /// <summary>A data protection fine, cents: so much a customer, between the floor and a share of a year's revenue; more off the register.</summary>
        public static long Fine(long affected, long yearRevenue, bool registered)
        {
            long fine = affected * FirmsCatalogue.FinePerCustomer;
            long most = Math.Max(FirmsCatalogue.FineLeast, yearRevenue * FirmsCatalogue.FineShareOfYear / 1000);
            fine = Math.Max(FirmsCatalogue.FineLeast, Math.Min(most, fine));
            return registered ? fine : fine * FirmsCatalogue.UnregisteredFine / 1000;
        }

        public static void Launch(Product p, int day)
        {
            p.Stage = 1;
            p.Launched = day;
            p.LastRelease = day;
        }

        /// <summary>A game's hype before launch: each day of marketing adds more the more is spent.</summary>
        public static void Hype(Product p)
        {
            if (p.K == ProductKind.Game && p.Stage == 0)
            {
                p.Hype = Math.Min(2000, p.Hype + FirmsCatalogue.HypePerMarketingDay * TalentCatalogue.Clamp(p.Marketing, 0, 4) * 10);
            }
        }

        /// <summary>A firm's reputation drifts towards what its products and work earn it: a twentieth of the way a day.</summary>
        public static void Reputation(Firm firm, int quality)
        {
            int target = 300 + quality / 2;
            firm.Reputation = TalentCatalogue.Clamp(firm.Reputation + (target - firm.Reputation) / 20, 0, 1000);
        }

        public static string Name(int seed, int n)
        {
            return FirmsCatalogue.NameFirst[TechRandom.Range(seed, n, 61, 0, 0, FirmsCatalogue.NameFirst.Length - 1)]
                   + FirmsCatalogue.NameSecond[TechRandom.Range(seed, n, 62, 0, 0, FirmsCatalogue.NameSecond.Length - 1)];
        }

        /// <summary>Products being built or on sale: at one firm, or across all of them with no place.</summary>
        public static int LiveCount(FirmsFile f, string place)
        {
            int n = 0;
            for (int i = 0; f != null && i < f.Products.Count; i++)
            {
                n += f.Products[i].Stage < 2 && (place == null || f.Products[i].Place == place) ? 1 : 0;
            }
            return n;
        }

        public const int RoomYes = 0, RoomFirmFull = 1, RoomAllFull = 2;

        /// <summary>Whether a firm can start another product: RoomYes, or which limit stops it (the firm's own first).</summary>
        public static int Room(FirmsFile f, string place)
        {
            if (LiveCount(f, place) >= FirmsCatalogue.ProductsPerFirm)
            {
                return RoomFirmFull;
            }
            return LiveCount(f, null) >= FirmsCatalogue.ProductsMost ? RoomAllFull : RoomYes;
        }

        /// <summary>
        /// Drops the oldest withdrawn products (first in the list) beyond <see cref="FirmsCatalogue.WithdrawnKept"/>, never <paramref name="keep"/>,
        /// the one just withdrawn. Returns how many went.
        /// </summary>
        public static int TrimWithdrawn(FirmsFile f, Product keep = null)
        {
            int withdrawn = 0;
            for (int i = 0; i < f.Products.Count; i++)
            {
                withdrawn += f.Products[i].Stage >= 2 ? 1 : 0;
            }
            int gone = 0;
            for (int i = 0; i < f.Products.Count && withdrawn > FirmsCatalogue.WithdrawnKept; )
            {
                if (f.Products[i].Stage >= 2 && !ReferenceEquals(f.Products[i], keep))
                {
                    f.Products.RemoveAt(i);
                    withdrawn--;
                    gone++;
                }
                else
                {
                    i++;
                }
            }
            return gone;
        }

        /// <summary>A new product, being built; its rival is the market's for its kind and segment as it stands.</summary>
        public static Product NewProduct(FirmsFile f, string place, ProductKind kind, Segment segment, int scope, string name)
        {
            var p = new Product
            {
                Id = f.Next(), Place = place, Kind = (int)kind,
                Segment = kind == ProductKind.App || kind == ProductKind.Game ? (int)Segment.Consumer : kind == ProductKind.Licence ? (int)Segment.Enterprise : (int)segment,
                Scope = TalentCatalogue.Clamp(scope, 0, 2), Name = string.IsNullOrEmpty(name) ? Name(f.Seed, f.NextId) : name
            };
            p.RivalFeatures = FirmsCatalogue.RivalFeaturesAtStart[p.Segment];
            p.RivalTier = 1;
            p.Train = kind == ProductKind.Model ? 2 : 0;
            return p;
        }

        /// <summary>A firm's recurring revenue a year, cents: its products' monthly revenue times twelve.</summary>
        public static long Arr(FirmsFile f, string place)
        {
            long arr = 0;
            for (int i = 0; i < f.Products.Count; i++)
            {
                Product p = f.Products[i];
                arr += p.Place == place && p.Live ? MonthlyRevenue(p) * 12 : 0;
            }
            return arr;
        }

        /// <summary>What a firm is worth, cents: a year's recurring revenue by a multiple for growth and debt, plus three months' other takings.</summary>
        public static long Value(Firm firm, long arr, int growthBp)
        {
            int multiple = TalentCatalogue.Clamp(4000 + growthBp * 2 - firm.Debt * 2, 2000, 12000);
            long other = Math.Max(0, firm.Revenue30 - arr / 12) * 3;
            return arr * multiple / 1000 + other;
        }

        /// <summary>A quarter's research and development relief: the extra deduction given on its qualifying costs, cents.</summary>
        public static long Relief(long qualifying) => Math.Max(0, qualifying) * FirmsCatalogue.ReliefShare / 1000;

        /// <summary>A day's takings rolled into the firm's last thirty days.</summary>
        public static void Roll(Firm firm, int day, long takings)
        {
            int slot = ((day % FirmsCatalogue.MonthDays) + FirmsCatalogue.MonthDays) % FirmsCatalogue.MonthDays;
            firm.Revenue30 += takings - firm.Days[slot];
            firm.Days[slot] = takings;
        }

        /// <summary>
        /// A firm whose business is no longer the player's closes: its products go, the work it had under way fails on the day,
        /// and its record goes. Returns the products taken out.
        /// </summary>
        public static int Close(FirmsFile f, Firm firm, int day)
        {
            if (f == null || firm == null)
            {
                return 0;
            }
            int gone = 0;
            for (int i = f.Products.Count - 1; i >= 0; i--)
            {
                if (f.Products[i].Place == firm.Place)
                {
                    f.Products.RemoveAt(i);
                    gone++;
                }
            }
            for (int c = 0; c < f.Contracts.Count; c++)
            {
                Contract w = f.Contracts[c];
                if (w.Place == firm.Place && w.Stage <= (int)WorkStage.Active)
                {
                    w.Stage = (int)WorkStage.Failed;
                    w.End = day;
                }
            }
            f.Firms.Remove(firm);
            return gone;
        }
    }
}
