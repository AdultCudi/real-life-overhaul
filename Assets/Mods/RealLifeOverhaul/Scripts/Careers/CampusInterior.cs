using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using Blueprints;
using Buildings;
using BusinessLayoutSets;
using Helpers;
using Localizor;
using PlayerActivity;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI;
using UI.InGameUI;
using UI.Notification;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// Inside Hudson City University: the game's own school interior (structure D1, layout GarmentDistrictSchool) on several
    /// floors, the building read as D1 while the game loads it and given a registration of the mod's own, kept nowhere. Each
    /// floor's five rooms hold five of the university's courses. The cost: per frame (CareersDirector.LateUpdate) the building
    /// card's target compared, near the open door one flag; once a second the panels and marks; the furniture over four frames.
    /// </summary>
    internal sealed partial class CampusInterior
    {
        public const string SchoolLayout = "GarmentDistrictSchool";
        private const string SizeD = "ba:buildingsize_d";
        private const int SchoolVersion = 1;
        private const float DoorReach = 3.2f;   // metres: the door's panel shows within
        private const float LiftReach = 2.6f;   // metres: the lift's panel shows within
        private const float WatchReach = 12f;   // metres: the game's own click on the building is watched for within
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
            _onEnter = Guard.Wrap<Address>(Entered);
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
                    return false;   // at the game's own school its classes are joined at the door: the school inside is the game's
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
                    : "the game's school layout is not as this version expects (" + (_set == null ? "not found" : doors + " classroom doors")
                      + "); classes are attended at the sign by the door instead."));
            }
            catch (Exception e)
            {
                _available = false;
                _logger?.Error(e);
                _logger?.Warn("Careers: the university's floors could not be read after the error above; classes are attended at the sign by the door instead.");
            }
        }

        private static bool IsDoor(string itemName) => itemName == "ba:itemname_schooldoor";

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

        private void Tell(string key)
        {
            _director.Tell(NotificationType.Warning, key, CareersDirector.Data("hours", HoursLine(false)), CareersDirector.TabStudying, null);
        }

        private static readonly int CullId = Shader.PropertyToID("_CullMode");

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
        /// To a course's classroom from wherever the player is, the floor first; with <paramref name="begin"/> its panel opened and
        /// its class begun at once, else a walk to its door. Null if under way, else a reason key.
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
                return _director.OpenDesk(door, room, true);   // the door's panel, the class pressed for the player
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
                    string why = Floor == floor && !PlayerActivityUI.IsPanelOpen ? _director.OpenDesk(door, room, false) : null;   // the door's panel, with the class
                    if (why != null)
                    {
                        Tell(why);
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

        private static string Clock()
        {
            GameInstance save = SaveGameManager.Current;
            return save == null ? "?"
                : "day " + save.Day + " (" + TimeHelper.GetDayOfWeek(Math.Max(1, save.Day)) + ") " + save.Hour.ToString("00") + ":" + Mathf.FloorToInt(save.Minute).ToString("00");
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
