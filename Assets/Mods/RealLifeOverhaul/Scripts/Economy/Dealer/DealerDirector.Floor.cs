using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// What the Car Showroom's floor needs of the showroom (Floor/): the bays the player chose for its cars, kept in the
    /// save under a line of their own (Floor/ShowroomBays) and used by every layout of a lot (DealerGame.Park); the hour
    /// just worked, handed to the floor of the showroom the player stands in so that its visitors follow it; and which
    /// showroom that is. Nothing here changes how an hour is worked.
    /// </summary>
    public sealed partial class DealerDirector
    {
        private struct HeardHour
        {
            public int Hour, Visitors, Served;
            public bool Staffed;
        }

        private readonly ShowroomBays _bays = new ShowroomBays();
        private readonly Dictionary<string, HeardHour> _heard = new Dictionary<string, HeardHour>(StringComparer.Ordinal);
        private DealerYard _floorYard;      // the showroom the floor was last told the player stands in

        /// <summary>The bays the player chose for the showrooms' cars.</summary>
        internal ShowroomBays Bays => _bays;

        /// <summary>The player's showroom they stand in now; null if none (or the feature is not running).</summary>
        internal DealerYard YardInside()
        {
            return _running && _state != null && _laidInside != null ? _state.Yard(_laidInside) : null;
        }

        // ---------------------------------------------------------------- the save

        private void ReadBays(GameInstance save)
        {
            _bays.Clear();
            if (save.modData == null || !save.modData.TryGetValue(ShowroomBayStore.Key, out string text) || string.IsNullOrEmpty(text))
            {
                return;
            }
            if (!ShowroomBayStore.Read(text, _bays, out int version))
            {
                SaveLines.Unreadable(save.modData, ShowroomBayStore.Key, "it could not be read");   // (put back as it was read at every save: Core/SaveLines)
                Logger?.Warn("Car Showroom: the save's line of chosen bays could not be read (" + ShowroomBayStore.Key + "); every car stands where the showroom puts it, and the line is kept in the save as it is.");
                return;
            }
            if (version > ShowroomBayStore.Version)
            {
                SaveLines.Later?.Invoke(ShowroomBayStore.Key, "line version " + version + ", this one knows " + ShowroomBayStore.Version);
            }
        }

        // The choices of cars still on a lot, under their own key; none, and the key goes.
        private void WriteBays(GameInstance save)
        {
            _bays.Prune(_ids);
            if (_bays.Count == 0)
            {
                save.modData.Remove(ShowroomBayStore.Key);
            }
            else
            {
                save.modData[ShowroomBayStore.Key] = ShowroomBayStore.Write(_bays);
            }
        }

        // ---------------------------------------------------------------- the bays

        /// <summary>
        /// The player puts a car of the showroom they stand in on a bay, the way round they chose: the car standing there
        /// takes the car's old bay, and the choice is kept. Every car of the lot is then put on its bay again. False, with
        /// the game's notice, if it cannot be.
        /// </summary>
        public bool PutOnBay(int carId, int bay, bool turned)
        {
            DealerYard y = null;
            StockCar car = _state != null ? _state.Car(carId, out y) : null;
            if (car == null || y == null || y != YardInside())
            {
                Notifications.ShowError(car == null ? "reallife_dealer_refuse_gone" : "reallife_showroom_refuse_inside");
                return false;
            }
            if (bay < 0 || bay >= y.Spots.Count)
            {
                return false;
            }
            int from = car.Spot;
            for (int c = 0; c < y.Cars.Count; c++)
            {
                StockCar other = y.Cars[c];
                if (other == car || other.Spot != bay)
                {
                    continue;
                }
                other.Spot = from;
                if (_bays.Of(y.Place, other.VehicleId) != null)
                {
                    if (from >= 0)
                    {
                        _bays.Choose(y.Place, other.VehicleId, from, _bays.TurnedOf(y.Place, other.VehicleId));
                    }
                    else
                    {
                        _bays.Forget(other.VehicleId);
                    }
                }
            }
            car.Spot = bay;
            _bays.Choose(y.Place, car.VehicleId, bay, turned);
            SeeTo(y);
            Logger?.Info("Car Showroom: the " + car.Type + " of '" + y.Name + "' put on bay " + (bay + 1) + " of " + y.Spots.Count + (turned ? ", turned round" : string.Empty)
                         + (from >= 0 && from != bay ? " (it stood on bay " + (from + 1) + ")" : string.Empty) + "; " + _bays.Count + " choice(s) kept.");
            return true;
        }

        /// <summary>Every car of a showroom on its bay again (after a bay was being chosen and was not, or was).</summary>
        internal void ParkAgain(DealerYard y)
        {
            if (!_running || _broken || y == null || _state == null || _state.Yard(y.Place) != y)
            {
                return;
            }
            try
            {
                SeeTo(y);
            }
            catch (Exception e)
            {
                Fault("putting the cars back on their bays", e);
            }
        }

        // ---------------------------------------------------------------- the floor

        // The floor told which showroom the player stands in: when it changes, or while the floor is not showing it (its
        // option was off, or the lot was not laid out yet). The floor itself remembers a showroom it cannot show.
        private void TellFloor(DealerYard y)
        {
            ShowroomFloor floor = ShowroomFloor.Instance;
            if (floor == null || (y == _floorYard && floor.Yard == y))
            {
                _floorYard = y;
                return;
            }
            _floorYard = y;
            floor.Stand(y);
        }

        // An hour of a showroom has been worked: kept for the floor, and the floor told if the player stands in it.
        private void Heard(DealerYard y, int hour, in DealerHour got, in YardHour h)
        {
            _heard[y.Place] = new HeardHour { Hour = hour, Visitors = got.Visitors, Served = got.Served, Staffed = h.Open && h.Staff > 0 };
            if (y == _floorYard)
            {
                ShowroomFloor.Instance?.NewHour(y);
            }
        }

        /// <summary>
        /// What an hour of a showroom brings its floor: its visitors, how many were served, whether a salesperson is on
        /// shift, and the cars offers were made on in it (their places in the lot's list). The hour as worked if it has
        /// been; before the first hour the showroom works while the player is in it, what it should bring, read from the
        /// game as the hour is but worked out without changing anything.
        /// </summary>
        internal void FloorHour(DealerYard y, int hour, out int visitors, out int served, out bool staffed, List<int> offerCars)
        {
            FloorOffers(y, hour, offerCars);
            if (_heard.TryGetValue(y.Place, out HeardHour heard) && heard.Hour == hour)
            {
                visitors = heard.Visitors;
                served = heard.Served;
                staffed = heard.Staffed;
                return;
            }
            BuildingRegistration reg = RegOf(y);
            YardHour h = reg != null ? DealerGame.Hour(reg) : new YardHour();
            visitors = (int)Math.Min(1000L, (DealerRules.VisitorThousandths(h, hour % 24, y.Cars.Count) + 500) / 1000);
            served = DealerRules.Served(visitors, h.Staff);
            staffed = h.Open && h.Staff > 0;
        }

        /// <summary>The cars of a lot offers were made on in an hour, by their places in its list.</summary>
        internal void FloorOffers(DealerYard y, int hour, List<int> offerCars)
        {
            offerCars.Clear();
            for (int d = 0; d < y.Deals.Count; d++)
            {
                if (y.Deals[d].Made != hour)
                {
                    continue;
                }
                for (int c = 0; c < y.Cars.Count; c++)
                {
                    if (y.Cars[c].Id == y.Deals[d].Car)
                    {
                        offerCars.Add(c);
                        break;
                    }
                }
            }
        }
    }
}
