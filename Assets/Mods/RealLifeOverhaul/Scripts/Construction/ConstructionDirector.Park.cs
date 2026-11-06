using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Construction
{
    /// <summary>What a car park on some squares would be: whether one can be built, why not, and its figures.</summary>
    public struct ParkFacts
    {
        public bool Ok;              // a car park can be built on the squares now
        public string Why;           // a text key for why not; null when it can
        public int Width, Depth;     // whole metres of ground
        public int Area;
        public int Bays, Accessible;
        public int Code;             // how it is laid out (CarParkRules.Codes), the way in clear of the street's furniture
        public int Hours, Days;      // the works' working hours; the days from the plans to the keys
        public long Works;           // the estimate of the works and City Hall's fee
        public long PlansFee;        // the practice's fee for the plans
        public long Cost => Works + PlansFee;
        public int Zones;            // the uses the land is zoned for
        public bool Homes;           // the car park is the residents' own: the land is zoned for homes alone
        public int Fair;             // the district's street rate an hour in cents
        public string Blocker;       // what stands where the way in would be (for the log), when it is the reason
    }

    /// <summary>
    /// Car parks, the player's part (docs/features/construction.md, "Parking lots"; the owner, 27 Oct 2026: "Give the ability to build
    /// parking lots and let it for use. In build mode they must be able to build parking lots. It must finish fast and it can be let
    /// out for rivals or the public."): a car park placed on the player's own squares in build mode is a project of the normal
    /// pipeline made fast (the practice draws its plans in a day, City Hall decides in a working day and grants it where the land is
    /// zoned for one, the works are one to three working days by its area, by the company's crews or a hired builder), and at the
    /// handover it opens: its bays are the game's own parking lanes, public pay parking at the district's street rate to begin with.
    /// COST: nothing per frame; a press, the day's pass over the player's car park projects.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        /// <summary>The design name a car park's project carries: a marker, there is no building to wear.</summary>
        public const string ParkDesign = "carpark";

        private readonly List<CarParkLayout> _parkLayouts = new List<CarParkLayout>(4);
        private readonly Dictionary<string, CarParkFile> _parkLots = new Dictionary<string, CarParkFile>(32, StringComparer.Ordinal);   // every space a car park takes
        private readonly List<LotView> _parkSpaces = new List<LotView>(8);
        private int _parkLotsStamp = int.MinValue, _parkChanged;

        /// <summary>True while car parks run: the construction game and the option on, Uptown standing.</summary>
        public bool ParkingOn => Running && Settings.ConstructionParking;

        // ------------------------------------------------------------------ the spaces taken

        /// <summary>The car park (under way or standing) that takes a lot, or null.</summary>
        public CarParkFile ParkOf(string lot)
        {
            if (string.IsNullOrEmpty(lot) || _state == null || _state.CarParks.Count == 0)
            {
                return null;
            }
            int lots = Lots.Count;   // the district's lots read first: their stamp moves when they change
            int stamp = _lotsStamp * 31 + _parkChanged;
            if (stamp != _parkLotsStamp)
            {
                _parkLotsStamp = stamp;
                _parkLots.Clear();
                for (int i = 0; i < _state.CarParks.Count && lots > 0; i++)
                {
                    CarParkFile c = _state.CarParks[i];
                    if (c.Stage == CarParkStage.Closed || !SiteSpaces(c.Lot, c.Spaces, _parkSpaces))
                    {
                        continue;
                    }
                    for (int k = 0; k < _parkSpaces.Count; k++)
                    {
                        _parkLots[_parkSpaces[k].Id] = c;
                    }
                }
                _parkSpaces.Clear();
            }
            return _parkLots.TryGetValue(lot, out CarParkFile found) ? found : null;
        }

        // A car park of the city's under way takes the lot: nobody else may buy or build there.
        private bool ParkUnderWayOn(string lot)
        {
            CarParkFile c = ParkOf(lot);
            return c != null && c.Owner == CarParkOwner.Builder;
        }

        private void ParksChanged()
        {
            _parkChanged++;
            _cellsFactsStamp++;
        }

        /// <summary>A car park's ground as one lot: its spaces joined and cut to the rows held; false when one of its spaces is not in the district at this load.</summary>
        public bool ParkGround(CarParkFile c, out LotView site)
        {
            site = default(LotView);
            if (!SiteSpaces(c.Lot, c.Spaces, _parkSpaces))
            {
                return false;
            }
            site = Joined(_parkSpaces);
            if (c.Owner == CarParkOwner.Player && !string.IsNullOrEmpty(c.Rows))
            {
                int stripRows = CellRules.RowsFor(site.Depth);
                site = CellRules.Trim(site, CellRules.MinRows(c.Rows, c.Spaces, stripRows), stripRows);
            }
            _parkSpaces.Clear();
            return true;
        }

        /// <summary>The district's street rate an hour in cents for a lot: what the game charges a car left in its streets there.</summary>
        public int ParkFair(string lot)
        {
            return CarParkRules.Fair(ConstructionGame.ParkingRate(DistrictKeyOf(DistrictOfLot(lot))));
        }

        // ------------------------------------------------------------------ what a car park on some squares would be

        // The squares' holder: the one site of the player's every square belongs to; false when any is not theirs, or they are two sites.
        private bool ParkHolder(int firstColumn, int count, out LotFile site, out int minRows)
        {
            site = null;
            minRows = int.MaxValue;
            CellGrid grid = Cells;
            int at = firstColumn;
            for (int i = 0; i < count; i++, at = at >= 0 ? grid.Next(at) : -1)
            {
                LotFile holder = null;
                int had = at >= 0 ? RowsHeld(at, out holder) : 0;
                if (holder == null || (site != null && holder != site))
                {
                    return false;
                }
                site = holder;
                minRows = Math.Min(minRows, had);
            }
            minRows = minRows == int.MaxValue ? 0 : minRows;
            return site != null;
        }

        /// <summary>
        /// What a car park on a footprint of the player's squares would be: the site's ground (the squares' columns joined, cut to the
        /// rows picked), the plan of bays it holds laid out the way whose way in is clear of the street's furniture, the zoning
        /// (shops, offices or warehouses; homes alone only for the residents' own), the cost and the days. Not Ok, with why, when it
        /// cannot be built there now. COST: a few physics queries on the way in's ground.
        /// </summary>
        public ParkFacts ParkAssess(int firstColumn, int count, int rows)
        {
            var f = new ParkFacts();
            if (GoingPublic.InSession)
            {
                f.Why = "reallife_bld_answer_session_land";
                return f;
            }
            if (!ParkingOn)
            {
                f.Why = "reallife_park_answer_off";
                return f;
            }
            if (!FootprintSite(firstColumn, count, rows, out LotView site))
            {
                f.Why = "reallife_bld_answer_no_lot";
                return f;
            }
            if (!ParkHolder(firstColumn, count, out LotFile holder, out int minRows))
            {
                f.Why = "reallife_bld_hint_place_not_yours";
                return f;
            }
            f.Width = site.Width;
            f.Depth = site.Depth;
            f.Area = site.Width * site.Depth;
            f.Fair = ParkFair(holder.Id);
            if (_state.OnLot(holder.Id) != null || ParkOf(holder.Id) != null || GrowOf(holder.Id) != null || LotTaken(site.Id))
            {
                f.Why = ParkOf(holder.Id) != null ? "reallife_park_answer_stands" : "reallife_bld_answer_lot_busy";
                return f;
            }
            if (count < CarParkRules.ColumnsLeast || Math.Max(1, minRows) < CarParkRules.RowsLeast)
            {
                f.Why = "reallife_park_answer_small";
                return f;
            }
            CarParkRules.Candidates(site.Width, site.Depth, _parkLayouts);
            if (_parkLayouts.Count == 0 || _parkLayouts[0].Bays < CarParkRules.BaysLeast)
            {
                f.Why = "reallife_park_answer_small";
                return f;
            }
            f.Zones = ZonesNow(holder);
            f.Homes = (f.Zones & ~BuildCatalogue.Zone(BuildUse.Homes)) == 0;
            if (!CarParkRules.Allowed(f.Zones, _parkLayouts[0].Bays, out string zoning))
            {
                f.Why = zoning;
                return f;
            }
            if (!CarParkGame.PickMouth(site, _parkLayouts, out CarParkLayout plan, out string blocker))
            {
                f.Why = "reallife_park_answer_mouth";
                f.Blocker = blocker;
                return f;
            }
            f.Bays = plan.Bays;
            f.Accessible = plan.Accessible;
            f.Code = plan.Code;
            f.Hours = CarParkRules.Hours(f.Area);
            f.Days = CarParkRules.PlansDays + CarParkRules.PermitDays + CarParkRules.WorkingDays(f.Hours);
            CostSheet sheet = ParkSheet(f.Area);
            f.Works = sheet.Total;
            f.PlansFee = CarParkRules.Quote(1, sheet.Total).Fee;
            f.Ok = true;
            return f;
        }

        // The estimate of a car park's works: the programme's own for its kind of site at those hours, City Hall's fee on it.
        private CostSheet ParkSheet(int area)
        {
            Exchange(_exchange);
            CostSheet sheet = PlansRules.EstimateWorks(CarParkRules.WorksKind, CarParkRules.Hours(area), ConstructionGame.Prices, _exchange);
            sheet.Fees += PlansRules.PlanningFee(sheet.Materials + sheet.Labour + sheet.Plant, ConstructionGame.Prices, Permits());
            return sheet;
        }

        // ------------------------------------------------------------------ a project made

        /// <summary>
        /// A car park placed on a footprint of the player's squares: the footprint made its own site by the Land tab's split and join
        /// where it is not the whole site, then the project made and its plans asked of a practice (a day). Returns the project's id,
        /// or 0 with why (a text key).
        /// </summary>
        public int PlacePark(int firstColumn, int count, int rows, out string why)
        {
            why = null;
            ParkFacts f = ParkAssess(firstColumn, count, rows);
            if (!f.Ok)
            {
                why = f.Why;
                return 0;
            }
            ParkHolder(firstColumn, count, out LotFile site, out _);
            if (!LotOfColumn(firstColumn, out LotView firstLot))
            {
                why = "reallife_bld_answer_no_lot";
                return 0;
            }
            string siteId = site.Id;
            if (site.Spaces != count || siteId != firstLot.Id)
            {
                string split = Split(siteId);   // the footprint made a site of its own: its columns joined back after the split
                if (split != "reallife_bld_answer_split")
                {
                    why = split;
                    return 0;
                }
                siteId = firstLot.Id;
                for (int i = 1; i < count; i++)
                {
                    string joined = Join(siteId);
                    if (joined != "reallife_bld_answer_joined")
                    {
                        why = joined;
                        return 0;
                    }
                }
            }
            int id = NewPark(siteId, f.Code, out why);
            ProjectFile p = _state.Project(id);
            if (p == null)
            {
                return 0;
            }
            SetArchitect(p, ArchitectKind.Firm, 1, null);
            string plans = DrawPlans(p);
            why = plans == "reallife_bld_answer_plans_asked" ? "reallife_park_answer_building" : plans;
            Logger?.Info("Construction: a car park placed on " + count + " space(s) from " + siteId + " (" + f.Width + " by " + f.Depth + " m, " + f.Bays + " bays, layout " + f.Code
                         + "): project " + id
                         + ", the plans: " + plans + ".");
            Changed();
            return id;
        }

        /// <summary>A car park's project on a site the player owns, laid out the way a code says. Returns the project's id, or 0 with why.</summary>
        public int NewPark(string lotId, int code, out string why)
        {
            why = null;
            LotFile owned = _state.Lot(lotId);
            if (owned == null || !Lot(lotId, out LotView lot))
            {
                why = "reallife_bld_answer_no_lot";
                return 0;
            }
            if (_state.OnLot(lotId) != null || ParkOf(lotId) != null)
            {
                why = ParkOf(lotId) != null ? "reallife_park_answer_stands" : "reallife_bld_answer_lot_busy";
                return 0;
            }
            if (!Room(out why))
            {
                return 0;
            }
            CarParkLayout plan = CarParkRules.LayoutOf(lot.Width, lot.Depth, code);
            if (!plan.Ok)
            {
                why = "reallife_park_answer_small";
                return 0;
            }
            int area = lot.Width * lot.Depth;
            var p = new ProjectFile
            {
                Id = _state.NewId(), Kind = ProjectKind.Parking, Stage = ProjectStage.Design, Created = ConstructionGame.Day, Lot = lotId, Use = CarParkRules.UseOf(ZonesNow(owned)),
                Width = lot.Width, Depth = lot.Depth, Area = area, Storeys = 1, Height = 3, District = lot.DistrictKey ?? string.Empty, Architect = ArchitectKind.Firm, Firm = 1,
                Design = ParkDesign, CivicKind = CarParkRules.WorksKind, Hours = CarParkRules.Hours(area), Park = code
            };
            _state.Projects.Add(p);
            owned.EmptySince = 0;
            ParksChanged();
            Logger?.Info("Construction: project " + p.Id + " made for a car park on lot " + lotId + " (" + lot.Width + " by " + lot.Depth + " m, " + plan.Bays + " bays).");
            Changed();
            return p.Id;
        }

        // ------------------------------------------------------------------ the pipeline, made fast

        // The day: each car park project of the player's carried to City Hall once its plans are drawn, the fee the player's as for any project.
        private void ParkProjectsDay(int day)
        {
            for (int i = 0; i < _state.Projects.Count; i++)
            {
                ProjectFile p = _state.Projects[i];
                if (p.Kind != ProjectKind.Parking || p.Stage != ProjectStage.Design || !p.PlansDone || p.Planning != PlanningState.None)
                {
                    continue;
                }
                string applied = Apply(p);
                if (applied == "reallife_bld_answer_applied")
                {
                    Tell(NotificationType.Info, "reallife_park_notice_applied", ProjectName(p), null, TabProjects, p.Id);
                }
                Logger?.Info("Construction: car park project " + p.Id + " put to City Hall on day " + day + ": " + applied + ".");
            }
        }

        // City Hall's decision on a car park: granted at once on its day when the land is zoned for one, with no neighbours' objections to weigh.
        private void DecidePark(ProjectFile p)
        {
            LotFile land = _state.Lot(p.Lot);
            int bays = CarParkRules.LayoutOf(p.Width, p.Depth, p.Park).Bays;
            bool zoned = land != null && CarParkRules.Allowed(ZonesNow(land), bays, out _);
            p.Objections = 0;
            p.Consulted = true;
            if (Verbose)
            {
                Logger?.Info("Construction check (planning): car park project " + p.Id + ": zoned " + zoned + ", " + bays + " bays: " + (zoned ? "granted" : "refused (not zoned)") + ": PASS.");
            }
            if (CarParkRules.Permit(zoned, true))
            {
                p.Planning = PlanningState.Granted;
                p.Stage = ProjectStage.Ready;
                Tell(NotificationType.Success, "reallife_park_notice_granted", ProjectName(p), null, TabProjects, p.Id);
                return;
            }
            p.Planning = PlanningState.Refused;
            p.Reasons = (int)Refusal.Zoning;
            Tell(NotificationType.Warning, "reallife_bld_notice_refused", ProjectName(p), null, TabProjects, p.Id);
        }

        // The estimate and the hours of a car park's project, as the plans show them.
        private CostSheet ParkPreview(ProjectFile p, out int limit)
        {
            limit = PlansRules.HeightLimit(p.District, true, Permits());
            return ParkSheet(p.Area);
        }

        // The site of a car park's works, set out on its ground in the programme (the company's crews, contractor 0, or a builder's).
        private int StartParkSite(ProjectFile p, int contractor, out string why)
        {
            why = null;
            if (!Lot(p.Lot, out LotView lot))
            {
                why = "no lot";
                return 0;
            }
            return StartLotSite(new UnityEngine.Vector3(lot.X, lot.Y, lot.Z), lot.Yaw, lot.Width, lot.Depth, contractor, p.Hours, out why);
        }

        // ------------------------------------------------------------------ the handover: the car park opens

        // The works are done: the car park is the player's, open to the public at the district's street rate. Its world is stood out of view.
        private void ParkOpened(ProjectFile p, int day)
        {
            if (_state.CarParkOfProject(p.Id) != null)
            {
                return;
            }
            LotFile land = _state.Lot(p.Lot);
            CarParkLayout plan = CarParkRules.LayoutOf(p.Width, p.Depth, p.Park);
            var c = new CarParkFile
            {
                Id = _state.NewId(), Owner = CarParkOwner.Player, Stage = CarParkStage.Open, Lot = p.Lot, Spaces = land != null ? land.Spaces : 1, Rows = land != null ? land.Rows : string.Empty,
                Width = p.Width, Depth = p.Depth, Mouth = Math.Max(1, p.Park), Bays = plan.Ok ? plan.Bays : 0, Mode = CarParkMode.Public, Price = ParkFair(p.Lot), Project = p.Id, Day = p.Created,
                Opened = day, LastDay = day
            };
            _state.CarParks.Add(c);
            TidyParks();
            if (land != null)
            {
                land.EmptySince = 0;
            }
            ParksChanged();
            Paper("reallife_park_paper_open", ParkName(c), c.Bays);
            ParkTell(NotificationType.Success, "reallife_park_notice_open", c);
            Logger?.Info("Construction: the car park " + c.Id + " opened at " + c.Lot + " on day " + day + ": " + c.Bays + " bays, public pay parking at " + c.Price + " cents an hour.");
        }

        // The oldest closed make room: a car park standing is always kept.
        private void TidyParks()
        {
            for (int i = 0; i < _state.CarParks.Count && _state.CarParks.Count > BuildState.CarParksMost; i++)
            {
                if (_state.CarParks[i].Stage == CarParkStage.Closed)
                {
                    _state.CarParks.RemoveAt(i);
                    i--;
                }
            }
        }

        // ------------------------------------------------------------------ in words

        /// <summary>A car park in words: where it is, and whose.</summary>
        public string ParkName(CarParkFile c)
        {
            return ConstructionText.T("reallife_park_name", "address", LotWords(c.Lot));
        }

        /// <summary>Who runs a car park: the player, or a builder of the city's.</summary>
        public static string ParkOwnerName(CarParkFile c)
        {
            return c.Owner == CarParkOwner.Player ? ConstructionText.T("reallife_park_owner_you")
                : ConstructionText.T(BuildCatalogue.RivalKeys[BuildCatalogue.Clamp(c.Rival, 0, BuildCatalogue.RivalCount - 1)]);
        }

        /// <summary>How a car park is let, in words.</summary>
        public static string ParkModeWords(CarParkFile c)
        {
            return c.Mode == CarParkMode.Public
                ? ConstructionText.T("reallife_park_mode_public", "price", ConstructionText.Cents(c.Price))
                : ConstructionText.T("reallife_park_mode_lease");
        }

        private void ParkTell(NotificationType type, string key, CarParkFile c)
        {
            var data = new Dictionary<string, string>(4)
            {
                { "name", ParkName(c) },
                { "bays", c.Bays.ToString(CultureInfo.InvariantCulture) },
                { "price", ConstructionText.Cents(c.Price) }
            };
            int id = c.Id;
            ConstructionGame.Tell(type, key, data, () => ParkDesk(id));
        }
    }
}
