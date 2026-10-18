using System;
using System.Collections.Generic;
using Helpers;
using PlayerActivity;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// Attending in place: where nothing in the world holds a class (the university's sign or door without its floors, a
    /// ward), the game's panel over the place with one button, "Attend {course}" or "Sit the {course} exam", on the game's
    /// interact key (<see cref="DoorPanel"/>); while it is up the key is the class's (<see cref="TakesInteractKey"/>). In a
    /// Going Public session the school's class is offered so too (ClassPlayRules.SchoolInPlace). Also the watch on the
    /// game's study panel at the school's desks and the university's classroom doors, and the notice as a class's hour comes.
    /// The cost: once a second the courses looked at; per frame, while the university's floors are there or the player is
    /// in the school on a course there, a reference comparison and a flag test, nothing allocated.
    /// </summary>
    public sealed partial class CareersDirector
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("careers_study", "CareersDirector", typeof(CareersDirector));   // its LateUpdate's guard (Core/Guard)

        private DoorPanel _classDoor;
        private Action _onClassDoor;
        private int _classShown = -1;      // course * 2 + (exam ? 1 : 0) the panel is up for, or -1
        private int _classOffered = -1;    // the same, offered this second
        private int _classDue = -1;        // day * 100 + course of the last class whose start was looked at
        private readonly Dictionary<int, object> _courseArgs = new Dictionary<int, object>(4);
        private Func<string> _classWhat;   // the panel's words for the log, made once (ClassWhat)...
        private bool _classWhatExam, _classWhatSign;
        private string _classWhatKey;      // ...from what it is up for this second

        private SchoolDesk _desk;          // a class at the school's desk, from its button until it is over
        private bool _deskWatch;           // inside the school, on a course there: the game's study panel is looked at each frame
        private IPlayerActivity _deskSeen; // the game's activity last looked at
        private PlayerActivityUI _activityUi;   // the game's activity panel: ActivityUi()
        private bool _roomDesksOff;        // a classroom door's panel could not be taken: the university's classes are attended in place

        /// <summary>True while the class panel is up, and in the frame its button ran.</summary>
        public static bool TakesInteractKey => Instance != null && (Instance._classDoor != null && Instance._classDoor.TakesKey || Instance._interior != null && Instance._interior.TakesKey);

        private bool DeskBusy => (_desk != null && _desk.Open) || (_computer != null && _computer.Open);

        private PlayerActivityUI ActivityUi()
        {
            if (_activityUi == null)
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                _activityUi = uis != null ? uis.playerActivityUI : null;
            }
            return _activityUi;
        }

        // Once a second, from the timer.
        private void ClassPrompt()
        {
            if (_classDoor == null)
            {
                _classDoor = new DoorPanel(Logger, "Careers check (class panel)");
                _onClassDoor = _classDoor.Press(UseClassDoor);
            }
            DeskTick();
            PlayerController player = PlayerHelper.PlayerController;
            bool busy = !_studyOn || player == null || PlayerHelper.IsUsingVehicle || ThirdPersonCamera.IsUiOpen() || (_skip != null && _skip.Open) || DeskBusy
                        || PlayerActivityUI.IsPanelOpen || JobDirector.TakesInteractKey;
            int offer = -1;
            bool session = GoingPublic.InSession;
            for (int i = 0; !busy && i < _study.Courses.Count && offer < 0; i++)
            {
                Enrolment e = _study.Courses[i];
                if (e == null || e.Def == null)
                {
                    continue;
                }
                bool online = ClassPlayRules.HeldOnline(e.Def, (StudyMode)e.Mode);
                ClassPlayRules.InPlace(AtTheSchool(e, false), online, AtTheSchool(e, true), InRooms(e), session, out bool classHere, out bool examHere);
                if (!classHere && !examHere)
                {
                    continue;   // at the school's desks, the home computer or the university's classrooms: no panel in place
                }
                string classWhy = classHere ? CanAttend(e, out _) : online ? "reallife_study_why_no_computer" : "reallife_study_why_not_school";
                string examWhy = examHere ? CanSit(e) : "reallife_study_why_exam_school";
                StudyPrompt prompt = StudyRules.Prompt(classWhy, examWhy, false);
                if (prompt != StudyPrompt.None)
                {
                    offer = e.Def.Id * 2 + (prompt == StudyPrompt.Exam ? 1 : 0);
                }
            }
            _classOffered = offer;
            if (offer < 0)
            {
                if (_classShown >= 0)
                {
                    _classShown = -1;
                    _classDoor.Hide("no class to join here now");
                }
                return;
            }
            int course = offer / 2;
            bool exam = (offer & 1) == 1;
            Enrolment live = _study.Live(course);
            CourseDef c = live != null ? live.Def : null;
            if (c == null)
            {
                return;
            }
            if (offer != _classShown)
            {
                _classShown = offer;
                WorldPanel panel = _classDoor.Panel;
                if (exam)
                {
                    panel.Lines(CareersText.Course(c.Id), CareersText.T(live.Stage == (int)EnrolStage.Retake ? "reallife_study_skip_retake" : "reallife_study_skip_exam", "pass", c.Pass.ToString()),
                        null, WorldPanel.White());
                }
                else
                {
                    CanAttend(live, out int minutes);
                    panel.Lines(CareersText.Course(c.Id), CareersText.T("reallife_study_skip_class", "number", (live.Seen + 1).ToString(), "of", c.Sessions.ToString()),
                        CareersText.T("reallife_study_skip_until", "time", CareersText.Minutes(minutes)), WorldPanel.White());
                }
                panel.Button(0, exam ? "reallife_study_prompt_exam" : "reallife_study_prompt_attend", CourseArgs(c.Id), _onClassDoor, true, true);
                panel.Button(1, null, null, false, false);
                panel.Button(2, null, null, false, false);
                panel.Button(3, null, null, false, false);
            }
            Vector3 standing = player.transform.position;
            bool atSign = c.Campus == Campus.University && (StudyMode)live.Mode != StudyMode.Distance && _campus != null && _campus.Usable && !BuildingManager.IsInsideBuilding;
            _classWhatExam = exam;
            _classWhatKey = c.Key;
            _classWhatSign = atSign;
            _classDoor.Show(atSign ? _campus.Front : standing, standing, atSign ? 2.3f : 2.1f, _classWhat ?? (_classWhat = ClassWhat));
        }

        // What the class panel is up for, for its log line (made only when it is written).
        private string ClassWhat()
        {
            return (_classWhatExam ? "exam of " : "class of ") + _classWhatKey
                + (_classWhatSign ? " at the university's " + (_campus != null && _campus.HasSign ? "sign" : "door") : " where the player is");
        }

        // True if this course's class (or exam) is held at the game's school, where its desks offer it instead of a panel.
        private static bool AtTheSchool(Enrolment e, bool exam)
        {
            CourseDef c = e.Def;
            bool home = (StudyMode)e.Mode == StudyMode.Distance || c.Campus == Campus.Online;
            return exam ? home || c.Campus == Campus.School : !home && c.Campus == Campus.School;
        }

        private object CourseArgs(int course)
        {
            if (!_courseArgs.TryGetValue(course, out object args))
            {
                args = new { course = CareersText.Course(course) };   // the game's button fills {course} from it
                _courseArgs[course] = args;
            }
            return args;
        }

        private void UseClassDoor()
        {
            int offer = _classOffered;
            if (offer < 0 || ThirdPersonCamera.IsUiOpen())
            {
                return;
            }
            try
            {
                _classDoor.Hide("the class was joined");
                _classShown = -1;
                string why = (offer & 1) == 1 ? Sit(offer / 2) : Attend(offer / 2);
                if (why != null)
                {
                    Tell(NotificationType.Warning, why, null, TabStudying, CampusApp.CourseRow + offer / 2);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void ClassPromptGone()
        {
            _classDoor?.Hide("the director went with the city");
            _classShown = -1;
            _classOffered = -1;
            _deskWatch = false;
            if (_desk != null && _desk.Open)
            {
                _desk.Finish();
            }
            _desk = null;
            if (_computer != null && _computer.Open)
            {
                _computer.Finish();
            }
            _computer = null;
        }

        // Once a second: the game's study panel is watched while the player is inside the school on a course there; in a
        // Going Public session, with the university's floors there, the click on its classroom doors is the mod's (DeskGame).
        private void DeskTick()
        {
            if (_studyOn && _interior != null && GoingPublic.InSession)
            {
                DeskGame.Install();   // once, then a null check; it answers only over a classroom door of the player's floor
            }
            bool watch = _studyOn && _interior != null && _interior.Floor > 0;   // the university's classrooms: always, so no course of the game's opens there
            if (!watch && _studyOn && CareersGame.InSchool())
            {
                for (int i = 0; i < _study.Courses.Count && !watch; i++)
                {
                    Enrolment e = _study.Courses[i];
                    watch = e != null && e.Def != null && (AtTheSchool(e, false) && e.Stage == (int)EnrolStage.Studying && e.Def.Timetabled
                                                           || AtTheSchool(e, true) && (e.Stage == (int)EnrolStage.Exam || e.Stage == (int)EnrolStage.Retake));
                }
            }
            _deskWatch = watch && ActivityUi() != null;
            if (_desk != null && _desk.Open && (_activityUi == null || !ReferenceEquals(_activityUi.GetCurrentActivity, _desk)))
            {
                _desk.Finish();   // the game's panel let it go: count the minutes so far and stand up
            }
            if (_desk != null && !_desk.Open)
            {
                _desk = null;
            }
            if (_computer != null && _computer.Open && !DeskGame.Shows(_computer))
            {
                _computer.Finish();   // the same at the computer at home
            }
            if (_computer != null && !_computer.Open)
            {
                _computer = null;
            }
            if (_sitting != null && !SittingNow && !(_deskWork != null && _deskWork.Open))
            {
                // Nothing on the game's panel holds it any more: let go, so that it never leaves every class and day "under way".
                Logger?.Info("Careers: a " + _sitting.Kind + " sitting was left with no desk, computer or panel of its own; let go, nothing counted.");
                _sitting = null;
                CareersGame.Tiring(false);   // its energy spent no more, if it had begun
            }
        }

        private static readonly int HotLateUpdate = HotSpots.Slot("Careers: CareersDirector.LateUpdate");
        private bool _watchOff;   // the card and the desk watch below stood down: this game lacks something they name (Core/GameMembers)

        // The frame's work is a method of its own, so that a game lacking something it names fails inside the try:
        // said once, and only the card over the university and the watch on the school's desks stop. The director runs on.
        private void LateUpdate()
        {
            using var hotSpot = HotSpots.Time(HotLateUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // switched off after its faults (Core/Guard)
                return;
            }
            if (_watchOff)
            {
                return;
            }
            try
            {
                OnLateUpdate();
            }
            catch (Exception e) when (Guard.StandsDown(e, "the university's building card and the watch on its desks"))
            {
                _watchOff = true;
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnLateUpdate()
        {
            _interior?.Card(false);   // one reference comparison: the game's card over the university
            bool rooms = _interior != null && _interior.Floor > 0;   // from the frame the player is on a floor: no course of the game's opens there
            if (!_deskWatch && !rooms)
            {
                return;
            }
            if (ActivityUi() == null)
            {
                return;
            }
            IPlayerActivity now = _activityUi.GetCurrentActivity;
            if (ReferenceEquals(now, _deskSeen))
            {
                return;
            }
            _deskSeen = now;
            if (now is StudyActivity game && game.GetState() == PlayerActivityState.NotStarted)
            {
                bool onFloor = _interior != null && _interior.Floor > 0;
                try
                {
                    SchoolDesk.DeskOffer offer;
                    int room = 0;
                    bool begin;
                    if (onFloor)
                    {
                        room = _interior.RoomOf(SchoolDesk.DoorOf(game));
                        offer = room > 0 && !_roomDesksOff ? RoomOffer(room) : null;
                        begin = offer != null && TakeBegin(offer.Course, offer);
                        Logger?.Info("Careers: a classroom door of the university, floor " + _interior.Floor + " room " + room + ": " + (offer == null ? "not one of the floor's five rooms"
                                     : (offer.Course >= 0 ? StudyCatalogue.Find(offer.Course).Key : "no course")
                                       + (offer.Can ? ", " + Words(offer.Kind) + " can be begun" + (begin ? ", begun for the player (the Campus app)." : ".")
                                           : ", nothing to begin now (" + offer.Key + ").")));
                    }
                    else
                    {
                        offer = DeskOfferNow();
                        begin = TakeBegin(-1, offer);
                    }
                    SchoolDesk desk = offer != null ? SchoolDesk.Take(_activityUi, game, this, offer) : null;
                    if (desk != null)
                    {
                        _deskSeen = desk;
                        _desk = desk;
                        if (begin)
                        {
                            desk.Begin();
                        }
                    }
                    else if (onFloor)
                    {
                        RoomPanelOff(game, room);
                    }
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                    if (onFloor)
                    {
                        RoomPanelOff(game, 0);
                    }
                }
            }
        }

        // A classroom door of the university whose panel could not be the room's course's: the game's own course is never
        // offered there. Its panel is closed at once, and from now on the university's classes are attended with the Attend
        // button where the player stands (InRooms is false), as they are when the floors are not there.
        private void RoomPanelOff(StudyActivity game, int room)
        {
            try
            {
                game.ChangeState(PlayerActivityState.Finished);   // the game's panel closes on its next frame, nothing studied
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (!_roomDesksOff)
            {
                _roomDesksOff = true;
                Logger?.Warn("Careers: the classroom door of floor " + (_interior != null ? _interior.Floor : 0) + " room " + room + " could not show its course on the game's study panel; "
                             + "the game's panel there is closed, and the university's classes are attended with the Attend button where the player stands.");
                Tell(NotificationType.Info, "reallife_campus_note_room_panel", null, TabStudying, null);
            }
        }

        // What a desk offers now: the class that can be joined, else the exam that can be sat, else the next class (greyed).
        private SchoolDesk.DeskOffer DeskOfferNow()
        {
            SchoolDesk.DeskOffer next = null;
            int nextAt = int.MaxValue;
            for (int i = 0; i < _study.Courses.Count; i++)
            {
                Enrolment e = _study.Courses[i];
                CourseDef c = e != null ? e.Def : null;
                if (c == null)
                {
                    continue;
                }
                if (AtTheSchool(e, false) && CanAttend(e, out _) == null)
                {
                    return new SchoolDesk.DeskOffer { Course = c.Id, Can = true, Key = "reallife_study_prompt_attend", Arguments = CourseArgs(c.Id) };
                }
                if (AtTheSchool(e, true) && CanSit(e) == null)
                {
                    return new SchoolDesk.DeskOffer { Course = c.Id, Kind = SittingKind.Exam, Can = true, Key = "reallife_study_prompt_exam", Arguments = CourseArgs(c.Id) };
                }
                if (AtTheSchool(e, false) && e.Stage == (int)EnrolStage.Studying && c.Timetabled)
                {
                    int at = StudyRules.NextClass(e, Save.Day, CareersGame.MinuteOfDay, SittingClass);
                    if (at >= 0 && at < nextAt)
                    {
                        nextAt = at;
                        next = new SchoolDesk.DeskOffer
                        {
                            Course = c.Id, Can = false, Key = "reallife_study_desk_next",
                            Arguments = new { course = CareersText.Course(c.Id), day = CareersText.Day(at / (24 * 60)), hour = CareersText.Hour(at % (24 * 60) / 60) }
                        };
                    }
                }
            }
            return next ?? new SchoolDesk.DeskOffer { Course = -1, Can = false, Key = "reallife_study_why_no_class", Arguments = null };
        }

        /// <summary>
        /// A sitting begun at a desk of the school or the university (Careers/SchoolDesk): the Campus app's checks, the sitting
        /// made for the desk's own activity. Null with the sitting, or the reason key.
        /// </summary>
        internal string DeskSitting(int course, SittingKind kind, out Sitting sitting)
        {
            sitting = null;
            Enrolment e = _studyOn ? _study.Live(course) : null;
            CourseDef c = e != null ? e.Def : null;
            if (c == null)
            {
                return "reallife_study_why_no_class";
            }
            if ((_skip != null && _skip.Open) || DeskBusy)
            {
                return "reallife_study_why_busy";
            }
            if (CareersGame.Energy < StudyCatalogue.TiredEnergy)
            {
                return "reallife_study_why_tired";
            }
            if (kind == SittingKind.Class)
            {
                string why = CanAttend(e, out int minutes);
                if (why != null)
                {
                    return why;
                }
                sitting = ClassSitting(e, minutes);
            }
            else if (kind == SittingKind.Homework)
            {
                string why = CanHomework(e);
                if (why != null)
                {
                    return why;
                }
                sitting = HomeworkSitting(e);
            }
            else
            {
                string why = CanSit(e);
                if (why != null)
                {
                    return why;
                }
                int fee = e.Stage == (int)EnrolStage.Retake ? Prices.At(StudyRules.RetakeFee(c)) : 0;
                if (fee > 0 && CareersGame.Cash < fee)
                {
                    return "reallife_study_why_money";
                }
                sitting = ExamSitting(e);
                sitting.Fee = fee;   // charged as the player sits down: a walk called off costs nothing
            }
            _sitting = sitting;
            Logger?.Info("Careers: " + Words(kind) + " of " + c.Key + " begun at a desk of the " + (_interior != null && _interior.Floor > 0 ? "university" : "school") + " (the game's study panel).");
            return null;
        }

        private static string Words(SittingKind kind)
        {
            switch (kind)
            {
                case SittingKind.Exam: return "the exam";
                case SittingKind.Homework: return "a piece of homework";
                case SittingKind.Work: return "a working day";
                case SittingKind.Overtime: return "overtime";
                default: return "a class";
            }
        }

        // "Your class starts now", once a class (StudyRules.StartsNow, kept in the save), when the player is not inside the place
        // that holds it, with how long the door stays open (StudyCatalogue.LateMinutes).
        private void ClassStarts(Enrolment e, CourseDef c, StudyMode mode, int day, int hour)
        {
            if (!StudyRules.StartsNow(e, day, hour * 60, SittingClass, e.StartTold))
            {
                return;
            }
            e.StartTold = day * 24 * 60 + hour * 60;
            var data = Data("course", CareersText.Course(c.Id), "place", PlaceOf(c, mode), "late", CareersText.Minutes(StudyCatalogue.LateMinutes));
            Tell(NotificationType.Info, "reallife_study_note_class_starts", data, TabStudying, CampusApp.CourseRow + c.Id);
        }

        private void ClassDue(Enrolment e, int day, int hour)
        {
            CourseDef c = e.Def;
            StudyMode mode = (StudyMode)e.Mode;
            if (c == null || !c.Timetabled || !StudyRules.On(StudyRules.Days(c, mode), day) || StudyRules.Hour(c, mode) != hour || e.SettledDay >= day || day < e.Start
                || InClass(c.Id))
            {
                return;
            }
            int key = day * 100 + c.Id;
            if (_classDue == key)
            {
                return;
            }
            _classDue = key;
            string why = CanAttend(e, out int minutes);
            PlayerController player = PlayerHelper.PlayerController;
            string where;
            if (c.Campus == Campus.University && mode != StudyMode.Distance)
            {
                if (_campus == null || !_campus.Usable || player == null)
                {
                    where = _campus == null ? "the university has not been chosen yet" : !_campus.Usable ? "the university has no place to attend in person" : "no player";
                }
                else
                {
                    Vector3 offset = _campus.Front - player.transform.position;
                    where = "the player is " + new Vector3(offset.x, 0f, offset.z).magnitude.ToString("0") + " m from the university's " + (_campus.HasSign ? "sign" : "door")
                            + " at " + _campus.Name + (BuildingManager.IsInsideBuilding
                                ? " (inside a building" + (CareersGame.InsideAt(_campus.Street, _campus.Number) ? ": the university's)" : ")") : "");
                }
            }
            else
            {
                where = "held " + c.Campus + (mode == StudyMode.Distance ? " (from home)" : string.Empty) + "; the player is "
                        + (CareersGame.InSchool() ? "inside the school" : BuildingManager.IsInsideBuilding ? "inside a building" : "out of doors");
            }
            Logger?.Info("Careers: class due for " + c.Key + " at " + hour.ToString("00") + ":00 on day " + day + "; " + where + "; "
                         + (InRooms(e) ? "held on floor " + CampusRules.FloorOf(c.Id) + ", room " + CampusRules.RoomOf(c.Id)
                             + " (the player is " + (_interior.Floor > 0 ? "on floor " + _interior.Floor : "not on a floor")
                            + ", the university " + (_interior.OpenNow() ? "open" : "closed") + "); " : string.Empty)
                         + (why == null ? "it can be joined now ("
                             + (AtTheSchool(e, false) && !ClassPlayRules.SchoolInPlace(GoingPublic.InSession) ? "Study at a desk" : InRooms(e) ? "the classroom's door"
                                 : ClassPlayRules.HeldOnline(c, mode) ? "the computer at home" : "the panel where the player stands")
                             + ")." : "it cannot be joined now: " + why + "."));
            int notice = StudyRules.ClassNotice(why);
            if (notice == 0)
            {
                return;
            }
            string course = CareersText.Course(c.Id);
            if (why != null && ClassPlayRules.HeldOnline(c, mode) && CareersBridges.HomeNetNow() == HomeNet.Outage)
            {
                // the provider's outage is on as the class begins: it will be excused (GoneBy), said now and not again as it goes by
                _excuseTold = key;
                Tell(NotificationType.Info, "reallife_study_note_class_outage", Data("course", course), TabStudying, CampusApp.CourseRow + c.Id);
                return;
            }
            if (notice == 1)
            {
                if (InRooms(e))
                {
                    Tell(NotificationType.Info, "reallife_campus_note_class_room", Data("course", course, "floor", CampusRules.FloorOf(c.Id).ToString(), "room", CampusRules.RoomOf(c.Id).ToString()),
                        TabStudying, CampusApp.CourseRow + c.Id);
                    return;
                }
                Tell(NotificationType.Info, AtTheSchool(e, false) && !ClassPlayRules.SchoolInPlace(GoingPublic.InSession) ? "reallife_study_note_class_desk"
                    : ClassPlayRules.HeldOnline(c, mode) ? "reallife_study_note_class_computer" : "reallife_study_note_class_here",
                    Data("course", course, "key", Keys.Label(KeyAction.Interact)), TabStudying, CampusApp.CourseRow + c.Id);
                return;
            }
            if (Settings.CareersClassStart && StudyRules.Elsewhere(why))
            {
                ClassStarts(e, c, mode, day, hour);
                return;
            }
            if (minutes <= 0)
            {
                StudyRules.Joinable(c, mode, day, hour * 60, out minutes);   // how long it runs, though it cannot be joined from here
            }
            var data = Data("course", course, "why", CareersText.T(why), "place", PlaceOf(c, mode));
            data["time"] = CareersText.Minutes(Math.Max(0, minutes));
            Tell(NotificationType.Warning, why == "reallife_study_why_not_school" ? "reallife_study_note_class_school" : "reallife_study_note_class_now", data,
                TabStudying, CampusApp.CourseRow + c.Id);
        }
    }
}
