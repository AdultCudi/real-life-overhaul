using System;
using System.Collections.Generic;
using System.Text;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>An office held: pay, approval and recall, the player's motions and orders, the register, standing down, and the city's budget.</summary>
    public sealed partial class CivicOffice
    {
        /// <summary>The departments' names, and a line on what each pays for.</summary>
        public static readonly string[] DepartmentKeys = { "reallife_civic_office_dept_0", "reallife_civic_office_dept_1", "reallife_civic_office_dept_2", "reallife_civic_office_dept_3" };
        public static readonly string[] DepartmentAbout =
        {
            "reallife_civic_office_dept_about_0", "reallife_civic_office_dept_about_1", "reallife_civic_office_dept_about_2", "reallife_civic_office_dept_about_3"
        };

        private void OfficeDay(int day)
        {
            if (day >= State.PayDay && State.PayDay > 0)
            {
                State.PayDay += 7;
                CivicEffects.Pay(Prices.At(OfficeRules.Salary(State.Holds)), "reallife_transaction_civic_office_salary");
            }
            if (!State.Registered && State.RegisterBy > 0 && day > State.RegisterBy)
            {
                State.RegisterBy = 0;
                Scandal(day, "reallife_civic_news_office_register", 3f, -2, 0f);
                CivicText.Toast(NotificationType.Warning, "reallife_civic_office_register_late", 14f);
            }
            if (State.NextMeeting == Gov.ElectionDay && Gov.Phase == ElectionPhase.Campaign)
            {
                State.NextMeeting++;   // nobody meets on election day: the day after
            }
            // A meeting that went by without the player: held without them.
            while (State.NextMeeting > 0 && State.NextMeeting < day)
            {
                HoldWithout(State.NextMeeting);
            }
            if (State.NextMeeting == day)
            {
                Prepare(day);
            }
            if (State.Holds == OfficeRace.Council)
            {
                CouncillorDay(day);
            }
        }

        private void CouncillorDay(int day)
        {
            Candidate held = State.Held;
            int kept = 0, unkept = 0;
            for (int i = 0; i < held.Planks; i++)
            {
                PolicyDef p = CivicCatalogue.At(held.Platform[i]);
                if (p != null)
                {
                    if (Gov.Levers[(int)p.Lever] == p.Dir)
                    {
                        kept++;
                    }
                    else
                    {
                        unkept++;
                    }
                }
            }
            State.VotesRecord *= 0.95f;
            float target = OfficeRules.CouncillorTarget(Gov.Approval, kept, unkept, State.VotesRecord, State.Scandals, State.Missed, CivicBridges.Cycle());
            var noise = new CivicRandom(CivicRandom.Mix(Gov.Seed, day, 1501));
            State.Approval = OfficeRules.Approach(State.Approval, target, noise.Range(-1f, 1f));
            State.LowDays = State.Approval < OfficeRules.RecallBelow ? State.LowDays + 1 : 0;
            if (State.RecallDay == 0 && State.LowDays >= OfficeRules.RecallDays && !Campaigning && day - State.TermStart > 14)
            {
                State.RecallDay = day + OfficeRules.RecallVoteAfter;
                State.RecallPush = 0f;
                CivicText.Toast(NotificationType.Warning, "reallife_civic_office_recall_called", "day", State.RecallDay.ToString(), 14f);
                State.Story(day + 1, "reallife_civic_news_office_recall", CivicText.PlayerName(), DistrictName(HomeDistrict()));
            }
            else if (State.RecallDay > 0 && day >= State.RecallDay)
            {
                float keep = OfficeRules.RecallKeep(State.Approval, State.RecallPush);
                State.RecallDay = 0;
                State.LowDays = 0;
                if (keep < 0.5f)
                {
                    LeaveOffice(day, "reallife_civic_office_recalled");
                    State.Story(day + 1, "reallife_civic_news_office_recalled", CivicText.PlayerName(), CivicGovernment.Percent(keep));
                }
                else
                {
                    State.Approval = Mathf.Max(State.Approval, 35f);
                    CivicText.Toast(NotificationType.Success, "reallife_civic_office_recall_survived", "share", CivicGovernment.Percent(keep), 14f);
                    State.Story(day + 1, "reallife_civic_news_office_survived", CivicText.PlayerName(), CivicGovernment.Percent(keep));
                }
            }
        }

        /// <summary>The player's approval in office: the city's for a mayor, their own for a councillor.</summary>
        public float Approval => State.Holds == OfficeRace.Mayor ? Gov.Approval : State.Approval;

        /// <summary>Promises of a platform not yet in force.</summary>
        public int Unkept(Candidate c)
        {
            int unkept = 0;
            for (int i = 0; i < c.Planks; i++)
            {
                PolicyDef p = CivicCatalogue.At(c.Platform[i]);
                unkept += p != null && Gov.Levers[(int)p.Lever] != p.Dir ? 1 : 0;
            }
            return unkept;
        }

        /// <summary>True if a lever touches the player's own businesses or property: a vote on it is a conflict of interest.</summary>
        public bool Conflicted(int policy)
        {
            PolicyDef p = CivicCatalogue.At(policy);
            return p != null && OfficeRules.Conflicted(p.Lever, Businesses(-1), CivicBridges.Ask(CivicBridges.Landlord));
        }

        /// <summary>How many motions the player may table now, and why not (a locale key, or null).</summary>
        public string MayTable(out int room)
        {
            room = 0;
            if (!Sits())
            {
                return "reallife_civic_office_not_in_office";
            }
            int mine = 0;
            for (int i = 0; i < State.Agenda.Count; i++)
            {
                mine += State.Agenda[i].By == OfficeMotion.ByPlayer && State.Agenda[i].Kind == OfficeMotion.KindPolicy ? 1 : 0;
            }
            room = (State.Holds == OfficeRace.Mayor ? 2 : 1) - mine;
            return room > 0 ? null : "reallife_civic_office_table_full";
        }

        /// <summary>The player puts a policy before the next meeting (a mayor sets the agenda). A mayor's undeclared interest becomes a secret if it passes.</summary>
        public string Table(int policy, bool declared, int day)
        {
            string why = MayTable(out _);
            if (why != null)
            {
                return why;
            }
            PolicyDef p = CivicCatalogue.At(policy);
            if (p == null || _gov.Settled(p.Lever, p.Dir))
            {
                return "reallife_civic_office_table_settled";
            }
            if (OnAgenda(policy))
            {
                return "reallife_civic_office_table_there";
            }
            if (State.Holds == OfficeRace.Mayor)
            {
                // The mayor sets the agenda: the council's own business makes way for theirs.
                for (int i = State.Agenda.Count - 1; i >= 0; i--)
                {
                    if (State.Agenda[i].By == OfficeMotion.ByCity)
                    {
                        State.Agenda.RemoveAt(i);
                    }
                }
            }
            var m = new OfficeMotion { Kind = OfficeMotion.KindPolicy, Policy = policy, By = OfficeMotion.ByPlayer, Day = day, Undeclared = State.Holds == OfficeRace.Mayor && !declared && Conflicted(policy) };
            Add(m);
            State.Tabled = day;
            if (declared && Conflicted(policy))
            {
                CivicBridges.StandingMoved?.Invoke(1, 0);
            }
            return "reallife_civic_office_tabled";
        }

        /// <summary>A mayor's proposed budget goes before the council at the next meeting.</summary>
        public string ProposeBudget(int[] levels, int day)
        {
            if (!Sits() || State.Holds != OfficeRace.Mayor)
            {
                return "reallife_civic_office_not_mayor";
            }
            for (int i = State.Agenda.Count - 1; i >= 0; i--)
            {
                if (State.Agenda[i].Kind == OfficeMotion.KindBudget)
                {
                    State.Agenda.RemoveAt(i);
                }
            }
            var m = new OfficeMotion { Kind = OfficeMotion.KindBudget, By = OfficeMotion.ByPlayer, Day = day };
            for (int d = 0; d < m.Budget.Length; d++)
            {
                m.Budget[d] = Mathf.Clamp(levels[d], -OfficeRules.DepartmentMost, OfficeRules.DepartmentMost);
            }
            Add(m);
            return "reallife_civic_office_budget_proposed";
        }

        /// <summary>A mayor's order on a lever that is theirs to set (once a fortnight).</summary>
        public string Order(int policy, int day)
        {
            PolicyDef p = CivicCatalogue.At(policy);
            if (!Sits() || State.Holds != OfficeRace.Mayor)
            {
                return "reallife_civic_office_not_mayor";
            }
            if (p == null || !OfficeRules.ByOrder(p.Lever))
            {
                return "reallife_civic_office_order_council";
            }
            if (State.OrderDay > 0 && day < State.OrderDay + OfficeRules.OrderEvery)
            {
                return "reallife_civic_office_order_wait";
            }
            if (!_gov.Ordered(p.Lever, p.Dir, day))
            {
                return "reallife_civic_office_table_settled";
            }
            State.OrderDay = day;
            State.Story(day + 1, "reallife_civic_news_office_order", CivicText.PlayerName(), CivicText.T(p.Name));
            return "reallife_civic_office_ordered";
        }

        /// <summary>The register of interests is filed: the player's businesses and property, in public.</summary>
        public string Register()
        {
            if (!State.InOffice)
            {
                return "reallife_civic_office_not_in_office";
            }
            if (State.Registered)
            {
                return "reallife_civic_office_registered_already";
            }
            State.Registered = true;
            CivicBridges.StandingMoved?.Invoke(1, 0);
            return "reallife_civic_office_registered";
        }

        public string StandDown(int day)
        {
            if (!State.InOffice)
            {
                return "reallife_civic_office_not_in_office";
            }
            OfficeRace held = State.Holds;
            int seat = State.Seat;
            LeaveOffice(day, null);
            if (held == OfficeRace.Council && seat >= 0)
            {
                Gov.Council[seat] = Gov.Mayor.Party;   // the mayor's party fills the seat until the next election
            }
            else if (held == OfficeRace.Mayor)
            {
                _gov.Vacate(day);
            }
            State.Story(day + 1, "reallife_civic_news_office_stood_down", CivicText.PlayerName(), DistrictName(HomeDistrict()));
            return "reallife_civic_office_stood_down";
        }

        private void Budget(int day)
        {
            if (!State.ReserveSet)
            {
                State.ReserveSet = true;
                State.Reserve = OfficeRules.StartReserve(_gov.Electorate());
                State.WeekDay = day + 7;
                State.BudgetDay = day + OfficeRules.BudgetEvery;
                OfficeRules.CityBudget(Gov.Mayor.Lead, State.Budget);
                return;
            }
            if (day >= State.WeekDay)
            {
                State.WeekDay = day + 7;
                BudgetWeek week = OfficeRules.Week(_gov.Electorate(), Gov.Levers, State.Budget);
                State.Reserve += week.Balance;
            }
            if (!Sits() && day >= State.BudgetDay)
            {
                // A mayor of the city's own sets the budget; nobody the player is asks a vote of them.
                State.BudgetDay = day + OfficeRules.BudgetEvery;
                OfficeRules.CityBudget(Gov.Mayor.Lead, State.Budget);
            }
        }
    }
}
