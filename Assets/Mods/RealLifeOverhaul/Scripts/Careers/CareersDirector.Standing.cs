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
        private readonly List<string> _employees = new List<string>(16);
        private int _whoCount;

        // A person of the city's people pool for a colleague, picked by the save's seed and a count, one the shift does not have yet; the pool's id is
        // kept to be saved beside the name (TagPeople).
        private string NameRoll()
        {
            PeoplePoolGame.Install();
            PoolPerson who = default(PoolPerson);
            for (int tries = 0; tries < 8; tries++)
            {
                who = PeoplePool.For(TechRandom.Hash(StudySeed, 7790, _whoCount++));
                if (!HasPeer(who.Name))
                {
                    break;
                }
            }
            if (who.Id > 0)
            {
                _pickedIds[who.Name] = who.Id;
            }
            return who.Name;
        }

        private bool HasPeer(string name)
        {
            for (int i = 0; _standing != null && i < _standing.Peers.Count; i++)
            {
                if (_standing.Peers[i].Name == name)
                {
                    return true;
                }
            }
            return false;
        }

        private Func<string> _nameRoll;
        private Func<string> Names => _nameRoll ?? (_nameRoll = NameRoll);

        private void PeopleForgotten()
        {
            _pickedIds.Clear();
        }

        private IList<string> EmployeeNames()
        {
            _employees.Clear();
            PeoplePoolGame.EmployeeNames(_employees, 24);
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
