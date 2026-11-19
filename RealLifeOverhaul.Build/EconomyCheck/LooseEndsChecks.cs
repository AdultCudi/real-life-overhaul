using System;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Tech;

// Two loose ends of the 1.0.17 reports. (1) The applicants' button on the kit's Open Roles page made an offer the applicant answers the next day and read "Hire at {pay} an hour": it says
// "Offer {pay} an hour" now, as the old interface's "Make the offer" does, wherever its key is used. (2) The option "Staff pay, raises, strikes and sick pay" off still left the Business events'
// rival offer to an employee and strike threat, and professionals' sick days: they need that option too now, and the tips and the two notices say which switch stops which message.
// Game-free for the rules; the sources are read for what only the game can do.
internal static partial class Program
{
    private static void LooseEndsChecks()
    {
        OfferButtonChecks();
        StaffTroubleChecks();
    }

    private static void OfferButtonChecks()
    {
        string text = LocaleText("en", "reallife_work_hire_at");
        Check(text == "Offer {pay} an hour", "offer button: the applicants' button reads \"Offer {pay} an hour\" (" + text + ")");
        string roles = ModSource(@"Ui\Work\RolesLive.People.cs"), samples = ModSource(@"..\..\..\..\RealLifeOverhaul.Build\UiPreview\UiSamples.WorkRoles.cs"), sampleTexts = ModSource(@"..\..\..\..\RealLifeOverhaul.Build\UiPreview\UiSamples.WorkEn.cs");
        Check(Occurs(roles, "\"reallife_work_hire_at\", \"Offer {pay} an hour\"") == 2 && !roles.Contains("Hire at {pay}") && Occurs(samples, "\"reallife_work_hire_at\", \"Offer {pay} an hour\"") == 1
              && sampleTexts.Contains("{ \"reallife_work_hire_at\", \"Offer {pay} an hour\" }") && !samples.Contains("Hire at {pay}") && !sampleTexts.Contains("Hire at {pay}"),
            "offer button: every place the key is used (the applicants' rows, the applicant's page, the previews) carries the same words, so no text still says Hire at");
        Check(Occurs(roles, "reallife_work_hire_at") == 2 && LocaleText("en", "reallife_hire_button_offer") == "Make the offer" && roles.Contains("MakeOffer("),
            "offer button: the old interface's button is a different key (Make the offer) and both press the same call that makes an offer");
        string note = LocaleText("en", "reallife_work_offer_waiting");
        Check(note.Contains("They answer tomorrow") && LocaleText("en", "reallife_work_offer_note").Contains("accept, counter or turn it down"),
            "offer button: the texts beside it already say the applicant answers tomorrow, so the button's word and theirs agree");
    }

    private static void StaffTroubleChecks()
    {
        int needs = 0;
        string which = string.Empty;
        for (int i = 0; i < BusinessCatalogue.All.Length; i++)
        {
            IssueDef def = BusinessCatalogue.All[i];
            if (BusinessCatalogue.NeedsStaffPay(def.Id))
            {
                needs++;
                which += def.Id + " ";
            }
        }
        Check(needs == 2 && BusinessCatalogue.NeedsStaffPay(BusinessCatalogue.Poach) && BusinessCatalogue.NeedsStaffPay(BusinessCatalogue.Strike) && !BusinessCatalogue.NeedsStaffPay(BusinessCatalogue.Undercut)
              && !BusinessCatalogue.NeedsStaffPay(BusinessCatalogue.Smear) && !BusinessCatalogue.NeedsStaffPay(BusinessCatalogue.StaffTheft) && !BusinessCatalogue.NeedsStaffPay(BusinessCatalogue.Argument)
              && !BusinessCatalogue.NeedsStaffPay(null),
            "staff trouble: only a rival's offer to an employee and a strike threat need the staff pay option; undercutting, smears, a theft and an argument do not (" + which.Trim() + ")");
        string issues = ModSource(@"Business\BusinessIssues.cs"), director = ModSource(@"Business\BusinessDirector.cs");
        string kind = MemberText(issues, "public static bool KindOn(IssueDef def)");
        Check(kind.Contains("GroupOn(def.Group)") && kind.Contains("Settings.EconomyCompanyStaff") && kind.Contains("BusinessCatalogue.NeedsStaffPay(def.Id)"),
            "staff trouble: a kind is on when its group's switch is on and, for the two staff pay kinds, the staff pay option is on too; the separate switches stay");
        Check(Occurs(director, "BusinessIssues.KindOn(def)") == 3 && !director.Contains("GroupOn(def.Group)"),
            "staff trouble: the sweep that takes open issues down, the day's deal and another feature's start of an issue all ask the same rule, so one already open when the option goes off is taken down without its outcome");
        Check(!Regex.IsMatch(ModSource(@"Business\BusinessIssues.Start.cs") + ModSource(@"Business\BusinessIssues.Deadline.cs"), @"EconomyCompanyStaff"),
            "staff trouble: the rule is in one place, not repeated where the kinds start and end");
        var p = new Profile { Id = "sick-check", Burnout = TalentCatalogue.BurnoutSick + 200 };
        int on = 0, off = 0;
        for (int day = 1; day <= 4000; day++)
        {
            on += TalentRules.GoesSick(true, 77, p, day) ? 1 : 0;
            off += TalentRules.GoesSick(false, 77, p, day) ? 1 : 0;
        }
        var rested = new Profile { Id = "rested", Burnout = 100 };
        int restedOn = 0;
        for (int day = 1; day <= 4000; day++)
        {
            restedOn += TalentRules.GoesSick(true, 77, rested, day) ? 1 : 0;
        }
        Check(on > 200 && off == 0 && restedOn == 0, "staff trouble: a burnt out professional has sick days while the option is on (" + on + " in 4000) and none while it is off, and a rested one none either way");
        string people = ModSource(@"Tech\TechDirector.People.cs");
        Check(people.Contains("TalentRules.GoesSick(staffPay, Seed, p, day)") && people.Contains("bool staffPay = Settings.EconomyCompanyStaff;") && !people.Contains("TalentRules.SickChance(p)"),
            "staff trouble: the professionals' day asks the one rule with the one option, and nothing else rolls a sick day");
        string staff = LocaleText("en", "reallife_opt_tip_economy_company_staff"), rivals = LocaleText("en", "reallife_opt_tip_business_rivals"), troubles = LocaleText("en", "reallife_opt_tip_business_staff");
        Check(staff.Contains("poaching") && staff.Contains("strike threats") && staff.Contains("sick days") && rivals.Contains("Staff pay") && troubles.Contains("Staff pay") && troubles.Contains("strike threat"),
            "staff trouble: the staff pay option's tip says it stops poaching, strike threats and sick days, and the two Business events' tips say which of their messages need it");
        int bad = 0;
        foreach (string key in new[] { "reallife_opt_tip_economy_company_staff", "reallife_opt_tip_business_rivals", "reallife_opt_tip_business_staff" })
        {
            string tip = LocaleText("en", key);
            bad += tip.Length > 190 || tip.Split(' ').Length > 32 || tip.Contains("!") || tip.Contains("—") || tip.Contains("–") ? 1 : 0;
        }
        Check(bad == 0, "staff trouble: each tip is at most 32 words and 190 characters, with no dash or exclamation mark");
        string poach = LocaleText("en", "reallife_biz_poach_start"), strike = LocaleText("en", "reallife_biz_strike_start"), option = LocaleText("en", "reallife_co_opt_staff");
        Check(poach.Contains(LocaleText("en", "reallife_business_rivals")) && poach.Contains(option) && strike.Contains(LocaleText("en", "reallife_business_staff")) && strike.Contains(option)
              && poach.Length <= 240 && strike.Length <= 240 && poach.Contains("{name}") && poach.Contains("{amount}") && poach.Contains("{percent}") && strike.Contains("{count}") && strike.Contains("{raise}")
              && strike.Contains("{days}") && strike.Contains("{business}"),
            "staff trouble: the notice of a poach and of a strike names both options that govern it by the words on the options page, in at most 240 characters, and keeps its figures (" + poach.Length + ", " + strike.Length + ")");
    }
}
