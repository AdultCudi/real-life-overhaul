using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Calendar;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Tech;

// The Calendar app (Scripts/Calendar, its sources in Scripts/Wiring/Wiring.Calendar*.cs): the months of every year length
// (Core/CalendarRules), the grid of weeks, the day each month ends on, the days each schedule is marked on against the
// feature's own daily rule run day by day, an empty month, the list's limits and order, the sources' use of those rules,
// the folder on the phone, and the texts in every language.
internal static partial class Program
{
    private static void CalendarApp()
    {
        CalendarMonths();
        CalendarGridChecks();
        CalendarSchedules();
        CalendarListChecks();
        CalendarSources();
        CalendarPlannerYear();
        CalendarTexts();
    }

    // ---------------------------------------------------------------- the months

    private static void CalendarMonths()
    {
        bool cover = true, lengths = true, market = true, ofDay = true, years = true;
        string first = null;
        for (int year = 8; year <= 400; year++)
        {
            int months = CalendarRules.MonthsIn(year);
            int least = year / months, most = (year + months - 1) / months;
            int sum = 0;
            for (int m = 0; m < months; m++)
            {
                int length = CalendarRules.MonthLength(m, year);
                sum += length;
                lengths &= length >= least && length <= most && length >= 1;
                cover &= m == 0 ? CalendarRules.MonthStart(0, year) == 1 : CalendarRules.MonthStart(m, year) == CalendarRules.MonthEnd(m - 1, year) + 1;
            }
            cover &= sum == year && CalendarRules.MonthStart(months, year) == year + 1;
            for (int day = 1; day <= year * 3; day++)
            {
                int month = CalendarRules.MonthOf(day, year);
                bool here = CalendarRules.MonthStart(month, year) <= day && day <= CalendarRules.MonthEnd(month, year);
                ofDay &= here;
                years &= CalendarRules.YearOfMonth(month, year) == CalendarRules.YearOf(day, year) && CalendarRules.YearOf(day, year) == (day - 1) / year + 1
                         && CalendarRules.MonthInYear(month, year) == month % months;
                if (year >= 12 && month != MarketCatalogue.MonthOf(day, year))
                {
                    market = false;
                    first = first ?? year + "-day year, day " + day;
                }
                if (!here && first == null)
                {
                    first = year + "-day year, day " + day;
                }
            }
        }
        Check(cover, "calendar app: for every year length from 8 to 400 days the months follow each other from day 1 and make up the year exactly");
        Check(lengths, "calendar app: every month is a twelfth of its year, the year's length divided by 12 or one day more");
        Check(ofDay, "calendar app: every day of three years is in the month MonthOf says (" + (first ?? "none") + ")");
        Check(years, "calendar app: a month's year and place in it are the day's, days 1 to the year's length being the first year");
        Check(market, "calendar app: from 12 days a year up, the calendar's month is the stock market's month every day (" + (first ?? "none") + ")");

        Check(Lengths(60) == "5,5,5,5,5,5,5,5,5,5,5,5", "calendar app: a 60-day year has twelve months of 5 days (" + Lengths(60) + ")");
        Check(Lengths(120) == "10,10,10,10,10,10,10,10,10,10,10,10" && Lengths(360) == "30,30,30,30,30,30,30,30,30,30,30,30", "calendar app: 120 and 360-day years have months of 10 and 30 days");
        Check(Lengths(365) == "31,30,31,30,31,30,30,31,30,31,30,30", "calendar app: a 365-day year has months of 30 and 31 days, five of 31 (" + Lengths(365) + ")");
        Check(Lengths(8) == "1,1,1,1,1,1,1,1" && CalendarRules.MonthsIn(8) == 8, "calendar app: a year shorter than twelve days has a month a day");

        // The day each month ends on, the short line the player asked for.
        Check(CalendarRules.MonthEnd(CalendarRules.MonthOf(1, 60), 60) == 5 && CalendarRules.MonthEnd(CalendarRules.MonthOf(7, 60), 60) == 10
              && CalendarRules.MonthEnd(CalendarRules.MonthOf(60, 60), 60) == 60 && CalendarRules.MonthEnd(CalendarRules.MonthOf(61, 60), 60) == 65,
            "calendar app: in a 60-day year the months end on days 5, 10 ... 60, then 65 in the next year");
        Check(CalendarRules.MonthEnd(CalendarRules.MonthOf(1, 365), 365) == 31 && CalendarRules.MonthEnd(CalendarRules.MonthOf(32, 365), 365) == 61
              && CalendarRules.MonthEnd(CalendarRules.MonthOf(365, 365), 365) == 365 && CalendarRules.MonthOf(366, 365) == 12,
            "calendar app: in a 365-day year January ends on day 31, February on 61, December on 365, and day 366 is the next January");
        Check(CalendarRules.MonthOf(0, 60) == -1 && CalendarRules.MonthStart(-1, 60) == -4 && CalendarRules.YearOf(0, 60) == 0,
            "calendar app: a day before the first does not throw and is in the month before");
        Check(MonthViewRules.DaysLeft(58, 60) == 2 && MonthViewRules.DaysLeft(60, 60) == 0 && MonthViewRules.DaysLeft(61, 60) == -1, "calendar app: the days left to a month's end");
    }

    private static string Lengths(int year)
    {
        var parts = new List<string>();
        for (int m = 0; m < CalendarRules.MonthsIn(year); m++)
        {
            parts.Add(CalendarRules.MonthLength(m, year).ToString());
        }
        return string.Join(",", parts);
    }

    // ---------------------------------------------------------------- the grid

    private static void CalendarGridChecks()
    {
        Check(MonthViewRules.Weekday(1) == 0 && MonthViewRules.Weekday(7) == 6 && MonthViewRules.Weekday(8) == 0 && MonthViewRules.Weekday(0) == 6 && MonthViewRules.Weekday(-6) == 0,
            "calendar app: day 1 is a Monday, as the game's TimeHelper has it, and any day has a weekday");
        bool fits = true, once = true, cells = true;
        foreach (int year in new[] { 8, 12, 24, 60, 61, 90, 120, 180, 240, 360, 365 })
        {
            for (int month = 0; month < CalendarRules.MonthsIn(year) * 3; month++)
            {
                int start = CalendarRules.MonthStart(month, year), length = CalendarRules.MonthLength(month, year);
                int weeks = MonthViewRules.Weeks(start, length);
                fits &= weeks >= 1 && weeks <= MonthViewRules.MostWeeks && MonthViewRules.Lead(start) == MonthViewRules.Weekday(start);
                var seen = new int[length];
                for (int row = 0; row < MonthViewRules.MostWeeks; row++)
                {
                    for (int column = 0; column < MonthViewRules.Columns; column++)
                    {
                        int day = MonthViewRules.DayAt(start, length, row, column);
                        if (day == 0)
                        {
                            continue;
                        }
                        seen[day - start]++;
                        cells &= row < weeks && MonthViewRules.Weekday(day) == column;
                        cells &= MonthViewRules.CellOf(start, length, day, out int r, out int c) && r == row && c == column;
                    }
                }
                for (int i = 0; i < length; i++)
                {
                    once &= seen[i] == 1;
                }
            }
        }
        Check(fits, "calendar app: every month of every year length stands in one to six weeks, from its first day's weekday");
        Check(once, "calendar app: every day of a month is in exactly one cell of its grid");
        Check(cells, "calendar app: a day's cell is in its weekday's column, and its cell gives the day back");
        Check(!MonthViewRules.CellOf(10, 5, 9, out _, out _) && !MonthViewRules.CellOf(10, 5, 15, out _, out _) && MonthViewRules.DayAt(10, 5, 5, 6) == 0,
            "calendar app: a day before or after the month has no cell, and a cell after it has no day");

        Check(MonthViewRules.ChosenDay(12, 11, 15) == 12 && MonthViewRules.ChosenDay(20, 11, 15) == 11 && MonthViewRules.ChosenDay(3, 11, 15) == 11,
            "calendar app: a month opens on today in today's month, else on its first day");
        Check(MonthViewRules.Step(5, -1, 5) == 4 && MonthViewRules.Step(0, -1, 5) == 0 && MonthViewRules.Step(5, 1, 5) == 6
              && MonthViewRules.Step(5 + MonthViewRules.AheadMonths, 1, 5) == 5 + MonthViewRules.AheadMonths && MonthViewRules.Step(40, 0, 5) == 5 + MonthViewRules.AheadMonths,
            "calendar app: the months turn back to the save's first and on to " + MonthViewRules.AheadMonths + " months from today's, no further");
        Check(MonthViewRules.FirstFrom(10, 7, 3) == 10 && MonthViewRules.FirstFrom(10, 7, 10) == 10 && MonthViewRules.FirstFrom(10, 7, 11) == 17
              && MonthViewRules.FirstFrom(10, 7, 24) == 24 && MonthViewRules.FirstFrom(10, 7, 25) == 31 && MonthViewRules.FirstFrom(10, 0, 50) == 10,
            "calendar app: the first day from a day on of something that comes every so many days");
    }

    // ---------------------------------------------------------------- the days each schedule is marked on

    private static void CalendarSchedules()
    {
        foreach (int year in new[] { 60, 120, 365 })
        {
            int month = CalendarRules.MonthDays(year);

            // The game's tax year: its TaxHelper works the year's tax out on each day that is a whole number of years.
            Check(Same(day => day % year == 0, (from, to, into) => Every(MonthViewRules.FirstFrom(year, year, from), year, to, into), year * 3),
                "calendar app: the tax year ends on the days the game's TaxHelper closes it (" + year + "-day year)");

            // Power and water: billed when a month of days has gone since the utilities' month began (UtilitiesDirector.Day).
            foreach (int monthStart in new[] { 1, 3, 17 })
            {
                Check(Same(Utilities(monthStart, month), (from, to, into) => Every(MonthViewRules.FirstFrom(monthStart + month, month, from), month, to, into), year * 2),
                    "calendar app: the bills are marked on the days the utilities bill (month from day " + monthStart + ", " + year + "-day year)");
            }

            // The player's birthday: the game adds a day to their age each night and keeps a whole number of years.
            foreach (int age in new[] { 18 * year, 18 * year + 1, 30 * year - 1, 25 * year + year / 2 })
            {
                int today = 40;
                int days = age;
                var real = new HashSet<int>();
                for (int day = today; day <= today + year * 3; day++, days++)
                {
                    if (days % year == 0)
                    {
                        real.Add(day);
                    }
                }
                var marked = new HashSet<int>();
                Every(MonthViewRules.FirstFrom(MonthViewRules.OwnBirthdayFrom(today, age, year), year, today), year, today + year * 3, marked);
                Check(real.SetEquals(marked), "calendar app: the player's birthday is the day the game makes their age a whole number of years (" + age + " days, " + year + "-day year)");
            }

            // People's birthdays: a day of the year, as the people of the social life keep theirs.
            foreach (int birthday in new[] { 1, 2, year / 2, year })
            {
                Check(Same(day => (day - 1) % year + 1 == birthday, (from, to, into) => Every(MonthViewRules.DayOfYearFrom(birthday, from, year), year, to, into), year * 3),
                    "calendar app: a person's birthday on day " + birthday + " of a " + year + "-day year is marked on that day each year");
            }
        }

        // A child's birthday, as the family grows its children a day's share at a time in single-precision sums.
        bool children = true;
        string late = null;
        foreach (int weeks in new[] { 1, 2, 4, 8, 12 })
        {
            int yearDays = weeks * 7;
            float step = 1 / (float)yearDays;
            foreach (float start in new[] { 0f, 0.3f, 1f, 2.9f, 4.5f, 11.99f })
            {
                float age = start;
                for (int day = 0; day < yearDays * 3; day++)
                {
                    int predicted = MonthViewRules.ChildBirthdayIn(age, yearDays);
                    float a = age;
                    int real = 0;
                    for (int k = 1; k <= yearDays * 2 + 2 && real == 0; k++)
                    {
                        a += step;
                        real = (int)Math.Floor(a) > (int)Math.Floor(age) ? k : 0;
                    }
                    if (predicted != real)
                    {
                        children = false;
                        late = late ?? weeks + " weeks a year, age " + age + ": " + predicted + " against " + real;
                    }
                    age += step;
                }
            }
        }
        Check(children, "calendar app: a child's next birthday is the day the family's own sums make them a year older (" + (late ?? "every case") + ")");

        // A card: a statement every so many days from its next one (AccountRules.RunDay), its minimum taken three days later.
        foreach (int opened in new[] { 1, 4, 30 })
        {
            CreditAccount card = AccountRules.Open(RealLifeOverhaul.Economy.ProductKind.CreditCard, 5000, 1900, opened);
            card.Owed = 2000;
            var purse = new Purse();
            var real = new HashSet<int>();
            int next = card.NextDay;
            for (int day = opened + 1; day <= opened + 120; day++)
            {
                if (AccountRules.RunDay(card, day, purse, ProductCatalogue.Standard, false, "test").Statement)
                {
                    real.Add(day);
                }
            }
            var marked = new HashSet<int>();
            Every(MonthViewRules.FirstFrom(next, ProductCatalogue.CardStatementDays, opened + 1), ProductCatalogue.CardStatementDays, opened + 120, marked);
            Check(real.SetEquals(marked), "calendar app: a card's statements are marked on the days its account makes them (opened on day " + opened + ": " + real.Count + " statements)");
        }

        // A tech firm's managed service: billed when thirty days have gone since the last bill (FirmsRules.Work), its last day included.
        foreach (int billed in new[] { 3, 10 })
        {
            var firm = new Firm { Place = "p" };
            var w = new RealLifeOverhaul.Tech.Contract { Id = 1, Place = "p", Kind = (int)WorkKind.Managed, Stage = (int)WorkStage.Active, Value = 100000, Start = billed, LastBilled = billed, End = billed + 6 * FirmsCatalogue.MonthDays };
            int end = w.End, last = w.LastBilled;
            var real = new HashSet<int>();
            for (int day = billed + 1; day <= billed + 300; day++)
            {
                if (FirmsRules.Work(w, firm, FirmsCatalogue.ManagedPoints, day, null) > 0)
                {
                    real.Add(day);
                }
            }
            var marked = new HashSet<int>();
            for (int day = MonthViewRules.FirstFrom(last + FirmsCatalogue.MonthDays, FirmsCatalogue.MonthDays, billed + 1); day <= billed + 300 && day <= end; day += FirmsCatalogue.MonthDays)
            {
                marked.Add(day);
            }
            Check(real.SetEquals(marked) && real.Count == 6, "calendar app: a managed service's invoices are marked on the days its firm bills them, every 30 days whatever the year (" + real.Count + ")");
        }

        // The company's tax quarter: closed when a quarter of days has gone since it began (TaxRules.QuarterOver, Close).
        foreach (int monthDays in new[] { 5, 10, 30 })
        {
            int quarter = TaxRules.QuarterDays(monthDays);
            var t = new TaxFile { QuarterFrom = 7 };
            int from = t.QuarterFrom;
            var real = new HashSet<int>();
            for (int day = 8; day <= 8 + quarter * 5; day++)
            {
                if (TaxRules.QuarterOver(t, day, quarter))
                {
                    real.Add(day);
                    TaxRules.Close(t, 0, day);
                }
            }
            var marked = new HashSet<int>();
            Every(MonthViewRules.FirstFrom(from + quarter, quarter, 8), quarter, 8 + quarter * 5, marked);
            Check(real.SetEquals(marked), "calendar app: the tax quarter is marked on the days the company closes it (a month of " + monthDays + " days)");
        }

        // The central bank: a decision takes effect each time the city's model has counted a whole month (CityModel.Day).
        bool rates = true;
        foreach (int monthDays in new[] { 5, 10, 30 })
        {
            for (int age = 0; age < monthDays * 2; age++)
            {
                var state = new CityState { Age = age };
                int today = 100;
                int turn = today + CityModel.DaysToTurn(state, monthDays);
                int simulated = today, counted = age;
                do
                {
                    simulated++;
                    counted++;
                }
                while (counted % monthDays != 0);
                rates &= turn == simulated;
            }
        }
        Check(rates, "calendar app: the central bank's decisions are marked on the days the city's model turns its month");

        // Profit share: paid when (day - hired) is a week less a day (CareersDirector.WorkDayMore).
        Check(Same(day => day > 13 && (day - 13) % 7 == 6, (from, to, into) => Every(MonthViewRules.FirstFrom(13 + 6, 7, from), 7, to, into), 200),
            "calendar app: a profession's profit share is marked on the days it is paid");

        // The trading bots' statement: at the close of the last trading day of each of the calendar's months.
        foreach (int year in new[] { 60, 120, 365 })
        {
            bool bots = true;
            for (int month = 0; month < 24; month++)
            {
                int end = CalendarRules.MonthEnd(month, year), lastTrading = end;
                while (!MarketCatalogue.TradingDay(lastTrading))
                {
                    lastTrading--;
                }
                for (int day = CalendarRules.MonthStart(month, year); day <= end; day++)
                {
                    bool statement = MarketCatalogue.TradingDay(day) && MarketCatalogue.MonthOf(MarketTrading.NextTradingDay(day), year) != MarketCatalogue.MonthOf(day, year);
                    bots &= statement == (day == lastTrading);
                }
            }
            Check(bots, "calendar app: the trading bots' statement falls on the last trading day of each month of the calendar (" + year + "-day year)");
        }
    }

    // The days a feature's own daily rule fires on, from 1 to 'days', against those the calendar marks for the same days.
    private static bool Same(Func<int, bool> real, Action<int, int, HashSet<int>> calendar, int days)
    {
        var want = new HashSet<int>();
        for (int day = 1; day <= days; day++)
        {
            if (real(day))
            {
                want.Add(day);
            }
        }
        var marked = new HashSet<int>();
        // The calendar is asked a month at a time, as the app asks it: in stretches of uneven length.
        for (int from = 1, step = 5; from <= days; from += step, step = step % 31 + 3)
        {
            calendar(from, Math.Min(days, from + step - 1), marked);
        }
        return want.SetEquals(marked);
    }

    private static void Every(int first, int every, int to, HashSet<int> into)
    {
        for (int day = first; day <= to; day += Math.Max(1, every))
        {
            into.Add(day);
        }
    }

    // UtilitiesDirector.Day's month: billed on the day a month of days has gone since the month began, which starts the next.
    private static Func<int, bool> Utilities(int monthStart, int monthDays)
    {
        int start = monthStart;
        var billed = new HashSet<int>();
        for (int d = monthStart; d < monthStart + 2000; d++)
        {
            if (d + 1 - start >= monthDays)
            {
                billed.Add(d + 1);
                start = d + 1;
            }
        }
        return day => billed.Contains(day);
    }

    // ---------------------------------------------------------------- the list

    private static void CalendarListChecks()
    {
        var list = new CalendarList();
        list.Begin(11, 15);
        Check(list.Items.Count == 0 && list.Nights.Count == 0 && MonthViewRules.Tally(list.Items, 12).Count == 0 && MonthViewRules.CountOn(list.Items, 12) == 0,
            "calendar app: an empty month has nothing on any day and nothing every night");
        Check(list.Add("a", 1, 0, CalendarGroup.Money, 10) == null && list.Add("a", 1, 0, CalendarGroup.Money, 16) == null && list.Add("a", 1, 0, CalendarGroup.Money, 11) != null,
            "calendar app: a source cannot put anything outside the month asked for");
        list.Night("n", "rent", -1, 0f);
        list.Night("n", "rent", -1, -5f);
        list.Night("n", "wages", -1, 40f);
        Check(list.Nights.Count == 1 && list.Nights[0].Sign == -1 && list.Nights[0].Amount == 40f, "calendar app: a nightly line of nothing is left out");

        list.Begin(1, 31);
        CalendarEntry pay = list.Add("b", 2, 0, CalendarGroup.Money, 5);
        pay.Sign = 1;
        pay.Amount = 120f;
        CalendarEntry bill = list.Add("a", 1, 0, CalendarGroup.Money, 5);
        bill.Sign = -1;
        bill.Amount = 30f;
        bill.Start = 9 * 60;
        CalendarEntry shift = list.Add("c", 1, 0, CalendarGroup.Time, 5);
        shift.Start = 8 * 60;
        CalendarEntry party = list.Add("d", 1, 0, CalendarGroup.City, 4);
        CalendarEntry note = list.Add("e", 1, 0, CalendarGroup.Money, 5);
        list.Items.Sort(MonthViewRules.ByTime);
        Check(list.Items[0] == party && list.Items[1] == pay && list.Items[2] == note && list.Items[3] == shift && list.Items[4] == bill,
            "calendar app: by day, then what has no hour, then the hour, then money before time and the city");
        DayTally five = MonthViewRules.Tally(list.Items, 5);
        Check(five.In == 120f && five.Out == 30f && five.Money == 3 && five.Time == 1 && five.City == 0 && MonthViewRules.CountOn(list.Items, 5) == 4,
            "calendar app: a day adds up its money in and out, and counts its kinds");

        // Full: a source gone wrong cannot fill the month.
        list.Begin(1, 31);
        int added = 0;
        for (int i = 0; i < CalendarList.MostEntries + 50; i++)
        {
            added += list.Add("x", i, 0, CalendarGroup.Time, 1 + i % 31) != null ? 1 : 0;
        }
        Check(added == CalendarList.MostEntries, "calendar app: a look holds " + CalendarList.MostEntries + " entries at most");

        // Writing a busy month again and again keeps its entries: nothing is made after the first.
        FillMonth(list);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 20; i++)
        {
            FillMonth(list);
        }
        long made = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(made == 0, "calendar app: a look reuses its entries (" + made + " bytes made in 20 looks)");
        var watch = Stopwatch.StartNew();
        int tallied = TallyMonth(list);
        Check(watch.Elapsed.TotalMilliseconds < 20.0 && tallied > 0, "calendar app: a month of " + list.Items.Count + " entries is sorted and every cell added up in "
                                                                       + watch.Elapsed.TotalMilliseconds.ToString("0.00") + " ms (20 at most)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void FillMonth(CalendarList list)
    {
        list.Begin(1, 31);
        for (int i = 0; i < CalendarList.MostEntries; i++)
        {
            CalendarEntry e = list.Add("x", i, 0, (CalendarGroup)(i % 3), 1 + i * 7 % 31);
            e.Start = i % 5 == 0 ? -1 : i % 1440;
        }
        list.Night("n", "rent", -1, 10f);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static int TallyMonth(CalendarList list)
    {
        list.Items.Sort(MonthViewRules.ByTime);
        int count = 0;
        for (int row = 0; row < MonthViewRules.MostWeeks; row++)
        {
            for (int column = 0; column < MonthViewRules.Columns; column++)
            {
                int day = MonthViewRules.DayAt(1, 31, row, column);
                count += day > 0 ? MonthViewRules.Tally(list.Items, day).Count : 0;
            }
        }
        return count;
    }

    // ---------------------------------------------------------------- the sources and the app as written

    private static string CalendarScripts(string file)
    {
        return System.IO.File.ReadAllText(System.IO.Path.Combine(System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts")), file));
    }

    private static void CalendarSources()
    {
        string money = CalendarScripts(System.IO.Path.Combine("Wiring", "Wiring.CalendarMoney.cs"));
        string work = CalendarScripts(System.IO.Path.Combine("Wiring", "Wiring.CalendarWork.cs"));
        string people = CalendarScripts(System.IO.Path.Combine("Wiring", "Wiring.CalendarPeople.cs"));
        string links = CalendarScripts(System.IO.Path.Combine("Wiring", "Wiring.CalendarLinks.cs"));
        string game = CalendarScripts(System.IO.Path.Combine("Calendar", "CalendarGame.cs"));
        string director = CalendarScripts(System.IO.Path.Combine("Calendar", "CalendarDirector.cs"));
        string app = CalendarScripts(System.IO.Path.Combine("Calendar", "CalendarApp.cs"));
        Check(money.Contains("MonthViewRules.FirstFrom(state.MonthStart + month, month, fromDay)") && money.Contains("UtilitiesRules.DueIn(month)")
              && money.Contains("MonthViewRules.FirstFrom(next, ProductCatalogue.CardStatementDays, fromDay)") && money.Contains("record.InsuredUntil + 1")
              && money.Contains("shop.HostDue") && money.Contains("state.PaidUntil + 1"),
            "calendar app: the bills, cards, cars, hosting and the paper are read from each feature's own next day, by the rules checked above");
        Check(work.Contains("MonthViewRules.FirstFrom(Math.Max(today, w.LastBilled + every), every, fromDay)") && work.Contains("day <= w.End")
              && work.Contains("t.QuarterFrom + quarter") && work.Contains("CityModel.DaysToTurn(director.State, month)") && work.Contains("MonthViewRules.FirstFrom(j.Hired + 6, 7")
              && work.Contains("MarketCatalogue.MonthOf(MarketTrading.NextTradingDay(day), year) == MarketCatalogue.MonthOf(day, year)"),
            "calendar app: the invoices, the quarter, the central bank, the profit share and the bots' statements are read by the rules checked above");
        Check(people.Contains("MonthViewRules.DayOfYearFrom(p.Birthday, fromDay, year)") && people.Contains("MonthViewRules.ChildBirthdayIn(c.Age, year)")
              && people.Contains("Children.WeekCharges(") && game.Contains("MonthViewRules.OwnBirthdayFrom(today, data.ageInDays, year)")
              && game.Contains("MonthViewRules.FirstFrom(year, year, Math.Max(1, fromDay))"),
            "calendar app: birthdays, the children's week and the tax year are read by the rules checked above");
        // Read only: no source writes to what it reads, nothing sends a notification but the app's own, nothing is saved.
        string all = money + work + people + links + game;
        Check(!Regex.IsMatch(all, @"ChangeMoney|\.Take\(|\.Give\(|Notifications\.Show|modData\[|SaveHook"),
            "calendar app: its sources only read: no money moved, no notification, nothing saved");
        Check(links.Contains("\"tax\"") && !links.Contains("\"finance\"") && !links.Contains("\"estate\"") && !links.Contains("\"company\", ") && !links.Contains("\"sample\""),
            "calendar app: the Planner's dated sources are taken, its alerts (the Finance, property and company rows, the samples) are left to the Planner");
        Check(director.Contains("if (now < _next)") && director.Contains("_view.DayCheck(CalendarGame.Today)") && !director.Contains("void LateUpdate") && !director.Contains("void FixedUpdate"),
            "calendar app: its one Update is a timer comparison; an open page is read again once a new day begins");
        Check(app.Contains("_director.Look(_month)") && Regex.Matches(app, @"_director\.Look\(").Count == 1,
            "calendar app: the sources are read in one place, when a month is shown (opened, turned, a new day)");
        string pagers = CalendarScripts(System.IO.Path.Combine("Core", "GameUi", "PagerRules.cs"));
        Check(pagers.Contains("\"RealLifeFlare\", \"RealLifeCalendar\", \"RealLifeCamera\""), "calendar app: the Calendar has its place in the phone's order, beside the Planner and Flare");
    }

    // ---------------------------------------------------------------- the Planner's year

    // The Planner's day box once counted the year as the game's age does (day / daysPerYear), so it said the new year
    // began on day 60 of a 60-day year, the day the game closes its tax year. The year and its next first day are now
    // the calendar's alone (CalendarRules.YearOf, NextYearStart), checked here against the game's tax year: it is paid
    // on each day that is a multiple of the year's length (TaxHelper), its period beginning the day after.
    private static void CalendarPlannerYear()
    {
        bool agree = true;
        string first = null;
        for (int year = 8; year <= 400 && agree; year++)
        {
            for (int day = 1; day <= year * 3; day++)
            {
                int taxDay = (day + year - 1) / year * year;   // the game's next tax day from this one, this day included
                int next = CalendarRules.NextYearStart(day, year);
                bool ok = next == taxDay + 1 && CalendarRules.YearOf(day, year) == CalendarRules.YearOf(taxDay, year)
                          && CalendarRules.YearOf(next, year) == CalendarRules.YearOf(day, year) + 1 && CalendarRules.YearOf(taxDay, year) == taxDay / year;
                if (!ok)
                {
                    agree = false;
                    first = year + "-day year, day " + day + ": next year from day " + next + ", tax day " + taxDay;
                    break;
                }
            }
        }
        Check(agree, "calendar app: the year's end is the game's tax day and the next year begins the day after, for every year length from 8 to 400 days (" + (first ?? "none") + ")");
        Check(CalendarRules.YearOf(60, 60) == 1 && CalendarRules.NextYearStart(60, 60) == 61 && CalendarRules.YearOf(61, 60) == 2 && CalendarRules.NextYearStart(1, 60) == 61
              && CalendarRules.NextYearStart(61, 120) == 121 && CalendarRules.NextYearStart(365, 365) == 366,
            "calendar app: in a 60-day year day 60 is still the first year and the second begins on day 61 (" + CalendarRules.NextYearStart(60, 60) + ")");
        string box = CalendarScripts(System.IO.Path.Combine("Planner", "PlannerApp.Box.cs"));
        string model = CalendarScripts(System.IO.Path.Combine("Planner", "PlannerModel.cs"));
        Check(box.Contains("CalendarRules.YearOf(today, perYear)") && box.Contains("CalendarRules.NextYearStart(today, perYear)")
              && box.Contains("\"year\", year.ToString()") && !Regex.IsMatch(box + model, @"/\s*(daysPerYear|perYear)\b"),
            "calendar app: the Planner's day box reads the year and the new year's first day from the calendar's rule, and counts no year of its own");
    }

    // ---------------------------------------------------------------- the texts

    private static void CalendarTexts()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("calendar app", words, codes))
        {
            return;
        }
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        var files = new List<string>(System.IO.Directory.GetFiles(System.IO.Path.Combine(folder, "Calendar"), "*.cs"));
        files.AddRange(System.IO.Directory.GetFiles(System.IO.Path.Combine(folder, "Wiring"), "Wiring.Calendar*.cs"));
        foreach (string file in files)
        {
            foreach (Match m in Regex.Matches(System.IO.File.ReadAllText(file), "\"(reallife_[a-z0-9_]+)\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        foreach (string key in new[] { "reallife_opt_tip_calendar", "reallife_opt_sec_calendar" })
        {
            keys.Add(key);
        }
        var lacking = new List<string>();
        foreach (string key in keys)
        {
            string missing = LanguagesLacking(words, codes, key);
            if (missing.Length > 0)
            {
                lacking.Add(key + " (" + missing + ")");
            }
        }
        Check(keys.Count > 100 && lacking.Count == 0, "calendar app: each of its " + keys.Count + " texts is in every language with the English's names in curly brackets ("
                                                    + (lacking.Count == 0 ? "all" : string.Join(", ", lacking)) + ")");
        Dictionary<string, string> en = words[0];
        bool plain = true;
        foreach (string key in keys)
        {
            if (en.TryGetValue(key, out string text) && (text.Contains("—") || text.Contains("–") || text.Contains(" - ") || text.Contains("!")))
            {
                plain = false;
            }
        }
        Check(plain, "calendar app: its English has no dashes and no exclamation marks");
        Check(en["reallife_calendar_test_done"].Length <= 240 && en["reallife_calendar_app_gone"].Length <= 240, "calendar app: its notifications are at most 240 characters");
        Check(en["reallife_calendar_headline_now"].Contains("This month ends on day {day}."), "calendar app: the line over the month says the day this month ends on");
        Check(en["reallife_guide_calendar"].Contains("{calendar_ahead}") && en["reallife_guide_calendar"].Contains("30 or 31 in a 365-day year"),
            "calendar app: the Guide's page says the months' lengths and fills the months ahead from the code");
        for (int l = 0; l < words.Length && l < 3; l++)
        {
            Check(AnyChangesRow(words[l], l == 0 ? "A Calendar app on the phone" : l == 1 ? "Calendário" : "日历"), "calendar app: What's new says so in " + codes[l]);
        }
    }
}
