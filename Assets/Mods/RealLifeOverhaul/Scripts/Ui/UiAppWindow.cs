using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>An entry of an app window's sidebar: its icon in its colour, its name and a count.</summary>
    public struct UiNavEntry
    {
        public UiIcon Icon;
        public UiRole Tint;
        public string Label;
        public string Count;
    }

    /// <summary>A row of an app window's table: its icon in its colour, its cells, a graded figure and a status chip.</summary>
    public struct UiAppRow
    {
        public UiIcon Icon;
        public UiRole Tint;
        public string[] Cells;      // as the columns, the first the row's name
        public int Grade;           // the graded column's value, 0 to 100, or -1 for none
        public string Status;
        public UiRole StatusTone;
    }

    /// <summary>A person in an app window's list: their face, name, role and two graded attributes.</summary>
    public struct UiPersonRow
    {
        public string Name;
        public float Hue;
        public UiIcon RoleIcon;
        public UiRole RoleTint;
        public string Role;
        public string FirstLabel;
        public int First;
        public string SecondLabel;
        public int Second;
    }

    /// <summary>What an app window shows: its sidebar, its next step and actions, its figures, a table and a list of people.</summary>
    public sealed class UiAppModel
    {
        public string Title;
        public UiIcon Icon = UiIcon.Grid;
        public UiNavEntry[] Nav = new UiNavEntry[0];
        public int NavSelected;
        public string Next;                         // the next step, a sentence
        public UiRole NextTone = UiRole.Accent;
        public UiActionModel[] Actions = new UiActionModel[0];
        public UiFigureModel[] Figures = new UiFigureModel[0];
        public string TableTitle;
        public UiColumn[] Columns = new UiColumn[0];
        public int GradeColumn = -1;                // the column whose figure is coloured by its grade
        public UiAppRow[] Rows = new UiAppRow[0];
        public string MoreTitle;                    // a second table under the first: the market the rows compete in
        public UiColumn[] MoreColumns = new UiColumn[0];
        public int MoreGrade = -1;
        public UiAppRow[] MoreRows = new UiAppRow[0];
        public string PeopleTitle;
        public string PeopleCount;
        public UiPersonRow[] People = new UiPersonRow[0];
        public string Note;                         // a quiet line under the people (what a group needs and how to fill it)
        public UiRole NoteTone = UiRole.Warn;
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// The app window, after Cities: Skylines II's large windows with Football Manager's sidebar (design/DIRECTION_CS2.md,
    /// section 4.3): a centred window with the city round it, a sidebar of the app's sections as tinted icons, then the
    /// page: its next step and its actions, its figures on a band, a table whose rows lead with an icon in their own colour
    /// and grade their figure from red to green, and a list of people with their faces and graded attributes. Commands:
    /// <see cref="NavCommand"/>, <see cref="RowCommand"/>, <see cref="PersonCommand"/> and <see cref="ActionCommand"/>
    /// (an index each). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiAppWindow
    {
        public const int NavCommand = 1, RowCommand = 2, PersonCommand = 3, ActionCommand = 4, MoreCommand = 5;
        public const float Sidebar = 232f;    // px
        private const float NavRow = 48f;     // px: a sidebar entry, its tile centred on the grid
        private const float Row = 40f;        // px: a table's row with its icon tile
        private const float PersonRow = 60f;  // px: a person with a face and two bars
        private const float PeopleShare = 0.4f;

        public static void Lay(UiScene s, UiAppModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("app window", frame.Width, frame.Height, dark);
            frame.Icon = m.Icon;
            int win = UiDraw.Window(s, frame, m.Title, keys);
            UiAt at = UiAt.Screen.In(win);
            frame.Window(out float wx, out float wy, out float ww, out float wh);
            float top = wy + UiFrame.Strip, h = wh - UiFrame.Strip;
            if (m.Nav.Length > 0)
            {
                Nav(s, at, m, wx, top, h);
            }
            float x = wx + (m.Nav.Length > 0 ? Sidebar : 0f) + UiTokens.S5, w = wx + ww - UiTokens.S5 - x;
            float y = top + UiTokens.S4;
            y = Lead(s, at, m, x, y, w, keys);
            y = Figures(s, at, m, x, y, w);
            float bottom = wy + wh - UiTokens.S4;
            float pw = m.People.Length > 0 ? UiDraw.SnapDown(w * PeopleShare) : 0f;
            float tw = m.People.Length > 0 ? w - pw - UiTokens.S6 : w;
            float under = Table(s, at, m.TableTitle, m.Columns, m.GradeColumn, m.Rows, RowCommand, x, y, tw, bottom - y);
            if (m.MoreRows.Length > 0 && bottom - under > UiTokens.S16 * 2f)
            {
                Table(s, at, m.MoreTitle, m.MoreColumns, m.MoreGrade, m.MoreRows, MoreCommand, x, under + UiTokens.S6, tw, bottom - under - UiTokens.S6);
            }
            if (m.People.Length > 0)
            {
                People(s, at, m, x + tw + UiTokens.S6, y, pw, bottom - y);
            }
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        // The sections down the left on a darker band, each a tinted icon on its tile, its name and its count; the shown one
        // in the cyan at a fifth with its bar, as a management game's menu.
        private static void Nav(UiScene s, UiAt at, UiAppModel m, float x, float y, float h)
        {
            UiDraw.Band(s, at, x, y, Sidebar, h);
            float ny = y + UiTokens.S3;
            for (int i = 0; i < m.Nav.Length; i++)
            {
                bool shown = i == m.NavSelected;
                float ex = x + UiTokens.S2, ew = Sidebar - UiTokens.S4;
                int face = UiDraw.Box(s, at, ex, ny, ew, NavRow, shown ? UiRole.RowSel : UiRole.None, UiRole.None, UiTokens.RadiusLarge, shown ? UiFlags.Selected : UiFlags.None);
                UiAt inside = at.In(face);
                if (shown)
                {
                    UiDraw.Box(s, inside, ex, ny + UiTokens.S2, UiTokens.SelectBar, NavRow - UiTokens.S4, UiRole.Accent, UiRole.None, UiTokens.SelectBar / 2f);
                }
                float tile = UiTokens.ControlSmall;
                int t = UiDraw.Tile(s, inside, ex + UiTokens.S3, ny + (NavRow - tile) / 2f, tile, m.Nav[i].Tint, shown);   // the shown section's tile in the cyan, as a game marks the tool in hand
                float g = UiTokens.Icon;
                UiDraw.Icon(s, inside.In(t), ex + UiTokens.S3 + (tile - g) / 2f, ny + (NavRow - g) / 2f, g, m.Nav[i].Icon, shown ? UiRole.OnAccent : m.Nav[i].Tint,
                    UiFlags.Middle | UiFlags.Centre, true);
                float cw = string.IsNullOrEmpty(m.Nav[i].Count) ? 0f : UiTextRules.Width(m.Nav[i].Count, UiStyle.Small) + UiTokens.S1;
                float lx = ex + UiTokens.S3 + tile + UiTokens.S3;
                UiDraw.TextMiddle(s, inside, lx, ny, NavRow, ex + ew - UiTokens.S3 - cw - UiTokens.S2 - lx, UiStyle.Nav, shown ? UiRole.AccentText : UiRole.Text, m.Nav[i].Label,
                    UiFlags.Label | UiFlags.Truncate);
                if (cw > 0f)
                {
                    UiDraw.TextMiddle(s, inside, ex + ew - UiTokens.S3 - cw, ny, NavRow, cw, UiStyle.Small, UiRole.Muted, m.Nav[i].Count, UiFlags.Right);
                }
                UiDraw.Node(s, at, ex, ny, ew, NavRow, NavCommand, i, face);
                ny += NavRow + UiTokens.S1;
            }
        }

        // The next step at the left with its icon, the actions at the right, the primary last. Returns the y under it.
        private static float Lead(UiScene s, UiAt at, UiAppModel m, float x, float y, float w, IUiKeys keys)
        {
            if (string.IsNullOrEmpty(m.Next) && m.Actions.Length == 0)
            {
                return y;
            }
            float h = UiTokens.Control;
            float right = x + w;
            for (int i = m.Actions.Length - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref m.Actions[i];
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                right -= bw;
                UiDraw.Button(s, at, right, y, bw, h, a.Kind, a.Label, a.Icon, a.Key, keys, ActionCommand, i, a.Disabled);
                right -= UiTokens.S3;
            }
            if (!string.IsNullOrEmpty(m.Next))
            {
                UiDraw.Icon(s, at, x, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Lightbulb, m.NextTone, UiFlags.Middle, true);
                UiDraw.TextMiddle(s, at, x + UiTokens.Icon + UiTokens.S2, y, h, right - UiTokens.S6 - x - UiTokens.Icon - UiTokens.S2, UiStyle.BodyStrong, UiRole.Text, m.Next,
                    UiFlags.Label | UiFlags.Truncate);
            }
            return y + h + UiTokens.S4;
        }

        // The figures across a band, each its label in capitals over its number, a hairline between, a figure's line beside it.
        private static float Figures(UiScene s, UiAt at, UiAppModel m, float x, float y, float w)
        {
            int count = Math.Min(m.Figures.Length, 4);
            if (count == 0)
            {
                return y;
            }
            float h = UiTokens.S3 * 2f + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S1 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display));
            UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            float slot = UiDraw.SnapDown(w / count);
            for (int i = 0; i < count; i++)
            {
                float fx = x + i * slot;
                if (i > 0)
                {
                    UiDraw.Box(s, at, fx, y + UiTokens.S3, UiTokens.Hairline, h - UiTokens.S3 * 2f, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
                }
                float ix = fx + UiTokens.S4, iw = slot - UiTokens.S4 * 2f;
                UiDraw.Text(s, at, ix, y + UiTokens.S3, iw, UiStyle.SectionHeader, UiRole.Text2, m.Figures[i].Label, UiFlags.Label | UiFlags.Truncate);
                float ny = y + UiTokens.S3 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S1;
                float vw = UiTextRules.Width(m.Figures[i].Value, UiStyle.Display) + UiTokens.S1;
                UiDraw.Text(s, at, ix, ny, Math.Min(iw, vw), UiStyle.Display, UiRole.Text, m.Figures[i].Value, UiFlags.Truncate);
                if (UiDraw.Drawn(m.Figures[i].Trend) && iw - vw > UiTokens.S16 * 2f)
                {
                    var line = new UiItem
                    {
                        Kind = UiKind.Spark, Fill = UiRole.Accent, X = ix + vw + UiTokens.S4, Y = ny, W = iw - vw - UiTokens.S4, H = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display)),
                        Points = m.Figures[i].Trend, Clip = at.Clip, Node = -1, Layer = at.Layer, Order = -1, Parent = at.Parent, Alpha = 1f
                    };
                    s.Add(ref line);
                }
            }
            return y + h + UiTokens.S5;
        }

        // A table: its title, its head in capitals, then rows that lead with an icon tile in their own colour, the graded
        // figure in its grade's colour and the status as a soft chip. Rows a press opens are targets. Returns the y under it.
        private static float Table(UiScene s, UiAt at, string title, UiColumn[] columns, int graded, UiAppRow[] rows, int command, float x, float y, float w, float h)
        {
            float ty = y;
            if (!string.IsNullOrEmpty(title))
            {
                UiDraw.Text(s, at, x, ty, w, UiStyle.SectionHeader, UiRole.Text, title, UiFlags.Label | UiFlags.Truncate);
                ty += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3;
            }
            float tile = UiTokens.ControlSmall;
            float lead = tile + UiTokens.S3;
            float[] cx = Columns(columns, x + lead, w - lead, out float[] cw);
            for (int c = 0; c < columns.Length; c++)
            {
                UiDraw.Text(s, at, cx[c], ty, cw[c], UiStyle.SectionHeader, UiRole.Muted, columns[c].Title, UiFlags.Label | UiFlags.Truncate | (columns[c].Right ? UiFlags.Right : UiFlags.None));
            }
            ty += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
            UiDraw.Box(s, at, x, ty, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            ty += UiTokens.S1;
            for (int r = 0; r < rows.Length && ty + Row <= y + h; r++)
            {
                ref UiAppRow row = ref rows[r];
                int face = UiDraw.Box(s, at, x - UiTokens.S2, ty, w + UiTokens.S4, Row, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
                UiAt inside = at.In(face);
                int t = UiDraw.Tile(s, inside, x, ty + (Row - tile) / 2f, tile, row.Tint, false);
                float g = UiTokens.Icon;
                UiDraw.Icon(s, inside.In(t), x + (tile - g) / 2f, ty + (Row - g) / 2f, g, row.Icon, row.Tint, UiFlags.Middle | UiFlags.Centre, true);
                for (int c = 0; c < columns.Length && row.Cells != null && c < row.Cells.Length; c++)
                {
                    bool right = columns[c].Right;
                    if (c == columns.Length - 1 && !string.IsNullOrEmpty(row.Status))
                    {
                        float chip = Math.Min(UiDraw.SnapDown(cw[c]), UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(row.Status, UiStyle.SmallStrong) + UiTokens.S1));
                        UiDraw.Chip(s, inside, cx[c] + cw[c] - chip, ty + (Row - UiTokens.Badge - UiTokens.S1) / 2f, row.Status, row.StatusTone, chip);
                        continue;
                    }
                    UiRole ink = c == 0 ? UiRole.Text : c == graded && row.Grade >= 0 ? UiTokens.Grade(row.Grade) : UiRole.Text2;
                    UiStyle style = c == 0 ? UiStyle.Title : c == graded ? UiStyle.BodyStrong : UiStyle.Body;
                    UiDraw.TextMiddle(s, inside, cx[c], ty, Row, cw[c], style, ink, row.Cells[c], UiFlags.Truncate | (right ? UiFlags.Right : UiFlags.None));
                }
                UiDraw.Node(s, at, x - UiTokens.S2, ty, w + UiTokens.S4, Row, command, r, face);
                ty += Row;
            }
            return ty;
        }

        // The columns' lefts and widths: the first takes what the others leave.
        private static float[] Columns(UiColumn[] columns, float x, float w, out float[] widths)
        {
            int n = columns.Length;
            var lefts = new float[n];
            widths = new float[n];
            float fixedW = 0f;
            for (int c = 1; c < n; c++)
            {
                fixedW += columns[c].Width + UiTokens.S4;
            }
            widths[0] = Math.Max(UiTokens.S16, w - fixedW);
            float cx = x;
            for (int c = 0; c < n; c++)
            {
                if (c > 0)
                {
                    widths[c] = columns[c].Width;
                }
                lefts[c] = cx;
                cx += widths[c] + UiTokens.S4;
            }
            return lefts;
        }

        // The people: their title and count, then each with their face, name, role behind its icon, and two attributes as a
        // management game grades them. A note under them says what a group needs and how to fill it.
        private static void People(UiScene s, UiAt at, UiAppModel m, float x, float y, float w, float h)
        {
            float ty = y;
            UiDraw.Text(s, at, x, ty, w - UiTokens.S16, UiStyle.SectionHeader, UiRole.Text, m.PeopleTitle, UiFlags.Label | UiFlags.Truncate);
            if (!string.IsNullOrEmpty(m.PeopleCount))
            {
                UiDraw.Text(s, at, x + w - UiTokens.S16, ty, UiTokens.S16, UiStyle.Small, UiRole.Muted, m.PeopleCount, UiFlags.Right);
            }
            ty += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3;
            float noteH = string.IsNullOrEmpty(m.Note) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(m.Note, UiStyle.Small, w - UiTokens.Icon - UiTokens.S2, 3)) + UiTokens.S4;
            const float face = UiTokens.AvatarRow;
            for (int i = 0; i < m.People.Length && ty + PersonRow <= y + h - noteH; i++)
            {
                ref UiPersonRow p = ref m.People[i];
                int row = UiDraw.Box(s, at, x - UiTokens.S2, ty, w + UiTokens.S4, PersonRow - UiTokens.S1, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
                UiAt inside = at.In(row);
                UiDraw.Portrait(s, inside, x, ty + (PersonRow - UiTokens.S1 - face) / 2f, face, p.Name, p.Hue, UiRole.None);
                float tx = x + face + UiTokens.S3, half = UiDraw.SnapDown((x + w - tx) * 0.46f);
                float lh = UiTextRules.LineHeight(UiStyle.Title);
                float top = ty + (PersonRow - UiTokens.S1 - lh - UiTokens.S1 - UiTokens.Icon) / 2f;
                UiDraw.Text(s, inside, tx, top, half, UiStyle.Title, UiRole.Text, p.Name, UiFlags.Truncate | UiFlags.Middle);
                UiDraw.Icon(s, inside, tx, top + lh + UiTokens.S1, UiTokens.IconSmall, p.RoleIcon, p.RoleTint, UiFlags.Middle, true);
                UiDraw.TextMiddle(s, inside, tx + UiTokens.IconSmall + UiTokens.S1, top + lh + UiTokens.S1 - (UiTokens.Icon - UiTokens.IconSmall) / 2f, UiTokens.Icon,
                    half - UiTokens.IconSmall - UiTokens.S1, UiStyle.Small, UiRole.Text2, p.Role, UiFlags.Truncate);
                float bx = tx + half + UiTokens.S3, bw = x + w - bx;
                float by = ty + (PersonRow - UiTokens.S1 - UiTokens.PairRow * 2f) / 2f;
                UiDraw.GradeBar(s, inside, bx, by, bw, p.FirstLabel, p.First);
                UiDraw.GradeBar(s, inside, bx, by + UiTokens.PairRow, bw, p.SecondLabel, p.Second);
                UiDraw.Node(s, at, x - UiTokens.S2, ty, w + UiTokens.S4, PersonRow - UiTokens.S1, PersonCommand, i, row);
                ty += PersonRow;
            }
            if (noteH > 0f)
            {
                float ny = ty + UiTokens.S2;
                UiDraw.Icon(s, at, x, ny, UiTokens.Icon, UiIcon.Info, m.NoteTone, UiFlags.Middle, true);
                UiDraw.Text(s, at, x + UiTokens.Icon + UiTokens.S2, ny + UiTokens.Hairline, w - UiTokens.Icon - UiTokens.S2, UiStyle.Small, UiRole.Text2, m.Note,
                    UiFlags.Label | UiFlags.Wrap | UiFlags.Middle, 3);
            }
        }
    }
}
