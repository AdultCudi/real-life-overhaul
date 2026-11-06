using System;
using System.Collections.Generic;
using System.Globalization;
using Entities;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The car parks' tests (Options > Tests, Construction; docs/features/construction.md, "Parking lots"): a car park built at once on
    /// land of the player's through the real pipeline (plans, City Hall, a builder hired for nothing, the keys) and stood; the player's car
    /// parked in a bay, legal in the game's own test of four wheels in a lane, the street rate paid back; a day of public income into the
    /// game's books; a lease offered, accepted, paid and ended; a car park of the city's builders put up and bays leased there. Each
    /// writes one "Parking check (...)" line ending PASS or FAIL and is a public static method a runner can call. Money moved after a test
    /// is real; the state from before the first test is kept for the construction game's put back.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private const string ParkCheck = "Parking check (";

        private static ConstructionDirector ParkReady(string test)
        {
            ConstructionDirector d = Instance;
            if (d == null || !ModState.Loaded || SaveGameManager.Current == null || !d._loaded)
            {
                TestRefusals.Tell(Logger, ParkCheck + test + ")", TestRefusal.NotLoaded);
                Logger?.Warn(ParkCheck + test + "): the construction game has not read this save yet: FAIL.");
                return null;
            }
            if (!d.ParkingOn || d.Cells.Columns.Count == 0 || GoingPublic.InSession)
            {
                TestRefusals.Tell(Logger, ParkCheck + test + ")", TestRefusal.FeatureOff);
                Logger?.Warn(ParkCheck + test + "): the construction game or car parks are off, Uptown is not a district at this load, or a Going Public session: FAIL.");
                return null;
            }
            return d;
        }

        private static bool ParkLine(string test, string what, bool pass)
        {
            Logger?.Warn(ParkCheck + test + "): " + what + ": " + (pass ? "PASS" : "FAIL") + ".");
            Notifications.Show(pass ? NotificationType.Info : NotificationType.Warning, pass ? "reallife_bld_mode_test_pass" : "reallife_park_test_fail", null, 7f, null, null, true, false);
            return pass;
        }

        private static string Yes(bool ok, string what) => what + (ok ? " yes" : " NO");

        // The nearest run of free squares to the player, so many side by side, each zoned for shops, offices or warehouses; its first column, or -1.
        private int ParkFreeRun(int spaces)
        {
            CellGrid grid = Cells;
            Vector3 near = ConstructionGame.Player;
            int commercial = BuildCatalogue.Zone(BuildUse.Shops) | BuildCatalogue.Zone(BuildUse.Offices) | BuildCatalogue.Zone(BuildUse.Warehouses);
            int best = -1;
            float far = float.MaxValue;
            for (int c = 0; c < grid.Columns.Count; c++)
            {
                bool all = true;
                int at = c;
                for (int k = 0; k < spaces && all; k++, at = at >= 0 ? grid.Next(at) : -1)
                {
                    all = at >= 0 && LotOfColumn(at, out LotView lot) && FreeSpace(lot) && (LandRules.ZonesOf(BuildCatalogue.Clamp(lot.Class, 0, BuildCatalogue.ClassCount - 1)) & commercial) != 0;
                }
                if (!all)
                {
                    continue;
                }
                grid.Rect(c, 0, 0f, out float x1, out float z1, out float x2, out float z2);
                float dx = (x1 + x2) * 0.5f - near.x, dz = (z1 + z2) * 0.5f - near.z, d = dx * dx + dz * dz;
                if (d < far)
                {
                    far = d;
                    best = c;
                }
            }
            return best;
        }

        // A car park of the player's built now through the real pipeline: land bought for nothing, the project made, its plans drawn, City Hall's decision, a builder hired for nothing, the keys.
        private bool ParkMakeLot(out ProjectFile p, out CarParkFile c, out string why)
        {
            p = null;
            c = null;
            why = null;
            if (_state.Lots.Count >= BuildCatalogue.LotsMost)
            {
                why = "the player holds the most lots there may be";
                return false;
            }
            int count = 3, first = ParkFreeRun(3);
            if (first < 0)
            {
                count = 2;
                first = ParkFreeRun(2);
            }
            if (first < 0)
            {
                why = "no two free squares side by side zoned for shops, offices or warehouses at this load";
                return false;
            }
            BeforeTest();
            BuyCells(PickColumns(first, count, 0, out _), true);
            RowsHeld(first, out LotFile site);
            if (site == null)
            {
                why = "the squares could not be bought for the test";
                return false;
            }
            ParkFacts f = ParkAssess(first, count, 0);
            if (!f.Ok)
            {
                why = "a car park cannot be built on the squares: " + f.Why + (string.IsNullOrEmpty(f.Blocker) ? string.Empty : " (" + f.Blocker + " is in the way)");
                return false;
            }
            int id = NewPark(site.Id, f.Code, out why);
            p = _state.Project(id);
            if (p == null)
            {
                return false;
            }
            SetArchitect(p, ArchitectKind.Firm, 1, null);
            int day = ConstructionGame.Day;
            p.PlansDay = day;
            PlansDrawn(p, CarParkRules.Quote(1, ParkSheet(p.Area).Total).Quality);   // the practice's day gone by
            p.Stage = ProjectStage.Planning;
            p.Planning = PlanningState.Submitted;
            p.Applications++;
            p.PlanningDay = day;
            p.DecideDay = day;
            Decide(p);   // City Hall's decision on its day
            if (p.Stage != ProjectStage.Ready)
            {
                why = "City Hall did not grant the car park (" + p.Planning + ")";
                return false;
            }
            int quoted = Quotes(p, _quotes);
            string answer = HireBuilder(p, CommissionRules.Cheapest(_quotes, quoted), true);
            if (!p.Hired || p.Stage != ProjectStage.Building)
            {
                why = "no builder could be hired for project " + p.Id + " (" + answer + ")";
                return false;
            }
            if (!FinishSite(p.SiteId) && p.Stage == ProjectStage.Building)
            {
                Handover(p, day);   // the site was gone from the programme: handed over all the same
            }
            c = _state.CarParkOfProject(p.Id);
            why = c == null ? "the project was handed over but no car park opened" : null;
            return c != null;
        }

        // The player's first car park standing, or one built now.
        private bool ParkTestLot(out CarParkFile c, out string why)
        {
            why = null;
            for (int i = 0; i < _state.CarParks.Count; i++)
            {
                if (_state.CarParks[i].Owner == CarParkOwner.Player && _state.CarParks[i].Standing)
                {
                    c = _state.CarParks[i];
                    return true;
                }
            }
            return ParkMakeLot(out _, out c, out why);
        }

        // ------------------------------------------------------------------ built at once

        /// <summary>
        /// Test: a car park built at once on land of the player's through the whole pipeline (plans in a day, City Hall in a working day, a builder, the
        /// keys) and stood in the world.
        /// </summary>
        public static bool TestParkBuildNow()
        {
            ConstructionDirector d = ParkReady("a lot built on your land at once");
            return d != null && d.ParkBuildTest();
        }

        private bool ParkBuildTest()
        {
            const string test = "a lot built on your land at once";
            if (GoingPublic.IsGuest)
            {
                return Failed(ParkCheck, test, "reallife_bld_test_host", "a Going Public guest starts no site of the city's programme");
            }
            if (!ParkMakeLot(out ProjectFile p, out CarParkFile c, out string why))
            {
                return Failed(ParkCheck, test, "reallife_bld_test_nothing", why ?? "no car park could be built");
            }
            CarParkLayout layout = CarParkRules.LayoutOf(c.Width, c.Depth, c.Mouth);
            int hours = CarParkRules.Hours(c.Width * c.Depth), works = CarParkRules.WorkingDays(hours);
            bool days = CarParkRules.PlansDays == 1 && CarParkRules.PermitDays == 1 && works >= 1 && works <= 3 && p.Hours == hours;
            bool opened = p.Stage == ProjectStage.Finished && c.Standing && c.Mode == CarParkMode.Public && c.Bays == layout.Bays && c.Price == ParkFair(c.Lot);
            long estimate = ParkSheet(c.Width * c.Depth).Total;
            bool cost = p.Estimate > 0L && Math.Abs(p.Estimate - estimate) <= estimate * 0.25 + 1;
            CarParkWorld world = ParkStandNow(c);
            bool stood = world != null && world.Ok;
            bool lanes = world != null && world.Lanes.Count == layout.Rows.Count && world.LaneBays == layout.Bays - layout.Accessible;
            string standing = world != null ? CarParkGame.Describe(world) : "not stood";
            bool pass = days && opened && cost && stood && lanes;
            TestDone("a car park of yours built at once on " + c.Spaces + " square(s) and open", TabProjects, p.Id);
            return ParkLine(test, "project " + p.Id + " on " + c.Spaces + " square(s) (" + c.Width + " by " + c.Depth + " m), " + layout.Bays + " bays, " + layout.Accessible
                                  + " for the disabled; plans " + CarParkRules.PlansDays + " day, City Hall " + CarParkRules.PermitDays + " working day, works " + hours + " hour(s) = "
                                  + works + " working day(s) " + Yes(days, "in range")
                                  + "; estimate " + p.Estimate + " against the rules' " + estimate + " " + Yes(cost, "agree") + "; open " + Yes(opened, "as public pay parking at the street rate")
                                  + "; in the world " + standing + " " + Yes(stood && lanes, "whole"), pass);
        }

        // ------------------------------------------------------------------ a car parked there

        /// <summary>
        /// Test: a car in a bay of the player's car park is legal in the game's own test (all four wheels in one of its lanes), a bay for the disabled and
        /// the aisle are not, and the street rate is paid back.
        /// </summary>
        public static bool TestParkCarNow()
        {
            ConstructionDirector d = ParkReady("your car parked in a bay");
            return d != null && d.ParkCarTest();
        }

        private bool ParkCarTest()
        {
            const string test = "your car parked in a bay";
            if (!ParkTestLot(out CarParkFile c, out string why))
            {
                return Failed(ParkCheck, test, "reallife_bld_test_nothing", why ?? "no car park of the player's");
            }
            CarParkWorld world = ParkStandNow(c);
            if (world == null || world.Lanes.Count == 0)
            {
                return Failed(ParkCheck, test, "reallife_bld_test_nothing", "the car park could not be stood with lanes of the game's parking (" + (world != null ? world.Why : "not stood") + ")");
            }
            CarParkLayout layout = CarParkRules.LayoutOf(c.Width, c.Depth, c.Mouth);
            ParkingLaneGenerator lane = world.Lanes[world.Lanes.Count - 1];
            CarParkGame.BayAt(lane, 0, out Vector3 bay, out Quaternion turn);
            int inBay = CarParkGame.WheelsOver(world, bay, turn, out string neighbourhood);
            string district = DistrictKeyOf(DistrictOfLot(c.Lot));
            bool legal = inBay == 4 && neighbourhood == district;
            int blue = layout.Accessible > 0 ? CarParkGame.WheelsOver(world, world.Root.transform.TransformPoint(new Vector3(layout.Blue[0].MidX, 0.1f, layout.Blue[0].MidZ)),
                world.Root.transform.rotation, out _) : 0;
            ParkRect aisleRect = layout.Aisles.Count > 0 ? layout.Aisles[0] : default(ParkRect);
            int aisle = layout.Aisles.Count > 0
                ? CarParkGame.WheelsOver(world, world.Root.transform.TransformPoint(new Vector3(aisleRect.MidX, 0.1f, aisleRect.MidZ)), world.Root.transform.rotation, out _)
                : 0;
            bool kept = blue < 4 && aisle < 4;
            // The player's own car there: counted by where it stands, charged by the game's hour, paid back at night.
            string counted = "no other car of the player's to stand there";
            bool count = true;
            GameInstance save = SaveGameManager.Current;
            VehicleInstance car = null;
            for (int i = 0; save.VehicleInstances != null && i < save.VehicleInstances.Count && car == null; i++)
            {
                VehicleInstance v = save.VehicleInstances[i];
                car = v != null && v.id != save.ActiveVehicleId ? v : null;
            }
            if (car != null && ParkGround(c, out LotView site))
            {
                Vector3 was = car.position;
                ParkingState state = car.parkingState;
                string street = car.streetName;
                car.position = bay;
                car.parkingState = ParkingState.Legal;
                car.streetName = string.Empty;
                int standing = ConstructionGame.PlayerCarsIn(site.X, site.Z, site.Yaw, site.Width, site.Depth);
                count = standing == 1;
                counted = standing + " car of the player's counted in the lot (1 expected)";
                car.position = was;
                car.parkingState = state;
                car.streetName = street;
            }
            c.Own = 3;
            long rate = (long)Math.Round(ConstructionGame.ParkingRate(district) * 100.0);
            long refund = CarParkRules.OwnFee(3, ConstructionGame.ParkingRate(district));
            long upkeep = CarParkRules.Upkeep(c.Bays, c.Mode, ConstructionGame.Prices), taken = c.Taken / 100L;
            float before = ConstructionGame.Cash;
            ParkAccounts(c, ConstructionGame.Day);
            float change = ConstructionGame.Cash - before;
            bool paid = Math.Abs(change - (taken + refund - upkeep)) < 1.5f && c.Own == 0;
            bool pass = legal && kept && count && paid;
            TestDone("your car parked in a bay of your car park", TabProjects, 0);
            return ParkLine(test, "a car in the bay: " + inBay + " of 4 wheels in a lane of the car park, the lane's district " + neighbourhood + " (" + district + ") " + Yes(legal, "legal")
                                  + "; a bay for the disabled " + blue + " wheels and the aisle " + aisle + " " + Yes(kept, "not legal") + "; " + counted + " " + Yes(count, "counted")
                                  + "; 3 car-hours at the street rate of " + rate + " cents paid back " + refund + " (the night's upkeep " + upkeep + "), the purse moved "
                                  + change.ToString("0.00", CultureInfo.InvariantCulture) + " " + Yes(paid, "as expected"), pass);
        }

        // ------------------------------------------------------------------ a day of income

        /// <summary>Test: a day of public pay parking worked hour by hour and paid into the game's books as its own line with the upkeep against it.</summary>
        public static bool TestParkIncomeNow()
        {
            ConstructionDirector d = ParkReady("a day of public income");
            return d != null && d.ParkIncomeTest();
        }

        private bool ParkIncomeTest()
        {
            const string test = "a day of public income";
            if (!ParkTestLot(out CarParkFile c, out string why))
            {
                return Failed(ParkCheck, test, "reallife_bld_test_nothing", why ?? "no car park of the player's");
            }
            BeforeTest();
            ParkSetMode(c, CarParkMode.Public);
            c.Price = ParkFair(c.Lot);
            c.Taken = 0L;
            c.Own = 0;
            int day = ConstructionGame.Day, busiest = 0, total = 0;
            long cents = 0L;
            var curve = new System.Text.StringBuilder(96);
            for (int hour = 0; hour < 24; hour++)
            {
                ParkHour(day, hour);
                busiest = Math.Max(busiest, c.Cars);
                total += c.Cars;
                curve.Append(hour > 0 ? "," : string.Empty).Append(c.Cars);
            }
            cents = c.Taken;
            long income = cents / 100L, upkeep = CarParkRules.Upkeep(c.Bays, c.Mode, ConstructionGame.Prices);
            float before = ConstructionGame.Cash;
            ParkAccounts(c, day + 1);
            float change = ConstructionGame.Cash - before;
            bool worked = income > 0L && busiest > 0 && busiest <= c.Bays;
            bool paid = Math.Abs(change - (income - upkeep)) < 1.5f && c.Taken == 0L;
            bool listed = c.Daily.Count > 0 && c.Daily[c.Daily.Count - 1] == (int)(income - upkeep);
            bool pass = worked && paid && listed;
            TestDone("a day of public pay parking worked and paid into your books", TabProjects, 0);
            return ParkLine(test, "car park " + c.Id + " (" + c.Bays + " bays at " + c.Price + " cents an hour): cars by the hour " + curve + ", busiest " + busiest + " "
                                  + Yes(worked, "within the bays")
                                  + "; took " + income + ", upkeep " + upkeep + ", the purse moved " + change.ToString("0.00", CultureInfo.InvariantCulture) + " " + Yes(paid, "as expected")
                                  + "; the day's list says " + (c.Daily.Count > 0 ? c.Daily[c.Daily.Count - 1].ToString(CultureInfo.InvariantCulture) : "nothing") + " " + Yes(listed, "right"), pass);
        }

        // ------------------------------------------------------------------ a lease

        /// <summary>Test: a car park let by the month: a business near it offers for bays, the offer accepted, paid on its term's day, ended; going public pays the unused days back.</summary>
        public static bool TestParkLeaseNow()
        {
            ConstructionDirector d = ParkReady("a lease offered, accepted and paid");
            return d != null && d.ParkLeaseTest();
        }

        private bool ParkLeaseTest()
        {
            const string test = "a lease offered, accepted and paid";
            if (!ParkTestLot(out CarParkFile c, out string why))
            {
                return Failed(ParkCheck, test, "reallife_bld_test_nothing", why ?? "no car park of the player's");
            }
            BeforeTest();
            int day = ConstructionGame.Day;
            c.Leases.Clear();
            c.Offers.Clear();
            ParkSetMode(c, CarParkMode.Lease);
            ParkRound r = ParkAround(c, day);
            var near = new List<ParkNeighbour> { new ParkNeighbour { Name = "Test and Company", Who = CarParkLease.Business, Weight = 6, Metres = 60 } };
            ParkOffersFrom(c, day, r, near, true);
            bool offered = c.Offers.Count == 1 && c.Offers[0].Bays >= CarParkRules.LeaseBaysLeast && c.Offers[0].Monthly > 0L;
            if (!offered)
            {
                return ParkLine(test, "car park " + c.Id + " got " + c.Offers.Count + " offer(s) from the business put near it (one expected)", false);
            }
            CarParkOffer o = c.Offers[0];
            int bays = o.Bays;
            long month = o.Monthly * bays / 100L;
            float before = ConstructionGame.Cash;
            ParkAccept(c, o.Id);
            float first = ConstructionGame.Cash - before;
            bool accepted = c.Leases.Count == 1 && c.Offers.Count == 0 && Math.Abs(first - month) < 1.5f && c.Leased == bays;
            ParkHour(day, 10);
            bool filled = c.Cars == CarParkRules.LeaseCars(bays, 10, LandRules.Weekday(day) >= 5) && c.Cars <= bays;
            c.Leases[0].PaidTo = day - 1;   // its term is over
            before = ConstructionGame.Cash;
            ParkLeasesDay(c, day);
            float second = ConstructionGame.Cash - before;
            bool renewed = c.Leases.Count == 1 && Math.Abs(second - month) < 1.5f && c.Leases[0].PaidTo >= day;
            ParkEnd(c, c.Leases[0].Id);
            c.Leases[0].PaidTo = day - 1;
            ParkLeasesDay(c, day);
            bool ended = c.Leases.Count == 0;
            before = ConstructionGame.Cash;
            c.Leases.Add(new CarParkLease { Id = _state.NewId(), Name = "Test and Company", Who = CarParkLease.Business, Bays = bays, Monthly = o.Monthly, Start = day, PaidTo = day + 14 });
            long refund = ParkLeaseRefund(c);
            ParkSetMode(c, CarParkMode.Public);
            float back = ConstructionGame.Cash - before;
            bool refunded = c.Leases.Count == 0 && refund > 0L && Math.Abs(back + refund) < 1.5f;
            bool pass = accepted && filled && renewed && ended && refunded;
            TestDone("a business leased bays of your car park by the month", TabProjects, 0);
            return ParkLine(test, "an offer for " + bays + " bays at " + o.Monthly + " cents a bay a month; accepted, the first month " + month + " paid, the purse moved "
                                  + first.ToString("0.00", CultureInfo.InvariantCulture) + " " + Yes(accepted, "right") + "; its cars at ten o'clock " + c.Cars + " "
                                  + Yes(filled, "by the lessees' hours")
                                  + "; on its term's day the next month " + Yes(renewed, "paid") + "; marked to end, then " + Yes(ended, "over") + "; going public paid back "
                                  + refund + ", the purse moved "
                                  + back.ToString("0.00", CultureInfo.InvariantCulture) + " " + Yes(refunded, "right"), pass);
        }

        // ------------------------------------------------------------------ the city's builders

        /// <summary>Test: a builder of the city's puts up a car park at once (land, plans, City Hall, works, open) and the player leases bays there.</summary>
        public static bool TestParkCityNow()
        {
            ConstructionDirector d = ParkReady("a builder of the city puts up a car park");
            return d != null && d.ParkCityTest();
        }

        private bool ParkCityTest()
        {
            const string test = "a builder of the city puts up a car park";
            if (GoingPublic.IsGuest || !Settings.ConstructionDevelopers)
            {
                return Failed(ParkCheck, test, "reallife_bld_test_host", "a Going Public guest, or the city's developers are off: the city's builders put up nothing");
            }
            int day = ConstructionGame.Day;
            BeforeTest();
            CarParkFile c = null;
            for (int k = 0; k < BuildCatalogue.Districts && c == null; k++)
            {
                c = ParkNpcBegin(day, (DistrictOfLot(ParkFreeLotId()) + k) % BuildCatalogue.Districts, true);
            }
            if (c == null)
            {
                return Failed(ParkCheck, test, "reallife_bld_test_nothing", "no run of two free squares zoned for a car park with a clear way in");
            }
            long land = _state.CityLand;
            bool planned = c.Owner == CarParkOwner.Builder && c.Stage == CarParkStage.Plans && c.UnderWay && ParkUnderWayOn(c.Lot);
            for (int step = 0; step < 4 && c.Stage != CarParkStage.Open; step++)
            {
                ParkNpcStep(c, day, true);   // the plans to City Hall, City Hall's leave and the works, the works done
            }
            bool open = c.Stage == CarParkStage.Open && c.Standing && c.SiteId == 0;
            CarParkWorld world = ParkStandNow(c);
            bool stood = world != null && world.Ok;
            int room = ParkLeaseRoom(c);
            long month = room > 0 ? ParkLeasePrice(c) / 100L : 0L;
            float before = ConstructionGame.Cash;
            string answer = room > 0 ? ParkLeaseFrom(c, 1) : "no room";
            float spent = before - ConstructionGame.Cash;
            bool leased = room > 0 && answer == null && c.LeasedByPlayer == 1 && Math.Abs(spent - month) < 1.5f;
            bool pass = planned && open && stood && leased;
            TestDone("a car park put up by " + ParkOwnerName(c) + " and a bay leased there", TabLand, 0);
            return ParkLine(test, ParkOwnerName(c) + " bought " + c.Spaces + " square(s) at " + c.Lot + " for a car park of " + c.Bays + " bays, " + c.Mode + ", the city's land at "
                                  + land + " " + Yes(planned, "planned")
                                  + "; works done and open " + Yes(open, "as public parking") + "; in the world " + (world != null ? CarParkGame.Describe(world) : "not stood") + " "
                                  + Yes(stood, "whole")
                                  + "; a bay leased for " + month + " a month (the purse moved " + (-spent).ToString("0.00", CultureInfo.InvariantCulture) + ") " + Yes(leased, "right"), pass);
        }

        // Any lot of the district, to start the districts' turn at the one the player is near.
        private string ParkFreeLotId()
        {
            int first = ParkFreeRun(2);
            return first >= 0 && LotOfColumn(first, out LotView lot) ? lot.Id : string.Empty;
        }
    }
}
