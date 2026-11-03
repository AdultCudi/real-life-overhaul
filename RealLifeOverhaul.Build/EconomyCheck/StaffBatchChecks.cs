using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Ui;

// The review sheet of a batch on My Employees (Core/StaffBatch, Economy/Company/StaffBatchRules, Ui/UiBatch,
// Ui/Employees/StaffBatchHire): the preview's totals against the plan's and against what is given, the share's rounding,
// a hire that fails not stopping the others, the notice's length, the sheet's life, the sources running every change through
// the game's own path, and the texts.
internal static partial class Program
{
    private static void StaffBatches()
    {
        StaffBatchTotals();
        StaffBatchRounding();
        StaffBatchRun();
        StaffBatchSheetLife();
        StaffBatchSources();
        StaffBatchTexts();
    }

    private static List<StaffFile> BatchTeam(int count)
    {
        var list = new List<StaffFile>(count);
        for (int i = 0; i < count; i++)
        {
            StaffFile f = PickedStaff("e" + i, 1450 + i * 37 % 900, 2300, 600 + i % 3 * 100);
            if (i % 7 == 3)
            {
                f.AskRest = 99;   // a raise lately
            }
            if (i % 11 == 5)
            {
                f.PoachDay = 4;   // a rival's offer to answer first
            }
            if (i % 9 == 8)
            {
                f.Wage = 2300;    // paid the going rate
            }
            list.Add(f);
        }
        list.Insert(2, null);   // somebody the company does not know
        return list;
    }

    // The sheet's lines and totals come from the one pass the question box's plan sums, so they cannot differ.
    private static void StaffBatchTotals()
    {
        List<StaffFile> team = BatchTeam(40);
        var names = new List<string>();
        for (int i = 0; i < team.Count; i++)
        {
            names.Add("n" + i);
        }
        foreach (int choice in new[] { 0, 1, 2, 4, 6, 8 })
        {
            var lines = new List<PickedLine>();
            PickedPlan plan = StaffRules.Plan(team, choice, 40, lines);
            PickedPlan plain = StaffRules.Plan(team, choice, 40);
            long raise = 0, now = 0, next = 0, dayNow = 0, dayNext = 0;
            int ready = 0;
            StaffSheet sheet = StaffBatchRules.RaiseSheet(names, names, names, names, lines, plan, "x");
            for (int i = 0; i < lines.Count; i++)
            {
                raise += lines[i].Raise;
                now += lines[i].Now;
                next += lines[i].Now + lines[i].Raise;
                dayNow += sheet.Lines[i].DayNow;
                dayNext += sheet.Lines[i].DayNext;
                ready += lines[i].State == BatchState.Ready ? 1 : 0;
            }
            Check(lines.Count == team.Count && plain.Raise == plan.Raise && plain.After == plan.After && plain.DayAfter == plan.DayAfter,
                "staff batch: the plan with lines is the plan without them (choice " + choice + ")");
            Check(raise == plan.Raise && now == plan.Before && next == plan.After && dayNow == plan.DayBefore && dayNext == plan.DayAfter,
                "staff batch: the lines sum to the plan's totals, pay an hour and a working day (choice " + choice + ")");
            Check(ready == plan.Raised && sheet.Ready == plan.Raised && sheet.Left == plan.Left && sheet.Picked == plan.Picked && ready + sheet.Left == plan.Picked,
                "staff batch: the sheet counts who is reached and who is left out as the plan does (choice " + choice + ")");
            Check(sheet.Now == plan.Before && sheet.Next == plan.After && sheet.DayNow == plan.DayBefore && sheet.DayNext == plan.DayAfter,
                "staff batch: the sheet's totals are the plan's (choice " + choice + ")");
            bool reasons = true;
            for (int i = 0; i < lines.Count; i++)
            {
                reasons &= (lines[i].State == BatchState.Ready) == (lines[i].Raise > 0 && StaffBatchRules.ReasonKey(lines[i].State) == null)
                           && (lines[i].State != BatchState.Ready || StaffBatchRules.ReasonKey(lines[i].State) == null);
            }
            Check(reasons, "staff batch: a person is Ready only with a raise, and everyone else has a reason (choice " + choice + ")");
        }
        // What the sheet showed is what is given: each line's new pay is the file's pay after the raise by the rule of one raise.
        var given = BatchTeam(40);
        var shown = new List<PickedLine>();
        StaffRules.Plan(given, 2, 40, shown);
        bool same = true;
        for (int i = 0; i < given.Count; i++)
        {
            if (given[i] == null)
            {
                continue;
            }
            int before = given[i].Paid;
            if (shown[i].State == BatchState.Ready)
            {
                StaffRules.GivePicked(given[i], StaffRules.PickedRaise(given[i], 2), 40);
            }
            same &= given[i].Paid == shown[i].Now + shown[i].Raise && shown[i].Now == before;
        }
        Check(same, "staff batch: each shown new pay is the pay after the raise is given by the rule of one raise");
    }

    // A share rounds half up to a hundredth of an hour, at least one, as every pay in the economy does (StaffRules.Pay).
    private static void StaffBatchRounding()
    {
        bool rounds = true, onePay = true;
        foreach (int choice in new[] { 1, 2, 3, 4, 5 })
        {
            int percent = StaffRules.PickedPercent(choice);
            for (int wage = 0; wage <= 6000; wage += 7)
            {
                StaffFile f = PickedStaff("r", wage, 9000);
                int raise = StaffRules.PickedRaise(f, choice);
                rounds &= raise == Math.Max(1, (int)Math.Round(wage * percent / 100.0, MidpointRounding.AwayFromZero));
                onePay &= raise == Math.Max(1, StaffRules.Pay(wage, percent * 100));
            }
        }
        Check(rounds, "staff batch: a share of the pay rounds half up to a hundredth, at least one");
        Check(onePay, "staff batch: and it is the very rounding of StaffRules.Pay, the economy's one rule for pay");
        StaffFile asking = PickedStaff("a", 2000, 2400);
        asking.AskDay = 30;
        asking.AskTo = 2400;
        StaffFile single = PickedStaff("a", 2000, 2400);
        single.AskDay = 30;
        single.AskTo = 2400;
        StaffRules.Answer(single, RaiseAnswer.Agree, 31);
        StaffRules.GivePicked(asking, StaffRules.PickedRaise(asking, 0), 31);
        Check(asking.Paid == single.Paid && asking.Raise == single.Raise, "staff batch: up to the going rate gives the one raise's pay, whole");
    }

    // A hire that fails does not stop the others, and the failures are named with their reasons.
    private static void StaffBatchRun()
    {
        var tried = new List<int>();
        BatchOutcome outcome = StaffBatchRules.Run(6, i =>
        {
            tried.Add(i);
            if (i == 1)
            {
                throw new InvalidOperationException("another mod's patch");
            }
            return i == 3 ? BatchState.NoFit : i == 4 ? BatchState.Gone : BatchState.Ready;
        });
        Check(tried.Count == 6 && outcome.Done == 3 && outcome.Failed.Count == 3, "staff batch: every candidate is tried, a throw or a refusal stops nobody");
        Check(outcome.Failed[0] == 1 && outcome.Why[0] == BatchState.Failed && outcome.Failed[1] == 3 && outcome.Why[1] == BatchState.NoFit
              && outcome.Failed[2] == 4 && outcome.Why[2] == BatchState.Gone, "staff batch: each one not hired is named by place with its reason");
        int faults = 0;
        StaffBatchRules.Run(2, i => throw new Exception("x"), (i, e) => faults++);
        Check(faults == 2, "staff batch: a throw is reported to the log once for each");
        Check(StaffBatchRules.Run(0, i => BatchState.Ready).Done == 0, "staff batch: nobody picked is nothing done");
        Check(StaffBatchRules.HireVerdict(false, true, true) == BatchState.Gone && StaffBatchRules.HireVerdict(true, false, true) == BatchState.NotYours
              && StaffBatchRules.HireVerdict(true, true, false) == BatchState.NoFit && StaffBatchRules.HireVerdict(true, true, true) == BatchState.Ready,
            "staff batch: a hire is refused for a candidate gone, a business or person not the player's, or no skill the business needs, in that order");
        bool keys = true;
        foreach (BatchState s in Enum.GetValues(typeof(BatchState)))
        {
            keys &= (s == BatchState.Ready) == (StaffBatchRules.ReasonKey(s) == null);
        }
        Check(keys, "staff batch: every reason has a text, and Ready has none");
        StaffSheet sheet = StaffBatchRules.HireSheet(new[] { "a", "b", "c" }, new[] { "A", "B", "C" }, new[] { "r", "r", "r" }, new[] { 1500, 1800, 2000 },
            new[] { BatchState.Ready, BatchState.NoFit, BatchState.Ready }, "Shop");
        Check(sheet.Hire && sheet.Ready == 2 && sheet.Left == 1 && sheet.Now == 3500 && sheet.Next == 3500 && sheet.Lines[1].State == BatchState.NoFit,
            "staff batch: a hiring sheet counts and sums those who will be hired only");
        // The notice names as many as fit its budget, then how many more, and always at least one.
        var left = new List<string>();
        for (int i = 0; i < 30; i++)
        {
            left.Add("Alexandria Montgomery-Okonkwo " + i + " (No skill the business needs)");
        }
        string listed = StaffBatchRules.Listed(left, "and {count} more", StaffBatchRules.ListBudget);
        Check(listed.Length <= StaffBatchRules.ListBudget + 20 && listed.Contains(left[0]) && listed.Contains(" and ") && listed.EndsWith(" more"),
            "staff batch: a long list is cut to the budget with how many more (" + listed.Length + " characters)");
        Check(StaffBatchRules.Listed(left.GetRange(0, 1), "and {count} more", 10) == left[0] && StaffBatchRules.Listed(new List<string>(), "x", 10) == string.Empty,
            "staff batch: one is always named, none is empty");
        Dictionary<string, string> en = LocaleFile("en");
        string worst = en["reallife_staff_batch_hired_some"].Replace("{count}", "29").Replace("{all}", "30").Replace("{business}", new string('b', 40)).Replace("{list}", new string('l', StaffBatchRules.ListBudget + 20));
        Check(worst.Length <= 240, "staff batch: the longest notice of a hire stays within 240 characters (" + worst.Length + ")");
        worst = en["reallife_staff_batch_hired_none"].Replace("{business}", new string('b', 40)).Replace("{list}", new string('l', StaffBatchRules.ListBudget + 20));
        Check(worst.Length <= 240 && Fits(en, "reallife_staff_batch_hired"), "staff batch: and the notice when nobody is hired");
    }

    // The sheet stands alone, is given once, and goes when put away.
    private static void StaffBatchSheetLife()
    {
        StaffBatch.Close();
        int gave = 0, put = 0;
        var sheet = new StaffSheet { Ready = 2, Give = () => gave++, Cancel = () => put++ };
        StaffBatch.Open(sheet);
        Check(StaffBatch.Sheet == sheet, "staff batch: the sheet waits for a yes");
        Check(StaffBatch.Give() && gave == 1 && StaffBatch.Sheet == null && !StaffBatch.Give() && gave == 1, "staff batch: a yes gives once, a second press gives nothing");
        StaffBatch.Open(sheet);
        Check(StaffBatch.Cancel() && put == 1 && gave == 1 && StaffBatch.Sheet == null && !StaffBatch.Cancel(), "staff batch: Cancel gives nothing and puts the sheet away");
        StaffBatch.Open(new StaffSheet { Ready = 0, Give = () => gave++ });
        Check(!StaffBatch.Give() && gave == 1 && StaffBatch.Sheet != null, "staff batch: with nobody reached there is nothing to give, and the sheet stays");
        Check(StaffBatch.Close() && StaffBatch.Sheet == null && !StaffBatch.Close(), "staff batch: closing takes the sheet away");
        StaffBatch.KitShows = () => throw new Exception("x");
        Check(!StaffBatch.Wanted, "staff batch: a fault in asking whether the kit shows is not the kit");
        StaffBatch.KitShows = () => true;
        Check(StaffBatch.Wanted, "staff batch: the sheet is wanted while the kit draws My Employees");
        StaffBatch.KitShows = null;
        Check(!StaffBatch.Wanted, "staff batch: without the kit the game's own question box is used");
    }

    // Every change goes through the game's own path; nothing is written another way, nothing runs by itself.
    private static void StaffBatchSources()
    {
        string hire = ModSource(@"Ui\Employees\StaffBatchHire.cs");
        string raise = ModSource(@"Economy\Company\PickedRaiseGame.cs");
        string director = ModSource(@"Economy\Company\CompanyDirector.PickedRaises.cs");
        string screen = ModSource(@"Ui\Employees\EmployeesScreen.Batch.cs");
        string main = ModSource(@"Ui\Employees\EmployeesScreen.cs");
        Check(hire.Length > 0 && raise.Length > 0 && screen.Length > 0 && main.Length > 0, "staff batch sources: the files are there");
        Check(hire.Contains("EmployeeHelper.HireCandidate(e)") && hire.Contains("RemoveCandidate(e)") && hire.Contains("StaffBatchRules.Run(") && hire.Contains("OnMassActionPerformed(true)"),
            "staff batch sources: each hire is the game's own, then its table's RemoveCandidate, every candidate tried through the run, and the game's own end of an action");
        Check(hire.Contains("GoingPublic.Mine(business)") && hire.Contains("GetPlayerBuildingRegistrations(PlayerBuildingFilter)") && !hire.Contains("hourlyWage ="),
            "staff batch sources: only the player's own businesses, the game's own list of them, and no wage written");
        Check(raise.Contains("StaffBatch.Open(sheet)") && raise.Contains("sheet.Give = () => Give(ids, choice)") && raise.Contains("company.GivePicked(ids, choice, out sum)"),
            "staff batch sources: the raise on the sheet is the very Give the question box makes, which is the director's GivePicked");
        Check(director.Contains("StaffRules.GivePicked(f, raise, day)") && !screen.Contains("GivePicked") && !main.Contains("GivePicked") && !hire.Contains("GivePicked"),
            "staff batch sources: only the director gives a raise, by the rule of one raise; the screen never writes a wage");
        Check(raise.Contains("GameConfirm.Show(") && raise.Contains("StaffBatch.Wanted"), "staff batch sources: without the kit the game's own question box still asks");
        Check(main.Contains("StaffBatch.Close()") && main.Contains("StaffBatch.Cancel()") && screen.Contains("MassPanelOpen()"),
            "staff batch sources: the sheet goes with the picking, the tab and the game's panel of choices, and is put away on Back");
        Check(!hire.Contains("void Update(") && !screen.Contains("void Update(") && !hire.Contains("FindObjects") && !hire.Contains("IEnumerator"),
            "staff batch sources: nothing runs by itself, nothing searches the scene");
        Check(!hire.Contains("Harmony") && !screen.Contains("Harmony") && !hire.Contains("Sync") && !raise.Contains("Sync"), "staff batch sources: no patch, nothing sent to other players");
    }

    private static void StaffBatchTexts()
    {
        string[] keys =
        {
            "reallife_staff_batch_raise_title", "reallife_staff_batch_raise_lead", "reallife_staff_batch_hire_title", "reallife_staff_batch_hire_lead", "reallife_staff_batch_hire_give",
            "reallife_staff_batch_hired_to", "reallife_staff_batch_now", "reallife_staff_batch_after", "reallife_staff_batch_day", "reallife_staff_batch_result", "reallife_staff_batch_total",
            "reallife_staff_batch_result_raise", "reallife_staff_batch_result_hire", "reallife_staff_batch_why_rested", "reallife_staff_batch_why_waiting", "reallife_staff_batch_why_at_rate",
            "reallife_staff_batch_why_unknown", "reallife_staff_batch_why_no_fit", "reallife_staff_batch_why_not_yours", "reallife_staff_batch_why_gone", "reallife_staff_batch_why_failed",
            "reallife_staff_batch_hired", "reallife_staff_batch_hired_some", "reallife_staff_batch_hired_none", "reallife_staff_batch_more"
        };
        Check(AllIn(keys, out string missing), "staff batch texts: every key in en, pt and zh-cn (" + missing + ")");
        bool reasons = true;
        foreach (BatchState s in Enum.GetValues(typeof(BatchState)))
        {
            string key = StaffBatchRules.ReasonKey(s);
            reasons &= key == null || Array.IndexOf(keys, key) >= 0;
        }
        Check(reasons, "staff batch texts: every reason the rules name is a text");
        string found = null;
        foreach (string code in ModLanguages.All)
        {
            Dictionary<string, string> texts = LocaleFile(code);
            if (!texts.TryGetValue("reallife_changes_next", out string next) || !(next.Contains("My Employees") || next.Contains("Meus Funcionários") || next.Contains("我的员工")
                || (texts.TryGetValue("reallife_ui_staff_app", out string app) && next.Contains(app))))
            {
                found = found ?? code;
            }
        }
        Check(found == null, "staff batch texts: the What's new line is in the next row of all eleven languages" + (found == null ? string.Empty : " (not in " + found + ")"));
    }
}
