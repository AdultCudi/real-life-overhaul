using System;
using Helpers;
using PlayerActivity;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// A class, homework or an exam where it is done, in one press: the university's classroom or the school's nearest desk,
    /// the computer at home, else the game's panel where the player stands; in a Going Public session the desk's own panel,
    /// never the game's study panel (<see cref="OpenDesk"/>). Nothing per frame.
    /// </summary>
    public sealed partial class CareersDirector
    {
        private const int BeginFrames = 3;   // frames a desk's panel opened for the player may take to be seen by the watch on the game's panel

        private ComputerSitting _computer;   // a class, homework or a working day at the computer at home, from its press until it is over
        private int _beginCourse = -1;       // the course whose class (exam, homework) is to be begun at its desk...
        private SittingKind _beginKind;      // ...which
        private int _beginFrame = -1;        // ...and the frame its desk's panel was opened in, -1 for not yet

        // Why not now, as every press of a class says it: a sitting under way, the game busy, too tired.
        private string SeatBusy() => SeatWhy(true);

        // Why the player cannot sit down now, or null. game: the game's panel and clock are asked too, which a desk's own panel,
        // being the game's panel, never is.
        private string SeatWhy(bool game)
        {
            if (SittingNow || _book.Count > 0)
            {
                return "reallife_study_why_busy";
            }
            if (game && !CareersGame.Free())
            {
                return "reallife_study_why_game_busy";
            }
            return CareersGame.Energy < StudyCatalogue.TiredEnergy ? "reallife_study_why_tired" : null;
        }

        /// <summary>
        /// The one decision for a course at its own desk (<see cref="ClassPlayRules.AtDesk"/>), read by the desk's panel at its
        /// door and by the Campus app's Attend and Sit. school: a desk of the school, which holds only its own courses' classes
        /// and the exams sat there; away: the Campus app walks the player to the classroom; game: the game's panel is asked too.
        /// </summary>
        private DeskStart DeskStartOf(Enrolment e, bool school, bool away, bool game)
        {
            string classWhy = !school || AtTheSchool(e, false) ? CanAttend(e, out _) : "reallife_study_why_no_class";
            string examWhy = !school || AtTheSchool(e, true) ? CanSit(e) : "reallife_study_why_no_exam";
            return ClassPlayRules.AtDesk(classWhy, examWhy, e.FeeLate, SeatWhy(game), away && !school);
        }

        // The words of a desk's button for a kind of sitting; none has a {placeholder}, as the game's activity panel writes a
        // button's key alone (PlayerActivityUI.SetButton reads neither its arguments nor whether it may be pressed).
        private static string StartKey(SittingKind kind)
        {
            return kind == SittingKind.Exam ? "reallife_campus_button_sit" : kind == SittingKind.Homework ? "reallife_campus_button_homework" : "reallife_campus_button_attend";
        }

        // A desk's offer for a course by the one decision: the button and the sitting's lines when it can begin, else the reason
        // and, while nothing is on, when the next class is and from when it can be begun. first: a line before them, or null.
        private void FillOffer(SchoolDesk.DeskOffer offer, Enrolment e, DeskStart start, string first)
        {
            CourseDef c = e.Def;
            offer.Course = c.Id;
            offer.Kind = start.Kind;
            offer.Can = start.Can;
            offer.Why = start.Why;
            offer.Key = start.Can ? StartKey(start.Kind) : null;
            offer.Title = CareersText.Course(c.Id);
            string[] lines;
            if (start.Can)
            {
                lines = (start.Kind == SittingKind.Exam ? ExamSitting(e) : ClassSitting(e, Minutes(e))).Lines;
            }
            else
            {
                string next = start.On ? null : NextLine(e);
                lines = next != null ? new[] { CareersText.T(start.Why), next } : new[] { CareersText.T(start.Why) };
            }
            offer.Lines = Lined(first, lines);
        }

        // A piece of homework as a desk's offer, the Campus app having sent the player to do it there.
        private void FillHomework(SchoolDesk.DeskOffer offer, Enrolment e, string first)
        {
            string why = CanHomework(e);
            offer.Course = e.Def.Id;
            offer.Kind = SittingKind.Homework;
            offer.Can = why == null;
            offer.Why = why;
            offer.Key = why == null ? StartKey(SittingKind.Homework) : null;
            offer.Title = CareersText.Course(e.Def.Id);
            offer.Lines = Lined(first, why == null ? HomeworkSitting(e).Lines : new[] { CareersText.T(why) });
        }

        // When a course's next class is and the span it can be begun in, every value filled; null once every class is held.
        private string NextLine(Enrolment e)
        {
            int at = StudyRules.NextClass(e, Save.Day, CareersGame.MinuteOfDay, SittingClass);
            if (at < 0)
            {
                return null;
            }
            ClassPlayRules.BeginSpan(at, out int from, out int until);
            const int ADay = 24 * 60;   // minutes
            return CareersText.Fill("reallife_study_desk_next_from", "day", CareersText.Day(at / ADay), "hour", CareersText.Hour(at % ADay / 60),
                "from", CareersText.TimeOfDay(from), "until", CareersText.TimeOfDay(until));
        }

        private static string[] Lined(string first, string[] lines)
        {
            if (first == null)
            {
                return lines;
            }
            var all = new string[lines.Length + 1];
            all[0] = first;
            Array.Copy(lines, 0, all, 1, lines.Length);
            return all;
        }

        /// <summary>The Campus app sent the player to a course's desk: once its panel opens there, its class (exam, homework) is begun.</summary>
        private void WantBegin(int course, SittingKind kind)
        {
            _beginCourse = course;
            _beginKind = kind;
            _beginFrame = -1;
        }

        /// <summary>As a classroom door's panel opens for a course the player was sent to: true if its class is to be begun, the watch looking this frame.</summary>
        internal bool BeginHere(int course)
        {
            if (course < 0 || course != _beginCourse || !_studyOn)
            {
                return false;
            }
            _beginFrame = Time.frameCount;
            _deskWatch = ActivityUi() != null;   // until the timer looks again: the panel is seen this frame
            return _deskWatch;
        }

        // The panel just seen is the one opened for the player: true if the sitting the Campus app asked for is begun there. The
        // desk's offer is the one decision's (DeskStartOf), never overruled: a class whose window closed on the walk there is
        // not begun, and the panel says why. course: the room's, or -1 at the school, whose offer is made for the course asked.
        private bool TakeBegin(int course, SchoolDesk.DeskOffer offer)
        {
            bool fresh = _beginCourse >= 0 && _beginFrame >= 0 && Time.frameCount - _beginFrame <= BeginFrames;
            int wanted = _beginCourse;
            SittingKind kind = _beginKind;
            _beginCourse = -1;
            _beginFrame = -1;
            Enrolment e = _studyOn ? _study.Live(wanted) : null;
            if (!fresh || offer == null || (course >= 0 && course != wanted) || e == null || e.Def == null)
            {
                return false;
            }
            string where = course >= 0 ? RoomWhere(CampusRules.FloorOf(wanted), CampusRules.RoomOf(wanted)) : null;
            if (kind == SittingKind.Homework)
            {
                FillHomework(offer, e, where);   // never offered at a door of itself: the Campus app's press asked for it
            }
            else if (course < 0)
            {
                FillOffer(offer, e, DeskStartOf(e, true, false, false), null);
            }
            if (!offer.Can || offer.Kind != kind)
            {
                Logger?.Info("Careers: the Campus app sent the player to begin " + Words(kind) + " of " + e.Def.Key + " at its desk; it cannot be begun there now ("
                             + (offer.Why ?? "the desk offers " + Words(offer.Kind)) + "), and the desk's panel says why.");
                return false;
            }
            offer.Due = true;   // begun for the player: the class's on the Start key
            return true;
        }

        /// <summary>
        /// Opens a study desk's panel as a click on its door does, by ClassPlayRules.WayToDesk: alone the game's study panel; in
        /// a session the desk's own (SchoolDesk.Own). Null if open or the game said why not, else the reason key; one self-check line.
        /// </summary>
        internal string OpenDesk(EducationDoorController door, int room, bool begin)
        {
            bool session = GoingPublic.InSession;
            DeskWay way = ClassPlayRules.WayToDesk(session);
            string where = room > 0 ? "room " + room + " on floor " + (_interior != null ? _interior.Floor : 0) + " of the university" : "a desk of the school";
            if (way == DeskWay.GameStudy)
            {
                bool refused = DeskGame.StudyDoorRefuses(false);   // asked first, unsaid: the door says it itself
                door.StartLearning();   // the game's study panel (its own refusals: closed, something in hand), its class given by the watch
                bool study = ShowsStudy();
                DeskCheck(session, way, where, begin, study || refused ? null : PlayerActivityUI.IsPanelOpen ? "the game's panel shows another activity"
                        : "the game's study panel did not open though its door lets the player in: no energy, or another mod took it (Going Public, in a session the mod has not seen)",
                    study ? "the game's study panel is up" : refused ? "the game's door refused it in its own words (closed, or something in hand)" : null);
                return null;
            }
            SchoolDesk.DeskOffer offer = room > 0 ? RoomOffer(room) : new SchoolDesk.DeskOffer { Course = -1, Why = "reallife_study_why_no_class" };
            bool begun = begin && (room == 0 || offer.Course >= 0) && TakeBegin(room > 0 ? offer.Course : -1, offer);
            if (room == 0 && !begun)
            {
                DeskCheck(session, way, where, begin, "no class of the school's was to be begun there", null);
                return "reallife_study_why_no_class";   // a school desk is opened only to begin: its own click is Going Public's
            }
            if (!ClassPlayRules.ShowsDesk(session, offer.Can))
            {
                DeskCheck(session, way, where, begin, null, "nothing to begin there now (" + (offer.Why ?? "a free classroom") + "): said in a notice, as Going Public hides the game's panel");
                return offer.Why ?? "reallife_study_why_no_class";
            }
            return OwnDesk(door, offer, begun, session, where, begin);
        }

        // The desk's own activity on the game's panel, which Going Public hides in a session and presses the desk's Start on for
        // the player (ClassPlayRules.SessionPick); begun: the Campus app's press, made here. Null if open or the game said why not.
        private string OwnDesk(EducationDoorController door, SchoolDesk.DeskOffer offer, bool begun, bool session, string where, bool begin)
        {
            DeskWay way = DeskWay.OwnPanel;
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
            ActivityUi();
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

        // With Developer diagnostics on, one line a desk panel: the way taken against the way wanted. fail: why it failed; said: what the game did.
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

        // A class, exam or homework at the school: its nearest desk's panel opened and begun for the player. Null if under way, else the reason key.
        private string ToSchoolDesk(Enrolment e, SittingKind kind)
        {
            string busy = kind == SittingKind.Homework ? SeatBusy() : ClassPlayRules.BeginWhy(DeskStartOf(e, true, false, true), kind, null);
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
                _beginCourse = -1;   // refused: the game has said why, or why is the reason key
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

        // An exam where the player stands: its retake's fee, then the panel.
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

        /// <summary>What the computer at home offers now (Careers/ComputerButtons): the class button and the homework button.</summary>
        internal ComputerOffer ComputerOfferNow()
        {
            if (!_studyOn || _study == null || Save == null)
            {
                return ComputerOffer.None;
            }
            return ClassPlayRules.AtComputer(_study.Courses, Save.Day, CareersGame.MinuteOfDay, SittingClass);
        }

        /// <summary>An online or distance class, or homework, at a home computer (the one pressed, or the first usable); null if under way, else the reason key.</summary>
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
                return "notification_need_empty_hands_to_interact";   // the game's own words, as its computer says them
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
            Hold(sitting, activity);
            activity.Go();
            Logger?.Info("Careers: " + Words(sitting.Kind) + " of " + what + " at the computer at home ("
                         + computer.itemName + (computer.FindChair() != null ? ", with its chair" : ", no chair") + "), "
                         + sitting.SkipMinutes + " minutes.");
            return null;
        }
    }
}
