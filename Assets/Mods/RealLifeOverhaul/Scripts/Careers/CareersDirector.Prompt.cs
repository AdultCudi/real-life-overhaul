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
    /// Attending in place, at every campus.
    ///
    ///   the school      Manhattan Business School's own study desks: press Study at a desk, as for the game's courses,
    ///                   and the game's panel for that desk has one more button, "Attend {course}" or "Sit the {course}
    ///                   exam" (Careers/SchoolDesk). The game's own courses at the desk are as they were.
    ///   elsewhere       at the university's sign or door, on a ward of a hospital or clinic, at home for an online class:
    ///                   the game's own panel (<see cref="DoorPanel"/>) over the place with one button, "Attend {course}"
    ///                   or "Sit the {course} exam", on the game's interact key. While it is up the key is the class's
    ///                   (<see cref="TakesInteractKey"/>, read by World/NearbyInteraction and the utilities desk), so it
    ///                   neither enters the building nor opens another desk.
    ///   the Campus app  its Attend and Sit buttons do the same from the phone, as before.
    ///
    /// When a class's hour comes, a notice: where the player is right, how to join it (press the key; at the school,
    /// press Study at a desk); where not, why, where it is held and how long it runs. One line goes to the log with how far
    /// the player is from the university's door, so that "no prompt" can be read from Player.log. Looked at once a
    /// second; per frame one flag test, and one reference comparison while the player is inside the school on a course there
    /// (the director is never disabled: the game's activity panel needs it running for the mod's sittings, Jobs/ShiftSkip).
    /// </summary>
    public sealed partial class CareersDirector
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("careers_study", "CareersDirector", typeof(CareersDirector));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private DoorPanel _classDoor;
        private Action _onClassDoor;
        private int _classShown = -1;      // course * 2 + (exam ? 1 : 0) the panel is up for, or -1
        private int _classOffered = -1;    // the same, offered this second
        private int _classDue = -1;        // day * 100 + course of the last class whose start was looked at
        private readonly Dictionary<int, object> _courseArgs = new Dictionary<int, object>(4);

        private SchoolDesk _desk;          // a class at the school's desk, from its button until it is over
        private bool _deskWatch;           // inside the school, on a course there: the game's study panel is looked at each frame
        private IPlayerActivity _deskSeen; // the game's activity last looked at
        private PlayerActivityUI _activityUi;

        /// <summary>True while the class panel is up, and in the frame its button ran.</summary>
        public static bool TakesInteractKey => Instance != null && (Instance._classDoor != null && Instance._classDoor.TakesKey || Instance._interior != null && Instance._interior.TakesKey);

        private bool DeskBusy => _desk != null && _desk.Open;

        // ---------------------------------------------------------------- the panel in place (not the school)

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
            for (int i = 0; !busy && i < _study.Courses.Count && offer < 0; i++)
            {
                Enrolment e = _study.Courses[i];
                if (e == null || e.Def == null || AtTheSchool(e, false) && AtTheSchool(e, true) || InRooms(e))
                {
                    continue;   // (at the school's desks, or in the university's classrooms: no panel in place)
                }
                string classWhy = AtTheSchool(e, false) ? "reallife_study_why_not_school" : CanAttend(e, out _);
                string examWhy = AtTheSchool(e, true) ? "reallife_study_why_exam_school" : CanSit(e);
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
            _classDoor.Show(atSign ? _campus.Front : standing, standing, atSign ? 2.3f : 2.1f,
                () => (exam ? "exam of " : "class of ") + c.Key + (atSign ? " at the university's " + (_campus.HasSign ? "sign" : "door") : " where the player is"));
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
                args = new { course = CareersText.Course(course) };   // (the game's button fills {course} from it)
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
        }

        // ---------------------------------------------------------------- the school's desks

        // Once a second: the game's study panel is watched while the player is inside the school on a course there.
        private void DeskTick()
        {
            bool watch = _studyOn && _interior != null && _interior.Floor > 0;   // (the university's classrooms: always, so no course of the game's opens there)
            if (!watch && _studyOn && CareersGame.InSchool())
            {
                for (int i = 0; i < _study.Courses.Count && !watch; i++)
                {
                    Enrolment e = _study.Courses[i];
                    watch = e != null && e.Def != null && (AtTheSchool(e, false) && e.Stage == (int)EnrolStage.Studying && e.Def.Timetabled
                                                           || AtTheSchool(e, true) && (e.Stage == (int)EnrolStage.Exam || e.Stage == (int)EnrolStage.Retake));
                }
            }
            if (watch && _activityUi == null)
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                _activityUi = uis != null ? uis.playerActivityUI : null;
            }
            _deskWatch = watch && _activityUi != null;
            if (_desk != null && _desk.Open && (_activityUi == null || !ReferenceEquals(_activityUi.GetCurrentActivity, _desk)))
            {
                _desk.Finish();   // the game's panel let it go without a word: the minutes so far are counted, and the player stands up
            }
            if (_desk != null && !_desk.Open)
            {
                _desk = null;
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
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
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
            catch (Exception e) when (GameMembers.StandsDown(e, "the university's building card and the watch on its desks"))
            {
                _watchOff = true;
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnLateUpdate()
        {
            _interior?.Card(false);   // (one reference comparison: the game's card over the university)
            bool rooms = _interior != null && _interior.Floor > 0;   // (from the frame the player is on a floor: no course of the game's opens there)
            if (!_deskWatch && !rooms)
            {
                return;
            }
            if (_activityUi == null)
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                _activityUi = uis != null ? uis.playerActivityUI : null;
                if (_activityUi == null)
                {
                    return;
                }
            }
            IPlayerActivity now = _activityUi.GetCurrentActivity;
            if (ReferenceEquals(now, _deskSeen))
            {
                return;
            }
            _deskSeen = now;
            if (now is StudyActivity game && game.GetState() == PlayerActivityState.NotStarted)
            {
                try
                {
                    SchoolDesk.DeskOffer offer;
                    if (_interior != null && _interior.Floor > 0)
                    {
                        int room = _interior.RoomOf(SchoolDesk.DoorOf(game));
                        offer = RoomOffer(room > 0 ? room : 1);
                        Logger?.Info("Careers: a classroom door of the university, floor " + _interior.Floor + " room " + room + ": " + (offer.Course >= 0 ? StudyCatalogue.Find(offer.Course).Key : "no course")
                                     + (offer.Can ? ", " + (offer.Exam ? "the exam" : "the class") + " can be begun." : ", nothing to begin now (" + offer.Key + ")."));
                    }
                    else
                    {
                        offer = DeskOfferNow();
                    }
                    SchoolDesk desk = SchoolDesk.Take(_activityUi, game, this, offer);
                    if (desk != null)
                    {
                        _deskSeen = desk;
                        _desk = desk;
                    }
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
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
                    return new SchoolDesk.DeskOffer { Course = c.Id, Exam = true, Can = true, Key = "reallife_study_prompt_exam", Arguments = CourseArgs(c.Id) };
                }
                if (AtTheSchool(e, false) && e.Stage == (int)EnrolStage.Studying && c.Timetabled)
                {
                    StudyMode mode = (StudyMode)e.Mode;
                    int at = StudyRules.NextClass(c, mode, Save.Day, CareersGame.MinuteOfDay, Math.Max(e.Start, e.SettledDay + 1));
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
        /// A class or an exam begun at one of the school's desks (Careers/SchoolDesk): the same checks as the Campus app's
        /// buttons, the sitting made for the desk's own activity. Null and the sitting, or the reason key.
        /// </summary>
        internal string DeskSitting(int course, bool exam, out Sitting sitting)
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
            if (!exam)
            {
                string why = CanAttend(e, out int minutes);
                if (why != null)
                {
                    return why;
                }
                sitting = new Sitting(this, SittingKind.Class, c.Id, minutes, CareersText.Course(c.Id), true,
                    CareersText.T("reallife_study_skip_class", "number", (e.Seen + 1).ToString(), "of", c.Sessions.ToString()),
                    CareersText.T("reallife_study_skip_until", "time", CareersText.Minutes(minutes)));
            }
            else
            {
                string why = CanSit(e);
                if (why != null)
                {
                    return why;
                }
                if (e.Stage == (int)EnrolStage.Retake)
                {
                    int fee = Prices.At(StudyRules.RetakeFee(c));
                    if (!CareersGame.Charge(fee, CareersGame.FeeKey, CareersText.Course(c.Id)))
                    {
                        return "reallife_study_why_money";
                    }
                    e.Fee += fee;
                }
                sitting = new Sitting(this, SittingKind.Exam, c.Id, StudyCatalogue.ExamHours * 60, CareersText.Course(c.Id), true,
                    CareersText.T(e.Stage == (int)EnrolStage.Retake ? "reallife_study_skip_retake" : "reallife_study_skip_exam", "pass", c.Pass.ToString()));
            }
            _sitting = sitting;
            Logger?.Info("Careers: " + (exam ? "the exam of " : "a class of ") + c.Key + " begun at a desk of the school (the game's study panel).");
            return null;
        }

        // ---------------------------------------------------------------- the hour a class begins

        // Where the player is, in the log; and a notice: how to join it here, or why not and where to go. Once a class.
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
                            + " at " + _campus.Name + (BuildingManager.IsInsideBuilding ? " (inside a building" + (CareersGame.InsideAt(_campus.Street, _campus.Number) ? ": the university's)" : ")") : "");
                }
            }
            else
            {
                where = "held " + c.Campus + (mode == StudyMode.Distance ? " (from home)" : string.Empty) + "; the player is "
                        + (CareersGame.InSchool() ? "inside the school" : BuildingManager.IsInsideBuilding ? "inside a building" : "out of doors");
            }
            Logger?.Info("Careers: class due for " + c.Key + " at " + hour.ToString("00") + ":00 on day " + day + "; " + where + "; "
                         + (InRooms(e) ? "held on floor " + CampusRules.FloorOf(c.Id) + ", room " + CampusRules.RoomOf(c.Id) + " (the player is " + (_interior.Floor > 0 ? "on floor " + _interior.Floor : "not on a floor")
                            + ", the university " + (_interior.OpenNow() ? "open" : "closed") + "); " : string.Empty)
                         + (why == null ? "it can be joined now (" + (AtTheSchool(e, false) ? "Study at a desk" : InRooms(e) ? "the classroom's door" : "the panel where the player stands") + ")." : "it cannot be joined now: " + why + "."));
            int notice = StudyRules.ClassNotice(why);
            if (notice == 0)
            {
                return;
            }
            string course = CareersText.Course(c.Id);
            if (notice == 1)
            {
                if (InRooms(e))
                {
                    Tell(NotificationType.Info, "reallife_campus_note_class_room", Data("course", course, "floor", CampusRules.FloorOf(c.Id).ToString(), "room", CampusRules.RoomOf(c.Id).ToString()),
                        TabStudying, CampusApp.CourseRow + c.Id);
                    return;
                }
                Tell(NotificationType.Info, AtTheSchool(e, false) ? "reallife_study_note_class_desk" : "reallife_study_note_class_here",
                    Data("course", course, "key", Keys.Label(KeyAction.Interact)), TabStudying, CampusApp.CourseRow + c.Id);
                return;
            }
            if (minutes <= 0)
            {
                StudyRules.Joinable(c, mode, day, hour * 60, out minutes);   // (how long it runs, though it cannot be joined from here)
            }
            var data = Data("course", course, "why", CareersText.T(why), "place", PlaceOf(c, mode));
            data["time"] = CareersText.Minutes(Math.Max(0, minutes));
            Tell(NotificationType.Warning, why == "reallife_study_why_not_school" ? "reallife_study_note_class_school" : "reallife_study_note_class_now", data,
                TabStudying, CampusApp.CourseRow + c.Id);
        }
    }
}
