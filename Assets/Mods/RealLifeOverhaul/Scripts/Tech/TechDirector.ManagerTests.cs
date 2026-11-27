using System;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    public sealed partial class TechDirector
    {
        /// <summary>
        /// Test: the most skilled professional of the firm with the most people is named its manager (when it has none), the firm is given a target of one tester if it has none, and
        /// the manager's morning and week are run now: the balancing, the hiring, the raises and the flags, and the week told in one notice. Says why not with the option off or no
        /// firm of two people or more.
        /// </summary>
        public void TestManager()
        {
            if (!_peopleOn || Save == null)
            {
                Notifications.ShowError("reallife_hire_test_off");
                return;
            }
            if (!Settings.TechManager)
            {
                Notifications.ShowError("reallife_tech_mgr_off");
                return;
            }
            ReadPeople(Save.Day);
            string place = null;
            int most = 1;
            for (int i = 0; i < _people.Count; i++)
            {
                string at = _people[i].File.Place;
                int n = at.Length > 0 ? FirmPeopleAt(at) : 0;
                if (n > most)
                {
                    most = n;
                    place = at;
                }
            }
            if (place == null)
            {
                Notifications.ShowError("reallife_tech_mgr_test_none");
                return;
            }
            ManagerPost post = ManagerOf(place);
            if (post == null)
            {
                Person best = null;
                int bestSkill = -1;
                for (int i = 0; i < _people.Count; i++)
                {
                    Person person = _people[i];
                    if (person.File.Place != place || person.File.NoticeEnd > 0)
                    {
                        continue;
                    }
                    int skill = FirmManagerRules.Skill(person.File, person.MainPoints);
                    if (skill > bestSkill)
                    {
                        best = person;
                        bestSkill = skill;
                    }
                }
                if (best == null || !NameManager(best.File.Id))
                {
                    Notifications.ShowError("reallife_tech_mgr_test_none");
                    return;
                }
                post = ManagerOf(place);
            }
            if (post.Targets.Count == 0)
            {
                post.SetTarget(TalentCatalogue.QaTester, 1);
            }
            post.LastDay = -1;
            post.ReportDay = Save.Day - FirmManagerCatalogue.ReportDays;
            int moves = post.WMoves, raises = post.WRaises;
            ManagerDay(Save.Day);
            Logger?.Info("Tech check (manager test, " + FirmNameAt(place) + "): " + (post.WMoves - moves) + " role move(s) and " + (post.WRaises - raises) + " raise(s) made, " + post.Targets.Count
                         + " target(s) kept, week told on day " + post.ReportDay + ": " + (post.ReportDay == Save.Day && post.LastDay == Save.Day ? "PASS" : "FAIL"));
            Tell(NotificationType.Info, "reallife_tech_mgr_test_done", Data("name", post.Name, "firm", FirmNameAt(place)), TabTeam, null);
            Changed();
        }
    }
}
