using System;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What a studio's people bring to a game of one genre on a day: their points by family, and the craft each family builds with.</summary>
    public struct StudioCrew
    {
        public int Engineering, Design, Product, Testing;   // the day's work points (output, hours, the office's spare time and connection, the week's meeting counted)
        public int CraftEngineering, CraftDesign, CraftProduct, CraftTesting;   // 0 to 100, weighed by each person's points
        public int Faults;                                   // thousandths: how often their work needs doing again (TalentRules.Faults)
        public int Support;                                  // support people at work (each answers FirmsCatalogue.TicketsPerPerson tickets a day)
    }

    /// <summary>
    /// The game studio's rules (plans/tech-overhaul DRAFT2 section 3): the concept's plan from its budget (ScaleRules), the
    /// team's craft from skill, level and genre experience, a day of building into the five parts of quality, the phases and
    /// their milestones, bugs and playtests, the desk sessions' choices, hype and the review. Sales, life after launch,
    /// bonuses, awards and the old saves' games are in StudioRules.Sales; the rival studios in StudioRules.Rivals. Whole
    /// numbers and seeded rolls only, so a reload and every machine decide the same. Game-free: EconomyCheck's StudioChecks
    /// hold it.
    /// </summary>
    public static partial class StudioRules
    {
        // the desk sessions' choices, by step (Wiring/Wiring.StudioLinks)
        public const int Lean = 0, AsPlanned = 1, Ambitious = 2;
        public const int SelfPublish = 0, Publisher = 1;
        public const int NoDrm = 0, WithDrm = 1;
        public const int Cut = 0, Hold = 1, Delay = 2;
        public const int Crunch = 0, Normal = 1, AddBudget = 2;
        public const int ByTeam = 0, ClosedBeta = 1, OpenBeta = 2;
        public const int FixAll = 0, FixWorst = 1, ShipThem = 2;
        public const int Hotfix = 0, FullPatch = 1, LeaveBugs = 2;
        public const int FreeUpdate = 0, AddOn = 1, Listen = 2;
        public const int HoldPrice = 0, Sale = 1, RemoveDrm = 2;

        // the studio's desk sessions (Wiring/Wiring.StudioLinks), with the ids their records are saved under: never renamed
        public const int SessionPitch = 0, SessionMilestone = 1, SessionPlaytest = 2, SessionPostLaunch = 3, Sessions = 4;
        public static readonly string[] SessionIds = { "studio_pitch", "studio_milestone", "studio_playtest", "studio_postlaunch" };

        // marketing beats (the game's page)
        public const int BeatAnnounce = 0, BeatTrailer = 1, BeatExpo = 2, BeatPreviews = 3, BeatDevlog = 4, Beats = 5;
        public const int Announced = 1, PreviewsHeld = 2;   // GameDev.Beats bits

        // what the team builds after launch (GameDev.Project)
        public const int ProjectNone = 0, ProjectUpdate = 1, ProjectAddOn = 2, ProjectPort = 3, ProjectLocalise = 4;

        // ---------------------------------------------------------------- the plan

        public static long BudgetAt(int step) => StudioCatalogue.Budgets[Clamp(step, 0, StudioCatalogue.Budgets.Length - 1)];

        public static int ScopeOf(long budget) => (int)ScaleRules.ScopeOf(budget);

        /// <summary>The usual price of a game of the scope on a platform, cents: the market's going price the concept shows.</summary>
        public static long UsualPrice(int scope, int platform)
        {
            return ScaleCatalogue.Price[Clamp(scope, 0, 2)] * StudioCatalogue.PlatformPrice[Clamp(platform, 0, StudioCatalogue.GamePlatforms - 1)] / 1000;
        }

        /// <summary>The price a concept's step shows on its platform, cents: a copy, a month's subscription, or nothing for a free game.</summary>
        public static long PriceShown(int model, int step, int scope, int platform)
        {
            int at = StudioCatalogue.PriceSteps[Clamp(step, 0, StudioCatalogue.PriceSteps.Length - 1)];
            if (model == (int)GameModel.FreeToPlay)
            {
                return 0;
            }
            long usual = model == (int)GameModel.Subscription ? UsualPrice(scope, platform) * StudioCatalogue.SubscribeMonth / 1000 : UsualPrice(scope, platform);
            return usual * at / 1000;
        }

        /// <summary>A game's price on a platform, cents, from its price in PC terms (a port sells at that platform's going rate).</summary>
        public static long PriceOn(GameDev g, int platform)
        {
            return g.Price * StudioCatalogue.PlatformPrice[Clamp(platform, 0, StudioCatalogue.GamePlatforms - 1)] / 1000;
        }

        /// <summary>
        /// The work points a budget buys: its team share (after the scope's usual marketing and the launch) over a point's
        /// cost, by the ambition, less on the studio's own engine. A real team takes the time its points take.
        /// </summary>
        public static long WorkFor(long budget, int ambition, bool ownEngine)
        {
            int scope = ScopeOf(budget);
            long team = Math.Max(0L, budget) * (1000 - ScaleCatalogue.MarketingShare[scope] - StudioCatalogue.LaunchShare) / 1000;
            long work = team / StudioCatalogue.PointCents;
            work = work * (ambition == Lean ? StudioCatalogue.AmbitionLean : ambition == Ambitious ? StudioCatalogue.AmbitionBold : 1000) / 1000;
            if (ownEngine)
            {
                work = work * StudioCatalogue.OwnEngineWork / 1000;
            }
            return Math.Max(100L, work);
        }

        /// <summary>Days a team of so many points a day takes over the work left; 0 with no points (the page says nobody builds it).</summary>
        public static int DaysLeft(long left, long pointsADay)
        {
            return pointsADay <= 0 ? 0 : (int)Math.Min(99999L, (Math.Max(0L, left) + pointsADay - 1) / pointsADay);
        }

        /// <summary>
        /// Thousandths of its genre's players a game of a budget is seen by: the bands' audience for its budget
        /// (ScaleRules.TypicalAudience) over the average genre's PC players, all of them at the most.
        /// </summary>
        public static int Visibility(long budget)
        {
            return (int)Math.Max(1L, Math.Min(1000L, ScaleRules.TypicalAudience(budget) * 1000 / StudioCatalogue.ReferenceMarket));
        }

        /// <summary>The setting in a genre's slot, and how well it fits (thousandths).</summary>
        public static int SettingOf(int genre, int slot) => StudioCatalogue.GenreSettings[Clamp(genre, 0, (int)Genre.Count - 1)][Clamp(slot, 0, 3)];

        public static int Fit(int genre, int slot) => StudioCatalogue.GenreFit[Clamp(genre, 0, (int)Genre.Count - 1)][Clamp(slot, 0, 3)];

        /// <summary>The genre's audience lifted while it is the hot genre a hit started.</summary>
        public static int Trend(StudioFile f, int genre, int day)
        {
            return f != null && f.TrendGenre == genre && day < f.TrendUntil ? 1000 + StudioCatalogue.TrendLift : 1000;
        }

        /// <summary>True if the studio has shipped a game in this genre and setting: its page may then say how the pairing went.</summary>
        public static bool Knows(StudioFile f, string place, int genre, int slot)
        {
            for (int i = 0; f != null && i < f.Games.Count; i++)
            {
                GameDev g = f.Games[i];
                if (g.Place == place && g.Genre == genre && g.Setting == slot && g.Score >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        // ---------------------------------------------------------------- the team's craft

        /// <summary>What a person brings to a part, 0 to 100: a base, their skill, their level, and their days in the genre.</summary>
        public static int Craft(int skill, int level, int experienceDays)
        {
            int c = StudioCatalogue.CraftBase + Clamp(skill, 0, 100) * StudioCatalogue.CraftPerSkill / 1000 + Clamp(level, 0, TalentCatalogue.Levels - 1) * StudioCatalogue.CraftPerLevel;
            c += Math.Min(StudioCatalogue.CraftExperienceMost, Math.Max(0, experienceDays) * StudioCatalogue.CraftExperienceMost / StudioCatalogue.ExperienceFull);
            return Clamp(c, 0, 100);
        }

        public static int ExperienceDays(StudioFile f, string id, int genre)
        {
            GenreExperience e = f != null ? f.ExperienceOf(id) : null;
            return e != null ? e.Days[Clamp(genre, 0, (int)Genre.Count - 1)] : 0;
        }

        /// <summary>A day in a genre for a person who worked it: person-days, a full shift one, up to the most kept.</summary>
        public static void Learn(StudioFile f, string id, int genre, int hours)
        {
            if (f == null || string.IsNullOrEmpty(id) || hours <= 0)
            {
                return;
            }
            GenreExperience e = f.ExperienceOf(id);
            if (e == null)
            {
                if (f.People.Count >= StudioCatalogue.PeopleStored)
                {
                    return;
                }
                e = new GenreExperience { Id = id };
                f.People.Add(e);
            }
            int g = Clamp(genre, 0, (int)Genre.Count - 1);
            e.Days[g] = Math.Min(StudioCatalogue.ExperienceMost, e.Days[g] + Math.Max(1, hours / TalentCatalogue.DayHours));
        }

        /// <summary>A studio's people as a crew for a game of one genre: points by family and the craft each builds with.</summary>
        public static StudioCrew CrewOf(StudioHand[] hands, int count, StudioFile f, int genre, int faults, int support)
        {
            var c = new StudioCrew { Faults = faults, Support = support };
            long ce = 0, cd = 0, cp = 0, ct = 0;
            for (int i = 0; hands != null && i < count && i < hands.Length; i++)
            {
                StudioHand h = hands[i];
                if (h.Points <= 0)
                {
                    continue;
                }
                int craft = Craft(h.Skill, h.Level, ExperienceDays(f, h.Id, genre));
                if (h.Family <= (int)Family.Infrastructure)
                {
                    if (h.Tester)
                    {
                        c.Testing += h.Points;
                        ct += (long)craft * h.Points;
                    }
                    else
                    {
                        c.Engineering += h.Points;
                        ce += (long)craft * h.Points;
                    }
                }
                else if (h.Family == (int)Family.Design)
                {
                    c.Design += h.Points;
                    cd += (long)craft * h.Points;
                }
                else if (h.Family == (int)Family.Product)
                {
                    c.Product += h.Points;
                    cp += (long)craft * h.Points;
                }
            }
            c.CraftEngineering = Weighed(ce, c.Engineering);
            c.CraftDesign = Weighed(cd, c.Design);
            c.CraftProduct = Weighed(cp, c.Product);
            c.CraftTesting = Weighed(ct, c.Testing);
            return c;
        }

        private static int Weighed(long sum, int points) => points > 0 ? Clamp((int)(sum / points), 0, 100) : StudioCatalogue.CraftBase;

        /// <summary>The day's points of a crew for one game: its share of the studio's (thousandths), with a crunch and contractors.</summary>
        public static int Boost(GameDev g, int day)
        {
            return 1000 + (day < g.CrunchUntil ? StudioCatalogue.CrunchOutput : 0) + (day < g.BoostUntil ? StudioCatalogue.BoostOutput : 0);
        }

        // ---------------------------------------------------------------- a day of building

        /// <summary>
        /// A day of building one game with its share of the studio's points (thousandths): testers and then engineers fix found
        /// bugs at the playtest's share, the rest goes into the parts by family (half of it to polish in the polish phase,
        /// with what testers have left), each point with its family's craft; new bugs hide in the work by the crew's faults.
        /// Returns the work points built.
        /// </summary>
        public static long Build(GameDev g, in StudioCrew c, int share, int day)
        {
            if (g == null || !g.Building)
            {
                return 0;
            }
            long boost = Boost(g, day);
            int s = Clamp(share, 0, 1000);
            long eng = (long)c.Engineering * s / 1000 * boost / 1000;
            long des = (long)c.Design * s / 1000 * boost / 1000;
            long pro = (long)c.Product * s / 1000 * boost / 1000;
            long test = (long)c.Testing * s / 1000 * boost / 1000;
            bool polish = g.Phase == (int)GamePhase.Polish;
            int fixShare = polish ? Math.Max(g.Fix, StudioCatalogue.FixSharePolish) : g.Fix;
            long byTesters = Math.Min(g.Found, test);
            long byEngineers = Math.Min(g.Found - byTesters, eng * fixShare / 1000 / FirmsCatalogue.PointsPerFix);
            g.Found -= (int)(byTesters + byEngineers);
            test -= byTesters;
            eng -= byEngineers * FirmsCatalogue.PointsPerFix;
            long built = 0;
            long toPolish = polish ? test : 0, polishCraft = polish ? test * c.CraftTesting : 0;
            for (int p = 0; p < (int)Part.Count; p++)
            {
                long e = eng * StudioCatalogue.EngineeringSplit[p] / 1000, d = des * StudioCatalogue.DesignSplit[p] / 1000, r = pro * StudioCatalogue.ProductSplit[p] / 1000;
                long pts = e + d + r;
                long craft = e * c.CraftEngineering + d * c.CraftDesign + r * c.CraftProduct;
                built += pts;
                if (polish && p != (int)Part.Polish && pts > 0)
                {
                    long moved = pts * StudioCatalogue.PolishPhaseShare / 1000;
                    long movedCraft = craft * moved / pts;
                    toPolish += moved;
                    polishCraft += movedCraft;
                    pts -= moved;
                    craft -= movedCraft;
                }
                g.PartDone[p] += pts;
                g.PartCraft[p] += craft;
            }
            if (day < g.CrunchUntil)
            {
                polishCraft = Math.Max(0L, polishCraft - toPolish * StudioCatalogue.CrunchPolish);   // a tired team polishes worse
            }
            g.PartDone[(int)Part.Polish] += toPolish;
            g.PartCraft[(int)Part.Polish] += polishCraft;
            built += polish ? test : 0;
            g.Done = Math.Min(Math.Max(g.Work, g.Done), g.Done + built);
            // new bugs hide in the work: half the rate a service's features take, carried in thousandths so a small team's few points count too
            long bugs = built * Math.Max(30, c.Faults) + g.BugCarry;
            g.Hidden = (int)Math.Min(int.MaxValue, g.Hidden + bugs / 2000);
            g.BugCarry = bugs % 2000;
            return built;
        }

        /// <summary>
        /// The phase a game's work has reached: a milestone review waits at the end of pre-production and of production (a
        /// newer one takes an older one's place), and a finished game waits for its day (a console game for certification).
        /// Returns 1 for a milestone reached, 2 for finished, 0 for neither.
        /// </summary>
        public static int Advance(GameDev g, int day)
        {
            if (g == null || g.Phase > (int)GamePhase.Polish)
            {
                return 0;
            }
            long at = g.Done * 1000 / Math.Max(1L, g.Work);
            int result = 0;
            if (g.Phase == (int)GamePhase.Pre && at >= StudioCatalogue.PhaseShare[0])
            {
                g.Phase = (int)GamePhase.Production;
                g.Milestone = (int)GamePhase.Pre;
                g.MilestoneDay = day;
                result = 1;
            }
            if (g.Phase == (int)GamePhase.Production && at >= StudioCatalogue.PhaseShare[0] + StudioCatalogue.PhaseShare[1])
            {
                g.Phase = (int)GamePhase.Polish;
                g.Milestone = (int)GamePhase.Production;
                g.MilestoneDay = day;
                result = 1;
            }
            if (g.Phase == (int)GamePhase.Polish && g.Done >= g.Work)
            {
                g.Phase = (int)GamePhase.Ready;
                g.Milestone = -1;
                g.CertUntil = g.On(Platform.Console) ? day + StudioCatalogue.CertificationDays : day;
                result = 2;
            }
            return result;
        }

        /// <summary>True when a milestone has waited its grace for a review: the team then holds the course by itself.</summary>
        public static bool Lapsed(GameDev g, int day) => g != null && g.Milestone >= 0 && day - g.MilestoneDay >= StudioCatalogue.MilestoneGraceDays;

        /// <summary>The thousandths of a game's work built.</summary>
        public static int Progress(GameDev g) => g == null ? 0 : (int)Math.Min(1000L, g.Done * 1000 / Math.Max(1L, g.Work));

        // ---------------------------------------------------------------- quality and bugs

        /// <summary>A part built against its plan, thousandths: as planned in full, work past the plan half again, up to the most.</summary>
        public static int Coverage(long done, long need)
        {
            if (need <= 0)
            {
                return StudioCatalogue.Coverage;
            }
            long r = Math.Max(0L, done) * 1000 / need;
            return (int)(r <= 1000 ? r : Math.Min(StudioCatalogue.CoverageMost, 1000 + (r - 1000) / 2));
        }

        /// <summary>A part's quality, 0 to 100: the craft it was built with, as far as it was built; the studio's own engine lifts tech.</summary>
        public static int PartQuality(GameDev g, int part)
        {
            int p = Clamp(part, 0, (int)Part.Count - 1);
            long need = g.Plan * StudioCatalogue.PartNeed[p] / 1000;
            int craft = g.PartDone[p] > 0 ? (int)(g.PartCraft[p] / g.PartDone[p]) : 0;
            if (p == (int)Part.Tech && g.Engine == (int)GameEngine.Own)
            {
                craft += StudioCatalogue.OwnEngineCraft;
            }
            return Clamp(craft * Coverage(g.PartDone[p], need) / 1000, 0, 100);
        }

        /// <summary>A game's quality as built, 0 to 100: its parts weighed by what its genre's players care about.</summary>
        public static int Quality(GameDev g)
        {
            if (g == null)
            {
                return 0;
            }
            int[] w = StudioCatalogue.PartWeights[Clamp(g.Genre, 0, (int)Genre.Count - 1)];
            long q = 0;
            for (int p = 0; p < (int)Part.Count; p++)
            {
                q += (long)w[p] * PartQuality(g, p);
            }
            return Clamp((int)(q / 1000), 0, 100);
        }

        /// <summary>The bugs a game holds before reviews see them in full: more for a bigger game.</summary>
        public static int BugRoom(GameDev g) => 10 + (int)Math.Min(int.MaxValue / 2, Math.Max(0L, g.Work) * StudioCatalogue.BugRoom / 1000);

        /// <summary>Points off the reviews for the bugs it ships with, found or not: up to the most at its room.</summary>
        public static int BugPenalty(GameDev g)
        {
            long bugs = (long)g.Hidden + g.Found + g.Reported;
            return (int)Math.Min(StudioCatalogue.BugPenaltyMost, bugs * StudioCatalogue.BugPenaltyMost / Math.Max(1, BugRoom(g)));
        }

        // ---------------------------------------------------------------- the desk sessions' choices

        /// <summary>A step's choice, or the given one when out of range.</summary>
        public static int Choice(int[] choices, int step, int count, int otherwise)
        {
            return choices != null && step >= 0 && step < choices.Length && choices[step] >= 0 && choices[step] < count ? choices[step] : otherwise;
        }

        /// <summary>
        /// The concept pitch held: the draft becomes a game with the ambition, the funding and the copy protection chosen. A
        /// licensed engine's fee and the scope's work are set; a publisher advances part of the budget against royalties. The
        /// product's record is the caller's. Returns the game.
        /// </summary>
        public static GameDev Greenlight(GameDraft d, int id, int[] choices, bool ownEngine, int day)
        {
            long budget = BudgetAt(d.Budget);
            int scope = ScopeOf(budget);
            int platform = Clamp(d.Platform, 0, StudioCatalogue.GamePlatforms - 1);
            int model = Clamp(d.Model, 0, (int)GameModel.Count - 1);
            bool own = d.Engine == (int)GameEngine.Own && ownEngine;
            int ambition = Choice(choices, 0, 3, AsPlanned);
            var g = new GameDev
            {
                Id = id, Place = d.Place ?? string.Empty, Genre = Clamp(d.Genre, 0, (int)Genre.Count - 1), Setting = Clamp(d.Setting, 0, 3), Platforms = 1 << platform,
                Model = model, Engine = own ? (int)GameEngine.Own : (int)GameEngine.Licensed, Scope = scope, Budget = budget, Ambition = ambition, Sequel = Math.Max(0, d.Sequel),
                Started = day, LastNews = day
            };
            g.Price = PriceShown(model, d.Price, scope, platform) * 1000 / StudioCatalogue.PlatformPrice[platform];
            g.Plan = WorkFor(budget, AsPlanned, own);
            g.Work = WorkFor(budget, ambition, own);
            g.Published = Choice(choices, 1, 2, SelfPublish) == Publisher;
            g.Advance = g.Published ? budget * StudioCatalogue.AdvanceShare / 1000 : 0;
            g.Drm = Choice(choices, 2, 2, NoDrm) == WithDrm && (model == (int)GameModel.Premium || model == (int)GameModel.EarlyAccess);
            g.Spent = own ? 0 : StudioCatalogue.EngineFee[scope];
            return g;
        }

        /// <summary>The milestone review held: the plan (cut a feature, hold the course, delay) and the pace (crunch, normal, contractors).</summary>
        public static void ApplyMilestone(GameDev g, int plan, int pace, int day)
        {
            if (plan == Cut)
            {
                g.Work = Math.Max(g.Done, g.Work * StudioCatalogue.CutWork / 1000);   // the parts' plan stays: what was cut shows in the quality
            }
            else if (plan == Delay)
            {
                g.Work = g.Work * StudioCatalogue.DelayWork / 1000;
            }
            if (pace == Crunch)
            {
                g.CrunchUntil = day + StudioCatalogue.CrunchDays;
            }
            else if (pace == AddBudget)
            {
                g.BoostUntil = day + StudioCatalogue.BoostDays;
            }
            g.Milestone = -1;
        }

        /// <summary>
        /// A playtest held: who plays it finds a share of the hidden bugs (a manager's strength of it), the fixes chosen set
        /// how much of the work fixes them; a closed beta adds a little hype and may leak, an open beta moves hype half way to
        /// what the game's quality carries. Returns the bugs found; <paramref name="hype"/> the hype moved.
        /// </summary>
        public static int ApplyPlaytest(GameDev g, int who, int fixes, int strength, int seed, int day, out int hype)
        {
            int w = Clamp(who, 0, 2);
            long find = (long)StudioCatalogue.PlaytestFinds[w] * Clamp(strength, 0, 1000) / 1000;
            int found = (int)(g.Hidden * find / 1000);
            g.Hidden -= found;
            g.Found += found;
            g.Fix = StudioCatalogue.FixShares[Clamp(fixes, 0, 2)];
            hype = 0;
            int quality = Quality(g);
            if (w == ClosedBeta)
            {
                hype = StudioCatalogue.BetaHype;
                if (TechRandom.Mille(seed, g.Id, day, 71) < StudioCatalogue.LeakChance)
                {
                    hype += (quality - 50) * 2;   // the leak shows the game as it is
                }
            }
            else if (w == OpenBeta)
            {
                hype = (quality * StudioCatalogue.BacklashPerQuality - g.Hype) / 2;
            }
            MoveHype(g, hype, day);
            return found;
        }

        /// <summary>
        /// The post-launch review held: the reported bugs (a hotfix of the worst at once, at a manager's strength, or a full
        /// patch by the team's work), the fans (a free update or an add-on started on their first request), the store (a sale
        /// of a week, or DRM taken off). Returns the bugs the hotfix fixed.
        /// </summary>
        public static int ApplyPostLaunch(GameDev g, int bugs, int fans, int store, int strength, int day)
        {
            int fixedNow = 0;
            if (bugs == Hotfix)
            {
                fixedNow = (int)((long)g.Reported * 400 * Clamp(strength, 0, 1000) / 1000000);
                g.Reported -= fixedNow;
            }
            else if (bugs == FullPatch)
            {
                g.Fix = StudioCatalogue.FixShares[FixAll];
            }
            if ((fans == FreeUpdate || fans == AddOn) && g.Project == ProjectNone)
            {
                StartProject(g, fans == FreeUpdate ? ProjectUpdate : ProjectAddOn, FirstAsk(g));
            }
            if (store == Sale && day >= g.SaleUntil)
            {
                g.SaleUntil = day + StudioCatalogue.SaleDays;
            }
            else if (store == RemoveDrm && g.Drm && !g.DrmRemoved)
            {
                g.DrmRemoved = true;
            }
            return fixedNow;
        }

        /// <summary>The first fan request still open; the comfort request when none is.</summary>
        public static int FirstAsk(GameDev g)
        {
            for (int a = 0; a < (int)FanAsk.Count; a++)
            {
                if ((g.Asks & (1 << a)) != 0)
                {
                    return a;
                }
            }
            return (int)FanAsk.Comfort;
        }

        /// <summary>A project after launch begins: its work by kind, a share of the game's.</summary>
        public static void StartProject(GameDev g, int project, int arg)
        {
            g.Project = Clamp(project, ProjectNone, ProjectLocalise);
            g.ProjectArg = arg;
            g.ProjectDone = 0;
            int share = project == ProjectUpdate ? StudioCatalogue.UpdateWork[0] : project == ProjectAddOn ? StudioCatalogue.UpdateWork[1]
                : project == ProjectPort ? StudioCatalogue.PortWork : StudioCatalogue.LocaliseWork;
            g.ProjectWork = Math.Max(10L, g.Work * share / 1000);
        }

        /// <summary>The sound choice at a step of a session, as a manager judges it (Wiring/Wiring.StudioLinks reads the game).</summary>
        public static int PickMilestone(GameDev g, int step, int day, long pointsADay)
        {
            if (step == 0)
            {
                // behind by more than half again its plan's days at the team's pace: cut; else hold
                long planned = g.Plan / Math.Max(1L, pointsADay);
                long taken = day - g.Started + DaysLeft(g.Work - g.Done, pointsADay);
                return pointsADay > 0 && taken > planned * 3 / 2 ? Cut : Hold;
            }
            return Normal;
        }

        public static int PickPlaytest(GameDev g, int step)
        {
            return step == 0 ? ClosedBeta : g.Phase == (int)GamePhase.Polish ? FixAll : FixWorst;
        }

        public static int PickPostLaunch(GameDev g, int step, int day)
        {
            switch (step)
            {
                case 0: return g.Reported > BugRoom(g) / 20 ? Hotfix : LeaveBugs;
                case 1: return g.Project == ProjectNone && g.Score >= 70 && g.Sold > 0 ? FreeUpdate : Listen;
                default: return g.Drm && !g.DrmRemoved && g.Launched >= 0 && day - g.Launched >= StudioCatalogue.DrmDays ? RemoveDrm : HoldPrice;
            }
        }

        // ---------------------------------------------------------------- hype and marketing

        /// <summary>Hype moved by news, kept within nothing and the most; any news resets the quiet spell.</summary>
        public static void MoveHype(GameDev g, int by, int day)
        {
            if (by == 0)
            {
                return;
            }
            g.Hype = Clamp(g.Hype + by, 0, StudioCatalogue.HypeMost);
            g.LastNews = day;
        }

        /// <summary>
        /// A day of hype: a running campaign keeps it rising, and a quiet spell with no news lets it fade. Returns the
        /// campaign's spend of the day, cents at the start's prices (spread over its days until its share of the budget is spent).
        /// </summary>
        public static long HypeDay(GameDev g, int day)
        {
            long total = g.Budget * StudioCatalogue.CampaignShare[Clamp(g.Campaign, 0, StudioCatalogue.CampaignShare.Length - 1)] / 1000;
            bool windowOver = g.Launched >= 0 && day - g.Launched >= StudioCatalogue.WindowDays;
            long spend = 0;
            if (total > g.CampaignSpent && !windowOver && g.Phase != (int)GamePhase.Ended)
            {
                spend = Math.Min(total - g.CampaignSpent, Math.Max(1L, total / StudioCatalogue.CampaignDays));
                g.CampaignSpent += spend;
                if (g.Launched < 0)
                {
                    MoveHype(g, StudioCatalogue.CampaignHype * g.Campaign, day);
                }
            }
            if (spend == 0 && day - g.LastNews > StudioCatalogue.HypeQuietDays && g.Hype > 0)
            {
                g.Hype = Math.Max(0, g.Hype - Math.Max(1, g.Hype * StudioCatalogue.HypeFade / 1000));
            }
            return spend;
        }

        /// <summary>
        /// Why a marketing beat cannot be done now (a key), or null: once only, too soon, not enough to show, or out of the expo's weeks
        /// (once a year of the save's <paramref name="year"/> days: the expo is the calendar's).
        /// </summary>
        public static string BeatWhy(GameDev g, int beat, int day, int yearPart, int year)
        {
            if (g == null || g.Launched >= 0 && !g.Early || g.Phase == (int)GamePhase.Ended)
            {
                return "reallife_studio_beat_why_out";
            }
            int done = Progress(g);
            switch (beat)
            {
                case BeatAnnounce:
                    return (g.Beats & Announced) != 0 ? "reallife_studio_beat_why_once" : null;
                case BeatTrailer:
                    return done < StudioCatalogue.TrailerWork ? "reallife_studio_beat_why_early" : day - g.LastTrailer < StudioCatalogue.TrailerGap ? "reallife_studio_beat_why_soon" : null;
                case BeatExpo:
                    if (done < StudioCatalogue.ExpoWork)
                    {
                        return "reallife_studio_beat_why_early";
                    }
                    if (yearPart < StudioCatalogue.ExpoFrom || yearPart >= StudioCatalogue.ExpoTo)
                    {
                        return "reallife_studio_beat_why_expo";
                    }
                    return day - g.LastExpo < Math.Max(1, year) / 2 ? "reallife_studio_beat_why_once" : null;
                case BeatPreviews:
                    return done < StudioCatalogue.PreviewWork ? "reallife_studio_beat_why_early" : (g.Beats & PreviewsHeld) != 0 ? "reallife_studio_beat_why_once" : null;
                case BeatDevlog:
                    return !StudioBridges.CanDevlog ? "reallife_studio_beat_why_flare" : day == g.LastNews ? "reallife_studio_beat_why_soon" : null;
                default:
                    return "reallife_studio_beat_why_out";
            }
        }

        /// <summary>What a beat costs, cents at the start's prices: a trailer and an expo demo by scope; the rest nothing.</summary>
        public static long BeatCost(GameDev g, int beat)
        {
            int s = Clamp(g.Scope, 0, 2);
            return beat == BeatTrailer ? StudioCatalogue.TrailerCost[s] : beat == BeatExpo ? StudioCatalogue.ExpoCost[s] : 0;
        }

        /// <summary>
        /// A beat done: an announcement (more for a sequel), a trailer, an expo demo or press previews (both show the game: a
        /// weak one loses hype), or a devlog's hype from Flare. Returns the hype moved.
        /// </summary>
        public static int Beat(GameDev g, int beat, int devlogHype, int day)
        {
            int quality = Quality(g), by = 0;
            switch (beat)
            {
                case BeatAnnounce:
                    by = StudioCatalogue.HypeAnnounce + (g.Sequel > 0 ? StudioCatalogue.HypeSequel : 0);
                    g.Beats |= Announced;
                    break;
                case BeatTrailer:
                    by = StudioCatalogue.HypeTrailer;
                    g.LastTrailer = day;
                    break;
                case BeatExpo:
                    by = StudioCatalogue.ExpoHype + (quality - 50) * StudioCatalogue.ExpoPerPoint;
                    g.LastExpo = day;
                    break;
                case BeatPreviews:
                    by = (quality - 50) * StudioCatalogue.PreviewPerPoint;
                    g.Beats |= PreviewsHeld;
                    break;
                case BeatDevlog:
                    by = Math.Max(0, devlogHype);
                    break;
            }
            g.Spent += BeatCost(g, beat);
            MoveHype(g, by, day);
            g.LastNews = day;
            return by;
        }

        // ---------------------------------------------------------------- the launch and the review

        /// <summary>True when a finished game can go on sale: ready, and a console game certified.</summary>
        public static bool CanLaunch(GameDev g, int day) => g != null && g.Phase == (int)GamePhase.Ready && day >= g.CertUntil;

        /// <summary>True when the game launches by itself today: ready on its day, or as soon as it can with no day set.</summary>
        public static bool LaunchesToday(GameDev g, int day) => CanLaunch(g, day) && (g.ReleaseDay <= 0 || day >= g.ReleaseDay);

        /// <summary>True when an early access game can open: its model, not out yet, and enough of it built to play.</summary>
        public static bool CanOpenEarly(GameDev g)
        {
            return g != null && g.M == GameModel.EarlyAccess && !g.Early && g.Launched < 0 && g.Phase >= (int)GamePhase.Production && g.Phase <= (int)GamePhase.Polish
                   && g.Done * 1000 >= g.Work * StudioCatalogue.EarlyFrom;
        }

        /// <summary>
        /// Where the critics' bar stands for a genre on a day, points: it rises each of the save's years of <paramref name="year"/> days
        /// to its most, as the studio's people's careers count them; a sequel's higher.
        /// </summary>
        public static int Expect(int genre, int day, bool sequel, int year)
        {
            long rise = (long)Math.Max(0, day) * StudioCatalogue.ExpectRise / Math.Max(CalendarRules.YearLeast, year) / 1000;
            int e = (int)Math.Min(StudioCatalogue.ExpectMost, StudioCatalogue.ExpectBase[Clamp(genre, 0, (int)Genre.Count - 1)] + rise);
            return e + (sequel ? StudioCatalogue.SequelExpect : 0);
        }

        /// <summary>Points the critics take off for hype far above what the game's quality carries, up to the most.</summary>
        public static int Backlash(int hype, int quality)
        {
            int over = hype - quality * StudioCatalogue.BacklashPerQuality;
            return Clamp(over / StudioCatalogue.BacklashStep, 0, StudioCatalogue.BacklashMost);
        }

        /// <summary>
        /// The critics' score, 0 to 100: the game's quality against its genre's bar, its bugs, DRM, a backlash for hype it
        /// cannot carry, its setting's fit, the studio's reputation, and the critics' own spread, rolled from the save's seed.
        /// </summary>
        public static int Review(GameDev g, int quality, int reputation, int day, int seed, int year)
        {
            int score = CriticsScore(quality, Expect(g.Genre, day, g.Sequel > 0, year));
            score -= BugPenalty(g);
            score -= g.Drm && !g.DrmRemoved ? StudioCatalogue.DrmReview : 0;
            score -= Backlash(g.Hype, quality);
            score += (Fit(g.Genre, g.Setting) - 1000) / StudioCatalogue.FitReviewStep;
            score += (Clamp(reputation, 0, 1000) - 500) / StudioCatalogue.ReputationReviewStep;
            score += TechRandom.Range(seed, g.Id, 73, 0, -StudioCatalogue.CriticsSpread, StudioCatalogue.CriticsSpread);
            return Clamp(score, 0, 100);
        }

        /// <summary>
        /// A game's quality as the critics score it against its genre's bar, before its bugs, DRM, backlash, fit, name and spread:
        /// at the bar <see cref="StudioCatalogue.ReviewBase"/>; under it a point and a fifth off for each point short
        /// (<see cref="StudioCatalogue.ReviewPerQuality"/>), so a weak game is punished; over it the scale's room above the base shared
        /// over the quality left above the bar, so a perfect game reviews 100 and one half way there three quarters. Nothing stretches
        /// it further: the money scale's sales climb steepest at the top, where a score ahead of quality would pay far past it.
        /// </summary>
        public static int CriticsScore(int quality, int bar)
        {
            int q = Clamp(quality, 0, 100), e = Clamp(bar, 1, 99);
            long s = q >= e
                ? StudioCatalogue.ReviewBase * 1000L + (long)(q - e) * (100 - StudioCatalogue.ReviewBase) * 1000 / (100 - e)
                : StudioCatalogue.ReviewBase * 1000L + (long)(q - e) * StudioCatalogue.ReviewPerQuality;
            return (int)(s / 1000);
        }

        // ---------------------------------------------------------------- shared

        public static int Clamp(int v, int least, int most) => v < least ? least : v > most ? most : v;

        /// <summary>The thousandths of the calendar's year a day is at (the expo's weeks, the season).</summary>
        public static int YearPart(int day, int yearDays)
        {
            int y = Math.Max(1, yearDays);
            return (int)((long)(((day % y) + y) % y) * 1000 / y);
        }
    }
}
