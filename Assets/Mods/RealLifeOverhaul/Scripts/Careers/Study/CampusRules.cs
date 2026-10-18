using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Careers
{
    /// <summary>The game's mark over a university classroom door: a class or exam to begin within the hour, enrolled in person, passed, or none.</summary>
    public enum RoomMark
    {
        None = 0,
        Enrolled = 1,
        Soon = 2,
        Completed = 3
    }

    /// <summary>Where Hudson City University is (Careers/CampusBuilding).</summary>
    public enum CampusPlace
    {
        None = 0,     // nowhere: its lectures are followed online
        Home = 1,     // its permanent building, 26 2nd Avenue
        Office = 2,   // meanwhile, the largest office building with no business in it
        School = 3    // meanwhile, at the door of the game's own Manhattan Business School (every office building is taken)
    }

    /// <summary>What keeps the university out of its permanent building.</summary>
    public enum HomeBlock
    {
        None = 0,      // nothing: it is there
        Missing = 1,   // not in this city as chosen, or no door to the street
        Players = 2,   // rented or owned by a player (another player's in a Going Public session)
        Business = 3,  // a business in it: the game's, a rival's, or another mod's
        Left = 4,      // a layout left inside
        Held = 5       // another of the city's services the mod placed there (a police station chosen while the university was off), until it moves off
    }

    /// <summary>
    /// Hudson City University's building as numbers, game-free: its permanent building, the place meanwhile, its floors of
    /// five rooms in the catalogue's order, and its hours (Monday to Friday 08:00 to 22:00, Saturday 09:00 to 14:00).
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

        /// <summary>Which way a room's sign faces along the door's forward (+1, -1): away from the room's chair, else the door's forward (out of the room).</summary>
        public static int SignSide(float forwardX, float forwardZ, float doorX, float doorZ, float chairX, float chairZ, bool chair)
        {
            if (!chair)
            {
                return 1;
            }
            float along = forwardX * (doorX - chairX) + forwardZ * (doorZ - chairZ);
            return along < 0f ? -1 : 1;
        }

        public const int SoonMinutes = 60;   // a class this close is marked as an action, as the game marks a course to study

        /// <summary>The game's mark over a classroom door for the room's course (<see cref="RoomMark"/>); <paramref name="minutesToNext"/> negative for none.</summary>
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

        /// <summary>The id the university's building has in the book of the mod's buildings (Core/SpecialBuildings), and its reservation.</summary>
        public const string BookId = "campus";

        // The permanent building, 26 2nd Avenue, chosen from the game's data: the city's one office of size C version 2, a size no
        // rival or office business of the game's can have, so only a player could take it, and the mod refuses it to the player.
        public const string HomeStreet = "ba:street_secondavenue";
        public const int HomeNumber = 26;
        public const string HomeSize = "ba:buildingsize_c";
        public const int HomeVersion = 2;
        public const string HomeType = "ba:buildingtype_office";
        public const string HomeStreetName = "2nd Avenue";   // the game's own street name (StreetData.streetLocalization), for a page made where the game's streets are not loaded

        /// <summary>True for the university's permanent building.</summary>
        public static bool IsHome(string street, int number) => number == HomeNumber && string.Equals(street, HomeStreet, StringComparison.Ordinal);

        /// <summary>The permanent building's address as the game writes one ("26 2nd Avenue"), from the street's name.</summary>
        public static string HomeAddress(string streetName) =>
            HomeNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + (string.IsNullOrEmpty(streetName) ? HomeStreetName : streetName);

        /// <summary>True if the permanent building found is the one chosen from the game's data (an office no rival can take); false after a change of the city.</summary>
        public static bool HomeIsAsChosen(string type, string size, int version, bool specialService)
        {
            return !specialService && type == HomeType && size == HomeSize && version == HomeVersion;
        }

        /// <summary>
        /// What keeps the university out of its permanent building, <see cref="HomeBlock.None"/> when free: a player's first, then
        /// another service filed there (<paramref name="heldByOther"/>), then a business, then a layout left inside.
        /// </summary>
        public static HomeBlock HomeBlockOf(bool homeServes, string businessType, bool rentedByPlayer, bool ownedByPlayer, string businessOwner, bool layoutLeft, bool heldByOther = false)
        {
            if (!homeServes)
            {
                return HomeBlock.Missing;
            }
            if (rentedByPlayer || ownedByPlayer)
            {
                return HomeBlock.Players;
            }
            if (heldByOther)
            {
                return HomeBlock.Held;   // another feature's book entry there: it moves off at its next choice, the university waits
            }
            if (!NoBusinessIn(businessType, false, false, businessOwner))
            {
                return HomeBlock.Business;
            }
            return layoutLeft ? HomeBlock.Left : HomeBlock.None;
        }

        /// <summary>Where the university is: home when free, else the largest office with no business, else the door of the game's school; None without it.</summary>
        public static CampusPlace Choose(bool homeFree, bool officeFree, bool schoolThere)
        {
            if (homeFree)
            {
                return CampusPlace.Home;
            }
            if (officeFree)
            {
                return CampusPlace.Office;
            }
            return schoolThere ? CampusPlace.School : CampusPlace.None;
        }

        /// <summary>True while the university has a row and a marker on the city map: whenever it stands somewhere, at home or meanwhile.</summary>
        public static bool OnMap(bool usable, bool hasBuilding, CampusPlace place)
        {
            return usable && hasBuilding && place != CampusPlace.None;
        }

        public const float DoorReach = 3f;        // metres: the university's place at its own door, with no board
        public const float PanelGap = 0.2f;       // metres kept between two places' reaches, so the game's panels never both show

        /// <summary>How near the door the player must be at the university with no board: <see cref="DoorReach"/>, less at the school so the driving desk's panel never also shows.</summary>
        public static float DoorReachAt(CampusPlace place, float deskAside, float deskReach)
        {
            return place == CampusPlace.School ? Math.Max(0.5f, Math.Min(DoorReach, deskAside - deskReach - PanelGap)) : DoorReach;
        }

        /// <summary>True if two places this far apart, reached within these distances, can never both be reached at once (with the gap kept).</summary>
        public static bool Apart(float distance, float reachA, float reachB)
        {
            return reachA + reachB + PanelGap <= distance + 0.0001f;
        }

        /// <summary>The locale key of the sentence that says why the university is not in its permanent building; null when it is.</summary>
        public static string AwayKey(HomeBlock block)
        {
            switch (block)
            {
                case HomeBlock.Players: return "reallife_campus_away_players";
                case HomeBlock.Business: return "reallife_campus_away_business";
                case HomeBlock.Left: return "reallife_campus_away_business";
                case HomeBlock.Missing: return "reallife_campus_away_missing";
                case HomeBlock.Held: return "reallife_campus_away_held";
                default: return null;
            }
        }

        /// <summary>The locale key of the notice that says where the university is meanwhile; null at its permanent building or nowhere.</summary>
        public static string AwayNoteKey(CampusPlace place)
        {
            return place == CampusPlace.Office ? "reallife_campus_note_away_office" : place == CampusPlace.School ? "reallife_campus_note_away_school" : null;
        }

        /// <summary>The notice last told of where the university is, as the save keeps it ("place|street|number|block"); null at home.</summary>
        public static string AwayMark(CampusPlace place, string street, int number, HomeBlock block)
        {
            if (place != CampusPlace.Office && place != CampusPlace.School)
            {
                return null;
            }
            return ((int)place).ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + street + "|" + number.ToString(System.Globalization.CultureInfo.InvariantCulture)
                   + "|" + ((int)block).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>True if a move is written to the log: the building differs from the save's and the save had one (a first choice is no move).</summary>
        public static bool SaysMove(bool moved, bool saveHadAddress) => moved && saveHadAddress;

        /// <summary>True if the notice of where the university is is to be told now: it is away from home and the save has not been told this.</summary>
        public static bool TellAway(string mark, string told)
        {
            return mark != null && !string.Equals(mark, told, StringComparison.Ordinal);
        }

        /// <summary>True if the university, standing elsewhere, moves home now: home is free, the player is not inside the old one and nothing is being sat.</summary>
        public static bool MovesHome(bool atHome, bool homeFree, bool playerInside, bool sitting)
        {
            return !atHome && homeFree && !playerInside && !sitting;
        }

        /// <summary>True for a registration that holds no business and is nobody's: the university may take its building.</summary>
        public static bool NoBusinessIn(string businessType, bool rentedByPlayer, bool ownedByPlayer, string businessOwner)
        {
            return (string.IsNullOrEmpty(businessType) || businessType == NoBusiness) && !rentedByPlayer && !ownedByPlayer && string.IsNullOrEmpty(businessOwner);
        }

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

        /// <summary>When it opens next from a minute of a game day (today's later opening, else the next open day); false only if no day opens.</summary>
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

        /// <summary>True if every class of a course followed in person falls inside the opening hours from when it can be joined until it ends.</summary>
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
