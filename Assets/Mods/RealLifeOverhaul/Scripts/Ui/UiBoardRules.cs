using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The board's edits as sums, game-free: where a pointer stands on it (a lane, a minute, a shift's end or body), what a
    /// drag of a shift's end or body or a stroke over an empty lane makes of it, what a key's step makes of the chosen
    /// shift, and whether a drag resting at the lanes' edge scrolls them. Every time snaps to the board's step and keeps
    /// to its ruler and to a shift's shortest and longest; the overlap of shifts and who may work where stay the game's
    /// (its schedule moves a shift off another as it does for its own drag, WorkShiftDrag.AdjustHoursForOverlap). Reads
    /// what <see cref="UiBoard.Lay"/> wrote back. EconomyCheck runs every rule. Cost: a few sums a call.
    /// </summary>
    public static class UiBoardRules
    {
        public const float GripZone = 12f;   // px from a bar's end that take hold of the end rather than the body
        public const float EdgeZone = 32f;   // px at the lanes' top and foot where a resting drag scrolls them

        /// <summary>Px of the ruler a minute takes.</summary>
        public static float PerMinute(UiBoardModel m) => m.RulerW / (Math.Max(1, m.LastHour - m.FirstHour) * 60f);

        /// <summary>The minute under a place across the ruler, kept to the ruler's span.</summary>
        public static int MinuteAt(UiBoardModel m, float x)
        {
            float per = PerMinute(m);
            int minute = m.FirstHour * 60 + (int)Math.Round(per > 0f ? (x - m.RulerX) / per : 0f);
            return Math.Max(m.FirstHour * 60, Math.Min(m.LastHour * 60, minute));
        }

        /// <summary>The lane under a place down the lanes (their scroll taken), or -1 outside them.</summary>
        public static int LaneAt(UiBoardModel m, float y)
        {
            if (m.LaneHeight <= 0f || y < m.LanesTop || y >= m.LanesTop + m.LanesHeight)
            {
                return -1;
            }
            int lane = (int)((y - m.LanesTop + m.Offset) / m.LaneHeight);
            return lane >= 0 && lane < m.Lanes.Length ? lane : -1;
        }

        /// <summary>A time snapped to the board's step, the nearest.</summary>
        public static int Snap(UiBoardModel m, int minutes)
        {
            int step = Math.Max(1, m.Step);
            return (int)Math.Round(minutes / (double)step, MidpointRounding.AwayFromZero) * step;
        }

        /// <summary>What a pointer at a place across a shift takes hold of: its start's end, its end's, or its body.</summary>
        public static UiBoardGrip Grip(UiBoardModel m, int shift, float x)
        {
            if (shift < 0 || shift >= m.Shifts.Length)
            {
                return UiBoardGrip.None;
            }
            float per = PerMinute(m);
            float left = m.RulerX + (m.Shifts[shift].Start - m.FirstHour * 60) * per, right = m.RulerX + (m.Shifts[shift].End - m.FirstHour * 60) * per;
            float zone = Math.Min(GripZone, (right - left) / 3f);   // a short bar keeps a body to move by
            return x <= left + zone ? UiBoardGrip.Start : x >= right - zone ? UiBoardGrip.End : UiBoardGrip.Body;
        }

        /// <summary>
        /// A shift dragged from one place to another by a grip: the body moves it along the ruler and onto the lane under the
        /// pointer, keeping its length; an end moves that end, keeping the shift within its shortest and longest.
        /// </summary>
        public static void Drag(UiBoardModel m, int shift, UiBoardGrip grip, float fromX, float fromY, float x, float y, out int lane, out int start, out int end)
        {
            UiBoardShift s = m.Shifts[shift];
            lane = s.Lane;
            start = s.Start;
            end = s.End;
            float per = PerMinute(m);
            int moved = per > 0f ? Snap(m, (int)Math.Round((x - fromX) / per)) : 0;
            int first = m.FirstHour * 60, last = m.LastHour * 60;
            switch (grip)
            {
                case UiBoardGrip.Body:
                    int length = s.End - s.Start;
                    start = Math.Max(first, Math.Min(last - length, s.Start + moved));
                    end = start + length;
                    int under = LaneAt(m, y);
                    lane = under >= 0 ? under : s.Lane;
                    break;
                case UiBoardGrip.Start:
                    start = Clamp(Snap(m, s.Start + moved), Math.Max(first, s.End - m.Longest), s.End - m.Shortest);
                    break;
                case UiBoardGrip.End:
                    end = Clamp(Snap(m, s.End + moved), s.Start + m.Shortest, Math.Min(last, s.Start + m.Longest));
                    break;
            }
        }

        /// <summary>A new shift stroked over an empty lane from one place to another: its hours, snapped outwards, at least its shortest and at most its longest.</summary>
        public static void Paint(UiBoardModel m, float fromX, float x, out int start, out int end)
        {
            int a = MinuteAt(m, Math.Min(fromX, x)), b = MinuteAt(m, Math.Max(fromX, x));
            int step = Math.Max(1, m.Step);
            start = a / step * step;
            end = (b + step - 1) / step * step;
            int first = m.FirstHour * 60, last = m.LastHour * 60;
            end = Math.Max(end, start + m.Shortest);
            end = Math.Min(end, Math.Min(last, start + m.Longest));
            start = Math.Max(first, Math.Min(start, end - m.Shortest));
        }

        /// <summary>
        /// The chosen shift moved by a key's step: across by the board's step (or its end resized, Shift held or a shoulder),
        /// down onto the lane below or up onto the one above.
        /// </summary>
        public static void Nudge(UiBoardModel m, int shift, int dx, int dy, bool resize, out int lane, out int start, out int end)
        {
            UiBoardShift s = m.Shifts[shift];
            int step = Math.Max(1, m.Step);
            int first = m.FirstHour * 60, last = m.LastHour * 60;
            lane = Math.Max(0, Math.Min(m.Lanes.Length - 1, s.Lane + dy));
            start = s.Start;
            end = s.End;
            if (resize)
            {
                end = Clamp(s.End + dx * step, s.Start + m.Shortest, Math.Min(last, s.Start + m.Longest));
            }
            else
            {
                int length = s.End - s.Start;
                start = Math.Max(first, Math.Min(last - length, s.Start + dx * step));
                end = start + length;
            }
        }

        /// <summary>Whether a drag resting at a place down the board scrolls its lanes: -1 up, 1 down, 0 still (or nothing more that way).</summary>
        public static int EdgeScroll(UiBoardModel m, float y)
        {
            float most = Math.Max(0f, m.Lanes.Length * m.LaneHeight - m.LanesHeight);
            if (y < m.LanesTop + EdgeZone && y >= m.LanesTop - EdgeZone && m.Offset > 0f)
            {
                return -1;
            }
            return y > m.LanesTop + m.LanesHeight - EdgeZone && y <= m.LanesTop + m.LanesHeight + EdgeZone && m.Offset < most ? 1 : 0;
        }

        /// <summary>The scroll that shows a lane whole, from the scroll now.</summary>
        public static float Reveal(UiBoardModel m, int lane)
        {
            float top = lane * m.LaneHeight, offset = m.Offset;
            offset = Math.Min(offset, top);
            offset = Math.Max(offset, top + m.LaneHeight - m.LanesHeight);
            return Math.Max(0f, Math.Min(offset, Math.Max(0f, m.Lanes.Length * m.LaneHeight - m.LanesHeight)));
        }

        private static int Clamp(int v, int lo, int hi) => hi < lo ? lo : v < lo ? lo : v > hi ? hi : v;
    }
}
