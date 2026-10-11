using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

// The third part of the economy's checks: the city's economy (Scripts/Economy/City). The model is run for twenty
// years from many seeds at every strength and every figure is held to the bounds the catalogue states; then the
// central bank's rule, the adjustment arithmetic, the rate the credit side reads, the save's line and the surface
// other features read are checked one by one. Every figure expected here is worked out by hand from the catalogue.
internal static partial class Program
{
    private const int CitySeeds = 60;
    private const int CityYears = 20;

    // The days outside the model, made up but always the same: a season by the day of the year, now and then some weather and an event.
    private static CityDay Outside(int day, int daysPerYear)
    {
        int part = (day - 1) % daysPerYear * 100 / daysPerYear;
        return new CityDay
        {
            Season = part < 17 || part >= 92 ? 0 : part < 42 ? 1 : part < 67 ? 2 : 3,
            Weather = (CityWeather)(day % 11 == 0 ? 1 + day / 11 % 4 : 0),
            Event = (CityEvent)(day % 7 == 6 ? 2 : day % 13 == 0 ? 1 + day / 13 % 7 : 0)
        };
    }

    private static CityState NewCity(int seed)
    {
        CityState s = CityModel.Open(seed, 1);
        CityModel.Know(s, "ba:neighborhood_a", CityModel.StartIncome(80, 15, 5));
        CityModel.Know(s, "ba:neighborhood_b", CityModel.StartIncome(30, 50, 20));
        CityModel.Know(s, "ba:neighborhood_c", CityModel.StartIncome(5, 25, 70));
        CityModel.Know(s, "ba:neighborhood_d", CityModel.StartIncome(0, 0, 0));
        return s;
    }

    private static void City()
    {
        CityCatalogueChecks();
        CitySimulation(60);
        CitySimulation(365);
        CityRule();
        CityFootfall();
        CityDistricts();
        CityAdjustments();
        CityRateHook();
        CitySaveAndLoad();
        CityDeterminism();
        CitySurface();
    }

    private static void CityCatalogueChecks()
    {
        Check(MacroCatalogue.MonthDays(60) == 5 && MacroCatalogue.MonthDays(365) == 30 && MacroCatalogue.MonthDays(8) == 3 && MacroCatalogue.MonthDays(24) == 3, "a month is a twelfth of the year, three days at least");
        Check(MacroCatalogue.Swing(MacroCatalogue.Relaxed) == 50 && MacroCatalogue.Swing(MacroCatalogue.Standard) == 100 && MacroCatalogue.Swing(MacroCatalogue.Hardcore) == 125
              && MacroCatalogue.Swing(-3) == 50 && MacroCatalogue.Swing(9) == 125, "Relaxed halves every swing, Hardcore widens it by a quarter, and a level out of range is the nearest one");
        Check(!MacroCatalogue.Recessions(MacroCatalogue.Relaxed) && MacroCatalogue.Recessions(MacroCatalogue.Standard) && MacroCatalogue.Recessions(MacroCatalogue.Hardcore), "only Relaxed has no recessions");
        bool rows = MacroCatalogue.Transition.Length == MacroCatalogue.StateCount && MacroCatalogue.JitterPair.Length == MacroCatalogue.StateCount;
        for (int r = 0; rows && r < MacroCatalogue.StateCount; r++)
        {
            int sum = 0;
            foreach (int chance in MacroCatalogue.Transition[r])
            {
                sum += chance;
            }
            int a = MacroCatalogue.Transition[r][MacroCatalogue.JitterPair[r][0]], b = MacroCatalogue.Transition[r][MacroCatalogue.JitterPair[r][1]];
            rows &= sum == 100 && MacroCatalogue.Transition[r].Length == MacroCatalogue.StateCount && a > MacroCatalogue.TransitionJitter && b > MacroCatalogue.TransitionJitter;
        }
        Check(rows, "every row of the transition table adds up to 100, and the save's jitter can never empty or overfill an outcome");
        Check(MacroCatalogue.Transition[(int)CycleState.Boom][(int)CycleState.Recession] == 0 && MacroCatalogue.Transition[(int)CycleState.Steady][(int)CycleState.Recession] == 0,
            "a recession only ever follows a slowdown");
        bool tables = MacroCatalogue.SeasonPush.Length == MacroCatalogue.SupplyKinds && MacroCatalogue.WeatherPush.Length == MacroCatalogue.SupplyKinds && MacroCatalogue.EventPush.Length == MacroCatalogue.SupplyKinds
                      && MacroCatalogue.KindOfType.Length == MacroCatalogue.KindOfTypeIs.Length;
        for (int k = 0; tables && k < MacroCatalogue.SupplyKinds; k++)
        {
            tables &= MacroCatalogue.SeasonPush[k].Length == 4 && MacroCatalogue.WeatherPush[k].Length == 5 && MacroCatalogue.EventPush[k].Length == 8;
        }
        Check(tables, "supply's tables have a row for every kind of goods and a figure for every season, weather and event");
        Check(MacroCatalogue.TypeRank("ba:businesstype_fruitandvegetablestore") >= 0 && MacroCatalogue.KindOfTypeIs[MacroCatalogue.TypeRank("ba:businesstype_fruitandvegetablestore")] == MacroCatalogue.Fresh
              && MacroCatalogue.KindOfTypeIs[MacroCatalogue.TypeRank("ba:businesstype_supermarket")] == MacroCatalogue.Groceries
              && MacroCatalogue.KindOfTypeIs[MacroCatalogue.TypeRank("ba:businesstype_electronicsstore")] == MacroCatalogue.Electronics
              && MacroCatalogue.TypeRank("ba:businesstype_fruitandvegetablestore") < MacroCatalogue.TypeRank("ba:businesstype_supermarket")
              && MacroCatalogue.TypeRank("ba:businesstype_gasstation") == -1 && MacroCatalogue.TypeRank(null) == -1,
            "a business type's goods are of the kind its name says, a specialist before the supermarket, and anything else is general goods");
        Check(1000 + MacroCatalogue.ShortageMost * MacroCatalogue.Swing(MacroCatalogue.Hardcore) / 100 == MacroCatalogue.FactorMost
              && 1000 - MacroCatalogue.GlutMost * MacroCatalogue.Swing(MacroCatalogue.Hardcore) / 100 == MacroCatalogue.FactorLeast,
            "the largest shortage and the largest glut at Hardcore are exactly the factor's bounds: 1.5 and 0.7");
        Check(CityModel.StartIncome(100, 0, 0) == 700 && CityModel.StartIncome(0, 100, 0) == 1000 && CityModel.StartIncome(0, 0, 100) == 1350 && CityModel.StartIncome(0, 0, 0) == 1000
              && CityModel.StartIncome(50, 30, 20) == 920, "a district starts at the mean of 70, 100 and 135 by the game's class shares, and at 100 where the game gives none");
    }

    // Twenty years from many seeds at every strength: every figure stays inside its bounds on every day.
    private static void CitySimulation(int daysPerYear)
    {
        int monthDays = MacroCatalogue.MonthDays(daysPerYear);
        int days = daysPerYear * CityYears;
        bool rate = true, moves = true, inflation = true, index = true, monthChange = true, opening = true, relaxed = true, months = true, shortRun = true;
        bool income = true, band = true, factor = true, atOnce = true, ahead = true, supplyNews = true, decisionNews = true, decisionEffect = true, newsADay = true, oneADay = true;
        int recessions = 0, booms = 0, shortages = 0, gluts = 0, ratesUp = 0, ratesDown = 0, highestBase = 0, lowestBase = 0;
        int highestInflation = MacroCatalogue.InflationTarget, lowestInflation = MacroCatalogue.InflationTarget;
        var was = new SupplyLine[MacroCatalogue.SupplyKinds];
        for (int level = MacroCatalogue.Relaxed; level <= MacroCatalogue.Hardcore; level++)
        {
            for (int seed = 1; seed <= CitySeeds; seed++)
            {
                CityState s = NewCity(seed * 7919 + level);
                int run = 0;
                for (int day = 2; day <= days + 1; day++)
                {
                    int baseBefore = s.Base;
                    bool pendingBefore = s.Pending;
                    int moveBefore = s.PendingMove;
                    CycleState cycleBefore = s.Cycle;
                    Array.Copy(s.Supply, was, was.Length);
                    int ageBefore = s.Age;
                    CityTick tick = CityModel.Day(s, day, Outside(day, daysPerYear), level, monthDays);

                    rate &= s.Base >= MacroCatalogue.BaseLeast && s.Base <= MacroCatalogue.BaseMost;
                    highestBase = Math.Max(highestBase, s.Base);
                    lowestBase = Math.Min(lowestBase, s.Base);
                    inflation &= s.Inflation >= MacroCatalogue.InflationLeast && s.Inflation <= MacroCatalogue.InflationMost;
                    highestInflation = Math.Max(highestInflation, s.Inflation);
                    lowestInflation = Math.Min(lowestInflation, s.Inflation);
                    index &= s.Index >= MacroCatalogue.IndexLeast && s.Index <= MacroCatalogue.IndexMost;
                    months &= s.MonthsLeft >= 1 && s.MonthsLeft <= MacroCatalogue.StateMonthsMost && s.MonthsIn >= 1;
                    if (CityModel.MonthNumber(s, monthDays) < MacroCatalogue.OpeningSteadyMonths)
                    {
                        opening &= s.Cycle == CycleState.Steady;
                    }
                    relaxed &= level != MacroCatalogue.Relaxed || s.Cycle != CycleState.Recession;
                    if (tick.MonthTurned)
                    {
                        // The decision announced the day before is exactly what took effect, one point at most.
                        decisionEffect &= pendingBefore && s.Base == MacroCatalogue.Clamp(baseBefore + moveBefore, MacroCatalogue.BaseLeast, MacroCatalogue.BaseMost)
                                          && tick.RateMoved == s.Base - baseBefore && !s.Pending;
                        moves &= Math.Abs(s.Base - baseBefore) <= MacroCatalogue.MoveMost;
                        ratesUp += s.Base > baseBefore ? 1 : 0;
                        ratesDown += s.Base < baseBefore ? 1 : 0;
                        monthChange &= Math.Abs(s.MonthChange * MacroCatalogue.MonthsPerYear - s.Inflation) <= MacroCatalogue.MonthsPerYear / 2;
                        run++;
                        if (s.Cycle != cycleBefore)
                        {
                            shortRun &= run >= MacroCatalogue.StateMonthsLeast && tick.CycleChanged;
                            run = 0;
                            recessions += s.Cycle == CycleState.Recession ? 1 : 0;
                            booms += s.Cycle == CycleState.Boom ? 1 : 0;
                        }
                    }
                    else
                    {
                        moves &= s.Base == baseBefore;   // the rate moves at the turn of a month and at no other time
                    }
                    bool lastDay = (s.Age + 1) % monthDays == 0;
                    decisionNews &= lastDay == s.Pending && lastDay == HasNews(s, day, CityNewsKind.RateDecision);
                    for (int i = 0; i < s.Districts.Count; i++)
                    {
                        CityDistrict d = s.Districts[i];
                        income &= d.Income >= MacroCatalogue.IncomeLeast && d.Income <= MacroCatalogue.IncomeMost;
                        band &= Math.Abs(d.Income - d.Start) <= MacroCatalogue.IncomeBand;
                    }
                    int busy = 0, drawn = 0;
                    for (int k = 0; k < MacroCatalogue.SupplyKinds; k++)
                    {
                        SupplyLine line = s.Supply[k];
                        factor &= line.Factor >= MacroCatalogue.FactorLeast && line.Factor <= MacroCatalogue.FactorMost;
                        busy += line.Shock != SupplyShock.None ? 1 : 0;
                        if (line.Running(s.Age) && line.From == s.Age)
                        {
                            // It starts today: yesterday it stood announced for today, with the same size and days.
                            ahead &= was[k].Shock == line.Shock && was[k].From == s.Age && was[k].Announced(ageBefore) && was[k].Days == line.Days && was[k].Size == line.Size;
                            shortages += line.Shock == SupplyShock.Shortage ? 1 : 0;
                            gluts += line.Shock == SupplyShock.Glut ? 1 : 0;
                        }
                        if (line.Announced(s.Age) && !was[k].Announced(ageBefore))
                        {
                            drawn++;
                            supplyNews &= tick.ShockAnnounced == k && HasNews(s, day, CityNewsKind.Supply) && line.From == s.Age + 1
                                          && line.Days >= MacroCatalogue.ShockDaysLeast && line.Days <= MacroCatalogue.ShockDaysMost;
                        }
                    }
                    atOnce &= busy <= MacroCatalogue.ShocksAtOnce;
                    oneADay &= drawn <= 1 && (drawn == 1) == (tick.ShockAnnounced >= 0);
                    for (int i = 0; i < s.News.Count; i++)
                    {
                        newsADay &= s.News[i].Day == day;   // nothing out of date is kept
                    }
                }
            }
        }
        string year = " (a year of " + daysPerYear + " days, " + CitySeeds + " seeds at each strength, " + CityYears + " years)";
        Check(rate, "the bank rate stays between -2 and +6 points" + year);
        Check(moves, "the bank rate moves only at the turn of a month, and by a point at most" + year);
        Check(decisionEffect, "what the central bank announced the day before is exactly what takes effect" + year);
        Check(decisionNews, "a decision is announced, with its piece for the paper, on the last day of every month and on no other day" + year);
        Check(inflation, "inflation stays between -1% and +12% a year" + year);
        Check(index, "the price index stays inside its bounds" + year);
        Check(monthChange, "a month's rise in prices is a twelfth of the year's inflation" + year);
        Check(opening, "the first three months are steady" + year);
        Check(relaxed, "Relaxed is never in a recession" + year);
        Check(months && shortRun, "a state runs two months at least before the next, and never has more than six months left" + year);
        Check(income && band, "every district's income stays between 60 and 140 and within 20 points of its start" + year);
        Check(factor, "every price factor stays between 0.7 and 1.5" + year);
        Check(atOnce && oneADay, "never more than two shortages or gluts at once, and never more than one drawn a day" + year);
        Check(ahead, "every shortage and glut that starts was announced the day before, unchanged" + year);
        Check(supplyNews, "every shortage and glut drawn has its piece in that day's paper and starts the next day, for three to eight days" + year);
        Check(newsADay, "no piece for the paper is kept past its day" + year);
        Check(recessions > 0 && booms > 0 && shortages > 0 && gluts > 0 && ratesUp > 0 && ratesDown > 0,
            "the runs had recessions (" + recessions + "), booms (" + booms + "), shortages (" + shortages + "), gluts (" + gluts + ") and rate moves up (" + ratesUp + ") and down (" + ratesDown + ")" + year);
        Check(highestBase > MacroCatalogue.BaseStart && lowestBase < MacroCatalogue.BaseStart && highestInflation > MacroCatalogue.InflationTarget + MacroCatalogue.InflationBand
              && lowestInflation < MacroCatalogue.InflationTarget - MacroCatalogue.InflationBand,
            "the rate went both sides of the banks' own (" + lowestBase + " to " + highestBase + " quarter points) and inflation both sides of its target (" + lowestInflation + " to " + highestInflation + ")" + year);
    }

    private static bool HasNews(CityState s, int day, CityNewsKind kind)
    {
        for (int i = 0; i < s.News.Count; i++)
        {
            if (s.News[i].Day == day && s.News[i].Kind == kind)
            {
                return true;
            }
        }
        return false;
    }

    // The central bank's rule, case by case.
    private static void CityRule()
    {
        RateReason why;
        Check(CityModel.Rule(200, CycleState.Steady, 0, out why) == 0 && why == RateReason.Hold, "on target, steady, at the banks' own: held");
        Check(CityModel.Rule(250, CycleState.Steady, 0, out why) == 0 && CityModel.Rule(150, CycleState.Steady, 0, out why) == 0, "half a point either side of the target is on target");
        Check(CityModel.Rule(251, CycleState.Steady, 0, out why) == 1 && why == RateReason.PricesHigh, "just over the band: up a quarter point, for prices");
        Check(CityModel.Rule(400, CycleState.Boom, 0, out why) == 2 && CityModel.Rule(520, CycleState.Steady, 0, out why) == 3, "a quarter point for each whole point over the target");
        Check(CityModel.Rule(1200, CycleState.Steady, 0, out why) == MacroCatalogue.MoveMost, "never more than a point at once");
        Check(CityModel.Rule(100, CycleState.Steady, 0, out why) == -1 && why == RateReason.PricesLow && CityModel.Rule(-100, CycleState.Steady, 4, out why) == -3, "under the band: cut, for prices");
        Check(CityModel.Rule(200, CycleState.Slowdown, 0, out why) == -1 && why == RateReason.Slowdown, "on target in a slowdown: cut a quarter point");
        Check(CityModel.Rule(200, CycleState.Recession, 0, out why) == -2 && why == RateReason.Recession, "on target in a recession: cut half a point");
        Check(CityModel.Rule(300, CycleState.Slowdown, 0, out why) == 0 && why == RateReason.Hold, "a slowdown takes its quarter point off a rise for prices");
        Check(CityModel.Rule(600, CycleState.Recession, 0, out why) == 2 && why == RateReason.PricesHigh, "prices far over the target still raise the rate in a recession, by less");
        Check(CityModel.Rule(200, CycleState.Steady, 6, out why) == -1 && why == RateReason.Normal && CityModel.Rule(200, CycleState.Boom, -6, out why) == 1 && why == RateReason.Normal,
            "with nothing pressing the rate goes a quarter point back towards the banks' own");
        Check(CityModel.Rule(1200, CycleState.Steady, MacroCatalogue.BaseMost, out why) == 0 && why == RateReason.Hold && CityModel.Rule(1200, CycleState.Steady, MacroCatalogue.BaseMost - 1, out why) == 1,
            "at +6 points it cannot rise, and one quarter under it rises by that quarter only");
        Check(CityModel.Rule(-100, CycleState.Recession, MacroCatalogue.BaseLeast, out why) == 0 && CityModel.Rule(-100, CycleState.Recession, MacroCatalogue.BaseLeast + 2, out why) == -2,
            "at -2 points it cannot fall, and a cut stops at the floor");
        bool up = true, down = true;
        for (int inflation = MacroCatalogue.InflationLeast; inflation <= MacroCatalogue.InflationMost; inflation += 7)
        {
            for (int at = MacroCatalogue.BaseLeast; at <= MacroCatalogue.BaseMost; at++)
            {
                int gap = inflation - MacroCatalogue.InflationTarget;
                int steady = CityModel.Rule(inflation, CycleState.Steady, at, out why), boom = CityModel.Rule(inflation, CycleState.Boom, at, out why);
                int slow = CityModel.Rule(inflation, CycleState.Slowdown, at, out why), recession = CityModel.Rule(inflation, CycleState.Recession, at, out why);
                if (gap > MacroCatalogue.InflationBand && at < MacroCatalogue.BaseMost)
                {
                    up &= steady > 0 && boom > 0;
                }
                if (gap <= MacroCatalogue.InflationBand && at > MacroCatalogue.BaseLeast)
                {
                    down &= slow < 0 && recession < 0;
                }
                down &= slow <= steady && recession <= slow;
                up &= Math.Abs(steady) <= MacroCatalogue.MoveMost && Math.Abs(recession) <= MacroCatalogue.MoveMost;
            }
        }
        Check(up, "over the whole range: with inflation over its band the rate rises in a boom and in steady times, unless it is at its most");
        Check(down, "over the whole range: with inflation not over its band the rate falls in a slowdown and a recession, unless it is at its least; and bad times never ask for a higher rate than good ones");
    }

    private static void CityFootfall()
    {
        Check(CityModel.PriceFootfall(0) == 0, "no prices to judge by: no change");
        Check(CityModel.PriceFootfall(1000) == MacroCatalogue.UnderGain && CityModel.PriceFootfall(800) == MacroCatalogue.UnderGain, "at or under what the district bears: two per cent more customers");
        Check(CityModel.PriceFootfall(1001) == 0 && CityModel.PriceFootfall(1020) == 0 && !CityModel.Over(1020) && CityModel.Over(1021), "up to two per cent over is not over");
        Check(CityModel.PriceFootfall(1050) == -4 && CityModel.PriceFootfall(1100) == -8 && CityModel.PriceFootfall(1125) == -10, "0.8 per cent of customers for each per cent over");
        Check(CityModel.PriceFootfall(1500) == -MacroCatalogue.OverLossMost && CityModel.PriceFootfall(9999) == -MacroCatalogue.OverLossMost, "ten per cent at most");
        Check(CityModel.Against(1100, 1100) == 1000 && CityModel.Against(1100, 1000) == 1100 && CityModel.Against(1000, 930) == 1075, "prices are judged against what the district bears");
        Check(CityModel.Footfall(-12, -4, -10) == MacroCatalogue.FootfallLeast && CityModel.Footfall(7, 4, 2) == 13 && CityModel.Footfall(20, 4, 2) == MacroCatalogue.FootfallMost,
            "district, cycle and prices together stay between -25% and +15%");
        Check(CityModel.Footfall(0, 1, 0) == 0 && CityModel.Footfall(0, -1, 0) == 0 && CityModel.Footfall(0, 2, 0) == 2 && CityModel.Footfall(-3, 1, 0) == -2, "a change under two per cent either way is none");
        var low = new CityDistrict { Start = 700, Income = 600 };
        var high = new CityDistrict { Start = 1200, Income = 1400 };
        var even = new CityDistrict { Start = 1000, Income = 1000 };
        Check(CityModel.DistrictFootfall(low) == -4 && CityModel.DistrictFootfall(high) == 4 && CityModel.DistrictFootfall(even) == 0 && CityModel.DistrictFootfall(null) == 0
              && CityModel.DistrictFootfall(new CityDistrict { Income = 1150 }) == 2, "a district is worth a tenth of a per cent of customers for each point away from 100");
        Check(CityModel.CycleFootfall(CycleState.Recession, MacroCatalogue.Standard) == -12 && CityModel.CycleFootfall(CycleState.Recession, MacroCatalogue.Relaxed) == -6
              && CityModel.CycleFootfall(CycleState.Recession, MacroCatalogue.Hardcore) == -15 && CityModel.CycleFootfall(CycleState.Boom, MacroCatalogue.Standard) == 6
              && CityModel.CycleFootfall(CycleState.Steady, MacroCatalogue.Hardcore) == 0, "the cycle's customers by state and strength");
        Check(CityModel.Spend(CycleState.Recession, MacroCatalogue.Standard) == 930 && CityModel.Spend(CycleState.Recession, MacroCatalogue.Relaxed) == 965
              && CityModel.Spend(CycleState.Boom, MacroCatalogue.Hardcore) == 1050 && CityModel.Spend(CycleState.Steady, MacroCatalogue.Relaxed) == 1000, "what customers spend by state and strength");
        Check(CityModel.Trouble(CycleState.Recession, MacroCatalogue.Standard) == 1350 && CityModel.Trouble(CycleState.Boom, MacroCatalogue.Standard) == 850
              && CityModel.Trouble(CycleState.Steady, MacroCatalogue.Hardcore) == 1000, "the chance of a business trouble by state");
        var s = new CityState { Cycle = CycleState.Recession };
        Check(CityModel.Tolerance(s, low, MacroCatalogue.Standard) == 950 * 930 / 1000 && CityModel.Tolerance(s, even, MacroCatalogue.Standard) == 930
              && CityModel.Tolerance(new CityState(), high, MacroCatalogue.Standard) == 1100 && CityModel.Tolerance(new CityState(), null, MacroCatalogue.Standard) == 1000,
            "a district bears half a per cent more for each point over its start, times what customers spend");
        s.Index = 110000;
        s.Supply[MacroCatalogue.Fresh].Factor = 1300;
        Check(CityModel.Costs(s) == 1100 && CityModel.Takings(s, MacroCatalogue.Standard) == 1100 * 930 / 1000 && CityModel.Wholesale(s, MacroCatalogue.Fresh) == 1430
              && CityModel.Wholesale(s, MacroCatalogue.General) == 1100, "rents and wages carry the index, takings the index and what customers spend, a wholesale price its kind's factor and the index");
        Check(CityModel.Property(s, high, MacroCatalogue.Standard) == (int)(850L * 1080 / 1000 * 110000 / 100000) && CityModel.Property(new CityState(), null, MacroCatalogue.Standard) == 1000,
            "a property's worth carries the cycle, the district against its start and the index");
    }

    // A district pushed to the edge of its band comes back towards its start in steady times, and the mean of many stays near it.
    private static void CityDistricts()
    {
        const int monthDays = 5;
        long far = 0, near = 0;
        int count = 0;
        bool closer = true;
        for (int seed = 1; seed <= 200; seed++)
        {
            CityState s = NewCity(seed * 104729);
            s.MonthsLeft = MacroCatalogue.StateMonthsMost;
            for (int i = 0; i < s.Districts.Count; i++)
            {
                CityDistrict d = s.Districts[i];
                d.Income = MacroCatalogue.Clamp(d.Start + (i % 2 == 0 ? MacroCatalogue.IncomeBand : -MacroCatalogue.IncomeBand), MacroCatalogue.IncomeLeast, MacroCatalogue.IncomeMost);
                far += Math.Abs(d.Income - d.Start);
            }
            for (int day = 2; day < 2 + monthDays * 12; day++)
            {
                s.Cycle = CycleState.Steady;      // held steady for the year
                s.MonthsLeft = MacroCatalogue.StateMonthsMost;
                CityModel.Day(s, day, new CityDay { Season = 1 }, MacroCatalogue.Standard, monthDays);
            }
            for (int i = 0; i < s.Districts.Count; i++)
            {
                CityDistrict d = s.Districts[i];
                near += Math.Abs(d.Income - d.Start);
                count++;
                closer &= Math.Abs(d.Income - d.Start) < MacroCatalogue.IncomeBand / 2;
            }
        }
        Check(closer, "after a steady year every district that stood at the edge of its band is within ten points of its start");
        Check(near * 4 < far && near / count < 50, "and on average within five points of it (" + (near / (float)count / 10f).ToString("0.0") + " points, from " + (far / (float)count / 10f).ToString("0.0") + ")");
        CityState known = NewCity(5);
        known.Districts[0].Income = 1390;
        CityModel.Know(known, "ba:neighborhood_a", 760);
        Check(known.Districts.Count == 4 && known.Districts[0].Start == 760 && known.Districts[0].Income == 960, "a district the save knows keeps its income, inside the band of the start the game gives now");
    }

    private static void CityAdjustments()
    {
        Check(CityAdjust.Of(1000f, 1042) == 42 && CityAdjust.Of(1000f, 958) == -42, "the game's figure times how far the factor is from one");
        Check(CityAdjust.Of(1000f, 1009) == 0 && CityAdjust.Of(1000f, 991) == 0 && CityAdjust.Of(50000f, 1005) == 0, "within 1% of one, nothing");
        Check(CityAdjust.Of(1000f, 1010) == 10 && CityAdjust.Of(1000f, 990) == -10, "at 1% it begins");
        Check(CityAdjust.Of(125f, 1100) == 13 && CityAdjust.Of(125f, 900) == -13 && CityAdjust.Of(124f, 1100) == 12 && CityAdjust.Of(1f, 1400) == 0 && CityAdjust.Of(1f, 1500) == 1,
            "whole money, halves away from nothing");
        Check(CityAdjust.Of(0f, 1500) == 0 && CityAdjust.Of(-80f, 1500) == 0, "nothing of nothing");
        Check(CityAdjust.Of(3e9f, 3000) == int.MaxValue / 2, "a figure too large to count is held to what can be counted");
        int carried = 0;
        Check(CityAdjust.Take(100, 1000, ref carried) == 100 && carried == 0, "a charge the cash covers is taken whole");
        Check(CityAdjust.Take(100, 60, ref carried) == 60 && carried == 40, "cash short: what there is is taken and the rest carried");
        Check(CityAdjust.Take(50, 0, ref carried) == 0 && carried == 90 && CityAdjust.Take(0, -500, ref carried) == 0 && carried == 90, "no cash, or cash under nothing: nothing is taken, everything carried, nothing added");
        Check(CityAdjust.Take(10, 1000, ref carried) == 100 && carried == 0, "what was carried is taken with the next charge the cash covers, as it was");
        carried = 40;
        Check(CityAdjust.Take(-30, 1000, ref carried) == 10 && carried == 0, "a refund smaller than what was carried pays part of it off");
        carried = 40;
        Check(CityAdjust.Take(-50, 0, ref carried) == -10 && carried == 0, "a refund larger than what was carried pays it off and the rest is paid out");
        carried = 0;
        Check(CityAdjust.Take(-25, 0, ref carried) == -25 && carried == 0 && CityAdjust.Take(0, 500, ref carried) == 0, "a refund with nothing carried is paid out whole; nothing due takes nothing");
        // Thirty days of a charge of 100 with cash that comes and goes: what is taken and what is carried always add up to what was due.
        carried = 0;
        long due = 0, taken = 0;
        bool never = true;
        for (int day = 0; day < 30; day++)
        {
            int cash = day % 4 == 0 ? 370 : day % 4 == 1 ? 0 : 55;
            int take = CityAdjust.Take(100, cash, ref carried);
            never &= take <= cash && take >= 0;
            due += 100;
            taken += take;
        }
        Check(never && due == taken + carried, "over thirty days nothing is taken beyond the cash in hand, and nothing is lost or added: " + taken + " taken, " + carried + " carried of " + due);
    }

    // The rate the credit side reads: nothing while the city's economy is off, and added before the rate is rounded and held to its floor.
    private static void CityRateHook()
    {
        LendingTerms.BasePoints = 0f;
        AccountRules.BaseBp = 0;
        AccountRules.BaseMultiplier = 1f;
        bool same = true;
        for (int score = CreditCatalogue.ScoreMin; score <= CreditCatalogue.ScoreMax; score += 3)
        {
            LenderTerms a = LendingTerms.For(score, 50000f, 8), b = LendingTerms.For(score, 50000f, 8, 0f);
            same &= a.AnnualRate == b.AnnualRate && a.Cap == b.Cap && a.Lends == b.Lends;
        }
        Check(same, "with the city's economy off the terms are the rating's alone, at every score");
        int own = 8, best = CreditCatalogue.BestTermsFrom, threshold = CreditCatalogue.LendingThreshold;
        Check(LendingTerms.For(best, 0f, own, 1.25f).AnnualRate == own + (int)Math.Round(CreditCatalogue.RatePointsAtBest + 1.25, MidpointRounding.AwayFromZero)
              && LendingTerms.For(threshold, 0f, own, 1.25f).AnnualRate == own + (int)Math.Round(CreditCatalogue.RatePointsAtThreshold + 1.25, MidpointRounding.AwayFromZero),
            "the central bank's points are added to the rating's before the rate is rounded to a whole point");
        Check(LendingTerms.For(best, 0f, own, 6f).AnnualRate - LendingTerms.For(best, 0f, own, 0f).AnnualRate == 6 && LendingTerms.For(best, 0f, own, -2f).AnnualRate - LendingTerms.For(best, 0f, own, 0f).AnnualRate == -2,
            "whole points move a bank's rate by exactly as much");
        Check(LendingTerms.For(best, 0f, 2, -2f).AnnualRate == CreditCatalogue.RateFloor && LendingTerms.For(best, 0f, 2, 3f).AnnualRate == 2 + CreditCatalogue.RatePointsAtBest + 3,
            "they are added before the floor: a rate cut under it is held at 1%, and a rise from under it counts in full");
        Check(LendingTerms.For(threshold - 1, 50000f, own, 6f).Lends == false && LendingTerms.For(best, 50000f, own, 6f).Cap == LendingTerms.For(best, 50000f, own, 0f).Cap,
            "the central bank's rate changes no cap and opens no door under the threshold");
        LendingTerms.BasePoints = 1.5f;
        Check(LendingTerms.For(best, 0f, own).AnnualRate == LendingTerms.For(best, 0f, own, 1.5f).AnnualRate, "the terms asked for without a rate carry the one that is set");
        int plain = ProductTerms.RateBp(700, own, ProductCatalogue.CardRatePoints, 1f);
        LendingTerms.BasePoints = 0f;
        Check(plain == ProductTerms.RateBp(700, own, ProductCatalogue.CardRatePoints, 1f), "a product's agreed rate is by the rating alone, whatever the central bank's rate");
        Check(ProductTerms.BaseBp(1.25f, 1f) == 125 && ProductTerms.BaseBp(-0.5f, 1f) == -50 && ProductTerms.BaseBp(1.25f, 2f) == 250 && ProductTerms.BaseBp(0f, 3f) == 0,
            "the central bank's rate in hundredths of a per cent, times the game's interest multiplier");
        Check(ProductTerms.WithBase(1400, 125, 1f) == 1525 && ProductTerms.WithBase(1400, 0, 1f) == 1400 && ProductTerms.WithBase(1400, -200, 1f) == 1200, "it goes on top of the agreed rate");
        Check(ProductTerms.WithBase(250, -200, 1f) == 100 && ProductTerms.WithBase(60, -200, 1f) == 60 && ProductTerms.WithBase(250, -200, 2f) == 200,
            "a cut never takes a rate under the floor, and never raises one that was under it already");
        LendingTerms.BasePoints = 2f;
        Check(ProductTerms.FixedRateBp(700, own, ProductCatalogue.CarRatePoints, 1f) == ProductTerms.RateBp(700, own, ProductCatalogue.CarRatePoints, 1f) + 200,
            "a purchase on finance is approved at the rating's rate with the central bank's rate of the day");
        var card = AccountRules.Open(ProductKind.CreditCard, 5000, 1400, 10);
        var car = AccountRules.Finance(ProductKind.CarFinance, 20000, 100, 900, 10, "car", "Car", 25000);
        AccountRules.BaseBp = 0;
        Check(card.RateAgreedNow == 1400 && card.RateNow == 1400 && car.RateAgreedNow == 900, "with the city's economy off an account's rate is the one it was opened at");
        AccountRules.BaseBp = 125;
        Check(card.RateAgreedNow == 1525 && card.RateNow == 1525 && card.RateBp == 1400, "an overdraft and a card follow the central bank's rate day by day; what was agreed is not rewritten");
        Check(car.RateAgreedNow == 900 && car.RateNow == 900, "finance already taken keeps its rate");
        card.Ladder.Penalty = true;
        Check(card.RateNow == 1525 + ProductCatalogue.PenaltyPoints * 100, "the penalty rate goes on top of both");
        AccountRules.BaseBp = -2000;
        Check(card.RateAgreedNow == 100, "and a card's rate is never under the floor");
        LendingTerms.BasePoints = 0f;
        AccountRules.BaseBp = 0;
        AccountRules.BaseMultiplier = 1f;
    }

    private static void CitySaveAndLoad()
    {
        CityState s = NewCity(424242);
        int version;
        bool same = true;
        for (int day = 2; day < 900; day++)
        {
            CityModel.Day(s, day, Outside(day, 60), MacroCatalogue.Hardcore, 5);
            if (day % 37 == 0)
            {
                string line = CityStore.Write(s);
                CityState back = CityStore.Read(line, out version);
                same &= back != null && version == CityStore.Version && CityStore.Write(back) == line;
            }
        }
        Check(same, "the state read back from its line writes the same line again, at any point of a long run");
        s.CarryRent = 120;
        s.CarryWages = 45;
        s.CarryTakings = 7;
        s.LastCopycatDay = 512;
        s.Buys = 0x2a;
        s.News.Add(new CityNews { Day = 899, Kind = CityNewsKind.Rival, Name = "Corner | Shop; Ltd ~" });
        string text = CityStore.Write(s);
        CityState read = CityStore.Read(text, out version);
        Check(read.CarryRent == 120 && read.CarryWages == 45 && read.CarryTakings == 7 && read.LastCopycatDay == 512 && read.Buys == 0x2a && read.Seed == s.Seed && read.Age == s.Age
              && read.Cycle == s.Cycle && read.Base == s.Base && read.Index == s.Index && read.Inflation == s.Inflation && read.Districts.Count == 4 && read.Districts[2].Income == s.Districts[2].Income,
            "every figure of the state is in the line");
        Check(read.News.Count == s.News.Count && read.News[read.News.Count - 1].Name == "Corner   Shop  Ltd  ", "a name cannot break the line apart");

        // A continuation of the run from the line gives the same history as the run itself.
        CityState resumed = CityStore.Read(CityStore.Write(s), out version);
        for (int day = 900; day < 1100; day++)
        {
            CityModel.Day(s, day, Outside(day, 60), MacroCatalogue.Hardcore, 5);
            CityModel.Day(resumed, day, Outside(day, 60), MacroCatalogue.Hardcore, 5);
        }
        Check(CityStore.Write(resumed) == CityStore.Write(s), "a run carried on from a save is the run itself");

        // A later version: a higher number, fields added at the end of records and a part this version does not know.
        string later = text.Replace("v=" + CityStore.Version + "~", "v=" + (CityStore.Version + 1) + "~").Replace("~d:", "|77|88~x:something|new;more~d:") + "~z:1|2|3";
        CityState newer = CityStore.Read(later, out version);
        Check(newer != null && version == CityStore.Version + 1 && newer.Base == read.Base && newer.Index == read.Index && newer.Districts.Count == 4 && newer.Buys == 0x2a,
            "a line from a later version loads: the fields this version knows are read, the rest is skipped");
        Check(CityStore.Read("v=1~h:5", out version) != null && CityStore.Read("v=1~h:5", out version).Index == MacroCatalogue.IndexStart && CityStore.Read("v=1~h:5", out version).Cycle == CycleState.Steady,
            "a short header is filled with the opening figures");
        Check(CityStore.Read(null, out version) == null && CityStore.Read(string.Empty, out version) == null && CityStore.Read("nonsense", out version) == null
              && CityStore.Read("v=0~h:1", out version) == null && CityStore.Read("v=1~d:a|1|2|3", out version) == null && CityStore.Read("v=x~h:1", out version) == null,
            "a line that is not the city's economy is not read");
        CityState wild = CityStore.Read("v=1~h:9|-5|-1|77|99|0|500|-500|1|99|42|77|-3|1|999999|-999999|0|-10|-20|-30|-1|4095~d:a|9999|-5|100000;a|1|2|3;|5|5|5~s:1|9|-4|99|99999;50000|1|3|5|200~n:5|99|1|2|3|4|x;5|2|1|1|0|0|", out version);
        Check(wild != null && wild.Age == 0 && wild.Cycle == CycleState.Recession && wild.MonthsLeft == MacroCatalogue.StateMonthsMost && wild.MonthsIn == 1 && wild.Base == MacroCatalogue.BaseMost
              && wild.BaseBefore == MacroCatalogue.BaseLeast && wild.PendingMove == MacroCatalogue.MoveMost && wild.PendingReason == RateReason.Hold && wild.LastReason == RateReason.Hold
              && wild.Index == MacroCatalogue.IndexLeast && wild.Inflation == MacroCatalogue.InflationMost && wild.Pressure == MacroCatalogue.PressureLeast && wild.CarryRent == 0 && wild.CarryWages == 0
              && wild.Buys == 63, "figures out of their bounds in a line are put inside them");
        Check(wild.Districts.Count == 1 && wild.Districts[0].Start == MacroCatalogue.IncomeMost && wild.Districts[0].Income == MacroCatalogue.IncomeLeast && wild.Districts[0].Before == MacroCatalogue.IncomeMost
              && wild.Supply[0].Shock == SupplyShock.None && wild.Supply[0].Factor == MacroCatalogue.FactorLeast && wild.Supply[0].Days == 0 && wild.Supply[1].Factor == MacroCatalogue.FactorMost
              && wild.Supply[1].Shock == SupplyShock.Shortage && wild.Supply[1].Days == 5 && wild.News.Count == 1 && wild.News[0].Kind == CityNewsKind.Cycle,
            "a district named twice or not at all, a shock of no known kind and a piece of no known kind are left out");
        var data = new Dictionary<string, string>();
        Check(CityStore.Load(data, CityStore.Key, out version) == null && CityStore.Load(null, CityStore.Key, out version) == null, "a save without the line has no economy yet");
        data[CityStore.Key] = text;
        Check(CityStore.Load(data, CityStore.Key, out version) != null && CityStore.Load(data, CityStore.TestKey, out version) == null && CityStore.Key != CityStore.TestKey, "the line is found under its own key only");
    }

    private static void CityDeterminism()
    {
        bool same = true, differs = false;
        string first = null;
        for (int seed = 1; seed <= 12; seed++)
        {
            CityState a = NewCity(seed), b = NewCity(seed);
            for (int day = 2; day < 700; day++)
            {
                CityModel.Day(a, day, Outside(day, 60), MacroCatalogue.Standard, 5);
                CityModel.Day(b, day, Outside(day, 60), MacroCatalogue.Standard, 5);
            }
            string line = CityStore.Write(a);
            same &= line == CityStore.Write(b);
            differs |= first != null && line != first;
            first = first ?? line;
        }
        Check(same, "the same seed and the same days give the same history, figure for figure");
        Check(differs, "another seed gives another history");
        bool inRange = true;
        var seen = new HashSet<int>();
        for (int n = 0; n < 5000; n++)
        {
            int draw = CityModel.Draw(99, 7, n, 1000);
            inRange &= draw >= 0 && draw < 1000 && draw == CityModel.Draw(99, 7, n, 1000);
            seen.Add(draw);
        }
        Check(inRange && seen.Count > 900 && CityModel.Draw(1, 1, 1, 1) == 0 && CityModel.Draw(1, 1, 1, 0) == 0, "a draw is always the same for its seed, purpose and count, inside its range, and spread over it");
        // What is announced is settled the moment it is announced: the strength of the day it runs on decides only how far it moves prices.
        CityState s = NewCity(31);
        CityModel.Announce(s, 10, MacroCatalogue.Fresh, SupplyShock.Shortage, 9999, 99, MacroCatalogue.Standard);
        Check(s.Supply[MacroCatalogue.Fresh].Size == MacroCatalogue.ShortageMost && s.Supply[MacroCatalogue.Fresh].Days == MacroCatalogue.ShockDaysMost && s.Supply[MacroCatalogue.Fresh].From == s.Age + 1
              && s.Supply[MacroCatalogue.Fresh].Announced(s.Age) && !s.Supply[MacroCatalogue.Fresh].Running(s.Age) && HasNews(s, 10, CityNewsKind.Supply),
            "a shortage that is asked for is held to the catalogue's size and days, starts tomorrow and is in today's paper");
        Check(CityModel.ShockPercent(s.Supply[MacroCatalogue.Fresh], MacroCatalogue.Standard) == 40 && CityModel.ShockPercent(s.Supply[MacroCatalogue.Fresh], MacroCatalogue.Relaxed) == 20
              && CityModel.ShockPercent(s.Supply[MacroCatalogue.Fresh], MacroCatalogue.Hardcore) == 50 && CityModel.ShockPercent(s.Supply[MacroCatalogue.Home], MacroCatalogue.Hardcore) == 0,
            "what a shortage does to prices by strength: 40% at its largest, 20% on Relaxed, 50% on Hardcore");
        CityModel.Day(s, 11, new CityDay { Season = 1 }, MacroCatalogue.Standard, 5);
        Check(s.Supply[MacroCatalogue.Fresh].Running(s.Age) && s.Supply[MacroCatalogue.Fresh].Factor == 1400, "the next day it runs: fresh goods cost 1.4 times the usual in a fair spring");
        CityModel.Announce(s, 11, MacroCatalogue.Home, SupplyShock.Glut, 0, 0, MacroCatalogue.Standard);
        CityModel.Day(s, 12, new CityDay { Season = 1 }, MacroCatalogue.Standard, 5);
        Check(s.Supply[MacroCatalogue.Home].Factor == 1000 + 30 - MacroCatalogue.GlutLeast && s.Supply[MacroCatalogue.Home].Days == MacroCatalogue.ShockDaysLeast,
            "a glut takes its size off the season's push, for three days at least");
        for (int day = 13; day < 13 + MacroCatalogue.ShockDaysMost; day++)
        {
            CityModel.Day(s, day, new CityDay { Season = 1 }, MacroCatalogue.Standard, 5);
        }
        Check(s.Supply[MacroCatalogue.Fresh].Shock == SupplyShock.None || s.Supply[MacroCatalogue.Fresh].From > 2, "after its days a shortage is over");
    }

    // What other features read: safe answers while nothing is live, the model's while it is, and a handler that throws stops nobody.
    private static void CitySurface()
    {
        CityEconomy.Live = null;
        Check(!CityEconomy.Running && CityEconomy.State == CycleState.Steady && CityEconomy.BasePoints == 0f && CityEconomy.PriceIndex == 100f && CityEconomy.InflationPercent == 2f
              && CityEconomy.DistrictIncome("ba:neighborhood_a") == 100f && CityEconomy.DistrictIncome(null) == 100f && CityEconomy.MonthOfState == 1,
            "with the city's economy off: steady, the banks' own rate, prices at 100, every district at 100");
        CityState s = NewCity(8);
        s.Cycle = CycleState.Slowdown;
        s.MonthsIn = 3;
        s.Base = 5;
        s.Index = 104200;
        s.Inflation = 350;
        s.Districts[0].Income = 815;
        CityEconomy.Live = s;
        Check(CityEconomy.Running && CityEconomy.State == CycleState.Slowdown && CityEconomy.BasePoints == 1.25f && Math.Abs(CityEconomy.PriceIndex - 104.2f) < 0.001f && CityEconomy.InflationPercent == 3.5f
              && CityEconomy.DistrictIncome("ba:neighborhood_a") == 81.5f && CityEconomy.DistrictIncome("ba:neighborhood_nowhere") == 100f && CityEconomy.MonthOfState == 3,
            "with it on: the model's own figures, and 100 for a district it does not know");
        int called = 0, faults = 0;
        Action good = () => called++;
        Action bad = () => throw new InvalidOperationException("a handler of another feature's");
        CityEconomy.MonthTurned += good;
        CityEconomy.MonthTurned += bad;
        CityEconomy.MonthTurned += good;
        CityEconomy.RaiseMonthTurned(e => faults++);
        Check(called == 2 && faults == 1, "a handler of the month's turn that throws does not cost the others theirs");
        CityEconomy.MonthTurned -= good;
        CityEconomy.MonthTurned -= bad;
        CityEconomy.MonthTurned -= good;
        CityEconomy.RaiseMonthTurned(null);
        Check(called == 2, "and nobody is called once they have gone");
        CityEconomy.Live = null;
    }
}
