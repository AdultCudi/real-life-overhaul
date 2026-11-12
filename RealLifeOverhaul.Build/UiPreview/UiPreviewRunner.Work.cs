using System.Collections.Generic;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    // Career, Campus, Open Roles and the Marketplace: every tab of each, a new player and an established one, full and empty lists, a filter chosen,
    // a person's page, a new ad, a haggle, and each app once in Brazilian Portuguese (the longer words). Over the day street the full menu leaves
    // (02 and 13), My job over an interior (05), with the frame's app row under it. The mouse rests on a row, a card or a day; the pad path shot draws the walk of
    // the page's real focus links, every target in reading order (WorkWalk).
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
            WorkHover(id, st, out int command, out int arg);
            string backdrop = id == "career" && st.StartsWith("my_job") ? "05.jpg" : i % 2 == 0 ? "13.jpg" : "02.jpg";
            // the path shot draws the walk the checks hold to (WorkWalk): every target in reading order through the real focus links, nothing hand written
            WorkScreen shown = null;
            screens.Add(new Screen
            {
                Name = id + "_" + st, AppName = title, Icon = icon, Backdrop = backdrop,
                Bar = () => UiSamples.ShellBar(4, false), HoverCommand = command, HoverArg = arg,
                Lay = (s, f, d, k) =>
                {
                    shown = make(ModEnglish, st);
                    shown.Lay(s, f, d, k);
                },
                Reading = scene =>
                {
                    WorkWalk.Walked w = shown.WalkOf(scene);
                    return w != null ? w.Stops.ToArray() : new int[0];
                }
            });
            // every state again in Brazilian Portuguese and in Simplified Chinese, the 1080 base only: the longer words and the wider script
            foreach (string lang in new[] { "pt", "zh-cn" })
            {
                string code = lang;
                screens.Add(new Screen
                {
                    Name = id + "_" + st + (code == "pt" ? "_pt" : "_zh"), AppName = title, Icon = icon, Backdrop = backdrop, Single = true, Bar = () => UiSamples.ShellBar(4, false),
                    Lay = (s, f, d, k) => make(DialogWords(code), st).Lay(s, f, d, k)
                });
            }
        }
    }

    // Where the mouse rests in the hover shot, by state: a row, a card or a day for the lists, a tab for the rest (the pad's path is the screen's own walk, WorkWalk).
    private static void WorkHover(string app, string st, out int command, out int arg)
    {
        command = UiAppWindow.NavCommand;
        arg = 1;
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
        }
        else if ((app == "campus" && st.StartsWith("courses")) || (app == "hustle" && st.StartsWith("listings") && st != "listings_none"))
        {
            command = WorkCmd.Card;
            arg = 2;   // not the chosen card (the full state chooses the fourth): a hover on it would not show
        }
        else if (app == "campus" && st.StartsWith("studying") && st != "studying_none")
        {
            command = UiDiary.DayCommand;
            arg = 18;
        }
        else if (app == "roles" && st.StartsWith("person"))
        {
            command = WorkCmd.PageTab;
            arg = 1;
        }
        else if (app == "roles" && st == "new_ad")
        {
            command = WorkCmd.Pick;   // the choice of role: a field whose hover the eye can see, where a stepper's small button steps by a few per cent
            arg = 0;
        }
        else if (app == "hustle" && st == "haggle")
        {
            command = UiAppWindow.ActionCommand;
            arg = 1;   // the second choice, pay the asking price: a button the player rests on to read before pressing
        }
        else if (app == "career" && st.StartsWith("my_job"))
        {
            command = UiAppWindow.ActionCommand;
            arg = 1;
        }
    }
}
