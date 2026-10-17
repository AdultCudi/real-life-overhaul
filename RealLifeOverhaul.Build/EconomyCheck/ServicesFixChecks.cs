using RealLifeOverhaul.Economy;

// The cleaning service and selling a business fixed, each reproduced here:
//   1. a round planned again in the middle of an hour dropped the visits already set out for, so their minutes were
//      cleaned twice and their way charged twice;
//   2. the van's driving was never charged when the van was given up before the morning, and a start after a gap let it go;
//   3. a visit planned in the last hour of a cleaner's shift was cleaned at the next hour, when the shift was over, and
//      counted as not made;
//   4. a visit was taken to have cleaned all it would clean though tiles stop at nothing, so a clean shop's measured dirt
//      grew each day and with it the minutes planned;
//   5. a counter could be made to an offer already at the asking price, where it could only lose the buyer;
//   6. the van's price a kilometre was shown rounded to the whole (5 where 4.5 is charged);
//   7. the seven days' van and fee figures kept days from a week before after a jump or a time off;
//   8. the end of the day (minute 1440) was written 23:00;
//   9. a business of the kind sold, taken on later on the day of the sale, escaped the clause; and a sale paid in parts while
//      as many prices as are kept were still being paid dropped the oldest, whose parts were never paid.
internal static partial class Program
{
    private static void ServicesFixes()
    {
        ServicesFixReplan();
        ServicesFixHour();
        ServicesFixTaken();
        ServicesFixCounter();
        ServicesFixVanPrice();
        ServicesFixDays();
        ServicesFixClock();
        ServicesFixSale();
        ServicesFixSources();
    }

    // A visit set out for at 10:30, cleaning 10:40 to 11:40: 20 minutes at 10:00, the round planned again at 10:15, 40 at 11:00.
    private static void ServicesFixReplan()
    {
        int first = RoundRules.Due(640, 700, 0, 600);
        bool kept = RoundRules.Kept(630, true, first, 615);
        int second = RoundRules.Due(640, 700, kept ? first : 0, 660);
        Check(first == 20 && kept && first + second == 60, "services fix: a visit set out for stays through a round planned again, so its hour is cleaned once (" + (first + second) + " minutes of 60)");
        Check(RoundRules.Kept(600, false, 0, 615) && !RoundRules.Kept(630, false, 0, 615) && RoundRules.Kept(630, false, 5, 615),
            "services fix: a visit not yet set out for is planned again, one under way or begun is kept");
    }

    private static void ServicesFixHour()
    {
        Check(RoundRules.HourOf(930, 0) == 15 && RoundRules.HourOf(900, 30) == 15 && RoundRules.HourOf(959, 1) == 16 && RoundRules.HourOf(930, -4) == 15,
            "services fix: the minutes still to clean begin in the visit's own hour, and that is the shift asked about");
    }

    private static void ServicesFixTaken()
    {
        Check(CleaningRules.Taken(300, 1000) == 300 && CleaningRules.Taken(4000, 1000) == 1000 && CleaningRules.Taken(0, 500) == 0 && CleaningRules.Taken(-20, 500) == 0,
            "services fix: a visit takes off the worst tile what it would, as far as there is dirt there");
        int need = CleaningRules.NeedDay(-1, 300, 0, CleaningRules.Taken(300, 1500));
        Check(need == 0, "services fix: a shop that stayed clean measures no dirt a day, however long it was cleaned (" + need + ")");
    }

    private static void ServicesFixCounter()
    {
        var state = new ServicesState { Seed = 5 };
        var at = new SaleOffer { Id = 1, Price = 50000, Ceiling = 90000 };
        var under = new SaleOffer { Id = 2, Price = 45000, Ceiling = 90000 };
        state.SaleOffers.Add(at);
        state.SaleOffers.Add(under);
        Check(!SaleRules.CanCounter(at, 50000) && !SaleRules.CanCounter(at, 40000) && SaleRules.CanCounter(under, 50000) && !SaleRules.CanCounter(null, 50000),
            "services fix: an offer at or over the asking price has no counter, one under it has");
        bool safe = true;
        for (int seed = 0; seed < 60; seed++)
        {
            state.Seed = seed;
            safe &= SaleRules.Counter(state, at, 50000) == CounterResult.Refused && !at.Countered && state.SaleOffers.Contains(at);
        }
        Check(safe, "services fix: a counter to an offer at the asking price changes nothing, and the buyer never walks away for it");
    }

    private static void ServicesFixVanPrice()
    {
        Check(TravelRules.PerKm(1125) == 4.5f && TravelRules.PerKm(1000) == 4f && TravelRules.PerKm(9000) == 16f, "services fix: the van's price a kilometre is shown as it is charged, to the cent (4.5 at 112.5)");
        Check(TravelRules.Cost(100000, 1125) == (int)(TravelRules.PerKm(1125) * 100), "services fix: a hundred kilometres cost a hundred times what a kilometre is shown at");
    }

    private static void ServicesFixDays()
    {
        int[] days = { 1, 2, 3, 4, 5, 6, 7 };
        ServicesState.ClearDays(days, 10, 12);
        Check(days[3] == 0 && days[4] == 0 && days[5] == 0 && days[2] == 3 && days[6] == 7 && days[0] == 1, "services fix: the days a jump passed over are emptied, the rest kept");
        ServicesState.ClearDays(days, 13, 12);
        Check(ServicesState.Week(days) == 1 + 2 + 3 + 7, "services fix: no days to empty empties none");
        ServicesState.ClearDays(days, 0, 400);
        Check(ServicesState.Week(days) == 0, "services fix: a jump of more than a week empties the whole week");
    }

    private static void ServicesFixClock()
    {
        ServicesCatalogue.Clock(1440, out int endHour, out int endMinute);
        ServicesCatalogue.Clock(615, out int hour, out int minute);
        ServicesCatalogue.Clock(-5, out int startHour, out int startMinute);
        Check(endHour == 23 && endMinute == 59 && hour == 10 && minute == 15 && startHour == 0 && startMinute == 0, "services fix: the end of the day is 23:59, never 23:00");
    }

    private static void ServicesFixSale()
    {
        var state = new ServicesState { Seed = 1 };
        SaleClause later = SaleRules.Clause(state, "ba:businesstype_giftshop", "north", "Gifts", 50000, 30, 10, false);
        SaleClause had = SaleRules.Clause(state, "ba:businesstype_giftshop", "north", "Gifts", 50000, 30, 10, true);
        Check(SaleRules.Breaks(later, "ba:businesstype_giftshop", "north", 10) && !SaleRules.Breaks(had, "ba:businesstype_giftshop", "north", 10)
              && SaleRules.Breaks(had, "ba:businesstype_giftshop", "north", 11) && !SaleRules.Breaks(later, "ba:businesstype_giftshop", "north", 9),
            "services fix: one of the kind taken on later on the day of the sale breaks the clause, unless one was taken on that day already");
        var full = new ServicesState { Seed = 1 };
        for (int i = 0; i < ServicesCatalogue.DuesMost; i++)
        {
            full.Dues.Add(new SaleDue { Name = "S" + i, Part = 100, Left = 2, NextDay = 60 });
        }
        int parts = SaleRules.Parts(full, 4);
        SaleMoney m = SaleRules.Settle(65610, parts, new SaleBreakdown { Value = 80000 }, 0);
        Check(parts == 1 && m.Now == 65610 && m.PartsLeft == 0 && SaleRules.Due(full, "New", 0, m, 50) == null && full.Dues.Count == ServicesCatalogue.DuesMost && full.Dues[0].Name == "S0",
            "services fix: with every place for a price in parts taken, a sale is paid at once and no price still being paid is dropped");
        full.Dues.RemoveAt(0);
        Check(SaleRules.Parts(full, 4) == 4 && SaleRules.Parts(full, 0) == 0, "services fix: with a place free, a buyer pays in parts as offered");
    }

    private static void ServicesFixSources()
    {
        string cleaning = EconomySource(@"Services\ServicesDirector.Cleaning.cs"), director = EconomySource(@"Services\ServicesDirector.cs"), sale = EconomySource(@"Services\ServicesDirector.Sale.cs");
        string game = EconomySource(@"Services\ServicesGame.cs"), page = EconomySource(@"Services\ServicesApp.Cleaning.cs"), salePage = EconomySource(@"Services\ServicesApp.Sale.cs");
        string guide = EconomySource(@"Services\ServicesGuide.cs"), text = EconomySource(@"Services\ServicesText.cs");
        Check(MemberText(cleaning, "private void Replan(").Contains("RoundRules.Kept(_visits[i].Leave, _visits[i].Walked, _visits[i].Done, now)"),
            "services fix: the round planned again keeps every visit set out for");
        string day = MemberText(cleaning, "private void CleaningDay("), start = MemberText(cleaning, "private void CleaningStart(");
        Check(day.Contains("int cost = TravelRules.Cost(_state.VanMetres, ServicesGame.Index);") && !start.Contains("VanMetres = 0"),
            "services fix: the van's way is charged whether or not the van is still kept, and a start after a gap lets none of it go");
        string hour = MemberText(cleaning, "private void CleaningHour(");
        Check(hour.Contains("ServicesGame.Working(member, from)") && !hour.Contains("IsWorking(") && MemberText(game, "public static bool Working(").Contains("OnCleaningShift(member.Base, e.id, hour)"),
            "services fix: a visit is made if its cleaner's shift covered its own hour");
        Check(MemberText(game, "public static int Clean(").Contains("CleaningRules.Taken("), "services fix: a visit counts what came off the worst tile");
        Check(salePage.Contains("SaleRules.CanCounter(o,"), "services fix: the counter button is offered only where a counter can gain");
        Check(page.Contains("TravelRules.PerKm(ServicesGame.Index)") && guide.Contains("TravelRules.PerKm(PriceRules.Start)") && !guide.Contains("Prices.Level"),
            "services fix: the page and the Guide show the van's price as charged, the Guide at a price index of 100 as it says");
        string onDay = MemberText(director, "private void OnDay("), startUp = MemberText(director, "private void StartUp(");
        Check(onDay.Contains("ServicesState.ClearDays(_state.VanCost, last, day - 1);") && onDay.Contains("ServicesState.ClearDays(_state.FeeIncome, last + 1, day);")
              && startUp.Contains("ServicesState.ClearDays(state.VanCost, state.LastDay, today - 1);") && startUp.Contains("ServicesState.ClearDays(state.FeeIncome, state.LastDay + 1, today);"),
            "services fix: every morning, and a start after days off, empties the days no figure was written for");
        Check(MemberText(text, "public static string Clock(").Contains("ServicesCatalogue.Clock(minute,"), "services fix: times are written through the clock that keeps within the day");
        Check(sale.Contains("SaleRules.Clause(_state, type, district, name, price, clauseDays, day, takenToday)") && Occurs(sale, "SaleRules.Parts(_state, parts)") == 2 && salePage.Contains("SaleRules.Parts("),
            "services fix: the sale reads whether one of the kind was taken on today, and pays at once when no place for parts is free");
    }
}
