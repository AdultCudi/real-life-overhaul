using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A filter of the city map: its switch, its glyph in its own colour, its name, and whether it can take the camera to its places.</summary>
    public struct UiMapFilter
    {
        public UiIcon Icon;
        public string Name;        // "Subway stations"
        public bool On;
        public bool Locate;        // a locate button that takes the camera to the nearest of them
    }

    /// <summary>
    /// What the city map's side panel shows: the views or filter groups as tinted icons, a search, the filters, the map's
    /// legend, and a picked place as a short block with Set destination. For the game's CityMapFilters and subway station
    /// list, the mod's MapPlaces rows, MapPicker, DistrictView and Build mode's layers.
    /// </summary>
    public sealed class UiMapPanelModel
    {
        public string Title;                               // the view's name
        public UiIcon Icon = UiIcon.MapTrifold;
        public UiIcon[] Views = new UiIcon[0];             // the filter groups or views, two across as an info view's menu
        public UiRole[] ViewTints = new UiRole[0];
        public int View;
        public string SearchPlaceholder;                   // "Search for a place"
        public string Search;                              // what is typed
        public string FiltersTitle;                        // "Show on the map"
        public UiMapFilter[] Filters = new UiMapFilter[0];
        public string LegendTitle;                         // "Map legend"; null for none
        public string ScaleLabel, ScaleLow, ScaleHigh;
        public UiRole[] ScaleSteps;                        // null for the kit's violet ramp
        public bool Picked;                                // a place is picked on the map
        public UiIcon PickedIcon = UiIcon.MapPin;
        public string PickedTitle;                         // "Bloom Café"
        public UiInfoRow[] PickedRows = new UiInfoRow[0];  // up to three: the address, the district, what it is
        public string DestinationLabel;                    // the game's "Set destination"
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// The city map's side panel, the info view extended for the map: the views as tinted icons two across beside a panel
    /// docked at the left (its title strip; a search; the filters as rows of a switch, a glyph, the name and a locate
    /// button; the map's legend on its ramp; and the picked place as a short block on a band with Set destination as the
    /// one main action). The 3D map stays the game's. Commands: <see cref="ViewCommand"/> (a view's index),
    /// <see cref="SearchCommand"/>, <see cref="FilterCommand"/> and <see cref="LocateCommand"/> (a filter's index),
    /// <see cref="DestinationCommand"/>. Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiMapPanel
    {
        public const int ViewCommand = 260, SearchCommand = 261, FilterCommand = 262, LocateCommand = 263, DestinationCommand = 264;
        public const float Width = UiInfoView.Width;   // px: the same panel as an info view's
        private const float Menu = 104f;               // px: the views' menu, two icons across, as an info view's
        private const float Cell = 44f;                // px: a view's icon button
        private const float FilterRow = UiTokens.Control;   // px: the kit's switch stands a control high
        private const float SwitchBox = 52f;                // px: the box the kit's switch hugs, its track and a grid step before it
        private const byte MenuRegion = 1, PanelRegion = 2;

        private static readonly UiRole[] Ramp = { UiRole.Ramp1, UiRole.Ramp2, UiRole.Ramp3, UiRole.Ramp4, UiRole.Ramp5 };

        /// <summary>The panel as a screen of its own over the map: docked at the left, with the status bar and its prompts.</summary>
        public static void Lay(UiScene s, UiMapPanelModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("map panel", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            Draw(s, UiAt.Screen, m, UiTokens.S2, UiTokens.S4, keys);
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        public static float Height(UiMapPanelModel m)
        {
            float h = UiFrame.Strip + UiTokens.S3 + UiTokens.Control + UiTokens.S3;
            if (m.Filters.Length > 0)
            {
                h += UiTokens.S2 + UiTokens.PairRow + m.Filters.Length * FilterRow + UiTokens.S2 + UiTokens.S1;
            }
            if (!string.IsNullOrEmpty(m.LegendTitle))
            {
                h += UiTokens.S3 + UiTokens.PairRow + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body)) + UiTokens.S2
                     + UiDraw.Snap(UiTokens.S2 + UiTokens.S1 + UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S3;
            }
            if (m.Picked)
            {
                h += UiTokens.S1 + PickedHeight(m);
            }
            return UiDraw.Snap(h);
        }

        private static float PickedHeight(UiMapPanelModel m) =>
            UiTokens.S3 + UiTokens.PairRow + Math.Min(3, m.PickedRows.Length) * UiTokens.PairRow + UiTokens.S3 + UiTokens.Control + UiTokens.S4;

        /// <summary>The menu and the panel at a top left (px of the layout); returns the panel's height.</summary>
        public static float Draw(UiScene s, UiAt at, UiMapPanelModel m, float x, float y, IUiKeys keys)
        {
            float px = x;
            if (m.Views.Length > 0)
            {
                ViewsMenu(s, at.InRegion(MenuRegion), m, x, y);
                px = x + Menu + UiTokens.S2;
            }
            float h = Height(m);
            UiAt column = at.InRegion(PanelRegion);
            int panel = UiDraw.Box(s, column, px, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = column.In(panel);
            float cy = UiDraw.TitleStrip(s, on, px, y, Width, m.Icon, m.Title, true);
            float ix = px + UiTokens.S4, iw = Width - UiTokens.S4 * 2f;
            cy += UiTokens.S3;
            UiDraw.Search(s, on, ix, cy, iw, m.SearchPlaceholder, m.Search, null, SearchCommand);
            cy += UiTokens.Control + UiTokens.S3;
            if (m.Filters.Length > 0)
            {
                float bh = UiTokens.S2 + UiTokens.PairRow + m.Filters.Length * FilterRow + UiTokens.S2;
                UiDraw.Band(s, on, px, cy, Width, bh);
                float ry = cy + UiTokens.S2;
                UiDraw.Text(s, on, ix, ry + (UiTokens.PairRow - UiTextRules.LineHeight(UiStyle.SectionHeader)) / 2f, iw, UiStyle.SectionHeader, UiRole.Text, m.FiltersTitle,
                    UiFlags.Label | UiFlags.Truncate | UiFlags.Middle);
                ry += UiTokens.PairRow;
                for (int i = 0; i < m.Filters.Length; i++)
                {
                    Filter(s, on, ref m.Filters[i], i, ix, ry, iw);
                    ry += FilterRow;
                }
                cy += bh + UiTokens.S1;
            }
            if (!string.IsNullOrEmpty(m.LegendTitle))
            {
                cy += UiTokens.S3;
                UiDraw.TextMiddle(s, on, ix, cy, UiTokens.PairRow, iw, UiStyle.SectionHeader, UiRole.Text, m.LegendTitle, UiFlags.Label | UiFlags.Truncate);
                cy += UiTokens.PairRow;
                UiDraw.Text(s, on, ix, cy, iw, UiStyle.Body, UiRole.Text2, m.ScaleLabel, UiFlags.Label | UiFlags.Truncate);
                cy += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body)) + UiTokens.S2;
                cy += UiDraw.Scale(s, on, ix, cy, iw, m.ScaleLow, m.ScaleHigh, m.ScaleSteps ?? Ramp) + UiTokens.S3;
            }
            if (m.Picked)
            {
                Picked(s, on, m, px, y + h - PickedHeight(m), ix, iw, keys);
            }
            Link(s, m);
            return h;
        }

        // The d-pad's way through the panel: from the views' right column to the search, down the filters one by one (a
        // locate beside its switch), on to Set destination, and back up the same way.
        private static void Link(UiScene s, UiMapPanelModel m)
        {
            int search = s.FindNode(SearchCommand, 0);
            if (search < 0)
            {
                return;
            }
            for (int i = 0; i < m.Views.Length; i++)
            {
                int view = s.FindNode(ViewCommand, i);
                if (view >= 0 && (i % 2 == 1 || i == m.Views.Length - 1))
                {
                    s.Nodes[view].Right = search;
                }
            }
            int shown = s.FindNode(ViewCommand, m.View);
            s.Nodes[search].Left = shown;
            int above = search;
            for (int i = 0; i < m.Filters.Length; i++)
            {
                int filter = s.FindNode(FilterCommand, i), locate = s.FindNode(LocateCommand, i);
                if (filter < 0)
                {
                    continue;
                }
                s.Nodes[above].Down = filter;
                s.Nodes[filter].Up = above;
                if (locate >= 0)
                {
                    s.Nodes[filter].Right = locate;
                    s.Nodes[locate].Left = filter;
                    s.Nodes[locate].Up = above;
                }
                above = filter;
            }
            int destination = s.FindNode(DestinationCommand, 0);
            if (destination >= 0)
            {
                s.Nodes[above].Down = destination;
                s.Nodes[destination].Up = above;
            }
        }

        // A filter: the kit's switch that turns it, its glyph, its name, and its locate button at the right.
        private static void Filter(UiScene s, UiAt at, ref UiMapFilter f, int index, float x, float y, float w)
        {
            float lb = UiTokens.MinTarget;
            UiDraw.Switch(s, at, x - UiTokens.S2, y, SwitchBox, f.On, null, FilterCommand, index, false);
            float gx = x - UiTokens.S2 + SwitchBox + UiTokens.S3;
            UiDraw.Icon(s, at, gx, y + (FilterRow - UiTokens.Icon) / 2f, UiTokens.Icon, f.Icon, UiRole.None, UiFlags.Middle);
            float nx = gx + UiTokens.Icon + UiTokens.S2;
            float right = x + w - (f.Locate ? lb + UiTokens.S2 : 0f);
            UiDraw.TextMiddle(s, at, nx, y, FilterRow, right - nx, UiStyle.Body, f.On ? UiRole.Text : UiRole.Text2, f.Name, UiFlags.Label | UiFlags.Truncate);
            if (f.Locate)
            {
                UiDraw.IconButton(s, at, x + w - lb, y + (FilterRow - lb) / 2f, lb, UiIcon.Locate, UiRole.None, LocateCommand, index, false, UiAction.Count, false, true);
            }
        }

        // The picked place on its band at the panel's foot: its glyph and name, its rows, and Set destination.
        private static void Picked(UiScene s, UiAt at, UiMapPanelModel m, float x, float y, float ix, float iw, IUiKeys keys)
        {
            float h = PickedHeight(m);
            UiDraw.Band(s, at, x, y, Width, h);
            float cy = y + UiTokens.S3;
            UiDraw.Icon(s, at, ix, cy + (UiTokens.PairRow - UiTokens.Icon) / 2f, UiTokens.Icon, m.PickedIcon, UiRole.None, UiFlags.Middle);
            UiDraw.TextMiddle(s, at, ix + UiTokens.Icon + UiTokens.S2, cy, UiTokens.PairRow, iw - UiTokens.Icon - UiTokens.S2, UiStyle.BodyStrong, UiRole.Text, m.PickedTitle, UiFlags.Truncate);
            cy += UiTokens.PairRow;
            for (int i = 0; i < Math.Min(3, m.PickedRows.Length); i++)
            {
                cy += UiDraw.InfoRow(s, at, ix, cy, iw, m.PickedRows[i].Label, m.PickedRows[i].Value);
            }
            cy += UiTokens.S3;
            UiDraw.Button(s, at, ix, cy, iw, UiTokens.Control, UiButtonKind.Primary, m.DestinationLabel, UiIcon.Navigation, UiAction.Alt, keys, DestinationCommand, 0);
        }

        // The views as tinted icons two across, the shown one filled cyan, as an info view's menu.
        private static void ViewsMenu(UiScene s, UiAt at, UiMapPanelModel m, float x, float y)
        {
            int rows = (m.Views.Length + 1) / 2;
            float h = rows * Cell + (rows - 1) * UiTokens.S1 + UiTokens.S2 * 2f;
            int menu = UiDraw.Box(s, at, x, y, Menu, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(menu);
            for (int i = 0; i < m.Views.Length; i++)
            {
                float cx = x + UiTokens.S2 + (i % 2) * (Cell + UiTokens.S1), cy = y + UiTokens.S2 + (i / 2) * (Cell + UiTokens.S1);
                UiRole tint = i < m.ViewTints.Length ? m.ViewTints[i] : UiRole.None;
                int node = UiDraw.IconButton(s, on, cx, cy, Cell, m.Views[i], tint, ViewCommand, i, i == m.View);
                if (i == m.View)
                {
                    s.FocusFirst = node;
                }
            }
        }
    }
}
