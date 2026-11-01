using System.Collections.Generic;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    // The HUD's pieces over the running game, each over the street it would stand on, and the box, a panel and the objectives
    // again over the night street: the bar by day, paused, fast-forwarding and at night with low needs; an info panel for a
    // business and a car; a box at a door and the hover form indoors; side
    // panels at work and everything folded at night; the objectives; a trade's timing; the subway picker, in custody and with
    // nothing open; the activity before it starts and in its other states indoors; the time skip veil; the map's side panel
    // over the city map; the designer's tool row indoors.
    static partial void AddHud(List<Screen> screens)
    {
        screens.Add(Hud("hud_bar", "13.jpg", (s, f, d, k) => UiSamples.HudBarScene(s, f, d, k, English, false, false, false), UiDraw.MoneyPillCommand, 0, "DRRRRLLL"));
        screens.Add(Hud("hud_bar_paused", "02.jpg", (s, f, d, k) => UiSamples.HudBarScene(s, f, d, k, English, true, false, false), UiDraw.TimePillCommand, 0, "DRRR"));
        screens.Add(Hud("hud_bar_fast", "02.jpg", (s, f, d, k) => UiSamples.HudBarScene(s, f, d, k, English, false, true, false), UiDraw.NeedsPillCommand, 0, "DRRR"));
        screens.Add(Hud("hud_bar_night", "11.jpg", (s, f, d, k) => UiSamples.HudBarScene(s, f, d, k, English, false, false, true), UiDraw.AlertsPillCommand, 0, "DRRRR"));
        screens.Add(Hud("hud_info_building", "13.jpg", (s, f, d, k) => UiSamples.HudInfo(s, f, d, k, English, false), UiInfoPanel.LinkCommand, 1 * 16 + 0, "DDDRDD"));
        screens.Add(Hud("hud_info_vehicle", "02.jpg", (s, f, d, k) => UiSamples.HudInfo(s, f, d, k, English, true), UiInfoPanel.FooterCommand, 1, "DDRR"));
        screens.Add(Hud("hud_prompt", "02.jpg", (s, f, d, k) => UiSamples.HudPrompt(s, f, d, k, English, false), UiWorldPrompt.ActionCommand, 1, "DDD"));
        screens.Add(Hud("hud_prompt_hover", "05.jpg", (s, f, d, k) => UiSamples.HudPrompt(s, f, d, k, English, true), UiDraw.MoneyPillCommand, 0, "RR"));
        screens.Add(Hud("hud_prompt_night", "11.jpg", (s, f, d, k) => UiSamples.HudPrompt(s, f, d, k, English, false), UiWorldPrompt.ActionCommand, 2, "DDD"));
        screens.Add(Hud("hud_info_night", "11.jpg", (s, f, d, k) => UiSamples.HudInfo(s, f, d, k, English, true), UiInfoPanel.LinkCommand, 0 * 16 + 15, "DDRR"));
        screens.Add(Hud("hud_objectives_night", "11.jpg", (s, f, d, k) => UiSamples.HudObjectives(s, f, d, k, English, false), UiObjectives.LineCommand, 1, "DRDRDRDDDDDDDD"));
        screens.Add(Hud("hud_side", "13.jpg", (s, f, d, k) => UiSamples.HudSide(s, f, d, k, English, false), UiSidePanel.ActionCommand, 4, "DDDDDDLDRD"));
        screens.Add(Hud("hud_side_collapsed", "11.jpg", (s, f, d, k) => UiSamples.HudSide(s, f, d, k, English, true), UiSidePanel.FoldCommand, 1, "DDDURRUUU"));
        screens.Add(Hud("hud_objectives", "02.jpg", (s, f, d, k) => UiSamples.HudObjectives(s, f, d, k, English, false), UiObjectives.LocateCommand, 0, "DDDDDDDDDDDDDD"));
        screens.Add(Hud("hud_objectives_empty", "13.jpg", (s, f, d, k) => UiSamples.HudObjectives(s, f, d, k, English, true), UiObjectives.FoldCommand, 0, "RR"));
        screens.Add(Hud("hud_meters", "09.jpg", (s, f, d, k) => UiSamples.HudMeters(s, f, d, k, English), UiSidePanel.FoldCommand, 0, "DD"));
        screens.Add(new Screen { Name = "hud_picker", AppName = "Subway", Icon = UiIcon.MapTrifold, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.HudPickerScene(s, f, d, k, English, false, false), HoverCommand = UiPicker.EntryCommand, HoverArg = 2, Walk = Moves("DDDDDDDDR") });
        screens.Add(new Screen { Name = "hud_picker_locked", AppName = "Court", Icon = UiIcon.Scales, Backdrop = "11.jpg",
            Lay = (s, f, d, k) => UiSamples.HudPickerScene(s, f, d, k, English, true, false), HoverCommand = UiPicker.EntryCommand, HoverArg = 1, Walk = Moves("DDD") });
        screens.Add(new Screen { Name = "hud_picker_empty", AppName = "Subway", Icon = UiIcon.MapTrifold, Backdrop = "11.jpg",
            Lay = (s, f, d, k) => UiSamples.HudPickerScene(s, f, d, k, English, false, true), Walk = Moves("D") });
        screens.Add(new Screen { Name = "hud_activity_before", AppName = "Work", Icon = UiIcon.Coffee, Backdrop = "05.jpg",
            Lay = (s, f, d, k) => UiSamples.HudActivityScene(s, f, d, k, English, UiActivityState.NotStarted), HoverCommand = UiActivity.ButtonCommand, HoverArg = 1, Walk = Moves("RDLURRD") });
        screens.Add(Hud("hud_activity_running", "05.jpg", (s, f, d, k) => UiSamples.HudActivityScene(s, f, d, k, English, UiActivityState.Running), UiActivity.SkipCommand, 0, "LR"));
        screens.Add(Hud("hud_activity_moving", "13.jpg", (s, f, d, k) => UiSamples.HudActivityScene(s, f, d, k, English, UiActivityState.Moving), UiActivity.CancelCommand, 0, "R"));
        screens.Add(Hud("hud_activity_finished", "05.jpg", (s, f, d, k) => UiSamples.HudActivityScene(s, f, d, k, English, UiActivityState.Finished), UiDraw.MoneyPillCommand, 0, "R"));
        screens.Add(new Screen { Name = "hud_time_skip", AppName = "Skip time", Icon = UiIcon.Clock, Backdrop = "12.jpg",
            Lay = (s, f, d, k) => UiSamples.HudSkipScene(s, f, d, k, English), HoverCommand = UiTimeSkip.CancelCommand, Walk = Moves("D") });
        screens.Add(new Screen { Name = "hud_map", AppName = "City map", Icon = UiIcon.MapTrifold, Backdrop = "map_rent.png",
            Lay = (s, f, d, k) => UiSamples.HudMapScene(s, f, d, k, English, true), HoverCommand = UiMapPanel.FilterCommand, HoverArg = 1, Walk = Moves("RRDDDDDDDRD") });
        screens.Add(Hud("hud_tools", "03.jpg", (s, f, d, k) => UiSamples.HudTools(s, f, d, k, English, true), UiToolRow.ChoiceCommand, 3, "URRRLLDRR"));
    }

    // A HUD scene over its street: the mouse resting on one of its targets, the pad's walk from its first.
    private static Screen Hud(string name, string backdrop, LayOut lay, int hoverCommand, int hoverArg, string walk) =>
        new Screen { Name = name, AppName = "HUD", Backdrop = backdrop, Lay = lay, HoverCommand = hoverCommand, HoverArg = hoverArg, Walk = Moves(walk) };
}
