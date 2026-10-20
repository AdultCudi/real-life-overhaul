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
        Check(PartnerWorkRules.Asks(true, true, full, false, at, at, 40, 0, 800, 800) && !PartnerWorkRules.Asks(true, true, full, false, at - 1, at, 40, 0, 800, 800),
            "family work: a partner who lives with the player asks once the player's cash reaches " + at + ", and not a dollar before");
        var week = new System.Collections.Generic.List<int>();
        for (int d = 0; d < 6; d++)
        {
            IncomeRules.Record(week, 900f);
        }
        int six = IncomeRules.Held(week);
        IncomeRules.Record(week, 799f);
        int dip = IncomeRules.Held(week);
        for (int d = 0; d < 7; d++)
        {
            IncomeRules.Record(week, 1200f);
        }
        Check(six == 0 && dip == 799 && IncomeRules.Held(week) == 1200 && week.Count == IncomeRules.Days
              && !PartnerWorkRules.Asks(true, true, full, false, at, at, 40, 0, 799, 800) && !PartnerWorkRules.Asks(true, true, full, false, at - 1, at, 40, 0, 800, 800)
              && PartnerWorkRules.AskAtCash == 10000 && PartnerWorkRules.AskAtIncome == 800,
            "family work: they ask only once the daily income has held at 800 or more all week (its worst day counts, nothing before a whole week) and there is 10,000 in cash");
        Check(!PartnerWorkRules.Asks(false, true, full, false, at, at, 40, 0, 800, 800) && !PartnerWorkRules.Asks(true, false, full, false, at, at, 40, 0, 800, 800)
              && !PartnerWorkRules.Asks(true, true, part, false, at, at, 40, 0, 800, 800) && !PartnerWorkRules.Asks(true, true, full, true, at, at, 40, 0, 800, 800),
            "family work: nothing is asked with the option off, of a partner who does not live with the player, on half days already, or while an ask waits");
        int again = PartnerWorkRules.AskAgainDays;
        Check(!PartnerWorkRules.Asks(true, true, full, false, at, at, 10 + again - 1, 10, 800, 800) && PartnerWorkRules.Asks(true, true, full, false, at, at, 10 + again, 10, 800, 800),
            "family work: after a no they ask again " + again + " days later, not sooner");
        Check(PartnerWorkRules.Until(8, 17, part) == 12 && PartnerWorkRules.Until(8, 17, full) == 17 && PartnerWorkRules.Until(14, 16, part) == 16
              && PartnerWorkRules.Income(210, part) == 105 && PartnerWorkRules.Income(210, full) == 210,
            "family work: half days are four hours from the start (never longer than the full day) and bring in half");

        string work = ModSource(@"Family\PartnerWork.cs"), home = ModSource(@"Family\HomeLife.cs"), partner = ModSource(@"Family\Partner.cs");
        string calendar = ModSource(@"Family\FamilyCalendar.cs"), choices = ModSource(@"Family\FamilyPhone.Choices.cs");
        Check(work.Contains("Settings.FamilyWork ? FamilyState.S.PWork : PartnerWorkRules.FullTime") && home.Contains("to = PartnerWorkRules.Until(from, to, PartnerWork.Work);")
              && partner.Contains("PartnerWorkRules.Income(") && partner.Contains("PartnerWork.Work);"),
            "family work: the timetable at home and the share of the rent read the work through the option, so off means full days as before");
        Check(Occurs(partner, "PartnerWork.Reset(s);") == 3 && calendar.Contains("PartnerWork.Hourly(day);")
              && choices.Contains("PartnerWork.CanTalk") && choices.Contains("PartnerWork.CanReturn")
              && MemberText(work, "public static void Hourly(int day)").Contains("FamilyState.IncomeHeld, Prices.At(PartnerWorkRules.AskAtIncome)")
              && ModSource(@"Family\FamilyState.cs").Contains("PlayerHelper.CalculateDailyIncome()") && calendar.Contains("FamilyState.RecordIncome();"),
            "family work: asked once a day at today's prices, talked over and undone from a family conversation, and started afresh with every new partner or none");
        string glad = ModSource(@"Family\PartnerGlad.cs");
        Check(work.Contains("PartnerGlad.Start(PartnerGlad.Work);") && work.Contains("FamilyState.Together && s.LivesIn && !Partner.Retired") && calendar.Contains("PartnerGlad.Hourly(day);")
              && glad.Contains("PartnerVoice.Warm(lines[s.PGladNext], Args.Us);") && work.Contains("if (!PartnerVoice.Free)")
              && work.Contains("PartnerVoice.Ask(PartnerVoice.Work,") && Occurs(work, "PartnerVoice.Answered(PartnerVoice.Work);") == 2
              && ModSource(@"Family\HomeLife.AtHome.cs").Contains("FamilyTalk.Open(() => PartnerVoice.OpenAsk() ?? ") && glad.Contains("private static readonly int[] After = { 1, 3, 6 };"),
            "family work: a yes is followed by three warm texts, the ask is raised when the partner's voice allows and talked over face to face first, and a retired partner is never asked to cut back");
        Check(!PartnerVoiceRules.Free(20, 15, 0, 7) && PartnerVoiceRules.Settle(7, 15) >= PartnerVoiceRules.SettleLeast && PartnerVoiceRules.Settle(7, 15) <= PartnerVoiceRules.SettleMost
              && PartnerVoiceRules.Free(15 + PartnerVoiceRules.SettleMost, 15, 0, 7) && !PartnerVoiceRules.Free(100, 15, 100 - PartnerVoiceRules.GapLeast + 1, 7)
              && PartnerVoiceRules.Free(100, 15, 100 - PartnerVoiceRules.GapMost, 7)
              && PartnerVoiceRules.Reminder(4, 0) < 0 && PartnerVoiceRules.Reminder(5, 0) == 0 && PartnerVoiceRules.Reminder(6, 1) == 1 && PartnerVoiceRules.Reminder(6, 2) < 0,
            "family voice: nothing raised for 7 to 12 days after moving in, then 5 to 10 days between words, and an open ask reminded on its fifth and sixth day");
        bool spread = true;
        for (int seed = 1; seed < 400; seed++)
        {
            int g = PartnerVoiceRules.Gap(seed * 7919, seed), st = PartnerVoiceRules.Settle(seed * 104729, seed);
            spread &= g >= PartnerVoiceRules.GapLeast && g <= PartnerVoiceRules.GapMost && st >= PartnerVoiceRules.SettleLeast && st <= PartnerVoiceRules.SettleMost;
        }
        Check(spread, "family voice: every gap and every settling-in stays within its days");

        string voice = ModSource(@"Family\PartnerVoice.cs"), tree = ModSource(@"Meta\OptionsTree.cs");
        string text = MemberText(voice, "private static void Text(string key, in Args a)"), hourly = MemberText(work, "public static void Hourly(int day)");
        Check(text.Contains("if (contact != null)") && text.Contains("FamilyText.Message(contact, key, a, notify: false);")
              && !voice.Contains("FamilyText.Message(FamilyPhone.PartnerContact"),
            "family voice: with no contact to text from (the phone's contacts off) the partner's word is their named notice alone, never that and a plain one");
        int off = hourly.IndexOf("if (!Settings.FamilyWork)", StringComparison.Ordinal);
        int lapse = hourly.IndexOf("if (s.PWorkAsk && !PartnerVoice.Asking(PartnerVoice.Work))", StringComparison.Ordinal);
        Check(voice.Contains("public static bool AnyAsk => !string.IsNullOrEmpty(S.PAsk) && On(S.PAsk);")
              && MemberText(voice, "public static void Hourly(int day)").Contains("if (!On(S.PAsk))")
              && MemberText(voice, "private static bool On(string kind)").Contains("case Work: return Settings.FamilyWork;") && off > 0 && lapse > off
              && tree.Contains("S(\"partner\").Guide(\"family\").Need(\"family\").Opts(\"family_work\""),
            "family work: switched off while an ask is open, the ask is dropped (no reminder, no lapse, nothing held against the player), and the partner's section greys with Family off");
    }
}
