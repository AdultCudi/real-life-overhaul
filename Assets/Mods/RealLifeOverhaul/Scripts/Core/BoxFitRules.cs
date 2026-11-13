using System;
using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>The game's two question boxes: the grey one of its main menu and HUD, and the white one over its full menu.</summary>
    public enum ConfirmBox
    {
        Hud,
        FullMenu
    }

    /// <summary>
    /// How much text the game's question box has room for. Its panel is as tall as its text (a ContentSizeFitter set to the
    /// preferred height, with no limit) and stands centred, so a text too long pushes its heading off the top of the screen
    /// and its buttons off the foot, while the dimmed sheet behind it takes every click: a player on Build 3670 was locked out
    /// of the main menu that way by a list of every part of the mod that was off. So every text for the box is measured here
    /// first, and one too long is shortened or cut.
    /// THE SUMS are the game's own, read from its scenes (Build 3682: HudConfirm in level1 and level4, FullMenuConfirm in
    /// level4). The canvas is 2160 units high over the UI zoom, whatever the screen (CanvasScaler 3840 by 2160 over the zoom,
    /// matched to the height). A line of text is counted wide on purpose: the narrowest box seen, a picture of Build 3670's,
    /// and a wide letter for every letter.
    /// Game-free, checked by EconomyCheck (OldBuildChecks.cs). COST: one pass over a text, only when a question is asked.
    /// </summary>
    public static class BoxFitRules
    {
        public const float CanvasHigh = 2160f;       // units: the canvas's height at a UI zoom of 1
        public const float ZoomLeast = 0.5f;         // the game's Options slider (Scenes.MainMenu/Options: minUIZooming, maxUIZooming)
        public const float ZoomMost = 2f;
        public const float Margin = 24f;             // units kept clear between a box and the screen's edge
        public const float FontSize = 36f;           // units: the body text of both boxes
        public const float LinePitch = 42.84f;       // units from one line to the next: 1.19 of the font size, as measured in the game
        public const float LetterWide = 0.56f;       // of the font size, for a Latin or Cyrillic letter or a space: the game's font averages 0.5
        public const float SignWide = 1f;            // of the font size, for a Chinese, Japanese or Korean sign

        public const float HudWide = 1000f;          // units: the grey box's panel
        public const float HudTextWide = 684f;       // units: its text as Build 3670 drew it (887 on Build 3682: 1000 less 59 and 54 of padding)
        public const float HudAbove = 113f;          // units of padding over the text: the heading stands in it
        public const float HudBelow = 167f;          // units of padding under the text: the buttons stand in it
        public const float HudTextLeast = 200f;      // units: the text's least height
        public const float HudButtonsUp = 53.4f;     // units from the panel's foot to the foot of its buttons
        public const float HudButtonsHigh = 80f;     // units

        public const float MenuWide = 1223.31f;      // units: the white box's panel
        public const float MenuTextWide = 1003.31f;  // units: its text (the panel less 110 of padding on each side)
        public const float MenuAbove = 100f;         // units of padding over the heading
        public const float MenuBelow = 142f;         // units of padding under the buttons
        public const float MenuGap = 29.12f;         // units between heading, text and buttons
        public const float MenuHeading = 71.4f;      // units: one line of the heading's 60
        public const float MenuTextEdges = 36f;      // units: the text's own margins, 2 over and 34 under
        public const float MenuButtonsHigh = 65.04f; // units
        public const float MenuRaised = 69.03f;      // units the white box stands above the middle of the screen

        /// <summary>The UI zoom held to the game's slider; a number that is none counts as the largest, which leaves the least room.</summary>
        public static float Zoom(float zoom)
        {
            if (float.IsNaN(zoom) || zoom <= 0f)
            {
                return ZoomMost;
            }
            return Math.Min(ZoomMost, Math.Max(ZoomLeast, zoom));
        }

        /// <summary>The canvas's height in units at a UI zoom.</summary>
        public static float Canvas(float zoom)
        {
            return CanvasHigh / Zoom(zoom);
        }

        /// <summary>The width of a box's text, in units.</summary>
        public static float TextWide(ConfirmBox box)
        {
            return box == ConfirmBox.Hud ? HudTextWide : MenuTextWide;
        }

        /// <summary>A box's height in units with so many lines of text.</summary>
        public static float High(ConfirmBox box, int lines)
        {
            float text = Math.Max(0, lines) * LinePitch;
            return box == ConfirmBox.Hud
                ? HudAbove + Math.Max(HudTextLeast, text) + HudBelow
                : MenuAbove + MenuHeading + MenuGap + MenuTextEdges + text + MenuGap + MenuButtonsHigh + MenuBelow;
        }

        /// <summary>The most lines of text with which a box, its heading and its buttons stay on the screen at a UI zoom.</summary>
        public static int Room(ConfirmBox box, float zoom)
        {
            float free = Canvas(zoom) - 2f * Margin - (box == ConfirmBox.FullMenu ? 2f * MenuRaised : 0f);
            return Math.Max(0, (int)Math.Floor((free - High(box, 0) + (box == ConfirmBox.Hud ? HudTextLeast : 0f)) / LinePitch));
        }

        /// <summary>The width of a piece of text on one line, in font sizes.</summary>
        public static float Wide(string text)
        {
            float wide = 0f;
            for (int i = 0; text != null && i < text.Length; i++)
            {
                wide += Wide(text[i]);
            }
            return wide;
        }

        private static float Wide(char c)
        {
            return Sign(c) ? SignWide : LetterWide;
        }

        // A sign of Chinese, Japanese or Korean, or a full-width form: as wide as the font is high, and a line may break after any.
        private static bool Sign(char c)
        {
            return (c >= 0x1100 && c <= 0x11FF) || (c >= 0x2E80 && c <= 0x9FFF) || (c >= 0xAC00 && c <= 0xD7AF) || (c >= 0xF900 && c <= 0xFAFF)
                   || (c >= 0xFF00 && c <= 0xFFEF);
        }

        /// <summary>
        /// The lines a text takes in a column <paramref name="wide"/> units across: a new line at each line break, words kept
        /// whole where they fit, a word wider than the column broken, signs broken anywhere. Tags are counted as letters, which
        /// only counts more.
        /// </summary>
        public static int Lines(string text, float wide)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }
            float column = Math.Max(1f, wide / FontSize);
            int lines = 1;
            float used = 0f, word = 0f;
            for (int i = 0; i <= text.Length; i++)
            {
                char c = i < text.Length ? text[i] : '\n';
                bool sign = i < text.Length && Sign(c);
                if (c != '\n' && c != ' ' && !sign)
                {
                    word += LetterWide;
                    continue;
                }
                // The word before this place, then the place itself.
                while (word > 0f)
                {
                    if (used > 0f && used + word > column)
                    {
                        lines++;
                        used = 0f;
                    }
                    float put = Math.Min(word, column);
                    used += put;
                    word -= put;
                    if (word > 0f)
                    {
                        lines++;
                        used = 0f;
                    }
                }
                if (c == '\n')
                {
                    if (i < text.Length)
                    {
                        lines++;
                        used = 0f;
                    }
                }
                else if (sign)
                {
                    if (used + SignWide > column)
                    {
                        lines++;
                        used = 0f;
                    }
                    used += SignWide;
                }
                else if (used > 0f)
                {
                    used += LetterWide;   // a space at the start of a line takes no room
                }
            }
            return lines;
        }

        /// <summary>
        /// True when the grey box holds the text on the screen at this UI zoom, and with <paramref name="menuToo"/> the white
        /// box too (in a city either may be the one that opens).
        /// </summary>
        public static bool Fits(string text, float zoom, bool menuToo)
        {
            return Lines(text, HudTextWide) <= Room(ConfirmBox.Hud, zoom)
                   && (!menuToo || Lines(text, MenuTextWide) <= Room(ConfirmBox.FullMenu, zoom));
        }

        /// <summary>
        /// The text if it fits; else its beginning, as many whole words as fit with <paramref name="tail"/> after them (the
        /// words that say the rest is in Player.log), or as many letters where its first word alone is too long. Never longer
        /// than the text it was given.
        /// </summary>
        public static string Cut(string text, float zoom, bool menuToo, string tail)
        {
            if (string.IsNullOrEmpty(text) || Fits(text, zoom, menuToo))
            {
                return text;
            }
            tail = tail ?? string.Empty;
            var ends = new List<int>(text.Length / 5 + 1);
            for (int i = 1; i <= text.Length; i++)
            {
                if (i == text.Length || text[i] == ' ' || text[i] == '\n' || Sign(text[i - 1]))
                {
                    ends.Add(i);
                }
            }
            var piece = new StringBuilder(text.Length + tail.Length + 1);
            int end = Longest(piece, text, ends, zoom, menuToo, tail);
            if (end == 0)
            {
                ends.Clear();
                for (int i = 1; i <= text.Length; i++)
                {
                    ends.Add(i);
                }
                end = Longest(piece, text, ends, zoom, menuToo, tail);
            }
            return end == 0 ? tail : Joined(piece, text, end, tail);
        }

        // The last of the places to end at with which the beginning and the tail fit, found by halving; 0 when none does.
        private static int Longest(StringBuilder piece, string text, List<int> ends, float zoom, bool menuToo, string tail)
        {
            int low = 0, high = ends.Count - 1, best = 0;
            while (low <= high)
            {
                int middle = (low + high) / 2;
                if (Fits(Joined(piece, text, ends[middle], tail), zoom, menuToo))
                {
                    best = ends[middle];
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }
            return best;
        }

        private static string Joined(StringBuilder piece, string text, int end, string tail)
        {
            piece.Length = 0;
            piece.Append(text, 0, end);
            while (piece.Length > 0 && (piece[piece.Length - 1] == ' ' || piece[piece.Length - 1] == '\n'))
            {
                piece.Length--;
            }
            if (tail.Length > 0)
            {
                piece.Append(' ').Append(tail);
            }
            return piece.ToString();
        }

        /// <summary>
        /// Where a box stands on a screen, in pixels from its top left corner: the panel, and the strip its buttons are in.
        /// For the checks, which hold both inside the screen.
        /// </summary>
        public static void Place(ConfirmBox box, int lines, float zoom, int screenWide, int screenHigh,
            out float left, out float top, out float right, out float bottom, out float buttonsTop, out float buttonsBottom)
        {
            float perUnit = screenHigh / Canvas(zoom);
            float high = High(box, lines) * perUnit;
            float wide = (box == ConfirmBox.Hud ? HudWide : MenuWide) * perUnit;
            float middle = screenHigh * 0.5f - (box == ConfirmBox.FullMenu ? MenuRaised * perUnit : 0f);
            left = screenWide * 0.5f - wide * 0.5f;
            right = left + wide;
            top = middle - high * 0.5f;
            bottom = top + high;
            float foot = box == ConfirmBox.Hud ? HudButtonsUp : MenuBelow;
            float buttons = box == ConfirmBox.Hud ? HudButtonsHigh : MenuButtonsHigh;
            buttonsBottom = bottom - foot * perUnit;
            buttonsTop = buttonsBottom - buttons * perUnit;
        }
    }
}
