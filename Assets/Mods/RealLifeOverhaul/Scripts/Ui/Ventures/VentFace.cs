using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The face of the Brands, Ventures and Web shop apps: an app window with its sections as icon tabs across, a head (a profile or a hero with figures, the page's
    /// buttons at its right) and a left and a right column of blocks. Game-free; the model says what, this says where. Cost: a layout when the model changes.
    /// </summary>
    public static partial class VentFace
    {
        // The block 7650 to 7679 is these apps' own. The kit's numbers repeat across its parts (a nav is 1, a decision's choice is 1), so every kind of target here has a number
        // nobody else uses, and a handler tests the shape it belongs to before it acts.
        public const int ActionCommand = 7650;   // a button of a column: the index of its action in the page's Actions block, with the block in the high bits
        public const int ItemCommand = 7651;     // a short row: the row's run
        public const int LinkCommand = 7652;     // a link or a switch or a selector: the block's run
        public const int StepLess = 7653, StepMore = 7654, StepType = 7655;   // a stepper: the argument is the block's run of its minus
        public const int BackCommand = 7656;     // the way out of a page under a tab
        public const int HeadCommand = 7657;     // a button at the head's right: the index of the action
        public const int TableTwo = 140;         // the second table of a page: its commands from here
        public const int SearchArg = 1000;       // the argument of the search field's typing target
        public const int BackArg = -77;          // an action's argument that means the way back
        public const float Gap = UiTokens.S6;

        private const float ColumnShare = 0.58f, NarrowBelow = 900f, ItemRow = 56f, MinChart = 160f;
        private const int MinRows = 3, EmptyRows = 5;   // an empty table keeps room for what would fill it

        /// <summary>Every command the screen answers, for the checks.</summary>
        public static readonly int[] Commands = AllCommands();

        private static int[] AllCommands()
        {
            var list = new List<int>
            {
                UiAppWindow.NavCommand, UiAppWindow.ActionCommand, ActionCommand, ItemCommand, LinkCommand, StepLess, StepMore, StepType, BackCommand, HeadCommand,
                    UiChart.First + UiChart.Point
            };
            for (int b = 0; b < 2; b++)
            {
                int start = b == 0 ? UiDataTable.First : TableTwo;
                for (int i = 0; i <= UiDataTable.MassAnswer; i++)
                {
                    list.Add(start + i);
                }
                list.Add(start + UiDataTable.Act);
                list.Add(start + UiDataTable.Less);
                list.Add(start + UiDataTable.More);
                list.Add(start + UiDataTable.Type);
            }
            return list.ToArray();
        }

        public static void Lay(UiScene s, VentModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("ventures", frame.Width, frame.Height, dark);
            UiAt at = UiAppWindow.Shell(s, frame, m.Title, m.Icon, m.Nav, m.Tab, true, keys, out float x, out float y, out float w, out float bottom);
            if (m.Shape == VentShape.Off)
            {
                UiDraw.Empty(s, at, x, y, w, bottom - y, m.OffTitle, m.OffBody);
            }
            else
            {
                y = Head(s, at, m, x, y, w, keys);
                Columns(s, at, m, x, y, w, bottom, keys);
            }
            for (int i = 0; i < 3; i++)
            {
                s.AddPrompt(i == 0 ? UiAction.Move : i == 1 ? UiAction.Confirm : UiAction.Back, i < m.Prompts.Length ? m.Prompts[i] : i == 0 ? "Move" : i == 1 ? "Select" : "Back");
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        // The head: a profile or a hero with its figures, or a line of what to do, and the buttons at the right.
        private static float Head(UiScene s, UiAt at, VentModel m, float x, float y, float w, IUiKeys keys)
        {
            UiActionModel[] acts = Heads(m.HeadActions);
            if (m.Profile != null)
            {
                return UiAppWindow.Profile(s, at, m.Profile, acts, x, y, w, keys);
            }
            y = UiAppWindow.Lead(s, at, m.Next, acts, x, y, w, keys);
            if (m.Hero != null)
            {
                float fw = m.HeroFigures.Length > 0 ? UiDraw.SnapDown(w * 0.4f) : 0f;
                float hh = UiAppWindow.Hero(s, at, m.Hero, x, y, fw > 0f ? w - fw - UiTokens.S4 : w);
                if (fw > 0f)
                {
                    UiAppWindow.Figures(s, at, m.HeroFigures, x + w - fw, y, fw, hh);
                }
                y += hh + UiTokens.S5;
            }
            else if (m.HeroFigures.Length > 0)
            {
                y = UiAppWindow.Figures(s, at, m.HeroFigures, x, y, w);
            }
            return y;
        }

        // The head's buttons are the window's own: their argument is their place in this list, and the screen reads the model's action at that place.
        private static UiActionModel[] Heads(UiActionModel[] actions) => actions ?? new UiActionModel[0];

        private static void Columns(UiScene s, UiAt at, VentModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (m.Left.Length == 0 && m.Right.Length == 0)
            {
                if (!string.IsNullOrEmpty(m.Empty))
                {
                    UiDraw.Empty(s, at, x, y, w, bottom - y, m.Empty, null);
                }
                return;
            }
            if (m.Right.Length == 0 || w < NarrowBelow)
            {
                var all = new VentBlock[m.Left.Length + m.Right.Length];
                Array.Copy(m.Left, all, m.Left.Length);
                Array.Copy(m.Right, 0, all, m.Left.Length, m.Right.Length);
                Column(s, at, all, x, y, w, bottom, keys);
                return;
            }
            float lw = UiDraw.SnapDown((w - Gap) * ColumnShare), rw = w - Gap - lw;
            Column(s, at.InRegion(3), m.Left, x, y, lw, bottom, keys);
            Column(s, at.InRegion(4), m.Right, x + lw + Gap, y, rw, bottom, keys);
        }

        // One column of blocks. What follows a table or a chart keeps the room it needs, so they take what is left; the buttons that end a column stand at its foot; a block that
        // would not fit is left out rather than drawn over the window's edge.
        private static void Column(UiScene s, UiAt at, VentBlock[] blocks, float x, float y, float w, float bottom, IUiKeys keys)
        {
            int n = blocks.Length;
            var need = new float[n];
            for (int i = 0; i < n; i++)
            {
                need[i] = Height(blocks[i], w, true, keys);
            }
            int foot = n > 0 && blocks[n - 1].Kind == VentBlockKind.Actions ? n - 1 : -1;
            for (int i = 0; i < n; i++)
            {
                float reserve = 0f;
                for (int k = i + 1; k < n; k++)
                {
                    reserve += need[k];
                }
                if (i == foot)
                {
                    Block(s, at, blocks[i], x, Math.Max(y, bottom - need[i]), w, bottom, keys);
                    break;
                }
                float room = bottom - reserve;
                if (y + need[i] > room + 0.5f)
                {
                    continue;
                }
                y = Block(s, at, blocks[i], x, y, w, room, keys);
            }
        }

        // What a block takes at a width: its natural height, or, for a table and a chart that give way, the least they are worth.
        private static float Height(VentBlock b, float w, bool least, IUiKeys keys)
        {
            switch (b.Kind)
            {
                case VentBlockKind.Section: return UiTokens.S2 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
                case VentBlockKind.Row:
                case VentBlockKind.Check:
                case VentBlockKind.Meter:
                    return UiTokens.PairRow;
                case VentBlockKind.Note:
                    return Math.Max(UiTokens.Icon, UiDraw.Snap(UiTextRules.WrappedHeight(b.Text, UiStyle.Small, w - UiTokens.Icon - UiTokens.S2, 4))) + UiTokens.S3;
                case VentBlockKind.Figures:
                    return UiTokens.S3 * 2f + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S1 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display)) + UiTokens.S5;
                case VentBlockKind.Chart:
                    return (least ? MinChart : b.ChartHeight) + UiTokens.S4;
                case VentBlockKind.Table:
                {
                    float row = UiDataTable.RowHeight(b.Table);
                    int rows = b.Table.RowCount == 0 ? EmptyRows : least ? Math.Min(b.Table.RowCount, MinRows) : b.Table.RowCount;
                    return UiDraw.Snap(UiDataTable.Chrome(b.Table) + rows * row) + UiTokens.S4;
                }
                case VentBlockKind.Items:
                    return b.Items.Length == 0 ? (string.IsNullOrEmpty(b.EmptyTitle) ? 0f : Math.Min(UiDraw.EmptyHeight(w, b.EmptyTitle, b.EmptyBody),
                        UiTokens.S16 * 2f) + UiTokens.S3) : b.Items.Length * ItemRow + UiTokens.S2;
                case VentBlockKind.Switch: return UiTokens.Control;
                case VentBlockKind.Stepper:
                case VentBlockKind.Choice:
                    return UiTokens.Control + UiTokens.S1;
                case VentBlockKind.Actions: return ActionRows(b, w, keys) * (UiTokens.Control + UiTokens.S2) + UiTokens.S1;
                case VentBlockKind.Steps: return UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2 + UiTokens.Segment + UiTokens.S3;
                case VentBlockKind.Search: return UiTokens.Control + UiTokens.S2;
                case VentBlockKind.Link: return UiTokens.Control;
            }
            return UiTokens.PairRow;
        }

        // One block; returns the y under it.
        private static float Block(UiScene s, UiAt at, VentBlock b, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float line = UiTokens.PairRow;
            switch (b.Kind)
            {
                case VentBlockKind.Section:
                    UiDraw.Text(s, at, x, y + UiTokens.S2, w, UiStyle.SectionHeader, UiRole.Text, b.Text, UiFlags.Label | UiFlags.Truncate);
                    return y + UiTokens.S2 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
                case VentBlockKind.Row:
                    return y + UiDraw.InfoRow(s, at, x, y, w, b.Text, b.Value, false, -1, 0, b.Tone == UiRole.None ? UiRole.Text : b.Tone);
                case VentBlockKind.Note:
                {
                    bool warns = b.Tone == UiRole.Warn || b.Tone == UiRole.WarnText || b.Tone == UiRole.Danger || b.Tone == UiRole.DangerText;
                    float tw = w - UiTokens.Icon - UiTokens.S2;
                    float h = UiDraw.Snap(UiTextRules.WrappedHeight(b.Text, UiStyle.Small, tw, 4));
                    UiDraw.Icon(s, at, x, y + UiTokens.S1, UiTokens.Icon, warns ? UiIcon.Warning : UiIcon.Info, warns ? UiRole.Warn : UiRole.None, UiFlags.Middle, true);
                    UiDraw.Text(s, at, x + UiTokens.Icon + UiTokens.S2, y + UiTokens.S1, tw, UiStyle.Small, UiRole.Text2, b.Text, UiFlags.Label | UiFlags.Wrap, 4);
                    return y + Math.Max(UiTokens.Icon, h) + UiTokens.S3;
                }
                case VentBlockKind.Check:
                    return y + UiDraw.CheckLine(s, at, x, y, w, b.Text, b.Met, b.Pending);
                case VentBlockKind.Figures:
                {
                    UiFigureModel[] figs = b.Figs;
                    return UiAppWindow.Figures(s, at, figs, x, y, w);
                }
                case VentBlockKind.Chart:
                {
                    float ch = Math.Max(MinChart, Math.Min(b.ChartHeight, bottom - y - UiTokens.S4));
                    UiChart.Lay(s, at, b.Chart, x, y, w, ch);
                    return y + ch + UiTokens.S4;
                }
                case VentBlockKind.Table:
                {
                    float natural = UiDraw.Snap(UiDataTable.Chrome(b.Table) + (b.Table.RowCount == 0 ? EmptyRows : b.Table.RowCount) * UiDataTable.RowHeight(b.Table));
                    float th = Math.Min(natural, Math.Max(UiDraw.Snap(UiDataTable.Chrome(b.Table) + (b.Table.RowCount == 0 ? EmptyRows : Math.Min(b.Table.RowCount,
                        MinRows)) * UiDataTable.RowHeight(b.Table)), bottom - y - UiTokens.S4));
                    b.List = UiDataTable.Lay(s, at, b.Table, x, y, w, th, keys, b.Base);
                    b.Clip = b.List >= 0 && b.List < s.ListCount ? s.Lists[b.List].Clip : (short)-1;
                    return y + th + UiTokens.S4;
                }
                case VentBlockKind.Items:
                    return ItemsBlock(s, at, b, x, y, w, bottom);
                case VentBlockKind.Switch:
                {
                    float h = UiTokens.Control;
                    float sw = Math.Min(w * 0.4f, UiTokens.S16 * 2f);
                    UiDraw.TextMiddle(s, at, x, y, h, w - sw - UiTokens.S3, UiStyle.Body, UiRole.Text2, b.Text, UiFlags.Label | UiFlags.Truncate);
                    UiDraw.Switch(s, at, x + w - sw, y, sw, b.On, b.Hint, LinkCommand, b.Run, b.Disabled);
                    return y + h;
                }
                case VentBlockKind.Stepper:
                {
                    float h = UiTokens.Control;
                    float fw = Math.Min(UiDataTable.StepperWidth, UiDraw.SnapDown(w * 0.5f));
                    UiDraw.TextMiddle(s, at, x, y, h, w - fw - UiTokens.S3, UiStyle.Body, UiRole.Text2, b.Text, UiFlags.Label | UiFlags.Truncate);
                    UiDraw.Stepper(s, at, x + w - fw, y, fw, b.Value, b.CanLess, b.CanMore, StepLess, StepType, StepMore, b.Run, b.Disabled, b.Hint);
                    return y + h + UiTokens.S1;
                }
                case VentBlockKind.Choice:
                {
                    float h = UiTokens.Control;
                    float fw = Math.Min(UiDataTable.StepperWidth + UiTokens.S8, UiDraw.SnapDown(w * 0.55f));
                    UiDraw.TextMiddle(s, at, x, y, h, w - fw - UiTokens.S3, UiStyle.Body, UiRole.Text2, b.Text, UiFlags.Label | UiFlags.Truncate);
                    UiDraw.Choice(s, at, x + w - fw, y, fw, b.Value, LinkCommand, b.Run, b.Disabled);
                    return y + h + UiTokens.S1;
                }
                case VentBlockKind.Actions:
                {
                    // The buttons end at the right, the main one last; those that do not fit the width go to a row above, so none stands over its neighbour.
                    int rows = ActionRows(b, w, keys);
                    float h = UiTokens.Control, right = x + w, rowY = y + (rows - 1) * (h + UiTokens.S2);
                    for (int i = b.Actions.Length - 1; i >= 0; i--)
                    {
                        ref UiActionModel a = ref b.Actions[i];
                        float bw = Math.Min(w, UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys));
                        if (right - bw < x - 0.5f)
                        {
                            right = x + w;
                            rowY -= h + UiTokens.S2;
                        }
                        right -= bw;
                        UiDraw.Button(s, at, right, rowY, bw, h, a.Kind, a.Label, a.Icon, a.Key, keys, a.Command != 0 ? a.Command : ActionCommand, a.Arg, a.Disabled);
                        right -= UiTokens.S3;
                    }
                    return y + rows * (h + UiTokens.S2) + UiTokens.S1;
                }
                case VentBlockKind.Meter:
                {
                    float lw = UiDraw.LabelColumn(w, b.Text);
                    UiDraw.GradeBar(s, at, x, y, w, b.Text, b.Grade, lw, null, b.Value);
                    return y + UiTokens.PairRow;
                }
                case VentBlockKind.Steps:
                    return y + UiDraw.Steps(s, at, x, y, w, b.Text, Math.Max(1, b.Count), b.Reached - 1) + UiTokens.S3;
                case VentBlockKind.Search:
                    UiDraw.Search(s, at, x, y, w, b.Hint, b.Text, b.Value, StepType, SearchArg);
                    return y + UiTokens.Control + UiTokens.S2;
                case VentBlockKind.Link:
                    return y + UiDraw.LinkRow(s, at, x, y, w, UiTokens.Control, b.Text, LinkCommand, b.Run);
            }
            return y + line;
        }

        // How many rows a block of buttons takes at a width.
        private static int ActionRows(VentBlock b, float w, IUiKeys keys)
        {
            int rows = 1;
            float right = w;
            for (int i = b.Actions.Length - 1; i >= 0; i--)
            {
                float bw = Math.Min(w, UiDraw.ButtonWidth(b.Actions[i].Kind, b.Actions[i].Label, b.Actions[i].Icon, b.Actions[i].Key, keys));
                if (right - bw < -0.5f)
                {
                    rows++;
                    right = w;
                }
                right -= bw + UiTokens.S3;
            }
            return rows;
        }

        // Short rows: a glyph on its tile or a face, a title and a line under it, a chip, an amount at the right. A row with a run is a target.
        private static float ItemsBlock(UiScene s, UiAt at, VentBlock b, float x, float y, float w, float bottom)
        {
            if (b.Items.Length == 0)
            {
                if (string.IsNullOrEmpty(b.EmptyTitle))
                {
                    return y;
                }
                float eh = Math.Min(UiDraw.EmptyHeight(w, b.EmptyTitle, b.EmptyBody), Math.Max(UiTokens.S16, bottom - y));
                UiDraw.Empty(s, at, x, y, w, eh, b.EmptyTitle, b.EmptyBody);
                return y + eh + UiTokens.S3;
            }
            float cy = y;
            for (int i = 0; i < b.Items.Length && cy + ItemRow <= bottom + 0.5f; i++)
            {
                ref VentItem it = ref b.Items[i];
                int face = UiDraw.Box(s, at, x - UiTokens.S2, cy, w + UiTokens.S4, ItemRow - UiTokens.S1, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
                UiAt inside = at.In(face);
                RowFace(s, inside, ref it, x, cy, w);
                if (it.Run >= 0)
                {
                    UiDraw.Node(s, at, x - UiTokens.S2, cy, w + UiTokens.S4, ItemRow - UiTokens.S1, ItemCommand, it.Run, face);
                }
                cy += ItemRow;
            }
            return cy + UiTokens.S2;
        }

        private static void RowFace(UiScene s, UiAt inside, ref VentItem it, float x, float y, float w)
        {
            float size = UiTokens.IconTile, h = ItemRow - UiTokens.S1;
            float ty = y + UiDraw.SnapDown((h - size) / 2f);
            if (it.Face != null)
            {
                UiDraw.Portrait(s, inside, x + UiTokens.S1, ty + UiTokens.S1, size - UiTokens.S2, it.Face, it.Hue, it.Ring);
            }
            else
            {
                int t = UiDraw.Tile(s, inside, x, ty, size, it.Tint, false);
                float g = UiTokens.S6;
                UiDraw.Icon(s, inside.In(t), x + (size - g) / 2f, ty + (size - g) / 2f, g, it.Icon == UiIcon.None ? UiIcon.Store : it.Icon, it.Tint, UiFlags.Middle | UiFlags.Centre, true);
            }
            float right = x + w;
            float aw = string.IsNullOrEmpty(it.Amount) ? 0f : Math.Min(UiDraw.SnapDown(w * 0.34f), UiDraw.Snap(UiTextRules.Width(it.Amount, UiStyle.BodyStrong) + UiTokens.S1));
            float cw = string.IsNullOrEmpty(it.Chip) ? 0f : UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(it.Chip, UiStyle.SmallStrong) + UiTokens.S1);
            float lx = x + size + UiTokens.S3;
            float lw = Math.Max(0f, right - aw - (aw > 0f ? UiTokens.S3 : 0f) - lx);
            float th = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)), sh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            float top = y + UiDraw.SnapDown((h - th - (string.IsNullOrEmpty(it.Sub) ? 0f : UiTokens.S1 + sh)) / 2f);
            float chipRoom = cw > 0f ? Math.Min(cw, UiDraw.SnapDown(lw * 0.45f)) : 0f;
            float nameW = lw - (chipRoom > 0f ? chipRoom + UiTokens.S3 : 0f);
            float nameFull = UiDraw.Snap(UiTextRules.Width(it.Title ?? string.Empty, UiStyle.Title) + UiTokens.S1);
            nameW = Math.Min(nameW, nameFull);
            UiDraw.TextMiddle(s, inside, lx, top, th, nameW, UiStyle.Title, UiRole.Text, it.Title ?? string.Empty, UiFlags.Truncate);
            if (chipRoom > 0f)
            {
                UiDraw.Chip(s, inside, lx + nameW + UiTokens.S3, top + UiDraw.SnapDown((th - UiTokens.Badge - UiTokens.S1) / 2f), it.Chip, it.ChipTone, chipRoom);
            }
            if (!string.IsNullOrEmpty(it.Sub))
            {
                UiDraw.TextMiddle(s, inside, lx, top + th + UiTokens.S1, sh, lw, UiStyle.Small, UiRole.Text2, it.Sub, UiFlags.Truncate);
            }
            if (aw > 0f)
            {
                UiDraw.TextMiddle(s, inside, right - aw, y, h, aw, UiStyle.BodyStrong, it.AmountInk == UiRole.None ? UiRole.Text : it.AmountInk, it.Amount, UiFlags.Right | UiFlags.Truncate);
            }
        }
    }
}
