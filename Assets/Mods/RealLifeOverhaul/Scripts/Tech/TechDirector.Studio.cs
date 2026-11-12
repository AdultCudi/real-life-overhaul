using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The game studio part (plans/tech-overhaul DRAFT2 section 3), under the master option Living population and its own
    /// switch: every game of the player's game studios runs by <see cref="StudioRules"/> from the concept pitched at the
    /// desk to its life after launch, sold to the audience of its genre (StudioBridges, the population when wired) in one
    /// market a genre with the rival studios, its money booked at the firm as every tech firm's is. The rivals run here on
    /// the host and travel to Going Public guests as a part of the shared city. Off, the firms run games as before, and the
    /// products are left as the old rules can carry on with. The cost: nothing per frame; the work runs on the firms' day
    /// (a pass over the studio's people and games), a page builds its screen when drawn.
    /// </summary>
    public sealed partial class TechDirector
    {
        private StudioFile _studio;
        private bool _studioOn;
        private readonly long[] _genreAppeal = new long[(int)Genre.Count];
        private readonly long[] _platforms = new long[StudioCatalogue.GamePlatforms];
        private readonly StudioCrew[] _crewOf = new StudioCrew[(int)Genre.Count];
        private readonly bool[] _crewRead = new bool[(int)Genre.Count];
        private readonly List<RivalGame> _released = new List<RivalGame>(4);
        private readonly Dictionary<string, long> _studioPoints = new Dictionary<string, long>(4);   // a studio's points a day for its games, as its last day gave them
        private readonly Dictionary<string, long> _studioTickets = new Dictionary<string, long>(4);  // a studio's support tickets a day, as its last day gave them
        private StudioHand[] _hands = new StudioHand[32];

        /// <summary>The studio's file; null while the part is off.</summary>
        public StudioFile Studio => _studioOn ? _studio : null;

        /// <summary>True while the options want the studio: the overhaul's master switch and the studio's own.</summary>
        public static bool StudioWanted => Settings.LivingPopulation && Settings.LivingStudio;

        /// <summary>True while the studio runs the games of firms of this kind.</summary>
        internal bool StudioRuns(FirmKind kind) => _studioOn && kind == FirmKind.Games;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // the notices of a project after launch released, by StudioRules.Project*
        private static readonly string[] ProjectNotes =
        {
            "reallife_studio_note_project_1", "reallife_studio_note_project_1", "reallife_studio_note_project_2", "reallife_studio_note_project_3", "reallife_studio_note_project_4"
        };

        // ...and of a free update and an add-on that have a name of their own (StudioViews.ProjectTitle), which the notice then says beside the game's
        private static readonly string[] NamedNotes = { "reallife_studio_note_named_1", "reallife_studio_note_named_1", "reallife_studio_note_named_2" };

        // ---------------------------------------------------------------- start, stop and the save

        private void StartStudio()
        {
            GameInstance save = Save;
            StudioFile file = StudioStore.Load(save.modData, out int version);
            bool fresh = file == null;
            if (fresh)
            {
                file = new StudioFile { Seed = new System.Random().Next(1, int.MaxValue), FirstDay = save.Day };
            }
            _studio = file;
            _studio.YearDays = GameCalendar.DaysPerYear;
            _studioOn = true;
            // an older save's rival games could share a name: named apart once by whoever owns the list (a guest takes the host's list, named there)
            int renamed = !fresh && !SharedCity.Follows(StudioShare.Part) ? StudioRules.NameRivals(_studio) : 0;
            if (renamed > 0)
            {
                Logger?.Info("Tech: " + renamed + " rival game(s) of an older save shared a name and were given their own.");
            }
            int taken = CatchUpStudio(save.Day);
            SharedCity.Register(new SharedCityPart
            {
                Id = StudioShare.Part, Name = "game studios",
                Ready = () => _studioOn && _studio != null && Save != null,
                Write = () => _studioOn && _studio != null ? StudioShare.Write(_studio) : null,
                Apply = StudioShared
            });
            TechBridges.StudioSessionsNow(true);
            Logger?.Info("Tech: the game studio " + (fresh ? "opened for this save" : "loaded (version " + version + ")") + " on day " + save.Day + ": " + _studio.Games.Count + " game(s), "
                         + _studio.Rivals.Count + " rival game(s)" + (taken > 0 ? ", " + taken + " game(s) of the firms taken into the new model" : string.Empty) + ".");
        }

        private void StopStudio(bool keep)
        {
            if (!_studioOn)
            {
                return;
            }
            SyncProducts();   // the old rules carry on from where the games stand
            if (keep && Save != null && _studio != null)
            {
                WriteStudio(Save.modData);
            }
            _studioOn = false;
            TechBridges.StudioSessionsNow(false);
        }

        private void WriteStudio(Dictionary<string, string> data)
        {
            if (_studio != null && data != null)
            {
                data[StudioStore.Key] = StudioStore.Write(_studio);
            }
        }

        // A guest: the host's rival market in place of this player's own (their games stay their own).
        private bool StudioShared(string text, bool whole)
        {
            return _studioOn && StudioShare.Apply(_studio, text);
        }

        /// <summary>
        /// Every game product with no record taken into the model (an older save's, or one made while the studio was off), and
        /// a record whose product moved on under the old rules caught up with it; records of products gone are let go.
        /// Returns how many were taken in.
        /// </summary>
        private int CatchUpStudio(int day)
        {
            if (_firmsFile == null)
            {
                return 0;
            }
            int taken = 0, cycle = HiringGame.Cycle;
            for (int i = 0; i < _firmsFile.Products.Count; i++)
            {
                Product p = _firmsFile.Products[i];
                if (p.K != ProductKind.Game)
                {
                    continue;
                }
                GameDev g = _studio.GameOf(p.Id);
                bool launchedOld = g != null && p.Stage == 1 && g.Launched < 0;   // launched by the old rules while the studio was off
                if (g != null && !launchedOld)
                {
                    FromProduct(g, p);
                    continue;
                }
                FirmPlace firm = FirmOf(p.Place);
                int appeal = firm != null ? FirmsRules.Appeal(p, firm.File, firm.Crew) : 500;
                int reach = FirmsCatalogue.MarketingReach[TalentCatalogue.Clamp(p.Marketing, 0, FirmsCatalogue.MarketingReach.Length - 1)];
                double daily = p.Stage == 1 ? StudioRules.OldDaily(appeal, p.Hype, reach, FirmsRules.CycleSpend[TalentCatalogue.Clamp(cycle, 0, 3)], day - p.Launched) : 0;
                GameDev made = StudioRules.Convert(p, FirmsRules.Quality(p), (long)daily, _studio.Seed);
                if (g != null)
                {
                    _studio.Games.Remove(g);
                }
                if (_studio.Games.Count < StudioCatalogue.GamesStored)
                {
                    _studio.Games.Add(made);
                    taken++;
                }
            }
            for (int i = _studio.Games.Count - 1; i >= 0; i--)
            {
                if (_firmsFile.ProductOf(_studio.Games[i].Id) == null)
                {
                    _studio.Games.RemoveAt(i);
                }
            }
            if (!_studio.Converted)
            {
                _studio.Converted = true;
                if (taken > 0)
                {
                    Logger?.Info("Tech: the game studio took this save's " + taken + " game(s) into the new model, each from where it stood: nothing jumps.");
                }
            }
            return taken;
        }

        // What the old rules did to a game while the studio was off: copies, takings and a withdrawal kept; the build's progress at least as far.
        private static void FromProduct(GameDev g, Product p)
        {
            if (p.Stage >= 2 && g.Phase != (int)GamePhase.Ended)
            {
                g.Phase = (int)GamePhase.Ended;
            }
            g.Sold = Math.Max(g.Sold, p.Users);
            g.Net = Math.Max(g.Net, p.Revenue);
            if (g.Building)
            {
                long done = (long)Math.Max(0, p.Progress) * g.Work / Math.Max(1, FirmsCatalogue.GameScope[TalentCatalogue.Clamp(p.Scope, 0, 2)]);
                g.Done = Math.Min(g.Work, Math.Max(g.Done, done));
            }
        }

        // The firms' list says of each game what the old rules need to carry on, should the studio be switched off.
        private void SyncProducts()
        {
            for (int i = 0; _studio != null && _firmsFile != null && i < _studio.Games.Count; i++)
            {
                GameDev g = _studio.Games[i];
                Product p = _firmsFile.ProductOf(g.Id);
                if (p == null)
                {
                    continue;
                }
                p.Stage = g.Phase == (int)GamePhase.Ended ? 2 : g.OnSale ? 1 : 0;
                p.Users = g.Sold;
                p.Revenue = g.Net;
                p.Scope = g.Scope;
                p.Bugs = (int)Math.Min(int.MaxValue, (long)g.Hidden + g.Found + g.Reported);
                p.Hype = Math.Min(2000, g.Hype * 2);
                p.Marketing = TalentCatalogue.Clamp(g.Campaign, 0, FirmsCatalogue.MarketingDay.Length - 1);
                int size = FirmsCatalogue.GameScope[TalentCatalogue.Clamp(g.Scope, 0, 2)];
                p.Progress = (int)Math.Min(size, g.Done * size / Math.Max(1L, g.Work));
                if (g.Launched >= 0)
                {
                    p.Launched = g.Launched;
                }
            }
        }

        // ---------------------------------------------------------------- the market's day

        /// <summary>
        /// Once a day before the studios: games made since by the old buttons taken in, the rival studios' releases (on the
        /// host or alone; a guest follows the host's), their news and stock, the genres' appeal, and the year's award.
        /// </summary>
        private void StudioMarketDay(int day)
        {
            _studio.YearDays = GameCalendar.DaysPerYear;   // the market's years are the save's (the one clock); a guest's calendar is the session's
            CatchUpStudio(day);
            bool following = SharedCity.Follows(StudioShare.Part);
            if (!following)
            {
                int n = StudioRules.RivalDay(_studio, day, _released);
                for (int i = 0; i < n; i++)
                {
                    RivalReleased(_released[i]);
                }
                if (n > 0)
                {
                    SharedCity.Changed(StudioShare.Part);
                }
            }
            StudioRules.GenreAppeals(_studio, day, _genreAppeal);
            int won = StudioRules.Award(_studio, day, GameCalendar.DaysPerYear, !following, out bool rival);
            if (won >= 0)
            {
                Awarded(won, rival);
            }
        }

        private void RivalReleased(RivalGame r)
        {
            int stock = StudioCatalogue.RivalStock[r.Studio];
            if (stock > 0)
            {
                TechBridges.Stock?.Invoke(stock, (r.Score - 60) * StudioCatalogue.RivalStockPerPoint);
            }
            if (r.Score >= StudioCatalogue.HitScore || r.Scope == (int)Scope.Big)
            {
                Story(r.Score >= StudioCatalogue.HitScore ? "reallife_studio_news_rival_hit" : "reallife_studio_news_rival", "studio", StudioCatalogue.RivalNames[r.Studio], "game", r.Name);
            }
            if (Verbose)
            {
                Logger?.Info("Tech check (studio rival): " + StudioCatalogue.RivalNames[r.Studio] + " released " + r.Name + " (" + (Genre)r.Genre + ", score " + r.Score + ", "
                             + r.Units.ToString(Inv) + " copies over its life) on day " + r.Launched + ", next on day " + _studio.RivalNext[r.Studio] + ": PASS");
            }
        }

        private void Awarded(int id, bool rival)
        {
            if (rival)
            {
                for (int i = 0; i < _studio.Rivals.Count; i++)
                {
                    if (_studio.Rivals[i].Id == id)
                    {
                        Story("reallife_studio_news_award_rival", "studio", StudioCatalogue.RivalNames[_studio.Rivals[i].Studio], "game", _studio.Rivals[i].Name);
                    }
                }
                return;
            }
            GameDev g = _studio.GameOf(id);
            Product p = _firmsFile.ProductOf(id);
            FirmPlace firm = g != null ? FirmOf(g.Place) : null;
            if (g == null || p == null)
            {
                return;
            }
            if (firm != null)
            {
                firm.File.Reputation = Math.Min(1000, firm.File.Reputation + StudioCatalogue.AwardReputation);
            }
            TechBridges.Fame?.Invoke(0, 2);
            Story("reallife_studio_news_award", "game", p.Name, "firm", firm != null ? firm.File.Name : string.Empty);
            StudioBridges.Say(StudioNews.Award, g.Place, p.Name, g.Score);
            TellTech(NotificationType.Success, "reallife_studio_note_award", Data("game", p.Name), TabProducts, TechApp.ProductRow + id);
        }

        // ---------------------------------------------------------------- a studio's day

        // The people at a studio who build its games, with their day's points (as the firms' crew counts them) and their craft's inputs.
        private int StudioHands(FirmPlace firm, int day)
        {
            int n = 0;
            TechBridges.TeamWeekAt(firm.File.Place, day, out int week, out _);
            for (int i = 0; i < _people.Count; i++)
            {
                Person person = _people[i];
                Profile p = person.File;
                RoleDef r = p.Place == firm.File.Place ? p.Def : null;
                int h = person.HoursYesterday;
                if (r == null || h <= 0 || r.Id == TalentCatalogue.Technician
                    || !(r.Family <= Family.Infrastructure || r.Family == Family.Design || r.Family == Family.Product))
                {
                    continue;
                }
                int points = MeetingRules.Points(TalentRules.Output(p, person.MainPoints, day) * FirmsCatalogue.PointsPerDay * h / TalentCatalogue.DayHours / 1000, week);
                if (r.Family != Family.Product)
                {
                    points = points * firm.Spare / 1000 * firm.Net / 1000;   // as the firms' crew: the office's spare hours and its connection
                }
                if (n >= _hands.Length)
                {
                    Array.Resize(ref _hands, _hands.Length * 2);
                }
                _hands[n++] = new StudioHand
                {
                    Id = p.Id, Family = (int)r.Family, Tester = r.Id == TalentCatalogue.QaTester, Points = points, Skill = (person.MainPoints * 1000 + p.A + p.B) / 3 / 1000,
                    Level = p.Level, Hours = h
                };
            }
            return n;
        }

        private StudioCrew CrewFor(int hands, int genre, Crew firmCrew, int engineCut)
        {
            int g = StudioRules.Clamp(genre, 0, (int)Genre.Count - 1);
            if (!_crewRead[g])
            {
                StudioCrew c = StudioRules.CrewOf(_hands, hands, _studio, g, firmCrew.Faults, firmCrew.Support);
                c.Engineering = c.Engineering * (1000 - engineCut) / 1000;
                _crewOf[g] = c;
                _crewRead[g] = true;
            }
            return _crewOf[g];
        }

        /// <summary>
        /// A game studio's day under the studio: the engine it builds, each game built, launched, sold and looked after with
        /// its share of the studio's points, the money into the firm's day (takings at today's prices into its day's total,
        /// servers among its hosting, the campaign among its marketing), bonuses and royalties booked, news told.
        /// </summary>
        private void StudioFirm(FirmPlace firm, int day, int cycle, ref long hosting, ref long marketing, ref long rndPoints, ref int quality, ref int rated)
        {
            Firm file = firm.File;
            int level = Prices.Level, dated = day - 1, year = GameCalendar.DaysPerYear;
            int hands = StudioHands(firm, day);
            Array.Clear(_crewRead, 0, _crewRead.Length);
            int shares = 0;
            bool building = false;
            for (int i = 0; i < _firmsFile.Products.Count; i++)
            {
                Product p = _firmsFile.Products[i];
                GameDev g = p.Place == file.Place && p.Stage < 2 ? _studio.GameOf(p.Id) : null;
                if (g != null)
                {
                    shares += Math.Max(0, p.Share);
                    building |= g.Building;
                }
            }
            long points = 0;
            for (int i = 0; i < hands; i++)
            {
                points += _hands[i].Points;
            }
            _studioPoints[file.Place] = points;
            int engineCut = EngineDay(file.Place, hands, building);
            int spend = FirmsRules.CycleSpend[TalentCatalogue.Clamp(cycle, 0, 3)], season = StudioRules.Season(day, year);
            long wages = firm.Crew.Wages * 1000 / Math.Max(1, level);   // the builders' day of pay at the start's prices
            long tickets = 0;
            for (int i = 0; i < _firmsFile.Products.Count; i++)
            {
                Product p = _firmsFile.Products[i];
                GameDev g = p.Place == file.Place ? _studio.GameOf(p.Id) : null;
                if (g == null || g.Phase == (int)GamePhase.Ended)
                {
                    continue;
                }
                int share = shares > 0 ? (int)((long)Math.Max(0, p.Share) * 1000 / shares) : 0;
                StudioCrew crew = CrewFor(hands, g.Genre, firm.Crew, engineCut);
                long built = 0;
                if (g.Building)
                {
                    built = StudioRules.Build(g, crew, share, day);
                    Learn(file.Place, g.Genre, hands, share);
                    StudioPhase(firm, g, p, StudioRules.Advance(g, day), day);
                }
                if (StudioRules.LaunchesToday(g, day))
                {
                    StudioRelease(firm, g, p, day);
                }
                if (g.Launched >= 0 && g.Score >= 0 && g.Reviews.Count == 0)
                {
                    StudioRules.WriteReviews(g, _studio.Seed, day);   // a game launched before the outlets existed is reviewed on its next day, by the parts it has now
                }
                if (g.OnSale)
                {
                    GameSales r = StudioRules.SellDay(g, _studio, day, _genreAppeal[g.Genre], _platforms, spend, season);
                    firm.Today += r.Payout * level / 1000;
                    hosting += r.Servers * level / 1000;
                    g.Spent += r.Servers;
                    StudioRules.ReportDay(g);
                    int released = StudioRules.AfterLaunch(g, crew, share, day);
                    if (released != StudioRules.ProjectNone)
                    {
                        ProjectReleased(firm, g, p, released, day);
                    }
                    quality += g.Score >= 0 ? g.Score * 10 : StudioRules.Quality(g) * 10;
                    rated++;
                    tickets += StudioRules.Tickets(g);
                }
                if (share > 0 && (g.Building || g.Project != StudioRules.ProjectNone || g.Reported > 0))
                {
                    g.Spent += wages * share / 1000;   // the team's time on it
                    built += g.Building ? 0 : (long)crew.Engineering * share / 1000;   // patches and projects are research and development too
                }
                rndPoints += built;
                long campaign = StudioRules.HypeDay(g, day);
                marketing += campaign;
                g.Spent += campaign;
                if (day < g.BoostUntil && g.Building)
                {
                    long extra = points * share / 1000 * StudioCatalogue.BoostOutput / 1000;
                    long cost = extra * StudioCatalogue.PointCents;
                    FirmsGame.Cost(cost * level / 1000, StudioGame.ContractorsKey, firm.Reg, file.Name, dated);
                    g.Spent += cost;
                }
                if (day < g.CrunchUntil && g.Building)
                {
                    Crunched(file.Place, day);
                }
                StudioMoney(firm, g, p, day, dated);
            }
            // the players' tickets: what the support people leave unanswered costs the studio's name
            _studioTickets[file.Place] = tickets;
            file.Reputation = Math.Max(0, file.Reputation - StudioRules.TicketToll(tickets, (long)firm.Crew.Support * FirmsCatalogue.TicketsPerPerson));
            SyncProducts();
        }

        // A studio's own engine takes its share of the engineering while a game is built (all of it while none is); done, it is ready for new games.
        private int EngineDay(string place, int hands, bool building)
        {
            StudioEngine e = _studio.EngineAt(place);
            if (e == null || e.Ready || !e.Building)
            {
                return 0;
            }
            long engineering = 0;
            for (int i = 0; i < hands; i++)
            {
                engineering += _hands[i].Family <= (int)Family.Infrastructure && !_hands[i].Tester ? _hands[i].Points : 0;
            }
            int cut = building ? StudioCatalogue.EngineShare : 1000;
            e.Done += engineering * cut / 1000;
            if (e.Done >= ScaleCatalogue.OwnEngineCost / StudioCatalogue.PointCents)
            {
                e.Ready = true;
                e.Building = false;
                FirmPlace firm = FirmOf(place);
                TellTech(NotificationType.Success, "reallife_studio_note_engine", Data("firm", firm != null ? firm.File.Name : string.Empty), TabFirms, TechApp.FirmRow + place);
            }
            return cut;
        }

        // A day of a game for the people who worked it: person-days in its genre by its share of their time.
        private void Learn(string place, int genre, int hands, int share)
        {
            for (int i = 0; i < hands; i++)
            {
                if (_hands[i].Points > 0)
                {
                    StudioRules.Learn(_studio, _hands[i].Id, genre, _hands[i].Hours * share / 1000);
                }
            }
        }

        // A crunch's day: burnout for each professional at the studio who worked, through the people part's own rules from then on.
        private void Crunched(string place, int day)
        {
            for (int i = 0; i < _people.Count; i++)
            {
                Profile p = _people[i].File;
                if (p.Place == place && _people[i].HoursYesterday > 0)
                {
                    p.Burnout = MeetingRules.Day(p.Burnout, _people[i].HoursYesterday, StudioCatalogue.CrunchBurnout);
                }
            }
        }

        // A milestone reached, a game finished, or a milestone that waited too long held by the team.
        private void StudioPhase(FirmPlace firm, GameDev g, Product p, int advanced, int day)
        {
            string row = TechApp.ProductRow + g.Id;
            if (advanced == 1)
            {
                TellTech(NotificationType.Info, "reallife_studio_note_milestone",
                    Data("game", p.Name, "studio", firm.File.Name, "days", StudioCatalogue.MilestoneGraceDays.ToString(Inv)), TabProducts, row);
            }
            else if (advanced == 2)
            {
                TellTech(NotificationType.Success, g.On(Platform.Console) ? "reallife_studio_note_gold_console" : "reallife_studio_note_gold",
                    Data("game", p.Name, "days", StudioCatalogue.CertificationDays.ToString(Inv)), TabProducts, row);
                if (g.On(Platform.Console))
                {
                    FirmsGame.Cost(StudioCatalogue.CertificationCost[TalentCatalogue.Clamp(g.Scope, 0, 2)] * Prices.Level / 1000, StudioGame.CertificationKey, firm.Reg, firm.File.Name, null);
                    g.Spent += StudioCatalogue.CertificationCost[TalentCatalogue.Clamp(g.Scope, 0, 2)];
                }
            }
            if (StudioRules.Lapsed(g, day))
            {
                StudioRules.ApplyMilestone(g, StudioRules.Hold, StudioRules.Normal, day);
                TellTech(NotificationType.Info, "reallife_studio_note_milestone_held", Data("game", p.Name), TabProducts, row);
            }
        }

        private void StudioRelease(FirmPlace firm, GameDev g, Product p, int day)
        {
            bool early = g.Early;
            GameDev prior = g.Sequel > 0 ? _studio.GameOf(g.Sequel) : null;
            StudioRules.Release(g, _studio, day, firm.File.Reputation, _studio.Seed, _genreAppeal[g.Genre], _platforms, StudioRules.FansOf(prior));
            g.Fans = StudioRules.FansOf(g);
            p.Stage = 1;
            p.Launched = g.Launched;
            string row = TechApp.ProductRow + g.Id;
            if (!early)
            {
                TechBridges.Fame?.Invoke(0, g.Score >= 80 ? 2 : 1);
                Story("reallife_tech_news_launch", "product", p.Name, "firm", firm.File.Name);
                StudioBridges.Say(StudioNews.Launched, g.Place, p.Name, g.Score);
            }
            Story(g.Score >= StudioCatalogue.AcclaimScore ? "reallife_studio_news_acclaim" : g.Score <= StudioCatalogue.FlopScore ? "reallife_studio_news_panned" : "reallife_studio_news_review",
                "game", p.Name, "score", g.Score.ToString(Inv));
            StudioBridges.Say(StudioNews.Reviewed, g.Place, p.Name, g.Score);
            int own = TechBridges.OwnStockNow();
            if (own > 0)
            {
                TechBridges.Stock?.Invoke(own, (g.Score - 60) * StudioCatalogue.OwnStockPerPoint);
            }
            if (g.Score <= StudioCatalogue.FlopScore)
            {
                TechBridges.Fame?.Invoke(0, -1);
            }
            if (g.Score >= StudioCatalogue.HitScore)
            {
                _studio.TrendGenre = g.Genre;   // a hit of the player's starts a trend too: the same rule as the rivals'
                _studio.TrendUntil = day + StudioCatalogue.TrendDays;
            }
            if (prior != null && StudioRules.Burns(g, prior))
            {
                prior.Burnt = true;
                g.Fans /= 2;
                Story("reallife_studio_news_burnt", "game", p.Name, "firm", firm.File.Name);
            }
            TellTech(g.Score <= StudioCatalogue.FlopScore ? NotificationType.Warning : NotificationType.Success, early ? "reallife_studio_note_full_release" : "reallife_studio_note_reviewed",
                Data("game", p.Name, "score", g.Score.ToString(Inv)), TabProducts, row);
            if (Verbose)
            {
                Logger?.Info("Tech check (studio launch): " + p.Name + " at " + firm.File.Name + ", quality " + g.Quality + " against the bar "
                             + StudioRules.Expect(g.Genre, day, g.Sequel > 0, _studio.YearDays)
                             + ", hype " + g.Hype + ", bugs " + (g.Hidden + g.Found) + " of room " + StudioRules.BugRoom(g) + ": score " + g.Score + ", reach " + g.Reach
                             + ", window " + g.WindowUnits.ToString(Inv) + " copies: PASS");
            }
        }

        private void ProjectReleased(FirmPlace firm, GameDev g, Product p, int project, int day)
        {
            long more = StudioRules.Released(g, project, _studio, day, _genreAppeal[g.Genre], _platforms, _studio.Seed);
            if (project == StudioRules.ProjectUpdate)
            {
                firm.File.Reputation = Math.Min(1000, firm.File.Reputation + StudioCatalogue.UpdateReputation);
                StudioBridges.Say(StudioNews.Update, g.Place, p.Name, more);
            }
            else if (project == StudioRules.ProjectAddOn)
            {
                StudioBridges.Say(StudioNews.AddOn, g.Place, p.Name, g.AddOnWindow);
            }
            // a free update and an add-on have names of their own, made from the game's by rule: the one just released (Released has counted it)
            string called = StudioViews.ProjectTitle(g, p.Name, project, 0);
            TellTech(NotificationType.Success, called.Length > 0 ? NamedNotes[project] : ProjectNotes[StudioRules.Clamp(project, 0, ProjectNotes.Length - 1)],
                Data("game", p.Name, "name", called, "copies", TechText.Whole(project == StudioRules.ProjectAddOn ? g.AddOnWindow : more)), TabProducts, TechApp.ProductRow + g.Id);
        }

        // A release's bonus day and the leads' monthly royalty: booked at the firm, felt by the team, and a hit that paid nothing remembered by the rivals.
        private void StudioMoney(FirmPlace firm, GameDev g, Product p, int day, int dated)
        {
            int level = Prices.Level;
            if (StudioRules.BonusDue(g, day))
            {
                g.BonusDone = true;
                long pay = StudioRules.BonusPay(g);
                bool hit = StudioRules.Hit(g);
                if (pay > 0)
                {
                    FirmsGame.Cost(pay * level / 1000, StudioGame.BonusKey, firm.Reg, firm.File.Name, dated);
                    g.Spent += pay;
                    int lifted = Team(firm.File.Place, StudioCatalogue.BonusMorale, day, true);
                    TellTech(NotificationType.Success, "reallife_studio_note_bonus", Data("game", p.Name, "amount", TechText.Cents(pay * level / 1000), "people", lifted.ToString(Inv)),
                        TabProducts, TechApp.ProductRow + g.Id);
                }
                else if (hit)
                {
                    _studio.SnubUntil[firm.File.Place] = day + StudioCatalogue.SnubDays;
                    Team(firm.File.Place, -StudioCatalogue.SnubMorale, day, false);
                    TellTech(NotificationType.Warning, "reallife_studio_note_snub", Data("game", p.Name, "studio", firm.File.Name), TabProducts, TechApp.ProductRow + g.Id);
                }
            }
            long leads = StudioRules.LeadsPay(g, day);
            if (leads > 0)
            {
                FirmsGame.Cost(leads * level / 1000, StudioGame.RoyaltyKey, firm.Reg, firm.File.Name, dated);
                g.Spent += leads;
                for (int i = 0; i < _people.Count; i++)
                {
                    Profile who = _people[i].File;
                    if (who.Place == firm.File.Place && who.Level >= TalentCatalogue.Senior && who.NoticeEnd == 0)
                    {
                        who.Morale = TalentCatalogue.Clamp(who.Morale + StudioCatalogue.BonusMorale / 4, 0, 1000);
                    }
                }
            }
        }

        // Morale moved for a studio's people with no notice running; recognised (a bonus) counts as a rise for their mood. Returns how many.
        private int Team(string place, int morale, int day, bool recognised)
        {
            int n = 0;
            for (int i = 0; i < _people.Count; i++)
            {
                Profile p = _people[i].File;
                if (p.Place != place || p.NoticeEnd != 0)
                {
                    continue;
                }
                p.Morale = TalentCatalogue.Clamp(p.Morale + morale, 0, 1000);
                if (recognised)
                {
                    p.RecognisedDay = Math.Max(p.RecognisedDay, day);
                    p.PoachDay = Math.Max(p.PoachDay, day + TalentCatalogue.PoachRestDays);   // paid people are not open to an approach for a while
                }
                n++;
            }
            return n;
        }

        /// <summary>For the save tools: the studio's line is forgotten; the timer starts it again from the firms' games.</summary>
        public void ForgetStudio()
        {
            try
            {
                StopStudio(false);
                _studio = null;
                Save?.modData?.Remove(StudioStore.Key);
            }
            catch (Exception e)
            {
                Fault("the save tools", e);
            }
        }

        /// <summary>A rival's pull on a professional's place (thousandths for TalentRules.Approached): strong at a studio whose hit paid no bonus.</summary>
        internal int StudioPull(string place, int day) => _studioOn ? StudioRules.Pull(_studio, place, day) : 1000;

        /// <summary>The rival studio that approaches a game studio's people, or null: the one with the newest hit (StudioRules.Poacher).</summary>
        internal string StudioPoacher(string place, int day)
        {
            FirmPlace firm = _studioOn ? FirmOf(place) : null;
            if (firm == null || firm.File.K != FirmKind.Games)
            {
                return null;
            }
            int s = StudioRules.Poacher(_studio, day);
            return s >= 0 ? StudioCatalogue.RivalNames[s] : null;
        }

        /// <summary>Games at milestones and games ready to launch: what the Tech app's badge counts for the studio.</summary>
        internal int StudioWaiting()
        {
            int n = 0;
            for (int i = 0; _studioOn && i < _studio.Games.Count; i++)
            {
                GameDev g = _studio.Games[i];
                n += g.Milestone >= 0 || (Save != null && StudioRules.CanLaunch(g, Save.Day)) ? 1 : 0;
            }
            return n;
        }

        /// <summary>A studio's support tickets a day, as its last day gave them.</summary>
        internal long StudioTicketsAt(string place) => place != null && _studioTickets.TryGetValue(place, out long t) ? t : 0;

        /// <summary>A game's quality as the studio has it, 0 to 100, or -1 for a game it does not run (the Products table's column).</summary>
        internal int StudioQualityOf(int id)
        {
            GameDev g = _studioOn ? _studio.GameOf(id) : null;
            return g == null ? -1 : g.Score >= 0 ? g.Quality : StudioRules.Quality(g);
        }

        /// <summary>The studio's points a day for its games, as its last day gave them; 0 before its first day since loading.</summary>
        internal long StudioPointsAt(string place) => place != null && _studioPoints.TryGetValue(place, out long p) ? p : 0;
    }
}
