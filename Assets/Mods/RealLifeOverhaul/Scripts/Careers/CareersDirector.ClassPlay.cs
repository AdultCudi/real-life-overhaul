using System;
using Helpers;
using PlayerActivity;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// A class, homework or an exam played the way the game's own studying is (a press of learn takes the player to a
    /// room, where the game's fast-forward or skip comes up), each where it is done, in one press:
    ///
    ///   the university  the Campus app's Attend, Sit or Homework takes the player to the course's floor and opens its
    ///                   classroom door's study panel, as a click on the door does; the class is begun there for them
    ///                   (Careers/SchoolDesk.Begin): the walk to the room's desk with the game's routing, the chair, the
    ///                   screen, the game's panel with fast forward, skip time and Stop
    ///   the school      the same at the nearest of Manhattan Business School's own desks
    ///   at home         an online or distance class, and homework, at the player's computer: its own buttons beside the
    ///                   game's Pass the time (Careers/ComputerButtons), or the Campus app's Attend and Homework at home,
    ///                   walk the player to it and sit them there as the game's computer passes the time
    ///                   (Careers/ComputerSitting). A home with no computer, or no working internet for an online class, says so.
    ///
    /// In a Going Public session the school's and the university's desks open the desk's own panel of the mod's, never the
    /// game's study panel, which Going Public takes for its own training window (<see cref="OpenDesk"/>).
    ///
    /// Where nothing in the world fits (a hospital's ward, the university while its floors are not there) the game's panel
    /// where the player stands, as before (<see cref="Offer"/>). The minutes count to the enrolment once, however it ends
    /// (Study/ClassPlayRules: ClassTally); nothing new is saved, and nothing is sent: classes are the local player's own.
    ///
    /// The cost: on a press, one look through the building's items (the desk or the computer); while a desk is wanted, the
    /// course and a frame number held; nothing per frame.
    /// </summary>
    public sealed partial class CareersDirector
    {
        private const int BeginFrames = 3;   // frames a desk's panel opened for the player may take to be seen by the watch on the game's panel

        private ComputerSitting _computer;   // a class, homework or a working day at the computer at home, from its press until it is over
        private int _beginCourse = -1;       // the course whose class (exam, homework) is to be begun at its desk for the player...
        private SittingKind _beginKind;      // ...which
        private int _beginFrame = -1;        // ...and the frame its desk's panel was opened in (-1: not yet)

        // ---------------------------------------------------------------- the desks: begun for the player

        // Why not now, as every press of a class says it: a sitting under way, the game busy, too tired.
        private string SeatBusy()
        {
            if (SittingNow || _sitting != null)
            {
                return "reallife_study_why_busy";
            }
            if (!CareersGame.Free())
            {
                return "reallife_study_why_game_busy";
            }
            return CareersGame.Energy < StudyCatalogue.TiredEnergy ? "reallife_study_why_tired" : null;
        }

        /// <summary>The Campus app sent the player to a course's desk: once its panel opens there, its class (exam, homework) is begun.</summary>
        private void WantBegin(int course, SittingKind kind)
        {
            _beginCourse = course;
            _beginKind = kind;
            _beginFrame = -1;
        }

        /// <summary>
        /// Called as a classroom door's panel is opened for a course the player was sent to (Careers/CampusInterior.GoToRoom):
        /// true if its class is to be begun for them, and the watch on the game's panel is then looking this frame.
        /// </summary>
        internal bool BeginHere(int course)
        {
            if (course < 0 || course != _beginCourse || !_studyOn)
            {
                return false;
            }
            _beginFrame = Time.frameCount;
            if (_activityUi == null)
            {
                UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
                _activityUi = uis != null ? uis.playerActivityUI : null;
            }
            _deskWatch = _activityUi != null;   // (until the timer looks again: the panel is seen this frame)
            return _deskWatch;
        }

        // The panel the watch has just seen is the one opened for the player: the offer made the class (exam, homework) of
        // the course they were sent for, to begin. course: the room's course, or -1 at the school (any desk will do).
        private bool TakeBegin(int course, SchoolDesk.DeskOffer offer)
        {
            bool fresh = _beginCourse >= 0 && _beginFrame >= 0 && Time.frameCount - _beginFrame <= BeginFrames;
            int wanted = _beginCourse;
            SittingKind kind = _beginKind;
            _beginCourse = -1;
            _beginFrame = -1;
            if (!fresh || offer == null || (course >= 0 && course != wanted))
            {
                return false;
            }
            offer.Course = wanted;
            offer.Kind = kind;
            offer.Can = true;
            offer.Key = kind == SittingKind.Exam ? "reallife_study_prompt_exam" : kind == SittingKind.Homework ? "reallife_study_prompt_homework" : "reallife_study_prompt_attend";
            offer.Arguments = CourseArgs(wanted);
            return true;
        }

        /// <summary>
        /// A study desk's panel opened for the player as a click on its door opens it, at the school (room 0) or a classroom
        /// of the university, by ClassPlayRules.WayToDesk: alone the game's study panel (StartLearning), given the class by
        /// the watch on it; in a Going Public session the desk's own panel (SchoolDesk.Own), never a StudyActivity, which
        /// Going Public would take for its training window. begin: the caller's <see cref="BeginHere"/> said yes. Null if the
        /// panel is open or the game has said why not; else the reason key. One self-check line a call.
        /// </summary>
        internal string OpenDesk(EducationDoorController door, int room, bool begin)
        {
            bool session = GoingPublic.InSession;
            DeskWay way = ClassPlayRules.WayToDesk(session);
            string where = room > 0 ? "room " + room + " on floor " + (_interior != null ? _interior.Floor : 0) + " of the university" : "a desk of the school";
            if (way == DeskWay.GameStudy)
            {
                bool refused = DeskGame.StudyDoorRefuses(false);   // (asked first, unsaid: the door says it itself)
                door.StartLearning();   // the game's study panel (its own refusals: closed, something in hand), its class given by the watch
                bool study = ShowsStudy();
                DeskCheck(session, way, where, begin, study || refused ? null : PlayerActivityUI.IsPanelOpen ? "the game's panel shows another activity"
                        : "the game's study panel did not open though its door lets the player in: no energy, or another mod took it (Going Public, in a session the mod has not seen)",
                    study ? "the game's study panel is up" : refused ? "the game's door refused it in its own words (closed, or something in hand)" : null);
                return null;
            }
            SchoolDesk.DeskOffer offer = room > 0 ? RoomOffer(room) : new SchoolDesk.DeskOffer { Course = -1, Key = "reallife_study_why_no_class" };
            bool begun = begin && TakeBegin(room > 0 ? offer.Course : -1, offer);
            if (room == 0 && !begun)
            {
                DeskCheck(session, way, where, begin, "no class of the school's was to be begun there", null);
                return "reallife_study_why_no_class";   // (a desk of the school is opened only to begin: its own click is Going Public's)
            }
            if (room == 0)
            {
                SchoolOffer(offer);
            }
            if (PlayerActivityUI.IsPanelOpen)
            {
                DeskCheck(session, way, where, begin, "the game's panel is busy", null);
                return "reallife_study_why_game_busy";
            }
            if (DeskGame.StudyDoorRefuses(true))
            {
                DeskCheck(session, way, where, begin, null, "the door refused it in the game's own words (closed, or something in hand)");
                return null;
            }
            SchoolDesk desk = SchoolDesk.Own(door, this, offer);
            if (desk == null)
            {
                DeskCheck(session, way, where, begin, "the desk lacks its chair or its way out", null);
                return "reallife_study_why_game_busy";
            }
            if (!DeskGame.Show(desk))
            {
                DeskCheck(session, way, where, begin, null, "the game's panel refused it in its own words (no energy, or a vehicle)");
                return null;
            }
            if (_activityUi == null)
            {
                UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
                _activityUi = uis != null ? uis.playerActivityUI : null;
            }
            _desk = desk;
            _deskSeen = desk;
            if (begun)
            {
                desk.Begin();
            }
            DeskCheck(session, way, where, begin, DeskGame.Shows(desk) && !ShowsStudy() ? null : "the game's panel does not hold the desk's own activity",
                "the desk's own panel is up, no study panel of the game's asked for");
            return null;
        }

        // True while the game's panel shows a study activity of the game's own.
        private static bool ShowsStudy()
        {
            UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
            return uis != null && uis.playerActivityUI != null && uis.playerActivityUI.GetCurrentActivity is StudyActivity;
        }

        // A school desk's offer as its own panel shows it before it is begun: the course as its headline, the sitting's lines.
        private void SchoolOffer(SchoolDesk.DeskOffer offer)
        {
            Enrolment e = _study.Live(offer.Course);
            offer.Title = CareersText.Course(offer.Course);
            if (e == null || e.Def == null)
            {
                offer.Lines = new string[0];
                return;
            }
            Sitting shown = offer.Kind == SittingKind.Exam ? ExamSitting(e) : offer.Kind == SittingKind.Homework ? HomeworkSitting(e) : ClassSitting(e, Minutes(e));
            offer.Lines = shown.Lines;
        }

        // With Developer diagnostics on, one line a desk's panel opened for the player: in a session or not, the way taken
        // against the way wanted, and what came of it. fail: why it failed, or null; said: what the game did, if anything.
        private static void DeskCheck(bool session, DeskWay way, string where, bool begin, string fail, string said)
        {
            if (!Meta.Dev.Verbose)
            {
                return;
            }
            DeskWay wanted = ClassPlayRules.WayToDesk(session);
            Logger?.Info("Careers check (desk panel): " + (session ? "in a Going Public session" : "alone") + ", " + where + (begin ? ", begun for the player" : string.Empty)
                         + ": the way taken is " + WayWords(way) + ", wanted " + WayWords(wanted) + (said != null ? " (" + said + ")" : string.Empty) + ": "
                         + (way != wanted ? "FAIL, not the way the session asks for" : fail == null ? "PASS" : "FAIL, " + fail));
        }

        private static string WayWords(DeskWay way)
        {
            return way == DeskWay.OwnPanel ? "the desk's own panel" : "the game's study panel";
        }

        // A class, an exam or homework at the school: its nearest desk's own panel opened, as a click on it opens it, and
        // begun there for the player. Null if under way (or the game has said why not); else the reason key.
        private string ToSchoolDesk(Enrolment e, SittingKind kind)
        {
            string busy = SeatBusy();
            if (busy != null)
            {
                return busy;
            }
            EducationDoorController door = DeskGame.NearestStudyDoor();
            if (door == null)
            {
                Logger?.Info("Careers: the school has no study desk with its chair to sit " + Words(kind) + " of " + e.Def.Key + " at; it is offered where the player stands.");
                return kind == SittingKind.Class ? Offer(ClassSitting(e, Minutes(e))) : kind == SittingKind.Homework ? Offer(HomeworkSitting(e)) : SitInPlace(e);
            }
            WantBegin(e.Def.Id, kind);
            if (!BeginHere(e.Def.Id))
            {
                return "reallife_study_why_game_busy";
            }
            CloseApps();
            string why = OpenDesk(door, 0, true);   // the desk's panel: alone the game's study panel, in a session the desk's own
            if (why != null || !PlayerActivityUI.IsPanelOpen)
            {
                _beginCourse = -1;   // (refused: the game has said why, or why is the reason key)
                _beginFrame = -1;
                return why;
            }
            Logger?.Info("Careers: " + Words(kind) + " of " + e.Def.Key + " is begun at a desk of the school from the Campus app.");
            return null;
        }

        private int Minutes(Enrolment e)
        {
            CanAttend(e, out int minutes);
            return minutes;
        }

        // An exam where the player stands (the school without a desk to sit it at): its retake's fee, then the panel.
        private string SitInPlace(Enrolment e)
        {
            CourseDef c = e.Def;
            if (e.Stage == (int)EnrolStage.Retake)
            {
                int fee = Prices.At(StudyRules.RetakeFee(c));
                if (!CareersGame.Charge(fee, CareersGame.FeeKey, CareersText.Course(c.Id)))
                {
                    return "reallife_study_why_money";
                }
                e.Fee += fee;
            }
            return Offer(ExamSitting(e));
        }

        // ---------------------------------------------------------------- the computer at home

        /// <summary>What the computer at home offers now (Careers/ComputerButtons): the class button and the homework button.</summary>
        internal ComputerOffer ComputerOfferNow()
        {
            if (!_studyOn || _study == null || Save == null)
            {
                return ComputerOffer.None;
            }
            return ClassPlayRules.AtComputer(_study.Courses, Save.Day, CareersGame.MinuteOfDay, SittingClass);
        }

        /// <summary>
        /// An online or distance class, or a piece of homework, at a computer of the player's home: the one pressed, or
        /// (from the Campus app) the first the game would pass the time at. The player walks there and sits, and the game's
        /// panel runs it (Careers/ComputerSitting). Null if under way; else the reason key.
        /// </summary>
        internal string AtComputer(int course, SittingKind kind, ComputerController computer)
        {
            Enrolment e = _studyOn ? _study.Live(course) : null;
            int minutes = 0;
            string why = kind == SittingKind.Class ? CanAttend(e, out minutes) : CanHomework(e);
            if (why != null)
            {
                return why;
            }
            if (!CareersGame.AtHome())
            {
                return kind == SittingKind.Class ? "reallife_study_why_not_home" : "reallife_study_why_not_desk";
            }
            computer = computer != null && DeskGame.Usable(computer) ? computer : DeskGame.UsableComputer();
            if (computer == null)
            {
                return kind == SittingKind.Class ? "reallife_study_why_no_computer" : "reallife_study_why_no_computer_homework";
            }
            string busy = SeatBusy();
            if (busy != null)
            {
                return busy;
            }
            if (PlayerHelper.ItemInstanceInHands != null && !PlayerHelper.IsHoldingBag)
            {
                return "notification_need_empty_hands_to_interact";   // (the game's own words, as its computer says them)
            }
            return StartAtComputer(computer, kind == SittingKind.Class ? ClassSitting(e, minutes) : HomeworkSitting(e), e.Def.Key);
        }

        // The game's panel opened with the sitting at the computer, and the walk there begun. Null, or the reason key.
        private string StartAtComputer(ComputerController computer, Sitting sitting, string what)
        {
            CloseApps();
            var activity = new ComputerSitting(this, computer, sitting);
            if (!DeskGame.Show(activity))
            {
                return "reallife_study_why_game_busy";
            }
            _computer = activity;
            _sitting = sitting;
            activity.Go();
            Logger?.Info("Careers: " + Words(sitting.Kind) + " of " + what + " at the computer at home ("
                         + computer.itemName + (computer.FindChair() != null ? ", with its chair" : ", no chair") + "), "
                         + sitting.SkipMinutes + " minutes.");
            return null;
        }
    }
}
