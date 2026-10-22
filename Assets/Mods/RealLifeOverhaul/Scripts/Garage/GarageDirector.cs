using System;
using System.Collections.Generic;
using BAModAPI;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The Garage: the custom shop (<see cref="GarageShop"/>), the builds on the player's cars (<see cref="GarageCar"/>,
    /// <see cref="GarageTuning"/>) and the two-wheelers (<see cref="TwoWheelers"/>). Its frame cost is a comparison of
    /// what the player rides; four times a second it sees to the panels and lists, once a second it puts builds on cars
    /// the game has spawned. Anything unexpected is logged once, everything put back, and the Garage is off for the session.
    /// </summary>
    public sealed partial class GarageDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("garage", "GarageDirector", typeof(GarageDirector));   // its Update (Core/Guard)

        public static IModLogger Logger;

        public static GarageDirector Instance { get; private set; }

        private GarageShop _shop;
        private TwoWheelers _bikes;
        private float _nextQuick, _nextSlow;
        private bool _failed;
        private int _exhaustFineDay = -1;
        private float _nextPoliceLook;
        private bool _wet;
        private VehicleController _shopWanted;   // pressed on the options page: the shop opens when the menus are closed
        private int _offStamp = -1;              // with the Garage off: the stamp a whole walk last left every car at (-1: none since it was on)

        public GarageShop Shop => _shop;

        public TwoWheelers Bikes => _bikes;

        /// <summary>
        /// True while the game's panel at a dock, a rack or a stand has its button on the interact key: the mod's own
        /// interact key then does nothing else (World/NearbyInteraction).
        /// </summary>
        public static bool TakesInteractKey => Instance != null && !Instance._failed && Instance._bikes.TakesInteractKey;

        /// <summary>False once the Garage has switched itself off for the session: its shop and lists are then not offered.</summary>
        public static bool Working => Instance != null && !Instance._failed;

        private void Awake()
        {
            Instance = this;
            GarageState.Reset();
            _shop = new GarageShop(transform);
            _bikes = new TwoWheelers(transform);
            _bikes.KeptBy(this);
            ScenePrewarm.Add("garage: the city's scooter stands being found for the bike-share docks", _bikes.Places.PrewarmStands);
            GarageBay.Logger = Logger;
            WorldPanel.Logger = Logger;
            TravelList.Logger = Logger;
            GuideMark.Logger = Logger;
            gameObject.AddComponent<GarageTuning>();
        }

        private static readonly int HotUpdate = HotSpots.Slot("Garage: GarageDirector.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // switched off after its faults (Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (_failed)
            {
                return;
            }
            var gm = InstanceBehavior<GameManager>.Instance;
            if (gm == null)
            {
                return;
            }
            try
            {
                _bikes.Frame(gm);
                float now = Time.unscaledTime;
                if (now >= _nextQuick)
                {
                    _nextQuick = now + 0.25f;
                    if (GarageState.Loaded && !LoadScene.isLoading)
                    {
                        GarageBay.Tick(gm);
                        if (_shopWanted != null && !GarageList.MenuUp)
                        {
                            VehicleController wanted = _shopWanted;
                            _shopWanted = null;
                            if (!_shop.IsOpen && !_shop.Open(wanted))
                            {
                                TestRefusals.Tell(Logger, "Custom shop", TestRefusal.NothingFits, 0, "the shop could not be opened on " + wanted.name);
                            }
                        }
                        _shop.Tick(gm);
                        _bikes.Quick(gm);
                        GarageSummary.Tick(gm);
                    }
                }
                if (now >= _nextSlow)
                {
                    _nextSlow = now + 1f;
                    Slow(gm);
                }
            }
            catch (Exception e)
            {
                _failed = true;
                Logger?.Warn("Garage: switched off after something unexpected; cars are back as the game made them. " + e);
                PutEverythingBack(false);
            }
        }

        private void Slow(GameManager gm)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || gm.playerController == null || LoadScene.isLoading)
            {
                return;
            }
            if (!GarageState.Loaded)
            {
                if (!ModState.Loaded)
                {
                    return;   // LifeEvents makes the save's modData ready first
                }
                GarageState.Load(save.modData);
                Logger?.Info("Garage: ready. " + GarageState.Builds.Count + " car build(s), " + GarageState.Bikes.Count + " two-wheeler(s) in this save"
                             + (GarageState.Helmet ? ", a helmet." : "."));
            }
            // The game clears its event hub on every load, so the save hook is kept attached.
            SaveHook.Keep(_onSave ?? (_onSave = OnSaveGame));

            bool on = Settings.Garage;
            bool looks = on && Settings.GarageCosmetics;
            bool handling = on && Settings.GarageTuning;
            _wet = GarageBridges.Wet();
            bool dark = looks && Settings.GarageUnderglow && gm.timeOfDayController != null && gm.timeOfDayController.ShouldOutdoorLightsBeOn();
            int stamp = GarageState.Version * 8 + (looks ? 1 : 0) + (handling ? 2 : 0) + (_wet ? 4 : 0);

            // With the Garage off the walk only takes builds off, and stops once every car is at the off stamp.
            List<VehicleController> all = on || stamp != _offStamp ? VehicleHelper.AllPlayerVehicles : null;
            bool passed = false;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                VehicleController vehicle = all[i];
                if (!(vehicle is CarController) || vehicle.vehicleInstance == null || !GoingPublic.MineVehicle(vehicle))
                {
                    continue;
                }
                if (_shop.IsOpen && _shop.Car == vehicle)
                {
                    passed = true;   // the car in the shop: seen to when the shop closes
                    continue;
                }
                GarageBuild build = on ? GarageState.Of(vehicle.vehicleInstance.id) : null;
                GarageCar car = GarageCar.Of(vehicle, build != null);
                if (car == null)
                {
                    continue;
                }
                if (car.Stamp != stamp)
                {
                    car.Stamp = stamp;
                    if (build != null)
                    {
                        car.Show(build, looks, handling, _wet);
                    }
                    else
                    {
                        car.Clear();
                    }
                }
                if (car.HasGlow)
                {
                    car.LightGlow(dark);
                }
            }
            if (all != null)
            {
                _offStamp = !on && !passed ? stamp : -1;
            }

            LoudExhaust(gm);
            _bikes.Slow(gm);
        }

        // A straight-through exhaust is not street legal: driven hard it is noticed now and then, once a day at most.
        private void LoudExhaust(GameManager gm)
        {
            if (!Settings.Garage || !Settings.GarageTuning || !Settings.GaragePolice || !Settings.Police || _exhaustFineDay == TimeHelper.CurrentDay
                || Time.time < _nextPoliceLook || !(gm.selectedVehicle is CarController car) || car.vehicleInstance == null || car.CurrentSpeed < 10f)   // m/s
            {
                return;
            }
            if (GarageBridges.LoudnessOf(car.vehicleInstance.id) < 2)
            {
                return;
            }
            _nextPoliceLook = Time.time + 30f;
            if (UnityEngine.Random.value < 0.08f)
            {
                _exhaustFineDay = TimeHelper.CurrentDay;
                Notice("exhaust", BikeCatalogue.ExhaustFine, "reallife_garage_fine_exhaust");
            }
        }

        /// <summary>The police have noticed something about the player's vehicle: the Crime bridge deals with it if set, else the mod's fine.</summary>
        public static void Notice(string what, int fine, string messageKey)
        {
            bool handled = false;
            try
            {
                handled = GarageBridges.PoliceNotice != null && GarageBridges.PoliceNotice(what, fine);
            }
            catch (Exception e)
            {
                Logger?.Warn("Garage: the police bridge failed. " + e.Message);
            }
            if (!handled)
            {
                Crime.Fine(fine, messageKey);
            }
            Logger?.Info("Garage: noticed by the police (" + what + "): " + (handled ? "handed to the Crime feature." : "fined " + fine + "."));
        }

        private Action _onSave;   // made once: SaveHook compares it

        private void OnSaveGame()
        {
            try
            {
                GameInstance save = SaveGameManager.Current;
                if (!GarageState.Loaded || save == null)
                {
                    return;
                }
                if (save.modData == null)
                {
                    save.modData = new Dictionary<string, string>();
                }
                _bikes.RememberPositions();
                GarageState.Save(save.modData, save.VehicleInstances);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            PutEverythingBack(true);
            GarageList.Destroy();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Lets go at once of everything that holds the player's car or the view: the custom shop, the lists, "My vehicles".
        /// The feature's clean-up (Meta/FeatureList), since after the guard's switch the director's timer never runs again.
        /// </summary>
        public static void LetGo()
        {
            GarageDirector director = Instance;
            try
            {
                if (director != null && director._shop != null && director._shop.IsOpen)
                {
                    director._shop.Close(false);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            try
            {
                GarageSummary.Close();
                GarageBay.CloseList("the Garage was switched off");
                if (director != null && director._bikes != null)
                {
                    director._bikes.HideUi();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // Every car and scooter as the game made it: on the way out of the city, or when the Garage switches itself off.
        private void PutEverythingBack(bool leaving)
        {
            try
            {
                _shop.Dispose();
                GarageSummary.Close();
                if (leaving)
                {
                    GarageBay.Remove();
                    _bikes.Dispose();
                }
                else
                {
                    _bikes.HideUi();
                    GarageBay.CloseList("the Garage switched itself off");   // the bay's panel stays: car ownership's service is still ordered there
                }
                List<VehicleController> all = VehicleHelper.AllPlayerVehicles;
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i] == null)
                    {
                        continue;
                    }
                    GarageCar car = all[i].GetComponent<GarageCar>();
                    if (car != null)
                    {
                        car.Clear();
                        Destroy(car);
                    }
                    BikeMount mount = all[i].GetComponent<BikeMount>();
                    if (mount != null)
                    {
                        mount.Detach();
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Garage: not everything could be put back. " + e);
            }
        }
    }
}
