using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;

// One wage everywhere (a player, 1 Nov 2026: "It is not the same value when you look at the My Employees tab as the one
// that's in Business -> Company -> Staff"). The game charges EmployeeInstance.hourlyWage as it is (EmployeeInstance.PayWage);
// the city's economy books the difference its prices make on every wage of the day (CityAdjust.Of on the day's salaries,
// CityEconomyDirector.Adjust) and the company books each agreed raise for the hours worked (StaffRules.Pay, CompanyDirector.StaffDay).
// The Staff page showed that sum; the game's own screens showed hourlyWage alone. Now every page asks Core/PaidWage, and the
// game's screens are written at it (Core/WageShown, WageShownWatch). Held here: what a page shows is what a day charges.
internal static partial class Program
{
    private static void StaffWageChecks()
    {
        int before = _run, failedBefore = _failed;
        StaffWageSample();
        StaffWageSweep();
        StaffWageEdges();
        StaffWageSources();
        StaffWageTexts();
        Console.WriteLine("  one wage everywhere (shown is charged): " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed");
    }

    // What one day charges for somebody, in cents: the game's own wage line, the city's adjustment of it, and the raise for the hours.
    private static long StaffWageDay(int baseCents, int factor, int raiseCents, int hours)
    {
        float game = baseCents / 100f * hours;   // the game's PayWage: worked hours times hourlyWage
        return (long)baseCents * hours + 100L * CityAdjust.Of(game, factor) + 100L * StaffRules.Pay(raiseCents, hours * 100);
    }

    // The player's case: a wage of 20.00 the game shows, prices up a quarter, a raise of 1.50 agreed, an 8 hour day.
    private static void StaffWageSample()
    {
        const int baseCents = 2000, factor = 1250, raise = 150, hours = 8;
        int level = PriceRules.Level(factor);
        int shown = PaidWage.Cents(baseCents, level, raise);
        Check(level == 1250 && shown == 2650, "one wage: 20.00 at prices of 125% with a raise of 1.50 is shown as 26.50 an hour (" + shown + ")");
        long day = StaffWageDay(baseCents, factor, raise, hours);
        Check(day == 21200 && day == (long)shown * hours, "one wage: the 8 hour day charges 160 by the game, 40 by the city and 12 for the raise, 212, which is the shown wage times 8 (" + day + ")");
        var f = new StaffFile { Base = baseCents, Wage = PaidWage.Cents(baseCents, level, 0), Raise = raise };
        Check(f.Paid == shown && f.Wage - f.Base == PaidWage.City(baseCents, level) && PaidWage.City(baseCents, level) == 500,
            "one wage: the Staff page's parts are the game's wage 20.00, the city's 5.00 and the raise 1.50, and its paid line is the same 26.50");
        Dictionary<string, object> row = CompanyCompanion.Wage("e1", baseCents, level, raise);
        Check(Convert.ToInt64(row["ordinaryHourlyCents"]) == shown && Convert.ToInt64(row["cityHourlyCents"]) == 500,
            "one wage: the companion's wage row is the same figure as the pages");
        Check(PaidWage.Differs(baseCents, shown), "one wage: the game's own 20.00 differs from what is paid, so its screens are written at the paid wage");
    }

    // Many wages, price levels, raises and days: what is shown, times the hours, is what the day charges, within the whole
    // money each part is booked in (the city's line and the raise's line are each rounded to a whole once a day).
    private static void StaffWageSweep()
    {
        int checkedCases = 0, worst = 0;
        string bad = null;
        int[] bases = { 950, 1500, 2000, 2875, 3333, 4210, 9999 };
        int[] factors = { 500, 640, 800, 989, 1011, 1100, 1250, 1733, 2600, 4000 };
        int[] raises = { 0, 25, 150, 333, 900 };
        int[] days = { 1, 4, 6, 8, 12 };
        foreach (int b in bases)
        {
            foreach (int factor in factors)
            {
                foreach (int r in raises)
                {
                    foreach (int h in days)
                    {
                        int shown = PaidWage.Cents(b, PriceRules.Level(factor), r);
                        long gap = Math.Abs(StaffWageDay(b, factor, r, h) - (long)shown * h);
                        checkedCases++;
                        worst = (int)Math.Max(worst, gap);
                        if (gap > 100 + h && bad == null)
                        {
                            bad = b + " at " + factor + " with " + r + " for " + h + "h: " + gap + " cents apart";
                        }
                    }
                }
            }
        }
        Check(bad == null, "one wage: over " + checkedCases + " wages, price levels, raises and days the shown wage times the hours is the day's charge"
                           + " within its rounding (worst " + worst + " cents; " + (bad ?? "none apart") + ")");
    }

    private static void StaffWageEdges()
    {
        Check(PaidWage.Cents(2000, PriceRules.Start, 0) == 2000 && !PaidWage.Differs(2000, PaidWage.Cents(2000, PriceRules.Start, 0)),
            "one wage: at the start's prices with no raise the game's own figure stands, and its screens are left as they are");
        Check(PriceRules.Level(1005) == PriceRules.Start && CityAdjust.Of(160f, 1005) == 0 && PaidWage.Cents(2000, PriceRules.Level(1005), 0) == 2000,
            "one wage: inside the city's quiet band neither the city's line nor the shown wage moves");
        Check(PaidWage.Cents(2000, 800, 0) == 1600 && PaidWage.City(2000, 800) == -400 && StaffWageDay(2000, 800, 0, 8) == 12800,
            "one wage: below the start's prices the city takes off, and the shown 16.00 is what the day charges");
        Check(PaidWage.Cents(0, 1250, 150) == 150 && PaidWage.Cents(-500, 1250, -20) == 0 && PaidWage.Cents(int.MaxValue, 4000, int.MaxValue) == int.MaxValue / 2,
            "one wage: nothing, a negative figure and the largest are neither negative nor overflowed");
        Check(PaidWage.Base(20f) == 2000 && PaidWage.Base(19.999f) == 2000 && PaidWage.Base(0f) == 0 && PaidWage.Base(-3f) == 0,
            "one wage: the game's wage is read in hundredths as the company has always read it");
        Func<string, int> held = PaidWage.Raise;
        try
        {
            PaidWage.Raise = null;
            bool none = PaidWage.RaiseOf("e1") == 0;
            PaidWage.Raise = id => id == "e1" ? 150 : -7;
            Check(none && PaidWage.RaiseOf("e1") == 150 && PaidWage.RaiseOf("e2") == 0 && PaidWage.RaiseOf(null) == 0,
                "one wage: the raise comes from the company when it is wired, and is nothing otherwise (never negative)");
        }
        finally
        {
            PaidWage.Raise = held;
        }
    }

    private static void StaffWageSources()
    {
        string root = WageScripts();
        string Read(string path) => System.IO.File.ReadAllText(System.IO.Path.Combine(root, path));
        string shown = Read(System.IO.Path.Combine("Core", "WageShown.cs")), watch = Read(System.IO.Path.Combine("Core", "WageShownWatch.cs"));
        Check(shown.Contains("PaidWage.Now(e.hourlyWage, e.id)") && shown.Contains("row.hourlyWage = paid / 100f;") && !shown.Contains("e.hourlyWage =")
              && shown.Contains("scroller.Delegate = this;") && shown.Contains("_table.scroller.Delegate = _table;"),
            "one wage: the game's tables are written at the paid wage on the screen's own copy of each row, never the employee's saved wage, and the scroller is given back");
        foreach (string table in new[] { "EmployeeCellView, EmployeeModel", "CandidateCellView, CandidateModel", "HrManagerEmployeeModel>", "ScheduleEmployeeCellView, ScheduleEmployeeModel" })
        {
            Check(shown.Contains(table), "one wage: the game's table of " + table + " is written at the paid wage");
        }
        Check(watch.Contains("WageLabel.EmployeeBox") && watch.Contains("WageLabel.ContactCard") && watch.Contains("WageLabel.PlanAverage")
              && watch.Contains("ReferenceEquals(words, _seen)") && watch.Contains("\"reallife_wage_hire_city\""),
            "one wage: My Employees' box, the Contacts card and an HR plan's average are written when the game writes them, and a candidate's chat tells the price");
        Check(shown.Contains("GoingPublic.Foreign(") && !shown.Contains("Network") && !watch.Contains("Network"),
            "one wage: another player's employee keeps the game's figure, and nothing is sent");
        Check(Read(System.IO.Path.Combine("Economy", "Company", "CompanyGame.cs")).Contains("f.Wage = PaidWage.Cents(f.Base, index, 0);")
              && Read(System.IO.Path.Combine("Economy", "Company", "ManagerGame.cs")).Contains("PaidWage.Cents(PaidWage.Base(e.hourlyWage), index, PaidWage.RaiseOf(e.id))")
              && Read(System.IO.Path.Combine("Economy", "Company", "RotaGame.Read.cs")).Contains("PaidWage.Cents(PaidWage.Base(e.hourlyWage), Prices.Level")
              && Read(System.IO.Path.Combine("Economy", "Services", "ServicesGame.cs")).Contains("m.Wage = PaidWage.Now(e.hourlyWage, e.id);")
              && Read(System.IO.Path.Combine("Economy", "OwnBank", "Branch", "BranchGame.cs")).Contains("PaidWage.Now(e.hourlyWage, e.id)")
              && Read(System.IO.Path.Combine("Tech", "People", "RolesApp.Team.cs")).Contains("TalentGame.PaidCents(person.Game)")
              && Read(System.IO.Path.Combine("Business", "BusinessIssues.Start.cs")).Contains("PaidWage.Cents(issue.A, Prices.Level, 0)"),
            "one wage: the Staff page, managers, the rota, the cleaners, the bank branch, the tech roles and a strike's raise all name the paid wage");
        string staff = Read(System.IO.Path.Combine("Economy", "Company", "CompanyApp.Staff.cs"));
        Check(staff.Contains("\"reallife_wage_row_game\"") && staff.Contains("\"reallife_wage_row_city\"") && staff.Contains("\"reallife_wage_row_paid\"")
              && staff.Contains("CompanyText.Wage(f.Paid)"),
            "one wage: the Staff page names the game's wage, the city's prices and the raise when they differ, and the paid wage");
        // The panel of an employee clicked at their station (Ui/Overlays) names the wage paid and a week's pay at it; no card of the overlays reads the game's raw wage.
        string person = Read(System.IO.Path.Combine("Ui", "Overlays", "OverlayCardsLive.Person.cs"));
        Check(person.Contains("float paid = WageShown.PaidMoney(who);") && person.Contains("UiText.Money(paid)) },") && person.Contains("OverlayRules.Earned(who.workedHoursThisWeek, paid)")
              && person.Contains("k = k * 31 + WageShown.PaidCents(who);"),
            "one wage: the panel of an employee clicked at their station names the wage paid an hour and a week's pay at it, and refreshes when the wage paid changes");
        Check(Read(System.IO.Path.Combine("Ui", "Employees", "EmployeesScreen.View.cs")).Contains("private static float PaidMoney(EmployeeInstance e) => WageShown.PaidMoney(e);")
              && Read(System.IO.Path.Combine("Ui", "Contacts", "ContactsScreen.Read.cs")).Contains("float paid = WageShown.PaidMoney(employee);"),
            "one wage: the employees list, the contact card and the panel at the station all name the one figure, WageShown.PaidMoney");
        // No screen of the mod reads an employee's game wage without asking for what is paid on the same line, but the wage a negotiation's offer, its ask or its terms name.
        string raw = string.Empty;
        foreach (string file in System.IO.Directory.GetFiles(System.IO.Path.Combine(root, "Ui"), "*.cs", System.IO.SearchOption.AllDirectories))
        {
            string[] lines = System.IO.File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                bool reads = System.Text.RegularExpressions.Regex.IsMatch(lines[i], @"\.hourlyWage\b");
                bool asks = lines[i].Contains("PaidWage.") || lines[i].Contains("WageShown.") || System.Text.RegularExpressions.Regex.IsMatch(lines[i], @"\b(offer|ask|n)\.hourlyWage\b");
                raw += reads && !asks ? System.IO.Path.GetFileName(file) + ":" + (i + 1) + " " : string.Empty;
            }
        }
        Check(raw.Length == 0, "one wage: no screen of the mod reads an employee's game wage as it is, apart from a negotiation's offer, ask and terms (" + (raw.Length == 0 ? "none do" : raw.Trim()) + ")");
        Func<int> held = Prices.Factor;
        Func<string, int> raised = PaidWage.Raise;
        try
        {
            Prices.Factor = () => 1250;
            PaidWage.Raise = id => id == "e1" ? 150 : 0;
            float panel = PaidWage.Shown(20f, "e1", true) / 100f, foreign = PaidWage.Shown(20f, "e1", false) / 100f;
            Check(panel == 26.5f && Math.Abs(RealLifeOverhaul.Ui.OverlayRules.Earned(12, panel) - 318f) < 0.01f && foreign == 20f && Math.Abs(RealLifeOverhaul.Ui.OverlayRules.Earned(12, foreign) - 240f) < 0.01f,
                "one wage: the panel's case, 20.00 at prices of 125% with a raise of 1.50, reads 26.50 an hour and 318.00 for 12 hours (the game's own 20.00 and 240.00 only for another player's employee)");
        }
        finally
        {
            Prices.Factor = held;
            PaidWage.Raise = raised;
        }
        string links = Read(System.IO.Path.Combine("Wiring", "CompanyLinks.cs"));
        Check(links.Contains("PaidWage.Raise = RaiseOf;") && links.Contains("PaidWage.Raise = null;")
              && Read(System.IO.Path.Combine("Core", "RealLifeOverhaulMod.cs")).Contains("Guard.Add<WageShown>(_root)"),
            "one wage: the company hands in the raises and takes them back, and the game's screens are watched from the start");
    }

    private static void StaffWageTexts()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("one wage's texts", words, codes))
        {
            return;
        }
        foreach (string key in new[] { "reallife_wage_row_game", "reallife_wage_row_city", "reallife_wage_row_paid", "reallife_wage_hire_city" })
        {
            string lacking = LanguagesLacking(words, codes, key);
            Check(lacking.Length == 0, "one wage's texts: " + key + " is in every language with the English names (" + lacking + ")");
        }
        string hire = words[0].TryGetValue("reallife_wage_hire_city", out string h) ? h : string.Empty;
        Check(hire.Length > 0 && hire.Length <= 240 && hire.Contains("{percent}") && hire.Contains("{ask}") && hire.Contains("{cost}") && hire.Contains("{name}"),
            "one wage's texts: the hiring notice names the asked wage, its cost and the price level, in 240 characters or fewer (" + hire.Length + ")");
        int fixes = Occurs(words[0].TryGetValue("reallife_changes_next", out string english) ? english : string.Empty, FixMarks["en"]);
        string apart = string.Empty;
        for (int l = 0; l < codes.Length; l++)
        {
            string row = words[l].TryGetValue("reallife_changes_next", out string own) ? own : string.Empty;
            apart += fixes > 0 && Occurs(row, FixMarks[codes[l]]) == fixes ? string.Empty : " " + codes[l];
        }
        Check(WhatsNewHas(words[0], "Fixed: the panel of an employee you click at their station now shows the wage paid") && apart.Length == 0,
            "one wage's texts: What's new says that the panel of an employee clicked at their station now shows the wage paid, and the next row has as many fixes in every language as in English (not so in:" + apart + ")");
        for (int l = 0; l < codes.Length; l++)
        {
            string mark = FixMarks.TryGetValue(codes[l], out string m) ? m : "Fixed: ";
            bool found = false;
            foreach (KeyValuePair<string, string> row in words[l])
            {
                found |= row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && row.Value.StartsWith(mark, StringComparison.Ordinal)
                         && (row.Value.Contains("My Employees") || row.Value.Contains("BizMan"));
            }
            Check(found, "one wage's texts: What's new says so in " + codes[l]);
        }
    }
}
