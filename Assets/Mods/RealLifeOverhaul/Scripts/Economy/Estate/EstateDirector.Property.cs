using System;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The property part: the market and each building's morning, and what the player does with them in the app.</summary>
    public sealed partial class EstateDirector
    {
        private const int MonthsAtOnce = 24;   // months the market moves at most after a long jump of days

        private void PropertyDay(int day)
        {
            EstateState s = _state;
            EstateClimate c = _climate;

            // What the game's nightly run has just paid for each building, before anything is written again; a building
            // the game lets has the rental income option settled on it as rent.
            int paid = 0, wrong = 0, topped = 0, topUps = 0, madeGood = 0, shorts = 0;
            for (int i = 0; i < s.Holdings.Count; i++)
            {
                EstateHolding h = s.Holdings[i];
                int rent = EstateGame.GameRent(h);
                int extra = EstateRules.TopUp(h, day, rent, c.RentScale);
                if (extra != 0)
                {
                    EstateGame.BookRent(extra, h);
                    topped += extra;
                    topUps++;
                }
                rent += extra;
                int agreed = h.Managed ? EstateRules.PayingRent(h) : 0;
                int shortfall = h.Managed ? EstateRules.RentShort(agreed, rent) : 0;
                if (shortfall > 0)
                {
                    EstateGame.BookRent(shortfall, h);   // the run let fewer units, as it does when the rent is over its market rate
                    madeGood += shortfall;
                    shorts++;
                    rent += shortfall;
                }
                h.RentYear += rent;
                paid += rent;
                wrong += h.Managed && rent - agreed > Math.Max(2, agreed / 12) ? 1 : 0;
            }
            if (Verbose)
            {
                Logger?.Info("Estate check (rent, day " + day + "): " + paid + " for " + s.Holdings.Count + " building(s), " + madeGood + " of it made good on " + shorts
                             + " building(s) the player lets himself that the game's run paid short; " + wrong
                             + " building(s) the player lets himself were paid more than the agreed rent of their units that owe nothing: " + (wrong == 0 ? "PASS" : "LOOK"));
            }
            if (topUps > 0)
            {
                Logger?.Info("Estate: rental income at " + (EstateRules.RentShare(c.RentScale) / 10) + "%: " + topped + " " + (topped > 0 ? "added to" : "taken from")
                             + " the game's own rent of " + topUps + " building(s) it lets.");
            }

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
                Logger?.Info("Estate check (the market, month " + s.Months + "): " + was + " to " + s.Real + ", heading for " + EstateRules.RealTarget(c.Cycle, c.Quarters)
                             + " (cycle " + c.Cycle + ", the bank's rate " + c.Quarters + " quarter point(s)): " + (Math.Abs(s.Real - was) <= EstateCatalogue.RealStepMost ? "PASS" : "FAIL"));
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

        private void HoldingDay(EstateHolding h, int day)
        {
            EstateState s = _state;
            EstateClimate c = _climate;
            string pick = "own:" + h.Place;
            if (h.Managed && h.Self)
            {
                // The player has taken a unit for himself in the game's own app: every tenancy ends and the game lets the rest.
                h.Managed = false;
                for (int u = 0; u < h.Units.Count; u++)
                {
                    h.Units[u].Vacant();
                }
                h.ClearApplicants();
                EstateGame.WriteRent(h, true);
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
                int lost = EstateRules.Wear(h);
                int missed = 0, missedRent = 0, firstMissed = -1;
                for (int u = 0; u < h.Units.Count; u++)
                {
                    if (UnitDay(h, u, day))
                    {
                        missed++;
                        missedRent += h.Units[u].Rent;
                        firstMissed = firstMissed < 0 ? u : firstMissed;
                    }
                }
                // Several units missing on one morning are one notice for the building.
                if (missed == 1)
                {
                    Tell(NotificationType.Warning, "reallife_estate_note_missed", Data("building", EstateText.UnitName(h, firstMissed), "amount", EstateText.Money(missedRent)), TabTenants,
                        UnitPick(h, firstMissed));
                }
                else if (missed > 1)
                {
                    Tell(NotificationType.Warning, "reallife_estate_note_missed_many", Data("building", h.Name, "count", missed.ToString(), "amount", EstateText.Money(missedRent)), TabTenants, null);
                }
                int applied = 0, firstApplied = -1;
                RentApplicant one = null;
                for (int u = 0; u < h.Units.Count; u++)
                {
                    RentApplicant a = EstateRules.Apply(h, u, s.Seed, day, c);
                    if (a != null)
                    {
                        applied++;
                        one = one ?? a;
                        firstApplied = firstApplied < 0 ? u : firstApplied;
                    }
                }
                if (applied == 1)
                {
                    Tell(NotificationType.Info, "reallife_estate_note_applied",
                        Data("building", EstateText.UnitName(h, firstApplied), "who", EstateText.Tenant(h.Use, one.Kind), "amount", EstateText.Money(one.Offer)), TabTenants,
                        "app:" + h.Place + ":" + one.Id);
                }
                else if (applied > 1)
                {
                    Tell(NotificationType.Info, "reallife_estate_note_applied_many", Data("building", h.Name, "count", applied.ToString()), TabTenants, null);
                }
                if (Verbose && h.Units.Count > 1)
                {
                    int behind, let = h.LetUnits(out behind);
                    Logger?.Info("Estate check (units, " + h.Name + ", day " + day + "): " + h.Units.Count + " unit(s), " + let + " let, " + behind + " behind; the building wore " + lost
                                 + " (the average of its units), " + applied + " application(s) came, " + h.ApplicantCount + " wait of " + EstateRules.ApplicantCap(h.Units.Count)
                                 + " at most; the game is told to pay " + EstateRules.PayingRent(h) + ": PASS");
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
                Tell(NotificationType.Error, z.Payout > 0 ? "reallife_estate_note_fire_covered" : "reallife_estate_note_fire",
                    Data("building", h.Name, "amount", EstateText.Money(z.Payout), "bill", EstateText.Money(z.Bill)), TabWorks, pick);
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
                EstateGame.WriteRent(h, false);
            }
        }

        // One unit's morning, with its money and notices. True if its tenant missed a day's rent for the first time,
        // which HoldingDay tells for the building's units together.
        private bool UnitDay(EstateHolding h, int unit, int day)
        {
            EstateUnit u = h.Units[unit];
            int days = u.ArrearsDays, rent = u.Rent;
            TenancyDay d = EstateRules.Day(h, unit, _state.Seed, day, _climate);
            string name = EstateText.UnitName(h, unit), pick = UnitPick(h, unit);
            if (d.Lump > 0)
            {
                EstateGame.BookRent(d.Lump, h);
                h.RentYear += d.Lump;
            }
            if (d.Evicted)
            {
                Tell(NotificationType.Info, "reallife_estate_note_evicted", Data("building", name, "amount", EstateText.Money(d.Lump), "lost", EstateText.Money(d.WrittenOff)), TabTenants, pick);
            }
            else if (d.Left)
            {
                Tell(NotificationType.Warning, "reallife_estate_note_left", Data("building", name, "amount", EstateText.Money(d.Lump), "lost", EstateText.Money(d.WrittenOff)), TabTenants, pick);
            }
            else if (d.Lump > 0)
            {
                Tell(NotificationType.Success, "reallife_estate_note_paid_up", Data("building", name, "amount", EstateText.Money(d.Lump)), TabTenants, pick);
            }
            else if (d.Renewed)
            {
                Tell(NotificationType.Info, "reallife_estate_note_renewed", Data("building", name, "day", u.Until.ToString()), TabTenants, pick);
            }
            if (Verbose && (d.Missed || d.Lump > 0 || d.Left || d.Evicted || d.Renewed))
            {
                string what = d.Evicted ? "put out by the court" : d.Left ? "gone" : d.Missed ? "a day's rent of " + rent + " missed" : d.Renewed ? "the lease renewed" : "paid up";
                Logger?.Info("Estate check (tenancy, " + name + ", day " + day + "): " + what + "; " + d.Lump + " came in, " + d.WrittenOff + " written off, arrears now " + u.Arrears
                             + " over " + u.ArrearsDays + " day(s): PASS");
            }
            return d.Missed && days == 0 && !d.Left && !d.Evicted;
        }

        // What a notice about a unit picks: its lease on the Tenants tab.
        internal static string UnitPick(EstateHolding h, int unit)
        {
            return "unt:" + h.Place + ":" + unit.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private void WorkDone(EstateHolding h, EstateWork done, bool granted)
        {
            string key = done == EstateWork.Repair ? "reallife_estate_note_repaired" : done == EstateWork.Refurbish ? "reallife_estate_note_refurbished"
                : done == EstateWork.Convert ? "reallife_estate_note_converted" : granted ? "reallife_estate_note_planning_yes" : "reallife_estate_note_planning_no";
            Tell(done == EstateWork.Planning && !granted ? NotificationType.Warning : NotificationType.Success, key,
                Data("building", h.Name, "use", EstateText.Use(done == EstateWork.Planning && granted ? h.PlanUse : h.Use), "level", h.Level.ToString()), TabWorks, "own:" + h.Place);
            if (Verbose)
            {
                Logger?.Info("Estate check (works, " + h.Name + "): " + done + " finished" + (done == EstateWork.Planning ? (granted ? ", permission given" : ", permission refused") : string.Empty)
                             + "; condition " + h.Cond + ", level " + h.Level + ", use " + (EstateUse)h.Use + ", worth " + EstateRules.Value(h, _state.Real, _climate.Index) + " now: PASS");
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
            if (GoingPublic.InSession)
            {
                // In a Going Public session who owns what is the host's record: the sale completes the first day played alone.
                h.SaleDay = day + 1;
                return false;
            }
            if (!EstateGame.Sell(h, price))
            {
                h.SaleDay = day + 1;   // the player stands inside it, which the game's own sale does not allow either
                return false;
            }
            int fee = EstateRules.AgentFee(price, _climate.Index), tax = EstateRules.GainsTax(price, fee, h.Paid, h.Spent, _climate.TaxPercent);
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
                Logger?.Info("Estate check (sale, " + h.Name + "): sold for " + price + " under the game's own transaction, fee " + fee + ", tax on the gain " + tax
                             + "; the game has it as a competitor's now: PASS");
            }
            return true;
        }

        // Every building the player lets himself is told to the game, or with 'own' handed back to it. Returns how many.
        private int WriteRents(bool own)
        {
            int written = 0;
            for (int i = 0; i < _state.Holdings.Count; i++)
            {
                EstateHolding h = _state.Holdings[i];
                if (h.Managed && EstateGame.WriteRent(h, own))
                {
                    written++;
                }
            }
            return written;
        }

        private EstateHolding Mine(string place)
        {
            return On(EstatePart.Property) && Save != null ? _state.Holding(place) : null;
        }

        private void Touched(EstateHolding h)
        {
            if (h.Managed)
            {
                EstateGame.WriteRent(h, false);
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

        /// <summary>The player lets every unit of a building himself from now on: the game's own letting stops and applicants are looked for.</summary>
        public void TakeOn(string place)
        {
            EstateHolding h = Mine(place);
            if (h == null || h.Managed || h.Self)
            {
                return;
            }
            h.Managed = true;
            for (int u = 0; u < h.Units.Count; u++)
            {
                h.Units[u].ToLet = true;
                h.Units[u].VoidDays = 0;
            }
            Touched(h);
        }

        /// <summary>A building with nobody in any unit goes back to the game's own letting.</summary>
        public void HandBack(string place)
        {
            EstateHolding h = Mine(place);
            if (h == null || !h.Managed || h.AnyLet || h.Busy)
            {
                return;
            }
            h.Managed = false;
            h.ClearApplicants();
            EstateGame.WriteRent(h, true);
            Changed();
        }

        private EstateUnit UnitOf(string place, int unit, out EstateHolding h)
        {
            h = Mine(place);
            return h != null ? h.Unit(unit) : null;
        }

        public void SetAsk(string place, int unit, int ask)
        {
            EstateHolding h;
            EstateUnit u = UnitOf(place, unit, out h);
            if (u != null)
            {
                u.Ask = EstateCatalogue.Clamp(ask, EstateCatalogue.AskLeast, EstateCatalogue.AskMost);
                Changed();
            }
        }

        public void SetToLet(string place, int unit, bool on)
        {
            EstateHolding h;
            EstateUnit u = UnitOf(place, unit, out h);
            if (u != null)
            {
                u.ToLet = on;
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

        /// <summary>An applicant becomes the tenant of the unit applied for. Returns the unit, or -1.</summary>
        public int Accept(string place, int applicant)
        {
            EstateHolding h = Mine(place);
            int unit;
            if (h == null || h.ApplicantOf(applicant, out unit) == null || !EstateRules.Accept(h, applicant, Save.Day))
            {
                return -1;
            }
            EstateUnit u = h.Units[unit];
            if (Verbose)
            {
                Logger?.Info("Estate check (letting, " + EstateText.UnitName(h, unit) + "): a tenant with a record of " + u.Record + " at " + u.Rent + " a day against the unit's market rent of "
                             + EstateRules.UnitRent(h, _climate) + " (the building's " + EstateRules.MarketRent(h, _climate) + " over " + h.Units.Count + " unit(s)), until day " + u.Until
                             + ", deposit " + u.Deposit + " held; the game is told to pay " + EstateRules.PayingRent(h) + " a night for the building: PASS");
            }
            Touched(h);
            return unit;
        }

        public void Refuse(string place, int applicant)
        {
            EstateHolding h = Mine(place);
            int unit = -1;
            RentApplicant a = h != null ? h.ApplicantOf(applicant, out unit) : null;
            if (a != null && unit >= 0)
            {
                h.Units[unit].Applicants.Remove(a);
                Changed();
            }
        }

        public void ServeNotice(string place, int unit)
        {
            EstateHolding h = Mine(place);
            if (h != null && EstateRules.ServeNotice(h, unit, Save.Day, _climate.RentLever))
            {
                Changed();
            }
        }

        /// <summary>The court is asked for possession of a unit: its fee is paid first, and the order comes in CourtDays.</summary>
        public void GoToCourt(string place, int unit)
        {
            EstateHolding h = Mine(place);
            if (h == null || !EstateRules.MayGoToCourt(h, unit, Save.Day))
            {
                return;
            }
            int fee = EstateRules.CourtFee(_climate.Index);
            if (!Cost(fee, EstateGame.CourtKey, EstateText.UnitName(h, unit), h.Place, "the court's fee for a possession order"))
            {
                Notifications.ShowInsufficientMoney();
                return;
            }
            h.CostYear += fee;
            EstateRules.GoToCourt(h, unit, Save.Day);
            Changed();
        }

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
            h.List = share <= 0 ? 0
                : EstateCatalogue.Share(EstateRules.Value(h, _state.Real, _climate.Index), EstateCatalogue.Clamp(share, EstateCatalogue.ListLeast, EstateCatalogue.ListMost));
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

        partial void PropertyAlerts(int day)
        {
            for (int i = 0; i < _state.Holdings.Count; i++)
            {
                EstateHolding h = _state.Holdings[i];
                int id = EstateRules.Hash(h.Place);
                // A building's units make one row of each kind, at the earliest day.
                int waiting = 0, lapse = int.MaxValue, owed = 0, behind = 0, answer = 0, court = int.MaxValue, notice = int.MaxValue;
                string named = h.Name;
                for (int n = 0; n < h.Units.Count; n++)
                {
                    EstateUnit u = h.Units[n];
                    waiting += u.Applicants.Count;
                    for (int a = 0; a < u.Applicants.Count; a++)
                    {
                        lapse = Math.Min(lapse, u.Applicants[a].Lapse);
                    }
                    if (EstateRules.MayServeNotice(h, n, day) || EstateRules.MayGoToCourt(h, n, day))
                    {
                        owed += u.Arrears;
                        named = answer++ == 0 ? EstateText.UnitName(h, n) : h.Name;   // one unit is named; several are the building
                        behind = Math.Max(behind, u.ArrearsDays);
                    }
                    if (u.CourtDay > 0)
                    {
                        court = Math.Min(court, u.CourtDay);
                    }
                    else if (u.NoticeEnd > 0 && u.NoticeEnd >= day)
                    {
                        notice = Math.Min(notice, u.NoticeEnd);
                    }
                }
                if (waiting > 0)
                {
                    Alert(1, id, Math.Max(day, lapse), EstateText.T("reallife_estate_plan_applicants_title", "building", h.Name),
                        EstateText.T("reallife_estate_plan_applicants_what", "count", waiting.ToString()), null, EstateText.T("reallife_estate_plan_applicants_missed"));
                }
                if (answer > 0)
                {
                    Alert(2, id, day, EstateText.T("reallife_estate_plan_arrears_title", "building", named),
                        EstateText.T("reallife_estate_plan_arrears_what", "amount", EstateText.Money(owed), "days", behind.ToString()), null, null);
                }
                if (court < int.MaxValue)
                {
                    Alert(3, id, Math.Max(day, court), EstateText.T("reallife_estate_plan_court_title", "building", h.Name), EstateText.T("reallife_estate_plan_court_what"), null, null);
                }
                if (notice < int.MaxValue)
                {
                    Alert(4, id, notice, EstateText.T("reallife_estate_plan_notice_title", "building", h.Name), EstateText.T("reallife_estate_plan_notice_what"), null, null);
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
