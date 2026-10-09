using System;
using System.Collections.Generic;
using BAModAPI;
using Extensions;
using Localizor;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What driving licences ask of a law feature, if there is one (a criminal record,
    /// custody). Whoever provides it sets <see cref="Licences.Law"/>; until then
    /// <see cref="DefaultLaw"/> does what the mod did before there was one.
    /// </summary>
    public interface ILicenceLawBridge
    {
        /// <summary>Something for the record. <paramref name="offenceId"/> is one of the ids in <see cref="LicenceOffences"/>, or "hit_and_run".</summary>
        void Record(string offenceId, bool arrested);

        /// <summary>Takes the player into custody for some hours. False if it could not: the caller then settles it with the fine alone.</summary>
        bool Custody(int hours);
    }

    /// <summary>Told when something about the player's licences changes: for insurance, employers, anything that cares. All optional.</summary>
    public interface ILicenceListener
    {
        void PointsAdded(PointCode code, int total);
        void DrivingChanged(LicenceClass kind, LicenceStanding standing);
    }

    /// <summary>The offence ids this feature reports to a law bridge.</summary>
    public static class LicenceOffences
    {
        public const string Unlicensed = "driving_unlicensed";
        public const string Suspended = "driving_suspended";
    }

    /// <summary>Today's behaviour without a law feature: the mod's own short memory of arrests, and its time-skip custody.</summary>
    public sealed class DefaultLaw : ILicenceLawBridge
    {
        public void Record(string offenceId, bool arrested)
        {
            if (arrested)
            {
                LicenceState.LastArrestDay = TimeHelper.CurrentDay;
            }
        }

        public bool Custody(int hours)
        {
            return PoliceAttention.TakeIntoCustody(hours);
        }
    }

    /// <summary>
    /// The one door to licences for the rest of the mod. Safe to read at any time: before a save
    /// is loaded, with the options off, with nothing held.
    ///     Licences.Driving.Has(LicenceClass.Taxi)     may the player drive for hire?
    /// </summary>
    public static class Licences
    {
        private const string FeeCategory = "ba:transactioncategory_licensingfees";

        private static readonly List<ILicenceListener> Listeners = new List<ILicenceListener>(2);
        private static readonly DefaultLaw Fallback = new DefaultLaw();
        private static ILicenceLawBridge _law;

        public static IModLogger Logger;

        /// <summary>The law feature's side of things; never null. Set it to null to go back to the default.</summary>
        public static ILicenceLawBridge Law
        {
            get => _law ?? Fallback;
            set => _law = value;
        }

        public static void Listen(ILicenceListener listener)
        {
            if (listener != null && !Listeners.Contains(listener))
            {
                Listeners.Add(listener);
            }
        }

        public static void Unlisten(ILicenceListener listener)
        {
            Listeners.Remove(listener);
        }

        public static class Driving
        {
            /// <summary>False when the whole requirement is switched off: everyone may then drive anything, as in the base game.</summary>
            public static bool Required => Settings.DrivingLicence;

            /// <summary>May the player drive this class right now? Always true with the requirement off or before the save is ready.</summary>
            public static bool Has(LicenceClass kind)
            {
                return !Required || !LicenceState.Loaded || LicenceState.Valid(kind, TimeHelper.CurrentDay);
            }

            public static LicenceStanding Standing(LicenceClass kind)
            {
                return LicenceState.Loaded ? LicenceState.Standing(kind, TimeHelper.CurrentDay) : LicenceStanding.None;
            }

            public static int Points => LicenceState.Loaded ? LicenceState.PointsOn(TimeHelper.CurrentDay) : 0;

            public static bool Suspended => LicenceState.Loaded && LicenceState.SuspendedUntil > TimeHelper.CurrentDay;

            /// <summary>
            /// Which licence a vehicle needs. False for what needs none: scooters, hand trucks, flatbed
            /// trolleys, anything without an engine. Vans and trucks are commercial, every other car is a car.
            /// </summary>
            public static bool ClassOf(VehicleController vehicle, out LicenceClass kind)
            {
                kind = LicenceClass.Car;
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
                return true;
            }

            public static bool MayDrive(VehicleController vehicle)
            {
                return !ClassOf(vehicle, out LicenceClass kind) || Has(kind);
            }
        }

        // ---------------------------------------------------------------- telling whoever listens

        internal static void TellPoints(PointCode code, int total)
        {
            for (int i = 0; i < Listeners.Count; i++)
            {
                try
                {
                    Listeners[i].PointsAdded(code, total);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
        }

        internal static void TellDriving(LicenceClass kind)
        {
            LicenceStanding standing = Driving.Standing(kind);
            for (int i = 0; i < Listeners.Count; i++)
            {
                try
                {
                    Listeners[i].DrivingChanged(kind, standing);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
        }

        // ---------------------------------------------------------------- money and words

        /// <summary>A fee that is only taken if it can be afforded in full. False (and nothing taken) if not.</summary>
        internal static bool Pay(int amount, string transactionKey)
        {
            return amount <= 0 || (SaveGameManager.Current != null
                && GameManager.ChangeMoneySafe(-amount, new TransactionInfo(transactionKey, FeeCategory), null, null, force: false));
        }

        /// <summary>A fine or a fee that is owed whatever is in the account (the game allows the red).</summary>
        internal static void Charge(int amount, string transactionKey)
        {
            if (amount > 0 && SaveGameManager.Current != null)
            {
                GameManager.ChangeMoneySafe(-amount, new TransactionInfo(transactionKey), null, null, force: true);
            }
        }

        internal static void Give(int amount, string transactionKey)
        {
            if (amount > 0 && SaveGameManager.Current != null)
            {
                GameManager.ChangeMoneySafe(amount, new TransactionInfo(transactionKey), null, null, force: true);
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
            // A new dictionary each time: the game keeps hold of it for as long as the toast is listed.
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
