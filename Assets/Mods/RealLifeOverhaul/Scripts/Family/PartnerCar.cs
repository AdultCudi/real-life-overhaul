using System;
using System.Collections.Generic;
using BAModAPI;
using Dialogs;
using Entities;
using Extensions;
using Helpers;
using Streets;
using UI.Notification;
using UnityEngine;
using Vehicles.VehicleTypes;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// A car of the partner's own (PartnerExpectRules): one of the player's cars handed over, the dearest they do not drive
    /// first and another once if asked, or a new one bought for them at the game's own price when the player has none to
    /// spare. A car handed over stays in the save, the player's on paper, so that a save without the mod still has it, but
    /// is kept out of the city as a car out on hire is (EstateGame), also at once after a load brings it back, and every
    /// part of the mod that looks after the player's cars leaves it alone (<see cref="Is"/>). While it is theirs the game
    /// bills no parking for it: the game charges every saved car by its parking state each hour, whether or not it stands
    /// in the city, so it has none (Off). A car bought for them is saved on the street outside, never at an interior's
    /// coordinates. Taken back, it is made again at the nearest free repair bay, as the game's tow would leave it. With the
    /// partner's expectations switched off it is the player's again at once, as without the feature: in the city where a
    /// load left it, else made again at a repair bay as when taken back, with a notice (Release). Cost: nothing per frame;
    /// a look at the player's cars once a game hour, after a load and when a conversation offers the car.
    /// </summary>
    internal static class PartnerCar
    {
        private static bool _releaseFailed;   // making the car again failed once (Release): not tried again this session

        /// <summary>The partner has a car of their own that is still in the save.</summary>
        public static bool Has => !string.IsNullOrEmpty(FamilyState.S.PCarId) && Economy.EstateGame.Vehicle(FamilyState.S.PCarId) != null;

        /// <summary>True for the car handed to the partner: not the player's to drive, insure, register, hire out or borrow against.</summary>
        public static bool Is(VehicleInstance vehicle)
        {
            string id = FamilyState.Loaded ? FamilyState.S.PCarId : null;
            return vehicle != null && !string.IsNullOrEmpty(id) && vehicle.id == id;
        }

        /// <summary>A car can be offered from a family conversation: they asked for one and have none yet.</summary>
        public static bool CanOffer => Settings.FamilyExpect && !GoingPublic.InSession && FamilyState.Together && FamilyState.S.LivesIn && FamilyState.S.PCarWishDay > 0 && !Has;

        /// <summary>They have one, and it can be asked back.</summary>
        public static bool CanTakeBack => Settings.FamilyExpect && !GoingPublic.InSession && FamilyState.Together && Has;

        /// <summary>
        /// Once a game hour and after a load: their car stays out of the city, also after a load brought it back, with no
        /// parking billed; with the switch off it is the player's again.
        /// </summary>
        public static void Hourly()
        {
            VehicleInstance v = Has ? Economy.EstateGame.Vehicle(FamilyState.S.PCarId) : null;
            if (v == null)
            {
                return;
            }
            VehicleController controller = Economy.EstateGame.Controller(v.id);
            if (!Settings.FamilyExpect)
            {
                Release(v, controller);
                return;
            }
            Off(v);
            if (controller != null && !controller.controlledByPlayer)
            {
                Economy.EstateGame.Hide(controller);
            }
        }

        // Out of the city and theirs: no parking state, so the game's hour bills it nothing (ParkingSimulator.RunHourly charges a
        // legally parked car whether or not it has an object, and tickets only one it can see).
        private static void Off(VehicleInstance v)
        {
            v.parkingState = ParkingState.NotAvailable;
        }

        // Back with the player: parked as the game parks a car it makes again (the private driver's PrivateDriverServiceDialog).
        private static void Back(VehicleInstance v)
        {
            v.parkingState = ParkingState.Legal;
        }

        // The switch is off: the car is the player's again, as without the feature. A load has made it in the city already,
        // where it was saved; else it is made again at the nearest free repair bay, as when it is asked back, and the player told
        // where. In a Going Public session a car made again would not be the same car to the other players (EstateGame.MayHire),
        // and with no bay free there is nowhere to put it: it waits, billed nothing, for the next game hour.
        private static void Release(VehicleInstance v, VehicleController controller)
        {
            string name = Partner.Name, shop = null;
            if (controller == null)
            {
                if (GoingPublic.InSession || _releaseFailed)
                {
                    Off(v);
                    return;
                }
                Address at;
                try
                {
                    at = Park(v);
                }
                catch (Exception e)
                {
                    _releaseFailed = true;   // tried once a session: the car stays as it is, out of the city and billed nothing
                    FamilyDirector.Logger?.Error(e);
                    Off(v);
                    return;
                }
                if (at == null || at.IsUndefined())
                {
                    Off(v);
                    return;
                }
                shop = at.ToFormattedString();
            }
            FamilyState.S.PCarId = null;
            Back(v);
            FamilyDirector.Logger?.Info("Family: the partner's expectations are off, so the car given to " + name + " is the player's again"
                + (shop != null ? ", left at " + shop + "." : ", where it stands."));
            if (shop != null)
            {
                FamilyText.Toast(NotificationType.Info, "reallife_family_car_released", new Args { Name = name, Text = Model(v), Place = shop }, 12f);
            }
        }

        /// <summary>The conversation about a car: the dearest the player does not drive, then once another, or a new one bought.</summary>
        public static DialogEntry Offer()
        {
            List<VehicleController> spare = Spare();
            if (spare.Count == 0)
            {
                return OfferNew();
            }
            return Pick(spare, 0);
        }

        private static DialogEntry Pick(List<VehicleController> spare, int at)
        {
            VehicleController car = spare[at];
            Args a = new Args { Name = Partner.Name, Text = Model(car.vehicleInstance) };
            bool more = at == 0 && spare.Count > 1;
            return FamilyTalk.Either(FamilyText.Say(at == 0 ? "reallife_family_car_pick" : "reallife_family_car_pick_other", a), "reallife_family_car_give", () => Give(car),
                more ? "reallife_family_car_other" : "reallife_family_car_not_now", more ? () => Pick(spare, 1) : (Func<DialogEntry>)NotNow);
        }

        private static DialogEntry OfferNew()
        {
            string type = NewModel(Level());
            VehicleType t = type != null ? VehicleTypeHelper.GetVehicleType(type) : null;
            if (t == null)
            {
                return FamilyTalk.Close("reallife_family_car_none");
            }
            int price = Mathf.RoundToInt(t.price);
            Args a = new Args { Name = Partner.Name, Text = Model(type), Amount = FamilyText.Money(price) };
            return FamilyTalk.Either(FamilyText.Say("reallife_family_car_buy_ask", a), "reallife_family_car_buy", () => Buy(type, price), "reallife_family_car_not_now", NotNow);
        }

        private static DialogEntry NotNow()
        {
            PartnerVoice.Answered(PartnerVoice.Car);
            return FamilyTalk.Close("reallife_family_car_later");
        }

        private static DialogEntry Give(VehicleController car)
        {
            if (car == null || car.vehicleInstance == null || car.controlledByPlayer)
            {
                return FamilyTalk.Close("reallife_family_car_cannot");
            }
            Took(car.vehicleInstance.id);
            Off(car.vehicleInstance);
            Economy.EstateGame.Hide(car);
            FamilyDirector.Logger?.Info("Family: the player gave " + Partner.Name + " their " + Model(car.vehicleInstance) + ".");
            return FamilyTalk.Close("reallife_family_car_given", new Args { Name = Partner.Name, Text = Model(car.vehicleInstance) });
        }

        private static DialogEntry Buy(string type, int price)
        {
            GameInstance save = SaveGameManager.Current;
            VehicleType t = VehicleTypeHelper.GetVehicleType(type);
            if (save == null || t == null)
            {
                return FamilyTalk.Close("reallife_family_car_cannot");
            }
            if (!FamilyTalk.Pay(price, "reallife_transaction_family_partner_car"))
            {
                return FamilyTalk.Close("reallife_family_car_short");
            }
            FamilyState.MoneyOut(price, 4);
            var v = new VehicleInstance(type)
            {
                id = UuidHelper.GenerateBase64Uuid(),
                fuel = t.maxFuel,
                dirtiness = 0f,
                damage = 0f
            };
            v.position = OnStreet();   // the game makes it there on a load (and the mod takes it out of the city again)
            v.rotation = Quaternion.identity;   // (the save's default is no rotation at all)
            Off(v);
            save.VehicleInstances.Add(v);
            Took(v.id);
            FamilyDirector.Logger?.Info("Family: the player bought " + Partner.Name + " a " + type + " for " + price + ".");
            return FamilyTalk.Close("reallife_family_car_bought", new Args { Name = Partner.Name, Text = Model(type) });
        }

        private static void Took(string id)
        {
            FamilySave s = FamilyState.S;
            PartnerVoice.Answered(PartnerVoice.Car);
            s.PCarId = id;
            s.PCarWishDay = 0;
            FamilyState.Harmony(PartnerExpectRules.WishMet);
            Partner.Tell(FamilyEvent.SharedTime, PartnerExpectRules.AffectionCar);
        }

        /// <summary>Asking for the car back: it hurts, and it is left at the nearest repair shop for the player, as the game's tow would (Park).</summary>
        public static DialogEntry TakeBack()
        {
            return FamilyTalk.Either(FamilyText.Say("reallife_family_car_back_ask", Args.Us), "reallife_family_car_back_yes", () =>
            {
                FamilySave s = FamilyState.S;
                VehicleInstance v = Economy.EstateGame.Vehicle(s.PCarId);
                Address shop = v != null ? Park(v) : null;
                if (shop == null || shop.IsUndefined())
                {
                    return FamilyTalk.Close("reallife_family_car_cannot");
                }
                s.PCarId = null;
                Back(v);
                s.PCarBackDay = TimeHelper.CurrentDay;
                FamilyState.Harmony(PartnerExpectRules.CarBackHarmony);
                Partner.Tell(FamilyEvent.BrokenPromise, PartnerExpectRules.CarBackAffection);
                FamilyDirector.Logger?.Info("Family: the player took the car back from " + Partner.Name + "; towed to " + shop.ToFormattedString() + ".");
                return FamilyTalk.Close("reallife_family_car_back_done", new Args { Name = Partner.Name, Place = shop.ToFormattedString() });
            }, "reallife_family_car_back_no", () => FamilyTalk.Close("reallife_family_car_back_kept"));
        }

        // A car of the partner's has no object in the city (EstateGame.Hide), so the game's tow, which moves the object, cannot
        // take it: it is made again from its save entry at the nearest free repair bay, as the tow would leave it, without
        // taking the player out of the home they are talking in. Null, with the car left theirs, when no bay is free.
        private static Address Park(VehicleInstance v)
        {
            GameInstance save = SaveGameManager.Current;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (save == null || city == null || v.VehicleType == null)
            {
                return null;
            }
            Vector3 from = OnStreet();
            bool truck = v.VehicleType.HasTag(BigAmbitions.Tags.TagRef.Vehicletag.istruck);
            CityBuildingController best = null;
            GasStationTrigger bay = null;
            float near = float.MaxValue;
            var hit = new Collider[1];
            foreach (CityBuildingController shop in GameDlc.Open(city))
            {
                if (shop == null || shop.building == null || shop.building.BuildingType != "ba:buildingtype_special" || shop.entranceDoors == null || shop.entranceDoors.Length == 0
                    || !(shop.building.SpecialService?.settings is Buildings.GasStationSettings gs) || gs.truckOnly != truck)
                {
                    continue;
                }
                float d = (shop.entranceDoors[0].doorTransform.position - from).sqrMagnitude;
                GasStationController station = shop.GetComponent<GasStationController>();
                if (d >= near || station == null || station.repairStationTriggers == null)
                {
                    continue;
                }
                foreach (GasStationTrigger t in station.repairStationTriggers)
                {
                    Bounds b = t.stationCollider.bounds;
                    if (Physics.OverlapBoxNonAlloc(b.center, b.extents, hit, Quaternion.identity, LayerHelper.vehicleAndHumanConflictMask) == 0)
                    {
                        best = shop;
                        bay = t;
                        near = d;
                        break;
                    }
                }
            }
            if (bay == null)
            {
                return null;
            }
            save.VehicleInstances.Remove(v);   // CreateAndSpawnVehicle adds it to the list again
            try
            {
                VehicleController car = VehicleHelper.CreateAndSpawnVehicle(v, bay.transform.position, Quaternion.LookRotation(bay.transform.up));
                VehicleHelper.TeleportVehicleToGround(car, bay.transform.position, Quaternion.LookRotation(bay.transform.up));
                car.SavePosition();
            }
            catch (Exception)
            {
                if (!save.VehicleInstances.Contains(v))
                {
                    save.VehicleInstances.Add(v);   // whatever happened, the car stays the player's
                }
                throw;
            }
            v.SetStreetData(string.Empty, 0);
            return best.building.Address;
        }

        // Where the player is in the city: indoors, the building's door, as the game's tow measures from (an interior's
        // coordinates are not the city's); else where they stand.
        private static Vector3 OnStreet()
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            if (BuildingManager.IsInsideBuilding && manager != null && manager.cityBuildingController != null
                && manager.cityBuildingController.entranceDoors != null && manager.cityBuildingController.entranceDoors.Length > 0)
            {
                return manager.cityBuildingController.entranceDoors[0].doorTransform.position;
            }
            PlayerController player = PlayerHelper.PlayerController;
            return player != null ? player.transform.position : Vector3.zero;
        }

        // The player's cars they could hand over: theirs, a motor car, not a dealer's stock, not the one they are in; dearest first.
        private static List<VehicleController> Spare()
        {
            var spare = new List<VehicleController>();
            List<VehicleController> all = VehicleHelper.AllPlayerVehicles;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                VehicleController v = all[i];
                if (v is CarController && v.vehicleInstance != null && v.vehicleType != null && v.vehicleType.IsMotorVehicle && !v.controlledByPlayer
                    && GoingPublic.MineVehicle(v) && !Dealerships.Stock(v.vehicleInstance))
                {
                    spare.Add(v);
                }
            }
            spare.Sort((a, b) => b.vehicleType.price.CompareTo(a.vehicleType.price));
            return spare;
        }

        // The model a new car for them is, by the level of the player's cash: from the cars on sale (DealerGame's list), dearer the richer.
        private static string NewModel(int level)
        {
            var models = new List<Economy.Dealership.MarketModel>();
            Economy.Dealership.DealerGame.Models(models, new List<string>());
            if (models.Count == 0)
            {
                return null;
            }
            models.Sort((a, b) => a.Price.CompareTo(b.Price));
            return models[PartnerExpectRules.ModelAt(level, models.Count)].Type;
        }

        private static int Level()
        {
            float cash = SaveGameManager.Current != null ? SaveGameManager.Current.Money : 0f;
            return PartnerExpectRules.LevelOf(cash, FamilyState.IncomeHeld, Prices.Level);
        }

        private static string Model(VehicleInstance v) => v != null ? Model(v.vehicleTypeName) : string.Empty;

        private static string Model(string type) => Economy.EstateGame.CarName(type);

        /// <summary>Somebody new, or nobody: no car of theirs (a car handed over stays in the save, the player's again).</summary>
        public static void Reset(FamilySave s)
        {
            s.PCarId = null;
            s.PCarBackDay = 0;
        }
    }
}
