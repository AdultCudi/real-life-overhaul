using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What the time skip veil says: the clock as it runs, the day, the goal, how far along, and whether it can be cancelled.</summary>
    public sealed class UiTimeSkipModel
    {
        public string Title;            // "Skipping time": said when there is no goal line
        public string Clock;            // "03:40", as the game's time machine counts it
        public string Day;              // "Wednesday (Day 413)", as the game's top bar writes it
        public string Goal;             // "Until 07:00, when your shift starts", or "Skipping to the end of your shift at"
        public string GoalTime;         // "17:00": in the strong face after the goal's words, when the goal ends on a time
        public string Pace;             // "Fast forward x60, 2 h 40 min to go"
        public float Share;             // 0 to 1 of the way to the goal
        public bool CanCancel = true;   // the game's own time machine may run without a way out
        public string CancelLabel;
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// The time skip veil, the game's TimeMachine on the kit: the world dimmed (no blur: the game's blur stays off under the
    /// kit), and at its centre the clock large inside a thin ring that fills toward the goal, the day under the clock, the
    /// goal's words with its time in the strong face, the pace and what is left, and Cancel on Back; the status bar stays
    /// in view under it. Commands: <see cref="CancelCommand"/>. Game-free. Cost: a layout a few times a second at most
    /// while the clock runs.
    /// </summary>
    public static class UiTimeSkip
    {
        public const int CancelCommand = 250;
        public const float Width = 440f;    // px: the words' column under the ring
        public const float Ring = 336f;     // px: the ring round the clock

        public static void Lay(UiScene s, UiTimeSkipModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("time skip", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            int scrim = UiDraw.Box(s, UiAt.Screen, 0f, 0f, frame.Width, frame.BandY, UiRole.Scrim, UiRole.None, 0f, UiFlags.Decor);
            UiAt on = UiAt.Screen.In(scrim);
            float h = Height(m);
            float top = UiDraw.SnapDown((frame.BandY - h) / 2f);
            float rx = UiDraw.SnapDown((frame.Width - Ring) / 2f);
            UiDraw.Ring(s, on, rx, top, Ring, m.Share);
            // The clock and the day held at the ring's middle.
            float ch = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Clock)), dh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body));
            float block = ch + UiTokens.S2 + dh, cy = top + UiDraw.SnapDown((Ring - block) / 2f);
            float iw = Ring - UiTokens.S12 * 2f, ix = rx + UiTokens.S12;
            UiDraw.Text(s, on, ix, cy, iw, UiStyle.Clock, UiRole.Text, m.Clock, UiFlags.Centre);
            UiDraw.Text(s, on, ix, cy + ch + UiTokens.S2, iw, UiStyle.Body, UiRole.Text2, m.Day, UiFlags.Centre | UiFlags.Truncate);
            float y = top + Ring + UiTokens.S8;
            float wx = UiDraw.SnapDown((frame.Width - Width) / 2f);
            string goal = !string.IsNullOrEmpty(m.Goal) ? m.Goal : m.Title;
            if (!string.IsNullOrEmpty(goal) || !string.IsNullOrEmpty(m.GoalTime))
            {
                Goal(s, on, goal, m.GoalTime, wx, y, frame.Width);
            }
            y += dh;
            if (!string.IsNullOrEmpty(m.Pace))
            {
                y += UiTokens.S1;
                UiDraw.Text(s, on, wx, y, Width, UiStyle.Small, UiRole.Text2, m.Pace, UiFlags.Centre | UiFlags.Truncate);
                y += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            }
            if (m.CanCancel)
            {
                y += UiTokens.S6;
                float bw = UiDraw.ButtonWidth(UiButtonKind.Secondary, m.CancelLabel, UiIcon.None, UiAction.Back, keys);
                UiDraw.Button(s, on, UiDraw.SnapDown((frame.Width - bw) / 2f), y, bw, UiTokens.Control, UiButtonKind.Secondary, m.CancelLabel, UiIcon.None, UiAction.Back, keys,
                    CancelCommand, 0);
            }
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        // The goal's words with its time after them in the strong face, the two centred as one line.
        private static void Goal(UiScene s, UiAt at, string words, string time, float x, float y, float frameWidth)
        {
            float ww = string.IsNullOrEmpty(words) ? 0f : UiDraw.Snap(UiTextRules.Width(words, UiStyle.Body) + UiTokens.S1);
            float tw = string.IsNullOrEmpty(time) ? 0f : UiDraw.Snap(UiTextRules.Width(time, UiStyle.BodyStrong) + UiTokens.S1);
            float gap = 0f;   // the words' own room after them is the space before the time
            if (ww + gap + tw > Width)
            {
                ww = Width - gap - tw;   // a long goal gives way, its time stays whole
            }
            float gx = Math.Max(x, UiDraw.SnapDown((frameWidth - (ww + gap + tw)) / 2f));
            if (ww > 0f)
            {
                UiDraw.Text(s, at, gx, y, ww, UiStyle.Body, UiRole.Text, words, UiFlags.Truncate);
            }
            if (tw > 0f)
            {
                UiDraw.Text(s, at, gx + ww + gap, y, tw, UiStyle.BodyStrong, UiRole.Text, time, UiFlags.None);
            }
        }

        /// <summary>The veil's height: the ring, the goal, the pace and Cancel under it.</summary>
        public static float Height(UiTimeSkipModel m)
        {
            float h = Ring + UiTokens.S8 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body));
            if (!string.IsNullOrEmpty(m.Pace))
            {
                h += UiTokens.S1 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            }
            if (m.CanCancel)
            {
                h += UiTokens.S6 + UiTokens.Control;
            }
            return UiDraw.Snap(h);
        }
    }
}
