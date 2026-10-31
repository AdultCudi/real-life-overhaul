using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>A line's or a chip's weight on a screen: plain, good news, a warning, bad news.</summary>
    public enum ViewTone
    {
        Plain = 0, Good = 1, Warn = 2, Bad = 3
    }

    /// <summary>How a screen's figures and names are written: the game's money, numbers, days and texts in the game; plain ones offline.</summary>
    public interface IStudioFormat
    {
        string Money(long cents);        // cents at the start's prices, written at today's
        string Whole(long n);
        string Mille(int thousandths);   // a share as a per cent
        string Day(int day);
        string Text(string key);
    }

    /// <summary>One line of a screen: a text's key with its names and values already written, and its weight.</summary>
    public sealed class ViewLine
    {
        public string Key;
        public string[] Pairs;
        public ViewTone Tone;
    }

    /// <summary>A button of a screen: its text's key, what it asks the director to do, whether it can be pressed now and why not.</summary>
    public sealed class ViewAction
    {
        public string Key;
        public int Act, Arg;
        public bool Enabled = true;
        public string WhyKey;            // said in a line when it cannot be pressed
        public bool Danger;              // the game's red button, asked to confirm
    }

    /// <summary>A choice of a screen: its label, its options as written, the one picked, the trade-off of the pick, and a button that acts on it.</summary>
    public sealed class ViewChoice
    {
        public string LabelKey;
        public int Act;
        public int Picked;
        public bool Slider;              // a slider of steps (a budget, a price), else a selector
        public readonly List<string> Options = new List<string>(16);
        public readonly List<int> Values = new List<int>(6);   // what each option sends (a day); empty: the option's index
        public ViewLine Trade;           // what the pick gives and costs
        public ViewAction Go;            // a button for the pick (a marketing beat, a project), or null
    }

    /// <summary>A section of a screen: a title, a few lines and choices.</summary>
    public sealed class ViewSection
    {
        public string TitleKey;
        public readonly List<ViewLine> Lines = new List<ViewLine>(6);
        public readonly List<ViewChoice> Choices = new List<ViewChoice>(4);
    }

    /// <summary>
    /// A screen of the game studio as data, apart from how it is drawn (the owner, 1 Nov 2026: "It must look absolutely
    /// beautiful at the same time, not cluttered"): a title, a chip, what to do next (always said: players were lost in the
    /// tech sector), a few sections, one primary action and the rest. The Tech app draws it today; the mod's own design
    /// system (Scripts/Ui) will draw the same data. Game-free: EconomyCheck's StudioChecks build every screen offline.
    /// </summary>
    public sealed class StudioScreen
    {
        public int Kind;                 // StudioViews.ScreenConcept, ScreenGame
        public string TitleKey;
        public string[] TitlePairs;
        public string Subtitle;          // written already: a genre, a setting, platforms
        public string ChipKey;
        public ViewTone ChipTone;
        public ViewLine Next;            // what to do next
        public readonly List<ViewSection> Sections = new List<ViewSection>(5);
        public ViewAction Primary;       // the one primary action, or null
        public readonly List<ViewAction> Others = new List<ViewAction>(3);
        public string EmptyKey;          // words for a screen with nothing to show

        public ViewSection Section(string titleKey)
        {
            var s = new ViewSection { TitleKey = titleKey };
            Sections.Add(s);
            return s;
        }
    }

    /// <summary>What a screen's choice or button asks the director to do (TechDirector.Studio's Act).</summary>
    public static class StudioAct
    {
        public const int Genre = 1, Setting = 2, Platform = 3, Model = 4, Engine = 5, Sequel = 6, Budget = 7, Price = 8, Desk = 9, Beat = 10, BeatPick = 11, Campaign = 12,
            Release = 13, Bonus = 14, Leads = 15, Share = 16, LaunchNow = 17, OpenEarly = 18, Project = 19, ProjectPick = 20, Withdraw = 21, BuildEngine = 22, Concept = 23, Close = 24,
            Pitch = 25, Greenlight = 26;
    }

    /// <summary>What the screens know of a studio from the game, filled by the director when a page is drawn.</summary>
    public sealed class StudioPlace
    {
        public string Place = string.Empty, Name = string.Empty;
        public long PointsADay;          // the studio's work points a day for its games, as the last day's crew gave them
        public int People;
        public bool OwnEngine;           // its own engine is built
        public bool Room = true;         // it may start another product (FirmsRules.Room)
        public int Reputation = 500;
        public bool Desks = true;        // desk sessions run: the pitch is held at the desk; off, the concept is greenlit on its page
        public long Tickets, Answered;   // support tickets a day from its games' players, and those its support people answer
    }

    /// <summary>The game studio's screens built from its state: the concept, a game's page, and the studio's own part of its firm's page. Game-free.</summary>
    public static class StudioViews
    {
        public const int ScreenConcept = 0, ScreenGame = 1, ScreenStudio = 2;
        public static readonly int[] ReleaseSteps = { 15, 30, 45, 60 };   // days from today a release date can be set to, beside as soon as ready
        private const int CrowdDays = 60;   // days either side of a day the release page counts rival games of the genre in
        private const string Mod = "reallife", P = Mod + "_studio_";   // the keys' start, written so the source holds no key prefix as a literal

        public static string GenreKey(int genre) => P + "genre_" + ((Genre)StudioRules.Clamp(genre, 0, (int)Genre.Count - 1)).ToString().ToLowerInvariant();

        public static string SettingKey(int setting) => P + "setting_" + StudioCatalogue.SettingKeys[StudioRules.Clamp(setting, 0, StudioCatalogue.Settings - 1)];

        public static string PlatformKey(int platform) => P + "platform_" + ((Platform)StudioRules.Clamp(platform, 0, StudioCatalogue.GamePlatforms - 1)).ToString().ToLowerInvariant();

        public static string ModelKey(int model) => P + "model_" + ((GameModel)StudioRules.Clamp(model, 0, (int)GameModel.Count - 1)).ToString().ToLowerInvariant();

        public static string PhaseKey(int phase) => P + "phase_" + ((GamePhase)StudioRules.Clamp(phase, 0, (int)GamePhase.Ended)).ToString().ToLowerInvariant();

        public static string PartKey(int part) => P + "part_" + ((Part)StudioRules.Clamp(part, 0, (int)Part.Count - 1)).ToString().ToLowerInvariant();

        public static string AskKey(int ask) => P + "ask_" + ((FanAsk)StudioRules.Clamp(ask, 0, (int)FanAsk.Count - 1)).ToString().ToLowerInvariant();

        public static string ScopeKey(int scope) => Mod + "_tech_scope_" + StudioRules.Clamp(scope, 0, 2);

        /// <summary>The platforms a game is sold on, written: "PC and console".</summary>
        public static string Platforms(int bits, IStudioFormat fmt)
        {
            string words = string.Empty;
            for (int p = 0; p < StudioCatalogue.GamePlatforms; p++)
            {
                if ((bits & (1 << p)) != 0)
                {
                    words = words.Length == 0 ? fmt.Text(PlatformKey(p)) : words + ", " + fmt.Text(PlatformKey(p));
                }
            }
            return words;
        }

        private static ViewLine Line(ViewSection s, string key, ViewTone tone, params string[] pairs)
        {
            var l = new ViewLine { Key = key, Pairs = pairs, Tone = tone };
            s?.Lines.Add(l);
            return l;
        }

        private static ViewLine Words(string key, ViewTone tone, params string[] pairs) => new ViewLine { Key = key, Pairs = pairs, Tone = tone };

        private static ViewChoice Choice(ViewSection s, string labelKey, int act, int picked, bool slider)
        {
            var c = new ViewChoice { LabelKey = labelKey, Act = act, Picked = picked, Slider = slider };
            s.Choices.Add(c);
            return c;
        }

        // ---------------------------------------------------------------- the concept

        /// <summary>
        /// The concept screen: the game (genre, setting, platform, model, engine, a sequel), its budget and price with the
        /// market's usual price, and what they mean (the audience, the critics' bar, rival games, the hot genre, the work at
        /// the team's pace). The one primary action sends the player to the desk, where the pitch makes it a game.
        /// </summary>
        public static StudioScreen Concept(StudioFile f, GameDraft d, StudioPlace s, int day, IStudioFormat fmt)
        {
            var v = new StudioScreen { Kind = ScreenConcept, TitleKey = P + "concept_title", TitlePairs = new[] { "studio", s.Name }, ChipKey = P + "chip_draft" };
            long budget = StudioRules.BudgetAt(d.Budget);
            int scope = StudioRules.ScopeOf(budget), genre = StudioRules.Clamp(d.Genre, 0, (int)Genre.Count - 1), platform = StudioRules.Clamp(d.Platform, 0, StudioCatalogue.GamePlatforms - 1);
            ViewSection game = v.Section(P + "sec_concept");
            ViewChoice c = Choice(game, P + "label_genre", StudioAct.Genre, genre, false);
            for (int g = 0; g < (int)Genre.Count; g++)
            {
                c.Options.Add(fmt.Text(GenreKey(g)));
            }
            c.Trade = Words(P + "genre_trade", ViewTone.Plain, "cares", Cares(genre, fmt), "usual", fmt.Text(ScopeKey(StudioCatalogue.UsualScope[genre])),
                "online", StudioCatalogue.Online[genre] ? fmt.Text(P + "genre_online") : string.Empty);
            c = Choice(game, P + "label_setting", StudioAct.Setting, StudioRules.Clamp(d.Setting, 0, 3), false);
            for (int slot = 0; slot < 4; slot++)
            {
                c.Options.Add(fmt.Text(SettingKey(StudioRules.SettingOf(genre, slot))));
            }
            c.Trade = SettingLine(f, s.Place, genre, StudioRules.Clamp(d.Setting, 0, 3));
            c = Choice(game, P + "label_platform", StudioAct.Platform, platform, false);
            for (int p = 0; p < StudioCatalogue.GamePlatforms; p++)
            {
                c.Options.Add(fmt.Text(PlatformKey(p)));
            }
            long people = StudioBridges.Audience(genre, platform, day, out long residents);
            c.Trade = Words(platform == (int)Tech.Platform.Console ? P + "platform_trade_console" : platform == (int)Tech.Platform.Mobile ? P + "platform_trade_mobile" : P + "platform_trade_pc",
                ViewTone.Plain, "people", fmt.Whole(people), "residents", fmt.Whole(residents), "genre", fmt.Text(GenreKey(genre)), "days", fmt.Whole(StudioCatalogue.CertificationDays));
            c = Choice(game, P + "label_model", StudioAct.Model, StudioRules.Clamp(d.Model, 0, (int)GameModel.Count - 1), false);
            for (int m = 0; m < (int)GameModel.Count; m++)
            {
                c.Options.Add(fmt.Text(ModelKey(m)));
            }
            c.Trade = Words(ModelKey(d.Model) + "_trade", ViewTone.Plain, "share", fmt.Mille(StudioCatalogue.EarlyFrom));
            c = Choice(game, P + "label_engine", StudioAct.Engine, StudioRules.Clamp(d.Engine, 0, 1), false);
            c.Options.Add(fmt.Text(P + "engine_licensed"));
            c.Options.Add(fmt.Text(s.OwnEngine ? P + "engine_own" : P + "engine_own_none"));
            c.Trade = d.Engine == (int)GameEngine.Own
                ? Words(s.OwnEngine ? P + "engine_own_trade" : P + "engine_own_missing", s.OwnEngine ? ViewTone.Plain : ViewTone.Warn)
                : Words(P + "engine_licensed_trade", ViewTone.Plain, "fee", fmt.Money(StudioCatalogue.EngineFee[scope]), "royalty", fmt.Mille(ScaleCatalogue.EngineRoyalty),
                    "free", fmt.Money(ScaleCatalogue.EngineRoyaltyFree));
            SequelChoice(game, f, d, s, fmt);
            ViewSection money = v.Section(P + "sec_budget");
            c = Choice(money, P + "label_budget", StudioAct.Budget, StudioRules.Clamp(d.Budget, 0, StudioCatalogue.Budgets.Length - 1), true);
            for (int b = 0; b < StudioCatalogue.Budgets.Length; b++)
            {
                c.Options.Add(fmt.Money(StudioCatalogue.Budgets[b]) + ", " + fmt.Text(ScopeKey(StudioRules.ScopeOf(StudioCatalogue.Budgets[b]))));
            }
            long work = StudioRules.WorkFor(budget, StudioRules.AsPlanned, d.Engine == (int)GameEngine.Own && s.OwnEngine);
            int days = StudioRules.DaysLeft(work, s.PointsADay);
            c.Trade = days > 0
                ? Words(P + "budget_trade", ViewTone.Plain, "points", fmt.Whole(work), "days", fmt.Whole(days), "usual", fmt.Money(ScaleCatalogue.BudgetTypical[StudioCatalogue.UsualScope[genre]]))
                : Words(P + "budget_trade_nobody", ViewTone.Warn, "points", fmt.Whole(work), "usual", fmt.Money(ScaleCatalogue.BudgetTypical[StudioCatalogue.UsualScope[genre]]));
            c = Choice(money, P + "label_price", StudioAct.Price, StudioRules.Clamp(d.Price, 0, StudioCatalogue.PriceSteps.Length - 1), true);
            for (int i = 0; i < StudioCatalogue.PriceSteps.Length; i++)
            {
                c.Options.Add(d.Model == (int)GameModel.FreeToPlay ? fmt.Text(P + "price_free") : fmt.Money(StudioRules.PriceShown(d.Model, i, scope, platform)));
            }
            c.Trade = d.Model == (int)GameModel.FreeToPlay
                ? Words(P + "price_trade_free", ViewTone.Plain, "spend", fmt.Money(StudioRules.UsualPrice(scope, platform) * StudioCatalogue.F2PSpend / 1000))
                : Words(d.Model == (int)GameModel.Subscription ? P + "price_trade_month" : P + "price_trade", ViewTone.Plain,
                    "usual", fmt.Money(StudioRules.PriceShown(d.Model, 2, scope, platform)));
            ViewSection outlook = v.Section(P + "sec_outlook");
            Line(outlook, P + "outlook_seen", ViewTone.Plain, "share", fmt.Mille(StudioRules.Visibility(budget)), "people", fmt.Whole(people * StudioRules.Visibility(budget) / 1000));
            Line(outlook, P + "outlook_bar", ViewTone.Plain, "genre", fmt.Text(GenreKey(genre)), "expect", fmt.Whole(StudioRules.Expect(genre, day, d.Sequel > 0)));
            int crowd = StudioRules.Crowd(f, genre, day, CrowdDays, null);
            Line(outlook, crowd > 0 ? P + "outlook_crowd" : P + "outlook_clear", crowd > 1 ? ViewTone.Warn : ViewTone.Plain, "count", fmt.Whole(crowd), "days", fmt.Whole(CrowdDays));
            if (f != null && f.TrendGenre >= 0 && day < f.TrendUntil)
            {
                Line(outlook, P + "outlook_trend", f.TrendGenre == genre ? ViewTone.Good : ViewTone.Plain, "genre", fmt.Text(GenreKey(f.TrendGenre)), "day", fmt.Day(f.TrendUntil));
            }
            if (StudioBridges.CanCrowdfund)
            {
                Line(outlook, P + "outlook_crowdfund", ViewTone.Plain);
            }
            v.Next = !s.Room ? Words(P + "next_full", ViewTone.Warn) : s.Desks ? Words(P + "next_pitch", ViewTone.Plain, "studio", s.Name) : Words(P + "next_greenlight", ViewTone.Plain);
            v.Primary = s.Desks
                ? new ViewAction { Key = P + "button_desk", Act = StudioAct.Pitch, Enabled = s.Room, WhyKey = s.Room ? null : P + "next_full" }
                : new ViewAction { Key = P + "button_greenlight", Act = StudioAct.Greenlight, Enabled = s.Room, WhyKey = s.Room ? null : P + "next_full" };
            v.Others.Add(new ViewAction { Key = "common_close", Act = StudioAct.Close });
            return v;
        }

        // What the genre's players care about most: its two heaviest parts.
        private static string Cares(int genre, IStudioFormat fmt)
        {
            int[] w = StudioCatalogue.PartWeights[genre];
            int first = 0, second = -1;
            for (int p = 1; p < (int)Part.Count; p++)
            {
                if (w[p] > w[first])
                {
                    second = first;
                    first = p;
                }
                else if (second < 0 || w[p] > w[second])
                {
                    second = p;
                }
            }
            return fmt.Text(PartKey(first)) + ", " + fmt.Text(PartKey(second));
        }

        // What the studio has learned of a setting in a genre: nothing until it has shipped a game in it.
        private static ViewLine SettingLine(StudioFile f, string place, int genre, int slot)
        {
            if (!StudioRules.Knows(f, place, genre, slot))
            {
                return Words(P + "setting_unknown", ViewTone.Plain);
            }
            int fit = StudioRules.Fit(genre, slot);
            return fit > 1000 ? Words(P + "setting_known_good", ViewTone.Good) : fit < 1000 ? Words(P + "setting_known_odd", ViewTone.Warn) : Words(P + "setting_known_fine", ViewTone.Plain);
        }

        // A sequel of one of the studio's released games: offered only when it has one.
        private static void SequelChoice(ViewSection s, StudioFile f, GameDraft d, StudioPlace place, IStudioFormat fmt)
        {
            if (f == null)
            {
                return;
            }
            ViewChoice c = null;
            int picked = 0, n = 0;
            for (int i = 0; i < f.Games.Count; i++)
            {
                GameDev g = f.Games[i];
                if (g.Place != place.Place || g.Score < 0)
                {
                    continue;
                }
                if (c == null)
                {
                    c = Choice(s, P + "label_sequel", StudioAct.Sequel, 0, false);
                    c.Options.Add(fmt.Text(P + "sequel_none"));
                }
                n++;
                c.Options.Add(fmt.Text(GenreKey(g.Genre)) + " " + fmt.Whole(g.Score));
                picked = g.Id == d.Sequel ? n : picked;
                if (g.Id == d.Sequel)
                {
                    c.Trade = Words(g.Burnt ? P + "sequel_trade_burnt" : P + "sequel_trade", g.Burnt ? ViewTone.Warn : ViewTone.Good, "fans", fmt.Whole(StudioRules.FansOf(g)),
                        "expect", fmt.Whole(StudioCatalogue.SequelExpect));
                }
            }
            if (c != null)
            {
                c.Picked = picked;
                c.Trade = c.Trade ?? Words(P + "sequel_trade_none", ViewTone.Plain);
            }
        }

        /// <summary>The ids of a studio's released games in the order the concept's sequel choice lists them, after "a new name".</summary>
        public static int SequelAt(StudioFile f, string place, int index)
        {
            int n = 0;
            for (int i = 0; f != null && i < f.Games.Count; i++)
            {
                GameDev g = f.Games[i];
                if (g.Place == place && g.Score >= 0 && ++n == index)
                {
                    return g.Id;
                }
            }
            return 0;
        }

        // ---------------------------------------------------------------- a game's page

        /// <summary>
        /// A game's page: where it stands (the chip), what to do next, then by its state the build (progress, the parts of
        /// its quality, bugs, hype, money spent), marketing and the release date, sales, life after launch, and the team's
        /// bonus and share. The one primary action is the step that moves it on: launch, open early access, or go to the desk.
        /// </summary>
        public static StudioScreen Game(StudioFile f, GameDev g, string name, StudioPlace s, int share, int ownShare, int day, int yearDays, int beatPick, int projectPick, IStudioFormat fmt)
        {
            var v = new StudioScreen { Kind = ScreenGame, TitleKey = P + "game_title", TitlePairs = new[] { "game", name } };
            v.Subtitle = fmt.Text(GenreKey(g.Genre)) + ", " + fmt.Text(SettingKey(StudioRules.SettingOf(g.Genre, g.Setting))) + ", " + Platforms(g.Platforms, fmt);
            v.ChipKey = g.Early && g.OnSale ? P + "chip_early" : PhaseKey(g.Phase);
            v.ChipTone = g.Phase == (int)GamePhase.Ready ? ViewTone.Good : g.Phase == (int)GamePhase.Ended ? ViewTone.Warn : ViewTone.Plain;
            v.Next = NextStep(g, s, day, out ViewAction primary);
            v.Primary = primary;
            if (g.Building || g.Phase == (int)GamePhase.Ready)
            {
                Build(v, g, s, share, day, fmt);
            }
            if (g.OnSale)
            {
                Sales(v, f, g, day, fmt);
            }
            if (g.Phase < (int)GamePhase.Ended && (g.Launched < 0 || g.Early))
            {
                Marketing(v, f, g, day, yearDays, beatPick, fmt);
            }
            if (g.Live)
            {
                After(v, g, projectPick, fmt);
            }
            Team(v, g, ownShare, fmt);
            if (g.Phase < (int)GamePhase.Ended)
            {
                v.Others.Add(new ViewAction { Key = "reallife_tech_button_withdraw", Act = StudioAct.Withdraw, Danger = true });
            }
            v.Others.Add(new ViewAction { Key = "common_close", Act = StudioAct.Close });
            return v;
        }

        /// <summary>What to do next with a game, always said, and the action that does it (or none: the team is on it, or it is over).</summary>
        public static ViewLine NextStep(GameDev g, StudioPlace s, int day, out ViewAction primary)
        {
            primary = null;
            if (g.Phase == (int)GamePhase.Ended)
            {
                return Words(P + "next_ended", ViewTone.Plain);
            }
            if (g.Milestone >= 0)
            {
                primary = new ViewAction { Key = P + "button_desk", Act = StudioAct.Desk };
                return Words(P + "next_milestone", ViewTone.Warn, "studio", s.Name, "days", Math.Max(0, StudioCatalogue.MilestoneGraceDays - (day - g.MilestoneDay)).ToString());
            }
            if (StudioRules.CanOpenEarly(g))
            {
                primary = new ViewAction { Key = P + "button_early", Act = StudioAct.OpenEarly };
                return Words(P + "next_early", ViewTone.Good);
            }
            if (g.Phase == (int)GamePhase.Ready)
            {
                if (day < g.CertUntil)
                {
                    return Words(P + "next_certification", ViewTone.Plain, "day", g.CertUntil.ToString());
                }
                primary = new ViewAction { Key = P + "button_launch", Act = StudioAct.LaunchNow };
                return g.ReleaseDay > day ? Words(P + "next_release_day", ViewTone.Good, "day", g.ReleaseDay.ToString()) : Words(P + "next_ready", ViewTone.Good);
            }
            if (g.Building)
            {
                if (s.PointsADay <= 0)
                {
                    return Words(P + "next_nobody", ViewTone.Bad, "studio", s.Name);
                }
                if (g.Found > 0 && g.Fix == 0)
                {
                    return Words(P + "next_shipping_bugs", ViewTone.Warn, "found", g.Found.ToString());
                }
                if (StudioRules.Progress(g) >= StudioCatalogue.TrailerWork && g.Found == 0 && g.Hidden > 0)
                {
                    primary = new ViewAction { Key = P + "button_desk", Act = StudioAct.Desk };
                    return Words(P + "next_playtest", ViewTone.Plain, "studio", s.Name);
                }
                return Words(P + "next_building", ViewTone.Plain, "days", StudioRules.DaysLeft(g.Work - g.Done, s.PointsADay * StudioRules.Boost(g, day) / 1000).ToString());
            }
            if (g.Reported > StudioRules.BugRoom(g) / 20 || g.Project == StudioRules.ProjectNone && g.Asks != 0 && g.Score >= StudioCatalogue.AwardScore)
            {
                primary = new ViewAction { Key = P + "button_desk", Act = StudioAct.Desk };
                return Words(P + "next_postlaunch", g.Reported > 0 ? ViewTone.Warn : ViewTone.Plain, "studio", s.Name, "bugs", g.Reported.ToString());
            }
            return g.Project != StudioRules.ProjectNone ? Words(P + "next_project", ViewTone.Plain) : Words(P + "next_selling", ViewTone.Plain);
        }

        private static void Build(StudioScreen v, GameDev g, StudioPlace s, int share, int day, IStudioFormat fmt)
        {
            ViewSection b = v.Section(P + "sec_build");
            long points = s.PointsADay * share / 1000 * StudioRules.Boost(g, day) / 1000;
            int days = StudioRules.DaysLeft(g.Work - g.Done, points);
            Line(b, days > 0 ? P + "build_progress" : P + "build_progress_still", ViewTone.Plain, "phase", fmt.Text(PhaseKey(g.Phase)), "done", fmt.Mille(StudioRules.Progress(g)),
                "days", fmt.Whole(days));
            Line(b, P + "build_parts", ViewTone.Plain, "design", fmt.Whole(StudioRules.PartQuality(g, (int)Part.Design)), "tech", fmt.Whole(StudioRules.PartQuality(g, (int)Part.Tech)),
                "art", fmt.Whole(StudioRules.PartQuality(g, (int)Part.Art)), "content", fmt.Whole(StudioRules.PartQuality(g, (int)Part.Content)),
                "polish", fmt.Whole(StudioRules.PartQuality(g, (int)Part.Polish)), "quality", fmt.Whole(StudioRules.Quality(g)));
            Line(b, g.Found > 0 ? P + "build_bugs" : P + "build_bugs_none", g.Found > StudioRules.BugRoom(g) / 4 ? ViewTone.Warn : ViewTone.Plain, "found", fmt.Whole(g.Found),
                "fix", fmt.Mille(g.Fix));
            if (day < g.CrunchUntil || day < g.BoostUntil)
            {
                Line(b, day < g.CrunchUntil ? P + "build_crunch" : P + "build_boost", day < g.CrunchUntil ? ViewTone.Warn : ViewTone.Plain,
                    "day", fmt.Day(Math.Max(g.CrunchUntil, g.BoostUntil)));
            }
            Line(b, P + "build_spent", ViewTone.Plain, "spent", fmt.Money(g.Spent), "budget", fmt.Money(g.Budget));
        }

        private static void Marketing(StudioScreen v, StudioFile f, GameDev g, int day, int yearDays, int beatPick, IStudioFormat fmt)
        {
            ViewSection m = v.Section(P + "sec_marketing");
            Line(m, P + "hype_line", g.Hype > StudioRules.Quality(g) * StudioCatalogue.BacklashPerQuality ? ViewTone.Warn : ViewTone.Plain, "hype", fmt.Mille(g.Hype),
                "carries", fmt.Mille(Math.Min(StudioCatalogue.HypeMost, StudioRules.Quality(g) * StudioCatalogue.BacklashPerQuality)));
            int pick = StudioRules.Clamp(beatPick, 0, StudioRules.Beats - 1);
            ViewChoice c = Choice(m, P + "label_beat", StudioAct.BeatPick, pick, false);
            for (int b = 0; b < StudioRules.Beats; b++)
            {
                c.Options.Add(fmt.Text(P + "beat_" + b));
            }
            string why = StudioRules.BeatWhy(g, pick, day, StudioRules.YearPart(day, yearDays));
            long cost = StudioRules.BeatCost(g, pick);
            c.Trade = why != null ? Words(why, ViewTone.Warn) : Words(P + "beat_" + pick + "_trade", ViewTone.Plain, "cost", fmt.Money(cost));
            c.Go = new ViewAction { Key = P + "button_beat", Act = StudioAct.Beat, Arg = pick, Enabled = why == null, WhyKey = why };
            c = Choice(m, P + "label_campaign", StudioAct.Campaign, StudioRules.Clamp(g.Campaign, 0, StudioCatalogue.CampaignShare.Length - 1), false);
            for (int i = 0; i < StudioCatalogue.CampaignShare.Length; i++)
            {
                c.Options.Add(i == 0 ? fmt.Text(P + "campaign_none") : fmt.Money(g.Budget * StudioCatalogue.CampaignShare[i] / 1000));
            }
            c.Trade = Words(P + "campaign_trade", ViewTone.Plain, "spent", fmt.Money(g.CampaignSpent), "days", fmt.Whole(StudioCatalogue.CampaignDays));
            if (g.Launched < 0)
            {
                c = Choice(m, P + "label_release", StudioAct.Release, 0, false);
                c.Options.Add(fmt.Text(P + "release_ready"));
                c.Values.Add(0);
                int picked = 0;
                if (g.ReleaseDay > day)
                {
                    c.Options.Add(fmt.Day(g.ReleaseDay));
                    c.Values.Add(g.ReleaseDay);
                    picked = 1;
                }
                for (int i = 0; i < ReleaseSteps.Length; i++)
                {
                    c.Options.Add(fmt.Day(day + ReleaseSteps[i]));
                    c.Values.Add(day + ReleaseSteps[i]);
                }
                c.Picked = picked;
                int target = g.ReleaseDay > day ? g.ReleaseDay : day;
                int crowd = StudioRules.Crowd(f, g.Genre, target, 14, null);
                int season = StudioRules.Season(target, yearDays);
                c.Trade = Words(crowd > 0 ? P + "release_crowd" : season > 1000 ? P + "release_holiday" : season < 1000 ? P + "release_summer" : P + "release_clear",
                    crowd > 0 || season < 1000 ? ViewTone.Warn : season > 1000 ? ViewTone.Good : ViewTone.Plain, "count", fmt.Whole(crowd));
            }
        }

        private static void Sales(StudioScreen v, StudioFile f, GameDev g, int day, IStudioFormat fmt)
        {
            ViewSection s = v.Section(P + "sec_sales");
            if (g.Score >= 0)
            {
                ViewTone tone = g.Score >= StudioCatalogue.HitScore ? ViewTone.Good : g.Score <= StudioCatalogue.FlopScore ? ViewTone.Bad : ViewTone.Plain;
                Line(s, g.Converted ? P + "sales_score_old" : P + "sales_score", tone, "score", fmt.Whole(g.Score), "expect", fmt.Whole(StudioRules.Expect(g.Genre, g.Launched, g.Sequel > 0)));
            }
            else
            {
                Line(s, P + "sales_early", ViewTone.Plain, "quality", fmt.Whole(StudioRules.Quality(g)));
            }
            Line(s, g.M == GameModel.FreeToPlay ? P + "sales_players" : g.M == GameModel.Subscription ? P + "sales_subscribers" : P + "sales_copies", ViewTone.Plain,
                "sold", fmt.Whole(g.Sold), "subs", fmt.Whole(g.Subscribers), "gross", fmt.Money(g.Gross), "net", fmt.Money(g.Net));
            Line(s, P + "sales_return", g.Net >= g.Spent ? ViewTone.Good : ViewTone.Plain, "spent", fmt.Money(g.Spent), "return", fmt.Mille(StudioRules.ReturnOf(g)));
            if (g.Pirated > 0 || g.Drm)
            {
                Line(s, g.Drm && !g.DrmRemoved ? P + "sales_pirated_drm" : P + "sales_pirated", ViewTone.Plain, "pirated", fmt.Whole(g.Pirated));
            }
            if (g.Published)
            {
                Line(s, P + "sales_publisher", ViewTone.Plain, "advance", fmt.Money(g.Advance), "earned", fmt.Money(g.Royalties));
            }
            if (g.Reported > 0)
            {
                Line(s, P + "sales_reported", g.Reported > StudioRules.BugRoom(g) / 10 ? ViewTone.Bad : ViewTone.Warn, "bugs", fmt.Whole(g.Reported));
            }
            if (g.Award)
            {
                Line(s, P + "sales_award", ViewTone.Good);
            }
            if (g.Sequel > 0 && f != null && f.GameOf(g.Sequel) != null && f.GameOf(g.Sequel).Burnt)
            {
                Line(s, P + "sales_burnt", ViewTone.Bad);
            }
        }

        private static void After(StudioScreen v, GameDev g, int projectPick, IStudioFormat fmt)
        {
            ViewSection a = v.Section(P + "sec_after");
            string asks = string.Empty;
            for (int i = 0; i < (int)FanAsk.Count; i++)
            {
                if ((g.Asks & (1 << i)) != 0)
                {
                    asks = asks.Length == 0 ? fmt.Text(AskKey(i)) : asks + ", " + fmt.Text(AskKey(i));
                }
            }
            Line(a, asks.Length > 0 ? P + "after_asks" : P + "after_asks_none", ViewTone.Plain, "asks", asks, "updates", fmt.Whole(g.Updates), "addons", fmt.Whole(g.AddOns));
            if (g.Project != StudioRules.ProjectNone)
            {
                Line(a, P + "after_project_" + g.Project, ViewTone.Plain, "done", fmt.Mille((int)Math.Min(1000L, g.ProjectDone * 1000 / Math.Max(1L, g.ProjectWork))),
                    "platform", fmt.Text(PlatformKey(g.ProjectArg)), "ask", fmt.Text(AskKey(g.ProjectArg)));
                return;
            }
            // the projects a page starts (updates and add-ons come from the post-launch review at the desk)
            int pick = StudioRules.Clamp(projectPick, 0, StudioCatalogue.GamePlatforms);
            ViewChoice c = Choice(a, P + "label_project", StudioAct.ProjectPick, pick, false);
            for (int p = 0; p < StudioCatalogue.GamePlatforms; p++)
            {
                c.Options.Add(fmt.Text(P + "project_port") + " " + fmt.Text(PlatformKey(p)));
            }
            c.Options.Add(fmt.Text(P + "project_localise"));
            int project = pick < StudioCatalogue.GamePlatforms ? StudioRules.ProjectPort : StudioRules.ProjectLocalise;
            string why = StudioRules.ProjectWhy(g, project, pick);
            long work = g.Work * (project == StudioRules.ProjectPort ? StudioCatalogue.PortWork : StudioCatalogue.LocaliseWork) / 1000;
            c.Trade = why != null ? Words(why, ViewTone.Warn) : Words(project == StudioRules.ProjectPort ? P + "project_port_trade" : P + "project_localise_trade", ViewTone.Plain,
                "points", fmt.Whole(work), "lift", fmt.Mille(StudioCatalogue.LocaliseLift));
            c.Go = new ViewAction { Key = P + "button_project", Act = StudioAct.Project, Arg = pick, Enabled = why == null, WhyKey = why };
        }

        private static void Team(StudioScreen v, GameDev g, int ownShare, IStudioFormat fmt)
        {
            ViewSection t = v.Section(P + "sec_team");
            ViewChoice c = Choice(t, P + "label_bonus", StudioAct.Bonus, StudioRules.Clamp(g.Bonus, 0, StudioCatalogue.BonusPool.Length - 1), false);
            for (int i = 0; i < StudioCatalogue.BonusPool.Length; i++)
            {
                c.Options.Add(i == 0 ? fmt.Text(P + "bonus_none") : fmt.Mille(StudioCatalogue.BonusPool[i]));
            }
            c.Trade = g.BonusDone ? Words(P + "bonus_done", ViewTone.Plain) : Words(g.Bonus == 0 ? P + "bonus_trade_none" : P + "bonus_trade", g.Bonus == 0 ? ViewTone.Warn : ViewTone.Plain,
                "days", fmt.Whole(StudioCatalogue.BonusDay));
            c = Choice(t, P + "label_leads", StudioAct.Leads, StudioRules.Clamp(g.Leads, 0, StudioCatalogue.LeadRoyalty.Length - 1), false);
            for (int i = 0; i < StudioCatalogue.LeadRoyalty.Length; i++)
            {
                c.Options.Add(i == 0 ? fmt.Text(P + "bonus_none") : fmt.Mille(StudioCatalogue.LeadRoyalty[i]));
            }
            c.Trade = Words(P + "leads_trade", ViewTone.Plain, "paid", fmt.Money(g.LeadsPaid));
            if (g.Phase < (int)GamePhase.Ended)
            {
                c = Choice(t, P + "label_share", StudioAct.Share, StudioRules.Clamp(ownShare / 25, 0, 4), false);
                for (int i = 0; i <= 4; i++)
                {
                    c.Options.Add(fmt.Mille(i * 250));
                }
                c.Trade = Words(P + "share_trade", ViewTone.Plain);
            }
        }

        // ---------------------------------------------------------------- the studio's part of its firm's page

        /// <summary>
        /// A studio's own lines on its firm's page: its engine, what its team knows, the hot genre and the rivals' newest
        /// games, with the one action that starts a game (the concept) or, with a draft, goes on with it.
        /// </summary>
        public static StudioScreen Studio(StudioFile f, StudioPlace s, int day, IStudioFormat fmt)
        {
            var v = new StudioScreen { Kind = ScreenStudio, TitleKey = P + "studio_title", TitlePairs = new[] { "studio", s.Name } };
            ViewSection own = v.Section(P + "sec_studio");
            StudioEngine e = f != null ? f.EngineAt(s.Place) : null;
            long engineWork = ScaleCatalogue.OwnEngineCost / StudioCatalogue.PointCents;
            if (e != null && e.Ready)
            {
                Line(own, P + "studio_engine_ready", ViewTone.Good, "craft", fmt.Whole(StudioCatalogue.OwnEngineCraft));
            }
            else if (e != null && e.Building)
            {
                Line(own, P + "studio_engine_building", ViewTone.Plain, "done", fmt.Mille((int)Math.Min(1000L, e.Done * 1000 / Math.Max(1L, engineWork))),
                    "share", fmt.Mille(StudioCatalogue.EngineShare));
            }
            else
            {
                Line(own, P + "studio_engine_none", ViewTone.Plain, "points", fmt.Whole(engineWork));
                v.Others.Add(new ViewAction { Key = P + "button_engine", Act = StudioAct.BuildEngine });
            }
            Line(own, P + "studio_known", ViewTone.Plain, "genres", Known(f, s.Place, fmt));
            if (s.Tickets > 0)
            {
                Line(own, P + "studio_support", s.Answered >= s.Tickets ? ViewTone.Plain : ViewTone.Warn, "tickets", fmt.Whole(s.Tickets), "answered", fmt.Whole(Math.Min(s.Tickets, s.Answered)));
            }
            ViewSection market = v.Section(P + "sec_market");
            if (f != null && f.TrendGenre >= 0 && day < f.TrendUntil)
            {
                Line(market, P + "outlook_trend", ViewTone.Good, "genre", fmt.Text(GenreKey(f.TrendGenre)), "day", fmt.Day(f.TrendUntil));
            }
            int shown = 0;
            for (int i = f != null ? f.Rivals.Count - 1 : -1; i >= 0 && shown < 3; i--, shown++)
            {
                RivalGame r = f.Rivals[i];
                Line(market, P + "studio_rival", r.Score >= StudioCatalogue.HitScore ? ViewTone.Warn : ViewTone.Plain, "studio", StudioCatalogue.RivalNames[r.Studio], "game", r.Name,
                    "genre", fmt.Text(GenreKey(r.Genre)), "score", fmt.Whole(r.Score), "sold", fmt.Whole(r.Sold));
            }
            if (shown == 0)
            {
                Line(market, P + "studio_rivals_none", ViewTone.Plain);
            }
            bool draft = f != null && f.DraftAt(s.Place) != null;
            v.Next = s.Room ? Words(draft ? P + "next_draft" : P + "next_concept", ViewTone.Plain, "studio", s.Name) : Words(P + "next_full", ViewTone.Warn);
            v.Primary = new ViewAction { Key = draft ? P + "button_concept_on" : P + "button_concept", Act = StudioAct.Concept, Enabled = s.Room, WhyKey = s.Room ? null : P + "next_full" };
            return v;
        }

        // The genres the studio's people have days in, the most first, three at most.
        private static string Known(StudioFile f, string place, IStudioFormat fmt)
        {
            if (f == null)
            {
                return fmt.Text(P + "studio_known_none");
            }
            var days = new long[(int)Genre.Count];
            for (int i = 0; i < f.Games.Count; i++)
            {
                GameDev g = f.Games[i];
                if (g.Place == place)
                {
                    days[g.Genre] += g.Done;
                }
            }
            string words = string.Empty;
            for (int n = 0; n < 3; n++)
            {
                int best = -1;
                for (int g = 0; g < days.Length; g++)
                {
                    best = days[g] > 0 && (best < 0 || days[g] > days[best]) ? g : best;
                }
                if (best < 0)
                {
                    break;
                }
                words = words.Length == 0 ? fmt.Text(GenreKey(best)) : words + ", " + fmt.Text(GenreKey(best));
                days[best] = 0;
            }
            return words.Length > 0 ? words : fmt.Text(P + "studio_known_none");
        }
    }
}
