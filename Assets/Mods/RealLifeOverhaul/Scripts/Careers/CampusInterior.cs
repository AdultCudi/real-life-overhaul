using System;
using System.Collections.Generic;
using BAModAPI;
using Blueprints;
using Buildings;
using BusinessLayoutSets;
using Helpers;
using Localizor;
using PlayerActivity;
using RealLifeOverhaul.Floors;
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
    /// floors of its building, as the floors core (Floors/FloorsDirector) holds it: the building read as D1 while the game loads
    /// it, a registration of the mod's own on the floor, kept nowhere, the door's and lift's panels, the lift. Each floor's five
    /// rooms hold five of the university's courses. The cost: per frame (CareersDirector.LateUpdate) the building card's target
    /// compared; once a second its hours and the marks on a floor; the floor's furniture laid by the core over four frames.
    /// </summary>
    internal sealed partial class CampusInterior
    {
        public const string SchoolLayout = "GarmentDistrictSchool";
        private const string SizeD = "ba:buildingsize_d";
        private const int SchoolVersion = 1;
        private const float WatchReach = 12f;   // metres: from its door the university takes the player in, and says the game's refusals there

        private readonly CareersDirector _director;
        private readonly IModLogger _logger;
        private readonly Dictionary<long, object> _args = new Dictionary<long, object>(16);   // a floor button's arguments by class, floor and course
        private BuildingResume _card;                 // the game's building card, kept until the city goes
        private long _cardHours = long.MinValue;      // what the card's hours were written from (HoursStamp)...
        private string _cardText;                     // ...and the label's text just after: written again only if either has changed
        private readonly EducationDoorController[] _rooms = new EducationDoorController[CampusRules.RoomsPerFloor];
        private readonly Transform[] _chairs = new Transform[CampusRules.RoomsPerFloor];   // each room's chair, found as the game's door finds it
        private readonly GameObject[] _signs = new GameObject[CampusRules.RoomsPerFloor];
        private readonly CampusMarks _marks;
        private readonly CampusFloors _place;   // the university's classrooms on its floors, as the floors core lays them
        private static Material _signLook;    // the game's font's material, drawn from the front only (one for every sign)
        private static TMP_FontAsset _signFont;

        private BusinessLayoutSet _set;
        private bool _checked, _available;
        private CampusHost _host;      // its building as the floors core holds it, while the floors are there
        private FloorVisit _visit;     // the player on one of its floors
        private int _goCourse = -1;    // a course's classroom to go to once on its floor...
        private bool _goBegin;         // ...and, once there, its class (or exam, or homework) begun at its desk for the player
        private int _layCourse = -1;   // the same, taken for the floor being laid
        private bool _layBegin;
        private int _toldClosed = -1;
        private EntityController _carded;
        private Action<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool> _onNotice, _noticeSeen;

        public CampusInterior(CareersDirector director, IModLogger logger)
        {
            _director = director;
            _logger = logger;
            _onNotice = Guard.Wrap<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool>(Noticed);
            _marks = new CampusMarks(logger);
            _place = new CampusFloors(this);
        }

        /// <summary>The floor the player is on, from 1; 0 when not inside the university's floors.</summary>
        public int Floor { get; private set; }

        /// <summary>True while a panel of the floors' is up (the university's door or lift among them), and in the frame its button ran.</summary>
        public bool TakesKey => FloorsDirector.TakesInteractKey;

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

        private const string SchoolDoor = "ba:itemname_schooldoor";

        private static bool IsDoor(string itemName) => itemName == SchoolDoor;

        /// <summary>Once a second: the university's building handed to the floors core while its floors are there, its card and the game's refusals.</summary>
        public void Tick()
        {
            EventHold.Keep(ref Notifications.onShow, _onNotice, ref _noticeSeen);
            CampusBuilding campus = Campus;
            if (!Available)
            {
                Gone("no university's floors");
                return;
            }
            if (_host == null || !ReferenceEquals(_host.Building, campus.Building))
            {
                Hold(campus);
            }
            _host.Name = CareersText.T("reallife_study_campus_university");
            _host.Door = campus.Door;
            CardNow();
        }

        // Its building to the floors core: a new holding for a new building, the old one let go first.
        private void Hold(CampusBuilding campus)
        {
            if (_host != null)
            {
                FloorsDirector.Let(_host, "the university's building changed");
            }
            _host = new CampusHost(this)
            {
                Building = campus.Building,
                Door = campus.Door,
                Name = CareersText.T("reallife_study_campus_university"),
                Rank = FloorsRules.RankCampus
            };
            _host.Own.Add(_place);
            FloorsDirector.Hold(_host);
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

        /// <summary>To another floor from inside: the floors core's lift (the screen dark, the rooms given that floor's courses, the player at the entrance).</summary>
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
            _goCourse = course;
            _goBegin = begin && course >= 0;
            string why = FloorsDirector.Lift(floor, "the Campus app");
            if (why != null)
            {
                _goCourse = -1;
                _goBegin = false;
                _logger?.Info("Careers: the lift to floor " + floor + " of the university is not taken (" + why + ").");
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

        /// <summary>Its building let go by the floors core: what was borrowed of it put back, its doors' flags and the panels as found.</summary>
        public void Gone(string why)
        {
            if (_host != null)
            {
                FloorsDirector.Let(_host, why);
                _host = null;
            }
            _goCourse = -1;
            _goBegin = false;
        }

        /// <summary>The city goes, or the part is switched off.</summary>
        public void Dispose()
        {
            _marks.Clear();
            Gone("the university's part went");
            Notifications.onShow = (Action<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool>)Delegate.Remove(Notifications.onShow, _onNotice);
            Floor = 0;
            _visit = null;
        }
    }
}
