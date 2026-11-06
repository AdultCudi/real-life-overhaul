using Localizor;
using RealLifeOverhaul.Law;

namespace RealLifeOverhaul.Planner
{
    /// <summary>The rota's shifts not yet worked, missed or excused (CareerState).</summary>
    public sealed class CareerSource : IPlannerSource
    {
        public string Id => "work";
        public bool Available => CareerState.Loaded && CareerState.Employed;

        public void Collect(int fromDay, int toDay, PlannerList into)
        {
            Employer employer = CareerState.Employer;
            CareerRank rank = CareerState.CurrentRank;
            if (employer == null || rank == null)
            {
                return;
            }
            int from = fromDay > CareerState.HandledDay ? fromDay : CareerState.HandledDay + 1;
            for (int day = from > CareerState.HiredDay ? from : CareerState.HiredDay + 1; day <= toDay; day++)
            {
                if (!CareerCatalogue.RotaDay(CareerState.RotaDays, day))
                {
                    continue;
                }
                PlannerEntry e = into.Add(Id, day, 0, PlannerCategory.Work, day);
                if (e == null)
                {
                    continue;
                }
                e.Start = CareerState.RotaHour * 60;
                e.End = ((CareerState.RotaHour + CareerCatalogue.ShiftHours) % 24) * 60;
                e.Attend = true;
                // CareerDirector.ClockOnRefusal: clocking on is refused this many hours after the rota's hour
                e.JoinBy = (CareerState.RotaHour + CareerCatalogue.LateHours) * 60 - 1;
                if (into.WantText)
                {
                    string place = employer.NameKey.GetLocalization();
                    e.Title = PlannerText.T("reallife_planner_work_title", "title", rank.TitleKey.GetLocalization());
                    e.Where = place;
                    e.What = PlannerText.T("reallife_planner_work_what", "employer", place);
                    float hourly = CareerState.Hourly;
                    e.Money = hourly > 0f ? PlannerText.T("reallife_planner_work_pays", "amount", PlannerText.Money(hourly), "hours", CareerCatalogue.ShiftHours.ToString()) : null;
                    e.IfMissed = PlannerText.T("reallife_planner_work_missed", "hours", CareerCatalogue.LateHours.ToString());
                }
            }
        }
    }

    /// <summary>Court dates, community service's last day and probation's (JusticeState).</summary>
    public sealed class JusticeSource : IPlannerSource
    {
        public string Id => "court";
        public bool Available => JusticeState.Loaded;

        public void Collect(int fromDay, int toDay, PlannerList into)
        {
            for (int i = 0; i < JusticeState.Cases.Count; i++)
            {
                JusticeCase c = JusticeState.Cases[i];
                if (c.Stage != CaseStage.Pending || c.CourtDay <= 0)
                {
                    continue;
                }
                PlannerEntry e = into.Add(Id, c.Number, 0, PlannerCategory.Court, c.CourtDay);
                if (e != null && into.WantText)
                {
                    e.Title = PlannerText.T("reallife_planner_court_title", "number", c.Number.ToString());
                    e.Where = JusticeDirector.Instance != null ? JusticeDirector.Instance.CourtPlaceName() : null;
                    e.What = PlannerText.T("reallife_planner_court_what");
                    e.IfMissed = PlannerText.T("reallife_planner_court_missed");
                }
            }
            if (JusticeState.CommunityHours > 0 && JusticeState.CommunityDue > 0)
            {
                PlannerEntry e = into.Add(Id, JusticeState.CommunityDue, 1, PlannerCategory.Court, JusticeState.CommunityDue);
                if (e != null && into.WantText)
                {
                    e.Title = PlannerText.T("reallife_planner_community_title", "hours", JusticeState.CommunityHours.ToString());
                    e.What = PlannerText.T("reallife_planner_community_what");
                    e.IfMissed = PlannerText.T("reallife_planner_community_missed");
                }
            }
            if (JusticeState.ProbationUntil > 0)
            {
                PlannerEntry e = into.Add(Id, JusticeState.ProbationUntil, 2, PlannerCategory.Court, JusticeState.ProbationUntil);
                if (e != null && into.WantText)
                {
                    e.Title = PlannerText.T("reallife_planner_probation_title");
                    e.What = PlannerText.T("reallife_planner_probation_what");
                }
            }
        }
    }

    /// <summary>The hour of release from a holding cell (LawState.Custody).</summary>
    public sealed class CustodySource : IPlannerSource
    {
        public string Id => "cell";
        public bool Available => LawState.Loaded && LawState.Custody.Active;

        public void Collect(int fromDay, int toDay, PlannerList into)
        {
            int release = (int)LawState.Custody.ReleaseAt;
            if (release <= 0)
            {
                return;
            }
            PlannerEntry e = into.Add(Id, 1, 0, PlannerCategory.Court, PlannerTime.DayOf(release));
            if (e == null)
            {
                return;
            }
            e.Start = PlannerTime.OfDay(release);
            e.End = e.Start;
            if (into.WantText)
            {
                e.Title = PlannerText.T("reallife_planner_release_title");
                e.What = PlannerText.T("reallife_planner_release_what");
            }
        }
    }

    /// <summary>Each licence's expiry, a suspension's end and the theory test's retry day (LicenceState).</summary>
    public sealed class LicenceSource : IPlannerSource
    {
        private static readonly string[] ClassKeys = { "reallife_lic_class_car", "reallife_lic_class_commercial", "reallife_lic_class_taxi", "reallife_lic_class_heavy" };
        private static readonly int[] Classes = { 0, 1, 2, 4 };   // LicenceClass values; the entry's number stays the class's own

        public string Id => "licence";
        public bool Available => LicenceState.Loaded && Settings.DrivingLicence;

        public void Collect(int fromDay, int toDay, PlannerList into)
        {
            for (int k = 0; k < ClassKeys.Length; k++)
            {
                LicenceState.Card card = LicenceState.Of((LicenceClass)Classes[k]);
                if (!card.Held || card.Expires <= 0)
                {
                    continue;
                }
                PlannerEntry e = into.Add(Id, Classes[k], 0, PlannerCategory.Licence, card.Expires);
                if (e != null && into.WantText)
                {
                    e.Title = PlannerText.T("reallife_planner_licence_title", "class", ClassKeys[k].GetLocalization());
                    e.What = PlannerText.T("reallife_planner_licence_what");
                    e.IfMissed = PlannerText.T("reallife_planner_licence_missed");
                }
            }
            if (LicenceState.SuspendedUntil > 0)
            {
                PlannerEntry e = into.Add(Id, 10, 0, PlannerCategory.Licence, LicenceState.SuspendedUntil);
                if (e != null && into.WantText)
                {
                    e.Title = PlannerText.T("reallife_planner_suspension_title");
                    e.What = PlannerText.T("reallife_planner_suspension_what");
                }
            }
            if (LicenceState.TheoryRetryDay > 0)
            {
                PlannerEntry e = into.Add(Id, 11, 0, PlannerCategory.Licence, LicenceState.TheoryRetryDay);
                if (e != null && into.WantText)
                {
                    e.Title = PlannerText.T("reallife_planner_theory_title");
                    e.What = PlannerText.T("reallife_planner_theory_what");
                }
            }
        }
    }
}
