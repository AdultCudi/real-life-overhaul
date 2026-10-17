using RealLifeOverhaul.Economy;

// The player's own bank fixed, each reproduced here:
//   1. switched back on after its option or the credit side was off, it ran and booked up to a week of the days it stood still;
//   2. the extra capital asked of a bank founded after a closure could be taken straight back out as a dividend;
//   3. what a written-off loan brings back never finished within its months: each day took a part of what was left;
//   4. the founding and sale questions confirmed sums worked out again when Yes was pressed, after a day may have passed;
//   5. founding paid the licence and the capital as two transactions, and kept the licence if the game refused the capital;
//   6. the regulator's watch notice was told again after every load, as the day it was told was not saved;
//   7. a month run by the test tools left that notice held back, and the store moved a rewound bank's days to nothing.
internal static partial class Program
{
    private static void OwnBankFixes()
    {
        OwnBankFixStill();
        OwnBankFixAgainCapital();
        OwnBankFixRecovery();
        OwnBankFixConfirmed();
        OwnBankFixWatch();
    }

    private static void OwnBankFixStill()
    {
        BankCity c = BankCity.Quiet(BankYear);
        OwnBank b = NewBank(c, 50);
        b.LastDay = 99;                       // stood down on day 100
        b.Still = true;
        OwnBank back = OwnBankStore.Read(OwnBankStore.Write(b), out _);
        Check(back != null && back.Still, "own bank fix: a line written while the feature stood down says so");
        OwnBankRules.Resume(back, 120, 7);
        Check(back.LastDay == 119 && !back.Still, "own bank fix: switched on again on day 120 after standing still from day 100, no day it stood still is run (" + back.LastDay + ")");
        OwnBank jump = NewBank(c, 50);
        jump.LastDay = 99;
        OwnBankRules.Resume(jump, 120, 7);
        Check(jump.LastDay == 112, "own bank fix: a bank that did not stand down still catches up a week at most");
        OwnBank recent = NewBank(c, 50);
        recent.LastDay = 118;
        OwnBankRules.Resume(recent, 120, 7);
        Check(recent.LastDay == 118, "own bank fix: a day not yet run when the save was made is still run");
        string director = EconomySource(@"OwnBank\OwnBankDirector.cs");
        string down = MemberText(director, "private void StandDown(");
        Check(down.IndexOf("_bank.Still = true", System.StringComparison.Ordinal) >= 0 && down.IndexOf("_bank.Still = true", System.StringComparison.Ordinal) < down.IndexOf("Write(Save)", System.StringComparison.Ordinal)
              && MemberText(director, "private void StartUp(").Contains("OwnBankRules.Resume(bank, save.Day, CatchUpDays)"),
            "own bank fix: the director marks the line it writes on standing down, and reads it back through the rule");
    }

    private static void OwnBankFixAgainCapital()
    {
        BankCity c = BankCity.Quiet(BankYear);
        foreach (int closures in new[] { 0, 1, 2 })
        {
            var b = new OwnBank { Closures = closures };
            long capital = OwnBankRules.CapitalLeast(c.Level, closures, 0);
            OwnBankRules.Found(b, 10, 7, capital, "Again");
            b.FoundedLeast = OwnBankRules.LeastAt(0, b.Closures);
            Check(OwnBankRules.MostOut(b, c.Level) == 0 && b.FoundedLeast == OwnBankRules.LeastAt(0) * (1000 + closures * OwnBankCatalogue.AgainCapital) / 1000,
                "own bank fix: on the day a bank is founded after " + closures + " closure(s) no dividend can be taken (" + OwnBankRules.MostOut(b, c.Level) + ")");
        }
        Check(MemberText(EconomySource(@"OwnBank\OwnBankDirector.cs"), "public bool Found(").Contains("_bank.FoundedLeast = OwnBankRules.LeastAt(share, _bank.Closures)"),
            "own bank fix: the bank keeps the capital it was founded with, closures and all");
    }

    private static void OwnBankFixRecovery()
    {
        BankCity c = BankCity.Quiet(BankYear);
        var day = new BankDay();
        OwnBank b = NewBank(c);
        for (int j = 0; j < OwnBankCatalogue.Segments; j++)
        {
            b.Lending[j] = false;
        }
        int months = OwnBankCatalogue.RecoverMonths * c.MonthDays;
        b.Recovering[OwnBankCatalogue.Shops] = 50000000L;
        b.Recover[OwnBankCatalogue.Shops] = months;
        long back = 0;
        RunBank(b, c, 2, months, null, day, d => back += day.Recovered);
        Check(b.Recovering[OwnBankCatalogue.Shops] == 0 && back == 50000000L,
            "own bank fix: a write-off's part comes back within its " + OwnBankCatalogue.RecoverMonths + " months (" + b.Recovering[OwnBankCatalogue.Shops] + " cents left)");
        OwnBank half = NewBank(c);
        for (int j = 0; j < OwnBankCatalogue.Segments; j++)
        {
            half.Lending[j] = false;
        }
        half.Recovering[OwnBankCatalogue.Shops] = 50000000L;
        half.Recover[OwnBankCatalogue.Shops] = months;
        RunBank(half, c, 2, months / 2, null, day);
        Check(System.Math.Abs(half.Recovering[OwnBankCatalogue.Shops] - 25000000L) <= 1000000L, "own bank fix: it comes back evenly, half by half way (" + half.Recovering[OwnBankCatalogue.Shops] + ")");
    }

    private static void OwnBankFixConfirmed()
    {
        string director = EconomySource(@"OwnBank\OwnBankDirector.cs"), app = EconomySource(@"OwnBank\OwnBankApp.cs");
        string found = MemberText(director, "public bool Found("), sell = MemberText(director, "public bool Sell(");
        Check(found.Contains("licence != licenceAsked || capital != capitalAsked") && sell.Contains("OwnBankRules.SaleValue(_bank) != priceAsked")
              && app.Contains("_director.Found(name, licence, capital, out string why)") && app.Contains("_director.Sell(value, out string why)"),
            "own bank fix: founding and selling go ahead only at the sums the player confirmed");
        int pay = found.IndexOf("OwnBankGame.Pay(capital", System.StringComparison.Ordinal);
        Check(pay > 0 && found.IndexOf("OwnBankGame.Give(licence, OwnBankCatalogue.LicenceKey, hq, GameBooks.Licensing)", pay, System.StringComparison.Ordinal) > pay
              && found.IndexOf("|| !OwnBankGame.Pay(capital", System.StringComparison.Ordinal) < 0,
            "own bank fix: a capital payment the game refuses gives the licence back");
    }

    private static void OwnBankFixWatch()
    {
        BankCity c = BankCity.Quiet(BankYear);
        OwnBank b = NewBank(c, 30);
        b.WatchTold = 57;
        OwnBank back = OwnBankStore.Read(OwnBankStore.Write(b), out _);
        Check(back != null && back.WatchTold == 57 && OwnBankRules.WatchDue(60, back.WatchTold, c.MonthDays) == false,
            "own bank fix: the day the watch was told is saved, so a load does not tell it again within the month");
        string line = OwnBankStore.Write(NewBank(c, 30));
        int head = line.IndexOf("^s:", System.StringComparison.Ordinal), end = line.IndexOf('^', head + 3);
        string[] fields = line.Substring(head + 3, end - head - 3).Split('|');
        string older = line.Substring(0, head + 3) + string.Join("|", fields, 0, 33) + line.Substring(end);
        OwnBank old = OwnBankStore.Read(older, out _);
        Check(fields.Length == 35 && old != null && old.WatchTold == int.MinValue && !old.Still, "own bank fix: a line written before both fields reads as never told and not stood still");
        Check(EconomySource(@"OwnBank\OwnBankDirector.cs").IndexOf("_watchTold", System.StringComparison.Ordinal) < 0, "own bank fix: the director keeps no watch day of its own");

        OwnBank t = NewBank(c, 3);
        t.WatchTold = 5;
        OwnBankRules.Rewind(t, 10);
        OwnBank rewound = OwnBankStore.Read(OwnBankStore.Write(t), out _);
        Check(t.WatchTold == -5 && rewound != null && rewound.FoundedDay == -7 && rewound.LastDay == t.LastDay && rewound.MonthFrom == -7 && rewound.WatchTold == -5,
            "own bank fix: a test's month moves the watch day back with the bank, and a rewound bank's days read back as they were");
        OwnBank never = NewBank(c, 3);
        OwnBankRules.Rewind(never, 10);
        Check(never.WatchTold == int.MinValue, "own bank fix: a watch never told stays never told through a rewind");
    }
}
