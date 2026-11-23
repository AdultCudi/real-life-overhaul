using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;

// What the own bank earns a month before and after the risk, the cards and the desk (printed by --bank-risk-report; it checks nothing): a small (community), a
// middling (regional) and a large (national) bank, each grown for years in a good economy (a boom) and in a recession, with the risk Off (the bank as it was), Standard
// and Hard, and with the cards and the desk open. The profit is the mean of the last twelve months' of the bank's own books, before the owner's pay.
internal static partial class Program
{
    private static void BankRiskReport()
    {
        string[] names = { "small (community)", "middling (regional)", "large (national)" };
        Console.WriteLine("Monthly profit of the own bank, mean of its last 12 months after 6 game years (the year of 60 days, the month of 5), at the start's prices, in dollars.");
        Console.WriteLine("economy      | bank                |        Off |   Standard |       Hard | Standard with cards and desk | Hard with cards and desk | closed? (off, std, hard, " +
            "std+, hard+)");
        foreach (CycleState cycle in new[] { CycleState.Boom, CycleState.Recession })
        {
            for (int tier = BankTiers.Community; tier <= BankTiers.National; tier++)
            {
                long[] profit = new long[5];
                string[] ended = new string[5];
                for (int mode = 0; mode < 5; mode++)
                {
                    int risk = mode == 0 ? 0 : mode == 1 || mode == 3 ? 1 : 2;
                    bool grown = mode >= 3;
                    BankCity c = TierCity(0);
                    c.Risk = risk;
                    c.Cycle = cycle;
                    c.Activity = cycle == CycleState.Boom ? 1100 : 1600;
                    c.Derivs = true;
                    OwnBank b = TierBank(c, tier, 300 + tier, 0);
                    if (grown)
                    {
                        b.CardsOn = true;
                        b.DeskOn = true;
                        b.DeskMandate = BankDesk.Index;
                        b.DeskDirty = true;
                    }
                    List<FundQuote> q = MarketOf(80, 5000, 9);
                    var day = new BankDay();
                    int d = 2;
                    for (; d < 2 + 6 * TierYear && b.Running; d++)
                    {
                        OwnBankRules.Day(b, c, d, q, day);
                    }
                    long sum = 0;
                    int n = Math.Min(12, b.HistoryCount);
                    for (int i = b.HistoryCount - n; i < b.HistoryCount; i++)
                    {
                        sum += b.History[i];
                    }
                    profit[mode] = n > 0 ? sum / n : 0;
                    ended[mode] = b.Running ? "no" : "closed day " + b.EndDay + " (" + b.Why + ")";
                }
                Console.WriteLine((cycle == CycleState.Boom ? "good times   " : "recession    ") + "| " + names[tier].PadRight(20) + "| " + ReportMoney(profit[0]) + " | " + ReportMoney(profit[1])
                    + " | " + ReportMoney(profit[2]) + " | "
                                  + ReportMoney(profit[3]).PadLeft(28) + " | " + ReportMoney(profit[4]).PadLeft(24) + " | " + string.Join(", ", ended));
            }
        }
    }

    private static string ReportMoney(long cents) => (cents / 100).ToString("N0", CultureInfo.InvariantCulture).PadLeft(10);
}
