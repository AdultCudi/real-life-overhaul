using System;
using System.Linq;
using RealLifeOverhaul.Ui;

// The interface parity fixes for Stocks, Company, Tech and Flare (13 Nov): what the old pages show and the kit had dropped. The layouts are laid out
// from hand-made models (the pages' builders need the game's market and company), and what a builder reads is checked in its source.
internal static partial class Program
{
    private static void UiStocksFixChecks()
    {
        UiStocksMarketChecks();
        UiStocksCompanyChecks();
        UiStocksBuilderChecks();
        UiStocksOtherChecks();
        StocksRuleFigureChecks();
    }

    private static StocksModel StocksPageModel(StocksPage page)
    {
        return new StocksModel { Page = page, Title = "Stocks", Nav = new[] { new UiNavEntry { Label = "Market", Icon = UiIcon.ChartLineUp } } };
    }

    private static UiScene StocksScene(StocksModel m)
    {
        var scene = new UiScene();
        StocksLay.Lay(scene, m, UiFrame.FullMenu(16f / 9f, "Stocks"), true, UiDefaultKeys.Keyboard);
        return scene;
    }

    private static bool HasCommand(UiScene s, int command)
    {
        for (int i = 0; i < s.NodeCount; i++)
        {
            if (s.Nodes[i].Command == command)
            {
                return true;
            }
        }
        return false;
    }

    private static void UiStocksMarketChecks()
    {
        // The Market page: a news card, the rate line, and the choice between the companies and the sectors.
        StocksModel m = StocksPageModel(StocksPage.Market);
        m.Figures = new[] { new UiFigureModel { Label = "Index", Value = "1,000.00" } };
        m.News = new[] { "Hawke Bank beat its forecast", "A fund bought Water Co" };
        m.NewsTitle = "The news";
        m.RateLine = "Bank rate: 4.25%   Inflation: 2.1%";
        m.Tape = new[] { "A fund sold 40 ARE" };
        m.TapeTitle = "Who is trading";
        m.ViewLabels = new[] { "Companies", "Sectors" };
        string text = TextsOf(StocksScene(m));
        Check(text.Contains("The news") && text.Contains("Hawke Bank beat its forecast") && text.Contains("A fund bought Water Co"), "stocks market: the news card carries the newest pieces of news");
        Check(text.Contains("Bank rate: 4.25%") && text.Contains("Inflation: 2.1%"), "stocks market: the bank rate and inflation stand in one line under the figures");
        Check(text.Contains("Companies") && text.Contains("Sectors") && HasCommand(StocksScene(m), StocksLay.View), "stocks market: the companies and the sectors are one choice over the table");
        m.News = new[] { "No news in the last days" };
        m.NewsQuiet = true;
        Check(TextsOf(StocksScene(m)).Contains("No news in the last days"), "stocks market: with no news the card says so");

        // The index page: figures, the period choice, the chart's room, the rate line, what the index is, and a way back.
        StocksModel index = StocksPageModel(StocksPage.Index);
        index.Figures = new[] { new UiFigureModel { Label = "The city index", Value = "1,012.40" }, new UiFigureModel { Label = "Today's range", Value = "1,001.00 to 1,015.00" } };
        index.ViewLabels = new[] { "1 month", "3 months", "1 year", "All" };
        index.RateLine = "Bank rate: 4.25%";
        index.Note = "The worth of every listed company together, 1,000 at the first open.";
        index.Actions = new[] { new UiActionModel { Label = "Back", Kind = UiButtonKind.Secondary, Key = UiAction.Count } };
        index.EmptyTitle = "No candles yet";
        UiScene scene = StocksScene(index);
        text = TextsOf(scene);
        Check(text.Contains("1,012.40") && text.Contains("Today's range") && text.Contains("Bank rate: 4.25%") && text.Contains("1,000 at the first open"),
            "stocks index page: the level, the day's range, the rate line and what the index is");
        Check(HasCommand(scene, StocksLay.View) && HasCommand(scene, StocksLay.Act), "stocks index page: the period is chosen on it and Back is a button");
        Check(text.Contains("No candles yet"), "stocks index page: before there are days the chart's room says why");
    }

    private static void UiStocksCompanyChecks()
    {
        // The company page's Figures view: both columns of rows and the results table, with the way back to the chart.
        StocksModel m = StocksPageModel(StocksPage.Company);
        m.Co = new StocksCompany
        {
            Name = "Hawke Bank", Ticker = "HWK", Sector = "Banks", Price = "$12.40", ChangeMoney = "+$0.10", ChangePercent = "+0.80%", Way = 1,
            Views = new[] { "Chart", "Figures" }, View = 1,
            Facts = new StocksFacts
            {
                Left = new[] { new StocksRow { Label = "Worth", Value = "$1.2B" }, new StocksRow { Label = "Price on earnings", Value = "14.2" } },
                Right = new[] { new StocksRow { Label = "Dividend a year", Value = "$0.40  (3.2%)" }, new StocksRow { Label = "Its finances", Value = "sound" } },
                ResultHead = new[] { "Quarter", "Day", "Turnover", "Profit", "Earnings", "Expected", "Dividend" },
                Results = new[] { new[] { "Q2 year 3", "180", "$30.0M", "$4.0M", "$0.31", "$0.29", "$0.10" }, new[] { "Q1 year 3", "90", "$28.0M", "$3.5M", "$0.27", "$0.28", "-" } },
                Beat = new[] { true, false }
            }
        };
        UiScene scene = StocksScene(m);
        string text = TextsOf(scene);
        Check(text.Contains("Price on earnings") && text.Contains("Dividend a year") && text.Contains("sound") && text.Contains("$1.2B"), "stocks company: the figures view carries the worth, the ratios, the dividend and its finances");
        Check(text.Contains("Expected") && text.Contains("Q2 year 3") && text.Contains("Q1 year 3") && text.Contains("$0.29"), "stocks company: the figures view has the results of the quarters");
        Check(HasCommand(scene, StocksCompanyLay.View), "stocks company: the chart and the figures are one choice");
        m.Co.View = 0;
        m.Co.Facts = null;
        Check(HasCommand(StocksScene(m), StocksCompanyLay.View), "stocks company: the choice stands on the chart view too, so the way to the figures is there");

        // The order panel: the bid and ask rows, the impact warning, the company's state and a trust's line.
        m = StocksPageModel(StocksPage.Company);
        m.Co = new StocksCompany
        {
            Name = "Hawke Bank", Ticker = "HWK", Sector = "Banks", Price = "$12.40", ChangeMoney = "+$0.10", ChangePercent = "+0.80%", Way = 1,
            Order = new StocksTicket
            {
                Title = "Order", Sides = new[] { "Buy", "Sell" }, Kinds = new[] { "Market", "Limit", "Stop loss", "Take profit" }, Lives = new[] { "Day", "Until cancelled" },
                Shares = "100", LifeLabel = "Good for", SharesLabel = "Shares", Place = "Place buy order", Impact = "Its size moves the price 1.2% against it",
                Rows = new[] { new StocksRow { Label = "Bid", Value = "$12.30" }, new StocksRow { Label = "Ask", Value = "$12.50" }, new StocksRow { Label = "Total", Value = "$1,250.00" } }
            },
            PositionTitle = "Your position", PositionNone = "You hold no shares of this company",
            Notes = new[] { "Results on day 180", "A property trust: about $0.40 a share a year, paid every week." }
        };
        text = TextsOf(StocksScene(m));
        Check(text.Contains("Bid") && text.Contains("$12.30") && text.Contains("Ask") && text.Contains("$12.50"), "stocks ticket: the bid and the ask stand among its rows");
        Check(text.Contains("Its size moves the price 1.2% against it"), "stocks ticket: an order big enough to move the price says by how much");
        Check(text.Contains("Results on day 180") && text.Contains("A property trust"), "stocks ticket: the company's state and a trust's payments are told under the position");
    }

    private static void UiStocksBuilderChecks()
    {
        string build = ModSource(@"Ui\Stocks\StocksBuild.cs"), market = ModSource(@"Ui\Stocks\StocksBuild.Market.cs"), screen = ModSource(@"Ui\Stocks\StocksScreen.cs");
        string figures = ModSource(@"Ui\Stocks\StocksBuild.Figures.cs"), company = ModSource(@"Ui\Stocks\StocksBuild.Company.cs");
        string marketPage = MemberText(build, "private static void Market(");
        Check(marketPage.Contains("IndexWords(state.IndexLast)") && !marketPage.Contains("state.IndexLast.ToString()") && !marketPage.Contains("(int)StocksMath.ValueOf"),
            "stocks market: the index is written as a level (1,000.00) and the capitalisations as money, not as raw hundredths and a cast that overflows");
        Check(market.Contains("(hundredths / 100.0).ToString(\"N2\"") && marketPage.Contains("NewsLines(state, StocksModel.NewsShown") && marketPage.Contains("RateLine(state)")
              && marketPage.Contains("SectorTable(state, words)"), "stocks market: the page reads the news, the rate line and the sector table the old Market tab shows");
        string sectors = MemberText(market, "private static UiDataTableModel SectorTable(");
        Check(sectors.Contains("reallife_mkt_col_week") && sectors.Contains("reallife_mkt_col_month") && sectors.Contains("reallife_mkt_col_quarter") && sectors.Contains("reallife_mkt_col_hundred")
              && market.Contains("Backs = { 5, 21, 63, 100 }") && market.Contains("MarketHistory.SectorDayMoveBp") && market.Contains("MarketHistory.IndexDayMoveBp"),
            "stocks sectors: the index and each sector with today and the 5, 21, 63 and 100 day moves, from the history the old table reads");
        string companies = MemberText(build, "private static UiDataTableModel Companies(");
        Check(companies.Contains("MarketApp.Pe(s)") && companies.Contains("MarketApp.YieldBp(s)") && companies.Contains("HeldOrFounder(state, s, director)") && companies.Contains("s.Shares * (s.Last / 100.0)"),
            "stocks companies: the worth, the P/E, the yield and the shares held are read as the old table reads them");
        string head = MemberText(build, "private static UiDataTableModel Head(");
        foreach (string key in new[] { "reallife_mkt_col_sector", "reallife_mkt_col_worth", "reallife_mkt_col_pe", "reallife_mkt_col_yield", "reallife_mkt_col_held" })
        {
            Check(head.Contains(key), "stocks companies: the table has the column " + key);
        }
        string portfolio = MemberText(build, "private static void Portfolio(");
        Check(portfolio.Contains("StocksMath.ChangeBp(s.PrevClose, s.Last)") && portfolio.Contains("reallife_mkt_col_cost") && portfolio.Contains("reallife_stk_gain") && portfolio.Contains("reallife_mkt_col_income")
              && portfolio.Contains("reallife_mkt_col_next_pay") && portfolio.Contains("InvestRules.Holdings("),
            "stocks portfolio: the Today column is the day's move, and the cost, the gain in money, the income and the next payment stand in their own columns from the old tab's sums");
        string ticket = MemberText(build, "private static StocksTicket TicketOf(");
        Check(ticket.Contains("reallife_ibox_bid") && ticket.Contains("reallife_ibox_ask") && ticket.Contains("ImpactWarnBp") && ticket.Contains("reallife_mkt_ticket_impact"),
            "stocks ticket: the bid, the ask and the impact warning are built from the quote and the estimate");
        Check(build.Contains("public const int ImpactWarnBp = 5") && build.Contains("TrustRules.Is(k)") && build.Contains("reallife_mkt_status_bid") && build.Contains("reallife_mkt_ticket_trust_none"),
            "stocks ticket: the status line and the dividend trust's line are the old ticket's, with its 5 basis point threshold");
        Check(company.Contains("notes.Add(StockStatus(s))") && company.Contains("TrustWords(s)") && company.Contains("c.Facts = Facts(state, s, words)"), "stocks company: the notes and the figures are built with the page");
        Check(figures.Contains("MarketApp.TrailingEps(k)") && figures.Contains("k.Results[k.Results.Count - 1 - i]") && figures.Contains("reallife_mkt_fig_distress_high") && figures.Contains("ResultsShown = 6"),
            "stocks figures: the sums are the old figures page's and the results are the newest six quarters");
        Check(screen.Contains("_page == StocksPage.Market && _view == StocksBuild.MarketSectors") && screen.Contains("SectorRow(arg)") && screen.Contains("_page = StocksPage.Index"),
            "stocks screen: the sector table's rows open the index page or that sector's companies, and the table has no search");
        string setView = MemberText(screen, "private bool SetView()");
        Check(setView.Contains("StocksPage.Market") && setView.Contains("StocksPage.Index") && setView.Contains("_period = Segment("), "stocks screen: the view choice works on the Market and the index page too");
        Check(ModSource(@"Ui\Stocks\StocksScreen.Sheet.cs").Contains("case StocksPage.Index: return Back();") && screen.Contains("_page = StocksPage.Market;   // the index page"),
            "stocks index: its Back button and the Back key return to the sectors");
        string keyList = ModSource(@"Ui\UiScreens.Stocks.cs");
        Check(keyList.Contains("\"reallife_mkt_fig_worth\"") && keyList.Contains("\"reallife_ibox_bid\"") && keyList.Contains("\"reallife_mkt_col_held\"") && keyList.Contains("\"reallife_mkt_view_figures\""),
            "stocks keys: the words the new pages use are in the screen's key list");
    }

    private static void UiStocksOtherChecks()
    {
        // Company: the tax statement carries what the old tab shows beyond it.
        string tax = ModSource(@"Ui\Company\CompanyLive.Tax.cs");
        Check(tax.Contains("tf.Owed > 0") && tax.Contains("reallife_co_tax_owed") && tax.Contains("day < tf.QuietUntil") && tax.Contains("reallife_co_tax_quiet_value")
              && tax.Contains("EconomyBridges.EstateTax") && tax.Contains("reallife_co_tax_risk") && tax.Contains("reallife_co_tax_game"),
            "company tax: the back tax owed, a settled case, the audit chance, the estate tax and how the game's own tax works are on the tab");
        Check(ModSource(@"Ui\Company\CompanyFace.Tax.cs").Contains("t.GameNote"), "company tax: the note about the game's tax is drawn");
        Check(ModSource(@"Ui\Company\CompanyLive.Managers.cs").Contains("d.ExecutivesMonthCost()") && ModSource(@"Ui\Company\CompanyLive.Managers.cs").Contains("reallife_hier_exec_cost"),
            "company managers: the executives' cost a month stands beside the managers' allowances");
        // The kit's Company screen is drawn while the old app's panel is up, and that panel's opening and closing already run the director's reads: not a second call.
        string app = ModSource(@"Economy\Company\CompanyApp.cs"), director = ModSource(@"Economy\Company\CompanyDirector.cs");
        Check(app.Contains("_ready = _director.AppOpened();") && app.Contains("_director.RotaLeft();") && director.Contains("RealLifeOverhaul.Ui.CompanyLive.Opened();")
              && director.Contains("KitScreen = RealLifeOverhaul.Ui.CompanyScreen.ScreenId"), "company kit: the app panel the kit stands on reads the staff and leaves the rota, and tells the kit it opened");

        // Tech: the firm page the sector screen does not replace has the crew, the connection, the relief and the risks.
        string tech = ModSource(@"Ui\Tech\TechLive.FirmPage.cs");
        Check(tech.Contains("private UiFactCardModel CrewCard()") && tech.Contains("reallife_tech_firm_crew") && tech.Contains("reallife_tech_firm_risks") && tech.Contains("FirmsRules.Relief(m.RnD)")
              && tech.Contains("!(_d.SectorOn && _d.SectorFirmScreen(_firm, W) != null)") && tech.Contains("NetSpeedRules.NextFloor(mbps)"),
            "tech firm: the crew by skill, the connection, the research relief and the risks are on the firm's page where the old page has them");

        // Flare: another account's profile has the old profile's buttons.
        Check(FlareScreenRules.IsPlayer("p:ada") && !FlareScreenRules.IsPlayer("p:") && !FlareScreenRules.IsPlayer("i:3") && !FlareScreenRules.IsPlayer(null), "flare profile: only a player of the session can be muted or hidden");
        Check(FlareScreenRules.CanMessage("p:ada") && FlareScreenRules.CanMessage("o:2") && FlareScreenRules.CanMessage("i:7") && FlareScreenRules.CanMessage("c12") && !FlareScreenRules.CanMessage("c:x")
              && !FlareScreenRules.CanMessage("me") && !FlareScreenRules.CanMessage(null), "flare profile: a player, an official, an influencer and a resident can be written to; no other key");
        Check(FlareFace.Commands.Contains(FlareFace.ProfileActCommand) && FlareFace.Commands.Distinct().Count() == FlareFace.Commands.Length, "flare profile: the new button's command is one of the face's and unique");
        UiWords en = UiWords("en");
        FlareFaceModel profile = UiSamples.FlareSample(en, "profile_other");
        profile.Profile.Follow = "Follow";
        profile.Profile.Pills = new[] { "Message", "Mute", "Hide" };
        profile.Profile.PillActs = new[] { FlareFace.ActMessage, FlareFace.ActMute, FlareFace.ActHide };
        var scene = new UiScene();
        FlareFace.Lay(scene, UiFrame.FullMenu(16f / 9f, "Flare"), true, UiDefaultKeys.Keyboard, en, profile);
        string text = TextsOf(scene);
        Check(text.Contains("Message") && text.Contains("Mute") && text.Contains("Hide") && HasCommand(scene, FlareFace.ProfileActCommand), "flare profile: Message, Mute and Hide stand beside Follow");
        string flare = ModSource(@"Ui\Flare\FlareScreen.cs");
        Check(flare.Contains("_flare.Mute(player, !file.Net.IsMuted(player))") && flare.Contains("_flare.Hide(player, !file.Net.IsHidden(player))") && flare.Contains("OpenConversation(key)"),
            "flare profile: the buttons go through the director's own mute, hide and conversation");
    }
}
