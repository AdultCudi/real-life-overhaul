using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A requirement line of a thing's card: its words, whether it is met, and what stands at its right ("Fitted", "Missing").</summary>
    public struct UiOverlayCheck
    {
        public string Label;
        public string Value;
        public bool Met;
        public bool Plain;         // a line to read, not to meet: a dot, no tick and no ring (what an item needs before it is bought)
    }

    /// <summary>One thing the game lets the player pick from a list (a shelf's stock, a machine's recipe): the chosen words and the options, a page at a time.</summary>
    public struct UiOverlayChoice
    {
        public string Label;       // "Holds"
        public string Value;       // the chosen option's words
        public bool Open;          // the list stands open under it
        public string[] Options;   // every option's words, in the game's order
        public int OptionCount;
        public int Chosen;         // the game's chosen index, or -1
        public int Page;           // the page of options shown
    }

    /// <summary>One line of a card's foot: a hint (words only), a key and its words, or a button the mouse presses.</summary>
    public struct UiOverlayAction
    {
        public string Label;
        public UiGameKey Key;      // the game's key, when it names one; a hint and a plain button have none
        public UiIcon Icon;        // a button's glyph; None for none
        public bool Hint;          // words that say how, not an action
        public bool Main;          // the one the game's Interact does: on the cyan
        public bool Disabled;      // seen, but a press does nothing
        public string Reason;      // why, at the row's right; null for none
    }

    /// <summary>
    /// What a box over an item says. One model for the game's two cards over a thing in the world: the small one the pointer
    /// calls up (the hover form: a figure with its meter, up to four rows, the lines that must be met, a hint of the click) and
    /// the large one a click opens (the detailed form: the same, then its choices, its volume and its buttons, each pressed
    /// through the game's own button). Arrays are filled in place and counted, so a read allocates nothing once they are made.
    /// </summary>
    public sealed class UiOverlayModel
    {
        public const int MostRows = 8, MostChecks = 12, MostChoices = 2, MostActions = 8, PerPage = 6, MostNoteLines = 3;

        public string Title;                                       // "Flour shelf"
        public UiIcon Icon = UiIcon.Package;
        public string Tag;                                         // what it is, at the strip's right ("Stock", "Price", "Recipe"); null for none
        public string Face;                                        // a person's name: their painted face in the strip's place of the glyph
        public float Hue;
        public UiRole Ring = UiRole.None;                          // the face's ring: the person's mood
        public string Figure;                                      // "12 of 40", "$4.50"; null for none
        public string FigureLabel;                                 // what the figure counts ("Stock", "Price"), small beside it
        public int Meter = -1;                                     // 0 to 100 under the figure; -1 for none
        public UiRole MeterTone = UiRole.Ok;
        public UiInfoRow[] Rows = new UiInfoRow[MostRows];
        public int RowCount;
        public UiOverlayCheck[] Checks = new UiOverlayCheck[MostChecks];
        public int CheckCount;
        public string Note;                                        // a sentence of the game's under the rest ("This fridge is empty")
        public UiRole NoteTone = UiRole.Text2;
        public UiOverlayChoice[] Choices = new UiOverlayChoice[MostChoices];
        public int ChoiceCount;
        public int Level = -1;                                     // a volume 0 to 100; -1 for none
        public string LevelLabel;                                  // "Volume"
        public UiOverlayAction[] Actions = new UiOverlayAction[MostActions];   // grows when a box holds more buttons; the card shows MostActions at a time
        public int ActionCount;
        public int ActionPage;                                     // the page of buttons shown, when there are more than fit
        public bool Detail;                                        // the click's card: its actions are pressed, its width is wider

        /// <summary>Back to an empty card, its arrays kept.</summary>
        public void Clear()
        {
            Title = Tag = Face = Figure = FigureLabel = Note = LevelLabel = null;
            Icon = UiIcon.Package;
            Hue = 0f;
            Ring = UiRole.None;
            Meter = Level = -1;
            MeterTone = UiRole.Ok;
            NoteTone = UiRole.Text2;
            RowCount = CheckCount = ChoiceCount = ActionCount = ActionPage = 0;
            Detail = false;
        }

        public void AddRow(string label, string value, UiRole ink = UiRole.None)
        {
            if (RowCount < Rows.Length && !string.IsNullOrEmpty(value))
            {
                Rows[RowCount++] = new UiInfoRow { Label = label, Value = value, Ink = ink };
            }
        }

        public void AddCheck(string label, string value, bool met, bool plain = false)
        {
            if (CheckCount < Checks.Length && !string.IsNullOrEmpty(label))
            {
                Checks[CheckCount++] = new UiOverlayCheck { Label = label, Value = value, Met = met, Plain = plain };
            }
        }

        /// <summary>A number that changes when anything the card says changes: the layout is made again only then.</summary>
        public int Digest()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + Hash(Title) + Hash(Tag) * 3 + Hash(Face) * 5 + Hash(Figure) * 7 + Hash(FigureLabel) * 11 + Hash(Note) * 13 + Hash(LevelLabel) * 17;
                h = h * 31 + (int)Icon + (int)Ring * 3 + (int)MeterTone * 5 + (int)NoteTone * 7 + Meter * 11 + Level * 13 + (Detail ? 1 : 0);
                for (int i = 0; i < RowCount; i++)
                {
                    h = h * 31 + Hash(Rows[i].Label) + Hash(Rows[i].Value) * 3 + (int)Rows[i].Ink * 5;
                }
                for (int i = 0; i < CheckCount; i++)
                {
                    h = h * 31 + Hash(Checks[i].Label) + Hash(Checks[i].Value) * 3 + (Checks[i].Met ? 5 : 0) + (Checks[i].Plain ? 7 : 0);
                }
                for (int i = 0; i < ChoiceCount; i++)
                {
                    h = h * 31 + Hash(Choices[i].Label) + Hash(Choices[i].Value) * 3 + (Choices[i].Open ? 5 : 0) + Choices[i].Chosen * 7 + Choices[i].Page * 11 + Choices[i].OptionCount * 13;
                }
                for (int i = 0; i < ActionCount; i++)
                {
                    h = h * 31 + Hash(Actions[i].Label) + Hash(Actions[i].Reason) * 3 + (Actions[i].Disabled ? 5 : 0) + (Actions[i].Main ? 7 : 0) + (Actions[i].Hint ? 11 : 0)
                        + (int)Actions[i].Icon * 13
                        + Hash(Actions[i].Key.Name) * 17 + (int)Actions[i].Key.Glyph * 19;
                }
                return h * 31 + ActionPage * 3 + ActionCount;
            }
        }

        private static int Hash(string s) => s == null ? 0 : s.GetHashCode();

        public void AddAction(UiOverlayAction action)
        {
            if (string.IsNullOrEmpty(action.Label))
            {
                return;
            }
            if (ActionCount == Actions.Length)
            {
                Array.Resize(ref Actions, Actions.Length * 2);   // a fridge or a street cart has a button for every kind it holds
            }
            Actions[ActionCount++] = action;
        }
    }

    /// <summary>
    /// The card over an item of the world, as the hover mockup draws it (previews/ui/mockups/overlays/hover_prompts): a slate
    /// box whose strip holds the thing's glyph or a person's face, its name in cyan capitals and what it is at the right, then
    /// the one figure that matters with its meter, a few rows of label and value, the lines that must be met, a sentence, and
    /// at the foot the hint of the click or the buttons of the click's card. A choice opens as its options in place, a page at
    /// a time, so the game's long drop-down lists are still reached through the game's own choice. Commands:
    /// <see cref="ActionCommand"/> (an action's index), <see cref="ChoiceCommand"/> (a choice's index: open or close it),
    /// <see cref="OptionCommand"/> (an option's index in the game's list), <see cref="PageCommand"/> (-1 or 1) and
    /// <see cref="StepCommand"/> (-1 or 1: the volume). The hover form takes no target at all, so the pointer passes through
    /// it to the world. Game-free. Cost: a layout a change; following its point moves it.
    /// </summary>
    public static class UiOverlayCard
    {
        // 9620 to 9659 is this job's block of command numbers: nothing else of the kit uses it.
        public const int ActionCommand = 9620, ChoiceCommand = 9621, OptionCommand = 9622, PageCommand = 9623, StepCommand = 9624, ActionPageCommand = 9625;
        public const float HoverWidth = 288f;      // px: as wide as the game's box with its longest line
        public const float DetailWidth = 320f;     // px: room for a button's words and a drop-down's choice
        private const float Pad = UiTokens.S4;     // px: the card's inner edge
        private const float ActionRow = UiTokens.ControlSmall;
        private const float OptionRow = 28f;       // px: an option of an open choice
        private const float Field = UiTokens.ControlSmall;
        private const float Leader = 8f;           // px: the dot at the item, and the line's width is a quarter of it

        /// <summary>The card's width.</summary>
        public static float Width(UiOverlayModel m) => m.Detail ? DetailWidth : HoverWidth;

        /// <summary>The card's height for its model.</summary>
        public static float Height(UiOverlayModel m)
        {
            float h = UiFrame.Strip + UiTokens.S2;
            h += Block(FigureHeight(m));
            h += Block(m.RowCount * UiTokens.PairRow);
            h += Block(m.CheckCount * UiTokens.PairRow);
            h += Block(NoteHeight(m));
            for (int i = 0; i < m.ChoiceCount; i++)
            {
                h += Block(ChoiceHeight(ref m.Choices[i]));
            }
            h += Block(m.Level >= 0 ? UiTokens.PairRow + Field : 0f);
            h += Block(ActionsHeight(m));
            return UiDraw.Snap(h);
        }

        // A block's height with the gap that follows it, or nothing for an empty block.
        private static float Block(float h) => h > 0f ? h + UiTokens.S2 : 0f;

        private static float FigureHeight(UiOverlayModel m) =>
            string.IsNullOrEmpty(m.Figure) ? 0f : UiTextRules.LineHeight(UiStyle.Title) + (m.Meter >= 0 ? UiTokens.S2 + UiTokens.S1 : 0f) + UiTokens.S1;

        private static float NoteHeight(UiOverlayModel m) =>
            string.IsNullOrEmpty(m.Note) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(m.Note, UiStyle.Body, Width(m) - Pad * 2f, UiOverlayModel.MostNoteLines));

        private static float ChoiceHeight(ref UiOverlayChoice c)
        {
            float h = UiTokens.PairRow + Field;
            if (c.Open)
            {
                h += UiTokens.S1 + Shown(ref c) * OptionRow + (Pages(ref c) > 1 ? ActionRow + UiTokens.S1 : 0f);
            }
            return h;
        }

        private static float ActionsHeight(UiOverlayModel m)
        {
            OverlayRules.ActionWindow(m.ActionCount, m.ActionPage, UiOverlayModel.MostActions, out int first, out int end);
            float h = 0f;
            for (int i = first; i < end; i++)
            {
                h += (m.Actions[i].Hint ? UiTokens.PairRow : ActionRow) + (i < end - 1 ? UiTokens.S1 : 0f);
            }
            if (OverlayRules.ActionPages(m.ActionCount, UiOverlayModel.MostActions) > 1)
            {
                h += UiTokens.S1 + ActionRow;
            }
            return h;
        }

        /// <summary>The pages an open choice's options fall over.</summary>
        public static int Pages(ref UiOverlayChoice c) => Math.Max(1, (c.OptionCount + UiOverlayModel.PerPage - 1) / UiOverlayModel.PerPage);

        // The options on the page shown.
        private static int Shown(ref UiOverlayChoice c) => Math.Max(0, Math.Min(UiOverlayModel.PerPage, c.OptionCount - c.Page * UiOverlayModel.PerPage));

        /// <summary>The card at a place (its top left, px of the layout); returns its panel.</summary>
        public static int Lay(UiScene s, UiAt at, UiOverlayModel m, float x, float y)
        {
            float w = Width(m), h = Height(m);
            int panel = UiDraw.Box(s, at, x, y, w, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(panel);
            float cy = Strip(s, on, m, x, y, w) + UiTokens.S2;
            float ix = x + Pad, iw = w - Pad * 2f;
            cy = Figure(s, on, m, ix, cy, iw);
            for (int i = 0; i < m.RowCount; i++)
            {
                UiDraw.InfoRow(s, on, ix, cy + i * UiTokens.PairRow, iw, m.Rows[i].Label, m.Rows[i].Value, false, -1, 0, m.Rows[i].Ink == UiRole.None ? UiRole.Text : m.Rows[i].Ink);
            }
            cy += Block(m.RowCount * UiTokens.PairRow);
            for (int i = 0; i < m.CheckCount; i++)
            {
                Check(s, on, ref m.Checks[i], ix, cy + i * UiTokens.PairRow, iw);
            }
            cy += Block(m.CheckCount * UiTokens.PairRow);
            if (!string.IsNullOrEmpty(m.Note))
            {
                UiDraw.Text(s, on, ix, cy, iw, UiStyle.Body, m.NoteTone, m.Note, UiFlags.Wrap, UiOverlayModel.MostNoteLines);
                cy += Block(NoteHeight(m));
            }
            for (int i = 0; i < m.ChoiceCount; i++)
            {
                cy += Block(Choice(s, on, ref m.Choices[i], i, ix, cy, iw));
            }
            if (m.Level >= 0)
            {
                Level(s, on, m, ix, cy, iw);
                cy += Block(UiTokens.PairRow + Field);
            }
            float ay = cy, caps = 0f;
            OverlayRules.ActionWindow(m.ActionCount, m.ActionPage, UiOverlayModel.MostActions, out int first, out int end);
            for (int i = first; i < end; i++)
            {
                caps = m.Actions[i].Hint ? caps : Math.Max(caps, UiDraw.GameKeyWidth(m.Actions[i].Key));
            }
            for (int i = first; i < end; i++)
            {
                ay += Action(s, on, m, ref m.Actions[i], i, x + UiTokens.S2, ay, w - UiTokens.S2 * 2f, caps) + UiTokens.S1;
            }
            int actionPages = OverlayRules.ActionPages(m.ActionCount, UiOverlayModel.MostActions);
            if (actionPages > 1)
            {
                ActionPager(s, on, m, actionPages, x + UiTokens.S2, ay, w - UiTokens.S2 * 2f);
            }
            return panel;
        }

        // The strip: the glyph or the face, the name in cyan capitals, and what it is at the right.
        private static float Strip(UiScene s, UiAt at, UiOverlayModel m, float x, float y, float w)
        {
            const float h = UiFrame.Strip;
            int strip = UiDraw.Box(s, at, x, y, w, h, UiRole.Header, UiRole.None, -UiTokens.RadiusPanel, UiFlags.Decor | UiFlags.Chrome);
            UiAt on = at.In(strip);
            float face = UiTokens.S6, fx = x + UiTokens.S3, tx = fx + face + UiTokens.S2;
            if (!string.IsNullOrEmpty(m.Face))
            {
                UiDraw.Portrait(s, on, fx, y + (h - face) / 2f, face, m.Face, m.Hue, m.Ring);
            }
            else if (m.Icon != UiIcon.None)
            {
                UiDraw.Icon(s, on, fx, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, m.Icon, UiRole.Accent, UiFlags.Middle | UiFlags.Chrome);
                tx = fx + UiTokens.Icon + UiTokens.S2;
            }
            float tag = string.IsNullOrEmpty(m.Tag) ? 0f : Math.Min(UiDraw.Snap(UiTextRules.Width(m.Tag, UiStyle.Small) + UiTokens.S1), UiDraw.SnapDown((w - tx + x) / 2f));
            if (tag > 0f)
            {
                UiDraw.TextMiddle(s, on, x + w - UiTokens.S3 - tag, y, h, tag, UiStyle.Small, UiRole.Text2, m.Tag, UiFlags.Right | UiFlags.Truncate | UiFlags.Chrome);
            }
            UiDraw.TextMiddle(s, on, tx, y, h, x + w - UiTokens.S3 - tx - (tag > 0f ? tag + UiTokens.S2 : 0f), UiStyle.SectionTitle, UiRole.AccentText, m.Title ?? string.Empty,
                UiFlags.Truncate | UiFlags.Chrome);
            UiDraw.Box(s, at, x, y + h - UiTokens.Hairline, w, UiTokens.Hairline, UiRole.Rule, UiRole.None, 0f, UiFlags.Decor | UiFlags.Chrome);
            return y + h;
        }

        // The one figure that matters, with what it counts at its side, and its meter under it.
        private static float Figure(UiScene s, UiAt at, UiOverlayModel m, float x, float y, float w)
        {
            if (string.IsNullOrEmpty(m.Figure))
            {
                return y;
            }
            float lh = UiTextRules.LineHeight(UiStyle.Title);
            float fw = Math.Min(UiDraw.Snap(UiTextRules.Width(m.Figure, UiStyle.Title) + UiTokens.S1), w);
            UiDraw.Text(s, at, x, y, fw, UiStyle.Title, UiRole.Text, m.Figure, UiFlags.Truncate);
            if (!string.IsNullOrEmpty(m.FigureLabel) && w - fw - UiTokens.S2 > UiTokens.S8)
            {
                UiDraw.TextMiddle(s, at, x + fw + UiTokens.S2, y, lh, w - fw - UiTokens.S2, UiStyle.Small, UiRole.Text2, m.FigureLabel, UiFlags.Truncate);
            }
            if (m.Meter >= 0)
            {
                UiDraw.Meter(s, at, x, y + lh + UiTokens.S2, w, UiTokens.S1, m.Meter, m.MeterTone);
            }
            return y + Block(FigureHeight(m));
        }

        // A line that must be met: a tick or an open ring, its words, and what stands at its right.
        private static void Check(UiScene s, UiAt at, ref UiOverlayCheck c, float x, float y, float w)
        {
            float g = UiTokens.IconSmall;
            if (c.Plain)
            {
                UiDraw.Circle(s, at, x + (g - UiTokens.S1) / 2f, y + (UiTokens.PairRow - UiTokens.S1) / 2f, UiTokens.S1, UiTokens.S1, UiRole.Muted, UiFlags.Middle);
            }
            else
            {
                UiDraw.Icon(s, at, x, y + (UiTokens.PairRow - g) / 2f, g, c.Met ? UiIcon.Check : UiIcon.Ring, c.Met ? UiRole.OkText : UiRole.Muted, UiFlags.Middle);
            }
            float lx = x + g + UiTokens.S2;
            float vw = string.IsNullOrEmpty(c.Value) ? 0f : Math.Min(UiDraw.SnapDown(w * 0.4f), UiDraw.Snap(UiTextRules.Width(c.Value, UiStyle.Body) + UiTokens.S1));
            UiDraw.TextMiddle(s, at, lx, y, UiTokens.PairRow, x + w - lx - (vw > 0f ? vw + UiTokens.S3 : 0f), UiStyle.Body, UiRole.Text2, c.Label, UiFlags.Truncate);
            if (vw > 0f)
            {
                UiDraw.TextMiddle(s, at, x + w - vw, y, UiTokens.PairRow, vw, UiStyle.Body, c.Met || c.Plain ? UiRole.Text : UiRole.WarnText, c.Value, UiFlags.Right | UiFlags.Truncate);
            }
        }

        // A choice: its label, the field showing the chosen words (a press opens the list), and, open, a page of options.
        private static float Choice(UiScene s, UiAt at, ref UiOverlayChoice c, int index, float x, float y, float w)
        {
            UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, w, UiStyle.Body, UiRole.Text2, c.Label ?? string.Empty, UiFlags.Label | UiFlags.Truncate);
            float fy = y + UiTokens.PairRow;
            int face = UiDraw.Box(s, at, x, fy, w, Field, c.Open ? UiRole.RowSel : UiRole.Field, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(face);
            float g = UiTokens.IconSmall;
            UiDraw.TextMiddle(s, inside, x + UiTokens.S3, fy, Field, w - UiTokens.S3 * 2f - g - UiTokens.S2, UiStyle.Body, UiRole.Text, c.Value ?? string.Empty, UiFlags.Truncate);
            UiDraw.Icon(s, inside, x + w - UiTokens.S3 - g, fy + (Field - g) / 2f, g, c.Open ? UiIcon.Up : UiIcon.Down, UiRole.Text2, UiFlags.Middle);
            UiDraw.Node(s, at, x, fy, w, Field, ChoiceCommand, index, face);
            float cy = fy + Field;
            if (!c.Open)
            {
                return cy - y;
            }
            cy += UiTokens.S1;
            int shown = Shown(ref c), first = c.Page * UiOverlayModel.PerPage;
            for (int i = 0; i < shown; i++)
            {
                int option = first + i;
                bool chosen = option == c.Chosen;
                int row = UiDraw.Box(s, at, x, cy, w, OptionRow, chosen ? UiRole.RowSel : UiRole.None, UiRole.None, UiTokens.RadiusLarge, chosen ? UiFlags.Selected : UiFlags.None);
                UiDraw.TextMiddle(s, at.In(row), x + UiTokens.S3, cy, OptionRow, w - UiTokens.S3 * 2f, UiStyle.Body, chosen ? UiRole.AccentText : UiRole.Text,
                    option < c.Options.Length ? c.Options[option] : string.Empty, UiFlags.Truncate);
                UiDraw.Node(s, at, x, cy, w, OptionRow, OptionCommand, option, row);
                cy += OptionRow;
            }
            int pages = Pages(ref c);
            if (pages > 1)
            {
                float by = cy + UiTokens.S1;
                float bw = UiTokens.ControlSmall;
                UiDraw.IconButton(s, at, x, by, bw, UiIcon.Back, UiRole.Text, PageCommand, -1, false, UiAction.Count, false, true);
                UiDraw.IconButton(s, at, x + w - bw, by, bw, UiIcon.Next, UiRole.Text, PageCommand, 1, false, UiAction.Count, false, true);
                UiDraw.TextMiddle(s, at, x + bw, by, bw, w - bw * 2f, UiStyle.Small, UiRole.Text2, (c.Page + 1) + " / " + pages, UiFlags.Centre);
                cy = by + bw;
            }
            return cy - y;
        }

        // The pager under a card whose box holds more buttons than fit: back, the page, next.
        private static void ActionPager(UiScene s, UiAt at, UiOverlayModel m, int pages, float x, float y, float w)
        {
            float bw = UiTokens.ControlSmall;
            UiDraw.IconButton(s, at, x, y, bw, UiIcon.Back, UiRole.Text, ActionPageCommand, -1, false, UiAction.Count, false, true);
            UiDraw.IconButton(s, at, x + w - bw, y, bw, UiIcon.Next, UiRole.Text, ActionPageCommand, 1, false, UiAction.Count, false, true);
            UiDraw.TextMiddle(s, at, x + bw, y, bw, w - bw * 2f, UiStyle.Small, UiRole.Text2, (m.ActionPage + 1) + " / " + pages, UiFlags.Centre);
        }

        // A volume: its label with the figure, and a meter between its two steps.
        private static void Level(UiScene s, UiAt at, UiOverlayModel m, float x, float y, float w)
        {
            string figure = m.Level + "%";
            float fw = UiDraw.Snap(UiTextRules.Width(figure, UiStyle.Body) + UiTokens.S1);
            UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, w - fw - UiTokens.S3, UiStyle.Body, UiRole.Text2, m.LevelLabel ?? string.Empty, UiFlags.Label | UiFlags.Truncate);
            UiDraw.TextMiddle(s, at, x + w - fw, y, UiTokens.PairRow, fw, UiStyle.Body, UiRole.Text, figure, UiFlags.Right);
            float by = y + UiTokens.PairRow, b = Field;
            UiDraw.IconButton(s, at, x, by, b, UiIcon.Minus, UiRole.Text, StepCommand, -1);
            UiDraw.IconButton(s, at, x + w - b, by, b, UiIcon.Plus, UiRole.Text, StepCommand, 1);
            UiDraw.Meter(s, at, x + b + UiTokens.S3, by + (b - UiTokens.S2) / 2f, w - (b + UiTokens.S3) * 2f, UiTokens.S2, m.Level, UiRole.Accent);
        }

        // A foot line: a hint in the quiet ink, or the game's key on its cap with its words; in the click's card a button the mouse presses.
        private static float Action(UiScene s, UiAt at, UiOverlayModel m, ref UiOverlayAction a, int index, float x, float y, float w, float caps)
        {
            if (a.Hint)
            {
                UiDraw.TextMiddle(s, at, x + UiTokens.S2, y, UiTokens.PairRow, w - UiTokens.S2 * 2f, UiStyle.Body, UiRole.Text2, a.Label, UiFlags.Truncate);
                return UiTokens.PairRow;
            }
            bool main = a.Main && !a.Disabled;
            int drawn = s.ItemCount;
            int face = UiDraw.Box(s, at, x, y, w, ActionRow, main ? UiRole.Accent : UiRole.Field, UiRole.None, UiTokens.RadiusLarge, a.Disabled ? UiFlags.Disabled : UiFlags.None);
            UiAt inside = at.In(face);
            float kx = x + UiTokens.S2;
            float cx = kx + (caps > 0f ? caps + UiTokens.S2 : 0f);
            if (caps <= 0f && a.Icon != UiIcon.None)
            {
                UiDraw.Icon(s, inside, kx, y + (ActionRow - UiTokens.Icon) / 2f, UiTokens.Icon, a.Icon, main ? UiRole.OnAccent : UiRole.None, UiFlags.Middle);
                cx = kx + UiTokens.Icon + UiTokens.S2;
            }
            else if (caps <= 0f)
            {
                cx = x + UiTokens.S3;
            }
            float rw = a.Disabled && !string.IsNullOrEmpty(a.Reason) ? UiDraw.Snap(UiTextRules.Width(a.Reason, UiStyle.SmallStrong) + UiTokens.S1) : 0f;
            UiDraw.TextMiddle(s, inside, cx, y, ActionRow, x + w - UiTokens.S3 - cx - (rw > 0f ? rw + UiTokens.S3 : 0f), UiStyle.Body, main ? UiRole.OnAccent : UiRole.Text, a.Label,
                UiFlags.Label | UiFlags.Truncate);
            int reason = -1;
            if (rw > 0f)
            {
                reason = UiDraw.TextMiddle(s, inside, x + w - UiTokens.S3 - rw, y, ActionRow, rw, UiStyle.SmallStrong, UiRole.WarnText, a.Reason, UiFlags.Right | UiFlags.Truncate);
            }
            int node = -1;
            if (m.Detail)
            {
                node = UiDraw.Node(s, at, x, y, w, ActionRow, ActionCommand, index, face, a.Disabled);
            }
            if (caps > 0f && a.Key.Shown)
            {
                float kw = UiDraw.GameKeyWidth(a.Key);
                UiDraw.GameKey(s, inside, kx + UiDraw.SnapDown((caps - kw) / 2f), y + (ActionRow - UiTokens.KeyCap) / 2f, a.Key, main, a.Disabled);
            }
            if (a.Disabled)
            {
                UiDraw.Dim(s, drawn, node);
                if (reason >= 0)
                {
                    s.Items[reason].Alpha = 1f;   // the reason reads whole: it says why the rest is dim
                }
            }
            return ActionRow;
        }

        // ------------------------------------------------------------------------------------------------- the leader

        /// <summary>The leader's height: the line from the card's foot down to the middle of the dot over the item, and the dot's lower half.</summary>
        public static float LeaderHeight => UiHudRules.Lift + Leader / 2f;

        /// <summary>The leader's width: the dot's.</summary>
        public static float LeaderWidth => Leader;

        /// <summary>
        /// The leader line: a thin line rising from a dot standing on the item to the card's foot, laid out once. Its box is moved
        /// each frame to the card's foot above the item's point (the dot's middle is the point), and it shows only while the card
        /// stands in its natural place over the point.
        /// </summary>
        public static void LayLeader(UiScene s)
        {
            s.Begin("overlay leader", LeaderWidth, LeaderHeight, true);
            s.Floats = true;
            s.Pointer = false;
            float line = UiTokens.S1 / 2f, reach = UiHudRules.Lift - Leader / 2f;
            UiDraw.Box(s, UiAt.Screen, (Leader - line) / 2f, 0f, line, reach, UiRole.Accent, UiRole.None, 0f, UiFlags.Decor);
            UiDraw.Circle(s, UiAt.Screen, 0f, reach, Leader, Leader, UiRole.Accent, UiFlags.Decor);
        }
    }
}
