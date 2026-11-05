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
        public bool HideAllOff;    // no layer is on, so there is nothing to hide
        public MapLayer[] Layers = new MapLayer[0];
    }

    /// <summary>A label over a place of the map: what the pointer rests on in a panel (the unit next door), shown where that place stands on the map.</summary>
    public struct MapLabel
    {
        public UiIcon Icon;
        public string Title;       // "For rent next door"
        public string Figure;      // "$96 per m2 a day", or null
        public float X, Y;         // px of the layout: the place on the screen
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
        public const float LayersWidth = 320f;
        private const float OptionWidth = 116f, LeastOption = 88f;
        private const float StripHeight = 64f;
        private const float LayerRow = 48f;   // two lines of a longer translation fit a row
        private static readonly byte RightRegion = 3, TopRegion = 4;

        // ------------------------------------------------------------------------------------------------- the route strip

        /// <summary>The strip's width for its options and Go, each option as wide as its words need.</summary>
        public static float RouteWidth(MapRouteModel m) => UiTokens.S3 * 2f + OptionsWidth(m) + UiTokens.S4 + GoWidth(m);

        private static float GoWidth(MapRouteModel m) => UiDraw.ButtonWidth(UiButtonKind.Primary, m.GoLabel, UiIcon.None, UiAction.Alt, UiDefaultKeys.Keyboard);

        // An option's width: the least one, or what its minutes and its words (a longer translation, a fare) need beside the glyph, so no word is cut short.
        private static float OptionWidthOf(MapRouteOption o)
        {
            float words = Math.Max(UiTextRules.Width(o.Minutes, UiStyle.BodyStrong), UiTextRules.Width(o.Sub, UiStyle.Small));
            return Math.Max(OptionWidth, UiDraw.Snap(UiTokens.S2 + UiTokens.Icon + UiTokens.S2 + words + UiTokens.S2 + UiTokens.S1));
        }

        private static float OptionsWidth(MapRouteModel m)
        {
            float sum = Math.Max(0, m.Options.Length - 1) * UiTokens.S1;
            for (int i = 0; i < m.Options.Length; i++)
            {
                sum += OptionWidthOf(m.Options[i]);
            }
            return sum;
        }

        /// <summary>
        /// The route strip at a place (px of the layout): its options as tiles, the chosen one cyan, and Go. Returns the strip's right edge. <paramref name="room"/> is the width
        /// it may take: where its words need more, the options give up room evenly down to a least width, and a word is only cut short past that.
        /// </summary>
        public static float Route(UiScene s, UiAt at, MapRouteModel m, float x, float y, float room, IUiKeys keys)
        {
            UiAt top = at.InRegion(TopRegion);
            float natural = OptionsWidth(m), spare = room - (RouteWidth(m) - natural);
            float squeeze = natural > spare && natural > 0f ? Math.Max(0.7f, spare / natural) : 1f;
            float w = RouteWidth(m) - natural;
            float[] widths = new float[m.Options.Length];
            for (int i = 0; i < widths.Length; i++)
            {
                widths[i] = squeeze < 1f ? Math.Max(LeastOption, UiDraw.Snap(OptionWidthOf(m.Options[i]) * squeeze)) : OptionWidthOf(m.Options[i]);
                w += widths[i] + (i > 0 ? UiTokens.S1 : 0f);
            }
            int panel = UiDraw.Box(s, top, x, y, w, StripHeight, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = top.In(panel);
            float cx = x + UiTokens.S3;
            for (int i = 0; i < m.Options.Length; i++)
            {
                MapRouteOption o = m.Options[i];
                bool chosen = i == m.Chosen;
                float ow = widths[i];
                int face = UiDraw.Box(s, on, cx, y + UiTokens.S2, ow, StripHeight - UiTokens.S2 * 2f, chosen ? UiRole.Accent : UiRole.None, UiRole.None, UiTokens.RadiusLarge,
                    chosen ? UiFlags.Selected : UiFlags.None);
                UiAt inside = on.In(face);
                UiRole ink = chosen ? UiRole.OnAccent : o.Off ? UiRole.Muted : UiRole.Text;
                UiRole sub = chosen ? UiRole.OnAccent : UiRole.Text2;
                UiDraw.Icon(s, inside, cx + UiTokens.S2, y + UiTokens.S2 + (StripHeight - UiTokens.S2 * 2f - UiTokens.Icon) / 2f, UiTokens.Icon, o.Icon, chosen ? UiRole.OnAccent : UiRole.None,
                    UiFlags.Middle, !chosen);
                float tx = cx + UiTokens.S2 + UiTokens.Icon + UiTokens.S2, tw = ow - (tx - cx) - UiTokens.S2;
                float lh = UiTextRules.LineHeight(UiStyle.BodyStrong), sh = UiTextRules.LineHeight(UiStyle.Small);
                float ty = y + (StripHeight - lh - sh) / 2f;
                UiDraw.Text(s, inside, tx, ty, tw, UiStyle.BodyStrong, ink, o.Minutes, UiFlags.Label | UiFlags.Truncate | UiFlags.Middle);
                UiDraw.Text(s, inside, tx, ty + lh, tw, UiStyle.Small, sub, o.Sub, UiFlags.Label | UiFlags.Truncate | UiFlags.Middle);
                UiDraw.Node(s, on, cx, y + UiTokens.S2, ow, StripHeight - UiTokens.S2 * 2f, OptionCommand, i, face, o.Off);
                cx += ow + UiTokens.S1;
            }
            float gw = GoWidth(m);
            UiDraw.Button(s, on, x + w - UiTokens.S3 - gw, y + (StripHeight - UiTokens.Control) / 2f, gw, UiTokens.Control, UiButtonKind.Primary, m.GoLabel, UiIcon.None, UiAction.Alt, keys,
                GoCommand, 0, !m.CanGo);
            return x + w;
        }

        // ------------------------------------------------------------------------------------------------- the map's buttons

        /// <summary>The buttons' column height.</summary>
        public static float ButtonsHeight => 5f * Button + 4f * UiTokens.S2;

        /// <summary>The map's buttons down the right edge, ending a gap over the lane of the keys' chips: north, centre on me, zoom in, zoom out, tilt.</summary>
        public static void Buttons(UiScene s, UiAt at, MapButtonsModel m, UiFrame frame)
        {
            UiAt right = at.InRegion(RightRegion);
            float x = frame.Width - UiTokens.S4 - Button, y = frame.BandY - UiDraw.KeyChipHeight - UiTokens.S4 - ButtonsHeight;
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

        /// <summary>The size of the label on the route's line: the glyph of the way, the minutes over the way still to go.</summary>
        public static void RouteTagSize(string minutes, string distance, out float w, out float h)
        {
            bool two = !string.IsNullOrEmpty(distance);
            float words = Math.Max(UiTextRules.Width(minutes, UiStyle.BodyStrong), two ? UiTextRules.Width(distance, UiStyle.Small) : 0f);
            w = UiDraw.Snap(UiTokens.S3 + UiTokens.Icon + UiTokens.S2 + words + UiTokens.S3 + UiTokens.S1);
            h = UiDraw.Snap(UiTokens.S2 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.BodyStrong)) + (two ? UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)) : 0f) + UiTokens.S2);
        }

        /// <summary>The label on the route's line, drawn once into the size <see cref="RouteTagSize"/> gave: a walker or a car, "14 min" over "1.2 km". It takes no press.</summary>
        public static void RouteTag(UiScene s, UiAt at, float x, float y, float w, float h, bool onFoot, string minutes, string distance)
        {
            int box = UiDraw.Box(s, at, x, y, w, h, UiRole.Panel, UiRole.None, UiTokens.RadiusLarge);
            UiAt on = at.In(box);
            UiDraw.Icon(s, on, x + UiTokens.S3, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, onFoot ? UiIcon.Walk : UiIcon.Car, UiRole.None, UiFlags.Middle);
            float tx = x + UiTokens.S3 + UiTokens.Icon + UiTokens.S2, tw = w - (tx - x) - UiTokens.S3;
            float lh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.BodyStrong));
            UiDraw.Text(s, on, tx, y + UiTokens.S2, tw, UiStyle.BodyStrong, UiRole.Text, minutes, UiFlags.Label | UiFlags.Truncate);
            if (!string.IsNullOrEmpty(distance))
            {
                UiDraw.Text(s, on, tx, y + UiTokens.S2 + lh, tw, UiStyle.Small, UiRole.Text2, distance, UiFlags.Label | UiFlags.Truncate);
            }
        }

        /// <summary>
        /// The label of a place over the map: a small slate piece above the place's point, its glyph and words, kept off the dock and the map's buttons and inside the screen.
        /// It takes no press.
        /// </summary>
        public static void Label(UiScene s, UiAt at, MapLabel m, UiFrame frame, float dockRight)
        {
            float lh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SmallStrong)), fh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            bool figure = !string.IsNullOrEmpty(m.Figure);
            float words = Math.Max(UiTextRules.Width(m.Title, UiStyle.SmallStrong), figure ? UiTextRules.Width(m.Figure, UiStyle.Small) : 0f);
            float w = UiDraw.Snap(UiTokens.S3 + UiTokens.Icon + UiTokens.S2 + words + UiTokens.S3 + UiTokens.S1);
            float h = UiDraw.Snap(UiTokens.S2 + lh + (figure ? fh : 0f) + UiTokens.S2);
            float left = dockRight + UiTokens.S2, right = frame.Width - UiTokens.S4 * 2f - Button - w;
            float x = UiDraw.Snap(Math.Max(left, Math.Min(right, m.X - w / 2f)));
            float y = UiDraw.Snap(Math.Max(UiTokens.S4, Math.Min(frame.BandY - h - UiTokens.S4, m.Y - h - UiTokens.S4)));
            int box = UiDraw.Box(s, at, x, y, w, h, UiRole.Panel, UiRole.None, UiTokens.RadiusLarge);
            UiAt on = at.In(box);
            UiDraw.Icon(s, on, x + UiTokens.S3, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, m.Icon, UiRole.None, UiFlags.Middle);
            float tx = x + UiTokens.S3 + UiTokens.Icon + UiTokens.S2, top = y + UiTokens.S2;
            UiDraw.Text(s, on, tx, top, words + UiTokens.S1, UiStyle.SmallStrong, UiRole.Text, m.Title, UiFlags.Label | UiFlags.Truncate);
            if (figure)
            {
                UiDraw.Text(s, on, tx, top + lh, words + UiTokens.S1, UiStyle.Small, UiRole.Text2, m.Figure, UiFlags.Truncate);
            }
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
            UiDraw.TextMiddle(s, on, x + UiTokens.S4 + UiTokens.Icon + UiTokens.S2, y, UiFrame.Strip, LayersWidth - hw - UiTokens.S4 * 2f - UiTokens.Icon,
                UiStyle.SectionTitle, UiRole.AccentText, m.Title,
                UiFlags.Label | UiFlags.Truncate);
            int link = UiDraw.Box(s, on, x + LayersWidth - hw - UiTokens.S2, y + (UiFrame.Strip - UiTokens.PairRow) / 2f, hw, UiTokens.PairRow, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiDraw.TextMiddle(s, on.In(link), x + LayersWidth - hw - UiTokens.S2, y + (UiFrame.Strip - UiTokens.PairRow) / 2f, UiTokens.PairRow, hw - UiTokens.S3, UiStyle.SmallStrong,
                m.HideAllOff ? UiRole.Muted : UiRole.AccentText, m.HideAllLabel, UiFlags.Label | UiFlags.Right);
            UiDraw.Node(s, on, x + LayersWidth - hw - UiTokens.S2, y + (UiFrame.Strip - UiTokens.PairRow) / 2f, hw, UiTokens.PairRow, HideAllCommand, 0, link, m.HideAllOff);
            UiDraw.Box(s, on, x, y + UiFrame.Strip - UiTokens.Hairline, LayersWidth, UiTokens.Hairline, UiRole.Rule, UiRole.None, 0f, UiFlags.Decor | UiFlags.Chrome);
            float cy = y + UiFrame.Strip + UiTokens.S2;
            for (int i = 0; i < m.Layers.Length; i++)
            {
                MapLayer l = m.Layers[i];
                int tile = UiDraw.Tile(s, on, x + UiTokens.S4, cy + (LayerRow - 32f) / 2f, 32f, l.Tint, false);
                UiDraw.Icon(s, on.In(tile), x + UiTokens.S4 + 6f, cy + (LayerRow - 32f) / 2f + 6f, UiTokens.Icon, l.Icon, l.Tint, UiFlags.Middle | UiFlags.Centre, true);
                float nx = x + UiTokens.S4 + 32f + UiTokens.S3, sw = 52f;
                float nw = x + LayersWidth - UiTokens.S4 - sw - nx - UiTokens.S2;
                int lines = Math.Max(1, UiTextRules.Lines(l.Name, UiStyle.Body, nw, 2, out float widest));
                float th = UiTextRules.WrappedHeight(l.Name, UiStyle.Body, nw, lines);
                UiDraw.Text(s, on, nx, cy + (LayerRow - th) / 2f, nw, UiStyle.Body, l.On && !l.Off ? UiRole.Text : UiRole.Text2, l.Name, UiFlags.Label | UiFlags.Wrap | UiFlags.Middle, lines);
                UiDraw.Switch(s, on, x + LayersWidth - UiTokens.S4 - sw, cy + (LayerRow - UiTokens.Control) / 2f, sw, l.On, null, LayerCommand, i, l.Off);
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
        public MapLabel Label;               // over a place of the map while the pointer rests on its row in a panel
        public bool HasLabel;
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
                // Up to the Layers panel's edge when that is there, else the screen's.
                float stripX = dockRight + UiTokens.S6, limit = frame.Width - UiTokens.S4 - (m.Layers != null ? UiMapChrome.LayersWidth + UiTokens.S4 : 0f);
                UiMapChrome.Route(s, UiAt.Screen, m.Route, stripX, UiTokens.S4, limit - stripX, keys);
            }
            if (m.Layers != null)
            {
                UiMapChrome.Layers(s, UiAt.Screen, m.Layers, frame);
            }
            if (m.Buttons != null)
            {
                UiMapChrome.Buttons(s, UiAt.Screen, m.Buttons, frame);
            }
            if (m.HasLabel)
            {
                UiMapChrome.Label(s, UiAt.Screen, m.Label, frame, dockRight);
            }
            for (int i = 0; prompts != null && i < count; i++)
            {
                s.AddPrompt(prompts[i].Action, prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }
    }
}
