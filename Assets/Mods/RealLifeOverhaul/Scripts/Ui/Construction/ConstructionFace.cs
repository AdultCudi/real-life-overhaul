using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Construction app on the kit: an app window with the site office's sections down a sidebar, and a page of the model's shape. The overview puts the builder
    /// first (the firm, the next decision, the projects as stage tracks beside the money, the crews, the planning and the rivals); a tab is its table with a card of what
    /// to do beside it; one thing's page is its head with its way back over the cards that say what it is and what to do. Game-free. Cost: a layout when the model changes.
    /// </summary>
    public static partial class ConstructionFace
    {
        // The block 3000 to 3019 is shared with the Property screen's parts (its short row reads the same command); the kit's own commands repeat across parts.
        public const int ActionCommand = EstateFace.ActionCommand, StepLess = EstateFace.StepLess, StepMore = EstateFace.StepMore, BackCommand = EstateFace.BackCommand;
        public const int TableBase = UiDataTable.First;
        private const float HeadHeight = 64f, TileBig = 56f, ProjectRow = 84f, DayRow = 32f, SideWidth = 520f, TableSideWidth = 400f, MinSide = 300f,
            NameLeast = 200f, TwoColumnsFrom = 1000f, ThreeColumnsFrom = 1100f, TrackLeast = 360f, TrackMost = 380f;
        private const byte RegionHead = 1, RegionLead = 2, RegionLeft = 3, RegionRight = 4, RegionTable = 5, RegionWeek = 6, RegionCards = 7;

        /// <summary>Every command the screen answers, for the checks.</summary>
        public static readonly int[] Commands = AllCommands();

        private static int[] AllCommands()
        {
            var list = new List<int> { UiAppWindow.NavCommand, ActionCommand, StepLess, StepMore, BackCommand, UiTrack.First + UiTrack.Step };
            for (int i = 0; i <= UiDataTable.MassAnswer; i++)
            {
                list.Add(TableBase + i);
            }
            list.Add(TableBase + UiDataTable.Act);
            return list.ToArray();
        }

        public static void Lay(UiScene s, ConstructionModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("construction", frame.Width, frame.Height, dark);
            UiAt at = UiAppWindow.Shell(s, frame, m.Title, m.Icon, m.Nav, m.Tab, false, keys, out float x, out float y, out float w, out float bottom);
            m.Dropped = 0;
            switch (m.Shape)
            {
                case ConstructionShape.Overview:
                    OverviewPage(s, at, m, x, y, w, bottom, keys);
                    break;
                case ConstructionShape.Table:
                    TablePage(s, at, m, x, y, w, bottom, keys);
                    break;
                case ConstructionShape.Detail:
                    DetailPage(s, at, m, x, y, w, bottom, keys);
                    break;
                default:
                    int leave = UiDraw.EmptyState(s, at, x, y, w, bottom - y, m.Icon, m.OffTitle, m.OffBody, m.OffAction, UiDraw.CloseCommand, keys);
                    if (leave >= 0)
                    {
                        s.FocusFirst = leave;   // a builder's office that is off has one thing to do, and focus starts on it rather than on the window's close
                    }
                    break;
            }
            for (int i = 0; i < m.Prompts.Length; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        // ------------------------------------------------------------------------------------------------- the head

        // A mark or a glyph on its tile, the page's title and a line under it, and up to four figures at the right. Returns the height.
        private static float Head(UiScene s, UiAt at, ConstructionModel m, float x, float y, float w)
        {
            UiAt on = at.InRegion(RegionHead);
            int tile = UiDraw.Tile(s, on, x, y, TileBig, UiRole.None, false);
            if (!string.IsNullOrEmpty(m.HeadMark))
            {
                UiDraw.TextMiddle(s, on.In(tile), x, y, TileBig, TileBig, UiStyle.Display, UiRole.AccentText, m.HeadMark, UiFlags.Centre);
            }
            else
            {
                float g = UiTokens.S8;
                UiDraw.Icon(s, on.In(tile), x + (TileBig - g) / 2f, y + (TileBig - g) / 2f, g, m.HeadIcon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            }
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
            UiDraw.Text(s, on, tx, y + UiTokens.S1, room, UiStyle.Display, UiRole.Text, m.HeadTitle, UiFlags.Truncate | UiFlags.Middle);
            float nh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display));
            UiDraw.Text(s, on, tx, y + UiTokens.S1 + nh + UiTokens.S2, room, UiStyle.Body, UiRole.Text2, m.HeadSub, UiFlags.Truncate | UiFlags.Middle);
            float fx = x + w - figW;
            for (int i = 0; i < n; i++)
            {
                UiDraw.Text(s, on, fx, y + UiTokens.S1, colW[i], UiStyle.SectionHeader, UiRole.Muted, m.FigLabels[i],
                    UiFlags.Label | UiFlags.Right | UiFlags.Truncate | UiFlags.Middle);
                UiRole ink = i < m.FigInks.Length && m.FigInks[i] != UiRole.None ? m.FigInks[i] : UiRole.Text;
                UiDraw.Text(s, on, fx, y + UiTokens.S1 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S1, colW[i], UiStyle.Number, ink, m.FigValues[i],
                    UiFlags.Right | UiFlags.Truncate | UiFlags.Middle);
                fx += colW[i] + UiTokens.S6;
            }
            return TileBig + UiTokens.S3;
        }

        // A section's label in capitals with a link or a count at the right. Returns the y under it.
        private static float Section(UiScene s, UiAt at, string title, string note, int run, float x, float y, float w)
        {
            float lh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader));
            float nw = string.IsNullOrEmpty(note) ? 0f : Math.Min(UiDraw.SnapDown(w * 0.4f), UiDraw.Snap(UiTextRules.Width(note, UiStyle.Body) + UiTokens.Icon + UiTokens.S4));
            UiDraw.Text(s, at, x, y, w - nw - (nw > 0f ? UiTokens.S3 : 0f), UiStyle.SectionHeader, UiRole.Text2, title, UiFlags.Label | UiFlags.Truncate | UiFlags.Middle);
            if (nw > 0f)
            {
                if (run >= 0)
                {
                    UiDraw.LinkRow(s, at, x + w - nw, y - UiDraw.SnapDown((UiTokens.Control - lh) / 2f), nw, UiTokens.Control, note, ActionCommand, run);
                }
                else
                {
                    UiDraw.Text(s, at, x + w - nw, y, nw, UiStyle.Small, UiRole.Muted, note, UiFlags.Right | UiFlags.Truncate | UiFlags.Middle);
                }
            }
            return y + Math.Max(lh, run >= 0 && nw > 0f ? UiTokens.Control - UiTokens.S3 : 0f) + UiTokens.S2;
        }

        // ------------------------------------------------------------------------------------------------- the overview

        private static void OverviewPage(UiScene s, UiAt at, ConstructionModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            y += Head(s, at, m, x, y, w);
            if (m.Lead != null)
            {
                y += LeadBand(s, at.InRegion(RegionLead), m.Lead, x, y, w, keys) + UiTokens.S4;
            }
            bool two = w >= TwoColumnsFrom;
            float rw = two ? Math.Min(SideWidth, UiDraw.SnapDown(w * 0.36f)) : 0f;
            float lw = two ? w - rw - UiTokens.S6 : w;
            UiAt left = at.InRegion(RegionLeft);
            float ly = Section(s, left, m.RowsTitle, m.RowsLink, m.RowsLinkRun, x, y, lw);
            if (m.Projects.Length == 0)
            {
                UiDraw.Text(s, left, x, ly + UiTokens.S2, lw, UiStyle.Body, UiRole.Text2, m.RowsEmpty, UiFlags.Label | UiFlags.Wrap, 3);
                ly += UiDraw.Snap(UiTextRules.WrappedHeight(m.RowsEmpty ?? string.Empty, UiStyle.Body, lw, 3)) + UiTokens.S6;
            }
            float weekH = m.Week.Length > 0 ? UiDraw.Snap(UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2 + m.Week.Length * DayRow) + UiTokens.S4 : 0f;
            int rows = m.Projects.Length;
            // Rows that do not leave the week's list its room are cut at the row that would cross it, never drawn over it.
            int fit = Math.Max(1, (int)((bottom - ly - weekH) / ProjectRow));
            // The chip and the money stand in columns as wide as the widest of the rows, so the tracks between them start at one place.
            float chipCol = 0f, moneyCol = 0f;
            for (int i = 0; i < rows; i++)
            {
                ConstructionProject p = m.Projects[i];
                chipCol = Math.Max(chipCol, string.IsNullOrEmpty(p.Chip) ? 0f : Math.Min(UiDraw.SnapDown(lw * 0.3f), UiFactCard.ChipWidth(p.Chip)));
                float spentW = UiTextRules.Width(p.Spent ?? string.Empty, UiStyle.BodyStrong), ofW = UiTextRules.Width(p.Of ?? string.Empty, UiStyle.Small);
                moneyCol = Math.Max(moneyCol, UiDraw.Snap(Math.Max(spentW, ofW) + UiTokens.S1));
            }
            for (int i = 0; i < Math.Min(rows, fit); i++)
            {
                ProjectLay(s, left, m.Projects[i], x, ly + i * ProjectRow, lw, chipCol, moneyCol);
            }
            m.Dropped += Math.Max(0, rows - fit);
            ly += Math.Min(rows, fit) * ProjectRow;
            if (m.Week.Length > 0 && bottom - ly >= weekH - UiTokens.S4)
            {
                UiAt week = at.InRegion(RegionWeek);
                float wy = Section(s, week, m.WeekTitle, null, -1, x, ly + UiTokens.S3, lw);
                for (int i = 0; i < m.Week.Length && wy + DayRow <= bottom + 0.5f; i++)
                {
                    DayLay(s, week, m.Week[i], x, wy, lw);
                    wy += DayRow;
                }
            }
            if (two)
            {
                float sx = x + lw + UiTokens.S6, sy = y;
                UiAt right = at.InRegion(RegionRight);
                for (int i = 0; i < m.Side.Length; i++)
                {
                    float room = bottom - sy;
                    if (room < UiTokens.S16 * 2f)
                    {
                        m.Dropped += m.Side.Length - i;
                        break;
                    }
                    sy += UiFactCard.Lay(s, right, m.Side[i], sx, sy, rw, room, keys, false) + UiTokens.S3;
                }
            }
        }

        private static float LeadBand(UiScene s, UiAt at, ConstructionLead lead, float x, float y, float w, IUiKeys keys)
        {
            float pad = UiTokens.S4, actionsW = 0f;
            for (int i = 0; i < lead.Actions.Length; i++)
            {
                actionsW += Math.Min(UiDraw.SnapDown(w * 0.3f), UiDraw.ButtonWidth(lead.Actions[i].Kind, lead.Actions[i].Label, lead.Actions[i].Icon,
                    lead.Actions[i].Key, keys)) + UiTokens.S3;
            }
            float tile = UiTokens.IconTile + UiTokens.S4, tx = x + pad + tile + UiTokens.S4, room = x + w - pad - tx - (actionsW > 0f ? actionsW + UiTokens.S4 : 0f);
            float kh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)), th = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Number));
            float bh = string.IsNullOrEmpty(lead.Body) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(lead.Body, UiStyle.Body, room, 2));
            float h = UiDraw.Snap(pad * 2f + kh + UiTokens.S1 + th + (bh > 0f ? UiTokens.S1 + bh : 0f));
            int box = UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt on = at.In(box);
            int t = UiDraw.Tile(s, on, x + pad, y + UiDraw.SnapDown((h - UiTokens.IconTile) / 2f), UiTokens.IconTile, UiRole.None, false);
            UiDraw.Icon(s, on.In(t), x + pad + (UiTokens.IconTile - UiTokens.S6) / 2f, y + UiDraw.SnapDown((h - UiTokens.S6) / 2f), UiTokens.S6,
                lead.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            float cy = y + pad;
            UiDraw.Text(s, on, tx, cy, room, UiStyle.SectionHeader, UiRole.Muted, lead.Kicker, UiFlags.Label | UiFlags.Truncate | UiFlags.Middle);
            cy += kh + UiTokens.S1;
            UiDraw.Text(s, on, tx, cy, room, UiStyle.Number, UiRole.Text, lead.Title, UiFlags.Truncate | UiFlags.Middle);
            cy += th + UiTokens.S1;
            if (bh > 0f)
            {
                UiDraw.Text(s, on, tx, cy, room, UiStyle.Body, UiRole.Text2, lead.Body, UiFlags.Wrap | UiFlags.Label, 2);
            }
            float right = x + w - pad;
            for (int i = lead.Actions.Length - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref lead.Actions[i];
                float bw = Math.Min(UiDraw.SnapDown(w * 0.3f), UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys));
                right -= bw;
                UiDraw.Button(s, on, right, y + UiDraw.SnapDown((h - UiTokens.Control) / 2f), bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key,
                    keys, a.Command, a.Arg, a.Disabled);
                right -= UiTokens.S3;
            }
            return h;
        }

        // A project: its glyph on a tile, its name and place, its stage track with the works' bar under it, what it has cost against its budget and the word that says whether it
        // waits on the player. The whole row opens the project.
        private static void ProjectLay(UiScene s, UiAt at, ConstructionProject p, float x, float y, float w, float chipCol, float moneyCol)
        {
            int face = UiDraw.Box(s, at, x - UiTokens.S2, y, w + UiTokens.S4, ProjectRow - UiTokens.S2, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt on = at.In(face);
            float tile = UiTokens.IconTile + UiTokens.S4, ty = y + UiDraw.SnapDown((ProjectRow - UiTokens.S2 - tile) / 2f);
            int t = UiDraw.Tile(s, on, x, ty, tile, UiRole.None, false);
            UiDraw.Icon(s, on.In(t), x + (tile - UiTokens.S6) / 2f, ty + (tile - UiTokens.S6) / 2f, UiTokens.S6, p.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            float chip = string.IsNullOrEmpty(p.Chip) ? 0f : Math.Min(chipCol, UiFactCard.ChipWidth(p.Chip)), money = moneyCol;
            float right = x + w;
            if (chip > 0f)
            {
                UiDraw.Chip(s, on, right - chip, y + UiDraw.SnapDown((ProjectRow - UiTokens.S2 - UiTokens.Badge - UiTokens.S1) / 2f), p.Chip, p.ChipTone, chip);
            }
            right -= chipCol + UiTokens.S4;   // the chip and the money stand in columns as wide as the rows' widest, so the tracks start at one place
            float mh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.BodyStrong)) + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            float my = y + UiDraw.SnapDown((ProjectRow - UiTokens.S2 - mh) / 2f);
            UiDraw.Text(s, on, right - money, my, money, UiStyle.BodyStrong, UiRole.Text, p.Spent, UiFlags.Right | UiFlags.Truncate | UiFlags.Middle);
            UiDraw.Text(s, on, right - money, my + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.BodyStrong)), money, UiStyle.Small, UiRole.Text2, p.Of,
                UiFlags.Right | UiFlags.Truncate | UiFlags.Middle);
            right -= money + UiTokens.S4;
            float nx = x + tile + UiTokens.S3, avail = right - nx;
            bool track = p.StepLabels != null && p.StepLabels.Length > 0 && avail >= TrackLeast;
            float trackW = track ? Math.Min(UiDraw.SnapDown(avail * 0.52f), TrackMost) : 0f, nameW = avail - trackW - (track ? UiTokens.S4 : 0f);
            float lh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)), sh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)), top = y
                + UiDraw.SnapDown((ProjectRow - UiTokens.S2 - lh - sh) / 2f);
            UiDraw.Text(s, on, nx, top, nameW, UiStyle.Title, UiRole.Text, p.Title, UiFlags.Truncate | UiFlags.Middle);
            UiDraw.Text(s, on, nx, top + lh, nameW, UiStyle.Small, UiRole.Text2, p.Sub, UiFlags.Truncate | UiFlags.Middle);
            if (track)
            {
                MiniTrack(s, on, p, right - trackW, y + UiTokens.S2, trackW);
            }
            UiDraw.Node(s, at, x - UiTokens.S2, y, w + UiTokens.S4, ProjectRow - UiTokens.S2, ActionCommand, p.Run, face);
        }

        // The stage track in small: a dot to a step joined by a rule that runs cyan up to the step in hand, the step's name under it, and the works' bar under those.
        private static void MiniTrack(UiScene s, UiAt at, ConstructionProject p, float x, float y, float w)
        {
            int n = p.StepLabels.Length;
            float slot = w / n, dot = UiTokens.S5;
            for (int i = 1; i < n; i++)
            {
                float x0 = x + slot * (i - 0.5f) + dot / 2f, x1 = x + slot * (i + 0.5f) - dot / 2f;
                UiDraw.Box(s, at, x0, y + (dot - 2f) / 2f, Math.Max(0f, x1 - x0), 2f, i <= p.StepAt ? UiRole.Accent : UiRole.Track, UiRole.None, 1f, UiFlags.Decor);
            }
            for (int i = 0; i < n; i++)
            {
                float cx = x + slot * (i + 0.5f) - dot / 2f;
                bool reached = i <= p.StepAt;
                int d = UiDraw.Box(s, at, cx, y, dot, dot, reached ? UiRole.Accent : UiRole.Track, UiRole.None, dot / 2f, UiFlags.Decor);
                if (i < p.StepAt)
                {
                    UiDraw.Icon(s, at.In(d), cx + (dot - UiTokens.IconSmall) / 2f, y + (dot - UiTokens.IconSmall) / 2f, UiTokens.IconSmall,
                        UiIcon.Check, UiRole.OnAccent, UiFlags.Middle | UiFlags.Centre, true);
                }
                UiDraw.Text(s, at, x + slot * i, y + dot + UiTokens.S1, slot, UiStyle.Small, i == p.StepAt ? UiRole.Text : UiRole.Text2,
                    p.StepLabels[i], UiFlags.Centre | UiFlags.Truncate | UiFlags.Middle);
            }
            if (p.Progress >= 0)
            {
                float by = y + dot + UiTokens.S1 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S2;
                UiDraw.Box(s, at, x, by, w, UiTokens.S1, UiRole.Track, UiRole.None, UiTokens.S1 / 2f, UiFlags.Decor);
                UiDraw.Box(s, at, x, by, Math.Max(UiTokens.S1, w * Math.Min(100, p.Progress) / 100f), UiTokens.S1, p.StepAt >= n
                    ? UiRole.Ok : UiRole.Accent, UiRole.None, UiTokens.S1 / 2f, UiFlags.Decor);
            }
        }

        // A dated line: the day at the left, what happens, what it costs or pays at the right. A line that opens something is a target over its whole width.
        private static void DayLay(UiScene s, UiAt at, ConstructionDay d, float x, float y, float w)
        {
            int face = d.Run >= 0 ? UiDraw.Box(s, at, x - UiTokens.S2, y, w + UiTokens.S4, DayRow, UiRole.None, UiRole.None, UiTokens.RadiusMedium) : -1;
            UiAt on = face >= 0 ? at.In(face) : at;
            float dayW = UiDraw.Snap(UiTextRules.Width("Day 000", UiStyle.Small) + UiTokens.S3), amountW = UiDraw.SnapDown(Math.Min(w * 0.22f,
                UiDraw.Snap(UiTextRules.Width(d.Amount ?? string.Empty, UiStyle.BodyStrong) + UiTokens.S1)));
            UiDraw.TextMiddle(s, on, x, y, DayRow, dayW, UiStyle.Small, UiRole.Muted, d.When, UiFlags.Truncate);
            if (amountW > 0f)
            {
                UiDraw.TextMiddle(s, on, x + w - amountW, y, DayRow, amountW, UiStyle.BodyStrong, d.AmountInk == UiRole.None
                    ? UiRole.Text : d.AmountInk, d.Amount, UiFlags.Right | UiFlags.Truncate);
            }
            UiDraw.TextMiddle(s, on, x + dayW, y, DayRow, w - dayW - amountW - UiTokens.S3, UiStyle.Body, UiRole.Text2, d.What, UiFlags.Truncate);
            UiDraw.Box(s, on, x, y + DayRow - UiTokens.Hairline, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            if (face >= 0)
            {
                UiDraw.Node(s, at, x - UiTokens.S2, y, w + UiTokens.S4, DayRow, ActionCommand, d.Run, face);
            }
        }

        // ------------------------------------------------------------------------------------------------- a tab's table

        private static void TablePage(UiScene s, UiAt at, ConstructionModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            y += Head(s, at, m, x, y, w);
            if (m.Lead != null)
            {
                y += LeadBand(s, at.InRegion(RegionLead), m.Lead, x, y, w, keys) + UiTokens.S4;
            }
            bool side = m.Card != null;
            float sw = side ? Math.Max(MinSide, Math.Min(TableSideWidth, UiDraw.SnapDown(w * 0.3f))) : 0f;
            float tw = w - (sw > 0f ? sw + UiTokens.S4 : 0f), th = bottom - y;
            float footH = m.Foot != null ? UiFactCard.Height(m.Foot, tw, keys, true) : 0f;
            if (footH > 0f)
            {
                float room = th - footH - UiTokens.S3;
                if (room >= UiTokens.S16 * 3f)
                {
                    th = UiDraw.SnapDown(room);
                }
                else
                {
                    footH = 0f;   // no room for both: the table, which is what the page is for, keeps it
                    m.Dropped += 1;
                }
            }
            if (m.Table != null)
            {
                float[] widths = Narrow(m.Table, tw);
                m.TableList = UiDataTable.Lay(s, at.InRegion(RegionTable), m.Table, x, y, tw, th, keys, TableBase);
                for (int c = 0; widths != null && c < widths.Length; c++)
                {
                    m.Table.Columns[c].Width = widths[c];
                }
                m.TableClip = m.TableList >= 0 && m.TableList < s.ListCount ? s.Lists[m.TableList].Clip : (short)-1;
            }
            if (footH > 0f)
            {
                UiFactCard.Lay(s, at.InRegion(RegionRight), m.Foot, x, y + th + UiTokens.S3, tw, footH, keys, true);
            }
            if (side)
            {
                UiFactCard.Lay(s, at.InRegion(RegionRight), m.Card, x + tw + UiTokens.S4, y, sw, th, keys, true);   // as tall as its words: an empty band to the floor says nothing
            }
        }

        // The fixed columns of a table beside a card are narrowed together, to as little as two thirds, when the room is less than they ask for with
        // a name column of its own left over;
        // the widths are the table's own again after the lay. Returns the widths to put back, or null when the table stands as it is.
        private static float[] Narrow(UiDataTableModel t, float w)
        {
            float fixedW = 0f;
            int flex = 0;
            for (int c = 0; c < t.Columns.Length; c++)
            {
                if (t.Columns[c].Width > 0f)
                {
                    fixedW += t.Columns[c].Width;
                }
                else
                {
                    flex++;
                }
            }
            float need = fixedW + UiTokens.S4 * (t.Columns.Length + 1) + (flex > 0 ? NameLeast : 0f);
            if (fixedW <= 0f || need <= w)
            {
                return null;
            }
            float keep = Math.Max(0.67f, (w - UiTokens.S4 * (t.Columns.Length + 1) - (flex > 0 ? NameLeast : 0f)) / fixedW);
            var saved = new float[t.Columns.Length];
            for (int c = 0; c < saved.Length; c++)
            {
                saved[c] = t.Columns[c].Width;
                if (t.Columns[c].Width > 0f)
                {
                    t.Columns[c].Width = UiDraw.SnapDown(t.Columns[c].Width * Math.Min(1f, keep));
                }
            }
            return saved;
        }

        // ------------------------------------------------------------------------------------------------- one thing's page

        private static void DetailPage(UiScene s, UiAt at, ConstructionModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            UiAt head = at.InRegion(RegionHead);
            float h = UiTokens.Control;
            if (!string.IsNullOrEmpty(m.Back))
            {
                float bw = UiDraw.ButtonWidth(UiButtonKind.Secondary, m.Back, UiIcon.Back, UiAction.Count, keys);
                UiDraw.Button(s, head, x, y, bw, h, UiButtonKind.Secondary, m.Back, UiIcon.Back, UiAction.Count, keys, BackCommand, 0, false);
            }
            float right = x + w;
            for (int i = m.Actions.Length - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref m.Actions[i];
                float bw = Math.Min(UiDraw.SnapDown(w * 0.34f), UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys));
                if (right - bw < x + UiDraw.SnapDown(w * 0.2f))
                {
                    break;
                }
                right -= bw;
                UiDraw.Button(s, head, right, y, bw, h, a.Kind, a.Label, a.Icon, a.Key, keys, a.Command, a.Arg, a.Disabled);
                right -= UiTokens.S3;
            }
            y += h + UiTokens.S4;
            int tile = UiDraw.Tile(s, head, x, y, TileBig, UiRole.None, false);
            UiDraw.Icon(s, head.In(tile), x + (TileBig - UiTokens.S8) / 2f, y + (TileBig - UiTokens.S8) / 2f, UiTokens.S8, m.DetailIcon, UiRole.None,
                UiFlags.Middle | UiFlags.Centre, true);
            float tx = x + TileBig + UiTokens.S4;
            float chip = string.IsNullOrEmpty(m.DetailChip) ? 0f : Math.Min(UiDraw.SnapDown(w * 0.3f), UiFactCard.ChipWidth(m.DetailChip));
            float nh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display));
            float nameW = Math.Min(x + w - tx - (chip > 0f ? chip + UiTokens.S3 : 0f), UiDraw.Snap(UiTextRules.Width(m.DetailTitle ?? string.Empty,
                UiStyle.Display) + UiTokens.S1));
            UiDraw.Text(s, head, tx, y + UiTokens.S1, nameW, UiStyle.Display, UiRole.Text, m.DetailTitle, UiFlags.Truncate | UiFlags.Middle);
            if (chip > 0f)
            {
                UiDraw.Chip(s, head, tx + nameW + UiTokens.S3, y + UiTokens.S1 + UiDraw.SnapDown((nh - UiTokens.Badge - UiTokens.S1) / 2f), m.DetailChip, m.DetailChipTone, chip);
            }
            float subY = y + UiTokens.S2 + nh + UiTokens.S1, subRoom = x + w - tx;
            if (!string.IsNullOrEmpty(m.DetailLocate))
            {
                float lw = Math.Min(UiDraw.SnapDown(subRoom * 0.4f), UiDraw.Snap(UiTextRules.Width(m.DetailLocate, UiStyle.Body) + UiTokens.Icon + UiTokens.S4));
                UiDraw.LinkRow(s, head, x + w - lw, subY - UiDraw.SnapDown((UiTokens.Control - UiTokens.PairRow) / 2f), lw, UiTokens.Control,
                    m.DetailLocate, ActionCommand, m.DetailLocateRun);
                subRoom -= lw + UiTokens.S3;
            }
            UiDraw.Text(s, head, tx, subY, subRoom, UiStyle.Body, UiRole.Text2, m.DetailSub, UiFlags.Truncate | UiFlags.Middle);
            // The map link's target stands a little below the head's tile: what follows starts under both.
            y += Math.Max(TileBig + UiTokens.S4, string.IsNullOrEmpty(m.DetailLocate)
                ? 0f : UiDraw.Snap(subY - UiDraw.SnapDown((UiTokens.Control - UiTokens.PairRow) / 2f) + UiTokens.Control - y + UiTokens.S1));
            if (m.Track != null && m.Track.Steps.Length > 0)
            {
                y += UiTrack.Lay(s, at.InRegion(RegionLead), m.Track, x, y, w) + UiTokens.S4;
            }
            if (m.DetailTable != null)
            {
                float th = Math.Min(UiDraw.Snap(UiDataTable.Chrome(m.DetailTable) + m.DetailTable.RowCount * UiDataTable.RowHeight(m.DetailTable)), UiDraw.SnapDown((bottom - y) * 0.45f));
                float[] widths = Narrow(m.DetailTable, w);
                m.TableList = UiDataTable.Lay(s, at.InRegion(RegionTable), m.DetailTable, x, y, w, th, keys, TableBase);
                for (int c = 0; widths != null && c < widths.Length; c++)
                {
                    m.DetailTable.Columns[c].Width = widths[c];
                }
                m.TableClip = m.TableList >= 0 && m.TableList < s.ListCount ? s.Lists[m.TableList].Clip : (short)-1;
                y += th + UiTokens.S4;
            }
            Columns(s, at.InRegion(RegionCards), m, x, y, w, bottom, keys);
        }

        // The cards in the order they were written, stood in two columns (three where two would not hold them): each goes under the shorter column, so the page reads left to
        // right and top to bottom. A card that does not fit what is left is cut at its last whole line and counted, which a check holds at nothing.
        private static void Columns(UiScene s, UiAt at, ConstructionModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            int cols = w >= 700f ? 2 : 1;
            float gap = UiTokens.S4;
            if (cols == 2 && Stacked(m.Cards, UiDraw.SnapDown((w - gap) / 2f), keys, 2) > bottom - y && w >= ThreeColumnsFrom)
            {
                cols = 3;
            }
            float cw = UiDraw.SnapDown((w - gap * (cols - 1)) / cols);
            var heights = new float[cols];
            for (int i = 0; i < m.Cards.Length; i++)
            {
                int c = 0;
                for (int k = 1; k < cols; k++)
                {
                    c = heights[k] < heights[c] ? k : c;
                }
                float room = bottom - y - heights[c];
                float need = UiFactCard.Height(m.Cards[i], cw, keys, true);
                if (room < UiTokens.S16 + UiTokens.S4)
                {
                    m.Dropped += m.Cards[i].Lines.Length + m.Cards[i].Actions.Length;
                    continue;
                }
                if (need > room)
                {
                    m.Dropped += 1;
                }
                heights[c] += UiFactCard.Lay(s, at, m.Cards[i], x + c * (cw + gap), y + heights[c], cw, room, keys, true) + UiTokens.S3;
            }
        }

        private static float Stacked(UiFactCardModel[] cards, float cw, IUiKeys keys, int cols)
        {
            var heights = new float[cols];
            for (int i = 0; i < cards.Length; i++)
            {
                int c = 0;
                for (int k = 1; k < cols; k++)
                {
                    c = heights[k] < heights[c] ? k : c;
                }
                heights[c] += UiFactCard.Height(cards[i], cw, keys, true) + UiTokens.S3;
            }
            float most = 0f;
            for (int k = 0; k < cols; k++)
            {
                most = Math.Max(most, heights[k]);
            }
            return most;
        }
    }
}
