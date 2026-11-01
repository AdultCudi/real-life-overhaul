using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a desk session's step shows: the session, how far it has come, the question and its choices, a summary.</summary>
    public sealed class UiSessionModel
    {
        public string Title;
        public string About;
        public string StepLabel;       // "Step 1 of 2"
        public int StepCount;
        public int Step;
        public string Question;
        public UiChoiceModel[] Choices = new UiChoiceModel[0];
        public int ChoiceCount;
        public string SummaryTitle;
        public UiPairModel[] Summary = new UiPairModel[0];
        public int SummaryCount;
        public UiActionModel[] Actions = new UiActionModel[0];   // the step's own buttons (hand to a manager, cancel), the primary last
        public int ActionCount;
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// The session steps template, for a desk session held at the owner's desk (Scripts/Sessions). The session is one
    /// framed card across the page from under the masthead, with its name and what it is, the step progress, the step's
    /// question, its choices as cards (each with the key that picks it and the trade-off it makes, side by side when they
    /// all fit in one row, else one under another) and the step's actions at the foot. Its summary, when it has one, is a
    /// framed panel at the right. Each frame is as tall as what it holds, so it never frames a hole. Commands:
    /// <see cref="ChoiceCommand"/> (the choice's index), <see cref="ActionCommand"/> (the action's). Game-free.
    /// Cost: a layout a change.
    /// </summary>
    public static class UiSessionSteps
    {
        public const int ChoiceCommand = 1, ActionCommand = 2;
        public const float SideWidth = 380f;     // px: as wide as a dashboard's side panel
        public const float MinChoice = 560f;     // px: the narrowest a choice stands beside another, its trade on two lines in every language
        public const byte RegionMain = 1, RegionSide = 2;   // the d-pad's columns

        /// <summary>Lays a session's step out into a scene.</summary>
        public static void Lay(UiScene s, UiSessionModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("session", frame.Width, frame.Height, dark);
            frame.Content(out float cx, out float cy, out float cw, out float ch);
            UiAt screen = UiAt.Screen;
            int bg = UiDraw.Box(s, screen, 0f, 0f, frame.Width, frame.Height, UiRole.Bg, UiRole.None, 0f, UiFlags.Decor);   // the host's whole area
            UiAt at = screen.In(bg);
            bool side = m.SummaryCount > 0;   // a summary panel only where there is a summary: an empty frame reads as a hole
            float pw = side ? cw - SideWidth - UiTokens.S4 : cw;
            UiAt main = at.InRegion(RegionMain);
            int panel = UiDraw.Box(s, main, cx, cy, pw, ch, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt inside = main.In(panel);
            float x = cx + UiTokens.S8, w = pw - UiTokens.S8 * 2f, y = cy + UiTokens.S8;
            UiDraw.Text(s, inside, x, y, w, UiStyle.Hero, UiRole.Text, m.Title, UiFlags.Label | UiFlags.Truncate);
            y += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Hero)) + UiTokens.S2;
            if (!string.IsNullOrEmpty(m.About))
            {
                float aboutH = UiTextRules.WrappedHeight(m.About, UiStyle.Body, w, 2);
                UiDraw.Text(s, inside, x, y, w, UiStyle.Body, UiRole.Text2, m.About, UiFlags.Label | UiFlags.Wrap, 2);
                y += UiDraw.Snap(aboutH);
            }
            y += UiTokens.S6;
            if (m.StepCount > 0)
            {
                y += UiDraw.Snap(UiDraw.Steps(s, inside, x, y, w, m.StepLabel, m.StepCount, m.Step)) + UiTokens.S6;
            }
            if (!string.IsNullOrEmpty(m.Question))
            {
                UiDraw.Text(s, inside, x, y, w, UiStyle.SectionTitle, UiRole.Text, m.Question, UiFlags.Label | UiFlags.Wrap, 2);
                y += UiDraw.Snap(UiTextRules.WrappedHeight(m.Question, UiStyle.SectionTitle, w, 2)) + UiTokens.S4;
            }
            y = Choices(s, inside, m, x, y, w, keys) - UiTokens.S3;
            float ph = UiDraw.Snap(y + (m.ActionCount > 0 ? UiTokens.S8 + UiTokens.Control : 0f) + UiTokens.S8 - cy);
            if (side)
            {
                int summary = Summary(s, at.InRegion(RegionSide), m, cx + cw - SideWidth, cy, ch);
                if (Math.Abs(s.Items[summary].H - ph) <= UiTokens.MostSlack - UiTokens.S8)
                {
                    // Nearly level: one foot reads tidier than a step, while the card's padding over its buttons and the step
                    // together stay within the slack a frame may hold.
                    ph = s.Items[summary].H = Math.Max(s.Items[summary].H, ph);
                }
            }
            s.Items[panel].H = ph;
            // The actions stand on the card's floor, so a card made level with its summary keeps the air over its buttons.
            float by = cy + ph - UiTokens.S8 - UiTokens.Control;
            float bx = x + w;
            for (int i = m.ActionCount - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref m.Actions[i];
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                bx -= bw;
                UiDraw.Button(s, inside, bx, by, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, ActionCommand, i, a.Disabled);
                bx -= UiTokens.S3;
            }
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
            UiDraw.Chrome(s, frame);
        }

        // The choices from a y, side by side and level when they all fit one row at their least width, else one under
        // another. Returns the y under the last, a row's gap included.
        private static float Choices(UiScene s, UiAt at, UiSessionModel m, float x, float y, float w, IUiKeys keys)
        {
            int fit = Math.Max(1, (int)((w + UiTokens.S4) / (MinChoice + UiTokens.S4)));
            int across = m.ChoiceCount <= fit ? Math.Max(1, m.ChoiceCount) : 1;
            float cw = across > 1 ? UiDraw.SnapDown((w - UiTokens.S4 * (across - 1)) / across) : w;
            float level = 0f;
            for (int i = 0; across > 1 && i < m.ChoiceCount; i++)
            {
                level = Math.Max(level, UiDraw.ChoiceCardHeight(ref m.Choices[i], keys, cw));
            }
            for (int i = 0; i < m.ChoiceCount; i++)
            {
                float px = across > 1 ? x + i * (cw + UiTokens.S4) : x;
                UiDraw.ChoiceCard(s, at.Arriving(i), px, y, cw, ref m.Choices[i], keys, ChoiceCommand, i, out float h, level);
                if (across == 1)
                {
                    y += h + UiTokens.S3;
                }
            }
            return across > 1 ? y + level + UiTokens.S3 : y;
        }

        // The summary as its own framed panel, as tall as its lines. Returns the frame.
        private static int Summary(UiScene s, UiAt at, UiSessionModel m, float px, float py, float most)
        {
            int panel = UiDraw.Box(s, at, px, py, SideWidth, most, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt inside = at.In(panel);
            float x = px + UiTokens.S5, w = SideWidth - UiTokens.S5 * 2f, y = py + UiTokens.S5;
            UiDraw.Text(s, inside, x, y, w, UiStyle.SectionHeader, UiRole.Muted, m.SummaryTitle, UiFlags.Label);
            y += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
            for (int i = 0; i < m.SummaryCount; i++)
            {
                y += UiDraw.KeyValue(s, inside, x, y, w, m.Summary[i].Key, m.Summary[i].Value);
            }
            s.Items[panel].H = Math.Min(most, UiDraw.Snap(y - py + UiTokens.S5));
            return panel;
        }
    }
}
