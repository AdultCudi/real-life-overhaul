using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>One line under a report: what is attached, and whether it is included.</summary>
    public struct UiReportCheck
    {
        public string Text;
        public bool Met;
        public string Link;   // a words button at the right, such as adding a screenshot; null for none
    }

    /// <summary>
    /// A short report over the world: two boxes, where it happened, what is attached, and the buttons.
    /// The main button saves a file. Nothing is sent.
    /// </summary>
    public sealed class UiReportModel
    {
        public string Title;
        public string Body;
        public string HappenedLabel;
        public string Happened;
        public string HappenedHint;
        public string ExpectedLabel;
        public string Expected;
        public string ExpectedHint;
        public string WhereLabel;
        public string Where;
        public bool WhereOpen;
        public string[] WhereOptions = new string[0];
        public int WhereChosen;
        public float WhereOffset;
        public int Typing = -1;            // 0 what happened, 1 what was expected, -1 neither (the host says which itself while it types; this is for a sample)
        public UiReportCheck[] Checks = new UiReportCheck[0];
        public UiActionModel[] Buttons = new UiActionModel[0];
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// A report card in the decision card's shape: centred over the dimmed world, a title, two fields, where it
    /// happened, check lines and the buttons. Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiReport
    {
        public const int FieldCommand = 1, WhereCommand = 2, CheckCommand = 3, LinkCommand = 4, ButtonCommand = 5, OptionCommand = 6;
        public const float Width = 880f;
        private const float FieldHeight = 72f;
        private const int FieldLines = 3;       // lines a field shows: the ones the caret is on

        public static void Lay(UiScene s, UiReportModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("report", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            UiDraw.Box(s, UiAt.Screen, 0f, 0f, frame.Width, frame.BandY, UiRole.Scrim, UiRole.None, 0f, UiFlags.Decor);
            float inner = Width - UiTokens.S6 * 2f;
            float h = Height(m, inner, keys);
            float x = UiDraw.SnapDown((frame.Width - Width) / 2f);
            float y = UiDraw.SnapDown((frame.BandY - h) / 2f);
            int card = UiDraw.Box(s, UiAt.Screen, x, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt at = UiAt.Screen.In(card);
            float cx = x + UiTokens.S6;
            float cy = y + UiTokens.S6;
            float titleH = UiDraw.Snap(UiTextRules.WrappedHeight(m.Title, UiStyle.AppTitle, inner, 2));
            UiDraw.Text(s, at, cx, cy, inner, UiStyle.AppTitle, UiRole.AccentText, m.Title, UiFlags.Label | UiFlags.Wrap, 2);
            cy += titleH + UiTokens.S3;
            if (!string.IsNullOrEmpty(m.Body))
            {
                float bh = UiDraw.Snap(UiTextRules.WrappedHeight(m.Body, UiStyle.Body, inner, 4));
                UiDraw.Text(s, at, cx, cy, inner, UiStyle.Body, UiRole.Text2, m.Body, UiFlags.Label | UiFlags.Wrap | UiFlags.Middle, 4);
                cy += bh + UiTokens.S4;
            }
            cy = Field(s, at, m, 0, cx, cy, inner);
            cy = Field(s, at, m, 1, cx, cy, inner);
            cy = Where(s, at, m, cx, cy, inner, keys);
            for (int i = 0; i < m.Checks.Length; i++)
            {
                cy += Check(s, at, ref m.Checks[i], i, cx, cy, inner) + UiTokens.S2;
            }
            cy += UiTokens.S3;
            float bx = x + Width - UiTokens.S6;
            for (int i = m.Buttons.Length - 1; i >= 0; i--)
            {
                ref UiActionModel b = ref m.Buttons[i];
                float bw = UiDraw.ButtonWidth(b.Kind, b.Label, b.Icon, b.Key, keys);
                bx -= bw;
                UiDraw.Button(s, at, bx, cy, bw, UiTokens.Control, b.Kind, b.Label, b.Icon, b.Key, keys, ButtonCommand, i, b.Disabled);
                bx -= UiTokens.S3;
            }
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        /// <summary>The card's height for what it holds.</summary>
        public static float Height(UiReportModel m, float inner, IUiKeys keys)
        {
            float h = UiDraw.Snap(UiTextRules.WrappedHeight(m.Title ?? string.Empty, UiStyle.AppTitle, inner, 2)) + UiTokens.S3;
            if (!string.IsNullOrEmpty(m.Body))
            {
                h += UiDraw.Snap(UiTextRules.WrappedHeight(m.Body, UiStyle.Body, inner, 4)) + UiTokens.S4;
            }
            h += LabelAnd(FieldHeight) * 2f;
            h += LabelAnd(UiTokens.Control);
            for (int i = 0; i < m.Checks.Length; i++)
            {
                h += UiTokens.PairRow + UiTokens.S2;
            }
            h += UiTokens.S3 + UiTokens.Control;
            return UiDraw.Snap(UiTokens.S6 + h + UiTokens.S6);
        }

        private static float LabelAnd(float control) => UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2 + control + UiTokens.S3;

        private static float Field(UiScene s, UiAt at, UiReportModel m, int which, float x, float y, float w)
        {
            string label = which == 0 ? m.HappenedLabel : m.ExpectedLabel;
            string text = which == 0 ? m.Happened : m.Expected;
            string hint = which == 0 ? m.HappenedHint : m.ExpectedHint;
            float labelH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader));
            UiDraw.Text(s, at, x, y, w, UiStyle.SectionHeader, UiRole.Text, label, UiFlags.Label | UiFlags.Truncate);
            y += labelH + UiTokens.S2;
            int face = UiDraw.Box(s, at, x, y, w, FieldHeight, UiRole.Field, UiRole.None, UiTokens.RadiusLarge);
            bool empty = string.IsNullOrEmpty(text);
            bool typing = m.Typing == which || UiDraw.Typing(s, FieldCommand, which);
            float tx = x + UiTokens.S3, room = w - UiTokens.S3 * 2f;
            float lead = typing && empty ? UiDraw.CaretRoom : 0f;   // the caret before the hint, not over its first letter
            float last = 0f;
            string shown = typing && !empty ? UiTextRules.LastLines(text, UiStyle.Body, room - UiDraw.CaretRoom, FieldLines, out last) : empty ? hint : text;   // typed: the lines the caret is on
            UiDraw.Text(s, at.In(face), tx + lead, y + UiTokens.S2, room - lead, UiStyle.Body, empty ? UiRole.Muted : UiRole.Text,
                shown, UiFlags.Wrap | UiFlags.Truncate | (empty ? UiFlags.Label : UiFlags.None), FieldLines);
            if (typing)
            {
                int row = empty ? 0 : Math.Max(0, UiTextRules.Lines(shown, UiStyle.Body, room - UiDraw.CaretRoom, FieldLines, out float _) - 1);
                UiDraw.Caret(s, at.In(face), tx + (empty ? 0f : last + 1f), y + UiTokens.S2 + row * UiTextRules.LineHeight(UiStyle.Body), UiTextRules.LineHeight(UiStyle.Body), UiStyle.Body);
            }
            int node = UiDraw.Node(s, at, x, y, w, FieldHeight, FieldCommand, which, face);
            if (which == 0)
            {
                s.FocusFirst = node;
            }
            return y + FieldHeight + UiTokens.S3;
        }

        private static float Where(UiScene s, UiAt at, UiReportModel m, float x, float y, float w, IUiKeys keys)
        {
            float labelH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader));
            UiDraw.Text(s, at, x, y, w, UiStyle.SectionHeader, UiRole.Text, m.WhereLabel, UiFlags.Label | UiFlags.Truncate);
            y += labelH + UiTokens.S2;
            UiDraw.Choice(s, at, x, y, w, m.Where, WhereCommand, 0, false);
            if (m.WhereOpen && m.WhereOptions != null && m.WhereOptions.Length > 0)
            {
                UiDraw.ChoiceList(s, at, x, y + UiTokens.Control + UiTokens.S1, w, m.WhereOptions, m.WhereOptions.Length, m.WhereChosen, m.WhereOffset, OptionCommand);
            }
            return y + UiTokens.Control + UiTokens.S3;
        }

        private static float Check(UiScene s, UiAt at, ref UiReportCheck check, int index, float x, float y, float w)
        {
            float h = UiTokens.PairRow;
            float linkW = 0f;
            if (!string.IsNullOrEmpty(check.Link))
            {
                linkW = UiDraw.Snap(UiTextRules.Width(check.Link, UiStyle.Body) + UiTokens.S6);
                UiDraw.Button(s, at, x + w - linkW, y + (h - UiTokens.ControlSmall) / 2f, linkW, UiTokens.ControlSmall, UiButtonKind.Secondary, check.Link,
                    UiIcon.None, UiAction.Count, null, LinkCommand, index, false);
            }
            UiDraw.CheckLine(s, at, x, y, w - linkW - (linkW > 0f ? UiTokens.S3 : 0f), check.Text, check.Met);
            return h;
        }
    }
}
