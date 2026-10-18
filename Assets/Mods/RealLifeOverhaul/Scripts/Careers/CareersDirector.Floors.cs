using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Careers
{
    /// <summary>The university's floors as study sees them: what it has for the player now, a classroom's panel and door, and its address kept so a move is said once.</summary>
    public sealed partial class CareersDirector
    {
        private CampusInterior _interior;
        private string _campusWas;          // "street|number" the university was at, as the save had it (or the old rule's building)
        private bool _campusKept;           // the save had the university's address: the player has had the university before
        private string _campusTold;         // what the save was last told of where the university is (CampusRules.AwayMark); null at home
        private bool _campusRead;

        /// <summary>The university's floors; null while the university is not there.</summary>
        internal CampusInterior Interior => _interior;

        /// <summary>True while a class, homework or exam of the mod's is running (in place, at a desk or at the computer at home).</summary>
        internal bool SittingNow => (_skip != null && _skip.Open) || DeskBusy;

        /// <summary>True for a course of the university followed in person while its floors are there: held in its classroom.</summary>
        private bool InRooms(Enrolment e)
        {
            CourseDef c = e != null ? e.Def : null;
            return c != null && c.Campus == Campus.University && (StudyMode)e.Mode != StudyMode.Distance && CampusRules.FloorOf(c.Id) > 0
                   && _interior != null && _interior.Available && !_roomDesksOff && !_watchOff && SchoolDesk.Works;
        }

        /// <summary>True if the university has something of the player's now: a class to join, an exam to sit, the ceremony, of <paramref name="course"/>.</summary>
        internal bool UniversityBusinessNow(out int course)
        {
            course = -1;
            if (!_studyOn || _study == null || Save == null)
            {
                return false;
            }
            int day = Save.Day, minute = CareersGame.MinuteOfDay;
            for (int i = 0; i < _study.Courses.Count; i++)
            {
                Enrolment e = _study.Courses[i];
                CourseDef c = e != null ? e.Def : null;
                if (c == null || c.Campus != Campus.University || (StudyMode)e.Mode == StudyMode.Distance)
                {
                    continue;
                }
                if (e.Stage == (int)EnrolStage.Studying && !e.FeeLate && e.SettledDay < day && day >= e.Start && StudyRules.Joinable(c, (StudyMode)e.Mode, day, minute, out _))
                {
                    course = c.Id;
                    return true;
                }
                if (StudyRules.ExamDay(e, day) && Save.Hour >= StudyCatalogue.ExamOpens && Save.Hour < StudyCatalogue.ExamCloses)
                {
                    course = c.Id;
                    return true;
                }
            }
            if (_study.GraduationCourse >= 0 && day == _study.GraduationDay && minute >= CeremonyHour * 60 - 30 && minute <= CeremonyHour * 60 + 60)
            {
                course = _study.GraduationCourse;
                return true;
            }
            return false;
        }

        /// <summary>The first course of the university the player is on in person, or -1.</summary>
        internal int FirstUniversityCourse()
        {
            for (int i = 0; _studyOn && _study != null && i < _study.Courses.Count; i++)
            {
                Enrolment e = _study.Courses[i];
                if (e != null && e.Def != null && e.Def.Campus == Campus.University && (StudyMode)e.Mode != StudyMode.Distance && CampusRules.FloorOf(e.Def.Id) > 0)
                {
                    return e.Def.Id;
                }
            }
            return -1;
        }

        // Once a second: the floors with the university's building.
        private void InteriorTick()
        {
            if (UniversityOpen && _studyOn && _campus != null && _campus.Searched)
            {
                if (_interior == null)
                {
                    _interior = new CampusInterior(this, Logger);
                }
                _interior.Tick();
                CampusMoved();
            }
            else if (_interior != null && (!UniversityOpen || !_studyOn))
            {
                _interior.Dispose();
                _interior = null;
            }
            else if (_interior != null)
            {
                _interior.Gone("the university is being chosen");
            }
        }

        private void InteriorGone()
        {
            _interior?.Dispose();
            _interior = null;
        }

        private string _campusNow, _campusNowStreet;   // the building as the save writes it, and the street it was made from...
        private int _campusNowNumber;                  // ...and the number

        // The building against the save's: a move logged and told once; and once a save, or when it changes, why it is away (CampusRules.AwayMark).
        private void CampusMoved()
        {
            if (_campus == null || !_campus.Usable || Save == null)
            {
                return;
            }
            if (_campusNow == null || !ReferenceEquals(_campusNowStreet, _campus.Street) || _campusNowNumber != _campus.Number)
            {
                _campusNowStreet = _campus.Street;
                _campusNowNumber = _campus.Number;
                _campusNow = _campus.Street + "|" + _campus.Number;   // made once for the building, not each second
            }
            string now = _campusNow;
            if (!_campusRead)
            {
                _campusRead = true;
                string kept = null;
                _campusKept = Save.modData != null && Save.modData.TryGetValue(CareersStore.CampusKey, out kept) && !string.IsNullOrEmpty(kept);
                _campusWas = _campusKept ? kept : _campus.Before;
                string told = null;
                _campusTold = Save.modData != null && Save.modData.TryGetValue(CareersStore.CampusToldKey, out told) ? told : null;
            }
            string mark = CampusRules.AwayMark(_campus.Place, _campus.Street, _campus.Number, _campus.Blocked);
            bool moved = !string.IsNullOrEmpty(_campusWas) && !string.Equals(_campusWas, now, StringComparison.Ordinal);
            bool away = CampusRules.TellAway(mark, _campusTold);
            if (!moved && !away)
            {
                _campusWas = now;
                _campusTold = mark;
                return;
            }
            string was = moved ? _campusWas.Replace('|', ' ') : null;
            _campusWas = now;
            _campusTold = mark;
            bool enrolled = FirstUniversityCourse() >= 0 || (_study != null && _study.GraduationCourse >= 0);
            if (CampusRules.SaysMove(moved, _campusKept))
            {
                Logger?.Info("Careers: the university has moved from " + was + " to " + _campus.Name + (_campus.AtHome ? " (its permanent building)" : " (a building of its own)") + "; "
                             + (enrolled ? "the courses under way, their classes and progress stay as they were." : "nothing of the player's was under way there."));
            }
            if (away)
            {
                // Once: why it is not at home, and where it is meanwhile (a move included).
                string why = CareersText.T(CampusRules.AwayKey(_campus.Blocked), "home", _campus.HomeName);
                Tell(NotificationType.Info, CampusRules.AwayNoteKey(_campus.Place), Data("why", why, "address", _campus.Name), TabStudying, null);
            }
            else if (moved && (enrolled || _campusKept))
            {
                // A move into the permanent building, or between buildings, told to a player who has had the university before.
                Tell(NotificationType.Info, _campus.AtHome ? "reallife_campus_note_home" : "reallife_campus_note_moved", Data("address", _campus.Name), TabStudying, null);
            }
        }

        private void WriteCampus(Dictionary<string, string> data)
        {
            if (!string.IsNullOrEmpty(_campusWas))
            {
                data[CareersStore.CampusKey] = _campusWas;
            }
            if (_campusRead)
            {
                if (_campusTold != null)
                {
                    data[CareersStore.CampusToldKey] = _campusTold;
                }
                else
                {
                    data.Remove(CareersStore.CampusToldKey);   // at home: a later move away is told again
                }
            }
        }

        // What a classroom of the floor the player is on offers: its course's class or exam, or when the next class is.
        private SchoolDesk.DeskOffer RoomOffer(int room)
        {
            int floor = _interior != null ? _interior.Floor : 0;
            int course = CampusRules.CourseAt(floor, room);
            string where = CareersText.T("reallife_campus_room_where", "floor", floor.ToString(), "room", room.ToString());
            if (course < 0)
            {
                return new SchoolDesk.DeskOffer
                {
                    Course = -1, Only = true, Can = false, Key = "reallife_study_why_no_class",
                    Title = CareersText.T("reallife_campus_room_sign_free", "room", room.ToString()), Lines = new[] { where, CareersText.T("reallife_campus_room_free") }
                };
            }
            string name = CareersText.Course(course);
            Enrolment e = _studyOn ? _study.Live(course) : null;
            var offer = new SchoolDesk.DeskOffer { Course = course, Only = true, Title = name };
            int grade = e == null && _studyOn ? _study.GradeOf(course) : -1;
            if (grade >= 0)
            {
                offer.Can = false;
                offer.Key = "reallife_campus_room_done";
                offer.Lines = new[] { where, CareersText.T("reallife_persona_edu_passed", "grade", CareersText.Grade(StudyCatalogue.Find(course), grade)) };
                return offer;
            }
            if (e == null || !InRooms(e))
            {
                offer.Can = false;
                offer.Key = "reallife_campus_room_not_on";
                offer.Lines = new[] { where, CareersText.T(e != null ? "reallife_campus_room_distance" : "reallife_campus_room_sign_up") };
                return offer;
            }
            CourseDef c = e.Def;
            string classWhy = CanAttend(e, out int minutes);
            if (classWhy == null)
            {
                offer.Can = true;
                offer.Key = "reallife_study_prompt_attend";
                offer.Arguments = CourseArgs(course);
                offer.Lines = new[] { where, CareersText.T("reallife_study_skip_class", "number", (e.Seen + 1).ToString(), "of", c.Sessions.ToString()),
                    CareersText.T("reallife_study_skip_until", "time", CareersText.Minutes(minutes)) };
                return offer;
            }
            string examWhy = CanSit(e);
            if (examWhy == null)
            {
                offer.Kind = SittingKind.Exam;
                offer.Can = true;
                offer.Key = "reallife_study_prompt_exam";
                offer.Arguments = CourseArgs(course);
                offer.Lines = new[] { where, CareersText.T(e.Stage == (int)EnrolStage.Retake ? "reallife_study_skip_retake" : "reallife_study_skip_exam", "pass", c.Pass.ToString()) };
                return offer;
            }
            offer.Can = false;
            int at = StudyRules.NextClass(e, Save.Day, CareersGame.MinuteOfDay, SittingClass);
            if (at >= 0)
            {
                offer.Key = "reallife_study_desk_next";
                offer.Arguments = new { course = name, day = CareersText.Day(at / (24 * 60)), hour = CareersText.Hour(at % (24 * 60) / 60) };
            }
            else
            {
                offer.Key = classWhy != "reallife_study_why_no_class" ? classWhy : examWhy;
            }
            offer.Lines = new[] { where, CareersText.T(e.FeeLate ? "reallife_study_why_fee_owed" : examWhy != "reallife_study_why_no_exam" ? examWhy : classWhy) };
            return offer;
        }

        /// <summary>True if a click on this study door is the mod's: a classroom door of the player's floor in a Going Public session (ClassPlayRules.TakesRoomClick).</summary>
        internal bool TakesRoomClick(EducationDoorController door)
        {
            bool session = GoingPublic.InSession;
            int room = session && _studyOn && !_roomDesksOff && _interior != null && _interior.Floor > 0 ? _interior.RoomOf(door) : 0;
            return ClassPlayRules.TakesRoomClick(session, room);
        }

        /// <summary>A classroom door clicked in a Going Public session: the room's own panel of the mod's (<see cref="OpenDesk"/>).</summary>
        internal void RoomDoorClicked(EducationDoorController door)
        {
            int room = _studyOn && _interior != null && _interior.Floor > 0 ? _interior.RoomOf(door) : 0;
            if (room == 0 || PlayerActivity.PlayerActivityUI.IsPanelOpen)
            {
                return;
            }
            string why = OpenDesk(door, room, false);
            if (why != null)
            {
                Tell(NotificationType.Warning, why, null, TabStudying, null);
            }
        }

        /// <summary>The game's mark over a classroom door for its room's course (CampusRules.MarkOf); once a second for five rooms on a floor.</summary>
        internal RoomMark RoomMarkOf(int course)
        {
            if (course < 0 || !_studyOn || _study == null || Save == null)
            {
                return RoomMark.None;
            }
            Enrolment e = _study.Live(course);
            CourseDef c = e != null ? e.Def : null;
            bool inPerson = c != null && InRooms(e);
            bool can = false;
            int soon = -1;
            if (inPerson)
            {
                can = CanAttend(e, out _) == null || CanSit(e) == null;
                if (!can && e.Stage == (int)EnrolStage.Studying && c.Timetabled && !e.FeeLate)
                {
                    int now = Save.Day * 24 * 60 + CareersGame.MinuteOfDay;
                    int at = StudyRules.NextClass(e, Save.Day, CareersGame.MinuteOfDay, SittingClass);
                    soon = at >= 0 ? at - now : -1;
                }
            }
            return CampusRules.MarkOf(true, e != null, inPerson, _study.Holds(course), can, soon);
        }

        /// <summary>For the Campus app: where a course of the university is held and whether the university is open; null for other courses.</summary>
        internal string RoomLine(Enrolment e)
        {
            if (!InRooms(e))
            {
                return null;
            }
            return CareersText.Fill("reallife_campus_on_room", "floor", CampusRules.FloorOf(e.Def.Id).ToString(), "room", CampusRules.RoomOf(e.Def.Id).ToString(),
                "hours", _interior.HoursLine(_interior.OpenNow()));
        }

        /// <summary>For the Campus app: the university's floors and its hours now, or null while it has no floors.</summary>
        internal string UniversityLine()
        {
            if (_interior == null || !_interior.Available)
            {
                return null;
            }
            return CareersText.Fill("reallife_campus_hours", "floors", CampusRules.Floors.ToString(), "weekdays", Span(1), "saturday", Span(6), "hours", _interior.HoursLine(_interior.OpenNow()));
        }

        // A day's hours in words (day 1 is a Monday, day 6 a Saturday).
        private static string Span(int day)
        {
            return CareersText.T("reallife_campus_hours_span", "from", CareersText.Hour(CampusRules.Opens(day)), "to", CareersText.Hour(CampusRules.Closes(day)));
        }

        // A university class, exam or homework in person: to its floor and room, begun there (WantBegin, then GoToRoom). Null if on the way, else the reason key.
        private string ToRoom(Enrolment e, string why, SittingKind kind)
        {
            if (why != null && why != "reallife_study_why_not_university")
            {
                return why;
            }
            string busy = SeatBusy();
            if (busy != null)
            {
                return busy;
            }
            WantBegin(e.Def.Id, kind);
            string go = _interior.GoToRoom(e.Def.Id, true);
            if (go == null)
            {
                CloseApps();
            }
            return go;
        }
    }
}
