using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// The Tech app's founding pages as samples: the flow that founds a startup, the niches and one niche, a founded firm's go to market, and a placed firm's. Each mirrors what
// TechLive.Found writes, with real-looking names and every button the page holds. Names are placeholders and never ship.
internal static partial class UiSamples
{
    private static void TechFoundNav(UiWords t, TechModel m, int tab)
    {
        m.Nav = TechTabNav(t, out string[] keys);
        m.NavKeys = keys;
        m.NavSelected = tab;
    }

    private static void TechFound(UiWords t, TechModel m, TechRunList runs)
    {
        m.Shape = TechShape.Sheet;
        TechFoundNav(t, m, 4);
        m.Back = TW(t, "back_startups", "Startups");
        m.Head = new UiHeadModel { Name = W(t, "reallife_found_title", "Found a startup"), Icon = TechGlyphs.Startup, Role = W(t, "reallife_found_role", "A new company with a niche, a team and its first money") };
        m.HeadActions = new[] { TechAct(runs, W(t, "reallife_found_button", "Found it"), UiButtonKind.Primary, UiAction.Alt) };
        var idea = new TechCard(runs.Runs, W(t, "reallife_found_card_idea", "The idea"));
        idea.Step(W(t, "reallife_found_firm", "Business"), "Atlas Edge Software", null, true, true, () => true, () => true)
            .Step(W(t, "reallife_found_problem", "Problem"), W(t, "reallife_vc_sector_software", "Software"), null, true, true, () => true, () => true)
            .Step(W(t, "reallife_found_niche", "Niche"), W(t, "reallife_niche_restaurants", "Restaurants"), null, true, true, () => true, () => true)
            .Say(WF(t, "reallife_niche_line", "214 such businesses in the city, 1,070,000 customers within reach, up to $6,420,000 a month. 7 rivals.", "businesses", "214", "buyers", "1,070,000",
                "ceiling", "$6,420,000", "rivals", "7"))
            .Say(WF(t, "reallife_niche_mood", "Their spending against usual: the city's cycle 92%, the season 104%, rents 100%.", "cycle", "92%", "season", "104%", "rent", "100%"));
        var team = new TechCard(runs.Runs, W(t, "reallife_found_card_team", "The team"));
        team.Step(W(t, "reallife_found_cofounder", "Co-founder"), "Priya Raman", null, true, true, () => true, () => true)
            .Say(WF(t, "reallife_found_offer_line", "Priya Raman, 34, Software engineer (Senior), skill 68. One of the city's people.", "name", "Priya Raman", "age", "34", "role", "Software engineer",
                "level", "Senior", "skill", "68", "from", W(t, "reallife_found_origin_population", "One of the city's people.")))
            .Step(W(t, "reallife_found_split", "Their share"), "20%", null, true, true, () => true, () => true)
            .Say(WF(t, "reallife_found_vesting", "Shares vest over 4 years with a 1 year cliff. A co-founder who leaves before the cliff takes nothing, and after it only the vested part.",
                "years", "4", "cliff", "1"));
        var papers = new TechCard(runs.Runs, W(t, "reallife_found_card_papers", "Papers and money"));
        papers.Step(W(t, "reallife_found_articles", "Articles"), W(t, "reallife_found_articles_friendly", "Founder friendly"), null, true, true, () => true, () => true)
            .Say(W(t, "reallife_found_articles_friendly_about", "Founder friendly articles. Investors can remove you only if they hold the board's majority. They value the firm lower, and the lawyer takes longer."))
            .Say(WF(t, "reallife_found_fee", "The legal fees are $6,750, paid when you found it.", "fee", "$6,750"))
            .Step(W(t, "reallife_found_source", "First money"), W(t, "reallife_found_source_family", "Friends and family"), null, true, true, () => true, () => true)
            .Step(W(t, "reallife_found_family_step", "Cheque"), "$50,000", null, true, true, () => true, () => true)
            .Say(WF(t, "reallife_found_src_family", "Friends and family put in $50,000 for 3.2%. They do not haggle, and they are upset if the firm fails.", "amount", "$50,000", "share", "3.2%"))
            .Say(W(t, "reallife_found_held", "This money is the firm's, held for the investors. Your wallet must keep it while the firm runs."));
        m.Left = new[] { idea.Build(), team.Build() };
        m.Right = new[] { papers.Build() };
    }

    // The second and third sources, for the page that shows an angel's cheque and an accelerator's terms.
    private static void TechFoundAngel(UiWords t, TechModel m, TechRunList runs)
    {
        TechFound(t, m, runs);
        var papers = new TechCard(runs.Runs, W(t, "reallife_found_card_papers", "Papers and money"));
        papers.Step(W(t, "reallife_found_source", "First money"), W(t, "reallife_found_source_angels", "An angel"), null, true, true, () => true, () => true)
            .Step(W(t, "reallife_found_angel", "Angel group"), "Old Mill Angels", null, true, true, () => true, () => true)
            .Say(WF(t, "reallife_found_src_angels", "The group offers $100,000 for 6.2%, valuing the firm at $1,510,000 before the money. No board seat.", "amount", "$100,000", "share", "6.2%",
                "pre", "$1,510,000"))
            .Step(W(t, "reallife_found_source", "First money"), W(t, "reallife_found_source_accelerator", "An accelerator"), null, true, true, () => true, () => true)
            .Say(WF(t, "reallife_found_src_accel", "$75,000 for 7%, then 60 days of mentoring that raise your Selling, Product sense and Leadership, and a demo day when venture firms meet the firm.",
                "amount", "$75,000", "share", "7%", "days", "60"))
            .Say(W(t, "reallife_found_src_savings", "Your own savings pay for the first months. Nothing is raised and no investor holds your money."));
        m.Right = new[] { papers.Build() };
    }

    private static void TechFoundNone(UiWords t, TechModel m, TechRunList runs)
    {
        m.Shape = TechShape.Sheet;
        TechFoundNav(t, m, 4);
        m.Back = TW(t, "back_startups", "Startups");
        m.Head = new UiHeadModel { Name = W(t, "reallife_found_title", "Found a startup"), Icon = TechGlyphs.Startup, Role = W(t, "reallife_found_role", "A new company with a niche, a team and its first money") };
        var none = new TechCard(runs.Runs, W(t, "reallife_found_card_idea", "The idea"));
        none.Say(W(t, "reallife_found_none", "You have no tech business to found a startup at. Open one first, or every one you have is founded already."))
            .Go(W(t, "reallife_found_firms_link", "See your businesses"), () => true);
        m.Left = new[] { none.Build() };
    }

    private static void TechNiches(UiWords t, TechModel m, TechRunList runs)
    {
        m.Shape = TechShape.Table;
        TechFoundNav(t, m, 4);
        m.Back = TW(t, "back_startups", "Startups");
        var rows = new List<UiDataRow>();
        void Add(string name, string businesses, string buyers, string ceiling, string rivals, string price, string mood, UiRole tone)
        {
            UiDataRow r = TechRow(7);
            r.Cells[0] = name; r.Values[0] = (int)TechGlyphs.Startup; r.Cells[1] = businesses; r.Cells[2] = buyers; r.Cells[3] = ceiling; r.Cells[4] = rivals; r.Cells[5] = price; r.Cells[6] = mood;
            r.Tones[6] = tone;
            rows.Add(r);
        }
        Add("Restaurants", "214", "1,070,000", "$6,420,000", "7", "112%", "96%", UiRole.Notice);
        Add("Gyms", "31", "155,000", "$930,000", "2", "94%", "92%", UiRole.Warn);
        Add("Landlords", "386", "1,930,000", "$11,580,000", "9", "100%", "108%", UiRole.Ok);
        Add("Hospitals and schools", "12", "260,000", "$78,000,000", "3", "100%", "100%", UiRole.Notice);
        m.Table = TechTable(t, new[]
        {
            TechCol(t, "reallife_niche_col_niche", "Niche", UiCellKind.Tile), TechCol(t, "reallife_niche_col_businesses", "Businesses", UiCellKind.Figure, 96f),
            TechCol(t, "reallife_niche_col_buyers", "Customers", UiCellKind.Figure, 100f), TechCol(t, "reallife_niche_col_ceiling", "Most a month", UiCellKind.Money, 112f),
            TechCol(t, "reallife_niche_col_rivals", "Rivals", UiCellKind.Figure, 76f), TechCol(t, "reallife_niche_col_price", "Price", UiCellKind.Text, 76f),
            TechCol(t, "reallife_niche_col_mood", "Mood", UiCellKind.Chip, 120f)
        }, rows, "", "");
        m.RowKeys = new[] { "niche:0", "niche:4", "niche:10", "niche:11" };
        m.Next = W(t, "reallife_niche_about", "Each niche is counted from this city's own businesses of that kind, once a day. A small niche has fewer rivals and caps out sooner.");
    }

    private static void TechNiche(UiWords t, TechModel m, TechRunList runs)
    {
        m.Shape = TechShape.Sheet;
        TechFoundNav(t, m, 4);
        m.Back = W(t, "reallife_niche_back", "Niches");
        m.Head = new UiHeadModel { Name = W(t, "reallife_niche_restaurants", "Restaurants"), Icon = TechGlyphs.Startup, Role = W(t, "reallife_niche_role", "A niche of the tech market") };
        m.Figures = new[]
        {
            new UiFigureModel { Label = W(t, "reallife_niche_col_businesses", "Businesses"), Value = "214" }, new UiFigureModel { Label = W(t, "reallife_niche_col_buyers", "Customers"), Value = "1,070,000" },
            new UiFigureModel { Label = W(t, "reallife_niche_col_ceiling", "Most a month"), Value = "$6,420,000" }
        };
        var facts = new TechCard(runs.Runs, W(t, "reallife_niche_card_size", "Its size"));
        facts.Row(W(t, "reallife_niche_col_businesses", "Businesses"), "214").Row(W(t, "reallife_niche_staff", "Jobs at them"), "2,310").Row(W(t, "reallife_niche_col_buyers", "Customers"), "1,070,000")
            .Row(W(t, "reallife_niche_col_rivals", "Rivals"), "7").Row(W(t, "reallife_niche_col_price", "Price"), "112%")
            .Say(W(t, "reallife_niche_size_about", "Customers are the city's businesses of this kind and the region's like them. The most a month is those customers at the going price."));
        m.Left = new[] { facts.Build() };
        var mood = new TechCard(runs.Runs, W(t, "reallife_niche_card_mood", "What moves it"));
        mood.Row(W(t, "reallife_niche_cycle", "The city's cycle"), "92%").Row(W(t, "reallife_niche_season", "The season"), "104%").Row(W(t, "reallife_niche_rent", "Rents"), "-")
            .Say(W(t, "reallife_niche_mood_about", "Restaurants buy less in a downturn, and landlords buy more when rents rise. A dash means the mod holds no data for it."));
        var open = new TechCard(runs.Runs, W(t, "reallife_niche_card_open", "Open it up"));
        open.Button(WF(t, "reallife_niche_button_open", "Open Atlas Edge Software here for $14,700", "firm", "Atlas Edge Software", "cost", "$14,700"), UiButtonKind.Secondary, UiAction.Count, () => true)
            .Say(W(t, "reallife_niche_open_none", "No firm of yours can open this niche now. It must be a neighbour of a niche the firm already has."));
        m.Right = new[] { mood.Build(), open.Build() };
    }

    private static void TechGtm(UiWords t, TechModel m, TechRunList runs, bool placed)
    {
        m.Shape = TechShape.Sheet;
        TechFoundNav(t, m, 5);
        m.Back = TW(t, "back_cap", "Back to the firm");
        m.Head = new UiHeadModel { Name = WF(t, "reallife_gtm_title", "Go to market: Atlas Edge Software", "firm", "Atlas Edge Software"), Icon = TechGlyphs.Startup, Role = "Software" };
        m.Figures = new[]
        {
            new UiFigureModel { Label = W(t, "reallife_gtm_figure_stage", "Stage"), Value = W(t, "reallife_gtm_stage_users", "Early users") },
            new UiFigureModel { Label = W(t, "reallife_gtm_figure_fit", "Fit"), Value = "58%" }, new UiFigureModel { Label = W(t, "reallife_gtm_figure_niche", "Niche"), Value = W(t, "reallife_niche_restaurants", "Restaurants") }
        };
        var stage = new TechCard(runs.Runs, W(t, "reallife_gtm_card_stage", "Where it stands"));
        stage.Row(W(t, "reallife_gtm_figure_stage", "Stage"), W(t, "reallife_gtm_stage_users", "Early users")).Bar(W(t, "reallife_gtm_figure_fit", "Fit"), 58, "58%")
            .Say(WF(t, "reallife_gtm_fit_not", "Not yet. Fit must reach 65% with 5 paying customers.", "bar", "65%", "users", "5"))
            .Row(W(t, "reallife_gtm_learned", "Learned from customers"), "74%").Row(W(t, "reallife_gtm_channel", "Channel"), W(t, "reallife_gtm_channel_none", "Not chosen"))
            .Row(W(t, "reallife_gtm_price", "Price tested"), "-")
            .Say(W(t, "reallife_gtm_next_users", "Listen to your first customers at your desk. Fit must pass the bar with enough paying customers."));
        if (placed)
        {
            stage.Say(W(t, "reallife_gtm_placed", "This firm was running before. Its niche is picked and nothing about it has changed. Use the niche and fit to start going to market."))
                .Button(W(t, "reallife_gtm_button_engage", "Use its niche and fit"), UiButtonKind.Primary, UiAction.Alt, () => true);
        }
        var niche = new TechCard(runs.Runs, W(t, "reallife_gtm_card_niche", "Its niche"));
        niche.Row(W(t, "reallife_gtm_main_niche", "Main niche"), W(t, "reallife_niche_restaurants", "Restaurants"))
            .Say(WF(t, "reallife_niche_line", "214 such businesses in the city, 1,070,000 customers within reach, up to $6,420,000 a month. 7 rivals.", "businesses", "214", "buyers", "1,070,000",
                "ceiling", "$6,420,000", "rivals", "7"))
            .Row(W(t, "reallife_gtm_opened", "Also in"), W(t, "reallife_niche_cafes", "Cafes"))
            .Go(W(t, "reallife_gtm_niches_link", "See all niches"), () => true);
        m.Left = new[] { stage.Build(), niche.Build() };
        var papers = new TechCard(runs.Runs, W(t, "reallife_gtm_card_papers", "Papers"));
        papers.Row(W(t, "reallife_found_articles", "Articles"), W(t, "reallife_found_articles_standard", "Standard")).Row(W(t, "reallife_found_source", "First money"), W(t, "reallife_found_source_accelerator", "An accelerator"))
            .Say(W(t, "reallife_found_articles_standard_about", "Standard articles. Investors with a board seat can remove you as chief executive if the firm's money goes missing. They value the firm at its full worth."));
        var team = new TechCard(runs.Runs, W(t, "reallife_gtm_card_cofounders", "Co-founders"));
        team.Who("Priya Raman", "Software engineer, Senior", WF(t, "reallife_gtm_cofounder_vested", "31% vested", "vested", "31%"))
            .Button(WF(t, "reallife_gtm_button_part", "Part ways with Priya Raman", "name", "Priya Raman"), UiButtonKind.Secondary, UiAction.Count, () => true)
            .Row("Tomas Ondrej", WF(t, "reallife_gtm_cofounder_left", "kept 120,000 shares, left on day 410", "kept", "120,000", "day", "410"), UiRole.WarnText);
        m.Right = new[] { papers.Build(), team.Build() };
    }
}
