using System;
using System.Collections.Generic;
using BigAmbitions.Items;
using BigAmbitions.Tags;
using Extensions;
using Helpers;
using Localizor;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The crimes against the player that nobody sees happen: a break-in at one of their shops
    /// overnight, their parked car broken into, a scam call. Rolled at most once a day each
    /// from the director's hourly look (nothing here runs otherwise), all behind the "crimes
    /// against you" option, each with days of rest after it has happened, and each bounded:
    ///   break-in    stock goes from the shelves exactly the way the game's own theft takes it
    ///               (the amount comes off the shelf, its cost is added to the shop's stolen
    ///               value and shows as Theft in the books), scaled down by the shop's security
    ///               level and capped; a repair bill never larger than the cash in hand; the
    ///               alarm at night and a police report in the morning.
    ///   car         a little damage on one parked car, worth a capped repair at the game's own
    ///               repair price. The car is never moved, emptied or removed.
    ///   scam call   a prompt; only saying yes can cost anything, and that is capped too.
    /// </summary>
    internal sealed class OffscreenCrimes
    {
        private const string EmptyBusinessType = "ba:businesstype_empty";
        private const float BreakInChance = 0.04f;     // per night, before security and the frequency option
        private const int BreakInRestDays = 6;
        private static float BreakInValueCap => Prices.At(2500f);
        private const float BreakInStockShare = 0.25f; // of what is on the shelves, at most
        private const float CarChance = 0.03f;         // per night
        private const int CarRestDays = 7;
        internal const float CarRepairCap = 250f;
        internal const int RepairLeast = 60, RepairMost = 350;     // a break-in's repair bill, at the start's prices (Crime.Rob takes them at today's)
        internal const int ScamLossLeast = 30, ScamLossMost = 400;
        private static float ScamLeast => Prices.At(100f);   // a scam call needs this much on the player
        private const float CarDamageMost = 0.6f;      // never anywhere near broken down
        private const float ScamChance = 0.06f;        // per day
        private const int ScamRestDays = 5;

        private static readonly string[] ScamBodies =
        {
            "reallife_crime_scam_body_1", "reallife_crime_scam_body_2", "reallife_crime_scam_body_3"
        };

        private readonly List<BuildingRegistration> _shops = new List<BuildingRegistration>();
        private readonly List<ItemInstance> _shelves = new List<ItemInstance>();
        private int _nightRolled = -1;
        private int _dayRolled = -1;
        private int _breakInDay = -100;
        private int _carDay = -100;
        private int _scamDay = -100;
        private int _scamAsked;
        private int _lastScam = -1;
        private int _testNext;

        // The morning after a break-in.
        private string _reportShop;
        private float _reportValue;

        private static float Frequency => Mathf.Lerp(0.5f, 2f, Mathf.InverseLerp(1f, 10f, Settings.CrimeFrequency));

        public void OnNewHour(int hour)
        {
            int day = TimeHelper.CurrentDay;
            if (_reportShop != null && hour >= 8 && hour <= 20)
            {
                MorningReport();
            }
            if (!Settings.CrimeAgainstPlayer)
            {
                return;
            }

            // Once a night, in the small hours.
            if (hour >= 1 && hour <= 5 && _nightRolled != day)
            {
                _nightRolled = day;
                if (day - _breakInDay >= BreakInRestDays && Random.value < BreakInChance * Frequency)
                {
                    BreakIn(false);
                }
                if (day - _carDay >= CarRestDays && Random.value < CarChance * Frequency)
                {
                    CarBreakIn();
                }
            }

            // Once a day, in office hours, and only when the player can take a call.
            if (hour >= 10 && hour <= 18 && _dayRolled != day && LifeEvents.IsSafeForEvent() && !HudConfirm.isOpen
                && !PlayerHelper.IsUsingVehicle)
            {
                _dayRolled = day;
                if (day - _scamDay >= ScamRestDays && Random.value < ScamChance * Frequency)
                {
                    ScamCall();
                }
            }
        }

        /// <summary>Test button: break-in, car, scam call, in turn. Says so when the one whose turn it is cannot happen.</summary>
        public void TestNext()
        {
            int which = _testNext++ % 3;
            bool done = which == 0 ? BreakIn(true) : which == 1 ? CarBreakIn() : ScamCall();
            if (!done)
            {
                Notifications.Show(NotificationType.Warning,
                    which == 0 ? "reallife_crime_none_breakin" : which == 1 ? "reallife_crime_none_car" : "reallife_crime_none_scam",
                    new System.Collections.Generic.Dictionary<string, string>(1) { { "least", ScamLeast.ToCurrencyFormat() } }, 7f);
            }
            else if (which == 0)
            {
                MorningReport();   // the tester should not have to wait for eight o'clock
            }
        }

        // ---------------------------------------------------------------- break-in

        private bool BreakIn(bool forced)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || save.BuildingRegistrations == null)
            {
                return false;
            }
            BuildingRegistration inside = BuildingManager.IsInsideBuilding ? InstanceBehavior<BuildingManager>.Instance.buildingRegistration : null;
            _shops.Clear();
            foreach (BuildingRegistration shop in save.BuildingRegistrations)
            {
                if (shop == null || !GoingPublic.Mine(shop) || shop == inside || string.IsNullOrEmpty(shop.BusinessName)
                    || shop.businessTypeName == EmptyBusinessType || shop.itemInstances == null)
                {
                    continue;
                }
                BusinessType type = BusinessTypeHelper.GetData(shop);
                if (type != null && type.HasTag(TagRef.Businesstag.allowtheft) && (forced || !BusinessHelper.IsBusinessOpen(shop)))
                {
                    _shops.Add(shop);
                }
            }
            if (_shops.Count == 0)
            {
                return false;
            }
            BuildingRegistration target = _shops[Random.Range(0, _shops.Count)];
            _shops.Clear();

            // Good security keeps most burglars out, and limits what the rest get away with.
            float exposure = 1f - Mathf.Clamp01(target.securityLevelPercentage / 100f);
            if (!forced && Random.value > Mathf.Max(exposure, 0.08f))
            {
                return false;
            }
            float taken = TakeStock(target, Mathf.Lerp(0.25f, 1f, exposure));
            int repair = Crime.Rob(0.02f, RepairLeast, RepairMost, "reallife_transaction_repair");
            _breakInDay = TimeHelper.CurrentDay;
            Law.Bridges.NoteIncident(Law.Bridges.BreakIn);   // a police report can be asked for at a front desk
            _reportShop = target.BusinessName;
            _reportValue = taken;
            ModState.AddModifier(ModState.Robbed);
            var data = new Dictionary<string, string>(2)
            {
                { "business", target.BusinessName }, { "amount", ((float)repair).ToCurrencyFormat() }
            };
            Notifications.Show(NotificationType.Error, repair > 0 ? "reallife_crime_breakin_alarm" : "reallife_crime_breakin_alarm_free", data, 12f);
            News.Report(NewsKind.BreakIn, Vector3.zero, target.BusinessName, repair);
            CivicBridges.NoteCrime();   // out of sight, so city hall cannot have counted it itself (Scripts/Civic)
            BreakIns.Raise(target, Mathf.RoundToInt(taken), repair);   // (for whoever insures it: Crime/BreakIns.cs)
            Crime.Logger?.Info($"Crime: break-in at {target.BusinessName}, security {target.securityLevelPercentage:F0}%, stock {taken:F0}, repair {repair}.");
            return true;
        }

        // Takes stock from the shelves the way the game's own theft does, up to a cap. Returns its cost.
        private float TakeStock(BuildingRegistration shop, float scale)
        {
            _shelves.Clear();
            float onShelves = 0f;
            foreach (ItemInstance item in shop.itemInstances.Values)
            {
                if (item == null || item.ItemCached == null || (item.ItemCached.type & (ItemType.PointOfSale | ItemType.ShowcaseShelf)) == 0
                    || item.cargoInstances == null || item.cargoInstances.Count != 1 || item.cargoInstances[0].amount <= 0)
                {
                    continue;
                }
                _shelves.Add(item);
                onShelves += item.cargoInstances[0].amount * item.cargoInstances[0].pricePerUnit;
            }
            float cap = Mathf.Min(BreakInValueCap, onShelves * BreakInStockShare) * scale;
            float taken = 0f;
            for (int i = 0; i < _shelves.Count && taken < cap; i++)
            {
                ItemInstance item = _shelves[i];
                CargoInstance stock = item.cargoInstances[0];
                if (item.isSecured && Random.value < 0.5f)
                {
                    continue;   // a shelf under a camera is left alone half the time
                }
                int units = Mathf.Max(1, Mathf.FloorToInt(stock.amount * Random.Range(0.15f, 0.5f)));
                if (stock.pricePerUnit > 0f && (cap - taken) < stock.pricePerUnit)
                {
                    continue;   // not even one of these fits under what is left of the cap
                }
                if (stock.pricePerUnit > 0f)
                {
                    units = Mathf.Min(units, Mathf.Max(1, Mathf.FloorToInt((cap - taken) / stock.pricePerUnit)));
                }
                units = Mathf.Min(units, stock.amount);
                stock.amount -= units;
                if (stock.amount <= 0)
                {
                    item.FillUpShowcaseShelfOrPointOfSale();
                    if (stock.amount <= 0)
                    {
                        item.UpdateStockTodoTasks();
                    }
                }
                float cost = units * stock.pricePerUnit;
                shop.stolenItemsCost += cost;
                taken += cost;
                item.OnItemsInCargoUpdated()?.Invoke();
            }
            _shelves.Clear();
            return taken;
        }

        private void MorningReport()
        {
            if (_reportShop == null)
            {
                return;
            }
            var data = new Dictionary<string, string>(2)
            {
                { "business", _reportShop }, { "amount", _reportValue.ToCurrencyFormat() }
            };
            Notifications.Show(NotificationType.Info, _reportValue > 0f ? "reallife_crime_breakin_report" : "reallife_crime_breakin_report_nothing", data, 12f);
            _reportShop = null;
        }

        // ---------------------------------------------------------------- parked car

        private bool CarBreakIn()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || save.VehicleInstances == null)
            {
                return false;
            }
            Vector3 player = PlayerHelper.PlayerController != null ? PlayerHelper.GetPosition() : Vector3.zero;
            VehicleInstance target = null;
            int seen = 0;
            foreach (VehicleInstance vehicle in save.VehicleInstances)
            {
                // Parked on the street (not in a garage), not the one being driven, a real car, and not right beside the player.
                if (vehicle == null || !string.IsNullOrEmpty(vehicle.streetName) || vehicle.id == save.ActiveVehicleId
                    || vehicle.VehicleType == null || vehicle.VehicleType.spawnInPlayerObject || !vehicle.VehicleType.IsMotorVehicle
                    || vehicle.VehicleType.price <= 0f || vehicle.damage >= CarDamageMost)
                {
                    continue;
                }
                VehicleController spawned = VehicleHelper.GetVehicleController(vehicle);
                if (spawned != null && !BuildingManager.IsInsideBuilding && (spawned.transform.position - player).sqrMagnitude < 60f * 60f)
                {
                    continue;
                }
                if (Random.Range(0, ++seen) == 0)
                {
                    target = vehicle;
                }
            }
            if (target == null)
            {
                return false;
            }

            // The game prices a repair at damage x half the car's price: work back from the cap.
            float perUnit = target.VehicleType.price * 0.5f;
            float added = Mathf.Min(0.06f, CarRepairCap / perUnit, CarDamageMost - target.damage);
            if (added <= 0.001f)
            {
                return false;
            }
            float damage = target.damage + added;
            if (VehicleHelper.GetVehicleController(target) is CarController car)
            {
                car.SetDamage(damage);
            }
            else
            {
                target.damage = damage;
            }
            GlobalEvents.onVehicleVariablesChanged?.Invoke();
            _carDay = TimeHelper.CurrentDay;
            Law.Bridges.NoteIncident(Law.Bridges.CarBreakIn);
            var data = new Dictionary<string, string>(2)
            {
                { "vehicle", target.vehicleTypeName.GetLocalization() }, { "amount", (added * perUnit).ToCurrencyFormat() }
            };
            Notifications.Show(NotificationType.Error, Random.value < 0.5f ? "reallife_crime_carbreak_1" : "reallife_crime_carbreak_2", data, 12f);
            News.Report(NewsKind.CarBreakIn, new Vector3(target.position.x, 0f, target.position.z));
            CivicBridges.NoteCrime();
            Crime.Logger?.Info(FormattableString.Invariant($"Crime: parked {target.vehicleTypeName} broken into, damage +{added:F3}."));
            return true;
        }

        // ---------------------------------------------------------------- scam call

        private bool ScamCall()
        {
            if (HudConfirm.isOpen || !LifeEvents.IsSafeForEvent() || Crime.Cash < ScamLeast)
            {
                return false;
            }
            int which = Random.Range(0, ScamBodies.Length);
            if (which == _lastScam)
            {
                which = (which + 1) % ScamBodies.Length;
            }
            _lastScam = which;
            _scamDay = TimeHelper.CurrentDay;
            int asked = ++_scamAsked;
            GameConfirm.Show("reallife_crime_scam_header", ScamBodies[which],
                () => Answer(asked, true), () => Answer(asked, false),
                "reallife_crime_scam_yes", "reallife_crime_scam_no", allowConfirmationSkip: false);
            return true;
        }

        // Arrives later, from the game's UI: the save may be gone by then.
        private void Answer(int asked, bool went)
        {
            if (asked != _scamAsked || !ModState.Loaded || SaveGameManager.Current == null)
            {
                return;
            }
            try
            {
                if (!went)
                {
                    Crime.Show(NotificationType.Info, Random.value < 0.5f ? "reallife_crime_scam_hungup_1" : "reallife_crime_scam_hungup_2");
                }
                else if (Random.value < 0.7f)
                {
                    int lost = Crime.Rob(0.08f, ScamLossLeast, ScamLossMost, "reallife_transaction_scam");
                    ModState.AddModifier(ModState.Robbed);
                    Crime.ShowAmount(NotificationType.Error, "reallife_crime_scam_lost", lost);
                }
                else
                {
                    Crime.Show(NotificationType.Success, "reallife_crime_scam_lucky");
                }
            }
            catch (Exception e)
            {
                Crime.Fault(e);
            }
        }
    }
}
