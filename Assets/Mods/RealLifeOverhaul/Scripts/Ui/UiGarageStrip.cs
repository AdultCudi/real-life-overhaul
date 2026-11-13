using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A card of the garage shop's strip: an entry with its words and its button, or a note of words alone.</summary>
    public struct UiGarageCard
    {
        public string Name;
        public string Detail;
        public string Button;      // the button's words, or null for a note
        public bool Enabled;
        public bool Note;          // words alone (the figures page's lines)
        public bool Main;          // the shop's one main action (Pay): on the cyan, on F
        public bool Opens;         // the row opens a page of its own (Paint, Finish): a chevron in place of a button, the row the target
        public bool Chosen;        // the option is the one in the cart: its button's words stand as a chip, the row not pressable
        public bool Back;          // the way back to the shop's first page: its glyph at the left, the row the target
        public string Swatch;      // a paint's colour (UiDraw.SwatchKey, or the game's swatch sprite) on a tile at the left; null for none
    }

    /// <summary>What the garage shop's strip shows: its headline, the lines under it and the page's cards.</summary>
    public sealed class UiGarageStripModel
    {
        public string Title;
        public UiIcon Icon = UiIcon.Wrench;
        public string[] Info = new string[0];                  // the money, the bill, what the cart does to the car; each line wraps
        public int InfoCount;
        public UiGarageCard[] Cards = new UiGarageCard[0];
        public int CardCount;
        public float Offset;
        public int Reveal = -1;
        public string Leave;
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// The garage shop (the mod's custom shop, Garage/GarageShop): a catalogue strip docked at the left while the car
    /// turns on its turntable right of the middle: the strip's title, the money and the bill, then the page's cards in a
    /// column that scrolls, each its name over its detail with its button at the right (Pay on the cyan), the figures
    /// page's notes as cards of words. Rows that open a page carry a chevron, a paint its colour on a tile, the way back its
    /// glyph, and only Pay a button, on F. The strip ends under its last row. Commands: <see cref="CardCommand"/> with the
    /// card's index, the strip's close for leaving. Game-free. Cost: a layout a change, the cards in view only.
    /// </summary>
    public static class UiGarageStrip
    {
        public const int CardCommand = 650;
        public static readonly int[] Commands = { CardCommand };
        public const float Width = UiInfoPanel.Width;
        private const float ButtonW = 128f;
        public const int DetailLines = 8;      // a part's effects run long: its row grows to hold them rather than cut them
        private const int InfoLines = 4;

        public static void Lay(UiScene s, UiGarageStripModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("garage_shop", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            Draw(s, m, UiTokens.S4, UiTokens.S4, UiDraw.SnapDown(frame.BandY - UiTokens.S4 * 2f), keys);
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        /// <summary>The page's buttons' width: the widest of their words, never under the strip's own nor over a third of it.</summary>
        public static float ButtonWidth(UiGarageStripModel m, float w)
        {
            float most = ButtonW;
            for (int i = 0; i < m.CardCount; i++)
            {
                if (!m.Cards[i].Note && !string.IsNullOrEmpty(m.Cards[i].Button))
                {
                    most = Math.Max(most, UiDraw.Snap(UiTextRules.Width(m.Cards[i].Button, UiStyle.Button) + UiTokens.S2 * 2f + UiTokens.S1));
                }
            }
            return Math.Min(most, UiDraw.SnapDown(w / 3f));
        }

        public static float CardHeight(ref UiGarageCard c, float w, float bw)
        {
            float tw = TextWidth(ref c, w, bw);
            float text = UiTextRules.LineHeight(UiStyle.Title) + (string.IsNullOrEmpty(c.Detail) ? 0f : UiTokens.S1 + UiTextRules.WrappedHeight(c.Detail, UiStyle.Small, tw, DetailLines));
            return UiDraw.Snap(Math.Max(UiTokens.Row, text + UiTokens.S3 * 2f));
        }

        /// <summary>One info line's height: the first (the money and the bill) in the strong style, the rest small, each as many lines as its words need.</summary>
        public static float InfoLineHeight(string line, int index, float w)
        {
            UiStyle style = index == 0 ? UiStyle.BodyStrong : UiStyle.Small;
            return UiDraw.Snap(Math.Max(UiTokens.PairRow, UiTextRules.WrappedHeight(line ?? string.Empty, style, w, InfoLines)));
        }

        /// <summary>The band under the title: its lines wrapped to the strip's width, with a little air above and below.</summary>
        public static float InfoHeight(UiGarageStripModel m, float w)
        {
            if (m.InfoCount <= 0)
            {
                return 0f;
            }
            float h = UiTokens.S2 * 2f;
            for (int i = 0; i < m.InfoCount; i++)
            {
                h += InfoLineHeight(m.Info[i], i, w);
            }
            return UiDraw.Snap(h);
        }

        public static void Draw(UiScene s, UiGarageStripModel m, float x, float y, float h, IUiKeys keys)
        {
            float w = Width;
            float cw = w - UiTokens.S4 * 2f - UiTokens.S3, cbw = ButtonWidth(m, cw), rows = 0f;
            for (int i = 0; i < m.CardCount; i++)
            {
                rows += CardHeight(ref m.Cards[i], cw, cbw) + UiTokens.S2;
            }
            float head = UiFrame.Strip + InfoHeight(m, w - UiTokens.S4 * 2f);
            h = Math.Min(h, UiDraw.Snap(head + UiTokens.S3 + Math.Max(0f, rows - UiTokens.S2) + UiTokens.S3));   // the strip ends under its last row
            int panel = UiDraw.Box(s, UiAt.Screen, x, y, w, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = UiAt.Screen.In(panel);
            float cy = UiDraw.TitleStrip(s, on, x, y, w, m.Icon, m.Title, !string.IsNullOrEmpty(m.Leave));
            float ix = x + UiTokens.S4, iw = w - UiTokens.S4 * 2f;
            if (m.InfoCount > 0)
            {
                float bh = InfoHeight(m, iw), ly = cy + UiTokens.S2;
                UiDraw.Band(s, on, x, cy, w, bh);
                for (int i = 0; i < m.InfoCount; i++)
                {
                    float lh = InfoLineHeight(m.Info[i], i, iw);
                    UiDraw.Text(s, on, ix, ly, iw, i == 0 ? UiStyle.BodyStrong : UiStyle.Small, i == 0 ? UiRole.Text : UiRole.Text2, m.Info[i], UiFlags.Wrap, InfoLines);
                    ly += lh;
                }
                cy += bh;
            }
            float top = cy + UiTokens.S3, bottom = y + h - UiTokens.S3;
            Cards(s, on, m, ix, top, iw - UiTokens.S3, bottom - top, keys);
        }

        // The cards in a column that scrolls in its frame; only those in view are laid out, walked up and down as a list.
        private static void Cards(UiScene s, UiAt at, UiGarageStripModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            float bw = ButtonWidth(m, w), content = 0f;
            for (int i = 0; i < m.CardCount; i++)
            {
                content += CardHeight(ref m.Cards[i], w, bw) + UiTokens.S2;
            }
            content = Math.Max(0f, content - UiTokens.S2);
            float offset = m.Offset;
            if (m.Reveal >= 0 && m.Reveal < m.CardCount)
            {
                float top = 0f;
                for (int i = 0; i < m.Reveal; i++)
                {
                    top += CardHeight(ref m.Cards[i], w, bw) + UiTokens.S2;
                }
                offset = Math.Min(offset, top);
                offset = Math.Max(offset, top + CardHeight(ref m.Cards[m.Reveal], w, bw) - h);
            }
            offset = Math.Max(0f, Math.Min(offset, content - h));
            m.Offset = offset;
            m.Reveal = -1;
            short clip = s.AddClip(x, y, w, h, offset);
            s.Clips[clip].ContentHeight = content;
            s.Clips[clip].Fills = true;   // a short page's few cards stand at the strip's top; the room under them is the strip's own
            int first = -1, last = -1;
            float cy = 0f;
            for (int i = 0; i < m.CardCount; i++)
            {
                float ch = CardHeight(ref m.Cards[i], w, bw);
                if (cy + ch > offset && cy < offset + h)
                {
                    first = first < 0 ? i : first;
                    last = i;
                }
                cy += ch + UiTokens.S2;
            }
            short list = s.AddList(m.CardCount, Math.Max(0, first), first < 0 ? 0 : last - first + 1, clip, -1);
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            cy = 0f;
            for (int i = 0; i < m.CardCount; i++)
            {
                ref UiGarageCard c = ref m.Cards[i];
                float ch = CardHeight(ref c, w, bw);
                if (i >= first && i <= last && first >= 0)
                {
                    Card(s, inClip.Arriving(i - first), ref c, i, 0f, cy, w, ch, bw, list, keys);
                }
                cy += ch + UiTokens.S2;
            }
            UiDraw.ScrollThumb(s, at, x + w + UiTokens.S1, y, h, offset, content);
        }

        // A row's words' room: the row less its lead (a colour, the way back's glyph) and what stands at its right.
        private static float TextWidth(ref UiGarageCard c, float w, float bw) => w - UiTokens.S3 * 2f - Lead(ref c) - Trail(ref c, bw);

        private static float Lead(ref UiGarageCard c) => !string.IsNullOrEmpty(c.Swatch) ? UiTokens.ControlSmall + UiTokens.S3 : c.Back ? UiTokens.Icon + UiTokens.S3 : 0f;

        private static float Trail(ref UiGarageCard c, float bw) => c.Note || c.Back ? 0f : c.Opens ? UiTokens.IconSmall + UiTokens.S3
            : string.IsNullOrEmpty(c.Button) ? 0f : bw + UiTokens.S3;

        // A row: a paint's colour or the way back's glyph at the left, its name over its detail, and at the right a chevron for
        // a page it opens, Pay's button on F, a chosen paint's chip or a quiet button. Rows stand on the strip with a hairline
        // between them; a note is words alone on the slate tile.
        private static void Card(UiScene s, UiAt at, ref UiGarageCard c, int index, float x, float y, float w, float h, float bw, short list, IUiKeys keys)
        {
            int drawn = s.ItemCount;
            int face = UiDraw.Box(s, at, x, y, w, h, c.Note ? UiRole.Tile : UiRole.None, UiRole.None, UiTokens.RadiusLarge, c.Note || c.Enabled ? UiFlags.None : UiFlags.Disabled);
            UiAt inside = at.In(face);
            if (!c.Note && index > 0)
            {
                UiDraw.Box(s, at, x + UiTokens.S3, y - UiTokens.S1, w - UiTokens.S3 * 2f, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);   // between rows
            }
            float lx = x + UiTokens.S3, d = UiTokens.ControlSmall;
            if (!string.IsNullOrEmpty(c.Swatch))
            {
                int tile = UiDraw.Box(s, inside, lx, y + (h - d) / 2f, d, d, UiRole.Tile, UiRole.None, UiTokens.RadiusSmall);
                var card = new UiCatalogueCard { Picture = c.Swatch, Aspect = 1f };
                UiCatalogue.Picture(s, inside.In(tile), ref card, lx, y + (h - d) / 2f, d, d, UiTokens.S4);
            }
            else if (c.Back)
            {
                UiDraw.Icon(s, inside, lx, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Back, UiRole.AccentText, UiFlags.Middle);
            }
            float tx = lx + Lead(ref c), tw = TextWidth(ref c, w, bw);
            bool detail = !string.IsNullOrEmpty(c.Detail);
            float block = UiTextRules.LineHeight(UiStyle.Title) + (detail ? UiTokens.S1 + UiTextRules.WrappedHeight(c.Detail, UiStyle.Small, tw, DetailLines) : 0f);
            float ty = y + UiDraw.SnapDown((h - block) / 2f);   // the pair centred on the row, its top on the grid
            UiDraw.Text(s, inside, tx, ty, tw, UiStyle.Title, c.Enabled || c.Note ? UiRole.Text : UiRole.Text2, c.Name, UiFlags.Truncate);
            if (detail)
            {
                UiDraw.Text(s, inside, tx, ty + UiTextRules.LineHeight(UiStyle.Title) + UiTokens.S1, tw, UiStyle.Small, UiRole.Text2, c.Detail, UiFlags.Wrap | UiFlags.Middle, DetailLines);
            }
            if (c.Note)
            {
                return;
            }
            float right = x + w - UiTokens.S3;
            if (c.Main && !string.IsNullOrEmpty(c.Button))
            {
                // Pay: the shop's one main action, a target of its own on F.
                UiDraw.Button(s, inside, right - bw, y + (h - UiTokens.Control) / 2f, bw, UiTokens.Control, UiButtonKind.Primary, c.Button, UiIcon.None, UiAction.Alt, keys,
                    CardCommand, index, !c.Enabled);
                return;
            }
            if (c.Opens)
            {
                UiDraw.Icon(s, inside, right - UiTokens.IconSmall, y + (h - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Next, UiRole.Text2, UiFlags.Middle);
            }
            else if (!c.Back && !string.IsNullOrEmpty(c.Button))
            {
                if (c.Chosen || (!c.Enabled && !string.IsNullOrEmpty(c.Swatch)))
                {
                    // The option the cart holds says so as a chip, not as a greyed button.
                    float cw = Math.Min(bw, UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(c.Button, UiStyle.SmallStrong) + UiTokens.S1));
                    UiDraw.Chip(s, inside, right - cw, y + (h - UiTokens.Badge - UiTokens.S1) / 2f, c.Button, UiRole.Ok, cw);
                }
                else
                {
                    float by = y + (h - UiTokens.ControlSmall) / 2f, bx = right - bw;
                    int bface = UiDraw.Box(s, inside, bx, by, bw, UiTokens.ControlSmall, UiRole.Field, UiRole.None, UiTokens.RadiusLarge, UiFlags.Middle);
                    UiDraw.TextMiddle(s, inside.In(bface), bx + UiTokens.S2, by, UiTokens.ControlSmall, bw - UiTokens.S2 * 2f, UiStyle.Button, UiRole.Text, c.Button,
                        UiFlags.Centre | UiFlags.Truncate | UiFlags.Label);
                }
            }
            int node = UiDraw.Node(s, at, x, y, w, h, CardCommand, index, face, !c.Enabled, list, index);
            if (!c.Enabled && (c.Chosen || !string.IsNullOrEmpty(c.Swatch)))
            {
                return;   // the option in the cart stays in its colour: it is chosen, not refused
            }
            if (!c.Enabled)
            {
                UiDraw.Dim(s, drawn, node);
            }
        }
    }
}
