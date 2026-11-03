using System.Collections.Generic;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    static partial void AddNews(List<Screen> screens)
    {
        AddNewsApp(screens, "news_economy", "Economy", UiSamples.NewsEconomyStates, UiIcon.Chart, "13.jpg", (t, st) => UiSamples.NewsEconomy(t, st));
        AddNewsApp(screens, "news_people", "People", UiSamples.NewsPeopleStates, UiIcon.People, "map_rent.png", (t, st) => UiSamples.NewsPeople(t, st));
        AddNewsApp(screens, "news_stand", "Newsstand", UiSamples.NewsStandStates, UiIcon.Newspaper, "13.jpg", (t, st) => UiSamples.NewsStand(t, st));
        AddNewsApp(screens, "news_paper", "City Ledger", UiSamples.NewsPaperStates, UiIcon.Newspaper, "03.jpg", (t, st) => UiSamples.NewsPaper(t, st));
        AddNewsApp(screens, "news_ending", "Ending", UiSamples.NewsEndingStates, UiIcon.Person, "03.jpg", (t, st) => UiSamples.NewsEnding(t, st));
        AddNewsApp(screens, "news_home", "Home", UiSamples.NewsHomeStates, UiIcon.Home, "map_rent.png", (t, st) => UiSamples.NewsHome(t, st));
        AddNewsApp(screens, "news_rent", "Top rents", UiSamples.NewsRentStates, UiIcon.Home, "13.jpg", (t, st) => UiSamples.NewsRent(t, st));
    }

    private static void AddNewsApp(List<Screen> screens, string name, string title, string[] states, UiIcon icon, string backdrop, System.Func<UiWords, string, NewsModel> make)
    {
        for (int i = 0; i < states.Length; i++)
        {
            string st = states[i];
            screens.Add(new Screen
            {
                Name = name + "_" + st,
                AppName = title,
                Icon = icon,
                Backdrop = backdrop,
                HoverCommand = UiAppWindow.NavCommand,
                HoverArg = 1,
                Walk = Moves("DDR"),
                Lay = (s, f, d, k) => NewsPages.Lay(s, make(English, st), f, d, k)
            });
        }
    }
}
