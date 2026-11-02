using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a conversation's form slot asks for, where the game's dialog puts an input in.</summary>
    public enum UiFormKind : byte
    {
        None = 0,
        Amount,        // a figure with less and more, typed or stepped
        Choice,
        Switch,
        Text,
        Slider,        // a value between two ends, the game's Slider, its figure beside the track
        Figure,        // a line read only: a label and its value (a fee, a total), as an info panel's row
        Note,          // a sentence of the form's own (what a contract covers, a warning), wrapped in the slot
        Heading,       // a short label over the fields under it (the hours a job asks for)
        Entry,         // a line of a list with its button at the right (a contract to cancel)
        Action,        // a button alone (add an item)
        Swatches,      // colours to pick, each a square of its own colour
        Break,         // a hairline between the form's parts, where the game puts its splitter
        Radio          // one of a few options of which one is chosen (where a tow takes the car)
    }

    /// <summary>
    /// A conversation: who is talking (a face, a name, a role, the mood in the ring), the thread, the typing dots, the
    /// choices as numbered rows with their consequence lines (the game's buttons: cancel, the second option, confirm) and
    /// a form slot where the game's dialog asks for an amount, a choice, a switch or words.
    /// </summary>
    public sealed class UiConversationModel
    {
        public string Title;                           // the docked panel's strip: "Talking to Maya Okafor"
        public string Name;
        public float Hue;
        public UiRole Ring = UiRole.Ok;                // the mood
        public string Role;                            // "Customer Service, Bloom Cafe"
        public string Status;                          // a chip beside the role ("On shift"); null for none
        public UiRole StatusTone = UiRole.Ok;
        public string ThreadStart;                     // the line over the first message; null for none
        public UiMessageModel[] Messages = new UiMessageModel[0];
        public int MessageCount;
        public float Offset = -1f;                     // px the thread has scrolled; -1 shows its newest
        public bool Typing;
        public UiDecisionChoice[] Choices = new UiDecisionChoice[0];
        public int Chosen = -1;
        public UiFormKind Form;
        public string FormLabel;                       // "Your offer"
        public string FormValue;                       // the figure, the choice, the words typed
        public string FormHint;                        // a quiet line under it ("The bank lends up to $40,000"); null for none
        public UiFormField[] Fields = new UiFormField[0];   // a form of several fields (a wage and a signing bonus) in place of the one above
        public int FieldCount;
        public UiFormRow[] Rows = new UiFormRow[0];         // a game's whole form (a loan, a campaign, a delivery) in place of both (UiFormRows)
        public int RowCount;
        public UiFormPick Pick = new UiFormPick();           // that form's open choice, held slider and scroll
        public bool FormOn;                            // a switch
        public bool FormCanLess = true, FormCanMore = true;
        public bool Composer;                          // the line takes the player's own words (the mod's texts): the composer at the foot
        public string ComposerPlaceholder, Draft, SendLabel;
        public string EmptyTitle, EmptyBody;           // a thread with nothing in it yet
        public UiPrompt[] Prompts = new UiPrompt[0];   // the docked panel's own prompt bar
        public int PromptCount;
    }

    /// <summary>
    /// A conversation, in two forms: a block of an app window's page (Contacts, Flare's messages), or a panel docked over
    /// the world at the right third of the screen with its title strip, for talk face to face, so the speaker stays in
    /// view (the game's DialogUI, the mod's dialogs and scenes). Its head is who is talking (the face in the ring of their
    /// mood, the name, the role, a state's chip); its thread is theirs at the left on the bubble, the player's at the
    /// right on the cyan's tone, the system's lines quiet in the middle, a short thread at its foot under its start line,
    /// the typing dots while they write; under it a form slot where the game's dialog asks for an amount, a choice, a
    /// switch or words, and the choices as numbered rows with their consequences in coloured lines, as the decision card
    /// reads (the game's buttons map onto them: cancel, the second option, confirm), each on its number's key, or the
    /// composer where the line takes the player's own words. Commands: <see cref="ChoiceCommand"/> (a choice),
    /// <see cref="FormCommand"/>, <see cref="FormLess"/> and <see cref="FormMore"/> (the slot's field and its steps),
    /// <see cref="ComposeCommand"/> and <see cref="SendCommand"/> (the composer).
    /// Game-free. Cost: a layout a change; only the messages in view are laid out.
    /// </summary>
    public static partial class UiConversation
    {
        public const int ChoiceCommand = 60, FormCommand = 61, FormLess = 62, FormMore = 63, ComposeCommand = 64, SendCommand = 65;
        public const float DockShare = 1f / 3f;      // of the screen's width, the docked panel at its right
        public const float DockLeast = 520f;         // px: the docked panel's least width, a choice's name beside its note
        private const float Face = 48f;              // px: the speaker's face at the head
        private const float FormControl = 240f;      // px: the form slot's control
        private const byte RegionThread = 1, RegionChoices = 2;

        /// <summary>Lays the conversation into a room: the head, the thread, the form slot and the choices.</summary>
        public static void Lay(UiScene s, UiAt at, UiConversationModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            float top = Head(s, at, m, x, y, w);
            Body(s, at, m, x, top, w, y + h - top, keys);
        }

        /// <summary>
        /// The conversation without its head, for a page that names who is talking in a head of its own (Contacts, whose
        /// head carries the call and the map): the thread, the form slot and the choices.
        /// </summary>
        public static void Body(UiScene s, UiAt at, UiConversationModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            float top = y;
            float foot = y + h;
            float composerH = m.Composer ? UiTokens.Control + UiTokens.S2 : 0f;
            if (composerH > 0f)
            {
                UiDraw.Composer(s, at.InRegion(RegionChoices), x, foot - composerH, w, m.ComposerPlaceholder, m.Draft, m.SendLabel, ComposeCommand, SendCommand);
                foot -= composerH + (m.Choices.Length > 0 ? UiTokens.S3 : 0f);
            }
            float choicesH = ChoicesHeight(m, w);
            float formH = FormHeight(m, w);
            float cy = foot - choicesH;
            Choices(s, at.InRegion(RegionChoices), m, x, cy, w, keys);
            float fy = cy - (formH > 0f ? formH + UiTokens.S3 : 0f);
            if (formH > 0f)
            {
                Form(s, at.InRegion(RegionChoices), m, x, fy, w);
            }
            float threadBottom = fy - (choicesH + formH + composerH > 0f ? UiTokens.S4 : 0f);
            Thread(s, at.InRegion(RegionThread), m, x, top, w, threadBottom - top);
        }

        /// <summary>
        /// The conversation as a panel docked over the world at the right third of the screen, its title strip at its head,
        /// the city and the speaker beside it.
        /// </summary>
        public static void LayDocked(UiScene s, UiConversationModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("conversation", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            float w = Math.Max(DockLeast, UiDraw.SnapDown(frame.Width * DockShare));
            float x = UiDraw.SnapDown(frame.Width - w - UiTokens.S4);
            // As tall as it holds, standing on the status bar, so a short talk leaves the world above it in view; a long
            // one grows up the screen and then scrolls.
            float inner = w - UiTokens.S5 * 2f;
            float need = UiFrame.Strip + UiTokens.S4 + Face + UiTokens.S4 + ThreadHeight(m, inner) + UiTokens.S4 + FormHeight(m, inner) + (FormHeight(m, inner) > 0f ? UiTokens.S3 : 0f)
                         + ChoicesHeight(m, inner) + (m.Composer ? UiTokens.Control + UiTokens.S2 + UiTokens.S3 : 0f) + UiTokens.S5;
            float h = Math.Min(UiDraw.SnapDown(frame.BandY - UiTokens.S4 * 2f), UiDraw.Snap(need));
            float y = UiDraw.SnapDown(frame.BandY - UiTokens.S4 - h);
            int panel = UiDraw.Box(s, UiAt.Screen, x, y, w, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt at = UiAt.Screen.In(panel);
            float top = UiDraw.TitleStrip(s, at, x, y, w, UiIcon.Message, m.Title, true);
            Lay(s, at, m, x + UiTokens.S5, top + UiTokens.S4, w - UiTokens.S5 * 2f, y + h - UiTokens.S5 - top - UiTokens.S4, keys);
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        // Who is talking: their face in the ring of their mood, their name, their role and a state's chip, a hairline under. Returns the y under it.
        private static float Head(UiScene s, UiAt at, UiConversationModel m, float x, float y, float w)
        {
            float ring = UiTokens.S1;   // the mood's ring inside the head's square, so the face keeps to the grid
            UiDraw.Portrait(s, at, x + ring, y + ring, Face - ring * 2f, m.Name, m.Hue, m.Ring);
            float tx = x + Face + UiTokens.S3, tw = x + w - tx;
            float nameH = UiTextRules.LineHeight(UiStyle.Title), roleH = UiTextRules.LineHeight(UiStyle.Small);
            float top = y + (Face - nameH - UiTokens.S1 - roleH) / 2f;
            float chip = string.IsNullOrEmpty(m.Status) ? 0f : UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(m.Status, UiStyle.SmallStrong) + UiTokens.S1);
            UiDraw.Text(s, at, tx, top, tw - (chip > 0f ? chip + UiTokens.S3 : 0f), UiStyle.Title, UiRole.Text, m.Name, UiFlags.Truncate | UiFlags.Middle);
            if (chip > 0f)
            {
                UiDraw.Chip(s, at, x + w - chip, y + (Face - UiTokens.Badge - UiTokens.S1) / 2f, m.Status, m.StatusTone);
            }
            UiDraw.Text(s, at, tx, top + nameH + UiTokens.S1, tw - (chip > 0f ? chip + UiTokens.S3 : 0f), UiStyle.Small, UiRole.Text2, m.Role, UiFlags.Truncate | UiFlags.Middle);
            UiDraw.Box(s, at, x, y + Face + UiTokens.S3, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            return y + Face + UiTokens.S4;
        }

        // The thread in its frame, its newest at the foot: a short one sits there under its start line.
        private static void Thread(UiScene s, UiAt at, UiConversationModel m, float x, float y, float w, float h)
        {
            float vh = UiDraw.SnapDown(h);
            if (m.MessageCount == 0 && !m.Typing)
            {
                s.AddClip(x, y, w, vh, 0f);
                UiDraw.Empty(s, at, x, y, w, vh, m.EmptyTitle, m.EmptyBody);
                return;
            }
            float startH = string.IsNullOrEmpty(m.ThreadStart) ? 0f : UiTokens.S8 + UiTokens.S4;
            float contentH = startH;
            for (int i = 0; i < m.MessageCount; i++)
            {
                contentH += UiDraw.BubbleHeight(ref m.Messages[i], w) + UiTokens.S3;
            }
            float typingH = m.Typing ? UiTokens.S10 + UiTokens.S3 : 0f;
            contentH += typingH;
            float most = Math.Max(0f, contentH - vh);
            float offset = m.Offset < 0f ? most : Math.Min(m.Offset, most);
            short clip = s.AddClip(x, y, w, vh, offset);
            s.Clips[clip].ContentHeight = contentH;
            s.Clips[clip].Fills = true;   // a short thread's room is its own: it sits at the foot by the choices
            UiDraw.ScrollThumb(s, at, x + w + UiTokens.S1, y, vh, offset, contentH);
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            // A short thread sits at its foot by what answers it; with nothing under it, it starts under the head, so no hole stands over it.
            bool answered = m.Choices.Length > 0 || m.Composer || FormHeight(m, w) > 0f;
            float my = answered ? UiDraw.SnapDown(Math.Max(0f, vh - contentH)) : 0f;
            if (startH > 0f)
            {
                if (my + startH > offset && my < offset + vh)
                {
                    UiDraw.Divider(s, inClip, 0f, my, w, m.ThreadStart);
                }
                my += startH;
            }
            for (int i = 0; i < m.MessageCount; i++)
            {
                float bh = UiDraw.BubbleHeight(ref m.Messages[i], w);
                if (my >= offset - 0.5f && my < offset + vh)   // one cut by the head's edge waits until it scrolls in whole: a sliver of a bubble reads as clutter
                {
                    if (m.Messages[i].From == UiFrom.System)
                    {
                        // The system's line in the quiet grey in the middle, as the thread's start line reads: no frame round it.
                        float lineW = 0.9f * w - UiTokens.S4 * 2f;
                        UiDraw.Text(s, inClip.Arriving(i), (w - lineW) / 2f, my + UiTokens.S3, lineW, UiStyle.Small, UiRole.Muted, m.Messages[i].Text,
                            UiFlags.Wrap | UiFlags.Truncate | UiFlags.Centre | UiFlags.Middle, 12);
                    }
                    else
                    {
                        UiDraw.Bubble(s, inClip.Arriving(i), 0f, my, w, ref m.Messages[i]);
                    }
                }
                my += bh + UiTokens.S3;
            }
            if (m.Typing)
            {
                int bubble = UiDraw.Box(s, inClip, 0f, my, UiTokens.S16 + UiTokens.S4, UiTokens.S10, UiRole.Bubble, UiRole.None, UiTokens.RadiusLarge);
                var dots = new UiItem
                {
                    Kind = UiKind.Busy, Fill = UiRole.AccentText, X = UiTokens.S4, Y = my + UiTokens.S4 + UiTokens.S1, W = UiTokens.S12, H = UiTokens.S2,
                    Clip = clip, Node = -1, Layer = at.Layer, Order = -1, Parent = bubble, Alpha = 1f
                };
                s.Add(ref dots);
            }
        }

        /// <summary>The thread's height at a width: its start line, its messages and the typing dots.</summary>
        public static float ThreadHeight(UiConversationModel m, float w)
        {
            float h = string.IsNullOrEmpty(m.ThreadStart) ? 0f : UiTokens.S8 + UiTokens.S4;
            for (int i = 0; i < m.MessageCount; i++)
            {
                h += UiDraw.BubbleHeight(ref m.Messages[i], w) + UiTokens.S3;
            }
            return h + (m.Typing ? UiTokens.S10 + UiTokens.S3 : 0f);
        }

        /// <summary>The choices' height at a width: each row with its lines and the gap under it.</summary>
        public static float ChoicesHeight(UiConversationModel m, float w)
        {
            float h = 0f;
            for (int i = 0; i < m.Choices.Length; i++)
            {
                h += ChoiceHeight(ref m.Choices[i]) + (i > 0 ? UiTokens.S2 : 0f);
            }
            return h;
        }

        /// <summary>A choice's height: its name and its consequence lines, as the decision card's.</summary>
        public static float ChoiceHeight(ref UiDecisionChoice c) =>
            UiDraw.Snap(UiTokens.S3 * 2f + UiTextRules.LineHeight(UiStyle.BodyStrong) + (c.Lines?.Length ?? 0) * (UiTokens.S1 + UiTextRules.LineHeight(UiStyle.Small)));

        private static void Choices(UiScene s, UiAt at, UiConversationModel m, float x, float y, float w, IUiKeys keys)
        {
            float cy = y;
            for (int i = 0; i < m.Choices.Length; i++)
            {
                cy += ChoiceRow(s, at, ref m.Choices[i], i, i == m.Chosen, x, cy, w, ChoiceCommand) + UiTokens.S2;
            }
        }

        /// <summary>
        /// A choice as the decision card reads one: its number in the cyan, its name, its note at the right behind the clock,
        /// its consequences under it each behind its rise or fall in its colour; chosen, the cyan's tone. On its number's key
        /// (the first nine). Returns its height.
        /// </summary>
        public static float ChoiceRow(UiScene s, UiAt at, ref UiDecisionChoice c, int index, bool chosen, float x, float y, float w, int command)
        {
            float h = ChoiceHeight(ref c);
            // The card's tone under a clear face, so the pointer's still lift reads on it as on a row.
            UiDraw.Box(s, at, x, y, w, h, UiRole.Row, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            int face = UiDraw.Box(s, at, x, y, w, h, chosen ? UiRole.RowSel : UiRole.None, UiRole.None, UiTokens.RadiusLarge,
                (chosen ? UiFlags.Selected : UiFlags.None) | (c.Disabled ? UiFlags.Disabled : UiFlags.None));
            UiAt inside = at.In(face);
            float nameH = UiTextRules.LineHeight(UiStyle.BodyStrong);
            float ix = x + UiTokens.S3, iw = w - UiTokens.S3 * 2f, ny = y + UiTokens.S3;
            string number = (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + ".";
            float numW = UiDraw.Snap(UiTextRules.Width("9.", UiStyle.BodyStrong) + UiTokens.S2);
            UiDraw.Text(s, inside, ix, ny, numW, UiStyle.BodyStrong, chosen ? UiRole.Text : UiRole.AccentText, number, UiFlags.Middle);
            float tx = ix + numW, tw = iw - numW;
            float noteW = string.IsNullOrEmpty(c.Note) ? 0f : UiDraw.Snap(UiTextRules.Width(c.Note, UiStyle.Small) + UiTokens.S1);
            float noteLead = noteW > 0f ? noteW + UiTokens.IconSmall + UiTokens.S1 + UiTokens.S3 : 0f;
            UiDraw.Text(s, inside, tx, ny, tw - noteLead, UiStyle.BodyStrong, UiRole.Text, c.Name, UiFlags.Label | UiFlags.Truncate | UiFlags.Middle);
            if (noteW > 0f)
            {
                UiRole noteInk = chosen ? UiRole.Text : UiRole.Muted;
                UiDraw.Icon(s, inside, ix + iw - noteW - UiTokens.IconSmall - UiTokens.S1, ny + (nameH - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Clock, noteInk, UiFlags.Middle);
                UiDraw.Text(s, inside, ix + iw - noteW, ny + (nameH - UiTextRules.LineHeight(UiStyle.Small)) / 2f, noteW, UiStyle.Small, noteInk, c.Note, UiFlags.Right | UiFlags.Middle);
            }
            float ly = ny + nameH, lh = UiTextRules.LineHeight(UiStyle.Small);
            for (int i = 0; c.Lines != null && i < c.Lines.Length; i++)
            {
                ly += UiTokens.S1;
                UiConsequence line = c.Lines[i];
                UiRole tone = line.Tone > 0 ? UiRole.OkText : line.Tone < 0 ? UiRole.WarnText : UiRole.Text2;
                UiIcon mark = line.Change > 0 ? UiIcon.TrendUp : line.Change < 0 ? UiIcon.TrendDown : line.Tone > 0 ? UiIcon.Check : line.Tone < 0 ? UiIcon.Warning : UiIcon.Minus;
                UiDraw.Icon(s, inside, tx, ly + (lh - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, mark, tone, UiFlags.Middle);
                UiDraw.Text(s, inside, tx + UiTokens.IconSmall + UiTokens.S2, ly, tw - UiTokens.IconSmall - UiTokens.S2, UiStyle.Small, tone, line.Text,
                    UiFlags.Label | UiFlags.Truncate | UiFlags.Middle);
                ly += lh;
            }
            UiAction key = index < 9 ? (UiAction)((int)UiAction.Pick1 + index) : UiAction.Count;
            UiDraw.Node(s, at, x, y, w, h, command, index, face, c.Disabled, -1, 0, key);
            return h;
        }

        private static float FormHeight(UiConversationModel m, float w)
        {
            if (m.RowCount > 0)
            {
                return UiFormRows.Height(m.Rows, m.RowCount, w, m.Pick);
            }
            if (m.FieldCount > 0)
            {
                return FieldsHeight(m);
            }
            if (m.Form == UiFormKind.None)
            {
                return 0f;
            }
            return UiTokens.S3 * 2f + UiTokens.Control + (string.IsNullOrEmpty(m.FormHint) ? 0f : UiTokens.S1 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)));
        }

        // The form slot on a band: its label at the left, its control at the right, a quiet hint under them.
        private static void Form(UiScene s, UiAt at, UiConversationModel m, float x, float y, float w)
        {
            if (m.RowCount > 0)
            {
                UiFormRows.Lay(s, at, m.Rows, m.RowCount, m.Pick, x, y, w);
                return;
            }
            if (m.FieldCount > 0)
            {
                Fields(s, at, m, x, y, w);
                return;
            }
            float h = FormHeight(m, w);
            UiDraw.Band(s, at, x, y, w, h);
            float cw = Math.Min(FormControl, UiDraw.SnapDown(w / 2f)), cx = x + w - UiTokens.S3 - cw, cy = y + UiTokens.S3;
            UiDraw.TextMiddle(s, at, x + UiTokens.S3, cy, UiTokens.Control, cx - UiTokens.S4 - x - UiTokens.S3, UiStyle.Title, UiRole.Text, m.FormLabel, UiFlags.Label | UiFlags.Truncate);
            switch (m.Form)
            {
                case UiFormKind.Amount:
                    UiDraw.Stepper(s, at, cx, cy, cw, m.FormValue, m.FormCanLess, m.FormCanMore, FormLess, FormCommand, FormMore, 0, false);
                    break;
                case UiFormKind.Choice:
                    UiDraw.Choice(s, at, cx, cy, cw, m.FormValue, FormCommand, 0, false);
                    break;
                case UiFormKind.Switch:
                    UiDraw.Switch(s, at, cx, cy, cw, m.FormOn, m.FormValue, FormCommand, 0, false);
                    break;
                default:
                    UiDraw.TextField(s, at, cx, cy, cw, m.FormValue, null, FormCommand, 0, false);
                    break;
            }
            if (!string.IsNullOrEmpty(m.FormHint))
            {
                UiDraw.Text(s, at, x + UiTokens.S3, cy + UiTokens.Control + UiTokens.S1, w - UiTokens.S3 * 2f, UiStyle.Small, UiRole.Text2, m.FormHint, UiFlags.Truncate);
            }
        }
    }
}
