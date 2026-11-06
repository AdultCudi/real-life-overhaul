using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The director's part for renovations: the interior of a unit of the player's (a business, a home or an empty unit, in a
    /// building they own or rent) refitted by their own company's crews (a project of its own, the programme's refit works behind
    /// a hoarding at its front) or, without a licensed company, by one of the city's builders at its quote, and furnished anew at
    /// the handover with one of the game's own interior designs, through the game's own installation contract, or its furniture
    /// kept. A business is kept closed while the works go on (business pressure's closure, held a few hours at a time), and at the
    /// handover Interiors takes its wear away and its new fittings lift the decor score. An exterior refresh is the construction
    /// game's rebuild of a building the player owns, which may carry the interior refit with it (WithRefit); and businesses of the
    /// city's put their own refits out to tender, the renovation speciality's work, each wearing another of the game's layouts
    /// for its kind once the works are done. On the hour, the day and a press; nothing per frame.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private readonly List<BusinessView> _businesses = new List<BusinessView>(8);
        private readonly List<string> _designs = new List<string>(16);
        private readonly List<string> _layouts = new List<string>(8);

        /// <summary>True while renovations are on: the construction game running and their option.</summary>
        public bool RefitsOn => Running && Settings.ConstructionRenovations;

        /// <summary>The player's businesses that can be refitted (owned or rented, theirs in a session), into the caller's list.</summary>
        public void Refittable(List<BusinessView> into)
        {
            ConstructionGame.Businesses(into);
        }

        /// <summary>Every unit of the player's that can be refitted and furnished anew: businesses, homes and empty units they rent or own, theirs in a session.</summary>
        public void RefitUnits(List<UnitView> into)
        {
            ConstructionGame.Units(into);
        }

        /// <summary>Why a unit cannot be refitted now, as a text key; null when it can.</summary>
        public string RefitBlock(string street, int number)
        {
            if (!RefitsOn)
            {
                return "reallife_bld_answer_refit_off";
            }
            if (!ConstructionGame.Unit(street, number, out _))
            {
                return "reallife_bld_answer_refit_not_yours";
            }
            if (_state.AtBuilding(street, number) != null)
            {
                return "reallife_bld_answer_building_busy";
            }
            return null;
        }

        /// <summary>The quote of the city's best renovation contractor for a business's refit today.</summary>
        public RefitQuote QuoteFor(BusinessView b)
        {
            return RefitRules.Quote(_state.Seed, ConstructionGame.Day, b.Sqm, ConstructionGame.Prices);
        }

        /// <summary>The quote of the city's best renovation contractor for a unit's refit today, the same as a business's of its size.</summary>
        public RefitQuote QuoteFor(UnitView u)
        {
            return RefitRules.Quote(_state.Seed, ConstructionGame.Day, u.Sqm, ConstructionGame.Prices);
        }

        /// <summary>The game's interior designs a unit can be furnished with, in the order the refit's selector lists them (after "keep the furniture").</summary>
        public void DesignsFor(UnitView u, List<string> into)
        {
            ConstructionGame.Designs(u, into);
        }

        /// <summary>
        /// What furnishing a unit with one of the game's designs would cost at the handover, as the game's own installation
        /// contract works it out (RefitRules.FurnishPrice), with the firm's fee, the pieces and what the pieces there sell for.
        /// False when the design cannot be read. Kept for the last unit and design asked that day, as the page asks on every redraw.
        /// </summary>
        public bool FurnishCost(UnitView u, string design, out long price, out long fee, out long pieces, out long sold)
        {
            string key = ConstructionGame.Place(u.Street, u.Number) + "|" + (design ?? string.Empty);
            if (key != _furnishKey || ConstructionGame.Day != _furnishDay)
            {
                _furnishRead = ConstructionGame.FurnishFigures(u, design, out _furnishFee, out _furnishPieces, out _furnishSold);
                _furnishKey = key;
                _furnishDay = ConstructionGame.Day;
            }
            fee = _furnishFee;
            pieces = _furnishPieces;
            sold = _furnishSold;
            price = RefitRules.FurnishPrice(fee, pieces, sold);   // the game's own figures, at the game's own prices
            return _furnishRead;
        }

        private string _furnishKey;
        private int _furnishDay = -1;
        private long _furnishFee, _furnishPieces, _furnishSold;
        private bool _furnishRead;

        /// <summary>A refit by the company's own crews: a project ready to start (no plans or permission for an interior). Its id, or 0 with why not.</summary>
        public int NewRefit(string street, int number, out string why)
        {
            return NewRefit(street, number, null, out why);
        }

        /// <summary>
        /// A refit by the company's own crews, furnished at the handover with one of the game's designs for the unit (null keeps
        /// its furniture): a project ready to start (no plans or permission for an interior). Its id, or 0 with why not.
        /// </summary>
        public int NewRefit(string street, int number, string design, out string why)
        {
            why = RefitBlock(street, number);
            if (why != null)
            {
                return 0;
            }
            if (_state.Firm.Founded <= 0)
            {
                why = "reallife_bld_answer_no_company";
                return 0;
            }
            if (!Room(out why))
            {
                return 0;
            }
            ConstructionGame.Unit(street, number, out UnitView u);
            int hours = RefitRules.Hours(u.Sqm);
            Exchange(_exchange);
            var p = new ProjectFile
            {
                Id = _state.NewId(), Kind = ProjectKind.Refit, Stage = ProjectStage.Ready, Created = ConstructionGame.Day, Street = street, Number = number, District = u.District,
                Use = UseOf(u), Area = Math.Max(1, u.Sqm), Storeys = 1, Hours = hours, CivicKind = RefitRules.Works, Source = TenderSource.Client,
                Quality = 70, PlansDone = true, Engineered = true, Planning = PlanningState.Granted
            };
            Fit(p, u, design);
            p.Estimate = PlansRules.EstimateWorks(RefitRules.Works, hours, ConstructionGame.Prices, _exchange).Total;
            _state.Projects.Add(p);
            Logger?.Info("Construction: project " + p.Id + " made to refit " + u.Name + " (" + u.Sqm + " m2): " + hours + " working hours, estimated at " + p.Estimate
                         + (p.FitDesign.Length > 0 ? ", furnished at the handover with the game's design " + p.FitDesign : ", its furniture kept") + ".");
            Changed();
            return p.Id;
        }

        /// <summary>
        /// A refit by the city's best renovation contractor at today's quote, paid now: the hoarding goes up at once and the business
        /// closes for the works. Its project's id, or 0 with why not.
        /// </summary>
        public int ContractorRefit(string street, int number, bool free, out string why)
        {
            return ContractorRefit(street, number, free, null, out why);
        }

        /// <summary>
        /// A refit by the city's best renovation contractor at today's quote, paid now, furnished at the handover with one of the
        /// game's designs for the unit (null keeps its furniture): the hoarding goes up at once and a business closes for the works.
        /// Its project's id, or 0 with why not.
        /// </summary>
        public int ContractorRefit(string street, int number, bool free, string design, out string why)
        {
            why = RefitBlock(street, number);
            if (why != null)
            {
                return 0;
            }
            if (!Room(out why))
            {
                return 0;
            }
            ConstructionGame.Unit(street, number, out UnitView u);
            RefitQuote q = QuoteFor(u);
            if (!free && !ConstructionGame.Take(q.Price, "reallife_transaction_bld_refit", u.Name, true))
            {
                why = "reallife_bld_answer_no_money";
                return 0;
            }
            int site = StartWorksSite(street, number, RefitRules.Works, true, q.Rival, q.Hours, out string refused);
            if (site <= 0)
            {
                if (!free)
                {
                    ConstructionGame.Give(q.Price, "reallife_transaction_bld_refit_back", u.Name);   // nothing started: the money comes back
                }
                Logger?.Info("Construction: the contractor's refit at " + u.Name + " could not start: " + refused + ".");
                why = SiteRefusals.Key(refused);
                return 0;
            }
            int day = ConstructionGame.Day;
            var p = new ProjectFile
            {
                Id = _state.NewId(), Kind = ProjectKind.Refit, Stage = ProjectStage.Building, Created = day, Street = street, Number = number, District = u.District,
                Use = UseOf(u), Area = Math.Max(1, u.Sqm), Storeys = 1, Hours = q.Hours, CivicKind = RefitRules.Works, Source = TenderSource.Client,
                Quality = 70, PlansDone = true, Engineered = true, Planning = PlanningState.Granted, Contractor = q.Rival, ContractorPrice = free ? 0L : q.Price, SiteId = site, StartDay = day,
                DueDay = PlanningRules.AfterWorkingDays(day, q.Days), Spent = free ? 0L : q.Price
            };
            Fit(p, u, design);
            _state.Projects.Add(p);
            HoldShut(p);
            Logger?.Info("Construction: project " + p.Id + ": " + u.Name + " refitted by contractor " + q.Rival + " for " + q.Price + " in about " + q.Days + " working day(s), site " + site
                         + (p.FitDesign.Length > 0 ? ", furnished at the handover with the game's design " + p.FitDesign : string.Empty) + ".");
            Changed();
            return p.Id;
        }

        /// <summary>
        /// The design a refit not yet handed over is furnished with at its handover: one of the game's designs for its unit, or none
        /// (null or empty) to keep the furniture there. Returns a text key, null when set.
        /// </summary>
        public string SetFitDesign(ProjectFile p, string design)
        {
            if (p == null || p.Kind != ProjectKind.Refit || !p.Live)
            {
                return "reallife_bld_answer_too_late";
            }
            if (!ConstructionGame.Unit(p.Street, p.Number, out UnitView u))
            {
                return "reallife_bld_answer_refit_not_yours";
            }
            if (!string.IsNullOrEmpty(design))
            {
                ConstructionGame.Designs(u, _designs);
                if (!_designs.Contains(design))
                {
                    return "reallife_bld_answer_no_design";
                }
            }
            Fit(p, u, design);
            Changed();
            return null;
        }

        // The design and the firm's folder it is in (the business's type, or the empty business's for a home or an empty unit).
        private static void Fit(ProjectFile p, UnitView u, string design)
        {
            p.FitDesign = string.IsNullOrEmpty(design) ? string.Empty : design;
            p.FitType = p.FitDesign.Length > 0 ? u.Business ?? string.Empty : string.Empty;
        }

        // The use a unit's building is, for the refit's project.
        private static BuildUse UseOf(UnitView u)
        {
            int use = BuildCatalogue.UseOf(u.Type);
            return use >= 0 ? (BuildUse)use : BuildUse.Shops;
        }

        /// <summary>A rebuild of a building of the player's carries an interior refit too (or not), before its site starts. Returns a text key, null when set.</summary>
        public string SetWithRefit(ProjectFile p, bool with)
        {
            if (p == null || p.Kind != ProjectKind.Rebuild || p.Stage == ProjectStage.Building || !p.Live)
            {
                return "reallife_bld_answer_too_late";
            }
            if (with && !ConstructionGame.Business(p.Street, p.Number, out _))
            {
                return "reallife_bld_answer_refit_not_yours";
            }
            p.WithRefit = with;
            Changed();
            return null;
        }

        // ------------------------------------------------------------------ the works

        /// <summary>True while a project's works keep its business closed: a refit going up, a rebuild's fit-out with the refit.</summary>
        internal static bool ClosesShop(ProjectFile p)
        {
            if (p.Stage != ProjectStage.Building)
            {
                return false;
            }
            return p.Kind == ProjectKind.Refit || (p.Kind == ProjectKind.Rebuild && p.WithRefit && p.StageSeen >= (int)WorkStage.Envelope);
        }

        // The hour: every business under a refit held closed a few hours more, so it opens by itself if the construction game stops.
        private void RefitHour()
        {
            if (!RefitsOn)
            {
                return;
            }
            for (int i = 0; i < _state.Projects.Count; i++)
            {
                if (ClosesShop(_state.Projects[i]))
                {
                    HoldShut(_state.Projects[i]);
                }
            }
        }

        private void HoldShut(ProjectFile p)
        {
            try
            {
                if (!Down("CloseShop") && ConstructionBridges.CloseShop != null && !ConstructionBridges.CloseShop(p.Street, p.Number, BuildCatalogue.RefitHold) && Verbose)
                {
                    Logger?.Info("Renovation check (closed): " + ProjectName(p) + " could not be kept closed for its works (closed by something else, or not the player's): PASS.");
                }
            }
            catch (Exception e)
            {
                Faulty("CloseShop", e);
            }
        }

        private static void LetOpen(ProjectFile p)
        {
            try
            {
                if (!Down("OpenShop"))
                {
                    ConstructionBridges.OpenShop?.Invoke(p.Street, p.Number);
                }
            }
            catch (Exception e)
            {
                Faulty("OpenShop", e);
            }
        }

        /// <summary>
        /// A refit's handover: a business opens, its new furnishing goes in through the game's own installation contract when a
        /// design was chosen, and Interiors takes a business's wear away and gives it new fittings of the work's grade (a
        /// contractor's good, the company's by its work and a grade better as a renovation company). Said once in the log.
        /// </summary>
        private void Refitted(ProjectFile p, int quality)
        {
            bool special = p.Contractor < 0 && FirmRules.Special(_state.Firm.Speciality, p.Model, p.Use, p.CivicKind);
            p.Grade = p.Contractor >= 0 ? BuildCatalogue.ContractorGrade : RefitRules.Grade(quality, special);
            GrowRefurbished(p.Street, p.Number);   // a grown building refurbished: its condition as new
            LetOpen(p);
            string furnished = Furnished(p);
            bool business = p.Kind != ProjectKind.Refit || (ConstructionGame.Unit(p.Street, p.Number, out UnitView u) && u.Trading);
            bool recorded = false;
            try
            {
                recorded = business && !Down("Refitted") && ConstructionBridges.Refitted != null && ConstructionBridges.Refitted(p.Street, p.Number, p.Grade);
            }
            catch (Exception e)
            {
                Faulty("Refitted", e);
            }
            Logger?.Info("Renovation check (handover): " + ProjectName(p) + " refitted by " + (p.Contractor >= 0 ? "contractor " + p.Contractor : "the company") + ", grade " + p.Grade
                         + (special ? " (the renovation speciality's)" : string.Empty) + "; "
                         + (business ? "the business open again, its interior " + (recorded ? "refitted" : "not recorded (Interiors off)")
                             : "a home or an empty unit, its finishes renewed") + furnished + ": " + (!business || recorded || !Settings.Interiors ? "PASS" : "FAIL") + ".");
        }

        // The design chosen for a refit laid by the game's own installers: at once, or once the player has left the unit (the
        // game's next day's pass, as for a contract of its own). In words for the handover's line.
        private string Furnished(ProjectFile p)
        {
            if (string.IsNullOrEmpty(p.FitDesign))
            {
                return ", its furniture kept";
            }
            if (!ConstructionGame.Unit(p.Street, p.Number, out UnitView u))
            {
                return ", not furnished: the unit is no longer the player's";
            }
            if (u.Business != p.FitType)
            {
                return ", not furnished: its business is another kind now, and the design was one for " + p.FitType;
            }
            try
            {
                if (!ConstructionGame.Furnish(u, p.FitDesign, out bool now, out string why))
                {
                    return ", not furnished: " + why;
                }
                return now ? ", furnished with the game's design " + p.FitDesign + " by the game's own installers"
                    : ", the game's own installers lay the design " + p.FitDesign + " once the player is out of the unit";
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return ", not furnished: the game's installation failed (Player.log has it)";
            }
        }

        /// <summary>
        /// A business of the city's refitted by tender, by a rival or by the player's company, wears another of the game's own
        /// layouts for its kind at its size from then on (its interior is laid from its layout whenever it is entered): the
        /// city's side of refurnishing. Never in a Going Public session, so every machine's AI businesses stay alike.
        /// </summary>
        private void AiRefurnish(string street, int number, int tender)
        {
            if (GoingPublic.InSession || !Settings.ConstructionRenovations || string.IsNullOrEmpty(street))
            {
                return;
            }
            GrowRefurbished(street, number);   // a grown building refurbished by the city's tender: its condition as new
            try
            {
                if (!ConstructionGame.AiLayouts(street, number, _layouts, out int current))
                {
                    Logger?.Info("Renovation check (the city's business): " + street + " " + number + " after tender " + tender
                                 + " keeps its interior (no business of the city's of its own there now, or a chain's store): PASS.");
                    return;
                }
                int pick = RefitRules.AiLayout(_state.Seed, tender, _layouts.Count, current);
                if (pick < 0)
                {
                    Logger?.Info("Renovation check (the city's business): " + street + " " + number + " after tender " + tender
                                 + " keeps its interior: the game has no other layout of its kind at its size: PASS.");
                    return;
                }
                string was = current >= 0 ? _layouts[current] : "(not one of the list)";
                bool set = ConstructionGame.SetAiLayout(street, number, _layouts[pick]);
                Logger?.Info("Renovation check (the city's business): " + street + " " + number + " refitted after tender " + tender + ": its layout " + was + " -> " + _layouts[pick]
                             + " of " + _layouts.Count + ": " + (set ? "PASS" : "FAIL") + ".");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            finally
            {
                _layouts.Clear();
            }
        }

        // A contractor's refit or demolition handed over: nothing of the company's in it, no opening day; the player is told.
        private void ContractorHandover(ProjectFile p, int day)
        {
            p.Stage = ProjectStage.Finished;
            p.Handed = day;
            p.SiteId = 0;
            p.Opened = true;   // no ribbon for an interior, nor for ground cleared
            if (p.Kind == ProjectKind.Demolition)
            {
                Demolished(p);   // its ground the player's land; asked again each day while the district cannot take it
                return;
            }
            Refitted(p, 70);
            Tell(NotificationType.Success, "reallife_bld_notice_refitted", ProjectName(p), BuildCatalogue.RivalKeys[BuildCatalogue.Clamp(p.Contractor, 0, BuildCatalogue.RivalCount - 1)],
                TabProjects, p.Id);
        }

        /// <summary>A refit's works in words for its business's Interior tab, or null while none is under way there.</summary>
        public string RenovationWords(string street, int number)
        {
            ProjectFile p = _state != null ? _state.AtBuilding(street, number) : null;
            if (p == null || (p.Kind == ProjectKind.Rebuild && !p.WithRefit))
            {
                return null;
            }
            if (p.Stage != ProjectStage.Building)
            {
                return ConstructionText.T("reallife_bld_refit_waiting", "stage", ConstructionText.StageWords(p));
            }
            return ConstructionText.T("reallife_bld_refit_under_way", "by", p.Contractor >= 0 ? ConstructionText.T(BuildCatalogue.RivalKeys[p.Contractor]) : NoticeName(p.SiteId) ?? string.Empty,
                "day", ConstructionText.Day(p.DueDay));
        }

        // ------------------------------------------------------------------ businesses' refits out to tender

        private void RefitTenders(int day)
        {
            if (day < _state.NextRefitDay || !Settings.ConstructionDevelopers || !Settings.ConstructionRenovations)
            {
                return;
            }
            _state.NextRefitDay = day + TenderRules.NextIn(_state.Seed, day, TenderSource.Refit);
            BuildRandom random = BuildRandom.Of(_state.Seed, day, 239);
            if (!ConstructionGame.Client(random.Range(0, 100000), out BuildingView b) || BuildCatalogue.UseOf(b.Type) < 0)
            {
                return;
            }
            var t = new TenderFile
            {
                Id = _state.NewId(), Source = TenderSource.Refit, CivicKind = RefitRules.Works, Street = b.Street, Number = b.Number, Use = (BuildUse)BuildCatalogue.UseOf(b.Type),
                Area = Math.Max(1, b.TotalSqm), Client = random.Range(0, BuildCatalogue.ClientKeys.Length), Opened = day * 24 + 8
            };
            t.Hours = RefitRules.Hours(t.Area);
            t.Value = RefitRules.Value(t.Hours, ConstructionGame.Prices);
            t.Closes = t.Opened + BuildCatalogue.TenderHours;
            AddTender(t);
            if (_state.Firm.Founded > 0)
            {
                Tell(NotificationType.Info, "reallife_bld_notice_tender", TenderName(t), null, TabTenders, t.Id);
            }
        }
    }
}
