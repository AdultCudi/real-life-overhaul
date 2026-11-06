using System;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// A landlord's duties (EstateRules.Duties.cs) in the director: each let building's morning of repair calls,
    /// satisfaction, complaints and the city's inspection; what a letting agent or a property manager does and charges;
    /// and the player's answers from the app. Runs in the day event only, over the player's buildings: nothing a frame.
    /// </summary>
    public sealed partial class EstateDirector
    {
        private bool Duties => On(EstatePart.Duties);

        // The morning of a building the player lets himself, after its tenancies' day.
        private void DutiesMorning(EstateHolding h, int day)
        {
            try
            {
                int sent = 0, chased = 0, served = 0;
                for (int n = 0; n < h.Units.Count; n++)
                {
                    if (!h.Units[n].Let)
                    {
                        continue;
                    }
                    UnitDuty(h, n, day);
                    if (h.Manager == (int)EstateManager.Manager)
                    {
                        ManagerUnit(h, n, day, ref sent, ref chased, ref served);
                    }
                }
                if (sent + chased + served > 0)
                {
                    Tell(NotificationType.Info, "reallife_estate_note_manager_day", Data("building", h.Name, "calls", sent.ToString(), "arrears", (chased + served).ToString()), TabMine,
                        "own:" + h.Place);
                }
                if (EstateRules.ManagerRepairs(h, day))
                {
                    ManagerRepairWorks(h);
                }
                InspectNow(h, day, false);
            }
            catch (Exception e)
            {
                PartFault(EstatePart.Duties, "a building's morning", e);
            }
        }

        private void UnitDuty(EstateHolding h, int unit, int day)
        {
            EstateUnit u = h.Units[unit];
            DutyDay d = EstateRules.Duty(h, unit, _state.Seed, day, _climate);
            string name = EstateText.UnitName(h, unit), call = CallPick(h, unit);
            bool quiet = h.Manager == (int)EstateManager.Manager;   // the manager deals with calls: the morning's one notice says so
            if (d.NewCall && !quiet)
            {
                Tell(NotificationType.Warning, "reallife_estate_note_call", Data("building", name, "fault", EstateText.Fault(u.Call), "amount", EstateText.Money(u.CallCost)), TabWorks, call);
            }
            if (d.Fixed && !quiet)
            {
                Tell(NotificationType.Success, "reallife_estate_note_call_fixed", Data("building", name), TabTenants, UnitPick(h, unit));
            }
            if (d.DiyLapsed)
            {
                FindVisit();
                Tell(NotificationType.Warning, "reallife_estate_note_diy_lapsed", Data("building", name, "fault", EstateText.Fault(u.Call)), TabWorks, call);
            }
            if (d.Lapsed)
            {
                Tell(NotificationType.Error, "reallife_estate_note_call_lapsed", Data("building", name, "fault", EstateText.Fault(u.Call), "days",
                    EstateCatalogue.CallLapseDays.ToString()), TabWorks, call);
            }
            if (d.Reported)
            {
                Tell(NotificationType.Error, "reallife_estate_note_reported", Data("building", name, "day", h.InspectDay.ToString()), TabTenants, UnitPick(h, unit));
            }
            else if (d.Complained)
            {
                Tell(NotificationType.Warning, "reallife_estate_note_complaint", Data("building", name, "about", EstateText.Complaint(EstateRules.Grievance(h, unit, day, _climate))), TabTenants,
                    UnitPick(h, unit));
            }
            if (d.Quits)
            {
                Tell(NotificationType.Warning, "reallife_estate_note_quits", Data("building", name, "day", u.QuitDay.ToString()), TabTenants, UnitPick(h, unit));
            }
            else if (d.Stays)
            {
                Tell(NotificationType.Success, "reallife_estate_note_stays", Data("building", name), TabTenants, UnitPick(h, unit));
            }
            if (Verbose && (d.NewCall || d.Fixed || d.Lapsed || d.Complained || d.Quits || d.Stays || d.DiyLapsed))
            {
                Logger?.Info("Estate check (duties, " + name + ", day " + day + "): satisfaction " + u.Happy + " heading for " + EstateRules.HappyTarget(h, unit, day, _climate)
                             + (d.NewCall ? "; a call for " + (u.Call) + " at " + u.CallCost : string.Empty) + (d.Fixed ? "; a call put right" : string.Empty)
                             + (d.Lapsed ? "; a call left " + EstateCatalogue.CallLapseDays + " days, the building down to " + h.Cond : string.Empty)
                             + (d.Complained ? "; complaint " + u.Moans + " in this lease" + (d.Reported ? ", to the city, the inspector on day " + h.InspectDay : string.Empty) : string.Empty)
                             + (d.Quits ? "; they go on day " + u.QuitDay : string.Empty) + (d.Stays ? "; they stay after all" : string.Empty) + ": PASS");
            }
        }

        // What a property manager does at a let unit: the rules decide, the money and the steps are taken here.
        private void ManagerUnit(EstateHolding h, int unit, int day, ref int sent, ref int chased, ref int served)
        {
            ManagerMove m = EstateRules.ManagerDay(h, unit, day);
            EstateUnit u = h.Units[unit];
            if (m.Contractor && PayContractor(h, unit, false))
            {
                sent++;
            }
            if (m.Remind && EstateRules.Remind(u))
            {
                chased++;
            }
            if (m.Plan)
            {
                EstateRules.OfferPlan(h, unit, _state.Seed, day);
                chased++;
            }
            if (m.Notice && EstateRules.ServeNotice(h, unit, day, _climate.RentLever))
            {
                served++;
            }
            if (m.Court)
            {
                int fee = EstateRules.CourtFee(_climate.Index);
                if (Cost(fee, EstateGame.CourtKey, EstateText.UnitName(h, unit), h.Place, "the court's fee, asked for by the property manager"))
                {
                    h.CostYear += fee;
                    EstateRules.GoToCourt(h, unit, day);
                    served++;
                }
            }
        }

        // An improvement order's day is near and the building is still below the mark: the manager has the builders in.
        private void ManagerRepairWorks(EstateHolding h)
        {
            int cost = EstateRules.Quote(h, EstateWork.Repair, -1, _climate);
            if (cost <= 0)
            {
                return;
            }
            if (!Cost(cost, EstateGame.WorksKey, h.Name, h.Place, "repairs the property manager started for the city's order"))
            {
                Tell(NotificationType.Warning, "reallife_estate_note_manager_broke", Data("building", h.Name, "amount", EstateText.Money(cost)), TabWorks, "own:" + h.Place);
                return;
            }
            h.CostYear += cost;
            EstateRules.Start(h, EstateWork.Repair, -1, Save.Day, _climate);
            ClearCalls(h);
            Tell(NotificationType.Info, "reallife_estate_note_manager_repairs", Data("building", h.Name, "amount", EstateText.Money(cost), "day", h.WorkEnd.ToString()), TabWorks, "own:" + h.Place);
        }

        // The city's inspection of a let building whose day has come; with 'now' (a test) whatever its day.
        private Inspection InspectNow(EstateHolding h, int day, bool now)
        {
            EstateRules.Schedule(h, _state.Seed, day);
            Inspection r = EstateRules.Inspect(h, day, _climate, now);
            string pick = "own:" + h.Place;
            if (r.Done && r.Passed)
            {
                Tell(NotificationType.Success, r.Lifted ? "reallife_estate_note_inspect_lifted" : "reallife_estate_note_inspect_passed",
                    Data("building", h.Name, "score", EstateText.Level(r.Score), "day", h.InspectDay.ToString()), TabMine, pick);
            }
            else if (r.Done)
            {
                Cost(r.Fine, EstateGame.FineKey, h.Name, h.Place, "the city's fine after an inspection scored " + r.Score + " against " + EstateCatalogue.InspectPass, true);
                h.FinesYear += r.Fine;
                h.CostYear += r.Fine;
                Tell(NotificationType.Error, r.Strikes >= EstateCatalogue.StrikesBarred ? "reallife_estate_note_inspect_barred" : "reallife_estate_note_inspect_failed",
                    Data("building", h.Name, "amount", EstateText.Money(r.Fine), "day", h.OrderDay.ToString()), TabMine, pick);
            }
            if (Verbose && (r.Done || r.Postponed))
            {
                Logger?.Info("Estate check (inspection, " + h.Name + ", day " + day + "): " + (r.Postponed ? "repair works under way, the inspector comes on day " + h.InspectDay
                                 : "scored " + r.Score + " against " + EstateCatalogue.InspectPass + ": " + (r.Passed ? "passed" + (r.Lifted ? ", the bar on new lettings lifted" : string.Empty)
                                     : "failed " + r.Strikes + " in a row, fined " + r.Fine + ", to be put right by day " + h.OrderDay
                                       + (EstateRules.Barred(h) ? ", no new tenants until it passes" : string.Empty)) + "; next on day " + h.InspectDay) + ": PASS");
            }
            return r;
        }

        // A letting agent or a property manager takes the best applicant for each empty unit it would take.
        private void AgentLets(EstateHolding h, int day)
        {
            for (int n = 0; n < h.Units.Count; n++)
            {
                int id = EstateRules.ManagerPick(h, n, day, _climate);
                if (id > 0 && Accept(h.Place, id) >= 0)
                {
                    EstateUnit u = h.Units[n];
                    Tell(NotificationType.Success, "reallife_estate_note_agent_let", Data("building", EstateText.UnitName(h, n), "who", EstateText.Tenant(h.Use,
                        u.Tenant), "amount", EstateText.Money(u.Rent)),
                        TabTenants, UnitPick(h, n));
                }
            }
        }

        // A letting agent's fee for each letting at a building it markets.
        private void AgentFee(EstateHolding h, int unit)
        {
            if (!Duties || h.Manager != (int)EstateManager.Agent)
            {
                return;
            }
            int fee = EstateRules.AgentFee(h.Units[unit].Rent);
            if (fee > 0)
            {
                Cost(fee, EstateGame.ManagerKey, EstateText.UnitName(h, unit), h.Place, "the letting agent's fee of " + EstateCatalogue.AgentLetDays + " days' rent", true);
                h.FeesYear += fee;
                h.CostYear += fee;
            }
        }

        // A property manager's cut of what came in at a building it runs.
        private void ManagerCut(EstateHolding h, int amount, string what)
        {
            if (!Duties || !h.Managed || h.Manager != (int)EstateManager.Manager)
            {
                return;
            }
            int fee = EstateRules.ManagerFee(amount);
            if (fee > 0)
            {
                Cost(fee, EstateGame.ManagerKey, h.Name, h.Place, "the property manager's " + EstateText.Share(EstateCatalogue.ManagerShare) + " of " + what + " of " + amount, true);
                h.FeesYear += fee;
                h.CostYear += fee;
            }
        }

        // A contractor paid for and sent; with 'tell', the player is told it is booked. False if the cash does not cover it.
        private bool PayContractor(EstateHolding h, int unit, bool tell)
        {
            EstateUnit u = h.Unit(unit);
            if (u == null || u.Call <= 0 || u.CallAnswer == (int)CallAnswer.Contractor)
            {
                return false;
            }
            if (!Cost(u.CallCost, EstateGame.RepairsKey, EstateText.UnitName(h, unit), h.Place, "a contractor for " + EstateText.Fault(u.Call)))
            {
                if (tell)
                {
                    Notifications.ShowInsufficientMoney();
                }
                else
                {
                    Tell(NotificationType.Warning, "reallife_estate_note_manager_broke", Data("building", EstateText.UnitName(h, unit), "amount",
                        EstateText.Money(u.CallCost)), TabWorks, CallPick(h, unit));
                }
                return false;
            }
            h.CallsYear += u.CallCost;
            h.CostYear += u.CallCost;
            bool wasMine = u.CallAnswer == (int)CallAnswer.Yourself;
            EstateRules.SendContractor(u, Save.Day);
            if (wasMine)
            {
                FindVisit();
            }
            return true;
        }

        // Repair works on the whole building: every open call goes with them, a contractor already paid for is not paid back.
        private void ClearCalls(EstateHolding h)
        {
            for (int n = 0; n < h.Units.Count; n++)
            {
                EstateRules.Fix(h.Units[n], Save.Day);
            }
            FindVisit();
        }

        // The option is off, or the part stopped: nothing of the duties is left pending.
        private void DutiesWithdrawn(int day)
        {
            if (_state == null)
            {
                return;
            }
            for (int i = 0; i < _state.Holdings.Count; i++)
            {
                EstateRules.DutiesOff(_state.Holdings[i], day);
            }
            CloseVisit();
            _visitPlace = null;
            if (Verbose)
            {
                Logger?.Info("Estate check (duties off): every payment plan ended with its arrears owed, every repair call and inspection let go, the player's own job called off: PASS");
            }
        }

        internal static string CallPick(EstateHolding h, int unit)
        {
            return "cal:" + h.Place + ":" + unit.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        // A building's rows of a landlord's duties for the Planner: calls waiting, the city's inspection or order, a tenant leaving.
        private void DutyAlerts(EstateHolding h, int id, int day)
        {
            int waiting = 0, since = int.MaxValue, quit = int.MaxValue;
            for (int n = 0; n < h.Units.Count; n++)
            {
                EstateUnit u = h.Units[n];
                if (u.Let && u.Call > 0 && u.CallAnswer == (int)CallAnswer.Waiting)
                {
                    waiting++;
                    since = Math.Min(since, u.CallDay);
                }
                if (u.Let && u.QuitDay > 0)
                {
                    quit = Math.Min(quit, u.QuitDay);
                }
            }
            if (waiting > 0 && h.Manager != (int)EstateManager.Manager)
            {
                Alert(13, id, Math.Max(day, since + EstateCatalogue.CallGraceDays), EstateText.T("reallife_estate_plan_calls_title", "building", h.Name),
                    EstateText.T("reallife_estate_plan_calls_what", "count", waiting.ToString()), null, EstateText.T("reallife_estate_plan_calls_missed"));
            }
            if (h.OrderDay > 0)
            {
                Alert(14, id, Math.Max(day, h.OrderDay), EstateText.T("reallife_estate_plan_order_title", "building", h.Name), EstateText.T("reallife_estate_plan_order_what"),
                    null, EstateText.T("reallife_estate_plan_order_missed"));
            }
            else if (h.InspectDay > 0)
            {
                Alert(14, id, Math.Max(day, h.InspectDay), EstateText.T("reallife_estate_plan_inspect_title", "building", h.Name), EstateText.T("reallife_estate_plan_inspect_what"), null, null);
            }
            if (quit < int.MaxValue)
            {
                Alert(15, id, Math.Max(day, quit), EstateText.T("reallife_estate_plan_quit_title", "building", h.Name), EstateText.T("reallife_estate_plan_quit_what"), null, null);
            }
        }

        // ------------------------------------------------------------------ the player's answers, from the app

        /// <summary>A contractor is sent to a unit's call, paid for now.</summary>
        public void SendContractor(string place, int unit)
        {
            EstateHolding h = Mine(place);
            if (h != null && Duties && PayContractor(h, unit, true))
            {
                Changed();
            }
        }

        /// <summary>The player will do a unit's repair himself: the way to the building is shown, and its door offers the job.</summary>
        public void DoItYourself(string place, int unit)
        {
            EstateHolding h = Mine(place);
            EstateUnit u = h != null ? h.Unit(unit) : null;
            if (u == null || !Duties || (_visitPlace != null && (_visitPlace != place || _visitUnit != unit)) || !EstateRules.DoItYourself(u, Save.Day))
            {
                return;
            }
            FindVisit();
            Changed();
        }

        /// <summary>The player's own job was done at the door: the materials are paid and the call is closed.</summary>
        internal void YourselfDone(string place, int unit)
        {
            EstateHolding h = Mine(place);
            EstateUnit u = h != null ? h.Unit(unit) : null;
            if (u == null || u.Call <= 0 || u.CallAnswer != (int)CallAnswer.Yourself)
            {
                return;
            }
            int materials = EstateRules.DiyCost(u.CallCost);
            Cost(materials, EstateGame.RepairsKey, EstateText.UnitName(h, unit), h.Place, "materials for a repair done by the player", true);
            h.CallsYear += materials;
            h.CostYear += materials;
            int gain = EstateRules.Fix(u, Save.Day);
            FindVisit();
            Tell(NotificationType.Success, "reallife_estate_note_diy_done", Data("building", EstateText.UnitName(h, unit), "amount", EstateText.Money(materials)), TabTenants, UnitPick(h, unit));
            if (Verbose)
            {
                Logger?.Info("Estate check (a repair done by the player, " + EstateText.UnitName(h, unit) + "): materials " + materials + " against a contractor's "
                    + EstateRules.CallCost(h, 1, _climate.Index)
                             + " for the dearest kind; satisfaction up " + gain + " to " + u.Happy + ": PASS");
            }
            Changed();
        }

        public void Remind(string place, int unit)
        {
            EstateHolding h = Mine(place);
            if (h != null && Duties && EstateRules.Remind(h.Unit(unit)))
            {
                Changed();
            }
        }

        /// <summary>A payment plan is offered; the tenant's answer is told at once.</summary>
        public void OfferPlan(string place, int unit)
        {
            EstateHolding h = Mine(place);
            if (h == null || !Duties || !EstateRules.MayOfferPlan(h.Unit(unit)))
            {
                return;
            }
            EstateUnit u = h.Units[unit];
            bool taken = EstateRules.OfferPlan(h, unit, _state.Seed, Save.Day);
            Tell(taken ? NotificationType.Success : NotificationType.Warning, taken ? "reallife_estate_note_plan_taken" : "reallife_estate_note_plan_refused",
                Data("building", EstateText.UnitName(h, unit), "amount", EstateText.Money(u.PlanEach), "days", EstateRules.PlanDays(u.ArrearsDays).ToString()), TabTenants, UnitPick(h, unit));
            if (taken)
            {
                EstateGame.WriteRent(h, false);   // the rent is paid again from tonight
            }
            if (Verbose)
            {
                Logger?.Info("Estate check (payment plan, " + EstateText.UnitName(h, unit) + "): a record of " + u.Record + " takes one " + EstateText.Share(EstateRules.PlanAcceptChance(u.Record))
                             + " of the time; " + (taken ? "taken: " + u.PlanEach + " a day with the rent of " + u.Rent + " against " + u.Arrears + " owed" : "refused") + ": PASS");
            }
            Changed();
        }

        /// <summary>Who runs a building day to day: the player, a letting agent or a property manager.</summary>
        public void SetManager(string place, int manager)
        {
            EstateHolding h = Mine(place);
            if (h != null && Duties)
            {
                h.Manager = EstateCatalogue.Clamp(manager, 0, EstateCatalogue.Managers - 1);
                Changed();
            }
        }
    }
}
