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
    /// The session steps template, for a desk session held at the owner's desk (Scripts/Sessions): one framed panel at the
    /// screen's centre with the session's name and what it is, the step progress, the step's question and its choices as
    /// cards (each with the key that picks it and the trade-off it makes), a summary at the side and the step's actions at
    /// the foot. Commands: <see cref="ChoiceCommand"/> (the choice's index), <see cref="ActionCommand"/> (the action's).
    /// Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiSessionSteps
    {
        public const int ChoiceCommand = 1, ActionCommand = 2;
        public const float PanelWidth = 1240f;   // px: a session reads as one card, not a page
        public const float SideWidth = 360f;     // px

        /// <summary>Lays a session's step out into a scene.</summary>
        public static void Lay(UiScene s, UiSessionModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("session", frame.Width, frame.Height + frame.Band, dark);
            frame.Content(out float cx, out float cy, out float cw, out float ch);
            UiAt screen = UiAt.Screen;
            int bg = UiDraw.Box(s, screen, 0f, 0f, frame.Width, frame.Height, UiRole.Bg, UiRole.None, 0f, UiFlags.Decor);   // the host's whole area
            UiAt at = screen.In(bg);
            float pw = Math.Min(cw, PanelWidth);
            float px = cx + UiDraw.SnapDown((cw - pw) / 2f);
            int panel = UiDraw.Box(s, at, px, cy, pw, ch, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel);
            UiAt inside = at.In(panel);
            UiDraw.Wash(s, inside, px, cy, pw * 0.5f, UiTokens.S16 * 3f);
            float x = px + UiTokens.S8, w = pw - UiTokens.S8 * 2f, y = cy + UiTokens.S8;
            UiDraw.Text(s, inside, x, y, w, UiStyle.Hero, UiRole.Text, m.Title, UiFlags.Label | UiFlags.Truncate);
            y += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Hero)) + UiTokens.S2;
            float aboutH = UiTextRules.WrappedHeight(m.About, UiStyle.Body, w, 2);
            UiDraw.Text(s, inside, x, y, w, UiStyle.Body, UiRole.Text2, m.About, UiFlags.Label | UiFlags.Wrap, 2);
            y += UiDraw.Snap(aboutH) + UiTokens.S6;
            y += UiDraw.Snap(UiDraw.Steps(s, inside, x, y, w, m.StepLabel, m.StepCount, m.Step)) + UiTokens.S6;
            float main = w - SideWidth - UiTokens.S8;
            float qy = y;
            UiDraw.Text(s, inside, x, qy, main, UiStyle.SectionTitle, UiRole.Text, m.Question, UiFlags.Label | UiFlags.Wrap, 2);
            qy += UiDraw.Snap(UiTextRules.WrappedHeight(m.Question, UiStyle.SectionTitle, main, 2)) + UiTokens.S4;
            for (int i = 0; i < m.ChoiceCount; i++)
            {
                UiDraw.ChoiceCard(s, inside.Arriving(i), x, qy, main, ref m.Choices[i], keys, ChoiceCommand, i, out float h);
                qy += h + UiTokens.S3;
            }
            float sx = x + main + UiTokens.S8;
            int summary = UiDraw.Box(s, inside, sx, y, SideWidth, UiDraw.SnapDown(qy - y - UiTokens.S3), UiRole.Field, UiRole.RowEdge, UiTokens.RadiusLarge);
            UiAt inSummary = inside.In(summary);
            float sy = y + UiTokens.S5, sw = SideWidth - UiTokens.S5 * 2f;
            UiDraw.Text(s, inSummary, sx + UiTokens.S5, sy, sw, UiStyle.SectionHeader, UiRole.Muted, m.SummaryTitle, UiFlags.Label);
            sy += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
            for (int i = 0; i < m.SummaryCount && sy + UiTokens.PairRow <= y + (qy - y - UiTokens.S3) - UiTokens.S4; i++)
            {
                sy += UiDraw.KeyValue(s, inSummary, sx + UiTokens.S5, sy, sw, m.Summary[i].Key, m.Summary[i].Value);
            }
            float by = cy + ch - UiTokens.S8 - UiTokens.Control;
            float bx = px + pw - UiTokens.S8;
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
            UiDraw.PromptBar(s, 0f, frame.Height, frame.Width, frame.Band, keys);
        }
    }
}
