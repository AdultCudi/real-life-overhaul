using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Dealership;

// The car showroom fixed, each reproduced here:
//   1. "offers waiting" never showed: the hour last told started at int.MinValue, and the hour now less it ran over;
//      the regulator's watch notice of the player's own bank had the same fault, and every such "never" in the mod is looked for;
//   2. a showroom's day was cleared rather than closed when the feature came back on a later day, so its figures never
//      reached the reports, and a catch-up hour of the day before closed the day too soon;
//   3. finance sold on at once (no room for one more plan) was paid but missing from the day's takings;
//   4. past the warranties kept, a premium was taken with no warranty behind it;
//   5. laying a lot out again kept the cars the save had lost;
//   6. an event that ended while the feature was off was overwritten by the next booking, its rest and notice lost.
internal static partial class Program
{
    private static void DealerFixes()
    {
        DealerFixNever();
        DealerFixBook();
        DealerFixMoney();
        DealerFixEvent();
        DealerFixSources();
    }

    private static void DealerFixNever()
    {
        Check(DealerRules.TellDue(5, int.MinValue) && DealerRules.TellDue(int.MaxValue, int.MinValue) && !DealerRules.TellDue(100, 96) && DealerRules.TellDue(102, 96),
            "dealer fix: offers waiting are told the first time, then six hours apart at most");
        Check(OwnBankRules.WatchDue(3, int.MinValue, 28) && !OwnBankRules.WatchDue(30, 10, 28) && OwnBankRules.WatchDue(38, 10, 28), "dealer fix: the own bank's watch notice is told the first time, then a month apart");
        string found = NeverSubtracted();
        Check(found.Length == 0, "dealer fix: no field or local that starts at int.MinValue as \"never\" is subtracted from (" + found + ")");
    }

    // Every name declared at int.MinValue or long.MinValue in Scripts that is then subtracted from or subtracted in its file.
    // Accumulators of a range, set by the loop that follows before they are read, are listed as such.
    private static string NeverSubtracted()
    {
        var accumulators = new HashSet<string>(StringComparer.Ordinal) { "DistrictRules.cs:most", "LedgerSpark.cs:high" };
        var declared = new Regex(@"\b(\w+)\s*=\s*(?:int|long)\.MinValue\b");
        string scripts = System.IO.Path.Combine(LocalesFolder(), @"..\Scripts");
        var found = new List<string>();
        foreach (string path in System.IO.Directory.GetFiles(scripts, "*.cs", System.IO.SearchOption.AllDirectories))
        {
            string code = System.IO.File.ReadAllText(path);
            if (!code.Contains("MinValue"))
            {
                continue;
            }
            string file = System.IO.Path.GetFileName(path);
            string[] lines = code.Split('\n');
            foreach (Match m in declared.Matches(code))
            {
                string name = m.Groups[1].Value;
                if (accumulators.Contains(file + ":" + name))
                {
                    continue;
                }
                var used = new Regex(@"-\s*" + name + @"\b(?!\s*\()|\b" + name + @"\s*-(?![-=>])");
                foreach (string line in lines)
                {
                    string t = line.Trim();
                    if (!t.StartsWith("//", StringComparison.Ordinal) && !t.Contains("MinValue") && used.IsMatch(t))
                    {
                        found.Add(file + ":" + name);
                        break;
                    }
                }
            }
        }
        return string.Join(", ", found);
    }

    private static void DealerFixBook()
    {
        var s = new DealerState { Seed = 3 };
        var y = BizYard("Gp~1", 2, 1000);
        s.Yards.Add(y);
        y.Today = new DealerBook { Day = 5, Visitors = 12, Sold = 2, Revenue = 30000, Takings = 31000 };
        DealerRules.Hour(s, y, new YardHour(), 7 * 24 + 9, 7, new List<int> { 20000, 20000 }, 5000, BizModels, DealerColours, 1000);
        Check(y.Past[0].Day == 5 && y.Past[0].Sold == 2 && y.Past[0].Revenue == 30000 && y.Past[0].Cars == 2 && y.Today.Day == 7,
            "dealer fix: the first hour after days off closes the last day worked into the reports rather than clearing it");
        DealerRules.Hour(s, y, new YardHour(), 7 * 24 + 10, 7, new List<int> { 20000, 20000 }, 5000, BizModels, DealerColours, 1000);
        Check(y.Past[0].Day == 5 && y.Past[1].Day != 7 && y.Today.Day == 7, "dealer fix: a second hour of the same day closes nothing more");
    }

    private static void DealerFixMoney()
    {
        DealMoney m = DealerRules.Settle(20000, 0, true, 20, 364, 720, 0, 112);
        Check(m.OnFinance && m.Financed > 0 && DealerRules.TakenIn(m, false, 300) == m.Deposit + m.Financed + 300 && DealerRules.TakenIn(m, true, 300) == m.Deposit + 300,
            "dealer fix: finance sold on at once is in the day's takings; finance kept as a plan comes in by the week");
        var s = new DealerState { Seed = 1 };
        for (int i = 0; i < DealerCatalogue.CoversMost - 1; i++)
        {
            s.Covers.Add(new DealerCover { Id = i + 1, Tier = 0, Until = 999 });
        }
        bool room = DealerRules.Tier(true, 1, s) == 1;
        s.Covers.Add(new DealerCover { Id = DealerCatalogue.CoversMost, Tier = 0, Until = 999 });
        int full = DealerRules.Tier(true, 1, s);
        Check(room && full == -1 && DealerRules.Premium(20000, full, 150) == 0 && DealerRules.Tier(false, 1, new DealerState()) == -1,
            "dealer fix: with every warranty kept there is none to sell, and no premium is taken for one");
    }

    private static void DealerFixEvent()
    {
        var y = BizYard("Ev~9", 1, 1000);
        DealerRules.Book(y, DealerCatalogue.OpenWeekend, 0, 5);
        int last = y.EventUntil;
        DealerRules.Book(y, DealerCatalogue.TestDriveDays, 0, last + 3);
        Check(y.Event == DealerCatalogue.TestDriveDays && y.EventLast[DealerCatalogue.OpenWeekend] == last && DealerRules.RestUntil(y, DealerCatalogue.OpenWeekend) == last + DealerCatalogue.EventRest,
            "dealer fix: an event that ended unclosed is closed before the next is booked: its kind rests from its own last day");
    }

    private static void DealerFixSources()
    {
        string director = EconomySource(@"Dealer\DealerDirector.cs"), clock = EconomySource(@"Dealer\DealerDirector.Clock.cs"), actions = EconomySource(@"Dealer\DealerDirector.Actions.cs");
        string business = EconomySource(@"Dealer\DealerDirector.Business.cs"), bank = EconomySource(@"OwnBank\OwnBankDirector.cs");
        Check(director.Contains("DealerRules.TellDue(Now, _toldHour)") && bank.Contains("OwnBankRules.WatchDue(day, _watchTold, city.MonthDays)"), "dealer fix: both notices ask their rule whether they are due");
        string startUp = MemberText(director, "private void StartUp("), book = MemberText(clock, "internal void Book(");
        Check(startUp.Contains("DealerRules.CloseDay(_state, _state.Yards[y], save.Day);") && book.Contains("int day = _working >= 0 ? _working : Save.Day;") && book.Contains("DealerRules.CloseDay(_state, y, day);")
              && MemberText(clock, "private void StepHour(").Contains("_working = day;"),
            "dealer fix: a new market day at the start closes each showroom's day, and money is booked to the day of the hour being worked");
        string close = MemberText(actions, "internal bool Close(");
        Check(close.Contains("DealerRules.Tier(y.Warranty, d.Tier, _state)") && close.Contains("DealerRules.TakenIn(m, planKept, extrasIn)"), "dealer fix: a sale books what it took in, and sells a warranty only where one is kept");
        Check(MemberText(actions, "public bool Relay(").Contains("DropLost(y);") && MemberText(director, "private void SeeTo(").Contains("DropLost(y);"),
            "dealer fix: laying a lot out again takes the cars the save lost off the stock, as walking in does");
        string bookEvent = MemberText(business, "public bool BookEvent(");
        Check(bookEvent.IndexOf("EndEvents(Save.Day);", StringComparison.Ordinal) >= 0 && bookEvent.IndexOf("EndEvents(Save.Day);", StringComparison.Ordinal) < bookEvent.IndexOf("CanBook(", StringComparison.Ordinal),
            "dealer fix: an event over is closed, with its notice, before another is booked");
    }
}
