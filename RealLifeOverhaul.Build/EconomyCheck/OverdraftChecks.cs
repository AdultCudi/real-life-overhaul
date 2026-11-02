using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

// The overdraft and the books around it, from a player's report (cash under nothing, 150,000 short, an 80,000 overdraft never drawn):
//   1. the overdraft covered only what the night's estimate said was due at midnight, once, at the first hour of the day; cash left under nothing by
//      anything else (the game forces its rent, wages, bonuses, boats and fees past what is held, and the estimate misses bills) stayed under nothing.
//      It now pays at the money change that leaves the cash under nothing, and every game hour as a net, up to its limit, told once;
//   2. the game's import payment is no hole in the books (it is booked with no business address, so the game's own statement never holds it): the Cash
//      flow tab files it as stock and the day's result counts it;
//   3. the own bank's profit is a month's figure; its page now also says what it came to a day.
internal static partial class Program
{
    private static void ShortCash()
    {
        ShortCashRule();
        ShortCashWiring();
        ShortCashImport();
        ShortCashBankDay();
        ShortCashTexts();
    }

    private static CreditAccount ShortCashOf(int limit, int owed = 0)
    {
        CreditAccount a = AccountRules.Open(ProductKind.Overdraft, limit, 900, 10);
        a.Owed = owed;
        return a;
    }

    // 1: the rule. Cash under nothing is paid back up to nothing, as far as the limit allows, whatever the amount.
    private static void ShortCashRule()
    {
        CreditAccount big = ShortCashOf(80000);
        int paid = AccountRules.CoverCash(big, -150000);
        Check(paid == 80000 && big.Owed == 80000 && big.Free == 0, "overdraft: 150,000 under nothing with an 80,000 limit draws all 80,000 (" + paid + ")");
        Check(AccountRules.CoverCash(big, -70000) == 0 && big.Owed == 80000, "overdraft: a limit used up draws nothing more");
        CreditAccount small = ShortCashOf(80000, 10000);
        Check(AccountRules.CoverCash(small, -25000) == 25000 && small.Owed == 35000, "overdraft: 25,000 under nothing draws 25,000 and no more, on top of what is drawn");
        CreditAccount one = ShortCashOf(80000);
        Check(AccountRules.CoverCash(one, -1) == 1 && one.Owed == 1, "overdraft: one unit under nothing draws one");
        CreditAccount none = ShortCashOf(80000);
        Check(AccountRules.CoverCash(none, 0) == 0 && AccountRules.CoverCash(none, 5000) == 0 && none.Owed == 0, "overdraft: cash at or over nothing draws nothing");
        CreditAccount frozen = ShortCashOf(80000);
        frozen.Frozen = true;
        CreditAccount closed = ShortCashOf(80000);
        closed.Closed = true;
        CreditAccount card = AccountRules.Open(ProductKind.CreditCard, 80000, 900, 10);
        Check(AccountRules.CoverCash(frozen, -5000) == 0 && AccountRules.CoverCash(closed, -5000) == 0 && AccountRules.CoverCash(card, -5000) == 0 && AccountRules.CoverCash(null, -5000) == 0,
            "overdraft: a frozen or closed overdraft, a card and no account draw nothing");
        // The drawn money is interest-bearing like the night's: the next day's charge is on it.
        AccountRules.DaysPerYear = Year;
        CreditAccount drawn = ShortCashOf(80000);
        AccountRules.CoverCash(drawn, -30000);
        DayOutcome day = Day(drawn, 11, new Purse { Money = 0 });
        Check(day.Interest > 0, "overdraft: what was drawn for cash under nothing earns the usual interest the next day (" + day.Interest + ")");
    }

    // 1: the wiring, read from the sources: the money change, the hour and the day's end all reach the cover, and the night's cover and this one share one gate.
    private static void ShortCashWiring()
    {
        string day = BankingSource("EconomyDirector.Day.cs"), director = BankingSource("EconomyDirector.cs"), shortcut = BankingSource(@"Desk\CreditDesk.Short.cs"), desk = BankingSource(@"Desk\CreditDesk.cs");
        string eventHandler = MemberText(day, "private void OnGameEvent(string gameEvent)"), hour = MemberText(day, "private void OnHour()"), newDay = MemberText(day, "private void OnDay()");
        Check(eventHandler.Contains("GameLoans.MoneyChangeEvent") && eventHandler.Contains("Save.Money < 0f") && eventHandler.Contains("DeskDo(\"the overdraft\", _deskShort);"),
            "overdraft: a money change that leaves the cash under nothing reaches the cover at once");
        Check(hour.Contains("DeskDo(\"the overdraft\", _deskHour);") && hour.IndexOf("_desk.BeforeRun(day)", StringComparison.Ordinal) < hour.IndexOf("_deskHour", StringComparison.Ordinal),
            "overdraft: every game hour the cover looks again, after the night's own");
        Check(newDay.Contains("DeskDo(\"the overdraft\", _deskHour);") && newDay.IndexOf("Settle(Save.Day);", StringComparison.Ordinal) < newDay.IndexOf("_deskHour", StringComparison.Ordinal),
            "overdraft: after the game's night run the cover looks again, and tells what it drew");
        Check(director.Contains("_deskShort = _desk.CoverShort;") && director.Contains("_deskHour = _desk.CoverHour;"), "overdraft: the cover's calls are kept delegates, so a money change allocates nothing");
        Check(MemberText(desk, "public void BeforeRun(int day)").Contains("OverdraftToDraw()") && MemberText(shortcut, "public void CoverShort()").Contains("OverdraftToDraw()"),
            "overdraft: the night's cover and the cover for cash under nothing share one gate (an open overdraft not frozen, room left, the option on, the lenders lending)");
        string cover = MemberText(shortcut, "public void CoverShort()");
        Check(cover.Contains("_covering") && cover.Contains("finally") && cover.Contains("AccountRules.CoverCash(overdraft, cash)"), "overdraft: the cover's own payment, a money change too, does not cover itself");
        Check(MemberText(shortcut, "public void TellCover()").Contains("reallife_econ_note_cover_short") && MemberText(shortcut, "public void TellCover()").Contains("_unsaid = 0;"),
            "overdraft: what was drawn is told once, not once for each charge of the night");
        Check(!shortcut.Contains("Update(") && !shortcut.Contains("FindObjects"), "overdraft: the cover costs nothing per frame and searches nothing");
        int events = 0;
        foreach (string path in System.IO.Directory.GetFiles(System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), @"..\Scripts")), "*.cs", System.IO.SearchOption.AllDirectories))
        {
            events += System.IO.File.ReadAllText(path).Contains("\"ba:gameevent_moneychange\"") ? 1 : 0;
        }
        Check(events == 1, "overdraft: the game's money-change event is named in one place only (" + events + ")");
        Check(BankingSource(@"Loans\GameLoans.cs").Contains("MoneyChangeEvent = \"ba:gameevent_moneychange\""), "overdraft: the event is the game's own (GameManager.ChangeMoney)");
    }

    // 2: the import payment is in the mod's books.
    private static void ShortCashImport()
    {
        const string import = "ba:transaction_importdelivery";
        Check(CashCategory.For(import) == CashCategory.Stock && CashCategory.For(import + "refund") == CashCategory.Stock, "import: the Cash flow tab files an import payment and its refund as stock");
        Check(!DayRules.IsTransfer(import) && DayRules.Of(import, false) == DayPart.Counted,
            "import: the day's result counts an import payment (the game's statement holds none, as it is booked at no business)");
        var tally = new ResultTally();
        tally.Add(import, -12000f, false);
        tally.Add("ba:transaction_revenue", 500f, true);
        DayFigure figure = tally.Figure(5, 800f);
        Check(figure.Extra == -12000f && figure.Mod == -11200f && figure.Counted == 1 && figure.Held == 1, "import: an import payment of 12,000 takes the day's result from 800 to -11,200");
        string result = EconomySource("GameBooks.Result.cs");
        Check(MemberText(result, "private static bool Holds(Transaction t)").Contains("if (!here)") && result.Contains("tally.Add(t.transactionType, t.amount, Holds(t));"),
            "import: a payment booked with no business is never held by the game's figure, so it is counted");
    }

    // 3: the bank's profit a day.
    private static void ShortCashBankDay()
    {
        var b = new OwnBank { MonthFrom = 10, LastDay = 19, MonthIncome = 300000, MonthCosts = 100000, LastIncome = 6000000, LastCosts = 5100000 };
        Check(OwnBankRules.DaysRun(b) == 10 && OwnBankRules.ProfitPerDayNow(b) == 20000 && OwnBankRules.ProfitPerDayLast(b, 30) == 30000,
            "bank day: 200,000 over ten days is 20,000 a day, and last month's 900,000 over thirty is 30,000");
        b.LastDay = 9;
        Check(OwnBankRules.DaysRun(b) == 0 && OwnBankRules.ProfitPerDayNow(b) == 0, "bank day: on the first morning of a month, with no day run, there is no figure");
        b.LastDay = 3;
        Check(OwnBankRules.DaysRun(b) == 0, "bank day: a day run before the month began counts no day");
        b.LastDay = 12;
        b.MonthCosts = 500000;
        Check(OwnBankRules.ProfitPerDayNow(b) == -66666 && OwnBankRules.ProfitPerDayLast(b, 0) == 900000, "bank day: a loss is a negative figure, and a month of no days is not divided by");
        string app = System.IO.File.ReadAllText(System.IO.Path.Combine(LocalesFolder(), @"..\Scripts\Economy\OwnBank\OwnBankApp.cs"));
        Check(MemberText(app, "private static void WriteDaily(").Contains("OwnBankRules.ProfitPerDayNow(b)") && app.Contains("WriteDaily(box, b);"), "bank day: the bank's page writes the profit a day beside the month's");
        // Where the rest of a profit goes: kept in the bank's capital, booked as a line of its own.
        string game = System.IO.File.ReadAllText(System.IO.Path.Combine(LocalesFolder(), @"..\Scripts\Economy\OwnBank\OwnBankGame.cs"));
        Check(MemberText(game, "public static void BookDay(").Contains("OwnBankCatalogue.KeptKey") && CashCategory.For(OwnBankCatalogue.KeptKey) == CashCategory.Investments && DayRules.IsTransfer(OwnBankCatalogue.KeptKey),
            "bank day: the profit kept in the bank is a line of its own, an investment in the Cash flow tab and a transfer in the day's result");
    }

    // The texts: the notice and the page's rows in en, pt and zh-cn with their names, and What's new in all eleven languages.
    private static void ShortCashTexts()
    {
        string folder = LocalesFolder();
        var marks = new Dictionary<string, string>
        {
            { "en", "Fixed: your cash could go under nothing" }, { "pt", "Corrigido: seu dinheiro podia ficar negativo" }, { "zh-cn", "已修复：你的现金可能变成负数" },
            { "de", "Behoben: Dein Bargeld konnte ins Minus" }, { "fr", "Corrigé : votre liquidité pouvait passer sous zéro" }, { "es", "Corregido: tu efectivo podía quedar en negativo" },
            { "ru", "Исправлено: ваши наличные могли уйти в минус" }, { "nl", "Opgelost: je geld kon onder nul komen" }, { "tr", "Düzeltildi: ek hesabınız kullanılmadan" },
            { "pl", "Naprawiono: twoja gotówka mogła zejść poniżej zera" }, { "ko", "수정: 당좌 대월이 쓰이지 않은 채" }
        };
        var wrong = new List<string>();
        foreach (KeyValuePair<string, string> language in marks)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(folder, language.Key + ".json"));
            if (words == null || !words.TryGetValue("reallife_changes_next", out string row) || !row.Contains(language.Value))
            {
                wrong.Add(language.Key);
                continue;
            }
            if (language.Key == "en" || language.Key == "pt" || language.Key == "zh-cn")
            {
                string[] names = { "{amount}", "{owed}", "{limit}" };
                bool ok = words.TryGetValue("reallife_econ_note_cover_short", out string note) && Array.TrueForAll(names, n => note.Contains(n)) && note.Length <= 240
                          && words.TryGetValue("reallife_ob_box_perday", out string rowName) && rowName.Length > 0
                          && words.TryGetValue("reallife_ob_box_perday_value", out string both) && both.Contains("{now}") && both.Contains("{last}")
                          && words.TryGetValue("reallife_ob_box_perday_now", out string now) && now.Contains("{now}");
                if (!ok)
                {
                    wrong.Add(language.Key + " notice or rows");
                }
            }
        }
        Check(wrong.Count == 0, "overdraft: the notice, the bank's rows and What's new's two lines are written in every language" + (wrong.Count > 0 ? " (not in " + string.Join(", ", wrong) + ")" : string.Empty));
    }
}
