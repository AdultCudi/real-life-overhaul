using System;
using RealLifeOverhaul;
using RealLifeOverhaul.Careers;

// Hudson City University's building, floors, rooms and hours (Scripts/Careers/Study/CampusRules), and the pick of its
// building by the rule every machine of a session follows (SpecialNameBook.PickLargest with CampusRules.NoBusinessIn).
internal static partial class Program
{
    private static void CampusRulesChecks()
    {
        // ---------------------------------------------------------------- floors and rooms
        int[] courses = CampusRules.Courses;
        Check(courses.Length == 17 && CampusRules.Floors == 4 && CampusRules.FloorsFor(17) == 4 && CampusRules.FloorsFor(20) == 4 && CampusRules.FloorsFor(21) == 5
              && CampusRules.FloorsFor(1) == 1 && CampusRules.FloorsFor(0) == 1,
            "campus: the university's 17 courses held in person on 4 floors, five to a floor, rounded up");
        bool all = true, once = true;
        var seen = new bool[StudyCatalogue.IdsMost];
        for (int i = 0; i < courses.Length; i++)
        {
            CourseDef c = StudyCatalogue.Find(courses[i]);
            int floor = CampusRules.FloorOf(c.Id), room = CampusRules.RoomOf(c.Id);
            all &= c.Campus == Campus.University && c.Timetabled && floor >= 1 && floor <= CampusRules.Floors && room >= 1 && room <= CampusRules.RoomsPerFloor
                   && CampusRules.CourseAt(floor, room) == c.Id;
            once &= !seen[c.Id];
            seen[c.Id] = true;
        }
        Check(all && once, "campus: every course of the university has one floor and one room, and that room holds it");
        Check(CampusRules.FloorOf(StudyCatalogue.ComputerScience) == 1 && CampusRules.RoomOf(StudyCatalogue.ComputerScience) == 1
              && CampusRules.FloorOf(StudyCatalogue.Pgce) == 4 && CampusRules.RoomOf(StudyCatalogue.Pgce) == 2,
            "campus: floor 1 room 1 is the first course (computer science), the last (the PGCE) is floor 4 room 2");
        Check(CampusRules.FloorOf(StudyCatalogue.Nursing) == 0 && CampusRules.FloorOf(StudyCatalogue.FirstAid) == 0 && CampusRules.FloorOf(StudyCatalogue.CloudAssociate) == 0,
            "campus: nursing (the wards), the school's courses and the certificates have no room at the university");
        Check(CampusRules.CourseAt(4, 3) == -1 && CampusRules.CourseAt(5, 1) == -1 && CampusRules.CourseAt(0, 1) == -1 && CampusRules.CourseAt(1, 6) == -1,
            "campus: the rooms past the last course, and floors and rooms that are none, are empty");
        Check(CampusRules.RoomDoors.Length == CampusRules.RoomsPerFloor && CampusRules.RoomOfDoor("BasicHr") == 1 && CampusRules.RoomOfDoor("ProductManufacturing") == 5
              && CampusRules.RoomOfDoor("FoodSafety") == 0 && CampusRules.RoomOfDoor(null) == 0,
            "campus: the school's five classroom doors are the five rooms, by their custom value");

        // ---------------------------------------------------------------- the hours
        // Day 1 is a Monday, day 5 a Friday, day 6 a Saturday, day 7 a Sunday.
        Check(CampusRules.Weekday(1) == 0 && CampusRules.Weekday(7) == 6 && CampusRules.Weekday(8) == 0, "campus: the game's day 1 is a Monday");
        Check(!CampusRules.OpenAt(5, 7 * 60 + 59) && CampusRules.OpenAt(5, 8 * 60) && CampusRules.OpenAt(5, 21 * 60 + 59) && !CampusRules.OpenAt(5, 22 * 60),
            "campus: Monday to Friday open from 08:00 until 22:00");
        Check(!CampusRules.OpenAt(6, 8 * 60 + 35) && CampusRules.OpenAt(6, 9 * 60) && !CampusRules.OpenAt(6, 14 * 60) && !CampusRules.OpenAt(7, 12 * 60) && !CampusRules.OpenDay(7),
            "campus: Saturday 09:00 to 14:00, Sunday shut");
        Check(CampusRules.NextOpening(5, 7 * 60, out int d1, out int h1) && d1 == 5 && h1 == 8
              && CampusRules.NextOpening(5, 22 * 60, out int d2, out int h2) && d2 == 6 && h2 == 9
              && CampusRules.NextOpening(6, 15 * 60, out int d3, out int h3) && d3 == 8 && h3 == 8
              && CampusRules.NextOpening(7, 0, out int d4, out int h4) && d4 == 8 && h4 == 8,
            "campus: closed, it opens today at 08:00 before eight, on Saturday at 09:00 after a Friday evening, on Monday after a Saturday or a Sunday");
        bool inside = true;
        for (int i = 0; i < StudyCatalogue.All.Length; i++)
        {
            CourseDef c = StudyCatalogue.All[i];
            if (c.Campus == Campus.University && c.Timetabled && !CampusRules.ClassesInside(c))
            {
                inside = false;
                Console.WriteLine("  outside the hours: " + c.Key);
            }
        }
        Check(inside, "campus: every class of the university, full time and part time, from half an hour before it until it ends, is inside its hours");
        bool exams = true;
        for (int day = 1; day <= 5; day++)
        {
            exams &= CampusRules.OpenAt(day, StudyCatalogue.ExamOpens * 60) && CampusRules.OpenAt(day, StudyCatalogue.ExamCloses * 60 - 1);
        }
        Check(exams, "campus: the exam hours of every weekday are inside the university's hours");
        int ceremony = StudyRules.CeremonyDay(3);
        Check(CampusRules.Weekday(ceremony) == 5 && CampusRules.OpenAt(ceremony, CareersCeremonyHour * 60 - 30) && CampusRules.OpenAt(ceremony, CareersCeremonyHour * 60 + 119),
            "campus: graduation day (a Saturday, from half an hour before the ceremony to its end) is inside its hours");

        // ---------------------------------------------------------------- the building
        Check(CampusRules.NoBusinessIn("ba:businesstype_empty", false, false, "") && CampusRules.NoBusinessIn(null, false, false, null)
              && !CampusRules.NoBusinessIn("ba:businesstype_lawfirm", false, false, "rival_duane") && !CampusRules.NoBusinessIn("ba:businesstype_empty", true, false, "")
              && !CampusRules.NoBusinessIn("ba:businesstype_empty", false, true, "") && !CampusRules.NoBusinessIn("ba:businesstype_empty", false, false, "player_2"),
            "campus: a building may serve only with no business in it and nobody's (the player's, another player's in a session, a rival's business)");

        // A city where the largest office (17 7th Street) is the utilities office; the next (12 Broadway) has a business.
        string[] street = { "ba:street_seventhstreet", "ba:street_broadwaystreet", "ba:street_broadwaystreet", "ba:street_secondstreet", "ba:street_firststreet" };
        int[] number = { 17, 12, 1, 54, 78 };
        int[] sqm = { 102502, 58989, 48567, 7195, 7195 };
        string[] type = { "ba:businesstype_empty", "ba:businesstype_lawfirm", "ba:businesstype_empty", "ba:businesstype_empty", "ba:businesstype_empty" };
        string[] owner = { "", "rival_duane", "", "", "" };
        Func<int, bool> free = i => CampusRules.NoBusinessIn(type[i], false, false, owner[i]);
        Func<string, int, bool> held = (s, n) => s == "ba:street_seventhstreet" && n == 17;
        int pick = SpecialNameBook.PickLargest(5, free, i => sqm[i], i => street[i], i => number[i], held);
        Check(pick == 2, "campus: past the utilities office and the office with a business in it, to the largest empty office (1 Broadway)");
        int again = SpecialNameBook.PickLargest(5, free, i => sqm[i], i => street[i], i => number[i], held);
        Check(again == pick, "campus: the same building on every machine and every load");
        type[2] = "ba:businesstype_travelagency";
        int moved = SpecialNameBook.PickLargest(5, free, i => sqm[i], i => street[i], i => number[i], held);
        Check(moved == 4, "campus: a business opens there: the next largest empty office, ties broken by address (1st Street before 2nd Street)");
    }

    private const int CareersCeremonyHour = 11;   // CareersDirector.CeremonyHour (the director is not compiled here)
}
