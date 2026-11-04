using System.Collections.Generic;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    // Career, Campus, Open Roles and the Marketplace: every tab of each, a new player and an established one, full and empty lists, a filter chosen,
    // a person's page, a new ad, a haggle, and each app once in Brazilian Portuguese (the longer words). Over the day street the full menu leaves
    // (02 and 13), My job over an interior (05), with the frame's app row under it. The mouse rests on a row, a card or a day; the pad walks from the
    // page's first target down the list and across to its detail.
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
            WorkHover(id, st, out int command, out int arg, out string walk);
            screens.Add(new Screen
            {
                Name = id + "_" + st, AppName = title, Icon = icon, Backdrop = id == "career" && st.StartsWith("my_job") ? "05.jpg" : i % 2 == 0 ? "13.jpg" : "02.jpg",
                Bar = () => UiSamples.ShellBar(4, false), HoverCommand = command, HoverArg = arg, Walk = Moves(walk),
                Lay = (s, f, d, k) => make(st.EndsWith("_pt") ? DialogWords("pt") : ModEnglish, st).Lay(s, f, d, k)
            });
        }
    }

    // Where the mouse rests in the hover shot and how the pad walks, by state: a row, a card or a day for the lists, a tab for the rest.
    private static void WorkHover(string app, string st, out int command, out int arg, out string walk)
    {
        command = UiAppWindow.NavCommand;
        arg = 1;
        walk = "DDR";
        if (app == "career" && st == "jobs_empty")
        {
            arg = 2;   // nothing to rest on in the list: the mouse rests on the Applications tab, so the shot differs from the base
        }
        bool table = app == "career" ? st.StartsWith("professions") || st.StartsWith("jobs") || st.StartsWith("applications")
            : app == "roles" ? st.StartsWith("jobs") || st.StartsWith("applicants") || st.StartsWith("market") || st.StartsWith("team")
            : app == "hustle" ? st.StartsWith("yours") || st.StartsWith("offers") || st.StartsWith("freelance") : st.StartsWith("schools");
        if (table && !st.StartsWith("jobs_empty") && !st.StartsWith("applicants_none"))
        {
            command = UiDataTable.First + UiDataTable.Row;
            arg = 2;
            walk = "DDDDR";
        }
        else if ((app == "campus" && st.StartsWith("courses")) || (app == "hustle" && st.StartsWith("listings") && st != "listings_none"))
        {
            command = WorkCmd.Card;
            arg = 2;   // not the chosen card (the full state chooses the fourth): a hover on it would not show
            walk = "RRDR";
        }
        else if (app == "campus" && st.StartsWith("studying") && st != "studying_none")
        {
            command = UiDiary.DayCommand;
            arg = 18;
            walk = "DRR";
        }
        else if (app == "roles" && st.StartsWith("person"))
        {
            command = WorkCmd.PageTab;
            arg = 1;
            walk = "RR";
        }
        else if (app == "roles" && st == "new_ad")
        {
            command = WorkCmd.More;
            arg = 5;
            walk = "DDDD";
        }
        else if (app == "hustle" && st == "haggle")
        {
            command = WorkCmd.More;
            arg = 0;
            walk = "DDD";
        }
        else if (app == "career" && st.StartsWith("my_job"))
        {
            command = UiAppWindow.ActionCommand;
            arg = 1;
            walk = "LDD";
        }
    }
}
