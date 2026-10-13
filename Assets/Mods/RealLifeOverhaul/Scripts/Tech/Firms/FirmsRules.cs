using System;
using System.Collections.Generic;

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
    /// The rules of tech firms: a product's day (features built, bugs, debt, customers won and lost, takings and costs,
    /// an outage, a breach, the rival shipping), a game's launch and its tail of sales, contracts (offers, the council's
    /// tenders, the work and the money), a firm's reputation, its value, and the research and development relief.
    /// Game-free and checked by EconomyCheck; every roll is <see cref="TechRandom"/>'s.
    /// </summary>
    public static class FirmsRules
    {
        public const int NoteDone = 1, NoteFailed = 2, NoteBilled = 3, NoteOffered = 4, NoteWon = 5, NoteLostTender = 6, NoteTender = 7, NoteLapsed = 8;

        /// <summary>What the city's cycle does to what people and companies spend on software, thousandths (boom to recession).</summary>
        public static readonly int[] CycleSpend = { 1100, 1000, 920, 820 };

        public static bool Makes(FirmKind firm, ProductKind kind) => (FirmsCatalogue.MakesProducts[(int)firm] & (1 << (int)kind)) != 0;

        public static bool Takes(FirmKind firm, WorkKind kind) => (FirmsCatalogue.TakesWork[(int)firm] & (1 << (int)kind)) != 0;

        // ---------------------------------------------------------------- building

        /// <summary>Work points a feature costs when so many are built.</summary>
        public static int FeatureCost(int built) => FirmsCatalogue.FeatureBase + FirmsCatalogue.FeatureStep * Math.Max(0, built);

        /// <summary>How a product's bugs weigh on it, thousandths of a sound one: 1000 bug-free, 300 at the worst.</summary>
        public static int Quality(Product p)
        {
            int allowance = Math.Max(1, p.Features) * FirmsCatalogue.BugsPerFeature * 2 + 10;
            return 1000 - Math.Min(700, (int)Math.Min(int.MaxValue, (long)p.Bugs * 1000 / allowance));
        }

        /// <summary>
        /// The share product people add to a feature's points, thousandths: up to four tenths more with one of them for
        /// every few engineers.
        /// </summary>
        public static int ProductBoost(in Crew c) => Math.Min(400, c.Product * 3000 / Math.Max(1, c.Engineering + c.Product));

        /// <summary>
        /// A day of building: the points given to the product, by the firm's focus, go to features (further with product
        /// people, less far with debt), to fixing bugs (two points a bug, a tester's one) and to paying down debt. The work
        /// brings bugs by how often the crew's work needs doing again, and each feature adds debt where few seniors built
        /// it. Returns how many features were finished.
        /// </summary>
        public static int Build(Firm firm, Product p, in Crew c, int points, int testing)
        {
            int[] split = FirmsCatalogue.FocusSplit[TalentCatalogue.Clamp(firm.Focus, 0, (int)Focus.Count - 1)];
            long feature = (long)points * split[0] / 1000 * (1000 + ProductBoost(c)) / 1000 * 1000 / (1000 + firm.Debt);
            int fix = points * split[1] / 1000 / FirmsCatalogue.PointsPerFix + testing;   // (a tester's point fixes a bug)
            int debt = points * split[2] / 1000 * FirmsCatalogue.DebtPerPoint;
            int finished = 0;
            // bugs come with the work: a point in so many by how often the crew's work needs doing again
            p.Bugs = (int)Math.Min(int.MaxValue, p.Bugs + feature * Math.Max(30, c.Faults) / 1000);
            if (p.K == ProductKind.Game)
            {
                p.Progress = (int)Math.Min(int.MaxValue, p.Progress + feature);
            }
            else
            {
                p.Progress = (int)Math.Min(int.MaxValue, p.Progress + feature);
                while (p.Features < FirmsCatalogue.FeaturesMost && p.Progress >= FeatureCost(p.Features)
                       && (p.K != ProductKind.Model || p.Compute >= InfraRules.TrainNeed(p.Features)))
                {
                    if (p.K == ProductKind.Model)
                    {
                        p.Compute -= InfraRules.TrainNeed(p.Features);   // (an AI model's feature is a generation's training as well as its engineering)
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
        public static bool GameReady(Product p) => p.K == ProductKind.Game && p.Stage == 0 && p.Progress >= FirmsCatalogue.GameScope[TalentCatalogue.Clamp(p.Scope, 0, 2)];

        public static bool CanLaunch(Product p) => p.Stage == 0 && (p.K == ProductKind.Game ? GameReady(p) : p.Features >= FirmsCatalogue.LaunchFeatures);

        // ---------------------------------------------------------------- selling

        /// <summary>
        /// How much a product appeals against its rival's, thousandths: its features against the rival's, its quality, its
        /// price tier, the firm's reputation and its design.
        /// </summary>
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
                    return p.Users * MonthPrice(p) * (1000 - FirmsCatalogue.AppStoreCut) / 1000 + (p.Users + p.Free) * FirmsCatalogue.Price[0] * FirmsCatalogue.AppAds / 1000 / 20;
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

        /// <summary>
        /// A day on sale. New customers come from those the product has not won yet, by its appeal, the marketing's reach,
        /// the sales people (for businesses and companies) and the economy; customers leave by the segment's churn, more
        /// for a buggy product, one short of support, a dear tier or a rival ahead. The day's takings and costs come out of
        /// that. A licence sells by deals the sellers turn up. A game sells its launch's tail. Then the risks: an outage, a
        /// breach, the rival shipping a feature.
        /// </summary>
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
                double units = FirmsCatalogue.GamePlayers * FirmsCatalogue.GameLaunchShare / 1e6 * appeal * (1000 + p.Hype) / 1000.0 * reach / 1000.0 * spend / 1000.0 * decay;
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
                int sellers = Math.Max(1, c.Selling / FirmsCatalogue.PointsPerDay);
                for (int s = 0; s < sellers; s++)
                {
                    if (TechRandom.Mille(f.Seed, p.Id, day, 100 + s) < FirmsCatalogue.LicenceLeadsPerSeller * appeal / 1000 * spend / 1000 && open > 0)
                    {
                        r.Won++;
                        r.Takings += (long)FirmsCatalogue.LicenceDeal * FirmsCatalogue.PriceTiers[TalentCatalogue.Clamp(p.Tier, 0, 2)] / 1000;
                    }
                }
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
                    long paying = whole * FirmsCatalogue.ConsumerPaying / 1000;
                    r.Won = paying;
                    p.Free += whole - paying;
                }
                else
                {
                    r.Won = whole;
                }
            }
            // leaving
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
            // the risks
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

        /// <summary>A data protection fine for a breach, cents: so much for each customer affected, at least the floor, at most a share of a year's revenue (but never under the floor); half again for a firm not on the register.</summary>
        public static long Fine(long affected, long yearRevenue, bool registered)
        {
            long fine = affected * FirmsCatalogue.FinePerCustomer;
            long most = Math.Max(FirmsCatalogue.FineLeast, yearRevenue * FirmsCatalogue.FineShareOfYear / 1000);
            fine = Math.Max(FirmsCatalogue.FineLeast, Math.Min(most, fine));
            return registered ? fine : fine * FirmsCatalogue.UnregisteredFine / 1000;
        }

        /// <summary>A game's launch: on sale from today; its hype is what its marketing built.</summary>
        public static void Launch(Product p, int day)
        {
            p.Stage = 1;
            p.Launched = day;
            p.LastRelease = day;
        }

        /// <summary>A game's hype before launch: each day of marketing adds a little, more the more is spent.</summary>
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

        /// <summary>A name for a product: two words of the catalogue, by a roll.</summary>
        public static string Name(int seed, int n)
        {
            return FirmsCatalogue.NameFirst[TechRandom.Range(seed, n, 61, 0, 0, FirmsCatalogue.NameFirst.Length - 1)]
                   + FirmsCatalogue.NameSecond[TechRandom.Range(seed, n, 62, 0, 0, FirmsCatalogue.NameSecond.Length - 1)];
        }

        /// <summary>A new product, being built; its rival is the market's for its kind and segment as it stands.</summary>
        public static Product NewProduct(FirmsFile f, string place, ProductKind kind, Segment segment, int scope, string name)
        {
            var p = new Product
            {
                Id = f.Next(), Place = place, Kind = (int)kind, Segment = kind == ProductKind.App || kind == ProductKind.Game ? (int)Segment.Consumer : kind == ProductKind.Licence ? (int)Segment.Enterprise : (int)segment,
                Scope = TalentCatalogue.Clamp(scope, 0, 2), Name = string.IsNullOrEmpty(name) ? Name(f.Seed, f.NextId) : name
            };
            p.RivalFeatures = FirmsCatalogue.RivalFeaturesAtStart[p.Segment];
            p.RivalTier = 1;
            p.Train = kind == ProductKind.Model ? 2 : 0;
            return p;
        }

        // ---------------------------------------------------------------- contracts

        /// <summary>What a contract of so many points is worth, cents: the point rate (security work dearer), a council job bigger.</summary>
        public static long ValueOf(WorkKind kind, int points, bool council)
        {
            long v = kind == WorkKind.Managed ? FirmsCatalogue.ManagedMonth
                : (long)points * (kind == WorkKind.Audit || kind == WorkKind.PenTest || kind == WorkKind.Incident ? FirmsCatalogue.SecurityRate : FirmsCatalogue.PointRate);
            return council ? v * FirmsCatalogue.CouncilShare / 1000 : v;
        }

        /// <summary>
        /// The day's offers: each firm that takes contracts is offered one now and then, more with a good reputation and
        /// sellers. A client is one of the city's businesses, or one of the player's own (named in <paramref name="own"/> as
        /// place and name). Every <see cref="FirmsCatalogue.TenderEvery"/> days the council puts a job out to tender.
        /// </summary>
        public static void Offers(FirmsFile f, int day, IList<Crew> crews, IList<KeyValuePair<string, string>> own, List<WorkNote> notes)
        {
            int offered = 0;
            for (int i = 0; i < f.Contracts.Count; i++)
            {
                offered += f.Contracts[i].Stage <= (int)WorkStage.Bid ? 1 : 0;
            }
            bool taker = false;
            for (int i = 0; i < f.Firms.Count && offered < FirmsCatalogue.OffersMost && f.Contracts.Count < FirmsCatalogue.ContractsMost; i++)
            {
                Firm firm = f.Firms[i];
                int kinds = FirmsCatalogue.TakesWork[TalentCatalogue.Clamp(firm.Kind, 0, (int)FirmKind.Count - 1)];
                if (kinds == 0)
                {
                    continue;
                }
                taker = true;
                Crew c = i < crews.Count ? crews[i] : default(Crew);
                int chance = FirmsCatalogue.OfferChance * (500 + firm.Reputation) / 1000 * (1000 + Math.Min(2000, c.Selling * 60)) / 1000;
                if (TechRandom.Mille(f.Seed, day, 71, i) >= chance)
                {
                    continue;
                }
                WorkKind kind = PickKind(kinds, TechRandom.Mille(f.Seed, day, 72, i));
                bool mine = own != null && own.Count > 0 && TechRandom.Mille(f.Seed, day, 73, i) < FirmsCatalogue.OwnShare;
                var w = new Contract { Id = f.Next(), Kind = (int)kind, Stage = (int)WorkStage.Offered, End = day + FirmsCatalogue.OfferDays };
                if (mine)
                {
                    KeyValuePair<string, string> client = own[TechRandom.Range(f.Seed, day, 74, i, 0, own.Count - 1)];
                    w.OwnPlace = client.Key;
                    w.Client = client.Value;
                }
                else
                {
                    w.Client = FirmsCatalogue.Clients[TechRandom.Range(f.Seed, day, 75, i, 0, FirmsCatalogue.Clients.Length - 1)];
                }
                w.Points = kind == WorkKind.Managed ? 0 : TechRandom.Range(f.Seed, day, 76, i, FirmsCatalogue.WorkLeast[(int)kind], FirmsCatalogue.WorkMost[(int)kind]);
                w.Value = ValueOf(kind, w.Points, false);
                w.Place = firm.Place;   // (the firm it was offered to; it does the work if it is taken)
                f.Contracts.Add(w);
                offered++;
                notes?.Add(new WorkNote { Contract = w.Id, Kind = NoteOffered });
            }
            if (taker && day - f.LastTender >= FirmsCatalogue.TenderEvery && f.Contracts.Count < FirmsCatalogue.ContractsMost)
            {
                f.LastTender = day;
                WorkKind kind = TechRandom.Mille(f.Seed, day, 77) < 500 ? WorkKind.Integration : WorkKind.Managed;
                var t = new Contract
                {
                    Id = f.Next(), Kind = (int)kind, Client = FirmsCatalogue.Council, Council = true, Stage = (int)WorkStage.Tender, End = day + FirmsCatalogue.TenderDays,
                    Points = kind == WorkKind.Managed ? 0 : TechRandom.Range(f.Seed, day, 78, 0, FirmsCatalogue.WorkLeast[(int)kind], FirmsCatalogue.WorkMost[(int)kind]),
                    Rivals = TechRandom.Range(f.Seed, day, 79, 0, 2, 4)
                };
                t.Value = ValueOf(kind, t.Points, true);
                f.Contracts.Add(t);
                notes?.Add(new WorkNote { Contract = t.Id, Kind = NoteTender });
            }
        }

        private static WorkKind PickKind(int kinds, int roll)
        {
            int count = 0;
            for (int k = 0; k < (int)WorkKind.Count; k++)
            {
                count += (kinds & (1 << k)) != 0 ? 1 : 0;
            }
            int n = roll * Math.Max(1, count) / 1000;
            for (int k = 0; k < (int)WorkKind.Count; k++)
            {
                if ((kinds & (1 << k)) != 0 && n-- == 0)
                {
                    return (WorkKind)k;
                }
            }
            return WorkKind.Website;
        }

        /// <summary>An offer taken by a firm: under way from today, a project's first share paid. Returns what is paid now, cents.</summary>
        public static long Take(Contract w, string place, int day)
        {
            w.Place = place;
            w.Stage = (int)WorkStage.Active;
            w.Start = day;
            w.LastBilled = day;
            if (w.Monthly)
            {
                w.End = day + 12 * FirmsCatalogue.MonthDays;
                return 0;
            }
            w.Due = day + FirmsCatalogue.WorkDays[TalentCatalogue.Clamp(w.Kind, 0, (int)WorkKind.Count - 1)];
            long upfront = w.Value * FirmsCatalogue.UpfrontShare / 1000;
            w.Paid = upfront;
            return upfront;
        }

        /// <summary>The fair price of a tender, cents, and a bid at a share of it.</summary>
        public static long BidAt(Contract tender, int thousandths) => tender.Value * Math.Max(500, thousandths) / 1000;

        /// <summary>
        /// A tender decided on its last day: the rivals bid between 85% and 125% of the fair price; each bid is weighed by
        /// the bidder's reputation (the player's firm's own, the rivals' an ordinary one), and the lowest weighed bid wins.
        /// True if the player's firm won: the job is then under way at its bid.
        /// </summary>
        public static bool Decide(FirmsFile f, Contract t, Firm bidder, int day)
        {
            return Decide(f, t, bidder, day, 0);
        }

        /// <summary>The same, with what the bidder's office adds to the weight of its bid, thousandths (0 to 50: Scripts/Interiors).</summary>
        public static bool Decide(FirmsFile f, Contract t, Firm bidder, int day, int office)
        {
            if (t.Bid <= 0 || bidder == null)
            {
                t.Stage = (int)WorkStage.Lost;
                return false;
            }
            long mine = t.Bid * (1500 - bidder.Reputation - TalentCatalogue.Clamp(office, 0, 50)) / 1000;
            for (int r = 0; r < t.Rivals; r++)
            {
                long bid = t.Value * TechRandom.Range(f.Seed, t.Id, 81, r, 850, 1250) / 1000;
                if (bid * (1500 - 550) / 1000 < mine)
                {
                    t.Stage = (int)WorkStage.Lost;
                    return false;
                }
            }
            t.Value = t.Bid;
            return true;
        }

        /// <summary>
        /// A day of a contract under way with the points given to it: a project's work done (finished: the rest paid, less
        /// a penalty for each day late; failed at twice its time), a managed service's month billed and its client's
        /// satisfaction kept up or let slip (too low, and the client goes). Returns cents paid in today.
        /// </summary>
        public static long Work(Contract w, Firm firm, int points, int day, List<WorkNote> notes)
        {
            if (w.Stage != (int)WorkStage.Active)
            {
                return 0;
            }
            if (w.Monthly)
            {
                w.Satisfaction = TalentCatalogue.Clamp(w.Satisfaction + (points >= FirmsCatalogue.ManagedPoints ? 15 : -40), 0, 1000);
                long billed = 0;
                if (day - w.LastBilled >= FirmsCatalogue.MonthDays)
                {
                    w.LastBilled = day;
                    billed = w.Value;
                    w.Paid += billed;
                    notes?.Add(new WorkNote { Contract = w.Id, Kind = NoteBilled, Amount = billed });
                }
                if (w.Satisfaction < 300 || day >= w.End)
                {
                    w.Stage = w.Satisfaction < 300 ? (int)WorkStage.Failed : (int)WorkStage.Done;
                    firm.Reputation = TalentCatalogue.Clamp(firm.Reputation + (w.Satisfaction < 300 ? -60 : 30), 0, 1000);
                    notes?.Add(new WorkNote { Contract = w.Id, Kind = w.Satisfaction < 300 ? NoteFailed : NoteDone });
                }
                return billed;
            }
            w.Done = Math.Min(w.Points, w.Done + Math.Max(0, points));
            if (w.Done >= w.Points)
            {
                int late = Math.Max(0, day - w.Due);
                long penalty = Math.Min(w.Value / 2, w.Value * FirmsCatalogue.LatePenalty / 1000 * late);
                long rest = Math.Max(0, w.Value - penalty - w.Paid);
                w.Paid += rest;
                w.Stage = (int)WorkStage.Done;
                w.Satisfaction = TalentCatalogue.Clamp(900 - late * 60, 100, 1000);
                firm.Reputation = TalentCatalogue.Clamp(firm.Reputation + (late == 0 ? 25 : -late * 5), 0, 1000);
                notes?.Add(new WorkNote { Contract = w.Id, Kind = NoteDone, Amount = rest });
                return rest;
            }
            if (day > w.Due + (w.Due - w.Start))
            {
                w.Stage = (int)WorkStage.Failed;
                firm.Reputation = Math.Max(0, firm.Reputation - 80);
                notes?.Add(new WorkNote { Contract = w.Id, Kind = NoteFailed });
            }
            return 0;
        }

        /// <summary>The points a contract asks for today: a project what is left over the days to its date, a fifth more to be safe; a managed service its daily share.</summary>
        public static int Need(Contract w, int day)
        {
            if (w.Stage != (int)WorkStage.Active)
            {
                return 0;
            }
            if (w.Monthly)
            {
                return FirmsCatalogue.ManagedPoints;
            }
            int left = Math.Max(0, w.Points - w.Done);
            int days = Math.Max(1, w.Due - day);
            return Math.Min(left, (left * 12 / 10 + days - 1) / days);
        }

        /// <summary>Offers and tenders past their last day lapse; finished and failed contracts are dropped a month after.</summary>
        public static void Lapse(FirmsFile f, int day, List<WorkNote> notes)
        {
            for (int i = f.Contracts.Count - 1; i >= 0; i--)
            {
                Contract w = f.Contracts[i];
                if ((w.Stage == (int)WorkStage.Offered || w.Stage == (int)WorkStage.Tender) && day > w.End)
                {
                    w.Stage = (int)WorkStage.Declined;
                    w.End = day;
                    notes?.Add(new WorkNote { Contract = w.Id, Kind = NoteLapsed });
                }
                else if (w.Stage >= (int)WorkStage.Done && day > w.End + FirmsCatalogue.MonthDays && day > w.Due + FirmsCatalogue.MonthDays)
                {
                    f.Contracts.RemoveAt(i);
                }
            }
        }

        // ---------------------------------------------------------------- value and relief

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

        /// <summary>
        /// What a tech firm is worth to a buyer or an investor, cents: its recurring revenue a year times a multiple that
        /// grows with its growth and falls with debt (four to twelve times), plus three months of its other takings.
        /// </summary>
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
    }
}
