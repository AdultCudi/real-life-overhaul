using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The test tools of the options page: each starts one thing now on the player's own people, so it can be seen
    /// without waiting for it. What they change is real (a review given, an offer made, a course ended), as it would be
    /// had the day come.
    /// </summary>
    public sealed partial class TechDirector
    {
        private Person TestPerson(bool senior)
        {
            if (!_peopleOn || Save == null)
            {
                Notifications.ShowError("reallife_talent_test_off");
                return null;
            }
            ReadPeople(Save.Day);
            Person best = null;
            for (int i = 0; i < _people.Count; i++)
            {
                Person p = _people[i];
                if (p.File.NoticeEnd == 0 && (best == null || (senior ? p.File.Level > best.File.Level : p.File.Level < best.File.Level)))
                {
                    best = p;
                }
            }
            if (best == null)
            {
                Notifications.ShowError("reallife_talent_test_none");
            }
            return best;
        }

        /// <summary>The review of the most junior professional falls due today, and is given.</summary>
        public void TestReview()
        {
            Person p = TestPerson(false);
            if (p == null)
            {
                return;
            }
            p.File.ReviewDay = Save.Day;
            int rating = TalentRules.Review(p.File, Seed, Save.Day, Year, p.MainPoints);
            Tell(NotificationType.Info, p.File.RaiseAsk > 0 ? "reallife_talent_note_review_ask" : "reallife_talent_note_review",
                Data("name", p.Name, "rating", rating.ToString(), "percent", (p.File.RaiseAsk / 100).ToString()), TabTeam, Row(p.File));
            Changed();
        }

        /// <summary>A rival comes for the most senior professional, whatever their level.</summary>
        public void TestOffer()
        {
            Person p = TestPerson(true);
            if (p == null)
            {
                return;
            }
            string rival = TechBridges.Rival != null && p.At != null ? TechBridges.Rival(p.At) : null;
            TalentRules.Offer(p.File, string.IsNullOrEmpty(rival) ? TechText.T("reallife_talent_rival_default") : rival, Save.Day);
            Tell(NotificationType.Warning, "reallife_talent_note_poach", Data("name", p.Name, "rival", p.File.PoachBy, "percent", (p.File.PoachBp / 100).ToString(), "days", TalentCatalogue.PoachAnswerDays.ToString()),
                TabTeam, Row(p.File));
            Changed();
        }

        /// <summary>The most junior professional finishes a course in their first specialism today.</summary>
        public void TestCourse()
        {
            Person p = TestPerson(false);
            if (p == null)
            {
                return;
            }
            TalentRules.StartCourse(p.File, 0, Save.Day - TalentCatalogue.CourseDays, -1);
            TalentRules.EndCourse(p.File, Seed, Save.Day);
            Tell(NotificationType.Success, "reallife_talent_note_course", Data("name", p.Name), TabTeam, Row(p.File));
            Changed();
        }
    }
}
