using Localizor;
using RealLifeOverhaul.Events;
using RealLifeOverhaul.Family;

namespace RealLifeOverhaul.Planner
{
    /// <summary>The family's places to be (scans, school evenings, dinners) and a planned wedding (FamilyState).</summary>
    public sealed class FamilySource : IPlannerSource
    {
        public string Id => "family";
        public bool Available => FamilyState.Loaded && Settings.Family && FamilyState.S != null;

        public void Collect(int fromDay, int toDay, PlannerList into)
        {
            var errands = FamilyState.S.Errands;
            bool weddingListed = false;
            for (int i = 0; errands != null && i < errands.Count; i++)
            {
                Errand errand = errands[i];
                if (errand == null || string.IsNullOrEmpty(errand.Kind))
                {
                    continue;
                }
                // two errands of one kind on a day are told apart by their hour
                PlannerEntry e = into.Add(Id, errand.Day, PlannerLogic.Stable(errand.Kind) ^ errand.FromHour, PlannerCategory.Family, errand.Day);
                if (e == null)
                {
                    continue;
                }
                weddingListed |= errand.Kind == "wedding";
                e.Start = errand.FromHour * 60;
                e.End = (errand.ToHour % 24) * 60;
                e.Attend = true;
                e.Street = errand.Street;
                e.Number = errand.Number;
                if (into.WantText)
                {
                    string key = "reallife" + "_family_errand_" + errand.Kind;   // the family's title for the kind
                    e.Title = LocalizorManager.IsLocalizedKey(key) ? key.GetLocalization() : PlannerText.T("reallife_planner_family_title");
                    e.What = PlannerText.T("reallife_planner_family_what");
                }
            }
            WeddingPlan wedding = FamilyState.S.Wedding;
            if (wedding != null && wedding.Active && wedding.Day > 0 && !weddingListed)
            {
                PlannerEntry e = into.Add(Id, wedding.Day, 1, PlannerCategory.Family, wedding.Day);
                if (e != null)
                {
                    e.Street = wedding.Street;
                    e.Number = wedding.Number;
                    if (into.WantText)
                    {
                        e.Title = PlannerText.T("reallife_planner_wedding_title");
                        e.What = PlannerText.T("reallife_planner_wedding_what");
                    }
                }
            }
        }
    }

    /// <summary>The one plan agreed with somebody (SocialState.Plan).</summary>
    public sealed class SocialSource : IPlannerSource
    {
        public string Id => "social";
        public bool Available => SocialState.Loaded && SocialState.Plan.Active;

        public void Collect(int fromDay, int toDay, PlannerList into)
        {
            SocialPlan plan = SocialState.Plan;
            int at = (int)plan.Time;
            PlannerEntry e = into.Add(Id, plan.Person, (int)plan.Kind, PlannerCategory.Social, PlannerTime.DayOf(at));
            if (e == null)
            {
                return;
            }
            e.Start = PlannerTime.OfDay(at);
            e.Attend = true;
            if (!string.IsNullOrEmpty(plan.Street))
            {
                e.Street = plan.Street;
                e.Number = plan.Number;
            }
            else if (plan.X != 0f || plan.Z != 0f)
            {
                e.HasPlace = true;
                e.X = plan.X;
                e.Y = plan.Y;
                e.Z = plan.Z;
            }
            if (into.WantText)
            {
                SocialPerson person = plan.Person >= 0 && plan.Person < SocialState.People.Length ? SocialState.People[plan.Person] : null;
                e.Title = PlannerText.T("reallife_planner_social_title", "name", person != null ? person.Name : string.Empty);
                e.Where = plan.Place;
                e.What = PlannerText.T("reallife_planner_social_what");
                e.IfMissed = PlannerText.T("reallife_planner_social_missed");
            }
        }
    }

    /// <summary>The hour a hotel room has to be left (HotelStay.CheckoutHour).</summary>
    public sealed class TravelSource : IPlannerSource
    {
        public string Id => "travel";
        public bool Available => TravelState.HasRoom;

        public void Collect(int fromDay, int toDay, PlannerList into)
        {
            PlannerEntry e = into.Add(Id, TravelState.CheckoutDay, 0, PlannerCategory.Travel, TravelState.CheckoutDay);
            if (e == null)
            {
                return;
            }
            e.Deadline = true;
            e.End = HotelStay.CheckoutHour * 60;
            if (into.WantText)
            {
                e.Title = PlannerText.T("reallife_planner_checkout_title");
                e.What = PlannerText.T("reallife_planner_checkout_what", "nights", TravelState.Nights.ToString());
                if (TravelState.Owed > 0)
                {
                    PlannerText.Costs(e, TravelState.Owed, PlannerText.Money(TravelState.Owed));
                }
            }
        }
    }

    /// <summary>The market, parade or fireworks of each day (EventCalendar answers for any day).</summary>
    public sealed class EventsSource : IPlannerSource
    {
        public string Id => "event";
        public bool Available => Settings.StreetEvents;

        public void Collect(int fromDay, int toDay, PlannerList into)
        {
            for (int day = fromDay < 1 ? 1 : fromDay; day <= toDay; day++)
            {
                PlannedEvent plan = EventCalendar.For(day);
                if (plan.Kind == EventKind.None)
                {
                    continue;
                }
                PlannerEntry e = into.Add(Id, day, (int)plan.Kind, PlannerCategory.City, day);
                if (e == null)
                {
                    continue;
                }
                e.Start = plan.From * 60;
                e.End = (plan.To % 24) * 60;
                if (into.WantText)
                {
                    e.Title = EventCalendar.Name(plan.Kind).GetLocalization();
                    e.What = PlannerText.T("reallife_planner_event_what");
                }
            }
        }
    }

    /// <summary>Election day and the debate before it; and the player's own politics' dates (CivicOffice.State).</summary>
    public sealed class CivicSource : IPlannerSource
    {
        public string Id => "civic";
        public bool Available => Settings.Civic && CivicDirector.Instance != null && CivicDirector.Instance.Government != null;

        public void Collect(int fromDay, int toDay, PlannerList into)
        {
            GovState state = CivicDirector.Instance.Government.State;
            CivicOffice office = CivicDirector.Instance.Office;
            if (office != null && CivicOffice.Enabled)
            {
                Office(office, state, into);
            }
            if (state == null || state.Phase != ElectionPhase.Campaign || state.ElectionDay <= 0)
            {
                return;
            }
            PlannerEntry vote = into.Add(Id, state.ElectionDay, 0, PlannerCategory.City, state.ElectionDay);
            if (vote != null)
            {
                vote.Start = CivicCatalogue.PollsOpen * 60;
                vote.End = CivicCatalogue.PollsClose * 60;
                if (into.WantText)
                {
                    vote.Title = PlannerText.T("reallife_planner_election_title");
                    vote.What = PlannerText.T("reallife_planner_election_what", "time", PlannerText.Clock(CivicCatalogue.ResultHour * 60));
                }
            }
            if (!state.DebateHeld && (office == null || !office.Campaigning))
            {
                int day = state.ElectionDay - CivicCatalogue.DebateBefore;
                PlannerEntry debate = into.Add(Id, day, 1, PlannerCategory.City, day);
                if (debate != null)
                {
                    debate.Start = 19 * 60;
                    if (into.WantText)
                    {
                        debate.Title = PlannerText.T("reallife_planner_debate_title");
                        debate.What = PlannerText.T("reallife_planner_debate_what");
                    }
                }
            }
        }

        // ids: the day, and 10 to 17 for the kind of row
        private void Office(CivicOffice office, GovState g, PlannerList into)
        {
            OfficeState s = office.State;
            if (office.Collecting)
            {
                int close = OfficeRules.NominationsClose(s.ForDay, CivicCatalogue.CampaignDays);
                PlannerEntry e = into.Add(Id, close, 10, PlannerCategory.City, close);
                if (e != null && into.WantText)
                {
                    e.Title = PlannerText.T("reallife_planner_office_file_title");
                    e.What = PlannerText.T("reallife_planner_office_file_what", "signatures", s.Signatures.ToString(), "needed", OfficeRules.SignaturesNeeded(s.Race).ToString());
                    e.IfMissed = PlannerText.T("reallife_planner_office_file_missed");
                }
            }
            if (office.Campaigning && g != null)
            {
                int debate = g.ElectionDay - CivicCatalogue.DebateBefore;
                if (s.Debate == 0)
                {
                    PlannerEntry e = into.Add(Id, debate, 11, PlannerCategory.City, debate);
                    if (e != null)
                    {
                        e.Start = 19 * 60;
                        e.End = 22 * 60;
                        e.Attend = true;
                        if (into.WantText)
                        {
                            e.Title = PlannerText.T("reallife_planner_office_debate_title");
                            e.What = PlannerText.T("reallife_planner_office_debate_what");
                            e.IfMissed = PlannerText.T("reallife_planner_office_debate_missed");
                        }
                    }
                }
                PlannerEntry night = into.Add(Id, g.ElectionDay, 12, PlannerCategory.City, g.ElectionDay);
                if (night != null)
                {
                    night.Start = CivicCatalogue.ResultHour * 60;
                    if (into.WantText)
                    {
                        night.Title = PlannerText.T("reallife_planner_office_result_title");
                        night.What = PlannerText.T("reallife_planner_office_result_what");
                    }
                }
                if (s.AttackKind >= 0)
                {
                    int by = s.AttackDay + OfficeRules.AttackAnswerDays;
                    PlannerEntry e = into.Add(Id, by, 13, PlannerCategory.City, by);
                    if (e != null && into.WantText)
                    {
                        e.Title = PlannerText.T("reallife_planner_office_attack_title");
                        e.What = PlannerText.T("reallife_planner_office_attack_what");
                    }
                }
            }
            if (!s.InOffice)
            {
                return;
            }
            if (s.NextMeeting > 0)
            {
                PlannerEntry e = into.Add(Id, s.NextMeeting, 14, PlannerCategory.City, s.NextMeeting);
                if (e != null)
                {
                    e.Start = OfficeRules.MeetingHour * 60;
                    e.End = OfficeRules.MeetingLast * 60;
                    e.Attend = true;
                    if (into.WantText)
                    {
                        e.Title = PlannerText.T("reallife_planner_office_meeting_title");
                        e.What = PlannerText.T("reallife_planner_office_meeting_what", "count", s.Agenda.Count.ToString());
                        e.IfMissed = PlannerText.T("reallife_planner_office_meeting_missed");
                    }
                }
            }
            if (!s.Registered && s.RegisterBy > 0)
            {
                PlannerEntry e = into.Add(Id, s.RegisterBy, 15, PlannerCategory.City, s.RegisterBy);
                if (e != null && into.WantText)
                {
                    e.Title = PlannerText.T("reallife_planner_office_register_title");
                    e.What = PlannerText.T("reallife_planner_office_register_what");
                    e.IfMissed = PlannerText.T("reallife_planner_office_register_missed");
                }
            }
            if (s.Holds == OfficeRace.Mayor && s.BudgetDay > 0)
            {
                PlannerEntry e = into.Add(Id, s.BudgetDay, 16, PlannerCategory.City, s.BudgetDay);
                if (e != null && into.WantText)
                {
                    e.Title = PlannerText.T("reallife_planner_office_budget_title");
                    e.What = PlannerText.T("reallife_planner_office_budget_what");
                }
            }
            if (s.RecallDay > 0)
            {
                PlannerEntry e = into.Add(Id, s.RecallDay, 17, PlannerCategory.City, s.RecallDay);
                if (e != null && into.WantText)
                {
                    e.Title = PlannerText.T("reallife_planner_office_recall_title");
                    e.What = PlannerText.T("reallife_planner_office_recall_what");
                }
            }
        }
    }

    /// <summary>The day an open matter at one of the player's businesses has to be dealt with by (BusinessState.Issues).</summary>
    public sealed class BusinessSource : IPlannerSource
    {
        public string Id => "business";
        public bool Available => BusinessState.Loaded;

        public void Collect(int fromDay, int toDay, PlannerList into)
        {
            for (int i = 0; i < BusinessState.Issues.Count; i++)
            {
                BusinessIssue issue = BusinessState.Issues[i];
                if (issue == null || issue.DueDay <= 0 || issue.Def == null)
                {
                    continue;
                }
                PlannerEntry e = into.Add(Id, issue.StartDay, PlannerLogic.Stable(issue.Kind) ^ PlannerLogic.Stable(issue.Place), PlannerCategory.Business, issue.DueDay);
                if (e == null)
                {
                    continue;
                }
                e.Deadline = true;
                e.End = (issue.DueHour > 0 && issue.DueHour < 24 ? issue.DueHour : 23) * 60 + (issue.DueHour > 0 && issue.DueHour < 24 ? 0 : 59);
                if (into.WantText)
                {
                    e.Title = issue.Def.NameKey.GetLocalization();
                    e.What = PlannerText.T("reallife_planner_business_what");
                }
            }
        }
    }

    /// <summary>The hour a doctor's sick note runs out (HomeState.SickNoteUntil).</summary>
    public sealed class HealthSource : IPlannerSource
    {
        public string Id => "health";
        public bool Available => HomeState.Loaded && HomeState.SickNoteUntil > 0f;

        public void Collect(int fromDay, int toDay, PlannerList into)
        {
            int until = (int)HomeState.SickNoteUntil;
            PlannerEntry e = into.Add(Id, 1, 0, PlannerCategory.Health, PlannerTime.DayOf(until));
            if (e == null)
            {
                return;
            }
            e.Start = PlannerTime.OfDay(until);
            e.End = e.Start;
            if (into.WantText)
            {
                e.Title = PlannerText.T("reallife_planner_sicknote_title");
                e.What = PlannerText.T("reallife_planner_sicknote_what");
            }
        }
    }
}
