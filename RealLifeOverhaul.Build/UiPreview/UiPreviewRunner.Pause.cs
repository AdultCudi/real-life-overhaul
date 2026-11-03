using System.Collections.Generic;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    // The pause menu over the paused city, as the game blurs and dims it: the day street (13) and the night street (11),
    // each state the samples hold; the French state is in the game's own French, its longest words for these rows.
    static partial void AddPause(List<Screen> screens)
    {
        string[] pause = { "13.jpg", "11.jpg", "13.jpg", "11.jpg" };
        for (int i = 0; i < UiSamples.PauseStates.Length; i++)
        {
            string st = UiSamples.PauseStates[i];
            screens.Add(new Screen
            {
                Name = "pause_menu_" + st, AppName = "Paused", Icon = UiIcon.Pause, Backdrop = pause[i],
                Lay = (s, f, d, k) => UiPauseMenu.Lay(s, UiSamples.Pause(English, st), f, d, k),
                FocusCommand = UiPauseMenu.RowCommand, FocusArg = 0, HoverCommand = UiPauseMenu.RowCommand, HoverArg = 1,
                Walk = Moves("DDDDD")
            });
        }
    }
}
