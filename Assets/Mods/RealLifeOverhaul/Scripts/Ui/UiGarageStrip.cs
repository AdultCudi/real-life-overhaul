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
        public bool Main;          // the shop's one main action (Pay): on the cyan
    }

    /// <summary>What the garage shop's strip shows: its headline, the lines under it and the page's cards.</summary>
    public sealed class UiGarageStripModel
    {
        public string Title;
        public UiIcon Icon = UiIcon.Wrench;
        public string[] Info = new string[0];                  // the money, the bill, what the cart does to the car
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
    /// page's notes as cards of words. Commands: <see cref="CardCommand"/> with the card's index, the strip's close for
    /// leaving. Game-free. Cost: a layout a change, the cards in view only.
    /// </summary>
    public static class UiGarageStrip
    {
        public const int CardCommand = 650;
        public static readonly int[] Commands = { CardCommand };
        public const float Width = UiInfoPanel.Width;
        private const float ButtonW = 128f;

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
            float tw = c.Note || string.IsNullOrEmpty(c.Button) ? w - UiTokens.S3 * 2f : w - UiTokens.S3 * 3f - bw;
            float text = UiTextRules.LineHeight(UiStyle.Title) + (string.IsNullOrEmpty(c.Detail) ? 0f : UiTokens.S1 + UiTextRules.WrappedHeight(c.Detail, UiStyle.Small, tw, 4));
            return UiDraw.Snap(Math.Max(UiTokens.Row, text + UiTokens.S3 * 2f));
        }

        public static void Draw(UiScene s, UiGarageStripModel m, float x, float y, float h, IUiKeys keys)
        {
            float w = Width;
            int panel = UiDraw.Box(s, UiAt.Screen, x, y, w, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = UiAt.Screen.In(panel);
            float cy = UiDraw.TitleStrip(s, on, x, y, w, m.Icon, m.Title, !string.IsNullOrEmpty(m.Leave));
            float ix = x + UiTokens.S4, iw = w - UiTokens.S4 * 2f;
            if (m.InfoCount > 0)
            {
                float bh = UiTokens.S2 * 2f + m.InfoCount * UiTokens.PairRow;
                UiDraw.Band(s, on, x, cy, w, bh);
                for (int i = 0; i < m.InfoCount; i++)
                {
                    UiDraw.TextMiddle(s, on, ix, cy + UiTokens.S2 + i * UiTokens.PairRow, UiTokens.PairRow, iw, i == 0 ? UiStyle.BodyStrong : UiStyle.Small, i == 0 ? UiRole.Text : UiRole.Text2,
                        m.Info[i], UiFlags.Truncate);
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

        // A card: its name over its detail on the slate tile, its button at the right; a note is words alone.
        private static void Card(UiScene s, UiAt at, ref UiGarageCard c, int index, float x, float y, float w, float h, float bw, short list, IUiKeys keys)
        {
            int face = UiDraw.Box(s, at, x, y, w, h, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge, c.Note || c.Enabled ? UiFlags.None : UiFlags.Disabled);
            UiAt inside = at.In(face);
            bool button = !c.Note && !string.IsNullOrEmpty(c.Button);
            float tw = button ? w - UiTokens.S3 * 3f - bw : w - UiTokens.S3 * 2f;
            float ty = y + UiTokens.S3;
            UiDraw.Text(s, inside, x + UiTokens.S3, ty, tw, UiStyle.Title, c.Enabled || c.Note ? UiRole.Text : UiRole.Text2, c.Name, UiFlags.Truncate);
            if (!string.IsNullOrEmpty(c.Detail))
            {
                // Under the name by its line.
                UiDraw.Text(s, inside, x + UiTokens.S3, ty + UiTextRules.LineHeight(UiStyle.Title) + UiTokens.S1, tw, UiStyle.Small, UiRole.Text2, c.Detail,
                    UiFlags.Wrap | UiFlags.Middle, 4);
            }
            if (button)
            {
                int drawn = s.ItemCount;
                float by = y + (h - UiTokens.ControlSmall) / 2f, bx = x + w - UiTokens.S3 - bw;
                int bface = UiDraw.Box(s, inside, bx, by, bw, UiTokens.ControlSmall, c.Main && c.Enabled ? UiRole.Accent : UiRole.Field, UiRole.None, UiTokens.RadiusLarge,
                    (c.Enabled ? UiFlags.None : UiFlags.Disabled) | UiFlags.Middle);
                UiDraw.TextMiddle(s, inside.In(bface), bx + UiTokens.S2, by, UiTokens.ControlSmall, bw - UiTokens.S2 * 2f, UiStyle.Button, c.Main && c.Enabled ? UiRole.OnAccent : UiRole.Text,
                    c.Button, UiFlags.Centre | UiFlags.Truncate | UiFlags.Label);
                int node = UiDraw.Node(s, at, x, y, w, h, CardCommand, index, face, !c.Enabled, list, index);
                if (!c.Enabled)
                {
                    UiDraw.Dim(s, drawn, node);
                }
            }
        }
    }
}
