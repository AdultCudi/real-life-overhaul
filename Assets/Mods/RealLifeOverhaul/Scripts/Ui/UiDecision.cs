using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A consequence of a choice: what changes, said in words, and whether it is good for the player.</summary>
    public struct UiConsequence
    {
        public string Text;
        public int Tone;            // +1 good, -1 bad, 0 neither
    }

    /// <summary>A choice of a decision: its number's key, its name, its consequences and a note (the time it takes).</summary>
    public struct UiDecisionChoice
    {
        public UiAction Key;
        public string Name;
        public UiConsequence[] Lines;
        public string Note;
        public bool Disabled;
    }

    /// <summary>What a decision card shows: who or what it is about, its words, its numbered choices and its buttons.</summary>
    public sealed class UiDecisionModel
    {
        public string Kicker;                       // "Desk session, Lantern Bay Games"
        public string Title;                        // "Weekly team meeting"
        public UiIcon Emblem = UiIcon.People;
        public UiRole EmblemTint = UiRole.Accent;
        public string EmblemLabel;                  // under the emblem: "12 people"
        public string[] Faces = new string[0];      // the people it is about, as a row of portraits under the emblem
        public string Body;
        public UiDecisionChoice[] Choices = new UiDecisionChoice[0];
        public int Selected = -1;
        public UiActionModel[] Buttons = new UiActionModel[0];
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// The decision card, after Frostpunk 2's big moments (design/DIRECTION_CS2.md, section 4.5): centred over the dimmed
    /// world, an emblem or the faces of the people it is about at its left, its kicker and title in capitals, a paragraph of
    /// what is happening, the choices numbered, each with its consequences in coloured lines (green good, amber a cost), and
    /// its buttons centred at its foot. Commands: <see cref="ChoiceCommand"/> (a choice's index) and
    /// <see cref="ButtonCommand"/> (a button's index). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiDecision
    {
        public const int ChoiceCommand = 1, ButtonCommand = 2;
        public const float Width = 1040f;     // px: the card
        private const float Side = 296f;      // px: the emblem's column
        private const float Emblem = 168f;    // px

        public static void Lay(UiScene s, UiDecisionModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("decision", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            UiDraw.Box(s, UiAt.Screen, 0f, 0f, frame.Width, frame.BandY, UiRole.Scrim, UiRole.None, 0f, UiFlags.Decor);   // the world dimmed, the status bar left clear
            float inner = Width - Side - UiTokens.S8 * 2f;
            float h = Height(m, inner, keys);
            float x = UiDraw.SnapDown((frame.Width - Width) / 2f), y = UiDraw.SnapDown((frame.BandY - h) / 2f);
            int card = UiDraw.Box(s, UiAt.Screen, x, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt at = UiAt.Screen.In(card);
            UiDraw.Box(s, at, x, y, Side, h, UiRole.Band, UiRole.None, -UiTokens.RadiusPanel, UiFlags.Decor);
            Emblems(s, at, m, x, y, h);
            float cx = x + Side + UiTokens.S8, cy = y + UiTokens.S8;
            if (!string.IsNullOrEmpty(m.Kicker))
            {
                UiDraw.Text(s, at, cx, cy, inner, UiStyle.SectionHeader, UiRole.Text2, m.Kicker, UiFlags.Label | UiFlags.Truncate);
                cy += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
            }
            UiDraw.Text(s, at, cx, cy, inner, UiStyle.AppTitle, UiRole.AccentText, m.Title, UiFlags.Label | UiFlags.Truncate);
            cy += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.AppTitle)) + UiTokens.S4;
            if (!string.IsNullOrEmpty(m.Body))
            {
                float bh = UiTextRules.WrappedHeight(m.Body, UiStyle.Body, inner, 5);
                UiDraw.Text(s, at, cx, cy, inner, UiStyle.Body, UiRole.Text2, m.Body, UiFlags.Label | UiFlags.Wrap | UiFlags.Middle, 5);
                cy += UiDraw.Snap(bh) + UiTokens.S5;
            }
            for (int i = 0; i < m.Choices.Length; i++)
            {
                cy += Choice(s, at, ref m.Choices[i], i, i == m.Selected, cx, cy, inner, keys) + UiTokens.S2;
            }
            cy += UiTokens.S4;
            float total = 0f;
            for (int i = 0; i < m.Buttons.Length; i++)
            {
                total += UiDraw.ButtonWidth(m.Buttons[i].Kind, m.Buttons[i].Label, m.Buttons[i].Icon, m.Buttons[i].Key, keys) + (i > 0 ? UiTokens.S3 : 0f);
            }
            float bx = cx + UiDraw.SnapDown((inner - total) / 2f);
            for (int i = 0; i < m.Buttons.Length; i++)
            {
                ref UiActionModel b = ref m.Buttons[i];
                float bw = UiDraw.ButtonWidth(b.Kind, b.Label, b.Icon, b.Key, keys);
                UiDraw.Button(s, at, bx, cy, bw, UiTokens.Control, b.Kind, b.Label, b.Icon, b.Key, keys, ButtonCommand, i, b.Disabled);
                bx += bw + UiTokens.S3;
            }
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        /// <summary>The card's height at its content's width.</summary>
        public static float Height(UiDecisionModel m, float inner, IUiKeys keys)
        {
            float h = UiTokens.S8;
            if (!string.IsNullOrEmpty(m.Kicker))
            {
                h += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
            }
            h += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.AppTitle)) + UiTokens.S4;
            if (!string.IsNullOrEmpty(m.Body))
            {
                h += UiDraw.Snap(UiTextRules.WrappedHeight(m.Body, UiStyle.Body, inner, 5)) + UiTokens.S5;
            }
            for (int i = 0; i < m.Choices.Length; i++)
            {
                h += ChoiceHeight(ref m.Choices[i]) + UiTokens.S2;
            }
            h += UiTokens.S4 + UiTokens.Control + UiTokens.S8;
            return Math.Max(UiDraw.Snap(h), Emblem + UiTokens.S16 * 2f);
        }

        private static float ChoiceHeight(ref UiDecisionChoice c) =>
            UiDraw.Snap(UiTokens.S3 * 2f + UiTextRules.LineHeight(UiStyle.BodyStrong) + (c.Lines?.Length ?? 0) * (UiTokens.S1 + UiTextRules.LineHeight(UiStyle.Small)));

        // The emblem in its ring at the head of the side, what it is under it, and the faces of the people it is about.
        private static void Emblems(UiScene s, UiAt at, UiDecisionModel m, float x, float y, float h)
        {
            float ex = x + (Side - Emblem) / 2f, ey = y + UiTokens.S12;
            UiDraw.Circle(s, at, ex - UiTokens.S1, ey - UiTokens.S1, Emblem + UiTokens.S2, Emblem + UiTokens.S2, m.EmblemTint, UiFlags.Decor);
            int disc = UiDraw.Circle(s, at, ex, ey, Emblem, Emblem, UiRole.Header, UiFlags.Decor);
            float g = UiDraw.Snap(Emblem * 0.5f);
            UiDraw.Icon(s, at.In(disc), ex + (Emblem - g) / 2f, ey + (Emblem - g) / 2f, g, m.Emblem, m.EmblemTint, UiFlags.Middle | UiFlags.Centre, true);
            float ly = ey + Emblem + UiTokens.S5;
            if (!string.IsNullOrEmpty(m.EmblemLabel))
            {
                UiDraw.Text(s, at, x + UiTokens.S5, ly, Side - UiTokens.S5 * 2f, UiStyle.SectionHeader, UiRole.Text, m.EmblemLabel, UiFlags.Label | UiFlags.Centre | UiFlags.Truncate);
                ly += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S4;
            }
            int faces = Math.Min(m.Faces.Length, 5);
            const float face = UiTokens.AvatarSmall;
            float overlap = UiTokens.S2;
            float fw = faces * face - (faces - 1) * overlap;
            float fx = x + UiDraw.SnapDown((Side - fw) / 2f);
            for (int i = 0; i < faces; i++)
            {
                int f = UiDraw.Portrait(s, at, fx + i * (face - overlap), ly, face, m.Faces[i], UiColour.HueOf(m.Faces[i]), UiRole.Band);
                s.Items[f].Flags |= UiFlags.Overflow;   // a row of faces tucked over each other, as a group is shown
            }
        }

        // A choice: its number in the cyan, its name, its consequences under it each behind its rise or fall in its colour,
        // its note at the right; chosen, the cyan at a fifth with its bar. Returns its height.
        private static float Choice(UiScene s, UiAt at, ref UiDecisionChoice c, int index, bool chosen, float x, float y, float w, IUiKeys keys)
        {
            float h = ChoiceHeight(ref c);
            int face = UiDraw.Box(s, at, x - UiTokens.S3, y, w + UiTokens.S3 * 2f, h, chosen ? UiRole.RowSel : UiRole.Row, UiRole.None, UiTokens.RadiusLarge,
                (chosen ? UiFlags.Selected : UiFlags.None) | (c.Disabled ? UiFlags.Disabled : UiFlags.None));
            UiAt inside = at.In(face);
            if (chosen)
            {
                UiDraw.Box(s, inside, x - UiTokens.S3, y + UiTokens.S2, UiTokens.SelectBar, h - UiTokens.S4, UiRole.Accent, UiRole.None, UiTokens.SelectBar / 2f);
            }
            float nameH = UiTextRules.LineHeight(UiStyle.BodyStrong);
            float ny = y + UiTokens.S3;
            string number = (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + ".";
            float numW = UiDraw.Snap(UiTextRules.Width("9.", UiStyle.BodyStrong) + UiTokens.S2);
            UiDraw.Text(s, inside, x, ny, numW, UiStyle.BodyStrong, UiRole.AccentText, number, UiFlags.Middle);
            float tx = x + numW, tw = w - numW;
            float noteW = string.IsNullOrEmpty(c.Note) ? 0f : UiDraw.Snap(UiTextRules.Width(c.Note, UiStyle.Small) + UiTokens.S1);
            float noteLead = noteW > 0f ? noteW + UiTokens.IconSmall + UiTokens.S1 + UiTokens.S3 : 0f;
            UiDraw.Text(s, inside, tx, ny, tw - noteLead, UiStyle.BodyStrong, UiRole.Text, c.Name, UiFlags.Label | UiFlags.Truncate | UiFlags.Middle);
            if (noteW > 0f)
            {
                UiRole noteInk = chosen ? UiRole.Text : UiRole.Muted;   // on the chosen card's cyan the quiet grey would not read
                UiDraw.Icon(s, inside, x + w - noteW - UiTokens.IconSmall - UiTokens.S1, ny + (nameH - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Clock, noteInk, UiFlags.Middle);
                UiDraw.Text(s, inside, x + w - noteW, ny + (nameH - UiTextRules.LineHeight(UiStyle.Small)) / 2f, noteW, UiStyle.Small, noteInk, c.Note, UiFlags.Right | UiFlags.Middle);
            }
            float ly = ny + nameH;
            float lh = UiTextRules.LineHeight(UiStyle.Small);
            for (int i = 0; c.Lines != null && i < c.Lines.Length; i++)
            {
                ly += UiTokens.S1;
                UiConsequence line = c.Lines[i];
                UiRole tone = line.Tone > 0 ? UiRole.OkText : line.Tone < 0 ? UiRole.WarnText : UiRole.Text2;
                UiIcon mark = line.Tone > 0 ? UiIcon.TrendUp : line.Tone < 0 ? UiIcon.TrendDown : UiIcon.Info;
                UiDraw.Icon(s, inside, tx, ly + (lh - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, mark, tone, UiFlags.Middle);
                UiDraw.Text(s, inside, tx + UiTokens.IconSmall + UiTokens.S2, ly, tw - UiTokens.IconSmall - UiTokens.S2, UiStyle.Small, tone, line.Text,
                    UiFlags.Label | UiFlags.Truncate | UiFlags.Middle);
                ly += lh;
            }
            UiDraw.Node(s, at, x - UiTokens.S3, y, w + UiTokens.S3 * 2f, h, ChoiceCommand, index, face, c.Disabled, -1, 0, c.Key);
            return h;
        }
    }
}
