using System.Collections.Generic;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    // The game's Contacts app on the kit, over a day street as the full menu leaves it.
    static partial void AddContacts(List<Screen> screens)
    {
        screens.Add(new Screen { Name = "contacts", AppName = "Contacts", Lay = (s, f, d, k) => UiListDetail.Lay(s, UiSamples.Contacts(English, false), f, d, k), FocusCommand = UiListDetail.Row, FocusArg = 4,
            HoverCommand = UiListDetail.Row, HoverArg = 1, Walk = new[] { Up, Down, Down, Right, Up, Right, Down, Down, Left, Left, Left }, Backdrop = "13.jpg" });
    }
}
