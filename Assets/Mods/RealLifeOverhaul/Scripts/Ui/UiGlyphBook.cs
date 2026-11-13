using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Which characters of a text the interface's face lacks and have not yet been given their font, game-free. A text in
    /// Chinese, Korean or Greek, or a name a player typed in any script, is drawn by a fallback font whose atlas takes a
    /// character in when it is first asked for. A text laid out and drawn before its glyphs were in the atlas drew its
    /// Latin letters and left the rest blank until something wrote the text again (a player's pictures, 13 Nov 2026: the
    /// BizMan table's names, districts and states blank until the list was scrolled). The book answers, for each text
    /// that is about to be drawn, whether it holds such a character and which of them are new, so the kit asks the fonts
    /// for them first (UiFonts.Need) and knows which texts to draw again if glyphs arrive later. A character is asked once:
    /// its answer is kept in a table of 65,536 bytes, the characters beyond the basic plane in a set. Cost: a byte read a
    /// character of a text that changed; nothing a frame.
    /// </summary>
    public sealed class UiGlyphBook
    {
        private const byte Unasked = 0, Drawn = 1, Given = 2;

        private readonly byte[] _state = new byte[65536];
        private readonly HashSet<uint> _high = new HashSet<uint>();

        /// <summary>
        /// True when the text holds a character the face does not draw and that is no mark without a glyph. The characters
        /// that no font has been asked for yet are added to <paramref name="fresh"/> (each once, in order).
        /// </summary>
        public bool Beyond(string text, UiFace face, List<uint> fresh)
        {
            bool beyond = false;
            for (int i = 0; text != null && i < text.Length; i++)
            {
                char c = text[i];
                if (c < 0x80)
                {
                    continue;   // the face draws every character of plain ASCII
                }
                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    uint code = (uint)char.ConvertToUtf32(c, text[i + 1]);
                    i++;
                    beyond = true;
                    if (!_high.Contains(code) && !fresh.Contains(code))
                    {
                        fresh.Add(code);
                    }
                    continue;
                }
                byte state = _state[c];
                if (state == Unasked)
                {
                    if (Silent(c) || face.Advance(c) >= 0)
                    {
                        _state[c] = Drawn;
                        continue;
                    }
                    beyond = true;
                    if (!fresh.Contains(c))
                    {
                        fresh.Add(c);
                    }
                }
                else if (state == Given)
                {
                    beyond = true;
                }
            }
            return beyond;
        }

        /// <summary>The characters were asked of the fonts: they are not asked again.</summary>
        public void Asked(List<uint> characters)
        {
            for (int i = 0; i < characters.Count; i++)
            {
                uint code = characters[i];
                if (code > 0xFFFF)
                {
                    _high.Add(code);
                }
                else
                {
                    _state[code] = Given;
                }
            }
        }

        /// <summary>The fonts changed (a copy came, the language changed): every character may find another font, so each is asked again.</summary>
        public void Forget()
        {
            System.Array.Clear(_state, 0, _state.Length);
            _high.Clear();
        }

        /// <summary>How many characters beyond the face were asked of the fonts, for the checks.</summary>
        public int Count
        {
            get
            {
                int n = _high.Count;
                for (int c = 0; c < _state.Length; c++)
                {
                    n += _state[c] == Given ? 1 : 0;
                }
                return n;
            }
        }

        /// <summary>A character that is no glyph of its own: a zero width mark, a joiner, a variation selector, a control.</summary>
        public static bool Silent(char c)
        {
            return c < 0xA0 || (c >= 0x200B && c <= 0x200F) || (c >= 0x2028 && c <= 0x202E) || c == 0x2060 || c == 0xFEFF || (c >= 0xFE00 && c <= 0xFE0F);
        }
    }
}
