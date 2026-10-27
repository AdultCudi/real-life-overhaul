using System;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The director's part the city's construction programme asks (through Wiring/Wiring.ConstructionLinks): how many hours one of
    /// the builders' sites works in a working hour, whether a site is a builder's, its notice board's name, its finished
    /// look and its handover. A project's site goes at its crews' pace, as far as the materials on site allow, stands at the
    /// end of each stage until the building inspector has passed it and goes straight on into the next (SiteRules.Hour,
    /// PlansRules.WorkingStage). The programme says which hour it works, so a sleep it catches up at once on waking keeps the
    /// days it would have kept awake: the inspector booked and coming at the hour's own time. Asked once a working hour a
    /// site, never per frame.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private readonly int[] _people = new int[(int)Trade.Count];
        private readonly int[] _need = new int[(int)Trade.Count];
        private readonly int[] _exchange = new int[(int)Material.Count];

        /// <summary>The hours a site of the programme works in one of its working hours (the day and hour it works), given what the city's pace makes it.</summary>
        public float WorkOn(int siteId, double done, double needed, float hours, int day, int hour)
        {
            if (LandmarkWorks(siteId) != null)
            {
                return LandmarkPace(siteId, done, needed, hours);   // a landmark's works keep the landmarks' timetable, the construction game on or off
            }
            if (!Ready())
            {
                // A builder's site waits while its construction game has faulted, as while it is off: at the city's pace it would be
                // handed over with nobody to hear it, its project left building with no site for good.
                return _loaded && _state != null && (_state.OnSite(siteId) != null || DevOnSite(siteId) != null) ? 0f : hours;
            }
            ProjectFile p = _state.OnSite(siteId);
            if (p != null)
            {
                if (p.Contractor >= 0 && !p.Hired)
                {
                    return Running ? hours : 0f;   // a contractor's refit goes at the city's own pace, its crews and materials the contractor's
                }
                return Running ? Work(p, done, needed, hours, day, hour) : 0f;   // the company's crews, or a hired builder's at its own pace through the same inspections
            }
            if (CityPace(siteId, done, needed, hours, out float pace))
            {
                return pace;   // the city's redevelopment (ConstructionDirector.CityRedevelop): its schedule hands it over on its day
            }
            if (GrowPace(siteId, done, needed, hours, out pace))
            {
                return pace;   // a grown site (ConstructionDirector.Grow): its schedule hands it over on its day
            }
            if (ParkPace(siteId, done, needed, hours, out pace))
            {
                return pace;   // a car park of the city's builders (ConstructionDirector.ParkNpc): alike
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
            return DevOnSite(siteId) != null || _state.CityRedevSite(siteId) != null || _state.GrowSite(siteId) != null || _state.CarParkSite(siteId) != null;
        }

        /// <summary>False for a builder's site while the construction game is off: it is put away with it.</summary>
        public bool Shown(int siteId)
        {
            return Running || !Owns(siteId) || LandmarkWorks(siteId) != null;
        }

        /// <summary>The name a site's notice board gives: the player's company for its own works; null for the city's contractor.</summary>
        public string NoticeName(int siteId)
        {
            ProjectFile p = Ready() ? _state.OnSite(siteId) : null;
            if (p == null)
            {
                // the city's redevelopment, a grown site or a car park shows its developer; the city's own works none
                return Ready() ? CityBoard(siteId) ?? GrowBoard(siteId) ?? ParkBoard(siteId) : null;
            }
            if (p.Contractor >= 0)
            {
                return ConstructionText.T(BuildCatalogue.RivalKeys[BuildCatalogue.Clamp(p.Contractor, 0, BuildCatalogue.RivalCount - 1)]);
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
            if (dev != null)
            {
                return !string.IsNullOrEmpty(dev.Design) ? dev.Design : null;
            }
            RedevFile r = _state.CityRedevSite(siteId);
            if (r == null)
            {
                GrowFile g = _state.GrowSite(siteId);
                return g != null && g.Stage == GrowStage.Building && !string.IsNullOrEmpty(g.Design) && g.Design != NoDesign ? g.Design : null;
            }
            return r.Stage == CityStage.Building && !string.IsNullOrEmpty(r.Design) && r.Design != NoDesign ? r.Design : null;
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
            RedevFile r = dev == null ? _state.CityRedevSite(siteId) : null;
            GrowFile g = dev == null && r == null ? _state.GrowSite(siteId) : null;
            return dev != null ? dev.Lot : r != null ? r.Lot : g != null ? g.Lot : null;
        }

        /// <summary>
        /// A site of the programme was handed over on a day (the day of the hour that finished it, which a sleep's catching up
        /// works some time after): a project's, a development's, or none of the builders'.
        /// </summary>
        public void SiteHanded(int siteId, int day)
        {
            if (!Ready())
            {
                return;
            }
            try
            {
                day = Math.Min(day, ConstructionGame.Day);
                ProjectFile p = _state.OnSite(siteId);
                if (p != null)
                {
                    Handover(p, day);
                    Changed();
                    return;
                }
                RivalHanded(siteId, day);   // a rival's works from a tender: the stock market hears whether on time
                CityHanded(siteId);         // the city's redevelopment: its schedule ends the rest on its day
                GrowSiteHanded(siteId);     // a grown site: alike
                ParkSiteHanded(siteId);     // a car park of the city's builders: alike
                DevFile dev = DevOnSite(siteId);
                if (dev != null)
                {
                    dev.SiteId = 0;
                    if (dev.Public && !dev.Handed && Lot(dev.Lot, out LotView lot))
                    {
                        FinishDevelopment(dev, lot, BuildUse.Offices);   // a public building a rival put up: the city's offices
                    }
                }
            }
            catch (Exception e)
            {
                Fault("a handover", e);
            }
        }

        // The day's pass: a building (a plot or a rebuild, the company's or a builder's) whose site the programme no longer holds
        // (emptied by its test, lost from a Going Public guest's copy of the host's programme) is set out again where its stage
        // began, so it never stands building with no site; what was passed and paid stands. Nothing while the programme holds no
        // site at all, which cannot tell a lost one from one not read yet.
        private void SitesLost()
        {
            if (SiteCount() <= 0)
            {
                return;
            }
            for (int i = 0; i < _state.Projects.Count; i++)
            {
                ProjectFile p = _state.Projects[i];
                if (p.Stage != ProjectStage.Building || p.SiteId <= 0 || (p.Kind != ProjectKind.Plot && p.Kind != ProjectKind.Rebuild && p.Kind != ProjectKind.Extension
                    && p.Kind != ProjectKind.Parking) || SiteOf(p.SiteId).Known)
                {
                    continue;
                }
                int lost = p.SiteId;
                int site = StartSite(p, Math.Max(0, p.Contractor), out string why);
                if (site <= 0)
                {
                    Logger?.Info("Construction: project " + p.Id + "'s site " + lost + " is not in the city's programme and could not be set out again (" + why + "); tried again tomorrow.");
                    continue;
                }
                WorkStage stage = (WorkStage)BuildCatalogue.Clamp(p.StageSeen, 0, (int)WorkStage.Envelope);
                PlansRules.Span(p.Model, stage, out double from, out _);
                SetSiteDone(site, (float)(from * p.Hours));
                p.SiteId = site;
                Logger?.Info("Construction: project " + p.Id + "'s site " + lost + " was not in the city's programme; set out again as site " + site + " where its " + stage + " stage began ("
                             + (int)Math.Round(from * 100.0) + "% of " + p.Hours + " working hours), its inspections and payments as they were.");
                Tell(NotificationType.Warning, "reallife_bld_notice_site_again", ProjectName(p), BuildCatalogue.StageKeys[(int)stage], TabProjects, p.Id);
                Changed();
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

        // The hour (day and hour) the programme works it: the rules decide what it comes to (SiteRules.Hour), this does it.
        private float Work(ProjectFile p, double done, double needed, float hours, int day, int hour)
        {
            if (needed <= 0.0)
            {
                return hours;
            }
            ProjectKind model = p.Model;
            if (p.Hired && p.WaitHours > 0)
            {
                HireResumed(p, done / needed, day);   // a site an older version held at a stage's end for good goes on from where it stands
            }
            WorkStage stage = PlansRules.WorkingStage(model, done / needed, PlansRules.Passed(p.Insp));
            if ((int)stage != p.StageSeen)
            {
                if (p.Hired)
                {
                    HiredStage(p, stage);   // its materials, plant and crews are the builder's own (ConstructionDirector.Hire.cs)
                }
                else
                {
                    EnterStage(p, stage);
                }
            }
            int pace = p.Hired ? p.HirePace : Pace(p, stage, day);
            double share = p.Hired ? 1.0 : SiteRules.MaterialsShare(p.Have, p.Need);   // read after a new stage has set out what it needs
            int inspection = PlansRules.InspectionAfter(model, stage);
            InspectionState held = inspection >= 0 ? p.Insp[inspection] : InspectionState.Passed;
            SiteHour work = SiteRules.Hour(model, stage, done, needed, hours, pace, share, held, inspection >= 0 ? p.InspDay[inspection] : 0, p.Rework, day, hour);
            p.Rework = work.Rework;
            switch (work.Call)
            {
                case SiteCall.Due:
                case SiteCall.Redo:
                    InspectionDue(p, work.Inspection, day, hour);   // a builder books it at this hour; the company's manager, or the player, books it
                    break;
                case SiteCall.Held:
                    if (p.Hired && p.Insp[work.Inspection] == InspectionState.Due)
                    {
                        HiredBooks(p, work.Inspection, day, hour);   // a builder never leaves its inspection unbooked
                    }
                    break;
                case SiteCall.Visit:
                    Inspect(p, work.Inspection, day);   // the inspector's visit at the hour's own time, whenever the programme works it
                    break;
                case SiteCall.Short:
                    p.WaitHours++;   // a working hour lost for want of materials
                    break;
                case SiteCall.Worked:
                    p.PaceSum += pace;
                    p.PaceDays++;
                    break;
            }
            return (float)work.Hours;
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
                p.Need[m] = ExposureRules.StageNeed(p, stage, (Material)m);   // one rule with the hedge's exposure
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

        // An inspection falls due in the hour (day and hour) the programme works the site.
        private void InspectionDue(ProjectFile p, int inspection, int day, int hour)
        {
            p.Insp[inspection] = InspectionState.Due;
            Logger?.Info("Construction: project " + p.Id + " is ready for its " + (Inspection)inspection + " inspection.");
            if (p.Hired)
            {
                HiredBooks(p, inspection, day, hour);   // the builder books the inspector and pays the fee itself
                return;
            }
            if (!ManagerBooks(p, inspection, day, hour))
            {
                Tell(NotificationType.Info, "reallife_bld_notice_inspection_due", ProjectName(p), BuildCatalogue.InspectionKeys[inspection], TabProjects, p.Id);
            }
        }

        /// <summary>Books the inspector for a project's due inspection: its fee now, the visit on the next working day. Returns a text key of the answer.</summary>
        public string BookInspection(int projectId)
        {
            ProjectFile p = _state != null ? _state.Project(projectId) : null;
            return Book(p, p != null ? DueInspection(p) : -1, ConstructionGame.Day, ConstructionGame.Hour);
        }

        // The booking made at a day's hour: the player's now, a site manager's when the stage ended.
        private string Book(ProjectFile p, int inspection, int day, int hour)
        {
            if (p == null || inspection < 0 || p.Insp[inspection] != InspectionState.Due)
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
            p.InspDay[inspection] = SiteRules.InspectorDay(day, hour);
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
            bool hired = p.Hired;
            int builder = hired ? CommissionRules.Quality(_state.Rivals[p.Contractor].Reputation) : 0;
            int crewSkill = hired ? builder : CrewSkill(p);
            bool manager = true;   // a builder of the city's runs its sites with a manager of its own
            int managerSkill = hired ? builder : ManagerSkill(p, out manager);
            int quality = SiteRules.WorkQuality(crewSkill, managerSkill, p.Quality);
            bool rushed = !hired && p.PaceDays > 0 && SiteRules.Rushed(p.PaceSum / p.PaceDays);
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
            if (hired)
            {
                Logger?.Info("Construction: project " + p.Id + " failed its " + (Inspection)inspection + " inspection on day " + day + "; builder " + p.Contractor + " puts it right ("
                             + (p.Rework / 10.0).ToString("0.#", CultureInfo.InvariantCulture) + " working hour(s)) and books the inspector again.");
                TellHire(NotificationType.Warning, "reallife_bld_notice_hire_failed", p, 0L, BuildCatalogue.InspectionKeys[inspection]);   // the builder puts it right in its own time
                return;
            }
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
            if (p.Hired)
            {
                LoanDraw(p, inspection + 1);   // the client's loan pays out as the company's would, before the builder's stage is paid
                HireStagePaid(p, inspection);
                return;
            }
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
                ManagerBooks(p, due, day, hour);
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

        private bool ManagerBooks(ProjectFile p, int inspection, int day, int hour)
        {
            if (ManagerOf(p) == null || p.Insp[inspection] != InspectionState.Due)
            {
                return false;
            }
            return Book(p, inspection, day, hour) == "reallife_bld_answer_inspection_booked";
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
            bool account = false;
            if (!ConstructionGame.Take(cost, "reallife_transaction_bld_materials", ProjectName(p), true))
            {
                if (!OnAccount(p, m0, cost, ConstructionGame.Day))
                {
                    return "reallife_bld_answer_no_money";
                }
                account = true;   // the merchant delivers on account: the company pays in its days, or is taken to court
            }
            else
            {
                Spend(p, cost);
            }
            for (int m = 0; m < (int)Material.Count; m++)
            {
                p.OnOrder[m] = Math.Max(0, p.Need[m] - p.Have[m]);
            }
            p.Merchant = m0;
            p.Orders++;
            p.OrderDay = SiteRules.DeliveryDay(ConstructionGame.Day, ConstructionGame.Hour, m0);
            Logger?.Info("Construction: project " + p.Id + " ordered " + LoadsLine(p.OnOrder) + " from merchant " + m0 + " for " + cost + ", to come on day " + p.OrderDay + ".");
            Changed();
            return account ? "reallife_bld_answer_ordered_account" : "reallife_bld_answer_ordered";
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
            _state.PayOut(amount, ConstructionGame.Day);
        }
    }
}
