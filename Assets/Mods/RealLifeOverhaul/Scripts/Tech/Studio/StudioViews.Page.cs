using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    public static partial class StudioViews
    {
        public const int WeekDays = 7;   // days in each point of the takings' line: a week's takings, day by day, so one busy day reads as a week's lift

        private static readonly TeamGroup[] StudioGroups =
        {
            TeamGroup.Engineers, TeamGroup.Designers, TeamGroup.Testers, TeamGroup.Product, TeamGroup.Data, TeamGroup.Infrastructure, TeamGroup.Security,
            TeamGroup.Support, TeamGroup.Sales, TeamGroup.Business
        };

        /// <summary>A team group's name, the same on every tech firm's page.</summary>
        public static string GroupKey(TeamGroup group) => P + "page_group_" + group.ToString().ToLowerInvariant();

        /// <summary>The team's heading on every tech firm's page.</summary>
        public static string TeamKey => P + "page_team";

        /// <summary>A group and how many are in it ("Engineers (4)"), for a bar of the team.</summary>
        public static string GroupCountKey => P + "page_group_count";

        /// <summary>The word a team's bar shows for a group the firm needs and has none of.</summary>
        public static string NeededKey => P + "page_needed";

        /// <summary>
        /// A studio's whole page as the mod's design system draws it: what to do next and its actions as the studio's part
        /// of its firm's page has them, the games out, the copies sold and the last 30 days' takings, its games (those in
        /// the works first, then the newest out), the games market's newest, its team by group with the groups a studio
        /// needs and lacks, and its sales and support.
        /// </summary>
        public static FirmPage Page(StudioFile f, FirmsFile firms, StudioPlace s, FirmReading r, int day, IStudioFormat fmt)
        {
            var page = new FirmPage { Screen = Studio(f, s, day, fmt), TeamKey = TeamKey, FactsKey = P + "page_facts" };
            var games = new List<GameDev>(8);
            long sold = 0, pirated = 0;
            int released = 0, live = 0, drm = 0, asks = 0;
            for (int i = 0; f != null && i < f.Games.Count; i++)
            {
                GameDev g = f.Games[i];
                if (g.Place != s.Place)
                {
                    continue;
                }
                games.Add(g);
                if (g.Launched >= 0)
                {
                    released++;
                    sold += g.Sold;
                    pirated += g.Pirated;
                }
                if (g.OnSale)
                {
                    live++;
                    drm += g.Drm && !g.DrmRemoved ? 1 : 0;
                    asks |= g.Asks;
                }
            }
            games.Sort(Newest);
            page.Figures.Add(new ViewFigure { LabelKey = P + "page_games_out", Value = fmt.Whole(released) });
            page.Figures.Add(new ViewFigure { LabelKey = P + "page_copies", Value = fmt.Whole(sold) });
            page.Figures.Add(TakingsFigure(r, P + "page_takings", fmt));
            Head(page.Main, new[] { P + "page_col_game", P + "page_col_genre", P + "page_col_score", P + "page_col_copies", P + "page_col_kept", P + "page_col_status" },
                new[] { false, false, true, true, true, true });
            page.Main.EmptyKey = P + "page_no_games";
            for (int i = 0; i < games.Count; i++)
            {
                GameDev g = games[i];
                bool shipped = g.Launched >= 0;
                page.Main.Rows.Add(new[]
                {
                    NameOf(firms, g.Id), fmt.Text(GenreKey(g.Genre)), g.Score >= 0 ? fmt.Whole(g.Score) : string.Empty, shipped ? fmt.Whole(g.Sold) : string.Empty,
                    shipped ? fmt.Money(g.Net) : string.Empty, Age(g, day, fmt)
                });
            }
            Head(page.More, new[] { P + "page_col_rival", P + "page_col_studio", P + "page_col_genre", P + "page_col_score", P + "page_col_copies" },
                new[] { false, false, false, true, true });
            page.More.EmptyKey = P + "studio_rivals_none";
            for (int i = f != null ? f.Rivals.Count - 1 : -1; i >= 0; i--)
            {
                RivalGame rg = f.Rivals[i];
                page.More.Rows.Add(new[]
                {
                    rg.Name, StudioCatalogue.RivalNames[StudioRules.Clamp(rg.Studio, 0, StudioCatalogue.RivalNames.Length - 1)], fmt.Text(GenreKey(rg.Genre)), fmt.Whole(rg.Score),
                    fmt.Whole(rg.Sold)
                });
            }
            for (int i = 0; i < StudioGroups.Length; i++)
            {
                TeamGroup g = StudioGroups[i];
                // A studio builds with engineers and designers, finds its bugs with testers once there are a few hands, and
                // answers its players with support while they write in.
                bool needs = g == TeamGroup.Engineers || g == TeamGroup.Designers || (g == TeamGroup.Testers && r.Count[(int)TeamGroup.Engineers] >= 2)
                             || (g == TeamGroup.Support && s.Tickets > 0);
                AddRole(page, r, g, needs);
            }
            Fact(page, P + "page_tickets", s.Tickets > 0
                ? Cell(fmt, P + "page_tickets_value", "tickets", fmt.Whole(s.Tickets), "answered", fmt.Whole(Math.Min(s.Tickets, s.Answered))) : NoneWords(fmt));
            Fact(page, P + "page_pirated", fmt.Whole(pirated));
            Fact(page, P + "page_drm", drm > 0 ? Cell(fmt, P + "page_drm_on", "count", fmt.Whole(drm), "games", fmt.Whole(live)) : fmt.Text(P + "page_drm_off"));
            Fact(page, P + "page_asks", asks != 0 ? Asks(asks, fmt) : NoneWords(fmt));
            Fact(page, P + "page_engine", Engine(f, s.Place, fmt));
            Fact(page, P + "page_trend", f != null && f.TrendGenre >= 0 && day < f.TrendUntil
                ? Cell(fmt, P + "page_trend_value", "genre", fmt.Text(GenreKey(f.TrendGenre)), "day", fmt.Day(f.TrendUntil)) : NoneWords(fmt));
            string known = Known(f, s.Place, fmt);
            Fact(page, P + "page_known", known == fmt.Text(P + "studio_known_none") ? NoneWords(fmt) : known);   // a value alone takes the page's "None", not the sentence's "no genre yet"
            return page;
        }

        /// <summary>
        /// The last 30 days' takings as a wide figure: their sum, and beside it a line of a week's takings for each day once a
        /// week is in, so a launch's spike reads as the week it lifted rather than a needle.
        /// </summary>
        public static ViewFigure TakingsFigure(FirmReading r, string labelKey, IStudioFormat fmt)
        {
            int n = r.Takings.Length;
            long sum = 0;
            for (int i = 0; i < n; i++)
            {
                sum += r.Takings[i];
            }
            var trend = new float[Math.Max(2, n - WeekDays + 1)];
            for (int i = 0; i + WeekDays <= n; i++)
            {
                long week = 0;
                for (int d = i; d < i + WeekDays; d++)
                {
                    week += r.Takings[d];
                }
                trend[i] = week;
            }
            return new ViewFigure { LabelKey = labelKey, Value = fmt.Money(sum), Trend = trend, Wide = true };
        }

        /// <summary>How long a thing has been out, in days, as every tech firm's page writes it.</summary>
        public static string AgeDays(int days, IStudioFormat fmt) => Cell(fmt, P + "page_age_days", "days", fmt.Whole(Math.Max(0, days)));

        /// <summary>The word a page writes where there is nothing to show.</summary>
        public static string NoneWords(IStudioFormat fmt) => fmt.Text(P + "page_none");

        /// <summary>A table's head: its columns' keys and which of them hold figures.</summary>
        public static void Head(ViewTable t, string[] keys, bool[] figures)
        {
            t.ColumnKeys = keys;
            t.Figures = figures;
            t.Rows.Clear();
        }

        /// <summary>A group of the team on a page: its people and their mean skill, or the group the firm needs and lacks; nothing for a group neither has nor needs.</summary>
        public static void AddRole(FirmPage page, FirmReading r, TeamGroup g, bool needed)
        {
            int count = r.Count[(int)g];
            if (count == 0 && !needed)
            {
                return;
            }
            int skill = count > 0 ? (int)Math.Max(0L, Math.Min(100L, r.Skill[(int)g] / count)) : 0;
            page.Team.Add(new ViewRole { Group = g, Count = count, Skill = skill, Needed = count == 0 });
        }

        /// <summary>A fact of a page: its label's key and its value.</summary>
        public static void Fact(FirmPage page, string key, string value) => page.Facts.Add(new ViewPair { LabelKey = key, Value = value ?? string.Empty });

        /// <summary>A key's words with its names filled: name, value, name, value.</summary>
        public static string Cell(IStudioFormat fmt, string key, params string[] pairs)
        {
            string words = fmt.Text(key);
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                words = words.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            }
            return words;
        }

        // In the works first (the furthest along first), then on sale and then withdrawn, the newest launch first in each.
        private static int Newest(GameDev a, GameDev b)
        {
            int ra = a.Launched < 0 ? 0 : a.Phase == (int)GamePhase.Ended ? 2 : 1, rb = b.Launched < 0 ? 0 : b.Phase == (int)GamePhase.Ended ? 2 : 1;
            if (ra != rb)
            {
                return ra.CompareTo(rb);
            }
            return ra == 0 ? StudioRules.Progress(b).CompareTo(StudioRules.Progress(a)) : b.Launched.CompareTo(a.Launched);
        }

        // A game's name as its product keeps it.
        private static string NameOf(FirmsFile firms, int id)
        {
            for (int i = 0; firms != null && i < firms.Products.Count; i++)
            {
                if (firms.Products[i].Id == id)
                {
                    return firms.Products[i].Name;
                }
            }
            return string.Empty;
        }

        // How far along a game in the works is, or how long one has been out, or that it is ready or withdrawn.
        private static string Age(GameDev g, int day, IStudioFormat fmt)
        {
            if (g.Phase == (int)GamePhase.Ended)
            {
                return fmt.Text(PhaseKey(g.Phase));
            }
            if (g.Launched >= 0)
            {
                return AgeDays(day - g.Launched, fmt);
            }
            return g.Phase == (int)GamePhase.Ready ? fmt.Text(PhaseKey(g.Phase)) : Cell(fmt, P + "page_made", "done", fmt.Mille(StudioRules.Progress(g)));
        }

        // The fan requests open across the studio's games on sale, as their names.
        private static string Asks(int bits, IStudioFormat fmt)
        {
            string words = string.Empty;
            for (int i = 0; i < (int)FanAsk.Count; i++)
            {
                if ((bits & (1 << i)) != 0)
                {
                    words = words.Length == 0 ? fmt.Text(AskKey(i)) : words + ", " + fmt.Text(AskKey(i));
                }
            }
            return words;
        }

        // The studio's engine: its own when built, how far one is built, or the licensed one it uses meanwhile.
        private static string Engine(StudioFile f, string place, IStudioFormat fmt)
        {
            StudioEngine e = f != null ? f.EngineAt(place) : null;
            if (e != null && e.Ready)
            {
                return fmt.Text(P + "page_engine_own");
            }
            if (e != null && e.Building)
            {
                long work = Math.Max(1L, ScaleCatalogue.OwnEngineCost / StudioCatalogue.PointCents);
                return Cell(fmt, P + "page_engine_building", "done", fmt.Mille((int)Math.Min(1000L, e.Done * 1000 / work)));
            }
            return fmt.Text(P + "page_engine_licensed");
        }
    }
}
