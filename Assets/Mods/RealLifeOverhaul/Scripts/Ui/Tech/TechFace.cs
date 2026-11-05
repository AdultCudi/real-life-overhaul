using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Tech app on the kit: an app window with its sections down a sidebar and a page of the model's shape (a tab's table, a firm's overview as the approved
    /// studio window, one thing's page of cards, a game's page). Game-free; the model says what, this says where. Cost: a layout when the model changes.
    /// </summary>
    public static partial class TechFace
    {
        // The block 8400 to 8419 is this screen's: the kit's own commands repeat across parts (a kit nav is 1, a decision's choice is 1), so the screen tests the page's shape first.
        public const int ActionCommand = 8400, StepLess = 8401, StepMore = 8402, BackCommand = 8403, QuoteCommand = 8404;
        public const int TableBase = UiDataTable.First;
        private const float HeadHeight = 64f, TileBig = 56f, SideWidth = 340f, QuoteRow = 84f, LeastChart = 168f;
        private const byte RegionTable = 1, RegionLeft = 2, RegionRight = 3, RegionHead = 4, RegionChart = 5, RegionQuotes = 6;

        /// <summary>Every command the screen answers, for the checks.</summary>
        public static readonly int[] Commands = AllCommands();

        private static int[] AllCommands()
        {
            var list = new System.Collections.Generic.List<int>
            {
                UiAppWindow.NavCommand, UiAppWindow.RowCommand, UiAppWindow.PersonCommand, UiAppWindow.ActionCommand, UiAppWindow.MoreCommand, UiAppWindow.AllCommand,
                UiAppWindow.NoteCommand, ActionCommand, StepLess, StepMore, BackCommand, QuoteCommand, UiTrack.First + UiTrack.Step, UiChart.First + UiChart.Point
            };
            for (int i = 0; i <= UiDataTable.MassAnswer; i++)
            {
                list.Add(TableBase + i);
            }
            return list.ToArray();
        }

        public static void Lay(UiScene s, TechModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("tech", frame.Width, frame.Height, dark);
            UiAt at = UiAppWindow.Shell(s, frame, m.Title, m.Icon, m.Nav, m.NavSelected, false, keys, out float x, out float y, out float w, out float bottom);
            switch (m.Shape)
            {
                case TechShape.Table:
                    TablePage(s, at, m, x, y, w, bottom, keys);
                    break;
                case TechShape.Overview:
                    OverviewPage(s, at, m, x, y, w, bottom, keys);
                    break;
                case TechShape.Sheet:
                    SheetPage(s, at, m, x, y, w, bottom, keys);
                    break;
                case TechShape.Game:
                    GamePage(s, at, m, x, y, w, bottom, keys);
                    break;
                default:
                    int leave = UiDraw.EmptyState(s, at, x, y, w, bottom - y, UiIcon.Monitor, m.OffTitle, m.OffBody, m.OffAction, UiDraw.CloseCommand, keys);
                    if (leave >= 0)
                    {
                        s.FocusFirst = leave;
                    }
                    break;
            }
            for (int i = 0; i < m.Prompts.Length; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        // ------------------------------------------------------------------------------------------------- a tab's table

        private static void TablePage(UiScene s, UiAt at, TechModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            y = Lead(s, at.InRegion(RegionHead), m, x, y, w, keys);
            y = UiAppWindow.Figures(s, at, m.Figures, x, y, w);
            bool side = m.Side != null;
            float sw = side ? Math.Min(SideWidth, UiDraw.SnapDown(w * 0.34f)) : 0f;
            // A side card stands only where the table keeps its columns: a narrow window or a long translation gives the table the whole width instead.
            if (side && Shortfall(m.Table, w - sw - UiTokens.S4) > 0f)
            {
                side = false;
                sw = 0f;
            }
            float tw = w - (sw > 0f ? sw + UiTokens.S4 : 0f);
            float room = bottom - y;
            float natural = Natural(m.Table);
            float sideH = side ? UiFactCard.Height(m.Side, sw, keys, true) : 0f;
            bool chart = m.Chart != null && room - Math.Max(natural, sideH) - UiTokens.S4 >= LeastChart;
            float th = chart ? Math.Max(UiTokens.S16, Math.Max(natural, sideH)) : room;
            float[] widths = Fit(m.Table, tw);
            m.TableList = UiDataTable.Lay(s, at.InRegion(RegionTable), m.Table, x, y, tw, th, keys, TableBase);
            Unfit(m.Table, widths);
            m.TableClip = ClipOf(s, m.TableList);
            if (side)
            {
                UiFactCard.Lay(s, at.InRegion(RegionRight), m.Side, x + tw + UiTokens.S4, y, sw, th, keys, true, !chart);
            }
            if (chart)
            {
                float cy = UiDraw.Snap(y + th + UiTokens.S4);
                UiChart.Lay(s, at.InRegion(RegionChart), m.Chart, x, cy, w, UiDraw.SnapDown(bottom - cy), UiChart.First);
            }
        }

        // What a table's columns ask of its width: each fixed column at the wider of its own width and its head's words, the gaps, and the least its one wide column keeps.
        private static float Needs(UiDataTableModel t, out float fixedSum)
        {
            fixedSum = 0f;
            if (t == null || t.Columns == null)
            {
                return 0f;
            }
            for (int c = 0; c < t.Columns.Length; c++)
            {
                if (t.Columns[c].Width > 0f)
                {
                    fixedSum += Math.Max(t.Columns[c].Width, UiDraw.Snap(UiTextRules.Width(t.Columns[c].Title ?? string.Empty, UiStyle.SectionHeader) + UiTokens.S1));
                }
            }
            return fixedSum + UiTokens.S4 * t.Columns.Length + UiTokens.S16 * 3f;
        }

        /// <summary>How many pixels a table lacks at a width to hold its columns whole (0 when it fits).</summary>
        private static float Shortfall(UiDataTableModel t, float w)
        {
            return Math.Max(0f, Needs(t, out _) - w);
        }

        // A table too wide for its room takes its fixed columns in to what their heads need, in proportion: the one wide column keeps room for its names. Returns the widths to put back.
        private static float[] Fit(UiDataTableModel t, float w)
        {
            float needs = Needs(t, out float fixedSum);
            if (t == null || needs <= w || fixedSum <= 0f)
            {
                return null;
            }
            float available = w - UiTokens.S4 * t.Columns.Length - UiTokens.S16 * 3f;
            var saved = new float[t.Columns.Length];
            for (int c = 0; c < saved.Length; c++)
            {
                saved[c] = t.Columns[c].Width;
                if (t.Columns[c].Width > 0f)
                {
                    float head = Up(UiTextRules.Width(t.Columns[c].Title ?? string.Empty, UiStyle.SectionHeader) + UiTokens.S1);
                    t.Columns[c].Width = Math.Max(head, Down(t.Columns[c].Width * Math.Max(0f, available) / fixedSum));
                }
            }
            return saved;
        }

        // A width on the 4 px grid, rounded up or down.
        private static float Up(float v) => (float)Math.Ceiling(v / 4f) * 4f;

        private static float Down(float v) => (float)Math.Floor(v / 4f) * 4f;

        private static void Unfit(UiDataTableModel t, float[] widths)
        {
            for (int c = 0; widths != null && c < widths.Length; c++)
            {
                t.Columns[c].Width = widths[c];
            }
        }

        private static short ClipOf(UiScene s, short list) => list >= 0 && list < s.ListCount ? s.Lists[list].Clip : (short)-1;

        private static float Natural(UiDataTableModel t) => t == null ? 0f : UiDraw.Snap(UiDataTable.Chrome(t) + t.RowCount * UiDataTable.RowHeight(t));

        // What to do next at the left and the actions at the right; a sub page's way back stands before them. Returns the y under it.
        private static float Lead(UiScene s, UiAt at, TechModel m, float x, float y, float w, IUiKeys keys)
        {
            if (!string.IsNullOrEmpty(m.Back))
            {
                float h = UiTokens.Control;
                float bw = UiDraw.ButtonWidth(UiButtonKind.Secondary, m.Back, UiIcon.Back, UiAction.Count, keys);
                UiDraw.Button(s, at, x, y, bw, h, UiButtonKind.Secondary, m.Back, UiIcon.Back, UiAction.Count, keys, BackCommand, 0, false);
                float rightEdge = x + w;
                for (int i = m.Actions.Length - 1; i >= 0; i--)
                {
                    ref UiActionModel a = ref m.Actions[i];
                    float abw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                    rightEdge -= abw;
                    UiDraw.Button(s, at, rightEdge, y, abw, h, a.Kind, a.Label, a.Icon, a.Key, keys, UiAppWindow.ActionCommand, i, a.Disabled);
                    rightEdge -= UiTokens.S3;
                }
                if (!string.IsNullOrEmpty(m.Next))
                {
                    float nx = x + bw + UiTokens.S4;
                    UiDraw.TextMiddle(s, at, nx, y, h, rightEdge - UiTokens.S3 - nx, UiStyle.BodyStrong, UiRole.Text, m.Next, UiFlags.Label | UiFlags.Truncate);
                }
                return y + h + UiTokens.S4;
            }
            return UiAppWindow.Lead(s, at, m.Next, m.Actions, x, y, w, keys);
        }

        // ------------------------------------------------------------------------------------------------- a firm's overview

        private static void OverviewPage(UiScene s, UiAt at, TechModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            UiAppModel a = m.App;
            if (a == null)
            {
                return;
            }
            y = UiAppWindow.Lead(s, at.InRegion(RegionHead), m.Next, m.Actions, x, y, w, keys);
            if (a.Hero != null)
            {
                float fw = a.Figures.Length > 0 ? UiDraw.SnapDown(w * 0.32f) : 0f;
                float hh = UiAppWindow.Hero(s, at, a.Hero, x, y, fw > 0f ? w - fw - UiTokens.S4 : w);
                if (fw > 0f)
                {
                    UiAppWindow.Figures(s, at, a.Figures, x + w - fw, y, fw, hh);
                }
                y += hh + UiTokens.S5;
            }
            else
            {
                y = UiAppWindow.Figures(s, at, a.Figures, x, y, w);
            }
            y = MetersBand(s, at, m, x, y, w);
            float pw = a.People.Length > 0 || !string.IsNullOrEmpty(a.Note) ? UiDraw.SnapDown(w * 0.4f) : 0f;
            float tw = pw > 0f ? w - pw - UiTokens.S6 : w;
            float under = a.Columns.Length > 0 ? UiAppWindow.Table(s, at.InRegion(RegionTable), a.TableTitle, a.Columns, a.GradeColumn, a.Rows, UiAppWindow.RowCommand, x, y, tw, bottom - y) : y;
            if (a.MoreRows.Length > 0 && bottom - under > UiTokens.S16 * 2f)
            {
                UiAppWindow.Table(s, at.InRegion(RegionLeft), a.MoreTitle, a.MoreColumns, a.MoreGrade, a.MoreRows, UiAppWindow.MoreCommand, x, under + UiTokens.S6, tw,
                    bottom - under - UiTokens.S6);
            }
            if (pw > 0f)
            {
                UiAppWindow.People(s, at.InRegion(RegionRight), a.PeopleTitle, a.PeopleCount, a.People, a.PeopleMore, a.Note, a.NoteTone, a.NoteAction, x + tw + UiTokens.S6, y, pw,
                    bottom - y);
            }
        }

        // The hall's supplies on a band: each its label, its bar filled to what it uses (green, then yellow near full, red at it) and its figure, two to a row.
        private static float MetersBand(UiScene s, UiAt at, TechModel m, float x, float y, float w)
        {
            if (m.Meters.Length == 0)
            {
                return y;
            }
            int rows = (m.Meters.Length + 1) / 2;
            float title = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3;
            float rowH = UiTokens.PairRow + UiTokens.S1;
            float h = UiTokens.S4 * 2f + title + rows * rowH;
            UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            float ix = x + UiTokens.S4, iw = w - UiTokens.S4 * 2f;
            UiDraw.Text(s, at, ix, y + UiTokens.S4, iw, UiStyle.SectionHeader, UiRole.Text2, m.MetersTitle, UiFlags.Label | UiFlags.Truncate);
            float colW = UiDraw.SnapDown((iw - UiTokens.S8) / 2f), top = y + UiTokens.S4 + title;
            for (int i = 0; i < m.Meters.Length; i++)
            {
                TechMeter t = m.Meters[i];
                float cx = ix + (i % 2) * (colW + UiTokens.S8), cy = top + (i / 2) * rowH;
                float label = UiDraw.SnapDown(colW * 0.2f), figure = UiDraw.SnapDown(colW * 0.3f);
                UiDraw.TextMiddle(s, at, cx, cy, UiTokens.PairRow, label, UiStyle.Body, UiRole.Text2, t.Label, UiFlags.Truncate);
                UiDraw.Meter(s, at, cx + label + UiTokens.S2, cy + UiDraw.SnapDown((UiTokens.PairRow - UiTokens.S2) / 2f), colW - label - figure - UiTokens.S4, UiTokens.S2, t.Fill, t.Tone);
                UiDraw.TextMiddle(s, at, cx + colW - figure, cy, UiTokens.PairRow, figure, UiStyle.BodyStrong, UiRole.Text, t.Figure, UiFlags.Right | UiFlags.Truncate);
            }
            return y + h + UiTokens.S4;
        }

        // ------------------------------------------------------------------------------------------------- one thing's page

        private static void SheetPage(UiScene s, UiAt at, TechModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            UiAt head = at.InRegion(RegionHead);
            y = TopBar(s, head, m.Back, m.HeadActions, x, y, w, keys);
            UiHeadModel h = m.Head;
            if (h != null)
            {
                y = SheetHead(s, head, h, x, y, w);
            }
            y = UiAppWindow.Figures(s, at, m.Figures, x, y, w);
            bool both = m.Left.Length > 0 && m.Right.Length > 0;
            float cw = both ? UiDraw.SnapDown((w - UiTokens.S4) / 2f) : UiDraw.SnapDown(Math.Min(w, UiTokens.S16 * 40f));
            float rx = x + cw + UiTokens.S4, rw = w - cw - UiTokens.S4;
            UiFactCardModel[] leftCards = m.Left.Length > 0 ? m.Left : m.Right, rightCards = both ? m.Right : new UiFactCardModel[0];
            float leftH = Stack(leftCards, cw, keys), rightH = Stack(rightCards, rw, keys);
            float tableH = m.Table != null ? Natural(m.Table) : 0f;
            float columnsH = Math.Max(leftH, rightH);
            float colBottom = bottom;
            if (m.Table != null && m.Table.RowCount > 0)
            {
                // The table keeps its rows' room under the cards, and the cards give way before the table does.
                float tableRoom = Math.Min(tableH, UiTokens.S16 * 4f);
                colBottom = Math.Max(y + UiTokens.S16 * 3f, bottom - tableRoom - UiTokens.S4);
            }
            Column(s, at.InRegion(RegionLeft), leftCards, x, y, cw, colBottom, keys);
            if (rightCards.Length > 0)
            {
                Column(s, at.InRegion(RegionRight), rightCards, rx, y, rw, colBottom, keys);
            }
            float under = y + Math.Min(columnsH, colBottom - y) + UiTokens.S4;
            if (m.Table != null)
            {
                float th = bottom - under;
                if (th > UiTokens.S16 * 2f)
                {
                    float[] widths = Fit(m.Table, w);
                    m.TableList = UiDataTable.Lay(s, at.InRegion(RegionTable), m.Table, x, under, w, th, keys, TableBase);
                    Unfit(m.Table, widths);
                    m.TableClip = ClipOf(s, m.TableList);
                    under += Math.Min(th, tableH) + UiTokens.S4;
                }
            }
            if (m.Chart != null && bottom - under >= LeastChart)
            {
                UiChart.Lay(s, at.InRegion(RegionChart), m.Chart, x, UiDraw.Snap(under), w, UiDraw.SnapDown(bottom - under), UiChart.First);
            }
        }

        // The thing's picture on its tile, its name with its state chip and a line under it.
        private static float SheetHead(UiScene s, UiAt at, UiHeadModel h, float x, float y, float w)
        {
            int tile = UiDraw.Tile(s, at, x, y, TileBig, h.Tint, false);
            UiDraw.Icon(s, at.In(tile), x + (TileBig - UiTokens.S8) / 2f, y + (TileBig - UiTokens.S8) / 2f, UiTokens.S8, h.Icon, h.Tint, UiFlags.Middle | UiFlags.Centre, true);
            float tx = x + TileBig + UiTokens.S4;
            float chip = string.IsNullOrEmpty(h.Status) ? 0f : UiFactCard.ChipWidth(h.Status);
            float nh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display));
            float nameW = Math.Min(x + w - tx - (chip > 0f ? chip + UiTokens.S3 : 0f), UiDraw.Snap(UiTextRules.Width(h.Name ?? string.Empty, UiStyle.Display) + UiTokens.S1));
            UiDraw.Text(s, at, tx, y + UiTokens.S1, nameW, UiStyle.Display, UiRole.Text, h.Name, UiFlags.Truncate | UiFlags.Middle);
            if (chip > 0f)
            {
                UiDraw.Chip(s, at, tx + nameW + UiTokens.S3, y + UiTokens.S1 + UiDraw.SnapDown((nh - UiTokens.Badge - UiTokens.S1) / 2f), h.Status, h.StatusTone, chip);
            }
            UiDraw.Text(s, at, tx, y + UiTokens.S2 + nh + UiTokens.S1, x + w - tx, UiStyle.Body, UiRole.Text2, h.Role, UiFlags.Truncate | UiFlags.Middle);
            return y + TileBig + UiTokens.S4;
        }

        // The way back at the left, the page's actions at the right (the main one last). Returns the y under them.
        private static float TopBar(UiScene s, UiAt at, string back, UiActionModel[] actions, float x, float y, float w, IUiKeys keys)
        {
            if (string.IsNullOrEmpty(back) && actions.Length == 0)
            {
                return y;
            }
            float h = UiTokens.Control;
            float right = x + w;
            for (int i = actions.Length - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref actions[i];
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                right -= bw;
                UiDraw.Button(s, at, right, y, bw, h, a.Kind, a.Label, a.Icon, a.Key, keys, UiAppWindow.ActionCommand, i, a.Disabled);
                right -= UiTokens.S3;
            }
            if (!string.IsNullOrEmpty(back))
            {
                float bw = UiDraw.ButtonWidth(UiButtonKind.Secondary, back, UiIcon.Back, UiAction.Count, keys);
                UiDraw.Button(s, at, x, y, bw, h, UiButtonKind.Secondary, back, UiIcon.Back, UiAction.Count, keys, BackCommand, 0, false);
            }
            return y + h + UiTokens.S4;
        }

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

        // ------------------------------------------------------------------------------------------------- a game's page

        private static void GamePage(UiScene s, UiAt at, TechModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            y = Lead(s, at.InRegion(RegionHead), m, x, y, w, keys);
            if (m.Hero != null)
            {
                float fw = m.Figures.Length > 0 ? UiDraw.SnapDown(w * 0.34f) : 0f;
                float hh = UiAppWindow.Hero(s, at, m.Hero, x, y, fw > 0f ? w - fw - UiTokens.S4 : w);
                if (fw > 0f)
                {
                    UiAppWindow.Figures(s, at, m.Figures, x + w - fw, y, fw, hh);
                }
                y += hh + UiTokens.S4;
            }
            if (m.Track != null && m.Track.Steps.Length > 0)
            {
                float th = UiTrack.Height(m.Track);
                UiDraw.Box(s, at, x, y, w, th + UiTokens.S6, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
                UiTrack.Lay(s, at, m.Track, x + UiTokens.S4, y + UiTokens.S3, w - UiTokens.S4 * 2f, UiTrack.First);
                y += th + UiTokens.S6 + UiTokens.S4;
            }
            float qw = UiDraw.SnapDown((w - UiTokens.S6) * 0.42f);
            float cx = x + qw + UiTokens.S6, cw = w - qw - UiTokens.S6;
            Quotes(s, at.InRegion(RegionQuotes), m, x, y, qw, bottom - y);
            if (m.Chart != null)
            {
                float notes = string.IsNullOrEmpty(m.ChartNotes) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(m.ChartNotes, UiStyle.Small, cw, 2)) + UiTokens.S2;
                UiChart.Lay(s, at.InRegion(RegionChart), m.Chart, cx, y, cw, UiDraw.SnapDown(bottom - y - notes), UiChart.First);
                if (notes > 0f)
                {
                    UiDraw.Text(s, at, cx, bottom - notes + UiTokens.S1, cw, UiStyle.Small, UiRole.Text2, m.ChartNotes, UiFlags.Wrap, 2);
                }
            }
        }

        // The reviews: the average over them, then each outlet in its round badge with its day, the score at the right in its grade and the line it wrote under.
        private static void Quotes(UiScene s, UiAt at, TechModel m, float x, float y, float w, float h)
        {
            float ty = y;
            UiDraw.Text(s, at, x, ty, w - UiTokens.S16, UiStyle.SectionHeader, UiRole.Text, m.QuotesTitle, UiFlags.Label | UiFlags.Truncate);
            if (!string.IsNullOrEmpty(m.QuotesCount))
            {
                UiDraw.Text(s, at, x + w - UiTokens.S16, ty, UiTokens.S16, UiStyle.Small, UiRole.Muted, m.QuotesCount, UiFlags.Right | UiFlags.Truncate);
            }
            ty += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3;
            if (m.Quotes.Length == 0)
            {
                UiDraw.Text(s, at, x, ty, w, UiStyle.Body, UiRole.Text2, m.QuotesEmpty, UiFlags.Wrap, 4);
                return;
            }
            if (!string.IsNullOrEmpty(m.QuotesAverage))
            {
                float aw = UiDraw.Snap(UiTextRules.Width(m.QuotesAverage, UiStyle.Display) + UiTokens.S2);
                int grade;
                int.TryParse(m.QuotesAverage, out grade);
                UiDraw.Text(s, at, x, ty, aw, UiStyle.Display, UiTokens.Grade(grade), m.QuotesAverage, UiFlags.Truncate);
                UiDraw.TextMiddle(s, at, x + aw + UiTokens.S2, ty, UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display)), w - aw - UiTokens.S2, UiStyle.Body, UiRole.Text2, m.QuotesAverageLabel,
                    UiFlags.Truncate);
                ty += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display)) + UiTokens.S3;
            }
            const float face = UiTokens.ControlSmall;
            for (int i = 0; i < m.Quotes.Length && ty + QuoteRow <= y + h; i++)
            {
                TechQuote q = m.Quotes[i];
                int row = UiDraw.Box(s, at, x - UiTokens.S2, ty, w + UiTokens.S4, QuoteRow - UiTokens.S1, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
                UiAt inside = at.In(row);
                UiDraw.Portrait(s, inside, x, ty + UiTokens.S2, face, q.Outlet, UiColour.HueOf(q.Outlet), UiRole.None);
                float tx = x + face + UiTokens.S3;
                float scoreW = UiDraw.Snap(UiTextRules.Width(q.Score.ToString(), UiStyle.Title) + UiTokens.S2);
                float nameW = UiDraw.Snap(UiTextRules.Width(q.Outlet, UiStyle.BodyStrong) + UiTokens.S1);
                float lineH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.BodyStrong));
                UiDraw.TextMiddle(s, inside, tx, ty + UiTokens.S2, face, Math.Min(nameW, w - face - scoreW - UiTokens.S10), UiStyle.BodyStrong, UiRole.Text, q.Outlet, UiFlags.Truncate);
                float dx = tx + Math.Min(nameW, w - face - scoreW - UiTokens.S10) + UiTokens.S2;
                UiDraw.TextMiddle(s, inside, dx, ty + UiTokens.S2, face, x + w - scoreW - dx, UiStyle.Small, UiRole.Muted, q.Day, UiFlags.Truncate);
                UiDraw.TextMiddle(s, inside, x + w - scoreW, ty + UiTokens.S2, face, scoreW, UiStyle.Title, UiTokens.Grade(q.Score), q.Score.ToString(), UiFlags.Right);
                UiDraw.Text(s, inside, tx, ty + UiTokens.S2 + face + UiTokens.S1 - lineH + lineH, x + w - tx, UiStyle.Body, UiRole.Text2, q.Line, UiFlags.Wrap, 2);
                UiDraw.Node(s, at, x - UiTokens.S2, ty, w + UiTokens.S4, QuoteRow - UiTokens.S1, QuoteCommand, i, row);
                ty += QuoteRow;
            }
        }
    }
}
