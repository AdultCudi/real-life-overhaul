using System.Collections.Generic;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    private static readonly int[] AllAppsCommands = { UiAllApps.TabCommand, UiAllApps.RowCommand };
    private static readonly int[] CardCommands = { UiCard.ButtonCommand, UiCard.CheckCommand, UiCard.FieldCommand };
    private static readonly int[] NoticeCommands = { UiNotices.CardCommand, UiNotices.ClearCommand };

    // The frame and the boxes and notices every screen uses: each folder of All apps (other mods', none of them), the
    // question plain, negative and long, the acknowledgement, the text box empty and typed, the mod's message, the notice
    // cards (each kind, a long one) and the list full and empty, and an app window with the frame's app row under it.
    static partial void UiCasesShell(UiScreenDef def, List<UiScreenCase> cases)
    {
        switch (def.Id)
        {
            case "all_apps":
                foreach (int folder in new[] { UiShellApps.Money, UiShellApps.Life, UiShellApps.Setup, UiShellApps.Other })
                {
                    int f = folder;
                    cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (folder " + f + ")", Title = "All apps", Commands = AllAppsCommands,
                        Lay = (s, fr, dark, k, t) => WithRow(UiSamples.RowContacts, true, () => UiAllApps.Lay(s, UiSamples.AllApps(t, f), fr, dark, k)) });
                }
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (no other mods)", Title = "All apps", Commands = AllAppsCommands,
                    Lay = (s, fr, dark, k, t) => WithRow(UiSamples.RowContacts, true, () => UiAllApps.Lay(s, UiSamples.AllApps(t, UiShellApps.Other, false), fr, dark, k)) });
                return;
            case "question_box":
                cases.Add(new UiScreenCase { Def = def, Name = def.Id, Title = "Question", Commands = CardCommands, Lay = (s, f, dark, k, t) => UiCard.Lay(s, UiSamples.Question(t, false, false), f, dark, k) });
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (negative)", Title = "Question", Commands = CardCommands, Lay = (s, f, dark, k, t) => UiCard.Lay(s, UiSamples.Question(t, true, false), f, dark, k) });
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (a long body)", Title = "Question", Commands = CardCommands, Lay = (s, f, dark, k, t) => UiCard.Lay(s, UiSamples.Question(t, false, true), f, dark, k) });
                return;
            case "acknowledge_box":
                cases.Add(new UiScreenCase { Def = def, Name = def.Id, Title = "Acknowledge", Commands = CardCommands, Lay = (s, f, dark, k, t) => UiCard.Lay(s, UiSamples.Acknowledge(t, false), f, dark, k) });
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (ticked)", Title = "Acknowledge", Commands = CardCommands, Lay = (s, f, dark, k, t) => UiCard.Lay(s, UiSamples.Acknowledge(t, true), f, dark, k) });
                return;
            case "text_box":
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (empty)", Title = "Text box", Commands = CardCommands, Lay = (s, f, dark, k, t) => UiCard.Lay(s, UiSamples.TextBox(t, null), f, dark, k) });
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (typed)", Title = "Text box", Commands = CardCommands, Lay = (s, f, dark, k, t) => UiCard.Lay(s, UiSamples.TextBox(t, "Harbour Lights Bakery and Coffee House on Fifth"), f, dark, k) });
                return;
            case "mod_box":
                cases.Add(new UiScreenCase { Def = def, Name = def.Id, Title = "Message", Commands = CardCommands, Lay = (s, f, dark, k, t) => UiCard.Lay(s, UiSamples.ModBox(t), f, dark, k) });
                return;
            case "notice_cards":
                for (int first = 0; first < 14; first += 3)
                {
                    int from = first;
                    cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (from kind " + from + ")", Title = "Notices", Commands = NoticeCommands, Overlay = true,
                        Lay = (s, f, dark, k, t) => UiNotices.LayCards(s, UiSamples.NoticeCards(t, from, 3, false), f, dark, k) });
                }
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (a long one)", Title = "Notices", Commands = NoticeCommands, Overlay = true,
                    Lay = (s, f, dark, k, t) => UiNotices.LayCards(s, UiSamples.NoticeCards(t, 3, 1, true), f, dark, k) });
                return;
            case "notice_list":
                cases.Add(new UiScreenCase { Def = def, Name = def.Id, Title = "Notifications", Commands = NoticeCommands, Lay = (s, f, dark, k, t) => UiNotices.LayList(s, UiSamples.NoticeList(t, false), f, dark, k) });
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (empty)", Title = "Notifications", Commands = NoticeCommands, Lay = (s, f, dark, k, t) => UiNotices.LayList(s, UiSamples.NoticeList(t, true), f, dark, k) });
                return;
            case "shell_frame":
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (an app on the kit, the app row under it)", Title = "Contacts",
                    Lay = (s, f, dark, k, t) => WithRow(UiSamples.RowContacts, false, () => UiListDetail.Lay(s, UiSamples.ContactsWindow(t), f, dark, k)), Commands = ListDetailCommands });
                return;
        }
    }

    // Lays a screen out with the frame's app row in its status bar, then leaves the bar as the other screens' checks know it.
    private static void WithRow(int open, bool appsOpen, System.Action lay)
    {
        System.Func<UiStatusModel> before = UiChrome.Bar;
        UiChrome.Bar = () =>
        {
            // The row alone: the bar's pills are the HUD's part of it, checked where they are built.
            UiStatusModel m = UiSamples.ShellBar(open, appsOpen);
            m.Time = m.Day = m.Season = m.Place = m.Money = m.Temperature = null;
            m.Energy = m.Hunger = m.Happiness = -1;
            m.Alerts = 0;
            return m;
        };
        try
        {
            lay();
        }
        finally
        {
            UiChrome.Bar = before;
        }
    }
}
