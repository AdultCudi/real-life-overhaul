using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Ui;

// Four small faults found by playing 1.0.32 (27 Nov 2026), each proved here:
//   1. All apps: the wheel and the layout let the list scroll until one app was left at the top and the panel empty below it. The furthest scroll is
//      now RowCount - Fit (the rows the panel holds), so the last app stands at the bottom, at both screen sizes, and a window that shrinks pulls it back;
//   2. a supplier contract's call card wrote the game's district key (ba:neighborhood_garmentdistrict) under the business: it now writes the district in words;
//   3. the recruitment confirm line read "within 1 days": a text of its own for one day (and one candidate);
//   4. a candidate's asking wage in the lists was the stored wage (17.23) where the negotiation opens at the next 5 cents up (17.25): the lists show 17.25.
// Run alone: --ui33.
internal static partial class Program
{
    private static void Ui33()
    {
        Ui33AllApps();
        Ui33District();
        Ui33Days();
        Ui33Wage();
    }

    // ---------------------------------------------------------------- 1. All apps' scroll end

    private static void Ui33AllApps()
    {
        foreach (float[] size in new[] { new[] { 1920f, 1080f }, new[] { 1280f, 720f } })
        {
            string what = "all apps scroll: " + size[0] + " x " + size[1];
            var frame = new UiFrame { Width = size[0], Height = size[1], Band = UiFrame.BandHeight };
            foreach (int first in new[] { 0, 3, 7, 14, 99 })
            {
                var m = new UiAllAppsModel { Title = "All apps", Mode = UiAllApps.Folder, EditLabel = "Edit the bar", Empty = "None", RowCount = 15, First = first };
                m.Rows = new UiAllAppsRow[15];
                for (int i = 0; i < 15; i++)
                {
                    m.Rows[i] = new UiAllAppsRow { Icon = UiIcon.Grid, Title = "App " + i, Status = i % 2 == 0 ? "A line" : null };
                }
                var s = new UiScene();
                UiAllApps.Lay(s, m, frame, true, UiDefaultKeys.Keyboard);
                UiList list = s.Lists[0];
                int most = Math.Max(0, 15 - m.Fit);
                Check(m.Fit >= 3 && m.First == Math.Min(first, most) && list.First == m.First && list.First + list.Shown == Math.Min(15, m.First + m.Fit),
                    what + ", first " + first + " asked: the list begins at " + m.First + " (the furthest is " + most + ", the panel holds " + m.Fit + ") and shows " + list.Shown + " rows");
                Check(first < most || list.First + list.Shown == 15 && list.Shown == m.Fit, what + ", first " + first + ": at the end the last app is the last row of a full panel");
            }
        }

        string screen = ModSource(@"Ui\Shell\AllAppsScreen.cs"), layout = ModSource(@"Ui\UiAllApps.cs");
        Check(MemberText(screen, "public void Wheel(int clip, float notches)").Contains("_m.RowCount - Math.Max(1, _m.Fit)")
              && !MemberText(screen, "public void Wheel(int clip, float notches)").Contains("RowCount - 1"),
            "all apps scroll: the wheel stops at RowCount - Fit, never at the last row alone");
        Check(layout.Contains("m.First = Math.Max(0, Math.Min(m.First, m.RowCount - room));") && screen.Contains("_m.RowCount - Math.Max(1, _m.Fit)"),
            "all apps scroll: the layout and the model's build both pull a first row back that lies past the end");
        Check(!MemberText(screen, "public void Reveal(int list, int index)").Contains("_m.First = index;"),
            "all apps scroll: focus reaching a row below the panel brings it in at the bottom, never to the top");
    }

    // ---------------------------------------------------------------- 2. a district in words

    private static void Ui33District()
    {
        string contracts = ModSource(@"Ui\Dialog\DialogForm.Contracts.cs"), regs = ModSource(@"Core\GameRegistrations.cs");
        Check(MemberText(contracts, "private static string Where(BuildingRegistration reg)").Contains("GameRegistrations.DistrictWords(GameRegistrations.District(reg))"),
            "district in words: the supplier call card's line under the business (kind, district) names the district through the game's text");
        Check(MemberText(contracts, "private static string Warehouse()").Contains("GameRegistrations.DistrictWords(GameRegistrations.District(reg))"),
            "district in words: the warehouse the import card names is given its district in words too");
        Check(ModSource(@"Core\HeldUnitsGame.cs").Contains("GameRegistrations.DistrictWords(b.Neighbourhood)"),
            "district in words: the held units' offered list names a district in words");
        Check(MemberText(regs, "public static string DistrictWords(string district)").Contains("LocalizorManager.IsLocalizedKey(district) ? district.GetLocalization() : district"),
            "district in words: one helper turns a ba:neighborhood_ key into the game's text, and leaves a name that is no key as it is");
    }

    // ---------------------------------------------------------------- 3. "1 days"

    private static void Ui33Days()
    {
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(LocalesFolder(), "en.json"));
        string many = en["reallife_ui_dialog_cost_candidates"], day = en["reallife_ui_dialog_cost_candidates_day"], one = en["reallife_ui_dialog_cost_candidate"], oneDay = en["reallife_ui_dialog_cost_candidate_day"];
        Check(many.Contains("{count}") && many.Contains("{days} days") && day.Contains("{count}") && day.EndsWith("within 1 day", StringComparison.Ordinal) && !day.Contains("{days}")
              && one.Contains("{days} days") && one.Contains("1 candidate ") && !one.Contains("{count}") && oneDay == "Up to 1 candidate within 1 day",
            "one day: the recruitment line has a text for one day, for one candidate and for both (\"" + day + "\")");
        string costs = ModSource(@"Ui\Dialog\DialogForm.Costs.cs");
        string line = MemberText(costs, "internal static string CandidatesLine(int count, int days)");
        Check(costs.Contains("CandidatesLine(Mathf.RoundToInt(recruitment.candidatesAmountSlider.value), Mathf.RoundToInt(recruitment.deadlineSlider.value))")
              && line.Contains("(days == 1 ? \"reallife_ui_dialog_cost_candidate_day\" : \"reallife_ui_dialog_cost_candidate\")") && line.Contains("(days == 1 ? \"reallife_ui_dialog_cost_candidates_day\" : \"reallife_ui_dialog_cost_candidates\")") && line.Contains("count == 1"),
            "one day: the line is chosen where it is filled, by the count and the days");
    }

    // ---------------------------------------------------------------- 4. the asking wage

    private static void Ui33Wage()
    {
        // The game's offer (CandidateSalaryNegotiation.SendOffer: Ceil(wage x 20) / 20): 17.23 is asked as 17.25, a wage already on a step stays.
        Check(PaidWage.Ask(17.23f) == 1725 && PaidWage.Ask(17.25f) == 1725 && PaidWage.Ask(17.21f) == 1725 && PaidWage.Ask(17.26f) == 1730 && PaidWage.Ask(20f) == 2000 && PaidWage.Ask(0f) == 0 && PaidWage.Ask(-3f) == 0,
            "asking wage: a candidate asks the wage rounded up to 5 cents, as the game's negotiation opens (17.23 asks " + PaidWage.Ask(17.23f) + ", 17.25 stays " + PaidWage.Ask(17.25f) + ")");
        Func<int> held = Prices.Factor;
        Func<string, int> raise = PaidWage.Raise;
        try
        {
            Prices.Factor = () => 1000;
            PaidWage.Raise = null;
            Check(PaidWage.Shown(17.23f, "c", true, true) == 1725 && PaidWage.Shown(17.23f, "c", true) == 1723 && PaidWage.Now(17.23f, "e", false) == 1723,
                "asking wage: at the start's prices a candidate shows 17.25 and an employee hired at 17.23 shows 17.23");
            Check(PaidWage.Shown(17.23f, "c", false, true) == 1723, "asking wage: another player's candidate keeps the game's figure (Going Public)");
            Prices.Factor = () => 1250;
            Check(PaidWage.Now(17.23f, "c", true) == PaidWage.Cents(1725, 1250, 0), "asking wage: the city's prices go on the asked wage, not on the stored one");
        }
        finally
        {
            Prices.Factor = held;
            PaidWage.Raise = raise;
        }
        string shown = ModSource(@"Core\WageShown.cs"), watch = ModSource(@"Core\WageShownWatch.cs");
        // The kit shows the asked wage and its own Hire pays it (the negotiation's AcceptOffer sets the wage to the offer's); the game's own Hire pays the
        // stored wage, so the game's own table (WageTable) and its box show that.
        Check(shown.Contains("PaidWage.Shown(e.hourlyWage, e.id, Ours(e), e.candidateInfo != null)") && MemberText(shown, "internal static bool Paid(").Contains("negotiating && e.candidateInfo != null"),
            "asking wage: the kit's lists and card take the asked wage for a candidate, the game's contact card in a negotiation too");
        Check(MemberText(shown, "private void Write(int index)").Contains("PaidWage.Now(e.hourlyWage, e.id);") && !MemberText(shown, "private void Write(int index)").Contains("candidateInfo")
              && MemberText(watch, "private void Box()").Contains("WageShown.Paid(app.SelectedEmployeeInstance, out _, out int paid)") && watch.Contains("WageShown.Paid(e, out _, out int paid, true)"),
            "asking wage: the game's own candidate table and box show the stored wage, which the game's Hire pays");
        string hire = ModSource(@"Core\AskedHire.cs");
        Check(hire.Contains("e.candidateInfo == null && asked > e.hourlyWage && WageShown.Ours(e)") && hire.Contains("e.hourlyWage = asked;")
              && ModSource(@"Ui\Employees\EmployeesScreen.cs").Contains("AskedHire.After(hiring, asked);")
              && ModSource(@"Ui\Employees\StaffBatchHire.cs").Contains("AskedHire.After(e, asked);"),
            "asking wage: the kit's Hire and its review's hire pay the asked wage, after the game's hire, for a person this machine pays");
        Check(watch.Contains("(WageShown.AskCents(e) / 100f).ToCurrencyFormat()"), "asking wage: the notice that tells the price of a hire names the asked wage");
    }
}
