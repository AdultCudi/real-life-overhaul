using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Tech;

// The tech fixes of 23 Nov 2026: a licence's page says why customers left and what more features do, a licence far over its share loses customers to rivals by the day's seed,
// the studio's support tickets (a game bought once raises few; one person answers 60), the campaign spent in the run-up to launch, a desk session naming its game, withdrawn
// products removed by hand, a firm's manager assigning roles (never past a pin) and the executives' pages (their reach, yesterday and the company rules' conditions).
// Run alone with --tech23.
internal static partial class Program
{
    private static void Tech23()
    {
        Tech23Assign();
        Tech23Saves();
        Tech23Drift();
        Tech23Page();
        Tech23Studio();
        Tech23Executives();
        Tech23Texts();
    }

    private static AssignPerson Tech23Person(string id, int role, int points, bool pinned = false, bool away = false)
    {
        RoleDef r = TalentCatalogue.Role(role);
        return new AssignPerson { Id = id, Role = role, Level = 1, Points = points, Pinned = pinned, Away = away, Support = r.Family == Family.Support, Sales = r.Family == Family.Commercial };
    }

    private static void Tech23Assign()
    {
        var moves = new List<AssignMove>();
        // two sellers, one support person, three support people needed, one seller wanted: the better seller goes to support, the pinned one stays
        var people = new List<AssignPerson>
        {
            Tech23Person("a", TalentCatalogue.SalesExecutive, 50000), Tech23Person("b", TalentCatalogue.SalesExecutive, 70000), Tech23Person("c", TalentCatalogue.ItSupport, 40000)
        };
        int made = AssignRules.Plan(people, 2, 1, false, moves);
        Check(made == 1 && moves[0].Id == "b" && moves[0].Why == AssignRules.WhySupport && TalentCatalogue.Role(moves[0].To).Family == Family.Support,
            "tech23 assign: a seller over what the firm wants goes to support when its customers' tickets need more, the better skilled first");
        people[1] = Tech23Person("b", TalentCatalogue.SalesExecutive, 70000, pinned: true);
        moves.Clear();
        made = AssignRules.Plan(people, 2, 1, false, moves);
        Check(made == 1 && moves[0].Id == "a", "tech23 assign: a person the player pinned in their role stays, the next one is moved");
        people[0] = Tech23Person("a", TalentCatalogue.SalesExecutive, 50000, away: true);
        moves.Clear();
        Check(AssignRules.Plan(people, 2, 1, false, moves) == 0, "tech23 assign: nobody on notice or a course is moved, and nobody else can be: nothing is moved");
        // no surplus, no move: a move never opens a gap of its own
        moves.Clear();
        Check(AssignRules.Plan(new List<AssignPerson> { Tech23Person("a", TalentCatalogue.SalesExecutive, 1), Tech23Person("c", TalentCatalogue.ItSupport, 1) }, 3, 1, false, moves) == 0,
            "tech23 assign: a side with only what it needs gives nobody up");
        // contracts first: a missing seller is filled before a missing support person, and not twice a morning past the most
        var two = new List<AssignPerson>();
        for (int i = 0; i < 6; i++)
        {
            two.Add(Tech23Person("s" + i, TalentCatalogue.ItSupport, 1000 * (i + 1)));
        }
        moves.Clear();
        made = AssignRules.Plan(two, 0, 1, true, moves);
        Check(made == 1 && moves[0].Why == AssignRules.WhySales && moves[0].To == TalentCatalogue.SalesExecutive && moves[0].Id == "s5", "tech23 assign: with contracts waiting a seller is made first, the most skilled support person with spare");
        moves.Clear();
        made = AssignRules.Plan(two, 0, 5, true, moves);
        Check(made == AssignRules.MovesMost, "tech23 assign: at most " + AssignRules.MovesMost + " moves a morning");
        var again = new List<AssignMove>();
        AssignRules.Plan(two, 0, 5, true, again);
        Check(again.Count == moves.Count && again[0].Id == moves[0].Id && again[1].Id == moves[1].Id, "tech23 assign: the same people and needs give the same moves");
    }

    private static void Tech23Saves()
    {
        var f = new FirmsFile { Seed = 3 };
        f.Firms.Add(new Firm { Place = "Long%20Rd~7", Kind = (int)FirmKind.Software, Assign = true });
        f.Firms.Add(new Firm { Place = "Long%20Rd~8", Kind = (int)FirmKind.Software });
        FirmsFile back = FirmsStore.Read(FirmsStore.Write(f), out _);
        Check(back.FirmAt("Long%20Rd~7").Assign && !back.FirmAt("Long%20Rd~8").Assign, "tech23 saves: a firm's manager assigning roles is kept, and is off for a firm that never said");
        var t = new TalentFile { Seed = 1 };
        t.People.Add(new Profile { Id = "pro:1", Role = TalentCatalogue.ItSupport, Pinned = true, Place = "x~1" });
        t.People.Add(new Profile { Id = "pro:2", Role = TalentCatalogue.ItSupport, Place = "x~1" });
        TalentFile tb = TalentStore.Read(TalentStore.Write(t), out _);
        Check(tb.People.Count == 2 && tb.People[0].Pinned && !tb.People[1].Pinned, "tech23 saves: a role pinned by hand is kept");
        string src = ModSource("Tech/TechDirector.Roles.cs");
        Check(MemberText(src, "internal void SetRole(string id, int role)").Contains("Pinned = true"), "tech23 saves: a role the player sets by hand is pinned");
        string dir = ModSource("Tech/TechDirector.Assign.cs");
        Check(dir.Contains("if (!file.Assign)") && dir.Contains("Assigner(file.Place)") && ModSource("Tech/TechDirector.Firms.cs").Contains("AssignDay(day);"),
            "tech23 assign: only a firm that has it on, with a manager or the operations executive in post, is assigned, in the firms' morning");
    }

    // A licence far over its share loses customers to rivals by the day's seed, where the old cut-off lost none for a few over.
    private static void Tech23Drift()
    {
        var ff = new FirmsFile { Seed = 11 };
        var firm = new Firm { Place = "p", Reputation = 700 };
        long drift = 0, again = 0, parts = 0;
        for (int pass = 0; pass < 2; pass++)
        {
            for (int day = 0; day < 200; day++)
            {
                Product p = FirmsRules.NewProduct(ff, "p", ProductKind.Licence, Segment.Enterprise, 0, "L");
                p.Id = 5;
                p.Stage = 1;
                p.Features = 10;
                p.Users = 25;
                var m = new SectorMarketDay { Buyers = 100, All = 10, Who = (int)SectorBuyers.Enterprises };
                ProductDay r = SectorRules.SellDay(ff, firm, p, default(Crew), 0, day, m, 100, 100, 1000);
                if (pass == 0)
                {
                    drift += r.LostDrift;
                }
                else
                {
                    again += r.LostDrift;
                }
                parts += r.LostChurn + r.LostDrift + r.LostIncident == r.Lost ? 0 : 1;
            }
        }
        Check(drift > 0 && drift == again, "tech23 drift: a licence a few customers over its share loses some to rivals (" + drift + " in 200 days), the same on every machine");
        Check(parts == 0, "tech23 drift: what is lost is the churn, the drift and an outage's together, nothing else");
    }

    // The product's page: yesterday in words, why churn is high, what is left of the market and what one more feature does; a licence's advice keeps to levers it has left.
    private static void Tech23Page()
    {
        Product lic = TechPointsSold(15, 0, 0, 40, out _, out SectorFile f, out _, out _);
        SectorMarketDay md = f.Markets[(int)SectorMarket.Licences];
        var firm = new Firm { Place = "p", Reputation = 700 };
        Crew c = Team(16);
        int appeal = SectorRules.Appeal(lic, firm, c, md), aware = SectorRules.Awareness(FirmsCatalogue.MarketingDay[0], md.Buyers, SectorRules.Have(lic), md.Who, 0);
        var fmt = new SectorWords();
        lic.LastDay = -1;
        ViewSection none = SectorViews.Market(f, lic, appeal, aware, fmt, 0);
        Check(TechPointsLine(none, "reallife_tech_mkt_product_yesterday") == null, "tech23 page: a product through no day yet says nothing of yesterday");
        lic.Last = new ProductDay { Won = 2, Lost = 5, LostChurn = 3, LostDrift = 1, LostIncident = 1, ChurnWhy = SectorRules.WhySupport | SectorRules.WhyBehind };
        lic.LastDay = 10;
        ViewSection sec = SectorViews.Market(f, lic, appeal, aware, fmt, 0);
        ViewLine y = TechPointsLine(sec, "reallife_tech_mkt_product_yesterday"), why = TechPointsLine(sec, "reallife_tech_mkt_product_why"), left = TechPointsLine(sec, "reallife_tech_mkt_product_left");
        ViewLine feat = TechPointsLine(sec, "reallife_tech_mkt_product_features_taken") ?? TechPointsLine(sec, "reallife_tech_mkt_product_features_room");
        Check(y != null && TechPointsPair(y, "won") == "2" && TechPointsPair(y, "lost") == "5" && TechPointsPair(y, "churn") == "3" && TechPointsPair(y, "drift") == "1" && TechPointsPair(y, "incident") == "1",
            "tech23 page: yesterday's customers won and lost are said with the cause of each loss");
        Check(why != null && TechPointsPair(why, "why").Contains(",") && left != null && int.Parse(TechPointsPair(left, "left")) >= 0 && feat != null && TechPointsPair(feat, "top") == "200%",
            "tech23 page: the causes of the churn, what is left of the market and what one more feature does (features never count past 200%) are said");
        Check(TechPointsFills(y, LocaleFile("en")) && TechPointsFills(why, LocaleFile("en")) && TechPointsFills(left, LocaleFile("en")) && TechPointsFills(feat, LocaleFile("en")),
            "tech23 page: every new line's names are exactly its text's");
        // levers: marketing at its top and the lowest price are not asked for again
        lic.Users = 100000;
        lic.Marketing = FirmsCatalogue.MarketingDay.Length - 1;
        lic.Tier = 0;
        ViewSection both = SectorViews.Market(f, lic, appeal, aware, fmt, 0);
        lic.Marketing = 0;
        lic.Tier = 1;
        ViewSection open = SectorViews.Market(f, lic, appeal, aware, fmt, 0);
        Check(TechPointsLine(both, "reallife_tech_mkt_product_deals_full_both") != null && TechPointsLine(open, "reallife_tech_mkt_product_deals_full") != null,
            "tech23 page: a licence with its marketing at the top and its price the lowest is not told to market and price more");
    }

    private static void Tech23Studio()
    {
        int offline = Array.IndexOf(StudioCatalogue.Online, false);
        int online = Array.IndexOf(StudioCatalogue.Online, true);
        var once = new GameDev { Genre = offline, Active = 2000000, Model = (int)GameModel.Premium };
        var live = new GameDev { Genre = online, Active = 2000000, Model = (int)GameModel.Premium };
        Check(StudioRules.Tickets(once) * StudioCatalogue.TicketsPerThousand == StudioRules.Tickets(live) * StudioCatalogue.TicketsPerThousandOnce && StudioRules.Tickets(once) < StudioRules.Tickets(live),
            "tech23 studio: a game bought once and played alone raises a fifth of an online game's tickets");
        Check(StudioRules.Answers(3) == 180 && StudioRules.SupportFor(180) == 3 && StudioRules.SupportFor(181) == 4 && StudioRules.SupportFor(0) == 0 && StudioRules.Answers(-2) == 0,
            "tech23 studio: one support person answers " + StudioCatalogue.TicketsPerSupport + " tickets and the people a day's tickets need are rounded up");
        // the campaign waits for the run-up to launch, then spends its share and keeps hype rising
        GameDev g = MadeGame(70, Genre.Action, Platform.Pc, ScaleCatalogue.BudgetTypical[1], 60, 0, 0);
        g.Phase = (int)GamePhase.Production;
        g.Campaign = 3;
        g.CampaignSpent = 0;
        g.LastNews = 0;
        long early = StudioRules.HypeDay(g, 5, StudioCatalogue.CampaignDays + 100);
        long near = StudioRules.HypeDay(g, 6, StudioCatalogue.CampaignDays);
        Check(early == 0 && g.CampaignSpent == near && near == g.Budget * StudioCatalogue.CampaignShare[3] / 1000 / StudioCatalogue.CampaignDays && g.Hype == StudioCatalogue.CampaignHype * 3,
            "tech23 studio: a campaign spends nothing a year before launch and starts in the last " + StudioCatalogue.CampaignDays + " days, adding its hype");
        Check(StudioRules.HypeDay(new GameDev { Budget = g.Budget, Campaign = 3, Launched = -1 }, 9, -1) == 0, "tech23 studio: a game nobody builds has no launch in view and spends nothing");
        var building = new GameDev { Phase = (int)GamePhase.Production, Work = 1000, Done = 0, ReleaseDay = 0 };
        Check(StudioRules.LaunchIn(building, 10, 10) == 100 && StudioRules.LaunchIn(building, 10, 0) == -1 && StudioRules.LaunchIn(new GameDev { Launched = 3 }, 10, 10) == -1,
            "tech23 studio: the days to launch are the work left at the team's pace, none in view with nobody building, none for a game that is out");
        building.ReleaseDay = 400;
        Check(StudioRules.LaunchIn(building, 10, 10) == 390, "tech23 studio: a release day set later than the work is the launch");
        Check(StudioRules.PeopleFor(8 * 365, 365) == 1 && StudioRules.PeopleFor(8 * 365 + 1, 365) == 2, "tech23 studio: the people a budget's work asks for in a year are rounded up");
        // sources: the desk session names its game, a withdrawn product can be removed, the support figures agree on the page
        Check(ModSource("Sessions/SessionPanel.cs").Contains("SubjectOf(def)") && ModSource("Wiring/Wiring.StudioLinks.cs").Contains("StudioSubject(kind, ask.Place)")
              && MemberText(ModSource("Tech/TechDirector.StudioDesk.cs"), "internal string StudioSubject(int kind, string place)").Contains("LiveGame(place)"),
            "tech23 studio: the post-launch review's desk page names the game it is about");
        Check(MemberText(ModSource("Tech/TechDirector.FirmsApp.cs"), "internal void Forget(int id)").Contains("p.Stage < 2") && ModSource("Tech/Studio/StudioViews.cs").Contains("StudioAct.Forget"),
            "tech23 studio: only a withdrawn product can be removed from the list, a game's record with it");
        var screen = StudioViews.Studio(new StudioFile(), new StudioPlace { Place = StudioPlace, Name = "Kestrel Works", Tickets = 600, Answered = 120, Support = 2 }, 900, new SectorWords());
        bool agree = false;
        foreach (ViewLine l in screen.Sections[0].Lines)
        {
            agree |= l.Key == "reallife_studio_studio_support" && TechPointsPair(l, "have") == "2" && TechPointsPair(l, "need") == "10";
        }
        Check(agree, "tech23 studio: the studio's page says the support people it has of the ones its tickets need");
    }

    private static void Tech23Executives()
    {
        string dir = ModSource("Economy/Company/CompanyDirector.Executives.cs");
        string cover = MemberText(dir, "private void ExecutivesCover(int day)");
        Check(cover.Contains("ExecutiveAtWork(ExecSeat.Chro)") && cover.Contains("PolicyField.AutoOffers") && cover.Contains("PolicyField.AutoRaises") && cover.Contains("PolicyField.PayBand")
              && cover.Contains("PolicyField.RaiseMost") && cover.Contains("PolicyField.RaiseEvery") && cover.Contains("UnmetDue"),
            "tech23 executives: the people chief answers what waits at a place with no manager by the policy's switches, band, largest raise, days between raises and days a request may wait");
        Check(cover.Contains("Settings.EconomyCompanyMatchRivals && RivalMatchRules.WithinOffer") && cover.Contains("!Manages(f.Place, ManagerTask.Offers"),
            "tech23 executives: an offer the match option can take, or a manager answers, is never answered a second time");
        Check(ModSource("Economy/Company/CompanyDirector.Escalations.cs").Contains("ExecutivesCover(day);") && ModSource("Economy/Company/CompanyDirector.Escalations.cs").Contains("ExecutivesTell(day);"),
            "tech23 executives: the covering and the morning notice run in the hierarchy's day");
        Check(MemberText(dir, "private void ExecutivesTell(int day)").Contains("ReportDaily"), "tech23 executives: the morning notice is not sent when the policy reports only what needs the owner");
        Check(ModSource("Ui/Company/CompanyLive.Managers.cs").Contains("ExecutiveDid(x, d.Today - 1") && ModSource("Ui/Company/CompanyLive.ManagersSub.cs").Contains("def.TextKey + \"_when\""),
            "tech23 executives: an executive's page says what they did since yesterday and why nothing came, and each rule's page what has to be so for it to act");
        Dictionary<string, string> en = LocaleFile("en");
        bool all = true;
        foreach (var d in RealLifeOverhaul.Economy.PolicyCatalogue.Defs)
        {
            all &= en.ContainsKey(d.TextKey + "_when");
        }
        Check(all, "tech23 executives: every company rule has its conditions in words");
    }

    private static void Tech23Texts()
    {
        Dictionary<string, string> en = LocaleFile("en");
        var wanted = new Dictionary<string, string[]>
        {
            { "reallife_tech_mkt_product_yesterday", new[] { "{won}", "{lost}", "{churn}", "{drift}", "{incident}" } },
            { "reallife_tech_mkt_product_why", new[] { "{why}" } },
            { "reallife_tech_mkt_product_why_base", new string[0] },
            { "reallife_tech_mkt_product_left", new[] { "{buyers}", "{mine}", "{rivals}", "{left}" } },
            { "reallife_tech_mkt_product_features_taken", new[] { "{now}", "{top}", "{target}", "{next}" } },
            { "reallife_tech_mkt_product_features_room", new[] { "{now}", "{top}", "{target}", "{next}" } },
            { "reallife_tech_mkt_product_deals_full", new[] { "{target}", "{who}", "{have}" } },
            { "reallife_tech_mkt_product_deals_full_price", new[] { "{target}", "{who}", "{have}" } },
            { "reallife_tech_mkt_product_deals_full_marketing", new[] { "{target}", "{who}", "{have}", "{reach}" } },
            { "reallife_tech_mkt_product_deals_full_both", new[] { "{target}", "{who}", "{have}", "{reach}" } },
            { "reallife_session_title_about", new[] { "{session}", "{subject}" } },
            { "reallife_studio_campaign_trade", new[] { "{cost}", "{days}", "{hype}", "{spent}" } },
            { "reallife_studio_campaign_trade_none", new[] { "{days}" } },
            { "reallife_studio_budget_trade", new[] { "{points}", "{days}", "{people}", "{have}", "{usual}" } },
            { "reallife_studio_studio_support", new[] { "{tickets}", "{answered}", "{have}", "{need}" } },
            { "reallife_tech_assign_note", new[] { "{moves}" } },
            { "reallife_tech_assign_firm", new[] { "{firm}", "{by}", "{moves}" } },
            { "reallife_tech_assign_to_support", new[] { "{name}", "{role}" } },
            { "reallife_tech_assign_to_sales", new[] { "{name}", "{role}" } },
            { "reallife_tech_assign_by_support", new[] { "{by}", "{day}" } },
            { "reallife_tech_assign_by_sales", new[] { "{by}", "{day}" } },
            { "reallife_hier_note_exec_day", new[] { "{count}", "{who}" } },
            { "reallife_hier_exec_did", new[] { "{settled}", "{passed}" } }
        };
        foreach (KeyValuePair<string, string[]> k in wanted)
        {
            bool has = en.TryGetValue(k.Key, out string text);
            var found = new HashSet<string>();
            foreach (Match m in Regex.Matches(has ? text : string.Empty, "\\{[a-z_0-9]+\\}"))
            {
                found.Add(m.Value);
            }
            bool same = has && found.SetEquals(k.Value) && text.IndexOf('!') < 0 && text.IndexOf('—') < 0 && text.IndexOf('–') < 0 && text.Length <= (k.Key.Contains("note") ? 240 : 600);
            Check(same, "tech23 texts: " + k.Key + " has exactly its names, no dash or exclamation mark");
        }
        string guide = en.TryGetValue("reallife_guide_firms", out string g1) ? g1 : string.Empty;
        string sector = en.TryGetValue("reallife_guide_tech_sector", out string g2) ? g2 : string.Empty;
        Check(guide.Contains("{firms_features_top}") && guide.Contains("{firms_features_ceiling}") && sector.Contains("no cap on features") && ModSource("Tech/Sector/SectorGuide.cs").Contains("firms_features_ceiling"),
            "tech23 texts: the Guide says features have no cap with Tech firms rebuilt, that the code's ceiling is no limit, and that a licence is bought once");
    }
}
