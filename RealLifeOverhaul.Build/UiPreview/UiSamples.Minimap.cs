using RealLifeOverhaul.Nav;
using RealLifeOverhaul.Ui;

internal static partial class UiSamples
{
    /// <summary>
    /// The HUD with an objective, two notice cards, the keys' prompts and a world prompt all up, and the minimap's card where
    /// the mod's placing rules put it over the kit's bar. The preview cannot draw the game's panel, so the card is a stand-in
    /// of the same rectangle: what the kit sees when the pieces keep clear of it.
    /// </summary>
    public static void HudMinimapScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, bool tall)
    {
        UiObjectivesModel goals = PiecesGoals(t, false);
        if (!tall)
        {
            goals.Groups = new[] { goals.Groups[0], goals.Groups[1] };   // the first two: a short column, with room under it for the card
        }
        UiHudNoticeModel[] notes = PiecesNotes(t, false);
        var m = new UiHudModel
        {
            Objectives = goals,
            Notices = notes,
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
        // The card's place from the same sums the game runs (NavPlaceRules), in the HUD's canvas units, then in this layout's px.
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
        NavPlaceRules.Place(space, width, NavPlaceRules.Wanted(width, true), NavPlaceRules.Frame(false), false, out float x, out float bottom, out float map, out _);
        float high = bottom + NavPlaceRules.Frame(false) + map;
        m.AvoidX = (float)System.Math.Floor(x / scale);
        m.AvoidW = (float)System.Math.Ceiling(width / scale);
        m.AvoidY = (float)System.Math.Floor(f.Height - high / scale);
        m.AvoidH = (float)System.Math.Ceiling((high - bottom) / scale);
        Hud(s, f, dark, k, t, m, bar);
        int card = UiDraw.Box(s, UiAt.Screen, m.AvoidX, m.AvoidY, m.AvoidW, m.AvoidH, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
        UiAt on = UiAt.Screen.In(card);
        float below = UiDraw.TitleStrip(s, on, m.AvoidX, m.AvoidY, m.AvoidW, UiIcon.MapTrifold, "Minimap (the card's rectangle)", false);
        UiDraw.Box(s, on, m.AvoidX + UiTokens.S3, below + UiTokens.S2, m.AvoidW - UiTokens.S3 * 2f, m.AvoidY + m.AvoidH - UiTokens.S3 - below - UiTokens.S2, UiRole.Bg, UiRole.None, UiTokens.RadiusPanel, UiFlags.Decor);
    }
}
