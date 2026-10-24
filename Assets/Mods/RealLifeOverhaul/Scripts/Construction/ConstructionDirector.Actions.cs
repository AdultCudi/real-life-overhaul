using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Construction
{
    /// <summary>A story of the construction game for a morning's City Ledger: a text key, the project's name and a count.</summary>
    public struct BuildStory
    {
        public int Day;      // the day of the paper it is in
        public string Key;
        public string Name;
        public int Count;
    }

    /// <summary>
    /// The director's part for what the player does: found the company, ask for the licence, keep a yard, choose a
    /// speciality and cover; buy land and ask the council to rezone it; hire, place, raise, match and let go crews; buy and
    /// sell plant; give the office's staff their roles; take the construction loan, an investor's money and sell flats before
    /// they are built; walk a site and cut a ribbon at the place. Each on a press; each answers with a text key.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private const int StoriesKept = 8;
        private readonly List<BuildStory> _stories = new List<BuildStory>(StoriesKept);

        /// <summary>The stories of a morning's paper: the same for the same day however often asked.</summary>
        public void Stories(int day, List<BuildStory> into)
        {
            for (int i = 0; i < _stories.Count; i++)
            {
                if (_stories[i].Day == day)
                {
                    into.Add(_stories[i]);
                }
            }
        }

        private const int PaperHour = 6;   // the City Ledger is out from six (Social/News): what happens later is the next morning's

        private void Paper(string key, string name, int count)
        {
            if (_stories.Count >= StoriesKept)
            {
                _stories.RemoveAt(0);
            }
            int day = ConstructionGame.Day;
            _stories.Add(new BuildStory { Day = ConstructionGame.Hour < PaperHour ? day : day + 1, Key = key, Name = name, Count = count });
        }

        // ------------------------------------------------------------------ the company

        /// <summary>The company founded at the player's office of the construction type. Returns a text key.</summary>
        public string Found()
        {
            if (_state.Firm.Founded > 0)
            {
                return "reallife_bld_answer_founded_already";
            }
            if (!Office(out PlaceView office))
            {
                return "reallife_bld_answer_no_office";
            }
            _state.Firm.Founded = ConstructionGame.Day;
            _state.Firm.Office = office.Place;
            Logger?.Info("Construction: the company founded at " + office.Place + " (" + office.Name + ").");
            Changed();
            return "reallife_bld_answer_founded";
        }

        /// <summary>The fee City Hall asks for the contractor's licence.</summary>
        public static long LicenceFee => (long)BuildCatalogue.LicenceFee * ConstructionGame.Prices / 1000L;

        /// <summary>An application for the contractor's licence (or its renewal) at City Hall: its fee, a decision in two days. Returns a text key.</summary>
        public string AskLicence()
        {
            FirmFile f = _state.Firm;
            if (f.Founded <= 0)
            {
                return "reallife_bld_answer_no_company";
            }
            if (!PlanningRules.MayAsk(f.Licence, f.LicenceDay, f.LicenceUntil, ConstructionGame.Day))
            {
                return f.Licence == LicenceState.Refused ? "reallife_bld_answer_licence_rest" : "reallife_bld_answer_licence_held";
            }
            if (!ConstructionGame.Take(LicenceFee, "reallife_transaction_bld_fees", ConstructionText.T("reallife_bld_licence"), true))
            {
                return "reallife_bld_answer_no_money";
            }
            _state.PayOut(LicenceFee, ConstructionGame.Day);
            if (f.Licence == LicenceState.Granted)
            {
                f.LicenceUntil = PlanningRules.LicenceUntil(f.LicenceUntil + 1, ConstructionGame.DaysPerYear);   // renewed from the day it would run out
                f.LicenceReminded = false;
                Changed();
                return "reallife_bld_answer_licence_renewed";
            }
            f.Licence = LicenceState.Applied;
            f.LicenceDay = ConstructionGame.Day;
            Changed();
            return "reallife_bld_answer_licence_asked";
        }

        /// <summary>A warehouse of the player's kept as the company's yard, or none. Owned plant needs a yard.</summary>
        public string SetYard(string place)
        {
            if (_state.Firm.Founded <= 0)
            {
                return "reallife_bld_answer_no_company";
            }
            _state.Firm.Yard = place ?? string.Empty;
            Changed();
            return null;
        }

        public string SetSpeciality(Speciality s)
        {
            FirmFile f = _state.Firm;
            if (f.Speciality == s)
            {
                return null;
            }
            if (f.SpecialSince > 0 && ConstructionGame.Day - f.SpecialSince < BuildCatalogue.SpecialityRest)
            {
                return "reallife_bld_answer_speciality_rest";
            }
            f.Speciality = s;
            f.SpecialSince = ConstructionGame.Day;
            Changed();
            return null;
        }

        public void SetCover(CoverLevel level)
        {
            _state.Firm.Cover = level;
            Changed();
        }

        // ------------------------------------------------------------------ land

        /// <summary>Why a lot cannot be bought now (a text key), or null.</summary>
        public string BuyBlock(LotView lot)
        {
            if (GoingPublic.InSession)
            {
                return "reallife_bld_answer_session_land";
            }
            if (!lot.Open || LotTaken(lot.Id) || _state.Lot(lot.Id) != null || DeveloperOn(lot.Id))
            {
                return "reallife_bld_answer_lot_taken";
            }
            if (_state.Lots.Count >= BuildCatalogue.LotsMost)
            {
                return "reallife_bld_answer_too_many_lots";
            }
            return LotPrice(lot) <= 0 ? "reallife_bld_answer_no_price" : null;
        }

        /// <summary>True while the city's developers are building on a lot or have built there.</summary>
        public bool DeveloperOn(string lot)
        {
            DevFile dev = _state.Development(lot);
            return dev != null && (dev.Handed || dev.SiteId > 0 || dev.Design == NoDesign || dev.Public);
        }

        /// <summary>A lot bought outright at its price. Returns a text key.</summary>
        public string BuyLot(string lotId)
        {
            if (!Lot(lotId, out LotView lot))
            {
                return "reallife_bld_answer_no_lot";
            }
            string block = BuyBlock(lot);
            if (block != null)
            {
                return block;
            }
            long price = LotPrice(lot);
            if (!ConstructionGame.Take(price, "reallife_transaction_bld_land", LotWords(lotId), false))
            {
                return "reallife_bld_answer_no_money";
            }
            int day = ConstructionGame.Day;
            _state.Lots.Add(new LotFile { Id = lotId, Bought = day, Paid = price, Zones = LandRules.ZonesOf(lot.Class), TaxedTo = day, EmptySince = day });
            Logger?.Info("Construction: lot " + lotId + " bought for " + price + ".");
            Changed();
            return "reallife_bld_answer_lot_bought";
        }

        /// <summary>The fee City Hall asks to put a change of zoning to the council.</summary>
        public static long ZoneFee => (long)BuildCatalogue.ZoneFee * ConstructionGame.Prices / 1000L;

        /// <summary>A change of a lot's zoning put to the council: a use added, heard at its next meeting. Returns a text key.</summary>
        public string AskZone(string lotId, BuildUse use)
        {
            LotFile l = _state.Lot(lotId);
            if (l == null)
            {
                return "reallife_bld_answer_no_lot";
            }
            if (LandRules.Zoned(ZonesNow(l), use))
            {
                return "reallife_bld_answer_zoned_already";
            }
            if (l.ZoneAsk != 0)
            {
                return "reallife_bld_answer_zone_waits";
            }
            if (!ConstructionGame.Take(ZoneFee, "reallife_transaction_bld_fees", LotWords(lotId), true))
            {
                return "reallife_bld_answer_no_money";
            }
            _state.PayOut(ZoneFee, ConstructionGame.Day);
            l.ZoneAsk = BuildCatalogue.Zone(use);
            l.ZoneDay = CouncilDay(ConstructionGame.Day);   // the city's one council: its sitting, or the player's meeting
            Changed();
            return "reallife_bld_answer_zone_asked";
        }

        /// <summary>A lot in words: its address.</summary>
        public string LotWords(string lotId)
        {
            return Lot(lotId, out LotView lot) ? ConstructionGame.AddressText(lot.Street, lot.Number) : ConstructionText.T("reallife_bld_place_lot");
        }

        // ------------------------------------------------------------------ crews

        /// <summary>This week's offer in a slot of the labour market, and whether it is still to be had.</summary>
        public CrewOffer OfferAt(int slot, out bool taken)
        {
            int week = FirmRules.Week(ConstructionGame.Day);
            if (_state.OffersWeek != week)
            {
                _state.OffersWeek = week;
                _state.HiredOffers.Clear();
            }
            taken = _state.HiredOffers.Contains(slot);
            return FirmRules.Offer(_state.Seed, week, slot, ConstructionGame.Prices);
        }

        /// <summary>A crew of this week's offers taken on. Returns a text key.</summary>
        public string Hire(int slot)
        {
            if (_state.Firm.Founded <= 0)
            {
                return "reallife_bld_answer_no_company";
            }
            CrewOffer offer = OfferAt(slot, out bool taken);
            if (taken)
            {
                return "reallife_bld_answer_offer_taken";
            }
            if (_state.Crews.Count >= BuildCatalogue.CrewsMost)
            {
                return "reallife_bld_answer_too_many_crews";
            }
            _state.HiredOffers.Add(slot);
            var c = new CrewFile { Id = _state.NewId(), Gang = offer.Gang, Foreman = offer.Foreman, Skill = offer.Skill, Rate = offer.Rate,
                Hired = ConstructionGame.Day, LastWeekLooked = FirmRules.Week(ConstructionGame.Day) };
            _state.Crews.Add(c);
            Logger?.Info("Construction: crew " + c.Id + " hired (" + BuildCatalogue.GangKeys[c.Gang] + ", skill " + c.Skill + ", " + c.Rate + " a working day).");
            Changed();
            return "reallife_bld_answer_hired";
        }

        public string Assign(int crewId, int projectId)
        {
            CrewFile c = _state.Crew(crewId);
            if (c == null)
            {
                return "reallife_bld_answer_no_crew_there";
            }
            ProjectFile p = _state.Project(projectId);
            c.Project = p != null && p.Live ? p.Id : 0;
            Changed();
            return null;
        }

        /// <summary>A crew let go with a couple of days' pay. Returns a text key.</summary>
        public string Release(int crewId)
        {
            CrewFile c = _state.Crew(crewId);
            if (c == null)
            {
                return "reallife_bld_answer_no_crew_there";
            }
            ConstructionGame.Force((long)c.Rate * BuildCatalogue.ReleaseDays, "reallife_transaction_bld_crews", ForemanWords(c), true);
            _state.Crews.Remove(c);
            Changed();
            return "reallife_bld_answer_released";
        }

        /// <summary>A crew's day rate raised: their morale with it.</summary>
        public string Raise(int crewId)
        {
            CrewFile c = _state.Crew(crewId);
            if (c == null)
            {
                return "reallife_bld_answer_no_crew_there";
            }
            c.Rate = (int)((long)c.Rate * (1000 + BuildCatalogue.RaisePermille) / 1000L);
            c.Morale = BuildCatalogue.Clamp(c.Morale + BuildCatalogue.MoraleRaise * 3, 0, 100);
            c.LowDays = 0;
            Changed();
            return "reallife_bld_answer_raised";
        }

        /// <summary>A rival's offer to a crew matched: their rate rises by as much, and they stay.</summary>
        public string Match(int crewId)
        {
            CrewFile c = _state.Crew(crewId);
            if (c == null || c.PoachRival < 0)
            {
                return "reallife_bld_answer_no_offer";
            }
            c.Rate = (int)((long)c.Rate * (1000 + c.PoachPay) / 1000L);
            c.PoachRival = -1;
            c.PoachPay = 0;
            c.Morale = BuildCatalogue.Clamp(c.Morale + BuildCatalogue.MoraleRaise, 0, 100);
            Changed();
            return "reallife_bld_answer_matched";
        }

        public string ForemanWords(CrewFile c)
        {
            return ConstructionText.T("reallife_bld_crew_name", "foreman", ConstructionText.T(BuildCatalogue.ForemanKeys[BuildCatalogue.Clamp(c.Foreman, 0, BuildCatalogue.ForemanKeys.Length - 1)]));
        }

        // ------------------------------------------------------------------ plant and staff

        /// <summary>A piece of plant bought for the yard. Returns a text key.</summary>
        public string BuyPlant(Plant kind)
        {
            if (string.IsNullOrEmpty(_state.Firm.Yard))
            {
                return "reallife_bld_answer_no_yard";
            }
            long price = (long)BuildCatalogue.BuyPrice[(int)kind] * ConstructionGame.Prices / 1000L;
            if (!ConstructionGame.Take(price, "reallife_transaction_bld_plant_buy", ConstructionText.T(BuildCatalogue.PlantKeys[(int)kind]), false))
            {
                return "reallife_bld_answer_no_money";
            }
            _state.Plant.Add(new PlantFile { Id = _state.NewId(), Kind = kind, Bought = ConstructionGame.Day, Price = (int)Math.Min(int.MaxValue, price) });
            Changed();
            return "reallife_bld_answer_plant_bought";
        }

        public string SellPlant(int id)
        {
            PlantFile q = _state.PlantOf(id);
            if (q == null)
            {
                return "reallife_bld_answer_no_plant";
            }
            ProjectFile p = _state.Project(q.Project);
            if (p != null)
            {
                LetPlantGo(p, q.Kind);
            }
            long back = FirmRules.Resale(q.Price, ConstructionGame.Day - q.Bought);
            ConstructionGame.Give(back, "reallife_transaction_bld_plant_sale", ConstructionText.T(BuildCatalogue.PlantKeys[(int)q.Kind]));
            _state.Plant.Remove(q);
            Changed();
            return "reallife_bld_answer_plant_sold";
        }

        /// <summary>An employee of the office given a role (and a site manager a project). The office's staff are read afresh.</summary>
        public void SetRole(string employee, StaffRole role, int project)
        {
            StaffFile s = _state.StaffOf(employee);
            if (s == null)
            {
                if (_state.Staff.Count >= BuildCatalogue.StaffMost)
                {
                    return;
                }
                s = new StaffFile { Employee = employee };
                _state.Staff.Add(s);
            }
            s.Role = role;
            s.Project = role == StaffRole.SiteManager ? project : 0;
            Changed();
        }

        /// <summary>The role an employee of the office has: the company's, else by their skill as the game has it.</summary>
        public StaffRole RoleOf(StaffView v)
        {
            StaffFile s = _state.StaffOf(v.Id);
            return s != null ? s.Role : FirmRules.RoleFor(v.Skill);
        }

        // The office's staff file kept to those still there: someone gone from the game's list leaves the company's.
        internal void TidyStaff(List<StaffView> staff)
        {
            for (int i = _state.Staff.Count - 1; i >= 0; i--)
            {
                bool there = false;
                for (int k = 0; k < staff.Count && !there; k++)
                {
                    there = staff[k].Id == _state.Staff[i].Employee;
                }
                if (!there)
                {
                    _state.Staff.RemoveAt(i);
                }
            }
            for (int k = 0; k < staff.Count; k++)
            {
                if (_state.StaffOf(staff[k].Id) == null && _state.Staff.Count < BuildCatalogue.StaffMost)
                {
                    _state.Staff.Add(new StaffFile { Employee = staff[k].Id, Role = FirmRules.RoleFor(staff[k].Skill) });
                }
            }
        }

        // ------------------------------------------------------------------ money

        /// <summary>The construction loan a bank offers on a project: its most, rate and fee; false with none to offer.</summary>
        public bool LoanOffer(ProjectFile p, out long most, out int rateBp, out long fee)
        {
            LotFile lot = _state.Lot(p.Lot);
            most = MoneyRules.LoanMost(lot != null ? lot.Paid : 0L, Math.Max(p.Estimate, 0L));
            rateBp = MoneyRules.RateBp(BusinessRate());
            fee = MoneyRules.Fee(most);
            return p.Kind != ProjectKind.Contract && most > 0 && p.LoanAgreed == 0 && (p.Stage == ProjectStage.Ready || p.Stage == ProjectStage.Building);
        }

        /// <summary>The construction loan agreed: drawn in stages, at the start and after each inspection. Returns a text key.</summary>
        public string TakeLoan(ProjectFile p)
        {
            if (p == null || !LoanOffer(p, out long most, out int rate, out _))
            {
                return "reallife_bld_answer_no_loan";
            }
            p.LoanAgreed = most;
            p.LoanRateBp = rate;
            p.LoanDraws = 0;
            if (p.Stage == ProjectStage.Building)
            {
                LoanDraw(p, 0);
            }
            Logger?.Info("Construction: project " + p.Id + "'s construction loan agreed: " + most + " at " + (rate / 100.0).ToString("0.00", CultureInfo.InvariantCulture) + "% a year.");
            Changed();
            return "reallife_bld_answer_loan_agreed";
        }

        // One draw of the loan: at the start (0) and after each inspection (1 to 3); the bank's fee with the first.
        private void LoanDraw(ProjectFile p, int index)
        {
            if (p.LoanAgreed <= 0 || index < 0 || index >= BuildCatalogue.LoanDrawPermille.Length || (p.LoanDraws & (1 << index)) != 0)
            {
                return;
            }
            long draw = MoneyRules.Draw(p.LoanAgreed, p.LoanDrawn, index);
            p.LoanDraws |= 1 << index;
            if (draw <= 0)
            {
                return;
            }
            if (p.LoanDrawn == 0)
            {
                long fee = MoneyRules.Fee(p.LoanAgreed);
                ConstructionGame.Force(fee, "reallife_transaction_bld_fees", ProjectName(p), true);
                Spend(p, fee);
            }
            p.LoanDrawn += draw;
            p.LoanOwed += draw;
            ConstructionGame.Give(draw, "reallife_transaction_bld_loan", ProjectName(p));
            Credit(0, draw);
            Logger?.Info("Construction: project " + p.Id + " drew " + draw + " of its loan (" + p.LoanDrawn + " of " + p.LoanAgreed + ").");
        }

        /// <summary>What is owed on a project's loan paid off from the cash now. Returns a text key.</summary>
        public string Repay(ProjectFile p)
        {
            if (p == null || p.LoanOwed <= 0)
            {
                return "reallife_bld_answer_no_loan_owed";
            }
            if (!ConstructionGame.Take(p.LoanOwed, "reallife_transaction_bld_repay", ProjectName(p), false))
            {
                return "reallife_bld_answer_no_money";
            }
            p.LoanAgreed = p.LoanDrawn;   // nothing more is drawn on a loan paid off
            LoanCleared(p);
            Changed();
            return "reallife_bld_answer_repaid";
        }

        /// <summary>An investor's offer on a project: what they put in and what they are owed at completion.</summary>
        public bool InvestorOffer(ProjectFile p, int investor, out long put, out long owed)
        {
            put = MoneyRules.InvestorPut(investor, Math.Max(0L, p.Estimate));
            owed = MoneyRules.InvestorOwed(investor, put);
            return p.Kind != ProjectKind.Contract && p.Kind != ProjectKind.Refit && p.Investor < 0 && put > 0 && (p.Stage == ProjectStage.Ready || p.Stage == ProjectStage.Building);
        }

        public string TakeInvestor(ProjectFile p, int investor)
        {
            if (p == null || !InvestorOffer(p, investor, out long put, out long owed))
            {
                return "reallife_bld_answer_no_investor";
            }
            p.Investor = BuildCatalogue.Clamp(investor, 0, BuildCatalogue.InvestorCount - 1);
            p.InvestorPut = put;
            p.InvestorOwed = owed;
            ConstructionGame.Give(put, "reallife_transaction_bld_investor_in", ConstructionText.T(BuildCatalogue.InvestorKeys[p.Investor]));
            Changed();
            return "reallife_bld_answer_investor_in";
        }

        /// <summary>Flats of a block of homes sold before it is built: a deposit each now, the rest at completion. Returns a text key.</summary>
        public string SellOffPlan(ProjectFile p, int count)
        {
            if (p == null || p.Kind != ProjectKind.Plot || p.Use != BuildUse.Homes || p.InDistrict || (p.Stage != ProjectStage.Ready && p.Stage != ProjectStage.Building))
            {
                return "reallife_bld_answer_no_off_plan";
            }
            p.Flats = FlatsOf(p);
            if (p.Flats - p.FlatsSold <= 0 || count <= 0)
            {
                return "reallife_bld_answer_no_flats";
            }
            int price = OffPlanPrice(p);
            if (price <= 0)
            {
                return "reallife_bld_answer_no_price";
            }
            OffPlanSale(p, price, count);
            Changed();
            return "reallife_bld_answer_off_plan";
        }

        // ------------------------------------------------------------------ at the place

        /// <summary>The player walks a site of theirs with the foreman, once a stage: the inspector sees the care taken, the crew the interest.</summary>
        public string Visit(int projectId)
        {
            ProjectFile p = _state != null ? _state.Project(projectId) : null;
            if (p == null || p.Stage != ProjectStage.Building)
            {
                return "reallife_bld_answer_not_building";
            }
            if (p.Visited)
            {
                return "reallife_bld_answer_visited";
            }
            p.Visited = true;
            for (int i = 0; i < _state.Crews.Count; i++)
            {
                if (_state.Crews[i].Project == p.Id)
                {
                    _state.Crews[i].Morale = BuildCatalogue.Clamp(_state.Crews[i].Morale + 3, 0, 100);
                }
            }
            Logger?.Info("Construction: the player walked project " + p.Id + "'s site with the foreman in its " + (WorkStage)Math.Max(0, p.StageSeen) + " stage.");
            Changed();
            return "reallife_bld_answer_visit";
        }

        // The opening: with the player cutting the ribbon, a moment for Flare, a story and a lift in standing; without, quietly.
        private void Open(ProjectFile p, bool ribbon)
        {
            if (p.Opened)
            {
                return;
            }
            p.Opened = true;
            p.Ribbon = ribbon;
            _testOpening = false;
            Standing(ribbon ? 3 : 1, false);
            if (ribbon)
            {
                if (!p.Hired)
                {
                    _state.Firm.Reputation = FirmRules.Moved(_state.Firm.Reputation, 1);   // a hired builder's work lifts the client's standing, not the company's
                }
                FlareOpened(PlaceWords(p), ProjectName(p));
                Paper("reallife_bld_paper_opening", ProjectName(p), 0);
                Tell(NotificationType.Success, "reallife_bld_notice_ribbon", ProjectName(p), null, TabProjects, p.Id);
            }
            Logger?.Info("Construction: project " + p.Id + " opened" + (ribbon ? ", the player cutting the ribbon." : " without the player."));
            HidePanels();
            Changed();
        }

        /// <summary>The ribbon cut at the opening, at the place. Returns a text key.</summary>
        public string CutRibbon(int projectId)
        {
            ProjectFile p = _state != null ? _state.Project(projectId) : null;
            if (p == null || p.Stage != ProjectStage.Finished || p.Opened || ConstructionGame.Day != p.Opening)
            {
                return "reallife_bld_answer_no_opening";
            }
            Open(p, true);
            return "reallife_bld_answer_ribbon";
        }
    }
}
