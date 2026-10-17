using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul.Economy;

// The checks of the staff rota planner's ladder of plans (Scripts/Economy/Company/RotaLadder.cs and
// RotaLadder.Steps.cs): what a week is valued at, part by part; that the best week of the people there are is never
// worse than the week as it is; that a rung for one more person is kept only if it pays; cleaning hours by what they
// bring; what an unmet demand costs against the wages that would meet it; the choices kept in a save; and that a
// ladder made a step at a time is the one made in one go. One worked example is printed with "--rota-example".
internal static partial class Program
{
    private sealed class RotaCase
    {
        public RotaShop Shop;
        public RotaProblem Problem;
        public readonly RotaPlan Now = new RotaPlan();
        public readonly RotaOutlook Outlook = new RotaOutlook();
        public readonly RotaLadderResult Ladder = new RotaLadderResult();
        public readonly RotaSolver Solver = new RotaSolver();
        public readonly RotaAdvisor Advisor = new RotaAdvisor();
        public readonly RotaLadder Maker = new RotaLadder();
        public bool[] Barred;
    }

    private static readonly int[] RotaCurve = { 1, 1, 0, 0, 1, 1, 2, 3, 5, 7, 9, 11, 12, 10, 9, 8, 7, 6, 6, 5, 4, 3, 1, 0 };   // 112 customers a day

    // A shop of tills (20 customers an hour each) and cleaning stations, with one employee for each station to begin with.
    private static RotaCase RotaShopCase(int tills, int cleaners, int workers, int scale = 100)
    {
        var c = new RotaCase { Shop = RotaTills(tills, cleaners), Problem = new RotaProblem() };
        int stations = tills + cleaners;
        c.Problem.Size(stations, workers);
        for (int s = 0; s < stations; s++)
        {
            c.Problem.Kind[s] = s < tills ? RotaKind.Work : RotaKind.Cleaning;
        }
        for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
        {
            int come = RotaCurve[cell % 24] * scale / 100;
            c.Shop.Wanting[cell] = come;
            c.Shop.Customers[cell] = Math.Min(come, tills * 20);
        }
        c.Now.Size(stations);
        return c;
    }

    private static void RotaOpenHours(RotaCase c, int days, int from, int to)
    {
        for (int d = 0; d < 7; d++)
        {
            for (int h = from; h < to && ((days >> d) & 1) != 0; h++)
            {
                c.Shop.Open[d * 24 + h] = true;
            }
        }
    }

    private static void RotaRoster(RotaCase c, int worker, int station, int days, int from, int to)
    {
        for (int d = 0; d < 7; d++)
        {
            for (int h = from; h < to && ((days >> d) & 1) != 0; h++)
            {
                c.Now.At[(d * 24 + h) * c.Shop.Stations + station] = (short)worker;
            }
        }
    }

    // The advice, the rota it has rated, and the ladder: as the director does them, in one go or a step at a time.
    private static RotaLadderResult RotaClimb(RotaCase c, int mode = RotaMode.Auto, bool stepped = false, RotaLadderResult into = null)
    {
        into = into ?? c.Ladder;
        c.Advisor.Advise(c.Shop, c.Problem, c.Solver, c.Now.At, c.Barred, c.Outlook);
        c.Advisor.Grid(c.Shop, c.Shop.Open, c.Problem);
        c.Solver.Rate(c.Problem, c.Now);
        if (!stepped)
        {
            c.Maker.Make(c.Shop, c.Problem, c.Solver, c.Advisor, c.Outlook, c.Now, c.Barred, mode, into);
            return into;
        }
        c.Maker.Begin(c.Shop, c.Problem, c.Solver, c.Advisor, c.Outlook, c.Now, c.Barred, mode, into);
        while (!c.Maker.Step(2))
        {
        }
        return into;
    }

    // A shop of one till, one cleaning station, a cashier who works 20 to 30 hours and a cleaner who asks for 35 to 45.
    private static RotaCase RotaSouvenirs()
    {
        RotaCase c = RotaShopCase(1, 1, 2);
        RotaShop shop = c.Shop;
        shop.Margin = 2086;
        shop.Takings = 2606;
        shop.Customers[5 * 24 + 12]++;
        shop.Customers[6 * 24 + 12]++;
        shop.Wanting[5 * 24 + 12]++;
        shop.Wanting[6 * 24 + 12]++;     // 786 a week
        shop.StationWage[0] = 1200;
        shop.StationWage[1] = 900;
        shop.GroupGoing[0] = 1250;
        shop.GroupGoing[1] = 950;
        shop.DirtPerCustomer = 360;
        shop.CleanPerHour = 50000;
        shop.Content = 70;
        shop.CleanNow = 80;
        shop.TheftRate = 23;
        shop.TheftLimit = 63;
        shop.NightRisk = 324;
        RotaProblem p = c.Problem;
        p.Worker[0].Wage = 1200;
        p.Worker[1].Wage = 900;
        p.Can[0 * 2 + 0] = 60;
        p.Can[1 * 2 + 1] = 50;
        p.Worker[0].Demands = new[] { RotaAsk(RotaDemandKind.Hours, 1, 20, 30) };
        p.Worker[1].Demands = new[] { RotaAsk(RotaDemandKind.Hours, 1, 35, 45) };
        RotaOpenHours(c, 0x3F, 10, 20);
        RotaOpenHours(c, 0x40, 10, 18);      // 68 hours
        RotaRoster(c, 0, 0, 0x3F, 10, 15);   // the cashier, 30 hours
        RotaRoster(c, 1, 1, 0x0F, 19, 20);   // the cleaner, 4 hours
        return c;
    }

    private static string RotaMoneyText(long hundredths)
    {
        return (hundredths / 100.0).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string RotaRungText(RotaRung r)
    {
        RotaWeekSum s = r.Sum;
        return (r.Kind == RotaRungKind.AsItIs ? "as it is" : r.Kind == RotaRungKind.Best ? "best with the people (source " + r.Source + ")" : "hire " + r.Hires + " (group " + r.Hire[r.Hires - 1] + ")")
               + ": " + s.Hours + " h open, " + s.Manned + " manned, " + s.StaffHours + " staff h (" + s.CleanHours + " cleaning), served " + s.Served + " of " + s.Come + " (" + s.LostUnmanned + " found nobody, "
               + s.LostFull + " over capacity), sales " + RotaMoneyText(s.Sales) + " (dirt takes " + RotaMoneyText(s.Clean) + "), wages " + RotaMoneyText(s.Wages) + ", theft " + RotaMoneyText(s.Theft) + ", night "
               + RotaMoneyText(s.Night) + ", demands " + RotaMoneyText(s.Demands) + ", net " + RotaMoneyText(s.Net) + ", gain " + RotaMoneyText(r.Gain) + ", unmet " + r.Plan.UnmetCount;
    }

    // A plan's week as the page shows it: each day's hours with the customers of the hour and who is at each station.
    private static void RotaWeekPrint(RotaCase c, RotaRung r, string[] names)
    {
        int stations = c.Shop.Stations;
        for (int d = 0; d < 7; d++)
        {
            string line = "    " + "Mon Tue Wed Thu Fri Sat Sun".Split(' ')[d] + ":";
            for (int h = 0; h < 24; h++)
            {
                int cell = d * 24 + h;
                if (!r.Open[cell])
                {
                    continue;
                }
                string who = string.Empty;
                for (int s = 0; s < stations; s++)
                {
                    int w = r.Plan.At[cell * stations + s];
                    who += w >= 0 ? names[Math.Min(w, names.Length - 1)] : "-";
                }
                line += " " + h + "h/" + Math.Max(c.Shop.Customers[cell], c.Shop.Wanting[cell]) + "/" + who;
            }
            Console.WriteLine(line);
        }
    }

    private static void RotaExample()
    {
        foreach (int mode in new[] { RotaMode.Auto, RotaMode.StaffFirst, RotaMode.Lean })
        {
            RotaCase c = RotaSouvenirs();
            RotaLadderResult l = RotaClimb(c, mode);
            Console.WriteLine("ROTA EXAMPLE (asked for " + (mode == RotaMode.Auto ? "auto" : mode == RotaMode.StaffFirst ? "staff first" : "lean") + "): made " + (l.Mode == RotaMode.StaffFirst ? "staff first" : "lean")
                              + ", lean " + RotaMoneyText(l.LeanNet) + ", staff first " + RotaMoneyText(l.StaffNet) + ", recommended rung " + l.Recommended + ", stop " + l.Stop + " (group " + l.StopGroup + ", gain "
                              + RotaMoneyText(l.StopGain) + ", needed " + RotaMoneyText(l.StopNeed) + "), " + l.Steps + " steps; old figures: now " + RotaMoneyText(c.Outlook.ProfitNow) + ", staffed "
                              + RotaMoneyText(c.Outlook.ProfitStaffed) + " (" + c.Outlook.HoursStaffed + " h), best " + RotaMoneyText(c.Outlook.ProfitBest) + " (" + c.Outlook.HoursBest + " h)");
            for (int i = 0; i < l.Count; i++)
            {
                RotaRung r = l.Rungs[i];
                Console.WriteLine("  rung " + i + ", " + RotaRungText(r) + "; hours of each: " + string.Join(",", new List<int>(r.HoursOf).GetRange(0, r.Workers)));
                if (i == 1 && mode == RotaMode.Auto)
                {
                    Console.WriteLine("    (hour / customers / till, cleaning: C the cashier, L the cleaner, - nobody)");
                    RotaWeekPrint(c, r, new[] { "C", "L", "n" });
                }
                foreach (RotaChange ch in r.Changes)
                {
                    Console.WriteLine("      day " + ch.Day + " " + ch.From + "-" + ch.To + " why " + ch.Why + " was " + RotaMoneyText(ch.Was) + " will " + RotaMoneyText(ch.Will));
                }
            }
        }
    }

    private static void RotaPlanChecks()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "--rota-example") >= 0)
        {
            RotaExample();
        }
        RotaValueChecks();
        RotaCleaningChecks();
        RotaDemandCostChecks();
        RotaLadderChecks();
        RotaLadderEdges();
        RotaStoreChecks();
        RotaLadderTiming();
    }

    private static long RotaLadderHash(RotaLadderResult l)
    {
        long hash = RotaLedger.StampStart;
        hash = RotaLedger.Mix(hash, l.Count);
        hash = RotaLedger.Mix(hash, l.Recommended * 16 + l.Mode * 4 + l.Stop);
        for (int i = 0; i < l.Count; i++)
        {
            RotaRung r = l.Rungs[i];
            hash = RotaLedger.Mix(hash, (int)(r.Sum.Net % 1000000007L));
            hash = RotaLedger.Mix(hash, r.Sum.Hours * 1000 + r.Hires);
            for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
            {
                hash = RotaLedger.Mix(hash, (r.Open[cell] ? 1 : 0) + r.Served[cell] * 2);
                for (int s = 0; s < r.Plan.Stations; s++)
                {
                    hash = RotaLedger.Mix(hash, r.Plan.At[cell * r.Plan.Stations + s]);
                }
            }
        }
        return hash;
    }

    // One till open nine to five every day, one cashier; the present rota is whatever the caller rosters.
    private static RotaCase RotaOneTill(int scale = 100)
    {
        RotaCase c = RotaShopCase(1, 0, 1, scale);
        c.Problem.Can[0] = 50;
        c.Problem.Worker[0].Wage = 1500;
        RotaOpenHours(c, 0x7F, 9, 17);
        return c;
    }

    private static RotaRung RotaValue(RotaCase c)
    {
        var rung = new RotaRung();
        c.Advisor.Grid(c.Shop, c.Shop.Open, c.Problem);
        c.Solver.Rate(c.Problem, c.Now);
        c.Maker.Evaluate(c.Advisor, c.Shop, c.Problem, c.Shop.Open, c.Now, rung);
        return rung;
    }

    // What a week is valued at, part by part.
    private static void RotaValueChecks()
    {
        RotaCase c = RotaOneTill();
        c.Shop.TheftRate = 10;
        RotaRung r = RotaValue(c);
        int come = 0;
        for (int h = 9; h < 17; h++)
        {
            come += RotaCurve[h] * 7;
        }
        Check(r.Sum.Hours == 56 && r.Sum.Manned == 0 && r.Sum.Served == 0 && r.Sum.Sales == 0, "rota plans: a week open with nobody at the till sells nothing (" + r.Sum.Served + " served, " + r.Sum.Sales + ")");
        Check(r.Sum.Come == come && r.Sum.LostUnmanned == come && r.Sum.LostFull == 0, "rota plans: every customer of an unmanned hour is counted as finding nobody (" + r.Sum.LostUnmanned + " of " + come + ")");
        Check(r.Sum.Theft == (long)come * 10 && r.Sum.Net == -r.Sum.Theft, "rota plans: thieves come to an unmanned open hour all the same, so it is worth less than nothing (" + r.Sum.Net + ")");
        Check(r.Earn[9] == 0 && r.Cost[9] == RotaCurve[9] * 10, "rota plans: an unmanned hour earns nothing and costs its theft, hour by hour");
        RotaRoster(c, 0, 0, 0x7F, 9, 17);
        r = RotaValue(c);
        Check(r.Sum.Manned == 56 && r.Sum.Served == come && r.Sum.LostUnmanned == 0, "rota plans: with the cashier there every customer of the open hours is served (" + r.Sum.Served + ")");
        Check(r.Sum.Sales == (long)come * 400 && r.Sum.Wages == 56L * 1500, "rota plans: sales are the customers served at the margin, wages the hours at the wage");
        Check(r.Sum.Net == r.Sum.Sales - r.Sum.Wages - r.Sum.Fees - r.Sum.Theft - r.Sum.Night - r.Sum.Demands, "rota plans: what is left is sales less wages, fees, theft, night risk and demands");
        Check(r.HoursOf[0] == 56 && r.Sum.StaffHours == 56, "rota plans: the hours of each person are counted");
        c.Problem.Can[0] = 5;
        int low = RotaValue(c).Sum.Served;
        c.Problem.Can[0] = 100;
        Check(low == come && RotaValue(c).Sum.Served == come, "rota plans: the skill of whoever stands at a till does not change how many it serves");

        c = RotaOneTill(300);     // three times the customers: more than one till serves at midday
        RotaRoster(c, 0, 0, 0x7F, 9, 17);
        r = RotaValue(c);
        Check(r.Come[12] == 36 && r.Served[12] == 20 && r.Sum.LostFull > 0, "rota plans: a till serves twenty an hour and no more; 36 come at midday, 20 are served (" + r.Served[12] + ")");
        int lostOne = r.Sum.LostFull;
        RotaCase two = RotaShopCase(2, 0, 2, 300);
        two.Problem.Can[0] = two.Problem.Can[3] = 50;
        RotaOpenHours(two, 0x7F, 9, 17);
        RotaRoster(two, 0, 0, 0x7F, 9, 17);
        RotaRoster(two, 1, 1, 0x7F, 9, 17);
        RotaRung both = RotaValue(two);
        Check(both.Served[12] == 36 && both.Sum.LostFull == 0 && lostOne > 0, "rota plans: a second till with somebody at it serves the customers the first could not (" + both.Served[12] + " at midday, " + lostOne + " lost before)");
        Check(both.Sum.Sales > r.Sum.Sales, "rota plans: and the week's sales rise with it");

        c = RotaOneTill();
        RotaRoster(c, 0, 0, 0x7F, 9, 17);
        c.Shop.TheftRate = 50;
        c.Shop.TheftLimit = 10;
        r = RotaValue(c);
        long theft = 0;
        for (int h = 9; h < 17; h++)
        {
            theft += RotaCurve[h] < 10 ? RotaCurve[h] * 50L * 7 : 0;
        }
        Check(r.Sum.Theft == theft && RotaLadder.Theft(c.Shop, 10) == 0 && RotaLadder.Theft(c.Shop, 9) == 450, "rota plans: theft follows the customers of the hour, and from the game's limit on it takes nothing (" + r.Sum.Theft + ")");
        c.Shop.TheftRate = 0;
        Check(RotaValue(c).Sum.Theft == 0 && RotaLadder.Theft(c.Shop, 5) == 0, "rota plans: a business the game takes nothing from is counted no theft");
        c.Shop.NightRisk = 300;
        Check(RotaValue(c).Sum.Night == 7 * 300, "rota plans: each night shut at one in the morning carries the break-in risk");
        RotaOpenHours(c, 0x03, 0, 3);
        Check(RotaValue(c).Sum.Night == 5 * 300, "rota plans: a night open at one in the morning carries none (" + RotaValue(c).Sum.Night + ")");
        c.Shop.DayFee[2] = 5000;
        Check(RotaValue(c).Sum.Fees == 5000, "rota plans: a day's licensing fee is counted on a day it opens");
        Check(RotaLadder.Going(c.Shop, 0) == 1500, "rota plans: with no going wage known, somebody new is counted at what an hour at the station costs");
        c.Shop.GroupGoing[0] = 1750;
        Check(RotaLadder.Going(c.Shop, 0) == 1750, "rota plans: the going wage is taken where it is known");
    }

    private static void RotaCleaningChecks()
    {
        Check(RotaLadder.CleanOf(0) == 100000 && RotaLadder.CleanOf(4999) == 95001 && RotaLadder.CleanOf(5000) == 90000 && RotaLadder.CleanOf(60000) == 0,
            "rota plans: cleanliness is a hundred less the dirt, and dirt that shows (five points and more) counts twice");
        var shop = new RotaShop();
        Check(RotaLadder.Factor(shop, 50000) == 1000, "rota plans: where the game gives no dirt figures, cleanliness changes nothing");
        shop.DirtPerCustomer = 300;
        shop.Content = 70;
        shop.CleanNow = 80;
        Check(RotaLadder.Factor(shop, 10000) == 1000, "rota plans: at the cleanliness the books were kept at, a customer leaves what the books say");
        Check(RotaLadder.Factor(shop, 0) == 1041 && RotaLadder.Factor(shop, 50000) == 834, "rota plans: a quarter of a point of satisfaction for each point of cleanliness (" + RotaLadder.Factor(shop, 0) + ", " + RotaLadder.Factor(shop, 50000) + ")");
        bool falls = true;
        for (int dirt = 0; dirt < 60000; dirt += 500)
        {
            falls &= RotaLadder.Factor(shop, dirt + 500) <= RotaLadder.Factor(shop, dirt);
        }
        Check(falls, "rota plans: the dirtier the floor, the less a customer leaves, never more");

        RotaCase c = RotaSouvenirs();
        RotaLadderResult l = RotaClimb(c, RotaMode.StaffFirst);
        RotaRung best = l.Rungs[1];
        var chosen = new int[RotaCatalogue.Cells];
        var closing = new int[RotaCatalogue.Cells];
        var none = new int[RotaCatalogue.Cells];
        for (int d = 0; d < 7; d++)
        {
            int hours = 0, last = -1;
            for (int h = 0; h < 24; h++)
            {
                int cell = d * 24 + h;
                chosen[cell] = best.Plan.At[cell * 2 + 1] >= 0 ? 1 : 0;
                hours += chosen[cell];
                last = best.Open[cell] ? h : last;
            }
            for (int h = last; h >= 0 && hours > 0; h--, hours--)
            {
                closing[d * 24 + h] = 1;
            }
        }
        long spread = RotaLadder.CleanWeek(c.Shop, best.Open, best.Served, chosen), atClosing = RotaLadder.CleanWeek(c.Shop, best.Open, best.Served, closing), dirty = RotaLadder.CleanWeek(c.Shop, best.Open, best.Served, none);
        Check(spread > dirty && atClosing > dirty, "rota plans: a week with cleaning leaves more than a week without (" + dirty + ", " + atClosing + ", " + spread + ")");
        Check(spread >= atClosing, "rota plans: the cleaning hours chosen bring at least what the same hours at closing time bring (" + spread + " against " + atClosing + ")");
        Check(best.Sum.CleanHours == 35 && best.HoursOf[1] == 35, "rota plans: staff first, the cleaner who asks for 35 hours is given them (" + best.HoursOf[1] + ")");
        Check(best.Sum.Clean < l.Rungs[0].Sum.Clean, "rota plans: and the floor costs less than with the four hours a week it has now (" + best.Sum.Clean + " against " + l.Rungs[0].Sum.Clean + ")");

        var one = new int[RotaCatalogue.Cells];
        var served = new int[RotaCatalogue.Cells];
        var open = new bool[RotaCatalogue.Cells];
        var late = new int[RotaCatalogue.Cells];
        for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
        {
            open[cell] = cell % 24 >= 9 && cell % 24 < 17;
            served[cell] = open[cell] ? RotaCurve[cell % 24] : 0;
            one[cell] = cell % 24 == 11 ? 1 : 0;
            late[cell] = cell % 24 == 16 ? 1 : 0;
        }
        shop.CleanPerHour = 50000;
        shop.Margin = 400;
        long least = long.MaxValue, most = long.MinValue;
        for (int hour = 9; hour < 17; hour++)
        {
            for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
            {
                one[cell] = cell % 24 == hour ? 1 : 0;
            }
            long week = RotaLadder.CleanWeek(shop, open, served, one);
            least = Math.Min(least, week);
            most = Math.Max(most, week);
        }
        Check(most > least && RotaLadder.CleanWeek(shop, open, served, late) <= most, "rota plans: which hour of the day is cleaned changes what the week brings (" + least + " to " + most + "), so the hour is chosen and not assumed");

        // The cleaning service looks after the floor: no cleaning shift, and the floor is taken to stay as it is.
        c = RotaSouvenirs();
        c.Shop.Cleaned = true;
        l = RotaClimb(c, RotaMode.StaffFirst);
        bool noneCleaning = true, noValue = true;
        for (int i = 1; i < l.Count; i++)
        {
            noneCleaning &= l.Rungs[i].Sum.CleanHours == 0 && l.Rungs[i].Sum.Clean == 0;
        }
        c.Advisor.Grid(c.Shop, c.Shop.Open, c.Problem);
        for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
        {
            noValue &= c.Problem.Value[cell * 2 + 1] == 0;
        }
        Check(noneCleaning, "rota plans: where the cleaning service cleans, no plan holds a cleaning shift and the floor costs nothing");
        Check(noValue, "rota plans: and no cleaning hour is wanted of the business's own staff");
        Check(l.Fill.At.Length >= RotaCatalogue.Cells * 2 && RotaHoursOf(l.Fill, 1) == 0, "rota plans: nor does \"Fill the rota\" write one");
        Check(l.Rungs[1].Left.Count == 1 && l.Rungs[1].Left[0].Worker == 1 && l.Rungs[1].Left[0].Why == RotaClash.Service, "rota plans: and the cleaner who asks for hours is named as left without any, because the service cleans");
    }

    private static void RotaDemandCostChecks()
    {
        int quit;
        Check(RotaLadder.DemandCost(0, 3, 0, 1, 100, 100000, 40000, 50, true, 70, out quit) == 0 && quit == 0, "rota plans: demands that are met cost nothing");
        long one = RotaLadder.DemandCost(3, 0, 1, 1, 100, 100000, 40000, 50, true, 70, out quit);
        Check(quit == 333 && one > 0, "rota plans: one demand of medium priority unmet takes 0.3 an hour: from a hundred, 333 hours to a resignation (" + quit + ")");
        int sooner;
        RotaLadder.DemandCost(5, 0, 1, 1, 100, 100000, 40000, 50, true, 70, out sooner);
        Check(sooner == 200, "rota plans: one of high priority takes 0.5 an hour: 200 hours (" + sooner + ")");
        RotaLadder.DemandCost(10, 0, 2, 2, 100, 100000, 40000, 50, true, 70, out sooner);
        Check(sooner == 166, "rota plans: the game takes no more than 0.6 an hour however much is unmet (" + sooner + ")");
        RotaLadder.DemandCost(3, 0, 1, 1, 50, 100000, 40000, 50, true, 70, out sooner);
        Check(sooner == 166, "rota plans: somebody at half satisfaction is half as far from resigning (" + sooner + ")");
        long settled = RotaLadder.DemandCost(2, 5, 1, 2, 100, 0, 40000, 50, false, 70, out quit);
        Check(quit == 0 && settled == 40000 * 20 / 100, "rota plans: where the demands met outweigh the one unmet nobody resigns, but satisfaction stops at 80 and a fifth of the work is lost (" + settled + ")");
        long third = RotaLadder.DemandCost(2, 10, 2, 3, 100, 0, 40000, 50, false, 70, out quit);
        Check(third == 40000 * 40 / 100, "rota plans: with one demand of three met it stops at 60 (" + third + ")");
        long more = RotaLadder.DemandCost(3, 0, 1, 1, 100, 200000, 40000, 50, true, 70, out quit);
        Check(more > one, "rota plans: the more somebody's customers leave, the more their resignation costs");
        long replace = RotaLadder.ReplaceDays * (100000L - 40000) / 7, expect = replace * 168 / 333 + 100000L * 50 * 50 / (400L * 120);
        Check(one == expect, "rota plans: the cost is the post empty for the days it takes to replace them, spread over the weeks to the resignation, and the worse service on the way (" + one + ", " + expect + ")");

        // Against the wages that would meet it, in both modes.
        RotaCase c = RotaSouvenirs();
        RotaLadderResult staff = RotaClimb(c, RotaMode.StaffFirst);
        RotaCase c2 = RotaSouvenirs();
        RotaLadderResult lean = RotaClimb(c2, RotaMode.Lean);
        Check(staff.Mode == RotaMode.StaffFirst && !staff.ModeAuto && lean.Mode == RotaMode.Lean && !lean.ModeAuto, "rota plans: the player's choice of staff first or lean is the one the plans are made in");
        Check(staff.Rungs[1].Sum.Demands == 0 && staff.Rungs[1].Plan.UnmetCount == 0, "rota plans: staff first, the best week with the people leaves no demand unmet where the hours hold them");
        Check(staff.FillSum.Wages > lean.Rungs[0].Sum.Wages, "rota plans: staff first pays the wages that meet the demands (" + staff.FillSum.Wages + ")");
        RotaCase c3 = RotaSouvenirs();
        RotaLadderResult auto = RotaClimb(c3);
        Check(auto.ModeAuto && auto.ModeMatters && auto.Mode == (auto.StaffNet > auto.LeanNet ? RotaMode.StaffFirst : RotaMode.Lean), "rota plans: left alone, the mode is whichever week comes to more (lean " + auto.LeanNet + ", staff first " + auto.StaffNet + ")");
        Check(lean.Rungs[1].HoursOf[0] <= 30 && lean.Rungs[1].HoursOf[1] >= 35 && lean.Rungs[1].Sum.Demands == 0, "rota plans: lean too, the hours a week people ask for are kept at both ends (" + lean.Rungs[1].HoursOf[0] + ", " + lean.Rungs[1].HoursOf[1] + ")");
        bool within = true;
        for (int i = 1; i < staff.Count; i++)
        {
            within &= staff.Rungs[i].HoursOf[0] <= 30;
        }
        Check(within, "rota plans: staff first, the cashier who asks for at most 30 hours is never given more in any plan");
    }

    private static int RotaHoursOf(RotaPlan plan, int worker)
    {
        int hours = 0;
        for (int i = 0; i < RotaCatalogue.Cells * plan.Stations; i++)
        {
            hours += plan.At[i] == worker ? 1 : 0;
        }
        return hours;
    }

    private static void RotaLadderChecks()
    {
        RotaCase c = RotaSouvenirs();
        RotaLadderResult l = RotaClimb(c);
        RotaRung now = l.Rungs[0], best = l.Rungs[1];
        Check(l.Count >= 3 && now.Kind == RotaRungKind.AsItIs && best.Kind == RotaRungKind.Best && l.Rungs[2].Kind == RotaRungKind.Hire, "rota plans: the ladder is the week as it is, the best with the people, then a hire (" + l.Count + " rungs)");
        Check(now.Sum.Hours == 68 && now.Sum.Manned == 30 && now.Sum.LostUnmanned > 0, "rota plans: as it is, the example is open 68 hours with a cashier for 30; the other 38 sell nothing (" + now.Sum.LostUnmanned + " customers find nobody)");
        Check(best.Sum.Net >= now.Sum.Net, "rota plans: the best week with the people is never worse than the week as it is (" + best.Sum.Net + " against " + now.Sum.Net + ")");
        bool gains = true, pays = true, rising = true, capped = true, never = true;
        for (int i = 1; i < l.Count; i++)
        {
            RotaRung r = l.Rungs[i], before = l.Rungs[i - 1];
            gains &= r.Gain == r.Sum.Net - before.Sum.Net;
            rising &= r.Sum.Net >= before.Sum.Net;
            pays &= r.Kind != RotaRungKind.Hire || r.Gain >= Math.Max(RotaLadder.GainLeast, before.Sum.Net * RotaLadder.ShareLeast / 100);
            never &= r.Kind != RotaRungKind.Hire || !RotaLadder.Writable(r);
            for (int w = r.Staff; w < r.Workers; w++)
            {
                capped &= r.HoursOf[w] <= RotaCatalogue.FullWeek;
            }
        }
        Check(gains, "rota plans: the gain shown for each rung is its week less the week of the rung before");
        Check(rising, "rota plans: the weeks rise from rung to rung up to the one advised");
        Check(pays, "rota plans: a rung for one more person is there only if it adds five per cent and the least a rung must");
        Check(never && !RotaLadder.Writable(now) && !RotaLadder.Writable(null), "rota plans: a rung that takes a hire is never one that may be written, nor is the week as it is");
        Check(RotaLadder.Writable(best) == (best.Source != 0), "rota plans: the best week with the people may be written when it differs from the week as it is");
        Check(capped, "rota plans: somebody to hire is counted for a full week of " + RotaCatalogue.FullWeek + " hours and no more");
        Check(l.Recommended == l.Count - 1, "rota plans: the advised rung is the last one, since every rung before it pays");
        Check(l.Rungs[2].Hires == 1 && l.Rungs[2].Hire[0] == 0 && l.Rungs[2].Sum.Manned > best.Sum.Manned, "rota plans: the first hire of the example is a cashier, who keeps more hours with somebody at the till (" + l.Rungs[2].Sum.Manned + " against " + best.Sum.Manned + ")");
        Check(l.Rungs[2].Workers == 3 && l.Rungs[2].Staff == 2, "rota plans: a hire rung counts the people there are and the one to hire");
        int hourly = 0;
        for (int h = 0; h < 24; h++)
        {
            hourly += l.Hourly[h];
        }
        Check(hourly == 786 && l.Hourly[12] == 12 * 7 + 2, "rota plans: the week's customers by the hour of the day add up to the week (" + hourly + ")");
        Check(l.FillSum.Hours == 68, "rota plans: \"Fill the rota\" keeps the hours the business has (" + l.FillSum.Hours + ")");

        // No hour changes without a reason.
        bool reasons = true, valid = true;
        for (int i = 1; i < l.Count; i++)
        {
            RotaRung r = l.Rungs[i];
            int differs = 0, told = 0;
            for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
            {
                differs += r.Open[cell] != c.Shop.Open[cell] ? 1 : 0;
            }
            foreach (RotaChange change in r.Changes)
            {
                told += change.To - change.From;
                valid &= change.Why <= RotaWhy.Day && change.To > change.From;
                for (int h = change.From; h < change.To; h++)
                {
                    valid &= r.Open[change.Day * 24 + h] == (change.Why == RotaWhy.Opened) && c.Shop.Open[change.Day * 24 + h] != r.Open[change.Day * 24 + h];
                }
            }
            reasons &= differs == told || r.Changes.Count == RotaLadder.ChangesMost;
        }
        Check(reasons, "rota plans: every hour a plan opens or closes against the hours kept is in a change with its reason");
        Check(valid, "rota plans: and each change says truly whether it opens or closes");

        // The same ladder every time, and the same a step at a time.
        long first = RotaLadderHash(l);
        RotaCase again = RotaSouvenirs();
        Check(RotaLadderHash(RotaClimb(again)) == first, "rota plans: the same business gives the same ladder every time");
        RotaCase stepped = RotaSouvenirs();
        Check(RotaLadderHash(RotaClimb(stepped, RotaMode.Auto, true)) == first, "rota plans: a ladder made two steps at a time is the ladder made in one go");
        RotaLadderResult second = RotaClimb(c);
        Check(RotaLadderHash(second) == first, "rota plans: and made again with the same planner it is the same once more");

        // Hours with no customers are never opened, however many are hired.
        c = RotaSouvenirs();
        for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
        {
            if (cell % 24 < 8 || cell % 24 >= 20)
            {
                c.Shop.Customers[cell] = c.Shop.Wanting[cell] = 0;
            }
        }
        l = RotaClimb(c);
        bool nights = false;
        for (int i = 1; i < l.Count; i++)
        {
            for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
            {
                nights |= l.Rungs[i].Open[cell] && (cell % 24 < 8 || cell % 24 >= 20) && !c.Shop.Open[cell];
            }
        }
        Check(!nights && l.Rungs[l.Count - 1].Sum.Hours < 168, "rota plans: no plan opens an hour nobody comes in; the week is never shown open round the clock for its own sake (" + l.Rungs[l.Count - 1].Sum.Hours + " hours at most)");

        // A hire that adds too little ends the ladder.
        c = RotaSouvenirs();
        c.Shop.Margin = 150;
        c.Shop.Takings = 300;
        l = RotaClimb(c);
        bool little = true;
        for (int i = 2; i < l.Count; i++)
        {
            little &= l.Rungs[i].Gain >= RotaLadder.GainLeast;
        }
        Check(little && (l.Stop != RotaEnd.TooLittle || l.StopGain < l.StopNeed), "rota plans: where customers leave little, the ladder ends at the first hire that would not pay (" + l.Count + " rungs, stop " + l.Stop + ", "
            + l.StopGain + " under " + l.StopNeed + ")");
        Check(l.Count < RotaLadder.RungsMost || l.Stop == RotaEnd.Most, "rota plans: and says why it ends");

        // Hours the business may not open are in no plan.
        c = RotaSouvenirs();
        c.Barred = new bool[RotaCatalogue.Cells];
        RotaAdvisor.LateHours(c.Barred);
        l = RotaClimb(c);
        bool barred = false;
        for (int i = 1; i < l.Count; i++)
        {
            for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
            {
                barred |= l.Rungs[i].Open[cell] && c.Barred[cell] && !c.Shop.Open[cell];
            }
        }
        Check(!barred, "rota plans: without a late opening permit no plan opens a late or an early hour");
    }

    private static void RotaLadderEdges()
    {
        // Nobody asks for a least number of hours: the two modes are the same and there is nothing to choose.
        RotaCase c = RotaOneTill();
        RotaRoster(c, 0, 0, 0x1F, 9, 17);
        RotaLadderResult l = RotaClimb(c);
        Check(!l.ModeMatters && l.Mode == RotaMode.Lean && l.LeanNet == l.StaffNet, "rota plans: where nobody asks for hours there is no choice between staff first and lean");
        Check(l.Rungs[1].Sum.Net >= l.Rungs[0].Sum.Net && l.Rungs[1].Source != 0, "rota plans: a cashier rostered five days of seven is given the other two (" + l.Rungs[1].Sum.Manned + " hours with somebody there)");

        // Somewhere no customers come: the rota is what matters, and nothing is advised about hours or hires.
        c = RotaOneTill();
        c.Shop.HasCustomers = false;
        l = RotaClimb(c);
        Check(l.Count == 2 && l.Stop == RotaEnd.NoAdvice, "rota plans: where no customers come the ladder is the week as it is and a rota for it, with no hire advised");
        Check(l.Rungs[1].Plan.CoveredValue >= l.Rungs[0].Plan.CoveredValue && l.Rungs[1].Source == 1, "rota plans: and the rota that covers more work is the better one there (" + l.Rungs[1].Plan.CoveredHours + " hours covered)");

        // A week that is already the best: the advised rung is the week as it is.
        c = RotaOneTill();
        for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
        {
            c.Shop.Open[cell] = false;
        }
        c.Shop.Margin = 0;
        l = RotaClimb(c);
        Check(l.Rungs[1].Source == 0 && l.Recommended == 0 && !RotaLadder.Writable(l.Rungs[1]), "rota plans: where nothing can be bettered the advised plan is the week as it is, and there is nothing to write");

        // What a hire costs once, and the weeks until the gain has paid it.
        c = RotaSouvenirs();
        c.Shop.HireCost = 100000;
        l = RotaClimb(c);
        RotaRung hire = l.Rungs[2];
        Check(hire.OneOff == 100000 && hire.WeeksBack == (int)((100000 + hire.Gain - 1) / hire.Gain), "rota plans: a cost paid once for a hire is shown with the weeks the gain takes to pay it (" + hire.WeeksBack + ")");
        c.Shop.HireCost = 0;
        l = RotaClimb(c);
        Check(l.Rungs[2].OneOff == 0 && l.Rungs[2].WeeksBack == 0, "rota plans: the game asks nothing to take somebody on, so a hire pays from its first week");

        // As many people as the planner keeps: no hire is advised on top.
        var full = new RotaCase { Shop = RotaTills(1), Problem = new RotaProblem() };
        full.Problem.Size(1, RotaCatalogue.WorkersMost);
        full.Now.Size(1);
        for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
        {
            full.Shop.Customers[cell] = full.Shop.Wanting[cell] = RotaCurve[cell % 24];
        }
        full.Problem.Can[0] = 50;
        full.Problem.Worker[0].Wage = 1500;
        full.Problem.Worker[0].Demands = new[] { RotaAsk(RotaDemandKind.Hours, 2, 0, 20) };
        RotaOpenHours(full, 0x7F, 9, 17);
        l = RotaClimb(full);
        Check(l.Count == 2 && l.Stop == RotaEnd.Most, "rota plans: a business with as many employees as the planner keeps is advised no hire on top (" + l.Count + " rungs)");

        // A ladder never has more rungs than it keeps, and stops at the most hires.
        c = RotaSouvenirs();
        c.Shop.Margin = 20000;
        l = RotaClimb(c);
        Check(l.Count <= RotaLadder.RungsMost && l.Rungs[l.Count - 1].Hires <= RotaLadder.HiresMost, "rota plans: no ladder advises more than " + RotaLadder.HiresMost + " hires (" + l.Rungs[l.Count - 1].Hires + ")");
    }

    private static void RotaStoreChecks()
    {
        var modes = new Dictionary<string, int> { { "Main Street~12", RotaMode.StaffFirst }, { "Odd^Road%~4", RotaMode.Lean }, { "Quiet Lane~1", RotaMode.Auto } };
        string line = RotaStore.Write(modes);
        var back = new Dictionary<string, int>();
        Check(line.StartsWith("v=" + RotaStore.Version + "^") && RotaStore.Read(line, back) && back.Count == 2, "rota save: the choices are written under a version and read back (" + line + ")");
        Check(back["Main Street~12"] == RotaMode.StaffFirst && back["Odd^Road%~4"] == RotaMode.Lean, "rota save: a place with the marks of the line in its name comes back as it was");
        Check(!back.ContainsKey("Quiet Lane~1"), "rota save: a business left to choose for itself has no entry");
        var other = new Dictionary<string, int> { { "Odd^Road%~4", RotaMode.Lean }, { "Main Street~12", RotaMode.StaffFirst } };
        Check(RotaStore.Write(other) == line, "rota save: the same choices give the same line whatever order they were made in");
        Check(RotaStore.Read(string.Empty, back) && back.Count == 0 && RotaStore.Read(null, back), "rota save: a save without the line has no choices");
        Check(!RotaStore.Read("v=9^1:Somewhere~1", back) && back.Count == 0, "rota save: a line of a later version is not read, and gives no choices");
        Check(!RotaStore.Read("v=1^7:Somewhere~1", back) && !RotaStore.Read("v=1^nonsense", back) && back.Count == 0, "rota save: a damaged line gives no choices rather than wrong ones");
        Check(RotaStore.Write(new Dictionary<string, int>()) == "v=" + RotaStore.Version, "rota save: with no choices the line is the version alone");
    }

    private static void RotaLadderTiming()
    {
        RotaProblem p = RotaGenerate(7, 40, 60, true);
        var c = new RotaCase { Shop = new RotaShop(), Problem = p };
        c.Shop.Size(40, 4);
        c.Shop.GroupKind[3] = RotaGroupKind.Cleaning;
        c.Shop.GroupKind[2] = RotaGroupKind.Support;
        for (int s = 0; s < 40; s++)
        {
            c.Shop.Group[s] = s < 20 ? 0 : s < 32 ? 1 : s < 36 ? 2 : 3;
            c.Shop.Cap[s] = s < 32 ? 20 : 0;
            c.Shop.StationWage[s] = 1500;
            p.Kind[s] = s >= 36 ? RotaKind.Cleaning : RotaKind.Work;
        }
        c.Shop.Margin = 400;
        c.Shop.Takings = 900;
        c.Shop.HasCustomers = true;
        c.Shop.DirtPerCustomer = 20;
        c.Shop.CleanPerHour = 8000;
        c.Shop.TheftRate = 5;
        c.Shop.TheftLimit = 100;
        for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
        {
            c.Shop.Wanting[cell] = RotaCurve[cell % 24] * 30;
            c.Shop.Customers[cell] = Math.Min(c.Shop.Wanting[cell], 240);
        }
        RotaOpenHours(c, 0x7F, 8, 20);
        c.Now.Size(40);
        c.Advisor.Advise(c.Shop, c.Problem, c.Solver, c.Now.At, null, c.Outlook);
        c.Advisor.Grid(c.Shop, c.Shop.Open, c.Problem);
        c.Solver.Rate(c.Problem, c.Now);
        c.Maker.Make(c.Shop, c.Problem, c.Solver, c.Advisor, c.Outlook, c.Now, null, RotaMode.Auto, c.Ladder);   // once to grow the arrays
        long whole = RotaLadderHash(c.Ladder);
        // The longest step, as the best of five runs: one run alone can be held up by whatever else the machine is doing.
        var into = new RotaLadderResult();
        var clock = new Stopwatch();
        double longest = double.MaxValue, total = double.MaxValue;
        int what = 0;
        for (int run = 0; run < 5; run++)
        {
            double runLongest = 0, runTotal = 0;
            int runWhat = 0;
            bool done = false;
            c.Maker.Begin(c.Shop, c.Problem, c.Solver, c.Advisor, c.Outlook, c.Now, null, RotaMode.Auto, into);
            while (!done)
            {
                int doing = c.Maker.Doing;
                clock.Restart();
                done = c.Maker.Step(1);
                double took = clock.Elapsed.TotalMilliseconds;
                runWhat = took > runLongest ? doing : runWhat;
                runLongest = Math.Max(runLongest, took);
                runTotal += took;
            }
            what = runLongest < longest ? runWhat : what;
            longest = Math.Min(longest, runLongest);
            total = Math.Min(total, runTotal);
        }
        Console.WriteLine("  rota plans timing, 40 stations and 60 employees: " + into.Count + " rung(s), " + into.Steps + " steps, " + total.ToString("0.0") + " ms in all, the longest step " + longest.ToString("0.000") + " ms (piece " + what + ")");
        Check(RotaLadderHash(into) == whole, "rota plans: for a large business too, a ladder made a step at a time is the one made in one go");
        Check(longest < 2.0, "rota plans: no single step of a large business's ladder takes a frame's share of two milliseconds here, the best of five runs (" + longest.ToString("0.000") + " ms; the game's runtime is several times slower, and the director looks at the clock every two steps)");
        Check(into.Rungs[1].Sum.Net >= into.Rungs[0].Sum.Net, "rota plans: for a large business too the best week with the people is never worse than the week as it is");
    }
}
