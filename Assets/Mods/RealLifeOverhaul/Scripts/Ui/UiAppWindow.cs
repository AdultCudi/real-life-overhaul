using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>An entry of an app window's sections: its icon in its colour, its name and a count.</summary>
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

    /// <summary>A person's head on a page, Football Manager's: the face and its ring, the name, the role, a state, the key facts and a rating.</summary>
    public sealed class UiProfileModel
    {
        public string Name;
        public float Hue;
        public UiRole Ring = UiRole.Ok;
        public string Role;                        // "Customer Service"
        public UiIcon RoleIcon = UiIcon.Briefcase;
        public UiRole RoleTint = UiRole.None;      // None for the icon's own colour
        public string Employer;                    // the firm, beside the role behind its badge ("Bloom Cafe"); null for none
        public UiIcon EmployerIcon = UiIcon.Store; // the firm's kind, as its badge
        public string Status;                      // a chip beside the role ("At work"); null for none
        public UiRole StatusTone = UiRole.Ok;
        public string[] FactLabels = new string[0];
        public string[] FactValues = new string[0];
        public string RatingLabel;                 // "Ability"
        public int Rating = -1;                    // halves of five stars, 0 to 10, or -1 for none
    }

    /// <summary>
    /// The one thing a page is about, put first as a game puts it (the game in development, the shop being fitted): its
    /// picture on a large tile, what it is, its name, where it stands as a bar, and when the next step comes.
    /// </summary>
    public sealed class UiHeroModel
    {
        public UiIcon Icon;
        public UiRole Tint;                         // None for the icon's own colour
        public string Kicker;                       // "In development"
        public string Title;                        // "Cinder Valley"
        public string Line;                         // "Simulation, 64% made"
        public int Progress = -1;                   // 0 to 100, or -1 for no bar
        public string Note;                         // "Playtest in 6 days"
        public UiIcon NoteIcon = UiIcon.Calendar;
    }

    /// <summary>
    /// What an app window shows: its sections, then a page of the blocks it holds, each left out when empty: a person's
    /// head or the next step, the actions, the hero beside the figures, graded attributes, a table and a second under it,
    /// and people.
    /// </summary>
    public sealed class UiAppModel
    {
        public string Title;
        public UiIcon Icon = UiIcon.Grid;
        public UiNavEntry[] Nav = new UiNavEntry[0];
        public int NavSelected;
        public bool TabsAcross;                     // the sections as icon tabs across the top, as an app of a few has them; the sidebar otherwise
        public UiProfileModel Profile;              // a person's head at the top of the page, with the actions at its right
        public string Next;                         // the next step, a sentence (a page without a head)
        public UiActionModel[] Actions = new UiActionModel[0];
        public UiHeroModel Hero;                    // the page's one thing, beside the figures, which then take a third
        public UiFigureModel[] Figures = new UiFigureModel[0];
        public string AttributesTitle;
        public string[] AttributeLabels = new string[0];
        public int[] AttributeValues = new int[0];  // 0 to 100, each graded from red to green
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
        public string PeopleMore;                   // a link under the people to the rest of them ("See all 12 in Team"); null for none
        public string Note;                         // a quiet line under the people (what a group needs and how to fill it)
        public UiRole NoteTone = UiRole.Warn;
        public string NoteAction;                   // a link under the note to the place it names ("Open Requests"); null for none
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// The app window, after Cities: Skylines II's large windows with Football Manager's sidebar and profile
    /// (design/DIRECTION_CS2.md, sections 4.3 and 4.4): a centred window with the city round it, the app's sections as a
    /// sidebar of tinted icons or as icon tabs across the top, then a page of blocks. <see cref="Shell"/> draws the
    /// window and its sections and gives the page's room, and the blocks (<see cref="Profile"/>, <see cref="Lead"/>,
    /// <see cref="Figures"/>, <see cref="Attributes"/>, <see cref="Table"/>, <see cref="People"/>) are public, so an app
    /// whose page is shaped otherwise lays out its own from the same parts. Commands: <see cref="NavCommand"/>,
    /// <see cref="RowCommand"/>, <see cref="PersonCommand"/>, <see cref="ActionCommand"/> and <see cref="MoreCommand"/>
    /// (an index each). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiAppWindow
    {
        public const int NavCommand = 1, RowCommand = 2, PersonCommand = 3, ActionCommand = 4, MoreCommand = 5, AllCommand = 6, NoteCommand = 7;
        public const float Sidebar = 232f;          // px
        private const float NavRow = 48f;           // px: a section's entry or tab, its tile centred on the grid
        private const float Row = 40f;              // px: a table's row with its icon tile
        private const float PersonRow = 60f;        // px: a person with a face and two bars
        private const float PeopleShare = 0.4f;
        private const float AttributeColumn = 280f; // px: the least an attribute's column takes before the bars run in fewer
        private const int MostFacts = 4;
        private const float HeroTile = 72f;         // px: the hero's picture
        private const float FigureShare = 0.32f;    // of the width, the figures' beside a hero

        public static void Lay(UiScene s, UiAppModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("app window", frame.Width, frame.Height, dark);
            UiAt at = Shell(s, frame, m.Title, m.Icon, m.Nav, m.NavSelected, m.TabsAcross, keys, out float x, out float y, out float w, out float bottom);
            y = m.Profile != null ? Profile(s, at, m.Profile, m.Actions, x, y, w, keys) : Lead(s, at, m.Next, m.Actions, x, y, w, keys);
            if (m.Hero != null)
            {
                float fw = m.Figures.Length > 0 ? UiDraw.SnapDown(w * FigureShare) : 0f;
                float hh = Hero(s, at, m.Hero, x, y, fw > 0f ? w - fw - UiTokens.S4 : w);
                if (fw > 0f)
                {
                    Figures(s, at, m.Figures, x + w - fw, y, fw, hh);
                }
                y += hh + UiTokens.S5;
            }
            else
            {
                y = Figures(s, at, m.Figures, x, y, w);
            }
            y = Attributes(s, at, m.AttributesTitle, m.AttributeLabels, m.AttributeValues, x, y, w);
            float pw = m.People.Length > 0 ? UiDraw.SnapDown(w * PeopleShare) : 0f;
            float tw = m.People.Length > 0 ? w - pw - UiTokens.S6 : w;
            float under = m.Columns.Length > 0 ? Table(s, at, m.TableTitle, m.Columns, m.GradeColumn, m.Rows, RowCommand, x, y, tw, bottom - y) : y;
            if (m.MoreRows.Length > 0 && bottom - under > UiTokens.S16 * 2f)
            {
                Table(s, at, m.MoreTitle, m.MoreColumns, m.MoreGrade, m.MoreRows, MoreCommand, x, under + UiTokens.S6, tw, bottom - under - UiTokens.S6);
            }
            if (m.People.Length > 0)
            {
                People(s, at, m.PeopleTitle, m.PeopleCount, m.People, m.PeopleMore, m.Note, m.NoteTone, m.NoteAction, x + tw + UiTokens.S6, y, pw, bottom - y);
            }
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        /// <summary>
        /// The window over the city with its strip, and the app's sections: down a sidebar, or across the top as icon tabs
        /// when asked and they fit (a longer translation takes the sidebar instead). Returns the window to lay the page in,
        /// with the page's room: its left, top, width and the bottom it ends at.
        /// </summary>
        public static UiAt Shell(UiScene s, UiFrame frame, string title, UiIcon icon, UiNavEntry[] nav, int selected, bool across, IUiKeys keys,
            out float x, out float y, out float w, out float bottom)
        {
            frame.Icon = icon;
            int win = UiDraw.Window(s, frame, title, keys);
            UiAt at = UiAt.Screen.In(win);
            frame.Window(out float wx, out float wy, out float ww, out float wh);
            float top = wy + UiFrame.Strip;
            bool side = nav != null && nav.Length > 0;
            int shown = -1;
            if (side && across && TabsFit(nav, ww - UiTokens.S2 * 2f))
            {
                top = Tabs(s, at, nav, selected, wx, top, ww, out shown);
                side = false;
            }
            else if (side)
            {
                Nav(s, at, nav, selected, wx, top, wh - UiFrame.Strip, out shown);
            }
            if (shown >= 0)
            {
                s.FocusFirst = shown;   // the keys start on the section in view, never on the close
            }
            x = wx + (side ? Sidebar : 0f) + UiTokens.S5;
            w = wx + ww - UiTokens.S5 - x;
            y = top + UiTokens.S4;
            bottom = wy + wh - UiTokens.S4;
            return at;
        }

        // The sections down the left on a darker band, each its icon in its colour on a tile, its name and its count; the
        // shown one in the cyan at a fifth with its tile filled cyan, as a management game's menu.
        private static void Nav(UiScene s, UiAt at, UiNavEntry[] nav, int selected, float x, float y, float h, out int shownNode)
        {
            shownNode = -1;
            UiDraw.Band(s, at, x, y, Sidebar, h);
            float ny = y + UiTokens.S3;
            for (int i = 0; i < nav.Length; i++)
            {
                bool shown = i == selected;
                float ex = x + UiTokens.S2, ew = Sidebar - UiTokens.S4;
                int face = UiDraw.Box(s, at, ex, ny, ew, NavRow, shown ? UiRole.RowSel : UiRole.None, UiRole.None, UiTokens.RadiusLarge, shown ? UiFlags.Selected : UiFlags.None);
                UiAt inside = at.In(face);
                float tile = UiTokens.ControlSmall;
                int t = UiDraw.Tile(s, inside, ex + UiTokens.S3, ny + (NavRow - tile) / 2f, tile, nav[i].Tint, shown);   // the shown section's tile in the cyan, as a game marks the tool in hand
                float g = UiTokens.Icon;
                UiDraw.Icon(s, inside.In(t), ex + UiTokens.S3 + (tile - g) / 2f, ny + (NavRow - g) / 2f, g, nav[i].Icon, shown ? UiRole.OnAccent : nav[i].Tint,
                    UiFlags.Middle | UiFlags.Centre, true);
                float cw = string.IsNullOrEmpty(nav[i].Count) ? 0f : UiTextRules.Width(nav[i].Count, UiStyle.Small) + UiTokens.S1;
                float lx = ex + UiTokens.S3 + tile + UiTokens.S3;
                UiDraw.TextMiddle(s, inside, lx, ny, NavRow, ex + ew - UiTokens.S3 - cw - UiTokens.S2 - lx, UiStyle.Nav, UiRole.Text, nav[i].Label,
                    UiFlags.Label | UiFlags.Truncate);
                if (cw > 0f)
                {
                    UiDraw.TextMiddle(s, inside, ex + ew - UiTokens.S3 - cw, ny, NavRow, cw, UiStyle.Small, UiRole.Muted, nav[i].Count, UiFlags.Right);
                }
                int node = UiDraw.Node(s, at, ex, ny, ew, NavRow, NavCommand, i, face);
                shownNode = shown ? node : shownNode;
                ny += NavRow + UiTokens.S1;
            }
        }

        // The sections across the top on the band, each its icon in its colour on a tile, its name and its count; the shown
        // one filled cyan with dark words, as Cities: Skylines II marks the tool in hand. Returns the y under them.
        private static float Tabs(UiScene s, UiAt at, UiNavEntry[] nav, int selected, float x, float y, float w, out int shownNode)
        {
            shownNode = -1;
            float h = NavRow + UiTokens.S2 * 2f;
            UiDraw.Band(s, at, x, y, w, h);
            float tx = x + UiTokens.S2, ty = y + UiTokens.S2;   // the first tile then stands level with the page's left edge
            for (int i = 0; i < nav.Length; i++)
            {
                bool shown = i == selected;
                float tw = TabWidth(nav[i], out float lw, out float cw);
                int face = UiDraw.Box(s, at, tx, ty, tw, NavRow, shown ? UiRole.Accent : UiRole.None, UiRole.None, UiTokens.RadiusLarge, shown ? UiFlags.Selected : UiFlags.None);
                UiAt inside = at.In(face);
                float tile = UiTokens.ControlSmall;
                int t = UiDraw.Tile(s, inside, tx + UiTokens.S3, ty + (NavRow - tile) / 2f, tile, nav[i].Tint, shown);
                float g = UiTokens.Icon;
                UiDraw.Icon(s, inside.In(t), tx + UiTokens.S3 + (tile - g) / 2f, ty + (NavRow - g) / 2f, g, nav[i].Icon, shown ? UiRole.OnAccent : nav[i].Tint,
                    UiFlags.Middle | UiFlags.Centre, true);
                float lx = tx + UiTokens.S3 + tile + UiTokens.S3;
                UiDraw.TextMiddle(s, inside, lx, ty, NavRow, lw, UiStyle.Nav, shown ? UiRole.OnAccent : UiRole.Text, nav[i].Label, UiFlags.Label);
                if (cw > 0f)
                {
                    UiDraw.TextMiddle(s, inside, lx + lw + UiTokens.S2, ty, NavRow, cw, UiStyle.Small, shown ? UiRole.OnAccent : UiRole.Muted, nav[i].Count, UiFlags.None);
                }
                int node = UiDraw.Node(s, at, tx, ty, tw, NavRow, NavCommand, i, face);
                shownNode = shown ? node : shownNode;
                tx += tw + UiTokens.S1;
            }
            return y + h;
        }

        // A tab's width on the grid: its tile, its name and its count, with the name's and the count's own widths.
        private static float TabWidth(UiNavEntry e, out float label, out float count)
        {
            label = UiDraw.Snap(UiTextRules.Width(e.Label, UiStyle.Nav) + UiTokens.S1);
            count = string.IsNullOrEmpty(e.Count) ? 0f : UiDraw.Snap(UiTextRules.Width(e.Count, UiStyle.Small) + UiTokens.S1);
            return UiTokens.S3 + UiTokens.ControlSmall + UiTokens.S3 + label + (count > 0f ? UiTokens.S2 + count : 0f) + UiTokens.S4;
        }

        private static bool TabsFit(UiNavEntry[] nav, float w)
        {
            float sum = 0f;
            for (int i = 0; i < nav.Length; i++)
            {
                sum += TabWidth(nav[i], out _, out _) + (i > 0 ? UiTokens.S1 : 0f);
            }
            return sum <= w;
        }

        /// <summary>The next step at the left with its icon, the actions at the right, the primary last. Returns the y under it.</summary>
        public static float Lead(UiScene s, UiAt at, string next, UiActionModel[] actions, float x, float y, float w, IUiKeys keys)
        {
            if (string.IsNullOrEmpty(next) && (actions == null || actions.Length == 0))
            {
                return y;
            }
            float h = UiTokens.Control;
            float right = Buttons(s, at, actions, x + w, y, keys);
            if (!string.IsNullOrEmpty(next))
            {
                UiDraw.Icon(s, at, x, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Spark, UiRole.None, UiFlags.Middle, true);   // a suggestion's own mark, never a role's
                UiDraw.TextMiddle(s, at, x + UiTokens.Icon + UiTokens.S2, y, h, right - UiTokens.S6 - x - UiTokens.Icon - UiTokens.S2, UiStyle.BodyStrong, UiRole.Text, next,
                    UiFlags.Label | UiFlags.Truncate);
            }
            return y + h + UiTokens.S4;
        }

        // The actions as buttons ending at a right edge, the primary last. Returns the left edge of the first.
        private static float Buttons(UiScene s, UiAt at, UiActionModel[] actions, float right, float y, IUiKeys keys)
        {
            for (int i = (actions == null ? 0 : actions.Length) - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref actions[i];
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                right -= bw;
                UiDraw.Button(s, at, right, y, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, ActionCommand, i, a.Disabled);
                right -= UiTokens.S3;
            }
            return right;
        }

        /// <summary>
        /// A person's head as Football Manager draws it: the face in its ring, the name, the role behind its badge with a
        /// state chip, the key facts as labels over values, the rating in stars at the right, and the actions at the top
        /// right. Returns the y under it.
        /// </summary>
        public static float Profile(UiScene s, UiAt at, UiProfileModel p, UiActionModel[] actions, float x, float y, float w, IUiKeys keys)
        {
            float ring = UiTokens.S1, face = UiTokens.AvatarHero, side = face + ring * 2f;
            UiDraw.Portrait(s, at, x + ring, y + ring, face, p.Name, p.Hue, p.Ring);
            float right = Buttons(s, at, actions, x + w, y, keys);
            float tx = x + side + UiTokens.S5;
            float nameH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Hero));
            float ty = y + UiTokens.S1;
            UiDraw.Text(s, at, tx, ty, right - UiTokens.S6 - tx, UiStyle.Hero, UiRole.Text, p.Name, UiFlags.Truncate);
            ty += nameH + UiTokens.S1;
            float rh = UiTokens.PairRow;
            UiDraw.Icon(s, at, tx, ty + (rh - UiTokens.Icon) / 2f, UiTokens.Icon, p.RoleIcon, p.RoleTint, UiFlags.Middle, true);
            float rx = tx + UiTokens.Icon + UiTokens.S2;
            float chip = string.IsNullOrEmpty(p.Status) ? 0f : UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(p.Status, UiStyle.SmallStrong) + UiTokens.S1);
            float room = UiDraw.SnapDown(right - UiTokens.S6 - rx - (chip > 0f ? chip + UiTokens.S3 : 0f));
            float rw = Math.Min(UiDraw.Snap(UiTextRules.Width(p.Role ?? string.Empty, UiStyle.Body) + UiTokens.S1), room);
            UiDraw.TextMiddle(s, at, rx, ty, rh, rw, UiStyle.Body, UiRole.Text2, p.Role, UiFlags.Truncate);
            float after = rx + rw;
            if (!string.IsNullOrEmpty(p.Employer))
            {
                // The firm behind its badge, as Football Manager puts the club's crest by the contract.
                float bx = after + UiTokens.S4, badge = UiTokens.MinTarget;
                int tile = UiDraw.Tile(s, at, bx, ty, badge, UiRole.None, false);
                UiDraw.Icon(s, at.In(tile), bx + UiTokens.S1, ty + UiTokens.S1, badge - UiTokens.S2, p.EmployerIcon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
                float ew = Math.Min(UiDraw.Snap(UiTextRules.Width(p.Employer, UiStyle.BodyStrong) + UiTokens.S1), Math.Max(0f, rx + room - bx - badge - UiTokens.S2));
                UiDraw.TextMiddle(s, at, bx + badge + UiTokens.S2, ty, rh, ew, UiStyle.BodyStrong, UiRole.Text, p.Employer, UiFlags.Truncate);
                after = bx + badge + UiTokens.S2 + ew;
            }
            if (chip > 0f)
            {
                UiDraw.Chip(s, at, after + UiTokens.S3, ty + (rh - UiTokens.Badge - UiTokens.S1) / 2f, p.Status, p.StatusTone);
            }
            ty += rh + UiTokens.S3;
            float labelH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S1;
            float factsH = labelH + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title));
            float starsW = p.Rating >= 0 ? UiDraw.Snap(5f * UiTokens.Icon + 8f * UiTokens.Hairline) : 0f;
            int count = Math.Min(MostFacts, Math.Min(p.FactLabels.Length, p.FactValues.Length));
            int slots = count + (starsW > 0f ? 1 : 0);   // the rating is the last slot, so every gutter is one rhythm
            float slot = slots > 0 ? UiDraw.SnapDown((x + w - tx) / slots) : 0f;
            for (int i = 0; i < count; i++)
            {
                float fx = tx + i * slot;
                UiDraw.Text(s, at, fx, ty, slot - UiTokens.S4, UiStyle.SectionHeader, UiRole.Text2, p.FactLabels[i], UiFlags.Label | UiFlags.Truncate);
                UiDraw.Text(s, at, fx, ty + labelH, slot - UiTokens.S4, UiStyle.Title, UiRole.Text, p.FactValues[i], UiFlags.Truncate);
            }
            if (starsW > 0f)
            {
                float sx = tx + count * slot;
                UiDraw.Text(s, at, sx, ty, starsW, UiStyle.SectionHeader, UiRole.Text2, p.RatingLabel, UiFlags.Label | UiFlags.Truncate);
                UiDraw.Stars(s, at, sx, ty + labelH, UiTokens.Icon, p.Rating);
            }
            return y + UiDraw.Snap(Math.Max(side, ty + factsH - y)) + UiTokens.S6;
        }

        /// <summary>
        /// The page's one thing on a band: its picture on a large tile in its colour, what it is in capitals, its name, a
        /// line under it, where it stands as a graded bar, and when the next step comes at the right. Returns its height.
        /// </summary>
        public static float Hero(UiScene s, UiAt at, UiHeroModel hero, float x, float y, float w)
        {
            float h = HeroHeight(hero);
            UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            float tx = x + UiTokens.S4, ty = y + UiDraw.SnapDown((h - HeroTile) / 2f);
            int tile = UiDraw.Tile(s, at, tx, ty, HeroTile, hero.Tint, false);
            float g = UiTokens.S10;
            UiDraw.Icon(s, at.In(tile), tx + (HeroTile - g) / 2f, ty + (HeroTile - g) / 2f, g, hero.Icon, hero.Tint, UiFlags.Middle | UiFlags.Centre, true);
            float noteW = string.IsNullOrEmpty(hero.Note) ? 0f : UiDraw.Snap(UiTokens.Icon + UiTokens.S2 + UiTextRules.Width(hero.Note, UiStyle.BodyStrong) + UiTokens.S1);
            float ix = tx + HeroTile + UiTokens.S5, iw = x + w - UiTokens.S4 - ix;
            float kh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)), nh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display));
            float lh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body));
            float cy = y + UiTokens.S4;   // the words from the band's top, as the figures beside it start, so both read on one line
            float textW = iw - (noteW > 0f ? noteW + UiTokens.S6 : 0f);
            UiDraw.Text(s, at, ix, cy, textW, UiStyle.SectionHeader, UiRole.Text2, hero.Kicker, UiFlags.Label | UiFlags.Truncate);
            cy += kh + UiTokens.S1;
            float titleY = cy;
            UiDraw.Text(s, at, ix, cy, textW, UiStyle.Display, UiRole.Text, hero.Title, UiFlags.Truncate);   // the figures' size: a screen keeps to three
            cy += nh;
            UiDraw.Text(s, at, ix, cy, iw, UiStyle.Body, UiRole.Text2, hero.Line, UiFlags.Truncate);
            cy += lh;
            if (hero.Progress >= 0)
            {
                UiDraw.Meter(s, at, ix, cy + UiTokens.S2, iw, UiTokens.S1, hero.Progress, UiRole.Accent);
            }
            if (noteW > 0f)
            {
                float nx = x + w - UiTokens.S4 - noteW, ny = titleY + UiDraw.SnapDown((nh - UiTokens.Icon) / 2f);   // level with the name
                UiDraw.Icon(s, at, nx, ny, UiTokens.Icon, hero.NoteIcon, UiRole.None, UiFlags.Middle, true);
                UiDraw.TextMiddle(s, at, nx + UiTokens.Icon + UiTokens.S2, ny, UiTokens.Icon, noteW - UiTokens.Icon - UiTokens.S2, UiStyle.BodyStrong, UiRole.Text, hero.Note, UiFlags.None);
            }
            return h;
        }

        // A hero's band: the taller of its picture and its words, with a step of room round its words.
        private static float HeroHeight(UiHeroModel hero)
        {
            float words = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S1 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display))
                          + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body)) + (hero.Progress >= 0 ? UiTokens.S2 + UiTokens.S1 : 0f);
            return UiDraw.Snap(UiTokens.S4 * 2f + Math.Max(HeroTile, words));
        }

        /// <summary>
        /// The figures across a band, each its label in capitals over its number, a hairline between, a figure's line beside
        /// it; as tall as a hero beside them when given its height, their words then from the top as the hero's. Returns the
        /// y under them.
        /// </summary>
        public static float Figures(UiScene s, UiAt at, UiFigureModel[] figures, float x, float y, float w, float height = 0f)
        {
            int count = Math.Min(figures == null ? 0 : figures.Length, 4);
            if (count == 0)
            {
                return y;
            }
            float own = UiTokens.S3 * 2f + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S1 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display));
            float h = Math.Max(own, height);
            UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            float top = height > 0f ? y + UiTokens.S4 - UiTokens.S3 : y;   // beside a hero, its label on the hero's kicker's line
            float slot = UiDraw.SnapDown(w / count);
            for (int i = 0; i < count; i++)
            {
                float fx = x + i * slot;
                if (i > 0)
                {
                    UiDraw.Box(s, at, fx, y + UiTokens.S3, UiTokens.Hairline, h - UiTokens.S3 * 2f, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
                }
                float ix = fx + UiTokens.S4, iw = slot - UiTokens.S4 * 2f;
                UiDraw.Text(s, at, ix, top + UiTokens.S3, iw, UiStyle.SectionHeader, UiRole.Text2, figures[i].Label, UiFlags.Label | UiFlags.Truncate);
                float ny = top + UiTokens.S3 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S1;
                float vw = UiTextRules.Width(figures[i].Value, UiStyle.Display) + UiTokens.S1;
                UiDraw.Text(s, at, ix, ny, Math.Min(iw, vw), UiStyle.Display, UiRole.Text, figures[i].Value, UiFlags.Truncate);
                if (UiDraw.Drawn(figures[i].Trend) && iw - vw > UiTokens.S16 * 2f)
                {
                    var line = new UiItem
                    {
                        Kind = UiKind.Spark, Fill = UiRole.Accent, X = ix + vw + UiTokens.S4, Y = ny, W = iw - vw - UiTokens.S4, H = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display)),
                        Points = figures[i].Trend, Clip = at.Clip, Node = -1, Layer = at.Layer, Order = -1, Parent = at.Parent, Alpha = 1f
                    };
                    s.Add(ref line);
                }
            }
            return y + h + UiTokens.S5;
        }

        /// <summary>
        /// Attributes as a management game grades them: a title in capitals, then a bar each in its grade's colour, running
        /// down as many columns as the width holds (three at most), as Football Manager lists a player's. Returns the y under them.
        /// </summary>
        public static float Attributes(UiScene s, UiAt at, string title, string[] labels, int[] values, float x, float y, float w)
        {
            int n = labels == null || values == null ? 0 : Math.Min(labels.Length, values.Length);
            if (n == 0)
            {
                return y;
            }
            float ty = y;
            if (!string.IsNullOrEmpty(title))
            {
                UiDraw.Text(s, at, x, ty, w, UiStyle.SectionHeader, UiRole.Text, title, UiFlags.Label | UiFlags.Truncate);
                ty += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3;
            }
            float gap = UiTokens.S8;
            int columns = Math.Max(1, Math.Min(3, (int)((w + gap) / (AttributeColumn + gap))));
            float cw = UiDraw.SnapDown((w - gap * (columns - 1)) / columns);
            int rows = (n + columns - 1) / columns;
            float lw = UiDraw.LabelColumn(cw, labels);
            for (int i = 0; i < n; i++)
            {
                UiDraw.GradeBar(s, at, x + (i / rows) * (cw + gap), ty + (i % rows) * UiTokens.PairRow, cw, labels[i], values[i], lw);
            }
            return ty + rows * UiTokens.PairRow + UiTokens.S6;
        }

        /// <summary>
        /// A table: its title, its head in capitals, then rows that lead with an icon tile in their own colour, the graded
        /// figure in its grade's colour and the status as a soft chip. Rows a press opens are targets. Returns the y under it.
        /// </summary>
        public static float Table(UiScene s, UiAt at, string title, UiColumn[] columns, int graded, UiAppRow[] rows, int command, float x, float y, float w, float h)
        {
            float ty = y;
            if (!string.IsNullOrEmpty(title))
            {
                UiDraw.Text(s, at, x, ty, w, UiStyle.SectionHeader, UiRole.Text, title, UiFlags.Label | UiFlags.Truncate);
                ty += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3;
            }
            float tile = UiTokens.ControlSmall;
            bool icons = false;
            float first = UiTextRules.Width(columns.Length > 0 ? columns[0].Title : string.Empty, UiStyle.SectionHeader);
            for (int r = 0; r < rows.Length; r++)
            {
                icons |= rows[r].Icon != UiIcon.None;   // a table whose rows share one picture leaves it out: an icon must say what differs
                first = Math.Max(first, rows[r].Cells != null && rows[r].Cells.Length > 0 ? UiTextRules.Width(rows[r].Cells[0], UiStyle.Title) : 0f);
            }
            float lead = icons ? tile + UiTokens.S3 : 0f;
            float[] cx = Columns(columns, x + lead, w - lead, UiDraw.Snap(first + UiTokens.S6), out float[] cw);
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
                if (row.Icon != UiIcon.None)
                {
                    int t = UiDraw.Tile(s, inside, x, ty + (Row - tile) / 2f, tile, row.Tint, false);
                    float g = UiTokens.Icon;
                    UiDraw.Icon(s, inside.In(t), x + (tile - g) / 2f, ty + (Row - g) / 2f, g, row.Icon, row.Tint, UiFlags.Middle | UiFlags.Centre, true);
                }
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

        // The columns' lefts and widths: the first takes what the others leave, or keeps to its words when another grows,
        // so a short name never stands across a gulf from the rest of its row.
        private static float[] Columns(UiColumn[] columns, float x, float w, float firstWords, out float[] widths)
        {
            int n = columns.Length;
            var lefts = new float[n];
            widths = new float[n];
            int grow = -1;
            float fixedW = 0f;
            for (int c = 1; c < n; c++)
            {
                grow = columns[c].Grow && grow < 0 ? c : grow;
                fixedW += (columns[c].Grow ? 0f : columns[c].Width) + UiTokens.S4;
            }
            if (n > 0)
            {
                widths[0] = grow > 0 ? Math.Max(UiTokens.S16, Math.Min(firstWords, w - fixedW - UiTokens.S16)) : Math.Max(UiTokens.S16, w - fixedW);
            }
            float cx = x;
            for (int c = 0; c < n; c++)
            {
                if (c > 0)
                {
                    widths[c] = c == grow ? Math.Max(UiTokens.S16, w - fixedW - widths[0]) : columns[c].Width;
                }
                lefts[c] = cx;
                cx += widths[c] + UiTokens.S4;
            }
            return lefts;
        }

        /// <summary>
        /// People: their title and count, then each with their face, name, role behind its icon, and two attributes as a
        /// management game grades them, as many as the room holds, then a link to the rest. A note under them says what a
        /// group needs and how to fill it.
        /// </summary>
        public static void People(UiScene s, UiAt at, string title, string count, UiPersonRow[] people, string more, string note, UiRole noteTone, string noteAction,
            float x, float y, float w, float h)
        {
            float ty = y;
            UiDraw.Text(s, at, x, ty, w - UiTokens.S16, UiStyle.SectionHeader, UiRole.Text, title, UiFlags.Label | UiFlags.Truncate);
            if (!string.IsNullOrEmpty(count))
            {
                UiDraw.Text(s, at, x + w - UiTokens.S16, ty, UiTokens.S16, UiStyle.Small, UiRole.Muted, count, UiFlags.Right);
            }
            ty += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3;
            float noteH = string.IsNullOrEmpty(note) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(note, UiStyle.Small, w - UiTokens.Icon - UiTokens.S2, 3)) + UiTokens.S4
                          + (string.IsNullOrEmpty(noteAction) ? 0f : UiTokens.Control);
            const float face = UiTokens.AvatarRow;
            float moreH = string.IsNullOrEmpty(more) ? 0f : UiTokens.Control;
            for (int i = 0; i < people.Length && ty + PersonRow <= y + h - noteH - moreH; i++)
            {
                ref UiPersonRow p = ref people[i];
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
                float lw = UiDraw.LabelColumn(bw, p.FirstLabel, p.SecondLabel);
                UiDraw.GradeBar(s, inside, bx, by, bw, p.FirstLabel, p.First, lw);
                UiDraw.GradeBar(s, inside, bx, by + UiTokens.PairRow, bw, p.SecondLabel, p.Second, lw);
                UiDraw.Node(s, at, x - UiTokens.S2, ty, w + UiTokens.S4, PersonRow - UiTokens.S1, PersonCommand, i, row);
                ty += PersonRow;
            }
            if (moreH > 0f)
            {
                LinkRow(s, at, more, AllCommand, x, ty, w);
                ty += moreH;
            }
            if (noteH > 0f)
            {
                float ny = ty + UiTokens.S2;
                bool warns = noteTone == UiRole.Warn || noteTone == UiRole.Danger;   // a warning wears the warning's glyph, never a recoloured info mark
                UiDraw.Icon(s, at, x, ny, UiTokens.Icon, warns ? UiIcon.Warning : UiIcon.Info, warns ? noteTone : UiRole.None, UiFlags.Middle, true);
                int words = UiDraw.Text(s, at, x + UiTokens.Icon + UiTokens.S2, ny + UiTokens.Hairline, w - UiTokens.Icon - UiTokens.S2, UiStyle.Small, UiRole.Text2, note,
                    UiFlags.Label | UiFlags.Wrap | UiFlags.Middle, 3);
                if (!string.IsNullOrEmpty(noteAction))
                {
                    LinkRow(s, at, noteAction, NoteCommand, x + UiTokens.Icon + UiTokens.S2, UiDraw.Snap(s.Items[words].Y + s.Items[words].H), w - UiTokens.Icon - UiTokens.S2);
                }
            }
        }

        // A link as a link: cyan words and the arrow one gap after them, the whole row a target.
        private static void LinkRow(UiScene s, UiAt at, string words, int command, float x, float y, float w)
        {
            float h = UiTokens.Control;
            int row = UiDraw.Box(s, at, x - UiTokens.S2, y, w + UiTokens.S4, h, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            float room = w - UiTokens.Icon - UiTokens.S2;
            float mw = UiDraw.Snap(UiTextRules.Width(words, UiStyle.BodyStrong) + UiTokens.S1);
            UiDraw.TextMiddle(s, at.In(row), x, y, h, Math.Min(mw, room), UiStyle.BodyStrong, UiRole.AccentText, words, UiFlags.Label | UiFlags.Truncate);
            float drawn = Math.Min(UiTextRules.Width(words, UiStyle.BodyStrong), room);
            UiDraw.Icon(s, at.In(row), x + drawn - UiTokens.S1 + UiDraw.LinkGap, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Next, UiRole.AccentText,
                UiFlags.Middle | UiFlags.Decor);   // the arrow's glyph stands a grid step inside its box, so its box starts a step early
            UiDraw.Node(s, at, x - UiTokens.S2, y, w + UiTokens.S4, h, command, 0, row);
        }
    }
}
