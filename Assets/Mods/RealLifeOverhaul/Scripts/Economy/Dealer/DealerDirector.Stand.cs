using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// What the showroom owes a car it paid for that the game does not stand as the model paid for (CarStandRules; docs/features/car-suppliers.md, "The car
    /// paid for is the car that stands"): a listing of a model the game has not is refused before any money moves; a car bought at once, a car that comes
    /// later and a car found stood as another model when the player walks in are each paid back in full, under the line the purchase was booked under,
    /// with a notice that says why. Nothing per frame.
    /// </summary>
    public sealed partial class DealerDirector
    {
        private readonly List<StockCar> _wrong = new List<StockCar>(2);   // the cars Park found stood as another model than the one paid for

        // A listing of a model the game has not: refused before any money moves, and said so.
        private bool Unavailable(string type)
        {
            Notifications.Show(NotificationType.Error, CarStandRules.RefuseKey, new Dictionary<string, string>(1) { { "car", DealerGame.CarName(type) } }, 8f);
            return false;
        }

        // A car paid for that could not be made, its price already given back: the model's fault is said with the price; a lot with no place for it only refuses, as before.
        private bool StandNot(string type, int price, CarMaking making)
        {
            if (!CarStandRules.ModelFault(making))
            {
                return Refuse(DealerRefusal.Busy);
            }
            StandTell(type, CarStandRules.Refund(price), making, TabMarket);
            return false;
        }

        private void StandTell(string type, int back, CarMaking making, int tab)
        {
            Tell(NotificationType.Warning, CarStandRules.NoticeKey(making),
                new Dictionary<string, string>(2) { { "car", DealerGame.CarName(type) }, { "price", DealerText.Money(back) } }, tab, null);
        }

        // A car on its way that the game cannot stand when it comes: its delivery ends and all it cost is given back, said why.
        private void Unstood(DealerDelivery q, CarMaking making)
        {
            DealerYard home = _state.Yard(q.Place);
            BuildingRegistration reg = home != null ? RegOf(home) : null;
            int back = CarStandRules.Refund(q.Cost);
            DealerGame.Give(back, DealerLines.BoughtType, DealerGame.Bought(q.Type), reg);
            Book(home, 0, -back, 0, 0);   // what its order booked as bought comes off the showroom's day
            _state.Deliveries.Remove(q);
            _supply.Arrived(q.Id);
            Logger?.Warn("Dealership: the " + q.Type + " on its way could not be stood when it came (" + making + "); the " + back + " it cost was given back.");
            StandTell(q.Type, back, making, TabStock);
        }

        // The cars Park found stood as another model than the one paid for: each is taken out of the game's save and off the lot and the stock, and paid back
        // all it cost, said why. Called wherever Park is.
        private void DropWrong(DealerYard y)
        {
            for (int i = 0; i < _wrong.Count; i++)
            {
                StockCar car = _wrong[i];
                int back = CarStandRules.Refund(car.Cost);
                Logger?.Warn("Dealership: the " + car.Type + " of '" + y.Name + "' is not the model paid for (" + DealerGame.StoodAs(car.VehicleId) + "); it was taken off and the " + back
                             + " it cost was given back.");
                DealerGame.RemoveCar(car.VehicleId);
                _ids.Remove(car.VehicleId);
                _bays.Forget(car.VehicleId);
                RemoveDeals(y, car.Id);
                y.Cars.Remove(car);
                DealerGame.Give(back, DealerLines.BoughtType, DealerGame.Bought(car.Type), RegOf(y));
                Book(y, 0, -back, 0, 0);
                StandTell(car.Type, back, CarMaking.WrongModel, TabStock);
            }
            if (_wrong.Count > 0)
            {
                RebuildIds();
            }
        }
    }
}
