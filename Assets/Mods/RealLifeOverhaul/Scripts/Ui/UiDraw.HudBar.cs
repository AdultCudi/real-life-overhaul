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
        public float Time, Weather, Place, Needs, Alerts, Money, Settings;   // px each pill takes; 0 for one left out
        public bool Day, Words, District, MoneyDay;   // the second lines kept: the day, the weather's words, the district, the money's day
        public float Meter;                           // px: each need's cell, its glyph over a meter as wide
        public float MoneyX;                          // px: the money pill's left edge, before the settings at the strip's corner
        public int Apps;                              // the app row's apps before All apps
        public float RowX, RowW;                      // px: the app row's left edge and its width; 0 wide for no row
        public float PromptX, Prompts;                // px: the keys' prompts' left edge and width, before the right pills
        public float Tools;                           // px: the top bar's small buttons' pill after the place (appended with the HUD's screens)
        public float ToolsX;                          // px: that pill's left edge, for a button's own panel to rise over it
    }

    /// <summary>
    /// The status bar's pills, the HUD's readings after Cities: Skylines II's bottom bar, each as tall as the app tiles on the
    /// strip's one centre line with the app row between them (UiDraw.StatusBar). At the left: the time over the day as the
    /// game's top bar writes it, after the game's pause, play and fast forward as three squares (the one in force on the
    /// cyan); the weather's glyph with the season over the weather in words; the place's pin with the nearest building over
    /// its district, as the game's HUD names them. At the right: the player's needs, each a glyph over its short meter in
    /// the need's own colour (the game's energy, hunger and happiness, the mod's hygiene, stress and home); the bell with
    /// its count; the money over the game's figure under it (yesterday's result, its arrow and its colour); and the settings'
    /// gear at the corner, which opens the game's menu. A field left empty leaves its pill out, and every pill that shows is
    /// whole: a strip the UI zoom made narrow gives up, at the left, the district, the weather's words, the weather, the
    /// place and last the day, and at the right the meters' length, the bell, the needs and the money's day, while the app
    /// row first hands apps to All apps, down to four. While the keys lead a window's prompts stand between the row and the
    /// right pills, which give way to them; the HUD sets them in key chips over the bar instead (<see cref="KeyChips"/>).
    /// The squares, the needs, the bell, the money and the gear are targets only where the model says the frame answers
    /// them (the HUD's bar). The money's change is a piece of its own (<see cref="MoneyChange"/>), rising above the bar where
    /// the HUD shows it. Game-free. Cost: a few boxes and measures a layout.
    /// </summary>
    public static partial class UiDraw
    {
        /// <summary>
        /// The pills' targets on the HUD's bar: the time's squares (arg 0 the pause, 1 the play, 2 the fast forward), the money,
        /// the needs, the alerts and the settings.
        /// </summary>
        public const int TimePillCommand = 9020, MoneyPillCommand = 9021, NeedsPillCommand = 9022, AlertsPillCommand = 9023, SettingsPillCommand = 9024;

        /// <summary>The time pill's fast forward square, its arg (0 the pause, 1 the play).</summary>
        public const int FastSquare = 2;

        /// <summary>One of the top bar's small buttons in their pill after the place (arg: its index in UiStatusModel.Tools).</summary>
        public const int ToolPillCommand = 9025;

        /// <summary>px: a pill's height on the bar, the app tiles' own, so every piece stands on the one centre line.</summary>
        public const float BarPill = UiFrame.Toolbar;

        /// <summary>px: a key chip's height (<see cref="KeyChips"/>).</summary>
        public const float KeyChipHeight = UiTokens.Chip;

        private const byte PillRegion = 241;              // the d-pad's column of its own: Left and Right walk the pills
        private const float PillPad = UiTokens.S3;        // px: a pill's room at either end
        private const float PillInner = UiTokens.S3;      // px: between a pill's glyph (or its squares) and its words
        private const float PillGap = UiTokens.S2;        // px: between two pills
        private const float PillGlyph = 28f;              // px: the weather's, the bell's, the money's and the gear's glyph
        private const float PinGlyph = UiTokens.S6;       // px: the place's pin, a step smaller beside its two lines
        private const float PlaceMost = 240f;             // px: the place's words at most: a long address ends at a word
        private const float SquarePill = UiFrame.Toolbar; // px: the bell's and the gear's pill, square
        private const float Square = UiTokens.Chip;       // px: one of the speed's squares
        private const float Squares = Square * 3f + UiTokens.S1 * 2f;
        private const float SquareGlyph = UiTokens.IconSmall;
        private const float NeedCell = 36f;               // px: a need's cell, its glyph over a meter as wide
        private const float NeedTight = 28f;              // px: on a strip the UI zoom made narrow
        private const float NeedGlyph = UiTokens.Icon;
        private const float NeedMeter = UiTokens.Segment; // px: a need's meter's height
        private const float NeedHeight = NeedGlyph + UiTokens.S1 + NeedMeter;   // px: a need's glyph, a step, its meter
        private const float DotRing = UiTokens.Hairline * 2f;   // px: the ring round a count's dot
        private const float ChipInset = (UiTokens.Chip - UiTokens.KeyCap) / 2f;   // px: a key cap's inset in its chip, as from its top
        private const float BarEdge = UiTokens.S4;        // px from the screen's sides to the outer pills
        private const float BarGap = UiTokens.S6;         // px between the row and the pills each side, and before the prompts

        /// <summary>True for a pill's target, which only the HUD answers.</summary>
        public static bool IsPill(int command) => command >= TimePillCommand && command <= ToolPillCommand;

        /// <summary>
        /// Where everything stands on a strip of a width, as <see cref="StatusBar"/> draws it: the row's apps and place, each
        /// pill's width, the money's place, and the room of the keys' prompts (<paramref name="prompts"/> px while they lead).
        /// </summary>
        public static UiPillPlan PlanPills(UiStatusModel m, float w, float prompts = 0f)
        {
            var p = new UiPillPlan { Day = true, Words = true, District = true, MoneyDay = true, Meter = NeedCell, Prompts = prompts };
            if (m == null)
            {
                p.MoneyX = w - BarEdge;
                p.PromptX = w - BarEdge - prompts;
                return p;
            }
            p.Time = TimeWidth(m, true);
            p.Weather = WeatherWidth(m, true);
            p.Place = PlaceWidth(m, true);
            p.Needs = NeedsWidth(m, NeedCell);
            p.Alerts = AlertsWidth(m);
            p.Money = MoneyWidth(m, true);
            // The gear keeps its corner on every bar with money on it, a picture where the bar takes no press, so the money never
            // meets the screen's edge; a bar given nothing has none.
            p.Settings = m.PillsPress || !string.IsNullOrEmpty(m.Money) ? SquarePill : 0f;
            p.Tools = ToolsWidth(m);
            // The row keeps every app it is given until the time with its day, or the right pills at their tightest (or the
            // prompts while the keys lead), would not stand whole beside it; then apps wait under All apps, down to four.
            float least = Math.Max(p.Time + (p.Tools > 0f ? PillGap + p.Tools : 0f), Math.Max(prompts, Pills(NeedsWidth(m, NeedTight), p.Alerts, p.Money, p.Settings)));
            int apps = RowApps(m);
            while (apps > 0 && (Zone(m, w, RowWidth(m, apps)) < 0f || (apps > RowLeast && Zone(m, w, RowWidth(m, apps)) < least)))
            {
                apps--;
            }
            p.Apps = apps;
            p.RowW = RowWidth(m, apps);
            p.RowX = SnapDown((w - p.RowW) / 2f);
            float zone = Zone(m, w, p.RowW);
            FitLeft(m, ref p, zone - (p.Tools > 0f ? p.Tools + PillGap : 0f));   // the buttons keep their pill: the mouse's only way to them
            FitRight(m, ref p, zone);
            p.MoneyX = w - BarEdge - p.Settings - (p.Settings > 0f ? PillGap : 0f) - p.Money;
            float left = Pills(p.Time, p.Weather, p.Place, 0f);
            p.ToolsX = BarEdge + left + (left > 0f ? PillGap : 0f);
            float pills = Pills(p.Needs, p.Alerts, p.Money, p.Settings);
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

        // The left pills within their room: the district goes first where it is the wider of the place's lines (under a
        // longer address it costs nothing and stays), then the weather's words, the weather, the place (the weather back,
        // with its words if they fit, once the place has gone), and last the day; the time stays.
        private static void FitLeft(UiStatusModel m, ref UiPillPlan p, float room)
        {
            if (Pills(p.Time, p.Weather, p.Place, 0f) > room && p.Place > 0f && PlaceWidth(m, false) < p.Place)
            {
                p.District = false;
                p.Place = PlaceWidth(m, false);
            }
            if (Pills(p.Time, p.Weather, p.Place, 0f) > room && p.Weather > 0f)
            {
                p.Words = false;
                p.Weather = WeatherWidth(m, false);
            }
            float weather = p.Weather;
            if (Pills(p.Time, p.Weather, p.Place, 0f) > room)
            {
                p.Weather = 0f;
            }
            if (Pills(p.Time, p.Weather, p.Place, 0f) > room)
            {
                p.Place = 0f;
                p.District = false;
                float whole = WeatherWidth(m, true);
                p.Words = whole > 0f && Pills(p.Time, whole, 0f, 0f) <= room;   // the place gone, the weather back with its words if they fit
                p.Weather = p.Words ? whole : Pills(p.Time, weather, 0f, 0f) <= room ? weather : 0f;
            }
            if (p.Weather == 0f)
            {
                p.Words = false;
            }
            if (p.Place > 0f && !p.District && Pills(p.Time, p.Weather, PlaceWidth(m, true), 0f) <= room)
            {
                p.District = true;   // the weather gone, the district fits again
                p.Place = PlaceWidth(m, true);
            }
            if (Pills(p.Time, p.Weather, p.Place, 0f) > room && p.Day)
            {
                p.Day = false;
                p.Time = TimeWidth(m, false);
            }
        }

        // The right pills within their room, after the prompts while the keys lead: the meters shorten, then the bell goes,
        // then the needs (the bell back if it then fits alone), then the money's day under it; the gear keeps the corner.
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
            if (Right(ref p) > room && p.Money > 0f)
            {
                p.MoneyDay = false;
                p.Money = MoneyWidth(m, false);
            }
            if (Right(ref p) > room)
            {
                p.Money = 0f;
            }
        }

        private static float Right(ref UiPillPlan p)
        {
            float pills = Pills(p.Needs, p.Alerts, p.Money, p.Settings);
            return p.Prompts + (p.Prompts > 0f && pills > 0f ? BarGap : 0f) + pills;
        }

        // Pills side by side, a gap between neighbours; 0 for none.
        private static float Pills(float a, float b, float c, float d)
        {
            int n = (a > 0f ? 1 : 0) + (b > 0f ? 1 : 0) + (c > 0f ? 1 : 0) + (d > 0f ? 1 : 0);
            return a + b + c + d + Math.Max(0, n - 1) * PillGap;
        }

        // A line of words' room in a style, a hair over its width; 0 for none.
        private static float Words(string text, UiStyle style) => string.IsNullOrEmpty(text) ? 0f : Snap(UiTextRules.Width(text, style) + UiTokens.S1);

        // A pill round a glyph and a column of words.
        private static float Round(float glyph, float column) => Snap(PillPad + (glyph > 0f ? glyph + PillInner : 0f) + column + PillPad);

        // ------------------------------------------------------------------------------------------------- the widths

        private static bool HasSpeed(UiStatusModel m) => m.Speed > 0 || m.Paused;

        private static float TimeWidth(UiStatusModel m, bool day) =>
            string.IsNullOrEmpty(m.Time) ? 0f : Round(HasSpeed(m) ? Squares : 0f, Math.Max(Words(m.Time, UiStyle.Number), day ? DayWidth(m) : 0f));

        // The day's line: as wide as the widest day of the week in the player's language, so a new day never moves the pills.
        private static float DayWidth(UiStatusModel m) => string.IsNullOrEmpty(m.Day) ? 0f : Math.Max(Words(m.Day, UiStyle.Small), Words(m.DayWidest, UiStyle.Small));

        // The weather's two lines: a temperature where the game gives one over the season, else the season over the weather in
        // words, or the words alone.
        private static string WeatherTop(UiStatusModel m) =>
            !string.IsNullOrEmpty(m.Temperature) ? m.Temperature : !string.IsNullOrEmpty(m.Season) ? m.Season : m.WeatherWords;

        private static UiStyle WeatherStyle(UiStatusModel m) => !string.IsNullOrEmpty(m.Temperature) ? UiStyle.Number : UiStyle.BodyStrong;

        private static string WeatherUnder(UiStatusModel m) =>
            !string.IsNullOrEmpty(m.Temperature) ? (!string.IsNullOrEmpty(m.Season) ? m.Season : m.WeatherWords) : !string.IsNullOrEmpty(m.Season) ? m.WeatherWords : null;

        private static float WeatherWidth(UiStatusModel m, bool words)
        {
            string top = WeatherTop(m);
            return string.IsNullOrEmpty(top) ? 0f : Round(PillGlyph, Math.Max(Words(top, WeatherStyle(m)), words ? Words(WeatherUnder(m), UiStyle.Small) : 0f));
        }

        private static float PlaceWidth(UiStatusModel m, bool district) => string.IsNullOrEmpty(m.Place) ? 0f
            : Round(PinGlyph, Math.Min(PlaceMost, Math.Max(Words(m.Place, UiStyle.BodyStrong), district ? Words(m.District, UiStyle.Small) : 0f)));

        // The needs' pill: each need a cell, a gap between (half as much on a narrow strip), padded as a pill.
        private static float NeedsWidth(UiStatusModel m, float cell)
        {
            int n = NeedCount(m);
            return n == 0 ? 0f : PillPad * 2f + n * cell + (n - 1) * NeedGap(cell);
        }

        private static float NeedGap(float cell) => cell > NeedTight ? UiTokens.S2 : UiTokens.S1;

        // The top bar's small buttons: one pill of square glyphs, on the HUD's bar alone.
        private static float ToolsWidth(UiStatusModel m) =>
            !m.PillsPress || m.Tools == null || m.ToolCount <= 0 ? 0f : PillPad * 2f + m.ToolCount * ToolButton + (m.ToolCount - 1) * UiTokens.S1;

        private const float ToolButton = UiTokens.Control + UiTokens.S1;   // px: one of the small buttons, square, on the pill's centre line, a grid step from its edges

        /// <summary>px: the middle of the bar's small button <paramref name="index"/>, from the plan's pill, for its panel to rise over it.</summary>
        public static float ToolMiddle(UiPillPlan p, int index) => p.ToolsX + PillPad + index * (ToolButton + UiTokens.S1) + ToolButton / 2f;

        // The small buttons' pill: each glyph a target on its own square, the one whose panel is open on the chosen tone, one
        // the game will not answer now greyed.
        private static void ToolsPill(UiScene s, UiAt at, float x, float y, float w, UiStatusModel m)
        {
            int pill = PillFace(s, at, x, y, w);
            UiAt on = at.In(pill);
            float bx = x + PillPad, by = y + (BarPill - ToolButton) / 2f;
            for (int i = 0; i < m.ToolCount && i < m.Tools.Length; i++)
            {
                bool chosen = m.ToolsOn != null && i < m.ToolsOn.Length && m.ToolsOn[i];
                bool off = m.ToolsOff != null && i < m.ToolsOff.Length && m.ToolsOff[i];
                int first = s.ItemCount;
                int node = IconButton(s, on, bx, by, ToolButton, m.Tools[i], UiRole.Text, ToolPillCommand, i, chosen);
                if (off)
                {
                    Dim(s, first, node);
                }
                bx += ToolButton + UiTokens.S1;
            }
        }

        // The bell: on the HUD's bar always, as the game's own top bar keeps it; elsewhere only while notices wait.
        private static float AlertsWidth(UiStatusModel m) => m.Alerts > 0 || m.PillsPress ? SquarePill : 0f;

        private static float MoneyWidth(UiStatusModel m, bool day) =>
            string.IsNullOrEmpty(m.Money) ? 0f : Round(PillGlyph, Math.Max(Words(m.Money, UiStyle.Number), day ? MoneyDayWidth(m) : 0f));

        private static float MoneyDayWidth(UiStatusModel m) =>
            string.IsNullOrEmpty(m.MoneyDay) ? 0f : (m.MoneyTrend != 0 ? UiTokens.IconSmall + UiTokens.S1 : 0f) + Words(m.MoneyDay, UiStyle.Small);

        // ------------------------------------------------------------------------------------------------- the pills

        // The pills on the strip's centre line: the left group from the left edge, the right group to the right edge with the
        // gear at the corner, each a target where the HUD answers it, linked left to right for the d-pad.
        private static void HudPills(UiScene s, UiAt on, float mid, float w, ref UiPillPlan p, UiStatusModel m)
        {
            float py = mid - BarPill / 2f;
            UiAt row = on.InRegion(PillRegion);
            int first = s.NodeCount;
            float px = BarEdge;
            if (p.Time > 0f)
            {
                TimePill(s, row, px, py, p.Time, m, p.Day);
                px += p.Time + PillGap;
            }
            if (p.Weather > 0f)
            {
                WeatherPill(s, row, px, py, p.Weather, m, p.Words);
                px += p.Weather + PillGap;
            }
            if (p.Place > 0f)
            {
                PlacePill(s, row, px, py, p.Place, m, p.District);
                px += p.Place + PillGap;
            }
            if (p.Tools > 0f)
            {
                ToolsPill(s, row, px, py, p.Tools, m);
            }
            float rx = w - BarEdge - Pills(p.Needs, p.Alerts, p.Money, p.Settings);
            if (p.Needs > 0f)
            {
                NeedsPill(s, row, rx, py, p.Needs, p.Meter, m);
                rx += p.Needs + PillGap;
            }
            if (p.Alerts > 0f)
            {
                BellPill(s, row, rx, py, m);
                rx += p.Alerts + PillGap;
            }
            if (p.Money > 0f)
            {
                MoneyPill(s, row, rx, py, p.Money, m, p.MoneyDay);
                rx += p.Money + PillGap;
            }
            if (p.Settings > 0f)
            {
                SettingsPill(s, row, rx, py, m);
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
                Node(s, at, x, y, w, BarPill, command, 0, face);
            }
        }

        // A pill's face, centred on the bar's centre line rather than on a grid step from the bar's top.
        private static int PillFace(UiScene s, UiAt at, float x, float y, float w) =>
            Box(s, at, x, y, w, BarPill, UiRole.Pill, UiRole.None, UiTokens.RadiusLarge, UiFlags.Chrome | UiFlags.Middle);

        // A glyph centred on the pill's height.
        private static void PillIcon(UiScene s, UiAt at, float x, float y, float size, UiIcon icon) =>
            Icon(s, at, x, y + (BarPill - size) / 2f, size, icon, UiRole.None, UiFlags.Middle | UiFlags.Chrome);

        // Two lines centred on the pill's height: the reading over its quieter words (the day, the weather, the district), or
        // the reading alone, both from x and as wide as the column.
        private static void Column(UiScene s, UiAt at, float x, float y, float w, string top, UiStyle style, string under)
        {
            float th = UiTextRules.LineHeight(style), uh = string.IsNullOrEmpty(under) ? 0f : UiTextRules.LineHeight(UiStyle.Small);
            float ty = y + (BarPill - th - uh) / 2f;
            Text(s, at, x, ty, w, style, UiRole.Text, top, UiFlags.Middle | UiFlags.Truncate | UiFlags.Chrome);
            if (uh > 0f)
            {
                Text(s, at, x, ty + th, w, UiStyle.Small, UiRole.Muted, under, UiFlags.Middle | UiFlags.Truncate | UiFlags.Chrome);
            }
        }

        // The time pill: the game's pause, play and fast forward as squares, the one in force on the cyan, then the time over
        // the day. The pause and the play are targets where the HUD answers them, dim while the game holds the clock; the
        // fast forward is a target there too, pressing the game's own while it offers one (an activity's), dim while it does not.
        private static void TimePill(UiScene s, UiAt at, float x, float y, float w, UiStatusModel m, bool day)
        {
            int face = PillFace(s, at, x, y, w);
            UiAt inside = at.In(face);
            float cx = x + PillPad;
            if (HasSpeed(m))
            {
                int lit = m.Paused ? 0 : m.Speed >= 3 ? 2 : 1;
                float sy = y + (BarPill - Square) / 2f;
                SpeedSquare(s, inside, cx, sy, UiIcon.Pause, lit == 0, 0, m);
                SpeedSquare(s, inside, cx + Square + UiTokens.S1, sy, UiIcon.Play, lit == 1, 1, m);
                SpeedSquare(s, inside, cx + (Square + UiTokens.S1) * 2f, sy, UiIcon.FastForward, lit == 2, 2, m);
                cx += Squares + PillInner;
            }
            Column(s, inside, cx, y, x + w - PillPad - cx, m.Time, UiStyle.Number, day ? m.Day : null);
        }

        // One square of the speed: its glyph in its own colour on slate, or dark on the cyan when it is in force.
        private static void SpeedSquare(UiScene s, UiAt at, float x, float y, UiIcon icon, bool lit, int arg, UiStatusModel m)
        {
            int first = s.ItemCount;
            int face = Box(s, at, x, y, Square, Square, lit ? UiRole.Accent : UiRole.Tile, UiRole.None, UiTokens.RadiusMedium,
                UiFlags.Chrome | UiFlags.Middle | (lit ? UiFlags.Selected : UiFlags.None));
            float g = SquareGlyph;
            Icon(s, at.In(face), x + (Square - g) / 2f, y + (Square - g) / 2f, g, icon, lit ? UiRole.OnAccent : UiRole.None, UiFlags.Middle | UiFlags.Centre | UiFlags.Chrome, true);
            // The fast forward dim while the game offers none is still a target: pressed, it says when the game offers one.
            bool dim = arg == FastSquare ? !lit && !m.FastOffered : m.TimeLocked;
            int node = m.PillsPress ? Node(s, at, x, y, Square, Square, TimePillCommand, arg, face, arg != FastSquare && m.TimeLocked) : -1;
            if (dim)
            {
                Dim(s, first, arg == FastSquare ? -1 : node);
            }
        }

        // The weather: its glyph, the season over the weather in words, as a city builder's bar says the day's sky.
        private static void WeatherPill(UiScene s, UiAt at, float x, float y, float w, UiStatusModel m, bool words)
        {
            int face = PillFace(s, at, x, y, w);
            UiAt inside = at.In(face);
            PillIcon(s, inside, x + PillPad, y, PillGlyph, m.Weather);
            float cx = x + PillPad + PillGlyph + PillInner;
            Column(s, inside, cx, y, x + w - PillPad - cx, WeatherTop(m), WeatherStyle(m), words ? WeatherUnder(m) : null);
        }

        // The place: the pin, the nearest building's address over its district, as the game's HUD names them.
        private static void PlacePill(UiScene s, UiAt at, float x, float y, float w, UiStatusModel m, bool district)
        {
            int face = PillFace(s, at, x, y, w);
            UiAt inside = at.In(face);
            PillIcon(s, inside, x + PillPad, y, PinGlyph, UiIcon.MapPin);
            float cx = x + PillPad + PinGlyph + PillInner;
            Column(s, inside, cx, y, x + w - PillPad - cx, m.Place, UiStyle.BodyStrong, district ? m.District : null);
        }

        private static int NeedCount(UiStatusModel m) =>
            (m.Energy >= 0 ? 1 : 0) + (m.Hunger >= 0 ? 1 : 0) + (m.Happiness >= 0 ? 1 : 0) + (m.Hygiene >= 0 ? 1 : 0) + (m.Stress >= 0 ? 1 : 0) + (m.HomeNeed >= 0 ? 1 : 0);

        // The player's needs in one pill, each a glyph over a meter as wide, both in the need's own colour: the meter says
        // how full. Stress fills as it rises, as the mod's own bar has it.
        private static void NeedsPill(UiScene s, UiAt at, float x, float y, float w, float cell, UiStatusModel m)
        {
            int face = PillFace(s, at, x, y, w);
            UiAt inside = at.In(face);
            float gap = NeedGap(cell), cx = x + PillPad;
            cx = Need(s, inside, cx, y, cell, gap, UiIcon.Lightning, m.Energy);
            cx = Need(s, inside, cx, y, cell, gap, UiIcon.ForkKnife, m.Hunger);
            cx = Need(s, inside, cx, y, cell, gap, UiIcon.SmileyHappy, m.Happiness);
            cx = Need(s, inside, cx, y, cell, gap, UiIcon.Drop, m.Hygiene);
            cx = Need(s, inside, cx, y, cell, gap, UiIcon.Brain, m.Stress);
            Need(s, inside, cx, y, cell, gap, UiIcon.Home, m.HomeNeed);
            PillTarget(s, at, x, y, w, NeedsPillCommand, face, m);
        }

        // A need's cell, centred in the pill: the glyph in its own colour and the meter under it in the same, never a grade's
        // red or green, so the six read apart at a glance and a short meter says what runs low.
        private static float Need(UiScene s, UiAt at, float x, float y, float cell, float gap, UiIcon icon, int value)
        {
            if (value < 0)
            {
                return x;
            }
            float top = y + (BarPill - NeedHeight) / 2f;
            Icon(s, at, x + (cell - NeedGlyph) / 2f, top, NeedGlyph, icon, UiRole.None, UiFlags.Middle | UiFlags.Centre | UiFlags.Chrome);
            Meter(s, at, x, top + NeedGlyph + UiTokens.S1, cell, NeedMeter, value, UiIconTints.Of(icon));
            return x + cell + gap;
        }

        // The bell, square, its notices counted on a dot at its corner; on the HUD's bar it opens the game's own list.
        private static void BellPill(UiScene s, UiAt at, float x, float y, UiStatusModel m)
        {
            int face = PillFace(s, at, x, y, SquarePill);
            UiAt inside = at.In(face);
            PillIcon(s, inside, x + (SquarePill - PillGlyph) / 2f, y, PillGlyph, UiIcon.Bell);
            if (m.Alerts > 0)
            {
                CountDot(s, inside, x + SquarePill - UiTokens.S2, y + UiTokens.S1, m.Alerts, UiRole.Pill);
            }
            PillTarget(s, at, x, y, SquarePill, AlertsPillCommand, face, m);
        }

        // The money in the bar's figures, over the game's own figure under it on its top bar (yesterday's result) with its arrow
        // in good's or bad's colour, as the game keeps the two in view.
        private static void MoneyPill(UiScene s, UiAt at, float x, float y, float w, UiStatusModel m, bool day)
        {
            int face = PillFace(s, at, x, y, w);
            UiAt inside = at.In(face);
            PillIcon(s, inside, x + PillPad, y, PillGlyph, UiIcon.Wallet);
            float cx = x + PillPad + PillGlyph + PillInner, cw = x + w - PillPad - cx;
            bool under = day && !string.IsNullOrEmpty(m.MoneyDay);
            float th = UiTextRules.LineHeight(UiStyle.Number), uh = under ? UiTextRules.LineHeight(UiStyle.Small) : 0f;
            float ty = y + (BarPill - th - uh) / 2f;
            Text(s, inside, cx, ty, cw, UiStyle.Number, UiRole.Text, m.Money, UiFlags.Middle | UiFlags.Chrome);
            if (under)
            {
                float ux = cx, uy = ty + th;
                if (m.MoneyTrend != 0)
                {
                    Icon(s, inside, ux, uy + (uh - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, m.MoneyTrend > 0 ? UiIcon.TrendUp : UiIcon.TrendDown,
                        m.MoneyTrend > 0 ? UiRole.Ok : UiRole.Danger, UiFlags.Middle | UiFlags.Chrome);
                    ux += UiTokens.IconSmall + UiTokens.S1;
                }
                UiRole ink = m.MoneyTrend > 0 ? UiRole.OkText : m.MoneyTrend < 0 ? UiRole.DangerText : UiRole.Muted;
                Text(s, inside, ux, uy, cx + cw - ux, UiStyle.Small, ink, m.MoneyDay, UiFlags.Middle | UiFlags.Truncate | UiFlags.Chrome);
            }
            PillTarget(s, at, x, y, w, MoneyPillCommand, face, m);
        }

        // The settings' gear at the strip's corner, square: it opens the game's menu, as its own top right button does.
        private static void SettingsPill(UiScene s, UiAt at, float x, float y, UiStatusModel m)
        {
            int face = PillFace(s, at, x, y, SquarePill);
            PillIcon(s, at.In(face), x + (SquarePill - PillGlyph) / 2f, y, PillGlyph, UiIcon.Gear);
            PillTarget(s, at, x, y, SquarePill, SettingsPillCommand, face, m);
        }

        private static string Count(int n) => n > 99 ? "99+" : n.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// A count of news on a tile's or a pill's corner (an app's, the bell's): the figure in dark ink on the danger's red, in
        /// a ring of what stands round it so it reads over a tile's corner and on the bar alike. The ring's outer box ends at
        /// <paramref name="right"/> and starts at <paramref name="top"/>; it may stand past its parent's edge. Returns its width.
        /// </summary>
        public static float CountDot(UiScene s, UiAt at, float right, float top, int count, UiRole ring, UiFlags flags = UiFlags.None)
        {
            string words = Count(count);
            float inner = Math.Max(UiTokens.Badge, Snap(UiTextRules.Width(words, UiStyle.Badge) + UiTokens.S1 * 2f));
            float outer = inner + DotRing * 2f, h = UiTokens.Badge + DotRing * 2f, x = right - outer;
            UiFlags f = UiFlags.Overflow | UiFlags.Middle | UiFlags.Centre | UiFlags.Chrome | flags;
            int r = Circle(s, at, x, top, outer, h, ring, f);
            // Dark ink on the red: a white would stand 2.7:1 there, the dark 5.6:1.
            int d = Circle(s, at.In(r), x + DotRing, top + DotRing, inner, UiTokens.Badge, UiRole.Danger, f);
            TextMiddle(s, at.In(d), x + DotRing, top + DotRing, UiTokens.Badge, inner, UiStyle.Badge, UiRole.OnAccent, words, f);
            return outer;
        }

        // ------------------------------------------------------------------------------------------------- the key chips

        /// <summary>The key chips' width for the prompts a scene holds, a grid step between, as <see cref="KeyChips"/> lays them; 0 for none.</summary>
        public static float KeyChipsWidth(UiScene s, IUiKeys keys)
        {
            float w = 0f;
            for (int i = 0; keys != null && i < s.PromptCount; i++)
            {
                w += KeyChipWidth(ref s.Prompts[i], keys) + (i > 0 ? UiTokens.S2 : 0f);
            }
            return w;
        }

        private static float KeyChipWidth(ref UiPrompt p, IUiKeys keys) => Snap(ChipInset + PromptWidth(ref p, keys) + UiTokens.S2);

        /// <summary>
        /// The keys' prompts as small chips in a row ending at <paramref name="right"/>, their top at <paramref name="y"/>: each
        /// the key's cap and its words on the bar's slate, standing over the world as a game's control hints do, never a strip
        /// of their own. The HUD stands them over the bar's right end while the keys lead (UiHud.Bar).
        /// </summary>
        public static void KeyChips(UiScene s, float right, float y, IUiKeys keys)
        {
            float total = KeyChipsWidth(s, keys);
            s.PromptX = right - total;
            s.PromptY = y;
            s.PromptW = total;
            s.PromptH = KeyChipHeight;
            float cx = right, capY = y + ChipInset;
            for (int i = s.PromptCount - 1; keys != null && i >= 0; i--)
            {
                ref UiPrompt p = ref s.Prompts[i];
                float cw = KeyChipWidth(ref p, keys);
                cx -= cw;
                int first = s.ItemCount;
                int chip = Box(s, UiAt.Screen, cx, y, cw, KeyChipHeight, UiRole.StatusBar, UiRole.None, UiTokens.RadiusMedium);
                UiAt on = UiAt.Screen.In(chip);
                float kx = cx + ChipInset;
                kx += KeyCap(s, on, kx, capY, p.Action, keys, UiRole.PromptCap, UiRole.PromptInk);
                if (p.Action == UiAction.TabPrev)
                {
                    kx += UiTokens.S1;
                    kx += KeyCap(s, on, kx, capY, UiAction.TabNext, keys, UiRole.PromptCap, UiRole.PromptInk);
                }
                TextMiddle(s, on, kx + UiTokens.S2, y, KeyChipHeight, UiTextRules.Width(p.Label, UiStyle.Prompt) + UiTokens.S1, UiStyle.Prompt, UiRole.PromptInk, p.Label, UiFlags.Label);
                for (int k = first; k < s.ItemCount; k++)
                {
                    s.Items[k].Flags |= UiFlags.Middle | UiFlags.Prompt | UiFlags.Chrome;
                }
                cx -= UiTokens.S2;
            }
        }

        // ------------------------------------------------------------------------------------------------- the money's change

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
