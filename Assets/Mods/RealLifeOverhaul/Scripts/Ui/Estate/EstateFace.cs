using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Property app on the kit: an app window with its sections down a sidebar, and a page of the model's shape: the rent roll as a table
    /// under its figures with the repair and landlord lists below it, a tab's table with a card beside it, one flat's page, or a page of
    /// cards about one thing with its way back. Game-free; the model says what, this says where. Cost: a layout when the model changes.
    /// </summary>
    public static partial class EstateFace
    {
        // The block 3000 to 3019 is this screen's: the kit's own commands repeat across parts (a kit nav is 1, a decision's choice is 1).
        public const int ActionCommand = 3000, StepLess = 3001, StepMore = 3002, BackCommand = 3003, SearchCommand = 3010;
        public const int TableBase = UiDataTable.First;
        private const float HeadHeight = 64f, RowHeight = 48f, TileBig = 56f, ListWidthShare = 0.5f, SideWidth = 360f;
        private const byte RegionTable = 1, RegionLeft = 2, RegionRight = 3, RegionLists = 4, RegionHead = 5;

        /// <summary>Every command the screen answers, for the checks.</summary>
        public static readonly int[] Commands = AllCommands();

        private static int[] AllCommands()
        {
            var list = new List<int> { UiAppWindow.NavCommand, ActionCommand, StepLess, StepMore, BackCommand, SearchCommand };
            for (int i = 0; i <= UiDataTable.MassAnswer; i++)
            {
                list.Add(TableBase + i);
            }
            return list.ToArray();
        }

        public static void Lay(UiScene s, EstateModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            if (m.Shape == EstateShape.Rents && m.Rents != null)
            {
                UiInfoView.Lay(s, m.Rents, frame, dark, keys);   // the city's rents stand over the world with no window, as every info view
                return;
            }
            Tidy(m);
            if (m.Lower == null)
            {
                // A short page is filled with a chart of its own figures: its table's graded or money column, or its cards' money rows.
                m.Lower = (m.Shape == EstateShape.Roll || m.Shape == EstateShape.Table) && m.Table != null ? EstateCharts.FromTable(m.Table)
                    : m.Shape == EstateShape.Detail ? EstateCharts.FromCards(m.Left, m.Right, !string.IsNullOrEmpty(m.DetailTitle) ? m.DetailTitle : m.Title) : null;
            }
            s.Begin("estate", frame.Width, frame.Height, dark);
            UiAt at = UiAppWindow.Shell(s, frame, m.Title, m.Icon, m.Nav, m.Tab, m.TabsAcross, keys, out float x, out float y, out float w, out float bottom);
            switch (m.Shape)
            {
                case EstateShape.Roll:
                    RollPage(s, at, m, x, y, w, bottom, keys);
                    break;
                case EstateShape.Table:
                    TablePage(s, at, m, x, y, w, bottom, keys);
                    break;
                case EstateShape.Unit:
                    UnitPage(s, at, m, x, y, w, bottom, keys);
                    break;
                case EstateShape.Detail:
                    DetailPage(s, at, m, x, y, w, bottom, keys);
                    break;
                case EstateShape.Auction:
                    Auction(s, at, m, x, y, w, bottom, keys);
                    break;
                case EstateShape.Catalogue:
                    CataloguePage(s, at, m, x, y, w, bottom, keys);
                    break;
                case EstateShape.Chart:
                    ChartPage(s, at, m, x, y, w, bottom, keys);
                    break;
                default:
                    UiDraw.EmptyState(s, at, x, y, w, bottom - y, m.OffIcon, m.OffTitle, m.OffBody, null, -1, keys);
                    break;
            }
            for (int i = 0; i < m.Prompts.Length; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        private static short ClipOf(UiScene s, short list) => list >= 0 && list < s.ListCount ? s.Lists[list].Clip : (short)-1;

        // ------------------------------------------------------------------------------------------------- the head

        // A glyph on its tile, the page's title and a line under it, and up to four figures at the right. Returns the height.
        private static float Head(UiScene s, UiAt at, EstateModel m, float x, float y, float w)
        {
            float linkW = string.IsNullOrEmpty(m.TableLink) || m.Shape != EstateShape.Table ? 0f
                : Math.Min(UiDraw.SnapDown(w * 0.3f), UiDraw.Snap(UiTextRules.Width(m.TableLink, UiStyle.Body) + UiTokens.Icon + UiTokens.S4));
            int tile = UiDraw.Tile(s, at, x, y, TileBig, UiRole.None, false);
            float g = UiTokens.S8;
            UiDraw.Icon(s, at.In(tile), x + (TileBig - g) / 2f, y + (TileBig - g) / 2f, g, m.HeadIcon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            float figW = 0f;
            int n = Math.Min(4, Math.Min(m.FigLabels.Length, m.FigValues.Length));
            var colW = new float[n];
            for (int i = 0; i < n; i++)
            {
                colW[i] = UiDraw.Snap(Math.Max(UiTextRules.Width(m.FigLabels[i], UiStyle.SectionHeader), UiTextRules.Width(m.FigValues[i], UiStyle.Number)) + UiTokens.S1);
                figW += colW[i] + (i > 0 ? UiTokens.S6 : 0f);
            }
            float tx = x + TileBig + UiTokens.S4;
            float room = x + w - tx - (figW > 0f ? figW + UiTokens.S6 : 0f);
            UiDraw.Text(s, at, tx, y + UiTokens.S1, room, UiStyle.Display, UiRole.Text, m.HeadTitle, UiFlags.Truncate);
            float nh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display));
            UiDraw.Text(s, at, tx, y + UiTokens.S1 + nh + UiTokens.S2, room - (linkW > 0f ? linkW + UiTokens.S3 : 0f), UiStyle.Body, UiRole.Text2, m.HeadSub, UiFlags.Truncate);
            if (linkW > 0f)
            {
                float sub = y + UiTokens.S1 + nh + UiTokens.S2;
                UiDraw.LinkRow(s, at, tx + room - linkW,
                    sub - UiDraw.SnapDown((UiTokens.Control - UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body))) / 2f), linkW, UiTokens.Control, m.TableLink, ActionCommand, m.TableLinkRun);
            }
            float fx = x + w - figW;
            for (int i = 0; i < n; i++)
            {
                UiDraw.Text(s, at, fx, y + UiTokens.S1, colW[i], UiStyle.SectionHeader, UiRole.Muted, m.FigLabels[i], UiFlags.Label | UiFlags.Right | UiFlags.Truncate);
                UiRole ink = i < m.FigInks.Length && m.FigInks[i] != UiRole.None ? m.FigInks[i] : UiRole.Text;
                UiDraw.Text(s, at, fx, y + UiTokens.S1 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S1, colW[i],
                    UiStyle.Number, ink, m.FigValues[i], UiFlags.Right | UiFlags.Truncate);
                fx += colW[i] + UiTokens.S6;
            }
            return TileBig + UiTokens.S3 + (linkW > 0f ? UiTokens.S3 : 0f);
        }

        // ------------------------------------------------------------------------------------------------- the rent roll and a tab's table

        private static void RollPage(UiScene s, UiAt at, EstateModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            y += Head(s, at.InRegion(RegionHead), m, x, y, w);
            float listsH = m.ListA.Length > 0 || m.ListB.Length > 0 || !string.IsNullOrEmpty(m.ListAEmpty) || !string.IsNullOrEmpty(m.ListBEmpty) ? ListsHeight(m) : 0f;
            float lowerLeast = m.Lower != null ? LowerLeast + UiTokens.S3 : 0f;
            float room = bottom - y - (listsH > 0f ? listsH + UiTokens.S3 : 0f) - lowerLeast;
            float th = Math.Max(UiTokens.S16, Math.Min(Natural(m.Table), room));
            if (m.Lower == null || Natural(m.Table) > room)
            {
                // A table that fills the page keeps the lists at the foot and the chart out: the page is whole without it.
                th = bottom - y - (listsH > 0f ? listsH + UiTokens.S3 : 0f);
                m.Lower = null;
            }
            m.TableList = UiDataTable.Lay(s, at.InRegion(RegionTable), m.Table, x, y, w, th, keys, TableBase);
            m.TableClip = ClipOf(s, m.TableList);
            float under = y + th + UiTokens.S3;
            if (listsH > 0f)
            {
                float ly = m.Lower != null ? under : bottom - listsH, half = UiDraw.SnapDown((w - UiTokens.S6) * ListWidthShare);
                under = ly + listsH + UiTokens.S3;
                UiAt lists = at.InRegion(RegionLists);
                List(s, lists, m.ListATitle, m.ListALink, m.ListALinkRun, m.ListAEmpty, m.ListA, x, ly, half, listsH);
                List(s, lists, m.ListBTitle, m.ListBLink, m.ListBLinkRun, m.ListBEmpty, m.ListB, x + half + UiTokens.S6, ly, w - half - UiTokens.S6, listsH);
            }
            LowerCard(s, at, m, x, under, w, bottom);
        }

        private const float LowerLeast = 200f;

        // What the pages say, put in one style before they are drawn: a state chip begins with a capital, an empty cell is blank (no stray hyphen),
        // a graded figure carries its percent, and a side box that only repeats the section's name has no title.
        private static void Tidy(EstateModel m)
        {
            UiDataTableModel t = m.Table;
            for (int c = 0; t != null && t.Columns != null && c < t.Columns.Length; c++)
            {
                UiCellKind kind = t.Columns[c].Kind;
                for (int r = 0; t.Rows != null && r < t.Rows.Length; r++)
                {
                    string[] cells = t.Rows[r].Cells;
                    if (cells == null || c >= cells.Length || cells[c] == null)
                    {
                        continue;
                    }
                    string v = cells[c];
                    if (v == "-")
                    {
                        cells[c] = string.Empty;
                    }
                    else if (kind == UiCellKind.Chip && v.Length > 0 && char.IsLower(v[0]))
                    {
                        cells[c] = char.ToUpperInvariant(v[0]) + v.Substring(1);
                    }
                    else if ((kind == UiCellKind.Meter) && v.Length > 0 && v.Length <= 3 && char.IsDigit(v[v.Length - 1]))
                    {
                        cells[c] = v + "%";
                    }
                    else if (kind == UiCellKind.Meter && v.Length > 0 && char.IsLower(v[0]))
                    {
                        cells[c] = char.ToUpperInvariant(v[0]) + v.Substring(1);
                    }
                }
            }
            CapRows(m.ListA);
            CapRows(m.ListB);
        }

        private static void CapRows(EstateRow[] rows)
        {
            for (int i = 0; rows != null && i < rows.Length; i++)
            {
                string chip = rows[i].Chip;
                if (!string.IsNullOrEmpty(chip) && char.IsLower(chip[0]))
                {
                    rows[i].Chip = char.ToUpperInvariant(chip[0]) + chip.Substring(1);
                }
            }
        }

        // The height a table takes with every row showing.
        private static float Natural(UiDataTableModel t) => t == null ? 0f : UiDraw.Snap(UiDataTable.Chrome(t) + t.RowCount * UiDataTable.RowHeight(t));

        // The chart that fills the floor a short page leaves: it takes what is left, never less than it needs to read, and is not drawn where the page is full.
        private static void LowerCard(UiScene s, UiAt at, EstateModel m, float x, float y, float w, float bottom, float least = LowerLeast)
        {
            if (m.Lower == null || bottom - y < least - 0.5f)
            {
                return;
            }
            y = UiDraw.Snap(y);
            UiChart.Lay(s, at.InRegion(RegionChart), m.Lower, x, y, w, UiDraw.SnapDown(bottom - y), UiChart.First);
        }

        private const int ListRows = 2;

        private static float ListsHeight(EstateModel m) => UiDraw.Snap(UiTokens.PairRow + UiTokens.S2 + ListRows * RowHeight);

        // A short list under a table: its title in capitals with its link at the right, then its rows.
        private static void List(UiScene s, UiAt at, string title, string link, int linkRun, string empty, EstateRow[] rows, float x, float y, float w, float h)
        {
            if (string.IsNullOrEmpty(title))
            {
                return;
            }
            float lw = string.IsNullOrEmpty(link) ? 0f : Math.Min(UiDraw.SnapDown(w * 0.5f), UiDraw.Snap(UiTextRules.Width(link, UiStyle.Body) + UiTokens.Icon + UiTokens.S4));
            UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, w - lw - (lw > 0f ? UiTokens.S3 : 0f), UiStyle.SectionHeader, UiRole.Text2, title, UiFlags.Label | UiFlags.Truncate);
            if (lw > 0f)
            {
                UiDraw.LinkRow(s, at, x + w - lw, y - UiDraw.SnapDown((UiTokens.Control - UiTokens.PairRow) / 2f), lw, UiTokens.Control, link, ActionCommand, linkRun);
            }
            float ry = y + UiTokens.PairRow + UiTokens.S2;
            if (rows.Length == 0)
            {
                if (!string.IsNullOrEmpty(empty))
                {
                    UiDraw.Text(s, at, x, ry + UiTokens.S2, w, UiStyle.Body, UiRole.Text2, empty, UiFlags.Label | UiFlags.Wrap, 2);
                }
                return;
            }
            for (int i = 0; i < rows.Length && i < ListRows && ry + RowHeight <= y + h + 0.5f; i++)
            {
                Row(s, at, rows[i], x, ry, w);
                ry += RowHeight;
            }
        }

        /// <summary>A short row: a glyph (or a face) on its tile, a title with a line under it, a state chip and an amount; the whole row a target when it has a run.</summary>
        internal static void Row(UiScene s, UiAt at, EstateRow r, float x, float y, float w)
        {
            int face = UiDraw.Box(s, at, x - UiTokens.S2, y, w + UiTokens.S4, RowHeight, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt on = at.In(face);
            float tile = UiTokens.ControlSmall, ty = y + UiDraw.SnapDown((RowHeight - tile) / 2f);
            if (!string.IsNullOrEmpty(r.Face))
            {
                UiDraw.Portrait(s, on, x, ty, tile, r.Face, r.Hue, UiRole.None);
            }
            else if (r.Icon != UiIcon.None)
            {
                int t = UiDraw.Tile(s, on, x, ty, tile, r.Tint, false);
                UiDraw.Icon(s, on.In(t), x + (tile - UiTokens.Icon) / 2f, ty + (tile - UiTokens.Icon) / 2f, UiTokens.Icon, r.Icon, r.Tint, UiFlags.Middle | UiFlags.Centre, true);
            }
            float tx = x + tile + UiTokens.S3, right = x + w;
            float amount = string.IsNullOrEmpty(r.Amount) ? 0f : UiDraw.Snap(UiTextRules.Width(r.Amount, UiStyle.BodyStrong) + UiTokens.S1);
            if (amount > 0f)
            {
                UiDraw.TextMiddle(s, on, right - amount, y, RowHeight, amount, UiStyle.BodyStrong, r.AmountInk == UiRole.None ? UiRole.Text : r.AmountInk, r.Amount, UiFlags.Right);
                right -= amount + UiTokens.S3;
            }
            float chip = string.IsNullOrEmpty(r.Chip) ? 0f : Math.Min(UiDraw.SnapDown(w * 0.34f), UiFactCard.ChipWidth(r.Chip));
            if (chip > 0f)
            {
                UiDraw.Chip(s, on, right - chip, y + UiDraw.SnapDown((RowHeight - UiTokens.Badge - UiTokens.S1) / 2f), r.Chip, r.ChipTone, chip);
                right -= chip + UiTokens.S3;
            }
            float lh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title));
            bool two = !string.IsNullOrEmpty(r.Sub);
            float top = y + UiDraw.SnapDown((RowHeight - (two ? lh + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)) : lh)) / 2f);
            UiDraw.Text(s, on, tx, top, right - tx, UiStyle.Title, UiRole.Text, r.Title, UiFlags.Truncate | UiFlags.Middle);
            if (two)
            {
                UiDraw.Text(s, on, tx, top + lh, right - tx, UiStyle.Small, UiRole.Text2, r.Sub, UiFlags.Truncate | UiFlags.Middle);
            }
            if (r.Run >= 0)
            {
                UiDraw.Node(s, at, x - UiTokens.S2, y, w + UiTokens.S4, RowHeight, ActionCommand, r.Run, face);
            }
        }

        private static void TablePage(UiScene s, UiAt at, EstateModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            y += Head(s, at.InRegion(RegionHead), m, x, y, w);
            bool side = m.Side != null && Acts(m.Side);
            float sw = side ? Math.Min(SideWidth, UiDraw.SnapDown(w * 0.34f)) : 0f;
            float tw = w - (sw > 0f ? sw + UiTokens.S4 : 0f);
            float room = bottom - y - (m.Lower != null ? LowerLeast + UiTokens.S3 : 0f);
            float sideH = side ? UiFactCard.Height(m.Side, sw, keys, true) : 0f;
            float th = m.Lower != null ? Math.Max(UiTokens.S16, Math.Min(Math.Max(Natural(m.Table), sideH), room)) : bottom - y;
            m.TableList = UiDataTable.Lay(s, at.InRegion(RegionTable), m.Table, x, y, tw, th, keys, TableBase);
            m.TableClip = ClipOf(s, m.TableList);
            if (side)
            {
                UiFactCard.Lay(s, at.InRegion(RegionRight), m.Side, x + tw + UiTokens.S4, y, sw, th, keys, true);
            }
            LowerCard(s, at, m, x, y + th + UiTokens.S3, w, bottom);
        }

        // ------------------------------------------------------------------------------------------------- a page about one thing

        private static void DetailPage(UiScene s, UiAt at, EstateModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            UiAt head = at.InRegion(RegionHead);
            y = TopBar(s, head, m.Back, m.DetailActions, x, y, w, keys);
            if (!string.IsNullOrEmpty(m.DetailFace))
            {
                UiDraw.Portrait(s, head, x, y, TileBig, m.DetailFace, UiColour.HueOf(m.DetailFace), UiRole.None);
            }
            else
            {
                int tile = UiDraw.Tile(s, head, x, y, TileBig, UiRole.None, false);
                UiDraw.Icon(s, head.In(tile), x + (TileBig - UiTokens.S8) / 2f, y + (TileBig - UiTokens.S8) / 2f, UiTokens.S8, m.DetailIcon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            }
            float tx = x + TileBig + UiTokens.S4;
            float chip = string.IsNullOrEmpty(m.DetailChip) ? 0f : UiFactCard.ChipWidth(m.DetailChip);
            float nh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display));
            float nameW = Math.Min(x + w - tx - (chip > 0f ? chip + UiTokens.S3 : 0f), UiDraw.Snap(UiTextRules.Width(m.DetailTitle ?? string.Empty, UiStyle.Display) + UiTokens.S1));
            UiDraw.Text(s, head, tx, y + UiTokens.S1, nameW, UiStyle.Display, UiRole.Text, m.DetailTitle, UiFlags.Truncate);
            if (chip > 0f)
            {
                UiDraw.Chip(s, head, tx + nameW + UiTokens.S3, y + UiTokens.S1 + UiDraw.SnapDown((nh - UiTokens.Badge - UiTokens.S1) / 2f), m.DetailChip, m.DetailChipTone, chip);
            }
            float subY = y + UiTokens.S2 + nh + UiTokens.S1;
            float subRoom = x + w - tx;
            if (!string.IsNullOrEmpty(m.DetailLocate))
            {
                float lw = Math.Min(UiDraw.SnapDown(subRoom * 0.4f), UiDraw.Snap(UiTextRules.Width(m.DetailLocate, UiStyle.Body) + UiTokens.Icon + UiTokens.S4));
                UiDraw.LinkRow(s, head, x + w - lw, subY - UiDraw.SnapDown((UiTokens.Control - UiTokens.PairRow) / 2f), lw, UiTokens.Control, m.DetailLocate, ActionCommand, m.DetailLocateRun);
                subRoom -= lw + UiTokens.S3;
            }
            UiDraw.Text(s, head, tx, subY, subRoom, UiStyle.Body, UiRole.Text2, m.DetailSub, UiFlags.Truncate);
            y += TileBig + UiTokens.S4;
            bool both = m.Left.Length > 0 && m.Right.Length > 0;
            float cw = both ? UiDraw.SnapDown((w - UiTokens.S4) / 2f) : UiDraw.SnapDown(Math.Min(w, UiTokens.S16 * 40f));
            float leftH = Stack(m.Left.Length > 0 ? m.Left : m.Right, cw, keys), rightH = both ? Stack(m.Right, w - cw - UiTokens.S4, keys) : 0f;
            float natural = y + Math.Max(leftH, rightH);
            float leftBottom = bottom, rightBottom = bottom, chartX = x, chartW = w, chartY = 0f;
            bool chart = false;
            if (m.Lower != null)
            {
                if (bottom - natural - UiTokens.S3 >= LowerLeast - 0.5f)
                {
                    // Room under both columns: the chart takes the page's width.
                    leftBottom = rightBottom = Math.Max(y + UiTokens.S16 * 2f, natural);
                    chartY = natural + UiTokens.S3;
                    chart = true;
                }
                else if (both && bottom - (y + Math.Min(leftH, rightH)) - UiTokens.S3 >= LowerLeast - 0.5f)
                {
                    // One column ends well short of the other: the chart stands under it, in its width, and the page stays whole.
                    bool leftShort = leftH <= rightH;
                    float shortBottom = y + (leftShort ? leftH : rightH);
                    leftBottom = leftShort ? shortBottom : bottom;
                    rightBottom = leftShort ? bottom : shortBottom;
                    chartX = leftShort ? x : x + cw + UiTokens.S4;
                    chartW = leftShort ? cw : w - cw - UiTokens.S4;
                    chartY = shortBottom + UiTokens.S3;
                    chart = true;
                }
            }
            Column(s, at.InRegion(RegionLeft), m.Left.Length > 0 ? m.Left : m.Right, x, y, cw, leftBottom, keys);
            if (both)
            {
                Column(s, at.InRegion(RegionRight), m.Right, x + cw + UiTokens.S4, y, w - cw - UiTokens.S4, rightBottom, keys);
            }
            if (chart)
            {
                UiChart.Lay(s, at.InRegion(RegionChart), m.Lower, chartX, chartY, chartW, UiDraw.SnapDown(bottom - chartY), UiChart.First);
            }
        }

        // The way back at the left, the page's actions at the right (the main one last). Returns the y under them.
        private static float TopBar(UiScene s, UiAt at, string back, UiActionModel[] actions, float x, float y, float w, IUiKeys keys)
        {
            float h = UiTokens.Control;
            float right = x + w;
            for (int i = actions.Length - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref actions[i];
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                right -= bw;
                UiDraw.Button(s, at, right, y, bw, h, a.Kind, a.Label, a.Icon, a.Key, keys, a.Command, a.Arg, a.Disabled);
                right -= UiTokens.S3;
            }
            if (!string.IsNullOrEmpty(back))
            {
                float bw = UiDraw.ButtonWidth(UiButtonKind.Secondary, back, UiIcon.Back, UiAction.Count, keys);
                UiDraw.Button(s, at, x, y, bw, h, UiButtonKind.Secondary, back, UiIcon.Back, UiAction.Count, keys, BackCommand, 0, false);
            }
            return y + h + UiTokens.S4;
        }

        // A card's lines or buttons act when it holds a step, a switch, a link or a button: a card of words alone is not a side box worth its room.
        private static bool Acts(UiFactCardModel c)
        {
            if (c.Actions.Length > 0)
            {
                return true;
            }
            for (int i = 0; i < c.Lines.Length; i++)
            {
                UiFactKind k = c.Lines[i].Kind;
                if (k == UiFactKind.Step || k == UiFactKind.Switch || k == UiFactKind.Link)
                {
                    return true;
                }
            }
            return false;
        }

        // The height a column of cards takes with all of them whole.
        private static float Stack(UiFactCardModel[] cards, float w, IUiKeys keys)
        {
            float h = 0f;
            for (int i = 0; i < cards.Length; i++)
            {
                h += UiFactCard.Height(cards[i], w, keys, true) + UiTokens.S3;
            }
            return h;
        }

        private static void Column(UiScene s, UiAt at, UiFactCardModel[] cards, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float cy = y;
            for (int i = 0; i < cards.Length; i++)
            {
                float left = bottom - cy;
                if (left < UiTokens.S16)
                {
                    break;
                }
                cy += UiFactCard.Lay(s, at, cards[i], x, cy, w, left, keys, true) + UiTokens.S3;
            }
        }
    }
}
