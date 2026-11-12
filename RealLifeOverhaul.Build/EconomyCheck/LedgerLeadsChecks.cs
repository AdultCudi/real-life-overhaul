using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using RealLifeOverhaul.Ledger;
using RealLifeOverhaul.Markets;

// A sector's lead changing hands in the City Ledger, and a rival tycoon's holding told as a player's company is
// (Scripts/Ledger: LedgerLeads, LedgerStories, LedgerDirector.Stories, Wiring/LedgerLinks): what each story weighs, a rival's
// holding at every figure of the ladder and at the top, the margin and its edge, the story of each pair (a player's company, a
// rival's holding, a city company), a holder that left or moved sector, a company that is not major or new to the market,
// quiet for a guest, each change told once, the cap on the city's companies, a random walk of sectors held to the rules on every
// morning, the record in the save, the real market over two years, the sources and the texts. Run by --ledgerstories.
internal static partial class Program
{
    private static readonly Func<int, string> LeadSector = n => "Sector " + n.ToString(CultureInfo.InvariantCulture);

    private static void LedgerLeadsChecks()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        LeadKinds();
        LeadRivalsAlike();
        LeadTold();
        LeadEdges();
        LeadWalk();
        LeadStore();
        LeadMarket();
        LeadSources();
        LeadTexts();
        Console.WriteLine("  the City Ledger's sector leaders and a rival tycoon's holding told alike: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, "
                          + watch.ElapsedMilliseconds + " ms");
    }

    private static LedgerCompany LeadFirm(int id, string name, double value, int sector, string kind = "")
    {
        return new LedgerCompany { Id = id, Name = name, Value = value, Sector = sector, Player = kind == "player", Rival = kind == "rival" };
    }

    private static List<LedgerCompany> LeadField(params LedgerCompany[] rows)
    {
        return new List<LedgerCompany>(rows);
    }

    // A morning as the director works it: the sectors' leaders first, then the milestones, which bring up to date who was on the market.
    private static List<LedgerPiece> LeadMorning(LedgerState s, List<LedgerCompany> list, bool quiet, out LedgerStoryCount count)
    {
        var into = new List<LedgerPiece>();
        count = default(LedgerStoryCount);
        LedgerLeads.Pass(s, list, quiet, StoryMoney, LeadSector, into, ref count);
        LedgerStories.Marks(s, list, quiet, StoryMoney, into, ref count);
        return into;
    }

    // A state that has had its first morning with these companies: every sector set where it stands, nothing printed.
    private static LedgerState LeadSeeded(params LedgerCompany[] rows)
    {
        var s = new LedgerState();
        LeadMorning(s, LeadField(rows), false, out LedgerStoryCount first);
        return s;
    }

    private static List<LedgerPiece> LeadStoriesOf(List<LedgerPiece> pieces)
    {
        return pieces.Where(q => q.Key.StartsWith("reallife_ledger_lead", StringComparison.Ordinal)).ToList();
    }

    private static int LeadHolder(LedgerState s, int sector)
    {
        foreach (LedgerLead l in s.Leads)
        {
            if (l.Sector == sector)
            {
                return l.Id;
            }
        }
        return 0;
    }

    private static void LeadKinds()
    {
        int Weight(string key) => LedgerRules.KindOf(key).Weight;
        Kind(LedgerLeads.LeadOwn, LedgerSection.Markets, 84, "reallife_ledger_head_lead_own");
        Kind(LedgerLeads.LeadLost, LedgerSection.Markets, 79, "reallife_ledger_head_lead_lost");
        Kind(LedgerLeads.LeadRival, LedgerSection.Markets, 68, "reallife_ledger_head_lead_rival");
        Kind(LedgerLeads.LeadCity, LedgerSection.Markets, 54, "reallife_ledger_head_lead_city");
        Kind(LedgerStories.MileRival, LedgerSection.Markets, 76, "reallife_ledger_head_mile_rival");
        Kind(LedgerStories.TopRival, LedgerSection.Markets, 80, "reallife_ledger_head_top_rival");
        Check(Weight(LedgerLeads.LeadOwn) > Weight("reallife_mkt_news_bid") && Weight(LedgerLeads.LeadLost) > Weight("reallife_mkt_news_taken_over"),
            "sector leaders: a player's company taking a sector's lead, or losing it, outweighs the markets' bids and takeovers, so it leads the markets page");
        Check(Weight(LedgerLeads.LeadOwn) < Weight(LedgerStories.MileBig) && Weight(LedgerLeads.LeadOwn) < Weight(LedgerStories.TopFirst)
              && Weight(LedgerLeads.LeadOwn) < Weight(LedgerStories.BeatenKeys[0]),
            "sector leaders: a sector's lead weighs less than a billion, the first place in the city and a rival taken over, which lead the front page");
        Check(Weight(LedgerLeads.LeadOwn) > Weight(LedgerLeads.LeadRival) && Weight(LedgerLeads.LeadRival) > Weight(LedgerLeads.LeadCity)
              && Weight(LedgerLeads.LeadLost) > Weight(LedgerLeads.LeadRival),
            "sector leaders: a player's company weighs more than a rival tycoon's holding, and that more than one of the city's");
        Check(Weight(LedgerStories.MileOwn) > Weight(LedgerStories.MileRival) && Weight(LedgerStories.MileRival) > Weight(LedgerStories.MileCity)
              && Weight(LedgerStories.TopLost) > Weight(LedgerStories.TopRival) && Weight(LedgerStories.TopRival) > Weight(LedgerStories.TopCity),
            "rivals alike: a rival tycoon's milestone and first place weigh between a player's company's and a city company's");
        Check(LedgerStories.Kept(LedgerLeads.LeadOwn) && LedgerStories.Kept(LedgerLeads.LeadLost) && LedgerStories.Kept(LedgerLeads.LeadRival) && LedgerStories.Kept(LedgerLeads.LeadCity)
              && LedgerStories.Kept(LedgerStories.MileRival) && LedgerStories.Kept(LedgerStories.TopRival) && LedgerStories.Story(LedgerLeads.LeadCity),
            "sector leaders: every new story is read from the state once and kept when the edition is written again");
        Check(LedgerLeads.LeadBp == 500 && LedgerLeads.Floor == 100000000L && LedgerLeads.CityMost == 2 && LedgerLeads.SectorsMost >= (int)MarketSector.Count,
            "sector leaders: a company takes a lead 5% ahead, the company passed is worth 100 million at least, the city's companies are told twice a morning at most, every sector of the market has room in the record");
    }

    // A rival tycoon's holding passes each figure of the ladder and takes the top place with a story of its own; a city company has only its first billion.
    private static void LeadRivalsAlike()
    {
        var s = new LedgerState();
        Mark(s, LeadField(LeadFirm(1, "Big Co", 30e9, 0), LeadFirm(143, "Karen Aerospace", 200e6, 3, "rival"), LeadFirm(5, "Mid Co", 300e6, 3)), false, out LedgerStoryCount c);
        List<LedgerPiece> p = Mark(s, LeadField(LeadFirm(1, "Big Co", 30e9, 0), LeadFirm(143, "Karen Aerospace", 270e6, 3, "rival"), LeadFirm(5, "Mid Co", 520e6, 3)), false, out c);
        Check(p.Count == 1 && p[0].Key == LedgerStories.MileRival && p[0].Value("name") == "Karen Aerospace" && p[0].Value("mark") == "$250000000" && p[0].Value("value") == "$270000000"
              && c.Milestones == 1,
            "rivals alike: a rival tycoon's holding passing 250 million has a story of its own, and a city company passing 500 million has none");
        p = Mark(s, LeadField(LeadFirm(1, "Big Co", 30e9, 0), LeadFirm(143, "Karen Aerospace", 1.2e9, 3, "rival"), LeadFirm(5, "Mid Co", 520e6, 3)), false, out c);
        Check(p.Count == 1 && p[0].Key == LedgerStories.MileRival && p[0].Value("mark") == "$1000000000",
            "rivals alike: a rival tycoon's holding passing a billion is told as its own, not as a city company's first billion");
        p = Mark(s, LeadField(LeadFirm(1, "Big Co", 30e9, 0), LeadFirm(143, "Karen Aerospace", 2.5e9, 3, "rival"), LeadFirm(5, "Mid Co", 1.1e9, 3)), false, out c);
        Check(p.Count == 2 && p.Any(x => x.Key == LedgerStories.MileRival && x.Value("mark") == "$2000000000") && p.Any(x => x.Key == LedgerStories.MileCity && x.Value("name") == "Mid Co"),
            "rivals alike: on the same morning a rival's holding passes two billion and a city company its first billion, each in its own story");
        p = Mark(s, LeadField(LeadFirm(1, "Big Co", 30e9, 0), LeadFirm(143, "Karen Aerospace", 1.9e9, 3, "rival"), LeadFirm(5, "Mid Co", 2.6e9, 3)), false, out c);
        bool fell = p.Count == 0;
        p = Mark(s, LeadField(LeadFirm(1, "Big Co", 30e9, 0), LeadFirm(143, "Karen Aerospace", 2.4e9, 3, "rival"), LeadFirm(5, "Mid Co", 2.6e9, 3)), false, out c);
        Check(fell && p.Count == 0, "rivals alike: a rival's holding that falls under a figure and climbs back over it prints nothing, as a player's company does");
        var quiet = new LedgerState();
        Mark(quiet, LeadField(LeadFirm(143, "Karen Aerospace", 200e6, 3, "rival"), LeadFirm(1, "Big Co", 30e9, 0)), false, out c);
        p = Mark(quiet, LeadField(LeadFirm(143, "Karen Aerospace", 4.2e9, 3, "rival"), LeadFirm(1, "Big Co", 30e9, 0)), true, out c);
        Check(p.Count == 0 && quiet.Marks.First(m => m.Id == 143).Rung == 7, "rivals alike: quiet (a guest), the record follows a rival's holding and nothing is printed");

        // The top place.
        var t = new LedgerState();
        Mark(t, LeadField(LeadFirm(1, "Harbour City Bancorp", 30e9, 0), LeadFirm(143, "Karen Aerospace", 29e9, 3, "rival"), LeadFirm(900, "Mine", 5e9, 3, "player")), false, out c);
        p = Mark(t, LeadField(LeadFirm(1, "Harbour City Bancorp", 30e9, 0), LeadFirm(143, "Karen Aerospace", 31e9, 3, "rival"), LeadFirm(900, "Mine", 5e9, 3, "player")), false, out c);
        Check(p.Count == 1 && p[0].Key == LedgerStories.TopRival && p[0].Value("name") == "Karen Aerospace" && p[0].Value("other") == "Harbour City Bancorp" && t.TopId == 143 && c.Top == 1,
            "rivals alike: a rival tycoon's holding taking the city's top place has a story of its own, naming the company it passed");
        var u = new LedgerState();
        Mark(u, LeadField(LeadFirm(900, "Mine", 40e9, 3, "player"), LeadFirm(143, "Karen Aerospace", 30e9, 3, "rival"), LeadFirm(1, "Harbour City Bancorp", 20e9, 0)), false, out c);
        p = Mark(u, LeadField(LeadFirm(900, "Mine", 40e9, 3, "player"), LeadFirm(143, "Karen Aerospace", 42e9, 3, "rival"), LeadFirm(1, "Harbour City Bancorp", 20e9, 0)), false, out c);
        Check(p.Count == 1 && p[0].Key == LedgerStories.TopLost && p[0].Value("name") == "Mine" && p[0].Value("other") == "Karen Aerospace",
            "rivals alike: a player's company passed at the top by a rival's holding is still the player's loss, naming the rival");
        var v = new LedgerState();
        Mark(v, LeadField(LeadFirm(143, "Karen Aerospace", 40e9, 3, "rival"), LeadFirm(1, "Harbour City Bancorp", 38e9, 0)), false, out c);
        p = Mark(v, LeadField(LeadFirm(143, "Karen Aerospace", 40e9, 3, "rival"), LeadFirm(1, "Harbour City Bancorp", 42e9, 0)), false, out c);
        Check(p.Count == 1 && p[0].Key == LedgerStories.TopCity && p[0].Value("name") == "Harbour City Bancorp" && p[0].Value("other") == "Karen Aerospace",
            "rivals alike: a city company passing a rival's holding at the top has the city's story");
    }

    // The story of each pair, the margin and its edge.
    private static void LeadTold()
    {
        // A city company passes a city company: 4.9% ahead does not take the lead, exactly 5% does.
        LedgerState s = LeadSeeded(LeadFirm(1, "Harbour Bank", 30e9, 0), LeadFirm(2, "Dockside", 28e9, 0), LeadFirm(3, "Saltmarsh", 12e9, 1));
        Check(LeadHolder(s, 0) == 1 && LeadHolder(s, 1) == 3 && s.LeadsKept && s.Leads.Count == 2,
            "sector leaders: a save without the record sets each sector's leader where it stands, the most valuable company of the sector");
        List<LedgerPiece> p = LeadMorning(s, LeadField(LeadFirm(1, "Harbour Bank", 30e9, 0), LeadFirm(2, "Dockside", 31.49e9, 0), LeadFirm(3, "Saltmarsh", 12e9, 1)), false, out LedgerStoryCount c);
        Check(LeadStoriesOf(p).Count == 0 && LeadHolder(s, 0) == 1 && c.Leads == 0, "sector leaders: a company 4.9% ahead does not take the lead (the margin is " + LedgerLeads.LeadBp + " bp)");
        p = LeadMorning(s, LeadField(LeadFirm(1, "Harbour Bank", 30e9, 0), LeadFirm(2, "Dockside", 31.5e9, 0), LeadFirm(3, "Saltmarsh", 12e9, 1)), false, out c);
        List<LedgerPiece> told = LeadStoriesOf(p);
        Check(told.Count == 1 && told[0].Key == LedgerLeads.LeadCity && told[0].Value("name") == "Dockside" && told[0].Value("other") == "Harbour Bank" && told[0].Value("sector") == "Sector 0"
              && told[0].Value("value") == "$31500000000" && told[0].Value("other_value") == "$30000000000" && told[0].Section == LedgerSection.Markets && LeadHolder(s, 0) == 2 && c.Leads == 1,
            "sector leaders: a city company exactly 5% ahead takes the lead, with a story naming both, their values and the sector");
        p = LeadMorning(s, LeadField(LeadFirm(1, "Harbour Bank", 30e9, 0), LeadFirm(2, "Dockside", 31.5e9, 0), LeadFirm(3, "Saltmarsh", 12e9, 1)), false, out c);
        Check(LeadStoriesOf(p).Count == 0, "sector leaders: the same standing a second morning prints nothing, each change is told once");
        p = LeadMorning(s, LeadField(LeadFirm(1, "Harbour Bank", 33.1e9, 0), LeadFirm(2, "Dockside", 31.5e9, 0), LeadFirm(3, "Saltmarsh", 12e9, 1)), false, out c);
        told = LeadStoriesOf(p);
        Check(told.Count == 1 && told[0].Value("name") == "Harbour Bank" && told[0].Value("other") == "Dockside", "sector leaders: the old leader coming back 5% ahead is told again, the names the other way round");

        // A player's company beats a rival tycoon's holding: the paper's industry leader defeating a major rival.
        s = LeadSeeded(LeadFirm(143, "Karen Aerospace", 3.4e9, 3, "rival"), LeadFirm(900, "Mine", 3.2e9, 3, "player"), LeadFirm(7, "Corner Row", 1e9, 3));
        p = LeadMorning(s, LeadField(LeadFirm(143, "Karen Aerospace", 3.4e9, 3, "rival"), LeadFirm(900, "Mine", 3.6e9, 3, "player"), LeadFirm(7, "Corner Row", 1e9, 3)), false, out c);
        told = LeadStoriesOf(p);
        Check(told.Count == 1 && told[0].Key == LedgerLeads.LeadOwn && told[0].Value("name") == "Mine" && told[0].Value("other") == "Karen Aerospace" && told[0].Value("sector") == "Sector 3"
              && told[0].Value("value") == "$3600000000" && told[0].Value("other_value") == "$3400000000" && LeadHolder(s, 3) == 900,
            "sector leaders: a player's company overtaking a rival tycoon's holding has the player's own story, naming the rival's holding");

        // A rival tycoon's holding passes a city company.
        s = LeadSeeded(LeadFirm(10, "Ashfold Builders", 3.4e9, 4), LeadFirm(144, "Luthor Everything", 3.0e9, 4, "rival"));
        p = LeadMorning(s, LeadField(LeadFirm(10, "Ashfold Builders", 3.4e9, 4), LeadFirm(144, "Luthor Everything", 3.7e9, 4, "rival")), false, out c);
        told = LeadStoriesOf(p);
        Check(told.Count == 1 && told[0].Key == LedgerLeads.LeadRival && told[0].Value("name") == "Luthor Everything" && told[0].Value("other") == "Ashfold Builders" && LeadHolder(s, 4) == 144,
            "sector leaders: a rival tycoon's holding taking the lead from a city company has a story of its own");

        // A player's company passed by a city company, and by a rival's holding: the player's loss either way, the player's company first.
        s = LeadSeeded(LeadFirm(900, "Mine", 3.4e9, 5, "player"), LeadFirm(20, "Gridstone Electric", 3.0e9, 5));
        p = LeadMorning(s, LeadField(LeadFirm(900, "Mine", 3.4e9, 5, "player"), LeadFirm(20, "Gridstone Electric", 3.7e9, 5)), false, out c);
        told = LeadStoriesOf(p);
        Check(told.Count == 1 && told[0].Key == LedgerLeads.LeadLost && told[0].Value("name") == "Mine" && told[0].Value("value") == "$3400000000" && told[0].Value("other") == "Gridstone Electric"
              && told[0].Value("other_value") == "$3700000000" && LeadHolder(s, 5) == 20,
            "sector leaders: a player's company passed by a city company has the player's loss, its own name and value first");
        s = LeadSeeded(LeadFirm(900, "Mine", 3.4e9, 5, "player"), LeadFirm(145, "Zucc Holdings", 3.0e9, 5, "rival"));
        p = LeadMorning(s, LeadField(LeadFirm(900, "Mine", 3.4e9, 5, "player"), LeadFirm(145, "Zucc Holdings", 3.7e9, 5, "rival")), false, out c);
        told = LeadStoriesOf(p);
        Check(told.Count == 1 && told[0].Key == LedgerLeads.LeadLost && told[0].Value("name") == "Mine" && told[0].Value("other") == "Zucc Holdings",
            "sector leaders: a player's company passed by a rival tycoon's holding is the player's loss, naming the rival");

        // A rival's holding passed by a city company: the city's story, nothing different about the rival.
        s = LeadSeeded(LeadFirm(144, "Luthor Everything", 3.4e9, 4, "rival"), LeadFirm(10, "Ashfold Builders", 3.0e9, 4));
        p = LeadMorning(s, LeadField(LeadFirm(144, "Luthor Everything", 3.4e9, 4, "rival"), LeadFirm(10, "Ashfold Builders", 3.7e9, 4)), false, out c);
        told = LeadStoriesOf(p);
        Check(told.Count == 1 && told[0].Key == LedgerLeads.LeadCity && told[0].Value("name") == "Ashfold Builders" && told[0].Value("other") == "Luthor Everything",
            "sector leaders: a rival's holding passed by a city company is told as the city's story, naming both");
    }

    // What moves the lead without a story, and what limits the stories.
    private static void LeadEdges()
    {
        // Only a major company's loss is news: the company passed is worth at least 100 million.
        LedgerState s = LeadSeeded(LeadFirm(1, "Small A", 90e6, 0), LeadFirm(2, "Small B", 85e6, 0));
        List<LedgerPiece> p = LeadMorning(s, LeadField(LeadFirm(1, "Small A", 90e6, 0), LeadFirm(2, "Small B", 95e6, 0)), false, out LedgerStoryCount c);
        Check(LeadStoriesOf(p).Count == 0 && LeadHolder(s, 0) == 2 && c.Leads == 0, "sector leaders: a company worth less than a hundred million passed is no major rival, so the lead moves without a story");
        s = LeadSeeded(LeadFirm(1, "Just Major", 100e6, 0), LeadFirm(2, "Challenger", 90e6, 0));
        p = LeadMorning(s, LeadField(LeadFirm(1, "Just Major", 100e6, 0), LeadFirm(2, "Challenger", 106e6, 0)), false, out c);
        Check(LeadStoriesOf(p).Count == 1 && LeadHolder(s, 0) == 2, "sector leaders: a company worth exactly a hundred million, passed, is a major rival");

        // The leader left the market: the largest left takes the lead, no company beaten.
        s = LeadSeeded(LeadFirm(1, "Gone Bank", 10e9, 0), LeadFirm(2, "Stays Bank", 9e9, 0));
        p = LeadMorning(s, LeadField(LeadFirm(2, "Stays Bank", 9e9, 0)), false, out c);
        Check(LeadStoriesOf(p).Count == 0 && LeadHolder(s, 0) == 2, "sector leaders: when the leader leaves the market the next takes the lead and nothing is printed");
        // The leader moved to another sector (a listed company whose business changed): nobody passed it.
        s = LeadSeeded(LeadFirm(900, "Mine", 10e9, 0, "player"), LeadFirm(2, "Stays Bank", 9e9, 0));
        p = LeadMorning(s, LeadField(LeadFirm(900, "Mine", 10e9, 1, "player"), LeadFirm(2, "Stays Bank", 9e9, 0)), false, out c);
        Check(!LeadStoriesOf(p).Any(x => x.Value("sector") == "Sector 0") && LeadHolder(s, 0) == 2, "sector leaders: a leader that moved to another sector leaves its old one to the next company without a story");

        // A company new to the market takes the lead without a story: its listing is the news; once it has been seen it can be passed.
        s = LeadSeeded(LeadFirm(1, "Old Leader", 10e9, 0), LeadFirm(2, "Second", 8e9, 0));
        p = LeadMorning(s, LeadField(LeadFirm(1, "Old Leader", 10e9, 0), LeadFirm(2, "Second", 8e9, 0), LeadFirm(9, "New Listing", 20e9, 0)), false, out c);
        Check(LeadStoriesOf(p).Count == 0 && LeadHolder(s, 0) == 9, "sector leaders: a company new to the market that comes in as the largest takes the lead without a story");
        p = LeadMorning(s, LeadField(LeadFirm(1, "Old Leader", 22e9, 0), LeadFirm(2, "Second", 8e9, 0), LeadFirm(9, "New Listing", 20e9, 0)), false, out c);
        List<LedgerPiece> told = LeadStoriesOf(p);
        Check(told.Count == 1 && told[0].Value("name") == "Old Leader" && told[0].Value("other") == "New Listing", "sector leaders: the next morning the new company is known and can be passed");

        // Quiet (a guest reading the host's paper): the record follows, nothing is printed.
        s = LeadSeeded(LeadFirm(1, "Harbour Bank", 30e9, 0), LeadFirm(2, "Dockside", 28e9, 0));
        p = LeadMorning(s, LeadField(LeadFirm(1, "Harbour Bank", 30e9, 0), LeadFirm(2, "Dockside", 34e9, 0)), true, out c);
        Check(LeadStoriesOf(p).Count == 0 && c.Leads == 0 && LeadHolder(s, 0) == 2, "sector leaders: quiet, the record moves on and nothing is printed");
        // Without a name for the sector no story is told, but the record still follows.
        s = LeadSeeded(LeadFirm(1, "Harbour Bank", 30e9, 0), LeadFirm(2, "Dockside", 28e9, 0));
        var into = new List<LedgerPiece>();
        c = default(LedgerStoryCount);
        LedgerLeads.Pass(s, LeadField(LeadFirm(1, "Harbour Bank", 30e9, 0), LeadFirm(2, "Dockside", 34e9, 0)), false, StoryMoney, null, into, ref c);
        Check(into.Count == 0 && LeadHolder(s, 0) == 2, "sector leaders: with no name for a sector (the wiring unset) nothing is printed, the record follows");
        into.Clear();
        s = LeadSeeded(LeadFirm(1, "Harbour Bank", 30e9, 0), LeadFirm(2, "Dockside", 28e9, 0));
        LedgerLeads.Pass(s, LeadField(LeadFirm(1, "Harbour Bank", 30e9, 0), LeadFirm(2, "Dockside", 34e9, 0)), false, StoryMoney, n => string.Empty, into, ref c);
        Check(into.Count == 0 && LeadHolder(s, 0) == 2, "sector leaders: a sector with an empty name tells nothing and the record follows");

        // The cap: three sectors of the city's companies change on one morning, and a player's and a rival's too.
        var before = new List<LedgerCompany>();
        var after = new List<LedgerCompany>();
        for (int sec = 0; sec < 5; sec++)
        {
            before.Add(LeadFirm(10 + sec, "Leader " + sec, 10e9, sec));
            before.Add(LeadFirm(20 + sec, "Challenger " + sec, 9.5e9, sec, sec == 3 ? "player" : sec == 4 ? "rival" : ""));
            after.Add(LeadFirm(10 + sec, "Leader " + sec, 10e9, sec));
            after.Add(LeadFirm(20 + sec, "Challenger " + sec, 11e9, sec, sec == 3 ? "player" : sec == 4 ? "rival" : ""));
        }
        s = new LedgerState();
        LeadMorning(s, before, false, out c);
        p = LeadMorning(s, after, false, out c);
        told = LeadStoriesOf(p);
        bool moved = true;
        for (int sec = 0; sec < 5; sec++)
        {
            moved &= LeadHolder(s, sec) == 20 + sec;
        }
        Check(told.Count == 4 && c.Leads == 4 && told.Count(x => x.Key == LedgerLeads.LeadCity) == LedgerLeads.CityMost && told.Any(x => x.Key == LedgerLeads.LeadOwn)
              && told.Any(x => x.Key == LedgerLeads.LeadRival) && told.Where(x => x.Key == LedgerLeads.LeadCity).All(x => x.Value("sector") == "Sector 0" || x.Value("sector") == "Sector 1") && moved,
            "sector leaders: of three sectors of the city's companies changing on one morning " + LedgerLeads.CityMost + " are told, the player's and the rival's still are, and every lead moves (" + told.Count + " told)");

        // A tie goes to the lower id whatever the order of the list; a sector outside the market's is left out; no companies at all is no harm.
        s = LeadSeeded(LeadFirm(9, "Nine", 5e9, 0), LeadFirm(3, "Three", 5e9, 0));
        LedgerState reversed = LeadSeeded(LeadFirm(3, "Three", 5e9, 0), LeadFirm(9, "Nine", 5e9, 0));
        Check(LeadHolder(s, 0) == 3 && LeadHolder(reversed, 0) == 3, "sector leaders: a tie goes to the lower id, the same on every machine whatever the list's order");
        s = LeadSeeded(LeadFirm(1, "Below", 1e9, -1), LeadFirm(2, "Above", 1e9, LedgerLeads.SectorsMost + 1), LeadFirm(3, "Fine", 1e9, 2));
        Check(s.Leads.Count == 1 && LeadHolder(s, 2) == 3 && s.LeadsKept, "sector leaders: a company in a sector the record has no room for is left out, the others are kept");
        s = LeadSeeded(LeadFirm(1, "Harbour Bank", 30e9, 0));
        LeadMorning(s, new List<LedgerCompany>(), false, out c);
        Check(s.Leads.Count == 0 && s.LeadsKept, "sector leaders: a market with no company leaves no leader and breaks nothing");

        // Switched off: the record goes, and switched on again each sector is taken as it stands.
        s = LeadSeeded(LeadFirm(1, "Harbour Bank", 30e9, 0), LeadFirm(2, "Dockside", 28e9, 0));
        LedgerStories.Forget(s);
        Check(!s.LeadsKept && s.Leads.Count == 0 && !s.MarksKept, "sector leaders: switching the stories off forgets the leaders with the rest of the record");
        p = LeadMorning(s, LeadField(LeadFirm(1, "Harbour Bank", 30e9, 0), LeadFirm(2, "Dockside", 40e9, 0)), false, out c);
        Check(LeadStoriesOf(p).Count == 0 && LeadHolder(s, 0) == 2 && s.LeadsKept, "sector leaders: after the option was off the first morning sets every sector again without a story");
        // A save from before this record (milestones kept, no leaders) does the same: no burst of stories for the standing it finds.
        s = new LedgerState { MarksKept = true };
        s.Marks.Add(new LedgerMark { Id = 1, Rung = 4 });
        s.Marks.Add(new LedgerMark { Id = 2, Rung = 4 });
        p = LeadMorning(s, LeadField(LeadFirm(1, "Harbour Bank", 30e9, 0), LeadFirm(2, "Dockside", 40e9, 0)), false, out c);
        Check(LeadStoriesOf(p).Count == 0 && LeadHolder(s, 0) == 2 && s.LeadsKept, "sector leaders: a save with the milestones but not the leaders sets the leaders where they stand, with no story");
    }

    // 400 mornings of six sectors drifting with a fixed seed, a player's company and a rival's among them: after every morning the lead is held by a company no more than
    // 5% behind the sector's largest, and every story names the largest as the winner, the one that held the lead as the loser, a winner 5% ahead of it.
    private static string LeadWalkRun(out int told, out string broken)
    {
        var rng = new Random(20261112);
        var list = new List<LedgerCompany>();
        for (int sec = 0; sec < 6; sec++)
        {
            for (int k = 0; k < 5; k++)
            {
                int id = sec * 10 + k + 1;
                list.Add(LeadFirm(id, "C" + id, 2e8 * Math.Pow(1.7, k) * (1.0 + rng.NextDouble() * 0.4), sec, sec == 2 && k == 1 ? "player" : sec == 3 && k == 2 ? "rival" : ""));
            }
        }
        var s = new LedgerState();
        var log = new System.Text.StringBuilder();
        told = 0;
        broken = null;
        for (int day = 0; day < 400; day++)
        {
            for (int i = 0; i < list.Count; i++)
            {
                LedgerCompany company = list[i];
                company.Value *= Math.Exp((rng.NextDouble() - 0.5) * 0.12);
                list[i] = company;
            }
            var heldBefore = new int[6];
            for (int sec = 0; sec < 6; sec++)
            {
                heldBefore[sec] = LeadHolder(s, sec);
            }
            List<LedgerPiece> p = LeadMorning(s, list, false, out LedgerStoryCount count);
            List<LedgerPiece> stories = LeadStoriesOf(p);
            if (stories.Count(x => x.Key == LedgerLeads.LeadCity) > LedgerLeads.CityMost && broken == null)
            {
                broken = "day " + day + ": " + stories.Count + " stories";
            }
            foreach (LedgerPiece piece in stories)
            {
                told++;
                log.Append(day).Append(':').Append(piece.Key).Append(':').Append(string.Join("|", piece.Data)).Append('\n');
                bool lost = piece.Key == LedgerLeads.LeadLost;
                LedgerCompany winner = list.First(x => x.Name == piece.Value(lost ? "other" : "name"));
                LedgerCompany loser = list.First(x => x.Name == piece.Value(lost ? "name" : "other"));
                LedgerCompany best = list.Where(x => x.Sector == winner.Sector).OrderByDescending(x => x.Value).ThenBy(x => x.Id).First();
                if (broken == null && (best.Id != winner.Id || loser.Id != heldBefore[winner.Sector] || winner.Value * 10000.0 < loser.Value * 10500.0 || loser.Value < LedgerLeads.Floor))
                {
                    broken = "day " + day + ": " + piece.Value("name") + " and " + piece.Value("other");
                }
            }
            for (int sec = 0; sec < 6; sec++)
            {
                LedgerCompany best = list.Where(x => x.Sector == sec).OrderByDescending(x => x.Value).ThenBy(x => x.Id).First();
                LedgerCompany holder = list.First(x => x.Id == LeadHolder(s, sec));
                if (broken == null && best.Id != holder.Id && best.Value * 10000.0 >= holder.Value * 10500.0)
                {
                    broken = "day " + day + ": sector " + sec + " is held by " + holder.Name + " though " + best.Name + " is 5% ahead";
                }
            }
        }
        return log + LedgerStore.Write(s);
    }

    private static void LeadWalk()
    {
        string first = LeadWalkRun(out int told, out string broken);
        string second = LeadWalkRun(out int toldAgain, out string brokenAgain);
        Check(broken == null && brokenAgain == null, "sector leaders: over 400 mornings of six drifting sectors every story is the largest passing the one that held the lead by 5%, and no holder is left 5% behind" + (broken != null ? " (" + broken + ")" : string.Empty));
        Check(told >= 10, "sector leaders: the walk is lively enough to prove the rules (" + told + " stories told over 400 mornings)");
        Check(first == second && told == toldAgain, "sector leaders: the same drift gives the same stories and the same record on every run (every machine)");
    }

    private static void LeadStore()
    {
        var s = new LedgerState { Subscribed = true, PaidUntil = 30 };
        string old = LedgerStore.Write(s);
        Check(!old.Contains("\nL|") && !LedgerStore.Read(old).LeadsKept, "sector leaders' save: no record, no line (the save is as before), and it reads as a save without the record");
        s.LeadsKept = true;
        s.Leads.Add(new LedgerLead { Sector = 3, Id = 144 });
        s.Leads.Add(new LedgerLead { Sector = 0, Id = 1 });
        s.Leads.Add(new LedgerLead { Sector = 23, Id = 137 });
        LedgerState r = LedgerStore.Read(LedgerStore.Write(s));
        Check(r.LeadsKept && r.Leads.Count == 3 && r.Leads[0].Sector == 3 && r.Leads[0].Id == 144 && r.Leads[1].Sector == 0 && r.Leads[1].Id == 1 && r.Leads[2].Sector == 23 && r.Leads[2].Id == 137
              && r.Subscribed && r.PaidUntil == 30,
            "sector leaders' save: the leaders come back whole, in order, beside the subscription");
        var empty = new LedgerState { LeadsKept = true };
        LedgerState e = LedgerStore.Read(LedgerStore.Write(empty));
        Check(e.LeadsKept && e.Leads.Count == 0, "sector leaders' save: a record kept with no sector yet stays kept");
        LedgerState bad = LedgerStore.Read(LedgerStore.Write(new LedgerState()) + "\nL|x:y,5:7,99:3,1:0,5:8,2:9,4");
        Check(bad.LeadsKept && bad.Leads.Count == 2 && bad.Leads[0].Sector == 5 && bad.Leads[0].Id == 7 && bad.Leads[1].Sector == 2 && bad.Leads[1].Id == 9,
            "sector leaders' save: a damaged entry, a sector past the record's room, a company of none and a second leader of one sector are left out");
        var both = new LedgerState { MarksKept = true, TopId = 4, LeadsKept = true };
        both.Marks.Add(new LedgerMark { Id = 4, Rung = 5, Topped = true });
        both.Leads.Add(new LedgerLead { Sector = 1, Id = 4 });
        both.Edition = new LedgerEdition { Day = 22 };
        both.Edition.Pieces.Add(LedgerRules.Piece(LedgerLeads.LeadCity, new[] { "name", "A", "other", "B", "sector", "Banks" }));
        LedgerState w = LedgerStore.Read(LedgerStore.Write(both));
        Check(w.MarksKept && w.TopId == 4 && w.Marks.Count == 1 && w.Leads.Count == 1 && w.Edition != null && w.Edition.Day == 22 && w.Edition.Pieces.Count == 1
              && w.Edition.Pieces[0].Value("sector") == "Banks",
            "sector leaders' save: the milestones' record and the edition read as before beside the leaders");
        var shared = new System.Text.StringBuilder();
        LedgerStore.Edition(shared, both.Edition);
        Check(!shared.ToString().Contains("\nL|") && !LedgerShare.Write(both.Edition).Contains("\nL|"), "sector leaders' save: the record stays on each machine, the edition sent to guests carries no line of it");
        Check(!LedgerShare.Personal(LedgerLeads.LeadOwn) && !LedgerShare.Personal(LedgerLeads.LeadLost) && !LedgerShare.Personal(LedgerLeads.LeadRival) && !LedgerShare.Personal(LedgerLeads.LeadCity),
            "sector leaders: a guest sends no sector story, the host's paper has them from the shared market");
        // The test's story: a player's company in its sector, else the most valuable company that has another in its sector, else none.
        string name;
        LedgerPiece test = LedgerLeads.Test(LeadField(LeadFirm(1, "Big", 30e9, 0), LeadFirm(2, "Next", 20e9, 0), LeadFirm(900, "Mine", 1e9, 4, "player"), LeadFirm(7, "Rival Co", 3e9, 4)), StoryMoney, LeadSector, out name);
        Check(test != null && test.Key == LedgerLeads.LeadOwn && name == "Mine" && test.Value("other") == "Rival Co" && test.Value("sector") == "Sector 4",
            "sector leaders' test: a player's company is shown against the most valuable other company of its sector, whatever the standing");
        test = LedgerLeads.Test(LeadField(LeadFirm(143, "Karen", 5e9, 6, "rival"), LeadFirm(9, "Peer", 4e9, 6), LeadFirm(8, "Lonely", 90e9, 5), LeadFirm(2, "Next", 20e9, 0), LeadFirm(1, "Big", 30e9, 0)), StoryMoney, LeadSector, out name);
        Check(test != null && test.Key == LedgerLeads.LeadCity && name == "Big" && test.Value("other") == "Next", "sector leaders' test: without a player's company the most valuable company that has another in its sector is shown, not a company alone in its sector");
        test = LedgerLeads.Test(LeadField(LeadFirm(8, "Lonely", 90e9, 5)), StoryMoney, LeadSector, out name);
        Check(test == null && name == null && LedgerLeads.Test(new List<LedgerCompany>(), StoryMoney, LeadSector, out name) == null && LedgerLeads.Test(LeadField(LeadFirm(1, "A", 1e9, 0), LeadFirm(2, "B", 1e9, 0)), StoryMoney, null, out name) == null,
            "sector leaders' test: no sector with two companies, no market or no name for a sector, no story");
    }

    // The real market over two 60-day years from two seeds: the stories are news, not noise, and each one is what the rules say.
    private static void LeadMarket()
    {
        int mornings = 0, withStories = 0, stories = 0, mostInAMorning = 0;
        bool rules = true, firstQuiet = true;
        string broken = null;
        foreach (ulong seed in new ulong[] { 41, 97 })
        {
            MarketEngine m = NewMarket(seed, 60);
            var s = new LedgerState();
            for (int day = 2; day <= 121; day++)
            {
                RunTo(m, day);
                var list = new List<LedgerCompany>();
                foreach (Stock k in m.State.Stocks)
                {
                    if (k.Listed && k.Daily.Count > 0 && k.Shares > 0)
                    {
                        list.Add(new LedgerCompany
                        {
                            Id = k.Id, Name = k.Name, Value = k.Shares * (double)k.Daily[k.Daily.Count - 1].C / 100.0, Player = false, Rival = k.Def != null && k.Def.Holding, Sector = (int)k.Sector
                        });
                    }
                }
                if (list.Count == 0)
                {
                    continue;
                }
                List<LedgerPiece> told = LeadStoriesOf(LeadMorning(s, list, false, out LedgerStoryCount count));
                mornings++;
                firstQuiet &= day != 2 || told.Count == 0;
                stories += told.Count;
                withStories += told.Count > 0 ? 1 : 0;
                mostInAMorning = Math.Max(mostInAMorning, told.Count);
                foreach (LedgerPiece piece in told)
                {
                    LedgerCompany winner = list.First(x => x.Name == piece.Value("name"));
                    LedgerCompany loser = list.First(x => x.Name == piece.Value("other"));
                    LedgerCompany best = list.Where(x => x.Sector == winner.Sector).OrderByDescending(x => x.Value).ThenBy(x => x.Id).First();
                    bool ok = (piece.Key == LedgerLeads.LeadCity || piece.Key == LedgerLeads.LeadRival) && best.Id == winner.Id && winner.Sector == loser.Sector
                              && winner.Value * 10000.0 >= loser.Value * 10500.0 && loser.Value >= LedgerLeads.Floor;
                    if (!ok && broken == null)
                    {
                        broken = "seed " + seed + " day " + day + ": " + piece.Key + " " + piece.Value("name") + " over " + piece.Value("other");
                    }
                    rules &= ok;
                }
            }
        }
        Console.WriteLine("    the market's sector leaders over " + mornings + " mornings: " + stories + " stories on " + withStories + " mornings, at most " + mostInAMorning + " in one");
        Check(firstQuiet, "sector leaders on the real market: the first morning sets every sector without a story");
        Check(rules, "sector leaders on the real market: every story is the sector's largest company passing the one that led it by 5%, the one passed a major company" + (broken != null ? " (" + broken + ")" : string.Empty));
        Check(mostInAMorning <= LedgerLeads.CityMost && withStories <= mornings / 4,
            "sector leaders on the real market: a change of lead is news, not noise, at most " + LedgerLeads.CityMost + " in a morning and one morning in four (" + stories + " on " + withStories + " of " + mornings + " mornings)");
        // Timing: once a morning over the whole market.
        var market = new List<LedgerCompany>();
        for (int i = 1; i <= 170; i++)
        {
            market.Add(LeadFirm(i, "C" + i, 2e8 + i * 1.3e8, i % (int)MarketSector.Count, i == 77 ? "player" : i % 40 == 0 ? "rival" : ""));
        }
        var seeded = new LedgerState();
        LeadMorning(seeded, market, false, out LedgerStoryCount first);
        var into = new List<LedgerPiece>();
        var timer = Stopwatch.StartNew();
        for (int round = 0; round < 100; round++)
        {
            for (int i = 0; i < market.Count; i++)
            {
                LedgerCompany k = market[i];
                k.Value *= i % 2 == 0 ? 1.01 : 0.995;
                market[i] = k;
            }
            into.Clear();
            LedgerStoryCount count = default(LedgerStoryCount);
            LedgerLeads.Pass(seeded, market, false, StoryMoney, LeadSector, into, ref count);
            LedgerStories.Marks(seeded, market, false, StoryMoney, into, ref count);
        }
        double ms = timer.Elapsed.TotalMilliseconds / 100.0;
        Check(ms < 2.0, "sector leaders: a morning's leaders and milestones over 170 companies take " + ms.ToString("0.000", CultureInfo.InvariantCulture) + " ms (wanted under 2 ms, once a morning)");
    }

    private static void LeadSources()
    {
        string stories = ModSource(@"Ledger\LedgerDirector.Stories.cs");
        string links = ModSource(@"Wiring\LedgerLinks.cs");
        string into = MemberText(stories, "private void StoriesInto(LedgerEdition e, int since, int day, bool fresh)");
        Check(into.Contains("LedgerLeads.Pass(_state, _companies, false, Large, LedgerBridges.SectorName, e.Pieces, ref _count);") && into.Contains("_state.LeadsKept")
              && into.IndexOf("LedgerLeads.Pass(", StringComparison.Ordinal) < into.IndexOf("LedgerStories.Marks(", StringComparison.Ordinal),
            "sector leaders' sources: an edition reads the sectors' leaders before the milestones, which bring the record of who was on the market up to date, and forgets both with the option off");
        string guest = MemberText(stories, "private void StoriesGuest(int day, List<LedgerPiece> into)");
        Check(guest.Contains("LedgerLeads.Pass(_state, _companies, true, Large, LedgerBridges.SectorName, into, ref _count);") && guest.Contains("_state.LeadsKept")
              && guest.IndexOf("LedgerLeads.Pass(", StringComparison.Ordinal) < guest.IndexOf("LedgerStories.Marks(", StringComparison.Ordinal),
            "sector leaders' sources: a guest following the host's paper keeps the leaders in step, quietly, before the milestones");
        string test = MemberText(stories, "private LedgerPiece TestStoriesReady(int day, out LedgerPiece mark, out bool own, out string name, out LedgerPiece lead, out string leadName)");
        string tests = MemberText(ModSource(@"Ledger\LedgerDirector.Tests.cs"), "public void TestStories()");
        Check(test.Contains("LedgerLeads.Test(_companies, Large, LedgerBridges.SectorName, out leadName)") && !test.Contains("_state") && !tests.Contains("_state.Leads"),
            "sector leaders' sources: the test shows a sector's lead in the paper and leaves the record alone");
        Check(links.Contains("LedgerBridges.SectorName = SectorName;") && links.Contains("Rival = IsRivalHolding(k)") && links.Contains("Sector = (int)k.Sector") && links.Contains("MarketText.Sector((MarketSector)sector)")
              && links.Contains("(k.Def != null && k.Def.Holding) || !string.IsNullOrEmpty(k.RivalId) || MarketCatalogue.IsRival(k.Id)"),
            "sector leaders' sources: the wiring hands the paper each company's sector and whether it is a rival tycoon's holding (the mod's tycoons' and the game's own rivals'), and the sectors' names");
        string market = string.Join("\n", System.IO.Directory.GetFiles(System.IO.Path.GetDirectoryName(ModSourcePath(@"Markets\MarketModel.cs")), "*.cs").Select(System.IO.File.ReadAllText));
        Check(!market.Contains("LedgerLeads") && !market.Contains("reallife_ledger_lead") && !market.Contains("reallife_ledger_mile_rival"),
            "sector leaders' sources: the market is untouched, the stories are read on each machine from what it has");
        string facts = ModSource(@"Meta\FeatureList.Life.cs");
        Check(facts.Contains(".Fact(\"ledger_lead_margin\", () => Guide.Percent(Ledger.LedgerLeads.LeadBp / 10000f))") && facts.Contains(".Fact(\"ledger_lead_most\", () => Whole(Ledger.LedgerLeads.CityMost))"),
            "sector leaders' sources: the Guide's margin and daily most are the code's own figures");
        Check(MemberText(stories, "private void StoriesCheck(LedgerEdition e, int found)").Contains("_count.Leads"), "sector leaders' sources: the self-check line counts the new sector leaders among the stories read");
        string off = ModSource(@"Ledger\LedgerStories.cs");
        Check(MemberText(off, "public static void Forget(LedgerState s)").Contains("LedgerLeads.Forget(s);"), "sector leaders' sources: forgetting the stories forgets the leaders");
    }

    private static void LeadTexts()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("sector leaders' texts", words, codes))
        {
            return;
        }
        var stories = new[] { LedgerLeads.LeadOwn, LedgerLeads.LeadLost, LedgerLeads.LeadRival, LedgerLeads.LeadCity, LedgerStories.MileRival, LedgerStories.TopRival };
        var heads = new[]
        {
            "reallife_ledger_head_lead_own", "reallife_ledger_head_lead_lost", "reallife_ledger_head_lead_rival", "reallife_ledger_head_lead_city", "reallife_ledger_head_mile_rival",
            "reallife_ledger_head_top_rival"
        };
        string lacking = string.Empty;
        foreach (string key in stories.Concat(heads))
        {
            lacking += LanguagesLacking(words, codes, key);
        }
        Check(lacking.Length == 0, "sector leaders' texts: every new story and headline is in every language, with the English names in curly brackets (lacking in: " + lacking + ")");
        var allowed = new Dictionary<string, string[]>
        {
            { "lead", new[] { "name", "value", "other", "other_value", "sector" } }, { "mile", new[] { "name", "mark", "value" } }, { "top", new[] { "name", "value", "other", "other_value" } }
        };
        for (int l = 0; l < words.Length; l++)
        {
            string bad = null;
            foreach (string key in stories.Concat(heads))
            {
                if (!words[l].TryGetValue(key, out string value) || value.Length == 0 || value.Contains('!') || value.Contains((char)0x2014) || value.Contains((char)0x2013) || value.Contains(" - "))
                {
                    bad = key;
                    break;
                }
                string kind = key.Contains("_lead_") ? "lead" : key.Contains("_mile_") ? "mile" : "top";
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(value, @"\{([a-z_]+)\}"))
                {
                    if (Array.IndexOf(allowed[kind], m.Groups[1].Value) < 0)
                    {
                        bad = key + " ({" + m.Groups[1].Value + "})";
                    }
                }
                if (Array.IndexOf(stories, key) >= 0 && value.Length > 400)
                {
                    bad = key + " (too long for a story)";
                }
            }
            Check(bad == null, "sector leaders' texts: every new text is plain in " + codes[l] + " (no dash, no exclamation mark, only the names its piece carries, a story under 400 characters)" + (bad != null ? " (not: " + bad + ")" : string.Empty));
        }
        // The English: the kicker the front page reads, the option's tip and the test's label on the options page's limits, the Guide's section and What's new.
        Dictionary<string, string> en = words[0];
        LedgerRules.Split(en[LedgerLeads.LeadOwn].Replace("{name}", "Mine").Replace("{other}", "Rival").Replace("{sector}", "Banks"), out string kicker, out string headline, out string body);
        Check(kicker == "MILESTONE" && headline == null && body.Length > 0 && en[LedgerLeads.LeadLost].Contains("{name}") && en[LedgerLeads.LeadLost].IndexOf("{name}", StringComparison.Ordinal) < en[LedgerLeads.LeadLost].IndexOf("{other}", StringComparison.Ordinal),
            "sector leaders' texts: a player's company taking the lead has the milestone kicker, and the loss opens with the player's company");
        string tip = en["reallife_opt_tip_ledger_stories"], label = en["reallife_ledger_test_stories"];
        Check(tip.EndsWith(".") && tip.Length <= 190 && tip.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length <= 32 && tip.Contains("sector"),
            "sector leaders' texts: the option's tip names the sector's lead and keeps to the options page's limits (190 characters, 32 words)");
        Check(label.Length <= 80 && label.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length <= 13 && !label.StartsWith("Test", StringComparison.OrdinalIgnoreCase) && label.Contains("sector lead"),
            "sector leaders' texts: the test button names the sector's lead and keeps to the options page's limits (80 characters, 13 words)");
        string guide = en["reallife_guide_ledger"];
        Check(guide.Contains("{ledger_lead_margin}") && guide.Contains("{ledger_lead_most}") && guide.Contains("{ledger_mile_first}") && guide.Contains("rival tycoon's holding"),
            "sector leaders' texts: the Guide's section tells the sector's lead with the code's margin and daily most, and the rivals' holdings");
        Check(WhatsNewHas(en, "The City Ledger now reports when a company takes the lead of its sector from a major rival") && en["reallife_changes_next"].Length > 0,
            "sector leaders' texts: What's new has the line (any of its rows)");
    }
}
