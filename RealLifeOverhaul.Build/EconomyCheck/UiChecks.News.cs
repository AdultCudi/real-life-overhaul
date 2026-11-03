using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    private static bool _newsChecked;

    static partial void UiCasesNews(UiScreenDef def, List<UiScreenCase> cases)
    {
        if (def.Id == "economy")
        {
            Each(cases, def, "Economy", UiSamples.NewsEconomyStates, (s, f, dark, k, t, st) => NewsPages.Lay(s, UiSamples.NewsEconomy(t, st), f, dark, k));
        }
        else if (def.Id == "people")
        {
            Each(cases, def, "People", UiSamples.NewsPeopleStates, (s, f, dark, k, t, st) => NewsPages.Lay(s, UiSamples.NewsPeople(t, st), f, dark, k));
        }
        else if (def.Id == "newsstand")
        {
            Each(cases, def, "Newsstand", UiSamples.NewsStandStates, (s, f, dark, k, t, st) => NewsPages.Lay(s, UiSamples.NewsStand(t, st), f, dark, k));
        }
        else if (def.Id == "ledger")
        {
            Each(cases, def, "City Ledger", UiSamples.NewsPaperStates, (s, f, dark, k, t, st) => NewsPages.Lay(s, UiSamples.NewsPaper(t, st), f, dark, k));
        }
        else if (def.Id == "ending")
        {
            Each(cases, def, "Ending", UiSamples.NewsEndingStates, (s, f, dark, k, t, st) => NewsPages.Lay(s, UiSamples.NewsEnding(t, st), f, dark, k));
        }
        else if (def.Id == "household")
        {
            Each(cases, def, "Home", UiSamples.NewsHomeStates, (s, f, dark, k, t, st) => NewsPages.Lay(s, UiSamples.NewsHome(t, st), f, dark, k));
        }
        else if (def.Id == "renters")
        {
            Each(cases, def, "Top rents", UiSamples.NewsRentStates, (s, f, dark, k, t, st) => NewsPages.Lay(s, UiSamples.NewsRent(t, st), f, dark, k));
        }
        if (!_newsChecked && def.Id == "economy")
        {
            _newsChecked = true;
            NewsRuleChecks();
        }
    }

    private delegate void NewsLay(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, string state);

    private static void Each(List<UiScreenCase> cases, UiScreenDef def, string title, string[] states, NewsLay lay)
    {
        foreach (string state in states)
        {
            string st = state;
            cases.Add(new UiScreenCase
            {
                Def = def,
                Name = def.Id + " (" + st + ")",
                Title = title,
                Commands = NewsCommandsAll,
                Lay = (s, f, dark, k, t) => lay(s, f, dark, k, t, st)
            });
        }
    }

    private static readonly int[] NewsCommandsAll = BuildNewsCommands();

    private static int[] BuildNewsCommands()
    {
        var list = new List<int>
        {
            UiAppWindow.NavCommand, UiAppWindow.ActionCommand, UiAppWindow.RowCommand, UiAppWindow.PersonCommand,
            UiAppWindow.MoreCommand, UiAppWindow.AllCommand, UiAppWindow.NoteCommand,
            UiChart.First + UiChart.Point, UiCatalogue.First + UiCatalogue.Card, UiCatalogue.First + UiCatalogue.Action,
            UiEdition.StoryCommand, UiEdition.LeadCommand, UiReader.AnchorCommand, UiReader.RelatedCommand, UiReader.ContentsCommand,
            UiDecision.ChoiceCommand, UiDecision.ButtonCommand, UiInfoView.ViewCommand,
            UiInfoPanel.TabCommand, UiInfoPanel.LinkCommand, UiInfoPanel.FooterCommand,
            NewsCommands.Link, NewsCommands.Rent, NewsCommands.Busy, NewsCommands.Home,
            NewsCommands.District + UiDataTable.Row
        };
        for (int i = 0; i <= UiDataTable.MassAnswer; i++)
        {
            list.Add(UiDataTable.First + i);
        }
        return list.ToArray();
    }

    private static void NewsRuleChecks()
    {
        int saving = NewsRules.Offer(1200000, 400, 700, 300, AccountRules.DaysPerYear, out int fixedDaily);
        int again = NewsRules.MonthlySaving(400, fixedDaily);
        Check(fixedDaily > 0 && saving == again && saving == NewsRules.MonthlySaving(400, NewsRules.FixedDaily(1200000, ProductTerms.FixedRateBp(700, 300, 0, 1f), AccountRules.DaysPerYear)),
            "news: the refinance saving matches the loan maths");
        Check(NewsRules.WillFix(true, 400, fixedDaily) == NewsRules.WillFix(true, 400, fixedDaily), "news: a rival uses the same fix rule");
        Check(!NewsRules.WillFix(false, 10, 40), "news: nobody fixes when the fixed day costs more and no rise is set");
        Check(NewsRules.BasketBp(1100, 1000) == 1000, "news: a basket 10% higher is 1000 basis points");
        Check(NewsRules.PassSaving(6 + 5 + 3 + 3, 12) == 5, "news: the Pass saving is the titles less the Pass");
        Check(NewsRules.Underpaid(20, 16) == 1 && NewsRules.Underpaid(20, 20) == 0, "news: pay talks count only staff under the going rate");
        Check(NewsRules.Defensive(0.4, 1.2) && !NewsRules.Defensive(1.2, 0.4), "news: defensive shares need the calm sector to vary less");
        Check(NewsRules.YearOf(61) == 2 && NewsRules.EditionOf(1102) == 1102, "news: the year is 60 days and the edition number is the day");
        var shares = new float[3];
        NewsRules.Shares(new[] { 22, 58, 20 }, shares);
        Check(shares[0] + shares[1] + shares[2] > 0.99f && shares[0] + shares[1] + shares[2] < 1.01f, "news: age shares sum to one");
        var scene = new UiScene();
        UiFrame frame = UiFrame.FullMenu(16f / 9f);
        NewsModel overview = UiSamples.NewsEconomy(UiWords("en"), "overview");
        NewsModel paper = UiSamples.NewsPaper(UiWords("en"), "busy");
        double open = NewsOpen(scene, overview, frame);
        double ledger = NewsOpen(scene, paper, frame);
        Check(open < 8.0, "news: the Economy overview opens in " + open.ToString("0.000") + " ms, under 8 ms");
        Check(ledger < 8.0, "news: the Ledger opens in " + ledger.ToString("0.000") + " ms, under 8 ms");
        Check(!HasPlaceholder(overview) && !HasPlaceholder(paper), "news: no page shows placeholder text");
    }

    private static bool HasPlaceholder(NewsModel m)
    {
        return (m.Lead != null && m.Lead.Contains("FIGURE")) || (m.Body != null && m.Body.Contains("lorem"));
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double NewsOpen(UiScene s, NewsModel m, UiFrame frame)
    {
        NewsPages.Lay(s, m, frame, true, UiDefaultKeys.Keyboard);
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 40; i++)
        {
            NewsPages.Lay(s, m, frame, (i & 1) == 0, UiDefaultKeys.Keyboard);
        }
        return watch.Elapsed.TotalMilliseconds / 40.0;
    }
}
