using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;

// Two faults of the player's own bank a player reported (1 Nov 2026), each reproduced here:
//   1. "In Finance, the bank shows the name of my headquarters instead of the name I gave it": every line the bank booked
//      named the headquarters (OwnBankGame.Book filled {place} from the headquarters' registration). Now every line names
//      the bank, and the headquarters' name stands in only for a bank without one.
//   2. "When I choose to receive a fixed salary, it still deducts more when there is a higher profit": a fixed pay was
//      capped at the day's profit, and the bank's lending, fund and counters spent its cash and capital first, so the
//      evening found only about the day's takings over the reserve and the lending line and the pay moved with them. Now a
//      fixed pay is held back before the bank lends or buys and is paid in full whatever the day made, within the reserve
//      and the capital the bank must keep; what still held it back is saved and named on the pay's page.
internal static partial class Program
{
    private static void BankPayFixes()
    {
        int before = _run, failedBefore = _failed;
        var watch = Stopwatch.StartNew();
        BankPayFixedInFull();
        BankPayFixedRules();
        BankPayHeld();
        BankPaySave();
        BankPayName();
        BankPayPage();
        BankPayTexts();
        BankPayTiming();
        Console.WriteLine("  own bank's name and fixed pay: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // A bank of a level at a share of the scale, a year run with no pay to learn its profit, then a fixed pay of a share of
    // that average: what the next two years paid. Returns the average day's profit.
    private static long BankPayRun(int tier, int scale, int offset, int tenths, out OwnBank b, out int clear, out int full, out int cutAbove, out bool balanced)
    {
        BankCity c = TierCity(scale);
        b = TierBank(c, tier, 83);
        b.SaverOffset[OwnBankCatalogue.People] = b.SaverOffset[OwnBankCatalogue.Firms] = offset;
        var day = new BankDay();
        long sum = 0;
        int days = 0;
        for (int d = 2; d < 2 + TierYear && b.Running; d++)
        {
            OwnBankRules.Day(b, c, d, null, day);
            sum += day.Profit;
            days++;
        }
        long average = sum / Math.Max(1, days);
        b.PayMode = BankTiers.PayFixedMode;
        b.PayFixed = Math.Max(0, average * tenths / 10);
        clear = full = cutAbove = 0;
        balanced = true;
        for (int d = 2 + TierYear; d < 2 + 3 * TierYear && b.Running; d++)
        {
            OwnBankRules.Day(b, c, d, null, day);
            balanced &= OwnBankRules.Balanced(b) && b.PayGiven == day.Pay && b.PayWanted == b.PayFixed;
            if (b.Stage == Regulator.Breach || b.Run)
            {
                continue;
            }
            clear++;
            full += day.Pay == b.PayFixed ? 1 : 0;
            cutAbove += day.Profit > b.PayFixed && day.Pay < b.PayFixed ? 1 : 0;
        }
        return average;
    }

    // The player's case: a fixed pay under the day's profit is paid in full on every day the regulator is content, at every
    // level and scale, even at a national bank that lends out every dollar it can (the report's bank).
    private static void BankPayFixedInFull()
    {
        foreach (int scale in new[] { 0, EndScaleRules.Full })
        {
            foreach (int tier in new[] { BankTiers.Community, BankTiers.National })
            {
                long average = BankPayRun(tier, scale, 0, 5, out OwnBank b, out int clear, out int full, out int cutAbove, out bool balanced);
                Check(clear > 0 && full == clear && cutAbove == 0 && balanced && average > 0,
                    "own bank's fixed pay: " + TierWords(tier) + (scale > 0 ? " at scale" : string.Empty) + ", " + Dollars(b.PayFixed) + " a day against " + Dollars(average)
                    + " of profit is paid in full on " + full + " of " + clear + " days, cut on " + cutAbove + " days the profit covered it");
            }
        }
        BankPayRun(BankTiers.National, 0, 300, 5, out OwnBank keen, out int kClear, out int kFull, out int kCut, out bool kBalanced);
        Check(kClear > 0 && kFull == kClear && kCut == 0 && kBalanced,
            "own bank's fixed pay: a national bank paying its savers 3 points over the rivals, lending all it can, pays it in full (" + kFull + " of " + kClear + " days)");
    }

    // The rule itself: a fixed sum whatever the day made, a share of a profit only, nothing in a breach, a run or from a test's bank.
    private static void BankPayFixedRules()
    {
        BankCity c = TierCity(0);
        OwnBank p = TierBank(c, BankTiers.Community, 89, OwnBankRules.TierCapital(BankTiers.Community, c.Level, 0, 0));
        RunBank(p, c, 2, TierYear, null, new BankDay());
        p.PayMode = BankTiers.PayFixedMode;
        p.PayFixed = 500000;
        long more = OwnBankRules.OwnerPay(p, 800000, c.Level), less = OwnBankRules.OwnerPay(p, 300000, c.Level), none = OwnBankRules.OwnerPay(p, 0, c.Level),
             loss = OwnBankRules.OwnerPay(p, -200000, c.Level, out long wanted, out int held);
        Check(more == 500000 && less == 500000 && none == 500000 && loss == 500000 && wanted == 500000 && held == BankTiers.PayHeldNone,
            "own bank's fixed pay: $5,000 a day is $5,000 on a day of $8,000, $3,000, nothing or a loss of $2,000, while the bank can spare it");
        p.PayMode = BankTiers.PayShareMode;
        p.PayShare = 350;
        Check(OwnBankRules.OwnerPay(p, 1000001, c.Level) == 350000 && OwnBankRules.OwnerPay(p, 0, c.Level) == 0 && OwnBankRules.OwnerPay(p, -5000, c.Level, out long w, out int h) == 0
              && w == 0 && h == BankTiers.PayHeldNone,
            "own bank's fixed pay: a share is still a share of a profit, and a day without one asks for nothing and holds nothing back");
        p.PayMode = BankTiers.PayFixedMode;
        Check(OwnBankRules.PayAhead(p) == 500000 && OwnBankRules.FreeCash(p) == p.Reserves - OwnBankRules.ReserveNeed(p) - 500000
              && OwnBankRules.FreeCapital(p) == OwnBankRules.CapitalNow(p) - 500000,
            "own bank's fixed pay: the bank holds the fixed pay back from what it may lend or buy with");
        p.Stage = Regulator.Breach;
        long breach = OwnBankRules.OwnerPay(p, 800000, c.Level, out _, out int breachWhy), aheadBreach = OwnBankRules.PayAhead(p);
        p.Stage = Regulator.Clear;
        p.Run = true;
        long run = OwnBankRules.OwnerPay(p, 800000, c.Level, out _, out int runWhy), aheadRun = OwnBankRules.PayAhead(p);
        p.Run = false;
        p.Test = true;
        long test = OwnBankRules.OwnerPay(p, 800000, c.Level, out _, out int testWhy);
        p.Test = false;
        Check(breach == 0 && run == 0 && test == 0 && breachWhy == BankTiers.PayHeldBreach && runWhy == BankTiers.PayHeldRun && testWhy == BankTiers.PayHeldNone
              && aheadBreach == 0 && aheadRun == 0,
            "own bank's fixed pay: nothing in a breach or a run, each named as why, nor from a test's bank, and nothing held back from the lending then");
        p.PayMode = BankTiers.PayShareMode;
        Check(OwnBankRules.PayAhead(p) == 0 && OwnBankRules.FreeCash(p) == p.Reserves - OwnBankRules.ReserveNeed(p),
            "own bank's fixed pay: a share is worked out of the profit after, so nothing is held back for it");

        // The same days, the same pay; a fixed pay of nothing is no pay at all, as before.
        BankPayRun(BankTiers.National, 0, 0, 5, out OwnBank one, out _, out _, out _, out _);
        BankPayRun(BankTiers.National, 0, 0, 5, out OwnBank two, out _, out _, out _, out _);
        Check(OwnBankStore.Write(one) == OwnBankStore.Write(two) && one.PaidAll > 0, "own bank's fixed pay: the same bank and days pay the same to the cent");
        OwnBank zero = TierBank(c, BankTiers.National, 109), share = TierBank(c, BankTiers.National, 109);
        zero.PayMode = BankTiers.PayFixedMode;
        RunBank(zero, c, 2, TierYear, null, new BankDay());
        RunBank(share, c, 2, TierYear, null, new BankDay());
        zero.PayMode = share.PayMode;
        Check(OwnBankStore.Write(zero) == OwnBankStore.Write(share) && share.PaidAll == 0, "own bank's fixed pay: a fixed pay of nothing lends, buys and keeps exactly as no pay does");
    }

    // A fixed pay more than the bank earns: paid while its capital allows, then held back at its least, never past the
    // reserve, its least capital or the lending line, and never into the regulator's watch; what was held back named.
    private static void BankPayHeld()
    {
        long average = BankPayRun(BankTiers.Community, 0, 300, 15, out OwnBank b, out int clear, out int full, out _, out bool balanced);
        BankCity c = TierCity(0);
        Check(average > 0 && full > 0 && full < clear && balanced && b.PayGiven < b.PayWanted && b.PayHeld == BankTiers.PayHeldCapital
              && OwnBankRules.CapitalNow(b) >= OwnBankRules.Keep(b, c.Level) && OwnBankRules.Ratio(b) >= OwnBankCatalogue.WatchRatio,
            "own bank's fixed pay: one over the profit is paid in full for " + full + " of " + clear + " days, then held back at the capital the bank keeps ("
            + Dollars(b.PayGiven) + " of " + Dollars(b.PayWanted) + ", the ratio " + OwnBankRules.Ratio(b) + ")");
        // A bank with capital to spare whose cash over the reserve is $10: the reserve binds, not the capital.
        long least = OwnBankRules.TierCapital(BankTiers.Community, c.Level, 0, 0);
        OwnBank dry = TierBank(c, BankTiers.Community, 97, 2 * least);
        dry.PayMode = BankTiers.PayFixedMode;
        dry.PayFixed = 100000000;
        dry.Deposits[OwnBankCatalogue.People] = 10 * least;
        dry.Reserves = OwnBankRules.ReserveNeed(dry) + 1000;
        dry.Loans[OwnBankCatalogue.Good] = dry.Savings + dry.Capital - dry.Reserves;
        long spare = Math.Min(dry.Reserves - OwnBankRules.ReserveNeed(dry), OwnBankRules.CapitalNow(dry) - OwnBankRules.Keep(dry, c.Level));
        long paid = OwnBankRules.OwnerPay(dry, 0, c.Level, out long wanted, out int why);
        Check(OwnBankRules.Balanced(dry) && spare == 1000 && paid == 1000 && wanted == 100000000 && why == BankTiers.PayHeldReserves,
            "own bank's fixed pay: never past the cash over the reserve, and the reserve named as what held it back (" + paid + " of " + wanted + ")");
    }

    // The last day's pay is saved, and a line written before it reads as nothing to tell.
    private static void BankPaySave()
    {
        BankCity c = TierCity(0);
        OwnBank b = TierBank(c, BankTiers.Regional, 11);
        b.PayWanted = 750000;
        b.PayGiven = 420000;
        b.PayHeld = BankTiers.PayHeldReserves;
        string line = OwnBankStore.Write(b);
        OwnBank back = OwnBankStore.Read(line, out _);
        Check(back != null && back.PayWanted == 750000 && back.PayGiven == 420000 && back.PayHeld == BankTiers.PayHeldReserves && OwnBankStore.Write(back) == line,
            "own bank's fixed pay: the last day's pay asked for, paid and why less are saved and read back");
        int head = line.IndexOf("^s:", StringComparison.Ordinal), end = line.IndexOf('^', head + 3);
        string[] fields = line.Substring(head + 3, end - head - 3).Split('|');
        const int payAt = 44;   // the last day's pay asked for, after the level's and the pay's fields (OwnBankStore's TierAt and on)
        string older = line.Substring(0, head + 3) + string.Join("|", fields, 0, payAt) + line.Substring(end);
        OwnBank old = OwnBankStore.Read(older, out _);
        Check(old != null && old.PayWanted == 0 && old.PayGiven == 0 && old.PayHeld == BankTiers.PayHeldNone && old.PaidAll == b.PaidAll,
            "own bank's fixed pay: a line written before them reads as no day's pay to tell of");
        string odd = line.Substring(0, head + 3) + string.Join("|", fields, 0, payAt) + "|100|900|77" + line.Substring(end);
        OwnBank bounded = OwnBankStore.Read(odd, out _);
        Check(bounded != null && bounded.PayGiven == 100 && bounded.PayHeld == BankTiers.PayHeldMost, "own bank's fixed pay: a pay read back is never more than was asked, and its reason within the list");
    }

    // Every line the bank books names the bank; the headquarters' name stands in only for a bank without one.
    private static void BankPayName()
    {
        string game = EconomySource(@"OwnBank\OwnBankGame.cs");
        string book = MemberText(game, "private static bool Book(");
        Check(book.Contains("{ \"place\", Place(bank, hq) }") && !book.Contains("Name(hq)") && MemberText(game, "public static string Place(").Contains("string.IsNullOrEmpty(bank) ? Name(hq) : bank"),
            "own bank's name: a bank transaction's {place} is the bank's own name, the headquarters' only for a bank without one");
        string calls = EconomySource(@"OwnBank\OwnBankDirector.cs") + EconomySource(@"OwnBank\Tiers\OwnBankDirector.Tiers.cs") + EconomySource(@"OwnBank\Branch\OwnBankDirector.Branches.cs")
                       + EconomySource(@"OwnBank\OwnBankDirector.Tests.cs");
        string[] wanted =
        {
            "OwnBankGame.BookDay(_day, hq, _bank.Name,", "OwnBankGame.Give(_day.WoundUp, OwnBankCatalogue.SaleKey, hq, name)", "OwnBankGame.Pay(licence, OwnBankCatalogue.LicenceKey, hq, named,",
            "OwnBankGame.Pay(capital, OwnBankCatalogue.CapitalInKey, hq, named,", "OwnBankGame.Give(licence, OwnBankCatalogue.LicenceKey, hq, named,",
            "OwnBankGame.Pay(cents, OwnBankCatalogue.CapitalInKey, OwnBankGame.Headquarters(), _bank.Name,", "OwnBankGame.Give(cents, OwnBankCatalogue.CapitalOutKey, OwnBankGame.Headquarters(), _bank.Name)",
            "OwnBankGame.Give(price, OwnBankCatalogue.SaleKey, OwnBankGame.Headquarters(), name)", "OwnBankGame.Pay(fee, OwnBankCatalogue.LicenceKey, OwnBankGame.Headquarters(), _bank.Name,"
        };
        int named = 0;
        foreach (string call in wanted)
        {
            named += calls.Contains(call) ? 1 : 0;
        }
        int all = Count(calls, "OwnBankGame.BookDay(") + Count(calls, "OwnBankGame.Give(") + Count(calls, "OwnBankGame.Pay(");
        Check(named == wanted.Length && all == wanted.Length,
            "own bank's name: each of the bank's " + all + " bookings (the day, the licence, capital in and out, a sale, a winding up, a move up) passes the bank's name (" + named + " of " + wanted.Length + ")");
        string found = MemberText(EconomySource(@"OwnBank\OwnBankDirector.cs"), "public bool Found(");
        int name = found.IndexOf("string named = Named(name, hq);", StringComparison.Ordinal), licence = found.IndexOf("OwnBankGame.Pay(licence", StringComparison.Ordinal);
        Check(name > 0 && licence > name && found.Contains("capital, named, BankTiers.Community)"),
            "own bank's name: the name typed at founding is on the licence and the capital paid in, and is the name the bank is founded under");
    }

    // The pay's page names what held the last day's pay back on a line of its own, never folded into the pay.
    private static void BankPayPage()
    {
        string tiers = EconomySource(@"OwnBank\Tiers\OwnBankApp.Tiers.cs");
        string page = MemberText(tiers, "private void WritePay("), held = MemberText(tiers, "private static string HeldText(");
        Check(page.Contains("\"reallife_ob_pay_day\", OwnBankText.Money(b.PayGiven)") && page.Contains("box.Line(HeldText(b), CellTone.Yellow)")
              && held.Contains("reallife_ob_pay_held_breach") && held.Contains("reallife_ob_pay_held_run") && held.Contains("reallife_ob_pay_held_reserves") && held.Contains("reallife_ob_pay_held_capital")
              && held.Contains("b.PayWanted - b.PayGiven"),
            "own bank's fixed pay: the pay's page shows the last day's pay and, on its own line, how much was held back and why");
        string day = EconomySource(@"OwnBank\OwnBankRules.Day.cs");
        Check(day.Contains("OwnerPay(b, into.Profit, c.Level, out long wanted, out int held)") && day.Contains("long cash = FreeCash(b) - hold;")
              && day.Contains("OwnBankFund.Day(b, c, day, quotes, into, hold);") && EconomySource(@"OwnBank\Branch\OwnBankRules.Branch.cs").Contains("long cash = FreeCash(b);"),
            "own bank's fixed pay: the day's lending, the fund and the counters all set the fixed pay aside, and the day saves what it paid");
    }

    private static void BankPayTexts()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("own bank's pay texts", words, codes))
        {
            return;
        }
        foreach (string key in new[] { "reallife_ob_pay_day", "reallife_ob_pay_held_breach", "reallife_ob_pay_held_run", "reallife_ob_pay_held_reserves", "reallife_ob_pay_held_capital",
                     "reallife_ob_note_pay_fixed", "reallife_ob_pay_note", OwnBankCatalogue.KeptKey })
        {
            string lacking = LanguagesLacking(words, codes, key);
            Check(lacking.Length == 0, "own bank's pay texts: " + key + " is in every language with the English names (" + lacking + ")");
        }
        string en(string key) => words[0].TryGetValue(key, out string v) ? v : string.Empty;
        Check(en("reallife_ob_pay_held_reserves").Contains("{amount}") && en("reallife_ob_pay_held_reserves").Contains("{reserve}") && en("reallife_ob_pay_held_capital").Contains("{amount}"),
            "own bank's pay texts: what was held back is named with its sum, the reserve with its share");
        string guide = en("reallife_guide_economy_ownbank");
        int pay = guide.IndexOf("# Your pay", StringComparison.Ordinal);
        string section = pay >= 0 ? guide.Substring(pay, Math.Max(0, guide.IndexOf("# Staff", pay, StringComparison.Ordinal) - pay)) : string.Empty;
        Check(section.Length > 0 && !section.Contains("never more than the day's profit") && section.Contains("whatever the day's profit")
              && !en("reallife_ob_note_pay_fixed").Contains("never more than") && en(OwnBankCatalogue.KeptKey).Contains("{place}"),
            "own bank's pay texts: the Guide and the pay's row no longer say a fixed pay is capped at the day's profit");
        string[][] lines =
        {
            new[] { "en", "Fixed: your own bank's lines in Finance", "Fixed: a fixed pay from your own bank" },
            new[] { "pt", "Corrigido: as linhas do seu próprio banco", "Corrigido: o pró-labore fixo do seu próprio banco" },
            new[] { "zh-cn", "已修复：你自己的银行在财务应用", "已修复：你自己的银行的固定薪酬" },
            new[] { "de", "Behoben: Die Zeilen deiner eigenen Bank", "Behoben: Eine feste Bezahlung von deiner eigenen Bank" },
            new[] { "fr", "Corrigé : les lignes de votre propre banque", "Corrigé : une rémunération fixe de votre propre banque" },
            new[] { "es", "Corregido: las líneas de tu propio banco", "Corregido: un sueldo fijo de tu propio banco" },
            new[] { "ru", "Исправлено: строки вашего собственного банка", "Исправлено: фиксированная оплата от вашего собственного банка" },
            new[] { "nl", "Opgelost: de regels van je eigen bank", "Opgelost: een vast loon van je eigen bank" },
            new[] { "tr", "Düzeltildi: kendi bankanızın Finans", "Düzeltildi: kendi bankanızdan aldığınız sabit maaş" },
            new[] { "pl", "Naprawiono: wiersze twojego własnego banku", "Naprawiono: stała wypłata z twojego własnego banku" },
            new[] { "ko", "수정: 내 은행의 줄이", "수정: 내 은행의 고정 급여가" }
        };
        foreach (string[] row in lines)
        {
            int l = Array.IndexOf(codes, row[0]);   // Polish and Korean are not per-feature languages: their own files are read
            Dictionary<string, string> own = l >= 0 ? words[l] : LoadWords(System.IO.Path.Combine(LocalesFolder(), row[0] + ".json"));
            Check(own != null && WhatsNewHas(own, row[1]) && WhatsNewHas(own, row[2]), "own bank's pay texts: What's new has both fixes in " + row[0]);
        }
    }

    // A day of a bank paying a fixed sum at its level's limits, at scale: as quick and as free of allocation as one without.
    private static void BankPayTiming()
    {
        BankCity c = TierCity(EndScaleRules.Full);
        OwnBank b = TierBank(c, BankTiers.Community, 127, OwnBankRules.TierCapital(BankTiers.Community, c.Level, 0, EndScaleRules.Full) * 6);
        b.SaverOffset[0] = b.SaverOffset[1] = 300;
        b.PayMode = BankTiers.PayFixedMode;
        b.PayFixed = 100000;
        var day = new BankDay();
        TierDays(b, c, 2, 30, day);   // warmed
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        TierDays(b, c, 32, 600, day);
        double perDay = watch.Elapsed.TotalMilliseconds / 600.0;
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Check(perDay < 0.5 && allocated < 4096, "own bank's fixed pay: a day holding a fixed pay back in under half a millisecond, allocating nothing ("
                                                + perDay.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " ms, " + allocated + " bytes over 600 days)");
    }
}
