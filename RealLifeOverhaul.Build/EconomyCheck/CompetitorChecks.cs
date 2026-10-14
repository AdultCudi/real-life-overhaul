using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Tech;

// Checks for the competitors' rule (Scripts/Tech/Firms/CompetitorRules), the one rule BizMan's start screen and the Tech
// app's Competitors page both count by: the companies against the stock market and the products' rivals, who competes with
// each kind, the startups, the schools, the businesses, the firm itself left out, the council's tenders' named bidders and
// their bids (the very ones a tender is decided by), a product's share of its market, the speed, and the words.
internal static partial class Program
{
    private static void Competitors()
    {
        CompetitorCompanies();
        CompetitorKinds();
        CompetitorGather();
        CompetitorTenders();
        CompetitorShare();
        CompetitorSpeed();
        CompetitorWords();
    }

    private static void CompetitorCompanies()
    {
        int companies = HiringCatalogue.RivalFirms.Length;
        Check(CompetitorRules.CompanyStock.Length == companies && CompetitorRules.CompanyMakes.Length == companies && CompetitorRules.CompanyTakes.Length == companies,
            "competitors: a stock, what it makes and what it takes on for each of the " + companies + " tech companies");
        bool listed = true;
        for (int c = 0; c < companies && c < CompetitorRules.CompanyStock.Length; c++)
        {
            CompanyDef d = MarketCatalogue.Find(CompetitorRules.CompanyStock[c]);
            listed &= d != null && d.Name == HiringCatalogue.RivalFirms[c] && CompetitorRules.CompanyOfStock(CompetitorRules.CompanyStock[c]) == c;
        }
        Check(listed, "competitors: each tech company is the stock market's company of that name");
        bool rivals = true;
        for (int k = 0; k < (int)ProductKind.Count; k++)
        {
            for (int s = 0; s < (int)Segment.Count; s++)
            {
                var f = new FirmsFile { Seed = 5 };
                Product p = FirmsRules.NewProduct(f, "p", (ProductKind)k, (Segment)s, 0, "Test");
                int c = CompetitorRules.RivalOf(p);
                rivals &= c >= 0 && (CompetitorRules.CompanyMakes[c] & (1 << k)) != 0 && FirmsCatalogue.RivalName(FirmsCatalogue.RivalStock(p.K, p.S)) == HiringCatalogue.RivalFirms[c];
            }
        }
        Check(rivals, "competitors: every product kind and segment's rival is one of the tech companies, and it makes that kind of product");
        Check(CompetitorRules.RivalOf(null) == -1 && CompetitorRules.CompanyOfStock(999) == -1, "competitors: no rival for nothing, no company for a stock that is none of theirs");
    }

    private static void CompetitorKinds()
    {
        bool six = true;
        for (int k = 0; k < CompetitorRules.Kinds; k++)
        {
            bool mods = k <= (int)FirmKind.AiLab || k == CompetitorRules.Academy;
            six &= CompetitorRules.InCity(k) == mods;
        }
        Check(six && !CompetitorRules.InCity(-1) && !CompetitorRules.InCity(CompetitorRules.Kinds), "competitors: the start screen says the city's competitors for the mod's own six kinds alone; the web agency and the graphic designer keep the game's count");
        Check(CompetitorRules.StartupSectors.Length == CompetitorRules.Kinds, "competitors: the startups each kind competes with, for every kind");
        bool symmetric = true, self = true, academy = true;
        for (int a = 0; a < CompetitorRules.Kinds; a++)
        {
            for (int b = 0; b < CompetitorRules.Kinds; b++)
            {
                symmetric &= CompetitorRules.Competes(a, b) == CompetitorRules.Competes(b, a);
                academy &= (a == CompetitorRules.Academy || b == CompetitorRules.Academy) ? CompetitorRules.Competes(a, b) == (a == b) : true;
            }
            self &= CompetitorRules.Competes(a, a) == (a == CompetitorRules.Academy || CompetitorRules.Makes(a) != 0 || CompetitorRules.Takes(a) != 0);
        }
        Check(symmetric && self && academy, "competitors: two kinds compete both ways, a kind with anything to sell with its own, an academy with academies alone");
        Check(!CompetitorRules.Competes(-1, 0) && !CompetitorRules.Competes(0, CompetitorRules.Kinds), "competitors: no kind out of range competes");
        Check(CompetitorRules.Competes((int)FirmKind.Software, (int)FirmKind.Consultancy) && CompetitorRules.Competes((int)FirmKind.Software, (int)FirmKind.WebAgency)
              && !CompetitorRules.Competes((int)FirmKind.Games, (int)FirmKind.Software) && !CompetitorRules.Competes((int)FirmKind.Design, (int)FirmKind.Software),
            "competitors: a software studio meets a consultancy and a web agency, a game studio and a graphic designer meet neither");
        int[] companies = new int[CompetitorRules.Kinds];
        for (int k = 0; k < CompetitorRules.Kinds; k++)
        {
            for (int c = 0; c < HiringCatalogue.RivalFirms.Length; c++)
            {
                companies[k] += CompetitorRules.CompanyCompetes(k, c) ? 1 : 0;
            }
        }
        bool eachTech = true;
        for (int k = 0; k <= (int)FirmKind.AiLab; k++)
        {
            eachTech &= companies[k] >= 1;
        }
        Check(eachTech, "competitors: each of the tech sector's five kinds has at least one tech company against it (" + string.Join(", ", companies) + ")");
        Check(companies[(int)FirmKind.Games] == 1 && CompetitorRules.CompanyCompetes((int)FirmKind.Games, Array.IndexOf(HiringCatalogue.RivalFirms, "Pixel Harbour Games")),
            "competitors: a game studio is up against Pixel Harbour Games alone of the companies");
        Check(companies[CompetitorRules.Academy] == 0 && !CompetitorRules.CompanyCompetes(CompetitorRules.Academy, 0), "competitors: no tech company competes with an academy");
        bool meet = true;
        for (int k = 0; k < (int)FirmKind.Count; k++)
        {
            for (int c = 0; c < HiringCatalogue.RivalFirms.Length; c++)
            {
                meet &= (CompetitorRules.Meet(k, CompetitorRules.CompanyMakes[c], CompetitorRules.CompanyTakes[c]) != 0) == CompetitorRules.CompanyCompetes(k, c);
            }
        }
        Check(meet, "competitors: where a firm and a company meet is something exactly when they compete");
        Check(CompetitorRules.StartupCompetes((int)FirmKind.AiLab, (int)Venture.Ai) && !CompetitorRules.StartupCompetes((int)FirmKind.AiLab, (int)Venture.Games)
              && CompetitorRules.StartupCompetes((int)FirmKind.Security, (int)Venture.Security) && !CompetitorRules.StartupCompetes(CompetitorRules.Academy, (int)Venture.Software)
              && !CompetitorRules.StartupCompetes((int)FirmKind.Software, -1) && !CompetitorRules.StartupCompetes((int)FirmKind.Software, (int)Venture.Count),
            "competitors: a startup competes by what it does");
    }

    private static Startup RivalStartup(int id, Venture sector, StartupState state)
    {
        return new Startup { Id = id, Name = "S" + id, Sector = (int)sector, State = (int)state };
    }

    private static void CompetitorGather()
    {
        var startups = new List<Startup>
        {
            RivalStartup(1, Venture.Ai, StartupState.Running), RivalStartup(2, Venture.Ai, StartupState.Failed), RivalStartup(3, Venture.Ai, StartupState.Raising),
            RivalStartup(4, Venture.Games, StartupState.Running), RivalStartup(5, Venture.Software, StartupState.Acquired), null
        };
        var firms = new List<FirmSeen>
        {
            new FirmSeen { Kind = (int)FirmKind.AiLab, Place = "a~1", Name = "Mine" },
            new FirmSeen { Kind = (int)FirmKind.AiLab, Place = "a~2", Name = "Theirs", Theirs = true },
            new FirmSeen { Kind = (int)FirmKind.Games, Place = "a~3", Name = "Games" },
            new FirmSeen { Kind = CompetitorRules.Academy, Place = "a~4", Name = "Academy" }
        };
        var schools = new List<string> { "Manhattan Business School", "Hudson City University" };
        int companies = 0;
        for (int c = 0; c < HiringCatalogue.RivalFirms.Length; c++)
        {
            companies += CompetitorRules.CompanyCompetes((int)FirmKind.AiLab, c) ? 1 : 0;
        }
        var into = new List<Competitor>();
        var pool = new List<Competitor>();
        int n = CompetitorRules.Gather((int)FirmKind.AiLab, startups, schools, firms, "a~1", into, pool);
        Check(n == companies + 2 + 1 && into.Count == n, "competitors: an AI lab's are the companies, the AI startups that run (not one that failed) and the other AI labs, but itself (" + n + ")");
        Check(into[0].Kind == (int)RivalKind.Company && into[companies].Kind == (int)RivalKind.Startup && into[companies].Startup == 1 && into[companies + 1].Startup == 3
              && into[n - 1].Kind == (int)RivalKind.Firm && into[n - 1].Theirs && into[n - 1].Place == "a~2",
            "competitors: companies first, then startups, then firms, another player's said so");
        Check(CompetitorRules.Gather((int)FirmKind.AiLab, startups, schools, firms, "a~1", null, null) == n, "competitors: BizMan's count is the length of the Tech app's list");
        Check(CompetitorRules.Gather((int)FirmKind.AiLab, startups, schools, firms, null, null, null) == n + 1, "competitors: on the start screen nothing is left out, the player's own lab counts");
        int academy = CompetitorRules.Gather(CompetitorRules.Academy, startups, schools, firms, null, into, pool);
        Check(academy == 3 && into[0].Kind == (int)RivalKind.School && into[1].Name == "Hudson City University" && into[2].Kind == (int)RivalKind.Firm,
            "competitors: an academy's are the two schools and the other academies (" + academy + ")");
        Check(CompetitorRules.Gather(CompetitorRules.Academy, null, null, null, null, null, null) == 0, "competitors: an academy with no school and no academy about has none");
        Check(CompetitorRules.Gather((int)FirmKind.Games, null, null, null, null, null, null) == 1, "competitors: a game studio in an empty city has one, Pixel Harbour Games");
        Check(CompetitorRules.Gather(-1, startups, schools, firms, null, into, pool) == 0 && into.Count == 0 && CompetitorRules.Gather(CompetitorRules.Kinds, startups, schools, firms, null, null, null) == 0,
            "competitors: a kind out of range has none");
        CompetitorRules.Gather((int)FirmKind.AiLab, startups, schools, firms, "a~1", into, pool);
        pool.AddRange(into);
        into.Clear();
        int spare = pool.Count;
        CompetitorRules.Gather((int)FirmKind.AiLab, startups, schools, firms, "a~1", into, pool);
        Check(pool.Count == spare - n && into[0].Building == null && into[0].Company >= 0, "competitors: a list made again takes its entries from the pool, emptied of what they were");
        var c1 = new Competitor { Kind = (int)RivalKind.Company, Company = 3 };
        var c2 = new Competitor { Kind = (int)RivalKind.Startup, Startup = 3 };
        var c3 = new Competitor { Kind = (int)RivalKind.Firm, Place = "a~1" };
        Check(c1.Key != c2.Key && c2.Key != c3.Key && c1.Key == "c3" && c3.Key == "fa~1", "competitors: each competitor of a firm has an id of its own");
    }

    private static void CompetitorTenders()
    {
        Check(CompetitorRules.Takers(WorkKind.Integration) >= 4 && CompetitorRules.Takers(WorkKind.Managed) >= 4,
            "competitors: at least four companies take on each kind of the council's tenders, the most rivals one has (" + CompetitorRules.Takers(WorkKind.Integration) + ", " + CompetitorRules.Takers(WorkKind.Managed) + ")");
        bool named = true, distinct = true, takes = true, same = true;
        var sets = new HashSet<string>();
        for (int id = 1; id <= 200; id++)
        {
            var t = new Contract { Id = id, Kind = (int)(id % 2 == 0 ? WorkKind.Integration : WorkKind.Managed), Council = true, Rivals = 2 + id % 3, Stage = (int)WorkStage.Tender, Points = 100 };
            var seen = new HashSet<int>();
            string set = string.Empty;
            for (int r = 0; r < t.Rivals; r++)
            {
                int c = CompetitorRules.TenderRival(9, t, r);
                named &= c >= 0;
                if (c < 0)
                {
                    continue;
                }
                distinct &= seen.Add(c);
                takes &= (CompetitorRules.CompanyTakes[c] & (1 << t.Kind)) != 0;
                same &= CompetitorRules.TenderRival(9, t, r) == c && CompetitorRules.BidderOf(9, t, c) == r;
                set += c + ",";
            }
            sets.Add(t.Kind + ":" + set);
        }
        Check(named && distinct && takes, "competitors: a tender's rival bidders are that many different companies, each taking on its kind of work");
        Check(same, "competitors: the same tender has the same bidders every time");
        Check(sets.Count > 4, "competitors: different tenders have different bidders (" + sets.Count + " sets)");
        Check(CompetitorRules.TenderRival(9, new Contract { Id = 1, Kind = (int)WorkKind.Branding, Council = true, Rivals = 2 }, 0) == -1 && CompetitorRules.TenderRival(9, null, 0) == -1,
            "competitors: no bidder for work no company takes on");

        // The rival's bid is the one the tender is decided by: the old formula, the same answer.
        bool sameBid = true;
        for (int id = 1; id <= 300; id++)
        {
            var t = new Contract { Id = id, Kind = (int)WorkKind.Integration, Value = 4000000 + id * 1000, Rivals = 3, Stage = (int)WorkStage.Bid };
            for (int r = 0; r < t.Rivals; r++)
            {
                sameBid &= FirmsRules.RivalBid(17, t, r, t.Value) == t.Value * TechRandom.Range(17, t.Id, 81, r, 850, 1250) / 1000;
            }
        }
        Check(sameBid, "competitors: a rival's bid is what the tender has always been decided by");
        var f = new FirmsFile { Seed = 21 };
        var firm = new Firm { Place = "p~1", Kind = (int)FirmKind.Consultancy, Reputation = 900 };
        var other = new Firm { Place = "p~2", Kind = (int)FirmKind.Games };
        long fair = FirmsRules.ValueOf(WorkKind.Integration, 300, true);
        var won = new Contract { Id = 7, Kind = (int)WorkKind.Integration, Council = true, Points = 300, Value = fair, Rivals = 2, Stage = (int)WorkStage.Tender, End = 50 };
        int company = CompetitorRules.TenderRival(f.Seed, won, 0);
        Check(CompetitorRules.BothBid(f.Seed, won, firm, company) && !CompetitorRules.BothBid(f.Seed, won, other, company), "competitors: an open tender is one both bid for when the firm takes on its work");
        Check(!CompetitorRules.BothBid(f.Seed, won, firm, -1) && !CompetitorRules.BothBid(f.Seed, null, firm, company) && !CompetitorRules.BothBid(f.Seed, won, null, company), "competitors: nothing with nobody");
        int notBidding = -1;
        for (int c = 0; c < HiringCatalogue.RivalFirms.Length && notBidding < 0; c++)
        {
            notBidding = CompetitorRules.BidderOf(f.Seed, won, c) < 0 ? c : -1;
        }
        Check(notBidding >= 0 && !CompetitorRules.BothBid(f.Seed, won, firm, notBidding), "competitors: a company that does not bid is not one both bid for");
        won.Bid = fair / 2;
        won.Place = firm.Place;
        won.Stage = (int)WorkStage.Bid;
        Check(CompetitorRules.BothBid(f.Seed, won, firm, company) && !CompetitorRules.Decided(won), "competitors: a bid in, not yet decided");
        bool decided = FirmsRules.Decide(f, won, firm, 50);
        if (decided)
        {
            FirmsRules.Take(won, firm.Place, 50);
        }
        Check(decided && CompetitorRules.Decided(won) && CompetitorRules.BothBid(f.Seed, won, firm, company), "competitors: a low bid wins, and the tender is still one both bid for, decided");
        Check(FirmsRules.FairOf(won) == fair && won.Value == won.Bid, "competitors: a won tender's fair price is the council's again, so the rival's bid shown is the one it lost with");
        var lost = new Contract { Id = 8, Kind = (int)WorkKind.Integration, Council = true, Points = 300, Value = fair, Rivals = 4, Stage = (int)WorkStage.Bid, Bid = fair * 2, Place = firm.Place };
        Check(!FirmsRules.Decide(f, lost, firm, 50) && CompetitorRules.Decided(lost) && FirmsRules.FairOf(lost) == fair, "competitors: a dear bid loses, and its fair price is its value");
        var lapsed = new Contract { Id = 9, Kind = (int)WorkKind.Integration, Council = true, Points = 300, Value = fair, Rivals = 4, Stage = (int)WorkStage.Lost };
        Check(!CompetitorRules.Decided(lapsed) && !CompetitorRules.BothBid(f.Seed, lapsed, firm, CompetitorRules.TenderRival(f.Seed, lapsed, 0)), "competitors: a tender the firm never bid for is not one both bid for");
        var offer = new Contract { Id = 10, Kind = (int)WorkKind.Integration, Rivals = 0, Stage = (int)WorkStage.Offered };
        Check(CompetitorRules.BidderOf(f.Seed, offer, company) == -1, "competitors: a client's offer has no rival bidders");
    }

    private static void CompetitorShare()
    {
        var f = new FirmsFile { Seed = 2 };
        Product saas = FirmsRules.NewProduct(f, "p", ProductKind.Saas, Segment.Business, 0, "T");
        Check(CompetitorRules.SharePpm(saas) == 0 && CompetitorRules.SharePpm(null) == 0, "competitors: no customers, no share");
        saas.Users = FirmsCatalogue.Market[(int)Segment.Business] / 4;
        Check(CompetitorRules.SharePpm(saas) == 250000, "competitors: a quarter of the businesses is a quarter of the market");
        saas.Users = FirmsCatalogue.Market[(int)Segment.Business] * 3;
        Check(CompetitorRules.SharePpm(saas) == 1000000, "competitors: never more than the whole market");
        Product app = FirmsRules.NewProduct(f, "p", ProductKind.App, Segment.Consumer, 0, "A");
        app.Users = 1000;
        app.Free = 19000;
        Check(CompetitorRules.SharePpm(app) == 20000L * 1000000 / FirmsCatalogue.Market[(int)Segment.Consumer], "competitors: a consumer service's free users are in its share");
        Product game = FirmsRules.NewProduct(f, "p", ProductKind.Game, Segment.Consumer, 1, "G");
        game.Users = FirmsCatalogue.GamePlayers / 10;
        Check(CompetitorRules.SharePpm(game) == 100000, "competitors: a game's share is of the players there are");
    }

    private static void CompetitorSpeed()
    {
        var startups = new List<Startup>();
        for (int i = 0; i < 20; i++)
        {
            startups.Add(RivalStartup(i, (Venture)(i % (int)Venture.Count), StartupState.Running));
        }
        var firms = new List<FirmSeen>();
        for (int i = 0; i < 40; i++)
        {
            firms.Add(new FirmSeen { Kind = i % CompetitorRules.Kinds, Place = "p~" + i, Name = "F" + i });
        }
        var schools = new List<string> { "A", "B" };
        var watch = Stopwatch.StartNew();
        int n = 0;
        for (int i = 0; i < 20000; i++)
        {
            n += CompetitorRules.Gather(i % CompetitorRules.Kinds, startups, schools, firms, null, null, null);
        }
        Check(n > 0 && watch.Elapsed.TotalMilliseconds < 300, "competitors: a count is quick (" + watch.Elapsed.TotalMilliseconds.ToString("F1") + " ms for 20,000)");
    }

    private static readonly string[] CompetitorKeys =
    {
        "reallife_bizman_competitors_city", "reallife_contract_part_start_competitors", "reallife_tech_button_rivals", "reallife_tech_col_rival", "reallife_tech_col_meet",
        "reallife_tech_col_tenders", "reallife_tech_col_where", "reallife_tech_kind_companies", "reallife_tech_kind_startups", "reallife_tech_kind_firms", "reallife_tech_rivals_all",
        "reallife_tech_empty_rivals", "reallife_tech_rival_company", "reallife_tech_rival_startup", "reallife_tech_rival_yours", "reallife_tech_rival_theirs", "reallife_tech_rivals_title",
        "reallife_tech_rivals_count", "reallife_tech_rivals_what", "reallife_tech_rivals_how", "reallife_tech_rival_title", "reallife_tech_rival_product", "reallife_tech_rival_game",
        "reallife_tech_rival_more", "reallife_tech_rival_no_products", "reallife_tech_rival_tender_open", "reallife_tech_rival_tender_bid", "reallife_tech_rival_tender_decided",
        "reallife_tech_rival_no_tenders", "reallife_tech_rival_at", "reallife_tech_rival_no_office", "reallife_tech_rival_startup_where", "reallife_tech_rival_firm_at",
        "reallife_tech_rival_floor", "reallife_tech_button_open_startup", "reallife_tech_contract_rivals_named", "reallife_tech_test_tender"
    };

    private static void CompetitorWords()
    {
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales"));
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(folder, "en.json"));
        Dictionary<string, string> pt = LoadWords(System.IO.Path.Combine(folder, "pt.json"));
        Dictionary<string, string> zh = LoadWords(System.IO.Path.Combine(folder, "zh-cn.json"));
        Check(en != null && pt != null && zh != null, "competitors: the three locales are where the checks look for them (" + folder + ")");
        if (en == null || pt == null || zh == null)
        {
            return;
        }
        var missing = new List<string>();
        var holes = new Regex(@"\{[a-z_]+\}");
        foreach (string key in CompetitorKeys)
        {
            if (!en.TryGetValue(key, out string e) || !pt.TryGetValue(key, out string p) || !zh.TryGetValue(key, out string z))
            {
                missing.Add(key);
                continue;
            }
            string want = Holes(holes, e);
            if (Holes(holes, p) != want || Holes(holes, z) != want)
            {
                missing.Add(key + " (its {names} differ)");
            }
        }
        Check(missing.Count == 0, "competitors: every text is in en.json, pt.json and zh-cn.json with the same {names} (" + CompetitorKeys.Length + " keys" + (missing.Count > 0 ? "; " + string.Join(", ", missing) : string.Empty) + ")");
        string button = en.TryGetValue("reallife_tech_button_rivals", out string b) ? b : "?";
        bool guide = en.TryGetValue("reallife_guide_tech_sector", out string g) && g.Contains("<b>" + button + "</b>")
                     && pt.TryGetValue("reallife_guide_tech_sector", out string gp) && gp.Contains("<b>" + pt["reallife_tech_button_rivals"] + "</b>")
                     && zh.TryGetValue("reallife_guide_tech_sector", out string gz) && gz.Contains("<b>" + zh["reallife_tech_button_rivals"] + "</b>");
        Check(guide, "competitors: the Guide's tech sector page names the Competitors button as the app does, in each language");
        // In the next row until its version is made, and in that version's own row after (the release empties the next row).
        bool line = false;
        foreach (KeyValuePair<string, string> row in en)
        {
            if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && row.Value.Contains("Competitors page"))
            {
                line = !row.Value.StartsWith("New: ", StringComparison.Ordinal);
                break;
            }
        }
        Check(line, "competitors: What's new has the feature's line, with no prefix (in the next row, or its version's row once it is made)");
    }

    private static string Holes(Regex holes, string text)
    {
        var found = new List<string>();
        foreach (Match m in holes.Matches(text))
        {
            found.Add(m.Value);
        }
        found.Sort(StringComparer.Ordinal);
        return string.Join(",", found);
    }
}
