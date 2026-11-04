using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Ui
{
    internal sealed partial class CompanyLive
    {
        // Tells the face of a person the company reads (a manager, a member of staff) to the interface, once, by the name the screen shows them under.
        internal static void Face(string name, string employeeId)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            if (!string.IsNullOrEmpty(employeeId))
            {
                FaceSources.Employee(name, CompanyGame.Employee(employeeId));
            }
        }

        /// <summary>
        /// A question waiting on the owner (an escalation), as a card or the Lead: who asks, about whom, what it is, what can be done with the consequences of each, from the
        /// escalation's own figures and the model behind it (the pay asked, the manager's limit, the lease's offer). Every choice is an answer the director already takes.
        /// </summary>
        internal CompanyQuestion QuestionOf(CompanyDirector d, Escalation e, Words w, bool card)
        {
            ManagedPlace mp = string.IsNullOrEmpty(e.Place) ? null : d.ManagedAt(e.Place);
            ManagerFile manager = mp != null ? mp.Own : null;
            string place = PlaceNameOf(d, e.Place);
            var q = new CompanyQuestion
            {
                Id = e.Id, Headline = CompanyApp.EscalationSays(d, e), Title = CompanyApp.EscalationName(e.Kind),
                Kicker = w.F("reallife_co_q_kicker_manager", "A manager's question, {place}", "place", place), Deep = e.Level < EscalationRules.LevelOwner
            };
            if (manager != null)
            {
                q.Asker = manager.Name;
                q.AskerRole = w.T("reallife_co_q_manager_asks", "Manager, asks");
                q.AskerHue = UiColour.HueOf(manager.Name);
                q.AskerRing = UiRole.Warn;
                Face(manager.Name, manager.Id);
                q.OpenLabel = w.F("reallife_co_open_page", "Open {name}'s page", "name", FirstName(manager.Name));
                q.Open = CompanyAct.Of(CompanyDo.Go, "mgr:" + e.Place, CompanyView.Managers);
            }
            else if (e.Kind == (int)EscalationKind.Proposal)
            {
                q.Kicker = w.F("reallife_co_q_kicker_proposal", "A proposal from {name}", "name", e.Detail);
                q.Asker = e.Detail;
                q.AskerHue = UiColour.HueOf(e.Detail);
                q.AskerRole = w.T("reallife_co_q_executive", "Executive");
                q.AskerRing = UiRole.Warn;
            }
            switch ((EscalationKind)e.Kind)
            {
                case EscalationKind.Exception: Exception(d, e, mp, q, w); break;
                case EscalationKind.Lease: Lease(d, e, q, w); break;
                case EscalationKind.Gap: Plain(q, e, w, "reallife_hier_do_fill", 0, w.T("reallife_co_q_gap_line", "A person from the staff board, at the going rate"), true); break;
                case EscalationKind.Budget: Budget(d, e, mp, q, w); break;
                case EscalationKind.Mood:
                case EscalationKind.Wave: Plain(q, e, w, "reallife_hier_do_raise", 0, w.T("reallife_co_q_raise_line", "The underpaid staff are brought up to the going rate"), true); break;
                case EscalationKind.Proposal: Proposal(q, e, w); break;
                default: Plain(q, e, w, null, 0, null, false); break;
            }
            q.Body = w.T(e.Kind == (int)EscalationKind.Exception || e.Kind == (int)EscalationKind.Lease ? "reallife_hier_what_yours" : "reallife_hier_what_yours", "Yours to decide: nobody under you may settle it.");
            if (q.Deep)
            {
                q.Body = w.T("reallife_hier_what_others", "Someone under you holds this one.");
            }
            q.Selected = 0;
            return q;
        }

        private static string FirstName(string name)
        {
            int at = string.IsNullOrEmpty(name) ? -1 : name.IndexOf(' ');
            return at > 0 ? name.Substring(0, at) : name;
        }

        // A raise or a rival's offer past what the manager may agree: agree it, raise the manager's limit, a one-off bonus in place of a raise, or say no.
        private void Exception(CompanyDirector d, Escalation e, ManagedPlace mp, CompanyQuestion q, Words w)
        {
            StaffFile f = d.State.Employee(e.Subject);
            bool offer = e.C == 1;
            string name = e.Detail;
            if (f != null)
            {
                q.Subject = f.Name;
                q.SubjectRole = w.F("reallife_co_q_cashier_days", "{skill}, {days} days", "skill", CompanyGame.SkillName(f.Id), "days", Math.Max(0, d.Today - f.AskDay).ToString());
                Face(f.Name, f.Id);
            }
            q.Title = w.T(offer ? "reallife_co_q_offer_title" : "reallife_co_q_raise_title", offer ? "A rival's offer above my limit" : "A raise above my limit");
            int now = f != null ? f.Paid : 0;
            q.FactLabels = new[] { w.T("reallife_co_q_now", "Now"), w.T(offer ? "reallife_co_q_rival_pays" : "reallife_co_q_asks_for", offer ? "Rival pays" : "Asks for"), w.T("reallife_co_q_limit", "Limit"),
                w.T("reallife_co_q_standing", "Standing") };
            q.FactValues = new[] { w.PerHour(now), w.PerHour(e.A), w.PerHour(e.B),
                f != null ? f.Standing.ToString() : "-" };
            int year = f != null ? YearCost(f, e.A - now) : 0;
            string pay = CompanyText.Wage(e.A);
            string raiseName = w.F("reallife_co_q_c_raise", "Raise {name} to {pay} an hour", "name", FirstName(name), "pay", pay);
            string raiseButton = w.F("reallife_co_q_raise_do", "Raise {name} to {pay}", "name", FirstName(name), "pay", pay);
            var choices = new List<CompanyChoice>(4);
            choices.Add(new CompanyChoice
            {
                Name = offer ? w.F("reallife_co_q_c_match", "Match {rival}'s {pay} an hour", "rival", f != null ? f.Rival : string.Empty, "pay", pay) : raiseName, Button = offer ? w.T("reallife_co_q_match_do", "Match the offer") : raiseButton,
                Note = w.T("reallife_co_q_now_note", "now"), Act = CompanyAct.Of(CompanyDo.Settle, null, e.Id, CompanyDirector.AnswerAgree),
                Lines = new[]
                {
                    new CompanyConsequence { Text = w.F("reallife_co_q_year_more", "About {amount} a year more in wages", "amount", w.Money(year)), Tone = UiConsequence.Loss, Change = 1 },
                    new CompanyConsequence { Text = w.F("reallife_co_q_stays", "{name} stays", "name", FirstName(name)), Tone = 1, Change = 0 }
                }
            });
            int band, most;
            if (!offer)
            {
                bool can = d.LimitSteps(e, out band, out most);
                int limitPay = can && f != null ? PolicyRules.RaiseCeiling(f.Paid, f.Going, band, most) : 0;
                string limit = CompanyText.Wage(can ? Math.Max(limitPay, e.A) : e.A);
                choices.Add(new CompanyChoice
                {
                    Name = w.F("reallife_co_q_c_limit", "Raise {manager}'s limit to {limit}, and let them decide", "manager", mp != null && mp.Own != null ? FirstName(mp.Own.Name) : string.Empty, "limit", limit),
                    Button = w.T("reallife_co_q_limit_do", "Raise the limit"), Note = w.T("reallife_co_q_standing_note", "standing"), Act = CompanyAct.Of(CompanyDo.Settle, null, e.Id, CompanyDirector.AnswerLimit),
                    Disabled = !can, Why = can ? null : w.T("reallife_hier_note_limit_short", "Even the widest limit would not reach it."),
                    Lines = new[]
                    {
                        new CompanyConsequence { Text = w.F("reallife_co_q_limit_line", "{manager} approves raises up to {limit} without asking you", "manager", mp != null && mp.Own != null ? FirstName(mp.Own.Name) : string.Empty, "limit", limit), Tone = -1, Change = 1 },
                        new CompanyConsequence { Text = w.T("reallife_co_q_fewer", "Fewer questions at your desk"), Tone = 1, Change = -1 }
                    }
                });
                if (d.CanBonus(e))
                {
                    int amount = d.BonusOf(e.Place);
                    choices.Add(new CompanyChoice
                    {
                        Name = w.F("reallife_co_q_c_bonus", "Give {name} a {amount} bonus instead", "name", FirstName(name), "amount", w.Money(amount)), Button = w.T("reallife_co_q_bonus_do", "Give the bonus"),
                        Note = w.T("reallife_co_q_once_note", "once"), Act = CompanyAct.Of(CompanyDo.Settle, null, e.Id, CompanyDirector.AnswerBonus),
                        Lines = new[]
                        {
                            new CompanyConsequence { Text = w.F("reallife_co_q_today", "{amount} today", "amount", w.Money(amount)), Tone = UiConsequence.Loss, Change = -1 },
                            new CompanyConsequence { Text = w.F("reallife_co_q_ask_again", "{name} may ask again after the rest", "name", FirstName(name)), Tone = -1, Change = 1 }
                        }
                    });
                }
            }
            int hire = f != null ? ManagerRules.HireCost(Math.Max(1, f.Going)) : 0;
            choices.Add(new CompanyChoice
            {
                Name = w.T("reallife_co_q_c_no", "Say no"), Button = w.T("reallife_co_q_no_do", "Say no"), Note = w.T("reallife_co_q_nocost_note", "no cost"), Act = CompanyAct.Of(CompanyDo.Settle, null, e.Id, CompanyDirector.AnswerRefuse),
                Lines = new[] { new CompanyConsequence { Text = w.F("reallife_co_q_may_leave", "{name} may leave; hiring costs about {amount}", "name", FirstName(name), "amount", w.Money(hire)), Tone = -1, Change = -1 } }
            });
            q.Choices = choices.ToArray();
        }

        // About what a raise costs over a year: the difference an hour over the hours a working day holds and the working days of a year.
        private static int YearCost(StaffFile f, int deltaHundredths)
        {
            if (deltaHundredths <= 0 || f.DayHours <= 0)
            {
                return 0;
            }
            long perDay = (long)deltaHundredths * f.DayHours / 10000L;   // whole money a working day
            long days = (long)CompanyGame.DaysPerYear * 5 / 7;
            return (int)Math.Min(int.MaxValue / 4, perDay * days);
        }

        // A landlord's offer at a lease's renewal: renew, haggle for less (once) or hold over month to month.
        private void Lease(CompanyDirector d, Escalation e, CompanyQuestion q, Words w)
        {
            LeaseFile l = d.LeaseOf(e);
            int rent = l != null ? l.Rent : e.A, offered = e.B, days = e.C;
            string business = e.Detail;
            q.Title = w.T("reallife_co_q_lease_title", "A lease is up for renewal");
            int rise = e.A > 0 ? (int)((e.B - e.A) * 1000L / e.A) : 0;
            q.FactLabels = new[] { w.T("reallife_co_q_rent_now", "Rent now"), w.T("reallife_co_q_rent_offer", "Offered"), w.T("reallife_co_q_term", "New term"), w.T("reallife_co_q_rise_limit", "Rise") };
            q.FactValues = new[] { w.Money(d.Monthly(rent)), w.Money(d.Monthly(offered)), l != null ? w.F("reallife_co_q_months", "{count} months", "count", Math.Max(1, l.Term / Math.Max(1, CompanyGame.MonthDays)).ToString()) : "-", CompanyText.Share(rise) };
            int yearMore = Math.Max(0, (offered - rent)) * CompanyGame.DaysPerYear;
            var choices = new List<CompanyChoice>(3)
            {
                new CompanyChoice
                {
                    Name = w.T("reallife_co_q_c_renew", "Renew at the offered rent"), Button = w.T("reallife_co_q_renew_do", "Renew the lease"), Note = w.T("reallife_co_q_standing_note", "standing"),
                    Act = CompanyAct.Of(CompanyDo.Settle, null, e.Id, CompanyDirector.AnswerAgree),
                    Lines = new[] { new CompanyConsequence { Text = w.F("reallife_co_q_rent_year", "About {amount} a year more in rent", "amount", w.Money(yearMore)), Tone = UiConsequence.Loss, Change = 1 } }
                },
                new CompanyChoice
                {
                    Name = w.T("reallife_co_q_c_haggle", "Haggle for a smaller rise"), Button = w.T("reallife_co_q_haggle_do", "Haggle"), Note = w.T("reallife_co_q_once_note", "once"),
                    Act = CompanyAct.Of(CompanyDo.Settle, null, e.Id, CompanyDirector.AnswerHaggle), Disabled = l != null && l.Haggled, Why = l != null && l.Haggled ? w.T("reallife_co_q_haggled", "The landlord was asked once already.") : null,
                    Lines = new[] { new CompanyConsequence { Text = w.T("reallife_co_q_haggle_line", "The landlord may meet you half way, or hold firm"), Tone = 0, Change = 0 } }
                },
                new CompanyChoice
                {
                    Name = w.T("reallife_co_q_c_hold", "Hold over, month to month"), Button = w.T("reallife_co_q_hold_do", "Hold over"), Note = w.T("reallife_co_q_nocost_note", "no cost"),
                    Act = CompanyAct.Of(CompanyDo.Settle, null, e.Id, CompanyDirector.AnswerRefuse),
                    Lines = new[] { new CompanyConsequence { Text = w.T("reallife_co_q_hold_line", "A premium on the rent, and no term to rely on"), Tone = -1, Change = 1 } }
                }
            };
            q.Choices = choices.ToArray();
            q.Headline = w.F("reallife_co_q_lease_head", "The lease at {place} renews in {days} days: {rent} a month, up {rise}.", "place", business, "days", days.ToString(), "rent", w.Money(d.Monthly(offered)), "rise", CompanyText.Share(rise));
        }

        // A manager's budget nearly spent: top it up for the month, or leave it.
        private void Budget(CompanyDirector d, Escalation e, ManagedPlace mp, CompanyQuestion q, Words w)
        {
            int amount = mp != null && mp.Own != null ? CompanyCatalogue.AtPrices(ManagerCatalogue.Step(ManagerCatalogue.Budgets, mp.Own.Budget), d.Index) : 0;
            q.FactLabels = new[] { w.T("reallife_co_q_spent", "Spent"), w.T("reallife_co_q_budget", "Budget") };
            q.FactValues = new[] { w.Money(e.A), w.Money(e.B) };
            q.Choices = new[]
            {
                new CompanyChoice
                {
                    Name = w.F("reallife_co_q_c_topup", "Top the budget up by {amount}", "amount", w.Money(amount)), Button = w.T("reallife_hier_do_budget", "Top up the budget"), Note = w.T("reallife_co_q_now_note", "now"),
                    Act = CompanyAct.Of(CompanyDo.Settle, null, e.Id, 0), Lines = new[] { new CompanyConsequence { Text = w.F("reallife_co_q_budget_line", "{amount} more for the manager this month", "amount", w.Money(amount)), Tone = UiConsequence.Loss, Change = 1 } }
                },
                DismissChoice(e, w)
            };
        }

        // An executive's proposal to change a company rule: approve it or reject it.
        private static void Proposal(CompanyQuestion q, Escalation e, Words w)
        {
            q.Choices = new[]
            {
                new CompanyChoice
                {
                    Name = w.T("reallife_hier_do_approve", "Approve"), Button = w.T("reallife_hier_do_approve", "Approve"), Note = w.T("reallife_co_q_standing_note", "standing"), Act = CompanyAct.Of(CompanyDo.Settle, null, e.Id, 0),
                    Lines = new[] { new CompanyConsequence { Text = w.T("reallife_co_q_proposal_line", "The company's rule changes at once, every manager's with it"), Tone = 0, Change = 0 } }
                },
                new CompanyChoice
                {
                    Name = w.T("reallife_hier_do_reject", "Reject"), Button = w.T("reallife_hier_do_reject", "Reject"), Note = w.T("reallife_co_q_nocost_note", "no cost"), Act = CompanyAct.Of(CompanyDo.Settle, null, e.Id, 1),
                    Lines = new[] { new CompanyConsequence { Text = w.T("reallife_co_q_reject_line", "The rule stays as it is"), Tone = 0, Change = 0 } }
                }
            };
        }

        // The kinds with one answer and a dismissal: the fix, with what it comes to, or let it go.
        private static void Plain(CompanyQuestion q, Escalation e, Words w, string doKey, int answer, string line, bool hasFix)
        {
            var list = new List<CompanyChoice>(2);
            if (hasFix)
            {
                list.Add(new CompanyChoice
                {
                    Name = w.T(doKey, doKey), Button = w.T(doKey, doKey), Note = w.T("reallife_co_q_now_note", "now"), Act = CompanyAct.Of(CompanyDo.Settle, null, e.Id, answer),
                    Lines = new[] { new CompanyConsequence { Text = line, Tone = 0, Change = 0 } }
                });
            }
            list.Add(DismissChoice(e, w));
            q.Choices = list.ToArray();
        }

        private static CompanyChoice DismissChoice(Escalation e, Words w)
        {
            return new CompanyChoice
            {
                Name = w.T("reallife_hier_do_dismiss", "Dismiss"), Button = w.T("reallife_hier_do_dismiss", "Dismiss"), Note = w.T("reallife_co_q_nocost_note", "no cost"), Act = CompanyAct.Of(CompanyDo.Dismiss, null, e.Id),
                Lines = new[] { new CompanyConsequence { Text = w.T("reallife_co_q_dismiss_line", "It comes back if it stays true"), Tone = 0, Change = 0 } }
            };
        }
    }
}
