using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The university's floors and classrooms (Careers/CampusInterior) as study sees them: a course of the university
    /// followed in person is held in its own room on its own floor (CampusRules), attended at that classroom's door with
    /// the game's study panel, as the school's courses are at the school's desks. Here: whether the university has
    /// something of the player's now (a class to join, an exam to sit, the graduation ceremony), what a classroom's panel
    /// offers, the way from the Campus app to the room, and the address the save keeps so that a move of the university
    /// is said once.
    /// </summary>
    public sealed partial class CareersDirector
    {
        private CampusInterior _interior;
        private string _campusWas;          // "street|number" the university was at, as the save had it (or the old rule's building)
        private bool _campusRead;

        /// <summary>The university's floors; null while the university is not there.</summary>
        internal CampusInterior Interior => _interior;

        /// <summary>True while a class, homework or exam of the mod's is running (in place or at a desk).</summary>
        internal bool SittingNow => (_skip != null && _skip.Open) || DeskBusy;

        /// <summary>True for a course of the university followed in person while its floors are there: held in its classroom.</summary>
        private bool InRooms(Enrolment e)
        {
            CourseDef c = e != null ? e.Def : null;
            return c != null && c.Campus == Campus.University && (StudyMode)e.Mode != StudyMode.Distance && CampusRules.FloorOf(c.Id) > 0
                   && _interior != null && _interior.Available && !_roomDesksOff && !_watchOff && SchoolDesk.Works;
        }

        /// <summary>
        /// True if the university has something of the player's now, wherever the player is: a class that can be joined, an
        /// exam that can be sat, the graduation ceremony. The course (the ceremony's degree) is given.
        /// </summary>
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

        // The university's building against the one the save had: a move is written to the log and told once.
        private void CampusMoved()
        {
            if (_campus == null || !_campus.Usable || Save == null)
            {
                return;
            }
            string now = _campus.Street + "|" + _campus.Number;
            if (!_campusRead)
            {
                _campusRead = true;
                string kept = null;
                _campusWas = Save.modData != null && Save.modData.TryGetValue(CareersStore.CampusKey, out kept) && !string.IsNullOrEmpty(kept) ? kept : _campus.Before;
            }
            if (string.IsNullOrEmpty(_campusWas) || string.Equals(_campusWas, now, StringComparison.Ordinal))
            {
                _campusWas = now;
                return;
            }
            string was = _campusWas.Replace('|', ' ');
            _campusWas = now;
            bool enrolled = FirstUniversityCourse() >= 0 || (_study != null && _study.GraduationCourse >= 0);
            Logger?.Info("Careers: the university has moved from " + was + " to " + _campus.Name + " (a building of its own); "
                         + (enrolled ? "the courses under way, their classes and progress stay as they were." : "nothing of the player's was under way there."));
            if (enrolled)
            {
                Tell(NotificationType.Info, "reallife_campus_note_moved", Data("address", _campus.Name), TabStudying, null);
            }
        }

        private void WriteCampus(Dictionary<string, string> data)
        {
            if (!string.IsNullOrEmpty(_campusWas))
            {
                data[CareersStore.CampusKey] = _campusWas;
            }
        }

        // ---------------------------------------------------------------- a classroom's panel

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
            StudyMode mode = (StudyMode)e.Mode;
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
                offer.Exam = true;
                offer.Can = true;
                offer.Key = "reallife_study_prompt_exam";
                offer.Arguments = CourseArgs(course);
                offer.Lines = new[] { where, CareersText.T(e.Stage == (int)EnrolStage.Retake ? "reallife_study_skip_retake" : "reallife_study_skip_exam", "pass", c.Pass.ToString()) };
                return offer;
            }
            offer.Can = false;
            int at = e.Stage == (int)EnrolStage.Studying && c.Timetabled ? StudyRules.NextClass(c, mode, Save.Day, CareersGame.MinuteOfDay, Math.Max(e.Start, e.SettledDay + 1)) : -1;
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

        /// <summary>
        /// The game's mark over a classroom door for the course of its room (CampusRules.MarkOf): an action when its class
        /// or exam can be begun now or its next class is within the hour, enrolled, completed, or none. Once a second, five
        /// rooms, while the player is on a floor.
        /// </summary>
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
                    int at = StudyRules.NextClass(c, (StudyMode)e.Mode, Save.Day, CareersGame.MinuteOfDay, Math.Max(e.Start, e.SettledDay + 1));
                    soon = at >= 0 ? at - now : -1;
                }
            }
            return CampusRules.MarkOf(true, e != null, inPerson, _study.Holds(course), can, soon);
        }

        // ---------------------------------------------------------------- from the Campus app

        /// <summary>For the Campus app: where a course of the university is held and whether the university is open; null for other courses.</summary>
        internal string RoomLine(Enrolment e)
        {
            if (!InRooms(e))
            {
                return null;
            }
            return CareersText.Fill("reallife_campus_on_room", "floor", CampusRules.FloorOf(e.Def.Id).ToString(), "room", CampusRules.RoomOf(e.Def.Id).ToString(), "hours", _interior.HoursLine(_interior.OpenNow()));
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

        // A class or an exam of the university in person: to its floor and room (from inside, or from the door). Null if on
        // the way; else the reason key.
        private string ToRoom(Enrolment e, string why)
        {
            if (why != null && why != "reallife_study_why_not_university")
            {
                return why;
            }
            string go = _interior.GoToRoom(e.Def.Id);
            if (go == null)
            {
                CloseApps();
            }
            return go;
        }
    }
}
