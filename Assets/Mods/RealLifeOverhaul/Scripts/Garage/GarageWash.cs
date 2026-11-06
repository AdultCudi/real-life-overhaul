using System;
using System.Reflection;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game's car wash in a repair bay, kept from holding a car for ever (<see cref="WashRules"/> says why it can): a
    /// car without its own spray or wash sound is given stand-ins before the wash can be pressed, and a wash that stops
    /// anyway is ended as the game's own end does. One compare four times a second while nothing washes; nothing saved
    /// or sent.
    /// </summary>
    public static class GarageWash
    {
        private const string StandInName = "RealLifeOverhaul Wash Stand-in";

        private static readonly FieldInfo SprayField = GameContract.WashSpray.Field;   // the game's private pieces, resolved once
        private static readonly FieldInfo SoundField = GameContract.WashSound.Field;

        private static CarController _readied;   // the last car looked at for its wash parts
        private static WashWatch _watch;

        private static IModLogger Logger => GarageDirector.Logger;

        /// <summary>A car about to be offered the wash gets its spray and sound, its own or stand-ins; once a car.</summary>
        public static void Ready(VehicleController vehicle)
        {
            if (!(vehicle is CarController car) || ReferenceEquals(car, _readied))
            {
                return;
            }
            _readied = car;
            if (SprayField == null || SoundField == null)
            {
                return;   // the watch still ends a wash that stops
            }
            try
            {
                bool spray = SprayField.GetValue(car) as GameObject != null;
                bool sound = SoundField.GetValue(car) as AudioSource != null;
                if (spray && sound)
                {
                    return;
                }
                Transform holder = car.transform.Find(StandInName);
                if (holder == null)
                {
                    var made = new GameObject(StandInName);
                    holder = made.transform;
                    holder.SetParent(car.transform, false);
                }
                if (!spray)
                {
                    var stand = new GameObject("Spray");
                    stand.transform.SetParent(holder, false);
                    stand.SetActive(false);   // the wash switches it on and off: nothing in it to see
                    SprayField.SetValue(car, stand);
                }
                if (!sound)
                {
                    AudioSource source = holder.gameObject.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.spatialBlend = 1f;
                    SoundField.SetValue(car, source);   // plays the car's own wash sounds if it has any, nothing otherwise
                }
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Garage check (wash): " + car.name + " has " + (spray ? "its spray" : "no spray") + " and " + (sound ? "its wash sound" : "no wash sound")
                                 + " of its own; stand-ins given for what it lacks, so the game's wash in a bay runs to its end: PASS.");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);   // the watch below still ends a wash that stops
            }
        }

        /// <summary>Four times a second: a wash of the player's car that has stopped without ending is ended.</summary>
        public static void Watch(GameManager gm)
        {
            CarController car = gm.selectedVehicle as CarController;
            if (car == null || !car.isWashing)
            {
                _watch.Car = 0;
                return;
            }
            float dirt = car.vehicleInstance != null ? car.vehicleInstance.dirtiness : 0f;
            if (_watch.Step(car.GetInstanceID(), true, dirt, Time.time))
            {
                End(car, gm);
            }
        }

        // The game's own end of a wash (WashCoroutine after its loop), for one whose coroutine has stopped.
        private static void End(CarController car, GameManager gm)
        {
            try
            {
                car.isWashing = false;
                GameObject spray = SprayField != null ? SprayField.GetValue(car) as GameObject : null;
                if (spray != null)
                {
                    spray.SetActive(false);
                }
                AudioSource sound = SoundField != null ? SoundField.GetValue(car) as AudioSource : null;
                if (sound != null)
                {
                    sound.loop = false;
                    sound.Stop();
                }
                GarageShop shop = GarageDirector.Working ? GarageDirector.Instance.Shop : null;
                bool held = shop != null && shop.IsOpen && ReferenceEquals(shop.Car, car);
                if (!held)
                {
                    car.SetFreeze(false);   // the custom shop's own hold goes when it closes
                }
                car.SetDirtiness(0f);
                if (gm.playerController != null)
                {
                    gm.playerController.UnsetVehicleNavigationBlocker(VehicleNavigationBlocker.WashingVehicle);
                }
                Logger?.Warn("Garage: the game's car wash stopped without ending on " + car.name + " (its own script failed or was stopped), which held the car and kept the player in it; "
                             + "both are let go as the wash's end does, and the car is clean.");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Forgets the city's cars: with the Garage's panel taken off, and as the city goes.</summary>
        public static void Forget()
        {
            _readied = null;
            _watch = default;
        }
    }
}
