using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The director's part for renovations: the interior of a business of the player's (in a building they own or rent) refitted
    /// by their own company's crews (a project of its own, the programme's refit works behind a hoarding at its front) or, without
    /// a licensed company, by one of the city's builders at its quote. The business is kept closed while the works go on (business
    /// pressure's closure, held a few hours at a time), and at the handover Interiors takes its wear away and its new fittings lift
    /// the decor score. An exterior refresh is the construction game's rebuild of a building the player owns, which may carry the
    /// interior refit with it (WithRefit); and businesses of the city's put their own refits out to tender, the renovation
    /// speciality's work. On the hour, the day and a press; nothing per frame.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private readonly List<BusinessView> _businesses = new List<BusinessView>(8);

        /// <summary>True while renovations are on: the construction game running and their option.</summary>
        public bool RefitsOn => Running && Settings.ConstructionRenovations;

        /// <summary>The player's businesses that can be refitted (owned or rented, theirs in a session), into the caller's list.</summary>
        public void Refittable(List<BusinessView> into)
        {
            ConstructionGame.Businesses(into);
        }

        /// <summary>Why a business cannot be refitted now, as a text key; null when it can.</summary>
        public string RefitBlock(string street, int number)
        {
            if (!RefitsOn)
            {
                return "reallife_bld_answer_refit_off";
            }
            if (!ConstructionGame.Business(street, number, out _))
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

        /// <summary>A refit by the company's own crews: a project ready to start (no plans or permission for an interior). Its id, or 0 with why not.</summary>
        public int NewRefit(string street, int number, out string why)
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
            ConstructionGame.Business(street, number, out BusinessView b);
            int hours = RefitRules.Hours(b.Sqm);
            Exchange(_exchange);
            var p = new ProjectFile
            {
                Id = _state.NewId(), Kind = ProjectKind.Refit, Stage = ProjectStage.Ready, Created = ConstructionGame.Day, Street = street, Number = number, District = b.District,
                Use = b.Office ? BuildUse.Offices : BuildUse.Shops, Area = Math.Max(1, b.Sqm), Storeys = 1, Hours = hours, CivicKind = RefitRules.Works, Source = TenderSource.Client,
                Quality = 70, PlansDone = true, Engineered = true, Planning = PlanningState.Granted
            };
            p.Estimate = PlansRules.EstimateWorks(RefitRules.Works, hours, ConstructionGame.Prices, _exchange).Total;
            _state.Projects.Add(p);
            Logger?.Info("Construction: project " + p.Id + " made to refit " + b.Name + " (" + b.Sqm + " m2): " + hours + " working hours, estimated at " + p.Estimate + ".");
            Changed();
            return p.Id;
        }

        /// <summary>
        /// A refit by the city's best renovation contractor at today's quote, paid now: the hoarding goes up at once and the business
        /// closes for the works. Its project's id, or 0 with why not.
        /// </summary>
        public int ContractorRefit(string street, int number, bool free, out string why)
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
            ConstructionGame.Business(street, number, out BusinessView b);
            RefitQuote q = QuoteFor(b);
            if (!free && !ConstructionGame.Take(q.Price, "reallife_transaction_bld_refit", b.Name, true))
            {
                why = "reallife_bld_answer_no_money";
                return 0;
            }
            int site = StartWorksSite(street, number, RefitRules.Works, true, q.Rival, q.Hours, out string refused);
            if (site <= 0)
            {
                if (!free)
                {
                    ConstructionGame.Give(q.Price, "reallife_transaction_bld_refit_back", b.Name);   // nothing started: the money comes back
                }
                Logger?.Info("Construction: the contractor's refit at " + b.Name + " could not start: " + refused + ".");
                why = "reallife_bld_answer_site_refused";
                return 0;
            }
            int day = ConstructionGame.Day;
            var p = new ProjectFile
            {
                Id = _state.NewId(), Kind = ProjectKind.Refit, Stage = ProjectStage.Building, Created = day, Street = street, Number = number, District = b.District,
                Use = b.Office ? BuildUse.Offices : BuildUse.Shops, Area = Math.Max(1, b.Sqm), Storeys = 1, Hours = q.Hours, CivicKind = RefitRules.Works, Source = TenderSource.Client,
                Quality = 70, PlansDone = true, Engineered = true, Planning = PlanningState.Granted, Contractor = q.Rival, ContractorPrice = free ? 0L : q.Price, SiteId = site, StartDay = day,
                DueDay = PlanningRules.AfterWorkingDays(day, q.Days), Spent = free ? 0L : q.Price
            };
            _state.Projects.Add(p);
            HoldShut(p);
            Logger?.Info("Construction: project " + p.Id + ": " + b.Name + " refitted by contractor " + q.Rival + " for " + q.Price + " in about " + q.Days + " working day(s), site " + site + ".");
            Changed();
            return p.Id;
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
        /// A refit's handover: the business opens, and Interiors takes its wear away and gives it new fittings of the work's grade
        /// (a contractor's good, the company's by its work and a grade better as a renovation company). Said once in the log.
        /// </summary>
        private void Refitted(ProjectFile p, int quality)
        {
            bool special = p.Contractor < 0 && FirmRules.Special(_state.Firm.Speciality, p.Model, p.Use, p.CivicKind);
            p.Grade = p.Contractor >= 0 ? BuildCatalogue.ContractorGrade : RefitRules.Grade(quality, special);
            LetOpen(p);
            bool recorded = false;
            try
            {
                recorded = !Down("Refitted") && ConstructionBridges.Refitted != null && ConstructionBridges.Refitted(p.Street, p.Number, p.Grade);
            }
            catch (Exception e)
            {
                Faulty("Refitted", e);
            }
            Logger?.Info("Renovation check (handover): " + ProjectName(p) + " refitted by " + (p.Contractor >= 0 ? "contractor " + p.Contractor : "the company") + ", grade " + p.Grade
                         + (special ? " (the renovation speciality's)" : string.Empty) + "; the business open again, its interior " + (recorded ? "refitted" : "not recorded (Interiors off)")
                         + ": " + (recorded || !Settings.Interiors ? "PASS" : "FAIL") + ".");
        }

        // A contractor's refit handed over: nothing of the company's in it, no opening day; the player is told.
        private void ContractorHandover(ProjectFile p, int day)
        {
            p.Stage = ProjectStage.Finished;
            p.Handed = day;
            p.SiteId = 0;
            p.Opened = true;   // no ribbon for an interior
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
