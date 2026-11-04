using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;
using UI.Notification;

namespace RealLifeOverhaul.Careers
{
    // What the people beside the player need of the game: the colleagues' names from the game's own name roll, the player's employees, and the
    // notices (CareersDirector.People.cs holds the rest and is game-free).
    public sealed partial class CareersDirector
    {
        private readonly HashSet<string> _taken = new HashSet<string>();
        private readonly List<string> _employees = new List<string>(16);

        // A name nobody in the company, the campus or the game's staff has: the game's own roll, as the tech sector draws its seekers' names.
        private string NameRoll()
        {
            _taken.Clear();
            StaffNamesGame.Names(_taken);
            if (_standing != null)
            {
                for (int i = 0; i < _standing.Peers.Count; i++) { _taken.Add(_standing.Peers[i].Name); }
                for (int i = 0; i < _standing.Others.Count; i++) { _taken.Add(_standing.Others[i].Name); }
            }
            if (_roster != null)
            {
                for (int i = 0; i < _roster.Intakes.Count; i++)
                {
                    for (int s = 0; s < _roster.Intakes[i].Students.Count; s++) { _taken.Add(_roster.Intakes[i].Students[s].Name); }
                }
                for (int i = 0; i < _roster.Tutors.Count; i++) { _taken.Add(_roster.Tutors[i].Name); }
            }
            return StaffNamesGame.Fresh(_taken, out bool _);
        }

        private Func<string> _nameRoll;
        private Func<string> Names => _nameRoll ?? (_nameRoll = NameRoll);

        private void PeopleForgotten()
        {
            _taken.Clear();
        }

        private IList<string> EmployeeNames()
        {
            _employees.Clear();
            var hired = new List<global::Entities.EmployeeInstance>(16);
            StaffNamesGame.Hired(hired, new List<bool>(16));
            for (int i = 0; i < hired.Count && _employees.Count < 24; i++)
            {
                _employees.Add(StaffNamesGame.Name(hired[i]));
            }
            return _employees;
        }

        // A colleague passed over for the post: told in a notice.
        private void PassedOver(string rival, string employer)
        {
            Tell2(NotificationType.Warning, "reallife_work_note_passed_over", Data("name", rival, "employer", CareersText.Employer(employer)), TabJob, null);
        }

        // An employee of the player's follows the same course: told in a notice.
        private void EmployeeFollowed(string who, int course)
        {
            Tell(NotificationType.Info, "reallife_study_note_employee_follows", Data("name", who, "course", CareersText.Course(course)), TabStudying, CampusApp.CourseRow + course);
        }
    }
}
