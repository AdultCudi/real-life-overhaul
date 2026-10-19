using System;
using System.Collections.Generic;
using Dialogs;
using BAModAPI;
using Entities;
using Extensions;
using Helpers;
using Streets;
using UnityEngine;
using Vehicles.VehicleTypes;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// A car of the partner's own (PartnerExpectRules): one of the player's cars handed over, the dearest they do not drive
    /// first and another once if asked, or a new one bought for them at the game's own price when the player has none to
    /// spare. A car handed over stays in the save, the player's on paper, so that a save without the mod still has it, but
    /// is kept out of the city as a car out on hire is (EstateGame), and every part of the mod that looks after the
    /// player's cars leaves it alone (<see cref="Is"/>). Taken back, the game's tow service leaves it at a repair shop.
    /// Cost: nothing per frame; a look at the player's cars once a game hour and when a conversation offers the car.
    /// </summary>
    internal static class PartnerCar
    {
        private const string TowTo = "ba:towdestination_autorepairshop";

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

        /// <summary>Once a game hour: their car stays out of the city, also after a load brought it back.</summary>
        public static void Hourly()
        {
            VehicleController controller = Has ? Economy.EstateGame.Controller(FamilyState.S.PCarId) : null;
            if (controller != null && !controller.controlledByPlayer)
            {
                Economy.EstateGame.Hide(controller);
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
            PlayerController player = PlayerHelper.PlayerController;
            v.position = player != null ? player.transform.position : Vector3.zero;   // (never spawned while theirs: a towed car is placed by the tow)
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

        /// <summary>Asking for the car back: it hurts, and the game's tow service leaves it at a repair shop for the player.</summary>
        public static DialogEntry TakeBack()
        {
            return FamilyTalk.Either(FamilyText.Say("reallife_family_car_back_ask", Args.Us), "reallife_family_car_back_yes", () =>
            {
                FamilySave s = FamilyState.S;
                VehicleInstance v = Economy.EstateGame.Vehicle(s.PCarId);
                Address shop = v != null ? VehicleHelper.TowVehicle(v, TowTo) : null;
                if (shop == null || shop.IsUndefined())
                {
                    return FamilyTalk.Close("reallife_family_car_cannot");
                }
                s.PCarId = null;
                s.PCarBackDay = TimeHelper.CurrentDay;
                FamilyState.Harmony(PartnerExpectRules.CarBackHarmony);
                Partner.Tell(FamilyEvent.BrokenPromise, PartnerExpectRules.CarBackAffection);
                FamilyDirector.Logger?.Info("Family: the player took the car back from " + Partner.Name + "; towed to " + shop.ToFormattedString() + ".");
                return FamilyTalk.Close("reallife_family_car_back_done", new Args { Name = Partner.Name, Place = shop.ToFormattedString() });
            }, "reallife_family_car_back_no", () => FamilyTalk.Close("reallife_family_car_back_kept"));
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
            return PartnerExpectRules.LevelOf(cash, Prices.Level);
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
