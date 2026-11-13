using System.Collections.Generic;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    private static readonly int[] HudCommands =
    {
        UiInfoPanel.TabCommand, UiInfoPanel.LinkCommand, UiInfoPanel.FooterCommand, UiWorldPrompt.ActionCommand, UiSidePanel.FoldCommand, UiSidePanel.LinkCommand,
        UiSidePanel.ActionCommand, UiObjectives.FoldCommand, UiObjectives.GroupCommand, UiObjectives.LineCommand, UiObjectives.LocateCommand, UiActivity.SkipCommand,
        UiActivity.FastCommand, UiActivity.CancelCommand, UiToolRow.ToolCommand, UiToolRow.ChoiceCommand, UiDraw.TimePillCommand, UiDraw.MoneyPillCommand, UiDraw.NeedsPillCommand,
        UiDraw.AlertsPillCommand, UiDraw.SettingsPillCommand
    };
    private static readonly int[] PickerCommands = { UiPicker.EntryCommand, UiPicker.PageCommand, UiPicker.EmptyCommand, UiPicker.ChooseCommand, UiPicker.SecondCommand };
    private static readonly int[] ActivityCardCommands = { UiActivity.ButtonCommand, UiActivity.StepCommand };
    private static readonly int[] TimeSkipCommands = { UiTimeSkip.CancelCommand };

    // The HUD's pieces, every state each has: laid out in every language, frame and input as any screen.
    static partial void UiCasesHud(UiScreenDef def, List<UiScreenCase> cases)
    {
        switch (def.Id)
        {
            case "hud":
                Hud(def, cases, "the bar walking", (s, f, d, k, t) => UiSamples.HudBarScene(s, f, d, k, t, false, false, false));
                Hud(def, cases, "the bar paused", (s, f, d, k, t) => UiSamples.HudBarScene(s, f, d, k, t, true, false, false));
                Hud(def, cases, "the bar fast-forwarding", (s, f, d, k, t) => UiSamples.HudBarScene(s, f, d, k, t, false, true, false));
                Hud(def, cases, "the bar at night, needs low", (s, f, d, k, t) => UiSamples.HudBarScene(s, f, d, k, t, false, false, true));
                Hud(def, cases, "the bar with the clock held", (s, f, d, k, t) => UiSamples.HudBarScene(s, f, d, k, t, false, false, false, true));
                Hud(def, cases, "a business's info panel", (s, f, d, k, t) => UiSamples.HudInfo(s, f, d, k, t, false));
                Hud(def, cases, "a car's info panel", (s, f, d, k, t) => UiSamples.HudInfo(s, f, d, k, t, true));
                Hud(def, cases, "a box at a door", (s, f, d, k, t) => UiSamples.HudPrompt(s, f, d, k, t, false));
                Hud(def, cases, "the hover form over an item", (s, f, d, k, t) => UiSamples.HudPrompt(s, f, d, k, t, true));
                Hud(def, cases, "side panels at work", (s, f, d, k, t) => UiSamples.HudSide(s, f, d, k, t, false));
                Hud(def, cases, "everything folded", (s, f, d, k, t) => UiSamples.HudSide(s, f, d, k, t, true));
                Hud(def, cases, "the objectives with every group", (s, f, d, k, t) => UiSamples.HudObjectives(s, f, d, k, t, false));
                Hud(def, cases, "the objectives with nothing to do", (s, f, d, k, t) => UiSamples.HudObjectives(s, f, d, k, t, true));
                Hud(def, cases, "a timing gauge and a meter", (s, f, d, k, t) => UiSamples.HudMeters(s, f, d, k, t));
                foreach (UiActivityState state in new[] { UiActivityState.Moving, UiActivityState.Waiting, UiActivityState.Started, UiActivityState.Running, UiActivityState.Finished })
                {
                    UiActivityState st = state;
                    Hud(def, cases, "the activity " + st, (s, f, d, k, t) => UiSamples.HudActivityScene(s, f, d, k, t, st));
                }
                Hud(def, cases, "the designer's tool row, floors in hand", (s, f, d, k, t) => UiSamples.HudTools(s, f, d, k, t, true));
                Hud(def, cases, "the designer's tool row, no tool in hand", (s, f, d, k, t) => UiSamples.HudTools(s, f, d, k, t, false));
                break;
            case "hud_picker":
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (ten stations, three greyed)", Title = "Subway", Commands = PickerCommands,
                    Lay = (s, f, d, k, t) => UiSamples.HudPickerScene(s, f, d, k, t, false, false) });
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (in custody)", Title = "Court", Commands = PickerCommands, Lay = (s, f, d, k, t) => UiSamples.HudPickerScene(s, f, d, k, t, true, false) });
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (nothing to pick)", Title = "Subway", Commands = PickerCommands, Lay = (s, f, d, k, t) => UiSamples.HudPickerScene(s, f, d, k, t, false, true) });
                break;
            case "hud_activity":
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (before it starts)", Title = "Work", Commands = ActivityCardCommands,
                    Lay = (s, f, d, k, t) => UiSamples.HudActivityScene(s, f, d, k, t, UiActivityState.NotStarted) });
                break;
            case "hud_time_skip":
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (sleeping until the shift)", Title = "Skip time", Commands = TimeSkipCommands, Lay = (s, f, d, k, t) => UiSamples.HudSkipScene(s, f, d, k, t) });
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (in custody, no Cancel)", Title = "Skip time", Commands = TimeSkipCommands, Lay = (s, f, d, k, t) => UiSamples.HudSkipScene(s, f, d, k, t, true) });
                break;
        }
    }

    private static void Hud(UiScreenDef def, List<UiScreenCase> cases, string name, System.Action<UiScene, UiFrame, bool, IUiKeys, UiWords> lay) =>
        cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (" + name + ")", Title = "HUD", Commands = HudCommands, Lay = lay });
}
