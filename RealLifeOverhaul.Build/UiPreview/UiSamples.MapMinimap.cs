using RealLifeOverhaul.Nav;
using RealLifeOverhaul.Ui;

internal static partial class UiSamples
{
    /// <summary>The minimap's states the previews and the checks draw: by day and by night, tilted and flat, and with no destination.</summary>
    public static readonly string[] MapMinimapStates = { "route_tilt", "route_flat", "route_long", "no_route" };

    /// <summary>
    /// The HUD with an objective, two notice cards, the keys' prompts and a world prompt all up at once, and the minimap's card where the mod's placing
    /// rules put it over the kit's bar, drawn as the card is built: its headline with the place and its address at the left and the minutes at the right, its view
    /// (the real 2.5D picture of the owner's footprints, the district's strip, the compass, the 2D and 3D button, the scale bar) and the GPS's two lines. The
    /// game's card is a copy of its task panel in the game's lettering, so this is its layout in the kit's, not its pixels.
    /// </summary>
    public static void MapMinimapScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, bool tall, string state)
    {
        bool route = state != "no_route", tilted = state != "route_flat", lng = state == "route_long";
        UiObjectivesModel goals = PiecesGoals(t, false);
        if (!tall)
        {
            goals.Groups = new[] { goals.Groups[0], goals.Groups[1] };
        }
        var m = new UiHudModel
        {
            Objectives = goals,
            Notices = PiecesNotes(t, false),
            NoticeCount = 2,
            Prompt = HudDoor(t, k),
            PointX = 1100f,
            PointY = 760f,
            Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Pick1, Label = t("input_key_throttle", "Throttle") }, new UiPrompt { Action = UiAction.Pick2, Label = t("input_key_brakes", "Brakes") },
                new UiPrompt { Action = UiAction.Pick3, Label = t("input_key_handbrake", "Handbrake") }, new UiPrompt { Action = UiAction.Back, Label = t("input_key_menu", "Menu") },
            },
            PromptCount = 4,
        };
        float scale = UiTokens.CanvasPerPx, canvasH = f.Height * scale;
        var bare = new UiScene();
        UiStatusModel bar = PiecesBar(false);
        Hud(bare, f, dark, k, t, m, bar);
        NavSpace space = NavSpace.Free(canvasH);
        space.BarTop = f.Band * scale;
        UiHud.LeftColumn(m, false, out float cx, out float cy, out float cw, out float ch);
        space.ColumnRight = cw > 0f ? (cx + cw) * scale : 0f;
        space.HasColumn = cw > 0f;
        space.ColumnBottom = canvasH - (cy + ch) * scale;
        if (UiHud.Chips(bare, m, f, k, out float kx, out float ky, out float kw, out float kh))
        {
            space.ChipsLeft = kx * scale;
            space.ChipsTop = canvasH - ky * scale;
        }
        float width = NavPlaceRules.Width(100);
        float frame = NavPlaceRules.Frame(route);
        NavPlaceRules.Place(space, width, NavPlaceRules.Wanted(width, true), frame, false, out float x, out float bottom, out float map, out _);
        float high = bottom + frame + map;
        m.AvoidX = (float)System.Math.Floor(x / scale);
        m.AvoidW = (float)System.Math.Ceiling(width / scale);
        m.AvoidY = (float)System.Math.Floor(f.Height - high / scale);
        m.AvoidH = (float)System.Math.Ceiling((high - bottom) / scale);
        Hud(s, f, dark, k, t, m, bar);
        Card(s, t, m.AvoidX, m.AvoidY, m.AvoidW, m.AvoidH, route, tilted, lng);
    }

    // The card: slate panel, headline, view, lines. All in px of the layout, from the card's rectangle.
    private static void Card(UiScene s, UiWords t, float x, float y, float w, float h, bool route, bool tilted, bool lng)
    {
        int card = UiDraw.Box(s, UiAt.Screen, x, y, w, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
        UiAt on = UiAt.Screen.In(card);
        float pad = UiTokens.S3, head = NavPlaceRules.Head / UiTokens.CanvasPerPx, side = NavPlaceRules.ViewSide / UiTokens.CanvasPerPx;
        float foot = NavPlaceRules.Foot / UiTokens.CanvasPerPx, lines = route ? (NavPlaceRules.LineHeight * 2f + NavPlaceRules.LinesPad) / UiTokens.CanvasPerPx : 0f;
        float lh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)), sh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
        if (route)
        {
            string name = lng ? "Speedy Bites, the all day diner on Fifth" : "Speedy Bites";
            string minutes = t("reallife_nav_minutes", "{minutes} min").Replace("{minutes}", "4");
            float rw = UiDraw.Snap(UiTextRules.Width(minutes, UiStyle.BodyStrong) + UiTokens.S2);
            UiDraw.Text(s, on, x + pad, y + (head - lh - sh) / 2f, w - pad * 2f - rw - UiTokens.S3, UiStyle.Title, UiRole.Text, name, UiFlags.Truncate | UiFlags.Middle);
            UiDraw.Text(s, on, x + pad, y + (head - lh - sh) / 2f + lh, w - pad * 2f - rw - UiTokens.S3, UiStyle.Small, UiRole.Text2, "214 5th Avenue", UiFlags.Truncate | UiFlags.Middle);
            UiDraw.Text(s, on, x + w - pad - rw, y + (head - lh - sh) / 2f, rw, UiStyle.BodyStrong, UiRole.Text, minutes, UiFlags.Right | UiFlags.Truncate | UiFlags.Middle);
            UiDraw.Text(s, on, x + w - pad - rw, y + (head - lh - sh) / 2f + lh, rw, UiStyle.Small, UiRole.Text2, "1.2 km", UiFlags.Right | UiFlags.Truncate | UiFlags.Middle);
        }
        else
        {
            UiDraw.Icon(s, on, x + pad, y + (head - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.MapTrifold, UiRole.None, UiFlags.Middle);
            UiDraw.TextMiddle(s, on, x + pad + UiTokens.Icon + UiTokens.S2, y, head, w - pad * 2f, UiStyle.Title, UiRole.Text, t("reallife_nav_title", "Map"), UiFlags.Truncate);
        }
        float vx = x + side, vy = y + head, vw = w - side * 2f, vh = h - head - lines - foot;
        var item = new UiItem
        {
            Kind = UiKind.Picture, Flags = UiFlags.Cover | UiFlags.Middle | UiFlags.Centre, X = vx, Y = vy, W = vw, H = vh, Text = tilted ? "map5:real_mini_tilt" : "map5:real_mini_flat",
            Value = 1f, Clip = on.Clip, Node = -1, Layer = on.Layer, Order = on.Order, Parent = on.Parent, Alpha = 1f
        };
        s.Add(ref item);
        // the strip, the compass and the 2D and 3D button over the view
        float sx = vx + UiTokens.S3, sy = vy + UiTokens.S3;
        string strip = "MIDTOWN   5th Avenue";
        float sw = UiDraw.Snap(UiTextRules.Width(strip, UiStyle.SmallStrong) + UiTokens.S4 * 2f);
        int plate = UiDraw.Box(s, on, sx, sy, sw, UiTokens.Control - UiTokens.S2, UiRole.Panel, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
        UiDraw.TextMiddle(s, on.In(plate), sx + UiTokens.S4, sy, UiTokens.Control - UiTokens.S2, sw - UiTokens.S4, UiStyle.SmallStrong, UiRole.AccentText, strip, UiFlags.Truncate);
        float bx = vx + vw - UiTokens.S3 - 32f;
        int north = UiDraw.Box(s, on, bx, sy, 32f, 32f, UiRole.Panel, UiRole.None, 16f, UiFlags.Circle | UiFlags.Decor);
        UiDraw.TextMiddle(s, on.In(north), bx, sy, 32f, 32f, UiStyle.SmallStrong, UiRole.AccentText, "N", UiFlags.Centre);
        int tilt = UiDraw.Box(s, on, bx, sy + 32f + UiTokens.S2, 32f, 32f, UiRole.Panel, UiRole.None, 16f, UiFlags.Circle | UiFlags.Decor);
        UiDraw.TextMiddle(s, on.In(tilt), bx, sy + 32f + UiTokens.S2, 32f, 32f, UiStyle.SmallStrong, UiRole.AccentText, tilted ? "3D" : "2D", UiFlags.Centre);
        float by = vy + vh - UiTokens.S3 - UiTokens.S4;
        UiDraw.Box(s, on, sx, by + UiTokens.S3, 80f, 3f, UiRole.Text2, UiRole.None, 0f, UiFlags.Decor);
        UiDraw.Text(s, on, sx, by - UiTokens.S3, 90f, UiStyle.Small, UiRole.Text, "200 m", UiFlags.Truncate);
        if (route)
        {
            float ly = vy + vh + UiTokens.S3;
            float lw = w - pad * 2f;
            UiDraw.TextMiddle(s, on, x + pad, ly, UiTokens.PairRow + UiTokens.S1, lw, UiStyle.Body, UiRole.Text, lng ? "In 240 m turn right onto East 40th Street near the Public Library" : "In 240 m turn right onto E 40th Street", UiFlags.Truncate);
            UiDraw.TextMiddle(s, on, x + pad, ly + UiTokens.PairRow + UiTokens.S1, UiTokens.PairRow + UiTokens.S1, lw, UiStyle.Body, UiRole.Text2, "1.2 km to go, arriving about 14:36", UiFlags.Truncate);
        }
    }
}
