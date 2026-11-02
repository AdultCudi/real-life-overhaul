using System;
using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Where the status bar's pieces stand on a strip of a width, worked out before any is drawn: how many apps the row keeps
    /// and where it stands, which pills fit whole and how wide each is, and the room of the keys' prompts.
    /// </summary>
    public struct UiPillPlan
    {
        public float Time, Weather, Place, Money, Needs, Alerts;   // px each pill takes; 0 for one left out
        public bool Day, Temperature, District;                     // the day's words, the temperature and the district stay in their pills
        public float Meter;                                         // px: each need's cell, its glyph over a meter as wide
        public float MoneyX;                                        // px: the money pill's left edge, at the strip's right corner
        public int Apps;                                            // the app row's apps before All apps
        public float RowX, RowW;                                    // px: the app row's left edge and its width; 0 wide for no row
        public float PromptX, Prompts;                              // px: the keys' prompts' left edge and width, before the right pills
    }

    /// <summary>
    /// The status bar's pills, the HUD's readings after Cities: Skylines II's bottom bar, on the strip's one centre line with
    /// the app row between them (UiDraw.StatusBar). At the left: the time with the game's speed (or the pause in words) and
    /// the day as the game's top bar writes it, the weather with the season, and the place (the nearest building and its
    /// district, as the game's HUD names them). At the right: the player's needs, each a glyph over its short meter (the
    /// game's energy, hunger and happiness, the mod's hygiene, stress and home), the alerts, and the money at the corner. A
    /// field left empty leaves its pill out, and every pill that shows is whole: a strip the UI zoom made narrow gives up,
    /// at the left, the district, the temperature, the weather, the place and last the day's words, and at the right the
    /// meters' length, the alerts and the needs, while the app row first hands apps to All apps, down to four. While the
    /// keys lead their prompts stand between the row and the right pills, which give way to them. The time, the money, the
    /// needs and the alerts are targets only where the model says the frame answers them (the HUD's bar). The money's change
    /// is a piece of its own (<see cref="MoneyChange"/>), rising above the bar where the HUD shows it. Game-free. Cost: a
    /// few boxes and measures a layout.
    /// </summary>
    public static partial class UiDraw
    {
        /// <summary>The pills' targets on the HUD's bar: the time (the game's pause), the money, the needs and the alerts.</summary>
        public const int TimePillCommand = 9020, MoneyPillCommand = 9021, NeedsPillCommand = 9022, AlertsPillCommand = 9023;

        private const byte PillRegion = 241;         // the d-pad's column of its own: Left and Right walk the pills
        private const float Chevron = 12f;           // px: one of the speed's chevrons
        private const float NeedCell = 28f;          // px: a need's cell, its glyph over a meter as wide
        private const float NeedTight = 24f;         // px: on a strip the UI zoom made narrow
        private const float NeedHeight = UiTokens.Icon + UiTokens.S1 * 2f;   // px: a need's glyph, a step, its meter
        private const float BarEdge = UiTokens.S4;   // px from the screen's sides to the outer pills
        private const float BarGap = UiTokens.S6;    // px between the row and the pills each side, and before the prompts

        /// <summary>True for a pill's target, which only the HUD answers.</summary>
        public static bool IsPill(int command) => command >= TimePillCommand && command <= AlertsPillCommand;

        /// <summary>
        /// Where everything stands on a strip of a width, as <see cref="StatusBar"/> draws it: the row's apps and place, each
        /// pill's width, the money's corner, and the room of the keys' prompts (<paramref name="prompts"/> px while they lead).
        /// </summary>
        public static UiPillPlan PlanPills(UiStatusModel m, float w, float prompts = 0f)
        {
            var p = new UiPillPlan { Day = true, Temperature = true, District = true, Meter = NeedCell, Prompts = prompts };
            if (m == null)
            {
                p.MoneyX = w - BarEdge;
                p.PromptX = w - BarEdge - prompts;
                return p;
            }
            p.Time = TimeWidth(m, true);
            p.Weather = WeatherWidth(m, true);
            p.Place = PlaceWidth(m, true);
            p.Money = MoneyWidth(m);
            p.Needs = NeedsWidth(m, NeedCell);
            p.Alerts = AlertsWidth(m);
            // The row keeps every app it is given until the time with its day, or the right pills at their tightest (or the
            // prompts while the keys lead), would not stand whole beside it; then apps wait under All apps, down to four.
            float least = Math.Max(p.Time, Math.Max(prompts, Pills(p.Money, p.Alerts, NeedsWidth(m, NeedTight))));
            int apps = RowApps(m);
            while (apps > 0 && (Zone(m, w, RowWidth(m, apps)) < 0f || (apps > RowLeast && Zone(m, w, RowWidth(m, apps)) < least)))
            {
                apps--;
            }
            p.Apps = apps;
            p.RowW = RowWidth(m, apps);
            p.RowX = SnapDown((w - p.RowW) / 2f);
            float zone = Zone(m, w, p.RowW);
            FitLeft(m, ref p, zone);
            FitRight(m, ref p, zone);
            p.MoneyX = w - BarEdge - p.Money;
            float pills = Pills(p.Money, p.Alerts, p.Needs);
            p.PromptX = w - BarEdge - pills - (pills > 0f && prompts > 0f ? BarGap : 0f) - prompts;
            return p;
        }

        // The room each side's pills have: from the screen's edge to the row (or to a row drawn over the bar in its place, the
        // build tools), a gap kept from it; to the middle when the strip has no row.
        private static float Zone(UiStatusModel m, float w, float row)
        {
            row = Math.Max(row, m.Middle);
            return row > 0f ? SnapDown((w - row) / 2f) - BarEdge - BarGap : SnapDown((w - BarGap) / 2f) - BarEdge;
        }

        // The left pills within their room: the district goes first, then the temperature, the weather, the place (the weather
        // back if it then fits alone), and last the day's words; the time stays.
        private static void FitLeft(UiStatusModel m, ref UiPillPlan p, float room)
        {
            if (Pills(p.Time, p.Weather, p.Place) > room && p.Place > 0f && !string.IsNullOrEmpty(m.District))
            {
                p.District = false;
                p.Place = PlaceWidth(m, false);
            }
            if (Pills(p.Time, p.Weather, p.Place) > room && p.Weather > 0f && !string.IsNullOrEmpty(m.Temperature))
            {
                p.Temperature = false;
                p.Weather = WeatherWidth(m, false);
            }
            float weather = p.Weather;
            if (Pills(p.Time, p.Weather, p.Place) > room)
            {
                p.Weather = 0f;
            }
            if (Pills(p.Time, p.Weather, p.Place) > room)
            {
                p.Place = 0f;
                p.Weather = Pills(p.Time, weather, 0f) <= room ? weather : 0f;
            }
            if (Pills(p.Time, p.Weather, p.Place) > room && p.Day)
            {
                p.Day = false;
                p.Time = TimeWidth(m, false);
            }
        }

        // The right pills within their room, after the prompts while the keys lead: the meters shorten, then the alerts go, then
        // the needs (the alerts back if they then fit alone); only the prompts could take the money's corner.
        private static void FitRight(UiStatusModel m, ref UiPillPlan p, float room)
        {
            if (Right(ref p) > room && p.Needs > 0f)
            {
                p.Meter = NeedTight;
                p.Needs = NeedsWidth(m, NeedTight);
            }
            float alerts = p.Alerts;
            if (Right(ref p) > room)
            {
                p.Alerts = 0f;
            }
            if (Right(ref p) > room)
            {
                p.Needs = 0f;
                p.Alerts = alerts;
                if (Right(ref p) > room)
                {
                    p.Alerts = 0f;
                }
            }
            if (Right(ref p) > room)
            {
                p.Money = 0f;
            }
        }

        private static float Right(ref UiPillPlan p)
        {
            float pills = Pills(p.Money, p.Alerts, p.Needs);
            return p.Prompts + (p.Prompts > 0f && pills > 0f ? BarGap : 0f) + pills;
        }

        // Pills side by side, a grid step between neighbours; 0 for none.
        private static float Pills(float a, float b, float c)
        {
            int n = (a > 0f ? 1 : 0) + (b > 0f ? 1 : 0) + (c > 0f ? 1 : 0);
            return a + b + c + Math.Max(0, n - 1) * UiTokens.S2;
        }

        private static float WeatherWidth(UiStatusModel m, bool temperature) => string.IsNullOrEmpty(m.Season) ? 0f : PillWidth(m.Weather, WeatherWords(m, temperature));

        private static float MoneyWidth(UiStatusModel m) => string.IsNullOrEmpty(m.Money) ? 0f
            : UiTokens.S2 + UiTokens.Icon + UiTokens.S2 + Snap(UiTextRules.Width(m.Money, UiStyle.Number) + UiTokens.S1) + UiTokens.S3 + (m.MoneyTrend != 0 ? UiTokens.IconSmall + UiTokens.S1 : 0f);

        private static float AlertsWidth(UiStatusModel m) => m.Alerts > 0 ? PillWidth(UiIcon.Bell, Count(m.Alerts)) : 0f;

        // The needs' pill: each need a cell, a grid step between (half a step on a narrow strip), padded as a pill.
        private static float NeedsWidth(UiStatusModel m, float cell)
        {
            int n = NeedCount(m);
            return n == 0 ? 0f : UiTokens.S3 * 2f + n * cell + (n - 1) * NeedGap(cell);
        }

        private static float NeedGap(float cell) => cell > NeedTight ? UiTokens.S2 : UiTokens.S1;

        // The pills on the strip's centre line: the left group from the left edge, the right group to the right edge with the
        // money at the corner, each a target where the HUD answers it, linked left to right for the d-pad.
        private static void HudPills(UiScene s, UiAt on, float mid, float w, ref UiPillPlan p, UiStatusModel m)
        {
            float py = mid - PillH / 2f;
            UiAt row = on.InRegion(PillRegion);
            int first = s.NodeCount;
            float px = BarEdge;
            if (p.Time > 0f)
            {
                TimePill(s, row, px, py, p.Time, m, p.Day);
                px += p.Time + UiTokens.S2;
            }
            if (p.Weather > 0f)
            {
                // The weather's icon goes with its temperature, as a city builder's bar shows it, and the season stays words.
                int face = s.ItemCount;   // the pill's own face is its first item
                Pill(s, row, px, py, m.Weather, UiRole.None, WeatherWords(m, p.Temperature), UiRole.Text);
                s.Items[face].Flags |= UiFlags.Middle;
                px += p.Weather + UiTokens.S2;
            }
            if (p.Place > 0f)
            {
                PlacePill(s, row, px, py, p.Place, m, p.District);
            }
            float rx = w - BarEdge - Pills(p.Money, p.Alerts, p.Needs);
            if (p.Needs > 0f)
            {
                NeedsPill(s, row, rx, py, p.Needs, p.Meter, m);
                rx += p.Needs + UiTokens.S2;
            }
            if (p.Alerts > 0f)
            {
                int face = s.ItemCount;   // the pill's own face is its first item
                Pill(s, row, rx, py, UiIcon.Bell, UiRole.None, Count(m.Alerts), UiRole.Text);
                s.Items[face].Flags |= UiFlags.Middle;
                PillTarget(s, row, rx, py, p.Alerts, AlertsPillCommand, face, m);
                rx += p.Alerts + UiTokens.S2;
            }
            if (p.Money > 0f)
            {
                MoneyPill(s, row, rx, py, p.Money, m);
            }
            for (int n = first; n < s.NodeCount; n++)
            {
                s.Nodes[n].Left = n > first ? n - 1 : -1;
                s.Nodes[n].Right = n < s.NodeCount - 1 ? n + 1 : -1;
            }
        }

        /// <summary>
        /// Joins a row of targets standing in the strip's middle (the app row, the build tools: nodes first to last, left to
        /// right) to the pills that are targets on its either side, so the d-pad walks the whole strip as one row.
        /// </summary>
        public static void JoinPills(UiScene s, int first, int last)
        {
            if (first < 0 || last < first || last >= s.NodeCount)
            {
                return;
            }
            float left = s.Nodes[first].X, right = s.Nodes[last].X + s.Nodes[last].W;
            int before = -1, after = -1;
            for (int i = 0; i < s.NodeCount; i++)
            {
                ref UiNode n = ref s.Nodes[i];
                if (!IsPill(n.Command) || n.Layer != s.Nodes[first].Layer)
                {
                    continue;
                }
                if (n.X + n.W <= left && (before < 0 || n.X > s.Nodes[before].X))
                {
                    before = i;
                }
                if (n.X >= right && (after < 0 || n.X < s.Nodes[after].X))
                {
                    after = i;
                }
            }
            if (before >= 0)
            {
                s.Nodes[before].Right = first;
                s.Nodes[first].Left = before;
            }
            if (after >= 0)
            {
                s.Nodes[after].Left = last;
                s.Nodes[last].Right = after;
            }
        }

        // A pill's face made a target, where the frame answers the pills.
        private static void PillTarget(UiScene s, UiAt at, float x, float y, float w, int command, int face, UiStatusModel m)
        {
            if (m.PillsPress)
            {
                Node(s, at, x, y, w, PillH, command, 0, face);
            }
        }

        // A pill's face, centred on the bar's row of pills rather than on a grid step from the bar's top.
        private static int PillFace(UiScene s, UiAt at, float x, float y, float w) =>
            Box(s, at, x, y, w, PillH, UiRole.Pill, UiRole.None, UiTokens.RadiusLarge, UiFlags.Chrome | UiFlags.Middle);

        private static float TimeWidth(UiStatusModel m, bool day)
        {
            if (string.IsNullOrEmpty(m.Time))
            {
                return 0f;
            }
            float tw = Snap(UiTextRules.Width(m.Time, UiStyle.BodyStrong) + UiTokens.S1);
            float dw = day ? DayWidth(m) : 0f;
            float tail = TimeTail(m);
            return Snap(UiTokens.S2 + UiTokens.Icon + UiTokens.S2 + tw + (dw > 0f ? UiTokens.S3 + dw : 0f) + (tail > 0f ? UiTokens.S3 + tail : 0f) + UiTokens.S2);
        }

        // The day's slot: as wide as the widest day of the week in the player's language, so a new day never moves the pills.
        private static float DayWidth(UiStatusModel m)
        {
            if (string.IsNullOrEmpty(m.Day))
            {
                return 0f;
            }
            float w = UiTextRules.Width(m.Day, UiStyle.Body);
            if (!string.IsNullOrEmpty(m.DayWidest))
            {
                w = Math.Max(w, UiTextRules.Width(m.DayWidest, UiStyle.Body));
            }
            return Snap(w + UiTokens.S1);
        }

        // What follows the time: the pause in words while paused, else the game's speed as chevrons once the game has said it;
        // one slot for both, as wide as the wider, so pausing never moves the pills beside it.
        private static float TimeTail(UiStatusModel m)
        {
            float words = string.IsNullOrEmpty(m.PausedWords) ? 0f : Snap(UiTextRules.Width(m.PausedWords, UiStyle.SmallStrong) + UiTokens.S1);
            float chevrons = m.Speed > 0 || m.Paused ? Chevron * 3f : 0f;
            return Math.Max(chevrons, words);
        }

        // The time pill: the play or pause mark, the time, the day, and the speed's chevrons or the pause's words.
        private static void TimePill(UiScene s, UiAt at, float x, float y, float w, UiStatusModel m, bool day)
        {
            int face = PillFace(s, at, x, y, w);
            UiAt inside = at.In(face);
            float cx = x + UiTokens.S2;
            Icon(s, inside, cx, y + (PillH - UiTokens.Icon) / 2f, UiTokens.Icon, m.Paused ? UiIcon.Pause : UiIcon.Play, UiRole.None, UiFlags.Middle | UiFlags.Chrome);
            cx += UiTokens.Icon + UiTokens.S2;
            float tw = Snap(UiTextRules.Width(m.Time, UiStyle.BodyStrong) + UiTokens.S1);
            TextMiddle(s, inside, cx, y, PillH, tw, UiStyle.BodyStrong, UiRole.Text, m.Time, UiFlags.Chrome);
            cx += tw;
            if (day && !string.IsNullOrEmpty(m.Day))
            {
                float dw = DayWidth(m);
                TextMiddle(s, inside, cx + UiTokens.S3, y, PillH, dw, UiStyle.Body, UiRole.Text2, m.Day, UiFlags.Chrome);
                cx += UiTokens.S3 + dw;
            }
            cx += UiTokens.S3;
            if (m.Paused && !string.IsNullOrEmpty(m.PausedWords))
            {
                // The pause is said in words in the warning's ink: a glyph keeps its own colour, and the chevrons would only be grey.
                TextMiddle(s, inside, cx, y, PillH, Snap(UiTextRules.Width(m.PausedWords, UiStyle.SmallStrong) + UiTokens.S1), UiStyle.SmallStrong, UiRole.WarnText, m.PausedWords,
                    UiFlags.Label | UiFlags.Chrome);
            }
            else
            {
                for (int i = 0; m.Speed > 0 && i < 3; i++)
                {
                    Icon(s, inside, cx + i * Chevron, y + (PillH - Chevron) / 2f, Chevron, UiIcon.Next, i < m.Speed && !m.Paused ? UiRole.Accent : UiRole.Muted,
                        UiFlags.Middle | UiFlags.Share | UiFlags.Chrome);
                }
            }
            PillTarget(s, at, x, y, w, TimePillCommand, face, m);
        }

        private static string WeatherWords(UiStatusModel m, bool temperature) =>
            temperature && !string.IsNullOrEmpty(m.Temperature) ? m.Temperature + ", " + m.Season : m.Season;

        private static float PlaceWidth(UiStatusModel m, bool district)
        {
            if (string.IsNullOrEmpty(m.Place))
            {
                return 0f;
            }
            float dw = district && !string.IsNullOrEmpty(m.District) ? UiTokens.S2 + Snap(UiTextRules.Width(m.District, UiStyle.Body) + UiTokens.S1) : 0f;
            return UiTokens.S2 + UiTokens.Icon + UiTokens.S2 + Snap(UiTextRules.Width(m.Place, UiStyle.Body) + UiTokens.S1) + dw + UiTokens.S3;
        }

        // The place: the nearest building's address and, quieter beside it, its district, as the game's HUD names them.
        private static void PlacePill(UiScene s, UiAt at, float x, float y, float w, UiStatusModel m, bool district)
        {
            int face = PillFace(s, at, x, y, w);
            UiAt inside = at.In(face);
            float cx = x + UiTokens.S2;
            Icon(s, inside, cx, y + (PillH - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.MapPin, UiRole.None, UiFlags.Middle | UiFlags.Chrome);
            cx += UiTokens.Icon + UiTokens.S2;
            float end = x + w - UiTokens.S3;
            float dw = district && !string.IsNullOrEmpty(m.District) ? Snap(UiTextRules.Width(m.District, UiStyle.Body) + UiTokens.S1) : 0f;
            float pw = Math.Min(Snap(UiTextRules.Width(m.Place, UiStyle.Body) + UiTokens.S1), SnapDown(end - cx - (dw > 0f ? UiTokens.S2 + dw : 0f)));
            TextMiddle(s, inside, cx, y, PillH, pw, UiStyle.Body, UiRole.Text, m.Place, UiFlags.Truncate | UiFlags.Chrome);
            if (dw > 0f)
            {
                TextMiddle(s, inside, cx + pw + UiTokens.S2, y, PillH, dw, UiStyle.Body, UiRole.Text2, m.District, UiFlags.Truncate | UiFlags.Chrome);
            }
        }

        // The money in the bar's largest words, as the game keeps it in view, with today's way as an arrow.
        private static void MoneyPill(UiScene s, UiAt at, float x, float y, float w, UiStatusModel m)
        {
            int face = PillFace(s, at, x, y, w);
            UiAt inside = at.In(face);
            float cx = x + UiTokens.S2;
            Icon(s, inside, cx, y + (PillH - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Wallet, UiRole.None, UiFlags.Middle | UiFlags.Chrome);
            cx += UiTokens.Icon + UiTokens.S2;
            float tw = Snap(UiTextRules.Width(m.Money, UiStyle.Number) + UiTokens.S1);
            TextMiddle(s, inside, cx, y, PillH, tw, UiStyle.Number, UiRole.Text, m.Money, UiFlags.Chrome);
            if (m.MoneyTrend != 0)
            {
                Icon(s, inside, cx + tw, y + (PillH - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, m.MoneyTrend > 0 ? UiIcon.TrendUp : UiIcon.TrendDown,
                    m.MoneyTrend > 0 ? UiRole.Ok : UiRole.Danger, UiFlags.Middle | UiFlags.Chrome);
            }
            PillTarget(s, at, x, y, w, MoneyPillCommand, face, m);
        }

        private static int NeedCount(UiStatusModel m) =>
            (m.Energy >= 0 ? 1 : 0) + (m.Hunger >= 0 ? 1 : 0) + (m.Happiness >= 0 ? 1 : 0) + (m.Hygiene >= 0 ? 1 : 0) + (m.Stress >= 0 ? 1 : 0) + (m.HomeNeed >= 0 ? 1 : 0);

        // The player's needs in one pill, each a glyph in its own colour over a meter as wide: the meter says how full and its
        // tone how well (stress is shown as calm, so every meter in the row reads full as good).
        private static void NeedsPill(UiScene s, UiAt at, float x, float y, float w, float cell, UiStatusModel m)
        {
            int face = PillFace(s, at, x, y, w);
            UiAt inside = at.In(face);
            float gap = NeedGap(cell), cx = x + UiTokens.S3;
            cx = Need(s, inside, cx, y, cell, gap, UiIcon.Lightning, m.Energy);
            cx = Need(s, inside, cx, y, cell, gap, UiIcon.ForkKnife, m.Hunger);
            cx = Need(s, inside, cx, y, cell, gap, UiIcon.SmileyHappy, m.Happiness);
            cx = Need(s, inside, cx, y, cell, gap, UiIcon.Drop, m.Hygiene);
            cx = Need(s, inside, cx, y, cell, gap, UiIcon.Brain, m.Stress < 0 ? -1 : 100 - m.Stress);
            Need(s, inside, cx, y, cell, gap, UiIcon.Home, m.HomeNeed);
            PillTarget(s, at, x, y, w, NeedsPillCommand, face, m);
        }

        // A need's cell, centred in the pill: the glyph keeps its own colour and the meter under it says how full, as a state is
        // told by its bar, never by recolouring a glyph.
        private static float Need(UiScene s, UiAt at, float x, float y, float cell, float gap, UiIcon icon, int value)
        {
            if (value < 0)
            {
                return x;
            }
            UiRole tone = value >= 60 ? UiRole.Ok : value >= 30 ? UiRole.Warn : UiRole.Danger;
            float top = y + (PillH - NeedHeight) / 2f;
            Icon(s, at, x + (cell - UiTokens.Icon) / 2f, top, UiTokens.Icon, icon, UiRole.None, UiFlags.Middle | UiFlags.Centre | UiFlags.Chrome);
            Meter(s, at, x, top + UiTokens.Icon + UiTokens.S1, cell, UiTokens.S1, value, tone);
            return x + cell + gap;
        }

        private static string Count(int n) => n > 99 ? "99+" : n.ToString(CultureInfo.InvariantCulture);

        /// <summary>The money change's chip's height.</summary>
        public const float MoneyChangeHeight = 28f;

        /// <summary>
        /// The money's last change as a chip standing on the bar over the money pill's right edge, in good's or bad's ink with
        /// its arrow, as the game floats its "+$" texts from the money. The HUD lifts and fades it (UiHudRules.ChangeLift,
        /// ChangeAlpha); laid out here where it starts. Returns its width.
        /// </summary>
        public static float MoneyChange(UiScene s, UiAt at, float right, float bottom, string words, bool rises)
        {
            if (string.IsNullOrEmpty(words))
            {
                return 0f;
            }
            float tw = UiTextRules.Width(words, UiStyle.BodyStrong) + UiTokens.S1;
            float w = Snap(UiTokens.S2 + UiTokens.IconSmall + UiTokens.S1 + tw + UiTokens.S2);
            float x = right - w, y = bottom - MoneyChangeHeight;
            int face = Box(s, at, x, y, w, MoneyChangeHeight, UiRole.Pill, UiRole.None, UiTokens.RadiusLarge, UiFlags.Chrome);
            UiAt inside = at.In(face);
            Icon(s, inside, x + UiTokens.S2, y + (MoneyChangeHeight - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, rises ? UiIcon.TrendUp : UiIcon.TrendDown,
                rises ? UiRole.Ok : UiRole.Danger, UiFlags.Middle | UiFlags.Chrome);
            TextMiddle(s, inside, x + UiTokens.S2 + UiTokens.IconSmall + UiTokens.S1, y, MoneyChangeHeight, tw, UiStyle.BodyStrong, rises ? UiRole.OkText : UiRole.DangerText, words,
                UiFlags.Chrome);
            return w;
        }
    }
}
