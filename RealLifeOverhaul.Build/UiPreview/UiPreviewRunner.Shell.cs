using System.Collections.Generic;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    // The full menu's frame (an app on the kit with the app row under it, and an app not on the kit yet, which keeps the
    // game's own page and chrome), All apps on each kind of folder, the question box plain, negative, long and over an app
    // window, the acknowledgement, the text box, the mod's message, the notice cards (every kind, a long one) and the list
    // full and empty: by day on the streets the full menu leaves, and by night.
    static partial void AddShell(List<Screen> screens)
    {
        screens.Add(new Screen { Name = "shell_frame_contacts", AppName = "Contacts", Lay = (s, f, d, k) => UiListDetail.Lay(s, UiSamples.ContactsWindow(English), f, d, k),
            Backdrop = "13.jpg", Icon = UiIcon.AddressBook, Bar = () => UiSamples.ShellBar(UiSamples.RowContacts, false), FocusCommand = UiListDetail.Row, FocusArg = 4,
            HoverCommand = UiDraw.AppRowCommand, HoverArg = UiSamples.RowBizMan, Walk = new[] { Down, Down, Down, Down, Down, Down, Down, Right, Right } });
        screens.Add(new Screen { Name = "shell_frame_contacts_night", AppName = "Contacts", Lay = (s, f, d, k) => UiListDetail.Lay(s, UiSamples.ContactsWindow(English), f, d, k),
            Backdrop = "11.jpg", Icon = UiIcon.AddressBook, Bar = () => UiSamples.ShellBar(UiSamples.RowContacts, false), FocusCommand = UiListDetail.Row, FocusArg = 4,
            HoverCommand = UiDraw.AllAppsCommand, HoverArg = 0, Walk = new[] { Down, Down, Down, Down, Down, Down, Down, Right } });
        // An app the kit does not draw yet: the game's own page in the game's own chrome, nothing of the kit's over it.
        screens.Add(new Screen { Name = "shell_frame_not_on_kit", AppName = "MarketInsider", Lay = (s, f, d, k) => s.Begin("the game's own app", f.Width, f.Height, d),
            Backdrop = "04.jpg", Walk = new[] { Down } });
        foreach (int folder in new[] { UiShellApps.Money, UiShellApps.Work, UiShellApps.Life, UiShellApps.Business, UiShellApps.Setup, UiShellApps.Other })
        {
            int fo = folder;
            // As it rises over the app the full menu shows, at the left above its tile.
            screens.Add(new Screen { Name = "shell_all_apps_" + fo, AppName = "Contacts", Icon = UiIcon.AddressBook,
                Lay = (s, f, d, k) =>
                {
                    UiListDetail.Lay(s, UiSamples.ContactsWindow(English), Held(f), d, k);
                    UiAllApps.LayOver(s, UiSamples.AllApps(English, fo), f, k);
                },
                Backdrop = fo == UiShellApps.Other ? "11.jpg" : "13.jpg", Bar = () => UiSamples.ShellBar(UiSamples.RowContacts, true),
                HoverCommand = UiAllApps.RowCommand, HoverArg = 1, Walk = new[] { Right, Down, Down, Left, Down, Up } });
        }
        // No other mod on the phone: Other mods says so.
        screens.Add(new Screen { Name = "shell_all_apps_none", AppName = "Contacts", Icon = UiIcon.AddressBook,
            Lay = (s, f, d, k) =>
            {
                UiListDetail.Lay(s, UiSamples.ContactsWindow(English), Held(f), d, k);
                UiAllApps.LayOver(s, UiSamples.AllApps(English, UiShellApps.Other, false), f, k);
            },
            Backdrop = "11.jpg", Bar = () => UiSamples.ShellBar(UiSamples.RowContacts, true), Walk = new[] { Right, Down } });
        // Over the street the boxes come while walking: the bar is the HUD's, without the full menu's app row.
        screens.Add(new Screen { Name = "shell_question", AppName = "Question", Lay = (s, f, d, k) => UiCard.Lay(s, UiSamples.Question(English, false, false), f, d, k),
            Backdrop = "02.jpg", HoverCommand = UiCard.ButtonCommand, HoverArg = 0, Walk = new[] { Left, Right }, Bar = UiSamples.WalkingBar });
        screens.Add(new Screen { Name = "shell_question_negative", AppName = "Question", Lay = (s, f, d, k) => UiCard.Lay(s, UiSamples.Question(English, true, false), f, d, k),
            Backdrop = "11.jpg", HoverCommand = UiCard.ButtonCommand, HoverArg = 1, Walk = new[] { Left, Right }, Bar = UiSamples.WalkingBar });
        screens.Add(new Screen { Name = "shell_question_long", AppName = "Question", Lay = (s, f, d, k) => UiCard.Lay(s, UiSamples.Question(English, false, true), f, d, k),
            Backdrop = "13.jpg", HoverCommand = UiCard.ButtonCommand, HoverArg = 0, Walk = new[] { Left, Right }, Bar = UiSamples.WalkingBar });
        screens.Add(new Screen { Name = "shell_question_over_window", AppName = "Contacts", Backdrop = "13.jpg", Icon = UiIcon.AddressBook,
            Lay = (s, f, d, k) =>
            {
                UiListDetail.Lay(s, UiSamples.ContactsWindow(English), Held(f), d, k);
                UiCard.LayOver(s, UiSamples.Question(English, true, false), f, k);
            },
            Bar = () => UiSamples.ShellBar(UiSamples.RowContacts, false), HoverCommand = UiCard.ButtonCommand, HoverArg = 0, Walk = new[] { Left, Right } });
        screens.Add(new Screen { Name = "shell_acknowledge", AppName = "Acknowledge", Lay = (s, f, d, k) => UiCard.Lay(s, UiSamples.Acknowledge(English, true), f, d, k),
            Backdrop = "02.jpg", HoverCommand = UiCard.CheckCommand, HoverArg = 0, Walk = new[] { Down, Left, Right, Up }, Bar = UiSamples.WalkingBar });
        screens.Add(Typing(new Screen { Name = "shell_text_box", AppName = "Text box", Lay = (s, f, d, k) => UiCard.Lay(s, UiSamples.TextBox(English, null), f, d, k),
            Backdrop = "13.jpg", HoverCommand = UiCard.ButtonCommand, HoverArg = 0, Walk = new[] { Down, Left, Up }, Bar = UiSamples.WalkingBar }, UiCard.FieldCommand, 0));
        screens.Add(Typing(new Screen { Name = "shell_text_box_typed", AppName = "Text box", Lay = (s, f, d, k) => UiCard.Lay(s, UiSamples.TextBox(English, "Harbour Lights Bakery"), f, d, k),
            Backdrop = "11.jpg", HoverCommand = UiCard.ButtonCommand, HoverArg = 1, Walk = new[] { Down, Left, Up }, Bar = UiSamples.WalkingBar }, UiCard.FieldCommand, 0));
        screens.Add(new Screen { Name = "shell_mod_box", AppName = "Message", Lay = (s, f, d, k) => UiCard.Lay(s, UiSamples.ModBox(English), f, d, k),
            Backdrop = "02.jpg", HoverCommand = UiCard.ButtonCommand, HoverArg = 1, Walk = new[] { Left, Right }, Bar = UiSamples.WalkingBar });
        for (int first = 0; first < 14; first += 3)
        {
            int from = first;
            screens.Add(new Screen { Name = "shell_notices_" + from, AppName = "Notices", Lay = (s, f, d, k) => WithBar(s, f, k, () => UiNotices.LayCards(s, UiSamples.NoticeCards(English, from, 3, false), f, d, k)), NoKeys = true, Bar = UiSamples.WalkingBar,
                Backdrop = from % 2 == 0 ? "02.jpg" : "11.jpg", HoverCommand = UiNotices.CardCommand, HoverArg = 1, Walk = new[] { Down, Down } });
        }
        screens.Add(new Screen { Name = "shell_notice_long", AppName = "Notices", Lay = (s, f, d, k) => WithBar(s, f, k, () => UiNotices.LayCards(s, UiSamples.NoticeCards(English, 3, 1, true), f, d, k)), NoKeys = true, Bar = UiSamples.WalkingBar,
            Backdrop = "13.jpg", HoverCommand = UiNotices.CardCommand, HoverArg = 0, Walk = new[] { Down } });
        screens.Add(new Screen { Name = "shell_notice_list", AppName = "Notifications", Lay = (s, f, d, k) => UiNotices.LayList(s, UiSamples.NoticeList(English, false), f, d, k),
            Backdrop = "13.jpg", Icon = UiIcon.Bell, HoverCommand = UiNotices.ClearCommand, HoverArg = 0, Walk = new[] { Up, Down }, Bar = UiSamples.WalkingBar });
        screens.Add(new Screen { Name = "shell_notice_list_empty", AppName = "Notifications", Lay = (s, f, d, k) => UiNotices.LayList(s, UiSamples.NoticeList(English, true), f, d, k),
            Backdrop = "11.jpg", Icon = UiIcon.Bell, Walk = new[] { Up, Down }, HoverCommand = UiDraw.CloseCommand, HoverArg = 0, Bar = UiSamples.WalkingBar });
    }

    // The window under an overlay, as the game leaves it while the overlay holds the keys: laid as the mouse leads.
    private static UiFrame Held(UiFrame f)
    {
        f.Pointer = true;
        f.Held = true;
        return f;
    }

    // The notice cards over the HUD's status bar, as they stand while walking: the bar under them, the cards on top.
    private static void WithBar(UiScene s, UiFrame f, IUiKeys k, System.Action cards)
    {
        cards();
        UiDraw.StatusBar(s, f, k);
    }
}
