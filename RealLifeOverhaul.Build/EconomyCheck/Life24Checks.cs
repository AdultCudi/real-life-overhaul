using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Family;

// Three suggestions of players, built together (feature/life24): part-timers who keep set hours (Economy/Company/AvailabilityRules, AvailabilityGame, the rota's two
// demands, the mood a shift outside them costs), a holiday that rests every need and takes the family along at a price a head (Travel, Family/HolidayRules), and the
// bank's Black card tier for its richest savers (Economy/OwnBank/Cards/BlackTerms, OwnBankRules.Black): the tables and the rules, the day's books, the save, the texts and
// the places that carry each into both interfaces.
internal static partial class Program
{
    private static void Life24Checks()
    {
        int before = _run, failedBefore = _failed;
        var watch = Stopwatch.StartNew();
        Life24Tables();
        Life24Windows();
        Life24Rota();
        Life24AvailSources();
        Life24Prices();
        Life24HolidayRules();
        Life24HolidayState();
        Life24HolidaySources();
        Life24BlackTables();
        Life24BlackBook();
        Life24BlackChoices();
        Life24BlackStore();
        Life24BlackSources();
        Life24Texts();
        Console.WriteLine("  part-timers' hours, family holidays and the Black card: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // ------------------------------------------------------------------------------------------------------------------ part-timers with set hours

    private static void Life24Tables()
    {
        Check(AvailabilityRules.PartTime(30) && AvailabilityRules.PartTime(10) && !AvailabilityRules.PartTime(31) && !AvailabilityRules.PartTime(50) && !AvailabilityRules.PartTime(0),
            "set hours: only a week of up to thirty hours is a part-time week, as the game's Part-time demand is (none, forty or fifty hours are not)");
        Check(AvailabilityRules.Of("", 20).None && AvailabilityRules.Of(null, 20).None && AvailabilityRules.Of("emp-1", 40).None && AvailabilityRules.Of("emp-1", 0).None,
            "set hours: nobody without an id or a part-time week has any");
        var kinds = new int[5];
        bool shapes = true, same = true;
        for (int i = 0; i < 4000; i++)
        {
            string id = "employee-" + i.ToString(CultureInfo.InvariantCulture);
            AvailabilityWindow w = AvailabilityRules.Of(id, 20), again = AvailabilityRules.Of(id, 25);
            kinds[(int)w.Kind]++;
            same &= w.Kind == again.Kind && w.Days == again.Days && w.From == again.From && w.To == again.To;
            switch (w.Kind)
            {
                case AvailabilityKind.Student:
                    shapes &= AvailabilityRules.DayCount(w) == 3 && (w.Days & ~AvailabilityRules.WeekdaysMask) == 0 && w.From == 16 && w.To == 22;
                    break;
                case AvailabilityKind.Parent:
                    shapes &= w.Days == AvailabilityRules.WeekdaysMask && w.From == 9 && w.To == 15;
                    break;
                case AvailabilityKind.EarlyRiser:
                    shapes &= w.Days == AvailabilityRules.WeekdaysMask && w.From == 6 && w.To == 14;
                    break;
                case AvailabilityKind.Weekender:
                    shapes &= w.Days == AvailabilityRules.WeekendMask && w.From == 8 && w.To == 20;
                    break;
                default:
                    shapes = false;
                    break;
            }
        }
        Check(shapes, "set hours: a student has three weekdays from 16:00 to 22:00, a parent the school day 09:00 to 15:00 on weekdays, an early riser 06:00 to 14:00 on weekdays and a weekender 08:00 to 20:00 on weekends");
        Check(same, "set hours: the same person has the same hours whatever the hours a week asked, on every machine and every run (they come from the id alone)");
        Check(kinds[0] == 0 && L24Near(kinds[1], 1600) && L24Near(kinds[2], 1000) && L24Near(kinds[3], 600) && L24Near(kinds[4], 800),
            "set hours: of the part-timers about 40% are students, 25% parents, 15% early risers and 20% weekenders (" + kinds[1] + ", " + kinds[2] + ", " + kinds[3] + ", " + kinds[4] + " of 4000)");
        var studentDays = new HashSet<int>();
        for (int i = 0; i < 4000; i++)
        {
            AvailabilityWindow w = AvailabilityRules.Of("s" + i, 20);
            if (w.Kind == AvailabilityKind.Student)
            {
                studentDays.Add(w.Days);
            }
        }
        Check(studentDays.Count == 10, "set hours: students have every one of the ten ways to have three of five weekdays (" + studentDays.Count + ")");
    }

    private static bool L24Near(int got, int want) => Math.Abs(got - want) <= want / 5 + 10;

    private static AvailabilityWindow L24Kind(AvailabilityKind kind)
    {
        for (int i = 0; i < 4000; i++)
        {
            AvailabilityWindow w = AvailabilityRules.Of("find-" + i, 20);
            if (w.Kind == kind)
            {
                return w;
            }
        }
        return default(AvailabilityWindow);
    }

    private static void Life24Windows()
    {
        AvailabilityWindow s = L24Kind(AvailabilityKind.Student);
        int firstDay = 0;
        while ((s.Days >> firstDay & 1) == 0)
        {
            firstDay++;
        }
        Check(s.Allows(firstDay, 16) && s.Allows(firstDay, 21) && !s.Allows(firstDay, 22) && !s.Allows(firstDay, 15) && !s.Allows(5, 18) && !s.Allows(6, 18) && !s.Allows(-1, 18) && !s.Allows(7, 18),
            "set hours: a student works from 16:00 to the end of 21:00, on their three weekdays and never at a weekend");
        Check(s.HoursOff(firstDay, 16, 22) == 0 && s.HoursOff(firstDay, 14, 22) == 2 && s.HoursOff(firstDay, 20, 24) == 2 && s.HoursOff(5, 16, 22) == 6 && s.HoursOff(firstDay, 0, 24) == 18,
            "set hours: the hours of a shift outside them are counted hour by hour (a shift from 14:00 has two, from 20:00 to midnight two, a whole Saturday evening six, a day eighteen)");
        ulong offHours = 0;
        for (int h = 0; h < 24; h++)
        {
            offHours |= h < 16 || h >= 22 ? 1UL << h : 0UL;
        }
        Check(AvailabilityRules.HoursOffMask(s) == offHours && AvailabilityRules.DaysOffMask(s) == (~(ulong)s.Days & 0x7FUL) && (AvailabilityRules.DaysOffMask(s) & 0x60UL) == 0x60UL,
            "set hours: the rota's two masks are the hours and the days outside the window, the weekend among the days");
        Check(AvailabilityRules.Going(1000, s) == 940 && AvailabilityRules.Going(1000, default(AvailabilityWindow)) == 1000 && AvailabilityRules.Going(0, s) == 0,
            "set hours: a person whose hours suit them takes " + AvailabilityRules.DiscountPercent + " per cent less than the going rate, and nobody else's rate changes");
        Check(AvailabilityRules.Cost(0) == 0 && AvailabilityRules.Cost(1) == RotaCatalogue.DemandWeight[1] && AvailabilityRules.Cost(4) == 4 * RotaCatalogue.DemandWeight[1]
              && AvailabilityRules.Cost(1000) == AvailabilityRules.CostMostTenths && AvailabilityRules.Cost(-2) == 0,
            "set hours: each hour outside them takes what the rota gives a medium demand of the game's (" + RotaCatalogue.DemandWeight[1] + " tenths of a point), a day's cost held to " + AvailabilityRules.CostMostTenths);
        Check(AvailabilityRules.KindKey(AvailabilityKind.Student) == "reallife_avail_student" && AvailabilityRules.KindKey(AvailabilityKind.Weekender) == "reallife_avail_weekend" && AvailabilityRules.KindKey(AvailabilityKind.None) == null,
            "set hours: every kind has a name key and none has none");
    }

    private static void Life24Rota()
    {
        // One student, a business open every day from 09:00 to 21:00: the rota covers what their hours allow and nothing else.
        AvailabilityWindow w = L24Kind(AvailabilityKind.Student);
        RotaProblem p = RotaMake(1, 1);
        RotaWant(p, 0, 0x7F, 9, 21);
        var asks = new List<RotaDemand>();
        asks.Add(new RotaDemand { Kind = RotaDemandKind.FreeDays, Priority = RotaCatalogue.PriorityHigh, Mask = AvailabilityRules.DaysOffMask(w) });
        asks.Add(new RotaDemand { Kind = RotaDemandKind.NoHours, Priority = RotaCatalogue.PriorityHigh, Mask = AvailabilityRules.HoursOffMask(w) });
        p.Worker[0].Demands = asks.ToArray();
        RotaPlan plan = RotaSolve(p, "a student's set hours");
        bool inside = true;
        int worked = 0;
        for (int d = 0; d < 7; d++)
        {
            for (int h = 0; h < 24; h++)
            {
                if (plan.At[(d * 24 + h) * p.Stations] == 0)
                {
                    worked++;
                    inside &= w.Allows(d, h);
                }
            }
        }
        int allowed = 0;
        for (int d = 0; d < 7; d++)
        {
            for (int h = 9; h < 21; h++)
            {
                allowed += w.Allows(d, h) ? 1 : 0;
            }
        }
        Check(inside && worked == allowed && allowed == 15, "set hours, rota: the student is never planned outside their hours, and is planned for all of them that the business is open (" + worked + " of " + allowed + " hours)");
        Check(!plan.Full && plan.Gaps.Count > 0, "set hours, rota: the hours nobody else can cover are told as gaps, not given to the student");
        // The same week for a person with no hours is planned out of their reach.
        RotaProblem free = RotaMake(1, 1);
        RotaWant(free, 0, 0x7F, 9, 21);
        RotaPlan all = RotaSolve(free, "the same week with no set hours");
        Check(all.CoveredHours > plan.CoveredHours, "set hours, rota: without them one person covers more of the week (" + all.CoveredHours + " hours against " + plan.CoveredHours + ")");
        // Two people: the student's hours and a full-timer for the rest, so the business is covered and the student keeps to theirs.
        RotaProblem two = RotaMake(1, 2);
        RotaWant(two, 0, 0x7F, 9, 21);
        two.Worker[0].Demands = asks.ToArray();
        RotaPlan both = RotaSolve(two, "a student and a full-timer");
        bool kept = true;
        for (int d = 0; d < 7; d++)
        {
            for (int h = 0; h < 24; h++)
            {
                kept &= both.At[(d * 24 + h) * two.Stations] != 0 || w.Allows(d, h);
            }
        }
        Check(kept, "set hours, rota: with somebody else to take the other hours the student still only works theirs");
    }

    private static void Life24AvailSources()
    {
        string settings = ModSource(@"Core\Settings.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs"), tree = ModSource(@"Meta\OptionsTree.cs"), list = ModSource(@"Meta\FeatureList.Economy.cs");
        Check(settings.Contains("public const string StaffAvailabilityId = \"staff_availability\";") && settings.Contains("public static bool StaffAvailability = false;")
              && settings.Contains("StaffAvailability = Bool(modId, StaffAvailabilityId, StaffAvailability);"),
            "set hours: the option exists, is off by default (it changes how hiring behaves in a running save) and is read from the saved settings");
        Check(mod.Contains(".AddToggle(Settings.StaffAvailabilityId, \"reallife_avail_option\", Settings.StaffAvailability,") && mod.Contains("\"reallife_avail_test\"")
              && tree.Contains(".Opt(\"staff_availability\", \"economy_company\")") && list.Contains("Settings.EconomyRotaId, Settings.StaffAvailabilityId") && list.Contains("\"reallife_avail_test\""),
            "set hours: the option is on the options page and its section, owned by the rota's feature, with its test button");
        string game = ModSource(@"Economy\Company\AvailabilityGame.cs"), read = ModSource(@"Economy\Company\RotaGame.Read.cs"), demands = ModSource(@"Economy\Company\RotaGame.Demands.cs");
        string company = ModSource(@"Economy\Company\CompanyGame.cs"), day = ModSource(@"Economy\Company\CompanyDirector.Staff.cs"), stamp = ModSource(@"Economy\Company\RotaGame.cs");
        Check(game.Contains("if (!Settings.StaffAvailability ||") && game.Contains("hours.MaxHours > 0") && game.Contains("AvailabilityRules.PartTime(most)") && game.Contains("TechSeats"),
            "set hours: nothing without the option, and only for a part-time week of the game's own demand and not for professionals a tech firm seats by its own weeks");
        Check(read.Contains("AvailabilityGame.Demands(window, Demands)") && read.Contains("DemandsMost - (window.None ? 0 : 2)") && demands.Contains("AvailabilityGame.IsWindowDemand(window, demand)")
              && stamp.Contains("Settings.StaffAvailability ? 1 : 0"),
            "set hours: the rota planner is given the two demands of the window beside the game's, they are named in its reasons, and a change of the option works a sheet out again");
        Check(company.Contains("AvailabilityGame.Judge(e, weekday, out _)") && company.Contains("AvailabilityRules.Going(f.Going, window)") && company.Contains("f.Fresh |= fresh && !window.None")
              && day.Contains("AvailabilityDay();"),
            "set hours: the day's shifts outside them are judged with the staff's hours, their going rate is the lower one and a new hire is told of theirs");
        Check(game.Contains("e.isAbsent || e.isTrainingDay") && game.Contains("BusinessCatalogue.SatisfactionFloor") && !game.Contains("hourlyWage"),
            "set hours: nobody absent or in training is judged, mood never falls under business pressure's floor, and no wage of the game's is written");
        string tip = ModSource(@"Ui\Tooltips\TooltipEmployee.cs"), picker = ModSource(@"Ui\BizMan\Ops\BizManScreen.Schedule.cs"), person = ModSource(@"Ui\Employees\EmployeesScreen.View.cs");
        string kit = ModSource(@"Ui\Company\CompanyLive.Staff.cs"), old = ModSource(@"Economy\Company\CompanyApp.Staff.cs");
        Check(tip.Contains("AvailabilityGame.Words(window)") && tip.Contains("reallife_avail_tip_off"), "set hours: the hover card says them, and warns when the week has a shift outside them");
        Check(picker.Contains("window.Allows((int)ScheduleHelper.CurrentScheduleDay.day - 1, hour)") && picker.Contains("reallife_ui_bizops_fit_window") && picker.Contains("AvailabilityGame.WeekOff(e, window) > 0"),
            "set hours: the schedule's picker marks a person whose hours the hour being filled is outside of, and the board warns of a shift outside them");
        Check(person.Contains("rows[n] = new UiStaffDemand") && person.Contains("AvailabilityGame.Words(window)"), "set hours: a person's page and a candidate's list their hours among what they ask");
        Check(kit.Contains("reallife_avail_sec") && old.Contains("reallife_avail_sec") && kit.Contains("AvailabilityGame.DaysWords(window)") && old.Contains("AvailabilityGame.DaysWords(window)"),
            "set hours: the Company app's staff page shows them in both interfaces");
    }

    // ------------------------------------------------------------------------------------------------------------------ holidays and the family

    private static void Life24Prices()
    {
        RealLifeOverhaul.Trip trip = TravelCatalogue.TripById("europe");
        int alone = TravelCatalogue.TripPrice(trip, 10, false, false, false, 0), oneMore = TravelCatalogue.TripPrice(trip, 10, false, true, false, 0);
        Check(alone == TravelCatalogue.TripPrice(trip, 10, false, 0, 0, false, 0) && oneMore == TravelCatalogue.TripPrice(trip, 10, false, 1, 0, false, 0),
            "family holiday: a trip alone or with one companion costs what it did before the family could come");
        int partner = oneMore, family = TravelCatalogue.TripPrice(trip, 10, false, 1, 2, false, 0), kids = TravelCatalogue.TripPrice(trip, 10, false, 0, 2, false, 0), child = TravelCatalogue.TripPrice(trip, 10, false, 0, 1, false, 0);
        Check(alone < child && child < partner && partner < family && kids < family,
            "family holiday: each head is paid for, a child less than a grown person (" + alone + " alone, " + child + " with a child, " + partner + " with a partner, " + family + " with a partner and two children)");
        float fare = trip.Price * (float)L24Season(trip, 10) * (1f + TravelCatalogue.CompanionShare + 2 * TravelCatalogue.ChildShare);
        Check(family == (int)Math.Round(Prices.At(fare) / 10f) * 10, "family holiday: a partner and two children cost the price times one and a share for each head (" + family + ")");
        Check(TravelCatalogue.TripPrice(trip, 10, false, 1, 2, true, 0) > family && TravelCatalogue.TripPrice(trip, 10, true, 1, 2, false, 0) > family,
            "family holiday: first class and insurance are priced on the whole family's fare");
        int coachOne = TravelCatalogue.TripPrice(trip, 10, false, 0, 0, false, 100) - alone, coachFamily = TravelCatalogue.TripPrice(trip, 10, false, 1, 2, false, 100) - family;
        Check(coachOne > 0 && Math.Abs(coachFamily - 4 * coachOne) <= 30, "family holiday: a coach seat is paid for each head, four for a partner and two children (" + coachOne + " for one, " + coachFamily + " for four)");
        Check(TravelCatalogue.TripPrice(trip, 10, false, -3, -2, false, 0) == alone, "family holiday: a negative number of heads is none");
    }

    private static double L24Season(RealLifeOverhaul.Trip trip, int day)
    {
        TravelSeason season = TravelCatalogue.Season(day);
        return season == trip.Peak ? 1.25 : season == trip.Low ? 0.85 : 1.0;
    }

    private static void Life24HolidayRules()
    {
        Check(HolidayRules.Affection(0) == HolidayRules.AffectionBase && HolidayRules.Affection(3) > HolidayRules.Affection(1) && HolidayRules.Affection(5) == HolidayRules.Affection(30)
              && HolidayRules.Affection(5) > 9f,
            "family holiday: the partner's affection grows with the days of the trip up to " + HolidayRules.DaysCounted + " and is more than a date's");
        Check(HolidayRules.Harmony(1) < HolidayRules.Harmony(4) && HolidayRules.Harmony(30) <= HolidayRules.HarmonyMost && HolidayRules.Harmony(-3) == HolidayRules.HarmonyBase,
            "family holiday: the home's harmony rises with the days, never past what shared time may give");
        Check(HolidayRules.Closeness(1) < HolidayRules.Closeness(3) && HolidayRules.Closeness(30) == HolidayRules.ClosenessMost && HolidayRules.ClosenessMost <= 8f,
            "family holiday: a child grows closer with the days, never more than a school play brings");
    }

    private static void Life24HolidayState()
    {
        TravelState.Reset();
        TravelState.TicketId = "europe";
        TravelState.CompanionId = Holidays24Family;
        TravelState.CompanionName = "Mia";
        TravelState.KinAdults = 1;
        TravelState.KinKids = TravelState.KidName("Ben; Jr") + "," + TravelState.KidName("Zoe=2") + "," + TravelState.KidName("Ann, Marie");
        string line = TravelState.Write();
        TravelState.Reset();
        TravelState.Read(line);
        var kids = new List<string>();
        int count = TravelState.KidList(kids);
        Check(TravelState.CompanionId == Holidays24Family && TravelState.KinAdults == 1 && count == 3 && kids[0] == "Ben  Jr" && kids[1] == "Zoe 2" && kids[2] == "Ann  Marie",
            "family holiday: the family on a ticket is saved and read back (the partner, three children), the line's separators taken out of their names");
        TravelState.KinKids = "A,B,C,D,E";
        Check(TravelState.KidList(kids) == TravelState.MostKids, "family holiday: no more children than a family has are read from a line");
        TravelState.Reset();
        TravelState.Read("ticket=europe;comp=reallife-friend;compname=Alex;");
        Check(TravelState.KinAdults == 0 && TravelState.KidList(kids) == 0 && TravelState.CompanionName == "Alex", "family holiday: a save from before the family can come reads as a trip alone or with one friend");
        TravelState.KinAdults = 5;
        string odd = TravelState.Write();
        TravelState.Reset();
        TravelState.Read(odd);
        Check(TravelState.KinAdults == 0 || TravelState.KinAdults == 1, "family holiday: at most one partner is read");
        TravelState.TicketId = "europe";
        TravelState.CompanionId = "x";
        TravelState.KinAdults = 1;
        TravelState.KinKids = "A";
        TravelState.ClearTicket();
        Check(TravelState.KinAdults == 0 && TravelState.KinKids == null && TravelState.CompanionId == null, "family holiday: a ticket given up or used takes its family with it");
        TravelState.Reset();
    }

    private const string Holidays24Family = "reallife-family";

    private static void Life24HolidaySources()
    {
        string settings = ModSource(@"Core\Settings.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs"), tree = ModSource(@"Meta\OptionsTree.cs"), list = ModSource(@"Meta\FeatureList.Life.cs");
        Check(settings.Contains("public const string HolidayRestId = \"holiday_rest\";") && settings.Contains("public static bool HolidayRest = false;") && settings.Contains("HolidayRest = Bool(modId, HolidayRestId, HolidayRest);"),
            "holiday rest: the option exists, is off by default (free needs change the balance) and is read from the saved settings");
        Check(mod.Contains(".AddToggle(Settings.HolidayRestId, \"reallife_travel_rest\", Settings.HolidayRest,") && tree.Contains("\"travel_extras\", \"holiday_rest\"") && list.Contains("Settings.TravelExtrasId, Settings.HolidayRestId"),
            "holiday rest: the option is on the options page under travel and owned by the travel feature");
        string effects = ModSource(@"Travel\TravelEffects.cs"), trip = ModSource(@"Travel\Holidays.Trip.cs"), holidays = ModSource(@"Travel\Holidays.cs");
        Check(effects.Contains("Feed(100f - save.Hunger)") && effects.Contains("EnergyHelper.GenerateEnergy(100f - save.Energy)") && effects.Contains("TravelBridges.Home.Freshen(3)") && effects.Contains("ModState.AddModifier(HolidayJoy)")
              && effects.Contains("(HolidayJoy, HolidayJoyAmount, 12)"),
            "holiday rest: food and energy go to their top, hygiene to full through home life, and a mood of the holiday's own fills happiness for twelve hours");
        int rests = L24Count(trip, "TravelEffects.RestNeeds()") + L24Count(holidays, "TravelEffects.RestNeeds()");
        Check(rests == 3 && trip.Contains("if (Settings.HolidayRest)") && holidays.Contains("if (Settings.HolidayRest)"),
            "holiday rest: needs are filled as a trip begins, each morning of it and on the way home (and for the Hamptons weekend when it ends), and only with the option on (" + rests + " places)");
        Check(trip.IndexOf("TravelEffects.RestNeeds();   // before the jet lag", StringComparison.Ordinal) > 0 && trip.IndexOf("TravelEffects.RestNeeds();   // before the jet lag", StringComparison.Ordinal) < trip.IndexOf("if (jetLag)", StringComparison.Ordinal),
            "holiday rest: the needs are filled before the jet lag's cost, which stays");
        Check(trip.Contains("else if (save.Hunger < 70f)") && trip.Contains("TravelEffects.Feed(70f - save.Hunger);"), "holiday rest: with the option off the holiday gives three meals a day as it did");
        string bridges = ModSource(@"Travel\TravelBridges.cs"), life = ModSource(@"Wiring\LifeLinks.cs"), api = ModSource(@"Family\FamilyBridges.cs"), dialog = ModSource(@"Travel\TravelAgentDialog.cs");
        Check(bridges.Contains("interface ITravelFamily") && bridges.Contains("_family = null;") && life.Contains("TravelBridges.Family = new FamilyForTravel();") && life.Contains("TravelBridges.Family = null;"),
            "family holiday: travel reaches the family through its own bridge, wired and let go with the others");
        Check(api.Contains("public static bool HolidayFamily(out string partner, List<string> children)") && api.Contains("Partner.Tell(FamilyEvent.SharedTime, HolidayRules.Affection(days), HolidayRules.TrustGiven)")
              && api.Contains("FamilyState.Harmony(HolidayRules.Harmony(days))") && api.Contains("kids[i].Closeness = UnityEngine.Mathf.Min(100f, kids[i].Closeness + HolidayRules.Closeness(days))"),
            "family holiday: the family is those at home, and coming back the partner's affection, the home's harmony and each child's closeness move by the family's own rules for time together");
        Check(holidays.Contains("Settings.TravelExtras") && holidays.Contains("AddFamily(into)") && trip.Contains("TravelBridges.Family.TripTogether(TravelState.KinAdults > 0, FamilyKids, days)")
              && dialog.Contains("reallife_travel_agent_option_family") && dialog.Contains("Host.Price(trip, first, companion, insured, true)"),
            "family holiday: the agent offers the family beside each companion, with its price, when extras are on, and the trip's end gives time together");
    }

    private static int L24Count(string text, string part)
    {
        int n = 0, at = 0;
        while ((at = text.IndexOf(part, at, StringComparison.Ordinal)) >= 0)
        {
            n++;
            at += part.Length;
        }
        return n;
    }

    // ------------------------------------------------------------------------------------------------------------------ the Black card

    private static OwnBank L24Bank(int seed, CycleState cycle = CycleState.Steady, int risk = 0)
    {
        BankCity c = RiskCity(risk, cycle);
        OwnBank b = TierBank(c, BankTiers.National, seed, 3 * OwnBankRules.TierCapital(BankTiers.National, c.Level, 0, 0));
        b.Deposits[OwnBankCatalogue.People] = 40000000000L;   // $400 million: some 160,000 people
        b.Reserves += 40000000000L;
        b.CardsOn = true;
        b.BlackOn = true;
        b.BlackWealth = 0;
        b.BlackScore = 700;
        return b;
    }

    private static void Life24BlackTables()
    {
        bool steps = true;
        for (int i = 1; i < BlackTerms.WealthLines.Length; i++)
        {
            steps &= BlackTerms.WealthLines[i] > BlackTerms.WealthLines[i - 1] && BlackTerms.TailPpm(BlackTerms.WealthLines[i]) < BlackTerms.TailPpm(BlackTerms.WealthLines[i - 1]);
        }
        for (int i = 1; i < BlackTerms.FeeSteps.Length; i++)
        {
            steps &= BlackTerms.FeeSteps[i] > BlackTerms.FeeSteps[i - 1];
        }
        for (int i = 1; i < BlackTerms.RewardBp.Length; i++)
        {
            steps &= BlackTerms.RewardBp[i] > BlackTerms.RewardBp[i - 1];
        }
        Check(steps && BlackTerms.WealthDefault < BlackTerms.WealthLines.Length && BlackTerms.FeeDefault < BlackTerms.FeeSteps.Length && BlackTerms.RewardDefault < BlackTerms.RewardBp.Length,
            "Black card: the wealth lines, fees and rewards are steps in order, the richer the line the fewer savers hold it, and every default is a step");
        int ppm = BlackTerms.TailPpm(BlackTerms.WealthLines[0]);
        Check(ppm > 500 && ppm < 3000 && BlackTerms.TailPpm(1L) == 1000000, "Black card: about one saver in a thousand holds $100,000 (" + ppm + " in a million), and everyone holds a cent");
        L24Tier(700, out int share700, out int mult700);
        L24Tier(840, out int share840, out int mult840);
        Check(share700 > share840 && share840 > 0 && mult840 < mult700 && mult840 < 1000 && mult700 < 1100, "Black card: a higher score line takes fewer savers and better ones (" + share700 + " and " + share840 + " in a thousand, defaults " + mult700 + " and " + mult840 + ")");
        Check(BlackTerms.ScoreOf(731) == 720 && BlackTerms.ScoreOf(10) == BlackTerms.ScoreLeast && BlackTerms.ScoreOf(9999) == BlackTerms.ScoreMost && BlackTerms.Clamp(9, 0, 3) == 3 && BlackTerms.Clamp(-1, 0, 3) == 0,
            "Black card: a score line is held to its steps and its ends");
        Check(CardTerms.BlackKey == "reallife_ob_card_black" && BlackTerms.NameKey == "reallife_ob_black_tier" && BlackTerms.NameKey != CardTerms.BlackKey,
            "Black card: the tier's name is its own, beside the owner's own Black card, which is a different thing");
    }

    private static void L24Tier(int line, out int share, out int mult) => OwnBankRules.BlackScores(line, out share, out mult);

    private static void Life24BlackBook()
    {
        BankCity steady = RiskCity(0);
        OwnBank b = L24Bank(150);
        var day = new BankDay();
        bool balanced = true, lines = true, held = true;
        long interchange = 0, fees = 0;
        for (int d = 2; d < 2 + 3 * TierYear && b.Running; d++)
        {
            OwnBankRules.Day(b, steady, d, null, day);
            interchange += day.CardInterchange;
            fees += day.CardFees;
            balanced &= !b.Running || OwnBankRules.Balanced(b);
            BankLines l = BankLines.Of(day);
            lines &= l.In == day.Income && l.Out == day.Costs && l.Cards == day.CardInterest + day.CardInterchange + day.CardFees && l.CardCosts == day.CardLost + day.CardFraud + day.CardCosts;
            held &= b.BlackHolders >= 0 && b.BlackBal >= 0 && b.BlackArr >= 0 && b.BlackRisk > 0;
        }
        Check(balanced && lines && held, "Black card: three years of the tier, the books balance every day, the statement's lines add up to the day, and no figure is under nothing");
        Check(b.BlackHolders > 0 && b.BlackBal > 0 && b.CardsHeld >= b.BlackHolders && b.CardBook >= b.BlackBal && b.CardArrears >= b.BlackArr,
            "Black card: the richest savers take the card (" + b.BlackHolders + " of " + OwnBankRules.BlackWanted(b) + " wanted) and carry " + Dollars(b.BlackBal) + ", counted in the bank's card book and cards held");
        long[] last = b.BlackLast;
        long month = last[CardTerms.FigInterest] + last[CardTerms.FigInterchange] + last[CardTerms.FigFees] - last[CardTerms.FigLost] - last[CardTerms.FigFraud] - last[CardTerms.FigCosts];
        Check(last[CardTerms.FigFees] > 0 && last[CardTerms.FigInterchange] > 0 && last[CardTerms.FigCosts] > 0 && last[CardTerms.FigSpend] > 0 && last[CardTerms.FigFraud] > 0 && month > 0,
            "Black card: in a steady city a month of the tier earns its fee, the shops' fee and interest, costs rewards, running costs and fraud, and is in profit (" + Dollars(month) + ")");
        Check(fees > 0 && interchange > 0 && b.CardFig[CardTerms.FigFees] >= b.BlackFig[CardTerms.FigFees] && b.CardFig[CardTerms.FigSpend] >= b.BlackFig[CardTerms.FigSpend],
            "Black card: its figures go into the cards' own month and the day's card lines too, where the other tiers' do");
        long weighted = OwnBankRules.RiskWeighted(b);
        b.BlackBal += 1000000;
        b.BlackArr += 1000000;
        long more = OwnBankRules.RiskWeighted(b) - weighted;
        b.BlackBal -= 1000000;
        b.BlackArr -= 1000000;
        Check(more == 1000000L * CardTerms.CardWeight / 1000 + 1000000L * OwnBankCatalogue.ArrearsWeight / 1000, "Black card: the regulator weighs a Black card balance as any card's and its arrears as any arrears (" + more + ")");
        // Its risk follows the economy by the loans' model.
        OwnBank slump = L24Bank(151, CycleState.Recession, 1), boom = L24Bank(151, CycleState.Boom, 1);
        Check(OwnBankRules.BlackDefaultPerMille(RiskCity(1, CycleState.Recession), slump) > OwnBankRules.BlackDefaultPerMille(RiskCity(1, CycleState.Boom), boom),
            "Black card: its defaults follow the economy by the loans' own risk model, more in a recession than in a boom");
        Check(OwnBankRules.BlackDefaultPerMille(steady, b) < OwnBankRules.DefaultPerMille(steady, b, OwnBankCatalogue.Fair) * CardTerms.DefaultMult[OwnBankCatalogue.Fair] / 1000,
            "Black card: it fails less often than a card for the fair borrowers, whose scores are lower");
    }

    private static void Life24BlackChoices()
    {
        BankCity c = RiskCity(0);
        OwnBank b = L24Bank(152);
        int normal = OwnBankRules.BlackWanted(b);
        b.BlackWealth = 3;
        int rich = OwnBankRules.BlackWanted(b);
        b.BlackWealth = 0;
        b.BlackScore = 840;
        int strict = OwnBankRules.BlackWanted(b);
        b.BlackScore = 700;
        b.BlackFee = 3;
        int dear = OwnBankRules.BlackWanted(b);
        b.BlackFee = 0;
        int cheap = OwnBankRules.BlackWanted(b);
        b.BlackFee = BlackTerms.FeeDefault;
        b.BlackReward = 3;
        int generous = OwnBankRules.BlackWanted(b);
        b.BlackReward = 0;
        int mean = OwnBankRules.BlackWanted(b);
        b.BlackReward = BlackTerms.RewardDefault;
        Check(normal > 0 && rich < normal && strict < normal && dear < cheap && mean < generous,
            "Black card: a richer wealth line, a stricter score line and a dearer fee bring fewer cards, richer rewards bring more (" + normal + " at the lines, " + rich + " richer, " + strict + " stricter, " + dear + " and " + cheap
              + " for the fees, " + mean + " and " + generous + " for the rewards)");
        Check(OwnBankRules.BlackRich(b, 0) > OwnBankRules.BlackRich(b, 3) && OwnBankRules.BlackRich(b, 3) >= 0, "Black card: the savers who hold enough fall as the line rises");
        b.BlackOn = false;
        Check(OwnBankRules.BlackWanted(b) > 0, "Black card: what clients would take does not depend on the switch, which only decides whether it is offered");
        b.BlackOn = true;
        // Withdrawn, the book runs down and is paid off, never to be under nothing.
        var day = new BankDay();
        RunBank(b, c, 2, 2 * TierYear, null, day);
        int out1 = b.BlackHolders;
        b.BlackOn = false;
        bool nonNegative = true;
        RunBank(b, c, 2 + 2 * TierYear, 3 * TierYear, null, day, d => nonNegative &= b.BlackHolders >= 0 && b.BlackBal >= 0 && b.BlackArr >= 0);
        Check(out1 > 0 && b.BlackHolders < out1 && nonNegative, "Black card: with the tier withdrawn, no new card is issued and the book runs down (" + out1 + " cards before, " + b.BlackHolders + " after three years)");
        OwnBank closed = L24Bank(153);
        closed.CardsOn = false;
        RunBank(closed, c, 2, 2 * TierYear, null, new BankDay());
        Check(closed.BlackHolders == 0, "Black card: with the card business closed no Black card is issued either");
    }

    private static void Life24BlackStore()
    {
        OwnBank b = FullBank();
        b.BlackOn = true;
        b.BlackWealth = 2;
        b.BlackScore = 780;
        b.BlackFee = 3;
        b.BlackReward = 2;
        b.BlackHolders = 321;
        b.BlackBal = 98765432;
        b.BlackArr = 4321;
        b.BlackRisk = 870;
        for (int i = 0; i < CardTerms.Figures; i++)
        {
            b.BlackFig[i] = 300 + i;
            b.BlackLast[i] = 400 + i;
        }
        string line = OwnBankStore.Write(b);
        OwnBank back = OwnBankStore.Read(line, out _);
        bool fields = back != null && back.BlackOn && back.BlackWealth == 2 && back.BlackScore == 780 && back.BlackFee == 3 && back.BlackReward == 2 && back.BlackHolders == 321 && back.BlackBal == 98765432
                      && back.BlackArr == 4321 && back.BlackRisk == 870;
        for (int i = 0; back != null && i < CardTerms.Figures; i++)
        {
            fields &= back.BlackFig[i] == 300 + i && back.BlackLast[i] == 400 + i;
        }
        Check(fields && OwnBankStore.Write(back) == line, "Black card: every field of the tier is saved and read back, the line the same again");
        string older = System.Text.RegularExpressions.Regex.Replace(line, @"\^x:[^\^]*", string.Empty);
        OwnBank oldBank = OwnBankStore.Read(older, out _);
        Check(older != line && oldBank != null && !oldBank.BlackOn && oldBank.BlackHolders == 0 && oldBank.BlackBal == 0 && oldBank.BlackWealth == BlackTerms.WealthDefault && oldBank.BlackScore == BlackTerms.ScoreDefault
              && oldBank.BlackRisk == 1000 && oldBank.CardsOn,
            "Black card: a save from before the tier has none of it, at its default lines and off, and the rest of the bank is read as before");
        string odd = line.Replace("^x:1|2|780|3|2|321", "^x:1|9|9999|9|9|-5");
        OwnBank clamped = OwnBankStore.Read(odd, out _);
        Check(clamped != null && clamped.BlackWealth == BlackTerms.WealthLines.Length - 1 && clamped.BlackScore == BlackTerms.ScoreMost && clamped.BlackFee == BlackTerms.FeeSteps.Length - 1
              && clamped.BlackReward == BlackTerms.RewardBp.Length - 1 && clamped.BlackHolders == 0, "Black card: a line outside the steps is held to them and a negative count read as none");
        // A bank taken by the regulator takes the tier with it.
        OwnBank gone = L24Bank(154);
        RunBank(gone, RiskCity(0), 2, TierYear, null, new BankDay());
        Check(gone.BlackHolders > 0 && gone.CardBook >= gone.BlackBal && gone.OtherAssets >= gone.BlackBal, "Black card: what the tier carries is among the bank's other assets, so the books balance with it");
    }

    private static void Life24BlackSources()
    {
        string bank = ModSource(@"Economy\OwnBank\OwnBank.cs"), store = ModSource(@"Economy\OwnBank\OwnBankStore.cs"), rules = ModSource(@"Economy\OwnBank\OwnBankRules.cs"), cards = ModSource(@"Economy\OwnBank\Cards\OwnBankRules.Cards.cs");
        string risk = ModSource(@"Economy\OwnBank\Risk\OwnBankRules.Risk.cs"), app = ModSource(@"Economy\OwnBank\OwnBankApp.Growth.cs"), reader = ModSource(@"Economy\OwnBank\OwnBankReader.Growth.cs");
        string director = ModSource(@"Economy\OwnBank\Cards\OwnBankDirector.Cards.cs"), build = ModSource(@"Ui\Money\OwnBankBuild.Growth.cs"), live = ModSource(@"Ui\Money\OwnBankLive.cs"), model = ModSource(@"Ui\Money\MoneyModel.cs");
        Check(cards.Contains("BlackDay(b, c, into, hold, cycle, caps, stopped);") && cards.Contains("Array.Copy(b.BlackFig, b.BlackLast, b.BlackFig.Length);"), "Black card: the bank's day runs the tier after the five kinds, and its month closes with theirs");
        Check(bank.Contains("long sum = OwnOwed + BlackBal;") && bank.Contains("long sum = BlackArr;") && bank.Contains("int sum = BlackHolders;") && rules.Contains("b.BlackBal * CardTerms.CardWeight / 1000 + b.BlackArr * OwnBankCatalogue.ArrearsWeight / 1000")
              && risk.Contains("(j == OwnBankCatalogue.Good ? b.BlackBal + b.BlackArr : 0)"),
            "Black card: its balance and arrears are in the card book, the cards held, the regulator's risk weights and the kind concentration");
        Check(rules.Contains("b.BlackHolders = 0;") && rules.Contains("Array.Clear(b.BlackFig, 0, b.BlackFig.Length);") && risk.Contains("b.BlackRisk = 1000;") && risk.Contains("b.BlackWealth = BlackTerms.WealthDefault;"),
            "Black card: a bank wound up or started again has no tier left");
        Check(store.Contains("\"^x:\"") && store.Contains("case 'x':") && director.Contains("public void SetBlack(bool on)") && director.Contains("public void SetBlackLines(int wealth, int score, int fee, int reward)"),
            "Black card: the tier is saved in a part of its own, and the owner's choices go through the director, held to their steps");
        Check(reader.Contains("f.BlackWanted = b.CardsOn && b.BlackOn ? OwnBankRules.BlackWanted(b) : 0;") && app.Contains("WriteBlackTier(box, f);") && build.Contains("BlackTierCard(f, w, cards);"),
            "Black card: the Cards tab shows the tier in both interfaces, from the same facts");
        Check(model.Contains("BlackOn, BlackWealth, BlackScore, BlackFee, BlackReward") && live.Contains("case MoneyAct.BlackOn: director.SetBlack(value == 1); return true;") && live.Contains("case MoneyAct.BlackReward:"),
            "Black card: every choice of the kit's card reaches the director");
        Check(app.Contains("_director.SetBlackLines(i, f.BlackScore, f.BlackFee, f.BlackReward)") && app.Contains("_director.SetBlack(on)"), "Black card: every choice of the old interface's box reaches the director");
    }

    // ------------------------------------------------------------------------------------------------------------------ the texts

    private static void Life24Texts()
    {
        Dictionary<string, string> en = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(System.IO.File.ReadAllText(System.IO.Path.Combine(LocalesFolder(), "en.json")).TrimStart('﻿'));
        string[] keys =
        {
            "reallife_ob_black_tier", "reallife_ob_black_wealth_head", "reallife_ob_black_wealth_hint", "reallife_ob_black_score_head", "reallife_ob_black_score_hint", "reallife_ob_black_fee_head",
            "reallife_ob_black_fee_hint", "reallife_ob_black_reward_head", "reallife_ob_black_reward_hint", "reallife_ob_black_rich", "reallife_ob_black_scores", "reallife_ob_black_switch",
            "reallife_ob_black_needs_cards", "reallife_ob_black_note", "reallife_avail_option", "reallife_opt_tip_staff_availability", "reallife_avail_test", "reallife_avail_test_result", "reallife_avail_test_none",
            "reallife_avail_student", "reallife_avail_parent", "reallife_avail_early", "reallife_avail_weekend", "reallife_avail_words", "reallife_avail_weekdays", "reallife_avail_weekend_days", "reallife_avail_demand",
            "reallife_avail_tag", "reallife_avail_about", "reallife_avail_tip_off", "reallife_avail_sec", "reallife_avail_row_kind", "reallife_avail_row_days", "reallife_avail_row_hours",
            "reallife_ui_bizops_fit_window", "reallife_avail_note_hired", "reallife_avail_note_hired_many", "reallife_avail_note_breach", "reallife_avail_note_breach_many", "reallife_travel_rest",
            "reallife_opt_tip_holiday_rest", "reallife_happinessmodifier_travel_holiday", "reallife_travel_agent_option_family", "reallife_travel_trip_side_family"
        };
        bool all = true, clean = true, notices = true;
        foreach (string key in keys)
        {
            string text;
            if (!en.TryGetValue(key, out text) || string.IsNullOrWhiteSpace(text))
            {
                all = false;
                Console.WriteLine("  missing text: " + key);
                continue;
            }
            clean &= text.IndexOf('—') < 0 && text.IndexOf('–') < 0 && !text.Contains(" - ") && !text.Contains("!") && !text.Contains(";");
            notices &= !key.StartsWith("reallife_avail_note_", StringComparison.Ordinal) || text.Length <= 240;
        }
        Check(all, "part-timers, holidays and the Black card: every text is in en.json");
        Check(clean, "part-timers, holidays and the Black card: no text has a dash, a semicolon or an exclamation mark");
        Check(notices, "part-timers, holidays and the Black card: a notice is at most 240 characters");
        Check(L24Words(en["reallife_opt_tip_staff_availability"]) <= 32 && en["reallife_opt_tip_staff_availability"].Length <= 190 && L24Words(en["reallife_opt_tip_holiday_rest"]) <= 32 && en["reallife_opt_tip_holiday_rest"].Length <= 190
              && L24Words(en["reallife_opt_tip_travel_extras"]) <= 32 && en["reallife_opt_tip_travel_extras"].Length <= 190,
            "part-timers and holidays: an option's tip is at most 32 words and 190 characters");
        Check(en["reallife_avail_words"].Contains("{kind}") && en["reallife_avail_words"].Contains("{days}") && en["reallife_avail_words"].Contains("{from}") && en["reallife_avail_words"].Contains("{to}")
              && en["reallife_avail_note_hired"].Contains("{name}") && en["reallife_avail_note_hired"].Contains("{words}") && en["reallife_avail_note_breach"].Contains("{hours}")
              && en["reallife_travel_agent_option_family"].Contains("{names}") && en["reallife_travel_agent_option_family"].Contains("{count}") && en["reallife_avail_test_result"].Contains("{count}"),
            "part-timers and holidays: the texts carry the names the code fills in");
        Check(en["reallife_guide_economy_bank_risk"].Contains("<b>" + en["reallife_ob_black_tier"] + "</b>") && en["reallife_guide_economy_rota"].Contains("<b>" + en["reallife_avail_option"] + "</b>")
              && en["reallife_guide_travel"].Contains("<b>" + en["reallife_travel_rest"] + "</b>") && en["reallife_guide_travel"].Contains("<b>" + en["reallife_travel_extras"] + "</b>"),
            "part-timers, holidays and the Black card: each Guide page names the new part and its option as the options page does");
        Check(WhatsNewHas(en, "Part-timers can keep set hours") && WhatsNewHas(en, "A holiday can fill every need") && WhatsNewHas(en, "Your bank can offer a Black card"),
            "part-timers, holidays and the Black card: What's new tells of the three, as features and not as fixes");
        string row = en.TryGetValue("reallife_changes_next", out string next) ? next : string.Empty;
        Check(row.IndexOf("Fixed: Part-timers", StringComparison.Ordinal) < 0 && row.IndexOf("Fixed: A holiday", StringComparison.Ordinal) < 0 && row.IndexOf("Fixed: Your bank", StringComparison.Ordinal) < 0,
            "part-timers, holidays and the Black card: none of their lines starts with Fixed");
    }

    private static int L24Words(string text) => text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
}
