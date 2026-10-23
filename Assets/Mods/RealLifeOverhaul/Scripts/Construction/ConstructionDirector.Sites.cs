using System;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The director's part the city's construction programme asks (through Wiring/Wiring.ConstructionLinks): how many hours one of
    /// the builders' sites works in a working hour, whether a site is a builder's, its notice board's name, its finished
    /// look and its handover. A project's site goes at its crews' pace, as far as the materials on site allow, and stands
    /// at the end of each stage until the building inspector has passed it. Asked once a working hour a site, never per frame.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private readonly int[] _people = new int[(int)Trade.Count];
        private readonly int[] _need = new int[(int)Trade.Count];
        private readonly int[] _exchange = new int[(int)Material.Count];

        /// <summary>The hours a site of the programme works in one of its working hours, given what the city's pace makes it.</summary>
        public float WorkOn(int siteId, double done, double needed, float hours)
        {
            if (LandmarkWorks(siteId) != null)
            {
                return LandmarkPace(siteId, done, needed, hours);   // a landmark's works keep the landmarks' timetable, the construction game on or off
            }
            if (!Ready())
            {
                return hours;
            }
            ProjectFile p = _state.OnSite(siteId);
            if (p != null)
            {
                return Running ? Work(p, done, needed, hours) : 0f;
            }
            DevFile dev = DevOnSite(siteId);
            if (dev != null)
            {
                if (dev.Public)
                {
                    return Running ? hours : 0f;   // a public building a rival won goes at the city's pace and is handed over when its works are done
                }
                return Running ? (float)Math.Max(0.0, Math.Min(hours, needed * 0.995 - done)) : 0f;   // the schedule hands it over on its day
            }
            TenderFile t = _state.TenderFor(siteId);
            if (t != null && t.State == TenderState.Open && Running)
            {
                return 0f;   // out to tender: the city waits for the bids (with the game off, its own contractor gets on with it)
            }
            return hours;
        }

        /// <summary>True for a site a builder started: the player's own works and the city's developers'; not the city's works won in a tender.</summary>
        public bool Owns(int siteId)
        {
            if (LandmarkWorks(siteId) != null)
            {
                return true;   // a landmark's works are the city's megaproject, not one of its six sites
            }
            if (siteId <= 0 || !Ready())
            {
                return false;
            }
            ProjectFile p = _state.OnSite(siteId);
            if (p != null)
            {
                return p.Kind != ProjectKind.Contract || p.Source != TenderSource.CityWorks;
            }
            return DevOnSite(siteId) != null;
        }

        /// <summary>False for a builder's site while the construction game is off: it is put away with it.</summary>
        public bool Shown(int siteId)
        {
            return Running || !Owns(siteId) || LandmarkWorks(siteId) != null;
        }

        /// <summary>The name a site's notice board gives: the player's company for its own works; null for the city's contractor.</summary>
        public string NoticeName(int siteId)
        {
            if (!Ready() || _state.OnSite(siteId) == null)
            {
                return null;
            }
            return Office(out PlaceView office) ? office.Name : ConstructionText.T("reallife_bld_company_unnamed");
        }

        /// <summary>The design a site's finished building wears; null for one picked to fit it.</summary>
        public string DesignFor(int siteId)
        {
            string landmark = LandmarkWorks(siteId);
            if (landmark != null)
            {
                return LandmarkDesign(landmark, out DesignChoice design) && !string.IsNullOrEmpty(design.Name) ? design.Name : null;
            }
            if (!Ready())
            {
                return null;
            }
            ProjectFile p = _state.OnSite(siteId);
            if (p != null)
            {
                return string.IsNullOrEmpty(p.Design) ? null : p.Design;
            }
            DevFile dev = DevOnSite(siteId);
            return dev != null && !string.IsNullOrEmpty(dev.Design) ? dev.Design : null;
        }

        /// <summary>A plot's finished building's lot in the district, or null.</summary>
        public string TagFor(int siteId)
        {
            string landmark = LandmarkWorks(siteId);
            if (landmark != null)
            {
                return landmark;   // the landmark's site: its look is given up once the landmark stands in its own right
            }
            if (!Ready())
            {
                return null;
            }
            ProjectFile p = _state.OnSite(siteId);
            if (p != null)
            {
                return string.IsNullOrEmpty(p.Lot) ? null : p.Lot;
            }
            DevFile dev = DevOnSite(siteId);
            return dev != null ? dev.Lot : null;
        }

        /// <summary>A site of the programme was handed over: a project's, a development's, or none of the builders'.</summary>
        public void SiteHanded(int siteId)
        {
            if (!Ready())
            {
                return;
            }
            try
            {
                int day = ConstructionGame.Day;
                ProjectFile p = _state.OnSite(siteId);
                if (p != null)
                {
                    Handover(p, day);
                    Changed();
                    return;
                }
                DevFile dev = DevOnSite(siteId);
                if (dev != null)
                {
                    dev.SiteId = 0;
                    if (dev.Public && !dev.Handed && Lot(dev.Lot, out LotView lot))
                    {
                        FinishDevelopment(dev, lot, BuildUse.Offices);   // a public building a rival put up: the city's offices from the next load
                    }
                }
            }
            catch (Exception e)
            {
                Fault("a handover", e);
            }
        }

        private DevFile DevOnSite(int siteId)
        {
            for (int i = 0; siteId > 0 && i < _state.Developments.Count; i++)
            {
                if (_state.Developments[i].SiteId == siteId)
                {
                    return _state.Developments[i];
                }
            }
            return null;
        }

        // ------------------------------------------------------------------ a project's working hour

        private float Work(ProjectFile p, double done, double needed, float hours)
        {
            if (needed <= 0.0)
            {
                return hours;
            }
            ProjectKind model = p.Model;
            double progress = done / needed;
            WorkStage stage = PlansRules.StageAt(model, Math.Max(0.0, progress - 1e-7));   // a stage's end still counts as that stage
            if ((int)stage != p.StageSeen)
            {
                EnterStage(p, stage);
            }
            PlansRules.Span(model, stage, out double from, out double to);
            int inspection = PlansRules.InspectionAfter(model, stage);
            bool last = stage == WorkStage.Envelope;
            double end = last ? 0.999 : to;
            if (progress >= end - 1e-6)
            {
                if (inspection >= 0 && p.Insp[inspection] != InspectionState.Passed)
                {
                    if (p.Insp[inspection] == InspectionState.NotDue)
                    {
                        InspectionDue(p, inspection);
                    }
                    return 0f;   // the building inspector decides
                }
                if (last)
                {
                    return (float)Math.Max(0.0, needed - done) + 0.01f;   // passed: handed over
                }
            }
            int day = ConstructionGame.Day;
            int pace = Pace(p, stage, day);
            if (pace <= 0)
            {
                return 0f;
            }
            if (p.Rework > 0)
            {
                p.Rework = Math.Max(0, p.Rework - (int)Math.Round(hours * pace / 100.0));   // tenths of an hour of rework
                if (p.Rework == 0 && inspection >= 0 && p.Insp[inspection] == InspectionState.Failed)
                {
                    InspectionDue(p, inspection);   // put right: the inspector is asked again
                }
                return 0f;
            }
            double share = SiteRules.MaterialsShare(p.Have, p.Need);
            double cap = Math.Min(end, from + (to - from) * share);
            double allowed = cap * needed - done;
            if (allowed <= 1e-6)
            {
                p.WaitHours++;   // a working hour lost for want of materials
                return 0f;
            }
            p.PaceSum += pace;
            p.PaceDays++;
            return (float)Math.Min(hours * pace / 1000.0, allowed);
        }

        // The pace of a project's stage: the crews on it (not on strike), their skill and morale, the plant there and missing.
        private int Pace(ProjectFile p, WorkStage stage, int day)
        {
            ProjectKind model = p.Model;
            Array.Clear(_people, 0, _people.Length);
            int heads = 0;
            long skill = 0, morale = 0;
            for (int i = 0; i < _state.Crews.Count; i++)
            {
                CrewFile c = _state.Crews[i];
                if (c.Project != p.Id || c.StrikeUntil >= day)
                {
                    continue;
                }
                int[] gang = c.People;
                int n = 1;
                for (int t = 0; t < gang.Length; t++)
                {
                    _people[t] += gang[t];
                    n += gang[t];
                }
                heads += n;
                skill += (long)c.Skill * n;
                morale += (long)c.Morale * n;
            }
            if (heads == 0)
            {
                return 0;
            }
            int missing = 0;
            bool craneMissing = false;
            for (int t = 0; t < (int)Trade.Count; t++)
            {
                _need[t] = model == ProjectKind.Contract ? PlansRules.WorksNeed(p.CivicKind, (Trade)t) : PlansRules.CrewNeed(stage, (Trade)t, p.Storeys);
            }
            for (int k = 0; k < (int)Plant.Count; k++)
            {
                bool needed = model == ProjectKind.Contract ? PlansRules.NeedsWorksPlant(p.CivicKind, (Plant)k) : PlansRules.NeedsPlant(stage, (Plant)k, p.Storeys);
                if (needed && p.Plant[k] < 0)
                {
                    if (k == (int)Plant.Crane)
                    {
                        craneMissing = true;
                    }
                    else
                    {
                        missing++;
                    }
                }
            }
            int special = FirmRules.SpecialPace(_state.Firm.Speciality, model, p.Use, p.CivicKind, stage);
            return SiteRules.Pace(_people, _need, (int)(skill / heads), (int)(morale / heads), missing, craneMissing, special);
        }

        // A new stage: what it needs of materials, the plant moved on and off, the inspector's visit to come, and a site
        // manager's orders and hires.
        private void EnterStage(ProjectFile p, WorkStage stage)
        {
            ProjectKind model = p.Model;
            p.StageSeen = (int)stage;
            p.Visited = false;
            p.PaceSum = 0;
            p.PaceDays = 0;
            for (int m = 0; m < (int)Material.Count; m++)
            {
                p.Need[m] = model == ProjectKind.Contract ? PlansRules.WorksLoads(p.CivicKind, p.Hours, stage, (Material)m)
                    : PlansRules.Loads(p.Use, p.Area, p.Storeys, p.Kind, stage, (Material)m) * p.CostPermille / 1000;   // a dearer design of the player's needs more
                p.Have[m] = 0;   // the stage before used its own
            }
            for (int k = 0; k < (int)Plant.Count; k++)
            {
                bool needed = model == ProjectKind.Contract ? PlansRules.NeedsWorksPlant(p.CivicKind, (Plant)k) : PlansRules.NeedsPlant(stage, (Plant)k, p.Storeys);
                if (!needed && p.Plant[k] >= 0)
                {
                    LetPlantGo(p, (Plant)k);
                }
            }
            Logger?.Info("Construction: project " + p.Id + " (" + ProjectName(p) + ") starts its " + stage + " stage: " + LoadsLine(p.Need) + ".");
            ManageSite(p, ConstructionGame.Day, ConstructionGame.Hour);
        }

        private static string LoadsLine(int[] tenths)
        {
            var text = new System.Text.StringBuilder(64);
            for (int m = 0; m < tenths.Length; m++)
            {
                if (tenths[m] > 0)
                {
                    text.Append(text.Length > 0 ? ", " : string.Empty).Append(((Material)m).ToString()).Append(' ').Append((tenths[m] / 10.0).ToString("0.#", CultureInfo.InvariantCulture));
                }
            }
            return text.Length > 0 ? text.ToString() + " loads" : "no materials";
        }

        // ------------------------------------------------------------------ inspections

        private void InspectionDue(ProjectFile p, int inspection)
        {
            p.Insp[inspection] = InspectionState.Due;
            Logger?.Info("Construction: project " + p.Id + " is ready for its " + (Inspection)inspection + " inspection.");
            if (!ManagerBooks(p, inspection))
            {
                Tell(NotificationType.Info, "reallife_bld_notice_inspection_due", ProjectName(p), BuildCatalogue.InspectionKeys[inspection], TabProjects, p.Id);
            }
        }

        /// <summary>Books the inspector for a project's due inspection: its fee now, the visit on the next working day. Returns a text key of the answer.</summary>
        public string BookInspection(int projectId)
        {
            ProjectFile p = _state != null ? _state.Project(projectId) : null;
            int inspection = p != null ? DueInspection(p) : -1;
            if (inspection < 0)
            {
                return "reallife_bld_answer_no_inspection";
            }
            long fee = (long)BuildCatalogue.InspectionFee[inspection] * ConstructionGame.Prices / 1000L;
            if (!ConstructionGame.Take(fee, "reallife_transaction_bld_fees", ProjectName(p), true))
            {
                return "reallife_bld_answer_no_money";
            }
            Spend(p, fee);
            p.Insp[inspection] = InspectionState.Booked;
            p.InspDay[inspection] = SiteRules.InspectorDay(ConstructionGame.Day, ConstructionGame.Hour);
            Logger?.Info("Construction: project " + p.Id + "'s " + (Inspection)inspection + " inspection booked for day " + p.InspDay[inspection] + " (fee " + fee + ").");
            Changed();
            return "reallife_bld_answer_inspection_booked";
        }

        /// <summary>The inspection a project waits to book, or -1.</summary>
        public static int DueInspection(ProjectFile p)
        {
            for (int i = 0; p != null && i < (int)Inspection.Count; i++)
            {
                if (p.Insp[i] == InspectionState.Due)
                {
                    return i;
                }
            }
            return -1;
        }

        // The inspector's visit, at ten on the day booked: the verdict, and what follows it.
        private void Inspect(ProjectFile p, int inspection, int day)
        {
            int tries = p.InspTries[inspection]++;
            int crewSkill = CrewSkill(p);
            int managerSkill = ManagerSkill(p, out bool manager);
            int quality = SiteRules.WorkQuality(crewSkill, managerSkill, p.Quality);
            bool rushed = p.PaceDays > 0 && SiteRules.Rushed(p.PaceSum / p.PaceDays);
            int odds = SiteRules.PassOdds(quality, rushed, p.Visited, manager && managerSkill >= 60);
            bool passed = SiteRules.Passes(_state.Seed, p.Id, inspection, tries, odds);
            if (Verbose)
            {
                Logger?.Info("Construction check (inspection): project " + p.Id + ", " + (Inspection)inspection + " try " + (tries + 1) + ": work quality " + quality + " (crews " + crewSkill
                             + ", site manager " + managerSkill + ", plans " + p.Quality + ")" + (rushed ? ", rushed" : string.Empty) + (p.Visited ? ", the site walked" : string.Empty) + "; odds "
                             + odds + " in 1000: " + (passed ? "passed" : "failed") + ": PASS.");
            }
            if (passed)
            {
                Passed(p, inspection, tries == 0, day);
                return;
            }
            p.Insp[inspection] = InspectionState.Failed;
            p.Fails++;
            WorkStage stage = (WorkStage)Math.Max(0, p.StageSeen);
            p.Rework = SiteRules.Rework(PlansRules.StageHours(p.Hours, p.Model, stage));
            _state.Firm.Reputation = FirmRules.Moved(_state.Firm.Reputation, -BuildCatalogue.RepFailed);
            for (int i = 0; i < _state.Crews.Count; i++)
            {
                if (_state.Crews[i].Project == p.Id)
                {
                    _state.Crews[i].Morale = BuildCatalogue.Clamp(_state.Crews[i].Morale - BuildCatalogue.MoraleRework, 0, 100);
                }
            }
            Tell(NotificationType.Warning, "reallife_bld_notice_inspection_failed", ProjectName(p), BuildCatalogue.InspectionKeys[inspection], TabProjects, p.Id);
        }

        private void Passed(ProjectFile p, int inspection, bool first, int day)
        {
            p.Insp[inspection] = InspectionState.Passed;
            if (first)
            {
                _state.Firm.Reputation = FirmRules.Moved(_state.Firm.Reputation, BuildCatalogue.RepPassed);
            }
            LoanDraw(p, inspection + 1);
            Tell(NotificationType.Success, "reallife_bld_notice_inspection_passed", ProjectName(p), BuildCatalogue.InspectionKeys[inspection], TabProjects, p.Id);
        }

        /// <summary>A failed inspection waved through for money: it passes now, and may come out for some days after. Returns a text key.</summary>
        public string Bribe(int projectId)
        {
            ProjectFile p = _state != null ? _state.Project(projectId) : null;
            int inspection = p != null ? FailedInspection(p) : -1;
            if (inspection < 0)
            {
                return "reallife_bld_answer_no_inspection";
            }
            int amount = BribeAmount(p);
            if (!ConstructionGame.Take(amount, "reallife_transaction_bld_cash", ProjectName(p), false))
            {
                return "reallife_bld_answer_no_money";
            }
            Spend(p, amount);
            p.Rework = 0;
            p.BribeDay = ConstructionGame.Day;
            p.BribeAmount = amount;
            Passed(p, inspection, false, ConstructionGame.Day);
            Logger?.Info("Construction: project " + p.Id + "'s " + (Inspection)inspection + " inspection waved through for " + amount + "; it may come out for " + BuildCatalogue.BribeDays + " days.");
            Changed();
            return "reallife_bld_answer_bribed";
        }

        /// <summary>What the inspector would be offered for a project's failed inspection.</summary>
        public int BribeAmount(ProjectFile p)
        {
            long stage = 0;
            Exchange(_exchange);
            for (int m = 0; m < (int)Material.Count; m++)
            {
                stage += PlansRules.Cost(p.Need[m], PlansRules.LoadPrice((Material)m, ConstructionGame.Prices, _exchange[m], BuildCatalogue.MerchantPricePermille[0]));
            }
            return SiteRules.Bribe(stage, ConstructionGame.Prices);
        }

        public static int FailedInspection(ProjectFile p)
        {
            for (int i = 0; p != null && i < (int)Inspection.Count; i++)
            {
                if (p.Insp[i] == InspectionState.Failed)
                {
                    return i;
                }
            }
            return -1;
        }

        // ------------------------------------------------------------------ a site manager

        // A site manager runs a site: books an inspection that is due, orders the stage's materials from the usual merchant,
        // hires the plant it needs, and puts an idle crew on it. Each once an hour at most, on the hour's pass.
        private void ManageSite(ProjectFile p, int day, int hour)
        {
            if (p.Stage != ProjectStage.Building || ManagerOf(p) == null)
            {
                return;
            }
            int due = DueInspection(p);
            if (due >= 0)
            {
                ManagerBooks(p, due);
            }
            if (p.OrderDay == 0 && !SiteRules.Supplied(p.Have, p.Need))
            {
                string answer = Order(p, 0);
                if (answer != "reallife_bld_answer_ordered")
                {
                    Logger?.Info("Construction: the site manager of project " + p.Id + " could not order the materials (" + answer + ").");
                }
            }
            WorkStage stage = (WorkStage)Math.Max(0, p.StageSeen);
            for (int k = 0; k < (int)Plant.Count; k++)
            {
                bool needed = p.Model == ProjectKind.Contract ? PlansRules.NeedsWorksPlant(p.CivicKind, (Plant)k) : PlansRules.NeedsPlant(stage, (Plant)k, p.Storeys);
                if (needed && p.Plant[k] < 0)
                {
                    TakePlant(p, (Plant)k);
                }
            }
            if (_state.CrewsOn(p.Id) == 0)
            {
                for (int i = 0; i < _state.Crews.Count; i++)
                {
                    if (_state.Crews[i].Project == 0)
                    {
                        _state.Crews[i].Project = p.Id;
                        break;
                    }
                }
            }
        }

        private bool ManagerBooks(ProjectFile p, int inspection)
        {
            if (ManagerOf(p) == null || p.Insp[inspection] != InspectionState.Due)
            {
                return false;
            }
            return BookInspection(p.Id) == "reallife_bld_answer_inspection_booked";
        }

        /// <summary>The site manager running a project, or null.</summary>
        public StaffFile ManagerOf(ProjectFile p)
        {
            for (int i = 0; p != null && i < _state.Staff.Count; i++)
            {
                StaffFile s = _state.Staff[i];
                if (s.Role == StaffRole.SiteManager && s.Project == p.Id)
                {
                    return s;
                }
            }
            return null;
        }

        private int ManagerSkill(ProjectFile p, out bool manager)
        {
            StaffFile s = ManagerOf(p);
            manager = s != null;
            return s != null && ConstructionGame.Employee(s.Employee, out StaffView v) ? v.Value : 50;
        }

        private int CrewSkill(ProjectFile p)
        {
            long sum = 0;
            int n = 0;
            for (int i = 0; i < _state.Crews.Count; i++)
            {
                if (_state.Crews[i].Project == p.Id)
                {
                    sum += _state.Crews[i].Skill;
                    n++;
                }
            }
            return n > 0 ? (int)(sum / n) : 30;
        }

        // ------------------------------------------------------------------ materials and plant

        /// <summary>Orders what the stage still needs from a merchant, paid now and delivered on its day. Returns a text key.</summary>
        public string Order(ProjectFile p, int merchant)
        {
            if (p == null || p.Stage != ProjectStage.Building)
            {
                return "reallife_bld_answer_not_building";
            }
            if (p.OrderDay > 0)
            {
                return "reallife_bld_answer_order_out";
            }
            int m0 = BuildCatalogue.Clamp(merchant, 0, BuildCatalogue.MerchantCount - 1);
            Exchange(_exchange);
            long cost = 0;
            bool any = false;
            for (int m = 0; m < (int)Material.Count; m++)
            {
                int want = Math.Max(0, p.Need[m] - p.Have[m]);
                if (want > 0)
                {
                    any = true;
                    cost += PlansRules.Cost(want, PlansRules.LoadPrice((Material)m, ConstructionGame.Prices, _exchange[m], BuildCatalogue.MerchantPricePermille[m0]));
                }
            }
            if (!any)
            {
                return "reallife_bld_answer_supplied";
            }
            cost = (long)Math.Round(cost * MaterialsFactor(p) / 1000.0);
            if (!ConstructionGame.Take(cost, "reallife_transaction_bld_materials", ProjectName(p), true))
            {
                return "reallife_bld_answer_no_money";
            }
            Spend(p, cost);
            for (int m = 0; m < (int)Material.Count; m++)
            {
                p.OnOrder[m] = Math.Max(0, p.Need[m] - p.Have[m]);
            }
            p.Merchant = m0;
            p.Orders++;
            p.OrderDay = SiteRules.DeliveryDay(ConstructionGame.Day, ConstructionGame.Hour, m0);
            Logger?.Info("Construction: project " + p.Id + " ordered " + LoadsLine(p.OnOrder) + " from merchant " + m0 + " for " + cost + ", to come on day " + p.OrderDay + ".");
            Changed();
            return "reallife_bld_answer_ordered";
        }

        // The speciality's saving on materials, in thousandths.
        private int MaterialsFactor(ProjectFile p)
        {
            return FirmRules.Special(_state.Firm.Speciality, p.Model, p.Use, p.CivicKind) ? 1000 - BuildCatalogue.SpecialCostPermille : 1000;
        }

        // A delivery at seven on its day: all of it, or a short one whose rest follows in two working days.
        private void Deliver(ProjectFile p, int day)
        {
            int share = SiteRules.Delivered(_state.Seed, p.Id, p.Orders, p.Merchant);
            bool shortOne = share < 1000;
            for (int m = 0; m < (int)Material.Count; m++)
            {
                int now = (int)((long)p.OnOrder[m] * share / 1000L);
                p.Have[m] = Math.Min(p.Need[m], p.Have[m] + now);
                p.OnOrder[m] -= now;
            }
            if (shortOne)
            {
                p.OrderDay = PlanningRules.AfterWorkingDays(day, BuildCatalogue.ShortFollowDays);
                p.Orders++;
                Tell(NotificationType.Warning, "reallife_bld_notice_delivery_short", ProjectName(p), BuildCatalogue.MerchantKeys[p.Merchant], TabProjects, p.Id);
            }
            else
            {
                p.OrderDay = 0;
                Array.Clear(p.OnOrder, 0, p.OnOrder.Length);
            }
            Logger?.Info("Construction: project " + p.Id + " took a delivery (" + (share / 10) + "% of the order): on site " + LoadsLine(p.Have) + ".");
        }

        /// <summary>A piece of plant for a stage: the company's own from the yard if one is free, else hired. Returns a text key.</summary>
        public string TakePlant(ProjectFile p, Plant kind)
        {
            int k = (int)kind;
            if (p.Plant[k] >= 0)
            {
                return "reallife_bld_answer_plant_there";
            }
            for (int i = 0; i < _state.Plant.Count; i++)
            {
                PlantFile own = _state.Plant[i];
                if (own.Kind == kind && own.Project == 0 && !string.IsNullOrEmpty(_state.Firm.Yard))
                {
                    own.Project = p.Id;
                    p.Plant[k] = own.Id;
                    Changed();
                    return "reallife_bld_answer_plant_own";
                }
            }
            long delivery = (long)BuildCatalogue.HireDelivery * ConstructionGame.Prices / 1000L;
            if (!ConstructionGame.Take(delivery, "reallife_transaction_bld_plant", ProjectName(p), true))
            {
                return "reallife_bld_answer_no_money";
            }
            Spend(p, delivery);
            p.Plant[k] = 0;
            Changed();
            return "reallife_bld_answer_plant_hired";
        }

        private void LetPlantGo(ProjectFile p, Plant kind)
        {
            int k = (int)kind;
            PlantFile own = _state.PlantOf(p.Plant[k]);
            if (own != null)
            {
                own.Project = 0;   // back to the yard
            }
            p.Plant[k] = -1;
        }

        private void Spend(ProjectFile p, long amount)
        {
            if (amount <= 0)
            {
                return;
            }
            p.Spent += amount;
            _state.Firm.Spent += amount;
        }
    }
}
