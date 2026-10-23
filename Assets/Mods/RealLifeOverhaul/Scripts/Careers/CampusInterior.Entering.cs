using System;
using System.Collections;
using System.Collections.Generic;
using BigAmbitions.DayNightCycle;
using Buildings;
using Helpers;
using Localizor;
using RealLifeOverhaul.Floors;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// Going into the university: its own refusals (closed, a vehicle, away from its door), then the floors core, which reads
    /// the building as the school's structure while the game loads it; the floor's registration of the mod's own; and the
    /// university's building and floors as the core sees them (<see cref="CampusHost"/>, <see cref="CampusFloors"/>).
    /// </summary>
    internal sealed partial class CampusInterior
    {
        /// <summary>Goes in to a floor and on to a course's room (or -1), begun there with <paramref name="begin"/>; null if asked, else a reason key, logged.</summary>
        public string Enter(int floor, int course, string how, bool begin = false)
        {
            CampusBuilding campus = Campus;
            BuildingManager manager = Manager;
            if (!Available || manager == null || _host == null)
            {
                return Refused("reallife_study_why_not_university", how, "the university's floors are not there");
            }
            if (!OpenNow())
            {
                return Refused("reallife_campus_why_closed", how, "closed (" + HoursLine(false) + ")");
            }
            if (PlayerHelper.IsUsingVehicle)
            {
                return Refused("reallife_campus_why_vehicle", how, "the player is in or holding a vehicle");
            }
            Vector3 offset = campus.Door - PlayerHelper.GetPosition();
            bool insideOurs = CareersGame.InsideAt(campus.Street, campus.Number);
            if (!insideOurs && (BuildingManager.IsInsideBuilding || new Vector2(offset.x, offset.z).magnitude > WatchReach))
            {
                return Refused("reallife_study_why_not_university", how, "the player is not at its door (" + new Vector2(offset.x, offset.z).magnitude.ToString("0") + " m away)");
            }
            if (manager.enteringBuilding || FloorsDirector.GoingIn)
            {
                return Refused("reallife_study_why_game_busy", how, "the game is already going into a building");
            }
            if (insideOurs && Floor > 0)
            {
                ChangeFloor(floor, course, begin);
                return null;
            }
            int pending = Mathf.Clamp(floor, 1, CampusRules.Floors);
            _goCourse = course;
            _goBegin = begin && course >= 0;
            int doorId = FloorsDirector.NearestDoor(campus.Building);
            _logger?.Info("Careers: going into the university, floor " + pending + (course >= 0 ? " for " + CareersText.Course(course) : string.Empty) + " (" + how
                          + ", door " + doorId + ", at " + Clock() + ").");
            string why = FloorsDirector.Enter(_host, pending, how);
            if (why != null)
            {
                _goCourse = -1;
                _goBegin = false;
                return Refused("reallife_study_why_game_busy", how, why);
            }
            return null;
        }

        private string Refused(string key, string how, string why)
        {
            _logger?.Info("Careers: the university refused the player (" + how + ", at " + Clock() + "): " + why + ".");
            return key;
        }

        // The floor the game's own door takes the player to: the floor of the class, exam or ceremony on now, else of the first
        // course they are on in person, else the first.
        private int DoorFloor()
        {
            if (_director.UniversityBusinessNow(out int course))
            {
                return CampusRules.FloorOf(course);
            }
            int first = _director.FirstUniversityCourse();
            return first >= 0 ? CampusRules.FloorOf(first) : 1;
        }

        // The game's own way in was seen: to the door's floor, and to its class's room if one is on there (nothing begun).
        private void GameWayIn(int floor)
        {
            _goCourse = _director.UniversityBusinessNow(out int course) && CampusRules.FloorOf(course) == floor ? course : -1;
            _goBegin = false;   // the game's own way in: to the classroom's door, nothing begun
            _logger?.Info("Careers: the player is going into the university by the game's own door, to floor " + floor + " (at " + Clock() + ").");
        }

        // The player is on a floor: the classrooms open as the university is.
        private void Arrived(FloorVisit v)
        {
            _visit = v;
            Floor = v.Floor;
            _layCourse = _goCourse;
            _layBegin = _goBegin;
            _goCourse = -1;
            _goBegin = false;
            BuildingManager manager = Manager;
            if (manager != null)
            {
                manager.isOpen = OpenNow();
            }
        }

        // The player is off the university's floors: what the game took away with the building is forgotten.
        private void Left(FloorVisit v)
        {
            _logger?.Info("Careers: the player has left the university (floor " + Floor + ", at " + Clock() + ").");
            Floor = 0;
            _visit = null;
            for (int i = 0; i < _rooms.Length; i++)
            {
                _rooms[i] = null;
                _chairs[i] = null;
                _signs[i] = null;   // destroyed with the game's indoor items
            }
            if (v.Stays)
            {
                _marks.Clear();    // the player stays inside (the floors were taken away): the icons are given back to the game's pool
            }
            else
            {
                _marks.Forget();   // the game let its icons go with the building
            }
        }

        // Once a second on a floor: the classrooms open or shut with the university, and the marks over their doors.
        private void OnFloor(FloorVisit v)
        {
            BuildingManager manager = Manager;
            bool open = OpenNow();
            if (manager != null)
            {
                bool wasOpen = manager.isOpen;
                manager.isOpen = open;
                if (wasOpen != open && Meta.Dev.Verbose)
                {
                    _logger?.Info("Careers check (university floor): the classrooms are " + (open ? "open" : "closed") + " now: PASS");
                }
            }
            Marks();
        }

        // The building manager's registration while on a floor: the mod's own, kept nowhere.
        private static BuildingRegistration Shadow(BuildingRegistration real, CampusBuilding campus)
        {
            var r = new BuildingRegistration
            {
                StreetName = campus.Street,
                StreetNumber = campus.Number,
                BusinessName = CareersText.T("reallife_study_campus_university"),
                businessTypeName = CareersGame.SchoolType,
                Layout = SchoolLayout,
                AvailableForRent = false,
                RentedByPlayer = false
            };
            if (real != null)
            {
                r.dirtSpots = real.dirtSpots;
                r.interiorDesigns = real.interiorDesigns;
                r.buildingOwnerRivalId = real.buildingOwnerRivalId;
            }
            r.scheduleDays = new List<ScheduleDay>(7);
            for (int d = 1; d <= 7; d++)
            {
                var day = new ScheduleDay { day = (DayOfWeekOrdered)d, isOpen = CampusRules.OpenDay(d) };
                if (day.isOpen)
                {
                    day.openingHourSlots.Add(new OpeningHourSlot(CampusRules.Opens(d), CampusRules.Closes(d)));
                }
                r.scheduleDays.Add(day);
            }
            return r;
        }

        // A floor button's arguments, made once for each (the class's key or the floor's, the floor, the course).
        private object Args(bool here, int floor, int course)
        {
            long id = ((here ? 1L : 0L) << 62) | ((long)(uint)floor << 31) | (uint)(course + 1);
            if (!_args.TryGetValue(id, out object args))
            {
                args = course >= 0 ? (object)new { floor = floor.ToString(), course = CareersText.Course(course) } : new { floor = floor.ToString() };
                _args[id] = args;
            }
            return args;
        }

        /// <summary>The university's building as the floors core holds it: open by its hours, its panel at the door even while closed.</summary>
        private sealed class CampusHost : FloorHost
        {
            private readonly CampusInterior _u;

            public CampusHost(CampusInterior u) : base(CampusRules.BookId, Holder, "Careers check (university door)", "Careers check (university lift)")
            {
                _u = u;
            }

            public override bool Open() => _u.OpenNow();

            public override bool ShowsDoor(bool open) => true;   // closed, its floors are offered greyed with its hours

            public override int DefaultFloor(bool afterLoad) => afterLoad ? 1 : _u.DoorFloor();

            public override void GameWayIn(int floor) => _u.GameWayIn(floor);

            public override void DoorLine(bool open, out string left, out string right, out Color32 colour)
            {
                left = _u.HoursLine(open);
                right = (open ? "common_open" : "common_closed").GetLocalization();
                colour = open ? WorldPanel.Green() : WorldPanel.Red();
            }

            public override void DoorsChanged(bool open)
            {
                _u._logger?.Info("Careers: the university's doors are " + (open ? "open" : "shut") + " (" + Clock() + ", " + _u.HoursLine(open) + ").");
            }

            public override void DoorUp(bool up, bool open)
            {
                GameInstance save = SaveGameManager.Current;
                if (!up || open || save == null || _u._toldClosed == save.Day * 100 + save.Hour)
                {
                    return;
                }
                _u._toldClosed = save.Day * 100 + save.Hour;
                _u._logger?.Info("Careers: the player is at the university's door at " + Clock() + " and it is closed (" + _u.HoursLine(false) + "); the floors are offered greyed.");
            }

            public override void FoundInside(int floor, bool throughDoor, bool open)
            {
                _u._logger?.Info(open
                    ? "Careers: the player is inside the university's building without its floors (a save made inside, or a way in the mod did not see); taken to floor " + floor + "."
                    : "Careers: the player is inside the university's building while it is closed; taken out to the street.");
            }

            public override string DoorWhat(bool open)
            {
                CampusBuilding campus = _u.Campus;
                return "at the door of " + (campus != null ? campus.Name : Name) + (open ? ", open" : ", closed");
            }

            public override string LiftWhat(int floor) => "the lift on floor " + floor;
        }

        /// <summary>
        /// The university's classrooms: its floors on the game's school structure (D1) with the school's layout laid in, five rooms a
        /// floor each holding a course of its own (CampusRules), and the registration of the mod's own while the player is there.
        /// </summary>
        private sealed class CampusFloors : FloorPlace
        {
            private readonly CampusInterior _u;

            public CampusFloors(CampusInterior u) : base(CampusRules.BookId, Holder, "reallife_study_campus_university")
            {
                _u = u;
                Floors = CampusRules.Floors;
                Structure = new FloorStructure(SizeD, SchoolVersion);
                Layout = new FloorLayout(CareersGame.SchoolType, SchoolLayout);
                LayLast = SchoolDoor;   // the classroom doors after the chairs they find
                Check = "Careers check (university way in)";
                LayCheck = "Careers check (university floor)";
                LogName = "the university";
                StructureWords = "the school's structure";
            }

            public override bool Open() => _u.OpenNow();

            public override void Button(int floor, int part, out string key, out object args, out bool marked)
            {
                int classFloor = _u._director.UniversityBusinessNow(out int classCourse) ? CampusRules.FloorOf(classCourse) : 0;
                bool here = floor == classFloor;
                key = here ? "reallife_campus_floor_class" : "reallife_campus_floor";
                args = _u.Args(here, floor, here ? classCourse : -1);
                marked = here;
            }

            public override string LiftLine(int floor, int part) => CareersText.T("reallife_campus_lift_here", "floor", floor.ToString());

            public override bool Busy() => _u._director.SittingNow;

            public override void Pressed(FloorHost host, int floor)
            {
                int course = _u._director.UniversityBusinessNow(out int now) && CampusRules.FloorOf(now) == floor ? now : -1;
                string why = _u.Enter(floor, course, "the floor button at the door");
                if (why != null)
                {
                    _u.Tell(why);
                }
            }

            public override void Arrive(FloorVisit visit) => _u.Arrived(visit);

            public override void Laid(FloorVisit visit, FloorLaid laid) => _u.Laid(visit, laid);

            public override void Moved(FloorVisit visit, int fromFloor) => _u.Moved(visit, fromFloor);

            public override void Shown(FloorVisit visit) => _u.Shown(visit);

            public override void Leave(FloorVisit visit) => _u.Left(visit);

            public override void Tick(FloorVisit visit) => _u.OnFloor(visit);

            public override BuildingRegistration MakeShadow(BuildingRegistration real, bool borrowed)
            {
                CampusBuilding campus = _u.Campus;
                return campus != null ? Shadow(real, campus) : null;
            }
        }
    }
}
