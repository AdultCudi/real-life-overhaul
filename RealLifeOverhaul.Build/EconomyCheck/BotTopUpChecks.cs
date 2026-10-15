using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Markets;

// Capital added to a running trading bot, or taken out of its money not in shares (Scripts/Markets: MarketBots.Add,
// MostOut, TakeOut, FeeWorth; MarketEngine.AddCapital, TakeCapital; MarketStore): no money moves and nothing is sold,
// the bot's money and the player's books agree to the unit; no fee for the days before the money came and no profit
// share on it; the most it may lose reckoned on the capital given, so a top-up neither stops it nor hides a loss; the
// money put into what it holds, up to its size each, at the next session's first step; every refusal; the same on two
// machines and nothing new sent from a guest; the save's line with the new fields and without them, and its history
// lines read by a version before them; every new text in the three locales.
internal static partial class Program
{
    private static void BotTopUp()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        TopUpMoney();
        TopUpFee();
        TopUpLossLimit();
        TopUpPutToWork();
        TopUpTakeOut();
        TopUpRefusals();
        TopUpSameEverywhere();
        TopUpStore();
        TopUpWords();
        Console.WriteLine("  trading bots' capital added and taken out: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // A bot whose month has run a few closes (holding something, if asked), in the session of a market day after its
    // first step, the next market day not a month's turn.
    private static Bot TopUpBot(MarketEngine m, int firm, long capital, int size, int loss, bool holding = true)
    {
        SessionDay(m, 3, 10);
        Bot b = m.Hire(firm, capital, size, 0, loss, out _);
        int day = m.State.Day;
        for (int n = 0; n < 120 && (b.MonthDays < 3 || (holding && b.Holdings.Count == 0) || MarketBots.MonthTurns(MarketTrading.NextTradingDay(m.State.Day), 60)); n++)
        {
            day = MarketTrading.NextTradingDay(day);
            RunTo(m, day, MarketCatalogue.PhaseOpen + 10);
        }
        return b;
    }

    // ---------------------------------------------------------------- the money

    private static void TopUpMoney()
    {
        MarketEngine m = BotMarket(701, 5000000, out BotWorld w);
        Bot b = TopUpBot(m, 1, 600000, 20, 35);
        long money = w.Purse.Money, botCash = b.Cash, worth = MarketBots.Worth(m.State, b, m.State.Phase), capital = b.Capital, keys = w.Purse.By.Count;
        long moved = w.Purse.By.Values.Sum();
        int trades = m.State.Trades.Count;
        Check(m.AddCapital(1, 250000, out BotWhy why) && why == BotWhy.None, "top-up: a running bot is given more capital (" + why + ")");
        Check(w.Purse.Money == money && w.Purse.By.Count == keys && w.Purse.By.Values.Sum() == moved,
            "top-up: no money moves and no transaction is booked: the bot trades the player's own cash, as at its hire");
        Check(b.Cash == botCash + 250000 && b.Capital == capital + 250000 && MarketBots.Worth(m.State, b, m.State.Phase) == worth + 250000,
            "top-up: its money, its capital and its worth rise by the sum, to the unit");
        Check(b.Holdings.Count > 0 && m.State.Trades.Count == trades + 1 && m.State.Trades.Last().Kind == TradeKind.BotAdded && m.State.Trades.Last().Value == 250000
              && m.State.Trades.Last().Bot == 1, "top-up: nothing is sold, and the history has one line of the bot's capital added");
        // Over the days after, what the bot's money did is what the player's cash did, the capital added left out.
        long money0 = w.Purse.Money, own0 = b.Cash - b.Capital;
        RunTo(m, m.State.Day + 20);
        Check(m.TakeCapital(1, Math.Min(m.BotMostOut(1), m.BotChangeLeast * 2), out why) || why == BotWhy.ChangeTooLittle, "top-up: some is taken out again where it can be (" + why + ")");
        RunTo(m, m.State.Day + 10);
        Check(w.Purse.Money - money0 == (b.Cash - b.Capital) - own0,
            "top-up: what the bot's money did is exactly what the player's cash did, with capital added and taken out (" + (w.Purse.Money - money0) + " against " + ((b.Cash - b.Capital) - own0) + ")");
    }

    // ---------------------------------------------------------------- the fee and the profit share

    private static void TopUpFee()
    {
        // Two machines with the same market and the same bot; one gives it more in the middle of the month and both
        // close the month at once: the money added was there for none of its days, so the fee and the share are the same.
        MarketEngine a = BotMarket(711, 5000000, out BotWorld wa), c = BotMarket(711, 5000000, out BotWorld wc);
        Bot ba = TopUpBot(a, 4, 800000, 10, 90, false), bc = TopUpBot(c, 4, 800000, 10, 90, false);
        Check(ba.MonthDays == bc.MonthDays && ba.MonthDays >= 3 && MarketBots.Worth(a.State, ba, a.State.Phase) == MarketBots.Worth(c.State, bc, c.State.Phase),
            "top-up fee: the two machines' bots stand the same before the top-up (" + ba.MonthDays + " days into its month)");
        a.AddCapital(4, 1600000, out _);
        Check(ba.MonthAdded == 1600000L * ba.MonthDays && MarketBots.FeeWorth(ba, 1000000, ba.MonthDays) == 1000000 - 1600000,
            "top-up fee: the money is counted for the days of the month that went before it, so its fee is on the worth without it");
        a.BotMonthNow();
        c.BotMonthNow();
        Check(ba.Fees == bc.Fees && wa.Purse.Of(MarketMoney.BotFee) == wc.Purse.Of(MarketMoney.BotFee) && wa.Purse.Of(MarketMoney.BotShare) == wc.Purse.Of(MarketMoney.BotShare),
            "top-up fee: money added today pays no fee for the days before it and no share of profit (" + ba.Fees + " against " + bc.Fees + ")");
        Check(ba.MonthAdded == 0 && bc.MonthAdded == 0, "top-up fee: a new month counts the money for all its days");
        // A whole month with the money: its fee is a share of the larger worth.
        BotFirm f = ba.Def;
        long least = a.BotChangeLeast * MarketBots.FeeLeastAtStart / MarketBots.ChangeLeastAtStart;
        Check(MarketBots.Fee(f, MarketBots.FeeWorth(ba, 2400000, 10), (int)least) == MarketBots.Fee(f, 2400000, (int)least) && MarketBots.Fee(f, 2400000, (int)least) > MarketBots.Fee(f, 800000, (int)least),
            "top-up fee: with the money there all month the fee is on the whole worth, more than without it");
        // Taken out: the money counts for the days it was there.
        var t = new Bot { Firm = 4, MonthDays = 5, Capital = 900000, Base = 900000, Cash = 900000, Loss = 35 };
        MarketBots.TakeOut(t, 100000);
        Check(t.MonthAdded == -500000 && MarketBots.FeeWorth(t, 800000, 10) == 850000, "top-up fee: money taken out pays its fee for the half of the month it was there");
        // The share: none on money added, whatever the high-water mark.
        Bot s = MarketBots.Hired(a.State, 4);
        s.HighWater = MarketBots.Worth(a.State, s, MarketCatalogue.PhaseClose);
        long share = wa.Purse.Of(MarketMoney.BotShare);
        a.AddCapital(4, 500000, out _);
        a.BotMonthNow();
        Check(wa.Purse.Of(MarketMoney.BotShare) == share && s.HighWater >= MarketBots.Worth(a.State, s, MarketCatalogue.PhaseClose) - 1,
            "top-up fee: its high-water mark rises by the money added, so no share of profit is taken on it");
    }

    // ---------------------------------------------------------------- the most it may lose

    private static void TopUpLossLimit()
    {
        MarketEngine m = BotMarket(721, 5000000, out _);
        Bot b = TopUpBot(m, 3, 500000, 50, 35);
        long worth = MarketBots.Worth(m.State, b, m.State.Phase), floor = MarketBots.Floor(b), lost = b.Base - worth;
        m.AddCapital(3, 400000, out _);
        long worth2 = MarketBots.Worth(m.State, b, m.State.Phase), floor2 = MarketBots.Floor(b);
        Check(floor2 == b.Base * (100 - b.Loss) / 100 && b.Base - worth2 == lost, "top-up limit: the loss so far stays the same in money, and the floor is reckoned on the capital given");
        Check(Math.Abs((worth2 - floor2) - (worth - floor) - 400000L * b.Loss / 100) <= 1, "top-up limit: the room to the floor grows by the limit's share of the money added, no more");
        RunTo(m, m.State.Day, m.State.Phase + 2);
        Check(b.Status == BotStatus.Running, "top-up limit: a top-up never takes a running bot to its floor");
        // A loss after the top-up stops it at the limit's share of the larger capital.
        var x = new Bot { Firm = 3, Capital = 1000000, Base = 1000000, Cash = 1000000, Loss = 20, Peak = 1000000, HighWater = 1000000 };
        MarketBots.Add(x, 1000000);
        Check(MarketBots.Floor(x) == 1600000 && x.Peak == 2000000 && x.HighWater == 2000000 && x.MonthWorth == 1000000,
            "top-up limit: on 2,000,000 given at a limit of 20% it stops under 1,600,000; its peak and mark rise with the money");
    }

    // ---------------------------------------------------------------- the money put to work

    private static void TopUpPutToWork()
    {
        MarketEngine m = BotMarket(731, 8000000, out _);
        Bot b = TopUpBot(m, 3, 600000, 50, 50);   // momentum: new holdings only at the month's turn
        var held = b.Holdings.ToDictionary(h => h.StockId, h => h.Shares);
        long cash = b.Cash;
        m.AddCapital(3, 900000, out _);
        Check(b.TopUp == MarketBots.TopUpAdded, "top-up work: capital added waits for the bot's next decision");
        int day = m.State.Day;
        RunTo(m, day);
        Check(b.TopUp == MarketBots.TopUpDecided && b.Pending.Any(o => o.Buy && held.ContainsKey(o.StockId)), "top-up work: at the close it decides to buy more of what it holds");
        RunTo(m, MarketTrading.NextTradingDay(day), MarketCatalogue.PhaseOpen + 3);
        long worth = MarketBots.Worth(m.State, b, m.State.Phase);
        bool grew = held.Keys.Any(id => b.Holding(id) != null && b.Holding(id).Shares > held[id]);
        bool within = b.Holdings.All(h => !held.ContainsKey(h.StockId) || h.Shares <= held[h.StockId]
                                          || MarketTrading.Value(h.Shares, MarketTrading.QuoteOf(m.State, m.State.Stock(h.StockId), m.State.Phase).Bid, false) <= worth * b.Size / 100 * 105 / 100);
        Check(grew && b.Cash < cash + 900000 && b.TopUp == 0, "top-up work: at the next session's first step it buys more of what it holds, and trades by its rules alone after");
        Check(within, "top-up work: no holding bought up goes over its size, a share of the larger worth");
        // Without a top-up nothing more of a holding is bought.
        int day2 = m.State.Day;
        var held2 = b.Holdings.ToDictionary(h => h.StockId, h => h.Shares);
        RunTo(m, MarketTrading.NextTradingDay(day2), MarketCatalogue.PhaseOpen + 3);
        Check(b.Holdings.All(h => !held2.ContainsKey(h.StockId) || h.Shares <= held2[h.StockId]), "top-up work: without capital added it never buys more of what it holds");
    }

    // ---------------------------------------------------------------- taken out

    private static void TopUpTakeOut()
    {
        MarketEngine m = BotMarket(741, 5000000, out BotWorld w);
        Bot b = TopUpBot(m, 1, 900000, 20, 35);
        m.AddCapital(1, 300000, out _);   // (money not in shares to take out)
        long most = m.BotMostOut(1), shares = b.Holdings.Sum(h => h.Shares), money = w.Purse.Money;
        Check(most > 0 && most <= b.Cash - b.PayOwed && most <= b.Capital - m.BotCapitalLeast, "top-up out: no more than its money not in shares, less what it owes, and leaving it its least (" + most + ")");
        Check(!m.TakeCapital(1, most + 1, out BotWhy why) && why == BotWhy.TooMuchOut, "top-up out: more than that is refused");
        Check(m.TakeCapital(1, most, out why) && b.Holdings.Sum(h => h.Shares) == shares && w.Purse.Money == money && MarketBots.Worth(m.State, b, m.State.Phase) > MarketBots.Floor(b),
            "top-up out: the most is taken out with nothing sold, no money moved, and it stays over its floor (" + why + ")");
        Check(m.State.Trades.Last().Kind == TradeKind.BotTaken && m.State.Trades.Last().Value == -most, "top-up out: the history has a line of the capital taken out");
        // The floor: a bot near it may take out only what keeps it over.
        var x = new Bot { Firm = 1, Status = BotStatus.Running, Capital = 1000000, Base = 1000000, Cash = 700000, Loss = 35, Peak = 1000000 };
        long xm = MarketBots.MostOut(m.State, x, 2500, m.State.Phase);   // worth 700,000, floor 650,000: 50,000 of room is 142,857 at 35%
        Check(xm > 140000 && 700000 - xm > (x.Base - xm) * 65 / 100 && 700000 - (xm + 1) <= (x.Base - xm - 1) * 65 / 100, "top-up out: near its floor only what keeps it over, to the unit (" + xm + ")");
        x.Status = BotStatus.Stopped;
        Check(MarketBots.MostOut(m.State, x, 2500, m.State.Phase) == 0, "top-up out: nothing from a bot that is not running");
    }

    // ---------------------------------------------------------------- refusals

    private static void TopUpRefusals()
    {
        MarketEngine m = BotMarket(751, 3000000, out BotWorld w);
        Bot b = TopUpBot(m, 2, 500000, 20, 35, false);
        long least = m.BotChangeLeast;
        Check(least == m.BotCapitalLeast * MarketBots.ChangeLeastAtStart / MarketBots.CapitalLeastAtStart, "top-up refusals: the least at a time follows prices as a bot's least capital does (" + least + ")");
        Check(!m.AddCapital(1, least, out BotWhy why) && why == BotWhy.NotHired, "top-up refusals: a firm not hired");
        Check(!m.AddCapital(2, least - 1, out why) && why == BotWhy.ChangeTooLittle && !m.TakeCapital(2, least - 1, out why) && why == BotWhy.ChangeTooLittle,
            "top-up refusals: less than the least at a time, in or out");
        Check(!m.AddCapital(2, m.Wallet.Cash + 1, out why) && why == BotWhy.NoCash, "top-up refusals: more than the player's cash");
        b.Status = BotStatus.SellingUp;
        Check(!m.AddCapital(2, least, out why) && why == BotWhy.NotRunning && !m.TakeCapital(2, least, out why) && why == BotWhy.NotRunning, "top-up refusals: a bot selling up");
        b.Status = BotStatus.Stopped;
        Check(!m.AddCapital(2, least, out why) && why == BotWhy.NotRunning, "top-up refusals: a stopped bot");
        b.Status = BotStatus.Running;
        long capital = b.Capital;
        m.HoldAfterLoad();
        m.Tape.Reach(m.State.Day + 3, MarketCatalogue.PhaseOpen, true);
        m.HoldAfterLoad();
        Check(m.Held && !m.AddCapital(2, least, out why) && why == BotWhy.Held && !m.TakeCapital(2, least, out why) && why == BotWhy.Held && b.Capital == capital,
            "top-up refusals: nothing while the market replays after a load");
        _ = w;
    }

    // ---------------------------------------------------------------- the same everywhere

    private static void TopUpSameEverywhere()
    {
        MarketEngine a = BotMarket(761, 5000000, out _), c = BotMarket(761, 5000000, out _);
        Bot ba = TopUpBot(a, 1, 500000, 20, 35), bc = TopUpBot(c, 1, 500000, 20, 35);
        a.AddCapital(1, 300000, out _);
        c.AddCapital(1, 300000, out _);
        RunTo(a, a.State.Day + 15);
        RunTo(c, c.State.Day + 15);
        Check(MarketBots.Worth(a.State, ba, a.State.Phase) == MarketBots.Worth(c.State, bc, c.State.Phase) && ba.Trades == bc.Trades && a.State.Stocks.All(s => c.State.Stock(s.Id).Last == s.Last),
            "top-up same: the same market and the same top-up come to the same trades, prices and worth (" + ba.Trades + " trades)");
        // A guest sends nothing new: each trade's move, as before.
        MarketEngine g = BotMarket(762, 5000000, out _);
        int sent = 0;
        g.TradeSink = (stock, micros, shares) => sent++;
        Bot bg = TopUpBot(g, 1, 500000, 20, 35);
        int before = sent;
        g.AddCapital(1, 300000, out _);
        Check(sent == before, "top-up same: capital added sends nothing");
        RunTo(g, g.State.Day + 10);
        Check(sent == bg.Trades, "top-up same: a guest's bot sends each trade's move to the host as before (" + sent + " for " + bg.Trades + ")");
    }

    // ---------------------------------------------------------------- the save

    private static void TopUpStore()
    {
        MarketEngine m = BotMarket(771, 5000000, out _);
        Bot b = TopUpBot(m, 1, 600000, 20, 35);
        m.AddCapital(1, 200000, out _);
        m.TakeCapital(1, m.BotChangeLeast, out _);
        string line = MarketStore.Write(m.State);
        MarketState back = MarketStore.Read(line, out _);
        Bot y = back != null ? MarketBots.Hired(back, 1) : null;
        Check(back != null && MarketStore.Write(back) == line, "top-up save: the line reads back and writes again the same" + FirstDifference(line, back != null ? MarketStore.Write(back) : null));
        Check(y != null && y.TopUp == b.TopUp && y.TopUp == MarketBots.TopUpAdded && y.MonthAdded == b.MonthAdded && b.MonthAdded != 0 && y.Capital == b.Capital && y.Cash == b.Cash,
            "top-up save: the bot comes back with its capital, its money, the top-up waiting and the fee's money-days");
        Check(back != null && back.Trades.Count(t => t.Kind == TradeKind.BotAdded) == 1 && back.Trades.Count(t => t.Kind == TradeKind.BotTaken) == 1,
            "top-up save: and the history's lines of capital added and taken out");
        // The bot's record ends with the two new fields; a record without them (an older save) loads as before.
        string part = Part(line, 'b');
        string[] records = part.Split(';');
        Check(records.All(r => r.Split('|').Length == 44), "top-up save: the new fields are the last two of the bot's record");
        string older = line.Replace(part, string.Join(";", records.Select(r => string.Join("|", r.Split('|').Take(42)))));
        MarketState old = MarketStore.Read(older, out _);
        Bot o = old != null ? MarketBots.Hired(old, 1) : null;
        Check(o != null && o.TopUp == 0 && o.MonthAdded == 0 && o.Capital == b.Capital && o.Cash == b.Cash && o.Holdings.Count == b.Holdings.Count,
            "top-up save: a save without the new fields loads the bot as before, with nothing waiting");
        // An older version reads the history's lines as a payment in and a withdrawal of the bot's (never a shortfall).
        string trades = Part(line, 't');
        string[] added = trades.Split(';').Where(r => r.Split('|').Length > 12 && r.Split('|')[12] == "1").ToArray();
        Check(added.Length == 2 && added.All(r => (r.Split('|')[3] == "7" || r.Split('|')[3] == "8") && r.Split('|')[9] == "1"),
            "top-up save: its lines are written as a payment in and a withdrawal, marked with the bot, for a version before them");
        string plain = line.Replace(trades, string.Join(";", trades.Split(';').Select(r => r.Split('|').Length > 12 ? string.Join("|", r.Split('|').Take(12)) : r)));
        MarketState before = MarketStore.Read(plain, out _);
        Check(before != null && before.Trades.Count(t => t.Kind == TradeKind.Deposit && t.Bot == 1) == 1 && before.Trades.Count(t => t.Kind == TradeKind.Withdrawal && t.Bot == 1) == 1,
            "top-up save: without the mark they read as those, the bot still named");
    }

    // A part of the save's line, its letter and colon left off.
    private static string Part(string line, char letter)
    {
        int at = line.IndexOf("~" + letter + ":", StringComparison.Ordinal);
        if (at < 0)
        {
            return string.Empty;
        }
        int end = line.IndexOf('~', at + 3);
        return end < 0 ? line.Substring(at + 3) : line.Substring(at + 3, end - at - 3);
    }

    // ---------------------------------------------------------------- the texts

    private static readonly string[] TopUpKeys =
    {
        "reallife_mkt_bot_box_capital", "reallife_mkt_bot_change", "reallife_mkt_bot_change_placeholder", "reallife_mkt_bot_add", "reallife_mkt_bot_take",
        "reallife_mkt_bot_confirm_add", "reallife_mkt_bot_confirm_add_body", "reallife_mkt_bot_confirm_take", "reallife_mkt_bot_confirm_take_body",
        "reallife_mkt_bot_note_added", "reallife_mkt_bot_note_taken", "reallife_mkt_bot_why_not_running", "reallife_mkt_bot_why_change_too_little",
        "reallife_mkt_bot_why_too_much_out", "reallife_mkt_kind_bot_added", "reallife_mkt_kind_bot_taken"
    };

    private static void TopUpWords()
    {
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul"));
        string folder = System.IO.Path.Combine(root, "Locales");
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(folder, "en.json"));
        Dictionary<string, string> pt = LoadWords(System.IO.Path.Combine(folder, "pt.json"));
        Dictionary<string, string> zh = LoadWords(System.IO.Path.Combine(folder, "zh-cn.json"));
        Check(en != null && pt != null && zh != null, "top-up words: the three locales are where the checks look for them (" + folder + ")");
        if (en == null || pt == null || zh == null)
        {
            return;
        }
        var holes = new Regex(@"\{[a-z_]+\}");
        var missing = new List<string>();
        foreach (string key in TopUpKeys)
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
        Check(missing.Count == 0, "top-up words: every text is in en.json, pt.json and zh-cn.json with the same {names} (" + TopUpKeys.Length + " keys" + (missing.Count > 0 ? "; " + string.Join(", ", missing) : string.Empty) + ")");
        Check(en["reallife_mkt_bot_note_added"].Length + 60 <= 240 && en["reallife_mkt_bot_note_taken"].Length + 60 <= 240, "top-up words: each notification fits the game's toast with its names filled (240 characters)");
        // Every refusal has its sentence and every line of the history its word, in the order of their numbers.
        string bots = System.IO.File.ReadAllText(System.IO.Path.Combine(root, @"Scripts\Markets\MarketApp.Bots.cs"));
        string box = System.IO.File.ReadAllText(System.IO.Path.Combine(root, @"Scripts\Markets\MarketApp.Box.cs"));
        int why = Regex.Matches(Between(bots, "BotWhyKeys =", "};"), "\"reallife_mkt_bot_why_").Count;
        int kinds = Regex.Matches(Between(box, "KindKeys =", "};"), "\"reallife_mkt_kind_").Count;
        Check(why == (int)BotWhy.Count && Between(bots, "BotWhyKeys =", "};").Contains("\"reallife_mkt_bot_why_too_much_out\""),
            "top-up words: every reason a bot refuses has its sentence (" + why + " of " + (int)BotWhy.Count + ")");
        Check(kinds == (int)TradeKind.BotTaken + 1 && Between(box, "KindKeys =", "};").TrimEnd().EndsWith("\"reallife_mkt_kind_bot_taken\"", StringComparison.Ordinal),
            "top-up words: every kind of history line has its word, the bot's capital last (" + kinds + ")");
        // The Guide's bots page says it, in each language, and What's new has the line (in the next row, or its version's row once it is made).
        Check(en["reallife_guide_markets_bots"].Contains("Add capital") && pt["reallife_guide_markets_bots"].Contains("Pôr capital") && zh["reallife_guide_markets_bots"].Contains("追加资本"),
            "top-up words: the Guide's bots page says how capital is added and taken out, in each language");
        bool line = false;
        foreach (KeyValuePair<string, string> row in en)
        {
            if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && row.Value.Contains("A running trading bot can be given more capital"))
            {
                line = !row.Value.StartsWith("New: ", StringComparison.Ordinal);
                break;
            }
        }
        Check(line, "top-up words: What's new has the feature's line, with no prefix");
    }

    private static string Between(string text, string from, string to)
    {
        int at = text.IndexOf(from, StringComparison.Ordinal);
        int end = at < 0 ? -1 : text.IndexOf(to, at, StringComparison.Ordinal);
        return at < 0 || end < 0 ? string.Empty : text.Substring(at, end - at);
    }
}
