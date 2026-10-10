using Localizor;
using RealLifeOverhaul.Events;
using RealLifeOverhaul.Family;

namespace RealLifeOverhaul.Planner
{
    /// <summary>Family: the places to be that the family keeps in the save (scans, school evenings, dinners, the wedding) and a wedding day that is planned (Family/FamilyState).</summary>
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
                // Two errands of one kind on one day are told apart by their hour.
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
                    string key = "reallife" + "_family_errand_" + errand.Kind;   // the family's own titles, one per kind of errand (Family/Errands)
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

    /// <summary>Social life: the one plan agreed with somebody (Social/SocialModel: SocialState.Plan).</summary>
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

    /// <summary>Travel and lodging: the hour a hotel room has to be left (Travel/TravelState, HotelStay.CheckoutHour).</summary>
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
                e.Money = TravelState.Owed > 0 ? PlannerText.T("reallife_planner_costs", "amount", PlannerText.Money(TravelState.Owed)) : null;
            }
        }
    }

    /// <summary>City events: the market, parade or fireworks of each day (Events/EventCalendar, which answers for any day from the save's own number).</summary>
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

    /// <summary>Civic life: election day with its polling hours, and the candidates' debate before it (Civic/CivicGovernment.State).</summary>
    public sealed class CivicSource : IPlannerSource
    {
        public string Id => "civic";
        public bool Available => Settings.Civic && CivicDirector.Instance != null && CivicDirector.Instance.Government != null;

        public void Collect(int fromDay, int toDay, PlannerList into)
        {
            GovState state = CivicDirector.Instance.Government.State;
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
            if (!state.DebateHeld)
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
    }

    /// <summary>Business pressure: what an open matter at one of the player's businesses has to be dealt with by (Business/BusinessState.Issues).</summary>
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

    /// <summary>Home life and health: the hour a doctor's sick note runs out (Home/HomeState.SickNoteUntil).</summary>
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
