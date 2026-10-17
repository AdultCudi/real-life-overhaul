using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using BigAmbitions.InteriorDesigner.InteriorElements;
using BigAmbitions.Items;
using Blueprints;
using Buildings;
using BusinessLayoutSets;
using Controllers;
using LayoutItem = BusinessLayoutSets.Item;
using Helpers;
using Localizor;
using Localizor.LanguageChangeEvent;
using PlayerActivity;
using RealLifeOverhaul.GameUi;
using Streets;
using TMPro;
using UI;
using UI.InGameUI;
using UI.Notification;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// Inside Hudson City University: the game's own Manhattan Business School on several floors.
    ///
    /// WHAT THE GAME'S SCHOOL IS. Manhattan Business School (7 4th Avenue) is the city's one building of size D, version 1,
    /// a special one: its interior is the game's building structure for D1 (BuildingStructureD1: the floor, the walls of
    /// five classrooms with their door frames, the school's entrance), loaded by BuildingManager from the size and version
    /// of the building entered, and its furniture is the game's business layout for a school of that size
    /// (StreamingAssets/BusinessLayouts/School/D1/GarmentDistrictSchool.json: 192 pieces, five classroom doors, each with
    /// a desk, a computer and a chair). A classroom door (EducationDoorController) opens the game's study panel for the
    /// course its custom value names, if the building's business is open (BuildingManager.isOpen, from its hours).
    ///
    /// HOW IT IS REUSED. Going in, the game is asked to enter the university's own building with its size and version read
    /// as D1 for the few frames until it has loaded the structure (the building's data object is the game's, kept in
    /// memory only; it is put back on the game's own "entered" event, and by a watch if the entering stops), so the game
    /// itself loads the school's structure and lighting. The school's layout is then laid in, piece by piece the way the
    /// game lays a layout (BuildingManager.LoadBusinessLayoutSet), over the four frames the game keeps the screen dark,
    /// its classroom doors last, and the game is asked to read the pieces again (ForceUpdateAvailableProducers), so the
    /// doors find their chairs. While the player is on a floor the building is the university to the game inside it: the
    /// building manager is given a registration of the mod's own, never kept in the save nor in the game's list (so Going
    /// Public sends nothing of it), with the university's name, the school's business type and the university's hours, so
    /// the doors open when it is open, and the game's study panel works as at the school. The real registration of the
    /// building, which holds no business, is never changed.
    ///
    /// FLOORS. Each floor is that one interior with five of the university's courses in its rooms (CampusRules): floor 1
    /// room 1 is the first. The floor is chosen at the door outside, on the game's own panel (Floor 1, Floor 2, and so on,
    /// the floor with a class of the player's now saying so), or, from inside, at the panel by the entrance, which is the
    /// lift: the screen goes dark, the rooms are given the other floor's courses, and the player is at the entrance of that
    /// floor. The game's own click on the building goes in too while it is open (its doors are "always open" then, as the
    /// game's own walk-in places are), to the floor of a class of the player's or the first. Each classroom door is named
    /// with its course where the game names what is under the pointer, and a sign over the door, read from the corridor,
    /// says the room and course. The game's mark over the door is the course's (Careers/CampusMarks): a class soon,
    /// enrolled, completed. A classroom door opens the game's study panel with the room's course alone, in place of the
    /// game's courses (Careers/SchoolDesk): its class or exam to begin, or when its next class is. The game's own five
    /// courses are never offered here; they are the business school's. Each door keeps the game's custom value, so the
    /// game's door still finds its own room's chair, where the class is sat.
    ///
    /// CLOSED. The building card, the panel at the door and the Campus app say when it closes or when it opens again. A
    /// press of a floor while it is closed, and the game's own refusal at its door, are told to the player with the hour it
    /// opens, and written to the log.
    ///
    /// GOING PUBLIC. The building is the same on every machine (Careers/CampusBuilding); everything here is the machine's
    /// own: the interior, the floors, the doors' flags. Nothing is sent and nothing of anyone's is changed.
    ///
    /// THE COST. Per frame (from CareersDirector.LateUpdate) the game's card over a building: its target read and compared
    /// with the last (the card itself kept, found again only with a new city), a fraction of a microsecond; while the player
    /// stands within a few metres of the door outside and it is open, one flag of the building manager read each frame (so
    /// the game's own click is seen before the game loads the structure); once a second the panels, the doors and the five
    /// rooms' marks, the card's hours written again only when they or the label changed, the panels' buttons and texts from
    /// kept lists and arguments; the furniture over four frames when a floor is entered.
    /// </summary>
    internal sealed class CampusInterior
    {
        public const string SchoolLayout = "GarmentDistrictSchool";
        private const string SizeD = "ba:buildingsize_d";
        private const int SchoolVersion = 1;
        private const float DoorReach = 3.2f;
        private const float LiftReach = 2.6f;
        private const float WatchReach = 12f;
        private const int Chunks = 4;   // the frames the game keeps the screen dark after it has entered

        private readonly CareersDirector _director;
        private readonly IModLogger _logger;
        private readonly DoorPanel _door;
        private readonly DoorPanel _lift;
        private readonly Action[] _doorPress = new Action[4];
        private readonly Action[] _liftPress = new Action[4];
        private readonly int[] _doorFloor = new int[4];
        private readonly int[] _liftFloor = new int[4];
        private readonly Dictionary<long, object> _args = new Dictionary<long, object>(16);   // a floor button's arguments by class, floor and course
        private readonly List<int> _floorList = new List<int>(CampusRules.Floors);              // the floors a panel offers, made again in place each second
        private Func<string> _doorWhat, _liftWhat;   // what the door's and the lift's panels say in the log (made once)
        private bool _doorWhatOpen;
        private BuildingResume _card;                 // the game's building card, kept until the city goes
        private long _cardHours = long.MinValue;      // what the card's hours were written from (HoursStamp)...
        private string _cardText;                     // ...and the label's text just after: written again only if either has changed
        private readonly EducationDoorController[] _rooms = new EducationDoorController[CampusRules.RoomsPerFloor];
        private readonly Transform[] _chairs = new Transform[CampusRules.RoomsPerFloor];   // each room's chair, found as the game's door finds it
        private readonly GameObject[] _signs = new GameObject[CampusRules.RoomsPerFloor];
        private readonly CampusMarks _marks;
        private static Material _signLook;    // the game's font's material, drawn from the front only (one for every sign)
        private static TMP_FontAsset _signFont;

        private BusinessLayoutSet _set;
        private bool _checked, _available;
        private Building _asset;
        private string _wasSize;
        private int _wasVersion;
        private bool _mutated;
        private int _pending = 1, _goCourse = -1;
        private bool _goBegin;         // ...and, once there, its class (or exam, or homework) begun at its desk for the player
        private int _built;            // generation of the floor being laid
        private bool _watching;
        private CityBuildingController _doorsOf;
        private bool[] _doorWas;
        private bool _doorsOpen;
        private BuildingRegistration _shadow;
        private Vector3 _spawn;
        private int _doorPage, _liftPage;
        private int _toldClosed = -1;
        private bool _wrongTold;
        private EntityController _carded;
        private Action<Address> _onEnter, _enterSeen, _onExit, _exitSeen;
        private Action<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool> _onNotice, _noticeSeen;

        public CampusInterior(CareersDirector director, IModLogger logger)
        {
            _director = director;
            _logger = logger;
            _door = new DoorPanel(logger, "Careers check (university door)");
            _lift = new DoorPanel(logger, "Careers check (university lift)");
            for (int i = 0; i < 4; i++)
            {
                int slot = i;
                _doorPress[i] = _door.Press(() => DoorPressed(slot));
                _liftPress[i] = _lift.Press(() => LiftPressed(slot));
            }
            _onEnter = Guard.Wrap<Address>(Entered);   // (a fault here never reaches the game's own handlers: Core/Guard)
            _onExit = Guard.Wrap<Address>(Left);
            _onNotice = Guard.Wrap<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool>(Noticed);
            _marks = new CampusMarks(logger);
        }

        /// <summary>The floor the player is on, from 1; 0 when not inside the university's floors.</summary>
        public int Floor { get; private set; }

        /// <summary>True while either panel of the university is up, and in the frame its button ran.</summary>
        public bool TakesKey => _door.TakesKey || _lift.TakesKey;

        private CampusBuilding Campus => _director.University;

        private static BuildingManager Manager => InstanceBehavior<BuildingManager>.Instance;

        /// <summary>True if the university's floors can be built: its building is chosen and the game's school interior is there.</summary>
        public bool Available
        {
            get
            {
                CampusBuilding campus = Campus;
                if (campus == null || !campus.Usable || campus.Building == null || campus.Place == CampusPlace.School)
                {
                    return false;   // (at the game's own school its classes are joined at the door: the school inside is the game's)
                }
                if (!_checked)
                {
                    _checked = true;
                    Check();
                }
                return _available;
            }
        }

        // Once: the game's school layout is there and has its five classrooms.
        private void Check()
        {
            try
            {
                _set = BusinessLayoutSetHelper.GetOrLoadBusinessLayoutSet(CareersGame.SchoolType, new BuildingSizeInfo(SizeD, SchoolVersion), SchoolLayout, false);
                int doors = 0, pieces = _set != null && _set.Items != null ? _set.Items.Count : 0;
                for (int i = 0; i < pieces; i++)
                {
                    doors += CampusRules.RoomOfDoor(_set.Items[i].customValue) > 0 && IsDoor(_set.Items[i].itemName) ? 1 : 0;
                }
                _available = _set != null && doors == CampusRules.RoomsPerFloor;
                _logger?.Info("Careers: the university's floors: " + (_available
                    ? "the game's own school inside (structure D1, layout " + SchoolLayout + ", " + pieces + " pieces, " + doors + " classrooms); " + CampusRules.Courses.Length
                      + " courses on " + CampusRules.Floors + " floors, " + CampusRules.RoomsPerFloor + " to a floor."
                    : "the game's school layout is not as this version expects (" + (_set == null ? "not found" : doors + " classroom doors") + "); classes are attended at the sign by the door instead."));
            }
            catch (Exception e)
            {
                _available = false;
                _logger?.Error(e);
                _logger?.Warn("Careers: the university's floors could not be read after the error above; classes are attended at the sign by the door instead.");
            }
        }

        private static bool IsDoor(string itemName) => itemName == "ba:itemname_schooldoor";

        // ---------------------------------------------------------------- once a second

        public void Tick()
        {
            EventHold.Keep(ref GlobalEvents.onEnterBuilding, _onEnter, ref _enterSeen);
            EventHold.Keep(ref GlobalEvents.onExitBuilding, _onExit, ref _exitSeen);
            EventHold.Keep(ref Notifications.onShow, _onNotice, ref _noticeSeen);
            CampusBuilding campus = Campus;
            if (!Available)
            {
                Gone("no university's floors");
                return;
            }
            if (!ReferenceEquals(_doorsOf, campus.Building))
            {
                Doors(false);
                _doorsOf = campus.Building;
                _doorWas = null;
            }
            bool open = OpenNow();
            Doors(open);
            CardNow();
            BuildingManager manager = Manager;
            PlayerController player = PlayerHelper.PlayerController;
            if (manager == null || player == null)
            {
                return;
            }
            bool insideOurs = CareersGame.InsideAt(campus.Street, campus.Number);
            if (insideOurs && Floor == 0 && !manager.enteringBuilding && !_mutated)
            {
                Wrong(open);
                return;
            }
            if (Floor > 0 && insideOurs)
            {
                _door.Hide("inside");
                _watching = false;
                bool wasOpen = manager.isOpen;
                manager.isOpen = open;
                if (wasOpen != open && Meta.Dev.Verbose)
                {
                    _logger?.Info("Careers check (university floor): the classrooms are " + (open ? "open" : "closed") + " now: PASS");
                }
                Marks();
                LiftPanel(player);
                return;
            }
            _lift.Hide("not on a floor");
            if (BuildingManager.IsInsideBuilding)
            {
                _door.Hide("inside another building");
                _watching = false;
                return;
            }
            Vector3 at = player.transform.position;
            Vector3 offset = campus.Door - at;
            float flat = new Vector2(offset.x, offset.z).magnitude;
            bool busy = PlayerHelper.IsUsingVehicle || ThirdPersonCamera.IsUiOpen() || PlayerActivityUI.IsPanelOpen || JobDirector.TakesInteractKey;
            if (open && flat < WatchReach && !_watching)
            {
                _watching = true;
                _director.StartCoroutine(Guard.Routine(WatchClick()));
            }
            else if ((!open || flat >= WatchReach) && _watching)
            {
                _watching = false;
            }
            if (busy || flat > DoorReach || Mathf.Abs(offset.y) > 2.5f)
            {
                _door.Hide("away from the door");
                return;
            }
            DoorPanel(open, player);
        }

        /// <summary>The university is open now: its hours, or a class, an exam or a ceremony of the player's there now.</summary>
        public bool OpenNow()
        {
            GameInstance save = SaveGameManager.Current;
            return save != null && (CampusRules.OpenAt(save.Day, CareersGame.MinuteOfDay) || _director.UniversityBusinessNow(out _));
        }

        // Everything HoursLine's words are made from, as one number (the same number, the same words in a language).
        private static long HoursStamp(bool open)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return 0L;
            }
            int day = save.Day;
            if (open)
            {
                return (1L << 60) | (uint)Math.Max(CampusRules.Closes(day), save.Hour + 1);
            }
            if (!CampusRules.NextOpening(day, CareersGame.MinuteOfDay, out int openDay, out int openHour))
            {
                return 2L << 60;
            }
            return (openDay == day ? 3L << 60 : 4L << 60) | ((long)(uint)openDay << 8) | (uint)(openHour & 0xFF);
        }

        /// <summary>When it closes today (an hour) or, closed, when it opens next, in words for the card, the door and the app.</summary>
        public string HoursLine(bool open)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return string.Empty;
            }
            int day = save.Day;
            if (open)
            {
                return CareersText.T("reallife_campus_door_open", "time", CareersText.Hour(Math.Max(CampusRules.Closes(day), save.Hour + 1)));
            }
            if (!CampusRules.NextOpening(day, CareersGame.MinuteOfDay, out int openDay, out int openHour))
            {
                return CareersText.T("reallife_campus_door_shut");
            }
            return openDay == day
                ? CareersText.T("reallife_campus_door_closed_today", "time", CareersText.Hour(openHour))
                : CareersText.T("reallife_campus_door_closed_day", "day", TimeHelper.GetDayOfWeek(openDay).GetLocalization(), "time", CareersText.Hour(openHour));
        }

        // The panel at the door outside: the university's name, its hours, and a button a floor.
        private void DoorPanel(bool open, PlayerController player)
        {
            WorldPanel panel = _door.Panel;
            panel.Lines(CareersText.T("reallife_study_campus_university"), HoursLine(open), (open ? "common_open" : "common_closed").GetLocalization(), open ? WorldPanel.Green() : WorldPanel.Red());
            int classFloor = _director.UniversityBusinessNow(out int classCourse) ? CampusRules.FloorOf(classCourse) : 0;
            FloorButtons(panel, _doorPress, _doorFloor, _doorPage, 0, open, classFloor, classCourse);
            Vector3 standing = player.transform.position;
            _doorWhatOpen = open;
            if (_door.Show(Campus.Door, standing, 2.2f, _doorWhat ?? (_doorWhat = () => "at the door of " + Campus.Name + (_doorWhatOpen ? ", open" : ", closed")))
                && !open && _toldClosed != SaveGameManager.Current.Day * 100 + SaveGameManager.Current.Hour)
            {
                _toldClosed = SaveGameManager.Current.Day * 100 + SaveGameManager.Current.Hour;
                _logger?.Info("Careers: the player is at the university's door at " + Clock() + " and it is closed (" + HoursLine(false) + "); the floors are offered greyed.");
            }
        }

        // The lift by the entrance inside: the other floors.
        private void LiftPanel(PlayerController player)
        {
            Vector3 standing = player.transform.position;
            Vector3 offset = _spawn - standing;
            bool busy = ThirdPersonCamera.IsUiOpen() || PlayerActivityUI.IsPanelOpen || _director.SittingNow;
            if (busy || new Vector2(offset.x, offset.z).magnitude > LiftReach || Mathf.Abs(offset.y) > 2f || CampusRules.Floors < 2)
            {
                _lift.Hide("away from the lift");
                return;
            }
            WorldPanel panel = _lift.Panel;
            panel.Lines(CareersText.T("reallife_study_campus_university"), CareersText.T("reallife_campus_lift_here", "floor", Floor.ToString()), null, WorldPanel.White());
            int classFloor = _director.UniversityBusinessNow(out int classCourse) ? CampusRules.FloorOf(classCourse) : 0;
            FloorButtons(panel, _liftPress, _liftFloor, _liftPage, Floor, true, classFloor, classCourse);
            _lift.Show(_spawn, standing, 2.1f, _liftWhat ?? (_liftWhat = () => "the lift on floor " + Floor));
        }

        // Up to four buttons: the floors (leaving out the one the player is on), or three and "more floors" when they do not fit.
        private void FloorButtons(WorldPanel panel, Action[] press, int[] floors, int page, int skip, bool enabled, int classFloor, int classCourse)
        {
            List<int> list = _floorList;
            list.Clear();
            for (int f = 1; f <= CampusRules.Floors; f++)
            {
                if (f != skip)
                {
                    list.Add(f);
                }
            }
            bool paged = list.Count > 4;
            int per = paged ? 3 : 4;
            int start = paged ? (page * per) % list.Count : 0;
            for (int slot = 0; slot < 4; slot++)
            {
                if (paged && slot == 3)
                {
                    floors[slot] = -1;
                    panel.Button(slot, "reallife_campus_floor_more", press[slot], true, false);
                    continue;
                }
                int i = start + slot;
                if (i >= list.Count || slot >= per)
                {
                    floors[slot] = 0;
                    panel.Button(slot, null, null, false, false);
                    continue;
                }
                int floor = list[i];
                floors[slot] = floor;
                bool here = floor == classFloor;
                string key = here ? "reallife_campus_floor_class" : "reallife_campus_floor";
                panel.Button(slot, key, Args(here, floor, here ? classCourse : -1), press[slot], enabled, here || slot == 0);
            }
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

        private void DoorPressed(int slot)
        {
            if (_doorFloor[slot] < 0)
            {
                _doorPage++;
                return;
            }
            if (_doorFloor[slot] > 0)
            {
                int course = _director.UniversityBusinessNow(out int now) && CampusRules.FloorOf(now) == _doorFloor[slot] ? now : -1;
                string why = Enter(_doorFloor[slot], course, "the floor button at the door");
                if (why != null)
                {
                    Tell(why);
                }
            }
        }

        private void LiftPressed(int slot)
        {
            if (_liftFloor[slot] < 0)
            {
                _liftPage++;
                return;
            }
            if (_liftFloor[slot] > 0)
            {
                ChangeFloor(_liftFloor[slot], -1);
            }
        }

        // ---------------------------------------------------------------- going in

        /// <summary>
        /// Goes in to a floor (and on to a course's classroom, or -1; begin: its desk's class begun there for the player, as
        /// the Campus app's Attend asks). Null if the game was asked to go in; else a reason key, which is written to the log
        /// with why.
        /// </summary>
        public string Enter(int floor, int course, string how, bool begin = false)
        {
            CampusBuilding campus = Campus;
            BuildingManager manager = Manager;
            if (!Available || manager == null)
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
            if (manager.enteringBuilding || _mutated)
            {
                return Refused("reallife_study_why_game_busy", how, "the game is already going into a building");
            }
            if (insideOurs && Floor > 0)
            {
                ChangeFloor(floor, course, begin);
                return null;
            }
            _pending = Mathf.Clamp(floor, 1, CampusRules.Floors);
            _goCourse = course;
            _goBegin = begin && course >= 0;
            int doorId = NearestDoor(campus.Building);
            Mutate("asked by " + how);
            _logger?.Info("Careers: going into the university, floor " + _pending + (course >= 0 ? " for " + CareersText.Course(course) : string.Empty) + " (" + how + ", door " + doorId + ", at " + Clock() + ").");
            try
            {
                manager.EnterBuilding(campus.Building.building, false, false, 0, doorId);
            }
            catch (Exception e)
            {
                Restore("the game's going in failed");
                _logger?.Error(e);
                return "reallife_study_why_game_busy";
            }
            return null;
        }

        private string Refused(string key, string how, string why)
        {
            _logger?.Info("Careers: the university refused the player (" + how + ", at " + Clock() + "): " + why + ".");
            return key;
        }

        private void Tell(string key)
        {
            var data = new Dictionary<string, string>(1) { { "hours", HoursLine(false) } };
            _director.Tell(NotificationType.Warning, key, data, CareersDirector.TabStudying, null);
        }

        private static int NearestDoor(CityBuildingController building)
        {
            Vector3 at = PlayerHelper.GetPosition();
            int best = building.entranceDoors[0].doorId;
            float near = float.MaxValue;
            for (int i = 0; i < building.entranceDoors.Length; i++)
            {
                Transform t = building.entranceDoors[i] != null ? building.entranceDoors[i].doorTransform : null;
                if (t != null && (t.position - at).sqrMagnitude < near)
                {
                    near = (t.position - at).sqrMagnitude;
                    best = building.entranceDoors[i].doorId;
                }
            }
            return best;
        }

        // The building's size and version read as the school's while the game loads the structure.
        private void Mutate(string why)
        {
            Building asset = Campus != null && Campus.Building != null ? Campus.Building.building : null;
            if (asset == null || _mutated)
            {
                return;
            }
            _asset = asset;
            _wasSize = asset.BuildingSize;
            _wasVersion = asset.BuildingVersion;
            asset.BuildingSize = SizeD;
            asset.BuildingVersion = SchoolVersion;
            _mutated = true;
            _director.StartCoroutine(Guard.Routine(Settle()));
            if (Meta.Dev.Verbose)
            {
                _logger?.Info("Careers check (university way in): " + why + ": the building read as D1 until the game has loaded it (was " + _wasSize + " " + _wasVersion + "): PASS");
            }
        }

        private void Restore(string why)
        {
            if (!_mutated)
            {
                return;
            }
            _mutated = false;
            if (_asset != null)
            {
                _asset.BuildingSize = _wasSize;
                _asset.BuildingVersion = _wasVersion;
            }
            if (Meta.Dev.Verbose)
            {
                _logger?.Info("Careers check (university way in): the building's own size put back (" + why + "): PASS");
            }
        }

        // The safety net: whatever happens to the entering, the building's own size is put back when it is over.
        private IEnumerator Settle()
        {
            float until = Time.realtimeSinceStartup + 15f;
            yield return null;
            while (_mutated && Manager != null && Manager.enteringBuilding && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }
            yield return null;
            if (_mutated && (Manager == null || !Manager.enteringBuilding))
            {
                Restore("the game's going in ended without the university");
            }
            else if (_mutated)
            {
                Restore("the game took longer than 15 s to go in");
            }
        }

        // While open and the player is near the door: the game's own click on the building is seen before it loads.
        private IEnumerator WatchClick()
        {
            while (_watching)
            {
                BuildingManager manager = Manager;
                if (manager != null && manager.enteringBuilding && !_mutated && !BuildingManager.IsInsideBuilding)
                {
                    _pending = Mathf.Clamp(DefaultFloor(out _goCourse), 1, CampusRules.Floors);
                    _goBegin = false;   // (the game's own way in: to the classroom's door, its panel up, nothing begun)
                    Mutate("the game's own way in (a click on the building, or its door)");
                    _logger?.Info("Careers: the player is going into the university by the game's own door, to floor " + _pending + " (at " + Clock() + ").");
                    _watching = false;
                }
                yield return null;
            }
        }

        private int DefaultFloor(out int course)
        {
            if (_director.UniversityBusinessNow(out course))
            {
                return CampusRules.FloorOf(course);
            }
            course = -1;
            int first = _director.FirstUniversityCourse();
            return first >= 0 ? CampusRules.FloorOf(first) : 1;
        }

        private static readonly int HotEntered = HotSpots.Slot("Careers: CampusInterior.Entered");
        private static readonly int HotLeft = HotSpots.Slot("Careers: CampusInterior.Left");
        private static readonly int HotNoticed = HotSpots.Slot("Careers: CampusInterior.Noticed");

        // The game has entered a building.
        private void Entered(Address address)
        {
            using var hotSpot = HotSpots.Time(HotEntered);
            try
            {
                CampusBuilding campus = Campus;
                bool ours = campus != null && campus.Usable && address != null && address.streetName == campus.Street && address.streetNumber == campus.Number;
                if (!ours)
                {
                    Restore("another building was entered");
                    return;
                }
                if (!_mutated)
                {
                    return;   // the building's own interior: Tick takes the player to a floor (Wrong)
                }
                Restore("the game has loaded the school's structure");
                BuildingManager manager = Manager;
                Floor = _pending;
                _wrongTold = false;
                _spawn = PlayerHelper.GetPosition();
                _shadow = Shadow(manager.buildingRegistration, campus);
                manager.buildingRegistration = _shadow;
                manager.isOpen = OpenNow();
                _director.StartCoroutine(Guard.Routine(Lay(++_built, _goCourse, _goBegin)));
                _goCourse = -1;
                _goBegin = false;
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        private void Left(Address address)
        {
            using var hotSpot = HotSpots.Time(HotLeft);
            CampusBuilding campus = Campus;
            if (Floor == 0 || campus == null || address == null || address.streetName != campus.Street || address.streetNumber != campus.Number)
            {
                return;
            }
            _logger?.Info("Careers: the player has left the university (floor " + Floor + ", at " + Clock() + ").");
            Floor = 0;
            _built++;
            _shadow = null;
            for (int i = 0; i < _rooms.Length; i++)
            {
                _rooms[i] = null;
                _chairs[i] = null;
                _signs[i] = null;   // (destroyed with the game's indoor items)
            }
            _marks.Forget();       // (the game let its icons go with the building)
            _lift.Hide("left the building");
        }

        // The building entered by its own interior (a save made inside it, or a way in the mod did not see): to a floor, or out.
        private void Wrong(bool open)
        {
            if (_wrongTold)
            {
                return;
            }
            _wrongTold = true;
            if (open)
            {
                _logger?.Info("Careers: the player is inside the university's building without its floors (a save made inside, or a way in the mod did not see); taken to floor 1.");
                _director.StartCoroutine(Guard.Routine(Again()));
            }
            else
            {
                _logger?.Info("Careers: the player is inside the university's building while it is closed; taken out to the street.");
                Manager.ExitFromBuilding(NearestDoor(Campus.Building));
            }
        }

        private IEnumerator Again()
        {
            yield return null;
            string why = Enter(1, -1, "a way back to the floors");
            if (why != null)
            {
                Manager?.ExitFromBuilding(0);
            }
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

        // ---------------------------------------------------------------- the floor's furniture

        // The school's layout laid in over the frames the game keeps the screen dark; the classroom doors last.
        private IEnumerator Lay(int built, int goCourse, bool begin)
        {
            var watch = Stopwatch.StartNew();
            BuildingManager manager = Manager;
            List<LayoutItem> items = _set.Items;
            var order = new List<LayoutItem>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                if (!IsDoor(items[i].itemName))
                {
                    order.Add(items[i]);
                }
            }
            for (int i = 0; i < items.Count; i++)
            {
                if (IsDoor(items[i].itemName))
                {
                    order.Add(items[i]);
                }
            }
            var made = new Dictionary<string, KeyValuePair<ItemController, List<AttachableChild>>>(order.Count);
            try
            {
                var elements = new List<InteriorElement>(InteriorElementsHelper.InteriorElementsCache.Values);
                BuildingManager.ApplyInteriorDesign(_set.interiorDesigns, elements.ToArray());
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            int per = (order.Count + Chunks - 1) / Chunks, at = 0, frames = 0, skipped = 0;
            while (at < order.Count)
            {
                yield return null;
                frames++;
                if (built != _built || !BuildingManager.IsInsideBuilding)
                {
                    yield break;
                }
                long before = watch.ElapsedMilliseconds;
                for (int n = 0; n < per && at < order.Count; n++, at++)
                {
                    try
                    {
                        ItemController made1 = Place(order[at], manager.IndoorItemContainer);
                        if (made1 != null)
                        {
                            made[order[at].id] = new KeyValuePair<ItemController, List<AttachableChild>>(made1, order[at].stackedItems);
                        }
                        else
                        {
                            skipped++;
                        }
                    }
                    catch (Exception e)
                    {
                        skipped++;
                        _logger?.Error(e);
                    }
                }
                if (Meta.Dev.Verbose)
                {
                    _logger?.Info("Careers check (university floor): frame " + frames + ", " + at + " of " + order.Count + " pieces, " + (watch.ElapsedMilliseconds - before) + " ms: PASS");
                }
            }
            try
            {
                foreach (KeyValuePair<string, KeyValuePair<ItemController, List<AttachableChild>>> pair in made)
                {
                    ItemController parent = pair.Value.Key;
                    List<AttachableChild> children = pair.Value.Value;
                    for (int i = 0; children != null && i < children.Count; i++)
                    {
                        if (made.TryGetValue(children[i].childId, out KeyValuePair<ItemController, List<AttachableChild>> child) && parent.AttachmentPoints.Length > children[i].attachmentIndex)
                        {
                            child.Key.SetToParentPlaceableItem(parent, parent.AttachmentPoints[children[i].attachmentIndex]);
                        }
                    }
                }
                manager.ForceUpdateAvailableProducers();   // the game reads the pieces again: the classroom doors find their chairs
                Rooms(manager);
                Label();
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            _logger?.Info("Careers: the university's floor " + Floor + " is laid: " + made.Count + " pieces of the game's school" + (skipped > 0 ? " (" + skipped + " left out)" : string.Empty)
                          + " over " + frames + " frames, " + watch.ElapsedMilliseconds + " ms in all; rooms: " + RoomsWords() + ".");
            Tell(NotificationType.Info, "reallife_campus_note_floor", "floor", Floor.ToString(), "courses", RoomsList());
            if (goCourse >= 0)
            {
                yield return null;   // (the doors have found their chairs: their Start has run)
                GoToRoom(goCourse, begin);
            }
        }

        // One piece of a layout, the way BuildingManager.LoadBusinessLayoutSet makes it.
        private static ItemController Place(LayoutItem item, Transform container)
        {
            BigAmbitions.Items.Item data = ItemsGetter.GetByName(item.itemName);
            if (data == null || data.isSeasonalForSale || (!data.IsAvailableInCurrentSeason() && !BuildingManager.ignoreSeasons))
            {
                return null;
            }
            ItemController c = PrefabHelper.CreatePrefabItem(item.itemName, container);
            if (c == null)
            {
                return null;
            }
            c.playerItemPurchaserSettings = new PlayerItemPurchaserSettings
            {
                enabled = item.playerItemPurchaserSettings != null && item.playerItemPurchaserSettings.enabled,
                itemName = item.playerItemPurchaserSettings != null ? item.playerItemPurchaserSettings.itemName : string.Empty,
                itemQuantity = item.playerItemPurchaserSettings != null ? item.playerItemPurchaserSettings.itemQuantity : 1
            };
            c.itemName = item.itemName;
            c.customPositions = item.customPositions;
            c.customColors = item.customColors;
            c.customValue = item.customValue;
            c.transform.position = item.position;
            c.transform.rotation = item.rotation;
            c.ItemInstance = ItemHelper.InitializeNewInstance(item.itemName);
            c.ItemInstance.id = item.id;
            c.ItemInstance.worldSpaceTextValue = item.worldSpaceTextValue;
            c.ItemInstance.linkedItemName = item.linkedItemName;
            c.gameObject.SetActive(true);
            return c;
        }

        // The floor's five classroom doors by room, and each room's chair as the game's door finds its own (the first seat
        // named like the door: EducationDoorController.SetStudyDiploma), so the sign and the class are the room's own.
        private void Rooms(BuildingManager manager)
        {
            Array.Clear(_rooms, 0, _rooms.Length);
            Array.Clear(_chairs, 0, _chairs.Length);
            List<ItemController> all = manager.allItemControllers;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (all[i] is EducationDoorController door)
                {
                    int room = CampusRules.RoomOfDoor(door.customValue);
                    if (room > 0)
                    {
                        _rooms[room - 1] = door;
                    }
                }
                else if (all[i] is SeatController seat)
                {
                    int room = CampusRules.RoomOfDoor(seat.customValue);
                    if (room > 0 && _chairs[room - 1] == null)
                    {
                        _chairs[room - 1] = seat.transform;
                    }
                }
            }
        }

        // Each classroom door named with its course where the game names what is under the pointer, and a sign over it.
        private void Label()
        {
            TMP_FontAsset font = GameFont.Asset;
            for (int r = 0; r < _rooms.Length; r++)
            {
                EducationDoorController door = _rooms[r];
                if (door == null)
                {
                    continue;
                }
                int course = CampusRules.CourseAt(Floor, r + 1);
                door.customOverlayHeaderKey = course >= 0 ? "reallife" + "_study_course_" + StudyCatalogue.Find(course).Key : "reallife_campus_room_free";
                string words = course >= 0
                    ? CareersText.T("reallife_campus_room_sign", "room", (r + 1).ToString(), "course", CareersText.Course(course))
                    : CareersText.T("reallife_campus_room_sign", "room", (r + 1).ToString(), "course", CareersText.T("reallife_campus_room_free"));   // ("Room 3: Free classroom")
                if (_signs[r] == null && font != null)
                {
                    _signs[r] = Sign(door.transform, _chairs[r], font, r + 1, _logger);
                }
                TextMeshPro text = _signs[r] != null ? _signs[r].GetComponentInChildren<TextMeshPro>() : null;
                if (text != null)
                {
                    text.text = words;
                }
            }
            Marks();
        }

        // Once a second on a floor, and when a floor is laid or changed: the game's mark over each classroom door.
        private void Marks()
        {
            for (int r = 0; r < _rooms.Length; r++)
            {
                if (_rooms[r] != null)
                {
                    _marks.Set(r, _rooms[r], _director.RoomMarkOf(CampusRules.CourseAt(Floor, r + 1)));
                }
            }
        }

        private const float SignHeight = 2.45f;   // metres over the door's foot: above the doorway
        private const float SignOut = 0.2f;       // metres out from the door's plane, clear of its frame
        private static readonly int CullId = Shader.PropertyToID("_CullMode");

        // The room's sign over its door: one face of lettering on a plaque, facing out of the room (away from the room's
        // chair, CampusRules.SignSide), so it reads from the corridor and shows nothing from inside. The plaque stands
        // between the lettering and the room, and the lettering is drawn from its front only, so it is never seen
        // mirrored. (It was two faces back to back, and the game's lettering is drawn from both sides: through the
        // doorway the room's face showed behind the corridor's, mirrored.) Parented to the game's door: it goes with it.
        private static GameObject Sign(Transform door, Transform chair, TMP_FontAsset font, int room, IModLogger logger)
        {
            Vector3 forward = door.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            Vector3 at = door.position, from = chair != null ? chair.position : at;
            int side = CampusRules.SignSide(forward.x, forward.z, at.x, at.z, from.x, from.z, chair != null);
            Vector3 outward = forward * side;
            var root = new GameObject("RealLifeOverhaul University Room Sign");
            Transform place = root.transform;
            place.SetParent(door, false);
            place.SetPositionAndRotation(at + Vector3.up * SignHeight + outward * SignOut, Quaternion.LookRotation(outward, Vector3.up));
            Material plaque = Shapes.Paint(new Color(0.93f, 0.92f, 0.89f));
            if (plaque != null)
            {
                Shapes.Piece("Plaque", place, Shapes.Cube, plaque, new Vector3(0f, 0f, -0.015f), new Vector3(1.62f, 0.36f, 0.02f));
            }
            var label = new GameObject("Lettering");
            Transform letters = label.transform;
            letters.SetParent(place, false);
            letters.localPosition = Vector3.zero;
            letters.localRotation = Quaternion.Euler(0f, 180f, 0f);   // lettering is read from behind its own forward: from outside the room
            var text = label.AddComponent<TextMeshPro>();
            text.font = font;
            Material look = SignLook(font);
            if (look != null)
            {
                text.fontSharedMaterial = look;
            }
            text.color = new Color(0.12f, 0.12f, 0.14f);
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(1.5f, 0.3f);
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.05f;
            text.fontSizeMax = 1.6f;
            var renderer = label.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            if (Meta.Dev.Verbose)
            {
                logger?.Info("Careers check (university room sign): room " + room + ": one face, out of the room ("
                             + (chair != null ? "the room's chair " + new Vector2(from.x - at.x, from.z - at.z).magnitude.ToString("0.0", CultureInfo.InvariantCulture) + " m away on the other side" : "no chair found, the door's own forward")
                             + "), " + (plaque != null ? "on its plaque" : "no plaque") + ", " + (look != null ? "drawn from the front only" : "the font's own material") + ": PASS");
            }
            return root;
        }

        // The game's font's material with its back faces left out (TextMeshPro's own cull setting), made once for every sign.
        private static Material SignLook(TMP_FontAsset font)
        {
            if (_signLook != null && ReferenceEquals(_signFont, font))
            {
                return _signLook;
            }
            Material source = font != null ? font.material : null;
            if (source == null || !source.HasProperty(CullId))
            {
                return null;
            }
            _signFont = font;
            _signLook = new Material(source) { name = "RealLifeOverhaul Room Sign" };
            _signLook.SetFloat(CullId, (float)CullMode.Back);
            return _signLook;
        }

        private string RoomsWords()
        {
            var b = new System.Text.StringBuilder(160);
            for (int r = 1; r <= CampusRules.RoomsPerFloor; r++)
            {
                int course = CampusRules.CourseAt(Floor, r);
                b.Append(r == 1 ? string.Empty : "; ").Append(r).Append(' ').Append(course >= 0 ? StudyCatalogue.Find(course).Key : "free")
                 .Append(_rooms[r - 1] != null ? string.Empty : " (no door)");
            }
            return b.ToString();
        }

        private string RoomsList()
        {
            var b = new System.Text.StringBuilder(160);
            for (int r = 1; r <= CampusRules.RoomsPerFloor; r++)
            {
                int course = CampusRules.CourseAt(Floor, r);
                if (course >= 0)
                {
                    b.Append(b.Length > 0 ? ", " : string.Empty).Append(r).Append(' ').Append(CareersText.Course(course));
                }
            }
            return b.ToString();
        }

        private void Tell(NotificationType type, string key, string a, string av, string b, string bv)
        {
            _director.Tell(type, key, CareersDirector.Data(a, av, b, bv), CareersDirector.TabStudying, null);
        }

        // ---------------------------------------------------------------- floors and rooms from inside

        /// <summary>To another floor from inside: the screen dark, the rooms given that floor's courses, the player at the entrance.</summary>
        public void ChangeFloor(int floor, int course, bool begin = false)
        {
            if (Floor == 0 || floor < 1 || floor > CampusRules.Floors)
            {
                return;
            }
            if (floor == Floor)
            {
                if (course >= 0)
                {
                    GoToRoom(course, begin);
                }
                return;
            }
            if (_director.SittingNow || PlayerActivityUI.IsPanelOpen)
            {
                Tell("reallife_study_why_busy");
                return;
            }
            _director.StartCoroutine(Guard.Routine(Lift(floor, course, begin)));
        }

        private IEnumerator Lift(int floor, int course, bool begin)
        {
            _lift.Hide("going to another floor");
            yield return UiFader.Fade(0.25f);
            int was = Floor;
            if (Floor > 0)
            {
                Floor = floor;
                Label();
                PlayerController player = PlayerHelper.PlayerController;
                if (player != null)
                {
                    player.ResetNavigation();
                    player.Character.navmeshAgent.Warp(_spawn);
                }
                _logger?.Info("Careers: the player takes the lift from floor " + was + " to floor " + floor + " of the university (at " + Clock() + "); rooms: " + RoomsWords() + ".");
            }
            yield return UiFader.UnFade(0.25f);
            if (Floor > 0)
            {
                Tell(NotificationType.Info, "reallife_campus_note_floor", "floor", Floor.ToString(), "courses", RoomsList());
                if (course >= 0)
                {
                    GoToRoom(course, begin);
                }
            }
        }

        /// <summary>
        /// To a course's classroom from wherever the player is: the floor first if need be, then the room. With begin (the
        /// Campus app's Attend, Sit or Homework), the classroom door's own study panel is opened at once, as a click on the
        /// door opens it, and its class begun for the player (Careers/SchoolDesk.Begin): the player walks to the room's desk
        /// with the game's own routing and sits, the way the game's studying goes, the door's refusals (closed, something
        /// in hand) the game's own. Without (a floor button), a walk to the classroom door, which opens the panel with the
        /// class to begin. Null if under way; else a reason key.
        /// </summary>
        public string GoToRoom(int course, bool begin = false)
        {
            int floor = CampusRules.FloorOf(course), room = CampusRules.RoomOf(course);
            if (floor == 0)
            {
                return "reallife_study_why_no_class";
            }
            CampusBuilding campus = Campus;
            if (Floor == 0 || campus == null || !CareersGame.InsideAt(campus.Street, campus.Number))
            {
                return Enter(floor, course, "a class of " + StudyCatalogue.Find(course).Key, begin);
            }
            if (floor != Floor)
            {
                ChangeFloor(floor, course, begin);
                return null;
            }
            EducationDoorController door = _rooms[room - 1];
            PlayerController player = PlayerHelper.PlayerController;
            if (door == null || player == null)
            {
                _logger?.Info("Careers: the classroom of " + StudyCatalogue.Find(course).Key + " (floor " + floor + ", room " + room + ") has no door to walk to.");
                return "reallife_study_why_game_busy";
            }
            if (begin && _director.BeginHere(course))
            {
                if (PlayerActivityUI.IsPanelOpen)
                {
                    return "reallife_study_why_game_busy";
                }
                _logger?.Info("Careers: the class of " + StudyCatalogue.Find(course).Key + " (floor " + floor + ", room " + room + ") is begun at its desk from the Campus app.");
                door.StartLearning();   // the game's study panel (its own refusals: closed, something in hand), the class pressed for the player
                return null;
            }
            if (!player.ExistsRoute(door, true))
            {
                _logger?.Info("Careers: no way to walk to the classroom of " + StudyCatalogue.Find(course).Key + " (floor " + floor + ", room " + room + ").");
                return "reallife_study_why_game_busy";
            }
            _logger?.Info("Careers: the player walks to floor " + floor + ", room " + room + " for " + StudyCatalogue.Find(course).Key + ".");
            player.SetGoal(door, Guard.Listener(() =>
            {
                try
                {
                    if (Floor == floor && !PlayerActivityUI.IsPanelOpen)
                    {
                        door.StartLearning();   // the game's study panel, with the class (Careers/SchoolDesk)
                    }
                }
                catch (Exception e)
                {
                    _logger?.Error(e);
                }
            }));
            return null;
        }

        /// <summary>The room a classroom door of the floor is, from 1; 0 for none.</summary>
        public int RoomOf(EntityController door)
        {
            for (int i = 0; i < _rooms.Length; i++)
            {
                if (_rooms[i] != null && ReferenceEquals(_rooms[i], door))
                {
                    return i + 1;
                }
            }
            return 0;
        }

        // ---------------------------------------------------------------- the doors, the card, the game's refusal

        // While it is open the building's doors are the game's walk-in kind ("always open"), so its own click goes in.
        private void Doors(bool open)
        {
            CityBuildingController b = _doorsOf;
            if (b == null || b.entranceDoors == null || open == _doorsOpen)
            {
                return;
            }
            if (_doorWas == null)
            {
                _doorWas = new bool[b.entranceDoors.Length];
                for (int i = 0; i < b.entranceDoors.Length; i++)
                {
                    _doorWas[i] = b.entranceDoors[i] != null && b.entranceDoors[i].alwaysOpen;
                }
            }
            for (int i = 0; i < b.entranceDoors.Length; i++)
            {
                if (b.entranceDoors[i] != null)
                {
                    b.entranceDoors[i].alwaysOpen = open || (i < _doorWas.Length && _doorWas[i]);
                }
            }
            _doorsOpen = open;
            _logger?.Info("Careers: the university's doors are " + (open ? "open" : "shut") + " (" + Clock() + ", " + HoursLine(open) + ").");
        }

        /// <summary>The game's card over the building: the university's hours in place of the game's line, and no owners' lines.</summary>
        public void Card(bool force)
        {
            BuildingResume card = _card;
            if (card == null)
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                card = _card = uis != null ? uis.buildingResume : null;   // (kept: the card lives as long as the city)
                if (card == null)
                {
                    return;
                }
            }
            CampusBuilding campus = Campus;
            if (campus == null || campus.Building == null)
            {
                return;
            }
            EntityController target = GameMembers.TargetOf(card);
            if (!force && ReferenceEquals(target, _carded))
            {
                return;
            }
            bool turned = !ReferenceEquals(target, _carded);
            _carded = target;
            if (!ReferenceEquals(target, campus.Building) || !CareersDirector.UniversityOpen || SpecialBuildings.NameFor(campus.Building) == null)
            {
                return;
            }
            try
            {
                bool open = OpenNow();
                if (card.openStateLabel != null)
                {
                    card.openStateLabel.gameObject.SetActive(true);
                    long hours = HoursStamp(open);
                    TMP_Text shown = card.openStateLabel.TextContainer;
                    if (turned || hours != _cardHours || shown == null || !ReferenceEquals(shown.text, _cardText))
                    {
                        // (written again when the card turned to it, the hours changed, or the game wrote its own line over it:
                        // on the hour and when a building changes hands, BuildingResume.UpdateDetails)
                        card.openStateLabel.Prefix = string.Empty;
                        card.openStateLabel.SetData(LanguageChangeEventDataHolder.Create("reallife_campus_card_state", new { state = HoursLine(open) }, "common_closed"));
                        card.openStateLabel.TextContainer.color = open ? WorldPanel.Green() : WorldPanel.Red();
                        _cardHours = hours;
                        _cardText = card.openStateLabel.TextContainer.text;
                    }
                }
                card.rivalBuildingOwner?.gameObject.SetActive(false);
                card.rivalBusinessOwner?.gameObject.SetActive(false);
                if (card.clickToEnterLabel != null)
                {
                    card.clickToEnterLabel.gameObject.SetActive(open && !CityMap.IsOpen && !PlayerHelper.IsUsingVehicle);
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        private void CardNow() => Card(true);

        // The game's own refusal at a door: when it is the university's, said in the log and to the player with the hours.
        private void Noticed(NotificationType type, string key, Dictionary<string, string> data, float seconds, string duplicate, Action click, bool sound, bool track)
        {
            using var hotSpot = HotSpots.Time(HotNoticed);
            if (key != "buildingmanager_notification_business_closed" && key != "buildingmanager_notification_private_property" && key != "educationdoorcontroller_closed")
            {
                return;
            }
            try
            {
                CampusBuilding campus = Campus;
                if (campus == null || !campus.Usable || !Available)
                {
                    return;
                }
                bool inside = CareersGame.InsideAt(campus.Street, campus.Number);
                Vector3 offset = campus.Door - PlayerHelper.GetPosition();
                if (!inside && (BuildingManager.IsInsideBuilding || new Vector2(offset.x, offset.z).magnitude > WatchReach))
                {
                    return;
                }
                _logger?.Info("Careers: the game refused the player at the university (" + key + ", at " + Clock() + "): " + (inside ? "a classroom door while the classrooms are shut"
                              : "its own door: the building holds no business of the game's, so the game's rule is that it is shut") + "; the university is " + (OpenNow() ? "open" : "closed")
                              + " (" + HoursLine(OpenNow()) + ").");
                if (!OpenNow())
                {
                    Tell("reallife_campus_why_closed");
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        private static string Clock()
        {
            GameInstance save = SaveGameManager.Current;
            return save == null ? "?" : "day " + save.Day + " (" + TimeHelper.GetDayOfWeek(Math.Max(1, save.Day)) + ") " + save.Hour.ToString("00") + ":" + Mathf.FloorToInt(save.Minute).ToString("00");
        }

        /// <summary>Everything put back: the doors' flags, the building's size, the panels.</summary>
        public void Gone(string why)
        {
            _watching = false;
            Restore(why);
            Doors(false);
            _doorsOf = null;
            _doorWas = null;
            _door.Hide(why);
            _lift.Hide(why);
        }

        /// <summary>The city goes, or the part is switched off.</summary>
        public void Dispose()
        {
            _marks.Clear();
            Gone("the university's part went");
            GlobalEvents.onEnterBuilding -= _onEnter;
            GlobalEvents.onExitBuilding -= _onExit;
            Notifications.onShow = (Action<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool>)Delegate.Remove(Notifications.onShow, _onNotice);
            Floor = 0;
            _built++;
        }
    }
}
