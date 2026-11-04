using System.Collections.Generic;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    // Career, Campus, Open Roles and the Marketplace: every tab of each, a new player and an established one, over the day street the full menu
    // leaves (02 and 13), My job over an interior (05), with the frame's app row under it.
    static partial void AddWork(List<Screen> screens)
    {
        AddWorkApp(screens, "career", "Career", UiIcon.Briefcase, UiSamples.CareerStates, (t, st) => UiSamples.CareerScreenOf(t, st));
        AddWorkApp(screens, "campus", "Campus", UiIcon.Cap, UiSamples.CampusStates, (t, st) => UiSamples.CampusScreenOf(t, st));
        AddWorkApp(screens, "roles", "Open Roles", UiIcon.People, UiSamples.RolesStates, (t, st) => UiSamples.RolesScreenOf(t, st));
        AddWorkApp(screens, "hustle", "Marketplace", UiIcon.Store, UiSamples.MarketplaceStates, (t, st) => UiSamples.MarketScreenOf(t, st));
    }

    private static void AddWorkApp(List<Screen> screens, string id, string title, UiIcon icon, string[] states, System.Func<UiWords, string, WorkScreen> make)
    {
        for (int i = 0; i < states.Length; i++)
        {
            string st = states[i];
            screens.Add(new Screen
            {
                Name = id + "_" + st, AppName = title, Icon = icon, Backdrop = id == "career" && st.StartsWith("my_job") ? "05.jpg" : i % 2 == 0 ? "13.jpg" : "02.jpg",
                Bar = () => UiSamples.ShellBar(4, false), HoverCommand = UiAppWindow.NavCommand, HoverArg = 1, Walk = Moves("DDR"),
                Lay = (s, f, d, k) => make(ModEnglish, st).Lay(s, f, d, k)
            });
        }
    }
}
