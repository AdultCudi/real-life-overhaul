using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Tech;

// Wages the mod raises (Scripts/Core/WageRules.cs): an academy's six staff were paid more every day,
// past 8,000 a day, after changing the cleaning; earlier, wages over 2,000 an hour where the game pays 29 to 35. A rise
// of the Tech people part was a percentage of the wage already raised, with no cap and no limit on how often
// (TalentGame.Rise, the Open Roles app's rise buttons). Now: at most one rise or promotion in WageRules.PeriodDays for
// one person, never past WageRules.CapBp of the game's own wage for the skill, and a save's runaway wages mended once on load.
internal static partial class Program
{
    private const int WageFormula = 3000;   // cents an hour: the game's own wage for an HR manager of middling skill, as the player's academy staff are paid

    private static void Wages()
    {
        WageCreep();
        WageCap();
        WagePeriod();
        WageMend();
        WageSources();
        WageWords();
    }

    // The rule a rise followed before the fix (TalentGame.Rise: a percentage of the wage as it stands, no cap).
    private static int RiseBefore(int wage, int bp)
    {
        return wage + (int)((long)wage * bp / 10000);
    }

    // Thirty days of an academy's HR manager, the cleaning changed on day 5: the workplace's comfort goes and the game's
    // satisfaction falls each hour (its clean-workplace demand unmet: the game's UpdateSatisfaction moves it at most 0.6 an hour), morale
    // follows the people part's own rules (TalentRules.Mood, GameMood), and the player answers falling satisfaction the
    // only way the Roles app moves it, with the 8% rise, whenever it is offered. Returns the wage at the end; the rises given.
    private static int WageMonth(bool after, out int rises, out int closest)
    {
        Profile p = Person(25, TalentCatalogue.Mid, 50, 50, 50);
        int wage = WageFormula, going = TalentRules.Going(WageFormula, TalentCatalogue.Role(25), 1000, 0);
        float satisfaction = 80f;
        int last = -1000;
        rises = 0;
        closest = int.MaxValue;
        for (int day = 1; day <= 30; day++)
        {
            int comfort = day < 5 ? 60 : 0;   // (the cleaning changed on day 5: the place is dirtier from then on)
            var d = new WorkDay { Day = day, Year = 120, Hours = 8, WeekHours = 40, MainPoints = 50, Paid = wage, Going = going, Esteem = 0, Wanted = true, Comfort = comfort };
            TalentRules.Mood(p, d);
            satisfaction += TalentRules.GameMood(p, satisfaction) - (day >= 5 ? 24 * 0.6f : 0f);
            satisfaction = Math.Max(1f, Math.Min(100f, satisfaction));
            if (satisfaction >= 60f)
            {
                continue;
            }
            if (!after)
            {
                wage = RiseBefore(wage, 800);
            }
            else if (TalentRules.CanRise(p, day))
            {
                wage = WageRules.Rise(wage, 800, WageFormula);
            }
            else
            {
                continue;
            }
            closest = Math.Min(closest, day - last);
            last = day;
            rises++;
            TalentRules.Raised(p, day, 800);
            satisfaction = Math.Min(100f, satisfaction + 2f);
        }
        return wage;
    }

    private static void WageCreep()
    {
        int cap = WageRules.Cap(WageFormula);
        int before = WageMonth(false, out int risesBefore, out int closestBefore);
        Check(before > cap && risesBefore >= 20 && closestBefore == 1,
            "wages, the report reproduced: with the rule before the fix, 30 days with the cleaning changed on day 5 raise a wage of " + WageFormula / 100 + " an hour " + risesBefore
            + " times, day after day, to " + before / 100 + " (" + before * 8 * 6 / 100 + " a day for six staff at eight hours), past four times the game's own (" + cap / 100 + ")");
        int fixedWage = WageMonth(true, out int risesAfter, out int closestAfter);
        Check(fixedWage <= cap && risesAfter <= 30 / WageRules.PeriodDays + 1 && closestAfter >= WageRules.PeriodDays,
            "wages: with the fix the same 30 days give " + risesAfter + " rise(s), never two within " + WageRules.PeriodDays + " days (closest " + closestAfter + "), and end at " + fixedWage / 100f
            + " an hour, within four times the game's own (" + cap / 100 + ")");
        int year = WageFormula;
        for (int day = 0; day < 365; day += WageRules.PeriodDays)
        {
            year = WageRules.Rise(year, 800, WageFormula);
        }
        Check(year == cap, "wages: an 8% rise every time it is offered for a whole year stops at the cap (" + year / 100f + " against " + cap / 100f + ")");
        Check(WageMonth(true, out int again, out _) == fixedWage && again == risesAfter, "wages: the month is the same each time it is run (" + again + " rises)");
    }

    private static void WageCap()
    {
        int cap = WageRules.Cap(WageFormula);
        Check(cap == WageFormula * 4 && WageRules.CapBp == 40000, "wages: the cap is four times the game's own wage for the skill (" + cap + " against " + WageFormula + ")");
        Check(WageRules.Rise(3000, 800, WageFormula) == 3240, "wages: an 8% rise on 30.00 is 32.40");
        Check(WageRules.Rise(11500, 800, WageFormula) == cap, "wages: a rise that would pass the cap stops at it");
        Check(WageRules.Rise(cap, 800, WageFormula) == cap, "wages: a wage at the cap is not raised");
        Check(WageRules.Rise(200000, 800, WageFormula) == 200000, "wages: a wage already over the cap is neither raised nor lowered by a rise");
        Check(WageRules.Rise(3000, 800, 0) == 3000 && WageRules.Rise(3000, 0, WageFormula) == 3000 && WageRules.Rise(3000, -500, WageFormula) == 3000,
            "wages: no formula, no rise or a negative one leaves the wage as it is");
        Check(WageRules.UpTo(3000, 2850, WageFormula) == 3000 && WageRules.UpTo(2000, 2850, WageFormula) == 2850, "wages: a strike's raise to a figure never lowers a wage");
        Check(WageRules.UpTo(3000, 900000, WageFormula) == cap, "wages: a hire at a wage agreed past the cap is hired at the cap");
        Check(WageRules.UpTo(3000, 900000, 0) == 3000, "wages: with no formula to measure by, a wage is not raised to a figure");
        Check(WageRules.Cap(0) == 0 && WageRules.Cap(-5) == 0 && WageRules.Cap(int.MaxValue) == int.MaxValue, "wages: the cap of nothing is nothing, and the largest is not overflowed");
        int going = TalentRules.Going(WageFormula, TalentCatalogue.Role(14), 1500, 1500);
        Check(going < cap, "wages: a tech chief with every certificate in the scarcest market is offered " + going + ", under the cap of " + cap + " (no hire of the people part's own is cut)");
    }

    private static void WagePeriod()
    {
        Check(WageRules.Due(-1000, 1) && !WageRules.Due(10, 23) && WageRules.Due(10, 24) && WageRules.NextDay(10) == 24, "wages: one rise in " + WageRules.PeriodDays + " days: day 10's next is day 24");
        Profile p = Person(25, TalentCatalogue.Mid, 50, 50, 50);
        Check(TalentRules.CanRise(p, 1), "wages: somebody never raised may be given a rise");
        TalentRules.Raised(p, 30, 300);
        Check(!TalentRules.CanRise(p, 30) && !TalentRules.CanRise(p, 43) && TalentRules.CanRise(p, 44), "wages: after a rise on day 30, none before day 44");
        Profile asked = Person(25, TalentCatalogue.Mid, 50, 50, 50);
        asked.RaiseAsk = 600;
        asked.AskEnd = 40;
        TalentRules.Raised(asked, 35, 300);
        Check(asked.RaiseAsk == 0 && asked.AskEnd == 0, "wages: any rise settles a review's request, so it never lapses into a refusal while the next has to wait");
        int asks = 0;
        for (int n = 0; n < 200; n++)
        {
            Profile r = Person(25, TalentCatalogue.Mid, 60, 60, 60);
            r.Id = "w" + n;
            r.Morale = 1000;
            r.RecognisedDay = 95;
            TalentRules.Review(r, 3, 100, 120, 60);
            asks += r.RaiseAsk > 0 ? 1 : 0;
        }
        Check(asks == 0, "wages: a review within the period of a rise asks for none (" + asks + " of 200 asked)");
        TalentRules.Promote(p, 50);
        Check(!TalentRules.CanRise(p, 51), "wages: a promotion counts as the period's rise");
        var f = new StaffFile { Id = "c", Wage = 2000, Going = 3000, AskRest = 60 };
        Check(!StaffRules.CanGive(f, 59) && StaffRules.CanGive(f, 60), "wages: the company's unasked raise waits for the rest after the last answer");
        f.Raise = 1000;
        Check(!StaffRules.CanGive(f, 90), "wages: nobody paid the going rate is given an unasked raise");
    }

    // The load-time repair: a wage the mod raised past the cap comes back to the cap, once; the rest are left alone.
    private static void WageMend()
    {
        int cap = WageRules.Cap(WageFormula);
        Check(WageRules.Mended(200000, WageFormula) == cap, "wages: a runaway wage of 2,000 an hour is brought back to " + cap / 100 + " on load");
        Check(WageRules.Mended(cap + 1, WageFormula) == cap && WageRules.Mended(cap, WageFormula) == cap && WageRules.Mended(9000, WageFormula) == 9000,
            "wages: only a wage past the cap is changed; one at or under it is left as it is");
        Check(WageRules.Mended(200000, 0) == 200000, "wages: with no formula to measure by, nothing is lowered");
        Check(WageRules.Mended(WageRules.Mended(200000, WageFormula), WageFormula) == cap, "wages: the repair is the same on a second load (once mended, nothing more)");
        int before = WageMonth(false, out _, out _);
        Check(WageRules.Mended(before, WageFormula) == cap, "wages: the month's runaway wage is mended to the cap");
    }

    private static string WageScripts()
    {
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
    }

    // Every write of a game wage in the mod goes through WageRules, the cleaning service writes none, the rise buttons stand
    // behind the period, and the repair runs as the people part starts on a save.
    private static void WageSources()
    {
        string root = WageScripts();
        var writes = new List<string>();
        bool bounded = true;
        foreach (string file in System.IO.Directory.GetFiles(root, "*.cs", System.IO.SearchOption.AllDirectories))
        {
            string[] lines = System.IO.File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int at = line.IndexOf("hourlyWage =", StringComparison.Ordinal);
                if (at < 0 || line.TrimStart().StartsWith("//", StringComparison.Ordinal) || line.IndexOf("hourlyWage ==", StringComparison.Ordinal) >= 0)
                {
                    continue;
                }
                string name = System.IO.Path.GetFileName(file);
                writes.Add(name + ":" + (i + 1));
                bool fromRules = line.Contains("WageRules.") || line.Contains("after / 100f");
                bounded &= fromRules && (name == "TalentGame.cs" || name == "BusinessGame.cs" || name == "HiringGame.cs");
            }
        }
        Check(writes.Count > 0 && bounded, "wages: every write of a game wage in the mod is bounded by WageRules (" + string.Join(", ", writes) + ")");
        bool cleaning = true;
        foreach (string file in System.IO.Directory.GetFiles(System.IO.Path.Combine(root, "Economy", "Services"), "*.cs"))
        {
            string text = System.IO.File.ReadAllText(file);
            cleaning &= text.IndexOf("hourlyWage =", StringComparison.Ordinal) < 0 && text.IndexOf("SetWage(", StringComparison.Ordinal) < 0 && text.IndexOf("Rise(", StringComparison.Ordinal) < 0;
        }
        Check(cleaning, "wages: the cleaning service and its settings raise no wage");
        string roles = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "Tech", "People", "RolesApp.Team.cs"));
        int gate = roles.IndexOf("TalentRules.CanRise(p, day)", StringComparison.Ordinal), button = roles.IndexOf("reallife_roles_button_rise_8", StringComparison.Ordinal);
        Check(gate > 0 && button > gate && button - gate < 900, "wages: the Roles app offers the rise buttons only when a rise is due");
        string people = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "Tech", "TechDirector.People.cs"));
        int start = people.IndexOf("private void StartPeople()", StringComparison.Ordinal), mend = people.IndexOf("MendWages();", StringComparison.Ordinal);
        Check(start > 0 && mend > start && mend - start < 1200 && people.Contains("Rested(person)") && people.Contains("WageRules.Rise(p.Wage"),
            "wages: the save's runaway wages are mended as the people part starts, a rise and a promotion wait for the period, and the mod's own payroll is bounded too");
    }

    private static readonly string[] WageKeys = { "reallife_wage_rested", "reallife_wage_rested_line", "reallife_wage_mended" };

    private static void WageWords()
    {
        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("wages", langs, codes))
        {
            return;
        }
        var missing = new List<string>();
        foreach (string key in WageKeys)
        {
            string lacking = LanguagesLacking(langs, codes, key);
            if (lacking.Length > 0)
            {
                missing.Add(key + " (" + lacking + ")");
            }
        }
        Check(missing.Count == 0, "wages: every text is in every language with the same {names} (" + string.Join(", ", missing) + ")");
        Dictionary<string, string> en = langs[0];
        string mended = en.TryGetValue("reallife_wage_mended", out string m) ? m.Replace("{count}", "600").Replace("{times}", "4") : string.Empty;
        Check(mended.Length > 0 && mended.Length <= 240, "wages: the repair's notice is at most 240 characters (" + mended.Length + ")");
        // In the next row until its version is made, and in that version's own row after (the release empties the next row): every row is read.
        bool line = false;
        foreach (KeyValuePair<string, string> row in en)
        {
            if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && row.Value.Contains("Fixed: wages could climb without end"))
            {
                line = true;
                break;
            }
        }
        Check(line, "wages: What's new has the fix's line, starting \"Fixed: \", in one of its rows");
    }
}
