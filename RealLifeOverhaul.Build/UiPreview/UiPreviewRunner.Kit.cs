using System.Collections.Generic;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    // The older shapes' own samples and every page of the gallery. The desk session stands indoors, where a desk is.
    static partial void AddKit(List<Screen> screens)
    {
        screens.Add(new Screen { Name = "dashboard", AppName = "Economy", Lay = (s, f, d, k) => UiDashboard.Lay(s, UiSamples.Dashboard(English), f, d, k),
            HoverCommand = UiDashboard.RowCommand, HoverArg = 2, Walk = new[] { Down, Down, Down, Right, Down, Left, Up, Up }, Backdrop = "02.jpg" });
        screens.Add(new Screen { Name = "session", AppName = "Desk", Lay = (s, f, d, k) => UiSessionSteps.Lay(s, UiSamples.Session(English), f, d, k),
            Walk = new[] { Down, Down, Down, Right, Left, Up }, Backdrop = "03.jpg" });
        screens.Add(new Screen { Name = "form", AppName = "CityAds", Lay = (s, f, d, k) => UiForm.Lay(s, UiSamples.Form(English), f, d, k),
            Walk = new[] { Down, Down, Down, Down, Right, Left }, Backdrop = "02.jpg" });
        for (int page = 0; page < UiGallery.PageCount; page++)
        {
            int p = page;
            screens.Add(new Screen { Name = "gallery_" + page, AppName = "Interface gallery", Lay = (s, f, d, k) => UiGallery.Lay(s, UiSamples.Gallery(English, p), f, d, k),
                Walk = new[] { Down, Down, Right, Down, Right, Down, Left }, Backdrop = "13.jpg" });
        }
    }
}
