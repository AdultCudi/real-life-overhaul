using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The control at the right of a settings row.</summary>
    public enum UiSettingControl : byte
    {
        None = 0,      // a section's heading
        Switch,
        Slider,
        Choice,
        Action,        // a button that does something and holds no value
        Key,           // one of the mod's keys: press it, then the new key
        Text
    }

    /// <summary>One row of the settings list: a section's heading, or an option with its sentence and its control.</summary>
    public struct UiSettingRow
    {
        public UiSettingControl Control;   // None for a section's heading
        public string Name;                // the option's name, or the section's
        public string Tip;                 // its one sentence, quiet under the name; null for none
        public bool On;                    // a switch
        public float Share;                // a slider's place on its track, 0 to 1
        public string Value;               // a slider's figure, a choice's value, a key's words, an action's label, a text
        public bool Greyed;                // its section's main switch is off: it reads quieter and takes no press
        public bool Listening;             // a key waiting for its new key
        public bool Differs;               // it differs from the nearest preset: a dot by its name
        public int MatchStart, MatchLength;   // the search's letters in its name, marked; length 0 for none
        public UiIcon Icon;                // a heading's glyph, in its own colour on its tile
    }

    /// <summary>
    /// The settings list: the chosen group's sections and rows (or the search's), the search, the nearest preset with how
    /// many choices differ from it and its actions, and the way back and forward through the pages seen.
    /// </summary>
    public sealed class UiSettingsModel
    {
        public UiSettingRow[] Rows = new UiSettingRow[0];
        public int RowCount;
        public float Offset;                           // px the rows have scrolled; Lay writes back what it took
        public int Reveal = -1;                        // a row to bring into view on the next layout, or -1
        public string SearchPlaceholder;               // "Search the options"
        public string Query;
        public string Found;                           // "12 found" while searching
        public string Preset;                          // "Nearest preset: Standard"
        public string Differs;                         // "4 choices differ"; null when none differ
        public UiActionModel[] PresetActions = new UiActionModel[0];
        public bool CanBack, CanForward;
        public string EmptyTitle, EmptyBody;           // a search that finds nothing
    }

    /// <summary>
    /// A settings list, a block of an app window's page (its groups stand in the window's sidebar, a Tests group last
    /// when shown): the way back and forward through the pages seen and the search at its head, the nearest preset on a
    /// band with how many choices differ from it, then the chosen group's sections as bands under their capitals, each
    /// option a row of its name, its one sentence quiet under it and its control at the right (a switch, a slider, a
    /// choice, an action, a key, a text). A section's main switch off greys the rest of its rows; a search marks the
    /// letters it found; a choice differing from the preset carries a dot. Hundreds of rows scroll in their frame, only
    /// those in view laid out. For the mod's settings (Meta/SettingsView, OptionsTree, OptionsPlan, SettingsRules) and the
    /// game's own Options in the city. Commands: <see cref="RowCommand"/> (a row's control), <see cref="SearchCommand"/>,
    /// <see cref="PresetCommand"/> (an action of the preset band), <see cref="HistoryCommand"/> (-1 back, 1 forward) and
    /// <see cref="GroupCommand"/> (a group in the sidebar, <see cref="LayWithGroups"/>).
    /// Game-free. Cost: a layout a change, a sum a row.
    /// </summary>
    public static class UiSettingsList
    {
        public const int RowCommand = 50, SearchCommand = 51, PresetCommand = 52, HistoryCommand = 53, GroupCommand = 54;
        private const float GroupRow = 48f;          // px: a group in the sidebar, as the app window's sections stand
        public const float OptionHeight = 56f;       // px: a name over its sentence, as a list row
        public const float SectionHeight = 48f;      // px: a section's band, its tile and its capitals
        public const float ControlWidth = 264f;      // px: the controls' column at the rows' right
        public const float SearchWidth = 440f;       // px
        private const byte RegionHead = 1, RegionRows = 2;

        // A row the d-pad steps to: an option its section's main switch leaves live. A greyed one reads, but is no stop.
        private static bool Steps(ref UiSettingRow row) => row.Control != UiSettingControl.None && !row.Greyed;

        /// <summary>A row's height: a section's band or an option's.</summary>
        public static float RowHeight(ref UiSettingRow row) => row.Control == UiSettingControl.None ? SectionHeight : OptionHeight;

        /// <summary>The rows' height in all: what the frame scrolls through.</summary>
        public static float ContentHeight(UiSettingsModel m)
        {
            float h = 0f;
            for (int i = 0; i < m.RowCount; i++)
            {
                h += RowHeight(ref m.Rows[i]);
            }
            return h;
        }

        /// <summary>Lays the list into the room an app window's Shell gave its page; writes back the scroll it took.</summary>
        public static void Lay(UiScene s, UiAt at, UiSettingsModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            float top = Head(s, at.InRegion(RegionHead), m, x, y, w, keys);
            int search = s.NodeCount - 1;
            int presets = s.NodeCount;
            top = Preset(s, at.InRegion(RegionHead), m, x, top, w, keys);
            int presetsEnd = s.NodeCount;
            int rows = s.NodeCount;
            Rows(s, at.InRegion(RegionRows), m, x, top, w, UiDraw.SnapDown(y + h - top));
            // The head, the preset band and the rows one over another for the d-pad: the search, the preset's actions, the rows.
            int firstRow = -1;
            for (int i = rows; i < s.NodeCount && firstRow < 0; i++)
            {
                firstRow = !s.Nodes[i].Disabled && s.Nodes[i].Command == RowCommand ? i : -1;
            }
            int below = presetsEnd > presets ? presets : firstRow;
            if (below >= 0)
            {
                s.Nodes[search].Down = below;
            }
            for (int i = presets; i < presetsEnd; i++)
            {
                s.Nodes[i].Up = search;
                if (firstRow >= 0)
                {
                    s.Nodes[i].Down = firstRow;
                }
            }
            if (firstRow >= 0 && m.Offset <= 0f)
            {
                s.Nodes[firstRow].Up = presetsEnd > presets ? presetsEnd - 1 : search;
            }
        }

        /// <summary>
        /// The list in an app window that Shell drew with no sections of its own: the groups down the sidebar's place on its
        /// band, each its glyph on a tile and its name (on two smaller lines when long, as the mod's groups are), the one in
        /// view chosen, and the list beside them.
        /// </summary>
        public static void LayWithGroups(UiScene s, UiAt at, UiSettingsModel m, UiFrame frame, string[] groups, UiIcon[] icons, int chosen, IUiKeys keys, int[] found = null)
        {
            frame.Window(out float wx, out float wy, out float ww, out float wh);
            float top = wy + UiFrame.Strip;
            float x = wx + UiAppWindow.Sidebar + UiTokens.S5, y = top + UiTokens.S4;
            Lay(s, at, m, x, y, wx + ww - UiTokens.S5 - x, wy + wh - UiTokens.S4 - y, keys);
            UiDraw.Band(s, at, wx, top, UiAppWindow.Sidebar, wh - UiFrame.Strip);
            UiAt side = at.InRegion(3);
            float gy = top + UiTokens.S3, ex = wx + UiTokens.S2, ew = UiAppWindow.Sidebar - UiTokens.S4;
            for (int i = 0; i < groups.Length; i++, gy += GroupRow + UiTokens.S1)
            {
                bool shown = i == chosen;
                int drawn = s.ItemCount;
                string count = found != null && i < found.Length && found[i] > 0 ? found[i].ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
                int face = UiDraw.Box(s, side, ex, gy, ew, GroupRow, shown ? UiRole.RowSel : UiRole.None, UiRole.None, UiTokens.RadiusLarge, shown ? UiFlags.Selected : UiFlags.None);
                UiAt inside = side.In(face);
                float tile = UiTokens.ControlSmall;
                int t = UiDraw.Tile(s, inside, ex + UiTokens.S3, gy + (GroupRow - tile) / 2f, tile, UiRole.None, shown);
                UiIcon icon = icons != null && i < icons.Length ? icons[i] : UiIcon.Gear;
                UiDraw.Icon(s, inside.In(t), ex + UiTokens.S3 + (tile - UiTokens.Icon) / 2f, gy + (GroupRow - UiTokens.Icon) / 2f, UiTokens.Icon, icon, shown ? UiRole.OnAccent : UiRole.None,
                    UiFlags.Middle | UiFlags.Centre, true);
                float cw = count != null ? UiDraw.Snap(UiTextRules.Width(count, UiStyle.Small) + UiTokens.S1) : 0f;
                float lx = ex + UiTokens.S3 + tile + UiTokens.S3, lw = ex + ew - UiTokens.S3 - lx - (cw > 0f ? cw + UiTokens.S2 : 0f);
                if (cw > 0f)
                {
                    UiDraw.TextMiddle(s, inside, ex + ew - UiTokens.S3 - cw, gy, GroupRow, cw, UiStyle.Small, shown ? UiRole.Text : UiRole.Muted, count, UiFlags.Right);
                }
                if (UiTextRules.Fits(groups[i], UiStyle.Nav, lw))
                {
                    UiDraw.TextMiddle(s, inside, lx, gy, GroupRow, lw, UiStyle.Nav, UiRole.Text, groups[i], UiFlags.Label);
                }
                else
                {
                    float lh = UiTextRules.WrappedHeight(groups[i], UiStyle.Nav, lw, 2);
                    UiDraw.Text(s, inside, lx, gy + (GroupRow - lh) / 2f, lw, UiStyle.Nav, UiRole.Text, groups[i], UiFlags.Label | UiFlags.Wrap | UiFlags.Middle, 2);
                }
                int node = UiDraw.Node(s, side, ex, gy, ew, GroupRow, GroupCommand, i, face);
                if (found != null && count == null)
                {
                    UiDraw.Dim(s, drawn, node);   // a search found nothing in it: it waits, quieter
                }
                if (shown)
                {
                    s.FocusFirst = node;   // the keys start on the group in view, never on the close
                }
            }
            if (chosen < 0)
            {
                s.FocusFirst = s.FindNode(SearchCommand, 0);   // a search spans the groups: the keys start on its words
            }
            UiDraw.JoinColumns(s, wx, wx + UiAppWindow.Sidebar, wx + ww, RowCommand);
            int back = chosen >= 0 ? s.FindNode(GroupCommand, chosen) : -1;
            for (int i = 0; back >= 0 && i < s.NodeCount; i++)
            {
                if (s.Nodes[i].Command == RowCommand)
                {
                    s.Nodes[i].Left = back;   // Left from any row goes back to the group it is shown under
                }
            }
        }

        // The way back and forward through the pages seen, then the search with what it found.
        private static float Head(UiScene s, UiAt at, UiSettingsModel m, float x, float y, float w, IUiKeys keys)
        {
            float h = UiTokens.Control, b = UiTokens.Control;
            int drawn = s.ItemCount;
            int back = UiDraw.IconButton(s, at, x, y, b, UiIcon.Back, UiRole.Text, HistoryCommand, -1);
            if (!m.CanBack)
            {
                UiDraw.Dim(s, drawn, back);
            }
            drawn = s.ItemCount;
            int on = UiDraw.IconButton(s, at, x + b + UiTokens.S2, y, b, UiIcon.Next, UiRole.Text, HistoryCommand, 1);
            if (!m.CanForward)
            {
                UiDraw.Dim(s, drawn, on);
            }
            float sx = x + b * 2f + UiTokens.S2 + UiTokens.S4;
            UiDraw.Search(s, at, sx, y, Math.Min(SearchWidth, x + w - sx), m.SearchPlaceholder, m.Query, string.IsNullOrEmpty(m.Query) ? null : m.Found, SearchCommand);
            return y + h + UiTokens.S4;
        }

        // The nearest preset on a band: its words, a chip saying how many choices differ, and its actions at the right.
        private static float Preset(UiScene s, UiAt at, UiSettingsModel m, float x, float y, float w, IUiKeys keys)
        {
            if (string.IsNullOrEmpty(m.Preset))
            {
                return y;
            }
            float h = UiTokens.Control + UiTokens.S2 * 2f;
            UiDraw.Band(s, at, x, y, w, h);
            float right = x + w - UiTokens.S2;
            for (int i = m.PresetActions.Length - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref m.PresetActions[i];
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                right -= bw;
                UiDraw.Button(s, at, right, y + UiTokens.S2, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, PresetCommand, i, a.Disabled);
                right -= UiTokens.S3;
            }
            float tile = UiTokens.ControlSmall;
            int t = UiDraw.Tile(s, at, x + UiTokens.S3, y + (h - tile) / 2f, tile, UiRole.None, false);
            UiDraw.Icon(s, at.In(t), x + UiTokens.S3 + (tile - UiTokens.Icon) / 2f, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Gear, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            float tx = x + UiTokens.S3 + tile + UiTokens.S3;
            float chip = string.IsNullOrEmpty(m.Differs) ? 0f : UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(m.Differs, UiStyle.SmallStrong) + UiTokens.S1);
            float pw = Math.Min(UiDraw.Snap(UiTextRules.Width(m.Preset, UiStyle.Title) + UiTokens.S1), right - UiTokens.S6 - tx - (chip > 0f ? chip + UiTokens.S3 : 0f));
            UiDraw.TextMiddle(s, at, tx, y, h, pw, UiStyle.Title, UiRole.Text, m.Preset, UiFlags.Truncate);
            if (chip > 0f)
            {
                UiDraw.Chip(s, at, tx + pw + UiTokens.S3, y + (h - UiTokens.Badge - UiTokens.S1) / 2f, m.Differs, UiRole.AccentText);   // the rows' own mark for a choice that differs
            }
            return y + h + UiTokens.S3;
        }

        // The rows in their frame: only those in view laid out, the options a list the d-pad steps through.
        private static void Rows(UiScene s, UiAt at, UiSettingsModel m, float x, float y, float w, float vh)
        {
            float contentH = ContentHeight(m);
            float offset = m.Offset;
            if (m.Reveal >= 0)
            {
                float rt = 0f;
                for (int i = 0, option = 0; i < m.RowCount; i++)
                {
                    float rh = RowHeight(ref m.Rows[i]);
                    if (Steps(ref m.Rows[i]) && option++ == m.Reveal)
                    {
                        // A row stepped to comes into view with its section's band over it where there is room.
                        float lead = i > 0 && m.Rows[i - 1].Control == UiSettingControl.None ? SectionHeight : 0f;
                        offset = Math.Min(offset, rt - lead);
                        offset = Math.Max(offset, rt + rh - vh);
                        break;
                    }
                    rt += rh;
                }
                m.Reveal = -1;
            }
            offset = Math.Max(0f, Math.Min(offset, contentH - vh));
            m.Offset = offset;
            short clip = s.AddClip(x, y, w, vh, offset);
            s.Clips[clip].ContentHeight = contentH;
            UiDraw.ScrollThumb(s, at, x + w + UiTokens.S1, y, vh, offset, contentH);
            if (m.RowCount == 0)
            {
                s.AddList(0, 0, 0, clip, -1);
                UiDraw.EmptyState(s, at, x, y, w, vh, UiIcon.Search, m.EmptyTitle, m.EmptyBody);
                return;
            }
            int options = 0;
            for (int i = 0; i < m.RowCount; i++)
            {
                options += Steps(ref m.Rows[i]) ? 1 : 0;
            }
            short list = s.AddList(options, 0, 0, clip, -1);
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            _viewTop = offset;
            _viewBottom = offset + vh;
            float ry = 0f;
            int option0 = 0, firstShown = -1, shown = 0;
            for (int i = 0; i < m.RowCount; i++)
            {
                ref UiSettingRow row = ref m.Rows[i];
                float rh = RowHeight(ref row);
                bool isOption = row.Control != UiSettingControl.None;
                if (ry + rh > offset && ry < offset + vh)
                {
                    UiAt arriving = inClip.Arriving(shown);
                    if (isOption)
                    {
                        int node = Option(s, arriving, ref row, i, ry, w);
                        if (node >= 0 && !row.Greyed)
                        {
                            s.Nodes[node].List = list;
                            s.Nodes[node].Index = option0;
                            firstShown = firstShown < 0 ? option0 : firstShown;
                            shown++;
                        }
                    }
                    else
                    {
                        Section(s, arriving, ref row, ry, w);
                    }
                }
                option0 += Steps(ref row) ? 1 : 0;
                ry += rh;
            }
            s.Lists[list].First = Math.Max(0, firstShown);
            s.Lists[list].Shown = shown;
        }

        [ThreadStatic] private static float _viewTop, _viewBottom;   // the rows' view in their content, while they are laid

        // Whether a part of a row shows in the frame: a row the frame's edge cuts lays out only what is in view.
        private static bool Seen(float y, float h) => y + h / 2f > _viewTop && y + h / 2f < _viewBottom;

        // A section's band: its glyph on a tile and its name in capitals.
        private static void Section(UiScene s, UiAt at, ref UiSettingRow row, float y, float w)
        {
            float bandH = SectionHeight - UiTokens.S2;
            int band = UiDraw.Box(s, at, 0f, y + UiTokens.S2, w, bandH, UiRole.Band, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(band);
            if (!Seen(y + UiTokens.S2, bandH))
            {
                return;
            }
            float tile = UiTokens.ControlSmall, tx = UiTokens.S3;
            if (row.Icon != UiIcon.None)
            {
                int t = UiDraw.Tile(s, inside, UiTokens.S1, y + UiTokens.S2 + UiTokens.S1, tile, UiRole.None, false);
                UiDraw.Icon(s, inside.In(t), UiTokens.S1 + (tile - UiTokens.Icon) / 2f, y + UiTokens.S2 + (bandH - UiTokens.Icon) / 2f, UiTokens.Icon, row.Icon, UiRole.None,
                    UiFlags.Middle | UiFlags.Centre, true);
                tx = UiTokens.S1 + tile + UiTokens.S3;
            }
            UiDraw.TextMiddle(s, inside, tx, y + UiTokens.S2, bandH, w - tx - UiTokens.S3, UiStyle.SectionHeader, UiRole.Text, row.Name, UiFlags.Label | UiFlags.Truncate);
        }

        // An option: its name (the search's letters marked, a dot when it differs from the preset), its sentence, its control.
        private static int Option(UiScene s, UiAt at, ref UiSettingRow row, int index, float y, float w)
        {
            int drawn = s.ItemCount;
            int face = UiDraw.Box(s, at, 0f, y, w, OptionHeight, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(face);
            float cx = w - UiTokens.S3 - ControlWidth;
            float tx = UiTokens.S3, tw = cx - UiTokens.S6 - tx;
            float nameH = UiTextRules.LineHeight(UiStyle.Title), tipH = string.IsNullOrEmpty(row.Tip) ? 0f : UiTextRules.LineHeight(UiStyle.Small);
            float top = y + (OptionHeight - nameH - (tipH > 0f ? UiTokens.S1 + tipH : 0f)) / 2f;
            string name = UiTextRules.Cut(row.Name, UiStyle.Title, tw - (row.Differs ? UiTokens.S4 : 0f) - UiTokens.S1);
            if (row.MatchLength > 0 && row.MatchStart >= 0 && row.MatchStart + row.MatchLength <= name.Length)
            {
                // The letters the search found, on the cyan's soft fill behind them, as a game marks a found word.
                float mx = tx + UiTextRules.Width(name, 0, row.MatchStart, UiStyle.Title);
                float mw = UiTextRules.Width(name, row.MatchStart, row.MatchStart + row.MatchLength, UiStyle.Title);
                UiDraw.Box(s, inside, mx - UiTokens.Hairline, top, mw + UiTokens.Hairline * 2f, nameH, UiRole.AccentSoft, UiRole.None, UiTokens.RadiusSmall, UiFlags.Decor | UiFlags.Share);
            }
            float nw = Math.Min(tw, UiDraw.Snap(UiTextRules.Width(name, UiStyle.Title) + UiTokens.S1));
            float cy = y + (OptionHeight - UiTokens.Control) / 2f;
            if (!Seen(cy, UiTokens.Control))
            {
                return -1;   // cut by the frame's edge past its middle: its words and control wait until it scrolls in
            }
            UiDraw.Text(s, inside, tx, top, nw, UiStyle.Title, UiRole.Text, name, UiFlags.Truncate | UiFlags.Middle);
            if (row.Differs)
            {
                // A choice that differs from the preset: a dot after its name, as a changed setting is marked.
                UiDraw.Circle(s, inside, tx + nw + UiTokens.S1, top + (nameH - UiTokens.StatusDot) / 2f, UiTokens.StatusDot, UiTokens.StatusDot, UiRole.AccentText,
                    UiFlags.Middle | UiFlags.Share);
            }
            if (tipH > 0f)
            {
                UiDraw.Text(s, inside, tx, top + nameH + UiTokens.S1, tw, UiStyle.Small, UiRole.Muted, row.Tip, UiFlags.Truncate | UiFlags.Middle);
            }
            int node;
            switch (row.Control)
            {
                case UiSettingControl.Switch: node = UiDraw.Switch(s, inside, cx, cy, ControlWidth, row.On, row.Value, RowCommand, index, row.Greyed); break;
                case UiSettingControl.Slider: node = UiDraw.Slider(s, inside, cx, cy, ControlWidth, row.Value, row.Share, RowCommand, index, row.Greyed); break;
                case UiSettingControl.Choice: node = UiDraw.Choice(s, inside, cx, cy, ControlWidth, row.Value, RowCommand, index, row.Greyed); break;
                case UiSettingControl.Key: node = UiDraw.KeyBinding(s, inside, cx, cy, ControlWidth, row.Value, row.Listening, RowCommand, index, row.Greyed); break;
                case UiSettingControl.Text: node = UiDraw.TextField(s, inside, cx, cy, ControlWidth, row.Value, null, RowCommand, index, row.Greyed); break;
                default:
                    node = UiDraw.Button(s, inside, cx, cy, ControlWidth, UiTokens.Control, UiButtonKind.Secondary, row.Value, UiIcon.None, UiAction.Count, null, RowCommand, index, row.Greyed);
                    break;
            }
            if (row.Greyed)
            {
                UiDraw.Dim(s, drawn, node);   // its section's main switch is off: it waits in its place, quieter, and takes no press
            }
            return node;
        }
    }
}
