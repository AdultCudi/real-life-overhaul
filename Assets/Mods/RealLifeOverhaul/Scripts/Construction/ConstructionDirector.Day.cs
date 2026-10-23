using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The director's hour and day. The hour: deliveries at seven, the inspector at ten, the site managers, a contract's
    /// first payment, the tenders closing, the opening days. The day: the crews' pay, morale, strikes and rivals' offers;
    /// the plant hired and kept; the loans, investors, land tax, cover and flats sold; the plans, the engineer, planning, the
    /// licence and the council's zoning; a night's theft, a storm, a fault coming to light and a bribe coming out.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private const int InspectorHour = 10;

        private void HourWork(int day, int hour)
        {
            for (int i = 0; i < _state.Projects.Count; i++)
            {
                ProjectFile p = _state.Projects[i];
                if (p.Stage != ProjectStage.Building)
                {
                    continue;
                }
                if (hour == BuildCatalogue.DeliveryHour && p.OrderDay > 0 && p.OrderDay <= day)
                {
                    Deliver(p, day);
                }
                for (int k = 0; k < (int)Inspection.Count; k++)
                {
                    if (p.Insp[k] == InspectionState.Booked && p.InspDay[k] <= day && (hour == InspectorHour || p.InspDay[k] < day))
                    {
                        Inspect(p, k, day);
                    }
                }
                ManageSite(p, day, hour);
                if (p.Kind == ProjectKind.Contract && p.ClientPaid == 0 && p.SiteId > 0)
                {
                    SiteFacts site = SiteOf(p.SiteId);
                    if (site.Known && site.Hours > 0f && site.Done / site.Hours >= 0.5f)
                    {
                        ContractPaid(p, false);
                    }
                }
            }
            TendersHour(day, hour);
            OpeningHour(day, hour);
        }

        // An opening day: the morning's reminder, and the opening held without the player once its hours are over.
        private void OpeningHour(int day, int hour)
        {
            for (int i = 0; i < _state.Projects.Count; i++)
            {
                ProjectFile p = _state.Projects[i];
                if (p.Stage != ProjectStage.Finished || p.Opened || p.Opening != day)
                {
                    continue;
                }
                if (hour == OpeningFrom - 2)
                {
                    Tell(NotificationType.Info, "reallife_bld_notice_opening", ProjectName(p), null, TabProjects, p.Id);
                }
                else if (hour >= OpeningTo && !_testOpening)
                {
                    Open(p, false);
                }
            }
        }

        // The office's staff file kept to those still there, and those new to it given the role their skill suits.
        private void StaffDay()
        {
            if (_state.Firm.Founded > 0 && Office(out PlaceView office))
            {
                ConstructionGame.Staff(office.Street, office.Number, _staffNow);
                TidyStaff(_staffNow);
            }
        }

        private readonly List<StaffView> _staffNow = new List<StaffView>(8);

        private void DayWork(int day)
        {
            long began = System.Diagnostics.Stopwatch.GetTimestamp();
            StaffDay();
            CrewsDay(day);
            PlantDay(day);
            MoneyDay(day);
            PapersDay(day);
            RisksDay(day);
            MarketDay(day);
            HedgesDay();   // what each project's futures have made (ConstructionDirector.Hedge.cs)
            OpenedDay(day);
            if (Verbose)
            {
                double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - began) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                Logger?.Info("Construction check (day " + day + "): " + _state.Projects.Count + " project(s), " + _state.Crews.Count + " crew(s), " + _state.Tenders.Count + " tender(s), "
                             + _state.Developments.Count + " development(s) known; " + ms.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " ms: PASS.");
            }
        }

        // ------------------------------------------------------------------ crews

        private void CrewsDay(int day)
        {
            bool working = SiteRules.WorkingDay(day - 1);   // the day that has ended
            int week = FirmRules.Week(day);
            for (int i = _state.Crews.Count - 1; i >= 0; i--)
            {
                CrewFile c = _state.Crews[i];
                ProjectFile p = _state.Project(c.Project);
                bool atWork = p != null && p.Stage == ProjectStage.Building && c.StrikeUntil < day - 1;
                bool paid = true;
                if (working)
                {
                    paid = ConstructionGame.Take(c.Rate, "reallife_transaction_bld_crews", ForemanWords(c), true);
                    if (paid && p != null)
                    {
                        Spend(p, c.Rate);
                    }
                    else if (paid)
                    {
                        _state.PayOut(c.Rate, day);
                    }
                }
                c.IdleDays = atWork ? 0 : c.IdleDays + 1;
                c.Morale = SiteRules.Morale(c.Morale, paid, atWork, c.IdleDays);
                if (!paid)
                {
                    Tell(NotificationType.Warning, "reallife_bld_notice_crew_unpaid", ForemanWords(c), null, TabCrews, 0);
                }
                c.LowDays = c.Morale <= BuildCatalogue.StrikeAt ? c.LowDays + 1 : 0;
                if (SiteRules.WalksOff(c.Morale))
                {
                    Logger?.Info("Construction: crew " + c.Id + " walked off (morale " + c.Morale + ").");
                    Tell(NotificationType.Warning, "reallife_bld_notice_crew_left", ForemanWords(c), null, TabCrews, 0);
                    _state.Crews.RemoveAt(i);
                    continue;
                }
                if (SiteRules.Strikes(c.LowDays, c.StrikeUntil, day))
                {
                    int days = SiteRules.StrikeDays(_state.Seed, c.Id, day);
                    c.StrikeUntil = PlanningRules.AfterWorkingDays(day, days) - 1;
                    c.Morale = BuildCatalogue.MoraleAfterStrike;
                    c.LowDays = 0;
                    if (p != null)
                    {
                        p.StrikeDays += days;
                    }
                    Tell(NotificationType.Warning, "reallife_bld_notice_strike", ForemanWords(c), null, TabCrews, 0, days);
                }
                if (c.PoachRival >= 0 && day > c.PoachUntil)
                {
                    Logger?.Info("Construction: crew " + c.Id + " left for rival " + c.PoachRival + "'s offer.");
                    Tell(NotificationType.Warning, "reallife_bld_notice_poached", ForemanWords(c), BuildCatalogue.RivalKeys[c.PoachRival], TabCrews, 0);
                    _state.Crews.RemoveAt(i);
                    continue;
                }
                if (week != c.LastWeekLooked && c.PoachRival < 0 && !GoingPublic.IsGuest)
                {
                    c.LastWeekLooked = week;
                    if (Settings.ConstructionDevelopers && FirmRules.Poach(_state.Seed, c.Id, week, c.Skill, out int rival, out int pay))
                    {
                        c.PoachRival = rival;
                        c.PoachPay = pay;
                        c.PoachUntil = day + BuildCatalogue.PoachDays;
                        Tell(NotificationType.Info, "reallife_bld_notice_poach", ForemanWords(c), BuildCatalogue.RivalKeys[rival], TabCrews, 0, pay / 10);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ plant

        private void PlantDay(int day)
        {
            bool working = SiteRules.WorkingDay(day - 1);
            for (int i = 0; i < _state.Projects.Count; i++)
            {
                ProjectFile p = _state.Projects[i];
                if (p.Stage != ProjectStage.Building || !working)
                {
                    continue;
                }
                for (int k = 0; k < (int)Plant.Count; k++)
                {
                    if (p.Plant[k] != 0)
                    {
                        continue;   // none, or the company's own
                    }
                    long hire = PlansRules.HireDay((Plant)k, ConstructionGame.Prices);
                    if (ConstructionGame.Take(hire, "reallife_transaction_bld_plant", ProjectName(p), true))
                    {
                        Spend(p, hire);
                    }
                    else
                    {
                        p.Plant[k] = -1;   // not paid for: the hire firm takes it back
                        Tell(NotificationType.Warning, "reallife_bld_notice_plant_taken", ProjectName(p), BuildCatalogue.PlantKeys[k], TabProjects, p.Id);
                    }
                }
            }
            for (int i = 0; i < _state.Plant.Count; i++)
            {
                long upkeep = FirmRules.Upkeep(_state.Plant[i].Price);
                ConstructionGame.Force(upkeep, "reallife_transaction_bld_plant", ConstructionText.T(BuildCatalogue.PlantKeys[(int)_state.Plant[i].Kind]), true);
                _state.PayOut(upkeep, ConstructionGame.Day);
            }
        }

        // ------------------------------------------------------------------ money

        private void MoneyDay(int day)
        {
            int year = ConstructionGame.DaysPerYear;
            long underWay = 0;
            for (int i = 0; i < _state.Projects.Count; i++)
            {
                ProjectFile p = _state.Projects[i];
                if (p.Stage == ProjectStage.Building)
                {
                    underWay += Math.Max(p.Estimate, p.ClientValue);
                }
                if (p.LoanOwed > 0)
                {
                    long carry = p.LoanCarry;
                    long interest = MoneyRules.DayInterest(p.LoanOwed, p.LoanRateBp, year, ref carry);
                    p.LoanCarry = carry;
                    ConstructionGame.Force(interest, "reallife_transaction_bld_interest", ProjectName(p), true);
                    Spend(p, interest);
                    if (p.LoanFrom > 0)
                    {
                        int left = MoneyRules.TermDays(year) - (day - p.LoanFrom);
                        long pay = MoneyRules.Instalment(p.LoanOwed, left);
                        ConstructionGame.Force(pay, "reallife_transaction_bld_repay", ProjectName(p), false);
                        p.LoanOwed -= pay;
                        if (p.LoanOwed <= 0)
                        {
                            LoanCleared(p);
                        }
                    }
                }
                if (p.InvestorOwed > 0 && p.InvestorDue > 0 && day >= p.InvestorDue)
                {
                    PayInvestor(p, true);
                }
                if (p.After == AfterUse.SellFlats && p.Stage == ProjectStage.Finished && p.FlatsSold < p.Flats)
                {
                    FlatDay(p, day);
                }
            }
            if (_state.Firm.Cover != CoverLevel.None && _state.Firm.Founded > 0)
            {
                long premium = FirmRules.Premium(_state.Firm.Cover, underWay, ConstructionGame.Prices);
                ConstructionGame.Force(premium, "reallife_transaction_bld_cover", ConstructionText.T(BuildCatalogue.CoverKeys[(int)_state.Firm.Cover]), true);
                _state.PayOut(premium, day);
            }
            if (Settings.ConstructionLandTax)
            {
                for (int i = 0; i < _state.Lots.Count; i++)
                {
                    LotFile l = _state.Lots[i];
                    if (l.TaxedTo <= 0)
                    {
                        l.TaxedTo = day;
                        continue;
                    }
                    if (day - l.TaxedTo < BuildCatalogue.TaxEvery)
                    {
                        continue;
                    }
                    bool empty = _state.OnLot(l.Id) == null && LandRules.Empty(l.EmptySince, day);
                    int tax = LandRules.LandTax(l.Paid, day - l.TaxedTo, year, empty);
                    ConstructionGame.Force(tax, empty ? "reallife_transaction_bld_vacant_tax" : "reallife_transaction_bld_land_tax", LotWords(l.Id), true);
                    _state.PayOut(tax, day);
                    l.TaxedTo = day;
                }
            }
        }

        private void LoanCleared(ProjectFile p)
        {
            p.LoanOwed = 0;
            p.LoanCarry = 0;
            Credit(1, 0);
            Tell(NotificationType.Success, "reallife_bld_notice_loan_paid", ProjectName(p), null, TabProjects, p.Id);
        }

        /// <summary>A day's chance of a buyer for one of a block's flats, at the asking price chosen; the money shared out.</summary>
        private void FlatDay(ProjectFile p, int day)
        {
            BuildRandom random = BuildRandom.Of(_state.Seed, p.Id, day * 3 + 211);
            if (random.Chance(LandRules.BuyerChance(p.Ask, _state.Firm.Reputation)))
            {
                FlatSale(p);
            }
        }

        // One flat sold at the asking price chosen; false when the flats have no price.
        private bool FlatSale(ProjectFile p)
        {
            int quality = BuildCatalogue.Clamp(p.Quality, 0, 100);
            int price = LandRules.FlatPrice(p.Value, p.Flats, quality, p.Ask);
            if (price <= 0)
            {
                return false;
            }
            p.FlatsSold++;
            long toPlayer = MoneyRules.Share(price, p.LoanOwed, p.InvestorOwed, out long toLoan, out long toInvestor);
            SalePaid(p, toLoan, toInvestor);
            ConstructionGame.Give(toPlayer, "reallife_transaction_bld_sales", ProjectName(p));
            _state.TakeIn(price, ConstructionGame.Day);
            Tell(NotificationType.Success, "reallife_bld_notice_flat_sold", ProjectName(p), null, TabProjects, p.Id, p.Flats - p.FlatsSold);
            return true;
        }

        // A sale's share to the loan and the investor, paid straight to them from the price.
        private void SalePaid(ProjectFile p, long toLoan, long toInvestor)
        {
            if (toLoan > 0)
            {
                p.LoanOwed -= toLoan;
                if (p.LoanOwed <= 0)
                {
                    LoanCleared(p);
                }
            }
            if (toInvestor > 0)
            {
                p.InvestorOwed -= toInvestor;
            }
        }

        // An investor's money due: paid from the cash, or claimed in court when it is not there.
        private void PayInvestor(ProjectFile p, bool due)
        {
            long owed = p.InvestorOwed;
            string investor = ConstructionText.T(BuildCatalogue.InvestorKeys[Math.Max(0, p.Investor)]);
            if (ConstructionGame.Take(owed, "reallife_transaction_bld_investor_out", investor, false))
            {
                p.InvestorOwed = 0;
                Tell(NotificationType.Success, "reallife_bld_notice_investor_paid", ProjectName(p), BuildCatalogue.InvestorKeys[Math.Max(0, p.Investor)], TabProjects, p.Id);
                return;
            }
            if (!due)
            {
                return;
            }
            int claim = Claim(owed, investor, PlaceWords(p));
            p.InvestorOwed = 0;
            _state.Firm.Reputation = FirmRules.Moved(_state.Firm.Reputation, -BuildCatalogue.RepClaimLost);
            Logger?.Info("Construction: project " + p.Id + "'s investor was not paid " + owed + "; their claim is " + (claim > 0 ? "case " + claim : "with no court to file with") + ".");
            Tell(NotificationType.Warning, "reallife_bld_notice_investor_claim", ProjectName(p), BuildCatalogue.InvestorKeys[Math.Max(0, p.Investor)], TabProjects, p.Id);
        }

        // A contract's money from its client: half once its works are half done, the rest at the handover.
        private void ContractPaid(ProjectFile p, bool final)
        {
            long value = p.ClientValue;
            long pay = final ? value - p.ClientPaid : value / 2;
            if (pay <= 0)
            {
                return;
            }
            p.ClientPaid += pay;
            _state.TakeIn(pay, ConstructionGame.Day);
            ConstructionGame.Give(pay, "reallife_transaction_bld_contract", ProjectName(p));
            Logger?.Info("Construction: project " + p.Id + " paid " + pay + " by its client (" + p.ClientPaid + " of " + value + ").");
        }

        // ------------------------------------------------------------------ papers

        private void PapersDay(int day)
        {
            for (int i = 0; i < _state.Projects.Count; i++)
            {
                ProjectFile p = _state.Projects[i];
                if (p.Stage == ProjectStage.Design && !p.PlansDone && p.PlansDay > 0)
                {
                    if (p.Architect == ArchitectKind.Player)
                    {
                        if (PlayerDesign(CommissionId(p), out int total, out int cost, out int storeys, out int units))
                        {
                            OwnDesign(p, total, cost, storeys, units);
                        }
                    }
                    else if (day >= p.PlansDay)
                    {
                        PlansDrawn(p, p.Quality);
                    }
                }
                if (p.PlansDone && !p.Engineered && p.EngineerDay > 0 && day >= p.EngineerDay)
                {
                    p.Engineered = true;
                    Tell(NotificationType.Success, "reallife_bld_notice_engineered", ProjectName(p), null, TabProjects, p.Id);
                }
                if (p.Planning == PlanningState.Submitted)
                {
                    if (!p.Consulted && day >= PlanningRules.AfterWorkingDays(p.PlanningDay, BuildCatalogue.ConsultDay))
                    {
                        Consult(p);
                    }
                    if (day >= p.DecideDay)
                    {
                        Decide(p);
                    }
                }
                if (p.Planning == PlanningState.Appealed && day >= p.AppealDay)
                {
                    Hear(p);
                }
                if (p.Kind == ProjectKind.Plot && p.Stage == ProjectStage.Finished && !p.InDistrict)
                {
                    HandToDistrict(p);
                }
            }
            LicenceDay(day);
            for (int i = 0; i < _state.Lots.Count; i++)
            {
                LotFile l = _state.Lots[i];
                if (l.ZoneAsk != 0 && day >= l.ZoneDay)
                {
                    ZoneVote(l);
                }
            }
        }

        private void LicenceDay(int day)
        {
            FirmFile f = _state.Firm;
            switch (f.Licence)
            {
                case LicenceState.Applied:
                    if (day >= f.LicenceDay + BuildCatalogue.LicenceDays)
                    {
                        f.Licence = PlanningRules.Licence(Record());
                        f.LicenceDay = day;
                        if (f.Licence == LicenceState.Granted)
                        {
                            f.LicenceUntil = PlanningRules.LicenceUntil(day, ConstructionGame.DaysPerYear);
                            f.LicenceReminded = false;
                            Tell(NotificationType.Success, "reallife_bld_notice_licence_granted", null, null, TabCompany, 0);
                        }
                        else
                        {
                            Tell(NotificationType.Warning, "reallife_bld_notice_licence_refused", null, null, TabCompany, 0);
                        }
                    }
                    break;
                case LicenceState.Granted:
                    if (day > f.LicenceUntil)
                    {
                        f.Licence = LicenceState.Lapsed;
                        Tell(NotificationType.Warning, "reallife_bld_notice_licence_lapsed", null, null, TabCompany, 0);
                    }
                    else if (!f.LicenceReminded && f.LicenceUntil - day <= BuildCatalogue.LicenceRemind)
                    {
                        f.LicenceReminded = true;
                        Tell(NotificationType.Info, "reallife_bld_notice_licence_soon", null, null, TabCompany, 0, f.LicenceUntil - day);
                    }
                    break;
            }
        }

        private void ZoneVote(LotFile l)
        {
            BuildUse use = BuildUse.Homes;
            for (int u = 0; u < (int)BuildUse.Count; u++)
            {
                if ((l.ZoneAsk & BuildCatalogue.Zone((BuildUse)u)) != 0)
                {
                    use = (BuildUse)u;
                }
            }
            Lot(l.Id, out LotView lot);
            int odds = LandRules.ZoneOdds(use, lot.Class, Permits(), _state.Firm.Reputation);
            bool yes = PlanningRules.ZoneVote(_state.Seed, l.Id, use, odds);
            if (yes)
            {
                l.Zones |= l.ZoneAsk;
            }
            l.ZoneAsk = 0;
            Logger?.Info("Construction: the council voted on " + use + " for lot " + l.Id + " (odds " + odds + " in 1000): " + (yes ? "agreed" : "refused") + ".");
            Tell(yes ? NotificationType.Success : NotificationType.Warning, yes ? "reallife_bld_notice_zone_yes" : "reallife_bld_notice_zone_no", LotWords(l.Id), BuildCatalogue.UseKeys[(int)use],
                TabLand, 0);
        }

        // ------------------------------------------------------------------ risks

        private void RisksDay(int day)
        {
            bool risks = Settings.ConstructionRisks;   // off: no theft, storm damage or fault coming to light; a bribe still can
            bool storm = risks && Storm();
            for (int i = 0; i < _state.Projects.Count; i++)
            {
                ProjectFile p = _state.Projects[i];
                if (p.Stage == ProjectStage.Building && risks)
                {
                    TheftNight(p, day);
                    if (storm && p.StageSeen == (int)WorkStage.Frame)
                    {
                        int rework = SiteRules.StormRework(PlansRules.StageHours(p.Hours, p.Model, WorkStage.Frame));
                        p.Rework += rework;
                        Tell(NotificationType.Warning, "reallife_bld_notice_storm", ProjectName(p), null, TabProjects, p.Id);
                    }
                }
                if (p.DefectDay > 0 && day >= p.DefectDay)
                {
                    if (risks)
                    {
                        Defect(p);
                    }
                    else
                    {
                        p.DefectDay = 0;
                        p.DefectClaim = 0;
                    }
                }
                if (p.BribeDay > 0)
                {
                    if (day - p.BribeDay > BuildCatalogue.BribeDays)
                    {
                        p.BribeDay = 0;   // it did not come out
                    }
                    else if (SiteRules.BribeOut(_state.Seed, p.Id, day))
                    {
                        BribeOut(p);
                    }
                }
            }
        }

        private void TheftNight(ProjectFile p, int day)
        {
            if (!SiteRules.Theft(_state.Seed, p.Id, day, out int material, out int share) || p.Have[material] <= 0)
            {
                return;
            }
            int lost = Math.Max(1, (int)((long)p.Have[material] * share / 1000L));
            p.Have[material] -= lost;
            Exchange(_exchange);
            long loss = PlansRules.Cost(lost, PlansRules.LoadPrice((Material)material, ConstructionGame.Prices, _exchange[material], BuildCatalogue.MerchantPricePermille[0]));
            long paid = FirmRules.Pays(_state.Firm.Cover, loss, false, false, ConstructionGame.Prices);
            if (paid > 0)
            {
                ConstructionGame.Give(paid, "reallife_transaction_bld_insurance", ProjectName(p));
            }
            Logger?.Info("Construction: a theft at project " + p.Id + "'s site: " + (lost / 10.0) + " load(s) of " + (Material)material + " (" + loss + "), the insurer paid " + paid + ".");
            Tell(NotificationType.Warning, paid > 0 ? "reallife_bld_notice_theft_covered" : "reallife_bld_notice_theft", ProjectName(p), BuildCatalogue.MaterialKeys[material], TabProjects, p.Id);
        }

        // A fault comes to light: its owner, its residents or the city claims for it in court, and the insurer pays its share.
        private void Defect(ProjectFile p)
        {
            long claim = p.DefectClaim;
            p.DefectDay = 0;
            p.DefectClaim = 0;
            _state.Firm.Defects++;
            _state.Firm.Reputation = FirmRules.Moved(_state.Firm.Reputation, -BuildCatalogue.RepDefect);
            string claimant = ConstructionText.T(p.Kind == ProjectKind.Contract ? "reallife_bld_claimant_client" : p.After == AfterUse.SellFlats
                ? "reallife_bld_claimant_residents" : "reallife_bld_claimant_tenants");
            int number = Claim(claim, claimant, PlaceWords(p));
            long covered = FirmRules.Pays(_state.Firm.Cover, claim, true, false, ConstructionGame.Prices);
            if (covered > 0)
            {
                ConstructionGame.Give(covered, "reallife_transaction_bld_insurance", ProjectName(p));
            }
            Paper("reallife_bld_paper_defect", ProjectName(p), 0);
            Logger?.Info("Construction: a fault at " + ProjectName(p) + " came to light: a claim of " + claim + " (" + (number > 0 ? "case " + number
                : "no court") + "), the insurer paid " + covered + ".");
            Tell(NotificationType.Warning, "reallife_bld_notice_defect", ProjectName(p), null, TabProjects, p.Id);
        }

        private void DefectAhead(ProjectFile p, int quality, int day)
        {
            BuildRandom random = BuildRandom.Of(_state.Seed, p.Id, 223);
            if (!random.Chance((100 - BuildCatalogue.Clamp(quality, 0, 100)) * 4))
            {
                return;
            }
            p.DefectDay = day + random.Range(10, 41);
            p.DefectClaim = Math.Max(1000L, Math.Max(p.Spent, p.ClientValue) * random.Range(15, 61) / 1000L);
        }

        private void BribeOut(ProjectFile p)
        {
            int amount = p.BribeAmount;
            p.BribeDay = 0;
            int number = Bribery(amount);
            _state.Firm.Reputation = FirmRules.Moved(_state.Firm.Reputation, -BuildCatalogue.RepBribe);
            Standing(-5, true);
            Paper("reallife_bld_paper_bribe", ProjectName(p), 0);
            Logger?.Info("Construction: the bribe at " + ProjectName(p) + " came out: " + (number > 0 ? "case " + number : "no court") + ".");
            Tell(NotificationType.Error, "reallife_bld_notice_bribe_out", ProjectName(p), null, TabProjects, p.Id);
        }

        private void OpenedDay(int day)
        {
            for (int i = 0; i < _state.Projects.Count; i++)
            {
                ProjectFile p = _state.Projects[i];
                if (p.Stage == ProjectStage.Finished && !p.Opened && p.Opening > 0 && day > p.Opening)
                {
                    Open(p, false);
                }
            }
        }
    }
}
