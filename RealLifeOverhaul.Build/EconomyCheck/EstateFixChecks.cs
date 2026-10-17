using System;
using RealLifeOverhaul.Economy;

// Property, hotels and car hire fixed, each reproduced here:
//   1. a building the player lets himself was paid short on a night the game let fewer units (its rent over the game's
//      market rate), and nothing made it good;
//   2. a hire car stood idle a day longer than the page and the balance say after it came back;
//   3. listing prices the mod wrote were never put back when the property part stopped;
//   4. the Mine tab's rent a day for a building the game lets left out the rental income option;
//   5. a hotel's campaign was missing from what it spent this year;
//   6. "this year" ran from the day the feature first ran, not the game's tax year;
//   7. a group's offer at a hotel could still be accepted the day after the day shown;
//   8. how a hire ended could change while the car waited for its space, the rating it was judged by moving with other hires;
//   9. the hire page was not refreshed when a car that waited was lost;
//  10. a car waiting for its space was filtered as on the lot;
//  11. a building at the top of the run-down range was run down in the list and kept on its page;
//  12. a car "came back a day late" on its own day;
//  13. a bought hotel Travel no longer prices was sold back for all that was paid;
//  14. a car taken off the save while out kept the customer's deposit.
internal static partial class Program
{
    private static void EstateFixes()
    {
        EstateFixRent();
        EstateFixYear();
        EstateFixHotel();
        EstateFixHire();
        EstateFixSources();
    }

    private static void EstateFixRent()
    {
        // The game's run on a night it lets five points fewer: what it pays, and what is made good.
        EstateRules.Encode(1000, true, 400, 80, out float occupancy, out float price);
        int paid = (int)Math.Round((occupancy - 5f) / 100f * 400 * price);
        Check(paid < 1000 && paid + EstateRules.RentShort(1000, paid) == 1000 && EstateRules.RentShort(1000, 1000) == 0 && EstateRules.RentShort(1000, 1006) == 0 && EstateRules.RentShort(0, 0) == 0
              && EstateRules.RentShort(1000, 0) == 0,
            "estate fix: a night the game let fewer units is made good to the agreed rent, and a building the run paid nothing for is left to the morning's reading (" + paid + " paid of 1000)");
        Check(EstateRules.GameNight(100, 3000) == 300 && EstateRules.GameNight(100, 1000) == 100 && EstateRules.GameNight(100, 500) == 50 && EstateRules.GameNight(0, 3000) == 0,
            "estate fix: a building the game lets brings its rent with the rental income option settled on it");
        Check(EstateRules.RunDown(EstateCatalogue.RunDownMost) && EstateRules.RunDown(EstateCatalogue.RunDownLeast) && !EstateRules.RunDown(EstateCatalogue.KeptLeast),
            "estate fix: every condition StreetCondition draws as run down is run down, its top included");
    }

    private static void EstateFixYear()
    {
        Check(!EstateRules.YearTurned(50, 112, 112) && EstateRules.YearTurned(50, 113, 112) && !EstateRules.YearTurned(113, 224, 112) && EstateRules.YearTurned(113, 225, 112),
            "estate fix: the year's figures start again when the game's tax year turns, on the day after a multiple of the year");
        Check(EstateRules.YearTurned(100, 150, 112) && EstateRules.TaxYearFirst(150, 112) == 113, "estate fix: a year begun on another day by an older save is put on the game's year at its next turn");
    }

    private static void EstateFixHotel()
    {
        HotelFile f = Hotel();
        HotelRules.Campaign(f, 10, 500);
        Check(f.PushEnd == 10 + EstateCatalogue.CampaignDays - 1 && f.CostsYear == 500, "estate fix: a campaign runs a week from today and counts in what the hotel spent this year");
        Check(HotelRules.SoldBack(false, 100000, 0) == EstateCatalogue.Share(100000, EstateCatalogue.HotelSellShare) && HotelRules.SoldBack(true, 5000, 0) == 5000
              && HotelRules.SoldBack(false, 100000, 120000) == EstateCatalogue.Share(120000, EstateCatalogue.HotelSellShare),
            "estate fix: a bought hotel Travel no longer prices is sold back under what was paid, as one it prices is under its price; a lease returns its deposit");
        EstateClimate c = Climate();
        HotelFile g = Hotel();
        HotelRules.AskGroup(g, 500, 1);
        int lapse = g.AskLapse;
        bool waits = !HotelRules.Night(g, 500, lapse - 1, c).GroupLapsed && g.AskRooms > 0;
        bool gone = HotelRules.Night(g, 12345, lapse, c).GroupLapsed;
        Check(waits && gone, "estate fix: a group can be answered until the end of the day shown, and has booked elsewhere by the next morning");
    }

    private static void EstateFixHire()
    {
        EstateClimate c = Climate();
        // A car whose hire ends stolen under a poor rating and not under a good one: hired under a good one, it is judged by it.
        var car = new HireCar { Id = "rated", Price = 20000, HireDays = 2, HireRate = 108, Deposit = 216, State = (int)HireState.Out, Day = 40 };
        bool found = false;
        for (int seed = 0; seed < 200000 && !found; seed++)
        {
            car.HireSeed = seed;
            found = HireRules.End(car, 100, true, c).Stolen && !HireRules.End(car, 900, true, c).Stolen;
        }
        car.HireRating = 900;
        Check(found && !HireRules.End(car, 100, true, c).Stolen && HireRules.End(car, 100, true, c).Rent == HireRules.End(car, 900, true, c).Rent,
            "estate fix: how a hire ends is fixed by the rating it went out under, whatever happens to the rating while it waits");
        var hired = new HireCar { Id = "h", Price = 20000 };
        Check(HireRules.TryHire(hired, 7, 3, c, 650, true) && hired.HireRating == 650, "estate fix: a hire keeps the rating it went out under");
        HireRules.Settle(hired, HireRules.End(hired, 650, true, c));
        Check(hired.HireRating == -1, "estate fix: a hire settled keeps no rating");
        var s = new EstateState { Seed = 3 };
        s.Hire.Lot = "Lot~1";
        s.Hire.Cars.Add(new HireCar { Id = "v1", Type = "t", Price = 20000, State = (int)HireState.Out, Day = 9, HireDays = 2, HireRate = 108, Deposit = 216, HireSeed = 77, HireRating = 640 });
        int version;
        EstateState back = EstateStore.Read(EstateStore.Write(s), out version);
        string line = EstateStore.Write(s);
        string old = line.Substring(0, line.LastIndexOf('|'));
        EstateState before = EstateStore.Read(old, out version);
        Check(back != null && back.Hire.Cars[0].HireRating == 640 && before != null && before.Hire.Cars[0].HireRating == -1,
            "estate fix: the hire's rating goes through the save; a line from before reads none and is judged as before");

        // Late: back the day after its day; on time: back on its day; stolen: found out on its day.
        var late = new HireCar { Id = "late", Price = 20000, HireDays = 2, HireRate = 108, Deposit = 216, State = (int)HireState.Out, Day = 40, HireRating = 600 };
        var onTime = new HireCar { Id = "on", Price = 20000, HireDays = 2, HireRate = 108, Deposit = 216, State = (int)HireState.Out, Day = 40, HireRating = 600 };
        for (int seed = 0; seed < 5000 && !HireRules.End(late, 600, true, c).Late; seed++)
        {
            late.HireSeed = seed;
        }
        for (int seed = 0; seed < 5000 && (HireRules.End(onTime, 600, true, c).Late || HireRules.End(onTime, 600, true, c).Stolen); seed++)
        {
            onTime.HireSeed = seed;
        }
        Check(HireRules.End(late, 600, true, c).Late && HireRules.Back(late, 600) == 41 && HireRules.Back(onTime, 600) == 40, "estate fix: a hire that runs a day late comes back the day after its day");

        Check(HireRules.Away((int)HireState.Out) && HireRules.Away((int)HireState.Waiting) && !HireRules.Away((int)HireState.Idle) && !HireRules.Away((int)HireState.Valet),
            "estate fix: a car waiting for its space is away from the lot, not on it");
        Check(HireRules.DepositHeld(new HireCar { State = (int)HireState.Out, Deposit = 216 }) == 216 && HireRules.DepositHeld(new HireCar { State = (int)HireState.Waiting, Deposit = 216 }) == 216
              && HireRules.DepositHeld(new HireCar { State = (int)HireState.Idle, Deposit = 0 }) == 0 && HireRules.DepositHeld(new HireCar { State = (int)HireState.Valet, Deposit = 216 }) == 0,
            "estate fix: a customer's deposit is held while the car is out or waits, and goes back if the car is gone");
    }

    private static void EstateFixSources()
    {
        string property = EstateSource("EstateDirector.Property.cs"), trade = EstateSource("EstateDirector.Trade.cs"), game = EstateSource("EstateGame.cs"), director = EstateSource("EstateDirector.cs");
        string money = EstateSource("EstateDirector.Money.cs"), app = EstateSource("EstateApp.cs"), appProperty = EstateSource("EstateApp.Property.cs"), appTrade = EstateSource("EstateApp.Trade.cs");
        string balance = EstateSource("EstateBalance.cs");
        string day = MemberText(property, "private void PropertyDay(");
        Check(day.Contains("EstateRules.RentShort(agreed, rent)") && day.Contains("EstateGame.BookRent(shortfall, h);"), "estate fix: the night's rent of a building the player lets is made good where the game paid short");
        string hireDay = MemberText(trade, "private void HireDay(");
        int valet = hireDay.IndexOf("case HireState.Valet:", StringComparison.Ordinal);
        Check(valet >= 0 && hireDay.IndexOf("goto default;", valet, StringComparison.Ordinal) > valet && hireDay.Contains("day >= HireRules.Back(car, hire.Rating)")
              && hireDay.Contains("HireRules.DepositHeld(car)"),
            "estate fix: a car cleaned yesterday can go out this morning, a late one comes back the day after, and a gone car's deposit goes back");
        string restore = MemberText(game, "public static int RestoreValues(");
        Check(restore.IndexOf("Reprice();", StringComparison.Ordinal) > 0 && restore.IndexOf("Reprice();", StringComparison.Ordinal) < restore.IndexOf("City.Clear();", StringComparison.Ordinal),
            "estate fix: the listings are priced at the game's own values again when the mod's are taken back");
        Check(Occurs(appProperty, "EstateRules.GameNight(EstateGame.GameRent(h), c.RentScale)") == 2, "estate fix: the Mine tab and a building's page show the same rent a day");
        Check(MemberText(trade, "public void HotelCampaign(").Contains("HotelRules.Campaign(f, Save.Day, cost);"), "estate fix: a campaign counts in the hotel's year");
        Check(MemberText(director, "private void OnDay(").Contains("EstateRules.YearTurned(_state.YearFrom, day, _climate.DaysPerYear)")
              && MemberText(money, "private void NewYear(").Contains("_state.YearFrom = EstateRules.TaxYearFirst(day, _climate.DaysPerYear);"),
            "estate fix: the year's figures turn with the game's tax year");
        string hour = MemberText(trade, "private void HireHour(");
        Check(hour.Contains("int had = hire.Cars.Count;") && hour.Contains("hire.Cars.Count != had"), "estate fix: a car lost while it waited refreshes the page and the Planner");
        Check(appTrade.Contains("HireRules.Away(car.State) ? EstateTables.CarOut : EstateTables.CarIn"), "estate fix: the hire page files a waiting car as away");
        Check(!app.Contains("EstateCatalogue.RunDownMost") && Occurs(app, "EstateRules.RunDown(cond)") == 4, "estate fix: the list and a listing's page judge run down alike");
        Check(MemberText(trade, "public void GiveUpHotel(").Contains("HotelRules.SoldBack(f.Leased, f.Paid,"), "estate fix: giving up a hotel pays what the rule says");
        Check(balance.Contains("day >= HireRules.Back(car, rating)"), "estate fix: the Guide's hire return counts a late car's extra day out, as the game now does");
    }

    private static string EstateSource(string file) => EconomySource(@"Estate\" + file);
}
