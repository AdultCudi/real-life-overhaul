using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>One way to get to the picked place: its glyph, the minutes it takes, and what it costs.</summary>
    public struct MapRouteOption
    {
        public UiIcon Icon;
        public string Minutes;     // "14 min"
        public string Sub;         // "Walk", "Taxi, $14"
        public bool Off;           // not offered now (no car, no subway line near)
    }

    /// <summary>The strip over the map that compares the ways to the picked place, with Go.</summary>
    public sealed class MapRouteModel
    {
        public MapRouteOption[] Options = new MapRouteOption[0];
        public int Chosen = -1;
        public string GoLabel;
        public bool CanGo;
    }

    /// <summary>A layer of the map's small panel: a key line or a group, its glyph and name, and whether it shows.</summary>
    public struct MapLayer
    {
        public UiIcon Icon;
        public UiRole Tint;
        public string Name;
        public bool On;
        public bool Off;           // the map cannot show it now
    }

    /// <summary>The map's small Layers panel, at its right while the place card has taken the dock.</summary>
    public sealed class MapLayersModel
    {
        public string Title, HideAllLabel;
        public MapLayer[] Layers = new MapLayer[0];
    }

    /// <summary>The map's buttons at its right edge: north, centre on me, zoom in and out, and the tilt.</summary>
    public sealed class MapButtonsModel
    {
        public string North;       // "N"
        public string Tilt;        // "3D" while the map stands tilted, "2D" while it is flat
        public bool Tilted;
        public bool CanZoomIn = true, CanZoomOut = true;
    }

    /// <summary>
    /// The pieces that stand over the city map beside its dock: the route strip over the top, the map's buttons at the right edge above the status bar,
    /// and the Layers panel at the top right. Commands, the 5040 block: <see cref="OptionCommand"/>, <see cref="GoCommand"/>, <see cref="NorthCommand"/>,
    /// <see cref="CentreCommand"/>, <see cref="ZoomInCommand"/>, <see cref="ZoomOutCommand"/>, <see cref="TiltCommand"/>, <see cref="LayerCommand"/>,
    /// <see cref="HideAllCommand"/>. Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiMapChrome
    {
        public const int OptionCommand = 5040, GoCommand = 5041, NorthCommand = 5042, CentreCommand = 5043, ZoomInCommand = 5044, ZoomOutCommand = 5045, TiltCommand = 5046,
            LayerCommand = 5047, HideAllCommand = 5048;
        public const float Button = UiTokens.Control;      // px: a map button
        public const float LayersWidth = 288f;
        private const float OptionWidth = 116f;
        private const float StripHeight = 64f;
        private const float LayerRow = UiTokens.Control;
        private static readonly byte RightRegion = 3, TopRegion = 4;

        // ------------------------------------------------------------------------------------------------- the route strip

        /// <summary>The strip's width for its options and Go.</summary>
        public static float RouteWidth(MapRouteModel m) => UiTokens.S3 * 2f + m.Options.Length * (OptionWidth + UiTokens.S1) - UiTokens.S1 + UiTokens.S4 + GoWidth(m);

        private static float GoWidth(MapRouteModel m) => UiDraw.ButtonWidth(UiButtonKind.Primary, m.GoLabel, UiIcon.None, UiAction.Alt, UiDefaultKeys.Keyboard);

        /// <summary>The route strip at a place (px of the layout): its options as tiles, the chosen one cyan, and Go. Returns the strip's right edge.</summary>
        public static float Route(UiScene s, UiAt at, MapRouteModel m, float x, float y, IUiKeys keys)
        {
            UiAt top = at.InRegion(TopRegion);
            float w = RouteWidth(m);
            int panel = UiDraw.Box(s, top, x, y, w, StripHeight, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = top.In(panel);
            float cx = x + UiTokens.S3;
            for (int i = 0; i < m.Options.Length; i++)
            {
                MapRouteOption o = m.Options[i];
                bool chosen = i == m.Chosen;
                int face = UiDraw.Box(s, on, cx, y + UiTokens.S2, OptionWidth, StripHeight - UiTokens.S2 * 2f, chosen ? UiRole.Accent : UiRole.None, UiRole.None, UiTokens.RadiusLarge,
                    chosen ? UiFlags.Selected : UiFlags.None);
                UiAt inside = on.In(face);
                UiRole ink = chosen ? UiRole.OnAccent : o.Off ? UiRole.Muted : UiRole.Text;
                UiRole sub = chosen ? UiRole.OnAccent : UiRole.Text2;
                UiDraw.Icon(s, inside, cx + UiTokens.S2, y + UiTokens.S2 + (StripHeight - UiTokens.S2 * 2f - UiTokens.Icon) / 2f, UiTokens.Icon, o.Icon, chosen ? UiRole.OnAccent : UiRole.None,
                    UiFlags.Middle, !chosen);
                float tx = cx + UiTokens.S2 + UiTokens.Icon + UiTokens.S2, tw = OptionWidth - (tx - cx) - UiTokens.S2;
                float lh = UiTextRules.LineHeight(UiStyle.BodyStrong), sh = UiTextRules.LineHeight(UiStyle.Small);
                float ty = y + (StripHeight - lh - sh) / 2f;
                UiDraw.Text(s, inside, tx, ty, tw, UiStyle.BodyStrong, ink, o.Minutes, UiFlags.Truncate | UiFlags.Middle);
                UiDraw.Text(s, inside, tx, ty + lh, tw, UiStyle.Small, sub, o.Sub, UiFlags.Truncate | UiFlags.Middle);
                UiDraw.Node(s, on, cx, y + UiTokens.S2, OptionWidth, StripHeight - UiTokens.S2 * 2f, OptionCommand, i, face, o.Off);
                cx += OptionWidth + UiTokens.S1;
            }
            float gw = GoWidth(m);
            UiDraw.Button(s, on, x + w - UiTokens.S3 - gw, y + (StripHeight - UiTokens.Control) / 2f, gw, UiTokens.Control, UiButtonKind.Primary, m.GoLabel, UiIcon.None, UiAction.Alt, keys,
                GoCommand, 0, !m.CanGo);
            return x + w;
        }

        // ------------------------------------------------------------------------------------------------- the map's buttons

        /// <summary>The buttons' column height.</summary>
        public static float ButtonsHeight => 5f * Button + 4f * UiTokens.S2;

        /// <summary>The map's buttons down the right edge, ending a gap over the status bar: north, centre on me, zoom in, zoom out, tilt.</summary>
        public static void Buttons(UiScene s, UiAt at, MapButtonsModel m, UiFrame frame)
        {
            UiAt right = at.InRegion(RightRegion);
            float x = frame.Width - UiTokens.S4 - Button, y = frame.BandY - UiTokens.S4 - ButtonsHeight;
            TextTile(s, right, x, y, m.North, false, NorthCommand);
            y += Button + UiTokens.S2;
            UiDraw.IconButton(s, right, x, y, Button, UiIcon.Locate, UiRole.None, CentreCommand, 0);
            y += Button + UiTokens.S2;
            UiDraw.IconButton(s, right, x, y, Button, UiIcon.Plus, UiRole.Text, ZoomInCommand, 0);
            s.Nodes[s.NodeCount - 1].Disabled = !m.CanZoomIn;
            y += Button + UiTokens.S2;
            UiDraw.IconButton(s, right, x, y, Button, UiIcon.Minus, UiRole.Text, ZoomOutCommand, 0);
            s.Nodes[s.NodeCount - 1].Disabled = !m.CanZoomOut;
            y += Button + UiTokens.S2;
            TextTile(s, right, x, y, m.Tilt, m.Tilted, TiltCommand);
        }

        // A square tile with words in it (N, 3D), as an icon button is one with a glyph; the chosen one is the cyan.
        private static void TextTile(UiScene s, UiAt at, float x, float y, string words, bool chosen, int command)
        {
            int face = UiDraw.Tile(s, at, x, y, Button, UiRole.None, chosen);
            UiDraw.TextMiddle(s, at.In(face), x, y, Button, Button, UiStyle.SmallStrong, chosen ? UiRole.OnAccent : UiRole.AccentText, words, UiFlags.Centre | UiFlags.Label);
            UiDraw.Node(s, at, x, y, Button, Button, command, 0, face);
        }

        // ------------------------------------------------------------------------------------------------- the layers

        /// <summary>The Layers panel's height.</summary>
        public static float LayersHeight(MapLayersModel m) => UiFrame.Strip + UiTokens.S2 + m.Layers.Length * LayerRow + UiTokens.S2;

        /// <summary>The Layers panel at the top right: a switch to each layer and Hide all.</summary>
        public static void Layers(UiScene s, UiAt at, MapLayersModel m, UiFrame frame)
        {
            UiAt right = at.InRegion(RightRegion);
            float x = frame.Width - UiTokens.S4 - LayersWidth, y = UiTokens.S4;
            float h = LayersHeight(m);
            int panel = UiDraw.Box(s, right, x, y, LayersWidth, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = right.In(panel);
            float hw = UiDraw.Snap(UiTextRules.Width(m.HideAllLabel, UiStyle.SmallStrong) + UiTokens.S3 * 2f);
            UiDraw.Icon(s, on, x + UiTokens.S4, y + (UiFrame.Strip - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Grid, UiRole.None, UiFlags.Middle);
            UiDraw.TextMiddle(s, on, x + UiTokens.S4 + UiTokens.Icon + UiTokens.S2, y, UiFrame.Strip, LayersWidth - hw - UiTokens.S4 * 2f - UiTokens.Icon, UiStyle.SectionTitle, UiRole.AccentText, m.Title,
                UiFlags.Label | UiFlags.Truncate);
            int link = UiDraw.Box(s, on, x + LayersWidth - hw - UiTokens.S2, y + (UiFrame.Strip - UiTokens.PairRow) / 2f, hw, UiTokens.PairRow, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiDraw.TextMiddle(s, on.In(link), x + LayersWidth - hw - UiTokens.S2, y + (UiFrame.Strip - UiTokens.PairRow) / 2f, UiTokens.PairRow, hw - UiTokens.S3, UiStyle.SmallStrong,
                UiRole.AccentText, m.HideAllLabel, UiFlags.Label | UiFlags.Right);
            UiDraw.Node(s, on, x + LayersWidth - hw - UiTokens.S2, y + (UiFrame.Strip - UiTokens.PairRow) / 2f, hw, UiTokens.PairRow, HideAllCommand, 0, link);
            UiDraw.Box(s, on, x, y + UiFrame.Strip - UiTokens.Hairline, LayersWidth, UiTokens.Hairline, UiRole.Rule, UiRole.None, 0f, UiFlags.Decor | UiFlags.Chrome);
            float cy = y + UiFrame.Strip + UiTokens.S2;
            for (int i = 0; i < m.Layers.Length; i++)
            {
                MapLayer l = m.Layers[i];
                int tile = UiDraw.Tile(s, on, x + UiTokens.S4, cy + (LayerRow - 32f) / 2f, 32f, l.Tint, false);
                UiDraw.Icon(s, on.In(tile), x + UiTokens.S4 + 6f, cy + (LayerRow - 32f) / 2f + 6f, UiTokens.Icon, l.Icon, l.Tint, UiFlags.Middle | UiFlags.Centre, true);
                float nx = x + UiTokens.S4 + 32f + UiTokens.S3, sw = 52f;
                UiDraw.TextMiddle(s, on, nx, cy, LayerRow, x + LayersWidth - UiTokens.S4 - sw - nx - UiTokens.S2, UiStyle.Body, l.On && !l.Off ? UiRole.Text : UiRole.Text2, l.Name, UiFlags.Label | UiFlags.Truncate);
                UiDraw.Switch(s, on, x + LayersWidth - UiTokens.S4 - sw, cy, sw, l.On, null, LayerCommand, i, l.Off);
                cy += LayerRow;
            }
        }
    }

    /// <summary>The whole of what stands over the city map as one scene: its dock, the route strip, the buttons and the Layers panel.</summary>
    public sealed class MapScreenModel
    {
        public MapPanelModel Filters;        // the dock's page when the filters, the subway or the picker are up
        public MapPlaceModel Place;          // or the place card
        public MapDistrictModel District;    // or the district view
        public MapRouteModel Route;          // over the top while Directions is chosen, or null
        public MapLayersModel Layers;        // while the place card or the district view has the dock, or null
        public MapButtonsModel Buttons;      // always while the map is open, or null
    }

    /// <summary>The city map's panels laid out as one scene, so the keys and the pad walk one focus graph and the preview draws what the host will.</summary>
    public static class UiMap
    {
        /// <summary>The scene for a frame: the dock page asked for, then the strip, the layers and the buttons, and the keys' prompts.</summary>
        public static void Lay(UiScene s, MapScreenModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("city map", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            float dockRight = UiTokens.S2;
            UiPrompt[] prompts = null;
            int count = 0;
            if (m.Place != null)
            {
                dockRight = UiMapPlace.Draw(s, UiAt.Screen, m.Place, frame, UiTokens.S2, UiTokens.S4, keys);
                prompts = m.Place.Prompts;
                count = m.Place.PromptCount;
            }
            else if (m.District != null)
            {
                dockRight = UiMapDistrict.Draw(s, UiAt.Screen, m.District, frame, UiTokens.S2, UiTokens.S4, keys);
                prompts = m.District.Prompts;
                count = m.District.PromptCount;
            }
            else if (m.Filters != null)
            {
                dockRight = UiMapFilters.Draw(s, UiAt.Screen, m.Filters, frame, UiTokens.S2, UiTokens.S4, keys);
                prompts = m.Filters.Prompts;
                count = m.Filters.PromptCount;
            }
            if (m.Route != null && m.Route.Options.Length > 0)
            {
                UiMapChrome.Route(s, UiAt.Screen, m.Route, dockRight + UiTokens.S6, UiTokens.S4, keys);
            }
            if (m.Layers != null)
            {
                UiMapChrome.Layers(s, UiAt.Screen, m.Layers, frame);
            }
            if (m.Buttons != null)
            {
                UiMapChrome.Buttons(s, UiAt.Screen, m.Buttons, frame);
            }
            for (int i = 0; prompts != null && i < count; i++)
            {
                s.AddPrompt(prompts[i].Action, prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }
    }
}
