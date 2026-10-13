using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul;
using RealLifeOverhaul.Ledger;

// Newsstand, the City Ledger online (Scripts/Ledger/Newsstand*): what a week of each title costs at every price level,
// the pass cheaper than the four, the connection rule (no working connection, no subscription and no edition, and never
// a charge for a morning that did not come), the week paid at a time, cancelling and taking up again, the pass over the
// titles' own subscriptions, the editions kept to be read again, what each title prints, and the save's line, whole and
// damaged.
internal static partial class Program
{
    private static void Newsstand()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        NewsstandPrices();
        NewsstandConnection();
        NewsstandWeeks();
        NewsstandPass();
        NewsstandKept();
        NewsstandTitles();
        NewsstandStoreChecks();
        Console.WriteLine("  Newsstand: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    private static void NewsstandPrices()
    {
        Check(NewsstandCatalogue.WeekAtStart.Length == NewsstandCatalogue.Subscriptions && NewsstandCatalogue.NameKeys.Length == NewsstandCatalogue.Subscriptions
              && NewsstandCatalogue.AboutKeys.Length == NewsstandCatalogue.Subscriptions && NewsstandCatalogue.TransactionKeys.Length == NewsstandCatalogue.Subscriptions,
            "a price, a name, a line and a transaction for every subscription");
        Check(NewsstandCatalogue.TaglineKeys.Length == NewsstandCatalogue.Titles && NewsstandCatalogue.Sections.Length == NewsstandCatalogue.Titles
              && NewsstandCatalogue.Markets.Length == NewsstandCatalogue.Titles && NewsstandCatalogue.SharePrices.Length == NewsstandCatalogue.Titles,
            "a tagline, its sections, its markets and its share prices for every title");
        var transactions = new HashSet<string>(NewsstandCatalogue.TransactionKeys);
        Check(transactions.Count == NewsstandCatalogue.Subscriptions, "each subscription's charge is a transaction of its own");
        foreach (string key in NewsstandCatalogue.TransactionKeys)
        {
            Check(key.StartsWith("reallife_transaction_", StringComparison.Ordinal), key + " is one of the mod's transactions");
        }
        bool cheaper = true, passCheaper = true, positive = true;
        foreach (int level in new[] { 500, 800, 1000, 1010, 1250, 1600, 2400, 4000 })
        {
            int sum = 0;
            for (int t = 0; t < NewsstandCatalogue.Titles; t++)
            {
                int price = NewsstandRules.WeekPrice(t, level);
                positive &= price > 0;
                sum += price;
            }
            int pass = NewsstandRules.WeekPrice(NewsstandCatalogue.Pass, level);
            positive &= pass > 0;
            passCheaper &= pass < sum;
            cheaper &= NewsstandRules.WeekPrice(NewsstandCatalogue.Ledger, level) < PriceRules.At(LedgerCatalogue.WeekAtStart, level);
        }
        Check(positive, "every week costs something at every price level");
        Check(cheaper, "the City Ledger online is cheaper than the printed one delivered, at every price level");
        Check(passCheaper, "the pass costs less than the four titles on their own, at every price level");
        Check(NewsstandRules.WeekPrice(NewsstandCatalogue.Exchange, PriceRules.Start) == NewsstandCatalogue.WeekAtStart[NewsstandCatalogue.Exchange],
            "at the start's prices a week is the catalogue's price");
        Check(NewsstandRules.WeekPrice(NewsstandCatalogue.Ledger, 2000) > NewsstandRules.WeekPrice(NewsstandCatalogue.Ledger, PriceRules.Start), "the city's price level raises the price");
    }

    private static void NewsstandConnection()
    {
        Check(NewsstandRules.Best(NetState.Online, NetState.NoLine) == NetState.Online && NewsstandRules.Best(NetState.Cut, NetState.Outage) == NetState.Outage
              && NewsstandRules.Best(NetState.NoHome, NetState.NoLine) == NetState.NoLine, "the best of the homes' connections is the one nearest to working");
        Check(NewsstandRules.Works(NetState.Online) && !NewsstandRules.Works(NetState.Outage) && !NewsstandRules.Works(NetState.Installing) && !NewsstandRules.Works(NetState.Cut)
              && !NewsstandRules.Works(NetState.NoLine) && !NewsstandRules.Works(NetState.NoHome), "only a working connection brings editions");
        var s = new NewsstandState();
        Check(NewsstandRules.Subscribe(s, NewsstandCatalogue.Street, NetState.NoHome, out bool pay) == OnlineRefusal.NoHome && !pay, "no home: no subscription");
        Check(NewsstandRules.Subscribe(s, NewsstandCatalogue.Street, NetState.NoLine, out pay) == OnlineRefusal.NoNet && !pay, "no connection at home: no subscription");
        Check(NewsstandRules.Subscribe(s, NewsstandCatalogue.Street, NetState.Outage, out pay) == OnlineRefusal.NoNet && !pay, "a connection that is down: no subscription");
        Check(NewsstandRules.Subscribe(s, NewsstandCatalogue.Street, NetState.Cut, out pay) == OnlineRefusal.NoNet && !pay, "a connection cut for a bill: no subscription");
        Check(!s.Subs[NewsstandCatalogue.Street].Subscribed, "a refused subscription changes nothing");
        Check(NewsstandRules.Subscribe(s, NewsstandCatalogue.Street, NetState.Online, out pay) == OnlineRefusal.None && pay, "a working connection: the first week is paid for at once");
    }

    private static void NewsstandWeeks()
    {
        const int T = NewsstandCatalogue.Exchange;
        var s = new NewsstandState();
        var results = new OnlineMorning[NewsstandCatalogue.Subscriptions];
        int charged = 0;
        Func<int, bool> pay = sub => { charged++; return true; };
        Func<int, bool> broke = sub => false;
        NewsstandRules.Subscribe(s, T, NetState.Online, out _);
        NewsstandRules.Start(s, T, 10);
        OnlineSub x = s.Subs[T];
        Check(x.Subscribed && x.PaidUntil == 16 && x.Weeks == 1 && x.Since == 10, "taken out on day 10: seven mornings, today's the first (paid until " + x.PaidUntil + ")");
        Check(NewsstandRules.Subscribe(s, T, NetState.Online, out _) == OnlineRefusal.Already, "a second subscription is not taken out on top of one that runs");
        Check(NewsstandRules.Round(s, 10, true, pay, results) == 1 && NewsstandRules.Has(s, T, 10) && charged == 0, "today's edition comes at once, with nothing more charged");
        Check(NewsstandRules.Round(s, 10, true, pay, results) == 0 && results[T] == OnlineMorning.None, "a title's edition comes once a morning");
        Check(!NewsstandRules.Has(s, NewsstandCatalogue.Ledger, 10), "a title not subscribed to gets nothing");
        int came = 0;
        for (int day = 11; day <= 16; day++)
        {
            came += NewsstandRules.Round(s, day, true, pay, results);
        }
        Check(came == 6 && charged == 0, "the week paid for brings its seven editions and no charge (" + came + " more, " + charged + " charges)");
        Check(NewsstandRules.NextPayment(x) == 17, "the next week is paid for on day 17");

        // Day 17, the morning the week renews, with the connection down all day: nothing comes and nothing is charged.
        Check(NewsstandRules.Round(s, 17, false, pay, results) == 0 && results[T] == OnlineMorning.Waiting && charged == 0, "no connection on the morning it renews: no edition and no charge");
        Check(NewsstandRules.Round(s, 17, false, pay, results) == 0 && charged == 0 && x.PaidUntil == 16, "looked at again with no connection: still nothing, and nothing given back that was not paid for");
        // Back on day 18: billed then, not for the lost day.
        Check(NewsstandRules.Round(s, 18, true, pay, results) == 1 && results[T] == OnlineMorning.Billed && charged == 1 && x.PaidUntil == 24 && x.Weeks == 2,
            "the connection back the next day: the week is paid for then, to day 24");
        NewsstandRules.Round(s, 19, true, pay, results);
        // A paid-for day lost to an outage is given back.
        Check(NewsstandRules.Round(s, 20, false, pay, results) == 0 && results[T] == OnlineMorning.Waiting, "an outage on day 20: the edition waits");
        Check(NewsstandRules.Round(s, 20, true, pay, results) == 1 && NewsstandRules.Has(s, T, 20) && x.PaidUntil == 24 && charged == 1,
            "back the same day: today's edition comes, and the day is not lost");
        NewsstandRules.Round(s, 21, false, pay, results);
        NewsstandRules.Round(s, 22, false, pay, results);
        Check(NewsstandRules.Round(s, 23, true, pay, results) == 1 && x.PaidUntil == 26 && charged == 1, "two paid days lost to a cut connection are given back: paid until day 26 (" + x.PaidUntil + ")");
        Check(!NewsstandRules.Has(s, T, 21) && !NewsstandRules.Has(s, T, 22), "the days without a connection have no edition");

        // Cancelled: the editions come until the last day paid for, then it ends without a charge.
        Check(NewsstandRules.Cancel(s, T) && x.Cancelled && !NewsstandRules.Cancel(s, T) && NewsstandRules.NextPayment(x) == 0, "a subscription is cancelled once, and has no next payment");
        Check(NewsstandRules.Subscribe(s, T, NetState.Online, out bool payNow) == OnlineRefusal.None && !payNow && !x.Cancelled, "a cancelled one is taken up again with nothing to pay");
        NewsstandRules.Cancel(s, T);
        for (int day = 24; day <= 26; day++)
        {
            NewsstandRules.Round(s, day, true, pay, results);
        }
        Check(NewsstandRules.Has(s, T, 26) && charged == 1, "after cancelling the editions come until the last day paid for");
        Check(NewsstandRules.Round(s, 27, true, pay, results) == 0 && results[T] == OnlineMorning.Ended && !x.Subscribed && charged == 1, "then it ends, and nothing more is charged");

        // A week that cannot be paid stops the subscription.
        var t = new NewsstandState();
        NewsstandRules.Start(t, T, 40);
        for (int day = 40; day <= 46; day++)
        {
            NewsstandRules.Round(t, day, true, broke, results);
        }
        Check(NewsstandRules.Round(t, 47, true, broke, results) == 0 && results[T] == OnlineMorning.Lapsed && !t.Subs[T].Subscribed, "a week that cannot be paid stops it");
        Check(!NewsstandRules.Due(t, 48), "a stopped subscription has nothing to settle");
    }

    private static void NewsstandPass()
    {
        var s = new NewsstandState();
        var results = new OnlineMorning[NewsstandCatalogue.Subscriptions];
        int charged = 0;
        Func<int, bool> pay = sub => { charged++; return true; };
        NewsstandRules.Start(s, NewsstandCatalogue.Street, 5);
        Check(NewsstandRules.Start(s, NewsstandCatalogue.Pass, 7) == 1 && s.Subs[NewsstandCatalogue.Street].Cancelled, "the pass cancels a title's own subscription, which runs to its last day paid for");
        for (int t = 0; t < NewsstandCatalogue.Titles; t++)
        {
            Check(NewsstandRules.Covered(s, t), "the pass covers " + NewsstandCatalogue.NameKeys[t]);
        }
        Check(NewsstandRules.Subscribe(s, NewsstandCatalogue.Review, NetState.Online, out _) == OnlineRefusal.InPass, "a title the pass covers is not taken out on its own");
        Check(NewsstandRules.Round(s, 7, true, pay, results) == NewsstandCatalogue.Titles && charged == 0, "with the pass every title's edition comes");
        Check(NewsstandRules.Weekly(s, PriceRules.Start) == NewsstandCatalogue.WeekAtStart[NewsstandCatalogue.Pass], "what renews each week is the pass alone (the cancelled title is not counted)");
        Check(NewsstandRules.Running(s) == 2, "two subscriptions run: the pass and the title's until its last day");
    }

    private static void NewsstandKept()
    {
        var s = new NewsstandState();
        for (int day = 1; day <= 9; day++)
        {
            var old = new LedgerEdition { Day = day };
            for (int c = 0; c < 60; c++)
            {
                old.IndexCloses.Add(1000 + c);
            }
            NewsstandRules.Keep(s, old, day + 1);
        }
        Check(s.Earlier.Count == NewsstandCatalogue.KeepDays - 1, "the editions of the days kept before today are kept (" + s.Earlier.Count + ")");
        Check(s.Earlier[0].Day == 7 && s.Earlier[s.Earlier.Count - 1].Day == 9, "the newest are kept, oldest first");
        Check(s.Earlier[0].IndexCloses.Count == NewsstandCatalogue.CloseDays, "an earlier edition keeps the index's last closes only");
        NewsstandRules.Keep(s, new LedgerEdition { Day = 9 }, 10);
        Check(s.Earlier.Count == NewsstandCatalogue.KeepDays - 1, "a day written again replaces its own");
        var latest = new LedgerEdition { Day = 10 };
        Check(ReferenceEquals(NewsstandRules.Find(s, latest, 10), latest) && NewsstandRules.Find(s, latest, 8) != null && NewsstandRules.Find(s, latest, 6) == null,
            "today's edition is the paper's own, the days kept are found, older ones are gone");
        s.Loaded[NewsstandCatalogue.Ledger].AddRange(new[] { 8, 10 });
        var days = new List<int>();
        NewsstandRules.Days(s, NewsstandCatalogue.Ledger, 10, days);
        Check(days.Count == 2 && days[0] == 10 && days[1] == 8, "the days a title can be read on, newest first");
        NewsstandRules.Days(s, NewsstandCatalogue.Ledger, 20, days);
        Check(days.Count == 0, "past the days kept nothing is left to read");
    }

    private static void NewsstandTitles()
    {
        var e = new LedgerEdition { Day = 3 };
        e.Pieces.Add(new LedgerPiece(LedgerSection.City, 90, "reallife_news_accident_severe_1", null, null));
        e.Pieces.Add(new LedgerPiece(LedgerSection.Markets, 92, "reallife_mkt_news_crash", null, null));
        e.Pieces.Add(new LedgerPiece(LedgerSection.Economy, 50, "reallife_macro_news_rate_hold", null, null));
        e.Pieces.Add(new LedgerPiece(LedgerSection.AroundTown, 18, "reallife_event_today_parade", null, null));
        e.Pieces.Add(new LedgerPiece(LedgerSection.People, 20, "reallife_news_person_birthday_1", null, null));
        e.Pieces.Add(new LedgerPiece(LedgerSection.Weather, 99, "reallife_news_dry_1", null, null));
        Check(NewsstandRules.Lead(e, NewsstandCatalogue.Ledger).Key == "reallife_mkt_news_crash", "the City Ledger online leads with the heaviest of all its news");
        Check(NewsstandRules.Lead(e, NewsstandCatalogue.Exchange).Section == LedgerSection.Markets, "the Exchange Gazette leads with money");
        Check(NewsstandRules.Lead(e, NewsstandCatalogue.Street).Section == LedgerSection.City, "the Street Sheet leads with the street");
        Check(NewsstandRules.Lead(e, NewsstandCatalogue.Review).Section == LedgerSection.People, "the Weekend Review leads with its own pages, never the weather");
        Check(NewsstandRules.Count(e, NewsstandCatalogue.Ledger) == 5 && NewsstandRules.Count(e, NewsstandCatalogue.Exchange) == 2 && NewsstandRules.Count(e, NewsstandCatalogue.Street) == 1
              && NewsstandRules.Count(e, NewsstandCatalogue.Review) == 2, "each title prints its own sections");
        bool weather = true;
        for (int t = 0; t < NewsstandCatalogue.Titles; t++)
        {
            weather &= NewsstandRules.Prints(t, LedgerSection.Weather);
        }
        Check(weather, "every title has the weather");
        Check(!NewsstandRules.Prints(NewsstandCatalogue.Street, LedgerSection.Markets) && NewsstandRules.Prints(NewsstandCatalogue.Exchange, LedgerSection.Markets), "the markets are the Exchange Gazette's, not the Street Sheet's");
        Check(NewsstandRules.Lead(new LedgerEdition { Day = 1 }, NewsstandCatalogue.Street) == null, "a title with nothing to print has no lead");
    }

    private static void NewsstandStoreChecks()
    {
        var s = new NewsstandState { NotedDay = 12, WaitingDay = 11 };
        NewsstandRules.Start(s, NewsstandCatalogue.Exchange, 9);
        NewsstandRules.Start(s, NewsstandCatalogue.Pass, 10);
        s.Loaded[NewsstandCatalogue.Exchange].AddRange(new[] { 10, 11, 12 });
        s.Loaded[NewsstandCatalogue.Review].Add(12);
        var e = new LedgerEdition { Day = 11, HasMarket = true, MarketDay = 10, IndexLevel = 123456, IndexChangeBp = -45, RateBp = 425, CycleKey = "reallife_x" };
        e.Pieces.Add(new LedgerPiece(LedgerSection.City, 70, "reallife_news_breakin_1", null, new[] { "business", "Joe's | Bar" }));
        e.Sectors.Add(new LedgerSector { Key = "reallife_sec", Level = 1000, ChangeBp = 12 });
        e.Quotes.Add(new LedgerQuote { Name = "Acme", Ticker = "ACM", Sector = 0, Close = 1234, ChangeBp = 50, Volume = 1000 });
        e.IndexCloses.AddRange(new[] { 1, 2, 3 });
        s.Earlier.Add(e);
        s.Earlier.Add(new LedgerEdition { Day = 10 });
        string text = NewsstandStore.Write(s);
        NewsstandState back = NewsstandStore.Read(text);
        Check(NewsstandStore.Write(back) == NewsstandStore.Write(NewsstandStore.Read(NewsstandStore.Write(back))), "the save's line reads back to itself");
        Check(back.NotedDay == 12 && back.WaitingDay == 11, "the mornings told are kept");
        OnlineSub x = back.Subs[NewsstandCatalogue.Exchange];
        Check(x.Subscribed && x.Cancelled && x.PaidUntil == 15 && x.Since == 9 && x.Weeks == 1 && x.LastDay == 8, "a cancelled subscription is kept as it was");
        Check(back.Subs[NewsstandCatalogue.Pass].Subscribed && !back.Subs[NewsstandCatalogue.Pass].Cancelled && back.Subs[NewsstandCatalogue.Pass].PaidUntil == 16, "the pass is kept");
        Check(back.Loaded[NewsstandCatalogue.Exchange].Count == 3 && back.Loaded[NewsstandCatalogue.Review][0] == 12 && back.Loaded[NewsstandCatalogue.Street].Count == 0, "the mornings each title came are kept");
        Check(back.Earlier.Count == 2 && back.Earlier[0].Day == 10 && back.Earlier[1].Day == 11, "the earlier editions are kept, oldest first");
        LedgerEdition r = back.Earlier[1];
        Check(r.HasMarket && r.IndexLevel == 123456 && r.Pieces.Count == 1 && r.Pieces[0].Value("business") == "Joe's / Bar" && r.Quotes.Count == 1 && r.Sectors.Count == 1 && r.IndexCloses.Count == 3,
            "an earlier edition reads back whole (a name's bar made safe)");
        Check(NewsstandStore.Read("LN9|1|2\nU|0|1|0|9|1|1|1").Subs[0].Subscribed == false, "another version's line is not read: a new state");
        Check(NewsstandStore.Read(null).Earlier.Count == 0 && NewsstandStore.Read(string.Empty).Subs.Length == NewsstandCatalogue.Subscriptions, "no line: a new state");
        NewsstandState damaged = NewsstandStore.Read("LN1|x|3\nU|9|1|0|5|1|1|1\nU|1|1|0|zz|1|1|1\nL|2|5,x,6\nE|bad\nP|0|5|reallife_x||\nQ|A|B|0|5|0|1|0\nE|7|0|0|0|0|0|-1|0|\nZ|what");
        Check(damaged.NotedDay == 0 && damaged.WaitingDay == 3, "a damaged head number is read as none");
        Check(damaged.Subs[1].Subscribed && damaged.Subs[1].PaidUntil == 0, "a damaged subscription line keeps what it can");
        Check(damaged.Loaded[2].Count == 2, "a damaged day in a list is left out");
        Check(damaged.Earlier.Count == 1 && damaged.Earlier[0].Day == 7 && damaged.Earlier[0].Pieces.Count == 0, "lines before a good edition's head are left out, and the edition is read");
        var keys = new Dictionary<string, string>();
        NewsstandStore.Save(keys, s);
        Check(keys.ContainsKey("reallife:ledger_online") && NewsstandStore.Load(keys).Subs[NewsstandCatalogue.Pass].Subscribed && NewsstandStore.Load(new Dictionary<string, string>()).Earlier.Count == 0,
            "kept under reallife:ledger_online, beside the City Ledger's own entry");
    }
}
