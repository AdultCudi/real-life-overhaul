using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The city's builders build car parks too (the owner, 25 Oct 2026: "Whatever I do, the rivals and NPCs must be able to do"): each
    /// district, on a day of its own about every ten days, where its shops and offices want more public bays than it has and the
    /// ones it has are busy, a builder's development arm takes a run of two to four free columns the council zones for shops, offices
    /// or warehouses, pays the city their price, has its plans drawn in a day, City Hall's leave in a working day, and the works go up
    /// on the programme's site for the same hours as the player's; then it is public pay parking at the street rate, or, for one in
    /// three, leases. The player may lease bays at one of theirs at its pay machine. Drawn from the save's seed and the day; they rest
    /// in a Going Public session, where nothing of Uptown changes on one machine alone. COST: nothing per frame; the day's pass reads
    /// the district's columns once for each district whose day it is.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private struct ParkRun
        {
            public int First, Count;
        }

        private readonly List<ParkRun> _parkRuns = new List<ParkRun>(16);

        // ------------------------------------------------------------------ the day

        private void ParkNpcDay(int day)
        {
            if (GoingPublic.InSession || !ParkingOn || !Settings.ConstructionDevelopers || !DistrictLives())
            {
                return;
            }
            for (int i = 0; i < _state.CarParks.Count; i++)
            {
                CarParkFile c = _state.CarParks[i];
                if (c.Owner == CarParkOwner.Builder && c.UnderWay)
                {
                    ParkNpcStep(c, day, false);
                }
            }
            for (int k = 0; k < BuildCatalogue.Districts; k++)
            {
                int district = (day + k) % BuildCatalogue.Districts;
                if (_state.ParkNextDay[district] <= 0)
                {
                    _state.ParkNextDay[district] = day + CarParkRules.NpcFirstDay;   // the first look: the market is counted first
                    continue;
                }
                if (day < _state.ParkNextDay[district] || SiteCount() >= BuildCatalogue.SitesMost)
                {
                    continue;
                }
                if (ParkNpcBegin(day, district, false) != null)
                {
                    _state.ParkNextDay[district] = day + CarParkRules.NpcGapDays;
                    break;
                }
            }
        }

        // A column of free land the council zones for a use a car park may stand on, in the district, not yet taken by anyone.
        private bool ParkFreeColumn(int column, int district)
        {
            if (!IsFreeColumn(column) || DistrictOfColumn(column) != district)
            {
                return false;
            }
            int use = CouncilColumnUse(column);
            return use == (int)BuildUse.Shops || use == (int)BuildUse.Offices || use == (int)BuildUse.Warehouses;
        }

        /// <summary>
        /// A car park begun by a builder of the city's in a district: where its shops and offices want the bays (or at once, for a test),
        /// on a run of free columns the council zones for it whose way in is clear. Null when nothing is wanted, no run fits, or the
        /// programme is full.
        /// </summary>
        internal CarParkFile ParkNpcBegin(int day, int district, bool rush)
        {
            int shops = DemandFor(BuildUse.Shops, district), offices = DemandFor(BuildUse.Offices, district);
            int pressure = shops < 0 || offices < 0 ? 50 : (shops + offices) / 2;
            int supply = 0, lots = 0, underWay = 0, fillSum = 0, standing = 0;
            for (int i = 0; i < _state.CarParks.Count; i++)
            {
                CarParkFile c = _state.CarParks[i];
                if (c.Stage == CarParkStage.Closed || DistrictOfLot(c.Lot) != district)
                {
                    continue;
                }
                lots += c.Owner == CarParkOwner.Builder ? 1 : 0;
                underWay += c.Owner == CarParkOwner.Builder && c.UnderWay ? 1 : 0;
                if (c.Mode == CarParkMode.Public)
                {
                    supply += c.Bays;
                    if (c.Standing)
                    {
                        fillSum += CarParkRules.Occupied(c.Cars, c.Bays) * 10;
                        standing++;
                    }
                }
            }
            if (!rush && !CarParkRules.NpcWants(pressure, supply, lots, standing > 0 ? fillSum / standing : 0, underWay))
            {
                return null;
            }
            CellGrid grid = Cells;
            _parkRuns.Clear();
            for (int col = 0; col < grid.Columns.Count; col++)
            {
                if (!ParkFreeColumn(col, district))
                {
                    continue;
                }
                int before = grid.Prev(col);
                if (before >= 0 && ParkFreeColumn(before, district))
                {
                    continue;   // not a run's first
                }
                int run = 0;
                for (int at = col; at >= 0 && run < BuildCatalogue.SiteSpacesMost && ParkFreeColumn(at, district); at = grid.Next(at))
                {
                    run++;
                }
                if (run >= CarParkRules.ColumnsLeast)
                {
                    _parkRuns.Add(new ParkRun { First = col, Count = run });
                }
            }
            if (_parkRuns.Count == 0)
            {
                Logger?.Info("Construction: the city's builders want a car park in district " + district + " (pressure " + pressure + ", " + supply
                             + " public bays), but no free run of two columns is zoned for one.");
                return null;
            }
            int start = BuildRandom.Of(_state.Seed, day, 227).Range(0, _parkRuns.Count);
            for (int t = 0; t < _parkRuns.Count; t++)
            {
                ParkRun run = _parkRuns[(start + t) % _parkRuns.Count];
                CarParkFile made = ParkNpcTake(run, day, pressure, rush);
                if (made != null)
                {
                    return made;
                }
            }
            Logger?.Info("Construction: the city's builders found " + _parkRuns.Count + " run(s) of free columns for a car park in district " + district
                         + ", but none had a plan with a clear way in.");
            return null;
        }

        private CarParkFile ParkNpcTake(ParkRun run, int day, int pressure, bool rush)
        {
            CellGrid grid = Cells;
            int count = CarParkRules.NpcColumns(_state.Seed, day, run.Count);
            if (count < CarParkRules.ColumnsLeast)
            {
                return null;
            }
            int offset = BuildRandom.Of(_state.Seed, day, 229).Range(0, run.Count - count + 1), at = run.First;
            for (int i = 0; i < offset; i++)
            {
                at = grid.Next(at);
            }
            if (!LotOfColumn(at, out LotView firstLot) || !SiteSpaces(firstLot.Id, count, _parkSpaces))
            {
                return null;
            }
            for (int k = 0; k < _parkSpaces.Count; k++)
            {
                if (!FreeSpace(_parkSpaces[k]))
                {
                    _parkSpaces.Clear();
                    return null;
                }
            }
            LotView site = Joined(_parkSpaces);
            _parkSpaces.Clear();
            CarParkRules.Candidates(site.Width, site.Depth, _parkLayouts);
            if (_parkLayouts.Count == 0 || _parkLayouts[0].Bays < CarParkRules.BaysLeast || !CarParkGame.PickMouth(site, _parkLayouts, out CarParkLayout plan, out _))
            {
                return null;
            }
            int id = _state.NewId();
            var c = new CarParkFile
            {
                Id = id, Owner = CarParkOwner.Builder, Rival = DevelopmentRules.Rival(firstLot.Id, BuildUse.Shops), Stage = CarParkStage.Plans, Lot = firstLot.Id, Spaces = count, Width = site.Width,
                Depth = site.Depth, Mouth = plan.Code, Bays = plan.Bays, Mode = CarParkRules.NpcLeases(_state.Seed, id) ? CarParkMode.Lease : CarParkMode.Public,
                Price = ParkFair(firstLot.Id), Day = day,
                Until = rush ? day : day + CarParkRules.PlansDays, Seen = -1
            };
            if (c.Mode == CarParkMode.Lease)
            {
                c.Others = c.Bays * BuildRandom.Of(_state.Seed, id, 233).Range(30, 61) / 100;   // bays the city's own businesses hold already
            }
            _state.CityLand += SitePrice(firstLot.Id, count);
            _state.CarParks.Add(c);
            TidyParks();
            ParksChanged();
            Paper("reallife_park_paper_bought", ParkName(c), count);
            Logger?.Info("Construction: the city's builders: " + ParkOwnerName(c) + " bought " + count + " space(s) from " + c.Lot + " to build a car park of " + c.Bays + " bays, " + c.Mode
                         + " (pressure " + pressure + "); plans until day " + c.Until + ".");
            Changed();
            return c;
        }

        // ------------------------------------------------------------------ its steps

        /// <summary>One car park of the city's moved on by a day: its plans to City Hall, City Hall's leave and its works begun, its works done and it opened. A test rushes it.</summary>
        internal void ParkNpcStep(CarParkFile c, int day, bool rush)
        {
            if (!rush && day < c.Until)
            {
                return;
            }
            switch (c.Stage)
            {
                case CarParkStage.Plans:
                    c.Stage = CarParkStage.Planning;
                    c.Until = rush ? day : PlanningRules.AfterWorkingDays(day, CarParkRules.PermitDays);
                    break;
                case CarParkStage.Planning:
                    ParkNpcWorks(c, day, rush);
                    break;
                case CarParkStage.Building:
                    ParkNpcOpen(c, day);
                    break;
            }
            Changed();
        }

        private void ParkNpcWorks(CarParkFile c, int day, bool rush)
        {
            if (!ParkGround(c, out LotView site))
            {
                if (day > c.Until + 30)
                {
                    ParkNpcDrop(c, "its ground is no longer in the district");
                }
                return;
            }
            int hours = CarParkRules.Hours(c.Width * c.Depth);
            c.Stage = CarParkStage.Building;
            c.Until = rush ? day : PlanningRules.AfterWorkingDays(day, CarParkRules.WorkingDays(hours));
            c.SiteId = SiteCount() < BuildCatalogue.SitesMost ? StartLotSite(new Vector3(site.X, site.Y, site.Z), site.Yaw, site.Width, site.Depth, c.Rival, hours, out _) : 0;
            Paper("reallife_park_paper_started", ParkName(c), c.Bays);
            ParksChanged();
            Logger?.Info("Construction: the car park at " + c.Lot + " has City Hall's leave; " + hours + " working hour(s) until day " + c.Until
                         + (c.SiteId > 0 ? ", site " + c.SiteId + "." : ", no site (the programme is full)."));
        }

        private void ParkNpcOpen(CarParkFile c, int day)
        {
            if (c.SiteId > 0)
            {
                FinishSite(c.SiteId);   // the works come down as the car park stands
                c.SiteId = 0;
            }
            c.Stage = CarParkStage.Open;
            c.Opened = day;
            c.Seen = -1;
            _state.Rivals[c.Rival].Built++;
            _state.CityLandTax += LandRules.LandTax(SitePrice(c.Lot, c.Spaces), Math.Max(0, day - c.Day), ConstructionGame.DaysPerYear, false, Permits());
            ParksChanged();
            Paper("reallife_park_paper_open", ParkName(c), c.Bays);
            Logger?.Info("Construction: the car park at " + c.Lot + " (" + ParkOwnerName(c) + ") opened on day " + day + ": " + c.Bays + " bays, " + c.Mode + ".");
        }

        private void ParkNpcDrop(CarParkFile c, string why)
        {
            if (c.SiteId > 0)
            {
                DropSite(c.SiteId);
                c.SiteId = 0;
            }
            c.Stage = CarParkStage.Closed;
            ParksChanged();
            Logger?.Info("Construction: the city's car park at " + c.Lot + " given up: " + why + ".");
        }

        // ------------------------------------------------------------------ the programme's site of one

        // The pace of a car park of the city's: its schedule hands it over on its day, never the programme's crews.
        private bool ParkPace(int siteId, double done, double needed, float hours, out float pace)
        {
            pace = 0f;
            if (_state.CarParkSite(siteId) == null)
            {
                return false;
            }
            pace = Running ? (float)Math.Max(0.0, Math.Min(hours, needed * 0.995 - done)) : 0f;
            return true;
        }

        // The developer's name for a car park's board; null for a site of none of theirs.
        private string ParkBoard(int siteId)
        {
            CarParkFile c = _state.CarParkSite(siteId);
            return c != null ? ParkOwnerName(c) : null;
        }

        // A site of the city's car park handed over by the programme: its schedule ends the rest on its day.
        private void ParkSiteHanded(int siteId)
        {
            CarParkFile c = _state.CarParkSite(siteId);
            if (c != null)
            {
                c.SiteId = 0;
            }
        }
    }
}
