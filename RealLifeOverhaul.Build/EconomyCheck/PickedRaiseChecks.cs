using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using RealLifeOverhaul.Economy;

// A raise for several employees picked in the game's MyEmployees app (Economy/Company: StaffRules' picked raise,
// CompanyDirector.PickedRaises, PickedRaiseGame): the choices, who it reaches and who is left out, that each one is a raise
// as one raise is, the preview against what is given, the action in the game's own list, and the texts.
internal static partial class Program
{
    // The three asks of one player's post, checked together: each part's own checks are in their files.
    private static void StaffCentres()
    {
        PickedRaises();
    }

    private static void PickedRaises()
    {
        PickedRaiseChoices();
        PickedRaiseWho();
        PickedRaiseAsOne();
        PickedRaisePlan();
        PickedRaiseSources();
        PickedRaiseTexts();
    }

    private static StaffFile PickedStaff(string id, int wage, int going, int hours = 800)
    {
        return new StaffFile { Id = id, Place = "1 Main St", Wage = wage, Going = going, DayHours = hours, Name = id };
    }

    private static void PickedRaiseChoices()
    {
        Check(StaffRules.PickedChoices == 1 + CompanyCatalogue.PickedRaisePercents.Length + CompanyCatalogue.PickedRaiseAmounts.Length,
            "picked raise: the choices are the going rate, then each share, then each amount");
        StaffFile f = PickedStaff("a", 2000, 2300);
        Check(StaffRules.PickedRaise(f, 0) == 300, "picked raise: up to the going rate gives the gap");
        Check(StaffRules.PickedRaise(PickedStaff("b", 2400, 2300), 0) == 0, "picked raise: up to the going rate gives nothing to somebody paid over it");
        Check(StaffRules.PickedPercent(2) == 5 && StaffRules.PickedRaise(f, 2) == 100, "picked raise: five per cent of 20.00 an hour is 1.00");
        f.Raise = 50;
        Check(StaffRules.PickedRaise(f, 1) == 41, "picked raise: a share is of what they are paid with the agreed raise, rounded (2% of 20.50)");
        Check(StaffRules.PickedRaise(PickedStaff("c", 10, 0), 1) == 1, "picked raise: a share is at least a cent");
        int firstAmount = 1 + CompanyCatalogue.PickedRaisePercents.Length;
        Check(StaffRules.PickedAmount(firstAmount) == CompanyCatalogue.PickedRaiseAmounts[0] && StaffRules.PickedRaise(f, firstAmount) == CompanyCatalogue.PickedRaiseAmounts[0],
            "picked raise: an amount is the same for each");
        Check(StaffRules.PickedRaise(f, StaffRules.PickedChoices) == 0 && StaffRules.PickedRaise(f, -1) == 0, "picked raise: a choice there is not gives nothing");
        Check(StaffRules.PickedPercent(0) == 0 && StaffRules.PickedAmount(1) == 0, "picked raise: a choice is a share or an amount, never both");
    }

    private static void PickedRaiseWho()
    {
        StaffFile rested = PickedStaff("r", 2000, 2300);
        rested.AskRest = 20;
        Check(!StaffRules.CanPick(rested, 19) && StaffRules.CanPick(rested, 20), "picked raise: one raise in the rest's time, as for one raise");
        StaffFile asking = PickedStaff("q", 2000, 2300);
        asking.AskRest = 50;
        asking.AskDay = 10;
        asking.AskTo = 2300;
        Check(StaffRules.CanPick(asking, 12), "picked raise: a request waiting is answered by it, whatever the rest");
        StaffFile offer = PickedStaff("o", 2000, 2300);
        offer.PoachDay = 5;
        StaffFile leaving = PickedStaff("l", 2000, 2300);
        leaving.LeaveDay = 30;
        Check(!StaffRules.CanPick(offer, 12) && !StaffRules.CanPick(leaving, 12), "picked raise: a rival's offer or a departure is answered on its own page");
    }

    private static void PickedRaiseAsOne()
    {
        // Up to the going rate for somebody underpaid: the same as the Staff tab's own raise (GiveRaise: Answer, agreed).
        StaffFile one = PickedStaff("x", 2000, 2300), picked = PickedStaff("x", 2000, 2300);
        one.Standing = picked.Standing = 50;
        one.AskDay = 30;
        one.AskTo = one.Going;
        StaffRules.Answer(one, RaiseAnswer.Agree, 30);
        StaffRules.GivePicked(picked, StaffRules.PickedRaise(picked, 0), 30);
        Check(one.Raise == picked.Raise && one.Standing == picked.Standing && one.AskRest == picked.AskRest && one.AskDay == picked.AskDay && one.UnderDays == picked.UnderDays,
            "picked raise: up to the going rate is the Staff tab's own raise, field for field");
        StaffFile asked = PickedStaff("s", 2000, 2400);
        asked.Standing = 50;
        asked.AskDay = 28;
        asked.AskTo = 2400;
        StaffRules.GivePicked(asked, 100, 30);
        Check(asked.Raise == 100 && asked.Standing == 50 + CompanyCatalogue.StandLess && asked.AskDay == 0 && asked.AskTo == 0 && asked.AskRest == 30 + CompanyCatalogue.RaiseRestDays,
            "picked raise: short of what they asked, it settles the request as offering less does");
        StaffFile over = PickedStaff("v", 2500, 2300);
        over.Standing = 95;
        StaffRules.GivePicked(over, 200, 30);
        Check(over.Raise == 200 && over.Standing == CompanyCatalogue.StandingMost, "picked raise: past the going rate it is given, and standing stops at its most");
        StaffFile none = PickedStaff("n", 2000, 2300);
        StaffRules.GivePicked(none, 0, 30);
        Check(none.Raise == 0 && none.AskRest == 0 && none.Standing == CompanyCatalogue.StandingStart, "picked raise: nothing to give changes nothing, and starts no rest");
    }

    private static void PickedRaisePlan()
    {
        var list = new List<StaffFile>
        {
            PickedStaff("a", 2000, 2300, 800), PickedStaff("b", 3000, 3000, 600), null
        };
        StaffFile rested = PickedStaff("c", 1500, 2000, 800);
        rested.AskRest = 99;
        StaffFile offer = PickedStaff("d", 1500, 2000, 800);
        offer.PoachDay = 3;
        list.Add(rested);
        list.Add(offer);
        PickedPlan going = StaffRules.Plan(list, 0, 40);
        Check(going.Picked == 5 && going.Raised == 1 && going.AtRate == 1 && going.Unknown == 1 && going.Rested == 1 && going.Waiting == 1 && going.Left == 4,
            "picked raise: the preview counts who is raised and why each other is left out");
        Check(going.Raise == 300 && going.After - going.Before == going.Raise && going.Before == 2000 + 3000 + 1500 + 1500,
            "picked raise: the preview's pay an hour in all moves by the raise and nothing else");
        PickedPlan five = StaffRules.Plan(list, 2, 40);
        Check(five.Raised == 2 && five.Raise == 100 + 150 && five.DayBefore == 2000L * 8 + 3000L * 6 + 1500L * 8 * 2 && five.DayAfter - five.DayBefore == 100L * 8 + 150L * 6,
            "picked raise: the preview's working day in all is each one's pay times their hours");
        Check(StaffRules.Plan(null, 2, 40).Picked == 0 && StaffRules.Plan(new List<StaffFile>(), 2, 40).Raised == 0, "picked raise: nobody picked comes to nothing");
        PickedPlan again = StaffRules.Plan(list, 2, 40);
        Check(again.Raise == five.Raise && again.DayAfter == five.DayAfter, "picked raise: the same picks give the same preview");
        var many = new List<StaffFile>(5000);
        for (int i = 0; i < 5000; i++)
        {
            many.Add(PickedStaff("m" + i, 1500 + i % 900, 2200, 800));
        }
        var watch = Stopwatch.StartNew();
        PickedPlan big = StaffRules.Plan(many, 3, 40);
        watch.Stop();
        Check(big.Raised == 5000 && watch.Elapsed.TotalMilliseconds < 50, "picked raise: the preview of 5,000 picked takes under 50 ms (" + watch.Elapsed.TotalMilliseconds.ToString("0.0") + " ms)");
    }

    private static void PickedRaiseSources()
    {
        string game = ModSource(@"Economy\Company\PickedRaiseGame.cs");
        string director = ModSource(@"Economy\Company\CompanyDirector.PickedRaises.cs");
        string slow = ModSource(@"Economy\Company\CompanyDirector.cs");
        Check(game.Length > 0 && director.Length > 0, "picked raise sources: the game's side and the director's are there");
        Check(game.Contains("GameContract.MassActions.Field") && game.Contains("GameContract.MassActionTabs.Field") && game.Contains("a is PayBonusesMassAction"),
            "picked raise sources: the action goes into the game's own lists by the contract, beside paying bonuses");
        Check(game.Contains("GameConfirm.Show(") && !game.Contains("HudConfirm.Show(") && game.Contains("ShowMassActionOptionsPanel(") && game.Contains("OnMassActionPerformed("),
            "picked raise sources: the game's panel of choices, its question box through GameConfirm, and its own end of an action");
        Check(!game.Contains("hourlyWage") && !director.Contains("hourlyWage"), "picked raise sources: the game's wage is never written");
        Check(game.Contains("Guard.Listener<int>(Chosen)") && game.Contains("Guard.Fail(Guarded, e)"), "picked raise sources: the choice and the game's Perform are behind the guard");
        Check(director.Contains("StaffRules.GivePicked(f, raise, day)") && director.Contains("StaffRules.CanPick(f, day)") && director.Contains("LogPicked(day)") && director.Contains("ReadPeople(out _)"),
            "picked raise sources: each picked employee is raised by the rule of one raise, from wages read afresh, and the hierarchy's log has it");
        Check(slow.Contains("PickedRaiseGame.Keep(On(CompanyPart.Staff))") && slow.Contains("PickedRaiseGame.Keep(false)"),
            "picked raise sources: the action stands while the staff part runs and goes when it stops");
    }

    private static void PickedRaiseTexts()
    {
        string[] keys =
        {
            "reallife_co_mass_raise", "reallife_co_mass_raise_title", "reallife_co_mass_raise_going", "reallife_co_mass_raise_percent", "reallife_co_mass_raise_amount",
            "reallife_co_mass_raise_confirm", "reallife_co_mass_raise_left", "reallife_co_mass_raise_none", "reallife_co_mass_raise_give", "reallife_co_mass_raise_done",
            "reallife_co_mass_raise_off", "reallife_co_staff_picked", "reallife_co_test_picked_raise", "reallife_co_test_picked_raise_pass", "reallife_co_test_picked_raise_fail"
        };
        Check(AllIn(keys, out string missing), "picked raise texts: every key in en, pt and zh-cn (" + missing + ")");
        Dictionary<string, string> en = LocaleFile("en");
        Check(Fits(en, "reallife_co_mass_raise_done") && Fits(en, "reallife_co_mass_raise_none") && Fits(en, "reallife_co_mass_raise_off"),
            "picked raise texts: the notices are 240 characters or fewer");
        Check(en.TryGetValue("reallife_guide_economy_company", out string guide) && guide.Contains("<b>Give a raise</b>") && guide.Contains("{co_raise_rest} days for the next"),
            "picked raise texts: the Guide's page on running a business says how to raise several at once");
    }

    private static readonly Dictionary<string, Dictionary<string, string>> LocaleFiles = new Dictionary<string, Dictionary<string, string>>();

    // A language's texts, read once for these checks.
    private static Dictionary<string, string> LocaleFile(string code)
    {
        if (LocaleFiles.TryGetValue(code, out Dictionary<string, string> held))
        {
            return held;
        }
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales", code + ".json"));
        if (File.Exists(path))
        {
            using (var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)))
            {
                foreach (System.Text.Json.JsonProperty p in doc.RootElement.EnumerateObject())
                {
                    texts[p.Name] = p.Value.GetString();
                }
            }
        }
        LocaleFiles[code] = texts;
        return texts;
    }

    // Every key in the three languages the mod writes itself; the first missing one named.
    private static bool AllIn(string[] keys, out string missing)
    {
        missing = "none missing";
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> texts = LocaleFile(code);
            foreach (string k in keys)
            {
                if (!texts.TryGetValue(k, out string v) || string.IsNullOrEmpty(v))
                {
                    missing = code + " " + k;
                    return false;
                }
            }
        }
        return true;
    }

    private static bool Fits(Dictionary<string, string> en, string key) => en.TryGetValue(key, out string v) && v.Length <= 240;
}
