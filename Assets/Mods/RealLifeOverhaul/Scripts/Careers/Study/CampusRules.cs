using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The game's mark over a classroom door of the university (the icon the game puts over its own classroom doors):
    /// an action (a class or an exam of the room's course to begin now, or its next class within the hour), enrolled
    /// (on it in person, nothing soon), completed (passed), or none (a free room, a course not taken, one followed online).
    /// </summary>
    public enum RoomMark
    {
        None = 0,
        Enrolled = 1,
        Soon = 2,
        Completed = 3
    }

    /// <summary>
    /// Hudson City University's building as numbers (Careers/CampusBuilding and Careers/CampusInterior hold the game's side),
    /// game-free: EconomyCheck runs every rule.
    ///
    /// THE BUILDING. One of the city's office buildings that holds no business of the game's and is nobody's: no business
    /// type in it, not rented or owned by the player (in a Going Public session another player's business is on this
    /// machine as rented by the player, so it is left too), no rival's business. Of those that may serve, the largest, ties
    /// broken by address (SpecialNameBook.PickLargest). Every machine of a session sees the same businesses (Going Public
    /// sends the host's), so every machine picks the same building.
    ///
    /// INSIDE. The game's own Manhattan Business School: its building's interior and its furniture (the game's school is
    /// the one building of size D, version 1; its classrooms are five, each with a classroom door and a desk). The
    /// university has more courses than five, so it has floors: each floor is that same interior, its five classrooms
    /// holding five of the university's courses in the catalogue's order, so floor 1 room 1 is the first course. The number
    /// of floors is the courses divided by five, rounded up.
    ///
    /// HOURS. Open Monday to Friday 08:00 to 22:00 (every class, from half an hour before it until it ends, the evening
    /// classes of part-time study, and the exams), Saturday 09:00 to 14:00 (graduation day), shut on Sunday.
    /// </summary>
    public static class CampusRules
    {
        public const int RoomsPerFloor = 5;          // the game's school has five classrooms
        public const string NoBusiness = "ba:businesstype_empty";

        // Opening hours by weekday, Monday first; a day whose closing hour is not after its opening is shut.
        private static readonly int[] OpensAt = { 8, 8, 8, 8, 8, 9, 0 };
        private static readonly int[] ClosesAt = { 22, 22, 22, 22, 22, 14, 0 };

        /// <summary>The game's classroom doors in the order of the rooms: each door's custom value is one of the game's courses.</summary>
        public static readonly string[] RoomDoors = { "BasicHr", "Headquarters", "MarketDemands", "OfficeBusinesses", "ProductManufacturing" };

        private static int[] _courses;

        /// <summary>The university's courses held in person, in the catalogue's order: the rooms, floor by floor.</summary>
        public static int[] Courses
        {
            get
            {
                if (_courses == null)
                {
                    var list = new List<int>(24);
                    for (int i = 0; i < StudyCatalogue.All.Length; i++)
                    {
                        CourseDef c = StudyCatalogue.All[i];
                        if (c.Campus == Campus.University && c.Timetabled)
                        {
                            list.Add(c.Id);
                        }
                    }
                    _courses = list.ToArray();
                }
                return _courses;
            }
        }

        /// <summary>Floors for a number of courses: five to a floor, rounded up, one at least.</summary>
        public static int FloorsFor(int courses) => Math.Max(1, (courses + RoomsPerFloor - 1) / RoomsPerFloor);

        /// <summary>The university's floors.</summary>
        public static int Floors => FloorsFor(Courses.Length);

        /// <summary>Floor of a course, from 1; 0 for a course not held in the university's building.</summary>
        public static int FloorOf(int course)
        {
            int i = Array.IndexOf(Courses, course);
            return i < 0 ? 0 : i / RoomsPerFloor + 1;
        }

        /// <summary>Room of a course on its floor, from 1; 0 for none.</summary>
        public static int RoomOf(int course)
        {
            int i = Array.IndexOf(Courses, course);
            return i < 0 ? 0 : i % RoomsPerFloor + 1;
        }

        /// <summary>The course in a room of a floor (both from 1), or -1 for an empty room.</summary>
        public static int CourseAt(int floor, int room)
        {
            if (floor < 1 || room < 1 || room > RoomsPerFloor)
            {
                return -1;
            }
            int i = (floor - 1) * RoomsPerFloor + room - 1;
            return i < Courses.Length ? Courses[i] : -1;
        }

        /// <summary>The room a classroom door is, from its custom value (from 1); 0 for a door that is none of them.</summary>
        public static int RoomOfDoor(string customValue)
        {
            return customValue == null ? 0 : Array.IndexOf(RoomDoors, customValue) + 1;
        }

        /// <summary>The course behind a classroom door of a floor, from the door's custom value; -1 for a free room or a door that is none.</summary>
        public static int CourseOfDoor(int floor, string customValue)
        {
            int room = RoomOfDoor(customValue);
            return room > 0 ? CourseAt(floor, room) : -1;
        }

        /// <summary>
        /// Which way a room's sign faces, along the door's own forward (x, z): +1 forward, -1 back. Out of the room: away
        /// from the room's chair (the game's school puts each classroom's chair, named like its door, inside the room).
        /// With no chair the door's forward, which in the game's school layout is out of the room.
        /// </summary>
        public static int SignSide(float forwardX, float forwardZ, float doorX, float doorZ, float chairX, float chairZ, bool chair)
        {
            if (!chair)
            {
                return 1;
            }
            float along = forwardX * (doorX - chairX) + forwardZ * (doorZ - chairZ);
            return along < 0f ? -1 : 1;
        }

        // ---------------------------------------------------------------- the mark over a classroom door

        public const int SoonMinutes = 60;   // a class this close is marked as an action, as the game marks a course to study

        /// <summary>The game's mark over a classroom door, as the mod's course of the room has it (<see cref="RoomMark"/>).</summary>
        /// <param name="course">The room holds a course.</param>
        /// <param name="live">The player is on it now.</param>
        /// <param name="inPerson">...in person, in its classroom (not online from home).</param>
        /// <param name="held">The player has passed it.</param>
        /// <param name="canNow">Its class can be joined, or its exam sat, now.</param>
        /// <param name="minutesToNext">Minutes until its next class begins; negative for none.</param>
        public static RoomMark MarkOf(bool course, bool live, bool inPerson, bool held, bool canNow, int minutesToNext)
        {
            if (!course)
            {
                return RoomMark.None;
            }
            if (live)
            {
                if (!inPerson)
                {
                    return RoomMark.None;
                }
                return canNow || (minutesToNext >= 0 && minutesToNext <= SoonMinutes) ? RoomMark.Soon : RoomMark.Enrolled;
            }
            return held ? RoomMark.Completed : RoomMark.None;
        }

        // ---------------------------------------------------------------- the building

        /// <summary>True for a registration that holds no business and is nobody's: the university may take its building.</summary>
        public static bool NoBusinessIn(string businessType, bool rentedByPlayer, bool ownedByPlayer, string businessOwner)
        {
            return (string.IsNullOrEmpty(businessType) || businessType == NoBusiness) && !rentedByPlayer && !ownedByPlayer && string.IsNullOrEmpty(businessOwner);
        }

        // ---------------------------------------------------------------- the hours

        /// <summary>Weekday of a game day: 0 Monday to 6 Sunday (the game's day 1 is a Monday).</summary>
        public static int Weekday(int day) => day > 0 ? (day - 1) % 7 : 0;

        /// <summary>The hour the university opens on a game day, and closes; equal when it is shut all day.</summary>
        public static int Opens(int day) => OpensAt[Weekday(day)];

        public static int Closes(int day) => Math.Max(OpensAt[Weekday(day)], ClosesAt[Weekday(day)]);

        public static bool OpenDay(int day) => Closes(day) > Opens(day);

        /// <summary>True while the university is open: a minute of the day (0 to 1439) of a game day.</summary>
        public static bool OpenAt(int day, int minute)
        {
            return OpenDay(day) && minute >= Opens(day) * 60 && minute < Closes(day) * 60;
        }

        /// <summary>
        /// When it opens next, from a minute of a game day: the day and the hour (today's later opening, else the next day
        /// that opens). False only if no day of the week opens.
        /// </summary>
        public static bool NextOpening(int day, int minute, out int openDay, out int openHour)
        {
            for (int d = day; d < day + 8; d++)
            {
                if (!OpenDay(d) || (d == day && minute >= Opens(d) * 60))
                {
                    continue;
                }
                openDay = d;
                openHour = Opens(d);
                return true;
            }
            openDay = 0;
            openHour = 0;
            return false;
        }

        /// <summary>
        /// True if every class of a course, in every way it is followed in person, falls inside the opening hours from the
        /// moment it can be joined until it ends (the checker runs it over the whole catalogue).
        /// </summary>
        public static bool ClassesInside(CourseDef c)
        {
            for (int m = 0; m < 2; m++)
            {
                var mode = (StudyMode)m;
                int days = StudyRules.Days(c, mode), start = StudyRules.Hour(c, mode) * 60;
                for (int day = 1; day <= 7; day++)
                {
                    if (!StudyRules.On(days, day))
                    {
                        continue;
                    }
                    int from = start - StudyCatalogue.ArriveEarlyMinutes, to = start + c.Hours * 60;
                    if (!OpenAt(day, Math.Max(0, from)) || !OpenAt(day, to - 1))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
