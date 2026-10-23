using System;
using System.Collections.Generic;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The director's part for a project's life: made on a lot or an old building of the player's, its plans (the use, a
    /// design of the city's own, the architect, the parking), the engineer's sign off, planning permission and an appeal, the
    /// site started in the city's programme, the handover, and what the building is for afterwards. Each step on a press,
    /// or on the day's pass when its day comes.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private const int DesignsShown = 8;    // designs of the city's own an architect proposes for some ground
        private const int DesignsAsked = 32;   // read before those of a size the lot does not take are left out

        private readonly List<DesignChoice> _choices = new List<DesignChoice>(DesignsAsked);
        private readonly List<LotView> _lots = new List<LotView>(128);
        private int _lotsStamp = int.MinValue;
        private readonly List<LotView> _testLots = new List<LotView>(2);

        /// <summary>The district's lots at this load (and a test's lot), read again when the district says they changed.</summary>
        public IReadOnlyList<LotView> Lots
        {
            get
            {
                int stamp = LotsStamp();
                if (stamp != _lotsStamp)
                {
                    _lotsStamp = stamp;
                    ReadLots(_lots);
                    _devOrderStale = true;
                }
                if (_testLots.Count > 0)
                {
                    for (int i = 0; i < _testLots.Count; i++)
                    {
                        if (!HasLot(_testLots[i].Id))
                        {
                            _lots.Add(_testLots[i]);
                        }
                    }
                }
                return _lots;
            }
        }

        private bool HasLot(string id)
        {
            for (int i = 0; i < _lots.Count; i++)
            {
                if (_lots[i].Id == id)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A lot of the district (or a test's) by its id; false for one not there at this load.</summary>
        public bool Lot(string id, out LotView lot)
        {
            IReadOnlyList<LotView> lots = Lots;
            for (int i = 0; id != null && i < lots.Count; i++)
            {
                if (lots[i].Id == id)
                {
                    lot = lots[i];
                    return true;
                }
            }
            lot = default(LotView);
            return false;
        }

        // ------------------------------------------------------------------ names and values

        /// <summary>A project in words: what it builds and where.</summary>
        public string ProjectName(ProjectFile p)
        {
            if (p == null)
            {
                return string.Empty;
            }
            string where = PlaceWords(p);
            switch (p.Kind)
            {
                case ProjectKind.Rebuild:
                    return ConstructionText.T("reallife_bld_name_rebuild", "address", where);
                case ProjectKind.Contract:
                    return ConstructionText.T(p.Source == TenderSource.CityWorks ? "reallife_bld_name_works" : p.Source == TenderSource.Public
                        ? "reallife_bld_name_public" : "reallife_bld_name_client",
                        "address", where);
                default:
                    return ConstructionText.T("reallife_bld_name_plot", "use", ConstructionText.T(BuildCatalogue.UseKeys[(int)p.Use]), "address", where);
            }
        }

        /// <summary>Where a project is, in words: its lot's address, or its building's.</summary>
        public string PlaceWords(ProjectFile p)
        {
            if (!string.IsNullOrEmpty(p.Lot) && Lot(p.Lot, out LotView lot))
            {
                return ConstructionGame.AddressText(lot.Street, lot.Number);
            }
            if (!string.IsNullOrEmpty(p.Street))
            {
                return ConstructionGame.AddressText(p.Street, p.Number);
            }
            return ConstructionText.T("reallife_bld_place_lot");
        }

        /// <summary>
        /// The game's value of a project's finished building: its floor area at the market price a square metre of its
        /// district, type and traffic (Building.GetMarketValue's own arithmetic). 0 when the district's price is not known.
        /// </summary>
        public long ValueOf(ProjectFile p, out double perSqm)
        {
            perSqm = 0.0;
            double district = double.NaN;
            int traffic = 30;
            if (!string.IsNullOrEmpty(p.Lot) && Lot(p.Lot, out LotView lot))
            {
                district = PriceOf(lot);
                traffic = lot.Traffic;
            }
            else if (!string.IsNullOrEmpty(p.Street) && ConstructionGame.Building(p.Street, p.Number, out BuildingView b))
            {
                district = ConstructionGame.DistrictPrice(b.District);
                traffic = b.Traffic;
            }
            if (double.IsNaN(district) || district <= 0.0)
            {
                return 0L;
            }
            double type = ConstructionGame.TypeMultiplier(BuildCatalogue.UseTypes[(int)p.Use], out bool lower);
            perSqm = LandRules.PricePerSqm(district, type, traffic, lower);
            return LandRules.Value(p.Area, perSqm);
        }

        /// <summary>A lot's price: a share of its usual building's value at its own traffic; 0 when the district's price is not known.</summary>
        public long LotPrice(LotView lot)
        {
            double district = PriceOf(lot);
            if (double.IsNaN(district) || district <= 0.0)
            {
                return 0L;
            }
            BuildUse use = BuildCatalogue.ClassUse[BuildCatalogue.Clamp(lot.Class, 0, BuildCatalogue.ClassCount - 1)];
            double type = ConstructionGame.TypeMultiplier(BuildCatalogue.UseTypes[(int)use], out bool lower);
            return LandRules.LotPrice(lot.Width, lot.Depth, lot.Class, LandRules.PricePerSqm(district, type, lot.Traffic, lower));
        }

        // The district's price a square metre for a lot: a test lot's is the price where it was made.
        private double PriceOf(LotView lot) => IsTestLot(lot.Id) ? _testPrice : LandPrice();

        // ------------------------------------------------------------------ a project made

        /// <summary>A new building on a lot the player owns, at its plans. Returns the project's id, or 0 with the text key of why not.</summary>
        public int NewPlot(string lotId, out string why)
        {
            why = null;
            LotFile owned = _state.Lot(lotId);
            if (owned == null || !Lot(lotId, out LotView lot))
            {
                why = "reallife_bld_answer_no_lot";
                return 0;
            }
            if (_state.OnLot(lotId) != null)
            {
                why = "reallife_bld_answer_lot_busy";
                return 0;
            }
            if (!Room(out why))
            {
                return 0;
            }
            BuildUse use = FirstZoned(owned.Zones, BuildCatalogue.ClassUse[BuildCatalogue.Clamp(lot.Class, 0, BuildCatalogue.ClassCount - 1)]);
            var p = new ProjectFile
            {
                Id = _state.NewId(), Kind = ProjectKind.Plot, Stage = ProjectStage.Design, Created = ConstructionGame.Day, Lot = lotId, Use = use, Width = lot.Width, Depth = lot.Depth,
                District = string.Empty, Architect = ArchitectKind.Firm, Firm = 1
            };
            DefaultDesign(p);
            _state.Projects.Add(p);
            owned.EmptySince = 0;
            Logger?.Info("Construction: project " + p.Id + " made on lot " + lotId + " (" + lot.Width + " by " + lot.Depth + " m, class " + lot.Class + "), " + p.Use + ".");
            Changed();
            return p.Id;
        }

        /// <summary>An old building the player owns, rebuilt behind the game's scaffolding to a new look. Returns its id, or 0 with why not.</summary>
        public int NewRebuild(string street, int number, out string why)
        {
            why = null;
            if (!ConstructionGame.Owns(street, number) || !ConstructionGame.Building(street, number, out BuildingView b))
            {
                why = "reallife_bld_answer_not_yours";
                return 0;
            }
            for (int i = 0; i < _state.Projects.Count; i++)
            {
                if (_state.Projects[i].Live && _state.Projects[i].Street == street && _state.Projects[i].Number == number)
                {
                    why = "reallife_bld_answer_building_busy";
                    return 0;
                }
            }
            if (!Room(out why))
            {
                return 0;
            }
            int use = BuildCatalogue.UseOf(b.Type);
            var p = new ProjectFile
            {
                Id = _state.NewId(), Kind = ProjectKind.Rebuild, Stage = ProjectStage.Design, Created = ConstructionGame.Day, Street = street, Number = number, District = b.District,
                Use = use >= 0 ? (BuildUse)use : BuildUse.Shops, Size = b.Size, Version = b.Version, Area = Math.Max(1, b.TotalSqm), Architect = ArchitectKind.Firm, Firm = 1
            };
            DefaultDesign(p);
            _state.Projects.Add(p);
            Logger?.Info("Construction: project " + p.Id + " made to rebuild " + street + " " + number + " (" + b.Type + ", " + b.TotalSqm + " m2).");
            Changed();
            return p.Id;
        }

        private bool Room(out string why)
        {
            int live = 0;
            for (int i = 0; i < _state.Projects.Count; i++)
            {
                live += _state.Projects[i].Live ? 1 : 0;
            }
            if (live >= BuildCatalogue.ProjectsMost / 2 || _state.Projects.Count >= BuildCatalogue.ProjectsMost)
            {
                TidyProjects();
            }
            why = _state.Projects.Count >= BuildCatalogue.ProjectsMost ? "reallife_bld_answer_too_many" : null;
            return why == null;
        }

        // Finished and given up projects whose business is over make room: the oldest first.
        private void TidyProjects()
        {
            for (int i = 0; i < _state.Projects.Count && _state.Projects.Count >= BuildCatalogue.ProjectsMost; i++)
            {
                ProjectFile p = _state.Projects[i];
                if (!p.Live && p.LoanOwed == 0 && p.InvestorOwed == 0 && p.DefectDay == 0 && p.FlatsSold >= p.Flats && p.Opened)
                {
                    _state.Projects.RemoveAt(i);
                    i--;
                }
            }
        }

        private static BuildUse FirstZoned(int zones, BuildUse usual)
        {
            if (LandRules.Zoned(zones, usual))
            {
                return usual;
            }
            for (int u = 0; u < (int)BuildUse.Count; u++)
            {
                if (LandRules.Zoned(zones, (BuildUse)u))
                {
                    return (BuildUse)u;
                }
            }
            return usual;
        }

        // The ground a design must fit: a plot's frame footprint (as the site's plot lays it), or an old building's own
        // footprint with the copy engine's slack round it.
        private void Ground(ProjectFile p, out float width, out float depth)
        {
            if (p.Kind == ProjectKind.Rebuild || (p.Kind == ProjectKind.Contract && p.CivicKind == 4))
            {
                if (FootprintOf(p.Street, p.Number, out width, out depth))
                {
                    return;
                }
                width = depth = 0f;
                return;
            }
            PlotFrame(p.Width, p.Depth, out width, out depth);
        }

        /// <summary>The frame's footprint a plot of a lot's size has (the site's own layout, narrow or with a yard beside it).</summary>
        public static void PlotFrame(int width, int depth, out float frameWidth, out float frameDepth)
        {
            LandRules.Frame(width, depth, out frameWidth, out frameDepth);
        }

        // A rebuild's design fits the building's own footprint and 1.5 m round it (the copy engine's slack over a building),
        // measured once a session for each building.
        private bool FootprintOf(string street, int number, out float width, out float depth)
        {
            width = depth = 0f;
            string place = ConstructionGame.Place(street, number);
            if (_footprints.TryGetValue(place, out Vector2 known))
            {
                width = known.x;
                depth = known.y;
                return true;
            }
            if (ConstructionBridges.Footprint == null || Down("Footprint"))
            {
                return false;
            }
            try
            {
                if (!ConstructionBridges.Footprint(street, number, out width, out depth))
                {
                    return false;
                }
                _footprints[place] = new Vector2(width, depth);
                return true;
            }
            catch (Exception e)
            {
                Faulty("Footprint", e);
                return false;
            }
        }

        private readonly Dictionary<string, Vector2> _footprints = new Dictionary<string, Vector2>(8);

        // The design the architect starts with: the first that fits (the best covering), or none yet.
        private void DefaultDesign(ProjectFile p)
        {
            if (DesignChoices(p, _choices) && _choices.Count > 0)
            {
                Wear(p, _choices[0]);
            }
        }

        /// <summary>The designs of the city's own that fit a project's ground for its use; false while they are being looked through.</summary>
        public bool DesignChoices(ProjectFile p, List<DesignChoice> into)
        {
            Ground(p, out float width, out float depth);
            if (width <= 0f || depth <= 0f)
            {
                into.Clear();
                return true;   // known: nothing fits
            }
            bool ready = ReadDesigns(width, depth, BuildCatalogue.UseTypes[(int)p.Use], DesignsAsked, into);
            if (p.Model == ProjectKind.Plot && !string.IsNullOrEmpty(p.Lot) && !IsTestLot(p.Lot))
            {
                KeepSizes(p.Lot, into);   // the district takes a building of its lot's class's sizes only
            }
            if (p.Kind == ProjectKind.Rebuild)
            {
                for (int i = into.Count - 1; i >= 0; i--)
                {
                    if (into[i].Size != p.Size)
                    {
                        into.RemoveAt(i);   // a rebuild keeps the game's building inside: a design of its own size
                    }
                }
            }
            if (into.Count > DesignsShown)
            {
                into.RemoveRange(DesignsShown, into.Count - DesignsShown);
            }
            return ready;
        }

        // Those of a size the lot takes, the best covering first, as many as are shown.
        private static void KeepSizes(string lot, List<DesignChoice> into)
        {
            for (int i = into.Count - 1; i >= 0; i--)
            {
                if (!SizeTaken(lot, into[i].Size))
                {
                    into.RemoveAt(i);
                }
            }
            if (into.Count > DesignsShown)
            {
                into.RemoveRange(DesignsShown, into.Count - DesignsShown);
            }
        }

        private void Wear(ProjectFile p, DesignChoice d)
        {
            p.Design = d.Name;
            p.Height = Mathf.RoundToInt(d.Height);
            p.Storeys = PlansRules.Storeys(d.Height);
            if (p.Kind != ProjectKind.Rebuild)
            {
                p.Size = d.Size;
                p.Version = d.Version;
                p.Area = Math.Max(1, d.TotalSqm);
            }
            p.PlansDone = false;
            p.PlansDay = 0;
        }

        // ------------------------------------------------------------------ the plans

        /// <summary>The use of a plot's building, among those its lot is zoned for. Returns a text key.</summary>
        public string SetUse(ProjectFile p, BuildUse use)
        {
            if (p == null || p.Stage != ProjectStage.Design || p.Kind != ProjectKind.Plot)
            {
                return "reallife_bld_answer_not_design";
            }
            LotFile lot = _state.Lot(p.Lot);
            if (lot != null && !LandRules.Zoned(lot.Zones, use))
            {
                return "reallife_bld_answer_not_zoned";
            }
            p.Use = use;
            p.Design = string.Empty;
            DefaultDesign(p);
            Changed();
            return null;
        }

        /// <summary>A design among those that fit (by its place in the list). Returns a text key, or null.</summary>
        public string SetDesign(ProjectFile p, int index)
        {
            if (p == null || p.Stage != ProjectStage.Design)
            {
                return "reallife_bld_answer_not_design";
            }
            if (!DesignChoices(p, _choices))
            {
                return "reallife_bld_answer_designs_wait";
            }
            if (index < 0 || index >= _choices.Count)
            {
                return "reallife_bld_answer_no_design";
            }
            Wear(p, _choices[index]);
            Changed();
            return null;
        }

        /// <summary>Who draws the plans: a practice, an architect of the staff, or the player. Returns a text key, or null.</summary>
        public string SetArchitect(ProjectFile p, ArchitectKind kind, int firm, string employee)
        {
            if (p == null || p.Stage != ProjectStage.Design || p.PlansDay > 0)
            {
                return "reallife_bld_answer_not_design";
            }
            if (kind == ArchitectKind.Player && !Qualified("architect"))
            {
                return "reallife_bld_answer_not_qualified";
            }
            p.Architect = kind;
            p.Firm = BuildCatalogue.Clamp(firm, 0, BuildCatalogue.FirmCount - 1);
            p.ArchitectId = kind == ArchitectKind.Staff ? employee ?? string.Empty : string.Empty;
            Changed();
            return null;
        }

        public void SetParking(ProjectFile p, bool under)
        {
            if (p != null && p.Stage == ProjectStage.Design)
            {
                p.ParkingUnder = under;
                p.PlansDone = false;
                Changed();
            }
        }

        /// <summary>What a project's plans come to: the estimate as the plans would show it, the requirements and the limit.</summary>
        public CostSheet Preview(ProjectFile p, out int exitsNeeded, out int parkingNeeded, out int limit, out bool engineer)
        {
            exitsNeeded = PlansRules.ExitsNeeded(p.Storeys, p.Area);
            parkingNeeded = PlansRules.ParkingNeeded(p.Use, p.Area);
            limit = PlansRules.HeightLimit(p.District, !string.IsNullOrEmpty(p.Lot));
            engineer = PlansRules.NeedsEngineer(p.Storeys, p.Kind);
            Exchange(_exchange);
            return PlansRules.Costed(PlansRules.Estimate(p.Model, p.Use, p.Area, p.Storeys, p.ParkingUnder, exitsNeeded, ConstructionGame.Prices, _exchange), p.CostPermille);
        }

        /// <summary>The architect's terms for a project's plans as chosen now.</summary>
        public PlansQuote QuoteOf(ProjectFile p)
        {
            CostSheet sheet = Preview(p, out _, out _, out _, out _);
            int skill = 0;
            if (p.Architect == ArchitectKind.Staff && ConstructionGame.Employee(p.ArchitectId, out StaffView v))
            {
                skill = v.Value;
            }
            return PlansRules.Quote(p.Architect, p.Firm, skill, sheet.Total, p.Area);
        }

        /// <summary>The architect is asked to draw the plans: a practice's fee now, the plans in their days. Returns a text key.</summary>
        public string DrawPlans(ProjectFile p)
        {
            if (p == null || p.Stage != ProjectStage.Design)
            {
                return "reallife_bld_answer_not_design";
            }
            if (string.IsNullOrEmpty(p.Design) || p.Area <= 0)
            {
                return "reallife_bld_answer_no_design";
            }
            if (p.PlansDay > 0)
            {
                return "reallife_bld_answer_plans_coming";
            }
            if (p.Architect == ArchitectKind.Staff && !ConstructionGame.Employee(p.ArchitectId, out _))
            {
                return "reallife_bld_answer_no_architect";
            }
            PlansQuote quote = QuoteOf(p);
            bool redraw = p.Applications > 0;
            long fee = redraw ? (long)quote.Fee * BuildCatalogue.RedrawFeePermille / 1000L : quote.Fee;
            int days = redraw ? Math.Max(1, quote.Days * BuildCatalogue.RedrawDaysPermille / 1000) : quote.Days;
            if (!ConstructionGame.Take(fee, "reallife_transaction_bld_fees", ProjectName(p), true))
            {
                return "reallife_bld_answer_no_money";
            }
            Spend(p, fee);
            if (p.Architect == ArchitectKind.Player)
            {
                CostSheet sheet = Preview(p, out _, out _, out _, out _);
                if (!Commission(CommissionId(p), p.Storeys, FlatsOf(p), sheet.Total))
                {
                    return "reallife_bld_answer_not_qualified";
                }
                p.PlansDay = int.MaxValue;   // the player's own: done when the architects' office has their design
            }
            else
            {
                p.PlansDay = ConstructionGame.Day + Math.Max(1, days);
            }
            p.Quality = quote.Quality;
            Logger?.Info("Construction: project " + p.Id + "'s plans asked of " + p.Architect + (p.Architect == ArchitectKind.Firm ? " " + p.Firm : string.Empty) + " for " + fee + ", ready on day "
                         + (p.PlansDay == int.MaxValue ? "the player designs" : p.PlansDay.ToString()) + ".");
            Changed();
            return "reallife_bld_answer_plans_asked";
        }

        /// <summary>The brief's id at the architects' practice for a project the player designs: "build:" and its number.</summary>
        public static string CommissionId(ProjectFile p) => "build:" + p.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>The flats a block of homes holds: the player's own design's, or one for every flat's floor area.</summary>
        public static int FlatsOf(ProjectFile p) => p.Units > 0 ? p.Units : LandRules.Flats(p.Area);

        // The player's design is in from the practice: its score is the plans' quality, its cost against the budget the build's,
        // and its flats the block's. The building's look stays the design of the city's own the project wears.
        private void OwnDesign(ProjectFile p, int total, int costPermille, int storeys, int units)
        {
            p.CostPermille = BuildCatalogue.Clamp(costPermille > 0 ? costPermille : 1000, BuildCatalogue.DesignCostLeast, BuildCatalogue.DesignCostMost);
            p.Units = p.Use == BuildUse.Homes && units > 0 ? BuildCatalogue.Clamp(units, BuildCatalogue.FlatsLeast, Math.Max(BuildCatalogue.FlatsLeast, p.Area / 30)) : 0;
            int quality = BuildCatalogue.PlayerQualityBase + BuildCatalogue.Clamp(total, 0, 100) * BuildCatalogue.PlayerQualityPermille / 1000;
            Logger?.Info("Construction check (own design): project " + p.Id + "'s design from the practice: score " + total + ", cost " + costPermille + " in 1000 of its budget, "
                         + storeys + " storey(s) (the project's " + p.Storeys + "), " + units + " flat(s); the plans' quality " + quality + ": PASS.");
            PlansDrawn(p, quality);
        }

        // The plans drawn: the requirements written into them, the estimate with its error, the engineer's sign off needed or not.
        private void PlansDrawn(ProjectFile p, int quality)
        {
            p.PlansDone = true;
            p.PlansDay = ConstructionGame.Day;
            p.Quality = BuildCatalogue.Clamp(quality, 0, 100);
            CostSheet sheet = Preview(p, out int exits, out int parking, out _, out bool engineer);
            p.Exits = exits;   // the architect draws the exits the building needs; their cost is in the estimate
            p.ExitsNeeded = exits;
            p.ParkingNeeded = parking;
            int skill = 0;
            bool estimator = EstimatorSkill(out skill);
            int spread = PlansRules.Spread(estimator, skill, p.Architect == ArchitectKind.Firm);
            p.Error = PlansRules.Error(_state.Seed, p.Id, spread);
            p.Estimate = PlansRules.Shown(sheet.Total, p.Error);
            if (!engineer)
            {
                p.Engineered = true;
            }
            Logger?.Info("Construction: project " + p.Id + "'s plans drawn: quality " + p.Quality + ", estimate " + p.Estimate + " (the cost "
                + sheet.Total + ", out by " + p.Error + " in 1000), exits "
                         + exits + ", parking " + parking + (p.ParkingUnder ? " under it" : " not provided") + (engineer ? ", an engineer's sign off needed." : "."));
            Tell(NotificationType.Success, "reallife_bld_notice_plans_drawn", ProjectName(p), null, TabProjects, p.Id);
        }

        private bool EstimatorSkill(out int skill)
        {
            skill = 0;
            bool any = false;
            for (int i = 0; i < _state.Staff.Count; i++)
            {
                if (_state.Staff[i].Role == StaffRole.Estimator && ConstructionGame.Employee(_state.Staff[i].Employee, out StaffView v))
                {
                    skill = Math.Max(skill, v.Value);
                    any = true;
                }
            }
            return any;
        }

        /// <summary>The engineer's sign off of a project's plans: an engineer of the staff, or an outside one for a fee. Returns a text key.</summary>
        public string AskEngineer(ProjectFile p, bool staff)
        {
            if (p == null || !p.PlansDone || p.Engineered || p.EngineerDay > 0)
            {
                return "reallife_bld_answer_no_engineer_needed";
            }
            if (staff)
            {
                if (!StaffInRole(StaffRole.Engineer) && !Qualified("engineer"))
                {
                    return "reallife_bld_answer_no_engineer";
                }
                p.EngineerDay = ConstructionGame.Day + BuildCatalogue.EngineerStaffDays;
                p.EngineerStaff = true;
            }
            else
            {
                long fee = Math.Max(0L, p.Estimate) * BuildCatalogue.EngineerFirmFeePermille / 1000L;
                if (!ConstructionGame.Take(fee, "reallife_transaction_bld_fees", ProjectName(p), true))
                {
                    return "reallife_bld_answer_no_money";
                }
                Spend(p, fee);
                p.EngineerDay = ConstructionGame.Day + BuildCatalogue.EngineerFirmDays;
            }
            Changed();
            return "reallife_bld_answer_engineer_asked";
        }

        private bool StaffInRole(StaffRole role)
        {
            for (int i = 0; i < _state.Staff.Count; i++)
            {
                if (_state.Staff[i].Role == role && ConstructionGame.Employee(_state.Staff[i].Employee, out _))
                {
                    return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------ planning

        /// <summary>The fee City Hall asks for a project's application now.</summary>
        public long PlanningFee(ProjectFile p)
        {
            return PlansRules.PlanningFee(Math.Max(0L, p.Estimate), ConstructionGame.Prices);
        }

        /// <summary>A planning application made at City Hall: its fee, a decision in its working days. Returns a text key.</summary>
        public string Apply(ProjectFile p)
        {
            if (p == null || !p.PlansDone || (p.Stage != ProjectStage.Design && p.Planning != PlanningState.Refused && p.Planning != PlanningState.AppealLost))
            {
                return "reallife_bld_answer_no_plans";
            }
            if (p.Planning == PlanningState.Submitted || p.Planning == PlanningState.Appealed)
            {
                return "reallife_bld_answer_planning_waits";
            }
            long fee = PlanningFee(p);
            if (!ConstructionGame.Take(fee, "reallife_transaction_bld_fees", ProjectName(p), true))
            {
                return "reallife_bld_answer_no_money";
            }
            Spend(p, fee);
            int day = ConstructionGame.Day;
            p.Stage = ProjectStage.Planning;
            p.Planning = PlanningState.Submitted;
            p.Applications++;
            p.PlanningDay = day;
            p.DecideDay = PlanningRules.AfterWorkingDays(day, PlanningRules.WorkingDays(p.Area));
            p.Consulted = false;
            p.Objections = 0;
            p.Reasons = 0;
            Logger?.Info("Construction: project " + p.Id + " applied for planning permission (fee " + fee + "), the decision on day " + p.DecideDay + ".");
            Changed();
            return "reallife_bld_answer_applied";
        }

        // The neighbours have had their say: the objections counted, a story in the next morning's paper if there are any.
        private void Consult(ProjectFile p)
        {
            p.Consulted = true;
            int norm = Mathf.Max(2, p.Kind == ProjectKind.Rebuild ? p.Storeys : 4);
            bool zoned = p.Kind != ProjectKind.Plot || (_state.Lot(p.Lot) is LotFile l && LandRules.Zoned(l.Zones, p.Use));
            int parkingShort = p.ParkingUnder ? 0 : p.ParkingNeeded;
            p.Objections = PlanningRules.Objections(_state.Seed, p.Id * 13 + p.Applications, p.Storeys, norm, zoned, parkingShort, _state.Firm.Reputation, p.Quality);
            if (p.Objections > 0)
            {
                Paper("reallife_bld_paper_objections", ProjectName(p), p.Objections);
                Tell(NotificationType.Info, "reallife_bld_notice_objections", ProjectName(p), null, TabProjects, p.Id, p.Objections);
            }
            Logger?.Info("Construction: project " + p.Id + "'s neighbours had their say: " + p.Objections + " objection(s).");
        }

        // City Hall's decision on its day.
        private void Decide(ProjectFile p)
        {
            if (!p.Consulted)
            {
                Consult(p);
            }
            int limit = PlansRules.HeightLimit(p.District, !string.IsNullOrEmpty(p.Lot));
            bool engineerNeeded = PlansRules.NeedsEngineer(p.Storeys, p.Kind);
            bool zoned = p.Kind != ProjectKind.Plot || (_state.Lot(p.Lot) is LotFile l && LandRules.Zoned(l.Zones, p.Use));
            PlansRules.Unmet(p.Exits, p.ExitsNeeded, p.ParkingUnder, p.ParkingNeeded, p.Storeys, limit, engineerNeeded, p.Engineered, out int reasons);
            bool granted = PlanningRules.Decide(_state.Seed, p.Id, p.Applications, zoned, reasons, p.Quality, _state.Firm.Reputation, p.Objections, Permits(), out Refusal why);
            if (Verbose)
            {
                Logger?.Info("Construction check (planning): project " + p.Id + ", application " + p.Applications + ": zoned " + zoned + ", requirements not met " + reasons + ", quality " + p.Quality
                             + ", reputation " + _state.Firm.Reputation + ", objections " + p.Objections + ", permits " + Permits() + ": " + (granted
                                 ? "granted" : "refused (" + why + ")") + ": PASS.");
            }
            if (granted)
            {
                p.Planning = PlanningState.Granted;
                p.Stage = ProjectStage.Ready;
                p.Conditions = PlanningRules.Conditions(p.Objections);
                if (p.Conditions)
                {
                    long contribution = PlanningRules.Contribution(p.Estimate);
                    ConstructionGame.Force(contribution, "reallife_transaction_bld_fees", ProjectName(p), true);
                    Spend(p, contribution);
                }
                Tell(NotificationType.Success, p.Conditions ? "reallife_bld_notice_granted_conditions" : "reallife_bld_notice_granted", ProjectName(p), null, TabProjects, p.Id);
                return;
            }
            p.Planning = PlanningState.Refused;
            p.Reasons = (int)why;
            Tell(NotificationType.Warning, "reallife_bld_notice_refused", ProjectName(p), null, TabProjects, p.Id);
        }

        /// <summary>An appeal to the court against a refusal: its fee now, the hearing in some days. Returns a text key.</summary>
        public string Appeal(ProjectFile p)
        {
            if (p == null || p.Planning != PlanningState.Refused)
            {
                return "reallife_bld_answer_no_refusal";
            }
            long fee = (long)BuildCatalogue.AppealFee * ConstructionGame.Prices / 1000L;
            if (!ConstructionGame.Take(fee, "reallife_transaction_bld_fees", ProjectName(p), true))
            {
                return "reallife_bld_answer_no_money";
            }
            Spend(p, fee);
            p.Planning = PlanningState.Appealed;
            p.AppealDay = PlanningRules.HearingDay(_state.Seed, p.Id * 7 + p.Applications, ConstructionGame.Day);
            Logger?.Info("Construction: project " + p.Id + " appealed against its refusal (fee " + fee + "), heard on day " + p.AppealDay + ".");
            Changed();
            return "reallife_bld_answer_appealed";
        }

        private void Hear(ProjectFile p)
        {
            int odds = PlanningRules.AppealOdds(PlanningRules.MetShare(p.Reasons), p.Quality, Lawyer(), p.Objections, (Refusal)p.Reasons);
            bool allowed = PlanningRules.Allowed(_state.Seed, p.Id * 7 + p.Applications, odds);
            if (Verbose)
            {
                Logger?.Info("Construction check (appeal): project " + p.Id + ": refused for " + (Refusal)p.Reasons + ", quality " + p.Quality
                    + ", objections " + p.Objections + ", a lawyer " + Lawyer()
                             + "; odds " + odds + " in 1000: " + (allowed ? "allowed" : "dismissed") + ": PASS.");
            }
            if (allowed)
            {
                p.Planning = PlanningState.Granted;
                p.Stage = ProjectStage.Ready;
                Paper("reallife_bld_paper_appeal_won", ProjectName(p), 0);
                Tell(NotificationType.Success, "reallife_bld_notice_appeal_won", ProjectName(p), null, TabProjects, p.Id);
            }
            else
            {
                p.Planning = PlanningState.AppealLost;
                Tell(NotificationType.Warning, "reallife_bld_notice_appeal_lost", ProjectName(p), null, TabProjects, p.Id);
            }
        }

        /// <summary>The plans drawn again after a refusal: back at the plans, for a share of the first fee. Returns a text key.</summary>
        public string Redraw(ProjectFile p)
        {
            if (p == null || (p.Planning != PlanningState.Refused && p.Planning != PlanningState.AppealLost))
            {
                return "reallife_bld_answer_no_refusal";
            }
            p.Stage = ProjectStage.Design;
            p.Planning = PlanningState.None;
            p.PlansDone = false;
            p.PlansDay = 0;
            p.Engineered = false;
            p.EngineerDay = 0;
            Changed();
            return "reallife_bld_answer_redraw";
        }

        // ------------------------------------------------------------------ the site

        /// <summary>What stands in the way of starting a project's site now, as a text key; null when it can start.</summary>
        public string StartBlock(ProjectFile p)
        {
            if (p == null || p.Stage != ProjectStage.Ready)
            {
                return p != null && p.Stage == ProjectStage.Building ? "reallife_bld_answer_building" : "reallife_bld_answer_no_permission";
            }
            if (!Licensed)
            {
                return "reallife_bld_answer_no_licence";
            }
            if (_state.CrewsOn(p.Id) == 0)
            {
                return "reallife_bld_answer_no_crew";
            }
            if (_state.Building() >= BuildCatalogue.BuildingMost)
            {
                return "reallife_bld_answer_too_many_sites";
            }
            if (SiteCount() >= BuildCatalogue.SitesMost)
            {
                return "reallife_bld_answer_city_full";
            }
            if (p.Model == ProjectKind.Plot && !Lot(p.Lot, out _))
            {
                return "reallife_bld_answer_no_lot";
            }
            return null;
        }

        /// <summary>The site goes up in the city's programme. Returns a text key.</summary>
        public string StartBuilding(ProjectFile p)
        {
            string block = StartBlock(p);
            if (block != null)
            {
                return block;
            }
            p.Hours = PlansRules.Hours(p.Use, p.Area, p.Storeys, p.Kind);
            string why;
            int site;
            if (p.Model == ProjectKind.Plot)
            {
                Lot(p.Lot, out LotView lot);
                int storeys = LandRules.FrameStoreys(lot.Width, p.Storeys);   // the frame the site shows
                site = StartPlotSite(new Vector3(lot.X, lot.Y, lot.Z), lot.Yaw, lot.Width, lot.Depth, storeys, 0, p.Hours, out why);
            }
            else
            {
                site = StartWorksSite(p.Street, p.Number, 4, true, 0, p.Hours, out why);   // a rebuild: the game's scaffolding to the roof
            }
            if (site <= 0)
            {
                Logger?.Info("Construction: project " + p.Id + "'s site could not be started: " + why + ".");
                return "reallife_bld_answer_site_refused";
            }
            int day = ConstructionGame.Day;
            p.SiteId = site;
            p.Stage = ProjectStage.Building;
            p.StartDay = day;
            p.DueDay = PlanningRules.AfterWorkingDays(day, TenderRules.StandardDays(p.Hours) + 2);   // the usual pace and a day for each inspection's wait
            p.StageSeen = -1;
            LoanDraw(p, 0);
            Logger?.Info("Construction: project " + p.Id + " started as site " + site + ": " + p.Hours + " working hours, due on day " + p.DueDay + ".");
            Changed();
            return "reallife_bld_answer_started";
        }

        /// <summary>A project given up: its site taken down, its crews and plant freed, its land kept. Returns a text key.</summary>
        public string Abandon(ProjectFile p)
        {
            if (p == null || !p.Live)
            {
                return "reallife_bld_answer_not_live";
            }
            if (p.SiteId > 0)
            {
                DropSite(p.SiteId);
            }
            FreeProject(p);
            p.Stage = ProjectStage.Abandoned;
            LotFile lot = _state.Lot(p.Lot);
            if (lot != null)
            {
                lot.EmptySince = ConstructionGame.Day;
            }
            Logger?.Info("Construction: project " + p.Id + " given up.");
            Changed();
            return "reallife_bld_answer_abandoned";
        }

        private void FreeProject(ProjectFile p)
        {
            for (int i = 0; i < _state.Crews.Count; i++)
            {
                if (_state.Crews[i].Project == p.Id)
                {
                    _state.Crews[i].Project = 0;
                }
            }
            for (int k = 0; k < (int)Plant.Count; k++)
            {
                if (p.Plant[k] >= 0)
                {
                    LetPlantGo(p, (Plant)k);
                }
            }
            for (int i = 0; i < _state.Staff.Count; i++)
            {
                if (_state.Staff[i].Project == p.Id)
                {
                    _state.Staff[i].Project = 0;
                }
            }
            p.SiteId = 0;
        }

        // ------------------------------------------------------------------ the handover

        private void Handover(ProjectFile p, int day)
        {
            FreeProject(p);
            p.Stage = ProjectStage.Finished;
            p.Handed = day;
            p.Opening = day + 1;
            FirmFile firm = _state.Firm;
            firm.Finished++;
            int quality = SiteRules.WorkQuality(CrewSkillLast(p), 50, p.Quality);
            int points = FirmRules.HandoverPoints(p.StartDay, p.DueDay, day, quality);
            firm.Reputation = FirmRules.Moved(firm.Reputation, points);
            if (day <= p.DueDay)
            {
                firm.OnTime++;
            }
            else
            {
                firm.Late++;
            }
            LoanDraw(p, 3);
            p.LoanFrom = day;
            if (p.InvestorOwed > 0)
            {
                p.InvestorDue = day + BuildCatalogue.InvestorPayDays;
            }
            if (p.Kind == ProjectKind.Contract)
            {
                ContractPaid(p, true);
            }
            DefectAhead(p, quality, day);
            if (p.Model == ProjectKind.Plot && !string.IsNullOrEmpty(p.Lot))
            {
                p.Value = ValueOf(p, out _);
                if (p.Use == BuildUse.Homes && (p.After == AfterUse.SellFlats || p.FlatsOffPlan > 0))
                {
                    p.After = AfterUse.SellFlats;
                    p.Flats = FlatsOf(p);
                    if (p.OffPlanBalance > 0)
                    {
                        long toPlayer = MoneyRules.Share(p.OffPlanBalance, p.LoanOwed, p.InvestorOwed, out long toLoan, out long toInvestor);
                        SalePaid(p, toLoan, toInvestor);
                        ConstructionGame.Give(toPlayer, "reallife_transaction_bld_sales", ProjectName(p));
                        firm.Earned += p.OffPlanBalance;
                        p.OffPlanBalance = 0;
                    }
                }
                HandToDistrict(p);
            }
            Paper("reallife_bld_paper_finished", ProjectName(p), 0);
            Logger?.Info("Construction: project " + p.Id + " handed over on day " + day + " (due " + p.DueDay + "), quality " + quality + ", reputation " + (points >= 0 ? "+" : string.Empty) + points
                         + "; spent " + p.Spent + ".");
            Tell(NotificationType.Success, p.Kind == ProjectKind.Plot ? "reallife_bld_notice_handed_plot" : "reallife_bld_notice_handed", ProjectName(p), null, TabProjects, p.Id);
            if (p.Kind == ProjectKind.Contract && p.ClientValue > 0)
            {
                _state.Firm.Reputation = FirmRules.Moved(_state.Firm.Reputation, day <= p.DueDay ? BuildCatalogue.RepContractOnTime : 0);
            }
        }

        private int CrewSkillLast(ProjectFile p)
        {
            return p.PaceDays > 0 ? BuildCatalogue.Clamp(40 + p.PaceSum / Math.Max(1, p.PaceDays) / 40, 30, 90) : 50;
        }

        // A plot's building becomes one of the district's from the next load: the player's, or its residents' when flats are sold.
        private void HandToDistrict(ProjectFile p)
        {
            if (p.InDistrict || string.IsNullOrEmpty(p.Lot))
            {
                return;
            }
            if (IsTestLot(p.Lot))
            {
                p.InDistrict = true;   // a test's lot is no lot of the district: its building stays the site's look
                Logger?.Info("Construction: project " + p.Id + " stands on a test's lot; the district is not told of it.");
                return;
            }
            bool player = p.Kind == ProjectKind.Plot && p.After == AfterUse.Keep && p.FlatsOffPlan == 0;   // a public building is the city's
            LotFile lot = _state.Lot(p.Lot);
            double paid = (lot != null ? lot.Paid : 0L) + p.Spent;
            if (FinishLot(p.Lot, p.Design, p.Size, p.Version, BuildCatalogue.UseTypes[(int)p.Use], 0, player, player ? paid : 0.0, out string why))
            {
                p.InDistrict = true;
                if (!player && lot != null)
                {
                    _state.Lots.Remove(lot);   // the residents' now: no more land tax of the player's
                }
                Logger?.Info("Construction: project " + p.Id + "'s building is the district's from the next load, " + (player ? "the player's" : "its residents'") + ".");
                return;
            }
            Logger?.Info("Construction: project " + p.Id + "'s building is not the district's yet (" + why + "); asked again each day.");
        }

        /// <summary>Whether a finished plot's building is the player's (used, let or sold through BizMan from the next load) or its flats are sold. Before the handover only.</summary>
        public string SetAfter(ProjectFile p, AfterUse after)
        {
            if (p == null || p.Kind != ProjectKind.Plot || p.InDistrict)
            {
                return "reallife_bld_answer_too_late";
            }
            if (after == AfterUse.SellFlats && p.Use != BuildUse.Homes)
            {
                return "reallife_bld_answer_homes_only";
            }
            if (after == AfterUse.Keep && p.FlatsOffPlan > 0)
            {
                return "reallife_bld_answer_flats_sold";
            }
            p.After = after;
            Changed();
            return null;
        }

        public void SetAsk(ProjectFile p, int ask)
        {
            if (p != null)
            {
                p.Ask = BuildCatalogue.Clamp(ask, 0, BuildCatalogue.AskPermille.Length - 1);
                Changed();
            }
        }
    }
}
