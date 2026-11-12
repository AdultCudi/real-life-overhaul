using System;
using RealLifeOverhaul.Ui;

// The map's filter list and rail stood where the dock was first laid while its panel went where the player left the window, the open state of a place was drawn with
// its break tag, the game's building box came back over the kit's card, and the card had no way into BizMan (the owner, 6 Nov). Run: --map-leak; the lines read "map leak: ...".
internal static partial class Program
{
    private static void UiMapLeakChecks()
    {
        UiWindowPlaces.Forget();
        UiWindowPlaces.Load = () => null;
        UiWindowPlaces.Save = line => { };
        UiWindowPlaces.Set("CityMapFilters", 1300f, 0f, false, true);
        var frame = new UiFrame { Width = 1920f, Height = 1080f, Band = UiFrame.BandHeight };
        MapPanelModel m = UiSamples.MapFilters(UiWords("en"), "filters");
        var screen = new MapScreenModel { Filters = m, Buttons = new MapButtonsModel() };
        var scene = new UiScene { Draggable = true };
        var grip = new UiWindowGrip { Id = "CityMapFilters", Saved = false };
        UiMap.Lay(scene, screen, frame, true, UiDefaultKeys.Keyboard);
        float railBefore = MapLeakRailX(scene), clipBefore = scene.ClipCount > 0 ? scene.Clips[0].X : -1f;
        grip.Place(scene, frame.Width, frame.BandY);
        Check(grip.X > 0f && MapLeakRailX(scene) - railBefore == grip.X && scene.ClipCount > 0 && scene.Clips[0].X - clipBefore == grip.X,
            "map leak: the dock's rail, its panel and its list move together when the window is dragged");

        // The game's own words: a break between "Open" and "Closing in 7 hours" is a line break or a dot, and no tag stays.
        string raw = "Open<br>Closing in 7 hours (17:00)";
        Check(MapText.Plain(raw) == "Open\nClosing in 7 hours (17:00)" && MapText.OneLine(raw) == "Open" + MapText.Join + "Closing in 7 hours (17:00)"
            && MapText.OneLine("<b>Bold</b> <color=#fff>white</color>") == "Bold white", "map leak: a game string's break and rich text tags are turned into lines or taken out by the one helper");
        string widgets = ModSource(@"Ui\Host\UiWidgets.cs"), world = ModSource(@"Ui\World\WorldHost.cs"), portfolio = ModSource(@"Ui\BizMan\BizManScreen.Portfolio.cs");
        Check(widgets.Contains("MapText.Plain(t.text)") && widgets.Contains("MapText.OneLine(Texts[i].text)") && !widgets.Contains("private static string Plain")
            && world.Contains("MapText.Plain(text)") && !world.Contains("bool tag = false") && portfolio.Contains("MapText.OneLine(b.OpenStatus.text.ToString())"),
            "map leak: the widgets, the world kits and BizMan's list read the game's words through MapText, not a copy of their own");

        // The game's filter window: every leaf of it is concealed, and the kit's rows are the game's groups and lines with their own switches.
        string conceal = ModSource(@"Ui\MapDock\MapFiltersGame.cs"), lines = ModSource(@"Ui\MapDock\MapLive.Lines.cs"), actions = ModSource(@"Ui\MapDock\MapLive.Actions.cs");
        Check(conceal.Contains("conceal.Add(panel.GetChild(i))") && conceal.Contains("conceal.Add(panel.GetComponent<Graphic>())") && conceal.Contains("conceal.Add(CardPanel())"),
            "map leak: the game's filter window and its building box have every child and their own picture concealed under the kit");
        string hold = ModSource(@"Ui\Host\UiConceal.cs");
        Check(hold.Contains("Dictionary<CanvasGroup, Hold> Holds") && hold.Contains("Release(l.Group)"),
            "map leak: a leaf two screens hide (the building box, under the street's card and the map's dock) is given back only when the last of them lets go");
        Check(lines.Contains("GetComponentsInChildren<CityMapFilterCategory>(true)") && lines.Contains("LinesOf(group)") && actions.Contains("Toggle"),
            "map leak: the kit's rows are read from the game's groups and their lines, and a switch is the game's own toggle");

        // BizMan from the card: drawn where the game's button is offered, and pressed through that button.
        var laid = new UiScene();
        MapPanelModel on = UiSamples.MapFilters(UiWords("en"), "filters"), off = UiSamples.MapFilters(UiWords("en"), "filters");
        on.Place.CanBizMan = true;
        on.Place.BizManLabel = "Open in BizMan";
        off.Place.CanBizMan = false;
        UiMapFilters.Lay(laid, on, frame, true, UiDefaultKeys.Keyboard);
        bool drawn = laid.FindNode(UiMapFilters.PickedBizManCommand, 0) >= 0;
        UiMapFilters.Lay(laid, off, frame, true, UiDefaultKeys.Keyboard);
        bool gone = laid.FindNode(UiMapFilters.PickedBizManCommand, 0) < 0;
        MapPlaceModel card = UiSamples.MapPlace(UiWords("en"), "overview");
        card.CanBizMan = true;
        card.BizManLabel = "Open in BizMan";
        UiMapPlace.Lay(laid, card, frame, true, UiDefaultKeys.Keyboard);
        bool onCard = laid.FindNode(UiMapPlace.BizManCommand, 0) >= 0;
        card.CanBizMan = false;
        UiMapPlace.Lay(laid, card, frame, true, UiDefaultKeys.Keyboard);
        Check(drawn && gone && onCard && laid.FindNode(UiMapPlace.BizManCommand, 0) < 0, "map leak: the picked block and the place card offer BizMan exactly where the game's card offers it");
        string picked = ModSource(@"Ui\MapDock\MapLive.Picked.cs");
        Check(picked.Contains("UiWidgets.Shows(card.bizManButton)") && picked.Contains("UiWidgets.Press(card.bizManButton)"),
            "map leak: BizMan opens through the game's own button on its building card");

        // The picked building's card stands at the building on the map, where the game puts its own box, kept on the screen, and the dock does not take it to a corner.
        string building = ModSource(@"Ui\World\WorldBuilding.cs");
        Check(building.Contains("bool up = on && (picked || !CityMap.IsOpen)") && building.Contains("HudStatusGame.Shows && !CityMap.IsOpen") && building.Contains("Vector2 at = Door(frame, _dockW, _dockH)")
            && building.Contains("_panel.GetWorldCorners(_corners)") && building.Contains("UiHudRules.Clamp(ref x, ref y, w, h, frame.Width, frame.BandY)"),
            "map leak: a building picked on the map gets its card at the building (the game's box's place, clamped), never in the HUD's column");
        float cx = 1900f, cy = 1000f;
        UiHudRules.Clamp(ref cx, ref cy, 300f, 400f, 1920f, frame.BandY);
        float bx = -50f, by = -20f;
        UiHudRules.Clamp(ref bx, ref by, 300f, 400f, 1920f, frame.BandY);
        Check(cx + 300f <= 1920f && cy + 400f <= frame.BandY && bx >= 0f && by >= 0f, "map leak: a card at a building near the screen's edge is moved whole back onto the screen");
    }

    private static float MapLeakRailX(UiScene s)
    {
        for (int i = 0; i < s.NodeCount; i++)
        {
            if (s.Nodes[i].Command == UiMapFilters.RailCommand)
            {
                return s.Nodes[i].X;
            }
        }
        return -1f;
    }
}
