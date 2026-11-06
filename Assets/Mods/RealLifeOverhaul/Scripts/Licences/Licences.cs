using System;
using System.Collections.Generic;
using BAModAPI;
using Extensions;
using Localizor;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>What driving licences ask of a law feature, when one is wired: the criminal record and custody.</summary>
    public interface ILicenceLawBridge
    {
        /// <summary><paramref name="offenceId"/> is one of <see cref="LicenceOffences"/>, or "hit_and_run".</summary>
        void Record(string offenceId, bool arrested);

        /// <summary>Takes the player into custody. False if it could not: the fine alone then settles it.</summary>
        bool Custody(int hours);
    }

    /// <summary>The offence ids this feature reports to a law bridge.</summary>
    public static class LicenceOffences
    {
        public const string Unlicensed = "driving_unlicensed";
        public const string Suspended = "driving_suspended";
    }

    /// <summary>Without a law feature: nothing is recorded, and custody is the mod's own time skip.</summary>
    public sealed class DefaultLaw : ILicenceLawBridge
    {
        public void Record(string offenceId, bool arrested)
        {
        }

        public bool Custody(int hours)
        {
            return PoliceAttention.TakeIntoCustody(hours);
        }
    }

    /// <summary>The one door to driving licences for the rest of the mod. Safe at any time, before a save loads too.</summary>
    public static class Licences
    {
        private const string FeeCategory = "ba:transactioncategory_licensingfees";

        private static readonly DefaultLaw Fallback = new DefaultLaw();
        private static ILicenceLawBridge _law;

        public static IModLogger Logger;

        /// <summary>The law feature's side; never null. Set to null for the default.</summary>
        public static ILicenceLawBridge Law
        {
            get => _law ?? Fallback;
            set => _law = value;
        }

        public static class Driving
        {
            /// <summary>False when the requirement is off: everyone may drive anything, as in the base game.</summary>
            public static bool Required => Settings.DrivingLicence;

            /// <summary>Always true with the requirement off or before the save is ready.</summary>
            public static bool Has(LicenceClass kind)
            {
                return !Required || !LicenceState.Loaded || LicenceState.Covered(kind, TimeHelper.CurrentDay);
            }

            /// <summary>Whether the heaviest trucks need a class of their own; off, the commercial licence covers them as before.</summary>
            public static bool HeavyClass => Settings.HeavyLicence;

            public static bool Suspended => LicenceState.Loaded && LicenceState.SuspendedUntil > TimeHelper.CurrentDay;

            /// <summary>Set by Wiring from the Garage: 0 neither, 1 a bicycle, 2 a motorbike. Null: always 0.</summary>
            public static Func<VehicleController, int> TwoWheelerOf;

            /// <summary>Set by Wiring: whether the save owns a motorbike, 1 yes, 0 no, -1 not known yet.</summary>
            public static Func<int> OwnsMotorbike;

            /// <summary>Which licence a vehicle needs; false for what needs none, such as bicycles, scooters and trolleys.</summary>
            public static bool ClassOf(VehicleController vehicle, out LicenceClass kind)
            {
                kind = LicenceClass.Car;
                int wheels = TwoWheeler(vehicle);
                if (wheels != 0)
                {
                    kind = LicenceClass.Motorcycle;
                    return wheels == 2;
                }
                Vehicles.VehicleTypes.VehicleType type = vehicle != null ? vehicle.vehicleType : null;
                if (type == null || !type.IsMotorVehicle || type.spawnInPlayerObject)
                {
                    return false;
                }
                Rigidbody body = vehicle.GetComponent<Rigidbody>();
                var traits = new CarTraits(true, false, type.maxSpeed, type.maxCargoCapacity, type.isLuxuryCar, body != null ? body.mass : 1400f);
                CarKind car = CarCatalogue.For(type.vehicleTypeName, traits).Kind;
                if (car == CarKind.Untouched)
                {
                    return false;
                }
                kind = car == CarKind.Van || car == CarKind.Truck ? LicenceClass.Commercial : LicenceClass.Car;
                if (car == CarKind.Truck && HeavyClass && HeavyRules.IsHeavy(type.vehicleTypeName, true, type.maxSpeed))
                {
                    kind = LicenceClass.Heavy;
                }
                return true;
            }

            private static int TwoWheeler(VehicleController vehicle)
            {
                try
                {
                    return vehicle != null && TwoWheelerOf != null ? TwoWheelerOf(vehicle) : 0;
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                    return 0;
                }
            }
        }

        /// <summary>Takes a fee only if it can be paid in full. False, with nothing taken, if not.</summary>
        internal static bool Pay(int amount, string transactionKey)
        {
            return amount <= 0 || (SaveGameManager.Current != null
                && RealLifeOverhaul.Economy.BizBooks.Book(-amount, new TransactionInfo(transactionKey, FeeCategory), null, null, force: false));
        }

        /// <summary>Takes a fine or fee whatever the balance: the game allows the red.</summary>
        internal static void Charge(int amount, string transactionKey)
        {
            if (amount > 0 && SaveGameManager.Current != null)
            {
                RealLifeOverhaul.Economy.BizBooks.Book(-amount, new TransactionInfo(transactionKey), null, null, force: true);
            }
        }

        internal static void Give(int amount, string transactionKey)
        {
            if (amount > 0 && SaveGameManager.Current != null)
            {
                RealLifeOverhaul.Economy.BizBooks.Book(amount, new TransactionInfo(transactionKey), null, null, force: true);
            }
        }

        internal static bool Affords(int amount)
        {
            return SaveGameManager.Current != null && SaveGameManager.Current.Money >= amount;
        }

        internal static string Money(float amount) => amount.ToCurrencyFormat();

        internal static string T(string key) => key.GetLocalization();

        internal static string T(string key, string name, string value)
        {
            return key.GetLocalization().Replace("{" + name + "}", value);
        }

        internal static string T(string key, string name, string value, string name2, string value2)
        {
            return key.GetLocalization().Replace("{" + name + "}", value).Replace("{" + name2 + "}", value2);
        }

        internal static void Toast(NotificationType type, string key, float seconds = 8f)
        {
            Notifications.Show(type, key, null, seconds);
        }

        internal static void Toast(NotificationType type, string key, string name, string value, float seconds = 9f)
        {
            // A new dictionary each time: the game keeps it for as long as the toast is listed.
            Notifications.Show(type, key, new Dictionary<string, string>(1) { { name, value } }, seconds);
        }

        internal static void Toast(NotificationType type, string key, string name, string value, string name2, string value2, float seconds = 10f)
        {
            Notifications.Show(type, key, new Dictionary<string, string>(2) { { name, value }, { name2, value2 } }, seconds);
        }

        internal static string ClassName(LicenceClass kind)
        {
            switch (kind)
            {
                case LicenceClass.Commercial: return "reallife_lic_class_commercial".GetLocalization();
                case LicenceClass.Taxi: return "reallife_lic_class_taxi".GetLocalization();
                case LicenceClass.Motorcycle: return "reallife_lic_class_motorcycle".GetLocalization();
                case LicenceClass.Heavy: return "reallife_lic_class_heavy".GetLocalization();
                default: return "reallife_lic_class_car".GetLocalization();
            }
        }

        internal static string StandingName(LicenceStanding standing)
        {
            switch (standing)
            {
                case LicenceStanding.Valid: return "reallife_lic_standing_valid".GetLocalization();
                case LicenceStanding.Expired: return "reallife_lic_standing_expired".GetLocalization();
                case LicenceStanding.Lapsed: return "reallife_lic_standing_lapsed".GetLocalization();
                case LicenceStanding.Suspended: return "reallife_lic_standing_suspended".GetLocalization();
                default: return "reallife_lic_standing_none".GetLocalization();
            }
        }
    }
}
