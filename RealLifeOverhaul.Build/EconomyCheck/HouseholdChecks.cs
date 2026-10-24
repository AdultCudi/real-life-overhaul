using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Household;

// Household staff (Scripts/Home/Household): the four kinds and their hours, wages from the city's going rate, the cook's
// dishes from home cooking's own table, the agency's candidates, who is shown at home, unpaid visits and leaving, the
// hours a late look makes up, the save's line; then, over the sources, that money moves only through the game's own call,
// that the Game file is the only one touching the game's cars and the floor, and the texts in every language.
internal static partial class Program
{
    private static void Household()
    {
        HouseholdRoleChecks();
        HouseholdWageChecks();
        HouseholdCookChecks();
        HouseholdCandidateChecks();
        HouseholdVisitChecks();
        HouseholdStoreChecks();
        HouseholdSourceChecks();
        HouseholdTextChecks();
    }

    private static void HouseholdRoleChecks()
    {
        Check(HouseholdRules.Roles.Length == 4 && HouseholdRules.Most == 4, "household: four kinds, one of each on the staff at most");
        var seen = new HashSet<StaffKind>();
        foreach (StaffRole r in HouseholdRules.Roles)
        {
            Check(seen.Add(r.Kind) && r.Kind != StaffKind.None, "household: " + r.Kind + " is listed once");
            Check(r.Hours > 0 && r.From >= 0 && r.From + r.Hours <= 23, "household: " + r.Kind + "'s visit is inside one day: " + r.From + " for " + r.Hours + " h");
            Check(r.From + r.Hours < HouseholdRules.NoteHour || r.Kind == StaffKind.Mechanic && r.From < HouseholdRules.NoteHour,
                "household: " + r.Kind + "'s visit is over before the day's note at " + HouseholdRules.NoteHour);
            Check(HouseholdRules.Role(r.Kind) == r, "household: the role of " + r.Kind + " is found by its kind");
            Check(r.Title.StartsWith("reallife_household_kind_") && r.When.StartsWith("reallife_household_when_") && r.Does.StartsWith("reallife_household_does_"),
                "household: " + r.Kind + "'s words are its own keys");
        }
        Check(HouseholdRules.Role(StaffKind.None) == null && HouseholdRules.Role((StaffKind)9) == null, "household: no role for none or an unknown kind");
        StaffRole cleaner = HouseholdRules.Role(StaffKind.Cleaner), cook = HouseholdRules.Role(StaffKind.Cook), butler = HouseholdRules.Role(StaffKind.Butler),
            mechanic = HouseholdRules.Role(StaffKind.Mechanic);
        Check(HouseholdRules.VisitsAWeek(cleaner) == 3 && HouseholdRules.Works(cleaner, 0) && !HouseholdRules.Works(cleaner, 1) && HouseholdRules.Works(cleaner, 2)
              && HouseholdRules.Works(cleaner, 4) && !HouseholdRules.Works(cleaner, 5), "household: the cleaner comes on Monday, Wednesday and Friday");
        Check(HouseholdRules.VisitsAWeek(cook) == 7 && HouseholdRules.VisitsAWeek(butler) == 7, "household: the cook and the butler every day");
        Check(HouseholdRules.VisitsAWeek(mechanic) == 1 && HouseholdRules.Works(mechanic, 5), "household: the mechanic on Saturday only");
        Check(cleaner.Cleans && !cleaner.Cooks && cook.Cooks && !cook.Cleans && butler.Cleans && butler.Cooks && butler.Washes && butler.LiveIn
              && mechanic.Washes && mechanic.Services && !mechanic.Cleans, "household: what each kind does");
        Check(HouseholdRules.Starts(cleaner, 0, 10) && !HouseholdRules.Starts(cleaner, 0, 11) && !HouseholdRules.Starts(cleaner, 1, 10), "household: a visit starts at its hour on its days only");
        Check(HouseholdRules.Ends(cleaner, 0, 13) && !HouseholdRules.Ends(cleaner, 0, 12), "household: and ends its hours later");
        Check(HouseholdRules.AtWork(cleaner, 0, 10) && HouseholdRules.AtWork(cleaner, 0, 12) && !HouseholdRules.AtWork(cleaner, 0, 13) && !HouseholdRules.AtWork(cleaner, 0, 9),
            "household: at work from the hour they come to the hour before they go");
        Check(!HouseholdRules.Works(cleaner, -1) && !HouseholdRules.Works(cleaner, 7) && !HouseholdRules.Works(null, 0), "household: no weekday out of range, no role, no work");
    }

    private static void HouseholdWageChecks()
    {
        StaffRole cleaner = HouseholdRules.Role(StaffKind.Cleaner), cook = HouseholdRules.Role(StaffKind.Cook), butler = HouseholdRules.Role(StaffKind.Butler),
            mechanic = HouseholdRules.Role(StaffKind.Mechanic);
        Check(HouseholdRules.Going(0, 0) == HouseholdRules.FallbackCents, "household: with no formula and no market, the usual going rate");
        Check(HouseholdRules.Going(1000, 1500) == 1500 && HouseholdRules.Going(1200, 1000) == 1200, "household: the going rate is the game's wage times the city's market");
        Check(HouseholdRules.Going(int.MaxValue, 4000) == int.MaxValue, "household: the going rate never overflows");
        int going = 1000;
        int a = HouseholdRules.Hourly(going, cleaner, 50), b = HouseholdRules.Hourly(going, cook, 50), c = HouseholdRules.Hourly(going, butler, 50);
        Check(a > 0 && b > a && c > b, "household: a cook asks more than a cleaner, a butler more than a cook: " + a + ", " + b + ", " + c);
        Check(a % 5 == 0 && b % 5 == 0 && c % 5 == 0, "household: an hourly wage is in whole five cents");
        Check(HouseholdRules.Hourly(going, cleaner, 95) > HouseholdRules.Hourly(going, cleaner, 40), "household: more skill asks a little more");
        Check(HouseholdRules.Hourly(going, cleaner, 95) <= going * 120 / 100 && HouseholdRules.Hourly(going, cleaner, 40) >= going * 90 / 100,
            "household: a cleaner asks within a tenth or a fifth of the going rate");
        Check(HouseholdRules.Hourly(going, mechanic, 50) == 0 && HouseholdRules.Hourly(0, cleaner, 50) == 0 && HouseholdRules.Hourly(going, null, 50) == 0,
            "household: the mechanic is paid by the job; no going rate or role, no wage");
        Check(HouseholdRules.VisitWage(1140, cleaner) == 34 && HouseholdRules.VisitWage(1, cleaner) == 1 && HouseholdRules.VisitWage(0, cleaner) == 0,
            "household: a visit's wage is its hours at the rate, at least one");
        Check(HouseholdRules.WeekWage(1140, cleaner) == 3 * 34 && HouseholdRules.WeekWage(1000, cook) == 7 * HouseholdRules.VisitWage(1000, cook), "household: a week's wages");
        Check(HouseholdRules.Groceries(3, 1000) == 18 && HouseholdRules.Groceries(3, 2000) == 36 && HouseholdRules.Groceries(0, 1000) == 0, "household: the shopping follows prices");
        Check(HouseholdRules.CallOutAt(1000) == HouseholdRules.CallOut && HouseholdRules.CallOutAt(1500) == 143, "household: the call-out follows prices");
        Check(HouseholdRules.CallOut == 95 && HouseholdRules.WashFee == 50, "household: the call-out is car ownership's roadside one and the wash the game's own");
        Check(HouseholdRules.CleanPoints(1) >= 60f && HouseholdRules.CleanPoints(100) <= 100f && HouseholdRules.CleanPoints(80) > HouseholdRules.CleanPoints(40),
            "household: a cleaning puts back 60 to 100 points, more for more skill");
    }

    private static void HouseholdCookChecks()
    {
        foreach (int number in HouseholdRules.CookDishes)
        {
            RealLifeOverhaul.Dish dish = null;
            foreach (RealLifeOverhaul.Dish d in RealLifeOverhaul.HomeBalance.Dishes)
            {
                dish = d.Number == number ? d : dish;
            }
            Check(dish != null && dish.Cooker && dish.Portions >= 2, "household: the cook's dish " + number + " is home cooking's, made at a cooker, two portions or more");
        }
        for (int i = 0; i < HouseholdRules.CookDishes.Length; i++)
        {
            Check(HouseholdRules.PortionsOf(i) >= 2 && HouseholdRules.DishAt(i) != null && HouseholdRules.DishAt(i).Number == HouseholdRules.CookDishes[i],
                "household: dish " + i + " is read from home cooking's table");
        }
        Check(HouseholdRules.DishAt(-1) == null && HouseholdRules.DishAt(99) == null && HouseholdRules.PortionsOf(-1) == 0, "household: no dish out of range");
        Check(HouseholdRules.LeftoversMost == RealLifeOverhaul.HomeBalance.LeftoversMost, "household: the fridge holds what home cooking's leftovers hold");
        Check(HouseholdRules.PortionsIn(4, 0) == 3 && HouseholdRules.PortionsIn(4, 6) == 2 && HouseholdRules.PortionsIn(4, 8) == 0 && HouseholdRules.PortionsIn(4, 12) == 0,
            "household: portions go in as far as the fridge has room");
        bool inRange = true, neverTwice = true, same = true;
        for (int day = 1; day <= 400; day++)
        {
            int d = HouseholdRules.DishOn(77, day, StaffKind.Cook);
            inRange &= d >= 0 && d < HouseholdRules.CookDishes.Length;
            neverTwice &= d != HouseholdRules.DishOn(77, day - 1, StaffKind.Cook) || day == 1 && d != HouseholdRules.DishOn(77, 0, StaffKind.Cook);
            same &= d == HouseholdRules.DishOn(77, day, StaffKind.Cook);
        }
        Check(inRange && same, "household: the day's dish is one of the cook's, the same every time for the same day");
        Check(neverTwice, "household: never the same dish two days running");
    }

    private static void HouseholdCandidateChecks()
    {
        StaffCandidate a = HouseholdRules.Candidate(12345, StaffKind.Cleaner, 0), b = HouseholdRules.Candidate(12345, StaffKind.Cleaner, 0);
        Check(a.Name == b.Name && a.Seed == b.Seed && a.Age == b.Age && a.Skill == b.Skill && a.Female == b.Female, "household: the same candidate for the same seed, kind and number");
        int different = 0, bounds = 0;
        var names = new HashSet<string>();
        for (int n = 0; n < 200; n++)
        {
            foreach (StaffRole r in HouseholdRules.Roles)
            {
                StaffCandidate c = HouseholdRules.Candidate(999, r.Kind, n);
                bounds += c.Kind == r.Kind && !string.IsNullOrEmpty(c.Name) && c.Name.IndexOf(' ') > 0 && c.Age >= HouseholdRules.AgeLeast && c.Age < HouseholdRules.AgeMost
                          && c.Skill >= HouseholdRules.SkillLeast && c.Skill <= HouseholdRules.SkillMost && c.Seed > 0 ? 1 : 0;
                names.Add(c.Name);
            }
            different += HouseholdRules.Candidate(999, StaffKind.Cook, n).Name != HouseholdRules.Candidate(999, StaffKind.Cook, n + 1).Name ? 1 : 0;
        }
        Check(bounds == 800, "household: every candidate has a name, an age and a skill within bounds and a look: " + bounds + " of 800");
        Check(different >= 180 && names.Count >= 150, "household: someone else is somebody else: " + different + " of 200 next ones differ, " + names.Count + " names");
        Check(HouseholdRules.Candidate(1, StaffKind.Cleaner, 0).Name != HouseholdRules.Candidate(1, StaffKind.Butler, 0).Name
              || HouseholdRules.Candidate(1, StaffKind.Cleaner, 0).Seed != HouseholdRules.Candidate(1, StaffKind.Butler, 0).Seed, "household: each kind has candidates of its own");

        var staff = new List<StaffMember>();
        Check(HouseholdRules.NextOpen(StaffKind.None, staff) == StaffKind.Cleaner && HouseholdRules.NextOpen(StaffKind.Cleaner, staff) == StaffKind.Cook
              && HouseholdRules.NextOpen(StaffKind.Mechanic, staff) == StaffKind.Cleaner, "household: the hiring page goes round the kinds");
        staff.Add(new StaffMember { Kind = StaffKind.Cook });
        Check(HouseholdRules.NextOpen(StaffKind.Cleaner, staff) == StaffKind.Butler, "household: a kind on the staff is skipped");
        staff.Add(new StaffMember { Kind = StaffKind.Cleaner });
        staff.Add(new StaffMember { Kind = StaffKind.Butler });
        Check(HouseholdRules.NextOpen(StaffKind.Mechanic, staff) == StaffKind.Mechanic, "household: with one kind left, that one again");
        staff.Add(new StaffMember { Kind = StaffKind.Mechanic });
        Check(HouseholdRules.NextOpen(StaffKind.None, staff) == StaffKind.None, "household: none when every kind is on the staff");
    }

    private static void HouseholdVisitChecks()
    {
        var cleaner = new StaffMember { Kind = StaffKind.Cleaner, OnDay = 5 };
        var cook = new StaffMember { Kind = StaffKind.Cook, OnDay = 5 };
        var butler = new StaffMember { Kind = StaffKind.Butler, OnDay = 5 };
        var mechanic = new StaffMember { Kind = StaffKind.Mechanic, OnDay = 5 };
        var staff = new List<StaffMember> { cleaner, cook, mechanic };
        Check(HouseholdRules.OnShow(staff, 5, 0, 11) == cleaner, "household: the cleaner is shown during the cleaning");
        Check(HouseholdRules.OnShow(staff, 5, 0, 17) == cook, "household: the cook during the cooking");
        Check(HouseholdRules.OnShow(staff, 5, 0, 14) == null && HouseholdRules.OnShow(staff, 6, 0, 11) == null, "household: nobody between visits, or on a day not paid for");
        Check(HouseholdRules.OnShow(staff, 5, 5, 10) == null, "household: the mechanic is never shown: they work at the cars outside");
        staff.Add(butler);
        Check(HouseholdRules.OnShow(staff, 5, 0, 11) == butler && HouseholdRules.OnShow(staff, 5, 0, 17) == butler, "household: the butler before the others");
        Check(HouseholdRules.OnShow(null, 5, 0, 11) == null, "household: no staff, nobody shown");

        var m = new StaffMember { Kind = StaffKind.Cleaner };
        HouseholdRules.Paid(m, 3, 34);
        Check(m.Visits == 1 && m.PaidTotal == 34 && m.Charged == 34 && m.OnDay == 3 && m.Missed == 0, "household: a paid visit is counted");
        Check(!HouseholdRules.Unpaid(m) && m.Missed == 1 && m.OnDay == -1, "household: one unpaid visit, and they stay");
        HouseholdRules.Paid(m, 4, 34);
        Check(m.Missed == 0 && m.Visits == 2 && m.PaidTotal == 68, "household: a paid visit ends a run of unpaid ones");
        Check(!HouseholdRules.Unpaid(m) && HouseholdRules.Unpaid(m) && m.Missed == HouseholdRules.QuitAfter, "household: two unpaid visits in a row and they leave");
        Check(!HouseholdRules.Unpaid(null), "household: nobody, nothing");

        Check(HouseholdRules.FirstToRun(0, 500) == 501 && HouseholdRules.FirstToRun(600, 500) == 501, "household: a first look, or a clock gone back, makes nothing up");
        Check(HouseholdRules.FirstToRun(499, 500) == 500 && HouseholdRules.FirstToRun(490, 500) == 491, "household: the hours since the last look");
        Check(HouseholdRules.FirstToRun(100, 500) == 500 - HouseholdRules.CatchUpHours + 1, "household: three days at most are made up");

        var state = new HouseholdState();
        state.AddNote(7, "One.");
        state.AddNote(7, "Two.");
        Check(state.TakeNote(7) == "One. Two." && state.TakeNote(7) == null, "household: the day's note holds the day's lines, once");
        state.AddNote(7, "Old.");
        state.AddNote(8, "New.");
        Check(state.TakeNote(7) == null && state.Note == null, "household: a new day begins a new note");
        state.AddNote(9, null);
        Check(state.Note == null, "household: an empty line adds nothing");
    }

    private static void HouseholdStoreChecks()
    {
        var s = new HouseholdState { Seed = 4242, LastStamp = 1234, HomeStreet = "ba:street_5th|av;e^", HomeNumber = 17, NoteDay = 51, Note = "Ada cleaned the home (34). Ben | cooked; ^" };
        s.Rolls[1] = 3;
        s.Rolls[4] = 1;
        s.Staff.Add(new StaffMember
        {
            Kind = StaffKind.Cleaner, Name = "Ada Bianchi|;^", Female = true, Age = 34, Seed = 99, Skill = 71, Hourly = 1140, HiredDay = 40, Visits = 5, Missed = 1,
            PaidTotal = 170, OnDay = 51, Dish = -1, Charged = 34
        });
        s.Staff.Add(new StaffMember { Kind = StaffKind.Cook, Name = "Ben Okafor", Age = 50, Seed = 7, Skill = 60, Hourly = 1400, HiredDay = 41, OnDay = -1, Dish = 3, Charged = 74 });
        string line = HouseholdStore.Write(s);
        HouseholdState r = HouseholdStore.Read(line, out int version);
        Check(r != null && version == HouseholdStore.Version, "household store: the line is read back");
        if (r != null)
        {
            Check(r.Seed == 4242 && r.LastStamp == 1234 && r.HomeStreet == s.HomeStreet && r.HomeNumber == 17 && r.NoteDay == 51 && r.Note == s.Note,
                "household store: the header and the note round trip, escaped");
            Check(r.Rolls[1] == 3 && r.Rolls[2] == 0 && r.Rolls[4] == 1, "household store: the candidates put forward by kind");
            Check(r.Staff.Count == 2, "household store: both people");
            StaffMember a = r.Staff.Count > 0 ? r.Staff[0] : null;
            Check(a != null && a.Kind == StaffKind.Cleaner && a.Name == "Ada Bianchi|;^" && a.Female && a.Age == 34 && a.Seed == 99 && a.Skill == 71 && a.Hourly == 1140
                  && a.HiredDay == 40 && a.Visits == 5 && a.Missed == 1 && a.PaidTotal == 170 && a.OnDay == 51 && a.Dish == -1 && a.Charged == 34,
                "household store: every field of a person round trips");
            StaffMember b = r.Staff.Count > 1 ? r.Staff[1] : null;
            Check(b != null && b.Kind == StaffKind.Cook && b.OnDay == -1 && b.Dish == 3 && b.Charged == 74 && !b.Female, "household store: the second person too");
            Check(HouseholdStore.Write(r) == line, "household store: written again, the same line");
        }
        Check(HouseholdStore.Read(null, out _) == null && HouseholdStore.Read("x", out _) == null && HouseholdStore.Read("v=0^h:1", out _) == null
              && HouseholdStore.Read("v=1^s:1|A|0|30|1|50|1000|1|0|0|0|-1|-1", out _) == null, "household store: a line that is not this feature's, or has no header, is not read");
        HouseholdState older = HouseholdStore.Read("v=1^h:5|10|3|ba%3Astreet|0|0|0|0|0^s:2|Cy%20Dee|0|30|8|50|1200|2|1|0|12|-1|-1", out _);
        Check(older != null && older.Staff.Count == 1 && older.Staff[0].Charged == 0 && older.Staff[0].Name == "Cy Dee", "household store: a person written without the last field still reads");
        HouseholdState newer = HouseholdStore.Read("v=2^h:5|10|3|ba%3Astreet|0|0|0|0|0|77^s:2|Cy|0|30|8|50|1200|2|1|0|12|-1|-1|5|99^z:later", out int v2);
        Check(newer != null && v2 == 2 && newer.Staff.Count == 1 && newer.Staff[0].Charged == 5, "household store: a newer line's fields at the end and new parts are passed over");
        HouseholdState homeless = HouseholdStore.Read("v=1^h:5|10|0||0|0|0|0|0^s:1|Ada|1|30|8|50|1200|2|1|0|12|-1|-1", out _);
        Check(homeless != null && homeless.Staff.Count == 0 && homeless.HomeStreet == null, "household store: people without a home to work at are nobody on the staff");
        HouseholdState twice = HouseholdStore.Read("v=1^h:5|10|3|st|0|0|0|0|0^s:1|Ada|1|30|8|50|1200|2|1|0|12|-1|-1;1|Bea|1|30|8|50|1200|2|1|0|12|-1|-1;9|Zed|1|30|8|50|1200|2|1|0|12|-1|-1", out _);
        Check(twice != null && twice.Staff.Count == 1 && twice.Staff[0].Name == "Ada", "household store: one of a kind, and no unknown kind");
        HouseholdState wild = HouseholdStore.Read("v=1^h:5|10|3|st|0|0|0|0|0^s:1|Ada|1|300|0|500|1200|2|1|9|12|-9|40", out _);
        Check(wild != null && wild.Staff[0].Age == 99 && wild.Staff[0].Skill == 100 && wild.Staff[0].Missed == HouseholdRules.QuitAfter && wild.Staff[0].OnDay == -1
              && wild.Staff[0].Dish == HouseholdRules.CookDishes.Length - 1 && wild.Staff[0].Seed == 1, "household store: every figure inside its bounds");

        var watch = Stopwatch.StartNew();
        int sum = 0;
        for (int i = 0; i < 20000; i++)
        {
            sum += HouseholdStore.Read(line, out _).Staff.Count + HouseholdRules.Candidate(i, StaffKind.Butler, i).Age;
        }
        watch.Stop();
        Check(sum > 0 && watch.Elapsed.TotalMilliseconds < 400.0, "household store: 20,000 reads and candidates in under 400 ms: " + watch.Elapsed.TotalMilliseconds.ToString("0.0") + " ms");
    }

    private static string HouseholdSource(string name)
    {
        return ModSource(@"Home\Household\" + name);
    }

    private static void HouseholdSourceChecks()
    {
        string[] names =
        {
            "HouseholdAtHome.cs", "HouseholdBridges.cs", "HouseholdDirector.cs", "HouseholdDirector.Tests.cs", "HouseholdGame.cs", "HouseholdGuide.cs", "HouseholdPhone.cs",
            "HouseholdRules.cs", "HouseholdState.cs", "HouseholdStore.cs", "HouseholdText.cs"
        };
        var code = new Dictionary<string, string>();
        foreach (string name in names)
        {
            code[name] = HouseholdSource(name);
            Check(code[name].Length > 0, "household sources: " + name + " is there");
        }
        foreach (KeyValuePair<string, string> kv in code)
        {
            Check(!kv.Value.Contains("ChangeMoney"), "household sources: " + kv.Key + " moves no money but through the game's own call in HouseholdGame.Pay");
            if (kv.Key != "HouseholdGame.cs")
            {
                Check(!Regex.IsMatch(kv.Value, @"\.(dirtiness|damage|deformations|dirtSpots)\b|SetDirtiness|\.Repair\(\)|HomeState\.PutBy|HomeTidy\.Change"),
                    "household sources: " + kv.Key + " leaves the cars, the floor and the fridge to HouseholdGame");
            }
            Check(!Regex.IsMatch(kv.Value, @"\bMP(Server|Client|Link)\b|SendEnvelope"), "household sources: " + kv.Key + " sends nothing over Going Public's network");
        }
        string game = code["HouseholdGame.cs"];
        Check(game.Contains("RoommateGame.Pay(") && game.Contains("VehicleHelper.GetVehicleController(") && game.Contains("HouseholdBridges.NotOurs"),
            "household sources: money through the game's call, a spawned car through the game's own list, other people's cars left alone");
        Check(game.Contains("save.VehicleInstances"), "household sources: only the save's own cars, never another player's");
        string director = code["HouseholdDirector.cs"];
        Check(director.Contains("_state.LastStamp = HouseholdGame.Stamp;") && director.Contains("SaveHook.Keep(_onSave)"),
            "household sources: a load makes nothing up; the state is written on the game's save");
        Check(Regex.IsMatch(director, @"if \(!HouseholdGame\.Pay\(total, TransactionKey\(role\.Kind\)\)\)") && director.Contains("if (!HouseholdGame.Pay(total, TransactionKey(StaffKind.Mechanic)))"),
            "household sources: a visit and the mechanic's round are paid before any work is done");
        string garage = ModSource(@"Transport\CarOwnership.Garage.cs");
        Check(garage.Contains("public static float ServiceWhereItStands(") && Regex.Matches(garage, @"\bDone\(record, kind\);").Count == 2
              && Regex.Matches(garage, @"\bDue\(").Count >= 3, "household sources: the bay and the mechanic share one rule of what is due and what a service does");
        string wiring = ModSource(@"Wiring\Wiring.HouseholdLinks.cs");
        Check(wiring.Contains("CarOwnership.ServiceWhereItStands(car, doIt)") && wiring.Contains("Dealerships.Stock(car)") && wiring.Contains("Family.PartnerCar.Is(car)")
              && wiring.Contains("StaffRules.Market("), "household sources: the wiring fills every bridge");
        string tidy = ModSource(@"Home\HomeTidy.cs");
        Check(tidy.Contains("public static void Change(BuildingRegistration home, float points)") && tidy.Contains("Change(Home(), points);"),
            "household sources: home life tidies the home the staff work at by the same rule as its own");
    }

    private static void HouseholdTextChecks()
    {
        string text = HouseholdSource("HouseholdText.cs");
        var keys = new List<string>();
        foreach (Match m in Regex.Matches(text, "\"(reallife[-_][a-z0-9_]+)\""))
        {
            keys.Add(m.Groups[1].Value);
        }
        foreach (string file in new[] { "HouseholdDirector.cs", "HouseholdDirector.Tests.cs", "HouseholdPhone.cs", "HouseholdGuide.cs", "HouseholdRules.cs" })
        {
            foreach (Match m in Regex.Matches(HouseholdSource(file), "\"(reallife[-_][a-z0-9_]+)\""))
            {
                string key = m.Groups[1].Value;
                if (key != "reallife_household_calldialogtype" && !key.StartsWith("reallife:"))
                {
                    keys.Add(key);
                }
            }
        }
        Check(keys.Count >= 80, "household texts: its keys are found: " + keys.Count);
        string[] facts =
        {
            "{hh_cleaner_from}", "{hh_cleaner_to}", "{hh_cook_from}", "{hh_cook_to}", "{hh_butler_from}", "{hh_butler_to}", "{hh_mechanic_at}", "{hh_cook_share}",
            "{hh_butler_share}", "{hh_callout}", "{hh_wash}", "{hh_shopping}", "{hh_portions}", "{hh_reach}", "{hh_quit}", "{hh_note}"
        };
        string guide = HouseholdSource("HouseholdGuide.cs");
        foreach (string fact in facts)
        {
            Check(guide.Contains(".Fact(\"" + fact.Trim('{', '}') + "\""), "household texts: the Guide's " + fact + " is filled from the rules");
        }
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            Check(words != null, "household texts: " + code + ".json is read");
            if (words == null)
            {
                continue;
            }
            foreach (string key in keys)
            {
                Check(words.TryGetValue(key, out string value) && value.Length > 0, "household texts: " + key + " is in " + code + ".json");
                if (words.TryGetValue(key, out string v))
                {
                    Check(!v.Contains('!') && !v.Contains((char)0x2014) && !v.Contains((char)0x2013) && !v.Contains(" - "), "household texts: " + key + " has no exclamation mark or dash in " + code);
                }
            }
            foreach (string key in HouseholdNotices(text))
            {
                Check(words.TryGetValue(key, out string value) && value.Length <= 240, "household texts: the notice " + key + " is at most 240 characters in " + code);
            }
            if (words.TryGetValue("reallife_guide_household", out string page))
            {
                foreach (string fact in facts)
                {
                    Check(page.Contains(fact), "household texts: the Guide page has " + fact + " in " + code);
                }
            }
            foreach (StaffRole r in HouseholdRules.Roles)
            {
                if (words.TryGetValue(r.When, out string when))
                {
                    Check(when.Contains("{from}") && (r.Kind == StaffKind.Mechanic || when.Contains("{to}")), "household texts: " + r.When + " names its hours in " + code);
                }
            }
        }
        // What's new: the feature's line in a row of every language, wherever the release has moved it (never the next row alone).
        foreach (string code in new[] { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr" })
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            if (words == null)
            {
                continue;
            }
            string start = code == "en" ? "Household staff: ring" : code == "pt" ? "Funcionários da casa: ligue" : code == "zh-cn" ? "家政人员：打给" : code == "de" ? "Hauspersonal: Ruf"
                : code == "fr" ? "Personnel de maison : appelez" : code == "es" ? "Personal doméstico: llama" : code == "ru" ? "Домашний персонал: позвоните"
                : code == "nl" ? "Huishoudelijk personeel: bel" : "Ev personeli: yeni";
            bool found = false;
            foreach (KeyValuePair<string, string> kv in words)
            {
                found |= kv.Key.StartsWith("reallife_changes_") && !kv.Key.EndsWith("_title") && kv.Value.Contains(start);
            }
            Check(found, "household texts: What's new has the feature's line in " + code);
        }
    }

    private static IEnumerable<string> HouseholdNotices(string text)
    {
        int at = text.IndexOf("Notices =", StringComparison.Ordinal);
        int end = at >= 0 ? text.IndexOf("};", at, StringComparison.Ordinal) : -1;
        var list = new List<string> { "reallife_household_arrived", "reallife_household_note_unpaid", "reallife_household_note_quit", "reallife_household_moved", "reallife_household_no_home_left" };
        if (at >= 0 && end > at)
        {
            foreach (Match m in Regex.Matches(text.Substring(at, end - at), "\"(reallife_[a-z0-9_]+)\""))
            {
                list.Add(m.Groups[1].Value);
            }
        }
        return list;
    }
}
