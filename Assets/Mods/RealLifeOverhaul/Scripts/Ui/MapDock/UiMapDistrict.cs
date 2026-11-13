using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A district in the view's ranked table: its band's colour on the map, its figure, how it moved this month and how much of it stands empty.</summary>
    public struct MapDistrictRow
    {
        public string Name;
        public UiRole Swatch;      // the key's step the district is tinted in; None for a district with no figure (grey)
        public string Figure;      // the layer's figure, or the sentence for none
        public string Month;       // "+1.8%", or null where no month is kept yet
        public int MonthSign;      // 1 up, -1 down, 0 flat or unknown: the arrow
        public UiRole MonthTone;   // Ok, Danger, or None for a layer whose rise is neither good nor bad
        public string Empty;       // "5.1%", or null
        public bool Chosen;
    }

    /// <summary>
    /// What the district view shows, after the approved mockup (districts_rent): the ten layers as tinted icons beside a panel of the city's average, a
    /// donut of a share where one helps, the map's key on the kit's ramp, the chosen district as a short block, and every district ranked in a table.
    /// The district view fills it from its figures, once a day and when the pointer changes the district; the kit only draws it.
    /// </summary>
    public sealed class MapDistrictModel
    {
        public string Title;                                  // "Districts, Rent"
        public UiIcon Icon = UiIcon.Coin;
        public string FiltersLabel;                           // the rail's first button goes back to the filters, or null for none
        public MapRailItem[] Layers = new MapRailItem[0];     // the ten layers
        public int Layer;
        public string About;                                  // a quiet sentence under the strip: what the layer measures, or null
        public string AverageTitle;                           // "City average"
        public UiInfoRow[] Average = new UiInfoRow[0];
        public string ChartTitle, ChartCentre, ChartCentreSub;
        public string[] ChartLabels = new string[0], ChartValues = new string[0];
        public float[] ChartShares = new float[0];
        public string LegendTitle, ScaleLabel, ScaleLow, ScaleHigh;
        public UiRole[] ScaleSteps;                           // null for the kit's ramp
        public bool HasChosen;
        public string ChosenName, ChosenRank, ShowLabel;      // "Midtown", "Rank 1 of 7", "Show on map"
        public UiInfoRow[] Chosen = new UiInfoRow[0];
        // The rest of the district's figures as the old card says them, the layer shown first, a sentence each, kept while the panel has the room.
        public string[] ChosenNotes = new string[0];
        public string TableTitle;                             // "Ranked by rent"
        public string ColDistrict, ColFigure, ColMonth, ColEmpty;
        public MapDistrictRow[] Rows = new MapDistrictRow[0];
        public float Offset;
        public int Reveal = -1;
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// The district view as an info view: the layers on the rail (the first button goes back to the filters), and the panel's sections on their bands. The
    /// ranked table takes the room the rest leave and scrolls; the donut goes first on a short screen. Commands, the 5060 block: <see cref="LayerCommand"/>,
    /// <see cref="RowCommand"/>, <see cref="ShowCommand"/>, <see cref="FiltersCommand"/>. Game-free. Cost: a layout a change, the rows in view only.
    /// </summary>
    public static class UiMapDistrict
    {
        public const int LayerCommand = 5060, RowCommand = 5061, ShowCommand = 5062, FiltersCommand = 5063;
        public const float Width = UiMapFilters.Width;
        private const float RowHeight = UiTokens.TableRow;     // px: a district's row
        private const float DonutSize = 108f, DonutHole = 0.62f;   // px; the share of its width the ring leaves open
        private const byte RailRegion = 1, PanelRegion = 2;
        private static readonly UiRole[] Ramp = { UiRole.Ramp1, UiRole.Ramp2, UiRole.Ramp3, UiRole.Ramp4, UiRole.Ramp5 };

        public static float Draw(UiScene s, UiAt at, MapDistrictModel m, UiFrame frame, float x, float y, IUiKeys keys)
        {
            int buttons = m.Layers.Length + (string.IsNullOrEmpty(m.FiltersLabel) ? 0 : 1);
            float px = x;
            if (buttons > 0)
            {
                Rail(s, at.InRegion(RailRegion), m, buttons, x, y);
                px = x + UiMapFilters.Menu + UiTokens.S2;
            }
            float h = Math.Min(UiMapFilters.PanelHeight(frame), Needed(m));
            UiAt column = at.InRegion(PanelRegion);
            int panel = UiDraw.Box(s, column, px, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = column.In(panel);
            float cy = UiDraw.TitleStrip(s, on, px, y, Width, m.Icon, m.Title, true);
            float ix = px + UiTokens.S4, iw = Width - UiTokens.S4 * 2f;
            float bottom = y + h;
            if (!string.IsNullOrEmpty(m.About))
            {
                int lines = UiTextRules.Lines(m.About, UiStyle.Small, iw, 2, out float widest);
                UiDraw.Text(s, on, ix, cy + UiTokens.S2, iw, UiStyle.Small, UiRole.Text2, m.About, UiFlags.Wrap | UiFlags.Truncate, lines);
                cy += UiTokens.S2 + UiDraw.Snap(lines * UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S1;
            }
            // The sections in the order of the mockup; each is kept only while the table keeps its least rows (three) under them.
            float least = TableHead(m) + RowHeight * Math.Min(3, m.Rows.Length) + UiTokens.S2;
            cy = Band(s, on, px, cy, m.AverageTitle, m.Average, ix, iw);
            if (m.ChartShares != null && m.ChartShares.Length > 0 && bottom - cy - ChartHeight(m) - LegendHeight(m) - ChosenHeight(m, 0f) >= least)
            {
                cy = Donut(s, on, m, px, cy, ix, iw);
            }
            if (!string.IsNullOrEmpty(m.LegendTitle))
            {
                cy = Legend(s, on, m, px, cy, ix, iw);
            }
            if (m.HasChosen && bottom - cy - ChosenHeight(m, 0f) >= least)
            {
                // The notes take what the panel leaves above the table's least rows, as many whole sentences as fit.
                float notesRoom = bottom - cy - ChosenHeight(m, 0f) - least;
                cy = Chosen(s, on, m, px, cy, ix, iw, NotesHeight(m, iw, notesRoom, out int shown), shown);
            }
            Table(s, on, m, px, cy, bottom);
            return px + Width;
        }

        /// <summary>The height the page needs for what it holds: every section whole and the table's every row; more than the screen has, it scrolls the table.</summary>
        public static float Needed(MapDistrictModel m)
        {
            float iw = Width - UiTokens.S4 * 2f;
            float need = UiFrame.Strip;
            if (!string.IsNullOrEmpty(m.About))
            {
                int lines = UiTextRules.Lines(m.About, UiStyle.Small, iw, 2, out float widest);
                need += UiTokens.S2 + UiDraw.Snap(lines * UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S1;
            }
            need += BandHeight(m.Average);
            need += m.ChartShares != null && m.ChartShares.Length > 0 ? ChartHeight(m) : 0f;
            need += LegendHeight(m);
            need += ChosenHeight(m, m.HasChosen ? NotesHeight(m, iw, float.MaxValue, out int all) : 0f);
            need += TableHead(m) + m.Rows.Length * RowHeight + UiTokens.S2;
            return UiDraw.Snap(need);
        }

        private static float BandHeight(UiInfoRow[] rows) => rows == null || rows.Length == 0 ? 0f : UiTokens.S2 + UiTokens.PairRow * (1 + rows.Length) + UiTokens.S2 + UiTokens.S1;

        // ------------------------------------------------------------------------------------------------- the names on the map

        private const float TagMost = 168f;   // px: a tag's words at most

        /// <summary>The size of a district's tag over the map: its colour, its name and the layer's figure.</summary>
        public static void TagSize(string name, string figure, out float w, out float h)
        {
            float words = Math.Min(TagMost, Math.Max(UiTextRules.Width(name, UiStyle.SmallStrong), UiTextRules.Width(figure, UiStyle.Body)));
            w = UiDraw.Snap(UiTokens.S3 + UiTokens.StatusDot + UiTokens.S2 + words + UiTokens.S3 + UiTokens.S1);
            h = UiDraw.Snap(UiTokens.S2 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SmallStrong)) + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body)) + UiTokens.S2);
        }

        /// <summary>
        /// A district's tag: a square in its band's colour, its name over its figure, on a slate piece, or on the cyan one for the district the page is about. Drawn once into the
        /// size <see cref="TagSize"/> gave.
        /// </summary>
        public static void Tag(UiScene s, UiAt at, float x, float y, float w, float h, string name, string figure, UiRole swatch, bool chosen = false)
        {
            int box = UiDraw.Box(s, at, x, y, w, h, chosen ? UiRole.Accent : UiRole.Panel, UiRole.None, UiTokens.RadiusLarge);
            UiAt on = at.In(box);
            float lh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SmallStrong));
            UiDraw.Box(s, on, x + UiTokens.S3, y + UiTokens.S2 + UiDraw.Snap((lh - UiTokens.StatusDot) / 2f), UiTokens.StatusDot, UiTokens.StatusDot, swatch == UiRole.None ? UiRole.Track : swatch,
                UiRole.None, UiTokens.RadiusSmall);
            float tx = x + UiTokens.S3 + UiTokens.StatusDot + UiTokens.S2, tw = w - (tx - x) - UiTokens.S3;
            UiDraw.Text(s, on, tx, y + UiTokens.S2, tw, UiStyle.SmallStrong, chosen ? UiRole.OnAccent : UiRole.Text, name, UiFlags.Truncate);
            UiDraw.Text(s, on, tx, y + UiTokens.S2 + lh, tw, UiStyle.Body, chosen ? UiRole.OnAccent : swatch == UiRole.None ? UiRole.Text2 : UiRole.Text, figure, UiFlags.Truncate);
        }

        // ------------------------------------------------------------------------------------------------- the rail

        private static void Rail(UiScene s, UiAt at, MapDistrictModel m, int buttons, float x, float y)
        {
            float h = UiMapFilters.RailHeight(buttons);
            int menu = UiDraw.Box(s, at, x, y, UiMapFilters.Menu, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(menu);
            int offset = string.IsNullOrEmpty(m.FiltersLabel) ? 0 : 1;
            for (int i = 0; i < buttons; i++)
            {
                float cx = x + UiTokens.S2 + (i % 2) * (UiMapFilters.Cell + UiTokens.S1), cy = y + UiTokens.S2 + (i / 2) * (UiMapFilters.Cell + UiTokens.S1);
                if (i < offset)
                {
                    UiDraw.IconButton(s, on, cx, cy, UiMapFilters.Cell, UiIcon.Funnel, UiRole.None, FiltersCommand, 0);
                    continue;
                }
                int layer = i - offset;
                int node = UiDraw.IconButton(s, on, cx, cy, UiMapFilters.Cell, m.Layers[layer].Icon, m.Layers[layer].Tint, LayerCommand, layer, layer == m.Layer);
                if (layer == m.Layer)
                {
                    s.FocusFirst = node;
                }
            }
        }

        // ------------------------------------------------------------------------------------------------- sections

        private static float Band(UiScene s, UiAt at, float px, float y, string title, UiInfoRow[] rows, float ix, float iw)
        {
            if (rows == null || rows.Length == 0)
            {
                return y;
            }
            float h = UiTokens.S2 + UiTokens.PairRow * (1 + rows.Length) + UiTokens.S2;
            UiDraw.Band(s, at, px, y, Width, h);
            float cy = y + UiTokens.S2;
            cy += UiDraw.SectionRow(s, at, ix, cy, iw, title, null, false, -1, 0);
            for (int i = 0; i < rows.Length; i++)
            {
                cy += UiDraw.InfoRow(s, at, ix, cy, iw, rows[i].Label, rows[i].Value, false, -1, 0, rows[i].Ink == UiRole.None ? UiRole.Text : rows[i].Ink);
            }
            return y + h + UiTokens.S1;
        }

        private static float ChartHeight(MapDistrictModel m) => UiTokens.S3 + UiTokens.PairRow + UiTokens.S2 + Math.Max(DonutSize, m.ChartLabels.Length * 20f) + UiTokens.S3 + UiTokens.S1;

        private static float Donut(UiScene s, UiAt at, MapDistrictModel m, float px, float y, float ix, float iw)
        {
            float h = ChartHeight(m) - UiTokens.S1;
            UiDraw.Band(s, at, px, y, Width, h);
            float cy = y + UiTokens.S3;
            UiDraw.TextMiddle(s, at, ix, cy, UiTokens.PairRow, iw, UiStyle.SectionHeader, UiRole.Text, m.ChartTitle, UiFlags.Label | UiFlags.Truncate);
            cy += UiTokens.PairRow + UiTokens.S2;
            UiDraw.Donut(s, at, ix, cy, DonutSize, m.ChartShares, m.ChartShares.Length);
            if (!string.IsNullOrEmpty(m.ChartCentre))
            {
                float inner = DonutSize * DonutHole, cx = ix + (DonutSize - inner) / 2f;
                float lh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)), sh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
                // The word under the figure only where the hole holds it whole, as wide as the hole is at the word's foot: a translation too long for it is left out, not cut off
                // by the ring (the chart's title says what it counts).
                float half = (lh + sh) / 2f, chord = 2f * (float)Math.Sqrt(Math.Max(0f, inner * inner / 4f - half * half));
                bool two = !string.IsNullOrEmpty(m.ChartCentreSub) && UiTextRules.Width(m.ChartCentreSub, UiStyle.Small) <= chord - UiTokens.S1;
                float top = cy + (DonutSize - lh - (two ? sh : 0f)) / 2f;
                UiDraw.TextMiddle(s, at, cx, top, lh, inner, UiStyle.Title, UiRole.Text, m.ChartCentre, UiFlags.Centre | UiFlags.Truncate);
                if (two)
                {
                    UiDraw.TextMiddle(s, at, cx, top + lh, sh, inner, UiStyle.Small, UiRole.Text2, m.ChartCentreSub, UiFlags.Centre | UiFlags.Truncate);
                }
            }
            int n = m.ChartLabels.Length;
            float lx = ix + DonutSize + UiTokens.S6, lw = ix + iw - lx, lh2 = n * 20f;
            UiDraw.Legend(s, at, lx, cy + (DonutSize - lh2) / 2f, lw, m.ChartLabels, m.ChartValues, n);
            return y + h + UiTokens.S1;
        }

        private static float LegendHeight(MapDistrictModel m) => string.IsNullOrEmpty(m.LegendTitle) ? 0f
            : UiTokens.S3 + UiTokens.PairRow + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body)) + UiTokens.S2
              + UiDraw.Snap(UiTokens.S2 + UiTokens.S1 + UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S3 + UiTokens.S1;

        private static float Legend(UiScene s, UiAt at, MapDistrictModel m, float px, float y, float ix, float iw)
        {
            float h = LegendHeight(m) - UiTokens.S1;
            UiDraw.Band(s, at, px, y, Width, h);
            float cy = y + UiTokens.S3;
            UiDraw.TextMiddle(s, at, ix, cy, UiTokens.PairRow, iw, UiStyle.SectionHeader, UiRole.Text, m.LegendTitle, UiFlags.Label | UiFlags.Truncate);
            cy += UiTokens.PairRow;
            UiDraw.Text(s, at, ix, cy, iw, UiStyle.Body, UiRole.Text2, m.ScaleLabel, UiFlags.Label | UiFlags.Truncate);
            cy += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body)) + UiTokens.S2;
            UiDraw.Scale(s, at, ix, cy, iw, m.ScaleLow, m.ScaleHigh, m.ScaleSteps ?? Ramp);
            return y + h + UiTokens.S1;
        }

        private static float ChosenHeight(MapDistrictModel m, float notes) =>
            !m.HasChosen ? 0f : UiTokens.S3 + UiTokens.PairRow + UiTokens.S1 + m.Chosen.Length * UiTokens.PairRow + notes + UiTokens.S2 + UiTokens.S1;

        // The notes' height while whole sentences fit the room given: how many of them show, and what they take (a line of room under each).
        private static float NotesHeight(MapDistrictModel m, float iw, float room, out int shown)
        {
            float sum = 0f;
            shown = 0;
            for (int i = 0; m.ChosenNotes != null && i < m.ChosenNotes.Length; i++)
            {
                int lines = UiTextRules.Lines(m.ChosenNotes[i], UiStyle.Small, iw, 3, out float widest);
                float one = UiDraw.Snap(lines * UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S1;
                if (sum + one > room)
                {
                    break;
                }
                sum += one;
                shown++;
            }
            return sum;
        }

        private static float Chosen(UiScene s, UiAt at, MapDistrictModel m, float px, float y, float ix, float iw, float notes, int shown)
        {
            float h = ChosenHeight(m, notes) - UiTokens.S1;
            UiDraw.Band(s, at, px, y, Width, h);
            float cy = y + UiTokens.S3;
            float nw = UiDraw.Snap(UiTextRules.Width(m.ChosenName, UiStyle.Title) + UiTokens.S1);
            float lw = string.IsNullOrEmpty(m.ShowLabel) ? 0f : UiDraw.Snap(UiTextRules.Width(m.ShowLabel, UiStyle.SmallStrong) + UiTokens.IconSmall + UiTokens.S1 + UiTokens.S3 * 2f);
            float cw = string.IsNullOrEmpty(m.ChosenRank) ? 0f : UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(m.ChosenRank, UiStyle.SmallStrong) + UiTokens.S1);
            nw = Math.Min(nw, iw - lw - cw - UiTokens.S3 * 2f);
            UiDraw.TextMiddle(s, at, ix, cy, UiTokens.PairRow, nw, UiStyle.Title, UiRole.Text, m.ChosenName, UiFlags.Truncate);
            if (cw > 0f)
            {
                UiDraw.Chip(s, at, ix + nw + UiTokens.S3, cy + (UiTokens.PairRow - UiTokens.Badge - UiTokens.S1) / 2f, m.ChosenRank, UiRole.Accent, cw);
            }
            if (lw > 0f)
            {
                int link = UiDraw.Box(s, at, ix + iw - lw, cy, lw, UiTokens.PairRow, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
                UiAt inside = at.In(link);
                UiDraw.Icon(s, inside, ix + iw - lw + UiTokens.S3, cy + (UiTokens.PairRow - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Locate, UiRole.AccentText, UiFlags.Middle);
                UiDraw.TextMiddle(s, inside, ix + iw - lw + UiTokens.S3 + UiTokens.IconSmall + UiTokens.S1, cy, UiTokens.PairRow,
                    lw - UiTokens.S3 * 2f - UiTokens.IconSmall - UiTokens.S1, UiStyle.SmallStrong,
                    UiRole.AccentText, m.ShowLabel, UiFlags.Label | UiFlags.Right);
                UiDraw.Node(s, at, ix + iw - lw, cy, lw, UiTokens.PairRow, ShowCommand, 0, link);
            }
            cy += UiTokens.PairRow + UiTokens.S1;
            for (int i = 0; i < m.Chosen.Length; i++)
            {
                cy += UiDraw.InfoRow(s, at, ix, cy, iw, m.Chosen[i].Label, m.Chosen[i].Value, false, -1, 0, m.Chosen[i].Ink == UiRole.None ? UiRole.Text : m.Chosen[i].Ink);
            }
            for (int i = 0; i < shown; i++)
            {
                int lines = UiTextRules.Lines(m.ChosenNotes[i], UiStyle.Small, iw, 3, out float widest);
                UiDraw.Text(s, at, ix, cy, iw, UiStyle.Small, UiRole.Text2, m.ChosenNotes[i], UiFlags.Wrap | UiFlags.Label, lines);
                cy += UiDraw.Snap(lines * UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S1;
            }
            return y + h + UiTokens.S1;
        }

        // ------------------------------------------------------------------------------------------------- the ranked table

        private static float TableHead(MapDistrictModel m) => UiTokens.S3 + UiTokens.PairRow + UiTokens.S1 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S2;

        // The share standing empty: as wide as its heading or its widest figure, whichever is more (a script with narrower letters has a narrower heading than its figures).
        private static float EmptyWidth(MapDistrictModel m)
        {
            if (string.IsNullOrEmpty(m.ColEmpty))
            {
                return 0f;
            }
            float widest = UiTextRules.Width(m.ColEmpty, UiStyle.SmallStrong);
            for (int i = 0; i < m.Rows.Length; i++)
            {
                widest = Math.Max(widest, UiTextRules.Width(m.Rows[i].Empty, UiStyle.Small));
            }
            return Math.Min(UiDraw.Snap(widest + UiTokens.S2), 72f);
        }

        // Columns: the rank, the district with its band's colour, its figure, the month's change and the share standing empty, the figures right aligned.
        private static void Table(UiScene s, UiAt at, MapDistrictModel m, float px, float y, float bottom)
        {
            float ix = px + UiTokens.S4, iw = Width - UiTokens.S4 * 2f;
            UiDraw.Band(s, at, px, y, Width, bottom - y);
            float cy = y + UiTokens.S3;
            UiDraw.TextMiddle(s, at, ix, cy, UiTokens.PairRow, iw, UiStyle.SectionHeader, UiRole.Text, m.TableTitle, UiFlags.Label | UiFlags.Truncate);
            cy += UiTokens.PairRow + UiTokens.S1;
            // A layer the share standing empty means nothing for has no such column: its room goes to the figures' columns.
            float empty = EmptyWidth(m);
            float month = 84f, figure = 76f, rank = 24f;
            float emptyX = ix + iw - empty, monthX = emptyX - month, figureX = monthX - figure, nameX = ix + rank + UiTokens.S2 + UiTokens.S3 + UiTokens.S3;
            float hh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            UiDraw.TextMiddle(s, at, nameX, cy, hh, figureX - nameX, UiStyle.SmallStrong, UiRole.Muted, m.ColDistrict, UiFlags.Label | UiFlags.Truncate);
            UiDraw.TextMiddle(s, at, figureX, cy, hh, figure, UiStyle.SmallStrong, UiRole.Muted, m.ColFigure, UiFlags.Label | UiFlags.Right | UiFlags.Truncate);
            UiDraw.TextMiddle(s, at, monthX, cy, hh, month, UiStyle.SmallStrong, UiRole.Muted, m.ColMonth, UiFlags.Label | UiFlags.Right | UiFlags.Truncate);
            if (empty > 0f)
            {
                UiDraw.TextMiddle(s, at, emptyX, cy, hh, empty, UiStyle.SmallStrong, UiRole.Muted, m.ColEmpty, UiFlags.Label | UiFlags.Right | UiFlags.Truncate);
            }
            cy += hh + UiTokens.S2;
            int rows = m.Rows.Length;
            float room = Math.Max(0f, bottom - cy - UiTokens.S2);
            float viewH = Math.Max(RowHeight, (float)Math.Floor(room / RowHeight + 0.001f) * RowHeight);
            float content = rows * RowHeight;
            float offset = m.Offset;
            if (m.Reveal >= 0 && m.Reveal < rows)
            {
                offset = Math.Min(offset, m.Reveal * RowHeight);
                offset = Math.Max(offset, m.Reveal * RowHeight + RowHeight - viewH);
            }
            offset = Math.Max(0f, Math.Min((float)Math.Round(offset / RowHeight) * RowHeight, Math.Max(0f, content - viewH)));
            m.Offset = offset;
            m.Reveal = -1;
            if (rows == 0)
            {
                return;
            }
            short clip = s.AddClip(px, cy, Width, Math.Min(viewH, content), offset);
            s.Clips[clip].ContentHeight = content;
            int first = Math.Min(rows - 1, (int)(offset / RowHeight));
            int last = Math.Min(rows - 1, (int)((offset + viewH - 0.5f) / RowHeight));
            short list = s.AddList(rows, first, last - first + 1, clip, -1);
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            float cix = UiTokens.S4;
            float ciw = Width - UiTokens.S4 * 2f - (content > viewH + 0.5f ? UiTokens.S2 : 0f);
            float cEmptyX = cix + ciw - empty, cMonthX = cEmptyX - month, cFigureX = cMonthX - figure, cNameX = cix + rank + UiTokens.S2 + UiTokens.S3 + UiTokens.S3;
            for (int r = first; r <= last; r++)
            {
                MapDistrictRow d = m.Rows[r];
                float ry = r * RowHeight;
                UiAt arriving = inClip.Arriving(r - first);
                int face = UiDraw.Box(s, arriving, 0f, ry, Width, RowHeight, d.Chosen ? UiRole.RowSel : UiRole.None, UiRole.None, UiTokens.RadiusLarge, d.Chosen ? UiFlags.Selected : UiFlags.None);
                UiAt inside = arriving.In(face);
                UiDraw.TextMiddle(s, inside, cix, ry, RowHeight, rank, UiStyle.Small, d.Chosen ? UiRole.Text : UiRole.Muted, (r + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    UiFlags.Right);
                UiDraw.Box(s, inside, cix + rank + UiTokens.S2, ry + (RowHeight - UiTokens.StatusDot - UiTokens.S1) / 2f, UiTokens.StatusDot + UiTokens.S1, UiTokens.StatusDot + UiTokens.S1,
                    d.Swatch == UiRole.None ? UiRole.Track : d.Swatch, UiRole.None, UiTokens.RadiusSmall, UiFlags.Middle);
                UiDraw.TextMiddle(s, inside, cNameX, ry, RowHeight, cFigureX - cNameX, UiStyle.Body, UiRole.Text, d.Name, UiFlags.Truncate);
                UiDraw.TextMiddle(s, inside, cFigureX, ry, RowHeight, figure, UiStyle.Body, d.Swatch == UiRole.None && !d.Chosen ? UiRole.Muted : UiRole.Text, d.Figure,
                    UiFlags.Label | UiFlags.Right | UiFlags.Truncate);
                if (!string.IsNullOrEmpty(d.Month))
                {
                    UiRole tone = d.MonthTone == UiRole.Ok ? UiRole.OkText : d.MonthTone == UiRole.Danger ? UiRole.DangerText : d.Chosen ? UiRole.Text : UiRole.Text2;
                    float mw = UiDraw.Snap(UiTextRules.Width(d.Month, UiStyle.Small) + UiTokens.S1);
                    if (d.MonthSign != 0)
                    {
                        // The arrow first, so the words beside it are the next thing laid out (the rule that an arrow agrees with its words reads them so).
                        UiDraw.Icon(s, inside, cMonthX + month - mw - UiTokens.IconSmall - UiTokens.S1, ry + (RowHeight - UiTokens.IconSmall) / 2f, UiTokens.IconSmall,
                            d.MonthSign > 0 ? UiIcon.TrendUp : UiIcon.TrendDown, d.MonthTone == UiRole.None ? (d.Chosen ? UiRole.Text : UiRole.Muted) : d.MonthTone, UiFlags.Middle);
                    }
                    UiDraw.TextMiddle(s, inside, cMonthX + month - mw, ry, RowHeight, mw, UiStyle.Small, tone, d.Month, UiFlags.Right | UiFlags.Truncate);
                }
                if (!string.IsNullOrEmpty(d.Empty))
                {
                    UiDraw.TextMiddle(s, inside, cEmptyX, ry, RowHeight, empty, UiStyle.Small, d.Chosen ? UiRole.Text : UiRole.Text2, d.Empty, UiFlags.Label | UiFlags.Right | UiFlags.Truncate);
                }
                UiDraw.Node(s, inClip, 0f, ry, Width, RowHeight, RowCommand, r, face, false, list, r);
            }
            if (content > viewH + 0.5f)
            {
                UiDraw.ScrollThumb(s, at, px + Width - UiTokens.S1, cy, Math.Min(viewH, content), offset, content);
            }
        }
    }
}
