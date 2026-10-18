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
    /// Hudson City University's building as numbers (Careers/CampusBuilding and CampusInterior hold the game's side);
    /// game-free, EconomyCheck runs every rule. Its permanent building, 26 2nd Avenue, is taken whenever it is free, else
    /// the largest free office building meanwhile (ties broken by address), the same on every machine of a session. Inside,
    /// each floor is the game's school with five of the university's courses in its rooms, in the catalogue's order. Open
    /// Monday to Friday 08:00 to 22:00 and Saturday 09:00 to 14:00 (graduation day); shut on Sunday.
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

        /// <summary>The id the university's building has in the book of the mod's buildings (Core/SpecialBuildings), and its reservation.</summary>
        public const string BookId = "campus";

        // The university's permanent building, 26 2nd Avenue, chosen from the game's own data: the city's one office building of
        // size C version 2 (5,971 sqm on a busy avenue), a size in none of the rivals' office sizes and of no office business's
        // layout, so the game's city leaves it empty in every new game and no rival or business of the game's can open in it.
        // Only a player could take it: the mod refuses it to the player (Core/ClaimedOffers).
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

        /// <summary>
        /// The permanent building found in the city is the one chosen from the game's data: an office of the size and
        /// version that no rival and no business of the game's can take. False after a change of the game's city.
        /// </summary>
        public static bool HomeIsAsChosen(string type, string size, int version, bool specialService)
        {
            return !specialService && type == HomeType && size == HomeSize && version == HomeVersion;
        }

        /// <summary>
        /// What keeps the university out of its permanent building now, <see cref="HomeBlock.None"/> when it is free: a player's
        /// first (rented or owned), then another of the mod's services filed there (<paramref name="heldByOther"/>), then a
        /// business (any type but the game's empty one), then a layout left inside; Missing: not in this city as chosen, or no door.
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

        /// <summary>
        /// Where the university is: its permanent building when free, else the largest office building with no business in it,
        /// else at the door of the game's own Manhattan Business School, which always stands and is nobody's; None only without it.
        /// </summary>
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

        /// <summary>
        /// True while the university has a row and a marker on the city map (World/MapLinks): whenever it stands somewhere,
        /// its permanent building or a place meanwhile (an office, the game's school's door); never while it has none.
        /// </summary>
        public static bool OnMap(bool usable, bool hasBuilding, CampusPlace place)
        {
            return usable && hasBuilding && place != CampusPlace.None;
        }

        public const float DoorReach = 3f;        // metres: the university's place at its own door, with no board
        public const float PanelGap = 0.2f;       // metres kept between two places' reaches, so the game's panels never both show

        /// <summary>
        /// How near the door the player stands to be at the university when it has no board: <see cref="DoorReach"/>, but at
        /// the door of Manhattan Business School no further than leaves <see cref="PanelGap"/> before the driving school's
        /// desk (<paramref name="deskAside"/> metres along the wall from the door, reached within <paramref name="deskReach"/>),
        /// so the class panel and the desk's panel never both show.
        /// </summary>
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

        /// <summary>
        /// What the save keeps of the notice last told about where the university is ("place|street|number|block"), so that
        /// it is told once and again only when something changes; null at its permanent building (nothing to tell).
        /// </summary>
        public static string AwayMark(CampusPlace place, string street, int number, HomeBlock block)
        {
            if (place != CampusPlace.Office && place != CampusPlace.School)
            {
                return null;
            }
            return ((int)place).ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + street + "|" + number.ToString(System.Globalization.CultureInfo.InvariantCulture)
                   + "|" + ((int)block).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// True if a move of the university is written to the log: its building differs from the one the save had, and the
        /// save had one (a new game's first choice, against the earlier rule's guess, is no move).
        /// </summary>
        public static bool SaysMove(bool moved, bool saveHadAddress) => moved && saveHadAddress;

        /// <summary>True if the notice of where the university is is to be told now: it is away from home and the save has not been told this.</summary>
        public static bool TellAway(string mark, string told)
        {
            return mark != null && !string.Equals(mark, told, StringComparison.Ordinal);
        }

        /// <summary>
        /// True if the university, standing elsewhere, moves to its permanent building now: it is free, and nothing of the
        /// player's is under way at the old one (the player inside it, a class or exam being sat). It moves once, for good.
        /// </summary>
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
