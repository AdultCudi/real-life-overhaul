using System;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;

// The one calendar (Scripts/Core/CalendarRules.cs) and money at the city's prices (Scripts/Economy/Prices.cs): the
// year from a save, the month as a twelfth of it whatever the player chose (60 to 365 days), the seasons as the
// weather has them and as travel and property count them, a month's worth of a daily price, the price level from the
// city's index, and how prices are rounded.
internal static partial class Program
{
    private static void Calendar()
    {
        Check(CalendarRules.Year(0) == 60 && CalendarRules.Year(5) == 60 && CalendarRules.Year(60) == 60 && CalendarRules.Year(365) == 365,
            "calendar: a save's year is its own, or the game's 60 days when it says none");
        Check(CalendarRules.MonthDays(60) == 5 && CalendarRules.MonthDays(120) == 10 && CalendarRules.MonthDays(365) == 30 && CalendarRules.MonthDays(24) == 3,
            "calendar: a month is a twelfth of the year (5, 10, 30 days), three days at least");
        bool sameAsMacro = true;
        for (int year = 8; year <= 400; year++)
        {
            sameAsMacro &= CalendarRules.MonthDays(year) == MacroCatalogue.MonthDays(year);
        }
        Check(sameAsMacro, "calendar: and it is the city economy's month, for every year length");
        Check(CalendarRules.DayOfYear(1, 60) == 1 && CalendarRules.DayOfYear(60, 60) == 60 && CalendarRules.DayOfYear(61, 60) == 1 && CalendarRules.DayOfYear(0, 60) == 1,
            "calendar: the day of the year starts again after the year's last");

        // The seasons, over a 60-day and a 365-day year: winter at the turn of the year, then spring, summer, autumn.
        foreach (int year in new[] { 60, 365 })
        {
            int[] count = new int[4];
            int changes = 0, last = CalendarRules.Season(year, year);
            for (int day = 1; day <= year; day++)
            {
                int season = CalendarRules.Season(day, year);
                count[season]++;
                if (season != last)
                {
                    changes++;
                    Check(season == (last + 1) % 4, "calendar: the seasons follow each other in order (" + year + "-day year, day " + day + ")");
                }
                last = season;
                Check(CalendarRules.SeasonFromSpring(day, year) == (season + 3) % 4, "calendar: counted from spring, the same season (" + year + "-day year, day " + day + ")");
            }
            Check(CalendarRules.Season(1, year) == CalendarRules.Winter && CalendarRules.Season(year, year) == CalendarRules.Winter && changes == 4,
                "calendar: a " + year + "-day year begins and ends in winter and has each season once");
            bool even = true;
            for (int s = 0; s < 4; s++)
            {
                even &= Math.Abs(count[s] - year / 4f) <= year * 0.1f;
            }
            Check(even, "calendar: each season is about a quarter of a " + year + "-day year (" + string.Join(", ", count) + ")");
        }
        Check(CalendarRules.Monthly(32, 60) == 160 && CalendarRules.Monthly(32, 365) == 960, "calendar: a month's worth of a daily price is that many days of it");

        PriceChecks();
    }

    private static void PriceChecks()
    {
        Check(PriceRules.Level(100f) == 1000 && PriceRules.Level(107.04f) == 1070 && PriceRules.Level(10f) == 500 && PriceRules.Level(900f) == 4000,
            "prices: the level is the city's index in tenths of a point, within the index's own bounds");
        Check(PriceRules.At(2.75f, 1000) == 2.75f && PriceRules.At(150, 1000) == 150 && PriceRules.At(25000, 1000) == 25000,
            "prices: at the start's level an amount is as written");
        Check(Near(PriceRules.At(2.75f, 1070), 2.95f) && Near(PriceRules.At(7.6f, 1070), 8.15f), "prices: under 10, to five cents (2.75 and 7.60 at 107)");
        Check(Near(PriceRules.At(34f, 1070), 36.5f) && Near(PriceRules.At(12f, 1150), 14f), "prices: under 100, to fifty cents (34 at 107, 12 at 115)");
        Check(PriceRules.At(150, 1070) == 160 && PriceRules.At(400, 1070) == 430 && PriceRules.At(900, 1040) == 935, "prices: under 1,000, to five");
        Check(PriceRules.At(2200, 1070) == 2350 && PriceRules.At(1800, 1255) == 2260, "prices: under 10,000, to ten");
        Check(PriceRules.At(25000, 1070) == 26750 && PriceRules.At(60000, 1333) == 80000, "prices: above, to fifty");
        Check(PriceRules.At(-150, 1070) == -160 && Near(PriceRules.At(-2.75f, 1070), -2.95f), "prices: a refund is rounded as the charge would be");
        Check(PriceRules.At(4, 1070) == 4 && PriceRules.At(6, 1150) == 7, "prices: a small amount in whole money is the whole nearest its own price");
        bool steady = true;
        for (int amount = 1; amount <= 20000; amount += 37)
        {
            int up = PriceRules.At(amount, 1200), down = PriceRules.At(amount, 800);
            steady &= up >= amount && down <= amount;
        }
        Check(steady, "prices: a higher level never makes an amount cheaper, a lower one never dearer");
    }

    private static bool Near(float a, float b)
    {
        return Math.Abs(a - b) < 0.001f;
    }
}
