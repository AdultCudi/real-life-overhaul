using System.Collections.Generic;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    // The game's Options in the city over the paused street (the day street 13 and the night street 11), every state the samples hold, and
    // Controls and the Game page in French, the longest of the game's words for these rows.
    static partial void AddOptions(List<Screen> screens)
    {
        for (int i = 0; i < UiSamples.OptionsStates.Length; i++)
        {
            string st = UiSamples.OptionsStates[i];
            bool diff = st.StartsWith("difficulty");
            screens.Add(OptionsShot("options_" + st, diff ? "Custom difficulty" : "Options", i % 3 == 1 ? "11.jpg" : "13.jpg", (s, f, d, k) => UiSamples.OptionsKit(s, f, d, k, English, st), st));
        }
        screens.Add(OptionsShot("options_controls_fr", "Options", "13.jpg", (s, f, d, k) => UiSamples.OptionsKit(s, f, d, k, French, "controls"), "controls"));
        screens.Add(OptionsShot("options_game_fr", "Options", "11.jpg", (s, f, d, k) => UiSamples.OptionsKit(s, f, d, k, French, "game"), "game"));
        screens.Add(OptionsShot("options_difficulty_fr", "Custom difficulty", "13.jpg", (s, f, d, k) => UiSamples.OptionsKit(s, f, d, k, French, "difficulty_economy"), "difficulty_economy"));
    }

    private static Screen OptionsShot(string name, string app, string backdrop, LayOut lay, string state)
    {
        bool single = name.EndsWith("_fr");
        Screen screen = new Screen
        {
            Name = name, AppName = app, Icon = app == "Options" ? UiIcon.Gear : UiIcon.Sliders, Backdrop = backdrop, Lay = lay, Single = single,
            FocusCommand = UiSettingsList.RowCommand, FocusArg = 2, HoverCommand = UiSettingsList.RowCommand, HoverArg = 3,
            Walk = Moves("DDDD")
        };
        if (state == "choice")
        {
            screen.FocusCommand = UiSettingsList.ChoiceCommand;
            screen.FocusArg = 0;
            screen.HoverCommand = UiSettingsList.ChoiceCommand;
            screen.HoverArg = 2;
        }
        return screen;
    }
}
