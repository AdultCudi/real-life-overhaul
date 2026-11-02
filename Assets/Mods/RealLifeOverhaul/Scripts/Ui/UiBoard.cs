using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A lane of the board (a workstation, a person, a day): its head's portrait or icon, its words and its colour.</summary>
    public struct UiBoardLane
    {
        public string Name;      // "Register 1", "Maya Okafor"
        public string Sub;       // a quiet line under it ("Cashier", "38 h this week"); null for none
        public UiIcon Icon;      // a workstation's icon on its tile; None for a person, who shows a face
        public float Hue;        // a person's face's hue
        public UiRole Tint;      // the lane's colour: its icon and its shifts' bars
        public string Warning;   // a lane that needs the player ("No one on Saturday"), said with the warning's glyph; null for none
    }

    /// <summary>A shift on the board: its lane, its start and end in minutes since midnight, who works it and its state.</summary>
    public struct UiBoardShift
    {
        public int Lane;
        public int Start, End;   // minutes since midnight
        public string Name;      // the person's name, written on the bar
        public bool Warn;        // overworked, or missing the skill: the warning's glyph on the bar
        public float Hue;        // the person's face's hue, where the board shows faces on its bars
        public UiRole Ring;      // their mood, as every portrait's ring says it
    }

    /// <summary>How a drag takes hold of a shift: its start edge, its end edge or its body.</summary>
    public enum UiBoardGrip : byte
    {
        None = 0,
        Start,
        End,
        Body
    }

    /// <summary>
    /// A board: the day's tabs, the actions on a day (copy, paste, clear, auto fill), the hours the ruler runs over and
    /// the opening hours inside them, the lanes, the shifts, the now line, the chosen shift and how far the lanes have
    /// scrolled. The board only draws: the screen using it sends each edit to the game's schedule or the mod's rota.
    /// <see cref="UiBoard.Lay"/> writes back where it drew (<see cref="LanesTop"/> and the rest), so a pointer's place
    /// turns into an hour and a lane (<see cref="UiBoardRules"/>).
    /// </summary>
    public sealed class UiBoardModel
    {
        public string[] Days = new string[0];          // the tabs: "Mon" to "Sun", in the game's order
        public int Day;
        public UiActionModel[] Actions = new UiActionModel[0];
        public int FirstHour = 6, LastHour = 24;       // the ruler's span, whole hours
        public string[] Hours = new string[0];         // the ruler's words for each hour from FirstHour to LastHour, in the game's form
        public int OpenFrom = -1, OpenTo = -1;         // the opening hours in minutes; outside them the lanes read darker; -1 for none
        public int Step = 60;                          // minutes a shift snaps to (the game's schedule works by the hour)
        public int Shortest = 60, Longest = 720;       // minutes a shift may last (the game caps a shift at 12 hours)
        public UiBoardLane[] Lanes = new UiBoardLane[0];
        public UiBoardShift[] Shifts = new UiBoardShift[0];
        public int Chosen = -1;                        // the shift chosen to move with the keys, filled cyan
        public bool Moving;                            // the chosen shift is being moved or dragged: its hours stand on it
        public string MovingSpan;                      // while moving, the chosen shift's hours in the game's form ("08:00 to 14:00")
        public int Now = -1;                           // minutes since midnight of the now line, or -1 on another day
        public float Offset;                           // px the lanes have scrolled; Lay writes back what it took
        public string EmptyTitle, EmptyBody;           // no lanes yet: what fills the board and how
        public bool[] OpenHours;                       // the opening hours by the hour of the day (0 to 23), for a day of several openings; null: OpenFrom to OpenTo
        public bool HoursPress;                        // each hour of the ruler is a target that opens or closes it (RulerCommand)
        public bool Faces;                             // a bar leads with the person's face where it has the room

        // Written by Lay: where the lanes stand, so a pointer's place reads as a lane and a time.
        public float RulerX, RulerW, LanesTop, LanesHeight, LaneHeight;
    }

    /// <summary>
    /// A board of shifts, a block of an app window's page: the day's tabs with the day's actions beside them, an hour ruler
    /// across the top over the hours given, lanes (workstations, people, days) each led by its face or its icon's tile,
    /// the hours outside the opening hours darker, shifts as flat bars in the lane's colour with the person's name, the
    /// gaps left open, the now line in cyan. The chosen shift is filled cyan with its hours on it while it moves. The
    /// board only draws and reports: a press (<see cref="ShiftCommand"/> a shift, <see cref="LaneCommand"/> a lane,
    /// <see cref="DayCommand"/> a tab, <see cref="ActionCommand"/> an action), and through <see cref="UiBoardRules"/> the
    /// hour and lane a pointer's drag or the keys' steps reach, which the screen using it sends to the game's schedule
    /// (ScheduleHelper) or the mod's rota. Lanes scroll in their frame, only those in view laid out. Game-free.
    /// Cost: a layout a change.
    /// </summary>
    public static class UiBoard
    {
        public const int ShiftCommand = 30, LaneCommand = 31, DayCommand = 32, ActionCommand = 33, RulerCommand = 34;
        public const float LaneHead = 232f;          // px: a lane's face and name, as wide as the window's sidebar
        public const float LaneHeight = 56f;         // px: a lane, a face of 40 with a step above and below
        private const float Stripe = 4f;             // px: a bar's edge in its lane's colour
        private const float BarInset = 8f;           // px: a bar from its lane's edges
        private const byte RegionTabs = 1, RegionLanes = 2;

        [ThreadStatic] private static int _section;

        /// <summary>Lays the board into the room an app window's Shell gave its page, and writes back where its lanes stand.</summary>
        public static void Lay(UiScene s, UiAt at, UiBoardModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            _section = s.FocusFirst;   // the app's section shown, as Shell left it: Left from a lane goes back to it
            int tabs = s.NodeCount;
            float top = Head(s, at.InRegion(RegionTabs), m, x, y, w, keys);
            if (m.Lanes.Length == 0)
            {
                for (int i = tabs; i < s.NodeCount; i++)
                {
                    s.Nodes[i].Down = i;   // nothing under the tabs and the actions: Down stays where it is
                }
            }
            m.RulerX = x + LaneHead;
            m.RulerW = UiDraw.SnapDown(w - LaneHead);
            m.LaneHeight = LaneHeight;
            if (m.Lanes.Length == 0)
            {
                m.LanesTop = top;
                m.LanesHeight = 0f;
                UiDraw.EmptyState(s, at, x, top, w, y + h - top, UiIcon.Calendar, m.EmptyTitle, m.EmptyBody);
                return;
            }
            float hours = Math.Max(1, m.LastHour - m.FirstHour);
            float perHour = m.RulerW / hours;
            Ruler(s, at, m, top, perHour);
            float lanesTop = top + UiTokens.PairRow + UiTokens.S1;
            float vh = Math.Max(1, (int)((y + h - lanesTop) / LaneHeight)) * LaneHeight;   // whole lanes: a lane cut through its face reads as a fault
            m.LanesTop = lanesTop;
            m.LanesHeight = vh;
            Lanes(s, at.InRegion(RegionLanes), m, x, lanesTop, w, vh, perHour);
            if (m.Now >= m.FirstHour * 60 && m.Now <= m.LastHour * 60)
            {
                // The now line over the lanes, from the ruler down, as a game's timeline marks the hour in play.
                float nx = m.RulerX + (m.Now - m.FirstHour * 60) * perHour / 60f;
                UiDraw.Box(s, at, nx - UiTokens.Hairline, lanesTop, UiTokens.Hairline * 2f, vh, UiRole.Accent, UiRole.None, 0f, UiFlags.Decor | UiFlags.Share);
            }
        }

        // The day's tabs at the left (the tab keys step them), the day's actions at the right, the primary last. Returns the y under them.
        private static float Head(UiScene s, UiAt at, UiBoardModel m, float x, float y, float w, IUiKeys keys)
        {
            float right = x + w;
            for (int i = m.Actions.Length - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref m.Actions[i];
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                right -= bw;
                UiDraw.Button(s, at, right, y, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, ActionCommand, i, a.Disabled);
                right -= UiTokens.S3;
            }
            if (m.Days.Length > 0)
            {
                UiDraw.Tabs(s, at, x, y, right - UiTokens.S6 - x, m.Days, m.Day, null, DayCommand);   // the tab keys' caps stand in the status bar while the keys lead
            }
            return y + UiTokens.Control + UiTokens.S4;
        }

        // The hours across the top, each one's words at its line, as the game's schedule heads its columns.
        private static void Ruler(UiScene s, UiAt at, UiBoardModel m, float y, float perHour)
        {
            int hours = Math.Max(1, m.LastHour - m.FirstHour);
            for (int i = 0; i < hours && i < m.Hours.Length; i++)
            {
                float hx = m.RulerX + i * perHour;
                int hour = m.FirstHour + i;
                bool open = Open(m, hour);
                UiAt on = at;
                int face = -1;
                if (m.HoursPress)
                {
                    // An hour that opens or closes as the game's own hour toggles do: a still face for the hover, the open
                    // hours marked by a strip under their words.
                    face = UiDraw.Box(s, at, hx, y, perHour, UiTokens.PairRow, UiRole.None, UiRole.None, UiTokens.RadiusSmall);
                    on = at.In(face);
                }
                UiDraw.Text(s, on, hx + UiTokens.S1, y + (UiTokens.PairRow - UiTextRules.LineHeight(UiStyle.Small)) / 2f, perHour - UiTokens.S1, UiStyle.Small,
                    open || m.OpenHours == null ? UiRole.Text2 : UiRole.Muted, m.Hours[i], UiFlags.Truncate | UiFlags.Share | UiFlags.Middle);
                if (m.OpenHours != null && open)
                {
                    UiDraw.Box(s, on, hx + UiTokens.Hairline, y + UiTokens.PairRow - UiTokens.Hairline * 2f, perHour - UiTokens.Hairline * 2f, UiTokens.Hairline * 2f, UiRole.Ok,
                        UiRole.None, 0f, UiFlags.Decor | UiFlags.Share);
                }
                if (face >= 0)
                {
                    UiDraw.Node(s, at, hx, y, perHour, UiTokens.PairRow, RulerCommand, hour, face);
                }
            }
        }

        // Whether an hour is open: by the hour where the day has several openings, else inside OpenFrom to OpenTo.
        private static bool Open(UiBoardModel m, int hour)
        {
            if (m.OpenHours != null)
            {
                return hour >= 0 && hour < m.OpenHours.Length && m.OpenHours[hour];
            }
            return m.OpenFrom < 0 || (hour * 60 >= m.OpenFrom && hour * 60 < m.OpenTo);
        }

        // The lanes in their frame: the hours' lines, then a lane each, its face and name, its closed hours darker and its shifts.
        private static void Lanes(UiScene s, UiAt at, UiBoardModel m, float x, float y, float w, float vh, float perHour)
        {
            int count = m.Lanes.Length;
            float contentH = count * LaneHeight;
            float offset = Math.Max(0f, Math.Min(m.Offset, contentH - vh));
            m.Offset = offset;
            short clip = s.AddClip(x, y, w, vh, offset);
            s.Clips[clip].ContentHeight = contentH;
            s.Clips[clip].Fills = true;   // a few lanes stand at the board's top; the room under them is the board's own
            UiDraw.ScrollThumb(s, at, x + w + UiTokens.S1, y, vh, offset, contentH);
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            float rx = m.RulerX - x;
            int hours = Math.Max(1, m.LastHour - m.FirstHour);
            float linesH = Math.Max(contentH, vh);
            for (int i = 0; i <= hours; i++)
            {
                UiDraw.Box(s, inClip, rx + i * perHour, 0f, UiTokens.Hairline, linesH, UiRole.Line, UiRole.None, 0f, UiFlags.Decor | UiFlags.Share);
            }
            int first = Math.Max(0, (int)(offset / LaneHeight));
            int last = Math.Min(count - 1, (int)((offset + vh) / LaneHeight));
            short list = s.AddList(count, first, Math.Max(0, last - first + 1), clip, -1);
            for (int lane = first; lane <= last; lane++)
            {
                Lane(s, inClip.Arriving(lane - first), m, lane, list, lane * LaneHeight, w, rx, perHour);
            }
        }

        private static void Lane(UiScene s, UiAt at, UiBoardModel m, int lane, short list, float y, float w, float rx, float perHour)
        {
            ref UiBoardLane l = ref m.Lanes[lane];
            int row = UiDraw.Box(s, at, 0f, y, w, LaneHeight, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(row);
            UiDraw.Box(s, inside, 0f, y + LaneHeight - UiTokens.Hairline, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            float face = UiTokens.AvatarRow;
            if (l.Icon != UiIcon.None)
            {
                float tile = UiTokens.ControlSmall;
                // The icon in its own colour, the same on every screen: the lane's colour is its bars' edge alone.
                int t = UiDraw.Tile(s, inside, UiTokens.S1, y + (LaneHeight - tile) / 2f, tile, UiRole.None, false);
                UiDraw.Icon(s, inside.In(t), UiTokens.S1 + (tile - UiTokens.Icon) / 2f, y + (LaneHeight - UiTokens.Icon) / 2f, UiTokens.Icon, l.Icon, UiRole.None,
                    UiFlags.Middle | UiFlags.Centre, true);
                face = tile;
            }
            else
            {
                UiDraw.Portrait(s, inside, 0f, y + (LaneHeight - face) / 2f, face, l.Name, l.Hue, UiRole.None);
            }
            float tx = (l.Icon != UiIcon.None ? UiTokens.S1 : 0f) + face + UiTokens.S3, tw = rx - UiTokens.S3 - tx;
            bool warns = !string.IsNullOrEmpty(l.Warning);
            string sub = warns ? l.Warning : l.Sub;
            float nameH = UiTextRules.LineHeight(UiStyle.Title), subH = string.IsNullOrEmpty(sub) ? 0f : UiTextRules.LineHeight(UiStyle.Small);
            float top = y + (LaneHeight - nameH - (subH > 0f ? UiTokens.S1 + subH : 0f)) / 2f;
            UiDraw.Text(s, inside, tx, top, tw, UiStyle.Title, UiRole.Text, l.Name, UiFlags.Truncate | UiFlags.Middle);
            if (subH > 0f)
            {
                float sx = tx;
                if (warns)
                {
                    UiDraw.Icon(s, inside, tx, top + nameH + UiTokens.S1 + (subH - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Warning, UiRole.Warn, UiFlags.Middle);
                    sx += UiTokens.IconSmall + UiTokens.S1;
                }
                UiDraw.Text(s, inside, sx, top + nameH + UiTokens.S1, tx + tw - sx, UiStyle.Small, warns ? UiRole.WarnText : UiRole.Text2, sub, UiFlags.Truncate | UiFlags.Middle);
            }
            if (m.OpenHours != null)
            {
                // Each run of closed hours darker, as the game's schedule shades the hours a shop is shut.
                for (int h = m.FirstHour; h < m.LastHour; h++)
                {
                    if (Open(m, h) || (h > m.FirstHour && !Open(m, h - 1)))
                    {
                        continue;
                    }
                    int e = h;
                    while (e < m.LastHour && !Open(m, e))
                    {
                        e++;
                    }
                    Closed(s, inside, m, y, rx, h * 60, e * 60, perHour);
                }
            }
            else if (m.OpenFrom >= 0 && m.OpenTo > m.OpenFrom)
            {
                Closed(s, inside, m, y, rx, m.FirstHour * 60, m.OpenFrom, perHour);
                Closed(s, inside, m, y, rx, m.OpenTo, m.LastHour * 60, perHour);
            }
            int laneNode = UiDraw.Node(s, at, 0f, y, w, LaneHeight, LaneCommand, lane, row, false, list, lane);
            s.Nodes[laneNode].Left = _section;
            int before = -1;
            for (int i = 0; i < m.Shifts.Length; i++)
            {
                if (m.Shifts[i].Lane != lane)
                {
                    continue;
                }
                int bar = Bar(s, inside, m, i, y, rx, perHour);
                if (bar < 0)
                {
                    continue;
                }
                // The d-pad walks a lane's shifts in their order and back to the lane: they lie inside its row, where a
                // search for the nearest target that way would never look.
                if (before < 0)
                {
                    s.Nodes[laneNode].Right = bar;
                    s.Nodes[bar].Left = laneNode;
                }
                else
                {
                    s.Nodes[before].Right = bar;
                    s.Nodes[bar].Left = before;
                }
                before = bar;
            }
            if (before >= 0)
            {
                s.Nodes[before].Right = before;   // the lane's last shift: Right stays in the lane, the day's actions are Up from the first lane
            }
        }

        // Hours outside the opening hours, darker, as a closed shop's hours are on the game's schedule.
        private static void Closed(UiScene s, UiAt at, UiBoardModel m, float y, float rx, int from, int to, float perHour)
        {
            int lo = Math.Max(from, m.FirstHour * 60), hi = Math.Min(to, m.LastHour * 60);
            if (hi <= lo)
            {
                return;
            }
            float bx = rx + (lo - m.FirstHour * 60) * perHour / 60f;
            UiDraw.Box(s, at, bx, y + UiTokens.S1, (hi - lo) * perHour / 60f, LaneHeight - UiTokens.S2, UiRole.Field, UiRole.None, 0f, UiFlags.Share | UiFlags.Decor);
        }

        // A shift's bar: its lane's colour as a tone, the person's name on it, the warning's glyph when it needs the player;
        // chosen, the cyan with dark words and its hours, with its ends' grips while it moves. Returns its target, or -1.
        private static int Bar(UiScene s, UiAt at, UiBoardModel m, int index, float y, float rx, float perHour)
        {
            ref UiBoardShift shift = ref m.Shifts[index];
            int lo = Math.Max(shift.Start, m.FirstHour * 60), hi = Math.Min(shift.End, m.LastHour * 60);
            if (hi <= lo)
            {
                return -1;
            }
            bool chosen = index == m.Chosen;
            float bx = rx + (lo - m.FirstHour * 60) * perHour / 60f, bw = (hi - lo) * perHour / 60f;
            float by = y + BarInset, bh = LaneHeight - BarInset * 2f;
            UiRole tint = m.Lanes[shift.Lane].Tint == UiRole.None ? UiRole.Chart1 : m.Lanes[shift.Lane].Tint;
            // A neutral bar with its lane's colour as its leading edge: a warm colour washed over the slate turns brown.
            int face = UiDraw.Box(s, at, bx, by, bw, bh, chosen ? UiRole.Accent : UiRole.Tile, UiRole.None, UiTokens.RadiusMedium, UiFlags.Share | (chosen ? UiFlags.Selected : UiFlags.None));
            UiAt inside = at.In(face);
            if (!chosen)
            {
                UiDraw.Box(s, inside, bx, by, Stripe, bh, tint, UiRole.None, UiTokens.RadiusSmall, UiFlags.Share | UiFlags.Decor);
            }
            UiRole ink = chosen ? UiRole.OnAccent : UiRole.Text;
            float pad = UiTokens.S2 + Stripe, room = bw - pad - UiTokens.S2;
            float head = UiTokens.IconSmall + UiTokens.S2;   // a face on a bar: the size of the words' line, its ring the person's mood
            if (m.Faces && room >= head + UiTokens.S16)
            {
                UiDraw.Portrait(s, inside, bx + pad, by + (bh - head) / 2f, head, shift.Name, shift.Hue, shift.Ring);
                pad += head + UiTokens.S2;
                room -= head + UiTokens.S2;
            }
            if (shift.Warn && room > UiTokens.IconSmall + UiTokens.S6)
            {
                UiDraw.Icon(s, inside, bx + bw - UiTokens.S2 - UiTokens.IconSmall, by + (bh - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Warning, chosen ? UiRole.OnAccent : UiRole.Warn,
                    UiFlags.Middle | UiFlags.Share);
                room -= UiTokens.IconSmall + UiTokens.S1;
            }
            bool times = chosen && m.Moving && !string.IsNullOrEmpty(m.MovingSpan);
            if (room >= UiTokens.S6)
            {
                float nameH = UiTextRules.LineHeight(UiStyle.SmallStrong), timeH = UiTextRules.LineHeight(UiStyle.Small);
                float top = by + (bh - nameH - (times ? timeH : 0f)) / 2f;
                UiDraw.Text(s, inside, bx + pad, top, room, UiStyle.SmallStrong, ink, shift.Name, UiFlags.Truncate | UiFlags.Share | UiFlags.Middle);
                if (times)
                {
                    UiDraw.Text(s, inside, bx + pad, top + nameH, room, UiStyle.Small, ink, m.MovingSpan, UiFlags.Truncate | UiFlags.Share | UiFlags.Middle);
                }
            }
            if (chosen && m.Moving)
            {
                // The ends a drag or the shoulders take hold of, as thin grips inside the bar.
                float gh = bh - UiTokens.S4;
                UiDraw.Box(s, inside, bx + UiTokens.S1, by + UiTokens.S2, UiTokens.Hairline * 2f, gh, UiRole.OnAccent, UiRole.None, UiTokens.Hairline, UiFlags.Decor | UiFlags.Share);
                UiDraw.Box(s, inside, bx + bw - UiTokens.S1 - UiTokens.Hairline * 2f, by + UiTokens.S2, UiTokens.Hairline * 2f, gh, UiRole.OnAccent, UiRole.None, UiTokens.Hairline,
                    UiFlags.Decor | UiFlags.Share);
            }
            return UiDraw.Node(s, at, bx, by, Math.Max(bw, UiTokens.MinTarget), bh, ShiftCommand, index, face);
        }
    }
}
