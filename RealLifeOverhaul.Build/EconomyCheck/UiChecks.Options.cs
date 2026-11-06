using System.Collections.Generic;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    // Options in the city draws on the settings list's commands and the name field of the custom difficulty (this job's block, 9500 to 9519).
    private static readonly int[] OptionsCommands =
    {
        UiSettingsList.RowCommand, UiSettingsList.SearchCommand, UiSettingsList.PresetCommand, UiSettingsList.HistoryCommand, UiSettingsList.GroupCommand,
        UiSettingsList.ChoiceCommand, 9501
    };

    // The game's Options in the city and its custom difficulty, every state each has: laid out in every language, frame and input as any screen.
    static partial void UiCasesOptions(UiScreenDef def, List<UiScreenCase> cases)
    {
        bool city = def.Id == "options_city", diff = def.Id == "options_difficulty";
        if (!city && !diff)
        {
            return;
        }
        foreach (string state in UiSamples.OptionsStates)
        {
            if (state.StartsWith("difficulty") != diff)
            {
                continue;
            }
            string st = state;
            cases.Add(new UiScreenCase
            {
                Def = def, Name = def.Id + " (" + st + ")", Title = diff ? "Custom difficulty" : "Options", Commands = OptionsCommands,
                Lay = (s, f, d, k, t) => UiSamples.OptionsKit(s, f, d, k, t, st)
            });
        }
    }
}
