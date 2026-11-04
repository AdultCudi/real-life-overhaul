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
        public const int ActionCommand = 3000, StepLess = 3001, StepMore = 3002, BackCommand = 3003;
        public const int TableBase = UiDataTable.First;
        private const float HeadHeight = 64f, RowHeight = 48f, TileBig = 56f, ListWidthShare = 0.5f, SideWidth = 360f;
        private const byte RegionTable = 1, RegionLeft = 2, RegionRight = 3, RegionLists = 4, RegionHead = 5;

        /// <summary>Every command the screen answers, for the checks.</summary>
        public static readonly int[] Commands = AllCommands();

        private static int[] AllCommands()
        {
            var list = new List<int> { UiAppWindow.NavCommand, ActionCommand, StepLess, StepMore, BackCommand };
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
            if (m.Shape == EstateShape.Auction && m.Auction != null)
            {
                s.Begin("estate", frame.Width, frame.Height, dark);
                Auction(s, m.Auction, frame, keys);
                for (int i = 0; i < m.Prompts.Length; i++)
                {
                    s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
                }
                UiDraw.PromptBar(s, frame, keys);
                return;
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
                case EstateShape.Catalogue:
                    CataloguePage(s, at, m, x, y, w, bottom, keys);
                    break;
                case EstateShape.Chart:
                    ChartPage(s, at, m, x, y, w, bottom, keys);
                    break;
                default:
                    UiDraw.EmptyState(s, at, x, y, w, bottom - y, UiIcon.Home, m.OffTitle, m.OffBody, null, -1, keys);
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
            UiDraw.Text(s, at, tx, y + UiTokens.S1 + nh + UiTokens.S1, room, UiStyle.Body, UiRole.Text2, m.HeadSub, UiFlags.Truncate);
            float fx = x + w - figW;
            for (int i = 0; i < n; i++)
            {
                UiDraw.Text(s, at, fx, y + UiTokens.S1, colW[i], UiStyle.SectionHeader, UiRole.Muted, m.FigLabels[i], UiFlags.Label | UiFlags.Right | UiFlags.Truncate);
                UiRole ink = i < m.FigInks.Length && m.FigInks[i] != UiRole.None ? m.FigInks[i] : UiRole.Text;
                UiDraw.Text(s, at, fx, y + UiTokens.S1 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S1, colW[i], UiStyle.Number, ink, m.FigValues[i], UiFlags.Right | UiFlags.Truncate);
                fx += colW[i] + UiTokens.S6;
            }
            return TileBig + UiTokens.S3;
        }

        // ------------------------------------------------------------------------------------------------- the rent roll and a tab's table

        private static void RollPage(UiScene s, UiAt at, EstateModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            y += Head(s, at.InRegion(RegionHead), m, x, y, w);
            float listsH = m.ListA.Length > 0 || m.ListB.Length > 0 || !string.IsNullOrEmpty(m.ListAEmpty) || !string.IsNullOrEmpty(m.ListBEmpty) ? ListsHeight(m) : 0f;
            float th = bottom - y - (listsH > 0f ? listsH + UiTokens.S3 : 0f);
            m.TableList = UiDataTable.Lay(s, at.InRegion(RegionTable), m.Table, x, y, w, th, keys, TableBase);
            m.TableClip = ClipOf(s, m.TableList);
            if (listsH > 0f)
            {
                float ly = bottom - listsH, half = UiDraw.SnapDown((w - UiTokens.S6) * ListWidthShare);
                UiAt lists = at.InRegion(RegionLists);
                List(s, lists, m.ListATitle, m.ListALink, m.ListALinkRun, m.ListAEmpty, m.ListA, x, ly, half, listsH);
                List(s, lists, m.ListBTitle, m.ListBLink, m.ListBLinkRun, m.ListBEmpty, m.ListB, x + half + UiTokens.S6, ly, w - half - UiTokens.S6, listsH);
            }
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
            UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, w - UiTokens.S16 * 2f, UiStyle.SectionHeader, UiRole.Text2, title, UiFlags.Label | UiFlags.Truncate);
            if (!string.IsNullOrEmpty(link))
            {
                float lw = Math.Min(UiDraw.SnapDown(w * 0.5f), UiDraw.Snap(UiTextRules.Width(link, UiStyle.Body) + UiTokens.Icon + UiTokens.S4));
                UiDraw.LinkRow(s, at, x + w - lw, y - (UiTokens.Control - UiTokens.PairRow) / 2f, lw, UiTokens.Control, link, ActionCommand, linkRun);
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
            int face = UiDraw.Box(s, at, x - UiTokens.S2, y, w + UiTokens.S4, RowHeight - UiTokens.S1, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt on = at.In(face);
            float tile = UiTokens.ControlSmall, ty = y + (RowHeight - UiTokens.S1 - tile) / 2f;
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
                UiDraw.TextMiddle(s, on, right - amount, y, RowHeight - UiTokens.S1, amount, UiStyle.BodyStrong, r.AmountInk == UiRole.None ? UiRole.Text : r.AmountInk, r.Amount, UiFlags.Right);
                right -= amount + UiTokens.S3;
            }
            float chip = string.IsNullOrEmpty(r.Chip) ? 0f : Math.Min(UiDraw.SnapDown(w * 0.34f), UiFactCard.ChipWidth(r.Chip));
            if (chip > 0f)
            {
                UiDraw.Chip(s, on, right - chip, y + (RowHeight - UiTokens.S1 - UiTokens.Badge - UiTokens.S1) / 2f, r.Chip, r.ChipTone, chip);
                right -= chip + UiTokens.S3;
            }
            float lh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title));
            bool two = !string.IsNullOrEmpty(r.Sub);
            float top = y + (RowHeight - UiTokens.S1 - (two ? lh + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)) : lh)) / 2f;
            UiDraw.Text(s, on, tx, top, right - tx, UiStyle.Title, UiRole.Text, r.Title, UiFlags.Truncate | UiFlags.Middle);
            if (two)
            {
                UiDraw.Text(s, on, tx, top + lh, right - tx, UiStyle.Small, UiRole.Text2, r.Sub, UiFlags.Truncate | UiFlags.Middle);
            }
            if (r.Run >= 0)
            {
                UiDraw.Node(s, at, x - UiTokens.S2, y, w + UiTokens.S4, RowHeight - UiTokens.S1, ActionCommand, r.Run, face);
            }
        }

        private static void TablePage(UiScene s, UiAt at, EstateModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            y += Head(s, at.InRegion(RegionHead), m, x, y, w);
            float sw = m.Side != null ? Math.Min(SideWidth, UiDraw.SnapDown(w * 0.34f)) : 0f;
            float tw = w - (sw > 0f ? sw + UiTokens.S4 : 0f);
            if (!string.IsNullOrEmpty(m.TableLink))
            {
                float lw = Math.Min(UiDraw.SnapDown(tw * 0.5f), UiDraw.Snap(UiTextRules.Width(m.TableLink, UiStyle.Body) + UiTokens.Icon + UiTokens.S4));
                UiDraw.LinkRow(s, at.InRegion(RegionHead), x + tw - lw, y - UiTokens.Control - UiTokens.S1, lw, UiTokens.Control, m.TableLink, ActionCommand, m.TableLinkRun);
            }
            m.TableList = UiDataTable.Lay(s, at.InRegion(RegionTable), m.Table, x, y, tw, bottom - y, keys, TableBase);
            m.TableClip = ClipOf(s, m.TableList);
            if (m.Side != null)
            {
                UiFactCard.Lay(s, at.InRegion(RegionRight), m.Side, x + tw + UiTokens.S4, y, sw, bottom - y, keys, true);
            }
        }

        // ------------------------------------------------------------------------------------------------- a page about one thing

        private static void DetailPage(UiScene s, UiAt at, EstateModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            UiAt head = at.InRegion(RegionHead);
            y = TopBar(s, head, m.Back, m.DetailActions, x, y, w, keys);
            int tile = UiDraw.Tile(s, head, x, y, TileBig, UiRole.None, false);
            UiDraw.Icon(s, head.In(tile), x + (TileBig - UiTokens.S8) / 2f, y + (TileBig - UiTokens.S8) / 2f, UiTokens.S8, m.DetailIcon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            float tx = x + TileBig + UiTokens.S4;
            float chip = string.IsNullOrEmpty(m.DetailChip) ? 0f : UiFactCard.ChipWidth(m.DetailChip);
            float nh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display));
            UiDraw.Text(s, head, tx, y + UiTokens.S1, x + w - tx - (chip > 0f ? chip + UiTokens.S3 : 0f), UiStyle.Display, UiRole.Text, m.DetailTitle, UiFlags.Truncate);
            if (chip > 0f)
            {
                float nameW = Math.Min(x + w - tx - chip - UiTokens.S3, UiDraw.Snap(UiTextRules.Width(m.DetailTitle ?? string.Empty, UiStyle.Display) + UiTokens.S1));
                UiDraw.Chip(s, head, tx + nameW + UiTokens.S3, y + UiTokens.S1 + (nh - UiTokens.Badge - UiTokens.S1) / 2f, m.DetailChip, m.DetailChipTone, chip);
            }
            float subY = y + UiTokens.S1 + nh + UiTokens.S1;
            float subRoom = x + w - tx;
            if (!string.IsNullOrEmpty(m.DetailLocate))
            {
                float lw = Math.Min(UiDraw.SnapDown(subRoom * 0.4f), UiDraw.Snap(UiTextRules.Width(m.DetailLocate, UiStyle.Body) + UiTokens.Icon + UiTokens.S4));
                UiDraw.LinkRow(s, head, x + w - lw, subY - (UiTokens.Control - UiTokens.PairRow) / 2f, lw, UiTokens.Control, m.DetailLocate, ActionCommand, m.DetailLocateRun);
                subRoom -= lw + UiTokens.S3;
            }
            UiDraw.Text(s, head, tx, subY, subRoom, UiStyle.Body, UiRole.Text2, m.DetailSub, UiFlags.Truncate);
            y += TileBig + UiTokens.S4;
            bool both = m.Left.Length > 0 && m.Right.Length > 0;
            float cw = both ? UiDraw.SnapDown((w - UiTokens.S4) / 2f) : UiDraw.SnapDown(Math.Min(w, UiTokens.S16 * 40f));
            Column(s, at.InRegion(RegionLeft), m.Left.Length > 0 ? m.Left : m.Right, x, y, cw, bottom, keys);
            if (both)
            {
                Column(s, at.InRegion(RegionRight), m.Right, x + cw + UiTokens.S4, y, w - cw - UiTokens.S4, bottom, keys);
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
