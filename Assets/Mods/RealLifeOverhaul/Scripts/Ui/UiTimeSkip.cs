using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What the time skip veil says: the clock as it runs, the day, the goal, how far along, and whether it can be cancelled.</summary>
    public sealed class UiTimeSkipModel
    {
        public string Title;            // "Skipping time"
        public string Clock;            // "03:40", as the game's time machine counts it
        public string Day;              // "Wed, Day 413"
        public string Goal;             // "Until 07:00, when your shift starts"
        public float Share;             // 0 to 1 of the way to the goal
        public bool CanCancel = true;   // the game's own time machine may run without a way out
        public string CancelLabel;
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// The time skip veil, the game's TimeMachine on the kit: the world dimmed (no blur: the game's blur stays off under the
    /// kit), and a card at the centre with the clock in the number style counting on, the day, the goal, a meter of the way
    /// there, and Cancel on Back. Commands: <see cref="CancelCommand"/> and the strip's close (Back). Game-free. Cost: a
    /// layout each time the clock's minute changes.
    /// </summary>
    public static class UiTimeSkip
    {
        public const int CancelCommand = 250;
        public const float Width = 440f;   // px: the card

        public static void Lay(UiScene s, UiTimeSkipModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("time skip", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            UiDraw.Scrim(s, 0f, 0f, frame.Width, frame.BandY, 0);
            float h = Height(m);
            float x = UiDraw.SnapDown((frame.Width - Width) / 2f), y = UiDraw.SnapDown((frame.BandY - h) / 2f);
            int panel = UiDraw.Box(s, UiAt.Screen, x, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = UiAt.Screen.In(panel);
            float cy = UiDraw.TitleStrip(s, on, x, y, Width, UiIcon.Clock, m.Title, m.CanCancel);
            float ix = x + UiTokens.S6, iw = Width - UiTokens.S6 * 2f;
            cy += UiTokens.S5;
            float ch = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display));
            UiDraw.Text(s, on, ix, cy, iw, UiStyle.Display, UiRole.Text, m.Clock, UiFlags.Centre);
            cy += ch + UiTokens.S1;
            float lh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body));
            UiDraw.Text(s, on, ix, cy, iw, UiStyle.Body, UiRole.Text2, m.Day, UiFlags.Centre | UiFlags.Truncate);
            cy += lh + UiTokens.S4;
            var bar = new UiProgressModel { Label = m.Goal, Share = m.Share };
            cy += UiDraw.Progress(s, on, ix, cy, iw, ref bar);
            if (m.CanCancel)
            {
                cy += UiTokens.S5;
                float bw = UiDraw.Snap(Math.Max(160f, UiDraw.ButtonWidth(UiButtonKind.Secondary, m.CancelLabel, UiIcon.None, UiAction.Back, keys)));
                UiDraw.Button(s, on, x + UiDraw.SnapDown((Width - bw) / 2f), cy, bw, UiTokens.Control, UiButtonKind.Secondary, m.CancelLabel, UiIcon.None, UiAction.Back, keys,
                    CancelCommand, 0);
            }
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        public static float Height(UiTimeSkipModel m)
        {
            float h = UiFrame.Strip + UiTokens.S5 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display)) + UiTokens.S1 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body)) + UiTokens.S4;
            var bar = new UiProgressModel { Label = m.Goal };
            h += UiDraw.ProgressHeight(ref bar);
            if (m.CanCancel)
            {
                h += UiTokens.S5 + UiTokens.Control;
            }
            return UiDraw.Snap(h + UiTokens.S6);
        }
    }
}
