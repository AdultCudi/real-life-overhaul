using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul.Ledger;

// The City Ledger (Scripts/Ledger): what section and weight each kind of piece has, how a piece's words divide into a
// kicker, a headline and its story, the subscription's round (a week paid at a time, never a morning charged twice, never
// one the round did not come), the copy the player has and where it can be read, the page's choices (the lead, a
// section's order, the movers, the busiest companies, the share prices) and the save's line, whole and damaged.
internal static partial class Program
{
    private static void Ledger()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        LedgerCatalogueChecks();
        LedgerKinds();
        LedgerSplit();
        LedgerSubscription();
        LedgerCopies();
        LedgerPage();
        LedgerStoreChecks();
        Console.WriteLine("  the City Ledger: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    private static void LedgerCatalogueChecks()
    {
        LedgerCatalogue.Kind[] kinds = LedgerCatalogue.Kinds;
        bool prefixes = true, reachable = true, heads = true;
        for (int i = 0; i < kinds.Length; i++)
        {
            prefixes &= kinds[i].Prefix.StartsWith("reallife_", StringComparison.Ordinal);
            heads &= kinds[i].Head == null || kinds[i].Head.StartsWith("reallife_ledger_head_", StringComparison.Ordinal);
            for (int j = 0; j < i; j++)
            {
                reachable &= !kinds[i].Prefix.StartsWith(kinds[j].Prefix, StringComparison.Ordinal);
            }
        }
        Check(prefixes, "every kind of piece is a reallife_ key");
        Check(reachable, "no kind of piece stands behind a shorter one it starts with (it would never be reached)");
        Check(heads, "every headline of the paper's own is one of its keys");
        Check(LedgerCatalogue.SectionKeys.Length == (int)LedgerSection.Count, "a name for every section");
        Check(LedgerCatalogue.CopyAtStart > 0 && LedgerCatalogue.WeekAtStart > LedgerCatalogue.CopyAtStart && LedgerCatalogue.WeekAtStart < LedgerCatalogue.CopyAtStart * LedgerCatalogue.WeekDays,
            "a week delivered costs more than a copy and less than a copy a day");
        Check(LedgerCatalogue.KioskOpenHour == LedgerCatalogue.DeliveryHour && LedgerCatalogue.KioskCloseHour > LedgerCatalogue.KioskOpenHour,
            "the kiosks open when the paper is out, and close later the same day");
        Check(LedgerCatalogue.KioskNearest < LedgerCatalogue.KioskFurthest && LedgerCatalogue.KioskApart < LedgerCatalogue.KioskFurthest * 2f, "the kiosks' distances make sense");
    }

    private static void LedgerKinds()
    {
        Kind("reallife_news_accident_severe_2", LedgerSection.City, 90, null);
        Kind("reallife_news_accident_minor_1", LedgerSection.City, 30, null);
        Kind("reallife_news_playerarrest_1", LedgerSection.City, 75, null);
        Kind("reallife_news_arrest_3", LedgerSection.City, 55, null);
        Kind("reallife_news_carbreakin_2", LedgerSection.City, 35, null);
        Kind("reallife_news_breakin_1", LedgerSection.City, 70, null);
        Kind("reallife_news_story_fire", LedgerSection.City, 85, null);
        Kind("reallife_civic_news_result_body", LedgerSection.City, 95, "reallife_ledger_head_election");
        Kind("reallife_civic_news_something_new", LedgerSection.City, 70, null);
        Kind("reallife_macro_news_rate_up", LedgerSection.Economy, 88, "reallife_ledger_head_rate_up");
        Kind("reallife_macro_news_rival", LedgerSection.Business, 45, "reallife_ledger_head_rival");
        Kind("reallife_macro_news_next_thing", LedgerSection.Economy, 40, null);
        Kind("reallife_co_news_strike", LedgerSection.Business, 72, "reallife_ledger_head_strike");
        Kind("reallife_news_shop_good_3", LedgerSection.Business, 25, null);
        Kind("reallife_mkt_news_bid_by", LedgerSection.Markets, 83, "reallife_ledger_head_bid");
        Kind("reallife_mkt_news_bid_failed", LedgerSection.Markets, 60, "reallife_ledger_head_bid_failed");
        Kind("reallife_mkt_news_failed_paid", LedgerSection.Markets, 87, "reallife_ledger_head_failed");
        Kind("reallife_mkt_news_craze_burst", LedgerSection.Markets, 76, "reallife_ledger_head_craze_burst");
        Kind("reallife_mkt_news_craze", LedgerSection.Markets, 66, "reallife_ledger_head_craze");
        Kind("reallife_mkt_news_rate_down", LedgerSection.Markets, 70, "reallife_ledger_head_market_rate");
        Kind("reallife_mkt_news_down", LedgerSection.Markets, 35, "reallife_ledger_head_down");
        Kind("reallife_news_rained_2", LedgerSection.Weather, 10, null);
        Kind("reallife_news_person_seen_1", LedgerSection.People, 20, null);
        Kind("reallife_news_filler_33", LedgerSection.AroundTown, 5, null);
        Kind("reallife_event_today_farmers", LedgerSection.AroundTown, 18, null);
        Kind("reallife_somebody_new_piece", LedgerSection.City, LedgerCatalogue.UnknownWeight, null);
        Check(LedgerRules.Piece("reallife_news_masthead", null) == null, "the old paper's masthead is left out (the page has its own)");
        LedgerPiece heavier = LedgerRules.Piece("reallife_mkt_news_results_beat", new[] { "name", "Harbour Bank" }, 12);
        Check(heavier != null && heavier.Weight == 60 && heavier.Value("name") == "Harbour Bank" && heavier.Value("eps") == null, "a piece carries its names and the weight it was given on top");
    }

    private static void Kind(string key, LedgerSection section, int weight, string head)
    {
        LedgerPiece p = LedgerRules.Piece(key, null);
        Check(p != null && p.Section == section && p.Weight == weight && p.Head == head,
            key + " goes in " + section + " weighing " + weight + (head != null ? " under " + head : string.Empty)
            + (p == null ? " (left out)" : " (it is " + p.Section + ", " + p.Weight + ", " + (p.Head ?? "no headline") + ")"));
    }

    private static void LedgerSplit()
    {
        Split("BUMP ON Elm Street. A light collision on Elm Street at about 14:00. Nobody was hurt.", null, "BUMP ON Elm Street", "A light collision on Elm Street at about 14:00. Nobody was hurt.");
        Split("A QUIET DAY. Police report no incidents of note.", null, "A QUIET DAY", "Police report no incidents of note.");
        Split("COURT: GUILTY. The city court handed down a conviction.", "COURT", "GUILTY", "The city court handed down a conviction.");
        Split("WEATHER: Dry and settled. A good day to be out.", "WEATHER", null, "Dry and settled. A good day to be out.");
        Split("ON THIS DAY: nothing in particular happened.", "ON THIS DAY", null, "nothing in particular happened.");
        Split("HOROSCOPE: Today favours those who look both ways. Tomorrow also.", "HOROSCOPE", null, "Today favours those who look both ways. Tomorrow also.");
        Split("LETTERS: \"Sir, the benches face the wrong way.\"", "LETTERS", null, "\"Sir, the benches face the wrong way.\"");
        Split("BREAK-IN AT Corner Deli. Thieves forced their way in overnight.", null, "BREAK-IN AT Corner Deli", "Thieves forced their way in overnight.");
        Split("Elm Street, 14:00: a mugging. Officers attended.", null, null, "Elm Street, 14:00: a mugging. Officers attended.");
        Split("I think the trams are late again. They always are.", null, null, "I think the trams are late again. They always are.");
        Split("The central bank raises its rate tomorrow: up 0.25, to 4.25%. Money is tighter.", null, null, "The central bank raises its rate tomorrow: up 0.25, to 4.25%. Money is tighter.");
        Split("FIRE CREWS CALLED OUT.", null, null, "FIRE CREWS CALLED OUT.");
        Split("TEMPO: Seco e estável. Um bom dia para sair.", "TEMPO", null, "Seco e estável. Um bom dia para sair.");
        Split("BATIDA LEVE EM Rua Augusta. Uma colisão leve por volta das 14:00.", null, "BATIDA LEVE EM Rua Augusta", "Uma colisão leve por volta das 14:00.");
        Split("NEGÓCIOS: A loja vendeu bem.", "NEGÓCIOS", null, "A loja vendeu bem.");
        Split(string.Empty, null, null, string.Empty);
        Split(null, null, null, string.Empty);
    }

    private static void Split(string text, string kicker, string headline, string body)
    {
        LedgerRules.Split(text, out string k, out string h, out string b);
        Check(k == kicker && h == headline && b == body, "the paper divides \"" + (text ?? "(nothing)") + "\" into kicker '" + (kicker ?? "-") + "', headline '" + (headline ?? "-")
                                                        + "' and its story (it gave '" + (k ?? "-") + "', '" + (h ?? "-") + "', '" + b + "')");
    }

    private static void LedgerSubscription()
    {
        // Taken out at ten in the morning on day 5, after that morning's round: the first paper comes on day 6.
        var s = new LedgerState { LastMorning = 5 };
        Check(LedgerRules.CanSubscribe(s) && LedgerRules.Subscribe(s, 5, 10, out bool pay) && pay, "a subscription can be taken out, and the first week is paid for at once");
        LedgerRules.Start(s, 5, 10);
        Check(s.Subscribed && !s.Cancelled && s.PaidUntil == 12 && s.Weeks == 1 && s.Since == 5, "taken out on day 5 after the round: seven papers, days 6 to 12 (paid until " + s.PaidUntil + ")");
        Check(!LedgerRules.Subscribe(s, 5, 11, out pay) && !pay, "a second subscription is not taken out on top of one that runs");
        int charged = 0;
        Func<bool> payWeek = () => { charged++; return true; };
        int delivered = 0;
        for (int day = 6; day <= 12; day++)
        {
            LedgerMorning m = LedgerRules.Morning(s, day, payWeek);
            delivered += m == LedgerMorning.Kept || m == LedgerMorning.Billed ? 1 : 0;
            Check(LedgerRules.Morning(s, day, payWeek) == LedgerMorning.None, "the round comes once a morning (day " + day + ")");
        }
        Check(delivered == 7 && charged == 0, "the week paid for brings seven papers and no charge (" + delivered + " papers, " + charged + " charges)");
        Check(LedgerRules.NextPayment(s) == 13, "the next week is paid for on day 13");
        Check(LedgerRules.Morning(s, 13, payWeek) == LedgerMorning.Billed && charged == 1 && s.PaidUntil == 19 && s.Weeks == 2, "on day 13 the next week is paid for, to day 19");

        // Cancelled on day 14: the paper still comes until day 19, then the subscription ends without a charge.
        Check(LedgerRules.Cancel(s) && s.Cancelled && !LedgerRules.Cancel(s), "a subscription is cancelled once");
        Check(LedgerRules.NextPayment(s) == 0, "a cancelled subscription has no next payment");
        delivered = 0;
        for (int day = 14; day <= 19; day++)
        {
            delivered += LedgerRules.Morning(s, day, payWeek) == LedgerMorning.Kept ? 1 : 0;
        }
        Check(delivered == 6 && charged == 1, "after cancelling the paper comes until the last day paid for (" + delivered + " papers)");
        Check(LedgerRules.Morning(s, 20, payWeek) == LedgerMorning.Ended && !s.Subscribed && !s.Cancelled && charged == 1, "then it ends, and nothing more is charged");
        Check(LedgerRules.Morning(s, 21, payWeek) == LedgerMorning.None && charged == 1, "with no subscription the round brings nothing");

        // Taken up again inside the days paid for: nothing to pay now.
        var again = new LedgerState { LastMorning = 30 };
        LedgerRules.Start(again, 30, 9);
        LedgerRules.Cancel(again);
        Check(LedgerRules.Subscribe(again, 32, 9, out pay) && !pay && !again.Cancelled && again.Subscribed, "a cancelled subscription is taken up again with nothing to pay until its week is over");

        // Before the round: the first paper is this morning's.
        var early = new LedgerState { LastMorning = 39 };
        LedgerRules.Start(early, 40, 5);
        Check(early.PaidUntil == 46 && LedgerRules.Morning(early, 40, payWeek) == LedgerMorning.Kept, "taken out at 05:00, before the round: the first paper is that morning's");
        var lateAgain = new LedgerState { LastMorning = 40 };
        LedgerRules.Start(lateAgain, 40, 4);
        Check(lateAgain.PaidUntil == 47, "taken out early but after that morning's round (a test, a load): the first paper is tomorrow's");

        // A week that cannot be paid: the subscription stops and nothing is delivered.
        var poor = new LedgerState { LastMorning = 50 };
        LedgerRules.Start(poor, 50, 12);
        for (int day = 51; day <= 57; day++)
        {
            LedgerRules.Morning(poor, day, payWeek);
        }
        int before = charged;
        Check(LedgerRules.Morning(poor, 58, () => false) == LedgerMorning.Lapsed && !poor.Subscribed && charged == before, "a week that cannot be paid stops the subscription and delivers nothing");

        // Mornings the round did not come are not charged: the days paid for move on by as many.
        var away = new LedgerState { LastMorning = 60 };
        LedgerRules.Start(away, 60, 12);   // days 61 to 67
        LedgerRules.Morning(away, 61, payWeek);
        LedgerRules.Morning(away, 62, payWeek);
        before = charged;
        LedgerMorning back = LedgerRules.Morning(away, 70, payWeek);   // days 63 to 69 went by at once
        Check(back == LedgerMorning.Kept && away.PaidUntil == 74 && charged == before, "seven mornings missed: still paid for, to day 74, and nothing charged (" + back + ", " + away.PaidUntil + ")");

        // Over a long stretch, exactly a week's payment for every seven papers.
        var year = new LedgerState { LastMorning = 100 };
        LedgerRules.Start(year, 100, 12);
        int papers = 0;
        before = charged;
        for (int day = 101; day <= 100 + 7 * 52; day++)
        {
            LedgerMorning m = LedgerRules.Morning(year, day, payWeek);
            papers += m == LedgerMorning.Kept || m == LedgerMorning.Billed ? 1 : 0;
        }
        Check(papers == 7 * 52 && charged - before == 51 && year.Weeks == 52, "a year of mornings: " + papers + " papers for 52 weeks paid, one of them at the start (" + (charged - before) + " charged on the round)");
    }

    private static void LedgerCopies()
    {
        var s = new LedgerState { Edition = new LedgerEdition { Day = 8 } };
        Check(!LedgerRules.HasCopy(s) && !LedgerRules.BoughtCopy(s), "no copy until one is delivered or bought");
        LedgerRules.Deliver(s, 8);
        Check(LedgerRules.HasCopy(s) && !LedgerRules.BoughtCopy(s), "a delivered copy is read at home, not at a kiosk");
        LedgerRules.Buy(s, 8);
        Check(LedgerRules.HasCopy(s) && LedgerRules.BoughtCopy(s) && s.Delivered && s.Copies == 1, "a copy bought as well: read at home and at the kiosk");
        s.Edition = new LedgerEdition { Day = 9 };
        Check(!LedgerRules.HasCopy(s) && !LedgerRules.BoughtCopy(s), "yesterday's copy is not today's paper");
        LedgerRules.Buy(s, 9);
        Check(LedgerRules.BoughtCopy(s) && !s.Delivered && s.Copies == 2, "a copy of the new edition bought: only that one counts");
        LedgerRules.Deliver(s, 9);
        Check(s.Delivered && s.Bought, "then delivered as well: both");
        Check(!LedgerRules.KioskOpen(5) && LedgerRules.KioskOpen(6) && LedgerRules.KioskOpen(19) && !LedgerRules.KioskOpen(20), "the kiosks are open from 06:00 until 20:00");
        Check(LedgerRules.OnSale(s, 9, 10) && !LedgerRules.OnSale(s, 10, 10) && !LedgerRules.OnSale(s, 9, 21), "today's paper is on sale once it is out, while the kiosks are open");
    }

    private static void LedgerPage()
    {
        var e = new LedgerEdition { Day = 3 };
        e.Pieces.Add(new LedgerPiece(LedgerSection.Weather, 200, "reallife_news_dry_1", null, null));   // heavy, but no news section
        e.Pieces.Add(new LedgerPiece(LedgerSection.City, 60, "a", null, null));
        e.Pieces.Add(new LedgerPiece(LedgerSection.Economy, 88, "b", null, null));
        e.Pieces.Add(new LedgerPiece(LedgerSection.City, 90, "c", null, null));
        e.Pieces.Add(new LedgerPiece(LedgerSection.City, 60, "d", null, null));
        e.Pieces.Add(new LedgerPiece(LedgerSection.Markets, 90, "e", null, null));
        LedgerPiece lead = LedgerRules.Lead(e);
        Check(lead != null && lead.Key == "c", "the lead is the heaviest news piece, the first of two that weigh the same (" + (lead != null ? lead.Key : "none") + ")");
        var list = new List<LedgerPiece>();
        LedgerRules.Of(e, LedgerSection.City, lead, list);
        Check(list.Count == 2 && list[0].Key == "a" && list[1].Key == "d", "a section's pieces are the heaviest first, in their order where they weigh the same, without the lead");
        Check(LedgerRules.Lead(new LedgerEdition()) == null, "an empty edition has no lead");

        for (int i = 0; i < 12; i++)
        {
            e.Sectors.Add(new LedgerSector { Key = "s" + i, Level = 1000 + i });
        }
        AddQuote(e, "Zeta", 2, 1000, 300, 100);
        AddQuote(e, "Alpha", 2, 2000, 300, 50);
        AddQuote(e, "Beta", 0, 500, -250, 5000);
        AddQuote(e, "Gamma", 1, 1500, -400, 10);
        AddQuote(e, "Delta", 0, 800, 0, 0);
        var quotes = new List<LedgerQuote>();
        LedgerRules.Movers(e, true, 5, quotes);
        Check(quotes.Count == 2 && quotes[0].Name == "Alpha" && quotes[1].Name == "Zeta", "the risers: the largest rise first, a tie by name");
        LedgerRules.Movers(e, false, 1, quotes);
        Check(quotes.Count == 1 && quotes[0].Name == "Gamma", "the fallers: the largest fall first, as many as asked for");
        LedgerRules.MostActive(e, 3, quotes);
        Check(quotes.Count == 3 && quotes[0].Name == "Beta" && quotes[1].Name == "Alpha" && quotes[2].Name == "Zeta", "the busiest: by the money that changed hands (a tie by name), none that did not trade");
        LedgerRules.Listing(e, quotes);
        Check(quotes.Count == 5 && quotes[0].Name == "Beta" && quotes[1].Name == "Delta" && quotes[2].Name == "Gamma" && quotes[3].Name == "Alpha" && quotes[4].Name == "Zeta",
            "the share prices: by sector, then by name");
    }

    private static void AddQuote(LedgerEdition e, string name, int sector, int close, int changeBp, long volume)
    {
        e.Quotes.Add(new LedgerQuote { Name = name, Ticker = name.Substring(0, 3).ToUpperInvariant(), Sector = sector, Close = close, ChangeBp = changeBp, Volume = volume });
    }

    private static void LedgerStoreChecks()
    {
        var s = new LedgerState
        {
            Subscribed = true, Cancelled = true, PaidUntil = 44, Since = 31, Weeks = 2, LastMorning = 40, CopyDay = 40, Delivered = true, Bought = false, Copies = 3,
            KiosksFor = "12 Elm Street", Council = "0,1,-1,0|x"
        };
        s.Kiosks.Add(new KioskSpot { X = 101.25f, Y = 0.0312f, Z = -77.5f, Heading = 270.5f, Building = "14 Elm Street" });
        s.Kiosks.Add(new KioskSpot { X = -3.1f, Y = 0.2f, Z = 8.9f, Heading = 0f, Building = null });
        var e = new LedgerEdition { Day = 40, HasMarket = true, MarketDay = 39, IndexLevel = 102345, IndexChangeBp = -125, IndexWeekBp = 310, RateBp = 425, InflationBp = -30, CycleKey = "reallife_macro_state_boom" };
        e.Pieces.Add(new LedgerPiece(LedgerSection.Markets, 87, "reallife_mkt_news_failed_paid", "reallife_ledger_head_failed", new[] { "name", "Northgate Builders", "price", "$0.40" }));
        e.Pieces.Add(new LedgerPiece(LedgerSection.AroundTown, 5, "reallife_news_filler_7", null, null));
        e.Pieces.Add(new LedgerPiece(LedgerSection.City, 68, "reallife_ledger_policy_on", "reallife_ledger_head_policy_on", new[] { "policy", "Late licences | for bars", "effect", "Bars open later\nat night" }));
        e.Sectors.Add(new LedgerSector { Key = "reallife_mkt_sector_banks", Level = 99012, ChangeBp = 45 });
        e.Sectors.Add(new LedgerSector { Key = "reallife_mkt_sector_water", Level = 101000, ChangeBp = -2 });
        e.Quotes.Add(new LedgerQuote { Name = "Harbour Bank", Ticker = "HBK", Sector = 0, Close = 4512, ChangeBp = 88, Volume = 1234567, Flags = LedgerQuote.ExDividend });
        e.Quotes.Add(new LedgerQuote { Name = "Clearwater", Ticker = "CLW", Sector = 1, Close = 1999, ChangeBp = -10, Volume = 0, Flags = 0 });
        for (int i = 0; i < 5; i++)
        {
            e.IndexCloses.Add(100000 + i * 37);
        }
        s.Edition = e;
        string line = LedgerStore.Write(s);
        LedgerState r = LedgerStore.Read(line);
        Check(LedgerStore.Write(r) == line, "the save's line reads back to the same line");
        Check(r.Subscribed && r.Cancelled && r.PaidUntil == 44 && r.Since == 31 && r.Weeks == 2 && r.LastMorning == 40 && r.CopyDay == 40 && r.Delivered && !r.Bought && r.Copies == 3
              && r.KiosksFor == "12 Elm Street", "the subscription and the copy read back exactly");
        Check(r.Kiosks.Count == 2 && r.Kiosks[0].X == 101.25f && r.Kiosks[0].Y == 0.0312f && r.Kiosks[0].Heading == 270.5f && r.Kiosks[0].Building == "14 Elm Street" && r.Kiosks[1].Building == null,
            "the kiosks read back exactly");
        Check(r.Council == "0,1,-1,0/x", "how the council stood reads back (a bar in it becomes a slash)");
        LedgerPiece policy = r.Edition != null && r.Edition.Pieces.Count == 3 ? r.Edition.Pieces[2] : null;
        Check(policy != null && policy.Value("policy") == "Late licences / for bars" && policy.Value("effect") == "Bars open later at night" && policy.Head == "reallife_ledger_head_policy_on",
            "a piece's words never break the line (a bar becomes a slash, a line break a space)");
        LedgerEdition re = r.Edition;
        Check(re != null && re.Day == 40 && re.HasMarket && re.MarketDay == 39 && re.IndexLevel == 102345 && re.IndexChangeBp == -125 && re.IndexWeekBp == 310 && re.RateBp == 425
              && re.InflationBp == -30 && re.CycleKey == "reallife_macro_state_boom", "the edition's figures read back exactly");
        Check(re != null && re.Pieces.Count == 3 && re.Pieces[0].Value("price") == "$0.40" && re.Pieces[1].Data.Length == 0 && re.Pieces[1].Head == null && re.Sectors.Count == 2
              && re.Quotes.Count == 2 && re.Quotes[0].Volume == 1234567 && re.Quotes[0].Flags == LedgerQuote.ExDividend && re.IndexCloses.Count == 5 && re.IndexCloses[4] == 100148,
            "the edition's pieces, sectors, prices and chart read back exactly");

        Check(LedgerStore.Read(null).Edition == null && !LedgerStore.Read(string.Empty).Subscribed, "no entry: a new state");
        Check(!LedgerStore.Read("LG9|1|0|44|31|2|40|40|1|0|3|x").Subscribed, "an entry of another version is not read");
        Check(!LedgerStore.Read("LG1|1|0").Subscribed, "a header cut short is not read");
        LedgerState damaged = LedgerStore.Read(line.Replace("Q|Clearwater|CLW|1", "Q|Clearwater|CLW|7").Replace("K|-3.1", "K|nonsense").Replace("P|3|87", "P|99|87").Replace("S|reallife_mkt_sector_water|101000", "S|reallife_mkt_sector_water|abc")
                                               + "\nX|what is this\nQ|cut short\nC|1,2,x,4\nP|2");
        Check(damaged.Subscribed && damaged.Kiosks.Count == 1 && damaged.Edition != null && damaged.Edition.Pieces.Count == 2 && damaged.Edition.Quotes.Count == 1,
            "a damaged entry: what can be read is kept; a piece of no section, a company whose sector is not there and a kiosk with no place are left out");
        Check(damaged.Edition != null && damaged.Edition.Sectors.Count == 2 && damaged.Edition.Sectors[1].Level == 0, "a number that cannot be read counts as nothing");
        LedgerState noDay = LedgerStore.Read("LG1|0|0|0|0|0|0|0|0|0|0|\nE|0|0|0|0|0|0|0|0|\nP|0|1|reallife_news_filler_1|");
        Check(noDay.Edition == null, "an edition without a day is no edition");
        var many = new LedgerState();
        for (int i = 0; i < 6; i++)
        {
            many.Kiosks.Add(new KioskSpot { X = i, Y = 0, Z = 0 });
        }
        Check(LedgerStore.Read(LedgerStore.Write(many)).Kiosks.Count == LedgerCatalogue.KiosksWanted, "never more kiosks read back than the paper places");
    }
}
