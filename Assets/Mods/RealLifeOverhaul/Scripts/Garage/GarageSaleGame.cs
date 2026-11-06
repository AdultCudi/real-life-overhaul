using System.Collections.Generic;
using BAModAPI;
using Helpers;
using UI.Notification;
using UnityEngine;
using UnityEngine.Events;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A car with the custom shop's work on it, sold through the game's item panel: the buyer pays the work's worth on top
    /// of the game's own price (<see cref="TuningRules.SaleWorth"/>). The game books its sale, then removes the car, telling
    /// <c>VehicleHelper.onVehicleDestroyed</c> first (ItemPanelUI.ClickSell, VehicleHelper.Delete), so the newest
    /// transaction says whether the car that goes was sold. The only place the sale meets the game. Nothing per frame: it
    /// runs on that event only, once per car that goes.
    /// </summary>
    public static class GarageSaleGame
    {
        private const string SoldInfo = "itemSoldInfo";   // the game's own key for what its sale was of
        public const string TransactionKey = "reallife_transaction_garage_resale";

        public static IModLogger Logger;

        private static UnityAction<VehicleController> _onGone;   // made once: the same delegate goes on and comes off
        private static bool _listening;

        /// <summary>For the test button: whether the sale is being listened for.</summary>
        public static bool Listening => _listening;

        public static void Start()
        {
            if (_listening || VehicleHelper.onVehicleDestroyed == null)
            {
                return;
            }
            _onGone = _onGone ?? Guard.Listener<VehicleController>(Gone);
            VehicleHelper.onVehicleDestroyed.AddListener(_onGone);
            _listening = true;
        }

        public static void Stop()
        {
            if (!_listening)
            {
                return;
            }
            _listening = false;
            VehicleHelper.onVehicleDestroyed?.RemoveListener(_onGone);
        }

        /// <summary>What selling this car now would fetch for its work; also whether the game would buy it at all.</summary>
        public static int WorthNow(VehicleController vehicle, out float gamePrice)
        {
            gamePrice = 0f;
            VehicleInstance instance = vehicle != null ? vehicle.vehicleInstance : null;
            GarageBuild build = instance != null ? GarageState.Of(instance.id) : null;
            if (instance == null || instance.VehicleType == null)
            {
                return 0;
            }
            gamePrice = instance.GetSellingPrice();
            return build != null ? Worth(instance, build, gamePrice) : 0;
        }

        // The work's share in the car's condition: what the game pays for the car itself (its cargo apart) against a perfect one.
        private static int Worth(VehicleInstance instance, GarageBuild build, float gamePrice)
        {
            float perfect = instance.VehicleType.price * ItemHelper.sellingMultiplier;
            float body = Mathf.Max(0f, perfect - instance.CalculateRepairCost());
            return gamePrice > 0f ? TuningRules.SaleWorth(build.Spent, body, perfect) : 0;
        }

        private static void Gone(VehicleController vehicle)
        {
            VehicleInstance instance = vehicle != null ? vehicle.vehicleInstance : null;
            if (!Settings.Garage || !Settings.GarageResale || instance == null || !GarageState.Loaded || !GoingPublic.MineVehicle(vehicle))
            {
                return;   // another Going Public player's car is theirs to sell, on their own machine
            }
            GarageBuild build = GarageState.Of(instance.id);
            if (build == null || build.Spent <= 0 || vehicle.vehicleType == null || instance.VehicleType == null)
            {
                return;
            }
            Transaction last = Newest();
            string info = null;
            if (last != null && last.transactionData != null)
            {
                last.transactionData.TryGetValue(SoldInfo, out info);
            }
            float price = instance.GetSellingPrice();
            if (last == null || !TuningRules.IsSale(last.transactionType, info, last.amount, vehicle.vehicleType.vehicleTypeName, price))
            {
                return;   // a tow, a hire, a car taken back: nothing was sold
            }
            int worth = Worth(instance, build, price);
            GarageState.Set(instance.id, null);   // the work goes with the car: never paid twice
            if (worth > 0)
            {
                Crime.Give(worth, TransactionKey);
                Notifications.Show(NotificationType.Success, "reallife_tuning_sold", new Dictionary<string, string>(1) { { "amount", GarageText.Money(worth) } }, 8f);
            }
            Logger?.Info("Garage: " + vehicle.vehicleType.vehicleTypeName + " sold for " + Mathf.RoundToInt(price) + "; its work (" + build.Spent + " spent) fetched " + worth + " more.");
        }

        // The game keeps its transactions oldest first and adds the sale just before the car goes: the last one.
        private static Transaction Newest()
        {
            GameInstance save = SaveGameManager.Current;
            Transaction last = null;
            if (save == null || save.Transactions == null)
            {
                return null;
            }
            foreach (Transaction t in save.Transactions)
            {
                last = t;
            }
            return last;
        }
    }
}
