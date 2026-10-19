using RealLifeOverhaul.Family;

// What the partner expects as the player gets richer (Scripts/Family/PartnerExpect): three levels of the player's cash
// at today's prices, a bigger home and evenings out more often at each, a home of unknown size never too small, evenings
// out counted from the move in when there has been none, and nothing with the option off. The rule is checked here, and
// its use in the game's side in its source.
internal static partial class Program
{
    private static void FamilyExpect()
    {
        Check(PartnerExpectRules.LevelOf(249999f, 1000) == 0 && PartnerExpectRules.LevelOf(250000f, 1000) == 1 && PartnerExpectRules.LevelOf(5000000f, 1000) == 3
              && PartnerExpectRules.LevelOf(250000f, 1100) == 0,
            "family expectations: the levels are 250,000, a million and five million in cash, at today's prices");
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
        Check(expect.Contains("GoingPublic.Mine(r) && r.GetBuildingType() == Residential")
              && expect.Contains("FamilyTalk.Move(-paid, \"reallife_transaction_family_allowance\")") && expect.Contains("day - s.PAllowanceDay < 7"),
            "family expectations: only the player's own homes count, and the allowance is paid once a week");
        Check(MemberText(expect, "public static void Check(int day, int level, bool speak)").Contains("!Settings.FamilyExpect")
              && expect.Contains("if (!PartnerVoice.Free)") && ModSource(@"Family\FamilyCalendar.cs").Contains("PartnerExpect.Hourly(day);") && expect.Contains("PartnerExpectRules.LevelOf(SaveGameManager.Current != null ? SaveGameManager.Current.Money : 0f, Prices.Level)")
              && ModSource(@"Family\FamilyCalendar.cs").Contains("PartnerExpect.Daily(day);") && Occurs(ModSource(@"Family\Partner.cs"), "PartnerExpect.Reset(s);") == 3,
            "family expectations: looked at once a day behind its option, at today's prices, and afresh with every new partner");
    }
}
