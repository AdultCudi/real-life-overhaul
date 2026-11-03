using System.Collections.Generic;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    static partial void AddFlare(List<Screen> screens)
    {
        Add("flare_home_empty", "home_empty", "13.jpg", FlareFace.FieldCommand, 0, "DR");
        Add("flare_home", "home", "13.jpg", UiFeed.ReactCommand, 0, "RDRD");
        Add("flare_explore", "explore", "02.jpg", UiFeed.AuthorCommand, 0, "RD");
        Add("flare_profile", "profile", "13.jpg", FlareFace.NavCommand, 4, "DD");
        Add("flare_business", "business", "02.jpg", FlareFace.NavCommand, 5, "DDD");
        Add("flare_thread", "thread", "11.jpg", FlareFace.RowCommand, 0, "D");
        Add("flare_messages", "messages", "02.jpg", UiMessageList.RowCommand, 0, "D");
        Add("flare_notices", "notices", "13.jpg", FlareFace.ActionCommand, 0, "DD");
        Add("flare_compose", "compose", "11.jpg", FlareFace.FieldCommand, 0, "D");
        Add("flare_hire", "hire", "02.jpg", FlareFace.NavCommand, 7, "D");
        Add("flare_city", "city", "13.jpg", FlareFace.NavCommand, 6, "DD");
        Add("flare_city_new", "city_new", "11.jpg", FlareFace.NavCommand, 6, "D");

        void Add(string name, string state, string backdrop, int hover, int arg, string walk)
        {
            screens.Add(new Screen
            {
                Name = name,
                AppName = "Flare",
                Icon = UiIcon.Fire,
                Backdrop = backdrop,
                HoverCommand = hover,
                HoverArg = arg,
                Walk = Moves(walk),
                Lay = (s, f, d, k) => UiSamples.Flare(s, f, d, k, English, state)
            });
        }
    }
}
