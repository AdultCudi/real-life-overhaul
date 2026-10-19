using System;
using RealLifeOverhaul.Family;

// How the partner takes the player's money (Scripts/Family/PartnerWealth): a reaction once at each new step of the
// player's cash, by how they lean, one for steps skipped; the envious minding the gap more at each step past 250,000,
// half as much once married; the supportive warmer; nothing with the option off. The rule is checked here, and its use in
// the game's side in its source.
internal static partial class Program
{
    private static void FamilyWealth()
    {
        const int start = 1000;
        Check(PartnerWealthRules.StepOf(2499f, 200, start) == 0 && PartnerWealthRules.StepOf(2500f, 200, start) == 1 && PartnerWealthRules.StepOf(2500f, 199, start) == 0
              && PartnerWealthRules.StepOf(50000f, 2500, start) == 3 && PartnerWealthRules.StepOf(5000000f, 2500, start) == 3 && PartnerWealthRules.StepOf(500000f, 20000, start) == 5
              && PartnerWealthRules.StepOf(2500f, 200, 1200) == 0 && PartnerWealthRules.StepOf(3000f, 240, 1200) == 1,
            "family wealth: each step wants the daily income held all week and the cash (200 and 2,500 up to 20,000 and 500,000), at today's prices; cash alone reaches none past the income");
        Check(PartnerWealthRules.NewStep(3, 1) == 3 && PartnerWealthRules.NewStep(3, 3) == -1 && PartnerWealthRules.NewStep(2, 3) == -1,
            "family wealth: one reaction for steps skipped, none for a step already reacted to, none when the cash falls back");
        int e = PartnerWealthRules.Envious, s = PartnerWealthRules.Supportive, n = PartnerWealthRules.Neither;
        Check(PartnerWealthRules.StepHarmony(s) > 0f && PartnerWealthRules.StepHarmony(e) < 0f && PartnerWealthRules.StepAffection(n) == 0f,
            "family wealth: a new step warms a supportive partner's home and cools an envious one's");
        Check(PartnerWealthRules.Drift(e, 2, false) == 0f && PartnerWealthRules.Drift(e, 4, false) < PartnerWealthRules.Drift(e, 3, false)
              && Math.Abs(PartnerWealthRules.Drift(e, 4, true) - PartnerWealthRules.Drift(e, 4, false) * PartnerWealthRules.MarriedShare) < 1e-6f
              && PartnerWealthRules.Drift(s, 3, false) > 0f && PartnerWealthRules.Drift(n, 5, false) == 0f,
            "family wealth: from 250,000 the envious mind the gap more at each step, half as much once married; the supportive warm; the rest are untouched");
        int envious = 0, supportive = 0;
        for (int seed = 1; seed <= 5000; seed++)
        {
            int l = PartnerWealthRules.LeaningOf(seed * 7919);
            envious += l == e ? 1 : 0;
            supportive += l == s ? 1 : 0;
        }
        Check(envious > 800 && envious < 1200 && supportive > 800 && supportive < 1200 && PartnerWealthRules.LeaningOfTraits(true, true) == s,
            "family wealth: about one default partner in five is envious and one supportive (" + envious + ", " + supportive + "), and Social's traits decide for its people");

        string wealth = ModSource(@"Family\PartnerWealth.cs"), partner = ModSource(@"Family\Partner.cs"), links = ModSource(@"Wiring\FamilyLinks.cs");
        Check(MemberText(wealth, "public static void Daily(int day)").Contains("!Settings.FamilyWealthMood") && MemberText(wealth, "public static void Hourly(int day)").Contains("!PartnerVoice.Free")
              && wealth.Contains("PartnerWealthRules.StepOf(SaveGameManager.Current != null ? SaveGameManager.Current.Money : 0f, FamilyState.IncomeHeld, Prices.Level)") && wealth.Contains("PartnerVoice.Say(Texts[leaning], Args.Us);")
              && ModSource(@"Family\FamilyCalendar.cs").Contains("PartnerWealth.Daily(day);") && Occurs(partner, "PartnerWealth.Reset(s);") == 3
              && links.Contains("PartnerWealthRules.LeaningOfTraits(p.Has(SocialTrait.Supportive), p.Has(SocialTrait.Envious))"),
            "family wealth: once a day behind its option, at today's prices, afresh with every new partner, and Social's partner leans by their own traits");
    }
}
