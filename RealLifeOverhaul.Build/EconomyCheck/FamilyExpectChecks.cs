using RealLifeOverhaul.Family;

// What the partner expects as the player gets richer (Scripts/Family/PartnerExpect): three levels of the player's
// prosperity (the daily income held all week and the cash) at today's prices, a bigger home and evenings out more often at each, a home of unknown size never too small, evenings
// out counted from the move in when there has been none, and nothing with the option off. The rule is checked here, and
// its use in the game's side in its source.
internal static partial class Program
{
    // A new allowance was only a notice, and the conversation still said the old one: they now answer in it, with the new
    // amount, thanked for a raise, put out by a cut below what they need, hurt when it stops.
    // The car and the evenings out were said by text with nothing to answer: both are asks talked over at home now (the
    // "wants to talk" prompt), the evenings out with a dinner date planned there and then.
    private static void FamilyWishesTalked()
    {
        string expect = ModSource(@"Family\PartnerExpect.cs"), voice = ModSource(@"Family\PartnerVoice.cs");
        Check(expect.Contains("Speak(\"reallife_family_expect_dates\", Args.Us, PartnerVoice.Date)") && voice.Contains("case Date: return PartnerExpect.DateTalk;")
              && voice.Contains("case Car: return PartnerCar.Offer;") && MemberText(expect, "public static DialogEntry DateTalk()").Contains("HomeLife.PlanDate"),
            "family expectations: the car and the evenings out are asked for by text and talked over at home, the evenings out with a date planned there and then");
    }

    private static void FamilyAllowanceAnswer()
    {
        bool rule = PartnerExpectRules.AllowanceReaction(500, 500, 400) == PartnerExpectRules.ReactSame
                    && PartnerExpectRules.AllowanceReaction(500, 0, 400) == PartnerExpectRules.ReactStopped
                    && PartnerExpectRules.AllowanceReaction(500, 450, 400) == PartnerExpectRules.ReactDownMet
                    && PartnerExpectRules.AllowanceReaction(500, 300, 400) == PartnerExpectRules.ReactDownShort
                    && PartnerExpectRules.AllowanceReaction(300, 450, 400) == PartnerExpectRules.ReactUpMet
                    && PartnerExpectRules.AllowanceReaction(100, 300, 400) == PartnerExpectRules.ReactUpShort
                    && PartnerExpectRules.AllowanceReactionHarmony(300, 450, 400) > 0f && PartnerExpectRules.AllowanceReactionHarmony(450, 300, 400) < 0f
                    && PartnerExpectRules.AllowanceReactionHarmony(450, 600, 400) == 0f && PartnerExpectRules.AllowanceReactionHarmony(450, 300, 0) == 0f;
        string typed = MemberText(ModSource(@"Family\PartnerExpect.cs"), "private static void Typed(string text)");
        Check(rule && typed.Contains("DialogController.current.ShowEntry(") && typed.Contains("Reactions[react]") && typed.Contains("AllowanceReactionHarmony(was, amount, expected)"),
            "family expectations: a changed allowance is answered in the conversation with the new amount (thanks, put out, hurt), with a little harmony only when it crosses what they expect");
    }

    private static void FamilyExpect()
    {
        FamilyAllowanceAnswer();
        FamilyWishesTalked();
        Check(PartnerExpectRules.LevelOf(24999f, 2000, 1000) == 0 && PartnerExpectRules.LevelOf(25000f, 2000, 1000) == 1 && PartnerExpectRules.LevelOf(25000f, 1999, 1000) == 0
              && PartnerExpectRules.LevelOf(500000f, 20000, 1000) == 3 && PartnerExpectRules.LevelOf(5000000f, 5000, 1000) == 2 && PartnerExpectRules.LevelOf(25000f, 2000, 1100) == 0,
            "family expectations: the levels want the daily income held all week and the cash: 2,000 and 25,000, 5,000 and 100,000, 20,000 and 500,000, at today's prices");
        Check(PartnerExpectRules.HomeWanted(0) == 0 && PartnerExpectRules.HomeWanted(1) < PartnerExpectRules.HomeWanted(2) && PartnerExpectRules.HomeWanted(2) < PartnerExpectRules.HomeWanted(3)
              && PartnerExpectRules.HomeWanted(9) == PartnerExpectRules.HomeWanted(3),
            "family expectations: a bigger home at each level, and nothing below the first");
        Check(PartnerExpectRules.HomeShortOf(50, 60) && !PartnerExpectRules.HomeShortOf(60, 60) && !PartnerExpectRules.HomeShortOf(0, 60) && !PartnerExpectRules.HomeShortOf(40, 0),
            "family expectations: a home is too small only when its size is known and below what is wanted");
        int most = PartnerExpectRules.DateDays[1];
        Check(!PartnerExpectRules.DatesShortOf(0, 500, 0, 0) && !PartnerExpectRules.DatesShortOf(1, 100 + most, 100, 50) && PartnerExpectRules.DatesShortOf(1, 101 + most, 100, 50)
              && !PartnerExpectRules.DatesShortOf(1, 60 + most, 0, 60) && PartnerExpectRules.DateDays[3] < PartnerExpectRules.DateDays[1],
            "family expectations: evenings out further apart than the level's " + most + " days fall short, counted from the move in when there has been none, more often as the player gets richer");

        Check(PartnerExpectRules.AllowanceExpected(0) == 0 && PartnerExpectRules.AllowanceExpected(1) < PartnerExpectRules.AllowanceExpected(3)
              && PartnerExpectRules.AllowanceWeek(0, 0, false) == 0f && PartnerExpectRules.AllowanceWeek(200, 200, false) > 0f && PartnerExpectRules.AllowanceWeek(199, 200, false) < 0f
              && PartnerExpectRules.AllowanceWeek(199, 200, true) == PartnerExpectRules.AllowanceWeek(199, 200, false) / 2f
              && PartnerExpectRules.AllowanceAffection(300, 200) > PartnerExpectRules.AllowanceAffection(200, 200) && PartnerExpectRules.AllowanceAffection(199, 200) == 0f
              && PartnerExpectRules.ParseAllowance("$1,500", 10000) == 1500 && PartnerExpectRules.ParseAllowance("1500.00", 10000) == 1500
              && PartnerExpectRules.ParseAllowance("504", 10000) == 500 && PartnerExpectRules.ParseAllowance("99999", 10000) == 10000
              && PartnerExpectRules.ParseAllowance("0", 10000) == 0 && PartnerExpectRules.ParseAllowance("lots", 10000) == -1 && PartnerExpectRules.ParseAllowance("", 10000) == -1,
            "family expectations: a bigger allowance at each level, a week of it warms the home and brings them closer when it is enough (closer still when generous), cools it when not (half as much once married), and it is typed ($1,500 or 1500.00), rounded to tens, never above the most, nothing for words");
        int usual = PartnerExpectRules.Usual, at1 = PartnerExpectRules.AllowanceExpected(1);
        Check(PartnerExpectRules.AllowanceExpected(1, usual, false, false, false, 0, false) == at1 && PartnerExpectRules.AllowanceExpected(0, usual, true, false, true, 3, false) == 0
              && PartnerExpectRules.AllowanceExpected(1, PartnerExpectRules.Lavish, false, false, false, 0, false) > at1
              && PartnerExpectRules.AllowanceExpected(1, PartnerExpectRules.Thrifty, false, false, false, 0, false) < at1
              && PartnerExpectRules.AllowanceExpected(1, usual, true, false, false, 0, false) > PartnerExpectRules.AllowanceExpected(1, usual, false, true, false, 0, false)
              && PartnerExpectRules.AllowanceExpected(1, usual, false, false, true, 0, false) > at1
              && PartnerExpectRules.AllowanceExpected(1, usual, false, false, false, 2, false) == at1 + 2 * PartnerExpectRules.PerChild
              && PartnerExpectRules.AllowanceExpected(1, usual, false, false, false, 2, true) == at1
              && PartnerExpectRules.SpendOfTraits(true, true, true) == PartnerExpectRules.Lavish,
            "family expectations: the allowance they expect follows how they are with money, whether they earn, marriage and the children whose care the player does not already pay for");
        Check(!PartnerExpectRules.SecondHomeMet(1) && PartnerExpectRules.SecondHomeMet(2) && PartnerExpectRules.CarFrom < PartnerExpectRules.SecondHomeFrom
              && PartnerExpectRules.WantsCar(1, 100, 0) && !PartnerExpectRules.WantsCar(0, 100, 0) && !PartnerExpectRules.WantsCar(1, 100, 100 - PartnerExpectRules.CarBackDays)
              && PartnerExpectRules.WantsCar(1, 101, 100 - PartnerExpectRules.CarBackDays)
              && PartnerExpectRules.ModelAt(3, 20) > PartnerExpectRules.ModelAt(1, 20) && PartnerExpectRules.ModelAt(3, 20) <= 19 && PartnerExpectRules.ModelAt(1, 0) == 0,
            "family expectations: a car from the first level, not asked again for a while after one is asked back, a dearer model the richer; a second home from the second");
        int g = PartnerExpectRules.WishGraceDays;
        Check(PartnerExpectRules.Wish(true, false, 0, 50) == WishStep.Ask && PartnerExpectRules.Wish(true, false, 50, 50 + g) == WishStep.None
              && PartnerExpectRules.Wish(true, false, 50, 51 + g) == WishStep.Short && PartnerExpectRules.Wish(true, true, 50, 60) == WishStep.Met
              && PartnerExpectRules.Wish(false, false, 0, 50) == WishStep.None && PartnerExpectRules.Wish(true, true, 0, 50) == WishStep.None,
            "family expectations: a wish is asked for once, held against the player only after " + g + " days, thanked for when met, and nothing is said of one already had");

        string expect = ModSource(@"Family\PartnerExpect.cs");
        string car = ModSource(@"Family\PartnerCar.cs");
        Check(car.Contains("!Dealerships.Stock(v.vehicleInstance)") && car.Contains("GoingPublic.MineVehicle(v)") && car.Contains("!v.controlledByPlayer") && car.Contains("Economy.EstateGame.Hide(")
              && car.Contains("Park(v)") && car.Contains("VehicleHelper.CreateAndSpawnVehicle(v, bay.transform.position") && !car.Contains("TowVehicle") && car.Contains("!GoingPublic.InSession")
              && ModSource(@"Transport\CarOwnership.cs").Contains("Family.PartnerCar.Is(instance)") && ModSource(@"Economy\Banking\Desk\CreditDesk.Finance.cs").Contains("Family.PartnerCar.Is(v)")
              && ModSource(@"Licences\LicenceDirector.cs").Contains("Family.PartnerCar.Is(instance)") && ModSource(@"Transport\CarPhone.cs").Contains("!Family.PartnerCar.Is(car)"),
            "family expectations: the car handed over is one of the player's own they are not driving, kept out of the city, left alone by insurance, credit, licences and roadside help, made again at a free repair bay when asked back (it has no object for the tow to move), and never in Going Public");
        Check(expect.Contains("GoingPublic.Mine(r) && GameRegistrations.BuildingType(r) == Residential")
              && expect.Contains("FamilyTalk.Move(-paid, \"reallife_transaction_family_allowance\")") && expect.Contains("day - s.PAllowanceDay < 7"),
            "family expectations: only the player's own homes count, and the allowance is paid once a week");
        Check(MemberText(expect, "public static void Check(int day, int level, bool speak)").Contains("!Settings.FamilyExpect")
              && expect.Contains("if (!PartnerVoice.Free)") && ModSource(@"Family\FamilyCalendar.cs").Contains("PartnerExpect.Hourly(day);") && expect.Contains("PartnerExpectRules.LevelOf(SaveGameManager.Current != null ? SaveGameManager.Current.Money : 0f, FamilyState.IncomeHeld, Prices.Level)")
              && ModSource(@"Family\FamilyCalendar.cs").Contains("PartnerExpect.Daily(day);") && Occurs(ModSource(@"Family\Partner.cs"), "PartnerExpect.Reset(s);") == 3,
            "family expectations: looked at each hour in the day and each day behind its option, at today's prices, and afresh with every new partner");

        // A load made the partner's car again in the city until the next game hour; a car bought for them was saved at the
        // interior's coordinates the player stood at; in a Going Public session, where no car is offered, the partner still
        // wished for one, the home cooled while it went without, and the open ask's talk offered the car anyway.
        string director = ModSource(@"Family\FamilyDirector.cs"), voice = ModSource(@"Family\PartnerVoice.cs");
        int load = director.IndexOf("FamilyState.Load();", System.StringComparison.Ordinal);
        int resync = load < 0 ? -1 : director.IndexOf("Partner.Resync();", load, System.StringComparison.Ordinal);
        int hour = resync < 0 ? -1 : director.IndexOf("_lastHour = TimeHelper.CurrentHour;", resync, System.StringComparison.Ordinal);
        int hide = resync < 0 ? -1 : director.IndexOf("PartnerCar.Hourly();", resync, System.StringComparison.Ordinal);
        string buy = MemberText(car, "private static DialogEntry Buy(string type, int price)"), on = MemberText(voice, "private static bool On(string kind)");
        Check(resync > 0 && hide > resync && hide < hour
              && buy.Contains("v.position = OnStreet();") && buy.Contains("v.rotation = Quaternion.identity;") && !buy.Contains("player.transform.position")
              && MemberText(car, "private static Vector3 OnStreet()").Contains("entranceDoors[0].doorTransform.position")
              && expect.Contains("PartnerExpectRules.WantsCar(level, day, s.PCarBackDay) && !GoingPublic.InSession")
              && on.Contains("case Car: return Settings.FamilyExpect && !GoingPublic.InSession;") && on.Contains("case Allowance: return Settings.FamilyExpect;")
              && on.Contains("case Date: return Settings.FamilyExpect;"),
            "family expectations: the partner's car is out of the city at once after a load, a car bought for them is saved on the street, "
            + "and in a Going Public session no car is wished for or offered");
    }
}
