using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a line of the controls page holds: a choice, a slider, an amount, a switch, a requirement, words or a key.</summary>
    public enum UiPartsField : byte
    {
        Choice = 0,
        Slider,
        Amount,
        Switch,
        Check,
        Text,
        Search,
        Key
    }

    /// <summary>A line of the controls page: its words at the left and its control at the right, in the state it is in.</summary>
    public struct UiPartsRow
    {
        public UiPartsField Kind;
        public string Label;
        public string Value;         // the choice's value, the slider's or the amount's figure, the switch's word, the typed words, the key
        public string Placeholder;   // a text field's words while empty
        public float Share;          // a slider's place between its ends
        public bool On;              // a switch on, a requirement met, a key binding waiting for its key, a field being typed in, a slider held
        public bool CanLess, CanMore;
        public bool Disabled;
    }

    /// <summary>A section of the controls page: its label in capitals and its lines.</summary>
    public struct UiPartsSection
    {
        public string Title;
        public UiPartsRow[] Rows;
    }

    /// <summary>The words and samples of the parts' gallery: a page a part, each with realistic data. The game fills it from the locale, the checks from fixtures.</summary>
    public sealed class UiPartsGalleryModel
    {
        public string Title;
        public UiNavEntry[] Nav = new UiNavEntry[0];
        public int Page;
        public UiPartsSection[] Left = new UiPartsSection[0];    // the controls page's two columns
        public UiPartsSection[] Right = new UiPartsSection[0];
        public int OpenSection = -1, OpenRow = -1;                // the choice whose list is open, or -1
        public string[] Options = new string[0];
        public int OptionChosen;
        public float OptionOffset;
        public UiDataTableModel Table = new UiDataTableModel();
        public string DetailRole;
        public UiIcon DetailRoleIcon = UiIcon.Briefcase;
        public string[] DetailLabels = new string[0];
        public string[] DetailValues = new string[0];
        public string DetailEmptyTitle;
        public string DetailEmptyBody;
        public string[] AttributeLabels = new string[0];
        public int[] AttributeValues = new int[0];
        public UiChartModel Line = new UiChartModel();
        public UiChartModel Bars = new UiChartModel { Kind = UiChartKind.Bars };
        public UiChartModel Candles = new UiChartModel { Kind = UiChartKind.Candles };
        public UiCatalogueModel Catalogue = new UiCatalogueModel();
        public UiStatementModel Statement = new UiStatementModel();
        public UiTrackModel[] Tracks = new UiTrackModel[0];
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
        // Written back by the layout, so the screen finds what a list's row stands for.
        public int CatalogueColumns = 1;
        public int CatalogueList = -1;
        public int TableList = -1;
        public int StatementList = -1;
        public float ControlWidth = 320f;   // the controls page's control column, so a pointer's place on a slider maps to its value
        public float ControlLeft;           // its left edge in the left column, where the sliders stand
    }

    /// <summary>
    /// The gallery of the kit's parts, an app window whose sidebar holds a page a part: the controls in each state (a
    /// choice with its list open, sliders, amounts at their ends, switches, requirements met and not, words typed and
    /// waiting, a key binding asking for its key), the big table of forty staff sorted, filtered and picked with its mass
    /// actions and the row's detail beside it, the charts (a year's line, a bar a district, a month of candles), a
    /// catalogue of goods with one chosen, a statement of a busy week and of a new save, and tracks of steps. It shows each
    /// part as a screen would hold it, so the owner judges the parts from Options, Tests, and the checks lay each out in
    /// every language and frame. Commands: the controls' from <see cref="Choice"/> to <see cref="Key"/>, then each part's
    /// own (UiDataTable, UiChart and its <see cref="BarsBase"/> and <see cref="CandlesBase"/>, UiCatalogue, UiStatement,
    /// UiTrack). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiPartsGallery
    {
        public const int PageControls = 0, PageTable = 1, PageCharts = 2, PageCatalogue = 3, PageStatement = 4, PageTrack = 5, PageCount = 6;
        public const int Choice = 100, Option = 101, Slider = 102, Less = 103, Amount = 104, More = 105, Switch = 106, Field = 107, Key = 108;
        public const int BarsBase = 124, CandlesBase = 126;   // the second and third charts' points, apart from the line's
        private const float Detail = 376f;        // px: the table's detail column
        private const float MostControl = 320f;   // px: a control's widest on the controls page

        /// <summary>Every command the gallery answers.</summary>
        public static readonly int[] Commands =
        {
            UiAppWindow.NavCommand, Choice, Option, Slider, Less, Amount, More, Switch, Field, Key,
            UiDataTable.First + UiDataTable.Head, UiDataTable.First + UiDataTable.Filter, UiDataTable.First + UiDataTable.Search, UiDataTable.First + UiDataTable.Row,
            UiDataTable.First + UiDataTable.Pick, UiDataTable.First + UiDataTable.Mass, UiDataTable.First + UiDataTable.Done, UiDataTable.First + UiDataTable.Clear,
            UiChart.First + UiChart.Point, BarsBase + UiChart.Point, CandlesBase + UiChart.Point,
            UiCatalogue.First + UiCatalogue.Card, UiCatalogue.First + UiCatalogue.Action,
            UiStatement.First + UiStatement.Period, UiStatement.First + UiStatement.Group, UiStatement.First + UiStatement.Line,
            UiTrack.First + UiTrack.Step, UiTrack.First + 1, UiTrack.First + 2
        };

        /// <summary>A controls line's argument: its column, section and row in one figure.</summary>
        public static int Arg(bool right, int section, int row) => (right ? 1000 : 0) + section * 32 + row;

        /// <summary>Lays the gallery's page out into a scene.</summary>
        public static void Lay(UiScene s, UiPartsGalleryModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("kit parts", frame.Width, frame.Height, dark);
            UiAt at = UiAppWindow.Shell(s, frame, m.Title, UiIcon.Grid, m.Nav, m.Page, false, keys, out float x, out float y, out float w, out float bottom);
            float h = bottom - y;
            int page = s.NodeCount;
            switch (m.Page)
            {
                case PageControls: Controls(s, at, m, x, y, w, keys); break;
                case PageTable: TablePage(s, at, m, x, y, w, h, keys); break;
                case PageCharts: Charts(s, at, m, x, y, w, h); break;
                case PageCatalogue:
                    m.CatalogueList = UiCatalogue.Lay(s, at, m.Catalogue, x, y, w, h, keys, out m.CatalogueColumns);
                    break;
                case PageStatement:
                    m.StatementList = UiStatement.Lay(s, at, m.Statement, x, y, w, h, keys);
                    break;
                default: Tracks(s, at, m, x, y, w); break;
            }
            Fence(s, page);
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        // Two columns of sections, each line its words at the left and its control at the right on one rhythm; an open
        // choice's list over the page under its field.
        private static void Controls(UiScene s, UiAt at, UiPartsGalleryModel m, float x, float y, float w, IUiKeys keys)
        {
            float gap = UiTokens.S12;
            float cw = UiDraw.SnapDown((w - gap) / 2f);
            float fx = 0f, fy = 0f, fw = 0f;
            Column(s, at, m, m.Left, false, x, y, cw, ref fx, ref fy, ref fw);
            Column(s, at, m, m.Right, true, x + cw + gap, y, cw, ref fx, ref fy, ref fw);
            if (m.OpenRow >= 0 && fw > 0f)
            {
                UiDraw.ChoiceList(s, at, fx, fy + UiTokens.Control + UiTokens.S1, fw, m.Options, m.Options.Length, m.OptionChosen, m.OptionOffset, Option);
            }
        }

        private static void Column(UiScene s, UiAt at, UiPartsGalleryModel m, UiPartsSection[] sections, bool right, float x, float y, float w,
            ref float openX, ref float openY, ref float openW)
        {
            float cw = UiDraw.SnapDown(Math.Min(MostControl, w * 0.5f));
            m.ControlWidth = cw;
            if (!right)
            {
                m.ControlLeft = x + w - cw;
            }
            float ty = y;
            for (int sec = 0; sec < sections.Length; sec++)
            {
                UiDraw.Text(s, at, x, ty, w, UiStyle.SectionHeader, UiRole.Text, sections[sec].Title, UiFlags.Label | UiFlags.Truncate);
                ty += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
                UiPartsRow[] rows = sections[sec].Rows;
                for (int r = 0; r < rows.Length; r++)
                {
                    ref UiPartsRow row = ref rows[r];
                    int arg = Arg(right, sec, r);
                    if (row.Kind == UiPartsField.Check)
                    {
                        int line = UiDraw.Box(s, at, x, ty, w, UiTokens.PairRow + UiTokens.S2, UiRole.None, UiRole.None, 0f);
                        UiDraw.CheckLine(s, at.In(line), x, ty + UiTokens.S1, w, row.Label, row.On);
                        ty += UiTokens.PairRow + UiTokens.S2;
                        continue;
                    }
                    int box = UiDraw.Box(s, at, x, ty, w, UiTokens.Control, UiRole.None, UiRole.None, 0f);
                    UiAt inRow = at.In(box);
                    float cx = x + w - cw;
                    UiDraw.TextMiddle(s, inRow, x, ty, UiTokens.Control, cx - x - UiTokens.S4, UiStyle.Body, row.Disabled ? UiRole.Muted : UiRole.Text, row.Label,
                        UiFlags.Label | UiFlags.Truncate | (row.Disabled ? UiFlags.Disabled : UiFlags.None));
                    switch (row.Kind)
                    {
                        case UiPartsField.Choice:
                            UiDraw.Choice(s, inRow, cx, ty, cw, row.Value, Choice, arg, row.Disabled);
                            if (m.OpenRow == r && m.OpenSection == sec + (right ? 1000 : 0))
                            {
                                openX = cx;
                                openY = ty;
                                openW = cw;
                            }
                            break;
                        case UiPartsField.Slider:
                            UiDraw.Slider(s, inRow, cx, ty, cw, row.Value, row.Share, Slider, arg, row.Disabled, row.On);
                            break;
                        case UiPartsField.Amount:
                            UiDraw.Stepper(s, inRow, cx, ty, cw, row.Value, row.CanLess, row.CanMore, Less, Amount, More, arg, row.Disabled);
                            break;
                        case UiPartsField.Switch:
                            UiDraw.Switch(s, inRow, cx, ty, cw, row.On, row.Value, Switch, arg, row.Disabled);
                            break;
                        case UiPartsField.Text:
                            UiDraw.TextField(s, inRow, cx, ty, cw, row.Value, row.Placeholder, Field, arg, row.Disabled, row.On);
                            break;
                        case UiPartsField.Search:
                            UiDraw.Search(s, inRow, cx, ty, cw, row.Placeholder, row.Value, null, Field);
                            s.Nodes[s.NodeCount - 1].Arg = arg;
                            break;
                        default:
                            UiDraw.KeyBinding(s, inRow, cx, ty, cw, row.Value, row.On, Key, arg, row.Disabled);
                            break;
                    }
                    ty += UiTokens.Control + UiTokens.S3;
                }
                ty += UiTokens.S6;
            }
        }

        // The big table at the left and the chosen row's person at the right: the face in its ring, the name, the role,
        // the facts and the graded attributes.
        private static void TablePage(UiScene s, UiAt at, UiPartsGalleryModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            UiDataTableModel t = m.Table;
            bool detail = t.Selected >= 0 && t.Selected < t.RowCount;
            float tw = w - Detail - UiTokens.S8;   // the detail keeps its room either way, so the columns never jump
            m.TableList = UiDataTable.Lay(s, at, t, x, y, tw, h, keys);
            float dx = x + tw + UiTokens.S8, dy = y + UiTokens.Control + UiTokens.S3;
            UiAt d = at;
            if (!detail)
            {
                if (t.RowCount > 0)
                {
                    UiDraw.EmptyState(s, d, dx, dy, Detail, y + h - dy, UiIcon.IdCard, m.DetailEmptyTitle, m.DetailEmptyBody, null, -1, keys);
                }
                return;   // an empty table says it once, in its own room
            }
            ref UiDataRow row = ref t.Rows[t.Selected];
            float ring = UiTokens.S1, face = UiTokens.AvatarHero;
            UiDraw.Portrait(s, d, dx + ring, dy + ring, face, row.Cells[0], row.Hue, row.Ring);
            float ny = dy + face + ring * 2f + UiTokens.S4;
            UiDraw.Text(s, d, dx, ny, Detail, UiStyle.Hero, UiRole.Text, row.Cells[0], UiFlags.Truncate);
            ny += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Hero)) + UiTokens.S1;
            UiDraw.Icon(s, d, dx, ny + (UiTokens.PairRow - UiTokens.Icon) / 2f, UiTokens.Icon, m.DetailRoleIcon, UiRole.None, UiFlags.Middle, true);
            UiDraw.TextMiddle(s, d, dx + UiTokens.Icon + UiTokens.S2, ny, UiTokens.PairRow, Detail - UiTokens.Icon - UiTokens.S2, UiStyle.Body, UiRole.Text2, m.DetailRole, UiFlags.Truncate);
            ny += UiTokens.PairRow + UiTokens.S5;
            for (int i = 0; i < m.DetailLabels.Length && i < m.DetailValues.Length; i++)
            {
                ny += UiDraw.InfoRow(s, d, dx, ny, Detail, m.DetailLabels[i], m.DetailValues[i]);
            }
            ny += UiTokens.S5;
            float lw = UiDraw.LabelColumn(Detail, m.AttributeLabels);
            for (int i = 0; i < m.AttributeLabels.Length && i < m.AttributeValues.Length; i++)
            {
                ny += UiDraw.GradeBar(s, d, dx, ny, Detail, m.AttributeLabels[i], m.AttributeValues[i], lw);
            }
        }

        // The year's line across the top, the districts' bars and the month's candles side by side under it.
        private static void Charts(UiScene s, UiAt at, UiPartsGalleryModel m, float x, float y, float w, float h)
        {
            float top = UiDraw.SnapDown((h - UiTokens.S4) * 0.48f);
            UiChart.Lay(s, at, m.Line, x, y, w, top);
            float half = UiDraw.SnapDown((w - UiTokens.S4) / 2f), y2 = y + top + UiTokens.S4, h2 = h - top - UiTokens.S4;
            UiChart.Lay(s, at, m.Bars, x, y2, half, h2, BarsBase);
            UiChart.Lay(s, at, m.Candles, x + half + UiTokens.S4, y2, w - half - UiTokens.S4, h2, CandlesBase);
        }

        // The d-pad keeps to the page and to its rows and columns. Sideways a move goes to the nearest target standing level
        // with it that way (a row's next button, never a head under it), and from the page's left edge to the sidebar; up
        // and down to the nearest sharing its width, a list stepping its own rows. A move with nowhere to go stops where it
        // is: the window's close is never one (it is Back, so a pad's press there would shut the app), nor, up and down,
        // the sidebar. The sidebar's own moves never reach the close sideways; the close stays reachable above the
        // sidebar's first entry, and down from it lands on what stands under it. Tracks link a step to the same step of the
        // track above and below.
        private static void Fence(UiScene s, int page)
        {
            int close = -1;
            for (int i = 0; i < page; i++)
            {
                close = s.Nodes[i].Command == UiDraw.CloseCommand ? i : close;
            }
            int n = s.NodeCount;
            var links = new int[n * 4];
            for (int i = 0; i < links.Length; i++)
            {
                links[i] = -2;   // left as the focus rules find it
            }
            int under = -1;
            for (int i = 0; i < n; i++)
            {
                ref UiNode node = ref s.Nodes[i];
                if (node.Layer != 0 || i == close)
                {
                    continue;
                }
                bool inPage = i >= page;
                for (int d = 0; d < 4; d++)
                {
                    int dx = d == 0 ? 1 : d == 1 ? -1 : 0, dy = d == 2 ? 1 : d == 3 ? -1 : 0;
                    int rules = UiFocusRules.Next(s, i, dx, dy);
                    if (!inPage)
                    {
                        links[i * 4 + d] = dx != 0 && rules == close ? i : -2;   // the sidebar: never sideways onto the close
                        continue;
                    }
                    int next = dx != 0 ? Level(s, i, page, dx) : node.List >= 0 && (rules == UiFocusRules.ScrollAsked || (rules >= page && Above(s, i, rules))) ? -2 : Column(s, i, page, dy);
                    if (next == -1 && dx < 0 && rules >= 0 && rules < page && rules != close)
                    {
                        next = rules;   // the page's left edge: the sidebar is a step away
                    }
                    links[i * 4 + d] = next == -1 ? i : next;
                }
                if (inPage && !node.Disabled)
                {
                    s.NodeBox(i, out float x, out float y, out float w, out float h);
                    if (under < 0)
                    {
                        under = i;
                    }
                    else
                    {
                        s.NodeBox(under, out float ux, out float uy, out float uw, out float uh);
                        under = x + w > ux + uw + UiTokens.S16 || (Math.Abs(x + w - (ux + uw)) <= UiTokens.S16 && y < uy) ? i : under;
                    }
                }
            }
            for (int i = 0; i < n; i++)
            {
                ref UiNode node = ref s.Nodes[i];
                node.Right = links[i * 4] != -2 ? links[i * 4] : node.Right;
                node.Left = links[i * 4 + 1] != -2 ? links[i * 4 + 1] : node.Left;
                node.Down = links[i * 4 + 2] != -2 ? links[i * 4 + 2] : node.Down;
                node.Up = links[i * 4 + 3] != -2 ? links[i * 4 + 3] : node.Up;
                if (i >= page && node.Command >= UiTrack.First && node.Command <= UiTrack.First + 2)
                {
                    int below = s.FindNode(node.Command + 1, node.Arg), above = s.FindNode(node.Command - 1, node.Arg);
                    node.Down = below >= 0 ? below : i;
                    node.Up = above >= 0 ? above : i;
                }
            }
            if (close >= 0 && under >= 0)
            {
                s.Nodes[close].Down = under;
            }
        }

        // The nearest page target sideways that stands level with a target (their heights overlap), or -1.
        private static int Level(UiScene s, int from, int page, int dx)
        {
            s.NodeBox(from, out float fx, out float fy, out float fw, out float fh);
            int best = -1;
            float bestAlong = float.MaxValue;
            for (int i = page; i < s.NodeCount; i++)
            {
                ref UiNode c = ref s.Nodes[i];
                if (i == from || c.Disabled || c.Layer != s.Nodes[from].Layer || !Shown(s, i))
                {
                    continue;
                }
                s.NodeBox(i, out float cx, out float cy, out float cw, out float ch);
                float along = dx > 0 ? cx - (fx + fw) : fx - (cx + cw);
                if (along < -UiTokens.S2 || Math.Min(fy + fh, cy + ch) - Math.Max(fy, cy) <= 0f)
                {
                    continue;
                }
                float score = along + Math.Abs(cy + ch / 2f - (fy + fh / 2f));
                if (score < bestAlong)
                {
                    bestAlong = score;
                    best = i;
                }
            }
            return best;
        }

        // The nearest page target up or down that shares some of a target's width, or -1.
        private static int Column(UiScene s, int from, int page, int dy)
        {
            s.NodeBox(from, out float fx, out float fy, out float fw, out float fh);
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = page; i < s.NodeCount; i++)
            {
                ref UiNode c = ref s.Nodes[i];
                if (i == from || c.Disabled || c.Layer != s.Nodes[from].Layer || !Shown(s, i) || (c.List >= 0 && c.List == s.Nodes[from].List))
                {
                    continue;
                }
                s.NodeBox(i, out float cx, out float cy, out float cw, out float ch);
                float along = dy > 0 ? cy - (fy + fh) : fy - (cy + ch);
                if (along < -UiTokens.S2 || Math.Min(fx + fw, cx + cw) - Math.Max(fx, cx) <= 0f)
                {
                    continue;
                }
                float score = along + Math.Abs(cx + cw / 2f - (fx + fw / 2f)) * 0.5f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        // A target is in view: outside a scroll region, or inside the part of one shown.
        private static bool Shown(UiScene s, int i)
        {
            ref UiNode c = ref s.Nodes[i];
            if (c.Clip < 0)
            {
                return true;
            }
            s.NodeBox(i, out float x, out float y, out float w, out float h);
            ref UiClip clip = ref s.Clips[c.Clip];
            return y + h > clip.Y + 0.5f && y < clip.Y + clip.H - 0.5f;
        }

        // Whether two targets share some of their width: up and down keep to a column, never jumping across the page.
        private static bool Above(UiScene s, int a, int b)
        {
            s.NodeBox(a, out float ax, out float ay, out float aw, out float ah);
            s.NodeBox(b, out float bx, out float by, out float bw, out float bh);
            return Math.Min(ax + aw, bx + bw) - Math.Max(ax, bx) > 0f;
        }

        // Each track on its band, one under another.
        private static void Tracks(UiScene s, UiAt at, UiPartsGalleryModel m, float x, float y, float w)
        {
            float ty = y;
            for (int i = 0; i < m.Tracks.Length; i++)
            {
                float th = UiTrack.Height(m.Tracks[i]) + UiTokens.S4 * 2f;
                int band = UiDraw.Box(s, at, x, ty, w, th, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
                UiTrack.Lay(s, at.In(band), m.Tracks[i], x + UiTokens.S4, ty + UiTokens.S4, w - UiTokens.S4 * 2f, UiTrack.First + i);   // each track its own command, its step the argument
                ty += th + UiTokens.S4;
            }
        }
    }
}
