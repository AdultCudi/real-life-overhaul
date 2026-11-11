using System;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What a desk session did, for the director to book and tell: money at the start's prices, the figures its notice names, a piece for the paper.</summary>
    public struct DeskResult
    {
        public bool Done;
        public long Cost;            // cents the firm pays: data, compute, credits, a fee, a hedge
        public long Income;          // cents paid in: a big customer's own feature
        public long Fine;            // a claim against the firm (scraped data)
        public long Racks;           // racks bought ahead (their price is in Cost, as capital)
        public int Figure, Figure2;  // what the notice names: customers, points, a score
        public int Story;            // a City Ledger piece: DeskStory*
        public int Rival;            // the rival product a benchmark beat, its index in the sector's list (-1 none; set by Training)
        public bool Launched;        // a model went on sale at its launch session
    }

    public static partial class SectorRules
    {
        // ---------------------------------------------------------------- the kinds of session

        public const int DeskSprint = 0, DeskOutage = 1, DeskBigCustomer = 2, DeskTraining = 3, DeskLaunch = 4, DeskPitch = 5, DeskScope = 6, DeskIncident = 7,
            DeskCapacity = 8, DeskPower = 9, DeskFailover = 10, DeskClientPitch = 11, DeskRevision = 12, DeskPortfolio = 13, Desks = 14;

        /// <summary>Each kind's id in the sessions' save: never renamed.</summary>
        public static readonly string[] DeskIds =
        {
            "firm_sprint", "firm_outage", "firm_bigcustomer", "firm_training", "firm_launch", "firm_pitch", "firm_scope", "firm_incident",
            "firm_capacity", "firm_power", "firm_failover", "firm_clientpitch", "firm_revision", "firm_portfolio"
        };

        /// <summary>The kind of firm each is held at (FirmKind); -1: a data centre's, held at a data centre or any of the player's tech offices.</summary>
        public static readonly int[] DeskFirm =
        {
            (int)FirmKind.Software, (int)FirmKind.Software, (int)FirmKind.Software, (int)FirmKind.AiLab, (int)FirmKind.AiLab, (int)FirmKind.Consultancy, (int)FirmKind.Consultancy,
            (int)FirmKind.Security, -1, -1, -1, (int)FirmKind.WebAgency, (int)FirmKind.WebAgency, (int)FirmKind.WebAgency
        };

        /// <summary>Days after it was held that each kind is due again; an event's kind is held whenever its event waits (1).</summary>
        public static readonly int[] DeskEvery =
        {
            SectorCatalogue.SprintDays, 1, SectorCatalogue.BigCustomerEvery, SectorCatalogue.TrainingEvery, 1, 1, 1, 1, 14, 1, 1, 1, 1, SectorCatalogue.PortfolioEvery
        };

        public const int DeskStoryNone = 0, DeskStoryBenchmark = 1, DeskStoryAward = 2, DeskStoryIncident = 3, DeskStoryCaught = 4;
        public const int ScopedBit = 4, RevisedBit = 8;   // Contract.Told's marks: a scope creep and a revision round answered

        // the choices, by step
        public const int SprintRequests = 0, SprintBalanced = 1, SprintDebt = 2, ReleaseWeekly = 0, ReleaseReady = 1;
        public const int OutageRollback = 0, OutageHotfix = 1, OutageStatus = 2, AfterPostMortem = 0, AfterCredit = 1, AfterMoveOn = 2;
        public const int BigBuild = 0, BigDiscount = 1, BigRefuse = 2;
        public const int ComputeOwn = 0, ComputeRented = 1, ComputeSmaller = 2, DataLicensed = 0, DataScraped = 1, DataSynthetic = 2, SafetyFull = 0, SafetyStandard = 1, SafetySkip = 2;
        public const int ReleaseOpen = 0, ReleaseApi = 1, ReleasePartners = 2;
        public const int StaffSeniors = 0, StaffMixed = 1, StaffJuniors = 2, PriceLow = 0, PriceFair = 1, PriceHigh = 2;
        public const int ScopeCharge = 0, ScopeAbsorb = 1, ScopePushBack = 2;
        public const int ContainIsolate = 0, ContainTargeted = 1, ContainTrace = 2, TellNow = 0, TellLater = 1, TellLittle = 2;
        public const int AheadBuy = 0, AheadAsNeeded = 1, AheadLet = 2;
        public const int PowerRide = 0, PowerThrottle = 1, PowerHedge = 2;
        public const int FailOver = 0, FailRestore = 1, FailQuiet = 2;
        public const int ConceptBold = 0, ConceptSafe = 1, ConceptCheap = 2, PaceFast = 0, PaceUsual = 1;
        public const int RevisionFree = 0, RevisionCharge = 1, RevisionHold = 2;
        public const int PortfolioPolish = 0, PortfolioAward = 1, PortfolioLater = 2;

        /// <summary>A step's choice from the answers given (an index out of range reads as <paramref name="otherwise"/>).</summary>
        public static int Answer(int[] choices, int step, int n, int otherwise)
        {
            return choices != null && step < choices.Length && choices[step] >= 0 && choices[step] < n ? choices[step] : otherwise;
        }

        // ---------------------------------------------------------------- the software studio: sprint planning

        /// <summary>A sprint planned: for its week the aim (customer requests, balanced, or debt and reliability) and the release (weekly, or when ready) shape the firm's build.</summary>
        public static DeskResult Sprint(SectorFirm sf, int aim, int release, int day)
        {
            sf.Sprint = Clamp(aim, 0, 2);
            sf.Release = Clamp(release, 0, 1);
            sf.SprintUntil = day + SectorCatalogue.SprintDays;
            if (sf.Sprint == SprintDebt)
            {
                sf.ReliableUntil = Math.Max(sf.ReliableUntil, sf.SprintUntil);
            }
            return new DeskResult { Done = true };
        }

        /// <summary>The focus a firm builds with today: a sprint's aim while it runs, else its own.</summary>
        public static int SprintFocus(SectorFirm sf, int own, int day)
        {
            if (sf == null || day >= sf.SprintUntil)
            {
                return own;
            }
            return sf.Sprint == SprintRequests ? (int)Focus.Features : sf.Sprint == SprintDebt ? (int)Focus.Stability : own;
        }

        /// <summary>Thousandths of the usual points a sprint's release gives (shipping weekly pushes harder).</summary>
        public static int SprintPoints(SectorFirm sf, int day) => sf != null && day < sf.SprintUntil && sf.Release == ReleaseWeekly ? 1000 + SectorCatalogue.SprintWeekly : 1000;

        /// <summary>Thousandths of the usual bugs a sprint leaves: more for customer requests and weekly releases, fewer shipping when ready.</summary>
        public static int SprintFaults(SectorFirm sf, int day)
        {
            if (sf == null || day >= sf.SprintUntil)
            {
                return 1000;
            }
            long f = 1000;
            f = f * (sf.Sprint == SprintRequests ? 1000 + SectorCatalogue.SprintPushBugs : 1000) / 1000;
            f = f * (sf.Release == ReleaseWeekly ? 1000 + SectorCatalogue.SprintWeekly : 1000 - SectorCatalogue.SprintReady) / 1000;
            return (int)f;
        }

        /// <summary>
        /// The sound sprint: debt and reliability first for a firm deep in debt or just down, customer requests for a
        /// product behind its market, else balanced; ship when ready while its bugs weigh.
        /// </summary>
        public static int PickSprint(int step, int debt, bool downLately, bool behind, int soundness)
        {
            if (step == 1)
            {
                return soundness < 700 ? ReleaseReady : ReleaseWeekly;
            }
            return debt > 400 || downLately ? SprintDebt : behind ? SprintRequests : SprintBalanced;
        }

        // ---------------------------------------------------------------- the software studio: an outage's response

        /// <summary>
        /// An outage answered: a roll back brings most of the lost customers back and loses the work on the next feature, a
        /// hotfix forward brings half back and may go wrong (a second outage), a status page keeps the firm's name; then a
        /// post-mortem keeps the service sounder for two weeks and pays down debt, a credit to customers costs half a week's
        /// revenue and earns their trust, or the team moves on. A manager brings back their strength's share (thousandths).
        /// </summary>
        public static DeskResult Outage(SectorFirm sf, Product p, Firm firm, int first, int after, int seed, int day, int strength = 1000)
        {
            var r = new DeskResult { Done = true };
            long lost = Math.Max(0L, sf.LostToOutage);
            long back = 0;
            switch (Clamp(first, 0, 2))
            {
                case OutageRollback:
                    back = lost * SectorCatalogue.RollbackSaved / 1000;
                    p.Progress = 0;
                    break;
                case OutageHotfix:
                    back = lost * SectorCatalogue.HotfixSaved / 1000;
                    if (TechRandom.Mille(seed, p.Id, day, 101) < SectorCatalogue.HotfixRisk)
                    {
                        long gone = p.Users * FirmsCatalogue.OutageChurn / 1000;
                        p.Users -= gone;
                        back -= gone;
                        firm.Reputation = Math.Max(0, firm.Reputation - 30);
                        firm.Outages++;
                        r.Figure2 = 1;
                    }
                    break;
                default:
                    firm.Reputation = Math.Min(1000, firm.Reputation + 15);
                    break;
            }
            back = back > 0 ? back * Clamp(strength, 0, 1000) / 1000 : back;
            p.Users = Math.Max(0L, p.Users + Math.Max(0L, back));
            switch (Clamp(after, 0, 2))
            {
                case AfterPostMortem:
                    sf.ReliableUntil = day + SectorCatalogue.PostMortemDays;
                    firm.Debt = Math.Max(0, firm.Debt - 50);
                    break;
                case AfterCredit:
                    r.Cost = FirmsRules.MonthlyRevenue(p) / FirmsCatalogue.MonthDays * 7 * SectorCatalogue.CreditShare / 1000;
                    firm.Reputation = Math.Min(1000, firm.Reputation + 20);
                    break;
            }
            r.Figure = (int)Math.Max(0L, Math.Min(int.MaxValue, back));
            sf.LostToOutage = 0;
            sf.LastOutage = -1000;
            return r;
        }

        /// <summary>The sound response: roll back when customers were lost, else a status page; a post-mortem after.</summary>
        public static int PickOutage(int step, long lost) => step == 0 ? (lost > 0 ? OutageRollback : OutageStatus) : AfterPostMortem;

        /// <summary>A post-mortem's reliability: thousandths of the way from a firm's posture to sound it adds while it holds.</summary>
        public static int Reliable(SectorFirm sf, int reliability, int day) =>
            sf != null && day < sf.ReliableUntil ? reliability + (1000 - reliability) * SectorCatalogue.PostMortemReliability / 1000 : reliability;

        // ---------------------------------------------------------------- the software studio: a big customer's demands

        /// <summary>
        /// A big customer asks: building their feature pays six months of a company's subscription now and takes a third of the
        /// engineering for two weeks; a discount keeps them and their peers for a fifth off a year of a tenth of the revenue; a
        /// refusal loses a twentieth of the product's customers who wanted the same.
        /// </summary>
        public static DeskResult BigCustomer(SectorFirm sf, Product p, int choice, int day)
        {
            var r = new DeskResult { Done = true };
            sf.LastBigAsk = day;
            switch (Clamp(choice, 0, 2))
            {
                case BigBuild:
                    r.Income = (long)SectorCatalogue.BigDeal * FirmsCatalogue.Price[(int)Segment.Enterprise];
                    sf.DivertUntil = day + SectorCatalogue.BigDivertDays;
                    break;
                case BigDiscount:
                    r.Cost = FirmsRules.MonthlyRevenue(p) * 12 * SectorCatalogue.BigDiscount / 1000 / 10;
                    break;
                default:
                    long gone = p.Users * SectorCatalogue.BigLost / 1000;
                    p.Users -= gone;
                    r.Figure = (int)Math.Min(int.MaxValue, gone);
                    break;
            }
            return r;
        }

        /// <summary>The engineering points a firm has for its products today: less while a big customer's feature takes its share.</summary>
        public static int Diverted(SectorFirm sf, int engineering, int day) =>
            sf != null && day < sf.DivertUntil ? engineering * (1000 - SectorCatalogue.BigDivert) / 1000 : engineering;

        /// <summary>The sound answer: build it with engineers to spare (three or more in good heart), else a discount.</summary>
        public static int PickBigCustomer(int engineering) => engineering >= 3 * FirmsCatalogue.PointsPerDay ? BigBuild : BigDiscount;

        // ---------------------------------------------------------------- the AI lab: a training run

        /// <summary>
        /// A training run: ten days of the model's training at once on the player's own AI racks (at the price the player's firms
        /// pay their own cloud) or rented, or half a run; licensed data makes it sounder and costs, scraped data
        /// is free and may bring a claim, synthetic is between; full safety checks hold the next training for days and earn a
        /// name, skipped ones risk an incident at its launch. Its benchmark against the rival labs of its market: a win earns
        /// a name and a piece in the paper. A rented run takes no more a day than a model may rent (<see cref="InfraCatalogue.RentedMost"/>),
        /// as the day's training does. Returns the run's cost and its score.
        /// </summary>
        public static DeskResult Training(SectorFile f, Product p, SectorProduct sp, Firm firm, int compute, int data, int safety, bool ownGpu, int seed, int day, int strength = 1000)
        {
            var r = new DeskResult { Done = true, Rival = -1 };
            int c = Clamp(compute, 0, 2), d = Clamp(data, 0, 2), s = Clamp(safety, 0, 2);
            if (c == ComputeOwn && !ownGpu)
            {
                c = ComputeRented;   // no accelerators of the player's to run it on
            }
            long daily = InfraRules.TrainLevels[Math.Max(2, InfraRules.TrainLevel(p.Train))];
            if (c != ComputeOwn)
            {
                daily = Math.Min(daily, InfraCatalogue.RentedMost);
            }
            long units = daily * SectorCatalogue.TrainRunDays * SectorCatalogue.TrainCompute[c] / 1000
                         * Clamp(strength, 0, 1000) / 1000;   // a manager's run makes less of the same compute
            p.Compute += units;
            r.Cost = units * InfraCatalogue.GpuUnitDay * (c == ComputeOwn ? InfraCatalogue.OwnHostingShare : InfraCatalogue.RentedGpuShare) / 1000 + SectorCatalogue.DataCost[d];
            switch (d)
            {
                case DataLicensed:
                    p.Bugs -= p.Bugs / 3;
                    break;
                case DataScraped:
                    p.Bugs += Math.Max(1, p.Features);
                    if (TechRandom.Mille(seed, p.Id, day, 103) < SectorCatalogue.ScrapeRisk)
                    {
                        r.Fine = FirmsRules.Fine(p.Users + p.Free, firm.Revenue30 * 12, day < firm.RegisteredUntil);
                        firm.Reputation = Math.Max(0, firm.Reputation - 60);
                    }
                    break;
                default:
                    p.Bugs -= p.Bugs / 6;
                    break;
            }
            sp.TrainedDay = day + SectorCatalogue.SafetyDays[s];
            sp.Data = d + s * 10;
            if (s == SafetyFull)
            {
                firm.Reputation = Math.Min(1000, firm.Reputation + SectorCatalogue.SafetyReputation);
            }
            long mine = Power(p.Features, p.Bugs, FirmsRules.Allowance(p), SectorCatalogue.DataQuality[d]);
            long best = 0;
            int market = MarketOf(p);
            for (int i = 0; i < f.Rivals.Count; i++)
            {
                RivalProduct rv = f.Rivals[i];
                long theirs = rv.Market == market ? Power(rv.Features, rv.Bugs, RivalRoom(rv), 1000) : 0;
                if (theirs > best)
                {
                    best = theirs;
                    r.Rival = i;
                }
            }
            sp.Benchmark = Clamp(50 + (int)(50L * (mine - best) / Math.Max(1L, mine + best)), 0, 100);
            r.Figure = sp.Benchmark;
            r.Figure2 = (int)Math.Min(int.MaxValue, units);
            if (mine > best && r.Rival >= 0)
            {
                firm.Reputation = Math.Min(1000, firm.Reputation + SectorCatalogue.BenchmarkWin);
                r.Story = DeskStoryBenchmark;
            }
            else
            {
                r.Rival = -1;
            }
            return r;
        }

        /// <summary>A model's strength on a benchmark: its generations, how sound it is, and its data's quality.</summary>
        public static long Power(int features, int bugs, int room, int data) => (long)Math.Max(0, features) * Soundness(bugs, room) / 1000 * Math.Max(0, data);

        /// <summary>True when a model's training is held for its safety checks today.</summary>
        public static bool Held(SectorProduct sp, int day) => sp != null && day < sp.TrainedDay;

        /// <summary>The sound run: on the lab's own accelerators when it has them, licensed data, full safety checks.</summary>
        public static int PickTraining(int step, bool ownGpu) => step == 0 ? (ownGpu ? ComputeOwn : ComputeRented) : step == 1 ? DataLicensed : SafetyFull;

        // ---------------------------------------------------------------- the AI lab: a model's launch

        /// <summary>
        /// A model launched (or a new generation of one): open weights make it widely known for a low price, an interface for
        /// developers earns, partners first sign a few large customers at once; its price against the leading lab's. A run
        /// whose safety checks were skipped may go wrong here: an incident costs a name and a tenth of its customers.
        /// </summary>
        public static DeskResult Launch(Product p, SectorProduct sp, Firm firm, int release, int price, int seed, int day)
        {
            var r = new DeskResult { Done = true };
            if (p.Stage == 0 && FirmsRules.CanLaunch(p))
            {
                FirmsRules.Launch(p, day);
                r.Launched = true;
            }
            int rel = Clamp(release, 0, 2);
            p.Tier = rel == ReleaseOpen ? 0 : Clamp(price, 0, 2);
            sp.Boost = SectorCatalogue.LaunchAwareness[rel];
            sp.BoostUntil = day + SectorCatalogue.LaunchDays;
            if (rel == ReleasePartners && p.Segment != (int)Segment.Consumer)
            {
                p.Users += SectorCatalogue.PartnersCompanies;
                r.Figure = SectorCatalogue.PartnersCompanies;
            }
            int safety = Clamp(sp.Data / 10, 0, 2);
            if (TechRandom.Mille(seed, p.Id, day, 105) < SectorCatalogue.SafetyIncident[safety])
            {
                firm.Reputation = Math.Max(0, firm.Reputation - 3 * SectorCatalogue.SafetyReputation);
                p.Users -= p.Users / 10;
                p.Free -= p.Free / 10;
                r.Story = DeskStoryIncident;
                r.Figure2 = 1;
            }
            sp.Generation = p.Features;
            return r;
        }

        /// <summary>True when a model has something to launch: ready and not yet on sale, or a new generation since its last launch.</summary>
        public static bool HasLaunch(Product p, SectorProduct sp) =>
            p != null && p.K == ProductKind.Model && (p.Stage == 0 ? FirmsRules.CanLaunch(p) : p.Live && p.Features >= (sp != null ? sp.Generation : 0) + 2);

        /// <summary>A launch's awareness while it lasts, thousandths.</summary>
        public static int Boost(SectorProduct sp, int day) => sp != null && day < sp.BoostUntil ? sp.Boost : 0;

        /// <summary>The sound launch: an interface for developers at the going price.</summary>
        public static int PickLaunch(int step) => step == 0 ? ReleaseApi : PriceFair;

        // ---------------------------------------------------------------- the IT consultancy: a pitch or a tender

        /// <summary>
        /// A pitch: for a council tender the bid at a low, fair or high share of its price, weighing better with seniors on it
        /// and worse with juniors; for an offer, the price asked of the client (a high one may lose it) and the team (juniors
        /// take longer). Then the tender is bid on, or the offer taken. True in Done when the work was won or bid on.
        /// </summary>
        public static DeskResult Pitch(Contract w, SectorFirm sf, string place, int staffing, int price, bool seniors, int seed, int day, int strength = 1000)
        {
            var r = new DeskResult { Done = true };
            int staff = Clamp(staffing, 0, 2), at = Clamp(price, 0, 2);
            if (w.Stage == (int)WorkStage.Tender)
            {
                w.Bid = FirmsRules.BidAt(w, SectorCatalogue.PitchPrice[at]);
                w.Place = place;
                w.Stage = (int)WorkStage.Bid;
                sf.PitchTender = w.Id;
                sf.PitchWeight = staff == StaffSeniors && seniors ? SectorCatalogue.PitchSeniorWeight * Clamp(strength, 0, 1000) / 1000 : staff == StaffJuniors ? SectorCatalogue.PitchJuniorWeight : 0;
                r.Figure = 1;
                return r;
            }
            if (at == PriceHigh && TechRandom.Mille(seed, w.Id, day, 107) < (SectorCatalogue.PitchPrice[PriceHigh] - 1000) * 2)
            {
                w.Stage = (int)WorkStage.Declined;
                w.End = day;
                r.Figure = 0;
                return r;
            }
            w.Value = w.Value * SectorCatalogue.PitchPrice[at] / 1000;
            if (staff == StaffJuniors && !w.Monthly)
            {
                w.Points = w.Points * (1000 + SectorCatalogue.PitchJuniorWork) / 1000;
            }
            r.Income = FirmsRules.Take(w, place, day);
            r.Figure = 2;
            return r;
        }

        /// <summary>A tender's pitch weighed into its bid: the weight its pitch set when it is that tender.</summary>
        public static int PitchOf(SectorFirm sf, Contract t) => sf != null && t != null && sf.PitchTender == t.Id ? sf.PitchWeight : 0;

        /// <summary>The sound pitch: seniors when the firm has any, else a mixed team, at the fair price.</summary>
        public static int PickPitch(int step, bool seniors) => step == 0 ? (seniors ? StaffSeniors : StaffMixed) : PriceFair;

        // ---------------------------------------------------------------- the IT consultancy: scope creep

        /// <summary>The client asks for a fifth more: a change order is paid for it and costs some goodwill, absorbing it wins goodwill and a name, pushing back loses more goodwill.</summary>
        public static DeskResult Scope(Contract w, Firm firm, int choice)
        {
            var r = new DeskResult { Done = true };
            int more = Math.Max(1, w.Points * SectorCatalogue.ScopeMore / 1000);
            switch (Clamp(choice, 0, 2))
            {
                case ScopeCharge:
                    w.Points += more;
                    w.Value += w.Value * SectorCatalogue.ScopeMore / 1000;
                    w.Due += Math.Max(0, w.Due - w.Start) * SectorCatalogue.ScopeMore / 1000;   // a change order agrees a date for the work it adds, so that work never makes the project late
                    w.Satisfaction = Clamp(w.Satisfaction - 50, 0, 1000);
                    break;
                case ScopeAbsorb:
                    w.Points += more;
                    w.Satisfaction = Clamp(w.Satisfaction + 100, 0, 1000);
                    firm.Reputation = Math.Min(1000, firm.Reputation + 10);
                    break;
                default:
                    w.Satisfaction = Clamp(w.Satisfaction - 150, 0, 1000);
                    break;
            }
            w.Told |= ScopedBit;
            r.Figure = more;
            return r;
        }

        /// <summary>True for a project far enough along for its client to ask for more, not yet answered.</summary>
        public static bool Creeps(Contract w) =>
            w != null && w.Stage == (int)WorkStage.Active && !w.Monthly && w.Points > 0 && (w.Told & ScopedBit) == 0 && w.Done * 1000L / w.Points >= SectorCatalogue.ScopeFrom;

        /// <summary>The sound answer: a change order with a happy client, else absorb it.</summary>
        public static int PickScope(Contract w) => w != null && w.Satisfaction >= 700 ? ScopeCharge : ScopeAbsorb;

        // ---------------------------------------------------------------- the cybersecurity firm: an incident

        /// <summary>
        /// An incident answered under time with the client watching: isolating everything does most of the work at once and
        /// stops their business, targeted containment does less (more with seniors), tracing the attacker may catch them or let
        /// them do more harm; telling the client at once wins their trust, waiting is neutral, saying little loses it.
        /// </summary>
        public static DeskResult Incident(Contract w, Firm firm, int contain, int tell, bool seniors, int seed, int day, int strength = 1000)
        {
            var r = new DeskResult { Done = true };
            int done = 0;
            bool harm = false;
            switch (Clamp(contain, 0, 2))
            {
                case ContainIsolate:
                    done = w.Points * SectorCatalogue.IncidentIsolateDone / 1000;
                    w.Satisfaction = Clamp(w.Satisfaction - 100, 0, 1000);
                    break;
                case ContainTargeted:
                    done = w.Points * (SectorCatalogue.IncidentTargetDone + (seniors ? 200 : 0)) / 1000;
                    break;
                default:
                    if (TechRandom.Mille(seed, w.Id, day, 109) < SectorCatalogue.IncidentTraceRisk)
                    {
                        w.Points += w.Points / 2;
                        harm = true;
                    }
                    else
                    {
                        done = w.Points / 5;
                        firm.Reputation = Math.Min(1000, firm.Reputation + 40);
                        r.Story = DeskStoryCaught;
                    }
                    break;
            }
            done = done * Clamp(strength, 0, 1000) / 1000;
            w.Done = Math.Min(w.Points, w.Done + done);
            switch (Clamp(tell, 0, 2))
            {
                case TellNow:
                    w.Satisfaction = Clamp(w.Satisfaction + 100, 0, 1000);
                    firm.Reputation = Math.Min(1000, firm.Reputation + 15);
                    break;
                case TellLittle:
                    w.Satisfaction = Clamp(w.Satisfaction - 100, 0, 1000);
                    firm.Reputation = Math.Max(0, firm.Reputation - (harm ? 40 : 0));
                    break;
            }
            r.Figure = done;
            r.Figure2 = harm ? 1 : 0;
            return r;
        }

        /// <summary>The sound response: targeted containment with seniors, else isolate everything; tell the client at once.</summary>
        public static int PickIncident(int step, bool seniors) => step == 0 ? (seniors ? ContainTargeted : ContainIsolate) : TellNow;

        // ---------------------------------------------------------------- data centres: capacity planning

        /// <summary>
        /// Capacity planned: racks bought ahead at a tenth off (a step's worth where there is room, at the end game's scale's
        /// price), or as needed, or more racks let to the city's firms; and the cloud's price against the rivals'. Returns the
        /// racks to buy and their cost (the director buys them in the centre with the most room).
        /// </summary>
        public static DeskResult Capacity(SectorFile f, InfraFile infra, int ahead, int price, int share, int day)
        {
            var r = new DeskResult { Done = true };
            f.LastCapacity = day;
            switch (Clamp(ahead, 0, 2))
            {
                case AheadBuy:
                    DataCentre d = Roomiest(infra);
                    int racks = d != null ? Math.Min(InfraRules.Room(d), Math.Max(SectorCatalogue.CapacityRacks, InfraRules.RacksStep(share))) : 0;
                    r.Racks = racks;
                    r.Cost = racks * InfraRules.RackPrice(false, share) * SectorCatalogue.CapacityAhead / 1000;
                    f.ColoTarget = -1;
                    break;
                case AheadLet:
                    int let = 0;
                    for (int i = 0; i < infra.Centres.Count; i++)
                    {
                        let += infra.Centres[i].Colo;
                    }
                    f.ColoTarget = let + Math.Max(1, InfraRules.RacksStep(share));
                    break;
                default:
                    f.ColoTarget = -1;
                    break;
            }
            if (infra.Cloud.Open)
            {
                infra.Cloud.Price = Clamp(infra.Cloud.RivalPrice * SectorCatalogue.CapacityPrice[Clamp(price, 0, 2)] / 1000, 500, 2000);
            }
            r.Figure = (int)r.Racks;
            return r;
        }

        /// <summary>The data centre with the most room for racks, or null.</summary>
        public static DataCentre Roomiest(InfraFile infra)
        {
            DataCentre best = null;
            for (int i = 0; infra != null && i < infra.Centres.Count; i++)
            {
                if (best == null || InfraRules.Room(infra.Centres[i]) > InfraRules.Room(best))
                {
                    best = infra.Centres[i];
                }
            }
            return best;
        }

        /// <summary>The sound plan: buy ahead when the cloud sold most of what it has, else as needed; match the rivals' price.</summary>
        public static int PickCapacity(int step, int sold, int capacity) => step == 0 ? (capacity > 0 && sold * 10 >= capacity * 8 ? AheadBuy : AheadAsNeeded) : PriceFair;

        // ---------------------------------------------------------------- data centres: a power price spike

        /// <summary>A spike answered: ride it out, throttle the AI racks for its days (half their power and their compute), or fix the price for it for a fee.</summary>
        public static DeskResult Power(SectorFile f, int choice, long monthPower, int day)
        {
            var r = new DeskResult { Done = true };
            switch (Clamp(choice, 0, 2))
            {
                case PowerThrottle:
                    f.ThrottleUntil = f.SpikeUntil;
                    break;
                case PowerHedge:
                    f.Hedged = true;
                    r.Cost = Math.Max(0L, monthPower) * SectorCatalogue.HedgeFee / 1000;
                    break;
            }
            r.Figure = Math.Max(0, f.SpikeUntil - day);
            return r;
        }

        /// <summary>True while a spike runs that has not been answered.</summary>
        public static bool SpikeWaits(SectorFile f, int day) => Spiking(f, day) && !f.Hedged && f.ThrottleUntil < f.SpikeUntil;

        /// <summary>The sound answer: fix the price when the spike's extra over its days left costs more than the fee, else ride it out.</summary>
        public static int PickPower(SectorFile f, int day)
        {
            long extra = (long)(Math.Max(1000, f.SpikeFactor) - 1000) * Math.Max(0, f.SpikeUntil - day);   // thousandths of a day's power
            return extra > (long)SectorCatalogue.HedgeFee * FirmsCatalogue.MonthDays ? PowerHedge : PowerRide;
        }

        // ---------------------------------------------------------------- data centres: an outage and failover

        /// <summary>
        /// A data centre's outage answered: failing over to another region keeps the cloud's name (it needs two regions),
        /// restoring in place costs a little more of it, saying nothing more; then a post-mortem keeps outages down for a month,
        /// a credit to customers costs half a day's takings and earns trust, or the team moves on.
        /// </summary>
        public static DeskResult Failover(SectorFile f, Cloud c, int regions, int first, int after, long dayTakings, int day)
        {
            var r = new DeskResult { Done = true };
            int move = Clamp(first, 0, 2);
            if (move == FailOver && regions < SectorCatalogue.FailoverRegions)
            {
                move = FailRestore;   // nowhere to fail over to
            }
            c.Reputation = Clamp(c.Reputation + (move == FailOver ? 60 : move == FailRestore ? -20 : -40), 0, 1000);
            switch (Clamp(after, 0, 2))
            {
                case AfterPostMortem:
                    f.FailoverUntil = day + SectorCatalogue.FailoverDays;
                    break;
                case AfterCredit:
                    r.Cost = Math.Max(0L, dayTakings) / 2;
                    c.Reputation = Math.Min(1000, c.Reputation + 40);
                    break;
            }
            r.Figure = move;
            return r;
        }

        /// <summary>The sound answer: fail over with two regions, else restore in place; a post-mortem after.</summary>
        public static int PickFailover(int step, int regions) => step == 0 ? (regions >= SectorCatalogue.FailoverRegions ? FailOver : FailRestore) : AfterPostMortem;

        /// <summary>True for a data centre down within the days its failover is answered in.</summary>
        public static bool DownLately(DataCentre d, int day) => d != null && d.Outages > 0 && day - (d.DownUntil - 1) <= SectorCatalogue.OutageWindow && day - (d.DownUntil - 1) >= 0;

        // ---------------------------------------------------------------- the web agency and the design studio

        /// <summary>
        /// A client pitch: a bold concept asks for more work and more money and may be turned down, a safe one takes the offer as
        /// it stands, a cheap one a fifth off; a fast promise brings the date forward by a third. The offer is then taken.
        /// </summary>
        public static DeskResult ClientPitch(Contract w, string place, int concept, int pace, int seed, int day)
        {
            var r = new DeskResult { Done = true };
            switch (Clamp(concept, 0, 2))
            {
                case ConceptBold:
                    if (TechRandom.Mille(seed, w.Id, day, 111) < 250)
                    {
                        w.Stage = (int)WorkStage.Declined;
                        w.End = day;
                        return r;
                    }
                    w.Points += w.Points / 5;
                    w.Value += w.Value * 3 / 10;
                    break;
                case ConceptCheap:
                    w.Value -= w.Value / 5;
                    break;
            }
            r.Income = FirmsRules.Take(w, place, day);
            if (Clamp(pace, 0, 1) == PaceFast && !w.Monthly)
            {
                w.Due = w.Start + Math.Max(1, (w.Due - w.Start) * 2 / 3);
                w.Satisfaction = Clamp(w.Satisfaction + 50, 0, 1000);
            }
            r.Figure = 1;
            return r;
        }

        /// <summary>The sound pitch: the safe concept at the usual pace.</summary>
        public static int PickClientPitch(int step) => step == 0 ? ConceptSafe : PaceUsual;

        /// <summary>A revision round: one more round free wins goodwill and takes work, charging for it pays and costs some, holding the line costs more goodwill.</summary>
        public static DeskResult Revision(Contract w, Firm firm, int choice)
        {
            var r = new DeskResult { Done = true };
            int more = Math.Max(1, w.Points * SectorCatalogue.RevisionMore / 1000);
            switch (Clamp(choice, 0, 2))
            {
                case RevisionFree:
                    w.Points += more;
                    w.Satisfaction = Clamp(w.Satisfaction + 100, 0, 1000);
                    break;
                case RevisionCharge:
                    w.Points += more;
                    w.Value += w.Value * SectorCatalogue.RevisionMore / 1000;
                    w.Satisfaction = Clamp(w.Satisfaction - 50, 0, 1000);
                    break;
                default:
                    w.Satisfaction = Clamp(w.Satisfaction - 120, 0, 1000);
                    break;
            }
            w.Told |= RevisedBit;
            r.Figure = more;
            return r;
        }

        /// <summary>True for a design or web project far enough along for its client to want changes, not yet answered.</summary>
        public static bool WantsChanges(Contract w) =>
            w != null && w.Stage == (int)WorkStage.Active && !w.Monthly && w.Points > 0 && (w.Told & RevisedBit) == 0 && w.Done * 1000L / w.Points >= SectorCatalogue.RevisionFrom;

        /// <summary>The sound answer: one round free for a client not yet happy, else charge for it.</summary>
        public static int PickRevision(Contract w) => w != null && w.Satisfaction < 800 ? RevisionFree : RevisionCharge;

        /// <summary>
        /// A portfolio piece: polishing the best work earns a name and draws bigger clients for two months; an award
        /// entry costs a fee and may win a bigger name and a piece in the paper.
        /// </summary>
        public static DeskResult Portfolio(Firm firm, SectorFirm sf, int choice, int seed, int day, int strength = 1000)
        {
            var r = new DeskResult { Done = true };
            switch (Clamp(choice, 0, 2))
            {
                case PortfolioPolish:
                    firm.Reputation = Math.Min(1000, firm.Reputation + SectorCatalogue.PortfolioReputation * Clamp(strength, 0, 1000) / 1000);
                    sf.PortfolioUntil = day + SectorCatalogue.PortfolioDays;
                    break;
                case PortfolioAward:
                    r.Cost = SectorCatalogue.AwardFee;
                    if (TechRandom.Mille(seed, TechRandom.Of(firm.Place), day, 113) < SectorCatalogue.AwardChance)
                    {
                        firm.Reputation = Math.Min(1000, firm.Reputation + SectorCatalogue.AwardReputation);
                        sf.PortfolioUntil = day + SectorCatalogue.PortfolioDays;
                        r.Story = DeskStoryAward;
                        r.Figure = 1;
                    }
                    break;
            }
            return r;
        }

        /// <summary>The sound piece: polish the best work.</summary>
        public static int PickPortfolio() => PortfolioPolish;
    }
}
