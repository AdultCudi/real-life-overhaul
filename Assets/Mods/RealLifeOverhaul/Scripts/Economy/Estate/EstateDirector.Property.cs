using System;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The property part's day and what the player can do in it: the market's month, each building's morning
    /// (EstateRules), the money that goes with it, and the agency (AgencyRules). One pass over the buildings the
    /// player owns and the agency's instructions, once a game day; a press of a button in the app changes one
    /// building. Nothing here runs between those.
    /// </summary>
    public sealed partial class EstateDirector
    {
        private const int MonthsAtOnce = 24;   // after a long jump of days the market moves this many months and no more

        private void PropertyDay(int day)
        {
            EstateState s = _state;
            EstateClimate c = _climate;

            // What the game's nightly run has just paid for each building, before anything is written again.
            int paid = 0, wrong = 0;
            for (int i = 0; i < s.Holdings.Count; i++)
            {
                EstateHolding h = s.Holdings[i];
                int rent = EstateGame.GameRent(h);
                h.RentYear += rent;
                paid += rent;
                wrong += h.Managed && Math.Abs(rent - (h.Let && h.Arrears == 0 ? h.Rent : 0)) > Math.Max(2, h.Rent / 12) ? 1 : 0;
            }
            if (Verbose)
            {
                Logger?.Info("Estate check (rent, day " + day + "): the game's run paid " + paid + " for " + s.Holdings.Count + " building(s); " + wrong
                             + " building(s) the player lets himself were paid something other than the agreed rent (the game may let a few units less on a day): " + (wrong == 0 ? "PASS" : "LOOK"));
            }

            // The market: a step for each month of the city's economy that has passed.
            int monthDays = MacroCatalogue.MonthDays(c.DaysPerYear);
            int months = (day - s.FirstDay) / Math.Max(1, monthDays);
            int was = s.Real;
            for (int n = 0; s.Months < months && n < MonthsAtOnce; n++)
            {
                s.Months++;
                s.Real = EstateRules.Month(s.Real, s.Seed, s.Months, c.Cycle, c.Quarters);
            }
            s.Months = Math.Max(s.Months, months);
            if (Verbose && s.Real != was)
            {
                Logger?.Info("Estate check (the market, month " + s.Months + "): " + was + " to " + s.Real + ", heading for " + EstateRules.RealTarget(c.Cycle, c.Quarters) + " (cycle " + c.Cycle + ", the bank's rate "
                             + c.Quarters + " quarter point(s)): " + (Math.Abs(s.Real - was) <= EstateCatalogue.RealStepMost ? "PASS" : "FAIL"));
            }

            int added, dropped;
            EstateGame.ReadHoldings(s, out added, out dropped);
            EstateGame.ReadAgency(s.Agency);
            for (int i = s.Holdings.Count - 1; i >= 0; i--)
            {
                HoldingDay(s.Holdings[i], day);
            }
            AgencyDay(day);
            EstateGame.WriteValues(s, c.Index);
        }

        // One building's morning.
        private void HoldingDay(EstateHolding h, int day)
        {
            EstateState s = _state;
            EstateClimate c = _climate;
            string pick = "own:" + h.Place;
            if (h.Managed && h.Self)
            {
                // The player has taken the unit for himself in the game's own app: the tenancy is over and the game lets the rest again.
                h.Managed = false;
                h.Tenant = -1;
                h.Rent = h.Deposit = h.Until = h.Arrears = h.ArrearsDays = h.NoticeEnd = h.CourtDay = 0;
                h.Applicants.Clear();
                EstateGame.WriteRent(h, false, true);
                Tell(NotificationType.Info, "reallife_estate_note_taken_over", Data("building", h.Name), TabMine, pick);
            }

            bool granted;
            EstateWork done = EstateRules.Finish(h, s.Seed, day, c, out granted);
            if (done != EstateWork.None)
            {
                WorkDone(h, done, granted);
            }

            if (h.Managed)
            {
                EstateRules.Lapse(h, day);
                int days = h.ArrearsDays;
                TenancyDay d = EstateRules.Day(h, s.Seed, day, c);
                if (d.Lump > 0)
                {
                    EstateGame.GiveRent(d.Lump, h);
                    h.RentYear += d.Lump;
                }
                if (d.Evicted)
                {
                    Tell(NotificationType.Info, "reallife_estate_note_evicted", Data("building", h.Name, "amount", EstateText.Money(d.Lump), "lost", EstateText.Money(d.WrittenOff)), TabTenants, pick);
                }
                else if (d.Left)
                {
                    Tell(NotificationType.Warning, "reallife_estate_note_left", Data("building", h.Name, "amount", EstateText.Money(d.Lump), "lost", EstateText.Money(d.WrittenOff)), TabTenants, pick);
                }
                else if (d.Missed && days == 0)
                {
                    Tell(NotificationType.Warning, "reallife_estate_note_missed", Data("building", h.Name, "amount", EstateText.Money(h.Rent)), TabTenants, pick);
                }
                else if (d.Lump > 0)
                {
                    Tell(NotificationType.Success, "reallife_estate_note_paid_up", Data("building", h.Name, "amount", EstateText.Money(d.Lump)), TabTenants, pick);
                }
                else if (d.Renewed)
                {
                    Tell(NotificationType.Info, "reallife_estate_note_renewed", Data("building", h.Name, "day", h.Until.ToString()), TabTenants, pick);
                }
                if (Verbose && (d.Missed || d.Lump > 0 || d.Left || d.Evicted || d.Renewed))
                {
                    Logger?.Info("Estate check (tenancy, " + h.Name + ", day " + day + "): " + (d.Evicted ? "put out by the court" : d.Left ? "gone" : d.Missed ? "a day's rent missed" : d.Renewed ? "the lease renewed" : "paid up")
                                 + "; " + d.Lump + " came in, " + d.WrittenOff + " written off, arrears now " + h.Arrears + " over " + h.ArrearsDays + " day(s): PASS");
                }
                RentApplicant a = EstateRules.Apply(h, s.Seed, day, c);
                if (a != null)
                {
                    Tell(NotificationType.Info, "reallife_estate_note_applied", Data("building", h.Name, "who", EstateText.Tenant(h.Use, a.Kind), "amount", EstateText.Money(a.Offer)), TabTenants, pick);
                }
            }
            else
            {
                h.Cond = Math.Max(0, h.Cond - EstateCatalogue.WearVoid);
            }

            EstateHazard z = EstateRules.Hazard(h, s.Seed, day, c);
            if (z.Fire)
            {
                if (z.Payout > 0)
                {
                    Pay(z.Payout, EstateGame.ClaimKey, h.Name, h.Place, "landlord's cover pays a fire's repair bill of " + z.Bill + " less its excess");
                }
                Tell(NotificationType.Error, z.Payout > 0 ? "reallife_estate_note_fire_covered" : "reallife_estate_note_fire", Data("building", h.Name, "amount", EstateText.Money(z.Payout), "bill", EstateText.Money(z.Bill)), TabWorks, pick);
            }
            else if (z.Breakdown)
            {
                Tell(NotificationType.Warning, "reallife_estate_note_breakdown", Data("building", h.Name, "amount", EstateText.Money(EstateRules.RepairCost(h.Base, h.Cond, c.Index))), TabWorks, pick);
            }

            if (h.Cover)
            {
                int premium = EstateRules.Premium(h.Base, c);
                if (Cost(premium, EstateGame.CoverKey, h.Name, h.Place, "a day of landlord's cover"))
                {
                    h.CostYear += premium;
                }
                else
                {
                    h.Cover = false;
                    Tell(NotificationType.Warning, "reallife_estate_note_cover_lapsed", Data("building", h.Name), TabMine, pick);
                }
            }

            if (h.SaleDay > 0 && day >= h.SaleDay)
            {
                if (CompleteSale(h, day))
                {
                    return;
                }
            }
            else
            {
                if (h.Offer > 0 && day > h.OfferLapse)
                {
                    h.Offer = h.OfferLapse = 0;
                    Tell(NotificationType.Info, "reallife_estate_note_offer_lapsed", Data("building", h.Name), TabMine, pick);
                }
                int offer = EstateRules.SaleOffer(h, s.Seed, day, EstateRules.Value(h, s.Real, c.Index), c.Cycle);
                if (offer > 0)
                {
                    Tell(NotificationType.Success, "reallife_estate_note_offer", Data("building", h.Name, "amount", EstateText.Money(offer), "day", h.OfferLapse.ToString()), TabMine, pick);
                }
            }
            if (h.Managed)
            {
                EstateGame.WriteRent(h, h.Let && h.Arrears == 0, false);
            }
        }

        private void WorkDone(EstateHolding h, EstateWork done, bool granted)
        {
            string key = done == EstateWork.Repair ? "reallife_estate_note_repaired" : done == EstateWork.Refurbish ? "reallife_estate_note_refurbished"
                : done == EstateWork.Convert ? "reallife_estate_note_converted" : granted ? "reallife_estate_note_planning_yes" : "reallife_estate_note_planning_no";
            Tell(done == EstateWork.Planning && !granted ? NotificationType.Warning : NotificationType.Success, key,
                Data("building", h.Name, "use", EstateText.Use(done == EstateWork.Planning && granted ? h.PlanUse : h.Use), "level", h.Level.ToString()), TabWorks, "own:" + h.Place);
            if (Verbose)
            {
                Logger?.Info("Estate check (works, " + h.Name + "): " + done + " finished" + (done == EstateWork.Planning ? (granted ? ", permission given" : ", permission refused") : string.Empty) + "; condition "
                             + h.Cond + ", level " + h.Level + ", use " + (EstateUse)h.Use + ", worth " + EstateRules.Value(h, _state.Real, _climate.Index) + " now: PASS");
            }
        }

        // The day of an agreed sale. True if the building is gone.
        private bool CompleteSale(EstateHolding h, int day)
        {
            string pick = "own:" + h.Place;
            if (h.SaleFails)
            {
                h.SaleDay = h.SalePrice = 0;
                h.SaleFails = false;
                Tell(NotificationType.Warning, "reallife_estate_note_sale_fell", Data("building", h.Name), TabMine, pick);
                return false;
            }
            int price = h.SalePrice;
            if (!EstateGame.Sell(h, price))
            {
                h.SaleDay = day + 1;   // the player is standing inside it, as the game's own sale would not have it either: tomorrow
                return false;
            }
            int fee = EstateRules.AgentFee(price), tax = EstateRules.GainsTax(price, fee, h.Paid, h.Spent, _climate.TaxPercent);
            Cost(fee, EstateGame.FeeKey, h.Name, null, "the agent's fee on a sale for " + price, true);
            Cost(tax, EstateGame.GainsKey, h.Name, null, "tax on the gain over " + h.Paid + " paid and " + h.Spent + " spent", true, false);
            _state.Holdings.Remove(h);
            CityBuilding c = EstateGame.Find(h.Place);
            if (c != null)
            {
                c.Holding = null;
            }
            Tell(NotificationType.Success, "reallife_estate_note_sold", Data("building", h.Name, "amount", EstateText.Money(price), "fee", EstateText.Money(fee + tax)), TabMine, null);
            if (Verbose)
            {
                Logger?.Info("Estate check (sale, " + h.Name + "): sold for " + price + " under the game's own transaction, fee " + fee + ", tax on the gain " + tax + "; the game has it as a competitor's now: PASS");
            }
            return true;
        }

        // Every building the player lets himself is told to the game (or, with 'own', handed back to it). Returns how many.
        private int WriteRents(bool own)
        {
            int written = 0;
            for (int i = 0; i < _state.Holdings.Count; i++)
            {
                EstateHolding h = _state.Holdings[i];
                if (h.Managed && EstateGame.WriteRent(h, h.Let && h.Arrears == 0, own))
                {
                    written++;
                }
            }
            return written;
        }

        // ---------------------------------------------------------------- what the player does

        private EstateHolding Mine(string place)
        {
            return On(EstatePart.Property) && Save != null ? _state.Holding(place) : null;
        }

        private void Touched(EstateHolding h)
        {
            if (h.Managed)
            {
                EstateGame.WriteRent(h, h.Let && h.Arrears == 0, false);
            }
            EstateGame.WriteValue(h, _state, _climate.Index);
            Changed();
        }

        /// <summary>The game's own purchase screen for a building on the market.</summary>
        public void Buy(string place)
        {
            if (On(EstatePart.Property) && !EstateGame.OpenPurchase(place))
            {
                Notifications.ShowError("reallife_estate_no_screen");
            }
        }

        /// <summary>The player lets a building himself from now on: the game's own letting stops, and applicants are looked for.</summary>
        public void TakeOn(string place)
        {
            EstateHolding h = Mine(place);
            if (h == null || h.Managed || h.Self)
            {
                return;
            }
            h.Managed = true;
            h.ToLet = true;
            h.VoidDays = 0;
            Touched(h);
        }

        /// <summary>An empty building goes back to the game's own letting.</summary>
        public void HandBack(string place)
        {
            EstateHolding h = Mine(place);
            if (h == null || !h.Managed || h.Let || h.Busy)
            {
                return;
            }
            h.Managed = false;
            h.Applicants.Clear();
            EstateGame.WriteRent(h, false, true);
            Changed();
        }

        public void SetAsk(string place, int ask)
        {
            EstateHolding h = Mine(place);
            if (h != null)
            {
                h.Ask = EstateCatalogue.Clamp(ask, EstateCatalogue.AskLeast, EstateCatalogue.AskMost);
                Changed();
            }
        }

        public void SetToLet(string place, bool on)
        {
            EstateHolding h = Mine(place);
            if (h != null)
            {
                h.ToLet = on;
                Changed();
            }
        }

        public void SetCover(string place, bool on)
        {
            EstateHolding h = Mine(place);
            if (h != null)
            {
                h.Cover = on;
                Changed();
            }
        }

        public void Accept(string place, int applicant)
        {
            EstateHolding h = Mine(place);
            if (h != null && EstateRules.Accept(h, applicant, Save.Day))
            {
                if (Verbose)
                {
                    Logger?.Info("Estate check (letting, " + h.Name + "): a tenant with a record of " + h.Record + " at " + h.Rent + " a day against a market rent of " + EstateRules.MarketRent(h, _climate) + ", until day "
                                 + h.Until + ", deposit " + h.Deposit + " held; the game is told to pay " + h.Rent + " a night: PASS");
                }
                Touched(h);
            }
        }

        public void Refuse(string place, int applicant)
        {
            EstateHolding h = Mine(place);
            RentApplicant a = h != null ? h.ApplicantOf(applicant) : null;
            if (a != null)
            {
                h.Applicants.Remove(a);
                Changed();
            }
        }

        public void ServeNotice(string place)
        {
            EstateHolding h = Mine(place);
            if (h != null && EstateRules.ServeNotice(h, Save.Day, _climate.RentLever))
            {
                Changed();
            }
        }

        /// <summary>The court is asked for possession: its fee is paid first, and the order comes in six days.</summary>
        public void GoToCourt(string place)
        {
            EstateHolding h = Mine(place);
            if (h == null || !EstateRules.MayGoToCourt(h, Save.Day))
            {
                return;
            }
            int fee = EstateRules.CourtFee(_climate.Index);
            if (!Cost(fee, EstateGame.CourtKey, h.Name, h.Place, "the court's fee for a possession order"))
            {
                Notifications.ShowInsufficientMoney();
                return;
            }
            h.CostYear += fee;
            EstateRules.GoToCourt(h, Save.Day);
            Changed();
        }

        /// <summary>A job is paid for and begun.</summary>
        public void StartWork(string place, EstateWork work, int toUse)
        {
            EstateHolding h = Mine(place);
            int cost = h != null ? EstateRules.Quote(h, work, toUse, _climate) : 0;
            if (cost <= 0)
            {
                return;
            }
            if (!Cost(cost, EstateGame.WorksKey, h.Name, h.Place, work + " at a building worth " + EstateRules.Value(h, _state.Real, _climate.Index)))
            {
                Notifications.ShowInsufficientMoney();
                return;
            }
            h.CostYear += cost;
            EstateRules.Start(h, work, toUse, Save.Day, _climate);
            Touched(h);
        }

        /// <summary>A building is put up for sale at a share of what it is worth, or taken off (0).</summary>
        public void List(string place, int share)
        {
            EstateHolding h = Mine(place);
            if (h == null || h.SaleDay > 0)
            {
                return;
            }
            h.List = share <= 0 ? 0 : EstateCatalogue.Share(EstateRules.Value(h, _state.Real, _climate.Index), EstateCatalogue.Clamp(share, EstateCatalogue.ListLeast, EstateCatalogue.ListMost));
            h.Offer = h.OfferLapse = 0;
            Changed();
        }

        public void AcceptOffer(string place)
        {
            EstateHolding h = Mine(place);
            if (h != null && EstateRules.AcceptOffer(h, _state.Seed, Save.Day, _climate.Cycle))
            {
                Changed();
            }
        }

        public void RefuseOffer(string place)
        {
            EstateHolding h = Mine(place);
            if (h != null && h.Offer > 0)
            {
                h.Offer = h.OfferLapse = 0;
                Changed();
            }
        }

        // ---------------------------------------------------------------- the agency

        private void AgencyDay(int day)
        {
            AgencyFile agency = _state.Agency;
            if (!agency.Running && agency.Open.Count == 0)
            {
                return;
            }
            Instruction arrived = AgencyRules.Arrive(agency, _state.Seed, day, _climate, _state.Real);
            if (arrived != null)
            {
                Tell(NotificationType.Info, arrived.Sale ? "reallife_estate_note_instruction_sale" : "reallife_estate_note_instruction_let",
                    Data("district", EstateText.District(arrived.District), "amount", EstateText.Money(arrived.Value), "day", arrived.Day.ToString()), TabAgency, "ins:" + arrived.Id);
            }
            _events.Clear();
            AgencyRules.Day(agency, _state.Seed, day, _climate, _events);
            for (int i = 0; i < _events.Count; i++)
            {
                AgencyEvent e = _events[i];
                string district = EstateText.District(e.District);
                switch (e.Kind)
                {
                    case AgencyEvent.Completed:
                        Pay(e.Amount, EstateGame.CommissionKey, district, agency.Running ? agency.Office : null, "commission on an instruction that completed");
                        Tell(NotificationType.Success, "reallife_estate_note_commission", Data("district", district, "amount", EstateText.Money(e.Amount)), TabAgency, null);
                        break;
                    case AgencyEvent.Agreed:
                        Tell(NotificationType.Info, "reallife_estate_note_agreed", Data("district", district, "amount", EstateText.Money(e.Amount)), TabAgency, "ins:" + e.Id);
                        break;
                    case AgencyEvent.NoDeal:
                    case AgencyEvent.FellThrough:
                    case AgencyEvent.Lapsed:
                        Tell(NotificationType.Warning, e.Kind == AgencyEvent.NoDeal ? "reallife_estate_note_no_deal" : e.Kind == AgencyEvent.Lapsed ? "reallife_estate_note_instruction_lost" : "reallife_estate_note_fell_through",
                            Data("district", district), TabAgency, null);
                        break;
                }
                if (Verbose)
                {
                    Logger?.Info("Estate check (agency, instruction " + e.Id + " in " + e.District + "): outcome " + e.Kind + ", " + e.Amount + (e.Kind == AgencyEvent.Completed ? " commission paid" : string.Empty)
                                 + "; " + agency.Capacity / 10f + " agent's hour(s) a day from " + agency.Negotiators + " negotiator(s): PASS");
                }
            }
        }

        /// <summary>The agency opens at an office the player holds, or moves there.</summary>
        public void OpenAgency(string place)
        {
            if (!On(EstatePart.Property) || string.IsNullOrEmpty(place))
            {
                return;
            }
            _state.Agency.Office = place;
            EstateGame.ReadAgency(_state.Agency);
            Changed();
        }

        /// <summary>The agency shuts: what it had in hand lapses by its own days.</summary>
        public void CloseAgency()
        {
            if (On(EstatePart.Property))
            {
                _state.Agency.Office = string.Empty;
                _state.Agency.Capacity = _state.Agency.Negotiators = 0;
                Changed();
            }
        }

        /// <summary>An instruction is taken on: its marketing is paid for now.</summary>
        public void TakeInstruction(int id)
        {
            Instruction i = On(EstatePart.Property) ? _state.Agency.Find(id) : null;
            int cost = AgencyRules.TakeCost(i);
            if (cost <= 0)
            {
                return;
            }
            if (!Cost(cost, EstateGame.MarketingKey, EstateText.District(i.District), _state.Agency.Running ? _state.Agency.Office : null, "marketing an instruction worth " + i.Value))
            {
                Notifications.ShowInsufficientMoney();
                return;
            }
            AgencyRules.Take(i, Save.Day);
            Changed();
        }

        public void DeclineInstruction(int id)
        {
            Instruction i = On(EstatePart.Property) ? _state.Agency.Find(id) : null;
            if (i != null && i.Stage == (int)InstructionStage.Offered)
            {
                _state.Agency.Open.Remove(i);
                Changed();
            }
        }

        // ---------------------------------------------------------------- the Planner's rows

        partial void PropertyAlerts(int day)
        {
            for (int i = 0; i < _state.Holdings.Count; i++)
            {
                EstateHolding h = _state.Holdings[i];
                int id = EstateRules.Hash(h.Place);
                if (h.Applicants.Count > 0)
                {
                    int lapse = h.Applicants[0].Lapse;
                    for (int a = 1; a < h.Applicants.Count; a++)
                    {
                        lapse = Math.Min(lapse, h.Applicants[a].Lapse);
                    }
                    Alert(1, id, Math.Max(day, lapse), EstateText.T("reallife_estate_plan_applicants_title", "building", h.Name), EstateText.T("reallife_estate_plan_applicants_what", "count", h.Applicants.Count.ToString()), null,
                        EstateText.T("reallife_estate_plan_applicants_missed"));
                }
                if (EstateRules.MayServeNotice(h, day) || EstateRules.MayGoToCourt(h, day))
                {
                    Alert(2, id, day, EstateText.T("reallife_estate_plan_arrears_title", "building", h.Name), EstateText.T("reallife_estate_plan_arrears_what", "amount", EstateText.Money(h.Arrears), "days", h.ArrearsDays.ToString()),
                        null, null);
                }
                if (h.CourtDay > 0)
                {
                    Alert(3, id, Math.Max(day, h.CourtDay), EstateText.T("reallife_estate_plan_court_title", "building", h.Name), EstateText.T("reallife_estate_plan_court_what"), null, null);
                }
                else if (h.NoticeEnd > 0 && h.NoticeEnd >= day)
                {
                    Alert(4, id, h.NoticeEnd, EstateText.T("reallife_estate_plan_notice_title", "building", h.Name), EstateText.T("reallife_estate_plan_notice_what"), null, null);
                }
                if (h.Busy)
                {
                    Alert(5, id, Math.Max(day, h.WorkEnd), EstateText.T("reallife_estate_plan_works_title", "building", h.Name), EstateText.Work(h.Work), null, null);
                }
                if (h.Offer > 0)
                {
                    Alert(6, id, Math.Max(day, h.OfferLapse), EstateText.T("reallife_estate_plan_offer_title", "building", h.Name), EstateText.T("reallife_estate_plan_offer_what"),
                        EstateText.T("reallife_planner_pays", "amount", EstateText.Money(h.Offer)), EstateText.T("reallife_estate_plan_offer_missed"));
                }
                if (h.SaleDay > 0)
                {
                    Alert(7, id, Math.Max(day, h.SaleDay), EstateText.T("reallife_estate_plan_sale_title", "building", h.Name), EstateText.T("reallife_estate_plan_sale_what"),
                        EstateText.T("reallife_planner_pays", "amount", EstateText.Money(h.SalePrice)), null);
                }
            }
            for (int i = 0; i < _state.Agency.Open.Count; i++)
            {
                Instruction n = _state.Agency.Open[i];
                if (n.Stage == (int)InstructionStage.Offered)
                {
                    Alert(8, n.Id, Math.Max(day, n.Day), EstateText.T("reallife_estate_plan_instruction_title", "district", EstateText.District(n.District)),
                        EstateText.T(n.Sale ? "reallife_estate_plan_instruction_sale" : "reallife_estate_plan_instruction_let"),
                        EstateText.T("reallife_planner_costs", "amount", EstateText.Money(AgencyRules.TakeCost(n))), EstateText.T("reallife_estate_plan_instruction_missed"));
                }
            }
        }
    }
}
