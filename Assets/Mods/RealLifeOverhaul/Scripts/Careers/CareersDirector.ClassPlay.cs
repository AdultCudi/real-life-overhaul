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
    ///                   (Careers/ComputerClass). A home with no computer, or no working internet for an online class, says so.
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

        private ComputerClass _computer;     // a class or homework at the computer at home, from its press until it is over
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
            door.StartLearning();   // the game's study panel for the desk (its own refusals: closed, something in hand)
            if (!PlayerActivityUI.IsPanelOpen)
            {
                _beginCourse = -1;   // (refused: the game has said why)
                _beginFrame = -1;
                return null;
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
        /// panel runs it (Careers/ComputerClass). Null if under way; else the reason key.
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
            var activity = new ComputerClass(this, computer, sitting);
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
