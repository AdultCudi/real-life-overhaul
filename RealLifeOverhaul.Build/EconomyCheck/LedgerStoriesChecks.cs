using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using RealLifeOverhaul.Ledger;
using RealLifeOverhaul.Markets;

// The City Ledger's stories of the market's big moments (a player's suggestion, 29 Oct 2026; Scripts/Ledger: LedgerStories,
// LedgerDirector.Stories, Wiring/LedgerLinks): what each kind weighs and where it goes, the ladder, a save without the record
// set silently, a player's milestone once and the highest figure only, a city company's first billion only, the most valuable
// company with its margin, lost and won back, a rival taken over in three versions drawn the same on every machine, the rivals'
// one line replaced, the record's line in the save, the guest's piece, the stories on the real market over two years, the
// sources (the option, the reprint, nothing new over the network, the self-check) and the texts.
internal static partial class Program
{
    private static readonly Func<double, string> StoryMoney = v => "$" + v.ToString("0", CultureInfo.InvariantCulture);

    private static void LedgerStoriesChecks()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        LedgerStoriesKinds();
        LedgerStoriesLadder();
        LedgerStoriesMarks();
        LedgerStoriesTop();
        LedgerStoriesBeaten();
        LedgerStoriesStore();
        LedgerStoriesMarket();
        LedgerStoriesSources();
        LedgerStoriesTexts();
        Console.WriteLine("  the City Ledger's milestone and rival stories: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    private static void LedgerStoriesKinds()
    {
        Kind("reallife_ledger_beaten_1", LedgerSection.Business, 96, "reallife_ledger_head_beaten_1");
        Kind("reallife_ledger_beaten_3", LedgerSection.Business, 96, "reallife_ledger_head_beaten_3");
        Kind("reallife_ledger_beaten_plain", LedgerSection.Business, 96, "reallife_ledger_head_beaten_plain");
        Kind("reallife_ledger_bought_own", LedgerSection.Business, 90, "reallife_ledger_head_bought_own");
        Kind("reallife_ledger_bought_city", LedgerSection.Business, 64, "reallife_ledger_head_bought_city");
        Kind("reallife_ledger_top_first", LedgerSection.Markets, 95, "reallife_ledger_head_top_first");
        Kind("reallife_ledger_mile_big", LedgerSection.Markets, 94, "reallife_ledger_head_mile_big");
        Kind("reallife_ledger_mile_own", LedgerSection.Markets, 85, "reallife_ledger_head_mile_own");
        Kind("reallife_ledger_top_lost", LedgerSection.Markets, 84, "reallife_ledger_head_top_lost");
        Kind("reallife_ledger_top_city", LedgerSection.Markets, 72, "reallife_ledger_head_top_city");
        Kind("reallife_ledger_mile_city", LedgerSection.Markets, 62, "reallife_ledger_head_mile_city");
        // Where each lands: the heaviest of the other news of a page decides; these weights against the catalogue's own.
        int crash = LedgerRules.KindOf("reallife_mkt_news_crash").Weight, bid = LedgerRules.KindOf("reallife_mkt_news_bid").Weight;
        int killed = LedgerRules.KindOf("reallife_news_ped_killed").Weight, severe = LedgerRules.KindOf("reallife_news_accident_severe").Weight;
        Check(LedgerRules.KindOf(LedgerStories.MileOwn).Weight > bid && LedgerRules.KindOf(LedgerStories.TopLost).Weight > LedgerRules.KindOf("reallife_mkt_news_taken_over").Weight,
            "milestones: a player's company's milestone and its lost first place outweigh every market story but a crash, a failure and the city's most valuable (they lead the markets page)");
        Check(LedgerRules.KindOf(LedgerStories.MileBig).Weight > crash && LedgerRules.KindOf(LedgerStories.TopFirst).Weight > crash
              && LedgerRules.KindOf(LedgerStories.BeatenKeys[0]).Weight > severe && LedgerRules.KindOf(LedgerStories.BeatenKeys[0]).Weight < killed,
            "milestones: a billion, the first place and a rival taken over outweigh a crash and a serious accident (the front page's lead), a death in the street still leads");
        Check(LedgerRules.KindOf(LedgerStories.TopCity).Weight < LedgerRules.KindOf(LedgerStories.TopLost).Weight && LedgerRules.KindOf(LedgerStories.MileCity).Weight < bid
              && LedgerRules.KindOf(LedgerStories.BoughtCity).Weight < LedgerRules.KindOf(LedgerStories.BoughtOwn).Weight,
            "milestones: the city's companies weigh less than a player's, so they are news on the markets page and seldom on the front page");
        var edition = new LedgerEdition { Day = 9 };
        edition.Pieces.Add(LedgerRules.Piece("reallife_news_accident_moderate", null));
        edition.Pieces.Add(LedgerRules.Piece(LedgerStories.MileCity, new[] { "name", "Dockside Savings" }));
        edition.Pieces.Add(LedgerRules.Piece(LedgerStories.BeatenKeys[1], new[] { "rival", "Bald Luthor" }));
        Check(LedgerRules.Lead(edition) != null && LedgerRules.Lead(edition).Key == LedgerStories.BeatenKeys[1], "milestones: a rival taken over leads the front page over the day's street news");
        Check(LedgerStories.Kept(LedgerStories.MileBig) && LedgerStories.Kept(LedgerStories.TopCity) && LedgerStories.Kept(LedgerStories.BeatenPlain) && !LedgerStories.Kept(LedgerStories.BoughtCity)
              && LedgerStories.Story(LedgerStories.BoughtOwn) && !LedgerStories.Story("reallife_mkt_news_taken_over") && !LedgerStories.Story(null),
            "milestones: the stories read from the state are kept when the edition is written again; a takeover by a listed company is read again from the market's news like its neighbours");
    }

    private static void LedgerStoriesLadder()
    {
        long[] l = LedgerStories.Ladder;
        bool rising = true;
        for (int i = 1; i < l.Length; i++)
        {
            rising &= l[i] > l[i - 1];
        }
        Check(rising && l[0] == 100000000L && l[LedgerStories.BigRung - 1] == 1000000000L && l[LedgerStories.CityRung - 1] == 1000000000L && l[l.Length - 1] == 100000000000L,
            "milestones: the ladder rises from a hundred million through each billion to ten, then 25, 50 and 100 billion; a billion is its fourth figure");
        Check(LedgerStories.Rung(0) == 0 && LedgerStories.Rung(99999999.0) == 0 && LedgerStories.Rung(100000000.0) == 1 && LedgerStories.Rung(999999999.0) == 3
              && LedgerStories.Rung(1000000000.0) == 4 && LedgerStories.Rung(10500000000.0) == 13 && LedgerStories.Rung(5e11) == l.Length && LedgerStories.Rung(double.NaN) == 0,
            "milestones: a value's place on the ladder counts the figures reached, none for nothing, all for more than the top");
        Check((float)l[0] == l[0] && (float)l[LedgerStories.BigRung - 1] == l[LedgerStories.BigRung - 1],
            "milestones: the Guide's figures (a hundred million, a billion) are exact as the Guide's money writes them");
    }

    private static List<LedgerCompany> Companies(params object[] rows)
    {
        var list = new List<LedgerCompany>();
        for (int i = 0; i + 3 < rows.Length; i += 4)
        {
            list.Add(new LedgerCompany { Id = (int)rows[i], Name = (string)rows[i + 1], Value = Convert.ToDouble(rows[i + 2], CultureInfo.InvariantCulture), Player = (bool)rows[i + 3] });
        }
        return list;
    }

    private static List<LedgerPiece> Mark(LedgerState s, List<LedgerCompany> list, bool quiet, out LedgerStoryCount count)
    {
        var into = new List<LedgerPiece>();
        count = default(LedgerStoryCount);
        LedgerStories.Marks(s, list, quiet, StoryMoney, into, ref count);
        return into;
    }

    private static void LedgerStoriesMarks()
    {
        // A save without the record: every company where it stands, no story (an old save never prints milestones passed long ago).
        var s = new LedgerState();
        List<LedgerPiece> p = Mark(s, Companies(1, "Big Co", 30e9, false, 2, "Small Co", 900e6, false, 900, "Mine", 1.6e9, true), false, out LedgerStoryCount c);
        Check(p.Count == 0 && c.Seeded == 3 && s.MarksKept && s.TopId == 1 && s.Marks.Count == 3 && s.Marks.First(m => m.Id == 900).Rung == 4,
            "milestones: a save without the record sets every company where it stands (the player's at 1.6 billion past four figures), the largest first, and prints nothing");
        // Climbing: the highest figure passed is printed once; a fall and a climb back print nothing.
        p = Mark(s, Companies(1, "Big Co", 30e9, false, 2, "Small Co", 950e6, false, 900, "Mine", 4.2e9, true), false, out c);
        Check(p.Count == 1 && p[0].Key == LedgerStories.MileBig && p[0].Value("mark") == "$4000000000" && p[0].Value("value") == "$4200000000" && p[0].Value("name") == "Mine" && c.Milestones == 1,
            "milestones: a player's company that jumps three figures has one story, for the highest (four billion), on the front page's weight");
        p = Mark(s, Companies(1, "Big Co", 30e9, false, 2, "Small Co", 950e6, false, 900, "Mine", 3.1e9, true), false, out c);
        bool quietFall = p.Count == 0;
        p = Mark(s, Companies(1, "Big Co", 30e9, false, 2, "Small Co", 950e6, false, 900, "Mine", 4.4e9, true), false, out c);
        Check(quietFall && p.Count == 0 && s.Marks.First(m => m.Id == 900).Rung == 7, "milestones: falling below a figure and climbing back over it prints nothing: each figure is news once");
        // Under a billion: the markets page's weight.
        var s2 = new LedgerState();
        Mark(s2, Companies(1, "Big Co", 30e9, false, 7, "Fresh", 80e6, true), false, out c);
        p = Mark(s2, Companies(1, "Big Co", 30e9, false, 7, "Fresh", 260e6, true), false, out c);
        Check(p.Count == 1 && p[0].Key == LedgerStories.MileOwn && p[0].Value("mark") == "$250000000" && p[0].Weight == 85,
            "milestones: a player's company passing 250 million has the markets page's story");
        // A city company: its first billion only, and never again.
        p = Mark(s, Companies(1, "Big Co", 30e9, false, 2, "Small Co", 1.05e9, false, 900, "Mine", 4.4e9, true), false, out c);
        Check(p.Count == 1 && p[0].Key == LedgerStories.MileCity && p[0].Value("name") == "Small Co" && p[0].Value("mark") == "$1000000000", "milestones: a city company passing its first billion has a story");
        p = Mark(s, Companies(1, "Big Co", 30e9, false, 2, "Small Co", 2.5e9, false, 900, "Mine", 4.4e9, true), false, out c);
        bool cityOnce = p.Count == 0;
        p = Mark(s, Companies(1, "Big Co", 30e9, false, 2, "Small Co", 0.8e9, false, 900, "Mine", 4.4e9, true), false, out c);
        bool cityDown = p.Count == 0;
        p = Mark(s, Companies(1, "Big Co", 30e9, false, 2, "Small Co", 1.2e9, false, 900, "Mine", 4.4e9, true), false, out c);
        Check(cityOnce && cityDown && p.Count == 0, "milestones: a city company's further billions, and its billion passed again after a fall, are no news");
        // A company new to the market is set where it stands; one gone leaves the record, and is new again if it comes back.
        p = Mark(s, Companies(1, "Big Co", 30e9, false, 2, "Small Co", 1.2e9, false, 900, "Mine", 4.4e9, true, 55, "New Listing", 3e9, false), false, out c);
        Check(p.Count == 0 && s.Marks.Any(m => m.Id == 55 && m.Rung == 6), "milestones: a new listing worth three billion is set where it stands, without a story (its listing is the news)");
        p = Mark(s, Companies(1, "Big Co", 30e9, false, 900, "Mine", 4.4e9, true, 55, "New Listing", 3e9, false), false, out c);
        Check(p.Count == 0 && !s.Marks.Any(m => m.Id == 2), "milestones: a company gone from the market leaves the record");
        // Quiet (a guest reading the host's paper): the record follows, nothing is printed.
        p = Mark(s, Companies(1, "Big Co", 30e9, false, 900, "Mine", 9.4e9, true, 55, "New Listing", 3e9, false), true, out c);
        Check(p.Count == 0 && c.Milestones == 0 && s.Marks.First(m => m.Id == 900).Rung == 12, "milestones: quiet, the record moves on to nine billion and nothing is printed");
        // Switched off: the record goes, and switched on again the market is taken as it stands.
        LedgerStories.Forget(s);
        p = Mark(s, Companies(1, "Big Co", 30e9, false, 900, "Mine", 12e9, true), false, out c);
        Check(p.Count == 0 && c.Seeded == 2 && s.MarksKept, "milestones: after the option was off, the first morning sets every company again without a story");
        // Bounded: more companies than the record keeps never grow it past its size.
        var many = new List<LedgerCompany>();
        for (int i = 1; i <= LedgerStories.MarksMost + 50; i++)
        {
            many.Add(new LedgerCompany { Id = i, Name = "C" + i, Value = i * 1e7, Player = false });
        }
        var s3 = new LedgerState();
        Mark(s3, many, false, out c);
        Check(s3.Marks.Count == LedgerStories.MarksMost, "milestones: the record holds " + LedgerStories.MarksMost + " companies at most (" + s3.Marks.Count + ")");
        // Timing: once a morning over the whole market.
        var market = new List<LedgerCompany>();
        for (int i = 1; i <= 170; i++)
        {
            market.Add(new LedgerCompany { Id = i, Name = "C" + i, Value = 2e8 + i * 1.3e8, Player = i == 77 });
        }
        var s4 = new LedgerState();
        Mark(s4, market, false, out c);
        var t = Stopwatch.StartNew();
        for (int round = 0; round < 50; round++)
        {
            for (int i = 0; i < market.Count; i++)
            {
                LedgerCompany k = market[i];
                k.Value *= 1.01;
                market[i] = k;
            }
            Mark(s4, market, false, out c);
        }
        double ms = t.Elapsed.TotalMilliseconds / 50.0;
        Check(ms < 2.0, "milestones: a morning's milestones over 170 companies take " + ms.ToString("0.000", CultureInfo.InvariantCulture) + " ms (wanted under 2 ms, once a morning)");
    }

    private static void LedgerStoriesTop()
    {
        var s = new LedgerState();
        Mark(s, Companies(1, "Harbour City Bancorp", 30e9, false, 2, "Pier Foods", 29.8e9, false, 900, "Mine", 5e9, true), false, out LedgerStoryCount c);
        List<LedgerPiece> p = Mark(s, Companies(1, "Harbour City Bancorp", 30e9, false, 2, "Pier Foods", 30.3e9, false, 900, "Mine", 5e9, true), false, out c);
        Check(p.Count == 0 && s.TopId == 1, "most valuable: a rival one per cent ahead does not take the place (the margin is " + LedgerStories.TopLeadBp + " bp), so two close companies do not swap it daily");
        p = Mark(s, Companies(1, "Harbour City Bancorp", 30e9, false, 2, "Pier Foods", 30.7e9, false, 900, "Mine", 5e9, true), false, out c);
        Check(p.Count == 1 && p[0].Key == LedgerStories.TopCity && p[0].Value("name") == "Pier Foods" && p[0].Value("other") == "Harbour City Bancorp" && s.TopId == 2 && c.Top == 1,
            "most valuable: a city company " + (LedgerStories.TopLeadBp / 100) + "% ahead takes the place, with the company it passed named second");
        p = Mark(s, Companies(1, "Harbour City Bancorp", 30e9, false, 2, "Pier Foods", 30.7e9, false, 900, "Mine", 40e9, true), false, out c);
        LedgerPiece won = p.FirstOrDefault(x => x.Key.StartsWith("reallife_ledger_top", StringComparison.Ordinal));
        bool first = c.Top == 1 && won != null && won.Key == LedgerStories.TopFirst && won.Value("name") == "Mine" && won.Value("other") == "Pier Foods";
        Check(first && s.Marks.First(m => m.Id == 900).Topped, "most valuable: a player's company taking the place has the front page's story, the first time");
        p = Mark(s, Companies(1, "Harbour City Bancorp", 30e9, false, 2, "Pier Foods", 45e9, false, 900, "Mine", 40e9, true), false, out c);
        Check(p.Count == 1 && p[0].Key == LedgerStories.TopLost && p[0].Value("name") == "Mine" && p[0].Value("other") == "Pier Foods",
            "most valuable: losing the place is the player's company's story, naming who passed it (one story for the change, not two)");
        p = Mark(s, Companies(1, "Harbour City Bancorp", 30e9, false, 2, "Pier Foods", 45e9, false, 900, "Mine", 47e9, true), false, out c);
        Check(p.Count == 1 && p[0].Key == LedgerStories.TopAgain, "most valuable: taking it back is 'once more', not the first time again");
        // The holder leaves the market: the largest takes the place at once.
        p = Mark(s, Companies(1, "Harbour City Bancorp", 30e9, false, 2, "Pier Foods", 45e9, false), false, out c);
        Check(p.Count == 1 && p[0].Key == LedgerStories.TopCity && p[0].Value("name") == "Pier Foods" && p[0].Value("other") == "Harbour City Bancorp" && s.TopId == 2,
            "most valuable: when the holder leaves the market, the largest left takes the place with no margin, the next named second");
        var lone = new LedgerState();
        Mark(lone, Companies(4, "Alone", 1e9, false), false, out c);
        p = Mark(lone, Companies(5, "Other", 2e9, false), false, out c);
        Check(p.Count == 0 && lone.TopId == 5, "most valuable: with no second company there is no story to write, and the place still moves");
        // A tie goes to the lower id on every machine.
        var tie = new LedgerState();
        Mark(tie, Companies(9, "Nine", 5e9, false, 3, "Three", 5e9, false), false, out c);
        Check(tie.TopId == 3, "most valuable: a tie goes to the lower id, the same on every machine");
    }

    private static void LedgerStoriesBeaten()
    {
        var b = new LedgerBeat { Tycoon = 1, Day = 40, Seed = 1234, Rival = "Bald Luthor", Holding = "Luthor Everything Co.", Buyer = "Corner Deli Group", Owner = "Sam", Quip = "Q.", Cost = 125e6, Back = 90e6, Days = 5 };
        LedgerPiece p = LedgerStories.Beaten(b, StoryMoney);
        Check(p != null && Array.IndexOf(LedgerStories.BeatenKeys, p.Key) >= 0 && p.Section == LedgerSection.Business && p.Value("cost") == "$125000000" && p.Value("back") == "$90000000"
              && p.Value("days") == "5" && p.Value("buyer") == "Corner Deli Group" && p.Value("owner") == "Sam" && p.Value("quip") == "Q." && p.Value("holding") == "Luthor Everything Co.",
            "rival beaten: the story names the rival, the holding, the player's company and the player, the cost, the break-up money and its days, and the rival's own line");
        Check(LedgerStories.Beaten(b, StoryMoney).Key == p.Key, "rival beaten: the version is the same each time it is drawn (every machine prints the same)");
        var seen = new HashSet<string>();
        for (int day = 1; day <= 60; day++)
        {
            for (int t = 0; t < 3; t++)
            {
                seen.Add(LedgerStories.BeatenKey(new LedgerBeat { Tycoon = t, Day = day, Seed = 77, Rival = "R", Cost = 1 }));
            }
        }
        Check(seen.Count == LedgerStories.BeatenKeys.Length, "rival beaten: every version is drawn over a year of takeovers (" + seen.Count + " of " + LedgerStories.BeatenKeys.Length + ")");
        b.Cost = 0;
        Check(LedgerStories.Beaten(b, StoryMoney).Key == LedgerStories.BeatenPlain, "rival beaten: without the cost (the counter's note gone) the plain version, which names no money");
        Check(LedgerStories.Beaten(null, StoryMoney) == null && LedgerStories.Beaten(new LedgerBeat(), StoryMoney) == null, "rival beaten: no rival, no story");
        // The rivals' one line of the same takeover goes; another rival's, or with no story, stays.
        var pieces = new List<LedgerPiece>
        {
            LedgerRules.Piece(LedgerStories.RivalTakeoverKey, new[] { "rival", "Bald Luthor", "holding", "Luthor Everything Co." }),
            LedgerRules.Piece(LedgerStories.RivalTakeoverKey, new[] { "rival", "The Zucc", "holding", "Zucc Social Holdings" }),
            LedgerStories.Beaten(new LedgerBeat { Rival = "Bald Luthor", Cost = 5 }, StoryMoney)
        };
        int gone = LedgerStories.Covered(pieces);
        Check(gone == 1 && pieces.Count == 2 && pieces.Any(x => x.Key == LedgerStories.RivalTakeoverKey && x.Value("rival") == "The Zucc"),
            "rival beaten: the rivals' own one line of the same takeover gives way to the story; another rival's stays");
        // AddOnce: a guest's piece taken again, or the test pressed twice, is one piece.
        var list = new List<LedgerPiece>();
        bool a = LedgerStories.AddOnce(list, LedgerRules.Piece(LedgerStories.TopCity, new[] { "name", "X" })), again = LedgerStories.AddOnce(list, LedgerRules.Piece(LedgerStories.TopCity, new[] { "name", "X" }));
        bool other = LedgerStories.AddOnce(list, LedgerRules.Piece(LedgerStories.TopCity, new[] { "name", "Y" }));
        Check(a && !again && other && list.Count == 2 && !LedgerStories.AddOnce(list, null), "milestones: the same story is put in once");
        // A takeover by a listed company.
        LedgerPiece own = LedgerStories.Bought("Zucc Social Holdings", "Corner Deli Group", "$12.00", true), city = LedgerStories.Bought("Harbour City Bancorp", "Dockside Savings", "$4.10", false);
        Check(own.Key == LedgerStories.BoughtOwn && own.Value("bidder") == "Zucc Social Holdings" && city.Key == LedgerStories.BoughtCity && city.Value("price") == "$4.10",
            "takeovers: a completed takeover names its buyer, heavier when the company taken was a player's");
        // The guest's piece: a rival is the player's own, so a guest's story goes to the host as one of its own pieces.
        LedgerPiece guest = LedgerStories.Beaten(new LedgerBeat { Rival = "Space Karen", Holding = "Karen Aerospace & Motors", Buyer = "B", Owner = "O", Cost = 9, Back = 8, Days = 3 }, StoryMoney);
        string line = LedgerShare.AskPiece(guest);
        LedgerPiece back = line != null ? LedgerShare.ReadPiece(line.Split('|')) : null;
        Check(LedgerShare.Personal(guest.Key) && back != null && back.Key == guest.Key && back.Value("rival") == "Space Karen" && back.Weight == guest.Weight,
            "rival beaten: a guest's story is a piece about its player, sent to the host in the line the host already reads, whole");
        Check(!LedgerShare.Personal(LedgerStories.MileBig) && !LedgerShare.Personal(LedgerStories.TopCity) && !LedgerShare.Personal(LedgerStories.BoughtCity),
            "milestones: a guest sends no milestone (the host's paper has the shared market's own)");
        // The test's milestone: the player's company at its highest figure, else the city's largest at its billion, else none.
        LedgerPiece test = LedgerStories.TestMark(Companies(1, "Big Co", 30e9, false, 9, "Mine", 2.7e9, true), StoryMoney, out bool isOwn, out string name);
        Check(test != null && isOwn && name == "Mine" && test.Key == LedgerStories.MileBig && test.Value("mark") == "$2000000000", "test: the milestone is the player's company's highest figure");
        test = LedgerStories.TestMark(Companies(1, "Big Co", 30e9, false, 9, "Mine", 40e6, true), StoryMoney, out isOwn, out name);
        Check(test != null && !isOwn && name == "Big Co" && test.Key == LedgerStories.MileCity, "test: a player's company under the first figure gives way to the city's largest");
        test = LedgerStories.TestMark(Companies(1, "Small", 300e6, false), StoryMoney, out isOwn, out name);
        Check(test == null && LedgerStories.TestMark(new List<LedgerCompany>(), StoryMoney, out isOwn, out name) == null, "test: no figure to print, no milestone (the notice says so)");
    }

    private static void LedgerStoriesStore()
    {
        var s = new LedgerState { Subscribed = true, PaidUntil = 30 };
        string old = LedgerStore.Write(s);
        Check(!old.Contains("\nM|") && !LedgerStore.Read(old).MarksKept, "milestones' save: no record, no line (the save is as before), and it reads as a save without the record");
        s.MarksKept = true;
        s.TopId = 12;
        s.Marks.Add(new LedgerMark { Id = 12, Rung = 9, Topped = true });
        s.Marks.Add(new LedgerMark { Id = 3, Rung = 0 });
        s.Marks.Add(new LedgerMark { Id = 900, Rung = 16 });
        string text = LedgerStore.Write(s);
        LedgerState r = LedgerStore.Read(text);
        Check(r.MarksKept && r.TopId == 12 && r.Marks.Count == 3 && r.Marks[0].Id == 12 && r.Marks[0].Rung == 9 && r.Marks[0].Topped && r.Marks[1].Rung == 0 && !r.Marks[1].Topped
              && r.Marks[2].Rung == 16 && r.Subscribed && r.PaidUntil == 30,
            "milestones' save: the record comes back whole beside the subscription");
        var empty = new LedgerState { MarksKept = true };
        LedgerState e = LedgerStore.Read(LedgerStore.Write(empty));
        Check(e.MarksKept && e.Marks.Count == 0 && e.TopId == 0, "milestones' save: a record kept with no company yet stays kept");
        LedgerState bad = LedgerStore.Read(LedgerStore.Write(new LedgerState()) + "\nM|7|x:y,5:99:1,0:2:0,8:3");
        Check(bad.MarksKept && bad.TopId == 7 && bad.Marks.Count == 1 && bad.Marks[0].Id == 5 && bad.Marks[0].Rung == LedgerStories.Ladder.Length,
            "milestones' save: a damaged company in the line is left out, a figure past the ladder held to its top");
        // An older version reads the line as one it does not know, and the edition's lines are untouched by it.
        var withEdition = new LedgerState { MarksKept = true, TopId = 4 };
        withEdition.Edition = new LedgerEdition { Day = 22 };
        withEdition.Edition.Pieces.Add(LedgerRules.Piece(LedgerStories.TopCity, new[] { "name", "A" }));
        LedgerState w = LedgerStore.Read(LedgerStore.Write(withEdition));
        Check(w.Edition != null && w.Edition.Day == 22 && w.Edition.Pieces.Count == 1 && w.TopId == 4, "milestones' save: the edition's lines read as before beside the record");
        var shared = new System.Text.StringBuilder();
        LedgerStore.Edition(shared, withEdition.Edition);
        Check(!shared.ToString().Contains("\nM|") && !LedgerShare.Write(withEdition.Edition).Contains("\nM|"),
            "milestones' save: the record stays on each machine: the edition a host sends its guests carries no line of it");
    }

    // The real market, two 60-day years from two seeds: the city's companies' worth spans the ladder, and the stories are news,
    // not noise (a few first billions and changes at the top a year, each company's first billion once).
    private static void LedgerStoriesMarket()
    {
        double least = double.MaxValue, most = 0;
        int cityFirsts = 0, topChanges = 0, mornings = 0;
        var firsts = new HashSet<string>();
        bool once = true;
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
                        list.Add(new LedgerCompany { Id = k.Id, Name = k.Name, Value = k.Shares * (double)k.Daily[k.Daily.Count - 1].C / 100.0, Player = false });
                    }
                }
                if (list.Count == 0)
                {
                    continue;
                }
                if (least == double.MaxValue || day == 2)
                {
                    least = Math.Min(least, list.Min(x => x.Value));
                    most = Math.Max(most, list.Max(x => x.Value));
                }
                List<LedgerPiece> p = Mark(s, list, false, out LedgerStoryCount c);
                mornings++;
                foreach (LedgerPiece piece in p)
                {
                    if (piece.Key == LedgerStories.MileCity)
                    {
                        cityFirsts++;
                        once &= firsts.Add(seed + ":" + piece.Value("name"));
                    }
                    else if (piece.Key == LedgerStories.TopCity)
                    {
                        topChanges++;
                    }
                }
            }
        }
        Console.WriteLine("    the market's companies at the start: " + (least / 1e6).ToString("0", CultureInfo.InvariantCulture) + " million to " + (most / 1e9).ToString("0.0", CultureInfo.InvariantCulture)
                          + " billion; over " + mornings + " mornings: " + cityFirsts + " first billion(s), " + topChanges + " change(s) at the top");
        Check(least < LedgerStories.Ladder[1] && most > LedgerStories.Ladder[12] && most < LedgerStories.Ladder[LedgerStories.Ladder.Length - 1],
            "milestones: the city's companies start from under 250 million to over ten billion, so the ladder's figures are reached and its top is still ahead of them");
        Check(once && cityFirsts <= mornings / 10 && topChanges <= mornings / 10,
            "milestones: on the real market a city company's first billion and a change at the top are news, at most one morning in ten (" + cityFirsts + " and " + topChanges + " in " + mornings + ")");
    }

    private static void LedgerStoriesSources()
    {
        string stories = ModSource(@"Ledger\LedgerDirector.Stories.cs");
        string director = ModSource(@"Ledger\LedgerDirector.cs");
        string links = ModSource(@"Wiring\LedgerLinks.cs");
        string share = ModSource(@"Ledger\LedgerDirector.Share.cs");
        string print = MemberText(director, "private void Print(int day, int hour, bool again)");
        Check(print.Contains("StoriesInto(e, since, day, fresh);") && print.Contains("StoriesCheck(e, stories);")
              && print.IndexOf("StoriesInto(", StringComparison.Ordinal) < print.IndexOf("RemoveRange(LedgerCatalogue.PiecesMost", StringComparison.Ordinal),
            "milestones' sources: an edition reads its stories before it is held to its pieces, and checks them after");
        string into = MemberText(stories, "private void StoriesInto(LedgerEdition e, int since, int day, bool fresh)");
        Check(into.Contains("if (!Settings.LedgerStories)") && into.Contains("LedgerStories.Forget(_state)") && into.Contains("if (fresh)") && into.Contains("LedgerStories.Kept(kept.Key)")
              && into.Contains("LedgerStories.Covered(e.Pieces)"),
            "milestones' sources: off, nothing is read and the record goes; the record moves only on an edition's first print, a reprint keeps what it printed");
        Check(MemberText(share, "private void GuestPieces(int day, int hour)").Contains("StoriesGuest(day, _scratch);")
              && MemberText(stories, "private void StoriesGuest(int day, List<LedgerPiece> into)").Contains("LedgerStories.Marks(_state, _companies, true,"),
            "milestones' sources: a guest following the host's paper keeps its record in step, quietly, and sends its own rival's story");
        Check(links.Contains("n.Kind == MarketNewsKind.BidDone && Settings.LedgerStories ? Bought(n, s) : null") && links.Contains("LedgerBridges.Companies = Companies;")
              && links.Contains("LedgerBridges.Beaten = Beaten;") && links.Contains("LedgerBridges.TestBeat = TestBeat;"),
            "milestones' sources: the wiring hands the paper the companies, the rivals taken over, the test's takeover and the bidder's name, only with the option on");
        string market = string.Join("\n", System.IO.Directory.GetFiles(System.IO.Path.GetDirectoryName(ModSourcePath(@"Markets\MarketModel.cs")), "*.cs").Select(System.IO.File.ReadAllText));
        Check(!market.Contains("LedgerStories") && !market.Contains("reallife_ledger_mile") && (int)MarketNewsKind.Count == 36 && MarketNewsKind.Reverse == (MarketNewsKind)34 && MarketNewsKind.Block == (MarketNewsKind)35,   // (the two after 33 are the stock market extras' own)
            "milestones' sources: the market is untouched: no new kind of news on its tape or in its session's lines, the stories read on each machine from what it has");
        Check(MemberText(stories, "private void StoriesCheck(LedgerEdition e, int found)").Contains("Meta.Dev.Verbose") && stories.Contains("City Ledger check (milestones and rivals)"),
            "milestones' sources: the self-check is one line an edition, with Developer diagnostics on");
        string tests = ModSource(@"Ledger\LedgerDirector.Tests.cs");
        string test = MemberText(tests, "public void TestStories()");
        Check(test.Contains("TestRefusal.FeatureOff") && test.Contains("reallife_ledger_test_stories_guest") && test.Contains("reallife_ledger_test_stories_no_market")
              && test.Contains("reallife_ledger_test_stories_city") && test.Contains("Read(LedgerPage.FromTest)") && !test.Contains("_state.Marks"),
            "milestones' test: it refuses with the option off or as a guest, opens the paper, says why the milestone is not the player's, and leaves the record alone");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        Check(mod.Contains("Settings.LedgerStoriesId, \"reallife_ledger_opt_stories\"") && mod.Contains("TestStories()") && ModSource(@"Meta\FeatureList.Life.cs").Contains("Settings.LedgerStoriesId)")
              && ModSource(@"Meta\OptionsTree.cs").Contains(".Opts(\"ledger_stories\")") && ModSource(@"Core\Settings.cs").Contains("public static bool LedgerStories = true;"),
            "milestones' option: on by default, in the City Ledger's options, owned by its Guide page, with its test button");
    }

    private static string ModSourcePath(string relative)
    {
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts", relative));
    }

    private static void LedgerStoriesTexts()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("milestones' texts", words, codes))
        {
            return;
        }
        var keys = new List<string>
        {
            "reallife_ledger_opt_stories", "reallife_opt_tip_ledger_stories", "reallife_ledger_test_stories", "reallife_ledger_test_stories_guest", "reallife_ledger_test_stories_city",
            "reallife_ledger_test_stories_no_market"
        };
        foreach (LedgerCatalogue.Kind k in LedgerCatalogue.Kinds)
        {
            if (LedgerStories.Story(k.Prefix))
            {
                keys.Add(k.Prefix);
                keys.Add(k.Head);
            }
        }
        Check(keys.Count == 6 + 2 * 13, "milestones' texts: every story kind has its text and its headline (" + keys.Count + " keys)");
        // Each text names what its piece carries, and nothing it does not.
        var carries = new Dictionary<string, string[]>
        {
            { "beaten", new[] { "rival", "holding", "buyer", "owner", "cost", "back", "days", "quip" } }, { "bought", new[] { "bidder", "name", "price" } },
            { "mile", new[] { "name", "mark", "value" } }, { "top", new[] { "name", "value", "other", "other_value" } }
        };
        for (int l = 0; l < 3; l++)
        {
            string bad = null;
            foreach (string key in keys)
            {
                if (!words[l].TryGetValue(key, out string value) || value.Length == 0 || value.Contains('!') || value.Contains((char)0x2014) || value.Contains((char)0x2013) || value.Contains(" - "))
                {
                    bad = key;
                    break;
                }
                foreach (KeyValuePair<string, string[]> c in carries)
                {
                    if (key.Contains("_ledger_" + c.Key) || key.Contains("_head_" + c.Key))
                    {
                        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(value, @"\{([a-z_]+)\}"))
                        {
                            if (Array.IndexOf(c.Value, m.Groups[1].Value) < 0)
                            {
                                bad = key + " ({" + m.Groups[1].Value + "})";
                            }
                        }
                    }
                }
                if (LedgerStories.Story(key) && value.Length > 400)
                {
                    bad = key + " (too long for a story)";
                }
            }
            Check(bad == null, "milestones' texts: every text is in " + codes[l] + ", plain (no dash, no exclamation mark), and names only what its piece carries" + (bad != null ? " (not: " + bad + ")" : string.Empty));
            Check(words[l].TryGetValue("reallife_ledger_test_stories_city", out string city) && city.Length <= 240 && city.Contains("{name}") && city.Contains("{mark}")
                  && words[l].TryGetValue("reallife_ledger_test_stories_no_market", out string none) && none.Length <= 240,
                "milestones' texts: the test's notices in " + codes[l] + " are at most 240 characters and name the company used");
            Check(words[l].TryGetValue("reallife_guide_ledger", out string guide) && guide.Contains("{ledger_mile_first}") && guide.Contains("{ledger_mile_big}") && guide.Contains("{ledger_mile_city}")
                  && guide.Contains("{ledger_top_lead}") && guide.Contains(words[l]["reallife_ledger_opt_stories"]),
                "milestones' Guide: the City Ledger's page in " + codes[l] + " tells the stories with the code's figures and names the option");
        }
        // The Guide's page in a later language: where it has the page, it has the new section's figures too (locales.py holds the rest).
        for (int l = 3; l < words.Length; l++)
        {
            Check(!words[l].TryGetValue("reallife_guide_ledger", out string g) || g == words[0]["reallife_guide_ledger"] || g.Contains("{ledger_top_lead}"),
                "milestones' Guide: " + codes[l] + "'s City Ledger page, where it has its own, tells of the stories as the English does");
        }
        Check(WhatsNewHas(words[0], "The City Ledger now reports when you take over a rival tycoon") && WhatsNewHas(words[1], "O City Ledger agora noticia quando você compra um magnata rival")
              && WhatsNewHas(words[2], "城市纪事报现在都会报道"),
            "milestones' texts: What's new has the stories' line in en, pt and zh-cn (any of its rows)");
    }
}
