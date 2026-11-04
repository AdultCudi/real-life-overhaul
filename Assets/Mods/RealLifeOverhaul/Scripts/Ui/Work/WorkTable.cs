using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a squad page lays: its chips, search, table, aside and the chosen row's detail. Built by <see cref="WorkTabPages"/>. Game-free.</summary>
    public sealed class WorkTableModel
    {
        public UiActionModel[] Chips = new UiActionModel[0];   // every chip, drawn in a row; the chosen one cyan
        public int ChosenChip;
        public string SearchHint, Query;
        public UiDataTableModel Table = new UiDataTableModel();
        public WorkRow Detail;                                  // null: no panel
        public UiActionModel[] Actions = new UiActionModel[0];
        public WorkAmount[] Amounts = new WorkAmount[0];
        public WorkAside Aside;
        public int AsideChosen = -1;
        public string UnderLead, UnderText;                     // a quiet band under a short table
        public List<KeyValuePair<string, string>> Stats;        // figures that sum the list, in a strip under a short table
        public UiActionModel[] PageActs = new UiActionModel[0]; // buttons of the page itself, in the tool row's right
        public string Why;                                      // why a disabled button of the detail is disabled
    }

    /// <summary>
    /// A squad page, as Football Manager's staff search and the mockups lay it: filter chips and a search over the big table (a tile or a face, graded
    /// figures, a chip of state), the chosen row's detail at the right, and where there is one the list it is about at the left. Every part is the
    /// kit's own. Game-free. Cost: a layout a change; the table lays only the rows in view.
    /// </summary>
    public static class WorkTable
    {
        private const float AsideWidth = 248f;

        private static float StatsHeight()
        {
            float label = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)), value = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Number));
            return UiDraw.Snap(label + value + UiTokens.S1 + UiTokens.S4 * 2f);
        }

        // Figures that sum a short list: a small capital label over the value, each in its own cell of a darker field.
        private static float StatStrip(UiScene s, UiAt at, List<KeyValuePair<string, string>> stats, float x, float y, float w)
        {
            float h = StatsHeight();
            UiDraw.Box(s, at, x, y, w, h, UiRole.Field, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            int n = Math.Min(4, stats.Count);
            float cell = UiDraw.SnapDown((w - UiTokens.S4 * 2f) / n), lh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader));
            for (int i = 0; i < n; i++)
            {
                float cx = x + UiTokens.S4 + i * cell;
                UiDraw.Text(s, at, cx, y + UiTokens.S4, cell - UiTokens.S2, UiStyle.SectionHeader, UiRole.Text2, stats[i].Key, UiFlags.Label | UiFlags.Truncate);
                UiDraw.Text(s, at, cx, y + UiTokens.S4 + lh + UiTokens.S1, cell - UiTokens.S2, UiStyle.Number, UiRole.Text, stats[i].Value, UiFlags.Truncate);
            }
            return h;
        }

        public static void Lay(UiScene s, UiAt at, WorkTableModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float panel = m.Detail != null ? WorkPanel.WidthFor(w) : 0f;
            float aside = m.Aside != null ? AsideWidth : 0f;
            float gap = UiTokens.S6;
            float ax = x, tx = x + aside + (aside > 0f ? gap : 0f);
            float tw = UiDraw.SnapDown(w - aside - (aside > 0f ? gap : 0f) - panel - (panel > 0f ? gap : 0f));
            if (m.Aside != null)
            {
                Aside(s, at, m, ax, y, aside, bottom - y);
            }
            float ty = Tools(s, at, m, tx, y, tw, keys);
            float room = bottom - ty;
            bool strip = m.Stats != null && m.Stats.Count > 0;
            if ((m.UnderText != null || strip) && m.Table.RowCount > 0)
            {
                float stats = strip ? StatsHeight() + UiTokens.S3 : 0f;
                float under = (m.UnderText != null ? WorkPanel.NoteHeight(m.UnderLead, m.UnderText, tw) + UiTokens.S3 : 0f) + stats;
                float rows = UiDataTable.RowHeight(m.Table) * m.Table.RowCount + UiDataTable.Chrome(m.Table) - (UiDataTable.HasTools(m.Table) ? UiTokens.Control + UiTokens.S3 : 0f);
                if (rows + under <= room)
                {
                    float end = ty + rows;
                    UiDataTable.Lay(s, at, m.Table, tx, ty, tw, rows, keys);
                    if (strip)
                    {
                        end += UiTokens.S3 + StatStrip(s, at, m.Stats, tx, end + UiTokens.S3, tw);
                    }
                    if (m.UnderText != null)
                    {
                        WorkPanel.Note(s, at, UiIcon.Info, m.UnderLead, m.UnderText, tx, end + UiTokens.S3, tw);
                    }
                    Detail(s, at, m, x + w - panel, y, panel, bottom - y, keys);
                    return;
                }
            }
            UiDataTable.Lay(s, at, m.Table, tx, ty, tw, room, keys);
            Detail(s, at, m, x + w - panel, y, panel, bottom - y, keys);
        }

        private static void Detail(UiScene s, UiAt at, WorkTableModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            if (m.Detail != null && w > 0f)
            {
                WorkPanel.Lay(s, at, m.Detail, m.Actions, x, y, w, h, keys, m.Amounts, m.Why);
            }
        }

        // ------------------------------------------------------------------------------------------------- the tool row

        private static float Tools(UiScene s, UiAt at, WorkTableModel m, float x, float y, float w, IUiKeys keys)
        {
            if (m.Chips.Length == 0 && string.IsNullOrEmpty(m.SearchHint) && m.PageActs.Length == 0)
            {
                return y;
            }
            float h = UiTokens.ControlSmall + UiTokens.S1;
            float right = x + w;
            for (int i = m.PageActs.Length - 1; i >= 0; i--)
            {
                UiActionModel a = m.PageActs[i];
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                right -= bw;
                UiDraw.Button(s, at, right, y, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, a.Command, a.Arg, a.Disabled);
                right -= UiTokens.S3;
            }
            float cx = x;
            for (int i = 0; i < m.Chips.Length; i++)
            {
                cx += Chip(s, at, m.Chips[i], i == m.ChosenChip, cx, y + UiDraw.SnapDown((UiTokens.Control - h) / 2f), h) + UiTokens.S2;
            }
            if (!string.IsNullOrEmpty(m.SearchHint))
            {
                float sw = UiDraw.SnapDown(Math.Min(300f, right - cx - UiTokens.S2));
                if (sw >= 160f)
                {
                    UiDraw.Search(s, at, right - sw, y, sw, m.SearchHint, m.Query, null, WorkCmd.Search);
                }
            }
            return y + UiTokens.Control + UiTokens.S3;
        }

        /// <summary>A filter chip: the chosen one a cyan fill with dark ink and a tick, the others a field's face. A target of its action.</summary>
        public static float Chip(UiScene s, UiAt at, UiActionModel a, bool chosen, float x, float y, float h)
        {
            float tick = chosen ? UiTokens.IconSmall + UiTokens.S1 : 0f;
            float w = UiDraw.Snap(UiTextRules.Width(a.Label, UiStyle.Nav) + UiTokens.S4 * 2f + tick);
            int face = UiDraw.Box(s, at, x, y, w, h, chosen ? UiRole.Accent : UiRole.Field, UiRole.None, UiTokens.RadiusLarge, chosen ? UiFlags.Selected : UiFlags.None);
            UiAt inside = at.In(face);
            UiRole ink = chosen ? UiRole.OnAccent : UiRole.Text;
            float tx = x + UiTokens.S4;
            if (chosen)
            {
                UiDraw.Icon(s, inside, tx, y + (h - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Check, UiRole.OnAccent, UiFlags.Middle | UiFlags.Centre);
                tx += tick;
            }
            UiDraw.TextMiddle(s, inside, tx, y, h, w - (tx - x) - UiTokens.S4 + UiTokens.S1, UiStyle.Nav, ink, a.Label, UiFlags.Truncate);
            UiDraw.Node(s, at, x, y, w, h, a.Command, a.Arg, face);
            return w;
        }

        // ------------------------------------------------------------------------------------------------- the aside

        private static void Aside(UiScene s, UiAt at, WorkTableModel m, float x, float y, float w, float h)
        {
            WorkAside a = m.Aside;
            UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            float iy = y + UiTokens.S3;
            iy += WorkPanel.SectionHead(s, at, x + UiTokens.S4, iy, w - UiTokens.S4 * 2f, a.Title, a.Count) + UiTokens.S1;
            float rowH = UiTokens.Row;
            for (int i = 0; i < a.Rows.Count && iy + rowH < y + h; i++)
            {
                WorkRow r = a.Rows[i];
                bool on = i == m.AsideChosen;
                int face = UiDraw.Box(s, at, x + UiTokens.S2, iy, w - UiTokens.S2 * 2f, rowH, on ? UiRole.RowSel : UiRole.None, UiRole.None,
                    UiTokens.RadiusLarge, on ? UiFlags.Selected : UiFlags.None);
                UiAt inside = at.In(face);
                float tile = UiTokens.ControlSmall, tx = x + UiTokens.S3, ty = iy + UiDraw.SnapDown((rowH - tile) / 2f);
                int t = UiDraw.Tile(s, inside, tx, ty, tile, UiRole.None, false);
                UiDraw.Icon(s, inside.In(t), tx + (tile - UiTokens.Icon) / 2f, ty + (tile - UiTokens.Icon) / 2f, UiTokens.Icon, r.Icon, r.Tint, UiFlags.Middle | UiFlags.Centre, true);
                float count = string.IsNullOrEmpty(r.Status) ? 0f : UiTextRules.Width(r.Status, UiStyle.BodyStrong) + UiTokens.S1;
                float nx = tx + tile + UiTokens.S3, nw = x + w - UiTokens.S3 - nx - count - (count > 0f ? UiTokens.S2 : 0f);
                float lh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)), sh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)), top = iy + UiDraw.SnapDown((rowH - lh - sh) / 2f);
                UiDraw.Text(s, inside, nx, top, nw, UiStyle.Title, UiRole.Text, r.Name, UiFlags.Truncate | UiFlags.Middle);
                UiDraw.Text(s, inside, nx, top + lh, nw, UiStyle.Small, UiRole.Text2, r.Sub, UiFlags.Truncate | UiFlags.Middle);
                if (count > 0f)
                {
                    UiDraw.TextMiddle(s, inside, x + w - UiTokens.S3 - count, iy, rowH, count, UiStyle.BodyStrong, UiRole.Text2, r.Status, UiFlags.Right);
                }
                UiDraw.Node(s, at, x + UiTokens.S2, iy, w - UiTokens.S2 * 2f, rowH, WorkCmd.Aside, i, face);
                iy += rowH;
            }
            iy += UiTokens.S3;
            if (a.List.Count > 0 && iy + UiTokens.PairRow + WorkPanelPersonHeight < y + h)
            {
                iy += WorkPanel.SectionHead(s, at, x + UiTokens.S4, iy, w - UiTokens.S4 * 2f, a.ListTitle, a.ListCount) + UiTokens.S1;
                for (int i = 0; i < a.List.Count && iy + WorkPanelPersonHeight < y + h - (string.IsNullOrEmpty(a.Foot) ? 0f : UiTokens.S16); i++)
                {
                    iy += WorkPanel.PersonRow(s, at, a.List[i], x + UiTokens.S4, iy, w - UiTokens.S4 * 2f);
                }
            }
            if (!string.IsNullOrEmpty(a.Foot))
            {
                float fh = UiTextRules.WrappedHeight(a.Foot, UiStyle.Small, w - UiTokens.S4 * 2f, 4);
                UiDraw.Text(s, at, x + UiTokens.S4, UiDraw.Snap(y + h - UiTokens.S4 - fh), w - UiTokens.S4 * 2f, UiStyle.Small, UiRole.Text2, a.Foot, UiFlags.Wrap | UiFlags.Truncate, 4);
            }
        }

        private const float WorkPanelPersonHeight = 48f;
    }
}
