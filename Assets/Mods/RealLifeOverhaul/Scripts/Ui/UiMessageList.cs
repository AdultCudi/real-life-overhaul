using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A row of the message list: who (a face, or a business's icon), the last words, when, what is unread and who is there.</summary>
    public struct UiMessageRow
    {
        public string Name;
        public float Hue;
        public UiIcon Icon;      // a business or a place has its icon on a tile in place of a face
        public UiRole Tint;
        public string Preview;   // the last words, cut at a whole word
        public string Time;
        public int Unread;
        public bool Online;      // presence, a dot on the face
        public bool Selected;
        public string Section;   // the band this row opens ("New messages", "Earlier"); null when it carries on the band above
    }

    /// <summary>The message list: its search, its rows under their bands, the selected one and how far they have scrolled.</summary>
    public sealed class UiMessageListModel
    {
        public string SearchPlaceholder;
        public string Query;
        public string Found;
        public UiMessageRow[] Rows = new UiMessageRow[0];
        public int RowCount;
        public float Offset;                           // px scrolled; Lay writes back what it took
        public int Reveal = -1;
        public string EmptyTitle, EmptyBody;
        public string EmptyAction;                     // the button that fills an empty list ("Clear search", answering ClearCommand); null for none
    }

    /// <summary>
    /// A message list, a block of an app window's page: the search at its head, then rows under their bands' capitals
    /// ("New messages", "Earlier"), each a face in its presence dot (or a business's icon on its tile), the name, the last
    /// words cut at a whole word, the time and an unread badge, the chosen one in the cyan's tone. The rows scroll in their
    /// frame, only those in view laid out, as the list and detail's list did. For Contacts (moving off UiListDetail) and
    /// Flare's messages. Commands: <see cref="RowCommand"/> (a row), <see cref="SearchCommand"/> and <see cref="ClearCommand"/> (an empty search's way out). Game-free.
    /// Cost: a layout a change.
    /// </summary>
    public static class UiMessageList
    {
        public const int RowCommand = 70, SearchCommand = 71, ClearCommand = 72;
        public const float RowGap = UiTokens.S2;     // px between two rows

        private static float BandHeight => UiDraw.Snap(UiTokens.S4 + UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;

        /// <summary>A row's height with the band it opens and the gap under it.</summary>
        public static float RowHeight(ref UiMessageRow row) => (row.Section != null ? BandHeight : 0f) + UiTokens.Row + RowGap;

        /// <summary>Lays the list into a room; writes back the scroll it took.</summary>
        public static void Lay(UiScene s, UiAt at, UiMessageListModel m, float x, float y, float w, float h)
        {
            UiDraw.Search(s, at, x, y, w, m.SearchPlaceholder, m.Query, string.IsNullOrEmpty(m.Query) ? null : m.Found, SearchCommand);
            float top = y + UiTokens.Control + UiTokens.S3;
            float vh = UiDraw.SnapDown(y + h - top);
            float contentH = 0f;
            for (int i = 0; i < m.RowCount; i++)
            {
                contentH += RowHeight(ref m.Rows[i]);
            }
            float offset = m.Offset;
            if (m.Reveal >= 0 && m.Reveal < m.RowCount)
            {
                float rt = 0f;
                for (int i = 0; i < m.Reveal; i++)
                {
                    rt += RowHeight(ref m.Rows[i]);
                }
                float lead = m.Rows[m.Reveal].Section != null ? BandHeight : 0f;
                offset = Math.Min(offset, rt);
                offset = Math.Max(offset, rt + lead + UiTokens.Row - vh);
                m.Reveal = -1;
            }
            offset = Math.Max(0f, Math.Min(offset, contentH - vh));
            m.Offset = offset;
            short clip = s.AddClip(x, top, w, vh, offset);
            s.Clips[clip].ContentHeight = contentH;
            UiDraw.ScrollThumb(s, at, x + w + UiTokens.S1, top, vh, offset, contentH);
            if (m.RowCount == 0)
            {
                s.AddList(0, 0, 0, clip, -1);
                if (string.IsNullOrEmpty(m.EmptyAction))
                {
                    UiDraw.Empty(s, at, x, top, w, vh, m.EmptyTitle, m.EmptyBody);
                }
                else
                {
                    UiDraw.EmptyState(s, at, x, top, w, vh, UiIcon.Search, m.EmptyTitle, m.EmptyBody, m.EmptyAction, ClearCommand, null);
                }
                return;
            }
            int selected = -1, first = -1, shown = 0;
            float ry = 0f;
            for (int i = 0; i < m.RowCount; i++)
            {
                selected = m.Rows[i].Selected ? i : selected;
                float rh = RowHeight(ref m.Rows[i]);
                if (ry + rh > offset && ry < offset + vh)
                {
                    first = first < 0 ? i : first;
                    shown++;
                }
                ry += rh;
            }
            short list = s.AddList(m.RowCount, Math.Max(0, first), shown, clip, selected);
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            ry = 0f;
            for (int i = 0; i < m.RowCount; i++)
            {
                ref UiMessageRow row = ref m.Rows[i];
                float rh = RowHeight(ref row);
                if (ry + rh > offset && ry < offset + vh)
                {
                    UiAt arriving = inClip.Arriving(i - first);
                    float rowTop = ry;
                    if (row.Section != null)
                    {
                        UiDraw.SectionHeader(s, arriving, 0f, ry, w, row.Section);
                        rowTop += BandHeight;
                    }
                    var model = new UiRowModel
                    {
                        Name = row.Name, Preview = row.Preview, Time = row.Time, Unread = row.Unread, Hue = row.Hue, Online = row.Online, Selected = row.Selected,
                        Icon = row.Icon, Tint = row.Tint
                    };
                    // The row's tone under a clear face, so the pointer's still lift reads on it.
                    UiDraw.Box(s, arriving, 0f, rowTop, w, UiTokens.Row, UiRole.Row, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
                    UiDraw.ListRow(s, arriving, 0f, rowTop, w, ref model, RowCommand, i, list, i, true, true);
                }
                ry += rh;
            }
        }
    }
}
