using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;

// The own bank's automatic dividends (Scripts/Economy/OwnBank/Dividends; a player's suggestion, 1 Nov 2026: "dividends
// paid out automatically at set intervals (e.g., every 7 days, monthly, or annually) simply by configuring the desired
// percentage"): the times, the share of the kept profit to the cent, a time without profit, the reserve and the capital
// the bank keeps, the regulator, a test's bank, off as before, the save, the test tools' rewind, the booking, the page,
// the notices and the texts.
internal static partial class Program
{
    private static void BankDividendChecks()
    {
        int before = _run, failedBefore = _failed;
        var watch = Stopwatch.StartNew();
        DividendTimes();
        DividendShares();
        DividendHeld();
        DividendOff();
        DividendSave();
        DividendSources();
        DividendTexts();
        DividendTiming();
        Console.WriteLine("  own bank's automatic dividends: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    private static BankDay Cleared(BankDay day)
    {
        day.Clear();
        return day;
    }

    // A bank of a level at the start's prices, a year run to grow, then dividends set on its next day.
    private static OwnBank DividendBank(BankCity c, int tier, int every, int share, out int from, long extra = 0)
    {
        OwnBank b = TierBank(c, tier, 211, extra);
        RunBank(b, c, 2, TierYear, null, new BankDay());
        from = b.LastDay + 1;
        OwnBankRules.SetDividends(b, every, share, from);
        return b;
    }

    // Each time falls due on its last day, the first one counted from the day they were set; the lengths are the game's.
    private static void DividendTimes()
    {
        BankCity c = TierCity(0);
        Check(BankDividends.Days(BankDividends.Weekly, c.MonthDays, c.YearDays) == 7 && BankDividends.Days(BankDividends.Monthly, c.MonthDays, c.YearDays) == c.MonthDays
              && BankDividends.Days(BankDividends.Yearly, c.MonthDays, c.YearDays) == c.YearDays && BankDividends.Days(BankDividends.Off, c.MonthDays, c.YearDays) == 0,
            "own bank's dividends: every 7 days, every game month (" + c.MonthDays + " days) and every game year (" + c.YearDays + " days); none while off");
        foreach (int every in new[] { BankDividends.Weekly, BankDividends.Monthly, BankDividends.Yearly })
        {
            OwnBank b = DividendBank(c, BankTiers.Regional, every, 300, out int from);
            int days = BankDividends.Days(every, c.MonthDays, c.YearDays), due = OwnBankRules.DividendDay(b, c.MonthDays, c.YearDays);
            var day = new BankDay();
            var on = new List<int>(8);
            for (int d = from; d < from + 3 * days && b.Running; d++)
            {
                OwnBankRules.Day(b, c, d, null, day);
                if (day.DividendDue)
                {
                    on.Add(d);
                }
            }
            Check(due == from + days - 1 && on.Count == 3 && on[0] == from + days - 1 && on[1] == on[0] + days && on[2] == on[1] + days,
                "own bank's dividends: " + BankDividends.EveryKeys[every] + " falls due on days " + string.Join(", ", on) + " from day " + from);
        }
    }

    // The share of the profit kept over the time, after the owner's pay, to the cent; the books balance and it leaves cash and capital together.
    private static void DividendShares()
    {
        BankCity c = TierCity(0);
        foreach (int share in new[] { 50, 300, 1000 })
        {
            // capital well over its least, so the share and not the capital decides what is paid
            OwnBank b = DividendBank(c, BankTiers.National, BankDividends.Weekly, share, out int from, 3 * OwnBankRules.TierCapital(BankTiers.National, c.Level, 0, 0));
            b.PayMode = BankTiers.PayFixedMode;
            b.PayFixed = 100000;
            var day = new BankDay();
            long kept = 0, sum = 0;
            int due = 0, exact = 0;
            bool balanced = true;
            for (int d = from; d < from + 8 * 7 && b.Running; d++)
            {
                long capital = b.Capital, reserves = b.Reserves;
                OwnBankRules.Day(b, c, d, null, day);
                kept += day.Profit - day.Pay;
                balanced &= OwnBankRules.Balanced(b) && b.Capital - capital == day.Profit - day.Pay - day.PaidOut;
                if (day.DividendDue)
                {
                    due++;
                    long wanted = kept > 0 ? kept * share / 1000 : 0;
                    exact += b.DivWanted == wanted && b.DivPaid == wanted && day.PaidOut == wanted && b.DivWhy == BankDividends.Paid ? 1 : 0;
                    sum += day.PaidOut;
                    kept = 0;
                }
            }
            Check(due == 8 && exact == 8 && balanced && b.DivAll == sum && sum > 0,
                "own bank's dividends: " + share / 10 + "% of the profit kept after the owner's pay is paid each week to the cent, the books balanced (" + Dollars(sum) + " in 8 weeks)");
        }
        OwnBank one = DividendBank(c, BankTiers.Regional, BankDividends.Monthly, 450, out int f1), two = DividendBank(c, BankTiers.Regional, BankDividends.Monthly, 450, out _);
        RunBank(one, c, f1, 3 * TierYear, null, new BankDay());
        RunBank(two, c, f1, 3 * TierYear, null, new BankDay());
        Check(OwnBankStore.Write(one) == OwnBankStore.Write(two) && one.DivAll > 0, "own bank's dividends: the same bank and days pay the same to the cent");
        OwnBank kept0 = DividendBank(c, BankTiers.Regional, BankDividends.Monthly, 0, out int f0);
        RunBank(kept0, c, f0, TierYear, null, new BankDay());
        Check(kept0.DivAll == 0 && kept0.DivDay > 0 && kept0.DivWhy == BankDividends.NoProfit, "own bank's dividends: a share of nothing pays nothing and says there was nothing to pay");
    }

    // Short or skipped, each with why: no profit, the regulator, a run, the reserve, the capital, a test's bank; never into the watch.
    private static void DividendHeld()
    {
        BankCity c = TierCity(0);
        var day = new BankDay();
        OwnBank loss = DividendBank(c, BankTiers.Regional, BankDividends.Weekly, 500, out int from);
        loss.DivKept = -100000000;
        loss.DivFrom = from - 6;
        OwnBankRules.Day(loss, c, from, null, day);
        Check(day.DividendDue && day.PaidOut == 0 && loss.DivWhy == BankDividends.NoProfit && loss.DivFrom == from + 1 && loss.DivKept == 0,
            "own bank's dividends: a time that kept no profit pays nothing, says so, and the next time starts afresh");
        foreach (int stage in new[] { 0, 1, 2 })
        {
            OwnBank b = DividendBank(c, BankTiers.Regional, BankDividends.Weekly, 500, out int f);
            b.DivFrom = f - 6;
            b.DivKept = 50000000;
            if (stage == 0)
            {
                b.Test = true;
            }
            int why = stage == 0 ? BankDividends.Test : stage == 1 ? BankDividends.Breach : BankDividends.Run;
            b.Stage = stage == 1 ? Regulator.Breach : Regulator.Clear;
            b.Run = stage == 2;
            long capital = b.Capital;
            OwnBankRules.Dividends(b, c, f, Cleared(day));
            Check(day.DividendDue && day.PaidOut == 0 && b.DivWhy == why && b.Capital == capital,
                "own bank's dividends: " + (stage == 0 ? "a test's bank" : stage == 1 ? "a breach" : "a run") + " pays nothing, and says why (" + b.DivWhy + ")");
        }
        long least = OwnBankRules.TierCapital(BankTiers.Community, c.Level, 0, 0);
        OwnBank dry = TierBank(c, BankTiers.Community, 97, 2 * least);
        dry.Deposits[OwnBankCatalogue.People] = 10 * least;
        dry.Reserves = OwnBankRules.ReserveNeed(dry) + 1000;
        dry.Loans[OwnBankCatalogue.Good] = dry.Savings + dry.Capital - dry.Reserves;
        OwnBankRules.SetDividends(dry, BankDividends.Weekly, 1000, 1);
        dry.DivKept = 50000000;
        OwnBankRules.Dividends(dry, c, 7, Cleared(day));
        Check(OwnBankRules.Balanced(dry) && day.PaidOut == 1000 && dry.DivPaid == 1000 && dry.DivWanted == 50000000 && dry.DivWhy == BankDividends.Reserves,
            "own bank's dividends: never past the cash over the reserve, and the reserve named as what held it back (" + dry.DivPaid + " of " + dry.DivWanted + ")");
        OwnBank thin = TierBank(c, BankTiers.Community, 98);
        OwnBankRules.SetDividends(thin, BankDividends.Weekly, 1000, 1);
        thin.DivKept = 50000000;
        OwnBankRules.Dividends(thin, c, 7, Cleared(day));
        Check(day.PaidOut == 0 && thin.DivWhy == BankDividends.Capital && OwnBankRules.CapitalNow(thin) >= OwnBankRules.Keep(thin, c.Level),
            "own bank's dividends: a bank at its least capital pays nothing out of it, and says its capital held it back");
        OwnBank lean = DividendBank(c, BankTiers.Community, BankDividends.Weekly, 1000, out int fl);
        lean.SaverOffset[OwnBankCatalogue.People] = lean.SaverOffset[OwnBankCatalogue.Firms] = 300;
        lean.PayMode = BankTiers.PayFixedMode;
        lean.PayFixed = 50000;
        bool watch = true;
        int weeks = 0;
        for (int d = fl; d < fl + 2 * TierYear && lean.Running; d++)
        {
            OwnBankRules.Day(lean, c, d, null, day);
            if (day.DividendDue)
            {
                weeks++;
                watch &= day.PaidOut == 0 || (OwnBankRules.Ratio(lean) >= OwnBankCatalogue.WatchRatio && OwnBankRules.CapitalNow(lean) >= OwnBankRules.Keep(lean, c.Level));
            }
        }
        Check(weeks > 0 && watch && lean.Running && lean.Stage == Regulator.Clear,
            "own bank's dividends: all of the profit paid out every week, with a fixed pay, never takes the bank under its lending line or into the watch (" + weeks + " weeks)");
        OwnBank ahead = TierBank(c, BankTiers.Community, 99, 2 * least);
        ahead.PayMode = BankTiers.PayFixedMode;
        ahead.PayFixed = 10000000;
        OwnBankRules.SetDividends(ahead, BankDividends.Weekly, 1000, 1);
        ahead.DivKept = 900000000;
        long spare = Math.Min(OwnBankRules.FreeCash(ahead), OwnBankRules.CapitalNow(ahead) - OwnBankRules.Keep(ahead, c.Level) - ahead.PayFixed);
        OwnBankRules.Dividends(ahead, c, 7, Cleared(day));
        Check(day.PaidOut == Math.Max(0, spare) && day.PaidOut < 900000000, "own bank's dividends: never into the fixed pay the bank holds back for its owner");
    }

    // Off, and a share set while off, change nothing; a change of how often begins the time again.
    private static void DividendOff()
    {
        BankCity c = TierCity(0);
        OwnBank off = TierBank(c, BankTiers.Regional, 223), plain = TierBank(c, BankTiers.Regional, 223);
        OwnBankRules.SetDividends(off, BankDividends.Off, 500, 1);
        RunBank(off, c, 2, 2 * TierYear, null, new BankDay());
        RunBank(plain, c, 2, 2 * TierYear, null, new BankDay());
        off.DivShare = plain.DivShare;
        Check(OwnBankStore.Write(off) == OwnBankStore.Write(plain) && plain.DivAll == 0 && plain.DivDay == 0, "own bank's dividends: off, the bank runs and keeps exactly as before");
        OwnBank b = DividendBank(c, BankTiers.Regional, BankDividends.Monthly, 300, out int from);
        b.DivKept = 12345;
        OwnBankRules.SetDividends(b, BankDividends.Monthly, 600, from + 3);
        bool kept = b.DivFrom == from && b.DivKept == 12345 && b.DivShare == 600;
        OwnBankRules.SetDividends(b, BankDividends.Yearly, 600, from + 3);
        Check(kept && b.DivFrom == from + 3 && b.DivKept == 0 && BankDividends.ShareOf(349) == 300 && BankDividends.ShareOf(1200) == 1000 && BankDividends.ShareOf(-5) == 0,
            "own bank's dividends: a new share keeps the time, a new how often begins it again; shares move in steps of 5%");
    }

    // The fields are saved after every older one, a line written before them reads as off, and the rewind moves the time back.
    private static void DividendSave()
    {
        BankCity c = TierCity(0);
        OwnBank b = TierBank(c, BankTiers.Regional, 11);
        b.DivEvery = BankDividends.Yearly;
        b.DivShare = 350;
        b.DivFrom = 41;
        b.DivKept = -2500;
        b.DivDay = 40;
        b.DivWanted = 9000;
        b.DivPaid = 7000;
        b.DivWhy = BankDividends.Capital;
        b.DivAll = 123456;
        string line = OwnBankStore.Write(b);
        OwnBank back = OwnBankStore.Read(line, out _);
        Check(back != null && back.DivEvery == BankDividends.Yearly && back.DivShare == 350 && back.DivFrom == 41 && back.DivKept == -2500 && back.DivDay == 40 && back.DivWanted == 9000
              && back.DivPaid == 7000 && back.DivWhy == BankDividends.Capital && back.DivAll == 123456 && OwnBankStore.Write(back) == line && back.PayHeld == b.PayHeld,
            "own bank's dividends: every field saved and read back, after the owner's pay's");
        int head = line.IndexOf("^s:", StringComparison.Ordinal), end = line.IndexOf('^', head + 3);
        string[] fields = line.Substring(head + 3, end - head - 3).Split('|');
        OwnBank old = OwnBankStore.Read(line.Substring(0, head + 3) + string.Join("|", fields, 0, fields.Length - 9) + line.Substring(end), out _);
        Check(old != null && old.DivEvery == BankDividends.Off && old.DivAll == 0 && old.DivFrom == old.LastDay + 1 && old.PayWanted == b.PayWanted,
            "own bank's dividends: a line written before them reads as off, its time starting after its last day run");
        OwnBank odd = OwnBankStore.Read(line.Substring(0, head + 3) + string.Join("|", fields, 0, fields.Length - 9) + "|9|333|5|0|0|10|50|99|0" + line.Substring(end), out _);
        Check(odd != null && odd.DivEvery == BankDividends.Yearly && odd.DivShare == 300 && odd.DivPaid == 10 && odd.DivWhy == BankDividends.WhyMost,
            "own bank's dividends: a line read back is put inside its bounds and on its step");
        OwnBank t = TierBank(c, BankTiers.Regional, 12);
        OwnBankRules.SetDividends(t, BankDividends.Weekly, 300, 1);
        t.DivFrom = 20;
        t.DivDay = 19;
        OwnBankRules.Rewind(t, 5);
        Check(t.DivFrom == 15 && t.DivDay == 14, "own bank's dividends: a test's rewind moves the time and the last one back with the bank");
    }

    // Booked as a dividend taken by hand, told once, on the page in the style of the pay's, and its test entry.
    private static void DividendSources()
    {
        string game = MemberText(EconomySource(@"OwnBank\OwnBankGame.cs"), "public static void BookDay(");
        Check(game.Contains("Book(d.PaidOut, OwnBankCatalogue.CapitalOutKey, hq, bank, null, day, false, false)"),
            "own bank's dividends: booked with the day into the player's cash as a dividend, under the bank's name");
        string director = EconomySource(@"OwnBank\Dividends\OwnBankDirector.Dividends.cs"), run = MemberText(EconomySource(@"OwnBank\OwnBankDirector.cs"), "private void RunDay(");
        Check(run.Contains("TellDividend(day);") && director.Contains("reallife_ob_toast_div_paid") && director.Contains("reallife_ob_toast_div_short") && director.Contains("reallife_ob_toast_div_none")
              && director.Contains("Own bank check (dividend"),
            "own bank's dividends: the day tells a dividend paid, paid short or skipped, and writes its check line");
        string tiers = EconomySource(@"OwnBank\Tiers\OwnBankApp.Tiers.cs"), page = EconomySource(@"OwnBank\Dividends\OwnBankApp.Dividends.cs");
        Check(tiers.Contains("DividendRow(b);") && tiers.Contains("WriteDividends(box, b, c);") && page.Contains("box.Slider(\"reallife_ob_div_share\"") && page.Contains("box.Selector(BankDividends.Count")
              && page.Contains("box.Line(DividendShort(b), CellTone.Yellow)") && EconomySource(@"OwnBank\OwnBankApp.cs").Contains("_divSlid = i => _director.SetDividendShare("),
            "own bank's dividends: a row of the Bank tab opens its page, with the game's own selector and slider as the pay's page has, and what held it back on a line of its own");
        string mod = System.IO.File.ReadAllText(System.IO.Path.Combine(DesignScripts, "Core", "RealLifeOverhaulMod.cs"));
        Check(mod.Contains("AddButton(\"reallife_ob_test_div\", () => Economy.OwnBankDirector.Instance?.TestDividend())")
              && EconomySource(@"OwnBank\OwnBankGuide.cs").Contains("\"reallife_ob_test_div\""),
            "own bank's dividends: a test brings the dividend's day to today");
    }

    private static void DividendTexts()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("own bank's dividends' texts", words, codes))
        {
            return;
        }
        var keys = new List<string>(BankDividends.EveryKeys)
        {
            "reallife_ob_row_div", "reallife_ob_note_div_off", "reallife_ob_note_div_on", "reallife_ob_div_title", "reallife_ob_div_head", "reallife_ob_div_share", "reallife_ob_div_next",
            "reallife_ob_div_kept", "reallife_ob_div_paid", "reallife_ob_div_last", "reallife_ob_div_last_value", "reallife_ob_div_all", "reallife_ob_div_short_line",
            "reallife_ob_div_none_line", "reallife_ob_div_note", "reallife_ob_toast_div_paid", "reallife_ob_toast_div_short", "reallife_ob_toast_div_none", "reallife_ob_test_div",
            "reallife_ob_test_no_div"
        };
        for (int w = 1; w < BankDividends.WhyKeys.Length; w++)
        {
            keys.Add(BankDividends.WhyKeys[w]);
        }
        foreach (string key in keys)
        {
            string lacking = LanguagesLacking(words, codes, key);
            Check(lacking.Length == 0, "own bank's dividends' texts: " + key + " is in every language with the English names (" + lacking + ")");
        }
        string en(string key) => words[0].TryGetValue(key, out string v) ? v : string.Empty;
        int longest = 0;
        for (int w = 1; w < BankDividends.WhyKeys.Length; w++)
        {
            longest = Math.Max(longest, en(BankDividends.WhyKeys[w]).Length);
        }
        bool fits = true;
        foreach (string notice in new[] { "reallife_ob_toast_div_paid", "reallife_ob_toast_div_short", "reallife_ob_toast_div_none" })
        {
            string text = en(notice);
            fits &= text.Length > 0 && text.Length + (text.Contains("{why}") ? longest : 0) + 60 <= 240;
        }
        Check(fits, "own bank's dividends' texts: each notice fits the game's notification with its reason and names filled (longest reason " + longest + ")");
        string guide = en("reallife_guide_economy_ownbank");
        Check(guide.Contains("# Automatic dividends") && guide.Contains("{ob_div_week}") && guide.Contains("{ob_div_step}") && guide.Contains("<b>Automatic dividends</b>")
              && EconomySource(@"OwnBank\OwnBankGuide.cs").Contains(".Fact(\"ob_div_week\"") && EconomySource(@"OwnBank\OwnBankGuide.cs").Contains(".Fact(\"ob_div_step\""),
            "own bank's dividends' texts: the Guide's bank page has the dividends, its figures from the code");
        string[][] lines =
        {
            new[] { "en", "Your own bank can pay you dividends on its own" }, new[] { "pt", "Seu próprio banco pode pagar dividendos a você sozinho" },
            new[] { "zh-cn", "你自己的银行可以自动向你支付股息" }, new[] { "de", "Deine eigene Bank kann dir von selbst Dividenden zahlen" },
            new[] { "fr", "Votre propre banque peut vous verser des dividendes" }, new[] { "es", "Tu propio banco puede pagarte dividendos por sí solo" },
            new[] { "ru", "Ваш собственный банк может сам выплачивать вам дивиденды" }, new[] { "nl", "Je eigen bank kan je vanzelf dividenden betalen" },
            new[] { "tr", "Kendi bankanız size kendiliğinden temettü ödeyebilir" }, new[] { "pl", "Twój własny bank może sam wypłacać ci dywidendy" },
            new[] { "ko", "내 은행이 알아서 배당을 지급할 수 있습니다" }
        };
        foreach (string[] row in lines)
        {
            int l = Array.IndexOf(codes, row[0]);   // Polish and Korean are not per-feature languages: their own files are read
            Dictionary<string, string> own = l >= 0 ? words[l] : LoadWords(System.IO.Path.Combine(LocalesFolder(), row[0] + ".json"));
            Check(own != null && WhatsNewHas(own, row[1]), "own bank's dividends' texts: What's new has the feature in " + row[0]);
        }
    }

    // Days of a bank paying weekly dividends and a fixed pay at its level's limits, at scale: quick and allocation-free.
    private static void DividendTiming()
    {
        BankCity c = TierCity(EndScaleRules.Full);
        OwnBank b = TierBank(c, BankTiers.Community, 131, OwnBankRules.TierCapital(BankTiers.Community, c.Level, 0, EndScaleRules.Full) * 6);
        b.SaverOffset[0] = b.SaverOffset[1] = 300;
        b.PayMode = BankTiers.PayFixedMode;
        b.PayFixed = 100000;
        OwnBankRules.SetDividends(b, BankDividends.Weekly, 500, 2);
        var day = new BankDay();
        TierDays(b, c, 2, 30, day);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        TierDays(b, c, 32, 600, day);
        double perDay = watch.Elapsed.TotalMilliseconds / 600.0;
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Check(perDay < 0.5 && allocated < 4096 && b.DivAll > 0, "own bank's dividends: a day with weekly dividends in under half a millisecond, allocating nothing ("
                                                                  + perDay.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " ms, " + allocated + " bytes over 600 days)");
    }
}
