using System;
using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    /// <summary>Where the status bar's pills stand on a bar of a width, worked out before any is drawn: which fit and how wide each is.</summary>
    public struct UiPillPlan
    {
        public float Time, Weather, Place, Money, Needs, Alerts;   // px each pill takes; 0 for one left out
        public bool Day, Temperature, District;                     // the day's words, the temperature and the district stay in their pills
        public float Meter;                                         // px of each need's meter
        public float MoneyX;                                        // px: the money pill's left edge
    }

    /// <summary>
    /// The status bar's row of pills, the HUD's readings after Cities: Skylines II's bottom bar: at the left the time with
    /// the game's speed (or the pause in words), the weather with the season, and the place (the nearest building and its
    /// district, as the game's HUD names them); at the right the money, the player's needs as short meters behind their
    /// icons (the game's energy, hunger and happiness, the mod's hygiene, stress and home) and the alerts. A field left
    /// empty leaves its pill out. A bar made narrow by the game's UI zoom gives up the district, then the place, then the
    /// meters' length, the day's words and the temperature, in that order, so every pill that stays is whole. The time,
    /// the money, the needs and the alerts are targets only where the model says the frame answers them (the HUD's bar).
    /// The money's change is a piece of its own (<see cref="MoneyChange"/>), rising above the bar where the HUD shows it.
    /// Game-free. Cost: a few boxes a layout.
    /// </summary>
    public static partial class UiDraw
    {
        /// <summary>The pills' targets on the HUD's bar: the time (the game's pause), the money, the needs and the alerts.</summary>
        public const int TimePillCommand = 9020, MoneyPillCommand = 9021, NeedsPillCommand = 9022, AlertsPillCommand = 9023;

        private const byte PillRegion = 241;      // the d-pad's column of its own: Left and Right walk the pills
        private const float Chevron = 12f;        // px: one of the speed's chevrons
        private const float MeterLong = 48f;      // px: a need's meter when three or fewer share the pill
        private const float MeterShort = 40f;     // px: when the mod's three join the game's
        private const float MeterTight = 28f;     // px: on a bar the UI zoom made narrow
        private const float LeastPlace = 160f;    // px: a place pill cut narrower than this says too little, so it goes

        /// <summary>True for a pill's target, which only the HUD answers.</summary>
        public static bool IsPill(int command) => command >= TimePillCommand && command <= AlertsPillCommand;

        /// <summary>Which pills fit a bar of a width, and how wide each is, as <see cref="StatusBar"/> will draw them.</summary>
        public static UiPillPlan PlanPills(UiStatusModel m, float w)
        {
            var p = new UiPillPlan { Day = true, Temperature = true, District = true };
            int needs = NeedCount(m);
            p.Meter = needs > 3 ? MeterShort : MeterLong;
            float room = w - UiTokens.S4 * 2f;
            Measure(m, ref p);
            // The least needed goes first, until what is left fits whole.
            if (Total(ref p) > room && p.District)
            {
                p.District = false;
                Measure(m, ref p);
            }
            if (Total(ref p) > room && p.Place > 0f)
            {
                float left = room - (Total(ref p) - p.Place);
                p.Place = left >= LeastPlace ? SnapDown(left) : 0f;
            }
            if (Total(ref p) > room && needs > 0)
            {
                p.Meter = MeterTight;
                Measure(m, ref p);
            }
            if (Total(ref p) > room && p.Day)
            {
                p.Day = false;
                Measure(m, ref p);
            }
            if (Total(ref p) > room && p.Temperature)
            {
                p.Temperature = false;
                Measure(m, ref p);
            }
            float right = w - UiTokens.S4 - (p.Alerts > 0f ? p.Alerts + UiTokens.S2 : 0f) - (p.Needs > 0f ? p.Needs + UiTokens.S2 : 0f);
            p.MoneyX = right - p.Money;
            return p;
        }

        // Every pill's width as the plan stands; the place keeps a cut it was given.
        private static void Measure(UiStatusModel m, ref UiPillPlan p)
        {
            float cut = p.Place;
            p.Time = TimeWidth(m, p.Day);
            p.Weather = string.IsNullOrEmpty(m.Season) ? 0f : PillWidth(m.Weather, WeatherWords(m, p.Temperature));
            p.Place = PlaceWidth(m, p.District);
            if (cut > 0f && cut < p.Place)
            {
                p.Place = cut;
            }
            p.Money = string.IsNullOrEmpty(m.Money) ? 0f
                : UiTokens.S2 + UiTokens.Icon + UiTokens.S2 + Snap(UiTextRules.Width(m.Money, UiStyle.Number) + UiTokens.S1) + UiTokens.S3
                  + (m.MoneyTrend != 0 ? UiTokens.IconSmall + UiTokens.S1 : 0f);
            int needs = NeedCount(m);
            p.Needs = needs == 0 ? 0f : Snap(UiTokens.S2 * 2f + needs * (UiTokens.Icon + UiTokens.S1 + p.Meter) + (needs - 1) * UiTokens.S3);
            p.Alerts = m.Alerts > 0 ? PillWidth(UiIcon.Bell, Count(m.Alerts)) : 0f;
        }

        private static float Total(ref UiPillPlan p)
        {
            float sum = p.Time + p.Weather + p.Place + p.Money + p.Needs + p.Alerts;
            int pills = (p.Time > 0f ? 1 : 0) + (p.Weather > 0f ? 1 : 0) + (p.Place > 0f ? 1 : 0) + (p.Money > 0f ? 1 : 0) + (p.Needs > 0f ? 1 : 0) + (p.Alerts > 0f ? 1 : 0);
            // A gap between neighbours, and the wider one between the left group and the right.
            return sum + Math.Max(0, pills - 1) * UiTokens.S2 + UiTokens.S8;
        }

        // The row of pills along the bar's foot, left then right, each a target where the HUD answers it.
        private static void HudPills(UiScene s, UiAt on, float y, float w, UiStatusModel m)
        {
            UiPillPlan p = PlanPills(m, w);
            float py = y + UiFrame.Toolbar + (UiFrame.StatusRow - PillH) / 2f;
            UiAt row = on.InRegion(PillRegion);
            int first = s.NodeCount;
            float px = UiTokens.S4;
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
            float rx = p.MoneyX;
            if (p.Money > 0f)
            {
                MoneyPill(s, row, rx, py, p.Money, m);
                rx += p.Money + UiTokens.S2;
            }
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
            }
            for (int n = first; n < s.NodeCount; n++)
            {
                s.Nodes[n].Left = n > first ? n - 1 : -1;
                s.Nodes[n].Right = n < s.NodeCount - 1 ? n + 1 : -1;
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

        // What follows the time: the pause in words while paused, else the game's speed as chevrons once the game has said it.
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

        // One slot for both, as wide as the wider, so pausing never moves the pills beside it.
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

        // The player's needs as short meters in one pill, each behind its icon in the icon's own colour; the meter says how full
        // and its tone how well (stress fills as it rises, so its tone is read the other way).
        private static void NeedsPill(UiScene s, UiAt at, float x, float y, float w, float meter, UiStatusModel m)
        {
            int face = PillFace(s, at, x, y, w);
            UiAt inside = at.In(face);
            float cx = x + UiTokens.S2;
            // One glyph a need in its own colour: how happy is the meter's to say, never the face's.
            cx = Need(s, inside, cx, y, meter, UiIcon.Lightning, m.Energy, false);
            cx = Need(s, inside, cx, y, meter, UiIcon.ForkKnife, m.Hunger, false);
            cx = Need(s, inside, cx, y, meter, UiIcon.SmileyHappy, m.Happiness, false);
            cx = Need(s, inside, cx, y, meter, UiIcon.Drop, m.Hygiene, false);
            cx = Need(s, inside, cx, y, meter, UiIcon.Brain, m.Stress < 0 ? -1 : 100 - m.Stress, false);   // as calm: every meter in the row reads full as good
            Need(s, inside, cx, y, meter, UiIcon.Home, m.HomeNeed, false);
            PillTarget(s, at, x, y, w, NeedsPillCommand, face, m);
        }

        private static float Need(UiScene s, UiAt at, float x, float y, float meter, UiIcon icon, int value, bool worseHigh)
        {
            if (value < 0)
            {
                return x;
            }
            int well = worseHigh ? 100 - value : value;
            UiRole tone = well >= 60 ? UiRole.Ok : well >= 30 ? UiRole.Warn : UiRole.Danger;
            // The icon keeps its own colour and the meter says how full: a state is told by its bar, never by recolouring a glyph.
            Icon(s, at, x, y + (PillH - UiTokens.Icon) / 2f, UiTokens.Icon, icon, UiRole.None, UiFlags.Middle | UiFlags.Chrome);
            Meter(s, at, x + UiTokens.Icon + UiTokens.S1, y + (PillH - UiTokens.S1) / 2f, meter, UiTokens.S1, value, tone);
            return x + UiTokens.Icon + UiTokens.S1 + meter + UiTokens.S3;
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
