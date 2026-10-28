using System;
using BAModAPI;
using Dialogs;
using Entities;
using Helpers;
using Localizor;
using Streets;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The two phone contacts of car ownership: "Garage", a page per car the player owns (fuel, condition, service, tyres,
    /// brakes, insurance, registration) where a car is insured or its policy ended; and "Roadside Assistance", which sends a
    /// mechanic, a can of fuel, or the game's own tow to the nearest repair shop. The conversations are the game's dialogue panel.
    /// </summary>
    public static class CarPhone
    {
        public const string GarageId = "reallife-garage";
        public const string RoadsideId = "reallife-roadside";
        private const float RoadsideReach = 30f;   // metres from the player to a car a roadside call can be for

        public static void Setup()
        {
            if (!Settings.CarOwnership || !Settings.PhoneContacts)
            {
                return;
            }
            try
            {
                ContactPictures.Add(GarageId);   // the game's own logo shapes (Phone/Contacts)
                ContactPictures.Add(RoadsideId);
                Register(GarageId, "reallife_contact_garage_desc", "reallife_garage_calldialogtype", () => new GarageDialog(), "reallife_text_garage_welcome");
                Register(RoadsideId, "reallife_contact_roadside_desc", "reallife_roadside_calldialogtype", () => new RoadsideDialog(), "reallife_text_roadside_welcome");
            }
            catch (Exception e)
            {
                CarOwnership.Logger?.Error(e);
                CarOwnership.Logger?.Warn("Car ownership: its phone contacts could not be added; the garage page is still on the options page's test button.");
            }
        }

        private static void Register(string id, string descriptionKey, string dialogTypeName, Func<Dialog> dialog, string welcomeKey)
        {
            Contact contact = Contact.GetContact(id, ContactCategoryName.General, descriptionKey, null, false, true);
            var dialogType = (CallDialogType)ModEnumHash.GetSafeHash(dialogTypeName);
            contact.callDialogTypeOverride = dialogType;   // not saved by the game, so set on every load
            CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(dialog));
            if (contact.messagesQueue == null || contact.messagesQueue.Count == 0)
            {
                contact.SendMessage(new TextMessage(welcomeKey), notify: false);
            }
        }

        /// <summary>The Garage page without the phone: the game's own dialogue panel, as its counters open one.</summary>
        public static void OpenGarage()
        {
            var dialogType = (CallDialogType)ModEnumHash.GetSafeHash("reallife_garage_calldialogtype");
            CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(() => new GarageDialog()));
            InstanceBehavior<UI.UIs>.Instance.playerHUD.dialogUI.ShowDialog(dialogType, NavigationBlocker.SpecialEmployeeDialog);
        }

        /// <summary>How many motor vehicles the save says the player owns.</summary>
        public static int OwnedCars(GameInstance save)
        {
            int count = 0;
            for (int i = 0; save != null && save.VehicleInstances != null && i < save.VehicleInstances.Count; i++)
            {
                count += Runs(save.VehicleInstances[i]) ? 1 : 0;
            }
            return count;
        }

        // A motor vehicle of the player's to run. A dealership's stock car is the dealership's to sell and has no Garage page.
        private static bool Runs(VehicleInstance car)
        {
            return car != null && car.VehicleType != null && car.VehicleType.IsMotorVehicle && !Dealerships.Stock(car) && !Family.PartnerCar.Is(car);
        }

        /// <summary>The n-th motor vehicle the player owns (wrapping round), or null.</summary>
        public static VehicleInstance Owned(GameInstance save, int index)
        {
            int count = OwnedCars(save);
            if (count == 0)
            {
                return null;
            }
            index = ((index % count) + count) % count;
            for (int i = 0; i < save.VehicleInstances.Count; i++)
            {
                VehicleInstance car = save.VehicleInstances[i];
                if (Runs(car) && index-- == 0)
                {
                    return car;
                }
            }
            return null;
        }

        /// <summary>The player's own car nearest them in the street, within reach of a roadside call; null if none.</summary>
        public static VehicleController NearestOwned(GameInstance save)
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null || save == null || save.VehicleInstances == null)
            {
                return null;
            }
            VehicleController nearest = null;
            float best = RoadsideReach * RoadsideReach;
            foreach (VehicleController vehicle in VehicleHelper.AllPlayerVehicles)
            {
                if (vehicle == null || !vehicle.isActiveAndEnabled || vehicle.vehicleInstance == null || vehicle.vehicleType == null || !vehicle.vehicleType.IsMotorVehicle
                    || !save.VehicleInstances.Contains(vehicle.vehicleInstance) || Dealerships.Stock(vehicle.vehicleInstance))
                {
                    continue;
                }
                float sqr = (vehicle.transform.position - player.transform.position).sqrMagnitude;
                if (sqr < best)
                {
                    best = sqr;
                    nearest = vehicle;
                }
            }
            return nearest;
        }
    }

    /// <summary>The Garage page: one car at a time, with what can be done about its insurance.</summary>
    public sealed class GarageDialog : Dialog
    {
        public GarageDialog()
        {
            npcNameKey = CarPhone.GarageId;
            DialogController.current.ShowEntry(Guard.Entry(Settings.CarOwnership ? Page(0) : PhoneContacts.Closing("reallife_garage_off")));
        }

        private static DialogEntry Page(int index)
        {
            GameInstance save = SaveGameManager.Current;
            VehicleInstance car = CarPhone.Owned(save, index);
            if (car == null)
            {
                return PhoneContacts.Closing("reallife_garage_none");
            }
            int today = TimeHelper.CurrentDay;
            CarRecord record = CarGarage.For(car.id, today);
            float price = car.VehicleType.price;
            var traits = new CarTraits(true, car.VehicleType.spawnInPlayerObject, car.VehicleType.maxSpeed, car.VehicleType.maxCargoCapacity, car.VehicleType.isLuxuryCar, 1400f);
            float every = CarRules.ServiceEvery(CarCatalogue.For(car.vehicleTypeName, traits).Kind);
            float untilService = every - (record.Km - record.ServicedKm);
            bool insured = record.InsuredUntil >= today;
            float premium = Mathf.Round(CarRules.PremiumOf(price, record.Claims, GarageBridges.InsuranceOf(car.id)));   // as CarOwnership charges it
            int registration = CarOwnership.RegistrationLeft(record);
            int count = CarPhone.OwnedCars(save);
            var entry = new DialogEntry
            {
                messageData = "reallife_garage_page".Localize(new
                {
                    car = car.vehicleTypeName.GetLocalization(),
                    number = (((index % count) + count) % count + 1).ToString(),
                    count = count.ToString(),
                    fuel = car.VehicleType.maxFuel > 0f ? Mathf.RoundToInt(Mathf.Clamp01(car.fuel / car.VehicleType.maxFuel) * 100f).ToString() : "0",
                    condition = Mathf.RoundToInt((1f - Mathf.Clamp01(car.damage)) * 100f).ToString(),
                    km = record.Km.ToString("F1"),
                    service = (record.Broken ? "reallife_garage_service_broken" : untilService < 0f ? "reallife_garage_service_overdue" : "reallife_garage_service_in").GetLocalization()
                        .Replace("{km}", Mathf.Abs(untilService).ToString("F0")),
                    tyres = Mathf.RoundToInt(record.Tyres * 100f).ToString(),
                    brakes = Mathf.RoundToInt(record.Brakes * 100f).ToString(),
                    insurance = (insured ? "reallife_garage_insured" : "reallife_garage_uninsured").GetLocalization()
                        .Replace("{days}", Mathf.Max(0, record.InsuredUntil - today + 1).ToString()).Replace("{amount}", TransitText.Money(premium)),
                    registration = (registration < 0 ? "reallife_garage_registration_overdue" : "reallife_garage_registration_left").GetLocalization()
                        .Replace("{days}", Mathf.Abs(registration).ToString())
                }),
                OnCancel = PhoneContacts.HangUp
            };
            if (insured)
            {
                entry.ConfirmTextOverride = "reallife_garage_cancel_policy".Localize();
                entry.OnConfirm = () =>
                {
                    record.InsuredUntil = 0;
                    return PhoneContacts.Closing("reallife_garage_policy_cancelled");
                };
            }
            else
            {
                entry.ConfirmTextOverride = "reallife_garage_insure".Localize(new { amount = TransitText.Money(premium) });
                entry.OnConfirm = () =>
                {
                    if (!RealLifeOverhaul.Economy.BizBooks.Book(-premium, new TransactionInfo("reallife_transaction_car_insurance"), null, null, force: false, showNotification: true))
                    {
                        return PhoneContacts.Closing("reallife_garage_no_money");
                    }
                    record.InsuredUntil = TimeHelper.CurrentDay + CarRules.PolicyDays - 1;
                    return PhoneContacts.Closing("reallife_garage_policy_bought");
                };
            }
            if (count > 1)
            {
                entry.SecondOptionTextOverride = "reallife_garage_next";
                entry.OnSecondOption = () => Page(index + 1);
            }
            return entry;
        }
    }

    /// <summary>Roadside Assistance: what it can do for the car the player is at.</summary>
    public sealed class RoadsideDialog : Dialog
    {
        public RoadsideDialog()
        {
            npcNameKey = CarPhone.RoadsideId;
            DialogController.current.ShowEntry(Guard.Entry(First()));
        }

        private static DialogEntry First()
        {
            CarOwnership owner = CarOwnership.Instance;
            GameInstance save = SaveGameManager.Current;
            if (owner == null || !Settings.CarOwnership)
            {
                return PhoneContacts.Closing("reallife_garage_off");
            }
            if (owner.Roadside.Active)
            {
                return PhoneContacts.Closing("reallife_roadside_on_way");
            }
            VehicleController vehicle = CarPhone.NearestOwned(save);
            var car = vehicle as CarController;
            if (car == null)
            {
                return PhoneContacts.Closing("reallife_roadside_no_car");
            }
            CarRecord record = CarGarage.For(car.vehicleInstance.id, TimeHelper.CurrentDay);
            bool dry = car.vehicleType.maxFuel > 0f && car.vehicleInstance.fuel <= car.vehicleType.maxFuel * 0.05f && !save.gameVariables.disableVehicleFuel;
            RoadsideJob job = record.Broken ? RoadsideJob.Breakdown : dry ? RoadsideJob.Fuel : RoadsideJob.None;
            string name = car.vehicleInstance.vehicleTypeName.GetLocalization();
            var entry = new DialogEntry { OnCancel = PhoneContacts.HangUp };
            if (job == RoadsideJob.None)
            {
                entry.messageData = "reallife_roadside_offer_tow".Localize(new { car = name, amount = TransitText.Money(CarRules.TowFee) });
                entry.ConfirmTextOverride = "reallife_roadside_tow".Localize();
                entry.OnConfirm = () => Tow(car);
                return entry;
            }
            float fee = CarRoadside.Fee(job, record);
            entry.messageData = (job == RoadsideJob.Breakdown ? "reallife_roadside_offer_mechanic" : "reallife_roadside_offer_fuel").Localize(new
            {
                car = name, amount = TransitText.Money(fee), tow = TransitText.Money(CarRules.TowFee)
            });
            entry.ConfirmTextOverride = "reallife_roadside_send".Localize();
            entry.OnConfirm = () => CarOwnership.Instance != null && CarOwnership.Instance.Roadside.Call(job, car, record, Time.time)
                ? PhoneContacts.Closing("reallife_roadside_sent")
                : PhoneContacts.Closing("reallife_roadside_failed");
            entry.SecondOptionTextOverride = "reallife_roadside_tow";
            entry.OnSecondOption = () => Tow(car);
            return entry;
        }

        // The game's own tow to a free bay at the nearest repair shop.
        private static DialogEntry Tow(CarController car)
        {
            try
            {
                if (car == null || car.vehicleInstance == null || PlayerHelper.IsUsingVehicle)
                {
                    return PhoneContacts.Closing("reallife_roadside_tow_get_out");
                }
                if (SaveGameManager.Current.Money < CarRules.TowFee)
                {
                    return PhoneContacts.Closing("reallife_garage_no_money");
                }
                Address shop = VehicleHelper.TowVehicle(car.vehicleInstance, "ba:towdestination_autorepairshop");
                if (shop == null || shop.IsUndefined())
                {
                    return PhoneContacts.Closing("reallife_roadside_tow_none");
                }
                RealLifeOverhaul.Economy.BizBooks.Book(-CarRules.TowFee, new TransactionInfo("reallife_transaction_towing"), null, null, force: true);
                CarOwnership.Logger?.Info("Roadside: the car was towed to the repair shop at " + shop.ToFormattedString() + ".");
                return PhoneContacts.Closing("reallife_roadside_towed", new { address = shop.ToFormattedString(), amount = TransitText.Money(CarRules.TowFee) });
            }
            catch (Exception e)
            {
                CarOwnership.Logger?.Error(e);
                return PhoneContacts.Closing("reallife_roadside_failed");
            }
        }
    }
}
