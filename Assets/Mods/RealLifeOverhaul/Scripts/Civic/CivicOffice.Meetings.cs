using System;
using System.Text;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The council's meetings: the agenda, each motion decided, and a meeting the player missed.</summary>
    public sealed partial class CivicOffice
    {
        // The day of a meeting: the council's own business (for a councillor), the budget when it is due.
        private void Prepare(int day)
        {
            if (State.Holds == OfficeRace.Council && !HasMotion(OfficeMotion.ByCity) && !HasMotion(OfficeMotion.ByMayor))
            {
                int policy = _gov.CityMotion(day);
                if (policy >= 0 && !OnAgenda(policy))
                {
                    Put(policy, CivicGovernment.Promised(Gov.Mayor, policy) ? OfficeMotion.ByMayor : OfficeMotion.ByCity, day);
                }
            }
            if (State.BudgetDay > 0 && State.BudgetDay <= day + OfficeRules.MeetingEvery && !HasBudget())
            {
                var m = new OfficeMotion { Kind = OfficeMotion.KindBudget, By = Gov.MayorIsPlayer ? OfficeMotion.ByPlayer : OfficeMotion.ByMayor, Day = day };
                if (Gov.MayorIsPlayer)
                {
                    Array.Copy(State.Budget, m.Budget, m.Budget.Length);   // as it stands, until the mayor proposes another
                }
                else
                {
                    OfficeRules.CityBudget(Gov.Mayor.Lead, m.Budget);
                }
                Add(m);
            }
        }

        private bool HasMotion(int by)
        {
            for (int i = 0; i < State.Agenda.Count; i++)
            {
                if (State.Agenda[i].Kind == OfficeMotion.KindPolicy && State.Agenda[i].By == by)
                {
                    return true;
                }
            }
            return false;
        }

        private bool HasBudget()
        {
            for (int i = 0; i < State.Agenda.Count; i++)
            {
                if (State.Agenda[i].Kind == OfficeMotion.KindBudget)
                {
                    return true;
                }
            }
            return false;
        }

        public bool OnAgenda(int policy)
        {
            for (int i = 0; i < State.Agenda.Count; i++)
            {
                if (State.Agenda[i].Kind == OfficeMotion.KindPolicy && State.Agenda[i].Policy == policy)
                {
                    return true;
                }
            }
            return false;
        }

        private void Put(int policy, int by, int day)
        {
            if (CivicCatalogue.At(policy) != null && !OnAgenda(policy))
            {
                Add(new OfficeMotion { Kind = OfficeMotion.KindPolicy, Policy = policy, By = by, Day = day });
            }
        }

        private void Add(OfficeMotion m)
        {
            if (State.Agenda.Count >= OfficeState.AgendaMost)
            {
                // the oldest of the city's own items makes way
                for (int i = 0; i < State.Agenda.Count; i++)
                {
                    if (State.Agenda[i].By == OfficeMotion.ByCity)
                    {
                        State.Agenda.RemoveAt(i);
                        break;
                    }
                }
            }
            if (State.Agenda.Count < OfficeState.AgendaMost)
            {
                State.Agenda.Add(m);
            }
        }

        /// <summary>True while today's meeting can be held with the player.</summary>
        public bool MeetingDue(int day, int hour)
        {
            return Sits() && State.NextMeeting == day && hour >= OfficeRules.MeetingHour && hour < OfficeRules.MeetingLast && day != Gov.ElectionDay;
        }

        /// <summary>One motion decided and applied: each other member by their odds, the player as they voted (1 aye, 0 no, else no vote).</summary>
        public MotionVote Decide(OfficeMotion m, int vote, bool declaredAside, int day, int speak = 0)
        {
            var result = new MotionVote();
            bool mayorTabled = m.By == OfficeMotion.ByMayor || (m.By == OfficeMotion.ByPlayer && State.Holds == OfficeRace.Mayor);
            bool playerTabled = m.By == OfficeMotion.ByPlayer;
            float support = m.Kind == OfficeMotion.KindBudget ? Mathf.Clamp01(0.5f - BudgetDeficit(m.Budget) * 0.3f) : _gov.Support(m.Policy);
            for (int seat = 0; seat < CivicCatalogue.CouncilSeats; seat++)
            {
                if (State.Holds == OfficeRace.Council && seat == State.Seat)
                {
                    continue;
                }
                bool mayorsSide = Gov.Council[seat] == Gov.Mayor.Party && (mayorTabled || (m.Kind == OfficeMotion.KindPolicy && CivicGovernment.Promised(Gov.Mayor, m.Policy)));
                float odds = OfficeRules.MemberOdds(support, m.By == OfficeMotion.ByPetition, mayorsSide, mayorTabled, playerTabled, Influence);
                if (State.Holds == OfficeRace.Mayor && Gov.Council[seat] == Gov.Mayor.Party)
                {
                    odds = Mathf.Clamp(odds + speak * OfficeRules.MayorWord, 0.05f, 0.95f);   // the mayor's word to their own party's members
                }
                var roll = new CivicRandom(CivicRandom.Mix(Gov.Seed, day * 31 + seat, (m.Kind == OfficeMotion.KindBudget ? 977 : m.Policy) + 911));
                if (roll.Value < odds)
                {
                    result.Ayes++;
                }
                else
                {
                    result.Noes++;
                }
            }
            bool voted = State.Holds == OfficeRace.Council && (vote == 0 || vote == 1);
            if (voted)
            {
                if (vote == 1)
                {
                    result.Ayes++;
                }
                else
                {
                    result.Noes++;
                }
            }
            result.Passed = OfficeRules.Passes(result.Ayes, result.Noes);
            if (voted)
            {
                result.Decisive = OfficeRules.Passes(result.Ayes - (vote == 1 ? 1 : 0), result.Noes - (vote == 0 ? 1 : 0)) != result.Passed;
            }
            Apply(m, result, voted, vote, declaredAside, day);
            return result;
        }

        private float BudgetDeficit(int[] levels)
        {
            BudgetWeek week = OfficeRules.Week(_gov.Electorate(), Gov.Levers, levels);
            return week.Revenue > 0 ? Mathf.Clamp(-week.Balance / (float)week.Revenue, 0f, 1f) * 3f : 0f;
        }

        private void Apply(OfficeMotion m, MotionVote result, bool voted, int vote, bool declaredAside, int day)
        {
            if (m.Kind == OfficeMotion.KindBudget)
            {
                if (result.Passed)
                {
                    Array.Copy(m.Budget, State.Budget, State.Budget.Length);
                    State.Story(day + 1, "reallife_civic_news_office_budget", CivicText.PlayerName(), Levels(m.Budget));
                }
                State.BudgetDay = day + OfficeRules.BudgetEvery;
                return;
            }
            PolicyDef policy = CivicCatalogue.At(m.Policy);
            if (policy == null)
            {
                return;
            }
            if (result.Passed)
            {
                _gov.Passed(m.Policy, day);
            }
            if (voted)
            {
                // The district's view of the vote: an aye for what it cares about more than the city, a no against.
                int home = HomeDistrict();
                float lean = home >= 0 ? _gov.DistrictLean(home, (int)policy.Issue) : 1f;
                State.VotesRecord = Mathf.Clamp(State.VotesRecord + (vote == 1 ? 1f : -1f) * (lean - 1f) * 6f, -20f, 20f);
                if (Conflicted(m.Policy) && !declaredAside && State.Secrets.Count < OfficeState.SecretsMost)
                {
                    State.Secrets.Add(new OfficeSecret { Day = day, Policy = m.Policy, Decisive = result.Decisive });
                }
            }
            if (declaredAside)
            {
                CivicBridges.Moved(1, 0);
            }
            if (m.Undeclared && result.Passed && State.Secrets.Count < OfficeState.SecretsMost)
            {
                State.Secrets.Add(new OfficeSecret { Day = day, Policy = m.Policy, Decisive = true });
            }
        }

        public static string Levels(int[] levels)
        {
            var text = new StringBuilder(64);
            for (int d = 0; d < levels.Length && d < 4; d++)
            {
                if (d > 0)
                {
                    text.Append(", ");
                }
                text.Append(CivicText.T(DepartmentKeys[d])).Append(' ').Append(levels[d] > 0 ? "+" : string.Empty).Append(levels[d]);
            }
            return text.ToString();
        }

        /// <summary>A meeting is over: the motions were decided at it (by the scene) and the agenda is cleared.</summary>
        public void MeetingHeld(int day)
        {
            State.Agenda.Clear();
            State.Attended++;
            State.NextMeeting = day + OfficeRules.MeetingEvery;
            if (State.Holds == OfficeRace.Council)
            {
                State.Approval = Mathf.Min(95f, State.Approval + 0.5f);
            }
            Post(PostMeeting, DistrictName(HomeDistrict()));
        }

        /// <summary>Apologies sent for today's meeting: the council decides without the player.</summary>
        public void Skip(int day) => HoldWithout(day);

        private void HoldWithout(int day)
        {
            if (State.NextMeeting == day)
            {
                Prepare(day);
            }
            int passed = 0;
            for (int i = 0; i < State.Agenda.Count; i++)
            {
                passed += Decide(State.Agenda[i], -1, false, day).Passed ? 1 : 0;
            }
            int count = State.Agenda.Count;
            State.Agenda.Clear();
            State.Missed++;
            if (State.Holds == OfficeRace.Council)
            {
                State.Approval = Mathf.Max(3f, State.Approval - 1.5f);
            }
            State.NextMeeting = day + OfficeRules.MeetingEvery;
            CivicText.Toast(NotificationType.Warning, "reallife_civic_office_meeting_missed", "count", count.ToString(), "passed", passed.ToString(), 12f);
        }
    }
}
