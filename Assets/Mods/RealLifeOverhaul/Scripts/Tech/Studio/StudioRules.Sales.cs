using System;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What a day of one game's sales came to: copies, money in cents at the start's prices, and what the studio books.</summary>
    public struct GameSales
    {
        public long Units, Pirated, AddOnUnits;
        public long Gross;        // what buyers paid
        public long Net;          // what is left after the stores, refunds and a licensed engine's royalty
        public long Payout;       // what the studio books: the net, or a publisher's royalties once its advance is earned back
        public long Royalty;      // the engine's royalty in it
        public long Servers;      // the day's servers for its players
    }

    public static partial class StudioRules
    {
        /// <summary>The share of its genre's market a typical game has among the live games a genre usually holds (the bands' share).</summary>
        public static readonly int TypicalShare = ScaleRules.Share(1000, 1000 + StudioCatalogue.TypicalRivalAppeal);

        // ---------------------------------------------------------------- one market a genre

        /// <summary>A game's appeal in its genre's market: its score, and how new it is (full for its first month, then halving).</summary>
        public static int AppealOf(int score, int age)
        {
            long a = StudioCatalogue.AppealBase + (long)StudioCatalogue.AppealPerScore * Clamp(score, 0, 100);
            int over = Math.Max(0, age - StudioCatalogue.FreshDays);
            int halvings = over / StudioCatalogue.FreshHalfLife;
            if (halvings >= 20)
            {
                return 0;
            }
            long fresh = 1000L >> halvings;
            fresh -= fresh * (over % StudioCatalogue.FreshHalfLife) / (2 * StudioCatalogue.FreshHalfLife);
            return (int)(a * fresh / 1000);
        }

        /// <summary>The appeal of every live game of each genre on a day, the player's and the rivals', into an array by Genre.</summary>
        public static void GenreAppeals(StudioFile f, int day, long[] into)
        {
            Array.Clear(into, 0, into.Length);
            for (int i = 0; f != null && i < f.Games.Count; i++)
            {
                GameDev g = f.Games[i];
                if (g.OnSale && g.Genre >= 0 && g.Genre < into.Length)
                {
                    into[g.Genre] += AppealOf(ScoreNow(g), day - g.Launched);
                }
            }
            for (int i = 0; f != null && i < f.Rivals.Count; i++)
            {
                RivalGame r = f.Rivals[i];
                if (r.Genre >= 0 && r.Genre < into.Length && day >= r.Launched)
                {
                    into[r.Genre] += AppealOf(r.Score, day - r.Launched);
                }
            }
        }

        /// <summary>The score a game sells on: the critics', or while in early access the quality it plays at.</summary>
        public static int ScoreNow(GameDev g) => g.Score >= 0 ? g.Score : Quality(g);

        /// <summary>
        /// A game's pull against its genre's other live games (ScaleRules.Share over the genre's appeal, buying nothing
        /// included), thousandths of the typical share: alone in a hot genre well over 1000, among strong rivals under.
        /// </summary>
        public static int Competition(int appeal, long genreAppeal)
        {
            return (int)((long)ScaleRules.Share(appeal, Math.Max(appeal, genreAppeal)) * 1000 / Math.Max(1, TypicalShare));
        }

        // ---------------------------------------------------------------- the audience and a game's life

        /// <summary>
        /// The people a game can reach on each platform it is sold on (into an array by Platform): its genre's audience
        /// (StudioBridges.Audience: the population, or the world), the share its budget is seen by, the genre's trend, and
        /// localisation for the world beyond the city. Returns their sum.
        /// </summary>
        public static long AudienceOf(GameDev g, StudioFile f, int day, long[] byPlatform)
        {
            int vis = Visibility(g.Budget), trend = Trend(f, g.Genre, day), year = f != null ? f.YearDays : CalendarRules.YearDefault;
            long total = 0;
            for (int p = 0; p < StudioCatalogue.GamePlatforms; p++)
            {
                if (!g.On((Platform)p))
                {
                    byPlatform[p] = 0;
                    continue;
                }
                long people = StudioBridges.Audience(g.Genre, p, day, year, out long residents);
                if (g.Localised)
                {
                    people = residents + (people - residents) * (1000 + StudioCatalogue.LocaliseLift) / 1000;   // the world beyond the city reads it in its own language
                }
                long m = people * vis / 1000 * trend / 1000;
                byPlatform[p] = m;
                total += m;
            }
            return total;
        }

        /// <summary>A game's reach now: what it launched with and what an award has added since, up to the most (its word of mouth is the money scale's tail, not a wider reach).</summary>
        public static int ReachNow(GameDev g)
        {
            return Clamp(g.Reach, 0, ScaleCatalogue.ReachMost);
        }

        /// <summary>What a price does to a platform's buyers (ScaleRules.PriceAppeal) for a game sold by the copy; a free game or a subscription none.</summary>
        public static int PriceAppealOn(GameDev g, int platform, int day)
        {
            if (g.M == GameModel.FreeToPlay || g.M == GameModel.Subscription)
            {
                return 1000;
            }
            long price = PriceOn(g, platform) * (g.Early ? StudioCatalogue.EarlyPrice : 1000) / 1000;
            return ScaleRules.PriceAppeal(price, UsualPrice(g.Scope, platform));
        }

        /// <summary>
        /// The copies (a free game: players) a game would sell over its whole life as things stand today: on each platform
        /// ScaleRules.Units over its audience, its reach, its score, its setting's fit with its pull in the genre, and its
        /// price; fewer for a paid game on phones; lifted by its updates and localisation. Before piracy. Its pull is its
        /// appeal as new against the genre's other games as they stand (<paramref name="genreAppeal"/> holds its own appeal
        /// at its age, taken out): its own ageing is the score's half-life already, while rivals ageing or new ones coming
        /// move what it has left. The audience is the only cap there is, and it grows with the platforms and the city.
        /// </summary>
        public static long LifeUnits(GameDev g, StudioFile f, int day, long genreAppeal, long[] byPlatform, out long audience)
        {
            audience = AudienceOf(g, f, day, byPlatform);
            if (audience <= 0)
            {
                return 0;
            }
            int score = ScoreNow(g), reach = ReachNow(g);
            int age = g.Launched >= 0 ? day - g.Launched : 0;
            int mine = AppealOf(score, 0);
            long others = Math.Max(0L, genreAppeal - AppealOf(score, age));
            int fit = (int)((long)Fit(g.Genre, g.Setting) * Competition(mine, mine + others) / 1000);
            long units = 0;
            for (int p = 0; p < StudioCatalogue.GamePlatforms; p++)
            {
                if (byPlatform[p] <= 0)
                {
                    continue;
                }
                long u = ScaleRules.Units(byPlatform[p], reach, score, fit, PriceAppealOn(g, p, day));
                if (p == (int)Platform.Mobile && (g.M == GameModel.Premium || g.M == GameModel.EarlyAccess))
                {
                    u = u * StudioCatalogue.MobilePremium / 1000;
                }
                units += u;
            }
            units = units * Math.Max(1000, g.Lift) / 1000;
            if (g.M == GameModel.FreeToPlay)
            {
                units = units * StudioCatalogue.F2PPlayers / 1000;
            }
            return Math.Min(audience, units);
        }

        /// <summary>
        /// Thousandths of would-be buyers who pirate a game sold for a price: by its platforms' rates, more for a hyped one;
        /// DRM cuts it hard in its first days and less once cracked, until it is taken off. None for a free game or a subscription.
        /// </summary>
        public static int PirateShare(GameDev g, int day, long[] byPlatform, long audience)
        {
            if (g.M == GameModel.FreeToPlay || g.M == GameModel.Subscription || audience <= 0)
            {
                return 0;
            }
            long share = 0;
            for (int p = 0; p < StudioCatalogue.GamePlatforms; p++)
            {
                share += StudioCatalogue.Piracy[p] * byPlatform[p] / audience;
            }
            share = share * (1000 + g.Hype / 2) / 1000;
            if (g.Drm && !g.DrmRemoved)
            {
                int age = g.Launched >= 0 ? day - g.Launched : 0;
                share = share * (age < StudioCatalogue.DrmDays ? StudioCatalogue.DrmEarly : StudioCatalogue.DrmLater) / 1000;
            }
            return (int)Math.Min(900L, share);
        }

        /// <summary>The reach a game launches with: its campaign and a publisher's spend over its audience, its hype, and a sequel's fans.</summary>
        public static int LaunchReach(GameDev g, long audience, long fans)
        {
            long spend = g.CampaignSpent + (g.Published ? g.Budget * StudioCatalogue.PublisherMarketing / 1000 : 0);
            int reach = ScaleRules.Reach(spend, audience);
            reach = (int)((long)reach * (1000 + (long)g.Hype * StudioCatalogue.HypeReach / StudioCatalogue.HypeMost) / 1000);
            if (audience > 0 && fans > 0)
            {
                reach += (int)Math.Min(1000L, fans * 1000 / audience);
            }
            return Clamp(reach, ScaleCatalogue.ReachFloor, ScaleCatalogue.ReachMost);
        }

        /// <summary>The buyers of a game who follow its sequel: a share by its score, halved once a weak sequel has burnt the name.</summary>
        public static long FansOf(GameDev prior)
        {
            if (prior == null || prior.Score < 0)
            {
                return 0;
            }
            long fans = prior.Sold * StudioCatalogue.FansPerScore * prior.Score / 1000;
            return prior.Burnt ? fans / 2 : fans;
        }

        /// <summary>
        /// An early access game opens: on sale from today at the early price, selling on the quality it plays at, its reach
        /// set; the critics wait for its full release.
        /// </summary>
        public static void OpenEarly(GameDev g, StudioFile f, int day, long genreAppeal, long[] byPlatform, long fans)
        {
            g.Early = true;
            g.Launched = day;
            g.Quality = Quality(g);
            long life = LifeUnits(g, f, day, genreAppeal, byPlatform, out long audience);
            g.Reach = LaunchReach(g, audience, fans);
            life = LifeUnits(g, f, day, genreAppeal, byPlatform, out audience);
            int pirate = PirateShare(g, day, byPlatform, audience);
            OpenWindow(g, day, life * (1000 - pirate) / 1000, StudioCatalogue.EarlyRelaunch);
        }

        /// <summary>
        /// The full launch (a finished game on its day, or an early access game's full release): the critics' score, the
        /// reach (an early access game keeps its own), the fans' first requests, and a launch window of the life's launch share
        /// still to come (an early access game's buyers so far take their part of it).
        /// </summary>
        public static void Release(GameDev g, StudioFile f, int day, int reputation, int seed, long genreAppeal, long[] byPlatform, long fans)
        {
            bool early = g.Early;
            g.Quality = Quality(g);
            g.Score = Review(g, g.Quality, reputation, day, seed, f != null ? f.YearDays : CalendarRules.YearDefault);
            WriteReviews(g, seed, day);
            g.Phase = (int)GamePhase.Live;
            g.Early = false;
            if (!early)
            {
                g.Launched = day;
                AudienceOf(g, f, day, byPlatform);
                long audience = 0;
                for (int p = 0; p < StudioCatalogue.GamePlatforms; p++)
                {
                    audience += byPlatform[p];
                }
                g.Reach = LaunchReach(g, audience, fans);
            }
            g.Asks = 0;
            for (int n = 0; n < 3; n++)
            {
                g.Asks |= 1 << TechRandom.Range(seed, g.Id, 75, n, 0, (int)FanAsk.Count - 1);
            }
            long life = LifeUnits(g, f, day, genreAppeal, byPlatform, out long reached);
            int pirate = PirateShare(g, day, byPlatform, reached);
            long left = Math.Max(0L, life * (1000 - pirate) / 1000 - g.Sold);
            g.WindowStart = -1;
            OpenWindow(g, day, left, early ? StudioCatalogue.EarlyRelaunch : 1000);
        }

        /// <summary>A launch window opens on copies still to come: the launch share of them (by the score's tail) sells over its days, front loaded.</summary>
        public static void OpenWindow(GameDev g, int day, long units, int strength)
        {
            long w = Math.Max(0L, units) * 1000 / ScaleRules.ByQuality(ScaleCatalogue.Tail, ScoreNow(g)) * Math.Max(0, strength) / 1000;
            if (g.WindowStart >= 0 && day - g.WindowStart < StudioCatalogue.WindowDays)
            {
                g.WindowUnits += w;   // an earlier window still runs: this one adds to it
                return;
            }
            g.WindowStart = day;
            g.WindowUnits = w;
        }

        /// <summary>The season's pull on a day's buyers, thousandths: the year's end brings more, summer fewer.</summary>
        public static int Season(int day, int yearDays)
        {
            int part = YearPart(day, yearDays);
            return part >= StudioCatalogue.HolidayFrom ? StudioCatalogue.SeasonHoliday
                : part >= StudioCatalogue.SummerFrom && part < StudioCatalogue.SummerTo ? StudioCatalogue.SeasonSummer : 1000;
        }

        /// <summary>
        /// A day on sale: the copies of the day (the launch window's share, then the life still to come halving over the
        /// score's half-life; a game of an older save on its old half-life), by the season, the economy and a sale; piracy
        /// counted; the money by model (copies, a free game's players spending, a subscription's month) across its platforms'
        /// stores, refunds (more for reported bugs left), a licensed engine's royalty past its free first million, a
        /// publisher's split; servers for online games; an add-on's own window.
        /// </summary>
        public static GameSales SellDay(GameDev g, StudioFile f, int day, long genreAppeal, long[] byPlatform, int spend, int season)
        {
            var r = new GameSales();
            if (g == null || !g.OnSale)
            {
                return r;
            }
            int score = ScoreNow(g);
            long audience = 0;
            int pirate = 0;
            long life;
            int halfLife;
            if (g.Converted)
            {
                life = g.Sold + g.Left;
                halfLife = FirmsCatalogue.GameHalfLife;
                AudienceOf(g, f, day, byPlatform);
                for (int p = 0; p < StudioCatalogue.GamePlatforms; p++)
                {
                    audience += byPlatform[p];
                }
            }
            else
            {
                long would = LifeUnits(g, f, day, genreAppeal, byPlatform, out audience);
                pirate = PirateShare(g, day, byPlatform, audience);
                life = would * (1000 - pirate) / 1000;
                halfLife = ScaleRules.HalfLife(score);
            }
            long left = Math.Max(0L, life - g.Sold);
            int k = day - g.WindowStart;
            long micro = g.WindowStart >= 0 && k >= 0 && k < StudioCatalogue.WindowDays && !g.Converted
                ? g.WindowUnits * (StudioCatalogue.WindowDays - k) * 1000000L / StudioCatalogue.WindowWeights
                : left * 693000L / Math.Max(1, halfLife);
            bool sale = day < g.SaleUntil;
            micro = micro / 1000 * Math.Max(0, season) / 1000 * Math.Max(0, spend) * (sale ? StudioCatalogue.SaleBuyers : 1000) / 1000;
            micro += g.Carry;
            long units = Math.Min(left, micro / 1000000L);
            g.Carry = units < left ? micro % 1000000L : 0;
            g.Sold += units;
            RecordSales(g, day, units);
            if (g.Converted)
            {
                g.Left = Math.Max(0L, g.Left - units);
            }
            r.Units = units;
            r.Pirated = pirate > 0 ? units * pirate / Math.Max(1, 1000 - pirate) : 0;
            g.Pirated += r.Pirated;
            // money across the platforms, by each one's share of the audience
            if (g.M == GameModel.Subscription)
            {
                int take = StudioCatalogue.SubscribeTake[StudioCatalogue.Online[Clamp(g.Genre, 0, (int)Genre.Count - 1)] ? 1 : 0];
                long churn = Math.Max(0, StudioCatalogue.ChurnBase * 1000 - StudioCatalogue.ChurnPerScore * score) / 1000;   // thousandths a month
                g.Subscribers = Math.Max(0L, g.Subscribers + units * take / 1000 - g.Subscribers * churn / 30000);
            }
            for (int p = 0; p < StudioCatalogue.GamePlatforms; p++)
            {
                if (byPlatform[p] <= 0 || audience <= 0)
                {
                    continue;
                }
                long gross;
                switch (g.M)
                {
                    case GameModel.FreeToPlay:
                        gross = units * byPlatform[p] / audience * UsualPrice(g.Scope, p) * StudioCatalogue.F2PSpend / 1000;
                        break;
                    case GameModel.Subscription:
                        gross = g.Subscribers * byPlatform[p] / audience * PriceOn(g, p) / 30;
                        break;
                    default:
                        gross = units * byPlatform[p] / audience * PriceOn(g, p) * (sale ? StudioCatalogue.SalePrice : 1000) / 1000 * (g.Early ? StudioCatalogue.EarlyPrice : 1000) / 1000;
                        break;
                }
                r.Gross += gross;
                r.Net += ScaleRules.Net(gross, (Platform)p, false);
            }
            AddOnDay(g, day, ref r);
            int bugRefunds = (int)Math.Min(StudioCatalogue.BugRefundsMost, (long)g.Reported * StudioCatalogue.BugRefundsMost / Math.Max(1, BugRoom(g) / 2));
            r.Net -= r.Gross * bugRefunds / 1000;
            if (g.Engine == (int)GameEngine.Licensed)
            {
                long free = Math.Max(0L, ScaleCatalogue.EngineRoyaltyFree - g.RoyaltyGross);
                r.Royalty = Math.Max(0L, r.Gross - free) * ScaleCatalogue.EngineRoyalty / 1000;
                g.RoyaltyGross += r.Gross;
                r.Net -= r.Royalty;
            }
            r.Net = Math.Max(0L, r.Net);
            if (g.Published)
            {
                long before = Math.Max(0L, g.Royalties - g.Advance);
                g.Royalties += r.Net * StudioCatalogue.Royalty / 1000;
                r.Payout = Math.Max(0L, g.Royalties - g.Advance) - before;
            }
            else
            {
                r.Payout = r.Net;
            }
            // players still playing, for the servers of an online game and the support
            g.Active = Math.Max(0L, g.Active + units - g.Active / Math.Max(1, halfLife));
            if (StudioCatalogue.Online[Clamp(g.Genre, 0, (int)Genre.Count - 1)] || g.M == GameModel.FreeToPlay || g.M == GameModel.Subscription)
            {
                r.Servers = ScaleRules.Servers(g.Active) / 30;
            }
            g.Gross += r.Gross;
            g.Net += r.Payout;
            g.NetMonth += r.Payout;
            return r;
        }

        // An add-on's window: its copies to owners over its days, at its share of the game's price (on the first platform's store).
        private static void AddOnDay(GameDev g, int day, ref GameSales r)
        {
            int k = day - g.AddOnStart;
            if (g.AddOnStart < 0 || k < 0 || k >= StudioCatalogue.WindowDays || g.AddOnWindow <= 0)
            {
                return;
            }
            long units = g.AddOnWindow * (StudioCatalogue.WindowDays - k) / StudioCatalogue.WindowWeights;
            g.AddOnSold += units;
            r.AddOnUnits = units;
            int first = FirstPlatform(g);
            long gross = units * PriceOn(g, first) * StudioCatalogue.AddOnPrice / 1000;
            r.Gross += gross;
            r.Net += ScaleRules.Net(gross, (Platform)first, false);
        }

        /// <summary>The first platform a game is sold on.</summary>
        public static int FirstPlatform(GameDev g)
        {
            for (int p = 0; p < StudioCatalogue.GamePlatforms; p++)
            {
                if (g.On((Platform)p))
                {
                    return p;
                }
            }
            return (int)Platform.Pc;
        }

        /// <summary>Support tickets a day of a game's players still playing.</summary>
        public static long Tickets(GameDev g) => g.Active * StudioCatalogue.TicketsPerThousand / 1000;

        /// <summary>The reputation a studio loses in a day for the tickets its support people leave unanswered: none when all are answered.</summary>
        public static int TicketToll(long tickets, long answered)
        {
            if (tickets <= 0 || answered >= tickets)
            {
                return 0;
            }
            long unanswered = (tickets - Math.Max(0L, answered)) * 1000 / tickets;
            return (int)Math.Max(1L, unanswered / StudioCatalogue.TicketReputationStep);
        }

        // ---------------------------------------------------------------- after launch

        /// <summary>Players report a share of the bugs still hidden each day (at least one while any hide).</summary>
        public static int ReportDay(GameDev g)
        {
            if (g == null || !g.OnSale || g.Hidden <= 0)
            {
                return 0;
            }
            int n = Math.Max(1, (int)((long)g.Hidden * StudioCatalogue.ReportPerDay / 1000));
            g.Hidden -= n;
            g.Reported += n;
            return n;
        }

        /// <summary>
        /// A day of the team on a game on sale and finished: testers and engineers fix reported bugs at the patch's share,
        /// the rest builds the project under way. Returns the project finished today (its kind), or none.
        /// </summary>
        public static int AfterLaunch(GameDev g, in StudioCrew c, int share, int day)
        {
            if (g == null || !g.OnSale || g.Building)
            {
                return ProjectNone;
            }
            long boost = Boost(g, day);
            int s = Clamp(share, 0, 1000);
            long eng = (long)c.Engineering * s / 1000 * boost / 1000, des = (long)c.Design * s / 1000 * boost / 1000;
            long pro = (long)c.Product * s / 1000 * boost / 1000, test = (long)c.Testing * s / 1000 * boost / 1000;
            int fixShare = Math.Max(g.Fix, StudioCatalogue.FixShareBuild);
            long byTesters = Math.Min(g.Reported, test);
            long byEngineers = Math.Min(g.Reported - byTesters, eng * fixShare / 1000 / FirmsCatalogue.PointsPerFix);
            g.Reported -= (int)(byTesters + byEngineers);
            if (g.Reported == 0 && g.Fix > StudioCatalogue.FixShareBuild)
            {
                g.Fix = StudioCatalogue.FixShareBuild;   // the patch is out
            }
            if (g.Project == ProjectNone)
            {
                return ProjectNone;
            }
            g.ProjectDone += eng - byEngineers * FirmsCatalogue.PointsPerFix + des + pro + (test - byTesters);
            if (g.ProjectDone < g.ProjectWork)
            {
                return ProjectNone;
            }
            int done = g.Project;
            g.Project = ProjectNone;
            return done;
        }

        /// <summary>
        /// A project after launch released: a free update lifts the life's sales and the studio's name and opens a window, an
        /// add-on sells to owners by the score, a port opens a new platform's store, localisation the world's languages. The
        /// request it answered is closed and a new one opens. Returns the copies the new window sells (none for an add-on).
        /// </summary>
        public static long Released(GameDev g, int project, StudioFile f, int day, long genreAppeal, long[] byPlatform, int seed)
        {
            long before = LifeUnits(g, f, day, genreAppeal, byPlatform, out _);
            switch (project)
            {
                case ProjectUpdate:
                    g.Updates++;
                    g.Lift += StudioCatalogue.UpdateLift;
                    break;
                case ProjectAddOn:
                    g.AddOns++;
                    g.AddOnStart = day;
                    g.AddOnWindow = g.Sold * (StudioCatalogue.AttachLeast + StudioCatalogue.AttachPerScore * ScoreNow(g)) / 1000;
                    break;
                case ProjectPort:
                    g.Platforms |= 1 << Clamp(g.ProjectArg, 0, StudioCatalogue.GamePlatforms - 1);
                    break;
                case ProjectLocalise:
                    g.Localised = true;
                    break;
            }
            if (project == ProjectUpdate || project == ProjectAddOn)
            {
                g.Asks &= ~(1 << Clamp(g.ProjectArg, 0, (int)FanAsk.Count - 1));
                g.Asks |= 1 << TechRandom.Range(seed, g.Id, 77, g.Updates + g.AddOns, 0, (int)FanAsk.Count - 1);   // the fans ask for something new
            }
            if (project == ProjectAddOn)
            {
                return 0;
            }
            long after = LifeUnits(g, f, day, genreAppeal, byPlatform, out long audience);
            int pirate = PirateShare(g, day, byPlatform, audience);
            long more = Math.Max(0L, (after - before) * (1000 - pirate) / 1000);
            OpenWindow(g, day, more, 1000);
            return more;
        }

        /// <summary>Why a project cannot start on a game now (a key), or null: on sale and finished, nothing under way, and the project's own needs.</summary>
        public static string ProjectWhy(GameDev g, int project, int arg)
        {
            if (g == null || !g.OnSale || g.Building)
            {
                return "reallife_studio_project_why_not_out";
            }
            if (g.Project != ProjectNone)
            {
                return "reallife_studio_project_why_busy";
            }
            if (project == ProjectPort && (arg < 0 || arg >= StudioCatalogue.GamePlatforms || g.On((Platform)arg)))
            {
                return "reallife_studio_project_why_platform";
            }
            if (project == ProjectLocalise && g.Localised)
            {
                return "reallife_studio_project_why_done";
            }
            return null;
        }

        // ---------------------------------------------------------------- bonuses and the leads' royalty

        /// <summary>A release's return, thousandths of what it cost the studio: what it kept over what it spent.</summary>
        public static int ReturnOf(GameDev g) => ScaleRules.Return(g.Net, Math.Max(1L, g.Spent));

        /// <summary>A hit: a score that starts a trend, or three times its cost back.</summary>
        public static bool Hit(GameDev g) => g.Score >= StudioCatalogue.HitScore || ReturnOf(g) >= StudioCatalogue.HitReturn;

        /// <summary>True on a release's bonus day: launched long enough ago, not settled yet.</summary>
        public static bool BonusDue(GameDev g, int day) => g != null && !g.BonusDone && g.Live && g.Launched >= 0 && day - g.Launched >= StudioCatalogue.BonusDay;

        /// <summary>The bonus pool a release pays on its day, cents at the start's prices: its share of the profit, none from a loss.</summary>
        public static long BonusPay(GameDev g)
        {
            long profit = g.Net - g.Spent;
            return profit > 0 ? profit * StudioCatalogue.BonusPool[Clamp(g.Bonus, 0, StudioCatalogue.BonusPool.Length - 1)] / 1000 : 0;
        }

        /// <summary>A month's royalty to the leads, cents at the start's prices: their share of the month's net, paid once a month after launch.</summary>
        public static long LeadsPay(GameDev g, int day)
        {
            if (g == null || g.Launched < 0 || day - Math.Max(g.Launched, g.LastLeadsPay) < 30)
            {
                return 0;
            }
            long pay = g.NetMonth * StudioCatalogue.LeadRoyalty[Clamp(g.Leads, 0, StudioCatalogue.LeadRoyalty.Length - 1)] / 1000;
            g.NetMonth = 0;
            g.LastLeadsPay = day;
            g.LeadsPaid += pay;
            return pay;
        }

        /// <summary>A rival's pull on a studio's people (thousandths for TalentRules.Approached): strong while a hit's team went without a bonus.</summary>
        public static int Pull(StudioFile f, string place, int day)
        {
            return f != null && place != null && f.SnubUntil.TryGetValue(place, out int until) && day < until ? StudioCatalogue.SnubPull : 1000;
        }

        // ---------------------------------------------------------------- sequels and the year's award

        /// <summary>True if a sequel's score burns its name: far under the first, or weak whatever the first was.</summary>
        public static bool Burns(GameDev sequel, GameDev prior)
        {
            return sequel != null && prior != null && sequel.Score >= 0 && prior.Score >= 0
                   && (sequel.Score < prior.Score - StudioCatalogue.BurnGap || sequel.Score < StudioCatalogue.BurnScore);
        }

        /// <summary>
        /// The year's award when the calendar's year turns: among the games launched in the calendar's last year with a score in
        /// the running and no award yet, the player's and the rivals', the best (the earlier launch on a tie). The year is
        /// marked either way; the first look only marks it. Returns the winner's id, -1 for none; <paramref name="rival"/> if a rival's.
        /// </summary>
        public static int Award(StudioFile f, int day, int yearDays, bool rivalsHere, out bool rival)
        {
            rival = false;
            int year = Math.Max(0, day) / Math.Max(1, yearDays);
            if (f == null || year <= f.AwardYear)
            {
                return -1;
            }
            bool first = f.AwardYear < 0;
            f.AwardYear = year;
            if (first)
            {
                return -1;
            }
            int best = -1, bestScore = StudioCatalogue.AwardScore - 1, bestDay = int.MaxValue;
            for (int i = 0; i < f.Games.Count; i++)
            {
                GameDev g = f.Games[i];
                if (g.Score > bestScore || g.Score == bestScore && g.Launched < bestDay)
                {
                    if (!g.Award && g.Launched >= 0 && day - g.Launched <= Math.Max(1, yearDays) && g.Score >= StudioCatalogue.AwardScore)
                    {
                        best = g.Id;
                        bestScore = g.Score;
                        bestDay = g.Launched;
                        rival = false;
                    }
                }
            }
            for (int i = 0; i < f.Rivals.Count; i++)
            {
                RivalGame r = f.Rivals[i];
                if (!r.Award && day - r.Launched <= Math.Max(1, yearDays) && r.Score >= StudioCatalogue.AwardScore && (r.Score > bestScore || r.Score == bestScore && r.Launched < bestDay))
                {
                    best = r.Id;
                    bestScore = r.Score;
                    bestDay = r.Launched;
                    rival = true;
                }
            }
            if (best < 0)
            {
                return -1;
            }
            if (rival)
            {
                for (int i = 0; rivalsHere && i < f.Rivals.Count; i++)
                {
                    f.Rivals[i].Award |= f.Rivals[i].Id == best;   // a guest leaves the rivals' marks to the host
                }
                return best;
            }
            GameDev won = f.GameOf(best);
            won.Award = true;
            won.Reach = Math.Min(ScaleCatalogue.ReachMost, won.Reach + StudioCatalogue.AwardReach);
            return best;
        }

        // ---------------------------------------------------------------- an older save's games

        /// <summary>The score shown for a game of an older save, from its quality there (300 to 1000): 40 to 90.</summary>
        public static int OldScore(int quality) => Clamp(40 + (quality - 300) * 50 / 700, 0, 100);

        /// <summary>A day's copies of a game by the older rules (FirmsRules.Sell's game branch), at its age: what a converted game goes on from.</summary>
        public static double OldDaily(int appeal, int hype, int reach, int spend, int age)
        {
            double decay = Math.Pow(0.5, Math.Max(0, age) / (double)FirmsCatalogue.GameHalfLife);
            return FirmsCatalogue.GamePlayers * FirmsCatalogue.GameLaunchShare / 1e9 * appeal * (1000 + hype) / 1000.0 * reach / 1000.0 * spend / 1000.0 * decay;
        }

        private static readonly string[][] NameHints =
        {
            new[] { "fight", "brawl", "blade", "ninja" }, new[] { "quest", "voyage", "journey", "island" }, new[] { "legend", "saga", "dragon", "realm" },
            new[] { "empire", "kingdom", "war", "conquest" }, new[] { "farm", "city", "tycoon", "life" }, new[] { "ball", "league", "goal", "cup" },
            new[] { "race", "drift", "speed", "kart" }, new[] { "puzzle", "block", "match", "maze" }, new[] { "dead", "night", "fear", "haunt" },
            new[] { "strike", "shot", "squad", "gun" }, new[] { "pop", "tap", "swipe", "candy" }, new[] { "family", "party", "pet", "friends" }
        };

        /// <summary>The genre a game of an older save is given: one its name hints at, or one rolled from its id.</summary>
        public static int GenreOfName(string name, int id, int seed)
        {
            string n = (name ?? string.Empty).ToLowerInvariant();
            for (int g = 0; g < NameHints.Length; g++)
            {
                for (int i = 0; i < NameHints[g].Length; i++)
                {
                    if (n.IndexOf(NameHints[g][i], StringComparison.Ordinal) >= 0)
                    {
                        return g;
                    }
                }
            }
            return TechRandom.Range(seed, id, 79, 0, 0, (int)Genre.Count - 1);
        }

        /// <summary>
        /// A game of an older save in this model, so nothing jumps: a genre from its name or rolled, on PC, sold by the copy at
        /// its old price, its old size of work (a game half built stays half built) at an ordinary team's craft, its bugs and
        /// hype kept; one on sale keeps its copies, takings and day, and sells on from its old day's copies on its old half-life.
        /// </summary>
        public static GameDev Convert(Product p, int quality, long oldDaily, int seed)
        {
            int scope = Clamp(p.Scope, 0, 2);
            var g = new GameDev
            {
                Id = p.Id, Place = p.Place ?? string.Empty, Genre = GenreOfName(p.Name, p.Id, seed), Setting = TechRandom.Range(seed, p.Id, 80, 0, 0, 3),
                Platforms = 1 << (int)Platform.Pc, Model = (int)GameModel.Premium, Engine = (int)GameEngine.Licensed, Scope = scope,
                Budget = ScaleCatalogue.BudgetTypical[scope], Price = FirmsCatalogue.GamePrice[scope], Started = p.Launched, LastNews = p.Launched,
                Work = FirmsCatalogue.GameScope[scope], Plan = FirmsCatalogue.GameScope[scope], Hidden = Math.Max(0, p.Bugs),
                Hype = Clamp(p.Hype / 2, 0, StudioCatalogue.HypeMost), Campaign = Clamp(p.Marketing, 0, StudioCatalogue.CampaignShare.Length - 1)
            };
            g.Done = Math.Min(g.Work, Math.Max(0, (long)p.Progress));
            int craft = ScaleCatalogue.TeamQuality[scope];
            for (int part = 0; part < (int)Part.Count; part++)
            {
                g.PartDone[part] = g.Done * StudioCatalogue.PartNeed[part] / 1000;
                g.PartCraft[part] = g.PartDone[part] * craft;
            }
            long at = g.Done * 1000 / Math.Max(1L, g.Work);
            g.Phase = at >= 1000 ? (int)GamePhase.Ready : at >= StudioCatalogue.PhaseShare[0] + StudioCatalogue.PhaseShare[1] ? (int)GamePhase.Polish
                : at >= StudioCatalogue.PhaseShare[0] ? (int)GamePhase.Production : (int)GamePhase.Pre;
            if (p.Stage == 1)
            {
                g.Phase = (int)GamePhase.Live;
                g.Launched = p.Launched;
                g.Score = OldScore(quality);
                g.Quality = g.Score;
                g.Sold = p.Users;
                g.Net = p.Revenue;
                g.Gross = p.Revenue * 1000 / (1000 - FirmsCatalogue.AppStoreCut);
                g.Converted = true;
                g.Left = (long)(Math.Max(0.0, oldDaily) * FirmsCatalogue.GameHalfLife / 0.693);
                g.Reach = ScaleCatalogue.ReachFloor;
                g.BonusDone = true;
            }
            else if (p.Stage >= 2)
            {
                g.Phase = (int)GamePhase.Ended;
                g.Launched = p.Launched;
                g.Score = OldScore(quality);
                g.Sold = p.Users;
                g.Net = p.Revenue;
                g.BonusDone = true;
            }
            return g;
        }
    }
}
