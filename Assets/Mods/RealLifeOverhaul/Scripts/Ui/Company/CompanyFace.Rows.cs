using System;

namespace RealLifeOverhaul.Ui
{
    public static partial class CompanyFace
    {
        /// <summary>
        /// Rows of one height that scroll in a region of their own when they do not all fit: only those in view are laid out, the wheel and a drag of the mouse move them through the layout's
        /// slot, and the pad reaches the rest through the list. Where they all fit nothing scrolls and the rows stand where the page puts them.
        /// </summary>
        internal struct CompanyRows
        {
            public UiAt At;            // where the rows are drawn: the page's, or the region's
            public int First, Last;    // the rows to draw, both included (Last is below First when there are none)
            public short List;         // the scene's list while it scrolls, else -1
            public bool Scrolls;
            public float OriginX, OriginY, RowH, ViewH, Content, Offset, ThumbX;

            /// <summary>Where a column the page names (its own x) stands in what is drawn.</summary>
            public float X(float pageX) => Scrolls ? pageX - OriginX : pageX;

            /// <summary>The top of row <paramref name="i"/> in what is drawn.</summary>
            public float Y(int i) => (Scrolls ? 0f : OriginY) + i * RowH;
        }

        /// <summary>
        /// Begins <paramref name="count"/> rows of <paramref name="rowH"/> from <paramref name="y"/>, of which <paramref name="fit"/> have room. The region reaches <paramref name="bleed"/> past the
        /// column to each side, for rows whose faces stand out of it. End it with <see cref="EndRows"/>.
        /// </summary>
        internal static CompanyRows BeginRows(UiScene s, UiAt at, CompanyLayout lay, int slot, float x, float y, float width, float rowH, int count, int fit, float bleed = 0f)
        {
            int shown = Math.Max(0, Math.Min(count, fit));
            var r = new CompanyRows
            {
                At = at, First = 0, Last = shown - 1, List = -1, OriginX = x - bleed, OriginY = y, RowH = rowH, ViewH = shown * rowH, Content = count * rowH, ThumbX = x + width - UiTokens.S1
            };
            if (count <= fit)
            {
                return r;
            }
            float offset = lay.Offset[slot];
            int reveal = lay.Reveal[slot];
            lay.Reveal[slot] = -1;
            if (reveal >= 0 && reveal < count)
            {
                offset = Math.Min(offset, reveal * rowH);
                offset = Math.Max(offset, reveal * rowH + rowH - r.ViewH);
            }
            offset = Math.Max(0f, Math.Min((float)Math.Round(offset / rowH) * rowH, r.Content - r.ViewH));
            lay.Offset[slot] = offset;
            short clip = s.AddClip(x - bleed, y, width + bleed * 2f, r.ViewH, offset);
            s.Clips[clip].ContentHeight = r.Content;
            r.Scrolls = true;
            r.Offset = offset;
            r.First = Math.Min(count - 1, (int)(offset / rowH));
            r.Last = Math.Min(count - 1, (int)((offset + r.ViewH - 0.5f) / rowH));
            r.List = s.AddList(count, r.First, r.Last - r.First + 1, clip, -1);
            r.At = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            lay.List[slot] = r.List;
            lay.Clip[slot] = clip;
            return r;
        }

        /// <summary>Ends rows begun with <see cref="BeginRows"/>: the thumb when they scroll. Returns the y after the rows.</summary>
        internal static float EndRows(UiScene s, UiAt at, in CompanyRows r)
        {
            if (r.Scrolls)
            {
                UiDraw.ScrollThumb(s, at, r.ThumbX, r.OriginY, r.ViewH, r.Offset, r.Content);
            }
            return r.OriginY + r.ViewH;
        }
    }
}
