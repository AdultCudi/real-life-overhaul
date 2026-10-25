using System;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// Checks for travel's prices (Scripts/Travel/TravelCatalogue): every amount of the hotels and trips is written at the
// start's prices and moves with the city's price level as the rooms do, and what the room's list shows is what it charges.
internal static partial class Program
{
    private static void TravelPrices()
    {
        Func<int> was = Prices.Factor;
        try
        {
            Prices.Factor = null;
            Check(TravelCatalogue.MealPrice(HotelTier.Budget) == 8 && TravelCatalogue.MealPrice(HotelTier.MidRange) == 32
                  && TravelCatalogue.MealPrice(HotelTier.Luxury) == 65 && TravelCatalogue.MealPrice(HotelTier.Cottage) == 28
                  && TravelCatalogue.MinibarPrice(HotelTier.MidRange) == 12 && TravelCatalogue.MinibarPrice(HotelTier.Luxury) == 18
                  && TravelCatalogue.LaundryPrice(HotelTier.Budget) == 8 && TravelCatalogue.LaundryPrice(HotelTier.MidRange) == 25
                  && TravelCatalogue.LaundryPrice(HotelTier.Luxury) == 0 && TravelCatalogue.TablePrice == 85 && TravelCatalogue.DinnerPrice == 60
                  && TravelCatalogue.FreeNightMost == 250,
                "travel: at the start's prices the extras are as the page gives them (sandwich 8, room service 32 or 65, hamper 28, minibar 12 or 18, laundry 8, 25 or free, dinners 85 and 60)");

            int level = 1500;
            Prices.Factor = () => level;
            Trip cruise = TravelCatalogue.Trips[1];
            var lost = new TravelEvent { Key = "x", Money = -80 };
            bool moved = true;
            foreach (HotelTier tier in new[] { HotelTier.Budget, HotelTier.MidRange, HotelTier.Luxury, HotelTier.Cottage })
            {
                int meal = tier == HotelTier.Budget ? 8 : tier == HotelTier.Luxury ? 65 : tier == HotelTier.Cottage ? 28 : 32;
                int bar = tier == HotelTier.Luxury ? 18 : 12;
                int wash = tier == HotelTier.Luxury ? 0 : tier == HotelTier.Budget ? 8 : 25;
                moved &= TravelCatalogue.MealPrice(tier) == PriceRules.At(meal, level) && TravelCatalogue.MinibarPrice(tier) == PriceRules.At(bar, level)
                         && TravelCatalogue.LaundryPrice(tier) == PriceRules.At(wash, level);
            }
            moved &= TravelCatalogue.TablePrice == PriceRules.At(85, level) && TravelCatalogue.DinnerPrice == PriceRules.At(60, level)
                     && TravelCatalogue.FreeNightMost == PriceRules.At(250, level) && TravelCatalogue.SplurgePrice(cruise) == PriceRules.At(cruise.Splurge, level)
                     && TravelCatalogue.EventMoney(lost) == PriceRules.At(-80, level) && TravelCatalogue.MealPrice(HotelTier.MidRange) == 48;
            Check(moved, "travel: with prices half as high again, meals, the minibar, laundry, both dinners, a splurge, a trip's event and the loyalty night move with them");
        }
        finally
        {
            Prices.Factor = was;
        }

        // The room's list: what is charged is the amount shown, both asked for once.
        string room = Read(@"Travel\HotelStay.Room.cs");
        MatchCollection paid = Regex.Matches(room, @"(?<!bool )PaidExtra\(([^()]*(?:\([^()]*\))?[^()]*)\)");
        bool same = paid.Count >= 6;
        foreach (Match m in paid)
        {
            string amount = m.Groups[1].Value;
            same &= Regex.IsMatch(amount, @"^\w+$") && room.Contains("TravelText.Money(" + amount + ")");
        }
        Check(same, "travel: every extra the room charges is shown at the amount charged (" + paid.Count + " charges)");
        string trip = Read(@"Travel\Holidays.Trip.cs");
        Check(!trip.Contains(".Pay(trip.Splurge") && !trip.Contains("Money(trip.Splurge)") && trip.Contains("TravelCatalogue.SplurgePrice(trip)")
              && Read(@"Travel\TravelEffects.cs").Contains("Pocket(TravelCatalogue.EventMoney(e)"),
            "travel: a splurge and a trip's event pay and show today's prices");
    }
}

namespace RealLifeOverhaul
{
    // The mod's GameCalendar reads the save; TravelCatalogue's seasons read a year of 365 days here.
    internal static class GameCalendar
    {
        public static int DaysPerYear => 365;
        public static int MonthDays => CalendarRules.MonthDays(DaysPerYear);
        public static int SeasonFromSpring(int day) => CalendarRules.SeasonFromSpring(day, 365);
    }
}
