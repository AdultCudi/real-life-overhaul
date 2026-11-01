using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a dashboard shows: its title and tabs, a row of big figures with their changes, a table or two and a side panel.</summary>
    public sealed class UiDashboardModel
    {
        public string Title;
        public string Subtitle;
        public UiRole SubtitleInk = UiRole.Text2;   // the line under the title: a page's next step takes its weight's colour
        public string[] Tabs = new string[0];
        public int Tab;
        public UiFigureModel[] Figures = new UiFigureModel[0];
        public int FigureCount;
        public string TableTitle;
        public UiTableModel Table = new UiTableModel();
        public UiTableModel More;             // a second table under the first, its columns naming it, or null
        public string SideTitle;
        public UiPairModel[] Side = new UiPairModel[0];
        public int SideCount;
        public string StatsTitle;
        public UiStatModel[] Stats = new UiStatModel[0];
        public int StatCount;
        public bool StatsFirst;               // the bars before the pairs in the side panel
        public string StatsNote;              // a quiet line under the bars, saying how to fill what is needed, or null
        public UiActionModel Action;          // the page's one primary action, drawn where tabs would be
        public bool HasAction;
        public UiActionModel[] Others = new UiActionModel[0];   // the page's other actions, at the primary's left
        public int OtherCount;
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
        public UiPageShape Shape;             // what the page is about: a place at a glance, a person, a household, a feed
        public float Hue;                     // a profile's face, by its name's hue
        public UiRowModel[] Entries = new UiRowModel[0];   // a feed's entries or a household's people, drawn as rows with faces
        public int EntryCount;
        public bool[] EntryLeads;             // the entries a press opens, or null for all
        public string EntriesTitle;
        public UiLedgerSection[] Ledger = new UiLedgerSection[0];   // a household's month and its home, as a ledger's sections
    }

    /// <summary>
    /// The dashboard template, for figures over time (the Economy app's city, district and population views, a tech firm's
    /// page): the title and its tabs or actions over a row of framed figure panels (the number large, its change with an
    /// arrow that follows its sign and a colour that says whether it is good for the player, a sparkline under it or, in a
    /// wide panel, beside it), then the table that stays readable at the left and a side panel of key figures, shares and
    /// bars at the right. Each frame fits what it holds: the table's its whole rows (scrolling when they outnumber the room,
    /// breathing into calmer rows when a short table would stop well over the page's foot), the side's the lines that fit,
    /// and the two share a foot when they nearly do. A second table stands under the first and takes the room the first
    /// leaves in whole rows, a long list that fills the column without a stretched row. Commands: <see cref="TabCommand"/>
    /// (the tab's index), <see cref="RowCommand"/> and <see cref="MoreCommand"/> (a row's), <see cref="ActionCommand"/> and
    /// <see cref="OtherCommand"/> (the primary, or another action's index). Game-free. Cost: a layout a change.
    /// </summary>
    public static partial class UiDashboard
    {
        public const int TabCommand = 1, RowCommand = 2, ActionCommand = 3, OtherCommand = 4, MoreCommand = 5;
        public const float SideWidth = 380f;     // px
        public const int MostFigures = 4;        // places in the figures' row: more and they stop being glanceable
        public const int MoreLeast = 3;          // rows the second table keeps when the first runs long

        /// <summary>Lays a dashboard out into a scene.</summary>
        public static void Lay(UiScene s, UiDashboardModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("dashboard", frame.Width, frame.Height, dark);
            frame.Content(out float cx, out float cy, out float cw, out float ch);
            UiAt screen = UiAt.Screen;
            int bg = UiDraw.Window(s, frame, m.Shape == UiPageShape.Profile ? null : m.Title, keys);   // the app's window over the world, the place in its strip
            UiAt at = screen.In(bg);
            float y = cy + Lead(s, at, m, cx, cy, cw, keys);
            if (m.Shape != UiPageShape.Board)
            {
                Shaped(s, at, m, cx, y, cw, UiDraw.SnapDown(cy + ch - y), keys);
                for (int i = 0; i < m.PromptCount; i++)
                {
                    s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
                }
                UiDraw.PromptBar(s, frame, keys);
                UiDraw.Chrome(s, frame, m.Shape == UiPageShape.Profile ? null : m.Title);   // a profile says its name on its own head
                return;
            }
            y = FigureRow(s, at, m, cx, y, cw, keys);
            float bodyH = UiDraw.SnapDown(cy + ch - y);
            float sideH = SideFit(m, bodyH, out int pairs, out int stats);
            bool side = pairs > 0 || stats > 0;   // a side with nothing to say takes no room: an empty box is a hole
            float tw = side ? cw - SideWidth - UiTokens.S4 : cw;
            int foot;
            float leftH;
            if (m.More != null && m.More.Columns.Length > 0)
            {
                Tables(s, at, m, cx, y, tw, bodyH, out foot, out leftH);
            }
            else
            {
                // A table whose columns name it needs no title over it: the head row is the frame's first line then.
                bool titled = !string.IsNullOrEmpty(m.TableTitle);
                float tableTop = titled ? UiTokens.S5 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionTitle)) + UiTokens.S3 : UiTokens.S4;
                float room = bodyH - tableTop - UiTokens.S4;
                m.Table.RowHeight = 0f;
                UiDraw.Breathe(m.Table, room);   // a short table takes the page's height in calmer rows rather than stopping over a hole
                float tableH = UiDraw.TableHeight(m.Table, room);
                leftH = tableTop + tableH + UiTokens.S4;
                foot = UiDraw.Box(s, at, cx, y, tw, leftH, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
                UiAt inTable = at.In(foot);
                if (titled)
                {
                    UiDraw.PanelTitle(s, inTable, cx + UiTokens.S5, y + UiTokens.S5, tw - UiTokens.S5 * 2f, m.TableTitle, null);
                }
                UiDraw.Table(s, inTable, cx + UiTokens.S2, y + tableTop, tw - UiTokens.S4, tableH, m.Table, RowCommand);
            }
            // Nearly level: one foot reads tidier than a step of a few px. Two tables level only by what their padding and the
            // step together keep within the slack a frame may hold.
            float level = m.More != null && m.More.Columns.Length > 0 ? UiTokens.MostSlack - UiTokens.S6 : UiTokens.MostSlack;
            // The side grows to the table's foot only as far as its frame may hold nothing under its last line.
            bool levels = leftH >= sideH ? leftH - sideH <= Math.Min(level, UiTokens.MostSlack - SideSlack(m, pairs, stats)) : sideH - leftH <= level;
            if (side && levels)
            {
                s.Items[foot].H += Math.Max(0f, sideH - leftH);
                sideH = Math.Max(sideH, leftH);
            }
            if (side)
            {
                Side(s, at, m, cx + tw + UiTokens.S4, y, sideH, pairs, stats);
            }
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
            UiDraw.Chrome(s, frame, m.Title);
        }

        // The page's lead row: the tabs at its left (they say where the page is), or the line about the place (a person's
        // class, a building's district), and the actions at its right. The place itself stands in the masthead after the
        // app's name. Nothing at all when the page has none of these, and nothing for actions alone over a strip of
        // figures: they stand at the strip's end then, as a button alone on a row reads as floating. Returns the height it
        // takes with the step under it.
        private static float Lead(UiScene s, UiAt at, UiDashboardModel m, float x, float y, float w, IUiKeys keys)
        {
            bool tabs = m.Tabs.Length > 0, actions = m.HasAction || m.OtherCount > 0, line = !string.IsNullOrEmpty(m.Subtitle) && m.Shape != UiPageShape.Profile;
            if (!tabs && !line && (!actions || ActionsInStrip(m) || m.Shape == UiPageShape.Profile))
            {
                return 0f;
            }
            float left = w;
            if (actions)
            {
                left = w - Actions(s, at, m, x + w, y, keys) - UiTokens.S8;
            }
            if (tabs)
            {
                UiDraw.Tabs(s, at, x, y, Math.Min(left, TabsWidth(m.Tabs, keys)), m.Tabs, m.Tab, keys, TabCommand);
            }
            else if (line)
            {
                UiDraw.TextMiddle(s, at, x, y, UiTokens.Control, left, UiStyle.Body, m.SubtitleInk, m.Subtitle, UiFlags.Label | UiFlags.Truncate);
            }
            return UiTokens.Control + UiTokens.S4;
        }

        // The page's actions at the head's right, the primary at the far right and the others at its left. Returns their width.
        private static float Actions(UiScene s, UiAt at, UiDashboardModel m, float right, float y, IUiKeys keys)
        {
            float bx = right;
            if (m.HasAction)
            {
                bx -= UiDraw.ButtonWidth(m.Action.Kind, m.Action.Label, m.Action.Icon, m.Action.Key, keys);
                UiDraw.Button(s, at, bx, y, right - bx, UiTokens.Control, m.Action.Kind, m.Action.Label, m.Action.Icon, m.Action.Key, keys, ActionCommand, 0, m.Action.Disabled);
                bx -= UiTokens.S3;
            }
            for (int i = m.OtherCount - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref m.Others[i];
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                bx -= bw;
                UiDraw.Button(s, at, bx, y, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, OtherCommand, i, a.Disabled);
                bx -= UiTokens.S3;
            }
            return right - bx - UiTokens.S3;
        }

        // A Board page's actions stand at the end of its figures' strip when nothing else heads the page.
        private static bool ActionsInStrip(UiDashboardModel m)
        {
            return m.Shape == UiPageShape.Board && Math.Min(m.FigureCount, MostFigures) > 0 && m.Tabs.Length == 0 && string.IsNullOrEmpty(m.Subtitle);
        }

        // The figures as one strip, the markets paper's: each figure in its places of the strip's four, a thin rule between
        // them, rather than a row of boxed cards alike, and the page's actions at its end when they have no row of their
        // own. Returns the y under it.
        private static float FigureRow(UiScene s, UiAt at, UiDashboardModel m, float x, float y, float w, IUiKeys keys)
        {
            int count = Math.Min(m.FigureCount, MostFigures), places = 0;
            for (int i = 0; i < count; i++)
            {
                places += Span(ref m.Figures[i]);
            }
            if (count == 0)
            {
                return y;
            }
            float fh = FigureHeight(m);
            int strip = UiDraw.Box(s, at, x, y, w, fh, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt inStrip = at.In(strip);
            float figuresW = w;
            if ((m.HasAction || m.OtherCount > 0) && ActionsInStrip(m))
            {
                float by = y + UiDraw.SnapDown((fh - UiTokens.Control) / 2f);
                figuresW = w - Actions(s, inStrip, m, x + w - UiTokens.S5, by, keys) - UiTokens.S5 - UiTokens.S8;
            }
            float slot = UiDraw.SnapDown((figuresW - UiTokens.S4 * (places - 1)) / places);
            float fx = x;
            for (int i = 0; i < count; i++)
            {
                int span = Span(ref m.Figures[i]);
                float fw = i == count - 1 ? x + figuresW - fx : span * slot + (span - 1) * UiTokens.S4;
                if (i > 0)
                {
                    UiDraw.Box(s, inStrip, fx - UiTokens.S2 - UiTokens.Hairline, y + UiTokens.S5, UiTokens.Hairline, fh - UiTokens.S5 * 2f, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
                }
                UiDraw.Figure(s, inStrip.Arriving(i), fx + UiTokens.S5, y + UiTokens.S5, fw - UiTokens.S5 * 2f, ref m.Figures[i], true, span > 1);
                fx += fw + UiTokens.S4;
            }
            return y + fh + UiTokens.S4;
        }

        // Two framed tables down the left: the first as tall as its rows (scrolling once they would push the second under
        // its least), the second taking what the first leaves in whole rows. Gives the lower frame and the column's height.
        private static void Tables(UiScene s, UiAt at, UiDashboardModel m, float x, float y, float w, float most, out int foot, out float height)
        {
            float top = UiTokens.S4;
            m.Table.RowHeight = 0f;
            m.More.RowHeight = 0f;
            float least = top + UiTokens.ControlSmall + MoreLeast * UiTokens.TableRow + UiTokens.S4;
            float firstH = UiDraw.TableHeight(m.Table, most - UiTokens.S4 - least - top - UiTokens.S4);
            float upper = top + firstH + UiTokens.S4;
            int first = UiDraw.Box(s, at, x, y, w, upper, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiDraw.Table(s, at.In(first), x + UiTokens.S2, y + top, w - UiTokens.S4, firstH, m.Table, RowCommand);
            float y2 = y + upper + UiTokens.S4;
            float secondH = UiDraw.TableHeight(m.More, most - upper - UiTokens.S4 - top - UiTokens.S4);
            float lower = top + secondH + UiTokens.S4;
            foot = UiDraw.Box(s, at, x, y2, w, lower, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiDraw.Table(s, at.In(foot), x + UiTokens.S2, y2 + top, w - UiTokens.S4, secondH, m.More, MoreCommand);
            height = upper + UiTokens.S4 + lower;
        }

        // The side panel with the pairs and bars that fit, in the model's order.
        private static void Side(UiScene s, UiAt at, UiDashboardModel m, float sx, float y, float sideH, int pairs, int stats)
        {
            int side = UiDraw.Box(s, at, sx, y, SideWidth, sideH, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt inSide = at.In(side);
            float iy = y + UiTokens.S5, iw = SideWidth - UiTokens.S5 * 2f;
            bool first = true;
            for (int block = 0; block < 2; block++)
            {
                bool bars = (block == 0) == m.StatsFirst;
                int n = bars ? stats : pairs;
                if (n == 0)
                {
                    continue;
                }
                iy += first ? 0f : UiTokens.S6;
                first = false;
                UiDraw.Text(s, inSide, sx + UiTokens.S5, iy, iw, UiStyle.SectionHeader, UiRole.Muted, bars ? m.StatsTitle : m.SideTitle, UiFlags.Label);
                iy += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + (bars ? UiTokens.S3 : UiTokens.S2);
                for (int i = 0; i < n; i++)
                {
                    iy += bars ? UiDraw.Snap(UiDraw.StatBar(s, inSide.Arriving(i), sx + UiTokens.S5, iy, iw, ref m.Stats[i])) + (i < n - 1 ? UiTokens.S4 : 0f)
                        : UiDraw.KeyValue(s, inSide, sx + UiTokens.S5, iy, iw, m.Side[i].Key, m.Side[i].Value);
                }
                if (bars && n == m.StatCount && !string.IsNullOrEmpty(m.StatsNote))
                {
                    iy += UiTokens.S3;
                    UiDraw.Text(s, inSide, sx + UiTokens.S5, iy, iw, UiStyle.Small, UiRole.Text2, m.StatsNote, UiFlags.Label | UiFlags.Wrap, NoteLines);
                    iy += UiDraw.Snap(UiTextRules.WrappedHeight(m.StatsNote, UiStyle.Small, iw, NoteLines));
                }
            }
        }

        private const int NoteLines = 4;

        // What the side holds under its last line as laid: its padding, and the part of a pair's row under its words when the
        // pairs come last.
        private static float SideSlack(UiDashboardModel m, int pairs, int stats)
        {
            bool pairsLast = pairs > 0 && (m.StatsFirst || stats == 0);
            return UiTokens.S5 + (pairsLast ? (UiTokens.PairRow - UiTextRules.LineHeight(UiStyle.Body)) / 2f : 0f);
        }

        // How many of the side's pairs and bars fit a height, the first block before the second, and the height they take
        // with the panel's padding: a block stops at its last whole line, so nothing spills from the frame.
        private static float SideFit(UiDashboardModel m, float most, out int pairs, out int stats)
        {
            float head = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader));
            float bar = UiDraw.Snap(UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S2 + UiTokens.Segment);
            float h = UiTokens.S5;
            pairs = stats = 0;
            bool first = true;
            for (int block = 0; block < 2; block++)
            {
                bool bars = (block == 0) == m.StatsFirst;
                int n = bars ? m.StatCount : m.SideCount;
                if (n == 0)
                {
                    continue;
                }
                float start = h + (first ? 0f : UiTokens.S6) + head + (bars ? UiTokens.S3 : UiTokens.S2);
                float each = bars ? bar + UiTokens.S4 : UiTokens.PairRow, trail = bars ? UiTokens.S4 : 0f;
                int fit = 0;
                while (fit < n && start + (fit + 1) * each - trail + UiTokens.S5 <= most)
                {
                    fit++;
                }
                if (fit == 0)
                {
                    break;
                }
                h = start + fit * each - trail;
                if (bars && fit == n && !string.IsNullOrEmpty(m.StatsNote))
                {
                    h += UiTokens.S3 + UiDraw.Snap(UiTextRules.WrappedHeight(m.StatsNote, UiStyle.Small, SideWidth - UiTokens.S5 * 2f, NoteLines));
                }
                if (bars)
                {
                    stats = fit;
                }
                else
                {
                    pairs = fit;
                }
                first = false;
            }
            return h + UiTokens.S5;
        }

        // A figure spans its places only to draw its line beside its number. One with no line to draw (a new firm's days, all
        // at nothing) takes one place, so the strip's figures share its width rather than end on a blank.
        private static int Span(ref UiFigureModel figure) => figure.Span > 1 && UiDraw.Drawn(figure.Trend) ? figure.Span : 1;

        // The row's height: a change's line when any figure has one, and a sparkline's when one stands under its number.
        private static float FigureHeight(UiDashboardModel m)
        {
            bool trend = false, change = false;
            for (int i = 0; i < m.FigureCount && i < MostFigures; i++)
            {
                trend |= UiDraw.Drawn(m.Figures[i].Trend) && Span(ref m.Figures[i]) == 1;
                change |= !string.IsNullOrEmpty(m.Figures[i].Change);
            }
            float h = UiTokens.S5 * 2f + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display));
            h += change ? UiDraw.Snap(Math.Max(UiTextRules.LineHeight(UiStyle.SmallStrong), UiTokens.Icon)) : 0f;
            h += trend ? UiTokens.S2 + UiTokens.S12 : 0f;
            return UiDraw.Snap(h);
        }

        private static float TabsWidth(string[] tabs, IUiKeys keys)
        {
            float w = keys != null ? UiDraw.KeyCapWidth(UiAction.TabPrev, keys) + UiTokens.S3 + UiTokens.S2 + UiDraw.KeyCapWidth(UiAction.TabNext, keys) : 0f;
            for (int i = 0; i < tabs.Length; i++)
            {
                w += UiDraw.Snap(UiTextRules.Width(tabs[i], UiStyle.Nav) + UiTokens.S4 * 2f) + UiTokens.S1;
            }
            return UiDraw.Snap(w);
        }
    }
}
