using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Let it grow on the player's own zoned land (plans/growables.md section 3): beside Choose a building, the player picks a
    /// budget tier (basic, standard, premium: GrowRules' cost, build quality and rent level) and a builder picks the design that
    /// fits the squares by the demand and the tier, its practice draws the plans at the tier's quality and the tier's cost, and
    /// the builder carries them to City Hall (the engineer's sign off where the storeys need it, the application and its fee).
    /// The crews or a hired builder, the costs and the loan are the player's as for any project. At the keys the building is the
    /// player's: its deed asks the tier's rent, the developer finds tenants in its first weeks beside the game's own lettings
    /// (they pay the player each night as the game's own rent revenue, in the game's books), or the player keeps a unit for a
    /// business of their own in BizMan, at once from the notice or later. Its condition is then watched as every grown
    /// building's. The rivals' holdings grow on land of theirs by the same tiers (ConstructionDirector.Grow).
    /// COST: nothing per frame; a press, and a look at the player's growing projects on the day's pass.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private readonly List<DesignChoice> _growChoices = new List<DesignChoice>(16);

        /// <summary>
        /// The use a Let it grow footprint takes: among the uses its squares carry in force (painted and allowed), the one with the
        /// strongest demand; -1 when none is.
        /// </summary>
        public int GrowUseOf(int firstColumn, int count, int rows)
        {
            CellGrid grid = Cells;
            int best = -1, bestDemand = int.MinValue;
            for (int u = 0; u < (int)BuildUse.Count; u++)
            {
                bool carried = false;
                int at = firstColumn;
                for (int i = 0; i < count && at >= 0 && !carried; i++, at = grid.Next(at))
                {
                    for (int r = 0; r < rows && !carried; r++)
                    {
                        carried = PaintOfSquare(at, r, out bool waits) == u + 1 && !waits;
                    }
                }
                int demand = DemandFor((BuildUse)u);
                if (carried && demand > bestDemand)
                {
                    best = u;
                    bestDemand = demand;
                }
            }
            return best;
        }

        /// <summary>A tier's estimate for a footprint: the design the builder would pick, its estimate times the tier's share; 0 while none can be picked.</summary>
        public long GrowEstimate(int firstColumn, int count, int rows, GrowTier tier, out string design)
        {
            design = null;
            int use = GrowUseOf(firstColumn, count, rows);
            if (use < 0 || !GrowPick(firstColumn, count, rows, (BuildUse)use, tier, out DesignChoice d))
            {
                return 0L;
            }
            design = d.Name;
            PlaceWarnings(firstColumn, count, d, true, (BuildUse)use, PlansRules.ParkingNeeded((BuildUse)use, Math.Max(1, d.TotalSqm), Permits()) > 0, out long estimate, out _);
            return GrowRules.TierCost(estimate, tier);
        }

        // The design the builder picks for a footprint at a tier: of those that fit and may be built there, by the tier and the demand among them by floor area.
        private bool GrowPick(int firstColumn, int count, int rows, BuildUse use, GrowTier tier, out DesignChoice design)
        {
            design = default(DesignChoice);
            if (!FootprintDesigns(firstColumn, count, rows, use, _growChoices))
            {
                return false;
            }
            for (int i = _growChoices.Count - 1; i >= 0; i--)
            {
                bool parking = PlansRules.ParkingNeeded(use, Math.Max(1, _growChoices[i].TotalSqm), Permits()) > 0;
                int warnings = PlaceWarnings(firstColumn, count, _growChoices[i], true, use, parking, out _, out _);
                if (!BuildModeRules.MayBuild(warnings) || (warnings & BuildModeRules.WarnTall) != 0)
                {
                    _growChoices.RemoveAt(i);   // the builder builds nothing the code or the squares stop
                }
            }
            int pick = BySize(_growChoices, GrowRules.TierPick(_growChoices.Count, tier, DemandFor(use)));
            if (pick < 0)
            {
                return false;
            }
            design = _growChoices[pick];
            return true;
        }

        /// <summary>
        /// Let it grow on a footprint of the player's zoned squares at a tier: the builder's design placed (as Place a building places
        /// one), its plans asked of the tier's practice at the tier's cost. Returns the project's id, or 0 with why (a text key).
        /// </summary>
        public int LetItGrow(int firstColumn, int count, int rows, GrowTier tier, out string why)
        {
            why = null;
            if (GoingPublic.InSession)
            {
                why = "reallife_bld_answer_session_land";
                return 0;
            }
            if (!Settings.ConstructionGrowth)
            {
                why = "reallife_grow_answer_off";
                return 0;
            }
            int use = GrowUseOf(firstColumn, count, rows);
            if (use < 0)
            {
                why = "reallife_grow_answer_no_zone";
                return 0;
            }
            if (!GrowPick(firstColumn, count, rows, (BuildUse)use, tier, out DesignChoice design))
            {
                why = "reallife_grow_answer_no_design";
                return 0;
            }
            bool parking = PlansRules.ParkingNeeded((BuildUse)use, Math.Max(1, design.TotalSqm), Permits()) > 0;   // the builder provides what the plans will ask
            int id = PlaceProject(firstColumn, count, design, (BuildUse)use, parking, out why);
            ProjectFile p = _state.Project(id);
            if (p == null)
            {
                return 0;
            }
            int t = BuildCatalogue.Clamp((int)tier, 0, (int)GrowTier.Count - 1);
            p.Grow = t + 1;
            p.CostPermille = BuildCatalogue.Clamp(GrowRules.TierCostPermille[t], BuildCatalogue.DesignCostLeast, BuildCatalogue.DesignCostMost);
            SetArchitect(p, ArchitectKind.Firm, GrowRules.TierFirm[t], null);
            string plans = DrawPlans(p);
            why = plans == "reallife_bld_answer_plans_asked" ? "reallife_grow_answer_growing" : plans;
            Logger?.Info("Construction: Let it grow at " + p.Lot + " (" + count + " space(s), " + (BuildUse)use + ", " + tier + "): " + (string.IsNullOrEmpty(design.Look) ? design.Name : design.Look)
                         + ", project " + id + ", the plans: " + plans + ".");
            Changed();
            return id;
        }

        // The day: each Let it grow project of the player's carried by its builder from its plans to City Hall (the engineer where the
        // storeys need one, then the application), each step's fee the player's as for any project.
        private void GrowProjectsDay(int day)
        {
            for (int i = 0; i < _state.Projects.Count; i++)
            {
                ProjectFile p = _state.Projects[i];
                if (p.Grow <= 0 || p.Kind != ProjectKind.Plot || p.Stage != ProjectStage.Design || !p.PlansDone || p.Planning != PlanningState.None)
                {
                    continue;
                }
                if (!p.Engineered)
                {
                    if (p.EngineerDay <= 0)
                    {
                        string asked = AskEngineer(p, false);
                        Logger?.Info("Construction: Let it grow project " + p.Id + "'s engineer asked on day " + day + ": " + asked + ".");
                    }
                    continue;
                }
                string applied = Apply(p);
                if (applied == "reallife_bld_answer_applied")
                {
                    Tell(NotificationType.Info, "reallife_grow_notice_applied", ProjectName(p), null, TabProjects, p.Id);
                }
                Logger?.Info("Construction: Let it grow project " + p.Id + " put to City Hall on day " + day + ": " + applied + ".");
            }
        }

        // A Let it grow building handed to the district as the player's: one of the city's grown buildings from now (its condition
        // watched), its deed asking the tier's rent once it is written, and the player asked whether to keep a unit for a business.
        private void GrowHandedToPlayer(ProjectFile p, int day)
        {
            if (p.Grow <= 0 || GrowOf(p.Lot) != null)
            {
                return;
            }
            LotFile land = _state.Lot(p.Lot);
            var g = new GrowFile
            {
                Id = _state.NewId(), Stage = GrowStage.Letting, Owner = GrowOwner.Player, Lot = p.Lot, Spaces = land != null ? land.Spaces : 1, Use = p.Use,
                Tier = (GrowTier)BuildCatalogue.Clamp(p.Grow - 1, 0, (int)GrowTier.Count - 1), Day = p.Created, Until = day, Design = string.IsNullOrEmpty(p.Look) ? p.Design : p.Look,
                Handed = day, Project = p.Id
            };
            _state.Grows.Add(g);
            TidyGrows();
            GrowsChanged();
            Logger?.Info("Construction: Let it grow project " + p.Id + "'s building is the player's: its deed asks the " + g.Tier + " rent once written.");
        }

        // The day: a Let it grow building of the player's whose deed is written now asks its tier's rent, and the player is told.
        private void GrowDeedsDay()
        {
            for (int i = 0; i < _state.Grows.Count; i++)
            {
                GrowFile g = _state.Grows[i];
                if (g.Owner != GrowOwner.Player || g.Stage != GrowStage.Letting || !Space(g.Lot, out LotView lot))
                {
                    continue;
                }
                if (!ConstructionGame.DeedRent(lot.Street, lot.Number, g.Tier, out float rent))
                {
                    if (ConstructionGame.Day > g.Handed + GrowRules.PatienceDays)
                    {
                        g.Stage = GrowStage.Let;   // no deed came (its flats sold, or the district never stood it): watched as it is
                        g.Week = ConstructionGame.Day / 7;
                    }
                    continue;
                }
                g.Stage = GrowStage.Let;
                g.Week = ConstructionGame.Day / 7;
                string street = lot.Street;
                int number = lot.Number;
                var data = new Dictionary<string, string>(2) { { "name", ConstructionGame.AddressText(street, number) }, { "tier", ConstructionText.T(GrowRules.TierKeys[(int)g.Tier]) } };
                ConstructionGame.Tell(NotificationType.Success, "reallife_grow_notice_keys", data, () => ConstructionGame.BizMan(street, number));
                Logger?.Info("Construction: the player's Let it grow building at " + street + " " + number + " asks " + rent.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                             + " a square metre a day (" + g.Tier + "); its tenants move in over the coming weeks.");
            }
        }

        /// <summary>The player's Let it grow building of a project, standing, or null.</summary>
        public GrowFile GrowOfProject(ProjectFile p)
        {
            for (int i = 0; p != null && _state != null && i < _state.Grows.Count; i++)
            {
                if (_state.Grows[i].Owner == GrowOwner.Player && _state.Grows[i].Project == p.Id && _state.Grows[i].Standing)
                {
                    return _state.Grows[i];
                }
            }
            return null;
        }

        /// <summary>A unit of the player's Let it grow building kept for a business of their own: the game's BizMan opened on it. Returns a text key, or null.</summary>
        public string KeepUnit(ProjectFile p)
        {
            GrowFile g = GrowOfProject(p);
            if (g == null || !Space(g.Lot, out LotView lot))
            {
                return "reallife_grow_answer_not_standing";
            }
            return ConstructionGame.BizMan(lot.Street, lot.Number) ? null : "reallife_grow_answer_no_bizman";
        }
    }
}
