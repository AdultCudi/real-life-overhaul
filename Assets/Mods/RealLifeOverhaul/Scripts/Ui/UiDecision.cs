using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A consequence of a choice: what changes, said in words, which way it goes, and whether that is good for the player.</summary>
    public struct UiConsequence
    {
        /// <summary>The tone of money the player pays out (a price, a fine, a fee): red, behind the falling arrow.</summary>
        public const int Loss = -2;

        public string Text;
        public int Tone;            // +1 good for the player, -1 against them (a risk, a reason), Loss money paid out, 0 neither: its colour
        public int Change;          // +1 the thing rises, -1 it falls, 0 no way: its arrow, so burnout easing is a green arrow down
    }

    /// <summary>A choice of a decision: its number's key, its name, its consequences and a note (the time it takes).</summary>
    public struct UiDecisionChoice
    {
        public UiAction Key;
        public string Name;
        public UiConsequence[] Lines;
        public string Note;
        public bool Disabled;
        public UiChoiceCap Cap;     // the bound key at the right of its name (F, P, the way back); None for its number alone
    }

    /// <summary>An amount the player moves before choosing (a counter-offer): its name, the figure between a minus and a plus, what the figure means under it.</summary>
    public struct UiDecisionStep
    {
        public string Label, Value, Hint;
        public bool CanLess, CanMore;
        public int Less, More, Arg;     // the commands of the minus and the plus, and the argument both carry
    }

    /// <summary>What a decision card shows: who or what it is about, its words, its numbered choices and its buttons.</summary>
    public sealed class UiDecisionModel
    {
        public string Kicker;                       // "Desk session, Lantern Bay Games"
        public string Title;                        // "Weekly team meeting"
        public UiIcon Emblem = UiIcon.People;
        public UiRole EmblemTint = UiRole.Accent;
        public string EmblemLabel;                  // under the emblem or the faces: "12 people"
        public string[] Faces = new string[0];      // the people it is about: their faces in a grid take the emblem's place
        public int FaceCount;                       // how many people that is, when more than the faces given; 0 for the faces alone
        public string[] FaceRoles;                  // who each face is ("Presiding"): one to a row, each beside its name and this; null for the grid alone
        public UiRole[] FaceRings;                  // with the roles, each face's ring (whose side they are on); null or None for none
        public int Speaking = -1;                   // with the roles, the face whose name is in the accent: who speaks; -1 for nobody
        public string Body;
        public int BodyLines = 5;                   // the lines the paragraph may take: an obituary takes more than a decision's sentence
        public string[] FactLabels = new string[0]; // a strip of facts under the paragraph, each a label in capitals over its figure
        public string[] FactValues = new string[0];
        public UiRole[] FactInks;                   // a fact's figure in its own ink (a margin in green); null or None for the main ink
        public string Portrait;                     // one person's portrait large in the side, in place of the emblem and the faces, with their name under it
        public string[] PortraitLines = new string[0];   // under the name, in the quiet grey: "Aged 38", "Founder of Speedy Bites"
        public UiDecisionChoice[] Choices = new UiDecisionChoice[0];
        public int Selected = -1;
        public UiActionModel[] Buttons = new UiActionModel[0];
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;

        // A screen that has its own command numbers (the kit's repeat across parts) gives them here: the choices carry ChoiceCmd with the argument in ChoiceArgs
        // (their index when null), the buttons carry their own Command and Arg when ButtonsOwn. Defaults are this card's own two numbers.
        public int ChoiceCmd = UiDecision.ChoiceCommand, ButtonCmd = UiDecision.ButtonCommand;
        public int[] ChoiceArgs;
        public bool ButtonsOwn;
        public int Start = -1, StartButton = -1;    // where focus starts: a choice, or a button: the safe answer, never the way out
        public UiDecisionStep[] Steps = new UiDecisionStep[0];   // amounts to set before choosing, between the facts and the choices
        public bool LiftChoices;                    // a choice's row under the pointer lifts as the table rows do: its fill stays as drawn and the hover fill lies over it, plainly seen
    }

    /// <summary>
    /// The decision card, after Frostpunk 2's big moments (design/DIRECTION_CS2.md, section 4.5): centred over the dimmed
    /// world, an emblem or the faces of the people it is about at its left, its kicker and title in capitals, a paragraph of
    /// what is happening, the choices numbered, each with its consequences in coloured lines (green good, red money paid out, amber a risk), and
    /// its buttons centred at its foot; the main answer carries F, the second P, as the conversation's do. Commands: <see cref="ChoiceCommand"/> (a choice's index) and
    /// <see cref="ButtonCommand"/> (a button's index). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiDecision
    {
        public const int ChoiceCommand = 1, ButtonCommand = 2;
        public const float Width = 1040f;     // px: the card
        private const float Side = 296f;      // px: the emblem's column
        private const float Emblem = 168f;    // px
        private const float Hero = 144f;      // px: one person's portrait, large
        private const float Face = 72f;       // px: a face in the grid of the people it is about
        private const int FacesAcross = 3, FacesDown = 4;
        private const float Person = 56f;     // px: a face beside its name and who they are, those present told with their roles
        private const int PeopleMost = 6, RoleLines = 2;

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
                float bh = UiTextRules.WrappedHeight(m.Body, UiStyle.Body, inner, m.BodyLines);
                UiDraw.Text(s, at, cx, cy, inner, UiStyle.Body, UiRole.Text2, m.Body, UiFlags.Label | UiFlags.Wrap | UiFlags.Middle, m.BodyLines);
                cy += UiDraw.Snap(bh) + UiTokens.S5;
            }
            if (m.FactLabels.Length > 0)
            {
                Facts(s, at, m, cx, cy, inner);
                cy += FactsHeight(m) + UiTokens.S5;
            }
            for (int i = 0; i < m.Steps.Length; i++)
            {
                cy += Step(s, at, ref m.Steps[i], cx, cy, inner) + UiTokens.S3;
            }
            for (int i = 0; i < m.Choices.Length; i++)
            {
                int arg = m.ChoiceArgs != null && i < m.ChoiceArgs.Length ? m.ChoiceArgs[i] : i;
                cy += Choice(s, at, ref m.Choices[i], i, i == m.Selected, cx, cy, inner, keys, m.ChoiceCmd, arg, m.LiftChoices) + UiTokens.S2;
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
                UiDraw.Button(s, at, bx, cy, bw, UiTokens.Control, b.Kind, b.Label, b.Icon, b.Key, keys, m.ButtonsOwn ? b.Command : m.ButtonCmd, m.ButtonsOwn ? b.Arg : i, b.Disabled);
                bx += bw + UiTokens.S3;
            }
            Start(s, m);
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
                h += UiDraw.Snap(UiTextRules.WrappedHeight(m.Body, UiStyle.Body, inner, m.BodyLines)) + UiTokens.S5;
            }
            if (m.FactLabels.Length > 0)
            {
                h += FactsHeight(m) + UiTokens.S5;
            }
            for (int i = 0; i < m.Steps.Length; i++)
            {
                h += StepHeight(ref m.Steps[i]) + UiTokens.S3;
            }
            for (int i = 0; i < m.Choices.Length; i++)
            {
                h += ChoiceHeight(ref m.Choices[i]) + UiTokens.S2;
            }
            // The buttons' row at the foot; a card without buttons (a moment the player may not leave) ends a padding under its last choice.
            h += m.Buttons.Length > 0 ? UiTokens.S4 + UiTokens.Control + UiTokens.S8 : UiTokens.S8 - UiTokens.S2;
            return Math.Max(UiDraw.Snap(h), SideHeight(m) + UiTokens.S12 * 2f);
        }

        // Focus starts where the card says: the safe choice or button. The first target of the card is the choice a reflexive press must not commit.
        private static void Start(UiScene s, UiDecisionModel m)
        {
            int node = -1;
            if (m.Start >= 0 && m.Start < m.Choices.Length)
            {
                node = s.FindNode(m.ChoiceCmd, m.ChoiceArgs != null && m.Start < m.ChoiceArgs.Length ? m.ChoiceArgs[m.Start] : m.Start);
            }
            else if (m.StartButton >= 0 && m.StartButton < m.Buttons.Length)
            {
                node = s.FindNode(m.ButtonsOwn ? m.Buttons[m.StartButton].Command : m.ButtonCmd, m.ButtonsOwn ? m.Buttons[m.StartButton].Arg : m.StartButton);
            }
            if (node >= 0 && !s.Nodes[node].Disabled)
            {
                s.FocusFirst = node;
            }
        }

        private static float StepHeight(ref UiDecisionStep st) =>
            UiTokens.Control + (string.IsNullOrEmpty(st.Hint) ? 0f : UiTokens.S1 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)));

        // An amount to set: its name at the left, the minus, the figure and the plus, what the figure means under them. Returns its height.
        private static float Step(UiScene s, UiAt at, ref UiDecisionStep st, float x, float y, float w)
        {
            float lw = UiDraw.SnapDown(w * 0.4f), b = UiDraw.StepButton, by = y + (UiTokens.Control - b) / 2f, cx = x + lw, cw = w - lw;
            UiDraw.TextMiddle(s, at, x, y, UiTokens.Control, lw - UiTokens.S2, UiStyle.BodyStrong, UiRole.Text, st.Label, UiFlags.Label | UiFlags.Truncate);
            StepKey(s, at, cx, by, UiIcon.Minus, st.Less, st.Arg, !st.CanLess);
            StepKey(s, at, cx + cw - b, by, UiIcon.Plus, st.More, st.Arg, !st.CanMore);
            float fx = cx + b + UiTokens.S2, fw = cw - (b + UiTokens.S2) * 2f;
            int field = UiDraw.Box(s, at, fx, y, fw, UiTokens.Control, UiRole.Field, UiRole.None, UiTokens.RadiusLarge);
            UiDraw.TextMiddle(s, at.In(field), fx + UiTokens.S2, y, UiTokens.Control, fw - UiTokens.S4, UiStyle.BodyStrong, UiRole.Text, st.Value, UiFlags.Centre | UiFlags.Truncate);
            if (!string.IsNullOrEmpty(st.Hint))
            {
                UiDraw.Text(s, at, x, y + UiTokens.Control + UiTokens.S1, w, UiStyle.Small, UiRole.Text2, st.Hint, UiFlags.Label | UiFlags.Truncate);
            }
            return StepHeight(ref st);
        }

        private static void StepKey(UiScene s, UiAt at, float x, float y, UiIcon icon, int command, int arg, bool disabled)
        {
            float b = UiDraw.StepButton, g = UiTokens.Icon;
            int face = UiDraw.Box(s, at, x, y, b, b, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge, UiFlags.Middle | (disabled ? UiFlags.Disabled : UiFlags.None));
            UiDraw.Icon(s, at.In(face), x + (b - g) / 2f, y + (b - g) / 2f, g, icon, disabled ? UiRole.Muted : UiRole.AccentText,
                UiFlags.Middle | UiFlags.Centre | (disabled ? UiFlags.Disabled : UiFlags.None));
            UiDraw.Node(s, at, x, y, b, b, command, arg, face, disabled);
        }

        /// <summary>The height a choice takes: its name and one line for each consequence (a page that lays the choices in its own room, as the Lead of an app window).</summary>
        public static float ChoiceHeight(ref UiDecisionChoice c) =>
            UiDraw.Snap(UiTokens.S3 * 2f + UiTextRules.LineHeight(UiStyle.BodyStrong) + (c.Lines?.Length ?? 0) * (UiTokens.S1 + UiTextRules.LineHeight(UiStyle.Small)));

        // The side's picture and its words: the faces of the people it is about in a grid, or the emblem in its ring for a
        // decision about no one in particular, with what it is under it, the whole held in the middle of the card's height.
        private static void Emblems(UiScene s, UiAt at, UiDecisionModel m, float x, float y, float h)
        {
            float labelH = string.IsNullOrEmpty(m.EmblemLabel) ? 0f : UiTokens.S5 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader));
            float top = y + UiDraw.SnapDown((h - SideHeight(m)) / 2f);
            float ly;
            if (!string.IsNullOrEmpty(m.Portrait))
            {
                float px = x + UiDraw.SnapDown((Side - Hero) / 2f);
                UiDraw.Portrait(s, at, px, top, Hero, m.Portrait, UiColour.HueOf(m.Portrait), UiRole.None);
                float ny = top + Hero + UiTokens.S4;
                UiDraw.Text(s, at, x + UiTokens.S5, ny, Side - UiTokens.S5 * 2f, UiStyle.Title, UiRole.Text, m.Portrait, UiFlags.Centre | UiFlags.Truncate);
                ny += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)) + UiTokens.S1;
                for (int i = 0; i < m.PortraitLines.Length; i++)
                {
                    UiDraw.Text(s, at, x + UiTokens.S5, ny, Side - UiTokens.S5 * 2f, UiStyle.Small, UiRole.Muted, m.PortraitLines[i], UiFlags.Centre | UiFlags.Truncate);
                    ny += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S1;
                }
                return;
            }
            if (m.Faces.Length > 0 && m.FaceRoles != null)
            {
                People(s, at, m, x, top);
                ly = top + PeopleHeight(m) + UiTokens.S5;
            }
            else if (m.Faces.Length > 0)
            {
                int cells = FacesAcross * FacesDown;
                int total = Math.Max(m.FaceCount, m.Faces.Length);
                int shown = total > cells ? cells - 1 : Math.Min(m.Faces.Length, cells);   // the last cell then says how many more
                int drawn = shown + (total > shown ? 1 : 0);
                float gap = UiTokens.S3;
                float gx = x + UiDraw.SnapDown((Side - FacesAcross * Face - (FacesAcross - 1) * gap) / 2f);
                for (int i = 0; i < drawn; i++)
                {
                    float fx = gx + (i % FacesAcross) * (Face + gap), fy = top + (i / FacesAcross) * (Face + gap);
                    if (i < shown)
                    {
                        UiDraw.Portrait(s, at, fx, fy, Face, m.Faces[i], UiColour.HueOf(m.Faces[i]), UiRole.None);
                        continue;
                    }
                    int more = UiDraw.Circle(s, at, fx, fy, Face, Face, UiRole.Tile, UiFlags.None);
                    UiDraw.TextMiddle(s, at.In(more), fx, fy, Face, Face, UiStyle.Title, UiRole.Text,
                        "+" + (total - shown).ToString(System.Globalization.CultureInfo.InvariantCulture), UiFlags.Centre);
                }
                ly = top + FacesHeight(m) + UiTokens.S5;
            }
            else
            {
                float ex = x + (Side - Emblem) / 2f, ey = top + UiTokens.S1;
                UiDraw.Circle(s, at, ex - UiTokens.S1, ey - UiTokens.S1, Emblem + UiTokens.S2, Emblem + UiTokens.S2, m.EmblemTint, UiFlags.Decor);
                int disc = UiDraw.Circle(s, at, ex, ey, Emblem, Emblem, UiRole.Header, UiFlags.Decor);
                float g = UiDraw.Snap(Emblem * 0.5f);
                UiDraw.Icon(s, at.In(disc), ex + (Emblem - g) / 2f, ey + (Emblem - g) / 2f, g, m.Emblem, m.EmblemTint, UiFlags.Middle | UiFlags.Centre, true);
                ly = ey + Emblem + UiTokens.S1 + UiTokens.S5;
            }
            if (labelH > 0f)
            {
                UiDraw.Text(s, at, x + UiTokens.S5, ly, Side - UiTokens.S5 * 2f, UiStyle.SectionHeader, UiRole.Text, m.EmblemLabel, UiFlags.Label | UiFlags.Centre | UiFlags.Truncate);
            }
        }

        // The side's picture with its words under it: the grid of faces or the emblem and its ring.
        private static float SideHeight(UiDecisionModel m)
        {
            float labelH = string.IsNullOrEmpty(m.EmblemLabel) ? 0f : UiTokens.S5 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader));
            if (!string.IsNullOrEmpty(m.Portrait))
            {
                return Hero + UiTokens.S4 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)) + m.PortraitLines.Length * (UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S1);
            }
            return (m.Faces.Length > 0 ? m.FaceRoles != null ? PeopleHeight(m) : FacesHeight(m) : Emblem + UiTokens.S2) + labelH;
        }

        // The facts' strip: each a label in capitals over its figure, in slots across the card's words.
        private static float FactsHeight(UiDecisionModel m) =>
            UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S1 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title));

        private static void Facts(UiScene s, UiAt at, UiDecisionModel m, float x, float y, float w)
        {
            int n = Math.Min(m.FactLabels.Length, m.FactValues.Length);
            float labelH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader));
            // Each fact takes the width of its words and the rest of the strip is shared out between them, so no label is cut short.
            var wide = new float[n];
            float sum = 0f;
            for (int i = 0; i < n; i++)
            {
                wide[i] = UiDraw.Snap(Math.Max(UiTextRules.Width(m.FactLabels[i], UiStyle.SectionHeader) * 1.18f, UiTextRules.Width(m.FactValues[i], UiStyle.Title)) + UiTokens.S1);
                sum += wide[i];
            }
            if (n > 1 && sum + (n - 1) * UiTokens.S1 > w)
            {
                // More than the strip holds (a longer language): each fact takes its share of the room and cuts its words at an ellipsis, never past the card.
                float scale = (w - (n - 1) * UiTokens.S1) / sum;
                for (int i = 0; i < n; i++)
                {
                    wide[i] = UiDraw.SnapDown(wide[i] * scale);
                }
                sum = 0f;
                for (int i = 0; i < n; i++)
                {
                    sum += wide[i];
                }
            }
            float gap = n > 1 ? Math.Max(UiTokens.S1, UiDraw.SnapDown((w - sum) / (n - 1))) : 0f;
            float fx = x;
            for (int i = 0; i < n; i++)
            {
                UiDraw.Text(s, at, fx, y, wide[i], UiStyle.SectionHeader, UiRole.Text2, m.FactLabels[i], UiFlags.Label | UiFlags.Truncate);
                UiRole ink = m.FactInks != null && i < m.FactInks.Length && m.FactInks[i] != UiRole.None ? m.FactInks[i] : UiRole.Text;
                UiDraw.Text(s, at, fx, y + labelH + UiTokens.S1, wide[i], UiStyle.Title, ink, m.FactValues[i], UiFlags.Truncate);
                fx += wide[i] + gap;
            }
        }

        // Those present with who each is, one to a row: each face in its ring at the left, at its right its name (in the
        // accent for who speaks) and who they are under it in the quiet grey, on two lines where a language needs them, so a
        // long title in any language is read whole.
        private static void People(UiScene s, UiAt at, UiDecisionModel m, float x, float top)
        {
            int shown = Math.Min(m.Faces.Length, PeopleMost);
            float tx = x + UiTokens.S5 + Person + UiTokens.S3, tw = PeopleWords;
            float nameH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SmallStrong));
            for (int i = 0; i < shown; i++)
            {
                float cy = top + i * (Person + UiTokens.S3);
                string name = m.Faces[i];
                UiRole ring = m.FaceRings != null && i < m.FaceRings.Length ? m.FaceRings[i] : UiRole.None;
                UiDraw.Portrait(s, at, x + UiTokens.S5, cy, Person, name, UiColour.HueOf(name), ring);
                string role = i < m.FaceRoles.Length && !string.IsNullOrEmpty(m.FaceRoles[i]) ? m.FaceRoles[i] : null;
                float roleH = role != null ? UiDraw.Snap(UiTextRules.WrappedHeight(role, UiStyle.Small, tw, RoleLines)) : 0f;
                float ty = cy + UiDraw.SnapDown((Person - nameH - roleH) / 2f);
                UiDraw.Text(s, at, tx, ty, tw, UiStyle.SmallStrong, i == m.Speaking ? UiRole.AccentText : UiRole.Text, name, UiFlags.Label | UiFlags.Truncate);
                if (role != null)
                {
                    UiDraw.Text(s, at, tx, ty + nameH, tw, UiStyle.Small, UiRole.Muted, role, UiFlags.Label | UiFlags.Wrap, RoleLines);
                }
            }
        }

        // The words' room beside a face among those present.
        private static float PeopleWords => Side - UiTokens.S5 * 2f - Person - UiTokens.S3;

        private static float PeopleHeight(UiDecisionModel m)
        {
            int shown = Math.Min(m.Faces.Length, PeopleMost);
            return shown * Person + Math.Max(0, shown - 1) * UiTokens.S3;
        }

        private static float FacesHeight(UiDecisionModel m)
        {
            int cells = FacesAcross * FacesDown;
            int total = Math.Max(m.FaceCount, m.Faces.Length);
            int drawn = Math.Min(cells, total > cells ? cells : m.Faces.Length);
            int rows = (drawn + FacesAcross - 1) / FacesAcross;
            return rows * Face + (rows - 1) * UiTokens.S3;
        }

        /// <summary>
        /// A choice: its number in the cyan, its name, its consequences under it each behind its rise or fall in its colour, its note at the right; chosen, the
        /// cyan at a fifth. Public so that a page can lay choices in its own room (the Company app's Lead); its press runs <paramref name="command"/> with
        /// <paramref name="arg"/> (its index when -1), the card's own by default. Returns its height.
        /// </summary>
        public static float Choice(UiScene s, UiAt at, ref UiDecisionChoice c, int index, bool chosen, float x, float y, float w, IUiKeys keys, int command = ChoiceCommand, int arg = -1, bool lift = false)
        {
            float h = ChoiceHeight(ref c);
            // A row's own fill steps only a few levels towards the words under the pointer, too little to see: with the lift the resting fill is a plain box behind and the
            // target's face has none, so the hover fill (a table row's) lies over it.
            bool over = lift && !chosen;
            if (over)
            {
                UiDraw.Box(s, at, x - UiTokens.S3, y, w + UiTokens.S3 * 2f, h, UiRole.Row, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor | (c.Disabled ? UiFlags.Disabled : UiFlags.None));
            }
            int face = UiDraw.Box(s, at, x - UiTokens.S3, y, w + UiTokens.S3 * 2f, h, chosen ? UiRole.RowSel : over ? UiRole.None : UiRole.Row, UiRole.None, UiTokens.RadiusLarge,
                (chosen ? UiFlags.Selected : UiFlags.None) | (c.Disabled ? UiFlags.Disabled : UiFlags.None));
            UiAt inside = at.In(face);
            float nameH = UiTextRules.LineHeight(UiStyle.BodyStrong);
            float ny = y + UiTokens.S3;
            string number = (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + ".";
            float numW = UiDraw.Snap(UiTextRules.Width("9.", UiStyle.BodyStrong) + UiTokens.S2);
            UiDraw.Text(s, inside, x, ny, numW, UiStyle.BodyStrong, chosen ? UiRole.Text : UiRole.AccentText, number, UiFlags.Middle);
            float tx = x + numW, tw = w - numW;
            float noteW = string.IsNullOrEmpty(c.Note) ? 0f : UiDraw.Snap(UiTextRules.Width(c.Note, UiStyle.Small) + UiTokens.S1);
            float capW = UiDraw.ChoiceCapWidth(c.Cap, keys);   // room for its key at the right: F, P, the way back
            float right = x + w - capW;
            float noteLead = noteW > 0f ? noteW + UiTokens.IconSmall + UiTokens.S1 + UiTokens.S3 : 0f;
            UiDraw.Text(s, inside, tx, ny, right - tx - noteLead, UiStyle.BodyStrong, UiRole.Text, c.Name, UiFlags.Label | UiFlags.Truncate | UiFlags.Middle);
            if (noteW > 0f)
            {
                UiRole noteInk = chosen ? UiRole.Text : UiRole.Muted;   // on the chosen card's cyan the quiet grey would not read
                UiDraw.Icon(s, inside, right - noteW - UiTokens.IconSmall - UiTokens.S1, ny + (nameH - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Clock, noteInk, UiFlags.Middle);
                UiDraw.Text(s, inside, right - noteW, ny + (nameH - UiTextRules.LineHeight(UiStyle.Small)) / 2f, noteW, UiStyle.Small, noteInk, c.Note, UiFlags.Right | UiFlags.Middle);
            }
            float ly = ny + nameH;
            float lh = UiTextRules.LineHeight(UiStyle.Small);
            for (int i = 0; c.Lines != null && i < c.Lines.Length; i++)
            {
                ly += UiTokens.S1;
                UiDraw.ConsequenceLine(s, inside, tx, ly, tw, ref c.Lines[i], chosen);
                ly += lh;
            }
            UiDraw.Node(s, at, x - UiTokens.S3, y, w + UiTokens.S3 * 2f, h, command, arg < 0 ? index : arg, face, c.Disabled, -1, 0, c.Key);
            // The cap after the target, so it is not the target's own key (that is its number): the screen takes F and P itself.
            UiDraw.ChoiceCap(s, inside, x + w, ny, nameH, c.Cap, keys);
            return h;
        }
    }
}
