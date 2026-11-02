using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A button at a side card's foot: its words, its look, and the key that presses it. A box the kit draws over
    /// everything owns its keys (<see cref="Key"/>); over the game's own panel the game reads them, so the cap shows the
    /// game's key (<see cref="GameKey"/>) and the kit only answers the pointer.
    /// </summary>
    public struct UiSideButton
    {
        public string Label;
        public UiButtonKind Kind;
        public UiAction Key;        // the kit's own key, or Count when the game reads it
        public UiGameKey GameKey;   // the game's key on its cap, when Key is Count
        public bool Disabled;
        public bool Left;           // stands at the foot's left (the way back), the rest at its right
    }

    /// <summary>A numbered choice of a side card: its name, the line under it, and the game's key for it when the game has one.</summary>
    public struct UiSideChoice
    {
        public string Name;         // "Load the backup"
        public string Line;         // "Return to your last backup, saved on Day 211"
        public UiGameKey GameKey;   // the game's own key for it (F), drawn at its right; the number keys are the kit's extra
        public bool Disabled;
    }

    /// <summary>What a side card shows: who or what it is at its left, its words, rows, a slider, choices and buttons at its right.</summary>
    public sealed class UiSideCardModel
    {
        // The side: an emblem on its tile (a working day's briefcase), or a person's initials large (the funeral).
        public UiIcon Emblem = UiIcon.Briefcase;
        public UiRole EmblemTint = UiRole.Gold;
        public string Portrait;                        // a name: their initials large in the emblem's place
        public string SideName;                        // "Pioneer Game Studio"
        public string[] SideLines = new string[0];     // the address, an age, a day
        public string Person;                          // the person met there: initials on a small disc
        public string PersonLine;                      // "Priya Nair, studio lead"

        // The words.
        public string Kicker;                          // "Working day, Monday"
        public string Title;                           // "Start your shift"
        public string Body;
        public UiInfoRow[] Rows = new UiInfoRow[0];    // on a band: pay, hours, what it gives

        // The slider, its ends under its track and two sums under those.
        public bool HasSlider;
        public string SliderLabel, SliderValue;        // "Work for", "6 hours"
        public string SliderFrom, SliderTo;            // "1 h", "8 h"
        public float SliderShare;
        public bool SliderDisabled, SliderHeld;
        public string SumLeft, SumRight;               // "You finish at 15:00", "$228 before tax"

        public UiSideChoice[] Choices = new UiSideChoice[0];
        public int Selected = -1;

        public string Note;                            // at the foot's left: "Press 1, 2 or 3"
        public UiSideButton[] Buttons = new UiSideButton[0];
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
        public bool Bar = true;                        // the status bar drawn under it; false over the game's own HUD, which has its own

        // The commands it gives back: a screen that keeps its own numbers sets them.
        public int ButtonCommand = UiSideCard.ButtonCommand, SliderCommand = UiSideCard.SliderCommand, ChoiceCommand = UiSideCard.ChoiceCommand;

        // Laid out: where the slider's track runs, so a press or a drag on it reads its share (TrackShare).
        public float TrackX, TrackW;
    }

    /// <summary>
    /// The side card, the decision card's cousin for a moment at a place (DIRECTION_CS2, 4.5): centred over the dimmed
    /// world, a darker side at its left with what it is about (an emblem on its tile and the place's name, address and
    /// person, or a person's initials large with their age and days), and at its right a kicker, the title in cyan
    /// capitals, a paragraph, rows on a band, a slider with its two ends and two sums under it, numbered choices with a
    /// line each, a note at the foot's left and the buttons at its right with their keys. Commands: its model's
    /// ButtonCommand (a button's index), SliderCommand (arg 0; its share from <see cref="TrackShare"/>) and ChoiceCommand
    /// (a choice's index, on its number key). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiSideCard
    {
        public const int ButtonCommand = 246, SliderCommand = 247, ChoiceCommand = 248;
        public const float Width = 980f;      // px: the card
        public const float Side = 288f;       // px: the side's column
        private const float Emblem = 112f;    // px: the emblem's tile
        private const float Face = 136f;      // px: a person's initials large
        private const float PersonFace = 48f; // px: the person met there
        private const float Knob = 16f;       // px: the slider's knob
        private const float HeldAlpha = 0.4f; // a held button's face and words, as the kit's buttons fade

        /// <summary>The card as a screen of its own: centred over the dimmed world above the status bar.</summary>
        public static void Lay(UiScene s, UiSideCardModel m, UiFrame frame, bool dark, IUiKeys keys, string name)
        {
            s.Begin(name, frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            UiDraw.Box(s, UiAt.Screen, 0f, 0f, frame.Width, frame.BandY, UiRole.Scrim, UiRole.None, 0f, UiFlags.Decor);   // the world dimmed, the status bar left clear
            float h = Height(m, keys);
            float x = UiDraw.SnapDown((frame.Width - Width) / 2f), y = UiDraw.SnapDown(Math.Max(UiTokens.S8, (frame.BandY - h) / 2f));
            Draw(s, UiAt.Screen, m, x, y, keys);
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            if (m.Bar)
            {
                UiDraw.PromptBar(s, frame, keys);
            }
        }

        /// <summary>The content's width at the card's right.</summary>
        public static float Inner => Width - Side - UiTokens.S8 * 2f;

        /// <summary>The card's height: its words and parts at the right, at least its side's picture with room round it.</summary>
        public static float Height(UiSideCardModel m, IUiKeys keys = null)
        {
            float inner = Inner;
            float h = UiTokens.S8;
            if (!string.IsNullOrEmpty(m.Kicker))
            {
                h += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
            }
            h += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.AppTitle)) + UiTokens.S3;
            if (!string.IsNullOrEmpty(m.Body))
            {
                h += UiDraw.Snap(UiTextRules.WrappedHeight(m.Body, UiStyle.Body, inner, 5)) + UiTokens.S4;
            }
            if (m.Rows.Length > 0)
            {
                h += RowsHeight(m) + UiTokens.S4;
            }
            if (m.HasSlider)
            {
                h += SliderHeight(m) + UiTokens.S4;
            }
            for (int i = 0; i < m.Choices.Length; i++)
            {
                h += ChoiceHeight(ref m.Choices[i]) + UiTokens.S2;
            }
            if (m.Choices.Length > 0)
            {
                h += UiTokens.S2;
            }
            h += UiTokens.Control + UiTokens.S8 + (FootSplit(m, keys) ? UiTokens.Control + UiTokens.S3 : 0f);
            return Math.Max(UiDraw.Snap(h), UiDraw.Snap(SideHeight(m) + UiTokens.S12 * 2f));
        }

        /// <summary>The share of the slider's run a point across the screen stands at, by the track laid last.</summary>
        public static float TrackShare(UiSideCardModel m, float x)
        {
            if (m == null || m.TrackW <= 0f)
            {
                return 0f;
            }
            float share = (x - m.TrackX) / m.TrackW;
            return share < 0f ? 0f : share > 1f ? 1f : share;
        }

        /// <summary>The card at (x, y): its side, its words, rows, slider, choices and foot.</summary>
        public static void Draw(UiScene s, UiAt at, UiSideCardModel m, float x, float y, IUiKeys keys)
        {
            float h = Height(m, keys), inner = Inner;
            int card = UiDraw.Box(s, at, x, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(card);
            UiDraw.Box(s, on, x, y, Side, h, UiRole.Band, UiRole.None, -UiTokens.RadiusPanel, UiFlags.Decor);
            SideColumn(s, on, m, x, y, h);
            float cx = x + Side + UiTokens.S8, cy = y + UiTokens.S8;
            if (!string.IsNullOrEmpty(m.Kicker))
            {
                UiDraw.Text(s, on, cx, cy, inner, UiStyle.SectionHeader, UiRole.Text, m.Kicker, UiFlags.Label | UiFlags.Truncate);
                cy += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
            }
            UiDraw.Text(s, on, cx, cy, inner, UiStyle.AppTitle, UiRole.AccentText, m.Title, UiFlags.Label | UiFlags.Truncate);
            cy += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.AppTitle)) + UiTokens.S3;
            if (!string.IsNullOrEmpty(m.Body))
            {
                float bh = UiTextRules.WrappedHeight(m.Body, UiStyle.Body, inner, 5);
                UiDraw.Text(s, on, cx, cy, inner, UiStyle.Body, UiRole.Text, m.Body, UiFlags.Label | UiFlags.Wrap, 5);
                cy += UiDraw.Snap(bh) + UiTokens.S4;
            }
            if (m.Rows.Length > 0)
            {
                Rows(s, on, m, cx, cy, inner);
                cy += RowsHeight(m) + UiTokens.S4;
            }
            if (m.HasSlider)
            {
                Slider(s, on, m, cx, cy, inner);
                cy += SliderHeight(m) + UiTokens.S4;
            }
            for (int i = 0; i < m.Choices.Length; i++)
            {
                cy += Choice(s, on, m, i, cx, cy, inner) + UiTokens.S2;
            }
            if (m.Choices.Length > 0)
            {
                cy += UiTokens.S2;
            }
            Foot(s, on, m, cx, y + h - UiTokens.S8 - UiTokens.Control, inner, keys);
        }

        // ------------------------------------------------------------------------------------------------- the side

        private static float SideHeight(UiSideCardModel m)
        {
            float h = string.IsNullOrEmpty(m.Portrait) ? Emblem : Face + UiTokens.S2;
            if (!string.IsNullOrEmpty(m.SideName))
            {
                h += UiTokens.S5 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title));
            }
            if (m.SideLines.Length > 0)
            {
                h += UiTokens.S2 + m.SideLines.Length * LineStep;
            }
            if (!string.IsNullOrEmpty(m.Person) || !string.IsNullOrEmpty(m.PersonLine))
            {
                h += UiTokens.S5 + PersonFace + UiTokens.S3 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            }
            return h;
        }

        private static float LineStep => UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S1;

        // The side's picture and its words under it, the whole held in the middle of the card's height.
        private static void SideColumn(UiScene s, UiAt at, UiSideCardModel m, float x, float y, float h)
        {
            float top = y + UiDraw.SnapDown((h - SideHeight(m)) / 2f);
            float tw = Side - UiTokens.S5 * 2f, tx = x + UiTokens.S5;
            float ly;
            if (!string.IsNullOrEmpty(m.Portrait))
            {
                float fx = x + (Side - Face) / 2f, fy = top + UiTokens.S1;
                UiDraw.Circle(s, at, fx - UiTokens.S1, fy - UiTokens.S1, Face + UiTokens.S2, Face + UiTokens.S2, UiRole.Track, UiFlags.Decor);
                UiDraw.Avatar(s, at, fx, fy, Face, m.Portrait, UiColour.HueOf(m.Portrait));
                ly = fy + Face + UiTokens.S1;
            }
            else
            {
                float ex = x + (Side - Emblem) / 2f;
                int tile = UiDraw.Tile(s, at, ex, top, Emblem, UiRole.None, false);
                float g = UiDraw.Snap(Emblem * 0.5f);
                UiDraw.Icon(s, at.In(tile), ex + (Emblem - g) / 2f, top + (Emblem - g) / 2f, g, m.Emblem, m.EmblemTint, UiFlags.Middle | UiFlags.Centre, true);
                ly = top + Emblem;
            }
            if (!string.IsNullOrEmpty(m.SideName))
            {
                ly += UiTokens.S5;
                UiDraw.Text(s, at, tx, ly, tw, UiStyle.Title, UiRole.Text, m.SideName, UiFlags.Centre | UiFlags.Truncate);
                ly += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title));
            }
            if (m.SideLines.Length > 0)
            {
                ly += UiTokens.S2;
                for (int i = 0; i < m.SideLines.Length; i++)
                {
                    UiDraw.Text(s, at, tx, ly, tw, UiStyle.Small, UiRole.Text2, m.SideLines[i], UiFlags.Centre | UiFlags.Truncate);
                    ly += LineStep;
                }
            }
            if (!string.IsNullOrEmpty(m.Person) || !string.IsNullOrEmpty(m.PersonLine))
            {
                ly += UiTokens.S5;
                if (!string.IsNullOrEmpty(m.Person))
                {
                    UiDraw.Avatar(s, at, x + (Side - PersonFace) / 2f, ly, PersonFace, m.Person, UiColour.HueOf(m.Person));
                }
                ly += PersonFace + UiTokens.S3;
                UiDraw.Text(s, at, tx, ly, tw, UiStyle.Small, UiRole.Text2, m.PersonLine, UiFlags.Centre | UiFlags.Truncate);
            }
        }

        // ------------------------------------------------------------------------------------------------- the rows

        private static float RowsHeight(UiSideCardModel m) => UiTokens.S2 * 2f + m.Rows.Length * UiTokens.PairRow;

        // The rows on a band: the label in the quiet ink at the left, the value strong at the right in its colour; a line
        // with no value reads across the band in its own colour.
        private static void Rows(UiScene s, UiAt at, UiSideCardModel m, float x, float y, float w)
        {
            int band = UiDraw.Box(s, at, x, y, w, RowsHeight(m), UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.None);
            UiAt on = at.In(band);
            float rx = x + UiTokens.S4, rw = w - UiTokens.S4 * 2f, ry = y + UiTokens.S2;
            for (int i = 0; i < m.Rows.Length; i++)
            {
                ref UiInfoRow row = ref m.Rows[i];
                UiRole ink = row.Ink == UiRole.None ? UiRole.Text : row.Ink;
                if (string.IsNullOrEmpty(row.Value))
                {
                    UiDraw.TextMiddle(s, on, rx, ry, UiTokens.PairRow, rw, UiStyle.Body, row.Ink == UiRole.None ? UiRole.Text2 : ink, row.Label, UiFlags.Truncate);
                }
                else
                {
                    float vw = Math.Min(UiDraw.SnapDown(rw * 0.6f), UiDraw.Snap(UiTextRules.Width(row.Value, UiStyle.BodyStrong) + UiTokens.S1));
                    UiDraw.TextMiddle(s, on, rx, ry, UiTokens.PairRow, rw - vw - UiTokens.S3, UiStyle.Body, UiRole.Text2, row.Label, UiFlags.Truncate);
                    UiDraw.TextMiddle(s, on, rx + rw - vw, ry, UiTokens.PairRow, vw, UiStyle.BodyStrong, ink, row.Value, UiFlags.Right | UiFlags.Truncate);
                }
                ry += UiTokens.PairRow;
            }
        }

        // ------------------------------------------------------------------------------------------------- the slider

        private static float SliderHeight(UiSideCardModel m)
        {
            float h = UiTokens.PairRow + UiTokens.PairRow;   // its words, then its track
            if (!string.IsNullOrEmpty(m.SliderFrom) || !string.IsNullOrEmpty(m.SliderTo))
            {
                h += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            }
            if (!string.IsNullOrEmpty(m.SumLeft) || !string.IsNullOrEmpty(m.SumRight))
            {
                h += UiTokens.S2 + UiTokens.PairRow;
            }
            return h;
        }

        // Its words and figure over the track, the track with the knob at its share, the ends under it, and the sums.
        private static void Slider(UiScene s, UiAt at, UiSideCardModel m, float x, float y, float w)
        {
            bool off = m.SliderDisabled;
            float vw = UiDraw.Snap(UiTextRules.Width(m.SliderValue, UiStyle.BodyStrong) + UiTokens.S1);
            UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, w - vw - UiTokens.S3, UiStyle.Body, UiRole.Text, m.SliderLabel, UiFlags.Label | UiFlags.Truncate);
            UiDraw.TextMiddle(s, at, x + w - vw, y, UiTokens.PairRow, vw, UiStyle.BodyStrong, off ? UiRole.Text2 : UiRole.Text, m.SliderValue, UiFlags.Right);
            y += UiTokens.PairRow;
            float share = m.SliderShare < 0f ? 0f : m.SliderShare > 1f ? 1f : m.SliderShare;
            int face = UiDraw.Box(s, at, x, y, w, UiTokens.PairRow, m.SliderHeld && !off && !s.Pointer ? UiRole.AccentSoft : UiRole.None, UiRole.None, UiTokens.RadiusLarge,
                off ? UiFlags.Disabled : UiFlags.None);
            UiAt inside = at.In(face);
            float tx = x + Knob / 2f, tw = w - Knob, mid = y + UiTokens.PairRow / 2f, th = UiTokens.S1;
            int track = UiDraw.Box(s, inside, tx, mid - th / 2f, tw, th, UiRole.Track, UiRole.None, th / 2f, UiFlags.Circle | UiFlags.Middle);
            if (share > 0f)
            {
                UiDraw.Box(s, inside.In(track), tx, mid - th / 2f, Math.Max(th, tw * share), th, off ? UiRole.Muted : UiRole.Accent, UiRole.None, th / 2f,
                    UiFlags.Circle | UiFlags.Share | UiFlags.Middle);
            }
            UiDraw.Circle(s, inside.In(track), tx + tw * share - Knob / 2f, mid - Knob / 2f, Knob, Knob, off ? UiRole.Muted : UiRole.Text, UiFlags.Share | UiFlags.Middle | UiFlags.Overflow);
            UiDraw.Node(s, at, x, y, w, UiTokens.PairRow, m.SliderCommand, 0, face, off);
            m.TrackX = tx;
            m.TrackW = tw;
            y += UiTokens.PairRow;
            if (!string.IsNullOrEmpty(m.SliderFrom) || !string.IsNullOrEmpty(m.SliderTo))
            {
                float ew = UiDraw.SnapDown(w / 2f);
                UiDraw.Text(s, at, x, y, ew, UiStyle.Small, UiRole.Text2, m.SliderFrom, UiFlags.Truncate);
                UiDraw.Text(s, at, x + w - ew, y, ew, UiStyle.Small, UiRole.Text2, m.SliderTo, UiFlags.Right | UiFlags.Truncate);
                y += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            }
            if (!string.IsNullOrEmpty(m.SumLeft) || !string.IsNullOrEmpty(m.SumRight))
            {
                y += UiTokens.S2;
                float rw = string.IsNullOrEmpty(m.SumRight) ? 0f : Math.Min(UiDraw.SnapDown(w * 0.5f), UiDraw.Snap(UiTextRules.Width(m.SumRight, UiStyle.BodyStrong) + UiTokens.S1));
                UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, w - rw - UiTokens.S3, UiStyle.Body, UiRole.Text, m.SumLeft, UiFlags.Truncate);
                if (rw > 0f)
                {
                    UiDraw.TextMiddle(s, at, x + w - rw, y, UiTokens.PairRow, rw, UiStyle.BodyStrong, UiRole.Text, m.SumRight, UiFlags.Right | UiFlags.Truncate);
                }
            }
        }

        // ------------------------------------------------------------------------------------------------- the choices

        private static float ChoiceHeight(ref UiSideChoice c) =>
            UiDraw.Snap(UiTokens.S3 * 2f + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.BodyStrong)) + (string.IsNullOrEmpty(c.Line) ? 0f : UiTokens.S1 + UiTextRules.LineHeight(UiStyle.Small)));

        // A choice: its number in the cyan, its name, its line under it, the game's key at its right; chosen, the selected
        // row's fill. Returns its height.
        private static float Choice(UiScene s, UiAt at, UiSideCardModel m, int index, float x, float y, float w)
        {
            ref UiSideChoice c = ref m.Choices[index];
            float h = ChoiceHeight(ref c);
            bool chosen = index == m.Selected;
            int face = UiDraw.Box(s, at, x, y, w, h, chosen ? UiRole.RowSel : UiRole.Row, UiRole.None, UiTokens.RadiusLarge,
                (chosen ? UiFlags.Selected : UiFlags.None) | (c.Disabled ? UiFlags.Disabled : UiFlags.None));
            UiAt inside = at.In(face);
            float numW = UiDraw.Snap(UiTextRules.Width("9.", UiStyle.BodyStrong) + UiTokens.S2);
            float kw = UiDraw.GameKeyWidth(c.GameKey);
            float nx = x + UiTokens.S4 + numW, nw = w - UiTokens.S4 * 2f - numW - (kw > 0f ? kw + UiTokens.S3 : 0f);
            float ny = y + UiTokens.S3, lh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.BodyStrong));
            UiRole ink = c.Disabled ? UiRole.Muted : UiRole.Text;
            UiDraw.Text(s, inside, x + UiTokens.S4, ny, numW, UiStyle.BodyStrong, c.Disabled ? UiRole.Muted : UiRole.AccentText,
                (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + ".", UiFlags.None);
            UiDraw.Text(s, inside, nx, ny, nw, UiStyle.BodyStrong, ink, c.Name, UiFlags.Label | UiFlags.Truncate);
            if (!string.IsNullOrEmpty(c.Line))
            {
                // On the chosen row's fill the grey would not read.
                UiRole lineInk = c.Disabled ? UiRole.Muted : chosen ? UiRole.Text : UiRole.Text2;
                UiDraw.Text(s, inside, nx, ny + lh + UiTokens.S1, nw, UiStyle.Small, lineInk, c.Line, UiFlags.Truncate);
            }
            UiDraw.Node(s, at, x, y, w, h, m.ChoiceCommand, index, face, c.Disabled, -1, 0, index < 9 ? (UiAction)((int)UiAction.Pick1 + index) : UiAction.Count);
            if (kw > 0f)
            {
                UiDraw.GameKey(s, inside, x + w - UiTokens.S4 - kw, y + UiDraw.SnapDown((h - UiTokens.KeyCap) / 2f), c.GameKey, false, c.Disabled);   // after the target: the game answers it
            }
            return h;
        }

        // ------------------------------------------------------------------------------------------------- the foot

        // The way back at the left, then the note; the rest of the buttons from the right edge, the main one last.
        private static void Foot(UiScene s, UiAt at, UiSideCardModel m, float x, float y, float w, IUiKeys keys)
        {
            // Too wide for one row (a long language): the way back and the note on a row of their own above the rest.
            bool split = FootSplit(m, keys);
            float left = x, right = x + w, ly = split ? y - UiTokens.Control - UiTokens.S3 : y;
            for (int i = 0; i < m.Buttons.Length; i++)
            {
                if (m.Buttons[i].Left)
                {
                    left += FootButton(s, at, m, i, left, ly, keys) + UiTokens.S3;
                }
            }
            float noteRight = split ? x + w : 0f;
            for (int pass = 0; pass < 2; pass++)
            {
                // The main button at the right edge whatever its place in the list, the others before it.
                for (int i = m.Buttons.Length - 1; i >= 0; i--)
                {
                    if (!m.Buttons[i].Left && (m.Buttons[i].Kind == UiButtonKind.Primary) == (pass == 0))
                    {
                        float bw = ButtonWidth(ref m.Buttons[i], keys);
                        right -= bw;
                        FootButton(s, at, m, i, right, y, keys);
                        right -= UiTokens.S3;
                    }
                }
            }
            if (split)
            {
                right = noteRight + UiTokens.S3;
            }
            if (!string.IsNullOrEmpty(m.Note) && right - left > UiTokens.S12)
            {
                UiDraw.TextMiddle(s, at, left, ly, UiTokens.Control, right - left - UiTokens.S3, UiStyle.Small, UiRole.Text2, m.Note, UiFlags.Truncate);
            }
        }

        // The foot's buttons too wide for one row of the card's content.
        private static bool FootSplit(UiSideCardModel m, IUiKeys keys)
        {
            if (m.Buttons == null || m.Buttons.Length < 2)
            {
                return false;
            }
            keys = keys ?? UiDefaultKeys.Keyboard;
            float all = 0f;
            for (int i = 0; i < m.Buttons.Length; i++)
            {
                all += ButtonWidth(ref m.Buttons[i], keys) + (i > 0 ? UiTokens.S3 : 0f);
            }
            return all > Inner;
        }

        /// <summary>A foot button's width: the kit's own for a key of the kit's, its words and the game's cap otherwise.</summary>
        public static float ButtonWidth(ref UiSideButton b, IUiKeys keys)
        {
            if (b.Key != UiAction.Count)
            {
                return UiDraw.ButtonWidth(b.Kind, b.Label, UiIcon.None, b.Key, keys);
            }
            float kw = UiDraw.GameKeyWidth(b.GameKey);
            return UiDraw.Snap(UiTokens.S5 * 2f + (kw > 0f ? kw + UiTokens.S2 : 0f) + UiTextRules.Width(b.Label, UiStyle.Button) + UiTokens.S1);
        }

        private static float FootButton(UiScene s, UiAt at, UiSideCardModel m, int i, float x, float y, IUiKeys keys)
        {
            ref UiSideButton b = ref m.Buttons[i];
            float w = ButtonWidth(ref b, keys);
            if (b.Key != UiAction.Count)
            {
                UiDraw.Button(s, at, x, y, w, UiTokens.Control, b.Kind, b.Label, UiIcon.None, b.Key, keys, m.ButtonCommand, i, b.Disabled);
                return w;
            }
            GameButton(s, at, x, y, w, UiTokens.Control, b.Kind, b.Label, b.GameKey, m.ButtonCommand, i, b.Disabled);
            return w;
        }

        /// <summary>
        /// A button whose key the game reads: its face, the game's key on its cap and its words. The target owns no key, so
        /// the kit never presses it from the keyboard: the game does. Returns the target.
        /// </summary>
        public static int GameButton(UiScene s, UiAt at, float x, float y, float w, float h, UiButtonKind kind, string label, UiGameKey key, int command, int arg, bool disabled)
        {
            int first = s.ItemCount;
            bool primary = kind == UiButtonKind.Primary;
            UiRole fill = primary ? UiRole.Accent : kind == UiButtonKind.Quiet ? UiRole.None : UiRole.Field;
            UiRole ink = primary ? UiRole.OnAccent : kind == UiButtonKind.Destructive ? UiRole.DangerText : UiRole.Text;
            int face = UiDraw.Box(s, at, x, y, w, h, fill, UiRole.None, UiTokens.RadiusLarge, disabled ? UiFlags.Disabled : UiFlags.None);
            UiAt inside = at.In(face);
            float kw = UiDraw.GameKeyWidth(key);
            float lw = UiTextRules.Width(label, UiStyle.Button) + UiTokens.S1;
            float content = (kw > 0f ? kw + UiTokens.S2 : 0f) + lw;
            float cx = x + Math.Max(UiTokens.S3, UiDraw.SnapDown((w - content) / 2f));
            UiDraw.TextMiddle(s, inside, cx + (kw > 0f ? kw + UiTokens.S2 : 0f), y, h, Math.Min(lw, x + w - UiTokens.S3 - cx), UiStyle.Button, ink, label,
                UiFlags.Label | (disabled ? UiFlags.Disabled : UiFlags.None));
            int node = UiDraw.Node(s, at, x, y, w, h, command, arg, face, disabled);
            UiDraw.GameKey(s, inside, cx, y + (h - UiTokens.KeyCap) / 2f, key, primary, disabled);   // after the target: the game, not the button, answers it
            for (int i = first; disabled && i < s.ItemCount; i++)
            {
                s.Items[i].Alpha *= HeldAlpha;
            }
            return node;
        }
    }
}
