using RealLifeOverhaul.Family;

// The partner's bigger wish about work (Scripts/Family/PartnerWish): at a million in cash a partner who lives with the
// player and still works asks once to stop working or to retrain for the job they always wanted, never the one they have;
// stopping and studying bring in nothing and keep them home; a yes is never asked again, a no a month later. The rule is
// checked here, and its use in the game's side in its source.
internal static partial class Program
{
    private static void FamilyWish()
    {
        const int at = PartnerWishRules.AskAtCash, full = PartnerWorkRules.FullTime, half = PartnerWorkRules.PartTime;
        Check(PartnerWishRules.Asks(true, true, full, false, false, at, at, 50, 0) && PartnerWishRules.Asks(true, true, half, false, false, at, at, 50, 0)
              && !PartnerWishRules.Asks(true, true, full, false, false, at - 1, at, 50, 0),
            "family wish: a partner who lives with the player and works, full or half days, asks once the cash reaches " + at);
        Check(!PartnerWishRules.Asks(false, true, full, false, false, at, at, 50, 0) && !PartnerWishRules.Asks(true, false, full, false, false, at, at, 50, 0)
              && !PartnerWishRules.Asks(true, true, PartnerWorkRules.Stopped, false, false, at, at, 50, 0) && !PartnerWishRules.Asks(true, true, PartnerWorkRules.Studying, false, false, at, at, 50, 0)
              && !PartnerWishRules.Asks(true, true, full, true, false, at, at, 50, 0) && !PartnerWishRules.Asks(true, true, full, false, true, at, at, 50, 0)
              && !PartnerWishRules.Asks(true, true, full, false, false, at, at, 50, 50 - PartnerWorkRules.AskAgainDays + 1),
            "family wish: nothing is asked with the option off, of a partner not living with the player, once stopped or studying, once granted, while an ask waits, or within a month of a no");
        bool never = true;
        int stop = 0;
        for (int seed = 1; seed <= 4000; seed++)
        {
            int s = seed * 104729;
            for (int job = 0; job < 10; job++)
            {
                int dream = PartnerWishRules.DreamOf(s, job, 10);
                never &= dream != job && dream >= 0 && dream < 10;
            }
            stop += PartnerWishRules.WishOf(s) == PartnerWishRules.Stop ? 1 : 0;
        }
        Check(never && stop > 1600 && stop < 2400, "family wish: the dream job is never the one they have, and about half would rather stop (" + stop + " of 4000)");
        Check(PartnerWorkRules.Income(200, PartnerWorkRules.Stopped) == 0 && PartnerWorkRules.Income(200, PartnerWorkRules.Studying) == 0
              && !PartnerWorkRules.Works(PartnerWorkRules.Stopped) && !PartnerWorkRules.Works(PartnerWorkRules.Studying) && PartnerWorkRules.Works(half)
              && PartnerWishRules.Studying(30, 29) && !PartnerWishRules.Studying(30, 30),
            "family wish: stopped or studying they bring in nothing and do not go to work; studies end on the day set");

        Check(PartnerWishRules.ChipsIn(PartnerWishRules.ChipInHarmony) && !PartnerWishRules.ChipsIn(PartnerWishRules.ChipInHarmony - 0.1f) && PartnerWishRules.ChipInWeek(200) == 350
              && !PartnerWishRules.CanAskChipIn(50, 60, 0) && PartnerWishRules.CanAskChipIn(60, 60, 0) && !PartnerWishRules.CanAskChipIn(60, 0, 60 - PartnerWishRules.ChipInAgainDays + 1),
            "family wish: asked to chip in for the rent they agree in a happy home, a quarter of a week's pay a week, and can be asked again only after a while");
        string wish = ModSource(@"Family\PartnerWish.cs"), work = ModSource(@"Family\PartnerWork.cs"), home = ModSource(@"Family\HomeLife.cs");
        Check(home.Contains("works = works && PartnerWorkRules.Works(PartnerWork.Work);") && work.Contains("PartnerWishRules.Studying(FamilyState.S.PRetrainUntil, TimeHelper.CurrentDay)")
              && wish.Contains("Prices.At(PartnerWishRules.AskAtCash)") && wish.Contains("s.PWishGranted = true;")
              && ModSource(@"Family\FamilyCalendar.cs").Contains("PartnerWish.Hourly(day);") && wish.Contains("s.PWorkAsk = false;") && work.Contains("&& !s.PWishAsk, s.PWork,")
              && wish.Contains("PartnerVoice.Ask(PartnerVoice.Wish, \"reallife_family_wish_text\", Args.Us);") && wish.Contains("!PartnerVoice.Asking(PartnerVoice.Wish)") && Occurs(ModSource(@"Family\Partner.cs"), "PartnerWish.Reset(s);") == 3,
            "family wish: the timetable and pay read stopping and studying through the switch, asked once a day at today's prices, afresh with every new partner");
        Check(wish.Contains("PartnerGlad.Start(PartnerGlad.Stop);") && wish.Contains("PartnerGlad.Start(PartnerGlad.Retrain);") && wish.Contains("s.PJobKey = null;")
              && ModSource(@"Family\Partner.cs").Contains("&& !PartnerWish.OwnJob)") && wish.Contains("s.LivesIn && !Partner.Retired"),
            "family wish: a yes is followed by warm texts, a job retrained for stays theirs over People's, and a retired partner is never asked");
    }
}
