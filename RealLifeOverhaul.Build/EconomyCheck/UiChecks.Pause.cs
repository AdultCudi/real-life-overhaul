using System.Collections.Generic;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    private static readonly int[] PauseCommands = UiPauseMenu.Commands;

    // The pause menu, Options in the city, blueprints and tooltips, every state each has: laid out in every language, frame
    // and input as any screen.
    static partial void UiCasesPause(UiScreenDef def, List<UiScreenCase> cases)
    {
        switch (def.Id)
        {
            case "pause_menu":
                foreach (string state in UiSamples.PauseStates)
                {
                    string st = state;
                    cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (" + st + ")", Title = "Paused", Commands = PauseCommands, Overlay = true,
                        Lay = (s, f, d, k, t) => UiPauseMenu.Lay(s, UiSamples.Pause(t, st), f, d, k) });
                }
                break;
        }
    }
}
