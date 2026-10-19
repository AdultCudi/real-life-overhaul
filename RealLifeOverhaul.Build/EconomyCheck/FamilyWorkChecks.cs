using System;
using RealLifeOverhaul.Family;

// The partner's work as the player's fortune grows (Scripts/Family/PartnerWork): a partner who lives with the player asks
// to cut back to half days once the player's cash reaches the sum, asks again a month after a no, and on half days works
// four hours and brings in half; with the option off nothing is asked and the days are full. The rule is checked here,
// and its use in the game's side in its source.
internal static partial class Program
{
    private static void FamilyWork()
    {
        const int at = PartnerWorkRules.AskAtCash, full = PartnerWorkRules.FullTime, part = PartnerWorkRules.PartTime;
        Check(PartnerWorkRules.Asks(true, true, full, false, at, at, 40, 0) && !PartnerWorkRules.Asks(true, true, full, false, at - 1, at, 40, 0),
            "family work: a partner who lives with the player asks once the player's cash reaches " + at + ", and not a dollar before");
        Check(!PartnerWorkRules.Asks(false, true, full, false, at, at, 40, 0) && !PartnerWorkRules.Asks(true, false, full, false, at, at, 40, 0)
              && !PartnerWorkRules.Asks(true, true, part, false, at, at, 40, 0) && !PartnerWorkRules.Asks(true, true, full, true, at, at, 40, 0),
            "family work: nothing is asked with the option off, of a partner who does not live with the player, on half days already, or while an ask waits");
        int again = PartnerWorkRules.AskAgainDays;
        Check(!PartnerWorkRules.Asks(true, true, full, false, at, at, 10 + again - 1, 10) && PartnerWorkRules.Asks(true, true, full, false, at, at, 10 + again, 10),
            "family work: after a no they ask again " + again + " days later, not sooner");
        Check(PartnerWorkRules.Until(8, 17, part) == 12 && PartnerWorkRules.Until(8, 17, full) == 17 && PartnerWorkRules.Until(14, 16, part) == 16
              && PartnerWorkRules.Income(210, part) == 105 && PartnerWorkRules.Income(210, full) == 210,
            "family work: half days are four hours from the start (never longer than the full day) and bring in half");

        string work = ModSource(@"Family\PartnerWork.cs"), home = ModSource(@"Family\HomeLife.cs"), partner = ModSource(@"Family\Partner.cs");
        string calendar = ModSource(@"Family\FamilyCalendar.cs"), choices = ModSource(@"Family\FamilyPhone.Choices.cs");
        Check(work.Contains("Settings.FamilyWork ? FamilyState.S.PWork : PartnerWorkRules.FullTime") && home.Contains("to = PartnerWorkRules.Until(from, to, PartnerWork.Work);")
              && partner.Contains("PartnerWorkRules.Income(") && partner.Contains("PartnerWork.Work);"),
            "family work: the timetable at home and the share of the rent read the work through the option, so off means full days as before");
        Check(Occurs(partner, "PartnerWork.Reset(s);") == 3 && calendar.Contains("PartnerWork.Daily(day);")
              && choices.Contains("PartnerWork.CanTalk") && choices.Contains("PartnerWork.CanReturn")
              && MemberText(work, "public static void Daily(int day)").Contains("Prices.At(PartnerWorkRules.AskAtCash)"),
            "family work: asked once a day at today's prices, talked over and undone from a family conversation, and started afresh with every new partner or none");
        string glad = ModSource(@"Family\PartnerGlad.cs");
        Check(work.Contains("PartnerGlad.Start(PartnerGlad.Work);") && work.Contains("FamilyState.Together && s.LivesIn && !Partner.Retired") && calendar.Contains("PartnerGlad.Daily(day);")
              && glad.Contains("FamilyText.Message(FamilyPhone.PartnerContact, lines[i], Args.Us);") && glad.Contains("private static readonly int[] After = { 1, 3, 6 };"),
            "family work: a yes is followed by three warm texts over the next days, and a retired partner is never asked to cut back");
    }
}
