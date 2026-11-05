using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What the city map's left dock holds: the filters, the subway's stations, or a choice of places.</summary>
    public enum MapPage : byte
    {
        Filters = 0,   // the game's map filters: groups of lines, each with a switch
        Subway,        // subway mode: the stations to ride to
        Picker         // MapPicker's "Choose a place": the bus destinations, build mode's plots, the mod's own choices
    }

    /// <summary>What a row of the list is: a group's head, a filter line, a station or a place to choose.</summary>
    public enum MapRowKind : byte
    {
        Group = 0,
        Line,
        Station,
        Choice
    }

    /// <summary>One row of the dock's list: a group's head with its master switch, a line with its switch, a station, or a choice.</summary>
    public struct MapRow
    {
        public MapRowKind Kind;
        public UiIcon Icon;
        public UiRole Tint;        // None for the glyph's own colour
        public string Picture;     // the game's own picture of the line (UiPictures key), or null for the glyph
        public string Name;
        public string Note;        // a quiet line: a choice's reason, a station's district, the mod a line comes from; or null
        public int Count;          // a line: the places it shows on the map, or -1 where none is known
        public int Total;          // a group: its lines
        public int OnCount;        // a group: its lines that are on
        public bool On;            // a line's switch; a group's master switch (all of its lines on)
        public bool Collapsed;     // a group folded
        public bool Locate;        // a line has a place the camera can go to
        public bool Off;           // the row takes no press now: a line the map cannot show, a station the player stands at, a choice that is not offered
        public bool Foreign;       // another mod's group or line
        public string Chip;        // a station's "You are here"; or null
    }

    /// <summary>A group of the map's filters on the dock's rail: its glyph, its name and how many of its lines are on.</summary>
    public struct MapRailItem
    {
        public UiIcon Icon;
        public UiRole Tint;
        public string Name;
        public int Count;          // lines on; 0 draws no badge
    }

    /// <summary>A quick chip under the search: it switches a set of existing lines on (Food, Fuel, For rent).</summary>
    public struct MapChip
    {
        public string Label;
        public UiIcon Icon;
        public bool On;            // every line it names is on
    }

    /// <summary>A place the search found: its name and its address, pressed to take the map's camera there.</summary>
    public struct MapResult
    {
        public string Name;
        public string Address;
    }

    /// <summary>A state word on a picked place or a place card: its words and its tone (open, closed, a rival's, yours, to let).</summary>
    public struct MapStateChip
    {
        public string Label;
        public UiRole Tone;
    }

    /// <summary>The place the player picked on the map, as a short block at the dock's foot.</summary>
    public struct MapPicked
    {
        public UiIcon Icon;                 // the type's glyph, the place's picture until a picture source exists
        public UiRole Tint;                 // the type's colour on its tile
        public string Picture;              // a picture the game holds for it, or null
        public string Name;
        public string Address;              // "214 5th Avenue, Garment District"
        public string Distance;             // "1.2 km on the road", or null
        public MapStateChip[] Chips;
        public string BlockLabel;           // the block's label in capitals
        public string ClearLabel;           // "Clear"
        public string LocateLabel;
        public string DestinationLabel;     // "Set destination"; in taxi mode the taxi's own words
        public bool CanDestination;
        public bool CanDetails;             // a place card stands behind the block's name
    }

    /// <summary>
    /// What the city map's left dock shows: the page's title, the rail of the filter groups, the search, the quick chips, the master
    /// switch, the list of rows (its scroll kept here), and the picked place's block. The game's CityMapFilters, its subway list and the
    /// mod's MapPicker fill it; the kit only draws it.
    /// </summary>
    public sealed class MapPanelModel
    {
        public MapPage Page;
        public string Title;
        public UiIcon Icon = UiIcon.MapTrifold;
        public string Note;                                 // one quiet sentence under the strip (the taxi's, the picker's), or null
        public MapRailItem[] Rail = new MapRailItem[0];
        public int RailShown = -1;
        public string SearchPlaceholder, Search;
        public MapResult[] Results = new MapResult[0];      // the places the typed words found, at most five
        public string ResultsNone;                          // the sentence when the words found nothing, or null while nothing is typed
        public MapChip[] Chips = new MapChip[0];
        public bool ShowAll;                                // the master switch is drawn (the filters page only)
        public string ShowAllLabel, ShowAllCount;           // "Show all", "47 of 52 on"
        public bool ShowAllOn;
        public MapRow[] Rows = new MapRow[0];
        public string Empty;                                // the sentence when the search leaves no row
        public float Offset;                                // px scrolled; the layout writes back what it took
        public int Reveal = -1;                             // a row scrolled into view as it is laid out
        public bool Picked;
        public MapPicked Place;
        public string FooterLabel;                          // the picker's Cancel, or null
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// The city map's left dock, after the approved map filters (not-in-repo/previews/ui/mockups/map/map_filters): the rail of the filter
    /// groups as tinted icons with their lines on counted, beside a panel with its title strip, the search with the places it finds, quick
    /// chips, "Show all" with how many lines are on, the lines in their groups (a switch, the glyph, the name, how many places, a locate
    /// button), and at its foot the picked place with Clear, Locate and Set destination. The same panel holds the subway's stations and the
    /// picker's choices. The list is one scroll region, every row one height, so only the rows in view are laid out. Commands (the 5000
    /// block is this job's: nothing else of the kit uses it): <see cref="RailCommand"/>, <see cref="ShowAllCommand"/>,
    /// <see cref="SearchCommand"/>, <see cref="ResultCommand"/>, <see cref="ChipCommand"/>, <see cref="GroupCommand"/>,
    /// <see cref="GroupAllCommand"/>, <see cref="LineCommand"/>, <see cref="LocateCommand"/>, <see cref="StationCommand"/>,
    /// <see cref="ChoiceCommand"/>, <see cref="FooterCommand"/>, <see cref="PickedLocateCommand"/>, <see cref="PickedDestinationCommand"/>,
    /// <see cref="PickedClearCommand"/> and <see cref="PickedMoreCommand"/>. Game-free. Cost: a layout a change, the rows in view only.
    /// </summary>
    public static class UiMapFilters
    {
        public const int RailCommand = 5000, ShowAllCommand = 5001, SearchCommand = 5002, ResultCommand = 5003, ChipCommand = 5004, GroupCommand = 5005, GroupAllCommand = 5006,
            LineCommand = 5007, LocateCommand = 5008, StationCommand = 5009, ChoiceCommand = 5010, FooterCommand = 5011, PickedLocateCommand = 5012, PickedDestinationCommand = 5013,
            PickedClearCommand = 5014, PickedMoreCommand = 5015;
        public const float Width = UiInfoPanel.Width;   // px: the dock's panel is an info panel's width
        public const float Menu = 104f;                  // px: the rail, two icons across as an info view's menu
        public const float DockRight = Menu + UiTokens.S2 + Width + UiTokens.S4;   // px: past the dock with its rail, which the names over the map keep off
        public const float Cell = 44f;                   // px: a rail icon

        /// <summary>The dock with its rail as a box of the layout's px, from the top to its foot over the status bar (PanelHeight): what the HUD's pieces keep clear of while it is up.</summary>
        public static void DockBox(float layoutHeight, out float x, out float y, out float w, out float h)
        {
            x = 0f;
            y = 0f;
            w = DockRight;
            h = layoutHeight - UiFrame.BandHeight - UiTokens.S4;
        }

        public const float RowHeight = UiTokens.Control; // px: a group, a line
        public const float TwoLineRow = 48f;             // px: a station or a choice with its quiet line
        private const float SwitchBox = 52f;
        private const float ResultRow = 44f;
        private const float Tile = 32f;                  // a line's glyph tile
        private const float PickedTile = 64f;
        private const byte RailRegion = 1, PanelRegion = 2;
        private const int ResultsMost = 5;
        private const int MostRows = 12;                   // rows in view at most: a taller screen gets a dock no taller, so the d-pad has few enough targets (UiCheckRules.MostTargets)

        /// <summary>The dock as a screen of its own over the map: its panel at the top left, with the status bar and its prompts.</summary>
        public static void Lay(UiScene s, MapPanelModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("map filters", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            Draw(s, UiAt.Screen, m, frame, UiTokens.S2, UiTokens.S4, keys);
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        /// <summary>The panel's height in a frame: from the screen's top margin to a gap over the status bar.</summary>
        public static float PanelHeight(UiFrame frame) => UiDraw.SnapDown(frame.BandY - UiTokens.S4 * 2f);

        /// <summary>The rail and the panel at a top left (px of the layout); returns the x the dock ends at.</summary>
        public static float Draw(UiScene s, UiAt at, MapPanelModel m, UiFrame frame, float x, float y, IUiKeys keys)
        {
            float px = x;
            if (m.Rail.Length > 0)
            {
                Rail(s, at.InRegion(RailRegion), m, x, y);
                px = x + Menu + UiTokens.S2;
            }
            float h = Math.Min(PanelHeight(frame), Needed(m, frame));
            UiAt column = at.InRegion(PanelRegion);
            int panel = UiDraw.Box(s, column, px, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = column.In(panel);
            float cy = UiDraw.TitleStrip(s, on, px, y, Width, m.Icon, m.Title, true);
            float ix = px + UiTokens.S4, iw = Width - UiTokens.S4 * 2f;
            cy += UiTokens.S3;
            if (!string.IsNullOrEmpty(m.Note))
            {
                int lines = UiTextRules.Lines(m.Note, UiStyle.Body, iw, 3, out float widest);
                UiDraw.Text(s, on, ix, cy, iw, UiStyle.Body, UiRole.Text2, m.Note, UiFlags.Wrap | UiFlags.Label, lines);
                cy += UiDraw.Snap(lines * UiTextRules.LineHeight(UiStyle.Body)) + UiTokens.S2;
            }
            int search = UiDraw.Search(s, on, ix, cy, iw, m.SearchPlaceholder, m.Search, null, SearchCommand);
            cy += UiTokens.Control + UiTokens.S2;
            cy = Results(s, on, m, ix, cy, iw);
            float picked = m.Picked ? PickedHeight(m) + UiTokens.S2 : 0f;
            float foot = !string.IsNullOrEmpty(m.FooterLabel) ? UiTokens.Control + UiTokens.S4 + UiTokens.S2 : 0f;
            float stepTail = m.ShowAll ? UiTokens.Control + UiTokens.S1 : 0f;
            // Quick chips only where the list keeps at least five rows: on a short screen the lines come first.
            if (m.Chips.Length > 0)
            {
                float ch = ChipsHeight(m, iw);
                if (y + h - cy - ch - UiTokens.S2 - stepTail - picked - foot - UiTokens.S2 >= RowStep(m) * 5f)
                {
                    cy += Chips(s, on, m, ix, cy, iw) + UiTokens.S2;
                }
            }
            if (m.ShowAll)
            {
                ShowAll(s, on, m, ix, cy, iw);
                cy += UiTokens.Control + UiTokens.S1;
            }
            float listBottom = y + h - picked - foot - UiTokens.S2;
            List(s, on, m, px, cy, Width, listBottom);
            if (m.Picked)
            {
                Picked(s, on, m, px, y + h - PickedHeight(m) - foot, ix, iw, keys);
            }
            if (!string.IsNullOrEmpty(m.FooterLabel))
            {
                UiDraw.Button(s, on, ix, y + h - UiTokens.Control - UiTokens.S4, iw, UiTokens.Control, UiButtonKind.Secondary, m.FooterLabel, UiIcon.Close, UiAction.Back, keys, FooterCommand, 0);
            }
            if (search >= 0)
            {
                Link(s, m);
            }
            return px + Width;
        }

        /// <summary>
        /// The height the page needs for what it holds: a short list (a search that found little, a few stops) makes a short panel, never a tall one with a hole
        /// under its rows. A long list takes the frame's whole height and scrolls.
        /// </summary>
        public static float Needed(MapPanelModel m, UiFrame frame)
        {
            float iw = Width - UiTokens.S4 * 2f;
            float need = UiFrame.Strip + UiTokens.S3;
            if (!string.IsNullOrEmpty(m.Note))
            {
                need += UiDraw.Snap(UiTextRules.Lines(m.Note, UiStyle.Body, iw, 3, out float widest) * UiTextRules.LineHeight(UiStyle.Body)) + UiTokens.S2;
            }
            need += UiTokens.Control + UiTokens.S2;
            int results = Math.Min(ResultsMost, m.Results.Length);
            need += results > 0 ? results * ResultRow + UiTokens.S2 : !string.IsNullOrEmpty(m.ResultsNone) ? UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S2 : 0f;
            if (m.Chips.Length > 0)
            {
                need += ChipsHeight(m, iw) + UiTokens.S2;
            }
            if (m.ShowAll)
            {
                need += UiTokens.Control + UiTokens.S1;
            }
            need += m.Rows.Length > 0 ? Math.Min(m.Rows.Length, MostRows) * RowStep(m)
                : !string.IsNullOrEmpty(m.Empty)
                    ? UiTokens.S4 + UiDraw.Snap(UiTextRules.Lines(m.Empty, UiStyle.Body, Width - UiTokens.S4 * 2f, 4, out float widest2) * UiTextRules.LineHeight(UiStyle.Body)) + UiTokens.S4
                    : 0f;
            need += UiTokens.S2;
            if (m.Picked)
            {
                need += PickedHeight(m) + UiTokens.S2;
            }
            if (!string.IsNullOrEmpty(m.FooterLabel))
            {
                need += UiTokens.Control + UiTokens.S4 + UiTokens.S2;
            }
            return UiDraw.Snap(need);
        }

        // ------------------------------------------------------------------------------------------------- the rail

        /// <summary>The rail's height for a number of groups: two icons across.</summary>
        public static float RailHeight(int groups) => ((groups + 1) / 2) * Cell + (((groups + 1) / 2) - 1) * UiTokens.S1 + UiTokens.S2 * 2f;

        private static void Rail(UiScene s, UiAt at, MapPanelModel m, float x, float y)
        {
            int menu = UiDraw.Box(s, at, x, y, Menu, RailHeight(m.Rail.Length), UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(menu);
            for (int i = 0; i < m.Rail.Length; i++)
            {
                float cx = x + UiTokens.S2 + (i % 2) * (Cell + UiTokens.S1), cy = y + UiTokens.S2 + (i / 2) * (Cell + UiTokens.S1);
                int node = UiDraw.IconButton(s, on, cx, cy, Cell, m.Rail[i].Icon, m.Rail[i].Tint, RailCommand, i, i == m.RailShown);
                if (m.Rail[i].Count > 0)
                {
                    UiDraw.Badge(s, on, cx + Cell + UiTokens.S1, cy - UiTokens.S1, m.Rail[i].Count, i == m.RailShown, UiFlags.Overflow);
                }
                if (i == m.RailShown)
                {
                    s.FocusFirst = node;
                }
            }
        }

        // ------------------------------------------------------------------------------------------------- search, chips, master switch

        private static float Results(UiScene s, UiAt at, MapPanelModel m, float x, float y, float w)
        {
            int n = Math.Min(ResultsMost, m.Results.Length);
            if (n == 0)
            {
                if (!string.IsNullOrEmpty(m.ResultsNone))
                {
                    UiDraw.Text(s, at, x + UiTokens.S4, y, w - UiTokens.S4 * 2f, UiStyle.Small, UiRole.Muted, m.ResultsNone, UiFlags.Label | UiFlags.Truncate);
                    return y + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S2;
                }
                return y;
            }
            for (int i = 0; i < n; i++)
            {
                float ry = y + i * ResultRow;
                int face = UiDraw.Box(s, at, x, ry, w, ResultRow, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
                UiAt inside = at.In(face);
                UiDraw.Icon(s, inside, x + UiTokens.S4, ry + (ResultRow - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.MapPin, UiRole.None, UiFlags.Middle);
                float tx = x + UiTokens.S4 + UiTokens.Icon + UiTokens.S3, tw = w - (tx - x) - UiTokens.S4;
                float lh = UiTextRules.LineHeight(UiStyle.Title), sh = UiTextRules.LineHeight(UiStyle.Small);
                float top = ry + (ResultRow - lh - sh) / 2f;
                UiDraw.Text(s, inside, tx, top, tw, UiStyle.Title, UiRole.Text, m.Results[i].Name, UiFlags.Truncate | UiFlags.Middle);
                UiDraw.Text(s, inside, tx, top + lh, tw, UiStyle.Small, UiRole.Text2, m.Results[i].Address, UiFlags.Truncate | UiFlags.Middle);
                UiDraw.Node(s, at, x, ry, w, ResultRow, ResultCommand, i, face);
            }
            return y + n * ResultRow + UiTokens.S2;
        }

        private static float ChipsHeight(MapPanelModel m, float w)
        {
            float cx = 0f, rows = 1f;
            for (int i = 0; i < m.Chips.Length; i++)
            {
                float cw = ChipWidth(m.Chips[i]);
                if (cx > 0f && cx + cw > w)
                {
                    cx = 0f;
                    rows++;
                }
                cx += cw + UiTokens.S2;
            }
            return rows * UiTokens.Chip + (rows - 1f) * UiTokens.S2;
        }

        private static float ChipWidth(MapChip c) => UiDraw.ButtonWidth(UiButtonKind.Chip, c.Label, c.Icon, UiAction.Count, null);

        // A chip that is on is filled cyan with dark words (the chosen look); one that is off is the quiet chip.
        private static float Chips(UiScene s, UiAt at, MapPanelModel m, float x, float y, float w)
        {
            float cx = x, cy = y;
            for (int i = 0; i < m.Chips.Length; i++)
            {
                float cw = Math.Min(w, ChipWidth(m.Chips[i]));
                if (cx > x && cx + cw > x + w)
                {
                    cx = x;
                    cy += UiTokens.Chip + UiTokens.S2;
                }
                int chip = UiDraw.Button(s, at, cx, cy, cw, UiTokens.Chip, m.Chips[i].On ? UiButtonKind.Primary : UiButtonKind.Chip, m.Chips[i].Label, m.Chips[i].Icon, UiAction.Count, null,
                    ChipCommand, i);
                if (m.Chips[i].On)
                {
                    s.Items[s.Nodes[chip].Face].Flags |= UiFlags.Selected;   // chosen, not the screen's main action
                }
                cx += cw + UiTokens.S2;
            }
            return cy - y + UiTokens.Chip;
        }

        // "Show all" and how many lines are on, over the list, with the master switch at the right.
        private static void ShowAll(UiScene s, UiAt at, MapPanelModel m, float x, float y, float w)
        {
            float h = UiTokens.Control;
            float sx = x + w - SwitchBox;
            float cw = string.IsNullOrEmpty(m.ShowAllCount) ? 0f : UiDraw.Snap(UiTextRules.Width(m.ShowAllCount, UiStyle.Small) + UiTokens.S1);
            UiDraw.TextMiddle(s, at, x, y, h, sx - x - cw - UiTokens.S4, UiStyle.BodyStrong, UiRole.Text, m.ShowAllLabel, UiFlags.Label | UiFlags.Truncate);
            if (cw > 0f)
            {
                UiDraw.TextMiddle(s, at, sx - cw - UiTokens.S2, y, h, cw, UiStyle.Small, UiRole.Muted, m.ShowAllCount, UiFlags.Right | UiFlags.Truncate);
            }
            UiDraw.Switch(s, at, sx, y, SwitchBox, m.ShowAllOn, null, ShowAllCommand, 0, false);
        }

        // ------------------------------------------------------------------------------------------------- the list

        /// <summary>The height of one row of the list on a page.</summary>
        public static float RowStep(MapPanelModel m) => m.Page == MapPage.Filters ? RowHeight : TwoLineRow;

        private static void List(UiScene s, UiAt at, MapPanelModel m, float x, float top, float w, float bottom)
        {
            int rows = m.Rows.Length;
            float room = Math.Max(0f, bottom - top);
            if (rows == 0)
            {
                m.Offset = 0f;
                if (!string.IsNullOrEmpty(m.Empty) && room >= UiTokens.S12)
                {
                    UiDraw.Text(s, at, x + UiTokens.S4, top + UiTokens.S4, w - UiTokens.S4 * 2f, UiStyle.Body, UiRole.Text2, m.Empty, UiFlags.Wrap | UiFlags.Label,
                        Math.Max(1, UiTextRules.Lines(m.Empty, UiStyle.Body, w - UiTokens.S4 * 2f, 4, out float widest)));
                }
                return;
            }
            float step = RowStep(m);
            float viewH = Math.Max(step, Math.Min((float)Math.Floor(room / step) * step, MostRows * step));   // whole rows, never a sliver of one
            float content = rows * step;
            float offset = m.Offset;
            if (m.Reveal >= 0 && m.Reveal < rows)
            {
                offset = Math.Min(offset, m.Reveal * step);
                offset = Math.Max(offset, m.Reveal * step + step - viewH);
            }
            offset = Math.Max(0f, Math.Min((float)Math.Round(offset / step) * step, Math.Max(0f, content - viewH)));
            m.Offset = offset;
            m.Reveal = -1;
            short clip = s.AddClip(x, top, w, Math.Min(viewH, content), offset);
            s.Clips[clip].ContentHeight = content;
            int first = Math.Min(rows - 1, (int)(offset / step));
            int last = Math.Min(rows - 1, (int)((offset + viewH - 0.5f) / step));
            short list = s.AddList(rows, first, last - first + 1, clip, -1);
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            for (int r = first; r <= last; r++)
            {
                Row(s, inClip.Arriving(r - first), ref m.Rows[r], r * step, w, step, list, r);
            }
            if (content > viewH + 0.5f)
            {
                UiDraw.ScrollThumb(s, at, x + w - UiTokens.S1, top, Math.Min(viewH, content), offset, content);
            }
        }

        // One row of the list, in its clip's own coordinates: x from the panel's left edge, y from the content's top.
        private static void Row(UiScene s, UiAt at, ref MapRow r, float y, float w, float h, short list, int index)
        {
            float pad = UiTokens.S4;
            switch (r.Kind)
            {
                case MapRowKind.Group:
                {
                    int face = UiDraw.Box(s, at, 0f, y, w, h, UiRole.Band, UiRole.None, 0f, UiFlags.Decor);
                    int row = UiDraw.Box(s, at, 0f, y, w, h, UiRole.None, UiRole.None, 0f);
                    UiAt inside = at.In(row);
                    UiDraw.Icon(s, inside, pad, y + (h - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, r.Collapsed ? UiIcon.Next : UiIcon.Down, UiRole.Muted, UiFlags.Middle);
                    float gx = pad + UiTokens.IconSmall + UiTokens.S2;
                    UiDraw.Icon(s, inside, gx, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, r.Icon, r.Tint, UiFlags.Middle);
                    float sx = w - pad - SwitchBox;
                    float cw = UiDraw.Snap(UiTextRules.Width(CountOf(r), UiStyle.Small) + UiTokens.S1);
                    float nx = gx + UiTokens.Icon + UiTokens.S2;
                    UiDraw.TextMiddle(s, inside, nx, y, h, sx - cw - UiTokens.S4 - nx, UiStyle.SmallStrong, UiRole.Text2, r.Name.ToUpperInvariant(), UiFlags.Label | UiFlags.Truncate);
                    UiDraw.TextMiddle(s, inside, sx - cw - UiTokens.S2, y, h, cw, UiStyle.Small, UiRole.Muted, CountOf(r), UiFlags.Right | UiFlags.Truncate);
                    int n = UiDraw.Node(s, at, 0f, y, w, h, GroupCommand, index, row, false, list, index);
                    int all = UiDraw.Switch(s, inside, sx, y, SwitchBox, r.On, null, GroupAllCommand, index, r.Off);
                    s.Nodes[all].List = list;
                    s.Nodes[all].Index = index;
                    s.Nodes[all].Left = n;
                    s.Nodes[n].Right = all;
                    return;
                }
                case MapRowKind.Line:
                {
                    int row = UiDraw.Box(s, at, 0f, y, w, h, UiRole.None, UiRole.None, 0f);
                    UiAt inside = at.In(row);
                    LineGlyph(s, inside, pad, y + (h - Tile) / 2f, ref r);
                    float sx = w - pad - SwitchBox;
                    float lx = sx - UiTokens.S2 - UiTokens.MinTarget;
                    float cw = r.Count >= 0 ? UiDraw.Snap(UiTextRules.Width(r.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), UiStyle.Small) + UiTokens.S1) : 0f;
                    float nx = pad + Tile + UiTokens.S3;
                    float right = lx - UiTokens.S2 - cw - (cw > 0f ? UiTokens.S2 : 0f);
                    UiRole ink = r.On && !r.Off ? UiRole.Text : UiRole.Text2;
                    float nameW = Math.Min(UiDraw.Snap(UiTextRules.Width(r.Name, UiStyle.Body) + UiTokens.S1), right - nx);
                    UiDraw.TextMiddle(s, inside, nx, y, h, nameW, UiStyle.Body, ink, r.Name, UiFlags.Label | UiFlags.Truncate);
                    if (!string.IsNullOrEmpty(r.Note) && right - nx - nameW - UiTokens.S2 > UiTokens.S12)
                    {
                        UiDraw.TextMiddle(s, inside, nx + nameW + UiTokens.S2, y, h, right - nx - nameW - UiTokens.S2, UiStyle.Small, UiRole.Muted, r.Note, UiFlags.Truncate);
                    }
                    if (cw > 0f)
                    {
                        UiDraw.TextMiddle(s, inside, lx - UiTokens.S2 - cw, y, h, cw, UiStyle.Small, UiRole.Muted, r.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), UiFlags.Right);
                    }
                    int n = UiDraw.Node(s, at, 0f, y, w, h, LineCommand, index, row, r.Off, list, index);
                    int locate = -1;
                    if (r.Locate)
                    {
                        locate = UiDraw.IconButton(s, inside, lx, y + (h - UiTokens.MinTarget) / 2f, UiTokens.MinTarget, UiIcon.Locate, UiRole.None, LocateCommand, index, false,
                            UiAction.Count, false, true);
                    }
                    UiDraw.SwitchLook(s, inside, sx + SwitchBox, y, r.On, r.Off);   // the row is the target: it toggles, the switch shows how it stands
                    if (locate >= 0)
                    {
                        s.Nodes[n].Right = locate;
                        s.Nodes[locate].Left = n;
                    }
                    return;
                }
                case MapRowKind.Station:
                case MapRowKind.Choice:
                {
                    int row = UiDraw.Box(s, at, 0f, y, w, h, UiRole.None, UiRole.None, 0f);
                    UiAt inside = at.In(row);
                    LineGlyph(s, inside, pad, y + (h - Tile) / 2f, ref r);
                    float nx = pad + Tile + UiTokens.S3;
                    float chip = string.IsNullOrEmpty(r.Chip) ? 0f : UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(r.Chip, UiStyle.SmallStrong) + UiTokens.S1);
                    float right = w - pad - (chip > 0f ? chip + UiTokens.S2 : 0f);
                    bool two = !string.IsNullOrEmpty(r.Note);
                    float lh = UiTextRules.LineHeight(UiStyle.Title), sh = UiTextRules.LineHeight(UiStyle.Small);
                    float top = y + (h - lh - (two ? sh : 0f)) / 2f;
                    UiRole ink = r.Off ? UiRole.Text2 : UiRole.Text;
                    UiDraw.Text(s, inside, nx, top, right - nx, UiStyle.Title, ink, r.Name, UiFlags.Truncate | UiFlags.Middle);
                    if (two)
                    {
                        UiDraw.Text(s, inside, nx, top + lh, right - nx, UiStyle.Small, r.Off ? UiRole.WarnText : UiRole.Text2, r.Note, UiFlags.Truncate | UiFlags.Middle);
                    }
                    if (chip > 0f)
                    {
                        UiDraw.Chip(s, inside, w - pad - chip, y + (h - UiTokens.Badge - UiTokens.S1) / 2f, r.Chip, UiRole.Ok, chip);
                    }
                    UiDraw.Node(s, at, 0f, y, w, h, r.Kind == MapRowKind.Station ? StationCommand : ChoiceCommand, index, row, r.Off, list, index);
                    return;
                }
            }
        }

        private static string CountOf(MapRow r) => r.Total > 0 ? r.OnCount + "/" + r.Total : string.Empty;

        // A line's glyph on its slate tile: the game's own picture where it has one, else the kit's glyph in its colour.
        private static void LineGlyph(UiScene s, UiAt at, float x, float y, ref MapRow r)
        {
            if (!string.IsNullOrEmpty(r.Picture))
            {
                UiDraw.PictureFace(s, at, x, y, Tile, r.Picture);
                return;
            }
            int tile = UiDraw.Tile(s, at, x, y, Tile, r.Tint, false);
            UiDraw.Icon(s, at.In(tile), x + (Tile - UiTokens.Icon) / 2f, y + (Tile - UiTokens.Icon) / 2f, UiTokens.Icon, r.Icon, r.Tint, UiFlags.Middle | UiFlags.Centre, true);
        }

        // The d-pad's way: the rail's right column into the search, the search down into the list, and the list's own up and down.
        private static void Link(UiScene s, MapPanelModel m)
        {
            int search = s.FindNode(SearchCommand, 0);
            if (search < 0)
            {
                return;
            }
            for (int i = 0; i < m.Rail.Length; i++)
            {
                int node = s.FindNode(RailCommand, i);
                if (node >= 0 && (i % 2 == 1 || i == m.Rail.Length - 1))
                {
                    s.Nodes[node].Right = search;
                }
            }
            int shown = m.RailShown >= 0 ? s.FindNode(RailCommand, m.RailShown) : -1;
            if (shown >= 0)
            {
                s.Nodes[search].Left = shown;
            }
        }

        // ------------------------------------------------------------------------------------------------- the picked place

        /// <summary>The picked block's height.</summary>
        public static float PickedHeight(MapPanelModel m)
        {
            bool chips = m.Place.Chips != null && m.Place.Chips.Length > 0;
            return UiTokens.S3 + UiTokens.PairRow + UiTokens.S1 + PickedTile + (chips ? UiTokens.S2 + UiTokens.Badge + UiTokens.S1 : 0f) + UiTokens.S3 + UiTokens.Control + UiTokens.S3;
        }

        private static void Picked(UiScene s, UiAt at, MapPanelModel m, float px, float y, float ix, float iw, IUiKeys keys)
        {
            ref MapPicked p = ref m.Place;
            float h = PickedHeight(m);
            UiDraw.Band(s, at, px, y, Width, h);
            float cy = y + UiTokens.S3;
            float clear = UiDraw.Snap(UiTextRules.Width(p.ClearLabel, UiStyle.SmallStrong) + UiTokens.S3 * 2f);
            UiDraw.TextMiddle(s, at, ix, cy, UiTokens.PairRow, iw - clear, UiStyle.SectionHeader, UiRole.Text, p.BlockLabel, UiFlags.Label | UiFlags.Truncate);
            int link = UiDraw.Box(s, at, ix + iw - clear + UiTokens.S3, cy, clear - UiTokens.S3, UiTokens.PairRow, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiDraw.TextMiddle(s, at.In(link), ix + iw - clear + UiTokens.S3, cy, UiTokens.PairRow, clear - UiTokens.S3, UiStyle.SmallStrong, UiRole.AccentText, p.ClearLabel,
                UiFlags.Label | UiFlags.Right);
            UiDraw.Node(s, at, ix + iw - clear, cy, clear, UiTokens.PairRow, PickedClearCommand, 0, link);
            cy += UiTokens.PairRow + UiTokens.S1;
            // The picture's place: the type's glyph on the type's colour until the game or the mod holds a picture of the place.
            int body = UiDraw.Box(s, at, ix, cy, iw, PickedTile, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(body);
            if (!string.IsNullOrEmpty(p.Picture))
            {
                UiDraw.PictureFace(s, inside, ix, cy, PickedTile, p.Picture);
            }
            else
            {
                int tile = UiDraw.Tile(s, inside, ix, cy, PickedTile, p.Tint, false);
                UiDraw.Icon(s, inside.In(tile), ix + (PickedTile - UiTokens.S8) / 2f, cy + (PickedTile - UiTokens.S8) / 2f, UiTokens.S8, p.Icon, p.Tint, UiFlags.Middle | UiFlags.Centre, true);
            }
            float tx = ix + PickedTile + UiTokens.S4, tw = ix + iw - tx - (p.CanDetails ? UiTokens.IconSmall + UiTokens.S2 : 0f);
            float lh = UiTextRules.LineHeight(UiStyle.Title), sh = UiTextRules.LineHeight(UiStyle.Small);
            int lines = 1 + (string.IsNullOrEmpty(p.Address) ? 0 : 1) + (string.IsNullOrEmpty(p.Distance) ? 0 : 1);
            float top = cy + (PickedTile - lh - (lines - 1) * sh) / 2f;
            UiDraw.Text(s, inside, tx, top, tw, UiStyle.Title, UiRole.Text, p.Name, UiFlags.Truncate | UiFlags.Middle);
            float ty = top + lh;
            if (!string.IsNullOrEmpty(p.Address))
            {
                UiDraw.Text(s, inside, tx, ty, tw, UiStyle.Small, UiRole.Text2, p.Address, UiFlags.Truncate | UiFlags.Middle);
                ty += sh;
            }
            if (!string.IsNullOrEmpty(p.Distance))
            {
                UiDraw.Text(s, inside, tx, ty, tw, UiStyle.Small, UiRole.Text2, p.Distance, UiFlags.Truncate | UiFlags.Middle);
            }
            if (p.CanDetails)
            {
                UiDraw.Icon(s, inside, ix + iw - UiTokens.IconSmall, cy + (PickedTile - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Next, UiRole.AccentText, UiFlags.Middle);
                UiDraw.Node(s, at, ix, cy, iw, PickedTile, PickedMoreCommand, 0, body);
            }
            cy += PickedTile;
            if (p.Chips != null && p.Chips.Length > 0)
            {
                cy += UiTokens.S2;
                float cx = ix;
                for (int i = 0; i < p.Chips.Length; i++)
                {
                    cx += UiDraw.Chip(s, at, cx, cy, p.Chips[i].Label, p.Chips[i].Tone, ix + iw - cx) + UiTokens.S2;
                }
                cy += UiTokens.Badge + UiTokens.S1;
            }
            cy += UiTokens.S3;
            float half = UiDraw.SnapDown((iw - UiTokens.S2) / 2f);
            UiDraw.Button(s, at, ix, cy, half, UiTokens.Control, UiButtonKind.Secondary, p.LocateLabel, UiIcon.Locate, UiAction.Option, keys, PickedLocateCommand, 0);
            UiDraw.Button(s, at, ix + half + UiTokens.S2, cy, iw - half - UiTokens.S2, UiTokens.Control, UiButtonKind.Primary, p.DestinationLabel, UiIcon.Navigation, UiAction.Alt, keys,
                PickedDestinationCommand, 0, !p.CanDestination);
        }
    }
}
